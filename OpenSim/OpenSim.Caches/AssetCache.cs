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
using System.Threading;
using System.Reflection;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;

namespace OpenSim.Caches
{
    /// <summary>
    /// Manages local cache of assets and their sending to viewers.
    /// </summary>
    public class AssetCache : IAssetReceiver
    {
        public Dictionary<libsecondlife.LLUUID, AssetInfo> Assets;
        public Dictionary<libsecondlife.LLUUID, TextureImage> Textures;

        public List<AssetRequest> AssetRequests = new List<AssetRequest>();  //assets ready to be sent to viewers
        public List<AssetRequest> TextureRequests = new List<AssetRequest>(); //textures ready to be sent

        public Dictionary<LLUUID, AssetRequest> RequestedAssets = new Dictionary<LLUUID, AssetRequest>(); //Assets requested from the asset server
        public Dictionary<LLUUID, AssetRequest> RequestedTextures = new Dictionary<LLUUID, AssetRequest>(); //Textures requested from the asset server

        private IAssetServer _assetServer;
        private Thread _assetCacheThread;
        private LLUUID[] textureList = new LLUUID[5];

        /// <summary>
        /// 
        /// </summary>
        public AssetCache(IAssetServer assetServer)
        {
            Console.WriteLine("Creating Asset cache");
            _assetServer = assetServer;
            _assetServer.SetReceiver(this);
            Assets = new Dictionary<libsecondlife.LLUUID, AssetInfo>();
            Textures = new Dictionary<libsecondlife.LLUUID, TextureImage>();
            this._assetCacheThread = new Thread(new ThreadStart(RunAssetManager));
            this._assetCacheThread.IsBackground = true;
            this._assetCacheThread.Start();

        }

        public AssetCache(string assetServerDLLName, string assetServerURL, string assetServerKey)
        {
            Console.WriteLine("Creating Asset cache");
            _assetServer = this.LoadAssetDll(assetServerDLLName);
            _assetServer.SetServerInfo(assetServerURL, assetServerKey);
            _assetServer.SetReceiver(this);
            Assets = new Dictionary<libsecondlife.LLUUID, AssetInfo>();
            Textures = new Dictionary<libsecondlife.LLUUID, TextureImage>();
            this._assetCacheThread = new Thread(new ThreadStart(RunAssetManager));
            this._assetCacheThread.IsBackground = true;
            this._assetCacheThread.Start();

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
                    //Console.WriteLine("Asset cache loop");
                    this.ProcessAssetQueue();
                    this.ProcessTextureQueue();
                    Thread.Sleep(500);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        public void LoadDefaultTextureSet()
        {
            //hack: so we can give each user a set of textures
            textureList[0] = new LLUUID("00000000-0000-0000-9999-000000000001");
            textureList[1] = new LLUUID("00000000-0000-0000-9999-000000000002");
            textureList[2] = new LLUUID("00000000-0000-0000-9999-000000000003");
            textureList[3] = new LLUUID("00000000-0000-0000-9999-000000000004");
            textureList[4] = new LLUUID("00000000-0000-0000-9999-000000000005");

            for (int i = 0; i < textureList.Length; i++)
            {
                this._assetServer.RequestAsset(textureList[i], true);
            }

        }

        public AssetBase[] CreateNewInventorySet(LLUUID agentID)
        {
            AssetBase[] inventorySet = new AssetBase[this.textureList.Length];
            for (int i = 0; i < textureList.Length; i++)
            {
                if (this.Textures.ContainsKey(textureList[i]))
                {
                    inventorySet[i] = this.CloneImage(agentID, this.Textures[textureList[i]]);
                    TextureImage image = new TextureImage(inventorySet[i]);
                    this.Textures.Add(image.FullID, image);
                    this._assetServer.UploadNewAsset(image); //save the asset to the asset server
                }
            }
            return inventorySet;
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

        public void AddAsset(AssetBase asset)
        {
            if (asset.Type == 0)
            {
                if (!this.Textures.ContainsKey(asset.FullID))
                { //texture
                    TextureImage textur = new TextureImage(asset);
                    this.Textures.Add(textur.FullID, textur);
                    this._assetServer.UploadNewAsset(asset);
                }
            }
            else
            {
                if (!this.Assets.ContainsKey(asset.FullID))
                {
                    AssetInfo assetInf = new AssetInfo(asset);
                    this.Assets.Add(assetInf.FullID, assetInf);
                    this._assetServer.UploadNewAsset(asset);
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

            if (this.TextureRequests.Count < 5)
            {
                //lower than 5 so do all of them
                num = this.TextureRequests.Count;
            }
            else
            {
                num = 5;
            }
            AssetRequest req;
            for (int i = 0; i < num; i++)
            {
                req = (AssetRequest)this.TextureRequests[i];
                if (req.PacketCounter != req.NumPackets)
                {
                    // if (req.ImageInfo.FullID == new LLUUID("00000000-0000-0000-5005-000000000005"))
                    if (req.PacketCounter == 0)
                    {
                        //first time for this request so send imagedata packet
                        if (req.NumPackets == 1)
                        {
                            //only one packet so send whole file
                            ImageDataPacket im = new ImageDataPacket();
                            im.ImageID.Packets = 1;
                            im.ImageID.ID = req.ImageInfo.FullID;
                            im.ImageID.Size = (uint)req.ImageInfo.Data.Length;
                            im.ImageData.Data = req.ImageInfo.Data;
                            im.ImageID.Codec = 2;
                            req.RequestUser.OutPacket(im);
                            req.PacketCounter++;
                            //req.ImageInfo.l= time;
                            //System.Console.WriteLine("sent texture: "+req.image_info.FullID);
                        }
                        else
                        {
                            //more than one packet so split file up
                            ImageDataPacket im = new ImageDataPacket();
                            im.ImageID.Packets = (ushort)req.NumPackets;
                            im.ImageID.ID = req.ImageInfo.FullID;
                            im.ImageID.Size = (uint)req.ImageInfo.Data.Length;
                            im.ImageData.Data = new byte[600];
                            Array.Copy(req.ImageInfo.Data, 0, im.ImageData.Data, 0, 600);
                            im.ImageID.Codec = 2;
                            req.RequestUser.OutPacket(im);
                            req.PacketCounter++;
                            //req.ImageInfo.last_used = time;
                            //System.Console.WriteLine("sent first packet of texture:
                        }
                    }
                    else
                    {
                        //send imagepacket
                        //more than one packet so split file up
                        ImagePacketPacket im = new ImagePacketPacket();
                        im.ImageID.Packet = (ushort)req.PacketCounter;
                        im.ImageID.ID = req.ImageInfo.FullID;
                        int size = req.ImageInfo.Data.Length - 600 - 1000 * (req.PacketCounter - 1);
                        if (size > 1000) size = 1000;
                        im.ImageData.Data = new byte[size];
                        Array.Copy(req.ImageInfo.Data, 600 + 1000 * (req.PacketCounter - 1), im.ImageData.Data, 0, size);
                        req.RequestUser.OutPacket(im);
                        req.PacketCounter++;
                        //req.ImageInfo.last_used = time;
                        //System.Console.WriteLine("sent a packet of texture: "+req.image_info.FullID);
                    }
                }
            }

            //remove requests that have been completed
            int count = 0;
            for (int i = 0; i < num; i++)
            {
                if (this.TextureRequests.Count > count)
                {
                    req = (AssetRequest)this.TextureRequests[count];
                    if (req.PacketCounter == req.NumPackets)
                    {
                        this.TextureRequests.Remove(req);
                    }
                    else
                    {
                        count++;
                    }
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
                    TextureImage image = new TextureImage(asset);
                    this.Textures.Add(image.FullID, image);
                    if (this.RequestedTextures.ContainsKey(image.FullID))
                    {
                        AssetRequest req = this.RequestedTextures[image.FullID];
                        req.ImageInfo = image;
                        if (image.Data.LongLength > 600)
                        {
                            //over 600 bytes so split up file
                            req.NumPackets = 1 + (int)(image.Data.Length - 600 + 999) / 1000;
                        }
                        else
                        {
                            req.NumPackets = 1;
                        }
                        this.RequestedTextures.Remove(image.FullID);
                        this.TextureRequests.Add(req);
                    }
                }
                else
                {
                    AssetInfo assetInf = new AssetInfo(asset);
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

        public void AssetNotFound(AssetBase asset)
        {
            //the asset server had no knowledge of requested asset

        }

        #region Assets
        /// <summary>
        /// 
        /// </summary>
        /// <param name="userInfo"></param>
        /// <param name="transferRequest"></param>
        public void AddAssetRequest(IClientAPI userInfo, TransferRequestPacket transferRequest)
        {
            LLUUID requestID = new LLUUID(transferRequest.TransferInfo.Params, 0);
            //check to see if asset is in local cache, if not we need to request it from asset server.
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
                    this.RequestedAssets.Add(requestID, request);
                    this._assetServer.RequestAsset(requestID, false);
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

                TransferInfoPacket Transfer = new TransferInfoPacket();
                Transfer.TransferInfo.ChannelType = 2;
                Transfer.TransferInfo.Status = 0;
                Transfer.TransferInfo.TargetType = 0;
                Transfer.TransferInfo.Params = req.RequestAssetID.GetBytes();
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
                    byte[] chunk = new byte[1000];
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
        public void AddTextureRequest(IClientAPI userInfo, LLUUID imageID)
        {
            //check to see if texture is in local cache, if not request from asset server
            if (!this.Textures.ContainsKey(imageID))
            {
                if (!this.RequestedTextures.ContainsKey(imageID))
                {
                    //not is cache so request from asset server
                    AssetRequest request = new AssetRequest();
                    request.RequestUser = userInfo;
                    request.RequestAssetID = imageID;
                    request.IsTextureRequest = true;
                    this.RequestedTextures.Add(imageID, request);
                    this._assetServer.RequestAsset(imageID, true);
                }
                return;
            }

            TextureImage imag = this.Textures[imageID];
            AssetRequest req = new AssetRequest();
            req.RequestUser = userInfo;
            req.RequestAssetID = imageID;
            req.IsTextureRequest = true;
            req.ImageInfo = imag;

            if (imag.Data.LongLength > 600)
            {
                //over 600 bytes so split up file
                req.NumPackets = 1 + (int)(imag.Data.Length - 600 + 999) / 1000;
            }
            else
            {
                req.NumPackets = 1;
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
            //public bool AssetInCache;
            //public int TimeRequested; 

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
    }
}

