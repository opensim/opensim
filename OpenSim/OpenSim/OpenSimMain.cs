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
using OpenSim.world;
using OpenSim.Terrain;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.UserServer;
using OpenSim.Assets;
using OpenSim.CAPS;
using OpenSim.Framework.Console;
using OpenSim.Physics.Manager;
using Nwc.XmlRpc;
using OpenSim.Servers;
using OpenSim.GenericConfig;

namespace OpenSim
{

    public class OpenSimMain : RegionServerBase, conscmd_callback
    {
        private CheckSumServer checkServer;

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
            this.regionData = new RegionInfo();
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
            this.regionData.InitConfig(this.m_sandbox, this.localConfig);
            this.localConfig.Close();//for now we can close it as no other classes read from it , but this should change

            GridServers = new Grid();
            if (m_sandbox)
            {
                this.SetupLocalGridServers();
                //Authenticate Session Handler
                AuthenticateSessionsLocal authen = new AuthenticateSessionsLocal();
                this.AuthenticateSessionsHandler = authen;
                this.checkServer = new CheckSumServer(12036);
                this.checkServer.ServerListener();
            }
            else
            {
                this.SetupRemoteGridServers();
                //Authenticate Session Handler
                AuthenticateSessionsRemote authen = new AuthenticateSessionsRemote();
                this.AuthenticateSessionsHandler = authen;
            }

            startuptime = DateTime.Now;

            try
            {
                AssetCache = new AssetCache(GridServers.AssetServer);
                InventoryCache = new InventoryCache();
            }
            catch (Exception e)
            {
                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, e.Message + "\nSorry, could not setup local cache");
                Environment.Exit(1);
            }

            m_udpServer = new UDPServer(this.regionData.IPListenPort, this.GridServers, this.AssetCache, this.InventoryCache, this.regionData, this.m_sandbox, this.user_accounts, this.m_console, this.AuthenticateSessionsHandler);

            //should be passing a IGenericConfig object to these so they can read the config data they want from it
            GridServers.AssetServer.SetServerInfo(regionData.AssetURL, regionData.AssetSendKey);
            IGridServer gridServer = GridServers.GridServer;
            gridServer.SetServerInfo(regionData.GridURL, regionData.GridSendKey, regionData.GridRecvKey);

            if (!m_sandbox)
            {
                this.ConnectToRemoteGridServer();
            }

            this.SetupLocalWorld();

            if (m_sandbox)
            {
                AssetCache.LoadDefaultTextureSet();
            }

            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Startup() - Initialising HTTP server");

            this.SetupHttpListener();

            //Login server setup
            LoginServer loginServer = null;
            LoginServer adminLoginServer = null;

            bool sandBoxWithLoginServer = m_loginserver && m_sandbox;
            if (sandBoxWithLoginServer)
            {
                loginServer = new LoginServer(regionData.IPListenAddr, regionData.IPListenPort, regionData.RegionLocX, regionData.RegionLocY, this.user_accounts);
                loginServer.Startup();
                loginServer.SetSessionHandler(((AuthenticateSessionsLocal)this.AuthenticateSessionsHandler).AddNewSession);

                if (user_accounts)
                {
                    //sandbox mode with loginserver using accounts
                    this.GridServers.UserServer = loginServer;
                    adminLoginServer = loginServer;

                    httpServer.AddXmlRPCHandler("login_to_simulator", loginServer.LocalUserManager.XmlRpcLoginMethod);
                }
                else
                {
                    //sandbox mode with loginserver not using accounts
                    httpServer.AddXmlRPCHandler("login_to_simulator", loginServer.XmlRpcLoginMethod);
                }
            }

            //Web front end setup
            AdminWebFront adminWebFront = new AdminWebFront("Admin", LocalWorld, InventoryCache, adminLoginServer);
            adminWebFront.LoadMethods(httpServer);

            //Start http server
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Startup() - Starting HTTP server");
            httpServer.Start();

            // Start UDP server
            this.m_udpServer.ServerListener();

            //Setup Master Avatar
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Startup() - Setting up Master Avatar");
            if (this.m_sandbox)
            {
                OpenSim.Framework.User.UserProfile masterUser = adminLoginServer.LocalUserManager.GetProfileByName(this.regionData.MasterAvatarFirstName, this.regionData.MasterAvatarLastName);
                if(masterUser == null)
                {
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Startup() - Sandbox Mode; Master Avatar is a new user; creating account.");
                    adminLoginServer.CreateUserAccount(this.regionData.MasterAvatarFirstName, this.regionData.MasterAvatarLastName, this.regionData.MasterAvatarSandboxPassword);
                    masterUser = adminLoginServer.LocalUserManager.GetProfileByName(this.regionData.MasterAvatarFirstName, this.regionData.MasterAvatarLastName);
                    if(masterUser == null) //Still NULL?!!?! OMG FAIL!
                    {
                        throw new Exception("Failure to create master user account");
                    }
                }
                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Startup() - Master User UUID: " + masterUser.UUID.ToStringHyphenated());
                regionData.MasterAvatarAssignedUUID = masterUser.UUID;

            }
            else
            {
                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Startup() - Grid Mode; Do not know how to get the user's master key yet!");
            }

            Console.WriteLine("Creating ParcelManager");
            LocalWorld.parcelManager = new OpenSim.RegionServer.world.ParcelManager(this.LocalWorld);

            Console.WriteLine("Loading Parcels from DB...");
            LocalWorld.localStorage.LoadParcels((ILocalStorageParcelReceiver)LocalWorld.parcelManager);

            m_heartbeatTimer.Enabled = true;
            m_heartbeatTimer.Interval = 100;
            m_heartbeatTimer.Elapsed += new ElapsedEventHandler(this.Heartbeat);
        }

        # region Setup methods
        protected override void SetupLocalGridServers()
        {
            GridServers.AssetDll = "OpenSim.GridInterfaces.Local.dll";
            GridServers.GridDll = "OpenSim.GridInterfaces.Local.dll";

            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Starting in Sandbox mode");

            try
            {
                GridServers.Initialise();
            }
            catch (Exception e)
            {
                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, e.Message + "\nSorry, could not setup the grid interface");
                Environment.Exit(1);
            }
        }

        protected override void SetupRemoteGridServers()
        {
            if (this.gridLocalAsset)
            {
                GridServers.AssetDll = "OpenSim.GridInterfaces.Local.dll";
            }
            else
            {
                GridServers.AssetDll = "OpenSim.GridInterfaces.Remote.dll";
            }
            GridServers.GridDll = "OpenSim.GridInterfaces.Remote.dll";

            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Starting in Grid mode");

            try
            {
                GridServers.Initialise();
            }
            catch (Exception e)
            {
                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, e.Message + "\nSorry, could not setup the grid interface");
                Environment.Exit(1);
            }
        }

        protected override void SetupLocalWorld()
        {
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.NORMAL, "Main.cs:Startup() - We are " + regionData.RegionName + " at " + regionData.RegionLocX.ToString() + "," + regionData.RegionLocY.ToString());
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Initialising world");
            m_console.componentname = "Region " + regionData.RegionName;

            m_localWorld = new World(this.m_udpServer.PacketServer.ClientThreads, regionData, regionData.RegionHandle, regionData.RegionName);
            LocalWorld.InventoryCache = InventoryCache;
            LocalWorld.AssetCache = AssetCache;

            this.m_udpServer.LocalWorld = LocalWorld;
            this.m_udpServer.PacketServer.RegisterClientPacketHandlers();

            this.physManager = new OpenSim.Physics.Manager.PhysicsManager();
            this.physManager.LoadPlugins();

            LocalWorld.m_datastore = this.regionData.DataStore;

            LocalWorld.LoadStorageDLL("OpenSim.Storage.LocalStorageDb4o.dll"); //all these dll names shouldn't be hard coded.
            LocalWorld.LoadWorldMap();

            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Startup() - Starting up messaging system");
            LocalWorld.PhysScene = this.physManager.GetPhysicsScene(this.m_physicsEngine);
            LocalWorld.PhysScene.SetTerrain(LocalWorld.Terrain.getHeights1D());
            LocalWorld.LoadPrimsFromStorage();
        }

        protected override void SetupHttpListener()
        {
            httpServer = new BaseHttpServer(regionData.IPListenPort);

            if (this.GridServers.GridServer.GetName() == "Remote")
            {

                // we are in Grid mode so set a XmlRpc handler to handle "expect_user" calls from the user server
                httpServer.AddXmlRPCHandler("expect_user", ((AuthenticateSessionsRemote)this.AuthenticateSessionsHandler).ExpectUser);

                httpServer.AddXmlRPCHandler("agent_crossing",
                    delegate(XmlRpcRequest request)
                    {
                        Hashtable requestData = (Hashtable)request.Params[0];
                        uint circuitcode = Convert.ToUInt32(requestData["circuit_code"]);
                            
                        AgentCircuitData agent_data = new AgentCircuitData();
                        agent_data.firstname = (string)requestData["firstname"];
                        agent_data.lastname = (string)requestData["lastname"];
                        agent_data.circuitcode = circuitcode;
                        agent_data.startpos = new LLVector3(Single.Parse((string)requestData["pos_x"]), Single.Parse((string)requestData["pos_y"]), Single.Parse((string)requestData["pos_z"]));

                        AuthenticateSessionsHandler.UpdateAgentData(agent_data);

                        return new XmlRpcResponse();
                    });

                httpServer.AddRestHandler("GET", "/simstatus/",
                    delegate(string request, string path, string param)
                    {
                        return "OK";
                    });
            }
        }

        protected override void ConnectToRemoteGridServer()
        {
            if (GridServers.GridServer.RequestConnection(regionData.SimUUID, regionData.IPListenAddr, (uint)regionData.IPListenPort))
            {
                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Startup() - Success: Got a grid connection OK!");
            }
            else
            {
                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.CRITICAL, "Main.cs:Startup() - FAILED: Unable to get connection to grid. Shutting down.");
                Shutdown();
            }

            GridServers.AssetServer.SetServerInfo((string)((RemoteGridBase)GridServers.GridServer).GridData["asset_url"], (string)((RemoteGridBase)GridServers.GridServer).GridData["asset_sendkey"]);

            // If we are being told to load a file, load it.
            string dataUri = (string)((RemoteGridBase)GridServers.GridServer).GridData["data_uri"];

            if (!String.IsNullOrEmpty(dataUri))
            {
                this.LocalWorld.m_datastore = dataUri;
            }

            if (((RemoteGridBase)(GridServers.GridServer)).GridData["regionname"].ToString() != "")
            {
                // The grid server has told us who we are
                // We must obey the grid server.
                try
                {
                    regionData.RegionLocX = Convert.ToUInt32(((RemoteGridBase)(GridServers.GridServer)).GridData["region_locx"].ToString());
                    regionData.RegionLocY = Convert.ToUInt32(((RemoteGridBase)(GridServers.GridServer)).GridData["region_locy"].ToString());
                    regionData.RegionName = ((RemoteGridBase)(GridServers.GridServer)).GridData["regionname"].ToString();
                }
                catch (Exception e)
                {
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.CRITICAL, e.Message + "\nBAD ERROR! THIS SHOULD NOT HAPPEN! Bad GridData from the grid interface!!!! ZOMG!!!");
                    Environment.Exit(1);
                }
            }
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
                        OpenSim.world.Avatar.PhysicsEngineFlying = false;
                        break;

                    case "basicphysics":
                        this.m_physicsEngine = "basicphysics";
                        configData.SetAttribute("PhysicsEngine", "basicphysics");
                        OpenSim.world.Avatar.PhysicsEngineFlying = false;
                        break;

                    case "RealPhysX":
                        this.m_physicsEngine = "RealPhysX";
                        OpenSim.world.Avatar.PhysicsEngineFlying = true;
                        break;

                    case "OpenDynamicsEngine":
                        this.m_physicsEngine = "OpenDynamicsEngine";
                        OpenSim.world.Avatar.PhysicsEngineFlying = true;
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
            LocalWorld.Close();
            GridServers.Close();
            m_console.Close();
            Environment.Exit(0);
        }

        /// <summary>
        /// Performs per-frame updates regularly
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Heartbeat(object sender, System.EventArgs e)
        {
            LocalWorld.Update();
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
                    Show(cmdparams[0]);
                    break;

                case "terrain":
                    string result = "";
                    if (!LocalWorld.Terrain.RunTerrainCmd(cmdparams, ref result))
                    {
                        m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, result);
                    }
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
                    OpenSim.world.Avatar TempAv;
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16}{5,-16}", "Firstname", "Lastname", "Agent ID", "Session ID", "Circuit", "IP"));
                    foreach (libsecondlife.LLUUID UUID in LocalWorld.Entities.Keys)
                    {
                        if (LocalWorld.Entities[UUID].ToString() == "OpenSim.world.Avatar")
                        {
                            TempAv = (OpenSim.world.Avatar)LocalWorld.Entities[UUID];
                            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16},{5,-16}", TempAv.firstname, TempAv.lastname, UUID, TempAv.ControllingClient.SessionID, TempAv.ControllingClient.CircuitCode, TempAv.ControllingClient.userEP.ToString()));
                        }
                    }
                    break;
            }
        }
        #endregion
    }


}