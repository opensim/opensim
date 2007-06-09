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
using OpenGrid.Framework.Data;
using libsecondlife;
using System.Reflection;

using System.Xml;
using Nwc.XmlRpc;
using OpenSim.Framework.Sims;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Utilities;

using System.Security.Cryptography;

namespace OpenGridServices.UserServer
{
    public class UserManager
    {
        public OpenSim.Framework.Interfaces.UserConfig _config;
        Dictionary<string, IUserData> _plugins = new Dictionary<string, IUserData>();

        /// <summary>
        /// Adds a new user server plugin - user servers will be requested in the order they were loaded.
        /// </summary>
        /// <param name="FileName">The filename to the user server plugin DLL</param>
        public void AddPlugin(string FileName)
        {
            OpenSim.Framework.Console.MainConsole.Instance.Verbose( "Userstorage: Attempting to load " + FileName);
            Assembly pluginAssembly = Assembly.LoadFrom(FileName);

            OpenSim.Framework.Console.MainConsole.Instance.Verbose( "Userstorage: Found " + pluginAssembly.GetTypes().Length + " interfaces.");
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
                        OpenSim.Framework.Console.MainConsole.Instance.Verbose( "Userstorage: Added IUserData Interface");
                    }

                    typeInterface = null;
                }
            }

            pluginAssembly = null;
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
                    OpenSim.Framework.Console.MainConsole.Instance.Verbose( "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }
        }

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
                    OpenSim.Framework.Console.MainConsole.Instance.Verbose( "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
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
                    OpenSim.Framework.Console.MainConsole.Instance.Verbose( "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
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
                    try
                    {
                        profile.currentAgent = getUserAgent(profile.UUID);
                    }
                    catch (Exception e)
                    {
                        // Ignore
                    }
                    return profile;
                }
                catch (Exception e)
                {
                    OpenSim.Framework.Console.MainConsole.Instance.Verbose( "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return null;
        }

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
                    OpenSim.Framework.Console.MainConsole.Instance.Verbose( "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
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
                    OpenSim.Framework.Console.MainConsole.Instance.Verbose( "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
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
                    OpenSim.Framework.Console.MainConsole.Instance.Verbose( "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a error response caused by invalid XML
        /// </summary>
        /// <returns>An XMLRPC response</returns>
        private static XmlRpcResponse CreateErrorConnectingToGridResponse()
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable ErrorRespData = new Hashtable();
            ErrorRespData["reason"] = "key";
            ErrorRespData["message"] = "Error connecting to grid. Could not percieve credentials from login XML.";
            ErrorRespData["login"] = "false";
            response.Value = ErrorRespData;
            return response;
        }

        /// <summary>
        /// Creates an error response caused by bad login credentials
        /// </summary>
        /// <returns>An XMLRPC response</returns>
        private static XmlRpcResponse CreateLoginErrorResponse()
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable ErrorRespData = new Hashtable();
            ErrorRespData["reason"] = "key";
            ErrorRespData["message"] = "Could not authenticate your avatar. Please check your username and password, and check the grid if problems persist.";
            ErrorRespData["login"] = "false";
            response.Value = ErrorRespData;
            return response;
        }

        /// <summary>
        /// Creates an error response caused by being logged in already
        /// </summary>
        /// <returns>An XMLRPC Response</returns>
        private static XmlRpcResponse CreateAlreadyLoggedInResponse()
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable PresenceErrorRespData = new Hashtable();
            PresenceErrorRespData["reason"] = "presence";
            PresenceErrorRespData["message"] = "You appear to be already logged in, if this is not the case please wait for your session to timeout, if this takes longer than a few minutes please contact the grid owner";
            PresenceErrorRespData["login"] = "false";
            response.Value = PresenceErrorRespData;
            return response;
        }

        /// <summary>
        /// Creates an error response caused by target region being down
        /// </summary>
        /// <returns>An XMLRPC Response</returns>
        private static XmlRpcResponse CreateDeadRegionResponse()
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable PresenceErrorRespData = new Hashtable();
            PresenceErrorRespData["reason"] = "key";
            PresenceErrorRespData["message"] = "The region you are attempting to log into is not responding. Please select another region and try again.";
            PresenceErrorRespData["login"] = "false";
            response.Value = PresenceErrorRespData;
            return response;
        }

        /// <summary>
        /// Customises the login response and fills in missing values.
        /// </summary>
        /// <param name="response">The existing response</param>
        /// <param name="theUser">The user profile</param>
        public virtual void CustomiseResponse(ref Hashtable response, ref UserProfileData theUser)
        {
            // Load information from the gridserver
            SimProfile SimInfo = new SimProfile();
            SimInfo = SimInfo.LoadFromGrid(theUser.currentAgent.currentHandle, _config.GridServerURL, _config.GridSendKey, _config.GridRecvKey);

            // Customise the response
            // Home Location
            response["home"] = "{'region_handle':[r" + (SimInfo.RegionLocX * 256).ToString() + ",r" + (SimInfo.RegionLocY * 256).ToString() + "], " + 
                "'position':[r" + theUser.homeLocation.X.ToString() + ",r" + theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "], " +
                "'look_at':[r" + theUser.homeLocation.X.ToString() + ",r" + theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "]}";

            // Destination
            response["sim_ip"] = SimInfo.sim_ip;
            response["sim_port"] = (Int32)SimInfo.sim_port;
            response["region_y"] = (Int32)SimInfo.RegionLocY * 256;
            response["region_x"] = (Int32)SimInfo.RegionLocX * 256;

            // Notify the target of an incoming user
            Console.WriteLine("Notifying " + SimInfo.regionname + " (" + SimInfo.caps_url + ")");

            // Prepare notification
            Hashtable SimParams = new Hashtable();
            SimParams["session_id"] = theUser.currentAgent.sessionID.ToString();
            SimParams["secure_session_id"] = theUser.currentAgent.secureSessionID.ToString();
            SimParams["firstname"] = theUser.username;
            SimParams["lastname"] = theUser.surname;
            SimParams["agent_id"] = theUser.UUID.ToString();
            SimParams["circuit_code"] = (Int32)Convert.ToUInt32(response["circuit_code"]);
            SimParams["startpos_x"] = theUser.currentAgent.currentPos.X.ToString();
            SimParams["startpos_y"] = theUser.currentAgent.currentPos.Y.ToString();
            SimParams["startpos_z"] = theUser.currentAgent.currentPos.Z.ToString();
            ArrayList SendParams = new ArrayList();
            SendParams.Add(SimParams);

            // Update agent with target sim
            theUser.currentAgent.currentRegion = SimInfo.UUID;
            theUser.currentAgent.currentHandle = SimInfo.regionhandle;

            // Send
            XmlRpcRequest GridReq = new XmlRpcRequest("expect_user", SendParams);
            XmlRpcResponse GridResp = GridReq.Send(SimInfo.caps_url, 3000);
        }

        /// <summary>
        /// Checks a user against it's password hash
        /// </summary>
        /// <param name="profile">The users profile</param>
        /// <param name="password">The supplied password</param>
        /// <returns>Authenticated?</returns>
        public bool AuthenticateUser(ref UserProfileData profile, string password)
        {
            OpenSim.Framework.Console.MainConsole.Instance.Verbose(
                "Authenticating " + profile.username + " " + profile.surname);

            password = password.Remove(0, 3); //remove $1$

            string s = Util.Md5Hash(password + ":" + profile.passwordSalt);

            return profile.passwordHash.Equals(s.ToString(), StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Creates and initialises a new user agent - make sure to use CommitAgent when done to submit to the DB
        /// </summary>
        /// <param name="profile">The users profile</param>
        /// <param name="request">The users loginrequest</param>
        public void CreateAgent(ref UserProfileData profile, XmlRpcRequest request)
        {
            Hashtable requestData = (Hashtable)request.Params[0];

            UserAgentData agent = new UserAgentData();

            // User connection
            agent.agentIP = "";
            agent.agentOnline = true;
            agent.agentPort = 0;

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
                    catch (Exception e)
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

            UserProfileData TheUser;

            if (GoodXML)
            {
                firstname = (string)requestData["first"];
                lastname = (string)requestData["last"];
                passwd = (string)requestData["passwd"];

                TheUser = getUserProfile(firstname, lastname);
                if (TheUser == null)
                    return CreateLoginErrorResponse();

                GoodLogin = AuthenticateUser(ref TheUser, passwd);
            }
            else
            {
                return CreateErrorConnectingToGridResponse();
            }

            if (!GoodLogin)
            {
                return CreateLoginErrorResponse();
            }
            else
            {
                // If we already have a session...
                if (TheUser.currentAgent != null && TheUser.currentAgent.agentOnline)
                {
                    // Reject the login
                    return CreateAlreadyLoggedInResponse();
                }
                // Otherwise...
                // Create a new agent session
                CreateAgent(ref TheUser, request);

                try
                {
                    Hashtable responseData = new Hashtable();

                    LLUUID AgentID = TheUser.UUID;
                    
                    // Global Texture Section
                    Hashtable GlobalT = new Hashtable();
                    GlobalT["sun_texture_id"] = "cce0f112-878f-4586-a2e2-a8f104bba271";
                    GlobalT["cloud_texture_id"] = "fc4b9f0b-d008-45c6-96a4-01dd947ac621";
                    GlobalT["moon_texture_id"] = "fc4b9f0b-d008-45c6-96a4-01dd947ac621";
                    ArrayList GlobalTextures = new ArrayList();
                    GlobalTextures.Add(GlobalT);

                    // Login Flags Section
                    Hashtable LoginFlagsHash = new Hashtable();
                    LoginFlagsHash["daylight_savings"] = "N";
                    LoginFlagsHash["stipend_since_login"] = "N";
                    LoginFlagsHash["gendered"] = "Y"; // Needs to be combined with below...
                    LoginFlagsHash["ever_logged_in"] = "Y"; // Should allow male/female av selection
                    ArrayList LoginFlags = new ArrayList();
                    LoginFlags.Add(LoginFlagsHash);

                    // UI Customisation Section
                    Hashtable uiconfig = new Hashtable();
                    uiconfig["allow_first_life"] = "Y";
                    ArrayList ui_config = new ArrayList();
                    ui_config.Add(uiconfig);

                    // Classified Categories Section
                    Hashtable ClassifiedCategoriesHash = new Hashtable();
                    ClassifiedCategoriesHash["category_name"] = "Generic";
                    ClassifiedCategoriesHash["category_id"] = (Int32)1;
                    ArrayList ClassifiedCategories = new ArrayList();
                    ClassifiedCategories.Add(ClassifiedCategoriesHash);

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

                    Hashtable InitialOutfitHash = new Hashtable();
                    InitialOutfitHash["folder_name"] = "Nightclub Female";
                    InitialOutfitHash["gender"] = "female";
                    ArrayList InitialOutfit = new ArrayList();
                    InitialOutfit.Add(InitialOutfitHash);

                    // Circuit Code
                    uint circode = (uint)(Util.RandomClass.Next());

                    // Generics
                    responseData["last_name"] = TheUser.surname;
                    responseData["ui-config"] = ui_config;
                    responseData["sim_ip"] = "127.0.0.1"; //SimInfo.sim_ip.ToString();
                    responseData["login-flags"] = LoginFlags;
                    responseData["global-textures"] = GlobalTextures;
                    responseData["classified_categories"] = ClassifiedCategories;
                    responseData["event_categories"] = new ArrayList();
                    responseData["inventory-skeleton"] = AgentInventoryArray;
                    responseData["inventory-skel-lib"] = new ArrayList();
                    responseData["inventory-root"] = InventoryRoot;
                    responseData["event_notifications"] = new ArrayList();
                    responseData["gestures"] = new ArrayList();
                    responseData["inventory-lib-owner"] = new ArrayList();
                    responseData["initial-outfit"] = InitialOutfit;
                    responseData["seconds_since_epoch"] = (Int32)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                    responseData["start_location"] = "last";
                    responseData["home"] = "!!null temporary value {home}!!";   // Overwritten
                    responseData["message"] = _config.DefaultStartupMsg;
                    responseData["first_name"] = TheUser.username;
                    responseData["circuit_code"] = (Int32)circode;
                    responseData["sim_port"] = 0; //(Int32)SimInfo.sim_port;
                    responseData["secure_session_id"] = TheUser.currentAgent.secureSessionID.ToStringHyphenated();
                    responseData["look_at"] = "\n[r" + TheUser.homeLookAt.X.ToString() + ",r" + TheUser.homeLookAt.Y.ToString() + ",r" + TheUser.homeLookAt.Z.ToString() + "]\n";
                    responseData["agent_id"] = AgentID.ToStringHyphenated();
                    responseData["region_y"] = (Int32)0;    // Overwritten
                    responseData["region_x"] = (Int32)0;    // Overwritten
                    responseData["seed_capability"] = "";
                    responseData["agent_access"] = "M";
                    responseData["session_id"] = TheUser.currentAgent.sessionID.ToStringHyphenated();
                    responseData["login"] = "true";

                    try
                    {
                        this.CustomiseResponse(ref responseData, ref TheUser);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                        return CreateDeadRegionResponse();
                    }

                    CommitAgent(ref TheUser);

                    response.Value = responseData;
                    //                   TheUser.SendDataToSim(SimInfo);
                    return response;

                }
                catch (Exception E)
                {
                    Console.WriteLine(E.ToString());
                }
                //}
            }
            return response;

        }

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

        public string CreateUnknownUserErrorResponse()
        {
            return "<error>Unknown user</error>";
        }

        /// <summary>
        /// Converts a user profile to an XML element which can be returned
        /// </summary>
        /// <param name="profile">The user profile</param>
        /// <returns>A string containing an XML Document of the user profile</returns>
        public string ProfileToXml(UserProfileData profile)
        {
            System.IO.StringWriter sw = new System.IO.StringWriter();
            XmlTextWriter xw = new XmlTextWriter(sw);

            // Header
            xw.Formatting = Formatting.Indented;
            xw.WriteStartDocument();
            xw.WriteDocType("userprofile", null, null, null);
            xw.WriteComment("Found user profiles matching the request");
            xw.WriteStartElement("users");

            // User
            xw.WriteStartElement("user");
            xw.WriteAttributeString("firstname", profile.username);
            xw.WriteAttributeString("lastname", profile.surname);
            xw.WriteAttributeString("uuid", profile.UUID.ToStringHyphenated());
            xw.WriteAttributeString("inventory", profile.userInventoryURI);
            xw.WriteAttributeString("asset", profile.userAssetURI);
            xw.WriteEndElement();

            // Footer
            xw.WriteEndElement();
            xw.Flush();
            xw.Close();

            return sw.ToString();
        }

        public string RestGetUserMethodName(string request, string path, string param)
        {
            UserProfileData userProfile = getUserProfile(param.Trim());

            if (userProfile == null)
            {
                return CreateUnknownUserErrorResponse();
            }

            return ProfileToXml(userProfile);
        }

    }
}
