/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Data;
using libsecondlife;

namespace OpenSim.Framework.Data.MySQL
{
    /// <summary>
    /// A database interface class to a user profile storage system
    /// </summary>
    class MySQLUserData : IUserData
    {
        /// <summary>
        /// Database manager for MySQL
        /// </summary>
        public MySQLManager database;

        /// <summary>
        /// Loads and initialises the MySQL storage plugin
        /// </summary>
        public void Initialise()
        {
            // Load from an INI file connection details
            // TODO: move this to XML?
            IniFile GridDataMySqlFile = new IniFile("mysql_connection.ini");
            string settingHostname = GridDataMySqlFile.ParseFileReadValue("hostname");
            string settingDatabase = GridDataMySqlFile.ParseFileReadValue("database");
            string settingUsername = GridDataMySqlFile.ParseFileReadValue("username");
            string settingPassword = GridDataMySqlFile.ParseFileReadValue("password");
            string settingPooling = GridDataMySqlFile.ParseFileReadValue("pooling");
            string settingPort = GridDataMySqlFile.ParseFileReadValue("port");

            database = new MySQLManager(settingHostname, settingDatabase, settingUsername, settingPassword, settingPooling, settingPort);
        }

        /// <summary>
        /// Searches the database for a specified user profile
        /// </summary>
        /// <param name="name">The account name of the user</param>
        /// <returns>A user profile</returns>
        public UserProfileData getUserByName(string name)
        {
            return getUserByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        /// <summary>
        /// Searches the database for a specified user profile by name components
        /// </summary>
        /// <param name="user">The first part of the account name</param>
        /// <param name="last">The second part of the account name</param>
        /// <returns>A user profile</returns>
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

                    UserProfileData row = database.readUserRow(reader);
                    
                    reader.Close();
                    result.Dispose();

                    return row;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Searches the database for a specified user profile by UUID
        /// </summary>
        /// <param name="uuid">The account ID</param>
        /// <returns>The users profile</returns>
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

                    UserProfileData row = database.readUserRow(reader);

                    reader.Close();
                    result.Dispose();

                    return row;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Returns a user session searching by name
        /// </summary>
        /// <param name="name">The account name</param>
        /// <returns>The users session</returns>
        public UserAgentData getAgentByName(string name)
        {
            return getAgentByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        /// <summary>
        /// Returns a user session by account name
        /// </summary>
        /// <param name="user">First part of the users account name</param>
        /// <param name="last">Second part of the users account name</param>
        /// <returns>The users session</returns>
        public UserAgentData getAgentByName(string user, string last)
        {
            UserProfileData profile = getUserByName(user, last);
            return getAgentByUUID(profile.UUID);
        }

        /// <summary>
        /// Returns an agent session by account UUID
        /// </summary>
        /// <param name="uuid">The accounts UUID</param>
        /// <returns>The users session</returns>
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

                    UserAgentData row = database.readAgentRow(reader);

                    reader.Close();
                    result.Dispose();

                    return row;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Creates a new users profile
        /// </summary>
        /// <param name="user">The user profile to create</param>
        public void addNewUserProfile(UserProfileData user)
        {
        }

        /// <summary>
        /// Creates a new agent
        /// </summary>
        /// <param name="agent">The agent to create</param>
        public void addNewUserAgent(UserAgentData agent)
        {
            // Do nothing.
        }

        /// <summary>
        /// Performs a money transfer request between two accounts
        /// </summary>
        /// <param name="from">The senders account ID</param>
        /// <param name="to">The recievers account ID</param>
        /// <param name="amount">The amount to transfer</param>
        /// <returns>Success?</returns>
        public bool moneyTransferRequest(LLUUID from, LLUUID to, uint amount)
        {
            return false;
        }

        /// <summary>
        /// Performs an inventory transfer request between two accounts
        /// </summary>
        /// <remarks>TODO: Move to inventory server</remarks>
        /// <param name="from">The senders account ID</param>
        /// <param name="to">The recievers account ID</param>
        /// <param name="item">The item to transfer</param>
        /// <returns>Success?</returns>
        public bool inventoryTransferRequest(LLUUID from, LLUUID to, LLUUID item)
        {
            return false;
        }

        /// <summary>
        /// Database provider name
        /// </summary>
        /// <returns>Provider name</returns>
        public string getName()
        {
            return "MySQL Userdata Interface";
        }

        /// <summary>
        /// Database provider version
        /// </summary>
        /// <returns>provider version</returns>
        public string getVersion()
        {
            return "0.1";
        }
    }
}
