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
using System.Data.SqlClient;
using libsecondlife;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Data.MSSQL
{
    /// <summary>
    /// A database interface class to a user profile storage system
    /// </summary>
    internal class MSSQLUserData : IUserData
    {
        /// <summary>
        /// Database manager for MySQL
        /// </summary>
        public MSSQLManager database;

        /// <summary>
        /// Loads and initialises the MySQL storage plugin
        /// </summary>
        public void Initialise()
        {
            // Load from an INI file connection details
            // TODO: move this to XML?
            IniFile GridDataMySqlFile = new IniFile("mssql_connection.ini");
            string settingDataSource = GridDataMySqlFile.ParseFileReadValue("data_source");
            string settingInitialCatalog = GridDataMySqlFile.ParseFileReadValue("initial_catalog");
            string settingPersistSecurityInfo = GridDataMySqlFile.ParseFileReadValue("persist_security_info");
            string settingUserId = GridDataMySqlFile.ParseFileReadValue("user_id");
            string settingPassword = GridDataMySqlFile.ParseFileReadValue("password");

            database =
                new MSSQLManager(settingDataSource, settingInitialCatalog, settingPersistSecurityInfo, settingUserId,
                                 settingPassword);
        }

        /// <summary>
        /// Searches the database for a specified user profile by name components
        /// </summary>
        /// <param name="user">The first part of the account name</param>
        /// <param name="last">The second part of the account name</param>
        /// <returns>A user profile</returns>
        public UserProfileData GetUserByName(string user, string last)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["first"] = user;
                    param["second"] = last;

                    IDbCommand result =
                        database.Query("SELECT * FROM users WHERE username = @first AND lastname = @second", param);
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

        public List<Framework.AvatarPickerAvatar> GeneratePickerResults(LLUUID queryID, string query)
        {
            List<Framework.AvatarPickerAvatar> returnlist = new List<Framework.AvatarPickerAvatar>();
            string[] querysplit;
            querysplit = query.Split(' ');
            if (querysplit.Length == 2)
            {
                try
                {
                    lock (database)
                    {
                        Dictionary<string, string> param = new Dictionary<string, string>();
                        param["first"] = querysplit[0];
                        param["second"] = querysplit[1];

                        IDbCommand result =
                            database.Query(
                                "SELECT UUID,username,surname FROM users WHERE username = @first AND lastname = @second",
                                param);
                        IDataReader reader = result.ExecuteReader();


                        while (reader.Read())
                        {
                            Framework.AvatarPickerAvatar user = new Framework.AvatarPickerAvatar();
                            user.AvatarID = new LLUUID((string) reader["UUID"]);
                            user.firstName = (string) reader["username"];
                            user.lastName = (string) reader["surname"];
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
                        param["first"] = querysplit[0];
                        param["second"] = querysplit[1];

                        IDbCommand result =
                            database.Query(
                                "SELECT UUID,username,surname FROM users WHERE username = @first OR lastname = @second",
                                param);
                        IDataReader reader = result.ExecuteReader();


                        while (reader.Read())
                        {
                            Framework.AvatarPickerAvatar user = new Framework.AvatarPickerAvatar();
                            user.AvatarID = new LLUUID((string) reader["UUID"]);
                            user.firstName = (string) reader["username"];
                            user.lastName = (string) reader["surname"];
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

        // See IUserData
        public UserProfileData GetUserByUUID(LLUUID uuid)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["uuid"] = uuid.ToString();

                    IDbCommand result = database.Query("SELECT * FROM users WHERE UUID = @uuid", param);
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
                    param["uuid"] = uuid.ToString();

                    IDbCommand result = database.Query("SELECT * FROM agents WHERE UUID = @uuid", param);
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


        public bool UpdateUserProfile(UserProfileData user)
        {
            SqlCommand command = new SqlCommand("UPDATE users set UUID = @uuid, " +
                                                "username = @username, " +
                                                "lastname = @lastname," +
                                                "passwordHash = @passwordHash," +
                                                "passwordSalt = @passwordSalt," +
                                                "homeRegion = @homeRegion," +
                                                "homeLocationX = @homeLocationX," +
                                                "homeLocationY = @homeLocationY," +
                                                "homeLocationZ = @homeLocationZ," +
                                                "homeLookAtX = @homeLookAtX," +
                                                "homeLookAtY = @homeLookAtY," +
                                                "homeLookAtZ = @homeLookAtZ," +
                                                "created = @created," +
                                                "lastLogin = @lastLogin," +
                                                "userInventoryURI = @userInventoryURI," +
                                                "userAssetURI = @userAssetURI," +
                                                "profileCanDoMask = @profileCanDoMask," +
                                                "profileWantDoMask = @profileWantDoMask," +
                                                "profileAboutText = @profileAboutText," +
                                                "profileFirstText = @profileFirstText," +
                                                "profileImage = @profileImage," +
                                                "profileFirstImage = @profileFirstImage where " +
                                                "UUID = @keyUUUID;", database.getConnection());
            SqlParameter param1 = new SqlParameter("@uuid", user.UUID.ToString());
            SqlParameter param2 = new SqlParameter("@username", user.username);
            SqlParameter param3 = new SqlParameter("@lastname", user.surname);
            SqlParameter param4 = new SqlParameter("@passwordHash", user.passwordHash);
            SqlParameter param5 = new SqlParameter("@passwordSalt", user.passwordSalt);
            SqlParameter param6 = new SqlParameter("@homeRegion", Convert.ToInt64(user.homeRegion));
            SqlParameter param7 = new SqlParameter("@homeLocationX", user.homeLocation.X);
            SqlParameter param8 = new SqlParameter("@homeLocationY", user.homeLocation.Y);
            SqlParameter param9 = new SqlParameter("@homeLocationZ", user.homeLocation.Y);
            SqlParameter param10 = new SqlParameter("@homeLookAtX", user.homeLookAt.X);
            SqlParameter param11 = new SqlParameter("@homeLookAtY", user.homeLookAt.Y);
            SqlParameter param12 = new SqlParameter("@homeLookAtZ", user.homeLookAt.Z);
            SqlParameter param13 = new SqlParameter("@created", Convert.ToInt32(user.created));
            SqlParameter param14 = new SqlParameter("@lastLogin", Convert.ToInt32(user.lastLogin));
            SqlParameter param15 = new SqlParameter("@userInventoryURI", user.userInventoryURI);
            SqlParameter param16 = new SqlParameter("@userAssetURI", user.userAssetURI);
            SqlParameter param17 = new SqlParameter("@profileCanDoMask", Convert.ToInt32(user.profileCanDoMask));
            SqlParameter param18 = new SqlParameter("@profileWantDoMask", Convert.ToInt32(user.profileWantDoMask));
            SqlParameter param19 = new SqlParameter("@profileAboutText", user.profileAboutText);
            SqlParameter param20 = new SqlParameter("@profileFirstText", user.profileFirstText);
            SqlParameter param21 = new SqlParameter("@profileImage", LLUUID.Zero.ToString());
            SqlParameter param22 = new SqlParameter("@profileFirstImage", LLUUID.Zero.ToString());
            SqlParameter param23 = new SqlParameter("@keyUUUID", user.UUID.ToString());
            command.Parameters.Add(param1);
            command.Parameters.Add(param2);
            command.Parameters.Add(param3);
            command.Parameters.Add(param4);
            command.Parameters.Add(param5);
            command.Parameters.Add(param6);
            command.Parameters.Add(param7);
            command.Parameters.Add(param8);
            command.Parameters.Add(param9);
            command.Parameters.Add(param10);
            command.Parameters.Add(param11);
            command.Parameters.Add(param12);
            command.Parameters.Add(param13);
            command.Parameters.Add(param14);
            command.Parameters.Add(param15);
            command.Parameters.Add(param16);
            command.Parameters.Add(param17);
            command.Parameters.Add(param18);
            command.Parameters.Add(param19);
            command.Parameters.Add(param20);
            command.Parameters.Add(param21);
            command.Parameters.Add(param22);
            command.Parameters.Add(param23);
            try
            {
                int affected = command.ExecuteNonQuery();
                if (affected != 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                MainLog.Instance.Error(e.ToString());
            }
            return false;
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
            return "MSSQL Userdata Interface";
        }

        /// <summary>
        /// Database provider version
        /// </summary>
        /// <returns>provider version</returns>
        public string GetVersion()
        {
            return database.getVersion();
        }

        /// <summary>
        /// Not implemented
        /// </summary>
        /// <param name="query"></param>
        public void runQuery(string query)
        {
        }
    }
}