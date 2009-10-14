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
 *     * Neither the name of the OpenSimulator Project nor the
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

using System;
using System.Collections.Generic;

using OpenMetaverse;
using Nini.Config;
using log4net;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;


namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Inventory
{
    public abstract class BaseInventoryConnector : IInventoryService
    {
        protected InventoryCache m_cache;

        protected virtual void Init(IConfigSource source)
        {
            m_cache = new InventoryCache();
            m_cache.Init(source, this);
        }

        /// <summary>
        /// Create the entire inventory for a given user
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public abstract bool CreateUserInventory(UUID user);

        /// <summary>
        /// Gets the skeleton of the inventory -- folders only
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public abstract List<InventoryFolderBase> GetInventorySkeleton(UUID userId);

        /// <summary>
        /// Synchronous inventory fetch.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public abstract InventoryCollection GetUserInventory(UUID userID);

        /// <summary>
        /// Request the inventory for a user.  This is an asynchronous operation that will call the callback when the
        /// inventory has been received
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="callback"></param>
        public abstract void GetUserInventory(UUID userID, InventoryReceiptCallback callback);

        /// <summary>
        /// Retrieve the root inventory folder for the given user.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>null if no root folder was found</returns>
        public InventoryFolderBase GetRootFolder(UUID userID)
        {
            // Root folder is here as system type Folder.
            return m_cache.GetFolderForType(userID, AssetType.Folder);
        }

        public abstract Dictionary<AssetType, InventoryFolderBase> GetSystemFolders(UUID userID);

        /// <summary>
        /// Gets the user folder for the given folder-type
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public InventoryFolderBase GetFolderForType(UUID userID, AssetType type)
        {
            return m_cache.GetFolderForType(userID, type);
        }

        /// <summary>
        /// Gets everything (folders and items) inside a folder
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="folderID"></param>
        /// <returns></returns>
        public abstract InventoryCollection GetFolderContent(UUID userID, UUID folderID);

        /// <summary>
        /// Gets the items inside a folder
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="folderID"></param>
        /// <returns></returns>
        public abstract List<InventoryItemBase> GetFolderItems(UUID userID, UUID folderID);

        /// <summary>
        /// Add a new folder to the user's inventory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully added</returns>
        public abstract bool AddFolder(InventoryFolderBase folder);

        /// <summary>
        /// Update a folder in the user's inventory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully updated</returns>
        public abstract bool UpdateFolder(InventoryFolderBase folder);

        /// <summary>
        /// Move an inventory folder to a new location
        /// </summary>
        /// <param name="folder">A folder containing the details of the new location</param>
        /// <returns>true if the folder was successfully moved</returns>
        public abstract bool MoveFolder(InventoryFolderBase folder);

        /// <summary>
        /// Delete a list of inventory folders (from trash)
        /// </summary>
        public abstract bool DeleteFolders(UUID ownerID, List<UUID> folderIDs);
        
        /// <summary>
        /// Purge an inventory folder of all its items and subfolders.
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully purged</returns>
        public abstract bool PurgeFolder(InventoryFolderBase folder);

        /// <summary>
        /// Add a new item to the user's inventory.
        /// If the given item has to parent folder, it tries to find the most
        /// suitable folder for it.
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully added</returns>
        public bool AddItem(InventoryItemBase item)
        {
            if (item == null)
                return false;

            if (item.Folder == UUID.Zero)
            {
                InventoryFolderBase f = GetFolderForType(item.Owner, (AssetType)item.AssetType);
                if (f != null)
                    item.Folder = f.ID;
                else
                {
                    f = GetRootFolder(item.Owner);
                    if (f != null)
                        item.Folder = f.ID;
                    else
                        return false;
                }
            }

            return AddItemPlain(item);
        }

        protected abstract bool AddItemPlain(InventoryItemBase item);

        /// <summary>
        /// Update an item in the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully updated</returns>
        public abstract bool UpdateItem(InventoryItemBase item);

        public abstract bool MoveItems(UUID ownerID, List<InventoryItemBase> items);

        /// <summary>
        /// Delete an item from the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully deleted</returns>
        public abstract bool DeleteItems(UUID ownerID, List<UUID> itemIDs);

        public abstract InventoryItemBase GetItem(InventoryItemBase item);

        public abstract InventoryFolderBase GetFolder(InventoryFolderBase folder);

        /// <summary>
        /// Does the given user have an inventory structure?
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public abstract bool HasInventoryForUser(UUID userID);

        /// <summary>
        /// Get the active gestures of the agent.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public abstract List<InventoryItemBase> GetActiveGestures(UUID userId);

        public abstract int GetAssetPermissions(UUID userID, UUID assetID);
    }
}
