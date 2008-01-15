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
using System.IO;
using System.Reflection;
using libsecondlife;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Data.MSSQL
{
    /// <summary>
    /// A management class for the MS SQL Storage Engine
    /// </summary>
    internal class MSSQLManager
    {
        /// <summary>
        /// The database connection object
        /// </summary>
        private IDbConnection dbcon;

        /// <summary>
        /// Connection string for ADO.net
        /// </summary>
        private string connectionString;

        /// <summary>
        /// Initialises and creates a new Sql connection and maintains it.
        /// </summary>
        /// <param name="hostname">The Sql server being connected to</param>
        /// <param name="database">The name of the Sql database being used</param>
        /// <param name="username">The username logging into the database</param>
        /// <param name="password">The password for the user logging in</param>
        /// <param name="cpooling">Whether to use connection pooling or not, can be one of the following: 'yes', 'true', 'no' or 'false', if unsure use 'false'.</param>
        public MSSQLManager(string dataSource, string initialCatalog, string persistSecurityInfo, string userId,
                            string password)
        {
            try
            {
                connectionString = "Data Source=" + dataSource + ";Initial Catalog=" + initialCatalog +
                                   ";Persist Security Info=" + persistSecurityInfo + ";User ID=" + userId + ";Password=" +
                                   password + ";";
                dbcon = new SqlConnection(connectionString);
                TestTables(dbcon);
                dbcon.Open();
            }
            catch (Exception e)
            {
                throw new Exception("Error initialising Sql Database: " + e.ToString());
            }
        }

        private bool TestTables(IDbConnection conn)
        {
            IDbCommand cmd = Query("SELECT * FROM regions", new Dictionary<string, string>());
            //SqlCommand cmd = (SqlCommand)dbcon.CreateCommand();
            //cmd.CommandText = "SELECT * FROM regions";    
            try
            {
                conn.Open();
                cmd.ExecuteNonQuery();
                cmd.Dispose();
                conn.Close();
            }
            catch (Exception)
            {
                MainLog.Instance.Verbose("DATASTORE", "MSSQL Database doesn't exist... creating");
                InitDB(conn);
            }
            cmd = Query("select top 1 webLoginKey from users", new Dictionary<string, string>());
            try
            {
                conn.Open();
                cmd.ExecuteNonQuery();
                cmd.Dispose();
                conn.Close();
            }
            catch (Exception)
            {
                conn.Open();
                cmd = Query("alter table users add column [webLoginKey] varchar(36) default NULL", new Dictionary<string, string>());
                cmd.ExecuteNonQuery();
                cmd.Dispose();
                conn.Close();
            }
            return true;
        }

        private void InitDB(IDbConnection conn)
        {
            string createRegions = defineTable(createRegionsTable());
            Dictionary<string, string> param = new Dictionary<string, string>();
            IDbCommand pcmd = Query(createRegions, param);
            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }
            pcmd.ExecuteNonQuery();
            pcmd.Dispose();

            ExecuteResourceSql("Mssql-users.sql");
            ExecuteResourceSql("Mssql-agents.sql");
            ExecuteResourceSql("Mssql-logs.sql");

            conn.Close();
        }

        private DataTable createRegionsTable()
        {
            DataTable regions = new DataTable("regions");

            createCol(regions, "regionHandle", typeof (ulong));
            createCol(regions, "regionName", typeof (String));
            createCol(regions, "uuid", typeof (String));

            createCol(regions, "regionRecvKey", typeof (String));
            createCol(regions, "regionSecret", typeof (String));
            createCol(regions, "regionSendKey", typeof (String));

            createCol(regions, "regionDataURI", typeof (String));
            createCol(regions, "serverIP", typeof (String));
            createCol(regions, "serverPort", typeof (String));
            createCol(regions, "serverURI", typeof (String));


            createCol(regions, "locX", typeof (uint));
            createCol(regions, "locY", typeof (uint));
            createCol(regions, "locZ", typeof (uint));

            createCol(regions, "eastOverrideHandle", typeof (ulong));
            createCol(regions, "westOverrideHandle", typeof (ulong));
            createCol(regions, "southOverrideHandle", typeof (ulong));
            createCol(regions, "northOverrideHandle", typeof (ulong));

            createCol(regions, "regionAssetURI", typeof (String));
            createCol(regions, "regionAssetRecvKey", typeof (String));
            createCol(regions, "regionAssetSendKey", typeof (String));

            createCol(regions, "regionUserURI", typeof (String));
            createCol(regions, "regionUserRecvKey", typeof (String));
            createCol(regions, "regionUserSendKey", typeof (String));

            createCol(regions, "regionMapTexture", typeof (String));
            createCol(regions, "serverHttpPort", typeof (String));
            createCol(regions, "serverRemotingPort", typeof (uint));

            // Add in contraints
            regions.PrimaryKey = new DataColumn[] {regions.Columns["UUID"]};
            return regions;
        }

        protected static void createCol(DataTable dt, string name, Type type)
        {
            DataColumn col = new DataColumn(name, type);
            dt.Columns.Add(col);
        }

        protected static string defineTable(DataTable dt)
        {
            string sql = "create table " + dt.TableName + "(";
            string subsql = String.Empty;
            foreach (DataColumn col in dt.Columns)
            {
                if (subsql.Length > 0)
                {
                    // a map function would rock so much here
                    subsql += ",\n";
                }

                subsql += col.ColumnName + " " + SqlType(col.DataType);
                if (col == dt.PrimaryKey[0])
                {
                    subsql += " primary key";
                }
            }
            sql += subsql;
            sql += ")";
            return sql;
        }


        // this is something we'll need to implement for each db
        // slightly differently.
        private static string SqlType(Type type)
        {
            if (type == typeof (String))
            {
                return "varchar(255)";
            }
            else if (type == typeof (Int32))
            {
                return "integer";
            }
            else if (type == typeof (Double))
            {
                return "float";
            }
            else if (type == typeof (Byte[]))
            {
                return "image";
            }
            else
            {
                return "varchar(255)";
            }
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
            lock (dbcon)
            {
                try
                {
                    //string connectionString = "Data Source=WRK-OU-738\\SQLEXPRESS;Initial Catalog=rex;Persist Security Info=True;User ID=sa;Password=rex";
                    // Close the DB connection
                    dbcon.Close();
                    // Try reopen it
                    dbcon = new SqlConnection(connectionString);
                    dbcon.Open();
                }
                catch (Exception e)
                {
                    MainLog.Instance.Error("Unable to reconnect to database " + e.ToString());
                }
            }
        }

        /// <summary>
        /// Runs a query with protection against SQL Injection by using parameterised input.
        /// </summary>
        /// <param name="sql">The SQL string - replace any variables such as WHERE x = "y" with WHERE x = @y</param>
        /// <param name="parameters">The parameters - index so that @y is indexed as 'y'</param>
        /// <returns>A Sql DB Command</returns>
        public IDbCommand Query(string sql, Dictionary<string, string> parameters)
        {
            SqlCommand dbcommand = (SqlCommand) dbcon.CreateCommand();
            dbcommand.CommandText = sql;
            foreach (KeyValuePair<string, string> param in parameters)
            {
                dbcommand.Parameters.AddWithValue(param.Key, param.Value);
            }

            return (IDbCommand) dbcommand;
        }

        /// <summary>
        /// Runs a database reader object and returns a region row
        /// </summary>
        /// <param name="reader">An active database reader</param>
        /// <returns>A region row</returns>
        public RegionProfileData getRegionRow(IDataReader reader)
        {
            RegionProfileData regionprofile = new RegionProfileData();

            if (reader.Read())
            {
                // Region Main
                regionprofile.regionHandle = Convert.ToUInt64(reader["regionHandle"]);
                regionprofile.regionName = (string) reader["regionName"];
                regionprofile.UUID = new LLUUID((string) reader["uuid"]);

                // Secrets
                regionprofile.regionRecvKey = (string) reader["regionRecvKey"];
                regionprofile.regionSecret = (string) reader["regionSecret"];
                regionprofile.regionSendKey = (string) reader["regionSendKey"];

                // Region Server
                regionprofile.regionDataURI = (string) reader["regionDataURI"];
                regionprofile.regionOnline = false; // Needs to be pinged before this can be set.
                regionprofile.serverIP = (string) reader["serverIP"];
                regionprofile.serverPort = Convert.ToUInt32(reader["serverPort"]);
                regionprofile.serverURI = (string) reader["serverURI"];
                regionprofile.httpPort = Convert.ToUInt32(reader["serverHttpPort"]);
                regionprofile.remotingPort = Convert.ToUInt32(reader["serverRemotingPort"]);


                // Location
                regionprofile.regionLocX = Convert.ToUInt32(reader["locX"]);
                regionprofile.regionLocY = Convert.ToUInt32(reader["locY"]);
                regionprofile.regionLocZ = Convert.ToUInt32(reader["locZ"]);

                // Neighbours - 0 = No Override
                regionprofile.regionEastOverrideHandle = Convert.ToUInt64(reader["eastOverrideHandle"]);
                regionprofile.regionWestOverrideHandle = Convert.ToUInt64(reader["westOverrideHandle"]);
                regionprofile.regionSouthOverrideHandle = Convert.ToUInt64(reader["southOverrideHandle"]);
                regionprofile.regionNorthOverrideHandle = Convert.ToUInt64(reader["northOverrideHandle"]);

                // Assets
                regionprofile.regionAssetURI = (string) reader["regionAssetURI"];
                regionprofile.regionAssetRecvKey = (string) reader["regionAssetRecvKey"];
                regionprofile.regionAssetSendKey = (string) reader["regionAssetSendKey"];

                // Userserver
                regionprofile.regionUserURI = (string) reader["regionUserURI"];
                regionprofile.regionUserRecvKey = (string) reader["regionUserRecvKey"];
                regionprofile.regionUserSendKey = (string) reader["regionUserSendKey"];

                // World Map Addition
                string tempRegionMap = reader["regionMapTexture"].ToString();
                if (tempRegionMap != String.Empty)
                {
                    regionprofile.regionMapTextureID = new LLUUID(tempRegionMap);
                }
                else
                {
                    regionprofile.regionMapTextureID = new LLUUID();
                }
            }
            else
            {
                reader.Close();
                throw new Exception("No rows to return");
            }
            return regionprofile;
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
                retval.UUID = new LLUUID((string) reader["UUID"]);
                retval.username = (string) reader["username"];
                retval.surname = (string) reader["lastname"];

                retval.passwordHash = (string) reader["passwordHash"];
                retval.passwordSalt = (string) reader["passwordSalt"];

                retval.homeRegion = Convert.ToUInt64(reader["homeRegion"].ToString());
                retval.homeLocation = new LLVector3(
                    Convert.ToSingle(reader["homeLocationX"].ToString()),
                    Convert.ToSingle(reader["homeLocationY"].ToString()),
                    Convert.ToSingle(reader["homeLocationZ"].ToString()));
                retval.homeLookAt = new LLVector3(
                    Convert.ToSingle(reader["homeLookAtX"].ToString()),
                    Convert.ToSingle(reader["homeLookAtY"].ToString()),
                    Convert.ToSingle(reader["homeLookAtZ"].ToString()));

                retval.created = Convert.ToInt32(reader["created"].ToString());
                retval.lastLogin = Convert.ToInt32(reader["lastLogin"].ToString());

                retval.userInventoryURI = (string) reader["userInventoryURI"];
                retval.userAssetURI = (string) reader["userAssetURI"];

                retval.profileCanDoMask = Convert.ToUInt32(reader["profileCanDoMask"].ToString());
                retval.profileWantDoMask = Convert.ToUInt32(reader["profileWantDoMask"].ToString());

                retval.profileAboutText = (string) reader["profileAboutText"];
                retval.profileFirstText = (string) reader["profileFirstText"];

                retval.profileImage = new LLUUID((string) reader["profileImage"]);
                retval.profileFirstImage = new LLUUID((string) reader["profileFirstImage"]);
                retval.webLoginKey = new LLUUID((string)reader["webLoginKey"]);
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
                retval.UUID = new LLUUID((string) reader["UUID"]);
                retval.sessionID = new LLUUID((string) reader["sessionID"]);
                retval.secureSessionID = new LLUUID((string) reader["secureSessionID"]);

                // Agent Who?
                retval.agentIP = (string) reader["agentIP"];
                retval.agentPort = Convert.ToUInt32(reader["agentPort"].ToString());
                retval.agentOnline = Convert.ToBoolean(reader["agentOnline"].ToString());

                // Login/Logout times (UNIX Epoch)
                retval.loginTime = Convert.ToInt32(reader["loginTime"].ToString());
                retval.logoutTime = Convert.ToInt32(reader["logoutTime"].ToString());

                // Current position
                retval.currentRegion = (string) reader["currentRegion"];
                retval.currentHandle = Convert.ToUInt64(reader["currentHandle"].ToString());
                LLVector3.TryParse((string) reader["currentPos"], out retval.currentPos);
            }
            else
            {
                return null;
            }
            return retval;
        }

        public AssetBase getAssetRow(IDataReader reader)
        {
            AssetBase asset = new AssetBase();
            if (reader.Read())
            {
                // Region Main

                asset = new AssetBase();
                asset.Data = (byte[]) reader["data"];
                asset.Description = (string) reader["description"];
                asset.FullID = new LLUUID((string) reader["id"]);
                asset.InvType = Convert.ToSByte(reader["invType"]);
                asset.Local = Convert.ToBoolean(reader["local"]); // ((sbyte)reader["local"]) != 0 ? true : false;
                asset.Name = (string) reader["name"];
                asset.Type = Convert.ToSByte(reader["assetType"]);
            }
            else
            {
                return null; // throw new Exception("No rows to return");
            }
            return asset;
        }

        /// <summary>
        /// Creates a new region in the database
        /// </summary>
        /// <param name="profile">The region profile to insert</param>
        /// <returns>Successful?</returns>
        public bool insertRegionRow(RegionProfileData profile)
        {
            //Insert new region
            string sql =
                "INSERT INTO regions ([regionHandle], [regionName], [uuid], [regionRecvKey], [regionSecret], [regionSendKey], [regionDataURI], ";
            sql +=
                "[serverIP], [serverPort], [serverURI], [locX], [locY], [locZ], [eastOverrideHandle], [westOverrideHandle], [southOverrideHandle], [northOverrideHandle], [regionAssetURI], [regionAssetRecvKey], ";
            sql +=
                "[regionAssetSendKey], [regionUserURI], [regionUserRecvKey], [regionUserSendKey], [regionMapTexture], [serverHttpPort], [serverRemotingPort]) VALUES ";

            sql += "(@regionHandle, @regionName, @uuid, @regionRecvKey, @regionSecret, @regionSendKey, @regionDataURI, ";
            sql +=
                "@serverIP, @serverPort, @serverURI, @locX, @locY, @locZ, @eastOverrideHandle, @westOverrideHandle, @southOverrideHandle, @northOverrideHandle, @regionAssetURI, @regionAssetRecvKey, ";
            sql +=
                "@regionAssetSendKey, @regionUserURI, @regionUserRecvKey, @regionUserSendKey, @regionMapTexture, @serverHttpPort, @serverRemotingPort);";

            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters["regionHandle"] = profile.regionHandle.ToString();
            parameters["regionName"] = profile.regionName;
            parameters["uuid"] = profile.UUID.ToString();
            parameters["regionRecvKey"] = profile.regionRecvKey;
            parameters["regionSecret"] = profile.regionSecret;
            parameters["regionSendKey"] = profile.regionSendKey;
            parameters["regionDataURI"] = profile.regionDataURI;
            parameters["serverIP"] = profile.serverIP;
            parameters["serverPort"] = profile.serverPort.ToString();
            parameters["serverURI"] = profile.serverURI;
            parameters["locX"] = profile.regionLocX.ToString();
            parameters["locY"] = profile.regionLocY.ToString();
            parameters["locZ"] = profile.regionLocZ.ToString();
            parameters["eastOverrideHandle"] = profile.regionEastOverrideHandle.ToString();
            parameters["westOverrideHandle"] = profile.regionWestOverrideHandle.ToString();
            parameters["northOverrideHandle"] = profile.regionNorthOverrideHandle.ToString();
            parameters["southOverrideHandle"] = profile.regionSouthOverrideHandle.ToString();
            parameters["regionAssetURI"] = profile.regionAssetURI;
            parameters["regionAssetRecvKey"] = profile.regionAssetRecvKey;
            parameters["regionAssetSendKey"] = profile.regionAssetSendKey;
            parameters["regionUserURI"] = profile.regionUserURI;
            parameters["regionUserRecvKey"] = profile.regionUserRecvKey;
            parameters["regionUserSendKey"] = profile.regionUserSendKey;
            parameters["regionMapTexture"] = profile.regionMapTextureID.ToString();
            parameters["serverHttpPort"] = profile.httpPort.ToString();
            parameters["serverRemotingPort"] = profile.remotingPort.ToString();


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
                MainLog.Instance.Error("MSSQLManager : " + e.ToString());
            }

            return returnval;
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
            string sql = "INSERT INTO logs ([target], [server], [method], [arguments], [priority], [message]) VALUES ";
            sql += "(@target, @server, @method, @arguments, @priority, @message);";

            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters["server"] = serverDaemon;
            parameters["target"] = target;
            parameters["method"] = methodCall;
            parameters["arguments"] = arguments;
            parameters["priority"] = priority.ToString();
            parameters["message"] = logMessage;

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
                MainLog.Instance.Error(e.ToString());
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
        /// <returns>Success?</returns>
        public bool insertUserRow(LLUUID uuid, string username, string lastname, string passwordHash,
                                  string passwordSalt, UInt64 homeRegion, float homeLocX, float homeLocY, float homeLocZ,
                                  float homeLookAtX, float homeLookAtY, float homeLookAtZ, int created, int lastlogin,
                                  string inventoryURI, string assetURI, uint canDoMask, uint wantDoMask,
                                  string aboutText, string firstText,
                                  LLUUID profileImage, LLUUID firstImage, LLUUID webLoginKey)
        {
            string sql = "INSERT INTO users ";
            sql += "([UUID], [username], [lastname], [passwordHash], [passwordSalt], [homeRegion], ";
            sql +=
                "[homeLocationX], [homeLocationY], [homeLocationZ], [homeLookAtX], [homeLookAtY], [homeLookAtZ], [created], ";
            sql +=
                "[lastLogin], [userInventoryURI], [userAssetURI], [profileCanDoMask], [profileWantDoMask], [profileAboutText], ";
            sql += "[profileFirstText], [profileImage], [profileFirstImage], [webLoginKey]) VALUES ";

            sql += "(@UUID, @username, @lastname, @passwordHash, @passwordSalt, @homeRegion, ";
            sql +=
                "@homeLocationX, @homeLocationY, @homeLocationZ, @homeLookAtX, @homeLookAtY, @homeLookAtZ, @created, ";
            sql +=
                "@lastLogin, @userInventoryURI, @userAssetURI, @profileCanDoMask, @profileWantDoMask, @profileAboutText, ";
            sql += "@profileFirstText, @profileImage, @profileFirstImage, @webLoginKey);";

            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters["UUID"] = uuid.ToString();
            parameters["username"] = username.ToString();
            parameters["lastname"] = lastname.ToString();
            parameters["passwordHash"] = passwordHash.ToString();
            parameters["passwordSalt"] = passwordSalt.ToString();
            parameters["homeRegion"] = homeRegion.ToString();
            parameters["homeLocationX"] = homeLocX.ToString();
            parameters["homeLocationY"] = homeLocY.ToString();
            parameters["homeLocationZ"] = homeLocZ.ToString();
            parameters["homeLookAtX"] = homeLookAtX.ToString();
            parameters["homeLookAtY"] = homeLookAtY.ToString();
            parameters["homeLookAtZ"] = homeLookAtZ.ToString();
            parameters["created"] = created.ToString();
            parameters["lastLogin"] = lastlogin.ToString();
            parameters["userInventoryURI"] = String.Empty;
            parameters["userAssetURI"] = String.Empty;
            parameters["profileCanDoMask"] = "0";
            parameters["profileWantDoMask"] = "0";
            parameters["profileAboutText"] = String.Empty;
            parameters["profileFirstText"] = String.Empty;
            parameters["profileImage"] = LLUUID.Zero.ToString();
            parameters["profileFirstImage"] = LLUUID.Zero.ToString();
            parameters["webLoginKey"] = LLUUID.Random().ToString();

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
                MainLog.Instance.Error(e.ToString());
                return false;
            }

            return returnval;
        }

        /// <summary>
        /// Execute a SQL statement stored in a resource, as a string
        /// </summary>
        /// <param name="name"></param>
        public void ExecuteResourceSql(string name)
        {
            try
            {
                SqlCommand cmd = new SqlCommand(getResourceString(name), (SqlConnection) dbcon);
                cmd.ExecuteNonQuery();
                cmd.Dispose();
            }
            catch (Exception e)
            {
                MainLog.Instance.Error("Unable to execute query " + e.ToString());
            }
        }

        public SqlConnection getConnection()
        {
            return (SqlConnection) dbcon;
        }

        /// <summary>
        /// Given a list of tables, return the version of the tables, as seen in the database
        /// </summary>
        /// <param name="tableList"></param>
        public void GetTableVersion(Dictionary<string, string> tableList)
        {
            lock (dbcon)
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["dbname"] = dbcon.Database;
                IDbCommand tablesCmd =
                    Query("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_CATALOG=@dbname", param);
                using (IDataReader tables = tablesCmd.ExecuteReader())
                {
                    while (tables.Read())
                    {
                        try
                        {
                            string tableName = (string) tables["TABLE_NAME"];
                            if (tableList.ContainsKey(tableName))
                                tableList[tableName] = tableName;
                        }
                        catch (Exception e)
                        {
                            MainLog.Instance.Error(e.ToString());
                        }
                    }
                    tables.Close();
                }
            }
        }

        private string getResourceString(string name)
        {
            Assembly assem = GetType().Assembly;
            string[] names = assem.GetManifestResourceNames();

            foreach (string s in names)
                if (s.EndsWith(name))
                    using (Stream resource = assem.GetManifestResourceStream(s))
                    {
                        using (StreamReader resourceReader = new StreamReader(resource))
                        {
                            string resourceString = resourceReader.ReadToEnd();
                            return resourceString;
                        }
                    }
            throw new Exception(string.Format("Resource '{0}' was not found", name));
        }

        /// <summary> 
        /// Returns the version of this DB provider
        /// </summary>
        /// <returns>A string containing the DB provider</returns>
        public string getVersion()
        {
            Module module = GetType().Module;
            string dllName = module.Assembly.ManifestModule.Name;
            Version dllVersion = module.Assembly.GetName().Version;


            return
                string.Format("{0}.{1}.{2}.{3}", dllVersion.Major, dllVersion.Minor, dllVersion.Build,
                              dllVersion.Revision);
        }
    }
}