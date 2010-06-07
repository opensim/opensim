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

namespace OpenSim.Data.SQLite
{
    /// <summary>
    /// SQLite Manager
    /// </summary>
    internal class SQLiteManager : SQLiteUtil
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IDbConnection dbcon;

        /// <summary>
        /// <list type="bullet">
        /// <item>Initialises and creates a new SQLite connection and maintains it.</item>
        /// <item>use default URI if connect string is empty.</item>
        /// </list>
        /// </summary>
        /// <param name="connect">connect string</param>
        public SQLiteManager(string connect)
        {
            try
            {
                string connectionString = String.Empty;
                if (connect != String.Empty)
                {
                    connectionString = connect;
                }
                else
                {
                    m_log.Warn("[SQLITE] grid db not specified, using default");
                    connectionString = "URI=file:GridServerSqlite.db;";
                }

                dbcon = new SqliteConnection(connectionString);

                dbcon.Open();
            }
            catch (Exception e)
            {
                throw new Exception("Error initialising SQLite Database: " + e.ToString());
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
        /// <returns>A SQLite DB Command</returns>
        public IDbCommand Query(string sql, Dictionary<string, string> parameters)
        {
            SqliteCommand dbcommand = (SqliteCommand) dbcon.CreateCommand();
            dbcommand.CommandText = sql;
            foreach (KeyValuePair<string, string> param in parameters)
            {
                SqliteParameter paramx = new SqliteParameter(param.Key, param.Value);
                dbcommand.Parameters.Add(paramx);
            }

            return (IDbCommand) dbcommand;
        }

        /// <summary>
        /// Reads a region row from a database reader
        /// </summary>
        /// <param name="reader">An active database reader</param>
        /// <returns>A region profile</returns>
        public RegionProfileData getRow(IDataReader reader)
        {
            RegionProfileData retval = new RegionProfileData();

            if (reader.Read())
            {
                // Region Main
                retval.regionHandle = (ulong) reader["regionHandle"];
                retval.regionName = (string) reader["regionName"];
                retval.UUID = new UUID((string) reader["uuid"]);

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

                // Location
                retval.regionLocX = (uint) ((int) reader["locX"]);
                retval.regionLocY = (uint) ((int) reader["locY"]);
                retval.regionLocZ = (uint) ((int) reader["locZ"]);

                // Neighbours - 0 = No Override
                retval.regionEastOverrideHandle = (ulong) reader["eastOverrideHandle"];
                retval.regionWestOverrideHandle = (ulong) reader["westOverrideHandle"];
                retval.regionSouthOverrideHandle = (ulong) reader["southOverrideHandle"];
                retval.regionNorthOverrideHandle = (ulong) reader["northOverrideHandle"];

                // Assets
                retval.regionAssetURI = (string) reader["regionAssetURI"];
                retval.regionAssetRecvKey = (string) reader["regionAssetRecvKey"];
                retval.regionAssetSendKey = (string) reader["regionAssetSendKey"];

                // Userserver
                retval.regionUserURI = (string) reader["regionUserURI"];
                retval.regionUserRecvKey = (string) reader["regionUserRecvKey"];
                retval.regionUserSendKey = (string) reader["regionUserSendKey"];
            }
            else
            {
                throw new Exception("No rows to return");
            }
            return retval;
        }

        /// <summary>
        /// Inserts a new region into the database
        /// </summary>
        /// <param name="profile">The region to insert</param>
        /// <returns>Success?</returns>
        public bool insertRow(RegionProfileData profile)
        {
            string sql =
                "REPLACE INTO regions VALUES (regionHandle, regionName, uuid, regionRecvKey, regionSecret, regionSendKey, regionDataURI, ";
            sql +=
                "serverIP, serverPort, serverURI, locX, locY, locZ, eastOverrideHandle, westOverrideHandle, southOverrideHandle, northOverrideHandle, regionAssetURI, regionAssetRecvKey, ";
            sql += "regionAssetSendKey, regionUserURI, regionUserRecvKey, regionUserSendKey) VALUES ";

            sql += "(@regionHandle, @regionName, @uuid, @regionRecvKey, @regionSecret, @regionSendKey, @regionDataURI, ";
            sql +=
                "@serverIP, @serverPort, @serverURI, @locX, @locY, @locZ, @eastOverrideHandle, @westOverrideHandle, @southOverrideHandle, @northOverrideHandle, @regionAssetURI, @regionAssetRecvKey, ";
            sql += "@regionAssetSendKey, @regionUserURI, @regionUserRecvKey, @regionUserSendKey);";

            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters["regionHandle"] = profile.regionHandle.ToString();
            parameters["regionName"] = profile.regionName;
            parameters["uuid"] = profile.UUID.ToString();
            parameters["regionRecvKey"] = profile.regionRecvKey;
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

            bool returnval = false;

            try
            {
                IDbCommand result = Query(sql, parameters);

                if (result.ExecuteNonQuery() == 1)
                    returnval = true;

                result.Dispose();
            }
            catch (Exception)
            {
                return false;
            }

            return returnval;
        }
    }
}
