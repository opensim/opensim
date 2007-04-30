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
using OpenSim.GridInterfaces.Local;		// REFACTORING IS NEEDED!!!!!!!!!!!
using OpenSim.Servers;

namespace OpenGridServices.AssetServer
{
    /// <summary>
    /// </summary>
    public class OpenAsset_Main : BaseServer, conscmd_callback
    {
        private IObjectContainer db;
        
        public static OpenAsset_Main assetserver;

        private ConsoleBase m_console;

        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting...\n");

            assetserver = new OpenAsset_Main();
            assetserver.Startup();

            assetserver.Work();
        }

        private void Work()
        {
            m_console.WriteLine("\nEnter help for a list of commands\n");

            while (true)
            {
                m_console.MainConsolePrompt();
            }
        }

        private OpenAsset_Main()
        {
            m_console = new ConsoleBase("opengrid-AssetServer-console.log", "OpenGrid", this, false);
            MainConsole.Instance = m_console;
        }

        public void Startup()
        {
            /*m_console.WriteLine("Main.cs:Startup() - Loading configuration");
            Cfg = this.LoadConfigDll(this.ConfigDll);
            Cfg.InitConfig();*/


            m_console.WriteLine("Main.cs:Startup() - Starting HTTP process");
            BaseHttpServer httpServer = new BaseHttpServer(8003);

            /*httpServer.AddRestHandler("GET", "/sims/", m_simProfileManager.RestGetSimMethod);
            httpServer.AddRestHandler("POST", "/sims/", m_simProfileManager.RestSetSimMethod);
	    httpServer.AddRestHandler("GET", "/regions/", m_simProfileManager.RestGetRegionMethod);
	    httpServer.AddRestHandler("POST", "/regions/", m_simProfileManager.RestSetRegionMethod);*/
	    httpServer.AddRestHAndler("GET", "/assets/", this.assetGetMethod);


            httpServer.Start();

        }

        /*private GridConfig LoadConfigDll(string dllName)
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
        }*/

        public void RunCmd(string cmd, string[] cmdparams)
        {
            switch (cmd)
            {
                case "help":
                    m_console.WriteLine("shutdown - shutdown this asset server (USE CAUTION!)");
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
