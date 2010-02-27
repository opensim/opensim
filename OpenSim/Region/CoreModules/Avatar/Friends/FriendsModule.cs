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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Friends;
using OpenSim.Server.Base;
using OpenSim.Framework.Servers.HttpServer;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;
using PresenceInfo = OpenSim.Services.Interfaces.PresenceInfo;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.Avatar.Friends
{
    public class FriendsModule : ISharedRegionModule, IFriendsModule
    {
        protected class UserFriendData
        {
            public UUID PrincipalID;
            public FriendInfo[] Friends;
            public int Refcount;
            public UUID RegionID;

            public bool IsFriend(string friend)
            {
                foreach (FriendInfo fi in Friends)
                {
                    if (fi.Friend == friend)
                        return true;
                }

                return false;
            }
        }
            
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected List<Scene> m_Scenes = new List<Scene>();

        protected IPresenceService m_PresenceService = null;
        protected IFriendsService m_FriendsService = null;
        protected FriendsSimConnector m_FriendsSimConnector;

        protected Dictionary<UUID, UserFriendData> m_Friends =
                new Dictionary<UUID, UserFriendData>();

        protected IPresenceService PresenceService
        {
            get
            {
                if (m_PresenceService == null)
                {
                    if (m_Scenes.Count > 0)
                        m_PresenceService = m_Scenes[0].RequestModuleInterface<IPresenceService>();
                }

                return m_PresenceService;
            }
        }

        protected IFriendsService FriendsService
        {
            get
            {
                if (m_FriendsService == null)
                {
                    if (m_Scenes.Count > 0)
                        m_FriendsService = m_Scenes[0].RequestModuleInterface<IFriendsService>();
                }

                return m_FriendsService;
            }
        }

        protected IGridService GridService
        {
            get
            {
                return m_Scenes[0].GridService;
            }
        }

        public void Initialise(IConfigSource config)
        {
            IConfig friendsConfig = config.Configs["Friends"];
            if (friendsConfig != null)
            {
                int mPort = friendsConfig.GetInt("Port", 0);

                string connector = friendsConfig.GetString("Connector", String.Empty);
                Object[] args = new Object[] { config };

                m_FriendsService = ServerUtils.LoadPlugin<IFriendsService>(connector, args);
                m_FriendsSimConnector = new FriendsSimConnector();

                // Instantiate the request handler
                IHttpServer server = MainServer.GetHttpServer((uint)mPort);
                server.AddStreamHandler(new FriendsRequestHandler(this));

            }

            if (m_FriendsService == null)
            {
                m_log.Error("[FRIENDS]: No Connector defined in section Friends, or filed to load, cannot continue");
                throw new Exception("Connector load error");
            }

        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            m_Scenes.Add(scene);
            scene.RegisterModuleInterface<IFriendsModule>(this);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClientClosed += OnClientClosed;
            scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
            scene.EventManager.OnMakeChildAgent += OnMakeChildAgent;
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            m_Scenes.Remove(scene);
        }

        public string Name
        {
            get { return "FriendsModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void OfferFriendship(UUID fromUserId, IClientAPI toUserClient, string offerMessage)
        {
        }

        public uint GetFriendPerms(UUID principalID, UUID friendID)
        {
            if (!m_Friends.ContainsKey(principalID))
                return 0;

            UserFriendData data = m_Friends[principalID];

            foreach (FriendInfo fi in data.Friends)
            {
                if (fi.Friend == friendID.ToString())
                    return (uint)fi.TheirFlags;
            }
            return 0;
        }

        private void OnNewClient(IClientAPI client)
        {
            client.OnInstantMessage += OnInstantMessage;
            client.OnApproveFriendRequest += OnApproveFriendRequest;
            client.OnDenyFriendRequest += OnDenyFriendRequest;
            client.OnTerminateFriendship += OnTerminateFriendship;

            client.OnGrantUserRights += OnGrantUserRights;

            client.OnLogout += OnLogout;
            client.OnEconomyDataRequest += SendPresence;

            if (m_Friends.ContainsKey(client.AgentId))
            {
                m_Friends[client.AgentId].Refcount++;
                return;
            }

            UserFriendData newFriends = new UserFriendData();

            newFriends.PrincipalID = client.AgentId;
            newFriends.Friends = m_FriendsService.GetFriends(client.AgentId);
            newFriends.Refcount = 1;
            newFriends.RegionID = UUID.Zero;

            m_Friends.Add(client.AgentId, newFriends);
            
            StatusChange(client.AgentId, true);
        }

        private void OnClientClosed(UUID agentID, Scene scene)
        {
            if (m_Friends.ContainsKey(agentID))
            {
                if (m_Friends[agentID].Refcount == 1)
                    m_Friends.Remove(agentID);
                else
                    m_Friends[agentID].Refcount--;
            }
        }

        private void OnLogout(IClientAPI client)
        {
            m_Friends.Remove(client.AgentId);

            StatusChange(client.AgentId, false);
        }

        private void OnMakeRootAgent(ScenePresence sp)
        {
            UUID agentID = sp.ControllingClient.AgentId;

            if (m_Friends.ContainsKey(agentID))
            {
                if (m_Friends[agentID].RegionID == UUID.Zero)
                {
                    m_Friends[agentID].Friends =
                            m_FriendsService.GetFriends(agentID);
                }
                m_Friends[agentID].RegionID =
                        sp.ControllingClient.Scene.RegionInfo.RegionID;
            }
        }


        private void OnMakeChildAgent(ScenePresence sp)
        {
            UUID agentID = sp.ControllingClient.AgentId;

            if (m_Friends.ContainsKey(agentID))
            {
                if (m_Friends[agentID].RegionID == sp.ControllingClient.Scene.RegionInfo.RegionID)
                    m_Friends[agentID].RegionID = UUID.Zero;
            }
        }

        private void SendPresence(UUID agentID)
        {
            if (!m_Friends.ContainsKey(agentID))
            {
                m_log.DebugFormat("[FRIENDS MODULE]: agent {0} not found in local cache", agentID);
                return;
            }

            IClientAPI client = LocateClientObject(agentID);
            if (client == null)
            {
                m_log.DebugFormat("[FRIENDS MODULE]: agent's client {0} not found in local scene", agentID);
                return;
            }

            List<string> friendList = new List<string>();

            foreach (FriendInfo fi in m_Friends[agentID].Friends)
            {
                if ((fi.TheirFlags & 1) != 0)
                    friendList.Add(fi.Friend);
            }

            PresenceInfo[] presence = PresenceService.GetAgents(friendList.ToArray());

            List<UUID> online = new List<UUID>();

            foreach (PresenceInfo pi in presence)
            {
                if (pi.Online)
                    online.Add(new UUID(pi.UserID));
            }

            client.SendAgentOnline(online.ToArray());
        }

        //
        // Find the client for a ID
        //
        private IClientAPI LocateClientObject(UUID agentID)
        {
            Scene scene=GetClientScene(agentID);
            if(scene == null)
                return null;

            ScenePresence presence=scene.GetScenePresence(agentID);
            if(presence == null)
                return null;

            return presence.ControllingClient;
        }

        //
        // Find the scene for an agent
        //
        private Scene GetClientScene(UUID agentId)
        {
            lock (m_Scenes)
            {
                foreach (Scene scene in m_Scenes)
                {
                    ScenePresence presence = scene.GetScenePresence(agentId);
                    if (presence != null)
                    {
                        if (!presence.IsChildAgent)
                            return scene;
                    }
                }
            }
            return null;
        }

        private void StatusChange(UUID agentID, bool online)
        {
            foreach (UserFriendData fd in m_Friends.Values)
            {
                // Is this a root agent? If not, they will get updates
                // through the root and this next check is redundant
                //
                if (fd.RegionID == UUID.Zero)
                    continue;

                if (fd.IsFriend(agentID.ToString()))
                {
                    UUID[] changed = new UUID[] { agentID };
                    IClientAPI client = LocateClientObject(fd.PrincipalID);
                    if (online)
                        client.SendAgentOnline(changed);
                    else
                        client.SendAgentOffline(changed);
                }
            }
        }

        private void OnInstantMessage(IClientAPI client, GridInstantMessage im)
        {
            if (im.dialog == (byte)OpenMetaverse.InstantMessageDialog.FriendshipOffered)
            { 
                // we got a friendship offer
                UUID principalID = new UUID(im.fromAgentID);
                UUID friendID = new UUID(im.toAgentID);

                // This user wants to be friends with the other user.
                // Let's add both relations to the DB, but one of them is inactive (-1)
                FriendsService.StoreFriend(principalID, friendID.ToString(), 1);
                FriendsService.StoreFriend(friendID, principalID.ToString(), -1);

                // Now let's ask the other user to be friends with this user
                ForwardFriendshipOffer(principalID, friendID, im);
            }
        }

        private void ForwardFriendshipOffer(UUID agentID, UUID friendID, GridInstantMessage im)
        {
            IClientAPI friendClient = LocateClientObject(friendID);
            if (friendClient != null)
            {
                // the prospective friend in this sim as root agent
                friendClient.SendInstantMessage(im);
                // we're done
                return ;
            }

            // The prospective friend is not here [as root]. Let's forward.
            PresenceInfo[] friendSessions = PresenceService.GetAgents(new string[] { friendID.ToString() });
            PresenceInfo friendSession = PresenceInfo.GetOnlinePresence(friendSessions);
            if (friendSession != null)
            {
                GridRegion region = GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, friendSession.RegionID);
                m_FriendsSimConnector.FriendshipOffered(region, agentID, friendID, im.message);
            }

            // If the prospective friend is not online, he'll get the message upon login.
        }

        private void OnApproveFriendRequest(IClientAPI client, UUID agentID, UUID friendID, List<UUID> callingCardFolders)
        {
            FriendsService.StoreFriend(agentID, friendID.ToString(), 1);

            //
            // Notify the friend
            //

            IClientAPI friendClient = LocateClientObject(friendID);
            if (friendClient != null)
            {
                // the prospective friend in this sim as root agent
                GridInstantMessage im = new GridInstantMessage(client.Scene, client.AgentId, client.Name, friendID,
                    (byte)OpenMetaverse.InstantMessageDialog.FriendshipAccepted, client.AgentId.ToString(), false, Vector3.Zero);
                friendClient.SendInstantMessage(im);
                // we're done
                return;
            }

            PresenceInfo[] friendSessions = PresenceService.GetAgents(new string[] { friendID.ToString() });
            PresenceInfo friendSession = PresenceInfo.GetOnlinePresence(friendSessions);
            if (friendSession != null)
            {
                GridRegion region = GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, friendSession.RegionID);
                m_FriendsSimConnector.FriendshipApproved(region, agentID, friendID);
            }
        }

        private void OnDenyFriendRequest(IClientAPI client, UUID agentID, UUID friendID, List<UUID> callingCardFolders)
        {
            FriendsService.Delete(agentID, friendID.ToString());
            FriendsService.Delete(friendID, agentID.ToString());

            //
            // Notify the friend
            //

            IClientAPI friendClient = LocateClientObject(friendID);
            if (friendClient != null)
            {
                // the prospective friend in this sim as root agent
                
                GridInstantMessage im = new GridInstantMessage(client.Scene, client.AgentId, client.Name, friendID,
                    (byte)OpenMetaverse.InstantMessageDialog.FriendshipDeclined, client.AgentId.ToString(), false, Vector3.Zero);
                friendClient.SendInstantMessage(im);
                // we're done
                return;
            }

            PresenceInfo[] friendSessions = PresenceService.GetAgents(new string[] { friendID.ToString() });
            PresenceInfo friendSession = PresenceInfo.GetOnlinePresence(friendSessions);
            if (friendSession != null)
            {
                GridRegion region = GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, friendSession.RegionID);
                m_FriendsSimConnector.FriendshipDenied(region, agentID, friendID);
            }
        }

        private void OnTerminateFriendship(IClientAPI client, UUID agentID, UUID exfriendID)
        {
            FriendsService.Delete(agentID, exfriendID.ToString());
            FriendsService.Delete(exfriendID, agentID.ToString());

            client.SendTerminateFriend(exfriendID);

            //
            // Notify the friend
            //

            IClientAPI friendClient = LocateClientObject(exfriendID);
            if (friendClient != null)
            {
                // the prospective friend in this sim as root agent
                friendClient.SendTerminateFriend(exfriendID);
                // we're done
                return;
            }

            PresenceInfo[] friendSessions = PresenceService.GetAgents(new string[] { exfriendID.ToString() });
            PresenceInfo friendSession = PresenceInfo.GetOnlinePresence(friendSessions);
            if (friendSession != null)
            {
                GridRegion region = GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, friendSession.RegionID);
                m_FriendsSimConnector.FriendshipTerminated(region, agentID, exfriendID);
            }
        }

        private void OnGrantUserRights(IClientAPI remoteClient, UUID requester, UUID target, int rights)
        {
            FriendsService.StoreFriend(requester, target.ToString(), rights);

            //
            // Notify the friend
            //

            IClientAPI friendClient = LocateClientObject(target);
            if (friendClient != null)
            {
                // the prospective friend in this sim as root agent
                //friendClient.???;
                // we're done
                return;
            }

            PresenceInfo[] friendSessions = PresenceService.GetAgents(new string[] { target.ToString() });
            PresenceInfo friendSession = PresenceInfo.GetOnlinePresence(friendSessions);
            if (friendSession != null)
            {
                GridRegion region = GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, friendSession.RegionID);
                m_FriendsSimConnector.GrantRights(region, requester, target);
            }
        }
    }
}
