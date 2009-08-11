using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework;
using OpenMetaverse;
using OpenSim.Services.Interfaces;
using Nini.Config;

namespace OpenSim.Tests.Common.Mock
{
    public class TestInventoryService : IInventoryService
    {
        public TestInventoryService()
        {
        }
        
        public TestInventoryService(IConfigSource config)
        {
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInterServiceInventoryServices"/>
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public bool CreateUserInventory(UUID userId)
        {
            return false;
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInterServiceInventoryServices"/>
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> GetInventorySkeleton(UUID userId)
        {
            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            InventoryFolderBase folder = new InventoryFolderBase();
            folder.ID = UUID.Random();
            folder.Owner = userId;
            folders.Add(folder);
            return folders;
        }

        public InventoryFolderBase GetRootFolder(UUID userID)
        {
            return new InventoryFolderBase();
        }

        public InventoryCollection GetFolderContent(UUID userID, UUID folderID)
        {
            return null;
        }

        public InventoryFolderBase GetFolderForType(UUID userID, AssetType type)
        {
            return null;
        }

        /// <summary>
        /// Returns a list of all the active gestures in a user's inventory.
        /// </summary>
        /// <param name="userId">
        /// The <see cref="UUID"/> of the user
        /// </param>
        /// <returns>
        /// A flat list of the gesture items.
        /// </returns>
        public List<InventoryItemBase> GetActiveGestures(UUID userId)
        {
            return null;
        }

        public InventoryCollection GetUserInventory(UUID userID)
        {
            return null;
        }

        public void GetUserInventory(UUID userID, OpenSim.Services.Interfaces.InventoryReceiptCallback callback)
        {
        }

        public List<InventoryItemBase> GetFolderItems(UUID userID, UUID folderID)
        {
            return null;
        }

        public bool AddFolder(InventoryFolderBase folder)
        {
            return false;
        }

        public bool UpdateFolder(InventoryFolderBase folder)
        {
            return false;
        }

        public bool MoveFolder(InventoryFolderBase folder)
        {
            return false;
        }

        public bool PurgeFolder(InventoryFolderBase folder)
        {
            return false;
        }

        public bool AddItem(InventoryItemBase item)
        {
            return false;
        }

        public bool UpdateItem(InventoryItemBase item)
        {
            return false;
        }

        public bool DeleteItem(InventoryItemBase item)
        {
            return false;
        }

        public InventoryItemBase QueryItem(InventoryItemBase item)
        {
            return null;
        }

        public InventoryFolderBase QueryFolder(InventoryFolderBase folder)
        {
            return null;
        }

        public bool HasInventoryForUser(UUID userID)
        {
            return false;
        }

        public InventoryFolderBase RequestRootFolder(UUID userID)
        {
            InventoryFolderBase root = new InventoryFolderBase();
            root.ID = UUID.Random();
            root.Owner = userID;
            root.ParentID = UUID.Zero;
            return root;
        }
    }
}
