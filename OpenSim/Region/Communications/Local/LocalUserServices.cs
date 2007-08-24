using System;
using libsecondlife;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Data;
using OpenSim.Framework.Types;
using OpenSim.Framework.UserManagement;
using OpenSim.Framework.Utilities;

namespace OpenSim.Region.Communications.Local
{
    public class LocalUserServices : UserManagerBase, IUserServices
    {
        private CommunicationsLocal m_Parent;

        private NetworkServersInfo serversInfo;
        private uint defaultHomeX ;
        private uint defaultHomeY;


        public LocalUserServices(CommunicationsLocal parent, NetworkServersInfo serversInfo)
        {
            m_Parent = parent;
            this.serversInfo = serversInfo;
            defaultHomeX = this.serversInfo.DefaultHomeLocX;
            defaultHomeY = this.serversInfo.DefaultHomeLocY;
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
            else
            {
                 m_Parent.InvenServices.CreateNewUserInventory(profile.UUID);
            }

            return profile;
        }
    }
}
