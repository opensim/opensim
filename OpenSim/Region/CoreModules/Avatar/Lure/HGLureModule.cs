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

using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using Mono.Addins;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Connectors.Hypergrid;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.Avatar.Lure
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "HGLureModule")]
    public class HGLureModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly List<Scene> m_scenes = [];

        private IMessageTransferModule m_TransferModule = null;
        private bool m_Enabled = false;

        private GridInfo m_thisGridInfo;

        private readonly ExpiringCacheOS<UUID, GridInstantMessage> m_PendingLures = new(3600000);

        public void Initialise(IConfigSource config)
        {
            if (config.Configs["Messaging"] != null)
            {
                if (config.Configs["Messaging"].GetString("LureModule", string.Empty) == Name)
                {
                    m_Enabled = true;
                }
            }
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock (m_scenes)
            {
                m_scenes.Add(scene);
                if(m_thisGridInfo == null)
                    m_thisGridInfo = scene.SceneGridInfo;
                scene.EventManager.OnIncomingInstantMessage += OnIncomingInstantMessage;
                scene.EventManager.OnNewClient += OnNewClient;
            }
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            if (m_TransferModule == null)
            {
                m_TransferModule = scene.RequestModuleInterface<IMessageTransferModule>();
                if (m_TransferModule == null)
                {
                    m_log.Error("[LURE MODULE]: No message transfer module, lures will not work!");

                    m_Enabled = false;
                    m_scenes.Clear();
                    scene.EventManager.OnNewClient -= OnNewClient;
                    scene.EventManager.OnIncomingInstantMessage -= OnIncomingInstantMessage;
                }
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock (m_scenes)
            {
                m_scenes.Remove(scene);
                scene.EventManager.OnNewClient -= OnNewClient;
                scene.EventManager.OnIncomingInstantMessage -= OnIncomingInstantMessage;
            }
        }

        void OnNewClient(IClientAPI client)
        {
            client.OnInstantMessage += OnInstantMessage;
            client.OnStartLure += OnStartLure;
            client.OnTeleportLureRequest += OnTeleportLureRequest;
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
            m_thisGridInfo = null;
        }

        public string Name
        {
            get { return "HGLureModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        void OnInstantMessage(IClientAPI client, GridInstantMessage im)
        {
            if (im.dialog == (byte)InstantMessageDialog.RequestLure)
            {
                m_TransferModule?.SendInstantMessage(im, delegate (bool success) { }, true);
            }
        }

        void OnIncomingInstantMessage(GridInstantMessage im)
        {
            if (im.dialog == (byte)InstantMessageDialog.RequestTeleport
                || im.dialog == (byte)InstantMessageDialog.GodLikeRequestTeleport)
            {
                UUID sessionID = new(im.imSessionID);

                if (!m_PendingLures.Contains(sessionID))
                {
                    m_log.DebugFormat("[HG LURE MODULE]: RequestTeleport sessionID={0}, regionID={1}, message={2}", im.imSessionID, im.RegionID, im.message);
                    m_PendingLures.Add(sessionID, im, 7200000); // 2 hours
                }

                // Forward. We do this, because the IM module explicitly rejects
                // IMs of this type
                m_TransferModule?.SendInstantMessage(im, delegate(bool success) { }, true);
            }
            else if (im.dialog == (byte)InstantMessageDialog.RequestLure)
            {
                m_TransferModule?.SendInstantMessage(im, delegate (bool success) { }, true);
            }
        }

        public void OnStartLure(byte lureType, string message, UUID targetid, IClientAPI client)
        {
            if (client.Scene is not Scene scene)
                return; 

            ScenePresence presence = scene.GetScenePresence(client.AgentId);

            message += "@" + m_thisGridInfo.GateKeeperURLNoEndSlash;

            m_log.DebugFormat("[HG LURE MODULE]: TP invite with message {0}", message);

            UUID sessionID = UUID.Random();

            GridInstantMessage m = new(scene, client.AgentId,
                    client.FirstName+" "+client.LastName, targetid,
                    (byte)InstantMessageDialog.RequestTeleport, false,
                    message, sessionID, false, presence.AbsolutePosition,
                    [], true);
            m.RegionID = client.Scene.RegionInfo.RegionID.Guid;

            m_log.Debug($"[HG LURE MODULE]: RequestTeleport sessionID={m.imSessionID}, regionID={m.RegionID}, message={m.message}");
            m_PendingLures.Add(sessionID, m, 7200000); // 2 hours

            m_TransferModule?.SendInstantMessage(m, delegate(bool success) { }, true);
        }

        public void OnTeleportLureRequest(UUID lureID, uint teleportFlags, IClientAPI client)
        {
            if (client.Scene is not Scene)
                return;

            if (m_PendingLures.TryGetValue(lureID, out GridInstantMessage im))
            {
                m_PendingLures.Remove(lureID);
                Lure(client, teleportFlags, im);
            }
            else
                m_log.DebugFormat("[HG LURE MODULE]: pending lure {0} not found", lureID);

        }

        private void Lure(IClientAPI client, uint teleportflags, GridInstantMessage im)
        {
            Scene scene = client.Scene as Scene;
            UUID regionID = new(im.RegionID);
            GridRegion region = scene.GridService.GetRegionByUUID(scene.RegionInfo.ScopeID, regionID);
            if (region != null)
                scene.RequestTeleportLocation(client, region.RegionHandle, im.Position + new Vector3(0.5f, 0.5f, 0f), Vector3.UnitX, teleportflags);
            else // we don't have that region here. Check if it's HG
            {
                string[] parts = im.message.Split(['@']);
                if (parts.Length > 1)
                {
                    string url = parts[parts.Length - 1]; // the last part
                    if (m_thisGridInfo.IsLocalGrid(url, true) == 0)
                    {
                        m_log.Debug($"[HG LURE MODULE]: Luring agent to grid {url} region {im.RegionID} position {im.Position}");
                        GatekeeperServiceConnector gConn = new GatekeeperServiceConnector();
                        GridRegion gatekeeper = new GridRegion { ServerURI = url };
                        string homeURI = scene.GetAgentHomeURI(client.AgentId);

                        GridRegion finalDestination = gConn.GetHyperlinkRegion(gatekeeper, regionID, client.AgentId, homeURI, out string message);
                        if (finalDestination != null)
                        {
                            ScenePresence sp = scene.GetScenePresence(client.AgentId);
                            IEntityTransferModule transferMod = scene.RequestModuleInterface<IEntityTransferModule>();

                            if (transferMod != null && sp != null)
                            {
                                if (message != null)
                                    sp.ControllingClient.SendAgentAlertMessage(message, true);

                                transferMod.DoTeleport(
                                    sp, gatekeeper, finalDestination, im.Position + new Vector3(0.5f, 0.5f, 0f),
                                    Vector3.UnitX, teleportflags);
                            }
                        }
                        else
                        {
                            m_log.Info("$[HG LURE MODULE]: Lure failed: {message}");
                            client.SendAgentAlertMessage(message, true);
                        }
                    }
                }
            }
        }
    }
}