using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework.Console;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;
using libsecondlife;

using System.Data;
using System.Data.SqlTypes;

using Mono.Data.SqliteClient;

namespace OpenSim.Framework.Data.SQLite
{

    public class SQLiteInventoryStore : SQLiteBase, IInventoryData
    {
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

            MainLog.Instance.Verbose("Inventory", "Sqlite - connecting: " + dbfile);
            SqliteConnection conn = new SqliteConnection(connectionString);

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
            MainLog.Instance.Verbose("DATASTORE", "Populated Intentory Folders Definitions");

            ds.Tables.Add(createInventoryItemsTable());
            invItemsDa.Fill(ds.Tables["inventoryitems"]);
            setupItemsCommands(invItemsDa, conn);
            MainLog.Instance.Verbose("DATASTORE", "Populated Intentory Items Definitions");

            ds.AcceptChanges();
            return;
        }

        public InventoryItemBase buildItem(DataRow row)
        {
            InventoryItemBase item = new InventoryItemBase();
            item.inventoryID = new LLUUID((string)row["UUID"]);
            item.assetID = new LLUUID((string)row["assetID"]);
            item.assetType = Convert.ToInt32(row["assetType"]);
            item.invType = Convert.ToInt32(row["invType"]);
            item.parentFolderID = new LLUUID((string)row["parentFolderID"]);
            item.avatarID = new LLUUID((string)row["avatarID"]);
            item.creatorsID = new LLUUID((string)row["creatorsID"]);
            item.inventoryName =(string) row["inventoryName"];
            item.inventoryDescription = (string) row["inventoryDescription"];

            item.inventoryNextPermissions = Convert.ToUInt32(row["inventoryNextPermissions"]);
            item.inventoryCurrentPermissions = Convert.ToUInt32(row["inventoryCurrentPermissions"]);
            item.inventoryBasePermissions = Convert.ToUInt32(row["inventoryBasePermissions"]);
            item.inventoryEveryOnePermissions = Convert.ToUInt32(row["inventoryEveryOnePermissions"]);
            return item;
        }

        private void fillItemRow(DataRow row, InventoryItemBase item)
        {
            row["UUID"] = item.inventoryID;
            row["assetID"] = item.assetID;
            row["assetType"] = item.assetType;
            row["invType"] = item.invType;
            row["parentFolderID"] = item.parentFolderID;
            row["avatarID"] = item.avatarID;
            row["creatorsID"] = item.creatorsID;
            row["inventoryName"] = item.inventoryName;
            row["inventoryDescription"] = item.inventoryDescription;

            row["inventoryNextPermissions"] = item.inventoryNextPermissions;
            row["inventoryCurrentPermissions"] = item.inventoryCurrentPermissions;
            row["inventoryBasePermissions"] = item.inventoryBasePermissions;
            row["inventoryEveryOnePermissions"] = item.inventoryEveryOnePermissions;
        }

        private void addFolder(InventoryFolderBase folder)
        {
            DataTable inventoryFolderTable = ds.Tables["inventoryfolders"];

            DataRow inventoryRow = inventoryFolderTable.Rows.Find(folder.folderID);
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

            this.invFoldersDa.Update(ds, "inventoryfolders");
        }

        private void addItem(InventoryItemBase item)
        {
            DataTable inventoryItemTable = ds.Tables["inventoryitems"];

            DataRow inventoryRow = inventoryItemTable.Rows.Find(item.inventoryID);
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
            this.invItemsDa.Update(ds, "inventoryitems");
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
            return "0.1";
        }

        /// <summary>
        /// Returns a list of inventory items contained within the specified folder
        /// </summary>
        /// <param name="folderID">The UUID of the target folder</param>
        /// <returns>A List of InventoryItemBase items</returns>
        public List<InventoryItemBase> getInventoryInFolder(LLUUID folderID)
        {
            List<InventoryItemBase> retval = new List<InventoryItemBase>();
            DataTable inventoryItemTable = ds.Tables["inventoryitems"];
            string selectExp = "parentFolderID = '" + folderID.ToString() + "'";
            DataRow[] rows = inventoryItemTable.Select(selectExp);
            foreach (DataRow row in rows)
            {
                retval.Add(buildItem(row));
            }

            return retval;
        }

        /// <summary>
        /// Returns a list of the root folders within a users inventory
        /// </summary>
        /// <param name="user">The user whos inventory is to be searched</param>
        /// <returns>A list of folder objects</returns>
        public List<InventoryFolderBase> getUserRootFolders(LLUUID user)
        {
            return null;
        }

        /// <summary>
        /// Returns the users inventory root folder.
        /// </summary>
        /// <param name="user">The UUID of the user who is having inventory being returned</param>
        /// <returns>Root inventory folder</returns>
        public InventoryFolderBase getUserRootFolder(LLUUID user)
        {
            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            DataTable inventoryFolderTable = ds.Tables["inventoryfolders"];
            string selectExp = "agentID = '" + user.ToString() + "' AND parentID = '" + LLUUID.Zero.ToString() + "'";
            DataRow[] rows = inventoryFolderTable.Select(selectExp);
            foreach (DataRow row in rows)
            {
                folders.Add(buildFolder(row));
            }

            if (folders.Count == 1)
            {
                //we found the root
                //System.Console.WriteLine("found root inventory folder");
                return folders[0];
            }
            else if (folders.Count > 1)
            {
                //err shouldn't be more than one root
                //System.Console.WriteLine("found more than one root inventory folder");
            }
            else if (folders.Count == 0)
            {
                // no root?
                //System.Console.WriteLine("couldn't find root inventory folder");
            }

            return null;
        }

        /// <summary>
        /// Returns a list of inventory folders contained in the folder 'parentID'
        /// </summary>
        /// <param name="parentID">The folder to get subfolders for</param>
        /// <returns>A list of inventory folders</returns>
        public List<InventoryFolderBase> getInventoryFolders(LLUUID parentID)
        {
            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            DataTable inventoryFolderTable = ds.Tables["inventoryfolders"];
            string selectExp = "parentID = '" + parentID.ToString() + "'";
            DataRow[] rows = inventoryFolderTable.Select(selectExp);
            foreach (DataRow row in rows)
            {
                folders.Add(this.buildFolder(row));
            }
            // System.Console.WriteLine("found " + folders.Count + " inventory folders");
            return folders;
        }

        /// <summary>
        /// Returns an inventory item by its UUID
        /// </summary>
        /// <param name="item">The UUID of the item to be returned</param>
        /// <returns>A class containing item information</returns>
        public InventoryItemBase getInventoryItem(LLUUID item)
        {
            DataRow row = ds.Tables["inventoryitems"].Rows.Find(item);
            if (row != null) {
                return buildItem(row);
            } else {
                return null;
            }
        }

        /// <summary>
        /// Returns a specified inventory folder by its UUID
        /// </summary>
        /// <param name="folder">The UUID of the folder to be returned</param>
        /// <returns>A class containing folder information</returns>
        public InventoryFolderBase getInventoryFolder(LLUUID folder)
        {
            DataRow row = ds.Tables["inventoryfolders"].Rows.Find(folder);
            if (row != null) {
                return buildFolder(row);
            } else {
                return null;
            }
        }

        /// <summary>
        /// Creates a new inventory item based on item
        /// </summary>
        /// <param name="item">The item to be created</param>
        public void addInventoryItem(InventoryItemBase item)
        {
            this.addItem(item);
        }

        /// <summary>
        /// Updates an inventory item with item (updates based on ID)
        /// </summary>
        /// <param name="item">The updated item</param>
        public void updateInventoryItem(InventoryItemBase item)
        {
            this.addItem(item);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        public void deleteInventoryItem(InventoryItemBase item)
        {
            DataTable inventoryItemTable = ds.Tables["inventoryitems"];

             DataRow inventoryRow = inventoryItemTable.Rows.Find(item.inventoryID);
             if (inventoryRow != null)
             {
                 inventoryRow.Delete();
             }

             this.invItemsDa.Update(ds, "inventoryitems");
        }

        /// <summary>
        /// Adds a new folder specified by folder
        /// </summary>
        /// <param name="folder">The inventory folder</param>
        public void addInventoryFolder(InventoryFolderBase folder)
        {
            this.addFolder(folder);
        }

        /// <summary>
        /// Updates a folder based on its ID with folder
        /// </summary>
        /// <param name="folder">The inventory folder</param>
        public void updateInventoryFolder(InventoryFolderBase folder)
        {
            this.addFolder(folder);
        }


        /***********************************************************************
         *
         *  Data Table definitions
         *
         **********************************************************************/
        
        private void createCol(DataTable dt, string name, System.Type type)
        {
            DataColumn col = new DataColumn(name, type);
            dt.Columns.Add(col);
        }

        private DataTable createInventoryItemsTable()
        {
            DataTable inv = new DataTable("inventoryitems");
            
            createCol(inv, "UUID", typeof(System.String)); //inventoryID
            createCol(inv, "assetID", typeof(System.String));
            createCol(inv, "assetType", typeof(System.Int32));
            createCol(inv, "invType", typeof(System.Int32));
            createCol(inv, "parentFolderID", typeof(System.String));
            createCol(inv, "avatarID", typeof(System.String));
            createCol(inv, "creatorsID", typeof(System.String));

            createCol(inv, "inventoryName", typeof(System.String));
            createCol(inv, "inventoryDescription", typeof(System.String));
            // permissions
            createCol(inv, "inventoryNextPermissions", typeof(System.Int32));
            createCol(inv, "inventoryCurrentPermissions", typeof(System.Int32));
            createCol(inv, "inventoryBasePermissions", typeof(System.Int32));
            createCol(inv, "inventoryEveryOnePermissions", typeof(System.Int32));
            
            inv.PrimaryKey = new DataColumn[] { inv.Columns["UUID"] };
            return inv;
        }
        
        private DataTable createInventoryFoldersTable()
        {
            DataTable fol = new DataTable("inventoryfolders");
            
            createCol(fol, "UUID", typeof(System.String)); //folderID
            createCol(fol, "name", typeof(System.String));
            createCol(fol, "agentID", typeof(System.String));
            createCol(fol, "parentID", typeof(System.String));
            createCol(fol, "type", typeof(System.Int32));
            createCol(fol, "version", typeof(System.Int32));
            
            fol.PrimaryKey = new DataColumn[] { fol.Columns["UUID"] };
            return fol;
        }

        private void setupItemsCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("inventoryitems", ds.Tables["inventoryitems"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("inventoryitems", "UUID=:UUID", ds.Tables["inventoryitems"]);
            da.UpdateCommand.Connection = conn;

            SqliteCommand delete = new SqliteCommand("delete from inventoryitems where UUID = :UUID");
            delete.Parameters.Add(createSqliteParameter("UUID", typeof(System.String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        private void setupFoldersCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("inventoryfolders", ds.Tables["inventoryfolders"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("inventoryfolders", "UUID=:UUID", ds.Tables["inventoryfolders"]);
            da.UpdateCommand.Connection = conn;

            SqliteCommand delete = new SqliteCommand("delete from inventoryfolders where UUID = :UUID");
            delete.Parameters.Add(createSqliteParameter("UUID", typeof(System.String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        private InventoryFolderBase buildFolder(DataRow row)
        {
            InventoryFolderBase folder = new InventoryFolderBase();
            folder.folderID = new LLUUID((string)row["UUID"]);
            folder.name = (string)row["name"];
            folder.agentID = new LLUUID((string)row["agentID"]);
            folder.parentID = new LLUUID((string)row["parentID"]);
            folder.type = Convert.ToInt16(row["type"]);
            folder.version = Convert.ToUInt16(row["version"]);
            return folder;
        }

        private void fillFolderRow(DataRow row, InventoryFolderBase folder)
        {
            row["UUID"] = folder.folderID;
            row["name"] = folder.name;
            row["agentID"] = folder.agentID;
            row["parentID"] = folder.parentID;
            row["type"] = folder.type;
            row["version"] = folder.version;
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
            conn.Open();
            pcmd.ExecuteNonQuery();
            scmd.ExecuteNonQuery();
            conn.Close(); 
        }

        private bool TestTables(SqliteConnection conn)
        {
            SqliteCommand invItemsSelectCmd = new SqliteCommand(invItemsSelect, conn);
            SqliteDataAdapter pDa = new SqliteDataAdapter(invItemsSelectCmd);
            SqliteCommand invFoldersSelectCmd = new SqliteCommand(invFoldersSelect, conn);
            SqliteDataAdapter sDa = new SqliteDataAdapter(invFoldersSelectCmd);

            DataSet tmpDS = new DataSet();
            try {
                pDa.Fill(tmpDS, "inventoryitems");
                sDa.Fill(tmpDS, "inventoryfolders");
            } catch (Mono.Data.SqliteClient.SqliteSyntaxException) {
                MainLog.Instance.Verbose("DATASTORE", "SQLite Database doesn't exist... creating");
                InitDB(conn);
            }

            pDa.Fill(tmpDS, "inventoryitems");
            sDa.Fill(tmpDS, "inventoryfolders");

            foreach (DataColumn col in createInventoryItemsTable().Columns) {
                if (! tmpDS.Tables["inventoryitems"].Columns.Contains(col.ColumnName) ) {
                    MainLog.Instance.Verbose("DATASTORE", "Missing required column:" + col.ColumnName);
                    return false;
                }
            }
            foreach (DataColumn col in createInventoryFoldersTable().Columns) {
                if (! tmpDS.Tables["inventoryfolders"].Columns.Contains(col.ColumnName) ) {
                    MainLog.Instance.Verbose("DATASTORE", "Missing required column:" + col.ColumnName);
                    return false;
                }
            }
            return true;
        }
    }
}

