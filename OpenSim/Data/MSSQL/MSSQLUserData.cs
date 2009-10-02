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
 *     * Neither the name of the OpenSimulator Project nor the
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
using log4net;
using OpenMetaverse;
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

        private const string m_agentsTableName = "agents";
        private const string m_usersTableName = "users";
        private const string m_userFriendsTableName = "userfriends";

        // [Obsolete("Cannot be default-initialized!")]
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
            if (!string.IsNullOrEmpty(connect))
            {
                database = new MSSQLManager(connect);
            }
            else
            {
                IniFile iniFile = new IniFile("mssql_connection.ini");

                string settingDataSource = iniFile.ParseFileReadValue("data_source");
                string settingInitialCatalog = iniFile.ParseFileReadValue("initial_catalog");
                string settingPersistSecurityInfo = iniFile.ParseFileReadValue("persist_security_info");
                string settingUserId = iniFile.ParseFileReadValue("user_id");
                string settingPassword = iniFile.ParseFileReadValue("password");

                database = new MSSQLManager(settingDataSource, settingInitialCatalog, settingPersistSecurityInfo, settingUserId, settingPassword);
            }

            //Check migration on DB
            database.CheckMigration(_migrationStore);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        override public void Dispose() { }

        #region User table methods

        /// <summary>
        /// Searches the database for a specified user profile by name components
        /// </summary>
        /// <param name="user">The first part of the account name</param>
        /// <param name="last">The second part of the account name</param>
        /// <returns>A user profile</returns>
        override public UserProfileData GetUserByName(string user, string last)
        {
            string sql = string.Format(@"SELECT * FROM {0} 
                                         WHERE username = @first AND lastname = @second", m_usersTableName);
            using (AutoClosingSqlCommand command = database.Query(sql))
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
                    m_log.ErrorFormat("[USER DB] Error getting user profile for {0} {1}: {2}", user, last, e.Message);
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
            string sql = string.Format("SELECT * FROM {0} WHERE UUID = @uuid", m_usersTableName);
            using (AutoClosingSqlCommand command = database.Query(sql))
            {
                command.Parameters.Add(database.CreateParameter("uuid", uuid));
                try
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        return ReadUserRow(reader);
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USER DB] Error getting user profile by UUID {0}, error: {1}", uuid, e.Message);
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
                InsertUserRow(user.ID, user.FirstName, user.SurName, user.Email, user.PasswordHash, user.PasswordSalt,
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
                m_log.ErrorFormat("[USER DB] Error adding new profile, error: {0}", e.Message);
            }
        }

        /// <summary>
        /// update a user profile
        /// </summary>
        /// <param name="user">the profile to update</param>
        /// <returns></returns>
        override public bool UpdateUserProfile(UserProfileData user)
        {
            string sql = string.Format(@"UPDATE {0} 
                                        SET UUID = @uuid,
                                        username = @username, 
                                        lastname = @lastname,
                                        email = @email,
                                        passwordHash = @passwordHash,
                                        passwordSalt = @passwordSalt,
                                        homeRegion = @homeRegion,
                                        homeLocationX = @homeLocationX,
                                        homeLocationY = @homeLocationY,
                                        homeLocationZ = @homeLocationZ,
                                        homeLookAtX = @homeLookAtX,
                                        homeLookAtY = @homeLookAtY,
                                        homeLookAtZ = @homeLookAtZ,
                                        created = @created,
                                        lastLogin = @lastLogin,
                                        userInventoryURI = @userInventoryURI,
                                        userAssetURI = @userAssetURI,
                                        profileCanDoMask = @profileCanDoMask,
                                        profileWantDoMask = @profileWantDoMask,
                                        profileAboutText = @profileAboutText,
                                        profileFirstText = @profileFirstText,
                                        profileImage = @profileImage,
                                        profileFirstImage = @profileFirstImage, 
                                        webLoginKey = @webLoginKey, 
                                        homeRegionID = @homeRegionID,
                                        userFlags = @userFlags,
                                        godLevel = @godLevel, 
                                        customType = @customType, 
                                        partner = @partner WHERE UUID = @keyUUUID;",m_usersTableName);
            using (AutoClosingSqlCommand command = database.Query(sql))
            {
                command.Parameters.Add(database.CreateParameter("uuid", user.ID));
                command.Parameters.Add(database.CreateParameter("username", user.FirstName));
                command.Parameters.Add(database.CreateParameter("lastname", user.SurName));
                command.Parameters.Add(database.CreateParameter("email", user.Email));
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
                    m_log.ErrorFormat("[USER DB] Error updating profile, error: {0}", e.Message);
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
            string sql = string.Format("SELECT * FROM {0} WHERE UUID = @uuid", m_agentsTableName);
            using (AutoClosingSqlCommand command = database.Query(sql))
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
                    m_log.ErrorFormat("[USER DB] Error updating agentdata by UUID, error: {0}", e.Message);
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
                m_log.ErrorFormat("[USER DB] Error adding new agentdata, error: {0}", e.Message);
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
            string sql = string.Format(@"INSERT INTO {0}
                                            (ownerID,friendID,friendPerms,datetimestamp) 
                                         VALUES
                                            (@ownerID,@friendID,@friendPerms,@datetimestamp)", m_userFriendsTableName);
            using (AutoClosingSqlCommand command = database.Query(sql))
            {
                command.Parameters.Add(database.CreateParameter("ownerID", friendlistowner));
                command.Parameters.Add(database.CreateParameter("friendID", friend));
                command.Parameters.Add(database.CreateParameter("friendPerms", perms));
                command.Parameters.Add(database.CreateParameter("datetimestamp", dtvalue));
                command.ExecuteNonQuery();

                try
                {
                    sql = string.Format(@"INSERT INTO {0}
                                            (ownerID,friendID,friendPerms,datetimestamp) 
                                          VALUES 
                                            (@friendID,@ownerID,@friendPerms,@datetimestamp)", m_userFriendsTableName);
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USER DB] Error adding new userfriend, error: {0}", e.Message);
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
            string sql = string.Format(@"DELETE from {0} 
                                         WHERE ownerID = @ownerID 
                                            AND friendID = @friendID", m_userFriendsTableName);
            using (AutoClosingSqlCommand command = database.Query(sql))
            {
                command.Parameters.Add(database.CreateParameter("@ownerID", friendlistowner));
                command.Parameters.Add(database.CreateParameter("@friendID", friend));
                command.ExecuteNonQuery();
                sql = string.Format(@"DELETE from {0} 
                                         WHERE ownerID = @friendID 
                                            AND friendID = @ownerID", m_userFriendsTableName);
                command.CommandText = sql;
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USER DB] Error removing userfriend, error: {0}", e.Message);
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
            string sql = string.Format(@"UPDATE {0} SET friendPerms = @friendPerms 
                                         WHERE ownerID = @ownerID 
                                            AND friendID = @friendID", m_userFriendsTableName);
            using (AutoClosingSqlCommand command = database.Query(sql))
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
                    m_log.ErrorFormat("[USER DB] Error updating userfriend, error: {0}", e.Message);
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
            string sql = string.Format(@"SELECT a.ownerID, a.friendID, a.friendPerms, b.friendPerms AS ownerperms 
                                        FROM {0} as a, {0} as b
                                        WHERE a.ownerID = @ownerID 
                                        AND b.ownerID = a.friendID 
                                        AND b.friendID = a.ownerID", m_userFriendsTableName);
            using (AutoClosingSqlCommand command = database.Query(sql))
            {
                command.Parameters.Add(database.CreateParameter("@ownerID", friendlistowner));
                try
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            FriendListItem fli = new FriendListItem();
                            fli.FriendListOwner = new UUID((Guid)reader["ownerID"]);
                            fli.Friend = new UUID((Guid)reader["friendID"]);
                            fli.FriendPerms = (uint)Convert.ToInt32(reader["friendPerms"]);

                            // This is not a real column in the database table, it's a joined column from the opposite record
                            fli.FriendListOwnerPerms = (uint)Convert.ToInt32(reader["ownerperms"]);
                            friendList.Add(fli);
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USER DB] Error updating userfriend, error: {0}", e.Message);
                }
            }
            return friendList;
        }

        override public Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos (List<UUID> uuids)
        {
            Dictionary<UUID, FriendRegionInfo> infos = new Dictionary<UUID,FriendRegionInfo>(); 
            try
            {
                foreach (UUID uuid in uuids)
                {
                    string sql = string.Format(@"SELECT agentOnline,currentHandle 
                                                 FROM {0} WHERE UUID = @uuid", m_agentsTableName);
                    using (AutoClosingSqlCommand command = database.Query(sql))
                    {
                        command.Parameters.Add(database.CreateParameter("@uuid", uuid));
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                FriendRegionInfo fri = new FriendRegionInfo();
                                fri.isOnline = (byte)reader["agentOnline"] != 0;
                                fri.regionHandle = Convert.ToUInt64(reader["currentHandle"].ToString());

                                infos[uuid] = fri;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Warn("[MSSQL]: Got exception on trying to find friends regions:", e);
            }

            return infos;
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
        override public AvatarAppearance GetUserAppearance(UUID user)
        {
            try
            {
                AvatarAppearance appearance = new AvatarAppearance();
                string sql = "SELECT * FROM avatarappearance WHERE owner = @UUID";
                using (AutoClosingSqlCommand command = database.Query(sql))
                {
                    command.Parameters.Add(database.CreateParameter("@UUID", user));
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                            appearance = readUserAppearance(reader);
                        else
                        {
                            m_log.WarnFormat("[USER DB] No appearance found for user {0}", user.ToString());
                            return null;
                        }

                    }
                }

                appearance.SetAttachments(GetUserAttachments(user));

                return appearance;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[USER DB] Error updating userfriend, error: {0}", e.Message);
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
            string sql = @"DELETE FROM avatarappearance WHERE owner=@owner; 
                        INSERT INTO avatarappearance 
                           (owner, serial, visual_params, texture, avatar_height, 
                            body_item, body_asset, skin_item, skin_asset, hair_item, 
                            hair_asset, eyes_item, eyes_asset, shirt_item, shirt_asset, 
                            pants_item, pants_asset, shoes_item, shoes_asset, socks_item, 
                            socks_asset, jacket_item, jacket_asset, gloves_item, gloves_asset, 
                            undershirt_item, undershirt_asset, underpants_item, underpants_asset, 
                            skirt_item, skirt_asset) 
                        VALUES
                           (@owner, @serial, @visual_params, @texture, @avatar_height, 
                            @body_item, @body_asset, @skin_item, @skin_asset, @hair_item, 
                            @hair_asset, @eyes_item, @eyes_asset, @shirt_item, @shirt_asset, 
                            @pants_item, @pants_asset, @shoes_item, @shoes_asset, @socks_item, 
                            @socks_asset, @jacket_item, @jacket_asset, @gloves_item, @gloves_asset, 
                            @undershirt_item, @undershirt_asset, @underpants_item, @underpants_asset, 
                            @skirt_item, @skirt_asset)";

            using (AutoClosingSqlCommand cmd = database.Query(sql))
            {
                cmd.Parameters.Add(database.CreateParameter("@owner", appearance.Owner));
                cmd.Parameters.Add(database.CreateParameter("@serial", appearance.Serial));
                cmd.Parameters.Add(database.CreateParameter("@visual_params", appearance.VisualParams));
                cmd.Parameters.Add(database.CreateParameter("@texture", appearance.Texture.GetBytes()));
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
                    m_log.ErrorFormat("[USER DB] Error updating user appearance, error: {0}", e.Message);
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
            string sql = "select attachpoint, item, asset from avatarattachments where UUID = @uuid";
            using (AutoClosingSqlCommand command = database.Query(sql, database.CreateParameter("@uuid", agentID)))
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
            string sql = "DELETE FROM avatarattachments WHERE UUID = @uuid";

            using (AutoClosingSqlCommand command = database.Query(sql))
            {
                command.Parameters.Add(database.CreateParameter("uuid", agentID));
                command.ExecuteNonQuery();
            }
            if (data == null)
                return;

            sql = @"INSERT INTO avatarattachments (UUID, attachpoint, item, asset) 
                    VALUES (@uuid, @attachpoint, @item, @asset)";

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
                        command.Parameters.Add(database.CreateParameter("@item", new UUID(item["item"].ToString())));
                        command.Parameters.Add(database.CreateParameter("@asset", new UUID(item["asset"].ToString())));
                        firstTime = false;
                    }
                    command.Parameters["@uuid"].Value = agentID.Guid; //.ToString();
                    command.Parameters["@attachpoint"].Value = attachpoint;
                    command.Parameters["@item"].Value = new Guid(item["item"].ToString());
                    command.Parameters["@asset"].Value = new Guid(item["asset"].ToString());

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
            string sql = "UPDATE avatarattachments SET asset = '00000000-0000-0000-0000-000000000000' WHERE UUID = @uuid";
            using (AutoClosingSqlCommand command = database.Query(sql))
            {
                command.Parameters.Add(database.CreateParameter("uuid", agentID));
                command.ExecuteNonQuery();
            }
        }

        override public void LogoutUsers(UUID regionID)
        {
        }

        #endregion

        #region Other public methods

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
                    string sql = string.Format(@"SELECT UUID,username,lastname FROM {0} 
                                                 WHERE username LIKE @first AND lastname LIKE @second", m_usersTableName);
                    using (AutoClosingSqlCommand command = database.Query(sql))
                    {
                        //Add wildcard to the search
                        command.Parameters.Add(database.CreateParameter("first", querysplit[0] + "%"));
                        command.Parameters.Add(database.CreateParameter("second", querysplit[1] + "%"));
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                AvatarPickerAvatar user = new AvatarPickerAvatar();
                                user.AvatarID = new UUID((Guid)reader["UUID"]);
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
                    string sql = string.Format(@"SELECT UUID,username,lastname FROM {0} 
                                                 WHERE username LIKE @first OR lastname LIKE @first", m_usersTableName);
                    using (AutoClosingSqlCommand command = database.Query(sql))
                    {
                        command.Parameters.Add(database.CreateParameter("first", querysplit[0] + "%"));

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                AvatarPickerAvatar user = new AvatarPickerAvatar();
                                user.AvatarID = new UUID((Guid)reader["UUID"]);
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
        private static AvatarAppearance readUserAppearance(SqlDataReader reader)
        {
            try
            {
                AvatarAppearance appearance = new AvatarAppearance();

                appearance.Owner = new UUID((Guid)reader["owner"]);
                appearance.Serial = Convert.ToInt32(reader["serial"]);
                appearance.VisualParams = (byte[])reader["visual_params"];
                appearance.Texture = new Primitive.TextureEntry((byte[])reader["texture"], 0, ((byte[])reader["texture"]).Length);
                appearance.AvatarHeight = (float)Convert.ToDouble(reader["avatar_height"]);
                appearance.BodyItem = new UUID((Guid)reader["body_item"]);
                appearance.BodyAsset = new UUID((Guid)reader["body_asset"]);
                appearance.SkinItem = new UUID((Guid)reader["skin_item"]);
                appearance.SkinAsset = new UUID((Guid)reader["skin_asset"]);
                appearance.HairItem = new UUID((Guid)reader["hair_item"]);
                appearance.HairAsset = new UUID((Guid)reader["hair_asset"]);
                appearance.EyesItem = new UUID((Guid)reader["eyes_item"]);
                appearance.EyesAsset = new UUID((Guid)reader["eyes_asset"]);
                appearance.ShirtItem = new UUID((Guid)reader["shirt_item"]);
                appearance.ShirtAsset = new UUID((Guid)reader["shirt_asset"]);
                appearance.PantsItem = new UUID((Guid)reader["pants_item"]);
                appearance.PantsAsset = new UUID((Guid)reader["pants_asset"]);
                appearance.ShoesItem = new UUID((Guid)reader["shoes_item"]);
                appearance.ShoesAsset = new UUID((Guid)reader["shoes_asset"]);
                appearance.SocksItem = new UUID((Guid)reader["socks_item"]);
                appearance.SocksAsset = new UUID((Guid)reader["socks_asset"]);
                appearance.JacketItem = new UUID((Guid)reader["jacket_item"]);
                appearance.JacketAsset = new UUID((Guid)reader["jacket_asset"]);
                appearance.GlovesItem = new UUID((Guid)reader["gloves_item"]);
                appearance.GlovesAsset = new UUID((Guid)reader["gloves_asset"]);
                appearance.UnderShirtItem = new UUID((Guid)reader["undershirt_item"]);
                appearance.UnderShirtAsset = new UUID((Guid)reader["undershirt_asset"]);
                appearance.UnderPantsItem = new UUID((Guid)reader["underpants_item"]);
                appearance.UnderPantsAsset = new UUID((Guid)reader["underpants_asset"]);
                appearance.SkirtItem = new UUID((Guid)reader["skirt_item"]);
                appearance.SkirtAsset = new UUID((Guid)reader["skirt_asset"]);

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
        private UserAgentData readAgentRow(SqlDataReader reader)
        {
            UserAgentData retval = new UserAgentData();

            if (reader.Read())
            {
                // Agent IDs
                retval.ProfileID = new UUID((Guid)reader["UUID"]);
                retval.SessionID = new UUID((Guid)reader["sessionID"]);
                retval.SecureSessionID = new UUID((Guid)reader["secureSessionID"]);

                // Agent Who?
                retval.AgentIP = (string)reader["agentIP"];
                retval.AgentPort = Convert.ToUInt32(reader["agentPort"].ToString());
                retval.AgentOnline = Convert.ToInt32(reader["agentOnline"].ToString()) != 0;

                // Login/Logout times (UNIX Epoch)
                retval.LoginTime = Convert.ToInt32(reader["loginTime"].ToString());
                retval.LogoutTime = Convert.ToInt32(reader["logoutTime"].ToString());

                // Current position
                retval.Region = new UUID((Guid)reader["currentRegion"]);
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
        /// <param name="email">Email of person</param>
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
        private void InsertUserRow(UUID uuid, string username, string lastname, string email, string passwordHash,
                                   string passwordSalt, UInt64 homeRegion, float homeLocX, float homeLocY, float homeLocZ,
                                   float homeLookAtX, float homeLookAtY, float homeLookAtZ, int created, int lastlogin,
                                   string inventoryURI, string assetURI, uint canDoMask, uint wantDoMask,
                                   string aboutText, string firstText,
                                   UUID profileImage, UUID firstImage, UUID webLoginKey, UUID homeRegionID,
                                   int godLevel, int userFlags, string customType, UUID partnerID)
        {
            string sql = string.Format(@"INSERT INTO {0} 
                ([UUID], [username], [lastname], [email], [passwordHash], [passwordSalt], 
                 [homeRegion], [homeLocationX], [homeLocationY], [homeLocationZ], [homeLookAtX], 
                 [homeLookAtY], [homeLookAtZ], [created], [lastLogin], [userInventoryURI], 
                 [userAssetURI], [profileCanDoMask], [profileWantDoMask], [profileAboutText], 
                 [profileFirstText], [profileImage], [profileFirstImage], [webLoginKey], 
                 [homeRegionID], [userFlags], [godLevel], [customType], [partner]) 
                VALUES 
                (@UUID, @username, @lastname, @email, @passwordHash, @passwordSalt, 
                 @homeRegion, @homeLocationX, @homeLocationY, @homeLocationZ, @homeLookAtX, 
                 @homeLookAtY, @homeLookAtZ, @created, @lastLogin, @userInventoryURI, 
                 @userAssetURI, @profileCanDoMask, @profileWantDoMask, @profileAboutText,
                 @profileFirstText, @profileImage, @profileFirstImage, @webLoginKey, 
                 @homeRegionID, @userFlags, @godLevel, @customType, @partner)", m_usersTableName);

            try
            {
                using (AutoClosingSqlCommand command = database.Query(sql))
                {
                    command.Parameters.Add(database.CreateParameter("UUID", uuid));
                    command.Parameters.Add(database.CreateParameter("username", username));
                    command.Parameters.Add(database.CreateParameter("lastname", lastname));
                    command.Parameters.Add(database.CreateParameter("email", email));
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
        private static UserProfileData ReadUserRow(SqlDataReader reader)
        {
            UserProfileData retval = new UserProfileData();

            if (reader.Read())
            {
                retval.ID = new UUID((Guid)reader["UUID"]);
                retval.FirstName = (string)reader["username"];
                retval.SurName = (string)reader["lastname"];
                if (reader.IsDBNull(reader.GetOrdinal("email")))
                    retval.Email = "";
                else
                    retval.Email = (string)reader["email"];

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

                if (reader.IsDBNull(reader.GetOrdinal("homeRegionID")))
                    retval.HomeRegionID = UUID.Zero;
                else
                    retval.HomeRegionID = new UUID((Guid)reader["homeRegionID"]);

                retval.Created = Convert.ToInt32(reader["created"].ToString());
                retval.LastLogin = Convert.ToInt32(reader["lastLogin"].ToString());

                if (reader.IsDBNull(reader.GetOrdinal("userInventoryURI")))
                    retval.UserInventoryURI = "";
                else
                    retval.UserInventoryURI = (string)reader["userInventoryURI"];

                if (reader.IsDBNull(reader.GetOrdinal("userAssetURI")))
                    retval.UserAssetURI = "";
                else
                    retval.UserAssetURI = (string)reader["userAssetURI"];

                retval.CanDoMask = Convert.ToUInt32(reader["profileCanDoMask"].ToString());
                retval.WantDoMask = Convert.ToUInt32(reader["profileWantDoMask"].ToString());


                if (reader.IsDBNull(reader.GetOrdinal("profileAboutText")))
                    retval.AboutText = "";
                else
                    retval.AboutText = (string)reader["profileAboutText"];

                if (reader.IsDBNull(reader.GetOrdinal("profileFirstText")))
                    retval.FirstLifeAboutText = "";
                else
                    retval.FirstLifeAboutText = (string)reader["profileFirstText"];

                if (reader.IsDBNull(reader.GetOrdinal("profileImage")))
                    retval.Image = UUID.Zero;
                else
                    retval.Image = new UUID((Guid)reader["profileImage"]);

                if (reader.IsDBNull(reader.GetOrdinal("profileFirstImage")))
                    retval.Image = UUID.Zero;
                else
                    retval.FirstLifeImage = new UUID((Guid)reader["profileFirstImage"]);

                if (reader.IsDBNull(reader.GetOrdinal("webLoginKey")))
                    retval.WebLoginKey = UUID.Zero;
                else
                    retval.WebLoginKey = new UUID((Guid)reader["webLoginKey"]);

                retval.UserFlags = Convert.ToInt32(reader["userFlags"].ToString());
                retval.GodLevel = Convert.ToInt32(reader["godLevel"].ToString());
                if (reader.IsDBNull(reader.GetOrdinal("customType")))
                    retval.CustomType = "";
                else
                    retval.CustomType = reader["customType"].ToString();

                if (reader.IsDBNull(reader.GetOrdinal("partner")))
                    retval.Partner = UUID.Zero;
                else
                    retval.Partner = new UUID((Guid)reader["partner"]);
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
