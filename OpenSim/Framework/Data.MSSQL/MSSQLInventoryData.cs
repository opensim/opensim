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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Data.MSSQL
{
    /// <summary>
    /// A MySQL interface for the inventory server
    /// </summary>
    public class MSSQLInventoryData : IInventoryData
    {
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

            database = new MSSQLManager(settingDataSource, settingInitialCatalog, settingPersistSecurityInfo, settingUserId, settingPassword);
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
                    param["parentFolderID"] = folderID.ToStringHyphenated();

                    IDbCommand result = database.Query("SELECT * FROM inventoryitems WHERE parentFolderID = @parentFolderID", param);
                    IDataReader reader = result.ExecuteReader();

                    while(reader.Read())
                        items.Add(readInventoryItem(reader));

                    reader.Close();
                    result.Dispose();
                    
                    return items;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                MainLog.Instance.Error(e.ToString());
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
                    param["uuid"] = user.ToStringHyphenated();
                    param["zero"] = LLUUID.Zero.ToStringHyphenated();

                    IDbCommand result = database.Query("SELECT * FROM inventoryfolders WHERE parentFolderID = @zero AND agentID = @uuid", param);
                    IDataReader reader = result.ExecuteReader();

                    List<InventoryFolderBase> items = new List<InventoryFolderBase>();
                    while(reader.Read())
                        items.Add(readInventoryFolder(reader));


                    reader.Close();
                    result.Dispose();

                    return items;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                MainLog.Instance.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Returns the users inventory root folder.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public InventoryFolderBase getUserRootFolder(LLUUID user)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["uuid"] = user.ToStringHyphenated();
                    param["zero"] = LLUUID.Zero.ToStringHyphenated();

                    IDbCommand result = database.Query("SELECT * FROM inventoryfolders WHERE parentFolderID = @zero AND agentID = @uuid", param);
                    IDataReader reader = result.ExecuteReader();

                    List<InventoryFolderBase> items = new List<InventoryFolderBase>();
                    while(reader.Read())
                        items.Add(readInventoryFolder(reader));

                    InventoryFolderBase rootFolder = null;
                    if (items.Count > 0) {
                        rootFolder = items[0]; //should only be one folder with parent set to zero (the root one).
                    }
                    
                    reader.Close();
                    result.Dispose();

                    return rootFolder;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                MainLog.Instance.Error(e.ToString());
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
                    param["parentFolderID"] = parentID.ToStringHyphenated();


                    IDbCommand result = database.Query("SELECT * FROM inventoryfolders WHERE parentFolderID = @parentFolderID", param);
                    IDataReader reader = result.ExecuteReader();

                    List<InventoryFolderBase> items = new List<InventoryFolderBase>();
                    
                    while(reader.Read())
                        items.Add(readInventoryFolder(reader));

                    reader.Close();
                    result.Dispose();

                    return items;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                MainLog.Instance.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Reads a one item from an SQL result
        /// </summary>
        /// <param name="reader">The SQL Result</param>
        /// <returns>the item read</returns>
        public InventoryItemBase readInventoryItem(IDataReader reader)
        {
            try
            {
                InventoryItemBase item = new InventoryItemBase();

                item.inventoryID = new LLUUID((string)reader["inventoryID"]);
                item.assetID = new LLUUID((string)reader["assetID"]);
                item.assetType = (int)reader["assetType"];
                item.parentFolderID = new LLUUID((string)reader["parentFolderID"]);
                item.avatarID = new LLUUID((string)reader["avatarID"]);
                item.inventoryName = (string)reader["inventoryName"];
                item.inventoryDescription = (string)reader["inventoryDescription"];
                item.inventoryNextPermissions = Convert.ToUInt32(reader["inventoryNextPermissions"]);
                item.inventoryCurrentPermissions = Convert.ToUInt32(reader["inventoryCurrentPermissions"]);
                item.invType = (int)reader["invType"];
                item.creatorsID = new LLUUID((string)reader["creatorID"]);
                item.inventoryBasePermissions = Convert.ToUInt32(reader["inventoryBasePermissions"]);
                item.inventoryEveryOnePermissions = Convert.ToUInt32(reader["inventoryEveryOnePermissions"]);
                return item;
            }
            catch (SqlException e)
            {
                MainLog.Instance.Error(e.ToString());
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
                    param["inventoryID"] = itemID.ToStringHyphenated();

                    IDbCommand result = database.Query("SELECT * FROM inventoryitems WHERE inventoryID = @inventoryID", param);
                    IDataReader reader = result.ExecuteReader();

                    InventoryItemBase item = null;
                    if(reader.Read())
                        item = readInventoryItem(reader);

                    reader.Close();
                    result.Dispose();

                    return item;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                MainLog.Instance.Error(e.ToString());
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
                folder.agentID = new LLUUID((string)reader["agentID"]);
                folder.parentID = new LLUUID((string)reader["parentFolderID"]);
                folder.folderID = new LLUUID((string)reader["folderID"]);
                folder.name = (string)reader["folderName"];
                folder.type = (short)reader["type"];
                folder.version = (ushort)((int)reader["version"]);
                return folder;
            }
            catch (Exception e)
            {
                MainLog.Instance.Error(e.ToString());
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
                    Dictionary<string, string> param = new Dictionary<string,string>();
                    param["uuid"] = folderID.ToStringHyphenated();

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
                MainLog.Instance.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Adds a specified item to the database
        /// </summary>
        /// <param name="item">The inventory item</param>
        public void addInventoryItem(InventoryItemBase item)
        {
            string sql = "INSERT INTO inventoryitems";
            sql += "([inventoryID], [assetID], [assetType], [parentFolderID], [avatarID], [inventoryName], [inventoryDescription], [inventoryNextPermissions], [inventoryCurrentPermissions], [invType], [creatorID], [inventoryBasePermissions], [inventoryEveryOnePermissions]) VALUES ";
            sql += "(@inventoryID, @assetID, @assetType, @parentFolderID, @avatarID, @inventoryName, @inventoryDescription, @inventoryNextPermissions, @inventoryCurrentPermissions, @invType, @creatorID, @inventoryBasePermissions, @inventoryEveryOnePermissions);";

            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["inventoryID"] = item.inventoryID.ToStringHyphenated();
                param["assetID"] = item.assetID.ToStringHyphenated();
                param["assetType"] = item.assetType.ToString();
                param["parentFolderID"] = item.parentFolderID.ToStringHyphenated();
                param["avatarID"] = item.avatarID.ToStringHyphenated();
                param["inventoryName"] = item.inventoryName;
                param["inventoryDescription"] = item.inventoryDescription;
                param["inventoryNextPermissions"] = item.inventoryNextPermissions.ToString();
                param["inventoryCurrentPermissions"] = item.inventoryCurrentPermissions.ToString();
                param["invType"] = Convert.ToString(item.invType);
                param["creatorID"] = item.creatorsID.ToStringHyphenated();
                param["inventoryBasePermissions"] = Convert.ToString(item.inventoryBasePermissions);
                param["inventoryEveryOnePermissions"] = Convert.ToString(item.inventoryEveryOnePermissions);

                IDbCommand result = database.Query(sql, param);
                result.ExecuteNonQuery();
                result.Dispose();

            }
            catch (SqlException e)
            {
                MainLog.Instance.Error(e.ToString());
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
                                                                       "inventoryName = @inventoryName"+
                                                                       "inventoryDescription = @inventoryDescription" +
                                                                       "inventoryNextPermissions = @inventoryNextPermissions" +
                                                                       "inventoryCurrentPermissions = @inventoryCurrentPermissions" +
                                                                       "invType = @invType" +
                                                                       "creatorID = @creatorID" +
                                                                       "inventoryBasePermissions = @inventoryBasePermissions" +
                                                                       "inventoryEveryOnePermissions = @inventoryEveryOnePermissions) where " +
                                                                       "invenoryID = @keyInventoryID;", database.getConnection());
            SqlParameter param1 = new SqlParameter("@inventoryID", item.inventoryID.ToStringHyphenated());
            SqlParameter param2 = new SqlParameter("@assetID", item.assetID);
            SqlParameter param3 = new SqlParameter("@assetType", item.assetType);
            SqlParameter param4 = new SqlParameter("@parentFolderID", item.parentFolderID);
            SqlParameter param5 = new SqlParameter("@avatarID", item.avatarID);
            SqlParameter param6 = new SqlParameter("@inventoryName", item.inventoryName);
            SqlParameter param7 = new SqlParameter("@inventoryDescription", item.inventoryDescription);
            SqlParameter param8 = new SqlParameter("@inventoryNextPermissions", item.inventoryNextPermissions);
            SqlParameter param9 = new SqlParameter("@inventoryCurrentPermissions", item.inventoryCurrentPermissions);
            SqlParameter param10 = new SqlParameter("@invType", item.invType);
            SqlParameter param11 = new SqlParameter("@creatorID", item.creatorsID);
            SqlParameter param12 = new SqlParameter("@inventoryBasePermissions", item.inventoryBasePermissions);
            SqlParameter param13 = new SqlParameter("@inventoryEveryOnePermissions", item.inventoryEveryOnePermissions);            
            SqlParameter param14 = new SqlParameter("@keyInventoryID", item.inventoryID.ToStringHyphenated());
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
                MainLog.Instance.Error(e.ToString());
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
                param["uuid"] = itemID.ToStringHyphenated();

                IDbCommand cmd = database.Query("DELETE FROM inventoryitems WHERE inventoryID=@uuid", param);
                cmd.ExecuteNonQuery();
                cmd.Dispose();
                
                
            }
            catch (SqlException e)
            {
                database.Reconnect();
                MainLog.Instance.Error(e.ToString());
            }
        }

        /// <summary>
        /// Creates a new inventory folder
        /// </summary>
        /// <param name="folder">Folder to create</param>
        public void addInventoryFolder(InventoryFolderBase folder)
        {
            string sql = "INSERT INTO inventoryfolders ([folderID], [agentID], [parentFolderID], [folderName], [type], [version]) VALUES ";
            sql += "(@folderID, @agentID, @parentFolderID, @folderName, @type, @version);";


            Dictionary<string, string> param = new Dictionary<string, string>();
            param["folderID"] = folder.folderID.ToStringHyphenated();
            param["agentID"] = folder.agentID.ToStringHyphenated();
            param["parentFolderID"] = folder.parentID.ToStringHyphenated();
            param["folderName"] = folder.name;
            param["type"] = Convert.ToString(folder.type);
            param["version"] = Convert.ToString(folder.version);
            
            try
            {                
                IDbCommand result = database.Query(sql, param);
                result.ExecuteNonQuery();
                result.Dispose();
            }
            catch (Exception e)
            {
                MainLog.Instance.Error(e.ToString());
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
            SqlParameter param1 = new SqlParameter("@folderID", folder.folderID.ToStringHyphenated());
            SqlParameter param2 = new SqlParameter("@agentID", folder.agentID.ToStringHyphenated());
            SqlParameter param3 = new SqlParameter("@parentFolderID", folder.parentID.ToStringHyphenated());
            SqlParameter param4 = new SqlParameter("@folderName", folder.name);
            SqlParameter param5 = new SqlParameter("@type", folder.type);
            SqlParameter param6 = new SqlParameter("@version", folder.version);
            SqlParameter param7 = new SqlParameter("@keyFolderID", folder.folderID.ToStringHyphenated());
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
                MainLog.Instance.Error(e.ToString());
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
        
        /// <summary>
        /// Returns all child folders in the hierarchy from the parent folder and down
        /// </summary>
        /// <param name="parentID">The folder to get subfolders for</param>
        /// <returns>A list of inventory folders</returns>
        protected List<InventoryFolderBase> getFolderHierarchy(LLUUID parentID)
        {
            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            getInventoryFolders(ref folders, parentID);

            for (int i = 0; i < folders.Count; i++)
                getInventoryFolders(ref folders, folders[i].folderID);

            return folders;
        }

        protected void deleteOneFolder(LLUUID folderID)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["folderID"] = folderID.ToStringHyphenated();

                IDbCommand cmd = database.Query("DELETE FROM inventoryfolders WHERE folderID=@folderID", param);
                cmd.ExecuteNonQuery();
                cmd.Dispose();

            }
            catch (SqlException e)
            {
                database.Reconnect();
                MainLog.Instance.Error(e.ToString());
            }
        }

        protected void deleteItemsInFolder(LLUUID folderID)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["parentFolderID"] = folderID.ToStringHyphenated();


                IDbCommand cmd = database.Query("DELETE FROM inventoryitems WHERE parentFolderID=@parentFolderID", param);
                cmd.ExecuteNonQuery();
                cmd.Dispose();
            }
            catch (SqlException e)
            {
                database.Reconnect();
                MainLog.Instance.Error(e.ToString());
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
                    deleteOneFolder(f.folderID);
                    deleteItemsInFolder(f.folderID);
                }

                //Delete the actual row
                deleteOneFolder(folderID);
                deleteItemsInFolder(folderID);
            }
        }
    }
}