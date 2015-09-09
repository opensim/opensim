using System;
using System.Reflection;
using log4net;
using Nini.Config;
using Mono.Addins;
using OdeAPI;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.PhysicsModule.UbitOde
{
	[Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "UBITODEPhysicsScene")]
    class UbitOdeModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;
		private IConfigSource m_config;
		private ODEScene  m_scene;
        private bool OdeUbitLib;

       #region INonSharedRegionModule

        public string Name
        {
            get { return "UbitODE"; }
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

            string ode_config = d.GetConfiguration();
            if (ode_config != null && ode_config != "")
            {
                m_log.InfoFormat("[UbitODE] ode library configuration: {0}", ode_config);

                if (ode_config.Contains("ODE_Ubit"))
                {
                    OdeUbitLib = true;
                }
            }

		m_scene = new ODEScene(scene, m_config, Name, OdeUbitLib);
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
