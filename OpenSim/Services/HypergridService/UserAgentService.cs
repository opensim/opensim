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
using System.Net;
using System.Reflection;

using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Services.Connectors.Friends;
using OpenSim.Services.Connectors.Hypergrid;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Server.Base;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// This service is for HG1.5 only, to make up for the fact that clients don't
    /// keep any private information in themselves, and that their 'home service'
    /// needs to do it for them.
    /// Once we have better clients, this shouldn't be needed.
    /// </summary>
    public class UserAgentService : UserAgentServiceBase, IUserAgentService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // This will need to go into a DB table
        //static Dictionary<UUID, TravelingAgentInfo> m_Database = new Dictionary<UUID, TravelingAgentInfo>();

        static bool m_Initialized = false;

        protected static IGridUserService m_GridUserService;
        protected static IGridService m_GridService;
        protected static GatekeeperServiceConnector m_GatekeeperConnector;
        protected static IGatekeeperService m_GatekeeperService;
        protected static IFriendsService m_FriendsService;
        protected static IPresenceService m_PresenceService;
        protected static IUserAccountService m_UserAccountService;
        protected static IFriendsSimConnector m_FriendsLocalSimConnector; // standalone, points to HGFriendsModule
        protected static FriendsSimConnector m_FriendsSimConnector; // grid

        protected static string m_GridName;
        protected static string m_MyExternalIP = "";

        protected static int m_LevelOutsideContacts;
        protected static bool m_ShowDetails;

        protected static bool m_BypassClientVerification;

        private static readonly Dictionary<int, bool> m_ForeignTripsAllowed = new();
        private static readonly Dictionary<int, List<string>> m_TripsAllowedExceptions = new();
        private static readonly Dictionary<int, List<string>> m_TripsDisallowedExceptions = new();

        // grids known to not implement status_notify (older OpenSim), so we don't
        // keep sending them a request that will fail; re-probed after expiry
        private const int STATUS_NOTIFY_UNSUPPORTED_MS = 3600 * 1000;
        private static readonly ExpiringCacheOS<string, bool> m_StatusNotifyUnsupported = new(60000);

        public UserAgentService(IConfigSource config) : this(config, null)
        {
        }

        public UserAgentService(IConfigSource config, IFriendsSimConnector friendsConnector)
            : base(config)
        {
            // Let's set this always, because we don't know the sequence
            // of instantiations
            if (friendsConnector is not null)
                m_FriendsLocalSimConnector = friendsConnector;

            if (!m_Initialized)
            {
                m_Initialized = true;

                m_log.DebugFormat("[HOME USERS SECURITY]: Starting...");

                m_FriendsSimConnector = new FriendsSimConnector();

                IConfig serverConfig = config.Configs["UserAgentService"];
                if (serverConfig is null)
                    throw new Exception(String.Format("No section UserAgentService in config file"));

                string gridService = serverConfig.GetString("GridService", String.Empty);
                string gridUserService = serverConfig.GetString("GridUserService", String.Empty);
                string gatekeeperService = serverConfig.GetString("GatekeeperService", String.Empty);
                string friendsService = serverConfig.GetString("FriendsService", String.Empty);
                string presenceService = serverConfig.GetString("PresenceService", String.Empty);
                string userAccountService = serverConfig.GetString("UserAccountService", String.Empty);

                m_BypassClientVerification = serverConfig.GetBoolean("BypassClientVerification", false);

                if (gridService.Length == 0 || gridUserService.Length == 0 || gatekeeperService.Length == 0)
                    throw new Exception(String.Format("Incomplete specifications, UserAgent Service cannot function."));

                Object[] args = new Object[] { config };
                m_GridService = ServerUtils.LoadPlugin<IGridService>(gridService, args);
                m_GridUserService = ServerUtils.LoadPlugin<IGridUserService>(gridUserService, args);
                m_GatekeeperConnector = new GatekeeperServiceConnector();
                m_GatekeeperService = ServerUtils.LoadPlugin<IGatekeeperService>(gatekeeperService, args);
                m_FriendsService = ServerUtils.LoadPlugin<IFriendsService>(friendsService, args);
                m_PresenceService = ServerUtils.LoadPlugin<IPresenceService>(presenceService, args);
                m_UserAccountService = ServerUtils.LoadPlugin<IUserAccountService>(userAccountService, args);

                m_LevelOutsideContacts = serverConfig.GetInt("LevelOutsideContacts", 0);
                m_ShowDetails = serverConfig.GetBoolean("ShowUserDetailsInHGProfile", true);

                LoadTripPermissionsFromConfig(serverConfig, "ForeignTripsAllowed");
                LoadDomainExceptionsFromConfig(serverConfig, "AllowExcept", m_TripsAllowedExceptions);
                LoadDomainExceptionsFromConfig(serverConfig, "DisallowExcept", m_TripsDisallowedExceptions);

                m_GridName = Util.GetConfigVarFromSections<string>(config, "GatekeeperURI",
                    new string[] { "Startup", "Hypergrid", "UserAgentService" }, String.Empty);
                if (string.IsNullOrEmpty(m_GridName)) // Legacy. Remove soon.
                {
                    m_GridName = serverConfig.GetString("ExternalName", string.Empty);
                    if (m_GridName.Length == 0)
                    {
                        serverConfig = config.Configs["GatekeeperService"];
                        m_GridName = serverConfig.GetString("ExternalName", string.Empty);
                    }
                }

                if (!string.IsNullOrEmpty(m_GridName))
                {
                    m_GridName = m_GridName.ToLowerInvariant();
                    if (!m_GridName.EndsWith("/"))
                        m_GridName += "/";
                    if (!Uri.TryCreate(m_GridName, UriKind.Absolute, out Uri gateURI))
                        throw new Exception(String.Format("[UserAgentService] could not parse gatekeeper uri"));
                    string host = gateURI.DnsSafeHost;
                    IPAddress ip = Util.GetHostFromDNS(host);
                    if(ip is null)
                        throw new Exception(String.Format("[UserAgentService] failed to resolve gatekeeper host"));
                    m_MyExternalIP = ip.ToString();
                }
                // Finally some cleanup
                m_Database.DeleteOld();

            }
        }

        protected void LoadTripPermissionsFromConfig(IConfig config, string variable)
        {
            foreach (string keyName in config.GetKeys())
            {
                if (keyName.StartsWith(variable + "_Level_"))
                {
                    if (Int32.TryParse(keyName.Replace(variable + "_Level_", ""), out int level))
                        m_ForeignTripsAllowed.Add(level, config.GetBoolean(keyName, true));
                }
            }
        }

        protected void LoadDomainExceptionsFromConfig(IConfig config, string variable, Dictionary<int, List<string>> exceptions)
        {
            foreach (string keyName in config.GetKeys())
            {
                if (keyName.StartsWith(variable + "_Level_"))
                {
                    if (Int32.TryParse(keyName.Replace(variable + "_Level_", ""), out int level) && !exceptions.ContainsKey(level))
                    {
                        exceptions.Add(level, new List<string>());
                        string value = config.GetString(keyName, string.Empty);
                        string[] parts = value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string s in parts)
                        {
                            string ss = s.Trim();
                            if(!ss.EndsWith("/"))
                                ss += '/';
                            exceptions[level].Add(ss);
                        }
                    }
                }
            }
        }

        public GridRegion GetHomeRegion(UUID userID, out Vector3 position, out Vector3 lookAt)
        {
            position = new Vector3(128, 128, 0); lookAt = Vector3.UnitY;

            m_log.DebugFormat("[USER AGENT SERVICE]: Request to get home region of user {0}", userID);

            GridRegion home = null;
            GridUserInfo uinfo = m_GridUserService.GetGridUserInfo(userID.ToString());
            if (uinfo is not null)
            {
                if (uinfo.HomeRegionID.IsNotZero())
                {
                    home = m_GridService.GetRegionByUUID(UUID.Zero, uinfo.HomeRegionID);
                    position = uinfo.HomePosition;
                    lookAt = uinfo.HomeLookAt;
                }
                if (home is null)
                {
                    List<GridRegion> defs = m_GridService.GetDefaultRegions(UUID.Zero);
                    if (defs is not null && defs.Count > 0)
                        home = defs[0];
                }
            }

            return home;
        }

        public bool LoginAgentToGrid(GridRegion source, AgentCircuitData agentCircuit, GridRegion gatekeeper, GridRegion finalDestination, bool fromLogin, out string reason)
        {
            m_log.DebugFormat("[USER AGENT SERVICE]: Request to login user {0} {1} (@{2}) to grid {3}",
                agentCircuit.firstname, agentCircuit.lastname, (fromLogin ? agentCircuit.IPAddress : "stored IP"), gatekeeper.ServerURI);

            string gridName = gatekeeper.ServerURI.ToLowerInvariant();

            UserAccount account = m_UserAccountService.GetUserAccount(UUID.Zero, agentCircuit.AgentID);
            if (account is null)
            {
                m_log.WarnFormat("[USER AGENT SERVICE]: Someone attempted to lauch a foreign user from here {0} {1}", agentCircuit.firstname, agentCircuit.lastname);
                reason = "Forbidden to launch your agents from here";
                return false;
            }

            // Is this user allowed to go there?
            if (m_GridName != gridName)
            {
                if (m_ForeignTripsAllowed.ContainsKey(account.UserLevel))
                {
                    bool allowed = m_ForeignTripsAllowed[account.UserLevel];

                    if (m_ForeignTripsAllowed[account.UserLevel] && IsException(gridName, account.UserLevel, m_TripsAllowedExceptions))
                        allowed = false;

                    if (!m_ForeignTripsAllowed[account.UserLevel] && IsException(gridName, account.UserLevel, m_TripsDisallowedExceptions))
                        allowed = true;

                    if (!allowed)
                    {
                        reason = "Your world does not allow you to visit the destination";
                        m_log.InfoFormat("[USER AGENT SERVICE]: Agents not permitted to visit {0}. Refusing service.", gridName);
                        return false;
                    }
                }
            }

            // Take the IP address + port of the gatekeeper (reg) plus the info of finalDestination
            GridRegion region = new(gatekeeper)
            {
                ServerURI = gatekeeper.ServerURI,
                ExternalHostName = finalDestination.ExternalHostName,
                InternalEndPoint = finalDestination.InternalEndPoint,
                RegionName = finalDestination.RegionName,
                RegionID = finalDestination.RegionID,
                RegionLocX = finalDestination.RegionLocX,
                RegionLocY = finalDestination.RegionLocY
            };

            // Generate a new service session
            agentCircuit.ServiceSessionID = region.ServerURI + ";" + UUID.Random();
            TravelingAgentInfo travel = CreateTravelInfo(agentCircuit, region, fromLogin, finalDestination.ServerURI, out TravelingAgentInfo old);

            if(!fromLogin && old is not null && !string.IsNullOrEmpty(old.ClientIPAddress))
            {
                m_log.DebugFormat("[USER AGENT SERVICE]: stored IP = {0}. Old circuit IP: {1}", old.ClientIPAddress, agentCircuit.IPAddress);
                agentCircuit.IPAddress = old.ClientIPAddress;
            }

            bool success;

            m_log.DebugFormat("[USER AGENT SERVICE]: this grid: {0}, desired grid: {1}, desired region: {2}", m_GridName, gridName, region.RegionID);

            if (m_GridName.Equals(gridName, StringComparison.InvariantCultureIgnoreCase))
            {
                success = m_GatekeeperService.LoginAgent(source, agentCircuit, finalDestination, out reason);
            }
            else
            {
                //TODO: Should there not be a call to QueryAccess here?
                EntityTransferContext ctx = new();
                success = m_GatekeeperConnector.CreateAgent(source, region, agentCircuit, (uint)Constants.TeleportFlags.ViaLogin, ctx, out reason);
            }

            if (!success)
            {
                m_log.DebugFormat("[USER AGENT SERVICE]: Unable to login user {0} {1} to grid {2}, reason: {3}",
                    agentCircuit.firstname, agentCircuit.lastname, region.ServerURI, reason);

                if (old is not null)
                    StoreTravelInfo(old);
                else
                    m_Database.Delete(agentCircuit.SessionID);

                return false;
            }

            // Everything is ok

            StoreTravelInfo(travel);

            return true;
        }

        public bool LoginAgentToGrid(GridRegion source, AgentCircuitData agentCircuit, GridRegion gatekeeper, GridRegion finalDestination, out string reason)
        {
            return LoginAgentToGrid(source, agentCircuit, gatekeeper, finalDestination, false, out reason);
        }

        TravelingAgentInfo CreateTravelInfo(AgentCircuitData agentCircuit, GridRegion region, bool fromLogin, string regionServerURI, out TravelingAgentInfo existing)
        {
            HGTravelingData hgt = m_Database.Get(agentCircuit.SessionID);
            existing = null;

            if (hgt is not null)
            {
                // Very important! Override whatever this agent comes with.
                // UserAgentService always sets the IP for every new agent
                // with the original IP address.
                existing = new TravelingAgentInfo(hgt);
                agentCircuit.IPAddress = existing.ClientIPAddress;
            }

            TravelingAgentInfo travel = new(existing)
            {
                SessionID = agentCircuit.SessionID,
                UserID = agentCircuit.AgentID,
                GridExternalName = region.ServerURI,
                ServiceToken = agentCircuit.ServiceSessionID,
                RegionServerURI = regionServerURI
            };

            if (fromLogin)
                travel.ClientIPAddress = agentCircuit.IPAddress;

            StoreTravelInfo(travel);

            return travel;
        }

        public void LogoutAgent(UUID userID, UUID sessionID)
        {
            m_log.DebugFormat("[USER AGENT SERVICE]: User {0} logged out", userID);

            // Capture whether this session was a stay on a foreign grid before
            // deleting it. This method is also called for ordinary local
            // logouts (the local sim notifies friends itself in that case).
            // m_GridName is lowercased in the constructor while GridExternalName
            // is stored raw, so the comparison must ignore case
            HGTravelingData hgt = m_Database.Get(sessionID);
            bool wasTravelingAbroad = hgt is not null && hgt.Data is not null
                && hgt.Data.TryGetValue("GridExternalName", out string gridName)
                && !m_GridName.Equals(gridName, StringComparison.InvariantCultureIgnoreCase);

            m_Database.Delete(sessionID);

            GridUserInfo guinfo = m_GridUserService.GetGridUserInfo(userID.ToString());
            if (guinfo is not null)
                m_GridUserService.LoggedOut(userID.ToString(), sessionID, guinfo.LastRegionID, guinfo.LastPosition, guinfo.LastLookAt);

            // A user logging out on a foreign grid has no sim on this grid to
            // send the offline status to their friends, and the foreign sim
            // doesn't know the visitor's friends; notify them from here.
            if (wasTravelingAbroad)
                Util.FireAndForget(o => NotifyFriendsOfStatus(userID, false), null, "UserAgentService.NotifyFriendsLoggedOut");
        }

        /// <summary>
        /// Send a status notification to all friends of this (local) user.
        /// Used when the status change happens on a foreign grid, where no sim
        /// of this grid is involved that could notify them.
        /// </summary>
        protected void NotifyFriendsOfStatus(UUID userID, bool online)
        {
            if (m_FriendsService is null)
                return;

            FriendInfo[] friends = m_FriendsService.GetFriends(userID);
            if (friends is null || friends.Length == 0)
                return;

            List<string> localFriends = new();
            Dictionary<string, List<string>> foreignFriendsPerDomain = new();
            foreach (FriendInfo fi in friends)
            {
                // same filter the sim-side FriendsModule.StatusChange applies
                if (fi.TheirFlags == -1 || (fi.MyFlags & (int)FriendRights.CanSeeOnline) == 0)
                    continue;

                if (UUID.TryParse(fi.Friend, out _))
                    localFriends.Add(fi.Friend);
                else if (Util.ParseUniversalUserIdentifier(fi.Friend, out _, out string url, out _, out _, out _))
                {
                    // foreign friend: group per grid, notified below
                    if (!foreignFriendsPerDomain.TryGetValue(url, out List<string> lst))
                    {
                        lst = new List<string>();
                        foreignFriendsPerDomain[url] = lst;
                    }
                    lst.Add(fi.Friend);
                }
            }

            if (localFriends.Count > 0)
            {
                // one batched presence lookup for all local friends instead of
                // one query per friend
                Dictionary<string, PresenceInfo> homeSessions = new();
                PresenceInfo[] sessions = m_PresenceService?.GetAgents(localFriends.ToArray());
                if (sessions is not null)
                    foreach (PresenceInfo p in sessions)
                        if (!p.RegionID.IsZero() && !homeSessions.ContainsKey(p.UserID)) // guard against traveling agents
                            homeSessions[p.UserID] = p;

                List<string> notAtHome = new(localFriends.Count);
                foreach (string friend in localFriends)
                {
                    // local friend: if online at home, notify their sim...
                    if (homeSessions.TryGetValue(friend, out PresenceInfo session))
                    {
                        NotifyLocalSimOfStatus(session.RegionID, userID, friend, online);
                        continue;
                    }

                    // ...or, if they are traveling abroad themselves, deliver via the
                    // visited grid below.
                    notAtHome.Add(friend);
                }

                // This is our own authoritative friend list for userID, already
                // filtered to local (plain-UUID) friends above, so no verifyFriend
                // callback is needed. The helper fires off one worker per candidate
                // internally, so one unreachable grid's request timeout can't stall
                // the notifications to the others.
                if (notAtHome.Count > 0)
                    TravelingFriendsNotifier.DeliverStatusToTravelers(this, notAtHome, userID, online);
            }

            foreach (KeyValuePair<string, List<string>> kvp in foreignFriendsPerDomain)
            {
                m_log.DebugFormat("[USER AGENT SERVICE]: Notifying {0} friends in {1} that user {2} is {3}",
                    kvp.Value.Count, kvp.Key, userID, online ? "online" : "offline");
                // each domain gets its own worker so one unreachable grid's
                // request timeout can't stall notifications to the others
                string domain = kvp.Key;
                List<string> ids = kvp.Value;
                Util.FireAndForget(o =>
                {
                    HGFriendsServicesConnector fConn = new HGFriendsServicesConnector(domain);
                    fConn.StatusNotification(ids, userID, online);
                }, null, "UserAgentService.NotifyForeignFriendsOfStatus");
            }
        }

        // We need to prevent foreign users with the same UUID as a local user
        public bool IsAgentComingHome(UUID sessionID, string thisGridExternalName)
        {
            HGTravelingData hgt = m_Database.Get(sessionID);
            if (hgt is null || hgt.Data is null)
                return false;
            if(!hgt.Data.TryGetValue("GridExternalName", out string htgGrid))
                return false;
            return htgGrid.Equals(thisGridExternalName, StringComparison.InvariantCultureIgnoreCase);
        }

        public bool VerifyClient(UUID sessionID, string reportedIP)
        {
            if (m_BypassClientVerification)
                return true;

            m_log.DebugFormat("[USER AGENT SERVICE]: Verifying Client session {0} with reported IP {1}.",
                sessionID, reportedIP);

            HGTravelingData hgt = m_Database.Get(sessionID);
            if (hgt is null)
                return false;

            TravelingAgentInfo travel = new(hgt);

            bool result = travel.ClientIPAddress == reportedIP;
            if(!result && !string.IsNullOrEmpty(m_MyExternalIP))
                result = reportedIP == m_MyExternalIP; // NATed

            m_log.DebugFormat("[USER AGENT SERVICE]: Comparing {0} with login IP {1} and MyIP {2}; result is {3}",
                                reportedIP, travel.ClientIPAddress, m_MyExternalIP, result);

            return result;
        }

        public bool VerifyAgent(UUID sessionID, string token)
        {
            HGTravelingData hgt = m_Database.Get(sessionID);
            if (hgt is null)
            {
                m_log.DebugFormat("[USER AGENT SERVICE]: Token verification for session {0}: no such session", sessionID);
                return false;
            }

            TravelingAgentInfo travel = new TravelingAgentInfo(hgt);
            m_log.DebugFormat("[USER AGENT SERVICE]: Verifying agent token {0} against {1}", token, travel.ServiceToken);
            return travel.ServiceToken == token;
        }

        [Obsolete]
        public List<UUID> StatusNotification(List<string> friends, UUID foreignUserID, bool online)
        {
            if (m_FriendsService == null || m_PresenceService == null)
            {
                m_log.WarnFormat("[USER AGENT SERVICE]: Unable to perform status notifications because friends or presence services are missing");
                return new List<UUID>();
            }

            List<UUID> localFriendsOnline = new();

            m_log.DebugFormat("[USER AGENT SERVICE]: Status notification: foreign user {0} wants to notify {1} local friends", foreignUserID, friends.Count);

            // First, let's double check that the reported friends are, indeed, friends of that user
            // And let's check that the secret matches
            List<string> usersToBeNotified = new();
            foreach (string uui in friends)
            {
                if (Util.ParseUniversalUserIdentifier(uui, out UUID localUserID, out _, out _, out _, out string secret))
                {
                    FriendInfo[] friendInfos = m_FriendsService.GetFriends(localUserID);
                    foreach (FriendInfo finfo in friendInfos)
                    {
                        if (finfo.Friend.StartsWith(foreignUserID.ToString()) && finfo.Friend.EndsWith(secret))
                        {
                            // great!
                            usersToBeNotified.Add(localUserID.ToString());
                        }
                    }
                }
            }

            // Now, let's send the notifications
            m_log.DebugFormat("[USER AGENT SERVICE]: Status notification: user has {0} local friends", usersToBeNotified.Count);

            // First, let's send notifications to local users who are online in the home grid
            PresenceInfo[] friendSessions = m_PresenceService.GetAgents(usersToBeNotified.ToArray());
            if (friendSessions != null && friendSessions.Length > 0)
            {
                PresenceInfo friendSession = null;
                foreach (PresenceInfo pinfo in friendSessions)
                {
                    if (pinfo.RegionID.IsNotZero()) // let's guard against traveling agents
                    {
                        friendSession = pinfo;
                        break;
                    }
                }
                if (friendSession is not null)
                {
                    ForwardStatusNotificationToSim(friendSession.RegionID, foreignUserID, friendSession.UserID, online);
                    usersToBeNotified.Remove(friendSession.UserID.ToString());
                    if (UUID.TryParse(friendSession.UserID, out UUID id))
                        localFriendsOnline.Add(id);

                }
            }

            //// Lastly, let's notify the rest who may be online somewhere else
            //foreach (string user in usersToBeNotified)
            //{
            //    UUID id = new UUID(user);
            //    if (m_Database.ContainsKey(id) && m_Database[id].GridExternalName != m_GridName)
            //    {
            //        string url = m_Database[id].GridExternalName;
            //        // forward
            //        m_log.WarnFormat("[USER AGENT SERVICE]: User {0} is visiting {1}. HG Status notifications still not implemented.", user, url);
            //    }
            //}

            // and finally, let's send the online friends
            if (online)
            {
                return localFriendsOnline;
            }
            else
                return new List<UUID>();
        }

        [Obsolete]
        protected void ForwardStatusNotificationToSim(UUID regionID, UUID foreignUserID, string user, bool online)
        {
            NotifyLocalSimOfStatus(regionID, foreignUserID, user, online);
        }

        // Same local-sim-vs-remote-REST dispatch as the obsolete method above
        // (and HGFriendsService's copy of it), kept non-obsolete so new code
        // (NotifyFriendsOfStatus) doesn't have to call deprecated API.
        private void NotifyLocalSimOfStatus(UUID regionID, UUID foreignUserID, string user, bool online)
        {
            if (UUID.TryParse(user, out UUID userID))
            {
                if (m_FriendsLocalSimConnector is not null)
                {
                    m_log.DebugFormat("[USER AGENT SERVICE]: Local Notify, user {0} is {1}", foreignUserID, (online ? "online" : "offline"));
                    m_FriendsLocalSimConnector.StatusNotify(foreignUserID, userID, online);
                }
                else
                {
                    GridRegion region = m_GridService.GetRegionByUUID(UUID.Zero /* !!! */, regionID);
                    if (region is not null)
                    {
                        m_log.DebugFormat("[USER AGENT SERVICE]: Remote Notify to region {0}, user {1} is {2}", region.RegionName, foreignUserID, (online ? "online" : "offline"));
                        m_FriendsSimConnector.StatusNotify(region, foreignUserID, userID.ToString(), online);
                    }
                }
            }
        }

        public List<UUID> GetOnlineFriends(UUID foreignUserID, List<string> friends)
        {
            List<UUID> online = new();

            if (m_FriendsService is null || m_PresenceService is null)
            {
                m_log.WarnFormat("[USER AGENT SERVICE]: Unable to get online friends because friends or presence services are missing");
                return online;
            }

            m_log.DebugFormat("[USER AGENT SERVICE]: Foreign user {0} wants to know status of {1} local friends", foreignUserID, friends.Count);

            // First, let's double check that the reported friends are, indeed, friends of that user
            // And let's check that the secret matches and the rights
            List<string> usersToBeNotified = new();
            foreach (string uui in friends)
            {
                if (Util.ParseUniversalUserIdentifier(uui, out UUID localUserID, out _, out _, out _, out string secret))
                {
                    FriendInfo[] friendInfos = m_FriendsService.GetFriends(localUserID);
                    foreach (FriendInfo finfo in friendInfos)
                    {
                        if (finfo.Friend.StartsWith(foreignUserID.ToString()) && finfo.Friend.EndsWith(secret) &&
                            (finfo.TheirFlags & (int)FriendRights.CanSeeOnline) != 0 && (finfo.TheirFlags != -1))
                        {
                            // great!
                            usersToBeNotified.Add(localUserID.ToString());
                        }
                    }
                }
            }

            // Now, let's find out their status
            m_log.DebugFormat("[USER AGENT SERVICE]: GetOnlineFriends: user has {0} local friends with status rights", usersToBeNotified.Count);

            // First, let's send notifications to local users who are online in the home grid
            PresenceInfo[] friendSessions = m_PresenceService.GetAgents(usersToBeNotified.ToArray());
            if (friendSessions is not null && friendSessions.Length > 0)
            {
                foreach (PresenceInfo pi in friendSessions)
                {
                    if (UUID.TryParse(pi.UserID, out UUID presenceID))
                        online.Add(presenceID);
                }
            }

            return online;
        }

        public Dictionary<string, object> GetUserInfo(UUID  userID)
        {
            Dictionary<string, object> info = new();

            if (m_UserAccountService is null)
            {
                m_log.WarnFormat("[USER AGENT SERVICE]: Unable to get user flags because user account service is missing");
                info["result"] = "fail";
                info["message"] = "UserAccountService is missing!";
                return info;
            }

            UserAccount account = m_UserAccountService.GetUserAccount(UUID.Zero /*!!!*/, userID);

            if (account != null)
            {
                info.Add("user_firstname", account.FirstName);
                info.Add("user_lastname", account.LastName);
                info.Add("result", "success");

                if (m_ShowDetails)
                {
                    info.Add("user_flags", account.UserFlags);
                    info.Add("user_created", account.Created);
                    info.Add("user_title", account.UserTitle);
                }
                else
                {
                    info.Add("user_flags", 0);
                    info.Add("user_created", 0);
                    info.Add("user_title", string.Empty);
                }
            }

            return info;
        }

        public Dictionary<string, object> GetServerURLs(UUID userID)
        {
            if (m_UserAccountService is null)
            {
                m_log.WarnFormat("[USER AGENT SERVICE]: Unable to get server URLs because user account service is missing");
                return new Dictionary<string, object>();
            }
            UserAccount account = m_UserAccountService.GetUserAccount(UUID.Zero /*!!!*/, userID);
            if (account != null)
                return account.ServiceURLs;

            return new Dictionary<string, object>();
        }

        public string LocateUser(UUID userID)
        {
            HGTravelingData[] hgts = m_Database.GetSessions(userID);
            if (hgts == null)
                return string.Empty;

            foreach (HGTravelingData t in hgts)
                // case-insensitive: m_GridName is lowercased, GridExternalName is stored raw
                if (t.Data.TryGetValue("GridExternalName", out string gridName)
                    && !m_GridName.Equals(gridName, StringComparison.InvariantCultureIgnoreCase))
                    return gridName;

            return string.Empty;
        }

        public bool StatusNotifyTravelingAgent(UUID userID, UUID friendID, bool online)
        {
            HGTravelingData[] hgts = m_Database.GetSessions(userID);
            if (hgts == null)
                return false;

            foreach (HGTravelingData t in hgts)
            {
                // case-insensitive: m_GridName is lowercased, GridExternalName is stored raw
                if (!t.Data.TryGetValue("GridExternalName", out string gridName)
                    || m_GridName.Equals(gridName, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                // Preferred: hand the notification to the visited grid's gatekeeper,
                // which resolves the traveler's current sim from its own presence
                // data and forwards grid-internally. This reaches the traveler even
                // after intra-grid teleports we never see. Note this does network
                // I/O; call from a worker thread.
                if (!m_StatusNotifyUnsupported.ContainsKey(gridName))
                {
                    bool delivered = m_GatekeeperConnector.StatusNotify(gridName, t.SessionID, userID, friendID, online, out bool notSupported);
                    if (delivered)
                    {
                        m_log.DebugFormat("[USER AGENT SERVICE]: {0} is traveling in {1}; status of {2} delivered via its gatekeeper",
                            userID, gridName, friendID);
                        return true;
                    }
                    if (notSupported)
                    {
                        // older grid without status_notify: stop asking it for a while
                        m_StatusNotifyUnsupported.Add(gridName, true, STATUS_NOTIFY_UNSUPPORTED_MS);
                    }
                    // else: the grid is unreachable, or it couldn't deliver (which is
                    // also the case in the short window after arrival before the
                    // destination sim reports the agent to presence); fall through to
                    // the stored sim URI as best effort
                }

                // Fallback: send directly to the sim recorded when the user entered
                // the grid. Only written at grid crossings, so it goes stale after
                // intra-grid teleports there — the pre-status_notify behavior.
                if (t.Data.TryGetValue("RegionURI", out string storedURI) && !string.IsNullOrEmpty(storedURI))
                {
                    m_log.DebugFormat("[USER AGENT SERVICE]: {0} is traveling in {1}; forwarding status of {2} to sim {3}",
                        userID, gridName, friendID, storedURI);
                    GridRegion region = new GridRegion { ServerURI = storedURI };
                    m_FriendsSimConnector.StatusNotify(region, friendID, userID.ToString(), online);
                    return true;
                }
                // no usable route in this record; a newer session row may have one
            }

            return false;
        }

        public string GetUUI(UUID userID, UUID targetUserID)
        {
            // Let's see if it's a local user
            UserAccount account = m_UserAccountService.GetUserAccount(UUID.Zero, targetUserID);
            if (account is not null)
                return targetUserID.ToString() + ";" + m_GridName + ";" + account.FirstName + " " + account.LastName ;

            // Let's try the list of friends
            if(m_FriendsService is not null)
            {
                FriendInfo[] friends = m_FriendsService.GetFriends(userID);
                if (friends is not null && friends.Length > 0)
                {
                    foreach (FriendInfo f in friends)
                        if (f.Friend.StartsWith(targetUserID.ToString()))
                        {
                            // Let's remove the secret
                            if (Util.ParseUniversalUserIdentifier(f.Friend, out _,
                                    out _, out _, out _, out string secret))
                                return f.Friend.Replace(secret, "0");
                        }
                }
            }
            return string.Empty;
        }

        public UUID GetUUID(String first, String last)
        {
            // Let's see if it's a local user
            UserAccount account = m_UserAccountService.GetUserAccount(UUID.Zero, first, last);
            if (account is not null)
            {
                // check user level
                if (account.UserLevel < m_LevelOutsideContacts)
                    return UUID.Zero;
                else
                    return account.PrincipalID;
            }
            else
                return UUID.Zero;
        }

        #region Misc

        private bool IsException(string dest, int level, Dictionary<int, List<string>> exceptions)
        {
            if (string.IsNullOrEmpty(dest))
                return false;
            if (!exceptions.TryGetValue(level, out List<string> excep) || excep.Count == 0)
                return false;

            string destination = dest;
            if (!destination.EndsWith("/"))
                destination += "/";

            foreach (string s in excep)
            {
                if (destination.Equals(s))
                    return true;
            }

            return false;
        }

        private void StoreTravelInfo(TravelingAgentInfo travel)
        {
            if (travel is null)
                return;

            HGTravelingData hgt = new()
            {
                SessionID = travel.SessionID,
                UserID = travel.UserID,
                Data = new Dictionary<string, string>
                {
                    ["GridExternalName"] = travel.GridExternalName,
                    ["ServiceToken"] = travel.ServiceToken,
                    ["ClientIPAddress"] = travel.ClientIPAddress,
                    ["RegionURI"] = travel.RegionServerURI
                }
            };

            m_Database.Store(hgt);
        }
        #endregion

    }

    class TravelingAgentInfo
    {
        public UUID SessionID;
        public UUID UserID;
        public string GridExternalName = string.Empty;
        public string ServiceToken = string.Empty;
        public string ClientIPAddress = string.Empty; // as seen from this user agent service
        public string RegionServerURI = string.Empty; // ServerURI of the specific sim region the user landed in

        public TravelingAgentInfo(HGTravelingData t)
        {
            if (t.Data is not null)
            {
                SessionID = new UUID(t.SessionID);
                UserID = new UUID(t.UserID);
                GridExternalName = t.Data["GridExternalName"];
                ServiceToken = t.Data["ServiceToken"];
                ClientIPAddress = t.Data["ClientIPAddress"];
                if (t.Data.ContainsKey("RegionURI"))
                    RegionServerURI = t.Data["RegionURI"];
            }
        }

        public TravelingAgentInfo(TravelingAgentInfo old)
        {
            if (old is not null)
            {
                SessionID = old.SessionID;
                UserID = old.UserID;
                GridExternalName = old.GridExternalName;
                ServiceToken = old.ServiceToken;
                ClientIPAddress = old.ClientIPAddress;
                RegionServerURI = old.RegionServerURI;
            }
        }
    }

}
