using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Framework.Monitoring.Monitors
{
    class PhysicsUpdateFrameMonitor : IMonitor
    {
        private readonly Scene m_scene;

        public PhysicsUpdateFrameMonitor(Scene scene)
        {
            m_scene = scene;
        }

        #region Implementation of IMonitor

        public double GetValue()
        {
            return m_scene.MonitorPhysicsUpdateTime;
        }

        public string GetName()
        {
            return "Physics Update Frame Time";
        }

        public string GetFriendlyValue()
        {
            return (int)GetValue() + "ms";
        }

        #endregion
    }
}
