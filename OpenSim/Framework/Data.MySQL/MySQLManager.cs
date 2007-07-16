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
using System.Collections.Generic;
using System.Data;
using libsecondlife;
using MySql.Data.MySqlClient;

namespace OpenSim.Framework.Data.MySQL
{
    /// <summary>
    /// A MySQL Database manager
    /// </summary>
    class MySQLManager
    {
        /// <summary>
        /// The database connection object
        /// </summary>
        IDbConnection dbcon;
        /// <summary>
        /// Connection string for ADO.net
        /// </summary>
        string connectionString;

        /// <summary>
        /// Initialises and creates a new MySQL connection and maintains it.
        /// </summary>
        /// <param name="hostname">The MySQL server being connected to</param>
        /// <param name="database">The name of the MySQL database being used</param>
        /// <param name="username">The username logging into the database</param>
        /// <param name="password">The password for the user logging in</param>
        /// <param name="cpooling">Whether to use connection pooling or not, can be one of the following: 'yes', 'true', 'no' or 'false', if unsure use 'false'.</param>
        public MySQLManager(string hostname, string database, string username, string password, string cpooling, string port)
        {
            try
            {
                connectionString = "Server=" + hostname + ";Port=" + port + ";Database=" + database + ";User ID=" + username + ";Password=" + password + ";Pooling=" + cpooling + ";";
                dbcon = new MySqlConnection(connectionString);

                dbcon.Open();

                Console.WriteLine("MySQL connection established");
            }
            catch (Exception e)
            {
                throw new Exception("Error initialising MySql Database: " + e.ToString());
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
                    dbcon = new MySqlConnection(connectionString);
                    dbcon.Open();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unable to reconnect to database " + e.ToString());
                }
            }
        }

        /// <summary>
        /// Runs a query with protection against SQL Injection by using parameterised input.
        /// </summary>
        /// <param name="sql">The SQL string - replace any variables such as WHERE x = "y" with WHERE x = @y</param>
        /// <param name="parameters">The parameters - index so that @y is indexed as 'y'</param>
        /// <returns>A MySQL DB Command</returns>
        public IDbCommand Query(string sql, Dictionary<string, string> parameters)
        {
            try
            {
                MySqlCommand dbcommand = (MySqlCommand)dbcon.CreateCommand();
                dbcommand.CommandText = sql;
                foreach (KeyValuePair<string, string> param in parameters)
                {
                    dbcommand.Parameters.Add(param.Key, param.Value);
                }

                return (IDbCommand)dbcommand;
            }
            catch
            {
                lock (dbcon)
                {
                    // Close the DB connection
                    try
                    {
                        dbcon.Close();
                    }
                    catch { }

                    // Try reopen it
                    try
                    {
                        dbcon = new MySqlConnection(connectionString);
                        dbcon.Open();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Unable to reconnect to database " + e.ToString());
                    }

                    // Run the query again
                    try
                    {
                        MySqlCommand dbcommand = (MySqlCommand)dbcon.CreateCommand();
                        dbcommand.CommandText = sql;
                        foreach (KeyValuePair<string, string> param in parameters)
                        {
                            dbcommand.Parameters.Add(param.Key, param.Value);
                        }

                        return (IDbCommand)dbcommand;
                    }
                    catch (Exception e)
                    {
                        // Return null if it fails.
                        Console.WriteLine("Failed during Query generation: " + e.ToString());
                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// Reads a region row from a database reader
        /// </summary>
        /// <param name="reader">An active database reader</param>
        /// <returns>A region profile</returns>
        public SimProfileData readSimRow(IDataReader reader)
        {
            SimProfileData retval = new SimProfileData();

            if (reader.Read())
            {
                // Region Main
                retval.regionHandle = Convert.ToUInt64(reader["regionHandle"].ToString());
                retval.regionName = (string)reader["regionName"];
                retval.UUID = new LLUUID((string)reader["uuid"]);

                // Secrets
                retval.regionRecvKey = (string)reader["regionRecvKey"];
                retval.regionSecret = (string)reader["regionSecret"];
                retval.regionSendKey = (string)reader["regionSendKey"];

                // Region Server
                retval.regionDataURI = (string)reader["regionDataURI"];
                retval.regionOnline = false; // Needs to be pinged before this can be set.
                retval.serverIP = (string)reader["serverIP"];
                retval.serverPort = (uint)reader["serverPort"];
                retval.serverURI = (string)reader["serverURI"];
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
                retval.regionAssetURI = (string)reader["regionAssetURI"];
                retval.regionAssetRecvKey = (string)reader["regionAssetRecvKey"];
                retval.regionAssetSendKey = (string)reader["regionAssetSendKey"];

                // Userserver
                retval.regionUserURI = (string)reader["regionUserURI"];
                retval.regionUserRecvKey = (string)reader["regionUserRecvKey"];
                retval.regionUserSendKey = (string)reader["regionUserSendKey"];

                // World Map Addition
                string tempRegionMap = reader["regionMapTexture"].ToString();
                if (tempRegionMap != "")
                {
                    retval.regionMapTextureID = new LLUUID(tempRegionMap);
                }
                else
                {
                    retval.regionMapTextureID = new LLUUID();
                }
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
                retval.gridRecvKey = (string)reader["gridRecvKey"];
                retval.gridSendKey = (string)reader["gridSendKey"];
                retval.reservationCompany = (string)reader["resCompany"];
                retval.reservationMaxX = Convert.ToInt32(reader["resXMax"].ToString());
                retval.reservationMaxY = Convert.ToInt32(reader["resYMax"].ToString());
                retval.reservationMinX = Convert.ToInt32(reader["resXMin"].ToString());
                retval.reservationMinY = Convert.ToInt32(reader["resYMin"].ToString());
                retval.reservationName = (string)reader["resName"];
                retval.status = Convert.ToInt32(reader["status"].ToString()) == 1;
                retval.userUUID = new LLUUID((string)reader["userUUID"]);

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
                retval.UUID = new LLUUID((string)reader["UUID"]);
                retval.sessionID = new LLUUID((string)reader["sessionID"]);
                retval.secureSessionID = new LLUUID((string)reader["secureSessionID"]);

                // Agent Who?
                retval.agentIP = (string)reader["agentIP"];
                retval.agentPort = Convert.ToUInt32(reader["agentPort"].ToString());
                retval.agentOnline = Convert.ToBoolean(reader["agentOnline"].ToString());

                // Login/Logout times (UNIX Epoch)
                retval.loginTime = Convert.ToInt32(reader["loginTime"].ToString());
                retval.logoutTime = Convert.ToInt32(reader["logoutTime"].ToString());

                // Current position
                retval.currentRegion = (string)reader["currentRegion"];
                retval.currentHandle = Convert.ToUInt64(reader["currentHandle"].ToString());
                LLVector3.TryParse((string)reader["currentPos"], out retval.currentPos);
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
                retval.UUID = new LLUUID((string)reader["UUID"]);
                retval.username = (string)reader["username"];
                retval.surname = (string)reader["lastname"];

                retval.passwordHash = (string)reader["passwordHash"];
                retval.passwordSalt = (string)reader["passwordSalt"];

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

                retval.userInventoryURI = (string)reader["userInventoryURI"];
                retval.userAssetURI = (string)reader["userAssetURI"];

                retval.profileCanDoMask = Convert.ToUInt32(reader["profileCanDoMask"].ToString());
                retval.profileWantDoMask = Convert.ToUInt32(reader["profileWantDoMask"].ToString());

                retval.profileAboutText = (string)reader["profileAboutText"];
                retval.profileFirstText = (string)reader["profileFirstText"];

                retval.profileImage = new LLUUID((string)reader["profileImage"]);
                retval.profileFirstImage = new LLUUID((string)reader["profileFirstImage"]);

            }
            else
            {
                return null;
            }
            return retval;
        }

        /// <summary>
        /// Reads a list of inventory folders returned by a query.
        /// </summary>
        /// <param name="reader">A MySQL Data Reader</param>
        /// <returns>A List containing inventory folders</returns>
        public List<InventoryFolderBase> readInventoryFolders(IDataReader reader)
        {
            List<InventoryFolderBase> rows = new List<InventoryFolderBase>();

            while(reader.Read())
            {
                try
                {
                    InventoryFolderBase folder = new InventoryFolderBase();

                    folder.agentID = new LLUUID((string)reader["agentID"]);
                    folder.parentID = new LLUUID((string)reader["parentFolderID"]);
                    folder.folderID = new LLUUID((string)reader["folderID"]);
                    folder.name = (string)reader["folderName"];

                    rows.Add(folder);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            return rows;
        }

        /// <summary>
        /// Reads a collection of items from an SQL result
        /// </summary>
        /// <param name="reader">The SQL Result</param>
        /// <returns>A List containing Inventory Items</returns>
        public List<InventoryItemBase> readInventoryItems(IDataReader reader)
        {
            List<InventoryItemBase> rows = new List<InventoryItemBase>();

            while (reader.Read())
            {
                try
                {
                    InventoryItemBase item = new InventoryItemBase();

                    item.assetID = new LLUUID((string)reader["assetID"]);
                    item.avatarID = new LLUUID((string)reader["avatarID"]);
                    item.inventoryCurrentPermissions = Convert.ToUInt32(reader["inventoryCurrentPermissions"].ToString());
                    item.inventoryDescription = (string)reader["inventoryDescription"];
                    item.inventoryID = new LLUUID((string)reader["inventoryID"]);
                    item.inventoryName = (string)reader["inventoryName"];
                    item.inventoryNextPermissions = Convert.ToUInt32(reader["inventoryNextPermissions"].ToString());
                    item.parentFolderID = new LLUUID((string)reader["parentFolderID"]);
                    item.type = Convert.ToInt32(reader["type"].ToString());

                    rows.Add(item);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            return rows;
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
        public bool insertLogRow(string serverDaemon, string target, string methodCall, string arguments, int priority, string logMessage)
        {
            string sql = "INSERT INTO logs (`target`, `server`, `method`, `arguments`, `priority`, `message`) VALUES ";
            sql += "(?target, ?server, ?method, ?arguments, ?priority, ?message)";

            Dictionary<string, string> parameters = new Dictionary<string, string>();
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
                Console.WriteLine(e.ToString());
                return false;
            }

            return returnval;
        }

        /// <summary>
        /// Inserts a new item into the database
        /// </summary>
        /// <param name="item">The item</param>
        /// <returns>Success?</returns>
        public bool insertItem(InventoryItemBase item)
        {
            string sql = "REPLACE INTO inventoryitems (inventoryID, assetID, type, parentFolderID, avatarID, inventoryName, inventoryDescription, inventoryNextPermissions, inventoryCurrentPermissions) VALUES ";
            sql += "(?inventoryID, ?assetID, ?type, ?parentFolderID, ?avatarID, ?inventoryName, ?inventoryDescription, ?inventoryNextPermissions, ?inventoryCurrentPermissions)";

            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters["?inventoryID"] = item.inventoryID.ToStringHyphenated();
            parameters["?assetID"] = item.assetID.ToStringHyphenated();
            parameters["?type"] = item.type.ToString();
            parameters["?parentFolderID"] = item.parentFolderID.ToStringHyphenated();
            parameters["?avatarID"] = item.avatarID.ToStringHyphenated();
            parameters["?inventoryName"] = item.inventoryName;
            parameters["?inventoryDescription"] = item.inventoryDescription;
            parameters["?inventoryNextPermissions"] = item.inventoryNextPermissions.ToString();
            parameters["?inventoryCurrentPermissions"] = item.inventoryCurrentPermissions.ToString();

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
                Console.WriteLine(e.ToString());
                return false;
            }

            return returnval;
        }

        /// <summary>
        /// Inserts a new folder into the database
        /// </summary>
        /// <param name="folder">The folder</param>
        /// <returns>Success?</returns>
        public bool insertFolder(InventoryFolderBase folder)
        {
            string sql = "REPLACE INTO inventoryfolders (folderID, agentID, parentFolderID, folderName) VALUES ";
            sql += "(?folderID, ?agentID, ?parentFolderID, ?folderName)";

            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters["?folderID"] = folder.folderID.ToStringHyphenated();
            parameters["?agentID"] = folder.agentID.ToStringHyphenated();
            parameters["?parentFolderID"] = folder.parentID.ToStringHyphenated();
            parameters["?folderName"] = folder.name;

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
                Console.WriteLine(e.ToString());
                return false;
            }
            return returnval;
        }

        /// <summary>
        /// Inserts a new region into the database
        /// </summary>
        /// <param name="profile">The region to insert</param>
        /// <returns>Success?</returns>
        public bool insertRegion(SimProfileData regiondata)
        {
            string sql = "REPLACE INTO regions (regionHandle, regionName, uuid, regionRecvKey, regionSecret, regionSendKey, regionDataURI, ";
            sql += "serverIP, serverPort, serverURI, locX, locY, locZ, eastOverrideHandle, westOverrideHandle, southOverrideHandle, northOverrideHandle, regionAssetURI, regionAssetRecvKey, ";
            sql += "regionAssetSendKey, regionUserURI, regionUserRecvKey, regionUserSendKey, regionMapTexture, serverHttpPort, serverRemotingPort) VALUES ";

            sql += "(?regionHandle, ?regionName, ?uuid, ?regionRecvKey, ?regionSecret, ?regionSendKey, ?regionDataURI, ";
            sql += "?serverIP, ?serverPort, ?serverURI, ?locX, ?locY, ?locZ, ?eastOverrideHandle, ?westOverrideHandle, ?southOverrideHandle, ?northOverrideHandle, ?regionAssetURI, ?regionAssetRecvKey, ";
            sql += "?regionAssetSendKey, ?regionUserURI, ?regionUserRecvKey, ?regionUserSendKey, ?regionMapTexture, ?serverHttpPort, ?serverRemotingPort);";

            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters["?regionHandle"] = regiondata.regionHandle.ToString();
            parameters["?regionName"] = regiondata.regionName.ToString();
            parameters["?uuid"] = regiondata.UUID.ToStringHyphenated();
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
            parameters["?regionMapTexture"] = regiondata.regionMapTextureID.ToStringHyphenated();
            parameters["?serverHttpPort"] = regiondata.httpPort.ToString();
            parameters["?serverRemotingPort"] = regiondata.remotingPort.ToString();

            bool returnval = false;

            try
            {
                
                IDbCommand result = Query(sql, parameters);

                //Console.WriteLine(result.CommandText);

                if (result.ExecuteNonQuery() == 1)
                    returnval = true;

                result.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }

            return returnval;
        }
    }
}
