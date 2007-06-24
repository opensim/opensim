using System;
using System.Collections.Generic;
using System.Text;
using OpenGrid.Framework.Communications;
using OpenGrid.Framework.Data;
using libsecondlife;

namespace OpenGrid.Framework.Communications.OGS1
{
    public class OGSUserServices :IUserServices
    {
        public UserProfileData GetUserProfile(string firstName, string lastName)
        {
            return null;
        }
        public UserProfileData GetUserProfile(string name)
        {
            return null;
        }
        public UserProfileData GetUserProfile(LLUUID avatarID)
        {
            return null;
        }
    }
}
