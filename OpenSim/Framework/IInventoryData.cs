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
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Collections.Generic;
using libsecondlife;

namespace OpenSim.Framework
{
    /// <summary>
    /// An interface for accessing inventory data from a storage server
    /// </summary>
    public interface IInventoryDataPlugin : IPlugin
    {
        /// <summary>
        /// Initialises the interface
        /// </summary>
        void Initialise(string connect);

        /// <summary>
        /// Returns all child folders in the hierarchy from the parent folder and down.
        /// Does not return the parent folder itself.
        /// </summary>
        /// <param name="parentID">The folder to get subfolders for</param>
        /// <returns>A list of inventory folders</returns>
        List<InventoryFolderBase> getFolderHierarchy(LLUUID parentID);

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
        /// Returns the users inventory root folder.
        /// </summary>
        /// <param name="user">The UUID of the user who is having inventory being returned</param>
        /// <returns>Root inventory folder, null if no root inventory folder was found</returns>
        InventoryFolderBase getUserRootFolder(LLUUID user);

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
        void deleteInventoryItem(LLUUID item);

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
        /// Updates a folder based on its ID with folder
        /// </summary>
        /// <param name="folder">The inventory folder</param>
        void moveInventoryFolder(InventoryFolderBase folder);

        /// <summary>
        /// Deletes a folder.  Thie will delete both the folder itself and its contents (items and descendent folders)
        /// </summary>
        /// <param name="folder">The id of the folder</param>
        void deleteInventoryFolder(LLUUID folder);
    }

    public class InventoryDataInitialiser : PluginInitialiserBase
    {
        private string connect;
        public InventoryDataInitialiser (string s) { connect = s; }
        public override void Initialise (IPlugin plugin)
        {
            IInventoryDataPlugin p = plugin as IInventoryDataPlugin;
            p.Initialise (connect);
        }
    }
}
