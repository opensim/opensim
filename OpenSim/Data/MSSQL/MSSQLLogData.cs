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

using System.Collections.Generic;
using System.Data;

namespace OpenSim.Data.MSSQL
{
    /// <summary>
    /// An interface to the log database for MSSQL
    /// </summary>
    internal class MSSQLLogData : ILogData
    {
        /// <summary>
        /// The database manager
        /// </summary>
        public MSSQLManager database;

        /// <summary>
        /// Artificial constructor called when the plugin is loaded
        /// </summary>
        public void Initialise(string connect)
        {
            // TODO: do something with the connect string
            IniFile gridDataMSSqlFile = new IniFile("mssql_connection.ini");
            string settingDataSource = gridDataMSSqlFile.ParseFileReadValue("data_source");
            string settingInitialCatalog = gridDataMSSqlFile.ParseFileReadValue("initial_catalog");
            string settingPersistSecurityInfo = gridDataMSSqlFile.ParseFileReadValue("persist_security_info");
            string settingUserId = gridDataMSSqlFile.ParseFileReadValue("user_id");
            string settingPassword = gridDataMSSqlFile.ParseFileReadValue("password");

            database =
                new MSSQLManager(settingDataSource, settingInitialCatalog, settingPersistSecurityInfo, settingUserId,
                                 settingPassword);

            using (IDbCommand cmd = database.Query("select top 1 * from logs", new Dictionary<string, string>()))
            {
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch
                {
                    database.ExecuteResourceSql("Mssql-logs.sql");
                }
            }

        }

        /// <summary>
        /// Saves a log item to the database
        /// </summary>
        /// <param name="serverDaemon">The daemon triggering the event</param>
        /// <param name="target">The target of the action (region / agent UUID, etc)</param>
        /// <param name="methodCall">The method call where the problem occured</param>
        /// <param name="arguments">The arguments passed to the method</param>
        /// <param name="priority">How critical is this?</param>
        /// <param name="logMessage">The message to log</param>
        public void saveLog(string serverDaemon, string target, string methodCall, string arguments, int priority,
                            string logMessage)
        {
            try
            {
                database.insertLogRow(serverDaemon, target, methodCall, arguments, priority, logMessage);
            }
            catch
            {
                // it didn't log, don't worry about it
            }
        }

        /// <summary>
        /// Returns the name of this DB provider
        /// </summary>
        /// <returns>A string containing the DB provider name</returns>
        public string getName()
        {
            return "MSSQL Logdata Interface";
        }

        /// <summary>
        /// Closes the database provider
        /// </summary>
        public void Close()
        {
            // Do nothing.
        }

        /// <summary>
        /// Returns the version of this DB provider
        /// </summary>
        /// <returns>A string containing the provider version</returns>
        public string getVersion()
        {
            return "0.1";
        }
    }
}
