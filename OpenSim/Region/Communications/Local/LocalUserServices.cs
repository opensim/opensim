using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework.Communications;
//using OpenSim.Framework.User;
using OpenSim.Framework.UserManagement;
using OpenSim.Framework.Data;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;

using libsecondlife;

namespace OpenSim.Region.Communications.Local
{
    public class LocalUserServices : UserManagerBase, IUserServices
    {
        private CommunicationsLocal m_Parent;

        private uint defaultHomeX ;
        private uint defaultHomeY;
        public LocalUserServices(CommunicationsLocal parent, uint defHomeX, uint defHomeY)
        {
            m_Parent = parent;
            defaultHomeX = defHomeX;
            defaultHomeY = defHomeY;
        }

        public UserProfileData GetUserProfile(string firstName, string lastName)
        {
            return GetUserProfile(firstName + " " + lastName);
        }

        public UserProfileData GetUserProfile(string name)
        {
            return this.getUserProfile(name);
        }
        public UserProfileData GetUserProfile(LLUUID avatarID)
        {
            return this.getUserProfile(avatarID);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string GetMessage()
        {
            return "Welcome to OpenSim";
        }

        public override UserProfileData GetTheUser(string firstname, string lastname)
        {
            UserProfileData profile = getUserProfile(firstname, lastname);
            if (profile != null)
            {
               
                return profile;
            }

            //no current user account so make one
            Console.WriteLine("No User account found so creating a new one ");
            this.AddUserProfile(firstname, lastname, "test", defaultHomeX, defaultHomeY);
            
            profile = getUserProfile(firstname, lastname);

            return profile;
        }

        public override bool AuthenticateUser(ref UserProfileData profile, string password)
        {
            //for now we will accept any password in sandbox mode
            Console.WriteLine("authorising user");
            return true;
        }

        public override void CustomiseResponse(ref LoginResponse response, ref UserProfileData theUser)
        {
            ulong currentRegion = theUser.currentAgent.currentHandle;
            RegionInfo reg = m_Parent.GridServer.RequestNeighbourInfo(currentRegion);


            if (reg != null)
            {
                response.Home = "{'region_handle':[r" + (reg.RegionLocX * 256).ToString() + ",r" + (reg.RegionLocY * 256).ToString() + "], " +
                 "'position':[r" + theUser.homeLocation.X.ToString() + ",r" + theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "], " +
                 "'look_at':[r" + theUser.homeLocation.X.ToString() + ",r" + theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "]}";
                string capsPath = Util.GetRandomCapsPath();
                response.SimAddress = reg.CommsIPListenAddr;
                response.SimPort = (Int32)reg.CommsIPListenPort;
                response.RegionX = reg.RegionLocX ;
                response.RegionY = reg.RegionLocY ;
                response.SeedCapability = "http://" + reg.CommsIPListenAddr + ":" + "9000" + "/CAPS/" + capsPath + "0000/";
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

                m_Parent.InformRegionOfLogin(currentRegion, _login);
            }
            else
            {
                Console.WriteLine("not found region " + currentRegion);
            }

        }

        public UserProfileData SetupMasterUser(string firstName, string lastName)
        {
            return SetupMasterUser(firstName, lastName, "");
        }
        public UserProfileData SetupMasterUser(string firstName, string lastName, string password)
        {
            UserProfileData profile = getUserProfile(firstName, lastName);
            if (profile != null)
            {

                return profile;
            }

            Console.WriteLine("Unknown Master User. Sandbox Mode: Creating Account");
            this.AddUserProfile(firstName, lastName, password, defaultHomeX, defaultHomeY);

            profile = getUserProfile(firstName, lastName);

            if (profile == null)
            {
                Console.WriteLine("Unknown Master User after creation attempt. No clue what to do here.");
            }

            return profile;
        }
    }
}
