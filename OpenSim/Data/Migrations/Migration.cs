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

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;

namespace OpenSim.Data.Migrations
{
    /// <summary> 
    ///
    /// The Migration theory is based on the ruby on rails concept.
    /// Each database driver is going to be allowed to have files in
    /// Resources that specify the database migrations.  They will be
    /// of the form:
    ///
    ///    001_Users.sql
    ///    002_Users.sql
    ///    003_Users.sql
    ///    001_Prims.sql
    ///    002_Prims.sql
    ///    ...etc...
    ///
    /// When a database driver starts up, it specifies a resource that
    /// needs to be brought up to the current revision.  For instance:
    ///
    ///    Migration um = new Migration(DbConnection, "Users");
    ///    um.Upgrade();
    ///
    /// This works out which version Users is at, and applies all the
    /// revisions past it to it.  If there is no users table, all
    /// revisions are applied in order.  Consider each future
    /// migration to be an incremental roll forward of the tables in
    /// question.
    ///
    /// </summary>

    public class Migration
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private string _type;
        private DbConnection _conn;
        private string _subtype;
        
        private static readonly string _migrations_create = "create table migrations(name varchar(100), version int)";
        private static readonly string _migrations_init = "insert into migrations values('migrations', 1)";
        private static readonly string _migrations_find = "select version from migrations where name='migrations'";
        
        public Migration(DbConnection conn, string type)
        {
            _type = type;
            _conn = conn;

        }

        private void Initialize()
        {
            DbCommand cmd = _conn.CreateCommand();
            cmd.CommandText = _migrations_find;
            // TODO: generic way to get that text
            // if ( not found )
            cmd.CommandText = _migrations_create;
            cmd.ExecuteNonQuery();

            cmd.CommandText = _migrations_init;
            cmd.ExecuteNonQuery();
        }

        public void Update()
        {
            int version = 0;
            version = FindVersion(_type);

            List<string> migrations = GetMigrationsAfter(version);
            foreach (string m in migrations) 
            {
                // TODO: update each
            }

            // TODO: find the last revision by number and populate it back
        }

        private int FindVersion(string _type) 
        {
            int version = 0;
            DbCommand cmd = _conn.CreateCommand();
            cmd.CommandText = "select version from migrations where name='" + _type + "' limit 1";
            
            return version;
        }
        
        
        private List<string> GetAllMigrations()
        {
            return GetMigrationsAfter(0);
        }

        private List<string> GetMigrationsAfter(int version)
        {
            Assembly assem = GetType().Assembly;
            string[] names = assem.GetManifestResourceNames();
            List<string> migrations = new List<string>();

            Regex r = new Regex(@"^(\d\d\d)_" + _type + @"\.sql");

            foreach (string s in names)
            {
                m_log.Info("MIGRATION: Resources: " + s);
                if (s.EndsWith(_type + @"\.sql"))
                {
                    Match m = r.Match(s);
                    m_log.Info("MIGRATION: Match: " + m.Groups[1].ToString());
                    int MigrationVersion = int.Parse(m.Groups[1].ToString());
                    using (Stream resource = assem.GetManifestResourceStream(s))
                    {
                        using (StreamReader resourceReader = new StreamReader(resource))
                        {
                            string resourceString = resourceReader.ReadToEnd();
                            migrations.Add(resourceString);
                        }
                    }
                }
            }

            // TODO: once this is working, get rid of this
            if (migrations.Count < 1) {
                throw new Exception(string.Format("Resource '{0}' was not found", _type));
            }

            return migrations;
        }


    }

    
}