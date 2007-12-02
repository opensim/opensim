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
    public class UserProfileCacheService
    {
        // Fields
        private readonly CommunicationsManager m_parent;
        private readonly Dictionary<LLUUID, CachedUserInfo> m_userProfiles = new Dictionary<LLUUID, CachedUserInfo>();

        public LibraryRootFolder libraryRoot = new LibraryRootFolder();

        // Methods
        public UserProfileCacheService(CommunicationsManager parent)
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
            lock (m_userProfiles)
            {
                if (!m_userProfiles.ContainsKey(userID))
                {
                    CachedUserInfo userInfo = new CachedUserInfo(m_parent);
                    userInfo.UserProfile = m_parent.UserService.GetUserProfile(userID);

                    if (userInfo.UserProfile != null)
                    {
                        RequestInventoryForUser(userID, userInfo);
                        m_userProfiles.Add(userID, userInfo);
                    }
                    else
                    {
                        System.Console.WriteLine("CACHE", "User profile for user not found");
                    }
                }
            }
        }

        public void UpdateUserInventory(LLUUID userID)
        {
            CachedUserInfo userInfo = GetUserDetails(userID);
            if (userInfo != null)
            {
                RequestInventoryForUser(userID, userInfo);
            }
        }

        public CachedUserInfo GetUserDetails(LLUUID userID)
        {
            if (m_userProfiles.ContainsKey(userID))
                return m_userProfiles[userID];
            else
                return null;
        }

        public void HandleCreateInventoryFolder(IClientAPI remoteClient, LLUUID folderID, ushort folderType,
                                                string folderName, LLUUID parentID)
        {
            CachedUserInfo userProfile;

            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                if (userProfile.RootFolder != null)
                {
                    if (userProfile.RootFolder.folderID == parentID)
                    {
                        InventoryFolderImpl createdFolder =
                            userProfile.RootFolder.CreateNewSubFolder(folderID, folderName, folderType);

                        if (createdFolder != null)
                        {
                            InventoryFolderBase createdBaseFolder = new InventoryFolderBase();
                            createdBaseFolder.agentID = createdFolder.agentID;
                            createdBaseFolder.folderID = createdFolder.folderID;
                            createdBaseFolder.name = createdFolder.name;
                            createdBaseFolder.parentID = createdFolder.parentID;
                            createdBaseFolder.type = createdFolder.type;
                            createdBaseFolder.version = createdFolder.version;
                            m_parent.InventoryService.AddNewInventoryFolder(remoteClient.AgentId, createdBaseFolder);
                        }
                    }
                    else
                    {
                        InventoryFolderImpl folder = userProfile.RootFolder.HasSubFolder(parentID);
                        if (folder != null)
                        {
                            folder.CreateNewSubFolder(folderID, folderName, folderType);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Tell the client about the various child items and folders contained in the requested folder.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="ownerID"></param>
        /// <param name="fetchFolders"></param>
        /// <param name="fetchItems"></param>
        /// <param name="sortOrder"></param>
        public void HandleFecthInventoryDescendents(IClientAPI remoteClient, LLUUID folderID, LLUUID ownerID,
                                                    bool fetchFolders, bool fetchItems, int sortOrder)
        {
            InventoryFolderImpl fold = null;
            if (folderID == libraryRoot.folderID)
            {
                remoteClient.SendInventoryFolderDetails(libraryRoot.agentID, libraryRoot.folderID,
                                                        libraryRoot.RequestListOfItems(), libraryRoot.SubFoldersCount);

                return;
            }

            if ((fold = libraryRoot.HasSubFolder(folderID)) != null)
            {
                remoteClient.SendInventoryFolderDetails(libraryRoot.agentID, folderID, fold.RequestListOfItems(), fold.SubFoldersCount);

                return;
            }

            CachedUserInfo userProfile;
            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                if (userProfile.RootFolder != null)
                {
                    if (userProfile.RootFolder.folderID == folderID)
                    {
                        if (fetchItems)
                        {
                            remoteClient.SendInventoryFolderDetails(remoteClient.AgentId, folderID,
                                                                    userProfile.RootFolder.RequestListOfItems(), userProfile.RootFolder.SubFoldersCount);
                        }
                    }
                    else
                    {
                        InventoryFolderImpl folder = userProfile.RootFolder.HasSubFolder(folderID);
                        
                        if (fetchItems && folder != null)
                        {
                            remoteClient.SendInventoryFolderDetails(remoteClient.AgentId, folderID, folder.RequestListOfItems(), folder.SubFoldersCount);
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

                return;
            }

            CachedUserInfo userProfile;
            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                if (userProfile.RootFolder != null)
                {
                    InventoryItemBase item = userProfile.RootFolder.HasItem(itemID);
                    if (item != null)
                    {
                        remoteClient.SendInventoryItemDetails(ownerID, item);
                    }
                }
            }
        }

        private void RequestInventoryForUser(LLUUID userID, CachedUserInfo userInfo)
        {
            m_parent.InventoryService.RequestInventoryForUser(userID, userInfo.FolderReceive, userInfo.ItemReceive);
        }
    }
}
