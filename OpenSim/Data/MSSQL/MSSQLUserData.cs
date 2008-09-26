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
using System.Data.SqlClient;
using System.Reflection;
using OpenMetaverse;
using log4net;
using OpenSim.Framework;

namespace OpenSim.Data.MSSQL
{
    /// <summary>
    /// A database interface class to a user profile storage system
    /// </summary>
    public class MSSQLUserData : UserDataBase
    {
        private const string _migrationStore = "UserStore";

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Database manager for MSSQL
        /// </summary>
        public MSSQLManager database;

        private string m_agentsTableName;
        private string m_usersTableName;
        private string m_userFriendsTableName;

        override public void Initialise()
        {
            m_log.Info("[MSSQLUserData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException(Name);
        }

        /// <summary>
        /// Loads and initialises the MSSQL storage plugin
        /// </summary>
        /// <param name="connect">connectionstring</param>
        /// <remarks>use mssql_connection.ini</remarks>
        override public void Initialise(string connect)
        {
            IniFile iniFile = new IniFile("mssql_connection.ini");

            if (string.IsNullOrEmpty(connect))
            {
                database = new MSSQLManager(connect);
            }
            else
            {
                string settingDataSource = iniFile.ParseFileReadValue("data_source");
                string settingInitialCatalog = iniFile.ParseFileReadValue("initial_catalog");
                string settingPersistSecurityInfo = iniFile.ParseFileReadValue("persist_security_info");
                string settingUserId = iniFile.ParseFileReadValue("user_id");
                string settingPassword = iniFile.ParseFileReadValue("password");

                database = new MSSQLManager(settingDataSource, settingInitialCatalog, settingPersistSecurityInfo, settingUserId, settingPassword);
            }

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

            //TODO this can be removed at one time!!!!!
            TestTables();

            //Check migration on DB
            database.CheckMigration(_migrationStore);
        }

        override public void Dispose() { }

        /// <summary>
        /// Can be deleted at one time!
        /// </summary>
        /// <returns></returns>
        private void TestTables()
        {
            using (IDbCommand cmd = database.Query("select top 1 * from " + m_usersTableName))
            {
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch
                {
                    database.ExecuteResourceSql("Mssql-users.sql");
                }
            }

            using (IDbCommand cmd = database.Query("select top 1 * from " + m_agentsTableName, new Dictionary<string, string>()))
            {
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch
                {
                    database.ExecuteResourceSql("Mssql-agents.sql");
                }
            }

            using (IDbCommand cmd = database.Query("select top 1 * from " + m_userFriendsTableName, new Dictionary<string, string>()))
            {
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch
                {
                    database.ExecuteResourceSql("CreateUserFriendsTable.sql");
                }
            }

            using (IDbCommand cmd = database.Query("select top 1 * from avatarappearance", new Dictionary<string, string>()))
            {
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch
                {
                    database.ExecuteResourceSql("AvatarAppearance.sql");
                }
            }

            //Special for Migrations
            using (AutoClosingSqlCommand cmd = database.Query("select * from migrations where name = 'UserStore'"))
            {
                try
                {
                    bool insert = true;
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read()) insert = false;
                    }
                    if (insert)
                    {
                        cmd.CommandText = "insert into migrations(name, version) values('UserStore', 1)";
                        cmd.ExecuteNonQuery();
                    }
                }
                catch
                {
                    //No migrations table
                    //HACK create one and add data
                    cmd.CommandText = "create table migrations(name varchar(100), version int)";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "insert into migrations(name, version) values('migrations', 1)";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "insert into migrations(name, version) values('UserStore', 1)";
                    cmd.ExecuteNonQuery();
                }
            }
            return;
        }

        #region User table methods

        /// <summary>
        /// Searches the database for a specified user profile by name components
        /// </summary>
        /// <param name="user">The first part of the account name</param>
        /// <param name="last">The second part of the account name</param>
        /// <returns>A user profile</returns>
        override public UserProfileData GetUserByName(string user, string last)
        {
            using (AutoClosingSqlCommand command = database.Query("SELECT * FROM " + m_usersTableName + " WHERE username = @first AND lastname = @second"))
            {
                command.Parameters.Add(database.CreateParameter("first", user));
                command.Parameters.Add(database.CreateParameter("second", last));

                try
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        return ReadUserRow(reader);
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[USER DB] Error getting user profile, error: " + e.Message);
                    return null;
                }
            }
        }

        /// <summary>
        /// See IUserDataPlugin
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        override public UserProfileData GetUserByUUID(UUID uuid)
        {
            using (AutoClosingSqlCommand command = database.Query("SELECT * FROM " + m_usersTableName + " WHERE UUID = @uuid"))
            {
                command.Parameters.Add(database.CreateParameter("uuid", uuid));

                try
                {
                    using (IDataReader reader = command.ExecuteReader())
                    {
                        return ReadUserRow(reader);
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[USER DB] Error getting user profile by UUID, error: " + e.Message);
                    return null;
                }
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
                InsertUserRow(user.ID, user.FirstName, user.SurName, user.PasswordHash, user.PasswordSalt,
                              user.HomeRegion, user.HomeLocation.X, user.HomeLocation.Y,
                              user.HomeLocation.Z,
                              user.HomeLookAt.X, user.HomeLookAt.Y, user.HomeLookAt.Z, user.Created,
                              user.LastLogin, user.UserInventoryURI, user.UserAssetURI,
                              user.CanDoMask, user.WantDoMask,
                              user.AboutText, user.FirstLifeAboutText, user.Image,
                              user.FirstLifeImage, user.WebLoginKey, user.HomeRegionID,
                              user.GodLevel, user.UserFlags, user.CustomType, user.Partner);
            }
            catch (Exception e)
            {
                m_log.Error("[USER DB] Error adding new profile, error: " + e.Message);
            }
        }

        /// <summary>
        /// update a user profile
        /// </summary>
        /// <param name="user">the profile to update</param>
        /// <returns></returns>
        override public bool UpdateUserProfile(UserProfileData user)
        {
            using (AutoClosingSqlCommand command = database.Query("UPDATE " + m_usersTableName + " set UUID = @uuid, " +
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
                                                                  "profileFirstImage = @profileFirstImage, " +
                                                                  "webLoginKey = @webLoginKey, " +
                                                                  "homeRegionID = @homeRegionID, " +
                                                                  "userFlags = @userFlags, " +
                                                                  "godLevel = @godLevel, " +
                                                                  "customType = @customType, " +
                                                                  "partner = @partner where " +
                                                                  "UUID = @keyUUUID;"))
            {
                command.Parameters.Add(database.CreateParameter("uuid", user.ID));
                command.Parameters.Add(database.CreateParameter("username", user.FirstName));
                command.Parameters.Add(database.CreateParameter("lastname", user.SurName));
                command.Parameters.Add(database.CreateParameter("passwordHash", user.PasswordHash));
                command.Parameters.Add(database.CreateParameter("passwordSalt", user.PasswordSalt));
                command.Parameters.Add(database.CreateParameter("homeRegion", user.HomeRegion));
                command.Parameters.Add(database.CreateParameter("homeLocationX", user.HomeLocation.X));
                command.Parameters.Add(database.CreateParameter("homeLocationY", user.HomeLocation.Y));
                command.Parameters.Add(database.CreateParameter("homeLocationZ", user.HomeLocation.Z));
                command.Parameters.Add(database.CreateParameter("homeLookAtX", user.HomeLookAt.X));
                command.Parameters.Add(database.CreateParameter("homeLookAtY", user.HomeLookAt.Y));
                command.Parameters.Add(database.CreateParameter("homeLookAtZ", user.HomeLookAt.Z));
                command.Parameters.Add(database.CreateParameter("created", user.Created));
                command.Parameters.Add(database.CreateParameter("lastLogin", user.LastLogin));
                command.Parameters.Add(database.CreateParameter("userInventoryURI", user.UserInventoryURI));
                command.Parameters.Add(database.CreateParameter("userAssetURI", user.UserAssetURI));
                command.Parameters.Add(database.CreateParameter("profileCanDoMask", user.CanDoMask));
                command.Parameters.Add(database.CreateParameter("profileWantDoMask", user.WantDoMask));
                command.Parameters.Add(database.CreateParameter("profileAboutText", user.AboutText));
                command.Parameters.Add(database.CreateParameter("profileFirstText", user.FirstLifeAboutText));
                command.Parameters.Add(database.CreateParameter("profileImage", user.Image));
                command.Parameters.Add(database.CreateParameter("profileFirstImage", user.FirstLifeImage));
                command.Parameters.Add(database.CreateParameter("webLoginKey", user.WebLoginKey));
                //
                command.Parameters.Add(database.CreateParameter("homeRegionID", user.HomeRegionID));
                command.Parameters.Add(database.CreateParameter("userFlags", user.UserFlags));
                command.Parameters.Add(database.CreateParameter("godLevel", user.GodLevel));
                command.Parameters.Add(database.CreateParameter("customType", user.CustomType));
                command.Parameters.Add(database.CreateParameter("partner", user.Partner));
                command.Parameters.Add(database.CreateParameter("keyUUUID", user.ID));

                try
                {
                    int affected = command.ExecuteNonQuery();
                    return (affected != 0);
                }
                catch (Exception e)
                {
                    m_log.Error("[USER DB] Error updating profile, error: " + e.Message);
                }
            }
            return false;
        }

        #endregion

        #region Agent table methods

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

        /// <summary>
        /// Returns an agent session by account UUID
        /// </summary>
        /// <param name="uuid">The accounts UUID</param>
        /// <returns>The users session</returns>
        override public UserAgentData GetAgentByUUID(UUID uuid)
        {
            using (AutoClosingSqlCommand command = database.Query("SELECT * FROM " + m_agentsTableName + " WHERE UUID = @uuid"))
            {
                command.Parameters.Add(database.CreateParameter("uuid", uuid));
                try
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        return readAgentRow(reader);
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[USER DB] Error updating agentdata by UUID, error: " + e.Message);
                    return null;
                }
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
                InsertUpdateAgentRow(agent);
            }
            catch (Exception e)
            {
                m_log.Error("[USER DB] Error adding new agentdata, error: " + e.Message);
            }
        }

        #endregion

        #region User Friends List Data

        /// <summary>
        /// Add a new friend in the friendlist
        /// </summary>
        /// <param name="friendlistowner">UUID of the friendlist owner</param>
        /// <param name="friend">Friend's UUID</param>
        /// <param name="perms">Permission flag</param>
        override public void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms)
        {
            int dtvalue = Util.UnixTimeSinceEpoch();

            using (AutoClosingSqlCommand command = database.Query(
                "INSERT INTO " + m_userFriendsTableName + " " +
                "(ownerID,friendID,friendPerms,datetimestamp) " +
                "VALUES " +
                "(@ownerID,@friendID,@friendPerms,@datetimestamp)"))
            {
                command.Parameters.Add(database.CreateParameter("ownerID", friendlistowner));
                command.Parameters.Add(database.CreateParameter("friendID", friend));
                command.Parameters.Add(database.CreateParameter("friendPerms", perms));
                command.Parameters.Add(database.CreateParameter("datetimestamp", dtvalue));
                command.ExecuteNonQuery();

                try
                {
                    command.CommandText = string.Format("INSERT INTO {0} (ownerID,friendID,friendPerms,datetimestamp) VALUES (@friendID,@ownerID,@friendPerms,@datetimestamp)",
                            m_userFriendsTableName);

                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    m_log.Error("[USER DB] Error adding new userfriend, error: " + e.Message);
                    return;
                }
            }
        }

        /// <summary>
        /// Remove an friend from the friendlist
        /// </summary>
        /// <param name="friendlistowner">UUID of the friendlist owner</param>
        /// <param name="friend">UUID of the not-so-friendly user to remove from the list</param>
        override public void RemoveUserFriend(UUID friendlistowner, UUID friend)
        {
            using (AutoClosingSqlCommand command = database.Query("delete from " + m_userFriendsTableName + " where ownerID = @ownerID and friendID = @friendID"))
            {
                command.Parameters.Add(database.CreateParameter("@ownerID", friendlistowner));
                command.Parameters.Add(database.CreateParameter("@friendID", friend));
                command.ExecuteNonQuery();

                command.CommandText = "delete from " + m_userFriendsTableName +
                                      " where ownerID = @friendID and friendID = @ownerID";
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    m_log.Error("[USER DB] Error removing userfriend, error: " + e.Message);
                }
            }
        }

        /// <summary>
        /// Update friendlist permission flag for a friend
        /// </summary>
        /// <param name="friendlistowner">UUID of the friendlist owner</param>
        /// <param name="friend">UUID of the friend</param>
        /// <param name="perms">new permission flag</param>
        override public void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms)
        {
            using (AutoClosingSqlCommand command = database.Query(
                "update " + m_userFriendsTableName +
                " SET friendPerms = @friendPerms " +
                "where ownerID = @ownerID and friendID = @friendID"))
            {
                command.Parameters.Add(database.CreateParameter("@ownerID", friendlistowner));
                command.Parameters.Add(database.CreateParameter("@friendID", friend));
                command.Parameters.Add(database.CreateParameter("@friendPerms", perms));

                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    m_log.Error("[USER DB] Error updating userfriend, error: " + e.Message);
                }
            }
        }

        /// <summary>
        /// Get (fetch?) the user's friendlist
        /// </summary>
        /// <param name="friendlistowner">UUID of the friendlist owner</param>
        /// <returns>Friendlist list</returns>
        override public List<FriendListItem> GetUserFriendList(UUID friendlistowner)
        {
            List<FriendListItem> friendList = new List<FriendListItem>();

            //Left Join userfriends to itself
            using (AutoClosingSqlCommand command = database.Query(
                "select a.ownerID,a.friendID,a.friendPerms,b.friendPerms as ownerperms from " + m_userFriendsTableName + " as a, " + m_userFriendsTableName + " as b" +
                " where a.ownerID = @ownerID and b.ownerID = a.friendID and b.friendID = a.ownerID"))
            {
                command.Parameters.Add(database.CreateParameter("@ownerID", friendlistowner));

                try
                {
                    using (IDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            FriendListItem fli = new FriendListItem();
                            fli.FriendListOwner = new UUID((string)reader["ownerID"]);
                            fli.Friend = new UUID((string)reader["friendID"]);
                            fli.FriendPerms = (uint)Convert.ToInt32(reader["friendPerms"]);

                            // This is not a real column in the database table, it's a joined column from the opposite record
                            fli.FriendListOwnerPerms = (uint)Convert.ToInt32(reader["ownerperms"]);

                            friendList.Add(fli);
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[USER DB] Error updating userfriend, error: " + e.Message);
                }
            }

            return friendList;
        }

        #endregion

        #region Money functions (not used)

        /// <summary>
        /// Performs a money transfer request between two accounts
        /// </summary>
        /// <param name="from">The senders account ID</param>
        /// <param name="to">The receivers account ID</param>
        /// <param name="amount">The amount to transfer</param>
        /// <returns>false</returns>
        override public bool MoneyTransferRequest(UUID from, UUID to, uint amount)
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
        /// <returns>false</returns>
        override public bool InventoryTransferRequest(UUID from, UUID to, UUID item)
        {
            return false;
        }

        #endregion

        #region Appearance methods

        /// <summary>
        /// Gets the user appearance.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <returns></returns>
        /// TODO: stubs for now to get us to a compiling state gently
        override public AvatarAppearance GetUserAppearance(UUID user)
        {
            try
            {
                AvatarAppearance appearance = new AvatarAppearance();

                using (AutoClosingSqlCommand command = database.Query("SELECT * FROM avatarappearance WHERE owner = @UUID"))
                {
                    command.Parameters.Add(database.CreateParameter("@UUID", user));
                    using (IDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                            appearance = readUserAppearance(reader);
                    }
                }

                appearance.SetAttachments(GetUserAttachments(user));

                return appearance;
            }
            catch (Exception e)
            {
                m_log.Error("[USER DB] Error updating userfriend, error: " + e.Message);
            }
            return null;
        }


        /// <summary>
        /// Update a user appearence into database
        /// </summary>
        /// <param name="user">the used UUID</param>
        /// <param name="appearance">the appearence</param>
        override public void UpdateUserAppearance(UUID user, AvatarAppearance appearance)
        {
            string sql = String.Empty;
            sql += "DELETE FROM avatarappearance WHERE owner=@owner ";
            sql += "INSERT INTO avatarappearance ";
            sql += "(owner, serial, visual_params, texture, avatar_height, ";
            sql += "body_item, body_asset, skin_item, skin_asset, hair_item, hair_asset, eyes_item, eyes_asset, ";
            sql += "shirt_item, shirt_asset, pants_item, pants_asset, shoes_item, shoes_asset, socks_item, socks_asset, ";
            sql += "jacket_item, jacket_asset, gloves_item, gloves_asset, undershirt_item, undershirt_asset, underpants_item, underpants_asset, ";
            sql += "skirt_item, skirt_asset) values (";
            sql += "@owner, @serial, @visual_params, @texture, @avatar_height, ";
            sql += "@body_item, @body_asset, @skin_item, @skin_asset, @hair_item, @hair_asset, @eyes_item, @eyes_asset, ";
            sql += "@shirt_item, @shirt_asset, @pants_item, @pants_asset, @shoes_item, @shoes_asset, @socks_item, @socks_asset, ";
            sql += "@jacket_item, @jacket_asset, @gloves_item, @gloves_asset, @undershirt_item, @undershirt_asset, @underpants_item, @underpants_asset, ";
            sql += "@skirt_item, @skirt_asset)";

            using (AutoClosingSqlCommand cmd = database.Query(sql))
            {
                cmd.Parameters.Add(database.CreateParameter("@owner", appearance.Owner));
                cmd.Parameters.Add(database.CreateParameter("@serial", appearance.Serial));
                cmd.Parameters.Add(database.CreateParameter("@visual_params", appearance.VisualParams));
                cmd.Parameters.Add(database.CreateParameter("@texture", appearance.Texture.ToBytes()));
                cmd.Parameters.Add(database.CreateParameter("@avatar_height", appearance.AvatarHeight));
                cmd.Parameters.Add(database.CreateParameter("@body_item", appearance.BodyItem));
                cmd.Parameters.Add(database.CreateParameter("@body_asset", appearance.BodyAsset));
                cmd.Parameters.Add(database.CreateParameter("@skin_item", appearance.SkinItem));
                cmd.Parameters.Add(database.CreateParameter("@skin_asset", appearance.SkinAsset));
                cmd.Parameters.Add(database.CreateParameter("@hair_item", appearance.HairItem));
                cmd.Parameters.Add(database.CreateParameter("@hair_asset", appearance.HairAsset));
                cmd.Parameters.Add(database.CreateParameter("@eyes_item", appearance.EyesItem));
                cmd.Parameters.Add(database.CreateParameter("@eyes_asset", appearance.EyesAsset));
                cmd.Parameters.Add(database.CreateParameter("@shirt_item", appearance.ShirtItem));
                cmd.Parameters.Add(database.CreateParameter("@shirt_asset", appearance.ShirtAsset));
                cmd.Parameters.Add(database.CreateParameter("@pants_item", appearance.PantsItem));
                cmd.Parameters.Add(database.CreateParameter("@pants_asset", appearance.PantsAsset));
                cmd.Parameters.Add(database.CreateParameter("@shoes_item", appearance.ShoesItem));
                cmd.Parameters.Add(database.CreateParameter("@shoes_asset", appearance.ShoesAsset));
                cmd.Parameters.Add(database.CreateParameter("@socks_item", appearance.SocksItem));
                cmd.Parameters.Add(database.CreateParameter("@socks_asset", appearance.SocksAsset));
                cmd.Parameters.Add(database.CreateParameter("@jacket_item", appearance.JacketItem));
                cmd.Parameters.Add(database.CreateParameter("@jacket_asset", appearance.JacketAsset));
                cmd.Parameters.Add(database.CreateParameter("@gloves_item", appearance.GlovesItem));
                cmd.Parameters.Add(database.CreateParameter("@gloves_asset", appearance.GlovesAsset));
                cmd.Parameters.Add(database.CreateParameter("@undershirt_item", appearance.UnderShirtItem));
                cmd.Parameters.Add(database.CreateParameter("@undershirt_asset", appearance.UnderShirtAsset));
                cmd.Parameters.Add(database.CreateParameter("@underpants_item", appearance.UnderPantsItem));
                cmd.Parameters.Add(database.CreateParameter("@underpants_asset", appearance.UnderPantsAsset));
                cmd.Parameters.Add(database.CreateParameter("@skirt_item", appearance.SkirtItem));
                cmd.Parameters.Add(database.CreateParameter("@skirt_asset", appearance.SkirtAsset));

                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    m_log.Error("[USER DB] Error updating user appearance, error: " + e.Message);
                }
            }

            UpdateUserAttachments(user, appearance.GetAttachments());
        }

        #endregion

        #region Attachment methods

        /// <summary>
        /// Gets all attachment of a agent.
        /// </summary>
        /// <param name="agentID">agent ID.</param>
        /// <returns></returns>
        public Hashtable GetUserAttachments(UUID agentID)
        {
            Hashtable returnTable = new Hashtable();
            using (AutoClosingSqlCommand command = database.Query("select attachpoint, item, asset from avatarattachments where UUID = @uuid", database.CreateParameter("@uuid", agentID)))
            {
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int attachpoint = Convert.ToInt32(reader["attachpoint"]);
                        if (returnTable.ContainsKey(attachpoint))
                            continue;
                        Hashtable item = new Hashtable();
                        item.Add("item", reader["item"].ToString());
                        item.Add("asset", reader["asset"].ToString());

                        returnTable.Add(attachpoint, item);
                    }
                }
            }
            return returnTable;
        }

        /// <summary>
        /// Updates all attachments of the agent.
        /// </summary>
        /// <param name="agentID">agentID.</param>
        /// <param name="data">data with all items on attachmentpoints</param>
        public void UpdateUserAttachments(UUID agentID, Hashtable data)
        {
            string sql = "delete from avatarattachments where UUID = @uuid";

            using (AutoClosingSqlCommand command = database.Query(sql))
            {
                command.Parameters.Add(database.CreateParameter("uuid", agentID));
                command.ExecuteNonQuery();
            }
            if (data == null)
                return;

            sql = "insert into avatarattachments (UUID, attachpoint, item, asset) values (@uuid, @attachpoint, @item, @asset)";

            using (AutoClosingSqlCommand command = database.Query(sql))
            {
                bool firstTime = true;
                foreach (DictionaryEntry e in data)
                {
                    int attachpoint = Convert.ToInt32(e.Key);

                    Hashtable item = (Hashtable)e.Value;

                    if (firstTime)
                    {
                        command.Parameters.Add(database.CreateParameter("@uuid", agentID));
                        command.Parameters.Add(database.CreateParameter("@attachpoint", attachpoint));
                        command.Parameters.Add(database.CreateParameter("@item", item["item"].ToString()));
                        command.Parameters.Add(database.CreateParameter("@asset", item["asset"].ToString()));
                        firstTime = false;
                    }
                    command.Parameters["@uuid"].Value = agentID.ToString();
                    command.Parameters["@attachpoint"].Value = attachpoint;
                    command.Parameters["@item"].Value = item["item"].ToString();
                    command.Parameters["@asset"].Value = item["asset"].ToString();

                    try
                    {
                        command.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        m_log.DebugFormat("[USER DB] : Error adding user attachment. {0}", ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Resets all attachments of a agent in the database.
        /// </summary>
        /// <param name="agentID">agentID.</param>
        override public void ResetAttachments(UUID agentID)
        {
            using (AutoClosingSqlCommand command = database.Query("update avatarattachments set asset = '00000000-0000-0000-0000-000000000000' where UUID = @uuid"))
            {
                command.Parameters.Add(database.CreateParameter("uuid", agentID));
                command.ExecuteNonQuery();
            }
        }

        #endregion

        #region Other public methods

        /// <summary>
        /// STUB ! Update current region
        /// </summary>
        /// <param name="avatarid">avatar uuid</param>
        /// <param name="regionuuid">region uuid</param>
        /// <param name="regionhandle">region handle</param>
        override public void UpdateUserCurrentRegion(UUID avatarid, UUID regionuuid, ulong regionhandle)
        {
            //m_log.Info("[USER]: Stub UpdateUserCUrrentRegion called");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="queryID"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        override public List<AvatarPickerAvatar> GeneratePickerResults(UUID queryID, string query)
        {
            List<AvatarPickerAvatar> returnlist = new List<AvatarPickerAvatar>();
            string[] querysplit = query.Split(' ');
            if (querysplit.Length == 2)
            {
                try
                {
                    using (AutoClosingSqlCommand command = database.Query("SELECT UUID,username,lastname FROM " + m_usersTableName + " WHERE username LIKE @first AND lastname LIKE @second"))
                    {
                        //Add wildcard to the search
                        command.Parameters.Add(database.CreateParameter("first", querysplit[0] + "%"));
                        command.Parameters.Add(database.CreateParameter("second", querysplit[1] + "%"));
                        using (IDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                AvatarPickerAvatar user = new AvatarPickerAvatar();
                                user.AvatarID = new UUID((string)reader["UUID"]);
                                user.firstName = (string)reader["username"];
                                user.lastName = (string)reader["lastname"];
                                returnlist.Add(user);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Error(e.ToString());
                }
            }
            else if (querysplit.Length == 1)
            {
                try
                {
                    using (AutoClosingSqlCommand command = database.Query("SELECT UUID,username,lastname FROM " + m_usersTableName + " WHERE username LIKE @first OR lastname LIKE @first"))
                    {
                        command.Parameters.Add(database.CreateParameter("first", querysplit[0] + "%"));

                        using (IDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                AvatarPickerAvatar user = new AvatarPickerAvatar();
                                user.AvatarID = new UUID((string)reader["UUID"]);
                                user.firstName = (string)reader["username"];
                                user.lastName = (string)reader["lastname"];
                                returnlist.Add(user);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Error(e.ToString());
                }
            }
            return returnlist;
        }

        /// <summary>
        /// Store a weblogin key
        /// </summary>
        /// <param name="AgentID">The agent UUID</param>
        /// <param name="WebLoginKey">the WebLogin Key</param>
        /// <remarks>unused ?</remarks>
        override public void StoreWebLoginKey(UUID AgentID, UUID WebLoginKey)
        {
            UserProfileData user = GetUserByUUID(AgentID);
            user.WebLoginKey = WebLoginKey;
            UpdateUserProfile(user);

        }

        /// <summary>
        /// Database provider name
        /// </summary>
        /// <returns>Provider name</returns>
        override public string Name
        {
            get { return "MSSQL Userdata Interface"; }
        }

        /// <summary>
        /// Database provider version
        /// </summary>
        /// <returns>provider version</returns>
        override public string Version
        {
            get { return database.getVersion(); }
        }

        #endregion

        #region Private functions

        /// <summary>
        /// Reads a one item from an SQL result
        /// </summary>
        /// <param name="reader">The SQL Result</param>
        /// <returns>the item read</returns>
        private static AvatarAppearance readUserAppearance(IDataReader reader)
        {
            try
            {
                AvatarAppearance appearance = new AvatarAppearance();

                appearance.Owner = new UUID((string)reader["owner"]);
                appearance.Serial = Convert.ToInt32(reader["serial"]);
                appearance.VisualParams = (byte[])reader["visual_params"];
                appearance.Texture = new Primitive.TextureEntry((byte[])reader["texture"], 0, ((byte[])reader["texture"]).Length);
                appearance.AvatarHeight = (float)Convert.ToDouble(reader["avatar_height"]);
                appearance.BodyItem = new UUID((string)reader["body_item"]);
                appearance.BodyAsset = new UUID((string)reader["body_asset"]);
                appearance.SkinItem = new UUID((string)reader["skin_item"]);
                appearance.SkinAsset = new UUID((string)reader["skin_asset"]);
                appearance.HairItem = new UUID((string)reader["hair_item"]);
                appearance.HairAsset = new UUID((string)reader["hair_asset"]);
                appearance.EyesItem = new UUID((string)reader["eyes_item"]);
                appearance.EyesAsset = new UUID((string)reader["eyes_asset"]);
                appearance.ShirtItem = new UUID((string)reader["shirt_item"]);
                appearance.ShirtAsset = new UUID((string)reader["shirt_asset"]);
                appearance.PantsItem = new UUID((string)reader["pants_item"]);
                appearance.PantsAsset = new UUID((string)reader["pants_asset"]);
                appearance.ShoesItem = new UUID((string)reader["shoes_item"]);
                appearance.ShoesAsset = new UUID((string)reader["shoes_asset"]);
                appearance.SocksItem = new UUID((string)reader["socks_item"]);
                appearance.SocksAsset = new UUID((string)reader["socks_asset"]);
                appearance.JacketItem = new UUID((string)reader["jacket_item"]);
                appearance.JacketAsset = new UUID((string)reader["jacket_asset"]);
                appearance.GlovesItem = new UUID((string)reader["gloves_item"]);
                appearance.GlovesAsset = new UUID((string)reader["gloves_asset"]);
                appearance.UnderShirtItem = new UUID((string)reader["undershirt_item"]);
                appearance.UnderShirtAsset = new UUID((string)reader["undershirt_asset"]);
                appearance.UnderPantsItem = new UUID((string)reader["underpants_item"]);
                appearance.UnderPantsAsset = new UUID((string)reader["underpants_asset"]);
                appearance.SkirtItem = new UUID((string)reader["skirt_item"]);
                appearance.SkirtAsset = new UUID((string)reader["skirt_asset"]);

                return appearance;
            }
            catch (SqlException e)
            {
                m_log.Error(e.ToString());
            }

            return null;
        }

        /// <summary>
        /// Insert/Update a agent row in the DB.
        /// </summary>
        /// <param name="agentdata">agentdata.</param>
        private void InsertUpdateAgentRow(UserAgentData agentdata)
        {
            string sql = @"

IF EXISTS (SELECT * FROM agents WHERE UUID = @UUID)
    BEGIN
        UPDATE agents SET UUID = @UUID, sessionID = @sessionID, secureSessionID = @secureSessionID, agentIP = @agentIP, agentPort = @agentPort, agentOnline = @agentOnline, loginTime = @loginTime, logoutTime = @logoutTime, currentRegion = @currentRegion, currentHandle = @currentHandle, currentPos = @currentPos
        WHERE UUID = @UUID
    END
ELSE
    BEGIN
        INSERT INTO 
            agents (UUID, sessionID, secureSessionID, agentIP, agentPort, agentOnline, loginTime, logoutTime, currentRegion, currentHandle, currentPos) VALUES 
            (@UUID, @sessionID, @secureSessionID, @agentIP, @agentPort, @agentOnline, @loginTime, @logoutTime, @currentRegion, @currentHandle, @currentPos)
    END
";

            using (AutoClosingSqlCommand command = database.Query(sql))
            {
                command.Parameters.Add(database.CreateParameter("@UUID", agentdata.ProfileID));
                command.Parameters.Add(database.CreateParameter("@sessionID", agentdata.SessionID));
                command.Parameters.Add(database.CreateParameter("@secureSessionID", agentdata.SecureSessionID));
                command.Parameters.Add(database.CreateParameter("@agentIP", agentdata.AgentIP));
                command.Parameters.Add(database.CreateParameter("@agentPort", agentdata.AgentPort));
                command.Parameters.Add(database.CreateParameter("@agentOnline", agentdata.AgentOnline));
                command.Parameters.Add(database.CreateParameter("@loginTime", agentdata.LoginTime));
                command.Parameters.Add(database.CreateParameter("@logoutTime", agentdata.LogoutTime));
                command.Parameters.Add(database.CreateParameter("@currentRegion", agentdata.Region));
                command.Parameters.Add(database.CreateParameter("@currentHandle", agentdata.Handle));
                command.Parameters.Add(database.CreateParameter("@currentPos", "<" + ((int)agentdata.Position.X) + "," + ((int)agentdata.Position.Y) + "," + ((int)agentdata.Position.Z) + ">"));

                command.Transaction = command.Connection.BeginTransaction(IsolationLevel.Serializable);
                try
                {
                    if (command.ExecuteNonQuery() > 0)
                    {
                        command.Transaction.Commit();
                        return;
                    }

                    command.Transaction.Rollback();
                    return;
                }
                catch (Exception e)
                {
                    command.Transaction.Rollback();
                    m_log.Error(e.ToString());
                    return;
                }
            }

        }

        /// <summary>
        /// Reads an agent row from a database reader
        /// </summary>
        /// <param name="reader">An active database reader</param>
        /// <returns>A user session agent</returns>
        private UserAgentData readAgentRow(IDataReader reader)
        {
            UserAgentData retval = new UserAgentData();

            if (reader.Read())
            {
                // Agent IDs
                retval.ProfileID = new UUID((string)reader["UUID"]);
                retval.SessionID = new UUID((string)reader["sessionID"]);
                retval.SecureSessionID = new UUID((string)reader["secureSessionID"]);

                // Agent Who?
                retval.AgentIP = (string)reader["agentIP"];
                retval.AgentPort = Convert.ToUInt32(reader["agentPort"].ToString());
                retval.AgentOnline = Convert.ToInt32(reader["agentOnline"].ToString()) != 0;

                // Login/Logout times (UNIX Epoch)
                retval.LoginTime = Convert.ToInt32(reader["loginTime"].ToString());
                retval.LogoutTime = Convert.ToInt32(reader["logoutTime"].ToString());

                // Current position
                retval.Region = (UUID)(string)reader["currentRegion"];
                retval.Handle = Convert.ToUInt64(reader["currentHandle"].ToString());
                Vector3 tmp_v;
                Vector3.TryParse((string)reader["currentPos"], out tmp_v);
                retval.Position = tmp_v;

            }
            else
            {
                return null;
            }
            return retval;
        }

        /// <summary>
        /// Creates a new user and inserts it into the database
        /// </summary>
        /// <param name="uuid">User ID</param>
        /// <param name="username">First part of the login</param>
        /// <param name="lastname">Second part of the login</param>
        /// <param name="passwordHash">A salted hash of the users password</param>
        /// <param name="passwordSalt">The salt used for the password hash</param>
        /// <param name="homeRegion">A regionHandle of the users home region</param>
        /// <param name="homeLocX">Home region position vector</param>
        /// <param name="homeLocY">Home region position vector</param>
        /// <param name="homeLocZ">Home region position vector</param>
        /// <param name="homeLookAtX">Home region 'look at' vector</param>
        /// <param name="homeLookAtY">Home region 'look at' vector</param>
        /// <param name="homeLookAtZ">Home region 'look at' vector</param>
        /// <param name="created">Account created (unix timestamp)</param>
        /// <param name="lastlogin">Last login (unix timestamp)</param>
        /// <param name="inventoryURI">Users inventory URI</param>
        /// <param name="assetURI">Users asset URI</param>
        /// <param name="canDoMask">I can do mask</param>
        /// <param name="wantDoMask">I want to do mask</param>
        /// <param name="aboutText">Profile text</param>
        /// <param name="firstText">Firstlife text</param>
        /// <param name="profileImage">UUID for profile image</param>
        /// <param name="firstImage">UUID for firstlife image</param>
        /// <param name="webLoginKey">web login key</param>
        /// <param name="homeRegionID">homeregion UUID</param>
        /// <param name="godLevel">has the user godlevel</param>
        /// <param name="userFlags">unknown</param>
        /// <param name="customType">unknown</param>
        /// <param name="partnerID">UUID of partner</param>
        /// <returns>Success?</returns>
        private void InsertUserRow(UUID uuid, string username, string lastname, string passwordHash,
                                   string passwordSalt, UInt64 homeRegion, float homeLocX, float homeLocY, float homeLocZ,
                                   float homeLookAtX, float homeLookAtY, float homeLookAtZ, int created, int lastlogin,
                                   string inventoryURI, string assetURI, uint canDoMask, uint wantDoMask,
                                   string aboutText, string firstText,
                                   UUID profileImage, UUID firstImage, UUID webLoginKey, UUID homeRegionID,
                                   int godLevel, int userFlags, string customType, UUID partnerID)
        {
            string sql = "INSERT INTO " + m_usersTableName;
            sql += " ([UUID], [username], [lastname], [passwordHash], [passwordSalt], [homeRegion], ";
            sql += "[homeLocationX], [homeLocationY], [homeLocationZ], [homeLookAtX], [homeLookAtY], [homeLookAtZ], [created], ";
            sql += "[lastLogin], [userInventoryURI], [userAssetURI], [profileCanDoMask], [profileWantDoMask], [profileAboutText], ";
            sql += "[profileFirstText], [profileImage], [profileFirstImage], [webLoginKey], ";
            sql += "[homeRegionID], [userFlags], [godLevel], [customType], [partner]) VALUES ";

            sql += "(@UUID, @username, @lastname, @passwordHash, @passwordSalt, @homeRegion, ";
            sql += "@homeLocationX, @homeLocationY, @homeLocationZ, @homeLookAtX, @homeLookAtY, @homeLookAtZ, @created, ";
            sql += "@lastLogin, @userInventoryURI, @userAssetURI, @profileCanDoMask, @profileWantDoMask, @profileAboutText, ";
            sql += "@profileFirstText, @profileImage, @profileFirstImage, @webLoginKey, ";
            sql += "@homeRegionID, @userFlags, @godLevel, @customType, @partner)";

            try
            {
                using (AutoClosingSqlCommand command = database.Query(sql))
                {
                    command.Parameters.Add(database.CreateParameter("UUID", uuid));
                    command.Parameters.Add(database.CreateParameter("username", username));
                    command.Parameters.Add(database.CreateParameter("lastname", lastname));
                    command.Parameters.Add(database.CreateParameter("passwordHash", passwordHash));
                    command.Parameters.Add(database.CreateParameter("passwordSalt", passwordSalt));
                    command.Parameters.Add(database.CreateParameter("homeRegion", homeRegion));
                    command.Parameters.Add(database.CreateParameter("homeLocationX", homeLocX));
                    command.Parameters.Add(database.CreateParameter("homeLocationY", homeLocY));
                    command.Parameters.Add(database.CreateParameter("homeLocationZ", homeLocZ));
                    command.Parameters.Add(database.CreateParameter("homeLookAtX", homeLookAtX));
                    command.Parameters.Add(database.CreateParameter("homeLookAtY", homeLookAtY));
                    command.Parameters.Add(database.CreateParameter("homeLookAtZ", homeLookAtZ));
                    command.Parameters.Add(database.CreateParameter("created", created));
                    command.Parameters.Add(database.CreateParameter("lastLogin", lastlogin));
                    command.Parameters.Add(database.CreateParameter("userInventoryURI", inventoryURI));
                    command.Parameters.Add(database.CreateParameter("userAssetURI", assetURI));
                    command.Parameters.Add(database.CreateParameter("profileCanDoMask", canDoMask));
                    command.Parameters.Add(database.CreateParameter("profileWantDoMask", wantDoMask));
                    command.Parameters.Add(database.CreateParameter("profileAboutText", aboutText));
                    command.Parameters.Add(database.CreateParameter("profileFirstText", firstText));
                    command.Parameters.Add(database.CreateParameter("profileImage", profileImage));
                    command.Parameters.Add(database.CreateParameter("profileFirstImage", firstImage));
                    command.Parameters.Add(database.CreateParameter("webLoginKey", webLoginKey));
                    //
                    command.Parameters.Add(database.CreateParameter("homeRegionID", homeRegionID));
                    command.Parameters.Add(database.CreateParameter("userFlags", userFlags));
                    command.Parameters.Add(database.CreateParameter("godLevel", godLevel));
                    command.Parameters.Add(database.CreateParameter("customType", customType));
                    command.Parameters.Add(database.CreateParameter("partner", partnerID));

                    
                    command.ExecuteNonQuery();
                    return;
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return;
            }

        }

        /// <summary>
        /// Reads a user profile from an active data reader
        /// </summary>
        /// <param name="reader">An active database reader</param>
        /// <returns>A user profile</returns>
        private static UserProfileData ReadUserRow(IDataReader reader)
        {
            UserProfileData retval = new UserProfileData();

            if (reader.Read())
            {
                retval.ID = new UUID((string)reader["UUID"]);
                retval.FirstName = (string)reader["username"];
                retval.SurName = (string)reader["lastname"];

                retval.PasswordHash = (string)reader["passwordHash"];
                retval.PasswordSalt = (string)reader["passwordSalt"];

                retval.HomeRegion = Convert.ToUInt64(reader["homeRegion"].ToString());
                retval.HomeLocation = new Vector3(
                    Convert.ToSingle(reader["homeLocationX"].ToString()),
                    Convert.ToSingle(reader["homeLocationY"].ToString()),
                    Convert.ToSingle(reader["homeLocationZ"].ToString()));
                retval.HomeLookAt = new Vector3(
                    Convert.ToSingle(reader["homeLookAtX"].ToString()),
                    Convert.ToSingle(reader["homeLookAtY"].ToString()),
                    Convert.ToSingle(reader["homeLookAtZ"].ToString()));

                retval.Created = Convert.ToInt32(reader["created"].ToString());
                retval.LastLogin = Convert.ToInt32(reader["lastLogin"].ToString());

                retval.UserInventoryURI = (string)reader["userInventoryURI"];
                retval.UserAssetURI = (string)reader["userAssetURI"];

                retval.CanDoMask = Convert.ToUInt32(reader["profileCanDoMask"].ToString());
                retval.WantDoMask = Convert.ToUInt32(reader["profileWantDoMask"].ToString());

                retval.AboutText = (string)reader["profileAboutText"];
                retval.FirstLifeAboutText = (string)reader["profileFirstText"];

                retval.Image = new UUID((string)reader["profileImage"]);
                retval.FirstLifeImage = new UUID((string)reader["profileFirstImage"]);
                retval.WebLoginKey = new UUID((string)reader["webLoginKey"]);
            }
            else
            {
                return null;
            }
            return retval;
        }

        #endregion
    }

}
