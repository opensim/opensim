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

        private static readonly FriendInfo[] EMPTY_FRIENDS = new FriendInfo[0];
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected List<Scene> m_Scenes = new List<Scene>();

        protected IPresenceService m_PresenceService = null;
        protected IFriendsService m_FriendsService = null;
        protected FriendsSimConnector m_FriendsSimConnector;

        protected Dictionary<UUID, UserFriendData> m_Friends =
                new Dictionary<UUID, UserFriendData>();

        protected HashSet<UUID> m_NeedsListOfFriends = new HashSet<UUID>();

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
            get { return m_Scenes[0].GridService; }
        }

        public IUserAccountService UserAccountService
        {
            get { return m_Scenes[0].UserAccountService; }
        }

        public IScene Scene
        {
            get
            {
                if (m_Scenes.Count > 0)
                    return m_Scenes[0];
                else
                    return null;
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
                m_log.Error("[FRIENDS]: No Connector defined in section Friends, or failed to load, cannot continue");
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
            scene.EventManager.OnClientLogin += OnClientLogin;
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

        public uint GetFriendPerms(UUID principalID, UUID friendID)
        {
            FriendInfo[] friends = GetFriends(principalID);
            foreach (FriendInfo fi in friends)
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

            // Asynchronously fetch the friends list or increment the refcount for the existing 
            // friends list
            Util.FireAndForget(
                delegate(object o)
                {
                    lock (m_Friends)
                    {
                        UserFriendData friendsData;
                        if (m_Friends.TryGetValue(client.AgentId, out friendsData))
                        {
                            friendsData.Refcount++;
                        }
                        else
                        {
                            friendsData = new UserFriendData();
                            friendsData.PrincipalID = client.AgentId;
                            friendsData.Friends = FriendsService.GetFriends(client.AgentId);
                            friendsData.Refcount = 1;

                            m_Friends[client.AgentId] = friendsData;
                        }
                    }
                }
            );
        }

        private void OnClientClosed(UUID agentID, Scene scene)
        {
            ScenePresence sp = scene.GetScenePresence(agentID);
            if (sp != null && !sp.IsChildAgent)
            {
                // do this for root agents closing out
                StatusChange(agentID, false);
            }

            lock (m_Friends)
            {
                UserFriendData friendsData;
                if (m_Friends.TryGetValue(agentID, out friendsData))
                {
                    friendsData.Refcount--;
                    if (friendsData.Refcount <= 0)
                        m_Friends.Remove(agentID);
                }
            }
        }

        private void OnMakeRootAgent(ScenePresence sp)
        {
            UUID agentID = sp.ControllingClient.AgentId;
            UpdateFriendsCache(agentID);
        }

        private void OnClientLogin(IClientAPI client)
        {
            UUID agentID = client.AgentId;

            // Inform the friends that this user is online
            StatusChange(agentID, true);
            
            // Register that we need to send the list of online friends to this user
            lock (m_NeedsListOfFriends)
                m_NeedsListOfFriends.Add(agentID);
        }

        public void SendFriendsOnlineIfNeeded(IClientAPI client)
        {
            UUID agentID = client.AgentId;

            // Check if the online friends list is needed
            lock (m_NeedsListOfFriends)
            {
                if (!m_NeedsListOfFriends.Remove(agentID))
                    return;
            }

            // Send the friends online
            List<UUID> online = GetOnlineFriends(agentID);
            if (online.Count > 0)
            {
                m_log.DebugFormat("[FRIENDS MODULE]: User {0} in region {1} has {2} friends online", client.AgentId, client.Scene.RegionInfo.RegionName, online.Count);
                client.SendAgentOnline(online.ToArray());
            }

            // Send outstanding friendship offers
            List<string> outstanding = new List<string>();
            FriendInfo[] friends = GetFriends(agentID);
            foreach (FriendInfo fi in friends)
            {
                if (fi.TheirFlags == -1)
                    outstanding.Add(fi.Friend);
            }

            GridInstantMessage im = new GridInstantMessage(client.Scene, UUID.Zero, String.Empty, agentID, (byte)InstantMessageDialog.FriendshipOffered,
                "Will you be my friend?", true, Vector3.Zero);

            foreach (string fid in outstanding)
            {
                UUID fromAgentID;
                if (!UUID.TryParse(fid, out fromAgentID))
                    continue;

                UserAccount account = m_Scenes[0].UserAccountService.GetUserAccount(client.Scene.RegionInfo.ScopeID, fromAgentID);

                PresenceInfo presence = null;
                PresenceInfo[] presences = PresenceService.GetAgents(new string[] { fid });
                if (presences != null && presences.Length > 0)
                    presence = presences[0];
                if (presence != null)
                    im.offline = 0;

                im.fromAgentID = fromAgentID.Guid;
                im.fromAgentName = account.FirstName + " " + account.LastName;
                im.offline = (byte)((presence == null) ? 1 : 0);
                im.imSessionID = im.fromAgentID;

                // Finally
                LocalFriendshipOffered(agentID, im);
            }
        }

        List<UUID> GetOnlineFriends(UUID userID)
        {
            List<string> friendList = new List<string>();
            List<UUID> online = new List<UUID>();

            FriendInfo[] friends = GetFriends(userID);
            foreach (FriendInfo fi in friends)
            {
                if (((fi.TheirFlags & 1) != 0) && (fi.TheirFlags != -1))
                    friendList.Add(fi.Friend);
            }

            if (friendList.Count > 0)
            {
                PresenceInfo[] presence = PresenceService.GetAgents(friendList.ToArray());
                foreach (PresenceInfo pi in presence)
                {
                    UUID presenceID;
                    if (UUID.TryParse(pi.UserID, out presenceID))
                        online.Add(presenceID);
                }
            }

            return online;
        }

        /// <summary>
        /// Find the client for a ID
        /// </summary>
        public IClientAPI LocateClientObject(UUID agentID)
        {
            Scene scene = GetClientScene(agentID);
            if (scene != null)
            {
                ScenePresence presence = scene.GetScenePresence(agentID);
                if (presence != null)
                    return presence.ControllingClient;
            }

            return null;
        }

        /// <summary>
        /// Find the scene for an agent
        /// </summary>
        private Scene GetClientScene(UUID agentId)
        {
            lock (m_Scenes)
            {
                foreach (Scene scene in m_Scenes)
                {
                    ScenePresence presence = scene.GetScenePresence(agentId);
                    if (presence != null && !presence.IsChildAgent)
                        return scene;
                }
            }

            return null;
        }

        /// <summary>
        /// Caller beware! Call this only for root agents.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="online"></param>
        private void StatusChange(UUID agentID, bool online)
        {
            FriendInfo[] friends = GetFriends(agentID);
            if (friends.Length > 0)
            {
                List<FriendInfo> friendList = new List<FriendInfo>();
                foreach (FriendInfo fi in friends)
                {
                    if (((fi.MyFlags & 1) != 0) && (fi.TheirFlags != -1))
                        friendList.Add(fi);
                }

                Util.FireAndForget(
                    delegate
                    {
                        foreach (FriendInfo fi in friendList)
                        {
                            //m_log.DebugFormat("[FRIENDS]: Notifying {0}", fi.PrincipalID);
                            // Notify about this user status
                            StatusNotify(fi, agentID, online);
                        }
                    }
                );
            }
        }

        private void StatusNotify(FriendInfo friend, UUID userID, bool online)
        {
            UUID friendID;
            if (UUID.TryParse(friend.Friend, out friendID))
            {
                // Try local
                if (LocalStatusNotification(userID, friendID, online))
                    return;

                // The friend is not here [as root]. Let's forward.
                PresenceInfo[] friendSessions = PresenceService.GetAgents(new string[] { friendID.ToString() });
                if (friendSessions != null && friendSessions.Length > 0)
                {
                    PresenceInfo friendSession = null; 
                    foreach (PresenceInfo pinfo in friendSessions)
                        if (pinfo.RegionID != UUID.Zero) // let's guard against sessions-gone-bad
                        {
                            friendSession = pinfo;
                            break;
                        }

                    if (friendSession != null)
                    {
                        GridRegion region = GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, friendSession.RegionID);
                        //m_log.DebugFormat("[FRIENDS]: Remote Notify to region {0}", region.RegionName);
                        m_FriendsSimConnector.StatusNotify(region, userID, friendID, online);
                    }
                }

                // Friend is not online. Ignore.
            }
            else
            {
                m_log.WarnFormat("[FRIENDS]: Error parsing friend ID {0}", friend.Friend);
            }
        }

        private void OnInstantMessage(IClientAPI client, GridInstantMessage im)
        {
            if ((InstantMessageDialog)im.dialog == InstantMessageDialog.FriendshipOffered)
            { 
                // we got a friendship offer
                UUID principalID = new UUID(im.fromAgentID);
                UUID friendID = new UUID(im.toAgentID);

                m_log.DebugFormat("[FRIENDS]: {0} ({1}) offered friendship to {2}", principalID, im.fromAgentName, friendID);

                // This user wants to be friends with the other user.
                // Let's add the relation backwards, in case the other is not online
                FriendsService.StoreFriend(friendID, principalID.ToString(), 0);

                // Now let's ask the other user to be friends with this user
                ForwardFriendshipOffer(principalID, friendID, im);
            }
        }

        private void ForwardFriendshipOffer(UUID agentID, UUID friendID, GridInstantMessage im)
        {
            // !!!!!!!! This is a hack so that we don't have to keep state (transactionID/imSessionID)
            // We stick this agent's ID as imSession, so that it's directly available on the receiving end
            im.imSessionID = im.fromAgentID;

            // Try the local sim
            UserAccount account = UserAccountService.GetUserAccount(UUID.Zero, agentID);
            im.fromAgentName = (account == null) ? "Unknown" : account.FirstName + " " + account.LastName;
            
            if (LocalFriendshipOffered(friendID, im))
                return;

            // The prospective friend is not here [as root]. Let's forward.
            PresenceInfo[] friendSessions = PresenceService.GetAgents(new string[] { friendID.ToString() });
            if (friendSessions != null && friendSessions.Length > 0)
            {
                PresenceInfo friendSession = friendSessions[0];
                if (friendSession != null)
                {
                    GridRegion region = GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, friendSession.RegionID);
                    m_FriendsSimConnector.FriendshipOffered(region, agentID, friendID, im.message);
                }
            }
            // If the prospective friend is not online, he'll get the message upon login.
        }

        private void OnApproveFriendRequest(IClientAPI client, UUID agentID, UUID friendID, List<UUID> callingCardFolders)
        {
            m_log.DebugFormat("[FRIENDS]: {0} accepted friendship from {1}", agentID, friendID);
            
            FriendsService.StoreFriend(agentID, friendID.ToString(), 1);
            FriendsService.StoreFriend(friendID, agentID.ToString(), 1);

            // Update the local cache
            UpdateFriendsCache(agentID);

            //
            // Notify the friend
            //

            // Try Local
            if (LocalFriendshipApproved(agentID, client.Name, friendID))
            {
                client.SendAgentOnline(new UUID[] { friendID });
                return;
            }

            // The friend is not here
            PresenceInfo[] friendSessions = PresenceService.GetAgents(new string[] { friendID.ToString() });
            if (friendSessions != null && friendSessions.Length > 0)
            {
                PresenceInfo friendSession = friendSessions[0];
                if (friendSession != null)
                {
                    GridRegion region = GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, friendSession.RegionID);
                    m_FriendsSimConnector.FriendshipApproved(region, agentID, client.Name, friendID);
                    client.SendAgentOnline(new UUID[] { friendID });
                }
            }
        }

        private void OnDenyFriendRequest(IClientAPI client, UUID agentID, UUID friendID, List<UUID> callingCardFolders)
        {
            m_log.DebugFormat("[FRIENDS]: {0} denied friendship to {1}", agentID, friendID);

            FriendsService.Delete(agentID, friendID.ToString());
            FriendsService.Delete(friendID, agentID.ToString());

            //
            // Notify the friend
            //

            // Try local
            if (LocalFriendshipDenied(agentID, client.Name, friendID))
                return;

            PresenceInfo[] friendSessions = PresenceService.GetAgents(new string[] { friendID.ToString() });
            if (friendSessions != null && friendSessions.Length > 0)
            {
                PresenceInfo friendSession = friendSessions[0];
                if (friendSession != null)
                {
                    GridRegion region = GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, friendSession.RegionID);
                    if (region != null)
                        m_FriendsSimConnector.FriendshipDenied(region, agentID, client.Name, friendID);
                    else
                        m_log.WarnFormat("[FRIENDS]: Could not find region {0} in locating {1}", friendSession.RegionID, friendID);
                }
            }
        }

        private void OnTerminateFriendship(IClientAPI client, UUID agentID, UUID exfriendID)
        {
            FriendsService.Delete(agentID, exfriendID.ToString());
            FriendsService.Delete(exfriendID, agentID.ToString());

            // Update local cache
            UpdateFriendsCache(agentID);

            client.SendTerminateFriend(exfriendID);

            //
            // Notify the friend
            //

            // Try local
            if (LocalFriendshipTerminated(exfriendID))
                return;

            PresenceInfo[] friendSessions = PresenceService.GetAgents(new string[] { exfriendID.ToString() });
            if (friendSessions != null && friendSessions.Length > 0)
            {
                PresenceInfo friendSession = friendSessions[0];
                if (friendSession != null)
                {
                    GridRegion region = GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, friendSession.RegionID);
                    m_FriendsSimConnector.FriendshipTerminated(region, agentID, exfriendID);
                }
            }
        }

        private void OnGrantUserRights(IClientAPI remoteClient, UUID requester, UUID target, int rights)
        {
            FriendInfo[] friends = GetFriends(remoteClient.AgentId);
            if (friends.Length == 0)
                return;

            m_log.DebugFormat("[FRIENDS MODULE]: User {0} changing rights to {1} for friend {2}", requester, rights, target);
            // Let's find the friend in this user's friend list
            FriendInfo friend = null;
            foreach (FriendInfo fi in friends)
            {
                if (fi.Friend == target.ToString())
                    friend = fi;
            }

            if (friend != null) // Found it
            {
                // Store it on the DB
                FriendsService.StoreFriend(requester, target.ToString(), rights);

                // Store it in the local cache
                int myFlags = friend.MyFlags;
                friend.MyFlags = rights;

                // Always send this back to the original client
                remoteClient.SendChangeUserRights(requester, target, rights);

                //
                // Notify the friend
                //

                // Try local
                if (LocalGrantRights(requester, target, myFlags, rights))
                    return;

                PresenceInfo[] friendSessions = PresenceService.GetAgents(new string[] { target.ToString() });
                if (friendSessions != null && friendSessions.Length > 0)
                {
                    PresenceInfo friendSession = friendSessions[0];
                    if (friendSession != null)
                    {
                        GridRegion region = GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, friendSession.RegionID);
                        // TODO: You might want to send the delta to save the lookup
                        // on the other end!!
                        m_FriendsSimConnector.GrantRights(region, requester, target, myFlags, rights);
                    }
                }
            }
        }

        #region Local

        public bool LocalFriendshipOffered(UUID toID, GridInstantMessage im)
        {
            IClientAPI friendClient = LocateClientObject(toID);
            if (friendClient != null)
            {
                // the prospective friend in this sim as root agent
                friendClient.SendInstantMessage(im);
                // we're done
                return true;
            }
            return false;
        }

        public bool LocalFriendshipApproved(UUID userID, string userName, UUID friendID)
        {
            IClientAPI friendClient = LocateClientObject(friendID);
            if (friendClient != null)
            {
                // the prospective friend in this sim as root agent
                GridInstantMessage im = new GridInstantMessage(Scene, userID, userName, friendID,
                    (byte)OpenMetaverse.InstantMessageDialog.FriendshipAccepted, userID.ToString(), false, Vector3.Zero);
                friendClient.SendInstantMessage(im);

                // Update the local cache
                UpdateFriendsCache(friendID);

                // we're done
                return true;
            }

            return false;
        }

        public bool LocalFriendshipDenied(UUID userID, string userName, UUID friendID)
        {
            IClientAPI friendClient = LocateClientObject(friendID);
            if (friendClient != null)
            {
                // the prospective friend in this sim as root agent
                GridInstantMessage im = new GridInstantMessage(Scene, userID, userName, friendID,
                    (byte)OpenMetaverse.InstantMessageDialog.FriendshipDeclined, userID.ToString(), false, Vector3.Zero);
                friendClient.SendInstantMessage(im);
                // we're done
                return true;
            }
            
            return false;
        }

        public bool LocalFriendshipTerminated(UUID exfriendID)
        {
            IClientAPI friendClient = LocateClientObject(exfriendID);
            if (friendClient != null)
            {
                // the friend in this sim as root agent
                friendClient.SendTerminateFriend(exfriendID);
                // update local cache
                UpdateFriendsCache(exfriendID);
                // we're done
                return true;
            }

            return false;
        }

        public bool LocalGrantRights(UUID userID, UUID friendID, int userFlags, int rights)
        {
            IClientAPI friendClient = LocateClientObject(friendID);
            if (friendClient != null)
            {
                bool onlineBitChanged = ((rights ^ userFlags) & (int)FriendRights.CanSeeOnline) != 0;
                if (onlineBitChanged)
                {
                    if ((rights & (int)FriendRights.CanSeeOnline) == 1)
                        friendClient.SendAgentOnline(new UUID[] { new UUID(userID) });
                    else
                        friendClient.SendAgentOffline(new UUID[] { new UUID(userID) });
                }
                else
                {
                    bool canEditObjectsChanged = ((rights ^ userFlags) & (int)FriendRights.CanModifyObjects) != 0;
                    if (canEditObjectsChanged)
                        friendClient.SendChangeUserRights(userID, friendID, rights);

                }

                // Update local cache
                lock (m_Friends)
                {
                    FriendInfo[] friends = GetFriends(friendID);
                    foreach (FriendInfo finfo in friends)
                    {
                        if (finfo.Friend == userID.ToString())
                            finfo.TheirFlags = rights;
                    }
                }

                return true;
            }

            return false;

        }

        public bool LocalStatusNotification(UUID userID, UUID friendID, bool online)
        {
            IClientAPI friendClient = LocateClientObject(friendID);
            if (friendClient != null)
            {
                //m_log.DebugFormat("[FRIENDS]: Local Status Notify {0} that user {1} is {2}", friendID, userID, online);
                // the  friend in this sim as root agent
                if (online)
                    friendClient.SendAgentOnline(new UUID[] { userID });
                else
                    friendClient.SendAgentOffline(new UUID[] { userID });
                // we're done
                return true;
            }

            return false;
        }

        #endregion

        private FriendInfo[] GetFriends(UUID agentID)
        {
            UserFriendData friendsData;

            lock (m_Friends)
            {
                if (m_Friends.TryGetValue(agentID, out friendsData))
                    return friendsData.Friends;
            }

            return EMPTY_FRIENDS;
        }

        private void UpdateFriendsCache(UUID agentID)
        {
            lock (m_Friends)
            {
                UserFriendData friendsData;
                if (m_Friends.TryGetValue(agentID, out friendsData))
                    friendsData.Friends = FriendsService.GetFriends(agentID);
            }
        }
    }
}
