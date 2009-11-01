using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Framework.Monitoring.Monitors
{
    class LandFrameMonitor : IMonitor
    {
        private readonly Scene m_scene;

        public LandFrameMonitor(Scene scene)
        {
            m_scene = scene;
        }

        #region Implementation of IMonitor

        public double GetValue()
        {
            return m_scene.MonitorLandTime;
        }

        public string GetName()
        {
            return "Land Frame Time";
        }

        public string GetFriendlyValue()
        {
            return (int)GetValue() + "ms";
        }

        #endregion
    }
}
