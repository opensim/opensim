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
using System.Reflection;
using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework.Types;
using OpenSim.Framework.Console;
using MySql.Data.MySqlClient;

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

            database = new MySQLManager(settingHostname, settingDatabase, settingUsername, settingPassword, settingPooling, settingPort);
            TestTables(database.Connection);
        }

        #region Test and initialization code
        /// <summary>
        /// Extract a named string resource from the embedded resources
        /// </summary>
        /// <param name="name">name of embedded resource</param>
        /// <returns>string contained within the embedded resource</returns>
        private string getResourceString(string name)
        {
            Assembly assem = this.GetType().Assembly;
            string[] names = assem.GetManifestResourceNames();

            foreach(string s in names)
                if(s.EndsWith(name))
                    using (Stream resource = assem.GetManifestResourceStream(s))
                    {
                        using (StreamReader resourceReader = new StreamReader(resource))
                        {
                            string resourceString = resourceReader.ReadToEnd();
                            return resourceString;
                        }
                    }
            throw new Exception(string.Format("Resource '{0}' was not found", name));
        }

        private void ExecuteResourceSql(MySqlConnection conn, string name)
        {
            MySqlCommand cmd = new MySqlCommand(getResourceString(name), conn);
            cmd.ExecuteNonQuery();
        }

        private void UpgradeFoldersTable(MySqlConnection conn, string oldVersion)
        {
            // null as the version, indicates that the table didn't exist
            if (oldVersion == null)
            {
                ExecuteResourceSql(conn, "CreateFoldersTable.sql");
                return;
            }

            // if the table is already at the current version, then we can exit immediately
            if (oldVersion == "Rev. 2")
                return;

            ExecuteResourceSql(conn, "UpgradeFoldersTableToVersion2.sql");
        }

        private void UpgradeItemsTable(MySqlConnection conn, string oldVersion)
        {
            // null as the version, indicates that the table didn't exist
            if (oldVersion == null)
            {
                ExecuteResourceSql(conn, "CreateItemsTable.sql");
                return;
            }

            // if the table is already at the current version, then we can exit immediately
            if (oldVersion == "Rev. 2")
                return;

            ExecuteResourceSql(conn, "UpgradeItemsTableToVersion2.sql");
        }

        private void TestTables(MySqlConnection conn)
        {

            Dictionary<string, string> tableList = new Dictionary<string, string>();

            tableList["inventoryfolders"] = null;
            tableList["inventoryitems"] = null;

            MySqlCommand tablesCmd = new MySqlCommand("SELECT TABLE_NAME, TABLE_COMMENT FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='opensim'", conn);
            MySqlDataReader tables = tablesCmd.ExecuteReader();
            while (tables.Read())
            {
                try
                {
                    string tableName = (string)tables["TABLE_NAME"];
                    string comment = (string)tables["TABLE_COMMENT"];
                    tableList[tableName] = comment;
                }
                catch (Exception e)
                {
                    MainLog.Instance.Error(e.ToString());
                }
            }
            tables.Close();

            UpgradeFoldersTable(conn, tableList["inventoryfolders"]);
            UpgradeItemsTable(conn, tableList["inventoryitems"]);
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
            System.Reflection.Module module = this.GetType().Module;
            string dllName = module.Assembly.ManifestModule.Name;
            Version dllVersion = module.Assembly.GetName().Version;


            return string.Format("{0}.{1}.{2}.{3}", dllVersion.Major, dllVersion.Minor, dllVersion.Build, dllVersion.Revision);
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

                    MySqlCommand result = new MySqlCommand("SELECT * FROM inventoryitems WHERE parentFolderID = ?uuid", database.Connection);
                    result.Parameters.Add("?uuid", folderID.ToStringHyphenated());
                    MySqlDataReader reader = result.ExecuteReader();

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
                    MySqlCommand result = new MySqlCommand("SELECT * FROM inventoryfolders WHERE parentFolderID = ?zero AND agentID = ?uuid", database.Connection);
                    result.Parameters.Add("?uuid", user.ToStringHyphenated());
                    result.Parameters.Add("?zero", LLUUID.Zero.ToStringHyphenated());
                    MySqlDataReader reader = result.ExecuteReader();

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
                    param["?uuid"] = user.ToStringHyphenated();
                    param["?zero"] = LLUUID.Zero.ToStringHyphenated();

                    MySqlCommand result = new MySqlCommand("SELECT * FROM inventoryfolders WHERE parentFolderID = ?zero AND agentID = ?uuid", database.Connection);
                    result.Parameters.Add("?uuid", user.ToStringHyphenated());
                    result.Parameters.Add("?zero", LLUUID.Zero.ToStringHyphenated());

                    MySqlDataReader reader = result.ExecuteReader();

                    List<InventoryFolderBase> items = new List<InventoryFolderBase>();
                    while(reader.Read())
                        items.Add(readInventoryFolder(reader));

                    InventoryFolderBase rootFolder = items[0]; //should only be one folder with parent set to zero (the root one).
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
                    MySqlCommand result = new MySqlCommand("SELECT * FROM inventoryfolders WHERE parentFolderID = ?uuid", database.Connection);
                    result.Parameters.Add("?uuid", parentID.ToStringHyphenated());
                    MySqlDataReader reader = result.ExecuteReader();

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
        public InventoryItemBase readInventoryItem(MySqlDataReader reader)
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
                item.inventoryNextPermissions = (uint)reader["inventoryNextPermissions"];
                item.inventoryCurrentPermissions = (uint)reader["inventoryCurrentPermissions"];
                item.invType = (int)reader["invType"];
                item.creatorsID = new LLUUID((string)reader["creatorID"]);
                item.inventoryBasePermissions = (uint)reader["inventoryBasePermissions"];
                item.inventoryEveryOnePermissions = (uint)reader["inventoryEveryOnePermissions"];
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

                    MySqlCommand result = new MySqlCommand("SELECT * FROM inventoryitems WHERE inventoryID = ?uuid", database.Connection);
                    result.Parameters.Add("?uuid", itemID.ToStringHyphenated());
                    MySqlDataReader reader = result.ExecuteReader();

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
        protected InventoryFolderBase readInventoryFolder(MySqlDataReader reader)
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
                    MySqlCommand result = new MySqlCommand("SELECT * FROM inventoryfolders WHERE folderID = ?uuid", database.Connection);
                    result.Parameters.Add("?uuid", folderID.ToStringHyphenated());
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
            string sql = "REPLACE INTO inventoryitems (inventoryID, assetID, assetType, parentFolderID, avatarID, inventoryName, inventoryDescription, inventoryNextPermissions, inventoryCurrentPermissions, invType, creatorID, inventoryBasePermissions, inventoryEveryOnePermissions) VALUES ";
            sql += "(?inventoryID, ?assetID, ?assetType, ?parentFolderID, ?avatarID, ?inventoryName, ?inventoryDescription, ?inventoryNextPermissions, ?inventoryCurrentPermissions, ?invType, ?creatorID, ?inventoryBasePermissions, ?inventoryEveryOnePermissions)";

            try
            {
                MySqlCommand result = new MySqlCommand(sql, database.Connection);
                result.Parameters.Add("?inventoryID", item.inventoryID.ToStringHyphenated());
                result.Parameters.Add("?assetID", item.assetID.ToStringHyphenated());
                result.Parameters.Add("?assetType", item.assetType.ToString());
                result.Parameters.Add("?parentFolderID", item.parentFolderID.ToStringHyphenated());
                result.Parameters.Add("?avatarID", item.avatarID.ToStringHyphenated());
                result.Parameters.Add("?inventoryName", item.inventoryName);
                result.Parameters.Add("?inventoryDescription", item.inventoryDescription);
                result.Parameters.Add("?inventoryNextPermissions", item.inventoryNextPermissions.ToString());
                result.Parameters.Add("?inventoryCurrentPermissions", item.inventoryCurrentPermissions.ToString());
                result.Parameters.Add("?invType", item.invType);
                result.Parameters.Add("?creatorID", item.creatorsID.ToStringHyphenated());
                result.Parameters.Add("?inventoryBasePermissions", item.inventoryBasePermissions);
                result.Parameters.Add("?inventoryEveryOnePermissions", item.inventoryEveryOnePermissions);
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
                MySqlCommand cmd = new MySqlCommand("DELETE FROM inventoryitems WHERE inventoryID=?uuid", database.Connection);
                cmd.Parameters.Add("?uuid", itemID.ToStringHyphenated());
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
            string sql = "REPLACE INTO inventoryfolders (folderID, agentID, parentFolderID, folderName, type, version) VALUES ";
            sql += "(?folderID, ?agentID, ?parentFolderID, ?folderName, ?type, ?version)";

            MySqlCommand cmd = new MySqlCommand(sql, database.Connection);
            cmd.Parameters.Add("?folderID", folder.folderID.ToStringHyphenated());
            cmd.Parameters.Add("?agentID", folder.agentID.ToStringHyphenated());
            cmd.Parameters.Add("?parentFolderID", folder.parentID.ToStringHyphenated());
            cmd.Parameters.Add("?folderName", folder.name);
            cmd.Parameters.Add("?type", (short)folder.type);
            cmd.Parameters.Add("?version", folder.version);
            
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
                MySqlCommand cmd = new MySqlCommand("DELETE FROM inventoryfolders WHERE folderID=?uuid", database.Connection);
                cmd.Parameters.Add("?uuid", folderID.ToStringHyphenated());
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
                MySqlCommand cmd = new MySqlCommand("DELETE FROM inventoryitems WHERE parentFolderID=?uuid", database.Connection);
                cmd.Parameters.Add("?uuid", folderID.ToStringHyphenated());
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
