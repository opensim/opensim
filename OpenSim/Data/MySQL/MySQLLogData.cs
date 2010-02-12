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
using System.Reflection;
using log4net;
using OpenSim.Framework;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// An interface to the log database for MySQL
    /// </summary>
    internal class MySQLLogData : ILogDataPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The database manager
        /// </summary>
        public MySQLManager database;

        public void Initialise()
        {
            m_log.Info("[MySQLLogData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException (Name);
        }

        /// <summary>
        /// Artificial constructor called when the plugin is loaded
        /// Uses the obsolete mysql_connection.ini if connect string is empty.
        /// </summary>
        /// <param name="connect">connect string</param>
        public void Initialise(string connect)
        {
            if (connect != String.Empty)
            {
                database = new MySQLManager(connect);
            }
            else
            {
                m_log.Warn("Using deprecated mysql_connection.ini.  Please update database_connect in GridServer_Config.xml and we'll use that instead");

                IniFile GridDataMySqlFile = new IniFile("mysql_connection.ini");
                string settingHostname = GridDataMySqlFile.ParseFileReadValue("hostname");
                string settingDatabase = GridDataMySqlFile.ParseFileReadValue("database");
                string settingUsername = GridDataMySqlFile.ParseFileReadValue("username");
                string settingPassword = GridDataMySqlFile.ParseFileReadValue("password");
                string settingPooling = GridDataMySqlFile.ParseFileReadValue("pooling");
                string settingPort = GridDataMySqlFile.ParseFileReadValue("port");

                database = new MySQLManager(settingHostname, settingDatabase, settingUsername, settingPassword,
                                            settingPooling, settingPort);
            }

            // This actually does the roll forward assembly stuff
            Assembly assem = GetType().Assembly;

            using (MySql.Data.MySqlClient.MySqlConnection dbcon = new MySql.Data.MySqlClient.MySqlConnection(connect))
            {
                dbcon.Open();

                Migration m = new Migration(dbcon, assem, "LogStore");

                // TODO: After rev 6000, remove this.  People should have
                // been rolled onto the new migration code by then.
                TestTables(m);

                m.Update();
            }
        }

        /// <summary></summary>
        /// <param name="m"></param>
        private void TestTables(Migration m)
        {
            // under migrations, bail
            if (m.Version > 0)
                return;

            Dictionary<string, string> tableList = new Dictionary<string, string>();
            tableList["logs"] = null;
            database.GetTableVersion(tableList);

            // migrations will handle it
            if (tableList["logs"] == null)
                return;

            // we have the table, so pretend like we did the first migration in the past
            if (m.Version == 0)
                m.Version = 1;
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
            }
        }

        /// <summary>
        /// Returns the name of this DB provider
        /// </summary>
        /// <returns>A string containing the DB provider name</returns>
        public string Name
        {
            get { return "MySQL Logdata Interface";}
        }

        /// <summary>
        /// Closes the database provider
        /// </summary>
        /// <remarks>do nothing</remarks>
        public void Dispose()
        {
            // Do nothing.
        }

        /// <summary>
        /// Returns the version of this DB provider
        /// </summary>
        /// <returns>A string containing the provider version</returns>
        public string Version
        {
            get { return "0.1"; }
        }
    }
}
