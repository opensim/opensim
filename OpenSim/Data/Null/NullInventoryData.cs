using System;
using System.Collections.Generic;

using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.Null
{
    /// <summary>
    /// This class is completely null.
    /// </summary>
    public class NullInventoryData : IInventoryDataPlugin
    {
        public string Version { get { return "1.0.0.0"; } }

        public void Initialise()
        {
        }

        public void Dispose()
        {
            // Do nothing.
        }

        public string Name
        {
            get { return "Null Inventory Data Interface"; }
        }

        public void Initialise(string connect)
        {
        }


        /// <summary>
        /// Returns all descendent folders of this folder.  Does not return the parent folder itself.
        /// </summary>
        /// <param name="parentID">The folder to get subfolders for</param>
        /// <returns>A list of inventory folders</returns>
        public List<InventoryFolderBase> getFolderHierarchy(UUID parentID)
        {
            return new List<InventoryFolderBase>();
        }

        /// <summary>
        /// Returns a list of inventory items contained within the specified folder
        /// </summary>
        /// <param name="folderID">The UUID of the target folder</param>
        /// <returns>A List of InventoryItemBase items</returns>
        public List<InventoryItemBase> getInventoryInFolder(UUID folderID)
        {
            return new List<InventoryItemBase>();
        }

        /// <summary>
        /// Returns a list of the root folders within a users inventory
        /// </summary>
        /// <param name="user">The user whos inventory is to be searched</param>
        /// <returns>A list of folder objects</returns>
        public List<InventoryFolderBase> getUserRootFolders(UUID user)
        {
            return new List<InventoryFolderBase>();
        }

        /// <summary>
        /// Returns the users inventory root folder.
        /// </summary>
        /// <param name="user">The UUID of the user who is having inventory being returned</param>
        /// <returns>Root inventory folder, null if no root inventory folder was found</returns>
        public InventoryFolderBase getUserRootFolder(UUID user)
        {
            return null;
        }

        /// <summary>
        /// Returns a list of inventory folders contained in the folder 'parentID'
        /// </summary>
        /// <param name="parentID">The folder to get subfolders for</param>
        /// <returns>A list of inventory folders</returns>
        public List<InventoryFolderBase> getInventoryFolders(UUID parentID)
        {
            return new List<InventoryFolderBase>();
        }

        /// <summary>
        /// Returns an inventory item by its UUID
        /// </summary>
        /// <param name="item">The UUID of the item to be returned</param>
        /// <returns>A class containing item information</returns>
        public InventoryItemBase getInventoryItem(UUID item)
        {
            return null;
        }

        /// <summary>
        /// Returns a specified inventory folder by its UUID
        /// </summary>
        /// <param name="folder">The UUID of the folder to be returned</param>
        /// <returns>A class containing folder information</returns>
        public InventoryFolderBase getInventoryFolder(UUID folder)
        {
            return null;
        }

        /// <summary>
        /// Creates a new inventory item based on item
        /// </summary>
        /// <param name="item">The item to be created</param>
        public void addInventoryItem(InventoryItemBase item)
        {
        }

        /// <summary>
        /// Updates an inventory item with item (updates based on ID)
        /// </summary>
        /// <param name="item">The updated item</param>
        public void updateInventoryItem(InventoryItemBase item)
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="item"></param>
        public void deleteInventoryItem(UUID item)
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="item"></param>
        public InventoryItemBase queryInventoryItem(UUID item)
        {
            return null;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="item"></param>
        public InventoryFolderBase queryInventoryFolder(UUID folder)
        {
            return null;
        }

        /// <summary>
        /// Adds a new folder specified by folder
        /// </summary>
        /// <param name="folder">The inventory folder</param>
        public void addInventoryFolder(InventoryFolderBase folder)
        {
        }

        /// <summary>
        /// Updates a folder based on its ID with folder
        /// </summary>
        /// <param name="folder">The inventory folder</param>
        public void updateInventoryFolder(InventoryFolderBase folder)
        {
        }

        /// <summary>
        /// Updates a folder based on its ID with folder
        /// </summary>
        /// <param name="folder">The inventory folder</param>
        public void moveInventoryFolder(InventoryFolderBase folder)
        {
        }

        /// <summary>
        /// Deletes a folder.  Thie will delete both the folder itself and its contents (items and descendent folders)
        /// </summary>
        /// <param name="folder">The id of the folder</param>
        public void deleteInventoryFolder(UUID folder)
        {
        }

        /// <summary>
        /// Returns all activated gesture-items in the inventory of the specified avatar.
        /// </summary>
        /// <param name="avatarID">
        /// The <see cref="UUID"/> of the avatar
        /// </param>
        /// <returns>
        /// The list of gestures (<see cref="InventoryItemBase"/>s)
        /// </returns>
        public List<InventoryItemBase> fetchActiveGestures(UUID avatarID)
        {
            return new List<InventoryItemBase>();
        }
    }
}
