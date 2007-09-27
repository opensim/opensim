using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework.Console;
using OpenSim.Framework.Data;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Configuration;


namespace OpenSim.Framework.UserManagement
{
    public class LoginService
    {
        protected string m_welcomeMessage = "Welcome to OpenSim";
        protected UserManagerBase m_userManager = null;
        protected IInventoryServices m_inventoryServer = null;

        public LoginService(UserManagerBase userManager, IInventoryServices inventoryServer, string welcomeMess)
        {
            m_userManager = userManager;
            m_inventoryServer = inventoryServer;
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
            System.Console.WriteLine("Attempting login now...");
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            bool GoodXML = (requestData.Contains("first") && requestData.Contains("last") && requestData.Contains("passwd"));
            bool GoodLogin = false;

            UserProfileData userProfile;
            LoginResponse logResponse = new LoginResponse();

            if (GoodXML)
            {
                string firstname = (string)requestData["first"];
                string lastname = (string)requestData["last"];
                string passwd = (string)requestData["passwd"];

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
                CreateAgent(userProfile, request);

                try
                {
                    LLUUID agentID = userProfile.UUID;

                    LLUUID libraryFolderID;
                    LLUUID personalFolderID;

                    m_inventoryServer.GetRootFoldersForUser(agentID, out libraryFolderID, out personalFolderID);
                    if (personalFolderID == LLUUID.Zero)
                    {
                        m_inventoryServer.CreateNewUserInventory(libraryFolderID, agentID);
                        m_inventoryServer.GetRootFoldersForUser(agentID, out libraryFolderID, out personalFolderID);
                    }

                    // The option "inventory-lib-owner" requires that we return the id of the 
                    // owner of the library inventory.
                    Hashtable dynamicStruct = new Hashtable();
                    dynamicStruct["agent_id"] = libraryFolderID.ToStringHyphenated();
                    logResponse.InventoryLibraryOwner.Add(dynamicStruct);

                    // The option "inventory-lib-root" requires that we return the id of the 
                    // root folder of the library inventory.
                    dynamicStruct = new Hashtable();
                    dynamicStruct["folder_id"] = libraryFolderID.ToStringHyphenated();
                    logResponse.InventoryLibraryRoot.Add(dynamicStruct);

                    // The option "inventory-root" requires that we return the id of the 
                    // root folder of the users inventory.
                    dynamicStruct = new Hashtable();
                    dynamicStruct["folder_id"] = personalFolderID.ToStringHyphenated();
                    logResponse.InventoryRoot.Add(dynamicStruct);

                    // The option "inventory-skeleton" requires that we return the structure of the
                    // users folder hierachy
                    logResponse.InventorySkeleton = GetInventorySkeleton(personalFolderID);

                    // The option "inventory-skel-lib" requires that we return the structure of the
                    // library folder hierachy
                    logResponse.InventoryLibrarySkeleton = GetInventorySkeleton(libraryFolderID);

                    // Circuit Code
                    uint circode = (uint)(Util.RandomClass.Next());

                    logResponse.Lastname = userProfile.surname;
                    logResponse.Firstname = userProfile.username;
                    logResponse.AgentID = agentID.ToStringHyphenated();
                    logResponse.SessionID = userProfile.currentAgent.sessionID.ToStringHyphenated();
                    logResponse.SecureSessionID = userProfile.currentAgent.secureSessionID.ToStringHyphenated();
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
                        this.CustomiseResponse(logResponse, userProfile);
                    }
                    catch (Exception e)
                    {
                        System.Console.WriteLine(e.ToString());
                        return logResponse.CreateDeadRegionResponse();
                        //return logResponse.ToXmlRpcResponse();
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
            // Saves the agent to database
            return true;
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
                "Authenticating " + profile.username + " " + profile.surname);

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
            this.m_userManager.CreateAgent(profile, request);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="firstname"></param>
        /// <param name="lastname"></param>
        /// <returns></returns>
        public virtual UserProfileData GetTheUser(string firstname, string lastname)
        {
            return this.m_userManager.GetUserProfile(firstname, lastname);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual string GetMessage()
        {
            return m_welcomeMessage;
        }

        /// <summary>
        /// Create a structure of the generic inventory structure of a specified folder
        /// </summary>
        /// <returns></returns>
        protected virtual ArrayList GetInventorySkeleton(LLUUID folderID)
        {

            List<InventoryFolderBase> folders = m_inventoryServer.RequestFirstLevelFolders(folderID);

            ArrayList temp = new ArrayList();
            foreach (InventoryFolderBase ifb in folders)
            {
                LLUUID tempFolderID = ifb.folderID;
                LLUUID tempParentID = ifb.parentID;

                Hashtable TempHash = new Hashtable();
                TempHash["folder_id"] = tempFolderID.ToStringHyphenated();
                TempHash["name"] = ifb.name;
                TempHash["parent_id"] = tempParentID.ToStringHyphenated();
                TempHash["type_default"] = (Int32)ifb.type;
                TempHash["version"] = (Int32)ifb.version+1;
                temp.Add(TempHash);
            }

            return temp;
        }
    }
}
