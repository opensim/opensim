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
using libsecondlife;

namespace OpenSim.Framework.Communications.Cache
{
    public class CachedUserInfo
    {
        private readonly CommunicationsManager m_parentCommsManager;
        // Fields
        public InventoryFolderImpl RootFolder = null;
        public UserProfileData UserProfile = null;

        public CachedUserInfo(CommunicationsManager commsManager)
        {
            m_parentCommsManager = commsManager;
        }

        // Methods
        public void FolderReceive(LLUUID userID, InventoryFolderImpl folderInfo)
        {
            if (userID == UserProfile.UUID)
            {
                if (RootFolder == null)
                {
                    if (folderInfo.parentID == LLUUID.Zero)
                    {
                        RootFolder = folderInfo;
                    }
                }
                else if (RootFolder.folderID == folderInfo.parentID)
                {
                    if (!RootFolder.SubFolders.ContainsKey(folderInfo.folderID))
                    {
                        RootFolder.SubFolders.Add(folderInfo.folderID, folderInfo);
                    }
                }
                else
                {
                    InventoryFolderImpl folder = RootFolder.HasSubFolder(folderInfo.parentID);
                    if (folder != null)
                    {
                        if (!folder.SubFolders.ContainsKey(folderInfo.folderID))
                        {
                            folder.SubFolders.Add(folderInfo.folderID, folderInfo);
                        }
                    }
                }
            }
        }

        public void ItemReceive(LLUUID userID, InventoryItemBase itemInfo)
        {
            if ((userID == UserProfile.UUID) && (RootFolder != null))
            {
                if (itemInfo.parentFolderID == RootFolder.folderID)
                {
                    if (!RootFolder.Items.ContainsKey(itemInfo.inventoryID))
                    {
                        RootFolder.Items.Add(itemInfo.inventoryID, itemInfo);
                    }
                }
                else
                {
                    InventoryFolderImpl folder = RootFolder.HasSubFolder(itemInfo.parentFolderID);
                    if (folder != null)
                    {
                        if (!folder.Items.ContainsKey(itemInfo.inventoryID))
                        {
                            folder.Items.Add(itemInfo.inventoryID, itemInfo);
                        }
                    }
                }
            }
        }

        public void AddItem(LLUUID userID, InventoryItemBase itemInfo)
        {
            if ((userID == UserProfile.UUID) && (RootFolder != null))
            {
                ItemReceive(userID, itemInfo);
                m_parentCommsManager.InventoryService.AddNewInventoryItem(userID, itemInfo);
            }
        }

        public void UpdateItem(LLUUID userID, InventoryItemBase itemInfo)
        {
            if ((userID == UserProfile.UUID) && (RootFolder != null))
            {
                m_parentCommsManager.InventoryService.AddNewInventoryItem(userID, itemInfo);
            }
        }

        public bool DeleteItem(LLUUID userID, InventoryItemBase item)
        {
            bool result = false;
            if ((userID == UserProfile.UUID) && (RootFolder != null))
            {
                result = RootFolder.DeleteItem(item.inventoryID);
                if (result)
                {
                    m_parentCommsManager.InventoryService.DeleteInventoryItem(userID, item);
                }
            }
            return result;
        }
    }
}