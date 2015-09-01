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
using log4net;
using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A MySQL Interface for the Asset Server
    /// </summary>
    public class MySQLAssetData : AssetDataBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_connectionString;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        #region IPlugin Members

        public override string Version { get { return "1.0.0.0"; } }

        /// <summary>
        /// <para>Initialises Asset interface</para>
        /// <para>
        /// <list type="bullet">
        /// <item>Loads and initialises the MySQL storage plugin.</item>
        /// <item>Warns and uses the obsolete mysql_connection.ini if connect string is empty.</item>
        /// <item>Check for migration</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="connect">connect string</param>
        public override void Initialise(string connect)
        {
            m_connectionString = connect;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                Migration m = new Migration(dbcon, Assembly, "AssetStore");
                m.Update();
            }
        }

        public override void Initialise()
        {
            throw new NotImplementedException();
        }

        public override void Dispose() { }

        /// <summary>
        /// The name of this DB provider
        /// </summary>
        override public string Name
        {
            get { return "MySQL Asset storage engine"; }
        }

        #endregion

        #region IAssetDataPlugin Members

        /// <summary>
        /// Fetch Asset <paramref name="assetID"/> from database
        /// </summary>
        /// <param name="assetID">Asset UUID to fetch</param>
        /// <returns>Return the asset</returns>
        /// <remarks>On failure : throw an exception and attempt to reconnect to database</remarks>
        override public AssetBase GetAsset(UUID assetID)
        {
            AssetBase asset = null;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT name, description, assetType, local, temporary, asset_flags, CreatorID, data FROM assets WHERE id=?id",
                    dbcon))
                {
                    cmd.Parameters.AddWithValue("?id", assetID.ToString());

                    try
                    {
                        using (MySqlDataReader dbReader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if (dbReader.Read())
                            {
                                asset = new AssetBase(assetID, (string)dbReader["name"], (sbyte)dbReader["assetType"], dbReader["CreatorID"].ToString());
                                asset.Data = (byte[])dbReader["data"];
                                asset.Description = (string)dbReader["description"];

                                string local = dbReader["local"].ToString();
                                if (local.Equals("1") || local.Equals("true", StringComparison.InvariantCultureIgnoreCase))
                                    asset.Local = true;
                                else
                                    asset.Local = false;

                                asset.Temporary = Convert.ToBoolean(dbReader["temporary"]);
                                asset.Flags = (AssetFlags)Convert.ToInt32(dbReader["asset_flags"]);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Error(
                            string.Format("[ASSETS DB]: MySql failure fetching asset {0}.  Exception  ", assetID), e);
                    }
                }
            }

            return asset;
        }

        /// <summary>
        /// Create an asset in database, or update it if existing.
        /// </summary>
        /// <param name="asset">Asset UUID to create</param>
        /// <remarks>On failure : Throw an exception and attempt to reconnect to database</remarks>
        override public void StoreAsset(AssetBase asset)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd =
                    new MySqlCommand(
                        "replace INTO assets(id, name, description, assetType, local, temporary, create_time, access_time, asset_flags, CreatorID, data)" +
                        "VALUES(?id, ?name, ?description, ?assetType, ?local, ?temporary, ?create_time, ?access_time, ?asset_flags, ?CreatorID, ?data)",
                        dbcon))
                {
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

                    try
                    {
                        using (cmd)
                        {
                            // create unix epoch time
                            int now = (int)Utils.DateTimeToUnixTime(DateTime.UtcNow);
                            cmd.Parameters.AddWithValue("?id", asset.ID);
                            cmd.Parameters.AddWithValue("?name", assetName);
                            cmd.Parameters.AddWithValue("?description", assetDescription);
                            cmd.Parameters.AddWithValue("?assetType", asset.Type);
                            cmd.Parameters.AddWithValue("?local", asset.Local);
                            cmd.Parameters.AddWithValue("?temporary", asset.Temporary);
                            cmd.Parameters.AddWithValue("?create_time", now);
                            cmd.Parameters.AddWithValue("?access_time", now);
                            cmd.Parameters.AddWithValue("?CreatorID", asset.Metadata.CreatorID);
                            cmd.Parameters.AddWithValue("?asset_flags", (int)asset.Flags);
                            cmd.Parameters.AddWithValue("?data", asset.Data);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Error(
                            string.Format(
                                "[ASSET DB]: MySQL failure creating asset {0} with name {1}.  Exception  ",
                                asset.FullID, asset.Name)
                            , e);
                    }
                }
            }
        }

        private void UpdateAccessTime(AssetBase asset)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd
                    = new MySqlCommand("update assets set access_time=?access_time where id=?id", dbcon))
                {
                    try
                    {
                        using (cmd)
                        {
                            // create unix epoch time
                            int now = (int)Utils.DateTimeToUnixTime(DateTime.UtcNow);
                            cmd.Parameters.AddWithValue("?id", asset.ID);
                            cmd.Parameters.AddWithValue("?access_time", now);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Error(
                            string.Format(
                                "[ASSETS DB]: Failure updating access_time for asset {0} with name {1}.  Exception  ", 
                                asset.FullID, asset.Name), 
                            e);
                    }
                }
            }
        }

        /// <summary>
        /// Check if the assets exist in the database.
        /// </summary>
        /// <param name="uuidss">The assets' IDs</param>
        /// <returns>For each asset: true if it exists, false otherwise</returns>
        public override bool[] AssetsExist(UUID[] uuids)
        {
            if (uuids.Length == 0)
                return new bool[0];

            HashSet<UUID> exist = new HashSet<UUID>();

            string ids = "'" + string.Join("','", uuids) + "'";
            string sql = string.Format("SELECT id FROM assets WHERE id IN ({0})", ids);

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                using (MySqlCommand cmd = new MySqlCommand(sql, dbcon))
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            UUID id = DBGuid.FromDB(dbReader["id"]);
                            exist.Add(id);
                        }
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

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd
                    = new MySqlCommand(
                        "SELECT name,description,assetType,temporary,id,asset_flags,CreatorID FROM assets LIMIT ?start, ?count",
                        dbcon))
                {
                    cmd.Parameters.AddWithValue("?start", start);
                    cmd.Parameters.AddWithValue("?count", count);

                    try
                    {
                        using (MySqlDataReader dbReader = cmd.ExecuteReader())
                        {
                            while (dbReader.Read())
                            {
                                AssetMetadata metadata = new AssetMetadata();
                                metadata.Name = (string)dbReader["name"];
                                metadata.Description = (string)dbReader["description"];
                                metadata.Type = (sbyte)dbReader["assetType"];
                                metadata.Temporary = Convert.ToBoolean(dbReader["temporary"]); // Not sure if this is correct.
                                metadata.Flags = (AssetFlags)Convert.ToInt32(dbReader["asset_flags"]);
                                metadata.FullID = DBGuid.FromDB(dbReader["id"]);
                                metadata.CreatorID = dbReader["CreatorID"].ToString();

                                // Current SHA1s are not stored/computed.
                                metadata.SHA1 = new byte[] { };

                                retList.Add(metadata);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Error(
                            string.Format(
                                "[ASSETS DB]: MySql failure fetching asset set from {0}, count {1}.  Exception  ", 
                                start, count), 
                            e);
                    }
                }
            }

            return retList;
        }

        public override bool Delete(string id)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("delete from assets where id=?id", dbcon))
                {
                    cmd.Parameters.AddWithValue("?id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            return true;
        }

        #endregion
    }
}