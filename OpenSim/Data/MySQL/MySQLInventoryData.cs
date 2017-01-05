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
using System.Collections.Generic;
using System.Reflection;
using log4net;
using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A MySQL interface for the inventory server
    /// </summary>
    public class MySQLInventoryData : IInventoryDataPlugin
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_connectionString;
        private object m_dbLock = new object();

        public string Version { get { return "1.0.0.0"; } }

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
            m_connectionString = connect;

            // This actually does the roll forward assembly stuff
            Assembly assem = GetType().Assembly;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                Migration m = new Migration(dbcon, assem, "InventoryStore");
                m.Update();
            }
        }

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
        /// Returns a list of items in a specified folder
        /// </summary>
        /// <param name="folderID">The folder to search</param>
        /// <returns>A list containing inventory items</returns>
        public List<InventoryItemBase> getInventoryInFolder(UUID folderID)
        {
            try
            {
                lock (m_dbLock)
                {
                    List<InventoryItemBase> items = new List<InventoryItemBase>();

                    using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                    {
                        dbcon.Open();

                        using (MySqlCommand result = new MySqlCommand("SELECT * FROM inventoryitems WHERE parentFolderID = ?uuid", dbcon))
                        {
                            result.Parameters.AddWithValue("?uuid", folderID.ToString());

                            using (MySqlDataReader reader = result.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    // A null item (because something went wrong) breaks everything in the folder
                                    InventoryItemBase item = readInventoryItem(reader);
                                    if (item != null)
                                        items.Add(item);
                                }

                                return items;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.Message, e);
                return null;
            }
        }

        /// <summary>
        /// Returns a list of the root folders within a users inventory
        /// </summary>
        /// <param name="user">The user whose inventory is to be searched</param>
        /// <returns>A list of folder objects</returns>
        public List<InventoryFolderBase> getUserRootFolders(UUID user)
        {
            try
            {
                lock (m_dbLock)
                {
                    using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                    {
                        dbcon.Open();

                        using (MySqlCommand result = new MySqlCommand(
                            "SELECT * FROM inventoryfolders WHERE parentFolderID = ?zero AND agentID = ?uuid", dbcon))
                        {
                            result.Parameters.AddWithValue("?uuid", user.ToString());
                            result.Parameters.AddWithValue("?zero", UUID.Zero.ToString());

                            using (MySqlDataReader reader = result.ExecuteReader())
                            {
                                List<InventoryFolderBase> items = new List<InventoryFolderBase>();
                                while (reader.Read())
                                    items.Add(readInventoryFolder(reader));

                                return items;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.Message, e);
                return null;
            }
        }


        /// <summary>
        /// see <see cref="InventoryItemBase.getUserRootFolder"/>
        /// </summary>
        /// <param name="user">The user UUID</param>
        /// <returns></returns>
        public InventoryFolderBase getUserRootFolder(UUID user)
        {
            try
            {
                lock (m_dbLock)
                {
                    using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                    {
                        dbcon.Open();

                        using (MySqlCommand result = new MySqlCommand(
                            "SELECT * FROM inventoryfolders WHERE parentFolderID = ?zero AND agentID = ?uuid", dbcon))
                        {
                            result.Parameters.AddWithValue("?uuid", user.ToString());
                            result.Parameters.AddWithValue("?zero", UUID.Zero.ToString());

                            using (MySqlDataReader reader = result.ExecuteReader())
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
                                    rootFolder = items[0];

                                return rootFolder;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.Message, e);
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
        public List<InventoryFolderBase> getInventoryFolders(UUID parentID)
        {
            try
            {
                lock (m_dbLock)
                {
                    using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                    {
                        dbcon.Open();

                        using (MySqlCommand result = new MySqlCommand("SELECT * FROM inventoryfolders WHERE parentFolderID = ?uuid", dbcon))
                        {
                            result.Parameters.AddWithValue("?uuid", parentID.ToString());
                            using (MySqlDataReader reader = result.ExecuteReader())
                            {
                                List<InventoryFolderBase> items = new List<InventoryFolderBase>();

                                while (reader.Read())
                                    items.Add(readInventoryFolder(reader));

                                return items;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.Message, e);
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

                // TODO: this is to handle a case where NULLs creep in there, which we are not sure is endemic to the system, or legacy.  It would be nice to live fix these.
                // (DBGuid.FromDB() reads db NULLs as well, returns UUID.Zero)
                item.CreatorId = reader["creatorID"].ToString();

                // Be a bit safer in parsing these because the
                // database doesn't enforce them to be not null, and
                // the inventory still works if these are weird in the
                // db

                // (Empty is Ok, but "weird" will throw!)
                item.Owner = DBGuid.FromDB(reader["avatarID"]);
                item.GroupID = DBGuid.FromDB(reader["groupID"]);

                // Rest of the parsing.  If these UUID's fail, we're dead anyway
                item.ID = DBGuid.FromDB(reader["inventoryID"]);
                item.AssetID = DBGuid.FromDB(reader["assetID"]);
                item.AssetType = (int) reader["assetType"];
                item.Folder = DBGuid.FromDB(reader["parentFolderID"]);
                item.Name = (string)(reader["inventoryName"] ?? String.Empty);
                item.Description = (string)(reader["inventoryDescription"] ?? String.Empty);
                item.NextPermissions = (uint) reader["inventoryNextPermissions"];
                item.CurrentPermissions = (uint) reader["inventoryCurrentPermissions"];
                item.InvType = (int) reader["invType"];
                item.BasePermissions = (uint) reader["inventoryBasePermissions"];
                item.EveryOnePermissions = (uint) reader["inventoryEveryOnePermissions"];
                item.GroupPermissions = (uint) reader["inventoryGroupPermissions"];
                item.SalePrice = (int) reader["salePrice"];
                item.SaleType = unchecked((byte)(Convert.ToSByte(reader["saleType"])));
                item.CreationDate = (int) reader["creationDate"];
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
        public InventoryItemBase getInventoryItem(UUID itemID)
        {
            try
            {
                lock (m_dbLock)
                {
                    using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                    {
                        dbcon.Open();

                        using (MySqlCommand result = new MySqlCommand("SELECT * FROM inventoryitems WHERE inventoryID = ?uuid", dbcon))
                        {
                            result.Parameters.AddWithValue("?uuid", itemID.ToString());

                            using (MySqlDataReader reader = result.ExecuteReader())
                            {
                                InventoryItemBase item = null;
                                if (reader.Read())
                                    item = readInventoryItem(reader);

                                return item;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.Message, e);
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
                folder.Owner = DBGuid.FromDB(reader["agentID"]);
                folder.ParentID = DBGuid.FromDB(reader["parentFolderID"]);
                folder.ID = DBGuid.FromDB(reader["folderID"]);
                folder.Name = (string) reader["folderName"];
                folder.Type = (short) reader["type"];
                folder.Version = (ushort) ((int) reader["version"]);
                return folder;
            }
            catch (Exception e)
            {
                m_log.Error(e.Message, e);
            }

            return null;
        }


        /// <summary>
        /// Returns a specified inventory folder
        /// </summary>
        /// <param name="folderID">The folder to return</param>
        /// <returns>A folder class</returns>
        public InventoryFolderBase getInventoryFolder(UUID folderID)
        {
            try
            {
                lock (m_dbLock)
                {
                    using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                    {
                        dbcon.Open();

                        using (MySqlCommand result = new MySqlCommand("SELECT * FROM inventoryfolders WHERE folderID = ?uuid", dbcon))
                        {
                            result.Parameters.AddWithValue("?uuid", folderID.ToString());

                            using (MySqlDataReader reader = result.ExecuteReader())
                            {
                                InventoryFolderBase folder = null;
                                if (reader.Read())
                                    folder = readInventoryFolder(reader);

                                return folder;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.Message, e);
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
                    + ", creatorID, inventoryBasePermissions, inventoryEveryOnePermissions, inventoryGroupPermissions, salePrice, saleType"
                    + ", creationDate, groupID, groupOwned, flags) VALUES ";
            sql +=
                "(?inventoryID, ?assetID, ?assetType, ?parentFolderID, ?avatarID, ?inventoryName, ?inventoryDescription"
                    + ", ?inventoryNextPermissions, ?inventoryCurrentPermissions, ?invType, ?creatorID"
                    + ", ?inventoryBasePermissions, ?inventoryEveryOnePermissions, ?inventoryGroupPermissions, ?salePrice, ?saleType, ?creationDate"
                    + ", ?groupID, ?groupOwned, ?flags)";

            string itemName = item.Name;
            if (item.Name.Length > 64)
            {
                itemName = item.Name.Substring(0, 64);
                m_log.Warn("[INVENTORY DB]: Name field truncated from " + item.Name.Length + " to " + itemName.Length + " characters on add item");
            }

            string itemDesc = item.Description;
            if (item.Description.Length > 128)
            {
                itemDesc = item.Description.Substring(0, 128);
                m_log.Warn("[INVENTORY DB]: Description field truncated from " + item.Description.Length + " to " + itemDesc.Length + " characters on add item");
            }

            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    using (MySqlCommand result = new MySqlCommand(sql, dbcon))
                    {
                        result.Parameters.AddWithValue("?inventoryID", item.ID.ToString());
                        result.Parameters.AddWithValue("?assetID", item.AssetID.ToString());
                        result.Parameters.AddWithValue("?assetType", item.AssetType.ToString());
                        result.Parameters.AddWithValue("?parentFolderID", item.Folder.ToString());
                        result.Parameters.AddWithValue("?avatarID", item.Owner.ToString());
                        result.Parameters.AddWithValue("?inventoryName", itemName);
                        result.Parameters.AddWithValue("?inventoryDescription", itemDesc);
                        result.Parameters.AddWithValue("?inventoryNextPermissions", item.NextPermissions.ToString());
                        result.Parameters.AddWithValue("?inventoryCurrentPermissions",
                                                       item.CurrentPermissions.ToString());
                        result.Parameters.AddWithValue("?invType", item.InvType);
                        result.Parameters.AddWithValue("?creatorID", item.CreatorId);
                        result.Parameters.AddWithValue("?inventoryBasePermissions", item.BasePermissions);
                        result.Parameters.AddWithValue("?inventoryEveryOnePermissions", item.EveryOnePermissions);
                        result.Parameters.AddWithValue("?inventoryGroupPermissions", item.GroupPermissions);
                        result.Parameters.AddWithValue("?salePrice", item.SalePrice);
                        result.Parameters.AddWithValue("?saleType", unchecked((sbyte)item.SaleType));
                        result.Parameters.AddWithValue("?creationDate", item.CreationDate);
                        result.Parameters.AddWithValue("?groupID", item.GroupID);
                        result.Parameters.AddWithValue("?groupOwned", item.GroupOwned);
                        result.Parameters.AddWithValue("?flags", item.Flags);

                        lock (m_dbLock)
                            result.ExecuteNonQuery();

                        result.Dispose();
                    }

                    using (MySqlCommand result = new MySqlCommand("update inventoryfolders set version=version+1 where folderID = ?folderID", dbcon))
                    {
                        result.Parameters.AddWithValue("?folderID", item.Folder.ToString());

                        lock (m_dbLock)
                            result.ExecuteNonQuery();
                    }
                }
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
        public void deleteInventoryItem(UUID itemID)
        {
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    using (MySqlCommand cmd = new MySqlCommand("DELETE FROM inventoryitems WHERE inventoryID=?uuid", dbcon))
                    {
                        cmd.Parameters.AddWithValue("?uuid", itemID.ToString());

                        lock (m_dbLock)
                            cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (MySqlException e)
            {
                m_log.Error(e.Message, e);
            }
        }

        public InventoryItemBase queryInventoryItem(UUID itemID)
        {
            return getInventoryItem(itemID);
        }

        public InventoryFolderBase queryInventoryFolder(UUID folderID)
        {
            return getInventoryFolder(folderID);
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

            string folderName = folder.Name;
            if (folderName.Length > 64)
            {
                folderName = folderName.Substring(0, 64);
                m_log.Warn("[INVENTORY DB]: Name field truncated from " + folder.Name.Length + " to " + folderName.Length + " characters on add folder");
            }

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand(sql, dbcon))
                {
                    cmd.Parameters.AddWithValue("?folderID", folder.ID.ToString());
                    cmd.Parameters.AddWithValue("?agentID", folder.Owner.ToString());
                    cmd.Parameters.AddWithValue("?parentFolderID", folder.ParentID.ToString());
                    cmd.Parameters.AddWithValue("?folderName", folderName);
                    cmd.Parameters.AddWithValue("?type", folder.Type);
                    cmd.Parameters.AddWithValue("?version", folder.Version);

                    try
                    {
                        lock (m_dbLock)
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Error(e.ToString());
                    }
                }
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

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand(sql, dbcon))
                {
                    cmd.Parameters.AddWithValue("?folderID", folder.ID.ToString());
                    cmd.Parameters.AddWithValue("?parentFolderID", folder.ParentID.ToString());

                    try
                    {
                        lock (m_dbLock)
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Error(e.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Append a list of all the child folders of a parent folder
        /// </summary>
        /// <param name="folders">list where folders will be appended</param>
        /// <param name="parentID">ID of parent</param>
        protected void getInventoryFolders(ref List<InventoryFolderBase> folders, UUID parentID)
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
        public List<InventoryFolderBase> getFolderHierarchy(UUID parentID)
        {
            /* Note: There are subtle changes between this implementation of getFolderHierarchy and the previous one
                 * - We will only need to hit the database twice instead of n times.
                 * - We assume the database is well-formed - no stranded/dangling folders, all folders in heirarchy owned
                 *   by the same person, each user only has 1 inventory heirarchy
                 * - The returned list is not ordered, instead of breadth-first ordered
               There are basically 2 usage cases for getFolderHeirarchy:
                 1) Getting the user's entire inventory heirarchy when they log in
                 2) Finding a subfolder heirarchy to delete when emptying the trash.
               This implementation will pull all inventory folders from the database, and then prune away any folder that
               is not part of the requested sub-heirarchy. The theory is that it is cheaper to make 1 request from the
               database than to make n requests. This pays off only if requested heirarchy is large.
               By making this choice, we are making the worst case better at the cost of making the best case worse.
               This way is generally better because we don't have to rebuild the connection/sql query per subfolder,
               even if we end up getting more data from the SQL server than we need.
                 - Francis
             */
            try
            {
                List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
                Dictionary<UUID, List<InventoryFolderBase>> hashtable = new Dictionary<UUID, List<InventoryFolderBase>>(); ;
                List<InventoryFolderBase> parentFolder = new List<InventoryFolderBase>();
                bool buildResultsFromHashTable = false;

                lock (m_dbLock)
                {
                    using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                    {
                        dbcon.Open();

                        /* Fetch the parent folder from the database to determine the agent ID, and if
                         * we're querying the root of the inventory folder tree */
                        using (MySqlCommand result = new MySqlCommand("SELECT * FROM inventoryfolders WHERE folderID = ?uuid", dbcon))
                        {
                            result.Parameters.AddWithValue("?uuid", parentID.ToString());

                            using (MySqlDataReader reader = result.ExecuteReader())
                            {
                                // Should be at most 1 result
                                while (reader.Read())
                                    parentFolder.Add(readInventoryFolder(reader));
                            }
                        }

                        if (parentFolder.Count >= 1)   // No result means parent folder does not exist
                        {
                            if (parentFolder[0].ParentID == UUID.Zero) // We are querying the root folder
                            {
                                /* Get all of the agent's folders from the database, put them in a list and return it */
                                using (MySqlCommand result = new MySqlCommand("SELECT * FROM inventoryfolders WHERE agentID = ?uuid", dbcon))
                                {
                                    result.Parameters.AddWithValue("?uuid", parentFolder[0].Owner.ToString());

                                    using (MySqlDataReader reader = result.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            InventoryFolderBase curFolder = readInventoryFolder(reader);
                                            if (curFolder.ID != parentID) // Do not need to add the root node of the tree to the list
                                                folders.Add(curFolder);
                                        }
                                    }
                                }
                            } // if we are querying the root folder
                            else // else we are querying a subtree of the inventory folder tree
                            {
                                /* Get all of the agent's folders from the database, put them all in a hash table
                                 * indexed by their parent ID */
                                using (MySqlCommand result = new MySqlCommand("SELECT * FROM inventoryfolders WHERE agentID = ?uuid", dbcon))
                                {
                                    result.Parameters.AddWithValue("?uuid", parentFolder[0].Owner.ToString());

                                    using (MySqlDataReader reader = result.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            InventoryFolderBase curFolder = readInventoryFolder(reader);
                                            if (hashtable.ContainsKey(curFolder.ParentID))      // Current folder already has a sibling
                                                hashtable[curFolder.ParentID].Add(curFolder);   // append to sibling list
                                            else // else current folder has no known (yet) siblings
                                            {
                                                List<InventoryFolderBase> siblingList = new List<InventoryFolderBase>();
                                                siblingList.Add(curFolder);
                                                // Current folder has no known (yet) siblings
                                                hashtable.Add(curFolder.ParentID, siblingList);
                                            }
                                        } // while more items to read from the database
                                    }
                                }

                                // Set flag so we know we need to build the results from the hash table after
                                // we unlock the database
                                buildResultsFromHashTable = true;

                            } // else we are querying a subtree of the inventory folder tree
                        } // if folder parentID exists

                        if (buildResultsFromHashTable)
                        {
                            /* We have all of the user's folders stored in a hash table indexed by their parent ID
                             * and we need to return the requested subtree. We will build the requested subtree
                             * by performing a breadth-first-search on the hash table */
                            if (hashtable.ContainsKey(parentID))
                                folders.AddRange(hashtable[parentID]);
                            for (int i = 0; i < folders.Count; i++) // **Note: folders.Count is *not* static
                                if (hashtable.ContainsKey(folders[i].ID))
                                    folders.AddRange(hashtable[folders[i].ID]);
                        }
                    }
                } // lock (database)

                return folders;
            }
            catch (Exception e)
            {
                m_log.Error(e.Message, e);
                return null;
            }
        }

        /// <summary>
        /// Delete a folder from database
        /// </summary>
        /// <param name="folderID">the folder UUID</param>
        protected void deleteOneFolder(UUID folderID)
        {
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    // System folders can never be deleted. Period.
                    using (MySqlCommand cmd = new MySqlCommand("DELETE FROM inventoryfolders WHERE folderID=?uuid and type=-1", dbcon))
                    {
                        cmd.Parameters.AddWithValue("?uuid", folderID.ToString());

                        lock (m_dbLock)
                            cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (MySqlException e)
            {
                m_log.Error(e.Message, e);
            }
        }

        /// <summary>
        /// Delete all item in a folder
        /// </summary>
        /// <param name="folderID">the folder UUID</param>
        protected void deleteItemsInFolder(UUID folderID)
        {
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    using (MySqlCommand cmd = new MySqlCommand("DELETE FROM inventoryitems WHERE parentFolderID=?uuid", dbcon))
                    {
                        cmd.Parameters.AddWithValue("?uuid", folderID.ToString());

                        lock (m_dbLock)
                            cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (MySqlException e)
            {
                m_log.Error(e.ToString());
            }
        }

        /// <summary>
        /// Deletes an inventory folder
        /// </summary>
        /// <param name="folderId">Id of folder to delete</param>
        public void deleteInventoryFolder(UUID folderID)
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

        public List<InventoryItemBase> fetchActiveGestures(UUID avatarID)
        {
            lock (m_dbLock)
            {
                try
                {
                    using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                    {
                        dbcon.Open();

                        using (MySqlCommand sqlCmd = new MySqlCommand(
                            "SELECT * FROM inventoryitems WHERE avatarId = ?uuid AND assetType = ?type and flags & 1", dbcon))
                        {
                            sqlCmd.Parameters.AddWithValue("?uuid", avatarID.ToString());
                            sqlCmd.Parameters.AddWithValue("?type", (int)AssetType.Gesture);

                            using (MySqlDataReader result = sqlCmd.ExecuteReader())
                            {
                                List<InventoryItemBase> list = new List<InventoryItemBase>();
                                while (result.Read())
                                {
                                    InventoryItemBase item = readInventoryItem(result);
                                    if (item != null)
                                        list.Add(item);
                                }
                                return list;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Error(e.Message, e);
                    return null;
                }
            }
        }
    }
}
