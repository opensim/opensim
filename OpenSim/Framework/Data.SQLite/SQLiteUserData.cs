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
                return buildUserProfile(row);
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
                return buildUserProfile(rows[0]);
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
        }
      
        /// <summary>
        /// Creates a new user profile
        /// </summary>
        /// <param name="user">The profile to add to the database</param>
        /// <returns>True on success, false on error</returns>
        public bool updateUserProfile(UserProfileData user)
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
            return true;
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

            createCol(users, "homeRegion", typeof(System.UInt32));
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

            user.homeRegion = Convert.ToUInt32(row["homeRegion"]);
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

//         private PrimitiveBaseShape buildShape(DataRow row)
//         {
//             PrimitiveBaseShape s = new PrimitiveBaseShape();
//             s.Scale = new LLVector3(
//                                     Convert.ToSingle(row["ScaleX"]),
//                                     Convert.ToSingle(row["ScaleY"]),
//                                     Convert.ToSingle(row["ScaleZ"])
//                                     );
//             // paths
//             s.PCode = Convert.ToByte(row["PCode"]);
//             s.PathBegin = Convert.ToUInt16(row["PathBegin"]);
//             s.PathEnd = Convert.ToUInt16(row["PathEnd"]);
//             s.PathScaleX = Convert.ToByte(row["PathScaleX"]);
//             s.PathScaleY = Convert.ToByte(row["PathScaleY"]);
//             s.PathShearX = Convert.ToByte(row["PathShearX"]);
//             s.PathShearY = Convert.ToByte(row["PathShearY"]);
//             s.PathSkew = Convert.ToSByte(row["PathSkew"]);
//             s.PathCurve = Convert.ToByte(row["PathCurve"]);
//             s.PathRadiusOffset = Convert.ToSByte(row["PathRadiusOffset"]);
//             s.PathRevolutions = Convert.ToByte(row["PathRevolutions"]);
//             s.PathTaperX = Convert.ToSByte(row["PathTaperX"]);
//             s.PathTaperY = Convert.ToSByte(row["PathTaperY"]);
//             s.PathTwist = Convert.ToSByte(row["PathTwist"]);
//             s.PathTwistBegin = Convert.ToSByte(row["PathTwistBegin"]);
//             // profile
//             s.ProfileBegin = Convert.ToUInt16(row["ProfileBegin"]);
//             s.ProfileEnd = Convert.ToUInt16(row["ProfileEnd"]);
//             s.ProfileCurve = Convert.ToByte(row["ProfileCurve"]);
//             s.ProfileHollow = Convert.ToByte(row["ProfileHollow"]);
//             // text TODO: this isn't right] = but I'm not sure the right
//             // way to specify this as a blob atm
//             s.TextureEntry = (byte[])row["Texture"];
//             s.ExtraParams = (byte[])row["ExtraParams"];
//             // System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();
//             //             string texture = encoding.GetString((Byte[])row["Texture"]);
//             //             if (!texture.StartsWith("<"))
//             //             {
//             //                 //here so that we can still work with old format database files (ie from before I added xml serialization)
//             //                  LLObject.TextureEntry textureEntry = null;
//             //                 textureEntry = new LLObject.TextureEntry(new LLUUID(texture));
//             //                 s.TextureEntry = textureEntry.ToBytes();
//             //             }
//             //             else
//             //             {
//             //                 TextureBlock textureEntry = TextureBlock.FromXmlString(texture);
//             //                 s.TextureEntry = textureEntry.TextureData;
//             //                 s.ExtraParams = textureEntry.ExtraParams;
//             // }
            
//             return s;
//         }
        
//         private void fillShapeRow(DataRow row, SceneObjectPart prim)
//         {
//             PrimitiveBaseShape s = prim.Shape;
//             row["UUID"] = prim.UUID;
//             // shape is an enum
//             row["Shape"] = 0;
//             // vectors
//             row["ScaleX"] = s.Scale.X;
//             row["ScaleY"] = s.Scale.Y;
//             row["ScaleZ"] = s.Scale.Z;
//             // paths
//             row["PCode"] = s.PCode;
//             row["PathBegin"] = s.PathBegin;
//             row["PathEnd"] = s.PathEnd;
//             row["PathScaleX"] = s.PathScaleX;
//             row["PathScaleY"] = s.PathScaleY;
//             row["PathShearX"] = s.PathShearX;
//             row["PathShearY"] = s.PathShearY;
//             row["PathSkew"] = s.PathSkew;
//             row["PathCurve"] = s.PathCurve;
//             row["PathRadiusOffset"] = s.PathRadiusOffset;
//             row["PathRevolutions"] = s.PathRevolutions;
//             row["PathTaperX"] = s.PathTaperX;
//             row["PathTaperY"] = s.PathTaperY;
//             row["PathTwist"] = s.PathTwist;
//             row["PathTwistBegin"] = s.PathTwistBegin;
//             // profile
//             row["ProfileBegin"] = s.ProfileBegin;
//             row["ProfileEnd"] = s.ProfileEnd;
//             row["ProfileCurve"] = s.ProfileCurve;
//             row["ProfileHollow"] = s.ProfileHollow;
//             // text TODO: this isn't right] = but I'm not sure the right
//             // way to specify this as a blob atm

//             // And I couldn't work out how to save binary data either
//             // seems that the texture colum is being treated as a string in the Datarow 
//             // if you do a .getType() on it, it returns string, while the other columns return correct type
//             // MW[10-08-07]
//             // Added following xml hack but not really ideal , also ExtraParams isn't currently part of the database
//             // am a bit worried about adding it now as some people will have old format databases, so for now including that data in this xml data
//             // MW[17-08-07]
//             row["Texture"] = s.TextureEntry;
//             row["ExtraParams"] = s.ExtraParams;
//             // TextureBlock textureBlock = new TextureBlock(s.TextureEntry);
//             //             textureBlock.ExtraParams = s.ExtraParams;
//             //             System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();
//             // row["Texture"] = encoding.GetBytes(textureBlock.ToXMLString());
//         }

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
