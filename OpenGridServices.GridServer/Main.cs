/*
Copyright (c) OpenSim project, http://osgrid.org/


* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.IO;
using System.Text;
using System.Timers;
using System.Net;
using System.Reflection;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Sims;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;

namespace OpenGridServices.GridServer
{
    /// <summary>
    /// </summary>
    public class OpenGrid_Main : conscmd_callback
    {
	private string ConfigDll = "OpenGrid.Config.GridConfigDb4o.dll";
	private GridConfig Cfg;
        public static OpenGrid_Main thegrid;
        public string GridOwner;
        public string DefaultStartupMsg;
        public string DefaultAssetServer;
        public string AssetSendKey;
        public string AssetRecvKey;
        public string DefaultUserServer;
        public string UserSendKey;
        public string UserRecvKey;
	public string SimSendKey;
	public string SimRecvKey;
	public LLUUID highestUUID;

        public GridHTTPServer _httpd;
        public SimProfileManager _regionmanager;

        private ConsoleBase m_console;
	private Timer SimCheckTimer;
        
        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting...\n");

            thegrid = new OpenGrid_Main();
            thegrid.Startup();

            thegrid.Work();
        }

        private void Work()
        {
            m_console.WriteLine("\nEnter help for a list of commands\n");

            while (true)
            {
                m_console.MainConsolePrompt();
            }
        }

        private OpenGrid_Main()
        {
            m_console = new ConsoleBase("opengrid-gridserver-console.log", "OpenGrid", this);
            MainConsole.Instance = m_console;
        }
        
        public void Startup()
        {
            m_console.WriteLine("Main.cs:Startup() - Loading configuration");
            Cfg = this.LoadConfigDll(this.ConfigDll);
            Cfg.InitConfig();

	    m_console.WriteLine("Main.cs:Startup() - Loading sim profiles from database");
	    this._regionmanager = new SimProfileManager();
	    _regionmanager.LoadProfiles();

	    m_console.WriteLine("Main.cs:Startup() - Starting HTTP process");
            _httpd = new GridHTTPServer();
            _httpd.Start();

	    m_console.WriteLine("Main.cs:Startup() - Starting sim status checker");
	    SimCheckTimer = new Timer();
	    SimCheckTimer.Interval = 300000;		// 5 minutes
	    SimCheckTimer.Elapsed+=new ElapsedEventHandler(CheckSims);
	    SimCheckTimer.Enabled=true;
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

	public void CheckSims(object sender, ElapsedEventArgs e) {
		foreach(SimProfileBase sim in _regionmanager.SimProfiles.Values) {
			string SimResponse="";
			try {
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
			} catch(Exception exception) {
			}
			if(SimResponse=="OK") {
				_regionmanager.SimProfiles[sim.UUID].online=true;
			} else {
				_regionmanager.SimProfiles[sim.UUID].online=false;
			}
		}
	}

	public void RunCmd(string cmd, string[] cmdparams)
        {
            switch (cmd)
            {
                case "help":
                    m_console.WriteLine("shutdown - shutdown the grid (USE CAUTION!)");
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
    }
}
