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
using System.IO;
using System.Reflection;
using log4net;
using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A MySQL Database manager
    /// </summary>
    public class MySQLManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The database connection object
        /// </summary>
        private MySqlConnection dbcon;

        /// <summary>
        /// Connection string for ADO.net
        /// </summary>
        private string connectionString;

        private const string m_waitTimeoutSelect = "select @@wait_timeout";

        /// <summary>
        /// Wait timeout for our connection in ticks.
        /// </summary>
        private long m_waitTimeout;

        /// <summary>
        /// Make our storage of the timeout this amount smaller than it actually is, to give us a margin on long
        /// running database operations.
        /// </summary>
        private long m_waitTimeoutLeeway = 60 * TimeSpan.TicksPerSecond;

        /// <summary>
        /// Holds the last tick time that the connection was used.
        /// </summary>
        private long m_lastConnectionUse;

        /// <summary>
        /// Initialises and creates a new MySQL connection and maintains it.
        /// </summary>
        /// <param name="hostname">The MySQL server being connected to</param>
        /// <param name="database">The name of the MySQL database being used</param>
        /// <param name="username">The username logging into the database</param>
        /// <param name="password">The password for the user logging in</param>
        /// <param name="cpooling">Whether to use connection pooling or not, can be one of the following: 'yes', 'true', 'no' or 'false', if unsure use 'false'.</param>
        /// <param name="port">The MySQL server port</param>
        public MySQLManager(string hostname, string database, string username, string password, string cpooling,
                            string port)
        {
            string s = "Server=" + hostname + ";Port=" + port + ";Database=" + database + ";User ID=" +
                username + ";Password=" + password + ";Pooling=" + cpooling + ";";

            Initialise(s);
        }

        /// <summary>
        /// Initialises and creates a new MySQL connection and maintains it.
        /// </summary>
        /// <param name="connect">connectionString</param>
        public MySQLManager(String connect)
        {
            Initialise(connect);
        }

        /// <summary>
        /// Initialises and creates a new MySQL connection and maintains it.
        /// </summary>
        /// <param name="connect">connectionString</param>
        public void Initialise(String connect)
        {
            try
            {
                connectionString = connect;
                dbcon = new MySqlConnection(connectionString);

                try
                {
                    dbcon.Open();
                }
                catch(Exception e)
                {
                    throw new Exception("Connection error while using connection string ["+connectionString+"]", e);
                }

                m_log.Info("[MYSQL]: Connection established");
                GetWaitTimeout();
            }
            catch (Exception e)
            {
                throw new Exception("Error initialising MySql Database: " + e.ToString());
            }
        }

        /// <summary>
        /// Get the wait_timeout value for our connection
        /// </summary>
        protected void GetWaitTimeout()
        {
            using (MySqlCommand cmd = new MySqlCommand(m_waitTimeoutSelect, dbcon))
            {
                using (MySqlDataReader dbReader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (dbReader.Read())
                    {
                        m_waitTimeout
                            = Convert.ToInt32(dbReader["@@wait_timeout"]) * TimeSpan.TicksPerSecond + m_waitTimeoutLeeway;
                    }
                }
            }

            m_lastConnectionUse = DateTime.Now.Ticks;

            m_log.DebugFormat(
                "[REGION DB]: Connection wait timeout {0} seconds", m_waitTimeout / TimeSpan.TicksPerSecond);
        }

        /// <summary>
        /// Should be called before any db operation.  This checks to see if the connection has not timed out
        /// </summary>
        public void CheckConnection()
        {
            //m_log.Debug("[REGION DB]: Checking connection");

            long timeNow = DateTime.Now.Ticks;
            if (timeNow - m_lastConnectionUse > m_waitTimeout || dbcon.State != ConnectionState.Open)
            {
                m_log.DebugFormat("[REGION DB]: Database connection has gone away - reconnecting");
                Reconnect();
            }

            // Strictly, we should set this after the actual db operation.  But it's more convenient to set here rather
            // than require the code to call another method - the timeout leeway should be large enough to cover the
            // inaccuracy.
            m_lastConnectionUse = timeNow;
        }

        /// <summary>
        /// Get the connection being used
        /// </summary>
        /// <returns>MySqlConnection Object</returns>
        public MySqlConnection Connection
        {
            get { return dbcon; }
        }

        /// <summary>
        /// Shuts down the database connection
        /// </summary>
        public void Close()
        {
            dbcon.Close();
            dbcon = null;
        }

        /// <summary>
        /// Reconnects to the database
        /// </summary>
        public void Reconnect()
        {
            m_log.Info("[REGION DB] Reconnecting database");

            lock (dbcon)
            {
                try
                {
                    // Close the DB connection
                    dbcon.Close();
                    // Try reopen it
                    dbcon = new MySqlConnection(connectionString);
                    dbcon.Open();
                }
                catch (Exception e)
                {
                    m_log.Error("Unable to reconnect to database " + e.ToString());
                }
            }
        }

        /// <summary>
        /// Returns the version of this DB provider
        /// </summary>
        /// <returns>A string containing the DB provider</returns>
        public string getVersion()
        {
            Module module = GetType().Module;
            // string dllName = module.Assembly.ManifestModule.Name;
            Version dllVersion = module.Assembly.GetName().Version;

            return
                string.Format("{0}.{1}.{2}.{3}", dllVersion.Major, dllVersion.Minor, dllVersion.Build,
                        dllVersion.Revision);
        }

        /// <summary>
        /// Extract a named string resource from the embedded resources
        /// </summary>
        /// <param name="name">name of embedded resource</param>
        /// <returns>string contained within the embedded resource</returns>
        private string getResourceString(string name)
        {
            Assembly assem = GetType().Assembly;
            string[] names = assem.GetManifestResourceNames();

            foreach (string s in names)
            {
                if (s.EndsWith(name))
                {
                    using (Stream resource = assem.GetManifestResourceStream(s))
                    {
                        using (StreamReader resourceReader = new StreamReader(resource))
                        {
                            string resourceString = resourceReader.ReadToEnd();
                            return resourceString;
                        }
                    }
                }
            }
            throw new Exception(string.Format("Resource '{0}' was not found", name));
        }

        /// <summary>
        /// Execute a SQL statement stored in a resource, as a string
        /// </summary>
        /// <param name="name">name of embedded resource</param>
        public void ExecuteResourceSql(string name)
        {
            CheckConnection();
            MySqlCommand cmd = new MySqlCommand(getResourceString(name), dbcon);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Execute a MySqlCommand
        /// </summary>
        /// <param name="sql">sql string to execute</param>
        public void ExecuteSql(string sql)
        {
            CheckConnection();
            MySqlCommand cmd = new MySqlCommand(sql, dbcon);
            cmd.ExecuteNonQuery();
        }

        public void ExecuteParameterizedSql(string sql, Dictionary<string, string> parameters)
        {
            CheckConnection();

            MySqlCommand cmd = (MySqlCommand)dbcon.CreateCommand();
            cmd.CommandText = sql;
            foreach (KeyValuePair<string, string> param in parameters)
            {
                cmd.Parameters.AddWithValue(param.Key, param.Value);
            }
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Given a list of tables, return the version of the tables, as seen in the database
        /// </summary>
        /// <param name="tableList"></param>
        public void GetTableVersion(Dictionary<string, string> tableList)
        {
            lock (dbcon)
            {
                CheckConnection();

                using (MySqlCommand tablesCmd = new MySqlCommand(
                    "SELECT TABLE_NAME, TABLE_COMMENT FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=?dbname",
                    dbcon))
                {
                    tablesCmd.Parameters.AddWithValue("?dbname", dbcon.Database);

                    using (MySqlDataReader tables = tablesCmd.ExecuteReader())
                    {
                        while (tables.Read())
                        {
                            try
                            {
                                string tableName = (string)tables["TABLE_NAME"];
                                string comment = (string)tables["TABLE_COMMENT"];
                                if (tableList.ContainsKey(tableName))
                                {
                                    tableList[tableName] = comment;
                                }
                            }
                            catch (Exception e)
                            {
                                m_log.Error(e.Message, e);
                            }
                        }
                    }
                }
            }
        }

        // TODO: at some time this code should be cleaned up

        /// <summary>
        /// Runs a query with protection against SQL Injection by using parameterised input.
        /// </summary>
        /// <param name="sql">The SQL string - replace any variables such as WHERE x = "y" with WHERE x = @y</param>
        /// <param name="parameters">The parameters - index so that @y is indexed as 'y'</param>
        /// <returns>A MySQL DB Command</returns>
        public IDbCommand Query(string sql, Dictionary<string, object> parameters)
        {
            try
            {
                CheckConnection(); // Not sure if this one is necessary

                MySqlCommand dbcommand = (MySqlCommand)dbcon.CreateCommand();
                dbcommand.CommandText = sql;
                foreach (KeyValuePair<string, object> param in parameters)
                {
                    dbcommand.Parameters.AddWithValue(param.Key, param.Value);
                }

                return (IDbCommand)dbcommand;
            }
            catch (Exception e)
            {
                // Return null if it fails.
                m_log.Error("Failed during Query generation: " + e.Message, e);
                return null;
            }
        }

        /// <summary>
        /// Reads a region row from a database reader
        /// </summary>
        /// <param name="reader">An active database reader</param>
        /// <returns>A region profile</returns>
        public RegionProfileData readSimRow(IDataReader reader)
        {
            RegionProfileData retval = new RegionProfileData();

            if (reader.Read())
            {
                // Region Main gotta-have-or-we-return-null parts
                UInt64 tmp64;
                if (!UInt64.TryParse(reader["regionHandle"].ToString(), out tmp64))
                {
                    return null;
                }
                else
                {
                    retval.regionHandle = tmp64;
                }
                UUID tmp_uuid;
                if (!UUID.TryParse((string)reader["uuid"], out tmp_uuid))
                {
                    return null;
                }
                else
                {
                    retval.UUID = tmp_uuid;
                }

                // non-critical parts
                retval.regionName = (string)reader["regionName"];
                retval.originUUID = new UUID((string) reader["originUUID"]);

                // Secrets
                retval.regionRecvKey = (string) reader["regionRecvKey"];
                retval.regionSecret = (string) reader["regionSecret"];
                retval.regionSendKey = (string) reader["regionSendKey"];

                // Region Server
                retval.regionDataURI = (string) reader["regionDataURI"];
                retval.regionOnline = false; // Needs to be pinged before this can be set.
                retval.serverIP = (string) reader["serverIP"];
                retval.serverPort = (uint) reader["serverPort"];
                retval.serverURI = (string) reader["serverURI"];
                retval.httpPort = Convert.ToUInt32(reader["serverHttpPort"].ToString());
                retval.remotingPort = Convert.ToUInt32(reader["serverRemotingPort"].ToString());

                // Location
                retval.regionLocX = Convert.ToUInt32(reader["locX"].ToString());
                retval.regionLocY = Convert.ToUInt32(reader["locY"].ToString());
                retval.regionLocZ = Convert.ToUInt32(reader["locZ"].ToString());

                // Neighbours - 0 = No Override
                retval.regionEastOverrideHandle = Convert.ToUInt64(reader["eastOverrideHandle"].ToString());
                retval.regionWestOverrideHandle = Convert.ToUInt64(reader["westOverrideHandle"].ToString());
                retval.regionSouthOverrideHandle = Convert.ToUInt64(reader["southOverrideHandle"].ToString());
                retval.regionNorthOverrideHandle = Convert.ToUInt64(reader["northOverrideHandle"].ToString());

                // Assets
                retval.regionAssetURI = (string) reader["regionAssetURI"];
                retval.regionAssetRecvKey = (string) reader["regionAssetRecvKey"];
                retval.regionAssetSendKey = (string) reader["regionAssetSendKey"];

                // Userserver
                retval.regionUserURI = (string) reader["regionUserURI"];
                retval.regionUserRecvKey = (string) reader["regionUserRecvKey"];
                retval.regionUserSendKey = (string) reader["regionUserSendKey"];

                // World Map Addition
                UUID.TryParse((string)reader["regionMapTexture"], out retval.regionMapTextureID);
                UUID.TryParse((string)reader["owner_uuid"], out retval.owner_uuid);
                retval.maturity = Convert.ToUInt32(reader["access"]);
            }
            else
            {
                return null;
            }
            return retval;
        }

        /// <summary>
        /// Reads a reservation row from a database reader
        /// </summary>
        /// <param name="reader">An active database reader</param>
        /// <returns>A reservation data object</returns>
        public ReservationData readReservationRow(IDataReader reader)
        {
            ReservationData retval = new ReservationData();
            if (reader.Read())
            {
                retval.gridRecvKey = (string) reader["gridRecvKey"];
                retval.gridSendKey = (string) reader["gridSendKey"];
                retval.reservationCompany = (string) reader["resCompany"];
                retval.reservationMaxX = Convert.ToInt32(reader["resXMax"].ToString());
                retval.reservationMaxY = Convert.ToInt32(reader["resYMax"].ToString());
                retval.reservationMinX = Convert.ToInt32(reader["resXMin"].ToString());
                retval.reservationMinY = Convert.ToInt32(reader["resYMin"].ToString());
                retval.reservationName = (string) reader["resName"];
                retval.status = Convert.ToInt32(reader["status"].ToString()) == 1;
                UUID tmp;
                UUID.TryParse((string) reader["userUUID"], out tmp);
                retval.userUUID = tmp;
            }
            else
            {
                return null;
            }
            return retval;
        }

        /// <summary>
        /// Reads an agent row from a database reader
        /// </summary>
        /// <param name="reader">An active database reader</param>
        /// <returns>A user session agent</returns>
        public UserAgentData readAgentRow(IDataReader reader)
        {
            UserAgentData retval = new UserAgentData();

            if (reader.Read())
            {
                // Agent IDs
                UUID tmp;
                if (!UUID.TryParse((string)reader["UUID"], out tmp))
                    return null;
                retval.ProfileID = tmp;

                UUID.TryParse((string) reader["sessionID"], out tmp);
                retval.SessionID = tmp;

                UUID.TryParse((string)reader["secureSessionID"], out tmp);
                retval.SecureSessionID = tmp;

                // Agent Who?
                retval.AgentIP = (string) reader["agentIP"];
                retval.AgentPort = Convert.ToUInt32(reader["agentPort"].ToString());
                retval.AgentOnline = Convert.ToBoolean(Convert.ToInt16(reader["agentOnline"].ToString()));

                // Login/Logout times (UNIX Epoch)
                retval.LoginTime = Convert.ToInt32(reader["loginTime"].ToString());
                retval.LogoutTime = Convert.ToInt32(reader["logoutTime"].ToString());

                // Current position
                retval.Region = new UUID((string)reader["currentRegion"]);
                retval.Handle = Convert.ToUInt64(reader["currentHandle"].ToString());
                Vector3 tmp_v;
                Vector3.TryParse((string) reader["currentPos"], out tmp_v);
                retval.Position = tmp_v;
                Vector3.TryParse((string)reader["currentLookAt"], out tmp_v);
                retval.LookAt = tmp_v;
            }
            else
            {
                return null;
            }
            return retval;
        }

        /// <summary>
        /// Reads a user profile from an active data reader
        /// </summary>
        /// <param name="reader">An active database reader</param>
        /// <returns>A user profile</returns>
        public UserProfileData readUserRow(IDataReader reader)
        {
            UserProfileData retval = new UserProfileData();

            if (reader.Read())
            {
                UUID id;
                if (!UUID.TryParse((string)reader["UUID"], out id))
                    return null;

                retval.ID = id;
                retval.FirstName = (string) reader["username"];
                retval.SurName = (string) reader["lastname"];
                retval.Email = (reader.IsDBNull(reader.GetOrdinal("email"))) ? "" : (string) reader["email"];

                retval.PasswordHash = (string) reader["passwordHash"];
                retval.PasswordSalt = (string) reader["passwordSalt"];

                retval.HomeRegion = Convert.ToUInt64(reader["homeRegion"].ToString());
                retval.HomeLocation = new Vector3(
                    Convert.ToSingle(reader["homeLocationX"].ToString()),
                    Convert.ToSingle(reader["homeLocationY"].ToString()),
                    Convert.ToSingle(reader["homeLocationZ"].ToString()));
                retval.HomeLookAt = new Vector3(
                    Convert.ToSingle(reader["homeLookAtX"].ToString()),
                    Convert.ToSingle(reader["homeLookAtY"].ToString()),
                    Convert.ToSingle(reader["homeLookAtZ"].ToString()));

                UUID regionID = UUID.Zero;
                UUID.TryParse(reader["homeRegionID"].ToString(), out regionID); // it's ok if it doesn't work; just use UUID.Zero
                retval.HomeRegionID = regionID;

                retval.Created = Convert.ToInt32(reader["created"].ToString());
                retval.LastLogin = Convert.ToInt32(reader["lastLogin"].ToString());
                
                retval.UserInventoryURI = (string) reader["userInventoryURI"];
                retval.UserAssetURI = (string) reader["userAssetURI"];

                retval.CanDoMask = Convert.ToUInt32(reader["profileCanDoMask"].ToString());
                retval.WantDoMask = Convert.ToUInt32(reader["profileWantDoMask"].ToString());

                if (reader.IsDBNull(reader.GetOrdinal("profileAboutText")))
                    retval.AboutText = "";
                else
                    retval.AboutText = (string) reader["profileAboutText"];

                if (reader.IsDBNull(reader.GetOrdinal("profileFirstText")))
                    retval.FirstLifeAboutText = "";
                else
                    retval.FirstLifeAboutText = (string)reader["profileFirstText"];

                if (reader.IsDBNull(reader.GetOrdinal("profileImage")))
                    retval.Image = UUID.Zero;
                else {
                    UUID tmp;
                    UUID.TryParse((string)reader["profileImage"], out tmp);
                    retval.Image = tmp;
                }

                if (reader.IsDBNull(reader.GetOrdinal("profileFirstImage")))
                    retval.FirstLifeImage = UUID.Zero;
                else {
                    UUID tmp;
                    UUID.TryParse((string)reader["profileFirstImage"], out tmp);
                    retval.FirstLifeImage = tmp;
                }

                if (reader.IsDBNull(reader.GetOrdinal("webLoginKey")))
                {
                    retval.WebLoginKey = UUID.Zero;
                }
                else
                {
                    UUID tmp;
                    UUID.TryParse((string)reader["webLoginKey"], out tmp);
                    retval.WebLoginKey = tmp;
                }

                retval.UserFlags = Convert.ToInt32(reader["userFlags"].ToString());
                retval.GodLevel = Convert.ToInt32(reader["godLevel"].ToString());
                if (reader.IsDBNull(reader.GetOrdinal("customType")))
                    retval.CustomType = "";
                else
                    retval.CustomType = reader["customType"].ToString();

                if (reader.IsDBNull(reader.GetOrdinal("partner")))
                {
                    retval.Partner = UUID.Zero;
                }
                else
                {
                    UUID tmp;
                    UUID.TryParse((string)reader["partner"], out tmp);
                    retval.Partner = tmp;
                }
            }
            else
            {
                return null;
            }
            return retval;
        }

        /// <summary>
        /// Reads an avatar appearence from an active data reader
        /// </summary>
        /// <param name="reader">An active database reader</param>
        /// <returns>An avatar appearence</returns>
        public AvatarAppearance readAppearanceRow(IDataReader reader)
        {
            AvatarAppearance appearance = null;
            if (reader.Read())
            {
                appearance = new AvatarAppearance();
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
            }
            return appearance;
        }

        // Read attachment list from data reader
        public Hashtable readAttachments(IDataReader r)
        {
            Hashtable ret = new Hashtable();

            while (r.Read())
            {
                int attachpoint = Convert.ToInt32(r["attachpoint"]);
                if (ret.ContainsKey(attachpoint))
                    continue;
                Hashtable item = new Hashtable();
                item.Add("item", r["item"].ToString());
                item.Add("asset", r["asset"].ToString());

                ret.Add(attachpoint, item);
            }

            return ret;
        }

        /// <summary>
        /// Inserts a new row into the log database
        /// </summary>
        /// <param name="serverDaemon">The daemon which triggered this event</param>
        /// <param name="target">Who were we operating on when this occured (region UUID, user UUID, etc)</param>
        /// <param name="methodCall">The method call where the problem occured</param>
        /// <param name="arguments">The arguments passed to the method</param>
        /// <param name="priority">How critical is this?</param>
        /// <param name="logMessage">Extra message info</param>
        /// <returns>Saved successfully?</returns>
        public bool insertLogRow(string serverDaemon, string target, string methodCall, string arguments, int priority,
                                 string logMessage)
        {
            string sql = "INSERT INTO logs (`target`, `server`, `method`, `arguments`, `priority`, `message`) VALUES ";
            sql += "(?target, ?server, ?method, ?arguments, ?priority, ?message)";

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["?server"] = serverDaemon;
            parameters["?target"] = target;
            parameters["?method"] = methodCall;
            parameters["?arguments"] = arguments;
            parameters["?priority"] = priority.ToString();
            parameters["?message"] = logMessage;

            bool returnval = false;

            try
            {
                IDbCommand result = Query(sql, parameters);

                if (result.ExecuteNonQuery() == 1)
                    returnval = true;

                result.Dispose();
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }

            return returnval;
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
        /// <param name="homeRegionID"> The UUID of the user's home region</param>
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
        /// <param name="webLoginKey">Ignored</param>
        /// <returns>Success?</returns>
        public bool insertUserRow(UUID uuid, string username, string lastname, string email, string passwordHash,
                                  string passwordSalt, UInt64 homeRegion, UUID homeRegionID, float homeLocX, float homeLocY, float homeLocZ,
                                  float homeLookAtX, float homeLookAtY, float homeLookAtZ, int created, int lastlogin,
                                  string inventoryURI, string assetURI, uint canDoMask, uint wantDoMask,
                                  string aboutText, string firstText,
                                  UUID profileImage, UUID firstImage, UUID webLoginKey, int userFlags, int godLevel, string customType, UUID partner)
        {
            m_log.Debug("[MySQLManager]: Fetching profile for " + uuid.ToString());
            string sql =
                "INSERT INTO users (`UUID`, `username`, `lastname`, `email`, `passwordHash`, `passwordSalt`, `homeRegion`, `homeRegionID`, ";
            sql +=
                "`homeLocationX`, `homeLocationY`, `homeLocationZ`, `homeLookAtX`, `homeLookAtY`, `homeLookAtZ`, `created`, ";
            sql +=
                "`lastLogin`, `userInventoryURI`, `userAssetURI`, `profileCanDoMask`, `profileWantDoMask`, `profileAboutText`, ";
            sql += "`profileFirstText`, `profileImage`, `profileFirstImage`, `webLoginKey`, `userFlags`, `godLevel`, `customType`, `partner`) VALUES ";

            sql += "(?UUID, ?username, ?lastname, ?email, ?passwordHash, ?passwordSalt, ?homeRegion, ?homeRegionID, ";
            sql +=
                "?homeLocationX, ?homeLocationY, ?homeLocationZ, ?homeLookAtX, ?homeLookAtY, ?homeLookAtZ, ?created, ";
            sql +=
                "?lastLogin, ?userInventoryURI, ?userAssetURI, ?profileCanDoMask, ?profileWantDoMask, ?profileAboutText, ";
            sql += "?profileFirstText, ?profileImage, ?profileFirstImage, ?webLoginKey, ?userFlags, ?godLevel, ?customType, ?partner)";

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["?UUID"] = uuid.ToString();
            parameters["?username"] = username;
            parameters["?lastname"] = lastname;
            parameters["?email"] = email;
            parameters["?passwordHash"] = passwordHash;
            parameters["?passwordSalt"] = passwordSalt;
            parameters["?homeRegion"] = homeRegion;
            parameters["?homeRegionID"] = homeRegionID.ToString();
            parameters["?homeLocationX"] = homeLocX;
            parameters["?homeLocationY"] = homeLocY;
            parameters["?homeLocationZ"] = homeLocZ;
            parameters["?homeLookAtX"] = homeLookAtX;
            parameters["?homeLookAtY"] = homeLookAtY;
            parameters["?homeLookAtZ"] = homeLookAtZ;
            parameters["?created"] = created;
            parameters["?lastLogin"] = lastlogin;
            parameters["?userInventoryURI"] = inventoryURI;
            parameters["?userAssetURI"] = assetURI;
            parameters["?profileCanDoMask"] = canDoMask;
            parameters["?profileWantDoMask"] = wantDoMask;
            parameters["?profileAboutText"] = aboutText;
            parameters["?profileFirstText"] = firstText;
            parameters["?profileImage"] = profileImage.ToString();
            parameters["?profileFirstImage"] = firstImage.ToString();
            parameters["?webLoginKey"] = webLoginKey.ToString();
            parameters["?userFlags"] = userFlags;
            parameters["?godLevel"] = godLevel;
            parameters["?customType"] = customType == null ? "" : customType;
            parameters["?partner"] = partner.ToString();
            bool returnval = false;
                
            try
            {
                IDbCommand result = Query(sql, parameters);

                if (result.ExecuteNonQuery() == 1)
                    returnval = true;

                result.Dispose();
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }

            //m_log.Debug("[MySQLManager]: Fetch user retval == " + returnval.ToString());
            return returnval;
        }

        /// <summary>
        /// Update user data into the database where User ID = uuid
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
        /// <param name="webLoginKey">UUID for weblogin Key</param>
        /// <returns>Success?</returns>
        public bool updateUserRow(UUID uuid, string username, string lastname, string email, string passwordHash,
                                  string passwordSalt, UInt64 homeRegion, UUID homeRegionID, float homeLocX, float homeLocY, float homeLocZ,
                                  float homeLookAtX, float homeLookAtY, float homeLookAtZ, int created, int lastlogin,
                                  string inventoryURI, string assetURI, uint canDoMask, uint wantDoMask,
                                  string aboutText, string firstText,
                                  UUID profileImage, UUID firstImage, UUID webLoginKey, int userFlags, int godLevel, string customType, UUID partner)
        {
            string sql = "UPDATE users SET `username` = ?username , `lastname` = ?lastname, `email` = ?email ";
            sql += ", `passwordHash` = ?passwordHash , `passwordSalt` = ?passwordSalt , ";
            sql += "`homeRegion` = ?homeRegion , `homeRegionID` = ?homeRegionID, `homeLocationX` = ?homeLocationX , ";
            sql += "`homeLocationY`  = ?homeLocationY , `homeLocationZ` = ?homeLocationZ , ";
            sql += "`homeLookAtX` = ?homeLookAtX , `homeLookAtY` = ?homeLookAtY , ";
            sql += "`homeLookAtZ` = ?homeLookAtZ , `created` = ?created , `lastLogin` = ?lastLogin , ";
            sql += "`userInventoryURI` = ?userInventoryURI , `userAssetURI` = ?userAssetURI , ";
            sql += "`profileCanDoMask` = ?profileCanDoMask , `profileWantDoMask` = ?profileWantDoMask , ";
            sql += "`profileAboutText` = ?profileAboutText , `profileFirstText` = ?profileFirstText, ";
            sql += "`profileImage` = ?profileImage , `profileFirstImage` = ?profileFirstImage , ";
            sql += "`userFlags` = ?userFlags , `godLevel` = ?godLevel , ";
            sql += "`customType` = ?customType , `partner` = ?partner , ";
            sql += "`webLoginKey` = ?webLoginKey WHERE UUID = ?UUID";

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["?UUID"] = uuid.ToString();
            parameters["?username"] = username;
            parameters["?lastname"] = lastname;
            parameters["?email"] = email;
            parameters["?passwordHash"] = passwordHash;
            parameters["?passwordSalt"] = passwordSalt;
            parameters["?homeRegion"] = homeRegion;
            parameters["?homeRegionID"] = homeRegionID.ToString();
            parameters["?homeLocationX"] = homeLocX;
            parameters["?homeLocationY"] = homeLocY;
            parameters["?homeLocationZ"] = homeLocZ;
            parameters["?homeLookAtX"] = homeLookAtX;
            parameters["?homeLookAtY"] = homeLookAtY;
            parameters["?homeLookAtZ"] = homeLookAtZ;
            parameters["?created"] = created;
            parameters["?lastLogin"] = lastlogin;
            parameters["?userInventoryURI"] = inventoryURI;
            parameters["?userAssetURI"] = assetURI;
            parameters["?profileCanDoMask"] = canDoMask;
            parameters["?profileWantDoMask"] = wantDoMask;
            parameters["?profileAboutText"] = aboutText;
            parameters["?profileFirstText"] = firstText;
            parameters["?profileImage"] = profileImage.ToString();
            parameters["?profileFirstImage"] = firstImage.ToString();
            parameters["?webLoginKey"] = webLoginKey.ToString();
            parameters["?userFlags"] = userFlags;
            parameters["?godLevel"] = godLevel;
            parameters["?customType"] = customType == null ? "" : customType;
            parameters["?partner"] = partner.ToString();

            bool returnval = false;
            try
            {
                IDbCommand result = Query(sql, parameters);

                if (result.ExecuteNonQuery() == 1)
                    returnval = true;

                result.Dispose();
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }

            //m_log.Debug("[MySQLManager]: update user retval == " + returnval.ToString());
            return returnval;
        }

        /// <summary>
        /// Inserts a new region into the database
        /// </summary>
        /// <param name="regiondata">The region to insert</param>
        /// <returns>Success?</returns>
        public bool insertRegion(RegionProfileData regiondata)
        {
            bool GRID_ONLY_UPDATE_NECESSARY_DATA = false;

            string sql = String.Empty;
            if (GRID_ONLY_UPDATE_NECESSARY_DATA)
            {
                sql += "INSERT INTO ";
            }
            else
            {
                sql += "REPLACE INTO ";
            }

            sql += "regions (regionHandle, regionName, uuid, regionRecvKey, regionSecret, regionSendKey, regionDataURI, ";
            sql +=
                "serverIP, serverPort, serverURI, locX, locY, locZ, eastOverrideHandle, westOverrideHandle, southOverrideHandle, northOverrideHandle, regionAssetURI, regionAssetRecvKey, ";

            // part of an initial brutish effort to provide accurate information (as per the xml region spec)
            // wrt the ownership of a given region
            // the (very bad) assumption is that this value is being read and handled inconsistently or
            // not at all. Current strategy is to put the code in place to support the validity of this information
            // and to roll forward debugging any issues from that point
            //
            // this particular section of the mod attempts to implement the commit of a supplied value
            // server for the UUID of the region's owner (master avatar). It consists of the addition of the column and value to the relevant sql,
            // as well as the related parameterization
            sql +=
                "regionAssetSendKey, regionUserURI, regionUserRecvKey, regionUserSendKey, regionMapTexture, serverHttpPort, serverRemotingPort, owner_uuid, originUUID, access) VALUES ";

            sql += "(?regionHandle, ?regionName, ?uuid, ?regionRecvKey, ?regionSecret, ?regionSendKey, ?regionDataURI, ";
            sql +=
                "?serverIP, ?serverPort, ?serverURI, ?locX, ?locY, ?locZ, ?eastOverrideHandle, ?westOverrideHandle, ?southOverrideHandle, ?northOverrideHandle, ?regionAssetURI, ?regionAssetRecvKey, ";
            sql +=
                "?regionAssetSendKey, ?regionUserURI, ?regionUserRecvKey, ?regionUserSendKey, ?regionMapTexture, ?serverHttpPort, ?serverRemotingPort, ?owner_uuid, ?originUUID, ?access)";

            if (GRID_ONLY_UPDATE_NECESSARY_DATA)
            {
                sql += "ON DUPLICATE KEY UPDATE serverIP = ?serverIP, serverPort = ?serverPort, serverURI = ?serverURI, owner_uuid - ?owner_uuid;";
            }
            else
            {
                sql += ";";
            }

            Dictionary<string, object> parameters = new Dictionary<string, object>();

            parameters["?regionHandle"] = regiondata.regionHandle.ToString();
            parameters["?regionName"] = regiondata.regionName.ToString();
            parameters["?uuid"] = regiondata.UUID.ToString();
            parameters["?regionRecvKey"] = regiondata.regionRecvKey.ToString();
            parameters["?regionSecret"] = regiondata.regionSecret.ToString();
            parameters["?regionSendKey"] = regiondata.regionSendKey.ToString();
            parameters["?regionDataURI"] = regiondata.regionDataURI.ToString();
            parameters["?serverIP"] = regiondata.serverIP.ToString();
            parameters["?serverPort"] = regiondata.serverPort.ToString();
            parameters["?serverURI"] = regiondata.serverURI.ToString();
            parameters["?locX"] = regiondata.regionLocX.ToString();
            parameters["?locY"] = regiondata.regionLocY.ToString();
            parameters["?locZ"] = regiondata.regionLocZ.ToString();
            parameters["?eastOverrideHandle"] = regiondata.regionEastOverrideHandle.ToString();
            parameters["?westOverrideHandle"] = regiondata.regionWestOverrideHandle.ToString();
            parameters["?northOverrideHandle"] = regiondata.regionNorthOverrideHandle.ToString();
            parameters["?southOverrideHandle"] = regiondata.regionSouthOverrideHandle.ToString();
            parameters["?regionAssetURI"] = regiondata.regionAssetURI.ToString();
            parameters["?regionAssetRecvKey"] = regiondata.regionAssetRecvKey.ToString();
            parameters["?regionAssetSendKey"] = regiondata.regionAssetSendKey.ToString();
            parameters["?regionUserURI"] = regiondata.regionUserURI.ToString();
            parameters["?regionUserRecvKey"] = regiondata.regionUserRecvKey.ToString();
            parameters["?regionUserSendKey"] = regiondata.regionUserSendKey.ToString();
            parameters["?regionMapTexture"] = regiondata.regionMapTextureID.ToString();
            parameters["?serverHttpPort"] = regiondata.httpPort.ToString();
            parameters["?serverRemotingPort"] = regiondata.remotingPort.ToString();
            parameters["?owner_uuid"] = regiondata.owner_uuid.ToString();
            parameters["?originUUID"] = regiondata.originUUID.ToString();
            parameters["?access"] = regiondata.maturity.ToString();

            bool returnval = false;

            try
            {
                IDbCommand result = Query(sql, parameters);

                // int x;
                // if ((x = result.ExecuteNonQuery()) > 0)
                // {
                //     returnval = true;
                // }
                if (result.ExecuteNonQuery() > 0)
                {
                    returnval = true;
                }
                result.Dispose();
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }

            return returnval;
        }

        /// <summary>
        /// Delete a region from the database
        /// </summary>
        /// <param name="uuid">The region to delete</param>
        /// <returns>Success?</returns>
        //public bool deleteRegion(RegionProfileData regiondata)
        public bool deleteRegion(string uuid)
        {
            bool returnval = false;

            string sql = "DELETE FROM regions WHERE uuid = ?uuid;";

            Dictionary<string, object> parameters = new Dictionary<string, object>();

            try
            {
                parameters["?uuid"] = uuid;

                IDbCommand result = Query(sql, parameters);

                // int x;
                // if ((x = result.ExecuteNonQuery()) > 0)
                // {
                //     returnval = true;
                // }
                if (result.ExecuteNonQuery() > 0)
                {
                    returnval = true;
                }
                result.Dispose();
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }

            return returnval;
        }

        /// <summary>
        /// Creates a new agent and inserts it into the database
        /// </summary>
        /// <param name="agentdata">The agent data to be inserted</param>
        /// <returns>Success?</returns>
        public bool insertAgentRow(UserAgentData agentdata)
        {
            string sql = String.Empty;
            sql += "REPLACE INTO ";
            sql += "agents (UUID, sessionID, secureSessionID, agentIP, agentPort, agentOnline, loginTime, logoutTime, currentRegion, currentHandle, currentPos, currentLookAt) VALUES ";
            sql += "(?UUID, ?sessionID, ?secureSessionID, ?agentIP, ?agentPort, ?agentOnline, ?loginTime, ?logoutTime, ?currentRegion, ?currentHandle, ?currentPos, ?currentLookAt);";
            Dictionary<string, object> parameters = new Dictionary<string, object>();

            parameters["?UUID"] = agentdata.ProfileID.ToString();
            parameters["?sessionID"] = agentdata.SessionID.ToString();
            parameters["?secureSessionID"] = agentdata.SecureSessionID.ToString();
            parameters["?agentIP"] = agentdata.AgentIP.ToString();
            parameters["?agentPort"] = agentdata.AgentPort.ToString();
            parameters["?agentOnline"] = (agentdata.AgentOnline == true) ? "1" : "0";
            parameters["?loginTime"] = agentdata.LoginTime.ToString();
            parameters["?logoutTime"] = agentdata.LogoutTime.ToString();
            parameters["?currentRegion"] = agentdata.Region.ToString();
            parameters["?currentHandle"] = agentdata.Handle.ToString();
            parameters["?currentPos"] = "<" + (agentdata.Position.X).ToString().Replace(",", ".") + "," + (agentdata.Position.Y).ToString().Replace(",", ".") + "," + (agentdata.Position.Z).ToString().Replace(",", ".") + ">";
            parameters["?currentLookAt"] = "<" + (agentdata.LookAt.X).ToString().Replace(",", ".") + "," + (agentdata.LookAt.Y).ToString().Replace(",", ".") + "," + (agentdata.LookAt.Z).ToString().Replace(",", ".") + ">";

            bool returnval = false;

            try
            {
                IDbCommand result = Query(sql, parameters);

                // int x;
                // if ((x = result.ExecuteNonQuery()) > 0)
                // {
                //     returnval = true;
                // }
                if (result.ExecuteNonQuery() > 0)
                {
                    returnval = true;
                }
                result.Dispose();
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }

            return returnval;
        }

        /// <summary>
        /// Create (or replace if existing) an avatar appearence
        /// </summary>
        /// <param name="appearance"></param>
        /// <returns>Succes?</returns>
        public bool insertAppearanceRow(AvatarAppearance appearance)
        {
            string sql = String.Empty;
            sql += "REPLACE INTO ";
            sql += "avatarappearance (owner, serial, visual_params, texture, avatar_height, ";
            sql += "body_item, body_asset, skin_item, skin_asset, hair_item, hair_asset, eyes_item, eyes_asset, ";
            sql += "shirt_item, shirt_asset, pants_item, pants_asset, shoes_item, shoes_asset, socks_item, socks_asset, ";
            sql += "jacket_item, jacket_asset, gloves_item, gloves_asset, undershirt_item, undershirt_asset, underpants_item, underpants_asset, ";
            sql += "skirt_item, skirt_asset) values (";
            sql += "?owner, ?serial, ?visual_params, ?texture, ?avatar_height, ";
            sql += "?body_item, ?body_asset, ?skin_item, ?skin_asset, ?hair_item, ?hair_asset, ?eyes_item, ?eyes_asset, ";
            sql += "?shirt_item, ?shirt_asset, ?pants_item, ?pants_asset, ?shoes_item, ?shoes_asset, ?socks_item, ?socks_asset, ";
            sql += "?jacket_item, ?jacket_asset, ?gloves_item, ?gloves_asset, ?undershirt_item, ?undershirt_asset, ?underpants_item, ?underpants_asset, ";
            sql += "?skirt_item, ?skirt_asset)";

            bool returnval = false;

            // we want to send in byte data, which means we can't just pass down strings
            try {
                MySqlCommand cmd = (MySqlCommand) dbcon.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("?owner", appearance.Owner.ToString());
                cmd.Parameters.AddWithValue("?serial", appearance.Serial);
                cmd.Parameters.AddWithValue("?visual_params", appearance.VisualParams);
                cmd.Parameters.AddWithValue("?texture", appearance.Texture.GetBytes());
                cmd.Parameters.AddWithValue("?avatar_height", appearance.AvatarHeight);
                cmd.Parameters.AddWithValue("?body_item", appearance.BodyItem.ToString());
                cmd.Parameters.AddWithValue("?body_asset", appearance.BodyAsset.ToString());
                cmd.Parameters.AddWithValue("?skin_item", appearance.SkinItem.ToString());
                cmd.Parameters.AddWithValue("?skin_asset", appearance.SkinAsset.ToString());
                cmd.Parameters.AddWithValue("?hair_item", appearance.HairItem.ToString());
                cmd.Parameters.AddWithValue("?hair_asset", appearance.HairAsset.ToString());
                cmd.Parameters.AddWithValue("?eyes_item", appearance.EyesItem.ToString());
                cmd.Parameters.AddWithValue("?eyes_asset", appearance.EyesAsset.ToString());
                cmd.Parameters.AddWithValue("?shirt_item", appearance.ShirtItem.ToString());
                cmd.Parameters.AddWithValue("?shirt_asset", appearance.ShirtAsset.ToString());
                cmd.Parameters.AddWithValue("?pants_item", appearance.PantsItem.ToString());
                cmd.Parameters.AddWithValue("?pants_asset", appearance.PantsAsset.ToString());
                cmd.Parameters.AddWithValue("?shoes_item", appearance.ShoesItem.ToString());
                cmd.Parameters.AddWithValue("?shoes_asset", appearance.ShoesAsset.ToString());
                cmd.Parameters.AddWithValue("?socks_item", appearance.SocksItem.ToString());
                cmd.Parameters.AddWithValue("?socks_asset", appearance.SocksAsset.ToString());
                cmd.Parameters.AddWithValue("?jacket_item", appearance.JacketItem.ToString());
                cmd.Parameters.AddWithValue("?jacket_asset", appearance.JacketAsset.ToString());
                cmd.Parameters.AddWithValue("?gloves_item", appearance.GlovesItem.ToString());
                cmd.Parameters.AddWithValue("?gloves_asset", appearance.GlovesAsset.ToString());
                cmd.Parameters.AddWithValue("?undershirt_item", appearance.UnderShirtItem.ToString());
                cmd.Parameters.AddWithValue("?undershirt_asset", appearance.UnderShirtAsset.ToString());
                cmd.Parameters.AddWithValue("?underpants_item", appearance.UnderPantsItem.ToString());
                cmd.Parameters.AddWithValue("?underpants_asset", appearance.UnderPantsAsset.ToString());
                cmd.Parameters.AddWithValue("?skirt_item", appearance.SkirtItem.ToString());
                cmd.Parameters.AddWithValue("?skirt_asset", appearance.SkirtAsset.ToString());

                if (cmd.ExecuteNonQuery() > 0)
                    returnval = true;

                cmd.Dispose();
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }

            return returnval;

        }

        public void writeAttachments(UUID agentID, Hashtable data)
        {
            string sql = "delete from avatarattachments where UUID = ?uuid";

            MySqlCommand cmd = (MySqlCommand) dbcon.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("?uuid", agentID.ToString());

            cmd.ExecuteNonQuery();

            if (data == null)
                return;

            sql = "insert into avatarattachments (UUID, attachpoint, item, asset) values (?uuid, ?attachpoint, ?item, ?asset)";

            cmd = (MySqlCommand) dbcon.CreateCommand();
            cmd.CommandText = sql;

            foreach (DictionaryEntry e in data)
            {
                int attachpoint = Convert.ToInt32(e.Key);

                Hashtable item = (Hashtable)e.Value;

                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("?uuid", agentID.ToString());
                cmd.Parameters.AddWithValue("?attachpoint", attachpoint);
                cmd.Parameters.AddWithValue("?item",  item["item"]);
                cmd.Parameters.AddWithValue("?asset", item["asset"]);

                cmd.ExecuteNonQuery();
            }
        }
    }
}
