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

using OpenMetaverse;
using OpenMetaverse.Packets;

namespace OpenSim.Framework
{
    public delegate void AssetRequestCallback(UUID assetId, AssetBase asset);

    /// <summary>
    /// Interface to the local asset cache.  This is the mechanism through which assets can be added and requested.
    /// </summary>
    public interface IAssetCache :  IPlugin
    {
        /// <value>
        /// The 'server' from which assets can be requested and to which assets are persisted.
        /// </value>
       
        void Initialise(ConfigSettings cs);

        /// <summary>
        /// Report statistical data to the log.
        /// </summary>
        void ShowState();
        
        /// <summary>
        /// Clear the asset cache.
        /// </summary>
        void Clear();
        
        /// <summary>
        /// Get an asset only if it's already in the cache.
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="asset"></param>
        /// <returns>true if the asset was in the cache, false if it was not</returns>
        bool TryGetCachedAsset(UUID assetID, out AssetBase asset);
        
        /// <summary>
        /// Asynchronously retrieve an asset.
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="callback">
        /// <param name="isTexture"></param>
        /// A callback invoked when the asset has either been found or not found.
        /// If the asset was found this is called with the asset UUID and the asset data
        /// If the asset was not found this is still called with the asset UUID but with a null asset data reference</param>
        void GetAsset(UUID assetID, AssetRequestCallback callback, bool isTexture);
        
        /// <summary>
        /// Synchronously retreive an asset.  If the asset isn't in the cache, a request will be made to the persistent store to
        /// load it into the cache.
        /// </summary>
        ///
        /// XXX We'll keep polling the cache until we get the asset or we exceed
        /// the allowed number of polls.  This isn't a very good way of doing things since a single thread
        /// is processing inbound packets, so if the asset server is slow, we could block this for up to
        /// the timeout period.  Whereever possible we want to use the asynchronous callback GetAsset()
        ///
        /// <param name="assetID"></param>
        /// <param name="isTexture"></param>
        /// <returns>null if the asset could not be retrieved</returns>
        AssetBase GetAsset(UUID assetID, bool isTexture);
        
        /// <summary>
        /// Add an asset to both the persistent store and the cache.
        /// </summary>
        /// <param name="asset"></param>
        void AddAsset(AssetBase asset);
        
        /// <summary>
        /// Expire an asset from the cache
        /// </summary>
        /// Allows you to clear a specific asset by uuid out
        /// of the asset cache.  This is needed because the osdynamic
        /// texture code grows the asset cache without bounds.  The
        /// real solution here is a much better cache archicture, but
        /// this is a stop gap measure until we have such a thing.
        void ExpireAsset(UUID assetID);
        
        /// <summary>
        /// Handle an asset request from the client.  The result will be sent back asynchronously.
        /// </summary>
        /// <param name="userInfo"></param>
        /// <param name="transferRequest"></param>
        void AddAssetRequest(IClientAPI userInfo, TransferRequestPacket transferRequest);
    }

}
