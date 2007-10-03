using System;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Data;
using OpenSim.Framework.Types;
using OpenSim.Framework.UserManagement;

namespace OpenSim.Region.Communications.Local
{
    public class LocalUserServices : UserManagerBase
    {
        private readonly NetworkServersInfo m_serversInfo;
        private readonly uint m_defaultHomeX;
        private readonly uint m_defaultHomeY;
        private IInventoryServices m_inventoryService;


        public LocalUserServices(NetworkServersInfo serversInfo, uint defaultHomeLocX, uint defaultHomeLocY, IInventoryServices inventoryService)
        {
            m_serversInfo = serversInfo;

            m_defaultHomeX = defaultHomeLocX;
            m_defaultHomeY = defaultHomeLocY;

            m_inventoryService = inventoryService;
            
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
                m_inventoryService.CreateNewUserInventory(profile.UUID);
            }

            return profile;
        }
    }
}