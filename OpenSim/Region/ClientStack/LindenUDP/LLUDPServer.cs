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
        private UDPClientCollection clients = new UDPClientCollection();
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

        /// <summary>The measured resolution of Environment.TickCount</summary>
        public float TickCountResolution { get { return m_tickCountResolution; } }
        public Socket Server { get { return null; } }

        public LLUDPServer(IPAddress listenIP, ref uint port, int proxyPortOffsetParm, bool allow_alternate_port, IConfigSource configSource, AgentCircuitManager circuitManager)
            : base((int)port)
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

            // TODO: Config support for throttling the entire connection
            m_throttle = new TokenBucket(null, 0, 0);
            m_throttleRates = new ThrottleRates(configSource);
        }

        public new void Start()
        {
            if (m_scene == null)
                throw new InvalidOperationException("Cannot LLUDPServer.Start() without an IScene reference");

            base.Start();

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

        public void RemoveClient(IClientAPI client)
        {
            m_scene.ClientManager.Remove(client.CircuitCode);
            client.Close(false);

            LLUDPClient udpClient;
            if (clients.TryGetValue(client.AgentId, out udpClient))
            {
                m_log.Debug("[LLUDPSERVER]: Removing LLUDPClient for " + client.Name);
                udpClient.Shutdown();
                clients.Remove(client.AgentId, udpClient.RemoteEndPoint);
            }
            else
            {
                m_log.Warn("[LLUDPSERVER]: Failed to remove LLUDPClient for " + client.Name);
            }
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
                    clients.ForEach(
                        delegate(LLUDPClient client)
                        { SendPacketData(client, data, data.Length, packet.Type, packet.Header.Zerocoded, category); });
                }
            }
            else
            {
                byte[] data = packet.ToBytes();
                clients.ForEach(
                    delegate(LLUDPClient client)
                    { SendPacketData(client, data, data.Length, packet.Type, packet.Header.Zerocoded, category); });
            }
        }

        public void SendPacket(UUID agentID, Packet packet, ThrottleOutPacketType category, bool allowSplitting)
        {
            LLUDPClient client;
            if (clients.TryGetValue(agentID, out client))
                SendPacket(client, packet, category, allowSplitting);
            else
                m_log.Warn("[LLUDPSERVER]: Attempted to send a packet to unknown agentID " + agentID);
        }

        public void SendPacket(LLUDPClient client, Packet packet, ThrottleOutPacketType category, bool allowSplitting)
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
                    SendPacketData(client, data, data.Length, packet.Type, packet.Header.Zerocoded, category);
                }
            }
            else
            {
                byte[] data = packet.ToBytes();
                SendPacketData(client, data, data.Length, packet.Type, packet.Header.Zerocoded, category);
            }
        }

        public void SendPacketData(LLUDPClient client, byte[] data, int dataLength, PacketType type, bool doZerocode, ThrottleOutPacketType category)
        {
            // Frequency analysis of outgoing packet sizes shows a large clump of packets at each end of the spectrum.
            // The vast majority of packets are less than 200 bytes, although due to asset transfers and packet splitting
            // there are a decent number of packets in the 1000-1140 byte range. We allocate one of two sizes of data here
            // to accomodate for both common scenarios and provide ample room for ACK appending in both
            int bufferSize = (dataLength > 180) ? Packet.MTU : 200;

            UDPPacketBuffer buffer = new UDPPacketBuffer(client.RemoteEndPoint, bufferSize);

            // Zerocode if needed
            if (doZerocode)
            {
                try { dataLength = Helpers.ZeroEncode(data, dataLength, buffer.Data); }
                catch (IndexOutOfRangeException)
                {
                    // The packet grew larger than the bufferSize while zerocoding.
                    // Remove the MSG_ZEROCODED flag and send the unencoded data
                    // instead
                    m_log.Info("[LLUDPSERVER]: Packet exceeded buffer size during zerocoding. Removing MSG_ZEROCODED flag");
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
            OutgoingPacket outgoingPacket = new OutgoingPacket(client, buffer, category);

            if (!outgoingPacket.Client.EnqueueOutgoing(outgoingPacket))
                SendPacketFinal(outgoingPacket);

            #endregion Queue or Send
        }

        public void SendAcks(LLUDPClient client)
        {
            uint ack;

            if (client.PendingAcks.Dequeue(out ack))
            {
                List<PacketAckPacket.PacketsBlock> blocks = new List<PacketAckPacket.PacketsBlock>();
                PacketAckPacket.PacketsBlock block = new PacketAckPacket.PacketsBlock();
                block.ID = ack;
                blocks.Add(block);

                while (client.PendingAcks.Dequeue(out ack))
                {
                    block = new PacketAckPacket.PacketsBlock();
                    block.ID = ack;
                    blocks.Add(block);
                }

                PacketAckPacket packet = new PacketAckPacket();
                packet.Header.Reliable = false;
                packet.Packets = blocks.ToArray();

                SendPacket(client, packet, ThrottleOutPacketType.Unknown, true);
            }
        }

        public void SendPing(LLUDPClient client)
        {
            IClientAPI api = client.ClientAPI;
            if (api != null)
                api.SendStartPingCheck(client.CurrentPingSequence++);
        }

        public void ResendUnacked(LLUDPClient client)
        {
            if (client.NeedAcks.Count > 0)
            {
                List<OutgoingPacket> expiredPackets = client.NeedAcks.GetExpiredPackets(client.RTO);

                if (expiredPackets != null)
                {
                    // Resend packets
                    for (int i = 0; i < expiredPackets.Count; i++)
                    {
                        OutgoingPacket outgoingPacket = expiredPackets[i];

                        // FIXME: Make this an .ini setting
                        if (outgoingPacket.ResendCount < 3)
                        {
                            //Logger.Debug(String.Format("Resending packet #{0} (attempt {1}), {2}ms have passed",
                            //    outgoingPacket.SequenceNumber, outgoingPacket.ResendCount, Environment.TickCount - outgoingPacket.TickCount));

                            // Set the resent flag
                            outgoingPacket.Buffer.Data[0] = (byte)(outgoingPacket.Buffer.Data[0] | Helpers.MSG_RESENT);
                            outgoingPacket.Category = ThrottleOutPacketType.Resend;

                            // The TickCount will be set to the current time when the packet
                            // is actually sent out again
                            outgoingPacket.TickCount = 0;

                            // Bump up the resend count on this packet
                            Interlocked.Increment(ref outgoingPacket.ResendCount);
                            //Interlocked.Increment(ref Stats.ResentPackets);

                            // Queue or (re)send the packet
                            if (!outgoingPacket.Client.EnqueueOutgoing(outgoingPacket))
                                SendPacketFinal(outgoingPacket);
                        }
                        else
                        {
                            m_log.DebugFormat("[LLUDPSERVER]: Dropping packet #{0} for agent {1} after {2} failed attempts",
                                outgoingPacket.SequenceNumber, outgoingPacket.Client.RemoteEndPoint, outgoingPacket.ResendCount);

                            lock (client.NeedAcks.SyncRoot)
                                client.NeedAcks.RemoveUnsafe(outgoingPacket.SequenceNumber);

                            //Interlocked.Increment(ref Stats.DroppedPackets);

                            // Disconnect an agent if no packets are received for some time
                            //FIXME: Make 60 an .ini setting
                            if (Environment.TickCount - client.TickLastPacketReceived > 1000 * 60)
                            {
                                m_log.Warn("[LLUDPSERVER]: Ack timeout, disconnecting " + client.ClientAPI.Name);

                                RemoveClient(client.ClientAPI);
                                return;
                            }
                        }
                    }
                }
            }
        }

        public void Flush()
        {
            // FIXME: Implement?
        }

        internal void SendPacketFinal(OutgoingPacket outgoingPacket)
        {
            UDPPacketBuffer buffer = outgoingPacket.Buffer;
            byte flags = buffer.Data[0];
            bool isResend = (flags & Helpers.MSG_RESENT) != 0;
            bool isReliable = (flags & Helpers.MSG_RELIABLE) != 0;
            LLUDPClient client = outgoingPacket.Client;

            // Keep track of when this packet was sent out (right now)
            outgoingPacket.TickCount = Environment.TickCount;

            #region ACK Appending

            int dataLength = buffer.DataLength;

            // Keep appending ACKs until there is no room left in the packet or there are
            // no more ACKs to append
            uint ackCount = 0;
            uint ack;
            while (dataLength + 5 < buffer.Data.Length && client.PendingAcks.Dequeue(out ack))
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

            if (!isResend)
            {
                // Not a resend, assign a new sequence number
                uint sequenceNumber = (uint)Interlocked.Increment(ref client.CurrentSequence);
                Utils.UIntToBytesBig(sequenceNumber, buffer.Data, 1);
                outgoingPacket.SequenceNumber = sequenceNumber;

                if (isReliable)
                {
                    // Add this packet to the list of ACK responses we are waiting on from the server
                    client.NeedAcks.Add(outgoingPacket);
                }
            }

            // Stats tracking
            Interlocked.Increment(ref client.PacketsSent);
            if (isReliable)
                Interlocked.Add(ref client.UnackedBytes, outgoingPacket.Buffer.DataLength);

            // Put the UDP payload on the wire
            AsyncBeginSend(buffer);
        }

        protected override void PacketReceived(UDPPacketBuffer buffer)
        {
            // Debugging/Profiling
            //try { Thread.CurrentThread.Name = "PacketReceived (" + scene.RegionName + ")"; }
            //catch (Exception) { }

            LLUDPClient client = null;
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
                m_log.ErrorFormat("[LLUDPSERVER]: Malformed data, cannot parse packet:\n{0}",
                    Utils.BytesToHexString(buffer.Data, buffer.DataLength, null));
            }

            // Fail-safe check
            if (packet == null)
            {
                m_log.Warn("[LLUDPSERVER]: Couldn't build a message from the incoming data");
                return;
            }

            //Stats.RecvBytes += (ulong)buffer.DataLength;
            //++Stats.RecvPackets;

            #endregion Decoding

            #region UseCircuitCode Handling

            if (packet.Type == PacketType.UseCircuitCode)
            {
                UseCircuitCodePacket useCircuitCode = (UseCircuitCodePacket)packet;
                IClientAPI newuser;
                uint circuitCode = useCircuitCode.CircuitCode.Code;

                // Check if the client is already established
                if (!m_scene.ClientManager.TryGetClient(circuitCode, out newuser))
                {
                    AddNewClient(useCircuitCode, (IPEndPoint)buffer.RemoteEndPoint);
                }
            }

            // Determine which agent this packet came from
            if (!clients.TryGetValue(address, out client))
            {
                m_log.Warn("[LLUDPSERVER]: Received a " + packet.Type + " packet from an unrecognized source: " + address);
                return;
            }

            #endregion UseCircuitCode Handling

            // Stats tracking
            Interlocked.Increment(ref client.PacketsReceived);

            #region ACK Receiving

            int now = Environment.TickCount;
            client.TickLastPacketReceived = now;

            // Handle appended ACKs
            if (packet.Header.AppendedAcks && packet.Header.AckList != null)
            {
                lock (client.NeedAcks.SyncRoot)
                {
                    for (int i = 0; i < packet.Header.AckList.Length; i++)
                        AcknowledgePacket(client, packet.Header.AckList[i], now, packet.Header.Resent);
                }
            }

            // Handle PacketAck packets
            if (packet.Type == PacketType.PacketAck)
            {
                PacketAckPacket ackPacket = (PacketAckPacket)packet;

                lock (client.NeedAcks.SyncRoot)
                {
                    for (int i = 0; i < ackPacket.Packets.Length; i++)
                        AcknowledgePacket(client, ackPacket.Packets[i].ID, now, packet.Header.Resent);
                }
            }

            #endregion ACK Receiving

            #region ACK Sending

            if (packet.Header.Reliable)
                client.PendingAcks.Enqueue((uint)packet.Header.Sequence);

            // This is a somewhat odd sequence of steps to pull the client.BytesSinceLastACK value out,
            // add the current received bytes to it, test if 2*MTU bytes have been sent, if so remove
            // 2*MTU bytes from the value and send ACKs, and finally add the local value back to
            // client.BytesSinceLastACK. Lockless thread safety
            int bytesSinceLastACK = Interlocked.Exchange(ref client.BytesSinceLastACK, 0);
            bytesSinceLastACK += buffer.DataLength;
            if (bytesSinceLastACK > Packet.MTU * 2)
            {
                bytesSinceLastACK -= Packet.MTU * 2;
                SendAcks(client);
            }
            Interlocked.Add(ref client.BytesSinceLastACK, bytesSinceLastACK);

            #endregion ACK Sending

            #region Incoming Packet Accounting

            // Check the archive of received reliable packet IDs to see whether we already received this packet
            if (packet.Header.Reliable && !client.PacketArchive.TryEnqueue(packet.Header.Sequence))
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
                packetInbox.Enqueue(new IncomingPacket(client, packet));
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
            //Slave regions don't accept new clients
            if (m_scene.RegionStatus != RegionStatus.SlaveScene)
            {
                AuthenticateResponse sessionInfo;
                bool isNewCircuit = !clients.ContainsKey(remoteEndPoint);

                if (!IsClientAuthorized(useCircuitCode, out sessionInfo))
                {
                    m_log.WarnFormat(
                        "[CONNECTION FAILURE]: Connection request for client {0} connecting with unnotified circuit code {1} from {2}",
                        useCircuitCode.CircuitCode.ID, useCircuitCode.CircuitCode.Code, remoteEndPoint);
                    return;
                }

                if (isNewCircuit)
                {
                    UUID agentID = useCircuitCode.CircuitCode.ID;
                    UUID sessionID = useCircuitCode.CircuitCode.SessionID;
                    uint circuitCode = useCircuitCode.CircuitCode.Code;

                    AddClient(circuitCode, agentID, sessionID, remoteEndPoint, sessionInfo);
                }
            }
        }

        private void AddClient(uint circuitCode, UUID agentID, UUID sessionID, IPEndPoint remoteEndPoint, AuthenticateResponse sessionInfo)
        {
            // Create the LLUDPClient
            LLUDPClient client = new LLUDPClient(this, m_throttleRates, m_throttle, circuitCode, agentID, remoteEndPoint);

            // Create the LLClientView
            LLClientView clientApi = new LLClientView(remoteEndPoint, m_scene, this, client, sessionInfo, agentID, sessionID, circuitCode);
            clientApi.OnViewerEffect += m_scene.ClientManager.ViewerEffectHandler;
            clientApi.OnLogout += LogoutHandler;
            clientApi.OnConnectionClosed += RemoveClient;

            // Start the IClientAPI
            m_scene.ClientManager.Add(circuitCode, clientApi);
            clientApi.Start();

            // Give LLUDPClient a reference to IClientAPI
            client.ClientAPI = clientApi;

            // Add the new client to our list of tracked clients
            clients.Add(agentID, client.RemoteEndPoint, client);
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

                clients.ForEach(
                    delegate(LLUDPClient client)
                    {
                        if (client.DequeueOutgoing())
                            packetSent = true;
                        if (resendUnacked)
                            ResendUnacked(client);
                        if (sendAcks)
                        {
                            SendAcks(client);
                            client.SendPacketStats();
                        }
                        if (sendPings)
                            SendPing(client);
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
            LLUDPClient client = incomingPacket.Client;

            // Sanity check
            if (packet == null || client == null || client.ClientAPI == null)
            {
                m_log.WarnFormat("[LLUDPSERVER]: Processing a packet with incomplete state. Packet=\"{0}\", Client=\"{1}\", Client.ClientAPI=\"{2}\"",
                    packet, client, (client != null) ? client.ClientAPI : null);
            }

            try
            {
                // Process this packet
                client.ClientAPI.ProcessInPacket(packet);
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
                m_log.ErrorFormat("[LLUDPSERVER]: Client packet handler for {0} for packet {1} threw an exception", client.AgentID, packet.Type);
                m_log.Error(e.Message, e);
            }
        }

        private void LogoutHandler(IClientAPI client)
        {
            client.SendLogoutPacket();
            RemoveClient(client);
        }
    }
}
