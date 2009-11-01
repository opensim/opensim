using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenSim.Region.CoreModules.Framework.Monitoring.Monitors;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Framework.Monitoring
{
    public class MonitorModule : IRegionModule 
    {
        private Scene m_scene;
        private readonly List<IMonitor> m_monitors = new List<IMonitor>();
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public void DebugMonitors(string module, string[] args)
        {
            foreach (IMonitor monitor in m_monitors)
            {
                m_log.Info("[MonitorModule] " + m_scene.RegionInfo.RegionName + " reports " + monitor.GetName() + " = " + monitor.GetValue());
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
