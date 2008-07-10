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
    public class GridServerBase : BaseOpenSimServer, conscmd_callback
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected GridConfig m_config;
        protected GridManager m_gridManager;
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
            m_console = new ConsoleBase("Grid", this);
            MainConsole.Instance = m_console;
        }

        public void managercallback(string cmd)
        {
            switch (cmd)
            {
                case "shutdown":
                    RunCmd("shutdown", new string[0]);
                    break;
            }
        }

        public override void Startup()
        {
            base.Startup();

            Config();

            SetupGridManager();

            m_log.Info("[GRID]: Starting HTTP process");
            m_httpServer = new BaseHttpServer(m_config.HttpPort);

            AddHttpHandlers();

            LoadPlugins();

            m_httpServer.Start();

//            m_log.Info("[GRID]: Starting sim status checker");
//
//            Timer simCheckTimer = new Timer(3600000 * 3); // 3 Hours between updates.
//            simCheckTimer.Elapsed += new ElapsedEventHandler(CheckSims);
//            simCheckTimer.Enabled = true;
        }

        protected void AddHttpHandlers()
        {
            m_httpServer.AddXmlRPCHandler("simulator_login", m_gridManager.XmlRpcSimulatorLoginMethod);
            m_httpServer.AddXmlRPCHandler("simulator_data_request", m_gridManager.XmlRpcSimulatorDataRequestMethod);
            m_httpServer.AddXmlRPCHandler("simulator_after_region_moved", m_gridManager.XmlRpcDeleteRegionMethod);
            m_httpServer.AddXmlRPCHandler("map_block", m_gridManager.XmlRpcMapBlockMethod);

            // Message Server ---> Grid Server
            m_httpServer.AddXmlRPCHandler("register_messageserver", m_gridManager.XmlRPCRegisterMessageServer);
            m_httpServer.AddXmlRPCHandler("deregister_messageserver", m_gridManager.XmlRPCDeRegisterMessageServer);

            m_httpServer.AddStreamHandler(new RestStreamHandler("GET", "/sims/", m_gridManager.RestGetSimMethod));
            m_httpServer.AddStreamHandler(new RestStreamHandler("POST", "/sims/", m_gridManager.RestSetSimMethod));

            m_httpServer.AddStreamHandler(new RestStreamHandler("GET", "/regions/", m_gridManager.RestGetRegionMethod));
            m_httpServer.AddStreamHandler(new RestStreamHandler("POST", "/regions/", m_gridManager.RestSetRegionMethod));
        }

        protected void LoadPlugins()
        {
            PluginLoader<IGridPlugin> loader = 
                new PluginLoader<IGridPlugin> (new GridPluginInitialiser (this));

            loader.Load ("/OpenSim/GridServer");
            m_plugins = loader.Plugins;
        }

        protected virtual void SetupGridManager()
        {
            m_log.Info("[DATA]: Connecting to Storage Server");
            m_gridManager = new GridManager();
            m_gridManager.AddPlugin(m_config.DatabaseProvider, m_config.DatabaseConnect);
            m_gridManager.Config = m_config;
        }

        public void Config()
        {
            m_config = new GridConfig("GRID SERVER", (Path.Combine(Util.configDir(), "GridServer_Config.xml")));
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

        public override void Shutdown()
        {
            foreach (IGridPlugin plugin in m_plugins) plugin.Dispose();

            base.Shutdown();
        }
    }
}
