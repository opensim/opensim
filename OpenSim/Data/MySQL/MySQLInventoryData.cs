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
using System.Reflection;
using libsecondlife;
using log4net;
using MySql.Data.MySqlClient;
using OpenSim.Framework;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A MySQL interface for the inventory server
    /// </summary>
    public class MySQLInventoryData : IInventoryDataPlugin
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The database manager
        /// </summary>
        private MySQLManager database;

        public void Initialise() 
        { 
            m_log.Info("[MySQLInventoryData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException (Name);
        }

        /// <summary>
        /// <para>Initialises Inventory interface</para>
        /// <para>
        /// <list type="bullet">
        /// <item>Loads and initialises the MySQL storage plugin</item>
        /// <item>warns and uses the obsolete mysql_connection.ini if connect string is empty.</item>
        /// <item>Check for migration</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="connect">connect string</param>
        public void Initialise(string connect)
        {
            if (connect != String.Empty)
            {
                database = new MySQLManager(connect);
            }
            else
            {
                m_log.Warn("Reverting to deprecated mysql_connection.ini file for connection info");
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
            }

            // This actually does the roll forward assembly stuff
            Assembly assem = GetType().Assembly;
            Migration m = new Migration(database.Connection, assem, "InventoryStore");

            // TODO: After rev 6000, remove this.  People should have
            // been rolled onto the new migration code by then.
            TestTables(database.Connection, m);

            m.Update();
        }

        #region Test and initialization code

        /// <summary>
        /// <list type="bullet">
        /// <item>Execute CreateFoldersTable.sql if oldVersion == null</item>
        /// <item>do nothing if oldVersion != null</item>
        /// </list>
        /// </summary>
        /// <param name="oldVersion"></param>
        private void UpgradeFoldersTable(string oldVersion)
        {
            // null as the version, indicates that the table didn't exist
            if (oldVersion == null)
            {
                database.ExecuteResourceSql("CreateFoldersTable.sql");
                return;
            }

            //// if the table is already at the current version, then we can exit immediately
            // if (oldVersion == "Rev. 2")
            // return;
            // database.ExecuteResourceSql("UpgradeFoldersTableToVersion2.sql");
        }

        /// <summary>
        /// <list type="bullet">
        /// <item>Execute CreateItemsTable.sql if oldVersion == null</item>
        /// <item>Execute "UpgradeItemsTableToVersion3.sql" if oldVersion start with "Rev. 2;"</item>
        /// </list>
        /// </summary>
        /// <param name="oldVersion"></param>
        private void UpgradeItemsTable(string oldVersion)
        {
            // null as the version, indicates that the table didn't exist
            if (oldVersion == null)
            {
                database.ExecuteResourceSql("CreateItemsTable.sql");
                return;
            }

            // if the table is already at the current version, then we can exit immediately
            if (oldVersion.StartsWith("Rev. 2;"))
            {
                m_log.Info("[INVENTORY DB]: Upgrading inventory items table from Rev. 2 to Rev. 3");
                database.ExecuteResourceSql("UpgradeItemsTableToVersion3.sql");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn">MySQL connection handler</param>
        /// <param name="m"></param>
        private void TestTables(MySqlConnection conn, Migration m)
        {
            Dictionary<string, string> tableList = new Dictionary<string, string>();

            tableList["inventoryfolders"] = null;
            tableList["inventoryitems"] = null;

            database.GetTableVersion(tableList);

            // if we've already started using migrations, get out of
            // here, we've got this under control
            if (m.Version > 0)
                return;

            // if there are no tables, get out of here and let
            // migrations do their job
            if (
               tableList["inventoryfolders"] == null &&
               tableList["inventoryitems"] == null
               )
                return;

            // otherwise, let the upgrade on legacy proceed...
            UpgradeFoldersTable(tableList["inventoryfolders"]);
            UpgradeItemsTable(tableList["inventoryitems"]);

            // ... and set the version
            if (m.Version == 0)
                m.Version = 1;
            
        }

        #endregion

        /// <summary>
        /// The name of this DB provider
        /// </summary>
        /// <returns>Name of DB provider</returns>
        public string Name
        {
            get { return "MySQL Inventory Data Interface"; }
        }

        /// <summary>
        /// Closes this DB provider
        /// </summary>
        /// <remarks>do nothing</remarks>
        public void Dispose()
        {
            // Do nothing.
        }

        /// <summary>
        /// Returns the version of this DB provider
        /// </summary>
        /// <returns>A string containing the DB provider version</returns>
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
                lock (database)
                {
                    List<InventoryItemBase> items = new List<InventoryItemBase>();

                    database.CheckConnection();

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
                    database.CheckConnection();

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
                m_log.Error(e.ToString());
                return null;
            }
        }

     
        /// <summary>
        /// see <see cref="InventoryItemBase.getUserRootFolder"/>
        /// </summary>
        /// <param name="user">The user UUID</param>
        /// <returns></returns>
        public InventoryFolderBase getUserRootFolder(LLUUID user)
        {
            try
            {
                lock (database)
                {
                    database.CheckConnection();

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
                m_log.Error(e.ToString());
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
                    database.CheckConnection();

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
                m_log.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Reads a one item from an SQL result
        /// </summary>
        /// <param name="reader">The SQL Result</param>
        /// <returns>the item read</returns>
        private static InventoryItemBase readInventoryItem(MySqlDataReader reader)
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
                item.NextPermissions = (uint) reader["inventoryNextPermissions"];
                item.CurrentPermissions = (uint) reader["inventoryCurrentPermissions"];
                item.InvType = (int) reader["invType"];
                item.Creator = new LLUUID((string) reader["creatorID"]);
                item.BasePermissions = (uint) reader["inventoryBasePermissions"];
                item.EveryOnePermissions = (uint) reader["inventoryEveryOnePermissions"];
                item.SalePrice = (int) reader["salePrice"];
                item.SaleType = Convert.ToByte(reader["saleType"]);
                item.CreationDate = (int) reader["creationDate"];
                item.GroupID = new LLUUID(reader["groupID"].ToString());
                item.GroupOwned = Convert.ToBoolean(reader["groupOwned"]);
                item.Flags = (uint) reader["flags"];

                return item;
            }
            catch (MySqlException e)
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
                    database.CheckConnection();

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
                m_log.Error(e.ToString());
            }
            return null;
        }

        /// <summary>
        /// Reads a list of inventory folders returned by a query.
        /// </summary>
        /// <param name="reader">A MySQL Data Reader</param>
        /// <returns>A List containing inventory folders</returns>
        protected static InventoryFolderBase readInventoryFolder(MySqlDataReader reader)
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
                    database.CheckConnection();

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
            string sql =
                "REPLACE INTO inventoryitems (inventoryID, assetID, assetType, parentFolderID, avatarID, inventoryName"
                    + ", inventoryDescription, inventoryNextPermissions, inventoryCurrentPermissions, invType"
                    + ", creatorID, inventoryBasePermissions, inventoryEveryOnePermissions, salePrice, saleType"
                    + ", creationDate, groupID, groupOwned, flags) VALUES ";
            sql +=
                "(?inventoryID, ?assetID, ?assetType, ?parentFolderID, ?avatarID, ?inventoryName, ?inventoryDescription"
                    + ", ?inventoryNextPermissions, ?inventoryCurrentPermissions, ?invType, ?creatorID"
                    + ", ?inventoryBasePermissions, ?inventoryEveryOnePermissions, ?salePrice, ?saleType, ?creationDate"
                    + ", ?groupID, ?groupOwned, ?flags)";

            try
            {
                database.CheckConnection();

                MySqlCommand result = new MySqlCommand(sql, database.Connection);
                result.Parameters.AddWithValue("?inventoryID", item.ID.ToString());
                result.Parameters.AddWithValue("?assetID", item.AssetID.ToString());
                result.Parameters.AddWithValue("?assetType", item.AssetType.ToString());
                result.Parameters.AddWithValue("?parentFolderID", item.Folder.ToString());
                result.Parameters.AddWithValue("?avatarID", item.Owner.ToString());
                result.Parameters.AddWithValue("?inventoryName", item.Name);
                result.Parameters.AddWithValue("?inventoryDescription", item.Description);
                result.Parameters.AddWithValue("?inventoryNextPermissions", item.NextPermissions.ToString());
                result.Parameters.AddWithValue("?inventoryCurrentPermissions",
                                               item.CurrentPermissions.ToString());
                result.Parameters.AddWithValue("?invType", item.InvType);
                result.Parameters.AddWithValue("?creatorID", item.Creator.ToString());
                result.Parameters.AddWithValue("?inventoryBasePermissions", item.BasePermissions);
                result.Parameters.AddWithValue("?inventoryEveryOnePermissions", item.EveryOnePermissions);
                result.Parameters.AddWithValue("?salePrice", item.SalePrice);
                result.Parameters.AddWithValue("?saleType", item.SaleType);
                result.Parameters.AddWithValue("?creationDate", item.CreationDate);
                result.Parameters.AddWithValue("?groupID", item.GroupID);
                result.Parameters.AddWithValue("?groupOwned", item.GroupOwned);
                result.Parameters.AddWithValue("?flags", item.Flags);

                lock (database)
                {
                    result.ExecuteNonQuery();
                }

                result.Dispose();
            }
            catch (MySqlException e)
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
            addInventoryItem(item);
        }

        /// <summary>
        /// Detele the specified inventory item
        /// </summary>
        /// <param name="item">The inventory item UUID to delete</param>
        public void deleteInventoryItem(LLUUID itemID)
        {
            try
            {
                database.CheckConnection();

                MySqlCommand cmd =
                    new MySqlCommand("DELETE FROM inventoryitems WHERE inventoryID=?uuid", database.Connection);
                cmd.Parameters.AddWithValue("?uuid", itemID.ToString());

                lock (database)
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch (MySqlException e)
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
                "REPLACE INTO inventoryfolders (folderID, agentID, parentFolderID, folderName, type, version) VALUES ";
            sql += "(?folderID, ?agentID, ?parentFolderID, ?folderName, ?type, ?version)";

            database.CheckConnection();

            MySqlCommand cmd = new MySqlCommand(sql, database.Connection);
            cmd.Parameters.AddWithValue("?folderID", folder.ID.ToString());
            cmd.Parameters.AddWithValue("?agentID", folder.Owner.ToString());
            cmd.Parameters.AddWithValue("?parentFolderID", folder.ParentID.ToString());
            cmd.Parameters.AddWithValue("?folderName", folder.Name);
            cmd.Parameters.AddWithValue("?type", (short) folder.Type);
            cmd.Parameters.AddWithValue("?version", folder.Version);

            try
            {
                lock (database)
                {
                    cmd.ExecuteNonQuery();
                }
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
            addInventoryFolder(folder);
        }

        /// <summary>
        /// Move an inventory folder
        /// </summary>
        /// <param name="folder">Folder to move</param>
        /// <remarks>UPDATE inventoryfolders SET parentFolderID=?parentFolderID WHERE folderID=?folderID</remarks>
        public void moveInventoryFolder(InventoryFolderBase folder)
        {
            string sql =
                "UPDATE inventoryfolders SET parentFolderID=?parentFolderID WHERE folderID=?folderID";

            database.CheckConnection();

            MySqlCommand cmd = new MySqlCommand(sql, database.Connection);
            cmd.Parameters.AddWithValue("?folderID", folder.ID.ToString());
            cmd.Parameters.AddWithValue("?parentFolderID", folder.ParentID.ToString());

            try
            {
                lock (database)
                {
                    cmd.ExecuteNonQuery();
                }
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


        /// <summary>
        /// See IInventoryDataPlugin
        /// </summary>
        /// <param name="parentID"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> getFolderHierarchy(LLUUID parentID)
        {
            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            getInventoryFolders(ref folders, parentID);

            for (int i = 0; i < folders.Count; i++)
                getInventoryFolders(ref folders, folders[i].ID);

            return folders;
        }

        /// <summary>
        /// Delete a folder from database
        /// </summary>
        /// <param name="folderID">the folder UUID</param>
        protected void deleteOneFolder(LLUUID folderID)
        {
            try
            {
                database.CheckConnection();

                MySqlCommand cmd =
                    new MySqlCommand("DELETE FROM inventoryfolders WHERE folderID=?uuid", database.Connection);
                cmd.Parameters.AddWithValue("?uuid", folderID.ToString());

                lock (database)
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch (MySqlException e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
            }
        }

        /// <summary>
        /// Delete all item in a folder
        /// </summary>
        /// <param name="folderID">the folder UUID</param>
        protected void deleteItemsInFolder(LLUUID folderID)
        {
            try
            {
                database.CheckConnection();

                MySqlCommand cmd =
                    new MySqlCommand("DELETE FROM inventoryitems WHERE parentFolderID=?uuid", database.Connection);
                cmd.Parameters.AddWithValue("?uuid", folderID.ToString());

                lock (database)
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch (MySqlException e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
            }
        }

        /// <summary>
        /// Deletes an inventory folder
        /// </summary>
        /// <param name="folderId">Id of folder to delete</param>
        public void deleteInventoryFolder(LLUUID folderID)
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
