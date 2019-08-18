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
#if CSharpSqlite
    using Community.CsharpSqlite.Sqlite;
#else
    using Mono.Data.Sqlite;
#endif
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Data.SQLite
{
    public class SQLiteEstateStore : IEstateDataStore
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private SqliteConnection m_connection;
        private string m_connectionString;

        private FieldInfo[] m_Fields;
        private Dictionary<string, FieldInfo> m_FieldMap =
                new Dictionary<string, FieldInfo>();

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public SQLiteEstateStore()
        {
        }

        public SQLiteEstateStore(string connectionString)
        {
            Initialise(connectionString);
        }

        public void Initialise(string connectionString)
        {
            if (Util.IsWindows())
                Util.LoadArchSpecificWindowsDll("sqlite3.dll");

            m_connectionString = connectionString;

            m_log.Info("[ESTATE DB]: Sqlite - connecting: "+m_connectionString);

            m_connection = new SqliteConnection(m_connectionString);
            m_connection.Open();

            Migration m = new Migration(m_connection, Assembly, "EstateStore");
            m.Update();

            //m_connection.Close();
           // m_connection.Open();

            Type t = typeof(EstateSettings);
            m_Fields = t.GetFields(BindingFlags.NonPublic |
                                   BindingFlags.Instance |
                                   BindingFlags.DeclaredOnly);

            foreach (FieldInfo f in m_Fields)
                if (f.Name.Substring(0, 2) == "m_")
                    m_FieldMap[f.Name.Substring(2)] = f;
        }

        private string[] FieldList
        {
            get { return new List<string>(m_FieldMap.Keys).ToArray(); }
        }

        public EstateSettings LoadEstateSettings(UUID regionID, bool create)
        {
            string sql = "select estate_settings."+String.Join(",estate_settings.", FieldList)+" from estate_map left join estate_settings on estate_map.EstateID = estate_settings.EstateID where estate_settings.EstateID is not null and RegionID = :RegionID";

            using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue(":RegionID", regionID.ToString());

                return DoLoad(cmd, regionID, create);
            }
        }

        private EstateSettings DoLoad(SqliteCommand cmd, UUID regionID, bool create)
        {
            EstateSettings es = new EstateSettings();
            es.OnSave += StoreEstateSettings;
            IDataReader r = null;
            try
            {
                 r = cmd.ExecuteReader();
            }
            catch (SqliteException)
            {
                m_log.Error("[SQLITE]: There was an issue loading the estate settings.  This can happen the first time running OpenSimulator with CSharpSqlite the first time.  OpenSimulator will probably crash, restart it and it should be good to go.");
            }

            if (r != null && r.Read())
            {
                foreach (string name in FieldList)
                {
                    if (m_FieldMap[name].GetValue(es) is bool)
                    {
                        int v = Convert.ToInt32(r[name]);
                        if (v != 0)
                            m_FieldMap[name].SetValue(es, true);
                        else
                            m_FieldMap[name].SetValue(es, false);
                    }
                    else if (m_FieldMap[name].GetValue(es) is UUID)
                    {
                        UUID uuid = UUID.Zero;

                        UUID.TryParse(r[name].ToString(), out uuid);
                        m_FieldMap[name].SetValue(es, uuid);
                    }
                    else
                    {
                        m_FieldMap[name].SetValue(es, Convert.ChangeType(r[name], m_FieldMap[name].FieldType));
                    }
                }
                r.Close();
            }
            else if (create)
            {
                DoCreate(es);
                LinkRegion(regionID, (int)es.EstateID);
            }

            LoadBanList(es);

            es.EstateManagers = LoadUUIDList(es.EstateID, "estate_managers");
            es.EstateAccess = LoadUUIDList(es.EstateID, "estate_users");
            es.EstateGroups = LoadUUIDList(es.EstateID, "estate_groups");
            return es;
        }

        public EstateSettings CreateNewEstate()
        {
            EstateSettings es = new EstateSettings();
            es.OnSave += StoreEstateSettings;

            DoCreate(es);

            LoadBanList(es);

            es.EstateManagers = LoadUUIDList(es.EstateID, "estate_managers");
            es.EstateAccess = LoadUUIDList(es.EstateID, "estate_users");
            es.EstateGroups = LoadUUIDList(es.EstateID, "estate_groups");

            return es;
        }

        private void DoCreate(EstateSettings es)
        {
            List<string> names = new List<string>(FieldList);

            IDataReader r = null;

            using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
            {
                names.Remove("EstateID");

                string sql = "insert into estate_settings ("+String.Join(",", names.ToArray())+") values ( :"+String.Join(", :", names.ToArray())+")";

                cmd.CommandText = sql;
                cmd.Parameters.Clear();

                foreach (string name in FieldList)
                {
                    if (m_FieldMap[name].GetValue(es) is bool)
                    {
                        if ((bool)m_FieldMap[name].GetValue(es))
                            cmd.Parameters.AddWithValue(":"+name, "1");
                        else
                            cmd.Parameters.AddWithValue(":"+name, "0");
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue(":"+name, m_FieldMap[name].GetValue(es).ToString());
                    }
                }

                cmd.ExecuteNonQuery();

                cmd.CommandText = "select LAST_INSERT_ROWID() as id";
                cmd.Parameters.Clear();

                r = cmd.ExecuteReader();
            }

            r.Read();

            es.EstateID = Convert.ToUInt32(r["id"]);

            r.Close();

            es.Save();
        }

        public void StoreEstateSettings(EstateSettings es)
        {
            List<string> fields = new List<string>(FieldList);
            fields.Remove("EstateID");

            List<string> terms = new List<string>();

            foreach (string f in fields)
                terms.Add(f+" = :"+f);

            string sql = "update estate_settings set "+String.Join(", ", terms.ToArray())+" where EstateID = :EstateID";

            using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
            {
                cmd.CommandText = sql;

                foreach (string name in FieldList)
                {
                    if (m_FieldMap[name].GetValue(es) is bool)
                    {
                        if ((bool)m_FieldMap[name].GetValue(es))
                            cmd.Parameters.AddWithValue(":"+name, "1");
                        else
                            cmd.Parameters.AddWithValue(":"+name, "0");
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue(":"+name, m_FieldMap[name].GetValue(es).ToString());
                    }
                }

                cmd.ExecuteNonQuery();
            }

            SaveBanList(es);
            SaveUUIDList(es.EstateID, "estate_managers", es.EstateManagers);
            SaveUUIDList(es.EstateID, "estate_users", es.EstateAccess);
            SaveUUIDList(es.EstateID, "estate_groups", es.EstateGroups);
        }

        private void LoadBanList(EstateSettings es)
        {
            es.ClearBans();

            IDataReader r;

            using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
            {
                cmd.CommandText = "select * from estateban where EstateID = :EstateID";
                cmd.Parameters.AddWithValue(":EstateID", es.EstateID);

                r = cmd.ExecuteReader();
            }

            while (r.Read())
            {
                EstateBan eb = new EstateBan();

                eb.BannedUserID = DBGuid.FromDB(r["bannedUUID"]); ;
                eb.BannedHostAddress = "0.0.0.0";
                eb.BannedHostIPMask = "0.0.0.0";
                eb.BanningUserID = DBGuid.FromDB(r["banningUUID"]);
                eb.BanTime = Convert.ToInt32(r["banTime"]);
                es.AddBan(eb);
            }
            r.Close();
        }

        private void SaveBanList(EstateSettings es)
        {
            using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
            {
                cmd.CommandText = "delete from estateban where EstateID = :EstateID";
                cmd.Parameters.AddWithValue(":EstateID", es.EstateID.ToString());

                cmd.ExecuteNonQuery();

                cmd.Parameters.Clear();

                cmd.CommandText = "insert into estateban (EstateID, bannedUUID, bannedIp, bannedIpHostMask, bannedNameMask, banningUUID, banTime) values ( :EstateID, :bannedUUID, '', '', '', :banningUUID, :banTime )";

                foreach (EstateBan b in es.EstateBans)
                {
                    cmd.Parameters.AddWithValue(":EstateID", es.EstateID.ToString());
                    cmd.Parameters.AddWithValue(":bannedUUID", b.BannedUserID.ToString());
                    cmd.Parameters.AddWithValue(":banningUUID", b.BanningUserID.ToString());
                    cmd.Parameters.AddWithValue(":banTime", b.BanTime);

                    cmd.ExecuteNonQuery();
                    cmd.Parameters.Clear();
                }
            }
        }

        void SaveUUIDList(uint EstateID, string table, UUID[] data)
        {
            using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
            {
                cmd.CommandText = "delete from "+table+" where EstateID = :EstateID";
                cmd.Parameters.AddWithValue(":EstateID", EstateID.ToString());

                cmd.ExecuteNonQuery();

                cmd.Parameters.Clear();

                cmd.CommandText = "insert into "+table+" (EstateID, uuid) values ( :EstateID, :uuid )";

                foreach (UUID uuid in data)
                {
                    cmd.Parameters.AddWithValue(":EstateID", EstateID.ToString());
                    cmd.Parameters.AddWithValue(":uuid", uuid.ToString());

                    cmd.ExecuteNonQuery();
                    cmd.Parameters.Clear();
                }
            }
        }

        UUID[] LoadUUIDList(uint EstateID, string table)
        {
            List<UUID> uuids = new List<UUID>();
            IDataReader r;

            using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
            {
                cmd.CommandText = "select uuid from "+table+" where EstateID = :EstateID";
                cmd.Parameters.AddWithValue(":EstateID", EstateID);

                r = cmd.ExecuteReader();
            }

            while (r.Read())
            {
                // EstateBan eb = new EstateBan();

                UUID uuid = new UUID();
                UUID.TryParse(r["uuid"].ToString(), out uuid);

                uuids.Add(uuid);
            }
            r.Close();

            return uuids.ToArray();
        }

        public EstateSettings LoadEstateSettings(int estateID)
        {
            string sql = "select estate_settings."+String.Join(",estate_settings.", FieldList)+" from estate_settings where estate_settings.EstateID = :EstateID";

            using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue(":EstateID", estateID.ToString());

                return DoLoad(cmd, UUID.Zero, false);
            }
        }

        public List<EstateSettings> LoadEstateSettingsAll()
        {
            List<EstateSettings> estateSettings = new List<EstateSettings>();

            List<int> estateIds = GetEstatesAll();
            foreach (int estateId in estateIds)
                estateSettings.Add(LoadEstateSettings(estateId));

            return estateSettings;
        }

        public List<int> GetEstates(string search)
        {
            List<int> result = new List<int>();

            string sql = "select EstateID from estate_settings where estate_settings.EstateName = :EstateName";
            IDataReader r;

            using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue(":EstateName", search);

                r = cmd.ExecuteReader();
            }

            while (r.Read())
            {
                result.Add(Convert.ToInt32(r["EstateID"]));
            }
            r.Close();

            return result;
        }

        public List<int> GetEstatesAll()
        {
            List<int> result = new List<int>();

            string sql = "select EstateID from estate_settings";
            IDataReader r;

            using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
            {
                cmd.CommandText = sql;

                r = cmd.ExecuteReader();
            }

            while (r.Read())
            {
                result.Add(Convert.ToInt32(r["EstateID"]));
            }
            r.Close();

            return result;
        }

        public List<int> GetEstatesByOwner(UUID ownerID)
        {
            List<int> result = new List<int>();

            string sql = "select EstateID from estate_settings where estate_settings.EstateOwner = :EstateOwner";
            IDataReader r;

            using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue(":EstateOwner", ownerID);

                r = cmd.ExecuteReader();
            }

            while (r.Read())
            {
                result.Add(Convert.ToInt32(r["EstateID"]));
            }
            r.Close();

            return result;
        }

        public bool LinkRegion(UUID regionID, int estateID)
        {
            SqliteTransaction transaction = m_connection.BeginTransaction();

            // Delete any existing estate mapping for this region.
            using(SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
            {
                cmd.CommandText = "delete from estate_map where RegionID = :RegionID";
                cmd.Transaction = transaction;
                cmd.Parameters.AddWithValue(":RegionID", regionID.ToString());

                cmd.ExecuteNonQuery();
            }

            using(SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
            {
                cmd.CommandText = "insert into estate_map values (:RegionID, :EstateID)";
                cmd.Transaction = transaction;
                cmd.Parameters.AddWithValue(":RegionID", regionID.ToString());
                cmd.Parameters.AddWithValue(":EstateID", estateID.ToString());

                if (cmd.ExecuteNonQuery() == 0)
                {
                    transaction.Rollback();
                    return false;
                }
                else
                {
                    transaction.Commit();
                    return true;
                }
            }
        }

        public List<UUID> GetRegions(int estateID)
        {
            return new List<UUID>();
        }

        public bool DeleteEstate(int estateID)
        {
            return false;
        }
    }
}
