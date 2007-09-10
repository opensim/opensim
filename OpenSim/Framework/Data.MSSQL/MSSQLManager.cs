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
using System.Data.SqlClient;
using libsecondlife;

namespace OpenSim.Framework.Data.MSSQL
{
    /// <summary>
    /// A management class for the MS SQL Storage Engine
    /// </summary>
    class MSSqlManager
    {
        /// <summary>
        /// The database connection object
        /// </summary>
        IDbConnection dbcon;

        /// <summary>
        /// Initialises and creates a new Sql connection and maintains it.
        /// </summary>
        /// <param name="hostname">The Sql server being connected to</param>
        /// <param name="database">The name of the Sql database being used</param>
        /// <param name="username">The username logging into the database</param>
        /// <param name="password">The password for the user logging in</param>
        /// <param name="cpooling">Whether to use connection pooling or not, can be one of the following: 'yes', 'true', 'no' or 'false', if unsure use 'false'.</param>
        public MSSqlManager(string hostname, string database, string username, string password, string cpooling)
        {
            try
            {
                string connectionString = "Server=" + hostname + ";Database=" + database + ";User ID=" + username + ";Password=" + password + ";Pooling=" + cpooling + ";";
                dbcon = new SqlConnection(connectionString);

                dbcon.Open();
            }
            catch (Exception e)
            {
                throw new Exception("Error initialising Sql Database: " + e.ToString());
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
        public SimProfileData getRow(IDataReader reader)
        {
            SimProfileData regionprofile = new SimProfileData();

            if (reader.Read())
            {
                // Region Main
                regionprofile.regionHandle = (ulong)reader["regionHandle"];
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
                regionprofile.serverPort = (uint)reader["serverPort"];
                regionprofile.serverURI = (string)reader["serverURI"];

                // Location
                regionprofile.regionLocX = (uint)((int)reader["locX"]);
                regionprofile.regionLocY = (uint)((int)reader["locY"]);
                regionprofile.regionLocZ = (uint)((int)reader["locZ"]);

                // Neighbours - 0 = No Override
                regionprofile.regionEastOverrideHandle = (ulong)reader["eastOverrideHandle"];
                regionprofile.regionWestOverrideHandle = (ulong)reader["westOverrideHandle"];
                regionprofile.regionSouthOverrideHandle = (ulong)reader["southOverrideHandle"];
                regionprofile.regionNorthOverrideHandle = (ulong)reader["northOverrideHandle"];

                // Assets
                regionprofile.regionAssetURI = (string)reader["regionAssetURI"];
                regionprofile.regionAssetRecvKey = (string)reader["regionAssetRecvKey"];
                regionprofile.regionAssetSendKey = (string)reader["regionAssetSendKey"];

                // Userserver
                regionprofile.regionUserURI = (string)reader["regionUserURI"];
                regionprofile.regionUserRecvKey = (string)reader["regionUserRecvKey"];
                regionprofile.regionUserSendKey = (string)reader["regionUserSendKey"];
            }
            else
            {
                throw new Exception("No rows to return");
            }
            return regionprofile;
        }

        /// <summary>
        /// Creates a new region in the database
        /// </summary>
        /// <param name="profile">The region profile to insert</param>
        /// <returns>Successful?</returns>
        public bool insertRow(SimProfileData profile)
        {
            string sql = "REPLACE INTO regions VALUES (regionHandle, regionName, uuid, regionRecvKey, regionSecret, regionSendKey, regionDataURI, ";
            sql += "serverIP, serverPort, serverURI, locX, locY, locZ, eastOverrideHandle, westOverrideHandle, southOverrideHandle, northOverrideHandle, regionAssetURI, regionAssetRecvKey, ";
            sql += "regionAssetSendKey, regionUserURI, regionUserRecvKey, regionUserSendKey) VALUES ";

            sql += "(@regionHandle, @regionName, @uuid, @regionRecvKey, @regionSecret, @regionSendKey, @regionDataURI, ";
            sql += "@serverIP, @serverPort, @serverURI, @locX, @locY, @locZ, @eastOverrideHandle, @westOverrideHandle, @southOverrideHandle, @northOverrideHandle, @regionAssetURI, @regionAssetRecvKey, ";
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
