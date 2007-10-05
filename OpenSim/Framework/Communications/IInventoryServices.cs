using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Communications.Caches;
using OpenSim.Framework.Types;
using InventoryFolder = OpenSim.Framework.Communications.Caches.InventoryFolder;

namespace OpenSim.Framework.Communications
{
    public delegate void InventoryFolderInfo(LLUUID userID, InventoryFolder folderInfo);
    public delegate void InventoryItemInfo(LLUUID userID, InventoryItemBase itemInfo);

    public interface IInventoryServices
    {
        void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack, InventoryItemInfo itemCallBack);
        void AddNewInventoryFolder(LLUUID userID, InventoryFolder folder);
        void AddNewInventoryItem(LLUUID userID, InventoryItemBase item);
        void DeleteInventoryItem(LLUUID userID, InventoryItemBase item);
        void CreateNewUserInventory(LLUUID user);

        /// <summary>
        /// Returns the root folder plus any folders in root (so down one level in the Inventory folders tree)
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        List<InventoryFolderBase> RequestFirstLevelFolders(LLUUID userID);
    }
}
