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
using OpenMetaverse;
using log4net;

namespace OpenSim.Grid.AssetInventoryServer.Plugins
{
    public class NullMetricsPlugin : IMetricsProvider
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        //private AssetInventoryServer m_server;

        public NullMetricsPlugin()
        {
        }

        #region IMetricsProvider implementation

        public void LogAssetMetadataFetch(string extension, BackendResponse response, UUID assetID, DateTime time)
        {
            m_log.DebugFormat("[{0}]: AssetMetadataFetch(): AssetID: {1}, Response: {2}", extension, assetID, response);
        }

        public void LogAssetDataFetch(string extension, BackendResponse response, UUID assetID, int dataSize, DateTime time)
        {
            m_log.DebugFormat("[{0}]: AssetDataFetch(): AssetID: {1}, DataSize: {2}, Response: {3}", extension, assetID,
                dataSize, response);
        }

        public void LogAssetCreate(string extension, BackendResponse response, UUID assetID, int dataSize, DateTime time)
        {
            m_log.DebugFormat("[{0}]: AssetCreate(): AssetID: {1}, DataSize: {2}, Response: {3}", extension, assetID,
                dataSize, response);
        }

        public void LogInventoryFetch(string extension, BackendResponse response, Uri owner, UUID objID, bool folder, DateTime time)
        {
            m_log.DebugFormat("[{0}]: InventoryFetch(): ObjID: {1}, Folder: {2}, OwnerID: {3}, Response: {4}", extension,
                objID, folder, owner, response);
        }

        public void LogInventoryFetchFolderContents(string extension, BackendResponse response, Uri owner, UUID folderID, DateTime time)
        {
            m_log.DebugFormat("[{0}]: InventoryFetchFolderContents(): FolderID: {1}, OwnerID: {2}, Response: {3}", extension,
                folderID, owner, response);
        }

        public void LogInventoryFetchFolderList(string extension, BackendResponse response, Uri owner, DateTime time)
        {
            m_log.DebugFormat("[{0}]: InventoryFetchFolderList(): OwnerID: {1}, Response: {2}", extension,
                owner, response);
        }

        public void LogInventoryFetchInventory(string extension, BackendResponse response, Uri owner, DateTime time)
        {
            m_log.DebugFormat("[{0}]: InventoryFetchInventory(): OwnerID: {1}, Response: {2}", extension,
                owner, response);
        }

        public void LogInventoryFetchActiveGestures(string extension, BackendResponse response, Uri owner, DateTime time)
        {
            m_log.DebugFormat("[{0}]: InventoryFetchActiveGestures(): OwnerID: {1}, Response: {2}", extension,
                owner, response);
        }

        public void LogInventoryCreate(string extension, BackendResponse response, Uri owner, bool folder, DateTime time)
        {
            m_log.DebugFormat("[{0}]: InventoryCreate(): OwnerID: {1}, Response: {2}", extension,
                owner, response);
        }

        public void LogInventoryCreateInventory(string extension, BackendResponse response, DateTime time)
        {
            m_log.DebugFormat("[{0}]: InventoryCreateInventory(): Response: {1}", extension,
                response);
        }

        public void LogInventoryDelete(string extension, BackendResponse response, Uri owner, UUID objID, bool folder, DateTime time)
        {
            m_log.DebugFormat("[{0}]: InventoryDelete(): OwnerID: {1}, Folder: {2}, Response: {3}", extension,
                owner, folder, response);
        }

        public void LogInventoryPurgeFolder(string extension, BackendResponse response, Uri owner, UUID folderID, DateTime time)
        {
            m_log.DebugFormat("[{0}]: InventoryPurgeFolder(): OwnerID: {1}, FolderID: {2}, Response: {3}", extension,
                owner, folderID, response);
        }

        #endregion IMetricsProvider implementation

        #region IPlugin implementation

        public void Initialise(AssetInventoryServer server)
        {
            //m_server = server;
        }

        /// <summary>
        /// <para>Initialises metrics interface</para>
        /// </summary>
        public void Initialise()
        {
            m_log.Info("[NULLMETRICS]: Null metrics loaded.");
        }

        public void Dispose()
        {
        }

        public string Version
        {
            // TODO: this should be something meaningful and not hardcoded?
            get { return "0.1"; }
        }

        public string Name
        {
            get { return "NullMetrics"; }
        }

        #endregion IPlugin implementation
    }
}
