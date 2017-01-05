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
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using log4net;
using Nini.Config;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;

namespace OpenSim.Framework.Monitoring
{
    public class ServerStatsCollector
    {
        private readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private readonly string LogHeader = "[SERVER STATS]";

        public bool Enabled = false;
        private static Dictionary<string, Stat> RegisteredStats = new Dictionary<string, Stat>();

        public readonly string CategoryServer = "server";

        public readonly string ContainerThreadpool = "threadpool";
        public readonly string ContainerProcessor = "processor";
        public readonly string ContainerMemory = "memory";
        public readonly string ContainerNetwork = "network";
        public readonly string ContainerProcess = "process";

        public string NetworkInterfaceTypes = "Ethernet";

        readonly int performanceCounterSampleInterval = 500;
//        int lastperformanceCounterSampleTime = 0;

        private class PerfCounterControl
        {
            public PerformanceCounter perfCounter;
            public int lastFetch;
            public string name;
            public PerfCounterControl(PerformanceCounter pPc)
                : this(pPc, String.Empty)
            {
            }
            public PerfCounterControl(PerformanceCounter pPc, string pName)
            {
                perfCounter = pPc;
                lastFetch = 0;
                name = pName;
            }
        }

        PerfCounterControl processorPercentPerfCounter = null;

        // IRegionModuleBase.Initialize
        public void Initialise(IConfigSource source)
        {
            if (source == null)
                return;

            IConfig cfg = source.Configs["Monitoring"];

            if (cfg != null)
                Enabled = cfg.GetBoolean("ServerStatsEnabled", true);

            if (Enabled)
            {
                NetworkInterfaceTypes = cfg.GetString("NetworkInterfaceTypes", "Ethernet");
            }
        }

        public void Start()
        {
            if (RegisteredStats.Count == 0)
                RegisterServerStats();
        }

        public void Close()
        {
            if (RegisteredStats.Count > 0)
            {
                foreach (Stat stat in RegisteredStats.Values)
                {
                    StatsManager.DeregisterStat(stat);
                    stat.Dispose();
                }
                RegisteredStats.Clear();
            }
        }

        private void MakeStat(string pName, string pDesc, string pUnit, string pContainer, Action<Stat> act)
        {
            MakeStat(pName, pDesc, pUnit, pContainer, act, MeasuresOfInterest.None);
        }

        private void MakeStat(string pName, string pDesc, string pUnit, string pContainer, Action<Stat> act, MeasuresOfInterest moi)
        {
            string desc = pDesc;
            if (desc == null)
                desc = pName;
            Stat stat = new Stat(pName, pName, desc, pUnit, CategoryServer, pContainer, StatType.Pull, moi, act, StatVerbosity.Debug);
            StatsManager.RegisterStat(stat);
            RegisteredStats.Add(pName, stat);
        }

        public void RegisterServerStats()
        {
//            lastperformanceCounterSampleTime = Util.EnvironmentTickCount();
            PerformanceCounter tempPC;
            Stat tempStat;
            string tempName;

            try
            {
                tempName = "CPUPercent";
                tempPC = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                processorPercentPerfCounter = new PerfCounterControl(tempPC);
                // A long time bug in mono is that CPU percent is reported as CPU percent idle. Windows reports CPU percent busy.
                tempStat = new Stat(tempName, tempName, "", "percent", CategoryServer, ContainerProcessor,
                                StatType.Pull, (s) => { GetNextValue(s, processorPercentPerfCounter); },
                                StatVerbosity.Info);
                StatsManager.RegisterStat(tempStat);
                RegisteredStats.Add(tempName, tempStat);

                MakeStat("TotalProcessorTime", null, "sec", ContainerProcessor,
                                    (s) => { s.Value = Math.Round(Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds, 3); });

                MakeStat("UserProcessorTime", null, "sec", ContainerProcessor,
                                    (s) => { s.Value = Math.Round(Process.GetCurrentProcess().UserProcessorTime.TotalSeconds, 3); });

                MakeStat("PrivilegedProcessorTime", null, "sec", ContainerProcessor,
                                    (s) => { s.Value = Math.Round(Process.GetCurrentProcess().PrivilegedProcessorTime.TotalSeconds, 3); });

                MakeStat("Threads", null, "threads", ContainerProcessor,
                                    (s) => { s.Value = Process.GetCurrentProcess().Threads.Count; });
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} Exception creating 'Process': {1}", LogHeader, e);
            }

            MakeStat("BuiltinThreadpoolWorkerThreadsAvailable", null, "threads", ContainerThreadpool,
                s =>
                {
                    int workerThreads, iocpThreads;
                    ThreadPool.GetAvailableThreads(out workerThreads, out iocpThreads);
                    s.Value = workerThreads;
                });

            MakeStat("BuiltinThreadpoolIOCPThreadsAvailable", null, "threads", ContainerThreadpool,
                s =>
                {
                    int workerThreads, iocpThreads;
                    ThreadPool.GetAvailableThreads(out workerThreads, out iocpThreads);
                    s.Value = iocpThreads;
                });

            if (Util.FireAndForgetMethod == FireAndForgetMethod.SmartThreadPool && Util.GetSmartThreadPoolInfo() != null)
            {
                MakeStat("STPMaxThreads", null, "threads", ContainerThreadpool, s => s.Value = Util.GetSmartThreadPoolInfo().MaxThreads);
                MakeStat("STPMinThreads", null, "threads", ContainerThreadpool, s => s.Value = Util.GetSmartThreadPoolInfo().MinThreads);
                MakeStat("STPConcurrency", null, "threads", ContainerThreadpool, s => s.Value = Util.GetSmartThreadPoolInfo().MaxConcurrentWorkItems);
                MakeStat("STPActiveThreads", null, "threads", ContainerThreadpool, s => s.Value = Util.GetSmartThreadPoolInfo().ActiveThreads);
                MakeStat("STPInUseThreads", null, "threads", ContainerThreadpool, s => s.Value = Util.GetSmartThreadPoolInfo().InUseThreads);
                MakeStat("STPWorkItemsWaiting", null, "threads", ContainerThreadpool, s => s.Value = Util.GetSmartThreadPoolInfo().WaitingCallbacks);
            }

            MakeStat(
                "HTTPRequestsMade",
                "Number of outbound HTTP requests made",
                "requests",
                ContainerNetwork,
                s => s.Value = WebUtil.RequestNumber,
                MeasuresOfInterest.AverageChangeOverTime);

            try
            {
                List<string> okInterfaceTypes = new List<string>(NetworkInterfaceTypes.Split(','));

                IEnumerable<NetworkInterface> nics = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface nic in nics)
                {
                    if (nic.OperationalStatus != OperationalStatus.Up)
                        continue;

                    string nicInterfaceType = nic.NetworkInterfaceType.ToString();
                    if (!okInterfaceTypes.Contains(nicInterfaceType))
                    {
                        m_log.DebugFormat("{0} Not including stats for network interface '{1}' of type '{2}'.",
                                                LogHeader, nic.Name, nicInterfaceType);
                        m_log.DebugFormat("{0}     To include, add to comma separated list in [Monitoring]NetworkInterfaceTypes={1}",
                                                LogHeader, NetworkInterfaceTypes);
                        continue;
                    }

                    if (nic.Supports(NetworkInterfaceComponent.IPv4))
                    {
                        IPv4InterfaceStatistics nicStats = nic.GetIPv4Statistics();
                        if (nicStats != null)
                        {
                            MakeStat("BytesRcvd/" + nic.Name, nic.Name, "KB", ContainerNetwork,
                                            (s) => { LookupNic(s, (ns) => { return ns.BytesReceived; }, 1024.0); });
                            MakeStat("BytesSent/" + nic.Name, nic.Name, "KB", ContainerNetwork,
                                            (s) => { LookupNic(s, (ns) => { return ns.BytesSent; }, 1024.0); });
                            MakeStat("TotalBytes/" + nic.Name, nic.Name, "KB", ContainerNetwork,
                                            (s) => { LookupNic(s, (ns) => { return ns.BytesSent + ns.BytesReceived; }, 1024.0); });
                        }
                    }
                    // TODO: add IPv6 (it may actually happen someday)
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} Exception creating 'Network Interface': {1}", LogHeader, e);
            }

            MakeStat("ProcessMemory", null, "MB", ContainerMemory,
                                (s) => { s.Value = Math.Round(Process.GetCurrentProcess().WorkingSet64 / 1024d / 1024d, 3); });
            MakeStat("HeapMemory", null, "MB", ContainerMemory,
                                (s) => { s.Value = Math.Round(GC.GetTotalMemory(false) / 1024d / 1024d, 3); });
            MakeStat("LastHeapAllocationRate", null, "MB/sec", ContainerMemory,
                                (s) => { s.Value = Math.Round(MemoryWatchdog.LastHeapAllocationRate * 1000d / 1024d / 1024d, 3); });
            MakeStat("AverageHeapAllocationRate", null, "MB/sec", ContainerMemory,
                                (s) => { s.Value = Math.Round(MemoryWatchdog.AverageHeapAllocationRate * 1000d / 1024d / 1024d, 3); });

            MakeStat("ProcessResident", null, "MB", ContainerProcess,
                                (s) =>
                                {
                                    Process myprocess = Process.GetCurrentProcess();
                                    myprocess.Refresh();
                                    s.Value = Math.Round(Process.GetCurrentProcess().WorkingSet64 / 1024.0 / 1024.0);
                                });
            MakeStat("ProcessPaged", null, "MB", ContainerProcess,
                                (s) =>
                                {
                                    Process myprocess = Process.GetCurrentProcess();
                                    myprocess.Refresh();
                                    s.Value = Math.Round(Process.GetCurrentProcess().PagedMemorySize64 / 1024.0 / 1024.0);
                                });
            MakeStat("ProcessVirtual", null, "MB", ContainerProcess,
                                (s) =>
                                {
                                    Process myprocess = Process.GetCurrentProcess();
                                    myprocess.Refresh();
                                    s.Value = Math.Round(Process.GetCurrentProcess().VirtualMemorySize64 / 1024.0 / 1024.0);
                                });
            MakeStat("PeakProcessResident", null, "MB", ContainerProcess,
                                (s) =>
                                {
                                    Process myprocess = Process.GetCurrentProcess();
                                    myprocess.Refresh();
                                    s.Value = Math.Round(Process.GetCurrentProcess().PeakWorkingSet64 / 1024.0 / 1024.0);
                                });
            MakeStat("PeakProcessPaged", null, "MB", ContainerProcess,
                                (s) =>
                                {
                                    Process myprocess = Process.GetCurrentProcess();
                                    myprocess.Refresh();
                                    s.Value = Math.Round(Process.GetCurrentProcess().PeakPagedMemorySize64 / 1024.0 / 1024.0);
                                });
            MakeStat("PeakProcessVirtual", null, "MB", ContainerProcess,
                                (s) =>
                                {
                                    Process myprocess = Process.GetCurrentProcess();
                                    myprocess.Refresh();
                                    s.Value = Math.Round(Process.GetCurrentProcess().PeakVirtualMemorySize64 / 1024.0 / 1024.0);
                                });
        }

        // Notes on performance counters:
        //  "How To Read Performance Counters": http://blogs.msdn.com/b/bclteam/archive/2006/06/02/618156.aspx
        //  "How to get the CPU Usage in C#": http://stackoverflow.com/questions/278071/how-to-get-the-cpu-usage-in-c
        //  "Mono Performance Counters": http://www.mono-project.com/Mono_Performance_Counters
        private delegate double PerfCounterNextValue();

        private void GetNextValue(Stat stat, PerfCounterControl perfControl)
        {
            if (Util.EnvironmentTickCountSubtract(perfControl.lastFetch) > performanceCounterSampleInterval)
            {
                if (perfControl != null && perfControl.perfCounter != null)
                {
                    try
                    {
                        stat.Value = Math.Round(perfControl.perfCounter.NextValue(), 3);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("{0} Exception on NextValue fetching {1}: {2}", LogHeader, stat.Name, e);
                    }

                    perfControl.lastFetch = Util.EnvironmentTickCount();
                }
            }
        }

        // Lookup the nic that goes with this stat and set the value by using a fetch action.
        // Not sure about closure with delegates inside delegates.
        private delegate double GetIPv4StatValue(IPv4InterfaceStatistics interfaceStat);
        private void LookupNic(Stat stat, GetIPv4StatValue getter, double factor)
        {
            // Get the one nic that has the name of this stat
            IEnumerable<NetworkInterface> nics = NetworkInterface.GetAllNetworkInterfaces().Where(
                                  (network) => network.Name == stat.Description);
            try
            {
                foreach (NetworkInterface nic in nics)
                {
                    IPv4InterfaceStatistics intrStats = nic.GetIPv4Statistics();
                    if (intrStats != null)
                    {
                        double newVal = Math.Round(getter(intrStats) / factor, 3);
                        stat.Value = newVal;
                    }
                    break;
                }
            }
            catch
            {
                // There are times interfaces go away so we just won't update the stat for this
                m_log.ErrorFormat("{0} Exception fetching stat on interface '{1}'", LogHeader, stat.Description);
            }
        }
    }

    public class ServerStatsAggregator : Stat
    {
        public ServerStatsAggregator(
            string shortName,
            string name,
            string description,
            string unitName,
            string category,
            string container
            )
            : base(
                shortName,
                name,
                description,
                unitName,
                category,
                container,
                StatType.Push,
                MeasuresOfInterest.None,
                null,
                StatVerbosity.Info)
        {
        }
        public override string ToConsoleString()
        {
            StringBuilder sb = new StringBuilder();

            return sb.ToString();
        }

        public override OSDMap ToOSDMap()
        {
            OSDMap ret = new OSDMap();

            return ret;
        }
    }
}
