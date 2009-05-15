/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

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

        protected CommandConsole m_console;

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

        public void PostInitialise()
        {

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
