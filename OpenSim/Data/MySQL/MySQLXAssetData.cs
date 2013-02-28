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

        private bool m_enableCompression = false;
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
            lock (m_dbLock)
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT name, description, asset_type, local, temporary, asset_flags, creator_id, data FROM xassetsmeta JOIN xassetsdata ON xassetsmeta.hash = xassetsdata.hash WHERE id=?id",
                        dbcon))
                    {
                        cmd.Parameters.AddWithValue("?id", assetID.ToString());

                        try
                        {
                            using (MySqlDataReader dbReader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                            {
                                if (dbReader.Read())
                                {
                                    asset = new AssetBase(assetID, (string)dbReader["name"], (sbyte)dbReader["asset_type"], dbReader["creator_id"].ToString());
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
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.Error("[MYSQL XASSET DATA]: MySql failure fetching asset " + assetID + ": " + e.Message);
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
            lock (m_dbLock)
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    using (MySqlTransaction transaction = dbcon.BeginTransaction())
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

//                        m_log.DebugFormat(
//                            "[XASSET DB]: Compressed data size for {0} {1}, hash {2} is {3}",
//                            asset.ID, asset.Name, hash, compressedData.Length);

                        try
                        {
                            using (MySqlCommand cmd =
                                new MySqlCommand(
                                    "replace INTO xassetsmeta(id, hash, name, description, asset_type, local, temporary, create_time, access_time, asset_flags, creator_id)" +
                                    "VALUES(?id, ?hash, ?name, ?description, ?asset_type, ?local, ?temporary, ?create_time, ?access_time, ?asset_flags, ?creator_id)",
                                    dbcon))
                            {
                                // create unix epoch time
                                int now = (int)Utils.DateTimeToUnixTime(DateTime.UtcNow);
                                cmd.Parameters.AddWithValue("?id", asset.ID);
                                cmd.Parameters.AddWithValue("?hash", hash);
                                cmd.Parameters.AddWithValue("?name", assetName);
                                cmd.Parameters.AddWithValue("?description", assetDescription);
                                cmd.Parameters.AddWithValue("?asset_type", asset.Type);
                                cmd.Parameters.AddWithValue("?local", asset.Local);
                                cmd.Parameters.AddWithValue("?temporary", asset.Temporary);
                                cmd.Parameters.AddWithValue("?create_time", now);
                                cmd.Parameters.AddWithValue("?access_time", now);
                                cmd.Parameters.AddWithValue("?creator_id", asset.Metadata.CreatorID);
                                cmd.Parameters.AddWithValue("?asset_flags", (int)asset.Flags);
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
                                        "INSERT INTO xassetsdata(hash, data) VALUES(?hash, ?data)",
                                        dbcon))
                                {
                                    cmd.Parameters.AddWithValue("?hash", hash);
                                    cmd.Parameters.AddWithValue("?data", asset.Data);
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
        }

//        private void UpdateAccessTime(AssetBase asset)
//        {
//            lock (m_dbLock)
//            {
//                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
//                {
//                    dbcon.Open();
//                    MySqlCommand cmd =
//                        new MySqlCommand("update assets set access_time=?access_time where id=?id",
//                                         dbcon);
//
//                    // need to ensure we dispose
//                    try
//                    {
//                        using (cmd)
//                        {
//                            // create unix epoch time
//                            int now = (int)Utils.DateTimeToUnixTime(DateTime.UtcNow);
//                            cmd.Parameters.AddWithValue("?id", asset.ID);
//                            cmd.Parameters.AddWithValue("?access_time", now);
//                            cmd.ExecuteNonQuery();
//                            cmd.Dispose();
//                        }
//                    }
//                    catch (Exception e)
//                    {
//                        m_log.ErrorFormat(
//                            "[ASSETS DB]: " +
//                            "MySql failure updating access_time for asset {0} with name {1}" + Environment.NewLine + e.ToString()
//                            + Environment.NewLine + "Attempting reconnection", asset.FullID, asset.Name);
//                    }
//                }
//            }
//
//        }

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

            using (MySqlCommand cmd = new MySqlCommand("SELECT hash FROM xassetsdata WHERE hash=?hash", dbcon))
            {
                cmd.Parameters.AddWithValue("?hash", hash);

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
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand("SELECT id FROM xassetsmeta WHERE id=?id", dbcon))
                    {
                        cmd.Parameters.AddWithValue("?id", uuid.ToString());

                        try
                        {
                            using (MySqlDataReader dbReader = cmd.ExecuteReader(CommandBehavior.SingleRow))
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
                            m_log.ErrorFormat(
                                "[XASSETS DB]: MySql failure fetching asset {0}" + Environment.NewLine + e.ToString(), uuid);
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
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();
                    MySqlCommand cmd = new MySqlCommand("SELECT name,description,asset_type,temporary,id,asset_flags,creator_id FROM xassetsmeta LIMIT ?start, ?count", dbcon);
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
                                metadata.Type = (sbyte)dbReader["asset_type"];
                                metadata.Temporary = Convert.ToBoolean(dbReader["temporary"]); // Not sure if this is correct.
                                metadata.Flags = (AssetFlags)Convert.ToInt32(dbReader["asset_flags"]);
                                metadata.FullID = DBGuid.FromDB(dbReader["id"]);
                                metadata.CreatorID = dbReader["creator_id"].ToString();

                                // We'll ignore this for now - it appears unused!
//                                metadata.SHA1 = dbReader["hash"]);

                                retList.Add(metadata);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Error("[XASSETS DB]: MySql failure fetching asset set" + Environment.NewLine + e.ToString());
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
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    using (MySqlCommand cmd = new MySqlCommand("delete from xassetsmeta where id=?id", dbcon))
                    {
                        cmd.Parameters.AddWithValue("?id", id);
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
