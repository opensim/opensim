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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Statistics;
using OpenSim.Services.Interfaces;

namespace OpenSim.Framework.Communications.Services
{
    public abstract class LoginService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected string m_welcomeMessage = "Welcome to OpenSim";
        protected int m_minLoginLevel = 0;
        protected UserManagerBase m_userManager = null;
        protected Mutex m_loginMutex = new Mutex(false);

        /// <summary>
        /// Used during login to send the skeleton of the OpenSim Library to the client.
        /// </summary>
        protected LibraryRootFolder m_libraryRootFolder;

        protected uint m_defaultHomeX;
        protected uint m_defaultHomeY;

        protected bool m_warn_already_logged = true;

        /// <summary>
        /// Used by the login service to make requests to the inventory service.
        /// </summary>
        protected IInterServiceInventoryServices m_interInventoryService;
        // Hack
        protected IInventoryService m_InventoryService;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="userManager"></param>
        /// <param name="libraryRootFolder"></param>
        /// <param name="welcomeMess"></param>
        public LoginService(UserManagerBase userManager, LibraryRootFolder libraryRootFolder,
                            string welcomeMess)
        {
            m_userManager = userManager;
            m_libraryRootFolder = libraryRootFolder;

            if (welcomeMess != String.Empty)
            {
                m_welcomeMessage = welcomeMess;
            }
        }

        /// <summary>
        /// If the user is already logged in, try to notify the region that the user they've got is dead.
        /// </summary>
        /// <param name="theUser"></param>
        public virtual void LogOffUser(UserProfileData theUser, string message)
        {
        }
        
        /// <summary>
        /// Called when we receive the client's initial XMLRPC login_to_simulator request message
        /// </summary>
        /// <param name="request">The XMLRPC request</param>
        /// <returns>The response to send</returns>
        public virtual XmlRpcResponse XmlRpcLoginMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Temporary fix
            m_loginMutex.WaitOne();

            try
            {
                //CFK: CustomizeResponse contains sufficient strings to alleviate the need for this.
                //CKF: m_log.Info("[LOGIN]: Attempting login now...");
                XmlRpcResponse response = new XmlRpcResponse();
                Hashtable requestData = (Hashtable)request.Params[0];

                SniffLoginKey((Uri)request.Params[2], requestData);

                bool GoodXML = (requestData.Contains("first") && requestData.Contains("last") &&
                                (requestData.Contains("passwd") || requestData.Contains("web_login_key")));

                string startLocationRequest = "last";

                UserProfileData userProfile;
                LoginResponse logResponse = new LoginResponse();

                string firstname;
                string lastname;

                if (GoodXML)
                {
                    if (requestData.Contains("start"))
                    {
                        startLocationRequest = (string)requestData["start"];
                    }

                    firstname = (string)requestData["first"];
                    lastname = (string)requestData["last"];

                    m_log.InfoFormat(
                        "[LOGIN BEGIN]: XMLRPC Received login request message from user '{0}' '{1}'",
                        firstname, lastname);

                    string clientVersion = "Unknown";

                    if (requestData.Contains("version"))
                    {
                        clientVersion = (string)requestData["version"];
                    }

                    m_log.DebugFormat(
                        "[LOGIN]: XMLRPC Client is {0}, start location is {1}", clientVersion, startLocationRequest);

                    if (!TryAuthenticateXmlRpcLogin(request, firstname, lastname, out userProfile))
                    {
                        return logResponse.CreateLoginFailedResponse();
                    }
                }
                else
                {
                    m_log.Info(
                        "[LOGIN END]: XMLRPC login_to_simulator login message did not contain all the required data");

                    return logResponse.CreateGridErrorResponse();
                }

                if (userProfile.GodLevel < m_minLoginLevel)
                {
                    return logResponse.CreateLoginBlockedResponse();
                }
                else
                {
                    // If we already have a session...
                    if (userProfile.CurrentAgent != null && userProfile.CurrentAgent.AgentOnline)
                    {
                        //TODO: The following statements can cause trouble:
                        //      If agentOnline could not turn from true back to false normally
                        //      because of some problem, for instance, the crashment of server or client,
                        //      the user cannot log in any longer.
                        userProfile.CurrentAgent.AgentOnline = false;

                        m_userManager.CommitAgent(ref userProfile);

                        // try to tell the region that their user is dead.
                        LogOffUser(userProfile, " XMLRPC You were logged off because you logged in from another location");

                        if (m_warn_already_logged)
                        {
                            // This is behavior for for grid, reject login
                            m_log.InfoFormat(
                                "[LOGIN END]: XMLRPC Notifying user {0} {1} that they are already logged in",
                                firstname, lastname);

                            return logResponse.CreateAlreadyLoggedInResponse();
                        }
                        else
                        {
                            // This is behavior for standalone (silent logout of last hung session)
                            m_log.InfoFormat(
                                "[LOGIN]: XMLRPC User {0} {1} is already logged in, not notifying user, kicking old presence and starting new login.",
                                firstname, lastname);
                        }
                    }

                    // Otherwise...
                    // Create a new agent session

                    // XXYY we don't need this
                    //m_userManager.ResetAttachments(userProfile.ID);

                    CreateAgent(userProfile, request);

                    // We need to commit the agent right here, even though the userProfile info is not complete
                    // at this point. There is another commit further down.
                    // This is for the new sessionID to be stored so that the region can check it for session authentication. 
                    // CustomiseResponse->PrepareLoginToRegion
                    CommitAgent(ref userProfile);

                    try
                    {
                        UUID agentID = userProfile.ID;
                        InventoryData inventData = null;

                        try
                        {
                            inventData = GetInventorySkeleton(agentID);
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat(
                                "[LOGIN END]: Error retrieving inventory skeleton of agent {0} - {1}",
                                agentID, e);

                            // Let's not panic
                            if (!AllowLoginWithoutInventory())
                                return logResponse.CreateLoginInventoryFailedResponse();
                        }

                        if (inventData != null)
                        {
                            ArrayList AgentInventoryArray = inventData.InventoryArray;

                            Hashtable InventoryRootHash = new Hashtable();
                            InventoryRootHash["folder_id"] = inventData.RootFolderID.ToString();
                            ArrayList InventoryRoot = new ArrayList();
                            InventoryRoot.Add(InventoryRootHash);
                            userProfile.RootInventoryFolderID = inventData.RootFolderID;

                            logResponse.InventoryRoot = InventoryRoot;
                            logResponse.InventorySkeleton = AgentInventoryArray;
                        }

                        // Inventory Library Section
                        Hashtable InventoryLibRootHash = new Hashtable();
                        InventoryLibRootHash["folder_id"] = "00000112-000f-0000-0000-000100bba000";
                        ArrayList InventoryLibRoot = new ArrayList();
                        InventoryLibRoot.Add(InventoryLibRootHash);

                        logResponse.InventoryLibRoot = InventoryLibRoot;
                        logResponse.InventoryLibraryOwner = GetLibraryOwner();
                        logResponse.InventoryLibrary = GetInventoryLibrary();

                        logResponse.CircuitCode = Util.RandomClass.Next();
                        logResponse.Lastname = userProfile.SurName;
                        logResponse.Firstname = userProfile.FirstName;
                        logResponse.AgentID = agentID;
                        logResponse.SessionID = userProfile.CurrentAgent.SessionID;
                        logResponse.SecureSessionID = userProfile.CurrentAgent.SecureSessionID;
                        logResponse.Message = GetMessage();
                        logResponse.BuddList = ConvertFriendListItem(m_userManager.GetUserFriendList(agentID));
                        logResponse.StartLocation = startLocationRequest;

                        if (CustomiseResponse(logResponse, userProfile, startLocationRequest, remoteClient))
                        {
                            userProfile.LastLogin = userProfile.CurrentAgent.LoginTime;
                            CommitAgent(ref userProfile);

                            // If we reach this point, then the login has successfully logged onto the grid
                            if (StatsManager.UserStats != null)
                                StatsManager.UserStats.AddSuccessfulLogin();

                            m_log.DebugFormat(
                                "[LOGIN END]: XMLRPC Authentication of user {0} {1} successful.  Sending response to client.",
                                firstname, lastname);

                            return logResponse.ToXmlRpcResponse();
                        }
                        else
                        {
                            m_log.ErrorFormat("[LOGIN END]: XMLRPC informing user {0} {1} that login failed due to an unavailable region", firstname, lastname);
                            return logResponse.CreateDeadRegionResponse();
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Error("[LOGIN END]: XMLRPC Login failed, " + e);
                        m_log.Error(e.StackTrace);
                    }
                }

                m_log.Info("[LOGIN END]: XMLRPC Login failed.  Sending back blank XMLRPC response");
                return response;
            }
            finally
            {
                m_loginMutex.ReleaseMutex();
            }
        }

        protected virtual bool TryAuthenticateXmlRpcLogin(
            XmlRpcRequest request, string firstname, string lastname, out UserProfileData userProfile)
        {
            Hashtable requestData = (Hashtable)request.Params[0];

            userProfile = GetTheUser(firstname, lastname);
            if (userProfile == null)
            {
                m_log.Debug("[LOGIN END]: XMLRPC Could not find a profile for " + firstname + " " + lastname);
                return false;
            }
            else
            {
                if (requestData.Contains("passwd"))
                {
                    string passwd = (string)requestData["passwd"];
                    bool authenticated = AuthenticateUser(userProfile, passwd);

                    if (!authenticated)
                        m_log.DebugFormat("[LOGIN END]: XMLRPC User {0} {1} failed password authentication",
                            firstname, lastname);

                    return authenticated;
                }
                
                if (requestData.Contains("web_login_key"))
                {
                    try
                    {
                        UUID webloginkey = new UUID((string)requestData["web_login_key"]);
                        bool authenticated = AuthenticateUser(userProfile, webloginkey);

                        if (!authenticated)
                            m_log.DebugFormat("[LOGIN END]: XMLRPC User {0} {1} failed web login key authentication",
                                firstname, lastname);

                        return authenticated;
                    }
                    catch (Exception e)
                    {
                        m_log.DebugFormat(
                            "[LOGIN END]: XMLRPC Bad web_login_key: {0} for user {1} {2}, exception {3}",
                            requestData["web_login_key"], firstname, lastname, e);

                        return false;
                    }
                }

                m_log.DebugFormat(
                    "[LOGIN END]: XMLRPC login request for {0} {1} contained neither a password nor a web login key",
                    firstname, lastname);
            }

            return false;
        }

        protected virtual bool TryAuthenticateLLSDLogin(string firstname, string lastname, string passwd, out UserProfileData userProfile)
        {
            bool GoodLogin = false;
            userProfile = GetTheUser(firstname, lastname);
            if (userProfile == null)
            {
                m_log.Info("[LOGIN]: LLSD Could not find a profile for " + firstname + " " + lastname);

                return false;
            }

            GoodLogin = AuthenticateUser(userProfile, passwd);
            return GoodLogin;
        }

        /// <summary>
        /// Called when we receive the client's initial LLSD login_to_simulator request message
        /// </summary>
        /// <param name="request">The LLSD request</param>
        /// <returns>The response to send</returns>
        public OSD LLSDLoginMethod(OSD request, IPEndPoint remoteClient)
        {
            // Temporary fix
            m_loginMutex.WaitOne();

            try
            {
                // bool GoodLogin = false;

                string startLocationRequest = "last";

                UserProfileData userProfile = null;
                LoginResponse logResponse = new LoginResponse();

                if (request.Type == OSDType.Map)
                {
                    OSDMap map = (OSDMap)request;

                    if (map.ContainsKey("first") && map.ContainsKey("last") && map.ContainsKey("passwd"))
                    {
                        string firstname = map["first"].AsString();
                        string lastname = map["last"].AsString();
                        string passwd = map["passwd"].AsString();

                        if (map.ContainsKey("start"))
                        {
                            m_log.Info("[LOGIN]: LLSD StartLocation Requested: " + map["start"].AsString());
                            startLocationRequest = map["start"].AsString();
                        }
                        m_log.Info("[LOGIN]: LLSD Login Requested for: '" + firstname + "' '" + lastname + "' / " + passwd);

                        if (!TryAuthenticateLLSDLogin(firstname, lastname, passwd, out userProfile))
                        {
                            return logResponse.CreateLoginFailedResponseLLSD();
                        }
                    }
                    else
                        return logResponse.CreateLoginFailedResponseLLSD();
                }
                else
                    return logResponse.CreateLoginFailedResponseLLSD();


                if (userProfile.GodLevel < m_minLoginLevel)
                {
                    return logResponse.CreateLoginBlockedResponseLLSD();
                }
                else
                {
                    // If we already have a session...
                    if (userProfile.CurrentAgent != null && userProfile.CurrentAgent.AgentOnline)
                    {
                        userProfile.CurrentAgent.AgentOnline = false;

                        m_userManager.CommitAgent(ref userProfile);
                        // try to tell the region that their user is dead.
                        LogOffUser(userProfile, " LLSD You were logged off because you logged in from another location");

                        if (m_warn_already_logged)
                        {
                            // This is behavior for for grid, reject login
                            m_log.InfoFormat(
                                "[LOGIN END]:  LLSD Notifying user {0} {1} that they are already logged in",
                                userProfile.FirstName, userProfile.SurName);

                            userProfile.CurrentAgent = null;
                            return logResponse.CreateAlreadyLoggedInResponseLLSD();
                        }
                        else
                        {
                            // This is behavior for standalone (silent logout of last hung session)
                            m_log.InfoFormat(
                                "[LOGIN]: LLSD User {0} {1} is already logged in, not notifying user, kicking old presence and starting new login.",
                                userProfile.FirstName, userProfile.SurName);
                        }
                    }

                    // Otherwise...
                    // Create a new agent session

                    // XXYY We don't need this
                    //m_userManager.ResetAttachments(userProfile.ID);

                    CreateAgent(userProfile, request);

                    // We need to commit the agent right here, even though the userProfile info is not complete
                    // at this point. There is another commit further down.
                    // This is for the new sessionID to be stored so that the region can check it for session authentication. 
                    // CustomiseResponse->PrepareLoginToRegion
                    CommitAgent(ref userProfile);

                    try
                    {
                        UUID agentID = userProfile.ID;

                        //InventoryData inventData = GetInventorySkeleton(agentID);
                        InventoryData inventData = null;

                        try
                        {
                            inventData = GetInventorySkeleton(agentID);
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat(
                                "[LOGIN END]:  LLSD Error retrieving inventory skeleton of agent {0}, {1} - {2}",
                                agentID, e.GetType(), e.Message);

                            return logResponse.CreateLoginFailedResponseLLSD();//  .CreateLoginInventoryFailedResponseLLSD ();
                        }


                        ArrayList AgentInventoryArray = inventData.InventoryArray;

                        Hashtable InventoryRootHash = new Hashtable();
                        InventoryRootHash["folder_id"] = inventData.RootFolderID.ToString();
                        ArrayList InventoryRoot = new ArrayList();
                        InventoryRoot.Add(InventoryRootHash);
                        userProfile.RootInventoryFolderID = inventData.RootFolderID;


                        // Inventory Library Section
                        Hashtable InventoryLibRootHash = new Hashtable();
                        InventoryLibRootHash["folder_id"] = "00000112-000f-0000-0000-000100bba000";
                        ArrayList InventoryLibRoot = new ArrayList();
                        InventoryLibRoot.Add(InventoryLibRootHash);

                        logResponse.InventoryLibRoot = InventoryLibRoot;
                        logResponse.InventoryLibraryOwner = GetLibraryOwner();
                        logResponse.InventoryRoot = InventoryRoot;
                        logResponse.InventorySkeleton = AgentInventoryArray;
                        logResponse.InventoryLibrary = GetInventoryLibrary();

                        logResponse.CircuitCode = (Int32)Util.RandomClass.Next();
                        logResponse.Lastname = userProfile.SurName;
                        logResponse.Firstname = userProfile.FirstName;
                        logResponse.AgentID = agentID;
                        logResponse.SessionID = userProfile.CurrentAgent.SessionID;
                        logResponse.SecureSessionID = userProfile.CurrentAgent.SecureSessionID;
                        logResponse.Message = GetMessage();
                        logResponse.BuddList = ConvertFriendListItem(m_userManager.GetUserFriendList(agentID));
                        logResponse.StartLocation = startLocationRequest;

                        try
                        {
                            CustomiseResponse(logResponse, userProfile, startLocationRequest, remoteClient);
                        }
                        catch (Exception ex)
                        {
                            m_log.Info("[LOGIN]:  LLSD " + ex.ToString());
                            return logResponse.CreateDeadRegionResponseLLSD();
                        }

                        userProfile.LastLogin = userProfile.CurrentAgent.LoginTime;
                        CommitAgent(ref userProfile);

                        // If we reach this point, then the login has successfully logged onto the grid
                        if (StatsManager.UserStats != null)
                            StatsManager.UserStats.AddSuccessfulLogin();

                        m_log.DebugFormat(
                            "[LOGIN END]:  LLSD Authentication of user {0} {1} successful.  Sending response to client.",
                            userProfile.FirstName, userProfile.SurName);

                        return logResponse.ToLLSDResponse();
                    }
                    catch (Exception ex)
                    {
                        m_log.Info("[LOGIN]:  LLSD " + ex.ToString());
                        return logResponse.CreateFailedResponseLLSD();
                    }
                }
            }
            finally
            {
                m_loginMutex.ReleaseMutex();
            }
        }

        public Hashtable ProcessHTMLLogin(Hashtable keysvals)
        {
            // Matches all unspecified characters
            // Currently specified,; lowercase letters, upper case letters, numbers, underline
            //    period, space, parens, and dash.

            Regex wfcut = new Regex("[^a-zA-Z0-9_\\.\\$ \\(\\)\\-]");

            Hashtable returnactions = new Hashtable();
            int statuscode = 200;

            string firstname = String.Empty;
            string lastname = String.Empty;
            string location = String.Empty;
            string region = String.Empty;
            string grid = String.Empty;
            string channel = String.Empty;
            string version = String.Empty;
            string lang = String.Empty;
            string password = String.Empty;
            string errormessages = String.Empty;

            // the client requires the HTML form field be named 'username'
            // however, the data it sends when it loads the first time is 'firstname'
            // another one of those little nuances.

            if (keysvals.Contains("firstname"))
                firstname = wfcut.Replace((string)keysvals["firstname"], String.Empty, 99999);

            if (keysvals.Contains("username"))
                firstname = wfcut.Replace((string)keysvals["username"], String.Empty, 99999);

            if (keysvals.Contains("lastname"))
                lastname = wfcut.Replace((string)keysvals["lastname"], String.Empty, 99999);

            if (keysvals.Contains("location"))
                location = wfcut.Replace((string)keysvals["location"], String.Empty, 99999);

            if (keysvals.Contains("region"))
                region = wfcut.Replace((string)keysvals["region"], String.Empty, 99999);

            if (keysvals.Contains("grid"))
                grid = wfcut.Replace((string)keysvals["grid"], String.Empty, 99999);

            if (keysvals.Contains("channel"))
                channel = wfcut.Replace((string)keysvals["channel"], String.Empty, 99999);

            if (keysvals.Contains("version"))
                version = wfcut.Replace((string)keysvals["version"], String.Empty, 99999);

            if (keysvals.Contains("lang"))
                lang = wfcut.Replace((string)keysvals["lang"], String.Empty, 99999);

            if (keysvals.Contains("password"))
                password = wfcut.Replace((string)keysvals["password"], String.Empty, 99999);

            // load our login form.
            string loginform = GetLoginForm(firstname, lastname, location, region, grid, channel, version, lang, password, errormessages);

            if (keysvals.ContainsKey("show_login_form"))
            {
                UserProfileData user = GetTheUser(firstname, lastname);
                bool goodweblogin = false;

                if (user != null)
                    goodweblogin = AuthenticateUser(user, password);

                if (goodweblogin)
                {
                    UUID webloginkey = UUID.Random();
                    m_userManager.StoreWebLoginKey(user.ID, webloginkey);
                    //statuscode = 301;

                    //                    string redirectURL = "about:blank?redirect-http-hack=" +
                    //                                         HttpUtility.UrlEncode("secondlife:///app/login?first_name=" + firstname + "&last_name=" +
                    //                                                               lastname +
                    //                                                               "&location=" + location + "&grid=Other&web_login_key=" + webloginkey.ToString());
                    //m_log.Info("[WEB]: R:" + redirectURL);
                    returnactions["int_response_code"] = statuscode;
                    //returnactions["str_redirect_location"] = redirectURL;
                    //returnactions["str_response_string"] = "<HTML><BODY>GoodLogin</BODY></HTML>";
                    returnactions["str_response_string"] = webloginkey.ToString();
                }
                else
                {
                    errormessages = "The Username and password supplied did not match our records. Check your caps lock and try again";

                    loginform = GetLoginForm(firstname, lastname, location, region, grid, channel, version, lang, password, errormessages);
                    returnactions["int_response_code"] = statuscode;
                    returnactions["str_response_string"] = loginform;
                }
            }
            else
            {
                returnactions["int_response_code"] = statuscode;
                returnactions["str_response_string"] = loginform;
            }
            return returnactions;
        }

        public string GetLoginForm(string firstname, string lastname, string location, string region,
                                   string grid, string channel, string version, string lang,
                                   string password, string errormessages)
        {
            // inject our values in the form at the markers

            string loginform = String.Empty;
            string file = Path.Combine(Util.configDir(), "http_loginform.html");
            if (!File.Exists(file))
            {
                loginform = GetDefaultLoginForm();
            }
            else
            {
                StreamReader sr = File.OpenText(file);
                loginform = sr.ReadToEnd();
                sr.Close();
            }

            loginform = loginform.Replace("[$firstname]", firstname);
            loginform = loginform.Replace("[$lastname]", lastname);
            loginform = loginform.Replace("[$location]", location);
            loginform = loginform.Replace("[$region]", region);
            loginform = loginform.Replace("[$grid]", grid);
            loginform = loginform.Replace("[$channel]", channel);
            loginform = loginform.Replace("[$version]", version);
            loginform = loginform.Replace("[$lang]", lang);
            loginform = loginform.Replace("[$password]", password);
            loginform = loginform.Replace("[$errors]", errormessages);

            return loginform;
        }

        public string GetDefaultLoginForm()
        {
            string responseString =
                "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\">";
            responseString += "<html xmlns=\"http://www.w3.org/1999/xhtml\">";
            responseString += "<head>";
            responseString += "<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" />";
            responseString += "<meta http-equiv=\"cache-control\" content=\"no-cache\">";
            responseString += "<meta http-equiv=\"Pragma\" content=\"no-cache\">";
            responseString += "<title>OpenSim Login</title>";
            responseString += "<body><br />";
            responseString += "<div id=\"login_box\">";

            responseString += "<form action=\"/go.cgi\" method=\"GET\" id=\"login-form\">";

            responseString += "<div id=\"message\">[$errors]</div>";
            responseString += "<fieldset id=\"firstname\">";
            responseString += "<legend>First Name:</legend>";
            responseString += "<input type=\"text\" id=\"firstname_input\" size=\"15\" maxlength=\"100\" name=\"username\" value=\"[$firstname]\" />";
            responseString += "</fieldset>";
            responseString += "<fieldset id=\"lastname\">";
            responseString += "<legend>Last Name:</legend>";
            responseString += "<input type=\"text\" size=\"15\" maxlength=\"100\" name=\"lastname\" value=\"[$lastname]\" />";
            responseString += "</fieldset>";
            responseString += "<fieldset id=\"password\">";
            responseString += "<legend>Password:</legend>";
            responseString += "<table cellspacing=\"0\" cellpadding=\"0\" border=\"0\">";
            responseString += "<tr>";
            responseString += "<td colspan=\"2\"><input type=\"password\" size=\"15\" maxlength=\"100\" name=\"password\" value=\"[$password]\" /></td>";
            responseString += "</tr>";
            responseString += "<tr>";
            responseString += "<td valign=\"middle\"><input type=\"checkbox\" name=\"remember_password\" id=\"remember_password\" [$remember_password] style=\"margin-left:0px;\"/></td>";
            responseString += "<td><label for=\"remember_password\">Remember password</label></td>";
            responseString += "</tr>";
            responseString += "</table>";
            responseString += "</fieldset>";
            responseString += "<input type=\"hidden\" name=\"show_login_form\" value=\"FALSE\" />";
            responseString += "<input type=\"hidden\" name=\"method\" value=\"login\" />";
            responseString += "<input type=\"hidden\" id=\"grid\" name=\"grid\" value=\"[$grid]\" />";
            responseString += "<input type=\"hidden\" id=\"region\" name=\"region\" value=\"[$region]\" />";
            responseString += "<input type=\"hidden\" id=\"location\" name=\"location\" value=\"[$location]\" />";
            responseString += "<input type=\"hidden\" id=\"channel\" name=\"channel\" value=\"[$channel]\" />";
            responseString += "<input type=\"hidden\" id=\"version\" name=\"version\" value=\"[$version]\" />";
            responseString += "<input type=\"hidden\" id=\"lang\" name=\"lang\" value=\"[$lang]\" />";
            responseString += "<div id=\"submitbtn\">";
            responseString += "<input class=\"input_over\" type=\"submit\" value=\"Connect\" />";
            responseString += "</div>";
            responseString += "<div id=\"connecting\" style=\"visibility:hidden\"> Connecting...</div>";

            responseString += "<div id=\"helplinks\"><!---";
            responseString += "<a href=\"#join now link\" target=\"_blank\"></a> | ";
            responseString += "<a href=\"#forgot password link\" target=\"_blank\"></a>";
            responseString += "---></div>";

            responseString += "<div id=\"channelinfo\"> [$channel] | [$version]=[$lang]</div>";
            responseString += "</form>";
            responseString += "<script language=\"JavaScript\">";
            responseString += "document.getElementById('firstname_input').focus();";
            responseString += "</script>";
            responseString += "</div>";
            responseString += "</div>";
            responseString += "</body>";
            responseString += "</html>";

            return responseString;
        }

        /// <summary>
        /// Saves a target agent to the database
        /// </summary>
        /// <param name="profile">The users profile</param>
        /// <returns>Successful?</returns>
        public bool CommitAgent(ref UserProfileData profile)
        {
            return m_userManager.CommitAgent(ref profile);
        }

        /// <summary>
        /// Checks a user against it's password hash
        /// </summary>
        /// <param name="profile">The users profile</param>
        /// <param name="password">The supplied password</param>
        /// <returns>Authenticated?</returns>
        public virtual bool AuthenticateUser(UserProfileData profile, string password)
        {
            bool passwordSuccess = false;
            //m_log.InfoFormat("[LOGIN]: Authenticating {0} {1} ({2})", profile.FirstName, profile.SurName, profile.ID);

            // Web Login method seems to also occasionally send the hashed password itself

            // we do this to get our hash in a form that the server password code can consume
            // when the web-login-form submits the password in the clear (supposed to be over SSL!)
            if (!password.StartsWith("$1$"))
                password = "$1$" + Util.Md5Hash(password);

            password = password.Remove(0, 3); //remove $1$

            string s = Util.Md5Hash(password + ":" + profile.PasswordSalt);
            // Testing...
            //m_log.Info("[LOGIN]: SubHash:" + s + " userprofile:" + profile.passwordHash);
            //m_log.Info("[LOGIN]: userprofile:" + profile.passwordHash + " SubCT:" + password);

            passwordSuccess = (profile.PasswordHash.Equals(s.ToString(), StringComparison.InvariantCultureIgnoreCase)
                               || profile.PasswordHash.Equals(password, StringComparison.InvariantCulture));
            
            return passwordSuccess;
        }

        public virtual bool AuthenticateUser(UserProfileData profile, UUID webloginkey)
        {
            bool passwordSuccess = false;
            m_log.InfoFormat("[LOGIN]: Authenticating {0} {1} ({2})", profile.FirstName, profile.SurName, profile.ID);

            // Match web login key unless it's the default weblogin key UUID.Zero
            passwordSuccess = ((profile.WebLoginKey == webloginkey) && profile.WebLoginKey != UUID.Zero);

            return passwordSuccess;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="request"></param>
        public void CreateAgent(UserProfileData profile, XmlRpcRequest request)
        {
            m_userManager.CreateAgent(profile, request);
        }

        public void CreateAgent(UserProfileData profile, OSD request)
        {
            m_userManager.CreateAgent(profile, request);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="firstname"></param>
        /// <param name="lastname"></param>
        /// <returns></returns>
        public virtual UserProfileData GetTheUser(string firstname, string lastname)
        {
            return m_userManager.GetUserProfile(firstname, lastname);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public virtual string GetMessage()
        {
            return m_welcomeMessage;
        }

        private static LoginResponse.BuddyList ConvertFriendListItem(List<FriendListItem> LFL)
        {
            LoginResponse.BuddyList buddylistreturn = new LoginResponse.BuddyList();
            foreach (FriendListItem fl in LFL)
            {
                LoginResponse.BuddyList.BuddyInfo buddyitem = new LoginResponse.BuddyList.BuddyInfo(fl.Friend);
                buddyitem.BuddyID = fl.Friend;
                buddyitem.BuddyRightsHave = (int)fl.FriendListOwnerPerms;
                buddyitem.BuddyRightsGiven = (int)fl.FriendPerms;
                buddylistreturn.AddNewBuddy(buddyitem);
            }
            return buddylistreturn;
        }

        /// <summary>
        /// Converts the inventory library skeleton into the form required by the rpc request.
        /// </summary>
        /// <returns></returns>
        protected virtual ArrayList GetInventoryLibrary()
        {
            Dictionary<UUID, InventoryFolderImpl> rootFolders
                = m_libraryRootFolder.RequestSelfAndDescendentFolders();
            ArrayList folderHashes = new ArrayList();

            foreach (InventoryFolderBase folder in rootFolders.Values)
            {
                Hashtable TempHash = new Hashtable();
                TempHash["name"] = folder.Name;
                TempHash["parent_id"] = folder.ParentID.ToString();
                TempHash["version"] = (Int32)folder.Version;
                TempHash["type_default"] = (Int32)folder.Type;
                TempHash["folder_id"] = folder.ID.ToString();
                folderHashes.Add(TempHash);
            }

            return folderHashes;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        protected virtual ArrayList GetLibraryOwner()
        {
            //for now create random inventory library owner
            Hashtable TempHash = new Hashtable();
            TempHash["agent_id"] = "11111111-1111-0000-0000-000100bba000";
            ArrayList inventoryLibOwner = new ArrayList();
            inventoryLibOwner.Add(TempHash);
            return inventoryLibOwner;
        }

        public class InventoryData
        {
            public ArrayList InventoryArray = null;
            public UUID RootFolderID = UUID.Zero;

            public InventoryData(ArrayList invList, UUID rootID)
            {
                InventoryArray = invList;
                RootFolderID = rootID;
            }
        }

        protected void SniffLoginKey(Uri uri, Hashtable requestData)
        {
            string uri_str = uri.ToString();
            string[] parts = uri_str.Split(new char[] { '=' });
            if (parts.Length > 1)
            {
                string web_login_key = parts[1];
                requestData.Add("web_login_key", web_login_key);
                m_log.InfoFormat("[LOGIN]: Login with web_login_key {0}", web_login_key);
            }
        }

        /// <summary>
        /// Customises the login response and fills in missing values.  This method also tells the login region to
        /// expect a client connection.
        /// </summary>
        /// <param name="response">The existing response</param>
        /// <param name="theUser">The user profile</param>
        /// <param name="startLocationRequest">The requested start location</param>
        /// <returns>true on success, false if the region was not successfully told to expect a user connection</returns>
        public bool CustomiseResponse(LoginResponse response, UserProfileData theUser, string startLocationRequest, IPEndPoint client)
        {
            // add active gestures to login-response
            AddActiveGestures(response, theUser);

            // HomeLocation
            RegionInfo homeInfo = null;

            // use the homeRegionID if it is stored already. If not, use the regionHandle as before
            UUID homeRegionId = theUser.HomeRegionID;
            ulong homeRegionHandle = theUser.HomeRegion;
            if (homeRegionId != UUID.Zero)
            {
                homeInfo = GetRegionInfo(homeRegionId);
            }
            else
            {
                homeInfo = GetRegionInfo(homeRegionHandle);
            }

            if (homeInfo != null)
            {
                response.Home =
                    string.Format(
                        "{{'region_handle':[r{0},r{1}], 'position':[r{2},r{3},r{4}], 'look_at':[r{5},r{6},r{7}]}}",
                        (homeInfo.RegionLocX * Constants.RegionSize),
                        (homeInfo.RegionLocY * Constants.RegionSize),
                        theUser.HomeLocation.X, theUser.HomeLocation.Y, theUser.HomeLocation.Z,
                        theUser.HomeLookAt.X, theUser.HomeLookAt.Y, theUser.HomeLookAt.Z);
            }
            else
            {
                m_log.InfoFormat("not found the region at {0} {1}", theUser.HomeRegionX, theUser.HomeRegionY);
                // Emergency mode: Home-region isn't available, so we can't request the region info.
                // Use the stored home regionHandle instead.
                // NOTE: If the home-region moves, this will be wrong until the users update their user-profile again
                ulong regionX = homeRegionHandle >> 32;
                ulong regionY = homeRegionHandle & 0xffffffff;
                response.Home =
                    string.Format(
                        "{{'region_handle':[r{0},r{1}], 'position':[r{2},r{3},r{4}], 'look_at':[r{5},r{6},r{7}]}}",
                        regionX, regionY,
                        theUser.HomeLocation.X, theUser.HomeLocation.Y, theUser.HomeLocation.Z,
                        theUser.HomeLookAt.X, theUser.HomeLookAt.Y, theUser.HomeLookAt.Z);

                m_log.InfoFormat("[LOGIN] Home region of user {0} {1} is not available; using computed region position {2} {3}",
                                 theUser.FirstName, theUser.SurName,
                                 regionX, regionY);
            }

            // StartLocation
            RegionInfo regionInfo = null;
            if (startLocationRequest == "home")
            {
                regionInfo = homeInfo;
                theUser.CurrentAgent.Position = theUser.HomeLocation;
                response.LookAt = String.Format("[r{0},r{1},r{2}]", theUser.HomeLookAt.X.ToString(), 
                                                theUser.HomeLookAt.Y.ToString(), theUser.HomeLookAt.Z.ToString());
            }
            else if (startLocationRequest == "last")
            {
                UUID lastRegion = theUser.CurrentAgent.Region;
                regionInfo = GetRegionInfo(lastRegion);
                response.LookAt = String.Format("[r{0},r{1},r{2}]", theUser.CurrentAgent.LookAt.X.ToString(),
                                                theUser.CurrentAgent.LookAt.Y.ToString(), theUser.CurrentAgent.LookAt.Z.ToString());
            }
            else
            {
                Regex reURI = new Regex(@"^uri:(?<region>[^&]+)&(?<x>\d+)&(?<y>\d+)&(?<z>\d+)$");
                Match uriMatch = reURI.Match(startLocationRequest);
                if (uriMatch == null)
                {
                    m_log.InfoFormat("[LOGIN]: Got Custom Login URL {0}, but can't process it", startLocationRequest);
                }
                else
                {
                    string region = uriMatch.Groups["region"].ToString();
                    regionInfo = RequestClosestRegion(region);
                    if (regionInfo == null)
                    {
                        m_log.InfoFormat("[LOGIN]: Got Custom Login URL {0}, can't locate region {1}", startLocationRequest, region);
                    }
                    else
                    {
                        theUser.CurrentAgent.Position = new Vector3(float.Parse(uriMatch.Groups["x"].Value),
                                                                    float.Parse(uriMatch.Groups["y"].Value), float.Parse(uriMatch.Groups["z"].Value));
                    }
                }
                response.LookAt = "[r0,r1,r0]";
                // can be: last, home, safe, url
                response.StartLocation = "url";
            }

            if ((regionInfo != null) && (PrepareLoginToRegion(regionInfo, theUser, response, client)))
            {
                return true;
            }

            // Get the default region handle
            ulong defaultHandle = Utils.UIntsToLong(m_defaultHomeX * Constants.RegionSize, m_defaultHomeY * Constants.RegionSize);

            // If we haven't already tried the default region, reset regionInfo
            if (regionInfo != null && defaultHandle != regionInfo.RegionHandle)
                regionInfo = null;

            if (regionInfo == null)
            {
                m_log.Error("[LOGIN]: Sending user to default region " + defaultHandle + " instead");
                regionInfo = GetRegionInfo(defaultHandle);
            }

            if (regionInfo == null)
            {
                m_log.ErrorFormat("[LOGIN]: Sending user to any region");
                regionInfo = RequestClosestRegion(String.Empty);
            }

            theUser.CurrentAgent.Position = new Vector3(128f, 128f, 0f);
            response.StartLocation = "safe";

            return PrepareLoginToRegion(regionInfo, theUser, response, client);
        }

        protected abstract RegionInfo RequestClosestRegion(string region);
        protected abstract RegionInfo GetRegionInfo(ulong homeRegionHandle);
        protected abstract RegionInfo GetRegionInfo(UUID homeRegionId);

        /// <summary>
        /// Prepare a login to the given region.  This involves both telling the region to expect a connection
        /// and appropriately customising the response to the user.
        /// </summary>
        /// <param name="sim"></param>
        /// <param name="user"></param>
        /// <param name="response"></param>
        /// <param name="remoteClient"></param>
        /// <returns>true if the region was successfully contacted, false otherwise</returns>
        protected abstract bool PrepareLoginToRegion(
            RegionInfo regionInfo, UserProfileData user, LoginResponse response, IPEndPoint client);

        /// <summary>
        /// Add active gestures of the user to the login response.
        /// </summary>
        /// <param name="response">
        /// A <see cref="LoginResponse"/>
        /// </param>
        /// <param name="theUser">
        /// A <see cref="UserProfileData"/>
        /// </param>
        protected void AddActiveGestures(LoginResponse response, UserProfileData theUser)
        {
            List<InventoryItemBase> gestures = null;
            try
            {
                if (m_InventoryService != null)
                    gestures = m_InventoryService.GetActiveGestures(theUser.ID);
                else
                    gestures = m_interInventoryService.GetActiveGestures(theUser.ID);
            }
            catch (Exception e)
            {
                m_log.Debug("[LOGIN]: Unable to retrieve active gestures from inventory server. Reason: " + e.Message);
            }
            //m_log.DebugFormat("[LOGIN]: AddActiveGestures, found {0}", gestures == null ? 0 : gestures.Count);
            ArrayList list = new ArrayList();
            if (gestures != null)
            {
                foreach (InventoryItemBase gesture in gestures)
                {
                    Hashtable item = new Hashtable();
                    item["item_id"] = gesture.ID.ToString();
                    item["asset_id"] = gesture.AssetID.ToString();
                    list.Add(item);
                }
            }
            response.ActiveGestures = list;
        }

        /// <summary>
        /// Get the initial login inventory skeleton (in other words, the folder structure) for the given user.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        /// <exception cref='System.Exception'>This will be thrown if there is a problem with the inventory service</exception>
        protected InventoryData GetInventorySkeleton(UUID userID)
        {
            List<InventoryFolderBase> folders = null;
            if (m_InventoryService != null)
            {
                folders = m_InventoryService.GetInventorySkeleton(userID);
            }
            else
            {
                folders = m_interInventoryService.GetInventorySkeleton(userID);
            }

            // If we have user auth but no inventory folders for some reason, create a new set of folders.
            if (folders == null || folders.Count == 0)
            {
                m_log.InfoFormat(
                    "[LOGIN]: A root inventory folder for user {0} was not found.  Requesting creation.", userID);

                // Although the create user function creates a new agent inventory along with a new user profile, some
                // tools are creating the user profile directly in the database without creating the inventory.  At
                // this time we'll accomodate them by lazily creating the user inventory now if it doesn't already
                // exist.
                if (m_interInventoryService != null)
                {
                    if (!m_interInventoryService.CreateNewUserInventory(userID))
                    {
                        throw new Exception(
                            String.Format(
                                "The inventory creation request for user {0} did not succeed."
                                + "  Please contact your inventory service provider for more information.",
                                userID));
                    }
                }
                else if ((m_InventoryService != null) && !m_InventoryService.CreateUserInventory(userID))
                {
                    throw new Exception(
                        String.Format(
                            "The inventory creation request for user {0} did not succeed."
                            + "  Please contact your inventory service provider for more information.",
                            userID));
                }


                m_log.InfoFormat("[LOGIN]: A new inventory skeleton was successfully created for user {0}", userID);

                if (m_InventoryService != null)
                    folders = m_InventoryService.GetInventorySkeleton(userID);
                else
                    folders = m_interInventoryService.GetInventorySkeleton(userID);

                if (folders == null || folders.Count == 0)
                {
                    throw new Exception(
                        String.Format(
                            "A root inventory folder for user {0} could not be retrieved from the inventory service",
                            userID));
                }
            }

            UUID rootID = UUID.Zero;
            ArrayList AgentInventoryArray = new ArrayList();
            Hashtable TempHash;
            foreach (InventoryFolderBase InvFolder in folders)
            {
                if (InvFolder.ParentID == UUID.Zero)
                {
                    rootID = InvFolder.ID;
                }
                TempHash = new Hashtable();
                TempHash["name"] = InvFolder.Name;
                TempHash["parent_id"] = InvFolder.ParentID.ToString();
                TempHash["version"] = (Int32)InvFolder.Version;
                TempHash["type_default"] = (Int32)InvFolder.Type;
                TempHash["folder_id"] = InvFolder.ID.ToString();
                AgentInventoryArray.Add(TempHash);
            }

            return new InventoryData(AgentInventoryArray, rootID);
        }

        protected virtual bool AllowLoginWithoutInventory()
        {
            return false;
        }

        public XmlRpcResponse XmlRPCCheckAuthSession(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            string authed = "FALSE";
            if (requestData.Contains("avatar_uuid") && requestData.Contains("session_id"))
            {
                UUID guess_aid;
                UUID guess_sid;

                UUID.TryParse((string)requestData["avatar_uuid"], out guess_aid);
                if (guess_aid == UUID.Zero)
                {
                    return Util.CreateUnknownUserErrorResponse();
                }
                
                UUID.TryParse((string)requestData["session_id"], out guess_sid);
                if (guess_sid == UUID.Zero)
                {
                    return Util.CreateUnknownUserErrorResponse();
                }
                
                if (m_userManager.VerifySession(guess_aid, guess_sid))
                {
                    authed = "TRUE";
                    m_log.InfoFormat("[UserManager]: CheckAuthSession TRUE for user {0}", guess_aid);
                }
                else
                {
                    m_log.InfoFormat("[UserManager]: CheckAuthSession FALSE");
                    return Util.CreateUnknownUserErrorResponse();
                }
            }
            
            Hashtable responseData = new Hashtable();
            responseData["auth_session"] = authed;
            response.Value = responseData;
            return response;
        }
    }
}