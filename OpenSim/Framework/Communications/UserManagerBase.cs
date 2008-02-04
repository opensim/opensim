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
* 
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using libsecondlife;
using libsecondlife.StructuredData;
using Nwc.XmlRpc;
using OpenSim.Framework.Console;
using OpenSim.Framework.Statistics;

namespace OpenSim.Framework.UserManagement
{
    /// <summary>
    /// Base class for user management (create, read, etc)
    /// </summary>
    public abstract class UserManagerBase : IUserService
    {
        public UserConfig _config;
        private Dictionary<string, IUserData> _plugins = new Dictionary<string, IUserData>();
        
        /// <summary>
        /// Adds a new user server plugin - user servers will be requested in the order they were loaded.
        /// </summary>
        /// <param name="FileName">The filename to the user server plugin DLL</param>
        public void AddPlugin(string FileName)
        {
            if (!String.IsNullOrEmpty(FileName))
            {
                MainLog.Instance.Verbose("USERSTORAGE", "Attempting to load " + FileName);
                Assembly pluginAssembly = Assembly.LoadFrom(FileName);

                MainLog.Instance.Verbose("USERSTORAGE", "Found " + pluginAssembly.GetTypes().Length + " interfaces.");
                foreach (Type pluginType in pluginAssembly.GetTypes())
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("IUserData", true);

                        if (typeInterface != null)
                        {
                            IUserData plug =
                                (IUserData) Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            AddPlugin(plug);
                        }
                    }
                }
            }
        }

        public void AddPlugin(IUserData plug)
        {
            plug.Initialise();
            _plugins.Add(plug.getName(), plug);
            MainLog.Instance.Verbose("USERSTORAGE", "Added IUserData Interface");
        }

        #region Get UserProfile 

        /// <summary>
        /// Loads a user profile from a database by UUID
        /// </summary>
        /// <param name="uuid">The target UUID</param>
        /// <returns>A user profile.  Returns null if no user profile is found.</returns>
        public UserProfileData GetUserProfile(LLUUID uuid)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                UserProfileData profile = plugin.Value.GetUserByUUID(uuid);

                if (null != profile)
                {
                    profile.currentAgent = getUserAgent(profile.UUID);
                    return profile;
                }
            }

            return null;
        }

        public List<AvatarPickerAvatar> GenerateAgentPickerRequestResponse(LLUUID queryID, string query)
        {
            List<AvatarPickerAvatar> pickerlist = new List<AvatarPickerAvatar>();
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    pickerlist = plugin.Value.GeneratePickerResults(queryID, query);
                }
                catch (Exception)
                {
                    MainLog.Instance.Verbose("USERSTORAGE",
                                             "Unable to generate AgentPickerData via  " + plugin.Key + "(" + query + ")");
                    return new List<AvatarPickerAvatar>();
                }
            }
            return pickerlist;
        }

        /// <summary>
        /// Loads a user profile by name
        /// </summary>
        /// <param name="fname">First name</param>
        /// <param name="lname">Last name</param>
        /// <returns>A user profile.  Returns null if no profile is found</returns>
        public UserProfileData GetUserProfile(string fname, string lname)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                UserProfileData profile = plugin.Value.GetUserByName(fname, lname);

                if (profile != null)
                {
                    profile.currentAgent = getUserAgent(profile.UUID);
                    return profile;
                }
            }

            return null;
        }

        /// <summary>
        /// Set's user profile from object
        /// </summary>
        /// <param name="fname">First name</param>
        /// <param name="lname">Last name</param>
        /// <returns>A user profile</returns>
        public bool setUserProfile(UserProfileData data)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    plugin.Value.UpdateUserProfile(data);
                    return true;
                }
                catch (Exception e)
                {
                    MainLog.Instance.Verbose("USERSTORAGE",
                                             "Unable to set user via " + plugin.Key + "(" + e.ToString() + ")");
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
        public UserAgentData getUserAgent(LLUUID uuid)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    return plugin.Value.GetAgentByUUID(uuid);
                }
                catch (Exception e)
                {
                    MainLog.Instance.Verbose("USERSTORAGE",
                                             "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return null;
        }

        /// <summary>
        /// Loads a user's friend list
        /// </summary>
        /// <param name="name">the UUID of the friend list owner</param>
        /// <returns>A List of FriendListItems that contains info about the user's friends</returns>
        public List<FriendListItem> GetUserFriendList(LLUUID ownerID)
        {

            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    return plugin.Value.GetUserFriendList(ownerID);
                }
                catch (Exception e)
                {
                    MainLog.Instance.Verbose("USERSTORAGE",
                                             "Unable to GetUserFriendList via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return null;

        }

        public void StoreWebLoginKey(LLUUID agentID, LLUUID webLoginKey)
        {

            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    plugin.Value.StoreWebLoginKey(agentID, webLoginKey);
                }
                catch (Exception e)
                {
                    MainLog.Instance.Verbose("USERSTORAGE",
                                             "Unable to Store WebLoginKey via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }
        }

        public void AddNewUserFriend(LLUUID friendlistowner, LLUUID friend, uint perms)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    plugin.Value.AddNewUserFriend(friendlistowner,friend,perms);
                }
                catch (Exception e)
                {
                    MainLog.Instance.Verbose("USERSTORAGE",
                                             "Unable to AddNewUserFriend via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

        }


        public void RemoveUserFriend(LLUUID friendlistowner, LLUUID friend)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                   plugin.Value.RemoveUserFriend(friendlistowner, friend);
                }
                catch (Exception e)
                {
                    MainLog.Instance.Verbose("USERSTORAGE",
                                             "Unable to RemoveUserFriend via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }
        }

        public void UpdateUserFriendPerms(LLUUID friendlistowner, LLUUID friend, uint perms)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    plugin.Value.UpdateUserFriendPerms(friendlistowner, friend, perms);
                }
                catch (Exception e)
                {
                    MainLog.Instance.Verbose("USERSTORAGE",
                                             "Unable to UpdateUserFriendPerms via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }
        }
        /// <summary>
        /// Loads a user agent by name (not called directly)
        /// </summary>
        /// <param name="name">The agent's name</param>
        /// <returns>A user agent</returns>
        public UserAgentData getUserAgent(string name)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    return plugin.Value.GetAgentByName(name);
                }
                catch (Exception e)
                {
                    MainLog.Instance.Verbose("USERSTORAGE",
                                             "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return null;
        }

        // TODO: document
        public void clearUserAgent(LLUUID agentID)
        {
            UserProfileData profile = GetUserProfile(agentID);
            profile.currentAgent = null;

            setUserProfile(profile);
        }

        /// <summary>
        /// Loads a user agent by name (not called directly)
        /// </summary>
        /// <param name="fname">The agent's firstname</param>
        /// <param name="lname">The agent's lastname</param>
        /// <returns>A user agent</returns>
        public UserAgentData getUserAgent(string fname, string lname)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    return plugin.Value.GetAgentByName(fname, lname);
                }
                catch (Exception e)
                {
                    MainLog.Instance.Verbose("USERSTORAGE",
                                             "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return null;
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
            //Hashtable requestData = (Hashtable) request.Params[0];

            UserAgentData agent = new UserAgentData();

            // User connection
            agent.agentOnline = true;

            // Generate sessions
            RNGCryptoServiceProvider rand = new RNGCryptoServiceProvider();
            byte[] randDataS = new byte[16];
            byte[] randDataSS = new byte[16];
            rand.GetBytes(randDataS);
            rand.GetBytes(randDataSS);

            agent.secureSessionID = new LLUUID(randDataSS, 0);
            agent.sessionID = new LLUUID(randDataS, 0);

            // Profile UUID
            agent.UUID = profile.UUID;

            // Current position (from Home)
            agent.currentHandle = profile.homeRegion;
            agent.currentPos = profile.homeLocation;

            // If user specified additional start, use that
//            if (requestData.ContainsKey("start"))
//            {
//                string startLoc = ((string) requestData["start"]).Trim();
//                if (!(startLoc == "last" || startLoc == "home"))
//                {
//                    // Format: uri:Ahern&162&213&34
//                    try
//                    {
//                        string[] parts = startLoc.Remove(0, 4).Split('&');
//                        //string region = parts[0];
//
//                        ////////////////////////////////////////////////////
//                        //SimProfile SimInfo = new SimProfile();
//                        //SimInfo = SimInfo.LoadFromGrid(theUser.currentAgent.currentHandle, _config.GridServerURL, _config.GridSendKey, _config.GridRecvKey);
//                    }
//                    catch (Exception)
//                    {
//                    }
//                }
//            }

            // What time did the user login?
            agent.loginTime = Util.UnixTimeSinceEpoch();
            agent.logoutTime = 0;

            // Current location
            agent.regionID = LLUUID.Zero; // Fill in later
            agent.currentRegion = LLUUID.Zero; // Fill in later

            profile.currentAgent = agent;
        }
        
        /// <summary>
        /// Process a user logoff from OpenSim.
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="regionid"></param>
        /// <param name="regionhandle"></param>
        /// <param name="posx"></param>
        /// <param name="posy"></param>
        /// <param name="posz"></param>
        public void LogOffUser(LLUUID userid, LLUUID regionid, ulong regionhandle, float posx, float posy, float posz)
        {
            if (StatsManager.UserStats != null)
                StatsManager.UserStats.AddLogout();
            
            UserProfileData userProfile;
            UserAgentData userAgent;
            LLVector3 currentPos = new LLVector3(posx, posy, posz);

            userProfile = GetUserProfile(userid);

            if (userProfile != null)
            {
                
                userAgent = userProfile.currentAgent;
                if (userAgent != null)
                {
                    userAgent.agentOnline = false;
                    userAgent.logoutTime = Util.UnixTimeSinceEpoch();
                    userAgent.sessionID = LLUUID.Zero;
                    userAgent.currentRegion = regionid;
                    userAgent.currentHandle = regionhandle;

                    userAgent.currentPos = currentPos;

                    userProfile.currentAgent = userAgent;


                    CommitAgent(ref userProfile);
                }
                else
                {
                    MainLog.Instance.Verbose("LOGOUT", "didn't save logout position, currentAgent is null *do Fix ");
                }
                MainLog.Instance.Verbose("LOGOUT", userProfile.username + " " + userProfile.surname + " from " + regionhandle + "(" + posx + "," + posy + "," + posz + ")" );
                MainLog.Instance.Verbose("LOGOUT", "userid: " + userid.ToString() + "   regionid: " + regionid.ToString() );
            }
            else
            {
                MainLog.Instance.Warn("LOGOUT", "Unknown User logged out");
            }
        }
        
        public void CreateAgent(UserProfileData profile, LLSD request)
        {
            UserAgentData agent = new UserAgentData();

            // User connection
            agent.agentOnline = true;

            // Generate sessions
            RNGCryptoServiceProvider rand = new RNGCryptoServiceProvider();
            byte[] randDataS = new byte[16];
            byte[] randDataSS = new byte[16];
            rand.GetBytes(randDataS);
            rand.GetBytes(randDataSS);

            agent.secureSessionID = new LLUUID(randDataSS, 0);
            agent.sessionID = new LLUUID(randDataS, 0);

            // Profile UUID
            agent.UUID = profile.UUID;

            // Current position (from Home)
            agent.currentHandle = profile.homeRegion;
            agent.currentPos = profile.homeLocation;

            // What time did the user login?
            agent.loginTime = Util.UnixTimeSinceEpoch();
            agent.logoutTime = 0;

            // Current location
            agent.regionID = LLUUID.Zero; // Fill in later
            agent.currentRegion = LLUUID.Zero; // Fill in later

            profile.currentAgent = agent;
        }

        /// <summary>
        /// Saves a target agent to the database
        /// </summary>
        /// <param name="profile">The users profile</param>
        /// <returns>Successful?</returns>
        public bool CommitAgent(ref UserProfileData profile)
        {
            // TODO: how is this function different from setUserProfile?
            return setUserProfile(profile);
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        public LLUUID AddUserProfile(string firstName, string lastName, string pass, uint regX, uint regY)
        {
            UserProfileData user = new UserProfileData();
            user.homeLocation = new LLVector3(128, 128, 100);
            user.UUID = LLUUID.Random();
            user.username = firstName;
            user.surname = lastName;
            user.passwordHash = pass;
            user.passwordSalt = String.Empty;
            user.created = Util.UnixTimeSinceEpoch();
            user.homeLookAt = new LLVector3(100, 100, 100);
            user.homeRegionX = regX;
            user.homeRegionY = regY;

            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    plugin.Value.AddNewUserProfile(user);
                }
                catch (Exception e)
                {
                    MainLog.Instance.Verbose("USERSTORAGE",
                                             "Unable to add user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return user.UUID;
        }

        public abstract UserProfileData SetupMasterUser(string firstName, string lastName);
        public abstract UserProfileData SetupMasterUser(string firstName, string lastName, string password);
        public abstract UserProfileData SetupMasterUser(LLUUID uuid);
    }
}
