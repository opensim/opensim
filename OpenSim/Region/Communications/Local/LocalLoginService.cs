using System;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Types;
using OpenSim.Framework.UserManagement;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Inventory;

namespace OpenSim.Region.Communications.Local
{
    public delegate void LoginToRegionEvent(ulong regionHandle, Login login);

    public class LocalLoginService : LoginService
    {
        private CommunicationsLocal m_Parent;

        private NetworkServersInfo serversInfo;
        private uint defaultHomeX;
        private uint defaultHomeY;
        private bool authUsers = false;

        public event LoginToRegionEvent OnLoginToRegion;

        public LocalLoginService(UserManagerBase userManager, string welcomeMess, CommunicationsLocal parent, NetworkServersInfo serversInfo, bool authenticate)
            : base(userManager, welcomeMess)
        {
            m_Parent = parent;
            this.serversInfo = serversInfo;
            defaultHomeX = this.serversInfo.DefaultHomeLocX;
            defaultHomeY = this.serversInfo.DefaultHomeLocY;
            this.authUsers = authenticate;
        }


        public override UserProfileData GetTheUser(string firstname, string lastname)
        {
            UserProfileData profile = this.m_userManager.GetUserProfile(firstname, lastname);
            if (profile != null)
            {

                return profile;
            }

            if (!authUsers)
            {
                //no current user account so make one
                Console.WriteLine("No User account found so creating a new one ");
                this.m_userManager.AddUserProfile(firstname, lastname, "test", defaultHomeX, defaultHomeY);

                profile = this.m_userManager.GetUserProfile(firstname, lastname);
                if (profile != null)
                {
                    m_Parent.InventoryService.CreateNewUserInventory(profile.UUID);
                }

                return profile;
            }
            return null;
        }

        public override bool AuthenticateUser(UserProfileData profile, string password)
        {
            if (!authUsers)
            {
                //for now we will accept any password in sandbox mode
                Console.WriteLine("authorising user");
                return true;
            }
            else
            {
                Console.WriteLine("Authenticating " + profile.username + " " + profile.surname);

                password = password.Remove(0, 3); //remove $1$

                string s = Util.Md5Hash(password + ":" + profile.passwordSalt);

                return profile.passwordHash.Equals(s.ToString(), StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public override void CustomiseResponse(LoginResponse response, UserProfileData theUser)
        {
            ulong currentRegion = theUser.currentAgent.currentHandle;
            RegionInfo reg = m_Parent.GridService.RequestNeighbourInfo(currentRegion);

            if (reg != null)
            {
                response.Home = "{'region_handle':[r" + (reg.RegionLocX * 256).ToString() + ",r" + (reg.RegionLocY * 256).ToString() + "], " +
                 "'position':[r" + theUser.homeLocation.X.ToString() + ",r" + theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "], " +
                 "'look_at':[r" + theUser.homeLocation.X.ToString() + ",r" + theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "]}";
                string capsPath = Util.GetRandomCapsPath();
                response.SimAddress = reg.ExternalEndPoint.Address.ToString();
                response.SimPort = (Int32)reg.ExternalEndPoint.Port;
                response.RegionX = reg.RegionLocX;
                response.RegionY = reg.RegionLocY;
                

                response.SeedCapability = "http://" + reg.ExternalHostName + ":" + this.serversInfo.HttpListenerPort.ToString() + "/CAPS/" + capsPath + "0000/";
               // response.SeedCapability = "http://" + reg.ExternalHostName + ":" + this.serversInfo.HttpListenerPort.ToString() + "/CapsSeed/" + capsPath + "0000/";
                theUser.currentAgent.currentRegion = reg.SimUUID;
                theUser.currentAgent.currentHandle = reg.RegionHandle;

                Login _login = new Login();
                //copy data to login object
                _login.First = response.Firstname;
                _login.Last = response.Lastname;
                _login.Agent = response.AgentID;
                _login.Session = response.SessionID;
                _login.SecureSession = response.SecureSessionID;
                _login.CircuitCode = (uint)response.CircuitCode;
                _login.CapsPath = capsPath;

                if( OnLoginToRegion != null )
                {
                    OnLoginToRegion(currentRegion, _login);
                }
            }
            else
            {
                Console.WriteLine("not found region " + currentRegion);
            }

        }

        protected override InventoryData CreateInventoryData(LLUUID userID)
        {
            List<InventoryFolderBase> folders = m_Parent.InventoryService.RequestFirstLevelFolders(userID);
            if (folders.Count > 0)
            {
                LLUUID rootID = LLUUID.Zero;
                ArrayList AgentInventoryArray = new ArrayList();
                Hashtable TempHash;
                foreach (InventoryFolderBase InvFolder in folders)
                {
                    if (InvFolder.parentID == LLUUID.Zero)
                    {
                        rootID = InvFolder.folderID;
                    }
                    TempHash = new Hashtable();
                    TempHash["name"] = InvFolder.name;
                    TempHash["parent_id"] = InvFolder.parentID.ToStringHyphenated();
                    TempHash["version"] = (Int32)InvFolder.version;
                    TempHash["type_default"] = (Int32)InvFolder.type;
                    TempHash["folder_id"] = InvFolder.folderID.ToStringHyphenated();
                    AgentInventoryArray.Add(TempHash);
                }
                return new InventoryData(AgentInventoryArray, rootID);
            }
            else
            {
                AgentInventory userInventory = new AgentInventory();
                userInventory.CreateRootFolder(userID, false);

                ArrayList AgentInventoryArray = new ArrayList();
                Hashtable TempHash;
                foreach (OpenSim.Framework.Inventory.InventoryFolder InvFolder in userInventory.InventoryFolders.Values)
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
        }
    }
}
