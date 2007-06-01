using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenGrid.Framework.Data.MySQL
{
    class MySQLInventoryData : IInventoryData
    {
        public MySQLManager database;

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
        
        public string getName()
        {
            return "MySQL Inventory Data Interface";
        }

        public void Close()
        {
            // Do nothing.
        }

        public string getVersion()
        {
            return "0.1";
        }

        public List<InventoryItemBase> getInventoryInFolder(LLUUID folderID)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?uuid"] = folderID.ToStringHyphenated();

                    System.Data.IDbCommand result = database.Query("SELECT * FROM inventoryitems WHERE parentFolderID = ?uuid", param);
                    System.Data.IDataReader reader = result.ExecuteReader();

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

        public List<InventoryFolderBase> getUserRootFolders(LLUUID user)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?uuid"] = user.ToStringHyphenated();
                    param["?zero"] = LLUUID.Zero.ToStringHyphenated();

                    System.Data.IDbCommand result = database.Query("SELECT * FROM inventoryfolders WHERE parentFolderID = ?zero AND agentID = ?uuid", param);
                    System.Data.IDataReader reader = result.ExecuteReader();

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

        public List<InventoryFolderBase> getInventoryFolders(LLUUID parentID)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?uuid"] = parentID.ToStringHyphenated();

                    System.Data.IDbCommand result = database.Query("SELECT * FROM inventoryfolders WHERE parentFolderID = ?uuid", param);
                    System.Data.IDataReader reader = result.ExecuteReader();

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

        public InventoryItemBase getInventoryItem(LLUUID item)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?uuid"] = item.ToStringHyphenated();

                    System.Data.IDbCommand result = database.Query("SELECT * FROM inventoryitems WHERE inventoryID = ?uuid", param);
                    System.Data.IDataReader reader = result.ExecuteReader();

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

        public InventoryFolderBase getInventoryFolder(LLUUID folder)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?uuid"] = folder.ToStringHyphenated();

                    System.Data.IDbCommand result = database.Query("SELECT * FROM inventoryfolders WHERE folderID = ?uuid", param);
                    System.Data.IDataReader reader = result.ExecuteReader();

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

        public void addInventoryItem(InventoryItemBase item)
        {
            
        }

        public void updateInventoryItem(InventoryItemBase item)
        {
            addInventoryItem(item);
        }

        public void addInventoryFolder(InventoryFolderBase folder)
        {

        }

        public void updateInventoryFolder(InventoryFolderBase folder)
        {
            addInventoryFolder(folder);
        }
    }
}
