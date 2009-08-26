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

using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;

namespace OpenSim.Grid.InventoryServer
{
    /// <summary>
    /// Abstract base class used by local and grid implementations of an inventory service.
    /// </summary>
    public abstract class InventoryServiceBase : IInterServiceInventoryServices
    {

        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected List<IInventoryDataPlugin> m_plugins = new List<IInventoryDataPlugin>();

        #region Plugin methods

        /// <summary>
        /// Add a new inventory data plugin - plugins will be requested in the order they were added.
        /// </summary>
        /// <param name="plugin">The plugin that will provide data</param>
        public void AddPlugin(IInventoryDataPlugin plugin)
        {
            m_plugins.Add(plugin);
        }

        /// <summary>
        /// Adds a list of inventory data plugins, as described by `provider'
        /// and `connect', to `m_plugins'.
        /// </summary>
        /// <param name="provider">
        /// The filename of the inventory server plugin DLL.
        /// </param>
        /// <param name="connect">
        /// The connection string for the storage backend.
        /// </param>
        public void AddPlugin(string provider, string connect)
        {
            m_plugins.AddRange(DataPluginFactory.LoadDataPlugins<IInventoryDataPlugin>(provider, connect));
        }        

        #endregion

        #region IInventoryServices methods

        public string Host
        {
            get { return "default"; }
        }

        public List<InventoryFolderBase> GetInventorySkeleton(UUID userId)
        {
//            m_log.DebugFormat("[AGENT INVENTORY]: Getting inventory skeleton for {0}", userId);

            InventoryFolderBase rootFolder = RequestRootFolder(userId);

            // Agent has no inventory structure yet.
            if (null == rootFolder)
            {
                return null;
            }

            List<InventoryFolderBase> userFolders = new List<InventoryFolderBase>();

            userFolders.Add(rootFolder);

            foreach (IInventoryDataPlugin plugin in m_plugins)
            {
                IList<InventoryFolderBase> folders = plugin.getFolderHierarchy(rootFolder.ID);
                userFolders.AddRange(folders);
            }

//            foreach (InventoryFolderBase folder in userFolders)
//            {
//                m_log.DebugFormat("[AGENT INVENTORY]: Got folder {0} {1}", folder.name, folder.folderID);
//            }

            return userFolders;
        }

        // See IInventoryServices
        public virtual bool HasInventoryForUser(UUID userID)
        {
            return false;
        }

        // See IInventoryServices
        public virtual InventoryFolderBase RequestRootFolder(UUID userID)
        {
            // Retrieve the first root folder we get from the list of plugins.
            foreach (IInventoryDataPlugin plugin in m_plugins)
            {
                InventoryFolderBase rootFolder = plugin.getUserRootFolder(userID);
                if (rootFolder != null)
                    return rootFolder;
            }

            // Return nothing if no plugin was able to supply a root folder
            return null;
        }

        // See IInventoryServices
        public bool CreateNewUserInventory(UUID user)
        {
            InventoryFolderBase existingRootFolder = RequestRootFolder(user);

            if (null != existingRootFolder)
            {
                m_log.WarnFormat(
                    "[AGENT INVENTORY]: Did not create a new inventory for user {0} since they already have "
                    + "a root inventory folder with id {1}",
                    user, existingRootFolder.ID);
            }
            else
            {
                UsersInventory inven = new UsersInventory();
                inven.CreateNewInventorySet(user);
                AddNewInventorySet(inven);

                return true;
            }

            return false;
        }

        public List<InventoryItemBase> GetActiveGestures(UUID userId)
        {
            List<InventoryItemBase> activeGestures = new List<InventoryItemBase>();
            foreach (IInventoryDataPlugin plugin in m_plugins)
            {
                activeGestures.AddRange(plugin.fetchActiveGestures(userId));
            }
            
            return activeGestures;
        }

        #endregion

        #region Methods used by GridInventoryService

        public List<InventoryFolderBase> RequestSubFolders(UUID parentFolderID)
        {
            List<InventoryFolderBase> inventoryList = new List<InventoryFolderBase>();
            
            foreach (IInventoryDataPlugin plugin in m_plugins)
            {
                inventoryList.AddRange(plugin.getInventoryFolders(parentFolderID));
            }
            
            return inventoryList;
        }

        public List<InventoryItemBase> RequestFolderItems(UUID folderID)
        {
            List<InventoryItemBase> itemsList = new List<InventoryItemBase>();
            
            foreach (IInventoryDataPlugin plugin in m_plugins)
            {
                itemsList.AddRange(plugin.getInventoryInFolder(folderID));
            }
            
            return itemsList;
        }

        #endregion

        // See IInventoryServices
        public virtual bool AddFolder(InventoryFolderBase folder)
        {
            m_log.DebugFormat(
                "[AGENT INVENTORY]: Adding folder {0} {1} to folder {2}", folder.Name, folder.ID, folder.ParentID);

            foreach (IInventoryDataPlugin plugin in m_plugins)
            {
                plugin.addInventoryFolder(folder);
            }

            // FIXME: Should return false on failure
            return true;
        }

        // See IInventoryServices
        public virtual bool UpdateFolder(InventoryFolderBase folder)
        {
            m_log.DebugFormat(
                "[AGENT INVENTORY]: Updating folder {0} {1} to folder {2}", folder.Name, folder.ID, folder.ParentID);

            foreach (IInventoryDataPlugin plugin in m_plugins)
            {
                plugin.updateInventoryFolder(folder);
            }

            // FIXME: Should return false on failure
            return true;
        }

        // See IInventoryServices
        public virtual bool MoveFolder(InventoryFolderBase folder)
        {
            m_log.DebugFormat(
                "[AGENT INVENTORY]: Moving folder {0} {1} to folder {2}", folder.Name, folder.ID, folder.ParentID);

            foreach (IInventoryDataPlugin plugin in m_plugins)
            {
                plugin.moveInventoryFolder(folder);
            }

            // FIXME: Should return false on failure
            return true;
        }

        // See IInventoryServices
        public virtual bool AddItem(InventoryItemBase item)
        {
            m_log.DebugFormat(
                "[AGENT INVENTORY]: Adding item {0} {1} to folder {2}", item.Name, item.ID, item.Folder);

            foreach (IInventoryDataPlugin plugin in m_plugins)
            {
                plugin.addInventoryItem(item);
            }

            // FIXME: Should return false on failure
            return true;
        }

        // See IInventoryServices
        public virtual bool UpdateItem(InventoryItemBase item)
        {
            m_log.InfoFormat(
                "[AGENT INVENTORY]: Updating item {0} {1} in folder {2}", item.Name, item.ID, item.Folder);

            foreach (IInventoryDataPlugin plugin in m_plugins)
            {
                plugin.updateInventoryItem(item);
            }

            // FIXME: Should return false on failure
            return true;
        }

        // See IInventoryServices
        public virtual bool DeleteItem(InventoryItemBase item)
        {
            m_log.InfoFormat(
                "[AGENT INVENTORY]: Deleting item {0} {1} from folder {2}", item.Name, item.ID, item.Folder);

            foreach (IInventoryDataPlugin plugin in m_plugins)
            {
                plugin.deleteInventoryItem(item.ID);
            }

            // FIXME: Should return false on failure
            return true;
        }

        public virtual InventoryItemBase QueryItem(InventoryItemBase item)
        {
            foreach (IInventoryDataPlugin plugin in m_plugins)
            {
                InventoryItemBase result = plugin.queryInventoryItem(item.ID);
                if (result != null)
                    return result;
            }

            return null;
        }

        public virtual InventoryFolderBase QueryFolder(InventoryFolderBase item)
        {
            foreach (IInventoryDataPlugin plugin in m_plugins)
            {
                InventoryFolderBase result = plugin.queryInventoryFolder(item.ID);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Purge a folder of all items items and subfolders.
        ///
        /// FIXME: Really nasty in a sense, because we have to query the database to get information we may
        /// already know...  Needs heavy refactoring.
        /// </summary>
        /// <param name="folder"></param>
        public virtual bool PurgeFolder(InventoryFolderBase folder)
        {
            m_log.DebugFormat(
                "[AGENT INVENTORY]: Purging folder {0} {1} of its contents", folder.Name, folder.ID);

            List<InventoryFolderBase> subFolders = RequestSubFolders(folder.ID);

            foreach (InventoryFolderBase subFolder in subFolders)
            {
//                m_log.DebugFormat("[AGENT INVENTORY]: Deleting folder {0} {1}", subFolder.Name, subFolder.ID);

                foreach (IInventoryDataPlugin plugin in m_plugins)
                {
                    plugin.deleteInventoryFolder(subFolder.ID);
                }
            }

            List<InventoryItemBase> items = RequestFolderItems(folder.ID);

            foreach (InventoryItemBase item in items)
            {
                DeleteItem(item);
            }

            // FIXME: Should return false on failure
            return true;
        }

        private void AddNewInventorySet(UsersInventory inventory)
        {
            foreach (InventoryFolderBase folder in inventory.Folders.Values)
            {
                AddFolder(folder);
            }
        }

        public InventoryItemBase GetInventoryItem(UUID itemID)
        {
            foreach (IInventoryDataPlugin plugin in m_plugins)
            {
                InventoryItemBase item = plugin.getInventoryItem(itemID);
                if (item != null)
                    return item;
            }

            return null;
        }

        /// <summary>
        /// Used to create a new user inventory.
        /// </summary>
        private class UsersInventory
        {
            public Dictionary<UUID, InventoryFolderBase> Folders = new Dictionary<UUID, InventoryFolderBase>();
            public Dictionary<UUID, InventoryItemBase> Items = new Dictionary<UUID, InventoryItemBase>();

            public virtual void CreateNewInventorySet(UUID user)
            {
                InventoryFolderBase folder = new InventoryFolderBase();

                folder.ParentID = UUID.Zero;
                folder.Owner = user;
                folder.ID = UUID.Random();
                folder.Name = "My Inventory";
                folder.Type = (short)AssetType.Folder;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                UUID rootFolder = folder.ID;

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = UUID.Random();
                folder.Name = "Animations";
                folder.Type = (short)AssetType.Animation;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = UUID.Random();
                folder.Name = "Body Parts";
                folder.Type = (short)AssetType.Bodypart;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = UUID.Random();
                folder.Name = "Calling Cards";
                folder.Type = (short)AssetType.CallingCard;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = UUID.Random();
                folder.Name = "Clothing";
                folder.Type = (short)AssetType.Clothing;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = UUID.Random();
                folder.Name = "Gestures";
                folder.Type = (short)AssetType.Gesture;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = UUID.Random();
                folder.Name = "Landmarks";
                folder.Type = (short)AssetType.Landmark;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = UUID.Random();
                folder.Name = "Lost And Found";
                folder.Type = (short)AssetType.LostAndFoundFolder;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = UUID.Random();
                folder.Name = "Notecards";
                folder.Type = (short)AssetType.Notecard;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = UUID.Random();
                folder.Name = "Objects";
                folder.Type = (short)AssetType.Object;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = UUID.Random();
                folder.Name = "Photo Album";
                folder.Type = (short)AssetType.SnapshotFolder;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = UUID.Random();
                folder.Name = "Scripts";
                folder.Type = (short)AssetType.LSLText;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = UUID.Random();
                folder.Name = "Sounds";
                folder.Type = (short)AssetType.Sound;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = UUID.Random();
                folder.Name = "Textures";
                folder.Type = (short)AssetType.Texture;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = UUID.Random();
                folder.Name = "Trash";
                folder.Type = (short)AssetType.TrashFolder;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);
            }
        }
    }
}
