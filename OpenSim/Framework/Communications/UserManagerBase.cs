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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;

namespace OpenSim.Framework.UserManagement
{
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
                MainLog.Instance.Verbose("Userstorage: Attempting to load " + FileName);
                Assembly pluginAssembly = Assembly.LoadFrom(FileName);

                MainLog.Instance.Verbose("Userstorage: Found " + pluginAssembly.GetTypes().Length + " interfaces.");
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
            MainLog.Instance.Verbose("Userstorage: Added IUserData Interface");
        }

        #region Get UserProfile 

        /// <summary>
        /// Loads a user profile from a database by UUID
        /// </summary>
        /// <param name="uuid">The target UUID</param>
        /// <returns>A user profile</returns>
        public UserProfileData GetUserProfile(LLUUID uuid)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    UserProfileData profile = plugin.Value.GetUserByUUID(uuid);
                    profile.currentAgent = getUserAgent(profile.UUID);
                    return profile;
                }
                catch (Exception e)
                {
                    MainLog.Instance.Verbose("Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return null;
        }


        /// <summary>
        /// Loads a user profile by name
        /// </summary>
        /// <param name="name">The target name</param>
        /// <returns>A user profile</returns>
        public UserProfileData GetUserProfile(string name)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    UserProfileData profile = plugin.Value.GetUserByName(name);
                    profile.currentAgent = getUserAgent(profile.UUID);
                    return profile;
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("EEK!");
                    MainLog.Instance.Verbose("Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return null;
        }

        /// <summary>
        /// Loads a user profile by name
        /// </summary>
        /// <param name="fname">First name</param>
        /// <param name="lname">Last name</param>
        /// <returns>A user profile</returns>
        public UserProfileData GetUserProfile(string fname, string lname)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    UserProfileData profile = plugin.Value.GetUserByName(fname, lname);

                    profile.currentAgent = getUserAgent(profile.UUID);

                    return profile;
                }
                catch (Exception e)
                {
                    MainLog.Instance.Verbose("Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
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
                    MainLog.Instance.Verbose("Unable to set user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return false;
        }

        #endregion

        #region Get UserAgent

        /// <summary>
        /// Loads a user agent by uuid (not called directly)
        /// </summary>
        /// <param name="uuid">The agents UUID</param>
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
                    MainLog.Instance.Verbose("Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return null;
        }

        /// <summary>
        /// Loads a user agent by name (not called directly)
        /// </summary>
        /// <param name="name">The agents name</param>
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
                    MainLog.Instance.Verbose("Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
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
        /// <param name="fname">The agents firstname</param>
        /// <param name="lname">The agents lastname</param>
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
                    MainLog.Instance.Verbose("Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
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
            Hashtable requestData = (Hashtable) request.Params[0];

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
            if (requestData.ContainsKey("start"))
            {
                string startLoc = ((string) requestData["start"]).Trim();
                if (!(startLoc == "last" || startLoc == "home"))
                {
                    // Format: uri:Ahern&162&213&34
                    try
                    {
                        string[] parts = startLoc.Remove(0, 4).Split('&');
                        string region = parts[0];

                        ////////////////////////////////////////////////////
                        //SimProfile SimInfo = new SimProfile();
                        //SimInfo = SimInfo.LoadFromGrid(theUser.currentAgent.currentHandle, _config.GridServerURL, _config.GridSendKey, _config.GridRecvKey);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            // What time did the user login?
            agent.loginTime = Util.UnixTimeSinceEpoch();
            agent.logoutTime = 0;

            // Current location
            agent.regionID = new LLUUID(); // Fill in later
            agent.currentRegion = new LLUUID(); // Fill in later

            profile.currentAgent = agent;
        }

        /// <summary>
        /// Saves a target agent to the database
        /// </summary>
        /// <param name="profile">The users profile</param>
        /// <returns>Successful?</returns>
        public bool CommitAgent(ref UserProfileData profile)
        {
            // Saves the agent to database
            return true;
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        public void AddUserProfile(string firstName, string lastName, string pass, uint regX, uint regY)
        {
            UserProfileData user = new UserProfileData();
            user.homeLocation = new LLVector3(128, 128, 100);
            user.UUID = LLUUID.Random();
            user.username = firstName;
            user.surname = lastName;
            user.passwordHash = pass;
            user.passwordSalt = "";
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
                    MainLog.Instance.Verbose("Unable to add user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }
        }

        public abstract UserProfileData SetupMasterUser(string firstName, string lastName);
        public abstract UserProfileData SetupMasterUser(string firstName, string lastName, string password);
    }
}