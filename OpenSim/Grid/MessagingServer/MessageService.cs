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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Net;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using log4net;
using Nwc.XmlRpc;
using OpenSim.Data;
using OpenSim.Framework;

namespace OpenSim.Grid.MessagingServer
{
    public class MessageService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private MessageServerConfig m_cfg;
        private UserManager m_userManager;

        // a dictionary of all current presences this server knows about
        private Dictionary<UUID, UserPresenceData> m_presences = new Dictionary<UUID,UserPresenceData>();

        // a dictionary of all current regions this server knows about
        private Dictionary<ulong, RegionProfileData> m_regionInfoCache = new Dictionary<ulong,RegionProfileData>();

        public MessageService(MessageServerConfig cfg)
        {
            m_cfg = cfg;
            m_userManager = new UserManager();
            UserConfig uc = new UserConfig();
            uc.DatabaseConnect = cfg.DatabaseConnect;
            uc.DatabaseProvider = cfg.DatabaseProvider;

            m_userManager.AddPlugin(cfg.DatabaseProvider, cfg.DatabaseConnect);
        }


        #region FriendList Methods

        /// <summary>
        /// Process Friendlist subscriptions for a user
        /// The login method calls this for a User
        /// </summary>
        /// <param name="userpresence">The Agent we're processing the friendlist subscriptions for</param>
        private void ProcessFriendListSubscriptions(UserPresenceData userpresence)
        {
            lock (m_presences)
            {
                m_presences[userpresence.agentData.AgentID] = userpresence;
            }

            Dictionary<UUID, FriendListItem> uFriendList = userpresence.friendData;
            foreach (KeyValuePair<UUID, FriendListItem> pair in uFriendList)
            {
                UserPresenceData friendup = null;
                lock (m_presences)
                {
                    m_presences.TryGetValue(pair.Key, out friendup);
                }
                if (friendup != null)
                {
                    SubscribeToPresenceUpdates(userpresence, friendup, pair.Value);
                }
            }
        }

        /// <summary>
        /// Enqueues a presence update, sending info about user 'talkingAbout' to user 'receiver'.
        /// </summary>
        /// <param name="talkingAbout">We are sending presence information about this user.</param>
        /// <param name="receiver">We are sending the presence update to this user</param>
        private void enqueuePresenceUpdate(UserPresenceData talkingAbout, UserPresenceData receiver)
        {
            UserAgentData p2Handle = m_userManager.GetUserAgentData(receiver.agentData.AgentID);
            if (p2Handle != null)
            {
                if (receiver.lookupUserRegionYN)
                {
                    receiver.regionData.regionHandle = p2Handle.Handle;
                }
                else
                {
                    receiver.lookupUserRegionYN = true; // TODO Huh?
                }

                PresenceInformer friendlistupdater = new PresenceInformer();
                friendlistupdater.presence1 = talkingAbout;
                friendlistupdater.presence2 = receiver;
                friendlistupdater.OnGetRegionData += GetRegionInfo;
                friendlistupdater.OnDone += PresenceUpdateDone;
                WaitCallback cb = new WaitCallback(friendlistupdater.go);
                ThreadPool.QueueUserWorkItem(cb);
            }
            else
            {
                m_log.WarnFormat("no data found for user {0}", receiver.agentData.AgentID);
                // Skip because we can't find any data on the user
            }
        }

        /// <summary>
        /// Does the necessary work to subscribe one agent to another's presence notifications
        /// Gets called by ProcessFriendListSubscriptions.  You shouldn't call this directly
        /// unless you know what you're doing
        /// </summary>
        /// <param name="userpresence">P1</param>
        /// <param name="friendpresence">P2</param>
        /// <param name="uFriendListItem"></param>
        private void SubscribeToPresenceUpdates(UserPresenceData userpresence,
                                               UserPresenceData friendpresence,
                                               FriendListItem uFriendListItem)
        {
            // Can the friend see me online?
            if ((uFriendListItem.FriendListOwnerPerms & (uint)FriendRights.CanSeeOnline) != 0)
            {
                // tell user to update friend about user's presence changes
                if (!userpresence.subscriptionData.Contains(friendpresence.agentData.AgentID))
                {
                   userpresence.subscriptionData.Add(friendpresence.agentData.AgentID);
                }

                // send an update about user's presence to the friend
                enqueuePresenceUpdate(userpresence, friendpresence);
            }

            // Can I see the friend online?
            if ((uFriendListItem.FriendPerms & (uint)FriendRights.CanSeeOnline) != 0)
            {
                // tell friend to update user about friend's presence changes
                if (!friendpresence.subscriptionData.Contains(userpresence.agentData.AgentID))
                {
                    friendpresence.subscriptionData.Add(userpresence.agentData.AgentID);
                }

                // send an update about friend's presence to user.
                enqueuePresenceUpdate(friendpresence, userpresence);
            }
        }

        /// <summary>
        /// Logoff Processor.  Call this to clean up agent presence data and send logoff presence notifications
        /// </summary>
        /// <param name="AgentID"></param>
        private void ProcessLogOff(UUID AgentID)
        {
            m_log.Info("[LOGOFF]: Processing Logoff");

            UserPresenceData userPresence = null;
            lock (m_presences)
            {
                m_presences.TryGetValue(AgentID, out userPresence);
            }

            if (userPresence != null) // found the user
            {
                List<UUID> AgentsNeedingNotification = userPresence.subscriptionData;
                userPresence.OnlineYN = false;

                for (int i = 0; i < AgentsNeedingNotification.Count; i++)
                {
                    UserPresenceData friendPresence = null;
                    lock (m_presences)
                    {
                        m_presences.TryGetValue(AgentsNeedingNotification[i], out friendPresence);
                    }

                    // This might need to be enumerated and checked before we try to remove it.
                    if (friendPresence != null)
                    {
                        lock (friendPresence)
                        {
                            // no updates for this user anymore
                            friendPresence.subscriptionData.Remove(AgentID);

                            // set user's entry in the friend's list to offline (if it exists)
                            if (friendPresence.friendData.ContainsKey(AgentID))
                            {
                                friendPresence.friendData[AgentID].onlinestatus = false;
                            }
                        }

                        enqueuePresenceUpdate(userPresence, friendPresence);
                    }
                }
            }
        }

        #endregion

        private void PresenceUpdateDone(PresenceInformer obj)
        {
            obj.OnGetRegionData -= GetRegionInfo;
            obj.OnDone -= PresenceUpdateDone;
        }

        #region UserServer Comms

        /// <summary>
        /// Returns a list of FriendsListItems that describe the friends and permissions in the friend
        /// relationship for UUID friendslistowner. For faster lookup, we index by friend's UUID.
        /// </summary>
        /// <param name="friendlistowner">The agent that we're retreiving the friends Data for.</param>
        private Dictionary<UUID, FriendListItem> GetUserFriendList(UUID friendlistowner)
        {
            Dictionary<UUID, FriendListItem> buddies = new Dictionary<UUID,FriendListItem>();

            try
            {
                Hashtable param = new Hashtable();
                param["ownerID"] = friendlistowner.ToString();

                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_user_friend_list", parameters);
                XmlRpcResponse resp = req.Send(m_cfg.UserServerURL, 3000);
                Hashtable respData = (Hashtable)resp.Value;

                if (respData.Contains("avcount"))
                {
                    buddies = ConvertXMLRPCDataToFriendListItemList(respData);
                }

            }
            catch (WebException e)
            {
                m_log.Warn("Error when trying to fetch Avatar's friends list: " +
                                      e.Message);
                // Return Empty list (no friends)
            }
            return buddies;
        }

        /// <summary>
        /// Converts XMLRPC Friend List to FriendListItem Object
        /// </summary>
        /// <param name="data">XMLRPC response data Hashtable</param>
        /// <returns></returns>
        public Dictionary<UUID, FriendListItem> ConvertXMLRPCDataToFriendListItemList(Hashtable data)
        {
            Dictionary<UUID, FriendListItem> buddies = new Dictionary<UUID,FriendListItem>();
            int buddycount = Convert.ToInt32((string)data["avcount"]);

            for (int i = 0; i < buddycount; i++)
            {
                FriendListItem buddylistitem = new FriendListItem();

                buddylistitem.FriendListOwner = new UUID((string)data["ownerID" + i.ToString()]);
                buddylistitem.Friend = new UUID((string)data["friendID" + i.ToString()]);
                buddylistitem.FriendListOwnerPerms = (uint)Convert.ToInt32((string)data["ownerPerms" + i.ToString()]);
                buddylistitem.FriendPerms = (uint)Convert.ToInt32((string)data["friendPerms" + i.ToString()]);

                buddies.Add(buddylistitem.Friend, buddylistitem);
            }

            return buddies;
        }

        /// <summary>
        /// UserServer sends an expect_user method
        /// this handles the method and provisions the
        /// necessary info for presence to work
        /// </summary>
        /// <param name="request">UserServer Data</param>
        /// <returns></returns>
        public XmlRpcResponse UserLoggedOn(XmlRpcRequest request)
        {
            Hashtable requestData = (Hashtable)request.Params[0];

            AgentCircuitData agentData = new AgentCircuitData();
            agentData.SessionID = new UUID((string)requestData["sessionid"]);
            agentData.SecureSessionID = new UUID((string)requestData["secure_session_id"]);
            agentData.firstname = (string)requestData["firstname"];
            agentData.lastname = (string)requestData["lastname"];
            agentData.AgentID = new UUID((string)requestData["agentid"]);
            agentData.circuitcode = Convert.ToUInt32(requestData["circuit_code"]);
            agentData.CapsPath = (string)requestData["caps_path"];

            if (requestData.ContainsKey("child_agent") && requestData["child_agent"].Equals("1"))
            {
                agentData.child = true;
            }
            else
            {
                agentData.startpos =
                     new Vector3(Convert.ToSingle(requestData["positionx"]),
                                 Convert.ToSingle(requestData["positiony"]),
                                 Convert.ToSingle(requestData["positionz"]));
                agentData.child = false;
            }

            ulong regionHandle = Convert.ToUInt64((string)requestData["regionhandle"]);

            m_log.InfoFormat("[LOGON]: User {0} {1} logged into region {2} as {3} agent, building indexes for user",
                             agentData.firstname, agentData.lastname, regionHandle, agentData.child ? "child" : "root");

            UserPresenceData up = new UserPresenceData();
            up.agentData = agentData;
            up.friendData = GetUserFriendList(agentData.AgentID);
            up.regionData = GetRegionInfo(regionHandle);
            up.OnlineYN = true;
            up.lookupUserRegionYN = false;
            ProcessFriendListSubscriptions(up);

            return new XmlRpcResponse();
        }

        /// <summary>
        /// The UserServer got a Logoff message
        /// Cleanup time for that user.  Send out presence notifications
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse UserLoggedOff(XmlRpcRequest request)
        {
            m_log.Info("[USERLOGOFF]: User logged off called");
            Hashtable requestData = (Hashtable)request.Params[0];

            UUID AgentID = new UUID((string)requestData["agentid"]);
            ProcessLogOff(AgentID);

            return new XmlRpcResponse();
        }

        #endregion

        public XmlRpcResponse GetPresenceInfoBulk(XmlRpcRequest request)
        {
            Hashtable paramHash = (Hashtable)request.Params[0];
            Hashtable result = new Hashtable();

            // TODO check access (recv_key/send_key)

            IList list = (IList)paramHash["uuids"];

            // convert into List<UUID>
            List<UUID> uuids = new List<UUID>();
            for (int i = 0; i < list.Count; ++i)
            {
                UUID uuid;
                if (UUID.TryParse((string)list[i], out uuid))
                {
                    uuids.Add(uuid);
                }
            }

            try {
                Dictionary<UUID, FriendRegionInfo> infos = m_userManager.GetFriendRegionInfos(uuids);
                m_log.DebugFormat("[FRIEND]: Got {0} region entries back.", infos.Count);
                int count = 0;
                foreach (KeyValuePair<UUID, FriendRegionInfo> pair in infos)
                {
                    result["uuid_" + count] = pair.Key.ToString();
                    result["isOnline_" + count] = pair.Value.isOnline;
                    result["regionHandle_" + count] = pair.Value.regionHandle.ToString(); // XML-RPC doesn't know ulongs
                    ++count;
                }
                result["count"] = count;

                XmlRpcResponse response = new XmlRpcResponse();
                response.Value = result;
                return response;
            }
            catch(Exception e) {
                m_log.Error("Got exception:", e);
                throw e;
            }
        }

        #region regioninfo gathering

        /// <summary>
        /// Gets and caches a RegionInfo object from the gridserver based on regionhandle
        /// if the regionhandle is already cached, use the cached values
        /// Gets called by lots of threads!!!!!
        /// </summary>
        /// <param name="regionhandle">handle to the XY of the region we're looking for</param>
        /// <returns>A RegionInfo object to stick in the presence info</returns>
        public RegionProfileData GetRegionInfo(ulong regionhandle)
        {
            RegionProfileData regionInfo = null;

            lock (m_regionInfoCache)
            {
                m_regionInfoCache.TryGetValue(regionhandle, out regionInfo);
            }

            if (regionInfo == null) // not found in cache
            {
                regionInfo = RequestRegionInfo(regionhandle);

                if (regionInfo != null) // lookup was successful
                {
                    lock (m_regionInfoCache)
                    {
                        m_regionInfoCache[regionhandle] = regionInfo;
                    }
                }
            }

            return regionInfo;
        }

        public int ClearRegionCache()
        {
            int cachecount = 0;

            lock (m_regionInfoCache)
            {
                cachecount = m_regionInfoCache.Count;
                m_regionInfoCache.Clear();
            }

            return cachecount;
        }

        /// <summary>
        /// Get RegionProfileData from the GridServer.
        /// We'll cache this information in GetRegionInfo and use it for presence updates
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public RegionProfileData RequestRegionInfo(ulong regionHandle)
        {
            RegionProfileData regionProfile = null;
            try
            {
                Hashtable requestData = new Hashtable();
                requestData["region_handle"] = regionHandle.ToString();
                requestData["authkey"] = m_cfg.GridSendKey;

                ArrayList SendParams = new ArrayList();
                SendParams.Add(requestData);

                XmlRpcRequest GridReq = new XmlRpcRequest("simulator_data_request", SendParams);

                XmlRpcResponse GridResp = GridReq.Send(m_cfg.GridServerURL, 3000);

                Hashtable responseData = (Hashtable)GridResp.Value;

                if (responseData.ContainsKey("error"))
                {
                    m_log.Error("[GRID]: error received from grid server" + responseData["error"]);
                    return null;
                }

                uint regX = Convert.ToUInt32((string)responseData["region_locx"]);
                uint regY = Convert.ToUInt32((string)responseData["region_locy"]);
                string internalIpStr = (string)responseData["sim_ip"];

                regionProfile = new RegionProfileData();
                regionProfile.httpPort = (uint)Convert.ToInt32((string)responseData["http_port"]);
                regionProfile.httpServerURI = "http://" + internalIpStr + ":" + regionProfile.httpPort + "/";
                regionProfile.regionHandle = Helpers.UIntsToLong((regX * Constants.RegionSize), (regY * Constants.RegionSize));
                regionProfile.regionLocX = regX;
                regionProfile.regionLocY = regY;

                regionProfile.remotingPort = Convert.ToUInt32((string)responseData["remoting_port"]);
                regionProfile.UUID = new UUID((string)responseData["region_UUID"]);
                regionProfile.regionName = (string)responseData["region_name"];
            }
            catch (WebException)
            {
                m_log.Error("[GRID]: " +
                            "Region lookup failed for: " + regionHandle.ToString() +
                            " - Is the GridServer down?");
            }

            return regionProfile;
        }

        public bool registerWithUserServer ()
        {
            Hashtable UserParams = new Hashtable();
            // Login / Authentication

            if (m_cfg.HttpSSL)
            {
                UserParams["uri"] = "https://" + m_cfg.MessageServerIP + ":" + m_cfg.HttpPort;
            }
            else
            {
                UserParams["uri"] = "http://" + m_cfg.MessageServerIP + ":" + m_cfg.HttpPort;
            }

            UserParams["recvkey"] = m_cfg.UserRecvKey;
            UserParams["sendkey"] = m_cfg.UserRecvKey;

            // Package into an XMLRPC Request
            ArrayList SendParams = new ArrayList();
            SendParams.Add(UserParams);

            bool success = true;
            string[] servers = m_cfg.UserServerURL.Split(' ');

            foreach (string srv in servers)
            {
                // Send Request
                try
                {
                    XmlRpcRequest UserReq = new XmlRpcRequest("register_messageserver", SendParams);
                    XmlRpcResponse UserResp = UserReq.Send(srv, 16000);

                    // Process Response
                    Hashtable GridRespData = (Hashtable)UserResp.Value;
                    // if we got a response, we were successful
                    if (!GridRespData.ContainsKey("responsestring"))
                        success = false;
                    else
                        m_log.InfoFormat("[SERVER] Registered with {0}", srv);
                }
                catch (Exception ex)
                {
                    m_log.ErrorFormat("Unable to connect to server {0}. Server not running?", srv);
                    success = false;
                }
            }
            return success;
        }

        public bool deregisterWithUserServer()
        {
            Hashtable UserParams = new Hashtable();
            // Login / Authentication

            if (m_cfg.HttpSSL)
            {
                UserParams["uri"] = "https://" + m_cfg.MessageServerIP + ":" + m_cfg.HttpPort;
            }
            else
            {
                UserParams["uri"] = "http://" + m_cfg.MessageServerIP + ":" + m_cfg.HttpPort;
            }

            UserParams["recvkey"] = m_cfg.UserRecvKey;
            UserParams["sendkey"] = m_cfg.UserRecvKey;

            // Package into an XMLRPC Request
            ArrayList SendParams = new ArrayList();
            SendParams.Add(UserParams);

            bool success = true;
            string[] servers = m_cfg.UserServerURL.Split(' ');

            // Send Request
            foreach (string srv in servers)
            {
                try
                {
                    XmlRpcRequest UserReq = new XmlRpcRequest("deregister_messageserver", SendParams);
                    XmlRpcResponse UserResp = UserReq.Send(m_cfg.UserServerURL, 16000);
                    // Process Response
                    Hashtable UserRespData = (Hashtable)UserResp.Value;
                    // if we got a response, we were successful
                    if(!UserRespData.ContainsKey("responsestring"))
                        success = false;
                }
                catch (Exception ex)
                {
                    m_log.Error("Unable to connect to grid. User server not running?");
                    success = false;
                }
            }
            return success;
        }

        #endregion
    }
}
