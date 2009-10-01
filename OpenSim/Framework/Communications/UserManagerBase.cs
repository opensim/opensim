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
using System.Security.Cryptography;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Data;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Statistics;
using OpenSim.Services.Interfaces;

namespace OpenSim.Framework.Communications
{
    /// <summary>
    /// Base class for user management (create, read, etc)
    /// </summary>
    public abstract class UserManagerBase 
        : IUserService, IUserAdminService, IAvatarService, IMessagingService, IAuthentication
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <value>
        /// List of plugins to search for user data
        /// </value>
        private List<IUserDataPlugin> m_plugins = new List<IUserDataPlugin>();

        protected CommunicationsManager m_commsManager;
        protected IInventoryService m_InventoryService;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="commsManager"></param>
        public UserManagerBase(CommunicationsManager commsManager)
        {
            m_commsManager = commsManager;
        }

        public virtual void SetInventoryService(IInventoryService invService)
        {
            m_InventoryService = invService;
        }

        /// <summary>
        /// Add a new user data plugin - plugins will be requested in the order they were added.
        /// </summary>
        /// <param name="plugin">The plugin that will provide user data</param>
        public void AddPlugin(IUserDataPlugin plugin)
        {
            m_plugins.Add(plugin);
        }

        /// <summary>
        /// Adds a list of user data plugins, as described by `provider' and
        /// `connect', to `_plugins'.
        /// </summary>
        /// <param name="provider">
        /// The filename of the inventory server plugin DLL.
        /// </param>
        /// <param name="connect">
        /// The connection string for the storage backend.
        /// </param>
        public void AddPlugin(string provider, string connect)
        {
            m_plugins.AddRange(DataPluginFactory.LoadDataPlugins<IUserDataPlugin>(provider, connect));
        }

        #region UserProfile
        
        public virtual void AddTemporaryUserProfile(UserProfileData userProfile)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                plugin.AddTemporaryUserProfile(userProfile);
            }
        }
        
        public virtual UserProfileData GetUserProfile(string fname, string lname)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
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
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                plugin.LogoutUsers(regionID);
            }
        }

        public void ResetAttachments(UUID userID)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                plugin.ResetAttachments(userID);
            }
        }

        public UserProfileData GetUserProfile(Uri uri)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                UserProfileData profile = plugin.GetUserByUri(uri);

                if (null != profile)
                    return profile;
            }

            return null;
        }

        public virtual UserAgentData GetAgentByUUID(UUID userId)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                UserAgentData agent = plugin.GetAgentByUUID(userId);

                if (agent != null)
                {
                    return agent;
                }
            }

            return null;
        }

        public Uri GetUserUri(UserProfileData userProfile)
        {
            throw new NotImplementedException();
        }

        // see IUserService
        public virtual UserProfileData GetUserProfile(UUID uuid)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
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

        public virtual List<AvatarPickerAvatar> GenerateAgentPickerRequestResponse(UUID queryID, string query)
        {
            List<AvatarPickerAvatar> allPickerList = new List<AvatarPickerAvatar>();
            
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    List<AvatarPickerAvatar> pickerList = plugin.GeneratePickerResults(queryID, query);
                    if (pickerList != null)
                        allPickerList.AddRange(pickerList);
                }
                catch (Exception)
                {
                    m_log.Error(
                        "[USERSTORAGE]: Unable to generate AgentPickerData via  " + plugin.Name + "(" + query + ")");
                }
            }

            return allPickerList;
        }
        
        public virtual bool UpdateUserProfile(UserProfileData data)
        {
            bool result = false;
            
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.UpdateUserProfile(data);
                    result = true;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[USERSTORAGE]: Unable to set user {0} {1} via {2}: {3}", 
                        data.FirstName, data.SurName, plugin.Name, e.ToString());
                }
            }
            
            return result;
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
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    UserAgentData result = plugin.GetAgentByUUID(uuid);

                    if (result != null)
                        return result;
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to find user via " + plugin.Name + "(" + e.ToString() + ")");
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
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    UserAgentData result = plugin.GetAgentByName(name);
                    
                    if (result != null)
                        return result;
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to find user via " + plugin.Name + "(" + e.ToString() + ")");
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
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    UserAgentData result = plugin.GetAgentByName(fname, lname);
                    
                    if (result != null)
                        return result;
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to find user via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }

            return null;
        }

        public virtual List<FriendListItem> GetUserFriendList(UUID ownerID)
        {
            List<FriendListItem> allFriends = new List<FriendListItem>();
            
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    List<FriendListItem> friends = plugin.GetUserFriendList(ownerID);

                    if (friends != null)
                        allFriends.AddRange(friends);
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to GetUserFriendList via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }

            return allFriends;
        }

        public virtual Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos (List<UUID> uuids)
        {
            //Dictionary<UUID, FriendRegionInfo> allFriendRegions = new Dictionary<UUID, FriendRegionInfo>();
            
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    Dictionary<UUID, FriendRegionInfo> friendRegions = plugin.GetFriendRegionInfos(uuids);

                    if (friendRegions != null)
                        return friendRegions;
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to GetFriendRegionInfos via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
            
            return new Dictionary<UUID, FriendRegionInfo>();
        }

        public void StoreWebLoginKey(UUID agentID, UUID webLoginKey)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.StoreWebLoginKey(agentID, webLoginKey);
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to Store WebLoginKey via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
        }

        public virtual void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.AddNewUserFriend(friendlistowner, friend, perms);
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to AddNewUserFriend via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
        }

        public virtual void RemoveUserFriend(UUID friendlistowner, UUID friend)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.RemoveUserFriend(friendlistowner, friend);
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to RemoveUserFriend via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
        }

        public virtual void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.UpdateUserFriendPerms(friendlistowner, friend, perms);
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to UpdateUserFriendPerms via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
        }

        /// <summary>
        /// Resets the currentAgent in the user profile
        /// </summary>
        /// <param name="agentID">The agent's ID</param>
        public virtual void ClearUserAgent(UUID agentID)
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
            //m_log.DebugFormat("[USER MANAGER]: Creating agent {0} {1}", profile.Name, profile.ID);
            
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
            //m_log.DebugFormat("[USER MANAGER]: Creating agent {0} {1}", profile.Name, profile.ID);
            
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
            //m_log.DebugFormat("[USER MANAGER]: Committing agent {0} {1}", profile.Name, profile.ID);
            
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
        public virtual void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, Vector3 position, Vector3 lookat)
        {
            if (StatsManager.UserStats != null)
                StatsManager.UserStats.AddLogout();

            UserProfileData userProfile = GetUserProfile(userid);

            if (userProfile != null)
            {
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
        public virtual UUID AddUser(string firstName, string lastName, string password, string email, uint regX, uint regY)
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
        public virtual UUID AddUser(
            string firstName, string lastName, string password, string email, uint regX, uint regY, UUID SetUUID)
        {

            UserProfileData user = new UserProfileData();

            user.PasswordSalt = Util.Md5Hash(UUID.Random().ToString());
            string md5PasswdHash = Util.Md5Hash(Util.Md5Hash(password) + ":" + user.PasswordSalt);

            user.HomeLocation = new Vector3(128, 128, 100);
            user.ID = SetUUID;
            user.FirstName = firstName;
            user.SurName = lastName;
            user.PasswordHash = md5PasswdHash;
            user.Created = Util.UnixTimeSinceEpoch();
            user.HomeLookAt = new Vector3(100, 100, 100);
            user.HomeRegionX = regX;
            user.HomeRegionY = regY;
            user.Email = email;

            foreach (IUserDataPlugin plugin in m_plugins)
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
                //
                // WARNING: This is a horrible hack
                // The purpose here is to avoid touching the user server at this point.
                // There are dragons there that I can't deal with right now.
                // diva 06/09/09
                //
                if (m_InventoryService != null)
                {
                    // local service (standalone)
                    m_log.Debug("[USERSTORAGE]: using IInventoryService to create user's inventory");
                    m_InventoryService.CreateUserInventory(userProf.ID);
                    InventoryFolderBase rootfolder = m_InventoryService.GetRootFolder(userProf.ID);
                    if (rootfolder != null)
                        userProf.RootInventoryFolderID = rootfolder.ID;
                }
                else if (m_commsManager.InterServiceInventoryService != null)
                {
                    // used by the user server
                    m_log.Debug("[USERSTORAGE]: using m_commsManager.InterServiceInventoryService to create user's inventory");
                    m_commsManager.InterServiceInventoryService.CreateNewUserInventory(userProf.ID);
                }

                return userProf.ID;
            }
        }

        /// <summary>
        /// Reset a user password.
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="newPassword"></param>
        /// <returns>true if the update was successful, false otherwise</returns>
        public virtual bool ResetUserPassword(string firstName, string lastName, string newPassword)
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
        /// Add an agent using data plugins.
        /// </summary>
        /// <param name="agentdata">The agent data to be added</param>
        /// <returns>
        /// true if at least one plugin added the user agent.  false if no plugin successfully added the agent
        /// </returns>
        public virtual bool AddUserAgent(UserAgentData agentdata)
        {
            bool result = false;
            
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.AddNewUserAgent(agentdata);
                    result = true;
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to add agent via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
            
            return result;
        }

        /// <summary>
        /// Get avatar appearance information
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public virtual AvatarAppearance GetUserAppearance(UUID user)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    AvatarAppearance appearance = plugin.GetUserAppearance(user);
                    
                    if (appearance != null)
                        return appearance;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to find user appearance {0} via {1} ({2})", user.ToString(), plugin.Name, e.ToString());
                }
            }
            
            return null;
        }

        public virtual void UpdateUserAppearance(UUID user, AvatarAppearance appearance)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.UpdateUserAppearance(user, appearance);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to update user appearance {0} via {1} ({2})", user.ToString(), plugin.Name, e.ToString());
                }
            }
        }

        #region IAuthentication

        protected Dictionary<UUID, List<string>> m_userKeys = new Dictionary<UUID, List<string>>();

        /// <summary>
        /// This generates authorization keys in the form
        /// http://userserver/uuid
        /// after verifying that the caller is, indeed, authorized to request a key
        /// </summary>
        /// <param name="url">URL of the user server</param>
        /// <param name="userID">The user ID requesting the new key</param>
        /// <param name="authToken">The original authorization token for that user, obtained during login</param>
        /// <returns></returns>
        public string GetNewKey(string url, UUID userID, UUID authToken)
        {
            UserProfileData profile = GetUserProfile(userID);
            string newKey = string.Empty;
            if (!url.EndsWith("/"))
                url = url + "/";

            if (profile != null)
            {
                // I'm overloading webloginkey for this, so that no changes are needed in the DB
                // The uses of webloginkey are fairly mutually exclusive
                if (profile.WebLoginKey.Equals(authToken))
                {
                    newKey = UUID.Random().ToString();
                    List<string> keys;
                    lock (m_userKeys)
                    {
                        if (m_userKeys.ContainsKey(userID))
                        {
                            keys = m_userKeys[userID];
                        }
                        else
                        {
                            keys = new List<string>();
                            m_userKeys.Add(userID, keys);
                        }
                        keys.Add(newKey);
                    }
                    m_log.InfoFormat("[USERAUTH]: Successfully generated new auth key for user {0}", userID);
                }
                else
                    m_log.Warn("[USERAUTH]: Unauthorized key generation request. Denying new key.");
            }
            else
                m_log.Warn("[USERAUTH]: User not found.");

            return url + newKey;
        }

        /// <summary>
        /// This verifies the uuid portion of the key given out by GenerateKey
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool VerifyKey(UUID userID, string key)
        {
            lock (m_userKeys)
            {
                if (m_userKeys.ContainsKey(userID))
                {
                    List<string> keys = m_userKeys[userID];
                    if (keys.Contains(key))
                    {
                        // Keys are one-time only, so remove it
                        keys.Remove(key);
                        return true;
                    }
                    return false;
                }
                else
                    return false;
            }
        }
        
        public virtual bool VerifySession(UUID userID, UUID sessionID)
        {
            UserProfileData userProfile = GetUserProfile(userID);

            if (userProfile != null && userProfile.CurrentAgent != null)
            {
                m_log.DebugFormat(
                    "[USER AUTH]: Verifying session {0} for {1}; current  session {2}", 
                    sessionID, userID, userProfile.CurrentAgent.SessionID);
                
                if (userProfile.CurrentAgent.SessionID == sessionID)
                {
                    return true;
                }
            }
            
            return false;
        }

        public virtual bool AuthenticateUserByPassword(UUID userID, string password)
        {
//            m_log.DebugFormat("[USER AUTH]: Authenticating user {0} given password {1}", userID, password);
            
            UserProfileData userProfile = GetUserProfile(userID);

            if (null == userProfile)
                return false;
      
            string md5PasswordHash = Util.Md5Hash(Util.Md5Hash(password) + ":" + userProfile.PasswordSalt);

//            m_log.DebugFormat(
//                "[USER AUTH]: Submitted hash {0}, stored hash {1}", md5PasswordHash, userProfile.PasswordHash);
    
            if (md5PasswordHash == userProfile.PasswordHash)
                return true;
            else
                return false;
        }

        #endregion
    }
}
