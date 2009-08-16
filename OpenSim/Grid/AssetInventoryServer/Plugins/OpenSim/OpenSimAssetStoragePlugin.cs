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
using System.Data;
using System.Collections.Generic;
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

            AssetBase asset = m_assetProvider.GetAsset(assetID);

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

            AssetBase asset = m_assetProvider.GetAsset(assetID);

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
            asset = m_assetProvider.GetAsset(assetID);

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

            m_assetProvider.StoreAsset(asset);
            ret = BackendResponse.Success;

            m_server.MetricsProvider.LogAssetCreate(EXTENSION_NAME, ret, asset.FullID, asset.Data.Length, DateTime.Now);
            return ret;
        }

        public int ForEach(Action<AssetMetadata> action, int start, int count)
        {
            int rowCount = 0;

            foreach (AssetMetadata metadata in m_assetProvider.FetchAssetMetadataSet(start, count))
            {
                // We set the ContentType here because Utils is only in
                // AssetInventoryServer. This should be moved to the DB
                // backends when the equivalent of SLAssetTypeToContentType is
                // in OpenSim.Framework or similar.
                metadata.ContentType = Utils.SLAssetTypeToContentType(metadata.Type);

                action(metadata);
                ++rowCount;
            }

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
                    m_log.Error("[OPENSIMASSETSTORAGE]: Failed to load a database plugin, server halting.");
                    Environment.Exit(-1);
                }
                else
                    m_log.InfoFormat("[OPENSIMASSETSTORAGE]: Loaded storage backend: {0}", Version);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[OPENSIMASSETSTORAGE]: Failure loading data plugin: {0}", e.ToString());
                throw new PluginNotInitialisedException(Name);
            }
        }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        public void Initialise()
        {
            m_log.InfoFormat("[OPENSIMASSETSTORAGE]: {0} cannot be default-initialized!", Name);
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
            get { return "OpenSimAssetStorage"; }
        }

        #endregion IPlugin implementation
    }
}
