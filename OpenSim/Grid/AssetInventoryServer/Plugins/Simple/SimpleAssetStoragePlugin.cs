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
using System.Collections.Generic;
using System.Net;
using System.IO;
using ExtensionLoader;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;

namespace OpenSim.Grid.AssetInventoryServer.Plugins.Simple
{
    public class SimpleAssetStoragePlugin : IAssetStorageProvider
    {
        const string EXTENSION_NAME = "SimpleAssetStorage"; // Used in metrics reporting
        const string DEFAULT_DATA_DIR = "SimpleAssets";
        const string TEMP_DATA_DIR = "SimpleAssetsTemp";

        AssetInventoryServer server;
        Dictionary<UUID, Metadata> metadataStorage;
        Dictionary<UUID, string> filenames;

        public SimpleAssetStoragePlugin()
        {
        }

        #region Required Interfaces

        public BackendResponse TryFetchMetadata(UUID assetID, out Metadata metadata)
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
                    Logger.Log.ErrorFormat("Failed reading data for asset {0} from {1}: {2}", assetID, filename, ex.Message);
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

        public BackendResponse TryFetchDataMetadata(UUID assetID, out Metadata metadata, out byte[] assetData)
        {
            metadata = null;
            assetData = null;
            string filename;
            BackendResponse ret;

            if (metadataStorage.TryGetValue(assetID, out metadata) &&
                    filenames.TryGetValue(assetID, out filename))
            {
                try
                {
                    assetData = File.ReadAllBytes(filename);
                    ret = BackendResponse.Success;
                }
                catch (Exception ex)
                {
                    Logger.Log.ErrorFormat("Failed reading data for asset {0} from {1}: {2}", assetID, filename, ex.Message);
                    ret = BackendResponse.Failure;
                }
            }
            else
            {
                ret = BackendResponse.NotFound;
            }

            server.MetricsProvider.LogAssetMetadataFetch(EXTENSION_NAME, ret, assetID, DateTime.Now);
            server.MetricsProvider.LogAssetDataFetch(EXTENSION_NAME, ret, assetID, (assetData != null ? assetData.Length : 0), DateTime.Now);
            return ret;
        }

        public BackendResponse TryCreateAsset(Metadata metadata, byte[] assetData, out UUID assetID)
        {
            assetID = metadata.ID = UUID.Random();
            return TryCreateAsset(metadata, assetData);
        }

        public BackendResponse TryCreateAsset(Metadata metadata, byte[] assetData)
        {
            BackendResponse ret;

            string path;
            string filename = String.Format("{0}.{1}", metadata.ID, Utils.ContentTypeToExtension(metadata.ContentType));

            if (metadata.Temporary)
                path = Path.Combine(TEMP_DATA_DIR, filename);
            else
                path = Path.Combine(DEFAULT_DATA_DIR, filename);

            try
            {
                File.WriteAllBytes(path, assetData);
                lock (filenames) filenames[metadata.ID] = path;

                // Set the creation date to right now
                metadata.CreationDate = DateTime.Now;

                lock (metadataStorage)
                    metadataStorage[metadata.ID] = metadata;

                ret = BackendResponse.Success;
            }
            catch (Exception ex)
            {
                Logger.Log.ErrorFormat("Failed writing data for asset {0} to {1}: {2}", metadata.ID, filename, ex.Message);
                ret = BackendResponse.Failure;
            }

            server.MetricsProvider.LogAssetCreate(EXTENSION_NAME, ret, metadata.ID, assetData.Length, DateTime.Now);
            return ret;
        }

        public int ForEach(Action<Metadata> action, int start, int count)
        {
            int rowCount = 0;

            lock (metadataStorage)
            {
                foreach (Metadata metadata in metadataStorage.Values)
                {
                    action(metadata);
                    ++rowCount;
                }
            }

            return rowCount;
        }

        #endregion Required Interfaces

        #region IPlugin implementation

        public void Initialise(AssetInventoryServer server)
        {
            this.server = server;

            metadataStorage = new Dictionary<UUID, Metadata>();
            filenames = new Dictionary<UUID, string>();

            LoadFiles(DEFAULT_DATA_DIR, false);
            LoadFiles(TEMP_DATA_DIR, true);

            Logger.Log.InfoFormat("Initialized the store index with metadata for {0} assets",
                metadataStorage.Count);
        }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        public void Initialise()
        {
            Logger.Log.InfoFormat("[ASSET]: {0} cannot be default-initialized!", Name);
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
            get { return "AssetInventoryServer Simple asset storage provider"; }
        }

        #endregion IPlugin implementation

        public void WipeTemporary()
        {
            if (Directory.Exists(TEMP_DATA_DIR))
            {
                try { Directory.Delete(TEMP_DATA_DIR); }
                catch (Exception ex) { Logger.Log.Error(ex.Message); }
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
                    Logger.Log.Warn(ex.Message);
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

                        Metadata metadata = new Metadata();
                        metadata.CreationDate = File.GetCreationTime(filename);
                        metadata.Description = String.Empty;
                        metadata.ID = SimpleUtils.ParseUUIDFromFilename(filename);
                        metadata.Name = SimpleUtils.ParseNameFromFilename(filename);
                        metadata.SHA1 = OpenMetaverse.Utils.SHA1(data);
                        metadata.Temporary = false;
                        metadata.ContentType = Utils.ExtensionToContentType(Path.GetExtension(filename).TrimStart('.'));

                        // Store the loaded data
                        metadataStorage[metadata.ID] = metadata;
                        filenames[metadata.ID] = filename;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log.Warn(ex.Message);
                }
            }
        }
    }
}
