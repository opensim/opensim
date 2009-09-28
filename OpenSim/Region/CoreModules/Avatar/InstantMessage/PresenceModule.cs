/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.Avatar.InstantMessage
{
    public class PresenceModule : IRegionModule, IPresenceModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;
        private bool m_Gridmode = false;

        // some default scene for doing things that aren't connected to a specific scene. Avoids locking.
        private Scene m_initialScene;

        private List<Scene> m_Scenes = new List<Scene>();

        // we currently are only interested in root-agents. If the root isn't here, we don't know the region the
        // user is in, so we have to ask the messaging server anyway.
        private Dictionary<UUID, Scene> m_RootAgents =
                new Dictionary<UUID, Scene>();

        public event PresenceChange OnPresenceChange;
        public event BulkPresenceData OnBulkPresenceData;

        public void Initialise(Scene scene, IConfigSource config)
        {
            lock (m_Scenes)
            {
                // This is a shared module; Initialise will be called for every region on this server.
                // Only check config once for the first region.
                if (m_Scenes.Count == 0)
                {
                    IConfig cnf = config.Configs["Messaging"];
                    if (cnf != null && cnf.GetString(
                            "PresenceModule", "PresenceModule") !=
                            "PresenceModule")
                        return;

                    cnf = config.Configs["Startup"];
                    if (cnf != null)
                        m_Gridmode = cnf.GetBoolean("gridmode", false);

                    m_Enabled = true;

                    m_initialScene = scene;
                }

                if (m_Gridmode)
                    NotifyMessageServerOfStartup(scene);

                m_Scenes.Add(scene);
            }

            scene.RegisterModuleInterface<IPresenceModule>(this);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnSetRootAgentScene += OnSetRootAgentScene;
            scene.EventManager.OnMakeChildAgent += OnMakeChildAgent;
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
            if (!m_Gridmode || !m_Enabled)
                return;

            if (OnPresenceChange != null)
            {
                lock (m_RootAgents)
                {
                    // on shutdown, users are kicked, too
                    foreach (KeyValuePair<UUID, Scene> pair in m_RootAgents)
                    {
                        OnPresenceChange(new PresenceInfo(pair.Key, UUID.Zero));
                    }
                }
            }

            lock (m_Scenes)
            {
                foreach (Scene scene in m_Scenes)
                    NotifyMessageServerOfShutdown(scene);
            }
        }

        public string Name
        {
            get { return "PresenceModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public void RequestBulkPresenceData(UUID[] users)
        {
            if (OnBulkPresenceData != null)
            {
                PresenceInfo[] result = new PresenceInfo[users.Length];
                if (m_Gridmode)
                {
                    // first check the local information
                    List<UUID> uuids = new List<UUID>(); // the uuids to check remotely
                    List<int> indices = new List<int>(); // just for performance.
                    lock (m_RootAgents)
                    {
                        for (int i = 0; i < uuids.Count; ++i)
                        {
                            Scene scene;
                            if (m_RootAgents.TryGetValue(users[i], out scene)) 
                            {
                                result[i] = new PresenceInfo(users[i], scene.RegionInfo.RegionID);
                            }
                            else
                            {
                                uuids.Add(users[i]);
                                indices.Add(i);
                            }
                        }
                    }

                    // now we have filtered out all the local root agents. The rest we have to request info about
                    Dictionary<UUID, FriendRegionInfo> infos = m_initialScene.GetFriendRegionInfos(uuids);
                    for (int i = 0; i < uuids.Count; ++i)
                    {
                        FriendRegionInfo info;
                        if (infos.TryGetValue(uuids[i], out info) && info.isOnline)
                        {
                            UUID regionID = info.regionID;
                            if (regionID == UUID.Zero)
                            {
                                // TODO this is the old messaging-server protocol; only the regionHandle is available.
                                // Fetch region-info to get the id
                                uint x = 0, y = 0;
                                Utils.LongToUInts(info.regionHandle, out x, out y);
                                GridRegion regionInfo = m_initialScene.GridService.GetRegionByPosition(m_initialScene.RegionInfo.ScopeID,
                                    (int)x, (int)y);
                                regionID = regionInfo.RegionID;
                            }
                            result[indices[i]] = new PresenceInfo(uuids[i], regionID);
                        }
                        else result[indices[i]] = new PresenceInfo(uuids[i], UUID.Zero);
                    }
                }
                else
                {
                    // in standalone mode, we have all the info locally available.
                    lock (m_RootAgents)
                    {
                        for (int i = 0; i < users.Length; ++i)
                        {
                            Scene scene;
                            if (m_RootAgents.TryGetValue(users[i], out scene))
                            {
                                result[i] = new PresenceInfo(users[i], scene.RegionInfo.RegionID);
                            }
                            else
                            {
                                result[i] = new PresenceInfo(users[i], UUID.Zero);
                            }
                        }
                    }
                }

                // tell everyone
                OnBulkPresenceData(result);
            }
        }

        // new client doesn't mean necessarily that user logged in, it just means it entered one of the
        // the regions on this server
        public void OnNewClient(IClientAPI client)
        {
            client.OnConnectionClosed += OnConnectionClosed;
            client.OnLogout += OnLogout;

            // KLUDGE: See handler for details.
            client.OnEconomyDataRequest += OnEconomyDataRequest;
        }

        // connection closed just means *one* client connection has been closed. It doesn't mean that the
        // user has logged off; it might have just TPed away.
        public void OnConnectionClosed(IClientAPI client)
        {
            // TODO: Have to think what we have to do here...
            // Should we just remove the root from the list (if scene matches)?
            if (!(client.Scene is Scene))
                return;
            Scene scene = (Scene)client.Scene;

            lock (m_RootAgents)
            {
                Scene rootScene;
                if (!(m_RootAgents.TryGetValue(client.AgentId, out rootScene)) || scene != rootScene)
                    return;

                m_RootAgents.Remove(client.AgentId);
            }

            // Should it have logged off, we'll do the logout part in OnLogout, even if no root is stored
            // anymore. It logged off, after all...
        }

        // Triggered when the user logs off.
        public void OnLogout(IClientAPI client)
        {
            if (!(client.Scene is Scene))
                return;
            Scene scene = (Scene)client.Scene;

            // On logout, we really remove the client from rootAgents, even if the scene doesn't match
            lock (m_RootAgents)
            {
                if (m_RootAgents.ContainsKey(client.AgentId)) m_RootAgents.Remove(client.AgentId);
            }

            // now inform the messaging server and anyone who is interested
            NotifyMessageServerOfAgentLeaving(client.AgentId, scene.RegionInfo.RegionID, scene.RegionInfo.RegionHandle);
            if (OnPresenceChange != null) OnPresenceChange(new PresenceInfo(client.AgentId, UUID.Zero));
        }

        public void OnSetRootAgentScene(UUID agentID, Scene scene)
        {
            // OnSetRootAgentScene can be called from several threads at once (with different agentID).
            // Concurrent access to m_RootAgents is prone to failure on multi-core/-processor systems without
            // correct locking).
            lock (m_RootAgents)
            {
                Scene rootScene;
                if (m_RootAgents.TryGetValue(agentID, out rootScene) && scene == rootScene)
                {
                    return;
                }
                m_RootAgents[agentID] = scene;
            }
            // inform messaging server that agent changed the region
            NotifyMessageServerOfAgentLocation(agentID, scene.RegionInfo.RegionID, scene.RegionInfo.RegionHandle);
        }

        private void OnEconomyDataRequest(UUID agentID)
        {
            // KLUDGE: This is the only way I found to get a message (only) after login was completed and the
            // client is connected enough to receive UDP packets.
            // This packet seems to be sent only once, just after connection was established to the first
            // region after login.
            // We use it here to trigger a presence update; the old update-on-login was never be heard by
            // the freshly logged in viewer, as it wasn't connected to the region at that time.
            // TODO: Feel free to replace this by a better solution if you find one.

            // get the agent. This should work every time, as we just got a packet from it
            ScenePresence agent = null;
            lock (m_Scenes)
            {
                foreach (Scene scene in m_Scenes)
                {
                    agent = scene.GetScenePresence(agentID);
                    if (agent != null) break;
                }
            }

            // just to be paranoid...
            if (agent == null)
            {
                m_log.ErrorFormat("[PRESENCE]: Got a packet from agent {0} who can't be found anymore!?", agentID);
                return;
            }

            // we are a bit premature here, but the next packet will switch this child agent to root.
            if (OnPresenceChange != null) OnPresenceChange(new PresenceInfo(agentID, agent.Scene.RegionInfo.RegionID));
        }

        public void OnMakeChildAgent(ScenePresence agent)
        {
            // OnMakeChildAgent can be called from several threads at once (with different agent).
            // Concurrent access to m_RootAgents is prone to failure on multi-core/-processor systems without
            // correct locking).
            lock (m_RootAgents)
            {
                Scene rootScene;
                if (m_RootAgents.TryGetValue(agent.UUID, out rootScene) && agent.Scene == rootScene)
                {
                    m_RootAgents.Remove(agent.UUID);
                }
            }
            // don't notify the messaging-server; either this agent just had been downgraded and another one will be upgraded
            // to root momentarily (which will notify the messaging-server), or possibly it will be closed in a moment,
            // which will update the messaging-server, too.
        }

        private void NotifyMessageServerOfStartup(Scene scene)
        {
            Hashtable xmlrpcdata = new Hashtable();
            xmlrpcdata["RegionUUID"] = scene.RegionInfo.RegionID.ToString();
            ArrayList SendParams = new ArrayList();
            SendParams.Add(xmlrpcdata);
            try
            {
                XmlRpcRequest UpRequest = new XmlRpcRequest("region_startup", SendParams);
                XmlRpcResponse resp = UpRequest.Send(scene.CommsManager.NetworkServersInfo.MessagingURL, 5000);

                Hashtable responseData = (Hashtable)resp.Value;
                if (responseData == null || (!responseData.ContainsKey("success")) || (string)responseData["success"] != "TRUE")
                {
                    m_log.ErrorFormat("[PRESENCE]: Failed to notify message server of region startup for region {0}", scene.RegionInfo.RegionName);
                }
            }
            catch (WebException)
            {
                m_log.ErrorFormat("[PRESENCE]: Failed to notify message server of region startup for region {0}", scene.RegionInfo.RegionName);
            }
        }

        private void NotifyMessageServerOfShutdown(Scene scene)
        {
            if (m_Scenes[0].CommsManager.NetworkServersInfo.MessagingURL == string.Empty)
                return;
            
            Hashtable xmlrpcdata = new Hashtable();
            xmlrpcdata["RegionUUID"] = scene.RegionInfo.RegionID.ToString();
            ArrayList SendParams = new ArrayList();
            SendParams.Add(xmlrpcdata);
            try
            {
                XmlRpcRequest DownRequest = new XmlRpcRequest("region_shutdown", SendParams);
                XmlRpcResponse resp = DownRequest.Send(scene.CommsManager.NetworkServersInfo.MessagingURL, 5000);

                Hashtable responseData = (Hashtable)resp.Value;
                if ((!responseData.ContainsKey("success")) || (string)responseData["success"] != "TRUE")
                {
                    m_log.ErrorFormat("[PRESENCE]: Failed to notify message server of region shutdown for region {0}", scene.RegionInfo.RegionName);
                }
            }
            catch (WebException)
            {
                m_log.ErrorFormat("[PRESENCE]: Failed to notify message server of region shutdown for region {0}", scene.RegionInfo.RegionName);
            }
        }

        private void NotifyMessageServerOfAgentLocation(UUID agentID, UUID region, ulong regionHandle)
        {
            if (m_Scenes[0].CommsManager.NetworkServersInfo.MessagingURL == string.Empty)
                return;

            Hashtable xmlrpcdata = new Hashtable();
            xmlrpcdata["AgentID"] = agentID.ToString();
            xmlrpcdata["RegionUUID"] = region.ToString();
            xmlrpcdata["RegionHandle"] = regionHandle.ToString();
            ArrayList SendParams = new ArrayList();
            SendParams.Add(xmlrpcdata);
            try
            {
                XmlRpcRequest LocationRequest = new XmlRpcRequest("agent_location", SendParams);
                XmlRpcResponse resp = LocationRequest.Send(m_Scenes[0].CommsManager.NetworkServersInfo.MessagingURL, 5000);

                Hashtable responseData = (Hashtable)resp.Value;
                if ((!responseData.ContainsKey("success")) || (string)responseData["success"] != "TRUE")
                {
                    m_log.ErrorFormat("[PRESENCE]: Failed to notify message server of agent location for {0}", agentID.ToString());
                }
            }
            catch (WebException)
            {
                m_log.ErrorFormat("[PRESENCE]: Failed to notify message server of agent location for {0}", agentID.ToString());
            }
        }

        private void NotifyMessageServerOfAgentLeaving(UUID agentID, UUID region, ulong regionHandle)
        {
            if (m_Scenes[0].CommsManager.NetworkServersInfo.MessagingURL == string.Empty)
                return;

            Hashtable xmlrpcdata = new Hashtable();
            xmlrpcdata["AgentID"] = agentID.ToString();
            xmlrpcdata["RegionUUID"] = region.ToString();
            xmlrpcdata["RegionHandle"] = regionHandle.ToString();
            ArrayList SendParams = new ArrayList();
            SendParams.Add(xmlrpcdata);
            try
            {
                XmlRpcRequest LeavingRequest = new XmlRpcRequest("agent_leaving", SendParams);
                XmlRpcResponse resp = LeavingRequest.Send(m_Scenes[0].CommsManager.NetworkServersInfo.MessagingURL, 5000);

                Hashtable responseData = (Hashtable)resp.Value;
                if ((!responseData.ContainsKey("success")) || (string)responseData["success"] != "TRUE")
                {
                    m_log.ErrorFormat("[PRESENCE]: Failed to notify message server of agent leaving for {0}", agentID.ToString());
                }
            }
            catch (WebException)
            {
                m_log.ErrorFormat("[PRESENCE]: Failed to notify message server of agent leaving for {0}", agentID.ToString());
            }
        }
    }
}
