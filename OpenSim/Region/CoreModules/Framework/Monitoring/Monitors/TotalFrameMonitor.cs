using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Framework.Monitoring.Monitors
{
    class TotalFrameMonitor : IMonitor
    {
        private readonly Scene m_scene;

        public TotalFrameMonitor(Scene scene)
        {
            m_scene = scene;
        }

        #region Implementation of IMonitor

        public double GetValue()
        {
            return m_scene.MonitorFrameTime;
        }

        public string GetName()
        {
            return "Total Frame Time";
        }

        public string GetFriendlyValue()
        {
            return (int)GetValue() + "ms";
        }

        #endregion
    }
}
