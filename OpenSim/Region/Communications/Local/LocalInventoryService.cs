using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Data;
using InventoryFolder=OpenSim.Framework.Communications.Caches.InventoryFolder;
using InventoryCategory = OpenSim.Framework.Data.InventoryCategory;

namespace OpenSim.Region.Communications.Local
{
    public class LocalInventoryService : InventoryServiceBase
    {

        public LocalInventoryService()
        {

        }

        public override void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack, InventoryItemInfo itemCallBack)
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

                        List<InventoryItemBase> items = this.RequestFolderItems(newfolder.folderID);
                        foreach (InventoryItemBase item in items)
                        {
                            itemCallBack(userID, item);
                        }
                    }
                }
            }
        }

        public override void AddNewInventoryFolder(LLUUID userID, InventoryFolderBase folder)
        {
            this.AddFolder(folder);
        }

        public override void AddNewInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            this.AddItem(item);
        }

        public override void DeleteInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            this.deleteItem(item);
        }
    }
}
