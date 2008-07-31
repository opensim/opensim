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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using libsecondlife;
using log4net;
using OpenSim.Framework;

namespace OpenSim.Data.MSSQL
{
    /// <summary>
    /// A MSSQL interface for the inventory server
    /// </summary>
    public class MSSQLInventoryData : IInventoryDataPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region Helper converters to preserve unsigned bitfield-type data in DB roundtrips via signed int32s
        private static int ConvertUint32BitFieldToInt32(uint bitField)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(bitField), 0);
        }
        private static uint ConvertInt32BitFieldToUint32(int bitField)
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(bitField), 0);
        }
        #endregion

        /// <summary>
        /// The database manager
        /// </summary>
        private MSSQLManager database;

        public void Initialise() 
        { 
            m_log.Info("[MSSQLInventoryData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException (Name);
        }

        /// <summary>
        /// Loads and initialises the MSSQL inventory storage interface
        /// </summary>
        /// <param name="connect">connect string</param>
        /// <remarks>use mssql_connection.ini</remarks>
        public void Initialise(string connect)
        {
            // TODO: actually use the provided connect string
            IniFile gridDataMSSqlFile = new IniFile("mssql_connection.ini");
            string settingDataSource = gridDataMSSqlFile.ParseFileReadValue("data_source");
            string settingInitialCatalog = gridDataMSSqlFile.ParseFileReadValue("initial_catalog");
            string settingPersistSecurityInfo = gridDataMSSqlFile.ParseFileReadValue("persist_security_info");
            string settingUserId = gridDataMSSqlFile.ParseFileReadValue("user_id");
            string settingPassword = gridDataMSSqlFile.ParseFileReadValue("password");

            database =
                new MSSQLManager(settingDataSource, settingInitialCatalog, settingPersistSecurityInfo, settingUserId,
                                 settingPassword);
            TestTables();
        }

        #region Test and initialization code

        /// <summary>
        /// Execute "CreateFoldersTable.sql" if tableName == null
        /// </summary>
        /// <param name="tableName">the table name</param>
        private void UpgradeFoldersTable(string tableName)
        {
            // null as the version, indicates that the table didn't exist
            if (tableName == null)
            {
                database.ExecuteResourceSql("CreateFoldersTable.sql");
                //database.ExecuteResourceSql("UpgradeFoldersTableToVersion2.sql");
                return;
            }
        }

        /// <summary>
        /// Execute "CreateItemsTable.sql" if tableName = null
        /// </summary>
        /// <param name="tableName">the table name</param>
        private void UpgradeItemsTable(string tableName)
        {
            // null as the version, indicates that the table didn't exist
            if (tableName == null)
            {
                database.ExecuteResourceSql("CreateItemsTable.sql");
                //database.ExecuteResourceSql("UpgradeItemsTableToVersion2.sql");
                return;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void TestTables()
        {
            Dictionary<string, string> tableList = new Dictionary<string, string>();

            tableList["inventoryfolders"] = null;
            tableList["inventoryitems"] = null;

            database.GetTableVersion(tableList);

            UpgradeFoldersTable(tableList["inventoryfolders"]);
            UpgradeItemsTable(tableList["inventoryitems"]);
        }

        #endregion

        /// <summary>
        /// The name of this DB provider
        /// </summary>
        /// <returns>A string containing the name of the DB provider</returns>
        public string Name
        {
            get { return "MSSQL Inventory Data Interface"; }
        }

        /// <summary>
        /// Closes this DB provider
        /// </summary>
        public void Dispose()
        {
            // Do nothing.
        }

        /// <summary>
        /// Returns the version of this DB provider
        /// </summary>
        /// <returns>A string containing the DB provider</returns>
        public string Version
        {
            get { return database.getVersion(); }
        }

        /// <summary>
        /// Returns a list of items in a specified folder
        /// </summary>
        /// <param name="folderID">The folder to search</param>
        /// <returns>A list containing inventory items</returns>
        public List<InventoryItemBase> getInventoryInFolder(LLUUID folderID)
        {
            try
            {
                List<InventoryItemBase> items = new List<InventoryItemBase>();

                Dictionary<string, string> param = new Dictionary<string, string>();
                param["parentFolderID"] = folderID.ToString();

                using (IDbCommand result =
                    database.Query("SELECT * FROM inventoryitems WHERE parentFolderID = @parentFolderID", param))
                using (IDataReader reader = result.ExecuteReader())
                {

                    while (reader.Read())
                        items.Add(readInventoryItem(reader));

                    reader.Close();
                }

                return items;
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Returns a list of the root folders within a users inventory
        /// </summary>
        /// <param name="user">The user whos inventory is to be searched</param>
        /// <returns>A list of folder objects</returns>
        public List<InventoryFolderBase> getUserRootFolders(LLUUID user)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["uuid"] = user.ToString();
                param["zero"] = LLUUID.Zero.ToString();

                using (IDbCommand result =
                    database.Query(
                        "SELECT * FROM inventoryfolders WHERE parentFolderID = @zero AND agentID = @uuid", param))
                using (IDataReader reader = result.ExecuteReader())
                {

                    List<InventoryFolderBase> items = new List<InventoryFolderBase>();
                    while (reader.Read())
                        items.Add(readInventoryFolder(reader));

                    return items;
                }

            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// see InventoryItemBase.getUserRootFolder
        /// </summary>
        /// <param name="user">the User UUID</param>
        /// <returns></returns>
        public InventoryFolderBase getUserRootFolder(LLUUID user)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["uuid"] = user.ToString();
                param["zero"] = LLUUID.Zero.ToString();

                using (IDbCommand result =
                    database.Query(
                        "SELECT * FROM inventoryfolders WHERE parentFolderID = @zero AND agentID = @uuid", param))
                using (IDataReader reader = result.ExecuteReader())
                {

                    List<InventoryFolderBase> items = new List<InventoryFolderBase>();
                    while (reader.Read())
                        items.Add(readInventoryFolder(reader));

                    InventoryFolderBase rootFolder = null;

                    // There should only ever be one root folder for a user.  However, if there's more
                    // than one we'll simply use the first one rather than failing.  It would be even
                    // nicer to print some message to this effect, but this feels like it's too low a
                    // to put such a message out, and it's too minor right now to spare the time to
                    // suitably refactor.
                    if (items.Count > 0)
                    {
                        rootFolder = items[0];
                    }

                    return rootFolder;
                }

            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Returns a list of folders in a users inventory contained within the specified folder
        /// </summary>
        /// <param name="parentID">The folder to search</param>
        /// <returns>A list of inventory folders</returns>
        public List<InventoryFolderBase> getInventoryFolders(LLUUID parentID)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["parentFolderID"] = parentID.ToString();

                using (IDbCommand result =
                    database.Query("SELECT * FROM inventoryfolders WHERE parentFolderID = @parentFolderID", param))
                using (IDataReader reader = result.ExecuteReader())
                {
                    List<InventoryFolderBase> items = new List<InventoryFolderBase>();

                    while (reader.Read())
                        items.Add(readInventoryFolder(reader));

                    return items;
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Reads a one item from an SQL result
        /// </summary>
        /// <param name="reader">The SQL Result</param>
        /// <returns>the item read</returns>
        private static InventoryItemBase readInventoryItem(IDataReader reader)
        {
            try
            {
                InventoryItemBase item = new InventoryItemBase();

                item.ID = new LLUUID((string) reader["inventoryID"]);
                item.AssetID = new LLUUID((string) reader["assetID"]);
                item.AssetType = (int) reader["assetType"];
                item.Folder = new LLUUID((string) reader["parentFolderID"]);
                item.Owner = new LLUUID((string) reader["avatarID"]);
                item.Name = (string) reader["inventoryName"];
                item.Description = (string) reader["inventoryDescription"];
                item.NextPermissions = ConvertInt32BitFieldToUint32((int)reader["inventoryNextPermissions"]);
                item.CurrentPermissions = ConvertInt32BitFieldToUint32((int)reader["inventoryCurrentPermissions"]);
                item.InvType = (int) reader["invType"];
                item.Creator = new LLUUID((string) reader["creatorID"]);
                item.BasePermissions = ConvertInt32BitFieldToUint32((int)reader["inventoryBasePermissions"]);
                item.EveryOnePermissions = ConvertInt32BitFieldToUint32((int)reader["inventoryEveryOnePermissions"]);
                item.SalePrice = (int) reader["salePrice"];
                item.SaleType = Convert.ToByte(reader["saleType"]);
                item.CreationDate = (int) reader["creationDate"];
                item.GroupID = new LLUUID(reader["groupID"].ToString());
                item.GroupOwned = Convert.ToBoolean(reader["groupOwned"]);
                item.Flags = ConvertInt32BitFieldToUint32((int)reader["flags"]);

                return item;
            }
            catch (SqlException e)
            {
                m_log.Error(e.ToString());
            }

            return null;
        }

        /// <summary>
        /// Returns a specified inventory item
        /// </summary>
        /// <param name="item">The item to return</param>
        /// <returns>An inventory item</returns>
        public InventoryItemBase getInventoryItem(LLUUID itemID)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["inventoryID"] = itemID.ToString();

                using (IDbCommand result =
                    database.Query("SELECT * FROM inventoryitems WHERE inventoryID = @inventoryID", param))
                using (IDataReader reader = result.ExecuteReader())
                {

                    InventoryItemBase item = null;
                    if (reader.Read())
                        item = readInventoryItem(reader);

                    return item;
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
            return null;
        }

        /// <summary>
        /// Reads a list of inventory folders returned by a query.
        /// </summary>
        /// <param name="reader">A MSSQL Data Reader</param>
        /// <returns>A List containing inventory folders</returns>
        protected static InventoryFolderBase readInventoryFolder(IDataReader reader)
        {
            try
            {
                InventoryFolderBase folder = new InventoryFolderBase();
                folder.Owner = new LLUUID((string) reader["agentID"]);
                folder.ParentID = new LLUUID((string) reader["parentFolderID"]);
                folder.ID = new LLUUID((string) reader["folderID"]);
                folder.Name = (string) reader["folderName"];
                folder.Type = (short) reader["type"];
                folder.Version = Convert.ToUInt16(reader["version"]);
                return folder;
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }

            return null;
        }

        /// <summary>
        /// Returns a specified inventory folder
        /// </summary>
        /// <param name="folder">The folder to return</param>
        /// <returns>A folder class</returns>
        public InventoryFolderBase getInventoryFolder(LLUUID folderID)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["uuid"] = folderID.ToString();

                using (IDbCommand result = database.Query("SELECT * FROM inventoryfolders WHERE folderID = @uuid", param))
                using (IDataReader reader = result.ExecuteReader())
                {

                    reader.Read();

                    InventoryFolderBase folder = readInventoryFolder(reader);

                    return folder;
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Adds a specified item to the database
        /// </summary>
        /// <param name="item">The inventory item</param>
        public void addInventoryItem(InventoryItemBase item)
        {
            if (getInventoryItem(item.ID) != null)
            {
                updateInventoryItem(item);
                return;
            }

            string sql = "INSERT INTO inventoryitems";
            sql +=
                "([inventoryID], [assetID], [assetType], [parentFolderID], [avatarID], [inventoryName]"
                    + ", [inventoryDescription], [inventoryNextPermissions], [inventoryCurrentPermissions]"
                    + ", [invType], [creatorID], [inventoryBasePermissions], [inventoryEveryOnePermissions]"
                    + ", [salePrice], [saleType], [creationDate], [groupID], [groupOwned], [flags]) VALUES ";
            sql +=
                "(@inventoryID, @assetID, @assetType, @parentFolderID, @avatarID, @inventoryName, @inventoryDescription"
                    + ", @inventoryNextPermissions, @inventoryCurrentPermissions, @invType, @creatorID"
                    + ", @inventoryBasePermissions, @inventoryEveryOnePermissions, @salePrice, @saleType"
                    + ", @creationDate, @groupID, @groupOwned, @flags);";

            using (AutoClosingSqlCommand command = database.Query(sql))
            {
                command.Parameters.AddWithValue("inventoryID", item.ID.ToString());
                command.Parameters.AddWithValue("assetID", item.AssetID.ToString());
                command.Parameters.AddWithValue("assetType", item.AssetType.ToString());
                command.Parameters.AddWithValue("parentFolderID", item.Folder.ToString());
                command.Parameters.AddWithValue("avatarID", item.Owner.ToString());
                command.Parameters.AddWithValue("inventoryName", item.Name);
                command.Parameters.AddWithValue("inventoryDescription", item.Description);
                command.Parameters.AddWithValue("inventoryNextPermissions", ConvertUint32BitFieldToInt32(item.NextPermissions));
                command.Parameters.AddWithValue("inventoryCurrentPermissions", ConvertUint32BitFieldToInt32(item.CurrentPermissions));
                command.Parameters.AddWithValue("invType", item.InvType);
                command.Parameters.AddWithValue("creatorID", item.Creator.ToString());
                command.Parameters.AddWithValue("inventoryBasePermissions", ConvertUint32BitFieldToInt32(item.BasePermissions));
                command.Parameters.AddWithValue("inventoryEveryOnePermissions", ConvertUint32BitFieldToInt32(item.EveryOnePermissions));
                command.Parameters.AddWithValue("salePrice", item.SalePrice);
                command.Parameters.AddWithValue("saleType", item.SaleType);
                command.Parameters.AddWithValue("creationDate", item.CreationDate);
                command.Parameters.AddWithValue("groupID", item.GroupID.ToString());
                command.Parameters.AddWithValue("groupOwned", item.GroupOwned);
                command.Parameters.AddWithValue("flags", ConvertUint32BitFieldToInt32(item.Flags));

                try
                {
                    command.ExecuteNonQuery();
                }
                catch (SqlException e)
                {
                    m_log.Error(e.ToString());
                }
            }

        }

        /// <summary>
        /// Updates the specified inventory item
        /// </summary>
        /// <param name="item">Inventory item to update</param>
        public void updateInventoryItem(InventoryItemBase item)
        {
            using (AutoClosingSqlCommand command = database.Query("UPDATE inventoryitems set inventoryID = @inventoryID, " +
                                                "assetID = @assetID, " +
                                                "assetType = @assetType," +
                                                "parentFolderID = @parentFolderID," +
                                                "avatarID = @avatarID," +
                                                "inventoryName = @inventoryName," +
                                                "inventoryDescription = @inventoryDescription," +
                                                "inventoryNextPermissions = @inventoryNextPermissions," +
                                                "inventoryCurrentPermissions = @inventoryCurrentPermissions," +
                                                "invType = @invType," +
                                                "creatorID = @creatorID," +
                                                "inventoryBasePermissions = @inventoryBasePermissions," +
                                                "inventoryEveryOnePermissions = @inventoryEveryOnePermissions," +
                                                "salePrice = @salePrice," +
                                                "saleType = @saleType," +
                                                "creationDate = @creationDate," +
                                                "groupID = @groupID," +
                                                "groupOwned = @groupOwned," +
                                                "flags = @flags where " +
                                                "inventoryID = @keyInventoryID;"))
            {
                command.Parameters.AddWithValue("inventoryID", item.ID.ToString());
                command.Parameters.AddWithValue("assetID", item.AssetID.ToString());
                command.Parameters.AddWithValue("assetType", item.AssetType.ToString());
                command.Parameters.AddWithValue("parentFolderID", item.Folder.ToString());
                command.Parameters.AddWithValue("avatarID", item.Owner.ToString());
                command.Parameters.AddWithValue("inventoryName", item.Name);
                command.Parameters.AddWithValue("inventoryDescription", item.Description);
                command.Parameters.AddWithValue("inventoryNextPermissions", ConvertUint32BitFieldToInt32(item.NextPermissions));
                command.Parameters.AddWithValue("inventoryCurrentPermissions", ConvertUint32BitFieldToInt32(item.CurrentPermissions));
                command.Parameters.AddWithValue("invType", item.InvType);
                command.Parameters.AddWithValue("creatorID", item.Creator.ToString());
                command.Parameters.AddWithValue("inventoryBasePermissions", ConvertUint32BitFieldToInt32(item.BasePermissions));
                command.Parameters.AddWithValue("inventoryEveryOnePermissions", ConvertUint32BitFieldToInt32(item.EveryOnePermissions));
                command.Parameters.AddWithValue("salePrice", item.SalePrice);
                command.Parameters.AddWithValue("saleType", item.SaleType);
                command.Parameters.AddWithValue("creationDate", item.CreationDate);
                command.Parameters.AddWithValue("groupID", item.GroupID.ToString());
                command.Parameters.AddWithValue("groupOwned", item.GroupOwned);
                command.Parameters.AddWithValue("flags", ConvertUint32BitFieldToInt32(item.Flags));
                command.Parameters.AddWithValue("@keyInventoryID", item.ID.ToString());

                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    m_log.Error(e.ToString());
                }
            }
        }

        /// <summary>
        /// Delete an item in inventory database
        /// </summary>
        /// <param name="item">the item UUID</param>
        public void deleteInventoryItem(LLUUID itemID)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["uuid"] = itemID.ToString();

                using (IDbCommand cmd = database.Query("DELETE FROM inventoryitems WHERE inventoryID=@uuid", param))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch (SqlException e)
            {
                m_log.Error(e.ToString());
            }
        }

        /// <summary>
        /// Creates a new inventory folder
        /// </summary>
        /// <param name="folder">Folder to create</param>
        public void addInventoryFolder(InventoryFolderBase folder)
        {
            string sql =
                "INSERT INTO inventoryfolders ([folderID], [agentID], [parentFolderID], [folderName], [type], [version]) VALUES ";
            sql += "(@folderID, @agentID, @parentFolderID, @folderName, @type, @version);";


            using (AutoClosingSqlCommand command = database.Query(sql))
            {
                command.Parameters.AddWithValue("folderID", folder.ID.ToString());
                command.Parameters.AddWithValue("agentID", folder.Owner.ToString());
                command.Parameters.AddWithValue("parentFolderID", folder.ParentID.ToString());
                command.Parameters.AddWithValue("folderName", folder.Name);
                command.Parameters.AddWithValue("type", folder.Type);
                command.Parameters.AddWithValue("version", Convert.ToInt32(folder.Version));

                try
                {
                    //IDbCommand result = database.Query(sql, param);
                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    m_log.Error(e.ToString());
                }
            }
        }

        /// <summary>
        /// Updates an inventory folder
        /// </summary>
        /// <param name="folder">Folder to update</param>
        public void updateInventoryFolder(InventoryFolderBase folder)
        {
            using (IDbCommand command = database.Query("UPDATE inventoryfolders set folderID = @folderID, " +
                                                "agentID = @agentID, " +
                                                "parentFolderID = @parentFolderID," +
                                                "folderName = @folderName," +
                                                "type = @type," +
                                                "version = @version where " +
                                                "folderID = @keyFolderID;"))
            {
                SqlParameter param1 = new SqlParameter("@folderID", folder.ID.ToString());
                SqlParameter param2 = new SqlParameter("@agentID", folder.Owner.ToString());
                SqlParameter param3 = new SqlParameter("@parentFolderID", folder.ParentID.ToString());
                SqlParameter param4 = new SqlParameter("@folderName", folder.Name);
                SqlParameter param5 = new SqlParameter("@type", folder.Type);
                SqlParameter param6 = new SqlParameter("@version", Convert.ToInt32(folder.Version));
                SqlParameter param7 = new SqlParameter("@keyFolderID", folder.ID.ToString());
                command.Parameters.Add(param1);
                command.Parameters.Add(param2);
                command.Parameters.Add(param3);
                command.Parameters.Add(param4);
                command.Parameters.Add(param5);
                command.Parameters.Add(param6);
                command.Parameters.Add(param7);

                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    m_log.Error(e.ToString());
                }
            }
        }

        /// <summary>
        /// Updates an inventory folder
        /// </summary>
        /// <param name="folder">Folder to update</param>
        public void moveInventoryFolder(InventoryFolderBase folder)
        {
            using (IDbCommand command = database.Query("UPDATE inventoryfolders set folderID = @folderID, " +
                                                "parentFolderID = @parentFolderID," +
                                                "folderID = @keyFolderID;"))
            {
                SqlParameter param1 = new SqlParameter("@folderID", folder.ID.ToString());
                SqlParameter param2 = new SqlParameter("@parentFolderID", folder.ParentID.ToString());
                SqlParameter param3 = new SqlParameter("@keyFolderID", folder.ID.ToString());
                command.Parameters.Add(param1);
                command.Parameters.Add(param2);
                command.Parameters.Add(param3);

                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    m_log.Error(e.ToString());
                }
            }
        }

        /// <summary>
        /// Append a list of all the child folders of a parent folder
        /// </summary>
        /// <param name="folders">list where folders will be appended</param>
        /// <param name="parentID">ID of parent</param>
        protected void getInventoryFolders(ref List<InventoryFolderBase> folders, LLUUID parentID)
        {
            List<InventoryFolderBase> subfolderList = getInventoryFolders(parentID);

            foreach (InventoryFolderBase f in subfolderList)
                folders.Add(f);
        }

        // See IInventoryDataPlugin
        public List<InventoryFolderBase> getFolderHierarchy(LLUUID parentID)
        {
            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            getInventoryFolders(ref folders, parentID);

            for (int i = 0; i < folders.Count; i++)
                getInventoryFolders(ref folders, folders[i].ID);

            return folders;
        }

        /// <summary>
        /// Delete a folder in inventory databasae
        /// </summary>
        /// <param name="folderID">the folder UUID</param>
        protected void deleteOneFolder(LLUUID folderID)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["folderID"] = folderID.ToString();

                using (IDbCommand cmd = database.Query("DELETE FROM inventoryfolders WHERE folderID=@folderID", param))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch (SqlException e)
            {
                m_log.Error(e.ToString());
            }
        }

        /// <summary>
        /// Delete an item in inventory database
        /// </summary>
        /// <param name="folderID">the item ID</param>
        protected void deleteItemsInFolder(LLUUID folderID)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["parentFolderID"] = folderID.ToString();


                using (IDbCommand cmd =
                    database.Query("DELETE FROM inventoryitems WHERE parentFolderID=@parentFolderID", param))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch (SqlException e)
            {
                m_log.Error(e.ToString());
            }
        }

        /// <summary>
        /// Delete an inventory folder
        /// </summary>
        /// <param name="folderId">Id of folder to delete</param>
        public void deleteInventoryFolder(LLUUID folderID)
        {
            // lock (database)
            {
                List<InventoryFolderBase> subFolders = getFolderHierarchy(folderID);

                //Delete all sub-folders
                foreach (InventoryFolderBase f in subFolders)
                {
                    deleteOneFolder(f.ID);
                    deleteItemsInFolder(f.ID);
                }

                //Delete the actual row
                deleteOneFolder(folderID);
                deleteItemsInFolder(folderID);
            }
        }
    }
}
