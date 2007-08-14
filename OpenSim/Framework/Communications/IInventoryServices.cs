using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Data;
using libsecondlife;
using OpenSim.Framework.Communications.Caches;
using InventoryFolder = OpenSim.Framework.Communications.Caches.InventoryFolder;

namespace OpenSim.Framework.Communications
{
    public delegate void InventoryFolderInfo(LLUUID userID, InventoryFolder folderInfo);
    public delegate void InventoryItemInfo(LLUUID userID, InventoryItemBase itemInfo);

    public interface IInventoryServices
    {
        void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack, InventoryItemInfo itemCallBack);
        void AddNewInventoryFolder(LLUUID userID, InventoryFolder folder);
    }
}
