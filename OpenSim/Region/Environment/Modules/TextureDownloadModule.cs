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

        private Dictionary<LLUUID, Dictionary<LLUUID, AssetRequest>> ClientRequests =
            new Dictionary<LLUUID, Dictionary<LLUUID, AssetRequest>>();

        private BlockingQueue<TextureSender> QueueSenders = new BlockingQueue<TextureSender>();
        private Dictionary<LLUUID, List<LLUUID>> InProcess = new Dictionary<LLUUID, List<LLUUID>>();
        // private Thread m_thread;

        public TextureDownloadModule()
        {
            //  m_thread = new Thread(new ThreadStart(ProcessTextureSenders));
            //  m_thread.IsBackground = true;
            //  m_thread.Start();
        }

        public void Initialise(Scene scene, IConfigSource config)
        {
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
            get { return true; }
        }

        public void NewClient(IClientAPI client)
        {
            /* lock (ClientRequests)
            {
                if (!ClientRequests.ContainsKey(client.AgentId))
                {
                    ClientRequests.Add(client.AgentId, new Dictionary<LLUUID, AssetRequest>());
                    InProcess.Add(client.AgentId, new List<LLUUID>());
                }
            }
            client.OnRequestTexture += TextureRequest;
            */
        }

        public void TextureCallback(LLUUID textureID, AssetBase asset)
        {
            lock (ClientRequests)
            {
                foreach (Dictionary<LLUUID, AssetRequest> reqList in ClientRequests.Values)
                {
                    if (reqList.ContainsKey(textureID))
                    {
                        //check the texture isn't already in the process of being sent to the client.
                        if (!InProcess[reqList[textureID].RequestUser.AgentId].Contains(textureID))
                        {
                            TextureSender sender = new TextureSender(reqList[textureID], asset);
                            QueueSenders.Enqueue(sender);
                            InProcess[reqList[textureID].RequestUser.AgentId].Add(textureID);
                            reqList.Remove(textureID);
                        }
                    }
                }
            }
        }

        public void TextureRequest(Object sender, TextureRequestArgs e)
        {
            IClientAPI client = (IClientAPI) sender;
            if (!ClientRequests[client.AgentId].ContainsKey(e.RequestedAssetID))
            {
                lock (ClientRequests)
                {
                    AssetRequest request = new AssetRequest(client, e.RequestedAssetID, e.DiscardLevel, e.PacketNumber);
                    ClientRequests[client.AgentId].Add(e.RequestedAssetID, request);
                }
                m_scene.AssetCache.GetAsset(e.RequestedAssetID, TextureCallback);
            }
        }

        public void ProcessTextureSenders()
        {
            while (true)
            {
                TextureSender sender = QueueSenders.Dequeue();
                bool finished = sender.SendTexture();
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

        private void TextureSent(TextureSender sender)
        {
            if (InProcess[sender.request.RequestUser.AgentId].Contains(sender.request.RequestAssetID))
            {
                InProcess[sender.request.RequestUser.AgentId].Remove(sender.request.RequestAssetID);
            }
        }

        public class TextureSender
        {
            public AssetRequest request;
            private int counter = 0;
            private AssetBase m_asset;
            public long DataPointer = 0;
            public int NumPackets = 0;
            public int PacketCounter = 0;

            public TextureSender(AssetRequest req, AssetBase asset)
            {
                request = req;
                m_asset = asset;

                if (asset.Data.LongLength > 600)
                {
                    NumPackets = 2 + (int) (asset.Data.Length - 601)/1000;
                }
                else
                {
                    NumPackets = 1;
                }

                PacketCounter = (int) req.PacketNumber;
            }

            public bool SendTexture()
            {
                SendPacket();
                counter++;
                if ((PacketCounter >= NumPackets) || counter > 100 || (NumPackets == 1) || (request.DiscardLevel == -1))
                {
                    return true;
                }
                return false;
            }

            public void SendPacket()
            {
                AssetRequest req = request;
                if (PacketCounter == 0)
                {
                    if (NumPackets == 1)
                    {
                        ImageDataPacket im = new ImageDataPacket();
                        im.Header.Reliable = false;
                        im.ImageID.Packets = 1;
                        im.ImageID.ID = m_asset.FullID;
                        im.ImageID.Size = (uint) m_asset.Data.Length;
                        im.ImageData.Data = m_asset.Data;
                        im.ImageID.Codec = 2;
                        req.RequestUser.OutPacket(im, ThrottleOutPacketType.Texture);
                        PacketCounter++;
                    }
                    else
                    {
                        ImageDataPacket im = new ImageDataPacket();
                        im.Header.Reliable = false;
                        im.ImageID.Packets = (ushort) (NumPackets);
                        im.ImageID.ID = m_asset.FullID;
                        im.ImageID.Size = (uint) m_asset.Data.Length;
                        im.ImageData.Data = new byte[600];
                        Array.Copy(m_asset.Data, 0, im.ImageData.Data, 0, 600);
                        im.ImageID.Codec = 2;
                        req.RequestUser.OutPacket(im, ThrottleOutPacketType.Texture);
                        PacketCounter++;
                    }
                }
                else
                {
                    ImagePacketPacket im = new ImagePacketPacket();
                    im.Header.Reliable = false;
                    im.ImageID.Packet = (ushort) (PacketCounter);
                    im.ImageID.ID = m_asset.FullID;
                    int size = m_asset.Data.Length - 600 - (1000*(PacketCounter - 1));
                    if (size > 1000) size = 1000;
                    im.ImageData.Data = new byte[size];
                    Array.Copy(m_asset.Data, 600 + (1000*(PacketCounter - 1)), im.ImageData.Data, 0, size);
                    req.RequestUser.OutPacket(im, ThrottleOutPacketType.Texture);
                    PacketCounter++;
                }
            }
        }

        public class AssetRequest
        {
            public IClientAPI RequestUser;
            public LLUUID RequestAssetID;
            public int DiscardLevel = -1;
            public uint PacketNumber = 0;

            public AssetRequest(IClientAPI client, LLUUID textureID, int discardLevel, uint packetNumber)
            {
                RequestUser = client;
                RequestAssetID = textureID;
                DiscardLevel = discardLevel;
                PacketNumber = packetNumber;
            }
        }
    }
}