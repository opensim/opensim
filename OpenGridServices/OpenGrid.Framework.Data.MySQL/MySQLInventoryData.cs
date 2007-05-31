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
            return null;
        }

        public List<InventoryFolderBase> getUserRootFolders(LLUUID user)
        {
            return null;
        }

        public List<InventoryFolderBase> getInventoryFolders(LLUUID parentID)
        {
            return null;
        }

        public InventoryItemBase getInventoryItem(LLUUID item)
        {
            return null;
        }

        public InventoryFolderBase getInventoryFolder(LLUUID folder)
        {
            return null;
        }
    }
}
