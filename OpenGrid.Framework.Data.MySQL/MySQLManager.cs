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
                string connectionString = "Server=" + hostname + ";Database=" + database + ";User ID=" + username + ";Password=" + password + ";Pooling=" + cpooling + ";";
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
            MySqlCommand dbcommand = (MySqlCommand)dbcon.CreateCommand();
            dbcommand.CommandText = sql;
            foreach (KeyValuePair<string, string> param in parameters)
            {
                dbcommand.Parameters.Add(param.Key, param.Value);
            }

            return (IDbCommand)dbcommand;
        }

        public SimProfileData getRow(IDataReader reader)
        {
            SimProfileData retval = new SimProfileData();

            if (reader.Read())
            {
                // Region Main
                retval.regionHandle = (ulong)reader["regionHandle"];
                retval.regionName = (string)reader["regionName"];
                retval.UUID = new libsecondlife.LLUUID((string)reader["uuid"]);

                // Secrets
                retval.regionRecvKey = (string)reader["regionRecvKey"];
                retval.regionSecret = (string)reader["regionSecret"];
                retval.regionSendKey = (string)reader["regionSendKey"];

                // Region Server
                retval.regionDataURI = (string)reader["regionDataURI"];
                retval.regionOnline = false;
                retval.serverIP = "";
                retval.serverPort = 0;
                retval.serverURI = "";

                // Location
                retval.regionLocX = (uint)((int)reader["locX"]);
                retval.regionLocY = (uint)((int)reader["locY"]);
                retval.regionLocZ = (uint)((int)reader["locZ"]);

                // Neighbours - 0 = No Override
                retval.regionEastOverrideHandle = (ulong)reader["eastOverrideHandle"];
                retval.regionWestOverrideHandle = (ulong)reader["westOverrideHandle"];
                retval.regionSouthOverrideHandle = (ulong)reader["southOverrideHandle"];
                retval.regionNorthOverrideHandle = (ulong)reader["northOverrideHandle"];
            }
            else
            {
                return null;
            }
            return retval;
        }
    }
}
