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
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class HGFriendsModule : FriendsModule, ISharedRegionModule, IFriendsModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region ISharedRegionModule
        public override string Name
        {
            get { return "HGFriendsModule"; }
        }

        #endregion

        protected override bool FetchFriendslist(IClientAPI client)
        {
            if (base.FetchFriendslist(client))
            {
                UUID agentID = client.AgentId;
                // We need to preload the user management cache with the names
                // of foreign friends, just like we do with SOPs' creators
                foreach (FriendInfo finfo in m_Friends[agentID].Friends)
                {
                    if (finfo.TheirFlags != -1)
                    {
                        UUID id;
                        if (!UUID.TryParse(finfo.Friend, out id))
                        {
                            string url = string.Empty, first = string.Empty, last = string.Empty;
                            if (Util.ParseUniversalUserIdentifier(finfo.Friend, out id, out url, out first, out last))
                            {
                                IUserManagement uMan = m_Scenes[0].RequestModuleInterface<IUserManagement>();
                                uMan.AddUser(id, url + ";" + first + " " + last);
                            }
                        }
                    }
                }
                return true;
            }
            return false;
        }

        public override bool SendFriendsOnlineIfNeeded(IClientAPI client)
        {
            if (base.SendFriendsOnlineIfNeeded(client))
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
            return false;
        }

        protected override FriendInfo[] GetFriendsFromService(IClientAPI client)
        {
            UserAccount account1 = UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, client.AgentId);
            if (account1 != null)
                return base.GetFriendsFromService(client);

            // Foreigner
            AgentCircuitData agentClientCircuit = ((Scene)(client.Scene)).AuthenticateHandler.GetAgentCircuitData(client.CircuitCode);
            string agentUUI = Util.ProduceUserUniversalIdentifier(agentClientCircuit);

            FriendInfo[] finfos = FriendsService.GetFriends(agentUUI);
            m_log.DebugFormat("[HGFRIENDS MODULE]: Fetched {0} local friends for visitor {1}", finfos.Length, agentUUI);
            return finfos;
        }


        protected override bool GetAgentInfo(UUID scopeID, string fid, out UUID agentID, out string first, out string last)
        {
            first = "Unknown"; last = "User";
            if (base.GetAgentInfo(scopeID, fid, out agentID, out first, out last))
                return true;

            // fid is not a UUID...
            string url = string.Empty;
            if (Util.ParseUniversalUserIdentifier(fid, out agentID, out url, out first, out last))
            {
                IUserManagement userMan = m_Scenes[0].RequestModuleInterface<IUserManagement>();
                userMan.AddUser(agentID, url + ";" + first + " " + last);

                try // our best
                {
                    string[] parts = userMan.GetUserName(agentID).Split();
                    first = parts[0];
                    last = parts[1];
                }
                catch { }
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

        protected override bool SimpleStore(UUID agentID, UUID friendID, int rights)
        {
            UserAccount account1 = UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, agentID);
            UserAccount account2 = UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, friendID);
            // Are they both local users?
            if (account1 != null && account2 != null)
            {
                // local grid users
                return base.SimpleStore(agentID, friendID, rights);
            }

            if (account1 != null)
            {
                FriendInfo[] finfos = GetFriends(agentID);
                if (finfos.Length > 0)
                {
                    FriendInfo finfo = GetFriend(finfos, friendID);
                    FriendsService.StoreFriend(agentID.ToString(), finfo.Friend, rights);
                    return true;
                }
            }
            if (account2 != null)
            {
                IClientAPI client = LocateClientObject(agentID);
                if (client != null)
                {
                    AgentCircuitData acircuit = m_Scenes[0].AuthenticateHandler.GetAgentCircuitData(client.CircuitCode);
                    if (acircuit != null)
                    {
                        FriendsService.StoreFriend(Util.ProduceUserUniversalIdentifier(acircuit), friendID.ToString(), rights);
                        return true;
                    }
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

            m_log.DebugFormat("[XXX] HG Friendship! thisUUI={0}; friendUUI={1}; foreignThisFriendService={2}; foreignFriendFriendService={3}",
                    agentUUI, friendUUI, agentFriendService, friendFriendService);

            if (agentAccount != null) // agent is local, 'friend' is foreigner
            {
                // This may happen when the agent returned home, in which case the friend is not there
                // We need to llok for its information in the friends list itself
                if (friendUUI == string.Empty)
                {
                    FriendInfo[] finfos = GetFriends(agentID);
                    foreach (FriendInfo finfo in finfos)
                    {
                        if (finfo.TheirFlags == -1)
                        {
                            if (finfo.Friend.StartsWith(friendID.ToString()))
                                friendUUI = finfo.Friend;
                        }
                    }
                }

                // store in the local friends service a reference to the foreign friend
                FriendsService.StoreFriend(agentID.ToString(), friendUUI, 1);
                // and also the converse
                FriendsService.StoreFriend(friendUUI, agentID.ToString(), 1);

                if (friendClientCircuit != null)
                {
                    // store in the foreign friends service a reference to the local agent
                    HGFriendsServicesConnector friendsConn = new HGFriendsServicesConnector(friendFriendService, friendClientCircuit.SessionID, friendClientCircuit.ServiceSessionID);
                    friendsConn.NewFriendship(friendID, agentUUI);
                }
            }
            else if (friendAccount != null) // 'friend' is local,  agent is foreigner
            {
                // store in the local friends service a reference to the foreign agent
                FriendsService.StoreFriend(friendID.ToString(), agentUUI, 1);
                // and also the converse
                FriendsService.StoreFriend(agentUUI, friendID.ToString(), 1);

                if (agentClientCircuit != null)
                {
                    // store in the foreign friends service a reference to the local agent
                    HGFriendsServicesConnector friendsConn = new HGFriendsServicesConnector(agentFriendService, agentClientCircuit.SessionID, agentClientCircuit.ServiceSessionID);
                    friendsConn.NewFriendship(agentID, friendUUI);
                }
            }
            else // They're both foreigners!
            {
                HGFriendsServicesConnector friendsConn;
                if (agentClientCircuit != null)
                {
                    friendsConn = new HGFriendsServicesConnector(agentFriendService, agentClientCircuit.SessionID, agentClientCircuit.ServiceSessionID);
                    friendsConn.NewFriendship(agentID, friendUUI);
                }
                if (friendClientCircuit != null)
                {
                    friendsConn = new HGFriendsServicesConnector(friendFriendService, friendClientCircuit.SessionID, friendClientCircuit.ServiceSessionID);
                    friendsConn.NewFriendship(friendID, agentUUI);
                }
            }
            // my brain hurts now
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

        protected override void DeleteFriendship(UUID agentID, UUID exfriendID)
        {
            base.DeleteFriendship(agentID, exfriendID);
            // Maybe some of the base deletes will fail.
            // Let's delete the local friendship with foreign friend
            FriendInfo[] friends = GetFriends(agentID);
            foreach (FriendInfo finfo in friends)
            {
                if (finfo.Friend != exfriendID.ToString() && finfo.Friend.StartsWith(exfriendID.ToString()))
                {
                    FriendsService.Delete(agentID, exfriendID.ToString());
                    // TODO: delete the friendship on the other side
                    // Should use the homeurl given in finfo.Friend
                }
            }
        }

    }
}
