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

        //private AssetInventoryServer m_server;
        private IInventoryDataPlugin m_inventoryProvider;
        private IConfig m_openSimConfig;

        public OpenSimInventoryStoragePlugin()
        {
        }

        #region IInventoryStorageProvider implementation

        public BackendResponse TryFetchItem(Uri owner, UUID itemID, out InventoryItem item)
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
            //            item = new InventoryItem();
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

        public BackendResponse TryFetchFolder(Uri owner, UUID folderID, out InventoryFolder folder)
        {
            folder = null;
            //BackendResponse ret;

            //using (MySqlConnection dbConnection = new MySqlConnection(m_openSimConfig.GetString("inventory_database_connect")))
            //{
            //    IDataReader reader;

            //    try
            //    {
            //        dbConnection.Open();

            //        IDbCommand command = dbConnection.CreateCommand();
            //        command.CommandText = String.Format("SELECT folderName,type,version,agentID,parentFolderID FROM inventoryfolders WHERE folderID='{0}'",
            //            folderID.ToString());
            //        reader = command.ExecuteReader();

            //        if (reader.Read())
            //        {
            //            folder = new InventoryFolder();
            //            folder.Children = null; // This call only returns data for the folder itself, no children data
            //            folder.ID = folderID;
            //            folder.Name = reader.GetString(0);
            //            folder.Type = reader.GetInt16(1);
            //            folder.Version = (ushort)reader.GetInt16(2);
            //            folder.Owner = UUID.Parse(reader.GetString(3));
            //            folder.ParentID = UUID.Parse(reader.GetString(4));

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

            //m_server.MetricsProvider.LogInventoryFetch(EXTENSION_NAME, ret, owner, folderID, true, DateTime.Now);
            //return ret;
            return BackendResponse.Success;
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

            //        contents.Folders = new Dictionary<UUID, InventoryFolder>();

            //        while (reader.Read())
            //        {
            //            InventoryFolder folder = new InventoryFolder();
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

            //        contents.Items = new Dictionary<UUID, InventoryItem>();

            //        while (reader.Read())
            //        {
            //            InventoryItem item = new InventoryItem();
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

        public BackendResponse TryFetchFolderList(Uri owner, out List<InventoryFolder> folders)
        {
            folders = null;
            //BackendResponse ret;
            //UUID ownerID;

            //if (Utils.TryGetOpenSimUUID(owner, out ownerID))
            //{
            //    using (MySqlConnection dbConnection = new MySqlConnection(m_openSimConfig.GetString("inventory_database_connect")))
            //    {
            //        IDataReader reader;

            //        try
            //        {
            //            dbConnection.Open();
            //            folders = new List<InventoryFolder>();

            //            IDbCommand command = dbConnection.CreateCommand();
            //            command.CommandText = String.Format("SELECT folderName,type,version,folderID,parentFolderID FROM inventoryfolders WHERE agentID='{0}'",
            //                ownerID.ToString());
            //            reader = command.ExecuteReader();

            //            while (reader.Read())
            //            {
            //                InventoryFolder folder = new InventoryFolder();
            //                folder.Owner = ownerID;
            //                folder.Children = null; // This call does not create a folder hierarchy
            //                folder.Name = reader.GetString(0);
            //                folder.Type = reader.GetInt16(1);
            //                folder.Version = (ushort)reader.GetInt16(2);
            //                folder.ID = UUID.Parse(reader.GetString(3));
            //                folder.ParentID = UUID.Parse(reader.GetString(4));

            //                folders.Add(folder);
            //            }

            //            ret = BackendResponse.Success;
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

            //m_server.MetricsProvider.LogInventoryFetchFolderList(EXTENSION_NAME, ret, owner, DateTime.Now);
            //return ret;
            return BackendResponse.Success;
        }

        public BackendResponse TryFetchInventory(Uri owner, out InventoryCollection inventory)
        {
            inventory = null;
            //BackendResponse ret;
            //List<InventoryFolder> folders;
            //UUID ownerID;

            //ret = TryFetchFolderList(owner, out folders);

            //if (ret == BackendResponse.Success)
            //{
            //    // Add the retrieved folders to the inventory collection
            //    inventory = new InventoryCollection();
            //    inventory.Folders = new Dictionary<UUID, InventoryFolder>(folders.Count);
            //    foreach (InventoryFolder folder in folders)
            //        inventory.Folders[folder.ID] = folder;

            //    // Fetch inventory items
            //    if (Utils.TryGetOpenSimUUID(owner, out ownerID))
            //    {
            //        using (MySqlConnection dbConnection = new MySqlConnection(m_openSimConfig.GetString("inventory_database_connect")))
            //        {
            //            IDataReader reader;

            //            try
            //            {
            //                dbConnection.Open();

            //                IDbCommand command = dbConnection.CreateCommand();
            //                command.CommandText = String.Format("SELECT assetID,assetType,inventoryName,inventoryDescription,inventoryNextPermissions," +
            //                    "inventoryCurrentPermissions,invType,creatorID,inventoryBasePermissions,inventoryEveryOnePermissions,salePrice,saleType," +
            //                    "creationDate,groupID,groupOwned,flags,inventoryID,parentFolderID,inventoryGroupPermissions FROM inventoryitems WHERE " +
            //                    "avatarID='{0}'", ownerID.ToString());
            //                reader = command.ExecuteReader();

            //                inventory.UserID = ownerID;
            //                inventory.Items = new Dictionary<UUID, InventoryItem>();

            //                while (reader.Read())
            //                {
            //                    InventoryItem item = new InventoryItem();
            //                    item.Owner = ownerID;
            //                    item.AssetID = UUID.Parse(reader.GetString(0));
            //                    item.AssetType = reader.GetInt32(1);
            //                    item.Name = reader.GetString(2);
            //                    item.Description = reader.GetString(3);
            //                    item.NextPermissions = (uint)reader.GetInt32(4);
            //                    item.CurrentPermissions = (uint)reader.GetInt32(5);
            //                    item.InvType = reader.GetInt32(6);
            //                    item.Creator = UUID.Parse(reader.GetString(7));
            //                    item.BasePermissions = (uint)reader.GetInt32(8);
            //                    item.EveryOnePermissions = (uint)reader.GetInt32(9);
            //                    item.SalePrice = reader.GetInt32(10);
            //                    item.SaleType = reader.GetByte(11);
            //                    item.CreationDate = reader.GetInt32(12);
            //                    item.GroupID = UUID.Parse(reader.GetString(13));
            //                    item.GroupOwned = reader.GetBoolean(14);
            //                    item.Flags = (uint)reader.GetInt32(15);
            //                    item.ID = UUID.Parse(reader.GetString(16));
            //                    item.Folder = UUID.Parse(reader.GetString(17));
            //                    item.GroupPermissions = (uint)reader.GetInt32(18);

            //                    inventory.Items.Add(item.ID, item);
            //                }

            //                ret = BackendResponse.Success;
            //            }
            //            catch (MySqlException ex)
            //            {
            //                m_log.Error("[OPENSIMINVENTORYSTORAGE]: Connection to MySQL backend failed: " + ex.Message);
            //                ret = BackendResponse.Failure;
            //            }
            //        }
            //    }
            //    else
            //    {
            //        ret = BackendResponse.NotFound;
            //    }
            //}

            //m_server.MetricsProvider.LogInventoryFetchInventory(EXTENSION_NAME, ret, owner, DateTime.Now);
            //return ret;
            return BackendResponse.Success;
        }

        public BackendResponse TryFetchActiveGestures(Uri owner, out List<InventoryItem> gestures)
        {
            gestures = null;
            //BackendResponse ret;
            //UUID ownerID;

            //if (Utils.TryGetOpenSimUUID(owner, out ownerID))
            //{
            //    using (MySqlConnection dbConnection = new MySqlConnection(m_openSimConfig.GetString("inventory_database_connect")))
            //    {
            //        IDataReader reader;

            //        try
            //        {
            //            dbConnection.Open();

            //            MySqlCommand command = new MySqlCommand("SELECT assetID,inventoryName,inventoryDescription,inventoryNextPermissions," +
            //                "inventoryCurrentPermissions,invType,creatorID,inventoryBasePermissions,inventoryEveryOnePermissions,salePrice,saleType," +
            //                "creationDate,groupID,groupOwned,inventoryID,parentFolderID,inventoryGroupPermissions FROM inventoryitems WHERE " +
            //                "avatarId=?uuid AND assetType=?type AND flags=1", dbConnection);
            //            command.Parameters.AddWithValue("?uuid", ownerID.ToString());
            //            command.Parameters.AddWithValue("?type", (int)AssetType.Gesture);
            //            reader = command.ExecuteReader();

            //            while (reader.Read())
            //            {
            //                InventoryItem item = new InventoryItem();
            //                item.Owner = ownerID;
            //                item.AssetType = (int)AssetType.Gesture;
            //                item.Flags = (uint)1;
            //                item.AssetID = UUID.Parse(reader.GetString(0));
            //                item.Name = reader.GetString(1);
            //                item.Description = reader.GetString(2);
            //                item.NextPermissions = (uint)reader.GetInt32(3);
            //                item.CurrentPermissions = (uint)reader.GetInt32(4);
            //                item.InvType = reader.GetInt32(5);
            //                item.Creator = UUID.Parse(reader.GetString(6));
            //                item.BasePermissions = (uint)reader.GetInt32(7);
            //                item.EveryOnePermissions = (uint)reader.GetInt32(8);
            //                item.SalePrice = reader.GetInt32(9);
            //                item.SaleType = reader.GetByte(10);
            //                item.CreationDate = reader.GetInt32(11);
            //                item.GroupID = UUID.Parse(reader.GetString(12));
            //                item.GroupOwned = reader.GetBoolean(13);
            //                item.ID = UUID.Parse(reader.GetString(14));
            //                item.Folder = UUID.Parse(reader.GetString(15));
            //                item.GroupPermissions = (uint)reader.GetInt32(16);

            //                gestures.Add(item);
            //            }

            //            ret = BackendResponse.Success;
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

            //m_server.MetricsProvider.LogInventoryFetchActiveGestures(EXTENSION_NAME, ret, owner, DateTime.Now);
            //return ret;
            return BackendResponse.Success;
        }

        public BackendResponse TryCreateItem(Uri owner, InventoryItem item)
        {
            //BackendResponse ret;

            //using (MySqlConnection dbConnection = new MySqlConnection(m_openSimConfig.GetString("inventory_database_connect")))
            //{
            //    try
            //    {
            //        dbConnection.Open();

            //        MySqlCommand command = new MySqlCommand(
            //            "REPLACE INTO inventoryitems (assetID,assetType,inventoryName,inventoryDescription,inventoryNextPermissions," +
            //            "inventoryCurrentPermissions,invType,creatorID,inventoryBasePermissions,inventoryEveryOnePermissions,salePrice,saleType," +
            //            "creationDate,groupID,groupOwned,flags,inventoryID,avatarID,parentFolderID,inventoryGroupPermissions) VALUES " +

            //            "(?assetID,?assetType,?inventoryName,?inventoryDescription,?inventoryNextPermissions,?inventoryCurrentPermissions,?invType," +
            //            "?creatorID,?inventoryBasePermissions,?inventoryEveryOnePermissions,?salePrice,?saleType,?creationDate,?groupID,?groupOwned," +
            //            "?flags,?inventoryID,?avatarID,?parentFolderID,?inventoryGroupPermissions)", dbConnection);

            //        command.Parameters.AddWithValue("?assetID", item.AssetID.ToString());
            //        command.Parameters.AddWithValue("?assetType", item.AssetType);
            //        command.Parameters.AddWithValue("?inventoryName", item.Name);
            //        command.Parameters.AddWithValue("?inventoryDescription", item.Description);
            //        command.Parameters.AddWithValue("?inventoryNextPermissions", item.NextPermissions);
            //        command.Parameters.AddWithValue("?inventoryCurrentPermissions", item.CurrentPermissions);
            //        command.Parameters.AddWithValue("?invType", item.InvType);
            //        command.Parameters.AddWithValue("?creatorID", item.Creator.ToString());
            //        command.Parameters.AddWithValue("?inventoryBasePermissions", item.BasePermissions);
            //        command.Parameters.AddWithValue("?inventoryEveryOnePermissions", item.EveryOnePermissions);
            //        command.Parameters.AddWithValue("?salePrice", item.SalePrice);
            //        command.Parameters.AddWithValue("?saleType", item.SaleType);
            //        command.Parameters.AddWithValue("?creationDate", item.CreationDate);
            //        command.Parameters.AddWithValue("?groupID", item.GroupID.ToString());
            //        command.Parameters.AddWithValue("?groupOwned", item.GroupOwned);
            //        command.Parameters.AddWithValue("?flags", item.Flags);
            //        command.Parameters.AddWithValue("?inventoryID", item.ID);
            //        command.Parameters.AddWithValue("?avatarID", item.Owner);
            //        command.Parameters.AddWithValue("?parentFolderID", item.Folder);
            //        command.Parameters.AddWithValue("?inventoryGroupPermissions", item.GroupPermissions);

            //        int rowsAffected = command.ExecuteNonQuery();
            //        if (rowsAffected == 1)
            //        {
            //            ret = BackendResponse.Success;
            //        }
            //        else if (rowsAffected == 2)
            //        {
            //            m_log.Info("[OPENSIMINVENTORYSTORAGE]: Replaced inventory item " + item.ID.ToString());
            //            ret = BackendResponse.Success;
            //        }
            //        else
            //        {
            //            m_log.ErrorFormat("[OPENSIMINVENTORYSTORAGE]: MySQL REPLACE query affected {0} rows", rowsAffected);
            //            ret = BackendResponse.Failure;
            //        }
            //    }
            //    catch (MySqlException ex)
            //    {
            //        m_log.Error("[OPENSIMINVENTORYSTORAGE]: Connection to MySQL backend failed: " + ex.Message);
            //        ret = BackendResponse.Failure;
            //    }
            //}

            //m_server.MetricsProvider.LogInventoryCreate(EXTENSION_NAME, ret, owner, false, DateTime.Now);
            //return ret;
            return BackendResponse.Success;
        }

        public BackendResponse TryCreateFolder(Uri owner, InventoryFolder folder)
        {
            //BackendResponse ret;

            //using (MySqlConnection dbConnection = new MySqlConnection(m_openSimConfig.GetString("inventory_database_connect")))
            //{
            //    try
            //    {
            //        dbConnection.Open();

            //        MySqlCommand command = new MySqlCommand(
            //            "REPLACE INTO inventoryfolders (folderName,type,version,folderID,agentID,parentFolderID) VALUES " +
            //            "(?folderName,?type,?version,?folderID,?agentID,?parentFolderID)", dbConnection);

            //        command.Parameters.AddWithValue("?folderName", folder.Name);
            //        command.Parameters.AddWithValue("?type", folder.Type);
            //        command.Parameters.AddWithValue("?version", folder.Version);
            //        command.Parameters.AddWithValue("?folderID", folder.ID);
            //        command.Parameters.AddWithValue("?agentID", folder.Owner);
            //        command.Parameters.AddWithValue("?parentFolderID", folder.ParentID);

            //        int rowsAffected = command.ExecuteNonQuery();
            //        if (rowsAffected == 1)
            //        {
            //            ret = BackendResponse.Success;
            //        }
            //        else if (rowsAffected == 2)
            //        {
            //            m_log.Info("[OPENSIMINVENTORYSTORAGE]: Replaced inventory folder " + folder.ID.ToString());
            //            ret = BackendResponse.Success;
            //        }
            //        else
            //        {
            //            m_log.ErrorFormat("[OPENSIMINVENTORYSTORAGE]: MySQL REPLACE query affected {0} rows", rowsAffected);
            //            ret = BackendResponse.Failure;
            //        }
            //    }
            //    catch (MySqlException ex)
            //    {
            //        m_log.Error("[OPENSIMINVENTORYSTORAGE]: Connection to MySQL backend failed: " + ex.Message);
            //        ret = BackendResponse.Failure;
            //    }
            //}

            //m_server.MetricsProvider.LogInventoryCreate(EXTENSION_NAME, ret, owner, true, DateTime.Now);
            //return ret;
            return BackendResponse.Success;
        }

        public BackendResponse TryCreateInventory(Uri owner, InventoryFolder rootFolder)
        {
            return TryCreateFolder(owner, rootFolder);
        }

        public BackendResponse TryDeleteItem(Uri owner, UUID itemID)
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
            //                "DELETE FROM inventoryitems WHERE inventoryID=?inventoryID AND avatarID=?avatarID", dbConnection);

            //            command.Parameters.AddWithValue("?inventoryID", itemID.ToString());
            //            command.Parameters.AddWithValue("?avatarID", ownerID.ToString());

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

            //m_server.MetricsProvider.LogInventoryDelete(EXTENSION_NAME, ret, owner, itemID, false, DateTime.Now);
            //return ret;
            return BackendResponse.Success;
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
            //BackendResponse ret;
            //UUID ownerID;

            //if (Utils.TryGetOpenSimUUID(owner, out ownerID))
            //{
            //    using (MySqlConnection dbConnection = new MySqlConnection(m_openSimConfig.GetString("inventory_database_connect")))
            //    {
            //        try
            //        {
            //            dbConnection.Open();

            //            #region Delete items

            //            MySqlCommand command = new MySqlCommand(
            //                "DELETE FROM inventoryitems WHERE parentFolderID=?parentFolderID AND avatarID=?avatarID", dbConnection);

            //            command.Parameters.AddWithValue("?parentFolderID", folderID.ToString());
            //            command.Parameters.AddWithValue("?avatarID", ownerID.ToString());

            //            int rowsAffected = command.ExecuteNonQuery();

            //            #endregion Delete items

            //            #region Delete folders

            //            command = new MySqlCommand(
            //                "DELETE FROM inventoryfolders WHERE parentFolderID=?parentFolderID AND agentID=?agentID", dbConnection);

            //            command.Parameters.AddWithValue("?parentFolderID", folderID.ToString());
            //            command.Parameters.AddWithValue("?agentID", ownerID.ToString());

            //            rowsAffected += command.ExecuteNonQuery();

            //            #endregion Delete folders

            //            m_log.DebugFormat("[OPENSIMINVENTORYSTORAGE]: Deleted {0} inventory objects from MySQL in a folder purge", rowsAffected);

            //            ret = BackendResponse.Success;
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

            //m_server.MetricsProvider.LogInventoryPurgeFolder(EXTENSION_NAME, ret, owner, folderID, DateTime.Now);
            //return ret;
            return BackendResponse.Success;
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
            //m_server = server;
            m_openSimConfig = server.ConfigFile.Configs["OpenSim"];

            try
            {
                m_inventoryProvider = DataPluginFactory.LoadDataPlugin<IInventoryDataPlugin>(m_openSimConfig.GetString("inventory_database_provider"),
                                                                                             m_openSimConfig.GetString("inventory_database_connect"));
                if (m_inventoryProvider == null)
                {
                    m_log.Error("[OPENSIMINVENTORYSTORAGE]: Failed to load a database plugin, server halting.");
                    Environment.Exit(-1);
                }
                else
                    m_log.InfoFormat("[OPENSIMINVENTORYSTORAGE]: Loaded storage backend: {0}", Version);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[OPENSIMINVENTORYSTORAGE]: Failure loading data plugin: {0}", e.ToString());
                throw new PluginNotInitialisedException(Name);
            }
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
            get { return m_inventoryProvider.Version; }
        }

        public string Name
        {
            get { return "OpenSimInventoryStorage"; }
        }

        #endregion IPlugin implementation
    }
}
