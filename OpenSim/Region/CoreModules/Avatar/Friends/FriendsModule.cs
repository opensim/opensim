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
using System.Linq;
using System.Reflection;
using System.Threading;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using Mono.Addins;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Servers;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Friends;
using OpenSim.Server.Base;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;
using PresenceInfo = OpenSim.Services.Interfaces.PresenceInfo;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.Avatar.Friends
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "FriendsModule")]
    public class FriendsModule : ISharedRegionModule, IFriendsModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected bool m_Enabled = false;

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

        protected static readonly FriendInfo[] EMPTY_FRIENDS = new FriendInfo[0];

        protected List<Scene> m_Scenes = new List<Scene>();

        protected IPresenceService m_PresenceService = null;
        protected IFriendsService m_FriendsService = null;
        protected FriendsSimConnector m_FriendsSimConnector;

        /// <summary>
        /// Cache friends lists for users.
        /// </summary>
        /// <remarks>
        /// This is a complex and error-prone thing to do.  At the moment, we assume that the efficiency gained in
        /// permissions checks outweighs the disadvantages of that complexity.
        /// </remarks>
        protected Dictionary<UUID, UserFriendData> m_Friends = new Dictionary<UUID, UserFriendData>();

        /// <summary>
        /// Maintain a record of clients that need to notify about their online status. This only
        /// needs to be done on login.  Subsequent online/offline friend changes are sent by a different mechanism.
        /// </summary>
        protected HashSet<UUID> m_NeedsToNotifyStatus = new HashSet<UUID>();

        /// <summary>
        /// Maintain a record of viewers that need to be sent notifications for friends that are online.  This only
        /// needs to be done on login.  Subsequent online/offline friend changes are sent by a different mechanism.
        /// </summary>
        protected HashSet<UUID> m_NeedsListOfOnlineFriends = new HashSet<UUID>();

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

        public IFriendsService FriendsService
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

        #region ISharedRegionModule
        public void Initialise(IConfigSource config)
        {
            IConfig moduleConfig = config.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("FriendsModule", "FriendsModule");
                if (name == Name)
                {
                    InitModule(config);

                    m_Enabled = true;
                    m_log.DebugFormat("[FRIENDS MODULE]: {0} enabled.", Name);
                }
            }
        }

        protected virtual void InitModule(IConfigSource config)
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

                if (server != null)
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

        public virtual void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

//            m_log.DebugFormat("[FRIENDS MODULE]: AddRegion on {0}", Name);

            m_Scenes.Add(scene);
            scene.RegisterModuleInterface<IFriendsModule>(this);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClientClosed += OnClientClosed;
            scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
            scene.EventManager.OnClientLogin += OnClientLogin;
        }

        public virtual void RegionLoaded(Scene scene) {}

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_Scenes.Remove(scene);
        }

        public virtual string Name
        {
            get { return "FriendsModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        public virtual int GetRightsGrantedByFriend(UUID principalID, UUID friendID)
        {
            FriendInfo[] friends = GetFriendsFromCache(principalID);
            FriendInfo finfo = GetFriend(friends, friendID);
            if (finfo != null && finfo.TheirFlags != -1)
            {
                return finfo.TheirFlags;
            }
            return 0;
        }

       private void OnMakeRootAgent(ScenePresence sp)
        {
            if(sp.m_gotCrossUpdate)
                return;

            RecacheFriends(sp.ControllingClient);

            lock (m_NeedsToNotifyStatus)
            {
                if (m_NeedsToNotifyStatus.Remove(sp.UUID))
                {
                    // Inform the friends that this user is online. This can only be done once the client is a Root Agent.
                    StatusChange(sp.UUID, true);
                }
            }
        }

        private void OnNewClient(IClientAPI client)
        {
            client.OnInstantMessage += OnInstantMessage;

            if (client is INPC)
                return;

            client.OnApproveFriendRequest += OnApproveFriendRequest;
            client.OnDenyFriendRequest += OnDenyFriendRequest;
            client.OnTerminateFriendship += RemoveFriendship;
            client.OnGrantUserRights += GrantRights;
            client.OnFindAgent += FindFriend;

            // We need to cache information for child agents as well as root agents so that friend edit/move/delete
            // permissions will work across borders where both regions are on different simulators.
            //
            // Do not do this asynchronously.  If we do, then subsequent code can outrace CacheFriends() and
            // return misleading results from the still empty friends cache.
            // If we absolutely need to do this asynchronously, then a signalling mechanism is needed so that calls
            // to GetFriends() will wait until CacheFriends() completes.  Locks are insufficient.
            CacheFriends(client);
        }

        /// <summary>
        /// Cache the friends list or increment the refcount for the existing friends list.
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <returns>
        /// Returns true if the list was fetched, false if it wasn't
        /// </returns>
        protected virtual bool CacheFriends(IClientAPI client)
        {
            UUID agentID = client.AgentId;
            lock (m_Friends)
            {
                UserFriendData friendsData;
                if (m_Friends.TryGetValue(agentID, out friendsData))
                {
                    friendsData.Refcount++;
                    return false;
                }
                else
                {
                    friendsData = new UserFriendData();
                    friendsData.PrincipalID = agentID;
                    friendsData.Friends = GetFriendsFromService(client);
                    friendsData.Refcount = 1;

                    m_Friends[agentID] = friendsData;
                    return true;
                }
            }
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

        private void OnClientLogin(IClientAPI client)
        {
            UUID agentID = client.AgentId;

            //m_log.DebugFormat("[XXX]: OnClientLogin!");

            // Register that we need to send this user's status to friends. This can only be done
            // once the client becomes a Root Agent, because as part of sending out the presence
            // we also get back the presence of the HG friends, and we need to send that to the
            // client, but that can only be done when the client is a Root Agent.
            lock (m_NeedsToNotifyStatus)
                m_NeedsToNotifyStatus.Add(agentID);

            // Register that we need to send the list of online friends to this user
            lock (m_NeedsListOfOnlineFriends)
                m_NeedsListOfOnlineFriends.Add(agentID);
        }

        public void IsNowRoot(ScenePresence sp)
        {
            OnMakeRootAgent(sp);
        }

        public virtual bool SendFriendsOnlineIfNeeded(IClientAPI client)
        {
            if (client == null)
                return false;

            UUID agentID = client.AgentId;

            // Check if the online friends list is needed
            lock (m_NeedsListOfOnlineFriends)
            {
                if (!m_NeedsListOfOnlineFriends.Remove(agentID))
                    return false;
            }

            // Send the friends online
            List<UUID> online = GetOnlineFriends(agentID);

            if (online.Count > 0)
                client.SendAgentOnline(online.ToArray());

            // Send outstanding friendship offers
            List<string> outstanding = new List<string>();
            FriendInfo[] friends = GetFriendsFromCache(agentID);
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
                string firstname = "Unknown", lastname = "UserFMSFOIN";
                if (!GetAgentInfo(client.Scene.RegionInfo.ScopeID, fid, out fromAgentID, out firstname, out lastname))
                {
                    m_log.DebugFormat("[FRIENDS MODULE]: skipping malformed friend {0}", fid);
                    continue;
                }

                im.offline = 0;
                im.fromAgentID = fromAgentID.Guid;
                im.fromAgentName = firstname + " " + lastname;
                im.imSessionID = im.fromAgentID;
                im.message = FriendshipMessage(fid);

                LocalFriendshipOffered(agentID, im);
            }

            return true;
        }

        protected virtual string FriendshipMessage(string friendID)
        {
            return "Will you be my friend?";
        }

        protected virtual bool GetAgentInfo(UUID scopeID, string fid, out UUID agentID, out string first, out string last)
        {
            first = "Unknown"; last = "UserFMGAI";
            if (!UUID.TryParse(fid, out agentID))
                return false;

            UserAccount account = m_Scenes[0].UserAccountService.GetUserAccount(scopeID, agentID);
            if (account != null)
            {
                first = account.FirstName;
                last = account.LastName;
            }

            return true;
        }

        List<UUID> GetOnlineFriends(UUID userID)
        {
            List<string> friendList = new List<string>();

            FriendInfo[] friends = GetFriendsFromCache(userID);
            foreach (FriendInfo fi in friends)
            {
                if (((fi.TheirFlags & (int)FriendRights.CanSeeOnline) != 0) && (fi.TheirFlags != -1))
                    friendList.Add(fi.Friend);
            }

            List<UUID> online = new List<UUID>();

            if (friendList.Count > 0)
                GetOnlineFriends(userID, friendList, online);

//            m_log.DebugFormat(
//                "[FRIENDS MODULE]: User {0} has {1} friends online", userID, online.Count);

            return online;
        }

        protected virtual void GetOnlineFriends(UUID userID, List<string> friendList, /*collector*/ List<UUID> online)
        {
//            m_log.DebugFormat(
//                "[FRIENDS MODULE]: Looking for online presence of {0} users for {1}", friendList.Count, userID);

            PresenceInfo[] presence = PresenceService.GetAgents(friendList.ToArray());
            foreach (PresenceInfo pi in presence)
            {
                UUID presenceID;
                if (UUID.TryParse(pi.UserID, out presenceID))
                    online.Add(presenceID);
            }
        }

        /// <summary>
        /// Find the client for a ID
        /// </summary>
        public IClientAPI LocateClientObject(UUID agentID)
        {
            lock (m_Scenes)
            {
                foreach (Scene scene in m_Scenes)
                {
                    ScenePresence presence = scene.GetScenePresence(agentID);
                    if (presence != null && !presence.IsChildAgent)
                        return presence.ControllingClient;
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
            FriendInfo[] friends = GetFriendsFromCache(agentID);
            if (friends.Length > 0)
            {
                List<FriendInfo> friendList = new List<FriendInfo>();
                foreach (FriendInfo fi in friends)
                {
                    if (((fi.MyFlags & (int)FriendRights.CanSeeOnline) != 0) && (fi.TheirFlags != -1))
                        friendList.Add(fi);
                }
                if(friendList.Count > 0)
                {
                    Util.FireAndForget(
                        delegate
                        {
//                            m_log.DebugFormat(
//                                "[FRIENDS MODULE]: Notifying {0} friends of {1} of online status {2}",
//                                friendList.Count, agentID, online);

                            // Notify about this user status
                            StatusNotify(friendList, agentID, online);
                        }, null, "FriendsModule.StatusChange"
                    );
                }
            }
        }

        protected virtual void StatusNotify(List<FriendInfo> friendList, UUID userID, bool online)
        {
            //m_log.DebugFormat("[FRIENDS]: Entering StatusNotify for {0}", userID);

            List<string> friendStringIds = friendList.ConvertAll<string>(friend => friend.Friend);
            List<string> remoteFriendStringIds = new List<string>();
            foreach (string friendStringId in friendStringIds)
            {
                UUID friendUuid;
                if (UUID.TryParse(friendStringId, out friendUuid))
                {
                    if (LocalStatusNotification(userID, friendUuid, online))
                        continue;

                    remoteFriendStringIds.Add(friendStringId);
                }
                else
                {
                    m_log.WarnFormat("[FRIENDS]: Error parsing friend ID {0}", friendStringId);
                }
            }

            // We do this regrouping so that we can efficiently send a single request rather than one for each
            // friend in what may be a very large friends list.
            PresenceInfo[] friendSessions = PresenceService.GetAgents(remoteFriendStringIds.ToArray());
            if(friendSessions == null)
                return;

            foreach (PresenceInfo friendSession in friendSessions)
            {
                // let's guard against sessions-gone-bad
                if (friendSession != null && friendSession.RegionID != UUID.Zero)
                {
                    //m_log.DebugFormat("[FRIENDS]: Get region {0}", friendSession.RegionID);
                    GridRegion region = GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, friendSession.RegionID);
                    if (region != null)
                    {
                        m_FriendsSimConnector.StatusNotify(region, userID, friendSession.UserID, online);
                    }
                }
                //else
                //    m_log.DebugFormat("[FRIENDS]: friend session is null or the region is UUID.Zero");
            }
        }

        protected virtual void OnInstantMessage(IClientAPI client, GridInstantMessage im)
        {
            if ((InstantMessageDialog)im.dialog == InstantMessageDialog.FriendshipOffered)
            {
                // we got a friendship offer
                UUID principalID = new UUID(im.fromAgentID);
                UUID friendID = new UUID(im.toAgentID);

                m_log.DebugFormat("[FRIENDS]: {0} ({1}) offered friendship to {2} ({3})", principalID, client.FirstName + client.LastName, friendID, im.fromAgentName);

                // Check that the friendship doesn't exist yet
                FriendInfo[] finfos = GetFriendsFromCache(principalID);
                if (finfos != null)
                {
                    FriendInfo f = GetFriend(finfos, friendID);
                    if (f != null)
                    {
                        client.SendAgentAlertMessage("This person is already your friend. Please delete it first if you want to reestablish the friendship.", false);
                        return;
                    }
                }

                // This user wants to be friends with the other user.
                // Let's add the relation backwards, in case the other is not online
                StoreBackwards(friendID, principalID);

                // Now let's ask the other user to be friends with this user
                ForwardFriendshipOffer(principalID, friendID, im);
            }
        }

        protected virtual bool ForwardFriendshipOffer(UUID agentID, UUID friendID, GridInstantMessage im)
        {
            // !!!!!!!! This is a hack so that we don't have to keep state (transactionID/imSessionID)
            // We stick this agent's ID as imSession, so that it's directly available on the receiving end
            im.imSessionID = im.fromAgentID;
            im.fromAgentName = GetFriendshipRequesterName(agentID);

            // Try the local sim
            if (LocalFriendshipOffered(friendID, im))
                return true;

            // The prospective friend is not here [as root]. Let's forward.
            PresenceInfo[] friendSessions = PresenceService.GetAgents(new string[] { friendID.ToString() });
            if (friendSessions != null && friendSessions.Length > 0)
            {
                PresenceInfo friendSession = friendSessions[0];
                if (friendSession != null)
                {
                    GridRegion region = GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, friendSession.RegionID);
                    m_FriendsSimConnector.FriendshipOffered(region, agentID, friendID, im.message);
                    return true;
                }
            }
            // If the prospective friend is not online, he'll get the message upon login.
            return false;
        }

        protected virtual string GetFriendshipRequesterName(UUID agentID)
        {
            UserAccount account = UserAccountService.GetUserAccount(UUID.Zero, agentID);
            return (account == null) ? "Unknown" : account.FirstName + " " + account.LastName;
        }

        protected virtual void OnApproveFriendRequest(IClientAPI client, UUID friendID, List<UUID> callingCardFolders)
        {
            m_log.DebugFormat("[FRIENDS]: {0} accepted friendship from {1}", client.AgentId, friendID);
            AddFriendship(client, friendID);
        }

        public void AddFriendship(IClientAPI client, UUID friendID)
        {
            StoreFriendships(client.AgentId, friendID);

            ICallingCardModule ccm = client.Scene.RequestModuleInterface<ICallingCardModule>();
            if (ccm != null)
            {
                ccm.CreateCallingCard(client.AgentId, friendID, UUID.Zero);
            }

            // Update the local cache.
            RecacheFriends(client);

            //
            // Notify the friend
            //

            // Try Local
            if (LocalFriendshipApproved(client.AgentId, client.Name, friendID))
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
                    m_FriendsSimConnector.FriendshipApproved(region, client.AgentId, client.Name, friendID);
                    client.SendAgentOnline(new UUID[] { friendID });
                }
            }
        }

        private void OnDenyFriendRequest(IClientAPI client, UUID friendID, List<UUID> callingCardFolders)
        {
            m_log.DebugFormat("[FRIENDS]: {0} denied friendship to {1}", client.AgentId, friendID);

            DeleteFriendship(client.AgentId, friendID);

            //
            // Notify the friend
            //

            // Try local
            if (LocalFriendshipDenied(client.AgentId, client.Name, friendID))
                return;

            PresenceInfo[] friendSessions = PresenceService.GetAgents(new string[] { friendID.ToString() });
            if (friendSessions != null && friendSessions.Length > 0)
            {
                PresenceInfo friendSession = friendSessions[0];
                if (friendSession != null)
                {
                    GridRegion region = GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, friendSession.RegionID);
                    if (region != null)
                        m_FriendsSimConnector.FriendshipDenied(region, client.AgentId, client.Name, friendID);
                    else
                        m_log.WarnFormat("[FRIENDS]: Could not find region {0} in locating {1}", friendSession.RegionID, friendID);
                }
            }
        }

        public void RemoveFriendship(IClientAPI client, UUID exfriendID)
        {
            if (!DeleteFriendship(client.AgentId, exfriendID))
                client.SendAlertMessage("Unable to terminate friendship on this sim.");

            // Update local cache
            RecacheFriends(client);

            client.SendTerminateFriend(exfriendID);

            //
            // Notify the friend
            //

            // Try local
            if (LocalFriendshipTerminated(client.AgentId, exfriendID))
                return;

            PresenceInfo[] friendSessions = PresenceService.GetAgents(new string[] { exfriendID.ToString() });
            if (friendSessions != null && friendSessions.Length > 0)
            {
                PresenceInfo friendSession = friendSessions[0];
                if (friendSession != null)
                {
                    GridRegion region = GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, friendSession.RegionID);
                    m_FriendsSimConnector.FriendshipTerminated(region, client.AgentId, exfriendID);
                }
            }
        }

        public void FindFriend(IClientAPI remoteClient,UUID HunterID ,UUID PreyID)
        {
            UUID requester = remoteClient.AgentId;
            if(requester != HunterID) // only allow client agent to be the hunter (?)
                return;

            FriendInfo[] friends = GetFriendsFromCache(requester);
            if (friends.Length == 0)
                return;

            FriendInfo friend = GetFriend(friends, PreyID);
            if (friend == null)
                return;

            if(friend.TheirFlags == -1 || (friend.TheirFlags & (int)FriendRights.CanSeeOnMap) == 0)
                return;

            Scene hunterScene = (Scene)remoteClient.Scene;

            if(hunterScene == null)
                return;

            // check local
            ScenePresence sp;
            double px;
            double py;
            if(hunterScene.TryGetScenePresence(PreyID, out sp))
            {
                if(sp == null)
                    return;
                px = hunterScene.RegionInfo.WorldLocX + sp.AbsolutePosition.X;
                py = hunterScene.RegionInfo.WorldLocY + sp.AbsolutePosition.Y;

                remoteClient.SendFindAgent(HunterID, PreyID, px, py);
                return;
            }

            PresenceInfo[] friendSessions = PresenceService.GetAgents(new string[] { PreyID.ToString() });

            if (friendSessions == null || friendSessions.Length == 0)
                return;

            PresenceInfo friendSession = friendSessions[0];
            if (friendSession == null)
                return;

            GridRegion region = GridService.GetRegionByUUID(hunterScene.RegionInfo.ScopeID, friendSession.RegionID);

            if(region == null)
                return;

            // we don't have presence location so point to a standard region center for now
            px = region.RegionLocX + 128.0;
            py = region.RegionLocY + 128.0;

            remoteClient.SendFindAgent(HunterID, PreyID, px, py);
        }

        public void GrantRights(IClientAPI remoteClient, UUID friendID, int rights)
        {
            UUID requester = remoteClient.AgentId;

            m_log.DebugFormat(
                "[FRIENDS MODULE]: User {0} changing rights to {1} for friend {2}",
                requester, rights, friendID);

            FriendInfo[] friends = GetFriendsFromCache(requester);
            if (friends.Length == 0)
            {
                return;
            }

            // Let's find the friend in this user's friend list
            FriendInfo friend = GetFriend(friends, friendID);

            if (friend != null) // Found it
            {
                // Store it on service
                if (!StoreRights(requester, friendID, rights))
                {
                    remoteClient.SendAlertMessage("Unable to grant rights.");
                    return;
                }

                // Store it in the local cache
                int myFlags = friend.MyFlags;
                friend.MyFlags = rights;

                // Always send this back to the original client
                remoteClient.SendChangeUserRights(requester, friendID, rights);

                //
                // Notify the friend
                //

                // Try local
                if (LocalGrantRights(requester, friendID, myFlags, rights))
                    return;

                PresenceInfo[] friendSessions = PresenceService.GetAgents(new string[] { friendID.ToString() });
                if (friendSessions != null && friendSessions.Length > 0)
                {
                    PresenceInfo friendSession = friendSessions[0];
                    if (friendSession != null)
                    {
                        GridRegion region = GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, friendSession.RegionID);
                        // TODO: You might want to send the delta to save the lookup
                        // on the other end!!
                        m_FriendsSimConnector.GrantRights(region, requester, friendID, myFlags, rights);
                    }
                }
            }
            else
            {
                m_log.DebugFormat("[FRIENDS MODULE]: friend {0} not found for {1}", friendID, requester);
            }
        }

        protected virtual FriendInfo GetFriend(FriendInfo[] friends, UUID friendID)
        {
            foreach (FriendInfo fi in friends)
            {
                if (fi.Friend == friendID.ToString())
                    return fi;
            }
            return null;
        }

        #region Local

        public virtual bool LocalFriendshipOffered(UUID toID, GridInstantMessage im)
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

                ICallingCardModule ccm = friendClient.Scene.RequestModuleInterface<ICallingCardModule>();
                if (ccm != null)
                {
                    ccm.CreateCallingCard(friendID, userID, UUID.Zero);
                }

                // Update the local cache
                RecacheFriends(friendClient);

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

        public bool LocalFriendshipTerminated(UUID userID, UUID exfriendID)
        {
            IClientAPI friendClient = LocateClientObject(exfriendID);
            if (friendClient != null)
            {
                // the friend in this sim as root agent
                friendClient.SendTerminateFriend(userID);
                // update local cache
                RecacheFriends(friendClient);
                // we're done
                return true;
            }

            return false;
        }

        public bool LocalGrantRights(UUID userID, UUID friendID, int oldRights, int newRights)
        {
            IClientAPI friendClient = LocateClientObject(friendID);
            if (friendClient != null)
            {
                int changedRights = newRights ^ oldRights;
                bool onlineBitChanged = (changedRights & (int)FriendRights.CanSeeOnline) != 0;
                if (onlineBitChanged)
                {
                    if ((newRights & (int)FriendRights.CanSeeOnline) == 1)
                        friendClient.SendAgentOnline(new UUID[] { userID });
                    else
                        friendClient.SendAgentOffline(new UUID[] { userID });
                }

                if(changedRights != 0)
                    friendClient.SendChangeUserRights(userID, friendID, newRights);

                // Update local cache
                UpdateLocalCache(userID, friendID, newRights);

                return true;
            }

            return false;

        }

        public bool LocalStatusNotification(UUID userID, UUID friendID, bool online)
        {
            //m_log.DebugFormat("[FRIENDS]: Local Status Notify {0} that user {1} is {2}", friendID, userID, online);
            IClientAPI friendClient = LocateClientObject(friendID);
            if (friendClient != null)
            {
                // the friend in this sim as root agent
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

        #region Get / Set friends in several flavours

        public FriendInfo[] GetFriendsFromCache(UUID userID)
        {
            UserFriendData friendsData;

            lock (m_Friends)
            {
                if (m_Friends.TryGetValue(userID, out friendsData))
                    return friendsData.Friends;
            }

            return EMPTY_FRIENDS;
        }

        /// <summary>
        /// Update local cache only
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="friendID"></param>
        /// <param name="rights"></param>
        protected void UpdateLocalCache(UUID userID, UUID friendID, int rights)
        {
            // Update local cache
            lock (m_Friends)
            {
                FriendInfo[] friends = GetFriendsFromCache(friendID);
                if(friends != EMPTY_FRIENDS)
                {
                    FriendInfo finfo = GetFriend(friends, userID);
                    if(finfo!= null)
                        finfo.TheirFlags = rights;
                }
            }
        }

        public virtual FriendInfo[] GetFriendsFromService(IClientAPI client)
        {
            return FriendsService.GetFriends(client.AgentId);
        }

        protected void RecacheFriends(IClientAPI client)
        {
            // FIXME: Ideally, we want to avoid doing this here since it sits the EventManager.OnMakeRootAgent event
            // is on the critical path for transferring an avatar from one region to another.
            UUID agentID = client.AgentId;
            lock (m_Friends)
            {
                UserFriendData friendsData;
                if (m_Friends.TryGetValue(agentID, out friendsData))
                    friendsData.Friends = GetFriendsFromService(client);
            }
        }

        public bool AreFriendsCached(UUID userID)
        {
            lock (m_Friends)
                return m_Friends.ContainsKey(userID);
        }

        protected virtual bool StoreRights(UUID agentID, UUID friendID, int rights)
        {
            FriendsService.StoreFriend(agentID.ToString(), friendID.ToString(), rights);
            return true;
        }

        protected virtual void StoreBackwards(UUID friendID, UUID agentID)
        {
            FriendsService.StoreFriend(friendID.ToString(), agentID.ToString(), 0);
        }

        protected virtual void StoreFriendships(UUID agentID, UUID friendID)
        {
            FriendsService.StoreFriend(agentID.ToString(), friendID.ToString(), (int)FriendRights.CanSeeOnline);
            FriendsService.StoreFriend(friendID.ToString(), agentID.ToString(), (int)FriendRights.CanSeeOnline);
        }

        protected virtual bool DeleteFriendship(UUID agentID, UUID exfriendID)
        {
            FriendsService.Delete(agentID, exfriendID.ToString());
            FriendsService.Delete(exfriendID, agentID.ToString());
            return true;
        }

        #endregion
    }
}
