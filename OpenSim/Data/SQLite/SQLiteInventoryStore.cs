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
using System.Data;
using System.Reflection;
using log4net;
#if CSharpSqlite
    using Community.CsharpSqlite.Sqlite;
#else
    using Mono.Data.Sqlite;
#endif
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.SQLite
{
    /// <summary>
    /// An Inventory Interface to the SQLite database
    /// </summary>
    public class SQLiteInventoryStore : SQLiteUtil, IInventoryDataPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const string invItemsSelect = "select * from inventoryitems";
        private const string invFoldersSelect = "select * from inventoryfolders";

        private static SqliteConnection conn;
        private static DataSet ds;
        private static SqliteDataAdapter invItemsDa;
        private static SqliteDataAdapter invFoldersDa;

        private static bool m_Initialized = false;

        public void Initialise()
        {
            m_log.Info("[SQLiteInventoryData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException(Name);
        }

        /// <summary>
        /// <list type="bullet">
        /// <item>Initialises Inventory interface</item>
        /// <item>Loads and initialises a new SQLite connection and maintains it.</item>
        /// <item>use default URI if connect string string is empty.</item>
        /// </list>
        /// </summary>
        /// <param name="dbconnect">connect string</param>
        public void Initialise(string dbconnect)
        {
            if (!m_Initialized)
            {
                m_Initialized = true;

                if (Util.IsWindows())
                    Util.LoadArchSpecificWindowsDll("sqlite3.dll");

                if (dbconnect == string.Empty)
                {
                    dbconnect = "URI=file:inventoryStore.db,version=3";
                }
                m_log.Info("[INVENTORY DB]: Sqlite - connecting: " + dbconnect);
                conn = new SqliteConnection(dbconnect);

                conn.Open();

                Assembly assem = GetType().Assembly;
                Migration m = new Migration(conn, assem, "InventoryStore");
                m.Update();

                SqliteCommand itemsSelectCmd = new SqliteCommand(invItemsSelect, conn);
                invItemsDa = new SqliteDataAdapter(itemsSelectCmd);
                //            SqliteCommandBuilder primCb = new SqliteCommandBuilder(primDa);

                SqliteCommand foldersSelectCmd = new SqliteCommand(invFoldersSelect, conn);
                invFoldersDa = new SqliteDataAdapter(foldersSelectCmd);

                ds = new DataSet();

                ds.Tables.Add(createInventoryFoldersTable());
                invFoldersDa.Fill(ds.Tables["inventoryfolders"]);
                setupFoldersCommands(invFoldersDa, conn);
                CreateDataSetMapping(invFoldersDa, "inventoryfolders");
                m_log.Info("[INVENTORY DB]: Populated Inventory Folders Definitions");

                ds.Tables.Add(createInventoryItemsTable());
                invItemsDa.Fill(ds.Tables["inventoryitems"]);
                setupItemsCommands(invItemsDa, conn);
                CreateDataSetMapping(invItemsDa, "inventoryitems");
                m_log.Info("[INVENTORY DB]: Populated Inventory Items Definitions");

                ds.AcceptChanges();
            }
        }

        /// <summary>
        /// Closes the inventory interface
        /// </summary>
        public void Dispose()
        {
            if (conn != null)
            {
                conn.Close();
                conn = null;
            }
            if (invItemsDa != null)
            {
                invItemsDa.Dispose();
                invItemsDa = null;
            }
            if (invFoldersDa != null)
            {
                invFoldersDa.Dispose();
                invFoldersDa = null;
            }
            if (ds != null)
            {
                ds.Dispose();
                ds = null;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public InventoryItemBase buildItem(DataRow row)
        {
            InventoryItemBase item = new InventoryItemBase();
            item.ID = new UUID((string) row["UUID"]);
            item.AssetID = new UUID((string) row["assetID"]);
            item.AssetType = Convert.ToInt32(row["assetType"]);
            item.InvType = Convert.ToInt32(row["invType"]);
            item.Folder = new UUID((string) row["parentFolderID"]);
            item.Owner = new UUID((string) row["avatarID"]);
            item.CreatorIdentification = (string)row["creatorsID"];
            item.Name = (string) row["inventoryName"];
            item.Description = (string) row["inventoryDescription"];

            item.NextPermissions = Convert.ToUInt32(row["inventoryNextPermissions"]);
            item.CurrentPermissions = Convert.ToUInt32(row["inventoryCurrentPermissions"]);
            item.BasePermissions = Convert.ToUInt32(row["inventoryBasePermissions"]);
            item.EveryOnePermissions = Convert.ToUInt32(row["inventoryEveryOnePermissions"]);
            item.GroupPermissions = Convert.ToUInt32(row["inventoryGroupPermissions"]);

            // new fields
            if (!Convert.IsDBNull(row["salePrice"]))
                item.SalePrice = Convert.ToInt32(row["salePrice"]);

            if (!Convert.IsDBNull(row["saleType"]))
                item.SaleType = Convert.ToByte(row["saleType"]);

            if (!Convert.IsDBNull(row["creationDate"]))
                item.CreationDate = Convert.ToInt32(row["creationDate"]);

            if (!Convert.IsDBNull(row["groupID"]))
                item.GroupID = new UUID((string)row["groupID"]);

            if (!Convert.IsDBNull(row["groupOwned"]))
                item.GroupOwned = Convert.ToBoolean(row["groupOwned"]);

            if (!Convert.IsDBNull(row["Flags"]))
                item.Flags = Convert.ToUInt32(row["Flags"]);

            return item;
        }

        /// <summary>
        /// Fill a database row with item data
        /// </summary>
        /// <param name="row"></param>
        /// <param name="item"></param>
        private static void fillItemRow(DataRow row, InventoryItemBase item)
        {
            row["UUID"] = item.ID.ToString();
            row["assetID"] = item.AssetID.ToString();
            row["assetType"] = item.AssetType;
            row["invType"] = item.InvType;
            row["parentFolderID"] = item.Folder.ToString();
            row["avatarID"] = item.Owner.ToString();
            row["creatorsID"] = item.CreatorIdentification.ToString();
            row["inventoryName"] = item.Name;
            row["inventoryDescription"] = item.Description;

            row["inventoryNextPermissions"] = item.NextPermissions;
            row["inventoryCurrentPermissions"] = item.CurrentPermissions;
            row["inventoryBasePermissions"] = item.BasePermissions;
            row["inventoryEveryOnePermissions"] = item.EveryOnePermissions;
            row["inventoryGroupPermissions"] = item.GroupPermissions;

            // new fields
            row["salePrice"] = item.SalePrice;
            row["saleType"] = item.SaleType;
            row["creationDate"] = item.CreationDate;
            row["groupID"] = item.GroupID.ToString();
            row["groupOwned"] = item.GroupOwned;
            row["flags"] = item.Flags;
        }

        /// <summary>
        /// Add inventory folder
        /// </summary>
        /// <param name="folder">Folder base</param>
        /// <param name="add">true=create folder. false=update existing folder</param>
        /// <remarks>nasty</remarks>
        private void addFolder(InventoryFolderBase folder, bool add)
        {
            lock (ds)
            {
                DataTable inventoryFolderTable = ds.Tables["inventoryfolders"];

                DataRow inventoryRow = inventoryFolderTable.Rows.Find(folder.ID.ToString());
                if (inventoryRow == null)
                {
                    if (! add)
                        m_log.ErrorFormat("Interface Misuse: Attempting to Update non-existent inventory folder: {0}", folder.ID);

                    inventoryRow = inventoryFolderTable.NewRow();
                    fillFolderRow(inventoryRow, folder);
                    inventoryFolderTable.Rows.Add(inventoryRow);
                }
                else
                {
                    if (add)
                        m_log.ErrorFormat("Interface Misuse: Attempting to Add inventory folder that already exists: {0}", folder.ID);

                    fillFolderRow(inventoryRow, folder);
                }

                invFoldersDa.Update(ds, "inventoryfolders");
            }
        }

        /// <summary>
        /// Move an inventory folder
        /// </summary>
        /// <param name="folder">folder base</param>
        private void moveFolder(InventoryFolderBase folder)
        {
            lock (ds)
            {
                DataTable inventoryFolderTable = ds.Tables["inventoryfolders"];

                DataRow inventoryRow = inventoryFolderTable.Rows.Find(folder.ID.ToString());
                if (inventoryRow == null)
                {
                    inventoryRow = inventoryFolderTable.NewRow();
                    fillFolderRow(inventoryRow, folder);
                    inventoryFolderTable.Rows.Add(inventoryRow);
                }
                else
                {
                    moveFolderRow(inventoryRow, folder);
                }

                invFoldersDa.Update(ds, "inventoryfolders");
            }
        }

        /// <summary>
        /// add an item in inventory
        /// </summary>
        /// <param name="item">the item</param>
        /// <param name="add">true=add item ; false=update existing item</param>
        private void addItem(InventoryItemBase item, bool add)
        {
            lock (ds)
            {
                DataTable inventoryItemTable = ds.Tables["inventoryitems"];

                DataRow inventoryRow = inventoryItemTable.Rows.Find(item.ID.ToString());
                if (inventoryRow == null)
                {
                    if (!add)
                        m_log.ErrorFormat("[INVENTORY DB]: Interface Misuse: Attempting to Update non-existent inventory item: {0}", item.ID);

                    inventoryRow = inventoryItemTable.NewRow();
                    fillItemRow(inventoryRow, item);
                    inventoryItemTable.Rows.Add(inventoryRow);
                }
                else
                {
                    if (add)
                        m_log.ErrorFormat("[INVENTORY DB]: Interface Misuse: Attempting to Add inventory item that already exists: {0}", item.ID);

                    fillItemRow(inventoryRow, item);
                }

                invItemsDa.Update(ds, "inventoryitems");

                DataTable inventoryFolderTable = ds.Tables["inventoryfolders"];

                inventoryRow = inventoryFolderTable.Rows.Find(item.Folder.ToString());
                if (inventoryRow != null) //MySQL doesn't throw an exception here, so sqlite shouldn't either.
                    inventoryRow["version"] = (int)inventoryRow["version"] + 1;

                invFoldersDa.Update(ds, "inventoryfolders");
            }
        }

        /// <summary>
        /// TODO : DataSet commit
        /// </summary>
        public void Shutdown()
        {
            // TODO: DataSet commit
        }

        /// <summary>
        /// The name of this DB provider
        /// </summary>
        /// <returns>Name of DB provider</returns>
        public string Name
        {
            get { return "SQLite Inventory Data Interface"; }
        }

        /// <summary>
        /// Returns the version of this DB provider
        /// </summary>
        /// <returns>A string containing the DB provider version</returns>
        public string Version
        {
            get
            {
                Module module = GetType().Module;
                // string dllName = module.Assembly.ManifestModule.Name;
                Version dllVersion = module.Assembly.GetName().Version;


                return
                    string.Format("{0}.{1}.{2}.{3}", dllVersion.Major, dllVersion.Minor, dllVersion.Build,
                            dllVersion.Revision);
            }
        }

        /// <summary>
        /// Returns a list of inventory items contained within the specified folder
        /// </summary>
        /// <param name="folderID">The UUID of the target folder</param>
        /// <returns>A List of InventoryItemBase items</returns>
        public List<InventoryItemBase> getInventoryInFolder(UUID folderID)
        {
            lock (ds)
            {
                List<InventoryItemBase> retval = new List<InventoryItemBase>();
                DataTable inventoryItemTable = ds.Tables["inventoryitems"];
                string selectExp = "parentFolderID = '" + folderID + "'";
                DataRow[] rows = inventoryItemTable.Select(selectExp);
                foreach (DataRow row in rows)
                {
                    retval.Add(buildItem(row));
                }

                return retval;
            }
        }

        /// <summary>
        /// Returns a list of the root folders within a users inventory
        /// </summary>
        /// <param name="user">The user whos inventory is to be searched</param>
        /// <returns>A list of folder objects</returns>
        public List<InventoryFolderBase> getUserRootFolders(UUID user)
        {
            return new List<InventoryFolderBase>();
        }

        // see InventoryItemBase.getUserRootFolder
        public InventoryFolderBase getUserRootFolder(UUID user)
        {
            lock (ds)
            {
                List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
                DataTable inventoryFolderTable = ds.Tables["inventoryfolders"];
                string selectExp = "agentID = '" + user + "' AND parentID = '" + UUID.Zero + "'";
                DataRow[] rows = inventoryFolderTable.Select(selectExp);
                foreach (DataRow row in rows)
                {
                    folders.Add(buildFolder(row));
                }

                // There should only ever be one root folder for a user.  However, if there's more
                // than one we'll simply use the first one rather than failing.  It would be even
                // nicer to print some message to this effect, but this feels like it's too low a
                // to put such a message out, and it's too minor right now to spare the time to
                // suitably refactor.
                if (folders.Count > 0)
                {
                    return folders[0];
                }

                return null;
            }
        }

        /// <summary>
        /// Append a list of all the child folders of a parent folder
        /// </summary>
        /// <param name="folders">list where folders will be appended</param>
        /// <param name="parentID">ID of parent</param>
        protected void getInventoryFolders(ref List<InventoryFolderBase> folders, UUID parentID)
        {
            lock (ds)
            {
                DataTable inventoryFolderTable = ds.Tables["inventoryfolders"];
                string selectExp = "parentID = '" + parentID + "'";
                DataRow[] rows = inventoryFolderTable.Select(selectExp);
                foreach (DataRow row in rows)
                {
                    folders.Add(buildFolder(row));
                }

            }
        }

        /// <summary>
        /// Returns a list of inventory folders contained in the folder 'parentID'
        /// </summary>
        /// <param name="parentID">The folder to get subfolders for</param>
        /// <returns>A list of inventory folders</returns>
        public List<InventoryFolderBase> getInventoryFolders(UUID parentID)
        {
            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            getInventoryFolders(ref folders, parentID);
            return folders;
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
               By making this choice, we are making the worst case better at the cost of making the best case worse
                 - Francis
             */

            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            DataRow[] folderRows = null, parentRow;
            InventoryFolderBase parentFolder = null;
            lock (ds)
            {
                /* Fetch the parent folder from the database to determine the agent ID.
                 * Then fetch all inventory folders for that agent from the agent ID.
                 */
                DataTable inventoryFolderTable = ds.Tables["inventoryfolders"];
                string selectExp = "UUID = '" + parentID + "'";
                parentRow = inventoryFolderTable.Select(selectExp); // Assume at most 1 result
                if (parentRow.GetLength(0) >= 1)                    // No result means parent folder does not exist
                {
                    parentFolder = buildFolder(parentRow[0]);
                    UUID agentID = parentFolder.Owner;
                    selectExp = "agentID = '" + agentID + "'";
                    folderRows = inventoryFolderTable.Select(selectExp);
                }

                if (folderRows != null && folderRows.GetLength(0) >= 1)   // No result means parent folder does not exist
                {                                                       // or has no children
                    /* if we're querying the root folder, just return an unordered list of all folders in the user's
                     * inventory
                     */
                    if (parentFolder.ParentID == UUID.Zero)
                    {
                        foreach (DataRow row in folderRows)
                        {
                            InventoryFolderBase curFolder = buildFolder(row);
                            if (curFolder.ID != parentID)   // Return all folders except the parent folder of heirarchy
                                folders.Add(buildFolder(row));
                        }
                    } // If requesting root folder
                    /* else we are querying a non-root folder. We currently have a list of all of the user's folders,
                     * we must construct a list of all folders in the heirarchy below parentID.
                     * Our first step will be to construct a hash table of all folders, indexed by parent ID.
                     * Once we have constructed the hash table, we will do a breadth-first traversal on the tree using the
                     * hash table to find child folders.
                     */
                    else
                    {                                                         // Querying a non-root folder

                        // Build a hash table of all user's inventory folders, indexed by each folder's parent ID
                        Dictionary<UUID, List<InventoryFolderBase>> hashtable =
                            new Dictionary<UUID, List<InventoryFolderBase>>(folderRows.GetLength(0));

                        foreach (DataRow row in folderRows)
                        {
                            InventoryFolderBase curFolder = buildFolder(row);
                            if (curFolder.ParentID != UUID.Zero) // Discard root of tree - not needed
                            {
                                if (hashtable.ContainsKey(curFolder.ParentID))
                                {
                                    // Current folder already has a sibling - append to sibling list
                                    hashtable[curFolder.ParentID].Add(curFolder);
                                }
                                else
                                {
                                    List<InventoryFolderBase> siblingList = new List<InventoryFolderBase>();
                                    siblingList.Add(curFolder);
                                    // Current folder has no known (yet) siblings
                                    hashtable.Add(curFolder.ParentID, siblingList);
                                }
                            }
                        } // For all inventory folders

                        // Note: Could release the ds lock here - we don't access folderRows or the database anymore.
                        // This is somewhat of a moot point as the callers of this function usually lock db anyways.

                        if (hashtable.ContainsKey(parentID)) // if requested folder does have children
                            folders.AddRange(hashtable[parentID]);

                        // BreadthFirstSearch build inventory tree **Note: folders.Count is *not* static
                        for (int i = 0; i < folders.Count; i++)
                            if (hashtable.ContainsKey(folders[i].ID))
                                folders.AddRange(hashtable[folders[i].ID]);

                    } // if requesting a subfolder heirarchy
                } // if folder parentID exists and has children
            } // lock ds
            return folders;
        }

        /// <summary>
        /// Returns an inventory item by its UUID
        /// </summary>
        /// <param name="item">The UUID of the item to be returned</param>
        /// <returns>A class containing item information</returns>
        public InventoryItemBase getInventoryItem(UUID item)
        {
            lock (ds)
            {
                DataRow row = ds.Tables["inventoryitems"].Rows.Find(item.ToString());
                if (row != null)
                {
                    return buildItem(row);
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Returns a specified inventory folder by its UUID
        /// </summary>
        /// <param name="folder">The UUID of the folder to be returned</param>
        /// <returns>A class containing folder information</returns>
        public InventoryFolderBase getInventoryFolder(UUID folder)
        {
            // TODO: Deep voodoo here.  If you enable this code then
            // multi region breaks.  No idea why, but I figured it was
            // better to leave multi region at this point.  It does mean
            // that you don't get to see system textures why creating
            // clothes and the like. :(
            lock (ds)
            {
                DataRow row = ds.Tables["inventoryfolders"].Rows.Find(folder.ToString());
                if (row != null)
                {
                    return buildFolder(row);
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Creates a new inventory item based on item
        /// </summary>
        /// <param name="item">The item to be created</param>
        public void addInventoryItem(InventoryItemBase item)
        {
            addItem(item, true);
        }

        /// <summary>
        /// Updates an inventory item with item (updates based on ID)
        /// </summary>
        /// <param name="item">The updated item</param>
        public void updateInventoryItem(InventoryItemBase item)
        {
            addItem(item, false);
        }

        /// <summary>
        /// Delete an inventory item
        /// </summary>
        /// <param name="item">The item UUID</param>
        public void deleteInventoryItem(UUID itemID)
        {
            lock (ds)
            {
                DataTable inventoryItemTable = ds.Tables["inventoryitems"];

                DataRow inventoryRow = inventoryItemTable.Rows.Find(itemID.ToString());
                if (inventoryRow != null)
                {
                    inventoryRow.Delete();
                }

                invItemsDa.Update(ds, "inventoryitems");
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
        /// Delete all items in the specified folder
        /// </summary>
        /// <param name="folderId">id of the folder, whose item content should be deleted</param>
        /// <todo>this is horribly inefficient, but I don't want to ruin the overall structure of this implementation</todo>
        private void deleteItemsInFolder(UUID folderId)
        {
            List<InventoryItemBase> items = getInventoryInFolder(folderId);

            foreach (InventoryItemBase i in items)
                deleteInventoryItem(i.ID);
        }

        /// <summary>
        /// Adds a new folder specified by folder
        /// </summary>
        /// <param name="folder">The inventory folder</param>
        public void addInventoryFolder(InventoryFolderBase folder)
        {
            addFolder(folder, true);
        }

        /// <summary>
        /// Updates a folder based on its ID with folder
        /// </summary>
        /// <param name="folder">The inventory folder</param>
        public void updateInventoryFolder(InventoryFolderBase folder)
        {
            addFolder(folder, false);
        }

        /// <summary>
        /// Moves a folder based on its ID with folder
        /// </summary>
        /// <param name="folder">The inventory folder</param>
        public void moveInventoryFolder(InventoryFolderBase folder)
        {
            moveFolder(folder);
        }

        /// <summary>
        /// Delete a folder
        /// </summary>
        /// <remarks>
        /// This will clean-up any child folders and child items as well
        /// </remarks>
        /// <param name="folderID">the folder UUID</param>
        public void deleteInventoryFolder(UUID folderID)
        {
            lock (ds)
            {
                List<InventoryFolderBase> subFolders = getFolderHierarchy(folderID);

                DataTable inventoryFolderTable = ds.Tables["inventoryfolders"];
                DataRow inventoryRow;

                //Delete all sub-folders
                foreach (InventoryFolderBase f in subFolders)
                {
                    inventoryRow = inventoryFolderTable.Rows.Find(f.ID.ToString());
                    if (inventoryRow != null)
                    {
                        deleteItemsInFolder(f.ID);
                        inventoryRow.Delete();
                    }
                }

                //Delete the actual row
                inventoryRow = inventoryFolderTable.Rows.Find(folderID.ToString());
                if (inventoryRow != null)
                {
                    deleteItemsInFolder(folderID);
                    inventoryRow.Delete();
                }

                invFoldersDa.Update(ds, "inventoryfolders");
            }
        }

        /***********************************************************************
         *
         *  Data Table definitions
         *
         **********************************************************************/

        protected void CreateDataSetMapping(IDataAdapter da, string tableName)
        {
            ITableMapping dbMapping = da.TableMappings.Add(tableName, tableName);
            foreach (DataColumn col in ds.Tables[tableName].Columns)
            {
                dbMapping.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            }
        }

        /// <summary>
        /// Create the "inventoryitems" table
        /// </summary>
        private static DataTable createInventoryItemsTable()
        {
            DataTable inv = new DataTable("inventoryitems");

            createCol(inv, "UUID", typeof (String)); //inventoryID
            createCol(inv, "assetID", typeof (String));
            createCol(inv, "assetType", typeof (Int32));
            createCol(inv, "invType", typeof (Int32));
            createCol(inv, "parentFolderID", typeof (String));
            createCol(inv, "avatarID", typeof (String));
            createCol(inv, "creatorsID", typeof (String));

            createCol(inv, "inventoryName", typeof (String));
            createCol(inv, "inventoryDescription", typeof (String));
            // permissions
            createCol(inv, "inventoryNextPermissions", typeof (Int32));
            createCol(inv, "inventoryCurrentPermissions", typeof (Int32));
            createCol(inv, "inventoryBasePermissions", typeof (Int32));
            createCol(inv, "inventoryEveryOnePermissions", typeof (Int32));
            createCol(inv, "inventoryGroupPermissions", typeof (Int32));

            // sale info
            createCol(inv, "salePrice", typeof(Int32));
            createCol(inv, "saleType", typeof(Byte));

            // creation date
            createCol(inv, "creationDate", typeof(Int32));

            // group info
            createCol(inv, "groupID", typeof(String));
            createCol(inv, "groupOwned", typeof(Boolean));

            // Flags
            createCol(inv, "flags", typeof(UInt32));

            inv.PrimaryKey = new DataColumn[] { inv.Columns["UUID"] };
            return inv;
        }

        /// <summary>
        /// Creates the "inventoryfolders" table
        /// </summary>
        /// <returns></returns>
        private static DataTable createInventoryFoldersTable()
        {
            DataTable fol = new DataTable("inventoryfolders");

            createCol(fol, "UUID", typeof (String)); //folderID
            createCol(fol, "name", typeof (String));
            createCol(fol, "agentID", typeof (String));
            createCol(fol, "parentID", typeof (String));
            createCol(fol, "type", typeof (Int32));
            createCol(fol, "version", typeof (Int32));

            fol.PrimaryKey = new DataColumn[] {fol.Columns["UUID"]};
            return fol;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="da"></param>
        /// <param name="conn"></param>
        private void setupItemsCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            lock (ds)
            {
                da.InsertCommand = createInsertCommand("inventoryitems", ds.Tables["inventoryitems"]);
                da.InsertCommand.Connection = conn;

                da.UpdateCommand = createUpdateCommand("inventoryitems", "UUID=:UUID", ds.Tables["inventoryitems"]);
                da.UpdateCommand.Connection = conn;

                SqliteCommand delete = new SqliteCommand("delete from inventoryitems where UUID = :UUID");
                delete.Parameters.Add(createSqliteParameter("UUID", typeof(String)));
                delete.Connection = conn;
                da.DeleteCommand = delete;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="da"></param>
        /// <param name="conn"></param>
        private void setupFoldersCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            lock (ds)
            {
                da.InsertCommand = createInsertCommand("inventoryfolders", ds.Tables["inventoryfolders"]);
                da.InsertCommand.Connection = conn;

                da.UpdateCommand = createUpdateCommand("inventoryfolders", "UUID=:UUID", ds.Tables["inventoryfolders"]);
                da.UpdateCommand.Connection = conn;

                SqliteCommand delete = new SqliteCommand("delete from inventoryfolders where UUID = :UUID");
                delete.Parameters.Add(createSqliteParameter("UUID", typeof(String)));
                delete.Connection = conn;
                da.DeleteCommand = delete;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private static InventoryFolderBase buildFolder(DataRow row)
        {
            InventoryFolderBase folder = new InventoryFolderBase();
            folder.ID = new UUID((string) row["UUID"]);
            folder.Name = (string) row["name"];
            folder.Owner = new UUID((string) row["agentID"]);
            folder.ParentID = new UUID((string) row["parentID"]);
            folder.Type = Convert.ToInt16(row["type"]);
            folder.Version = Convert.ToUInt16(row["version"]);
            return folder;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="folder"></param>
        private static void fillFolderRow(DataRow row, InventoryFolderBase folder)
        {
            row["UUID"] = folder.ID.ToString();
            row["name"] = folder.Name;
            row["agentID"] = folder.Owner.ToString();
            row["parentID"] = folder.ParentID.ToString();
            row["type"] = folder.Type;
            row["version"] = folder.Version;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="folder"></param>
        private static void moveFolderRow(DataRow row, InventoryFolderBase folder)
        {
            row["UUID"] = folder.ID.ToString();
            row["parentID"] = folder.ParentID.ToString();
        }

        public List<InventoryItemBase> fetchActiveGestures (UUID avatarID)
        {
            lock (ds)
            {
                List<InventoryItemBase> items = new List<InventoryItemBase>();

                DataTable inventoryItemTable = ds.Tables["inventoryitems"];
                string selectExp 
                    = "avatarID = '" + avatarID + "' AND assetType = " + (int)AssetType.Gesture + " AND flags = 1";
                //m_log.DebugFormat("[SQL]: sql = " + selectExp);
                DataRow[] rows = inventoryItemTable.Select(selectExp);
                foreach (DataRow row in rows)
                {
                    items.Add(buildItem(row));
                }
                return items;
            }
        }
    }
}
