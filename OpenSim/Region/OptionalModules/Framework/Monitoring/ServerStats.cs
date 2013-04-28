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

    public string NetworkInterfaceTypes = "Ethernet";

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

    #region ISharedRegionModule
    // IRegionModuleBase.Name
    public string Name { get { return "Server Stats"; } }
    // IRegionModuleBase.ReplaceableInterface
    public Type ReplaceableInterface { get { return null; } }
    // IRegionModuleBase.Initialize
    public void Initialise(IConfigSource source)
    {
        IConfig cfg = source.Configs["Monitoring"];

        if (cfg != null)
            Enabled = cfg.GetBoolean("ServerStatsEnabled", true);

        if (Enabled)
        {
            NetworkInterfaceTypes = cfg.GetString("NetworkInterfaceTypes", "Ethernet");
        }
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

    private void MakeStat(string pName, string pDesc, string pUnit, string pContainer, Action<Stat> act)
    {
        string desc = pDesc;
        if (desc == null)
            desc = pName;
        Stat stat = new Stat(pName, pName, desc, pUnit, CategoryServer, pContainer, StatType.Pull, act, StatVerbosity.Info);
        StatsManager.RegisterStat(stat);
        RegisteredStats.Add(pName, stat);
    }

    public void RegisterServerStats()
    {
        lastperformanceCounterSampleTime = Util.EnvironmentTickCount();
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
                            StatType.Pull, (s) => { GetNextValue(s, processorPercentPerfCounter, Util.IsWindows() ? 1 : -1); },
                            StatVerbosity.Info);
            StatsManager.RegisterStat(tempStat);
            RegisteredStats.Add(tempName, tempStat);

            MakeStat("TotalProcessorTime", null, "sec", ContainerProcessor, 
                                (s) => { s.Value = Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds; });

            MakeStat("UserProcessorTime", null, "sec", ContainerProcessor,
                                (s) => { s.Value = Process.GetCurrentProcess().UserProcessorTime.TotalSeconds; });

            MakeStat("PrivilegedProcessorTime", null, "sec", ContainerProcessor,
                                (s) => { s.Value = Process.GetCurrentProcess().PrivilegedProcessorTime.TotalSeconds; });

            MakeStat("Threads", null, "threads", ContainerProcessor,
                                (s) => { s.Value = Process.GetCurrentProcess().Threads.Count; });
        }
        catch (Exception e)
        {
            m_log.ErrorFormat("{0} Exception creating 'Process': {1}", LogHeader, e);
        }

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
                            (s) => { s.Value = Process.GetCurrentProcess().WorkingSet64 / 1024d / 1024d; });
        MakeStat("ObjectMemory", null, "MB", ContainerMemory,
                            (s) => { s.Value = GC.GetTotalMemory(false) / 1024d / 1024d; });
        MakeStat("LastMemoryChurn", null, "MB/sec", ContainerMemory,
                            (s) => { s.Value = Math.Round(MemoryWatchdog.LastMemoryChurn * 1000d / 1024d / 1024d, 3); });
        MakeStat("AverageMemoryChurn", null, "MB/sec", ContainerMemory,
                            (s) => { s.Value = Math.Round(MemoryWatchdog.AverageMemoryChurn * 1000d / 1024d / 1024d, 3); });
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
