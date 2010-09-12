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
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Scenes;

using OpenMetaverse;
using Nini.Config;
using log4net;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Inventory
{
    public class InventoryCache
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType);

        protected BaseInventoryConnector m_Connector;
        protected List<Scene> m_Scenes;

        // The cache proper
        protected Dictionary<UUID, Dictionary<AssetType, InventoryFolderBase>> m_InventoryCache;

        // A cache of userIDs --> ServiceURLs, for HGBroker only
        protected Dictionary<UUID, string> m_InventoryURLs =
                new Dictionary<UUID, string>();

        public virtual void Init(IConfigSource source, BaseInventoryConnector connector)
        {
            m_Scenes = new List<Scene>();
            m_InventoryCache = new Dictionary<UUID, Dictionary<AssetType, InventoryFolderBase>>();
            m_Connector = connector;
        }

        public virtual void AddRegion(Scene scene)
        {
            m_Scenes.Add(scene);
            scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
            scene.EventManager.OnClientClosed += OnClientClosed;
        }

        public virtual void RemoveRegion(Scene scene)
        {
            if ((m_Scenes != null) && m_Scenes.Contains(scene))
            {
                m_Scenes.Remove(scene);
            }
        }

        void OnMakeRootAgent(ScenePresence presence)
        {
            // Get system folders

            // First check if they're here already
            lock (m_InventoryCache)
            {
                if (m_InventoryCache.ContainsKey(presence.UUID))
                {
                    m_log.DebugFormat("[INVENTORY CACHE]: OnMakeRootAgent, system folders for {0} {1} already in cache", presence.Firstname, presence.Lastname);
                    return;
                }
            }

            // If not, go get them and place them in the cache
            Dictionary<AssetType, InventoryFolderBase> folders = CacheSystemFolders(presence.UUID);
            CacheInventoryServiceURL(presence.Scene, presence.UUID);
            
            m_log.DebugFormat("[INVENTORY CACHE]: OnMakeRootAgent in {0}, fetched system folders for {1} {2}: count {3}", 
                presence.Scene.RegionInfo.RegionName, presence.Firstname, presence.Lastname, folders.Count);

        }

        void OnClientClosed(UUID clientID, Scene scene)
        {
            if (m_InventoryCache.ContainsKey(clientID)) // if it's still in cache
            {
                ScenePresence sp = null;
                foreach (Scene s in m_Scenes)
                {
                    s.TryGetScenePresence(clientID, out sp);
                    if ((sp != null) && !sp.IsChildAgent && (s != scene))
                    {
                        m_log.DebugFormat("[INVENTORY CACHE]: OnClientClosed in {0}, but user {1} still in sim. Keeping system folders in cache",
                            scene.RegionInfo.RegionName, clientID);
                        return;
                    }
                }

                m_log.DebugFormat(
                    "[INVENTORY CACHE]: OnClientClosed in {0}, user {1} out of sim. Dropping system folders",
                    scene.RegionInfo.RegionName, clientID);
                DropCachedSystemFolders(clientID);
                DropInventoryServiceURL(clientID);
            }
        }

        /// <summary>
        /// Cache a user's 'system' folders.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>Folders cached</returns>
        protected Dictionary<AssetType, InventoryFolderBase> CacheSystemFolders(UUID userID)
        {
            // If not, go get them and place them in the cache
            Dictionary<AssetType, InventoryFolderBase> folders = m_Connector.GetSystemFolders(userID);

            if (folders.Count > 0)
                lock (m_InventoryCache)
                    m_InventoryCache.Add(userID, folders);

            return folders;
        }

        /// <summary>
        /// Drop a user's cached 'system' folders
        /// </summary>
        /// <param name="userID"></param>
        protected void DropCachedSystemFolders(UUID userID)
        {
            // Drop system folders
            lock (m_InventoryCache)
                if (m_InventoryCache.ContainsKey(userID))
                    m_InventoryCache.Remove(userID);
        }

        /// <summary>
        /// Get the system folder for a particular asset type
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public InventoryFolderBase GetFolderForType(UUID userID, AssetType type)
        {
            m_log.DebugFormat("[INVENTORY CACHE]: Getting folder for asset type {0} for user {1}", type, userID);
            
            Dictionary<AssetType, InventoryFolderBase> folders = null;
            
            lock (m_InventoryCache)
            {
                m_InventoryCache.TryGetValue(userID, out folders);

                // In some situations (such as non-secured standalones), system folders can be requested without
                // the user being logged in.  So we need to try caching them here if we don't already have them.
                if (null == folders)
                    CacheSystemFolders(userID);

                m_InventoryCache.TryGetValue(userID, out folders);
            }
            
            if ((folders != null) && folders.ContainsKey(type))
            {
                m_log.DebugFormat(
                    "[INVENTORY CACHE]: Returning folder {0} as type {1} for {2}", folders[type], type, userID);
                
                return folders[type];
            }
            
            m_log.WarnFormat("[INVENTORY CACHE]: Could not find folder for system type {0} for {1}", type, userID);

            return null;
        }

        /// <summary>
        /// Gets the user's inventory URL from its serviceURLs, if the user is foreign,
        /// and sticks it in the cache
        /// </summary>
        /// <param name="userID"></param>
        private void CacheInventoryServiceURL(Scene scene, UUID userID)
        {
            if (scene.UserAccountService.GetUserAccount(scene.RegionInfo.ScopeID, userID) == null)
            {
                // The user does not have a local account; let's cache its service URL
                string inventoryURL = string.Empty;
                ScenePresence sp = null;
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
                        }
                    }
                }
            }
        }

        private void DropInventoryServiceURL(UUID userID)
        {
            lock (m_InventoryURLs)
                if (m_InventoryURLs.ContainsKey(userID))
                    m_InventoryURLs.Remove(userID);
        }

        public string GetInventoryServiceURL(UUID userID)
        {
            if (m_InventoryURLs.ContainsKey(userID))
                return m_InventoryURLs[userID];

            return null;
        }
    }
}
