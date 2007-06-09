using System;
using System.Collections.Generic;
using System.Text;

using libsecondlife;
namespace OpenGrid.Framework.Communications.UserServer
{
    public class UserCommsManagerBase
    {
        public UserCommsManagerBase()
        {
        }

        public virtual UserProfileData GetUserProfile(string name)
        {
            return null;
        }
        public virtual UserProfileData GetUserProfile(LLUUID avatar_id)
        {
            return null;
        }
    }

    public class UserProfileData
    {
        public UserProfileData()
        {
        }
    }
}
