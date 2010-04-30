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
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using log4net;
using Mono.Data.Sqlite;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.SQLite
{
    /// <summary>
    /// A User storage interface for the SQLite database system
    /// </summary>
    public class SQLiteUserData : UserDataBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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
        private const string userAgentSelect = "select * from useragents";
        private const string AvatarAppearanceSelect = "select * from avatarappearance";

        private const string AvatarPickerAndSQL = "select * from users where username like :username and surname like :surname";
        private const string AvatarPickerOrSQL = "select * from users where username like :username or surname like :surname";

        private DataSet ds;
        private SqliteDataAdapter da;
        private SqliteDataAdapter daf;
        private SqliteDataAdapter dua;
        private SqliteDataAdapter daa;
        SqliteConnection g_conn;

        public override void Initialise()
        {
            m_log.Info("[SQLiteUserData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException (Name);
        }

        /// <summary>
        /// <list type="bullet">
        /// <item>Initialises User Interface</item>
        /// <item>Loads and initialises a new SQLite connection and maintains it.</item>
        /// <item>use default URI if connect string string is empty.</item>
        /// </list>
        /// </summary>
        /// <param name="connect">connect string</param>
        override public void Initialise(string connect)
        {
            // default to something sensible
            if (connect == "")
                connect = "URI=file:userprofiles.db,version=3";

            SqliteConnection conn = new SqliteConnection(connect);

            // This sucks, but It doesn't seem to work with the dataset Syncing :P
            g_conn = conn;
            g_conn.Open();

            Assembly assem = GetType().Assembly;
            Migration m = new Migration(g_conn, assem, "UserStore");
            m.Update();


            ds = new DataSet();
            da = new SqliteDataAdapter(new SqliteCommand(userSelect, conn));
            dua = new SqliteDataAdapter(new SqliteCommand(userAgentSelect, conn));
            daf = new SqliteDataAdapter(new SqliteCommand(userFriendsSelect, conn));
            daa = new SqliteDataAdapter(new SqliteCommand(AvatarAppearanceSelect, conn));
            //if (daa == null) m_log.Info("[SQLiteUserData]: daa = null");

            lock (ds)
            {
                Console.WriteLine("Here1");
                ds.Tables.Add(createUsersTable());
                ds.Tables.Add(createUserAgentsTable());
                ds.Tables.Add(createUserFriendsTable());
                ds.Tables.Add(createAvatarAppearanceTable());

                Console.WriteLine("Here2");
                setupUserCommands(da, conn);
                da.Fill(ds.Tables["users"]);
                CreateDataSetMapping(da, "users");

                Console.WriteLine("Here3");
                setupAgentCommands(dua, conn);
                dua.Fill(ds.Tables["useragents"]);
                CreateDataSetMapping(dua, "useragents");

                Console.WriteLine("Here4");
                setupUserFriendsCommands(daf, conn);
                daf.Fill(ds.Tables["userfriends"]);
                CreateDataSetMapping(daf, "userfriends");

                Console.WriteLine("Here5");
                setupAvatarAppearanceCommands(daa, conn);
                daa.Fill(ds.Tables["avatarappearance"]);
                CreateDataSetMapping(daa, "avatarappearance");
                Console.WriteLine("Here6");
            }

            return;
        }

        public override void Dispose ()
        {
            if (g_conn != null)
            {
                g_conn.Close();
                g_conn = null;
            }
            if (ds != null)
            {
                ds.Dispose();
                ds = null;
            }
            if (da != null)
            {
                da.Dispose();
                da = null;
            }
            if (daf != null)
            {
                daf.Dispose();
                daf = null;
            }
            if (dua != null)
            {
                dua.Dispose();
                dua = null;
            }
            if (daa != null)
            {
                daa.Dispose();
                daa = null;
            }
        }

        /// <summary>
        /// see IUserDataPlugin,
        /// Get user data profile by UUID
        /// </summary>
        /// <param name="uuid">User UUID</param>
        /// <returns>user profile data</returns>
        override public UserProfileData GetUserByUUID(UUID uuid)
        {
            lock (ds)
            {
                DataRow row = ds.Tables["users"].Rows.Find(uuid.ToString());
                if (row != null)
                {
                    UserProfileData user = buildUserProfile(row);
                    return user;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// see IUserDataPlugin,
        /// Get user data profile by name
        /// </summary>
        /// <param name="fname">first name</param>
        /// <param name="lname">last name</param>
        /// <returns>user profile data</returns>
        override public UserProfileData GetUserByName(string fname, string lname)
        {
            string select = "surname = '" + lname + "' and username = '" + fname + "'";
            lock (ds)
            {
                DataRow[] rows = ds.Tables["users"].Select(select);
                if (rows.Length > 0)
                {
                    UserProfileData user = buildUserProfile(rows[0]);
                    return user;
                }
                else
                {
                    return null;
                }
            }
        }

        #region User Friends List Data

        private bool ExistsFriend(UUID owner, UUID friend)
        {
            string FindFriends = "select * from userfriends where (ownerID=:ownerID and friendID=:friendID) or (ownerID=:friendID and friendID=:ownerID)";
            using (SqliteCommand cmd = new SqliteCommand(FindFriends, g_conn))
            {
                cmd.Parameters.Add(new SqliteParameter(":ownerID", owner.ToString()));
                cmd.Parameters.Add(new SqliteParameter(":friendID", friend.ToString()));
                try
                {
                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            reader.Close();
                            return true;
                        }
                        else
                        {
                            reader.Close();
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    m_log.Error("[USER DB]: Exception getting friends list for user: " + ex.ToString());
                    return false;
                }
            }
        }
        /// <summary>
        /// Add a new friend in the friendlist
        /// </summary>
        /// <param name="friendlistowner">UUID of the friendlist owner</param>
        /// <param name="friend">UUID of the friend to add</param>
        /// <param name="perms">permission flag</param>
        override public void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms)
        {
            if (ExistsFriend(friendlistowner, friend))
                return;

            string InsertFriends = "insert into userfriends(ownerID, friendID, friendPerms) values(:ownerID, :friendID, :perms)";
            using (SqliteCommand cmd = new SqliteCommand(InsertFriends, g_conn))
            {
                cmd.Parameters.Add(new SqliteParameter(":ownerID", friendlistowner.ToString()));
                cmd.Parameters.Add(new SqliteParameter(":friendID", friend.ToString()));
                cmd.Parameters.Add(new SqliteParameter(":perms", perms));
                cmd.ExecuteNonQuery();
            }
            using (SqliteCommand cmd = new SqliteCommand(InsertFriends, g_conn))
            {
                cmd.Parameters.Add(new SqliteParameter(":ownerID", friend.ToString()));
                cmd.Parameters.Add(new SqliteParameter(":friendID", friendlistowner.ToString()));
                cmd.Parameters.Add(new SqliteParameter(":perms", perms));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Remove a user from the friendlist
        /// </summary>
        /// <param name="friendlistowner">UUID of the friendlist owner</param>
        /// <param name="friend">UUID of the friend to remove</param>
        override public void RemoveUserFriend(UUID friendlistowner, UUID friend)
        {
            string DeletePerms = "delete from userfriends where (ownerID=:ownerID and friendID=:friendID) or (ownerID=:friendID and friendID=:ownerID)";
            using (SqliteCommand cmd = new SqliteCommand(DeletePerms, g_conn))
            {
                cmd.Parameters.Add(new SqliteParameter(":ownerID", friendlistowner.ToString()));
                cmd.Parameters.Add(new SqliteParameter(":friendID", friend.ToString()));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Update the friendlist permission
        /// </summary>
        /// <param name="friendlistowner">UUID of the friendlist owner</param>
        /// <param name="friend">UUID of the friend to modify</param>
        /// <param name="perms">updated permission flag</param>
        override public void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms)
        {
            string UpdatePerms = "update userfriends set friendPerms=:perms where ownerID=:ownerID and friendID=:friendID";
            using (SqliteCommand cmd = new SqliteCommand(UpdatePerms, g_conn))
            {
                cmd.Parameters.Add(new SqliteParameter(":perms", perms));
                cmd.Parameters.Add(new SqliteParameter(":ownerID", friendlistowner.ToString()));
                cmd.Parameters.Add(new SqliteParameter(":friendID", friend.ToString()));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Get (fetch?) the friendlist for a user
        /// </summary>
        /// <param name="friendlistowner">UUID of the friendlist owner</param>
        /// <returns>The friendlist list</returns>
        override public List<FriendListItem> GetUserFriendList(UUID friendlistowner)
        {
            List<FriendListItem> returnlist = new List<FriendListItem>();

            using (SqliteCommand cmd = new SqliteCommand(SelectFriendsByUUID, g_conn))
            {
                cmd.Parameters.Add(new SqliteParameter(":ownerID", friendlistowner.ToString()));

                try
                {
                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            FriendListItem user = new FriendListItem();
                            user.FriendListOwner = friendlistowner;
                            user.Friend = new UUID((string)reader[0]);
                            user.FriendPerms = Convert.ToUInt32(reader[1]);
                            user.FriendListOwnerPerms = Convert.ToUInt32(reader[2]);
                            returnlist.Add(user);
                        }
                        reader.Close();
                    }
                }
                catch (Exception ex)
                {
                    m_log.Error("[USER DB]: Exception getting friends list for user: " + ex.ToString());
                }
            }

            return returnlist;
        }

        override public Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos (List<UUID> uuids)
        {
            Dictionary<UUID, FriendRegionInfo> infos = new Dictionary<UUID,FriendRegionInfo>();

            DataTable agents = ds.Tables["useragents"];
            foreach (UUID uuid in uuids)
            {
                lock (ds)
                {
                    DataRow row = agents.Rows.Find(uuid.ToString());
                    if (row == null) infos[uuid] = null;
                    else
                    {
                        FriendRegionInfo fri = new FriendRegionInfo();
                        fri.isOnline = (bool)row["agentOnline"];
                        fri.regionHandle = Convert.ToUInt64(row["currentHandle"]);
                        infos[uuid] = fri;
                    }
                }
            }
            return infos;
        }

        #endregion

        /// <summary>
        ///
        /// </summary>
        /// <param name="queryID"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        override public List<AvatarPickerAvatar> GeneratePickerResults(UUID queryID, string query)
        {
            List<AvatarPickerAvatar> returnlist = new List<AvatarPickerAvatar>();
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
                            AvatarPickerAvatar user = new AvatarPickerAvatar();
                            user.AvatarID = new UUID((string) reader["UUID"]);
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
                            AvatarPickerAvatar user = new AvatarPickerAvatar();
                            user.AvatarID = new UUID((string) reader["UUID"]);
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
        override public UserAgentData GetAgentByUUID(UUID uuid)
        {
            lock (ds)
            {
                DataRow row = ds.Tables["useragents"].Rows.Find(uuid.ToString());
                if (row != null)
                {
                    return buildUserAgent(row);
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Returns a session by account name
        /// </summary>
        /// <param name="name">The account name</param>
        /// <returns>The user's session agent</returns>
        override public UserAgentData GetAgentByName(string name)
        {
            return GetAgentByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        /// <summary>
        /// Returns a session by account name
        /// </summary>
        /// <param name="fname">The first part of the user's account name</param>
        /// <param name="lname">The second part of the user's account name</param>
        /// <returns>A user agent</returns>
        override public UserAgentData GetAgentByName(string fname, string lname)
        {
            UserAgentData agent = null;

            UserProfileData profile = GetUserByName(fname, lname);
            if (profile != null)
            {
                agent = GetAgentByUUID(profile.ID);
            }
            return agent;
        }

        /// <summary>
        /// DEPRECATED? Store the weblogin key
        /// </summary>
        /// <param name="AgentID">UUID of the user</param>
        /// <param name="WebLoginKey">UUID of the weblogin</param>
        override public void StoreWebLoginKey(UUID AgentID, UUID WebLoginKey)
        {
            DataTable users = ds.Tables["users"];
            lock (ds)
            {
                DataRow row = users.Rows.Find(AgentID.ToString());
                if (row == null)
                {
                    m_log.Warn("[USER DB]: Unable to store new web login key for non-existant user");
                }
                else
                {
                    UserProfileData user = GetUserByUUID(AgentID);
                    user.WebLoginKey = WebLoginKey;
                    fillUserRow(row, user);
                    da.Update(ds, "users");
                }
            }
        }

        private bool ExistsFirstLastName(String fname, String lname)
        {
            string FindUser = "select * from users where (username=:username and surname=:surname)";
            using (SqliteCommand cmd = new SqliteCommand(FindUser, g_conn))
            {
                cmd.Parameters.Add(new SqliteParameter(":username", fname));
                cmd.Parameters.Add(new SqliteParameter(":surname", lname));
                try
                {
                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            reader.Close();
                            return true;
                        }
                        else
                        {
                            reader.Close();
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    m_log.Error("[USER DB]: Exception searching for user's first and last name: " + ex.ToString());
                    return false;
                }
            }
        }

        /// <summary>
        /// Creates a new user profile
        /// </summary>
        /// <param name="user">The profile to add to the database</param>
        override public void AddNewUserProfile(UserProfileData user)
        {
            DataTable users = ds.Tables["users"];
            UUID zero = UUID.Zero;
            if (ExistsFirstLastName(user.FirstName, user.SurName) || user.ID == zero)
                return;

            lock (ds)
            {
                DataRow row = users.Rows.Find(user.ID.ToString());
                if (row == null)
                {
                    row = users.NewRow();
                    fillUserRow(row, user);
                    users.Rows.Add(row);

                    m_log.Debug("[USER DB]: Syncing user database: " + ds.Tables["users"].Rows.Count + " users stored");

                    // save changes off to disk
                    da.Update(ds, "users");
                }
                else
                {
                    m_log.WarnFormat("[USER DB]: Ignoring add since user with id {0} already exists", user.ID);
                }
            }
        }

        /// <summary>
        /// Creates a new user profile
        /// </summary>
        /// <param name="user">The profile to add to the database</param>
        /// <returns>True on success, false on error</returns>
        override public bool UpdateUserProfile(UserProfileData user)
        {
            DataTable users = ds.Tables["users"];
            lock (ds)
            {
                DataRow row = users.Rows.Find(user.ID.ToString());
                if (row == null)
                {
                    return false;
                }
                else
                {
                    fillUserRow(row, user);
                    da.Update(ds, "users");
                }
            }

            //AddNewUserProfile(user);
            return true;
        }

        /// <summary>
        /// Creates a new user agent
        /// </summary>
        /// <param name="agent">The agent to add to the database</param>
        override public void AddNewUserAgent(UserAgentData agent)
        {
            UUID zero = UUID.Zero;
            if (agent.SessionID == zero || agent.ProfileID == zero)
                return;

            DataTable agents = ds.Tables["useragents"];
            lock (ds)
            {
                DataRow row = agents.Rows.Find(agent.ProfileID.ToString());
                if (row == null)
                {
                    row = agents.NewRow();
                    fillUserAgentRow(row, agent);
                    agents.Rows.Add(row);
                }
                else
                {
                    fillUserAgentRow(row, agent);

                }
                m_log.Info("[USER DB]: Syncing useragent database: " + ds.Tables["useragents"].Rows.Count + " agents stored");
                // save changes off to disk
                dua.Update(ds, "useragents");
            }
        }

        /// <summary>
        /// Transfers money between two user accounts
        /// </summary>
        /// <param name="from">Starting account</param>
        /// <param name="to">End account</param>
        /// <param name="amount">The amount to move</param>
        /// <returns>Success?</returns>
        override public bool MoneyTransferRequest(UUID from, UUID to, uint amount)
        {
            return false; // for consistency with the MySQL impl
        }

        /// <summary>
        /// Transfers inventory between two accounts
        /// </summary>
        /// <remarks>Move to inventory server</remarks>
        /// <param name="from">Senders account</param>
        /// <param name="to">Receivers account</param>
        /// <param name="item">Inventory item</param>
        /// <returns>Success?</returns>
        override public bool InventoryTransferRequest(UUID from, UUID to, UUID item)
        {
            return false; //for consistency with the MySQL impl
        }


        /// <summary>
        /// Appearance.
        /// TODO: stubs for now to do in memory appearance.
        /// </summary>
        /// <param name="user">The user UUID</param>
        /// <returns>Avatar Appearence</returns>
        override public AvatarAppearance GetUserAppearance(UUID user)
        {
            m_log.Info("[APPEARANCE] GetUserAppearance " + user.ToString());

            AvatarAppearance aa = new AvatarAppearance(user);
            //try {
            aa.Owner = user;

            DataTable aap = ds.Tables["avatarappearance"];
            lock (ds)
            {
                DataRow row = aap.Rows.Find(Util.ToRawUuidString(user));
                if (row == null)
                {
                    m_log.Info("[APPEARANCE] Could not find appearance for " + user.ToString());

                    //m_log.Debug("[USER DB]: Creating avatarappearance For: " + user.ToString());

                    //row = aap.NewRow();
                    //fillAvatarAppearanceRow(row, user, appearance);
                    //aap.Rows.Add(row);
                    //    m_log.Debug("[USER DB]: Syncing user database: " + ds.Tables["users"].Rows.Count + " users stored");
                    // save changes off to disk
                    //daa.Update(ds, "avatarappearance");
                }
                else
                {
                    m_log.InfoFormat("[APPEARANCE] appearance found for {0}", user.ToString());

                    aa.BodyAsset        = new UUID((String)row["BodyAsset"]);
                    aa.BodyItem         = new UUID((String)row["BodyItem"]);
                    aa.SkinItem         = new UUID((String)row["SkinItem"]);
                    aa.SkinAsset        = new UUID((String)row["SkinAsset"]);
                    aa.HairItem         = new UUID((String)row["HairItem"]);
                    aa.HairAsset        = new UUID((String)row["HairAsset"]);
                    aa.EyesItem         = new UUID((String)row["EyesItem"]);
                    aa.EyesAsset        = new UUID((String)row["EyesAsset"]);
                    aa.ShirtItem        = new UUID((String)row["ShirtItem"]);
                    aa.ShirtAsset       = new UUID((String)row["ShirtAsset"]);
                    aa.PantsItem        = new UUID((String)row["PantsItem"]);
                    aa.PantsAsset       = new UUID((String)row["PantsAsset"]);
                    aa.ShoesItem        = new UUID((String)row["ShoesItem"]);
                    aa.ShoesAsset       = new UUID((String)row["ShoesAsset"]);
                    aa.SocksItem        = new UUID((String)row["SocksItem"]);
                    aa.SocksAsset       = new UUID((String)row["SocksAsset"]);
                    aa.JacketItem       = new UUID((String)row["JacketItem"]);
                    aa.JacketAsset      = new UUID((String)row["JacketAsset"]);
                    aa.GlovesItem       = new UUID((String)row["GlovesItem"]);
                    aa.GlovesAsset      = new UUID((String)row["GlovesAsset"]);
                    aa.UnderShirtItem   = new UUID((String)row["UnderShirtItem"]);
                    aa.UnderShirtAsset  = new UUID((String)row["UnderShirtAsset"]);
                    aa.UnderPantsItem   = new UUID((String)row["UnderPantsItem"]);
                    aa.UnderPantsAsset  = new UUID((String)row["UnderPantsAsset"]);
                    aa.SkirtItem        = new UUID((String)row["SkirtItem"]);
                    aa.SkirtAsset       = new UUID((String)row["SkirtAsset"]);

                    byte[] texture = (byte[])row["Texture"];
                    aa.Texture = new Primitive.TextureEntry(texture, 0, texture.Length);

                    byte[] VisualParams = (byte[])row["VisualParams"];
                    aa.VisualParams = VisualParams;

                    aa.Serial = Convert.ToInt32(row["Serial"]);
                    aa.AvatarHeight = Convert.ToSingle(row["AvatarHeight"]);
                    m_log.InfoFormat("[APPEARANCE] appearance set for {0}", user.ToString());
                }
            }

           //     m_log.Info("[APPEARANCE] Found appearance for " + user.ToString() + aa.ToString());
           // } catch (KeyNotFoundException) {
           //     m_log.InfoFormat("[APPEARANCE] No appearance found for {0}", user.ToString());
           // }
            return aa;
        }

        /// <summary>
        /// Update a user appearence
        /// </summary>
        /// <param name="user">the user UUID</param>
        /// <param name="appearance">appearence</param>
        override public void UpdateUserAppearance(UUID user, AvatarAppearance appearance)
        {
            appearance.Owner = user;
            DataTable aap = ds.Tables["avatarappearance"];
            lock (ds)
            {
                DataRow row = aap.Rows.Find(Util.ToRawUuidString(user));
                if (row == null)
                {
                    m_log.Debug("[USER DB]: Creating UserAppearance For: " + user.ToString());

                    row = aap.NewRow();
                    fillAvatarAppearanceRow(row, user, appearance);
                    aap.Rows.Add(row);
                    //    m_log.Debug("[USER DB]: Syncing user database: " + ds.Tables["users"].Rows.Count + " users stored");
                    // save changes off to disk
                    daa.Update(ds, "avatarappearance");
                }
                else
                {
                    m_log.Debug("[USER DB]: Updating UserAppearance For: " + user.ToString());
                    fillAvatarAppearanceRow(row, user, appearance);
                    daa.Update(ds, "avatarappearance");
                }
            }
        }

        /// <summary>
        /// Returns the name of the storage provider
        /// </summary>
        /// <returns>Storage provider name</returns>
        override public string Name
        {
            get {return "Sqlite Userdata";}
        }

        /// <summary>
        /// Returns the version of the storage provider
        /// </summary>
        /// <returns>Storage provider version</returns>
        override public string Version
        {
            get {return "0.1";}
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

        protected void CreateDataSetMapping(IDataAdapter da, string tableName)
        {       
            ITableMapping dbMapping = da.TableMappings.Add(tableName, tableName);
            foreach (DataColumn col in ds.Tables[tableName].Columns)
            {       
                dbMapping.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            }       
        }
        
        /// <summary>
        /// Create the "users" table
        /// </summary>
        /// <returns>DataTable</returns>
        private static DataTable createUsersTable()
        {
            DataTable users = new DataTable("users");

            SQLiteUtil.createCol(users, "UUID", typeof (String));
            SQLiteUtil.createCol(users, "username", typeof (String));
            SQLiteUtil.createCol(users, "surname", typeof (String));
            SQLiteUtil.createCol(users, "email", typeof (String));
            SQLiteUtil.createCol(users, "passwordHash", typeof (String));
            SQLiteUtil.createCol(users, "passwordSalt", typeof (String));

            SQLiteUtil.createCol(users, "homeRegionX", typeof (Int32));
            SQLiteUtil.createCol(users, "homeRegionY", typeof (Int32));
            SQLiteUtil.createCol(users, "homeRegionID", typeof (String));
            SQLiteUtil.createCol(users, "homeLocationX", typeof (Double));
            SQLiteUtil.createCol(users, "homeLocationY", typeof (Double));
            SQLiteUtil.createCol(users, "homeLocationZ", typeof (Double));
            SQLiteUtil.createCol(users, "homeLookAtX", typeof (Double));
            SQLiteUtil.createCol(users, "homeLookAtY", typeof (Double));
            SQLiteUtil.createCol(users, "homeLookAtZ", typeof (Double));
            SQLiteUtil.createCol(users, "created", typeof (Int32));
            SQLiteUtil.createCol(users, "lastLogin", typeof (Int32));

            //TODO: Please delete this column.  It's now a brick
            SQLiteUtil.createCol(users, "rootInventoryFolderID", typeof (String));

            SQLiteUtil.createCol(users, "userInventoryURI", typeof (String));
            SQLiteUtil.createCol(users, "userAssetURI", typeof (String));
            SQLiteUtil.createCol(users, "profileCanDoMask", typeof (Int32));
            SQLiteUtil.createCol(users, "profileWantDoMask", typeof (Int32));
            SQLiteUtil.createCol(users, "profileAboutText", typeof (String));
            SQLiteUtil.createCol(users, "profileFirstText", typeof (String));
            SQLiteUtil.createCol(users, "profileImage", typeof (String));
            SQLiteUtil.createCol(users, "profileFirstImage", typeof (String));
            SQLiteUtil.createCol(users, "webLoginKey", typeof(String));
            SQLiteUtil.createCol(users, "userFlags", typeof (Int32));
            SQLiteUtil.createCol(users, "godLevel", typeof (Int32));
            SQLiteUtil.createCol(users, "customType", typeof (String));
            SQLiteUtil.createCol(users, "partner", typeof (String));
            // Add in contraints
            users.PrimaryKey = new DataColumn[] {users.Columns["UUID"]};
            return users;
        }

        /// <summary>
        /// Create the "useragents" table
        /// </summary>
        /// <returns>Data Table</returns>
        private static DataTable createUserAgentsTable()
        {
            DataTable ua = new DataTable("useragents");
            // this is the UUID of the user
            SQLiteUtil.createCol(ua, "UUID", typeof (String));
            SQLiteUtil.createCol(ua, "agentIP", typeof (String));
            SQLiteUtil.createCol(ua, "agentPort", typeof (Int32));
            SQLiteUtil.createCol(ua, "agentOnline", typeof (Boolean));
            SQLiteUtil.createCol(ua, "sessionID", typeof (String));
            SQLiteUtil.createCol(ua, "secureSessionID", typeof (String));
            SQLiteUtil.createCol(ua, "regionID", typeof (String));
            SQLiteUtil.createCol(ua, "loginTime", typeof (Int32));
            SQLiteUtil.createCol(ua, "logoutTime", typeof (Int32));
            SQLiteUtil.createCol(ua, "currentRegion", typeof (String));
            SQLiteUtil.createCol(ua, "currentHandle", typeof (String));
            // vectors
            SQLiteUtil.createCol(ua, "currentPosX", typeof (Double));
            SQLiteUtil.createCol(ua, "currentPosY", typeof (Double));
            SQLiteUtil.createCol(ua, "currentPosZ", typeof (Double));
            // constraints
            ua.PrimaryKey = new DataColumn[] {ua.Columns["UUID"]};

            return ua;
        }

        /// <summary>
        /// Create the "userfriends" table
        /// </summary>
        /// <returns>Data Table</returns>
        private static DataTable createUserFriendsTable()
        {
            DataTable ua = new DataTable("userfriends");
            // table contains user <----> user relationship with perms
            SQLiteUtil.createCol(ua, "ownerID", typeof(String));
            SQLiteUtil.createCol(ua, "friendID", typeof(String));
            SQLiteUtil.createCol(ua, "friendPerms", typeof(Int32));
            SQLiteUtil.createCol(ua, "ownerPerms", typeof(Int32));
            SQLiteUtil.createCol(ua, "datetimestamp", typeof(Int32));

            return ua;
        }

        /// <summary>
        /// Create the "avatarappearance" table
        /// </summary>
        /// <returns>Data Table</returns>
        private static DataTable createAvatarAppearanceTable()
        {
            DataTable aa = new DataTable("avatarappearance");
            // table contains user appearance items

            SQLiteUtil.createCol(aa, "Owner", typeof(String));
            SQLiteUtil.createCol(aa, "BodyItem", typeof(String));
            SQLiteUtil.createCol(aa, "BodyAsset", typeof(String));
            SQLiteUtil.createCol(aa, "SkinItem", typeof(String));
            SQLiteUtil.createCol(aa, "SkinAsset", typeof(String));
            SQLiteUtil.createCol(aa, "HairItem", typeof(String));
            SQLiteUtil.createCol(aa, "HairAsset", typeof(String));
            SQLiteUtil.createCol(aa, "EyesItem", typeof(String));
            SQLiteUtil.createCol(aa, "EyesAsset", typeof(String));
            SQLiteUtil.createCol(aa, "ShirtItem", typeof(String));
            SQLiteUtil.createCol(aa, "ShirtAsset", typeof(String));
            SQLiteUtil.createCol(aa, "PantsItem", typeof(String));
            SQLiteUtil.createCol(aa, "PantsAsset", typeof(String));
            SQLiteUtil.createCol(aa, "ShoesItem", typeof(String));
            SQLiteUtil.createCol(aa, "ShoesAsset", typeof(String));
            SQLiteUtil.createCol(aa, "SocksItem", typeof(String));
            SQLiteUtil.createCol(aa, "SocksAsset", typeof(String));
            SQLiteUtil.createCol(aa, "JacketItem", typeof(String));
            SQLiteUtil.createCol(aa, "JacketAsset", typeof(String));
            SQLiteUtil.createCol(aa, "GlovesItem", typeof(String));
            SQLiteUtil.createCol(aa, "GlovesAsset", typeof(String));
            SQLiteUtil.createCol(aa, "UnderShirtItem", typeof(String));
            SQLiteUtil.createCol(aa, "UnderShirtAsset", typeof(String));
            SQLiteUtil.createCol(aa, "UnderPantsItem", typeof(String));
            SQLiteUtil.createCol(aa, "UnderPantsAsset", typeof(String));
            SQLiteUtil.createCol(aa, "SkirtItem", typeof(String));
            SQLiteUtil.createCol(aa, "SkirtAsset", typeof(String));

            SQLiteUtil.createCol(aa, "Texture", typeof (Byte[]));
            SQLiteUtil.createCol(aa, "VisualParams", typeof (Byte[]));

            SQLiteUtil.createCol(aa, "Serial", typeof(Int32));
            SQLiteUtil.createCol(aa, "AvatarHeight", typeof(Double));

            aa.PrimaryKey = new DataColumn[] { aa.Columns["Owner"] };

            return aa;
        }

        /***********************************************************************
         *
         *  Convert between ADO.NET <=> OpenSim Objects
         *
         *  These should be database independant
         *
         **********************************************************************/

        /// <summary>
        /// TODO: this doesn't work yet because something more
        /// interesting has to be done to actually get these values
        /// back out.  Not enough time to figure it out yet.
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private static UserProfileData buildUserProfile(DataRow row)
        {
            UserProfileData user = new UserProfileData();
            UUID tmp;
            UUID.TryParse((String)row["UUID"], out tmp);
            user.ID = tmp;
            user.FirstName = (String) row["username"];
            user.SurName = (String) row["surname"];
            user.Email = (row.IsNull("email")) ? "" : (String) row["email"];

            user.PasswordHash = (String) row["passwordHash"];
            user.PasswordSalt = (String) row["passwordSalt"];

            user.HomeRegionX = Convert.ToUInt32(row["homeRegionX"]);
            user.HomeRegionY = Convert.ToUInt32(row["homeRegionY"]);
            user.HomeLocation = new Vector3(
                Convert.ToSingle(row["homeLocationX"]),
                Convert.ToSingle(row["homeLocationY"]),
                Convert.ToSingle(row["homeLocationZ"])
                );
            user.HomeLookAt = new Vector3(
                Convert.ToSingle(row["homeLookAtX"]),
                Convert.ToSingle(row["homeLookAtY"]),
                Convert.ToSingle(row["homeLookAtZ"])
                );

            UUID regionID = UUID.Zero;
            UUID.TryParse(row["homeRegionID"].ToString(), out regionID); // it's ok if it doesn't work; just use UUID.Zero
            user.HomeRegionID = regionID;

            user.Created = Convert.ToInt32(row["created"]);
            user.LastLogin = Convert.ToInt32(row["lastLogin"]);
            user.UserInventoryURI = (String) row["userInventoryURI"];
            user.UserAssetURI = (String) row["userAssetURI"];
            user.CanDoMask = Convert.ToUInt32(row["profileCanDoMask"]);
            user.WantDoMask = Convert.ToUInt32(row["profileWantDoMask"]);
            user.AboutText = (String) row["profileAboutText"];
            user.FirstLifeAboutText = (String) row["profileFirstText"];
            UUID.TryParse((String)row["profileImage"], out tmp);
            user.Image = tmp;
            UUID.TryParse((String)row["profileFirstImage"], out tmp);
            user.FirstLifeImage = tmp;
            user.WebLoginKey = new UUID((String) row["webLoginKey"]);
            user.UserFlags = Convert.ToInt32(row["userFlags"]);
            user.GodLevel = Convert.ToInt32(row["godLevel"]);
            user.CustomType = row["customType"].ToString();
            user.Partner = new UUID((String) row["partner"]);

            return user;
        }

        /// <summary>
        /// Persist user profile data
        /// </summary>
        /// <param name="row"></param>
        /// <param name="user"></param>
        private void fillUserRow(DataRow row, UserProfileData user)
        {
            row["UUID"] = user.ID.ToString();
            row["username"] = user.FirstName;
            row["surname"] = user.SurName;
            row["email"] = user.Email;
            row["passwordHash"] = user.PasswordHash;
            row["passwordSalt"] = user.PasswordSalt;

            row["homeRegionX"] = user.HomeRegionX;
            row["homeRegionY"] = user.HomeRegionY;
            row["homeRegionID"] = user.HomeRegionID.ToString();
            row["homeLocationX"] = user.HomeLocation.X;
            row["homeLocationY"] = user.HomeLocation.Y;
            row["homeLocationZ"] = user.HomeLocation.Z;
            row["homeLookAtX"] = user.HomeLookAt.X;
            row["homeLookAtY"] = user.HomeLookAt.Y;
            row["homeLookAtZ"] = user.HomeLookAt.Z;

            row["created"] = user.Created;
            row["lastLogin"] = user.LastLogin;
            //TODO: Get rid of rootInventoryFolderID in a safe way.
            row["rootInventoryFolderID"] = UUID.Zero.ToString();
            row["userInventoryURI"] = user.UserInventoryURI;
            row["userAssetURI"] = user.UserAssetURI;
            row["profileCanDoMask"] = user.CanDoMask;
            row["profileWantDoMask"] = user.WantDoMask;
            row["profileAboutText"] = user.AboutText;
            row["profileFirstText"] = user.FirstLifeAboutText;
            row["profileImage"] = user.Image.ToString();
            row["profileFirstImage"] = user.FirstLifeImage.ToString();
            row["webLoginKey"] = user.WebLoginKey.ToString();
            row["userFlags"] = user.UserFlags;
            row["godLevel"] = user.GodLevel;
            row["customType"] = user.CustomType == null ? "" : user.CustomType;
            row["partner"] = user.Partner.ToString();

            // ADO.NET doesn't handle NULL very well
            foreach (DataColumn col in ds.Tables["users"].Columns)
            {
                if (row[col] == null)
                {
                    row[col] = String.Empty;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="user"></param>
        private void fillAvatarAppearanceRow(DataRow row, UUID user, AvatarAppearance appearance)
        {
            row["Owner"] = Util.ToRawUuidString(user);
            row["BodyItem"] = appearance.BodyItem.ToString();
            row["BodyAsset"] = appearance.BodyAsset.ToString();
            row["SkinItem"] = appearance.SkinItem.ToString();
            row["SkinAsset"] = appearance.SkinAsset.ToString();
            row["HairItem"] = appearance.HairItem.ToString();
            row["HairAsset"] = appearance.HairAsset.ToString();
            row["EyesItem"] = appearance.EyesItem.ToString();
            row["EyesAsset"] = appearance.EyesAsset.ToString();
            row["ShirtItem"] = appearance.ShirtItem.ToString();
            row["ShirtAsset"] = appearance.ShirtAsset.ToString();
            row["PantsItem"] = appearance.PantsItem.ToString();
            row["PantsAsset"] = appearance.PantsAsset.ToString();
            row["ShoesItem"] = appearance.ShoesItem.ToString();
            row["ShoesAsset"] = appearance.ShoesAsset.ToString();
            row["SocksItem"] = appearance.SocksItem.ToString();
            row["SocksAsset"] = appearance.SocksAsset.ToString();
            row["JacketItem"] = appearance.JacketItem.ToString();
            row["JacketAsset"] = appearance.JacketAsset.ToString();
            row["GlovesItem"] = appearance.GlovesItem.ToString();
            row["GlovesAsset"] = appearance.GlovesAsset.ToString();
            row["UnderShirtItem"] = appearance.UnderShirtItem.ToString();
            row["UnderShirtAsset"] = appearance.UnderShirtAsset.ToString();
            row["UnderPantsItem"] = appearance.UnderPantsItem.ToString();
            row["UnderPantsAsset"] = appearance.UnderPantsAsset.ToString();
            row["SkirtItem"] = appearance.SkirtItem.ToString();
            row["SkirtAsset"] = appearance.SkirtAsset.ToString();

            //  Used Base64String because for some reason it wont accept using Byte[] (which works in Region date)
            row["Texture"] = appearance.Texture.GetBytes();
            row["VisualParams"] = appearance.VisualParams;

            row["Serial"] = appearance.Serial;
            row["AvatarHeight"] = appearance.AvatarHeight;

            // ADO.NET doesn't handle NULL very well
            foreach (DataColumn col in ds.Tables["avatarappearance"].Columns)
            {
                if (row[col] == null)
                {
                    row[col] = String.Empty;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private static UserAgentData buildUserAgent(DataRow row)
        {
            UserAgentData ua = new UserAgentData();

            UUID tmp;
            UUID.TryParse((String)row["UUID"], out tmp);
            ua.ProfileID = tmp;
            ua.AgentIP = (String)row["agentIP"];
            ua.AgentPort = Convert.ToUInt32(row["agentPort"]);
            ua.AgentOnline = Convert.ToBoolean(row["agentOnline"]);
            ua.SessionID = new UUID((String) row["sessionID"]);
            ua.SecureSessionID = new UUID((String) row["secureSessionID"]);
            ua.InitialRegion = new UUID((String) row["regionID"]);
            ua.LoginTime = Convert.ToInt32(row["loginTime"]);
            ua.LogoutTime = Convert.ToInt32(row["logoutTime"]);
            ua.Region = new UUID((String) row["currentRegion"]);
            ua.Handle = Convert.ToUInt64(row["currentHandle"]);
            ua.Position = new Vector3(
                Convert.ToSingle(row["currentPosX"]),
                Convert.ToSingle(row["currentPosY"]),
                Convert.ToSingle(row["currentPosZ"])
                );
            ua.LookAt = new Vector3(
                Convert.ToSingle(row["currentLookAtX"]),
                Convert.ToSingle(row["currentLookAtY"]),
                Convert.ToSingle(row["currentLookAtZ"])
                );
            return ua;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="ua"></param>
        private static void fillUserAgentRow(DataRow row, UserAgentData ua)
        {
            row["UUID"] = ua.ProfileID.ToString();
            row["agentIP"] = ua.AgentIP;
            row["agentPort"] = ua.AgentPort;
            row["agentOnline"] = ua.AgentOnline;
            row["sessionID"] = ua.SessionID.ToString();
            row["secureSessionID"] = ua.SecureSessionID.ToString();
            row["regionID"] = ua.InitialRegion.ToString();
            row["loginTime"] = ua.LoginTime;
            row["logoutTime"] = ua.LogoutTime;
            row["currentRegion"] = ua.Region.ToString();
            row["currentHandle"] = ua.Handle.ToString();
            // vectors
            row["currentPosX"] = ua.Position.X;
            row["currentPosY"] = ua.Position.Y;
            row["currentPosZ"] = ua.Position.Z;
            row["currentLookAtX"] = ua.LookAt.X;
            row["currentLookAtY"] = ua.LookAt.Y;
            row["currentLookAtZ"] = ua.LookAt.Z;
        }

        /***********************************************************************
         *
         *  Database Binding functions
         *
         *  These will be db specific due to typing, and minor differences
         *  in databases.
         *
         **********************************************************************/

        /// <summary>
        ///
        /// </summary>
        /// <param name="da"></param>
        /// <param name="conn"></param>
        private void setupUserCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = SQLiteUtil.createInsertCommand("users", ds.Tables["users"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = SQLiteUtil.createUpdateCommand("users", "UUID=:UUID", ds.Tables["users"]);
            da.UpdateCommand.Connection = conn;

            SqliteCommand delete = new SqliteCommand("delete from users where UUID = :UUID");
            delete.Parameters.Add(SQLiteUtil.createSqliteParameter("UUID", typeof(String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        private void setupAgentCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = SQLiteUtil.createInsertCommand("useragents", ds.Tables["useragents"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = SQLiteUtil.createUpdateCommand("useragents", "UUID=:UUID", ds.Tables["useragents"]);
            da.UpdateCommand.Connection = conn;

            SqliteCommand delete = new SqliteCommand("delete from useragents where UUID = :ProfileID");
            delete.Parameters.Add(SQLiteUtil.createSqliteParameter("ProfileID", typeof(String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="daf"></param>
        /// <param name="conn"></param>
        private void setupUserFriendsCommands(SqliteDataAdapter daf, SqliteConnection conn)
        {
            daf.InsertCommand = SQLiteUtil.createInsertCommand("userfriends", ds.Tables["userfriends"]);
            daf.InsertCommand.Connection = conn;

            daf.UpdateCommand = SQLiteUtil.createUpdateCommand("userfriends", "ownerID=:ownerID and friendID=:friendID", ds.Tables["userfriends"]);
            daf.UpdateCommand.Connection = conn;

            SqliteCommand delete = new SqliteCommand("delete from userfriends where ownerID=:ownerID and friendID=:friendID");
            delete.Parameters.Add(SQLiteUtil.createSqliteParameter("ownerID", typeof(String)));
            delete.Parameters.Add(SQLiteUtil.createSqliteParameter("friendID", typeof(String)));
            delete.Connection = conn;
            daf.DeleteCommand = delete;

        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="daf"></param>
        /// <param name="conn"></param>
        private void setupAvatarAppearanceCommands(SqliteDataAdapter daa, SqliteConnection conn)
        {
            daa.InsertCommand = SQLiteUtil.createInsertCommand("avatarappearance", ds.Tables["avatarappearance"]);
            daa.InsertCommand.Connection = conn;

            daa.UpdateCommand = SQLiteUtil.createUpdateCommand("avatarappearance", "Owner=:Owner", ds.Tables["avatarappearance"]);
            daa.UpdateCommand.Connection = conn;

            SqliteCommand delete = new SqliteCommand("delete from avatarappearance where Owner=:Owner");
            delete.Parameters.Add(SQLiteUtil.createSqliteParameter("Owner", typeof(String)));
            delete.Connection = conn;
            daa.DeleteCommand = delete;
        }


        override public void ResetAttachments(UUID userID)
        {
        }

        override public void LogoutUsers(UUID regionID)
        {
        }
    }
}
