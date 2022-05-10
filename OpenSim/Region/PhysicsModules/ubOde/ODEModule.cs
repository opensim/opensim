using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.PhysicsModule.ubOde
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ubODEPhysicsScene")]
    class ubOdeModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        ODEScene m_odeScene = null;

        private IConfigSource m_config;
        private string m_libVersion = string.Empty;
        private bool m_Enabled = false;

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
                    string mesher = config.GetString("meshing", string.Empty);
                    
                    if (string.IsNullOrEmpty(mesher) || !mesher.Equals("ubODEMeshmerizer"))
                    {
                        m_log.Error("[ubODE] Opensim.ini meshing option must be set to \"ubODEMeshmerizer\"");
                        //throw new Exception("Invalid physics meshing option");
                    }

                    if (Util.IsWindows())
                        Util.LoadArchSpecificWindowsDll("ubode.dll");

                    SafeNativeMethods.InitODE();

                    string ode_config = SafeNativeMethods.GetConfiguration();
                    if (string.IsNullOrEmpty(ode_config))
                    {
                        m_log.Error("[ubODE] Native ode library version not supported");
                        return;
                    }

                    int indx = ode_config.IndexOf("ODE_OPENSIM");
                    if (indx < 0)
                    {
                        m_log.Error("[ubODE] Native ode library version not supported");
                        return;
                    }
                    indx += 12;
                    if (indx >= ode_config.Length)
                    {
                        m_log.Error("[ubODE] Native ode library version not supported");
                        return;
                    }
                    m_libVersion = ode_config.Substring(indx);
                    if (string.IsNullOrEmpty(m_libVersion))
                    {
                        m_log.Error("[ubODE] Native ode library version not supported");
                        return;
                    }
                    m_libVersion.Trim();
                    if(m_libVersion.StartsWith("OS"))
                        m_libVersion = m_libVersion.Substring(2);

                    m_log.InfoFormat("[ubODE] ode library configuration: {0}", ode_config);
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

            m_odeScene = new ODEScene(scene, m_config, Name, Version + "-" + m_libVersion);
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            // a odescene.dispose is called later directly by scene.cs
            // since it is seen as a module interface

            m_odeScene = null;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            if(m_odeScene != null)
                m_odeScene.RegionLoaded();

        }
        #endregion
    }
}
