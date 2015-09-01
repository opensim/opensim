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
using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Data;

namespace OpenSim.Data.MySQL
{
    public class MySQLEstateStore : IEstateDataStore
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_connectionString;

        private FieldInfo[] m_Fields;
        private Dictionary<string, FieldInfo> m_FieldMap =
                new Dictionary<string, FieldInfo>();

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySQLEstateStore()
        {
        }

        public MySQLEstateStore(string connectionString)
        {
            Initialise(connectionString);
        }

        public void Initialise(string connectionString)
        {
            m_connectionString = connectionString;

            try
            {
                m_log.Info("[REGION DB]: MySql - connecting: " + Util.GetDisplayConnectionString(m_connectionString));
            }
            catch (Exception e)
            {
                m_log.Debug("Exception: password not found in connection string\n" + e.ToString());
            }

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                Migration m = new Migration(dbcon, Assembly, "EstateStore");
                m.Update();

                Type t = typeof(EstateSettings);
                m_Fields = t.GetFields(BindingFlags.NonPublic |
                                       BindingFlags.Instance |
                                       BindingFlags.DeclaredOnly);

                foreach (FieldInfo f in m_Fields)
                {
                    if (f.Name.Substring(0, 2) == "m_")
                        m_FieldMap[f.Name.Substring(2)] = f;
                }
            }
        }

        private string[] FieldList
        {
            get { return new List<string>(m_FieldMap.Keys).ToArray(); }
        }

        public EstateSettings LoadEstateSettings(UUID regionID, bool create)
        {
            string sql = "select estate_settings." + String.Join(",estate_settings.", FieldList) +
                " from estate_map left join estate_settings on estate_map.EstateID = estate_settings.EstateID where estate_settings.EstateID is not null and RegionID = ?RegionID";

            using (MySqlCommand cmd = new MySqlCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("?RegionID", regionID.ToString());

                EstateSettings e = DoLoad(cmd, regionID, create);
                if (!create && e.EstateID == 0) // Not found
                    return null;

                return e;
            }
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

        private EstateSettings DoLoad(MySqlCommand cmd, UUID regionID, bool create)
        {
            EstateSettings es = new EstateSettings();
            es.OnSave += StoreEstateSettings;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                cmd.Connection = dbcon;

                bool found = false;

                using (IDataReader r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        found = true;

                        foreach (string name in FieldList)
                        {
                            if (m_FieldMap[name].FieldType == typeof(bool))
                            {
                                m_FieldMap[name].SetValue(es, Convert.ToInt32(r[name]) != 0);
                            }
                            else if (m_FieldMap[name].FieldType == typeof(UUID))
                            {
                                m_FieldMap[name].SetValue(es, DBGuid.FromDB(r[name]));
                            }
                            else
                            {
                                m_FieldMap[name].SetValue(es, r[name]);
                            }
                        }
                    }
                }

                if (!found && create)
                {
                    DoCreate(es);
                    LinkRegion(regionID, (int)es.EstateID);
                }
            }

            LoadBanList(es);
            es.EstateManagers = LoadUUIDList(es.EstateID, "estate_managers");
            es.EstateAccess = LoadUUIDList(es.EstateID, "estate_users");
            es.EstateGroups = LoadUUIDList(es.EstateID, "estate_groups");
            return es;
        }

        private void DoCreate(EstateSettings es)
        {
            // Migration case
            List<string> names = new List<string>(FieldList);

            names.Remove("EstateID");

            string sql = "insert into estate_settings (" + String.Join(",", names.ToArray()) + ") values ( ?" + String.Join(", ?", names.ToArray()) + ")";

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                using (MySqlCommand cmd2 = dbcon.CreateCommand())
                {
                    cmd2.CommandText = sql;
                    cmd2.Parameters.Clear();

                    foreach (string name in FieldList)
                    {
                        if (m_FieldMap[name].GetValue(es) is bool)
                        {
                            if ((bool)m_FieldMap[name].GetValue(es))
                                cmd2.Parameters.AddWithValue("?" + name, "1");
                            else
                                cmd2.Parameters.AddWithValue("?" + name, "0");
                        }
                        else
                        {
                            cmd2.Parameters.AddWithValue("?" + name, m_FieldMap[name].GetValue(es).ToString());
                        }
                    }

                    cmd2.ExecuteNonQuery();

                    cmd2.CommandText = "select LAST_INSERT_ID() as id";
                    cmd2.Parameters.Clear();

                    using (IDataReader r = cmd2.ExecuteReader())
                    {
                        r.Read();
                        es.EstateID = Convert.ToUInt32(r["id"]);
                    }

                    es.Save();
                }
            }
        }

        public void StoreEstateSettings(EstateSettings es)
        {
            string sql = "replace into estate_settings (" + String.Join(",", FieldList) + ") values ( ?" + String.Join(", ?", FieldList) + ")";

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = sql;

                    foreach (string name in FieldList)
                    {
                        if (m_FieldMap[name].GetValue(es) is bool)
                        {
                            if ((bool)m_FieldMap[name].GetValue(es))
                                cmd.Parameters.AddWithValue("?" + name, "1");
                            else
                                cmd.Parameters.AddWithValue("?" + name, "0");
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("?" + name, m_FieldMap[name].GetValue(es).ToString());
                        }
                    }

                    cmd.ExecuteNonQuery();
                }
            }

            SaveBanList(es);
            SaveUUIDList(es.EstateID, "estate_managers", es.EstateManagers);
            SaveUUIDList(es.EstateID, "estate_users", es.EstateAccess);
            SaveUUIDList(es.EstateID, "estate_groups", es.EstateGroups);
        }

        private void LoadBanList(EstateSettings es)
        {
            es.ClearBans();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = "select bannedUUID from estateban where EstateID = ?EstateID";
                    cmd.Parameters.AddWithValue("?EstateID", es.EstateID);

                    using (IDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            EstateBan eb = new EstateBan();

                            UUID uuid = new UUID();
                            UUID.TryParse(r["bannedUUID"].ToString(), out uuid);

                            eb.BannedUserID = uuid;
                            eb.BannedHostAddress = "0.0.0.0";
                            eb.BannedHostIPMask = "0.0.0.0";
                            es.AddBan(eb);
                        }
                    }
                }
            }
        }

        private void SaveBanList(EstateSettings es)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = "delete from estateban where EstateID = ?EstateID";
                    cmd.Parameters.AddWithValue("?EstateID", es.EstateID.ToString());

                    cmd.ExecuteNonQuery();

                    cmd.Parameters.Clear();

                    cmd.CommandText = "insert into estateban (EstateID, bannedUUID, bannedIp, bannedIpHostMask, bannedNameMask) values ( ?EstateID, ?bannedUUID, '', '', '' )";

                    foreach (EstateBan b in es.EstateBans)
                    {
                        cmd.Parameters.AddWithValue("?EstateID", es.EstateID.ToString());
                        cmd.Parameters.AddWithValue("?bannedUUID", b.BannedUserID.ToString());

                        cmd.ExecuteNonQuery();
                        cmd.Parameters.Clear();
                    }
                }
            }
        }

        void SaveUUIDList(uint EstateID, string table, UUID[] data)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = "delete from " + table + " where EstateID = ?EstateID";
                    cmd.Parameters.AddWithValue("?EstateID", EstateID.ToString());

                    cmd.ExecuteNonQuery();

                    cmd.Parameters.Clear();

                    cmd.CommandText = "insert into " + table + " (EstateID, uuid) values ( ?EstateID, ?uuid )";

                    foreach (UUID uuid in data)
                    {
                        cmd.Parameters.AddWithValue("?EstateID", EstateID.ToString());
                        cmd.Parameters.AddWithValue("?uuid", uuid.ToString());

                        cmd.ExecuteNonQuery();
                        cmd.Parameters.Clear();
                    }
                }
            }
        }

        UUID[] LoadUUIDList(uint EstateID, string table)
        {
            List<UUID> uuids = new List<UUID>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = "select uuid from " + table + " where EstateID = ?EstateID";
                    cmd.Parameters.AddWithValue("?EstateID", EstateID);

                    using (IDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            // EstateBan eb = new EstateBan();
                            uuids.Add(DBGuid.FromDB(r["uuid"]));
                        }
                    }
                }
            }

            return uuids.ToArray();
        }

        public EstateSettings LoadEstateSettings(int estateID)
        {
            using (MySqlCommand cmd = new MySqlCommand())
            {
                string sql = "select estate_settings." + String.Join(",estate_settings.", FieldList) + " from estate_settings where EstateID = ?EstateID";

                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("?EstateID", estateID);

                EstateSettings e =  DoLoad(cmd, UUID.Zero, false);
                if (e.EstateID != estateID)
                    return null;
                return e;
            }
        }
        
        public List<EstateSettings> LoadEstateSettingsAll()
        {
            List<EstateSettings> allEstateSettings = new List<EstateSettings>();            
            
            List<int> allEstateIds = GetEstatesAll();
            
            foreach (int estateId in allEstateIds)
                allEstateSettings.Add(LoadEstateSettings(estateId));
            
            return allEstateSettings;
        }
        
        public List<int> GetEstatesAll()
        {
            List<int> result = new List<int>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = "select estateID from estate_settings";

                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(Convert.ToInt32(reader["EstateID"]));
                        }
                        reader.Close();
                    }
                }

                dbcon.Close();
            }

            return result;            
        }

        public List<int> GetEstates(string search)
        {
            List<int> result = new List<int>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = "select estateID from estate_settings where EstateName = ?EstateName";
                    cmd.Parameters.AddWithValue("?EstateName", search);

                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(Convert.ToInt32(reader["EstateID"]));
                        }
                        reader.Close();
                    }
                }

                dbcon.Close();
            }

            return result;
        }

        public List<int> GetEstatesByOwner(UUID ownerID)
        {
            List<int> result = new List<int>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = "select estateID from estate_settings where EstateOwner = ?EstateOwner";
                    cmd.Parameters.AddWithValue("?EstateOwner", ownerID);

                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(Convert.ToInt32(reader["EstateID"]));
                        }
                        reader.Close();
                    }
                }

                dbcon.Close();
            }

            return result;
        }

        public bool LinkRegion(UUID regionID, int estateID)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                MySqlTransaction transaction = dbcon.BeginTransaction();

                try
                {
                    // Delete any existing association of this region with an estate.
                     using (MySqlCommand cmd = dbcon.CreateCommand())
                     {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "delete from estate_map where RegionID = ?RegionID";
                        cmd.Parameters.AddWithValue("?RegionID", regionID);

                        cmd.ExecuteNonQuery();
                    }

                    using (MySqlCommand cmd = dbcon.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "insert into estate_map values (?RegionID, ?EstateID)";
                        cmd.Parameters.AddWithValue("?RegionID", regionID);
                        cmd.Parameters.AddWithValue("?EstateID", estateID);

                        int ret = cmd.ExecuteNonQuery();

                        if (ret != 0)
                            transaction.Commit();
                        else
                            transaction.Rollback();

                        dbcon.Close();

                        return (ret != 0);
                    }
                }
                catch (MySqlException ex)
                {
                    m_log.Error("[REGION DB]: LinkRegion failed: " + ex.Message);
                    transaction.Rollback();
                }

                dbcon.Close();
            }

            return false;
        }

        public List<UUID> GetRegions(int estateID)
        {
            List<UUID> result = new List<UUID>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                try
                {
                    using (MySqlCommand cmd = dbcon.CreateCommand())
                    {
                        cmd.CommandText = "select RegionID from estate_map where EstateID = ?EstateID";
                        cmd.Parameters.AddWithValue("?EstateID", estateID.ToString());

                        using (IDataReader reader = cmd.ExecuteReader())
                        {
                            while(reader.Read())
                                result.Add(DBGuid.FromDB(reader["RegionID"]));
                            reader.Close();
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[REGION DB]: Error reading estate map. " + e.ToString());
                    return result;
                }
                dbcon.Close();
            }

            return result;
        }

        public bool DeleteEstate(int estateID)
        {
            return false;
        }
    }
}
