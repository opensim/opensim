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
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using log4net;
using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;

namespace OpenSim.Data.MySQL
{
    public class MySQLXAssetData : IXAssetDataPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        /// <summary>
        /// Number of days that must pass before we update the access time on an asset when it has been fetched.
        /// </summary>
        private const int DaysBetweenAccessTimeUpdates = 30;

        private bool m_enableCompression = false;
        private string m_connectionString;

        /// <summary>
        /// We can reuse this for all hashing since all methods are single-threaded through m_dbBLock
        /// </summary>
        private HashAlgorithm hasher = new SHA256CryptoServiceProvider();

        #region IPlugin Members

        public string Version { get { return "1.0.0.0"; } }

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
        public void Initialise(string connect)
        {
            m_log.ErrorFormat("[MYSQL XASSETDATA]: ***********************************************************");
            m_log.ErrorFormat("[MYSQL XASSETDATA]: ***********************************************************");
            m_log.ErrorFormat("[MYSQL XASSETDATA]: ***********************************************************");
            m_log.ErrorFormat("[MYSQL XASSETDATA]: THIS PLUGIN IS STRICTLY EXPERIMENTAL.");
            m_log.ErrorFormat("[MYSQL XASSETDATA]: DO NOT USE FOR ANY DATA THAT YOU DO NOT MIND LOSING.");
            m_log.ErrorFormat("[MYSQL XASSETDATA]: DATABASE TABLES CAN CHANGE AT ANY TIME, CAUSING EXISTING DATA TO BE LOST.");
            m_log.ErrorFormat("[MYSQL XASSETDATA]: ***********************************************************");
            m_log.ErrorFormat("[MYSQL XASSETDATA]: ***********************************************************");
            m_log.ErrorFormat("[MYSQL XASSETDATA]: ***********************************************************");

            m_connectionString = connect;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                Migration m = new Migration(dbcon, Assembly, "XAssetStore");
                m.Update();
            }
        }

        public void Initialise()
        {
            throw new NotImplementedException();
        }

        public void Dispose() { }

        /// <summary>
        /// The name of this DB provider
        /// </summary>
        public string Name
        {
            get { return "MySQL XAsset storage engine"; }
        }

        #endregion

        #region IAssetDataPlugin Members

        /// <summary>
        /// Fetch Asset <paramref name="assetID"/> from database
        /// </summary>
        /// <param name="assetID">Asset UUID to fetch</param>
        /// <returns>Return the asset</returns>
        /// <remarks>On failure : throw an exception and attempt to reconnect to database</remarks>
        public AssetBase GetAsset(UUID assetID)
        {
//            m_log.DebugFormat("[MYSQL XASSET DATA]: Looking for asset {0}", assetID);

            AssetBase asset = null;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT Name, Description, AccessTime, AssetType, Local, Temporary, AssetFlags, CreatorID, Data FROM XAssetsMeta JOIN XAssetsData ON XAssetsMeta.Hash = XAssetsData.Hash WHERE ID=?ID",
                    dbcon))
                {
                    cmd.Parameters.AddWithValue("?ID", assetID.ToString());

                    try
                    {
                        using (MySqlDataReader dbReader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if (dbReader.Read())
                            {
                                asset = new AssetBase(assetID, (string)dbReader["Name"], (sbyte)dbReader["AssetType"], dbReader["CreatorID"].ToString());
                                asset.Data = (byte[])dbReader["Data"];
                                asset.Description = (string)dbReader["Description"];

                                string local = dbReader["Local"].ToString();
                                if (local.Equals("1") || local.Equals("true", StringComparison.InvariantCultureIgnoreCase))
                                    asset.Local = true;
                                else
                                    asset.Local = false;

                                asset.Temporary = Convert.ToBoolean(dbReader["Temporary"]);
                                asset.Flags = (AssetFlags)Convert.ToInt32(dbReader["AssetFlags"]);

                                if (m_enableCompression)
                                {
                                    using (GZipStream decompressionStream = new GZipStream(new MemoryStream(asset.Data), CompressionMode.Decompress))
                                    {
                                        MemoryStream outputStream = new MemoryStream();
                                        WebUtil.CopyStream(decompressionStream, outputStream, int.MaxValue);
//                                        int compressedLength = asset.Data.Length;
                                        asset.Data = outputStream.ToArray();

//                                        m_log.DebugFormat(
//                                            "[XASSET DB]: Decompressed {0} {1} to {2} bytes from {3}",
//                                            asset.ID, asset.Name, asset.Data.Length, compressedLength);
                                    }
                                }

                                UpdateAccessTime(asset.Metadata, (int)dbReader["AccessTime"]);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Error(string.Format("[MYSQL XASSET DATA]: Failure fetching asset {0}", assetID), e);
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
        public void StoreAsset(AssetBase asset)
        {
//            m_log.DebugFormat("[XASSETS DB]: Storing asset {0} {1}", asset.Name, asset.ID);

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlTransaction transaction = dbcon.BeginTransaction())
                {
                    string assetName = asset.Name;
                    if (asset.Name.Length > AssetBase.MAX_ASSET_NAME)
                    {
                        assetName = asset.Name.Substring(0, AssetBase.MAX_ASSET_NAME);
                        m_log.WarnFormat(
                            "[XASSET DB]: Name '{0}' for asset {1} truncated from {2} to {3} characters on add", 
                            asset.Name, asset.ID, asset.Name.Length, assetName.Length);
                    }

                    string assetDescription = asset.Description;
                    if (asset.Description.Length > AssetBase.MAX_ASSET_DESC)
                    {
                        assetDescription = asset.Description.Substring(0, AssetBase.MAX_ASSET_DESC);
                        m_log.WarnFormat(
                            "[XASSET DB]: Description '{0}' for asset {1} truncated from {2} to {3} characters on add", 
                            asset.Description, asset.ID, asset.Description.Length, assetDescription.Length);
                    }

                    if (m_enableCompression)
                    {
                        MemoryStream outputStream = new MemoryStream();

                        using (GZipStream compressionStream = new GZipStream(outputStream, CompressionMode.Compress, false))
                        {
//                            Console.WriteLine(WebUtil.CopyTo(new MemoryStream(asset.Data), compressionStream, int.MaxValue));
                            // We have to close the compression stream in order to make sure it writes everything out to the underlying memory output stream.
                            compressionStream.Close();
                            byte[] compressedData = outputStream.ToArray();
                            asset.Data = compressedData;
                        }
                    }

                    byte[] hash = hasher.ComputeHash(asset.Data);

//                        m_log.DebugFormat(
//                            "[XASSET DB]: Compressed data size for {0} {1}, hash {2} is {3}",
//                            asset.ID, asset.Name, hash, compressedData.Length);

                    try
                    {
                        using (MySqlCommand cmd =
                            new MySqlCommand(
                                "replace INTO XAssetsMeta(ID, Hash, Name, Description, AssetType, Local, Temporary, CreateTime, AccessTime, AssetFlags, CreatorID)" +
                                "VALUES(?ID, ?Hash, ?Name, ?Description, ?AssetType, ?Local, ?Temporary, ?CreateTime, ?AccessTime, ?AssetFlags, ?CreatorID)",
                                dbcon))
                        {
                            // create unix epoch time
                            int now = (int)Utils.DateTimeToUnixTime(DateTime.UtcNow);
                            cmd.Parameters.AddWithValue("?ID", asset.ID);
                            cmd.Parameters.AddWithValue("?Hash", hash);
                            cmd.Parameters.AddWithValue("?Name", assetName);
                            cmd.Parameters.AddWithValue("?Description", assetDescription);
                            cmd.Parameters.AddWithValue("?AssetType", asset.Type);
                            cmd.Parameters.AddWithValue("?Local", asset.Local);
                            cmd.Parameters.AddWithValue("?Temporary", asset.Temporary);
                            cmd.Parameters.AddWithValue("?CreateTime", now);
                            cmd.Parameters.AddWithValue("?AccessTime", now);
                            cmd.Parameters.AddWithValue("?CreatorID", asset.Metadata.CreatorID);
                            cmd.Parameters.AddWithValue("?AssetFlags", (int)asset.Flags);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[ASSET DB]: MySQL failure creating asset metadata {0} with name \"{1}\". Error: {2}",
                            asset.FullID, asset.Name, e.Message);

                        transaction.Rollback();

                        return;
                    }

                    if (!ExistsData(dbcon, transaction, hash))
                    {
                        try
                        {
                            using (MySqlCommand cmd =
                                new MySqlCommand(
                                    "INSERT INTO XAssetsData(Hash, Data) VALUES(?Hash, ?Data)",
                                    dbcon))
                            {
                                cmd.Parameters.AddWithValue("?Hash", hash);
                                cmd.Parameters.AddWithValue("?Data", asset.Data);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat("[XASSET DB]: MySQL failure creating asset data {0} with name \"{1}\". Error: {2}",
                                asset.FullID, asset.Name, e.Message);

                            transaction.Rollback();

                            return;
                        }
                    }

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Updates the access time of the asset if it was accessed above a given threshhold amount of time.
        /// </summary>
        /// <remarks>
        /// This gives us some insight into assets which haven't ben accessed for a long period.  This is only done
        /// over the threshold time to avoid excessive database writes as assets are fetched.
        /// </remarks>
        /// <param name='asset'></param>
        /// <param name='accessTime'></param>
        private void UpdateAccessTime(AssetMetadata assetMetadata, int accessTime)
        {
            DateTime now = DateTime.UtcNow;

            if ((now - Utils.UnixTimeToDateTime(accessTime)).TotalDays < DaysBetweenAccessTimeUpdates)
                return;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                MySqlCommand cmd =
                    new MySqlCommand("update XAssetsMeta set AccessTime=?AccessTime where ID=?ID", dbcon);

                try
                {
                    using (cmd)
                    {
                        // create unix epoch time
                        cmd.Parameters.AddWithValue("?ID", assetMetadata.ID);
                        cmd.Parameters.AddWithValue("?AccessTime", (int)Utils.DateTimeToUnixTime(now));
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception)
                {
                    m_log.ErrorFormat(
                        "[XASSET MYSQL DB]: Failure updating access_time for asset {0} with name {1}", 
                        assetMetadata.ID, assetMetadata.Name);
                }
            }
        }

        /// <summary>
        /// We assume we already have the m_dbLock.
        /// </summary>
        /// TODO: need to actually use the transaction.
        /// <param name="dbcon"></param>
        /// <param name="transaction"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        private bool ExistsData(MySqlConnection dbcon, MySqlTransaction transaction, byte[] hash)
        {
//            m_log.DebugFormat("[ASSETS DB]: Checking for asset {0}", uuid);

            bool exists = false;

            using (MySqlCommand cmd = new MySqlCommand("SELECT Hash FROM XAssetsData WHERE Hash=?Hash", dbcon))
            {
                cmd.Parameters.AddWithValue("?Hash", hash);

                try
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (dbReader.Read())
                        {
//                                    m_log.DebugFormat("[ASSETS DB]: Found asset {0}", uuid);
                            exists = true;
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[XASSETS DB]: MySql failure in ExistsData fetching hash {0}.  Exception {1}{2}",
                        hash, e.Message, e.StackTrace);
                }
            }

            return exists;
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
            string sql = string.Format("SELECT ID FROM assets WHERE ID IN ({0})", ids);

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                using (MySqlCommand cmd = new MySqlCommand(sql, dbcon))
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            UUID id = DBGuid.FromDB(dbReader["ID"]);
                            exists.Add(id);
                        }
                    }
                }
            }

            bool[] results = new bool[uuids.Length];
            for (int i = 0; i < uuids.Length; i++)
                results[i] = exists.Contains(uuids[i]);
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
        public List<AssetMetadata> FetchAssetMetadataSet(int start, int count)
        {
            List<AssetMetadata> retList = new List<AssetMetadata>(count);

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                MySqlCommand cmd = new MySqlCommand("SELECT Name, Description, AccessTime, AssetType, Temporary, ID, AssetFlags, CreatorID FROM XAssetsMeta LIMIT ?start, ?count", dbcon);
                cmd.Parameters.AddWithValue("?start", start);
                cmd.Parameters.AddWithValue("?count", count);

                try
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            AssetMetadata metadata = new AssetMetadata();
                            metadata.Name = (string)dbReader["Name"];
                            metadata.Description = (string)dbReader["Description"];
                            metadata.Type = (sbyte)dbReader["AssetType"];
                            metadata.Temporary = Convert.ToBoolean(dbReader["Temporary"]); // Not sure if this is correct.
                            metadata.Flags = (AssetFlags)Convert.ToInt32(dbReader["AssetFlags"]);
                            metadata.FullID = DBGuid.FromDB(dbReader["ID"]);
                            metadata.CreatorID = dbReader["CreatorID"].ToString();

                            // We'll ignore this for now - it appears unused!
//                                metadata.SHA1 = dbReader["hash"]);

                            UpdateAccessTime(metadata, (int)dbReader["AccessTime"]);

                            retList.Add(metadata);
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[XASSETS DB]: MySql failure fetching asset set" + Environment.NewLine + e.ToString());
                }
            }

            return retList;
        }

        public bool Delete(string id)
        {
//            m_log.DebugFormat("[XASSETS DB]: Deleting asset {0}", id);

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("delete from XAssetsMeta where ID=?ID", dbcon))
                {
                    cmd.Parameters.AddWithValue("?ID", id);
                    cmd.ExecuteNonQuery();
                }

                // TODO: How do we deal with data from deleted assets?  Probably not easily reapable unless we
                // keep a reference count (?)
            }

            return true;
        }

        #endregion
    }
}
