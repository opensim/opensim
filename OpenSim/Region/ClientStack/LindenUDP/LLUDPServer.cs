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
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>Handlers for incoming packets</summary>
        //PacketEventDictionary packetEvents = new PacketEventDictionary();
        /// <summary>Incoming packets that are awaiting handling</summary>
        private OpenMetaverse.BlockingQueue<IncomingPacket> packetInbox = new OpenMetaverse.BlockingQueue<IncomingPacket>();
        /// <summary></summary>
        //private UDPClientCollection m_clients = new UDPClientCollection();
        /// <summary>Bandwidth throttle for this UDP server</summary>
        private TokenBucket m_throttle;
        /// <summary>Bandwidth throttle rates for this UDP server</summary>
        private ThrottleRates m_throttleRates;
        /// <summary>Manages authentication for agent circuits</summary>
        private AgentCircuitManager m_circuitManager;
        /// <summary>Reference to the scene this UDP server is attached to</summary>
        private IScene m_scene;
        /// <summary>The X/Y coordinates of the scene this UDP server is attached to</summary>
        private Location m_location;
        /// <summary>The measured resolution of Environment.TickCount</summary>
        private float m_tickCountResolution;
        /// <summary>The size of the receive buffer for the UDP socket. This value
        /// is passed up to the operating system and used in the system networking
        /// stack. Use zero to leave this value as the default</summary>
        private int m_recvBufferSize;

        /// <summary>The measured resolution of Environment.TickCount</summary>
        public float TickCountResolution { get { return m_tickCountResolution; } }
        public Socket Server { get { return null; } }

        public LLUDPServer(IPAddress listenIP, ref uint port, int proxyPortOffsetParm, bool allow_alternate_port, IConfigSource configSource, AgentCircuitManager circuitManager)
            : base(listenIP, (int)port)
        {
            #region Environment.TickCount Measurement

            // Measure the resolution of Environment.TickCount
            m_tickCountResolution = 0f;
            for (int i = 0; i < 5; i++)
            {
                int start = Environment.TickCount;
                int now = start;
                while (now == start)
                    now = Environment.TickCount;
                m_tickCountResolution += (float)(now - start) * 0.2f;
            }
            m_log.Info("[LLUDPSERVER]: Average Environment.TickCount resolution: " + TickCountResolution + "ms");

            #endregion Environment.TickCount Measurement

            m_circuitManager = circuitManager;

            IConfig config = configSource.Configs["ClientStack.LindenUDP"];
            if (config != null)
            {
                m_recvBufferSize = config.GetInt("client_socket_rcvbuf_size", 0);
            }

            // TODO: Config support for throttling the entire connection
            m_throttle = new TokenBucket(null, 0, 0);
            m_throttleRates = new ThrottleRates(configSource);
        }

        public new void Start()
        {
            if (m_scene == null)
                throw new InvalidOperationException("[LLUDPSERVER]: Cannot LLUDPServer.Start() without an IScene reference");

            base.Start(m_recvBufferSize);

            // Start the incoming packet processing thread
            Thread incomingThread = new Thread(IncomingPacketHandler);
            incomingThread.Name = "Incoming Packets (" + m_scene.RegionInfo.RegionName + ")";
            incomingThread.Start();

            Thread outgoingThread = new Thread(OutgoingPacketHandler);
            outgoingThread.Name = "Outgoing Packets (" + m_scene.RegionInfo.RegionName + ")";
            outgoingThread.Start();
        }

        public new void Stop()
        {
            m_log.Info("[LLUDPSERVER]: Shutting down the LLUDP server for " + m_scene.RegionInfo.RegionName);
            base.Stop();
        }

        public void AddScene(IScene scene)
        {
            if (m_scene == null)
            {
                m_scene = scene;
                m_location = new Location(m_scene.RegionInfo.RegionHandle);
            }
            else
            {
                m_log.Error("[LLUDPSERVER]: AddScene() called on an LLUDPServer that already has a scene");
            }
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
                    m_scene.ClientManager.ForEach(
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
                m_scene.ClientManager.ForEach(
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

            // Frequency analysis of outgoing packet sizes shows a large clump of packets at each end of the spectrum.
            // The vast majority of packets are less than 200 bytes, although due to asset transfers and packet splitting
            // there are a decent number of packets in the 1000-1140 byte range. We allocate one of two sizes of data here
            // to accomodate for both common scenarios and provide ample room for ACK appending in both
            int bufferSize = (dataLength > 180) ? Packet.MTU : 200;

            UDPPacketBuffer buffer = new UDPPacketBuffer(udpClient.RemoteEndPoint, bufferSize);

            // Zerocode if needed
            if (doZerocode)
            {
                try { dataLength = Helpers.ZeroEncode(data, dataLength, buffer.Data); }
                catch (IndexOutOfRangeException)
                {
                    // The packet grew larger than the bufferSize while zerocoding.
                    // Remove the MSG_ZEROCODED flag and send the unencoded data
                    // instead
                    m_log.Debug("[LLUDPSERVER]: Packet exceeded buffer size during zerocoding for " + type + ". Removing MSG_ZEROCODED flag");
                    data[0] = (byte)(data[0] & ~Helpers.MSG_ZEROCODED);
                    Buffer.BlockCopy(data, 0, buffer.Data, 0, dataLength);
                }
            }
            else
            {
                Buffer.BlockCopy(data, 0, buffer.Data, 0, dataLength);
            }
            buffer.DataLength = dataLength;

            #region Queue or Send

            // Look up the UDPClient this is going to
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

            OutgoingPacket oldestPacket = udpClient.NeedAcks.GetOldest();

            pc.PingID.PingID = (byte)udpClient.CurrentPingSequence++;
            pc.PingID.OldestUnacked = (oldestPacket != null) ? oldestPacket.SequenceNumber : 0;

            SendPacket(udpClient, pc, ThrottleOutPacketType.Unknown, false);
        }

        public void ResendUnacked(LLUDPClient udpClient)
        {
            if (udpClient.IsConnected && udpClient.NeedAcks.Count > 0)
            {
                // Disconnect an agent if no packets are received for some time
                //FIXME: Make 60 an .ini setting
                if (Environment.TickCount - udpClient.TickLastPacketReceived > 1000 * 60)
                {
                    m_log.Warn("[LLUDPSERVER]: Ack timeout, disconnecting " + udpClient.AgentID);

                    RemoveClient(udpClient);
                    return;
                }

                // Get a list of all of the packets that have been sitting unacked longer than udpClient.RTO
                List<OutgoingPacket> expiredPackets = udpClient.NeedAcks.GetExpiredPackets(udpClient.RTO);

                if (expiredPackets != null)
                {
                    // Resend packets
                    for (int i = 0; i < expiredPackets.Count; i++)
                    {
                        OutgoingPacket outgoingPacket = expiredPackets[i];

                        //m_log.DebugFormat("[LLUDPSERVER]: Resending packet #{0} (attempt {1}), {2}ms have passed",
                        //    outgoingPacket.SequenceNumber, outgoingPacket.ResendCount, Environment.TickCount - outgoingPacket.TickCount);

                        // Set the resent flag
                        outgoingPacket.Buffer.Data[0] = (byte)(outgoingPacket.Buffer.Data[0] | Helpers.MSG_RESENT);
                        outgoingPacket.Category = ThrottleOutPacketType.Resend;

                        // The TickCount will be set to the current time when the packet
                        // is actually sent out again
                        outgoingPacket.TickCount = 0;

                        // Bump up the resend count on this packet
                        Interlocked.Increment(ref outgoingPacket.ResendCount);
                        //Interlocked.Increment(ref Stats.ResentPackets);

                        // Requeue or resend the packet
                        if (!outgoingPacket.Client.EnqueueOutgoing(outgoingPacket))
                            SendPacketFinal(outgoingPacket);
                    }
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

            // Keep track of when this packet was sent out (right now)
            outgoingPacket.TickCount = Environment.TickCount;

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
                AddNewClient((UseCircuitCodePacket)packet, (IPEndPoint)buffer.RemoteEndPoint);
            }

            // Determine which agent this packet came from
            IClientAPI client;
            if (!m_scene.ClientManager.TryGetValue(address, out client) || !(client is LLClientView))
            {
                m_log.Warn("[LLUDPSERVER]: Received a " + packet.Type + " packet from an unrecognized source: " + address +
                    " in " + m_scene.RegionInfo.RegionName + ", currently tracking " + m_scene.ClientManager.Count + " clients");
                return;
            }

            udpClient = ((LLClientView)client).UDPClient;

            if (!udpClient.IsConnected)
                return;

            #endregion Packet to Client Mapping

            // Stats tracking
            Interlocked.Increment(ref udpClient.PacketsReceived);

            #region ACK Receiving

            int now = Environment.TickCount;
            udpClient.TickLastPacketReceived = now;

            // Handle appended ACKs
            if (packet.Header.AppendedAcks && packet.Header.AckList != null)
            {
                lock (udpClient.NeedAcks.SyncRoot)
                {
                    for (int i = 0; i < packet.Header.AckList.Length; i++)
                        AcknowledgePacket(udpClient, packet.Header.AckList[i], now, packet.Header.Resent);
                }
            }

            // Handle PacketAck packets
            if (packet.Type == PacketType.PacketAck)
            {
                PacketAckPacket ackPacket = (PacketAckPacket)packet;

                lock (udpClient.NeedAcks.SyncRoot)
                {
                    for (int i = 0; i < ackPacket.Packets.Length; i++)
                        AcknowledgePacket(udpClient, ackPacket.Packets[i].ID, now, packet.Header.Resent);
                }
            }

            #endregion ACK Receiving

            #region ACK Sending

            if (packet.Header.Reliable)
                udpClient.PendingAcks.Enqueue(packet.Header.Sequence);

            // This is a somewhat odd sequence of steps to pull the client.BytesSinceLastACK value out,
            // add the current received bytes to it, test if 2*MTU bytes have been sent, if so remove
            // 2*MTU bytes from the value and send ACKs, and finally add the local value back to
            // client.BytesSinceLastACK. Lockless thread safety
            int bytesSinceLastACK = Interlocked.Exchange(ref udpClient.BytesSinceLastACK, 0);
            bytesSinceLastACK += buffer.DataLength;
            if (bytesSinceLastACK > Packet.MTU * 2)
            {
                bytesSinceLastACK -= Packet.MTU * 2;
                SendAcks(udpClient);
            }
            Interlocked.Add(ref udpClient.BytesSinceLastACK, bytesSinceLastACK);

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

            // Don't bother clogging up the queue with PacketAck packets that are already handled here
            if (packet.Type != PacketType.PacketAck)
            {
                // Inbox insertion
                packetInbox.Enqueue(new IncomingPacket(udpClient, packet));
            }
        }

        protected override void PacketSent(UDPPacketBuffer buffer, int bytesSent)
        {
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

        private void AddClient(uint circuitCode, UUID agentID, UUID sessionID, IPEndPoint remoteEndPoint, AuthenticateResponse sessionInfo)
        {
            // Create the LLUDPClient
            LLUDPClient udpClient = new LLUDPClient(this, m_throttleRates, m_throttle, circuitCode, agentID, remoteEndPoint);

            if (!m_scene.ClientManager.ContainsKey(agentID))
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
            if (m_scene.ClientManager.TryGetValue(udpClient.AgentID, out client))
                client.Close();
        }

        private void AcknowledgePacket(LLUDPClient client, uint ack, int currentTime, bool fromResend)
        {
            OutgoingPacket ackedPacket;
            if (client.NeedAcks.RemoveUnsafe(ack, out ackedPacket) && !fromResend)
            {
                // Update stats
                Interlocked.Add(ref client.UnackedBytes, -ackedPacket.Buffer.DataLength);

                // Calculate the round-trip time for this packet and its ACK
                int rtt = currentTime - ackedPacket.TickCount;
                if (rtt > 0)
                    client.UpdateRoundTrip(rtt);
            }
        }

        private void IncomingPacketHandler()
        {
            // Set this culture for the thread that incoming packets are received
            // on to en-US to avoid number parsing issues
            Culture.SetCurrentCulture();

            IncomingPacket incomingPacket = null;

            while (base.IsRunning)
            {
                if (packetInbox.Dequeue(100, ref incomingPacket))
                    Util.FireAndForget(ProcessInPacket, incomingPacket);
            }

            if (packetInbox.Count > 0)
                m_log.Warn("[LLUDPSERVER]: IncomingPacketHandler is shutting down, dropping " + packetInbox.Count + " packets");
            packetInbox.Clear();
        }

        private void OutgoingPacketHandler()
        {
            // Set this culture for the thread that outgoing packets are sent
            // on to en-US to avoid number parsing issues
            Culture.SetCurrentCulture();

            int now = Environment.TickCount;
            int elapsedMS = 0;
            int elapsed100MS = 0;
            int elapsed500MS = 0;

            while (base.IsRunning)
            {
                bool resendUnacked = false;
                bool sendAcks = false;
                bool sendPings = false;
                bool packetSent = false;

                elapsedMS += Environment.TickCount - now;

                // Check for pending outgoing resends every 100ms
                if (elapsedMS >= 100)
                {
                    resendUnacked = true;
                    elapsedMS -= 100;
                    ++elapsed100MS;
                }
                // Check for pending outgoing ACKs every 500ms
                if (elapsed100MS >= 5)
                {
                    sendAcks = true;
                    elapsed100MS = 0;
                    ++elapsed500MS;
                }
                // Send pings to clients every 5000ms
                if (elapsed500MS >= 10)
                {
                    sendPings = true;
                    elapsed500MS = 0;
                }

                m_scene.ClientManager.ForEach(
                    delegate(IClientAPI client)
                    {
                        if (client is LLClientView)
                        {
                            LLUDPClient udpClient = ((LLClientView)client).UDPClient;

                            if (udpClient.IsConnected)
                            {
                                if (udpClient.DequeueOutgoing())
                                    packetSent = true;
                                if (resendUnacked)
                                    ResendUnacked(udpClient);
                                if (sendAcks)
                                {
                                    SendAcks(udpClient);
                                    udpClient.SendPacketStats();
                                }
                                if (sendPings)
                                    SendPing(udpClient);
                            }
                        }
                    }
                );

                if (!packetSent)
                    Thread.Sleep(20);
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
            if (m_scene.ClientManager.TryGetValue(udpClient.AgentID, out client))
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

        private void LogoutHandler(IClientAPI client)
        {
            client.SendLogoutPacket();
        }
    }
}
