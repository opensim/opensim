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
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Console;

namespace OpenSim.Data.MSSQL
{
    /// <summary>
    /// A MySQL interface for the inventory server
    /// </summary>
    public class MSSQLInventoryData : IInventoryData
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The database manager
        /// </summary>
        private MSSQLManager database;

        /// <summary>
        /// Loads and initialises this database plugin
        /// </summary>
        public void Initialise()
        {
            IniFile GridDataMySqlFile = new IniFile("mssql_connection.ini");
            string settingDataSource = GridDataMySqlFile.ParseFileReadValue("data_source");
            string settingInitialCatalog = GridDataMySqlFile.ParseFileReadValue("initial_catalog");
            string settingPersistSecurityInfo = GridDataMySqlFile.ParseFileReadValue("persist_security_info");
            string settingUserId = GridDataMySqlFile.ParseFileReadValue("user_id");
            string settingPassword = GridDataMySqlFile.ParseFileReadValue("password");

            database =
                new MSSQLManager(settingDataSource, settingInitialCatalog, settingPersistSecurityInfo, settingUserId,
                                 settingPassword);
            TestTables();
        }

        #region Test and initialization code

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
        /// <returns>Name of DB provider</returns>
        public string getName()
        {
            return "MSSQL Inventory Data Interface";
        }

        /// <summary>
        /// Closes this DB provider
        /// </summary>
        public void Close()
        {
            // Do nothing.
        }

        /// <summary>
        /// Returns the version of this DB provider
        /// </summary>
        /// <returns>A string containing the DB provider</returns>
        public string getVersion()
        {
            return database.getVersion();
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
                lock (database)
                {
                    List<InventoryItemBase> items = new List<InventoryItemBase>();

                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["parentFolderID"] = folderID.ToString();

                    IDbCommand result =
                        database.Query("SELECT * FROM inventoryitems WHERE parentFolderID = @parentFolderID", param);
                    IDataReader reader = result.ExecuteReader();

                    while (reader.Read())
                        items.Add(readInventoryItem(reader));

                    reader.Close();
                    result.Dispose();

                    return items;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
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
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["uuid"] = user.ToString();
                    param["zero"] = LLUUID.Zero.ToString();

                    IDbCommand result =
                        database.Query(
                            "SELECT * FROM inventoryfolders WHERE parentFolderID = @zero AND agentID = @uuid", param);
                    IDataReader reader = result.ExecuteReader();

                    List<InventoryFolderBase> items = new List<InventoryFolderBase>();
                    while (reader.Read())
                        items.Add(readInventoryFolder(reader));


                    reader.Close();
                    result.Dispose();

                    return items;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
                return null;
            }
        }

        // see InventoryItemBase.getUserRootFolder
        public InventoryFolderBase getUserRootFolder(LLUUID user)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["uuid"] = user.ToString();
                    param["zero"] = LLUUID.Zero.ToString();

                    IDbCommand result =
                        database.Query(
                            "SELECT * FROM inventoryfolders WHERE parentFolderID = @zero AND agentID = @uuid", param);
                    IDataReader reader = result.ExecuteReader();

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

                    reader.Close();
                    result.Dispose();

                    return rootFolder;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
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
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["parentFolderID"] = parentID.ToString();


                    IDbCommand result =
                        database.Query("SELECT * FROM inventoryfolders WHERE parentFolderID = @parentFolderID", param);
                    IDataReader reader = result.ExecuteReader();

                    List<InventoryFolderBase> items = new List<InventoryFolderBase>();

                    while (reader.Read())
                        items.Add(readInventoryFolder(reader));

                    reader.Close();
                    result.Dispose();

                    return items;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Reads a one item from an SQL result
        /// </summary>
        /// <param name="reader">The SQL Result</param>
        /// <returns>the item read</returns>
        private InventoryItemBase readInventoryItem(IDataReader reader)
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
                item.NextPermissions = Convert.ToUInt32(reader["inventoryNextPermissions"]);
                item.CurrentPermissions = Convert.ToUInt32(reader["inventoryCurrentPermissions"]);
                item.InvType = (int) reader["invType"];
                item.Creator = new LLUUID((string) reader["creatorID"]);
                item.BasePermissions = Convert.ToUInt32(reader["inventoryBasePermissions"]);
                item.EveryOnePermissions = Convert.ToUInt32(reader["inventoryEveryOnePermissions"]);
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
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["inventoryID"] = itemID.ToString();

                    IDbCommand result =
                        database.Query("SELECT * FROM inventoryitems WHERE inventoryID = @inventoryID", param);
                    IDataReader reader = result.ExecuteReader();

                    InventoryItemBase item = null;
                    if (reader.Read())
                        item = readInventoryItem(reader);

                    reader.Close();
                    result.Dispose();

                    return item;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
            }
            return null;
        }

        /// <summary>
        /// Reads a list of inventory folders returned by a query.
        /// </summary>
        /// <param name="reader">A MySQL Data Reader</param>
        /// <returns>A List containing inventory folders</returns>
        protected InventoryFolderBase readInventoryFolder(IDataReader reader)
        {
            try
            {
                InventoryFolderBase folder = new InventoryFolderBase();
                folder.Owner = new LLUUID((string) reader["agentID"]);
                folder.ParentID = new LLUUID((string) reader["parentFolderID"]);
                folder.ID = new LLUUID((string) reader["folderID"]);
                folder.Name = (string) reader["folderName"];
                folder.Type = (short) reader["type"];
                folder.Version = (ushort) ((int) reader["version"]);
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
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["uuid"] = folderID.ToString();

                    IDbCommand result = database.Query("SELECT * FROM inventoryfolders WHERE folderID = @uuid", param);
                    IDataReader reader = result.ExecuteReader();

                    reader.Read();
                    InventoryFolderBase folder = readInventoryFolder(reader);
                    reader.Close();
                    result.Dispose();

                    return folder;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
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
                "([inventoryID], [assetID], [assetType], [parentFolderID], [avatarID], [inventoryName], [inventoryDescription], [inventoryNextPermissions], [inventoryCurrentPermissions], [invType], [creatorID], [inventoryBasePermissions], [inventoryEveryOnePermissions]) VALUES ";
            sql +=
                "(@inventoryID, @assetID, @assetType, @parentFolderID, @avatarID, @inventoryName, @inventoryDescription, @inventoryNextPermissions, @inventoryCurrentPermissions, @invType, @creatorID, @inventoryBasePermissions, @inventoryEveryOnePermissions);";

            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["inventoryID"] = item.ID.ToString();
                param["assetID"] = item.AssetID.ToString();
                param["assetType"] = item.AssetType.ToString();
                param["parentFolderID"] = item.Folder.ToString();
                param["avatarID"] = item.Owner.ToString();
                param["inventoryName"] = item.Name;
                param["inventoryDescription"] = item.Description;
                param["inventoryNextPermissions"] = item.NextPermissions.ToString();
                param["inventoryCurrentPermissions"] = item.CurrentPermissions.ToString();
                param["invType"] = Convert.ToString(item.InvType);
                param["creatorID"] = item.Creator.ToString();
                param["inventoryBasePermissions"] = Convert.ToString(item.BasePermissions);
                param["inventoryEveryOnePermissions"] = Convert.ToString(item.EveryOnePermissions);

                IDbCommand result = database.Query(sql, param);
                result.ExecuteNonQuery();
                result.Dispose();
            }
            catch (SqlException e)
            {
                m_log.Error(e.ToString());
            }
        }

        /// <summary>
        /// Updates the specified inventory item
        /// </summary>
        /// <param name="item">Inventory item to update</param>
        public void updateInventoryItem(InventoryItemBase item)
        {
            SqlCommand command = new SqlCommand("UPDATE inventoryitems set inventoryID = @inventoryID, " +
                                                "assetID = @assetID, " +
                                                "assetType = @assetType" +
                                                "parentFolderID = @parentFolderID" +
                                                "avatarID = @avatarID" +
                                                "inventoryName = @inventoryName" +
                                                "inventoryDescription = @inventoryDescription" +
                                                "inventoryNextPermissions = @inventoryNextPermissions" +
                                                "inventoryCurrentPermissions = @inventoryCurrentPermissions" +
                                                "invType = @invType" +
                                                "creatorID = @creatorID" +
                                                "inventoryBasePermissions = @inventoryBasePermissions" +
                                                "inventoryEveryOnePermissions = @inventoryEveryOnePermissions) where " +
                                                "inventoryID = @keyInventoryID;", database.getConnection());
            SqlParameter param1 = new SqlParameter("@inventoryID", item.ID.ToString());
            SqlParameter param2 = new SqlParameter("@assetID", item.AssetID);
            SqlParameter param3 = new SqlParameter("@assetType", item.AssetType);
            SqlParameter param4 = new SqlParameter("@parentFolderID", item.Folder);
            SqlParameter param5 = new SqlParameter("@avatarID", item.Owner);
            SqlParameter param6 = new SqlParameter("@inventoryName", item.Name);
            SqlParameter param7 = new SqlParameter("@inventoryDescription", item.Description);
            SqlParameter param8 = new SqlParameter("@inventoryNextPermissions", item.NextPermissions);
            SqlParameter param9 = new SqlParameter("@inventoryCurrentPermissions", item.CurrentPermissions);
            SqlParameter param10 = new SqlParameter("@invType", item.InvType);
            SqlParameter param11 = new SqlParameter("@creatorID", item.Creator);
            SqlParameter param12 = new SqlParameter("@inventoryBasePermissions", item.BasePermissions);
            SqlParameter param13 = new SqlParameter("@inventoryEveryOnePermissions", item.EveryOnePermissions);
            SqlParameter param14 = new SqlParameter("@keyInventoryID", item.ID.ToString());
            command.Parameters.Add(param1);
            command.Parameters.Add(param2);
            command.Parameters.Add(param3);
            command.Parameters.Add(param4);
            command.Parameters.Add(param5);
            command.Parameters.Add(param6);
            command.Parameters.Add(param7);
            command.Parameters.Add(param8);
            command.Parameters.Add(param9);
            command.Parameters.Add(param10);
            command.Parameters.Add(param11);
            command.Parameters.Add(param12);
            command.Parameters.Add(param13);
            command.Parameters.Add(param14);

            try
            {
                command.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        public void deleteInventoryItem(LLUUID itemID)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["uuid"] = itemID.ToString();

                IDbCommand cmd = database.Query("DELETE FROM inventoryitems WHERE inventoryID=@uuid", param);
                cmd.ExecuteNonQuery();
                cmd.Dispose();
            }
            catch (SqlException e)
            {
                database.Reconnect();
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


            Dictionary<string, string> param = new Dictionary<string, string>();
            param["folderID"] = folder.ID.ToString();
            param["agentID"] = folder.Owner.ToString();
            param["parentFolderID"] = folder.ParentID.ToString();
            param["folderName"] = folder.Name;
            param["type"] = Convert.ToString(folder.Type);
            param["version"] = Convert.ToString(folder.Version);

            try
            {
                IDbCommand result = database.Query(sql, param);
                result.ExecuteNonQuery();
                result.Dispose();
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
        }

        /// <summary>
        /// Updates an inventory folder
        /// </summary>
        /// <param name="folder">Folder to update</param>
        public void updateInventoryFolder(InventoryFolderBase folder)
        {
            SqlCommand command = new SqlCommand("UPDATE inventoryfolders set folderID = @folderID, " +
                                                "agentID = @agentID, " +
                                                "parentFolderID = @parentFolderID," +
                                                "folderName = @folderName," +
                                                "type = @type," +
                                                "version = @version where " +
                                                "folderID = @keyFolderID;", database.getConnection());
            SqlParameter param1 = new SqlParameter("@folderID", folder.ID.ToString());
            SqlParameter param2 = new SqlParameter("@agentID", folder.Owner.ToString());
            SqlParameter param3 = new SqlParameter("@parentFolderID", folder.ParentID.ToString());
            SqlParameter param4 = new SqlParameter("@folderName", folder.Name);
            SqlParameter param5 = new SqlParameter("@type", folder.Type);
            SqlParameter param6 = new SqlParameter("@version", folder.Version);
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

        /// <summary>
        /// Updates an inventory folder
        /// </summary>
        /// <param name="folder">Folder to update</param>
        public void moveInventoryFolder(InventoryFolderBase folder)
        {
            SqlCommand command = new SqlCommand("UPDATE inventoryfolders set folderID = @folderID, " +
                                                "parentFolderID = @parentFolderID," +
                                                "folderID = @keyFolderID;", database.getConnection());
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

        // See IInventoryData
        public List<InventoryFolderBase> getFolderHierarchy(LLUUID parentID)
        {
            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            getInventoryFolders(ref folders, parentID);

            for (int i = 0; i < folders.Count; i++)
                getInventoryFolders(ref folders, folders[i].ID);

            return folders;
        }

        protected void deleteOneFolder(LLUUID folderID)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["folderID"] = folderID.ToString();

                IDbCommand cmd = database.Query("DELETE FROM inventoryfolders WHERE folderID=@folderID", param);
                cmd.ExecuteNonQuery();
                cmd.Dispose();
            }
            catch (SqlException e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
            }
        }

        protected void deleteItemsInFolder(LLUUID folderID)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["parentFolderID"] = folderID.ToString();


                IDbCommand cmd =
                    database.Query("DELETE FROM inventoryitems WHERE parentFolderID=@parentFolderID", param);
                cmd.ExecuteNonQuery();
                cmd.Dispose();
            }
            catch (SqlException e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
            }
        }

        /// <summary>
        /// Delete an inventory folder
        /// </summary>
        /// <param name="folderId">Id of folder to delete</param>
        public void deleteInventoryFolder(LLUUID folderID)
        {
            lock (database)
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
