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
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.UserManagement;
using InventoryFolder=OpenSim.Framework.InventoryFolder;

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

        public LocalLoginService(UserManagerBase userManager, string welcomeMess, CommunicationsLocal parent,
                                 NetworkServersInfo serversInfo, bool authenticate)
            : base(userManager, welcomeMess)
        {
            m_Parent = parent;
            this.serversInfo = serversInfo;
            defaultHomeX = this.serversInfo.DefaultHomeLocX;
            defaultHomeY = this.serversInfo.DefaultHomeLocY;
            authUsers = authenticate;
        }


        public override UserProfileData GetTheUser(string firstname, string lastname)
        {
            UserProfileData profile = m_userManager.GetUserProfile(firstname, lastname);
            if (profile != null)
            {
                return profile;
            }

            if (!authUsers)
            {
                //no current user account so make one
                MainLog.Instance.Notice("LOGIN", "No user account found so creating a new one.");

                m_userManager.AddUserProfile(firstname, lastname, "test", defaultHomeX, defaultHomeY);

                profile = m_userManager.GetUserProfile(firstname, lastname);
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
                MainLog.Instance.Notice("LOGIN", "Authorising user (no actual password check)");

                return true;
            }
            else
            {
                MainLog.Instance.Notice(
                    "LOGIN", "Authenticating " + profile.username + " " + profile.surname);

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
                response.Home = "{'region_handle':[r" + (reg.RegionLocX*256).ToString() + ",r" +
                                (reg.RegionLocY*256).ToString() + "], " +
                                "'position':[r" + theUser.homeLocation.X.ToString() + ",r" +
                                theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "], " +
                                "'look_at':[r" + theUser.homeLocation.X.ToString() + ",r" +
                                theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "]}";
                string capsPath = Util.GetRandomCapsPath();
                response.SimAddress = reg.ExternalEndPoint.Address.ToString();
                response.SimPort = (uint) reg.ExternalEndPoint.Port;
                response.RegionX = reg.RegionLocX;
                response.RegionY = reg.RegionLocY;

                response.SeedCapability = "http://" + reg.ExternalHostName + ":" +
                                          serversInfo.HttpListenerPort.ToString() + "/CAPS/" + capsPath + "0000/";
                // response.SeedCapability = "http://" + reg.ExternalHostName + ":" + this.serversInfo.HttpListenerPort.ToString() + "/CapsSeed/" + capsPath + "0000/";
                theUser.currentAgent.currentRegion = reg.RegionID;
                theUser.currentAgent.currentHandle = reg.RegionHandle;

                LoginResponse.BuddyList buddyList = new LoginResponse.BuddyList();
                buddyList.AddNewBuddy(new LoginResponse.BuddyList.BuddyInfo("11111111-1111-0000-0000-000100bba000"));
                response.BuddList = buddyList;

                Login _login = new Login();
                //copy data to login object
                _login.First = response.Firstname;
                _login.Last = response.Lastname;
                _login.Agent = response.AgentID;
                _login.Session = response.SessionID;
                _login.SecureSession = response.SecureSessionID;
                _login.CircuitCode = (uint) response.CircuitCode;
                _login.StartPos = new LLVector3(128, 128, 70);
                _login.CapsPath = capsPath;

                if (OnLoginToRegion != null)
                {
                    OnLoginToRegion(currentRegion, _login);
                }
            }
            else
            {
                MainLog.Instance.Warn("LOGIN", "Not found region " + currentRegion);
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
                    TempHash["parent_id"] = InvFolder.parentID.ToString();
                    TempHash["version"] = (Int32) InvFolder.version;
                    TempHash["type_default"] = (Int32) InvFolder.type;
                    TempHash["folder_id"] = InvFolder.folderID.ToString();
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
        }
    }
}