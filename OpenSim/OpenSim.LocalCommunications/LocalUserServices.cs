using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using OpenGrid.Framework.Communications;
using OpenSim.Framework.User;
using OpenGrid.Framework.UserManagement;
using OpenGrid.Framework.Data;

using libsecondlife;

namespace OpenSim.LocalCommunications
{
    public class LocalUserServices : UserManagerBase, IUserServices
    {
       
        public LocalUserServices()
        {

        }

        public UserProfileData GetUserProfile(string first_name, string last_name)
        {
            return GetUserProfile(first_name + " " + last_name);
        }

        public UserProfileData GetUserProfile(string name)
        {
            return null;
        }
        public UserProfileData GetUserProfile(LLUUID avatar_id)
        {
            return null;
        }

        public override void CustomiseResponse(ref Hashtable response, ref UserProfileData theUser)
        {

        }

    }
}
