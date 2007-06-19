using System;
using System.Collections.Generic;
using System.Text;

using OpenGrid.Framework.Communications;
using OpenSim.Framework.User;

using libsecondlife;

namespace OpenSim.LocalCommunications.LocalUserManagement
{
    public class LocalUserServices : IUserServices
    {
        public UserProfileManager userProfileManager = new UserProfileManager();
        public LocalLoginService localLoginService;
        public LocalUserServices()
        {
            localLoginService  = new LocalLoginService(this);
        }

        public UserProfile GetUserProfile(string first_name, string last_name)
        {
            return GetUserProfile(first_name + " " + last_name);
        }

        public UserProfile GetUserProfile(string name)
        {
            return null;
        }
        public UserProfile GetUserProfile(LLUUID avatar_id)
        {
            return null;
        }

    }
}
