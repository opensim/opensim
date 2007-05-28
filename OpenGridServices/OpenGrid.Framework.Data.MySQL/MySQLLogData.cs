using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGrid.Framework.Data.MySQL
{
    class MySQLLogData : ILogData
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

        public void saveLog(string serverDaemon, string target, string methodCall, string arguments, int priority, string logMessage)
        {
            database.insertLogRow(serverDaemon, target, methodCall, arguments, priority, logMessage);
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
