/*
* Copyright (c) Contributors, http://opensimulator.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System.Collections.Generic;
using libsecondlife;

namespace OpenSim.Framework.Communications.Cache
{
    public class UserProfileCache
    {
        // Fields
        private CommunicationsManager m_parent;
        public Dictionary<LLUUID, CachedUserInfo> UserProfiles = new Dictionary<LLUUID, CachedUserInfo>();

        public LibraryRootFolder libraryRoot = new LibraryRootFolder();

        // Methods
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
            // Potential fix - Multithreading issue.
            lock (UserProfiles)
            {
                if (!UserProfiles.ContainsKey(userID))
                {
                    CachedUserInfo userInfo = new CachedUserInfo(m_parent);
                    userInfo.UserProfile = RequestUserProfileForUser(userID);
                    if (userInfo.UserProfile != null)
                    {
                        RequestInventoryForUser(userID, userInfo);
                        UserProfiles.Add(userID, userInfo);
                    }
                    else
                    {
                        System.Console.WriteLine("CACHE", "User profile for user not found");
                    }
                }
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

        public CachedUserInfo GetUserDetails(LLUUID userID)
        {
            if (UserProfiles.ContainsKey(userID))
            {
                return UserProfiles[userID];
            }
            return null;
        }

        public void HandleCreateInventoryFolder(IClientAPI remoteClient, LLUUID folderID, ushort folderType,
                                                string folderName, LLUUID parentID)
        {
            if (UserProfiles.ContainsKey(remoteClient.AgentId))
            {
                if (UserProfiles[remoteClient.AgentId].RootFolder != null)
                {
                    CachedUserInfo info = UserProfiles[remoteClient.AgentId];
                    if (info.RootFolder.folderID == parentID)
                    {
                        InventoryFolderImpl createdFolder =
                            info.RootFolder.CreateNewSubFolder(folderID, folderName, folderType);
                        if (createdFolder != null)
                        {
                            m_parent.InventoryService.AddNewInventoryFolder(remoteClient.AgentId, createdFolder);
                        }
                    }
                    else
                    {
                        InventoryFolderImpl folder = info.RootFolder.HasSubFolder(parentID);
                        if (folder != null)
                        {
                            folder.CreateNewSubFolder(folderID, folderName, folderType);
                        }
                    }
                }
            }
        }

        public void HandleFecthInventoryDescendents(IClientAPI remoteClient, LLUUID folderID, LLUUID ownerID,
                                                    bool fetchFolders, bool fetchItems, int sortOrder)
        {
            InventoryFolderImpl fold = null;
            if (folderID == libraryRoot.folderID)
            {
                remoteClient.SendInventoryFolderDetails(libraryRoot.agentID, libraryRoot.folderID,
                                                        libraryRoot.RequestListOfItems());
            }
            else if ((fold = libraryRoot.HasSubFolder(folderID)) != null)
            {
                remoteClient.SendInventoryFolderDetails(libraryRoot.agentID, folderID, fold.RequestListOfItems());
            }
            else if (UserProfiles.ContainsKey(remoteClient.AgentId))
            {
                if (UserProfiles[remoteClient.AgentId].RootFolder != null)
                {
                    CachedUserInfo info = UserProfiles[remoteClient.AgentId];
                    if (info.RootFolder.folderID == folderID)
                    {
                        if (fetchItems)
                        {
                            remoteClient.SendInventoryFolderDetails(remoteClient.AgentId, folderID,
                                                                    info.RootFolder.RequestListOfItems());
                        }
                    }
                    else
                    {
                        InventoryFolderImpl folder = info.RootFolder.HasSubFolder(folderID);
                        if ((folder != null) && fetchItems)
                        {
                            remoteClient.SendInventoryFolderDetails(remoteClient.AgentId, folderID,
                                                                    folder.RequestListOfItems());
                        }
                    }
                }
            }
        }

        public void HandleFetchInventory(IClientAPI remoteClient, LLUUID itemID, LLUUID ownerID)
        {
            if (ownerID == libraryRoot.agentID)
            {
                //Console.WriteLine("request info for library item");
            }
            else if (UserProfiles.ContainsKey(remoteClient.AgentId))
            {
                if (UserProfiles[remoteClient.AgentId].RootFolder != null)
                {
                    InventoryItemBase item = UserProfiles[remoteClient.AgentId].RootFolder.HasItem(itemID);
                    if (item != null)
                    {
                        remoteClient.SendInventoryItemDetails(ownerID, item);
                    }
                }
            }
        }

        /// <summary>
        /// Request Iventory Info from Inventory server
        /// </summary>
        /// <param name="userID"></param>
        private void RequestInventoryForUser(LLUUID userID, CachedUserInfo userInfo)
        {
            m_parent.InventoryService.RequestInventoryForUser(userID, userInfo.FolderReceive, userInfo.ItemReceive);
        }

        /// <summary>
        /// Request the user profile from User server
        /// </summary>
        /// <param name="userID"></param>
        private UserProfileData RequestUserProfileForUser(LLUUID userID)
        {
            return m_parent.UserService.GetUserProfile(userID);
        }

        /// <summary>
        /// Update Inventory data to Inventory server
        /// </summary>
        /// <param name="userID"></param>
        private void UpdateInventoryToServer(LLUUID userID)
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
        /// A user has left this instance 
        /// so make sure servers have been updated
        /// Then remove cached info
        /// </summary>
        /// <param name="userID"></param>
        public void UserLogOut(LLUUID userID)
        {
        }
    }
}