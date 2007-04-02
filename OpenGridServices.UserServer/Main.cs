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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Text;
using libsecondlife;
using OpenSim.Framework.User;
using OpenSim.Framework.Sims;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Console;

namespace OpenGridServices.UserServer
{
	/// <summary>
	/// </summary>
	public class OpenUser_Main : conscmd_callback
	{
		private string ConfigDll = "OpenUser.Config.UserConfigDb4o.dll";
	        private UserConfig Cfg;

		public static OpenUser_Main userserver;
		
		public UserHTTPServer _httpd;
		public UserProfileManager _profilemanager;

		public Dictionary<LLUUID, UserProfile> UserSessions = new Dictionary<LLUUID, UserProfile>();

	        ConsoleBase m_console;
	    
		[STAThread]
		public static void Main( string[] args )
		{
			Console.WriteLine("Starting...\n");

			userserver = new OpenUser_Main();
			userserver.Startup();	
		    
		    userserver.Work();
		}

	    private OpenUser_Main()
	    {
        	m_console = new ConsoleBase("opengrid-userserver-console.log", "OpenUser", this);
            	MainConsole.Instance = m_console;
            }
	
	    private void Work()
	    {
            m_console.WriteLine("\nEnter help for a list of commands\n");

            while (true)
            {
                m_console.MainConsolePrompt();
            }
	    }
	    
		public void Startup() {
			MainConsole.Instance.WriteLine("Main.cs:Startup() - Loading configuration");
            		Cfg = this.LoadConfigDll(this.ConfigDll);
            		Cfg.InitConfig();

			MainConsole.Instance.WriteLine("Main.cs:Startup() - Creating user profile manager");
			_profilemanager = new UserProfileManager();
			_profilemanager.InitUserProfiles();
            		_profilemanager.SetKeys(Cfg.GridSendKey, Cfg.GridRecvKey, Cfg.GridServerURL, Cfg.DefaultStartupMsg);

			MainConsole.Instance.WriteLine("Main.cs:Startup() - Starting HTTP process");
			_httpd = new UserHTTPServer();
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

	private UserConfig LoadConfigDll(string dllName)
        {
            Assembly pluginAssembly = Assembly.LoadFrom(dllName);
            UserConfig config = null;

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("IUserConfig", true);

                        if (typeInterface != null)
                        {
                            IUserConfig plug = (IUserConfig)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
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
	    
        public void Show(string ShowWhat)
        {
        }
	}
}
