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
using System.Reflection;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Timers;
using OpenMetaverse;
using OpenMetaverse.Packets;
using log4net;
using OpenSim.Framework;
using Timer=System.Timers.Timer;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public class LLPacketHandler : ILLPacketHandler
    {
        private static readonly ILog m_log 
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //private int m_resentCount;

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
        private List<uint> m_PendingAcks = new List<uint>();
        private Dictionary<uint, uint> m_PendingAcksMap = new Dictionary<uint, uint>();

        private Dictionary<uint, LLQueItem> m_NeedAck =
                new Dictionary<uint, LLQueItem>();

        /// <summary>
        /// The number of milliseconds that can pass before a packet that needs an ack is resent.
        /// </param>
        private uint m_ResendTimeout = 4000;

        public uint ResendTimeout
        {
            get { return m_ResendTimeout; }
            set { m_ResendTimeout = value; }
        }

        private int m_MaxReliableResends = 3;

        public int MaxReliableResends
        {
            get { return m_MaxReliableResends; }
            set { m_MaxReliableResends = value; }
        }

        // Track duplicated packets. This uses a Dictionary. Both insertion
        // and lookup are common operations and need to take advantage of
        // the hashing. Expiration is less common and can be allowed the
        // time for a linear scan.
        //
        private List<uint> m_alreadySeenList = new List<uint>();
        private Dictionary<uint, int>m_alreadySeenTracker = new Dictionary<uint, int>();
        private int m_alreadySeenWindow = 30000;
        private int m_lastAlreadySeenCheck = Environment.TickCount & Int32.MaxValue;

        // private Dictionary<uint, int> m_DupeTracker =
        //     new Dictionary<uint, int>();
        // private uint m_DupeTrackerWindow = 30;
        // private int m_DupeTrackerLastCheck = Environment.TickCount;

        // Values for the SimStatsReporter
        //
        private int m_PacketsReceived = 0;
        private int m_PacketsReceivedReported = 0;
        private int m_PacketsSent = 0;
        private int m_PacketsSentReported = 0;
        private int m_UnackedBytes = 0;

        private int m_LastResend = 0;

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
        public event QueueEmpty OnQueueEmpty;

        
        //private SynchronizeClientHandler m_SynchronizeClient = null;

        public SynchronizeClientHandler SynchronizeClient
        {
            set { /* m_SynchronizeClient = value; */ }
        }

        // Packet sequencing
        //
        private uint m_Sequence = 0;
        private object m_SequenceLock = new object();
        private const int MAX_SEQUENCE = 0xFFFFFF;

        // Packet dropping
        //
        List<PacketType> m_ImportantPackets = new List<PacketType>();
        private bool m_ReliableIsImportant = false;

        public bool ReliableIsImportant
        {
            get { return m_ReliableIsImportant; }
            set { m_ReliableIsImportant = value; }
        }

        private int m_DropSafeTimeout;

        LLPacketServer m_PacketServer;
        private byte[] m_ZeroOutBuffer = new byte[4096];

        ////////////////////////////////////////////////////////////////////

        // Constructors
        //
        public LLPacketHandler(IClientAPI client, LLPacketServer server, ClientStackUserSettings userSettings)
        {
            m_Client = client;
            m_PacketServer = server;
            m_DropSafeTimeout = Environment.TickCount + 15000;

            m_PacketQueue = new LLPacketQueue(client.AgentId, userSettings);

            m_PacketQueue.OnQueueEmpty += TriggerOnQueueEmpty;

            m_AckTimer.Elapsed += AckTimerElapsed;
            m_AckTimer.Start();
        }

        public void Dispose()
        {
            m_AckTimer.Stop();
            m_AckTimer.Close();

            m_PacketQueue.Enqueue(null);
            m_PacketQueue.Close();
            m_Client = null;
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
            // These packet types have shown to have issues with
            // acks being appended to the payload, just don't send
            // any with them until libsl is fixed.
            //
            if (packet is ViewerEffectPacket)
                return;
            if (packet is SimStatsPacket)
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

                for (int i = 0; i < count; i++)
                {
                    packet.Header.AckList[i] = m_PendingAcks[i];
                    m_PendingAcksMap.Remove(m_PendingAcks[i]);
                }
                m_PendingAcks.RemoveRange(0, count);
            }
        }

        private void QueuePacket(
                Packet packet, ThrottleOutPacketType throttlePacketType,
                Object id)
        {
            LLQueItem item = new LLQueItem();
            item.Packet = packet;
            item.Incoming = false;
            item.throttleType = throttlePacketType;
            item.TickCount = Environment.TickCount;
            item.Identifier = id;
            item.Resends = 0;
            item.Length = packet.Length;
            item.Sequence = packet.Header.Sequence;

            m_PacketQueue.Enqueue(item);
            m_PacketsSent++;
        }

        private void ResendUnacked()
        {
            int now = Environment.TickCount;

            int intervalMs = 250;

            if (m_LastResend != 0)
                intervalMs = now - m_LastResend;

            lock (m_NeedAck)
            {
                if (m_DropSafeTimeout > now ||
                    intervalMs > 500) // We were frozen!
                {
                    foreach (LLQueItem data in m_NeedAck.Values)
                    {
                        if (m_DropSafeTimeout > now)
                        {
                            m_NeedAck[data.Packet.Header.Sequence].TickCount = now;
                        }
                        else
                        {
                            m_NeedAck[data.Packet.Header.Sequence].TickCount += intervalMs;
                        }
                    }
                }

                m_LastResend = now;
                
                // Unless we have received at least one ack, don't bother resending
                // anything. There may not be a client there, don't clog up the
                // pipes.


                // Nothing to do
                //
                if (m_NeedAck.Count == 0)
                    return;

                int resent = 0;
                long dueDate = now - m_ResendTimeout;

                List<LLQueItem> dropped = new List<LLQueItem>();
                foreach (LLQueItem data in m_NeedAck.Values)
                {
                    Packet packet = data.Packet;

                    // Packets this old get resent
                    //
                    if (data.TickCount < dueDate && data.Sequence != 0 && !m_PacketQueue.Contains(data.Sequence))
                    {
                        if (resent < 20) // Was 20 (= Max 117kbit/sec resends)
                        {
                            m_NeedAck[packet.Header.Sequence].Resends++;

                            // The client needs to be told that a packet is being resent, otherwise it appears to believe
                            // that it should reset its sequence to that packet number.
                            packet.Header.Resent = true;

                            if ((m_NeedAck[packet.Header.Sequence].Resends >= m_MaxReliableResends) && 
                                (!m_ReliableIsImportant))
                            {
                                dropped.Add(data);
                                continue;
                            }

                            m_NeedAck[packet.Header.Sequence].TickCount = Environment.TickCount;
                            QueuePacket(packet, ThrottleOutPacketType.Resend, data.Identifier);
                            resent++;
                        }
                        else
                        {
                            m_NeedAck[packet.Header.Sequence].TickCount += intervalMs;
                        }
                    }
                }

                foreach (LLQueItem data in dropped)
                {
                    m_NeedAck.Remove(data.Packet.Header.Sequence);
                    TriggerOnPacketDrop(data.Packet, data.Identifier);
                    m_PacketQueue.Cancel(data.Packet.Header.Sequence);
                    PacketPool.Instance.ReturnPacket(data.Packet);
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

                for (int i = 0; i < m_PendingAcks.Count; i++)
                {
                    acks.Packets[i] = new PacketAckPacket.PacketsBlock();
                    acks.Packets[i].ID = m_PendingAcks[i];

                }
                m_PendingAcks.Clear();
                m_PendingAcksMap.Clear();

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
                    if (!m_PendingAcksMap.ContainsKey(packet.Header.Sequence))
                    {
                        m_PendingAcks.Add(packet.Header.Sequence);
                        m_PendingAcksMap.Add(packet.Header.Sequence,
                                             packet.Header.Sequence);
                    }
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
                    if (!m_PendingAcksMap.ContainsKey(packet.Header.Sequence))
                    {
                        m_PendingAcks.Add(packet.Header.Sequence);
                        m_PendingAcksMap.Add(packet.Header.Sequence,
                                             packet.Header.Sequence);
                    }
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
        // NOTE: this needs to be called from within lock
        // (m_alreadySeenTracker) context!
        private void ExpireSeenPackets()
        {
            if (m_alreadySeenList.Count < 1024)
                return;
            
            int ticks = 0;
            int tc = Environment.TickCount & Int32.MaxValue;
            if (tc >= m_lastAlreadySeenCheck) 
                ticks = tc - m_lastAlreadySeenCheck;
            else
                ticks = Int32.MaxValue - m_lastAlreadySeenCheck + tc;
            
            if (ticks < 2000) return;
            m_lastAlreadySeenCheck = tc;

            // we calculate the drop dead tick count here instead of
            // in the loop: any packet with a timestamp before
            // dropDeadTC can be expired
            int dropDeadTC = tc - m_alreadySeenWindow;
            int i = 0;
            while (i < m_alreadySeenList.Count && m_alreadySeenTracker[m_alreadySeenList[i]] < dropDeadTC)
            {
                m_alreadySeenTracker.Remove(m_alreadySeenList[i]);
                i++;
            }
            // if we dropped packet from m_alreadySeenTracker we need
            // to drop them from m_alreadySeenList as well, let's do
            // that in one go: the list is ordered after all.
            if (i > 0)
            {
                m_alreadySeenList.RemoveRange(0, i);
                // m_log.DebugFormat("[CLIENT]: expired {0} packets, {1}:{2} left", i, m_alreadySeenList.Count, m_alreadySeenTracker.Count);
            }
        }

        public void InPacket(Packet packet)
        {
            if (packet == null)
                return;

            // When too many acks are needed to be sent, the client sends
            // a packet consisting of acks only
            //
            if (packet.Type == PacketType.PacketAck)
            {
                PacketAckPacket ackPacket = (PacketAckPacket)packet;

                foreach (PacketAckPacket.PacketsBlock block in ackPacket.Packets)
                {
                    ProcessAck(block.ID);
                }

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

            // If this client is on another partial instance, no need
            // to handle packets
            //
            if (!m_Client.IsActive && packet.Type != PacketType.LogoutRequest)
            {
                PacketPool.Instance.ReturnPacket(packet);
                return;
            }

            if (packet.Type == PacketType.StartPingCheck)
            {
                StartPingCheckPacket startPing = (StartPingCheckPacket)packet;
                CompletePingCheckPacket endPing
                    = (CompletePingCheckPacket)PacketPool.Instance.GetPacket(PacketType.CompletePingCheck);

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

            // Check for duplicate packets..    packets that the client is
            // resending because it didn't receive our ack
            //
            lock (m_alreadySeenTracker)
            {
                ExpireSeenPackets();
            
                if (m_alreadySeenTracker.ContainsKey(packet.Header.Sequence))
                    return;

                m_alreadySeenTracker.Add(packet.Header.Sequence, Environment.TickCount & Int32.MaxValue);
                m_alreadySeenList.Add(packet.Header.Sequence);
            }

            m_Client.ProcessInPacket(packet);
        }

        public void Flush()
        {
            m_PacketQueue.Flush();
            m_UnackedBytes = (-1 * m_UnackedBytes);
            SendPacketStats();
        }

        public void Clear()
        {
            m_UnackedBytes = (-1 * m_UnackedBytes);
            SendPacketStats();
            lock (m_NeedAck) 
            {
                m_NeedAck.Clear();
                m_PendingAcks.Clear();
                m_PendingAcksMap.Clear();
            }
            m_Sequence += 1000000;
        }

        private void ProcessAck(uint id)
        {
            LLQueItem data;

            lock (m_NeedAck)
            {
                //m_log.DebugFormat("[CLIENT]: In {0} received ack for packet {1}", m_Client.Scene.RegionInfo.ExternalEndPoint.Port, id);

                if (!m_NeedAck.TryGetValue(id, out data))
                    return;

                m_NeedAck.Remove(id);
                m_PacketQueue.Cancel(data.Sequence);
                PacketPool.Instance.ReturnPacket(data.Packet);
                m_UnackedBytes -= data.Length;
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

            info.pendingAcks = m_PendingAcksMap;
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

            float multiplier = m_PacketQueue.ThrottleMultiplier;
            info.resendThrottle = (int) (m_PacketQueue.ResendThrottle.Throttle / multiplier);
            info.landThrottle = (int) (m_PacketQueue.LandThrottle.Throttle / multiplier);
            info.windThrottle = (int) (m_PacketQueue.WindThrottle.Throttle / multiplier);
            info.cloudThrottle = (int) (m_PacketQueue.CloudThrottle.Throttle / multiplier);
            info.taskThrottle = (int) (m_PacketQueue.TaskThrottle.Throttle / multiplier);
            info.assetThrottle = (int) (m_PacketQueue.AssetThrottle.Throttle / multiplier);
            info.textureThrottle = (int) (m_PacketQueue.TextureThrottle.Throttle / multiplier);
            info.totalThrottle = (int) (m_PacketQueue.TotalThrottle.Throttle / multiplier);

            return info;
        }

        public void SetClientInfo(ClientInfo info)
        {
            m_PendingAcksMap = info.pendingAcks;
            m_PendingAcks = new List<uint>(m_PendingAcksMap.Keys);
            m_NeedAck = new Dictionary<uint, LLQueItem>();

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

                LLQueItem item = new LLQueItem();
                item.Packet = packet;
                item.Incoming = false;
                item.throttleType = 0;
                item.TickCount = Environment.TickCount;
                item.Identifier = 0;
                item.Resends = 0;
                item.Length = packet.Length;
                item.Sequence = packet.Header.Sequence;
                m_NeedAck.Add(key, item);
            }

            m_Sequence = info.sequence;

            m_PacketQueue.ResendThrottle.Throttle = info.resendThrottle;
            m_PacketQueue.LandThrottle.Throttle = info.landThrottle;
            m_PacketQueue.WindThrottle.Throttle = info.windThrottle;
            m_PacketQueue.CloudThrottle.Throttle = info.cloudThrottle;
            m_PacketQueue.TaskThrottle.Throttle = info.taskThrottle;
            m_PacketQueue.AssetThrottle.Throttle = info.assetThrottle;
            m_PacketQueue.TextureThrottle.Throttle = info.textureThrottle;
            m_PacketQueue.TotalThrottle.Throttle = info.totalThrottle;
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
            LLQueItem d = null;

            foreach (LLQueItem data in m_NeedAck.Values)
            {
                if (data.Identifier != null && data.Identifier == id)
                {
                    d = data;
                    break;
                }
            }

            if (null == d) return;

            m_NeedAck.Remove(d.Packet.Header.Sequence);
            m_PacketQueue.Cancel(d.Sequence);
            PacketPool.Instance.ReturnPacket(d.Packet);
        }

        private void TriggerOnPacketDrop(Packet packet, Object id)
        {
            PacketDrop handlerPacketDrop = OnPacketDrop;

            if (handlerPacketDrop == null)
                return;

            handlerPacketDrop(packet, id);
        }

        private void TriggerOnQueueEmpty(ThrottleOutPacketType queue)
        {
            QueueEmpty handlerQueueEmpty = OnQueueEmpty;

            if (handlerQueueEmpty != null)
                handlerQueueEmpty(queue);
        }

        // Convert the packet to bytes and stuff it onto the send queue
        //
        public void ProcessOutPacket(LLQueItem item)
        {
            Packet packet = item.Packet;

            // Assign sequence number here to prevent out of order packets
            if (packet.Header.Sequence == 0)
            {
                lock (m_NeedAck)
                {
                    packet.Header.Sequence = NextPacketSequenceNumber();
                    item.Sequence = packet.Header.Sequence;
                    item.TickCount = Environment.TickCount;

                    // We want to see that packet arrive if it's reliable
                    if (packet.Header.Reliable)
                    {
                        m_UnackedBytes += item.Length;

                        // Keep track of when this packet was sent out
                        item.TickCount = Environment.TickCount;

                        m_NeedAck[packet.Header.Sequence] = item;
                    }
                }
            }

            // If we sent a killpacket
            if (packet is KillPacket)
                Abort();

            try
            {
                // If this packet has been reused/returned, the ToBytes
                // will blow up in our face.
                // Fail gracefully.
                //

                // Actually make the byte array and send it
                byte[] sendbuffer = item.Packet.ToBytes();

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
            }
            catch (NullReferenceException)
            {
                m_log.Error("[PACKET]: Detected reuse of a returned packet");
                m_PacketQueue.Cancel(item.Sequence);
                return;
            }

            // If this is a reliable packet, we are still holding a ref
            // Dont't return in that case
            //
            if (!packet.Header.Reliable)
            {
                m_PacketQueue.Cancel(item.Sequence);
                PacketPool.Instance.ReturnPacket(packet);
            }
        }

        private void Abort()
        {
            m_PacketQueue.Close();
            Thread.CurrentThread.Abort();
        }

        public int GetQueueCount(ThrottleOutPacketType queue)
        {
            return m_PacketQueue.GetQueueCount(queue);
        }
    }
}
