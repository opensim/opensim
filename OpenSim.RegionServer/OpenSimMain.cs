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
using OpenSim.Framework.Interfaces;
using OpenSim.UserServer;
using OpenSim.Assets;
using OpenSim.CAPS;
using OpenSim.Framework.Console;
using OpenSim.Physics.Manager;

namespace OpenSim
{
    public class OpenSimMain : OpenSimNetworkHandler, conscmd_callback
    {
        private PhysicsManager physManager;
        private World LocalWorld;
        private Grid GridServers;
        private SimConfig Cfg;
        private SimCAPSHTTPServer HttpServer;
        private AssetCache AssetCache;
        private InventoryCache InventoryCache;
        //public Dictionary<EndPoint, SimClient> ClientThreads = new Dictionary<EndPoint, SimClient>();
        private Dictionary<uint, SimClient> ClientThreads = new Dictionary<uint, SimClient>();
        private Dictionary<EndPoint, uint> clientCircuits = new Dictionary<EndPoint, uint>();
        private DateTime startuptime;

        public Socket Server;
        private IPEndPoint ServerIncoming;
        private byte[] RecvBuffer = new byte[4096];
        private byte[] ZeroBuffer = new byte[8192];
        private IPEndPoint ipeSender;
        private EndPoint epSender;
        private AsyncCallback ReceivedData;

        private System.Timers.Timer timer1 = new System.Timers.Timer();
        private string ConfigDll = "OpenSim.Config.SimConfigDb4o.dll";
        public string m_physicsEngine;
        public bool m_sandbox = false;
        public bool m_loginserver;
        public bool user_accounts = false;

        protected ConsoleBase m_console;
        
        public OpenSimMain( bool sandBoxMode, bool startLoginServer, string physicsEngine )
        {
            m_sandbox = sandBoxMode;
            m_loginserver = startLoginServer;
            m_physicsEngine = physicsEngine;
            
            m_console = new ConsoleBase("region-console.log", "Region", this);
            OpenSim.Framework.Console.MainConsole.Instance = m_console;
        }

        public virtual void StartUp()
        {
            GridServers = new Grid();
            if ( m_sandbox )
            {
                GridServers.AssetDll = "OpenSim.GridInterfaces.Local.dll";
                GridServers.GridDll = "OpenSim.GridInterfaces.Local.dll";
                
                m_console.WriteLine("Starting in Sandbox mode");
            }
            else
            {
                GridServers.AssetDll = "OpenSim.GridInterfaces.Remote.dll";
                GridServers.GridDll = "OpenSim.GridInterfaces.Remote.dll";

                m_console.WriteLine("Starting in Grid mode");
            }

            GridServers.Initialise();
            
            startuptime = DateTime.Now;

            AssetCache = new AssetCache(GridServers.AssetServer);
            InventoryCache = new InventoryCache();

            // We check our local database first, then the grid for config options
            m_console.WriteLine("Main.cs:Startup() - Loading configuration");
            Cfg = this.LoadConfigDll(this.ConfigDll);
            Cfg.InitConfig(this.m_sandbox);
            m_console.WriteLine("Main.cs:Startup() - Contacting gridserver");
            Cfg.LoadFromGrid();

            m_console.WriteLine("Main.cs:Startup() - We are " + Cfg.RegionName + " at " + Cfg.RegionLocX.ToString() + "," + Cfg.RegionLocY.ToString());
            m_console.WriteLine("Initialising world");
            LocalWorld = new World(ClientThreads, Cfg.RegionHandle, Cfg.RegionName, Cfg);
            LocalWorld.LandMap = Cfg.LoadWorld();

            this.physManager = new OpenSim.Physics.Manager.PhysicsManager();
            this.physManager.LoadPlugins();

            m_console.WriteLine("Main.cs:Startup() - Starting up messaging system");
            LocalWorld.PhysScene = this.physManager.GetPhysicsScene(this.m_physicsEngine); //should be reading from the config file what physics engine to use
            LocalWorld.PhysScene.SetTerrain(LocalWorld.LandMap);

            GridServers.AssetServer.SetServerInfo(Cfg.AssetURL, Cfg.AssetSendKey);
            GridServers.GridServer.SetServerInfo(Cfg.GridURL, Cfg.GridSendKey, Cfg.GridRecvKey);

            LocalWorld.LoadStorageDLL("OpenSim.Storage.LocalStorageDb4o.dll"); //all these dll names shouldn't be hard coded.
            LocalWorld.LoadPrimsFromStorage();

            if ( m_sandbox)
            {
                AssetCache.LoadDefaultTextureSet();
            }

            m_console.WriteLine("Main.cs:Startup() - Starting CAPS HTTP server");
            HttpServer = new SimCAPSHTTPServer(GridServers.GridServer, Cfg.IPListenPort);

            LoginServer loginServer = null;
            if (m_loginserver && m_sandbox)
            {
                loginServer = new LoginServer(GridServers.GridServer, Cfg.IPListenAddr, Cfg.IPListenPort, this.user_accounts);
                loginServer.Startup();
                
            }
            if((m_loginserver) && (m_sandbox) && (user_accounts))
            {
                this.GridServers.UserServer = loginServer;
                HttpServer.AddRestHandler("Admin", new AdminWebFront("Admin", LocalWorld, loginServer));
            }
            else 
            {
                HttpServer.AddRestHandler("Admin", new AdminWebFront("Admin", LocalWorld, null));
            }

            MainServerListener();

            timer1.Enabled = true;
            timer1.Interval = 100;
            timer1.Elapsed += new ElapsedEventHandler(this.Timer1Tick);
        }

        private SimConfig LoadConfigDll(string dllName)
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
            if(this.clientCircuits.ContainsKey(epSender))
            {
                ClientThreads[this.clientCircuits[epSender]].InPacket(packet);
            }
            else if (packet.Type == PacketType.UseCircuitCode)
            { // new client
                UseCircuitCodePacket useCircuit = (UseCircuitCodePacket)packet;
                this.clientCircuits.Add(epSender, useCircuit.CircuitCode.Code);
                SimClient newuser = new SimClient(epSender, useCircuit, LocalWorld, ClientThreads, AssetCache, GridServers.GridServer, this, InventoryCache, m_sandbox);
                if ((this.GridServers.UserServer != null) && (user_accounts))
                {
                    Console.WriteLine("setting userserver");
                    newuser.UserServer = this.GridServers.UserServer;
                }
                //OpenSimRoot.Instance.ClientThreads.Add(epSender, newuser);
                ClientThreads.Add(useCircuit.CircuitCode.Code, newuser);
            }
            else
            { // invalid client
                Console.Error.WriteLine("Main.cs:OnReceivedData() - WARNING: Got a packet from an invalid client - " + epSender.ToString());
            }
            Server.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender, ReceivedData, null);
        }

        private void MainServerListener()
        {
            m_console.WriteLine("Main.cs:MainServerListener() - New thread started");
            m_console.WriteLine("Main.cs:MainServerListener() - Opening UDP socket on " + Cfg.IPListenAddr + ":" + Cfg.IPListenPort);

            ServerIncoming = new IPEndPoint(IPAddress.Any, Cfg.IPListenPort);
            Server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Server.Bind(ServerIncoming);

            m_console.WriteLine("Main.cs:MainServerListener() - UDP socket bound, getting ready to listen");

            ipeSender = new IPEndPoint(IPAddress.Any, 0);
            epSender = (EndPoint)ipeSender;
            ReceivedData = new AsyncCallback(this.OnReceivedData);
            Server.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender, ReceivedData, null);

            m_console.WriteLine("Main.cs:MainServerListener() - Listening...");

        }

        public virtual void SendPacketTo(byte[] buffer, int size, SocketFlags flags, uint circuitcode )//EndPoint packetSender)
        {
            // find the endpoint for this circuit
            EndPoint sendto = null;
            foreach(KeyValuePair<EndPoint, uint> p in this.clientCircuits)
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

        public virtual void Shutdown()
        {
            m_console.WriteLine("Main.cs:Shutdown() - Closing all threads");
            m_console.WriteLine("Main.cs:Shutdown() - Killing listener thread");
            m_console.WriteLine("Main.cs:Shutdown() - Killing clients");
            // IMPLEMENT THIS
            m_console.WriteLine("Main.cs:Shutdown() - Closing console and terminating");
            LocalWorld.Close();
            GridServers.Close();
            m_console.Close();
            Environment.Exit(0);
        }

        void Timer1Tick(object sender, System.EventArgs e)
        {
            LocalWorld.Update();
        }
        
        public void RunCmd(string command, string[] cmdparams)
        {
            switch (command)
            {
                case "help":
                    m_console.WriteLine("show users - show info about connected users");
                    m_console.WriteLine("shutdown - disconnect all clients and shutdown");
                    m_console.WriteLine("regenerate - regenerate the sim's terrain");
                    break;

                case "show":
                    Show(cmdparams[0]);
                    break;

                case "regenerate":
                    LocalWorld.RegenerateTerrain();
                    break;

                case "shutdown":
                    Shutdown();
                    break;
            }
        }

        public void Show(string ShowWhat)
        {
            switch (ShowWhat)
            {
                case "uptime":
                    m_console.WriteLine("OpenSim has been running since " + startuptime.ToString());
                    m_console.WriteLine("That is " + (DateTime.Now - startuptime).ToString());
                    break;
                case "users":
                    OpenSim.world.Avatar TempAv;
                    m_console.WriteLine(String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16}{5,-16}", "Firstname", "Lastname", "Agent ID", "Session ID", "Circuit", "IP"));
                    foreach (libsecondlife.LLUUID UUID in LocalWorld.Entities.Keys)
                    {
                        if (LocalWorld.Entities[UUID].ToString() == "OpenSim.world.Avatar")
                        {
                            TempAv = (OpenSim.world.Avatar)LocalWorld.Entities[UUID];
                            m_console.WriteLine(String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16},{5,-16}", TempAv.firstname, TempAv.lastname, UUID, TempAv.ControllingClient.SessionID, TempAv.ControllingClient.CircuitCode, TempAv.ControllingClient.userEP.ToString()));
                        }
                    }
                    break;
            }
        }
    }

    
}
