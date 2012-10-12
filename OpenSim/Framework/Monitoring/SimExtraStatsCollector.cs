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
 *     * Neither the name of the OpenSimulator Project nor the
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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework.Monitoring.Interfaces;

namespace OpenSim.Framework.Monitoring
{
    /// <summary>
    /// Collects sim statistics which aren't already being collected for the linden viewer's statistics pane
    /// </summary>
    public class SimExtraStatsCollector : BaseStatsCollector
    {
        private long abnormalClientThreadTerminations;

//        private long assetsInCache;
//        private long texturesInCache;
//        private long assetCacheMemoryUsage;
//        private long textureCacheMemoryUsage;
//        private TimeSpan assetRequestTimeAfterCacheMiss;
//        private long blockedMissingTextureRequests;

//        private long assetServiceRequestFailures;
//        private long inventoryServiceRetrievalFailures;

        private volatile float timeDilation;
        private volatile float simFps;
        private volatile float physicsFps;
        private volatile float agentUpdates;
        private volatile float rootAgents;
        private volatile float childAgents;
        private volatile float totalPrims;
        private volatile float activePrims;
        private volatile float totalFrameTime;
        private volatile float netFrameTime;
        private volatile float physicsFrameTime;
        private volatile float otherFrameTime;
        private volatile float imageFrameTime;
        private volatile float inPacketsPerSecond;
        private volatile float outPacketsPerSecond;
        private volatile float unackedBytes;
        private volatile float agentFrameTime;
        private volatile float pendingDownloads;
        private volatile float pendingUploads;
        private volatile float activeScripts;
        private volatile float scriptLinesPerSecond;

        /// <summary>
        /// Number of times that a client thread terminated because of an exception
        /// </summary>
        public long AbnormalClientThreadTerminations { get { return abnormalClientThreadTerminations; } }

//        /// <summary>
//        /// These statistics are being collected by push rather than pull.  Pull would be simpler, but I had the
//        /// notion of providing some flow statistics (which pull wouldn't give us).  Though admittedly these
//        /// haven't yet been implemented...
//        /// </summary>
//        public long AssetsInCache { get { return assetsInCache; } }
//        
//        /// <value>
//        /// Currently unused
//        /// </value>
//        public long TexturesInCache { get { return texturesInCache; } }
//        
//        /// <value>
//        /// Currently misleading since we can't currently subtract removed asset memory usage without a performance hit
//        /// </value>
//        public long AssetCacheMemoryUsage { get { return assetCacheMemoryUsage; } }
//        
//        /// <value>
//        /// Currently unused
//        /// </value>
//        public long TextureCacheMemoryUsage { get { return textureCacheMemoryUsage; } }

        public float TimeDilation { get { return timeDilation; } }
        public float SimFps { get { return simFps; } }
        public float PhysicsFps { get { return physicsFps; } }
        public float AgentUpdates { get { return agentUpdates; } }
        public float RootAgents { get { return rootAgents; } }
        public float ChildAgents { get { return childAgents; } }
        public float TotalPrims { get { return totalPrims; } }
        public float ActivePrims { get { return activePrims; } }
        public float TotalFrameTime { get { return totalFrameTime; } }
        public float NetFrameTime { get { return netFrameTime; } }
        public float PhysicsFrameTime { get { return physicsFrameTime; } }
        public float OtherFrameTime { get { return otherFrameTime; } }
        public float ImageFrameTime { get { return imageFrameTime; } }
        public float InPacketsPerSecond { get { return inPacketsPerSecond; } }
        public float OutPacketsPerSecond { get { return outPacketsPerSecond; } }
        public float UnackedBytes { get { return unackedBytes; } }
        public float AgentFrameTime { get { return agentFrameTime; } }
        public float PendingDownloads { get { return pendingDownloads; } }
        public float PendingUploads { get { return pendingUploads; } }
        public float ActiveScripts { get { return activeScripts; } }
        public float ScriptLinesPerSecond { get { return scriptLinesPerSecond; } }
        
//        /// <summary>
//        /// This is the time it took for the last asset request made in response to a cache miss.
//        /// </summary>
//        public TimeSpan AssetRequestTimeAfterCacheMiss { get { return assetRequestTimeAfterCacheMiss; } }
//
//        /// <summary>
//        /// Number of persistent requests for missing textures we have started blocking from clients.  To some extent
//        /// this is just a temporary statistic to keep this problem in view - the root cause of this lies either
//        /// in a mishandling of the reply protocol, related to avatar appearance or may even originate in graphics
//        /// driver bugs on clients (though this seems less likely).
//        /// </summary>
//        public long BlockedMissingTextureRequests { get { return blockedMissingTextureRequests; } }
//
//        /// <summary>
//        /// Record the number of times that an asset request has failed.  Failures are effectively exceptions, such as
//        /// request timeouts.  If an asset service replies that a particular asset cannot be found, this is not counted
//        /// as a failure
//        /// </summary>
//        public long AssetServiceRequestFailures { get { return assetServiceRequestFailures; } }

        /// <summary>
        /// Number of known failures to retrieve avatar inventory from the inventory service.  This does not
        /// cover situations where the inventory service accepts the request but never returns any data, since
        /// we do not yet timeout this situation.
        /// </summary>
        /// <remarks>Commented out because we do not cache inventory at this point</remarks>
//        public long InventoryServiceRetrievalFailures { get { return inventoryServiceRetrievalFailures; } }

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
        private IDictionary<UUID, PacketQueueStatsCollector> packetQueueStatsCollectors
            = new Dictionary<UUID, PacketQueueStatsCollector>();

        public void AddAbnormalClientThreadTermination()
        {
            abnormalClientThreadTerminations++;
        }

//        public void AddAsset(AssetBase asset)
//        {
//            assetsInCache++;
//            //assetCacheMemoryUsage += asset.Data.Length;
//        }
//        
//        public void RemoveAsset(UUID uuid)
//        {
//            assetsInCache--;
//        }
//
//        public void AddTexture(AssetBase image)
//        {
//            if (image.Data != null)
//            {
//                texturesInCache++;
//
//                // This could have been a pull stat, though there was originally a nebulous idea to measure flow rates
//                textureCacheMemoryUsage += image.Data.Length;
//            }
//        }
//
//        /// <summary>
//        /// Signal that the asset cache has been cleared.
//        /// </summary>
//        public void ClearAssetCacheStatistics()
//        {
//            assetsInCache = 0;
//            assetCacheMemoryUsage = 0;
//            texturesInCache = 0;
//            textureCacheMemoryUsage = 0;
//        }
//        
//        public void AddAssetRequestTimeAfterCacheMiss(TimeSpan ts)
//        {
//            assetRequestTimeAfterCacheMiss = ts;
//        }
//
//        public void AddBlockedMissingTextureRequest()
//        {
//            blockedMissingTextureRequests++;
//        }
//
//        public void AddAssetServiceRequestFailure()
//        {
//            assetServiceRequestFailures++;
//        }

//        public void AddInventoryServiceRetrievalFailure()
//        {
//            inventoryServiceRetrievalFailures++;
//        }

        /// <summary>
        /// Register as a packet queue stats provider
        /// </summary>
        /// <param name="uuid">An agent UUID</param>
        /// <param name="provider"></param>
        public void RegisterPacketQueueStatsProvider(UUID uuid, IPullStatsProvider provider)
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
        /// <param name="uuid">An agent UUID</param>
        public void DeregisterPacketQueueStatsProvider(UUID uuid)
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
        public void ReceiveClassicSimStatsPacket(SimStats stats)
        {
            // FIXME: SimStats shouldn't allow an arbitrary stat packing order (which is inherited from the original
            // SimStatsPacket that was being used).
            timeDilation            = stats.StatsBlock[0].StatValue;
            simFps                  = stats.StatsBlock[1].StatValue;
            physicsFps              = stats.StatsBlock[2].StatValue;
            agentUpdates            = stats.StatsBlock[3].StatValue;
            rootAgents              = stats.StatsBlock[4].StatValue;
            childAgents             = stats.StatsBlock[5].StatValue;
            totalPrims              = stats.StatsBlock[6].StatValue;
            activePrims             = stats.StatsBlock[7].StatValue;
            totalFrameTime          = stats.StatsBlock[8].StatValue;
            netFrameTime            = stats.StatsBlock[9].StatValue;
            physicsFrameTime        = stats.StatsBlock[10].StatValue;
            otherFrameTime          = stats.StatsBlock[11].StatValue;
            imageFrameTime          = stats.StatsBlock[12].StatValue;
            inPacketsPerSecond      = stats.StatsBlock[13].StatValue;
            outPacketsPerSecond     = stats.StatsBlock[14].StatValue;
            unackedBytes            = stats.StatsBlock[15].StatValue;
            agentFrameTime          = stats.StatsBlock[16].StatValue;
            pendingDownloads        = stats.StatsBlock[17].StatValue;
            pendingUploads          = stats.StatsBlock[18].StatValue;
            activeScripts           = stats.StatsBlock[19].StatValue;
            scriptLinesPerSecond    = stats.StatsBlock[20].StatValue;
        }

        /// <summary>
        /// Report back collected statistical information.
        /// </summary>
        /// <returns></returns>
        public override string Report()
        {
            StringBuilder sb = new StringBuilder(Environment.NewLine);
//            sb.Append("ASSET STATISTICS");
//            sb.Append(Environment.NewLine);
                        
            /*
            sb.Append(
                string.Format(
@"Asset cache contains   {0,6} non-texture assets using {1,10} K
Texture cache contains {2,6} texture     assets using {3,10} K
Latest asset request time after cache miss: {4}s
Blocked client requests for missing textures: {5}
Asset service request failures: {6}"+ Environment.NewLine,
                    AssetsInCache, Math.Round(AssetCacheMemoryUsage / 1024.0),
                    TexturesInCache, Math.Round(TextureCacheMemoryUsage / 1024.0),
                    assetRequestTimeAfterCacheMiss.Milliseconds / 1000.0,
                    BlockedMissingTextureRequests,
                    AssetServiceRequestFailures));
            */

            /*
            sb.Append(
                string.Format(
@"Asset cache contains   {0,6} assets
Latest asset request time after cache miss: {1}s
Blocked client requests for missing textures: {2}
Asset service request failures: {3}" + Environment.NewLine,
                    AssetsInCache,
                    assetRequestTimeAfterCacheMiss.Milliseconds / 1000.0,
                    BlockedMissingTextureRequests,
                    AssetServiceRequestFailures));
                    */

            sb.Append(Environment.NewLine);
            sb.Append("CONNECTION STATISTICS");
            sb.Append(Environment.NewLine);
            sb.Append(
                string.Format(
                    "Abnormal client thread terminations: {0}" + Environment.NewLine,
                    abnormalClientThreadTerminations));

//            sb.Append(Environment.NewLine);
//            sb.Append("INVENTORY STATISTICS");
//            sb.Append(Environment.NewLine);
//            sb.Append(
//                string.Format(
//                    "Initial inventory caching failures: {0}" + Environment.NewLine,
//                    InventoryServiceRetrievalFailures));

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
                    "{0,6:0}  {1,6:0}  {2,6:0}  {3,6:0}  {4,6:0}  {5,6:0.0}  {6,6:0.0}  {7,6:0.0}  {8,6:0.0}  {9,6:0.0}  {10,6:0.0}\n\n",
                    inPacketsPerSecond, outPacketsPerSecond, pendingDownloads, pendingUploads, unackedBytes, totalFrameTime,
                    netFrameTime, physicsFrameTime, otherFrameTime, agentFrameTime, imageFrameTime));

            Dictionary<string, Dictionary<string, Stat>> sceneStats;

            if (StatsManager.TryGetStats("scene", out sceneStats))
            {
                foreach (KeyValuePair<string, Dictionary<string, Stat>> kvp in sceneStats)
                {
                    foreach (Stat stat in kvp.Value.Values)
                    {
                        if (stat.Verbosity == StatVerbosity.Info)
                        {
                            sb.AppendFormat("{0} ({1}): {2}{3}\n", stat.Name, stat.Container, stat.Value, stat.UnitName);
                        }
                    }
                }
            }

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

            foreach (UUID key in packetQueueStatsCollectors.Keys)
            {
                sb.Append(string.Format("{0}: ", key));
                sb.Append(packetQueueStatsCollectors[key].Report());
                sb.Append(Environment.NewLine);
            }
            */

            sb.Append(base.Report());

            return sb.ToString();
        }

        /// <summary>
        /// Report back collected statistical information as json serialization.
        /// </summary>
        /// <returns></returns>
        public override string XReport(string uptime, string version)
        {
            OSDMap args = new OSDMap(30);
//            args["AssetsInCache"] = OSD.FromString (String.Format ("{0:0.##}", AssetsInCache));
//            args["TimeAfterCacheMiss"] = OSD.FromString (String.Format ("{0:0.##}",
//                    assetRequestTimeAfterCacheMiss.Milliseconds / 1000.0));
//            args["BlockedMissingTextureRequests"] = OSD.FromString (String.Format ("{0:0.##}",
//                    BlockedMissingTextureRequests));
//            args["AssetServiceRequestFailures"] = OSD.FromString (String.Format ("{0:0.##}",
//                    AssetServiceRequestFailures));
//            args["abnormalClientThreadTerminations"] = OSD.FromString (String.Format ("{0:0.##}",
//                    abnormalClientThreadTerminations));
//            args["InventoryServiceRetrievalFailures"] = OSD.FromString (String.Format ("{0:0.##}",
//                    InventoryServiceRetrievalFailures));
            args["Dilatn"] = OSD.FromString (String.Format ("{0:0.##}", timeDilation));
            args["SimFPS"] = OSD.FromString (String.Format ("{0:0.##}", simFps));
            args["PhyFPS"] = OSD.FromString (String.Format ("{0:0.##}", physicsFps));
            args["AgntUp"] = OSD.FromString (String.Format ("{0:0.##}", agentUpdates));
            args["RootAg"] = OSD.FromString (String.Format ("{0:0.##}", rootAgents));
            args["ChldAg"] = OSD.FromString (String.Format ("{0:0.##}", childAgents));
            args["Prims"] = OSD.FromString (String.Format ("{0:0.##}", totalPrims));
            args["AtvPrm"] = OSD.FromString (String.Format ("{0:0.##}", activePrims));
            args["AtvScr"] = OSD.FromString (String.Format ("{0:0.##}", activeScripts));
            args["ScrLPS"] = OSD.FromString (String.Format ("{0:0.##}", scriptLinesPerSecond));
            args["PktsIn"] = OSD.FromString (String.Format ("{0:0.##}", inPacketsPerSecond));
            args["PktOut"] = OSD.FromString (String.Format ("{0:0.##}", outPacketsPerSecond));
            args["PendDl"] = OSD.FromString (String.Format ("{0:0.##}", pendingDownloads));
            args["PendUl"] = OSD.FromString (String.Format ("{0:0.##}", pendingUploads));
            args["UnackB"] = OSD.FromString (String.Format ("{0:0.##}", unackedBytes));
            args["TotlFt"] = OSD.FromString (String.Format ("{0:0.##}", totalFrameTime));
            args["NetFt"] = OSD.FromString (String.Format ("{0:0.##}", netFrameTime));
            args["PhysFt"] = OSD.FromString (String.Format ("{0:0.##}", physicsFrameTime));
            args["OthrFt"] = OSD.FromString (String.Format ("{0:0.##}", otherFrameTime));
            args["AgntFt"] = OSD.FromString (String.Format ("{0:0.##}", agentFrameTime));
            args["ImgsFt"] = OSD.FromString (String.Format ("{0:0.##}", imageFrameTime));
            args["Memory"] = OSD.FromString (base.XReport (uptime, version));
            args["Uptime"] = OSD.FromString (uptime);
            args["Version"] = OSD.FromString (version);
            
            string strBuffer = "";
            strBuffer = OSDParser.SerializeJsonString(args);

            return strBuffer;
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
        
        public string XReport(string uptime, string version)
        {
            return "";
        }
    }
}
