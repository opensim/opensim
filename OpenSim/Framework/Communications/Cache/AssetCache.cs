/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
using System.Reflection;
using System.Threading;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;

namespace OpenSim.Framework.Communications.Caches
{
    public delegate void DownloadComplete(AssetCache.TextureSender sender);

    /// <summary>
    /// Manages local cache of assets and their sending to viewers.
    /// </summary>
    public class AssetCache : IAssetReceiver
    {
        public Dictionary<LLUUID, AssetInfo> Assets;
        public Dictionary<LLUUID, TextureImage> Textures;

        public List<AssetRequest> AssetRequests = new List<AssetRequest>();  //assets ready to be sent to viewers
        public List<AssetRequest> TextureRequests = new List<AssetRequest>(); //textures ready to be sent

        public Dictionary<LLUUID, AssetRequest> RequestedAssets = new Dictionary<LLUUID, AssetRequest>(); //Assets requested from the asset server
        public Dictionary<LLUUID, AssetRequest> RequestedTextures = new Dictionary<LLUUID, AssetRequest>(); //Textures requested from the asset server

        public Dictionary<LLUUID, TextureSender> SendingTextures = new Dictionary<LLUUID, TextureSender>();
        private BlockingQueue<TextureSender> QueueTextures = new BlockingQueue<TextureSender>();

        private Dictionary<LLUUID, List<LLUUID>> AvatarRecievedTextures = new Dictionary<LLUUID, List<LLUUID>>();

        private Dictionary<LLUUID, Dictionary<LLUUID, int>> TimesTextureSent = new Dictionary<LLUUID, Dictionary<LLUUID, int>>();

        private IAssetServer _assetServer;
        private Thread _assetCacheThread;

        private Thread TextureSenderThread;

        /// <summary>
        /// 
        /// </summary>
        public AssetCache(IAssetServer assetServer)
        {
            System.Console.WriteLine("Creating Asset cache");
            _assetServer = assetServer;
            _assetServer.SetReceiver(this);
            Assets = new Dictionary<LLUUID, AssetInfo>();
            Textures = new Dictionary<LLUUID, TextureImage>();
            this._assetCacheThread = new Thread(new ThreadStart(RunAssetManager));
            this._assetCacheThread.IsBackground = true;
            this._assetCacheThread.Start();

            this.TextureSenderThread = new Thread(new ThreadStart(this.ProcessTextureSenders));
            this.TextureSenderThread.IsBackground = true;
            this.TextureSenderThread.Start();

        }

        public AssetCache(string assetServerDLLName, string assetServerURL, string assetServerKey)
        {
            System.Console.WriteLine("Creating Asset cache");
            _assetServer = this.LoadAssetDll(assetServerDLLName);
            _assetServer.SetServerInfo(assetServerURL, assetServerKey);
            _assetServer.SetReceiver(this);
            Assets = new Dictionary<LLUUID, AssetInfo>();
            Textures = new Dictionary<LLUUID, TextureImage>();
            this._assetCacheThread = new Thread(new ThreadStart(RunAssetManager));
            this._assetCacheThread.IsBackground = true;
            this._assetCacheThread.Start();

            this.TextureSenderThread = new Thread(new ThreadStart(this.ProcessTextureSenders));
            this.TextureSenderThread.IsBackground = true;
            this.TextureSenderThread.Start();
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
                    this.ProcessAssetQueue();
                    this.ProcessTextureQueue();
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
            if (this.Textures.ContainsKey(assetID))
            {
                asset = this.Textures[assetID];
            }
            else if (this.Assets.ContainsKey(assetID))
            {
                asset = this.Assets[assetID];
            }
            return asset;
        }

        public AssetBase GetAsset(LLUUID assetID, bool isTexture)
        {
            AssetBase asset = GetAsset(assetID);
            if (asset == null)
            {
                this._assetServer.FetchAsset(assetID, isTexture);
            }
            return asset;
        }

        public void AddAsset(AssetBase asset)
        {
            // System.Console.WriteLine("adding asset " + asset.FullID.ToStringHyphenated());
            if (asset.Type == 0)
            {
                //Console.WriteLine("which is a texture");
                if (!this.Textures.ContainsKey(asset.FullID))
                { //texture
                    TextureImage textur = new TextureImage(asset);
                    this.Textures.Add(textur.FullID, textur);
                    this._assetServer.CreateAsset(asset);
                }
                else
                {
                    TextureImage textur = new TextureImage(asset);
                    this.Textures[asset.FullID] = textur;
                }
            }
            else
            {
                if (!this.Assets.ContainsKey(asset.FullID))
                {
                    AssetInfo assetInf = new AssetInfo(asset);
                    this.Assets.Add(assetInf.FullID, assetInf);
                    this._assetServer.CreateAsset(asset);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void ProcessTextureQueue()
        {
            if (this.TextureRequests.Count == 0)
            {
                //no requests waiting
                return;
            }
            int num;
            num = this.TextureRequests.Count;

            AssetRequest req;
            for (int i = 0; i < num; i++)
            {
                req = (AssetRequest)this.TextureRequests[i];
                if (!this.SendingTextures.ContainsKey(req.ImageInfo.FullID))
                {
                    //Console.WriteLine("new texture to send");
                    TextureSender sender = new TextureSender(req);
                    //sender.OnComplete += this.TextureSent;
                    lock (this.SendingTextures)
                    {
                        this.SendingTextures.Add(req.ImageInfo.FullID, sender);
                    }
                    this.QueueTextures.Enqueue(sender);
                }

            }

            this.TextureRequests.Clear();
        }

        public void ProcessTextureSenders()
        {
            while (true)
            {
                TextureSender sender = this.QueueTextures.Dequeue();
                /* if (TimesTextureSent.ContainsKey(sender.request.RequestUser.AgentId))
                 {
                     if (TimesTextureSent[sender.request.RequestUser.AgentId].ContainsKey(sender.request.ImageInfo.FullID))
                     {
                         TimesTextureSent[sender.request.RequestUser.AgentId][sender.request.ImageInfo.FullID]++;
                     }
                     else
                     {
                          TimesTextureSent[sender.request.RequestUser.AgentId].Add(sender.request.ImageInfo.FullID, 1);
                     }
                 }
                 else
                 {
                     Dictionary<LLUUID, int> UsersSent = new Dictionary<LLUUID,int>();
                     TimesTextureSent.Add(sender.request.RequestUser.AgentId, UsersSent );
                     UsersSent.Add(sender.request.ImageInfo.FullID, 1);
                   
                 }
                 if (TimesTextureSent[sender.request.RequestUser.AgentId][sender.request.ImageInfo.FullID] < 1000)
                 {*/
                bool finished = sender.SendTexture();
                if (finished)
                {
                    this.TextureSent(sender);
                }
                else
                {
                    // Console.WriteLine("readding texture");
                    this.QueueTextures.Enqueue(sender);
                }
                /*  }
                  else
                  {
                      this.TextureSent(sender);
                  }*/
            }
        }

        /// <summary>
        /// Event handler, called by a TextureSender object to say that texture has been sent
        /// </summary>
        /// <param name="sender"></param>
        public void TextureSent(TextureSender sender)
        {
            if (this.SendingTextures.ContainsKey(sender.request.ImageInfo.FullID))
            {
                lock (this.SendingTextures)
                {
                    this.SendingTextures.Remove(sender.request.ImageInfo.FullID);
                    // this.AvatarRecievedTextures[sender.request.RequestUser.AgentId].Add(sender.request.ImageInfo.FullID);
                }
            }
        }

        public void AssetReceived(AssetBase asset, bool IsTexture)
        {
            if (asset.FullID != LLUUID.Zero)  // if it is set to zero then the asset wasn't found by the server
            {
                //check if it is a texture or not
                //then add to the correct cache list
                //then check for waiting requests for this asset/texture (in the Requested lists)
                //and move those requests into the Requests list.

                if (IsTexture)
                {
                    //Console.WriteLine("asset  recieved from asset server");

                    TextureImage image = new TextureImage(asset);
                    if (!this.Textures.ContainsKey(image.FullID))
                    {
                        this.Textures.Add(image.FullID, image);
                        if (this.RequestedTextures.ContainsKey(image.FullID))
                        {
                            AssetRequest req = this.RequestedTextures[image.FullID];
                            req.ImageInfo = image;
                            if (image.Data.LongLength > 600)
                            {
                                //over 600 bytes so split up file
                                req.NumPackets = 1 + (int)(image.Data.Length - 600) / 1000;
                            }
                            else
                            {
                                req.NumPackets = 1;
                            }
                            this.RequestedTextures.Remove(image.FullID);
                            this.TextureRequests.Add(req);
                        }
                    }
                }
                else
                {
                    AssetInfo assetInf = new AssetInfo(asset);
                    if (!this.Assets.ContainsKey(assetInf.FullID))
                    {
                        this.Assets.Add(assetInf.FullID, assetInf);
                        if (this.RequestedAssets.ContainsKey(assetInf.FullID))
                        {
                            AssetRequest req = this.RequestedAssets[assetInf.FullID];
                            req.AssetInf = assetInf;
                            if (assetInf.Data.LongLength > 600)
                            {
                                //over 600 bytes so split up file
                                req.NumPackets = 1 + (int)(assetInf.Data.Length - 600 + 999) / 1000;
                            }
                            else
                            {
                                req.NumPackets = 1;
                            }
                            this.RequestedAssets.Remove(assetInf.FullID);
                            this.AssetRequests.Add(req);
                        }
                    }
                }
            }
        }

        public void AssetNotFound(LLUUID assetID)
        {
            if (this.RequestedTextures.ContainsKey(assetID))
            {
                AssetRequest req = this.RequestedTextures[assetID];
                ImageNotInDatabasePacket notFound = new ImageNotInDatabasePacket();
                notFound.ImageID.ID = assetID;
                req.RequestUser.OutPacket(notFound);
                //Console.WriteLine("sending image not found for " + assetID);

                this.RequestedTextures.Remove(assetID);
            }
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
            if (!this.Assets.ContainsKey(requestID))
            {
                //not found asset	
                // so request from asset server
                if (!this.RequestedAssets.ContainsKey(requestID))
                {
                    AssetRequest request = new AssetRequest();
                    request.RequestUser = userInfo;
                    request.RequestAssetID = requestID;
                    request.TransferRequestID = transferRequest.TransferInfo.TransferID;
                    request.AssetRequestSource = source;
                    request.Params = transferRequest.TransferInfo.Params;
                    this.RequestedAssets.Add(requestID, request);
                    this._assetServer.FetchAsset(requestID, false);
                }
                return;
            }
            //it is in our cache 
            AssetInfo asset = this.Assets[requestID];

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
                req.NumPackets = 1 + (int)(asset.Data.Length - 600 + 999) / 1000;
            }
            else
            {
                req.NumPackets = 1;
            }

            this.AssetRequests.Add(req);
        }

        /// <summary>
        /// 
        /// </summary>
        private void ProcessAssetQueue()
        {
            if (this.AssetRequests.Count == 0)
            {
                //no requests waiting
                return;
            }
            int num;

            if (this.AssetRequests.Count < 5)
            {
                //lower than 5 so do all of them
                num = this.AssetRequests.Count;
            }
            else
            {
                num = 5;
            }
            AssetRequest req;
            for (int i = 0; i < num; i++)
            {
                req = (AssetRequest)this.AssetRequests[i];
                //Console.WriteLine("sending asset " + req.RequestAssetID);
                TransferInfoPacket Transfer = new TransferInfoPacket();
                Transfer.TransferInfo.ChannelType = 2;
                Transfer.TransferInfo.Status = 0;
                Transfer.TransferInfo.TargetType = 0;
                if (req.AssetRequestSource == 2)
                {
                    Transfer.TransferInfo.Params = new byte[20];
                    Array.Copy(req.RequestAssetID.GetBytes(), 0, Transfer.TransferInfo.Params, 0, 16);
                    int assType = (int)req.AssetInf.Type;
                    Array.Copy(Helpers.IntToBytes(assType), 0, Transfer.TransferInfo.Params, 16, 4);
                }
                else if (req.AssetRequestSource == 3)
                {
                    Transfer.TransferInfo.Params = req.Params;
                    // Transfer.TransferInfo.Params = new byte[100];
                    //Array.Copy(req.RequestUser.AgentId.GetBytes(), 0, Transfer.TransferInfo.Params, 0, 16);
                    //Array.Copy(req.RequestUser.SessionId.GetBytes(), 0, Transfer.TransferInfo.Params, 16, 16);
                }
                Transfer.TransferInfo.Size = (int)req.AssetInf.Data.Length;
                Transfer.TransferInfo.TransferID = req.TransferRequestID;
                req.RequestUser.OutPacket(Transfer);

                if (req.NumPackets == 1)
                {
                    TransferPacketPacket TransferPacket = new TransferPacketPacket();
                    TransferPacket.TransferData.Packet = 0;
                    TransferPacket.TransferData.ChannelType = 2;
                    TransferPacket.TransferData.TransferID = req.TransferRequestID;
                    TransferPacket.TransferData.Data = req.AssetInf.Data;
                    TransferPacket.TransferData.Status = 1;
                    req.RequestUser.OutPacket(TransferPacket);
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
                        req.RequestUser.OutPacket(TransferPacket);
                    }
                    else
                    {
                        chunk = new byte[1000];
                        Array.Copy(req.AssetInf.Data, chunk, 1000);

                        TransferPacket.TransferData.Data = chunk;
                        TransferPacket.TransferData.Status = 0;
                        req.RequestUser.OutPacket(TransferPacket);

                        TransferPacket = new TransferPacketPacket();
                        TransferPacket.TransferData.Packet = 1;
                        TransferPacket.TransferData.ChannelType = 2;
                        TransferPacket.TransferData.TransferID = req.TransferRequestID;
                        byte[] chunk1 = new byte[(req.AssetInf.Data.Length - 1000)];
                        Array.Copy(req.AssetInf.Data, 1000, chunk1, 0, chunk1.Length);
                        TransferPacket.TransferData.Data = chunk1;
                        TransferPacket.TransferData.Status = 1;
                        req.RequestUser.OutPacket(TransferPacket);
                    }
                }

            }

            //remove requests that have been completed
            for (int i = 0; i < num; i++)
            {
                this.AssetRequests.RemoveAt(0);
            }

        }

        public AssetInfo CloneAsset(LLUUID newOwner, AssetInfo sourceAsset)
        {
            AssetInfo newAsset = new AssetInfo();
            newAsset.Data = new byte[sourceAsset.Data.Length];
            Array.Copy(sourceAsset.Data, newAsset.Data, sourceAsset.Data.Length);
            newAsset.FullID = LLUUID.Random();
            newAsset.Type = sourceAsset.Type;
            newAsset.InvType = sourceAsset.InvType;
            return (newAsset);
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
            //Console.WriteLine("texture request for " + imageID.ToStringHyphenated() + " packetnumber= " + packetNumber);
            //check to see if texture is in local cache, if not request from asset server
            if (!this.AvatarRecievedTextures.ContainsKey(userInfo.AgentId))
            {
                this.AvatarRecievedTextures.Add(userInfo.AgentId, new List<LLUUID>());
            }
            /* if(this.AvatarRecievedTextures[userInfo.AgentId].Contains(imageID))
             {
                 //Console.WriteLine(userInfo.AgentId +" is requesting a image( "+ imageID+" that has already been sent to them");
                 return;
             }*/
            if (!this.Textures.ContainsKey(imageID))
            {
                if (!this.RequestedTextures.ContainsKey(imageID))
                {
                    //not is cache so request from asset server
                    AssetRequest request = new AssetRequest();
                    request.RequestUser = userInfo;
                    request.RequestAssetID = imageID;
                    request.IsTextureRequest = true;
                    request.DiscardLevel = discard;
                    this.RequestedTextures.Add(imageID, request);
                    this._assetServer.FetchAsset(imageID, true);
                }
                return;
            }

            //Console.WriteLine("texture already in cache");
            TextureImage imag = this.Textures[imageID];
            AssetRequest req = new AssetRequest();
            req.RequestUser = userInfo;
            req.RequestAssetID = imageID;
            req.IsTextureRequest = true;
            req.ImageInfo = imag;
            req.DiscardLevel = discard;

            if (imag.Data.LongLength > 600)
            {
                //Console.WriteLine("{0}", imag.Data.LongLength);
                //over 600 bytes so split up file
                req.NumPackets = 2 + (int)(imag.Data.Length - 601) / 1000;
                //Console.WriteLine("texture is " + imag.Data.Length + " which we will send in " +req.NumPackets +" packets");
            }
            else
            {
                req.NumPackets = 1;
            }
            if (packetNumber != 0)
            {
                req.PacketCounter = (int)packetNumber;
            }
            this.TextureRequests.Add(req);
        }

        public TextureImage CloneImage(LLUUID newOwner, TextureImage source)
        {
            TextureImage newImage = new TextureImage();
            newImage.Data = new byte[source.Data.Length];
            Array.Copy(source.Data, newImage.Data, source.Data.Length);
            //newImage.filename = source.filename;
            newImage.FullID = LLUUID.Random();
            newImage.Name = source.Name;
            return (newImage);
        }
        #endregion

        private IAssetServer LoadAssetDll(string dllName)
        {
            Assembly pluginAssembly = Assembly.LoadFrom(dllName);
            IAssetServer server = null;

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("IAssetPlugin", true);

                        if (typeInterface != null)
                        {
                            IAssetPlugin plug = (IAssetPlugin)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            server = plug.GetAssetServer();
                            break;
                        }

                        typeInterface = null;
                    }
                }
            }
            pluginAssembly = null;
            return server;
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

                if ((request.PacketCounter >= request.NumPackets) | counter > 100 | (request.NumPackets == 1) | (request.DiscardLevel == -1))
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
                        im.ImageID.Size = (uint)req.ImageInfo.Data.Length;
                        im.ImageData.Data = req.ImageInfo.Data;
                        im.ImageID.Codec = 2;
                        req.RequestUser.OutPacket(im);
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
                        im.ImageID.Packets = (ushort)(req.NumPackets);
                        im.ImageID.ID = req.ImageInfo.FullID;
                        im.ImageID.Size = (uint)req.ImageInfo.Data.Length;
                        im.ImageData.Data = new byte[600];
                        Array.Copy(req.ImageInfo.Data, 0, im.ImageData.Data, 0, 600);
                        im.ImageID.Codec = 2;
                        req.RequestUser.OutPacket(im);

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
                    im.ImageID.Packet = (ushort)(req.PacketCounter);
                    im.ImageID.ID = req.ImageInfo.FullID;
                    int size = req.ImageInfo.Data.Length - 600 - (1000 * (req.PacketCounter - 1));
                    if (size > 1000) size = 1000;
                    //Console.WriteLine("length= {0} counter= {1} size= {2}",req.ImageInfo.Data.Length, req.PacketCounter, size);
                    im.ImageData.Data = new byte[size];
                    Array.Copy(req.ImageInfo.Data, 600 + (1000 * (req.PacketCounter - 1)), im.ImageData.Data, 0, size);
                    req.RequestUser.OutPacket(im);
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
}

