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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using libsecondlife;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Data;
using OpenSim.Framework.Utilities;

namespace OpenSim.Framework.Communications.Caches
{
    public class CachedUserInfo : MarshalByRefObject
    {
        private CommunicationsManager m_parentCommsManager;
        // Fields
        public InventoryFolder RootFolder = null;
        public UserProfileData UserProfile = null;

        public CachedUserInfo(CommunicationsManager commsManager)
        {
            m_parentCommsManager = commsManager;
        }

        // Methods
        public void FolderReceive(LLUUID userID, InventoryFolderBase folderInfo)
        {
            if (userID == this.UserProfile.UUID)
            {
                if (this.RootFolder == null)
                {
                    if (folderInfo.parentID == LLUUID.Zero)
                    {
                        this.RootFolder = new InventoryFolder(folderInfo);
                    }
                }
                else if (this.RootFolder.folderID == folderInfo.parentID)
                {
                    this.RootFolder.SubFolders.Add(folderInfo.folderID, new InventoryFolder(folderInfo));
                }
                else
                {
                    InventoryFolder folder = this.RootFolder.HasSubFolder(folderInfo.parentID);
                    if (folder != null)
                    {
                        folder.SubFolders.Add(folderInfo.folderID, new InventoryFolder(folderInfo));
                    }
                }
            }
        }

        public void ItemReceive(LLUUID userID, InventoryItemBase itemInfo)
        {
            if ((userID == this.UserProfile.UUID) && (this.RootFolder != null))
            {
                if (itemInfo.parentFolderID == this.RootFolder.folderID)
                {
                    this.RootFolder.Items.Add(itemInfo.inventoryID, itemInfo);
                }
                else
                {
                    InventoryFolder folder = this.RootFolder.HasSubFolder(itemInfo.parentFolderID);
                    if (folder != null)
                    {
                        folder.Items.Add(itemInfo.inventoryID, itemInfo);                    
                    }
                }
            }
        }

        public void AddItem(LLUUID userID, InventoryItemBase itemInfo)
        {
            if ((userID == this.UserProfile.UUID) && (this.RootFolder != null))
            {
                this.ItemReceive(userID, itemInfo);
                this.m_parentCommsManager.InventoryService.AddNewInventoryItem(userID, itemInfo);
            }
        }

        public void UpdateItem(LLUUID userID, InventoryItemBase itemInfo)
        {
            if ((userID == this.UserProfile.UUID) && (this.RootFolder != null))
            {
                this.m_parentCommsManager.InventoryService.AddNewInventoryItem(userID, itemInfo);
            }
        }

        public bool DeleteItem(LLUUID userID, InventoryItemBase item)
        {
            bool result = false;
            if ((userID == this.UserProfile.UUID) && (this.RootFolder != null))
            {
                result = RootFolder.DeleteItem(item.inventoryID);
                if (result)
                {
                    this.m_parentCommsManager.InventoryService.DeleteInventoryItem(userID, item);
                }
            }
            return result;
        }
    }


}





