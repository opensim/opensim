using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Framework.Monitoring.Monitors
{
    class EventFrameMonitor : IMonitor
    {
        private readonly Scene m_scene;

        public EventFrameMonitor(Scene scene)
        {
            m_scene = scene;
        }

        #region Implementation of IMonitor

        public double GetValue()
        {
            return m_scene.MonitorEventTime;
        }

        public string GetName()
        {
            return "Total Event Frame Time";
        }

        public string GetFriendlyValue()
        {
            return (int)GetValue() + "ms";
        }

        #endregion
    }
}
