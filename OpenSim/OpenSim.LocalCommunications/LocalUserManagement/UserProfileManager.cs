using System;
using System.Collections.Generic;
using System.Text;

using libsecondlife;
using OpenSim.Framework.User;

namespace OpenSim.LocalCommunications.LocalUserManagement
{
    public class UserProfileManager
    {
        Dictionary<LLUUID, UserProfile> userProfiles = new Dictionary<LLUUID, UserProfile>();

        public UserProfileManager()
        {
        }

        private LLUUID getUserUUID(string first_name, string last_name)
        {
            return getUserUUID(first_name + " " + last_name);
        }
        private LLUUID getUserUUID(string name)
        {
            return null;
        }


        public UserProfile getUserProfile(string first_name, string last_name)
        {
            return getUserProfile(first_name + " " + last_name);
        }
        public UserProfile getUserProfile(string name)
        {
            return null;
        }
        public UserProfile getUserProfile(LLUUID user_id)
        {
            return null;
        }

    }
}
