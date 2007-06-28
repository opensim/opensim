using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Data;
using libsecondlife;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGSUserServices :IUserServices
    {
        public UserProfileData GetUserProfile(string firstName, string lastName)
        {
            return GetUserProfile(firstName + " " + lastName);
        }
        public UserProfileData GetUserProfile(string name)
        {
            return null;
        }
        public UserProfileData GetUserProfile(LLUUID avatarID)
        {
            return null;
        }

        public UserProfileData SetupMasterUser(string firstName, string lastName)
        {
            return SetupMasterUser(firstName, lastName, "");
        }

        public UserProfileData SetupMasterUser(string firstName, string lastName, string password)
        {
            UserProfileData profile = GetUserProfile(firstName, lastName);
            if (profile == null)
            {
                Console.WriteLine("Unknown Master User. Grid Mode: No clue what I should do. Probably would choose the grid owner UUID when that is implemented");
            }
            return null;
        }
    }
}
