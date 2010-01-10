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

using log4net;
using Nini.Config;

using System;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Data;
using OpenSim.Server.Base;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Inventory
{
    public class LocalInventoryServicesConnector : BaseInventoryConnector, ISharedRegionModule, IInventoryService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private IInventoryService m_InventoryService;

        private bool m_Enabled = false;
        private bool m_Initialized = false;

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "LocalInventoryServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("InventoryServices", "");
                if (name == Name)
                {
                    IConfig inventoryConfig = source.Configs["InventoryService"];
                    if (inventoryConfig == null)
                    {
                        m_log.Error("[INVENTORY CONNECTOR]: InventoryService missing from OpenSim.ini");
                        return;
                    }

                    string serviceDll = inventoryConfig.GetString("LocalServiceModule", String.Empty);

                    if (serviceDll == String.Empty)
                    {
                        m_log.Error("[INVENTORY CONNECTOR]: No LocalServiceModule named in section InventoryService");
                        return;
                    }

                    Object[] args = new Object[] { source };
                    m_log.DebugFormat("[INVENTORY CONNECTOR]: Service dll = {0}", serviceDll);

                    m_InventoryService = ServerUtils.LoadPlugin<IInventoryService>(serviceDll, args);

                    if (m_InventoryService == null)
                    {
                        m_log.Error("[INVENTORY CONNECTOR]: Can't load inventory service");
                        //return;
                        throw new Exception("Unable to proceed. Please make sure your ini files in config-include are updated according to .example's");
                    }

                    //List<IInventoryDataPlugin> plugins
                    //    = DataPluginFactory.LoadDataPlugins<IInventoryDataPlugin>(
                    //        configSettings.StandaloneInventoryPlugin,
                    //        configSettings.StandaloneInventorySource);

                    //foreach (IInventoryDataPlugin plugin in plugins)
                    //{
                    //    // Using the OSP wrapper plugin for database plugins should be made configurable at some point
                    //    m_InventoryService.AddPlugin(new OspInventoryWrapperPlugin(plugin, this));
                    //}

                    Init(source);

                    m_Enabled = true;
                    m_log.Info("[INVENTORY CONNECTOR]: Local inventory connector enabled");
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            if (!m_Initialized)
            {
                m_Initialized = true;
            }

//            m_log.DebugFormat(
//                "[INVENTORY CONNECTOR]: Registering IInventoryService to scene {0}", scene.RegionInfo.RegionName);
            
            scene.RegisterModuleInterface<IInventoryService>(this);
            m_cache.AddRegion(scene);
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_cache.RemoveRegion(scene);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_log.InfoFormat(
                "[INVENTORY CONNECTOR]: Enabled local invnetory for region {0}", scene.RegionInfo.RegionName);
        }

        #region IInventoryService

        public override bool CreateUserInventory(UUID user)
        {
            return m_InventoryService.CreateUserInventory(user);
        }

        public override List<InventoryFolderBase> GetInventorySkeleton(UUID userId)
        {
            return m_InventoryService.GetInventorySkeleton(userId);
        }

        public override InventoryCollection GetUserInventory(UUID id)
        {
            return m_InventoryService.GetUserInventory(id);
        }

        public override void GetUserInventory(UUID userID, InventoryReceiptCallback callback)
        {
            m_InventoryService.GetUserInventory(userID, callback);
        }

        // Inherited. See base
        //public InventoryFolderBase GetFolderForType(UUID userID, AssetType type)
        //{
        //    return m_InventoryService.GetFolderForType(userID, type);
        //}

        public override Dictionary<AssetType, InventoryFolderBase> GetSystemFolders(UUID userID)
        {
            InventoryFolderBase root = m_InventoryService.GetRootFolder(userID);
            if (root != null)
            {
                InventoryCollection content = GetFolderContent(userID, root.ID);
                if (content != null)
                {
                    Dictionary<AssetType, InventoryFolderBase> folders = new Dictionary<AssetType, InventoryFolderBase>();
                    foreach (InventoryFolderBase folder in content.Folders)
                    {
                        if ((folder.Type != (short)AssetType.Folder) && (folder.Type != (short)AssetType.Unknown))
                        {
                            //m_log.InfoFormat("[INVENTORY CONNECTOR]: folder type {0} ", folder.Type);
                            folders[(AssetType)folder.Type] = folder;
                        }
                    }
                    // Put the root folder there, as type Folder
                    folders[AssetType.Folder] = root;
                    //m_log.InfoFormat("[INVENTORY CONNECTOR]: root folder is type {0} ", root.Type);

                    return folders;
                }
            }
            m_log.WarnFormat("[INVENTORY CONNECTOR]: System folders for {0} not found", userID);
            return new Dictionary<AssetType, InventoryFolderBase>();
        }

        public override InventoryCollection GetFolderContent(UUID userID, UUID folderID)
        {
            return m_InventoryService.GetFolderContent(userID, folderID);
        }


        public override List<InventoryItemBase> GetFolderItems(UUID userID, UUID folderID)
        {
            return m_InventoryService.GetFolderItems(userID, folderID);
        }

        /// <summary>
        /// Add a new folder to the user's inventory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully added</returns>
        public override bool AddFolder(InventoryFolderBase folder)
        {
            return m_InventoryService.AddFolder(folder);
        }

        /// <summary>
        /// Update a folder in the user's inventory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully updated</returns>
        public override bool UpdateFolder(InventoryFolderBase folder)
        {
            return m_InventoryService.UpdateFolder(folder);
        }

        /// <summary>
        /// Move an inventory folder to a new location
        /// </summary>
        /// <param name="folder">A folder containing the details of the new location</param>
        /// <returns>true if the folder was successfully moved</returns>
        public override bool MoveFolder(InventoryFolderBase folder)
        {
            return m_InventoryService.MoveFolder(folder);
        }

        public override bool DeleteFolders(UUID ownerID, List<UUID> folderIDs)
        {
            return m_InventoryService.DeleteFolders(ownerID, folderIDs);
        }

        /// <summary>
        /// Purge an inventory folder of all its items and subfolders.
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully purged</returns>
        public override bool PurgeFolder(InventoryFolderBase folder)
        {
            return m_InventoryService.PurgeFolder(folder);
        }

        /// <summary>
        /// Add a new item to the user's inventory, plain
        /// Called by base class AddItem
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully added</returns>
        protected override bool AddItemPlain(InventoryItemBase item)
        {
            return m_InventoryService.AddItem(item);
        }

        /// <summary>
        /// Update an item in the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully updated</returns>
        public override bool UpdateItem(InventoryItemBase item)
        {
            return m_InventoryService.UpdateItem(item);
        }


        public override bool MoveItems(UUID ownerID, List<InventoryItemBase> items)
        {
            return m_InventoryService.MoveItems(ownerID, items);
        }

        /// <summary>
        /// Delete an item from the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully deleted</returns>
        public override bool DeleteItems(UUID ownerID, List<UUID> itemIDs)
        {
            return m_InventoryService.DeleteItems(ownerID, itemIDs);
        }

        public override InventoryItemBase GetItem(InventoryItemBase item)
        {
            return m_InventoryService.GetItem(item);
        }

        public override InventoryFolderBase GetFolder(InventoryFolderBase folder)
        {
            return m_InventoryService.GetFolder(folder);
        }

        /// <summary>
        /// Does the given user have an inventory structure?
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public override bool HasInventoryForUser(UUID userID)
        {
            return m_InventoryService.HasInventoryForUser(userID);
        }

        public override List<InventoryItemBase> GetActiveGestures(UUID userId)
        {
            return m_InventoryService.GetActiveGestures(userId);
        }

        public override int GetAssetPermissions(UUID userID, UUID assetID)
        {
            return m_InventoryService.GetAssetPermissions(userID, assetID);
        }
        #endregion IInventoryService
    }
}
