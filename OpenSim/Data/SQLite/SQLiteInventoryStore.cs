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
using System.Reflection;
using libsecondlife;
using Mono.Data.SqliteClient;
using OpenSim.Framework.Console;

namespace OpenSim.Data.SQLite
{
    public class SQLiteInventoryStore : SQLiteUtil, IInventoryData
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const string invItemsSelect = "select * from inventoryitems";
        private const string invFoldersSelect = "select * from inventoryfolders";

        private DataSet ds;
        private SqliteDataAdapter invItemsDa;
        private SqliteDataAdapter invFoldersDa;

        /// <summary>
        /// Initialises the interface
        /// </summary>
        public void Initialise()
        {
            Initialise("inventoryStore.db", "inventoryDatabase");
        }

        public void Initialise(string dbfile, string dbname)
        {
            string connectionString = "URI=file:" + dbfile + ",version=3";

            m_log.Info("[Inventory]: Sqlite - connecting: " + dbfile);
            SqliteConnection conn = new SqliteConnection(connectionString);

            conn.Open();

            TestTables(conn);

            SqliteCommand itemsSelectCmd = new SqliteCommand(invItemsSelect, conn);
            invItemsDa = new SqliteDataAdapter(itemsSelectCmd);
            //            SqliteCommandBuilder primCb = new SqliteCommandBuilder(primDa);

            SqliteCommand foldersSelectCmd = new SqliteCommand(invFoldersSelect, conn);
            invFoldersDa = new SqliteDataAdapter(foldersSelectCmd);

            ds = new DataSet();

            ds.Tables.Add(createInventoryFoldersTable());
            invFoldersDa.Fill(ds.Tables["inventoryfolders"]);
            setupFoldersCommands(invFoldersDa, conn);
            m_log.Info("[DATASTORE]: Populated Intentory Folders Definitions");

            ds.Tables.Add(createInventoryItemsTable());
            invItemsDa.Fill(ds.Tables["inventoryitems"]);
            setupItemsCommands(invItemsDa, conn);
            m_log.Info("[DATASTORE]: Populated Intentory Items Definitions");

            ds.AcceptChanges();
        }

        public InventoryItemBase buildItem(DataRow row)
        {
            InventoryItemBase item = new InventoryItemBase();
            item.inventoryID = new LLUUID((string) row["UUID"]);
            item.assetID = new LLUUID((string) row["assetID"]);
            item.assetType = Convert.ToInt32(row["assetType"]);
            item.invType = Convert.ToInt32(row["invType"]);
            item.parentFolderID = new LLUUID((string) row["parentFolderID"]);
            item.avatarID = new LLUUID((string) row["avatarID"]);
            item.creatorsID = new LLUUID((string) row["creatorsID"]);
            item.inventoryName = (string) row["inventoryName"];
            item.inventoryDescription = (string) row["inventoryDescription"];

            item.inventoryNextPermissions = Convert.ToUInt32(row["inventoryNextPermissions"]);
            item.inventoryCurrentPermissions = Convert.ToUInt32(row["inventoryCurrentPermissions"]);
            item.inventoryBasePermissions = Convert.ToUInt32(row["inventoryBasePermissions"]);
            item.inventoryEveryOnePermissions = Convert.ToUInt32(row["inventoryEveryOnePermissions"]);
            return item;
        }

        private void fillItemRow(DataRow row, InventoryItemBase item)
        {
            row["UUID"] = Util.ToRawUuidString(item.inventoryID);
            row["assetID"] = Util.ToRawUuidString(item.assetID);
            row["assetType"] = item.assetType;
            row["invType"] = item.invType;
            row["parentFolderID"] = Util.ToRawUuidString(item.parentFolderID);
            row["avatarID"] = Util.ToRawUuidString(item.avatarID);
            row["creatorsID"] = Util.ToRawUuidString(item.creatorsID);
            row["inventoryName"] = item.inventoryName;
            row["inventoryDescription"] = item.inventoryDescription;

            row["inventoryNextPermissions"] = item.inventoryNextPermissions;
            row["inventoryCurrentPermissions"] = item.inventoryCurrentPermissions;
            row["inventoryBasePermissions"] = item.inventoryBasePermissions;
            row["inventoryEveryOnePermissions"] = item.inventoryEveryOnePermissions;
        }

        private void addFolder(InventoryFolderBase folder)
        {
            lock (ds)
            {
                DataTable inventoryFolderTable = ds.Tables["inventoryfolders"];

                DataRow inventoryRow = inventoryFolderTable.Rows.Find(Util.ToRawUuidString(folder.folderID));
                if (inventoryRow == null)
                {
                    inventoryRow = inventoryFolderTable.NewRow();
                    fillFolderRow(inventoryRow, folder);
                    inventoryFolderTable.Rows.Add(inventoryRow);
                }
                else
                {
                    fillFolderRow(inventoryRow, folder);
                }

                invFoldersDa.Update(ds, "inventoryfolders");
            }
        }

        private void moveFolder(InventoryFolderBase folder)
        {
            lock (ds)
            {
                DataTable inventoryFolderTable = ds.Tables["inventoryfolders"];

                DataRow inventoryRow = inventoryFolderTable.Rows.Find(Util.ToRawUuidString(folder.folderID));
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

        private void addItem(InventoryItemBase item)
        {
            lock (ds)
            {
                DataTable inventoryItemTable = ds.Tables["inventoryitems"];

                DataRow inventoryRow = inventoryItemTable.Rows.Find(Util.ToRawUuidString(item.inventoryID));
                if (inventoryRow == null)
                {
                    inventoryRow = inventoryItemTable.NewRow();
                    fillItemRow(inventoryRow, item);
                    inventoryItemTable.Rows.Add(inventoryRow);
                }
                else
                {
                    fillItemRow(inventoryRow, item);
                }
                invItemsDa.Update(ds, "inventoryitems");
            }
        }

        public void Shutdown()
        {
            // TODO: DataSet commit
        }

        /// <summary>
        /// Closes the interface
        /// </summary>
        public void Close()
        {
        }

        /// <summary>
        /// The plugin being loaded
        /// </summary>
        /// <returns>A string containing the plugin name</returns>
        public string getName()
        {
            return "SQLite Inventory Data Interface";
        }

        /// <summary>
        /// The plugins version
        /// </summary>
        /// <returns>A string containing the plugin version</returns>
        public string getVersion()
        {
            Module module = GetType().Module;
            string dllName = module.Assembly.ManifestModule.Name;
            Version dllVersion = module.Assembly.GetName().Version;


            return
                string.Format("{0}.{1}.{2}.{3}", dllVersion.Major, dllVersion.Minor, dllVersion.Build,
                              dllVersion.Revision);
        }

        /// <summary>
        /// Returns a list of inventory items contained within the specified folder
        /// </summary>
        /// <param name="folderID">The UUID of the target folder</param>
        /// <returns>A List of InventoryItemBase items</returns>
        public List<InventoryItemBase> getInventoryInFolder(LLUUID folderID)
        {
            lock (ds)
            {
                List<InventoryItemBase> retval = new List<InventoryItemBase>();
                DataTable inventoryItemTable = ds.Tables["inventoryitems"];
                string selectExp = "parentFolderID = '" + Util.ToRawUuidString(folderID) + "'";
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
        public List<InventoryFolderBase> getUserRootFolders(LLUUID user)
        {
            return new List<InventoryFolderBase>();
        }

        // see InventoryItemBase.getUserRootFolder
        public InventoryFolderBase getUserRootFolder(LLUUID user)
        {
            lock (ds)
            {
                List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
                DataTable inventoryFolderTable = ds.Tables["inventoryfolders"];
                string selectExp = "agentID = '" + Util.ToRawUuidString(user) + "' AND parentID = '" +
                                   Util.ToRawUuidString(LLUUID.Zero) + "'";
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
        protected void getInventoryFolders(ref List<InventoryFolderBase> folders, LLUUID parentID)
        {
            lock (ds)
            {
                DataTable inventoryFolderTable = ds.Tables["inventoryfolders"];
                string selectExp = "parentID = '" + Util.ToRawUuidString(parentID) + "'";
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
        public List<InventoryFolderBase> getInventoryFolders(LLUUID parentID)
        {
            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            getInventoryFolders(ref folders, Util.ToRawUuidString(parentID));
            return folders;
        }

        // See IInventoryData
        public List<InventoryFolderBase> getFolderHierarchy(LLUUID parentID)
        {
            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            getInventoryFolders(ref folders, Util.ToRawUuidString(parentID));

            for (int i = 0; i < folders.Count; i++)
                getInventoryFolders(ref folders, Util.ToRawUuidString(folders[i].folderID));

            return folders;
        }

        /// <summary>
        /// Returns an inventory item by its UUID
        /// </summary>
        /// <param name="item">The UUID of the item to be returned</param>
        /// <returns>A class containing item information</returns>
        public InventoryItemBase getInventoryItem(LLUUID item)
        {
            lock (ds)
            {
                DataRow row = ds.Tables["inventoryitems"].Rows.Find(Util.ToRawUuidString(item));
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
        public InventoryFolderBase getInventoryFolder(LLUUID folder)
        {
            // TODO: Deep voodoo here.  If you enable this code then
            // multi region breaks.  No idea why, but I figured it was
            // better to leave multi region at this point.  It does mean
            // that you don't get to see system textures why creating
            // clothes and the like. :(
            lock (ds)
            {
                DataRow row = ds.Tables["inventoryfolders"].Rows.Find(Util.ToRawUuidString(folder));
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
            addItem(item);
        }

        /// <summary>
        /// Updates an inventory item with item (updates based on ID)
        /// </summary>
        /// <param name="item">The updated item</param>
        public void updateInventoryItem(InventoryItemBase item)
        {
            addItem(item);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        public void deleteInventoryItem(LLUUID itemID)
        {
            lock (ds)
            {
                DataTable inventoryItemTable = ds.Tables["inventoryitems"];

                DataRow inventoryRow = inventoryItemTable.Rows.Find(Util.ToRawUuidString(itemID));
                if (inventoryRow != null)
                {
                    inventoryRow.Delete();
                }

                invItemsDa.Update(ds, "inventoryitems");
            }
        }

        /// <summary>
        /// Delete all items in the specified folder
        /// </summary>
        /// <param name="folderId">id of the folder, whose item content should be deleted</param>
        //!TODO, this is horribly inefficient, but I don't want to ruin the overall structure of this implementation
        private void deleteItemsInFolder(LLUUID folderId)
        {
            List<InventoryItemBase> items = getInventoryInFolder(Util.ToRawUuidString(folderId));

            foreach (InventoryItemBase i in items)
                deleteInventoryItem(Util.ToRawUuidString(i.inventoryID));
        }

        /// <summary>
        /// Adds a new folder specified by folder
        /// </summary>
        /// <param name="folder">The inventory folder</param>
        public void addInventoryFolder(InventoryFolderBase folder)
        {
            addFolder(folder);
        }

        /// <summary>
        /// Updates a folder based on its ID with folder
        /// </summary>
        /// <param name="folder">The inventory folder</param>
        public void updateInventoryFolder(InventoryFolderBase folder)
        {
            addFolder(folder);
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
        /// <param name="item"></param>
        public void deleteInventoryFolder(LLUUID folderID)
        {
            lock (ds)
            {
                List<InventoryFolderBase> subFolders = getFolderHierarchy(Util.ToRawUuidString(folderID));

                DataTable inventoryFolderTable = ds.Tables["inventoryfolders"];
                DataRow inventoryRow;

                //Delete all sub-folders
                foreach (InventoryFolderBase f in subFolders)
                {
                    inventoryRow = inventoryFolderTable.Rows.Find(Util.ToRawUuidString(f.folderID));
                    if (inventoryRow != null)
                    {
                        deleteItemsInFolder(Util.ToRawUuidString(f.folderID));
                        inventoryRow.Delete();
                    }
                }

                //Delete the actual row
                inventoryRow = inventoryFolderTable.Rows.Find(Util.ToRawUuidString(folderID));
                if (inventoryRow != null)
                {
                    deleteItemsInFolder(Util.ToRawUuidString(folderID));
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

            inv.PrimaryKey = new DataColumn[] {inv.Columns["UUID"]};
            return inv;
        }

        private DataTable createInventoryFoldersTable()
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

        private InventoryFolderBase buildFolder(DataRow row)
        {
            InventoryFolderBase folder = new InventoryFolderBase();
            folder.folderID = new LLUUID((string) row["UUID"]);
            folder.name = (string) row["name"];
            folder.agentID = new LLUUID((string) row["agentID"]);
            folder.parentID = new LLUUID((string) row["parentID"]);
            folder.type = Convert.ToInt16(row["type"]);
            folder.version = Convert.ToUInt16(row["version"]);
            return folder;
        }

        private void fillFolderRow(DataRow row, InventoryFolderBase folder)
        {
            row["UUID"] = Util.ToRawUuidString(folder.folderID);
            row["name"] = folder.name;
            row["agentID"] = Util.ToRawUuidString(folder.agentID);
            row["parentID"] = Util.ToRawUuidString(folder.parentID);
            row["type"] = folder.type;
            row["version"] = folder.version;
        }

        private void moveFolderRow(DataRow row, InventoryFolderBase folder)
        {
            row["UUID"] = Util.ToRawUuidString(folder.folderID);
            row["parentID"] = Util.ToRawUuidString(folder.parentID);
        }

        /***********************************************************************
         *
         *  Test and Initialization code
         *
         **********************************************************************/

        private void InitDB(SqliteConnection conn)
        {
            string createInventoryItems = defineTable(createInventoryItemsTable());
            string createInventoryFolders = defineTable(createInventoryFoldersTable());
            
            SqliteCommand pcmd = new SqliteCommand(createInventoryItems, conn);
            SqliteCommand scmd = new SqliteCommand(createInventoryFolders, conn);

            pcmd.ExecuteNonQuery();
            scmd.ExecuteNonQuery();
        }

        private bool TestTables(SqliteConnection conn)
        {
            SqliteCommand invItemsSelectCmd = new SqliteCommand(invItemsSelect, conn);
            SqliteDataAdapter pDa = new SqliteDataAdapter(invItemsSelectCmd);
            SqliteCommand invFoldersSelectCmd = new SqliteCommand(invFoldersSelect, conn);
            SqliteDataAdapter sDa = new SqliteDataAdapter(invFoldersSelectCmd);

            DataSet tmpDS = new DataSet();
            try
            {
                pDa.Fill(tmpDS, "inventoryitems");
                sDa.Fill(tmpDS, "inventoryfolders");
            }
            catch (SqliteSyntaxException)
            {
                m_log.Info("[DATASTORE]: SQLite Database doesn't exist... creating");
                InitDB(conn);
            }

            pDa.Fill(tmpDS, "inventoryitems");
            sDa.Fill(tmpDS, "inventoryfolders");

            foreach (DataColumn col in createInventoryItemsTable().Columns)
            {
                if (! tmpDS.Tables["inventoryitems"].Columns.Contains(col.ColumnName))
                {
                    m_log.Info("[DATASTORE]: Missing required column:" + col.ColumnName);
                    return false;
                }
            }
            foreach (DataColumn col in createInventoryFoldersTable().Columns)
            {
                if (! tmpDS.Tables["inventoryfolders"].Columns.Contains(col.ColumnName))
                {
                    m_log.Info("[DATASTORE]: Missing required column:" + col.ColumnName);
                    return false;
                }
            }
            return true;
        }
    }
}
