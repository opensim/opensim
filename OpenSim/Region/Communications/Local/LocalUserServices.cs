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
        private readonly CommunicationsLocal m_parent;

        private readonly NetworkServersInfo m_serversInfo;
        private readonly uint m_defaultHomeX ;
        private readonly uint m_defaultHomeY;


        public LocalUserServices(CommunicationsLocal parent, NetworkServersInfo serversInfo)
        {
            m_parent = parent;
            m_serversInfo = serversInfo;

            m_defaultHomeX = this.m_serversInfo.DefaultHomeLocX;
            m_defaultHomeY = this.m_serversInfo.DefaultHomeLocY;
        }

        public UserProfileData SetupMasterUser(string firstName, string lastName)
        {
            return SetupMasterUser(firstName, lastName, "");
        }

        public UserProfileData SetupMasterUser(string firstName, string lastName, string password)
        {
            UserProfileData profile = base.GetUserProfile(firstName, lastName);
            if (profile != null)
            {

                return profile;
            }

            Console.WriteLine("Unknown Master User. Sandbox Mode: Creating Account");
            this.AddUserProfile(firstName, lastName, password, m_defaultHomeX, m_defaultHomeY);

            profile = base.GetUserProfile(firstName, lastName);

            if (profile == null)
            {
                Console.WriteLine("Unknown Master User after creation attempt. No clue what to do here.");
            }
            else
            {
                 m_parent.InvenServices.CreateNewUserInventory(profile.UUID);
            }

            return profile;
        }
    }
}
