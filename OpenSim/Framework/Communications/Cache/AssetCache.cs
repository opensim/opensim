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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Reflection;
using System.Threading;
using GlynnTucker.Cache;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework.Statistics;

namespace OpenSim.Framework.Communications.Cache
{
    /// <summary>
    /// Manages local cache of assets and their sending to viewers.
    /// </summary>
    ///
    /// This class actually encapsulates two largely separate mechanisms.  One mechanism fetches assets either
    /// synchronously or async and passes the data back to the requester.  The second mechanism fetches assets and
    /// sends packetised data directly back to the client.  The only point where they meet is AssetReceived() and
    /// AssetNotFound(), which means they do share the same asset and texture caches.
    public class AssetCache : IAssetCache
    {
        #region IPlugin

        /// <summary>
        /// The methods and properties in this section are needed to
        /// support the IPlugin interface. They cann all be overridden
        /// as needed by a derived class.
        /// </summary>

        public virtual string Name
        {
            get { return "OpenSim.Framework.Communications.Cache.AssetCache"; }
        }

        public virtual string Version
        {
            get { return "1.0"; }
        }

        public virtual void Initialise()
        {
            m_log.Debug("[ASSET CACHE]: Asset cache null initialisation");
        }

        public virtual void Initialise(IAssetServer assetServer)
        {
            m_log.Debug("[ASSET CACHE]: Asset cache server-specified initialisation");
            m_log.InfoFormat("[ASSET CACHE]: Asset cache initialisation [{0}/{1}]", Name, Version);

            Initialize();

            m_assetServer = assetServer;
            m_assetServer.SetReceiver(this);

            Thread assetCacheThread = new Thread(RunAssetManager);
            assetCacheThread.Name = "AssetCacheThread";
            assetCacheThread.IsBackground = true;
            assetCacheThread.Start();
            ThreadTracker.Add(assetCacheThread);
        }

        public virtual void Initialise(ConfigSettings settings, IAssetServer assetServer)
        {
            m_log.Debug("[ASSET CACHE]: Asset cache configured initialisation");
            Initialise(assetServer);
        }

        public AssetCache()
        {
            m_log.Debug("[ASSET CACHE]: Asset cache (plugin constructor)");
        }

        public void Dispose()
        {
        }

        #endregion

        protected ICache m_memcache = new SimpleMemoryCache();

        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <value>
        /// Assets requests which are waiting for asset server data.  This includes texture requests
        /// </value>
        private Dictionary<UUID, AssetRequest> RequestedAssets;

        /// <value>
        /// Asset requests with data which are ready to be sent back to requesters.  This includes textures.
        /// </value>
        private List<AssetRequest> AssetRequests;

        /// <value>
        /// Until the asset request is fulfilled, each asset request is associated with a list of requesters
        /// </value>
        private Dictionary<UUID, AssetRequestsList> RequestLists;        

        public IAssetServer AssetServer
        {
            get { return m_assetServer; }
        }
        private IAssetServer m_assetServer;        

        public void ShowState()
        {
            m_log.InfoFormat("Memcache:{0}   RequestLists:{1}",
                m_memcache.Count,
              //   AssetRequests.Count,
              //    RequestedAssets.Count,
                RequestLists.Count);
        }

        public void Clear()
        {
            m_log.Info("[ASSET CACHE]: Clearing Asset cache");

            if (StatsManager.SimExtraStats != null)
                StatsManager.SimExtraStats.ClearAssetCacheStatistics();

            Initialize();
        }

        /// <summary>
        /// Initialize the cache.
        /// </summary>
        private void Initialize()
        {
            AssetRequests = new List<AssetRequest>();

            RequestedAssets = new Dictionary<UUID, AssetRequest>();
            RequestLists = new Dictionary<UUID, AssetRequestsList>();
        }

        /// <summary>
        /// Constructor.  Initialize will need to be called separately.
        /// </summary>
        /// <param name="assetServer"></param>
        public AssetCache(IAssetServer assetServer)
        {
            m_log.Info("[ASSET CACHE]: Asset cache direct constructor");
            Initialise(assetServer);
        }

        /// <summary>
        /// Process the asset queue which holds data which is packeted up and sent
        /// directly back to the client.
        /// </summary>
        private void RunAssetManager()
        {
            while (true)
            {
                try
                {
                    ProcessAssetQueue();
                    Thread.Sleep(500);
                }
                catch (Exception e)
                {
                    m_log.Error("[ASSET CACHE]: " + e.ToString());
                }
            }
        }

        public bool TryGetCachedAsset(UUID assetId, out AssetBase asset)
        {
            Object tmp;
            if (m_memcache.TryGet(assetId, out tmp))
            {
                asset = (AssetBase)tmp;
                //m_log.Info("Retrieved from cache " + assetId);
                return true;
            }

            asset = null;
            return false;
        }

        public void GetAsset(UUID assetId, AssetRequestCallback callback, bool isTexture)
        {
            //m_log.DebugFormat("[ASSET CACHE]: Requesting {0} {1}", isTexture ? "texture" : "asset", assetId);

            // Xantor 20080526:
            // if a request is made for an asset which is not in the cache yet, but has already been requested by
            // something else, queue up the callbacks on that requestor instead of swamping the assetserver
            // with multiple requests for the same asset.

            AssetBase asset;

            if (TryGetCachedAsset(assetId, out asset))
            {
                callback(assetId, asset);
            }
            else
            {
                // m_log.DebugFormat("[ASSET CACHE]: Adding request for {0} {1}", isTexture ? "texture" : "asset", assetId);

                NewAssetRequest req = new NewAssetRequest(callback);
                AssetRequestsList requestList;

                lock (RequestLists)
                {
                    if (RequestLists.TryGetValue(assetId, out requestList)) // do we already have a request pending?
                    {
                        // m_log.DebugFormat("[ASSET CACHE]: Intercepted Duplicate request for {0} {1}", isTexture ? "texture" : "asset", assetId);
                        // add to callbacks for this assetId
                        RequestLists[assetId].Requests.Add(req);
                    }
                    else
                    {
                        // m_log.DebugFormat("[ASSET CACHE]: Adding request for {0} {1}", isTexture ? "texture" : "asset", assetId);
                        requestList = new AssetRequestsList();
                        requestList.TimeRequested = DateTime.Now;
                        requestList.Requests.Add(req);

                        RequestLists.Add(assetId, requestList);

                        m_assetServer.RequestAsset(assetId, isTexture);
                    }
                }
            }
        }

        public AssetBase GetAsset(UUID assetID, bool isTexture)
        {
            // I'm not going over 3 seconds since this will be blocking processing of all the other inbound
            // packets from the client.
            const int pollPeriod = 200;
            int maxPolls = 15;

            AssetBase asset;

            if (TryGetCachedAsset(assetID, out asset))
            {
                return asset;
            }

            m_assetServer.RequestAsset(assetID, isTexture);

            do
            {
                Thread.Sleep(pollPeriod);

                if (TryGetCachedAsset(assetID, out asset))
                {
                    return asset;
                }
            }
            while (--maxPolls > 0);

            m_log.WarnFormat("[ASSET CACHE]: {0} {1} was not received before the retrieval timeout was reached",
                             isTexture ? "texture" : "asset", assetID.ToString());

            return null;
        }

        public void AddAsset(AssetBase asset)
        {
            if (!m_memcache.Contains(asset.FullID))
            {
                m_log.Info("[CACHE] Caching " + asset.FullID + " for 24 hours from last access");
                // Use 24 hour rolling asset cache.
                m_memcache.AddOrUpdate(asset.FullID, asset, TimeSpan.FromHours(24));

                // According to http://wiki.secondlife.com/wiki/AssetUploadRequest, Local signifies that the
                // information is stored locally.  It could disappear, in which case we could send the
                // ImageNotInDatabase packet to tell the client this.
                //
                // However, this doesn't quite appear to work with local textures that are part of an avatar's
                // appearance texture set.  Whilst sending an ImageNotInDatabase does trigger an automatic rebake
                // and reupload by the client, if those assets aren't pushed to the asset server anyway, then
                // on crossing onto another region server, other avatars can no longer get the required textures.
                // There doesn't appear to be any signal from the sim to the newly region border crossed client
                // asking it to reupload its local texture assets to that region server.
                //
                // One can think of other cunning ways around this.  For instance, on a region crossing or teleport,
                // the original sim could squirt local assets to the new sim.  Or the new sim could have pointers
                // to the original sim to fetch the 'local' assets (this is getting more complicated).
                //
                // But for now, we're going to take the easy way out and store local assets globally.
                //
                // TODO: Also, Temporary is now deprecated.  We should start ignoring it and not passing it out from LLClientView.
                if (!asset.Temporary || asset.Local)
                {
                    m_assetServer.StoreAsset(asset);
                }
            }
        }

        public void ExpireAsset(UUID uuid)
        {
            // uuid is unique, so no need to worry about it showing up
            // in the 2 caches differently.  Also, locks are probably
            // needed in all of this, or move to synchronized non
            // generic forms for Dictionaries.
            if (m_memcache.Contains(uuid))
            {
                m_memcache.Remove(uuid);
            }
        }

        // See IAssetReceiver
        public virtual void AssetReceived(AssetBase asset, bool IsTexture)
        {
            AssetInfo assetInf = new AssetInfo(asset);
            if (!m_memcache.Contains(assetInf.FullID))
            {
                m_memcache.AddOrUpdate(assetInf.FullID, assetInf, TimeSpan.FromHours(24));

                if (StatsManager.SimExtraStats != null)
                    StatsManager.SimExtraStats.AddAsset(assetInf);

                if (RequestedAssets.ContainsKey(assetInf.FullID))
                {
                    AssetRequest req = RequestedAssets[assetInf.FullID];
                    req.AssetInf = assetInf;
                    req.NumPackets = CalculateNumPackets(assetInf.Data);

                    RequestedAssets.Remove(assetInf.FullID);
                    // If it's a direct request for a script, drop it
                    // because it's a hacked client
                    if (req.AssetRequestSource != 2 || assetInf.Type != 10)
                        AssetRequests.Add(req);
                }
            }

            // Notify requesters for this asset
            AssetRequestsList reqList;

            lock (RequestLists)
            {
                if (RequestLists.TryGetValue(asset.FullID, out reqList))
                    RequestLists.Remove(asset.FullID);
            }

            if (reqList != null)
            {
                if (StatsManager.SimExtraStats != null)
                    StatsManager.SimExtraStats.AddAssetRequestTimeAfterCacheMiss(DateTime.Now - reqList.TimeRequested);

                foreach (NewAssetRequest req in reqList.Requests)
                {
                    // Xantor 20080526 are we really calling all the callbacks if multiple queued for 1 request? -- Yes, checked
                    // m_log.DebugFormat("[ASSET CACHE]: Callback for asset {0}", asset.FullID);
                    req.Callback(asset.FullID, asset);
                }
            }
        }

        // See IAssetReceiver
        public virtual void AssetNotFound(UUID assetID, bool IsTexture)
        {
//            m_log.WarnFormat("[ASSET CACHE]: AssetNotFound for {0}", assetID);

            // Remember the fact that this asset could not be found to prevent delays from repeated requests
            m_memcache.Add(assetID, null, TimeSpan.FromHours(24));

            // Notify requesters for this asset
            AssetRequestsList reqList;
            lock (RequestLists)
            {
                if (RequestLists.TryGetValue(assetID, out reqList))
                    RequestLists.Remove(assetID);
            }

            if (reqList != null)
            {
                if (StatsManager.SimExtraStats != null)
                    StatsManager.SimExtraStats.AddAssetRequestTimeAfterCacheMiss(DateTime.Now - reqList.TimeRequested);

                foreach (NewAssetRequest req in reqList.Requests)
                {
                    req.Callback(assetID, null);
                }
            }
        }

        /// <summary>
        /// Calculate the number of packets required to send the asset to the client.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private static int CalculateNumPackets(byte[] data)
        {
            const uint m_maxPacketSize = 600;
            int numPackets = 1;

            if (data.LongLength > m_maxPacketSize)
            {
                // over max number of bytes so split up file
                long restData = data.LongLength - m_maxPacketSize;
                int restPackets = (int)((restData + m_maxPacketSize - 1) / m_maxPacketSize);
                numPackets += restPackets;
            }

            return numPackets;
        }

        public void AddAssetRequest(IClientAPI userInfo, TransferRequestPacket transferRequest)
        {
            UUID requestID = UUID.Zero;
            byte source = 2;
            if (transferRequest.TransferInfo.SourceType == 2)
            {
                //direct asset request
                requestID = new UUID(transferRequest.TransferInfo.Params, 0);
            }
            else if (transferRequest.TransferInfo.SourceType == 3)
            {
                //inventory asset request
                requestID = new UUID(transferRequest.TransferInfo.Params, 80);
                source = 3;
                //m_log.Debug("asset request " + requestID);
            }

            //check to see if asset is in local cache, if not we need to request it from asset server.
            //m_log.Debug("asset request " + requestID);
            if (!m_memcache.Contains(requestID))
            {
                //not found asset
                // so request from asset server
                if (!RequestedAssets.ContainsKey(requestID))
                {
                    AssetRequest request = new AssetRequest();
                    request.RequestUser = userInfo;
                    request.RequestAssetID = requestID;
                    request.TransferRequestID = transferRequest.TransferInfo.TransferID;
                    request.AssetRequestSource = source;
                    request.Params = transferRequest.TransferInfo.Params;
                    RequestedAssets.Add(requestID, request);
                    m_assetServer.RequestAsset(requestID, false);
                }

                return;
            }

            // It has an entry in our cache
            AssetBase asset = (AssetBase)m_memcache[requestID];

            // FIXME: We never tell the client about assets which do not exist when requested by this transfer mechanism, which can't be right.
            if (null == asset)
            {
                //m_log.DebugFormat("[ASSET CACHE]: Asset transfer request for asset which is {0} already known to be missing.  Dropping", requestID);
                return;
            }

            // Scripts cannot be retrieved by direct request
            if (transferRequest.TransferInfo.SourceType == 2 && asset.Type == 10)
                return;

            // The asset is knosn to exist and is in our cache, so add it to the AssetRequests list
            AssetRequest req = new AssetRequest();
            req.RequestUser = userInfo;
            req.RequestAssetID = requestID;
            req.TransferRequestID = transferRequest.TransferInfo.TransferID;
            req.AssetRequestSource = source;
            req.Params = transferRequest.TransferInfo.Params;
            req.AssetInf = new AssetInfo(asset);
            req.NumPackets = CalculateNumPackets(asset.Data);
            AssetRequests.Add(req);
        }

        /// <summary>
        /// Process the asset queue which sends packets directly back to the client.
        /// </summary>
        private void ProcessAssetQueue()
        {
            //should move the asset downloading to a module, like has been done with texture downloading
            if (AssetRequests.Count == 0)
            {
                //no requests waiting
                return;
            }

            // if less than 5, do all of them
            int num = Math.Min(5, AssetRequests.Count);

            AssetRequest req;
            AssetRequestToClient req2 = null;
            for (int i = 0; i < num; i++)
            {
                req = AssetRequests[i];
                if (req2 == null)
                {
                    req2 = new AssetRequestToClient();
                }
                
                // Trying to limit memory usage by only creating AssetRequestToClient if needed               
                req2.AssetInf = req.AssetInf;
                req2.AssetRequestSource = req.AssetRequestSource;
                req2.DataPointer = req.DataPointer;
                req2.DiscardLevel = req.DiscardLevel;
                req2.ImageInfo = req.ImageInfo;
                req2.IsTextureRequest = req.IsTextureRequest;
                req2.NumPackets = req.NumPackets;
                req2.PacketCounter = req.PacketCounter;
                req2.Params = req.Params;
                req2.RequestAssetID = req.RequestAssetID;
                req2.TransferRequestID = req.TransferRequestID;
                req.RequestUser.SendAsset(req2);
            }

            //remove requests that have been completed
            for (int i = 0; i < num; i++)
            {
                AssetRequests.RemoveAt(0);
            }
        }

        public class AssetRequest
        {
            public IClientAPI RequestUser;
            public UUID RequestAssetID;
            public AssetInfo AssetInf;
            public TextureImage ImageInfo;
            public UUID TransferRequestID;
            public long DataPointer = 0;
            public int NumPackets = 0;
            public int PacketCounter = 0;
            public bool IsTextureRequest;
            public byte AssetRequestSource = 2;
            public byte[] Params = null;
            //public bool AssetInCache;
            //public int TimeRequested;
            public int DiscardLevel = -1;
        }

        public class AssetInfo : AssetBase
        {
            public AssetInfo(AssetBase aBase)
            {
                Data = aBase.Data;
                FullID = aBase.FullID;
                Type = aBase.Type;
                Name = aBase.Name;
                Description = aBase.Description;
            }
        }

        public class TextureImage : AssetBase
        {
            public TextureImage(AssetBase aBase)
            {
                Data = aBase.Data;
                FullID = aBase.FullID;
                Type = aBase.Type;
                Name = aBase.Name;
                Description = aBase.Description;
            }
        }

        /// <summary>
        /// A list of requests for a particular asset.
        /// </summary>
        public class AssetRequestsList
        {
            /// <summary>
            /// A list of requests for assets
            /// </summary>
            public List<NewAssetRequest> Requests = new List<NewAssetRequest>();

            /// <summary>
            /// Record the time that this request was first made.
            /// </summary>
            public DateTime TimeRequested;
        }

        /// <summary>
        /// Represent a request for an asset that has yet to be fulfilled.
        /// </summary>
        public class NewAssetRequest
        {
            public AssetRequestCallback Callback;

            public NewAssetRequest(AssetRequestCallback callback)
            {
                Callback = callback;
            }
        }
    }
}
