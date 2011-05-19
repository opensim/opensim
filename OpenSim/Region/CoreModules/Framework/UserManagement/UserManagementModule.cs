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

using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Region.CoreModules.Framework.UserManagement
{
    struct UserData
    {
        public UUID Id;
        public string FirstName;
        public string LastName;
        public string ProfileURL;
    }

    public class UserManagementModule : ISharedRegionModule, IUserManagement
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_Scenes = new List<Scene>();

        // The cache
        Dictionary<UUID, UserData> m_UserCache = new Dictionary<UUID, UserData>();

        #region ISharedRegionModule

        public void Initialise(IConfigSource config)
        {
            //m_Enabled = config.Configs["Modules"].GetBoolean("LibraryModule", m_Enabled);
            //if (m_Enabled)
            //{
            //    IConfig libConfig = config.Configs["LibraryService"];
            //    if (libConfig != null)
            //    {
            //        string dllName = libConfig.GetString("LocalServiceModule", string.Empty);
            //        m_log.Debug("[LIBRARY MODULE]: Library service dll is " + dllName);
            //        if (dllName != string.Empty)
            //        {
            //            Object[] args = new Object[] { config };
            //            m_Library = ServerUtils.LoadPlugin<ILibraryService>(dllName, args);
            //        }
            //    }
            //}
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public string Name
        {
            get { return "UserManagement Module"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void AddRegion(Scene scene)
        {
            m_Scenes.Add(scene);

            scene.RegisterModuleInterface<IUserManagement>(this);
            scene.EventManager.OnNewClient += new EventManager.OnNewClientDelegate(EventManager_OnNewClient);
        }

        public void RemoveRegion(Scene scene)
        {
            scene.UnregisterModuleInterface<IUserManagement>(this);
            m_Scenes.Remove(scene);
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void PostInitialise()
        {
            foreach (Scene s in m_Scenes)
            {
                // let's sniff all the user names referenced by objects in the scene
                m_log.DebugFormat("[USER MANAGEMENT MODULE]: Caching creators' data from {0} ({1} objects)...", s.RegionInfo.RegionName, s.GetEntities().Length);
                s.ForEachSOG(delegate(SceneObjectGroup sog) { CacheCreators(sog); });
            }
        }

        public void Close()
        {
            m_Scenes.Clear();
            m_UserCache.Clear();
        }

        #endregion ISharedRegionModule

 
        #region Event Handlers

        void EventManager_OnNewClient(IClientAPI client)
        {
            client.OnNameFromUUIDRequest += new UUIDNameRequest(HandleUUIDNameRequest);
        }

        void HandleUUIDNameRequest(UUID uuid, IClientAPI remote_client)
        {
            m_log.DebugFormat("[XXX] HandleUUIDNameRequest {0}", uuid);
            if (m_Scenes[0].LibraryService != null && (m_Scenes[0].LibraryService.LibraryRootFolder.Owner == uuid))
            {
                remote_client.SendNameReply(uuid, "Mr", "OpenSim");
            }
            else
            {
                string[] names = GetUserNames(uuid);
                if (names.Length == 2)
                {
                    remote_client.SendNameReply(uuid, names[0], names[1]);
                }

            }
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

            if (m_UserCache.ContainsKey(uuid))
            {
                returnstring[0] = m_UserCache[uuid].FirstName;
                returnstring[1] = m_UserCache[uuid].LastName;
                return returnstring;
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

        public string GetUserName(UUID uuid)
        {
            m_log.DebugFormat("[XXX] GetUserName {0}", uuid);
            string[] names = GetUserNames(uuid);
            if (names.Length == 2)
            {
                string firstname = names[0];
                string lastname = names[1];

                return firstname + " " + lastname;

            }
            return "(hippos)";
        }

        public void AddUser(UUID id, string creatorData)
        {
            if (m_UserCache.ContainsKey(id))
                return;

            UserData user = new UserData();
            user.Id = id;

            UserAccount account = m_Scenes[0].UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, id);

            if (account != null)
            {
                user.FirstName = account.FirstName;
                user.LastName = account.LastName;
                // user.ProfileURL = we should initialize this to the default
            }
            else
            {
                if (creatorData != null && creatorData != string.Empty)
                {
                    //creatorData = <endpoint>;<name>

                    string[] parts = creatorData.Split(';');
                    if (parts.Length >= 1)
                    {
                        user.ProfileURL = parts[0];
                        try
                        {
                            Uri uri = new Uri(parts[0]);
                            user.LastName = "@" + uri.Authority;
                        }
                        catch
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
                    user.FirstName = "Unknown";
                    user.LastName = "User";
                }
            }

            lock (m_UserCache)
                m_UserCache[id] = user;

            m_log.DebugFormat("[USER MANAGEMENT MODULE]: Added user {0} {1} {2} {3}", user.Id, user.FirstName, user.LastName, user.ProfileURL);
        }

        public void AddUser(UUID uuid, string first, string last, string profileURL)
        {
            AddUser(uuid, profileURL + ";" + first + " " + last);
        }

        //public void AddUser(UUID uuid, string userData)
        //{
        //    if (m_UserCache.ContainsKey(uuid))
        //        return;

        //    UserData user = new UserData();
        //    user.Id = uuid;

        //    // userData = <profile url>;<name>
        //    string[] parts = userData.Split(';');
        //    if (parts.Length >= 1)
        //        user.ProfileURL = parts[0].Trim();
        //    if (parts.Length >= 2)
        //    {
        //        string[] name = parts[1].Trim().Split(' ');
        //        if (name.Length >= 1)
        //            user.FirstName = name[0];
        //        if (name.Length >= 2)
        //            user.LastName = name[1];
        //        else
        //            user.LastName = "?";
        //    }

        //    lock (m_UserCache)
        //        m_UserCache.Add(uuid, user);

        //    m_log.DebugFormat("[USER MANAGEMENT MODULE]: Added user {0} {1} {2} {3}", user.Id, user.FirstName, user.LastName, user.ProfileURL);

        //}

        #endregion IUserManagement
    }
}
