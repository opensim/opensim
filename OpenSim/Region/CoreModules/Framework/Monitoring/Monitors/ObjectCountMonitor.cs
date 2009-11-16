using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Framework.Monitoring.Monitors
{
    class ObjectCountMonitor : IMonitor
    {
        private readonly Scene m_scene;

        public ObjectCountMonitor(Scene scene)
        {
            m_scene = scene;
        }

        #region Implementation of IMonitor

        public double GetValue()
        {
            return m_scene.SceneGraph.GetTotalObjectsCount();
        }

        public string GetName()
        {
            return "Total Objects Count";
        }

        public string GetFriendlyValue()
        {
            return (int)GetValue() + " Object(s)";
        }

        #endregion
    }
}
