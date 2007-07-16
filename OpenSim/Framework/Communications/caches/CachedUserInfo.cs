using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Data;
using libsecondlife;

namespace OpenSim.Framework.Communications.Caches
{
    public class CachedUserInfo
    {
        public UserProfileData UserProfile;
        //public Dictionary<LLUUID, InventoryFolder> Folders = new Dictionary<LLUUID, InventoryFolder>();
        public InventoryFolder RootFolder;

        public CachedUserInfo()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="folderInfo"></param>
        public void FolderReceive(LLUUID userID, InventoryFolder folderInfo)
        {
            if (userID == UserProfile.UUID)
            {
                if (this.RootFolder == null)
                {
                    if (folderInfo.parentID == LLUUID.Zero)
                    {
                        this.RootFolder = folderInfo;
                    }
                }
                else
                {
                    if (this.RootFolder.folderID == folderInfo.parentID)
                    {
                        this.RootFolder.SubFolders.Add(folderInfo.folderID, folderInfo);
                    }
                    else
                    {
                        InventoryFolder pFolder = this.RootFolder.HasSubFolder(folderInfo.parentID);
                        if (pFolder != null)
                        {
                            pFolder.SubFolders.Add(folderInfo.folderID, folderInfo);
                        }
                    }
                }
            }
        }

        public void ItemReceive(LLUUID userID, InventoryItemBase itemInfo)
        {
            if (userID == UserProfile.UUID)
            {
                if (this.RootFolder != null)
                {
                    if (itemInfo.parentFolderID == this.RootFolder.folderID)
                    {
                        this.RootFolder.Items.Add(itemInfo.inventoryID, itemInfo);
                    }
                    else
                    {
                        InventoryFolder pFolder = this.RootFolder.HasSubFolder(itemInfo.parentFolderID);
                        if (pFolder != null)
                        {
                            pFolder.Items.Add(itemInfo.inventoryID, itemInfo);
                        }
                    }
                }

            }
        }
    }
}
