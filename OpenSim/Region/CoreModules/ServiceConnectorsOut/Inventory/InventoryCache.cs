using System;
using System.Collections.Generic;

using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Inventory
{
    public class InventoryCache
    {
        private const double CACHE_EXPIRATION_SECONDS = 3600.0; // 1 hour

        private static ExpiringCache<UUID, InventoryFolderBase> m_RootFolders = new ExpiringCache<UUID, InventoryFolderBase>();
        private static ExpiringCache<UUID, Dictionary<AssetType, InventoryFolderBase>> m_FolderTypes = new ExpiringCache<UUID, Dictionary<AssetType, InventoryFolderBase>>();
        private static ExpiringCache<UUID, InventoryCollection> m_Inventories = new ExpiringCache<UUID, InventoryCollection>();

        public void Cache(UUID userID, InventoryFolderBase root)
        {
            lock (m_RootFolders)
                m_RootFolders.AddOrUpdate(userID, root, CACHE_EXPIRATION_SECONDS);
        }

        public InventoryFolderBase GetRootFolder(UUID userID)
        {
            InventoryFolderBase root = null;
            if (m_RootFolders.TryGetValue(userID, out root))
                return root;

            return null;
        }

        public void Cache(UUID userID, AssetType type, InventoryFolderBase folder)
        {
            lock (m_FolderTypes)
            {
                Dictionary<AssetType, InventoryFolderBase> ff = null;
                if (!m_FolderTypes.TryGetValue(userID, out ff))
                {
                    ff = new Dictionary<AssetType, InventoryFolderBase>();
                    m_FolderTypes.Add(userID, ff, CACHE_EXPIRATION_SECONDS);
                }
                if (!ff.ContainsKey(type))
                    ff.Add(type, folder);
            }
        }

        public InventoryFolderBase GetFolderForType(UUID userID, AssetType type)
        {
            Dictionary<AssetType, InventoryFolderBase> ff = null;
            if (m_FolderTypes.TryGetValue(userID, out ff))
            {
                InventoryFolderBase f = null;
                if (ff.TryGetValue(type, out f))
                    return f;
            }

            return null;
        }

        public void Cache(UUID userID, InventoryCollection inv)
        {
            lock (m_Inventories)
                m_Inventories.AddOrUpdate(userID, inv, 120);
        }

        public InventoryCollection GetUserInventory(UUID userID)
        {
            InventoryCollection inv = null;
            if (m_Inventories.TryGetValue(userID, out inv))
                return inv;
            return null;
        }

        public InventoryCollection GetFolderContent(UUID userID, UUID folderID)
        {
            InventoryCollection inv = null;
            InventoryCollection c;
            if (m_Inventories.TryGetValue(userID, out inv))
            {
                c = new InventoryCollection();
                c.UserID = userID;

                c.Folders = inv.Folders.FindAll(delegate(InventoryFolderBase f)
                {
                    return f.ParentID == folderID;
                });
                c.Items = inv.Items.FindAll(delegate(InventoryItemBase i)
                {
                    return i.Folder == folderID;
                });
                return c;
            }
            return null;
        }

        public List<InventoryItemBase> GetFolderItems(UUID userID, UUID folderID)
        {
            InventoryCollection inv = null;
            if (m_Inventories.TryGetValue(userID, out inv))
            {
                List<InventoryItemBase> items = inv.Items.FindAll(delegate(InventoryItemBase i)
                {
                    return i.Folder == folderID;
                });
                return items;
            }
            return null;
        }
    }
}
