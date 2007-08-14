using System;
using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Data;
using OpenSim.Framework.Types;
using OpenSim.Framework.UserManagement;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.InventoryServiceBase;
using InventoryFolder = OpenSim.Framework.Communications.Caches.InventoryFolder;

namespace OpenSim.Region.Communications.Local
{
    public class LocalInventoryService : InventoryServiceBase , IInventoryServices
    {

        public LocalInventoryService()
        {

        }

        public void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack, InventoryItemInfo itemCallBack)
        {
            List<InventoryFolderBase> folders = this.RequestFirstLevelFolders(userID);
            InventoryFolder rootFolder = null;

            //need to make sure we send root folder first
            foreach (InventoryFolderBase folder in folders)
            {
                if (folder.parentID == libsecondlife.LLUUID.Zero)
                {
                    InventoryFolder newfolder = new InventoryFolder(folder);
                    rootFolder = newfolder;
                    folderCallBack(userID, newfolder);
                }
            }

            if (rootFolder != null)
            {
                foreach (InventoryFolderBase folder in folders)
                {
                    if (folder.folderID != rootFolder.folderID)
                    {
                        InventoryFolder newfolder = new InventoryFolder(folder);
                        folderCallBack(userID, newfolder);
                    }
                }
            }
        }

        public void AddNewInventoryFolder(LLUUID userID, InventoryFolder folder)
        {
            this.AddFolder(folder);
        }
    }
}
