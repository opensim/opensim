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
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using libsecondlife;
using log4net;
using OpenSim.Framework;

namespace OpenSim.Data.MSSQL
{
    /// <summary>
    /// A management class for the MS SQL Storage Engine
    /// </summary>
    public class MSSQLManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The database connection object
        /// </summary>
        private IDbConnection dbcon;

        /// <summary>
        /// Connection string for ADO.net
        /// </summary>
        private readonly string connectionString;

        public MSSQLManager(string dataSource, string initialCatalog, string persistSecurityInfo, string userId,
                            string password)
        {
            connectionString = "Data Source=" + dataSource + ";Initial Catalog=" + initialCatalog +
                                   ";Persist Security Info=" + persistSecurityInfo + ";User ID=" + userId + ";Password=" +
                                   password + ";";
            dbcon = new SqlConnection(connectionString);
            dbcon.Open();
        }

        //private DataTable createRegionsTable()
        //{
        //    DataTable regions = new DataTable("regions");

        //    createCol(regions, "regionHandle", typeof (ulong));
        //    createCol(regions, "regionName", typeof (String));
        //    createCol(regions, "uuid", typeof (String));

        //    createCol(regions, "regionRecvKey", typeof (String));
        //    createCol(regions, "regionSecret", typeof (String));
        //    createCol(regions, "regionSendKey", typeof (String));

        //    createCol(regions, "regionDataURI", typeof (String));
        //    createCol(regions, "serverIP", typeof (String));
        //    createCol(regions, "serverPort", typeof (String));
        //    createCol(regions, "serverURI", typeof (String));


        //    createCol(regions, "locX", typeof (uint));
        //    createCol(regions, "locY", typeof (uint));
        //    createCol(regions, "locZ", typeof (uint));

        //    createCol(regions, "eastOverrideHandle", typeof (ulong));
        //    createCol(regions, "westOverrideHandle", typeof (ulong));
        //    createCol(regions, "southOverrideHandle", typeof (ulong));
        //    createCol(regions, "northOverrideHandle", typeof (ulong));

        //    createCol(regions, "regionAssetURI", typeof (String));
        //    createCol(regions, "regionAssetRecvKey", typeof (String));
        //    createCol(regions, "regionAssetSendKey", typeof (String));

        //    createCol(regions, "regionUserURI", typeof (String));
        //    createCol(regions, "regionUserRecvKey", typeof (String));
        //    createCol(regions, "regionUserSendKey", typeof (String));

        //    createCol(regions, "regionMapTexture", typeof (String));
        //    createCol(regions, "serverHttpPort", typeof (String));
        //    createCol(regions, "serverRemotingPort", typeof (uint));

        //    // Add in contraints
        //    regions.PrimaryKey = new DataColumn[] {regions.Columns["UUID"]};
        //    return regions;
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        protected static void createCol(DataTable dt, string name, Type type)
        {
            DataColumn col = new DataColumn(name, type);
            dt.Columns.Add(col);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Type conversion function
        /// </summary>
        /// <param name="type">a type</param>
        /// <returns>a sqltype</returns>
        /// <remarks>this is something we'll need to implement for each db slightly differently.</remarks>
        public static string SqlType(Type type)
        {
            if (type == typeof(String))
            {
                return "varchar(255)";
            }
            else if (type == typeof(Int32))
            {
                return "integer";
            }
            else if (type == typeof(Double))
            {
                return "float";
            }
            else if (type == typeof(Byte[]))
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
                    // Close the DB connection
                    dbcon.Close();
                    // Try reopen it
                    dbcon = new SqlConnection(connectionString);
                    dbcon.Open();
                }
                catch (Exception e)
                {
                    m_log.Error("Unable to reconnect to database " + e.ToString());
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
            SqlCommand dbcommand = (SqlCommand)dbcon.CreateCommand();
            dbcommand.CommandText = sql;
            foreach (KeyValuePair<string, string> param in parameters)
            {
                dbcommand.Parameters.AddWithValue(param.Key, param.Value);
            }

            return (IDbCommand)dbcommand;
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
                regionprofile.regionName = (string)reader["regionName"];
                regionprofile.UUID = new LLUUID((string)reader["uuid"]);

                // Secrets
                regionprofile.regionRecvKey = (string)reader["regionRecvKey"];
                regionprofile.regionSecret = (string)reader["regionSecret"];
                regionprofile.regionSendKey = (string)reader["regionSendKey"];

                // Region Server
                regionprofile.regionDataURI = (string)reader["regionDataURI"];
                regionprofile.regionOnline = false; // Needs to be pinged before this can be set.
                regionprofile.serverIP = (string)reader["serverIP"];
                regionprofile.serverPort = Convert.ToUInt32(reader["serverPort"]);
                regionprofile.serverURI = (string)reader["serverURI"];
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
                regionprofile.regionAssetURI = (string)reader["regionAssetURI"];
                regionprofile.regionAssetRecvKey = (string)reader["regionAssetRecvKey"];
                regionprofile.regionAssetSendKey = (string)reader["regionAssetSendKey"];

                // Userserver
                regionprofile.regionUserURI = (string)reader["regionUserURI"];
                regionprofile.regionUserRecvKey = (string)reader["regionUserRecvKey"];
                regionprofile.regionUserSendKey = (string)reader["regionUserSendKey"];
                regionprofile.owner_uuid = new LLUUID((string) reader["owner_uuid"]);
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
                retval.ID = new LLUUID((string)reader["UUID"]);
                retval.FirstName = (string)reader["username"];
                retval.SurName = (string)reader["lastname"];

                retval.PasswordHash = (string)reader["passwordHash"];
                retval.PasswordSalt = (string)reader["passwordSalt"];

                retval.HomeRegion = Convert.ToUInt64(reader["homeRegion"].ToString());
                retval.HomeLocation = new LLVector3(
                    Convert.ToSingle(reader["homeLocationX"].ToString()),
                    Convert.ToSingle(reader["homeLocationY"].ToString()),
                    Convert.ToSingle(reader["homeLocationZ"].ToString()));
                retval.HomeLookAt = new LLVector3(
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

                retval.Image = new LLUUID((string)reader["profileImage"]);
                retval.FirstLifeImage = new LLUUID((string)reader["profileFirstImage"]);
                retval.WebLoginKey = new LLUUID((string)reader["webLoginKey"]);
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
                retval.ProfileID = new LLUUID((string)reader["UUID"]);
                retval.SessionID = new LLUUID((string)reader["sessionID"]);
                retval.SecureSessionID = new LLUUID((string)reader["secureSessionID"]);

                // Agent Who?
                retval.AgentIP = (string)reader["agentIP"];
                retval.AgentPort = Convert.ToUInt32(reader["agentPort"].ToString());
                retval.AgentOnline = Convert.ToBoolean(reader["agentOnline"].ToString());

                // Login/Logout times (UNIX Epoch)
                retval.LoginTime = Convert.ToInt32(reader["loginTime"].ToString());
                retval.LogoutTime = Convert.ToInt32(reader["logoutTime"].ToString());

                // Current position
                retval.Region = (string)reader["currentRegion"];
                retval.Handle = Convert.ToUInt64(reader["currentHandle"].ToString());
                LLVector3 tmp_v;
                LLVector3.TryParse((string)reader["currentPos"], out tmp_v);
                retval.Position = tmp_v;

            }
            else
            {
                return null;
            }
            return retval;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public AssetBase getAssetRow(IDataReader reader)
        {
            AssetBase asset = new AssetBase();
            if (reader.Read())
            {
                // Region Main

                asset = new AssetBase();
                asset.Data = (byte[])reader["data"];
                asset.Description = (string)reader["description"];
                asset.FullID = new LLUUID((string)reader["id"]);
                asset.Local = Convert.ToBoolean(reader["local"]); // ((sbyte)reader["local"]) != 0 ? true : false;
                asset.Name = (string)reader["name"];
                asset.Type = Convert.ToSByte(reader["assetType"]);
            }
            else
            {
                return null; // throw new Exception("No rows to return");
            }
            return asset;
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
                m_log.Error(e.ToString());
                return false;
            }

            return returnval;
        }

        /// <summary>
        /// Execute a SQL statement stored in a resource, as a string
        /// </summary>
        /// <param name="name">the ressource string</param>
        public void ExecuteResourceSql(string name)
        {
            SqlCommand cmd = new SqlCommand(getResourceString(name), (SqlConnection)dbcon);
            cmd.ExecuteNonQuery();
            cmd.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>The actual SqlConnection</returns>
        public SqlConnection getConnection()
        {
            return (SqlConnection)dbcon;
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
                            string tableName = (string)tables["TABLE_NAME"];
                            if (tableList.ContainsKey(tableName))
                                tableList[tableName] = tableName;
                        }
                        catch (Exception e)
                        {
                            m_log.Error(e.ToString());
                        }
                    }
                    tables.Close();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
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
            // string dllName = module.Assembly.ManifestModule.Name;
            Version dllVersion = module.Assembly.GetName().Version;


            return
                string.Format("{0}.{1}.{2}.{3}", dllVersion.Major, dllVersion.Minor, dllVersion.Build,
                              dllVersion.Revision);
        }
    }
}
