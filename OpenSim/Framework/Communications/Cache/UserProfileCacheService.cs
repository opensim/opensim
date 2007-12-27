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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
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
using OpenSim.Framework.Console;

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
                        // The request itself will occur when the agent finishes logging on to the region
                        // so there's no need to do it here.
                        //RequestInventoryForUser(userID, userInfo);
                        m_userProfiles.Add(userID, userInfo);
                    }
                    else
                    {
                        MainLog.Instance.Error("USERCACHE", "User profile for user {0} not found", userID);
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

        public void HandleUpdateInventoryFolder(IClientAPI remoteClient, LLUUID folderID, ushort type, string name,
                                                LLUUID parentID)
        {
            CachedUserInfo userProfile;

            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                if (userProfile.RootFolder != null)
                {
                    InventoryFolderBase baseFolder = new InventoryFolderBase();
                    baseFolder.agentID = remoteClient.AgentId;
                    baseFolder.folderID = folderID;
                    baseFolder.name = name;
                    baseFolder.parentID = parentID;
                    baseFolder.type = (short) type;
                    baseFolder.version = userProfile.RootFolder.version;
                    m_parent.InventoryService.AddNewInventoryFolder(remoteClient.AgentId, baseFolder);
                }
            }
        }

        public void HandleMoveInventoryFolder(IClientAPI remoteClient, LLUUID folderID, LLUUID parentID)
        {
            CachedUserInfo userProfile;

            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                if (userProfile.RootFolder != null)
                {
                    InventoryFolderBase baseFolder = new InventoryFolderBase();
                    baseFolder.agentID = remoteClient.AgentId;
                    baseFolder.folderID = folderID;
                    baseFolder.parentID = parentID;
                    m_parent.InventoryService.MoveInventoryFolder(remoteClient.AgentId, baseFolder);
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
        public void HandleFetchInventoryDescendents(IClientAPI remoteClient, LLUUID folderID, LLUUID ownerID,
                                                    bool fetchFolders, bool fetchItems, int sortOrder)
        {
            // XXX We're not handling sortOrder yet!

            InventoryFolderImpl fold = null;
            if (folderID == libraryRoot.folderID)
            {
                remoteClient.SendInventoryFolderDetails(
                    libraryRoot.agentID, libraryRoot.folderID, libraryRoot.RequestListOfItems(),
                    libraryRoot.RequestListOfFolders(), fetchFolders, fetchItems);

                return;
            }

            if ((fold = libraryRoot.HasSubFolder(folderID)) != null)
            {
                remoteClient.SendInventoryFolderDetails(
                    libraryRoot.agentID, folderID, fold.RequestListOfItems(),
                    fold.RequestListOfFolders(), fetchFolders, fetchItems);

                return;
            }

            CachedUserInfo userProfile;
            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                if (userProfile.RootFolder != null)
                {
                    if (userProfile.RootFolder.folderID == folderID)
                    {
                        remoteClient.SendInventoryFolderDetails(
                            remoteClient.AgentId, folderID, userProfile.RootFolder.RequestListOfItems(),
                            userProfile.RootFolder.RequestListOfFolders(),
                            fetchFolders, fetchItems);

                        return;
                    }
                    else
                    {
                        if ((fold = userProfile.RootFolder.HasSubFolder(folderID)) != null)
                        {
                            remoteClient.SendInventoryFolderDetails(
                                remoteClient.AgentId, folderID, fold.RequestListOfItems(),
                                fold.RequestListOfFolders(), fetchFolders, fetchItems);

                            return;
                        }
                    }
                }
                else
                {
                    MainLog.Instance.Error(
                        "INVENTORYCACHE", "Could not find root folder for user {0}", remoteClient.Name);

                    return;
                }
            }
            else
            {
                MainLog.Instance.Error(
                    "INVENTORYCACHE",
                    "Could not find user profile for {0} for folder {1}",
                    remoteClient.Name, folderID);

                return;
            }

            // If we've reached this point then we couldn't find the folder, even though the client thinks
            // it exists
            MainLog.Instance.Error(
                "INVENTORYCACHE",
                "Could not find folder {0} for user {1}",
                folderID, remoteClient.Name);
        }

        public void HandlePurgeInventoryDescendents(IClientAPI remoteClient, LLUUID folderID)
        {
            CachedUserInfo userProfile;
            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                if (userProfile.RootFolder != null)
                {
                    InventoryFolderImpl subFolder = userProfile.RootFolder.HasSubFolder(folderID);
                    if (subFolder != null)
                    {
                        List<InventoryItemBase> items = subFolder.RequestListOfItems();
                        foreach (InventoryItemBase item in items)
                        {
                            userProfile.DeleteItem(remoteClient.AgentId, item);
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