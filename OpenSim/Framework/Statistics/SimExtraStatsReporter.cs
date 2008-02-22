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
* 
*/

using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework;
using OpenSim.Framework.Statistics.Interfaces;

using libsecondlife;

namespace OpenSim.Framework.Statistics
{  
    public class SimExtraStatsReporter
    {        
        private long assetsInCache;
        private long texturesInCache;        
        private long assetCacheMemoryUsage;
        private long textureCacheMemoryUsage;
        
        public long AssetsInCache { get { return assetsInCache; } }
        public long TexturesInCache { get { return texturesInCache; } }
        public long AssetCacheMemoryUsage { get { return assetCacheMemoryUsage; } }
        public long TextureCacheMemoryUsage { get { return textureCacheMemoryUsage; } }
        
        /// <summary>
        /// Retain a dictionary of all packet queues stats reporters
        /// </summary>
        private IDictionary<LLUUID, PacketQueueStatsReporter> packetQueueStatsReporters
            = new Dictionary<LLUUID, PacketQueueStatsReporter>();
        
        public void AddAsset(AssetBase asset)
        {
            assetsInCache++;
            assetCacheMemoryUsage += asset.Data.Length;
        }
        
        public void AddTexture(AssetBase image)
        {
            // Tedd: I added null check to avoid exception. Don't know if texturesInCache should ++ anyway?
            if (image.Data != null)
            {
                texturesInCache++;
                textureCacheMemoryUsage += image.Data.Length;
            }
        }  
        
        /// <summary>
        /// Register as a packet queue stats provider
        /// </summary>
        /// <param name="uuid">An agent LLUUID</param>
        /// <param name="provider"></param>
        public void RegisterPacketQueueStatsProvider(LLUUID uuid, IPullStatsProvider provider)
        {
            lock (packetQueueStatsReporters)
            {
                packetQueueStatsReporters[uuid] = new PacketQueueStatsReporter(provider);
            }
        }
        
        /// <summary>
        /// Deregister a packet queue stats provider
        /// </summary>
        /// <param name="uuid">An agent LLUUID</param>
        public void DeregisterPacketQueueStatsProvider(LLUUID uuid)
        {
            lock (packetQueueStatsReporters)
            {
                packetQueueStatsReporters.Remove(uuid);
            }
        }

        /// <summary>
        /// Report back collected statistical information.
        /// </summary>
        /// <returns></returns>
        public string Report()
        {    
            StringBuilder sb = new StringBuilder(Environment.NewLine);
            sb.Append("ASSET CACHE STATISTICS");
            sb.Append(Environment.NewLine);            
            sb.Append(
                string.Format(
@"Asset   cache contains {0,6} assets   using {1,10:0.000}K
Texture cache contains {2,6} textures using {3,10:0.000}K" + Environment.NewLine,
                    AssetsInCache, AssetCacheMemoryUsage / 1024.0, 
                    TexturesInCache, TextureCacheMemoryUsage / 1024.0));

            sb.Append(Environment.NewLine);
            sb.Append("PACKET QUEUE STATISTICS");
            sb.Append(Environment.NewLine);
            sb.Append("Agent UUID                          ");
            sb.Append(
                string.Format(
                    "  {0,7}  {1,7}  {2,7}  {3,7}  {4,7}  {5,7}  {6,7}  {7,7}  {8,7}  {9,7}", 
                    "Send", "In", "Out", "Resend", "Land", "Wind", "Cloud", "Task", "Texture", "Asset"));
            sb.Append(Environment.NewLine);            
                
            foreach (LLUUID key in packetQueueStatsReporters.Keys)
            {
                sb.Append(string.Format("{0}: ", key));
                sb.Append(packetQueueStatsReporters[key].Report());
                sb.Append(Environment.NewLine);
            }
            
            return sb.ToString();
        }        
    }

    /// <summary>
    /// Pull packet queue stats from packet queues and report
    /// </summary>
    public class PacketQueueStatsReporter
    {
        private IPullStatsProvider m_statsProvider;
        
        public PacketQueueStatsReporter(IPullStatsProvider provider)
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
