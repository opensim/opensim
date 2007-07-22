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

        // Methods
        public UserProfileCache(CommunicationsManager parent)
        {
            this.m_parent = parent;
        }

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
                    Console.WriteLine("UserProfileCache.cs: user profile for user not found");
                }
            }
        }

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
                CachedUserInfo info = this.UserProfiles[remoteClient.AgentId];
                if (info.RootFolder.folderID == parentID)
                {
                    info.RootFolder.CreateNewSubFolder(folderID, folderName, folderType);
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

        public void HandleFecthInventoryDescendents(IClientAPI remoteClient, LLUUID folderID, LLUUID ownerID, bool fetchFolders, bool fetchItems, int sortOrder)
        {
            if (this.UserProfiles.ContainsKey(remoteClient.AgentId))
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

        public void HandleFetchInventory(IClientAPI remoteClient, LLUUID itemID, LLUUID ownerID)
        {
            if (this.UserProfiles.ContainsKey(remoteClient.AgentId))
            {
                InventoryItemBase item = this.UserProfiles[remoteClient.AgentId].RootFolder.HasItem(itemID);
                if (item != null)
                {
                    remoteClient.SendInventoryItemDetails(ownerID, item);
                }
            }
        }

        private void RequestInventoryForUser(LLUUID userID, CachedUserInfo userInfo)
        {
            InventoryFolder folderInfo = new InventoryFolder();
            folderInfo.agentID = userID;
            folderInfo.folderID = userInfo.UserProfile.rootInventoryFolderID;
            folderInfo.name = "My Inventory";
            folderInfo.parentID = LLUUID.Zero;
            folderInfo.type = 8;
            folderInfo.version = 1;
            userInfo.FolderReceive(userID, folderInfo);
        }

        private UserProfileData RequestUserProfileForUser(LLUUID userID)
        {
            return this.m_parent.UserServer.GetUserProfile(userID);
        }

        private void UpdateInventoryToServer(LLUUID userID)
        {
        }

        private void UpdateUserProfileToServer(LLUUID userID)
        {
        }

        public void UserLogOut(LLUUID userID)
        {
        }
    }
}

