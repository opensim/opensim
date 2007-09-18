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
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Timers;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Communications.Caches;

using Timer = System.Timers.Timer;

namespace OpenSim.Region.ClientStack
{
    public delegate bool PacketMethod(IClientAPI simClient, Packet packet);

    /// <summary>
    /// Handles new client connections
    /// Constructor takes a single Packet and authenticates everything
    /// </summary>
    public partial class ClientView : IClientAPI
    {
        public static TerrainManager TerrainManager;

        protected static Dictionary<PacketType, PacketMethod> PacketHandlers = new Dictionary<PacketType, PacketMethod>(); //Global/static handlers for all clients
        protected Dictionary<PacketType, PacketMethod> m_packetHandlers = new Dictionary<PacketType, PacketMethod>(); //local handlers for this instance 

        private LLUUID m_sessionId;
        public LLUUID SecureSessionID = LLUUID.Zero;
        public string firstName;
        public string lastName;
        public bool m_child = false;
        private UseCircuitCodePacket cirpack;
        public Thread ClientThread;
        public LLVector3 startpos;

        //private AgentAssetUpload UploadAssets;
        private LLUUID newAssetFolder = LLUUID.Zero;
        private int debug = 0;
        protected IScene m_scene;
        private ClientManager m_clientManager;
        private AssetCache m_assetCache;
        // private InventoryCache m_inventoryCache;
        private int cachedtextureserial = 0;
        protected AgentCircuitManager m_authenticateSessionsHandler;
        private Encoding enc = Encoding.ASCII;
        // Dead client detection vars
        private Timer clientPingTimer;
        private int packetsReceived = 0;
        private int probesWithNoIngressPackets = 0;
        private int lastPacketsReceived = 0;

        public ClientView(EndPoint remoteEP, UseCircuitCodePacket initialcirpack, ClientManager clientManager, IScene scene, AssetCache assetCache, PacketServer packServer, AgentCircuitManager authenSessions)
        {
            m_moneyBalance = 1000;

            m_scene = scene;
            m_clientManager = clientManager;
            m_assetCache = assetCache;

            m_networkServer = packServer;
            // m_inventoryCache = inventoryCache;
            m_authenticateSessionsHandler = authenSessions;

            MainLog.Instance.Verbose("OpenSimClient.cs - Started up new client thread to handle incoming request");
            cirpack = initialcirpack;
            userEP = remoteEP;

            this.startpos = m_authenticateSessionsHandler.GetPosition(initialcirpack.CircuitCode.Code);

            PacketQueue = new BlockingQueue<QueItem>();

            //this.UploadAssets = new AgentAssetUpload(this, m_assetCache, m_inventoryCache);
            AckTimer = new Timer(500);
            AckTimer.Elapsed += new ElapsedEventHandler(AckTimer_Elapsed);
            AckTimer.Start();

            this.RegisterLocalPacketHandlers();

            ClientThread = new Thread(new ThreadStart(AuthUser));
            ClientThread.IsBackground = true;
            ClientThread.Start();
        }

        public LLUUID SessionId
        {
            get { return m_sessionId; }
        }

        public void SetDebug(int newDebug) 
        {
            debug = newDebug;
        }

        # region Client Methods

        public void Close()
        {
            clientPingTimer.Stop();

            m_scene.RemoveClient(this.AgentId);

            m_clientManager.Remove(this.CircuitCode); // TODO: Move out and delete ref to clientmanager.
            m_networkServer.RemoveClientCircuit(this.CircuitCode);
            this.ClientThread.Abort();
        }

        #endregion

        # region Packet Handling
        public static bool AddPacketHandler(PacketType packetType, PacketMethod handler)
        {
            bool result = false;
            lock (PacketHandlers)
            {
                if (!PacketHandlers.ContainsKey(packetType))
                {
                    PacketHandlers.Add(packetType, handler);
                    result = true;
                }
            }
            return result;
        }

        public bool AddLocalPacketHandler(PacketType packetType, PacketMethod handler)
        {
            bool result = false;
            lock (m_packetHandlers)
            {
                if (!m_packetHandlers.ContainsKey(packetType))
                {
                    m_packetHandlers.Add(packetType, handler);
                    result = true;
                }
            }
            return result;
        }

        protected virtual bool ProcessPacketMethod(Packet packet)
        {
            bool result = false;
            bool found = false;
            PacketMethod method;
            if (m_packetHandlers.TryGetValue(packet.Type, out method))
            {
                //there is a local handler for this packet type
                result = method(this, packet);
            }
            else
            {
                //there is not a local handler so see if there is a Global handler
                lock (PacketHandlers)
                {
                    found = PacketHandlers.TryGetValue(packet.Type, out method);
                }
                if (found)
                {
                    result = method(this, packet);
                }
            }
            return result;
        }

        protected void DebugPacket(string direction, Packet packet)
        {
            if (debug > 0) {
                string info;
                if (debug < 255 && packet.Type == PacketType.AgentUpdate) 
                  return;
                if (debug < 254 && packet.Type == PacketType.ViewerEffect) 
                  return;
                if (debug < 253 && (
                      packet.Type == PacketType.CompletePingCheck ||
                      packet.Type == PacketType.StartPingCheck 
                      ) )
                  return;
                if (debug < 252 && packet.Type == PacketType.PacketAck) 
                  return;

                if (debug > 1) {
                    info = packet.ToString();
                } else {
                    info = packet.Type.ToString();
                }
                Console.WriteLine(CircuitCode + ":" + direction + ": " + info);
            }
        }

        protected virtual void ClientLoop()
        {
            MainLog.Instance.Verbose("OpenSimClient.cs:ClientLoop() - Entered loop");
            while (true)
            {
                QueItem nextPacket = PacketQueue.Dequeue();
                if (nextPacket.Incoming)
                {
                    //is a incoming packet
                    if (nextPacket.Packet.Type != PacketType.AgentUpdate)
                    {
                        packetsReceived++;
                    }
                    DebugPacket("IN", nextPacket.Packet);
                    ProcessInPacket(nextPacket.Packet);
                }
                else
                {
                    //is a out going packet
                    DebugPacket("OUT", nextPacket.Packet);
                    ProcessOutPacket(nextPacket.Packet);
                }
            }
        }
        # endregion

        protected void CheckClientConnectivity(object sender, ElapsedEventArgs e)
        {
            if (packetsReceived == lastPacketsReceived)
            {
                probesWithNoIngressPackets++;
                if (probesWithNoIngressPackets > 30)
                {
                    // Refactor out this into an OnConnectionClosed so the ClientManager can clean up
                    this.Close();
                }
                else
                {
                    // this will normally trigger at least one packet (ping response)
                    SendStartPingCheck(0);
                }
            }
            else
            {
                // Something received in the meantime - we can reset the counters
                probesWithNoIngressPackets = 0;
                lastPacketsReceived = packetsReceived;
            }
        }

        # region Setup

        protected virtual void InitNewClient()
        {
            clientPingTimer = new Timer(5000);
            clientPingTimer.Elapsed += new ElapsedEventHandler(CheckClientConnectivity);
            clientPingTimer.Enabled = true;

            MainLog.Instance.Verbose("OpenSimClient.cs:InitNewClient() - Adding viewer agent to scene");
            this.m_scene.AddNewClient(this, false);
        }

        protected virtual void AuthUser()
        {
            // AuthenticateResponse sessionInfo = m_gridServer.AuthenticateSession(cirpack.CircuitCode.m_sessionId, cirpack.CircuitCode.ID, cirpack.CircuitCode.Code);
            AuthenticateResponse sessionInfo = this.m_authenticateSessionsHandler.AuthenticateSession(cirpack.CircuitCode.SessionID, cirpack.CircuitCode.ID, cirpack.CircuitCode.Code);
            if (!sessionInfo.Authorised)
            {
                //session/circuit not authorised
                MainLog.Instance.Notice("OpenSimClient.cs:AuthUser() - New user request denied to " + userEP.ToString());
                ClientThread.Abort();
            }
            else
            {
                MainLog.Instance.Notice("OpenSimClient.cs:AuthUser() - Got authenticated connection from " + userEP.ToString());
                //session is authorised
                m_agentId = cirpack.CircuitCode.ID;
                this.m_sessionId = cirpack.CircuitCode.SessionID;
                this.CircuitCode = cirpack.CircuitCode.Code;
                this.firstName = sessionInfo.LoginInfo.First;
                this.lastName = sessionInfo.LoginInfo.Last;

                if (sessionInfo.LoginInfo.SecureSession != LLUUID.Zero)
                {
                    this.SecureSessionID = sessionInfo.LoginInfo.SecureSession;
                }
                InitNewClient();

                ClientLoop();
            }
        }
        # endregion


        protected void KillThread()
        {
            this.ClientThread.Abort();
        }

    }
}
