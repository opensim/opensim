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
using OpenSim.Framework;
using OpenMetaverse;

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
    /// Fired when the queue for a packet category is empty. This event can be
    /// hooked to put more data on the empty queue
    /// </summary>
    /// <param name="category">Category of the packet queue that is empty</param>
    public delegate void QueueEmpty(ThrottleOutPacketType category);

    #endregion Delegates

    /// <summary>
    /// Tracks state for a client UDP connection and provides client-specific methods
    /// </summary>
    public sealed class LLUDPClient
    {
        /// <summary>The number of packet categories to throttle on. If a throttle category is added
        /// or removed, this number must also change</summary>
        const int THROTTLE_CATEGORY_COUNT = 7;

        /// <summary>Fired when updated networking stats are produced for this client</summary>
        public event PacketStats OnPacketStats;
        /// <summary>Fired when the queue for a packet category is empty. This event can be
        /// hooked to put more data on the empty queue</summary>
        public event QueueEmpty OnQueueEmpty;

        /// <summary>AgentID for this client</summary>
        public readonly UUID AgentID;
        /// <summary>The remote address of the connected client</summary>
        public readonly IPEndPoint RemoteEndPoint;
        /// <summary>Circuit code that this client is connected on</summary>
        public readonly uint CircuitCode;
        /// <summary>Sequence numbers of packets we've received (for duplicate checking)</summary>
        public readonly IncomingPacketHistoryCollection PacketArchive = new IncomingPacketHistoryCollection(200);
        /// <summary>Packets we have sent that need to be ACKed by the client</summary>
        public readonly UnackedPacketCollection NeedAcks = new UnackedPacketCollection();
        /// <summary>ACKs that are queued up, waiting to be sent to the client</summary>
        public readonly LocklessQueue<uint> PendingAcks = new LocklessQueue<uint>();

        /// <summary>Reference to the IClientAPI for this client</summary>
        public LLClientView ClientAPI;
        /// <summary>Current packet sequence number</summary>
        public int CurrentSequence;
        /// <summary>Current ping sequence number</summary>
        public byte CurrentPingSequence;
        /// <summary>True when this connection is alive, otherwise false</summary>
        public bool IsConnected = true;
        /// <summary>True when this connection is paused, otherwise false</summary>
        public bool IsPaused = true;
        /// <summary>Environment.TickCount when the last packet was received for this client</summary>
        public int TickLastPacketReceived;

        /// <summary>Timer granularity. This is set to the measured resolution of Environment.TickCount</summary>
        public readonly float G;
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
        /// <summary>Total byte count of unacked packets sent to this client</summary>
        public int UnackedBytes;

        /// <summary>Total number of received packets that we have reported to the OnPacketStats event(s)</summary>
        private int m_packetsReceivedReported;
        /// <summary>Total number of sent packets that we have reported to the OnPacketStats event(s)</summary>
        private int m_packetsSentReported;

        /// <summary>Throttle bucket for this agent's connection</summary>
        private readonly TokenBucket throttle;
        /// <summary>Throttle buckets for each packet category</summary>
        private readonly TokenBucket[] throttleCategories;
        /// <summary>Throttle rate defaults and limits</summary>
        private readonly ThrottleRates defaultThrottleRates;
        /// <summary>Outgoing queues for throttled packets</summary>
        private readonly LocklessQueue<OutgoingPacket>[] packetOutboxes = new LocklessQueue<OutgoingPacket>[THROTTLE_CATEGORY_COUNT];
        /// <summary>A container that can hold one packet for each outbox, used to store
        /// dequeued packets that are being held for throttling</summary>
        private readonly OutgoingPacket[] nextPackets = new OutgoingPacket[THROTTLE_CATEGORY_COUNT];
        /// <summary>An optimization to store the length of dequeued packets being held
        /// for throttling. This avoids expensive calls to Packet.Length</summary>
        private readonly int[] nextPacketLengths = new int[THROTTLE_CATEGORY_COUNT];
        /// <summary>A reference to the LLUDPServer that is managing this client</summary>
        private readonly LLUDPServer udpServer;

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
        public LLUDPClient(LLUDPServer server, ThrottleRates rates, TokenBucket parentThrottle, uint circuitCode, UUID agentID, IPEndPoint remoteEndPoint)
        {
            udpServer = server;
            AgentID = agentID;
            RemoteEndPoint = remoteEndPoint;
            CircuitCode = circuitCode;
            defaultThrottleRates = rates;

            for (int i = 0; i < THROTTLE_CATEGORY_COUNT; i++)
                packetOutboxes[i] = new LocklessQueue<OutgoingPacket>();

            throttle = new TokenBucket(parentThrottle, 0, 0);
            throttleCategories = new TokenBucket[THROTTLE_CATEGORY_COUNT];
            throttleCategories[(int)ThrottleOutPacketType.Resend] = new TokenBucket(throttle, rates.ResendLimit, rates.Resend);
            throttleCategories[(int)ThrottleOutPacketType.Land] = new TokenBucket(throttle, rates.LandLimit, rates.Land);
            throttleCategories[(int)ThrottleOutPacketType.Wind] = new TokenBucket(throttle, rates.WindLimit, rates.Wind);
            throttleCategories[(int)ThrottleOutPacketType.Cloud] = new TokenBucket(throttle, rates.CloudLimit, rates.Cloud);
            throttleCategories[(int)ThrottleOutPacketType.Task] = new TokenBucket(throttle, rates.TaskLimit, rates.Task);
            throttleCategories[(int)ThrottleOutPacketType.Texture] = new TokenBucket(throttle, rates.TextureLimit, rates.Texture);
            throttleCategories[(int)ThrottleOutPacketType.Asset] = new TokenBucket(throttle, rates.AssetLimit, rates.Asset);

            // Set the granularity variable used for retransmission calculations to
            // the measured resolution of Environment.TickCount
            G = server.TickCountResolution;

            // Default the retransmission timeout to three seconds
            RTO = 3000;
        }

        /// <summary>
        /// Shuts down this client connection
        /// </summary>
        public void Shutdown()
        {
            // TODO: Do we need to invalidate the circuit?
            IsConnected = false;
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
            ClientInfo info = new ClientInfo();
            info.pendingAcks = new Dictionary<uint, uint>();
            info.needAck = new Dictionary<uint, byte[]>();

            info.resendThrottle = throttleCategories[(int)ThrottleOutPacketType.Resend].DripRate;
            info.landThrottle = throttleCategories[(int)ThrottleOutPacketType.Land].DripRate;
            info.windThrottle = throttleCategories[(int)ThrottleOutPacketType.Wind].DripRate;
            info.cloudThrottle = throttleCategories[(int)ThrottleOutPacketType.Cloud].DripRate;
            info.taskThrottle = throttleCategories[(int)ThrottleOutPacketType.Task].DripRate;
            info.assetThrottle = throttleCategories[(int)ThrottleOutPacketType.Asset].DripRate;
            info.textureThrottle = throttleCategories[(int)ThrottleOutPacketType.Texture].DripRate;
            info.totalThrottle = info.resendThrottle + info.landThrottle + info.windThrottle + info.cloudThrottle +
                info.taskThrottle + info.assetThrottle + info.textureThrottle;

            return info;
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

        public string GetStats()
        {
            // TODO: ???
            return string.Format("{0,7} {1,7} {2,7} {3,7} {4,7} {5,7} {6,7} {7,7} {8,7} {9,7}",
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
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

            int resend = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int land = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int wind = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int cloud = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int task = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int texture = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int asset = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f);

            resend = (resend <= defaultThrottleRates.ResendLimit) ? resend : defaultThrottleRates.ResendLimit;
            land = (land <= defaultThrottleRates.LandLimit) ? land : defaultThrottleRates.LandLimit;
            wind = (wind <= defaultThrottleRates.WindLimit) ? wind : defaultThrottleRates.WindLimit;
            cloud = (cloud <= defaultThrottleRates.CloudLimit) ? cloud : defaultThrottleRates.CloudLimit;
            task = (task <= defaultThrottleRates.TaskLimit) ? task : defaultThrottleRates.TaskLimit;
            texture = (texture <= defaultThrottleRates.TextureLimit) ? texture : defaultThrottleRates.TextureLimit;
            asset = (asset <= defaultThrottleRates.AssetLimit) ? asset : defaultThrottleRates.AssetLimit;

            SetThrottle(ThrottleOutPacketType.Resend, resend);
            SetThrottle(ThrottleOutPacketType.Land, land);
            SetThrottle(ThrottleOutPacketType.Wind, wind);
            SetThrottle(ThrottleOutPacketType.Cloud, cloud);
            SetThrottle(ThrottleOutPacketType.Task, task);
            SetThrottle(ThrottleOutPacketType.Texture, texture);
            SetThrottle(ThrottleOutPacketType.Asset, asset);
        }

        public byte[] GetThrottlesPacked()
        {
            byte[] data = new byte[7 * 4];
            int i = 0;

            Buffer.BlockCopy(Utils.FloatToBytes((float)throttleCategories[(int)ThrottleOutPacketType.Resend].DripRate), 0, data, i, 4); i += 4;
            Buffer.BlockCopy(Utils.FloatToBytes((float)throttleCategories[(int)ThrottleOutPacketType.Land].DripRate), 0, data, i, 4); i += 4;
            Buffer.BlockCopy(Utils.FloatToBytes((float)throttleCategories[(int)ThrottleOutPacketType.Wind].DripRate), 0, data, i, 4); i += 4;
            Buffer.BlockCopy(Utils.FloatToBytes((float)throttleCategories[(int)ThrottleOutPacketType.Cloud].DripRate), 0, data, i, 4); i += 4;
            Buffer.BlockCopy(Utils.FloatToBytes((float)throttleCategories[(int)ThrottleOutPacketType.Task].DripRate), 0, data, i, 4); i += 4;
            Buffer.BlockCopy(Utils.FloatToBytes((float)throttleCategories[(int)ThrottleOutPacketType.Texture].DripRate), 0, data, i, 4); i += 4;
            Buffer.BlockCopy(Utils.FloatToBytes((float)throttleCategories[(int)ThrottleOutPacketType.Asset].DripRate), 0, data, i, 4); i += 4;

            return data;
        }

        public void SetThrottle(ThrottleOutPacketType category, int rate)
        {
            int i = (int)category;
            if (i >= 0 && i < throttleCategories.Length)
            {
                TokenBucket bucket = throttleCategories[(int)category];
                bucket.MaxBurst = rate;
                bucket.DripRate = rate;
            }
        }

        public bool EnqueueOutgoing(OutgoingPacket packet)
        {
            int category = (int)packet.Category;

            if (category >= 0 && category < packetOutboxes.Length)
            {
                LocklessQueue<OutgoingPacket> queue = packetOutboxes[category];
                TokenBucket bucket = throttleCategories[category];

                if (throttleCategories[category].RemoveTokens(packet.Buffer.DataLength))
                {
                    // Enough tokens were removed from the bucket, the packet will not be queued
                    return false;
                }
                else
                {
                    // Not enough tokens in the bucket, queue this packet
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
        /// any outgoing packets, obeying the throttling bucket limits
        /// </summary>
        /// <remarks>This function is only called from a synchronous loop in the
        /// UDPServer so we don't need to bother making this thread safe</remarks>
        /// <returns>True if any packets were sent, otherwise false</returns>
        public bool DequeueOutgoing()
        {
            OutgoingPacket packet;
            LocklessQueue<OutgoingPacket> queue;
            TokenBucket bucket;
            bool packetSent = false;

            for (int i = 0; i < THROTTLE_CATEGORY_COUNT; i++)
            {
                bucket = throttleCategories[i];

                if (nextPackets[i] != null)
                {
                    // This bucket was empty the last time we tried to send a packet,
                    // leaving a dequeued packet still waiting to be sent out. Try to
                    // send it again
                    if (bucket.RemoveTokens(nextPacketLengths[i]))
                    {
                        // Send the packet
                        udpServer.SendPacketFinal(nextPackets[i]);
                        nextPackets[i] = null;
                        packetSent = true;
                    }
                }
                else
                {
                    // No dequeued packet waiting to be sent, try to pull one off
                    // this queue
                    queue = packetOutboxes[i];
                    if (queue.Dequeue(out packet))
                    {
                        // A packet was pulled off the queue. See if we have
                        // enough tokens in the bucket to send it out
                        if (bucket.RemoveTokens(packet.Buffer.DataLength))
                        {
                            // Send the packet
                            udpServer.SendPacketFinal(packet);
                            packetSent = true;
                        }
                        else
                        {
                            // Save the dequeued packet and the length calculation for
                            // the next iteration
                            nextPackets[i] = packet;
                            nextPacketLengths[i] = packet.Buffer.DataLength;
                        }
                    }
                    else
                    {
                        // No packets in this queue. Fire the queue empty callback
                        QueueEmpty callback = OnQueueEmpty;
                        if (callback != null)
                            callback((ThrottleOutPacketType)i);
                    }
                }
            }

            return packetSent;
        }

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

            // Always round retransmission timeout up to two seconds
            RTO = Math.Max(2000, (int)(SRTT + Math.Max(G, K * RTTVAR)));
            //Logger.Debug("Setting agent " + this.Agent.FullName + "'s RTO to " + RTO + "ms with an RTTVAR of " +
            //    RTTVAR + " based on new RTT of " + r + "ms");
        }
    }
}
