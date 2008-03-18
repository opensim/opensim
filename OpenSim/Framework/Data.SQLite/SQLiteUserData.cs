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
using libsecondlife;
using Mono.Data.SqliteClient;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Data.SQLite
{
    /// <summary>
    /// A User storage interface for the SQLite database system
    /// </summary>
    public class SQLiteUserData : SQLiteBase, IUserData
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The database manager
        /// </summary>
        /// <summary>
        /// Artificial constructor called upon plugin load
        /// </summary>
        private const string SelectUserByUUID = "select * from users where UUID=:UUID";
        private const string SelectUserByName = "select * from users where username=:username and surname=:surname";
        private const string SelectFriendsByUUID = "select a.friendID, a.friendPerms, b.friendPerms from userfriends as a, userfriends as b where a.ownerID=:ownerID and b.ownerID=a.friendID and b.friendID=a.ownerID";

        private const string userSelect = "select * from users";
        private const string userFriendsSelect = "select a.ownerID as ownerID,a.friendID as friendID,a.friendPerms as friendPerms,b.friendPerms as ownerperms, b.ownerID as fownerID, b.friendID as ffriendID from userfriends as a, userfriends as b";

        private const string AvatarPickerAndSQL = "select * from users where username like :username and surname like :surname";
        private const string AvatarPickerOrSQL = "select * from users where username like :username or surname like :surname";
        
        private DataSet ds;
        private SqliteDataAdapter da;
        private SqliteDataAdapter daf;
        SqliteConnection g_conn; 

        public void Initialise()
        {
            SqliteConnection conn = new SqliteConnection("URI=file:userprofiles.db,version=3");
            TestTables(conn);

            // This sucks, but It doesn't seem to work with the dataset Syncing :P
            g_conn = conn;
            g_conn.Open();
            
            ds = new DataSet();
            da = new SqliteDataAdapter(new SqliteCommand(userSelect, conn));
            daf = new SqliteDataAdapter(new SqliteCommand(userFriendsSelect, conn));

            lock (ds)
            {
                ds.Tables.Add(createUsersTable());
                ds.Tables.Add(createUserAgentsTable());
                ds.Tables.Add(createUserFriendsTable());

                setupUserCommands(da, conn);
                da.Fill(ds.Tables["users"]);

                setupUserFriendsCommands(daf, conn);
                try
                {
                    daf.Fill(ds.Tables["userfriends"]);
                }
                catch (SqliteSyntaxException)
                {
                    m_log.Info("[SQLITE]: userfriends table not found, creating.... ");
                    InitDB(conn);
                    daf.Fill(ds.Tables["userfriends"]);
                }

            }

            return;
        }

        // see IUserData
        public UserProfileData GetUserByUUID(LLUUID uuid)
        {
            lock (ds)
            {
                DataRow row = ds.Tables["users"].Rows.Find(Util.ToRawUuidString(uuid));
                if (row != null)
                {
                    UserProfileData user = buildUserProfile(row);
                    row = ds.Tables["useragents"].Rows.Find(Util.ToRawUuidString(uuid));
                    if (row != null)
                    {
                        user.currentAgent = buildUserAgent(row);
                    }
                    return user;
                }
                else
                {
                    return null;
                }
            }
        }

        // see IUserData
        public UserProfileData GetUserByName(string fname, string lname)
        {
            string select = "surname = '" + lname + "' and username = '" + fname + "'";
            lock (ds)
            {
                DataRow[] rows = ds.Tables["users"].Select(select);
                if (rows.Length > 0)
                {
                    UserProfileData user = buildUserProfile(rows[0]);
                    DataRow row = ds.Tables["useragents"].Rows.Find(Util.ToRawUuidString(user.UUID));
                    if (row != null)
                    {
                        user.currentAgent = buildUserAgent(row);
                    }
                    return user;
                }
                else
                {
                    return null;
                }
            }
        }

        #region User Friends List Data
        
        public void AddNewUserFriend(LLUUID friendlistowner, LLUUID friend, uint perms)
        {
            string InsertFriends = "insert into userfriends(ownerID, friendID, friendPerms) values(:ownerID, :friendID, :perms)";
            
            using (SqliteCommand cmd = new SqliteCommand(InsertFriends, g_conn))
            {
                cmd.Parameters.Add(new SqliteParameter(":ownerID", friendlistowner.UUID.ToString()));
                cmd.Parameters.Add(new SqliteParameter(":friendID", friend.UUID.ToString()));
                cmd.Parameters.Add(new SqliteParameter(":perms", perms));
                cmd.ExecuteNonQuery();
            }
            using (SqliteCommand cmd = new SqliteCommand(InsertFriends, g_conn))
            {
                cmd.Parameters.Add(new SqliteParameter(":ownerID", friend.UUID.ToString()));
                cmd.Parameters.Add(new SqliteParameter(":friendID", friendlistowner.UUID.ToString()));
                cmd.Parameters.Add(new SqliteParameter(":perms", perms));
                cmd.ExecuteNonQuery();
            }
        }
        
        public void RemoveUserFriend(LLUUID friendlistowner, LLUUID friend)
        {
            string DeletePerms = "delete from friendlist where (ownerID=:ownerID and friendID=:friendID) or (ownerID=:friendID and friendID=:ownerID)";
            using (SqliteCommand cmd = new SqliteCommand(DeletePerms, g_conn))
            {
                cmd.Parameters.Add(new SqliteParameter(":ownerID", friendlistowner.UUID.ToString()));
                cmd.Parameters.Add(new SqliteParameter(":friendID", friend.UUID.ToString()));
                cmd.ExecuteNonQuery();
            }
        }
                
        public void UpdateUserFriendPerms(LLUUID friendlistowner, LLUUID friend, uint perms)
        {
            string UpdatePerms = "update friendlist set perms=:perms where ownerID=:ownerID and friendID=:friendID";
            using (SqliteCommand cmd = new SqliteCommand(UpdatePerms, g_conn))
            {
                cmd.Parameters.Add(new SqliteParameter(":perms", perms));
                cmd.Parameters.Add(new SqliteParameter(":ownerID", friendlistowner.UUID.ToString()));
                cmd.Parameters.Add(new SqliteParameter(":friendID", friend.UUID.ToString()));
                cmd.ExecuteNonQuery();
            }
        }

        public List<FriendListItem> GetUserFriendList(LLUUID friendlistowner)
        {
            List<FriendListItem> returnlist = new List<FriendListItem>();

            using (SqliteCommand cmd = new SqliteCommand(SelectFriendsByUUID, g_conn))
            {
                cmd.Parameters.Add(new SqliteParameter(":ownerID", friendlistowner.UUID.ToString()));

                try
                {
                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            FriendListItem user = new FriendListItem();
                            user.FriendListOwner = friendlistowner;
                            user.Friend = new LLUUID((string)reader[0]);
                            user.FriendPerms = Convert.ToUInt32(reader[1]);
                            user.FriendListOwnerPerms = Convert.ToUInt32(reader[2]);
                            returnlist.Add(user);
                        }
                        reader.Close();
                    }
                }
                catch (Exception ex)
                {
                    m_log.Error("[USER]: Exception getting friends list for user: " + ex.ToString());
                }
            }
             
            return returnlist;
        }
       
        


        #endregion

        public void UpdateUserCurrentRegion(LLUUID avatarid, LLUUID regionuuid)
        {
            m_log.Info("[USER]: Stub UpdateUserCUrrentRegion called");
        }


        public List<Framework.AvatarPickerAvatar> GeneratePickerResults(LLUUID queryID, string query)
        {
            List<Framework.AvatarPickerAvatar> returnlist = new List<Framework.AvatarPickerAvatar>();
            string[] querysplit;
            querysplit = query.Split(' ');
            if (querysplit.Length == 2)
            {
                using (SqliteCommand cmd = new SqliteCommand(AvatarPickerAndSQL, g_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":username", querysplit[0] + "%"));
                    cmd.Parameters.Add(new SqliteParameter(":surname", querysplit[1] + "%"));

                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Framework.AvatarPickerAvatar user = new Framework.AvatarPickerAvatar();
                            user.AvatarID = new LLUUID((string) reader["UUID"]);
                            user.firstName = (string) reader["username"];
                            user.lastName = (string) reader["surname"];
                            returnlist.Add(user);
                        }
                        reader.Close();
                    }
                }
            }
            else if (querysplit.Length == 1)
            {
                using (SqliteCommand cmd = new SqliteCommand(AvatarPickerOrSQL, g_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":username", querysplit[0] + "%"));
                    cmd.Parameters.Add(new SqliteParameter(":surname", querysplit[0] + "%"));

                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Framework.AvatarPickerAvatar user = new Framework.AvatarPickerAvatar();
                            user.AvatarID = new LLUUID((string) reader["UUID"]);
                            user.firstName = (string) reader["username"];
                            user.lastName = (string) reader["surname"];
                            returnlist.Add(user);
                        }
                        reader.Close();
                    }
                }
            }
            return returnlist;
        }

        /// <summary>
        /// Returns a user by UUID direct
        /// </summary>
        /// <param name="uuid">The user's account ID</param>
        /// <returns>A matching user profile</returns>
        public UserAgentData GetAgentByUUID(LLUUID uuid)
        {
            try
            {
                return GetUserByUUID(uuid).currentAgent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Returns a session by account name
        /// </summary>
        /// <param name="name">The account name</param>
        /// <returns>The user's session agent</returns>
        public UserAgentData GetAgentByName(string name)
        {
            return GetAgentByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        /// <summary>
        /// Returns a session by account name
        /// </summary>
        /// <param name="fname">The first part of the user's account name</param>
        /// <param name="lname">The second part of the user's account name</param>
        /// <returns>A user agent</returns>
        public UserAgentData GetAgentByName(string fname, string lname)
        {
            try
            {
                return GetUserByName(fname, lname).currentAgent;
            }
            catch (Exception)
            {
                return null;
            }
        }


        public void StoreWebLoginKey(LLUUID AgentID, LLUUID WebLoginKey)
        {
            DataTable users = ds.Tables["users"];
            lock (ds)
            {
                DataRow row = users.Rows.Find(Util.ToRawUuidString(AgentID));
                if (row == null)
                {
                    m_log.Warn("[WEBLOGIN]: Unable to store new web login key for non-existant user");
                }
                else
                {
                    UserProfileData user = GetUserByUUID(AgentID);
                    user.webLoginKey = WebLoginKey;
                    fillUserRow(row, user);
                    da.Update(ds, "users");

                }
            }

        }

        /// <summary>
        /// Creates a new user profile
        /// </summary>
        /// <param name="user">The profile to add to the database</param>
        public void AddNewUserProfile(UserProfileData user)
        {
            DataTable users = ds.Tables["users"];
            lock (ds)
            {
                DataRow row = users.Rows.Find(Util.ToRawUuidString(user.UUID));
                if (row == null)
                {
                    row = users.NewRow();
                    fillUserRow(row, user);
                    users.Rows.Add(row);
                }
                else
                {
                    fillUserRow(row, user);
                    
                }
                // This is why we're getting the 'logins never log-off'..    because It isn't clearing the 
                // useragents table once the useragent is null
                //
                // A database guy should look at this and figure out the best way to clear the useragents table.
                if (user.currentAgent != null)
                {
                    DataTable ua = ds.Tables["useragents"];
                    row = ua.Rows.Find(Util.ToRawUuidString(user.UUID));
                    if (row == null)
                    {
                        row = ua.NewRow();
                        fillUserAgentRow(row, user.currentAgent);
                        ua.Rows.Add(row);
                    }
                    else
                    {
                        fillUserAgentRow(row, user.currentAgent);
                    }
                }
                else
                {
                    // I just added this to help the standalone login situation.  
                    //It still needs to be looked at by a Database guy
                    DataTable ua = ds.Tables["useragents"];
                    row = ua.Rows.Find(Util.ToRawUuidString(user.UUID));

                    if (row == null)
                    {
                        // do nothing
                    }
                    else
                    {
                        row.Delete();
                        ua.AcceptChanges();
                    }
                }

                m_log.Info("[SQLITE]: " +
                                         "Syncing user database: " + ds.Tables["users"].Rows.Count + " users stored");
                // save changes off to disk
                da.Update(ds, "users");
            }
        }

        /// <summary>
        /// Creates a new user profile
        /// </summary>
        /// <param name="user">The profile to add to the database</param>
        /// <returns>True on success, false on error</returns>
        public bool UpdateUserProfile(UserProfileData user)
        {
            try
            {
                AddNewUserProfile(user);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a new user agent
        /// </summary>
        /// <param name="agent">The agent to add to the database</param>
        public void AddNewUserAgent(UserAgentData agent)
        {
            // Do nothing. yet.
        }

        /// <summary>
        /// Transfers money between two user accounts
        /// </summary>
        /// <param name="from">Starting account</param>
        /// <param name="to">End account</param>
        /// <param name="amount">The amount to move</param>
        /// <returns>Success?</returns>
        public bool MoneyTransferRequest(LLUUID from, LLUUID to, uint amount)
        {
            return true;
        }

        /// <summary>
        /// Transfers inventory between two accounts
        /// </summary>
        /// <remarks>Move to inventory server</remarks>
        /// <param name="from">Senders account</param>
        /// <param name="to">Receivers account</param>
        /// <param name="item">Inventory item</param>
        /// <returns>Success?</returns>
        public bool InventoryTransferRequest(LLUUID from, LLUUID to, LLUUID item)
        {
            return true;
        }

        /// <summary>
        /// Returns the name of the storage provider
        /// </summary>
        /// <returns>Storage provider name</returns>
        public string getName()
        {
            return "Sqlite Userdata";
        }

        /// <summary>
        /// Returns the version of the storage provider
        /// </summary>
        /// <returns>Storage provider version</returns>
        public string GetVersion()
        {
            return "0.1";
        }

        /***********************************************************************
         *
         *  DataTable creation 
         *
         **********************************************************************/
        /***********************************************************************
         *
         *  Database Definition Functions
         * 
         *  This should be db agnostic as we define them in ADO.NET terms
         *
         **********************************************************************/

        private DataTable createUsersTable()
        {
            DataTable users = new DataTable("users");

            createCol(users, "UUID", typeof (String));
            createCol(users, "username", typeof (String));
            createCol(users, "surname", typeof (String));
            createCol(users, "passwordHash", typeof (String));
            createCol(users, "passwordSalt", typeof (String));

            createCol(users, "homeRegionX", typeof (Int32));
            createCol(users, "homeRegionY", typeof (Int32));
            createCol(users, "homeLocationX", typeof (Double));
            createCol(users, "homeLocationY", typeof (Double));
            createCol(users, "homeLocationZ", typeof (Double));
            createCol(users, "homeLookAtX", typeof (Double));
            createCol(users, "homeLookAtY", typeof (Double));
            createCol(users, "homeLookAtZ", typeof (Double));
            createCol(users, "created", typeof (Int32));
            createCol(users, "lastLogin", typeof (Int32));
            createCol(users, "rootInventoryFolderID", typeof (String));
            createCol(users, "userInventoryURI", typeof (String));
            createCol(users, "userAssetURI", typeof (String));
            createCol(users, "profileCanDoMask", typeof (Int32));
            createCol(users, "profileWantDoMask", typeof (Int32));
            createCol(users, "profileAboutText", typeof (String));
            createCol(users, "profileFirstText", typeof (String));
            createCol(users, "profileImage", typeof (String));
            createCol(users, "profileFirstImage", typeof (String));
            createCol(users, "webLoginKey", typeof(String));
            // Add in contraints
            users.PrimaryKey = new DataColumn[] {users.Columns["UUID"]};
            return users;
        }

        private DataTable createUserAgentsTable()
        {
            DataTable ua = new DataTable("useragents");
            // this is the UUID of the user
            createCol(ua, "UUID", typeof (String));
            createCol(ua, "agentIP", typeof (String));
            createCol(ua, "agentPort", typeof (Int32));
            createCol(ua, "agentOnline", typeof (Boolean));
            createCol(ua, "sessionID", typeof (String));
            createCol(ua, "secureSessionID", typeof (String));
            createCol(ua, "regionID", typeof (String));
            createCol(ua, "loginTime", typeof (Int32));
            createCol(ua, "logoutTime", typeof (Int32));
            createCol(ua, "currentRegion", typeof (String));
            createCol(ua, "currentHandle", typeof (String));
            // vectors
            createCol(ua, "currentPosX", typeof (Double));
            createCol(ua, "currentPosY", typeof (Double));
            createCol(ua, "currentPosZ", typeof (Double));
            // constraints
            ua.PrimaryKey = new DataColumn[] {ua.Columns["UUID"]};

            return ua;
        }

        private DataTable createUserFriendsTable()
        {
            DataTable ua = new DataTable("userfriends");
            // table contains user <----> user relationship with perms
            createCol(ua, "ownerID", typeof(String));
            createCol(ua, "friendID", typeof(String));
            createCol(ua, "friendPerms", typeof(Int32));
            createCol(ua, "ownerPerms", typeof(Int32));
            createCol(ua, "datetimestamp", typeof(Int32));

            return ua;
        }

        /***********************************************************************
         *  
         *  Convert between ADO.NET <=> OpenSim Objects
         *
         *  These should be database independant
         *
         **********************************************************************/
         
        private UserProfileData buildUserProfile(DataRow row)
        {
            // TODO: this doesn't work yet because something more
            // interesting has to be done to actually get these values
            // back out.  Not enough time to figure it out yet.
            UserProfileData user = new UserProfileData();
            LLUUID.TryParse((String)row["UUID"], out user.UUID);
            user.username = (String) row["username"];
            user.surname = (String) row["surname"];
            user.passwordHash = (String) row["passwordHash"];
            user.passwordSalt = (String) row["passwordSalt"];

            user.homeRegionX = Convert.ToUInt32(row["homeRegionX"]);
            user.homeRegionY = Convert.ToUInt32(row["homeRegionY"]);
            user.homeLocation = new LLVector3(
                Convert.ToSingle(row["homeLocationX"]),
                Convert.ToSingle(row["homeLocationY"]),
                Convert.ToSingle(row["homeLocationZ"])
                );
            user.homeLookAt = new LLVector3(
                Convert.ToSingle(row["homeLookAtX"]),
                Convert.ToSingle(row["homeLookAtY"]),
                Convert.ToSingle(row["homeLookAtZ"])
                );
            user.created = Convert.ToInt32(row["created"]);
            user.lastLogin = Convert.ToInt32(row["lastLogin"]);
            user.rootInventoryFolderID = new LLUUID((String) row["rootInventoryFolderID"]);
            user.userInventoryURI = (String) row["userInventoryURI"];
            user.userAssetURI = (String) row["userAssetURI"];
            user.profileCanDoMask = Convert.ToUInt32(row["profileCanDoMask"]);
            user.profileWantDoMask = Convert.ToUInt32(row["profileWantDoMask"]);
            user.profileAboutText = (String) row["profileAboutText"];
            user.profileFirstText = (String) row["profileFirstText"];
            LLUUID.TryParse((String)row["profileImage"], out user.profileImage);
            LLUUID.TryParse((String)row["profileFirstImage"], out user.profileFirstImage);
            user.webLoginKey = new LLUUID((String) row["webLoginKey"]);

            return user;
        }

        private void fillFriendRow(DataRow row, LLUUID ownerID, LLUUID friendID, uint perms)
        {
            row["ownerID"] = ownerID.UUID.ToString();
            row["friendID"] = friendID.UUID.ToString();
            row["friendPerms"] = perms;
            foreach (DataColumn col in ds.Tables["userfriends"].Columns)
            {
                if (row[col] == null)
                {
                    row[col] = String.Empty;
                }
            }
        }

        private void fillUserRow(DataRow row, UserProfileData user)
        {
            row["UUID"] = Util.ToRawUuidString(user.UUID);
            row["username"] = user.username;
            row["surname"] = user.surname;
            row["passwordHash"] = user.passwordHash;
            row["passwordSalt"] = user.passwordSalt;


            row["homeRegionX"] = user.homeRegionX;
            row["homeRegionY"] = user.homeRegionY;
            row["homeLocationX"] = user.homeLocation.X;
            row["homeLocationY"] = user.homeLocation.Y;
            row["homeLocationZ"] = user.homeLocation.Z;
            row["homeLookAtX"] = user.homeLookAt.X;
            row["homeLookAtY"] = user.homeLookAt.Y;
            row["homeLookAtZ"] = user.homeLookAt.Z;

            row["created"] = user.created;
            row["lastLogin"] = user.lastLogin;
            row["rootInventoryFolderID"] = user.rootInventoryFolderID;
            row["userInventoryURI"] = user.userInventoryURI;
            row["userAssetURI"] = user.userAssetURI;
            row["profileCanDoMask"] = user.profileCanDoMask;
            row["profileWantDoMask"] = user.profileWantDoMask;
            row["profileAboutText"] = user.profileAboutText;
            row["profileFirstText"] = user.profileFirstText;
            row["profileImage"] = user.profileImage;
            row["profileFirstImage"] = user.profileFirstImage;
            row["webLoginKey"] = user.webLoginKey;

            // ADO.NET doesn't handle NULL very well
            foreach (DataColumn col in ds.Tables["users"].Columns)
            {
                if (row[col] == null)
                {
                    row[col] = String.Empty;
                }
            }
        }

        private UserAgentData buildUserAgent(DataRow row)
        {
            UserAgentData ua = new UserAgentData();

            ua.UUID = new LLUUID((String) row["UUID"]);
            ua.agentIP = (String) row["agentIP"];
            ua.agentPort = Convert.ToUInt32(row["agentPort"]);
            ua.agentOnline = Convert.ToBoolean(row["agentOnline"]);
            ua.sessionID = new LLUUID((String) row["sessionID"]);
            ua.secureSessionID = new LLUUID((String) row["secureSessionID"]);
            ua.regionID = new LLUUID((String) row["regionID"]);
            ua.loginTime = Convert.ToInt32(row["loginTime"]);
            ua.logoutTime = Convert.ToInt32(row["logoutTime"]);
            ua.currentRegion = new LLUUID((String) row["currentRegion"]);
            ua.currentHandle = Convert.ToUInt64(row["currentHandle"]);
            ua.currentPos = new LLVector3(
                Convert.ToSingle(row["currentPosX"]),
                Convert.ToSingle(row["currentPosY"]),
                Convert.ToSingle(row["currentPosZ"])
                );
            return ua;
        }

        private void fillUserAgentRow(DataRow row, UserAgentData ua)
        {
            row["UUID"] = ua.UUID;
            row["agentIP"] = ua.agentIP;
            row["agentPort"] = ua.agentPort;
            row["agentOnline"] = ua.agentOnline;
            row["sessionID"] = ua.sessionID;
            row["secureSessionID"] = ua.secureSessionID;
            row["regionID"] = ua.regionID;
            row["loginTime"] = ua.loginTime;
            row["logoutTime"] = ua.logoutTime;
            row["currentRegion"] = ua.currentRegion;
            row["currentHandle"] = ua.currentHandle.ToString();
            // vectors
            row["currentPosX"] = ua.currentPos.X;
            row["currentPosY"] = ua.currentPos.Y;
            row["currentPosZ"] = ua.currentPos.Z;
        }

        /***********************************************************************
         *
         *  Database Binding functions
         *
         *  These will be db specific due to typing, and minor differences
         *  in databases.
         *
         **********************************************************************/

        private void setupUserCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("users", ds.Tables["users"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("users", "UUID=:UUID", ds.Tables["users"]);
            da.UpdateCommand.Connection = conn;

            SqliteCommand delete = new SqliteCommand("delete from users where UUID = :UUID");
            delete.Parameters.Add(createSqliteParameter("UUID", typeof (String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        private void setupUserFriendsCommands(SqliteDataAdapter daf, SqliteConnection conn)
        {
            daf.InsertCommand = createInsertCommand("userfriends", ds.Tables["userfriends"]);
            daf.InsertCommand.Connection = conn;

            daf.UpdateCommand = createUpdateCommand("userfriends", "ownerID=:ownerID and friendID=:friendID", ds.Tables["userfriends"]);
            daf.UpdateCommand.Connection = conn;

            SqliteCommand delete = new SqliteCommand("delete from userfriends where ownerID=:ownerID and friendID=:friendID");
            delete.Parameters.Add(createSqliteParameter("ownerID", typeof(String)));
            delete.Parameters.Add(createSqliteParameter("friendID", typeof(String)));
            delete.Connection = conn;
            daf.DeleteCommand = delete;

        }

        private void InitDB(SqliteConnection conn)
        {
            string createUsers = defineTable(createUsersTable());
            string createFriends = defineTable(createUserFriendsTable());

            SqliteCommand pcmd = new SqliteCommand(createUsers, conn);
            SqliteCommand fcmd = new SqliteCommand(createFriends, conn);

            conn.Open();

            try
            {

                pcmd.ExecuteNonQuery();
            }
            catch (System.Exception)
            {
                m_log.Info("[USERS]: users table already exists");
            }

            try
            {
                fcmd.ExecuteNonQuery();
            }
            catch (System.Exception)
            {
                m_log.Info("[USERS]: userfriends table already exists");
            }

            conn.Close();
        }

        private bool TestTables(SqliteConnection conn)
        {
            SqliteCommand cmd = new SqliteCommand(userSelect, conn);
            SqliteCommand fcmd = new SqliteCommand(userFriendsSelect, conn);
            SqliteDataAdapter pDa = new SqliteDataAdapter(cmd);
            SqliteDataAdapter fDa = new SqliteDataAdapter(cmd);

            DataSet tmpDS = new DataSet();
            DataSet tmpDS2 = new DataSet();

            try
            {
                pDa.Fill(tmpDS, "users");
                fDa.Fill(tmpDS2, "userfriends");
            }
            catch (SqliteSyntaxException)
            {
                m_log.Info("[DATASTORE]: SQLite Database doesn't exist... creating");
                InitDB(conn);
            }
            conn.Open();
            try
            {
                cmd = new SqliteCommand("select webLoginKey from users limit 1;", conn);
                cmd.ExecuteNonQuery();
            }
            catch (SqliteSyntaxException)
            {
                cmd = new SqliteCommand("alter table users add column webLoginKey text default '00000000-0000-0000-0000-000000000000';", conn);
                cmd.ExecuteNonQuery();
                pDa.Fill(tmpDS, "users");
            }
            finally
            {
                conn.Close();
            }

            return true;
        }
    }
}
