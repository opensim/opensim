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
using System.Data;
using System.Reflection;
using System.Collections.Generic;
using OpenMetaverse;
using log4net;
using OpenSim.Framework;
using Npgsql;
using NpgsqlTypes;

namespace OpenSim.Data.PGSQL
{
    /// <summary>
    /// A PGSQL Interface for the Asset server
    /// </summary>
    public class PGSQLAssetData : AssetDataBase
    {
        private const string _migrationStore = "AssetStore";

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private long m_ticksToEpoch;
        /// <summary>
        /// Database manager
        /// </summary>
        private PGSQLManager m_database;
        private string m_connectionString;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        #region IPlugin Members

        override public void Dispose() { }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        // [Obsolete("Cannot be default-initialized!")]
        override public void Initialise()
        {
            m_log.Info("[PGSQLAssetData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException(Name);
        }

        /// <summary>
        /// Initialises asset interface
        /// </summary>
        /// <para>
        /// a string instead of file, if someone writes the support
        /// </para>
        /// <param name="connectionString">connect string</param>
        override public void Initialise(string connectionString)
        {
            m_ticksToEpoch = new System.DateTime(1970, 1, 1).Ticks;

            m_database = new PGSQLManager(connectionString);
            m_connectionString = connectionString;

            //New migration to check for DB changes
            m_database.CheckMigration(_migrationStore);
        }

        /// <summary>
        /// Database provider version.
        /// </summary>
        override public string Version
        {
            get { return m_database.getVersion(); }
        }

        /// <summary>
        /// The name of this DB provider.
        /// </summary>
        override public string Name
        {
            get { return "PGSQL Asset storage engine"; }
        }

        #endregion

        #region IAssetDataPlugin Members

        /// <summary>
        /// Fetch Asset from m_database
        /// </summary>
        /// <param name="assetID">the asset UUID</param>
        /// <returns></returns>
        override public AssetBase GetAsset(UUID assetID)
        {
            string sql = "SELECT * FROM assets WHERE id = :id";
            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("id", assetID));
                conn.Open();
                using (NpgsqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        AssetBase asset = new AssetBase(
                            DBGuid.FromDB(reader["id"]),
                            (string)reader["name"],
                            Convert.ToSByte(reader["assetType"]),
                            reader["creatorid"].ToString()
                        );
                        // Region Main
                        asset.Description = (string)reader["description"];
                        asset.Local = Convert.ToBoolean(reader["local"]);
                        asset.Temporary = Convert.ToBoolean(reader["temporary"]);
                        asset.Flags = (AssetFlags)(Convert.ToInt32(reader["asset_flags"]));
                        asset.Data = (byte[])reader["data"];
                        return asset;
                    }
                    return null; // throw new Exception("No rows to return");
                }
            }
        }

        /// <summary>
        /// Create asset in m_database
        /// </summary>
        /// <param name="asset">the asset</param>
        override public bool StoreAsset(AssetBase asset)
        {
           
            string sql =
                @"UPDATE assets set name = :name, description = :description, " + "\"assetType\" " + @" = :assetType,
                         local = :local, temporary = :temporary, creatorid = :creatorid, data = :data
                    WHERE id=:id;

                  INSERT INTO assets
                    (id, name, description, " + "\"assetType\" " + @", local, 
                     temporary, create_time, access_time, creatorid, asset_flags, data)
                  Select :id, :name, :description, :assetType, :local, 
                         :temporary, :create_time, :access_time, :creatorid, :asset_flags, :data
                   Where not EXISTS(SELECT * FROM assets WHERE id=:id) 
                ";
            
            string assetName = asset.Name;
            if (asset.Name.Length > AssetBase.MAX_ASSET_NAME)
            {
                assetName = asset.Name.Substring(0, AssetBase.MAX_ASSET_NAME);
                m_log.WarnFormat(
                    "[ASSET DB]: Name '{0}' for asset {1} truncated from {2} to {3} characters on add", 
                    asset.Name, asset.ID, asset.Name.Length, assetName.Length);
            }
            
            string assetDescription = asset.Description;
            if (asset.Description.Length > AssetBase.MAX_ASSET_DESC)
            {
                assetDescription = asset.Description.Substring(0, AssetBase.MAX_ASSET_DESC);
                m_log.WarnFormat(
                    "[ASSET DB]: Description '{0}' for asset {1} truncated from {2} to {3} characters on add", 
                    asset.Description, asset.ID, asset.Description.Length, assetDescription.Length);
            }

            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand command = new NpgsqlCommand(sql, conn))
            {
                int now = (int)((System.DateTime.Now.Ticks - m_ticksToEpoch) / 10000000);
                command.Parameters.Add(m_database.CreateParameter("id", asset.FullID));
                command.Parameters.Add(m_database.CreateParameter("name", assetName));
                command.Parameters.Add(m_database.CreateParameter("description", assetDescription));
                command.Parameters.Add(m_database.CreateParameter("assetType", asset.Type));
                command.Parameters.Add(m_database.CreateParameter("local", asset.Local));
                command.Parameters.Add(m_database.CreateParameter("temporary", asset.Temporary));
                command.Parameters.Add(m_database.CreateParameter("access_time", now));
                command.Parameters.Add(m_database.CreateParameter("create_time", now));
                command.Parameters.Add(m_database.CreateParameter("asset_flags", (int)asset.Flags));
                command.Parameters.Add(m_database.CreateParameter("creatorid", asset.Metadata.CreatorID));
                command.Parameters.Add(m_database.CreateParameter("data", asset.Data));
                conn.Open();
                try
                {
                    command.ExecuteNonQuery();
                }
                catch(Exception e)
                {
                    m_log.Error("[ASSET DB]: Error storing item :" + e.Message + " sql "+sql);
                }
            }
            return true;
        }


// Commented out since currently unused - this probably should be called in GetAsset()
//        private void UpdateAccessTime(AssetBase asset)
//        {
//            using (AutoClosingSqlCommand cmd = m_database.Query("UPDATE assets SET access_time = :access_time WHERE id=:id"))
//            {
//                int now = (int)((System.DateTime.Now.Ticks - m_ticksToEpoch) / 10000000);
//                cmd.Parameters.AddWithValue(":id", asset.FullID.ToString());
//                cmd.Parameters.AddWithValue(":access_time", now);
//                try
//                {
//                    cmd.ExecuteNonQuery();
//                }
//                catch (Exception e)
//                {
//                    m_log.Error(e.ToString());
//                }
//            }
//        }

        /// <summary>
        /// Check if the assets exist in the database.
        /// </summary>
        /// <param name="uuids">The assets' IDs</param>
        /// <returns>For each asset: true if it exists, false otherwise</returns>
        public override bool[] AssetsExist(UUID[] uuids)
        {
            if (uuids.Length == 0)
                return new bool[0];

            HashSet<UUID> exist = new HashSet<UUID>();

            string ids = "'" + string.Join("','", uuids) + "'";
            string sql = string.Format("SELECT id FROM assets WHERE id IN ({0})", ids);

            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                conn.Open();
                using (NpgsqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        UUID id = DBGuid.FromDB(reader["id"]);
                        exist.Add(id);
                    }
                }
            }

            bool[] results = new bool[uuids.Length];
            for (int i = 0; i < uuids.Length; i++)
                results[i] = exist.Contains(uuids[i]);
            return results;
        }

        /// <summary>
        /// Returns a list of AssetMetadata objects. The list is a subset of
        /// the entire data set offset by <paramref name="start" /> containing
        /// <paramref name="count" /> elements.
        /// </summary>
        /// <param name="start">The number of results to discard from the total data set.</param>
        /// <param name="count">The number of rows the returned list should contain.</param>
        /// <returns>A list of AssetMetadata objects.</returns>
        public override List<AssetMetadata> FetchAssetMetadataSet(int start, int count)
        {
            List<AssetMetadata> retList = new List<AssetMetadata>(count);
            string sql = @" SELECT id, name, description, " + "\"assetType\"" + @", temporary, creatorid
                              FROM assets 
                             order by id
                             limit :stop
                            offset :start;";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("start", start));
                cmd.Parameters.Add(m_database.CreateParameter("stop", start + count - 1));
                conn.Open();
                using (NpgsqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        AssetMetadata metadata = new AssetMetadata();
                        metadata.FullID = DBGuid.FromDB(reader["id"]);
                        metadata.Name = (string)reader["name"];
                        metadata.Description = (string)reader["description"];
                        metadata.Type = Convert.ToSByte(reader["assetType"]);
                        metadata.Temporary = Convert.ToBoolean(reader["temporary"]);
                        metadata.CreatorID = (string)reader["creatorid"];
                        retList.Add(metadata);
                    }
                }
            }

            return retList;
        }

        public override bool Delete(string id)
        {
            return false;
        }
        #endregion
    }
}
