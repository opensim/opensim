/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenGrid.Framework.Data
{
    /// <summary>
    /// Inventory Item - contains all the properties associated with an individual inventory piece.
    /// </summary>
    public class InventoryItemBase
    {
        /// <summary>
        /// A UUID containing the ID for the inventory item itself
        /// </summary>
        public LLUUID inventoryID;
        /// <summary>
        /// The UUID of the associated asset on the asset server
        /// </summary>
        public LLUUID assetID;
        /// <summary>
        /// This is an enumerated value determining the type of asset (eg Notecard, Sound, Object, etc)
        /// </summary>
        public int type;
        /// <summary>
        /// The folder this item is contained in (NULL_KEY = Inventory Root)
        /// </summary>
        public LLUUID parentFolderID;
        /// <summary>
        /// The owner of this inventory item
        /// </summary>
        public LLUUID avatarID;
        /// <summary>
        /// The name of the inventory item (must be less than 64 characters)
        /// </summary>
        public string inventoryName;
        /// <summary>
        /// The description of the inventory item (must be less than 64 characters)
        /// </summary>
        public string inventoryDescription;
        /// <summary>
        /// A mask containing the permissions for the next owner (cannot be enforced)
        /// </summary>
        public uint inventoryNextPermissions;
        /// <summary>
        /// A mask containing permissions for the current owner (cannot be enforced)
        /// </summary>
        public uint inventoryCurrentPermissions;
    }

    /// <summary>
    /// A Class for folders which contain users inventory
    /// </summary>
    public class InventoryFolderBase
    {
        /// <summary>
        /// The name of the folder (64 characters or less)
        /// </summary>
        public string name;
        /// <summary>
        /// The agent who's inventory this is contained by
        /// </summary>
        public LLUUID agentID;
        /// <summary>
        /// The folder this folder is contained in (NULL_KEY for root)
        /// </summary>
        public LLUUID parentID;
        /// <summary>
        /// The UUID for this folder
        /// </summary>
        public LLUUID folderID;
    }

    /// <summary>
    /// An interface for accessing inventory data from a storage server
    /// </summary>
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
