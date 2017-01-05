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
using log4net;
using Nini.Config;
using System.Reflection;
using OpenSim.Services.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Data;
using OpenSim.Framework;

namespace OpenSim.Services.InventoryService
{
    public class XInventoryService : ServiceBase, IInventoryService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        protected IXInventoryData m_Database;
        protected bool m_AllowDelete = true;
        protected string m_ConfigName = "InventoryService";

        public XInventoryService(IConfigSource config)
            : this(config, "InventoryService")
        {
        }

        public XInventoryService(IConfigSource config, string configName) : base(config)
        {
            if (configName != string.Empty)
                m_ConfigName = configName;

            string dllName = String.Empty;
            string connString = String.Empty;
            //string realm = "Inventory"; // OSG version doesn't use this

            //
            // Try reading the [InventoryService] section first, if it exists
            //
            IConfig authConfig = config.Configs[m_ConfigName];
            if (authConfig != null)
            {
                dllName = authConfig.GetString("StorageProvider", dllName);
                connString = authConfig.GetString("ConnectionString", connString);
                m_AllowDelete = authConfig.GetBoolean("AllowDelete", true);
                // realm = authConfig.GetString("Realm", realm);
            }

            //
            // Try reading the [DatabaseService] section, if it exists
            //
            IConfig dbConfig = config.Configs["DatabaseService"];
            if (dbConfig != null)
            {
                if (dllName == String.Empty)
                    dllName = dbConfig.GetString("StorageProvider", String.Empty);
                if (connString == String.Empty)
                    connString = dbConfig.GetString("ConnectionString", String.Empty);
            }

            //
            // We tried, but this doesn't exist. We can't proceed.
            //
            if (dllName == String.Empty)
                throw new Exception("No StorageProvider configured");

            m_Database = LoadPlugin<IXInventoryData>(dllName,
                    new Object[] {connString, String.Empty});

            if (m_Database == null)
                throw new Exception("Could not find a storage interface in the given module");
        }

        public virtual bool CreateUserInventory(UUID principalID)
        {
            // This is braindeaad. We can't ever communicate that we fixed
            // an existing inventory. Well, just return root folder status,
            // but check sanity anyway.
            //
            bool result = false;

            InventoryFolderBase rootFolder = GetRootFolder(principalID);

            if (rootFolder == null)
            {
                rootFolder = ConvertToOpenSim(CreateFolder(principalID, UUID.Zero, (int)FolderType.Root, InventoryFolderBase.ROOT_FOLDER_NAME));
                result = true;
            }

            XInventoryFolder[] sysFolders = GetSystemFolders(principalID, rootFolder.ID);

            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Animation) return true; return false; }))
                CreateFolder(principalID, rootFolder.ID, (int)FolderType.Animation, "Animations");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.BodyPart) return true; return false; }))
                CreateFolder(principalID, rootFolder.ID, (int)FolderType.BodyPart, "Body Parts");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.CallingCard) return true; return false; }))
            {
                XInventoryFolder folder = CreateFolder(principalID, rootFolder.ID, (int)FolderType.CallingCard, "Calling Cards");
                folder = CreateFolder(principalID, folder.folderID, (int)FolderType.CallingCard, "Friends");
                CreateFolder(principalID, folder.folderID, (int)FolderType.CallingCard, "All");
            }
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Clothing) return true; return false; }))
                CreateFolder(principalID, rootFolder.ID, (int)FolderType.Clothing, "Clothing");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.CurrentOutfit) return true; return false; }))
                CreateFolder(principalID, rootFolder.ID, (int)FolderType.CurrentOutfit, "Current Outfit");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Favorites) return true; return false; }))
                CreateFolder(principalID, rootFolder.ID, (int)FolderType.Favorites, "Favorites");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Gesture) return true; return false; }))
                CreateFolder(principalID, rootFolder.ID, (int)FolderType.Gesture, "Gestures");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Landmark) return true; return false; }))
                CreateFolder(principalID, rootFolder.ID, (int)FolderType.Landmark, "Landmarks");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.LostAndFound) return true; return false; }))
                CreateFolder(principalID, rootFolder.ID, (int)FolderType.LostAndFound, "Lost And Found");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Notecard) return true; return false; }))
                CreateFolder(principalID, rootFolder.ID, (int)FolderType.Notecard, "Notecards");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Object) return true; return false; }))
                CreateFolder(principalID, rootFolder.ID, (int)FolderType.Object, "Objects");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Snapshot) return true; return false; }))
                CreateFolder(principalID, rootFolder.ID, (int)FolderType.Snapshot, "Photo Album");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.LSLText) return true; return false; }))
                CreateFolder(principalID, rootFolder.ID, (int)FolderType.LSLText, "Scripts");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Sound) return true; return false; }))
                CreateFolder(principalID, rootFolder.ID, (int)FolderType.Sound, "Sounds");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Texture) return true; return false; }))
                CreateFolder(principalID, rootFolder.ID, (int)FolderType.Texture, "Textures");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Trash) return true; return false; }))
                CreateFolder(principalID, rootFolder.ID, (int)FolderType.Trash, "Trash");

            return result;
        }

        protected XInventoryFolder CreateFolder(UUID principalID, UUID parentID, int type, string name)
        {
            XInventoryFolder newFolder = new XInventoryFolder();

            newFolder.folderName = name;
            newFolder.type = type;
            newFolder.version = 1;
            newFolder.folderID = UUID.Random();
            newFolder.agentID = principalID;
            newFolder.parentFolderID = parentID;

            m_Database.StoreFolder(newFolder);

            return newFolder;
        }

        protected virtual XInventoryFolder[] GetSystemFolders(UUID principalID, UUID rootID)
        {
//            m_log.DebugFormat("[XINVENTORY SERVICE]: Getting system folders for {0}", principalID);

            XInventoryFolder[] allFolders = m_Database.GetFolders(
                    new string[] { "agentID", "parentFolderID" },
                    new string[] { principalID.ToString(), rootID.ToString() });

            XInventoryFolder[] sysFolders = Array.FindAll(
                    allFolders,
                    delegate (XInventoryFolder f)
                    {
                        if (f.type > 0)
                            return true;
                        return false;
                    });

//            m_log.DebugFormat(
//                "[XINVENTORY SERVICE]: Found {0} system folders for {1}", sysFolders.Length, principalID);

            return sysFolders;
        }

        public virtual List<InventoryFolderBase> GetInventorySkeleton(UUID principalID)
        {
            XInventoryFolder[] allFolders = m_Database.GetFolders(
                    new string[] { "agentID" },
                    new string[] { principalID.ToString() });

            if (allFolders.Length == 0)
                return null;

            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();

            foreach (XInventoryFolder x in allFolders)
            {
                //m_log.DebugFormat("[XINVENTORY SERVICE]: Adding folder {0} to skeleton", x.folderName);
                folders.Add(ConvertToOpenSim(x));
            }

            return folders;
        }

        public virtual InventoryFolderBase GetRootFolder(UUID principalID)
        {
            XInventoryFolder[] folders = m_Database.GetFolders(
                    new string[] { "agentID", "parentFolderID"},
                    new string[] { principalID.ToString(), UUID.Zero.ToString() });

            if (folders.Length == 0)
                return null;

            XInventoryFolder root = null;
            foreach (XInventoryFolder folder in folders)
            {
                if (folder.folderName == InventoryFolderBase.ROOT_FOLDER_NAME)
                {
                    root = folder;
                    break;
                }
            }

            if (root == null) // oops
                root = folders[0];

            return ConvertToOpenSim(root);
        }

        public virtual InventoryFolderBase GetFolderForType(UUID principalID, FolderType type)
        {
//            m_log.DebugFormat("[XINVENTORY SERVICE]: Getting folder type {0} for user {1}", type, principalID);

            InventoryFolderBase rootFolder = GetRootFolder(principalID);

            if (rootFolder == null)
            {
                m_log.WarnFormat(
                    "[XINVENTORY]: Found no root folder for {0} in GetFolderForType() when looking for {1}",
                    principalID, type);

                return null;
            }

            return GetSystemFolderForType(rootFolder, type);
        }

        private InventoryFolderBase GetSystemFolderForType(InventoryFolderBase rootFolder, FolderType type)
        {
            //m_log.DebugFormat("[XINVENTORY SERVICE]: Getting folder type {0}", type);

            if (type == FolderType.Root)
                return rootFolder;

            XInventoryFolder[] folders = m_Database.GetFolders(
                    new string[] { "agentID", "parentFolderID", "type"},
                    new string[] { rootFolder.Owner.ToString(), rootFolder.ID.ToString(), ((int)type).ToString() });

            if (folders.Length == 0)
            {
                //m_log.WarnFormat("[XINVENTORY SERVICE]: Found no folder for type {0} ", type);
                return null;
            }

            //m_log.DebugFormat(
            //    "[XINVENTORY SERVICE]: Found folder {0} {1} for type {2}",
            //    folders[0].folderName, folders[0].folderID, type);

            return ConvertToOpenSim(folders[0]);
        }

        public virtual InventoryCollection GetFolderContent(UUID principalID, UUID folderID)
        {
            // This method doesn't receive a valud principal id from the
            // connector. So we disregard the principal and look
            // by ID.
            //
            //m_log.DebugFormat("[XINVENTORY SERVICE]: Fetch contents for folder {0}", folderID.ToString());
            InventoryCollection inventory = new InventoryCollection();
            inventory.OwnerID = principalID;
            inventory.Folders = new List<InventoryFolderBase>();
            inventory.Items = new List<InventoryItemBase>();

            XInventoryFolder[] folders = m_Database.GetFolders(
                    new string[] { "parentFolderID"},
                    new string[] { folderID.ToString() });

            foreach (XInventoryFolder x in folders)
            {
                //m_log.DebugFormat("[XINVENTORY]: Adding folder {0} to response", x.folderName);
                inventory.Folders.Add(ConvertToOpenSim(x));
            }

            XInventoryItem[] items = m_Database.GetItems(
                    new string[] { "parentFolderID"},
                    new string[] { folderID.ToString() });

            foreach (XInventoryItem i in items)
            {
                //m_log.DebugFormat("[XINVENTORY]: Adding item {0} to response", i.inventoryName);
                inventory.Items.Add(ConvertToOpenSim(i));
            }

            InventoryFolderBase f = GetFolder(principalID, folderID);
            if (f != null)
            {
                inventory.Version = f.Version;
                inventory.OwnerID = f.Owner;
            }
            inventory.FolderID = folderID;

            return inventory;
        }

        public virtual InventoryCollection[] GetMultipleFoldersContent(UUID principalID, UUID[] folderIDs)
        {
            InventoryCollection[] multiple = new InventoryCollection[folderIDs.Length];
            int i = 0;
            foreach (UUID fid in folderIDs)
                multiple[i++] = GetFolderContent(principalID, fid);

            return multiple;
        }

        public virtual List<InventoryItemBase> GetFolderItems(UUID principalID, UUID folderID)
        {
//            m_log.DebugFormat("[XINVENTORY]: Fetch items for folder {0}", folderID);

            // Since we probably don't get a valid principal here, either ...
            //
            List<InventoryItemBase> invItems = new List<InventoryItemBase>();

            XInventoryItem[] items = m_Database.GetItems(
                    new string[] { "parentFolderID" },
                    new string[] { folderID.ToString() });

            foreach (XInventoryItem i in items)
                invItems.Add(ConvertToOpenSim(i));

            return invItems;
        }

        public virtual bool AddFolder(InventoryFolderBase folder)
        {
//            m_log.DebugFormat("[XINVENTORY]: Add folder {0} type {1} in parent {2}", folder.Name, folder.Type, folder.ParentID);

            InventoryFolderBase check = GetFolder(folder.Owner, folder.ID);
            if (check != null)
                return false;

            if (folder.Type != (short)FolderType.None)
            {
                InventoryFolderBase rootFolder = GetRootFolder(folder.Owner);

                if (rootFolder == null)
                {
                    m_log.WarnFormat(
                        "[XINVENTORY]: Found no root folder for {0} in AddFolder() when looking for {1}",
                        folder.Owner, folder.Type);

                    return false;
                }

                // Check we're not trying to add this as a system folder.
                if (folder.ParentID == rootFolder.ID)
                {
                    InventoryFolderBase existingSystemFolder
                        = GetSystemFolderForType(rootFolder, (FolderType)folder.Type);

                    if (existingSystemFolder != null)
                    {
                        m_log.WarnFormat(
                            "[XINVENTORY]: System folder of type {0} already exists when tried to add {1} to {2} for {3}",
                            folder.Type, folder.Name, folder.ParentID, folder.Owner);

                        return false;
                    }
                }
            }

            XInventoryFolder xFolder = ConvertFromOpenSim(folder);
            return m_Database.StoreFolder(xFolder);
        }

        public virtual bool UpdateFolder(InventoryFolderBase folder)
        {
//            m_log.DebugFormat("[XINVENTORY]: Update folder {0} {1} ({2})", folder.Name, folder.Type, folder.ID);

            XInventoryFolder xFolder = ConvertFromOpenSim(folder);
            InventoryFolderBase check = GetFolder(folder.Owner, folder.ID);

            if (check == null)
                return AddFolder(folder);

            if ((check.Type != (short)FolderType.None || xFolder.type != (short)FolderType.None)
                && (check.Type != (short)FolderType.Outfit || xFolder.type != (short)FolderType.Outfit))
            {
                if (xFolder.version < check.Version)
                {
//                    m_log.DebugFormat("[XINVENTORY]: {0} < {1} can't do", xFolder.version, check.Version);
                    return false;
                }

                check.Version = (ushort)xFolder.version;
                xFolder = ConvertFromOpenSim(check);

//                m_log.DebugFormat(
//                    "[XINVENTORY]: Storing version only update to system folder {0} {1} {2}",
//                    xFolder.folderName, xFolder.version, xFolder.type);

                return m_Database.StoreFolder(xFolder);
            }

            if (xFolder.version < check.Version)
                xFolder.version = check.Version;

            xFolder.folderID = check.ID;

            return m_Database.StoreFolder(xFolder);
        }

        public virtual bool MoveFolder(InventoryFolderBase folder)
        {
            return m_Database.MoveFolder(folder.ID.ToString(), folder.ParentID.ToString());
        }

        // We don't check the principal's ID here
        //
        public virtual bool DeleteFolders(UUID principalID, List<UUID> folderIDs)
        {
            return DeleteFolders(principalID, folderIDs, true);
        }

        public virtual bool DeleteFolders(UUID principalID, List<UUID> folderIDs, bool onlyIfTrash)
        {
            if (!m_AllowDelete)
                return false;

            // Ignore principal ID, it's bogus at connector level
            //
            foreach (UUID id in folderIDs)
            {
                if (onlyIfTrash && !ParentIsTrash(id))
                    continue;
                //m_log.InfoFormat("[XINVENTORY SERVICE]: Delete folder {0}", id);
                InventoryFolderBase f = new InventoryFolderBase();
                f.ID = id;
                PurgeFolder(f, onlyIfTrash);
                m_Database.DeleteFolders("folderID", id.ToString());
            }

            return true;
        }

        public virtual bool PurgeFolder(InventoryFolderBase folder)
        {
            return PurgeFolder(folder, true);
        }

        public virtual bool PurgeFolder(InventoryFolderBase folder, bool onlyIfTrash)
        {
            if (!m_AllowDelete)
                return false;

            if (onlyIfTrash && !ParentIsTrash(folder.ID))
                return false;

            XInventoryFolder[] subFolders = m_Database.GetFolders(
                    new string[] { "parentFolderID" },
                    new string[] { folder.ID.ToString() });

            foreach (XInventoryFolder x in subFolders)
            {
                PurgeFolder(ConvertToOpenSim(x), onlyIfTrash);
                m_Database.DeleteFolders("folderID", x.folderID.ToString());
            }

            m_Database.DeleteItems("parentFolderID", folder.ID.ToString());

            return true;
        }

        public virtual bool AddItem(InventoryItemBase item)
        {
//            m_log.DebugFormat(
//                "[XINVENTORY SERVICE]: Adding item {0} {1} to folder {2} for {3}", item.Name, item.ID, item.Folder, item.Owner);

            return m_Database.StoreItem(ConvertFromOpenSim(item));
        }

        public virtual bool UpdateItem(InventoryItemBase item)
        {
            if (!m_AllowDelete)
                if (item.AssetType == (sbyte)AssetType.Link || item.AssetType == (sbyte)AssetType.LinkFolder)
                    return false;

//            m_log.InfoFormat(
//                "[XINVENTORY SERVICE]: Updating item {0} {1} in folder {2}", item.Name, item.ID, item.Folder);

            InventoryItemBase retrievedItem = GetItem(item.Owner, item.ID);

            if (retrievedItem == null)
            {
                m_log.WarnFormat(
                    "[XINVENTORY SERVICE]: Tried to update item {0} {1}, owner {2} but no existing item found.",
                    item.Name, item.ID, item.Owner);

                return false;
            }

            // Do not allow invariants to change.  Changes to folder ID occur in MoveItems()
            if (retrievedItem.InvType != item.InvType
                || retrievedItem.AssetType != item.AssetType
                || retrievedItem.Folder != item.Folder
                || retrievedItem.CreatorIdentification != item.CreatorIdentification
                || retrievedItem.Owner != item.Owner)
            {
                m_log.WarnFormat(
                    "[XINVENTORY SERVICE]: Caller to UpdateItem() for {0} {1} tried to alter property(s) that should be invariant, (InvType, AssetType, Folder, CreatorIdentification, Owner), existing ({2}, {3}, {4}, {5}, {6}), update ({7}, {8}, {9}, {10}, {11})",
                    retrievedItem.Name,
                    retrievedItem.ID,
                    retrievedItem.InvType,
                    retrievedItem.AssetType,
                    retrievedItem.Folder,
                    retrievedItem.CreatorIdentification,
                    retrievedItem.Owner,
                    item.InvType,
                    item.AssetType,
                    item.Folder,
                    item.CreatorIdentification,
                    item.Owner);

                item.InvType = retrievedItem.InvType;
                item.AssetType = retrievedItem.AssetType;
                item.Folder = retrievedItem.Folder;
                item.CreatorIdentification = retrievedItem.CreatorIdentification;
                item.Owner = retrievedItem.Owner;
            }

            return m_Database.StoreItem(ConvertFromOpenSim(item));
        }

        public virtual bool MoveItems(UUID principalID, List<InventoryItemBase> items)
        {
            // Principal is b0rked. *sigh*
            //
            foreach (InventoryItemBase i in items)
            {
                m_Database.MoveItem(i.ID.ToString(), i.Folder.ToString());
            }

            return true;
        }

        public virtual bool DeleteItems(UUID principalID, List<UUID> itemIDs)
        {
            if (!m_AllowDelete)
            {
                // We must still allow links and links to folders to be deleted, otherwise they will build up
                // in the player's inventory until they can no longer log in.  Deletions of links due to code bugs or
                // similar is inconvenient but on a par with accidental movement of items.  The original item is never
                // touched.
                foreach (UUID id in itemIDs)
                {
                    if (!m_Database.DeleteItems(
                        new string[] { "inventoryID", "assetType" },
                        new string[] { id.ToString(), ((sbyte)AssetType.Link).ToString() }))
                    {
                        m_Database.DeleteItems(
                            new string[] { "inventoryID", "assetType" },
                            new string[] { id.ToString(), ((sbyte)AssetType.LinkFolder).ToString() });
                    }
                }
            }
            else
            {
                // Just use the ID... *facepalms*
                //
                foreach (UUID id in itemIDs)
                    m_Database.DeleteItems("inventoryID", id.ToString());
            }

            return true;
        }

        public virtual InventoryItemBase GetItem(UUID principalID, UUID itemID)
        {
            XInventoryItem[] items = m_Database.GetItems(
                    new string[] { "inventoryID" },
                    new string[] { itemID.ToString() });

            if (items.Length == 0)
                return null;

            return ConvertToOpenSim(items[0]);
        }

        public virtual InventoryItemBase[] GetMultipleItems(UUID userID, UUID[] ids)
        {
            InventoryItemBase[] items = new InventoryItemBase[ids.Length];
            int i = 0;
            foreach (UUID id in ids)
                items[i++] = GetItem(userID, id);

            return items;
        }

        public virtual InventoryFolderBase GetFolder(UUID principalID, UUID folderID)
        {
            XInventoryFolder[] folders = m_Database.GetFolders(
                    new string[] { "folderID"},
                    new string[] { folderID.ToString() });

            if (folders.Length == 0)
                return null;

            return ConvertToOpenSim(folders[0]);
        }

        public virtual List<InventoryItemBase> GetActiveGestures(UUID principalID)
        {
            XInventoryItem[] items = m_Database.GetActiveGestures(principalID);

            if (items.Length == 0)
                return new List<InventoryItemBase>();

            List<InventoryItemBase> ret = new List<InventoryItemBase>();

            foreach (XInventoryItem x in items)
                ret.Add(ConvertToOpenSim(x));

            return ret;
        }

        public virtual int GetAssetPermissions(UUID principalID, UUID assetID)
        {
            return m_Database.GetAssetPermissions(principalID, assetID);
        }

        // Unused.
        //
        public bool HasInventoryForUser(UUID userID)
        {
            return false;
        }

        // CM Helpers
        //
        protected InventoryFolderBase ConvertToOpenSim(XInventoryFolder folder)
        {
            InventoryFolderBase newFolder = new InventoryFolderBase();

            newFolder.ParentID = folder.parentFolderID;
            newFolder.Type = (short)folder.type;
            //// Viewer can't understand anything that's not in it's LLFolderType enum
            //if (newFolder.Type == InventoryItemBase.SUITCASE_FOLDER_TYPE)
            //    newFolder.Type = InventoryItemBase.SUITCASE_FOLDER_FAKE_TYPE;
            newFolder.Version = (ushort)folder.version;
            newFolder.Name = folder.folderName;
            newFolder.Owner = folder.agentID;
            newFolder.ID = folder.folderID;

            return newFolder;
        }

        protected XInventoryFolder ConvertFromOpenSim(InventoryFolderBase folder)
        {
            XInventoryFolder newFolder = new XInventoryFolder();

            newFolder.parentFolderID = folder.ParentID;
            newFolder.type = (int)folder.Type;
            newFolder.version = (int)folder.Version;
            newFolder.folderName = folder.Name;
            newFolder.agentID = folder.Owner;
            newFolder.folderID = folder.ID;

            return newFolder;
        }

        protected InventoryItemBase ConvertToOpenSim(XInventoryItem item)
        {
            InventoryItemBase newItem = new InventoryItemBase();

            newItem.AssetID = item.assetID;
            newItem.AssetType = item.assetType;
            newItem.Name = item.inventoryName;
            newItem.Owner = item.avatarID;
            newItem.ID = item.inventoryID;
            newItem.InvType = item.invType;
            newItem.Folder = item.parentFolderID;
            newItem.CreatorIdentification = item.creatorID;
            newItem.Description = item.inventoryDescription;
            newItem.NextPermissions = (uint)item.inventoryNextPermissions;
            newItem.CurrentPermissions = (uint)item.inventoryCurrentPermissions;
            newItem.BasePermissions = (uint)item.inventoryBasePermissions;
            newItem.EveryOnePermissions = (uint)item.inventoryEveryOnePermissions;
            newItem.GroupPermissions = (uint)item.inventoryGroupPermissions;
            newItem.GroupID = item.groupID;
            if (item.groupOwned == 0)
                newItem.GroupOwned = false;
            else
                newItem.GroupOwned = true;
            newItem.SalePrice = item.salePrice;
            newItem.SaleType = (byte)item.saleType;
            newItem.Flags = (uint)item.flags;
            newItem.CreationDate = item.creationDate;

            return newItem;
        }

        protected XInventoryItem ConvertFromOpenSim(InventoryItemBase item)
        {
            XInventoryItem newItem = new XInventoryItem();

            newItem.assetID = item.AssetID;
            newItem.assetType = item.AssetType;
            newItem.inventoryName = item.Name;
            newItem.avatarID = item.Owner;
            newItem.inventoryID = item.ID;
            newItem.invType = item.InvType;
            newItem.parentFolderID = item.Folder;
            newItem.creatorID = item.CreatorIdentification;
            newItem.inventoryDescription = item.Description;
            newItem.inventoryNextPermissions = (int)item.NextPermissions;
            newItem.inventoryCurrentPermissions = (int)item.CurrentPermissions;
            newItem.inventoryBasePermissions = (int)item.BasePermissions;
            newItem.inventoryEveryOnePermissions = (int)item.EveryOnePermissions;
            newItem.inventoryGroupPermissions = (int)item.GroupPermissions;
            newItem.groupID = item.GroupID;
            if (item.GroupOwned)
                newItem.groupOwned = 1;
            else
                newItem.groupOwned = 0;
            newItem.salePrice = item.SalePrice;
            newItem.saleType = (int)item.SaleType;
            newItem.flags = (int)item.Flags;
            newItem.creationDate = item.CreationDate;

            return newItem;
        }

        private bool ParentIsTrash(UUID folderID)
        {
            XInventoryFolder[] folder = m_Database.GetFolders(new string[] {"folderID"}, new string[] {folderID.ToString()});
            if (folder.Length < 1)
                return false;

            if (folder[0].type == (int)FolderType.Trash)
                return true;

            UUID parentFolder = folder[0].parentFolderID;

            while (parentFolder != UUID.Zero)
            {
                XInventoryFolder[] parent = m_Database.GetFolders(new string[] {"folderID"}, new string[] {parentFolder.ToString()});
                if (parent.Length < 1)
                    return false;

                if (parent[0].type == (int)FolderType.Trash)
                    return true;
                if (parent[0].type == (int)FolderType.Root)
                    return false;

                parentFolder = parent[0].parentFolderID;
            }
            return false;
        }
    }
}
