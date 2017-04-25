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
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Avatar.Inventory.Archiver;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Tests.Common
{
    /// <summary>
    /// Utility functions for carrying out user inventory tests.
    /// </summary>
    public static class UserInventoryHelpers
    {
        public static readonly string PATH_DELIMITER = "/";

        /// <summary>
        /// Add an existing scene object as an item in the user's inventory.
        /// </summary>
        /// <remarks>
        /// Will be added to the system Objects folder.
        /// </remarks>
        /// <param name='scene'></param>
        /// <param name='so'></param>
        /// <param name='inventoryIdTail'></param>
        /// <param name='assetIdTail'></param>
        /// <returns>The inventory item created.</returns>
        public static InventoryItemBase AddInventoryItem(
            Scene scene, SceneObjectGroup so, int inventoryIdTail, int assetIdTail)
        {
            return AddInventoryItem(
                scene,
                so.Name,
                TestHelpers.ParseTail(inventoryIdTail),
                InventoryType.Object,
                AssetHelpers.CreateAsset(TestHelpers.ParseTail(assetIdTail), so),
                so.OwnerID);
        }

        /// <summary>
        /// Add an existing scene object as an item in the user's inventory at the given path.
        /// </summary>
        /// <param name='scene'></param>
        /// <param name='so'></param>
        /// <param name='inventoryIdTail'></param>
        /// <param name='assetIdTail'></param>
        /// <returns>The inventory item created.</returns>
        public static InventoryItemBase AddInventoryItem(
            Scene scene, SceneObjectGroup so, int inventoryIdTail, int assetIdTail, string path)
        {
            return AddInventoryItem(
                scene,
                so.Name,
                TestHelpers.ParseTail(inventoryIdTail),
                InventoryType.Object,
                AssetHelpers.CreateAsset(TestHelpers.ParseTail(assetIdTail), so),
                so.OwnerID,
                path);
        }

        /// <summary>
        /// Adds the given item to the existing system folder for its type (e.g. an object will go in the "Objects"
        /// folder).
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="itemName"></param>
        /// <param name="itemId"></param>
        /// <param name="itemType"></param>
        /// <param name="asset">The serialized asset for this item</param>
        /// <param name="userId"></param>
        /// <returns></returns>
        private static InventoryItemBase AddInventoryItem(
            Scene scene, string itemName, UUID itemId, InventoryType itemType, AssetBase asset, UUID userId)
        {
            return AddInventoryItem(
                scene, itemName, itemId, itemType, asset, userId,
                scene.InventoryService.GetFolderForType(userId, (FolderType)asset.Type).Name);
        }

        /// <summary>
        /// Adds the given item to an inventory folder
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="itemName"></param>
        /// <param name="itemId"></param>
        /// <param name="itemType"></param>
        /// <param name="asset">The serialized asset for this item</param>
        /// <param name="userId"></param>
        /// <param name="path">Existing inventory path at which to add.</param>
        /// <returns></returns>
        private static InventoryItemBase AddInventoryItem(
            Scene scene, string itemName, UUID itemId, InventoryType itemType, AssetBase asset, UUID userId, string path)
        {
            scene.AssetService.Store(asset);

            InventoryItemBase item = new InventoryItemBase();
            item.Name = itemName;
            item.AssetID = asset.FullID;
            item.ID = itemId;
            item.Owner = userId;
            item.AssetType = asset.Type;
            item.InvType = (int)itemType;
            item.BasePermissions = (uint)OpenMetaverse.PermissionMask.All |
                (uint)(Framework.PermissionMask.FoldedMask | Framework.PermissionMask.FoldedCopy | Framework.PermissionMask.FoldedModify | Framework.PermissionMask.FoldedTransfer);
            item.CurrentPermissions = (uint)OpenMetaverse.PermissionMask.All |
                (uint)(Framework.PermissionMask.FoldedMask | Framework.PermissionMask.FoldedCopy | Framework.PermissionMask.FoldedModify | Framework.PermissionMask.FoldedTransfer);

            InventoryFolderBase folder = InventoryArchiveUtils.FindFoldersByPath(scene.InventoryService, userId, path)[0];

            item.Folder = folder.ID;
            scene.AddInventoryItem(item);

            return item;
        }

        /// <summary>
        /// Creates a notecard in the objects folder and specify an item id.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="itemName"></param>
        /// <param name="itemId"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public static InventoryItemBase CreateInventoryItem(Scene scene, string itemName, UUID userId)
        {
            return CreateInventoryItem(scene, itemName, UUID.Random(), UUID.Random(), userId, InventoryType.Notecard);
        }

        /// <summary>
        /// Creates an item of the given type with an accompanying asset.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="itemName"></param>
        /// <param name="itemId"></param>
        /// <param name="userId"></param>
        /// <param name="type">Type of item to create</param>
        /// <returns></returns>
        public static InventoryItemBase CreateInventoryItem(
            Scene scene, string itemName, UUID userId, InventoryType type)
        {
            return CreateInventoryItem(scene, itemName, UUID.Random(), UUID.Random(), userId, type);
        }

        /// <summary>
        /// Creates a notecard in the objects folder and specify an item id.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="itemName"></param>
        /// <param name="itemId"></param>
        /// <param name="assetId"></param>
        /// <param name="userId"></param>
        /// <param name="type">Type of item to create</param>
        /// <returns></returns>
        public static InventoryItemBase CreateInventoryItem(
            Scene scene, string itemName, UUID itemId, UUID assetId, UUID userId, InventoryType itemType)
        {
            AssetBase asset = null;

            if (itemType == InventoryType.Notecard)
            {
                asset = AssetHelpers.CreateNotecardAsset();
                asset.CreatorID = userId.ToString();
            }
            else if (itemType == InventoryType.Object)
            {
                asset = AssetHelpers.CreateAsset(assetId, SceneHelpers.CreateSceneObject(1, userId));
            }
            else
            {
                throw new Exception(string.Format("Inventory type {0} not supported", itemType));
            }

            return AddInventoryItem(scene, itemName, itemId, itemType, asset, userId);
        }

        /// <summary>
        /// Create inventory folders starting from the user's root folder.
        /// </summary>
        /// <param name="inventoryService"></param>
        /// <param name="userId"></param>
        /// <param name="path">
        /// The folders to create.  Multiple folders can be specified on a path delimited by the PATH_DELIMITER
        /// </param>
        /// <param name="useExistingFolders">
        /// If true, then folders in the path which already the same name are
        /// used.  This applies to the terminal folder as well.
        /// If false, then all folders in the path are created, even if there is already a folder at a particular
        /// level with the same name.
        /// </param>
        /// <returns>
        /// The folder created.  If the path contains multiple folders then the last one created is returned.
        /// Will return null if the root folder could not be found.
        /// </returns>
        public static InventoryFolderBase CreateInventoryFolder(
            IInventoryService inventoryService, UUID userId, string path, bool useExistingFolders)
        {
            return CreateInventoryFolder(inventoryService, userId, UUID.Random(), path, useExistingFolders);
        }

        /// <summary>
        /// Create inventory folders starting from the user's root folder.
        /// </summary>
        /// <param name="inventoryService"></param>
        /// <param name="userId"></param>
        /// <param name="folderId"></param>
        /// <param name="path">
        /// The folders to create.  Multiple folders can be specified on a path delimited by the PATH_DELIMITER
        /// </param>
        /// <param name="useExistingFolders">
        /// If true, then folders in the path which already the same name are
        /// used.  This applies to the terminal folder as well.
        /// If false, then all folders in the path are created, even if there is already a folder at a particular
        /// level with the same name.
        /// </param>
        /// <returns>
        /// The folder created.  If the path contains multiple folders then the last one created is returned.
        /// Will return null if the root folder could not be found.
        /// </returns>
        public static InventoryFolderBase CreateInventoryFolder(
            IInventoryService inventoryService, UUID userId, UUID folderId, string path, bool useExistingFolders)
        {
            InventoryFolderBase rootFolder = inventoryService.GetRootFolder(userId);

            if (null == rootFolder)
                return null;

            return CreateInventoryFolder(inventoryService, folderId, rootFolder, path, useExistingFolders);
        }

        /// <summary>
        /// Create inventory folders starting from a given parent folder
        /// </summary>
        /// <remarks>
        /// If any stem of the path names folders that already exist then these are not recreated.  This includes the
        /// final folder.
        /// TODO: May need to make it an option to create duplicate folders.
        /// </remarks>
        /// <param name="inventoryService"></param>
        /// <param name="folderId">ID of the folder to create</param>
        /// <param name="parentFolder"></param>
        /// <param name="path">
        /// The folder to create.
        /// </param>
        /// <param name="useExistingFolders">
        /// If true, then folders in the path which already the same name are
        /// used.  This applies to the terminal folder as well.
        /// If false, then all folders in the path are created, even if there is already a folder at a particular
        /// level with the same name.
        /// </param>
        /// <returns>
        /// The folder created.  If the path contains multiple folders then the last one created is returned.
        /// </returns>
        public static InventoryFolderBase CreateInventoryFolder(
            IInventoryService inventoryService, UUID folderId, InventoryFolderBase parentFolder, string path, bool useExistingFolders)
        {
            string[] components = path.Split(new string[] { PATH_DELIMITER }, 2, StringSplitOptions.None);

            InventoryFolderBase folder = null;

            if (useExistingFolders)
                folder = InventoryArchiveUtils.FindFolderByPath(inventoryService, parentFolder, components[0]);

            if (folder == null)
            {
//                Console.WriteLine("Creating folder {0} at {1}", components[0], parentFolder.Name);

                UUID folderIdForCreate;

                if (components.Length > 1)
                    folderIdForCreate = UUID.Random();
                else
                    folderIdForCreate = folderId;

                folder
                    = new InventoryFolderBase(
                        folderIdForCreate, components[0], parentFolder.Owner, (short)AssetType.Unknown, parentFolder.ID, 0);

                inventoryService.AddFolder(folder);
            }
//            else
//            {
//                Console.WriteLine("Found existing folder {0}", folder.Name);
//            }

            if (components.Length > 1)
                return CreateInventoryFolder(inventoryService, folderId, folder, components[1], useExistingFolders);
            else
                return folder;
        }

        /// <summary>
        /// Get the inventory folder that matches the path name.  If there are multiple folders then only the first
        /// is returned.
        /// </summary>
        /// <param name="inventoryService"></param>
        /// <param name="userId"></param>
        /// <param name="path"></param>
        /// <returns>null if no folder matching the path was found</returns>
        public static InventoryFolderBase GetInventoryFolder(IInventoryService inventoryService, UUID userId, string path)
        {
            List<InventoryFolderBase> folders = GetInventoryFolders(inventoryService, userId, path);

            if (folders.Count != 0)
                return folders[0];
            else
                return null;
        }

        /// <summary>
        /// Get the inventory folders that match the path name.
        /// </summary>
        /// <param name="inventoryService"></param>
        /// <param name="userId"></param>
        /// <param name="path"></param>
        /// <returns>An empty list if no matching folders were found</returns>
        public static List<InventoryFolderBase> GetInventoryFolders(IInventoryService inventoryService, UUID userId, string path)
        {
            return InventoryArchiveUtils.FindFoldersByPath(inventoryService, userId, path);
        }

        /// <summary>
        /// Get the inventory item that matches the path name.  If there are multiple items then only the first
        /// is returned.
        /// </summary>
        /// <param name="inventoryService"></param>
        /// <param name="userId"></param>
        /// <param name="path"></param>
        /// <returns>null if no item matching the path was found</returns>
        public static InventoryItemBase GetInventoryItem(IInventoryService inventoryService, UUID userId, string path)
        {
            return InventoryArchiveUtils.FindItemByPath(inventoryService, userId, path);
        }

        /// <summary>
        /// Get the inventory items that match the path name.
        /// </summary>
        /// <param name="inventoryService"></param>
        /// <param name="userId"></param>
        /// <param name="path"></param>
        /// <returns>An empty list if no matching items were found.</returns>
        public static List<InventoryItemBase> GetInventoryItems(IInventoryService inventoryService, UUID userId, string path)
        {
            return InventoryArchiveUtils.FindItemsByPath(inventoryService, userId, path);
        }
    }
}
