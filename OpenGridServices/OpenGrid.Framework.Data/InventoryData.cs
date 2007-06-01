using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenGrid.Framework.Data
{
    public class InventoryItemBase
    {
        public LLUUID inventoryID;
        public LLUUID assetID;
        public int type;
        public LLUUID parentFolderID;
        public LLUUID avatarID;
        public string inventoryName;
        public string inventoryDescription;
        public uint inventoryNextPermissions;
        public uint inventoryCurrentPermissions;
    }

    public class InventoryFolderBase
    {
        public string name;
        public LLUUID agentID;
        public LLUUID parentID;
        public LLUUID folderID;
    }

    public interface IInventoryData
    {
        /// <summary>
        /// Initialises the interface
        /// </summary>
        void Initialise();

        /// <summary>
        /// Closes the interface
        /// </summary>
        void Close();

        /// <summary>
        /// The plugin being loaded
        /// </summary>
        /// <returns>A string containing the plugin name</returns>
        string getName();

        /// <summary>
        /// The plugins version
        /// </summary>
        /// <returns>A string containing the plugin version</returns>
        string getVersion();

        /// <summary>
        /// Returns a list of inventory items contained within the specified folder
        /// </summary>
        /// <param name="folderID">The UUID of the target folder</param>
        /// <returns>A List of InventoryItemBase items</returns>
        List<InventoryItemBase> getInventoryInFolder(LLUUID folderID);

        /// <summary>
        /// Returns a list of folders in the users inventory root.
        /// </summary>
        /// <param name="user">The UUID of the user who is having inventory being returned</param>
        /// <returns>A list of folders</returns>
        List<InventoryFolderBase> getUserRootFolders(LLUUID user);

        /// <summary>
        /// Returns a list of inventory folders contained in the folder 'parentID'
        /// </summary>
        /// <param name="parentID">The folder to get subfolders for</param>
        /// <returns>A list of inventory folders</returns>
        List<InventoryFolderBase> getInventoryFolders(LLUUID parentID);

        /// <summary>
        /// Returns an inventory item by its UUID
        /// </summary>
        /// <param name="item">The UUID of the item to be returned</param>
        /// <returns>A class containing item information</returns>
        InventoryItemBase getInventoryItem(LLUUID item);

        /// <summary>
        /// Returns a specified inventory folder by its UUID
        /// </summary>
        /// <param name="folder">The UUID of the folder to be returned</param>
        /// <returns>A class containing folder information</returns>
        InventoryFolderBase getInventoryFolder(LLUUID folder);

        /// <summary>
        /// Creates a new inventory item based on item
        /// </summary>
        /// <param name="item">The item to be created</param>
        void addInventoryItem(InventoryItemBase item);

        /// <summary>
        /// Updates an inventory item with item (updates based on ID)
        /// </summary>
        /// <param name="item">The updated item</param>
        void updateInventoryItem(InventoryItemBase item);

        /// <summary>
        /// Adds a new folder specified by folder
        /// </summary>
        /// <param name="folder">The inventory folder</param>
        void addInventoryFolder(InventoryFolderBase folder);

        /// <summary>
        /// Updates a folder based on its ID with folder
        /// </summary>
        /// <param name="folder">The inventory folder</param>
        void updateInventoryFolder(InventoryFolderBase folder);
    }
}
