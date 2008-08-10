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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using System.Reflection;
using libsecondlife;
using libsecondlife.Packets;
using Timer = System.Timers.Timer;
using OpenSim.Framework;
using OpenSim.Region.ClientStack.LindenUDP;
using log4net;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public delegate void PacketStats(int inPackets, int outPackets, int unAckedBytes);
    public delegate void PacketDrop(Packet pack, Object id);
    public delegate bool SynchronizeClientHandler(IScene scene, Packet packet, LLUUID agentID, ThrottleOutPacketType throttlePacketType);

    public interface IPacketHandler
    {
        event PacketStats OnPacketStats;
        event PacketDrop OnPacketDrop;
        SynchronizeClientHandler SynchronizeClient { set; }

        int PacketsReceived { get; }
        int PacketsReceivedReported { get; }
        uint SilenceLimit { get; set; }
        uint DiscardTimeout { get; set; }
        uint ResendTimeout { get; set; }

        void InPacket(Packet packet);
        void ProcessInPacket(LLQueItem item);
        void ProcessOutPacket(LLQueItem item);
        void OutPacket(Packet NewPack,
                       ThrottleOutPacketType throttlePacketType);
        void OutPacket(Packet NewPack,
                       ThrottleOutPacketType throttlePacketType, Object id);
        LLPacketQueue PacketQueue { get; }
        void Stop();
        void Flush();
        void Clear();
        ClientInfo GetClientInfo();
        void SetClientInfo(ClientInfo info);
        void AddImportantPacket(PacketType type);
        void RemoveImportantPacket(PacketType type);
    }

    public class LLPacketHandler : IPacketHandler
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Packet queues
        //
        LLPacketQueue m_PacketQueue;

        public LLPacketQueue PacketQueue
        {
            get { return m_PacketQueue; }
        }

        // Timer to run stats and acks on
        //
        private Timer m_AckTimer = new Timer(250);

        // A list of the packets we haven't acked yet
        //
        private Dictionary<uint,uint> m_PendingAcks = new Dictionary<uint,uint>();
        // Dictionary of the packets that need acks from the client.
        //
        private class AckData
        {
            public AckData(Packet packet, Object identifier)
            {
                Packet = packet;
                Identifier = identifier;
            }

            public Packet Packet;
            public Object Identifier;
        }
        private Dictionary<uint, AckData> m_NeedAck =
                new Dictionary<uint, AckData>();

        private uint m_ResendTimeout = 1000;

        public uint ResendTimeout
        {
            get { return m_ResendTimeout; }
            set { m_ResendTimeout = value; }
        }

        private uint m_DiscardTimeout = 8000;

        public uint DiscardTimeout
        {
            get { return m_DiscardTimeout; }
            set { m_DiscardTimeout = value; }
        }

        private uint m_SilenceLimit = 250;

        public uint SilenceLimit
        {
            get { return m_SilenceLimit; }
            set { m_SilenceLimit = value; }
        }

        private int m_LastAck = 0;

        // Track duplicated packets. This uses a Dictionary. Both insertion
        // and lookup are common operations and need to take advantage of
        // the hashing. Expiration is less common and can be allowed the
        // time for a linear scan.
        //
        private Dictionary<uint, int> m_DupeTracker =
            new Dictionary<uint, int>();
        private uint m_DupeTrackerWindow = 30;
        private int m_DupeTrackerLastCheck = System.Environment.TickCount;

        // Values for the SimStatsReporter
        //
        private int m_PacketsReceived = 0;
        private int m_PacketsReceivedReported = 0;
        private int m_PacketsSent = 0;
        private int m_PacketsSentReported = 0;
        private int m_UnackedBytes = 0;

        public int PacketsReceived
        {
            get { return m_PacketsReceived; }
        }

        public int PacketsReceivedReported
        {
            get { return m_PacketsReceivedReported; }
        }

        // The client we are working for
        //
        private IClientAPI m_Client;

        // Some events
        //
        public event PacketStats OnPacketStats;
        public event PacketDrop OnPacketDrop;

        private SynchronizeClientHandler m_SynchronizeClient = null;

        public SynchronizeClientHandler SynchronizeClient
        {
            set { m_SynchronizeClient = value; }
        }

        // Packet sequencing
        //
        private uint m_Sequence = 0;
        private object m_SequenceLock = new object();
        private const int MAX_SEQUENCE = 0xFFFFFF;

        List<PacketType> m_ImportantPackets = new List<PacketType>();

        LLPacketServer m_PacketServer;
        private byte[] m_ZeroOutBuffer = new byte[4096];

        ////////////////////////////////////////////////////////////////////

        // Constructors
        //
        public LLPacketHandler(IClientAPI client, LLPacketServer server)
        {
            m_Client = client;
            m_PacketServer = server;

            m_PacketQueue = new LLPacketQueue(client.AgentId);

            m_AckTimer.Elapsed += AckTimerElapsed;
            m_AckTimer.Start();
        }

        public void Stop()
        {
            m_AckTimer.Stop();

            m_PacketQueue.Enqueue(null);
        }

        // Send one packet. This actually doesn't send anything, it queues
        // it. Designed to be fire-and-forget, but there is an optional
        // notifier.
        //
        public void OutPacket(
            Packet packet, ThrottleOutPacketType throttlePacketType)
        {
            OutPacket(packet, throttlePacketType, null);
        }

        public void OutPacket(
            Packet packet, ThrottleOutPacketType throttlePacketType,
            Object id)
        {
            // Call the load balancer's hook. If this is not active here
            // we defer to the sim server this client is actually connected
            // to. Packet drop notifies will not be triggered in this 
            // configuration!
            //
            if ((m_SynchronizeClient != null) && (!m_Client.IsActive))
            {
                if (m_SynchronizeClient(m_Client.Scene, packet,
                                        m_Client.AgentId, throttlePacketType))
                    return;
            }

			packet.Header.Sequence = 0;

            lock (m_NeedAck)
            {
                DropResend(id);

                AddAcks(ref packet);
                QueuePacket(packet, throttlePacketType, id);
            }
        }

        private void AddAcks(ref Packet packet)
        {
            // This packet type has shown to have issues with
            // acks being appended to the payload, just don't send
            // any with this packet type until libsl is fixed.
            //
            if (packet is libsecondlife.Packets.ViewerEffectPacket)
                return;

            // Add acks to outgoing packets
            //
            if (m_PendingAcks.Count > 0)
            {
                int count = m_PendingAcks.Count;
                if (count > 10)
                    count = 10;
                packet.Header.AckList = new uint[count];
                packet.Header.AppendedAcks = true;

                int i = 0;

                foreach (uint ack in new List<uint>(m_PendingAcks.Keys))
                {
                    packet.Header.AckList[i] = ack;
                    i++;
                    m_PendingAcks.Remove(ack);
                    if (i >= count) // That is how much space there is
                        break;
                }
            }
        }
        
        private void QueuePacket(
                Packet packet, ThrottleOutPacketType throttlePacketType,
                Object id)
        {
            packet.TickCount = System.Environment.TickCount;

            LLQueItem item = new LLQueItem();
            item.Packet = packet;
            item.Incoming = false;
            item.throttleType = throttlePacketType;
            item.Identifier = id;

            m_PacketQueue.Enqueue(item);
            m_PacketsSent++;
        }

        private void ResendUnacked()
        {
            int now = System.Environment.TickCount;
            int lastAck = m_LastAck;
            
            // Unless we have received at least one ack, don't bother resending
            // anything. There may not be a client there, don't clog up the
            // pipes.
            //
            if (lastAck == 0)
                return;

            lock (m_NeedAck)
            {
                // Nothing to do
                //
                if (m_NeedAck.Count == 0)
                    return;

                // If we have seen no acks in <SilenceLimit> s but are
                // waiting for acks, then there may be no one listening.
                // No need to resend anything. Keep it until it gets stale,
                // then it will be dropped.
                //
                if ((((now - lastAck) > m_SilenceLimit) &&
                     m_NeedAck.Count > 0) || m_NeedAck.Count == 0)
                {
                    return;
                }

                foreach (AckData data in new List<AckData>(m_NeedAck.Values))
                {
                    Packet packet = data.Packet;

                    // Packets this old get resent
                    //
                    if ((now - packet.TickCount) > m_ResendTimeout)
                    {
                        // Resend the packet. Set the packet's tick count to
                        // now, and keep it marked as resent.
                        //
                        packet.Header.Resent = true;
                        QueuePacket(packet, ThrottleOutPacketType.Resend,
                                data.Identifier);
                    }

                    // The discard logic
                    // If the packet is in the queue for <DiscardTimeout> s
                    // without having been processed, then we have clogged
                    // pipes. Most likely, the client is gone
                    // Drop the packets
                    //
                    if ((now - packet.TickCount) > m_DiscardTimeout)
                    {
                        if (!m_ImportantPackets.Contains(packet.Type))
                            m_NeedAck.Remove(packet.Header.Sequence);

                        TriggerOnPacketDrop(packet, data.Identifier);

                        continue;
                    }
                }
            }
        }

        // Send the pending packet acks to the client
        // Will send blocks of acks for up to 250 packets
        //
        private void SendAcks()
        {
            lock (m_NeedAck)
            {
                if (m_PendingAcks.Count == 0)
                    return;

                PacketAckPacket acks = (PacketAckPacket)PacketPool.Instance.GetPacket(PacketType.PacketAck);

                // The case of equality is more common than one might think,
                // because this function will be called unconditionally when
                // the counter reaches 250. So there is a good chance another
                // packet with 250 blocks exists.
                //
                if (acks.Packets == null ||
                    acks.Packets.Length != m_PendingAcks.Count)
                    acks.Packets = new PacketAckPacket.PacketsBlock[m_PendingAcks.Count];
                int i = 0;
                foreach (uint ack in new List<uint>(m_PendingAcks.Keys))
                {
                    acks.Packets[i] = new PacketAckPacket.PacketsBlock();
                    acks.Packets[i].ID = ack;

                    m_PendingAcks.Remove(ack);
                    i++;
                }

                acks.Header.Reliable = false;
                OutPacket(acks, ThrottleOutPacketType.Unknown);
            }
        }

        // Queue a packet ack. It will be sent either after 250 acks are
        // queued, or when the timer fires.
        //
        private void AckPacket(Packet packet)
        {
            lock (m_NeedAck)
            {
                if (m_PendingAcks.Count < 250)
                {
                    if (!m_PendingAcks.ContainsKey(packet.Header.Sequence))
                        m_PendingAcks.Add(packet.Header.Sequence,
                                          packet.Header.Sequence);
                    return;
                }
            }

            SendAcks();

            lock (m_NeedAck)
            {
                // If this is still full we have a truly exceptional
                // condition (means, can't happen)
                //
                if (m_PendingAcks.Count < 250)
                {
                    if (!m_PendingAcks.ContainsKey(packet.Header.Sequence))
                        m_PendingAcks.Add(packet.Header.Sequence,
                                          packet.Header.Sequence);
                    return;
                }
            }
        }

        // When the timer elapses, send the pending acks, trigger resends
        // and report all the stats.
        //
        private void AckTimerElapsed(object sender, ElapsedEventArgs ea)
        {
            SendAcks();
            ResendUnacked();
            SendPacketStats();
        }

        // Push out pachet counts for the sim status reporter
        //
        private void SendPacketStats()
        {
            PacketStats handlerPacketStats = OnPacketStats;
            if (handlerPacketStats != null)
            {
                handlerPacketStats(
                    m_PacketsReceived - m_PacketsReceivedReported,
                    m_PacketsSent - m_PacketsSentReported,
                    m_UnackedBytes);

                m_PacketsReceivedReported = m_PacketsReceived;
                m_PacketsSentReported = m_PacketsSent;
            }
        }

        // We can't keep an unlimited record of dupes. This will prune the
        // dictionary by age.
        //
        private void PruneDupeTracker()
        {
            lock (m_DupeTracker)
            {
                if (m_DupeTracker.Count < 1024)
                    return;

                if (System.Environment.TickCount - m_DupeTrackerLastCheck < 2000)
                    return;

                m_DupeTrackerLastCheck = System.Environment.TickCount;

                Dictionary<uint, int> packs =
                    new Dictionary<uint, int>(m_DupeTracker);

                foreach (uint pack in packs.Keys)
                {
                    if (Util.UnixTimeSinceEpoch() - m_DupeTracker[pack] >
                        m_DupeTrackerWindow)
                        m_DupeTracker.Remove(pack);
                }
            }
        }

        public void InPacket(Packet packet)
        {
            if (packet == null)
                return;

            // If this client is on another partial instance, no need
            // to handle packets
            //
            if (!m_Client.IsActive && packet.Type != PacketType.LogoutRequest)
            {
                PacketPool.Instance.ReturnPacket(packet);
                return;
            }

            // Any packet can have some packet acks in the header.
            // Process them here
            //
            if (packet.Header.AppendedAcks)
            {
                foreach (uint id in packet.Header.AckList)
                {
                    ProcessAck(id);
                }
            }

            // When too many acks are needed to be sent, the client sends
            // a packet consisting of acks only
            //
            if (packet.Type == PacketType.PacketAck)
            {
                PacketAckPacket ackPacket = (PacketAckPacket)packet;

                foreach (PacketAckPacket.PacketsBlock block in
                         ackPacket.Packets)
                {
                    ProcessAck(block.ID);
                }

                PacketPool.Instance.ReturnPacket(packet);
                return;
            }
            else if (packet.Type == PacketType.StartPingCheck)
            {
                StartPingCheckPacket startPing = (StartPingCheckPacket)packet;
                CompletePingCheckPacket endPing = (CompletePingCheckPacket)PacketPool.Instance.GetPacket(PacketType.CompletePingCheck);

                endPing.PingID.PingID = startPing.PingID.PingID;
                OutPacket(endPing, ThrottleOutPacketType.Task);
            }
            else
            {
                LLQueItem item = new LLQueItem();
                item.Packet = packet;
                item.Incoming = true;
                m_PacketQueue.Enqueue(item);
            }
        }

        public void ProcessInPacket(LLQueItem item)
        {
            Packet packet = item.Packet;

            // Always ack the packet!
            //
            if (packet.Header.Reliable)
                AckPacket(packet);

            if (packet.Type != PacketType.AgentUpdate)
                m_PacketsReceived++;

            PruneDupeTracker();

            // Check for duplicate packets..    packets that the client is 
            // resending because it didn't receive our ack
            //
            lock (m_DupeTracker)
            {
                if (m_DupeTracker.ContainsKey(packet.Header.Sequence))
                    return;

                m_DupeTracker.Add(packet.Header.Sequence,
                                  Util.UnixTimeSinceEpoch());
            }

            m_Client.ProcessInPacket(packet);
        }

        public void Flush()
        {
            m_PacketQueue.Flush();
        }

        public void Clear()
        {
            m_NeedAck.Clear();
            m_PendingAcks.Clear();
            m_Sequence += 1000000;
        }

        private void ProcessAck(uint id)
        {
            AckData data;
            Packet packet;

            lock (m_NeedAck)
            {
                if (!m_NeedAck.TryGetValue(id, out data))
                    return;

                packet = data.Packet;

                m_NeedAck.Remove(id);
                m_UnackedBytes -= packet.ToBytes().Length;

                m_LastAck = System.Environment.TickCount;
            }
        }

        // Allocate packet sequence numbers in a threadsave manner
        //
        protected uint NextPacketSequenceNumber()
        {
            // Set the sequence number
            uint seq = 1;
            lock (m_SequenceLock)
            {
                if (m_Sequence >= MAX_SEQUENCE)
                {
                    m_Sequence = 1;
                }
                else
                {
                    m_Sequence++;
                }
                seq = m_Sequence;
            }
            return seq;
        }

        public ClientInfo GetClientInfo()
        {
            ClientInfo info = new ClientInfo();
            info.pendingAcks = m_PendingAcks;
            info.needAck = new Dictionary<uint, byte[]>();

            lock (m_NeedAck)
            {
                foreach (uint key in m_NeedAck.Keys)
                    info.needAck.Add(key, m_NeedAck[key].Packet.ToBytes());
            }

            LLQueItem[] queitems = m_PacketQueue.GetQueueArray();

            for (int i = 0; i < queitems.Length; i++)
            {
                if (queitems[i].Incoming == false)
                    info.out_packets.Add(queitems[i].Packet.ToBytes());
            }

            info.sequence = m_Sequence;

            return info;
        }

        public void SetClientInfo(ClientInfo info)
        {
            m_PendingAcks = info.pendingAcks;
            m_NeedAck = new Dictionary<uint, AckData>();

            Packet packet = null;
            int packetEnd = 0;
            byte[] zero = new byte[3000];

            foreach (uint key in info.needAck.Keys)
            {
                byte[] buff = info.needAck[key];
                packetEnd = buff.Length - 1;

                try
                {
                    packet = PacketPool.Instance.GetPacket(buff, ref packetEnd, zero);
                }
                catch (Exception)
                {
                }

                m_NeedAck.Add(key, new AckData(packet, null));
            }

            m_Sequence = info.sequence;
        }
        
        public void AddImportantPacket(PacketType type)
        {
            if (m_ImportantPackets.Contains(type))
                return;

            m_ImportantPackets.Add(type);
        }

        public void RemoveImportantPacket(PacketType type)
        {
            if (!m_ImportantPackets.Contains(type))
                return;

            m_ImportantPackets.Remove(type);
        }
        
        private void DropResend(Object id)
        {
            foreach (AckData data in new List<AckData>(m_NeedAck.Values))
            {
                if (data.Identifier != null && data.Identifier == id)
                {
                    m_NeedAck.Remove(data.Packet.Header.Sequence);
                    return;
                }
            }
        }

        private void TriggerOnPacketDrop(Packet packet, Object id)
        {
            PacketDrop handlerPacketDrop = OnPacketDrop;

            if (handlerPacketDrop == null)
                return;

            handlerPacketDrop(packet, id);
        }

        // Convert the packet to bytes and stuff it onto the send queue
        //
        public void ProcessOutPacket(LLQueItem item)
        {
            Packet packet = item.Packet;

            // Keep track of when this packet was sent out
            packet.TickCount = System.Environment.TickCount;

            // Assign sequence number here to prevent out of order packets
			if(packet.Header.Sequence == 0)
			{
				packet.Header.Sequence = NextPacketSequenceNumber();

				lock (m_NeedAck)
				{
					// We want to see that packet arrive if it's reliable
					if (packet.Header.Reliable)
					{
						m_UnackedBytes += packet.ToBytes().Length;
						m_NeedAck[packet.Header.Sequence] = new AckData(packet, 
								item.Identifier);
					}
				}
			}

            // Actually make the byte array and send it
            try
            {
                byte[] sendbuffer = packet.ToBytes();

                if (packet.Header.Zerocoded)
                {
                    int packetsize = Helpers.ZeroEncode(sendbuffer,
                            sendbuffer.Length, m_ZeroOutBuffer);
                    m_PacketServer.SendPacketTo(m_ZeroOutBuffer, packetsize,
                            SocketFlags.None, m_Client.CircuitCode);
                }
                else
                {
                    // Need some extra space in case we need to add proxy
                    // information to the message later
                    Buffer.BlockCopy(sendbuffer, 0, m_ZeroOutBuffer, 0,
                            sendbuffer.Length);
                    m_PacketServer.SendPacketTo(m_ZeroOutBuffer,
                            sendbuffer.Length, SocketFlags.None, m_Client.CircuitCode);
                }

                PacketPool.Instance.ReturnPacket(packet);
            }
            catch (Exception e)
            {
                m_log.Warn("[client]: " +
                       "PacketHandler:ProcessOutPacket() - WARNING: Socket "+
                       "exception occurred  - killing thread");
                m_log.Error(e.ToString());
                m_Client.Close(true);
            }
        }
    }
}
