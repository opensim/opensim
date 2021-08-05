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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.CoreModules.Framework.Monitoring.Alerts;
using OpenSim.Region.CoreModules.Framework.Monitoring.Monitors;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Mono.Addins;

namespace OpenSim.Region.CoreModules.Framework.Monitoring
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "MonitorModule")]
    public class MonitorModule : INonSharedRegionModule
    {
        /// <summary>
        /// Is this module enabled?
        /// </summary>
        public bool Enabled { get; private set; }

        private Scene m_scene;

        /// <summary>
        /// These are monitors where we know the static details in advance.
        /// </summary>
        /// <remarks>
        /// Dynamic monitors also exist (we don't know any of the details of what stats we get back here)
        /// but these are currently hardcoded.
        /// </remarks>
        private readonly List<IMonitor> m_staticMonitors = new List<IMonitor>();

        private readonly List<IAlert> m_alerts = new List<IAlert>();
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public MonitorModule()
        {
            Enabled = true;
        }

        #region Implementation of INonSharedRegionModule

        public void Initialise(IConfigSource source)
        {
            IConfig cnfg = source.Configs["Monitoring"];

            if (cnfg != null)
                Enabled = cnfg.GetBoolean("Enabled", true);

            if (!Enabled)
                return;

        }

        public void AddRegion(Scene scene)
        {
            if (!Enabled)
                return;

            m_scene = scene;

            m_scene.AddCommand("General", this, "monitor report",
                               "monitor report",
                               "Returns a variety of statistics about the current region and/or simulator",
                               DebugMonitors);

            MainServer.Instance.AddSimpleStreamHandler(new SimpleStreamHandler("/monitorstats/" + m_scene.RegionInfo.RegionID, StatsPage));
            MainServer.Instance.AddSimpleStreamHandler(new SimpleStreamHandler(
                "/monitorstats/" + Uri.EscapeDataString(m_scene.RegionInfo.RegionName), StatsPage));

            AddMonitors();
            RegisterStatsManagerRegionStatistics();
        }

        public void RemoveRegion(Scene scene)
        {
            if (!Enabled)
                return;

            MainServer.Instance.RemoveHTTPHandler("GET", "/monitorstats/" + m_scene.RegionInfo.RegionID);
            MainServer.Instance.RemoveHTTPHandler("GET", "/monitorstats/" + Uri.EscapeDataString(m_scene.RegionInfo.RegionName));

            UnRegisterStatsManagerRegionStatistics();

            m_scene = null;
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "Region Health Monitoring Module"; }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        public void AddMonitors()
        {
            m_staticMonitors.Add(new AgentCountMonitor(m_scene));
            m_staticMonitors.Add(new ChildAgentCountMonitor(m_scene));
            m_staticMonitors.Add(new GCMemoryMonitor());
            m_staticMonitors.Add(new ObjectCountMonitor(m_scene));
            m_staticMonitors.Add(new PhysicsFrameMonitor(m_scene));
            m_staticMonitors.Add(new PhysicsUpdateFrameMonitor(m_scene));
            m_staticMonitors.Add(new PWSMemoryMonitor());
            m_staticMonitors.Add(new ThreadCountMonitor());
            m_staticMonitors.Add(new TotalFrameMonitor(m_scene));
            m_staticMonitors.Add(new EventFrameMonitor(m_scene));
            m_staticMonitors.Add(new LandFrameMonitor(m_scene));
            m_staticMonitors.Add(new LastFrameTimeMonitor(m_scene));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "TimeDilationMonitor",
                    "Time Dilation",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.TimeDilation],
                    m => m.GetValue().ToString("0.#")));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "SimFPSMonitor",
                    "Sim FPS",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.SimFPS],
                    m => m.GetValue().ToString("0.#")));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "PhysicsFPSMonitor",
                    "Physics FPS",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.PhysicsFPS],
                    m => m.GetValue().ToString("0.#")));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "AgentUpdatesPerSecondMonitor",
                    "Agent Updates",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.AgentUpdates],
                    m => string.Format("{0} per second", m.GetValue())));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "ActiveObjectCountMonitor",
                    "Active Objects",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.ActivePrim],
                    m => m.GetValue().ToString("0.")));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "ActiveScriptsMonitor",
                    "Active Scripts",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.ActiveScripts],
                    m => m.GetValue().ToString("0.")));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "ScriptEventsPerSecondMonitor",
                    "Script Events",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.ScriptEps],
                    m => string.Format("{0} per second", m.GetValue())));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "InPacketsPerSecondMonitor",
                    "In Packets",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.InPacketsPerSecond],
                    m => string.Format("{0} per second", m.GetValue())));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "OutPacketsPerSecondMonitor",
                    "Out Packets",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.OutPacketsPerSecond],
                    m => string.Format("{0} per second", m.GetValue())));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "UnackedBytesMonitor",
                    "Unacked Bytes",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.UnAckedBytes],
                    m => string.Format("{0}", m.GetValue())));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "PendingDownloadsMonitor",
                    "Pending Downloads",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.PendingDownloads],
                    m => string.Format("{0}", m.GetValue())));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "PendingUploadsMonitor",
                    "Pending Uploads",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.PendingUploads],
                    m => string.Format("{0}", m.GetValue())));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "TotalFrameTimeMonitor",
                    "Total Frame Time",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.FrameMS],
                    m => string.Format("{0:0.###} ms", m.GetValue())));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "NetFrameTimeMonitor",
                    "Net Frame Time",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.NetMS],
                    m => string.Format("{0:0.###} ms", m.GetValue())));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "PhysicsFrameTimeMonitor",
                    "Physics Frame Time",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.PhysicsMS],
                    m => string.Format("{0:0.###} ms", m.GetValue())));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "SimulationFrameTimeMonitor",
                    "Simulation Frame Time",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.FrameMS],
                    m => string.Format("{0:0.###} ms", m.GetValue())));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "AgentFrameTimeMonitor",
                    "Agent Frame Time",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.AgentMS],
                    m => string.Format("{0:0.###} ms", m.GetValue())));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "ImagesFrameTimeMonitor",
                    "Images Frame Time",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.ImageMS],
                    m => string.Format("{0:0.###} ms", m.GetValue())));

            m_staticMonitors.Add(
                new GenericMonitor(
                    m_scene,
                    "SpareFrameTimeMonitor",
                    "Spare Frame Time",
                    m => m.Scene.StatsReporter.LastReportedSimStats[(int)StatsIndex.SimSpareMs],
                    m => string.Format("{0:0.###} ms", m.GetValue())));

            m_alerts.Add(new DeadlockAlert(m_staticMonitors.Find(x => x is LastFrameTimeMonitor) as LastFrameTimeMonitor));

            foreach (IAlert alert in m_alerts)
            {
                alert.OnTriggerAlert += OnTriggerAlert;
            }
        }

        public void DebugMonitors(string module, string[] args)
        {
            foreach (IMonitor monitor in m_staticMonitors)
            {
                MainConsole.Instance.Output(
                    "[MONITOR MODULE]: {0} reports {1} = {2}",
                    m_scene.RegionInfo.RegionName, monitor.GetFriendlyName(), monitor.GetFriendlyValue());
            }

            foreach (KeyValuePair<string, float> tuple in m_scene.StatsReporter.GetExtraSimStats())
            {
                MainConsole.Instance.Output(
                    "[MONITOR MODULE]: {0} reports {1} = {2}",
                    m_scene.RegionInfo.RegionName, tuple.Key, tuple.Value);
            }
        }

        public void TestAlerts()
        {
            foreach (IAlert alert in m_alerts)
            {
                alert.Test();
            }
        }

        public void StatsPage(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.KeepAlive = false;
            if(request.HttpMethod != "GET")
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
            // If request was for a specific monitor
            // eg url/?monitor=Monitor.Name
            if (request.QueryAsDictionary.TryGetValue("monitor", out string monID))
            {
                foreach (IMonitor monitor in m_staticMonitors)
                {
                    string elemName = monitor.ToString();
                    if (elemName.StartsWith(monitor.GetType().Namespace))
                        elemName = elemName.Substring(monitor.GetType().Namespace.Length + 1);

                    if (elemName == monID || monitor.ToString() == monID)
                    {
                        response.RawBuffer = Util.UTF8.GetBytes(monitor.GetValue().ToString());
                        response.StatusCode = (int)HttpStatusCode.OK;
                        return;
                    }
                }

                // FIXME: Arguably this should also be done with dynamic monitors but I'm not sure what the above code
                // is even doing.  Why are we inspecting the type of the monitor???

                // No monitor with that name
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            string xml = "<data>";
            foreach (IMonitor monitor in m_staticMonitors)
            {
                string elemName = monitor.GetName();
                xml += "<" + elemName + ">" + monitor.GetValue().ToString() + "</" + elemName + ">";
//                m_log.DebugFormat("[MONITOR MODULE]: {0} = {1}", elemName, monitor.GetValue());
            }

            foreach (KeyValuePair<string, float> tuple in m_scene.StatsReporter.GetExtraSimStats())
            {
                xml += "<" + tuple.Key + ">" + tuple.Value + "</" + tuple.Key + ">";
            }

            xml += "</data>";

            response.RawBuffer = Util.UTF8.GetBytes(xml);
            response.ContentType = "text/xml";
            response.StatusCode = (int)HttpStatusCode.OK;
        }

        void OnTriggerAlert(System.Type reporter, string reason, bool fatal)
        {
            m_log.Error("[Monitor] " + reporter.Name + " for " + m_scene.RegionInfo.RegionName + " reports " + reason + " (Fatal: " + fatal + ")");
        }

        private List<Stat> registeredStats = new List<Stat>();
        private void MakeStat(string pName, string pUnitName, Action<Stat> act)
        {
            Stat tempStat = new Stat(pName, pName, pName, pUnitName, "scene", m_scene.RegionInfo.RegionName, StatType.Pull, act, StatVerbosity.Info);
            StatsManager.RegisterStat(tempStat);
            registeredStats.Add(tempStat);
        }
        private void RegisterStatsManagerRegionStatistics()
        {
            MakeStat("RootAgents", "avatars", (s) => { s.Value = m_scene.SceneGraph.GetRootAgentCount(); });
            MakeStat("ChildAgents", "avatars", (s) => { s.Value = m_scene.SceneGraph.GetChildAgentCount(); });
            MakeStat("NPCs", "avatars", (s) => { s.Value = m_scene.SceneGraph.GetRootNPCCount(); });
            MakeStat("TotalPrims", "objects", (s) => { s.Value = m_scene.SceneGraph.GetTotalObjectsCount(); });
            MakeStat("ActivePrims", "objects", (s) => { s.Value = m_scene.SceneGraph.GetActiveObjectsCount(); });
            MakeStat("ActiveScripts", "scripts", (s) => { s.Value = m_scene.SceneGraph.GetActiveScriptsCount(); });

            float[] data =  m_scene.StatsReporter.LastReportedSimStats;
            MakeStat("TimeDilation", "", (s) => { s.Value = data[(int)StatsIndex.TimeDilation]; });
            MakeStat("SimFPS", "fps", (s) => { s.Value = data[(int)StatsIndex.SimFPS]; });
            MakeStat("PhysicsFPS", "fps", (s) => { s.Value = data[(int)StatsIndex.PhysicsFPS]; });
            MakeStat("AgentUpdates", "updates/sec", (s) => { s.Value = data[(int)StatsIndex.AgentUpdates]; });
            MakeStat("FrameTime", "ms", (s) => { s.Value = data[(int)StatsIndex.FrameMS]; });
            MakeStat("NetTime", "ms", (s) => { s.Value = data[(int)StatsIndex.NetMS]; });
            MakeStat("OtherTime", "ms", (s) => { s.Value = data[(int)StatsIndex.OtherMS]; });
            MakeStat("PhysicsTime", "ms", (s) => { s.Value = data[(int)StatsIndex.PhysicsMS]; });
            MakeStat("AgentTime", "ms", (s) => { s.Value = data[(int)StatsIndex.Agents]; });
            MakeStat("ImageTime", "ms", (s) => { s.Value = data[(int)StatsIndex.ImageMS]; });
            MakeStat("ScriptEPS", "Events/sec", (s) => { s.Value = data[(int)StatsIndex.ScriptEps]; });
            MakeStat("SimSpareMS", "ms", (s) => { s.Value = data[(int)StatsIndex.SimSpareMs]; });
        }

        private void UnRegisterStatsManagerRegionStatistics()
        {
            foreach (Stat stat in registeredStats)
            {
                StatsManager.DeregisterStat(stat);
                stat.Dispose();
            }
            registeredStats.Clear();
        }

    }
}