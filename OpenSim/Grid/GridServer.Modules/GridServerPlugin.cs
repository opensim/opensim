using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Grid.Framework;
using OpenSim.Grid;

namespace OpenSim.Grid.GridServer.Modules
{
    public class GridServerPlugin : IGridPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected GridXmlRpcModule m_gridXmlRpcModule;
        protected GridMessagingModule m_gridMessageModule;
        protected GridRestModule m_gridRestModule;

        protected GridDBService m_gridDBService;

        protected string m_version;

        protected GridConfig m_config;

        protected IGridServiceCore m_core;

        protected ConsoleBase m_console;

        #region IGridPlugin Members

        public void Initialise(GridServerBase gridServer)
        {
            m_core = gridServer;
            m_config = gridServer.Config;
            m_version = gridServer.Version;
            m_console = MainConsole.Instance;

            AddConsoleCommands();

            SetupGridServices();
        }

        #endregion

        #region IPlugin Members

        public string Version
        {
            get { return "0.0"; }
        }

        public string Name
        {
            get { return "GridServerPlugin"; }
        }

        public void Initialise()
        {

        }

        #endregion

        protected virtual void SetupGridServices()
        {
           // m_log.Info("[DATA]: Connecting to Storage Server");
            m_gridDBService = new GridDBService();
            m_gridDBService.AddPlugin(m_config.DatabaseProvider, m_config.DatabaseConnect);

            //Register the database access service so modules can fetch it
            // RegisterInterface<GridDBService>(m_gridDBService);

            m_gridMessageModule = new GridMessagingModule();
            m_gridMessageModule.Initialise(m_version, m_gridDBService, m_core, m_config);

            m_gridXmlRpcModule = new GridXmlRpcModule();
            m_gridXmlRpcModule.Initialise(m_version, m_gridDBService, m_core, m_config);

            m_gridRestModule = new GridRestModule();
            m_gridRestModule.Initialise(m_version, m_gridDBService, m_core, m_config);

            m_gridMessageModule.PostInitialise();
            m_gridXmlRpcModule.PostInitialise();
            m_gridRestModule.PostInitialise();
        }

        #region Console Command Handlers

        protected virtual void AddConsoleCommands()
        {
            m_console.Commands.AddCommand("gridserver", false,
                    "enable registration",
                    "enable registration",
                    "Enable new regions to register", HandleRegistration);

            m_console.Commands.AddCommand("gridserver", false,
                    "disable registration",
                    "disable registration",
                    "Disable registering new regions", HandleRegistration);

            m_console.Commands.AddCommand("gridserver", false, "show status",
                    "show status",
                    "Show registration status", HandleShowStatus);
        }

        private void HandleRegistration(string module, string[] cmd)
        {
            switch (cmd[0])
            {
                case "enable":
                    m_config.AllowRegionRegistration = true;
                    m_log.Info("Region registration enabled");
                    break;
                case "disable":
                    m_config.AllowRegionRegistration = false;
                    m_log.Info("Region registration disabled");
                    break;
            }
        }

        private void HandleShowStatus(string module, string[] cmd)
        {
            if (m_config.AllowRegionRegistration)
            {
                m_log.Info("Region registration enabled.");
            }
            else
            {
                m_log.Info("Region registration disabled.");
            }
        }

        #endregion

       

        #region IDisposable Members

        public void Dispose()
        {
        }

        #endregion
    }
}
