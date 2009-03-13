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
using System.IO;
using OpenMetaverse;
using OpenSim.Framework;
using log4net;

namespace OpenSim.Grid.AssetInventoryServer.Plugins.Simple
{
    public class SimpleAssetStoragePlugin : IAssetStorageProvider
    {
        const string EXTENSION_NAME = "SimpleAssetStorage"; // Used in metrics reporting
        const string DEFAULT_DATA_DIR = "SimpleAssets";
        const string TEMP_DATA_DIR = "SimpleAssetsTemp";

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        AssetInventoryServer server;
        Dictionary<UUID, AssetMetadata> metadataStorage;
        Dictionary<UUID, string> filenames;

        public SimpleAssetStoragePlugin()
        {
        }

        #region Required Interfaces

        public BackendResponse TryFetchMetadata(UUID assetID, out AssetMetadata metadata)
        {
            metadata = null;
            BackendResponse ret;

            if (metadataStorage.TryGetValue(assetID, out metadata))
                ret = BackendResponse.Success;
            else
                ret = BackendResponse.NotFound;

            server.MetricsProvider.LogAssetMetadataFetch(EXTENSION_NAME, ret, assetID, DateTime.Now);
            return ret;
        }

        public BackendResponse TryFetchData(UUID assetID, out byte[] assetData)
        {
            assetData = null;
            string filename;
            BackendResponse ret;

            if (filenames.TryGetValue(assetID, out filename))
            {
                try
                {
                    assetData = File.ReadAllBytes(filename);
                    ret = BackendResponse.Success;
                }
                catch (Exception ex)
                {
                    m_log.ErrorFormat("[SIMPLEASSETSTORAGE]: Failed reading data for asset {0} from {1}: {2}", assetID, filename, ex.Message);
                    ret = BackendResponse.Failure;
                }
            }
            else
            {
                ret = BackendResponse.NotFound;
            }

            server.MetricsProvider.LogAssetDataFetch(EXTENSION_NAME, ret, assetID, (assetData != null ? assetData.Length : 0), DateTime.Now);
            return ret;
        }

        public BackendResponse TryFetchDataMetadata(UUID assetID, out AssetBase asset)
        {
            asset = new AssetBase();
            AssetMetadata metadata = asset.Metadata;

            string filename;
            BackendResponse ret;

            if (metadataStorage.TryGetValue(assetID, out metadata) &&
                    filenames.TryGetValue(assetID, out filename))
            {
                try
                {
                    asset.Data = File.ReadAllBytes(filename);
                    ret = BackendResponse.Success;
                }
                catch (Exception ex)
                {
                    m_log.ErrorFormat("[SIMPLEASSETSTORAGE]: Failed reading data for asset {0} from {1}: {2}", assetID, filename, ex.Message);
                    ret = BackendResponse.Failure;
                }

                asset.Type = (sbyte) Utils.ContentTypeToSLAssetType(metadata.ContentType);
                asset.Local = false;
            }
            else
            {
                asset = null;
                ret = BackendResponse.NotFound;
            }

            server.MetricsProvider.LogAssetMetadataFetch(EXTENSION_NAME, ret, assetID, DateTime.Now);
            server.MetricsProvider.LogAssetDataFetch(EXTENSION_NAME, ret, assetID, (asset != null && asset.Data != null ? asset.Data.Length : 0), DateTime.Now);
            return ret;
        }

        public BackendResponse TryCreateAsset(AssetBase asset, out UUID assetID)
        {
            assetID = asset.FullID = UUID.Random();
            return TryCreateAsset(asset);
        }

        public BackendResponse TryCreateAsset(AssetBase asset)
        {
            BackendResponse ret;
            AssetMetadata metadata = asset.Metadata;

            string path;
            string filename = String.Format("{0}.{1}", asset.FullID, Utils.ContentTypeToExtension(metadata.ContentType));

            if (asset.Temporary)
                path = Path.Combine(TEMP_DATA_DIR, filename);
            else
                path = Path.Combine(DEFAULT_DATA_DIR, filename);

            try
            {
                File.WriteAllBytes(path, asset.Data);
                lock (filenames) filenames[asset.FullID] = path;

                // Set the creation date to right now
                metadata.CreationDate = DateTime.Now;

                lock (metadataStorage)
                    metadataStorage[asset.FullID] = metadata;

                ret = BackendResponse.Success;
            }
            catch (Exception ex)
            {
                m_log.ErrorFormat("[SIMPLEASSETSTORAGE]: Failed writing data for asset {0} to {1}: {2}", asset.FullID, filename, ex.Message);
                ret = BackendResponse.Failure;
            }

            server.MetricsProvider.LogAssetCreate(EXTENSION_NAME, ret, asset.FullID, asset.Data.Length, DateTime.Now);
            return ret;
        }

        public int ForEach(Action<AssetMetadata> action, int start, int count)
        {
            int rowCount = 0;

            //lock (metadataStorage)
            //{
            //    foreach (Metadata metadata in metadataStorage.Values)
            //    {
            //        action(metadata);
            //        ++rowCount;
            //    }
            //}

            return rowCount;
        }

        #endregion Required Interfaces

        #region IPlugin implementation

        public void Initialise(AssetInventoryServer server)
        {
            this.server = server;

            metadataStorage = new Dictionary<UUID, AssetMetadata>();
            filenames = new Dictionary<UUID, string>();

            LoadFiles(DEFAULT_DATA_DIR, false);
            LoadFiles(TEMP_DATA_DIR, true);

            m_log.InfoFormat("[SIMPLEASSETSTORAGE]: Initialized the store index with metadata for {0} assets",
                metadataStorage.Count);
        }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        public void Initialise()
        {
            m_log.InfoFormat("[SIMPLEASSETSTORAGE]: {0} cannot be default-initialized!", Name);
            throw new PluginNotInitialisedException(Name);
        }

        public void Dispose()
        {
            WipeTemporary();
        }

        public string Version
        {
            // TODO: this should be something meaningful and not hardcoded?
            get { return "0.1"; }
        }

        public string Name
        {
            get { return "SimpleAssetStorage"; }
        }

        #endregion IPlugin implementation

        public void WipeTemporary()
        {
            if (Directory.Exists(TEMP_DATA_DIR))
            {
                try { Directory.Delete(TEMP_DATA_DIR); }
                catch (Exception ex) { m_log.Error("[SIMPLEASSETSTORAGE]: " + ex.Message); }
            }
        }

        void LoadFiles(string folder, bool temporary)
        {
            // Try to create the directory if it doesn't already exist
            if (!Directory.Exists(folder))
            {
                try { Directory.CreateDirectory(folder); }
                catch (Exception ex)
                {
                    m_log.Warn("[SIMPLEASSETSTORAGE]: " + ex.Message);
                    return;
                }
            }

            lock (metadataStorage)
            {
                try
                {
                    string[] assets = Directory.GetFiles(folder);

                    for (int i = 0; i < assets.Length; i++)
                    {
                        string filename = assets[i];
                        byte[] data = File.ReadAllBytes(filename);

                        AssetMetadata metadata = new AssetMetadata();
                        metadata.CreationDate = File.GetCreationTime(filename);
                        metadata.Description = String.Empty;
                        metadata.FullID = SimpleUtils.ParseUUIDFromFilename(filename);
                        metadata.Name = SimpleUtils.ParseNameFromFilename(filename);
                        metadata.SHA1 = OpenMetaverse.Utils.SHA1(data);
                        metadata.Temporary = false;
                        metadata.ContentType = Utils.ExtensionToContentType(Path.GetExtension(filename).TrimStart('.'));

                        // Store the loaded data
                        metadataStorage[metadata.FullID] = metadata;
                        filenames[metadata.FullID] = filename;
                    }
                }
                catch (Exception ex)
                {
                    m_log.Warn("[SIMPLEASSETSTORAGE]: " + ex.Message);
                }
            }
        }
    }
}
