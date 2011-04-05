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
using System.Data.SqlClient;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Data.MSSQL
{
    public class MSSQLEstateStore : IEstateDataStore
    {
        private const string _migrationStore = "EstateStore";

        private static readonly ILog _Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private MSSQLManager _Database;
        private string m_connectionString;
        private FieldInfo[] _Fields;
        private Dictionary<string, FieldInfo> _FieldMap = new Dictionary<string, FieldInfo>();

        #region Public methods

        public MSSQLEstateStore()
        {
        }

        public MSSQLEstateStore(string connectionString)
        {
            Initialise(connectionString);
        }

        /// <summary>
        /// Initialises the estatedata class.
        /// </summary>
        /// <param name="connectionString">connectionString.</param>
        public void Initialise(string connectionString)
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                m_connectionString = connectionString;
                _Database = new MSSQLManager(connectionString);
            }

            //Migration settings
            _Database.CheckMigration(_migrationStore);

            //Interesting way to get parameters! Maybe implement that also with other types
            Type t = typeof(EstateSettings);
            _Fields = t.GetFields(BindingFlags.NonPublic |
                                  BindingFlags.Instance |
                                  BindingFlags.DeclaredOnly);

            foreach (FieldInfo f in _Fields)
            {
                if (f.Name.Substring(0, 2) == "m_")
                    _FieldMap[f.Name.Substring(2)] = f;
            }
        }

        /// <summary>
        /// Loads the estate settings.
        /// </summary>
        /// <param name="regionID">region ID.</param>
        /// <returns></returns>
        public EstateSettings LoadEstateSettings(UUID regionID, bool create)
        {
            EstateSettings es = new EstateSettings();

            string sql = "select estate_settings." + String.Join(",estate_settings.", FieldList) + " from estate_map left join estate_settings on estate_map.EstateID = estate_settings.EstateID where estate_settings.EstateID is not null and RegionID = @RegionID";

            bool insertEstate = false;
            using (SqlConnection conn = new SqlConnection(m_connectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(_Database.CreateParameter("@RegionID", regionID));
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        foreach (string name in FieldList)
                        {
                            FieldInfo f = _FieldMap[name];
                            object v = reader[name];
                            if (f.FieldType == typeof(bool) )
                            {
                                f.SetValue(es, Convert.ToInt32(v) != 0);
                            }
                            else if (f.FieldType == typeof(UUID) )
                            {
                                f.SetValue(es, new UUID((Guid)v)); // uuid);
                            }
                            else if (f.FieldType == typeof(string)) 
                            {
                                f.SetValue(es, v.ToString());
                            }
                            else if (f.FieldType == typeof(UInt32))  
                            {
                                f.SetValue(es, Convert.ToUInt32(v));
                            }
                            else if (f.FieldType == typeof(Single))
                            {
                                f.SetValue(es, Convert.ToSingle(v));
                            }
                            else
                                f.SetValue(es, v);
                        }
                    }
                    else
                    {
                        insertEstate = true;
                    }
                }
            }


            if (insertEstate && create)
            {
                List<string> names = new List<string>(FieldList);

                names.Remove("EstateID");

                sql = string.Format("insert into estate_settings ({0}) values ( @{1})", String.Join(",", names.ToArray()), String.Join(", @", names.ToArray()));

                //_Log.Debug("[DB ESTATE]: SQL: " + sql);
                using (SqlConnection conn = new SqlConnection(m_connectionString))
                using (SqlCommand insertCommand = new SqlCommand(sql, conn))
                {
                    insertCommand.CommandText = sql + " SET @ID = SCOPE_IDENTITY()";

                    foreach (string name in names)
                    {
                        insertCommand.Parameters.Add(_Database.CreateParameter("@" + name, _FieldMap[name].GetValue(es)));
                    }
                    SqlParameter idParameter = new SqlParameter("@ID", SqlDbType.Int);
                    idParameter.Direction = ParameterDirection.Output;
                    insertCommand.Parameters.Add(idParameter);
                    conn.Open();
                    insertCommand.ExecuteNonQuery();

                    es.EstateID = Convert.ToUInt32(idParameter.Value);
                }

                sql = "INSERT INTO [estate_map] ([RegionID] ,[EstateID]) VALUES (@RegionID, @EstateID)";
                using (SqlConnection conn = new SqlConnection(m_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {

                    cmd.Parameters.Add(_Database.CreateParameter("@RegionID", regionID));
                    cmd.Parameters.Add(_Database.CreateParameter("@EstateID", es.EstateID));
                    // This will throw on dupe key
                    try
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception e)
                    {
                        _Log.DebugFormat("[ESTATE DB]: Error inserting regionID and EstateID in estate_map: {0}", e);
                    }
                }

                //TODO check if this is needed??
                es.Save();
            }

            LoadBanList(es);

            es.EstateManagers = LoadUUIDList(es.EstateID, "estate_managers");
            es.EstateAccess = LoadUUIDList(es.EstateID, "estate_users");
            es.EstateGroups = LoadUUIDList(es.EstateID, "estate_groups");

            //Set event
            es.OnSave += StoreEstateSettings;
            return es;
        }

        /// <summary>
        /// Stores the estate settings.
        /// </summary>
        /// <param name="es">estate settings</param>
        public void StoreEstateSettings(EstateSettings es)
        {
            List<string> names = new List<string>(FieldList);

            names.Remove("EstateID");

            string sql = string.Format("UPDATE estate_settings SET ");
            foreach (string name in names)
            {
                sql += name + " = @" + name + ", ";
            }
            sql = sql.Remove(sql.LastIndexOf(","));
            sql += " WHERE EstateID = @EstateID";

            using (SqlConnection conn = new SqlConnection(m_connectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                foreach (string name in names)
                {
                    cmd.Parameters.Add(_Database.CreateParameter("@" + name, _FieldMap[name].GetValue(es)));
                }

                cmd.Parameters.Add(_Database.CreateParameter("@EstateID", es.EstateID));
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            SaveBanList(es);
            SaveUUIDList(es.EstateID, "estate_managers", es.EstateManagers);
            SaveUUIDList(es.EstateID, "estate_users", es.EstateAccess);
            SaveUUIDList(es.EstateID, "estate_groups", es.EstateGroups);
        }

        #endregion

        #region Private methods

        private string[] FieldList
        {
            get { return new List<string>(_FieldMap.Keys).ToArray(); }
        }

        private void LoadBanList(EstateSettings es)
        {
            es.ClearBans();

            string sql = "select bannedUUID from estateban where EstateID = @EstateID";

            using (SqlConnection conn = new SqlConnection(m_connectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                SqlParameter idParameter = new SqlParameter("@EstateID", SqlDbType.Int);
                idParameter.Value = es.EstateID;
                cmd.Parameters.Add(idParameter);
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        EstateBan eb = new EstateBan();

                        eb.BannedUserID = new UUID((Guid)reader["bannedUUID"]); //uuid;
                        eb.BannedHostAddress = "0.0.0.0";
                        eb.BannedHostIPMask = "0.0.0.0";
                        es.AddBan(eb);
                    }
                }
            }
        }

        private UUID[] LoadUUIDList(uint estateID, string table)
        {
            List<UUID> uuids = new List<UUID>();

            string sql = string.Format("select uuid from {0} where EstateID = @EstateID", table);

            using (SqlConnection conn = new SqlConnection(m_connectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(_Database.CreateParameter("@EstateID", estateID));
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        uuids.Add(new UUID((Guid)reader["uuid"])); //uuid);
                    }
                }
            }

            return uuids.ToArray();
        }

        private void SaveBanList(EstateSettings es)
        {
            //Delete first
            using (SqlConnection conn = new SqlConnection(m_connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText  = "delete from estateban where EstateID = @EstateID";
                    cmd.Parameters.AddWithValue("@EstateID", (int)es.EstateID);
                    cmd.ExecuteNonQuery();

                    //Insert after
                    cmd.CommandText = "insert into estateban (EstateID, bannedUUID) values ( @EstateID, @bannedUUID )";
                    cmd.Parameters.AddWithValue("@bannedUUID", Guid.Empty);
                    foreach (EstateBan b in es.EstateBans)
                    {
                        cmd.Parameters["@bannedUUID"].Value = b.BannedUserID.Guid;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private void SaveUUIDList(uint estateID, string table, UUID[] data)
        {
            using (SqlConnection conn = new SqlConnection(m_connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.Parameters.AddWithValue("@EstateID", (int)estateID);
                    cmd.CommandText = string.Format("delete from {0} where EstateID = @EstateID", table);
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = string.Format("insert into {0} (EstateID, uuid) values ( @EstateID, @uuid )", table);
                    cmd.Parameters.AddWithValue("@uuid", Guid.Empty);
                    foreach (UUID uuid in data)
                    {
                        cmd.Parameters["@uuid"].Value = uuid.Guid; //.ToString(); //TODO check if this works
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public EstateSettings LoadEstateSettings(int estateID)
        {
            // TODO: Implementation!
            return new EstateSettings();
        }
        
        public List<EstateSettings> LoadEstateSettingsAll()
        {
            // TODO: Implementation!
            return new List<EstateSettings>();            
        }

        public List<int> GetEstates(string search)
        {
            // TODO: Implementation!            
            return new List<int>();
        }
        
        public List<int> GetEstatesAll()
        {
            // TODO: Implementation!            
            return new List<int>();
        }

        public List<int> GetEstatesByOwner(UUID ownerID)
        {
            return new List<int>();
        }

        public bool LinkRegion(UUID regionID, int estateID)
        {
            // TODO: Implementation!            
            return false;
        }

        public List<UUID> GetRegions(int estateID)
        {
            // TODO: Implementation!            
            return new List<UUID>();
        }

        public bool DeleteEstate(int estateID)
        {
            // TODO: Implementation!            
            return false;
        }
        #endregion
    }
}
