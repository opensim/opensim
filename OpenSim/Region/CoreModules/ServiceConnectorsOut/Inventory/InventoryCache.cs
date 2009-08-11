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
    public abstract class InventoryCache
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType);

        protected List<Scene> m_Scenes;

        // The cache proper
        protected Dictionary<UUID, Dictionary<AssetType, InventoryFolderBase>> m_InventoryCache;

        protected virtual void Init(IConfigSource source)
        {
            m_Scenes = new List<Scene>();
            m_InventoryCache = new Dictionary<UUID, Dictionary<AssetType, InventoryFolderBase>>();
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
            Dictionary<AssetType, InventoryFolderBase> folders = GetSystemFolders(presence.UUID);
            m_log.DebugFormat("[INVENTORY CACHE]: OnMakeRootAgent, fetched system folders for {0} {1}: count {2}", 
                presence.Firstname, presence.Lastname, folders.Count);
            if (folders.Count > 0)
                lock (m_InventoryCache)
                    m_InventoryCache.Add(presence.UUID, folders);
        }

        void OnClientClosed(UUID clientID, Scene scene)
        {
            ScenePresence sp = null;
            foreach (Scene s in m_Scenes)
            {
                s.TryGetAvatar(clientID, out sp);
                if ((sp != null) && !sp.IsChildAgent)
                {
                    m_log.DebugFormat("[INVENTORY CACHE]: OnClientClosed in {0}, but user {1} still in sim. Keeping system folders in cache", 
                        scene.RegionInfo.RegionName, clientID);
                    return;
                }
            }

            m_log.DebugFormat("[INVENTORY CACHE]: OnClientClosed in {0}, user {1} out of sim. Dropping system folders", 
                scene.RegionInfo.RegionName, clientID);
            // Drop system folders
            lock (m_InventoryCache)
                if (m_InventoryCache.ContainsKey(clientID))
                    m_InventoryCache.Remove(clientID);

        }

        public abstract Dictionary<AssetType, InventoryFolderBase> GetSystemFolders(UUID userID);

        public InventoryFolderBase GetFolderForType(UUID userID, AssetType type)
        {
            Dictionary<AssetType, InventoryFolderBase> folders = null;
            lock (m_InventoryCache)
            {
                m_InventoryCache.TryGetValue(userID, out folders);
            }
            if ((folders != null) && folders.ContainsKey(type))
            {
                return folders[type];
            }

            return null;
        }
    }
}
