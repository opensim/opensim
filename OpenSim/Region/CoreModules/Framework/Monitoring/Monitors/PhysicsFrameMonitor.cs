using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Framework.Monitoring.Monitors
{
    class PhysicsFrameMonitor : IMonitor
    {
        private readonly Scene m_scene;

        public PhysicsFrameMonitor(Scene scene)
        {
            m_scene = scene;
        }

        #region Implementation of IMonitor

        public double GetValue()
        {
            return m_scene.MonitorPhysicsSyncTime + m_scene.MonitorPhysicsUpdateTime;
        }

        public string GetName()
        {
            return "Total Physics Frame Time";
        }

        public string GetFriendlyValue()
        {
            return (int)GetValue() + "ms";
        }

        #endregion
    }
}
