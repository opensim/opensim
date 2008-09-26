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
using OpenMetaverse;
using OpenMetaverse.Packets;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Agent.AssetDownload
{
    public class AssetDownloadModule : IRegionModule
    {
        /// <summary>
        /// Asset requests with data which are ready to be sent back to requesters.  This includes textures.
        /// </summary>
        private List<AssetRequest> AssetRequests;

        private Scene m_scene;
        private Dictionary<UUID, Scene> RegisteredScenes = new Dictionary<UUID, Scene>();

        ///
        /// Assets requests (for each user) which are waiting for asset server data.  This includes texture requests
        /// </summary>
        private Dictionary<UUID, Dictionary<UUID, AssetRequest>> RequestedAssets;

        public AssetDownloadModule()
        {
            RequestedAssets = new Dictionary<UUID, Dictionary<UUID, AssetRequest>>();
            AssetRequests = new List<AssetRequest>();
        }

        #region IRegionModule Members

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

        #endregion

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
                //Console.WriteLine("asset request " + requestID);
            }

            //not found asset
            // so request from asset server
            Dictionary<UUID, AssetRequest> userRequests = null;
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
                userRequests = new Dictionary<UUID, AssetRequest>();
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

        public void AssetCallback(UUID assetID, AssetBase asset)
        {
            if (asset != null)
            {
                foreach (Dictionary<UUID, AssetRequest> userRequests in RequestedAssets.Values)
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
                int restPackets = (int) ((restData + m_maxPacketSize - 1) / m_maxPacketSize);
                numPackets += restPackets;
            }

            return numPackets;
        }

        #region Nested type: AssetRequest

        public class AssetRequest
        {
            public AssetBase AssetInf;
            public byte AssetRequestSource = 2;
            public long DataPointer = 0;
            public int DiscardLevel = -1;
            public AssetBase ImageInfo;
            public bool IsTextureRequest;
            public int NumPackets = 0;
            public int PacketCounter = 0;
            public byte[] Params = null;
            public UUID RequestAssetID;
            public IClientAPI RequestUser;
            public UUID TransferRequestID;
            //public bool AssetInCache;
            //public int TimeRequested;

            public AssetRequest()
            {
            }
        }

        #endregion
    }
}
