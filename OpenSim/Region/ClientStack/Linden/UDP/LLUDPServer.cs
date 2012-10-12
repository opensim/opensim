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
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using log4net;
using Nini.Config;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Scenes;
using OpenMetaverse;

using TokenBucket = OpenSim.Region.ClientStack.LindenUDP.TokenBucket;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    /// <summary>
    /// A shim around LLUDPServer that implements the IClientNetworkServer interface
    /// </summary>
    public sealed class LLUDPServerShim : IClientNetworkServer
    {
        LLUDPServer m_udpServer;

        public LLUDPServerShim()
        {
        }

        public void Initialise(IPAddress listenIP, ref uint port, int proxyPortOffsetParm, bool allow_alternate_port, IConfigSource configSource, AgentCircuitManager circuitManager)
        {
            m_udpServer = new LLUDPServer(listenIP, ref port, proxyPortOffsetParm, allow_alternate_port, configSource, circuitManager);
        }

        public void NetworkStop()
        {
            m_udpServer.Stop();
        }

        public void AddScene(IScene scene)
        {
            m_udpServer.AddScene(scene);
        }

        public bool HandlesRegion(Location x)
        {
            return m_udpServer.HandlesRegion(x);
        }

        public void Start()
        {
            m_udpServer.Start();
        }

        public void Stop()
        {
            m_udpServer.Stop();
        }
    }

    /// <summary>
    /// The LLUDP server for a region. This handles incoming and outgoing
    /// packets for all UDP connections to the region
    /// </summary>
    public class LLUDPServer : OpenSimUDPBase
    {
        /// <summary>Maximum transmission unit, or UDP packet size, for the LLUDP protocol</summary>
        public const int MTU = 1400;

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>The measured resolution of Environment.TickCount</summary>
        public readonly float TickCountResolution;

        /// <summary>Number of prim updates to put on the queue each time the 
        /// OnQueueEmpty event is triggered for updates</summary>
        public readonly int PrimUpdatesPerCallback;

        /// <summary>Number of texture packets to put on the queue each time the
        /// OnQueueEmpty event is triggered for textures</summary>
        public readonly int TextureSendLimit;

        /// <summary>Handlers for incoming packets</summary>
        //PacketEventDictionary packetEvents = new PacketEventDictionary();
        /// <summary>Incoming packets that are awaiting handling</summary>
        private OpenMetaverse.BlockingQueue<IncomingPacket> packetInbox = new OpenMetaverse.BlockingQueue<IncomingPacket>();

        /// <summary></summary>
        //private UDPClientCollection m_clients = new UDPClientCollection();
        /// <summary>Bandwidth throttle for this UDP server</summary>
        protected TokenBucket m_throttle;
        
        /// <summary>Bandwidth throttle rates for this UDP server</summary>
        public ThrottleRates ThrottleRates { get; private set; }
        
        /// <summary>Manages authentication for agent circuits</summary>
        private AgentCircuitManager m_circuitManager;

        /// <summary>Reference to the scene this UDP server is attached to</summary>
        protected Scene m_scene;

        /// <summary>The X/Y coordinates of the scene this UDP server is attached to</summary>
        private Location m_location;

        /// <summary>The size of the receive buffer for the UDP socket. This value
        /// is passed up to the operating system and used in the system networking
        /// stack. Use zero to leave this value as the default</summary>
        private int m_recvBufferSize;

        /// <summary>Flag to process packets asynchronously or synchronously</summary>
        private bool m_asyncPacketHandling;

        /// <summary>Tracks whether or not a packet was sent each round so we know
        /// whether or not to sleep</summary>
        private bool m_packetSent;

        /// <summary>Environment.TickCount of the last time that packet stats were reported to the scene</summary>
        private int m_elapsedMSSinceLastStatReport = 0;

        /// <summary>Environment.TickCount of the last time the outgoing packet handler executed</summary>
        private int m_tickLastOutgoingPacketHandler;

        /// <summary>Keeps track of the number of elapsed milliseconds since the last time the outgoing packet handler looped</summary>
        private int m_elapsedMSOutgoingPacketHandler;

        /// <summary>Keeps track of the number of 100 millisecond periods elapsed in the outgoing packet handler executed</summary>
        private int m_elapsed100MSOutgoingPacketHandler;

        /// <summary>Keeps track of the number of 500 millisecond periods elapsed in the outgoing packet handler executed</summary>
        private int m_elapsed500MSOutgoingPacketHandler;

        /// <summary>Flag to signal when clients should check for resends</summary>
        protected bool m_resendUnacked;

        /// <summary>Flag to signal when clients should send ACKs</summary>
        protected bool m_sendAcks;

        /// <summary>Flag to signal when clients should send pings</summary>
        protected bool m_sendPing;

        private int m_defaultRTO = 0;
        private int m_maxRTO = 0;
        private int m_ackTimeout = 0;
        private int m_pausedAckTimeout = 0;
        private bool m_disableFacelights = false;

        public Socket Server { get { return null; } }

        private int m_malformedCount = 0; // Guard against a spamming attack

        /// <summary>
        /// Record current outgoing client for monitoring purposes.
        /// </summary>
        private IClientAPI m_currentOutgoingClient;

        /// <summary>
        /// Recording current incoming client for monitoring purposes.
        /// </summary>
        private IClientAPI m_currentIncomingClient;

        public LLUDPServer(IPAddress listenIP, ref uint port, int proxyPortOffsetParm, bool allow_alternate_port, IConfigSource configSource, AgentCircuitManager circuitManager)
            : base(listenIP, (int)port)
        {
            #region Environment.TickCount Measurement

            // Measure the resolution of Environment.TickCount
            TickCountResolution = 0f;
            for (int i = 0; i < 5; i++)
            {
                int start = Environment.TickCount;
                int now = start;
                while (now == start)
                    now = Environment.TickCount;
                TickCountResolution += (float)(now - start) * 0.2f;
            }
            m_log.Info("[LLUDPSERVER]: Average Environment.TickCount resolution: " + TickCountResolution + "ms");
            TickCountResolution = (float)Math.Ceiling(TickCountResolution);

            #endregion Environment.TickCount Measurement

            m_circuitManager = circuitManager;
            int sceneThrottleBps = 0;

            IConfig config = configSource.Configs["ClientStack.LindenUDP"];
            if (config != null)
            {
                m_asyncPacketHandling = config.GetBoolean("async_packet_handling", true);
                m_recvBufferSize = config.GetInt("client_socket_rcvbuf_size", 0);
                sceneThrottleBps = config.GetInt("scene_throttle_max_bps", 0);

                PrimUpdatesPerCallback = config.GetInt("PrimUpdatesPerCallback", 100);
                TextureSendLimit = config.GetInt("TextureSendLimit", 20);

                m_defaultRTO = config.GetInt("DefaultRTO", 0);
                m_maxRTO = config.GetInt("MaxRTO", 0);
                m_disableFacelights = config.GetBoolean("DisableFacelights", false);
                m_ackTimeout = 1000 * config.GetInt("AckTimeout", 60);
                m_pausedAckTimeout = 1000 * config.GetInt("PausedAckTimeout", 300);
            }
            else
            {
                PrimUpdatesPerCallback = 100;
                TextureSendLimit = 20;
                m_ackTimeout = 1000 * 60; // 1 minute
                m_pausedAckTimeout = 1000 * 300; // 5 minutes
            }

            // FIXME: This actually only needs to be done once since the PacketPool is shared across all servers.
            // However, there is no harm in temporarily doing it multiple times.
            IConfig packetConfig = configSource.Configs["PacketPool"];
            if (packetConfig != null)
            {
                PacketPool.Instance.RecyclePackets = packetConfig.GetBoolean("RecyclePackets", true);
                PacketPool.Instance.RecycleDataBlocks = packetConfig.GetBoolean("RecycleDataBlocks", true);
            }

            #region BinaryStats
            config = configSource.Configs["Statistics.Binary"];
            m_shouldCollectStats = false;
            if (config != null)
            {
               if (config.Contains("enabled") && config.GetBoolean("enabled"))
               {
                   if (config.Contains("collect_packet_headers"))
                       m_shouldCollectStats = config.GetBoolean("collect_packet_headers");
                   if (config.Contains("packet_headers_period_seconds"))
                   {
                       binStatsMaxFilesize = TimeSpan.FromSeconds(config.GetInt("region_stats_period_seconds"));
                   }
                   if (config.Contains("stats_dir"))
                   {
                       binStatsDir = config.GetString("stats_dir");
                   }
               }
               else
               {
                   m_shouldCollectStats = false;
               }
           }
           #endregion BinaryStats

            m_throttle = new TokenBucket(null, sceneThrottleBps);
            ThrottleRates = new ThrottleRates(configSource);
        }

        public void Start()
        {
            if (m_scene == null)
                throw new InvalidOperationException("[LLUDPSERVER]: Cannot LLUDPServer.Start() without an IScene reference");

            m_log.InfoFormat(
                "[LLUDPSERVER]: Starting the LLUDP server in {0} mode",
                m_asyncPacketHandling ? "asynchronous" : "synchronous");

            base.Start(m_recvBufferSize, m_asyncPacketHandling);

            // Start the packet processing threads
            Watchdog.StartThread(
                IncomingPacketHandler,
                string.Format("Incoming Packets ({0})", m_scene.RegionInfo.RegionName),
                ThreadPriority.Normal,
                false,
                true,
                GetWatchdogIncomingAlarmData,
                Watchdog.DEFAULT_WATCHDOG_TIMEOUT_MS);

            Watchdog.StartThread(
                OutgoingPacketHandler,
                string.Format("Outgoing Packets ({0})", m_scene.RegionInfo.RegionName),
                ThreadPriority.Normal,
                false,
                true,
                GetWatchdogOutgoingAlarmData,
                Watchdog.DEFAULT_WATCHDOG_TIMEOUT_MS);

            m_elapsedMSSinceLastStatReport = Environment.TickCount;
        }

        /// <summary>
        /// If the outgoing UDP thread times out, then return client that was being processed to help with debugging.
        /// </summary>
        /// <returns></returns>
        private string GetWatchdogIncomingAlarmData()
        {
            return string.Format(
                "Client is {0}",
                m_currentIncomingClient != null ? m_currentIncomingClient.Name : "none");
        }

        /// <summary>
        /// If the outgoing UDP thread times out, then return client that was being processed to help with debugging.
        /// </summary>
        /// <returns></returns>
        private string GetWatchdogOutgoingAlarmData()
        {
            return string.Format(
                "Client is {0}",
                m_currentOutgoingClient != null ? m_currentOutgoingClient.Name : "none");
        }

        public new void Stop()
        {
            m_log.Info("[LLUDPSERVER]: Shutting down the LLUDP server for " + m_scene.RegionInfo.RegionName);
            base.Stop();
        }

        public void AddScene(IScene scene)
        {
            if (m_scene != null)
            {
                m_log.Error("[LLUDPSERVER]: AddScene() called on an LLUDPServer that already has a scene");
                return;
            }

            if (!(scene is Scene))
            {
                m_log.Error("[LLUDPSERVER]: AddScene() called with an unrecognized scene type " + scene.GetType());
                return;
            }

            m_scene = (Scene)scene;
            m_location = new Location(m_scene.RegionInfo.RegionHandle);
        }

        public bool HandlesRegion(Location x)
        {
            return x == m_location;
        }

        public void BroadcastPacket(Packet packet, ThrottleOutPacketType category, bool sendToPausedAgents, bool allowSplitting)
        {
            // CoarseLocationUpdate and AvatarGroupsReply packets cannot be split in an automated way
            if ((packet.Type == PacketType.CoarseLocationUpdate || packet.Type == PacketType.AvatarGroupsReply) && allowSplitting)
                allowSplitting = false;

            if (allowSplitting && packet.HasVariableBlocks)
            {
                byte[][] datas = packet.ToBytesMultiple();
                int packetCount = datas.Length;

                if (packetCount < 1)
                    m_log.Error("[LLUDPSERVER]: Failed to split " + packet.Type + " with estimated length " + packet.Length);

                for (int i = 0; i < packetCount; i++)
                {
                    byte[] data = datas[i];
                    m_scene.ForEachClient(
                        delegate(IClientAPI client)
                        {
                            if (client is LLClientView)
                                SendPacketData(((LLClientView)client).UDPClient, data, packet.Type, category, null);
                        }
                    );
                }
            }
            else
            {
                byte[] data = packet.ToBytes();
                m_scene.ForEachClient(
                    delegate(IClientAPI client)
                    {
                        if (client is LLClientView)
                            SendPacketData(((LLClientView)client).UDPClient, data, packet.Type, category, null);
                    }
                );
            }
        }

        /// <summary>
        /// Start the process of sending a packet to the client.
        /// </summary>
        /// <param name="udpClient"></param>
        /// <param name="packet"></param>
        /// <param name="category"></param>
        /// <param name="allowSplitting"></param>
        /// <param name="method">
        /// The method to call if the packet is not acked by the client.  If null, then a standard
        /// resend of the packet is done.
        /// </param>
        public virtual void SendPacket(
            LLUDPClient udpClient, Packet packet, ThrottleOutPacketType category, bool allowSplitting, UnackedPacketMethod method)
        {
            // CoarseLocationUpdate packets cannot be split in an automated way
            if (packet.Type == PacketType.CoarseLocationUpdate && allowSplitting)
                allowSplitting = false;

            if (allowSplitting && packet.HasVariableBlocks)
            {
                byte[][] datas = packet.ToBytesMultiple();
                int packetCount = datas.Length;

                if (packetCount < 1)
                    m_log.Error("[LLUDPSERVER]: Failed to split " + packet.Type + " with estimated length " + packet.Length);

                for (int i = 0; i < packetCount; i++)
                {
                    byte[] data = datas[i];
                    SendPacketData(udpClient, data, packet.Type, category, method);
                }
            }
            else
            {
                byte[] data = packet.ToBytes();
                SendPacketData(udpClient, data, packet.Type, category, method);
            }

            PacketPool.Instance.ReturnPacket(packet);
        }

        /// <summary>
        /// Start the process of sending a packet to the client.
        /// </summary>
        /// <param name="udpClient"></param>
        /// <param name="data"></param>
        /// <param name="type"></param>
        /// <param name="category"></param>
        /// <param name="method">
        /// The method to call if the packet is not acked by the client.  If null, then a standard
        /// resend of the packet is done.
        /// </param>
        public void SendPacketData(
            LLUDPClient udpClient, byte[] data, PacketType type, ThrottleOutPacketType category, UnackedPacketMethod method)
        {
            int dataLength = data.Length;
            bool doZerocode = (data[0] & Helpers.MSG_ZEROCODED) != 0;
            bool doCopy = true;

            // Frequency analysis of outgoing packet sizes shows a large clump of packets at each end of the spectrum.
            // The vast majority of packets are less than 200 bytes, although due to asset transfers and packet splitting
            // there are a decent number of packets in the 1000-1140 byte range. We allocate one of two sizes of data here
            // to accomodate for both common scenarios and provide ample room for ACK appending in both
            int bufferSize = (dataLength > 180) ? LLUDPServer.MTU : 200;

            UDPPacketBuffer buffer = new UDPPacketBuffer(udpClient.RemoteEndPoint, bufferSize);

            // Zerocode if needed
            if (doZerocode)
            {
                try
                {
                    dataLength = Helpers.ZeroEncode(data, dataLength, buffer.Data);
                    doCopy = false;
                }
                catch (IndexOutOfRangeException)
                {
                    // The packet grew larger than the bufferSize while zerocoding.
                    // Remove the MSG_ZEROCODED flag and send the unencoded data
                    // instead
                    m_log.Debug("[LLUDPSERVER]: Packet exceeded buffer size during zerocoding for " + type + ". DataLength=" + dataLength +
                        " and BufferLength=" + buffer.Data.Length + ". Removing MSG_ZEROCODED flag");
                    data[0] = (byte)(data[0] & ~Helpers.MSG_ZEROCODED);
                }
            }

            // If the packet data wasn't already copied during zerocoding, copy it now
            if (doCopy)
            {
                if (dataLength <= buffer.Data.Length)
                {
                    Buffer.BlockCopy(data, 0, buffer.Data, 0, dataLength);
                }
                else
                {
                    bufferSize = dataLength;
                    buffer = new UDPPacketBuffer(udpClient.RemoteEndPoint, bufferSize);

                    // m_log.Error("[LLUDPSERVER]: Packet exceeded buffer size! This could be an indication of packet assembly not obeying the MTU. Type=" +
                    //     type + ", DataLength=" + dataLength + ", BufferLength=" + buffer.Data.Length + ". Dropping packet");
                    Buffer.BlockCopy(data, 0, buffer.Data, 0, dataLength);
                }
            }

            buffer.DataLength = dataLength;

            #region Queue or Send

            OutgoingPacket outgoingPacket = new OutgoingPacket(udpClient, buffer, category, null);
            // If we were not provided a method for handling unacked, use the UDPServer default method
            outgoingPacket.UnackedMethod = ((method == null) ? delegate(OutgoingPacket oPacket) { ResendUnacked(oPacket); } : method);

            // If a Linden Lab 1.23.5 client receives an update packet after a kill packet for an object, it will 
            // continue to display the deleted object until relog.  Therefore, we need to always queue a kill object
            // packet so that it isn't sent before a queued update packet.
            bool requestQueue = type == PacketType.KillObject;
            if (!outgoingPacket.Client.EnqueueOutgoing(outgoingPacket, requestQueue))
                SendPacketFinal(outgoingPacket);

            #endregion Queue or Send
        }

        public void SendAcks(LLUDPClient udpClient)
        {
            uint ack;

            if (udpClient.PendingAcks.Dequeue(out ack))
            {
                List<PacketAckPacket.PacketsBlock> blocks = new List<PacketAckPacket.PacketsBlock>();
                PacketAckPacket.PacketsBlock block = new PacketAckPacket.PacketsBlock();
                block.ID = ack;
                blocks.Add(block);

                while (udpClient.PendingAcks.Dequeue(out ack))
                {
                    block = new PacketAckPacket.PacketsBlock();
                    block.ID = ack;
                    blocks.Add(block);
                }

                PacketAckPacket packet = new PacketAckPacket();
                packet.Header.Reliable = false;
                packet.Packets = blocks.ToArray();

                SendPacket(udpClient, packet, ThrottleOutPacketType.Unknown, true, null);
            }
        }

        public void SendPing(LLUDPClient udpClient)
        {
            StartPingCheckPacket pc = (StartPingCheckPacket)PacketPool.Instance.GetPacket(PacketType.StartPingCheck);
            pc.Header.Reliable = false;

            pc.PingID.PingID = (byte)udpClient.CurrentPingSequence++;
            // We *could* get OldestUnacked, but it would hurt performance and not provide any benefit
            pc.PingID.OldestUnacked = 0;

            SendPacket(udpClient, pc, ThrottleOutPacketType.Unknown, false, null);
        }

        public void CompletePing(LLUDPClient udpClient, byte pingID)
        {
            CompletePingCheckPacket completePing = new CompletePingCheckPacket();
            completePing.PingID.PingID = pingID;
            SendPacket(udpClient, completePing, ThrottleOutPacketType.Unknown, false, null);
        }

        public void HandleUnacked(LLClientView client)
        {
            LLUDPClient udpClient = client.UDPClient;

            if (!udpClient.IsConnected)
                return;

            // Disconnect an agent if no packets are received for some time
            int timeoutTicks = m_ackTimeout;

            // Allow more slack if the client is "paused" eg file upload dialogue is open
            // Some sort of limit is needed in case the client crashes, loses its network connection
            // or some other disaster prevents it from sendung the AgentResume
            if (udpClient.IsPaused)
                timeoutTicks = m_pausedAckTimeout;

            if (client.IsActive &&
                (Environment.TickCount & Int32.MaxValue) - udpClient.TickLastPacketReceived > timeoutTicks)
            {
                // We must set IsActive synchronously so that we can stop the packet loop reinvoking this method, even
                // though it's set later on by LLClientView.Close()
                client.IsActive = false;

                // Fire this out on a different thread so that we don't hold up outgoing packet processing for
                // everybody else if this is being called due to an ack timeout.
                // This is the same as processing as the async process of a logout request.
                Util.FireAndForget(o => DeactivateClientDueToTimeout(client));

                return;
            }

            // Get a list of all of the packets that have been sitting unacked longer than udpClient.RTO
            List<OutgoingPacket> expiredPackets = udpClient.NeedAcks.GetExpiredPackets(udpClient.RTO);

            if (expiredPackets != null)
            {
                //m_log.Debug("[LLUDPSERVER]: Handling " + expiredPackets.Count + " packets to " + udpClient.AgentID + ", RTO=" + udpClient.RTO);
                // Exponential backoff of the retransmission timeout
                udpClient.BackoffRTO();
                for (int i = 0; i < expiredPackets.Count; ++i)
                    expiredPackets[i].UnackedMethod(expiredPackets[i]);
            }
        }

        public void ResendUnacked(OutgoingPacket outgoingPacket)
        {
            //m_log.DebugFormat("[LLUDPSERVER]: Resending packet #{0} (attempt {1}), {2}ms have passed",
            //    outgoingPacket.SequenceNumber, outgoingPacket.ResendCount, Environment.TickCount - outgoingPacket.TickCount);

            // Set the resent flag
            outgoingPacket.Buffer.Data[0] = (byte)(outgoingPacket.Buffer.Data[0] | Helpers.MSG_RESENT);
            outgoingPacket.Category = ThrottleOutPacketType.Resend;

            // Bump up the resend count on this packet
            Interlocked.Increment(ref outgoingPacket.ResendCount);

            // Requeue or resend the packet
            if (!outgoingPacket.Client.EnqueueOutgoing(outgoingPacket, false))
                SendPacketFinal(outgoingPacket);
        }

        public void Flush(LLUDPClient udpClient)
        {
            // FIXME: Implement?
        }

        /// <summary>
        /// Actually send a packet to a client.
        /// </summary>
        /// <param name="outgoingPacket"></param>
        internal void SendPacketFinal(OutgoingPacket outgoingPacket)
        {
            UDPPacketBuffer buffer = outgoingPacket.Buffer;
            byte flags = buffer.Data[0];
            bool isResend = (flags & Helpers.MSG_RESENT) != 0;
            bool isReliable = (flags & Helpers.MSG_RELIABLE) != 0;
            bool isZerocoded = (flags & Helpers.MSG_ZEROCODED) != 0;
            LLUDPClient udpClient = outgoingPacket.Client;

            if (!udpClient.IsConnected)
                return;

            #region ACK Appending

            int dataLength = buffer.DataLength;

            // NOTE: I'm seeing problems with some viewers when ACKs are appended to zerocoded packets so I've disabled that here
            if (!isZerocoded)
            {
                // Keep appending ACKs until there is no room left in the buffer or there are
                // no more ACKs to append
                uint ackCount = 0;
                uint ack;
                while (dataLength + 5 < buffer.Data.Length && udpClient.PendingAcks.Dequeue(out ack))
                {
                    Utils.UIntToBytesBig(ack, buffer.Data, dataLength);
                    dataLength += 4;
                    ++ackCount;
                }

                if (ackCount > 0)
                {
                    // Set the last byte of the packet equal to the number of appended ACKs
                    buffer.Data[dataLength++] = (byte)ackCount;
                    // Set the appended ACKs flag on this packet
                    buffer.Data[0] = (byte)(buffer.Data[0] | Helpers.MSG_APPENDED_ACKS);
                }
            }

            buffer.DataLength = dataLength;

            #endregion ACK Appending

            #region Sequence Number Assignment

            if (!isResend)
            {
                // Not a resend, assign a new sequence number
                uint sequenceNumber = (uint)Interlocked.Increment(ref udpClient.CurrentSequence);
                Utils.UIntToBytesBig(sequenceNumber, buffer.Data, 1);
                outgoingPacket.SequenceNumber = sequenceNumber;

                if (isReliable)
                {
                    // Add this packet to the list of ACK responses we are waiting on from the server
                    udpClient.NeedAcks.Add(outgoingPacket);
                }
            }
            else
            {
                Interlocked.Increment(ref udpClient.PacketsResent);
            }

            #endregion Sequence Number Assignment

            // Stats tracking
            Interlocked.Increment(ref udpClient.PacketsSent);

            // Put the UDP payload on the wire
            AsyncBeginSend(buffer);

            // Keep track of when this packet was sent out (right now)
            outgoingPacket.TickCount = Environment.TickCount & Int32.MaxValue;
        }

        public override void PacketReceived(UDPPacketBuffer buffer)
        {
            // Debugging/Profiling
            //try { Thread.CurrentThread.Name = "PacketReceived (" + m_scene.RegionInfo.RegionName + ")"; }
            //catch (Exception) { }
//            m_log.DebugFormat(
//                "[LLUDPSERVER]: Packet received from {0} in {1}", buffer.RemoteEndPoint, m_scene.RegionInfo.RegionName);

            LLUDPClient udpClient = null;
            Packet packet = null;
            int packetEnd = buffer.DataLength - 1;
            IPEndPoint address = (IPEndPoint)buffer.RemoteEndPoint;

            #region Decoding

            if (buffer.DataLength < 7)
            {
//                m_log.WarnFormat(
//                    "[LLUDPSERVER]: Dropping undersized packet with {0} bytes received from {1} in {2}",
//                    buffer.DataLength, buffer.RemoteEndPoint, m_scene.RegionInfo.RegionName);

                return; // Drop undersizd packet
            }

            int headerLen = 7;
            if (buffer.Data[6] == 0xFF)
            {
                if (buffer.Data[7] == 0xFF)
                    headerLen = 10;
                else
                    headerLen = 8;
            }

            if (buffer.DataLength < headerLen)
            {
//                m_log.WarnFormat(
//                    "[LLUDPSERVER]: Dropping packet with malformed header received from {0} in {1}",
//                    buffer.RemoteEndPoint, m_scene.RegionInfo.RegionName);

                return; // Malformed header
            }

            try
            {
//                packet = Packet.BuildPacket(buffer.Data, ref packetEnd,
//                    // Only allocate a buffer for zerodecoding if the packet is zerocoded
//                    ((buffer.Data[0] & Helpers.MSG_ZEROCODED) != 0) ? new byte[4096] : null);
                packet = PacketPool.Instance.GetPacket(buffer.Data, ref packetEnd,
                    // Only allocate a buffer for zerodecoding if the packet is zerocoded
                    ((buffer.Data[0] & Helpers.MSG_ZEROCODED) != 0) ? new byte[4096] : null);
            }
            catch (MalformedDataException)
            {
            }
            catch (IndexOutOfRangeException)
            {
//                m_log.WarnFormat(
//                    "[LLUDPSERVER]: Dropping short packet received from {0} in {1}",
//                    buffer.RemoteEndPoint, m_scene.RegionInfo.RegionName);

                return; // Drop short packet
            }
            catch (Exception e)
            {
                if (m_malformedCount < 100)
                    m_log.DebugFormat("[LLUDPSERVER]: Dropped malformed packet: " + e.ToString());

                m_malformedCount++;

                if ((m_malformedCount % 100000) == 0)
                    m_log.DebugFormat("[LLUDPSERVER]: Received {0} malformed packets so far, probable network attack.", m_malformedCount);
            }

            // Fail-safe check
            if (packet == null)
            {
                m_log.ErrorFormat("[LLUDPSERVER]: Malformed data, cannot parse {0} byte packet from {1}:",
                    buffer.DataLength, buffer.RemoteEndPoint);
                m_log.Error(Utils.BytesToHexString(buffer.Data, buffer.DataLength, null));
                return;
            }

            #endregion Decoding

            #region Packet to Client Mapping

            // UseCircuitCode handling
            if (packet.Type == PacketType.UseCircuitCode)
            {
                object[] array = new object[] { buffer, packet };

                Util.FireAndForget(HandleUseCircuitCode, array);

                return;
            }

            // Determine which agent this packet came from
            IClientAPI client;
            if (!m_scene.TryGetClient(address, out client) || !(client is LLClientView))
            {
                //m_log.Debug("[LLUDPSERVER]: Received a " + packet.Type + " packet from an unrecognized source: " + address + " in " + m_scene.RegionInfo.RegionName);
                return;
            }

            udpClient = ((LLClientView)client).UDPClient;

            if (!udpClient.IsConnected)
                return;

            #endregion Packet to Client Mapping

            // Stats tracking
            Interlocked.Increment(ref udpClient.PacketsReceived);

            int now = Environment.TickCount & Int32.MaxValue;
            udpClient.TickLastPacketReceived = now;

            #region ACK Receiving

            // Handle appended ACKs
            if (packet.Header.AppendedAcks && packet.Header.AckList != null)
            {
                for (int i = 0; i < packet.Header.AckList.Length; i++)
                    udpClient.NeedAcks.Acknowledge(packet.Header.AckList[i], now, packet.Header.Resent);
            }

            // Handle PacketAck packets
            if (packet.Type == PacketType.PacketAck)
            {
                PacketAckPacket ackPacket = (PacketAckPacket)packet;

                for (int i = 0; i < ackPacket.Packets.Length; i++)
                    udpClient.NeedAcks.Acknowledge(ackPacket.Packets[i].ID, now, packet.Header.Resent);

                // We don't need to do anything else with PacketAck packets
                return;
            }

            #endregion ACK Receiving

            #region ACK Sending

            if (packet.Header.Reliable)
            {
                udpClient.PendingAcks.Enqueue(packet.Header.Sequence);

                // This is a somewhat odd sequence of steps to pull the client.BytesSinceLastACK value out,
                // add the current received bytes to it, test if 2*MTU bytes have been sent, if so remove
                // 2*MTU bytes from the value and send ACKs, and finally add the local value back to
                // client.BytesSinceLastACK. Lockless thread safety
                int bytesSinceLastACK = Interlocked.Exchange(ref udpClient.BytesSinceLastACK, 0);
                bytesSinceLastACK += buffer.DataLength;
                if (bytesSinceLastACK > LLUDPServer.MTU * 2)
                {
                    bytesSinceLastACK -= LLUDPServer.MTU * 2;
                    SendAcks(udpClient);
                }
                Interlocked.Add(ref udpClient.BytesSinceLastACK, bytesSinceLastACK);
            }

            #endregion ACK Sending

            #region Incoming Packet Accounting

            // Check the archive of received reliable packet IDs to see whether we already received this packet
            if (packet.Header.Reliable && !udpClient.PacketArchive.TryEnqueue(packet.Header.Sequence))
            {
                if (packet.Header.Resent)
                    m_log.DebugFormat(
                        "[LLUDPSERVER]: Received a resend of already processed packet #{0}, type {1} from {2}", 
                        packet.Header.Sequence, packet.Type, client.Name);
                 else
                    m_log.WarnFormat(
                        "[LLUDPSERVER]: Received a duplicate (not marked as resend) of packet #{0}, type {1} from {2}",
                        packet.Header.Sequence, packet.Type, client.Name);

                // Avoid firing a callback twice for the same packet
                return;
            }

            #endregion Incoming Packet Accounting

            #region BinaryStats
            LogPacketHeader(true, udpClient.CircuitCode, 0, packet.Type, (ushort)packet.Length);
            #endregion BinaryStats

            #region Ping Check Handling

            if (packet.Type == PacketType.StartPingCheck)
            {
                // We don't need to do anything else with ping checks
                StartPingCheckPacket startPing = (StartPingCheckPacket)packet;
                CompletePing(udpClient, startPing.PingID.PingID);

                if ((Environment.TickCount - m_elapsedMSSinceLastStatReport) >= 3000)
                {
                    udpClient.SendPacketStats();
                    m_elapsedMSSinceLastStatReport = Environment.TickCount;
                }
                return;
            }
            else if (packet.Type == PacketType.CompletePingCheck)
            {
                // We don't currently track client ping times
                return;
            }

            #endregion Ping Check Handling

            // Inbox insertion
            packetInbox.Enqueue(new IncomingPacket((LLClientView)client, packet));
        }

        #region BinaryStats

        public class PacketLogger
        {
            public DateTime StartTime;
            public string Path = null;
            public System.IO.BinaryWriter Log = null;
        }

        public static PacketLogger PacketLog;

        protected static bool m_shouldCollectStats = false;
        // Number of seconds to log for
        static TimeSpan binStatsMaxFilesize = TimeSpan.FromSeconds(300);
        static object binStatsLogLock = new object();
        static string binStatsDir = "";

        public static void LogPacketHeader(bool incoming, uint circuit, byte flags, PacketType packetType, ushort size)
        {
            if (!m_shouldCollectStats) return;

            // Binary logging format is TTTTTTTTCCCCFPPPSS, T=Time, C=Circuit, F=Flags, P=PacketType, S=size

            // Put the incoming bit into the least significant bit of the flags byte
            if (incoming)
                flags |= 0x01;
            else
                flags &= 0xFE;

            // Put the flags byte into the most significant bits of the type integer
            uint type = (uint)packetType;
            type |= (uint)flags << 24;

            // m_log.Debug("1 LogPacketHeader(): Outside lock");
            lock (binStatsLogLock)
            {
                DateTime now = DateTime.Now;

                // m_log.Debug("2 LogPacketHeader(): Inside lock. now is " + now.Ticks);
                try
                {
                    if (PacketLog == null || (now > PacketLog.StartTime + binStatsMaxFilesize))
                    {
                        if (PacketLog != null && PacketLog.Log != null)
                        {
                            PacketLog.Log.Close();
                        }

                        // First log file or time has expired, start writing to a new log file
                        PacketLog = new PacketLogger();
                        PacketLog.StartTime = now;
                        PacketLog.Path = (binStatsDir.Length > 0 ? binStatsDir + System.IO.Path.DirectorySeparatorChar.ToString() : "")
                                + String.Format("packets-{0}.log", now.ToString("yyyyMMddHHmmss"));
                        PacketLog.Log = new BinaryWriter(File.Open(PacketLog.Path, FileMode.Append, FileAccess.Write));
                    }

                    // Serialize the data
                    byte[] output = new byte[18];
                    Buffer.BlockCopy(BitConverter.GetBytes(now.Ticks), 0, output, 0, 8);
                    Buffer.BlockCopy(BitConverter.GetBytes(circuit), 0, output, 8, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes(type), 0, output, 12, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes(size), 0, output, 16, 2);

                    // Write the serialized data to disk
                    if (PacketLog != null && PacketLog.Log != null)
                        PacketLog.Log.Write(output);
                }
                catch (Exception ex)
                {
                    m_log.Error("Packet statistics gathering failed: " + ex.Message, ex);
                    if (PacketLog.Log != null)
                    {
                        PacketLog.Log.Close();
                    }
                    PacketLog = null;
                }
            }
        }

        #endregion BinaryStats

        private void HandleUseCircuitCode(object o)
        {
            IPEndPoint remoteEndPoint = null;
            IClientAPI client = null;

            try
            {
    //            DateTime startTime = DateTime.Now;
                object[] array = (object[])o;
                UDPPacketBuffer buffer = (UDPPacketBuffer)array[0];
                UseCircuitCodePacket uccp = (UseCircuitCodePacket)array[1];

                m_log.DebugFormat(
                    "[LLUDPSERVER]: Handling UseCircuitCode request for circuit {0} to {1} from IP {2}",
                    uccp.CircuitCode.Code, m_scene.RegionInfo.RegionName, buffer.RemoteEndPoint);
    
                remoteEndPoint = (IPEndPoint)buffer.RemoteEndPoint;
    
                AuthenticateResponse sessionInfo;
                if (IsClientAuthorized(uccp, out sessionInfo))
                {
                    // Begin the process of adding the client to the simulator
                    client
                        = AddClient(
                            uccp.CircuitCode.Code,
                            uccp.CircuitCode.ID,
                            uccp.CircuitCode.SessionID,
                            remoteEndPoint,
                            sessionInfo);
            
                    // Send ack straight away to let the viewer know that the connection is active.
                    // The client will be null if it already exists (e.g. if on a region crossing the client sends a use
                    // circuit code to the existing child agent.  This is not particularly obvious.
                    SendAckImmediate(remoteEndPoint, uccp.Header.Sequence);
            
                    // We only want to send initial data to new clients, not ones which are being converted from child to root.
                    if (client != null)
                        client.SceneAgent.SendInitialDataToMe();
                }
                else
                {
                    // Don't create clients for unauthorized requesters.
                    m_log.WarnFormat(
                        "[LLUDPSERVER]: Ignoring connection request for {0} to {1} with unknown circuit code {2} from IP {3}",
                        uccp.CircuitCode.ID, m_scene.RegionInfo.RegionName, uccp.CircuitCode.Code, remoteEndPoint);
                }
    
                //            m_log.DebugFormat(
    //                "[LLUDPSERVER]: Handling UseCircuitCode request from {0} took {1}ms", 
    //                buffer.RemoteEndPoint, (DateTime.Now - startTime).Milliseconds);

            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[LLUDPSERVER]: UseCircuitCode handling from endpoint {0}, client {1} {2} failed.  Exception {3}{4}",
                    remoteEndPoint != null ? remoteEndPoint.ToString() : "n/a",
                    client != null ? client.Name : "unknown",
                    client != null ? client.AgentId.ToString() : "unknown",
                    e.Message,
                    e.StackTrace);
            }
        }

        /// <summary>
        /// Send an ack immediately to the given endpoint.
        /// </summary>
        /// <remarks>
        /// FIXME: Might be possible to use SendPacketData() like everything else, but this will require refactoring so
        /// that we can obtain the UDPClient easily at this point.
        /// </remarks>
        /// <param name="remoteEndpoint"></param>
        /// <param name="sequenceNumber"></param>
        private void SendAckImmediate(IPEndPoint remoteEndpoint, uint sequenceNumber)
        {
            PacketAckPacket ack = new PacketAckPacket();
            ack.Header.Reliable = false;
            ack.Packets = new PacketAckPacket.PacketsBlock[1];
            ack.Packets[0] = new PacketAckPacket.PacketsBlock();
            ack.Packets[0].ID = sequenceNumber;

            SendAckImmediate(remoteEndpoint, ack);
        }

        public virtual void SendAckImmediate(IPEndPoint remoteEndpoint, PacketAckPacket ack)
        {
            byte[] packetData = ack.ToBytes();
            int length = packetData.Length;

            UDPPacketBuffer buffer = new UDPPacketBuffer(remoteEndpoint, length);
            buffer.DataLength = length;

            Buffer.BlockCopy(packetData, 0, buffer.Data, 0, length);

            AsyncBeginSend(buffer);
        }

        private bool IsClientAuthorized(UseCircuitCodePacket useCircuitCode, out AuthenticateResponse sessionInfo)
        {
            UUID agentID = useCircuitCode.CircuitCode.ID;
            UUID sessionID = useCircuitCode.CircuitCode.SessionID;
            uint circuitCode = useCircuitCode.CircuitCode.Code;

            sessionInfo = m_circuitManager.AuthenticateSession(sessionID, agentID, circuitCode);
            return sessionInfo.Authorised;
        }

        /// <summary>
        /// Add a client.
        /// </summary>
        /// <param name="circuitCode"></param>
        /// <param name="agentID"></param>
        /// <param name="sessionID"></param>
        /// <param name="remoteEndPoint"></param>
        /// <param name="sessionInfo"></param>
        /// <returns>The client if it was added.  Null if the client already existed.</returns>
        protected virtual IClientAPI AddClient(
            uint circuitCode, UUID agentID, UUID sessionID, IPEndPoint remoteEndPoint, AuthenticateResponse sessionInfo)
        {
            IClientAPI client = null;

            // We currently synchronize this code across the whole scene to avoid issues such as
            // http://opensimulator.org/mantis/view.php?id=5365  However, once locking per agent circuit can be done
            // consistently, this lock could probably be removed.
            lock (this)
            {
                if (!m_scene.TryGetClient(agentID, out client))
                {
                    LLUDPClient udpClient = new LLUDPClient(this, ThrottleRates, m_throttle, circuitCode, agentID, remoteEndPoint, m_defaultRTO, m_maxRTO);
    
                    client = new LLClientView(m_scene, this, udpClient, sessionInfo, agentID, sessionID, circuitCode);
                    client.OnLogout += LogoutHandler;
    
                    ((LLClientView)client).DisableFacelights = m_disableFacelights;
    
                    client.Start();
                }
            }

            return client;
        }

        /// <summary>
        /// Deactivates the client if we don't receive any packets within a certain amount of time (default 60 seconds).
        /// </summary>
        /// <remarks>
        /// If a connection is active then we will always receive packets even if nothing else is happening, due to
        /// regular client pings.
        /// </remarks>
        /// <param name='client'></param>
        private void DeactivateClientDueToTimeout(LLClientView client)
        {
            lock (client.CloseSyncLock)
            {
                m_log.WarnFormat(
                    "[LLUDPSERVER]: Ack timeout, disconnecting {0} agent for {1} in {2}",
                    client.SceneAgent.IsChildAgent ? "child" : "root", client.Name, m_scene.RegionInfo.RegionName);
    
                StatsManager.SimExtraStats.AddAbnormalClientThreadTermination();
    
                if (!client.SceneAgent.IsChildAgent)
                     client.Kick("Simulator logged you out due to connection timeout");
    
                client.CloseWithoutChecks();
            }
        }

        private void IncomingPacketHandler()
        {
            // Set this culture for the thread that incoming packets are received
            // on to en-US to avoid number parsing issues
            Culture.SetCurrentCulture();

            while (base.IsRunning)
            {
                try
                {
                    IncomingPacket incomingPacket = null;

                    // HACK: This is a test to try and rate limit packet handling on Mono.
                    // If it works, a more elegant solution can be devised
                    if (Util.FireAndForgetCount() < 2)
                    {
                        //m_log.Debug("[LLUDPSERVER]: Incoming packet handler is sleeping");
                        Thread.Sleep(30);
                    }

                    if (packetInbox.Dequeue(100, ref incomingPacket))
                        ProcessInPacket(incomingPacket);//, incomingPacket); Util.FireAndForget(ProcessInPacket, incomingPacket);
                }
                catch (Exception ex)
                {
                    m_log.Error("[LLUDPSERVER]: Error in the incoming packet handler loop: " + ex.Message, ex);
                }

                Watchdog.UpdateThread();
            }

            if (packetInbox.Count > 0)
                m_log.Warn("[LLUDPSERVER]: IncomingPacketHandler is shutting down, dropping " + packetInbox.Count + " packets");
            packetInbox.Clear();

            Watchdog.RemoveThread();
        }

        private void OutgoingPacketHandler()
        {
            // Set this culture for the thread that outgoing packets are sent
            // on to en-US to avoid number parsing issues
            Culture.SetCurrentCulture();

            // Typecast the function to an Action<IClientAPI> once here to avoid allocating a new
            // Action generic every round
            Action<IClientAPI> clientPacketHandler = ClientOutgoingPacketHandler;

            while (base.IsRunning)
            {
                try
                {
                    m_packetSent = false;

                    #region Update Timers

                    m_resendUnacked = false;
                    m_sendAcks = false;
                    m_sendPing = false;

                    // Update elapsed time
                    int thisTick = Environment.TickCount & Int32.MaxValue;
                    if (m_tickLastOutgoingPacketHandler > thisTick)
                        m_elapsedMSOutgoingPacketHandler += ((Int32.MaxValue - m_tickLastOutgoingPacketHandler) + thisTick);
                    else
                        m_elapsedMSOutgoingPacketHandler += (thisTick - m_tickLastOutgoingPacketHandler);

                    m_tickLastOutgoingPacketHandler = thisTick;

                    // Check for pending outgoing resends every 100ms
                    if (m_elapsedMSOutgoingPacketHandler >= 100)
                    {
                        m_resendUnacked = true;
                        m_elapsedMSOutgoingPacketHandler = 0;
                        m_elapsed100MSOutgoingPacketHandler += 1;
                    }

                    // Check for pending outgoing ACKs every 500ms
                    if (m_elapsed100MSOutgoingPacketHandler >= 5)
                    {
                        m_sendAcks = true;
                        m_elapsed100MSOutgoingPacketHandler = 0;
                        m_elapsed500MSOutgoingPacketHandler += 1;
                    }

                    // Send pings to clients every 5000ms
                    if (m_elapsed500MSOutgoingPacketHandler >= 10)
                    {
                        m_sendPing = true;
                        m_elapsed500MSOutgoingPacketHandler = 0;
                    }

                    #endregion Update Timers

                    // Use this for emergency monitoring -- bug hunting
                    //if (m_scene.EmergencyMonitoring)
                    //    clientPacketHandler = MonitoredClientOutgoingPacketHandler;
                    //else
                    //    clientPacketHandler = ClientOutgoingPacketHandler;

                    // Handle outgoing packets, resends, acknowledgements, and pings for each
                    // client. m_packetSent will be set to true if a packet is sent
                    m_scene.ForEachClient(clientPacketHandler);

                    m_currentOutgoingClient = null;

                    // If nothing was sent, sleep for the minimum amount of time before a
                    // token bucket could get more tokens
                    if (!m_packetSent)
                        Thread.Sleep((int)TickCountResolution);

                    Watchdog.UpdateThread();
                }
                catch (Exception ex)
                {
                    m_log.Error("[LLUDPSERVER]: OutgoingPacketHandler loop threw an exception: " + ex.Message, ex);
                }
            }

            Watchdog.RemoveThread();
        }

        protected void ClientOutgoingPacketHandler(IClientAPI client)
        {
            m_currentOutgoingClient = client;

            try
            {
                if (client is LLClientView)
                {
                    LLClientView llClient = (LLClientView)client;
                    LLUDPClient udpClient = llClient.UDPClient;

                    if (udpClient.IsConnected)
                    {
                        if (m_resendUnacked)
                            HandleUnacked(llClient);

                        if (m_sendAcks)
                            SendAcks(udpClient);

                        if (m_sendPing)
                            SendPing(udpClient);

                        // Dequeue any outgoing packets that are within the throttle limits
                        if (udpClient.DequeueOutgoing())
                            m_packetSent = true;
                    }
                }
            }
            catch (Exception ex)
            {
                m_log.Error(
                    string.Format("[LLUDPSERVER]: OutgoingPacketHandler iteration for {0} threw ", client.Name), ex);
            }
        }

        #region Emergency Monitoring
        // Alternative packet handler fuull of instrumentation
        // Handy for hunting bugs
        private Stopwatch watch1 = new Stopwatch();
        private Stopwatch watch2 = new Stopwatch();

        private float avgProcessingTicks = 0;
        private float avgResendUnackedTicks = 0;
        private float avgSendAcksTicks = 0;
        private float avgSendPingTicks = 0;
        private float avgDequeueTicks = 0;
        private long nticks = 0;
        private long nticksUnack = 0;
        private long nticksAck = 0;
        private long nticksPing = 0;
        private int npacksSent = 0;
        private int npackNotSent = 0;

        private void MonitoredClientOutgoingPacketHandler(IClientAPI client)
        {
            nticks++;
            watch1.Start();
            m_currentOutgoingClient = client;

            try
            {
                if (client is LLClientView)
                {
                    LLClientView llClient = (LLClientView)client;
                    LLUDPClient udpClient = llClient.UDPClient;

                    if (udpClient.IsConnected)
                    {
                        if (m_resendUnacked)
                        {
                            nticksUnack++;
                            watch2.Start();

                            HandleUnacked(llClient);

                            watch2.Stop();
                            avgResendUnackedTicks = (nticksUnack - 1)/(float)nticksUnack * avgResendUnackedTicks + (watch2.ElapsedTicks / (float)nticksUnack);
                            watch2.Reset();
                        }

                        if (m_sendAcks)
                        {
                            nticksAck++;
                            watch2.Start();
                            
                            SendAcks(udpClient);

                            watch2.Stop();
                            avgSendAcksTicks = (nticksAck - 1) / (float)nticksAck * avgSendAcksTicks + (watch2.ElapsedTicks / (float)nticksAck);
                            watch2.Reset();
                        }

                        if (m_sendPing)
                        {
                            nticksPing++;
                            watch2.Start();
                            
                            SendPing(udpClient);

                            watch2.Stop();
                            avgSendPingTicks = (nticksPing - 1) / (float)nticksPing * avgSendPingTicks + (watch2.ElapsedTicks / (float)nticksPing);
                            watch2.Reset();
                        }

                        watch2.Start();
                        // Dequeue any outgoing packets that are within the throttle limits
                        if (udpClient.DequeueOutgoing())
                        {
                            m_packetSent = true;
                            npacksSent++;
                        }
                        else
                            npackNotSent++;

                        watch2.Stop();
                        avgDequeueTicks = (nticks - 1) / (float)nticks * avgDequeueTicks + (watch2.ElapsedTicks / (float)nticks);
                        watch2.Reset();

                    }
                    else
                        m_log.WarnFormat("[LLUDPSERVER]: Client is not connected");
                }
            }
            catch (Exception ex)
            {
                m_log.Error("[LLUDPSERVER]: OutgoingPacketHandler iteration for " + client.Name +
                    " threw an exception: " + ex.Message, ex);
            }
            watch1.Stop();
            avgProcessingTicks = (nticks - 1) / (float)nticks * avgProcessingTicks + (watch1.ElapsedTicks / (float)nticks);
            watch1.Reset();

            // reuse this -- it's every ~100ms
            if (m_scene.EmergencyMonitoring && nticks % 100 == 0)
            {
                m_log.InfoFormat("[LLUDPSERVER]: avg processing ticks: {0} avg unacked: {1} avg acks: {2} avg ping: {3} avg dequeue: {4} (TickCountRes: {5} sent: {6} notsent: {7})", 
                    avgProcessingTicks, avgResendUnackedTicks, avgSendAcksTicks, avgSendPingTicks, avgDequeueTicks, TickCountResolution, npacksSent, npackNotSent);
                npackNotSent = npacksSent = 0;
            }

        }

        #endregion 

        private void ProcessInPacket(IncomingPacket incomingPacket)
        {
            Packet packet = incomingPacket.Packet;
            LLClientView client = incomingPacket.Client;

            if (client.IsActive)
            {
                m_currentIncomingClient = client;

                try
                {
                    // Process this packet
                    client.ProcessInPacket(packet);
                }
                catch (ThreadAbortException)
                {
                    // If something is trying to abort the packet processing thread, take that as a hint that it's time to shut down
                    m_log.Info("[LLUDPSERVER]: Caught a thread abort, shutting down the LLUDP server");
                    Stop();
                }
                catch (Exception e)
                {
                    // Don't let a failure in an individual client thread crash the whole sim.
                    m_log.Error(
                        string.Format(
                            "[LLUDPSERVER]: Client packet handler for {0} for packet {1} threw ",
                            client.Name, packet.Type),
                        e);
                }
                finally
                {
                    m_currentIncomingClient = null;
                }
            }
            else
            {
                m_log.DebugFormat(
                    "[LLUDPSERVER]: Dropped incoming {0} for dead client {1} in {2}",
                    packet.Type, client.Name, m_scene.RegionInfo.RegionName);
            }
        }

        protected void LogoutHandler(IClientAPI client)
        {
            client.SendLogoutPacket();

            if (!client.IsLoggingOut)
            {
                client.IsLoggingOut = true;
                client.Close();
            }
        }
    }
}