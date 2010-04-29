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
using System;
using System.Collections.Generic;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Statistics;

using OpenSim.Services.Connectors;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Inventory
{
    public class RemoteXInventoryServicesConnector : BaseInventoryConnector, ISharedRegionModule, IInventoryService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;
        private bool m_Initialized = false;
        private Scene m_Scene;
        private XInventoryServicesConnector m_RemoteConnector;

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "RemoteXInventoryServicesConnector"; }
        }

        public RemoteXInventoryServicesConnector()
        {
        }

        public RemoteXInventoryServicesConnector(string url)
        {
            m_RemoteConnector = new XInventoryServicesConnector(url);
        }

        public RemoteXInventoryServicesConnector(IConfigSource source)
        {
            Init(source);
        }

        protected override void Init(IConfigSource source)
        {
            m_RemoteConnector = new XInventoryServicesConnector(source);
            base.Init(source);
        }


        #region ISharedRegionModule

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("InventoryServices", "");
                if (name == Name)
                {
                    Init(source);
                    m_Enabled = true;

                    m_log.Info("[XINVENTORY CONNECTOR]: Remote XInventory enabled");
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
            m_Scene = scene;
            //m_log.Debug("[XXXX] Adding scene " + m_Scene.RegionInfo.RegionName);

            if (!m_Enabled)
                return;

            if (!m_Initialized)
            {
                m_Initialized = true;
            }

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

            m_log.InfoFormat("[XINVENTORY CONNECTOR]: Enabled remote XInventory for region {0}", scene.RegionInfo.RegionName);

        }

        #endregion ISharedRegionModule

        #region IInventoryService

        public override bool CreateUserInventory(UUID user)
        {
            return false;
        }

        public override List<InventoryFolderBase> GetInventorySkeleton(UUID userId)
        {
            return new List<InventoryFolderBase>();
        }

        public override InventoryCollection GetUserInventory(UUID userID)
        {
            return null;
        }

        public override void GetUserInventory(UUID userID, InventoryReceiptCallback callback)
        {
            try
            {
                m_RemoteConnector.GetUserInventory(userID, callback);
            }
            catch (Exception e)
            {
                if (StatsManager.SimExtraStats != null)
                    StatsManager.SimExtraStats.AddInventoryServiceRetrievalFailure();

                m_log.ErrorFormat("[XINVENTORY CONNECTOR]: Request inventory operation failed, {0} {1}",
                    e.Source, e.Message);
            }

        }

        // inherited. See base class
        // public InventoryFolderBase GetFolderForType(UUID userID, AssetType type)

        public override Dictionary<AssetType, InventoryFolderBase> GetSystemFolders(UUID userID)
        {
            return m_RemoteConnector.GetSystemFolders(userID);
        }

        public override InventoryCollection GetFolderContent(UUID userID, UUID folderID)
        {
            m_log.DebugFormat("[XINVENTORY CONNECTOR]: GetFolderContent {0}", folderID);
            try
            {
                return m_RemoteConnector.GetFolderContent(userID, folderID);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[XINVENTORY CONNECTOR]: GetFolderContent operation failed, {0} {1}",
                    e.Source, e.Message);
            }
            InventoryCollection nullCollection = new InventoryCollection();
            nullCollection.Folders = new List<InventoryFolderBase>();
            nullCollection.Items = new List<InventoryItemBase>();
            nullCollection.UserID = userID;
            return nullCollection;
        }

        public override List<InventoryItemBase> GetFolderItems(UUID userID, UUID folderID)
        {
            return m_RemoteConnector.GetFolderItems(userID, folderID);
        }

        public override bool AddFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return false;

            return m_RemoteConnector.AddFolder(folder);
        }

        public override bool UpdateFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return false;

            return m_RemoteConnector.UpdateFolder(folder);
        }

        public override bool MoveFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return false;

            return m_RemoteConnector.MoveFolder(folder);
        }

        public override bool DeleteFolders(UUID ownerID, List<UUID> folderIDs)
        {
            if (folderIDs == null)
                return false;
            if (folderIDs.Count == 0)
                return false;

            return m_RemoteConnector.DeleteFolders(ownerID, folderIDs);
        }


        public override bool PurgeFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return false;

            return m_RemoteConnector.PurgeFolder(folder);
        }

        // public bool AddItem(InventoryItemBase item) inherited
        // Uses AddItemPlain

        protected override bool AddItemPlain(InventoryItemBase item)
        {
            if (item == null)
                return false;

            return m_RemoteConnector.AddItem(item);
        }

        public override bool UpdateItem(InventoryItemBase item)
        {
            if (item == null)
                return false;

            return m_RemoteConnector.UpdateItem(item);
        }

        public override bool MoveItems(UUID ownerID, List<InventoryItemBase> items)
        {
            if (items == null)
                return false;

            return m_RemoteConnector.MoveItems(ownerID, items);
        }


        public override bool DeleteItems(UUID ownerID, List<UUID> itemIDs)
        {
            if (itemIDs == null)
                return false;
            if (itemIDs.Count == 0)
                return true;

            return m_RemoteConnector.DeleteItems(ownerID, itemIDs);
        }

        public override InventoryItemBase GetItem(InventoryItemBase item)
        {
            if (item == null)
                return null;

            return m_RemoteConnector.GetItem(item);
        }

        public override InventoryFolderBase GetFolder(InventoryFolderBase folder)
        {
            m_log.DebugFormat("[XINVENTORY CONNECTOR]: GetFolder {0}", folder.ID);
            if (folder == null)
                return null;

            return m_RemoteConnector.GetFolder(folder);
        }

        public override bool HasInventoryForUser(UUID userID)
        {
            return false;
        }

        public override List<InventoryItemBase> GetActiveGestures(UUID userId)
        {
            return new List<InventoryItemBase>();
        }

        public override int GetAssetPermissions(UUID userID, UUID assetID)
        {
            return m_RemoteConnector.GetAssetPermissions(userID, assetID);
        }


        #endregion


    }
}
