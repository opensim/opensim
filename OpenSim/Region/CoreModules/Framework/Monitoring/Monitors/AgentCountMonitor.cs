using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Framework.Monitoring.Monitors
{
    class AgentCountMonitor : IMonitor
    {
        private readonly Scene m_scene;

        public AgentCountMonitor(Scene scene)
        {
            m_scene = scene;
        }

        #region Implementation of IMonitor

        public double GetValue()
        {
            return m_scene.SceneGraph.GetRootAgentCount();
        }

        public string GetName()
        {
            return "Root Agent Count";
        }

        public string GetFriendlyValue()
        {
            return (int)GetValue() + " agent(s)";
        }

        #endregion
    }
}
