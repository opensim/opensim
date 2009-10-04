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

namespace OpenSim.Data
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
    ///    Migration um = new Migration(Assembly, DbConnection, "Users");
    ///    um.Upgrade();
    ///
    /// This works out which version Users is at, and applies all the
    /// revisions past it to it.  If there is no users table, all
    /// revisions are applied in order.  Consider each future
    /// migration to be an incremental roll forward of the tables in
    /// question.
    ///
    /// Assembly must be specifically passed in because otherwise you
    /// get the assembly that Migration.cs is part of, and what you
    /// really want is the assembly of your database class.
    ///
    /// </summary>

    public class Migration
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string _type;
        private DbConnection _conn;
        // private string _subtype;
        private Assembly _assem;
        private Regex _match;

        private static readonly string _migrations_create = "create table migrations(name varchar(100), version int)";
        // private static readonly string _migrations_init = "insert into migrations values('migrations', 1)";
        // private static readonly string _migrations_find = "select version from migrations where name='migrations'";


        public Migration(DbConnection conn, Assembly assem, string type)
        {
            _type = type;
            _conn = conn;
            _assem = assem;
            _match = new Regex(@"\.(\d\d\d)_" + _type + @"\.sql");
            Initialize();
        }

        public Migration(DbConnection conn, Assembly assem, string subtype, string type)
        {
            _type = type;
            _conn = conn;
            _assem = assem;
            _match = new Regex(subtype + @"\.(\d\d\d)_" + _type + @"\.sql");
            Initialize();
        }

        private void Initialize()
        {
            // clever, eh, we figure out which migrations version we are
            int migration_version = FindVersion(_conn, "migrations");

            if (migration_version > 0)
                return;

            // If not, create the migration tables
            DbCommand cmd = _conn.CreateCommand();
            cmd.CommandText = _migrations_create;
            cmd.ExecuteNonQuery();
            cmd.Dispose();

            InsertVersion("migrations", 1);
        }

        public void Update()
        {
            int version = 0;
            version = FindVersion(_conn, _type);

            SortedList<int, string> migrations = GetMigrationsAfter(version);
            if (migrations.Count < 1)
                return;

            // to prevent people from killing long migrations.
            m_log.InfoFormat("[MIGRATIONS] Upgrading {0} to latest revision.", _type);
            m_log.Info("[MIGRATIONS] NOTE: this may take a while, don't interupt this process!");

            using (DbCommand cmd = _conn.CreateCommand())
            {
                foreach (KeyValuePair<int, string> kvp in migrations)
                {
                    int newversion = kvp.Key;
                    cmd.CommandText = kvp.Value;
                    // we need to up the command timeout to infinite as we might be doing long migrations.
                    cmd.CommandTimeout = 0;
                    cmd.ExecuteNonQuery();

                    if (version == 0)
                    {
                        InsertVersion(_type, newversion);
                    }
                    else
                    {
                        UpdateVersion(_type, newversion);
                    }
                    version = newversion;
                }
            }
        }

        // private int MaxVersion()
        // {
        //     int max = 0;
        //     string[] names = _assem.GetManifestResourceNames();

        //     foreach (string s in names)
        //     {
        //         Match m = _match.Match(s);
        //         if (m.Success)
        //         {
        //             int MigrationVersion = int.Parse(m.Groups[1].ToString());
        //             if (MigrationVersion > max)
        //                 max = MigrationVersion;
        //         }
        //     }
        //     return max;
        // }

        public int Version
        {
            get { return FindVersion(_conn, _type); }
            set {
                if (Version < 1)
                {
                    InsertVersion(_type, value);
                }
                else
                {
                    UpdateVersion(_type, value);
                }
            }
        }

        protected virtual int FindVersion(DbConnection conn, string type)
        {
            int version = 0;

            using (DbCommand cmd = conn.CreateCommand())
            {
                try
                {
                    cmd.CommandText = "select version from migrations where name='" + type + "' order by version desc";
                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                            version = Convert.ToInt32(reader["version"]);
                    }
                }
                catch
                {
                    // Something went wrong, so we're version 0
                }
            }

            return version;
        }

        private void InsertVersion(string type, int version)
        {
            using (DbCommand cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "insert into migrations(name, version) values('" + type + "', " + version + ")";
                m_log.InfoFormat("[MIGRATIONS]: Creating {0} at version {1}", type, version);
                cmd.ExecuteNonQuery();
            }
        }

        private void UpdateVersion(string type, int version)
        {
            using (DbCommand cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "update migrations set version=" + version + " where name='" + type + "'";
                m_log.InfoFormat("[MIGRATIONS]: Updating {0} to version {1}", type, version);
                cmd.ExecuteNonQuery();
            }
        }

        // private SortedList<int, string> GetAllMigrations()
        // {
        //     return GetMigrationsAfter(0);
        // }

        private SortedList<int, string> GetMigrationsAfter(int after)
        {
            string[] names = _assem.GetManifestResourceNames();
            SortedList<int, string> migrations = new SortedList<int, string>();
            // because life is funny if we don't
            Array.Sort(names);

            foreach (string s in names)
            {
                Match m = _match.Match(s);
                if (m.Success)
                {
                    int version = int.Parse(m.Groups[1].ToString());
                    if (version > after) {
                        using (Stream resource = _assem.GetManifestResourceStream(s))
                        {
                            using (StreamReader resourceReader = new StreamReader(resource))
                            {
                                string resourceString = resourceReader.ReadToEnd();
                                migrations.Add(version, resourceString);
                            }
                        }
                    }
                }
            }

            if (migrations.Count < 1) {
                m_log.InfoFormat("[MIGRATIONS]: {0} up to date, no migrations to apply", _type);
            }
            return migrations;
        }
    }
}
