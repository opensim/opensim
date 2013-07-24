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
using System.IO;
using System.Reflection;
using System.Threading;

using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.ClientStack.LindenUDP;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Hypergrid;

using OpenMetaverse;
using OpenMetaverse.Packets;
using log4net;
using Nini.Config;
using Mono.Addins;

namespace OpenSim.Region.CoreModules.Framework.UserManagement
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "UserManagementModule")]
    public class UserManagementModule : ISharedRegionModule, IUserManagement, IPeople
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected bool m_Enabled;
        protected List<Scene> m_Scenes = new List<Scene>();

        protected IServiceThrottleModule m_ServiceThrottle;
        // The cache
        protected Dictionary<UUID, UserData> m_UserCache = new Dictionary<UUID, UserData>();

        #region ISharedRegionModule

        public void Initialise(IConfigSource config)
        {
            string umanmod = config.Configs["Modules"].GetString("UserManagementModule", Name);
            if (umanmod == Name)
            {
                m_Enabled = true;
                Init();
                m_log.DebugFormat("[USER MANAGEMENT MODULE]: {0} is enabled", Name);
            }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public virtual string Name
        {
            get { return "BasicUserManagementModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void AddRegion(Scene scene)
        {
            if (m_Enabled)
            {
                m_Scenes.Add(scene);

                scene.RegisterModuleInterface<IUserManagement>(this);
                scene.RegisterModuleInterface<IPeople>(this);
                scene.EventManager.OnNewClient += new EventManager.OnNewClientDelegate(EventManager_OnNewClient);
                scene.EventManager.OnPrimsLoaded += new EventManager.PrimsLoaded(EventManager_OnPrimsLoaded);
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_Enabled)
            {
                scene.UnregisterModuleInterface<IUserManagement>(this);
                m_Scenes.Remove(scene);
            }
        }

        public void RegionLoaded(Scene s)
        {
            if (m_Enabled && m_ServiceThrottle == null)
                m_ServiceThrottle = s.RequestModuleInterface<IServiceThrottleModule>();
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
            m_Scenes.Clear();

            lock (m_UserCache)
                m_UserCache.Clear();
        }

        #endregion ISharedRegionModule

 
        #region Event Handlers

        void EventManager_OnPrimsLoaded(Scene s)
        {
            // let's sniff all the user names referenced by objects in the scene
            m_log.DebugFormat("[USER MANAGEMENT MODULE]: Caching creators' data from {0} ({1} objects)...", s.RegionInfo.RegionName, s.GetEntities().Length);
            s.ForEachSOG(delegate(SceneObjectGroup sog) { CacheCreators(sog); });
        }

        void EventManager_OnNewClient(IClientAPI client)
        {
            client.OnConnectionClosed += new Action<IClientAPI>(HandleConnectionClosed);
            client.OnNameFromUUIDRequest += new UUIDNameRequest(HandleUUIDNameRequest);
            client.OnAvatarPickerRequest += new AvatarPickerRequest(HandleAvatarPickerRequest);
        }

        void HandleConnectionClosed(IClientAPI client)
        {
            client.OnNameFromUUIDRequest -= new UUIDNameRequest(HandleUUIDNameRequest);
            client.OnAvatarPickerRequest -= new AvatarPickerRequest(HandleAvatarPickerRequest);
        }

        void HandleUUIDNameRequest(UUID uuid, IClientAPI client)
        {
//            m_log.DebugFormat(
//                "[USER MANAGEMENT MODULE]: Handling request for name binding of UUID {0} from {1}", 
//                uuid, remote_client.Name);

            if (m_Scenes[0].LibraryService != null && (m_Scenes[0].LibraryService.LibraryRootFolder.Owner == uuid))
            {
                client.SendNameReply(uuid, "Mr", "OpenSim");
            }
            else
            {
                string[] names = new string[2];
                if (TryGetUserNamesFromCache(uuid, names))
                {
                    client.SendNameReply(uuid, names[0], names[1]);
                    return;
                }

                // Not found in cache, queue continuation
                m_ServiceThrottle.Enqueue("name", uuid.ToString(),  delegate
                {
                    //m_log.DebugFormat("[YYY]: Name request {0}", uuid);
                    bool foundRealName = TryGetUserNames(uuid, names);

                    if (names.Length == 2)
                    {
                        if (!foundRealName)
                            m_log.DebugFormat("[USER MANAGEMENT MODULE]: Sending {0} {1} for {2} to {3} since no bound name found", names[0], names[1], uuid, client.Name);

                        client.SendNameReply(uuid, names[0], names[1]);
                    }
                });

            }
        }

        public void HandleAvatarPickerRequest(IClientAPI client, UUID avatarID, UUID RequestID, string query)
        {
            //EventManager.TriggerAvatarPickerRequest();

            m_log.DebugFormat("[USER MANAGEMENT MODULE]: HandleAvatarPickerRequest for {0}", query);

            List<UserData> users = GetUserData(query, 500, 1);

            AvatarPickerReplyPacket replyPacket = (AvatarPickerReplyPacket)PacketPool.Instance.GetPacket(PacketType.AvatarPickerReply);
            // TODO: don't create new blocks if recycling an old packet

            AvatarPickerReplyPacket.DataBlock[] searchData =
                new AvatarPickerReplyPacket.DataBlock[users.Count];
            AvatarPickerReplyPacket.AgentDataBlock agentData = new AvatarPickerReplyPacket.AgentDataBlock();

            agentData.AgentID = avatarID;
            agentData.QueryID = RequestID;
            replyPacket.AgentData = agentData;
            //byte[] bytes = new byte[AvatarResponses.Count*32];

            int i = 0;
            foreach (UserData item in users)
            {
                UUID translatedIDtem = item.Id;
                searchData[i] = new AvatarPickerReplyPacket.DataBlock();
                searchData[i].AvatarID = translatedIDtem;
                searchData[i].FirstName = Utils.StringToBytes((string)item.FirstName);
                searchData[i].LastName = Utils.StringToBytes((string)item.LastName);
                i++;
            }
            if (users.Count == 0)
            {
                searchData = new AvatarPickerReplyPacket.DataBlock[0];
            }
            replyPacket.Data = searchData;

            AvatarPickerReplyAgentDataArgs agent_data = new AvatarPickerReplyAgentDataArgs();
            agent_data.AgentID = replyPacket.AgentData.AgentID;
            agent_data.QueryID = replyPacket.AgentData.QueryID;

            List<AvatarPickerReplyDataArgs> data_args = new List<AvatarPickerReplyDataArgs>();
            for (i = 0; i < replyPacket.Data.Length; i++)
            {
                AvatarPickerReplyDataArgs data_arg = new AvatarPickerReplyDataArgs();
                data_arg.AvatarID = replyPacket.Data[i].AvatarID;
                data_arg.FirstName = replyPacket.Data[i].FirstName;
                data_arg.LastName = replyPacket.Data[i].LastName;
                data_args.Add(data_arg);
            }
            client.SendAvatarPickerReply(agent_data, data_args);
        }

        protected virtual void AddAdditionalUsers(string query, List<UserData> users)
        {
        }

        #endregion Event Handlers

        #region IPeople

        public List<UserData> GetUserData(string query, int page_size, int page_number)
        {
            // search the user accounts service
            List<UserAccount> accs = m_Scenes[0].UserAccountService.GetUserAccounts(m_Scenes[0].RegionInfo.ScopeID, query);

            List<UserData> users = new List<UserData>();
            if (accs != null)
            {
                foreach (UserAccount acc in accs)
                {
                    UserData ud = new UserData();
                    ud.FirstName = acc.FirstName;
                    ud.LastName = acc.LastName;
                    ud.Id = acc.PrincipalID;
                    users.Add(ud);
                }
            }

            // search the local cache
            lock (m_UserCache)
            {
                foreach (UserData data in m_UserCache.Values)
                {
                    if (users.Find(delegate(UserData d) { return d.Id == data.Id; }) == null &&
                        (data.FirstName.ToLower().StartsWith(query.ToLower()) || data.LastName.ToLower().StartsWith(query.ToLower())))
                        users.Add(data);
                }
            }

            AddAdditionalUsers(query, users);

            return users;

        }

        #endregion IPeople

        private void CacheCreators(SceneObjectGroup sog)
        {
            //m_log.DebugFormat("[USER MANAGEMENT MODULE]: processing {0} {1}; {2}", sog.RootPart.Name, sog.RootPart.CreatorData, sog.RootPart.CreatorIdentification);
            AddUser(sog.RootPart.CreatorID, sog.RootPart.CreatorData);

            foreach (SceneObjectPart sop in sog.Parts)
            {
                AddUser(sop.CreatorID, sop.CreatorData);
                foreach (TaskInventoryItem item in sop.TaskInventory.Values)
                    AddUser(item.CreatorID, item.CreatorData);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uuid"></param>
        /// <param name="names">Caller please provide a properly instantiated array for names, string[2]</param>
        /// <returns></returns>
        private bool TryGetUserNames(UUID uuid, string[] names)
        {
            if (names == null)
                names = new string[2];

            if (TryGetUserNamesFromCache(uuid, names))
                return true;

            if (TryGetUserNamesFromServices(uuid, names))
                return true;

            return false;
        }

        private bool TryGetUserNamesFromCache(UUID uuid, string[] names)
        {
            lock (m_UserCache)
            {
                if (m_UserCache.ContainsKey(uuid))
                {
                    names[0] = m_UserCache[uuid].FirstName;
                    names[1] = m_UserCache[uuid].LastName;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Try to get the names bound to the given uuid, from the services.
        /// </summary>
        /// <returns>True if the name was found, false if not.</returns>
        /// <param name='uuid'></param>
        /// <param name='names'>The array of names if found.  If not found, then names[0] = "Unknown" and names[1] = "User"</param>
        private bool TryGetUserNamesFromServices(UUID uuid, string[] names)
        {
            UserAccount account = m_Scenes[0].UserAccountService.GetUserAccount(UUID.Zero, uuid);

            if (account != null)
            {
                names[0] = account.FirstName;
                names[1] = account.LastName;

                UserData user = new UserData();
                user.FirstName = account.FirstName;
                user.LastName = account.LastName;

                lock (m_UserCache)
                    m_UserCache[uuid] = user;

                return true;
            }
            else
            {
                // Let's try the GridUser service
                GridUserInfo uInfo = m_Scenes[0].GridUserService.GetGridUserInfo(uuid.ToString());
                if (uInfo != null)
                {
                    string url, first, last, tmp;
                    UUID u;
                    if (Util.ParseUniversalUserIdentifier(uInfo.UserID, out u, out url, out first, out last, out tmp))
                    {
                        AddUser(uuid, first, last, url);

                        if (m_UserCache.ContainsKey(uuid))
                        {
                            names[0] = m_UserCache[uuid].FirstName;
                            names[1] = m_UserCache[uuid].LastName;

                            return true;
                        }
                    }
                    else
                        m_log.DebugFormat("[USER MANAGEMENT MODULE]: Unable to parse UUI {0}", uInfo.UserID);
                }
                else
                {
                    m_log.DebugFormat("[USER MANAGEMENT MODULE]: No grid user found for {0}", uuid);
                }

                names[0] = "Unknown";
                names[1] = "UserUMMTGUN7";

                return false;
            }
        }

        #region IUserManagement

        public UUID GetUserIdByName(string name)
        {
            string[] parts = name.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                throw new Exception("Name must have 2 components");

            return GetUserIdByName(parts[0], parts[1]);
        }

        public UUID GetUserIdByName(string firstName, string lastName)
        {
            // TODO: Optimize for reverse lookup if this gets used by non-console commands.
            lock (m_UserCache)
            {
                foreach (UserData user in m_UserCache.Values)
                {
                    if (user.FirstName == firstName && user.LastName == lastName)
                        return user.Id;
                }
            }

            UserAccount account = m_Scenes[0].UserAccountService.GetUserAccount(UUID.Zero, firstName, lastName);

            if (account != null)
                return account.PrincipalID;

            return UUID.Zero;
        }

        public string GetUserName(UUID uuid)
        {
            string[] names = new string[2];
            TryGetUserNames(uuid, names);

            return names[0] + " " + names[1];

        }

        public string GetUserHomeURL(UUID userID)
        {
            lock (m_UserCache)
            {
                if (m_UserCache.ContainsKey(userID))
                    return m_UserCache[userID].HomeURL;
            }

            return string.Empty;
        }

        public string GetUserServerURL(UUID userID, string serverType)
        {
            UserData userdata;
            lock (m_UserCache)
                m_UserCache.TryGetValue(userID, out userdata);

            if (userdata != null)
            {
//                m_log.DebugFormat("[USER MANAGEMENT MODULE]: Requested url type {0} for {1}", serverType, userID);

                if (userdata.ServerURLs != null && userdata.ServerURLs.ContainsKey(serverType) && userdata.ServerURLs[serverType] != null)
                {
                    return userdata.ServerURLs[serverType].ToString();
                }

                if (userdata.HomeURL != null && userdata.HomeURL != string.Empty)
                {
                    //m_log.DebugFormat(
                    //    "[USER MANAGEMENT MODULE]: Did not find url type {0} so requesting urls from '{1}' for {2}",
                    //    serverType, userdata.HomeURL, userID);

                    UserAgentServiceConnector uConn = new UserAgentServiceConnector(userdata.HomeURL);
                    userdata.ServerURLs = uConn.GetServerURLs(userID);
                    if (userdata.ServerURLs != null && userdata.ServerURLs.ContainsKey(serverType) && userdata.ServerURLs[serverType] != null)
                        return userdata.ServerURLs[serverType].ToString();
                }
            }

            return string.Empty;
        }

        public string GetUserUUI(UUID userID)
        {
            UserAccount account = m_Scenes[0].UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, userID);
            if (account != null)
                return userID.ToString();

            UserData ud;
            lock (m_UserCache)
                m_UserCache.TryGetValue(userID, out ud);

            if (ud != null)
            {
                string homeURL = ud.HomeURL;
                string first = ud.FirstName, last = ud.LastName;
                if (ud.LastName.StartsWith("@"))
                {
                    string[] parts = ud.FirstName.Split('.');
                    if (parts.Length >= 2)
                    {
                        first = parts[0];
                        last = parts[1];
                    }
                    return userID + ";" + homeURL + ";" + first + " " + last;
                }
            }

            return userID.ToString();
        }

        public void AddUser(UUID uuid, string first, string last)
        {
            lock (m_UserCache)
            {
                if (m_UserCache.ContainsKey(uuid))
                    return;
            }

            UserData user = new UserData();
            user.Id = uuid;
            user.FirstName = first;
            user.LastName = last;

            AddUserInternal(user);
        }

        public void AddUser(UUID uuid, string first, string last, string homeURL)
        {
            //m_log.DebugFormat("[USER MANAGEMENT MODULE]: Adding user with id {0}, first {1}, last {2}, url {3}", uuid, first, last, homeURL);
            if (homeURL == string.Empty)
                return;

            AddUser(uuid, homeURL + ";" + first + " " + last);
        }

        public void AddUser (UUID id, string creatorData)
        {
            //m_log.DebugFormat("[USER MANAGEMENT MODULE]: Adding user with id {0}, creatorData {1}", id, creatorData);

            UserData oldUser;
            lock (m_UserCache)
                m_UserCache.TryGetValue(id, out oldUser);

            if (oldUser != null)
            {
                if (creatorData == null || creatorData == String.Empty)
                {
                    //ignore updates without creator data
                    return;
                }

                //try update unknown users, but don't update anyone else
                if (oldUser.FirstName == "Unknown" && !creatorData.Contains("Unknown")) 
                {
                    lock (m_UserCache)
                        m_UserCache.Remove(id);
                    m_log.DebugFormat("[USER MANAGEMENT MODULE]: Re-adding user with id {0}, creatorData [{1}] and old HomeURL {2}", id, creatorData, oldUser.HomeURL);
                }
                else
                {
                    //we have already a valid user within the cache
                    return;
                }
            }

            UserAccount account = m_Scenes[0].UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, id);

            if (account != null)
            {
                AddUser(id, account.FirstName, account.LastName);
            }
            else
            {
                UserData user = new UserData();
                user.Id = id;

                if (!string.IsNullOrEmpty(creatorData))
                {
                    //creatorData = <endpoint>;<name>

                    string[] parts = creatorData.Split(';');
                    if (parts.Length >= 1)
                    {
                        user.HomeURL = parts[0];
                        try
                        {
                            Uri uri = new Uri(parts[0]);
                            user.LastName = "@" + uri.Authority;
                        }
                        catch (UriFormatException)
                        {
                            m_log.DebugFormat("[SCENE]: Unable to parse Uri {0}", parts[0]);
                            user.LastName = "@unknown";
                        }
                    }
                    if (parts.Length >= 2)
                        user.FirstName = parts[1].Replace(' ', '.');
                }
                else
                {
                    // Temporarily add unknown user entries of this type into the cache so that we can distinguish
                    // this source from other recent (hopefully resolved) bugs that fail to retrieve a user name binding
                    // TODO: Can be removed when GUN* unknown users have definitely dropped significantly or
                    // disappeared.
                    user.FirstName = "Unknown";
                    user.LastName = "UserUMMAU3";
                }

                AddUserInternal(user);
            }
        }

        void AddUserInternal(UserData user)
        {
            lock (m_UserCache)
                m_UserCache[user.Id] = user;

            //m_log.DebugFormat(
            //    "[USER MANAGEMENT MODULE]: Added user {0} {1} {2} {3}",
            //    user.Id, user.FirstName, user.LastName, user.HomeURL);
        }

        public bool IsLocalGridUser(UUID uuid)
        {
            UserAccount account = m_Scenes[0].UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, uuid);
            if (account == null || (account != null && !account.LocalToGrid))
                return false;

            return true;
        }

        #endregion IUserManagement

        protected void Init()
        {
            RegisterConsoleCmds();
        }

        protected void RegisterConsoleCmds()
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
        }

        private void HandleShowUser(string module, string[] cmd)
        {
            if (cmd.Length < 3)
            {
                MainConsole.Instance.OutputFormat("Usage: show name <uuid>");
                return;
            }

            UUID userId;
            if (!ConsoleUtil.TryParseConsoleUuid(MainConsole.Instance, cmd[2], out userId))
                return;

            string[] names;

            UserData ud;

            lock (m_UserCache)
            {
                if (!m_UserCache.TryGetValue(userId, out ud))
                {
                    MainConsole.Instance.OutputFormat("No name known for user with id {0}", userId);
                    return;
                }
            }

            ConsoleDisplayTable cdt = new ConsoleDisplayTable();
            cdt.AddColumn("UUID", 36);
            cdt.AddColumn("Name", 30);
            cdt.AddColumn("HomeURL", 40);
            cdt.AddRow(userId, string.Format("{0} {1}", ud.FirstName, ud.LastName), ud.HomeURL);

            MainConsole.Instance.Output(cdt.ToString());
        }

        private void HandleShowUsers(string module, string[] cmd)
        {
            ConsoleDisplayTable cdt = new ConsoleDisplayTable();
            cdt.AddColumn("UUID", 36);
            cdt.AddColumn("Name", 30);
            cdt.AddColumn("HomeURL", 40);

            lock (m_UserCache)
            {
                foreach (KeyValuePair<UUID, UserData> kvp in m_UserCache)
                    cdt.AddRow(kvp.Key, string.Format("{0} {1}", kvp.Value.FirstName, kvp.Value.LastName), kvp.Value.HomeURL);
            }

            MainConsole.Instance.Output(cdt.ToString());
        }

    }

}