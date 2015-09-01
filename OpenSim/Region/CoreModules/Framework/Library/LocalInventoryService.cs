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
using System.Reflection;

using OpenSim.Framework;

using OpenSim.Services.Interfaces;

using OpenMetaverse;
using log4net;

namespace OpenSim.Region.CoreModules.Framework.Library
{
    public class LocalInventoryService : IInventoryService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private InventoryFolderImpl m_Library;

        public LocalInventoryService(InventoryFolderImpl lib)
        {
            m_Library = lib;
        }

        /// <summary>
        /// Retrieve the root inventory folder for the given user.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>null if no root folder was found</returns>
        public InventoryFolderBase GetRootFolder(UUID userID) { return m_Library; }

        /// <summary>
        /// Gets everything (folders and items) inside a folder
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="folderID"></param>
        /// <returns></returns>
        public InventoryCollection GetFolderContent(UUID userID, UUID folderID)
        {
            InventoryFolderImpl folder = null;
            InventoryCollection inv = new InventoryCollection();
            inv.OwnerID = m_Library.Owner;

            if (folderID != m_Library.ID)
            {
                folder = m_Library.FindFolder(folderID);
                if (folder == null)
                {
                    inv.Folders = new List<InventoryFolderBase>();
                    inv.Items = new List<InventoryItemBase>();
                    return inv;
                }
            }
            else
                folder = m_Library;

            inv.Folders = folder.RequestListOfFolders();
            inv.Items = folder.RequestListOfItems();

            m_log.DebugFormat("[LIBRARY MODULE]: Got content for folder {0}", folder.Name);
            return inv;
        }

        public virtual InventoryCollection[] GetMultipleFoldersContent(UUID principalID, UUID[] folderIDs)
        {
            InventoryCollection[] invColl = new InventoryCollection[folderIDs.Length];
            int i = 0;
            foreach (UUID fid in folderIDs)
            {
                invColl[i++] = GetFolderContent(principalID, fid);
            }

            return invColl;
        }

        public virtual InventoryItemBase[] GetMultipleItems(UUID principalID, UUID[] itemIDs)
        {
            InventoryItemBase[] itemColl = new InventoryItemBase[itemIDs.Length];
            int i = 0;
            InventoryItemBase item = new InventoryItemBase();
            item.Owner = principalID;
            foreach (UUID fid in itemIDs)
            {
                item.ID = fid;
                itemColl[i++] = GetItem(item);
            }

            return itemColl;
        }


        /// <summary>
        /// Add a new folder to the user's inventory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully added</returns>
        public bool AddFolder(InventoryFolderBase folder)
        {
            //m_log.DebugFormat("[LIBRARY MODULE]: Adding folder {0} ({1}) to {2}", folder.Name, folder.ID, folder.ParentID);
            InventoryFolderImpl parent = m_Library;
            if (m_Library.ID != folder.ParentID)
                parent = m_Library.FindFolder(folder.ParentID);

            if (parent == null)
            {
                m_log.DebugFormat("[LIBRARY MODULE]: could not add folder {0} because parent folder {1} not found", folder.Name, folder.ParentID);
                return false;
            }

            parent.CreateChildFolder(folder.ID, folder.Name, (ushort)folder.Type);

            return true;
        }

        /// <summary>
        /// Add a new item to the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully added</returns>
        public bool AddItem(InventoryItemBase item)
        {
            //m_log.DebugFormat("[LIBRARY MODULE]: Adding item {0} to {1}", item.Name, item.Folder);
            InventoryFolderImpl folder = m_Library;
            if (m_Library.ID != item.Folder)
                folder = m_Library.FindFolder(item.Folder);

            if (folder == null)
            {
                m_log.DebugFormat("[LIBRARY MODULE]: could not add item {0} because folder {1} not found", item.Name, item.Folder);
                return false;
            }

            folder.Items.Add(item.ID, item);
            return true;
        }

        public bool CreateUserInventory(UUID user) { return false; }

        /// <summary>
        /// Gets the skeleton of the inventory -- folders only
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> GetInventorySkeleton(UUID userId) { return null; }

        /// <summary>
        /// Gets the user folder for the given folder-type
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public InventoryFolderBase GetFolderForType(UUID userID, FolderType type) { return null; }


        /// <summary>
        /// Gets the items inside a folder
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="folderID"></param>
        /// <returns></returns>
        public List<InventoryItemBase> GetFolderItems(UUID userID, UUID folderID) { return null; }


        /// <summary>
        /// Update a folder in the user's inventory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully updated</returns>
        public bool UpdateFolder(InventoryFolderBase folder) { return false; }

        /// <summary>
        /// Move an inventory folder to a new location
        /// </summary>
        /// <param name="folder">A folder containing the details of the new location</param>
        /// <returns>true if the folder was successfully moved</returns>
        public bool MoveFolder(InventoryFolderBase folder) { return false; }

        /// <summary>
        /// Delete an item from the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully deleted</returns>
        //bool DeleteItem(InventoryItemBase item);
        public bool DeleteFolders(UUID userID, List<UUID> folderIDs) { return false; }

        /// <summary>
        /// Purge an inventory folder of all its items and subfolders.
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully purged</returns>
        public bool PurgeFolder(InventoryFolderBase folder) { return false; }


        /// <summary>
        /// Update an item in the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully updated</returns>
        public bool UpdateItem(InventoryItemBase item) { return false; }

        public bool MoveItems(UUID ownerID, List<InventoryItemBase> items) { return false; }

        /// <summary>
        /// Delete an item from the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully deleted</returns>
        //bool DeleteItem(InventoryItemBase item);
        public bool DeleteItems(UUID userID, List<UUID> itemIDs) { return false; }

        /// <summary>
        /// Get an item, given by its UUID
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public InventoryItemBase GetItem(InventoryItemBase item) { return null; }

        /// <summary>
        /// Get a folder, given by its UUID
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public InventoryFolderBase GetFolder(InventoryFolderBase folder) { return null; }

        /// <summary>
        /// Does the given user have an inventory structure?
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public bool HasInventoryForUser(UUID userID) { return false; }

        /// <summary>
        /// Get the active gestures of the agent.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<InventoryItemBase> GetActiveGestures(UUID userId) { return null; }

        /// <summary>
        /// Get the union of permissions of all inventory items
        /// that hold the given assetID. 
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="assetID"></param>
        /// <returns>The permissions or 0 if no such asset is found in 
        /// the user's inventory</returns>
        public int GetAssetPermissions(UUID userID, UUID assetID) { return 0; }
    }
}
