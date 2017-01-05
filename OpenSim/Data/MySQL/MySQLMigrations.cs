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
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;
using MySql.Data.MySqlClient;

namespace OpenSim.Data.MySQL
{
    /// <summary>This is a MySQL-customized migration processor.  The only difference is in how
    /// it executes SQL scripts (using MySqlScript instead of MyCommand)
    ///
    /// </summary>
    public class MySqlMigration : Migration
    {
        public MySqlMigration()
            : base()
        {
        }

        public MySqlMigration(DbConnection conn, Assembly assem, string subtype, string type) :
            base(conn, assem, subtype, type)
        {
        }

        public MySqlMigration(DbConnection conn, Assembly assem, string type) :
            base(conn, assem, type)
        {
        }

        protected override void ExecuteScript(DbConnection conn, string[] script)
        {
            if (!(conn is MySqlConnection))
            {
                base.ExecuteScript(conn, script);
                return;
            }

            MySqlScript scr = new MySqlScript((MySqlConnection)conn);
            {
                foreach (string sql in script)
                {
                    scr.Query = sql;
                    scr.Error += delegate(object sender, MySqlScriptErrorEventArgs args)
                    {
                        throw new Exception(sql);
                    };
                    scr.Execute();
                }
            }
        }
    }
}
