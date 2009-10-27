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
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using log4net;
using Nini.Config;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Statistics;
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
        /// <summary>Number of terse prim updates to put on the queue each time the
        /// OnQueueEmpty event is triggered for updates</summary>
        public readonly int PrimTerseUpdatesPerPacket;
        /// <summary>Number of terse avatar updates to put on the queue each time the
        /// OnQueueEmpty event is triggered for updates</summary>
        public readonly int AvatarTerseUpdatesPerPacket;
        /// <summary>Number of full prim updates to put on the queue each time the
        /// OnQueueEmpty event is triggered for updates</summary>
        public readonly int PrimFullUpdatesPerPacket;
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
        protected ThrottleRates m_throttleRates;
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

        /// <summary>Environment.TickCount of the last time the outgoing packet handler executed</summary>
        private int m_tickLastOutgoingPacketHandler;
        /// <summary>Keeps track of the number of elapsed milliseconds since the last time the outgoing packet handler looped</summary>
        private int m_elapsedMSOutgoingPacketHandler;
        /// <summary>Keeps track of the number of 100 millisecond periods elapsed in the outgoing packet handler executed</summary>
        private int m_elapsed100MSOutgoingPacketHandler;
        /// <summary>Keeps track of the number of 500 millisecond periods elapsed in the outgoing packet handler executed</summary>
        private int m_elapsed500MSOutgoingPacketHandler;

        /// <summary>Flag to signal when clients should check for resends</summary>
        private bool m_resendUnacked;
        /// <summary>Flag to signal when clients should send ACKs</summary>
        private bool m_sendAcks;
        /// <summary>Flag to signal when clients should send pings</summary>
        private bool m_sendPing;

        public Socket Server { get { return null; } }

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
                m_asyncPacketHandling = config.GetBoolean("async_packet_handling", false);
                m_recvBufferSize = config.GetInt("client_socket_rcvbuf_size", 0);
                sceneThrottleBps = config.GetInt("scene_throttle_max_bps", 0);

                PrimTerseUpdatesPerPacket = config.GetInt("PrimTerseUpdatesPerPacket", 25);
                AvatarTerseUpdatesPerPacket = config.GetInt("AvatarTerseUpdatesPerPacket", 10);
                PrimFullUpdatesPerPacket = config.GetInt("PrimFullUpdatesPerPacket", 100);
                TextureSendLimit = config.GetInt("TextureSendLimit", 20);
            }
            else
            {
                PrimTerseUpdatesPerPacket = 25;
                AvatarTerseUpdatesPerPacket = 10;
                PrimFullUpdatesPerPacket = 100;
                TextureSendLimit = 20;
            }

            m_throttle = new TokenBucket(null, sceneThrottleBps, sceneThrottleBps);
            m_throttleRates = new ThrottleRates(configSource);
        }

        public void Start()
        {
            if (m_scene == null)
                throw new InvalidOperationException("[LLUDPSERVER]: Cannot LLUDPServer.Start() without an IScene reference");

            m_log.Info("[LLUDPSERVER]: Starting the LLUDP server in " + (m_asyncPacketHandling ? "asynchronous" : "synchronous") + " mode");

            base.Start(m_recvBufferSize, m_asyncPacketHandling);

            // Start the packet processing threads
            Watchdog.StartThread(IncomingPacketHandler, "Incoming Packets (" + m_scene.RegionInfo.RegionName + ")", ThreadPriority.Normal, false);
            Watchdog.StartThread(OutgoingPacketHandler, "Outgoing Packets (" + m_scene.RegionInfo.RegionName + ")", ThreadPriority.Normal, false);
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
            // CoarseLocationUpdate packets cannot be split in an automated way
            if (packet.Type == PacketType.CoarseLocationUpdate && allowSplitting)
                allowSplitting = false;

            if (allowSplitting && packet.HasVariableBlocks)
            {
                byte[][] datas = packet.ToBytesMultiple();
                int packetCount = datas.Length;

                //if (packetCount > 1)
                //    m_log.Debug("[LLUDPSERVER]: Split " + packet.Type + " packet into " + packetCount + " packets");

                for (int i = 0; i < packetCount; i++)
                {
                    byte[] data = datas[i];
                    m_scene.ForEachClient(
                        delegate(IClientAPI client)
                        {
                            if (client is LLClientView)
                                SendPacketData(((LLClientView)client).UDPClient, data, packet.Type, category);
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
                            SendPacketData(((LLClientView)client).UDPClient, data, packet.Type, category);
                    }
                );
            }
        }

        public void SendPacket(LLUDPClient udpClient, Packet packet, ThrottleOutPacketType category, bool allowSplitting)
        {
            // CoarseLocationUpdate packets cannot be split in an automated way
            if (packet.Type == PacketType.CoarseLocationUpdate && allowSplitting)
                allowSplitting = false;

            if (allowSplitting && packet.HasVariableBlocks)
            {
                byte[][] datas = packet.ToBytesMultiple();
                int packetCount = datas.Length;

                //if (packetCount > 1)
                //    m_log.Debug("[LLUDPSERVER]: Split " + packet.Type + " packet into " + packetCount + " packets");

                for (int i = 0; i < packetCount; i++)
                {
                    byte[] data = datas[i];
                    SendPacketData(udpClient, data, packet.Type, category);
                }
            }
            else
            {
                byte[] data = packet.ToBytes();
                SendPacketData(udpClient, data, packet.Type, category);
            }
        }

        public void SendPacketData(LLUDPClient udpClient, byte[] data, PacketType type, ThrottleOutPacketType category)
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
                    m_log.Error("[LLUDPSERVER]: Packet exceeded buffer size! This could be an indication of packet assembly not obeying the MTU. Type=" +
                        type + ", DataLength=" + dataLength + ", BufferLength=" + buffer.Data.Length + ". Dropping packet");
                    return;
                }
            }

            buffer.DataLength = dataLength;

            #region Queue or Send

            OutgoingPacket outgoingPacket = new OutgoingPacket(udpClient, buffer, category);

            if (!outgoingPacket.Client.EnqueueOutgoing(outgoingPacket))
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

                SendPacket(udpClient, packet, ThrottleOutPacketType.Unknown, true);
            }
        }

        public void SendPing(LLUDPClient udpClient)
        {
            StartPingCheckPacket pc = (StartPingCheckPacket)PacketPool.Instance.GetPacket(PacketType.StartPingCheck);
            pc.Header.Reliable = false;

            pc.PingID.PingID = (byte)udpClient.CurrentPingSequence++;
            // We *could* get OldestUnacked, but it would hurt performance and not provide any benefit
            pc.PingID.OldestUnacked = 0;

            SendPacket(udpClient, pc, ThrottleOutPacketType.Unknown, false);
        }

        public void ResendUnacked(LLUDPClient udpClient)
        {
            if (!udpClient.IsConnected)
                return;

            // Disconnect an agent if no packets are received for some time
            //FIXME: Make 60 an .ini setting
            if ((Environment.TickCount & Int32.MaxValue) - udpClient.TickLastPacketReceived > 1000 * 60)
            {
                m_log.Warn("[LLUDPSERVER]: Ack timeout, disconnecting " + udpClient.AgentID);

                RemoveClient(udpClient);
                return;
            }

            // Get a list of all of the packets that have been sitting unacked longer than udpClient.RTO
            List<OutgoingPacket> expiredPackets = udpClient.NeedAcks.GetExpiredPackets(udpClient.RTO);

            if (expiredPackets != null)
            {
                m_log.Debug("[LLUDPSERVER]: Resending " + expiredPackets.Count + " packets to " + udpClient.AgentID + ", RTO=" + udpClient.RTO);

                // Exponential backoff of the retransmission timeout
                udpClient.BackoffRTO();

                // Resend packets
                for (int i = 0; i < expiredPackets.Count; i++)
                {
                    OutgoingPacket outgoingPacket = expiredPackets[i];

                    //m_log.DebugFormat("[LLUDPSERVER]: Resending packet #{0} (attempt {1}), {2}ms have passed",
                    //    outgoingPacket.SequenceNumber, outgoingPacket.ResendCount, Environment.TickCount - outgoingPacket.TickCount);

                    // Set the resent flag
                    outgoingPacket.Buffer.Data[0] = (byte)(outgoingPacket.Buffer.Data[0] | Helpers.MSG_RESENT);
                    outgoingPacket.Category = ThrottleOutPacketType.Resend;

                    // Bump up the resend count on this packet
                    Interlocked.Increment(ref outgoingPacket.ResendCount);
                    //Interlocked.Increment(ref Stats.ResentPackets);

                    // Requeue or resend the packet
                    if (!outgoingPacket.Client.EnqueueOutgoing(outgoingPacket))
                        SendPacketFinal(outgoingPacket);
                }
            }
        }

        public void Flush(LLUDPClient udpClient)
        {
            // FIXME: Implement?
        }

        internal void SendPacketFinal(OutgoingPacket outgoingPacket)
        {
            UDPPacketBuffer buffer = outgoingPacket.Buffer;
            byte flags = buffer.Data[0];
            bool isResend = (flags & Helpers.MSG_RESENT) != 0;
            bool isReliable = (flags & Helpers.MSG_RELIABLE) != 0;
            LLUDPClient udpClient = outgoingPacket.Client;

            if (!udpClient.IsConnected)
                return;

            #region ACK Appending

            int dataLength = buffer.DataLength;

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

            #endregion Sequence Number Assignment

            // Stats tracking
            Interlocked.Increment(ref udpClient.PacketsSent);
            if (isReliable)
                Interlocked.Add(ref udpClient.UnackedBytes, outgoingPacket.Buffer.DataLength);

            // Put the UDP payload on the wire
            AsyncBeginSend(buffer);

            // Keep track of when this packet was sent out (right now)
            outgoingPacket.TickCount = Environment.TickCount & Int32.MaxValue;
        }

        protected override void PacketReceived(UDPPacketBuffer buffer)
        {
            // Debugging/Profiling
            //try { Thread.CurrentThread.Name = "PacketReceived (" + m_scene.RegionInfo.RegionName + ")"; }
            //catch (Exception) { }

            LLUDPClient udpClient = null;
            Packet packet = null;
            int packetEnd = buffer.DataLength - 1;
            IPEndPoint address = (IPEndPoint)buffer.RemoteEndPoint;

            #region Decoding

            try
            {
                packet = Packet.BuildPacket(buffer.Data, ref packetEnd,
                    // Only allocate a buffer for zerodecoding if the packet is zerocoded
                    ((buffer.Data[0] & Helpers.MSG_ZEROCODED) != 0) ? new byte[4096] : null);
            }
            catch (MalformedDataException)
            {
                m_log.ErrorFormat("[LLUDPSERVER]: Malformed data, cannot parse packet from {0}:\n{1}",
                    buffer.RemoteEndPoint, Utils.BytesToHexString(buffer.Data, buffer.DataLength, null));
            }

            // Fail-safe check
            if (packet == null)
            {
                m_log.Warn("[LLUDPSERVER]: Couldn't build a message from incoming data " + buffer.DataLength +
                    " bytes long from " + buffer.RemoteEndPoint);
                return;
            }

            #endregion Decoding

            #region Packet to Client Mapping

            // UseCircuitCode handling
            if (packet.Type == PacketType.UseCircuitCode)
            {
                m_log.Debug("[LLUDPSERVER]: Handling UseCircuitCode packet from " + buffer.RemoteEndPoint);
                object[] array = new object[] { buffer, packet };

                if (m_asyncPacketHandling)
                    Util.FireAndForget(HandleUseCircuitCode, array);
                else
                    HandleUseCircuitCode(array);

                return;
            }

            // Determine which agent this packet came from
            IClientAPI client;
            if (!m_scene.TryGetClient(address, out client) || !(client is LLClientView))
            {
                m_log.Debug("[LLUDPSERVER]: Received a " + packet.Type + " packet from an unrecognized source: " + address + " in " + m_scene.RegionInfo.RegionName);
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
                    udpClient.NeedAcks.Remove(packet.Header.AckList[i], now, packet.Header.Resent);
            }

            // Handle PacketAck packets
            if (packet.Type == PacketType.PacketAck)
            {
                PacketAckPacket ackPacket = (PacketAckPacket)packet;

                for (int i = 0; i < ackPacket.Packets.Length; i++)
                    udpClient.NeedAcks.Remove(ackPacket.Packets[i].ID, now, packet.Header.Resent);

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
                    m_log.Debug("[LLUDPSERVER]: Received a resend of already processed packet #" + packet.Header.Sequence + ", type: " + packet.Type);
                else
                    m_log.Warn("[LLUDPSERVER]: Received a duplicate (not marked as resend) of packet #" + packet.Header.Sequence + ", type: " + packet.Type);

                // Avoid firing a callback twice for the same packet
                return;
            }

            #endregion Incoming Packet Accounting

            #region Ping Check Handling

            if (packet.Type == PacketType.StartPingCheck)
            {
                // We don't need to do anything else with ping checks
                StartPingCheckPacket startPing = (StartPingCheckPacket)packet;

                CompletePingCheckPacket completePing = new CompletePingCheckPacket();
                completePing.PingID.PingID = startPing.PingID.PingID;
                SendPacket(udpClient, completePing, ThrottleOutPacketType.Unknown, false);
                return;
            }
            else if (packet.Type == PacketType.CompletePingCheck)
            {
                // We don't currently track client ping times
                return;
            }

            #endregion Ping Check Handling

            // Inbox insertion
            packetInbox.Enqueue(new IncomingPacket(udpClient, packet));
        }

        private void HandleUseCircuitCode(object o)
        {
            object[] array = (object[])o;
            UDPPacketBuffer buffer = (UDPPacketBuffer)array[0];
            UseCircuitCodePacket packet = (UseCircuitCodePacket)array[1];

            IPEndPoint remoteEndPoint = (IPEndPoint)buffer.RemoteEndPoint;

            // Begin the process of adding the client to the simulator
            AddNewClient((UseCircuitCodePacket)packet, remoteEndPoint);

            // Acknowledge the UseCircuitCode packet
            SendAckImmediate(remoteEndPoint, packet.Header.Sequence);
        }

        private void SendAckImmediate(IPEndPoint remoteEndpoint, uint sequenceNumber)
        {
            PacketAckPacket ack = new PacketAckPacket();
            ack.Header.Reliable = false;
            ack.Packets = new PacketAckPacket.PacketsBlock[1];
            ack.Packets[0] = new PacketAckPacket.PacketsBlock();
            ack.Packets[0].ID = sequenceNumber;

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

        private void AddNewClient(UseCircuitCodePacket useCircuitCode, IPEndPoint remoteEndPoint)
        {
            UUID agentID = useCircuitCode.CircuitCode.ID;
            UUID sessionID = useCircuitCode.CircuitCode.SessionID;
            uint circuitCode = useCircuitCode.CircuitCode.Code;

            if (m_scene.RegionStatus != RegionStatus.SlaveScene)
            {
                AuthenticateResponse sessionInfo;
                if (IsClientAuthorized(useCircuitCode, out sessionInfo))
                {
                    AddClient(circuitCode, agentID, sessionID, remoteEndPoint, sessionInfo);
                }
                else
                {
                    // Don't create circuits for unauthorized clients
                    m_log.WarnFormat(
                        "[LLUDPSERVER]: Connection request for client {0} connecting with unnotified circuit code {1} from {2}",
                        useCircuitCode.CircuitCode.ID, useCircuitCode.CircuitCode.Code, remoteEndPoint);
                }
            }
            else
            {
                // Slave regions don't accept new clients
                m_log.Debug("[LLUDPSERVER]: Slave region " + m_scene.RegionInfo.RegionName + " ignoring UseCircuitCode packet");
            }
        }

        protected virtual void AddClient(uint circuitCode, UUID agentID, UUID sessionID, IPEndPoint remoteEndPoint, AuthenticateResponse sessionInfo)
        {
            // Create the LLUDPClient
            LLUDPClient udpClient = new LLUDPClient(this, m_throttleRates, m_throttle, circuitCode, agentID, remoteEndPoint);
            IClientAPI existingClient;

            if (!m_scene.TryGetClient(agentID, out existingClient))
            {
                // Create the LLClientView
                LLClientView client = new LLClientView(remoteEndPoint, m_scene, this, udpClient, sessionInfo, agentID, sessionID, circuitCode);
                client.OnLogout += LogoutHandler;

                // Start the IClientAPI
                client.Start();
            }
            else
            {
                m_log.WarnFormat("[LLUDPSERVER]: Ignoring a repeated UseCircuitCode from {0} at {1} for circuit {2}",
                    udpClient.AgentID, remoteEndPoint, circuitCode);
            }
        }

        private void RemoveClient(LLUDPClient udpClient)
        {
            // Remove this client from the scene
            IClientAPI client;
            if (m_scene.TryGetClient(udpClient.AgentID, out client))
                client.Close();
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
                        Util.FireAndForget(ProcessInPacket, incomingPacket);
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

                    // Handle outgoing packets, resends, acknowledgements, and pings for each
                    // client. m_packetSent will be set to true if a packet is sent
                    m_scene.ForEachClient(clientPacketHandler, false);

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

        private void ClientOutgoingPacketHandler(IClientAPI client)
        {
            try
            {
                if (client is LLClientView)
                {
                    LLUDPClient udpClient = ((LLClientView)client).UDPClient;

                    if (udpClient.IsConnected)
                    {
                        if (m_resendUnacked)
                            ResendUnacked(udpClient);

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
                m_log.Error("[LLUDPSERVER]: OutgoingPacketHandler iteration for " + client.Name +
                    " threw an exception: " + ex.Message, ex);
            }
        }

        private void ProcessInPacket(object state)
        {
            IncomingPacket incomingPacket = (IncomingPacket)state;
            Packet packet = incomingPacket.Packet;
            LLUDPClient udpClient = incomingPacket.Client;
            IClientAPI client;

            // Sanity check
            if (packet == null || udpClient == null)
            {
                m_log.WarnFormat("[LLUDPSERVER]: Processing a packet with incomplete state. Packet=\"{0}\", UDPClient=\"{1}\"",
                    packet, udpClient);
            }

            // Make sure this client is still alive
            if (m_scene.TryGetClient(udpClient.AgentID, out client))
            {
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
                    m_log.ErrorFormat("[LLUDPSERVER]: Client packet handler for {0} for packet {1} threw an exception", udpClient.AgentID, packet.Type);
                    m_log.Error(e.Message, e);
                }
            }
            else
            {
                m_log.DebugFormat("[LLUDPSERVER]: Dropping incoming {0} packet for dead client {1}", packet.Type, udpClient.AgentID);
            }
        }

        protected void LogoutHandler(IClientAPI client)
        {
            client.SendLogoutPacket();
            if (client.IsActive)
                RemoveClient(((LLClientView)client).UDPClient);
        }
    }
}
