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

using OpenSim.Framework;
using OpenSim.Framework.Console;
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

namespace OpenSim.Region.CoreModules.Framework.UserManagement
{
    public class UserData
    {
        public UUID Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string HomeURL { get; set; }
        public Dictionary<string, object> ServerURLs { get; set; }
    }

    public class UserManagementModule : ISharedRegionModule, IUserManagement
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected bool m_Enabled;
        protected List<Scene> m_Scenes = new List<Scene>();

        // The cache
        protected Dictionary<UUID, UserData> m_UserCache = new Dictionary<UUID, UserData>();

        #region ISharedRegionModule

        public void Initialise(IConfigSource config)
        {
            string umanmod = config.Configs["Modules"].GetString("UserManagementModule", Name);
            if (umanmod == Name)
            {
                m_Enabled = true;
                RegisterConsoleCmds();
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

        void HandleUUIDNameRequest(UUID uuid, IClientAPI remote_client)
        {
            if (m_Scenes[0].LibraryService != null && (m_Scenes[0].LibraryService.LibraryRootFolder.Owner == uuid))
            {
                remote_client.SendNameReply(uuid, "Mr", "OpenSim");
            }
            else
            {
                string[] names = GetUserNames(uuid);
                if (names.Length == 2)
                {
                    //m_log.DebugFormat("[XXX] HandleUUIDNameRequest {0} is {1} {2}", uuid, names[0], names[1]);
                    remote_client.SendNameReply(uuid, names[0], names[1]);
                }

            }
        }

        public void HandleAvatarPickerRequest(IClientAPI client, UUID avatarID, UUID RequestID, string query)
        {
            //EventManager.TriggerAvatarPickerRequest();

            m_log.DebugFormat("[USER MANAGEMENT MODULE]: HandleAvatarPickerRequest for {0}", query);

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

            AddAdditionalUsers(avatarID, query, users);

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

        protected virtual void AddAdditionalUsers(UUID avatarID, string query, List<UserData> users)
        {
        }

        #endregion Event Handlers

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

        private string[] GetUserNames(UUID uuid)
        {
            string[] returnstring = new string[2];

            lock (m_UserCache)
            {
                if (m_UserCache.ContainsKey(uuid))
                {
                    returnstring[0] = m_UserCache[uuid].FirstName;
                    returnstring[1] = m_UserCache[uuid].LastName;
                    return returnstring;
                }
            }

            UserAccount account = m_Scenes[0].UserAccountService.GetUserAccount(UUID.Zero, uuid);

            if (account != null)
            {
                returnstring[0] = account.FirstName;
                returnstring[1] = account.LastName;

                UserData user = new UserData();
                user.FirstName = account.FirstName;
                user.LastName = account.LastName;

                lock (m_UserCache)
                    m_UserCache[uuid] = user;
            }
            else
            {
                returnstring[0] = "Unknown";
                returnstring[1] = "User";
            }

            return returnstring;
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
            string[] names = GetUserNames(uuid);
            if (names.Length == 2)
            {
                string firstname = names[0];
                string lastname = names[1];

                return firstname + " " + lastname;

            }
            return "(hippos)";
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
            AddUser(uuid, homeURL + ";" + first + " " + last);
        }

        public void AddUser (UUID id, string creatorData)
        {
            //m_log.DebugFormat("[USER MANAGEMENT MODULE]: Adding user with id {0}, creatorData {1}", id, creatorData);

            UserData oldUser;
            //lock the whole block - prevent concurrent update
            lock (m_UserCache)
            {
                m_UserCache.TryGetValue (id, out oldUser);
                if (oldUser != null)
                {
                    if (creatorData == null || creatorData == String.Empty)
                    {
                        //ignore updates without creator data
                        return;
                    }
                    //try update unknown users
                    //and creator's home URL's
                    if ((oldUser.FirstName == "Unknown" && !creatorData.Contains ("Unknown")) || (oldUser.HomeURL != null && !creatorData.StartsWith (oldUser.HomeURL)))
                    {
                        m_UserCache.Remove (id);
//                      m_log.DebugFormat("[USER MANAGEMENT MODULE]: Re-adding user with id {0}, creatorData [{1}] and old HomeURL {2}", id, creatorData,oldUser.HomeURL);
                    }
                    else
                    {
                        //we have already a valid user within the cache
                        return;
                    }
                }

                UserAccount account = m_Scenes[0].UserAccountService.GetUserAccount (m_Scenes [0].RegionInfo.ScopeID, id);

                if (account != null)
                {
                    AddUser (id, account.FirstName, account.LastName);
                }
                else
                {
                    UserData user = new UserData ();
                    user.Id = id;

                    if (creatorData != null && creatorData != string.Empty)
                    {
                        //creatorData = <endpoint>;<name>

                        string[] parts = creatorData.Split (';');
                        if (parts.Length >= 1)
                        {
                            user.HomeURL = parts [0];
                            try
                            {
                                Uri uri = new Uri (parts [0]);
                                user.LastName = "@" + uri.Authority;
                            }
                            catch (UriFormatException)
                            {
                                m_log.DebugFormat ("[SCENE]: Unable to parse Uri {0}", parts [0]);
                                user.LastName = "@unknown";
                            }
                        }
                        if (parts.Length >= 2)
                            user.FirstName = parts [1].Replace (' ', '.');
                    }
                    else
                    {
                        user.FirstName = "Unknown";
                        user.LastName = "User";
                    }

                    AddUserInternal (user);
                }
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

        protected void RegisterConsoleCmds()
        {
            MainConsole.Instance.Commands.AddCommand("Users", true,
                "show names",
                "show names",
                "Show the bindings between user UUIDs and user names",
                String.Empty,
                HandleShowUsers);
        }

        private void HandleShowUsers(string module, string[] cmd)
        {
            lock (m_UserCache)
            {
                if (m_UserCache.Count == 0)
                {
                    MainConsole.Instance.Output("No users found");
                    return;
                }
    
                MainConsole.Instance.Output("UUID                                 User Name");
                MainConsole.Instance.Output("-----------------------------------------------------------------------------");
                foreach (KeyValuePair<UUID, UserData> kvp in m_UserCache)
                {
                    MainConsole.Instance.Output(String.Format("{0} {1} {2} ({3})",
                           kvp.Key, kvp.Value.FirstName, kvp.Value.LastName, kvp.Value.HomeURL));
                }
    
                return;
            }
        }
    }
}