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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    //this is a first attempt, to start breaking the mess thats called the assetcache up.
    // basically this should be the texture sending (to clients) code moved out of assetcache 
    //and some small clean up
    // but on first tests it didn't seem to work very well so is currently not in use.
    public class TextureDownloadModule : IRegionModule
    {
        private Scene m_scene;
        private List<Scene> m_scenes = new List<Scene>();

        private BlockingQueue<TextureSender> QueueSenders = new BlockingQueue<TextureSender>();

        private Dictionary<LLUUID, UserTextureDownloadService> m_userTextureServices = new Dictionary<LLUUID, UserTextureDownloadService>();

        private Thread m_thread;

        public TextureDownloadModule()
        {
        }

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (m_scene == null)
            {
                //Console.WriteLine("Creating Texture download module");
                m_thread = new Thread(new ThreadStart(ProcessTextureSenders));
                m_thread.IsBackground = true;
                m_thread.Start();
            }

            if (!m_scenes.Contains(scene))
            {
                m_scenes.Add(scene);
                m_scene = scene;
                m_scene.EventManager.OnNewClient += NewClient;
            }

        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "TextureDownloadModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        public void NewClient(IClientAPI client)
        {
            client.OnRequestTexture += TextureRequest;
        }

        private bool TryGetUserTextureService(LLUUID userID, out UserTextureDownloadService textureService)
        {
            lock (m_userTextureServices)
            {
                if (m_userTextureServices.TryGetValue(userID, out textureService))
                {
                    return true;
                }

                textureService = new UserTextureDownloadService(m_scene, QueueSenders);
                m_userTextureServices.Add(userID, textureService);
                return true;
            }
        }

        public void TextureRequest(Object sender, TextureRequestArgs e)
        {
            IClientAPI client = (IClientAPI)sender;
            UserTextureDownloadService textureService;
            if (TryGetUserTextureService(client.AgentId, out textureService))
            {
                textureService.HandleTextureRequest(client, e);
            }
        }

        public void ProcessTextureSenders()
        {
            while (true)
            {
                TextureSender sender = QueueSenders.Dequeue();
                if (sender.Cancel)
                {
                    TextureSent(sender);
                }
                else
                {
                    bool finished = sender.SendTexturePacket();
                    if (finished)
                    {
                        TextureSent(sender);
                    }
                    else
                    {
                        QueueSenders.Enqueue(sender);
                    }
                }
            }
        }

        private void TextureSent(TextureSender sender)
        {
            sender.Sending = false;
        }

        public class UserTextureDownloadService
        {
            private Dictionary<LLUUID, TextureSender> m_textureSenders = new Dictionary<LLUUID, TextureSender>();

            private BlockingQueue<TextureSender> m_sharedSendersQueue;

            private Scene m_scene;

            public UserTextureDownloadService(Scene scene, BlockingQueue<TextureSender> sharedQueue)
            {
                m_scene = scene;
                m_sharedSendersQueue = sharedQueue;
            }

            public void HandleTextureRequest(IClientAPI client, TextureRequestArgs e)
            {
                //TODO: should be working out the data size/ number of packets to be sent for each discard level
                if ((e.DiscardLevel >= 0) || (e.Priority != 0))
                {
                    lock (m_textureSenders)
                    {
                        if (!m_textureSenders.ContainsKey(e.RequestedAssetID))
                        {
                            TextureSender requestHandler = new TextureSender(client, e.RequestedAssetID, e.DiscardLevel, e.PacketNumber);
                            m_textureSenders.Add(e.RequestedAssetID, requestHandler);
                            m_scene.AssetCache.GetAsset(e.RequestedAssetID, TextureCallback);
                        }
                        else
                        {
                            m_textureSenders[e.RequestedAssetID].UpdateRequest(e.DiscardLevel, e.PacketNumber);
                            m_textureSenders[e.RequestedAssetID].counter = 0;
                            if ((m_textureSenders[e.RequestedAssetID].ImageLoaded) && (m_textureSenders[e.RequestedAssetID].Sending ==false))
                            {
                                m_textureSenders[e.RequestedAssetID].Sending = true;
                                m_sharedSendersQueue.Enqueue(m_textureSenders[e.RequestedAssetID]);
                            }
                        }
                    }
                }
                else
                {
                    lock (m_textureSenders)
                    {
                        if (m_textureSenders.ContainsKey(e.RequestedAssetID))
                        {
                            m_textureSenders[e.RequestedAssetID].Cancel = true;
                        }
                    }
                }
            }

            public void TextureCallback(LLUUID textureID, AssetBase asset)
            {
                lock (m_textureSenders)
                {
                    if (m_textureSenders.ContainsKey(textureID))
                    {
                        if (!m_textureSenders[textureID].ImageLoaded)
                        {
                            m_textureSenders[textureID].TextureReceived(asset);
                            m_textureSenders[textureID].Sending = true;
                            m_textureSenders[textureID].counter = 0;
                            m_sharedSendersQueue.Enqueue(m_textureSenders[textureID]);
                        }
                    }
                    else
                    {
                        // Got a texture with no sender object to handle it, this shouldn't happen
                    }
                }
            }
        }

        public class TextureSender
        {
            public int counter = 0;
            private AssetBase m_asset;
            public long DataPointer = 0;
            public int NumPackets = 0;
            public int PacketCounter = 0;
            public bool Cancel = false;
            public bool ImageLoaded = false;

            public bool Sending = false;

            public IClientAPI RequestUser;
            public LLUUID RequestedAssetID;
            public int RequestedDiscardLevel = -1;
            public uint StartPacketNumber = 0;

            // private int m_sentDiscardLevel = -1;

            public TextureSender(IClientAPI client, LLUUID textureID, int discardLevel, uint packetNumber)
            {
                RequestUser = client;
                RequestedAssetID = textureID;
                RequestedDiscardLevel = discardLevel;
                StartPacketNumber = packetNumber;
            }

            public void TextureReceived(AssetBase asset)
            {
                m_asset = asset;
                NumPackets = CalculateNumPackets(asset.Data.Length);
                PacketCounter = (int)StartPacketNumber;
                ImageLoaded = true;
            }

            public void UpdateRequest(int discardLevel, uint packetNumber)
            {
                RequestedDiscardLevel = discardLevel;
                StartPacketNumber = packetNumber;
                PacketCounter = (int)StartPacketNumber;
            }

            public bool SendTexturePacket()
            {
                SendPacket();
                counter++;
                if ((NumPackets == 0) || (RequestedDiscardLevel == -1) || (PacketCounter > NumPackets) || ((RequestedDiscardLevel > 0) && (counter > 50 + (NumPackets / (RequestedDiscardLevel + 1)))) )
                {
                    return true;
                }
                return false;
            }

            private void SendPacket()
            {
                if (PacketCounter <= NumPackets)
                {
                    if (PacketCounter == 0)
                    {
                        if (NumPackets == 0)
                        {
                            ImageDataPacket im = new ImageDataPacket();
                            im.Header.Reliable = false;
                            im.ImageID.Packets = 1;
                            im.ImageID.ID = m_asset.FullID;
                            im.ImageID.Size = (uint)m_asset.Data.Length;
                            im.ImageData.Data = m_asset.Data;
                            im.ImageID.Codec = 2;
                            RequestUser.OutPacket(im, ThrottleOutPacketType.Texture);
                            PacketCounter++;
                        }
                        else
                        {
                            ImageDataPacket im = new ImageDataPacket();
                            im.Header.Reliable = false;
                            im.ImageID.Packets = (ushort)(NumPackets);
                            im.ImageID.ID = m_asset.FullID;
                            im.ImageID.Size = (uint)m_asset.Data.Length;
                            im.ImageData.Data = new byte[600];
                            Array.Copy(m_asset.Data, 0, im.ImageData.Data, 0, 600);
                            im.ImageID.Codec = 2;
                            RequestUser.OutPacket(im, ThrottleOutPacketType.Texture);
                            PacketCounter++;
                        }
                    }
                    else
                    {
                        ImagePacketPacket im = new ImagePacketPacket();
                        im.Header.Reliable = false;
                        im.ImageID.Packet = (ushort)(PacketCounter);
                        im.ImageID.ID = m_asset.FullID;
                        int size = m_asset.Data.Length - 600 - (1000 * (PacketCounter - 1));
                        if (size > 1000) size = 1000;
                        im.ImageData.Data = new byte[size];
                        Array.Copy(m_asset.Data, 600 + (1000 * (PacketCounter - 1)), im.ImageData.Data, 0, size);
                        RequestUser.OutPacket(im, ThrottleOutPacketType.Texture);
                        PacketCounter++;
                    }
                }
            }

            private int CalculateNumPackets(int length)
            {
                int numPackets = 0;

                if (length > 600)
                {
                    //over 600 bytes so split up file
                    int restData = (length - 600);
                    int restPackets = ((restData + 999) / 1000);
                    numPackets = restPackets;
                }

                return numPackets;
            }
        }


    }
}