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
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Statistics.Interfaces;

namespace OpenSim.Framework.Statistics
{
    /// <summary>
    /// Collects sim statistics which aren't already being collected for the linden viewer's statistics pane
    /// </summary>
    public class SimExtraStatsCollector : BaseStatsCollector
    {
        private long abnormalClientThreadTerminations;
        
        private long assetsInCache;
        private long texturesInCache;
        private long assetCacheMemoryUsage;
        private long textureCacheMemoryUsage;
        private long blockedMissingTextureRequests;

        private long assetServiceRequestFailures;
        private long inventoryServiceRetrievalFailures;
        
        private float timeDilation;
        private float simFps;
        private float physicsFps;
        private float agentUpdates;
        private float rootAgents;
        private float childAgents;
        private float totalPrims;
        private float activePrims;
        private float totalFrameTime;
        private float netFrameTime;
        private float physicsFrameTime;
        private float otherFrameTime;
        private float imageFrameTime;
        private float inPacketsPerSecond;
        private float outPacketsPerSecond;
        private float unackedBytes;
        private float agentFrameTime;
        private float pendingDownloads;
        private float pendingUploads;
        private float activeScripts;
        private float scriptLinesPerSecond;                        
        
        /// <summary>
        /// Number of times that a client thread terminated because of an exception
        /// </summary>
        public long AbnormalClientThreadTerminations { get { return abnormalClientThreadTerminations; } }

        /// <summary>
        /// These statistics are being collected by push rather than pull.  Pull would be simpler, but I had the
        /// notion of providing some flow statistics (which pull wouldn't give us).  Though admittedly these 
        /// haven't yet been implemented... :)
        /// </summary>
        public long AssetsInCache { get { return assetsInCache; } }
        public long TexturesInCache { get { return texturesInCache; } }
        public long AssetCacheMemoryUsage { get { return assetCacheMemoryUsage; } }
        public long TextureCacheMemoryUsage { get { return textureCacheMemoryUsage; } }

        /// <summary>
        /// Number of persistent requests for missing textures we have started blocking from clients.  To some extent
        /// this is just a temporary statistic to keep this problem in view - the root cause of this lies either
        /// in a mishandling of the reply protocol, related to avatar appearance or may even originate in graphics
        /// driver bugs on clients (though this seems less likely).
        /// </summary>
        public long BlockedMissingTextureRequests { get { return blockedMissingTextureRequests; } }

        /// <summary>
        /// Record the number of times that an asset request has failed.  Failures are effectively exceptions, such as
        /// request timeouts.  If an asset service replies that a particular asset cannot be found, this is not counted
        /// as a failure
        /// </summary>
        public long AssetServiceRequestFailures { get { return assetServiceRequestFailures; } }
        
        /// <summary>
        /// Number of known failures to retrieve avatar inventory from the inventory service.  This does not
        /// cover situations where the inventory service accepts the request but never returns any data, since
        /// we do not yet timeout this situation.
        /// </summary>
        public long InventoryServiceRetrievalFailures { get { return inventoryServiceRetrievalFailures; } }
        
        /// <summary>
        /// Retrieve the total frame time (in ms) of the last frame
        /// </summary>
        //public float TotalFrameTime { get { return totalFrameTime; } }
        
        /// <summary>
        /// Retrieve the physics update component (in ms) of the last frame
        /// </summary>
        //public float PhysicsFrameTime { get { return physicsFrameTime; } }

        /// <summary>
        /// Retain a dictionary of all packet queues stats reporters
        /// </summary>
        private IDictionary<LLUUID, PacketQueueStatsCollector> packetQueueStatsCollectors
            = new Dictionary<LLUUID, PacketQueueStatsCollector>();
        
        public void AddAbnormalClientThreadTermination()
        {
            abnormalClientThreadTerminations++;
        }

        public void AddAsset(AssetBase asset)
        {
            assetsInCache++;
            assetCacheMemoryUsage += asset.Data.Length;
        }

        public void AddTexture(AssetBase image)
        {
            if (image.Data != null)
            {
                texturesInCache++;

                // This could have been a pull stat, though there was originally a nebulous idea to measure flow rates
                textureCacheMemoryUsage += image.Data.Length;
            }
        }
        
        /// <summary>
        /// Signal that the asset cache can be cleared.
        /// </summary>
        public void ClearAssetCacheStatistics()
        {
            assetsInCache = 0;
            assetCacheMemoryUsage = 0;
            texturesInCache = 0;
            textureCacheMemoryUsage = 0;
        }

        public void AddBlockedMissingTextureRequest()
        {
            blockedMissingTextureRequests++;
        }

        public void AddAssetServiceRequestFailure()
        {
            assetServiceRequestFailures++;
        }
        
        public void AddInventoryServiceRetrievalFailure()
        {
            inventoryServiceRetrievalFailures++;
        }

        /// <summary>
        /// Register as a packet queue stats provider
        /// </summary>
        /// <param name="uuid">An agent LLUUID</param>
        /// <param name="provider"></param>
        public void RegisterPacketQueueStatsProvider(LLUUID uuid, IPullStatsProvider provider)
        {
            lock (packetQueueStatsCollectors)
            {
                // FIXME: If the region service is providing more than one region, then the child and root agent
                // queues are wrongly replacing each other here.
                packetQueueStatsCollectors[uuid] = new PacketQueueStatsCollector(provider);
            }
        }

        /// <summary>
        /// Deregister a packet queue stats provider
        /// </summary>
        /// <param name="uuid">An agent LLUUID</param>
        public void DeregisterPacketQueueStatsProvider(LLUUID uuid)
        {
            lock (packetQueueStatsCollectors)
            {
                packetQueueStatsCollectors.Remove(uuid);
            }
        }
        
        /// <summary>
        /// This is the method on which the classic sim stats reporter (which collects stats for 
        /// client purposes) sends information to listeners.
        /// </summary>
        /// <param name="pack"></param>
        public void ReceiveClassicSimStatsPacket(SimStatsPacket statsPacket)        
        {
            // FIXME: Really shouldn't rely on the probably arbitrary order in which
            // stats are packed into the packet
            timeDilation            = statsPacket.Stat[0].StatValue;
            simFps                  = statsPacket.Stat[1].StatValue;            
            physicsFps              = statsPacket.Stat[2].StatValue;
            agentUpdates            = statsPacket.Stat[3].StatValue;
            rootAgents              = statsPacket.Stat[4].StatValue;
            childAgents             = statsPacket.Stat[5].StatValue;
            totalPrims              = statsPacket.Stat[6].StatValue;
            activePrims             = statsPacket.Stat[7].StatValue;
            totalFrameTime          = statsPacket.Stat[8].StatValue;
            netFrameTime            = statsPacket.Stat[9].StatValue;
            physicsFrameTime        = statsPacket.Stat[10].StatValue;
            otherFrameTime          = statsPacket.Stat[11].StatValue;
            imageFrameTime          = statsPacket.Stat[12].StatValue;
            inPacketsPerSecond      = statsPacket.Stat[13].StatValue;
            outPacketsPerSecond     = statsPacket.Stat[14].StatValue;
            unackedBytes            = statsPacket.Stat[15].StatValue;
            agentFrameTime          = statsPacket.Stat[16].StatValue;
            pendingDownloads        = statsPacket.Stat[17].StatValue;
            pendingUploads          = statsPacket.Stat[18].StatValue;
            activeScripts           = statsPacket.Stat[19].StatValue;
            scriptLinesPerSecond    = statsPacket.Stat[20].StatValue;
        }
        
        /// <summary>
        /// Report back collected statistical information.
        /// </summary>
        /// <returns></returns>
        public override string Report()
        {
            StringBuilder sb = new StringBuilder(Environment.NewLine);
            sb.Append("ASSET STATISTICS");
            sb.Append(Environment.NewLine);
            sb.Append(
                string.Format(
@"Asset cache contains   {0,6} non-texture assets using {1,10} K
Texture cache contains {2,6} texture     assets using {3,10} K
Blocked client requests for missing textures: {4}
Asset service request failures: {5}"+ Environment.NewLine,
                    AssetsInCache, Math.Round(AssetCacheMemoryUsage / 1024.0),
                    TexturesInCache, Math.Round(TextureCacheMemoryUsage / 1024.0), 
                    BlockedMissingTextureRequests,
                    AssetServiceRequestFailures));
            
            sb.Append(Environment.NewLine);
            sb.Append("CONNECTION STATISTICS");
            sb.Append(Environment.NewLine);
            sb.Append(
                string.Format(
                    "Abnormal client thread terminations: {0}" + Environment.NewLine,
                    abnormalClientThreadTerminations));

            sb.Append(Environment.NewLine);
            sb.Append("INVENTORY STATISTICS");
            sb.Append(Environment.NewLine);
            sb.Append(
                string.Format(
                    "Initial inventory caching failures: {0}" + Environment.NewLine,
                    InventoryServiceRetrievalFailures));
            
            sb.Append(Environment.NewLine);
            sb.Append("FRAME STATISTICS");
            sb.Append(Environment.NewLine);
            sb.Append("Dilatn  SimFPS  PhyFPS  AgntUp  RootAg  ChldAg  Prims   AtvPrm  AtvScr  ScrLPS");
            sb.Append(Environment.NewLine);
            sb.Append(
                string.Format(
                    "{0,6:0.00}  {1,6:0}  {2,6:0.0}  {3,6:0.0}  {4,6:0}  {5,6:0}  {6,6:0}  {7,6:0}  {8,6:0}  {9,6:0}",
                    timeDilation, simFps, physicsFps, agentUpdates, rootAgents,
                    childAgents, totalPrims, activePrims, activeScripts, scriptLinesPerSecond));
                    
            sb.Append(Environment.NewLine);
            sb.Append(Environment.NewLine);
            // There is no script frame time currently because we don't yet collect it
            sb.Append("PktsIn  PktOut  PendDl  PendUl  UnackB  TotlFt  NetFt   PhysFt  OthrFt  AgntFt  ImgsFt");
            sb.Append(Environment.NewLine);                    
            sb.Append(
                string.Format(
                    "{0,6:0}  {1,6:0}  {2,6:0}  {3,6:0}  {4,6:0}  {5,6:0.0}  {6,6:0.0}  {7,6:0.0}  {8,6:0.0}  {9,6:0.0}  {10,6:0.0}",
                    inPacketsPerSecond, outPacketsPerSecond, pendingDownloads, pendingUploads, unackedBytes, totalFrameTime,
                    netFrameTime, physicsFrameTime, otherFrameTime, agentFrameTime, imageFrameTime));
            sb.Append(Environment.NewLine);                  

            /*
            sb.Append(Environment.NewLine);
            sb.Append("PACKET QUEUE STATISTICS");
            sb.Append(Environment.NewLine);
            sb.Append("Agent UUID                          ");
            sb.Append(
                string.Format(
                    "  {0,7}  {1,7}  {2,7}  {3,7}  {4,7}  {5,7}  {6,7}  {7,7}  {8,7}  {9,7}",
                    "Send", "In", "Out", "Resend", "Land", "Wind", "Cloud", "Task", "Texture", "Asset"));
            sb.Append(Environment.NewLine);

            foreach (LLUUID key in packetQueueStatsCollectors.Keys)
            {
                sb.Append(string.Format("{0}: ", key));
                sb.Append(packetQueueStatsCollectors[key].Report());
                sb.Append(Environment.NewLine);
            }
            */

            sb.Append(base.Report());
            
            return sb.ToString();
        }
    }

    /// <summary>
    /// Pull packet queue stats from packet queues and report
    /// </summary>
    public class PacketQueueStatsCollector : IStatsCollector
    {
        private IPullStatsProvider m_statsProvider;

        public PacketQueueStatsCollector(IPullStatsProvider provider)
        {
            m_statsProvider = provider;
        }

        /// <summary>
        /// Report back collected statistical information.
        /// </summary>
        /// <returns></returns>
        public string Report()
        {
            return m_statsProvider.GetStats();
        }
    }
}
