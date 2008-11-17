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
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using Nwc.XmlRpc;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Statistics;

namespace OpenSim.Framework.Communications
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
        /// Customises the login response and fills in missing values.  This method also tells the login region to
        /// expect a client connection.
        /// </summary>
        /// <param name="response">The existing response</param>
        /// <param name="theUser">The user profile</param>
        /// <returns>true on success, false if the region was not successfully told to expect a user connection</returns>
        public abstract bool CustomiseResponse(LoginResponse response, UserProfileData theUser, string startLocationRequest);

        /// <summary>
        /// If the user is already logged in, try to notify the region that the user they've got is dead.
        /// </summary>
        /// <param name="theUser"></param>
        public virtual void LogOffUser(UserProfileData theUser, string message)
        {
        }

        /// <summary>
        /// Get the initial login inventory skeleton (in other words, the folder structure) for the given user.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        /// <exception cref='System.Exception'>This will be thrown if there is a problem with the inventory service</exception>
        protected abstract InventoryData GetInventorySkeleton(UUID userID);

        /// <summary>
        /// Called when we receive the client's initial XMLRPC login_to_simulator request message
        /// </summary>
        /// <param name="request">The XMLRPC request</param>
        /// <returns>The response to send</returns>
        public virtual XmlRpcResponse XmlRpcLoginMethod(XmlRpcRequest request)
        {
            // Temporary fix
            m_loginMutex.WaitOne();
            try
            {
                //CFK: CustomizeResponse contains sufficient strings to alleviate the need for this.
                //CKF: m_log.Info("[LOGIN]: Attempting login now...");
                XmlRpcResponse response = new XmlRpcResponse();
                Hashtable requestData = (Hashtable) request.Params[0];

                bool GoodXML = (requestData.Contains("first") && requestData.Contains("last") &&
                                (requestData.Contains("passwd") || requestData.Contains("web_login_key")));
                bool GoodLogin = false;

                string startLocationRequest = "last";

                UserProfileData userProfile;
                LoginResponse logResponse = new LoginResponse();

                string firstname;
                string lastname;

                if (GoodXML)
                {
                    firstname = (string) requestData["first"];
                    lastname = (string) requestData["last"];

                    m_log.InfoFormat(
                        "[LOGIN BEGIN]: XMLRPC Received login request message from user '{0}' '{1}'",
                        firstname, lastname);

                    string clientVersion = "Unknown";

                    if (requestData.Contains("version"))
                    {
                        clientVersion = (string)requestData["version"];
                    }

                    if (requestData.Contains("start"))
                    {
                        startLocationRequest = (string)requestData["start"];
                    }

                    m_log.DebugFormat(
                        "[LOGIN]: XMLRPC Client is {0}, start location is {1}", clientVersion, startLocationRequest);

                    userProfile = GetTheUser(firstname, lastname);
                    if (userProfile == null)
                    {
                        m_log.Info("[LOGIN END]: XMLRPC Could not find a profile for " + firstname + " " + lastname);

                        return logResponse.CreateLoginFailedResponse();
                    }

                    if (requestData.Contains("passwd"))
                    {
                        string passwd = (string)requestData["passwd"];
                        GoodLogin = AuthenticateUser(userProfile, passwd);
                    }
                    else if (requestData.Contains("web_login_key"))
                    {
                        UUID webloginkey = UUID.Zero;
                        try
                        {
                            webloginkey = new UUID((string)requestData["web_login_key"]);
                        }
                        catch (Exception e)
                        {
                            m_log.InfoFormat(
                                "[LOGIN END]: XMLRPC  Bad web_login_key: {0} for user {1} {2}, exception {3}",
                                requestData["web_login_key"], firstname, lastname, e);

                            return logResponse.CreateFailedResponse();
                        }
                        GoodLogin = AuthenticateUser(userProfile, webloginkey);

                    }
                }
                else
                {
                    m_log.Info(
                        "[LOGIN END]: XMLRPC  login_to_simulator login message did not contain all the required data");

                    return logResponse.CreateGridErrorResponse();
                }

                if (!GoodLogin)
                {
                    m_log.InfoFormat("[LOGIN END]: XMLRPC  User {0} {1} failed authentication", firstname, lastname);

                    return logResponse.CreateLoginFailedResponse();
                }
                else if (userProfile.GodLevel < m_minLoginLevel)
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

                        // Reject the login

                        m_log.InfoFormat(
                            "[LOGIN END]:  XMLRPC Notifying user {0} {1} that they are already logged in",
                            firstname, lastname);

                        return logResponse.CreateAlreadyLoggedInResponse();
                    }
                    // Otherwise...
                    // Create a new agent session

                    m_userManager.ResetAttachments(userProfile.ID);

                    CreateAgent(userProfile, request);

                    try
                    {
                        UUID agentID = userProfile.ID;
                        InventoryData inventData;

                        try
                        {
                            inventData = GetInventorySkeleton(agentID);
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat(
                                "[LOGIN END]: Error retrieving inventory skeleton of agent {0} - {1}",
                                agentID, e);

                            return logResponse.CreateLoginInventoryFailedResponse();
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

                        logResponse.CircuitCode = Util.RandomClass.Next();
                        logResponse.Lastname = userProfile.SurName;
                        logResponse.Firstname = userProfile.FirstName;
                        logResponse.AgentID = agentID;
                        logResponse.SessionID = userProfile.CurrentAgent.SessionID;
                        logResponse.SecureSessionID = userProfile.CurrentAgent.SecureSessionID;
                        logResponse.Message = GetMessage();
                        logResponse.BuddList = ConvertFriendListItem(m_userManager.GetUserFriendList(agentID));
                        logResponse.StartLocation = startLocationRequest;

                        if (CustomiseResponse(logResponse, userProfile, startLocationRequest))
                        {
                            userProfile.LastLogin = userProfile.CurrentAgent.LoginTime;
                            CommitAgent(ref userProfile);

                            // If we reach this point, then the login has successfully logged onto the grid
                            if (StatsManager.UserStats != null)
                                StatsManager.UserStats.AddSuccessfulLogin();

                            m_log.DebugFormat(
                                "[LOGIN END]:  XMLRPC Authentication of user {0} {1} successful.  Sending response to client.",
                                firstname, lastname);

                            return logResponse.ToXmlRpcResponse();
                        }
                        else
                        {
                            m_log.ErrorFormat("[LOGIN END]:  XMLRPC informing user {0} {1} that login failed due to an unavailable region", firstname, lastname);
                            return logResponse.CreateDeadRegionResponse();
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Info("[LOGIN END]:  XMLRPC Login failed, " + e);
                    }
                }

                m_log.Info("[LOGIN END]:  XMLRPC Login failed.  Sending back blank XMLRPC response");
                return response;
            }
            finally
            {
                m_loginMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Called when we receive the client's initial LLSD login_to_simulator request message
        /// </summary>
        /// <param name="request">The LLSD request</param>
        /// <returns>The response to send</returns>
        public OSD LLSDLoginMethod(OSD request)
        {
            // Temporary fix
            m_loginMutex.WaitOne();

            try
            {
                bool GoodLogin = false;

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
                        m_log.Info("[LOGIN]: LLSD Login Requested for: '" + firstname+"' '"+lastname+"' / "+passwd);

                        userProfile = GetTheUser(firstname, lastname);
                        if (userProfile == null)
                        {
                            m_log.Info("[LOGIN]:  LLSD Could not find a profile for " + firstname + " " + lastname);

                            return logResponse.CreateLoginFailedResponseLLSD();
                        }

                        GoodLogin = AuthenticateUser(userProfile, passwd);
                    }
                }

                if (!GoodLogin)
                {
                    return logResponse.CreateLoginFailedResponseLLSD();
                }
                else if (userProfile.GodLevel < m_minLoginLevel)
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

                        // Reject the login

                        m_log.InfoFormat(
                            "[LOGIN END]:  LLSD Notifying user {0} {1} that they are already logged in",
                            userProfile.FirstName, userProfile.SurName);

                        userProfile.CurrentAgent = null;
                        return logResponse.CreateAlreadyLoggedInResponseLLSD();
                    }

                    // Otherwise...
                    // Create a new agent session

                    m_userManager.ResetAttachments(userProfile.ID);

                    CreateAgent(userProfile, request);

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
                            CustomiseResponse(logResponse, userProfile, startLocationRequest);
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
            string region =String.Empty;
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
                password = wfcut.Replace((string)keysvals["password"],  String.Empty, 99999);

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
                    statuscode = 301;

                    string redirectURL = "about:blank?redirect-http-hack=" +
                                         HttpUtility.UrlEncode("secondlife:///app/login?first_name=" + firstname + "&last_name=" +
                                                               lastname +
                                                               "&location=" + location + "&grid=Other&web_login_key=" + webloginkey.ToString());
                    //m_log.Info("[WEB]: R:" + redirectURL);
                    returnactions["int_response_code"] = statuscode;
                    returnactions["str_redirect_location"] = redirectURL;
                    returnactions["str_response_string"] = "<HTML><BODY>GoodLogin</BODY></HTML>";
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

            string loginform=String.Empty;
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

            responseString += "<div id=\"helplinks\">";
            responseString += "<a href=\"#join now link\" target=\"_blank\"></a> | ";
            responseString += "<a href=\"#forgot password link\" target=\"_blank\"></a>";
            responseString += "</div>";

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
                               || profile.PasswordHash.Equals(password, StringComparison.InvariantCultureIgnoreCase));

            return passwordSuccess;
        }

        public virtual bool AuthenticateUser(UserProfileData profile, UUID webloginkey)
        {
            bool passwordSuccess = false;
            m_log.InfoFormat("[LOGIN]: Authenticating {0} {1} ({2})", profile.FirstName, profile.SurName, profile.ID);

            // Match web login key unless it's the default weblogin key UUID.Zero
            passwordSuccess = ((profile.WebLoginKey==webloginkey) && profile.WebLoginKey != UUID.Zero);

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
                buddyitem.BuddyRightsGiven = (int) fl.FriendPerms;
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
    }
}
