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
* 
*/

using System;
using System.Collections.Generic;
using System.Threading;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Communications.Cache
{
    public delegate void AssetRequestCallback(LLUUID assetID, AssetBase asset);

    /// <summary>
    /// Manages local cache of assets and their sending to viewers.
    /// </summary>
    public class AssetCache : IAssetReceiver
    {
        public Dictionary<LLUUID, AssetInfo> Assets;
        public Dictionary<LLUUID, TextureImage> Textures;

        public List<AssetRequest> AssetRequests = new List<AssetRequest>(); //assets ready to be sent to viewers
        public List<AssetRequest> TextureRequests = new List<AssetRequest>(); //textures ready to be sent

        public Dictionary<LLUUID, AssetRequest> RequestedAssets = new Dictionary<LLUUID, AssetRequest>();
        //Assets requested from the asset server

        public Dictionary<LLUUID, AssetRequest> RequestedTextures = new Dictionary<LLUUID, AssetRequest>();
        //Textures requested from the asset server

        public Dictionary<LLUUID, AssetRequestsList> RequestLists = new Dictionary<LLUUID, AssetRequestsList>();

        private readonly IAssetServer m_assetServer;

        private readonly Thread m_assetCacheThread;
        private readonly LogBase m_log;

        /// <summary>
        /// 
        /// </summary>
        public AssetCache(IAssetServer assetServer, LogBase log)
        {
            log.Verbose("ASSETSTORAGE", "Creating Asset cache");
            m_assetServer = assetServer;
            m_assetServer.SetReceiver(this);
            Assets = new Dictionary<LLUUID, AssetInfo>();
            Textures = new Dictionary<LLUUID, TextureImage>();
            m_assetCacheThread = new Thread(new ThreadStart(RunAssetManager));
            m_assetCacheThread.IsBackground = true;
            m_assetCacheThread.Start();

            m_log = log;
        }

        /// <summary>
        /// 
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
                    m_log.Error("ASSETCACHE", e.ToString());
                }
            }
        }

        /// <summary>
        /// Only get an asset if we already have it in the cache.
        /// </summary>
        /// <param name="assetID"></param></param>
        /// <returns></returns>
        private AssetBase GetCachedAsset(LLUUID assetID)
        {
            AssetBase asset = null;
            if (Textures.ContainsKey(assetID))
            {
                asset = Textures[assetID];
            }
            else if (Assets.ContainsKey(assetID))
            {
                asset = Assets[assetID];
            }
            return asset;
        }

        public void GetAsset(LLUUID assetID, AssetRequestCallback callback)
        {
            AssetBase asset = null;
            if (Textures.ContainsKey(assetID))
            {
                asset = Textures[assetID];
            }
            else if (Assets.ContainsKey(assetID))
            {
                asset = Assets[assetID];
            }

            if (asset != null)
            {
                callback(assetID, asset);
            }
            else
            {
                NewAssetRequest req = new NewAssetRequest(assetID, callback);
                if (RequestLists.ContainsKey(assetID))
                {
                    lock (RequestLists)
                    {
                        RequestLists[assetID].Requests.Add(req);
                    }
                }
                else
                {
                    AssetRequestsList reqList = new AssetRequestsList(assetID);
                    reqList.Requests.Add(req);
                    lock (RequestLists)
                    {
                        RequestLists.Add(assetID, reqList);
                    }
                }
                m_assetServer.RequestAsset(assetID, false);
            }
        }

        /// <summary>
        /// Get an asset.  If the asset isn't in the cache, a request will be made to the persistent store to
        /// load it into the cache.  
        /// 
        /// XXX We'll keep polling the cache until we get the asset or we exceed
        /// the allowed number of polls.  This isn't a very good way of doing things since a single thread
        /// is processing inbound packets, so if the asset server is slow, we could block this for up to
        /// the timeout period.  What we might want to do is register asynchronous callbacks on asset
        /// receipt in the same manner as the nascent (but not yet active) TextureDownloadModule.  Of course,
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

            AssetBase asset = GetCachedAsset(assetID);
            if (asset != null)
            {
                return asset;
            }

            m_assetServer.RequestAsset(assetID, isTexture);

            do
            {
                Thread.Sleep(pollPeriod);

                asset = GetCachedAsset(assetID);
                if (asset != null)
                {
                    return asset;
                }
            } while (--maxPolls > 0);

            MainLog.Instance.Warn(
                "ASSETCACHE", "Asset {0} was not received before the retrieval timeout was reached");

            return null;
        }

        /// <summary>
        /// Add an asset to both the persistent store and the cache.
        /// </summary>
        /// <param name="asset"></param>
        public void AddAsset(AssetBase asset)
        {
            string temporary = asset.Temporary ? "temporary" : "";
            string type = asset.Type == 0 ? "texture" : "asset";

            string result = "Ignored";

            if (asset.Type == 0)
            {
                if (Textures.ContainsKey(asset.FullID))
                {
                    result = "Duplicate ignored.";
                }
                else
                {
                    TextureImage textur = new TextureImage(asset);
                    Textures.Add(textur.FullID, textur);
                    if (asset.Temporary)
                    {
                        result = "Added to cache";
                    }
                    else
                    {
                        m_assetServer.StoreAndCommitAsset(asset);
                        result = "Added to server";
                    }
                }
            }
            else
            {
                if (Assets.ContainsKey(asset.FullID))
                {
                    result = "Duplicate ignored.";
                }
                else
                {
                    AssetInfo assetInf = new AssetInfo(asset);
                    Assets.Add(assetInf.FullID, assetInf);
                    if (asset.Temporary)
                    {
                        result = "Added to cache";
                    }
                    else
                    {
                        m_assetServer.StoreAndCommitAsset(asset);
                        result = "Added to server";
                    }
                }
            }

            m_log.Verbose("ASSETCACHE", "Adding {0} {1} [{2}]: {3}.", temporary, type, asset.FullID, result);
        }

        public void DeleteAsset(LLUUID assetID)
        {
            //  this.m_assetServer.DeleteAsset(assetID);

            //Todo should delete it from memory too
        }

        public AssetBase CopyAsset(LLUUID assetID)
        {
            AssetBase asset = GetCachedAsset(assetID);
            if (asset == null)
                return null;

            asset.FullID = LLUUID.Random(); // TODO: check for conflicts
            AddAsset(asset);
            return asset;
        }

        public void AssetReceived(AssetBase asset, bool IsTexture)
        {
            if (asset.FullID != LLUUID.Zero) // if it is set to zero then the asset wasn't found by the server
            {
                //check if it is a texture or not
                //then add to the correct cache list
                //then check for waiting requests for this asset/texture (in the Requested lists)
                //and move those requests into the Requests list.

                if (IsTexture)
                {
                    //Console.WriteLine("asset received from asset server");

                    TextureImage image = new TextureImage(asset);
                    if (!Textures.ContainsKey(image.FullID))
                    {
                        Textures.Add(image.FullID, image);
                        if (RequestedTextures.ContainsKey(image.FullID))
                        {
                            AssetRequest req = RequestedTextures[image.FullID];
                            req.ImageInfo = image;

                            req.NumPackets = CalculateNumPackets(image.Data);

                            RequestedTextures.Remove(image.FullID);
                            TextureRequests.Add(req);
                        }
                    }
                }
                else
                {
                    AssetInfo assetInf = new AssetInfo(asset);
                    if (!Assets.ContainsKey(assetInf.FullID))
                    {
                        Assets.Add(assetInf.FullID, assetInf);
                        if (RequestedAssets.ContainsKey(assetInf.FullID))
                        {
                            AssetRequest req = RequestedAssets[assetInf.FullID];
                            req.AssetInf = assetInf;
                            req.NumPackets = CalculateNumPackets(assetInf.Data);
                            RequestedAssets.Remove(assetInf.FullID);
                            AssetRequests.Add(req);
                        }
                    }
                }

                if (RequestLists.ContainsKey(asset.FullID))
                {
                    AssetRequestsList reqList = RequestLists[asset.FullID];
                    foreach (NewAssetRequest req in reqList.Requests)
                    {
                        req.Callback(asset.FullID, asset);
                    }

                    lock (RequestLists)
                    {
                        RequestLists.Remove(asset.FullID);
                        reqList.Requests.Clear();
                    }
                }
            }
        }

        public void AssetNotFound(LLUUID assetID)
        {
            //if (this.RequestedTextures.ContainsKey(assetID))
            //{
            //    MainLog.Instance.Warn("ASSET CACHE", "sending image not found for {0}", assetID);
            //    AssetRequest req = this.RequestedTextures[assetID];
            //    ImageNotInDatabasePacket notFound = new ImageNotInDatabasePacket();
            //    notFound.ImageID.ID = assetID;
            //    req.RequestUser.OutPacket(notFound);
            //    this.RequestedTextures.Remove(assetID);
            //}
            //else
            //{
            //    MainLog.Instance.Error("ASSET CACHE", "Cound not send image not found for {0}", assetID);
            //}
        }

        private int CalculateNumPackets(byte[] data)
        {
            const uint m_maxPacketSize = 600;
            int numPackets = 1;

            if (data.LongLength > m_maxPacketSize)
            {
                // over max number of bytes so split up file
                long restData = data.LongLength - m_maxPacketSize;
                int restPackets = (int) ((restData + m_maxPacketSize - 1)/m_maxPacketSize);
                numPackets += restPackets;
            }

            return numPackets;
        }

        #region Assets

        /// <summary>
        /// 
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
            //it is in our cache 
            AssetInfo asset = Assets[requestID];

            // add to the AssetRequests list
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
        /// 
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
            for (int i = 0; i < num; i++)
            {
                req = (AssetRequest) AssetRequests[i];
                //Console.WriteLine("sending asset " + req.RequestAssetID);
                TransferInfoPacket Transfer = new TransferInfoPacket();
                Transfer.TransferInfo.ChannelType = 2;
                Transfer.TransferInfo.Status = 0;
                Transfer.TransferInfo.TargetType = 0;
                if (req.AssetRequestSource == 2)
                {
                    Transfer.TransferInfo.Params = new byte[20];
                    Array.Copy(req.RequestAssetID.GetBytes(), 0, Transfer.TransferInfo.Params, 0, 16);
                    int assType = (int) req.AssetInf.Type;
                    Array.Copy(Helpers.IntToBytes(assType), 0, Transfer.TransferInfo.Params, 16, 4);
                }
                else if (req.AssetRequestSource == 3)
                {
                    Transfer.TransferInfo.Params = req.Params;
                    // Transfer.TransferInfo.Params = new byte[100];
                    //Array.Copy(req.RequestUser.AgentId.GetBytes(), 0, Transfer.TransferInfo.Params, 0, 16);
                    //Array.Copy(req.RequestUser.SessionId.GetBytes(), 0, Transfer.TransferInfo.Params, 16, 16);
                }
                Transfer.TransferInfo.Size = (int) req.AssetInf.Data.Length;
                Transfer.TransferInfo.TransferID = req.TransferRequestID;
                req.RequestUser.OutPacket(Transfer, ThrottleOutPacketType.Asset);

                if (req.NumPackets == 1)
                {
                    TransferPacketPacket TransferPacket = new TransferPacketPacket();
                    TransferPacket.TransferData.Packet = 0;
                    TransferPacket.TransferData.ChannelType = 2;
                    TransferPacket.TransferData.TransferID = req.TransferRequestID;
                    TransferPacket.TransferData.Data = req.AssetInf.Data;
                    TransferPacket.TransferData.Status = 1;
                    req.RequestUser.OutPacket(TransferPacket, ThrottleOutPacketType.Asset);
                }
                else
                {
                    int processedLength = 0;
                    // libsecondlife hardcodes 1500 as the maximum data chunk size
                    int maxChunkSize = 1500;
                    int packetNumber = 0;

                    while (processedLength < req.AssetInf.Data.Length)
                    {
                        TransferPacketPacket TransferPacket = new TransferPacketPacket();
                        TransferPacket.TransferData.Packet = packetNumber;
                        TransferPacket.TransferData.ChannelType = 2;
                        TransferPacket.TransferData.TransferID = req.TransferRequestID;

                        int chunkSize = Math.Min(req.AssetInf.Data.Length - processedLength, maxChunkSize);
                        byte[] chunk = new byte[chunkSize];
                        Array.Copy(req.AssetInf.Data, processedLength, chunk, 0, chunk.Length);

                        TransferPacket.TransferData.Data = chunk;

                        // 0 indicates more packets to come, 1 indicates last packet
                        if (req.AssetInf.Data.Length - processedLength > maxChunkSize)
                        {
                            TransferPacket.TransferData.Status = 0;
                        }
                        else
                        {
                            TransferPacket.TransferData.Status = 1;
                        }

                        req.RequestUser.OutPacket(TransferPacket, ThrottleOutPacketType.Asset);

                        processedLength += chunkSize;
                        packetNumber++;
                    }
                }
            }

            //remove requests that have been completed
            for (int i = 0; i < num; i++)
            {
                AssetRequests.RemoveAt(0);
            }
        }

        #endregion

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
                InvType = aBase.InvType;
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
                InvType = aBase.InvType;
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