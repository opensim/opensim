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
using System.Collections.Specialized;
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Services.Connectors.SimianGrid
{
    /// <summary>
    /// Permissions bitflags
    /// </summary>
    /*
    [Flags]
    public enum PermissionMask : uint
    {
        None = 0,
        Transfer = 1 << 13,
        Modify = 1 << 14,
        Copy = 1 << 15,
        Move = 1 << 19,
        Damage = 1 << 20,
        All = 0x7FFFFFFF
    }
    */

    /// <summary>
    /// Connects avatar inventories to the SimianGrid backend
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "SimianInventoryServiceConnector")]
    public class SimianInventoryServiceConnector : IInventoryService, ISharedRegionModule
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_serverUrl = String.Empty;
        private string m_userServerUrl = String.Empty;
//        private object m_gestureSyncRoot = new object();
        private bool m_Enabled = false;

        private const double CACHE_EXPIRATION_SECONDS = 20.0;
        private static ExpiringCache<UUID, InventoryItemBase> m_ItemCache;

        #region ISharedRegionModule

        public Type ReplaceableInterface { get { return null; } }
        public void RegionLoaded(Scene scene) { }
        public void PostInitialise() { }
        public void Close() { }

        public SimianInventoryServiceConnector() { }
        public string Name { get { return "SimianInventoryServiceConnector"; } }
        public void AddRegion(Scene scene) { if (m_Enabled) { scene.RegisterModuleInterface<IInventoryService>(this); } }
        public void RemoveRegion(Scene scene) { if (m_Enabled) { scene.UnregisterModuleInterface<IInventoryService>(this); } }

        #endregion ISharedRegionModule

        public SimianInventoryServiceConnector(IConfigSource source)
        {
            CommonInit(source);
        }

        public SimianInventoryServiceConnector(string url)
        {
            if (!url.EndsWith("/") && !url.EndsWith("="))
                url = url + '/';
            m_serverUrl = url;

            if (m_ItemCache == null)
                m_ItemCache = new ExpiringCache<UUID, InventoryItemBase>();

        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("InventoryServices", "");
                if (name == Name)
                    CommonInit(source);
            }
        }

        private void CommonInit(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["InventoryService"];
            if (gridConfig != null)
            {
                string serviceUrl = gridConfig.GetString("InventoryServerURI");
                if (!String.IsNullOrEmpty(serviceUrl))
                {
                    if (!serviceUrl.EndsWith("/") && !serviceUrl.EndsWith("="))
                        serviceUrl = serviceUrl + '/';
                    m_serverUrl = serviceUrl;

                    gridConfig = source.Configs["UserAccountService"];
                    if (gridConfig != null)
                    {
                        serviceUrl = gridConfig.GetString("UserAccountServerURI");
                        if (!String.IsNullOrEmpty(serviceUrl))
                        {
                            m_userServerUrl = serviceUrl;
                            m_Enabled = true;
                            if (m_ItemCache == null)
                                m_ItemCache = new ExpiringCache<UUID, InventoryItemBase>();
                        }
                    }
                }
            }

            if (String.IsNullOrEmpty(m_serverUrl))
                m_log.Info("[SIMIAN INVENTORY CONNECTOR]: No InventoryServerURI specified, disabling connector");
            else if (String.IsNullOrEmpty(m_userServerUrl))
                m_log.Info("[SIMIAN INVENTORY CONNECTOR]: No UserAccountServerURI specified, disabling connector");
        }

        /// <summary>
        /// Create the entire inventory for a given user
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public bool CreateUserInventory(UUID userID)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddInventory" },
                { "OwnerID", userID.ToString() }
            };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("[SIMIAN INVENTORY CONNECTOR]: Inventory creation for " + userID + " failed: " + response["Message"].AsString());

            return success;
        }

        /// <summary>
        /// Gets the skeleton of the inventory -- folders only
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> GetInventorySkeleton(UUID userID)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetInventoryNode" },
                { "ItemID", userID.ToString() },
                { "OwnerID", userID.ToString() },
                { "IncludeFolders", "1" },
                { "IncludeItems", "0" },
                { "ChildrenOnly", "0" }
            };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean() && response["Items"] is OSDArray)
            {
                OSDArray items = (OSDArray)response["Items"];
                return GetFoldersFromResponse(items, userID, true);
            }
            else
            {
                m_log.Warn("[SIMIAN INVENTORY CONNECTOR]: Failed to retrieve inventory skeleton for " + userID + ": " +
                    response["Message"].AsString());
                return new List<InventoryFolderBase>(0);
            }
        }

        /// <summary>
        /// Retrieve the root inventory folder for the given user.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>null if no root folder was found</returns>
        public InventoryFolderBase GetRootFolder(UUID userID)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetInventoryNode" },
                { "ItemID", userID.ToString() },
                { "OwnerID", userID.ToString() },
                { "IncludeFolders", "1" },
                { "IncludeItems", "0" },
                { "ChildrenOnly", "1" }
            };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean() && response["Items"] is OSDArray)
            {
                OSDArray items = (OSDArray)response["Items"];
                List<InventoryFolderBase> folders = GetFoldersFromResponse(items, userID, true);

                if (folders.Count > 0)
                    return folders[0];
            }

            return null;
        }

        /// <summary>
        /// Gets the user folder for the given folder-type
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public InventoryFolderBase GetFolderForType(UUID userID, FolderType type)
        {
            string contentType = SLUtil.SLAssetTypeToContentType((int)type);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetFolderForType" },
                { "ContentType", contentType },
                { "OwnerID", userID.ToString() }
            };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean() && response["Folder"] is OSDMap)
            {
                OSDMap folder = (OSDMap)response["Folder"];

                return new InventoryFolderBase(
                    folder["ID"].AsUUID(),
                    folder["Name"].AsString(),
                    folder["OwnerID"].AsUUID(),
                    (short)SLUtil.ContentTypeToSLAssetType(folder["ContentType"].AsString()),
                    folder["ParentID"].AsUUID(),
                    (ushort)folder["Version"].AsInteger()
                );
            }
            else
            {
                m_log.Warn("[SIMIAN INVENTORY CONNECTOR]: Default folder not found for content type " + contentType + ": " + response["Message"].AsString());
                return GetRootFolder(userID);
            }
        }

        /// <summary>
        /// Get an item, given by its UUID
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public InventoryItemBase GetItem(InventoryItemBase item)
        {
            InventoryItemBase retrieved = null;
            if (m_ItemCache.TryGetValue(item.ID, out retrieved))
                return retrieved;

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetInventoryNode" },
                { "ItemID", item.ID.ToString() },
                { "OwnerID", item.Owner.ToString() },
                { "IncludeFolders", "1" },
                { "IncludeItems", "1" },
                { "ChildrenOnly", "1" }
            };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean() && response["Items"] is OSDArray)
            {
                List<InventoryItemBase> items = GetItemsFromResponse((OSDArray)response["Items"]);
                if (items.Count > 0)
                {
                    // The requested item should be the first in this list, but loop through
                    // and sanity check just in case
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (items[i].ID == item.ID)
                        {
                            retrieved = items[i];
                            m_ItemCache.AddOrUpdate(item.ID, retrieved, CACHE_EXPIRATION_SECONDS);
                            return retrieved;
                        }
                    }
                }
            }

            m_log.Warn("[SIMIAN INVENTORY CONNECTOR]: Item " + item.ID + " owned by " + item.Owner + " not found");
            return null;
        }

        public InventoryItemBase[] GetMultipleItems(UUID principalID, UUID[] itemIDs)
        {
            InventoryItemBase[] result = new InventoryItemBase[itemIDs.Length];
            int i = 0;
            InventoryItemBase item = new InventoryItemBase();
            item.Owner = principalID;
            foreach (UUID id in itemIDs)
            {
                item.ID = id;
                result[i++] = GetItem(item);
            }

            return result;
        }

        /// <summary>
        /// Get a folder, given by its UUID
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public InventoryFolderBase GetFolder(InventoryFolderBase folder)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetInventoryNode" },
                { "ItemID", folder.ID.ToString() },
                { "OwnerID", folder.Owner.ToString() },
                { "IncludeFolders", "1" },
                { "IncludeItems", "0" },
                { "ChildrenOnly", "1" }
            };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean() && response["Items"] is OSDArray)
            {
                OSDArray items = (OSDArray)response["Items"];
                List<InventoryFolderBase> folders = GetFoldersFromResponse(items, folder.ID, true);

                if (folders.Count > 0)
                    return folders[0];
            }

            return null;
        }

        /// <summary>
        /// Gets everything (folders and items) inside a folder
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="folderID"></param>
        /// <returns></returns>
        public InventoryCollection GetFolderContent(UUID userID, UUID folderID)
        {
            InventoryCollection inventory = new InventoryCollection();
            inventory.OwnerID = userID;

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetInventoryNode" },
                { "ItemID", folderID.ToString() },
                { "OwnerID", userID.ToString() },
                { "IncludeFolders", "1" },
                { "IncludeItems", "1" },
                { "ChildrenOnly", "1" }
            };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean() && response["Items"] is OSDArray)
            {
                OSDArray items = (OSDArray)response["Items"];

                inventory.Folders = GetFoldersFromResponse(items, folderID, false);
                inventory.Items = GetItemsFromResponse(items);
            }
            else
            {
                m_log.Warn("[SIMIAN INVENTORY CONNECTOR]: Error fetching folder " + folderID + " content for " + userID + ": " +
                    response["Message"].AsString());
                inventory.Folders = new List<InventoryFolderBase>(0);
                inventory.Items = new List<InventoryItemBase>(0);
            }

            return inventory;
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

        /// <summary>
        /// Gets the items inside a folder
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="folderID"></param>
        /// <returns></returns>
        public List<InventoryItemBase> GetFolderItems(UUID userID, UUID folderID)
        {
            InventoryCollection inventory = new InventoryCollection();
            inventory.OwnerID = userID;

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetInventoryNode" },
                { "ItemID", folderID.ToString() },
                { "OwnerID", userID.ToString() },
                { "IncludeFolders", "0" },
                { "IncludeItems", "1" },
                { "ChildrenOnly", "1" }
            };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean() && response["Items"] is OSDArray)
            {
                OSDArray items = (OSDArray)response["Items"];
                return GetItemsFromResponse(items);
            }
            else
            {
                m_log.Warn("[SIMIAN INVENTORY CONNECTOR]: Error fetching folder " + folderID + " for " + userID + ": " +
                    response["Message"].AsString());
                return new List<InventoryItemBase>(0);
            }
        }

        /// <summary>
        /// Add a new folder to the user's inventory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully added</returns>
        public bool AddFolder(InventoryFolderBase folder)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddInventoryFolder" },
                { "FolderID", folder.ID.ToString() },
                { "ParentID", folder.ParentID.ToString() },
                { "OwnerID", folder.Owner.ToString() },
                { "Name", folder.Name },
                { "ContentType", SLUtil.SLAssetTypeToContentType(folder.Type) }
            };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
            {
                m_log.Warn("[SIMIAN INVENTORY CONNECTOR]: Error creating folder " + folder.Name + " for " + folder.Owner + ": " +
                    response["Message"].AsString());
            }

            return success;
        }

        /// <summary>
        /// Update a folder in the user's inventory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully updated</returns>
        public bool UpdateFolder(InventoryFolderBase folder)
        {
            return AddFolder(folder);
        }

        /// <summary>
        /// Move an inventory folder to a new location
        /// </summary>
        /// <param name="folder">A folder containing the details of the new location</param>
        /// <returns>true if the folder was successfully moved</returns>
        public bool MoveFolder(InventoryFolderBase folder)
        {
            return AddFolder(folder);
        }

        /// <summary>
        /// Delete an item from the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully deleted</returns>
        //bool DeleteItem(InventoryItemBase item);
        public bool DeleteFolders(UUID userID, List<UUID> folderIDs)
        {
            return DeleteItems(userID, folderIDs);
        }

        /// <summary>
        /// Delete an item from the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully deleted</returns>
        public bool DeleteItems(UUID userID, List<UUID> itemIDs)
        {
            // TODO: RemoveInventoryNode should be replaced with RemoveInventoryNodes
            bool allSuccess = true;

            for (int i = 0; i < itemIDs.Count; i++)
            {
                UUID itemID = itemIDs[i];

                NameValueCollection requestArgs = new NameValueCollection
                {
                    { "RequestMethod", "RemoveInventoryNode" },
                    { "OwnerID", userID.ToString() },
                    { "ItemID", itemID.ToString() }
                };

                OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
                bool success = response["Success"].AsBoolean();

                if (!success)
                {
                    m_log.Warn("[SIMIAN INVENTORY CONNECTOR]: Error removing item " + itemID + " for " + userID + ": " +
                        response["Message"].AsString());
                    allSuccess = false;
                }
            }
            
            return allSuccess;
        }

        /// <summary>
        /// Purge an inventory folder of all its items and subfolders.
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully purged</returns>
        public bool PurgeFolder(InventoryFolderBase folder)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "PurgeInventoryFolder" },
                { "OwnerID", folder.Owner.ToString() },
                { "FolderID", folder.ID.ToString() }
            };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
            {
                m_log.Warn("[SIMIAN INVENTORY CONNECTOR]: Error purging folder " + folder.ID + " for " + folder.Owner + ": " +
                    response["Message"].AsString());
            }

            return success;
        }

        /// <summary>
        /// Add a new item to the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully added</returns>
        public bool AddItem(InventoryItemBase item)
        {
            // A folder of UUID.Zero means we need to find the most appropriate home for this item
            if (item.Folder == UUID.Zero)
            {
                InventoryFolderBase folder = null;
                if (Enum.IsDefined(typeof(FolderType), (sbyte)item.AssetType))
                    folder = GetFolderForType(item.Owner, (FolderType)item.AssetType);
                if (folder != null && folder.ID != UUID.Zero)
                    item.Folder = folder.ID;
                else
                    item.Folder = item.Owner; // Root folder
            }

            if ((AssetType)item.AssetType == AssetType.Gesture)
                UpdateGesture(item.Owner, item.ID, item.Flags == 1);

            if (item.BasePermissions == 0)
                m_log.WarnFormat("[SIMIAN INVENTORY CONNECTOR]: Adding inventory item {0} ({1}) with no base permissions", item.Name, item.ID);

            OSDMap permissions = new OSDMap
            {
                { "BaseMask", OSD.FromInteger(item.BasePermissions) },
                { "EveryoneMask", OSD.FromInteger(item.EveryOnePermissions) },
                { "GroupMask", OSD.FromInteger(item.GroupPermissions) },
                { "NextOwnerMask", OSD.FromInteger(item.NextPermissions) },
                { "OwnerMask", OSD.FromInteger(item.CurrentPermissions) }
            };

            OSDMap extraData = new OSDMap()
            {
                { "Flags", OSD.FromInteger(item.Flags) },
                { "GroupID", OSD.FromUUID(item.GroupID) },
                { "GroupOwned", OSD.FromBoolean(item.GroupOwned) },
                { "SalePrice", OSD.FromInteger(item.SalePrice) },
                { "SaleType", OSD.FromInteger(item.SaleType) },
                { "Permissions", permissions }
            };

            // Add different asset type only if it differs from inventory type
            // (needed for links)
            string invContentType = SLUtil.SLInvTypeToContentType(item.InvType);
            string assetContentType = SLUtil.SLAssetTypeToContentType(item.AssetType);

            if (invContentType != assetContentType)
                extraData["LinkedItemType"] = OSD.FromString(assetContentType);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddInventoryItem" },
                { "ItemID", item.ID.ToString() },
                { "AssetID", item.AssetID.ToString() },
                { "ParentID", item.Folder.ToString() },
                { "OwnerID", item.Owner.ToString() },
                { "Name", item.Name },
                { "Description", item.Description },
                { "CreatorID", item.CreatorId },
                { "CreatorData", item.CreatorData },
                { "ContentType", invContentType },
                { "ExtraData", OSDParser.SerializeJsonString(extraData) }
            };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
            {
                m_log.Warn("[SIMIAN INVENTORY CONNECTOR]: Error creating item " + item.Name + " for " + item.Owner + ": " +
                    response["Message"].AsString());
            }

            return success;
        }

        /// <summary>
        /// Update an item in the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully updated</returns>
        public bool UpdateItem(InventoryItemBase item)
        {
            if (item.AssetID != UUID.Zero)
            {
                return AddItem(item);
            }
            else
            {
                // This is actually a folder update
                InventoryFolderBase folder = new InventoryFolderBase(item.ID, item.Name, item.Owner, (short)item.AssetType, item.Folder, 0);
                return UpdateFolder(folder);
            }
        }

        public bool MoveItems(UUID ownerID, List<InventoryItemBase> items)
        {
            bool success = true;

            while (items.Count > 0)
            {
                List<InventoryItemBase> currentItems = new List<InventoryItemBase>();
                UUID destFolderID = items[0].Folder;

                // Find all of the items being moved to the current destination folder
                for (int i = 0; i < items.Count; i++)
                {
                    InventoryItemBase item = items[i];
                    if (item.Folder == destFolderID)
                        currentItems.Add(item);
                }

                // Do the inventory move for the current items
                success &= MoveItems(ownerID, items, destFolderID);

                // Remove the processed items from the list
                for (int i = 0; i < currentItems.Count; i++)
                    items.Remove(currentItems[i]);
            }

            return success;
        }

        /// <summary>
        /// Does the given user have an inventory structure?
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public bool HasInventoryForUser(UUID userID)
        {
            return GetRootFolder(userID) != null;
        }

        /// <summary>
        /// Get the active gestures of the agent.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public List<InventoryItemBase> GetActiveGestures(UUID userID)
        {
            OSDArray items = FetchGestures(userID);

            string[] itemIDs = new string[items.Count];
            for (int i = 0; i < items.Count; i++)
                itemIDs[i] = items[i].AsUUID().ToString();

//            NameValueCollection requestArgs = new NameValueCollection
//            {
//                { "RequestMethod", "GetInventoryNodes" },
//                { "OwnerID", userID.ToString() },
//                { "Items", String.Join(",", itemIDs) }
//            };

            // FIXME: Implement this in SimianGrid
            return new List<InventoryItemBase>(0);
        }

        /// <summary>
        /// Get the union of permissions of all inventory items
        /// that hold the given assetID. 
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="assetID"></param>
        /// <returns>The permissions or 0 if no such asset is found in 
        /// the user's inventory</returns>
        public int GetAssetPermissions(UUID userID, UUID assetID)
        {
//            NameValueCollection requestArgs = new NameValueCollection
//            {
//                { "RequestMethod", "GetInventoryNodes" },
//                { "OwnerID", userID.ToString() },
//                { "AssetID", assetID.ToString() }
//            };

            // FIXME: Implement this in SimianGrid
            return (int)PermissionMask.All;
        }

        private List<InventoryFolderBase> GetFoldersFromResponse(OSDArray items, UUID baseFolder, bool includeBaseFolder)
        {
            List<InventoryFolderBase> invFolders = new List<InventoryFolderBase>(items.Count);

            for (int i = 0; i < items.Count; i++)
            {
                OSDMap item = items[i] as OSDMap;

                if (item != null && item["Type"].AsString() == "Folder")
                {
                    UUID folderID = item["ID"].AsUUID();

                    if (folderID == baseFolder && !includeBaseFolder)
                        continue;

                    invFolders.Add(new InventoryFolderBase(
                        folderID,
                        item["Name"].AsString(),
                        item["OwnerID"].AsUUID(),
                        (short)SLUtil.ContentTypeToSLAssetType(item["ContentType"].AsString()),
                        item["ParentID"].AsUUID(),
                        (ushort)item["Version"].AsInteger()
                    ));
                }
            }

//            m_log.Debug("[SIMIAN INVENTORY CONNECTOR]: Parsed " + invFolders.Count + " folders from SimianGrid response");
            return invFolders;
        }

        private List<InventoryItemBase> GetItemsFromResponse(OSDArray items)
        {
            List<InventoryItemBase> invItems = new List<InventoryItemBase>(items.Count);

            for (int i = 0; i < items.Count; i++)
            {
                OSDMap item = items[i] as OSDMap;

                if (item != null && item["Type"].AsString() == "Item")
                {
                    InventoryItemBase invItem = new InventoryItemBase();
                    
                    invItem.AssetID = item["AssetID"].AsUUID();
                    invItem.AssetType = SLUtil.ContentTypeToSLAssetType(item["ContentType"].AsString());
                    invItem.CreationDate = item["CreationDate"].AsInteger();
                    invItem.CreatorId = item["CreatorID"].AsString();
                    invItem.CreatorData = item["CreatorData"].AsString();
                    invItem.Description = item["Description"].AsString();
                    invItem.Folder = item["ParentID"].AsUUID();
                    invItem.ID = item["ID"].AsUUID();
                    invItem.InvType = SLUtil.ContentTypeToSLInvType(item["ContentType"].AsString());
                    invItem.Name = item["Name"].AsString();
                    invItem.Owner = item["OwnerID"].AsUUID();

                    OSDMap extraData = item["ExtraData"] as OSDMap;
                    if (extraData != null && extraData.Count > 0)
                    {
                        invItem.Flags = extraData["Flags"].AsUInteger();
                        invItem.GroupID = extraData["GroupID"].AsUUID();
                        invItem.GroupOwned = extraData["GroupOwned"].AsBoolean();
                        invItem.SalePrice = extraData["SalePrice"].AsInteger();
                        invItem.SaleType = (byte)extraData["SaleType"].AsInteger();

                        OSDMap perms = extraData["Permissions"] as OSDMap;
                        if (perms != null)
                        {
                            invItem.BasePermissions = perms["BaseMask"].AsUInteger();
                            invItem.CurrentPermissions = perms["OwnerMask"].AsUInteger();
                            invItem.EveryOnePermissions = perms["EveryoneMask"].AsUInteger();
                            invItem.GroupPermissions = perms["GroupMask"].AsUInteger();
                            invItem.NextPermissions = perms["NextOwnerMask"].AsUInteger();
                        }

                        if (extraData.ContainsKey("LinkedItemType"))
                            invItem.AssetType = SLUtil.ContentTypeToSLAssetType(extraData["LinkedItemType"].AsString());
                    }

                    if (invItem.BasePermissions == 0)
                    {
                        m_log.InfoFormat("[SIMIAN INVENTORY CONNECTOR]: Forcing item permissions to full for item {0} ({1})",
                            invItem.Name, invItem.ID);
                        invItem.BasePermissions = (uint)PermissionMask.All;
                        invItem.CurrentPermissions = (uint)PermissionMask.All;
                        invItem.EveryOnePermissions = (uint)PermissionMask.All;
                        invItem.GroupPermissions = (uint)PermissionMask.All;
                        invItem.NextPermissions = (uint)PermissionMask.All;
                    }

                    invItems.Add(invItem);
                }
            }

//            m_log.Debug("[SIMIAN INVENTORY CONNECTOR]: Parsed " + invItems.Count + " items from SimianGrid response");
            return invItems;
        }

        private bool MoveItems(UUID ownerID, List<InventoryItemBase> items, UUID destFolderID)
        {
            string[] itemIDs = new string[items.Count];
            for (int i = 0; i < items.Count; i++)
                itemIDs[i] = items[i].ID.ToString();

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "MoveInventoryNodes" },
                { "OwnerID", ownerID.ToString() },
                { "FolderID", destFolderID.ToString() },
                { "Items", String.Join(",", itemIDs) }
            };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
            {
                m_log.Warn("[SIMIAN INVENTORY CONNECTOR]: Failed to move " + items.Count + " items to " +
                    destFolderID + ": " + response["Message"].AsString());
            }

            return success;
        }

        private void UpdateGesture(UUID userID, UUID itemID, bool enabled)
        {
            OSDArray gestures = FetchGestures(userID);
            OSDArray newGestures = new OSDArray();

            for (int i = 0; i < gestures.Count; i++)
            {
                UUID gesture = gestures[i].AsUUID();
                if (gesture != itemID)
                    newGestures.Add(OSD.FromUUID(gesture));
            }

            if (enabled)
                newGestures.Add(OSD.FromUUID(itemID));

            SaveGestures(userID, newGestures);
        }

        private OSDArray FetchGestures(UUID userID)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetUser" },
                { "UserID", userID.ToString() }
            };

            OSDMap response = SimianGrid.PostToService(m_userServerUrl, requestArgs);
            if (response["Success"].AsBoolean())
            {
                OSDMap user = response["User"] as OSDMap;
                if (user != null && response.ContainsKey("Gestures"))
                {
                    OSD gestures = OSDParser.DeserializeJson(response["Gestures"].AsString());
                    if (gestures != null && gestures is OSDArray)
                        return (OSDArray)gestures;
                    else
                        m_log.Error("[SIMIAN INVENTORY CONNECTOR]: Unrecognized active gestures data for " + userID);
                }
            }
            else
            {
                m_log.Warn("[SIMIAN INVENTORY CONNECTOR]: Failed to fetch active gestures for " + userID + ": " +
                    response["Message"].AsString());
            }

            return new OSDArray();
        }

        private void SaveGestures(UUID userID, OSDArray gestures)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddUserData" },
                { "UserID", userID.ToString() },
                { "Gestures", OSDParser.SerializeJsonString(gestures) }
            };

            OSDMap response = SimianGrid.PostToService(m_userServerUrl, requestArgs);
            if (!response["Success"].AsBoolean())
            {
                m_log.Warn("[SIMIAN INVENTORY CONNECTOR]: Failed to save active gestures for " + userID + ": " +
                    response["Message"].AsString());
            }
        }
    }
}
