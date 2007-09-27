using System;
using System.Text;
using System.Collections.Generic;

using libsecondlife;
using OpenSim.Framework.Data;
using OpenSim.Framework.Communications.Caches;

namespace OpenSim.Framework.Communications
{
    public delegate void InventoryFolderInfo(LLUUID userID, InventoryFolderBase folderInfo);
    public delegate void InventoryItemInfo(LLUUID userID, InventoryItemBase itemInfo);

    public interface IInventoryServices
    {
        void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack, InventoryItemInfo itemCallBack);
        void AddNewInventoryFolder(LLUUID userID, InventoryFolderBase folder);
        void AddNewInventoryItem(LLUUID userID, InventoryItemBase item);
        void DeleteInventoryItem(LLUUID userID, InventoryItemBase item);
        void CreateNewUserInventory(LLUUID libraryRootId, LLUUID user);
        void GetRootFoldersForUser(LLUUID user, out LLUUID libraryFolder, out LLUUID personalFolder);

        /// <summary>
        /// Returns the root folder plus any folders in root (so down one level in the Inventory folders tree)
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        List<InventoryFolderBase> RequestFirstLevelFolders(LLUUID folderID);
        List<InventoryItemBase> RequestFolderItems(LLUUID folderID);
    }
}
