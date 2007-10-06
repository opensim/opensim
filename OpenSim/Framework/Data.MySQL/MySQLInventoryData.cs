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
using System.Collections.Generic;
using System.Data;
using libsecondlife;
using OpenSim.Framework.Types;

namespace OpenSim.Framework.Data.MySQL
{
    /// <summary>
    /// A MySQL interface for the inventory server
    /// </summary>
    class MySQLInventoryData : IInventoryData
    {
        /// <summary>
        /// The database manager
        /// </summary>
        public MySQLManager database;

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
        }
        
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
            return "0.1";
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
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?uuid"] = folderID.ToStringHyphenated();

                    IDbCommand result = database.Query("SELECT * FROM inventoryitems WHERE parentFolderID = ?uuid", param);
                    IDataReader reader = result.ExecuteReader();

                    List<InventoryItemBase> items = database.readInventoryItems(reader);

                    reader.Close();
                    result.Dispose();

                    return items;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                Console.WriteLine(e.ToString());
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
                    param["?uuid"] = user.ToStringHyphenated();
                    param["?zero"] = LLUUID.Zero.ToStringHyphenated();

                    IDbCommand result = database.Query("SELECT * FROM inventoryfolders WHERE parentFolderID = ?zero AND agentID = ?uuid", param);
                    IDataReader reader = result.ExecuteReader();

                    List<InventoryFolderBase> items = database.readInventoryFolders(reader);

                    reader.Close();
                    result.Dispose();

                    return items;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                Console.WriteLine(e.ToString());
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

                    IDbCommand result = database.Query("SELECT * FROM inventoryfolders WHERE parentFolderID = ?zero AND agentID = ?uuid", param);
                    IDataReader reader = result.ExecuteReader();

                    List<InventoryFolderBase> items = database.readInventoryFolders(reader);
                    InventoryFolderBase rootFolder = items[0]; //should only be one folder with parent set to zero (the root one).
                    reader.Close();
                    result.Dispose();

                    return rootFolder;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                Console.WriteLine(e.ToString());
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
                    param["?uuid"] = parentID.ToStringHyphenated();

                    IDbCommand result = database.Query("SELECT * FROM inventoryfolders WHERE parentFolderID = ?uuid", param);
                    IDataReader reader = result.ExecuteReader();

                    List<InventoryFolderBase> items = database.readInventoryFolders(reader);

                    reader.Close();
                    result.Dispose();

                    return items;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Returns a specified inventory item
        /// </summary>
        /// <param name="item">The item to return</param>
        /// <returns>An inventory item</returns>
        public InventoryItemBase getInventoryItem(LLUUID item)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?uuid"] = item.ToStringHyphenated();

                    IDbCommand result = database.Query("SELECT * FROM inventoryitems WHERE inventoryID = ?uuid", param);
                    IDataReader reader = result.ExecuteReader();

                    List<InventoryItemBase> items = database.readInventoryItems(reader);

                    reader.Close();
                    result.Dispose();

                    if (items.Count > 0)
                    {
                        return items[0];
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Returns a specified inventory folder
        /// </summary>
        /// <param name="folder">The folder to return</param>
        /// <returns>A folder class</returns>
        public InventoryFolderBase getInventoryFolder(LLUUID folder)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?uuid"] = folder.ToStringHyphenated();

                    IDbCommand result = database.Query("SELECT * FROM inventoryfolders WHERE folderID = ?uuid", param);
                    IDataReader reader = result.ExecuteReader();

                    List<InventoryFolderBase> items = database.readInventoryFolders(reader);

                    reader.Close();
                    result.Dispose();

                    if (items.Count > 0)
                    {
                        return items[0];
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Adds a specified item to the database
        /// </summary>
        /// <param name="item">The inventory item</param>
        public void addInventoryItem(InventoryItemBase item)
        {
            lock (database)
            {
                database.insertItem(item);
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
        public void deleteInventoryItem(InventoryItemBase item)
        {

        }

        /// <summary>
        /// Creates a new inventory folder
        /// </summary>
        /// <param name="folder">Folder to create</param>
        public void addInventoryFolder(InventoryFolderBase folder)
        {
            lock (database)
            {
                database.insertFolder(folder);
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
        /// Delete an inventory folder
        /// </summary>
        /// <param name="folderId">Id of folder to delete</param>
        public void deleteInventoryFolder(LLUUID folderID)
        {
        }
    }
}
