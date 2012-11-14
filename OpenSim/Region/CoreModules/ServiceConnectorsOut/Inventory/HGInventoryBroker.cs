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

using OpenSim.Server.Base;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors;
using OpenSim.Services.Connectors.SimianGrid;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Inventory
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "HGInventoryBroker")]
    public class HGInventoryBroker : ISharedRegionModule, IInventoryService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private static bool m_Enabled = false;

        private static IInventoryService m_LocalGridInventoryService;
        private Dictionary<string, IInventoryService> m_connectors = new Dictionary<string, IInventoryService>();

        // A cache of userIDs --> ServiceURLs, for HGBroker only
        protected Dictionary<UUID, string> m_InventoryURLs = new Dictionary<UUID,string>();

        private List<Scene> m_Scenes = new List<Scene>();

        private InventoryCache m_Cache = new InventoryCache();

        protected IUserManagement m_UserManagement;
        protected IUserManagement UserManagementModule
        {
            get
            {
                if (m_UserManagement == null)
                {
                    m_UserManagement = m_Scenes[0].RequestModuleInterface<IUserManagement>();

                    if (m_UserManagement == null)
                        m_log.ErrorFormat(
                            "[HG INVENTORY CONNECTOR]: Could not retrieve IUserManagement module from {0}",
                            m_Scenes[0].RegionInfo.RegionName);
                }

                return m_UserManagement;
            }
        }

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "HGInventoryBroker"; }
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
                        m_log.Error("[HG INVENTORY CONNECTOR]: InventoryService missing from OpenSim.ini");
                        return;
                    }

                    string localDll = inventoryConfig.GetString("LocalGridInventoryService",
                            String.Empty);
                    //string HGDll = inventoryConfig.GetString("HypergridInventoryService",
                    //        String.Empty);

                    if (localDll == String.Empty)
                    {
                        m_log.Error("[HG INVENTORY CONNECTOR]: No LocalGridInventoryService named in section InventoryService");
                        //return;
                        throw new Exception("Unable to proceed. Please make sure your ini files in config-include are updated according to .example's");
                    }

                    Object[] args = new Object[] { source };
                    m_LocalGridInventoryService =
                            ServerUtils.LoadPlugin<IInventoryService>(localDll,
                            args);

                    if (m_LocalGridInventoryService == null)
                    {
                        m_log.Error("[HG INVENTORY CONNECTOR]: Can't load local inventory service");
                        return;
                    }

                    m_Enabled = true;
                    m_log.InfoFormat("[HG INVENTORY CONNECTOR]: HG inventory broker enabled with inner connector of type {0}", m_LocalGridInventoryService.GetType());
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

            m_Scenes.Add(scene);

            scene.RegisterModuleInterface<IInventoryService>(this);

            if (m_Scenes.Count == 1)
            {
                // FIXME: The local connector needs the scene to extract the UserManager.  However, it's not enabled so
                // we can't just add the region.  But this approach is super-messy.
                if (m_LocalGridInventoryService is RemoteXInventoryServicesConnector)
                {
                    m_log.DebugFormat(
                        "[HG INVENTORY BROKER]: Manually setting scene in RemoteXInventoryServicesConnector to {0}",
                        scene.RegionInfo.RegionName);

                    ((RemoteXInventoryServicesConnector)m_LocalGridInventoryService).Scene = scene;
                }
                else if (m_LocalGridInventoryService is LocalInventoryServicesConnector)
                {
                    m_log.DebugFormat(
                        "[HG INVENTORY BROKER]: Manually setting scene in LocalInventoryServicesConnector to {0}",
                        scene.RegionInfo.RegionName);

                    ((LocalInventoryServicesConnector)m_LocalGridInventoryService).Scene = scene;
                }

                scene.EventManager.OnClientClosed += OnClientClosed;
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_Scenes.Remove(scene);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_log.InfoFormat("[HG INVENTORY CONNECTOR]: Enabled HG inventory for region {0}", scene.RegionInfo.RegionName);

        }

        #region URL Cache

        void OnClientClosed(UUID clientID, Scene scene)
        {
            if (m_InventoryURLs.ContainsKey(clientID)) // if it's in cache
            {
                ScenePresence sp = null;
                foreach (Scene s in m_Scenes)
                {
                    s.TryGetScenePresence(clientID, out sp);
                    if ((sp != null) && !sp.IsChildAgent && (s != scene))
                    {
                        m_log.DebugFormat("[INVENTORY CACHE]: OnClientClosed in {0}, but user {1} still in sim. Keeping inventoryURL in cache",
                            scene.RegionInfo.RegionName, clientID);
                        return;
                    }
                }
                DropInventoryServiceURL(clientID);
            }
        }

        /// <summary>
        /// Gets the user's inventory URL from its serviceURLs, if the user is foreign,
        /// and sticks it in the cache
        /// </summary>
        /// <param name="userID"></param>
        private void CacheInventoryServiceURL(UUID userID)
        {
            if (UserManagementModule != null && !UserManagementModule.IsLocalGridUser(userID))
            {
                // The user is not local; let's cache its service URL
                string inventoryURL = string.Empty;
                ScenePresence sp = null;
                foreach (Scene scene in m_Scenes)
                {
                    scene.TryGetScenePresence(userID, out sp);
                    if (sp != null)
                    {
                        AgentCircuitData aCircuit = scene.AuthenticateHandler.GetAgentCircuitData(sp.ControllingClient.CircuitCode);
                        if (aCircuit.ServiceURLs.ContainsKey("InventoryServerURI"))
                        {
                            inventoryURL = aCircuit.ServiceURLs["InventoryServerURI"].ToString();
                            if (inventoryURL != null && inventoryURL != string.Empty)
                            {
                                inventoryURL = inventoryURL.Trim(new char[] { '/' });
                                m_InventoryURLs.Add(userID, inventoryURL);
                                m_log.DebugFormat("[HG INVENTORY CONNECTOR]: Added {0} to the cache of inventory URLs", inventoryURL);
                                return;
                            }
                        }
//                        else
//                        {
//                            m_log.DebugFormat("[HG INVENTORY CONNECTOR]: User {0} does not have InventoryServerURI. OH NOES!", userID);
//                            return;
//                        }
                    }
                }
                if (sp == null)
                {
                    inventoryURL = UserManagementModule.GetUserServerURL(userID, "InventoryServerURI");
                    if (inventoryURL != null && inventoryURL != string.Empty)
                    {
                        inventoryURL = inventoryURL.Trim(new char[] { '/' });
                        m_InventoryURLs.Add(userID, inventoryURL);
                        m_log.DebugFormat("[HG INVENTORY CONNECTOR]: Added {0} to the cache of inventory URLs", inventoryURL);
                    }

                }

            }
        }

        private void DropInventoryServiceURL(UUID userID)
        {
            lock (m_InventoryURLs)
                if (m_InventoryURLs.ContainsKey(userID))
                {
                    string url = m_InventoryURLs[userID];
                    m_InventoryURLs.Remove(userID);
                    m_log.DebugFormat("[HG INVENTORY CONNECTOR]: Removed {0} from the cache of inventory URLs", url);
                }
        }

        public string GetInventoryServiceURL(UUID userID)
        {
            if (m_InventoryURLs.ContainsKey(userID))
                return m_InventoryURLs[userID];

            CacheInventoryServiceURL(userID);

            if (m_InventoryURLs.ContainsKey(userID))
                return m_InventoryURLs[userID];

            return null; //it means that the methods should forward to local grid's inventory
 
        }
        #endregion

        #region IInventoryService

        public bool CreateUserInventory(UUID userID)
        {
            return m_LocalGridInventoryService.CreateUserInventory(userID);
        }

        public List<InventoryFolderBase> GetInventorySkeleton(UUID userID)
        {
            string invURL = GetInventoryServiceURL(userID);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.GetInventorySkeleton(userID);

            IInventoryService connector = GetConnector(invURL);

            return connector.GetInventorySkeleton(userID);
        }

        public InventoryCollection GetUserInventory(UUID userID)
        {
            string invURL = GetInventoryServiceURL(userID);
            m_log.DebugFormat("[HG INVENTORY CONNECTOR]: GetUserInventory for {0} {1}", userID, invURL);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.GetUserInventory(userID);

            InventoryCollection c = m_Cache.GetUserInventory(userID);
            if (c != null)
                return c;

            IInventoryService connector = GetConnector(invURL);
            c = connector.GetUserInventory(userID);

            m_Cache.Cache(userID, c);
            return c;
        }

        public void GetUserInventory(UUID userID, InventoryReceiptCallback callback)
        {
        }

        public InventoryFolderBase GetRootFolder(UUID userID)
        {
            //m_log.DebugFormat("[HG INVENTORY CONNECTOR]: GetRootFolder for {0}", userID);
            InventoryFolderBase root = m_Cache.GetRootFolder(userID);
            if (root != null)
                return root;

            string invURL = GetInventoryServiceURL(userID);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.GetRootFolder(userID);

            IInventoryService connector = GetConnector(invURL);

            root = connector.GetRootFolder(userID);

            m_Cache.Cache(userID, root);

            return root;
        }

        public InventoryFolderBase GetFolderForType(UUID userID, AssetType type)
        {
            //m_log.DebugFormat("[HG INVENTORY CONNECTOR]: GetFolderForType {0} type {1}", userID, type);
            InventoryFolderBase f = m_Cache.GetFolderForType(userID, type);
            if (f != null)
                return f;

            string invURL = GetInventoryServiceURL(userID);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.GetFolderForType(userID, type);

            IInventoryService connector = GetConnector(invURL);

            f = connector.GetFolderForType(userID, type);

            m_Cache.Cache(userID, type, f);

            return f;
        }

        public InventoryCollection GetFolderContent(UUID userID, UUID folderID)
        {
            //m_log.Debug("[HG INVENTORY CONNECTOR]: GetFolderContent " + folderID);

            string invURL = GetInventoryServiceURL(userID);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.GetFolderContent(userID, folderID);

            InventoryCollection c = m_Cache.GetFolderContent(userID, folderID);
            if (c != null)
            {
                m_log.Debug("[HG INVENTORY CONNECTOR]: GetFolderContent found content in cache " + folderID);
                return c;
            }

            IInventoryService connector = GetConnector(invURL);
            return connector.GetFolderContent(userID, folderID);

        }

        public List<InventoryItemBase> GetFolderItems(UUID userID, UUID folderID)
        {
            //m_log.Debug("[HG INVENTORY CONNECTOR]: GetFolderItems " + folderID);

            string invURL = GetInventoryServiceURL(userID);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.GetFolderItems(userID, folderID);

            List<InventoryItemBase> items = m_Cache.GetFolderItems(userID, folderID);
            if (items != null)
            {
                m_log.Debug("[HG INVENTORY CONNECTOR]: GetFolderItems found items in cache " + folderID);
                return items;
            }

            IInventoryService connector = GetConnector(invURL);
            return connector.GetFolderItems(userID, folderID);

        }

        public bool AddFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return false;

            //m_log.Debug("[HG INVENTORY CONNECTOR]: AddFolder " + folder.ID);

            string invURL = GetInventoryServiceURL(folder.Owner);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.AddFolder(folder);

            IInventoryService connector = GetConnector(invURL);

            return connector.AddFolder(folder);
        }

        public bool UpdateFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return false;

            //m_log.Debug("[HG INVENTORY CONNECTOR]: UpdateFolder " + folder.ID);

            string invURL = GetInventoryServiceURL(folder.Owner);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.UpdateFolder(folder);

            IInventoryService connector = GetConnector(invURL);

            return connector.UpdateFolder(folder);
        }

        public bool DeleteFolders(UUID ownerID, List<UUID> folderIDs)
        {
            if (folderIDs == null)
                return false;
            if (folderIDs.Count == 0)
                return false;

            //m_log.Debug("[HG INVENTORY CONNECTOR]: DeleteFolders for " + ownerID);

            string invURL = GetInventoryServiceURL(ownerID);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.DeleteFolders(ownerID, folderIDs);

            IInventoryService connector = GetConnector(invURL);

            return connector.DeleteFolders(ownerID, folderIDs);
        }

        public bool MoveFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return false;

            //m_log.Debug("[HG INVENTORY CONNECTOR]: MoveFolder for " + folder.Owner);

            string invURL = GetInventoryServiceURL(folder.Owner);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.MoveFolder(folder);

            IInventoryService connector = GetConnector(invURL);

            return connector.MoveFolder(folder);
        }

        public bool PurgeFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return false;

            //m_log.Debug("[HG INVENTORY CONNECTOR]: PurgeFolder for " + folder.Owner);

            string invURL = GetInventoryServiceURL(folder.Owner);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.PurgeFolder(folder);

            IInventoryService connector = GetConnector(invURL);

            return connector.PurgeFolder(folder);
        }

        public bool AddItem(InventoryItemBase item)
        {
            if (item == null)
                return false;

            //m_log.Debug("[HG INVENTORY CONNECTOR]: AddItem " + item.ID);

            string invURL = GetInventoryServiceURL(item.Owner);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.AddItem(item);

            IInventoryService connector = GetConnector(invURL);

            return connector.AddItem(item);
        }

        public bool UpdateItem(InventoryItemBase item)
        {
            if (item == null)
                return false;

            //m_log.Debug("[HG INVENTORY CONNECTOR]: UpdateItem " + item.ID);

            string invURL = GetInventoryServiceURL(item.Owner);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.UpdateItem(item);

            IInventoryService connector = GetConnector(invURL);

            return connector.UpdateItem(item);
        }

        public bool MoveItems(UUID ownerID, List<InventoryItemBase> items)
        {
            if (items == null)
                return false;
            if (items.Count == 0)
                return true;

            //m_log.Debug("[HG INVENTORY CONNECTOR]: MoveItems for " + ownerID);

            string invURL = GetInventoryServiceURL(ownerID);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.MoveItems(ownerID, items);

            IInventoryService connector = GetConnector(invURL);

            return connector.MoveItems(ownerID, items);
        }

        public bool DeleteItems(UUID ownerID, List<UUID> itemIDs)
        {
            //m_log.DebugFormat("[HG INVENTORY CONNECTOR]: Delete {0} items for user {1}", itemIDs.Count, ownerID);

            if (itemIDs == null)
                return false;
            if (itemIDs.Count == 0)
                return true;

            string invURL = GetInventoryServiceURL(ownerID);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.DeleteItems(ownerID, itemIDs);

            IInventoryService connector = GetConnector(invURL);

            return connector.DeleteItems(ownerID, itemIDs);
        }

        public InventoryItemBase GetItem(InventoryItemBase item)
        {
            if (item == null)
                return null;
            //m_log.Debug("[HG INVENTORY CONNECTOR]: GetItem " + item.ID);

            string invURL = GetInventoryServiceURL(item.Owner);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.GetItem(item);

            IInventoryService connector = GetConnector(invURL);

            return connector.GetItem(item);
        }

        public InventoryFolderBase GetFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return null;

            //m_log.Debug("[HG INVENTORY CONNECTOR]: GetFolder " + folder.ID);

            string invURL = GetInventoryServiceURL(folder.Owner);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.GetFolder(folder);

            IInventoryService connector = GetConnector(invURL);

            return connector.GetFolder(folder);
        }

        public bool HasInventoryForUser(UUID userID)
        {
            return false;
        }

        public List<InventoryItemBase> GetActiveGestures(UUID userId)
        {
            return new List<InventoryItemBase>();
        }

        public int GetAssetPermissions(UUID userID, UUID assetID)
        {
            //m_log.Debug("[HG INVENTORY CONNECTOR]: GetAssetPermissions " + assetID);

            string invURL = GetInventoryServiceURL(userID);

            if (invURL == null) // not there, forward to local inventory connector to resolve
                return m_LocalGridInventoryService.GetAssetPermissions(userID, assetID);

            IInventoryService connector = GetConnector(invURL);

            return connector.GetAssetPermissions(userID, assetID);
        }

        #endregion

        private IInventoryService GetConnector(string url)
        {
            IInventoryService connector = null;
            lock (m_connectors)
            {
                if (m_connectors.ContainsKey(url))
                {
                    connector = m_connectors[url];
                }
                else
                {
                    // Still not as flexible as I would like this to be,
                    // but good enough for now
                    string connectorType = new HeloServicesConnector(url).Helo();
                    m_log.DebugFormat("[HG INVENTORY SERVICE]: HELO returned {0}", connectorType);
                    if (connectorType == "opensim-simian")
                    {
                        connector = new SimianInventoryServiceConnector(url);
                    }
                    else
                    {
                        RemoteXInventoryServicesConnector rxisc = new RemoteXInventoryServicesConnector(url);
                        rxisc.Scene = m_Scenes[0];
                        connector = rxisc;
                    }

                    m_connectors.Add(url, connector);
                }
            }

            return connector;
        }
    }
}