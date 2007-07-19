using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Interfaces;
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
                
                if (userInfo.UserProfile != null)
                {
                    this.RequestInventoryForUser(userID, userInfo);
                    this.UserProfiles.Add(userID, userInfo);
                }
                else
                {
                    //no profile for this user, what do we do now?
                    Console.WriteLine("UserProfileCache.cs: user profile for user not found");
                    
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

        public void HandleCreateInventoryFolder(IClientAPI remoteClient, LLUUID folderID, ushort folderType, string folderName, LLUUID parentID)
        {
            if (this.UserProfiles.ContainsKey(remoteClient.AgentId))
            {
                CachedUserInfo userInfo = this.UserProfiles[remoteClient.AgentId];
                if (userInfo.RootFolder.folderID == parentID)
                {
                    userInfo.RootFolder.CreateNewSubFolder(folderID, folderName, folderType);
                }
                else
                {
                    InventoryFolder parentFolder = userInfo.RootFolder.HasSubFolder(parentID);
                    if (parentFolder != null)
                    {
                        parentFolder.CreateNewSubFolder(folderID, folderName, folderType);
                    }
                }
            }
        }

        public void HandleFecthInventoryDescendents(IClientAPI remoteClient, LLUUID folderID, LLUUID ownerID, bool fetchFolders, bool fetchItems, int sortOrder)
        {
            if (this.UserProfiles.ContainsKey(remoteClient.AgentId))
            {
                CachedUserInfo userInfo = this.UserProfiles[remoteClient.AgentId];
                if (userInfo.RootFolder.folderID == folderID)
                {
                    if (fetchItems)
                    {
                        remoteClient.SendInventoryFolderDetails(remoteClient.AgentId, folderID, userInfo.RootFolder.RequestListOfItems());
                    }
                }
                else
                {
                    InventoryFolder parentFolder = userInfo.RootFolder.HasSubFolder(folderID);
                    if(parentFolder != null)
                    {
                        if(fetchItems)
                        {
                            remoteClient.SendInventoryFolderDetails(remoteClient.AgentId, folderID, parentFolder.RequestListOfItems());
                        }
                    }
                }
            }
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
        private void RequestInventoryForUser(LLUUID userID, CachedUserInfo userInfo)
        {
           // this.m_parent.InventoryServer.RequestInventoryForUser(userID, userInfo.FolderReceive, userInfo.ItemReceive);
            
            //for now we manually create the root folder,
            // but should be requesting all inventory from inventory server.
            InventoryFolder rootFolder = new InventoryFolder();
            rootFolder.agentID = userID;
            rootFolder.folderID = userInfo.UserProfile.rootInventoryFolderID;
            rootFolder.name = "My Inventory";
            rootFolder.parentID = LLUUID.Zero;
            rootFolder.type = 8;
            rootFolder.version = 1;
            userInfo.FolderReceive(userID, rootFolder);
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
