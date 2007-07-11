using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Data;
using OpenSim.Framework.Communications;

namespace OpenSim.Framework.Communications.Caches
{
    public class UserProfileCache
    {
        public Dictionary<LLUUID, CachedUserInfo> UserProfiles = new Dictionary<LLUUID, CachedUserInfo>();

        private CommunicationsManager m_parent;

        public UserProfileCache(CommunicationsManager parent)
        {
            m_parent = parent;
        }

        /// <summary>
        /// A new user has moved into a region in this instance
        /// so get info from servers
        /// </summary>
        /// <param name="userID"></param>
        public void AddNewUser(LLUUID userID)
        {
            if (!this.UserProfiles.ContainsKey(userID))
            {
                CachedUserInfo userInfo = new CachedUserInfo();
                userInfo.UserProfile = this.RequestUserProfileForUser(userID);
                this.m_parent.InventoryServer.RequestInventoryForUser(userID, userInfo.FolderReceive, userInfo.ItemReceive);
                if (userInfo.UserProfile != null)
                {
                    this.UserProfiles.Add(userID, userInfo);
                }
                else
                {
                    //no profile for this user, what do we do now?
                }
            }
            else
            {
                //already have a cached profile for this user
                //we should make sure its upto date with the user server version
            }
        }

        /// <summary>
        /// A new user has moved into a region in this instance
        /// so get info from servers
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        public void AddNewUser(string firstName, string lastName)
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
        private UserProfileData RequestUserProfileForUser(LLUUID userID)
        {
            return this.m_parent.UserServer.GetUserProfile(userID);
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
