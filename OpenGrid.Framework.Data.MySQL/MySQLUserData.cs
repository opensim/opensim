using System;
using System.Collections.Generic;
using System.Text;
using OpenGrid.Framework.Data;
using libsecondlife;

namespace OpenGrid.Framework.Data.MySQL
{
    class MySQLUserData : IUserData
    {
        public MySQLManager manager;

        public void Initialise()
        {
            manager = new MySQLManager("host", "database", "user", "password", "false");
        }

        public UserProfileData getUserByName(string name)
        {
            return getUserByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        public UserProfileData getUserByName(string user, string last)
        {
            return new UserProfileData();
        }

        public UserProfileData getUserByUUID(LLUUID uuid)
        {
            return new UserProfileData();
        }

        public UserAgentData getAgentByName(string name)
        {
            return getAgentByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        public UserAgentData getAgentByName(string user, string last)
        {
            return new UserAgentData();
        }

        public UserAgentData getAgentByUUID(LLUUID uuid)
        {
            return new UserAgentData();
        }

        public bool moneyTransferRequest(LLUUID from, LLUUID to, uint amount)
        {
            return false;
        }

        public bool inventoryTransferRequest(LLUUID from, LLUUID to, LLUUID item)
        {
            return false;
        }

        public string getName()
        {
            return "MySQL Userdata Interface";
        }

        public string getVersion()
        {
            return "0.1";
        }
    }
}
