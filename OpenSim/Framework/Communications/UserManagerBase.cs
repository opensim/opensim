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
using System.Security.Cryptography;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Statistics;

namespace OpenSim.Framework.Communications
{
    /// <summary>
    /// Base class for user management (create, read, etc)
    /// </summary>
    public abstract class UserManagerBase : IUserService, IUserAdminService, IAvatarService, IMessagingService
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <value>
        /// List of plugins to search for user data
        /// </value>
        private List<IUserDataPlugin> _plugins = new List<IUserDataPlugin>();
        
        private IInterServiceInventoryServices m_interServiceInventoryService;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="interServiceInventoryService"></param>
        public UserManagerBase(IInterServiceInventoryServices interServiceInventoryService)
        {
            m_interServiceInventoryService = interServiceInventoryService;            
        }        
                
        /// <summary>
        /// Add a new user data plugin - plugins will be requested in the order they were added.
        /// </summary>
        /// <param name="plugin">The plugin that will provide user data</param>     
        public void AddPlugin(IUserDataPlugin plugin)
        {
            _plugins.Add(plugin);
        }

        /// <summary>
        /// Add a new user data plugin - plugins will be requested in the order they were added.
        /// </summary>
        /// <param name="provider">The filename to the user data plugin DLL</param>
        /// <param name="connect"></param>
        public void AddPlugin(string provider, string connect)
        {
            PluginLoader<IUserDataPlugin> loader =
                new PluginLoader<IUserDataPlugin>(new UserDataInitialiser(connect));

            // loader will try to load all providers (MySQL, MSSQL, etc)
            // unless it is constrainted to the correct "Provider" entry in the addin.xml
            loader.Add("/OpenSim/UserData", new PluginProviderFilter(provider));            
            loader.Load();

            _plugins.AddRange(loader.Plugins);
        }

        #region Get UserProfile      

        // see IUserService
        public UserProfileData GetUserProfile(string fname, string lname)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                UserProfileData profile = plugin.GetUserByName(fname, lname);

                if (profile != null)
                {
                    profile.CurrentAgent = GetUserAgent(profile.ID);
                    return profile;
                }
            }

            return null;
        }
        
        public void LogoutUsers(UUID regionID)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                plugin.LogoutUsers(regionID);
            }
        }
        
        public void ResetAttachments(UUID userID)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                plugin.ResetAttachments(userID);
            }
        }
        
        public UserAgentData GetAgentByUUID(UUID userId)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                UserAgentData agent = plugin.GetAgentByUUID(userId);

                if (agent != null)
                {
                    return agent;
                }
            }

            return null;
        }
        
        // see IUserService
        public virtual UserProfileData GetUserProfile(UUID uuid)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                UserProfileData profile = plugin.GetUserByUUID(uuid);

                if (null != profile)
                {
                    profile.CurrentAgent = GetUserAgent(profile.ID);
                    return profile;
                }
            }

            return null;
        }

        public List<AvatarPickerAvatar> GenerateAgentPickerRequestResponse(UUID queryID, string query)
        {
            List<AvatarPickerAvatar> pickerlist = new List<AvatarPickerAvatar>();
            foreach (IUserDataPlugin plugin in _plugins)
            {
                try
                {
                    pickerlist = plugin.GeneratePickerResults(queryID, query);
                }
                catch (Exception)
                {
                    m_log.Info("[USERSTORAGE]: Unable to generate AgentPickerData via  " + plugin.Name + "(" + query + ")");
                    return new List<AvatarPickerAvatar>();
                }
            }
            
            return pickerlist;
        }

        /// <summary>
        /// Updates a user profile from data object
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool UpdateUserProfile(UserProfileData data)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                try
                {
                    plugin.UpdateUserProfile(data);
                    return true;
                }
                catch (Exception e)
                {
                    m_log.InfoFormat("[USERSTORAGE]: Unable to set user {0} {1} via {2}: {3}", data.FirstName, data.SurName,
                                     plugin.Name, e.ToString());
                }
            }
            return false;
        }

        #endregion

        #region Get UserAgent

        /// <summary>
        /// Loads a user agent by uuid (not called directly)
        /// </summary>
        /// <param name="uuid">The agent's UUID</param>
        /// <returns>Agent profiles</returns>
        public UserAgentData GetUserAgent(UUID uuid)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                try
                {
                    UserAgentData result = plugin.GetAgentByUUID(uuid);
                  
                    if (result != null) 
                    {
                        return result;
                    }
                }
                catch (Exception e)
                {
                    m_log.Info("[USERSTORAGE]: Unable to find user via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }

            return null;
        }

        /// <summary>
        /// Loads a user agent by name (not called directly)
        /// </summary>
        /// <param name="name">The agent's name</param>
        /// <returns>A user agent</returns>
        public UserAgentData GetUserAgent(string name)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                try
                {
                    return plugin.GetAgentByName(name);
                }
                catch (Exception e)
                {
                    m_log.Info("[USERSTORAGE]: Unable to find user via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }

            return null;
        }

        /// <summary>
        /// Loads a user agent by name (not called directly)
        /// </summary>
        /// <param name="fname">The agent's firstname</param>
        /// <param name="lname">The agent's lastname</param>
        /// <returns>A user agent</returns>
        public UserAgentData GetUserAgent(string fname, string lname)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                try
                {
                    return plugin.GetAgentByName(fname, lname);
                }
                catch (Exception e)
                {
                    m_log.Info("[USERSTORAGE]: Unable to find user via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }

            return null;
        }

        /// <summary>
        /// Loads a user's friend list
        /// </summary>
        /// <param name="name">the UUID of the friend list owner</param>
        /// <returns>A List of FriendListItems that contains info about the user's friends</returns>
        public List<FriendListItem> GetUserFriendList(UUID ownerID)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                try
                {
                    List<FriendListItem> result = plugin.GetUserFriendList(ownerID);
                  
                    if (result != null) 
                    {
                        return result;
                    }
                }
                catch (Exception e)
                {
                    m_log.Info("[USERSTORAGE]: Unable to GetUserFriendList via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }

            return null;
        }

        public Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos (List<UUID> uuids)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                try
                {
                    Dictionary<UUID, FriendRegionInfo> result = plugin.GetFriendRegionInfos(uuids);
                  
                    if (result != null) 
                    {
                        return result;
                    }
                }
                catch (Exception e)
                {
                    m_log.Info("[USERSTORAGE]: Unable to GetFriendRegionInfos via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
            return null;
        }

        public void StoreWebLoginKey(UUID agentID, UUID webLoginKey)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                try
                {
                    plugin.StoreWebLoginKey(agentID, webLoginKey);
                }
                catch (Exception e)
                {
                    m_log.Info("[USERSTORAGE]: Unable to Store WebLoginKey via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
        }

        public void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                try
                {
                    plugin.AddNewUserFriend(friendlistowner,friend,perms);
                }
                catch (Exception e)
                {
                    m_log.Info("[USERSTORAGE]: Unable to AddNewUserFriend via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
        }

        public void RemoveUserFriend(UUID friendlistowner, UUID friend)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                try
                {
                    plugin.RemoveUserFriend(friendlistowner, friend);
                }
                catch (Exception e)
                {
                    m_log.Info("[USERSTORAGE]: Unable to RemoveUserFriend via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
        }

        public void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                try
                {
                    plugin.UpdateUserFriendPerms(friendlistowner, friend, perms);
                }
                catch (Exception e)
                {
                    m_log.Info("[USERSTORAGE]: Unable to UpdateUserFriendPerms via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
        }

        /// <summary>
        /// Resets the currentAgent in the user profile
        /// </summary>
        /// <param name="agentID">The agent's ID</param>
        public void ClearUserAgent(UUID agentID)
        {
            UserProfileData profile = GetUserProfile(agentID);

            if (profile == null)
            {
                return;
            }
            
            profile.CurrentAgent = null;

            UpdateUserProfile(profile);
        }


        #endregion

        #region CreateAgent

        /// <summary>
        /// Creates and initialises a new user agent - make sure to use CommitAgent when done to submit to the DB
        /// </summary>
        /// <param name="profile">The users profile</param>
        /// <param name="request">The users loginrequest</param>
        public void CreateAgent(UserProfileData profile, XmlRpcRequest request)
        {
            UserAgentData agent = new UserAgentData();

            // User connection
            agent.AgentOnline = true;

            if (request.Params.Count > 1)
            {
                if (request.Params[1] != null)
                {
                    IPEndPoint RemoteIPEndPoint = (IPEndPoint)request.Params[1];
                    agent.AgentIP = RemoteIPEndPoint.Address.ToString();
                    agent.AgentPort = (uint)RemoteIPEndPoint.Port;
                }
            }

            // Generate sessions
            RNGCryptoServiceProvider rand = new RNGCryptoServiceProvider();
            byte[] randDataS = new byte[16];
            byte[] randDataSS = new byte[16];
            rand.GetBytes(randDataS);
            rand.GetBytes(randDataSS);

            agent.SecureSessionID = new UUID(randDataSS, 0);
            agent.SessionID = new UUID(randDataS, 0);

            // Profile UUID
            agent.ProfileID = profile.ID;

            // Current location/position/alignment
            if (profile.CurrentAgent != null)
            {
                agent.Region = profile.CurrentAgent.Region;
                agent.Handle = profile.CurrentAgent.Handle;
                agent.Position = profile.CurrentAgent.Position;
                agent.LookAt = profile.CurrentAgent.LookAt;
            }
            else
            {
                agent.Region = profile.HomeRegionID;
                agent.Handle = profile.HomeRegion;
                agent.Position = profile.HomeLocation;
                agent.LookAt = profile.HomeLookAt;
            }

            // What time did the user login?
            agent.LoginTime = Util.UnixTimeSinceEpoch();
            agent.LogoutTime = 0;

            profile.CurrentAgent = agent;
        }

        public void CreateAgent(UserProfileData profile, OSD request)
        {
            UserAgentData agent = new UserAgentData();

            // User connection
            agent.AgentOnline = true;

            //if (request.Params.Count > 1)
            //{
            //    IPEndPoint RemoteIPEndPoint = (IPEndPoint)request.Params[1];
            //    agent.AgentIP = RemoteIPEndPoint.Address.ToString();
            //    agent.AgentPort = (uint)RemoteIPEndPoint.Port;
            //}

            // Generate sessions
            RNGCryptoServiceProvider rand = new RNGCryptoServiceProvider();
            byte[] randDataS = new byte[16];
            byte[] randDataSS = new byte[16];
            rand.GetBytes(randDataS);
            rand.GetBytes(randDataSS);

            agent.SecureSessionID = new UUID(randDataSS, 0);
            agent.SessionID = new UUID(randDataS, 0);

            // Profile UUID
            agent.ProfileID = profile.ID;

            // Current location/position/alignment
            if (profile.CurrentAgent != null)
            {
                agent.Region = profile.CurrentAgent.Region;
                agent.Handle = profile.CurrentAgent.Handle;
                agent.Position = profile.CurrentAgent.Position;
                agent.LookAt = profile.CurrentAgent.LookAt;
            }
            else
            {
                agent.Region = profile.HomeRegionID;
                agent.Handle = profile.HomeRegion;
                agent.Position = profile.HomeLocation;
                agent.LookAt = profile.HomeLookAt;
            }

            // What time did the user login?
            agent.LoginTime = Util.UnixTimeSinceEpoch();
            agent.LogoutTime = 0;

            profile.CurrentAgent = agent;
        }

        /// <summary>
        /// Saves a target agent to the database
        /// </summary>
        /// <param name="profile">The users profile</param>
        /// <returns>Successful?</returns>
        public bool CommitAgent(ref UserProfileData profile)
        {
            // TODO: how is this function different from setUserProfile?  -> Add AddUserAgent() here and commit both tables "users" and "agents"
            // TODO: what is the logic should be?
            bool ret = false;
            ret = AddUserAgent(profile.CurrentAgent);
            ret = ret & UpdateUserProfile(profile);
            return ret;
        }

        /// <summary>
        /// Process a user logoff from OpenSim.
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="regionid"></param>
        /// <param name="regionhandle"></param>
        /// <param name="position"></param>
        /// <param name="lookat"></param>
        public void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, Vector3 position, Vector3 lookat)
        {
            if (StatsManager.UserStats != null)
                StatsManager.UserStats.AddLogout();

            UserProfileData userProfile = GetUserProfile(userid);

            if (userProfile != null)
            {
                // This line needs to be in side the above if statement or the UserServer will crash on some logouts.
                m_log.Info("[LOGOUT]: " + userProfile.FirstName + " " + userProfile.SurName + " from " + regionhandle + "(" + position.X + "," + position.Y + "," + position.Z + ")");

                UserAgentData userAgent = userProfile.CurrentAgent;
                if (userAgent != null)
                {
                    userAgent.AgentOnline = false;
                    userAgent.LogoutTime = Util.UnixTimeSinceEpoch();
                    //userAgent.sessionID = UUID.Zero;
                    if (regionid != UUID.Zero)
                    {
                        userAgent.Region = regionid;
                    }
                    userAgent.Handle = regionhandle;
                    userAgent.Position = position;
                    userAgent.LookAt = lookat;
                    //userProfile.CurrentAgent = userAgent;
                    userProfile.LastLogin = userAgent.LogoutTime;

                    CommitAgent(ref userProfile);
                }
                else
                {
                    // If currentagent is null, we can't reference it here or the UserServer crashes!
                    m_log.Info("[LOGOUT]: didn't save logout position: " + userid.ToString());
                }
            }
            else
            {
                m_log.Warn("[LOGOUT]: Unknown User logged out");
            }
        }

        /// <summary>
        /// Process a user logoff from OpenSim (deprecated as of 2008-08-27)
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="regionid"></param>
        /// <param name="regionhandle"></param>
        /// <param name="posx"></param>
        /// <param name="posy"></param>
        /// <param name="posz"></param>
        public void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, float posx, float posy, float posz)
        {
            LogOffUser(userid, regionid, regionhandle, new Vector3(posx, posy, posz), new Vector3());
        }

        #endregion

        /// <summary>
        /// Add a new user
        /// </summary>
        /// <param name="firstName">first name</param>
        /// <param name="lastName">last name</param>
        /// <param name="password">password</param>
        /// <param name="email">email</param>
        /// <param name="regX">location X</param>
        /// <param name="regY">location Y</param>
        /// <returns>The UUID of the created user profile.  On failure, returns UUID.Zero</returns>
        public UUID AddUser(string firstName, string lastName, string password, string email, uint regX, uint regY)
        {
            return AddUser(firstName, lastName, password, email, regX, regY, UUID.Random());
        }

        /// <summary>
        /// Add a new user
        /// </summary>
        /// <param name="firstName">first name</param>
        /// <param name="lastName">last name</param>
        /// <param name="password">password</param>
        /// <param name="email">email</param>
        /// <param name="regX">location X</param>
        /// <param name="regY">location Y</param>
        /// <param name="SetUUID">UUID of avatar.</param>
        /// <returns>The UUID of the created user profile.  On failure, returns UUID.Zero</returns>
        public UUID AddUser(
            string firstName, string lastName, string password, string email, uint regX, uint regY, UUID SetUUID)
        {
            string md5PasswdHash = Util.Md5Hash(Util.Md5Hash(password) + ":" + String.Empty);
            
            UserProfileData user = new UserProfileData();
            user.HomeLocation = new Vector3(128, 128, 100);
            user.ID = SetUUID;
            user.FirstName = firstName;
            user.SurName = lastName;
            user.PasswordHash = md5PasswdHash;
            user.PasswordSalt = String.Empty;
            user.Created = Util.UnixTimeSinceEpoch();
            user.HomeLookAt = new Vector3(100, 100, 100);
            user.HomeRegionX = regX;
            user.HomeRegionY = regY;
            user.Email = email;

            foreach (IUserDataPlugin plugin in _plugins)
            {
                try
                {
                    plugin.AddNewUserProfile(user);
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to add user via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }

            UserProfileData userProf = GetUserProfile(firstName, lastName);
            if (userProf == null)
            {
                return UUID.Zero;
            }
            else
            {
                m_interServiceInventoryService.CreateNewUserInventory(userProf.ID);
                
                return userProf.ID;
            }            
        }

        /// <summary>
        /// Reset a user password
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="newPassword"></param>
        /// <returns>true if the update was successful, false otherwise</returns>
        public bool ResetUserPassword(string firstName, string lastName, string newPassword)
        {
            string md5PasswdHash = Util.Md5Hash(Util.Md5Hash(newPassword) + ":" + String.Empty);
            
            UserProfileData profile = GetUserProfile(firstName, lastName);
            
            if (null == profile)
            {
                m_log.ErrorFormat("[USERSTORAGE]: Could not find user {0} {1}", firstName, lastName);
                return false;
            }
            
            profile.PasswordHash = md5PasswdHash;
            profile.PasswordSalt = String.Empty;
            
            UpdateUserProfile(profile);
            
            return true;
        }        

        public abstract UserProfileData SetupMasterUser(string firstName, string lastName);
        public abstract UserProfileData SetupMasterUser(string firstName, string lastName, string password);
        public abstract UserProfileData SetupMasterUser(UUID uuid);

        /// <summary>
        /// Add agent to DB
        /// </summary>
        /// <param name="agentdata">The agent data to be added</param>
        public bool AddUserAgent(UserAgentData agentdata)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                try
                {
                    plugin.AddNewUserAgent(agentdata);
                    return true;
                }
                catch (Exception e)
                {
                    m_log.Info("[USERSTORAGE]: Unable to add agent via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
            return false;
        }

        /// <summary>
        /// Get avatar appearance information
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public AvatarAppearance GetUserAppearance(UUID user)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                try
                {
                    return plugin.GetUserAppearance(user);
                }
                catch (Exception e)
                {
                    m_log.InfoFormat("[USERSTORAGE]: Unable to find user appearance {0} via {1} ({2})", user.ToString(), plugin.Name, e.ToString());
                }
            }
            return null;
        }

        /// <summary>
        /// Update avatar appearance information
        /// </summary>
        /// <param name="user"></param>
        /// <param name="appearance"></param>
        public void UpdateUserAppearance(UUID user, AvatarAppearance appearance)
        {
            foreach (IUserDataPlugin plugin in _plugins)
            {
                try
                {
                    plugin.UpdateUserAppearance(user, appearance);
                }
                catch (Exception e)
                {
                    m_log.InfoFormat("[USERSTORAGE]: Unable to update user appearance {0} via {1} ({2})", user.ToString(), plugin.Name, e.ToString());
                }
            }
        }
    }
}
