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
using System.Reflection;
using System.Text.RegularExpressions;
using libsecondlife;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Communications;

namespace OpenSim.Region.Communications.Local
{
    public delegate void LoginToRegionEvent(ulong regionHandle, Login login);

    public class LocalLoginService : LoginService
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private CommunicationsLocal m_Parent;

        private NetworkServersInfo serversInfo;
        private uint defaultHomeX;
        private uint defaultHomeY;
        private bool authUsers = false;

        public event LoginToRegionEvent OnLoginToRegion;

        private LoginToRegionEvent handlerLoginToRegion = null; // OnLoginToRegion;

        public LocalLoginService(UserManagerBase userManager, string welcomeMess,
                                 CommunicationsLocal parent, NetworkServersInfo serversInfo,
                                 bool authenticate)
            : base(userManager, parent.UserProfileCacheService.libraryRoot, welcomeMess)
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
                m_log.Info("[LOGIN]: No user account found so creating a new one.");

                m_userManager.AddUserProfile(firstname, lastname, "test", defaultHomeX, defaultHomeY);

                profile = m_userManager.GetUserProfile(firstname, lastname);
                if (profile != null)
                {
                    m_Parent.InterGridInventoryService.CreateNewUserInventory(profile.ID);
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
                m_log.Info("[LOGIN]: Authorising user (no actual password check)");

                return true;
            }
            else
            {
                m_log.Info(
                    "[LOGIN]: Authenticating " + profile.FirstName + " " + profile.SurName);

                if (!password.StartsWith("$1$"))
                    password = "$1$" + Util.Md5Hash(password);

                password = password.Remove(0, 3); //remove $1$

                string s = Util.Md5Hash(password + ":" + profile.PasswordSalt);

                bool loginresult = (profile.PasswordHash.Equals(s.ToString(), StringComparison.InvariantCultureIgnoreCase)
                            || profile.PasswordHash.Equals(password, StringComparison.InvariantCultureIgnoreCase));
                return loginresult;
            }
        }

        private Regex reURI = new Regex(@"^uri:(?<region>[^&]+)&(?<x>\d+)&(?<y>\d+)&(?<z>\d+)$");

        public override void CustomiseResponse(LoginResponse response, UserProfileData theUser, string startLocationRequest)
        {
            ulong currentRegion = 0;

            uint locX = 0;
            uint locY = 0;
            uint locZ = 0;
            bool specificStartLocation = false;

            // get start location
            if (startLocationRequest == "last")
            {
                currentRegion = theUser.CurrentAgent.Handle;
            }
            else if (startLocationRequest == "home")
            {
                currentRegion = theUser.HomeRegion;
            }
            else
            {
                // use last location as default
                currentRegion = theUser.CurrentAgent.Handle;

                Match uriMatch = reURI.Match(startLocationRequest);
                if (null == uriMatch)
                {
                    m_log.InfoFormat("[LOGIN]: Got Custom Login URL {0}, but can't process it", startLocationRequest);
                }
                else
                {
                    string region = uriMatch.Groups["region"].ToString();

                    RegionInfo r = m_Parent.GridService.RequestClosestRegion(region);
                    if (null == r)
                    {
                        m_log.InfoFormat("[LOGIN]: Got Custom Login URL {0}, can't locate region {1}",
                                         startLocationRequest, region);
                    }
                    else
                    {
                        currentRegion = r.RegionHandle;
                        locX = UInt32.Parse(uriMatch.Groups["x"].ToString());
                        locY = UInt32.Parse(uriMatch.Groups["y"].ToString());
                        locZ = UInt32.Parse(uriMatch.Groups["z"].ToString());
                        specificStartLocation = true;
                    }
                }
            }

            RegionInfo homeReg = m_Parent.GridService.RequestNeighbourInfo(theUser.HomeRegion);
            RegionInfo reg = m_Parent.GridService.RequestNeighbourInfo(currentRegion);

            if ((homeReg != null) && (reg != null))
            {
                response.Home = "{'region_handle':[r" +
                    (homeReg.RegionLocX * Constants.RegionSize).ToString() + ",r" +
                    (homeReg.RegionLocY * Constants.RegionSize).ToString() + "], " +
                    "'position':[r" +
                    theUser.HomeLocation.X.ToString() + ",r" +
                    theUser.HomeLocation.Y.ToString() + ",r" +
                    theUser.HomeLocation.Z.ToString() + "], " +
                    "'look_at':[r" +
                    theUser.HomeLocation.X.ToString() + ",r" +
                    theUser.HomeLocation.Y.ToString() + ",r" +
                    theUser.HomeLocation.Z.ToString() + "]}";
                string capsPath = Util.GetRandomCapsPath();
                response.SimAddress = reg.ExternalEndPoint.Address.ToString();
                response.SimPort = (uint) reg.ExternalEndPoint.Port;
                response.RegionX = reg.RegionLocX;
                response.RegionY = reg.RegionLocY;

                m_log.DebugFormat(
                    "[CAPS][LOGIN]: RegionX {0} RegionY {0}", response.RegionX, response.RegionY);

                // can be: last, home, safe, url
                if (specificStartLocation) response.StartLocation = "url";

                response.SeedCapability = "http://" + reg.ExternalHostName + ":" +
                                          serversInfo.HttpListenerPort.ToString() + "/CAPS/" + capsPath + "0000/";

                m_log.DebugFormat(
                    "[CAPS]: Sending new CAPS seed url {0} to client {1}",
                    response.SeedCapability, response.AgentID);

                theUser.CurrentAgent.Region = reg.RegionID;
                theUser.CurrentAgent.Handle = reg.RegionHandle;

                // LoginResponse.BuddyList buddyList = new LoginResponse.BuddyList();

                response.BuddList = ConvertFriendListItem(m_userManager.GetUserFriendList(theUser.ID));

                Login _login = new Login();
                //copy data to login object
                _login.First = response.Firstname;
                _login.Last = response.Lastname;
                _login.Agent = response.AgentID;
                _login.Session = response.SessionID;
                _login.SecureSession = response.SecureSessionID;
                _login.CircuitCode = (uint) response.CircuitCode;
                if (specificStartLocation)
                    _login.StartPos = new LLVector3(locX, locY, locZ);
                else
                    _login.StartPos = new LLVector3(128, 128, 128);
                _login.CapsPath = capsPath;

                m_log.InfoFormat(
                    "[LOGIN]: Telling region {0} @ {1},{2} ({3}:{4}) to expect user connection",
                    reg.RegionName, response.RegionX, response.RegionY, response.SimAddress, response.SimPort);

                handlerLoginToRegion = OnLoginToRegion;
                if (handlerLoginToRegion != null)
                {
                    handlerLoginToRegion(currentRegion, _login);
                }
            }
            else
            {
                m_log.Warn("[LOGIN]: Not found region " + currentRegion);
            }
        }

        private LoginResponse.BuddyList ConvertFriendListItem(List<FriendListItem> LFL)
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

        // See LoginService
        protected override InventoryData GetInventorySkeleton(LLUUID userID, string serverUrl)
        {
            List<InventoryFolderBase> folders = m_Parent.InterGridInventoryService.GetInventorySkeleton(userID);

            // If we have user auth but no inventory folders for some reason, create a new set of folders.
            if (null == folders || 0 == folders.Count)
            {
                m_Parent.InterGridInventoryService.CreateNewUserInventory(userID);
                folders = m_Parent.InterGridInventoryService.GetInventorySkeleton(userID);
            }

            LLUUID rootID = LLUUID.Zero;
            ArrayList AgentInventoryArray = new ArrayList();
            Hashtable TempHash;
            foreach (InventoryFolderBase InvFolder in folders)
            {
                if (InvFolder.ParentID == LLUUID.Zero)
                {
                    rootID = InvFolder.ID;
                }
                TempHash = new Hashtable();
                TempHash["name"] = InvFolder.Name;
                TempHash["parent_id"] = InvFolder.ParentID.ToString();
                TempHash["version"] = (Int32) InvFolder.Version;
                TempHash["type_default"] = (Int32) InvFolder.Type;
                TempHash["folder_id"] = InvFolder.ID.ToString();
                AgentInventoryArray.Add(TempHash);
            }

            return new InventoryData(AgentInventoryArray, rootID);
        }
    }
}
