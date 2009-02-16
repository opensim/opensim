/*
 * Copyright (c) 2008 Intel Corporation
 * All rights reserved.
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 *
 * -- Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * -- Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * -- Neither the name of the Intel Corporation nor the names of its
 *    contributors may be used to endorse or promote products derived from
 *    this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
 * PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE INTEL OR ITS
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Xml;
using ExtensionLoader.Config;
using MySql.Data.MySqlClient;

namespace AssetServer.Extensions
{
    public static class DBConnString
    {
        private static string connectionString;

        /// <summary>
        /// Parses the MySQL connection string out of either the asset server
        /// .ini or a OpenSim-style .xml file and caches the result for future
        /// requests
        /// </summary>
        public static string GetConnectionString(IniConfigSource configFile)
        {
            if (connectionString == null)
            {
                // Try parsing from the ini file
                try
                {
                    // Load the extension list (and ordering) from our config file
                    IConfig extensionConfig = configFile.Configs["MySQL"];
                    connectionString = extensionConfig.GetString("database_connect", null);
                }
                catch (Exception) { }

                if (connectionString != null)
                {
                    // Force MySQL's broken connection pooling off
                    MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder(connectionString);
                    builder.Pooling = false;
                    if (String.IsNullOrEmpty(builder.Database))
                        Logger.Log.Error("No database selected in the connectionString: " + connectionString);
                    connectionString = builder.ToString();
                }
                else
                {
                    Logger.Log.Error("Database connection string is missing, check that the database_connect line is " +
                        "correct and uncommented in AssetServer.ini");
                }
            }

            return connectionString;
        }
    }
}
