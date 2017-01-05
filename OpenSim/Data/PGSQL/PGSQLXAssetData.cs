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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;
using Npgsql;

namespace OpenSim.Data.PGSQL
{
    public class PGSQLXAssetData : IXAssetDataPlugin
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
        private PGSQLManager m_database;
        private string m_connectionString;
        private object m_dbLock = new object();

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
        /// <item>Loads and initialises the PGSQL storage plugin.</item>
        /// <item>Warns and uses the obsolete pgsql_connection.ini if connect string is empty.</item>
        /// <item>Check for migration</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="connect">connect string</param>
        public void Initialise(string connect)
        {
            m_log.ErrorFormat("[PGSQL XASSETDATA]: ***********************************************************");
            m_log.ErrorFormat("[PGSQL XASSETDATA]: ***********************************************************");
            m_log.ErrorFormat("[PGSQL XASSETDATA]: ***********************************************************");
            m_log.ErrorFormat("[PGSQL XASSETDATA]: THIS PLUGIN IS STRICTLY EXPERIMENTAL.");
            m_log.ErrorFormat("[PGSQL XASSETDATA]: DO NOT USE FOR ANY DATA THAT YOU DO NOT MIND LOSING.");
            m_log.ErrorFormat("[PGSQL XASSETDATA]: DATABASE TABLES CAN CHANGE AT ANY TIME, CAUSING EXISTING DATA TO BE LOST.");
            m_log.ErrorFormat("[PGSQL XASSETDATA]: ***********************************************************");
            m_log.ErrorFormat("[PGSQL XASSETDATA]: ***********************************************************");
            m_log.ErrorFormat("[PGSQL XASSETDATA]: ***********************************************************");

            m_connectionString = connect;
            m_database = new PGSQLManager(m_connectionString);

            using (NpgsqlConnection dbcon = new NpgsqlConnection(m_connectionString))
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
            get { return "PGSQL XAsset storage engine"; }
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
//            m_log.DebugFormat("[PGSQL XASSET DATA]: Looking for asset {0}", assetID);

            AssetBase asset = null;
            lock (m_dbLock)
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    using (NpgsqlCommand cmd = new NpgsqlCommand(
                        @"SELECT name, description, access_time, ""AssetType"", local, temporary, asset_flags, creatorid, data
                            FROM XAssetsMeta
                            JOIN XAssetsData ON XAssetsMeta.hash = XAssetsData.Hash WHERE id=:ID",
                        dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("ID", assetID));

                        try
                        {
                            using (NpgsqlDataReader dbReader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                            {
                                if (dbReader.Read())
                                {
                                    asset = new AssetBase(
                                        assetID,
                                        (string)dbReader["name"],
                                        Convert.ToSByte(dbReader["AssetType"]),
                                        dbReader["creatorid"].ToString());

                                    asset.Data = (byte[])dbReader["data"];
                                    asset.Description = (string)dbReader["description"];

                                    string local = dbReader["local"].ToString();
                                    if (local.Equals("1") || local.Equals("true", StringComparison.InvariantCultureIgnoreCase))
                                        asset.Local = true;
                                    else
                                        asset.Local = false;

                                    asset.Temporary = Convert.ToBoolean(dbReader["temporary"]);
                                    asset.Flags = (AssetFlags)Convert.ToInt32(dbReader["asset_flags"]);

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

                                    UpdateAccessTime(asset.Metadata, (int)dbReader["access_time"]);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.Error(string.Format("[PGSQL XASSET DATA]: Failure fetching asset {0}", assetID), e);
                        }
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

            lock (m_dbLock)
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    using (NpgsqlTransaction transaction = dbcon.BeginTransaction())
                    {
                        string assetName = asset.Name;
                        if (asset.Name.Length > 64)
                        {
                            assetName = asset.Name.Substring(0, 64);
                            m_log.WarnFormat(
                                "[XASSET DB]: Name '{0}' for asset {1} truncated from {2} to {3} characters on add",
                                asset.Name, asset.ID, asset.Name.Length, assetName.Length);
                        }

                        string assetDescription = asset.Description;
                        if (asset.Description.Length > 64)
                        {
                            assetDescription = asset.Description.Substring(0, 64);
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

                        UUID asset_id;
                        UUID.TryParse(asset.ID, out asset_id);

//                        m_log.DebugFormat(
//                            "[XASSET DB]: Compressed data size for {0} {1}, hash {2} is {3}",
//                            asset.ID, asset.Name, hash, compressedData.Length);

                        try
                        {
                            using (NpgsqlCommand cmd =
                                new NpgsqlCommand(
                                    @"insert INTO XAssetsMeta(id, hash, name, description, ""AssetType"", local, temporary, create_time, access_time, asset_flags, creatorid)
                                       Select :ID, :Hash, :Name, :Description, :AssetType, :Local, :Temporary, :CreateTime, :AccessTime, :AssetFlags, :CreatorID
                                        where not exists( Select id from XAssetsMeta where id = :ID);

                                      update XAssetsMeta
                                          set id = :ID, hash = :Hash, name = :Name, description = :Description,
                                              ""AssetType"" = :AssetType, local = :Local, temporary = :Temporary, create_time = :CreateTime,
                                              access_time = :AccessTime, asset_flags = :AssetFlags, creatorid = :CreatorID
                                        where id = :ID;
                                     ",
                                    dbcon))
                            {

                                // create unix epoch time
                                int now = (int)Utils.DateTimeToUnixTime(DateTime.UtcNow);
                                cmd.Parameters.Add(m_database.CreateParameter("ID", asset_id));
                                cmd.Parameters.Add(m_database.CreateParameter("Hash", hash));
                                cmd.Parameters.Add(m_database.CreateParameter("Name", assetName));
                                cmd.Parameters.Add(m_database.CreateParameter("Description", assetDescription));
                                cmd.Parameters.Add(m_database.CreateParameter("AssetType", asset.Type));
                                cmd.Parameters.Add(m_database.CreateParameter("Local", asset.Local));
                                cmd.Parameters.Add(m_database.CreateParameter("Temporary", asset.Temporary));
                                cmd.Parameters.Add(m_database.CreateParameter("CreateTime", now));
                                cmd.Parameters.Add(m_database.CreateParameter("AccessTime", now));
                                cmd.Parameters.Add(m_database.CreateParameter("CreatorID", asset.Metadata.CreatorID));
                                cmd.Parameters.Add(m_database.CreateParameter("AssetFlags", (int)asset.Flags));

                                cmd.ExecuteNonQuery();
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat("[ASSET DB]: PGSQL failure creating asset metadata {0} with name \"{1}\". Error: {2}",
                                asset.FullID, asset.Name, e.Message);

                            transaction.Rollback();

                            return;
                        }

                        if (!ExistsData(dbcon, transaction, hash))
                        {
                            try
                            {
                                using (NpgsqlCommand cmd =
                                    new NpgsqlCommand(
                                        @"INSERT INTO XAssetsData(hash, data) VALUES(:Hash, :Data)",
                                        dbcon))
                                {
                                    cmd.Parameters.Add(m_database.CreateParameter("Hash", hash));
                                    cmd.Parameters.Add(m_database.CreateParameter("Data", asset.Data));
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            catch (Exception e)
                            {
                                m_log.ErrorFormat("[XASSET DB]: PGSQL failure creating asset data {0} with name \"{1}\". Error: {2}",
                                    asset.FullID, asset.Name, e.Message);

                                transaction.Rollback();

                                return;
                            }
                        }

                        transaction.Commit();
                    }
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

            lock (m_dbLock)
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(m_connectionString))
                {
                    dbcon.Open();
                    NpgsqlCommand cmd =
                        new NpgsqlCommand(@"update XAssetsMeta set access_time=:AccessTime where id=:ID", dbcon);

                    try
                    {
                        UUID asset_id;
                        UUID.TryParse(assetMetadata.ID, out asset_id);

                        using (cmd)
                        {
                            // create unix epoch time
                            cmd.Parameters.Add(m_database.CreateParameter("id", asset_id));
                            cmd.Parameters.Add(m_database.CreateParameter("access_time", (int)Utils.DateTimeToUnixTime(now)));
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat(
                            "[XASSET PGSQL DB]: Failure updating access_time for asset {0} with name {1} : {2}",
                            assetMetadata.ID, assetMetadata.Name, e.Message);
                    }
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
        private bool ExistsData(NpgsqlConnection dbcon, NpgsqlTransaction transaction, byte[] hash)
        {
//            m_log.DebugFormat("[ASSETS DB]: Checking for asset {0}", uuid);

            bool exists = false;

            using (NpgsqlCommand cmd = new NpgsqlCommand(@"SELECT hash FROM XAssetsData WHERE hash=:Hash", dbcon))
            {
                cmd.Parameters.Add(m_database.CreateParameter("Hash", hash));

                try
                {
                    using (NpgsqlDataReader dbReader = cmd.ExecuteReader(CommandBehavior.SingleRow))
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
                        "[XASSETS DB]: PGSql failure in ExistsData fetching hash {0}.  Exception {1}{2}",
                        hash, e.Message, e.StackTrace);
                }
            }

            return exists;
        }

        /// <summary>
        /// Check if the assets exist in the database.
        /// </summary>
        /// <param name="uuids">The assets' IDs</param>
        /// <returns>For each asset: true if it exists, false otherwise</returns>
        public bool[] AssetsExist(UUID[] uuids)
        {
            if (uuids.Length == 0)
                return new bool[0];

            HashSet<UUID> exist = new HashSet<UUID>();

            string ids = "'" + string.Join("','", uuids) + "'";
            string sql = string.Format(@"SELECT id FROM XAssetsMeta WHERE id IN ({0})", ids);

            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            {
                conn.Open();
                using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
                {
                    using (NpgsqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            UUID id = DBGuid.FromDB(reader["id"]);
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
        /// Check if the asset exists in the database
        /// </summary>
        /// <param name="uuid">The asset UUID</param>
        /// <returns>true if it exists, false otherwise.</returns>
        public bool ExistsAsset(UUID uuid)
        {
//            m_log.DebugFormat("[ASSETS DB]: Checking for asset {0}", uuid);

            bool assetExists = false;

            lock (m_dbLock)
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(m_connectionString))
                {
                    dbcon.Open();
                    using (NpgsqlCommand cmd = new NpgsqlCommand(@"SELECT id FROM XAssetsMeta WHERE id=:ID", dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("id", uuid));

                        try
                        {
                            using (NpgsqlDataReader dbReader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                            {
                                if (dbReader.Read())
                                {
//                                    m_log.DebugFormat("[ASSETS DB]: Found asset {0}", uuid);
                                    assetExists = true;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.Error(string.Format("[XASSETS DB]: PGSql failure fetching asset {0}", uuid), e);
                        }
                    }
                }
            }

            return assetExists;
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

            lock (m_dbLock)
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(m_connectionString))
                {
                    dbcon.Open();
                    using(NpgsqlCommand cmd = new NpgsqlCommand(@"SELECT name, description, access_time, ""AssetType"", temporary, id, asset_flags, creatorid
                                            FROM XAssetsMeta
                                            LIMIT :start, :count",dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("start",start));
                        cmd.Parameters.Add(m_database.CreateParameter("count", count));

                        try
                        {
                            using (NpgsqlDataReader dbReader = cmd.ExecuteReader())
                            {
                                while (dbReader.Read())
                                {
                                    AssetMetadata metadata = new AssetMetadata();
                                    metadata.Name = (string)dbReader["name"];
                                    metadata.Description = (string)dbReader["description"];
                                    metadata.Type = Convert.ToSByte(dbReader["AssetType"]);
                                    metadata.Temporary = Convert.ToBoolean(dbReader["temporary"]);
                                    metadata.Flags = (AssetFlags)Convert.ToInt32(dbReader["asset_flags"]);
                                    metadata.FullID = DBGuid.FromDB(dbReader["id"]);
                                    metadata.CreatorID = dbReader["creatorid"].ToString();

                                    // We'll ignore this for now - it appears unused!
    //                                metadata.SHA1 = dbReader["hash"]);

                                    UpdateAccessTime(metadata, (int)dbReader["access_time"]);

                                    retList.Add(metadata);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.Error("[XASSETS DB]: PGSql failure fetching asset set" + Environment.NewLine + e.ToString());
                        }
                   }
                }
            }

            return retList;
        }

        public bool Delete(string id)
        {
//            m_log.DebugFormat("[XASSETS DB]: Deleting asset {0}", id);

            lock (m_dbLock)
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    using (NpgsqlCommand cmd = new NpgsqlCommand(@"delete from XAssetsMeta where id=:ID", dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter(id, id));
                        cmd.ExecuteNonQuery();
                    }

                    // TODO: How do we deal with data from deleted assets?  Probably not easily reapable unless we
                    // keep a reference count (?)
                }
            }

            return true;
        }

        #endregion
    }
}
