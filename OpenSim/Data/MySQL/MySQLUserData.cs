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
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using libsecondlife;
using log4net;
using OpenSim.Framework;
using OpenSim.Data.Base;
using MySql.Data.MySqlClient;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A database interface class to a user profile storage system
    /// </summary>
    internal class MySQLUserData : UserDataBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Database manager for MySQL
        /// </summary>
        public MySQLManager database;

        private string m_agentsTableName;
        private string m_usersTableName;
        private string m_userFriendsTableName;
        private string m_appearanceTableName = "avatarappearance";
        private string m_connectString;

        public override void Initialise()
        {
            m_log.Info("[MySQLUserData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException (Name);
        }

        /// <summary>
        /// Initialise User Interface
        /// Loads and initialises the MySQL storage plugin
        /// Warns and uses the obsolete mysql_connection.ini if connect string is empty.
        /// Checks for migration
        /// </summary>
        /// <param name="connect">connect string.</param>
        override public void Initialise(string connect)
        {
            if (connect == String.Empty) {
                // TODO: actually do something with our connect string
                // instead of loading the second config

                m_log.Warn("Using obsoletely mysql_connection.ini, try using user_source connect string instead");
                IniFile iniFile = new IniFile("mysql_connection.ini");
                string settingHostname = iniFile.ParseFileReadValue("hostname");
                string settingDatabase = iniFile.ParseFileReadValue("database");
                string settingUsername = iniFile.ParseFileReadValue("username");
                string settingPassword = iniFile.ParseFileReadValue("password");
                string settingPooling = iniFile.ParseFileReadValue("pooling");
                string settingPort = iniFile.ParseFileReadValue("port");

                m_usersTableName = iniFile.ParseFileReadValue("userstablename");
                if (m_usersTableName == null)
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

                m_connectString = "Server=" + settingHostname + ";Port=" + settingPort + ";Database=" + settingDatabase + ";User ID=" +
                    settingUsername + ";Password=" + settingPassword + ";Pooling=" + settingPooling + ";";

                database = new MySQLManager(m_connectString);
            }
            else
            {
                m_connectString = connect;
                m_agentsTableName = "agents";
                m_usersTableName = "users";
                m_userFriendsTableName = "userfriends";
                database = new MySQLManager(m_connectString);
            }

            // This actually does the roll forward assembly stuff
            Assembly assem = GetType().Assembly;
            Migration m = new Migration(database.Connection, assem, "UserStore");

            // TODO: After rev 6000, remove this.  People should have
            // been rolled onto the new migration code by then.
            TestTables(m);

            m.Update();
        }

        public override void Dispose () { }


        #region Test and initialization code

        /// <summary>
        /// Ensure that the user related tables exists and are at the latest version
        /// </summary>
        private void TestTables(Migration m)
        {
            Dictionary<string, string> tableList = new Dictionary<string, string>();

            tableList[m_agentsTableName] = null;
            tableList[m_usersTableName] = null;
            tableList[m_userFriendsTableName] = null;
            tableList[m_appearanceTableName] = null;
            database.GetTableVersion(tableList);

            // if we've already started using migrations, get out of
            // here, we've got this under control
            if (m.Version > 0)
                return;

            // if there are no tables, get out of here and let
            // migrations do their job
            if (
               tableList[m_agentsTableName] == null &&
               tableList[m_usersTableName] == null &&
               tableList[m_userFriendsTableName] == null &&
               tableList[m_appearanceTableName] == null
               )
                return;

            // otherwise, let the upgrade on legacy proceed...
            UpgradeAgentsTable(tableList[m_agentsTableName]);
            UpgradeUsersTable(tableList[m_usersTableName]);
            UpgradeFriendsTable(tableList[m_userFriendsTableName]);
            UpgradeAppearanceTable(tableList[m_appearanceTableName]);

            // ... and set the version
            if (m.Version == 0)
                m.Version = 1;
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

        /// <summary>
        /// Create or upgrade the table if necessary
        /// </summary>
        /// <param name="oldVersion">A null indicates that the table does not
        /// currently exist</param>
        private void UpgradeAppearanceTable(string oldVersion)
        {
            // null as the version, indicates that the table didn't exist
            if (oldVersion == null)
            {
                database.ExecuteResourceSql("CreateAvatarAppearance.sql");
                return;
            }
            else if (oldVersion.Contains("Rev.1"))
            {
                database.ExecuteSql("drop table avatarappearance");
                database.ExecuteResourceSql("CreateAvatarAppearance.sql");
                return;
            }
        }

        #endregion

        // see IUserDataPlugin
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

                    reader.Dispose();
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

                    reader.Dispose();
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

        override public void UpdateUserCurrentRegion(LLUUID avatarid, LLUUID regionuuid, ulong regionhandle)
        {
            //m_log.Info("[USER DB]: Stub UpdateUserCUrrentRegion called");
        }

        override public List<AvatarPickerAvatar> GeneratePickerResults(LLUUID queryID, string query)
        {
            List<AvatarPickerAvatar> returnlist = new List<AvatarPickerAvatar>();

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
                            AvatarPickerAvatar user = new AvatarPickerAvatar();
                            user.AvatarID = new LLUUID((string) reader["UUID"]);
                            user.firstName = (string) reader["username"];
                            user.lastName = (string) reader["lastname"];
                            returnlist.Add(user);
                        }
                        reader.Dispose();
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
                            AvatarPickerAvatar user = new AvatarPickerAvatar();
                            user.AvatarID = new LLUUID((string) reader["UUID"]);
                            user.firstName = (string) reader["username"];
                            user.lastName = (string) reader["lastname"];
                            returnlist.Add(user);
                        }
                        reader.Dispose();
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

        /// <summary>
        /// See IUserDataPlugin
        /// </summary>
        /// <param name="uuid">User UUID</param>
        /// <returns>User profile data</returns>
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

                    reader.Dispose();
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
        /// <param name="name">The account name : "Username Lastname"</param>
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

        /// <summary>
        /// </summary>
        /// <param name="AgentID"></param>
        /// <param name="WebLoginKey"></param>
        /// <remarks>is it still used ?</remarks>
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

                    reader.Dispose();
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
                                       user.HomeRegion, user.HomeRegionID, user.HomeLocation.X, user.HomeLocation.Y, user.HomeLocation.Z, user.HomeLookAt.X,
                                       user.HomeLookAt.Y, user.HomeLookAt.Z, user.Created, user.LastLogin, user.UserInventoryURI,
                                       user.UserAssetURI, user.CanDoMask, user.WantDoMask, user.AboutText,
                                       user.FirstLifeAboutText, user.Image, user.FirstLifeImage, user.WebLoginKey, user.UserFlags, user.GodLevel, user.CustomType, user.Partner);
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
        /// Appearance
        /// TODO: stubs for now to get us to a compiling state gently
        /// override
        /// </summary>
        override public AvatarAppearance GetUserAppearance(LLUUID user)
        {
            try {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?owner"] = user.ToString();

                    IDbCommand result = database.Query("SELECT * FROM " + m_appearanceTableName + " WHERE owner = ?owner", param);
                    IDataReader reader = result.ExecuteReader();

                    AvatarAppearance appearance = database.readAppearanceRow(reader);

                    reader.Dispose();
                    result.Dispose();

                    appearance.SetAttachments(GetUserAttachments(user));

                    return appearance;
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
        /// Updates an avatar appearence
        /// </summary>
        /// <param name="user">The user UUID</param>
        /// <param name="appearance">The avatar appearance</param>
        // override
        override public void UpdateUserAppearance(LLUUID user, AvatarAppearance appearance)
        {
            try
            {
                lock (database)
                {
                    appearance.Owner = user;
                    database.insertAppearanceRow(appearance);

                    UpdateUserAttachments(user, appearance.GetAttachments());
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
            }
        }

        /// <summary>
        /// Adds an attachment item to a user
        /// </summary>
        /// <param name="user">the user UUID</param>
        /// <param name="item">the item UUID</param>
        override public void AddAttachment(LLUUID user, LLUUID item)
        {
            return;
        }

        /// <summary>
        /// Removes an attachment from a user
        /// </summary>
        /// <param name="user">the user UUID</param>
        /// <param name="item">the item UUID</param>
        override public void RemoveAttachment(LLUUID user, LLUUID item)
        {
            return;
        }

        /// <summary>
        /// Get the list of item attached to a user
        /// </summary>
        /// <param name="user">the user UUID</param>
        /// <returns>UUID list of attached item</returns>
        override public List<LLUUID> GetAttachments(LLUUID user)
        {
            return new List<LLUUID>();
        }

        /// <summary>
        /// Database provider name
        /// </summary>
        /// <returns>Provider name</returns>
        override public string Name
        {
            get {return "MySQL Userdata Interface";}
        }

        /// <summary>
        /// Database provider version
        /// </summary>
        /// <returns>provider version</returns>
        override public string Version
        {
            get {return "0.1";}
        }

        public Hashtable GetUserAttachments(LLUUID agentID)
        {
            MySqlCommand cmd = (MySqlCommand) (database.Connection.CreateCommand());
            cmd.CommandText = "select attachpoint, item, asset from avatarattachments where UUID = ?uuid";
            cmd.Parameters.AddWithValue("?uuid", agentID.ToString());

            IDataReader r = cmd.ExecuteReader();

            Hashtable ret =  database.readAttachments(r);

            r.Close();

            return ret;
        }

        public void UpdateUserAttachments(LLUUID agentID, Hashtable data)
        {
            database.writeAttachments(agentID, data);
        }
    }
}
