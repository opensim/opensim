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
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Console;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A database interface class to a user profile storage system
    /// </summary>
    internal class MySQLUserData : UserDataBase
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Database manager for MySQL
        /// </summary>
        public MySQLManager database;

        private string m_agentsTableName;
        private string m_usersTableName;
        private string m_userFriendsTableName;

        /// <summary>
        /// Loads and initialises the MySQL storage plugin
        /// </summary>
        override public void Initialise()
        {
            // Load from an INI file connection details
            // TODO: move this to XML? Yes, PLEASE!
            
            IniFile iniFile = new IniFile("mysql_connection.ini");
            string settingHostname = iniFile.ParseFileReadValue("hostname");
            string settingDatabase = iniFile.ParseFileReadValue("database");
            string settingUsername = iniFile.ParseFileReadValue("username");
            string settingPassword = iniFile.ParseFileReadValue("password");
            string settingPooling = iniFile.ParseFileReadValue("pooling");
            string settingPort = iniFile.ParseFileReadValue("port");
            
            m_usersTableName = iniFile.ParseFileReadValue("userstablename");
            if( m_usersTableName == null )
            {
                m_usersTableName = "users";
            }

            m_userFriendsTableName = iniFile.ParseFileReadValue("userfriendstablename");
            if (m_userFriendsTableName == null)
            {
                m_userFriendsTableName = "userfriends";
            }

            m_agentsTableName = iniFile.ParseFileReadValue("agentstablename");
            if (m_agentsTableName == null)
            {
                m_agentsTableName = "agents";
            }

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

            tableList[m_agentsTableName] = null;
            tableList[m_usersTableName] = null;
            tableList[m_userFriendsTableName] = null;
            database.GetTableVersion(tableList);

            UpgradeAgentsTable(tableList[m_agentsTableName]);
            UpgradeUsersTable(tableList[m_usersTableName]);
            UpgradeFriendsTable(tableList[m_userFriendsTableName]);
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
            else if (oldVersion.Contains("Rev. 1"))
            {
                database.ExecuteResourceSql("UpgradeUsersTableToVersion2.sql");
                return;
            }
            //m_log.Info("[DB]: DBVers:" + oldVersion);
        }

        /// <summary>
        /// Create or upgrade the table if necessary
        /// </summary>
        /// <param name="oldVersion">A null indicates that the table does not
        /// currently exist</param>
        private void UpgradeFriendsTable(string oldVersion)
        {
            // null as the version, indicates that the table didn't exist
            if (oldVersion == null)
            {
                database.ExecuteResourceSql("CreateUserFriendsTable.sql");
                return;
            }
        }

        #endregion

        // see IUserData
        override public UserProfileData GetUserByName(string user, string last)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?first"] = user;
                    param["?second"] = last;

                    IDbCommand result =
                        database.Query("SELECT * FROM " + m_usersTableName + " WHERE username = ?first AND lastname = ?second", param);
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
                m_log.Error(e.ToString());
                return null;
            }
        }

        #region User Friends List Data

        override public void AddNewUserFriend(LLUUID friendlistowner, LLUUID friend, uint perms)
        {
            int dtvalue = Util.UnixTimeSinceEpoch();

            Dictionary<string, string> param = new Dictionary<string, string>();
            param["?ownerID"] = friendlistowner.UUID.ToString();
            param["?friendID"] = friend.UUID.ToString();
            param["?friendPerms"] = perms.ToString();
            param["?datetimestamp"] = dtvalue.ToString();
            
            try 
            {
                lock (database)
                {
                    IDbCommand adder =
                        database.Query(
                        "INSERT INTO `" + m_userFriendsTableName + "` " +
                        "(`ownerID`,`friendID`,`friendPerms`,`datetimestamp`) " + 
                        "VALUES " +
                        "(?ownerID,?friendID,?friendPerms,?datetimestamp)",
                            param);
                    adder.ExecuteNonQuery();

                    adder =
                        database.Query(
                        "INSERT INTO `" + m_userFriendsTableName + "` " +
                        "(`ownerID`,`friendID`,`friendPerms`,`datetimestamp`) " +
                        "VALUES " +
                        "(?friendID,?ownerID,?friendPerms,?datetimestamp)",
                            param);
                    adder.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
                return;
            }
        }

        override public void RemoveUserFriend(LLUUID friendlistowner, LLUUID friend)
        {
            Dictionary<string, string> param = new Dictionary<string, string>();
            param["?ownerID"] = friendlistowner.UUID.ToString();
            param["?friendID"] = friend.UUID.ToString();

            try
            {
                lock (database)
                {
                    IDbCommand updater =
                        database.Query(
                            "delete from " + m_userFriendsTableName + " where ownerID = ?ownerID and friendID = ?friendID",
                            param);
                    updater.ExecuteNonQuery();

                    updater =
                        database.Query(
                            "delete from " + m_userFriendsTableName + " where ownerID = ?friendID and friendID = ?ownerID",
                            param);
                    updater.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
                return;
            }
        }

        override public void UpdateUserFriendPerms(LLUUID friendlistowner, LLUUID friend, uint perms)
        {
            Dictionary<string, string> param = new Dictionary<string, string>();
            param["?ownerID"] = friendlistowner.UUID.ToString();
            param["?friendID"] = friend.UUID.ToString();
            param["?friendPerms"] = perms.ToString();

            try
            {
                lock (database)
                {
                    IDbCommand updater =
                        database.Query(
                            "update " + m_userFriendsTableName +
                            " SET friendPerms = ?friendPerms " +
                            "where ownerID = ?ownerID and friendID = ?friendID",
                            param);
                    updater.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
                return;
            }
        }

        override public List<FriendListItem> GetUserFriendList(LLUUID friendlistowner)
        {
            List<FriendListItem> Lfli = new List<FriendListItem>();

            Dictionary<string, string> param = new Dictionary<string, string>();
            param["?ownerID"] = friendlistowner.UUID.ToString();

            try
            {
                lock (database)
                {
                    //Left Join userfriends to itself
                    IDbCommand result =
                        database.Query(
                            "select a.ownerID,a.friendID,a.friendPerms,b.friendPerms as ownerperms from " + m_userFriendsTableName + " as a, " + m_userFriendsTableName + " as b" +
                            " where a.ownerID = ?ownerID and b.ownerID = a.friendID and b.friendID = a.ownerID",
                            param);
                    IDataReader reader = result.ExecuteReader();

                    while (reader.Read())
                    {
                        FriendListItem fli = new FriendListItem();
                        fli.FriendListOwner = new LLUUID((string)reader["ownerID"]);
                        fli.Friend = new LLUUID((string)reader["friendID"]);
                        fli.FriendPerms = (uint)Convert.ToInt32(reader["friendPerms"]);

                        // This is not a real column in the database table, it's a joined column from the opposite record
                        fli.FriendListOwnerPerms = (uint)Convert.ToInt32(reader["ownerperms"]);
                        
                        Lfli.Add(fli);
                    }
                    reader.Close();
                    result.Dispose();
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
                return Lfli;
            }

            return Lfli;
        }

        #endregion

        override public void UpdateUserCurrentRegion(LLUUID avatarid, LLUUID regionuuid)
        {
            m_log.Info("[USER]: Stub UpdateUserCUrrentRegion called");
        }

        override public List<Framework.AvatarPickerAvatar> GeneratePickerResults(LLUUID queryID, string query)
        {
            List<Framework.AvatarPickerAvatar> returnlist = new List<Framework.AvatarPickerAvatar>();

            Regex objAlphaNumericPattern = new Regex("[^a-zA-Z0-9]");

            string[] querysplit;
            querysplit = query.Split(' ');
            if (querysplit.Length == 2)
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["?first"] = objAlphaNumericPattern.Replace(querysplit[0], String.Empty) + "%";
                param["?second"] = objAlphaNumericPattern.Replace(querysplit[1], String.Empty) + "%";
                try
                {
                    lock (database)
                    {
                        IDbCommand result =
                            database.Query(
                                "SELECT UUID,username,lastname FROM " + m_usersTableName + " WHERE username like ?first AND lastname like ?second LIMIT 100",
                                param);
                        IDataReader reader = result.ExecuteReader();

                        while (reader.Read())
                        {
                            Framework.AvatarPickerAvatar user = new Framework.AvatarPickerAvatar();
                            user.AvatarID = new LLUUID((string) reader["UUID"]);
                            user.firstName = (string) reader["username"];
                            user.lastName = (string) reader["lastname"];
                            returnlist.Add(user);
                        }
                        reader.Close();
                        result.Dispose();
                    }
                }
                catch (Exception e)
                {
                    database.Reconnect();
                    m_log.Error(e.ToString());
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
                        param["?first"] = objAlphaNumericPattern.Replace(querysplit[0], String.Empty) + "%";

                        IDbCommand result =
                            database.Query(
                                "SELECT UUID,username,lastname FROM " + m_usersTableName + " WHERE username like ?first OR lastname like ?first LIMIT 100",
                                param);
                        IDataReader reader = result.ExecuteReader();

                        while (reader.Read())
                        {
                            Framework.AvatarPickerAvatar user = new Framework.AvatarPickerAvatar();
                            user.AvatarID = new LLUUID((string) reader["UUID"]);
                            user.firstName = (string) reader["username"];
                            user.lastName = (string) reader["lastname"];
                            returnlist.Add(user);
                        }
                        reader.Close();
                        result.Dispose();
                    }
                }
                catch (Exception e)
                {
                    database.Reconnect();
                    m_log.Error(e.ToString());
                    return returnlist;
                }
            }
            return returnlist;
        }

        // see IUserData
        override public UserProfileData GetUserByUUID(LLUUID uuid)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?uuid"] = uuid.ToString();

                    IDbCommand result = database.Query("SELECT * FROM " + m_usersTableName + " WHERE UUID = ?uuid", param);
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
                m_log.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Returns a user session searching by name
        /// </summary>
        /// <param name="name">The account name</param>
        /// <returns>The users session</returns>
        override public UserAgentData GetAgentByName(string name)
        {
            return GetAgentByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        /// <summary>
        /// Returns a user session by account name
        /// </summary>
        /// <param name="user">First part of the users account name</param>
        /// <param name="last">Second part of the users account name</param>
        /// <returns>The users session</returns>
        override public UserAgentData GetAgentByName(string user, string last)
        {
            UserProfileData profile = GetUserByName(user, last);
            return GetAgentByUUID(profile.ID);
        }

        override public void StoreWebLoginKey(LLUUID AgentID, LLUUID WebLoginKey)
        {
            Dictionary<string, string> param = new Dictionary<string, string>();
            param["?UUID"] = AgentID.UUID.ToString();
            param["?webLoginKey"] = WebLoginKey.UUID.ToString();

            try
            {
                lock (database)
                {
                    IDbCommand updater =
                        database.Query(
                            "update " + m_usersTableName + " SET webLoginKey = ?webLoginKey " +
                            "where UUID = ?UUID",
                            param);
                    updater.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
                return;
            }
        }

        /// <summary>
        /// Returns an agent session by account UUID
        /// </summary>
        /// <param name="uuid">The accounts UUID</param>
        /// <returns>The users session</returns>
        override public UserAgentData GetAgentByUUID(LLUUID uuid)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?uuid"] = uuid.ToString();

                    IDbCommand result = database.Query("SELECT * FROM " + m_agentsTableName + " WHERE UUID = ?uuid", param);
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
                m_log.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Creates a new users profile
        /// </summary>
        /// <param name="user">The user profile to create</param>
        override public void AddNewUserProfile(UserProfileData user)
        {
            try
            {
                lock (database)
                {
                    database.insertUserRow(user.ID, user.FirstName, user.SurName, user.PasswordHash, user.PasswordSalt,
                                           user.HomeRegion, user.HomeLocation.X, user.HomeLocation.Y,
                                           user.HomeLocation.Z,
                                           user.HomeLookAt.X, user.HomeLookAt.Y, user.HomeLookAt.Z, user.Created,
                                           user.LastLogin, user.UserInventoryURI, user.UserAssetURI,
                                           user.CanDoMask, user.WantDoMask,
                                           user.AboutText, user.FirstLifeAboutText, user.Image,
                                           user.FirstLifeImage, user.WebLoginKey);
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
            }
        }

        /// <summary>
        /// Creates a new agent
        /// </summary>
        /// <param name="agent">The agent to create</param>
        override public void AddNewUserAgent(UserAgentData agent)
        {
            try
            {
                lock (database)
                {
                    database.insertAgentRow(agent);
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
            }
        }

        /// <summary>
        /// Updates a user profile stored in the DB
        /// </summary>
        /// <param name="user">The profile data to use to update the DB</param>
        override public bool UpdateUserProfile(UserProfileData user)
        {
            lock (database)
            {
                database.updateUserRow(user.ID, user.FirstName, user.SurName, user.PasswordHash, user.PasswordSalt,
                                       user.HomeRegion, user.HomeLocation.X, user.HomeLocation.Y, user.HomeLocation.Z, user.HomeLookAt.X,
                                       user.HomeLookAt.Y, user.HomeLookAt.Z, user.Created, user.LastLogin, user.UserInventoryURI,
                                       user.UserAssetURI, user.CanDoMask, user.WantDoMask, user.AboutText,
                                       user.FirstLifeAboutText, user.Image, user.FirstLifeImage, user.WebLoginKey);
            }
            
            return true;
        }

        /// <summary>
        /// Performs a money transfer request between two accounts
        /// </summary>
        /// <param name="from">The senders account ID</param>
        /// <param name="to">The receivers account ID</param>
        /// <param name="amount">The amount to transfer</param>
        /// <returns>Success?</returns>
        override public bool MoneyTransferRequest(LLUUID from, LLUUID to, uint amount)
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
        override public bool InventoryTransferRequest(LLUUID from, LLUUID to, LLUUID item)
        {
            return false;
        }

        /// <summary>
        /// Database provider name
        /// </summary>
        /// <returns>Provider name</returns>
        override public string getName()
        {
            return "MySQL Userdata Interface";
        }

        /// <summary>
        /// Database provider version
        /// </summary>
        /// <returns>provider version</returns>
        override public string GetVersion()
        {
            return "0.1";
        }
    }
}
