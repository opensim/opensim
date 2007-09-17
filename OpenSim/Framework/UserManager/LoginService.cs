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
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Utilities;

using OpenSim.Framework.Configuration;
using InventoryFolder = OpenSim.Framework.Inventory.InventoryFolder;

namespace OpenSim.Framework.UserManagement
{
    public class LoginService
    {
        protected string m_welcomeMessage = "Welcome to OpenSim";
        protected UserManagerBase m_userManager = null;

        public LoginService(UserManagerBase userManager, string welcomeMess)
        {
            m_userManager = userManager;
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
                CreateAgent(userProfile, request);

                try
                {
                    LLUUID agentID = userProfile.UUID;

                    // Inventory Library Section
                    InventoryData inventData = this.CreateInventoryData(agentID);
                    ArrayList AgentInventoryArray = inventData.InventoryArray;

                    Hashtable InventoryRootHash = new Hashtable();
                    InventoryRootHash["folder_id"] = inventData.RootFolderID.ToStringHyphenated();
                    ArrayList InventoryRoot = new ArrayList();
                    InventoryRoot.Add(InventoryRootHash);
                    userProfile.rootInventoryFolderID = inventData.RootFolderID;

                    // Circuit Code
                    uint circode = (uint)(Util.RandomClass.Next());

                    logResponse.Lastname = userProfile.surname;
                    logResponse.Firstname = userProfile.username;
                    logResponse.AgentID = agentID.ToStringHyphenated();
                    logResponse.SessionID = userProfile.currentAgent.sessionID.ToStringHyphenated();
                    logResponse.SecureSessionID = userProfile.currentAgent.secureSessionID.ToStringHyphenated();
                    logResponse.InventoryRoot = InventoryRoot;
                    logResponse.InventorySkeleton = AgentInventoryArray;
                    logResponse.InventoryLibrary = this.GetInventoryLibrary();
                    logResponse.InventoryLibraryOwner = this.GetLibraryOwner();
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
            return this.m_userManager.getUserProfile(firstname, lastname);
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
        /// 
        /// </summary>
        /// <returns></returns>
        protected virtual ArrayList GetInventoryLibrary()
        {
            //return new ArrayList();
            Hashtable TempHash = new Hashtable();
            TempHash["name"] = "OpenSim Library";
            TempHash["parent_id"] = LLUUID.Zero.ToStringHyphenated();
            TempHash["version"] = 1;
            TempHash["type_default"] = -1;
            TempHash["folder_id"] = "00000112-000f-0000-0000-000100bba000";
            ArrayList temp = new ArrayList();
            temp.Add(TempHash);

            TempHash = new Hashtable();
            TempHash["name"] = "Texture Library";
            TempHash["parent_id"] = "00000112-000f-0000-0000-000100bba000";
            TempHash["version"] = 1;
            TempHash["type_default"] = -1;
            TempHash["folder_id"] = "00000112-000f-0000-0000-000100bba001";
            temp.Add(TempHash);
            return temp;
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
                TempHash["parent_id"] = InvFolder.ParentID.ToStringHyphenated();
                TempHash["version"] = (Int32)InvFolder.Version;
                TempHash["type_default"] = (Int32)InvFolder.DefaultType;
                TempHash["folder_id"] = InvFolder.FolderID.ToStringHyphenated();
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
