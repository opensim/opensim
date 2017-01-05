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
#if CSharpSqlite
    using Community.CsharpSqlite.Sqlite;
#else
    using Mono.Data.Sqlite;
#endif

using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.SQLite
{
    /// <summary>
    /// An asset storage interface for the SQLite database system
    /// </summary>
    public class SQLiteAssetData : AssetDataBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const string SelectAssetSQL = "select * from assets where UUID=:UUID";
        private const string SelectAssetMetadataSQL = "select Name, Description, Type, Temporary, asset_flags, UUID, CreatorID from assets limit :start, :count";
        private const string DeleteAssetSQL = "delete from assets where UUID=:UUID";
        private const string InsertAssetSQL = "insert into assets(UUID, Name, Description, Type, Local, Temporary, asset_flags, CreatorID, Data) values(:UUID, :Name, :Description, :Type, :Local, :Temporary, :Flags, :CreatorID, :Data)";
        private const string UpdateAssetSQL = "update assets set Name=:Name, Description=:Description, Type=:Type, Local=:Local, Temporary=:Temporary, asset_flags=:Flags, CreatorID=:CreatorID, Data=:Data where UUID=:UUID";
        private const string assetSelect = "select * from assets";

        private SqliteConnection m_conn;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        override public void Dispose()
        {
            if (m_conn != null)
            {
                m_conn.Close();
                m_conn = null;
            }
        }

        /// <summary>
        /// <list type="bullet">
        /// <item>Initialises AssetData interface</item>
        /// <item>Loads and initialises a new SQLite connection and maintains it.</item>
        /// <item>use default URI if connect string is empty.</item>
        /// </list>
        /// </summary>
        /// <param name="dbconnect">connect string</param>
        override public void Initialise(string dbconnect)
        {
            if (Util.IsWindows())
                Util.LoadArchSpecificWindowsDll("sqlite3.dll");

            if (dbconnect == string.Empty)
            {
                dbconnect = "URI=file:Asset.db,version=3";
            }
            m_conn = new SqliteConnection(dbconnect);
            m_conn.Open();

            Migration m = new Migration(m_conn, Assembly, "AssetStore");
            m.Update();

            return;
        }

        /// <summary>
        /// Fetch Asset
        /// </summary>
        /// <param name="uuid">UUID of ... ?</param>
        /// <returns>Asset base</returns>
        override public AssetBase GetAsset(UUID uuid)
        {
            lock (this)
            {
                using (SqliteCommand cmd = new SqliteCommand(SelectAssetSQL, m_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":UUID", uuid.ToString()));
                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            AssetBase asset = buildAsset(reader);
                            reader.Close();
                            return asset;
                        }
                        else
                        {
                            reader.Close();
                            return null;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Create an asset
        /// </summary>
        /// <param name="asset">Asset Base</param>
        override public bool StoreAsset(AssetBase asset)
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

            //m_log.Info("[ASSET DB]: Creating Asset " + asset.FullID.ToString());
            if (AssetsExist(new[] { asset.FullID })[0])
            {
                //LogAssetLoad(asset);

                lock (this)
                {
                    using (SqliteCommand cmd = new SqliteCommand(UpdateAssetSQL, m_conn))
                    {
                        cmd.Parameters.Add(new SqliteParameter(":UUID", asset.FullID.ToString()));
                        cmd.Parameters.Add(new SqliteParameter(":Name", assetName));
                        cmd.Parameters.Add(new SqliteParameter(":Description", assetDescription));
                        cmd.Parameters.Add(new SqliteParameter(":Type", asset.Type));
                        cmd.Parameters.Add(new SqliteParameter(":Local", asset.Local));
                        cmd.Parameters.Add(new SqliteParameter(":Temporary", asset.Temporary));
                        cmd.Parameters.Add(new SqliteParameter(":Flags", asset.Flags));
                        cmd.Parameters.Add(new SqliteParameter(":CreatorID", asset.Metadata.CreatorID));
                        cmd.Parameters.Add(new SqliteParameter(":Data", asset.Data));

                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            else
            {
                lock (this)
                {
                    using (SqliteCommand cmd = new SqliteCommand(InsertAssetSQL, m_conn))
                    {
                        cmd.Parameters.Add(new SqliteParameter(":UUID", asset.FullID.ToString()));
                        cmd.Parameters.Add(new SqliteParameter(":Name", assetName));
                        cmd.Parameters.Add(new SqliteParameter(":Description", assetDescription));
                        cmd.Parameters.Add(new SqliteParameter(":Type", asset.Type));
                        cmd.Parameters.Add(new SqliteParameter(":Local", asset.Local));
                        cmd.Parameters.Add(new SqliteParameter(":Temporary", asset.Temporary));
                        cmd.Parameters.Add(new SqliteParameter(":Flags", asset.Flags));
                        cmd.Parameters.Add(new SqliteParameter(":CreatorID", asset.Metadata.CreatorID));
                        cmd.Parameters.Add(new SqliteParameter(":Data", asset.Data));

                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
        }

//        /// <summary>
//        /// Some... logging functionnality
//        /// </summary>
//        /// <param name="asset"></param>
//        private static void LogAssetLoad(AssetBase asset)
//        {
//            string temporary = asset.Temporary ? "Temporary" : "Stored";
//            string local = asset.Local ? "Local" : "Remote";
//
//            int assetLength = (asset.Data != null) ? asset.Data.Length : 0;
//
//            m_log.Debug("[ASSET DB]: " +
//                                     string.Format("Loaded {5} {4} Asset: [{0}][{3}] \"{1}\":{2} ({6} bytes)",
//                                                   asset.FullID, asset.Name, asset.Description, asset.Type,
//                                                   temporary, local, assetLength));
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
            string sql = string.Format("select UUID from assets where UUID in ({0})", ids);

            lock (this)
            {
                using (SqliteCommand cmd = new SqliteCommand(sql, m_conn))
                {
                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            UUID id = new UUID((string)reader["UUID"]);
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
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private static AssetBase buildAsset(IDataReader row)
        {
            // TODO: this doesn't work yet because something more
            // interesting has to be done to actually get these values
            // back out.  Not enough time to figure it out yet.
            AssetBase asset = new AssetBase(
                new UUID((String)row["UUID"]),
                (String)row["Name"],
                Convert.ToSByte(row["Type"]),
                (String)row["CreatorID"]
            );

            asset.Description = (String) row["Description"];
            asset.Local = Convert.ToBoolean(row["Local"]);
            asset.Temporary = Convert.ToBoolean(row["Temporary"]);
            asset.Flags = (AssetFlags)Convert.ToInt32(row["asset_flags"]);
            asset.Data = (byte[])row["Data"];
            return asset;
        }

        private static AssetMetadata buildAssetMetadata(IDataReader row)
        {
            AssetMetadata metadata = new AssetMetadata();

            metadata.FullID = new UUID((string) row["UUID"]);
            metadata.Name = (string) row["Name"];
            metadata.Description = (string) row["Description"];
            metadata.Type = Convert.ToSByte(row["Type"]);
            metadata.Temporary = Convert.ToBoolean(row["Temporary"]); // Not sure if this is correct.
            metadata.Flags = (AssetFlags)Convert.ToInt32(row["asset_flags"]);
            metadata.CreatorID = row["CreatorID"].ToString();

            // Current SHA1s are not stored/computed.
            metadata.SHA1 = new byte[] {};

            return metadata;
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

            lock (this)
            {
                using (SqliteCommand cmd = new SqliteCommand(SelectAssetMetadataSQL, m_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":start", start));
                    cmd.Parameters.Add(new SqliteParameter(":count", count));

                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            AssetMetadata metadata = buildAssetMetadata(reader);
                            retList.Add(metadata);
                        }
                    }
                }
            }

            return retList;
        }

        /***********************************************************************
         *
         *  Database Binding functions
         *
         *  These will be db specific due to typing, and minor differences
         *  in databases.
         *
         **********************************************************************/

        #region IPlugin interface

        /// <summary>
        ///
        /// </summary>
        override public string Version
        {
            get
            {
                Module module = GetType().Module;
                // string dllName = module.Assembly.ManifestModule.Name;
                Version dllVersion = module.Assembly.GetName().Version;

                return
                    string.Format("{0}.{1}.{2}.{3}", dllVersion.Major, dllVersion.Minor, dllVersion.Build,
                                  dllVersion.Revision);
            }
        }

        /// <summary>
        /// Initialise the AssetData interface using default URI
        /// </summary>
        override public void Initialise()
        {
            Initialise("URI=file:Asset.db,version=3");
        }

        /// <summary>
        /// Name of this DB provider
        /// </summary>
        override public string Name
        {
            get { return "SQLite Asset storage engine"; }
        }

        // TODO: (AlexRa): one of these is to be removed eventually (?)

        /// <summary>
        /// Delete an asset from database
        /// </summary>
        /// <param name="uuid"></param>
        public bool DeleteAsset(UUID uuid)
        {
            lock (this)
            {
                using (SqliteCommand cmd = new SqliteCommand(DeleteAssetSQL, m_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":UUID", uuid.ToString()));
                    cmd.ExecuteNonQuery();
                }
            }

            return true;
        }

        public override bool Delete(string id)
        {
            UUID assetID;

            if (!UUID.TryParse(id, out assetID))
                return false;

            return DeleteAsset(assetID);
        }

        #endregion
    }
}
