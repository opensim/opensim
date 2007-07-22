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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Threading;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;

namespace OpenSim.Framework.Communications.Caches
{
    public delegate void DownloadComplete(AssetCache.TextureSender sender);

    public class AssetCache : IAssetReceiver
    {
        // Fields
        private Thread _assetCacheThread;
        private IAssetServer _assetServer;
        public List<AssetRequest> AssetRequests;
        public Dictionary<LLUUID, AssetInfo> Assets;
        public Dictionary<LLUUID, AssetRequest> RequestedAssets;
        public Dictionary<LLUUID, AssetRequest> RequestedTextures;
        public Dictionary<LLUUID, TextureSender> SendingTextures;
        private LLUUID[] textureList;
        public List<AssetRequest> TextureRequests;
        public Dictionary<LLUUID, TextureImage> Textures;

        // Methods
        public AssetCache(IAssetServer assetServer)
        {
            this.AssetRequests = new List<AssetRequest>();
            this.TextureRequests = new List<AssetRequest>();
            this.RequestedAssets = new Dictionary<LLUUID, AssetRequest>();
            this.RequestedTextures = new Dictionary<LLUUID, AssetRequest>();
            this.SendingTextures = new Dictionary<LLUUID, TextureSender>();
            this.textureList = new LLUUID[5];
            Console.WriteLine("Creating Asset cache");
            this._assetServer = assetServer;
            this._assetServer.SetReceiver(this);
            this.Assets = new Dictionary<LLUUID, AssetInfo>();
            this.Textures = new Dictionary<LLUUID, TextureImage>();
            this._assetCacheThread = new Thread(new ThreadStart(this.RunAssetManager));
            this._assetCacheThread.IsBackground = true;
            this._assetCacheThread.Start();
        }

        public AssetCache(string assetServerDLLName, string assetServerURL, string assetServerKey)
        {
            this.AssetRequests = new List<AssetRequest>();
            this.TextureRequests = new List<AssetRequest>();
            this.RequestedAssets = new Dictionary<LLUUID, AssetRequest>();
            this.RequestedTextures = new Dictionary<LLUUID, AssetRequest>();
            this.SendingTextures = new Dictionary<LLUUID, TextureSender>();
            this.textureList = new LLUUID[5];
            Console.WriteLine("Creating Asset cache");
            this._assetServer = this.LoadAssetDll(assetServerDLLName);
            this._assetServer.SetServerInfo(assetServerURL, assetServerKey);
            this._assetServer.SetReceiver(this);
            this.Assets = new Dictionary<LLUUID, AssetInfo>();
            this.Textures = new Dictionary<LLUUID, TextureImage>();
            this._assetCacheThread = new Thread(new ThreadStart(this.RunAssetManager));
            this._assetCacheThread.IsBackground = true;
            this._assetCacheThread.Start();
        }

        public void AddAsset(AssetBase asset)
        {
            if (asset.Type == 0)
            {
                if (!this.Textures.ContainsKey(asset.FullID))
                {
                    TextureImage image = new TextureImage(asset);
                    this.Textures.Add(image.FullID, image);
                    this._assetServer.UploadNewAsset(asset);
                }
            }
            else if (!this.Assets.ContainsKey(asset.FullID))
            {
                AssetInfo info = new AssetInfo(asset);
                this.Assets.Add(info.FullID, info);
                this._assetServer.UploadNewAsset(asset);
            }
        }

        public void AddAssetRequest(IClientAPI userInfo, TransferRequestPacket transferRequest)
        {
            LLUUID assetID = new LLUUID(transferRequest.TransferInfo.Params, 0);
            if (!this.Assets.ContainsKey(assetID))
            {
                if (!this.RequestedAssets.ContainsKey(assetID))
                {
                    AssetRequest request = new AssetRequest();
                    request.RequestUser = userInfo;
                    request.RequestAssetID = assetID;
                    request.TransferRequestID = transferRequest.TransferInfo.TransferID;
                    this.RequestedAssets.Add(assetID, request);
                    this._assetServer.RequestAsset(assetID, false);
                }
            }
            else
            {
                AssetInfo info = this.Assets[assetID];
                AssetRequest request2 = new AssetRequest();
                request2.RequestUser = userInfo;
                request2.RequestAssetID = assetID;
                request2.TransferRequestID = transferRequest.TransferInfo.TransferID;
                request2.AssetInf = info;
                if (info.Data.LongLength > 600)
                {
                    request2.NumPackets = 1 + (((info.Data.Length - 600) + 0x3e7) / 0x3e8);
                }
                else
                {
                    request2.NumPackets = 1;
                }
                this.AssetRequests.Add(request2);
            }
        }

        public void AddTextureRequest(IClientAPI userInfo, LLUUID imageID)
        {
            if (!this.Textures.ContainsKey(imageID))
            {
                if (!this.RequestedTextures.ContainsKey(imageID))
                {
                    AssetRequest request = new AssetRequest();
                    request.RequestUser = userInfo;
                    request.RequestAssetID = imageID;
                    request.IsTextureRequest = true;
                    this.RequestedTextures.Add(imageID, request);
                    this._assetServer.RequestAsset(imageID, true);
                }
            }
            else
            {
                TextureImage image = this.Textures[imageID];
                AssetRequest request2 = new AssetRequest();
                request2.RequestUser = userInfo;
                request2.RequestAssetID = imageID;
                request2.IsTextureRequest = true;
                request2.ImageInfo = image;
                if (image.Data.LongLength > 600)
                {
                    request2.NumPackets = 1 + (((image.Data.Length - 600) + 0x3e7) / 0x3e8);
                }
                else
                {
                    request2.NumPackets = 1;
                }
                this.TextureRequests.Add(request2);
            }
        }

        public void AssetNotFound(AssetBase asset)
        {
        }

        public void AssetReceived(AssetBase asset, bool IsTexture)
        {
            if (asset.FullID != LLUUID.Zero)
            {
                if (IsTexture)
                {
                    TextureImage image = new TextureImage(asset);
                    this.Textures.Add(image.FullID, image);
                    if (this.RequestedTextures.ContainsKey(image.FullID))
                    {
                        AssetRequest request = this.RequestedTextures[image.FullID];
                        request.ImageInfo = image;
                        if (image.Data.LongLength > 600)
                        {
                            request.NumPackets = 1 + (((image.Data.Length - 600) + 0x3e7) / 0x3e8);
                        }
                        else
                        {
                            request.NumPackets = 1;
                        }
                        this.RequestedTextures.Remove(image.FullID);
                        this.TextureRequests.Add(request);
                    }
                }
                else
                {
                    AssetInfo info = new AssetInfo(asset);
                    this.Assets.Add(info.FullID, info);
                    if (this.RequestedAssets.ContainsKey(info.FullID))
                    {
                        AssetRequest request2 = this.RequestedAssets[info.FullID];
                        request2.AssetInf = info;
                        if (info.Data.LongLength > 600)
                        {
                            request2.NumPackets = 1 + (((info.Data.Length - 600) + 0x3e7) / 0x3e8);
                        }
                        else
                        {
                            request2.NumPackets = 1;
                        }
                        this.RequestedAssets.Remove(info.FullID);
                        this.AssetRequests.Add(request2);
                    }
                }
            }
        }

        public AssetInfo CloneAsset(LLUUID newOwner, AssetInfo sourceAsset)
        {
            AssetInfo info = new AssetInfo();
            info.Data = new byte[sourceAsset.Data.Length];
            Array.Copy(sourceAsset.Data, info.Data, sourceAsset.Data.Length);
            info.FullID = LLUUID.Random();
            info.Type = sourceAsset.Type;
            info.InvType = sourceAsset.InvType;
            return info;
        }

        public TextureImage CloneImage(LLUUID newOwner, TextureImage source)
        {
            TextureImage image = new TextureImage();
            image.Data = new byte[source.Data.Length];
            Array.Copy(source.Data, image.Data, source.Data.Length);
            image.FullID = LLUUID.Random();
            image.Name = source.Name;
            return image;
        }

        public AssetBase[] CreateNewInventorySet(LLUUID agentID)
        {
            AssetBase[] baseArray = new AssetBase[this.textureList.Length];
            for (int i = 0; i < this.textureList.Length; i++)
            {
                if (this.Textures.ContainsKey(this.textureList[i]))
                {
                    baseArray[i] = this.CloneImage(agentID, this.Textures[this.textureList[i]]);
                    TextureImage asset = new TextureImage(baseArray[i]);
                    this.Textures.Add(asset.FullID, asset);
                    this._assetServer.UploadNewAsset(asset);
                }
            }
            return baseArray;
        }

        public AssetBase GetAsset(LLUUID assetID)
        {
            AssetBase base2 = null;
            if (this.Textures.ContainsKey(assetID))
            {
                return this.Textures[assetID];
            }
            if (this.Assets.ContainsKey(assetID))
            {
                base2 = this.Assets[assetID];
            }
            return base2;
        }

        private IAssetServer LoadAssetDll(string dllName)
        {
            Assembly assembly = Assembly.LoadFrom(dllName);
            IAssetServer assetServer = null;
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsPublic && !type.IsAbstract)
                {
                    if (type.GetInterface("IAssetPlugin", true) != null)
                    {
                        assetServer = ((IAssetPlugin)Activator.CreateInstance(assembly.GetType(type.ToString()))).GetAssetServer();
                        break;
                    }
                }
            }
            assembly = null;
            return assetServer;
        }

        public void LoadDefaultTextureSet()
        {
            this.textureList[0] = new LLUUID("00000000-0000-0000-9999-000000000001");
            this.textureList[1] = new LLUUID("00000000-0000-0000-9999-000000000002");
            this.textureList[2] = new LLUUID("00000000-0000-0000-9999-000000000003");
            this.textureList[3] = new LLUUID("00000000-0000-0000-9999-000000000004");
            this.textureList[4] = new LLUUID("00000000-0000-0000-9999-000000000005");
            for (int i = 0; i < this.textureList.Length; i++)
            {
                this._assetServer.RequestAsset(this.textureList[i], true);
            }
        }

        private void ProcessAssetQueue()
        {
            if (this.AssetRequests.Count != 0)
            {
                int num;
                if (this.AssetRequests.Count < 5)
                {
                    num = this.AssetRequests.Count;
                }
                else
                {
                    num = 5;
                }
                for (int i = 0; i < num; i++)
                {
                    AssetRequest request = this.AssetRequests[i];
                    TransferInfoPacket newPack = new TransferInfoPacket();
                    newPack.TransferInfo.ChannelType = 2;
                    newPack.TransferInfo.Status = 0;
                    newPack.TransferInfo.TargetType = 0;
                    newPack.TransferInfo.Params = request.RequestAssetID.GetBytes();
                    newPack.TransferInfo.Size = request.AssetInf.Data.Length;
                    newPack.TransferInfo.TransferID = request.TransferRequestID;
                    request.RequestUser.OutPacket(newPack);
                    if (request.NumPackets == 1)
                    {
                        TransferPacketPacket packet2 = new TransferPacketPacket();
                        packet2.TransferData.Packet = 0;
                        packet2.TransferData.ChannelType = 2;
                        packet2.TransferData.TransferID = request.TransferRequestID;
                        packet2.TransferData.Data = request.AssetInf.Data;
                        packet2.TransferData.Status = 1;
                        request.RequestUser.OutPacket(packet2);
                    }
                    else
                    {
                        TransferPacketPacket packet3 = new TransferPacketPacket();
                        packet3.TransferData.Packet = 0;
                        packet3.TransferData.ChannelType = 2;
                        packet3.TransferData.TransferID = request.TransferRequestID;
                        byte[] destinationArray = new byte[0x3e8];
                        Array.Copy(request.AssetInf.Data, destinationArray, 0x3e8);
                        packet3.TransferData.Data = destinationArray;
                        packet3.TransferData.Status = 0;
                        request.RequestUser.OutPacket(packet3);
                        packet3 = new TransferPacketPacket();
                        packet3.TransferData.Packet = 1;
                        packet3.TransferData.ChannelType = 2;
                        packet3.TransferData.TransferID = request.TransferRequestID;
                        byte[] buffer2 = new byte[request.AssetInf.Data.Length - 0x3e8];
                        Array.Copy(request.AssetInf.Data, 0x3e8, buffer2, 0, buffer2.Length);
                        packet3.TransferData.Data = buffer2;
                        packet3.TransferData.Status = 1;
                        request.RequestUser.OutPacket(packet3);
                    }
                }
                for (int j = 0; j < num; j++)
                {
                    this.AssetRequests.RemoveAt(0);
                }
            }
        }

        private void ProcessTextureQueue()
        {
            if (this.TextureRequests.Count != 0)
            {
                int num = this.TextureRequests.Count;
                for (int i = 0; i < num; i++)
                {
                    AssetRequest req = this.TextureRequests[i];
                    if (!this.SendingTextures.ContainsKey(req.ImageInfo.FullID))
                    {
                        TextureSender sender = new TextureSender(req);
                        sender.OnComplete += new DownloadComplete(this.TextureSent);
                        lock (this.SendingTextures)
                        {
                            this.SendingTextures.Add(req.ImageInfo.FullID, sender);
                        }
                    }
                }
                this.TextureRequests.Clear();
            }
        }

        public void RunAssetManager()
        {
        Label_0000:
            try
            {
                this.ProcessAssetQueue();
                this.ProcessTextureQueue();
                Thread.Sleep(500);
                goto Label_0000;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
                goto Label_0000;
            }
        }

        public void TextureSent(TextureSender sender)
        {
            if (this.SendingTextures.ContainsKey(sender.request.ImageInfo.FullID))
            {
                lock (this.SendingTextures)
                {
                    this.SendingTextures.Remove(sender.request.ImageInfo.FullID);
                }
            }
        }

        // Nested Types
        public class AssetInfo : AssetBase
        {
            // Methods
            public AssetInfo()
            {
            }

            public AssetInfo(AssetBase aBase)
            {
                base.Data = aBase.Data;
                base.FullID = aBase.FullID;
                base.Type = aBase.Type;
                base.InvType = aBase.InvType;
                base.Name = aBase.Name;
                base.Description = aBase.Description;
            }
        }

        public class AssetRequest
        {
            // Fields
            public AssetCache.AssetInfo AssetInf;
            public long DataPointer;
            public AssetCache.TextureImage ImageInfo;
            public bool IsTextureRequest;
            public int NumPackets;
            public int PacketCounter;
            public LLUUID RequestAssetID;
            public IClientAPI RequestUser;
            public LLUUID TransferRequestID;
        }

        public class TextureImage : AssetBase
        {
            // Methods
            public TextureImage()
            {
            }

            public TextureImage(AssetBase aBase)
            {
                base.Data = aBase.Data;
                base.FullID = aBase.FullID;
                base.Type = aBase.Type;
                base.InvType = aBase.InvType;
                base.Name = aBase.Name;
                base.Description = aBase.Description;
            }
        }

        public class TextureSender
        {
            // Fields
            private Thread m_thread;
            public AssetCache.AssetRequest request;

            // Events
            public event DownloadComplete OnComplete;

            // Methods
            public TextureSender(AssetCache.AssetRequest req)
            {
                this.request = req;
                this.m_thread = new Thread(new ThreadStart(this.SendTexture));
                this.m_thread.IsBackground = true;
                this.m_thread.Start();
            }

            public void SendPacket()
            {
                AssetCache.AssetRequest request = this.request;
                if (request.PacketCounter == 0)
                {
                    if (request.NumPackets == 1)
                    {
                        ImageDataPacket newPack = new ImageDataPacket();
                        newPack.ImageID.Packets = 1;
                        newPack.ImageID.ID = request.ImageInfo.FullID;
                        newPack.ImageID.Size = (uint)request.ImageInfo.Data.Length;
                        newPack.ImageData.Data = request.ImageInfo.Data;
                        newPack.ImageID.Codec = 2;
                        request.RequestUser.OutPacket(newPack);
                        request.PacketCounter++;
                    }
                    else
                    {
                        ImageDataPacket packet2 = new ImageDataPacket();
                        packet2.ImageID.Packets = (ushort)request.NumPackets;
                        packet2.ImageID.ID = request.ImageInfo.FullID;
                        packet2.ImageID.Size = (uint)request.ImageInfo.Data.Length;
                        packet2.ImageData.Data = new byte[600];
                        Array.Copy(request.ImageInfo.Data, 0, packet2.ImageData.Data, 0, 600);
                        packet2.ImageID.Codec = 2;
                        request.RequestUser.OutPacket(packet2);
                        request.PacketCounter++;
                    }
                }
                else
                {
                    ImagePacketPacket packet3 = new ImagePacketPacket();
                    packet3.ImageID.Packet = (ushort)request.PacketCounter;
                    packet3.ImageID.ID = request.ImageInfo.FullID;
                    int length = (request.ImageInfo.Data.Length - 600) - (0x3e8 * (request.PacketCounter - 1));
                    if (length > 0x3e8)
                    {
                        length = 0x3e8;
                    }
                    packet3.ImageData.Data = new byte[length];
                    Array.Copy(request.ImageInfo.Data, 600 + (0x3e8 * (request.PacketCounter - 1)), packet3.ImageData.Data, 0, length);
                    request.RequestUser.OutPacket(packet3);
                    request.PacketCounter++;
                }
            }

            public void SendTexture()
            {
                while (this.request.PacketCounter != this.request.NumPackets)
                {
                    this.SendPacket();
                    Thread.Sleep(500);
                }
                if (this.OnComplete != null)
                {
                    this.OnComplete(this);
                }
            }
        }
    }
}
