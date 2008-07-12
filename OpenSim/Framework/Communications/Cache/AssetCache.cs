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
using libsecondlife;
using libsecondlife.Packets;
using log4net;
using OpenSim.Framework.Statistics;

namespace OpenSim.Framework.Communications.Cache
{
    public delegate void AssetRequestCallback(LLUUID assetID, AssetBase asset);

    /// <summary>
    /// Manages local cache of assets and their sending to viewers.
    ///
    /// This class actually encapsulates two largely separate mechanisms.  One mechanism fetches assets either
    /// synchronously or async and passes the data back to the requester.  The second mechanism fetches assets and
    /// sends packetised data directly back to the client.  The only point where they meet is AssetReceived() and
    /// AssetNotFound(), which means they do share the same asset and texture caches.
    ///
    /// TODO  Assets in this cache are effectively immortal (they are never disposed off through old age).
    /// This is not a huge problem at the moment since other memory use usually dwarfs that used by assets
    /// but it's something to bear in mind.
    /// </summary>
    public class AssetCache : IAssetReceiver
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The cache of assets.  This does not include textures.
        /// </summary>
        private Dictionary<LLUUID, AssetInfo> Assets;

        /// <summary>
        /// The cache of textures.
        /// </summary>
        private Dictionary<LLUUID, TextureImage> Textures;

        /// <summary>
        /// Assets requests which are waiting for asset server data.  This includes texture requests
        /// </summary>
         private Dictionary<LLUUID, AssetRequest> RequestedAssets;

        /// <summary>
        /// Asset requests with data which are ready to be sent back to requesters.  This includes textures.
        /// </summary>
        private List<AssetRequest> AssetRequests;

        /// <summary>
        /// Until the asset request is fulfilled, each asset request is associated with a list of requesters
        /// </summary>
        private Dictionary<LLUUID, AssetRequestsList> RequestLists;

        private readonly IAssetServer m_assetServer;

        private readonly Thread m_assetCacheThread;

        /// <summary>
        /// Report statistical data.
        /// </summary>
        public void ShowState()
        {
            m_log.InfoFormat("Assets:{0}  Textures:{1}   RequestLists:{2}",
                Assets.Count,
                Textures.Count,
              //   AssetRequests.Count,
              //    RequestedAssets.Count,
                RequestLists.Count);

            int temporaryImages = 0;
            int temporaryAssets = 0;

            long imageBytes = 0;
            long assetBytes = 0;

            foreach (TextureImage texture in Textures.Values)
            {
                if (texture != null)
                {
                    if (texture.Temporary)
                    {
                        temporaryImages++;
                    }

                    imageBytes += texture.Data.GetLongLength(0);
                }
            }

            foreach (AssetInfo asset in Assets.Values)
            {
                if (asset != null)
                {
                    if (asset.Temporary)
                    {
                        temporaryAssets++;
                    }

                    assetBytes += asset.Data.GetLongLength(0);
                }
            }

            m_log.InfoFormat("Temporary Images: {0}  Temporary Assets: {1}",
                temporaryImages,
                temporaryAssets);

            m_log.InfoFormat("Image data: {0}kb  Asset data: {1}kb",
                imageBytes / 1024,
                assetBytes / 1024);
        }

        /// <summary>
        /// Clear the asset cache.
        /// </summary>
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
            Assets = new Dictionary<LLUUID, AssetInfo>();
            Textures = new Dictionary<LLUUID, TextureImage>();
            AssetRequests = new List<AssetRequest>();

            RequestedAssets = new Dictionary<LLUUID, AssetRequest>();
            RequestLists = new Dictionary<LLUUID, AssetRequestsList>();
        }

        /// <summary>
        /// Constructor.  Initialize will need to be called separately.
        /// </summary>
        /// <param name="assetServer"></param>
        public AssetCache(IAssetServer assetServer)
        {
            m_log.Info("[ASSET CACHE]: Creating Asset cache");
            Initialize();

            m_assetServer = assetServer;
            m_assetServer.SetReceiver(this);

            m_assetCacheThread = new Thread(new ThreadStart(RunAssetManager));
            m_assetCacheThread.Name = "AssetCacheThread";
            m_assetCacheThread.IsBackground = true;
            m_assetCacheThread.Start();
            ThreadTracker.Add(m_assetCacheThread);
        }

        /// <summary>
        /// Process the asset queue which holds data which is packeted up and sent
        /// directly back to the client.
        /// </summary>
        public void RunAssetManager()
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

        /// <summary>
        /// Only get an asset if we already have it in the cache.
        /// </summary>
        /// <param name="assetId"></param></param>
        /// <returns></returns>
        //private AssetBase GetCachedAsset(LLUUID assetId)
        //{
        //    AssetBase asset = null;

        //    if (Textures.ContainsKey(assetId))
        //    {
        //        asset = Textures[assetId];
        //    }
        //    else if (Assets.ContainsKey(assetId))
        //    {
        //        asset = Assets[assetId];
        //    }

        //    return asset;
        //}

        private bool TryGetCachedAsset(LLUUID assetId, out AssetBase asset)
        {
            if (Textures.ContainsKey(assetId))
            {
                asset = Textures[assetId];
                return true;
            }
            else if (Assets.ContainsKey(assetId))
            {
                asset = Assets[assetId];
                return true;
            }

            asset = null;
            return false;
        }

        /// <summary>
        /// Asynchronously retrieve an asset.
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="callback">
        /// A callback invoked when the asset has either been found or not found.
        /// If the asset was found this is called with the asset UUID and the asset data
        /// If the asset was not found this is still called with the asset UUID but with a null asset data reference</param>
        public void GetAsset(LLUUID assetId, AssetRequestCallback callback, bool isTexture)
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
#if DEBUG
                // m_log.DebugFormat("[ASSET CACHE]: Adding request for {0} {1}", isTexture ? "texture" : "asset", assetId);
#endif

                NewAssetRequest req = new NewAssetRequest(assetId, callback);
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
                        requestList = new AssetRequestsList(assetId);
                        RequestLists.Add(assetId, requestList);
                        requestList.Requests.Add(req);
                        m_assetServer.RequestAsset(assetId, isTexture);
                    }
                }
            }
        }

        /// <summary>
        /// Synchronously retreive an asset.  If the asset isn't in the cache, a request will be made to the persistent store to
        /// load it into the cache.
        ///
        /// XXX We'll keep polling the cache until we get the asset or we exceed
        /// the allowed number of polls.  This isn't a very good way of doing things since a single thread
        /// is processing inbound packets, so if the asset server is slow, we could block this for up to
        /// the timeout period.  What we might want to do is register asynchronous callbacks on asset
        /// receipt in the same manner as the TextureDownloadModule.  Of course,
        /// a timeout before asset receipt usually isn't fatal, the operation will work on the retry when the
        /// asset is much more likely to have made it into the cache.
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="isTexture"></param>
        /// <returns>null if the asset could not be retrieved</returns>
        public AssetBase GetAsset(LLUUID assetID, bool isTexture)
        {
            // I'm not going over 3 seconds since this will be blocking processing of all the other inbound
            // packets from the client.
            int pollPeriod = 200;
            int maxPolls = 15;

            AssetBase asset;

            if (TryGetCachedAsset(assetID, out asset))
            {
                return asset;
            }
            else
            {
                m_assetServer.RequestAsset(assetID, isTexture);

                do
                {
                    Thread.Sleep(pollPeriod);

                    if (TryGetCachedAsset(assetID, out asset))
                    {
                        return asset;
                    }
                } while (--maxPolls > 0);

                m_log.WarnFormat("[ASSET CACHE]: {0} {1} was not received before the retrieval timeout was reached",
                                 isTexture ? "texture" : "asset", assetID.ToString());

                return null;
            }
        }

        /// <summary>
        /// Add an asset to both the persistent store and the cache.
        /// </summary>
        /// <param name="asset"></param>
        public void AddAsset(AssetBase asset)
        {
            if (asset.Type == (int)AssetType.Texture)
            {
                if (!Textures.ContainsKey(asset.FullID))
                {
                    TextureImage textur = new TextureImage(asset);
                    Textures.Add(textur.FullID, textur);

                    if (StatsManager.SimExtraStats != null)
                        StatsManager.SimExtraStats.AddTexture(textur);

                    if (!asset.Temporary)
                    {
                        m_assetServer.StoreAsset(asset);
                    }
                }
            }
            else
            {                
                if (!Assets.ContainsKey(asset.FullID))
                {                    
                    AssetInfo assetInf = new AssetInfo(asset);
                    Assets.Add(assetInf.FullID, assetInf);

                    if (StatsManager.SimExtraStats != null)
                        StatsManager.SimExtraStats.AddAsset(assetInf);

                    if (!asset.Temporary)
                    {
                        m_assetServer.StoreAsset(asset);
                    }
                }
            }
        }

        /// <summary> 
        /// Allows you to clear a specific asset by uuid out
        /// of the asset cache.  This is needed because the osdynamic
        /// texture code grows the asset cache without bounds.  The
        /// real solution here is a much better cache archicture, but
        /// this is a stop gap measure until we have such a thing.
        /// </summary>

        public void ExpireAsset(LLUUID uuid)
        {
            // uuid is unique, so no need to worry about it showing up
            // in the 2 caches differently.  Also, locks are probably
            // needed in all of this, or move to synchronized non
            // generic forms for Dictionaries.
            if (Textures.ContainsKey(uuid))
            {
                Textures.Remove(uuid);
            } 
            else if (Assets.ContainsKey(uuid)) 
            {
                Assets.Remove(uuid);
            }
        }

        // See IAssetReceiver
        public void AssetReceived(AssetBase asset, bool IsTexture)
        {
            //check if it is a texture or not
            //then add to the correct cache list
            //then check for waiting requests for this asset/texture (in the Requested lists)
            //and move those requests into the Requests list.
            if (IsTexture)
            {
                TextureImage image = new TextureImage(asset);
                if (!Textures.ContainsKey(image.FullID))
                {
                    Textures.Add(image.FullID, image);

                    if (StatsManager.SimExtraStats != null)
                    {
                        StatsManager.SimExtraStats.AddTexture(image);
                    }
                }
            }
            else
            {
                AssetInfo assetInf = new AssetInfo(asset);
                if (!Assets.ContainsKey(assetInf.FullID))
                {
                    Assets.Add(assetInf.FullID, assetInf);

                    if (StatsManager.SimExtraStats != null)
                    {
                        StatsManager.SimExtraStats.AddAsset(assetInf);
                    }

                    if (RequestedAssets.ContainsKey(assetInf.FullID))
                    {
                        AssetRequest req = RequestedAssets[assetInf.FullID];
                        req.AssetInf = assetInf;
                        req.NumPackets = CalculateNumPackets(assetInf.Data);

                        RequestedAssets.Remove(assetInf.FullID);
                        // If it's a direct request for a script, drop it
                        // because it's a hacked client
                        if(req.AssetRequestSource != 2 || assetInf.Type != 10)
                            AssetRequests.Add(req);
                    }
                }
            }

            // Notify requesters for this asset
            if (RequestLists.ContainsKey(asset.FullID))
            {
                AssetRequestsList reqList = null;
                lock (RequestLists)
                {
                    //m_log.Info("AssetCache: Lock taken on requestLists (AssetReceived #1)");
                    reqList = RequestLists[asset.FullID];

                }
                //m_log.Info("AssetCache: Lock released on requestLists (AssetReceived #1)");
                if (reqList != null)
                {
                    //making a copy of the list is not ideal
                    //but the old method of locking around this whole block of code was causing a multi-thread lock
                    //between this and the TextureDownloadModule
                    //while the localAsset thread running this and trying to send a texture to the callback in the
                    //texturedownloadmodule , and hitting a lock in there. While the texturedownload thread (which was holding
                    // the lock in the texturedownload module) was trying to
                    //request a new asset and hitting a lock in here on the RequestLists.

                    List<NewAssetRequest> theseRequests = new List<NewAssetRequest>(reqList.Requests);
                    reqList.Requests.Clear();

                    lock (RequestLists)
                    {
                       // m_log.Info("AssetCache: Lock taken on requestLists (AssetReceived #2)");
                        RequestLists.Remove(asset.FullID);
                    }
                    //m_log.Info("AssetCache: Lock released on requestLists (AssetReceived #2)");

                    foreach (NewAssetRequest req in theseRequests)
                    {
                        // Xantor 20080526 are we really calling all the callbacks if multiple queued for 1 request? -- Yes, checked
                        // m_log.DebugFormat("[ASSET CACHE]: Callback for asset {0}", asset.FullID);
                        req.Callback(asset.FullID, asset);
                    }
                }
            }
        }

        // See IAssetReceiver
        public void AssetNotFound(LLUUID assetID, bool IsTexture)
        {
            //m_log.WarnFormat("[ASSET CACHE]: AssetNotFound for {0}", assetID);

            if (IsTexture)
            {
                Textures[assetID] = null;
            }
            else
            {
                Assets[assetID] = null;
            }

            // Notify requesters for this asset
            AssetRequestsList reqList = null;
            lock (RequestLists)
            {
                //  m_log.Info("AssetCache: Lock taken on requestLists (AssetNotFound #1)");
                if (RequestLists.ContainsKey(assetID))
                {
                   reqList = RequestLists[assetID];
                }
            }
            // m_log.Info("AssetCache: Lock released on requestLists (AssetNotFound #1)");

            if (reqList != null)
            {
                List<NewAssetRequest> theseRequests = new List<NewAssetRequest>(reqList.Requests);
                reqList.Requests.Clear();

                lock (RequestLists)
                {
                    //  m_log.Info("AssetCache: Lock taken on requestLists (AssetNotFound #2)");
                    RequestLists.Remove(assetID);
                }
                //  m_log.Info("AssetCache: Lock released on requestLists (AssetNotFound #2)");

                foreach (NewAssetRequest req in theseRequests)
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

        /// <summary>
        /// Handle an asset request from the client.  The result will be sent back asynchronously.
        /// </summary>
        /// <param name="userInfo"></param>
        /// <param name="transferRequest"></param>
        public void AddAssetRequest(IClientAPI userInfo, TransferRequestPacket transferRequest)
        {
            LLUUID requestID = null;
            byte source = 2;
            if (transferRequest.TransferInfo.SourceType == 2)
            {
                //direct asset request
                requestID = new LLUUID(transferRequest.TransferInfo.Params, 0);
            }
            else if (transferRequest.TransferInfo.SourceType == 3)
            {
                //inventory asset request
                requestID = new LLUUID(transferRequest.TransferInfo.Params, 80);
                source = 3;
                //Console.WriteLine("asset request " + requestID);
            }
            //check to see if asset is in local cache, if not we need to request it from asset server.
            //Console.WriteLine("asset request " + requestID);
            if (!Assets.ContainsKey(requestID))
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
            AssetInfo asset = Assets[requestID];

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
            req.AssetInf = asset;
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
                req = (AssetRequest)AssetRequests[i];
                if (req2 == null)
                {
                    req2 = new AssetRequestToClient();
                }
                // Trying to limit memory usage by only creating AssetRequestToClient if needed
                //req2 = new AssetRequestToClient();
                req2.AssetInf = (AssetBase)req.AssetInf;
                req2.AssetRequestSource = req.AssetRequestSource;
                req2.DataPointer = req.DataPointer;
                req2.DiscardLevel = req.DiscardLevel;
                req2.ImageInfo = (AssetBase)req.ImageInfo;
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
            public LLUUID RequestAssetID;
            public AssetInfo AssetInf;
            public TextureImage ImageInfo;
            public LLUUID TransferRequestID;
            public long DataPointer = 0;
            public int NumPackets = 0;
            public int PacketCounter = 0;
            public bool IsTextureRequest;
            public byte AssetRequestSource = 2;
            public byte[] Params = null;
            //public bool AssetInCache;
            //public int TimeRequested;
            public int DiscardLevel = -1;

            public AssetRequest()
            {
            }
        }

        public class AssetInfo : AssetBase
        {
            public AssetInfo()
            {
            }

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
            public TextureImage()
            {
            }

            public TextureImage(AssetBase aBase)
            {
                Data = aBase.Data;
                FullID = aBase.FullID;
                Type = aBase.Type;
                Name = aBase.Name;
                Description = aBase.Description;
            }
        }

        public class AssetRequestsList
        {
            public LLUUID AssetID;
            public List<NewAssetRequest> Requests = new List<NewAssetRequest>();

            public AssetRequestsList(LLUUID assetID)
            {
                AssetID = assetID;
            }
        }

        public class NewAssetRequest
        {
            public LLUUID AssetID;
            public AssetRequestCallback Callback;

            public NewAssetRequest(LLUUID assetID, AssetRequestCallback callback)
            {
                AssetID = assetID;
                Callback = callback;
            }
        }
    }
}
