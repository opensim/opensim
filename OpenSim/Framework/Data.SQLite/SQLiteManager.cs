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
using System.Data.SQLite;
using libsecondlife;
using Mono.Data.SqliteClient;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Data.SQLite
{
    internal class SQLiteManager : SQLiteBase
    {
        private IDbConnection dbcon;

        /// <summary>
        /// Initialises and creates a new SQLite connection and maintains it.
        /// </summary>
        /// <param name="hostname">The SQLite server being connected to</param>
        /// <param name="database">The name of the SQLite database being used</param>
        /// <param name="username">The username logging into the database</param>
        /// <param name="password">The password for the user logging in</param>
        /// <param name="cpooling">Whether to use connection pooling or not, can be one of the following: 'yes', 'true', 'no' or 'false', if unsure use 'false'.</param>
        public SQLiteManager(string hostname, string database, string username, string password, string cpooling)
        {
            try
            {
                string connectionString = "URI=file:GridServerSqlite.db;";
                dbcon = new SQLiteConnection(connectionString);

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
            SQLiteCommand dbcommand = (SQLiteCommand) dbcon.CreateCommand();
            dbcommand.CommandText = sql;
            foreach (KeyValuePair<string, string> param in parameters)
            {
                SQLiteParameter paramx = new SQLiteParameter(param.Key, param.Value);
                dbcommand.Parameters.Add(paramx);
            }

            return (IDbCommand) dbcommand;
        }

        private bool TestTables(SQLiteConnection conn)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM regions", conn);
            SQLiteDataAdapter pDa = new SQLiteDataAdapter(cmd);
            DataSet tmpDS = new DataSet();
            try
            {
                pDa.Fill(tmpDS, "regions");
            }
            catch (SqliteSyntaxException)
            {
                MainLog.Instance.Verbose("DATASTORE", "SQLite Database doesn't exist... creating");
                InitDB(conn);
            }
            return true;
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

            // Add in contraints
            regions.PrimaryKey = new DataColumn[] {regions.Columns["UUID"]};
            return regions;
        }

        private void InitDB(SQLiteConnection conn)
        {
            string createUsers = defineTable(createRegionsTable());
            SQLiteCommand pcmd = new SQLiteCommand(createUsers, conn);
            conn.Open();
            pcmd.ExecuteNonQuery();
            conn.Close();
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
                retval.UUID = new LLUUID((string) reader["uuid"]);

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
