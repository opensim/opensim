/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
using System.Text;
using OpenSim.Framework.Data;
using libsecondlife;
using System.Reflection;

using System.Xml;
using Nwc.XmlRpc;
using OpenSim.Framework.Sims;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Utilities;

using System.Security.Cryptography;

namespace OpenSim.Framework.UserManagement
{
    public abstract class UserManagerBase
    {
        public OpenSim.Framework.Interfaces.UserConfig _config;
        Dictionary<string, IUserData> _plugins = new Dictionary<string, IUserData>();

        /// <summary>
        /// Adds a new user server plugin - user servers will be requested in the order they were loaded.
        /// </summary>
        /// <param name="FileName">The filename to the user server plugin DLL</param>
        public void AddPlugin(string FileName)
        {
            OpenSim.Framework.Console.MainLog.Instance.Verbose( "Userstorage: Attempting to load " + FileName);
            Assembly pluginAssembly = Assembly.LoadFrom(FileName);

            OpenSim.Framework.Console.MainLog.Instance.Verbose( "Userstorage: Found " + pluginAssembly.GetTypes().Length + " interfaces.");
            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (!pluginType.IsAbstract)
                {
                    Type typeInterface = pluginType.GetInterface("IUserData", true);

                    if (typeInterface != null)
                    {
                        IUserData plug = (IUserData)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                        plug.Initialise();
                        this._plugins.Add(plug.getName(), plug);
                        OpenSim.Framework.Console.MainLog.Instance.Verbose( "Userstorage: Added IUserData Interface");
                    }

                    typeInterface = null;
                }
            }

            pluginAssembly = null;
        }

        #region Get UserProfile 
        /// <summary>
        /// Loads a user profile from a database by UUID
        /// </summary>
        /// <param name="uuid">The target UUID</param>
        /// <returns>A user profile</returns>
        public UserProfileData getUserProfile(LLUUID uuid)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    UserProfileData profile = plugin.Value.getUserByUUID(uuid);
                    profile.currentAgent = getUserAgent(profile.UUID);
                    return profile;
                }
                catch (Exception e)
                {
                    OpenSim.Framework.Console.MainLog.Instance.Verbose( "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return null;
        }


        /// <summary>
        /// Loads a user profile by name
        /// </summary>
        /// <param name="name">The target name</param>
        /// <returns>A user profile</returns>
        public UserProfileData getUserProfile(string name)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    UserProfileData profile = plugin.Value.getUserByName(name);
                    profile.currentAgent = getUserAgent(profile.UUID);
                    return profile;
                }
                catch (Exception e)
                {
                    OpenSim.Framework.Console.MainLog.Instance.Verbose( "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
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
        public UserProfileData getUserProfile(string fname, string lname)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    UserProfileData profile = plugin.Value.getUserByName(fname,lname);

                    profile.currentAgent = getUserAgent(profile.UUID);

                    return profile;
                }
                catch (Exception e)
                {
                    OpenSim.Framework.Console.MainLog.Instance.Verbose( "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return null;
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
                    return plugin.Value.getAgentByUUID(uuid);
                }
                catch (Exception e)
                {
                    OpenSim.Framework.Console.MainLog.Instance.Verbose( "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
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
                    return plugin.Value.getAgentByName(name);
                }
                catch (Exception e)
                {
                    OpenSim.Framework.Console.MainLog.Instance.Verbose( "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return null;
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
                    return plugin.Value.getAgentByName(fname,lname);
                }
                catch (Exception e)
                {
                    OpenSim.Framework.Console.MainLog.Instance.Verbose( "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
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
            Hashtable requestData = (Hashtable)request.Params[0];

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
                string startLoc = ((string)requestData["start"]).Trim();
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
            agent.currentRegion = new LLUUID();      // Fill in later

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
        /// Checks a user against it's password hash
        /// </summary>
        /// <param name="profile">The users profile</param>
        /// <param name="password">The supplied password</param>
        /// <returns>Authenticated?</returns>
        public virtual bool AuthenticateUser(UserProfileData profile, string password)
        {
            OpenSim.Framework.Console.MainLog.Instance.Verbose(
                "Authenticating " + profile.username + " " + profile.surname);

            password = password.Remove(0, 3); //remove $1$

            string s = Util.Md5Hash(password + ":" + profile.passwordSalt);

            return profile.passwordHash.Equals(s.ToString(), StringComparison.InvariantCultureIgnoreCase);
        }

        #region Xml Response

        /// <summary>
        /// 
        /// </summary>
        /// <param name="firstname"></param>
        /// <param name="lastname"></param>
        /// <returns></returns>
        public virtual UserProfileData GetTheUser(string firstname, string lastname)
        {
            return getUserProfile(firstname, lastname);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual string GetMessage()
        {
            return _config.DefaultStartupMsg;
        }

        /// <summary>
        /// Customises the login response and fills in missing values.
        /// </summary>
        /// <param name="response">The existing response</param>
        /// <param name="theUser">The user profile</param>
        public abstract void CustomiseResponse( LoginResponse response, UserProfileData theUser);

        /// <summary>
        /// Main user login function
        /// </summary>
        /// <param name="request">The XMLRPC request</param>
        /// <returns>The response to send</returns>
        public XmlRpcResponse XmlRpcLoginMethod(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            bool GoodXML = (requestData.Contains("first") && requestData.Contains("last") && requestData.Contains("passwd"));
            bool GoodLogin = false;
            string firstname = "";
            string lastname = "";
            string passwd = "";

            UserProfileData userProfile;
            LoginResponse logResponse = new LoginResponse();

            if (GoodXML)
            {
                firstname = (string)requestData["first"];
                lastname = (string)requestData["last"];
                passwd = (string)requestData["passwd"];

                userProfile = GetTheUser(firstname, lastname);
                if (userProfile == null)
                    return logResponse.CreateLoginFailedResponse();

                GoodLogin = AuthenticateUser(userProfile, passwd);
            }
            else
            {
                return logResponse.CreateGridErrorResponse();
            }

            if (!GoodLogin)
            {
                return logResponse.CreateLoginFailedResponse();
            }
            else
            {
                // If we already have a session...
                if (userProfile.currentAgent != null && userProfile.currentAgent.agentOnline)
                {
                    // Reject the login
                    return logResponse.CreateAlreadyLoggedInResponse();
                }
                // Otherwise...
                // Create a new agent session
                CreateAgent( userProfile, request);

                try
                {

                    LLUUID AgentID = userProfile.UUID;

                    // Inventory Library Section
                    ArrayList AgentInventoryArray = new ArrayList();
                    Hashtable TempHash;

                    AgentInventory Library = new AgentInventory();
                    Library.CreateRootFolder(AgentID, true);

                    foreach (InventoryFolder InvFolder in Library.InventoryFolders.Values)
                    {
                        TempHash = new Hashtable();
                        TempHash["name"] = InvFolder.FolderName;
                        TempHash["parent_id"] = InvFolder.ParentID.ToStringHyphenated();
                        TempHash["version"] = (Int32)InvFolder.Version;
                        TempHash["type_default"] = (Int32)InvFolder.DefaultType;
                        TempHash["folder_id"] = InvFolder.FolderID.ToStringHyphenated();
                        AgentInventoryArray.Add(TempHash);
                    }

                    Hashtable InventoryRootHash = new Hashtable();
                    InventoryRootHash["folder_id"] = Library.InventoryRoot.FolderID.ToStringHyphenated();
                    ArrayList InventoryRoot = new ArrayList();
                    InventoryRoot.Add(InventoryRootHash);

                    // Circuit Code
                    uint circode = (uint)(Util.RandomClass.Next());

                    logResponse.Lastname = userProfile.surname;
                    logResponse.Firstname = userProfile.username;
                    logResponse.AgentID = AgentID.ToStringHyphenated();
                    logResponse.SessionID = userProfile.currentAgent.sessionID.ToStringHyphenated();
                    logResponse.SecureSessionID = userProfile.currentAgent.secureSessionID.ToStringHyphenated();
                    logResponse.InventoryRoot = InventoryRoot;
                    logResponse.InventorySkeleton = AgentInventoryArray;
                    logResponse.CircuitCode = (Int32)circode;
                    //logResponse.RegionX = 0; //overwritten
                    //logResponse.RegionY = 0; //overwritten
                    logResponse.Home = "!!null temporary value {home}!!";   // Overwritten
                    //logResponse.LookAt = "\n[r" + TheUser.homeLookAt.X.ToString() + ",r" + TheUser.homeLookAt.Y.ToString() + ",r" + TheUser.homeLookAt.Z.ToString() + "]\n";
                    //logResponse.SimAddress = "127.0.0.1"; //overwritten
                    //logResponse.SimPort = 0; //overwritten
                    logResponse.Message = this.GetMessage();
                    
                    try
                    {
                        this.CustomiseResponse(  logResponse,   userProfile);
                    }
                    catch (Exception e)
                    {
                        System.Console.WriteLine(e.ToString());
                        return logResponse.CreateDeadRegionResponse();
                    }
                    CommitAgent(ref userProfile);
                    return logResponse.ToXmlRpcResponse();

                }
                catch (Exception E)
                {
                    System.Console.WriteLine(E.ToString());
                }
                //}
            }
            return response;

        }

        #endregion

        /// <summary>
        /// Deletes an active agent session
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="path">The path (eg /bork/narf/test)</param>
        /// <param name="param">Parameters sent</param>
        /// <returns>Success "OK" else error</returns>
        public string RestDeleteUserSessionMethod(string request, string path, string param)
        {
            // TODO! Important!

            return "OK";
        }

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
            user.homeRegion = Util.UIntsToLong((regX * 256), (regY * 256));

            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    plugin.Value.addNewUserProfile(user);

                }
                catch (Exception e)
                {
                    OpenSim.Framework.Console.MainLog.Instance.Verbose("Unable to add user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }
        }

        /// <summary>
        /// Returns an error message that the user could not be found in the database
        /// </summary>
        /// <returns>XML string consisting of a error element containing individual error(s)</returns>
        public XmlRpcResponse CreateUnknownUserErrorResponse()
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            responseData["error_type"] = "unknown_user";
            responseData["error_desc"] = "The user requested is not in the database";

            response.Value = responseData;
            return response;
        }

        /// <summary>
        /// Converts a user profile to an XML element which can be returned
        /// </summary>
        /// <param name="profile">The user profile</param>
        /// <returns>A string containing an XML Document of the user profile</returns>
        public XmlRpcResponse ProfileToXmlRPCResponse(UserProfileData profile)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();

            // Account information
            responseData["firstname"] = profile.username;
            responseData["lastname"]  = profile.surname;
            responseData["uuid"]  = profile.UUID.ToStringHyphenated();
            // Server Information
            responseData["server_inventory"]  = profile.userInventoryURI;
            responseData["server_asset"]  = profile.userAssetURI;
            // Profile Information
            responseData["profile_about"]  = profile.profileAboutText;
            responseData["profile_firstlife_about"]  = profile.profileFirstText;
            responseData["profile_firstlife_image"]  = profile.profileFirstImage.ToStringHyphenated();
            responseData["profile_can_do"]  = profile.profileCanDoMask.ToString();
            responseData["profile_want_do"]  = profile.profileWantDoMask.ToString();
            responseData["profile_image"]  = profile.profileImage.ToStringHyphenated();
            responseData["profile_created"] = profile.created.ToString();
            responseData["profile_lastlogin"] = profile.lastLogin.ToString();
            // Home region information
            responseData["home_coordinates"]  = profile.homeLocation.ToString();
            responseData["home_region"]  = profile.homeRegion.ToString();
            responseData["home_look"]  = profile.homeLookAt.ToString();

            response.Value = responseData;
            return response;
        }

        #region XMLRPC User Methods 
        //should most likely move out of here and into the grid's userserver sub class
        public XmlRpcResponse XmlRPCGetUserMethodName(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            UserProfileData userProfile;

            if (requestData.Contains("avatar_name"))
            {
                userProfile = getUserProfile((string)requestData["avatar_name"]);
                if (userProfile == null)
                {
                    return CreateUnknownUserErrorResponse();
                }
            }
            else
            {
                return CreateUnknownUserErrorResponse();
            }
            

            return ProfileToXmlRPCResponse(userProfile);
        }

        public XmlRpcResponse XmlRPCGetUserMethodUUID(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            UserProfileData userProfile;
            if (requestData.Contains("avatar_uuid"))
            {
                userProfile = getUserProfile((LLUUID)requestData["avatar_uuid"]);
                if (userProfile == null)
                {
                    return CreateUnknownUserErrorResponse();
                }
            }
            else
            {
                return CreateUnknownUserErrorResponse();
            }


            return ProfileToXmlRPCResponse(userProfile);
        }
        #endregion

    }
}
