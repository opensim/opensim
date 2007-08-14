/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using libsecondlife;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Data;

namespace OpenSim.Framework.Communications.Caches
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
            this.m_parent = parent;
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
                    Console.WriteLine("CACHE", "User profile for user not found");
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
            if (this.UserProfiles.ContainsKey(userID))
            {
                return this.UserProfiles[userID];
            }
            return null;
        }

        public void HandleCreateInventoryFolder(IClientAPI remoteClient, LLUUID folderID, ushort folderType, string folderName, LLUUID parentID)
        {
            if (this.UserProfiles.ContainsKey(remoteClient.AgentId))
            {
                if (this.UserProfiles[remoteClient.AgentId].RootFolder != null)
                {
                    CachedUserInfo info = this.UserProfiles[remoteClient.AgentId];
                    if (info.RootFolder.folderID == parentID)
                    {
                       InventoryFolder createdFolder = info.RootFolder.CreateNewSubFolder(folderID, folderName, folderType);
                       if (createdFolder != null)
                       {
                           this.m_parent.InventoryServer.AddNewInventoryFolder(remoteClient.AgentId, createdFolder);
                       }
                    }
                    else
                    {
                        InventoryFolder folder = info.RootFolder.HasSubFolder(parentID);
                        if (folder != null)
                        {
                            folder.CreateNewSubFolder(folderID, folderName, folderType);
                        }
                    }
                }
            }
        }

        public void HandleFecthInventoryDescendents(IClientAPI remoteClient, LLUUID folderID, LLUUID ownerID, bool fetchFolders, bool fetchItems, int sortOrder)
        {
            InventoryFolder fold  = null;
            if (folderID == libraryRoot.folderID )
            {
                remoteClient.SendInventoryFolderDetails(libraryRoot.agentID, libraryRoot.folderID, libraryRoot.RequestListOfItems());
            }
            else if (( fold = libraryRoot.HasSubFolder(folderID)) != null)
            {
                remoteClient.SendInventoryFolderDetails(libraryRoot.agentID, folderID, fold.RequestListOfItems());
            }
            else if (this.UserProfiles.ContainsKey(remoteClient.AgentId))
            {
                if (this.UserProfiles[remoteClient.AgentId].RootFolder != null)
                {
                    CachedUserInfo info = this.UserProfiles[remoteClient.AgentId];
                    if (info.RootFolder.folderID == folderID)
                    {
                        if (fetchItems)
                        {
                            remoteClient.SendInventoryFolderDetails(remoteClient.AgentId, folderID, info.RootFolder.RequestListOfItems());
                        }
                    }
                    else
                    {
                        InventoryFolder folder = info.RootFolder.HasSubFolder(folderID);
                        if ((folder != null) && fetchItems)
                        {
                            remoteClient.SendInventoryFolderDetails(remoteClient.AgentId, folderID, folder.RequestListOfItems());
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
            else if (this.UserProfiles.ContainsKey(remoteClient.AgentId))
            {
                if (this.UserProfiles[remoteClient.AgentId].RootFolder != null)
                {
                    InventoryItemBase item = this.UserProfiles[remoteClient.AgentId].RootFolder.HasItem(itemID);
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
             this.m_parent.InventoryServer.RequestInventoryForUser(userID, userInfo.FolderReceive, userInfo.ItemReceive);

            //for now we manually create the root folder,
            // but should be requesting all inventory from inventory server.
           /* InventoryFolder folderInfo = new InventoryFolder();
            folderInfo.agentID = userID;
            folderInfo.folderID = userInfo.UserProfile.rootInventoryFolderID;
            folderInfo.name = "My Inventory";
            folderInfo.parentID = LLUUID.Zero;
            folderInfo.type = 8;
            folderInfo.version = 1;
            userInfo.FolderReceive(userID, folderInfo);*/
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

