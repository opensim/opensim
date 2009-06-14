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
    public class RemoteInventoryServicesConnector : ISharedRegionModule, IInventoryService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;
        private bool m_Initialized = false;
        private Scene m_Scene;
        private InventoryServicesConnector m_RemoteConnector;

        public string Name
        {
            get { return "RemoteInventoryServicesConnector"; }
        }

        public RemoteInventoryServicesConnector()
        {
        }

        public RemoteInventoryServicesConnector(IConfigSource source)
        {
            Init(source);
        }

        private void Init(IConfigSource source)
        {
            m_RemoteConnector = new InventoryServicesConnector(source);
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

                    m_log.Info("[INVENTORY CONNECTOR]: Remote inventory enabled");
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
                m_Scene = scene;
                // ugh!
                scene.CommsManager.UserProfileCacheService.SetInventoryService(this);
                scene.CommsManager.UserService.SetInventoryService(this); 
                m_Initialized = true;
            }

            scene.RegisterModuleInterface<IInventoryService>(this);
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_log.InfoFormat("[INVENTORY CONNECTOR]: Enabled remote inventory for region {0}", scene.RegionInfo.RegionName);

        }

        #endregion ISharedRegionModule

        #region IInventoryService

        public bool CreateUserInventory(UUID user)
        {
            return false;
        }

        public List<InventoryFolderBase> GetInventorySkeleton(UUID userId)
        {
            return new List<InventoryFolderBase>();
        }

        public InventoryCollection GetUserInventory(UUID userID)
        {
            return null;
        }

        public void GetUserInventory(UUID userID, InventoryReceiptCallback callback)
        {
            UUID sessionID = GetSessionID(userID);
            try
            {
                m_RemoteConnector.GetUserInventory(userID.ToString(), sessionID, callback);
            }
            catch (Exception e)
            {
                if (StatsManager.SimExtraStats != null)
                    StatsManager.SimExtraStats.AddInventoryServiceRetrievalFailure();

                m_log.ErrorFormat("[INVENTORY CONNECTOR]: Request inventory operation failed, {0} {1}",
                    e.Source, e.Message);
            }

        }

        public List<InventoryItemBase> GetFolderItems(UUID userID, UUID folderID)
        {
            return new List<InventoryItemBase>();
        }

        public bool AddFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return false;

            UUID sessionID = GetSessionID(folder.Owner);
            return m_RemoteConnector.AddFolder(folder.Owner.ToString(), folder, sessionID);
        }

        public bool UpdateFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return false;

            UUID sessionID = GetSessionID(folder.Owner);
            return m_RemoteConnector.UpdateFolder(folder.Owner.ToString(), folder, sessionID);
        }

        public bool MoveFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return false;

            UUID sessionID = GetSessionID(folder.Owner);
            return m_RemoteConnector.MoveFolder(folder.Owner.ToString(), folder, sessionID);
        }

        public bool PurgeFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return false;

            UUID sessionID = GetSessionID(folder.Owner);
            return m_RemoteConnector.PurgeFolder(folder.Owner.ToString(), folder, sessionID);
        }

        public bool AddItem(InventoryItemBase item)
        {
            if (item == null)
                return false;

            UUID sessionID = GetSessionID(item.Owner);
            return m_RemoteConnector.AddItem(item.Owner.ToString(), item, sessionID);
        }

        public bool UpdateItem(InventoryItemBase item)
        {
            if (item == null)
                return false;

            UUID sessionID = GetSessionID(item.Owner);
            return m_RemoteConnector.UpdateItem(item.Owner.ToString(), item, sessionID);
        }

        public bool DeleteItem(InventoryItemBase item)
        {
            if (item == null)
                return false;

            UUID sessionID = GetSessionID(item.Owner);
            return m_RemoteConnector.DeleteItem(item.Owner.ToString(), item, sessionID);
        }

        public InventoryItemBase QueryItem(InventoryItemBase item)
        {
            if (item == null)
                return null;

            UUID sessionID = GetSessionID(item.Owner);
            return m_RemoteConnector.QueryItem(item.Owner.ToString(), item, sessionID);
        }

        public InventoryFolderBase QueryFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return null;

            UUID sessionID = GetSessionID(folder.Owner);
            return m_RemoteConnector.QueryFolder(folder.Owner.ToString(), folder, sessionID);
        }

        public bool HasInventoryForUser(UUID userID)
        {
            return false;
        }

        public InventoryFolderBase RequestRootFolder(UUID userID)
        {
            return null;
        }

        public List<InventoryItemBase> GetActiveGestures(UUID userId)
        {
            return new List<InventoryItemBase>();
        }

        #endregion

        private UUID GetSessionID(UUID userID)
        {
            ScenePresence sp = m_Scene.GetScenePresence(userID);
            if (sp != null)
                return sp.ControllingClient.SessionId;

            return UUID.Zero;
        }

    }
}
