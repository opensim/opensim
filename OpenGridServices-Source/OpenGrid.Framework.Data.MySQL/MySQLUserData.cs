using System;
using System.Collections.Generic;
using System.Text;
using OpenGrid.Framework.Data;
using libsecondlife;

namespace OpenGrid.Framework.Data.MySQL
{
    class MySQLUserData : IUserData
    {
        public MySQLManager database;

        public void Initialise()
        {
            database = new MySQLManager("host", "database", "user", "password", "false");
        }

        public UserProfileData getUserByName(string name)
        {
            return getUserByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        public UserProfileData getUserByName(string user, string last)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?first"] = user;
                    param["?second"] = last;

                    System.Data.IDbCommand result = database.Query("SELECT * FROM users WHERE username = ?first AND lastname = ?second", param);
                    System.Data.IDataReader reader = result.ExecuteReader();

                    UserProfileData row = database.getUserRow(reader);
                    
                    reader.Close();
                    result.Dispose();

                    return row;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        public UserProfileData getUserByUUID(LLUUID uuid)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?uuid"] = uuid.ToStringHyphenated();

                    System.Data.IDbCommand result = database.Query("SELECT * FROM users WHERE UUID = ?uuid", param);
                    System.Data.IDataReader reader = result.ExecuteReader();

                    UserProfileData row = database.getUserRow(reader);

                    reader.Close();
                    result.Dispose();

                    return row;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        public UserAgentData getAgentByName(string name)
        {
            return getAgentByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        public UserAgentData getAgentByName(string user, string last)
        {
            UserProfileData profile = getUserByName(user, last);
            return getAgentByUUID(profile.UUID);
        }

        public UserAgentData getAgentByUUID(LLUUID uuid)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?uuid"] = uuid.ToStringHyphenated();

                    System.Data.IDbCommand result = database.Query("SELECT * FROM agents WHERE UUID = ?uuid", param);
                    System.Data.IDataReader reader = result.ExecuteReader();

                    UserAgentData row = database.getAgentRow(reader);

                    reader.Close();
                    result.Dispose();

                    return row;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
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
