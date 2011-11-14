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
using System.Threading;

using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Hypergrid;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;
using PresenceInfo = OpenSim.Services.Interfaces.PresenceInfo;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.Avatar.Friends
{
    public class HGFriendsModule : FriendsModule, ISharedRegionModule, IFriendsModule, IFriendsSimConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region ISharedRegionModule
        public override string Name
        {
            get { return "HGFriendsModule"; }
        }

        public override void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            base.AddRegion(scene);
            scene.RegisterModuleInterface<IFriendsSimConnector>(this);
        }

        #endregion

        #region IFriendsSimConnector

        /// <summary>
        /// Notify the user that the friend's status changed
        /// </summary>
        /// <param name="userID">user to be notified</param>
        /// <param name="friendID">friend whose status changed</param>
        /// <param name="online">status</param>
        /// <returns></returns>
        public bool StatusNotify(UUID friendID, UUID userID, bool online)
        {
            return LocalStatusNotification(friendID, userID, online);
        }

        #endregion

        protected override bool FetchFriendslist(IClientAPI client)
        {
            if (base.FetchFriendslist(client))
            {
                UUID agentID = client.AgentId;
                // we do this only for the root agent
                if (m_Friends[agentID].Refcount == 1)
                {
                    // We need to preload the user management cache with the names
                    // of foreign friends, just like we do with SOPs' creators
                    foreach (FriendInfo finfo in m_Friends[agentID].Friends)
                    {
                        if (finfo.TheirFlags != -1)
                        {
                            UUID id;
                            if (!UUID.TryParse(finfo.Friend, out id))
                            {
                                string url = string.Empty, first = string.Empty, last = string.Empty, tmp = string.Empty;
                                if (Util.ParseUniversalUserIdentifier(finfo.Friend, out id, out url, out first, out last, out tmp))
                                {
                                    IUserManagement uMan = m_Scenes[0].RequestModuleInterface<IUserManagement>();
                                    uMan.AddUser(id, url + ";" + first + " " + last);
                                }
                            }
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        public override bool SendFriendsOnlineIfNeeded(IClientAPI client)
        {
            if (base.SendFriendsOnlineIfNeeded(client))
            {
                AgentCircuitData aCircuit = ((Scene)client.Scene).AuthenticateHandler.GetAgentCircuitData(client.AgentId);
                if (aCircuit != null && (aCircuit.teleportFlags & (uint)Constants.TeleportFlags.ViaHGLogin) != 0)
                {
                    UserAccount account = m_Scenes[0].UserAccountService.GetUserAccount(client.Scene.RegionInfo.ScopeID, client.AgentId);
                    if (account == null) // foreign
                    {
                        FriendInfo[] friends = GetFriends(client.AgentId);
                        foreach (FriendInfo f in friends)
                        {
                            client.SendChangeUserRights(new UUID(f.Friend), client.AgentId, f.TheirFlags);
                        }
                    }
                }
            }
            return false;
        }

        protected override void GetOnlineFriends(UUID userID, List<string> friendList, /*collector*/ List<UUID> online)
        {
            List<string> fList = new List<string>();
            foreach (string s in friendList)
            {
                if (s.Length < 36)
                    m_log.WarnFormat(
                        "[HGFRIENDS MODULE]: Ignoring friend {0} ({1} chars) for {2} since identifier too short",
                        s, s.Length, userID);
                else
                    fList.Add(s.Substring(0, 36));
            }

            PresenceInfo[] presence = PresenceService.GetAgents(fList.ToArray());
            foreach (PresenceInfo pi in presence)
            {
                UUID presenceID;
                if (UUID.TryParse(pi.UserID, out presenceID))
                    online.Add(presenceID);
            }
        }

        //protected override void GetOnlineFriends(UUID userID, List<string> friendList, /*collector*/ List<UUID> online)
        //{
        //    // Let's single out the UUIs
        //    List<string> localFriends = new List<string>();
        //    List<string> foreignFriends = new List<string>();
        //    string tmp = string.Empty;

        //    foreach (string s in friendList)
        //    {
        //        UUID id;
        //        if (UUID.TryParse(s, out id))
        //            localFriends.Add(s);
        //        else if (Util.ParseUniversalUserIdentifier(s, out id, out tmp, out tmp, out tmp, out tmp))
        //        {
        //            foreignFriends.Add(s);
        //            // add it here too, who knows maybe the foreign friends happens to be on this grid
        //            localFriends.Add(id.ToString());
        //        }
        //    }

        //    // OK, see who's present on this grid
        //    List<string> toBeRemoved = new List<string>();
        //    PresenceInfo[] presence = PresenceService.GetAgents(localFriends.ToArray());
        //    foreach (PresenceInfo pi in presence)
        //    {
        //        UUID presenceID;
        //        if (UUID.TryParse(pi.UserID, out presenceID))
        //        {
        //            online.Add(presenceID);
        //            foreach (string s in foreignFriends)
        //                if (s.StartsWith(pi.UserID))
        //                    toBeRemoved.Add(s);
        //        }
        //    }

        //    foreach (string s in toBeRemoved)
        //        foreignFriends.Remove(s);

        //    // OK, let's send this up the stack, and leave a closure here
        //    // collecting online friends in other grids
        //    Util.FireAndForget(delegate { CollectOnlineFriendsElsewhere(userID, foreignFriends); });

        //}

        //private void CollectOnlineFriendsElsewhere(UUID userID, List<string> foreignFriends)
        //{
        //    // let's divide the friends on a per-domain basis
        //    Dictionary<string, List<string>> friendsPerDomain = new Dictionary<string, List<string>>();
        //    foreach (string friend in foreignFriends)
        //    {
        //        UUID friendID;
        //        if (!UUID.TryParse(friend, out friendID))
        //        {
        //            // it's a foreign friend
        //            string url = string.Empty, tmp = string.Empty;
        //            if (Util.ParseUniversalUserIdentifier(friend, out friendID, out url, out tmp, out tmp, out tmp))
        //            {
        //                if (!friendsPerDomain.ContainsKey(url))
        //                    friendsPerDomain[url] = new List<string>();
        //                friendsPerDomain[url].Add(friend);
        //            }
        //        }
        //    }

        //    // Now, call those worlds
            
        //    foreach (KeyValuePair<string, List<string>> kvp in friendsPerDomain)
        //    {
        //        List<string> ids = new List<string>();
        //        foreach (string f in kvp.Value)
        //            ids.Add(f);
        //        UserAgentServiceConnector uConn = new UserAgentServiceConnector(kvp.Key);
        //        List<UUID> online = uConn.GetOnlineFriends(userID, ids);
        //        // Finally send the notifications to the user
        //        // this whole process may take a while, so let's check at every
        //        // iteration that the user is still here
        //        IClientAPI client = LocateClientObject(userID);
        //        if (client != null)
        //            client.SendAgentOnline(online.ToArray());
        //        else
        //            break;
        //    }

        //}

        protected override void StatusNotify(List<FriendInfo> friendList, UUID userID, bool online)
        {
            // First, let's divide the friends on a per-domain basis
            Dictionary<string, List<FriendInfo>> friendsPerDomain = new Dictionary<string, List<FriendInfo>>();
            foreach (FriendInfo friend in friendList)
            {
                UUID friendID;
                if (UUID.TryParse(friend.Friend, out friendID))
                {
                    if (!friendsPerDomain.ContainsKey("local"))
                        friendsPerDomain["local"] = new List<FriendInfo>();
                    friendsPerDomain["local"].Add(friend);
                }
                else
                {
                    // it's a foreign friend
                    string url = string.Empty, tmp = string.Empty;
                    if (Util.ParseUniversalUserIdentifier(friend.Friend, out friendID, out url, out tmp, out tmp, out tmp))
                    {
                        // Let's try our luck in the local sim. Who knows, maybe it's here
                        if (LocalStatusNotification(userID, friendID, online))
                            continue;

                        if (!friendsPerDomain.ContainsKey(url))
                            friendsPerDomain[url] = new List<FriendInfo>();
                        friendsPerDomain[url].Add(friend);
                    }
                }
            }

            // For the local friends, just call the base method
            // Let's do this first of all
            if (friendsPerDomain.ContainsKey("local"))
                base.StatusNotify(friendsPerDomain["local"], userID, online);

            foreach (KeyValuePair<string, List<FriendInfo>> kvp in friendsPerDomain)
            {
                if (kvp.Key != "local")
                {
                    // For the others, call the user agent service 
                    List<string> ids = new List<string>();
                    foreach (FriendInfo f in kvp.Value)
                        ids.Add(f.Friend);
                    UserAgentServiceConnector uConn = new UserAgentServiceConnector(kvp.Key);
                    List<UUID> friendsOnline = uConn.StatusNotification(ids, userID, online);

                    if (online && friendsOnline.Count > 0)
                    {
                        IClientAPI client = LocateClientObject(userID);
                        if (client != null)
                            client.SendAgentOnline(friendsOnline.ToArray());
                    }
                }
            }
        }

        protected override bool GetAgentInfo(UUID scopeID, string fid, out UUID agentID, out string first, out string last)
        {
            first = "Unknown"; last = "User";
            if (base.GetAgentInfo(scopeID, fid, out agentID, out first, out last))
                return true;

            // fid is not a UUID...
            string url = string.Empty, tmp = string.Empty;
            if (Util.ParseUniversalUserIdentifier(fid, out agentID, out url, out first, out last, out tmp))
            {
                IUserManagement userMan = m_Scenes[0].RequestModuleInterface<IUserManagement>();
                userMan.AddUser(agentID, first, last, url);

                return true;
            }
            return false;
        }

        protected override string GetFriendshipRequesterName(UUID agentID)
        {
            // For the time being we assume that HG friendship requests can only happen 
            // when avies are on the same region.
            IClientAPI client = LocateClientObject(agentID);
            if (client != null)
                return client.FirstName + " " + client.LastName;
            else
                return base.GetFriendshipRequesterName(agentID);
        }

        protected override string FriendshipMessage(string friendID)
        {
            UUID id;
            if (UUID.TryParse(friendID, out id))
                return base.FriendshipMessage(friendID);

            return "Please confirm this friendship you made while you were away.";
        }

        protected override FriendInfo GetFriend(FriendInfo[] friends, UUID friendID)
        {
            foreach (FriendInfo fi in friends)
            {
                if (fi.Friend.StartsWith(friendID.ToString()))
                    return fi;
            }
            return null;
        }


        protected override FriendInfo[] GetFriendsFromService(IClientAPI client)
        {
            UserAccount account1 = UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, client.AgentId);
            if (account1 != null)
                return base.GetFriendsFromService(client);

            FriendInfo[] finfos = new FriendInfo[0];
            // Foreigner
            AgentCircuitData agentClientCircuit = ((Scene)(client.Scene)).AuthenticateHandler.GetAgentCircuitData(client.CircuitCode);
            if (agentClientCircuit != null)
            {
                string agentUUI = Util.ProduceUserUniversalIdentifier(agentClientCircuit);

                finfos = FriendsService.GetFriends(agentUUI);
                m_log.DebugFormat("[HGFRIENDS MODULE]: Fetched {0} local friends for visitor {1}", finfos.Length, agentUUI);
            }
            return finfos;
        }

        protected override bool StoreRights(UUID agentID, UUID friendID, int rights)
        {
            UserAccount account1 = UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, agentID);
            UserAccount account2 = UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, friendID);
            // Are they both local users?
            if (account1 != null && account2 != null)
            {
                // local grid users
                return base.StoreRights(agentID, friendID, rights);
            }

            if (account1 != null) // agent is local, friend is foreigner
            {
                FriendInfo[] finfos = GetFriends(agentID);
                FriendInfo finfo = GetFriend(finfos, friendID);
                if (finfo != null)
                {
                    FriendsService.StoreFriend(agentID.ToString(), finfo.Friend, rights);
                    return true;
                }
            }

            if (account2 != null) // agent is foreigner, friend is local
            {
                string agentUUI = GetUUI(friendID, agentID);
                if (agentUUI != string.Empty)
                {
                    FriendsService.StoreFriend(agentUUI, friendID.ToString(), rights);
                    return true;
                }
            }

            return false;

        }

        protected override void StoreBackwards(UUID friendID, UUID agentID)
        {
            UserAccount account1 = UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, agentID);
            UserAccount account2 = UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, friendID);
            // Are they both local users?
            if (account1 != null && account2 != null)
            {
                // local grid users
                m_log.DebugFormat("[HGFRIENDS MODULE]: Users are both local");
                base.StoreBackwards(friendID, agentID);
                return;
            }

            // no provision for this temporary friendship state
            //FriendsService.StoreFriend(friendID.ToString(), agentID.ToString(), 0);
        }

        protected override void StoreFriendships(UUID agentID, UUID friendID)
        {
            UserAccount agentAccount = UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, agentID);
            UserAccount friendAccount = UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, friendID);
            // Are they both local users?
            if (agentAccount != null && friendAccount != null)
            {
                // local grid users
                m_log.DebugFormat("[HGFRIENDS MODULE]: Users are both local");
                base.StoreFriendships(agentID, friendID);
                return;
            }

            // ok, at least one of them is foreigner, let's get their data
            IClientAPI agentClient = LocateClientObject(agentID);
            IClientAPI friendClient = LocateClientObject(friendID);
            AgentCircuitData agentClientCircuit = null;
            AgentCircuitData friendClientCircuit = null;
            string agentUUI = string.Empty;
            string friendUUI = string.Empty;
            string agentFriendService = string.Empty;
            string friendFriendService = string.Empty;

            if (agentClient != null)
            {
                agentClientCircuit = ((Scene)(agentClient.Scene)).AuthenticateHandler.GetAgentCircuitData(agentClient.CircuitCode);
                agentUUI = Util.ProduceUserUniversalIdentifier(agentClientCircuit);
                agentFriendService = agentClientCircuit.ServiceURLs["FriendsServerURI"].ToString();
            }
            if (friendClient != null)
            {
                friendClientCircuit = ((Scene)(friendClient.Scene)).AuthenticateHandler.GetAgentCircuitData(friendClient.CircuitCode);
                friendUUI = Util.ProduceUserUniversalIdentifier(friendClientCircuit);
                friendFriendService = friendClientCircuit.ServiceURLs["FriendsServerURI"].ToString();
            }

            m_log.DebugFormat("[HGFRIENDS MODULE] HG Friendship! thisUUI={0}; friendUUI={1}; foreignThisFriendService={2}; foreignFriendFriendService={3}",
                    agentUUI, friendUUI, agentFriendService, friendFriendService);

            // Generate a random 8-character hex number that will sign this friendship
            string secret = UUID.Random().ToString().Substring(0, 8);

            if (agentAccount != null) // agent is local, 'friend' is foreigner
            {
                // This may happen when the agent returned home, in which case the friend is not there
                // We need to look for its information in the friends list itself
                bool confirming = false;
                if (friendUUI == string.Empty)
                {
                    FriendInfo[] finfos = GetFriends(agentID);
                    foreach (FriendInfo finfo in finfos)
                    {
                        if (finfo.TheirFlags == -1)
                        {
                            if (finfo.Friend.StartsWith(friendID.ToString()))
                            {
                                friendUUI = finfo.Friend;
                                confirming = true;
                            }
                        }
                    }
                }

                // If it's confirming the friendship, we already have the full friendUUI with the secret
                string theFriendUUID = confirming ? friendUUI : friendUUI + ";" + secret;

                // store in the local friends service a reference to the foreign friend
                FriendsService.StoreFriend(agentID.ToString(), theFriendUUID, 1);
                // and also the converse
                FriendsService.StoreFriend(theFriendUUID, agentID.ToString(), 1);

                if (!confirming && friendClientCircuit != null)
                {
                    // store in the foreign friends service a reference to the local agent
                    HGFriendsServicesConnector friendsConn = new HGFriendsServicesConnector(friendFriendService, friendClientCircuit.SessionID, friendClientCircuit.ServiceSessionID);
                    friendsConn.NewFriendship(friendID, agentUUI + ";" + secret);
                }
            }
            else if (friendAccount != null) // 'friend' is local,  agent is foreigner
            {
                // store in the local friends service a reference to the foreign agent
                FriendsService.StoreFriend(friendID.ToString(), agentUUI + ";" + secret, 1);
                // and also the converse
                FriendsService.StoreFriend(agentUUI + ";" + secret, friendID.ToString(), 1);

                if (agentClientCircuit != null)
                {
                    // store in the foreign friends service a reference to the local agent
                    HGFriendsServicesConnector friendsConn = new HGFriendsServicesConnector(agentFriendService, agentClientCircuit.SessionID, agentClientCircuit.ServiceSessionID);
                    friendsConn.NewFriendship(agentID, friendUUI + ";" + secret);
                }
            }
            else // They're both foreigners!
            {
                HGFriendsServicesConnector friendsConn;
                if (agentClientCircuit != null)
                {
                    friendsConn = new HGFriendsServicesConnector(agentFriendService, agentClientCircuit.SessionID, agentClientCircuit.ServiceSessionID);
                    friendsConn.NewFriendship(agentID, friendUUI + ";" + secret);
                }
                if (friendClientCircuit != null)
                {
                    friendsConn = new HGFriendsServicesConnector(friendFriendService, friendClientCircuit.SessionID, friendClientCircuit.ServiceSessionID);
                    friendsConn.NewFriendship(friendID, agentUUI + ";" + secret);
                }
            }
            // my brain hurts now
        }

        protected override bool DeleteFriendship(UUID agentID, UUID exfriendID)
        {
            UserAccount agentAccount = UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, agentID);
            UserAccount friendAccount = UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, exfriendID);
            // Are they both local users?
            if (agentAccount != null && friendAccount != null)
            {
                // local grid users
                return base.DeleteFriendship(agentID, exfriendID);
            }

            // ok, at least one of them is foreigner, let's get their data
            string agentUUI = string.Empty;
            string friendUUI = string.Empty;

            if (agentAccount != null) // agent is local, 'friend' is foreigner
            {
                // We need to look for its information in the friends list itself
                FriendInfo[] finfos = GetFriends(agentID);
                FriendInfo finfo = GetFriend(finfos, exfriendID);
                if (finfo != null)
                {
                    friendUUI = finfo.Friend;

                    // delete in the local friends service the reference to the foreign friend
                    FriendsService.Delete(agentID, friendUUI);
                    // and also the converse
                    FriendsService.Delete(friendUUI, agentID.ToString());

                    // notify the exfriend's service
                    Util.FireAndForget(delegate { Delete(exfriendID, agentID, friendUUI); });

                    m_log.DebugFormat("[HGFRIENDS MODULE]: {0} terminated {1}", agentID, friendUUI);
                    return true;
                }
            }
            else if (friendAccount != null) // agent is foreigner, 'friend' is local
            {
                agentUUI = GetUUI(exfriendID, agentID);

                if (agentUUI != string.Empty)
                {
                    // delete in the local friends service the reference to the foreign agent
                    FriendsService.Delete(exfriendID, agentUUI);
                    // and also the converse
                    FriendsService.Delete(agentUUI, exfriendID.ToString());

                    // notify the agent's service?
                    Util.FireAndForget(delegate { Delete(agentID, exfriendID, agentUUI); });

                    m_log.DebugFormat("[HGFRIENDS MODULE]: {0} terminated {1}", agentUUI, exfriendID);
                    return true;
                }
            }
            //else They're both foreigners! Can't handle this

            return false;
        }

        private string GetUUI(UUID localUser, UUID foreignUser)
        {
            // Let's see if the user is here by any chance
            FriendInfo[] finfos = GetFriends(localUser);
            if (finfos != EMPTY_FRIENDS) // friend is here, cool
            {
                FriendInfo finfo = GetFriend(finfos, foreignUser);
                if (finfo != null)
                {
                    return finfo.Friend;
                }
            }
            else // user is not currently on this sim, need to get from the service
            {
                finfos = FriendsService.GetFriends(localUser);
                foreach (FriendInfo finfo in finfos)
                {
                    if (finfo.Friend.StartsWith(foreignUser.ToString())) // found it!
                    {
                        return finfo.Friend;
                    }
                }
            }
            return string.Empty;
        }

        private void Delete(UUID foreignUser, UUID localUser, string uui)
        {
            UUID id;
            string url = string.Empty, secret = string.Empty, tmp = string.Empty;
            if (Util.ParseUniversalUserIdentifier(uui, out id, out url, out tmp, out tmp, out secret))
            {
                m_log.DebugFormat("[HGFRIENDS MODULE]: Deleting friendship from {0}", url);
                HGFriendsServicesConnector friendConn = new HGFriendsServicesConnector(url);
                friendConn.DeleteFriendship(foreignUser, localUser, secret);
            }
        }
    }
}
