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
using System.Reflection;
using System.Collections.Generic;
using System.Data;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;
using Nini.Config;
using log4net;

namespace OpenSim.Grid.AssetInventoryServer.Plugins.OpenSim
{
    public class OpenSimInventoryStoragePlugin : IInventoryStorageProvider
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        const string EXTENSION_NAME = "OpenSimInventoryStorage"; // Used in metrics reporting

        private AssetInventoryServer m_server;
        private IConfig m_openSimConfig;
        private OpenSimInventoryService m_inventoryService;

        public OpenSimInventoryStoragePlugin()
        {
        }

        #region IInventoryStorageProvider implementation

        public BackendResponse TryFetchItem(Uri owner, UUID itemID, out InventoryItemBase item)
        {
            item = null;
            //BackendResponse ret;

            //using (MySqlConnection dbConnection = new MySqlConnection(m_openSimConfig.GetString("inventory_database_connect")))
            //{
            //    IDataReader reader;

            //    try
            //    {
            //        dbConnection.Open();

            //        IDbCommand command = dbConnection.CreateCommand();
            //        command.CommandText = String.Format("SELECT assetID,assetType,inventoryName,inventoryDescription,inventoryNextPermissions," +
            //            "inventoryCurrentPermissions,invType,creatorID,inventoryBasePermissions,inventoryEveryOnePermissions,salePrice,saleType," +
            //            "creationDate,groupID,groupOwned,flags,avatarID,parentFolderID,inventoryGroupPermissions FROM inventoryitems WHERE inventoryID='{0}'",
            //            itemID.ToString());
            //        reader = command.ExecuteReader();

            //        if (reader.Read())
            //        {
            //            item = new InventoryItemBase();
            //            item.ID = itemID;
            //            item.AssetID = UUID.Parse(reader.GetString(0));
            //            item.AssetType = reader.GetInt32(1);
            //            item.Name = reader.GetString(2);
            //            item.Description = reader.GetString(3);
            //            item.NextPermissions = (uint)reader.GetInt32(4);
            //            item.CurrentPermissions = (uint)reader.GetInt32(5);
            //            item.InvType = reader.GetInt32(6);
            //            item.Creator = UUID.Parse(reader.GetString(7));
            //            item.BasePermissions = (uint)reader.GetInt32(8);
            //            item.EveryOnePermissions = (uint)reader.GetInt32(9);
            //            item.SalePrice = reader.GetInt32(10);
            //            item.SaleType = reader.GetByte(11);
            //            item.CreationDate = reader.GetInt32(12);
            //            item.GroupID = UUID.Parse(reader.GetString(13));
            //            item.GroupOwned = reader.GetBoolean(14);
            //            item.Flags = (uint)reader.GetInt32(15);
            //            item.Owner = UUID.Parse(reader.GetString(16));
            //            item.Folder = UUID.Parse(reader.GetString(17));
            //            item.GroupPermissions = (uint)reader.GetInt32(18);

            //            ret = BackendResponse.Success;
            //        }
            //        else
            //        {
            //            ret = BackendResponse.NotFound;
            //        }
            //    }
            //    catch (MySqlException ex)
            //    {
            //        m_log.Error("[OPENSIMINVENTORYSTORAGE]: Connection to MySQL backend failed: " + ex.Message);
            //        ret = BackendResponse.Failure;
            //    }
            //}

            //m_server.MetricsProvider.LogInventoryFetch(EXTENSION_NAME, ret, owner, itemID, false, DateTime.Now);
            //return ret;
            m_log.Warn("[OPENSIMINVENTORYSTORAGE]: Called TryFetchItem which is not implemented.");
            return BackendResponse.Success;
        }

        public BackendResponse TryFetchFolder(Uri owner, UUID folderID, out InventoryFolderWithChildren folder)
        {
            BackendResponse ret;

            // TODO: implement some logic for "folder not found"
            folder = m_inventoryService.GetInventoryFolder(folderID);
            ret = BackendResponse.Success;

            m_server.MetricsProvider.LogInventoryFetch(EXTENSION_NAME, ret, owner, folderID, true, DateTime.Now);
            return ret;
        }

        public BackendResponse TryFetchFolderContents(Uri owner, UUID folderID, out InventoryCollection contents)
        {
            contents = null;
            //BackendResponse ret;

            //using (MySqlConnection dbConnection = new MySqlConnection(m_openSimConfig.GetString("inventory_database_connect")))
            //{
            //    IDataReader reader;

            //    try
            //    {
            //        dbConnection.Open();

            //        contents = new InventoryCollection();

            //        #region Folder retrieval

            //        IDbCommand command = dbConnection.CreateCommand();
            //        command.CommandText = String.Format("SELECT folderName,type,version,agentID,folderID FROM inventoryfolders WHERE parentFolderID='{0}'",
            //            folderID.ToString());
            //        reader = command.ExecuteReader();

            //        contents.Folders = new Dictionary<UUID, InventoryFolderWithChildren>();

            //        while (reader.Read())
            //        {
            //            InventoryFolderWithChildren folder = new InventoryFolderWithChildren();
            //            folder.ParentID = folderID;
            //            folder.Children = null; // This call doesn't do recursion
            //            folder.Name = reader.GetString(0);
            //            folder.Type = reader.GetInt16(1);
            //            folder.Version = (ushort)reader.GetInt16(2);
            //            folder.Owner = UUID.Parse(reader.GetString(3));
            //            folder.ID = UUID.Parse(reader.GetString(4));

            //            contents.Folders.Add(folder.ID, folder);
            //            contents.UserID = folder.Owner;
            //        }

            //        reader.Close();

            //        #endregion Folder retrieval

            //        #region Item retrieval

            //        command = dbConnection.CreateCommand();
            //        command.CommandText = String.Format("SELECT assetID,assetType,inventoryName,inventoryDescription,inventoryNextPermissions," +
            //            "inventoryCurrentPermissions,invType,creatorID,inventoryBasePermissions,inventoryEveryOnePermissions,salePrice,saleType," +
            //            "creationDate,groupID,groupOwned,flags,avatarID,inventoryID,inventoryGroupPermissions FROM inventoryitems WHERE parentFolderID='{0}'",
            //            folderID.ToString());
            //        reader = command.ExecuteReader();

            //        contents.Items = new Dictionary<UUID, InventoryItemBase>();

            //        while (reader.Read())
            //        {
            //            InventoryItemBase item = new InventoryItemBase();
            //            item.Folder = folderID;
            //            item.AssetID = UUID.Parse(reader.GetString(0));
            //            item.AssetType = reader.GetInt32(1);
            //            item.Name = reader.GetString(2);
            //            item.Description = reader.GetString(3);
            //            item.NextPermissions = (uint)reader.GetInt32(4);
            //            item.CurrentPermissions = (uint)reader.GetInt32(5);
            //            item.InvType = reader.GetInt32(6);
            //            item.Creator = UUID.Parse(reader.GetString(7));
            //            item.BasePermissions = (uint)reader.GetInt32(8);
            //            item.EveryOnePermissions = (uint)reader.GetInt32(9);
            //            item.SalePrice = reader.GetInt32(10);
            //            item.SaleType = reader.GetByte(11);
            //            item.CreationDate = reader.GetInt32(12);
            //            item.GroupID = UUID.Parse(reader.GetString(13));
            //            item.GroupOwned = reader.GetBoolean(14);
            //            item.Flags = (uint)reader.GetInt32(15);
            //            item.Owner = UUID.Parse(reader.GetString(16));
            //            item.ID = UUID.Parse(reader.GetString(17));
            //            item.GroupPermissions = (uint)reader.GetInt32(18);

            //            contents.Items.Add(item.ID, item);
            //            contents.UserID = item.Owner;
            //        }

            //        #endregion Item retrieval

            //        ret = BackendResponse.Success;
            //    }
            //    catch (MySqlException ex)
            //    {
            //        m_log.Error("[OPENSIMINVENTORYSTORAGE]: Connection to MySQL backend failed: " + ex.Message);
            //        ret = BackendResponse.Failure;
            //    }
            //}

            //m_server.MetricsProvider.LogInventoryFetchFolderContents(EXTENSION_NAME, ret, owner, folderID, DateTime.Now);
            //return ret;
            m_log.Warn("[OPENSIMINVENTORYSTORAGE]: Called TryFetchFolderContents which is not implemented.");
            return BackendResponse.Success;
        }

        public BackendResponse TryFetchFolderList(Uri owner, out List<InventoryFolderWithChildren> folders)
        {
            folders = new List<InventoryFolderWithChildren>();
            BackendResponse ret;
            UUID ownerID;

            if (Utils.TryGetOpenSimUUID(owner, out ownerID))
            {
                List<InventoryFolderBase> baseFolders = m_inventoryService.GetInventorySkeleton(ownerID);
                foreach (InventoryFolderBase baseFolder in baseFolders)
                {
                    InventoryFolderWithChildren folder = new InventoryFolderWithChildren(baseFolder);
                    //folder.Children = null; // This call does not create a folder hierarchy
                    folders.Add(folder);
                }

                ret = BackendResponse.Success;
            }
            else
            {
                folders = null;
                ret = BackendResponse.NotFound;
            }

            m_server.MetricsProvider.LogInventoryFetchFolderList(EXTENSION_NAME, ret, owner, DateTime.Now);
            return ret;
        }

        public BackendResponse TryFetchInventory(Uri owner, out InventoryCollection inventory)
        {
            inventory = null;
            BackendResponse ret;
            List<InventoryFolderWithChildren> folders;

            ret = TryFetchFolderList(owner, out folders);

            if (ret == BackendResponse.Success)
            {
                // Add the retrieved folders to the inventory collection
                inventory = new InventoryCollection();
                inventory.Folders = new Dictionary<UUID, InventoryFolderWithChildren>(folders.Count);
                foreach (InventoryFolderWithChildren folder in folders)
                    inventory.Folders[folder.ID] = folder;

                // Fetch inventory items
                UUID ownerID;
                if (Utils.TryGetOpenSimUUID(owner, out ownerID))
                {
                    inventory.UserID = ownerID;
                    inventory.Items = new Dictionary<UUID, InventoryItemBase>();

                    foreach (InventoryFolderWithChildren folder in folders)
                    {
                        foreach (InventoryItemBase item in m_inventoryService.RequestFolderItems(folder.ID))
                        {
                            inventory.Items.Add(item.ID, item);
                        }
                    }

                    ret = BackendResponse.Success;

                }
                else
                {
                    ret = BackendResponse.NotFound;
                }
            }

            m_server.MetricsProvider.LogInventoryFetchInventory(EXTENSION_NAME, ret, owner, DateTime.Now);
            return ret;
        }

        public BackendResponse TryFetchActiveGestures(Uri owner, out List<InventoryItemBase> gestures)
        {
            gestures = null;
            BackendResponse ret;
            UUID ownerID;

            if (Utils.TryGetOpenSimUUID(owner, out ownerID))
            {
                gestures = m_inventoryService.GetActiveGestures(ownerID);
                ret = BackendResponse.Success;
            }
            else
            {
                ret = BackendResponse.NotFound;
            }

            m_server.MetricsProvider.LogInventoryFetchActiveGestures(EXTENSION_NAME, ret, owner, DateTime.Now);
            return ret;
        }

        public BackendResponse TryCreateItem(Uri owner, InventoryItemBase item)
        {
            BackendResponse ret;

            if (m_inventoryService.AddItem(item))
            {
                ret = BackendResponse.Success;
            }
            else
            {
                ret = BackendResponse.Failure;
            }

            m_server.MetricsProvider.LogInventoryCreate(EXTENSION_NAME, ret, owner, false, DateTime.Now);
            return ret;
        }

        public BackendResponse TryCreateFolder(Uri owner, InventoryFolderWithChildren folder)
        {
            BackendResponse ret;

            if (m_inventoryService.AddFolder(folder))
            {
                ret = BackendResponse.Success;
            }
            else
            {
                ret = BackendResponse.Failure;
            }

            m_server.MetricsProvider.LogInventoryCreate(EXTENSION_NAME, ret, owner, true, DateTime.Now);
            return ret;
        }

        public BackendResponse TryCreateInventory(Uri owner, InventoryFolderWithChildren rootFolder)
        {
            BackendResponse ret;
            UUID ownerID;

            if (Utils.TryGetOpenSimUUID(owner, out ownerID))
            {
                if (m_inventoryService.CreateNewUserInventory(ownerID))
                {
                    ret = BackendResponse.Success;
                }
                else
                {
                    ret = BackendResponse.Failure;
                }
            }
            else
            {
                ret = BackendResponse.Failure;
            }

            return ret;
        }

        public BackendResponse TryDeleteItem(Uri owner, UUID itemID)
        {
            BackendResponse ret;

            if (m_inventoryService.DeleteItem(m_inventoryService.GetInventoryItem(itemID)))
            {
                ret = BackendResponse.Success;
            }
            else
            {
                ret = BackendResponse.Failure;
            }

            m_server.MetricsProvider.LogInventoryDelete(EXTENSION_NAME, ret, owner, itemID, false, DateTime.Now);
            return ret;
        }

        public BackendResponse TryDeleteFolder(Uri owner, UUID folderID)
        {
            //BackendResponse ret;
            //UUID ownerID;

            //if (Utils.TryGetOpenSimUUID(owner, out ownerID))
            //{
            //    using (MySqlConnection dbConnection = new MySqlConnection(m_openSimConfig.GetString("inventory_database_connect")))
            //    {
            //        try
            //        {
            //            dbConnection.Open();

            //            MySqlCommand command = new MySqlCommand(
            //                "DELETE FROM inventoryfolders WHERE folderID=?folderID AND agentID=?agentID", dbConnection);

            //            command.Parameters.AddWithValue("?folderID", folderID.ToString());
            //            command.Parameters.AddWithValue("?agentID", ownerID.ToString());

            //            int rowsAffected = command.ExecuteNonQuery();
            //            if (rowsAffected == 1)
            //            {
            //                ret = BackendResponse.Success;
            //            }
            //            else
            //            {
            //                m_log.ErrorFormat("[OPENSIMINVENTORYSTORAGE]: MySQL DELETE query affected {0} rows", rowsAffected);
            //                ret = BackendResponse.NotFound;
            //            }
            //        }
            //        catch (MySqlException ex)
            //        {
            //            m_log.Error("[OPENSIMINVENTORYSTORAGE]: Connection to MySQL backend failed: " + ex.Message);
            //            ret = BackendResponse.Failure;
            //        }
            //    }
            //}
            //else
            //{
            //    ret = BackendResponse.NotFound;
            //}

            //m_server.MetricsProvider.LogInventoryDelete(EXTENSION_NAME, ret, owner, folderID, true, DateTime.Now);
            //return ret;
            m_log.Warn("[OPENSIMINVENTORYSTORAGE]: Called TryDeleteFolder which is not implemented.");
            return BackendResponse.Success;
        }

        public BackendResponse TryPurgeFolder(Uri owner, UUID folderID)
        {
            BackendResponse ret;

            if (m_inventoryService.PurgeFolder(m_inventoryService.GetInventoryFolder(folderID)))
            {
                ret = BackendResponse.Success;
            }
            else
            {
                ret = BackendResponse.Failure;
            }

            m_server.MetricsProvider.LogInventoryPurgeFolder(EXTENSION_NAME, ret, owner, folderID, DateTime.Now);
            return ret;
        }

        public int ForEach(Action<AssetMetadata> action, int start, int count)
        {
            int rowCount = 0;

            //using (MySqlConnection dbConnection = new MySqlConnection(m_openSimConfig.GetString("inventory_database_connect")))
            //{
            //    MySqlDataReader reader;

            //    try
            //    {
            //        dbConnection.Open();

            //        MySqlCommand command = dbConnection.CreateCommand();
            //        command.CommandText = String.Format("SELECT name,description,assetType,temporary,data,id FROM assets LIMIT {0}, {1}",
            //            start, count);
            //        reader = command.ExecuteReader();
            //    }
            //    catch (MySqlException ex)
            //    {
            //        m_log.Error("[OPENSIMINVENTORYSTORAGE]: Connection to MySQL backend failed: " + ex.Message);
            //        return 0;
            //    }

            //    while (reader.Read())
            //    {
            //        Metadata metadata = new Metadata();
            //        metadata.CreationDate = OpenMetaverse.Utils.Epoch;
            //        metadata.Description = reader.GetString(1);
            //        metadata.ID = UUID.Parse(reader.GetString(5));
            //        metadata.Name = reader.GetString(0);
            //        metadata.SHA1 = OpenMetaverse.Utils.SHA1((byte[])reader.GetValue(4));
            //        metadata.Temporary = reader.GetBoolean(3);
            //        metadata.ContentType = Utils.SLAssetTypeToContentType(reader.GetInt32(2));

            //        action(metadata);
            //        ++rowCount;
            //    }

            //    reader.Close();
            //}

            return rowCount;
        }

        #endregion IInventoryStorageProvider implementation

        #region IPlugin implementation

        public void Initialise(AssetInventoryServer server)
        {
            m_server = server;
            m_openSimConfig = server.ConfigFile.Configs["OpenSim"];

            m_inventoryService = new OpenSimInventoryService();
            m_inventoryService.AddPlugin(m_openSimConfig.GetString("inventory_database_provider"),
                                         m_openSimConfig.GetString("inventory_database_connect"));
        }

        public void Stop()
        {
        }

        public void Initialise()
        {
            m_log.InfoFormat("[OPENSIMINVENTORYSTORAGE]: {0} cannot be default-initialized!", Name);
            throw new PluginNotInitialisedException(Name);
        }

        public void Dispose()
        {
        }

        public string Version
        {
            // TODO: this should be something meaningful and not hardcoded?
            get { return "0.1"; }
        }

        public string Name
        {
            get { return "OpenSimInventoryStorage"; }
        }

        #endregion IPlugin implementation
    }
}
