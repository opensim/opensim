using System;
using System.Reflection;
using System.Collections.Generic;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;


namespace OpenSim.SimulatorServices
{
    public class SimulationService : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool m_Enabled = false;

        private IConfigSource m_Config;
        bool m_Registered = false;

        #region IRegionModule interface

        public void Initialise(IConfigSource config)
        {
            m_Config = config;

            IConfig moduleConfig = config.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("AssetServices", "");
                if (name == Name)
                {
                    m_Enabled = true;
                    m_log.Info("[SIM SERVICE]: SimulationService enabled");

                }
            }

        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "SimulationService"; }
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            if (!m_Registered)
            {
                m_Registered = true;

                m_log.Info("[SIM SERVICE]: Starting...");

                Object[] args = new Object[] { m_Config, scene.CommsManager.HttpServer, scene };

                ServerUtils.LoadPlugin<IServiceConnector>("OpenSim.Server.Handlers.dll:SimulationServiceInConnector", args);
            }
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }

        #endregion

    }
}
