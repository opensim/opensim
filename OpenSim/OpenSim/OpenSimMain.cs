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
using System.Text;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Region;
using OpenSim.Terrain;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework;
using OpenSim.UserServer;
using OpenSim.Assets;
using OpenSim.Caches;
using OpenSim.Framework.Console;
using OpenSim.Physics.Manager;
using Nwc.XmlRpc;
using OpenSim.Servers;
using OpenSim.GenericConfig;
using OpenGrid.Framework.Communications;

namespace OpenSim
{

    public class OpenSimMain : RegionServerBase, conscmd_callback
    {
        private CheckSumServer checkServer;
        protected RegionServerCommsManager commsManager;


        public OpenSimMain(bool sandBoxMode, bool startLoginServer, string physicsEngine, bool useConfigFile, bool silent, string configFile)
        {
            this.configFileSetup = useConfigFile;
            m_sandbox = sandBoxMode;
            m_loginserver = startLoginServer;
            m_physicsEngine = physicsEngine;
            m_config = configFile;
            m_console = new ConsoleBase("region-console-" + Guid.NewGuid().ToString() + ".log", "Region", this, silent);
            OpenSim.Framework.Console.MainConsole.Instance = m_console;
        }

        /// <summary>
        /// Performs initialisation of the world, such as loading configuration from disk.
        /// </summary>
        public override void StartUp()
        {
            this.serversData = new NetworkServersInfo();
            try
            {
                this.localConfig = new XmlConfig(m_config);
                this.localConfig.LoadData();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            if (this.configFileSetup)
            {
                this.SetupFromConfigFile(this.localConfig);
            }
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Startup() - Loading configuration");
            this.serversData.InitConfig(this.m_sandbox, this.localConfig);
            this.localConfig.Close();//for now we can close it as no other classes read from it , but this should change

            ClientView.TerrainManager = new TerrainManager(new SecondLife());

            if (m_sandbox)
            {
                this.SetupLocalGridServers();
                this.checkServer = new CheckSumServer(12036);
                this.checkServer.ServerListener();
                this.commsManager = new RegionServerCommsLocal(); 
            }
            else
            {
                this.SetupRemoteGridServers();
                this.commsManager = new RegionServerCommsOGS();
            }

            startuptime = DateTime.Now;

            this.physManager = new OpenSim.Physics.Manager.PhysicsManager();
            this.physManager.LoadPlugins();

            this.SetupWorld();

            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Startup() - Initialising HTTP server");

            this.SetupHttpListener();

            //Login server setup
            LoginServer loginServer = null;
            LoginServer adminLoginServer = null;

            if (m_sandbox)
            {
                loginServer = new LoginServer(regionData[0].IPListenAddr, regionData[0].IPListenPort, regionData[0].RegionLocX, regionData[0].RegionLocY, false);
                loginServer.Startup();
                loginServer.SetSessionHandler(((RegionServerCommsLocal)this.commsManager).SandManager.AddNewSession);
                //sandbox mode with loginserver not using accounts
                httpServer.AddXmlRPCHandler("login_to_simulator", loginServer.XmlRpcLoginMethod);
            }

            //Start http server
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Startup() - Starting HTTP server");
            httpServer.Start();

            // Start UDP servers
            for (int i = 0; i < m_udpServer.Count; i++)
            {
                this.m_udpServer[i].ServerListener();
            }

        }

        # region Setup methods
        protected override void SetupLocalGridServers()
        {
            try
            {
                AssetCache = new AssetCache("OpenSim.GridInterfaces.Local.dll", this.serversData.AssetURL, this.serversData.AssetSendKey);
                InventoryCache = new InventoryCache();
            }
            catch (Exception e)
            {
                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, e.Message + "\nSorry, could not setup local cache");
                Environment.Exit(1);
            }

        }

        protected override void SetupRemoteGridServers()
        {
            try
            {
                AssetCache = new AssetCache("OpenSim.GridInterfaces.Remote.dll", this.serversData.AssetURL, this.serversData.AssetSendKey);
                InventoryCache = new InventoryCache();
            }
            catch (Exception e)
            {
                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, e.Message + "\nSorry, could not setup remote cache");
                Environment.Exit(1);
            }
        }

        protected override void SetupWorld()
        {
            IGenericConfig regionConfig;
            Scene LocalWorld;
            UDPServer udpServer;
            RegionInfo regionDat = new RegionInfo();
            AuthenticateSessionsBase authenBase;

            string path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Regions");
            string[] configFiles = Directory.GetFiles(path, "*.xml");

            if (configFiles.Length == 0)
            {
                string path2 = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Regions");
                string path3 = Path.Combine(path2, "default.xml");
                Console.WriteLine("Creating default region config file");
                //TODO create default region
                IGenericConfig defaultConfig = new XmlConfig(path3);
                defaultConfig.LoadData();
                defaultConfig.Commit();
                defaultConfig.Close();
                defaultConfig = null;
                configFiles = Directory.GetFiles(path, "*.xml");
            }

            for (int i = 0; i < configFiles.Length; i++)
            {
                regionDat = new RegionInfo();
                if (m_sandbox)
                {
                    AuthenticateSessionsBase authen = new AuthenticateSessionsBase();  // new AuthenticateSessionsLocal();
                    this.AuthenticateSessionsHandler.Add(authen);
                    authenBase = authen;
                }
                else
                {
                    AuthenticateSessionsBase authen = new AuthenticateSessionsBase(); //new AuthenticateSessionsRemote();
                    this.AuthenticateSessionsHandler.Add (authen);
                    authenBase = authen;
                }
                Console.WriteLine("Loading region config file");
                regionConfig = new XmlConfig(configFiles[i]);
                regionConfig.LoadData();
                regionDat.InitConfig(this.m_sandbox, regionConfig);
                regionConfig.Close();

                udpServer = new UDPServer(regionDat.IPListenPort, this.AssetCache, this.InventoryCache, this.m_console, authenBase);

                m_udpServer.Add(udpServer);
                this.regionData.Add(regionDat);

                /*
                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.NORMAL, "Main.cs:Startup() - We are " + regionData.RegionName + " at " + regionData.RegionLocX.ToString() + "," + regionData.RegionLocY.ToString());
                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Initialising world");
                m_console.componentname = "Region " + regionData.RegionName;
                */

                LocalWorld = new Scene(udpServer.PacketServer.ClientAPIs, regionDat, authenBase, commsManager);
                this.m_localWorld.Add(LocalWorld);
                //LocalWorld.InventoryCache = InventoryCache;
                //LocalWorld.AssetCache = AssetCache;

                udpServer.LocalWorld = LocalWorld;

                LocalWorld.LoadStorageDLL("OpenSim.Storage.LocalStorageDb4o.dll"); //all these dll names shouldn't be hard coded.
                LocalWorld.LoadWorldMap();

                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Startup() - Starting up messaging system");
                LocalWorld.PhysScene = this.physManager.GetPhysicsScene(this.m_physicsEngine);
                LocalWorld.PhysScene.SetTerrain(LocalWorld.Terrain.getHeights1D());
                LocalWorld.LoadPrimsFromStorage();
                LocalWorld.localStorage.LoadParcels((ILocalStorageParcelReceiver)LocalWorld.parcelManager);


                LocalWorld.StartTimer();
            }
        }

        protected override void SetupHttpListener()
        {
            httpServer = new BaseHttpServer(9000); //regionData[0].IPListenPort);

            if (!this.m_sandbox)
            {

                // we are in Grid mode so set a XmlRpc handler to handle "expect_user" calls from the user server
               

                httpServer.AddRestHandler("GET", "/simstatus/",
                    delegate(string request, string path, string param)
                    {
                        return "OK";
                    });
            }
        }

        protected override void ConnectToRemoteGridServer()
        {

        }

        #endregion

        private void SetupFromConfigFile(IGenericConfig configData)
        {
            try
            {
                // SandBoxMode
                string attri = "";
                attri = configData.GetAttribute("SandBox");
                if ((attri == "") || ((attri != "false") && (attri != "true")))
                {
                    this.m_sandbox = false;
                    configData.SetAttribute("SandBox", "false");
                }
                else
                {
                    this.m_sandbox = Convert.ToBoolean(attri);
                }

                // LoginServer
                attri = "";
                attri = configData.GetAttribute("LoginServer");
                if ((attri == "") || ((attri != "false") && (attri != "true")))
                {
                    this.m_loginserver = false;
                    configData.SetAttribute("LoginServer", "false");
                }
                else
                {
                    this.m_loginserver = Convert.ToBoolean(attri);
                }

                // Sandbox User accounts
                attri = "";
                attri = configData.GetAttribute("UserAccount");
                if ((attri == "") || ((attri != "false") && (attri != "true")))
                {
                    this.user_accounts = false;
                    configData.SetAttribute("UserAccounts", "false");
                }
                else if (attri == "true")
                {
                    this.user_accounts = Convert.ToBoolean(attri);
                }

                // Grid mode hack to use local asset server
                attri = "";
                attri = configData.GetAttribute("LocalAssets");
                if ((attri == "") || ((attri != "false") && (attri != "true")))
                {
                    this.gridLocalAsset = false;
                    configData.SetAttribute("LocalAssets", "false");
                }
                else if (attri == "true")
                {
                    this.gridLocalAsset = Convert.ToBoolean(attri);
                }


                attri = "";
                attri = configData.GetAttribute("PhysicsEngine");
                switch (attri)
                {
                    default:
                        m_console.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "Main.cs: SetupFromConfig() - Invalid value for PhysicsEngine attribute, terminating");
                        Environment.Exit(1);
                        break;

                    case "":
                        this.m_physicsEngine = "basicphysics";
                        configData.SetAttribute("PhysicsEngine", "basicphysics");
                        OpenSim.Region.Avatar.PhysicsEngineFlying = false;
                        break;

                    case "basicphysics":
                        this.m_physicsEngine = "basicphysics";
                        configData.SetAttribute("PhysicsEngine", "basicphysics");
                        OpenSim.Region.Avatar.PhysicsEngineFlying = false;
                        break;

                    case "RealPhysX":
                        this.m_physicsEngine = "RealPhysX";
                        OpenSim.Region.Avatar.PhysicsEngineFlying = true;
                        break;

                    case "OpenDynamicsEngine":
                        this.m_physicsEngine = "OpenDynamicsEngine";
                        OpenSim.Region.Avatar.PhysicsEngineFlying = true;
                        break;
                }

                configData.Commit();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("\nSorry, a fatal error occurred while trying to initialise the configuration data");
                Console.WriteLine("Can not continue starting up");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Performs any last-minute sanity checking and shuts down the region server
        /// </summary>
        public virtual void Shutdown()
        {
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Shutdown() - Closing all threads");
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Shutdown() - Killing listener thread");
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Shutdown() - Killing clients");
            // IMPLEMENT THIS
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Shutdown() - Closing console and terminating");
            for (int i = 0; i < m_localWorld.Count; i++)
            {
                ((Scene)m_localWorld[i]).Close();
            }
            m_console.Close();
            Environment.Exit(0);
        }

        #region Console Commands
        /// <summary>
        /// Runs commands issued by the server console from the operator
        /// </summary>
        /// <param name="command">The first argument of the parameter (the command)</param>
        /// <param name="cmdparams">Additional arguments passed to the command</param>
        public void RunCmd(string command, string[] cmdparams)
        {
            switch (command)
            {
                case "help":
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, "show users - show info about connected users");
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, "shutdown - disconnect all clients and shutdown");
                    break;

                case "show":
                    if (cmdparams.Length > 0)
                    {
                        Show(cmdparams[0]);
                    }
                    break;

                case "terrain":
                    //string result = "";
                    /* if (!((World)m_localWorld).Terrain.RunTerrainCmd(cmdparams, ref result))
                     {
                         m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, result);
                     }*/
                    break;

                case "shutdown":
                    Shutdown();
                    break;

                default:
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, "Unknown command");
                    break;
            }
        }

        /// <summary>
        /// Outputs to the console information about the region
        /// </summary>
        /// <param name="ShowWhat">What information to display (valid arguments are "uptime", "users")</param>
        public void Show(string ShowWhat)
        {
            switch (ShowWhat)
            {
                case "uptime":
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, "OpenSim has been running since " + startuptime.ToString());
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, "That is " + (DateTime.Now - startuptime).ToString());
                    break;
                case "users":
                    OpenSim.Region.Avatar TempAv;
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16}{5,-16}", "Firstname", "Lastname", "Agent ID", "Session ID", "Circuit", "IP"));
                    /* foreach (libsecondlife.LLUUID UUID in LocalWorld.Entities.Keys)
                     {
                         if (LocalWorld.Entities[UUID].ToString() == "OpenSim.world.Avatar")
                         {
                             TempAv = (OpenSim.world.Avatar)LocalWorld.Entities[UUID];
                             m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16},{5,-16}", TempAv.firstname, TempAv.lastname, UUID, TempAv.ControllingClient.SessionID, TempAv.ControllingClient.CircuitCode, TempAv.ControllingClient.userEP.ToString()));
                         }
                     }*/
                    break;
            }
        }
        #endregion
    }


}