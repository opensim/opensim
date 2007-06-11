/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System;
using System.IO;
using System.Text;
using System.Timers;
using System.Net;
using System.Threading;
using System.Reflection;
using libsecondlife;
using OpenGrid.Framework.Manager;
using OpenSim.Framework;
using OpenSim.Framework.Sims;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Servers;
using OpenSim.GenericConfig;

namespace OpenGridServices.GridServer
{
    /// <summary>
    /// </summary>
    public class OpenGrid_Main : conscmd_callback
    {
        private string ConfigDll = "OpenGrid.Config.GridConfigDb4o.dll";
        private string GridDll = "OpenGrid.Framework.Data.MySQL.dll";
        public GridConfig Cfg;

        public static OpenGrid_Main thegrid;
        protected IGenericConfig localXMLConfig;

        public static bool setuponly;

        //public LLUUID highestUUID;

        //        private SimProfileManager m_simProfileManager;

        private GridManager m_gridManager;

        private ConsoleBase m_console;

        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                if (args[0] == "-setuponly") setuponly = true;
            }
            Console.WriteLine("Starting...\n");

            thegrid = new OpenGrid_Main();
            thegrid.Startup();

            thegrid.Work();
        }

        private void Work()
        {
            while (true)
            {
                Thread.Sleep(5000);
                // should flush the DB etc here
            }
        }

        private OpenGrid_Main()
        {
            m_console = new ConsoleBase("opengrid-gridserver-console.log", "OpenGrid", this, false);
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


        public void Startup()
        {
            this.localXMLConfig = new XmlConfig("GridServerConfig.xml");
            this.localXMLConfig.LoadData();
            this.ConfigDB(this.localXMLConfig);
            this.localXMLConfig.Close();

            m_console.Verbose( "Main.cs:Startup() - Loading configuration");
            Cfg = this.LoadConfigDll(this.ConfigDll);
            Cfg.InitConfig();
            if (setuponly) Environment.Exit(0);

            m_console.Verbose( "Main.cs:Startup() - Connecting to Storage Server");
            m_gridManager = new GridManager();
            m_gridManager.AddPlugin(GridDll); // Made of win
            m_gridManager.config = Cfg;

            m_console.Verbose( "Main.cs:Startup() - Starting HTTP process");
            BaseHttpServer httpServer = new BaseHttpServer(8001);
            //GridManagementAgent GridManagerAgent = new GridManagementAgent(httpServer, "gridserver", Cfg.SimSendKey, Cfg.SimRecvKey, managercallback);

            httpServer.AddXmlRPCHandler("simulator_login", m_gridManager.XmlRpcLoginToSimulatorMethod);
            httpServer.AddXmlRPCHandler("map_block", m_gridManager.XmlRpcMapBlockMethod);

            httpServer.AddRestHandler("GET", "/sims/", m_gridManager.RestGetSimMethod);
            httpServer.AddRestHandler("POST", "/sims/", m_gridManager.RestSetSimMethod);
            httpServer.AddRestHandler("GET", "/regions/", m_gridManager.RestGetRegionMethod);
            httpServer.AddRestHandler("POST", "/regions/", m_gridManager.RestSetRegionMethod);


            // lbsa71 : This code snippet taken from old http server.
            // I have no idea what this was supposed to do - looks like an infinite recursion to me.
            //        case "regions":
            //// DIRTY HACK ALERT
            //Console.Notice("/regions/ accessed");
            //TheSim = OpenGrid_Main.thegrid._regionmanager.GetProfileByHandle((ulong)Convert.ToUInt64(rest_params[1]));
            //respstring = ParseREST("/regions/" + rest_params[1], requestBody, HTTPmethod);
            //break;

            // lbsa71 : I guess these were never used?
            //Listener.Prefixes.Add("http://+:8001/gods/");
            //Listener.Prefixes.Add("http://+:8001/highestuuid/");
            //Listener.Prefixes.Add("http://+:8001/uuidblocks/");

            httpServer.Start();

            m_console.Verbose( "Main.cs:Startup() - Starting sim status checker");

            System.Timers.Timer simCheckTimer = new System.Timers.Timer(3600000 * 3); // 3 Hours between updates.
            simCheckTimer.Elapsed += new ElapsedEventHandler(CheckSims);
            simCheckTimer.Enabled = true;
        }

        private GridConfig LoadConfigDll(string dllName)
        {
            Assembly pluginAssembly = Assembly.LoadFrom(dllName);
            GridConfig config = null;

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("IGridConfig", true);

                        if (typeInterface != null)
                        {
                            IGridConfig plug = (IGridConfig)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            config = plug.GetConfigObject();
                            break;
                        }

                        typeInterface = null;
                    }
                }
            }
            pluginAssembly = null;
            return config;
        }

        public void CheckSims(object sender, ElapsedEventArgs e)
        {
            /*
            foreach (SimProfileBase sim in m_simProfileManager.SimProfiles.Values)
            {
                string SimResponse = "";
                try
                {
                    WebRequest CheckSim = WebRequest.Create("http://" + sim.sim_ip + ":" + sim.sim_port.ToString() + "/checkstatus/");
                    CheckSim.Method = "GET";
                    CheckSim.ContentType = "text/plaintext";
                    CheckSim.ContentLength = 0;

                    StreamWriter stOut = new StreamWriter(CheckSim.GetRequestStream(), System.Text.Encoding.ASCII);
                    stOut.Write("");
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

        public void RunCmd(string cmd, string[] cmdparams)
        {
            switch (cmd)
            {
                case "help":
                    m_console.Notice("shutdown - shutdown the grid (USE CAUTION!)");
                    break;

                case "shutdown":
                    m_console.Close();
                    Environment.Exit(0);
                    break;
            }
        }

        public void Show(string ShowWhat)
        {
        }

        private void ConfigDB(IGenericConfig configData)
        {
            try
            {
                string attri = "";
                attri = configData.GetAttribute("DataBaseProvider");
                if (attri == "")
                {
                    GridDll = "OpenGrid.Framework.Data.DB4o.dll";
                    configData.SetAttribute("DataBaseProvider", "OpenGrid.Framework.Data.DB4o.dll");
                }
                else
                {
                    GridDll = attri;
                }
                configData.Commit();
            }
            catch (Exception e)
            {

            }
        }
    }
}
