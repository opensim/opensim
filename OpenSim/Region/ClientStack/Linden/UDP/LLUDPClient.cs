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
using System.Threading;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenMetaverse;
using OpenMetaverse.Packets;

using TokenBucket = OpenSim.Region.ClientStack.LindenUDP.TokenBucket;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    #region Delegates

    /// <summary>
    /// Fired when updated networking stats are produced for this client
    /// </summary>
    /// <param name="inPackets">Number of incoming packets received since this
    /// event was last fired</param>
    /// <param name="outPackets">Number of outgoing packets sent since this
    /// event was last fired</param>
    /// <param name="unAckedBytes">Current total number of bytes in packets we
    /// are waiting on ACKs for</param>
    public delegate void PacketStats(int inPackets, int outPackets, int unAckedBytes);
    /// <summary>
    /// Fired when the queue for one or more packet categories is empty. This 
    /// event can be hooked to put more data on the empty queues
    /// </summary>
    /// <param name="category">Categories of the packet queues that are empty</param>
    public delegate void QueueEmpty(ThrottleOutPacketTypeFlags categories);

    #endregion Delegates

    /// <summary>
    /// Tracks state for a client UDP connection and provides client-specific methods
    /// </summary>
    public sealed class LLUDPClient
    {
        // TODO: Make this a config setting
        /// <summary>Percentage of the task throttle category that is allocated to avatar and prim
        /// state updates</summary>
        const float STATE_TASK_PERCENTAGE = 0.8f;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>The number of packet categories to throttle on. If a throttle category is added
        /// or removed, this number must also change</summary>
        const int THROTTLE_CATEGORY_COUNT = 8;

        /// <summary>
        /// Controls whether information is logged about each outbound packet immediately before it is sent.  For debug purposes.
        /// </summary>
        /// <remarks>Any level above 0 will turn on logging.</remarks>
        public int DebugDataOutLevel { get; set; }

        /// <summary>
        /// Controls whether information is logged about each outbound packet immediately before it is sent.  For debug purposes.
        /// </summary>
        /// <remarks>Any level above 0 will turn on logging.</remarks>
        public int ThrottleDebugLevel 
        { 
            get
            {
                return m_throttleDebugLevel;
            }

            set
            {
                m_throttleDebugLevel = value;
                m_throttleClient.DebugLevel = m_throttleDebugLevel;
                foreach (TokenBucket tb in m_throttleCategories)
                    tb.DebugLevel = m_throttleDebugLevel;
            }
        }
        private int m_throttleDebugLevel;

        /// <summary>Fired when updated networking stats are produced for this client</summary>
        public event PacketStats OnPacketStats;
        /// <summary>Fired when the queue for a packet category is empty. This event can be
        /// hooked to put more data on the empty queue</summary>
        public event QueueEmpty OnQueueEmpty;

        public event Func<ThrottleOutPacketTypeFlags, bool> HasUpdates;

        /// <summary>AgentID for this client</summary>
        public readonly UUID AgentID;
        /// <summary>The remote address of the connected client</summary>
        public readonly IPEndPoint RemoteEndPoint;
        /// <summary>Circuit code that this client is connected on</summary>
        public readonly uint CircuitCode;
        /// <summary>Sequence numbers of packets we've received (for duplicate checking)</summary>
        public readonly IncomingPacketHistoryCollection PacketArchive = new IncomingPacketHistoryCollection(200);

        /// <summary>
        /// If true then we take action in response to unacked reliably sent packets such as resending the packet.
        /// </summary>
        public bool ProcessUnackedSends { get; set; }

        /// <summary>Packets we have sent that need to be ACKed by the client</summary>
        public readonly UnackedPacketCollection NeedAcks = new UnackedPacketCollection();

        /// <summary>ACKs that are queued up, waiting to be sent to the client</summary>
        public readonly OpenSim.Framework.LocklessQueue<uint> PendingAcks = new OpenSim.Framework.LocklessQueue<uint>();

        /// <summary>Current packet sequence number</summary>
        public int CurrentSequence;
        /// <summary>Current ping sequence number</summary>
        public byte CurrentPingSequence;
        /// <summary>True when this connection is alive, otherwise false</summary>
        public bool IsConnected = true;
        /// <summary>True when this connection is paused, otherwise false</summary>
        public bool IsPaused;
        /// <summary>Environment.TickCount when the last packet was received for this client</summary>
        public int TickLastPacketReceived;

        /// <summary>Smoothed round-trip time. A smoothed average of the round-trip time for sending a
        /// reliable packet to the client and receiving an ACK</summary>
        public float SRTT;
        /// <summary>Round-trip time variance. Measures the consistency of round-trip times</summary>
        public float RTTVAR;
        /// <summary>Retransmission timeout. Packets that have not been acknowledged in this number of
        /// milliseconds or longer will be resent</summary>
        /// <remarks>Calculated from <seealso cref="SRTT"/> and <seealso cref="RTTVAR"/> using the
        /// guidelines in RFC 2988</remarks>
        public int RTO;
        /// <summary>Number of bytes received since the last acknowledgement was sent out. This is used
        /// to loosely follow the TCP delayed ACK algorithm in RFC 1122 (4.2.3.2)</summary>
        public int BytesSinceLastACK;
        /// <summary>Number of packets received from this client</summary>
        public int PacketsReceived;
        /// <summary>Number of packets sent to this client</summary>
        public int PacketsSent;
        /// <summary>Number of packets resent to this client</summary>
        public int PacketsResent;        
        /// <summary>Total byte count of unacked packets sent to this client</summary>
        public int UnackedBytes;

        /// <summary>Total number of received packets that we have reported to the OnPacketStats event(s)</summary>
        private int m_packetsReceivedReported;
        /// <summary>Total number of sent packets that we have reported to the OnPacketStats event(s)</summary>
        private int m_packetsSentReported;
        /// <summary>Holds the Environment.TickCount value of when the next OnQueueEmpty can be fired</summary>
        private int m_nextOnQueueEmpty = 1;

        /// <summary>Throttle bucket for this agent's connection</summary>
        private readonly AdaptiveTokenBucket m_throttleClient;
        public AdaptiveTokenBucket FlowThrottle
        {
            get { return m_throttleClient; }
        }

        /// <summary>Throttle buckets for each packet category</summary>
        private readonly TokenBucket[] m_throttleCategories;
        /// <summary>Outgoing queues for throttled packets</summary>
        private readonly OpenSim.Framework.LocklessQueue<OutgoingPacket>[] m_packetOutboxes = new OpenSim.Framework.LocklessQueue<OutgoingPacket>[THROTTLE_CATEGORY_COUNT];
        /// <summary>A container that can hold one packet for each outbox, used to store
        /// dequeued packets that are being held for throttling</summary>
        private readonly OutgoingPacket[] m_nextPackets = new OutgoingPacket[THROTTLE_CATEGORY_COUNT];
        /// <summary>A reference to the LLUDPServer that is managing this client</summary>
        private readonly LLUDPServer m_udpServer;

        /// <summary>Caches packed throttle information</summary>
        private byte[] m_packedThrottles;

        private int m_defaultRTO = 1000; // 1sec is the recommendation in the RFC
        private int m_maxRTO = 60000;

        /// <summary>
        /// This is the percentage of the udp texture queue to add to the task queue since
        /// textures are now generally handled through http.
        /// </summary>
        private double m_cannibalrate = 0.0;
        
        private ClientInfo m_info = new ClientInfo();

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="server">Reference to the UDP server this client is connected to</param>
        /// <param name="rates">Default throttling rates and maximum throttle limits</param>
        /// <param name="parentThrottle">Parent HTB (hierarchical token bucket)
        /// that the child throttles will be governed by</param>
        /// <param name="circuitCode">Circuit code for this connection</param>
        /// <param name="agentID">AgentID for the connected agent</param>
        /// <param name="remoteEndPoint">Remote endpoint for this connection</param>
        /// <param name="defaultRTO">
        /// Default retransmission timeout for unacked packets.  The RTO will never drop
        /// beyond this number.
        /// </param>
        /// <param name="maxRTO">
        /// The maximum retransmission timeout for unacked packets.  The RTO will never exceed this number.
        /// </param>
        public LLUDPClient(
            LLUDPServer server, ThrottleRates rates, TokenBucket parentThrottle, uint circuitCode, UUID agentID,
            IPEndPoint remoteEndPoint, int defaultRTO, int maxRTO)
        {
            AgentID = agentID;
            RemoteEndPoint = remoteEndPoint;
            CircuitCode = circuitCode;
            m_udpServer = server;
            if (defaultRTO != 0)
                m_defaultRTO = defaultRTO;
            if (maxRTO != 0)
                m_maxRTO = maxRTO;

            ProcessUnackedSends = true;

            // Create a token bucket throttle for this client that has the scene token bucket as a parent
            m_throttleClient 
                = new AdaptiveTokenBucket(
                    string.Format("adaptive throttle for {0} in {1}", AgentID, server.Scene.Name), 
                    parentThrottle, 0, rates.Total, rates.MinimumAdaptiveThrottleRate, rates.AdaptiveThrottlesEnabled);

            // Create an array of token buckets for this clients different throttle categories
            m_throttleCategories = new TokenBucket[THROTTLE_CATEGORY_COUNT];

            m_cannibalrate = rates.CannibalizeTextureRate;
            
            for (int i = 0; i < THROTTLE_CATEGORY_COUNT; i++)
            {
                ThrottleOutPacketType type = (ThrottleOutPacketType)i;

                // Initialize the packet outboxes, where packets sit while they are waiting for tokens
                m_packetOutboxes[i] = new OpenSim.Framework.LocklessQueue<OutgoingPacket>();

                // Initialize the token buckets that control the throttling for each category
                m_throttleCategories[i]
                    = new TokenBucket(
                        string.Format("{0} throttle for {1} in {2}", type, AgentID, server.Scene.Name), 
                    m_throttleClient, rates.GetRate(type), 0);
            }

            // Default the retransmission timeout to one second
            RTO = m_defaultRTO;

            // Initialize this to a sane value to prevent early disconnects
            TickLastPacketReceived = Environment.TickCount & Int32.MaxValue;
        }

        /// <summary>
        /// Shuts down this client connection
        /// </summary>
        public void Shutdown()
        {
            IsConnected = false;
            for (int i = 0; i < THROTTLE_CATEGORY_COUNT; i++)
            {
                m_packetOutboxes[i].Clear();
                m_nextPackets[i] = null;
            }

            // pull the throttle out of the scene throttle
            m_throttleClient.Parent.UnregisterRequest(m_throttleClient);
            OnPacketStats = null;
            OnQueueEmpty = null;
        }

        /// <summary>
        /// Gets information about this client connection
        /// </summary>
        /// <returns>Information about the client connection</returns>
        public ClientInfo GetClientInfo()
        {
            // TODO: This data structure is wrong in so many ways. Locking and copying the entire lists
            // of pending and needed ACKs for every client every time some method wants information about
            // this connection is a recipe for poor performance

            m_info.resendThrottle = (int)m_throttleCategories[(int)ThrottleOutPacketType.Resend].DripRate;
            m_info.landThrottle = (int)m_throttleCategories[(int)ThrottleOutPacketType.Land].DripRate;
            m_info.windThrottle = (int)m_throttleCategories[(int)ThrottleOutPacketType.Wind].DripRate;
            m_info.cloudThrottle = (int)m_throttleCategories[(int)ThrottleOutPacketType.Cloud].DripRate;
            m_info.taskThrottle = (int)m_throttleCategories[(int)ThrottleOutPacketType.Task].DripRate;
            m_info.assetThrottle = (int)m_throttleCategories[(int)ThrottleOutPacketType.Asset].DripRate;
            m_info.textureThrottle = (int)m_throttleCategories[(int)ThrottleOutPacketType.Texture].DripRate;
            m_info.totalThrottle = (int)m_throttleClient.DripRate;
            m_info.targetThrottle = (int)m_throttleClient.TargetDripRate;
            m_info.maxThrottle = (int)m_throttleClient.MaxDripRate;

            return m_info;
        }

        /// <summary>
        /// Modifies the UDP throttles
        /// </summary>
        /// <param name="info">New throttling values</param>
        public void SetClientInfo(ClientInfo info)
        {
            // TODO: Allowing throttles to be manually set from this function seems like a reasonable
            // idea. On the other hand, letting external code manipulate our ACK accounting is not
            // going to happen
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the total number of pakcets queued for this client.
        /// </summary>
        /// <returns></returns>
        public int GetTotalPacketsQueuedCount()
        {
            int total = 0;

            for (int i = 0; i <= (int)ThrottleOutPacketType.Asset; i++)
                total += m_packetOutboxes[i].Count;

            return total;
        }

        /// <summary>
        /// Get the number of packets queued for the given throttle type.
        /// </summary>
        /// <returns></returns>
        /// <param name="throttleType"></param>
        public int GetPacketsQueuedCount(ThrottleOutPacketType throttleType)
        {
            if ((int)throttleType > 0)
                return m_packetOutboxes[(int)throttleType].Count;
            else
                return 0;
        }

        /// <summary>
        /// Return statistics information about client packet queues.
        /// </summary>
        /// <remarks>
        /// FIXME: This should really be done in a more sensible manner rather than sending back a formatted string.
        /// </remarks>
        /// <returns></returns>
        public string GetStats()
        {
            return string.Format(
                "{0,7} {1,7} {2,7} {3,9} {4,7} {5,7} {6,7} {7,7} {8,7} {9,8} {10,7} {11,7}",
                Util.EnvironmentTickCountSubtract(TickLastPacketReceived),
                PacketsReceived,                                 
                PacketsSent,
                PacketsResent,
                UnackedBytes,
                m_packetOutboxes[(int)ThrottleOutPacketType.Resend].Count,
                m_packetOutboxes[(int)ThrottleOutPacketType.Land].Count,
                m_packetOutboxes[(int)ThrottleOutPacketType.Wind].Count,
                m_packetOutboxes[(int)ThrottleOutPacketType.Cloud].Count,
                m_packetOutboxes[(int)ThrottleOutPacketType.Task].Count,
                m_packetOutboxes[(int)ThrottleOutPacketType.Texture].Count,
                m_packetOutboxes[(int)ThrottleOutPacketType.Asset].Count);
        }

        public void SendPacketStats()
        {
            PacketStats callback = OnPacketStats;
            if (callback != null)
            {
                int newPacketsReceived = PacketsReceived - m_packetsReceivedReported;
                int newPacketsSent = PacketsSent - m_packetsSentReported;

                callback(newPacketsReceived, newPacketsSent, UnackedBytes);

                m_packetsReceivedReported += newPacketsReceived;
                m_packetsSentReported += newPacketsSent;
            }
        }

        public void SetThrottles(byte[] throttleData)
        {
            byte[] adjData;
            int pos = 0;

            if (!BitConverter.IsLittleEndian)
            {
                byte[] newData = new byte[7 * 4];
                Buffer.BlockCopy(throttleData, 0, newData, 0, 7 * 4);

                for (int i = 0; i < 7; i++)
                    Array.Reverse(newData, i * 4, 4);

                adjData = newData;
            }
            else
            {
                adjData = throttleData;
            }

            // 0.125f converts from bits to bytes
            int resend = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int land = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int wind = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int cloud = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int task = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int texture = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int asset = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f);

            if (ThrottleDebugLevel > 0)
            {
                long total = resend + land + wind + cloud + task + texture + asset;
                m_log.DebugFormat(
                    "[LLUDPCLIENT]: {0} is setting throttles in {1} to Resend={2}, Land={3}, Wind={4}, Cloud={5}, Task={6}, Texture={7}, Asset={8}, TOTAL = {9}",
                    AgentID, m_udpServer.Scene.Name, resend, land, wind, cloud, task, texture, asset, total);
            }

            // Make sure none of the throttles are set below our packet MTU,
            // otherwise a throttle could become permanently clogged
            resend = Math.Max(resend, LLUDPServer.MTU);
            land = Math.Max(land, LLUDPServer.MTU);
            wind = Math.Max(wind, LLUDPServer.MTU);
            cloud = Math.Max(cloud, LLUDPServer.MTU);
            task = Math.Max(task, LLUDPServer.MTU);
            texture = Math.Max(texture, LLUDPServer.MTU);
            asset = Math.Max(asset, LLUDPServer.MTU);

            // Since most textures are now delivered through http, make it possible
            // to cannibalize some of the bw from the texture throttle to use for
            // the task queue (e.g. object updates)
            task = task + (int)(m_cannibalrate * texture);
            texture = (int)((1 - m_cannibalrate) * texture);
            
            //int total = resend + land + wind + cloud + task + texture + asset;

            if (ThrottleDebugLevel > 0)
            {
                long total = resend + land + wind + cloud + task + texture + asset;
                m_log.DebugFormat(
                    "[LLUDPCLIENT]: {0} is setting throttles in {1} to Resend={2}, Land={3}, Wind={4}, Cloud={5}, Task={6}, Texture={7}, Asset={8}, TOTAL = {9}",
                    AgentID, m_udpServer.Scene.Name, resend, land, wind, cloud, task, texture, asset, total);
            }

            // Update the token buckets with new throttle values
            if (m_throttleClient.AdaptiveEnabled)
            {
                long total = resend + land + wind + cloud + task + texture + asset;
                m_throttleClient.TargetDripRate = total;
            }

            TokenBucket bucket;

            bucket = m_throttleCategories[(int)ThrottleOutPacketType.Resend];
            bucket.RequestedDripRate = resend;

            bucket = m_throttleCategories[(int)ThrottleOutPacketType.Land];
            bucket.RequestedDripRate = land;

            bucket = m_throttleCategories[(int)ThrottleOutPacketType.Wind];
            bucket.RequestedDripRate = wind;

            bucket = m_throttleCategories[(int)ThrottleOutPacketType.Cloud];
            bucket.RequestedDripRate = cloud;

            bucket = m_throttleCategories[(int)ThrottleOutPacketType.Asset];
            bucket.RequestedDripRate = asset;

            bucket = m_throttleCategories[(int)ThrottleOutPacketType.Task];
            bucket.RequestedDripRate = task;

            bucket = m_throttleCategories[(int)ThrottleOutPacketType.Texture];
            bucket.RequestedDripRate = texture;

            // Reset the packed throttles cached data
            m_packedThrottles = null;
        }

        public byte[] GetThrottlesPacked(float multiplier)
        {
            byte[] data = m_packedThrottles;

            if (data == null)
            {
                float rate;

                data = new byte[7 * 4];
                int i = 0;

                // multiply by 8 to convert bytes back to bits
                rate = (float)m_throttleCategories[(int)ThrottleOutPacketType.Resend].RequestedDripRate * 8 * multiplier;
                Buffer.BlockCopy(Utils.FloatToBytes(rate), 0, data, i, 4); i += 4;

                rate = (float)m_throttleCategories[(int)ThrottleOutPacketType.Land].RequestedDripRate * 8 * multiplier;
                Buffer.BlockCopy(Utils.FloatToBytes(rate), 0, data, i, 4); i += 4;

                rate = (float)m_throttleCategories[(int)ThrottleOutPacketType.Wind].RequestedDripRate * 8 * multiplier;
                Buffer.BlockCopy(Utils.FloatToBytes(rate), 0, data, i, 4); i += 4;

                rate = (float)m_throttleCategories[(int)ThrottleOutPacketType.Cloud].RequestedDripRate * 8 * multiplier;
                Buffer.BlockCopy(Utils.FloatToBytes(rate), 0, data, i, 4); i += 4;

                rate = (float)m_throttleCategories[(int)ThrottleOutPacketType.Task].RequestedDripRate * 8 * multiplier;
                Buffer.BlockCopy(Utils.FloatToBytes(rate), 0, data, i, 4); i += 4;

                rate = (float)m_throttleCategories[(int)ThrottleOutPacketType.Texture].RequestedDripRate * 8 * multiplier;
                Buffer.BlockCopy(Utils.FloatToBytes(rate), 0, data, i, 4); i += 4;

                rate = (float)m_throttleCategories[(int)ThrottleOutPacketType.Asset].RequestedDripRate * 8 * multiplier;
                Buffer.BlockCopy(Utils.FloatToBytes(rate), 0, data, i, 4); i += 4;

                m_packedThrottles = data;
            }

            return data;
        }

        /// <summary>
        /// Queue an outgoing packet if appropriate.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="forceQueue">Always queue the packet if at all possible.</param>
        /// <returns>
        /// true if the packet has been queued, 
        /// false if the packet has not been queued and should be sent immediately.
        /// </returns>
        public bool EnqueueOutgoing(OutgoingPacket packet, bool forceQueue)
        {
            int category = (int)packet.Category;

            if (category >= 0 && category < m_packetOutboxes.Length)
            {
                OpenSim.Framework.LocklessQueue<OutgoingPacket> queue = m_packetOutboxes[category];
                TokenBucket bucket = m_throttleCategories[category];

                // Don't send this packet if there is already a packet waiting in the queue
                // even if we have the tokens to send it, tokens should go to the already
                // queued packets
                if (queue.Count > 0)
                {
                    queue.Enqueue(packet);
                    return true;
                }
                
                    
                if (!forceQueue && bucket.RemoveTokens(packet.Buffer.DataLength))
                {
                    // Enough tokens were removed from the bucket, the packet will not be queued
                    return false;
                }
                else
                {
                    // Force queue specified or not enough tokens in the bucket, queue this packet
                    queue.Enqueue(packet);
                    return true;
                }
            }
            else
            {
                // We don't have a token bucket for this category, so it will not be queued
                return false;
            }
        }

        /// <summary>
        /// Loops through all of the packet queues for this client and tries to send
        /// an outgoing packet from each, obeying the throttling bucket limits
        /// </summary>
        /// 
        /// <remarks>
        /// Packet queues are inspected in ascending numerical order starting from 0.  Therefore, queues with a lower 
        /// ThrottleOutPacketType number will see their packet get sent first (e.g. if both Land and Wind queues have
        /// packets, then the packet at the front of the Land queue will be sent before the packet at the front of the
        /// wind queue).
        /// 
        /// This function is only called from a synchronous loop in the
        /// UDPServer so we don't need to bother making this thread safe
        /// </remarks>
        /// 
        /// <returns>True if any packets were sent, otherwise false</returns>
        public bool DequeueOutgoing()
        {
            OutgoingPacket packet;
            OpenSim.Framework.LocklessQueue<OutgoingPacket> queue;
            TokenBucket bucket;
            bool packetSent = false;
            ThrottleOutPacketTypeFlags emptyCategories = 0;

            //string queueDebugOutput = String.Empty; // Serious debug business

            for (int i = 0; i < THROTTLE_CATEGORY_COUNT; i++)
            {
                bucket = m_throttleCategories[i];
                //queueDebugOutput += m_packetOutboxes[i].Count + " ";  // Serious debug business

                if (m_nextPackets[i] != null)
                {
                    // This bucket was empty the last time we tried to send a packet,
                    // leaving a dequeued packet still waiting to be sent out. Try to
                    // send it again
                    OutgoingPacket nextPacket = m_nextPackets[i];
                    if (bucket.RemoveTokens(nextPacket.Buffer.DataLength))
                    {
                        // Send the packet
                        m_udpServer.SendPacketFinal(nextPacket);
                        m_nextPackets[i] = null;
                        packetSent = true;
                    }
                }
                else
                {
                    // No dequeued packet waiting to be sent, try to pull one off
                    // this queue
                    queue = m_packetOutboxes[i];
                    if (queue.Dequeue(out packet))
                    {
                        // A packet was pulled off the queue. See if we have
                        // enough tokens in the bucket to send it out
                        if (bucket.RemoveTokens(packet.Buffer.DataLength))
                        {
                            // Send the packet
                            m_udpServer.SendPacketFinal(packet);
                            packetSent = true;
                        }
                        else
                        {
                            // Save the dequeued packet for the next iteration
                            m_nextPackets[i] = packet;
                        }

                        // If the queue is empty after this dequeue, fire the queue
                        // empty callback now so it has a chance to fill before we 
                        // get back here
                        if (queue.Count == 0)
                            emptyCategories |= CategoryToFlag(i);
                    }
                    else
                    {
                        // No packets in this queue. Fire the queue empty callback
                        // if it has not been called recently
                        emptyCategories |= CategoryToFlag(i);
                    }
                }
            }

            if (emptyCategories != 0)
                BeginFireQueueEmpty(emptyCategories);

            //m_log.Info("[LLUDPCLIENT]: Queues: " + queueDebugOutput); // Serious debug business
            return packetSent;
        }

        /// <summary>
        /// Called when an ACK packet is received and a round-trip time for a
        /// packet is calculated. This is used to calculate the smoothed
        /// round-trip time, round trip time variance, and finally the
        /// retransmission timeout
        /// </summary>
        /// <param name="r">Round-trip time of a single packet and its
        /// acknowledgement</param>
        public void UpdateRoundTrip(float r)
        {
            const float ALPHA = 0.125f;
            const float BETA = 0.25f;
            const float K = 4.0f;

            if (RTTVAR == 0.0f)
            {
                // First RTT measurement
                SRTT = r;
                RTTVAR = r * 0.5f;
            }
            else
            {
                // Subsequence RTT measurement
                RTTVAR = (1.0f - BETA) * RTTVAR + BETA * Math.Abs(SRTT - r);
                SRTT = (1.0f - ALPHA) * SRTT + ALPHA * r;
            }

            int rto = (int)(SRTT + Math.Max(m_udpServer.TickCountResolution, K * RTTVAR));

            // Clamp the retransmission timeout to manageable values
            rto = Utils.Clamp(rto, m_defaultRTO, m_maxRTO);

            RTO = rto;

            //if (RTO != rto)
       //          m_log.Debug("[LLUDPCLIENT]: Setting RTO to " + RTO + "ms from " + rto + "ms with an RTTVAR of " +
                       //RTTVAR + " based on new RTT of " + r + "ms");
        }

        /// <summary>
        /// Exponential backoff of the retransmission timeout, per section 5.5
        /// of RFC 2988
        /// </summary>
        public void BackoffRTO()
        {
            // Reset SRTT and RTTVAR, we assume they are bogus since things
            // didn't work out and we're backing off the timeout
            SRTT = 0.0f;
            RTTVAR = 0.0f;

            // Double the retransmission timeout
            RTO = Math.Min(RTO * 2, m_maxRTO);
        }

        /// <summary>
        /// Does an early check to see if this queue empty callback is already
        /// running, then asynchronously firing the event
        /// </summary>
        /// <param name="categories">Throttle categories to fire the callback for</param>
        private void BeginFireQueueEmpty(ThrottleOutPacketTypeFlags categories)
        {
//            if (m_nextOnQueueEmpty != 0 && (Environment.TickCount & Int32.MaxValue) >= m_nextOnQueueEmpty)
            if (!m_isQueueEmptyRunning && (Environment.TickCount & Int32.MaxValue) >= m_nextOnQueueEmpty)
            {
                m_isQueueEmptyRunning = true;

                int start = Environment.TickCount & Int32.MaxValue;
                const int MIN_CALLBACK_MS = 30;

                m_nextOnQueueEmpty = start + MIN_CALLBACK_MS;
                if (m_nextOnQueueEmpty == 0)
                    m_nextOnQueueEmpty = 1;

                // Use a value of 0 to signal that FireQueueEmpty is running
//                m_nextOnQueueEmpty = 0;

                m_categories = categories;

                if (HasUpdates(m_categories))
                {
                    if (!m_udpServer.OqrEngine.IsRunning)
                    {
                        // Asynchronously run the callback
                        Util.FireAndForget(FireQueueEmpty, categories, "LLUDPClient.BeginFireQueueEmpty");
                    }
                    else
                    {
                        m_udpServer.OqrEngine.QueueJob(AgentID.ToString(), () => FireQueueEmpty(categories));
                    }
                }
                else
                {
                    m_isQueueEmptyRunning = false;
                }
            }
        }

        private bool m_isQueueEmptyRunning;
        private ThrottleOutPacketTypeFlags m_categories = 0;

        /// <summary>
        /// Fires the OnQueueEmpty callback and sets the minimum time that it
        /// can be called again
        /// </summary>
        /// <param name="o">Throttle categories to fire the callback for,
        /// stored as an object to match the WaitCallback delegate
        /// signature</param>
        public void FireQueueEmpty(object o)
        {
//            m_log.DebugFormat("[LLUDPCLIENT]: FireQueueEmpty for {0} in {1}", AgentID, m_udpServer.Scene.Name);

//            int start = Environment.TickCount & Int32.MaxValue;
//            const int MIN_CALLBACK_MS = 30;

//            if (m_udpServer.IsRunningOutbound)
//            {        
                ThrottleOutPacketTypeFlags categories = (ThrottleOutPacketTypeFlags)o;
                QueueEmpty callback = OnQueueEmpty;                      

                if (callback != null)
                {
//                    if (m_udpServer.IsRunningOutbound)
//                    {                
                        try { callback(categories); }
                        catch (Exception e) { m_log.Error("[LLUDPCLIENT]: OnQueueEmpty(" + categories + ") threw an exception: " + e.Message, e); }
//                    }
                }
//            }

//            m_nextOnQueueEmpty = start + MIN_CALLBACK_MS;
//            if (m_nextOnQueueEmpty == 0)
//                m_nextOnQueueEmpty = 1;

//            }

            m_isQueueEmptyRunning = false;
        }

        /// <summary>
        /// Converts a <seealso cref="ThrottleOutPacketType"/> integer to a
        /// flag value
        /// </summary>
        /// <param name="i">Throttle category to convert</param>
        /// <returns>Flag representation of the throttle category</returns>
        private static ThrottleOutPacketTypeFlags CategoryToFlag(int i)
        {
            ThrottleOutPacketType category = (ThrottleOutPacketType)i;

            /*
             * Land = 1,
        /// <summary>Wind data</summary>
        Wind = 2,
        /// <summary>Cloud data</summary>
        Cloud = 3,
        /// <summary>Any packets that do not fit into the other throttles</summary>
        Task = 4,
        /// <summary>Texture assets</summary>
        Texture = 5,
        /// <summary>Non-texture assets</summary>
        Asset = 6,
             */

            switch (category)
            {
                case ThrottleOutPacketType.Land:
                    return ThrottleOutPacketTypeFlags.Land;
                case ThrottleOutPacketType.Wind:
                    return ThrottleOutPacketTypeFlags.Wind;
                case ThrottleOutPacketType.Cloud:
                    return ThrottleOutPacketTypeFlags.Cloud;
                case ThrottleOutPacketType.Task:
                    return ThrottleOutPacketTypeFlags.Task;
                case ThrottleOutPacketType.Texture:
                    return ThrottleOutPacketTypeFlags.Texture;
                case ThrottleOutPacketType.Asset:
                    return ThrottleOutPacketTypeFlags.Asset;
                default:
                    return 0;
            }
        }
    }
}
