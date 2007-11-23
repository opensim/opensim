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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using System.IO;
using System.Threading;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Communications.Cache
{
    public delegate void DownloadComplete(AssetCache.TextureSender sender);

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

        public Dictionary<LLUUID, TextureSender> SendingTextures = new Dictionary<LLUUID, TextureSender>();

        public Dictionary<LLUUID, AssetRequestsList> RequestLists = new Dictionary<LLUUID, AssetRequestsList>();

        private BlockingQueue<TextureSender> m_queueTextures = new BlockingQueue<TextureSender>();
        private Dictionary<LLUUID, List<LLUUID>> m_avatarReceivedTextures = new Dictionary<LLUUID, List<LLUUID>>();

        private Dictionary<LLUUID, Dictionary<LLUUID, int>> m_timesTextureSent =
            new Dictionary<LLUUID, Dictionary<LLUUID, int>>();


        private IAssetServer m_assetServer;

        private Thread m_assetCacheThread;
        private Thread m_textureSenderThread;
        private LogBase m_log;

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

            m_textureSenderThread = new Thread(new ThreadStart(ProcessTextureSenders));
            m_textureSenderThread.IsBackground = true;
            m_textureSenderThread.Start();
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
                    ProcessTextureQueue();
                    Thread.Sleep(500);
                }
                catch (Exception e)
                {
                    System.Console.WriteLine(e.Message + " : " + e.StackTrace);
                }
            }
        }


        public AssetBase GetAsset(LLUUID assetID)
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


        public AssetBase GetAsset(LLUUID assetID, bool isTexture)
        {
            AssetBase asset = GetAsset(assetID);
            if (asset == null)
            {
                m_assetServer.RequestAsset(assetID, isTexture);
            }
            return asset;
        }

        public void AddAsset(AssetBase asset)
        {
            string temporary = asset.Temporary ? "temporary" : "";
            string type = asset.Type == 0 ? "texture" : "asset";

            string result = "Ignored";

            if (asset.Type == 0)
            {
                if(Textures.ContainsKey(asset.FullID))
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
            AssetBase asset = GetAsset(assetID);
            if (asset == null)
                return null;

            asset.FullID = LLUUID.Random(); // TODO: check for conflicts
            AddAsset(asset);
            return asset;
        }

        /// <summary>
        /// 
        /// </summary>
        private void ProcessTextureQueue()
        {
            if (TextureRequests.Count == 0)
            {
                //no requests waiting
                return;
            }
            int num;
            num = TextureRequests.Count;

            AssetRequest req;
            for (int i = 0; i < num; i++)
            {
                req = (AssetRequest) TextureRequests[i];
                if (!SendingTextures.ContainsKey(req.ImageInfo.FullID))
                {
                    //Console.WriteLine("new texture to send");
                    TextureSender sender = new TextureSender(req);
                    //sender.OnComplete += this.TextureSent;
                    SendingTextures.Add(req.ImageInfo.FullID, sender);
                    m_queueTextures.Enqueue(sender);
                }
            }

            TextureRequests.Clear();
        }

        public void ProcessTextureSenders()
        {
            while (true)
            {
                TextureSender sender = m_queueTextures.Dequeue();

                bool finished = sender.SendTexture();
                if (finished)
                {
                    TextureSent(sender);
                }
                else
                {
                    // Console.WriteLine("readding texture");
                    m_queueTextures.Enqueue(sender);
                }
            }
        }

        /// <summary>
        /// Event handler, called by a TextureSender object to say that texture has been sent
        /// </summary>
        /// <param name="sender"></param>
        public void TextureSent(TextureSender sender)
        {
            if (SendingTextures.ContainsKey(sender.request.ImageInfo.FullID))
            {
                SendingTextures.Remove(sender.request.ImageInfo.FullID);
                // this.m_avatarReceivedTextures[sender.request.RequestUser.AgentId].Add(sender.request.ImageInfo.FullID);
            }
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
                    //Console.WriteLine("asset  recieved from asset server");

                    TextureImage image = new TextureImage(asset);
                    if (!Textures.ContainsKey(image.FullID))
                    {
                        Textures.Add(image.FullID, image);
                        if (RequestedTextures.ContainsKey(image.FullID))
                        {
                            AssetRequest req = RequestedTextures[image.FullID];
                            req.ImageInfo = image;

                            req.NumPackets = CalculateNumPackets(image.Data.Length);

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
                            if (assetInf.Data.LongLength > 600)
                            {
                                //over 600 bytes so split up file
                                req.NumPackets = 1 + (int) (assetInf.Data.Length - 600 + 999)/1000;
                            }
                            else
                            {
                                req.NumPackets = 1;
                            }
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

            //work out how many packets it  should be sent in 
            // and add to the AssetRequests list
            AssetRequest req = new AssetRequest();
            req.RequestUser = userInfo;
            req.RequestAssetID = requestID;
            req.TransferRequestID = transferRequest.TransferInfo.TransferID;
            req.AssetRequestSource = source;
            req.Params = transferRequest.TransferInfo.Params;
            req.AssetInf = asset;

            if (asset.Data.LongLength > 600)
            {
                //over 600 bytes so split up file
                req.NumPackets = 1 + (int) (asset.Data.Length - 600 + 999)/1000;
            }
            else
            {
                req.NumPackets = 1;
            }

            AssetRequests.Add(req);
        }

        /// <summary>
        /// 
        /// </summary>
        private void ProcessAssetQueue()
        {
            if (AssetRequests.Count == 0)
            {
                //no requests waiting
                return;
            }
            int num;

            if (AssetRequests.Count < 5)
            {
                //lower than 5 so do all of them
                num = AssetRequests.Count;
            }
            else
            {
                num = 5;
            }
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
                req.RequestUser.OutPacket(Transfer,ThrottleOutPacketType.Asset);

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
                    //more than one packet so split file up , for now it can't be bigger than 2000 bytes
                    TransferPacketPacket TransferPacket = new TransferPacketPacket();
                    TransferPacket.TransferData.Packet = 0;
                    TransferPacket.TransferData.ChannelType = 2;
                    TransferPacket.TransferData.TransferID = req.TransferRequestID;
                    byte[] chunk = null;
                    if (req.AssetInf.Data.Length <= 1000)
                    {
                        chunk = new byte[req.AssetInf.Data.Length];
                        Array.Copy(req.AssetInf.Data, chunk, req.AssetInf.Data.Length);
                        TransferPacket.TransferData.Data = chunk;
                        TransferPacket.TransferData.Status = 1;
                        req.RequestUser.OutPacket(TransferPacket, ThrottleOutPacketType.Asset);
                    }
                    else
                    {
                        chunk = new byte[1000];
                        Array.Copy(req.AssetInf.Data, chunk, 1000);

                        TransferPacket.TransferData.Data = chunk;
                        TransferPacket.TransferData.Status = 0;
                        req.RequestUser.OutPacket(TransferPacket, ThrottleOutPacketType.Asset);

                        TransferPacket = new TransferPacketPacket();
                        TransferPacket.TransferData.Packet = 1;
                        TransferPacket.TransferData.ChannelType = 2;
                        TransferPacket.TransferData.TransferID = req.TransferRequestID;
                        byte[] chunk1 = new byte[(req.AssetInf.Data.Length - 1000)];
                        Array.Copy(req.AssetInf.Data, 1000, chunk1, 0, chunk1.Length);
                        TransferPacket.TransferData.Data = chunk1;
                        TransferPacket.TransferData.Status = 1;
                        req.RequestUser.OutPacket(TransferPacket, ThrottleOutPacketType.Asset);
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

        #region Textures

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userInfo"></param>
        /// <param name="imageID"></param>
        public void AddTextureRequest(IClientAPI userInfo, LLUUID imageID, uint packetNumber, int discard)
        {
            // System.Console.WriteLine("texture request for " + imageID.ToStringHyphenated() + " packetnumber= " + packetNumber);
            //check to see if texture is in local cache, if not request from asset server
            if (!m_avatarReceivedTextures.ContainsKey(userInfo.AgentId))
            {
                m_avatarReceivedTextures.Add(userInfo.AgentId, new List<LLUUID>());
            }
            /* if(this.m_avatarReceivedTextures[userInfo.AgentId].Contains(imageID))
             {
                 //Console.WriteLine(userInfo.AgentId +" is requesting a image( "+ imageID+" that has already been sent to them");
                 return;
             }*/

            if (!Textures.ContainsKey(imageID))
            {
                if (!RequestedTextures.ContainsKey(imageID))
                {
                    //not is cache so request from asset server
                    AssetRequest request = new AssetRequest();
                    request.RequestUser = userInfo;
                    request.RequestAssetID = imageID;
                    request.IsTextureRequest = true;
                    request.DiscardLevel = discard;
                    RequestedTextures.Add(imageID, request);
                    m_assetServer.RequestAsset(imageID, true);
                }
                return;
            }

            // System.Console.WriteLine("texture already in cache");
            TextureImage imag = Textures[imageID];
            AssetRequest req = new AssetRequest();
            req.RequestUser = userInfo;
            req.RequestAssetID = imageID;
            req.IsTextureRequest = true;
            req.ImageInfo = imag;
            req.DiscardLevel = discard;

            req.NumPackets = CalculateNumPackets(imag.Data.Length);

            if (packetNumber != 0)
            {
                req.PacketCounter = (int) packetNumber;
            }

            TextureRequests.Add(req);
        }

        private int CalculateNumPackets(int length)
        {
            int numPackets = 1;
            
            if (length > 600)
            {
                //over 600 bytes so split up file
                int restData = (length - 600);
                int restPackets = ((restData+999)/1000);
                numPackets = 1 + restPackets;
            }

            return numPackets;
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

        public class TextureSender
        {
            public AssetRequest request;
            private int counter = 0;

            public TextureSender(AssetRequest req)
            {
                request = req;
            }

            public bool SendTexture()
            {
                SendPacket();
                counter++;

                if ((request.PacketCounter >= request.NumPackets) || counter > 100 || (request.NumPackets == 1) ||
                    (request.DiscardLevel == -1))
                {
                    return true;
                }
                return false;
            }

            public void SendPacket()
            {
                AssetRequest req = request;
                //Console.WriteLine("sending " + req.ImageInfo.FullID);
                if (req.PacketCounter == 0)
                {
                    //first time for this request so send imagedata packet
                    if (req.NumPackets == 1)
                    {
                        //Console.WriteLine("only one packet so send whole file");
                        ImageDataPacket im = new ImageDataPacket();
                        im.Header.Reliable = false;
                        im.ImageID.Packets = 1;
                        im.ImageID.ID = req.ImageInfo.FullID;
                        im.ImageID.Size = (uint) req.ImageInfo.Data.Length;
                        im.ImageData.Data = req.ImageInfo.Data;
                        im.ImageID.Codec = 2;
                        req.RequestUser.OutPacket(im, ThrottleOutPacketType.Texture);
                        req.PacketCounter++;
                        //req.ImageInfo.l= time;
                        //System.Console.WriteLine("sent texture: " + req.ImageInfo.FullID);
                        //Console.WriteLine("sending single packet for " + req.ImageInfo.FullID.ToStringHyphenated());
                    }
                    else
                    {
                        //more than one packet so split file up
                        ImageDataPacket im = new ImageDataPacket();
                        im.Header.Reliable = false;
                        im.ImageID.Packets = (ushort) (req.NumPackets);
                        im.ImageID.ID = req.ImageInfo.FullID;
                        im.ImageID.Size = (uint) req.ImageInfo.Data.Length;
                        im.ImageData.Data = new byte[600];
                        Array.Copy(req.ImageInfo.Data, 0, im.ImageData.Data, 0, 600);
                        im.ImageID.Codec = 2;
                        req.RequestUser.OutPacket(im, ThrottleOutPacketType.Texture);

                        req.PacketCounter++;
                        //req.ImageInfo.last_used = time;
                        //System.Console.WriteLine("sent first packet of texture: "  + req.ImageInfo.FullID);
                        //Console.WriteLine("sending packet 1 for " + req.ImageInfo.FullID.ToStringHyphenated());
                    }
                }
                else
                {
                    //Console.WriteLine("sending packet " + req.PacketCounter + " for " + req.ImageInfo.FullID.ToStringHyphenated());
                    //send imagepacket
                    //more than one packet so split file up
                    ImagePacketPacket im = new ImagePacketPacket();
                    im.Header.Reliable = false;
                    im.ImageID.Packet = (ushort) (req.PacketCounter);
                    im.ImageID.ID = req.ImageInfo.FullID;
                    int size = req.ImageInfo.Data.Length - 600 - (1000*(req.PacketCounter - 1));
                    if (size > 1000) size = 1000;
                    //Console.WriteLine("length= {0} counter= {1} size= {2}",req.ImageInfo.Data.Length, req.PacketCounter, size);
                    im.ImageData.Data = new byte[size];
                    Array.Copy(req.ImageInfo.Data, 600 + (1000*(req.PacketCounter - 1)), im.ImageData.Data, 0, size);
                    req.RequestUser.OutPacket(im, ThrottleOutPacketType.Texture);
                    req.PacketCounter++;
                    //req.ImageInfo.last_used = time;
                    //System.Console.WriteLine("sent a packet of texture: "+req.ImageInfo.FullID);
                }
            }

            private void SaveAssetToFile(string filename, byte[] data)
            {
                FileStream fs = File.Create(filename);
                BinaryWriter bw = new BinaryWriter(fs);
                bw.Write(data);
                bw.Close();
                fs.Close();
            }
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