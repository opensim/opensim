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
using Mono.Addins;
using Nini.Config;

using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using OpenMetaverse.StructuredData;

namespace OpenSim.Region.OptionalModules.Framework.Monitoring
{
[Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ServerStatistics")]
public class ServerStats : ISharedRegionModule
{
    private readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    private readonly string LogHeader = "[SERVER STATS]";

    public bool Enabled = false;
    private static Dictionary<string, Stat> RegisteredStats = new Dictionary<string, Stat>();

    public readonly string CategoryServer = "server";

    public readonly string ContainerProcessor = "processor";
    public readonly string ContainerMemory = "memory";
    public readonly string ContainerNetwork = "network";
    public readonly string ContainerProcess = "process";


    readonly int performanceCounterSampleInterval = 500;
    int lastperformanceCounterSampleTime = 0;

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

    PerfCounterControl processThreadCountPerfCounter = null;
    PerfCounterControl processVirtualBytesPerfCounter = null;
    PerfCounterControl processWorkingSetPerfCounter = null;

    PerfCounterControl dotNETCLRMemoryAllocatedBytesPerSecPerfCounter = null;
    PerfCounterControl dotNETCLRMemoryGen0HeapSizePerfCounter = null;
    PerfCounterControl dotNETCLRMemoryGen1HeapSizePerfCounter = null;
    PerfCounterControl dotNETCLRMemoryGen2HeapSizePerfCounter = null;

    PerfCounterControl dotNETCLRLaTTotalContentionsPerfCounter = null;
    PerfCounterControl dotNETCLRLaTContentionsPerSecPerfCounter = null;
    PerfCounterControl dotNETCLRLaTLogicalThreadsPerfCounter = null;
    PerfCounterControl dotNETCLRLaTPhysicalThreadsPerfCounter = null;

    #region ISharedRegionModule
    // IRegionModuleBase.Name
    public string Name { get { return "Server Stats"; } }
    // IRegionModuleBase.ReplaceableInterface
    public Type ReplaceableInterface { get { return null; } }
    // IRegionModuleBase.Initialize
    public void Initialise(IConfigSource source)
    {
        IConfig cnfg = source.Configs["Statistics"];

        if (cnfg != null)
            Enabled = cnfg.GetBoolean("Enabled", true);
    }
    // IRegionModuleBase.Close
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
    // IRegionModuleBase.AddRegion
    public void AddRegion(Scene scene)
    {
    }
    // IRegionModuleBase.RemoveRegion
    public void RemoveRegion(Scene scene)
    {
    }
    // IRegionModuleBase.RegionLoaded
    public void RegionLoaded(Scene scene)
    {
    }
    // ISharedRegionModule.PostInitialize
    public void PostInitialise()
    {
        if (RegisteredStats.Count == 0)
        {
            RegisterServerStats();
        }
    }
    #endregion ISharedRegionModule

    public void RegisterServerStats()
    {
        lastperformanceCounterSampleTime = Util.EnvironmentTickCount();
        PerformanceCounter tempPC;
        Stat tempStat;
        string tempName;

        try
        {
            tempName = "CPU_Percent";
            tempPC = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            processorPercentPerfCounter = new PerfCounterControl(tempPC);
            // A long time bug in mono is that CPU percent is reported as CPU percent idle. Windows reports CPU percent busy.
            tempStat = new Stat(tempName, tempName, "", "percent", CategoryServer, ContainerProcessor,
                            StatType.Pull, (s) => { GetNextValue(s, processorPercentPerfCounter, Util.IsWindows() ? 1 : -1); },
                            StatVerbosity.Info);
            StatsManager.RegisterStat(tempStat);
            RegisteredStats.Add(tempName, tempStat);

            /*  Performance counters are not the way to go. Ick. Find another way.
            tempName = "Thread_Count";
            tempPC = new PerformanceCounter("Process", "Thread Count", AppDomain.CurrentDomain.FriendlyName);
            processThreadCountPerfCounter = new PerfCounterControl(tempPC);
            tempStat = new Stat("Thread_Count", "Thread_Count", "", "threads", CategoryServer, ContainerProcess,
                        StatType.Pull, (s) => { GetNextValue(s, processThreadCountPerfCounter); }, StatVerbosity.Info);
            StatsManager.RegisterStat(tempStat);
            RegisteredStats.Add(tempName, tempStat);

            tempName = "Virtual_Bytes";
            tempPC = new PerformanceCounter("Process", "Virtual Bytes", AppDomain.CurrentDomain.FriendlyName);
            processVirtualBytesPerfCounter = new PerfCounterControl(tempPC);
            tempStat = new Stat("Virtual_Bytes", "Virtual_Bytes", "", "MB", CategoryServer, ContainerProcess,
                        StatType.Pull, (s) => { GetNextValue(s, processVirtualBytesPerfCounter, 1024.0*1024.0); }, StatVerbosity.Info);
            StatsManager.RegisterStat(tempStat);
            RegisteredStats.Add(tempName, tempStat);

            tempName = "Working_Set";
            tempPC = new PerformanceCounter("Process", "Working Set", AppDomain.CurrentDomain.FriendlyName);
            processWorkingSetPerfCounter = new PerfCounterControl(tempPC);
            tempStat = new Stat("Working_Set", "Working_Set", "", "MB", CategoryServer, ContainerProcess,
                        StatType.Pull, (s) => { GetNextValue(s, processWorkingSetPerfCounter, 1024.0*1024.0); }, StatVerbosity.Info);
            StatsManager.RegisterStat(tempStat);
            RegisteredStats.Add(tempName, tempStat);
             */
        }
        catch (Exception e)
        {
            m_log.ErrorFormat("{0} Exception creating 'Process': {1}", LogHeader, e);
        }

        try
        {
            /* The ".NET CLR *" categories aren't working for me.
            tempName = ""Bytes_Allocated_Per_Sec";
            tempPC = new PerformanceCounter(".NET CLR Memory", "Allocated Bytes/sec", AppDomain.CurrentDomain.FriendlyName);
            dotNETCLRMemoryAllocatedBytesPerSecPerfCounter = new PerfCounterControl(tempPC, tempStat);
            tempStat = new Stat(tempName, tempName, "", "bytes/sec", ServerCategory, MemoryContainer,
                StatType.Pull, (s) => { GetNextValue(s, dotNETCLRMemoryAllocatedBytesPerSecPerfCounter); }, StatVerbosity.Info);
            StatsManager.RegisterStat(tempStat);
            RegisteredStats.Add(tempName, tempStat);

            tempName = "Gen_0_Heap_Size";
            tempPC = new PerformanceCounter(".NET CLR Memory", "Gen 0 heap size", AppDomain.CurrentDomain.FriendlyName);
            dotNETCLRMemoryGen0HeapSizePerfCounter = new PerfCounterControl(tempPC, tempStat);
            tempStat = new Stat("Gen_0_Heap_Size", "Gen_0_Heap_Size", "", "bytes", ServerCategory, MemoryContainer,
                StatType.Pull, (s) => { GetNextValue(s, dotNETCLRMemoryGen0HeapSizePerfCounter); }, StatVerbosity.Info);
            StatsManager.RegisterStat(tempStat);
            RegisteredStats.Add(tempName, tempStat);
        
            tempName = "Gen_1_Heap_Size";
            tempPC = new PerformanceCounter(".NET CLR Memory", "Gen 1 heap size", AppDomain.CurrentDomain.FriendlyName);
            dotNETCLRMemoryGen1HeapSizePerfCounter = new PerfCounterControl(tempPC, tempStat);
            tempStat = new Stat("Gen_1_Heap_Size", "Gen_1_Heap_Size", "", "bytes", ServerCategory, MemoryContainer,
                StatType.Pull, (s) => { GetNextValue(s, dotNETCLRMemoryGen1HeapSizePerfCounter); }, StatVerbosity.Info);
            StatsManager.RegisterStat(tempStat);
            RegisteredStats.Add(tempName, tempStat);

            tempName = "Gen_2_Heap_Size";
            tempPC = new PerformanceCounter(".NET CLR Memory", "Gen 2 heap size", AppDomain.CurrentDomain.FriendlyName);
            dotNETCLRMemoryGen2HeapSizePerfCounter = new PerfCounterControl(tempPC, tempStat);
            tempStat = new Stat("Gen_2_Heap_Size", "Gen_2_Heap_Size", "", "bytes", ServerCategory, MemoryContainer,
                StatType.Pull, (s) => { GetNextValue(s, dotNETCLRMemoryGen2HeapSizePerfCounter); }, StatVerbosity.Info);
            StatsManager.RegisterStat(tempStat);
            RegisteredStats.Add(tempName, tempStat);

            tempName = "Total_Lock_Contentions";
            tempPC = new PerformanceCounter(".NET CLR LocksAndThreads", "Total # of Contentions");
            dotNETCLRLaTTotalContentionsPerfCounter = new PerfCounterControl(tempPC, tempStat);
            tempStat = new Stat("Total_Lock_Contentions", "Total_Lock_Contentions", "", "contentions", ServerCategory, ProcessContainer,
                StatType.Pull, (s) => { GetNextValue(s, dotNETCLRLaTTotalContentionsPerfCounter); }, StatVerbosity.Info);
            StatsManager.RegisterStat(tempStat);
            RegisteredStats.Add(tempName, tempStat);

            tempName = "Lock_Contentions";
            tempPC = new PerformanceCounter(".NET CLR LocksAndThreads", "Contention Rate / sec");
            dotNETCLRLaTContentionsPerSecPerfCounter = new PerfCounterControl(tempPC, tempStat);
            tempStat = new Stat("Lock_Contentions", "Lock_Contentions", "", "contentions/sec", ServerCategory, ProcessContainer,
                StatType.Pull, (s) => { GetNextValue(s, dotNETCLRLaTContentionsPerSecPerfCounter); }, StatVerbosity.Info);
            StatsManager.RegisterStat(tempStat);
            RegisteredStats.Add(tempName, tempStat);

            tempName = "Logical_Threads";
            tempPC = new PerformanceCounter(".NET CLR LocksAndThreads", "# of current logical Threads");
            dotNETCLRLaTLogicalThreadsPerfCounter = new PerfCounterControl(tempPC, tempStat);
            tempStat = new Stat("Logicial_Threads", "Logicial_Threads", "", "threads", ServerCategory, ProcessContainer,
                StatType.Pull, (s) => { GetNextValue(s, dotNETCLRLaTLogicalThreadsPerfCounter); }, StatVerbosity.Info);
            StatsManager.RegisterStat(tempStat);
            RegisteredStats.Add(tempName, tempStat);

            tempName = "Physical_Threads";
            tempPC = new PerformanceCounter(".NET CLR LocksAndThreads", "# of current physical Threads");
            dotNETCLRLaTPhysicalThreadsPerfCounter = new PerfCounterControl(tempPC, tempStat);
            tempStat = new Stat("Physical_Threads", "Physical_Threads", "", "threads", ServerCategory, ProcessContainer,
                StatType.Pull, (s) => { GetNextValue(s, dotNETCLRLaTPhysicalThreadsPerfCounter); }, StatVerbosity.Info);
            StatsManager.RegisterStat(tempStat);
            RegisteredStats.Add(tempName, tempStat);
            */
        }
        catch (Exception e)
        {
            m_log.ErrorFormat("{0} Exception creating '.NET CLR Memory': {1}", LogHeader, e);
        }

        try
        {
            IEnumerable<NetworkInterface> nics = NetworkInterface.GetAllNetworkInterfaces();
            // IEnumerable<NetworkInterface> nics = NetworkInterface.GetAllNetworkInterfaces().Where(
            //                      (network) => network.NetworkInterfaceType == NetworkInterfaceType.Ethernet);
            // IEnumerable<NetworkInterface> nics = NetworkInterface.GetAllNetworkInterfaces().Where(
            //                   (network) => network.OperationalStatus == OperationalStatus.Up);

            foreach (NetworkInterface nic in nics)
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                    continue;

                if (nic.Supports(NetworkInterfaceComponent.IPv4))
                {
                    IPv4InterfaceStatistics nicStats = nic.GetIPv4Statistics();
                    if (nicStats != null)
                    {
                        tempName = "Bytes_Rcvd/" + nic.Name;
                        tempStat = new Stat(tempName, tempName, nic.Name, "KB", CategoryServer, ContainerNetwork,
                            StatType.Pull, (s) => { LookupNic(s, (ns) => { return ns.BytesReceived; }, 1024.0); }, StatVerbosity.Info);
                        StatsManager.RegisterStat(tempStat);
                        RegisteredStats.Add(tempName, tempStat);

                        tempName = "Bytes_Sent/" + nic.Name;
                        tempStat = new Stat(tempName, tempName, nic.Name, "KB", CategoryServer, ContainerNetwork,
                            StatType.Pull, (s) => { LookupNic(s, (ns) => { return ns.BytesSent; }, 1024.0); }, StatVerbosity.Info);
                        StatsManager.RegisterStat(tempStat);
                        RegisteredStats.Add(tempName, tempStat);

                        tempName = "Total_Bytes/" + nic.Name;
                        tempStat = new Stat(tempName, tempName, nic.Name, "KB", CategoryServer, ContainerNetwork,
                            StatType.Pull, (s) => { LookupNic(s, (ns) => { return ns.BytesSent + ns.BytesReceived; }, 1024.0); }, StatVerbosity.Info);
                        StatsManager.RegisterStat(tempStat);
                        RegisteredStats.Add(tempName, tempStat);
                    }
                }
            }
        }
        catch (Exception e)
        {
            m_log.ErrorFormat("{0} Exception creating 'Network Interface': {1}", LogHeader, e);
        }

        tempName = "Process_Memory";
        tempStat = new Stat(tempName, tempName, "", "MB", CategoryServer, ContainerMemory,
            StatType.Pull, (s) => { s.Value = Process.GetCurrentProcess().WorkingSet64 / 1024d / 1024d; }, StatVerbosity.Info);
        StatsManager.RegisterStat(tempStat);
        RegisteredStats.Add(tempName, tempStat);

        tempName = "Object_Memory";
        tempStat = new Stat(tempName, tempName, "", "MB", CategoryServer, ContainerMemory,
            StatType.Pull, (s) => { s.Value = GC.GetTotalMemory(false) / 1024d / 1024d; }, StatVerbosity.Info);
        StatsManager.RegisterStat(tempStat);
        RegisteredStats.Add(tempName, tempStat);

        tempName = "Last_Memory_Churn";
        tempStat = new Stat(tempName, tempName, "", "MB/sec", CategoryServer, ContainerMemory,
            StatType.Pull, (s) => { s.Value = Math.Round(MemoryWatchdog.LastMemoryChurn * 1000d / 1024d / 1024d, 3); }, StatVerbosity.Info);
        StatsManager.RegisterStat(tempStat);
        RegisteredStats.Add(tempName, tempStat);

        tempName = "Average_Memory_Churn";
        tempStat = new Stat(tempName, tempName, "", "MB/sec", CategoryServer, ContainerMemory,
            StatType.Pull, (s) => { s.Value = Math.Round(MemoryWatchdog.AverageMemoryChurn * 1000d / 1024d / 1024d, 3); }, StatVerbosity.Info);
        StatsManager.RegisterStat(tempStat);
        RegisteredStats.Add(tempName, tempStat);

    }

    // Notes on performance counters: 
    //  "How To Read Performance Counters": http://blogs.msdn.com/b/bclteam/archive/2006/06/02/618156.aspx
    //  "How to get the CPU Usage in C#": http://stackoverflow.com/questions/278071/how-to-get-the-cpu-usage-in-c
    //  "Mono Performance Counters": http://www.mono-project.com/Mono_Performance_Counters
    private delegate double PerfCounterNextValue();
    private void GetNextValue(Stat stat, PerfCounterControl perfControl)
    {
        GetNextValue(stat, perfControl, 1.0);
    }
    private void GetNextValue(Stat stat, PerfCounterControl perfControl, double factor)
    {
        if (Util.EnvironmentTickCountSubtract(perfControl.lastFetch) > performanceCounterSampleInterval)
        {
            if (perfControl != null && perfControl.perfCounter != null)
            {
                try
                {
                    // Kludge for factor to run double duty. If -1, subtract the value from one
                    if (factor == -1)
                        stat.Value = 1 - perfControl.perfCounter.NextValue();
                    else
                        stat.Value = perfControl.perfCounter.NextValue() / factor;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("{0} Exception on NextValue fetching {1}: {2}", LogHeader, stat.Name, e);
                }
                perfControl.lastFetch = Util.EnvironmentTickCount();
            }
        }
    }

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
                    stat.Value = Math.Round(getter(intrStats) / factor, 3);
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
