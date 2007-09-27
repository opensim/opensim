/*
* Copyright (c) Contributors, http://opensimulator.org/
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
using System.Xml.Serialization;
using System.Collections.Generic;
using libsecondlife;

namespace OpenSim.Framework.Data
{

    public enum InventoryCategory : byte { Library, Default, User };
    /// <summary>
    /// Inventory Item - contains all the properties associated with an individual inventory piece.
    /// </summary>
    public class InventoryItemBase : MarshalByRefObject
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
        public int assetType;
        /// <summary>
        /// The type of inventory item. (Can be slightly different to the asset type
        /// </summary>
        public int invType;
        /// <summary>
        /// The folder this item is contained in 
        /// </summary>
        public LLUUID parentFolderID;
        /// <summary>
        /// The owner of this inventory item
        /// </summary>
        public LLUUID avatarID;
        /// <summary>
        /// The creator of this item
        /// </summary>
        public LLUUID creatorsID;
        /// <summary>
        /// The name of the inventory item (must be less than 64 characters)
        /// </summary>
        [XmlElement(ElementName="name")]
        public string inventoryName;
        /// <summary>
        /// The description of the inventory item (must be less than 64 characters)
        /// </summary>
        [XmlElement(ElementName = "description")]
        public string inventoryDescription;
        /// <summary>
        /// A mask containing the permissions for the next owner (cannot be enforced)
        /// </summary>
        public uint inventoryNextPermissions;
        /// <summary>
        /// A mask containing permissions for the current owner (cannot be enforced)
        /// </summary>
        public uint inventoryCurrentPermissions;
        /// <summary>
        /// 
        /// </summary>
        public uint inventoryBasePermissions;
        /// <summary>
        /// 
        /// </summary>
        public uint inventoryEveryOnePermissions;
    }

    /// <summary>
    /// A Class for folders which contain users inventory
    /// </summary>
    public class InventoryFolderBase : MarshalByRefObject
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
        /// The folder this folder is contained in 
        /// </summary>
        public LLUUID parentID;
        /// <summary>
        /// The UUID for this folder
        /// </summary>
        public LLUUID folderID;
        /// <summary>
        /// Tyep of Items normally stored in this folder
        /// </summary>
        public short type;
        /// <summary>
        /// 
        /// </summary>
        public ushort version;
        /// <summary>
        /// Inventory category, Library, Default, System
        /// </summary>
        public InventoryCategory category;
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
        /// Returns a list of the root folders within a users inventory
        /// </summary>
        /// <param name="user">The user whos inventory is to be searched</param>
        /// <returns>A list of folder objects</returns>
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
        /// 
        /// </summary>
        /// <param name="item"></param>
        void deleteInventoryItem(InventoryItemBase item);

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

        /// <summary>
        /// Delete a complete inventory category
        /// </summary>
        /// <param name="inventoryCategory">What folder category shout be deleted</param>
        void deleteInventoryCategory(InventoryCategory inventoryCategory);

        /// <summary>
        /// Setup the initial folderset of a user
        /// </summary>
        /// <param name="user"></param>
        //void CreateNewUserInventory(LLUUID user);
    }
}
