using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Framework.Monitoring.Monitors
{
    class ChildAgentCountMonitor : IMonitor
    {
        private readonly Scene m_scene;

        public ChildAgentCountMonitor(Scene scene)
        {
            m_scene = scene;
        }

        #region Implementation of IMonitor

        public double GetValue()
        {
            return m_scene.SceneGraph.GetChildAgentCount();
        }

        public string GetName()
        {
            return "Child Agent Count";
        }

        public string GetFriendlyValue()
        {
            return (int)GetValue() + " child agent(s)";
        }

        #endregion
    }
}
