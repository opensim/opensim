using System;
using System.Reflection;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.PhysicsModule.ODE
{
	[Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ODEPhysicsScene")]
    public class OdeModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;
		private IConfigSource m_config;
		private OdeScene  m_scene;

       #region INonSharedRegionModule

        public string Name
        {
            get { return "OpenDynamicsEngine"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["Startup"];
            if (config != null)
            {
                string physics = config.GetString("physics", string.Empty);
                if (physics == Name)
                {
                    m_config = source;
                    m_Enabled = true;
                }
            }
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            if (Util.IsWindows())
                Util.LoadArchSpecificWindowsDll("ode.dll");

            // Initializing ODE only when a scene is created allows alternative ODE plugins to co-habit (according to
            // http://opensimulator.org/mantis/view.php?id=2750).
            d.InitODE();

            m_scene = new OdeScene(scene, m_config, Name);
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled || m_scene == null)
                return;

            m_scene.Dispose();
            m_scene = null;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled || m_scene == null)
                return;

            m_scene.RegionLoaded();
        }
        #endregion			
    }
}
