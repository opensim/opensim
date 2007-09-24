using System;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Data;
using OpenSim.Framework.Types;
using OpenSim.Framework.UserManagement;

namespace OpenSim.Region.Communications.Local
{
    public class LocalUserServices : UserManagerBase
    {
        private readonly CommunicationsLocal m_parent;

        private readonly NetworkServersInfo m_serversInfo;
        private readonly uint m_defaultHomeX;
        private readonly uint m_defaultHomeY;


        public LocalUserServices(CommunicationsLocal parent, NetworkServersInfo serversInfo)
        {
            m_parent = parent;
            m_serversInfo = serversInfo;

            m_defaultHomeX = m_serversInfo.DefaultHomeLocX;
            m_defaultHomeY = m_serversInfo.DefaultHomeLocY;
        }

        public override UserProfileData SetupMasterUser(string firstName, string lastName)
        {
            return SetupMasterUser(firstName, lastName, "");
        }

        public override UserProfileData SetupMasterUser(string firstName, string lastName, string password)
        {
            UserProfileData profile = GetUserProfile(firstName, lastName);
            if (profile != null)
            {
                return profile;
            }

            Console.WriteLine("Unknown Master User. Sandbox Mode: Creating Account");
            AddUserProfile(firstName, lastName, password, m_defaultHomeX, m_defaultHomeY);

            profile = GetUserProfile(firstName, lastName);

            if (profile == null)
            {
                Console.WriteLine("Unknown Master User after creation attempt. No clue what to do here.");
            }
            else
            {
                m_parent.InventoryService.CreateNewUserInventory(profile.UUID);
            }

            return profile;
        }
    }
}