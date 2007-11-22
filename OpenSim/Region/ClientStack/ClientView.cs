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
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using Timer=System.Timers.Timer;

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

        protected static Dictionary<PacketType, PacketMethod> PacketHandlers =
            new Dictionary<PacketType, PacketMethod>(); //Global/static handlers for all clients

        protected Dictionary<PacketType, PacketMethod> m_packetHandlers = new Dictionary<PacketType, PacketMethod>();
        //local handlers for this instance 

        private LLUUID m_sessionId;
        public LLUUID SecureSessionID = LLUUID.Zero;
        public string firstName;
        public string lastName;
        private UseCircuitCodePacket cirpack;
        public Thread ClientThread;
        public LLVector3 startpos;

        //private AgentAssetUpload UploadAssets;
        private LLUUID newAssetFolder = LLUUID.Zero;
        private int debug = 0;

        protected IScene m_scene;

        public IScene Scene
        {
            get { return m_scene; }
        }

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

        // 1536000
        private int throttleOutboundMax = 1536000; // Number of bytes allowed to go out per second. (256kbps per client) 
                                              // TODO: Make this variable. Lower throttle on un-ack. Raise over time?
        private int throttleSentPeriod = 0;   // Number of bytes sent this period

        private int throttleOutbound = 162144; // Number of bytes allowed to go out per second. (256kbps per client) 
        // TODO: Make this variable. Lower throttle on un-ack. Raise over time

        // All throttle times and number of bytes are calculated by dividing by this value
        private int throttleTimeDivisor = 7;

        private int throttletimems = 1000;

        // Maximum -per type- throttle
        private int ResendthrottleMAX = 100000;
        private int LandthrottleMax = 100000;
        private int WindthrottleMax = 100000;
        private int CloudthrottleMax = 100000;
        private int TaskthrottleMax = 800000;
        private int AssetthrottleMax = 800000;
        private int TexturethrottleMax = 800000;

        // Minimum -per type- throttle
        private int ResendthrottleMin = 5000; // setting resendmin to 0 results in mostly dropped packets
        private int LandthrottleMin = 1000;
        private int WindthrottleMin = 1000;
        private int CloudthrottleMin = 1000;
        private int TaskthrottleMin = 1000;
        private int AssetthrottleMin = 1000;
        private int TexturethrottleMin = 1000;

        // Sim default per-client settings.
        private int ResendthrottleOutbound = 50000;
        private int ResendthrottleSentPeriod = 0;
        private int LandthrottleOutbound = 100000;
        private int LandthrottleSentPeriod = 0;
        private int WindthrottleOutbound = 10000;
        private int WindthrottleSentPeriod = 0;
        private int CloudthrottleOutbound = 5000;
        private int CloudthrottleSentPeriod = 0;
        private int TaskthrottleOutbound = 100000;
        private int TaskthrottleSentPeriod = 0;
        private int AssetthrottleOutbound = 80000;
        private int AssetthrottleSentPeriod = 0;
        private int TexturethrottleOutbound = 100000;
        private int TexturethrottleSentPeriod = 0;

        private Timer throttleTimer;

        public ClientView(EndPoint remoteEP, UseCircuitCodePacket initialcirpack, ClientManager clientManager,
                          IScene scene, AssetCache assetCache, PacketServer packServer,
                          AgentCircuitManager authenSessions)
        {
            m_moneyBalance = 1000;

            m_scene = scene;
            m_clientManager = clientManager;
            m_assetCache = assetCache;

            m_networkServer = packServer;
            // m_inventoryCache = inventoryCache;
            m_authenticateSessionsHandler = authenSessions;

            MainLog.Instance.Verbose("CLIENT", "Started up new client thread to handle incoming request");
            cirpack = initialcirpack;
            userEP = remoteEP;

            startpos = m_authenticateSessionsHandler.GetPosition(initialcirpack.CircuitCode.Code);


            // While working on this, the BlockingQueue had me fooled for a bit.
            // The Blocking queue causes the thread to stop until there's something 
            // in it to process.  it's an on-purpose threadlock though because 
            // without it, the clientloop will suck up all sim resources.

            PacketQueue = new BlockingQueue<QueItem>();

            IncomingPacketQueue = new Queue<QueItem>();
            OutgoingPacketQueue = new Queue<QueItem>();
            ResendOutgoingPacketQueue = new Queue<QueItem>();
            LandOutgoingPacketQueue = new Queue<QueItem>();
            WindOutgoingPacketQueue = new Queue<QueItem>();
            CloudOutgoingPacketQueue = new Queue<QueItem>();
            TaskOutgoingPacketQueue = new Queue<QueItem>();
            TextureOutgoingPacketQueue = new Queue<QueItem>();
            AssetOutgoingPacketQueue = new Queue<QueItem>();


            //this.UploadAssets = new AgentAssetUpload(this, m_assetCache, m_inventoryCache);
            AckTimer = new Timer(750);
            AckTimer.Elapsed += new ElapsedEventHandler(AckTimer_Elapsed);
            AckTimer.Start();

            throttleTimer = new Timer((int)(throttletimems/throttleTimeDivisor));
            throttleTimer.Elapsed += new ElapsedEventHandler(throttleTimer_Elapsed);
            throttleTimer.Start();

            RegisterLocalPacketHandlers();

            ClientThread = new Thread(new ThreadStart(AuthUser));
            ClientThread.IsBackground = true;
            ClientThread.Start();
        }

        void throttleTimer_Elapsed(object sender, ElapsedEventArgs e)
        {   
            throttleSentPeriod = 0;
            ResendthrottleSentPeriod = 0;
            LandthrottleSentPeriod = 0;
            WindthrottleSentPeriod = 0;
            CloudthrottleSentPeriod = 0;
            TaskthrottleSentPeriod = 0;
            AssetthrottleSentPeriod = 0;
            TexturethrottleSentPeriod = 0;
            
            // I was considering this..   Will an event fire if the thread it's on is blocked?

            // Then I figured out..  it doesn't really matter..  because this thread won't be blocked for long
            // The General overhead of the UDP protocol gets sent to the queue un-throttled by this
            // so This'll pick up about around the right time.

            int MaxThrottleLoops = 4550; // 50*7 packets can be dequeued at once.
            int throttleLoops = 0;

            // We're going to dequeue all of the saved up packets until 
            // we've hit the throttle limit or there's no more packets to send
            while ((throttleSentPeriod <= ((int)(throttleOutbound/throttleTimeDivisor)) &&
                (ResendOutgoingPacketQueue.Count > 0 ||
                 LandOutgoingPacketQueue.Count > 0 ||
                 WindOutgoingPacketQueue.Count > 0 ||
                 CloudOutgoingPacketQueue.Count > 0 ||
                 TaskOutgoingPacketQueue.Count > 0 ||
                 AssetOutgoingPacketQueue.Count > 0 ||
                 TextureOutgoingPacketQueue.Count > 0)) && throttleLoops <= MaxThrottleLoops)
            {
                throttleLoops++;
                //Now comes the fun part..   we dump all our elements into PacketQueue that we've saved up.
                if (ResendthrottleSentPeriod <= ((int)(ResendthrottleOutbound/throttleTimeDivisor)) && ResendOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = ResendOutgoingPacketQueue.Dequeue();

                    PacketQueue.Enqueue(qpack);
                    throttleSentPeriod += qpack.Packet.ToBytes().Length;
                    ResendthrottleSentPeriod += qpack.Packet.ToBytes().Length;
                }
                if (LandthrottleSentPeriod <= ((int)(LandthrottleOutbound/throttleTimeDivisor)) && LandOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = LandOutgoingPacketQueue.Dequeue();

                    PacketQueue.Enqueue(qpack);
                    throttleSentPeriod += qpack.Packet.ToBytes().Length;
                    LandthrottleSentPeriod += qpack.Packet.ToBytes().Length;
                }
                if (WindthrottleSentPeriod <= ((int)(WindthrottleOutbound/throttleTimeDivisor)) && WindOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = WindOutgoingPacketQueue.Dequeue();

                    PacketQueue.Enqueue(qpack);
                    throttleSentPeriod += qpack.Packet.ToBytes().Length;
                    WindthrottleSentPeriod += qpack.Packet.ToBytes().Length;
                }
                if (CloudthrottleSentPeriod <= ((int)(CloudthrottleOutbound/throttleTimeDivisor)) && CloudOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = CloudOutgoingPacketQueue.Dequeue();

                    PacketQueue.Enqueue(qpack);
                    throttleSentPeriod += qpack.Packet.ToBytes().Length;
                    CloudthrottleSentPeriod += qpack.Packet.ToBytes().Length;
                }
                if (TaskthrottleSentPeriod <= ((int)(TaskthrottleOutbound/throttleTimeDivisor)) && TaskOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = TaskOutgoingPacketQueue.Dequeue();

                    PacketQueue.Enqueue(qpack);
                    throttleSentPeriod += qpack.Packet.ToBytes().Length;
                    TaskthrottleSentPeriod += qpack.Packet.ToBytes().Length;
                }
                if (TexturethrottleSentPeriod <= ((int)(TexturethrottleOutbound/throttleTimeDivisor)) && TextureOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = TextureOutgoingPacketQueue.Dequeue();

                    PacketQueue.Enqueue(qpack);
                    throttleSentPeriod += qpack.Packet.ToBytes().Length;
                    TexturethrottleSentPeriod += qpack.Packet.ToBytes().Length;
                }
                if (AssetthrottleSentPeriod <= ((int)(AssetthrottleOutbound/throttleTimeDivisor)) && AssetOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = AssetOutgoingPacketQueue.Dequeue();

                    PacketQueue.Enqueue(qpack);
                    throttleSentPeriod += qpack.Packet.ToBytes().Length;
                    AssetthrottleSentPeriod += qpack.Packet.ToBytes().Length;
                }

            }

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

            m_scene.RemoveClient(AgentId);

            ClientThread.Abort();
        }

        public void Stop()
        {
            clientPingTimer.Stop();

            libsecondlife.Packets.DisableSimulatorPacket disable = new libsecondlife.Packets.DisableSimulatorPacket();
            OutPacket(disable, ThrottleOutPacketType.Task);

            ClientThread.Abort();
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
            if (debug > 0)
            {
                string info = "";
                if (debug < 255 && packet.Type == PacketType.AgentUpdate)
                    return;
                if (debug < 254 && packet.Type == PacketType.ViewerEffect)
                    return;
                if (debug < 253 && (
                                       packet.Type == PacketType.CompletePingCheck ||
                                       packet.Type == PacketType.StartPingCheck
                                   ))
                    return;
                if (debug < 252 && packet.Type == PacketType.PacketAck)
                    return;

                if (debug > 1)
                {
                    info = packet.ToString();
                }
                else
                {
                    info = packet.Type.ToString();
                }
                Console.WriteLine(m_circuitCode + ":" + direction + ": " + info);
            }
        }

        protected virtual void ClientLoop()
        {
            bool queuedLast = false;

            MainLog.Instance.Verbose("CLIENT", "Entered loop");
            while (true)
            {
                QueItem nextPacket = PacketQueue.Dequeue();
                if (nextPacket.Incoming)
                {
                    queuedLast = false;

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
                    // Throw it back on the queue if it's going to cause us to flood the client
                    if (throttleSentPeriod > throttleOutboundMax)
                    {
                        PacketQueue.Enqueue(nextPacket);
                        MainLog.Instance.Verbose("Client over throttle limit, requeuing packet");

                        if (queuedLast)
                        {
                            MainLog.Instance.Verbose("No more sendable packets, need to sleep now");
                            Thread.Sleep(100); // Wait a little while if this was the last packet we saw
                        }

                        queuedLast = true;
                    }
                    else
                    {
                        queuedLast = false;

                        // TODO: May be a bit expensive doing this twice.
                        
                            //Don't throttle AvatarPickerReplies!, they return a null .ToBytes()!
                            if (nextPacket.Packet.Type != PacketType.AvatarPickerReply)
                                throttleSentPeriod += nextPacket.Packet.ToBytes().Length;
                        

                            //is a out going packet
                            DebugPacket("OUT", nextPacket.Packet);
                            ProcessOutPacket(nextPacket.Packet);
                        
                    }
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
                    if (OnConnectionClosed != null)
                    {
                        OnConnectionClosed(this);
                    }
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

            MainLog.Instance.Verbose("CLIENT", "Adding viewer agent to scene");
            m_scene.AddNewClient(this, true);
        }

        protected virtual void AuthUser()
        {
            // AuthenticateResponse sessionInfo = m_gridServer.AuthenticateSession(cirpack.m_circuitCode.m_sessionId, cirpack.m_circuitCode.ID, cirpack.m_circuitCode.Code);
            AuthenticateResponse sessionInfo =
                m_authenticateSessionsHandler.AuthenticateSession(cirpack.CircuitCode.SessionID, cirpack.CircuitCode.ID,
                                                                  cirpack.CircuitCode.Code);
            if (!sessionInfo.Authorised)
            {
                //session/circuit not authorised
                MainLog.Instance.Notice("CLIENT", "New user request denied to " + userEP.ToString());
                ClientThread.Abort();
            }
            else
            {
                MainLog.Instance.Notice("CLIENT", "Got authenticated connection from " + userEP.ToString());
                //session is authorised
                m_agentId = cirpack.CircuitCode.ID;
                m_sessionId = cirpack.CircuitCode.SessionID;
                m_circuitCode = cirpack.CircuitCode.Code;
                firstName = sessionInfo.LoginInfo.First;
                lastName = sessionInfo.LoginInfo.Last;

                if (sessionInfo.LoginInfo.SecureSession != LLUUID.Zero)
                {
                    SecureSessionID = sessionInfo.LoginInfo.SecureSession;
                }
                InitNewClient();

                ClientLoop();
            }
        }

        # endregion

        protected void KillThread()
        {
            ClientThread.Abort();
        }
    }
}