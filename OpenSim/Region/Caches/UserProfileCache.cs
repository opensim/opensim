using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Data;

namespace OpenSim.Region.Caches
{
    public class UserProfileCache
    {
        public Dictionary<LLUUID, CachedUserInfo> UserProfiles = new Dictionary<LLUUID, CachedUserInfo>();

        public UserProfileCache()
        {

        }

        /// <summary>
        /// A new user has moved into a region in this instance
        /// so get info from servers
        /// </summary>
        /// <param name="userID"></param>
        public void AddNewUser(LLUUID userID)
        {

        }

        /// <summary>
        /// A user has left this instance 
        /// so make sure servers have been updated
        /// Then remove cached info
        /// </summary>
        /// <param name="userID"></param>
        public void UserLogOut(LLUUID userID)
        {

        }

        /// <summary>
        /// Request the user profile from User server
        /// </summary>
        /// <param name="userID"></param>
        private void RequestUserProfileForUser(LLUUID userID)
        {

        }

        /// <summary>
        /// Request Iventory Info from Inventory server
        /// </summary>
        /// <param name="userID"></param>
        private void RequestInventoryForUser(LLUUID userID)
        {

        }

        /// <summary>
        /// Make sure UserProfile is updated on user server
        /// </summary>
        /// <param name="userID"></param>
        private void UpdateUserProfileToServer(LLUUID userID)
        {

        }

        /// <summary>
        /// Update Inventory data to Inventory server
        /// </summary>
        /// <param name="userID"></param>
        private void UpdateInventoryToServer(LLUUID userID)
        {

        }
    }
}
