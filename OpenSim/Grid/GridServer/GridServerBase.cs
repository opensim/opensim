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
 *     * Neither the name of the OpenSim Project nor the
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
using System.IO;
using System.Reflection;
using System.Timers;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;

namespace OpenSim.Grid.GridServer
{
    /// <summary>
    /// </summary>
    public class GridServerBase : BaseOpenSimServer, IGridCore
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected GridConfig m_config;

        protected GridXmlRpcModule m_gridXmlRpcModule;
        protected GridMessagingModule m_gridMessageModule;
        protected GridRestModule m_gridRestModule;

        protected GridDBService m_gridDBService;

        protected List<IGridPlugin> m_plugins = new List<IGridPlugin>();

        public void Work()
        {
            m_console.Notice("Enter help for a list of commands\n");

            while (true)
            {
                m_console.Prompt();
            }
        }

        public GridServerBase()
        {
            m_console = new ConsoleBase("Grid");
            MainConsole.Instance = m_console;
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


        protected override void StartupSpecific()
        {
            m_config = new GridConfig("GRID SERVER", (Path.Combine(Util.configDir(), "GridServer_Config.xml")));

            m_log.Info("[GRID]: Starting HTTP process");
            m_httpServer = new BaseHttpServer(m_config.HttpPort);

            SetupGridServices();

            AddHttpHandlers();

            LoadPlugins();

            m_httpServer.Start();

            //            m_log.Info("[GRID]: Starting sim status checker");
            //
            //            Timer simCheckTimer = new Timer(3600000 * 3); // 3 Hours between updates.
            //            simCheckTimer.Elapsed += new ElapsedEventHandler(CheckSims);
            //            simCheckTimer.Enabled = true;

            base.StartupSpecific();

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

        protected void AddHttpHandlers()
        {
            // Registering Handlers is now done in the components/modules
        }

        protected void LoadPlugins()
        {
            PluginLoader<IGridPlugin> loader =
                new PluginLoader<IGridPlugin>(new GridPluginInitialiser(this));

            loader.Load("/OpenSim/GridServer");
            m_plugins = loader.Plugins;
        }

        protected virtual void SetupGridServices()
        {
            m_log.Info("[DATA]: Connecting to Storage Server");
            m_gridDBService = new GridDBService();
            m_gridDBService.AddPlugin(m_config.DatabaseProvider, m_config.DatabaseConnect);

            m_gridMessageModule = new GridMessagingModule(m_version, m_gridDBService, this, m_config);
            m_gridMessageModule.Initialise();

            m_gridXmlRpcModule = new GridXmlRpcModule(m_version, m_gridDBService, this, m_config);
            m_gridXmlRpcModule.Initialise();

            m_gridRestModule = new GridRestModule(m_version, m_gridDBService, this, m_config);
            m_gridRestModule.Initialise();
        }

        public void CheckSims(object sender, ElapsedEventArgs e)
        {
            /*
            foreach (SimProfileBase sim in m_simProfileManager.SimProfiles.Values)
            {
                string SimResponse = String.Empty;
                try
                {
                    WebRequest CheckSim = WebRequest.Create("http://" + sim.sim_ip + ":" + sim.sim_port.ToString() + "/checkstatus/");
                    CheckSim.Method = "GET";
                    CheckSim.ContentType = "text/plaintext";
                    CheckSim.ContentLength = 0;

                    StreamWriter stOut = new StreamWriter(CheckSim.GetRequestStream(), System.Text.Encoding.ASCII);
                    stOut.Write(String.Empty);
                    stOut.Close();

                    StreamReader stIn = new StreamReader(CheckSim.GetResponse().GetResponseStream());
                    SimResponse = stIn.ReadToEnd();
                    stIn.Close();
                }
                catch
                {
                }

                if (SimResponse == "OK")
                {
                    m_simProfileManager.SimProfiles[sim.UUID].online = true;
                }
                else
                {
                    m_simProfileManager.SimProfiles[sim.UUID].online = false;
                }
            }
            */
        }

        public override void ShutdownSpecific()
        {
            foreach (IGridPlugin plugin in m_plugins) plugin.Dispose();
        }

        #region IGridCore
        private readonly Dictionary<Type, object> m_gridInterfaces = new Dictionary<Type, object>();

        /// <summary>
        /// Register an interface on this client, should only be called in the constructor.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="iface"></param>
        public void RegisterInterface<T>(T iface)
        {
            lock (m_gridInterfaces)
            {
                m_gridInterfaces.Add(typeof(T), iface);
            }
        }

        public bool TryGet<T>(out T iface)
        {
            if (m_gridInterfaces.ContainsKey(typeof(T)))
            {
                iface = (T)m_gridInterfaces[typeof(T)];
                return true;
            }
            iface = default(T);
            return false;
        }

        public T Get<T>()
        {
            return (T)m_gridInterfaces[typeof(T)];
        }

        public BaseHttpServer GetHttpServer()
        {
            return m_httpServer;
        }
        #endregion
    }
}
