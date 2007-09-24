using System;
using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Data;
using InventoryFolder = OpenSim.Framework.Communications.Caches.InventoryFolder;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGS1InventoryService : IInventoryServices
    {

        public OGS1InventoryService()
        {

        }

        #region IInventoryServices Members

        public void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack, InventoryItemInfo itemCallBack)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void AddNewInventoryFolder(LLUUID userID, InventoryFolder folder)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void AddNewInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void DeleteInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void CreateNewUserInventory(LLUUID user)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public List<InventoryFolderBase> RequestFirstLevelFolders(LLUUID userID)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }
}
