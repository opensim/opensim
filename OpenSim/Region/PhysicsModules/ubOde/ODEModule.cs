using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using Mono.Addins;
using OdeAPI;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.PhysicsModule.ubOde
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ubODEPhysicsScene")]
    class ubOdeModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static Dictionary<Scene, ODEScene> m_scenes = new Dictionary<Scene, ODEScene>();
        private bool m_Enabled = false;
        private IConfigSource m_config;
        private bool OSOdeLib;


       #region INonSharedRegionModule

        public string Name
        {
            get { return "ubODE"; }
        }

        public string Version
        {
            get { return "1.0"; }
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

                    if (Util.IsWindows())
                        Util.LoadArchSpecificWindowsDll("ode.dll");

                    d.InitODE();

                    string ode_config = d.GetConfiguration();
                    if (ode_config != null && ode_config != "")
                    {
                        m_log.InfoFormat("[ubODE] ode library configuration: {0}", ode_config);

                        if (ode_config.Contains("ODE_OPENSIM"))
                        {
                            OSOdeLib = true;
                        }
                    }
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

            if(m_scenes.ContainsKey(scene)) // ???
                return;
            ODEScene newodescene = new ODEScene(scene, m_config, Name, Version, OSOdeLib);
            m_scenes[scene] = newodescene;
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            // a odescene.dispose is called later directly by scene.cs
            // since it is seen as a module interface

            if(m_scenes.ContainsKey(scene))
                m_scenes.Remove(scene);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            if(m_scenes.ContainsKey(scene))
            {
                m_scenes[scene].RegionLoaded();
            }

        }
        #endregion
    }
}
