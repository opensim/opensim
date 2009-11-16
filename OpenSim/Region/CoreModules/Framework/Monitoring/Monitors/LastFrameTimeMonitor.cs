using System;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Framework.Monitoring.Monitors
{
    class LastFrameTimeMonitor : IMonitor
    {
        private readonly Scene m_scene;

        public LastFrameTimeMonitor(Scene scene)
        {
            m_scene = scene;
        }

        #region Implementation of IMonitor

        public double GetValue()
        {
            return Environment.TickCount - m_scene.MonitorLastFrameTick;
        }

        public string GetName()
        {
            return "Last Completed Frame At";
        }

        public string GetFriendlyValue()
        {
            return (int)GetValue() + "ms ago";
        }

        #endregion
    }
}
