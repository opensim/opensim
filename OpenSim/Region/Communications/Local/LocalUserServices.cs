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
        private readonly CommunicationsLocal m_Parent;

        private readonly NetworkServersInfo serversInfo;
        private readonly uint defaultHomeX ;
        private readonly uint defaultHomeY;


        public LocalUserServices(CommunicationsLocal parent, NetworkServersInfo serversInfo)
        {
            m_Parent = parent;
            this.serversInfo = serversInfo;

            defaultHomeX = this.serversInfo.DefaultHomeLocX;
            defaultHomeY = this.serversInfo.DefaultHomeLocY;
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
            this.AddUserProfile(firstName, lastName, password, defaultHomeX, defaultHomeY);

            profile = base.GetUserProfile(firstName, lastName);

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
