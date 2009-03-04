namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public abstract class MiniModuleBase
    {
        private IWorld m_world;
        private IHost m_host;

        public void InitMiniModule(IWorld world, IHost host)
        {
            m_world = world;
            m_host = host;
        }

        protected IWorld World
        {
            get { return m_world; }
        }

        protected  IHost Host
        {
            get { return m_host; }
        }

        public abstract void Start();
        public abstract void Stop();
    }
}
