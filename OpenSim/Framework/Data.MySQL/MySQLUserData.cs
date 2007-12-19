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
* 
*/
using System;
using System.Collections.Generic;
using System.Data;
using libsecondlife;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Data.MySQL
{
    /// <summary>
    /// A database interface class to a user profile storage system
    /// </summary>
    internal class MySQLUserData : IUserData
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

            database =
                new MySQLManager(settingHostname, settingDatabase, settingUsername, settingPassword, settingPooling,
                                 settingPort);
            
            TestTables();
        }

        #region Test and initialization code

        /// <summary>
        /// Ensure that the user related tables exists and are at the latest version
        /// </summary>
        private void TestTables()
        {
            Dictionary<string, string> tableList = new Dictionary<string, string>();

            tableList["agents"] = null;
            tableList["users"] = null;
            database.GetTableVersion(tableList);

            UpgradeAgentsTable(tableList["agents"]);
            UpgradeUsersTable(tableList["users"]);
        }
        
        /// <summary>
        /// Create or upgrade the table if necessary
        /// </summary>
        /// <param name="oldVersion">A null indicates that the table does not
        /// currently exist</param>
        private void UpgradeAgentsTable(string oldVersion)
        {
            // null as the version, indicates that the table didn't exist
            if (oldVersion == null)
            {
                database.ExecuteResourceSql("CreateAgentsTable.sql");
                return;
            }
        }           
        
        /// <summary>
        /// Create or upgrade the table if necessary
        /// </summary>
        /// <param name="oldVersion">A null indicates that the table does not
        /// currently exist</param>
        private void UpgradeUsersTable(string oldVersion)
        {
            // null as the version, indicates that the table didn't exist
            if (oldVersion == null)
            {
                database.ExecuteResourceSql("CreateUsersTable.sql");
                return;
            }
        }   
        
        #endregion

        // see IUserData
        public UserProfileData GetUserByName(string user, string last)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?first"] = user;
                    param["?second"] = last;

                    IDbCommand result =
                        database.Query("SELECT * FROM users WHERE username = ?first AND lastname = ?second", param);
                    IDataReader reader = result.ExecuteReader();

                    UserProfileData row = database.readUserRow(reader);

                    reader.Close();
                    result.Dispose();
                    return row;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                MainLog.Instance.Error(e.ToString());
                return null;
            }
        }

        public List<OpenSim.Framework.AvatarPickerAvatar> GeneratePickerResults(LLUUID queryID, string query)
        {
            List<OpenSim.Framework.AvatarPickerAvatar> returnlist = new List<OpenSim.Framework.AvatarPickerAvatar>();

            System.Text.RegularExpressions.Regex objAlphaNumericPattern = new System.Text.RegularExpressions.Regex("[^a-zA-Z0-9]");

            string[] querysplit;
            querysplit = query.Split(' ');
            if (querysplit.Length == 2)
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["?first"] = objAlphaNumericPattern.Replace(querysplit[0], "") + "%";
                param["?second"] = objAlphaNumericPattern.Replace(querysplit[1], "") + "%";
                try
                {
                    lock (database)
                    {


                        IDbCommand result =
                            database.Query("SELECT UUID,username,lastname FROM users WHERE username like ?first AND lastname like ?second LIMIT 100", param);
                        IDataReader reader = result.ExecuteReader();


                        while (reader.Read())
                        {
                            OpenSim.Framework.AvatarPickerAvatar user = new OpenSim.Framework.AvatarPickerAvatar();
                            user.AvatarID = new LLUUID((string)reader["UUID"]);
                            user.firstName = (string)reader["username"];
                            user.lastName = (string)reader["lastname"];
                            returnlist.Add(user);

                        }
                        reader.Close();
                        result.Dispose();
                    }
                }
                catch (Exception e)
                {
                    database.Reconnect();
                    MainLog.Instance.Error(e.ToString());
                    return returnlist;
                }



            }
            else if (querysplit.Length == 1)
            {

                try
                {
                    lock (database)
                    {
                        Dictionary<string, string> param = new Dictionary<string, string>();
                        param["?first"] = objAlphaNumericPattern.Replace(querysplit[0], "") + "%";

                        IDbCommand result =
                            database.Query("SELECT UUID,username,lastname FROM users WHERE username like ?first OR lastname like ?first LIMIT 100", param);
                        IDataReader reader = result.ExecuteReader();


                        while (reader.Read())
                        {
                            OpenSim.Framework.AvatarPickerAvatar user = new OpenSim.Framework.AvatarPickerAvatar();
                            user.AvatarID = new LLUUID((string)reader["UUID"]);
                            user.firstName = (string)reader["username"];
                            user.lastName = (string)reader["lastname"];
                            returnlist.Add(user);

                        }
                        reader.Close();
                        result.Dispose();
                    }
                }
                catch (Exception e)
                {
                    database.Reconnect();
                    MainLog.Instance.Error(e.ToString());
                    return returnlist;
                }
            }
            return returnlist;
        }

        // see IUserData
        public UserProfileData GetUserByUUID(LLUUID uuid)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?uuid"] = uuid.ToStringHyphenated();

                    IDbCommand result = database.Query("SELECT * FROM users WHERE UUID = ?uuid", param);
                    IDataReader reader = result.ExecuteReader();

                    UserProfileData row = database.readUserRow(reader);

                    reader.Close();
                    result.Dispose();

                    return row;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                MainLog.Instance.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Returns a user session searching by name
        /// </summary>
        /// <param name="name">The account name</param>
        /// <returns>The users session</returns>
        public UserAgentData GetAgentByName(string name)
        {
            return GetAgentByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        /// <summary>
        /// Returns a user session by account name
        /// </summary>
        /// <param name="user">First part of the users account name</param>
        /// <param name="last">Second part of the users account name</param>
        /// <returns>The users session</returns>
        public UserAgentData GetAgentByName(string user, string last)
        {
            UserProfileData profile = GetUserByName(user, last);
            return GetAgentByUUID(profile.UUID);
        }

        /// <summary>
        /// Returns an agent session by account UUID
        /// </summary>
        /// <param name="uuid">The accounts UUID</param>
        /// <returns>The users session</returns>
        public UserAgentData GetAgentByUUID(LLUUID uuid)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?uuid"] = uuid.ToStringHyphenated();

                    IDbCommand result = database.Query("SELECT * FROM agents WHERE UUID = ?uuid", param);
                    IDataReader reader = result.ExecuteReader();

                    UserAgentData row = database.readAgentRow(reader);

                    reader.Close();
                    result.Dispose();

                    return row;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                MainLog.Instance.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Creates a new users profile
        /// </summary>
        /// <param name="user">The user profile to create</param>
        public void AddNewUserProfile(UserProfileData user)
        {
            try
            {
                lock (database)
                {
                    database.insertUserRow(user.UUID, user.username, user.surname, user.passwordHash, user.passwordSalt,
                                           user.homeRegion, user.homeLocation.X, user.homeLocation.Y,
                                           user.homeLocation.Z,
                                           user.homeLookAt.X, user.homeLookAt.Y, user.homeLookAt.Z, user.created,
                                           user.lastLogin, user.userInventoryURI, user.userAssetURI,
                                           user.profileCanDoMask, user.profileWantDoMask,
                                           user.profileAboutText, user.profileFirstText, user.profileImage,
                                           user.profileFirstImage);
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                MainLog.Instance.Error(e.ToString());
            }
        }

        /// <summary>
        /// Creates a new agent
        /// </summary>
        /// <param name="agent">The agent to create</param>
        public void AddNewUserAgent(UserAgentData agent)
        {
            // Do nothing.
        }

        /// <summary>
        /// Updates a user profile stored in the DB
        /// </summary>
        /// <param name="user">The profile data to use to update the DB</param>
        public bool UpdateUserProfile(UserProfileData user)
        {
            // TODO: implement
            return true;
        }

        /// <summary>
        /// Performs a money transfer request between two accounts
        /// </summary>
        /// <param name="from">The senders account ID</param>
        /// <param name="to">The receivers account ID</param>
        /// <param name="amount">The amount to transfer</param>
        /// <returns>Success?</returns>
        public bool MoneyTransferRequest(LLUUID from, LLUUID to, uint amount)
        {
            return false;
        }

        /// <summary>
        /// Performs an inventory transfer request between two accounts
        /// </summary>
        /// <remarks>TODO: Move to inventory server</remarks>
        /// <param name="from">The senders account ID</param>
        /// <param name="to">The receivers account ID</param>
        /// <param name="item">The item to transfer</param>
        /// <returns>Success?</returns>
        public bool InventoryTransferRequest(LLUUID from, LLUUID to, LLUUID item)
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
        public string GetVersion()
        {
            return "0.1";
        }
    }
}
