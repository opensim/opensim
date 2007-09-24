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
            
        }

        public void AddNewInventoryFolder(LLUUID userID, InventoryFolder folder)
        {
            
        }

        public void AddNewInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            
        }

        public void DeleteInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            
        }

        public void CreateNewUserInventory(LLUUID user)
        {
            
        }

        public List<InventoryFolderBase> RequestFirstLevelFolders(LLUUID userID)
        {
            return new List<InventoryFolderBase>();
        }

        #endregion
    }
}
