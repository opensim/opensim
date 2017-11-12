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
using System.Reflection;
using System.Collections.Generic;
using System.Data;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using log4net;
using OpenMetaverse;
using Npgsql;
using NpgsqlTypes;

namespace OpenSim.Data.PGSQL
{
    public class PGSQLFSAssetData : IFSAssetDataPlugin
    {
        private const string _migrationStore = "FSAssetStore";
        private static string m_Table = "fsassets";
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private long m_ticksToEpoch;

        private PGSQLManager m_database;
        private string m_connectionString;

        public PGSQLFSAssetData()
        {
        }

        public void Initialise(string connect, string realm, int UpdateAccessTime)
        {
            DaysBetweenAccessTimeUpdates = UpdateAccessTime;

            m_ticksToEpoch = new System.DateTime(1970, 1, 1).Ticks;

            m_connectionString = connect;
            m_database = new PGSQLManager(m_connectionString);

            //New migration to check for DB changes
            m_database.CheckMigration(_migrationStore);
        }

        public void Initialise()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Number of days that must pass before we update the access time on an asset when it has been fetched
        /// Config option to change this is "DaysBetweenAccessTimeUpdates"
        /// </summary>
        private int DaysBetweenAccessTimeUpdates = 0;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }
        
        #region IPlugin Members

        public string Version { get { return "1.0.0.0"; } }

        public void Dispose() { }

        public string Name
        {
            get { return "PGSQL FSAsset storage engine"; }
        }

        #endregion

        #region IFSAssetDataPlugin Members

        public AssetMetadata Get(string id, out string hash)
        {
            hash = String.Empty;
            AssetMetadata meta = null;
            UUID uuid = new UUID(id);

            string query = String.Format("select \"id\", \"type\", \"hash\", \"create_time\", \"access_time\", \"asset_flags\" from {0} where \"id\" = :id", m_Table);
            using (NpgsqlConnection dbcon = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
            {
                dbcon.Open();
                cmd.Parameters.Add(m_database.CreateParameter("id", uuid));
                using (NpgsqlDataReader reader = cmd.ExecuteReader(CommandBehavior.Default))
                {
                    if (reader.Read())
                    {
                        meta = new AssetMetadata();
                        hash = reader["hash"].ToString();
                        meta.ID = id;
                        meta.FullID = uuid;
                        meta.Name = String.Empty;
                        meta.Description = String.Empty;
                        meta.Type = (sbyte)Convert.ToInt32(reader["type"]);
                        meta.ContentType = SLUtil.SLAssetTypeToContentType(meta.Type);
                        meta.CreationDate = Util.ToDateTime(Convert.ToInt32(reader["create_time"]));
                        meta.Flags = (AssetFlags)Convert.ToInt32(reader["asset_flags"]);
                        int atime = Convert.ToInt32(reader["access_time"]);
                        UpdateAccessTime(atime, uuid);
                    }
                }
            }

            return meta;
        }

        private void UpdateAccessTime(int AccessTime, UUID id)
        {
            // Reduce DB work by only updating access time if asset hasn't recently been accessed
            // 0 By Default, Config option is "DaysBetweenAccessTimeUpdates"
            if (DaysBetweenAccessTimeUpdates > 0 && (DateTime.UtcNow - Utils.UnixTimeToDateTime(AccessTime)).TotalDays < DaysBetweenAccessTimeUpdates)
                return;

            string query = String.Format("UPDATE {0} SET \"access_time\" = :access_time WHERE \"id\" = :id", m_Table);
            using (NpgsqlConnection dbcon = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
            {
                dbcon.Open();
                int now = (int)((System.DateTime.Now.Ticks - m_ticksToEpoch) / 10000000);
                cmd.Parameters.Add(m_database.CreateParameter("id", id));
                cmd.Parameters.Add(m_database.CreateParameter("access_time", now));
                cmd.ExecuteNonQuery();
            }
        }

        public bool Store(AssetMetadata meta, string hash)
        {
            try
            {
                bool found = false;
                string oldhash;
                AssetMetadata existingAsset = Get(meta.ID, out oldhash);

                string query = String.Format("UPDATE {0} SET \"access_time\" = :access_time WHERE \"id\" = :id", m_Table);
                if (existingAsset == null)
                {
                   query = String.Format("insert into {0} (\"id\", \"type\", \"hash\", \"asset_flags\", \"create_time\", \"access_time\") values ( :id, :type, :hash, :asset_flags, :create_time, :access_time)", m_Table);
                   found = true;
                }

                using (NpgsqlConnection dbcon = new NpgsqlConnection(m_connectionString))
                using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                {
                    dbcon.Open();
                    int now = (int)((System.DateTime.Now.Ticks - m_ticksToEpoch) / 10000000);
                    cmd.Parameters.Add(m_database.CreateParameter("id", meta.FullID));
                    cmd.Parameters.Add(m_database.CreateParameter("type", meta.Type));
                    cmd.Parameters.Add(m_database.CreateParameter("hash", hash));
                    cmd.Parameters.Add(m_database.CreateParameter("asset_flags", Convert.ToInt32(meta.Flags)));
                    cmd.Parameters.Add(m_database.CreateParameter("create_time", now));
                    cmd.Parameters.Add(m_database.CreateParameter("access_time", now));
                    cmd.ExecuteNonQuery();
                }
                return found;
            }
            catch(Exception e)
            {
                m_log.Error("[PGSQL FSASSETS] Failed to store asset with ID " + meta.ID);
		        m_log.Error(e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Check if the assets exist in the database.
        /// </summary>
        /// <param name="uuids">The asset UUID's</param>
        /// <returns>For each asset: true if it exists, false otherwise</returns>
        public bool[] AssetsExist(UUID[] uuids)
        {
            if (uuids.Length == 0)
                return new bool[0];

            HashSet<UUID> exists = new HashSet<UUID>();

            string ids = "'" + string.Join("','", uuids) + "'";
            string query = string.Format("select \"id\" from {1} where id in ({0})", ids, m_Table);
            using (NpgsqlConnection dbcon = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
            {
                dbcon.Open();
                using (NpgsqlDataReader reader = cmd.ExecuteReader(CommandBehavior.Default))
                {
                    while (reader.Read())
                    {
                        UUID id = DBGuid.FromDB(reader["id"]);;
                        exists.Add(id);
                    }
                }
            }

            bool[] results = new bool[uuids.Length];
            for (int i = 0; i < uuids.Length; i++)
                results[i] = exists.Contains(uuids[i]);
            return results;
        }

        public int Count()
        {
            int count = 0;
            string query = String.Format("select count(*) as count from {0}", m_Table);
            using (NpgsqlConnection dbcon = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
            {
                dbcon.Open();
                IDataReader reader = cmd.ExecuteReader();
                reader.Read();
                count = Convert.ToInt32(reader["count"]);
                reader.Close();
            }

            return count;
        }

        public bool Delete(string id)
        {
            string query = String.Format("delete from {0} where \"id\" = :id", m_Table);
            using (NpgsqlConnection dbcon = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
            {
                dbcon.Open();
                cmd.Parameters.Add(m_database.CreateParameter("id", new UUID(id)));
                cmd.ExecuteNonQuery();
            }

            return true;
        }

        public void Import(string conn, string table, int start, int count, bool force, FSStoreDelegate store)
        {
            int imported = 0;
            string limit = String.Empty;
            if(count != -1)
            {
                limit = String.Format(" limit {0} offset {1}", start, count);
            }
            string query = String.Format("select * from {0}{1}", table, limit);
            try
            {
                using (NpgsqlConnection remote = new NpgsqlConnection(conn))
                using (NpgsqlCommand cmd = new NpgsqlCommand(query, remote))
                {
                    remote.Open();
                    MainConsole.Instance.Output("Querying database");
                    MainConsole.Instance.Output("Reading data");
                    using (NpgsqlDataReader reader = cmd.ExecuteReader(CommandBehavior.Default))
                    {
                        while (reader.Read())
                        {
                            if ((imported % 100) == 0)
                            {
                                MainConsole.Instance.Output(String.Format("{0} assets imported so far", imported));
                            }
    
                            AssetBase asset = new AssetBase();
                            AssetMetadata meta = new AssetMetadata();

                            meta.ID = reader["id"].ToString();
                            meta.FullID = new UUID(meta.ID);

                            meta.Name = String.Empty;
                            meta.Description = String.Empty;
                            meta.Type = (sbyte)Convert.ToInt32(reader["assetType"]);
                            meta.ContentType = SLUtil.SLAssetTypeToContentType(meta.Type);
                            meta.CreationDate = Util.ToDateTime(Convert.ToInt32(reader["create_time"]));

                            asset.Metadata = meta;
                            asset.Data = (byte[])reader["data"];

                            store(asset, force);

                            imported++;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PGSQL FSASSETS]: Error importing assets: {0}",
                        e.Message.ToString());
                return;
            }

            MainConsole.Instance.Output(String.Format("Import done, {0} assets imported", imported));
        }

        #endregion
    }
}
