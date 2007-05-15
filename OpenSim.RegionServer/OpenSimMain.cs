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

    public class OpenSimMain : OpenSimNetworkHandler, conscmd_callback
    {
        //private SimConfig Cfg;
        private IGenericConfig localConfig;
        //private IGenericConfig remoteConfig;
        private PhysicsManager physManager;
        private Grid GridServers;
        private PacketServer _packetServer;
        private World LocalWorld;
        private AssetCache AssetCache;
        private InventoryCache InventoryCache;
        //private Dictionary<uint, SimClient> ClientThreads = new Dictionary<uint, SimClient>();
        private Dictionary<EndPoint, uint> clientCircuits = new Dictionary<EndPoint, uint>();
        private DateTime startuptime;
        private RegionInfo regionData;

        public Socket Server;
        private IPEndPoint ServerIncoming;
        private byte[] RecvBuffer = new byte[4096];
        private byte[] ZeroBuffer = new byte[8192];
        private IPEndPoint ipeSender;
        private EndPoint epSender;
        private AsyncCallback ReceivedData;

        private System.Timers.Timer m_heartbeatTimer = new System.Timers.Timer();
        //private string ConfigDll = "OpenSim.Config.SimConfigDb4o.dll";
        public string m_physicsEngine;
        public bool m_sandbox = false;
        public bool m_loginserver;
        public OpenGridProtocolServer OGSServer;
        public bool user_accounts = false;
        public bool gridLocalAsset = false;
        private bool configFileSetup = false;
        public string m_config;

        protected ConsoleBase m_console;

        public OpenSimMain(bool sandBoxMode, bool startLoginServer, string physicsEngine, bool useConfigFile, bool verbose, string configFile)
        {
            this.configFileSetup = useConfigFile;
            m_sandbox = sandBoxMode;
            m_loginserver = startLoginServer;
            m_physicsEngine = physicsEngine;
            m_config = configFile;

            m_console = new ConsoleBase("region-console-" + Guid.NewGuid().ToString() + ".log", "Region", this, verbose);
            OpenSim.Framework.Console.MainConsole.Instance = m_console;
        }

        /// <summary>
        /// Performs initialisation of the world, such as loading configuration from disk.
        /// </summary>
        public virtual void StartUp()
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
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Main.cs:Startup() - Loading configuration");
            this.regionData.InitConfig(this.m_sandbox, this.localConfig);
            this.localConfig.Close();//for now we can close it as no other classes read from it , but this should change


            GridServers = new Grid();
            if (m_sandbox)
            {
                GridServers.AssetDll =  "OpenSim.GridInterfaces.Local.dll";
                GridServers.GridDll = "OpenSim.GridInterfaces.Local.dll";

                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Starting in Sandbox mode");
            }
            else
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

                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Starting in Grid mode");
            }

            try
            {
                GridServers.Initialise();
            }
            catch (Exception e)
            {
                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH,e.Message + "\nSorry, could not setup the grid interface");
                Environment.Exit(1);
            }

            startuptime = DateTime.Now;

            try
            {
                AssetCache = new AssetCache(GridServers.AssetServer);
                InventoryCache = new InventoryCache();
            }
            catch (Exception e)
            {
                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH,e.Message + "\nSorry, could not setup local cache");
                Environment.Exit(1);
            }

            PacketServer packetServer = new PacketServer(this);


            //should be passing a IGenericConfig object to these so they can read the config data they want from it
           GridServers.AssetServer.SetServerInfo(regionData.AssetURL, regionData.AssetSendKey);
            IGridServer gridServer = GridServers.GridServer;
            gridServer.SetServerInfo(regionData.GridURL, regionData.GridSendKey, regionData.GridRecvKey);

            if (!m_sandbox)
            {
                if (GridServers.GridServer.RequestConnection(regionData.SimUUID, regionData.IPListenAddr, (uint)regionData.IPListenPort))
                {
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Main.cs:Startup() - Success: Got a grid connection OK!");
                }
                else
                {
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.CRITICAL,"Main.cs:Startup() - FAILED: Unable to get connection to grid. Shutting down.");
                    Shutdown();
                }

                GridServers.AssetServer.SetServerInfo((string)((RemoteGridBase)GridServers.GridServer).GridData["asset_url"], (string)((RemoteGridBase)GridServers.GridServer).GridData["asset_sendkey"]);

                // If we are being told to load a file, load it.
                string dataUri = (string)((RemoteGridBase)GridServers.GridServer).GridData["data_uri"];
                
                if ( !String.IsNullOrEmpty( dataUri ) )
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
                        m_console.WriteLine(OpenSim.Framework.Console.LogPriority.CRITICAL,e.Message + "\nBAD ERROR! THIS SHOULD NOT HAPPEN! Bad GridData from the grid interface!!!! ZOMG!!!");
                        Environment.Exit(1);
                    }
                }

            }


            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.NORMAL,"Main.cs:Startup() - We are " + regionData.RegionName + " at " + regionData.RegionLocX.ToString() + "," + regionData.RegionLocY.ToString());
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Initialising world");
            m_console.componentname = "Region " + regionData.RegionName;
            LocalWorld = new World(this._packetServer.ClientThreads, regionData, regionData.RegionHandle, regionData.RegionName);
            LocalWorld.InventoryCache = InventoryCache;
            LocalWorld.AssetCache = AssetCache;

            this._packetServer.LocalWorld = LocalWorld;
            this._packetServer.RegisterClientPacketHandlers();

            this.physManager = new OpenSim.Physics.Manager.PhysicsManager();
            this.physManager.LoadPlugins();

            LocalWorld.m_datastore = this.regionData.DataStore;

            LocalWorld.LoadStorageDLL("OpenSim.Storage.LocalStorageDb4o.dll"); //all these dll names shouldn't be hard coded.
            LocalWorld.LoadWorldMap();

            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Main.cs:Startup() - Starting up messaging system");
            LocalWorld.PhysScene = this.physManager.GetPhysicsScene(this.m_physicsEngine); //should be reading from the config file what physics engine to use
            LocalWorld.PhysScene.SetTerrain(LocalWorld.Terrain.getHeights1D());


            LocalWorld.LoadPrimsFromStorage();

            if (m_sandbox)
            {
                AssetCache.LoadDefaultTextureSet();
            }

            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Main.cs:Startup() - Initialising HTTP server");
            // HttpServer = new SimCAPSHTTPServer(GridServers.GridServer, Cfg.IPListenPort);

            BaseHttpServer httpServer = new BaseHttpServer(regionData.IPListenPort);

            if (gridServer.GetName() == "Remote")
            {
                // should startup the OGS protocol server here
                // Are we actually using this?
                OGSServer = new OpenGridProtocolServer(this.regionData.IPListenPort - 500); // Changed so we can have more than one OGSServer per machine.

                // we are in Grid mode so set a XmlRpc handler to handle "expect_user" calls from the user server
                httpServer.AddXmlRPCHandler("expect_user",
                    delegate(XmlRpcRequest request)
                    {
                        Hashtable requestData = (Hashtable)request.Params[0];
                        AgentCircuitData agent_data = new AgentCircuitData();
                        agent_data.SessionID = new LLUUID((string)requestData["session_id"]);
                        agent_data.SecureSessionID = new LLUUID((string)requestData["secure_session_id"]);
                        agent_data.firstname = (string)requestData["firstname"];
                        agent_data.lastname = (string)requestData["lastname"];
                        agent_data.AgentID = new LLUUID((string)requestData["agent_id"]);
                        agent_data.circuitcode = Convert.ToUInt32(requestData["circuit_code"]);
                        if (requestData.ContainsKey("child_agent") && requestData["child_agent"].Equals("1"))
                        {
                            agent_data.child = true;
                        }
                        else
                        {
                            agent_data.startpos = new LLVector3(Convert.ToUInt32(requestData["startpos_x"]), Convert.ToUInt32(requestData["startpos_y"]), Convert.ToUInt32(requestData["startpos_z"]));
                            agent_data.child = false;
                        }

                        if (((RemoteGridBase)gridServer).agentcircuits.ContainsKey((uint)agent_data.circuitcode))
                        {
                            ((RemoteGridBase)gridServer).agentcircuits[(uint)agent_data.circuitcode] = agent_data;
                        }
                        else
                        {
                            ((RemoteGridBase)gridServer).agentcircuits.Add((uint)agent_data.circuitcode, agent_data);
                        }

                        return new XmlRpcResponse();
                    });

                httpServer.AddXmlRPCHandler("agent_crossing",
                    delegate(XmlRpcRequest request)
                    {
                        Hashtable requestData = (Hashtable)request.Params[0];
                        AgentCircuitData agent_data = new AgentCircuitData();
                        agent_data.firstname = (string)requestData["firstname"];
                        agent_data.lastname = (string)requestData["lastname"];
                        agent_data.circuitcode = Convert.ToUInt32(requestData["circuit_code"]);
                        agent_data.startpos = new LLVector3(Single.Parse((string)requestData["pos_x"]), Single.Parse((string)requestData["pos_y"]), Single.Parse((string)requestData["pos_z"]));

                        if (((RemoteGridBase)gridServer).agentcircuits.ContainsKey((uint)agent_data.circuitcode))
                        {
                            ((RemoteGridBase)gridServer).agentcircuits[(uint)agent_data.circuitcode].firstname = agent_data.firstname;
                            ((RemoteGridBase)gridServer).agentcircuits[(uint)agent_data.circuitcode].lastname = agent_data.lastname;
                            ((RemoteGridBase)gridServer).agentcircuits[(uint)agent_data.circuitcode].startpos = agent_data.startpos;
                        }

                        return new XmlRpcResponse();
                    });

                httpServer.AddRestHandler("GET", "/simstatus/",
                    delegate(string request, string path, string param)
                    {
                        return "OK";
                    });
            }

            LoginServer loginServer = null;
            LoginServer adminLoginServer = null;

            bool sandBoxWithLoginServer = m_loginserver && m_sandbox;
            if (sandBoxWithLoginServer)
            {
                loginServer = new LoginServer(gridServer, regionData.IPListenAddr, regionData.IPListenPort,regionData.RegionLocX, regionData.RegionLocY, this.user_accounts);
                loginServer.Startup();

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

            AdminWebFront adminWebFront = new AdminWebFront("Admin", LocalWorld, InventoryCache, adminLoginServer);
            adminWebFront.LoadMethods(httpServer);

            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Main.cs:Startup() - Starting HTTP server");
            httpServer.Start();

            if (gridServer.GetName() == "Remote")
            {
                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Main.cs:Startup() - Starting up OGS protocol server");
                OGSServer.Start();
            }

            MainServerListener();

            m_heartbeatTimer.Enabled = true;
            m_heartbeatTimer.Interval = 100;
            m_heartbeatTimer.Elapsed += new ElapsedEventHandler(this.Heartbeat);
        }

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
                        m_console.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM,"Main.cs: SetupFromConfig() - Invalid value for PhysicsEngine attribute, terminating");
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

        private SimConfig LoadConfigDll(string dllName)
        {
            try
            {
                Assembly pluginAssembly = Assembly.LoadFrom(dllName);
                SimConfig config = null;

                foreach (Type pluginType in pluginAssembly.GetTypes())
                {
                    if (pluginType.IsPublic)
                    {
                        if (!pluginType.IsAbstract)
                        {
                            Type typeInterface = pluginType.GetInterface("ISimConfig", true);

                            if (typeInterface != null)
                            {
                                ISimConfig plug = (ISimConfig)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
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
            catch (Exception e)
            {
                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.CRITICAL,e.Message + "\nSorry, a fatal error occurred while trying to load the config DLL");
                m_console.WriteLine(OpenSim.Framework.Console.LogPriority.CRITICAL,"Can not continue starting up");
                Environment.Exit(1);
                return null;
            }
        }

        private void OnReceivedData(IAsyncResult result)
        {
            ipeSender = new IPEndPoint(IPAddress.Any, 0);
            epSender = (EndPoint)ipeSender;
            Packet packet = null;
            int numBytes = Server.EndReceiveFrom(result, ref epSender);
            int packetEnd = numBytes - 1;

            packet = Packet.BuildPacket(RecvBuffer, ref packetEnd, ZeroBuffer);

            // This is either a new client or a packet to send to an old one
            // if (OpenSimRoot.Instance.ClientThreads.ContainsKey(epSender))

            // do we already have a circuit for this endpoint
            if (this.clientCircuits.ContainsKey(epSender))
            {
                //ClientThreads[this.clientCircuits[epSender]].InPacket(packet);
                this._packetServer.ClientInPacket(this.clientCircuits[epSender], packet);
            }
            else if (packet.Type == PacketType.UseCircuitCode)
            { // new client

                UseCircuitCodePacket useCircuit = (UseCircuitCodePacket)packet;
                this.clientCircuits.Add(epSender, useCircuit.CircuitCode.Code);
                bool isChildAgent = false;
                if (this.GridServers.GridServer.GetName() == "Remote")
                {
                    isChildAgent = ((RemoteGridBase)this.GridServers.GridServer).agentcircuits[useCircuit.CircuitCode.Code].child;
                }
                SimClient newuser = new SimClient(epSender, useCircuit, LocalWorld, _packetServer.ClientThreads, AssetCache, GridServers.GridServer, this, InventoryCache, m_sandbox, isChildAgent, this.regionData);
                if ((this.GridServers.UserServer != null) && (user_accounts))
                {
                    newuser.UserServer = this.GridServers.UserServer;
                }
                //OpenSimRoot.Instance.ClientThreads.Add(epSender, newuser);
                this._packetServer.ClientThreads.Add(useCircuit.CircuitCode.Code, newuser);

                //if (!((RemoteGridBase)GridServers.GridServer).agentcircuits[useCircuit.CircuitCode.Code].child)
              

            }
            else
            { // invalid client
                Console.Error.WriteLine("Main.cs:OnReceivedData() - WARNING: Got a packet from an invalid client - " + epSender.ToString());
            }

            Server.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender, ReceivedData, null);
        }

        private void MainServerListener()
        {
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Main.cs:MainServerListener() - New thread started");
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Main.cs:MainServerListener() - Opening UDP socket on " + regionData.IPListenAddr + ":" + regionData.IPListenPort);

            ServerIncoming = new IPEndPoint(IPAddress.Any, regionData.IPListenPort);
            Server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Server.Bind(ServerIncoming);

            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Main.cs:MainServerListener() - UDP socket bound, getting ready to listen");

            ipeSender = new IPEndPoint(IPAddress.Any, 0);
            epSender = (EndPoint)ipeSender;
            ReceivedData = new AsyncCallback(this.OnReceivedData);
            Server.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender, ReceivedData, null);

            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Main.cs:MainServerListener() - Listening...");

        }

        public void RegisterPacketServer(PacketServer server)
        {
            this._packetServer = server;
        }

        public virtual void SendPacketTo(byte[] buffer, int size, SocketFlags flags, uint circuitcode)//EndPoint packetSender)
        {
            // find the endpoint for this circuit
            EndPoint sendto = null;
            foreach (KeyValuePair<EndPoint, uint> p in this.clientCircuits)
            {
                if (p.Value == circuitcode)
                {
                    sendto = p.Key;
                    break;
                }
            }
            if (sendto != null)
            {
                //we found the endpoint so send the packet to it
                this.Server.SendTo(buffer, size, flags, sendto);
            }
        }

        public virtual void RemoveClientCircuit(uint circuitcode)
        {
            foreach (KeyValuePair<EndPoint, uint> p in this.clientCircuits)
            {
                if (p.Value == circuitcode)
                {
                    this.clientCircuits.Remove(p.Key);
                    break;
                }
            }
        }

        /// <summary>
        /// Performs any last-minute sanity checking and shuts down the region server
        /// </summary>
        public virtual void Shutdown()
        {
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Main.cs:Shutdown() - Closing all threads");
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Main.cs:Shutdown() - Killing listener thread");
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Main.cs:Shutdown() - Killing clients");
            // IMPLEMENT THIS
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Main.cs:Shutdown() - Closing console and terminating");
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
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH,"show users - show info about connected users");
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH,"shutdown - disconnect all clients and shutdown");
                    break;

                case "show":
                    Show(cmdparams[0]);
                    break;

                case "terrain":
                    string result = "";
                    if (!LocalWorld.Terrain.RunTerrainCmd(cmdparams, ref result))
                    {
                        m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH,result);
                    }
                    break;

                case "shutdown":
                    Shutdown();
                    break;

                default:
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH,"Unknown command");
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
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH,"OpenSim has been running since " + startuptime.ToString());
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH,"That is " + (DateTime.Now - startuptime).ToString());
                    break;
                case "users":
                    OpenSim.world.Avatar TempAv;
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH,String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16}{5,-16}", "Firstname", "Lastname", "Agent ID", "Session ID", "Circuit", "IP"));
                    foreach (libsecondlife.LLUUID UUID in LocalWorld.Entities.Keys)
                    {
                        if (LocalWorld.Entities[UUID].ToString() == "OpenSim.world.Avatar")
                        {
                            TempAv = (OpenSim.world.Avatar)LocalWorld.Entities[UUID];
                            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH,String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16},{5,-16}", TempAv.firstname, TempAv.lastname, UUID, TempAv.ControllingClient.SessionID, TempAv.ControllingClient.CircuitCode, TempAv.ControllingClient.userEP.ToString()));
                        }
                    }
                    break;
            }
        }
    }


}
