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
using Mono.Addins;
using Nini.Config;

using System;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Data;
using OpenSim.Server.Base;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Inventory
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LocalInventoryServicesConnector")]
    public class LocalInventoryServicesConnector : ISharedRegionModule, IInventoryService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Scene used by this module.  This currently needs to be publicly settable for HGInventoryBroker.
        /// </summary>
        public Scene Scene { get; set; }

        private IInventoryService m_InventoryService;

        private IUserManagement m_UserManager;
        private IUserManagement UserManager
        {
            get
            {
                if (m_UserManager == null)
                {
                    m_UserManager = Scene.RequestModuleInterface<IUserManagement>();
                }
                return m_UserManager;
            }
        }

        private bool m_Enabled = false;

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
                        m_log.Error("[LOCAL INVENTORY SERVICES CONNECTOR]: InventoryService missing from OpenSim.ini");
                        return;
                    }

                    string serviceDll = inventoryConfig.GetString("LocalServiceModule", String.Empty);

                    if (serviceDll == String.Empty)
                    {
                        m_log.Error("[LOCAL INVENTORY SERVICES CONNECTOR]: No LocalServiceModule named in section InventoryService");
                        return;
                    }

                    Object[] args = new Object[] { source };
                    m_log.DebugFormat("[LOCAL INVENTORY SERVICES CONNECTOR]: Service dll = {0}", serviceDll);

                    m_InventoryService = ServerUtils.LoadPlugin<IInventoryService>(serviceDll, args);

                    if (m_InventoryService == null)
                    {
                        m_log.Error("[LOCAL INVENTORY SERVICES CONNECTOR]: Can't load inventory service");
                        throw new Exception("Unable to proceed. Please make sure your ini files in config-include are updated according to .example's");
                    }

                    m_Enabled = true;
                    m_log.Info("[LOCAL INVENTORY SERVICES CONNECTOR]: Local inventory connector enabled");
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
            
            scene.RegisterModuleInterface<IInventoryService>(this);

            if (Scene == null)
                Scene = scene;
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;
        }

        #region IInventoryService

        public bool CreateUserInventory(UUID user)
        {
            return m_InventoryService.CreateUserInventory(user);
        }

        public List<InventoryFolderBase> GetInventorySkeleton(UUID userId)
        {
            return m_InventoryService.GetInventorySkeleton(userId);
        }

        public InventoryFolderBase GetRootFolder(UUID userID)
        {
            return m_InventoryService.GetRootFolder(userID);
        }

        public InventoryFolderBase GetFolderForType(UUID userID, FolderType type)
        {
            return m_InventoryService.GetFolderForType(userID, type);
        }

        public InventoryCollection GetFolderContent(UUID userID, UUID folderID)
        {
            InventoryCollection invCol = m_InventoryService.GetFolderContent(userID, folderID);

            if (UserManager != null)
            {
                // Protect ourselves against the caller subsequently modifying the items list
                List<InventoryItemBase> items = new List<InventoryItemBase>(invCol.Items);

                WorkManager.RunInThread(delegate
                {
                    foreach (InventoryItemBase item in items)
                        if (!string.IsNullOrEmpty(item.CreatorData))
                            UserManager.AddUser(item.CreatorIdAsUuid, item.CreatorData);
                }, null, string.Format("GetFolderContent (user {0}, folder {1})", userID, folderID));
            }

            return invCol;
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

        public List<InventoryItemBase> GetFolderItems(UUID userID, UUID folderID)
        {
            return m_InventoryService.GetFolderItems(userID, folderID);
        }

        /// <summary>
        /// Add a new folder to the user's inventory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully added</returns>
        public bool AddFolder(InventoryFolderBase folder)
        {
            return m_InventoryService.AddFolder(folder);
        }

        /// <summary>
        /// Update a folder in the user's inventory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully updated</returns>
        public bool UpdateFolder(InventoryFolderBase folder)
        {
            return m_InventoryService.UpdateFolder(folder);
        }

        /// <summary>
        /// Move an inventory folder to a new location
        /// </summary>
        /// <param name="folder">A folder containing the details of the new location</param>
        /// <returns>true if the folder was successfully moved</returns>
        public bool MoveFolder(InventoryFolderBase folder)
        {
            return m_InventoryService.MoveFolder(folder);
        }

        public bool DeleteFolders(UUID ownerID, List<UUID> folderIDs)
        {
            return m_InventoryService.DeleteFolders(ownerID, folderIDs);
        }

        /// <summary>
        /// Purge an inventory folder of all its items and subfolders.
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully purged</returns>
        public bool PurgeFolder(InventoryFolderBase folder)
        {
            return m_InventoryService.PurgeFolder(folder);
        }

        public bool AddItem(InventoryItemBase item)
        {
//            m_log.DebugFormat(
//                "[LOCAL INVENTORY SERVICES CONNECTOR]: Adding inventory item {0} to user {1} folder {2}", 
//                item.Name, item.Owner, item.Folder);
            
            return m_InventoryService.AddItem(item);
        }

        /// <summary>
        /// Update an item in the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully updated</returns>
        public bool UpdateItem(InventoryItemBase item)
        {
            return m_InventoryService.UpdateItem(item);
        }

        public bool MoveItems(UUID ownerID, List<InventoryItemBase> items)
        {
            return m_InventoryService.MoveItems(ownerID, items);
        }

        /// <summary>
        /// Delete an item from the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully deleted</returns>
        public bool DeleteItems(UUID ownerID, List<UUID> itemIDs)
        {
            return m_InventoryService.DeleteItems(ownerID, itemIDs);
        }

        public InventoryItemBase GetItem(InventoryItemBase item)
        {
//            m_log.DebugFormat("[LOCAL INVENTORY SERVICES CONNECTOR]: Requesting inventory item {0}", item.ID);

//            UUID requestedItemId = item.ID;
            
            item = m_InventoryService.GetItem(item);

//            if (null == item)
//                m_log.ErrorFormat(
//                    "[LOCAL INVENTORY SERVICES CONNECTOR]: Could not find item with id {0}", requestedItemId);

            return item;
        }

        public InventoryItemBase[] GetMultipleItems(UUID userID, UUID[] itemIDs)
        {
            return m_InventoryService.GetMultipleItems(userID, itemIDs);
        }

        public InventoryFolderBase GetFolder(InventoryFolderBase folder)
        {
            return m_InventoryService.GetFolder(folder);
        }

        /// <summary>
        /// Does the given user have an inventory structure?
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public bool HasInventoryForUser(UUID userID)
        {
            return m_InventoryService.HasInventoryForUser(userID);
        }

        public List<InventoryItemBase> GetActiveGestures(UUID userId)
        {
            return m_InventoryService.GetActiveGestures(userId);
        }

        public int GetAssetPermissions(UUID userID, UUID assetID)
        {
            return m_InventoryService.GetAssetPermissions(userID, assetID);
        }
        #endregion IInventoryService
    }
}
