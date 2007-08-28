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
using System.IO;
using libsecondlife;
using OpenSim.Framework.Utilities;
using System.Data;
using System.Data.SqlTypes;
using Mono.Data.SqliteClient;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Data.SQLite
{
    /// <summary>
    /// A User storage interface for the DB4o database system
    /// </summary>
    public class SQLiteUserData : IUserData
    {
        /// <summary>
        /// The database manager
        /// </summary>

        /// <summary>
        /// Artificial constructor called upon plugin load
        /// </summary>
        private const string userSelect = "select * from users";
        private DataSet ds;
        private SqliteDataAdapter da;
        
        public void Initialise()
        {
            SqliteConnection conn = new SqliteConnection("URI=file:userprofiles.db,version=3");
            TestTables(conn);
            
            ds = new DataSet();
            da = new SqliteDataAdapter(new SqliteCommand(userSelect, conn));
 
            ds.Tables.Add(createUsersTable());
            ds.Tables.Add(createUserAgentsTable());
            
            setupUserCommands(da, conn);
            da.Fill(ds.Tables["users"]);
            
            return;
        }

        /// <summary>
        /// Loads a specified user profile from a UUID
        /// </summary>
        /// <param name="uuid">The users UUID</param>
        /// <returns>A user profile</returns>
        public UserProfileData getUserByUUID(LLUUID uuid)
        {
            DataRow row = ds.Tables["users"].Rows.Find(uuid);
            if(row != null) {
                UserProfileData user = buildUserProfile(row);
                row = ds.Tables["useragents"].Rows.Find(uuid);
                if(row != null) {
                    user.currentAgent = buildUserAgent(row);
                }
                return user;
            } else {
                return null;
            }
        }

        /// <summary>
        /// Returns a user by searching for its name
        /// </summary>
        /// <param name="name">The users account name</param>
        /// <returns>A matching users profile</returns>
        public UserProfileData getUserByName(string name)
        {
            return getUserByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        /// <summary>
        /// Returns a user by searching for its name
        /// </summary>
        /// <param name="fname">The first part of the users account name</param>
        /// <param name="lname">The second part of the users account name</param>
        /// <returns>A matching users profile</returns>
        public UserProfileData getUserByName(string fname, string lname)
        {
            string select = "surname = '" + lname + "' and username = '" + fname + "'";
            DataRow[] rows = ds.Tables["users"].Select(select);
            if(rows.Length > 0) {
                UserProfileData user = buildUserProfile(rows[0]);
                DataRow row = ds.Tables["useragents"].Rows.Find(user.UUID);
                if(row != null) {
                    user.currentAgent = buildUserAgent(row);
                }
                return user;
            } else {
                return null;
            }
        }

        /// <summary>
        /// Returns a user by UUID direct
        /// </summary>
        /// <param name="uuid">The users account ID</param>
        /// <returns>A matching users profile</returns>
        public UserAgentData getAgentByUUID(LLUUID uuid)
        {   
            try
            {
                return getUserByUUID(uuid).currentAgent;
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
        /// <returns>The users session agent</returns>
        public UserAgentData getAgentByName(string name)
        {
            return getAgentByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        /// <summary>
        /// Returns a session by account name
        /// </summary>
        /// <param name="fname">The first part of the users account name</param>
        /// <param name="lname">The second part of the users account name</param>
        /// <returns>A user agent</returns>
        public UserAgentData getAgentByName(string fname, string lname)
        {
            try
            {
                return getUserByName(fname,lname).currentAgent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a new user profile
        /// </summary>
        /// <param name="user">The profile to add to the database</param>
        public void addNewUserProfile(UserProfileData user)
        {
            DataTable users = ds.Tables["users"];
            DataRow row = users.Rows.Find(user.UUID); 
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

            if(user.currentAgent != null) {
                DataTable ua = ds.Tables["useragents"];
                row = ua.Rows.Find(user.UUID); 
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
            // save changes off to disk
            da.Update(ds, "users");
        }
      
        /// <summary>
        /// Creates a new user profile
        /// </summary>
        /// <param name="user">The profile to add to the database</param>
        /// <returns>True on success, false on error</returns>
        public bool updateUserProfile(UserProfileData user)
        {
            try {
                addNewUserProfile(user);
                return true;
            } catch (Exception) {
                return false;
            }
        }

        /// <summary>
        /// Creates a new user agent
        /// </summary>
        /// <param name="agent">The agent to add to the database</param>
        public void addNewUserAgent(UserAgentData agent)
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
        public bool moneyTransferRequest(LLUUID from, LLUUID to, uint amount)
        {
            return true;
        }

        /// <summary>
        /// Transfers inventory between two accounts
        /// </summary>
        /// <remarks>Move to inventory server</remarks>
        /// <param name="from">Senders account</param>
        /// <param name="to">Recievers account</param>
        /// <param name="item">Inventory item</param>
        /// <returns>Success?</returns>
        public bool inventoryTransferRequest(LLUUID from, LLUUID to, LLUUID item)
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
        public string getVersion()
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
        
        private void createCol(DataTable dt, string name, System.Type type)
        {
            DataColumn col = new DataColumn(name, type);
            dt.Columns.Add(col);
        }

        private DataTable createUsersTable()
        {
            DataTable users = new DataTable("users");

            createCol(users, "UUID", typeof(System.String));
            createCol(users, "username", typeof(System.String));
            createCol(users, "surname", typeof(System.String));
            createCol(users, "passwordHash", typeof(System.String));
            createCol(users, "passwordSalt", typeof(System.String));

            createCol(users, "homeRegion", typeof(System.UInt64));
            createCol(users, "homeLocationX", typeof(System.Double));
            createCol(users, "homeLocationY", typeof(System.Double));
            createCol(users, "homeLocationZ", typeof(System.Double));
            createCol(users, "homeLookAtX", typeof(System.Double));
            createCol(users, "homeLookAtY", typeof(System.Double));
            createCol(users, "homeLookAtZ", typeof(System.Double));
            createCol(users, "created", typeof(System.Int32));
            createCol(users, "lastLogin", typeof(System.Int32));
            createCol(users, "rootInventoryFolderID", typeof(System.String));
            createCol(users, "userInventoryURI", typeof(System.String));
            createCol(users, "userAssetURI", typeof(System.String));
            createCol(users, "profileCanDoMask", typeof(System.UInt32));
            createCol(users, "profileWantDoMask", typeof(System.UInt32));
            createCol(users, "profileAboutText", typeof(System.String));
            createCol(users, "profileFirstText", typeof(System.String));
            createCol(users, "profileImage", typeof(System.String));
            createCol(users, "profileFirstImage", typeof(System.String));
            // Add in contraints
            users.PrimaryKey = new DataColumn[] { users.Columns["UUID"] };
            return users;
        }

        private DataTable createUserAgentsTable()
        {
            DataTable ua = new DataTable("useragents");
            // this is the UUID of the user
            createCol(ua, "UUID", typeof(System.String));
            createCol(ua, "agentIP", typeof(System.String));
            createCol(ua, "agentPort", typeof(System.UInt32));
            createCol(ua, "agentOnline", typeof(System.Boolean));
            createCol(ua, "sessionID", typeof(System.String));
            createCol(ua, "secureSessionID", typeof(System.String));
            createCol(ua, "regionID", typeof(System.String));
            createCol(ua, "loginTime", typeof(System.Int32));
            createCol(ua, "logoutTime", typeof(System.Int32));
            createCol(ua, "currentRegion", typeof(System.String));
            createCol(ua, "currentHandle", typeof(System.UInt32));
            // vectors
            createCol(ua, "currentPosX", typeof(System.Double));
            createCol(ua, "currentPosY", typeof(System.Double));
            createCol(ua, "currentPosZ", typeof(System.Double));
            // constraints
            ua.PrimaryKey = new DataColumn[] { ua.Columns["UUID"] };

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
            user.UUID = new LLUUID((String)row["UUID"]);
            user.username = (string)row["username"];
            user.surname = (string)row["surname"];
            user.passwordHash = (string)row["passwordHash"];
            user.passwordSalt = (string)row["passwordSalt"];

            user.homeRegion = Convert.ToUInt64(row["homeRegion"]);
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
            user.rootInventoryFolderID = new LLUUID((string)row["rootInventoryFolderID"]);
            user.userInventoryURI = (string)row["userInventoryURI"];
            user.userAssetURI = (string)row["userAssetURI"];
            user.profileCanDoMask = Convert.ToUInt32(row["profileCanDoMask"]);
            user.profileWantDoMask = Convert.ToUInt32(row["profileWantDoMask"]);
            user.profileAboutText = (string)row["profileAboutText"];
            user.profileFirstText = (string)row["profileFirstText"];
            user.profileImage = new LLUUID((string)row["profileImage"]);
            user.profileFirstImage = new LLUUID((string)row["profileFirstImage"]);
            return user;
        }

        private void fillUserRow(DataRow row, UserProfileData user)
        {
            row["UUID"] = user.UUID;
            row["username"] = user.username;
            row["surname"] = user.surname;
            row["passwordHash"] = user.passwordHash;
            row["passwordSalt"] = user.passwordSalt;
            
            
            row["homeRegion"] = user.homeRegion;
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
        }

        private UserAgentData buildUserAgent(DataRow row)
        {
            UserAgentData ua = new UserAgentData();
            
            ua.UUID = new LLUUID((string)row["UUID"]);
            ua.agentIP = (string)row["agentIP"];
            ua.agentPort = Convert.ToUInt32(row["agentPort"]);
            ua.agentOnline = Convert.ToBoolean(row["agentOnline"]);
            ua.sessionID = new LLUUID((string)row["sessionID"]);
            ua.secureSessionID = new LLUUID((string)row["secureSessionID"]);
            ua.regionID = new LLUUID((string)row["regionID"]);
            ua.loginTime = Convert.ToInt32(row["loginTime"]);
            ua.logoutTime = Convert.ToInt32(row["logoutTime"]);
            ua.currentRegion = new LLUUID((string)row["currentRegion"]);
            ua.currentHandle = Convert.ToUInt32(row["currentHandle"]);
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
            row["agentIP"] =  ua.agentIP;
            row["agentPort"] =  ua.agentPort;
            row["agentOnline"] =  ua.agentOnline;
            row["sessionID"] =  ua.sessionID;
            row["secureSessionID"] = ua.secureSessionID;
            row["regionID"] = ua.regionID;
            row["loginTime"] = ua.loginTime;
            row["logoutTime"] = ua.logoutTime;
            row["currentRegion"] = ua.currentRegion;
            row["currentHandle"] = ua.currentHandle;
            // vectors
            row["currentPosX"] = ua.currentPos.X;
            row["currentPosY"] = ua.currentPos.Y;
            row["currentPosZ"] = ua.currentPos.Z;
        }

        /***********************************************************************
         *
         *  SQL Statement Creation Functions
         *
         *  These functions create SQL statements for update, insert, and create.
         *  They can probably be factored later to have a db independant
         *  portion and a db specific portion
         *
         **********************************************************************/

        private SqliteCommand createInsertCommand(string table, DataTable dt)
        {
            /**
             *  This is subtle enough to deserve some commentary.
             *  Instead of doing *lots* and *lots of hardcoded strings
             *  for database definitions we'll use the fact that
             *  realistically all insert statements look like "insert
             *  into A(b, c) values(:b, :c) on the parameterized query
             *  front.  If we just have a list of b, c, etc... we can
             *  generate these strings instead of typing them out.
             */
            string[] cols = new string[dt.Columns.Count];
            for (int i = 0; i < dt.Columns.Count; i++) {
                DataColumn col = dt.Columns[i];
                cols[i] = col.ColumnName;
            }

            string sql = "insert into " + table + "(";
            sql += String.Join(", ", cols);
            // important, the first ':' needs to be here, the rest get added in the join
            sql += ") values (:";
            sql += String.Join(", :", cols);
            sql += ")";
            SqliteCommand cmd = new SqliteCommand(sql);

            // this provides the binding for all our parameters, so
            // much less code than it used to be
            foreach (DataColumn col in dt.Columns) 
            {
                cmd.Parameters.Add(createSqliteParameter(col.ColumnName, col.DataType));
            }
            return cmd;
        }

        private SqliteCommand createUpdateCommand(string table, string pk, DataTable dt)
        {
            string sql = "update " + table + " set ";
            string subsql = "";
            foreach (DataColumn col in dt.Columns)
            {
                if (subsql.Length > 0)
                { // a map function would rock so much here
                    subsql += ", ";
                }
                subsql += col.ColumnName + "= :" + col.ColumnName;
            }
            sql += subsql;
            sql += " where " + pk;
            SqliteCommand cmd = new SqliteCommand(sql);

            // this provides the binding for all our parameters, so
            // much less code than it used to be

            foreach (DataColumn col in dt.Columns) 
            {
                cmd.Parameters.Add(createSqliteParameter(col.ColumnName, col.DataType));
            }
            return cmd;
        }


        private string defineTable(DataTable dt)
        {
            string sql = "create table " + dt.TableName + "(";
            string subsql = "";
            foreach (DataColumn col in dt.Columns)
            {
                if (subsql.Length > 0)
                { // a map function would rock so much here
                    subsql += ",\n";
                }
                subsql += col.ColumnName + " " + sqliteType(col.DataType);
                if(col == dt.PrimaryKey[0])
                {
                    subsql += " primary key";
                }
            }
            sql += subsql;
            sql += ")";
            return sql;
        }

        /***********************************************************************
         *
         *  Database Binding functions
         *
         *  These will be db specific due to typing, and minor differences
         *  in databases.
         *
         **********************************************************************/

        ///<summary>
        /// This is a convenience function that collapses 5 repetitive
        /// lines for defining SqliteParameters to 2 parameters:
        /// column name and database type.
        ///        
        /// It assumes certain conventions like :param as the param
        /// name to replace in parametrized queries, and that source
        /// version is always current version, both of which are fine
        /// for us.
        ///</summary>
        ///<returns>a built sqlite parameter</returns>
        private SqliteParameter createSqliteParameter(string name, System.Type type)
        {
            SqliteParameter param = new SqliteParameter();
            param.ParameterName = ":" + name;
            param.DbType = dbtypeFromType(type);
            param.SourceColumn = name;
            param.SourceVersion = DataRowVersion.Current;
            return param;
        }

        private void setupUserCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("users", ds.Tables["users"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("users", "UUID=:UUID", ds.Tables["users"]);
            da.UpdateCommand.Connection = conn;

            SqliteCommand delete = new SqliteCommand("delete from users where UUID = :UUID");
            delete.Parameters.Add(createSqliteParameter("UUID", typeof(System.String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }
        
        private void InitDB(SqliteConnection conn)
        {
            string createUsers = defineTable(createUsersTable());
            SqliteCommand pcmd = new SqliteCommand(createUsers, conn);
            conn.Open();
            pcmd.ExecuteNonQuery();
            conn.Close(); 
        }

        private bool TestTables(SqliteConnection conn)
        {
            SqliteCommand cmd = new SqliteCommand(userSelect, conn);
            SqliteDataAdapter pDa = new SqliteDataAdapter(cmd);
            DataSet tmpDS = new DataSet();
            try {
                pDa.Fill(tmpDS, "users");
            } catch (Mono.Data.SqliteClient.SqliteSyntaxException) {
                MainLog.Instance.Verbose("DATASTORE", "SQLite Database doesn't exist... creating");
                InitDB(conn);
            }
            return true;
        }

        /***********************************************************************
         *
         *  Type conversion functions
         *
         **********************************************************************/
        
        private DbType dbtypeFromType(Type type)
        {
            if (type == typeof(System.String)) {
                return DbType.String;
            } else if (type == typeof(System.Int32)) {
                return DbType.Int32;
            } else if (type == typeof(System.UInt32)) {
                return DbType.UInt32;
            } else if (type == typeof(System.Int64)) {
                return DbType.Int64;
            } else if (type == typeof(System.UInt64)) {
                return DbType.UInt64;
            } else if (type == typeof(System.Double)) {
                return DbType.Double;
            } else if (type == typeof(System.Byte[])) {
                return DbType.Binary;
            } else {
                return DbType.String;
            }
        }
        
        // this is something we'll need to implement for each db
        // slightly differently.
        private string sqliteType(Type type)
        {
            if (type == typeof(System.String)) {
                return "varchar(255)";
            } else if (type == typeof(System.Int32)) {
                return "integer";
            } else if (type == typeof(System.UInt32)) {
                return "integer";
            } else if (type == typeof(System.Int64)) {
                return "integer";
            } else if (type == typeof(System.UInt64)) {
                return "integer";
            } else if (type == typeof(System.Double)) {
                return "float";
            } else if (type == typeof(System.Byte[])) {
                return "blob";
            } else {
                return "string";
            }
        }
    }
}
