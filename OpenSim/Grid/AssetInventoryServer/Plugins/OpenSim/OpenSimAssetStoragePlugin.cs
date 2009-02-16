/*
 * Copyright (c) 2008 Intel Corporation
 * All rights reserved.
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 *
 * -- Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * -- Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * -- Neither the name of the Intel Corporation nor the names of its
 *    contributors may be used to endorse or promote products derived from
 *    this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
 * PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE INTEL OR ITS
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Reflection;
using System.Data;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;
using Nini.Config;
using log4net;

namespace OpenSim.Grid.AssetInventoryServer.Plugins.OpenSim
{
    public class OpenSimAssetStoragePlugin : IAssetStorageProvider
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        const string EXTENSION_NAME = "OpenSimAssetStorage"; // Used in metrics reporting

        private AssetInventoryServer m_server;
        private IAssetDataPlugin m_assetProvider;
        private IConfig m_openSimConfig;

        public OpenSimAssetStoragePlugin()
        {
        }

        #region IAssetStorageProvider implementation

        public BackendResponse TryFetchMetadata(UUID assetID, out AssetMetadata metadata)
        {
            metadata = null;
            BackendResponse ret;

            AssetBase asset = m_assetProvider.FetchAsset(assetID);

            if (asset == null) ret = BackendResponse.NotFound;
            else
            {
                metadata = asset.Metadata;
                ret = BackendResponse.Success;
            }

            m_server.MetricsProvider.LogAssetMetadataFetch(EXTENSION_NAME, ret, assetID, DateTime.Now);
            return ret;
        }

        public BackendResponse TryFetchData(UUID assetID, out byte[] assetData)
        {
            assetData = null;
            BackendResponse ret;

            AssetBase asset = m_assetProvider.FetchAsset(assetID);

            if (asset == null) ret = BackendResponse.NotFound;
            else
            {
                assetData = asset.Data;
                ret = BackendResponse.Success;
            }

            m_server.MetricsProvider.LogAssetDataFetch(EXTENSION_NAME, ret, assetID, (assetData != null ? assetData.Length : 0), DateTime.Now);
            return ret;
        }

        public BackendResponse TryFetchDataMetadata(UUID assetID, out AssetBase asset)
        {
            asset = m_assetProvider.FetchAsset(assetID);

            if (asset == null) return BackendResponse.NotFound;

            return BackendResponse.Success;
        }

        public BackendResponse TryCreateAsset(AssetBase asset, out UUID assetID)
        {
            assetID = asset.FullID = UUID.Random();
            return TryCreateAsset(asset);
        }

        public BackendResponse TryCreateAsset(AssetBase asset)
        {
            BackendResponse ret;

            m_assetProvider.CreateAsset(asset);
            ret = BackendResponse.Success;

            m_server.MetricsProvider.LogAssetCreate(EXTENSION_NAME, ret, asset.FullID, asset.Data.Length, DateTime.Now);
            return ret;
        }

        public int ForEach(Action<AssetMetadata> action, int start, int count)
        {
            int rowCount = 0;

            //using (MySqlConnection dbConnection = new MySqlConnection(m_openSimConfig.GetString("asset_database_connect")))
            //{
            //    MySqlDataReader reader;

            //    try
            //    {
            //        dbConnection.Open();

            //        MySqlCommand command = dbConnection.CreateCommand();
            //        command.CommandText = String.Format("SELECT name,description,assetType,temporary,data,id FROM assets LIMIT {0}, {1}",
            //            start, count);
            //        reader = command.ExecuteReader();
            //    }
            //    catch (MySqlException ex)
            //    {
            //        m_log.Error("Connection to MySQL backend failed: " + ex.Message);
            //        return 0;
            //    }

            //    while (reader.Read())
            //    {
            //        Metadata metadata = new Metadata();
            //        metadata.CreationDate = OpenMetaverse.Utils.Epoch;
            //        metadata.Description = reader.GetString(1);
            //        metadata.ID = UUID.Parse(reader.GetString(5));
            //        metadata.Name = reader.GetString(0);
            //        metadata.SHA1 = OpenMetaverse.Utils.SHA1((byte[])reader.GetValue(4));
            //        metadata.Temporary = reader.GetBoolean(3);
            //        metadata.ContentType = Utils.SLAssetTypeToContentType(reader.GetInt32(2));

            //        action(metadata);
            //        ++rowCount;
            //    }

            //    reader.Close();
            //}

            return rowCount;
        }

        #endregion IAssetStorageProvider implementation

        #region IPlugin implementation

        public void Initialise(AssetInventoryServer server)
        {
            m_server = server;
            m_openSimConfig = server.ConfigFile.Configs["OpenSim"];

            try
            {
                m_assetProvider = DataPluginFactory.LoadDataPlugin<IAssetDataPlugin>(m_openSimConfig.GetString("asset_database_provider"),
                                                                                     m_openSimConfig.GetString("asset_database_connect"));
                if (m_assetProvider == null)
                {
                    m_log.Error("[ASSET]: Failed to load a database plugin, server halting.");
                    Environment.Exit(-1);
                }
                else
                    m_log.InfoFormat("[ASSET]: Loaded storage backend: {0}", Version);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[ASSET]: Failure loading data plugin: {0}", e.ToString());
            }
        }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        public void Initialise()
        {
            m_log.InfoFormat("[ASSET]: {0} cannot be default-initialized!", Name);
            throw new PluginNotInitialisedException(Name);
        }

        public void Dispose()
        {
        }

        public string Version
        {
            get { return m_assetProvider.Version; }
        }

        public string Name
        {
            get { return "AssetInventoryServer OpenSim asset storage provider"; }
        }

        #endregion IPlugin implementation
    }
}
