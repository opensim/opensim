using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGrid.Framework.Data.MySQL
{
    class MySQLInventoryData
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
            return "MySQL Logdata Interface";
        }

        public void Close()
        {
            // Do nothing.
        }

        public string getVersion()
        {
            return "0.1";
        }
    }
}
