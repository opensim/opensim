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
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading;

using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Hypergrid;

using OpenMetaverse;
using log4net;
using Nini.Config;
using Mono.Addins;


namespace OpenSim.Region.CoreModules.Framework.UserManagement
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "UserManagementModule")]
    public class UserManagementModule : ISharedRegionModule, IUserManagement, IPeople
    {
        private const int BADURLEXPIRE = 2 * 60;
        private const int NOEXPIRE = int.MinValue;
        private const int LOCALEXPIRE = 3600000;
        private const int HGEXPIRE = 3600000;
        private const int BADEXPIRE = 3600000;
        private const int BADHGEXPIRE =600000;

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected bool m_Enabled;
        protected List<Scene> m_Scenes = new List<Scene>();

        protected IServiceThrottleModule m_ServiceThrottle;
        protected IUserAccountService m_userAccountService = null;
        protected IGridUserService m_gridUserService = null;

        protected GridInfo m_thisGridInfo;

        // The cache
        protected ExpiringCacheOS<UUID, UserData> m_userCacheByID = new ExpiringCacheOS<UUID, UserData>(120000);

        protected bool m_DisplayChangingHomeURI = false;

        UUID m_scopeID = UUID.Zero;

        ~UserManagementModule()
        {
            Dispose(false);
        }

        private bool disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;
                m_userCacheByID.Dispose();
                m_userCacheByID = null;
            }
        }

        #region ISharedRegionModule

        public virtual void Initialise(IConfigSource config)
        {
            string umanmod = config.Configs["Modules"].GetString("UserManagementModule", Name);
            if (umanmod == Name)
            {
                m_Enabled = true;
                Init(config);
                m_log.DebugFormat("[USER MANAGEMENT MODULE]: {0} is enabled", Name);
            }
        }

        public virtual bool IsSharedModule
        {
            get { return true; }
        }

        public virtual string Name
        {
            get { return "BasicUserManagementModule"; }
        }

        public virtual Type ReplaceableInterface
        {
            get { return null; }
        }

        public virtual void AddRegion(Scene scene)
        {
            if (m_Enabled)
            {
                lock (m_Scenes)
                {
                    m_Scenes.Add(scene);
                }
                if(m_thisGridInfo == null)
                    m_thisGridInfo = scene.SceneGridInfo;
                scene.RegisterModuleInterface<IUserManagement>(this);
                scene.RegisterModuleInterface<IPeople>(this);
                scene.EventManager.OnNewClient += EventManager_OnNewClient;
                scene.EventManager.OnPrimsLoaded += EventManager_OnPrimsLoaded;
            }
        }

        public virtual void RemoveRegion(Scene scene)
        {
            if (m_Enabled)
            {
                scene.UnregisterModuleInterface<IUserManagement>(this);
                lock (m_Scenes)
                {
                    m_Scenes.Remove(scene);
                }
            }
        }

        public virtual void RegionLoaded(Scene s)
        {
            if (!m_Enabled)
                return;
            if(m_ServiceThrottle == null)
                m_ServiceThrottle = s.RequestModuleInterface<IServiceThrottleModule>();
            if(m_userAccountService == null)
                m_userAccountService = s.UserAccountService;
            if(m_gridUserService == null)
                m_gridUserService = s.GridUserService;
            if (!s.RegionInfo.ScopeID.IsZero())
                m_scopeID = s.RegionInfo.ScopeID;
        }

        public virtual void PostInitialise()
        {
        }

        public virtual void Close()
        {
            m_Enabled = false;
            lock (m_Scenes)
            {
                m_Scenes.Clear();
            }
            m_thisGridInfo = null;
            Dispose(false);
        }

        #endregion ISharedRegionModule

        #region Event Handlers

        protected virtual void EventManager_OnPrimsLoaded(Scene s)
        {
            // let's sniff all the user names referenced by objects in the scene
            m_log.DebugFormat("[USER MANAGEMENT MODULE]: Caching creators' data from {0} ({1} objects)...", s.RegionInfo.RegionName, s.GetEntities().Length);
            s.ForEachSOG(delegate(SceneObjectGroup sog) { CacheCreators(sog); });
        }

        protected virtual void EventManager_OnNewClient(IClientAPI client)
        {
            client.OnConnectionClosed += HandleConnectionClosed;
            client.OnNameFromUUIDRequest += HandleUUIDNameRequest;
            client.OnAvatarPickerRequest += HandleAvatarPickerRequest;
        }

        protected virtual void HandleConnectionClosed(IClientAPI client)
        {
            client.OnNameFromUUIDRequest -= HandleUUIDNameRequest;
            client.OnAvatarPickerRequest -= HandleAvatarPickerRequest;
            client.OnConnectionClosed -= HandleConnectionClosed;
        }

        protected virtual void HandleUUIDNameRequest(UUID uuid, IClientAPI client)
        {
//            m_log.DebugFormat(
//                "[USER MANAGEMENT MODULE]: Handling request for name binding of UUID {0} from {1}",
//                uuid, remote_client.Name);
            if(!m_Enabled || m_Scenes.Count <= 0)
                return;

            if (m_userCacheByID.TryGetValue(uuid, out UserData user))
            {
                if (user.HasGridUserTried)
                {
                    client.SendNameReply(uuid, user.FirstName, user.LastName);
                    return;
                }
            }

            if(m_ServiceThrottle == null)
                return;

            IClientAPI deferedcli = client;
            // Not found in cache, queue continuation
            m_ServiceThrottle.Enqueue("uuidname", uuid.ToString(),  delegate
            {
                if(deferedcli.IsActive)
                {
                    if (GetUser(uuid, deferedcli.ScopeId, out UserData defuser))
                    {
                        if(deferedcli.IsActive)
                            deferedcli.SendNameReply(uuid, defuser.FirstName, defuser.LastName);
                    }
                }
                deferedcli = null;
            });
        }

        public virtual void HandleAvatarPickerRequest(IClientAPI client, UUID avatarID, UUID RequestID, string query)
        {
            //EventManager.TriggerAvatarPickerRequest();

            m_log.DebugFormat("[USER MANAGEMENT MODULE]: HandleAvatarPickerRequest for {0}", query);
            List<UserData> users = GetUserData(query, 500, 1);
            client.SendAvatarPickerReply(RequestID, users);
        }

        public bool CheckUrl(string url, out bool islocal, out OSHHTPHost host)
        {
            host = new OSHHTPHost(url);
            int type = m_thisGridInfo.IsLocalGrid(host);
            if(type < 0 )
            {
                islocal = false;
                return false;
            }
            islocal = type == 1;
            return true;
        }

        protected virtual void AddAdditionalUsers(string query, List<UserData> users, HashSet<UUID> found)
        {
        }

        #endregion Event Handlers

        #region IPeople
        public virtual UserData GetUserData(UUID id)
        {
            if(GetUser(id, out UserData u))
                return u;
            return null;
        }

        public virtual List<UserData> GetUserData(string query, int page_size, int page_number)
        {
             if(m_Scenes.Count <= 0 || m_userAccountService == null)
                return new List<UserData>();

            var users = new List<UserData>();
            var found = new HashSet<UUID>();

            // search the user accounts service
            if (m_userAccountService != null)
            {
                List<UserAccount> accs = m_userAccountService.GetUserAccounts(m_scopeID, query);
                if (accs != null)
                {
                    for(int i = 0; i < accs.Count; ++i)
                    {
                        UserAccount acc = accs[i];
                        UUID id = acc.PrincipalID;
                        UserData ud = new UserData();
                        ud.FirstName = acc.FirstName;
                        ud.LastName = acc.LastName;
                        ud.Id = id;
                        ud.HasGridUserTried = true;
                        ud.IsUnknownUser = false;
                        ud.IsLocal = acc.LocalToGrid;
                        users.Add(ud);
                        found.Add(id);
                    }
                }
            }

            // search the local cache
            string q = query.ToLower();
            foreach (UserData data in m_userCacheByID.Values)
            {
                if (found.Contains(data.Id))
                    continue;
                if (data.Id.IsZero() || data.IsUnknownUser)
                    continue;
                if (data.FirstName.ToLower().StartsWith(q) || data.LastName.ToLower().StartsWith(q))
                    users.Add(data);
            }

            AddAdditionalUsers(query, users, found);
            return users;
        }

        #endregion IPeople

        protected virtual void CacheCreators(SceneObjectGroup sog)
        {
            //m_log.DebugFormat("[USER MANAGEMENT MODULE]: processing {0} {1}; {2}", sog.RootPart.Name, sog.RootPart.CreatorData, sog.RootPart.CreatorIdentification);
            AddCreatorUser(sog.RootPart.CreatorID, sog.RootPart.CreatorData);

            foreach (SceneObjectPart sop in sog.Parts)
            {
                AddCreatorUser(sop.CreatorID, sop.CreatorData);
                foreach (TaskInventoryItem item in sop.TaskInventory.Values)
                    AddCreatorUser(item.CreatorID, item.CreatorData);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="uuid"></param>
        /// <param name="names">Caller please provide a properly instantiated array for names, string[2]</param>
        /// <returns></returns>
        protected virtual bool TryGetUserNames(UUID uuid, string[] names)
        {
            if (names == null)
                names = new string[2];

            if(GetUser(uuid, out UserData u))
            {
                names[0] = u.FirstName;
                names[1] = u.LastName;
                return true;
            }

            names[0] = "UnknownUMM3";
            names[1] = uuid.ToString();
            return false;
        }

        #region IUserManagement

        public virtual UUID GetUserIdByName(string name)
        {
            string[] parts = name.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                throw new Exception("Name must have 2 components");

            return GetUserIdByName(parts[0], parts[1]);
        }

        public virtual UUID GetUserIdByName(string firstName, string lastName)
        {
            if(m_Scenes.Count <= 0)
                return UUID.Zero;

            // TODO: Optimize for reverse lookup if this gets used by non-console commands.
            foreach (UserData user in m_userCacheByID.Values)
            {
                if (user.FirstName.Equals(firstName, StringComparison.InvariantCultureIgnoreCase) && 
                        user.LastName.Equals(lastName, StringComparison.InvariantCultureIgnoreCase))
                    return user.Id;
            }

            if(m_userAccountService != null)
            {
                UserAccount account = m_userAccountService.GetUserAccount(UUID.Zero, firstName, lastName);
                if (account != null)
                {
                    AddUser(account);
                    return account.PrincipalID;
                }
            }
            return UUID.Zero;
        }

        public virtual string GetUserName(UUID uuid)
        {
            if(GetUser(uuid, out UserData user))
                return user.FirstName + " " + user.LastName;
            return "UnknownUMM2 " + uuid.ToString();
        }

        public bool GetUserName(UUID uuid, out string FirstName, out string LastName)
        {
            if (GetUser(uuid, out UserData user))
            {
                FirstName = user.FirstName;
                LastName = user.LastName;
                return true;
            }

            FirstName = string.Empty;
            LastName = string.Empty;
            return false;
        }

        public virtual Dictionary<UUID,string> GetUsersNames(string[] ids, UUID scopeID)
        {
            var ret = new Dictionary<UUID,string>();
            if(m_Scenes.Count <= 0)
                return ret;

            List<string> missing = new List<string>(ids.Length);
            var untried = new Dictionary<UUID, UserData>();
            foreach (string id in ids)
            {
                if(!UUID.TryParse(id, out UUID uuid) || uuid.IsZero())
                    continue;

                if (m_userCacheByID.TryGetValue(uuid, out UserData userdata))
                {
                    if (userdata.HasGridUserTried)
                    {
                        ret[uuid] = userdata.FirstName + " " + userdata.LastName;
                        continue;
                    }
                    untried[uuid] = userdata;
                }
                missing.Add(id);
            }

            if(missing.Count == 0)
                return ret;

            ids = null;

            List<UserAccount> accounts = m_userAccountService.GetUserAccounts(scopeID, missing);
            if (accounts.Count != 0)
            {
                foreach (UserAccount uac in accounts)
                {
                    if (uac != null)
                    {
                        UUID id = uac.PrincipalID;

                        var userdata = new UserData();
                        userdata.Id = id;
                        userdata.FirstName = uac.FirstName;
                        userdata.LastName = uac.LastName;
                        userdata.HomeURL = string.Empty;
                        userdata.IsUnknownUser = false;
                        userdata.IsLocal = true;
                        userdata.HasGridUserTried = true;
                        m_userCacheByID.Add(id, userdata, 1800000);

                        ret[id] = uac.FirstName + " " + uac.LastName;
                        missing.Remove(id.ToString()); // slowww
                        untried.Remove(id);
                    }
                }
            }

            if (missing.Count == 0 || m_gridUserService == null)
                return ret;

            GridUserInfo[] pinfos = m_gridUserService.GetGridUserInfo(missing.ToArray());
            if (pinfos.Length > 0)
            {
                foreach (GridUserInfo uInfo in pinfos)
                {
                    if (uInfo != null && uInfo.UserID.Length > 36)
                    {
                        if (Util.ParseFullUniversalUserIdentifier(uInfo.UserID, out UUID u, out string url, out string first, out string last))
                        {
                            bool isvalid = CheckUrl(url, out bool islocal, out OSHHTPHost host);
                            var userdata = new UserData();
                            userdata.Id = u;
                            if (isvalid)
                            {
                                if (islocal)
                                {
                                    userdata.FirstName = first;
                                    userdata.LastName = last;
                                    userdata.HomeURL = string.Empty;
                                    userdata.IsLocal = true;
                                }
                                else
                                {
                                    userdata.FirstName = first.Replace(" ", ".") + "." + last.Replace(" ", ".");
                                    userdata.HomeURL = host.URI;
                                    userdata.LastName = "@" + host.HostAndPort;
                                    userdata.IsLocal = false;
                                }
                                userdata.IsUnknownUser = false;
                                userdata.HasGridUserTried = true;
                                m_userCacheByID.Add(u, userdata, 1800000);

                                ret[u] = userdata.FirstName + " " + userdata.LastName;
                                missing.Remove(u.ToString());
                                untried.Remove(u);
                            }
                        }
                        else
                            m_log.DebugFormat("[USER MANAGEMENT MODULE]: Unable to parse UUI {0}", uInfo.UserID);
                    }
                }
            }

            // add the untried in cache
            if (untried.Count > 0)
            {
                foreach (UserData ud in untried.Values)
                {
                    UUID id = ud.Id;
                    ud.HasGridUserTried = true;
                    m_userCacheByID.Add(id, ud, 1800000);

                    ret[id] = ud.FirstName + " " + ud.LastName;
                    missing.Remove(id.ToString());
                }
            }

            // add the UMMthings ( not sure we should)
            if (missing.Count > 0)
            {
                foreach (string id in missing)
                {
                    if (UUID.TryParse(id, out UUID uuid))
                        ret[uuid] = "Unknown UserUMMAU43";
                }
            }

            return ret;
        }

        public virtual Dictionary<UUID, string> GetKnownUserNames(string[] ids, UUID scopeID)
        {
            var ret = new Dictionary<UUID, string>();
            if (m_Scenes.Count <= 0)
                return ret;

            List<string> missing = new List<string>(ids.Length);
            var untried = new Dictionary<UUID, UserData>();
            foreach (string id in ids)
            {
                if (!UUID.TryParse(id, out UUID uuid) || uuid.IsZero())
                    continue;

                if (m_userCacheByID.TryGetValue(uuid, out UserData userdata))
                {
                    if (userdata.HasGridUserTried)
                    {
                        if(!userdata.IsUnknownUser)
                            ret[uuid] = userdata.FirstName + " " + userdata.LastName;
                        continue;
                    }
                    else
                        untried[uuid] = userdata;
                }
                missing.Add(id);
            }

            if (missing.Count == 0)
                return ret;

            ids = null;

            List<UserAccount> accounts = m_userAccountService.GetUserAccounts(scopeID, missing);
            if (accounts.Count != 0)
            {
                foreach (UserAccount uac in accounts)
                {
                    if (uac != null)
                    {
                        UUID id = uac.PrincipalID;

                        var userdata = new UserData();
                        userdata.Id = id;
                        userdata.FirstName = uac.FirstName;
                        userdata.LastName = uac.LastName;
                        userdata.HomeURL = string.Empty;
                        userdata.IsUnknownUser = false;
                        userdata.IsLocal = true;
                        userdata.HasGridUserTried = true;
                        m_userCacheByID.Add(id, userdata, 1800000);

                        ret[id] = uac.FirstName + " " + uac.LastName;
                        missing.Remove(id.ToString()); // slowww
                        untried.Remove(id);
                    }
                }
            }

            if (missing.Count == 0 || m_gridUserService == null)
                return ret;

            GridUserInfo[] pinfos = m_gridUserService.GetGridUserInfo(missing.ToArray());
            missing = null;
            if (pinfos.Length > 0)
            {
                foreach (GridUserInfo uInfo in pinfos)
                {
                    if (uInfo != null && uInfo.UserID.Length > 36)
                    {
                        if (Util.ParseFullUniversalUserIdentifier(uInfo.UserID, out UUID uuid, out string url, out string first, out string last))
                        {
                            bool isvalid = CheckUrl(url, out bool islocal, out OSHHTPHost host);
                            var userdata = new UserData();
                            userdata.Id = uuid;
                            if (isvalid)
                            {
                                if (islocal)
                                {
                                    userdata.FirstName = first;
                                    userdata.LastName = last;
                                    userdata.HomeURL = string.Empty;
                                    userdata.IsLocal = true;
                                }
                                else
                                {
                                    userdata.FirstName = first.Replace(" ", ".") + "." + last.Replace(" ", ".");
                                    userdata.HomeURL = host.URI;
                                    userdata.LastName = "@" + host.HostAndPort;
                                    userdata.IsLocal = false;
                                }

                                userdata.IsUnknownUser = false;
                                userdata.HasGridUserTried = true;
                                m_userCacheByID.Add(uuid, userdata, 1800000);

                                string name = userdata.FirstName + " " + userdata.LastName;
                                untried.Remove(uuid);
                                ret[uuid] = name;
                            }
                        }
                        else
                            m_log.DebugFormat("[USER MANAGEMENT MODULE]: Unable to parse UUI {0}", uInfo.UserID);
                    }
                }
            }

            foreach(UserData ud in untried.Values)
            {
                ud.HasGridUserTried = true;
                m_userCacheByID.Add(ud.Id, ud, 1800000);
                if(!ud.IsUnknownUser)
                    ret[ud.Id] = ud.FirstName + " " + ud.LastName;
            }
            return ret;
        }

        public List<UserData> GetKnownUsers(string[] ids, UUID scopeID)
        {
            if (m_Scenes.Count <= 0)
                return new List<UserData>();

            var ret = new List<UserData>(ids.Length);

            List<string> missing = new List<string>(ids.Length);
            var untried = new Dictionary<UUID, UserData>();
            foreach (string id in ids)
            {
                if (!UUID.TryParse(id, out UUID uuid) || uuid.IsZero())
                    continue;

                if (m_userCacheByID.TryGetValue(uuid, out UserData userdata))
                {
                    if (userdata.HasGridUserTried)
                    {
                        if (!userdata.IsUnknownUser)
                            ret.Add(userdata);
                        continue;
                    }
                    else
                        untried[uuid] = userdata;
                }
                missing.Add(id);
            }

            if (missing.Count == 0)
                return ret;

            ids = null;

            List<UserAccount> accounts = m_userAccountService.GetUserAccounts(scopeID, missing);
            if (accounts.Count != 0)
            {
                foreach (UserAccount uac in accounts)
                {
                    if (uac != null)
                    {
                        UUID id = uac.PrincipalID;

                        var userdata = new UserData();
                        userdata.Id = id;
                        userdata.FirstName = uac.FirstName;
                        userdata.LastName = uac.LastName;
                        userdata.HomeURL = string.Empty;
                        userdata.IsUnknownUser = false;
                        userdata.IsLocal = true;
                        userdata.HasGridUserTried = true;
                        m_userCacheByID.Add(id, userdata, 1800000);

                        ret.Add(userdata);
                        missing.Remove(id.ToString()); // slowww
                        untried.Remove(id);
                    }
                }
            }

            if (missing.Count == 0 || m_gridUserService == null)
                return ret;

            GridUserInfo[] pinfos = m_gridUserService.GetGridUserInfo(missing.ToArray());
            missing = null;
            if (pinfos.Length > 0)
            {
                foreach (GridUserInfo uInfo in pinfos)
                {
                    if (uInfo != null && uInfo.UserID.Length > 36)
                    {
                        if (Util.ParseFullUniversalUserIdentifier(uInfo.UserID, out UUID uuid, out string url, out string first, out string last))
                        {
                            bool isvalid = CheckUrl(url, out bool islocal, out OSHHTPHost host);
                            var userdata = new UserData();
                            userdata.Id = uuid;
                            if (isvalid)
                            {
                                if (islocal)
                                {
                                    userdata.FirstName = first;
                                    userdata.LastName = last;
                                    userdata.HomeURL = string.Empty;
                                    userdata.IsLocal = true;
                                }
                                else
                                {
                                    userdata.FirstName = first.Replace(" ", ".") + "." + last.Replace(" ", ".");
                                    userdata.HomeURL = host.URI;
                                    userdata.LastName = "@" + host.HostAndPort;
                                    userdata.IsLocal = false;
                                }

                                userdata.IsUnknownUser = false;
                                userdata.HasGridUserTried = true;
                                m_userCacheByID.Add(uuid, userdata, 1800000);

                                untried.Remove(uuid);
                                ret.Add(userdata);
                            }
                        }
                        else
                            m_log.DebugFormat("[USER MANAGEMENT MODULE]: Unable to parse UUI {0}", uInfo.UserID);
                    }
                }
            }

            foreach (UserData ud in untried.Values)
            {
                ud.HasGridUserTried = true;
                m_userCacheByID.Add(ud.Id, ud, 1800000);
                if (!ud.IsUnknownUser)
                    ret.Add(ud);
            }
            return ret;
        }

        public virtual string GetUserHomeURL(UUID userID)
        {
            if (GetUser(userID, out UserData user) && user != null)
            {
                if (user.LastWebFail > 0 && Util.GetTimeStamp() - user.LastWebFail > BADURLEXPIRE)
                    user.LastWebFail = -1;
                return user.HomeURL;
            }
            return string.Empty;
        }

        public virtual string GetUserHomeURL(UUID userID, out bool recentFail)
        {
            recentFail = false;
            if (GetUser(userID, out UserData user))
            {
                if (user.LastWebFail > 0)
                {
                    if (Util.GetTimeStamp() - user.LastWebFail > BADURLEXPIRE)
                        user.LastWebFail = -1;
                    else
                        recentFail = true;
                }
                return user.HomeURL;
            }
            return string.Empty;
        }

        public virtual string GetUserServerURL(UUID userID, string serverType)
        {
            UserData userdata;
            if(!GetUser(userID, out userdata))
                return string.Empty;

            if(userdata.IsLocal)
                return string.Empty;

            if(userdata.LastWebFail > 0)
            {
                if(Util.GetTimeStamp() - userdata.LastWebFail < BADURLEXPIRE)
                    return string.Empty;
                userdata.LastWebFail = -1;
            }

            if (userdata.ServerURLs != null)
            {
                if(userdata.ServerURLs.TryGetValue(serverType, out object ourl) && ourl != null)
                {
                    string turl = ourl as string;
                    OSHHTPHost otmp = new OSHHTPHost(turl);
                    if (otmp.IsValidHost)
                        return otmp.URI;
                }
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(userdata.HomeURL))
            {
                string homeuri = userdata.HomeURL.ToLower();
                if (!WebUtil.GlobalExpiringBadURLs.ContainsKey(homeuri))
                {
                    //m_log.DebugFormat("[USER MANAGEMENT MODULE]: Requested url type {0} for {1}", serverType, userID);
                    UserAgentServiceConnector uConn = new UserAgentServiceConnector(homeuri);
                    try
                    {
                        userdata.ServerURLs = uConn.GetServerURLs(userID);
                    }
                    catch(System.Net.WebException e)
                    {
                        m_log.DebugFormat("[USER MANAGEMENT MODULE]: GetServerURLs call failed {0}", e.Message);
                        WebUtil.GlobalExpiringBadURLs.Add(homeuri, BADURLEXPIRE * 1000);
                        userdata.ServerURLs = new Dictionary<string, object>();
                    }
                    catch (Exception e)
                    {
                        m_log.Debug("[USER MANAGEMENT MODULE]: GetServerURLs call failed ", e);
                        userdata.ServerURLs = new Dictionary<string, object>();
                    }

                    if (userdata.ServerURLs != null && userdata.ServerURLs.TryGetValue(serverType, out object ourl) && ourl != null)
                    {
                        string turl = ourl as string;
                        OSHHTPHost otmp = new OSHHTPHost(turl);
                        if (otmp.IsValidHost)
                            return otmp.URI;
                    }
                }
            }
            return string.Empty;
        }

        public void UserWebFailed(UUID id)
        {
            if(m_userCacheByID.TryGetValue(id, out UserData u))
                u.LastWebFail = Util.GetTimeStamp();
        }

        public virtual string GetUserServerURL(UUID userID, string serverType, out bool recentFail)
        {
            recentFail = false;
            if (!GetUser(userID, out UserData userdata))
                return string.Empty;

            if (userdata.IsLocal)
                return string.Empty;

            if (userdata.LastWebFail > 0)
            {
                if (Util.GetTimeStamp() - userdata.LastWebFail > BADURLEXPIRE)
                    userdata.LastWebFail = -1;
                else
                    recentFail = true;
            }

            if (userdata.ServerURLs != null)
            {
                if (userdata.ServerURLs.TryGetValue(serverType, out object ourl) && ourl != null)
                {
                    string turl = ourl as string;
                    OSHHTPHost otmp = new OSHHTPHost(turl);
                    if (otmp.IsValidHost)
                        return otmp.URI;
                }
                return string.Empty;
            }

            if (!recentFail && !string.IsNullOrEmpty(userdata.HomeURL))
            {
                string homeurl = userdata.HomeURL.ToLower();
                if(!WebUtil.GlobalExpiringBadURLs.ContainsKey(homeurl))
                {
                    //m_log.DebugFormat("[USER MANAGEMENT MODULE]: Requested url type {0} for {1}", serverType, userID);
                    UserAgentServiceConnector uConn = new UserAgentServiceConnector(homeurl);
                    try
                    {
                        userdata.ServerURLs = uConn.GetServerURLs(userID);
                    }
                    catch (System.Net.WebException e)
                    {
                        m_log.DebugFormat("[USER MANAGEMENT MODULE]: GetServerURLs call failed {0}", e.Message);
                        userdata.ServerURLs = new Dictionary<string, object>();
                        userdata.LastWebFail = Util.GetTimeStamp();
                        WebUtil.GlobalExpiringBadURLs.Add(homeurl, BADURLEXPIRE * 1000);
                        recentFail = true;
                    }
                    catch (Exception e)
                    {
                        m_log.Debug("[USER MANAGEMENT MODULE]: GetServerURLs call failed ", e);
                        userdata.ServerURLs = new Dictionary<string, object>();
                        userdata.LastWebFail = Util.GetTimeStamp();
                        recentFail = true;
                    }

                    if (userdata.ServerURLs != null && userdata.ServerURLs.TryGetValue(serverType, out object ourl) && ourl != null)
                    {
                        string turl = ourl as string;
                        OSHHTPHost otmp = new OSHHTPHost(turl);
                        if (otmp.IsValidHost)
                            return otmp.URI;
                    }
                }
            }
            return string.Empty;
        }

        public virtual string GetUserUUI(UUID userID)
        {
            string uui;
            GetUserUUI(userID, out uui);
            return uui;
        }

        public virtual bool GetUserUUI(UUID userID, out string uui)
        {
            if (GetUser(userID, out UserData ud) && ud != null)
            {
                if (ud.LastName.StartsWith("@"))
                {
                    string[] parts = ud.FirstName.Split('.');
                    if (parts.Length >= 2)
                        uui = userID.ToString() + ";" + ud.HomeURL + ";" + parts[0] + " " + parts[1];
                    else
                        uui = userID.ToString() + ";" + ud.HomeURL + ";" + ud.FirstName + " " + ud.LastName;
                }
                else
                    uui = userID.ToString();
                return true;
            }

            uui = userID.ToString();
            return false;
        }

        #region Cache Management
        public virtual bool GetUser(UUID uuid, out UserData userdata)
        {
             return GetUser(uuid, m_scopeID, out userdata);
        }

        public virtual bool GetUser(UUID uuid, UUID scopeID, out UserData userdata)
        {
            if (m_Scenes.Count <= 0)
            {
                userdata = new UserData();
                return false;
            }

            if (m_userCacheByID.TryGetValue(uuid, out userdata))
            {
                if (userdata.HasGridUserTried)
                    return true;
            }
            else
            {
                userdata = new UserData();
                userdata.Id = uuid;
                userdata.FirstName = "Unknown";
                userdata.LastName = uuid.ToString();
                userdata.HomeURL = string.Empty;
                userdata.IsUnknownUser = true;
                userdata.HasGridUserTried = false;
            }

            if (!userdata.HasGridUserTried)
            {
                /* rewrite here */
                UserAccount account = m_userAccountService.GetUserAccount(scopeID, uuid);
                if (account != null)
                {
                    userdata.FirstName = account.FirstName;
                    userdata.LastName = account.LastName;
                    userdata.HomeURL = string.Empty;
                    userdata.IsUnknownUser = false;
                    userdata.IsLocal = true;
                    userdata.HasGridUserTried = true;
                }
            }

            if (!userdata.HasGridUserTried)
            {
                GridUserInfo uInfo = null;
                if (null != m_gridUserService)
                {
                    uInfo = m_gridUserService.GetGridUserInfo(uuid.ToString());
                }
                if (uInfo != null)
                {
                    if (Util.ParseFullUniversalUserIdentifier(uInfo.UserID, out UUID u, out string url, out string first, out string last))
                    {
                        bool isvalid = CheckUrl(url, out bool islocal, out OSHHTPHost host);

                        if (isvalid)
                        {
                            if(islocal)
                            {
                                userdata.FirstName = first;
                                userdata.LastName = last;
                                userdata.HomeURL = string.Empty;
                                userdata.IsLocal = true;
                                userdata.IsUnknownUser = false;
                            }
                            else
                            {
                                userdata.FirstName = first.Replace(" ", ".") + "." + last.Replace(" ", ".");
                                userdata.HomeURL = host.URI;
                                userdata.LastName = "@" + host.HostAndPort;
                                userdata.IsLocal = false;
                                userdata.IsUnknownUser = false;
                            }
                        }
                    }
                    else
                        m_log.DebugFormat("[USER MANAGEMENT MODULE]: Unable to parse UUI {0}", uInfo.UserID);
                }
                userdata.HasGridUserTried = true;
            }
            /* END: do not wrap this code in any lock here */

            m_userCacheByID.Add(uuid, userdata, 1800000);
            return !userdata.IsUnknownUser;
        }

        public void AddUser(UserAccount account)
        {
            UUID id = account.PrincipalID;
            bool local = account.LocalToGrid;

            UserData user = new UserData();
            user.Id = id;
            user.FirstName = account.FirstName;
            user.LastName = account.LastName;
            user.HomeURL = string.Empty;
            user.IsUnknownUser = false;
            user.HasGridUserTried = true;
            user.IsLocal = local;
            m_userCacheByID.Add(id, user, local ? LOCALEXPIRE : HGEXPIRE);
        }

        public void AddSystemUser(UUID uuid, string first, string last)
        {
            UserData user = new UserData()
            {
                Id = uuid,
                FirstName = first,
                LastName = last,
                IsLocal = true,
                HasGridUserTried = true,
                HomeURL = string.Empty,
                IsUnknownUser = false
            };
            m_userCacheByID.Add(uuid, user, NOEXPIRE);
        }

        public void AddNPCUser(UUID uuid, string first, string last)
        {
            UserData user = new UserData()
            {
                Id = uuid,
                FirstName = first,
                LastName = last,
                HasGridUserTried = true,
                IsLocal = true,
                HomeURL = string.Empty,
                IsUnknownUser = false
            };
            m_userCacheByID.Add(uuid, user, NOEXPIRE);
        }

        public virtual void AddUser(UUID uuid, string first, string last, string homeURL)
        {
            //m_log.DebugFormat("[USER MANAGEMENT MODULE]: Adding user with id {0}, first {1}, last {2}, url {3}", uuid, first, last, homeURL);

            UserData oldUser;
            if (m_userCacheByID.TryGetValue(uuid, out oldUser))
            {
                if (!oldUser.IsUnknownUser)
                {
                    if (!homeURL.Equals(oldUser.HomeURL) && m_DisplayChangingHomeURI)
                    {
                        m_log.DebugFormat("[USER MANAGEMENT MODULE]: Different HomeURI for {0} {1} ({2}): {3} and {4}",
                            first, last, uuid.ToString(), homeURL, oldUser.HomeURL);
                    }
                    /* no update needed */
                    return;
                }
            }

            oldUser = new UserData();
            oldUser.Id = uuid;
            oldUser.HasGridUserTried = false;
            oldUser.IsUnknownUser = false;

            bool local;
            if (CheckUrl(homeURL, out local, out OSHHTPHost host))
            {
                if (local)
                {
                    oldUser.FirstName = first;
                    oldUser.LastName = last;
                    oldUser.IsLocal = true;
                    oldUser.HomeURL = string.Empty;
                    oldUser.HasGridUserTried = true;
                    m_userCacheByID.Add(uuid, oldUser, LOCALEXPIRE);
                }
                else
                {
                    oldUser.FirstName = first.Replace(" ", ".") + "." + last.Replace(" ", ".");
                    oldUser.LastName = "@" + host.HostAndPort;
                    oldUser.HomeURL = host.URI;
                    oldUser.IsLocal = false;
                    m_userCacheByID.Add(uuid, oldUser, HGEXPIRE);
                }
            }
            else
            {
                oldUser.FirstName = first.Replace(" ", ".") + "." + last.Replace(" ", ".");
                oldUser.LastName = "UMMM0Unknown";
                oldUser.IsLocal = true;
                oldUser.HomeURL = string.Empty;
                oldUser.HasGridUserTried = true;
                oldUser.IsUnknownUser = true;
                m_userCacheByID.Add(uuid, oldUser, BADEXPIRE);
            }
        }

        public virtual void AddCreatorUser(UUID id, string creatorData)
        {
            // m_log.InfoFormat("[USER MANAGEMENT MODULE]: Adding user with id {0}, creatorData {1}", id, creatorData);

            if(string.IsNullOrEmpty(creatorData))
                return;

            if(m_userCacheByID.ContainsKey(id))
                return;

            string homeURL;
            string firstname = string.Empty;
            string lastname = string.Empty;

            //creatorData = <endpoint>;<name>
            string[] parts = creatorData.Split(';');
            if(parts.Length > 1)
            {
                string[] nameparts = parts[1].Split(' ');
                if(nameparts.Length < 2)
                    return;
                firstname = nameparts[0];
                for(int xi = 1; xi < nameparts.Length; ++xi)
                    lastname += nameparts[xi];

                if (string.IsNullOrWhiteSpace(firstname))
                    return;
                if (string.IsNullOrWhiteSpace(lastname))
                    return;
            }
            else
                return;

            homeURL = parts[0];

            var oldUser = new UserData();
            oldUser.Id = id;
            oldUser.HasGridUserTried = false;
            oldUser.IsUnknownUser = false;

            OSHTTPURI homeuri = new OSHTTPURI(homeURL);
            if (homeuri.IsValidHost)
            {
                if (m_thisGridInfo.IsLocalGrid(homeuri.URL) == 1) // local
                {
                    oldUser.FirstName = firstname;
                    oldUser.LastName = lastname;
                    oldUser.IsLocal = true;
                    oldUser.HomeURL = string.Empty;
                    oldUser.HasGridUserTried = true;
                }
                else
                {
                    oldUser.FirstName = firstname + "." + lastname.Replace(" ", ".");
                    oldUser.LastName = "@" + homeuri.HostAndPort;
                    oldUser.HomeURL = homeuri.URL;
                    oldUser.IsLocal = false;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(homeuri.Host)) // take this as local
                {
                    oldUser.FirstName = firstname;
                    oldUser.LastName = lastname;
                    oldUser.IsLocal = true;
                    oldUser.HomeURL = string.Empty;
                    oldUser.HasGridUserTried = true;
                }
                else
                {
                    oldUser.FirstName = firstname + "." + lastname.Replace(" ", ".");
                    oldUser.LastName = "UMMM1Unknown";
                    oldUser.IsLocal = true;
                    oldUser.HomeURL = string.Empty;
                    oldUser.HasGridUserTried = true;
                    oldUser.IsUnknownUser = true;
                }
            }
            m_userCacheByID.Add(id, oldUser, NOEXPIRE);
        }

        public bool RemoveUser(UUID uuid)
        {
            return m_userCacheByID.Remove(uuid);
        }

        #endregion

        public virtual bool IsLocalGridUser(UUID uuid)
        {
            if (m_Scenes.Count <= 0)
                return true;

            if (m_userCacheByID.TryGetValue(uuid, out UserData u))
            {
                if (u.HasGridUserTried)
                    return u.IsLocal;
            }

            if(m_userAccountService == null)
                return true;

            UserAccount account = m_userAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, uuid);
            if (account == null)
                return false;

            if(u == null)
                AddUser(account);
            else
                u.HasGridUserTried = true;
            return true;
        }

        #endregion IUserManagement

        protected virtual void Init(IConfigSource config)
        {
            AddSystemUser(UUID.Zero, "Unknown", "User");
            AddSystemUser(Constants.m_MrOpenSimID, "Mr", "Opensim");
            RegisterConsoleCmds();

            IConfig userManagementConfig = config.Configs["UserManagement"];
            if (userManagementConfig != null)
                m_DisplayChangingHomeURI = userManagementConfig.GetBoolean("DisplayChangingHomeURI", false);

        }

        protected virtual void RegisterConsoleCmds()
        {
            MainConsole.Instance.Commands.AddCommand("Users", true,
                "show name",
                "show name <uuid>",
                "Show the bindings between a single user UUID and a user name",
                String.Empty,
                HandleShowUser);

            MainConsole.Instance.Commands.AddCommand("Users", true,
                "show names",
                "show names",
                "Show the bindings between user UUIDs and user names",
                String.Empty,
                HandleShowUsers);

            MainConsole.Instance.Commands.AddCommand("Users", true,
                "reset user cache",
                "reset user cache",
                "reset user cache to allow changed settings to be applied",
                String.Empty,
                HandleResetUserCache);
        }

        protected virtual void HandleResetUserCache(string module, string[] cmd)
        {
            m_userCacheByID.Clear();
        }

        protected virtual void HandleShowUser(string module, string[] cmd)
        {
            if (cmd.Length < 3)
            {
                MainConsole.Instance.Output("Usage: show name <uuid>");
                return;
            }

            UUID userId;
            if (!ConsoleUtil.TryParseConsoleUuid(MainConsole.Instance, cmd[2], out userId))
                return;

            UserData ud;

            if(!GetUser(userId, out ud))
            {
                MainConsole.Instance.Output("No name known for user with id {0}", userId);
                return;
            }

            ConsoleDisplayTable cdt = new ConsoleDisplayTable();
            cdt.AddColumn("UUID", 36);
            cdt.AddColumn("Name", 30);
            cdt.AddColumn("HomeURL", 40);
            cdt.AddRow(userId, string.Format("{0} {1}", ud.FirstName, ud.LastName), ud.HomeURL);

            MainConsole.Instance.Output(cdt.ToString());
        }

        protected virtual void HandleShowUsers(string module, string[] cmd)
        {
            ConsoleDisplayTable cdt = new ConsoleDisplayTable();
            cdt.AddColumn("UUID", 36);
            cdt.AddColumn("Name", 30);
            cdt.AddColumn("HomeURL", 40);
            cdt.AddColumn("Checked", 10);

            foreach(UserData u in m_userCacheByID.Values)
            {
                cdt.AddRow(u.Id, string.Format("{0} {1}", u.FirstName, u.LastName), u.HomeURL, u.HasGridUserTried ? "yes" : "no");
            }

            MainConsole.Instance.Output(cdt.ToString());
        }
    }
}
