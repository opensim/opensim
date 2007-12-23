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
* 
*/
using System;
using System.Collections.Generic;
using libsecondlife;
using MySql.Data.MySqlClient;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Data.MySQL
{
    /// <summary>
    /// A MySQL interface for the inventory server
    /// </summary>
    public class MySQLInventoryData : IInventoryData
    {
        /// <summary>
        /// The database manager
        /// </summary>
        private MySQLManager database;

        /// <summary>
        /// Loads and initialises this database plugin
        /// </summary>
        public void Initialise()
        {
            IniFile GridDataMySqlFile = new IniFile("mysql_connection.ini");
            string settingHostname = GridDataMySqlFile.ParseFileReadValue("hostname");
            string settingDatabase = GridDataMySqlFile.ParseFileReadValue("database");
            string settingUsername = GridDataMySqlFile.ParseFileReadValue("username");
            string settingPassword = GridDataMySqlFile.ParseFileReadValue("password");
            string settingPooling = GridDataMySqlFile.ParseFileReadValue("pooling");
            string settingPort = GridDataMySqlFile.ParseFileReadValue("port");

            database =
                new MySQLManager(settingHostname, settingDatabase, settingUsername, settingPassword, settingPooling,
                                 settingPort);
            TestTables(database.Connection);
        }

        #region Test and initialization code

        private void UpgradeFoldersTable(string oldVersion)
        {
            // null as the version, indicates that the table didn't exist
            if (oldVersion == null)
            {
                database.ExecuteResourceSql("CreateFoldersTable.sql");
                return;
            }

            // if the table is already at the current version, then we can exit immediately
//             if (oldVersion == "Rev. 2")
//                 return;

//             database.ExecuteResourceSql("UpgradeFoldersTableToVersion2.sql");
        }

        private void UpgradeItemsTable(string oldVersion)
        {
            // null as the version, indicates that the table didn't exist
            if (oldVersion == null)
            {
                database.ExecuteResourceSql("CreateItemsTable.sql");
                return;
            }

            // if the table is already at the current version, then we can exit immediately
//             if (oldVersion == "Rev. 2")
//                 return;

//             database.ExecuteResourceSql("UpgradeItemsTableToVersion2.sql");
        }

        private void TestTables(MySqlConnection conn)
        {
            Dictionary<string, string> tableList = new Dictionary<string, string>();

            tableList["inventoryfolders"] = null;
            tableList["inventoryitems"] = null;

            database.GetTableVersion(tableList);
            MainLog.Instance.Verbose("MYSQL", "Inventory Folder Version: " + tableList["inventoryfolders"]);
            MainLog.Instance.Verbose("MYSQL", "Inventory Items Version: " + tableList["inventoryitems"]);

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
            return "MySQL Inventory Data Interface";
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

                    MySqlCommand result =
                        new MySqlCommand("SELECT * FROM inventoryitems WHERE parentFolderID = ?uuid",
                                         database.Connection);
                    result.Parameters.AddWithValue("?uuid", folderID.ToString());
                    MySqlDataReader reader = result.ExecuteReader();

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
                    MySqlCommand result =
                        new MySqlCommand(
                            "SELECT * FROM inventoryfolders WHERE parentFolderID = ?zero AND agentID = ?uuid",
                            database.Connection);
                    result.Parameters.AddWithValue("?uuid", user.ToString());
                    result.Parameters.AddWithValue("?zero", LLUUID.Zero.ToString());
                    MySqlDataReader reader = result.ExecuteReader();

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
                MainLog.Instance.Error(e.ToString());
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
                    MySqlCommand result =
                        new MySqlCommand(
                            "SELECT * FROM inventoryfolders WHERE parentFolderID = ?zero AND agentID = ?uuid",
                            database.Connection);
                    result.Parameters.AddWithValue("?uuid", user.ToString());
                    result.Parameters.AddWithValue("?zero", LLUUID.Zero.ToString());

                    MySqlDataReader reader = result.ExecuteReader();

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
                MainLog.Instance.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Return a list of folders in a users inventory contained within the specified folder.
        /// This method is only used in tests - in normal operation the user always have one,
        /// and only one, root folder.
        /// </summary>
        /// <param name="parentID">The folder to search</param>
        /// <returns>A list of inventory folders</returns>
        public List<InventoryFolderBase> getInventoryFolders(LLUUID parentID)
        {
            try
            {
                lock (database)
                {
                    MySqlCommand result =
                        new MySqlCommand("SELECT * FROM inventoryfolders WHERE parentFolderID = ?uuid",
                                         database.Connection);
                    result.Parameters.AddWithValue("?uuid", parentID.ToString());
                    MySqlDataReader reader = result.ExecuteReader();

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
                MainLog.Instance.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Reads a one item from an SQL result
        /// </summary>
        /// <param name="reader">The SQL Result</param>
        /// <returns>the item read</returns>
        private InventoryItemBase readInventoryItem(MySqlDataReader reader)
        {
            try
            {
                InventoryItemBase item = new InventoryItemBase();

                item.inventoryID = new LLUUID((string) reader["inventoryID"]);
                item.assetID = new LLUUID((string) reader["assetID"]);
                item.assetType = (int) reader["assetType"];
                item.parentFolderID = new LLUUID((string) reader["parentFolderID"]);
                item.avatarID = new LLUUID((string) reader["avatarID"]);
                item.inventoryName = (string) reader["inventoryName"];
                item.inventoryDescription = (string) reader["inventoryDescription"];
                item.inventoryNextPermissions = (uint) reader["inventoryNextPermissions"];
                item.inventoryCurrentPermissions = (uint) reader["inventoryCurrentPermissions"];
                item.invType = (int) reader["invType"];
                item.creatorsID = new LLUUID((string) reader["creatorID"]);
                item.inventoryBasePermissions = (uint) reader["inventoryBasePermissions"];
                item.inventoryEveryOnePermissions = (uint) reader["inventoryEveryOnePermissions"];
                return item;
            }
            catch (MySqlException e)
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

                    MySqlCommand result =
                        new MySqlCommand("SELECT * FROM inventoryitems WHERE inventoryID = ?uuid", database.Connection);
                    result.Parameters.AddWithValue("?uuid", itemID.ToString());
                    MySqlDataReader reader = result.ExecuteReader();

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
                MainLog.Instance.Error(e.ToString());
            }
            return null;
        }

        /// <summary>
        /// Reads a list of inventory folders returned by a query.
        /// </summary>
        /// <param name="reader">A MySQL Data Reader</param>
        /// <returns>A List containing inventory folders</returns>
        protected InventoryFolderBase readInventoryFolder(MySqlDataReader reader)
        {
            try
            {
                InventoryFolderBase folder = new InventoryFolderBase();
                folder.agentID = new LLUUID((string) reader["agentID"]);
                folder.parentID = new LLUUID((string) reader["parentFolderID"]);
                folder.folderID = new LLUUID((string) reader["folderID"]);
                folder.name = (string) reader["folderName"];
                folder.type = (short) reader["type"];
                folder.version = (ushort) ((int) reader["version"]);
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
                    MySqlCommand result =
                        new MySqlCommand("SELECT * FROM inventoryfolders WHERE folderID = ?uuid", database.Connection);
                    result.Parameters.AddWithValue("?uuid", folderID.ToString());
                    MySqlDataReader reader = result.ExecuteReader();

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
            string sql =
                "REPLACE INTO inventoryitems (inventoryID, assetID, assetType, parentFolderID, avatarID, inventoryName, inventoryDescription, inventoryNextPermissions, inventoryCurrentPermissions, invType, creatorID, inventoryBasePermissions, inventoryEveryOnePermissions) VALUES ";
            sql +=
                "(?inventoryID, ?assetID, ?assetType, ?parentFolderID, ?avatarID, ?inventoryName, ?inventoryDescription, ?inventoryNextPermissions, ?inventoryCurrentPermissions, ?invType, ?creatorID, ?inventoryBasePermissions, ?inventoryEveryOnePermissions)";

            try
            {
                MySqlCommand result = new MySqlCommand(sql, database.Connection);
                result.Parameters.AddWithValue("?inventoryID", item.inventoryID.ToString());
                result.Parameters.AddWithValue("?assetID", item.assetID.ToString());
                result.Parameters.AddWithValue("?assetType", item.assetType.ToString());
                result.Parameters.AddWithValue("?parentFolderID", item.parentFolderID.ToString());
                result.Parameters.AddWithValue("?avatarID", item.avatarID.ToString());
                result.Parameters.AddWithValue("?inventoryName", item.inventoryName);
                result.Parameters.AddWithValue("?inventoryDescription", item.inventoryDescription);
                result.Parameters.AddWithValue("?inventoryNextPermissions", item.inventoryNextPermissions.ToString());
                result.Parameters.AddWithValue("?inventoryCurrentPermissions",
                                               item.inventoryCurrentPermissions.ToString());
                result.Parameters.AddWithValue("?invType", item.invType);
                result.Parameters.AddWithValue("?creatorID", item.creatorsID.ToString());
                result.Parameters.AddWithValue("?inventoryBasePermissions", item.inventoryBasePermissions);
                result.Parameters.AddWithValue("?inventoryEveryOnePermissions", item.inventoryEveryOnePermissions);
                result.ExecuteNonQuery();
                result.Dispose();
            }
            catch (MySqlException e)
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
            addInventoryItem(item);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        public void deleteInventoryItem(LLUUID itemID)
        {
            try
            {
                MySqlCommand cmd =
                    new MySqlCommand("DELETE FROM inventoryitems WHERE inventoryID=?uuid", database.Connection);
                cmd.Parameters.AddWithValue("?uuid", itemID.ToString());
                cmd.ExecuteNonQuery();
            }
            catch (MySqlException e)
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
            string sql =
                "REPLACE INTO inventoryfolders (folderID, agentID, parentFolderID, folderName, type, version) VALUES ";
            sql += "(?folderID, ?agentID, ?parentFolderID, ?folderName, ?type, ?version)";

            MySqlCommand cmd = new MySqlCommand(sql, database.Connection);
            cmd.Parameters.AddWithValue("?folderID", folder.folderID.ToString());
            cmd.Parameters.AddWithValue("?agentID", folder.agentID.ToString());
            cmd.Parameters.AddWithValue("?parentFolderID", folder.parentID.ToString());
            cmd.Parameters.AddWithValue("?folderName", folder.name);
            cmd.Parameters.AddWithValue("?type", (short) folder.type);
            cmd.Parameters.AddWithValue("?version", folder.version);

            try
            {
                cmd.ExecuteNonQuery();
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
            addInventoryFolder(folder);
        }

        /// Creates a new inventory folder
        /// </summary>
        /// <param name="folder">Folder to create</param>
        public void moveInventoryFolder(InventoryFolderBase folder)
        {
            string sql =
                "UPDATE inventoryfolders SET parentFolderID=?parentFolderID WHERE folderID=?folderID";

            MySqlCommand cmd = new MySqlCommand(sql, database.Connection);
            cmd.Parameters.AddWithValue("?folderID", folder.folderID.ToString());
            cmd.Parameters.AddWithValue("?parentFolderID", folder.parentID.ToString());


            try
            {
                cmd.ExecuteNonQuery();
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
                MySqlCommand cmd =
                    new MySqlCommand("DELETE FROM inventoryfolders WHERE folderID=?uuid", database.Connection);
                cmd.Parameters.AddWithValue("?uuid", folderID.ToString());
                cmd.ExecuteNonQuery();
            }
            catch (MySqlException e)
            {
                database.Reconnect();
                MainLog.Instance.Error(e.ToString());
            }
        }

        protected void deleteItemsInFolder(LLUUID folderID)
        {
            try
            {
                MySqlCommand cmd =
                    new MySqlCommand("DELETE FROM inventoryitems WHERE parentFolderID=?uuid", database.Connection);
                cmd.Parameters.AddWithValue("?uuid", folderID.ToString());
                cmd.ExecuteNonQuery();
            }
            catch (MySqlException e)
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
