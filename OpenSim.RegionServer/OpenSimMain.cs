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
    public class OpenSimMain : OpenSimApplication
    {
        private Dictionary<EndPoint, uint> clientCircuits = new Dictionary<EndPoint, uint>();
        private PhysicsManager physManager;

        public Socket Server;
        private IPEndPoint ServerIncoming;
        private byte[] RecvBuffer = new byte[4096];
        private byte[] ZeroBuffer = new byte[8192];
        private IPEndPoint ipeSender;
        private EndPoint epSender;
        private AsyncCallback ReceivedData;

        private System.Timers.Timer timer1 = new System.Timers.Timer();
        private string ConfigDll = "OpenSim.Config.SimConfigDb4o.dll";
        public string _physicsEngine = "basicphysics";
        public bool sandbox = false;
        public bool loginserver = false;

        public OpenSimMain()
        {
        }

        public override void StartUp()
        {
            OpenSimRoot.Instance.startuptime = DateTime.Now;

            OpenSimRoot.Instance.AssetCache = new AssetCache(OpenSimRoot.Instance.GridServers.AssetServer);
            OpenSimRoot.Instance.InventoryCache = new InventoryCache();

            // We check our local database first, then the grid for config options
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Main.cs:Startup() - Loading configuration");
            OpenSimRoot.Instance.Cfg = this.LoadConfigDll(this.ConfigDll);
            OpenSimRoot.Instance.Cfg.InitConfig(this.sandbox);
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Main.cs:Startup() - Contacting gridserver");
            OpenSimRoot.Instance.Cfg.LoadFromGrid();

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Main.cs:Startup() - We are " + OpenSimRoot.Instance.Cfg.RegionName + " at " + OpenSimRoot.Instance.Cfg.RegionLocX.ToString() + "," + OpenSimRoot.Instance.Cfg.RegionLocY.ToString());
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Initialising world");
            OpenSimRoot.Instance.LocalWorld = new World();
            OpenSimRoot.Instance.LocalWorld.LandMap = OpenSimRoot.Instance.Cfg.LoadWorld();

            this.physManager = new OpenSim.Physics.Manager.PhysicsManager();
            this.physManager.LoadPlugins();

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Main.cs:Startup() - Starting up messaging system");
            OpenSimRoot.Instance.LocalWorld.PhysScene = this.physManager.GetPhysicsScene(this._physicsEngine); //should be reading from the config file what physics engine to use
            OpenSimRoot.Instance.LocalWorld.PhysScene.SetTerrain(OpenSimRoot.Instance.LocalWorld.LandMap);

            OpenSimRoot.Instance.GridServers.AssetServer.SetServerInfo(OpenSimRoot.Instance.Cfg.AssetURL, OpenSimRoot.Instance.Cfg.AssetSendKey);
            OpenSimRoot.Instance.GridServers.GridServer.SetServerInfo(OpenSimRoot.Instance.Cfg.GridURL, OpenSimRoot.Instance.Cfg.GridSendKey, OpenSimRoot.Instance.Cfg.GridRecvKey);

            OpenSimRoot.Instance.LocalWorld.LoadStorageDLL("OpenSim.Storage.LocalStorageDb4o.dll"); //all these dll names shouldn't be hard coded.
            OpenSimRoot.Instance.LocalWorld.LoadPrimsFromStorage();

            if (this.sandbox)
            {
                OpenSimRoot.Instance.AssetCache.LoadDefaultTextureSet();
            }

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Main.cs:Startup() - Starting CAPS HTTP server");
            OpenSimRoot.Instance.HttpServer = new SimCAPSHTTPServer();

            timer1.Enabled = true;
            timer1.Interval = 100;
            timer1.Elapsed += new ElapsedEventHandler(this.Timer1Tick);

            MainServerListener();
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
                OpenSimRoot.Instance.ClientThreads[this.clientCircuits[epSender]].InPacket(packet);
            }
            else if (packet.Type == PacketType.UseCircuitCode)
            { // new client
                UseCircuitCodePacket useCircuit = (UseCircuitCodePacket)packet;
                this.clientCircuits.Add(epSender, useCircuit.CircuitCode.Code);
                SimClient newuser = new SimClient(epSender, useCircuit);
                //OpenSimRoot.Instance.ClientThreads.Add(epSender, newuser);
                OpenSimRoot.Instance.ClientThreads.Add(useCircuit.CircuitCode.Code, newuser);
            }
            else
            { // invalid client
                Console.Error.WriteLine("Main.cs:OnReceivedData() - WARNING: Got a packet from an invalid client - " + epSender.ToString());
            }
            Server.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender, ReceivedData, null);
        }

        private void MainServerListener()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Main.cs:MainServerListener() - New thread started");
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Main.cs:MainServerListener() - Opening UDP socket on " + OpenSimRoot.Instance.Cfg.IPListenAddr + ":" + OpenSimRoot.Instance.Cfg.IPListenPort);

            ServerIncoming = new IPEndPoint(IPAddress.Any, OpenSimRoot.Instance.Cfg.IPListenPort);
            Server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Server.Bind(ServerIncoming);

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Main.cs:MainServerListener() - UDP socket bound, getting ready to listen");

            ipeSender = new IPEndPoint(IPAddress.Any, 0);
            epSender = (EndPoint)ipeSender;
            ReceivedData = new AsyncCallback(this.OnReceivedData);
            Server.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender, ReceivedData, null);

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Main.cs:MainServerListener() - Listening...");

        }

        public override void SendPacketTo(byte[] buffer, int size, SocketFlags flags, uint circuitcode )//EndPoint packetSender)
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

        public override void RemoveClientCircuit(uint circuitcode)
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

        public override void Shutdown()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Main.cs:Shutdown() - Closing all threads");
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Main.cs:Shutdown() - Killing listener thread");
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Main.cs:Shutdown() - Killing clients");
            // IMPLEMENT THIS
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Main.cs:Shutdown() - Closing console and terminating");
            OpenSimRoot.Instance.LocalWorld.Close();
            OpenSimRoot.Instance.GridServers.Close();
            OpenSim.Framework.Console.MainConsole.Instance.Close();
            Environment.Exit(0);
        }

        void Timer1Tick(object sender, System.EventArgs e)
        {
            OpenSimRoot.Instance.LocalWorld.Update();
        }
    }

    
}
