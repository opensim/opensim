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
    public class MSSQLEstateData : IEstateDataStore
    {
        private const string _migrationStore = "EstateStore";

        private static readonly ILog _Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private MSSQLManager _Database;

        private FieldInfo[] _Fields;
        private Dictionary<string, FieldInfo> _FieldMap = new Dictionary<string, FieldInfo>();

        #region Public methods

        /// <summary>
        /// Initialises the estatedata class.
        /// </summary>
        /// <param name="connectionString">connectionString.</param>
        public void Initialise(string connectionString)
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                _Database = new MSSQLManager(connectionString);
            }
            else
            {
                //TODO when can this be deleted 
                IniFile iniFile = new IniFile("mssql_connection.ini");
                string settingDataSource = iniFile.ParseFileReadValue("data_source");
                string settingInitialCatalog = iniFile.ParseFileReadValue("initial_catalog");
                string settingPersistSecurityInfo = iniFile.ParseFileReadValue("persist_security_info");
                string settingUserId = iniFile.ParseFileReadValue("user_id");
                string settingPassword = iniFile.ParseFileReadValue("password");

                _Database =
                    new MSSQLManager(settingDataSource, settingInitialCatalog, settingPersistSecurityInfo, settingUserId,
                                     settingPassword);
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
        public EstateSettings LoadEstateSettings(UUID regionID)
        {
            EstateSettings es = new EstateSettings();

            string sql = "select estate_settings." + String.Join(",estate_settings.", FieldList) + " from estate_map left join estate_settings on estate_map.EstateID = estate_settings.EstateID where estate_settings.EstateID is not null and RegionID = @RegionID";

            bool insertEstate = false;

            using (AutoClosingSqlCommand cmd = _Database.Query(sql))
            {
                cmd.Parameters.Add(_Database.CreateParameter("@RegionID", regionID));

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        foreach (string name in FieldList)
                        {
                            if (_FieldMap[name].GetValue(es) is bool)
                            {
                                int v = Convert.ToInt32(reader[name]);
                                if (v != 0)
                                    _FieldMap[name].SetValue(es, true);
                                else
                                    _FieldMap[name].SetValue(es, false);
                            }
                            else if (_FieldMap[name].GetValue(es) is UUID)
                            {
                                _FieldMap[name].SetValue(es, new UUID((Guid) reader[name])); // uuid);
                            }
                            else
                            {
                                es.EstateID = Convert.ToUInt32(reader["EstateID"].ToString());
                            }
                        }
                    }
                    else
                    {
                        insertEstate = true;
                    }
                }
            }


            if (insertEstate)
            {
                List<string> names = new List<string>(FieldList);

                names.Remove("EstateID");

                sql = string.Format("insert into estate_settings ({0}) values ( @{1})", String.Join(",", names.ToArray()), String.Join(", @", names.ToArray()));

                //_Log.Debug("[DB ESTATE]: SQL: " + sql);
                using (SqlConnection connection = _Database.DatabaseConnection())
                {
                    using (SqlCommand insertCommand = connection.CreateCommand())
                    {
                        insertCommand.CommandText = sql + " SET @ID = SCOPE_IDENTITY()";

                        foreach (string name in names)
                        {
                            insertCommand.Parameters.Add(_Database.CreateParameter("@" + name, _FieldMap[name].GetValue(es)));
                        }
                        SqlParameter idParameter = new SqlParameter("@ID", SqlDbType.Int);
                        idParameter.Direction = ParameterDirection.Output;
                        insertCommand.Parameters.Add(idParameter);

                        insertCommand.ExecuteNonQuery();

                        es.EstateID = Convert.ToUInt32(idParameter.Value);
                    }
                }

                using (AutoClosingSqlCommand cmd = _Database.Query("INSERT INTO [estate_map] ([RegionID] ,[EstateID]) VALUES (@RegionID, @EstateID)"))
                {
                    cmd.Parameters.Add(_Database.CreateParameter("@RegionID", regionID));
                    cmd.Parameters.Add(_Database.CreateParameter("@EstateID", es.EstateID));
                    // This will throw on dupe key
                    try
                    {
                       cmd.ExecuteNonQuery();
                    }
                    catch (Exception e)
                    {
                        _Log.DebugFormat("[ESTATE DB]: Error inserting regionID and EstateID in estate_map: {0}", e);
                    }
                }

                // Munge and transfer the ban list

                sql = string.Format("insert into estateban select {0}, bannedUUID, bannedIp, bannedIpHostMask, '' from regionban where regionban.regionUUID = @UUID", es.EstateID);
                using (AutoClosingSqlCommand cmd = _Database.Query(sql))
                {
                    cmd.Parameters.Add(_Database.CreateParameter("@UUID", regionID));
                    try
                    {

                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        _Log.Debug("[ESTATE DB]: Error setting up estateban from regionban");
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

            string sql = string.Format("UPDATE estate_settings SET ") ; 
            foreach (string name in names)
            {
                sql += name + " = @" + name + ", ";
            }
            sql = sql.Remove(sql.LastIndexOf(","));
            sql += " WHERE EstateID = @EstateID";

            using (AutoClosingSqlCommand cmd = _Database.Query(sql))
            {
                foreach (string name in names)
                {
                    cmd.Parameters.Add(_Database.CreateParameter("@" + name, _FieldMap[name].GetValue(es)));
                }

                cmd.Parameters.Add(_Database.CreateParameter("@EstateID", es.EstateID));
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

            using (AutoClosingSqlCommand cmd = _Database.Query(sql))
            {
                SqlParameter idParameter = new SqlParameter("@EstateID", SqlDbType.Int);
                idParameter.Value = es.EstateID;
                cmd.Parameters.Add(idParameter);

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

            using (AutoClosingSqlCommand cmd = _Database.Query(sql))
            {
                cmd.Parameters.Add(_Database.CreateParameter("@EstateID", estateID));

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
            string sql = "delete from estateban where EstateID = @EstateID";
            using (AutoClosingSqlCommand cmd = _Database.Query(sql))
            {
                cmd.Parameters.Add(_Database.CreateParameter("@EstateID", es.EstateID));
                cmd.ExecuteNonQuery();
            }

            //Insert after
            sql = "insert into estateban (EstateID, bannedUUID) values ( @EstateID, @bannedUUID )";
            using (AutoClosingSqlCommand cmd = _Database.Query(sql))
            {
                foreach (EstateBan b in es.EstateBans)
                {
                    cmd.Parameters.Add(_Database.CreateParameter("@EstateID", es.EstateID));
                    cmd.Parameters.Add(_Database.CreateParameter("@bannedUUID", b.BannedUserID));
                    cmd.ExecuteNonQuery();
                    cmd.Parameters.Clear();
                }
            }
        }

        private void SaveUUIDList(uint estateID, string table, UUID[] data)
        {
            //Delete first
            string sql = string.Format("delete from {0} where EstateID = @EstateID", table);
            using (AutoClosingSqlCommand cmd = _Database.Query(sql))
            {
                cmd.Parameters.Add(_Database.CreateParameter("@EstateID", estateID));
                cmd.ExecuteNonQuery();
            }

            sql = string.Format("insert into {0} (EstateID, uuid) values ( @EstateID, @uuid )", table);
            using (AutoClosingSqlCommand cmd = _Database.Query(sql))
            {
                cmd.Parameters.Add(_Database.CreateParameter("@EstateID", estateID));

                bool createParamOnce = true;

                foreach (UUID uuid in data)
                {
                    if (createParamOnce)
                    {
                        cmd.Parameters.Add(_Database.CreateParameter("@uuid", uuid));
                        createParamOnce = false;
                    }
                    else
                        cmd.Parameters["@uuid"].Value = uuid.Guid; //.ToString(); //TODO check if this works

                    cmd.ExecuteNonQuery();
                }
            }
        }
        #endregion
    }
}
