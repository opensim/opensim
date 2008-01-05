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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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
using System.Threading;
using libsecondlife;
using libsecondlife.StructuredData;
using Nwc.XmlRpc;

using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.UserManagement
{
    public class LoginService
    {
        protected string m_welcomeMessage = "Welcome to OpenSim";
        protected UserManagerBase m_userManager = null;
        protected Mutex m_loginMutex = new Mutex(false);
        
        /// <summary>
        /// Used during login to send the skeleton of the OpenSim Library to the client.
        /// </summary>
        protected LibraryRootFolder m_libraryRootFolder;

        public LoginService(
            UserManagerBase userManager, LibraryRootFolder libraryRootFolder, string welcomeMess)
        {
            m_userManager = userManager;
            m_libraryRootFolder = libraryRootFolder;
            
            if (welcomeMess != "")
            {
                m_welcomeMessage = welcomeMess;
            }
        }

        /// <summary>
        /// Main user login function
        /// </summary>
        /// <param name="request">The XMLRPC request</param>
        /// <returns>The response to send</returns>
        public XmlRpcResponse XmlRpcLoginMethod(XmlRpcRequest request)
        {
            // Temporary fix
            m_loginMutex.WaitOne();
            try
            {
                //CFK: CustomizeResponse contains sufficient strings to alleviate the need for this.
                //CKF: MainLog.Instance.Verbose("LOGIN", "Attempting login now...");
                XmlRpcResponse response = new XmlRpcResponse();
                Hashtable requestData = (Hashtable) request.Params[0];

                bool GoodXML = (requestData.Contains("first") && requestData.Contains("last") &&
                                requestData.Contains("passwd"));
                bool GoodLogin = false;

                UserProfileData userProfile;
                LoginResponse logResponse = new LoginResponse();

                if (GoodXML)
                {
                    string firstname = (string) requestData["first"];
                    string lastname = (string) requestData["last"];
                    string passwd = (string) requestData["passwd"];

                    userProfile = GetTheUser(firstname, lastname);
                    if (userProfile == null)
                    {
                        MainLog.Instance.Verbose(
                            "LOGIN",
                            "Could not find a profile for " + firstname + " " + lastname);

                        return logResponse.CreateLoginFailedResponse();
                    }

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
                        userProfile.currentAgent = null;
                        m_userManager.CommitAgent(ref userProfile);

                        // Reject the login
                        return logResponse.CreateAlreadyLoggedInResponse();
                    }
                    // Otherwise...
                    // Create a new agent session
                    CreateAgent(userProfile, request);

                    try
                    {
                        LLUUID agentID = userProfile.UUID;

                        // Inventory Library Section
                        InventoryData inventData = CreateInventoryData(agentID);
                        ArrayList AgentInventoryArray = inventData.InventoryArray;

                        Hashtable InventoryRootHash = new Hashtable();
                        InventoryRootHash["folder_id"] = inventData.RootFolderID.ToString();
                        ArrayList InventoryRoot = new ArrayList();
                        InventoryRoot.Add(InventoryRootHash);
                        userProfile.rootInventoryFolderID = inventData.RootFolderID;

                        // Circuit Code
                        uint circode = (uint) (Util.RandomClass.Next());

                        logResponse.Lastname = userProfile.surname;
                        logResponse.Firstname = userProfile.username;
                        logResponse.AgentID = agentID.ToString();
                        logResponse.SessionID = userProfile.currentAgent.sessionID.ToString();
                        logResponse.SecureSessionID = userProfile.currentAgent.secureSessionID.ToString();
                        logResponse.InventoryRoot = InventoryRoot;
                        logResponse.InventorySkeleton = AgentInventoryArray;
                        logResponse.InventoryLibrary = GetInventoryLibrary();

                        Hashtable InventoryLibRootHash = new Hashtable();
                        InventoryLibRootHash["folder_id"] = "00000112-000f-0000-0000-000100bba000";
                        ArrayList InventoryLibRoot = new ArrayList();
                        InventoryLibRoot.Add(InventoryLibRootHash);
                        logResponse.InventoryLibRoot = InventoryLibRoot;

                        logResponse.InventoryLibraryOwner = GetLibraryOwner();
                        logResponse.CircuitCode = (Int32) circode;
                        //logResponse.RegionX = 0; //overwritten
                        //logResponse.RegionY = 0; //overwritten
                        logResponse.Home = "!!null temporary value {home}!!"; // Overwritten
                        //logResponse.LookAt = "\n[r" + TheUser.homeLookAt.X.ToString() + ",r" + TheUser.homeLookAt.Y.ToString() + ",r" + TheUser.homeLookAt.Z.ToString() + "]\n";
                        //logResponse.SimAddress = "127.0.0.1"; //overwritten
                        //logResponse.SimPort = 0; //overwritten
                        logResponse.Message = GetMessage();
                        logResponse.BuddList = ConvertFriendListItem(m_userManager.GetUserFriendList(agentID)); 

                        try
                        {
                            CustomiseResponse(logResponse, userProfile);
                        }
                        catch (Exception e)
                        {
                            MainLog.Instance.Verbose("LOGIN", e.ToString());
                            return logResponse.CreateDeadRegionResponse();
                            //return logResponse.ToXmlRpcResponse();
                        }
                        CommitAgent(ref userProfile);
                        return logResponse.ToXmlRpcResponse();
                    }

                    catch (Exception E)
                    {
                        MainLog.Instance.Verbose("LOGIN", E.ToString());
                    }
                    //}
                }
                return response;
            }
            finally
            {
                m_loginMutex.ReleaseMutex();
            }
        }

        public LLSD LLSDLoginMethod(LLSD request)
        {
            // Temporary fix
            m_loginMutex.WaitOne();

            try
            {
                bool GoodLogin = false;

                UserProfileData userProfile = null;
                LoginResponse logResponse = new LoginResponse();

                if (request.Type == LLSDType.Map)
                {
                    LLSDMap map = (LLSDMap)request;

                    if (map.ContainsKey("first") && map.ContainsKey("last") && map.ContainsKey("passwd"))
                    {
                        string firstname = map["first"].AsString();
                        string lastname = map["last"].AsString();
                        string passwd = map["passwd"].AsString();

                        userProfile = GetTheUser(firstname, lastname);
                        if (userProfile == null)
                        {
                            MainLog.Instance.Verbose(
                                "LOGIN",
                                "Could not find a profile for " + firstname + " " + lastname);

                            return logResponse.CreateLoginFailedResponseLLSD();
                        }

                        GoodLogin = AuthenticateUser(userProfile, passwd);
                    }
                }

                if (!GoodLogin)
                {
                    return logResponse.CreateLoginFailedResponseLLSD();
                }
                else
                {
                    // If we already have a session...
                    if (userProfile.currentAgent != null && userProfile.currentAgent.agentOnline)
                    {
                        userProfile.currentAgent = null;
                        m_userManager.CommitAgent(ref userProfile);

                        // Reject the login
                        return logResponse.CreateAlreadyLoggedInResponseLLSD();
                    }

                    // Otherwise...
                    // Create a new agent session
                    CreateAgent(userProfile, request);

                    try
                    {
                        LLUUID agentID = userProfile.UUID;

                        // Inventory Library Section
                        InventoryData inventData = CreateInventoryData(agentID);
                        ArrayList AgentInventoryArray = inventData.InventoryArray;

                        Hashtable InventoryRootHash = new Hashtable();
                        InventoryRootHash["folder_id"] = inventData.RootFolderID.ToString();
                        ArrayList InventoryRoot = new ArrayList();
                        InventoryRoot.Add(InventoryRootHash);
                        userProfile.rootInventoryFolderID = inventData.RootFolderID;

                        // Circuit Code
                        uint circode = (uint)(Util.RandomClass.Next());

                        logResponse.Lastname = userProfile.surname;
                        logResponse.Firstname = userProfile.username;
                        logResponse.AgentID = agentID.ToString();
                        logResponse.SessionID = userProfile.currentAgent.sessionID.ToString();
                        logResponse.SecureSessionID = userProfile.currentAgent.secureSessionID.ToString();
                        logResponse.InventoryRoot = InventoryRoot;
                        logResponse.InventorySkeleton = AgentInventoryArray;
                        logResponse.InventoryLibrary = GetInventoryLibrary();

                        Hashtable InventoryLibRootHash = new Hashtable();
                        InventoryLibRootHash["folder_id"] = "00000112-000f-0000-0000-000100bba000";
                        ArrayList InventoryLibRoot = new ArrayList();
                        InventoryLibRoot.Add(InventoryLibRootHash);
                        logResponse.InventoryLibRoot = InventoryLibRoot;

                        logResponse.InventoryLibraryOwner = GetLibraryOwner();
                        logResponse.CircuitCode = (Int32)circode;
                        //logResponse.RegionX = 0; //overwritten
                        //logResponse.RegionY = 0; //overwritten
                        logResponse.Home = "!!null temporary value {home}!!"; // Overwritten
                        //logResponse.LookAt = "\n[r" + TheUser.homeLookAt.X.ToString() + ",r" + TheUser.homeLookAt.Y.ToString() + ",r" + TheUser.homeLookAt.Z.ToString() + "]\n";
                        //logResponse.SimAddress = "127.0.0.1"; //overwritten
                        //logResponse.SimPort = 0; //overwritten
                        logResponse.Message = GetMessage();
                        logResponse.BuddList = ConvertFriendListItem(m_userManager.GetUserFriendList(agentID));

                        try
                        {
                            CustomiseResponse(logResponse, userProfile);
                        }
                        catch (Exception ex)
                        {
                            MainLog.Instance.Verbose("LOGIN", ex.ToString());
                            return logResponse.CreateDeadRegionResponseLLSD();
                        }

                        CommitAgent(ref userProfile);

                        return logResponse.ToLLSDResponse();
                    }
                    catch (Exception ex)
                    {
                        MainLog.Instance.Verbose("LOGIN", ex.ToString());
                        return logResponse.CreateFailedResponseLLSD();
                    }
                }
            }
            finally
            {
                m_loginMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Customises the login response and fills in missing values.
        /// </summary>
        /// <param name="response">The existing response</param>
        /// <param name="theUser">The user profile</param>
        public virtual void CustomiseResponse(LoginResponse response, UserProfileData theUser)
        {
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
            MainLog.Instance.Verbose(
                "LOGIN", "Authenticating {0} {1} ({2})", profile.username, profile.surname, profile.UUID);

            password = password.Remove(0, 3); //remove $1$

            string s = Util.Md5Hash(password + ":" + profile.passwordSalt);

            return profile.passwordHash.Equals(s.ToString(), StringComparison.InvariantCultureIgnoreCase);
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

        public void CreateAgent(UserProfileData profile, LLSD request)
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

        private LoginResponse.BuddyList ConvertFriendListItem(List<FriendListItem> LFL)
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
            Dictionary<LLUUID, InventoryFolderImpl> rootFolders 
                = m_libraryRootFolder.RequestSelfAndDescendentFolders();
            ArrayList folderHashes = new ArrayList();
            
            foreach (InventoryFolderBase folder in rootFolders.Values)
            {
                Hashtable TempHash = new Hashtable();
                TempHash["name"] = folder.name;
                TempHash["parent_id"] = folder.parentID.ToString();
                TempHash["version"] = (Int32)folder.version;
                TempHash["type_default"] = (Int32)folder.type;
                TempHash["folder_id"] = folder.folderID.ToString();
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

        protected virtual InventoryData CreateInventoryData(LLUUID userID)
        {
            AgentInventory userInventory = new AgentInventory();
            userInventory.CreateRootFolder(userID, false);

            ArrayList AgentInventoryArray = new ArrayList();
            Hashtable TempHash;
            foreach (InventoryFolder InvFolder in userInventory.InventoryFolders.Values)
            {
                TempHash = new Hashtable();
                TempHash["name"] = InvFolder.FolderName;
                TempHash["parent_id"] = InvFolder.ParentID.ToString();
                TempHash["version"] = (Int32) InvFolder.Version;
                TempHash["type_default"] = (Int32) InvFolder.DefaultType;
                TempHash["folder_id"] = InvFolder.FolderID.ToString();
                AgentInventoryArray.Add(TempHash);
            }

            return new InventoryData(AgentInventoryArray, userInventory.InventoryRoot.FolderID);
        }

        public class InventoryData
        {
            public ArrayList InventoryArray = null;
            public LLUUID RootFolderID = LLUUID.Zero;

            public InventoryData(ArrayList invList, LLUUID rootID)
            {
                InventoryArray = invList;
                RootFolderID = rootID;
            }
        }
    }
}
