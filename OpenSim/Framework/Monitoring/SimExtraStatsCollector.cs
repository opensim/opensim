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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
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
        /// <summary>
        /// Retain a dictionary of all packet queues stats reporters
        /// </summary>
        private readonly Dictionary<UUID, PacketQueueStatsCollector> packetQueueStatsCollectors
            = new Dictionary<UUID, PacketQueueStatsCollector>();

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

        private UUID firstReceivedRegion;
        private readonly ConcurrentDictionary<UUID, SimStats> ReceivedStats = new ConcurrentDictionary<UUID, SimStats>();
        private readonly ConcurrentDictionary<string, SimStats> ReceivedStatsByName = new ConcurrentDictionary<string, SimStats>();
        /// <summary>
        /// This is the method on which the classic sim stats reporter (which collects stats for
        /// client purposes) sends information to listeners.
        /// </summary>
        /// <param name="pack"></param>
        public void ReceiveClassicSimStatsPacket(SimStats stats)
        {
            UUID id = stats.RegionUUID;
            if (!id.IsZero())
            {
                if(ReceivedStats.Count == 0)
                    firstReceivedRegion = id;
                ReceivedStats[id] = stats;
                ReceivedStatsByName[stats.RegionName.ToLower()] = stats;
            }
        }

        /// <summary>
        /// Report back collected statistical information.
        /// </summary>
        /// <returns></returns>
        public override string Report(IScene scene)
        {
            SimStats sdata = null;
            if (ReceivedStats.Count > 0)
            {
                if (scene == null)
                    ReceivedStats.TryGetValue(firstReceivedRegion, out sdata);
                else
                    ReceivedStats.TryGetValue(scene.RegionInfo.RegionID, out sdata);
            }

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("CONNECTION STATISTICS");
            List<Stat> stats = StatsManager.GetStatsFromEachContainer("clientstack", "ClientLogoutsDueToNoReceives");
            sb.AppendFormat(
                "Client logouts due to no data receive timeout: {0}\n\n",
                stats != null ? stats.Sum(s => s.Value).ToString() : "unknown");

            sb.Append(Environment.NewLine);
            if(sdata != null)
            {
                float[] data = sdata.StatsValues;
                sb.AppendFormat("{0} FRAME STATISTICS", sdata.RegionName);
                sb.Append(Environment.NewLine);
                sb.Append("Dilatn  SimFPS  PhyFPS  AgntUp  RootAg  ChldAg  Prims   AtvPrm  AtvScr  ScrEPS\n");
                sb.AppendFormat(
                        "{0,6:0.00}  {1,6:0}  {2,6:0.0}  {3,6:0.0}  {4,6:0}  {5,6:0}  {6,6:0}  {7,6:0}  {8,6:0}  {9,6:0}\n",
                        data[(int)StatsIndex.TimeDilation], data[(int)StatsIndex.SimFPS],
                        data[(int)StatsIndex.PhysicsFPS],
                        data[(int)StatsIndex.AgentUpdates], data[(int)StatsIndex.Agents],
                        data[(int)StatsIndex.ChildAgents], data[(int)StatsIndex.TotalPrim],
                        data[(int)StatsIndex.ActivePrim], data[(int)StatsIndex.ActiveScripts],
                        data[(int)StatsIndex.ScriptEps]);

                sb.Append(Environment.NewLine);
                // There is no script frame time currently because we don't yet collect it
                sb.Append("PktsIn  PktOut  PendDl  PendUl  UnackB  TotlFt  NetFt   PhysFt  OthrFt  AgntFt  ImgsFt\n");
                sb.AppendFormat(
                        "{0,6:0}  {1,6:0}  {2,6:0}  {3,6:0}  {4,6:0}  {5,6:0.00}  {6,6:0.00}  {7,6:0.00}  {8,6:0.00}  {9,6:0.00}  {10,6:0.00}\n",
                        data[(int)StatsIndex.InPacketsPerSecond], data[(int)StatsIndex.OutPacketsPerSecond],
                        data[(int)StatsIndex.PendingDownloads], data[(int)StatsIndex.PendingUploads],
                        data[(int)StatsIndex.UnAckedBytes], data[(int)StatsIndex.FrameMS],
                        data[(int)StatsIndex.NetMS], data[(int)StatsIndex.PhysicsMS],
                        data[(int)StatsIndex.OtherMS] , data[(int)StatsIndex.AgentMS],
                        data[(int)StatsIndex.ImageMS]);
            }
            sb.Append(base.Report());

            return sb.ToString();
        }

        /// <summary>
        /// Report back collected statistical information as json serialization.
        /// </summary>
        /// <returns></returns>
        public override string XReport(string uptime, string version, string scene)
        {
            return OSDParser.SerializeJsonString(OReport(uptime, version, scene));
        }

        /// <summary>
        /// Report back collected statistical information as an OSDMap
        /// </summary>
        /// <returns></returns>
        public override OSDMap OReport(string uptime, string version, string scene)
        {
            // Get the amount of physical memory, allocated with the instance of this program, in kilobytes;
            // the working set is the set of memory pages currently visible to this program in physical RAM
            // memory and includes both shared (e.g. system libraries) and private data
            int numberThreads = 0;
            int numberThreadsRunning = 0;
            double memUsage = 0;
            using(Process p = Process.GetCurrentProcess())
            {
                memUsage = p.WorkingSet64 / 1024.0;
                numberThreads = p.Threads.Count;

                // Get the number of threads from the system that are currently
                // running
                foreach (ProcessThread currentThread in p.Threads)
                {
                    if (currentThread != null && currentThread.ThreadState == ThreadState.Running)
                        numberThreadsRunning++;
                }
            }

            SimStats sdata = null;
            if (ReceivedStats.Count > 0)
            {
                if (scene == null || string.IsNullOrEmpty(scene))
                    ReceivedStats.TryGetValue(firstReceivedRegion, out sdata);
                else
                {
                    if(UUID.TryParse(scene, out UUID id))
                        ReceivedStats.TryGetValue(id, out sdata);
                    else
                        ReceivedStatsByName.TryGetValue(scene.ToLower(), out sdata);
                }
            }

            OSDMap args = new OSDMap(33);
            if(sdata != null && sdata.StatsValues != null)
            {
                float[] data = sdata.StatsValues;
                args["Dilatn"] = OSD.FromString (String.Format ("{0:0.##}", data[(int)StatsIndex.TimeDilation]));
                args["SimFPS"] = OSD.FromString (String.Format ("{0:0.##}", data[(int)StatsIndex.SimFPS]));
                args["PhyFPS"] = OSD.FromString (String.Format ("{0:0.##}", data[(int)StatsIndex.PhysicsFPS]));
                args["AgntUp"] = OSD.FromString (String.Format ("{0:0.##}", data[(int)StatsIndex.AgentUpdates]));
                args["RootAg"] = OSD.FromString (String.Format ("{0}", (int)data[(int)StatsIndex.Agents]));
                args["ChldAg"] = OSD.FromString(String.Format("{0}", (int)data[(int)StatsIndex.ChildAgents]));
                args["NPCAg"] = OSD.FromString(String.Format("{0}", (int)data[(int)StatsIndex.NPCs]));
                args["Prims"] = OSD.FromString (String.Format ("{0}", (int)data[(int)StatsIndex.TotalPrim]));
                args["AtvPrm"] = OSD.FromString (String.Format ("{0}", (int)data[(int)StatsIndex.ActivePrim]));
                args["AtvScr"] = OSD.FromString (String.Format ("{0}", (int)data[(int)StatsIndex.ActiveScripts]));
                args["ScrLPS"] = OSD.FromString(String.Format("{0:0.##}", data[(int)StatsIndex.LSLScriptLinesPerSecond]));
                args["ScrEPS"] = OSD.FromString(String.Format("{0:0.##}", data[(int)StatsIndex.ScriptEps]));
                args["PktsIn"] = OSD.FromString (String.Format ("{0:0.##}", data[(int)StatsIndex.InPacketsPerSecond]));
                args["PktOut"] = OSD.FromString (String.Format ("{0:0.##}", data[(int)StatsIndex.OutPacketsPerSecond]));
                args["PendDl"] = OSD.FromString (String.Format ("{0:0.##}", data[(int)StatsIndex.PendingDownloads]));
                args["PendUl"] = OSD.FromString (String.Format ("{0:0.##}", data[(int)StatsIndex.PendingUploads]));
                args["UnackB"] = OSD.FromString (String.Format ("{0:0.##}", data[(int)StatsIndex.UnAckedBytes]));
                args["TotlFt"] = OSD.FromString (String.Format ("{0:0.##}", data[(int)StatsIndex.FrameMS]));
                args["NetFt"] = OSD.FromString (String.Format ("{0:0.##}", data[(int)StatsIndex.NetMS]));
                args["PhysFt"] = OSD.FromString (String.Format ("{0:0.##}", data[(int)StatsIndex.PhysicsMS]));
                args["OthrFt"] = OSD.FromString (String.Format ("{0:0.##}", data[(int)StatsIndex.OtherMS]));
                args["AgntFt"] = OSD.FromString (String.Format ("{0:0.##}", data[(int)StatsIndex.AgentMS]));
                args["ImgsFt"] = OSD.FromString (String.Format ("{0:0.##}", data[(int)StatsIndex.ImageMS]));

                args["FrameDilatn"] = OSD.FromString(String.Format("{0:0.#}", data[(int)StatsIndex.FrameDilation2]));
                args["Logging in Users"] = OSD.FromString(String.Format("{0:0.#}", data[(int)StatsIndex.UsersLoggingIn]));
                args["GeoPrims"] = OSD.FromString(String.Format("{0:0.#}", data[(int)StatsIndex.TotalGeoPrim]));
                args["Mesh Objects"] = OSD.FromString(String.Format("{0:0.##}", data[(int)StatsIndex.TotalMesh]));
                args["Script Engine Thread Count"] = OSD.FromString(String.Format("{0:0.#}", data[(int)StatsIndex.ScriptEngineThreadCount]));
                args["RegionName"] = sdata.RegionName;
            }
            else
                args["Error"] = "No Region data";

            args["Util Thread Count"] = OSD.FromString(String.Format("{0:0.##}", Util.GetSmartThreadPoolInfo().InUseThreads));
            args["System Thread Count"] = OSD.FromString(String.Format("{0:0.##}", numberThreads));
            args["System Thread Active"] = OSD.FromString(String.Format("{0:0.##}", numberThreadsRunning));
            args["ProcMem"] = OSD.FromString(String.Format("{0:0.##}", memUsage));

            args["Memory"] = OSD.FromString(base.XReport(uptime, version));
            args["Uptime"] = OSD.FromString(uptime);
            args["Version"] = OSD.FromString(version);

            return args;
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

        public OSDMap OReport(string uptime, string version)
        {
            OSDMap ret = new OSDMap();
            return ret;
        }
    }
}
