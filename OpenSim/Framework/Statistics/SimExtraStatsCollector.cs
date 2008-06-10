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

        private long inventoryServiceRetrievalFailures;
        
        /// <summary>
        /// Number of times that a client thread terminated because of an exception
        /// </summary>
        public long AbnormalClientThreadTerminations { get { return abnormalClientThreadTerminations; } }

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
        /// Number of known failures to retrieve avatar inventory from the inventory service.  This does not
        /// cover situations where the inventory service accepts the request but never returns any data, since
        /// we do not yet timeout this situation.
        /// </summary>
        public long InventoryServiceRetrievalFailures { get { return inventoryServiceRetrievalFailures; } }

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

        public void AddBlockedMissingTextureRequest()
        {
            blockedMissingTextureRequests++;
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
@"Asset   cache contains {0,6} assets   using {1,10:0.000} K" + Environment.NewLine,
                    AssetsInCache, AssetCacheMemoryUsage / 1024.0));

            sb.Append(Environment.NewLine);
            sb.Append("TEXTURE STATISTICS");
            sb.Append(Environment.NewLine);
            sb.Append(
                string.Format(
@"Texture cache contains {0,6} textures using {1,10:0.000} K
Blocked requests for missing textures: {2}" + Environment.NewLine,
                    TexturesInCache, TextureCacheMemoryUsage / 1024.0,
                    BlockedMissingTextureRequests));
            
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
