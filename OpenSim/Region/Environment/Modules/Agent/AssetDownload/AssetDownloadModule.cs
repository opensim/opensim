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

using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Agent.AssetDownload
{
    public class AssetDownloadModule : IRegionModule
    {
        private Scene m_scene;
        private Dictionary<LLUUID, Scene> RegisteredScenes = new Dictionary<LLUUID, Scene>();
        ///
        /// Assets requests (for each user) which are waiting for asset server data.  This includes texture requests
        /// </summary>
        private Dictionary<LLUUID, Dictionary<LLUUID,AssetRequest>> RequestedAssets;

        /// <summary>
        /// Asset requests with data which are ready to be sent back to requesters.  This includes textures.
        /// </summary>
        private List<AssetRequest> AssetRequests;

        public AssetDownloadModule()
        {
            RequestedAssets = new Dictionary<LLUUID, Dictionary<LLUUID, AssetRequest>>();
            AssetRequests = new List<AssetRequest>();
        }

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (!RegisteredScenes.ContainsKey(scene.RegionInfo.RegionID))
            {
                RegisteredScenes.Add(scene.RegionInfo.RegionID, scene);
                //  scene.EventManager.OnNewClient += NewClient;
            }

            if (m_scene == null)
            {
                m_scene = scene;
                // m_thread = new Thread(new ThreadStart(RunAssetQueue));
                //  m_thread.Name = "AssetDownloadQueueThread";
                // m_thread.IsBackground = true;
                // m_thread.Start();
                // OpenSim.Framework.ThreadTracker.Add(m_thread);
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
            get { return "AssetDownloadModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public void NewClient(IClientAPI client)
        {
            // client.OnRequestAsset += AddAssetRequest;
        }

        /// <summary>
        /// Make an asset request the result of which will be packeted up and sent directly back to the client.
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

            //not found asset
            // so request from asset server
            Dictionary<LLUUID, AssetRequest> userRequests = null;
            if (RequestedAssets.TryGetValue(userInfo.AgentId, out userRequests))
            {
                if (!userRequests.ContainsKey(requestID))
                {
                    AssetRequest request = new AssetRequest();
                    request.RequestUser = userInfo;
                    request.RequestAssetID = requestID;
                    request.TransferRequestID = transferRequest.TransferInfo.TransferID;
                    request.AssetRequestSource = source;
                    request.Params = transferRequest.TransferInfo.Params;
                    userRequests[requestID] = request;
                    m_scene.AssetCache.GetAsset(requestID, AssetCallback, false);
                }
            }
            else
            {
                userRequests = new Dictionary<LLUUID, AssetRequest>();
                AssetRequest request = new AssetRequest();
                request.RequestUser = userInfo;
                request.RequestAssetID = requestID;
                request.TransferRequestID = transferRequest.TransferInfo.TransferID;
                request.AssetRequestSource = source;
                request.Params = transferRequest.TransferInfo.Params;
                userRequests.Add(requestID, request);
                RequestedAssets[userInfo.AgentId] = userRequests;
                m_scene.AssetCache.GetAsset(requestID, AssetCallback, false);
            }
        }

        public void AssetCallback(LLUUID assetID, AssetBase asset)
        {
            if (asset != null)
            {
                foreach (Dictionary<LLUUID, AssetRequest> userRequests in RequestedAssets.Values)
                {
                    if (userRequests.ContainsKey(assetID))
                    {
                        AssetRequest req = userRequests[assetID];
                        if (req != null)
                        {
                            req.AssetInf = asset;
                            req.NumPackets = CalculateNumPackets(asset.Data);

                            userRequests.Remove(assetID);
                            AssetRequests.Add(req);
                        }
                    }
                }
            }
        }

// TODO: unused
//         private void RunAssetQueue()
//         {
//             while (true)
//             {
//                 try
//                 {
//                     ProcessAssetQueue();
//                     Thread.Sleep(500);
//                 }
//                 catch (Exception)
//                 {
//                   //  m_log.Error("[ASSET CACHE]: " + e.ToString());
//                 }
//             }
//         }

// TODO: unused
//         /// <summary>
//         /// Process the asset queue which sends packets directly back to the client.
//         /// </summary>
//         private void ProcessAssetQueue()
//         {
//             //should move the asset downloading to a module, like has been done with texture downloading
//             if (AssetRequests.Count == 0)
//             {
//                 //no requests waiting
//                 return;
//             }
//             // if less than 5, do all of them
//             int num = Math.Min(5, AssetRequests.Count);

//             AssetRequest req;
//             for (int i = 0; i < num; i++)
//             {
//                 req = (AssetRequest)AssetRequests[i];
//                 //Console.WriteLine("sending asset " + req.RequestAssetID);
//                 TransferInfoPacket Transfer = new TransferInfoPacket();
//                 Transfer.TransferInfo.ChannelType = 2;
//                 Transfer.TransferInfo.Status = 0;
//                 Transfer.TransferInfo.TargetType = 0;
//                 if (req.AssetRequestSource == 2)
//                 {
//                     Transfer.TransferInfo.Params = new byte[20];
//                     Array.Copy(req.RequestAssetID.GetBytes(), 0, Transfer.TransferInfo.Params, 0, 16);
//                     int assType = (int)req.AssetInf.Type;
//                     Array.Copy(Helpers.IntToBytes(assType), 0, Transfer.TransferInfo.Params, 16, 4);
//                 }
//                 else if (req.AssetRequestSource == 3)
//                 {
//                     Transfer.TransferInfo.Params = req.Params;
//                     // Transfer.TransferInfo.Params = new byte[100];
//                     //Array.Copy(req.RequestUser.AgentId.GetBytes(), 0, Transfer.TransferInfo.Params, 0, 16);
//                     //Array.Copy(req.RequestUser.SessionId.GetBytes(), 0, Transfer.TransferInfo.Params, 16, 16);
//                 }
//                 Transfer.TransferInfo.Size = (int)req.AssetInf.Data.Length;
//                 Transfer.TransferInfo.TransferID = req.TransferRequestID;
//                 req.RequestUser.OutPacket(Transfer, ThrottleOutPacketType.Asset);

//                 if (req.NumPackets == 1)
//                 {
//                     TransferPacketPacket TransferPacket = new TransferPacketPacket();
//                     TransferPacket.TransferData.Packet = 0;
//                     TransferPacket.TransferData.ChannelType = 2;
//                     TransferPacket.TransferData.TransferID = req.TransferRequestID;
//                     TransferPacket.TransferData.Data = req.AssetInf.Data;
//                     TransferPacket.TransferData.Status = 1;
//                     req.RequestUser.OutPacket(TransferPacket, ThrottleOutPacketType.Asset);
//                 }
//                 else
//                 {
//                     int processedLength = 0;
//                     // libsecondlife hardcodes 1500 as the maximum data chunk size
//                     int maxChunkSize = 1250;
//                     int packetNumber = 0;

//                     while (processedLength < req.AssetInf.Data.Length)
//                     {
//                         TransferPacketPacket TransferPacket = new TransferPacketPacket();
//                         TransferPacket.TransferData.Packet = packetNumber;
//                         TransferPacket.TransferData.ChannelType = 2;
//                         TransferPacket.TransferData.TransferID = req.TransferRequestID;

//                         int chunkSize = Math.Min(req.AssetInf.Data.Length - processedLength, maxChunkSize);
//                         byte[] chunk = new byte[chunkSize];
//                         Array.Copy(req.AssetInf.Data, processedLength, chunk, 0, chunk.Length);

//                         TransferPacket.TransferData.Data = chunk;

//                         // 0 indicates more packets to come, 1 indicates last packet
//                         if (req.AssetInf.Data.Length - processedLength > maxChunkSize)
//                         {
//                             TransferPacket.TransferData.Status = 0;
//                         }
//                         else
//                         {
//                             TransferPacket.TransferData.Status = 1;
//                         }

//                         req.RequestUser.OutPacket(TransferPacket, ThrottleOutPacketType.Asset);

//                         processedLength += chunkSize;
//                         packetNumber++;
//                     }
//                 }
//             }

//             //remove requests that have been completed
//             for (int i = 0; i < num; i++)
//             {
//                 AssetRequests.RemoveAt(0);
//             }
//         }

        /// <summary>
        /// Calculate the number of packets required to send the asset to the client.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private int CalculateNumPackets(byte[] data)
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

        public class AssetRequest
        {
            public IClientAPI RequestUser;
            public LLUUID RequestAssetID;
            public AssetBase AssetInf;
            public AssetBase ImageInfo;
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
    }
}