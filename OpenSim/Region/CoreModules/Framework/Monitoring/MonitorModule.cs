using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Framework.Monitoring.Alerts;
using OpenSim.Region.CoreModules.Framework.Monitoring.Monitors;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Framework.Monitoring
{
    public class MonitorModule : IRegionModule 
    {
        private Scene m_scene;
        private readonly List<IMonitor> m_monitors = new List<IMonitor>();
        private readonly List<IAlert> m_alerts = new List<IAlert>();
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public void DebugMonitors(string module, string[] args)
        {
            foreach (IMonitor monitor in m_monitors)
            {
                m_log.Info("[MonitorModule] " + m_scene.RegionInfo.RegionName + " reports " + monitor.GetName() + " = " + monitor.GetFriendlyValue());
            }
        }

        public void TestAlerts()
        {
            foreach (IAlert alert in m_alerts)
            {
                alert.Test();
            }
        }

        #region Implementation of IRegionModule

        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scene = scene;


            m_scene.AddCommand(this, "monitor report",
                               "monitor report",
                               "Returns a variety of statistics about the current region and/or simulator",
                               DebugMonitors);

            MainServer.Instance.AddHTTPHandler("/monitorstats/" + m_scene.RegionInfo.RegionID + "/", StatsPage);
        }

        public Hashtable StatsPage(Hashtable request)
        {
            string xml = "<data>";
            foreach (IMonitor monitor in m_monitors)
            {
                xml += "<" + monitor.ToString() + ">" + monitor.GetValue() + "</" + monitor.ToString() + ">";
            }
            xml += "</data>";

            Hashtable ereply = new Hashtable();

            ereply["int_response_code"] = 200; // 200 OK
            ereply["str_response_string"] = xml;
            ereply["content_type"] = "text/xml";

            return ereply;
        }

        public void PostInitialise()
        {
            m_monitors.Add(new AgentCountMonitor(m_scene));
            m_monitors.Add(new ChildAgentCountMonitor(m_scene));
            m_monitors.Add(new GCMemoryMonitor());
            m_monitors.Add(new ObjectCountMonitor(m_scene));
            m_monitors.Add(new PhysicsFrameMonitor(m_scene));
            m_monitors.Add(new PhysicsUpdateFrameMonitor(m_scene));
            m_monitors.Add(new PWSMemoryMonitor());
            m_monitors.Add(new ThreadCountMonitor());
            m_monitors.Add(new TotalFrameMonitor(m_scene));
            m_monitors.Add(new EventFrameMonitor(m_scene));
            m_monitors.Add(new LandFrameMonitor(m_scene));
            m_monitors.Add(new LastFrameTimeMonitor(m_scene));

            m_alerts.Add(new DeadlockAlert(m_monitors.Find(x => x is LastFrameTimeMonitor) as LastFrameTimeMonitor));

            foreach (IAlert alert in m_alerts)
            {
                alert.OnTriggerAlert += OnTriggerAlert;
            }
        }

        void OnTriggerAlert(System.Type reporter, string reason, bool fatal)
        {
            m_log.Error("[Monitor] " + reporter.Name + " for " + m_scene.RegionInfo.RegionName + " reports " + reason + " (Fatal: " + fatal + ")");
        }

        public void Close()
        {
            
        }

        public string Name
        {
            get { return "Region Health Monitoring Module"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion
    }
}
