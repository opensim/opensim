using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

// MySQL Native
using MySql;
using MySql.Data;
using MySql.Data.Types;
using MySql.Data.MySqlClient;

using OpenGrid.Framework.Data;

namespace OpenGrid.Framework.Data.MySQL
{
    class MySQLManager
    {
        IDbConnection dbcon;

        /// <summary>
        /// Initialises and creates a new MySQL connection and maintains it.
        /// </summary>
        /// <param name="hostname">The MySQL server being connected to</param>
        /// <param name="database">The name of the MySQL database being used</param>
        /// <param name="username">The username logging into the database</param>
        /// <param name="password">The password for the user logging in</param>
        /// <param name="cpooling">Whether to use connection pooling or not, can be one of the following: 'yes', 'true', 'no' or 'false', if unsure use 'false'.</param>
        public MySQLManager(string hostname, string database, string username, string password, string cpooling)
        {
            try
            {
                string connectionString = "Server=" + hostname + ";Port=13306;Database=" + database + ";User ID=" + username + ";Password=" + password + ";Pooling=" + cpooling + ";";
                dbcon = new MySqlConnection(connectionString);

                dbcon.Open();
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
            catch (Exception e)
            {
                Console.WriteLine("Failed during Query generation: " + e.ToString());
                return null;
            }
        }

        public SimProfileData getSimRow(IDataReader reader)
        {
            SimProfileData retval = new SimProfileData();

            if (reader.Read())
            {
                // Region Main
                retval.regionHandle = Convert.ToUInt64(reader["regionHandle"].ToString());
                retval.regionName = (string)reader["regionName"];
                retval.UUID = new libsecondlife.LLUUID((string)reader["uuid"]);

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
                retval.regionMapTextureID = new libsecondlife.LLUUID((string)reader["regionMapTexture"]);
            }
            else
            {
                return null;
            }
            return retval;
        }

        public UserAgentData getAgentRow(IDataReader reader)
        {
            UserAgentData retval = new UserAgentData();

            if (reader.Read())
            {
                // Agent IDs
                retval.UUID = new libsecondlife.LLUUID((string)reader["UUID"]);
                retval.sessionID = new libsecondlife.LLUUID((string)reader["sessionID"]);
                retval.secureSessionID = new libsecondlife.LLUUID((string)reader["secureSessionID"]);

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
                libsecondlife.LLVector3.TryParse((string)reader["currentPos"], out retval.currentPos);
            }
            else
            {
                return null;
            }
            return retval;
        }

        public UserProfileData getUserRow(IDataReader reader)
        {
            UserProfileData retval = new UserProfileData();

            if (reader.Read())
            {
                retval.UUID = new libsecondlife.LLUUID((string)reader["UUID"]);
                retval.username = (string)reader["username"];
                retval.surname = (string)reader["lastname"];

                retval.passwordHash = (string)reader["passwordHash"];
                retval.passwordSalt = (string)reader["passwordSalt"];

                retval.homeRegion = Convert.ToUInt64(reader["homeRegion"].ToString());
                retval.homeLocation = new libsecondlife.LLVector3(
                    Convert.ToSingle(reader["homeLocationX"].ToString()),
                    Convert.ToSingle(reader["homeLocationY"].ToString()),
                    Convert.ToSingle(reader["homeLocationZ"].ToString()));
                retval.homeLookAt = new libsecondlife.LLVector3(
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

                retval.profileImage = new libsecondlife.LLUUID((string)reader["profileImage"]);
                retval.profileFirstImage = new libsecondlife.LLUUID((string)reader["profileFirstImage"]);

            }
            else
            {
                return null;
            }
            return retval;
        }

        public bool insertRow(SimProfileData profile)
        {
            string sql = "REPLACE INTO regions (regionHandle, regionName, uuid, regionRecvKey, regionSecret, regionSendKey, regionDataURI, ";
            sql += "serverIP, serverPort, serverURI, locX, locY, locZ, eastOverrideHandle, westOverrideHandle, southOverrideHandle, northOverrideHandle, regionAssetURI, regionAssetRecvKey, ";
            sql += "regionAssetSendKey, regionUserURI, regionUserRecvKey, regionUserSendKey, regionMapTexture) VALUES ";

            sql += "(?regionHandle, ?regionName, ?uuid, ?regionRecvKey, ?regionSecret, ?regionSendKey, ?regionDataURI, ";
            sql += "?serverIP, ?serverPort, ?serverURI, ?locX, ?locY, ?locZ, ?eastOverrideHandle, ?westOverrideHandle, ?southOverrideHandle, ?northOverrideHandle, ?regionAssetURI, ?regionAssetRecvKey, ";
            sql += "?regionAssetSendKey, ?regionUserURI, ?regionUserRecvKey, ?regionUserSendKey, ?regionMapTexture);";

            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters["?regionHandle"] = profile.regionHandle.ToString();
            parameters["?regionName"] = profile.regionName.ToString();
            parameters["?uuid"] = profile.UUID.ToStringHyphenated();
            parameters["?regionRecvKey"] = profile.regionRecvKey.ToString();
            parameters["?regionSecret"] = profile.regionSecret.ToString();
            parameters["?regionSendKey"] = profile.regionSendKey.ToString();
            parameters["?regionDataURI"] = profile.regionDataURI.ToString();
            parameters["?serverIP"] = profile.serverIP.ToString();
            parameters["?serverPort"] = profile.serverPort.ToString();
            parameters["?serverURI"] = profile.serverURI.ToString();
            parameters["?locX"] = profile.regionLocX.ToString();
            parameters["?locY"] = profile.regionLocY.ToString();
            parameters["?locZ"] = profile.regionLocZ.ToString();
            parameters["?eastOverrideHandle"] = profile.regionEastOverrideHandle.ToString();
            parameters["?westOverrideHandle"] = profile.regionWestOverrideHandle.ToString();
            parameters["?northOverrideHandle"] = profile.regionNorthOverrideHandle.ToString();
            parameters["?southOverrideHandle"] = profile.regionSouthOverrideHandle.ToString();
            parameters["?regionAssetURI"] = profile.regionAssetURI.ToString();
            parameters["?regionAssetRecvKey"] = profile.regionAssetRecvKey.ToString();
            parameters["?regionAssetSendKey"] = profile.regionAssetSendKey.ToString();
            parameters["?regionUserURI"] = profile.regionUserURI.ToString();
            parameters["?regionUserRecvKey"] = profile.regionUserRecvKey.ToString();
            parameters["?regionUserSendKey"] = profile.regionUserSendKey.ToString();
            parameters["?regionMapTexture"] = profile.regionMapTextureID.ToStringHyphenated();

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
