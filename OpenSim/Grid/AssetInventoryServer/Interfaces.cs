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
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Grid.AssetInventoryServer
{
    /// <summary>
    /// Response from a call to a backend provider
    /// </summary>
    public enum BackendResponse
    {
        /// <summary>The call succeeded</summary>
        Success,
        /// <summary>The resource requested was not found</summary>
        NotFound,
        /// <summary>A server failure prevented the call from
        /// completing</summary>
        Failure
    }

    public class AssetInventoryServerPluginInitialiser : PluginInitialiserBase
    {
        private AssetInventoryServer server;

        public AssetInventoryServerPluginInitialiser(AssetInventoryServer server)
        {
            this.server = server;
        }

        public override void Initialise(IPlugin plugin)
        {
            IAssetInventoryServerPlugin p = plugin as IAssetInventoryServerPlugin;
            p.Initialise (server);
        }
    }

    #region Interfaces

    public interface IAssetInventoryServerPlugin : IPlugin
    {
        void Initialise(AssetInventoryServer server);
    }

    public interface IAssetStorageProvider : IAssetInventoryServerPlugin
    {
        BackendResponse TryFetchMetadata(UUID assetID, out AssetMetadata metadata);
        BackendResponse TryFetchData(UUID assetID, out byte[] assetData);
        BackendResponse TryFetchDataMetadata(UUID assetID, out AssetBase asset);
        BackendResponse TryCreateAsset(AssetBase asset);
        BackendResponse TryCreateAsset(AssetBase asset, out UUID assetID);
        int ForEach(Action<AssetMetadata> action, int start, int count);
    }

    public interface IInventoryStorageProvider : IAssetInventoryServerPlugin
    {
        BackendResponse TryFetchItem(Uri owner, UUID itemID, out InventoryItemBase item);
        BackendResponse TryFetchFolder(Uri owner, UUID folderID, out InventoryFolderWithChildren folder);
        BackendResponse TryFetchFolderContents(Uri owner, UUID folderID, out InventoryCollection contents);
        BackendResponse TryFetchFolderList(Uri owner, out List<InventoryFolderWithChildren> folders);
        BackendResponse TryFetchInventory(Uri owner, out InventoryCollection inventory);

        BackendResponse TryFetchActiveGestures(Uri owner, out List<InventoryItemBase> gestures);

        BackendResponse TryCreateItem(Uri owner, InventoryItemBase item);
        BackendResponse TryCreateFolder(Uri owner, InventoryFolderWithChildren folder);
        BackendResponse TryCreateInventory(Uri owner, InventoryFolderWithChildren rootFolder);

        BackendResponse TryDeleteItem(Uri owner, UUID itemID);
        BackendResponse TryDeleteFolder(Uri owner, UUID folderID);
        BackendResponse TryPurgeFolder(Uri owner, UUID folderID);
    }

    public interface IAuthenticationProvider : IAssetInventoryServerPlugin
    {
        void AddIdentifier(UUID authToken, Uri identifier);
        bool RemoveIdentifier(UUID authToken);
        bool TryGetIdentifier(UUID authToken, out Uri identifier);
    }

    public interface IAuthorizationProvider : IAssetInventoryServerPlugin
    {
        bool IsMetadataAuthorized(UUID authToken, UUID assetID);
        /// <summary>
        /// Authorizes access to the data for an asset. Access to asset data
        /// also implies access to the metadata for that asset
        /// </summary>
        /// <param name="authToken">Authentication token to check for access</param>
        /// <param name="assetID">ID of the requested asset</param>
        /// <returns>True if access is granted, otherwise false</returns>
        bool IsDataAuthorized(UUID authToken, UUID assetID);
        bool IsCreateAuthorized(UUID authToken);

        bool IsInventoryReadAuthorized(UUID authToken, Uri owner);
        bool IsInventoryWriteAuthorized(UUID authToken, Uri owner);
    }

    public interface IMetricsProvider : IAssetInventoryServerPlugin
    {
        void LogAssetMetadataFetch(string extension, BackendResponse response, UUID assetID, DateTime time);
        void LogAssetDataFetch(string extension, BackendResponse response, UUID assetID, int dataSize, DateTime time);
        void LogAssetCreate(string extension, BackendResponse response, UUID assetID, int dataSize, DateTime time);

        void LogInventoryFetch(string extension, BackendResponse response, Uri owner, UUID objID, bool folder, DateTime time);
        void LogInventoryFetchFolderContents(string extension, BackendResponse response, Uri owner, UUID folderID, DateTime time);
        void LogInventoryFetchFolderList(string extension, BackendResponse response, Uri owner, DateTime time);
        void LogInventoryFetchInventory(string extension, BackendResponse response, Uri owner, DateTime time);
        void LogInventoryFetchActiveGestures(string extension, BackendResponse response, Uri owner, DateTime time);
        void LogInventoryCreate(string extension, BackendResponse response, Uri owner, bool folder, DateTime time);
        void LogInventoryCreateInventory(string extension, BackendResponse response, DateTime time);
        void LogInventoryDelete(string extension, BackendResponse response, Uri owner, UUID objID, bool folder, DateTime time);
        void LogInventoryPurgeFolder(string extension, BackendResponse response, Uri owner, UUID folderID, DateTime time);
    }

    #endregion Interfaces
}
