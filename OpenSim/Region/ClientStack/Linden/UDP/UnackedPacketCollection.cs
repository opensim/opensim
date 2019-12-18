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
using OpenMetaverse;

//using System.Reflection;
//using log4net;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    /// <summary>
    /// Special collection that is optimized for tracking unacknowledged packets
    /// </summary>
    public sealed class UnackedPacketCollection
    {
        /// <summary>
        /// Holds information about a pending acknowledgement
        /// </summary>
        private struct PendingAck
        {
            /// <summary>Sequence number of the packet to remove</summary>
            public uint SequenceNumber;
            /// <summary>Environment.TickCount value when the remove was queued.
            /// This is used to update round-trip times for packets</summary>
            public int RemoveTime;
            /// <summary>Whether or not this acknowledgement was attached to a
            /// resent packet. If so, round-trip time will not be calculated</summary>
            public bool FromResend;

            public PendingAck(uint sequenceNumber, int currentTime, bool fromResend)
            {
                SequenceNumber = sequenceNumber;
                RemoveTime = currentTime;
                FromResend = fromResend;
            }
        }

        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>Holds the actual unacked packet data, sorted by sequence number</summary>
        private SortedDictionary<uint, OutgoingPacket> m_packets = new SortedDictionary<uint, OutgoingPacket>();
        /// <summary>Holds packets that need to be added to the unacknowledged list</summary>
        private LocklessQueue<OutgoingPacket> m_pendingAdds = new LocklessQueue<OutgoingPacket>();
        /// <summary>Holds information about pending acknowledgements</summary>
        private LocklessQueue<PendingAck> m_pendingAcknowledgements = new LocklessQueue<PendingAck>();
        /// <summary>Holds information about pending removals</summary>
        private LocklessQueue<uint> m_pendingRemoves = new LocklessQueue<uint>();
        private uint m_older;
        public void Clear()
        {
            m_packets.Clear();
            m_pendingAdds = null;
            m_pendingAcknowledgements = null;
            m_pendingRemoves = null;
            m_older = 0;
        }

        public int Count()
        {
            return m_packets.Count + m_pendingAdds.Count - m_pendingAcknowledgements.Count - m_pendingRemoves.Count;
        }

        public uint Oldest()
        {
            return m_older;
        }

        /// <summary>
        /// Add an unacked packet to the collection
        /// </summary>
        /// <param name="packet">Packet that is awaiting acknowledgement</param>
        /// <returns>True if the packet was successfully added, false if the
        /// packet already existed in the collection</returns>
        /// <remarks>This does not immediately add the ACK to the collection,
        /// it only queues it so it can be added in a thread-safe way later</remarks>
        public void Add(OutgoingPacket packet)
        {
            m_pendingAdds.Enqueue(packet);
            Interlocked.Add(ref packet.Client.UnackedBytes, packet.Buffer.DataLength);
        }

        /// <summary>
        /// Marks a packet as acknowledged
        /// This method is used when an acknowledgement is received from the network for a previously
        /// sent packet. Effects of removal this way are to update unacked byte count, adjust RTT
        /// and increase throttle to the coresponding client.
        /// </summary>
        /// <param name="sequenceNumber">Sequence number of the packet to
        /// acknowledge</param>
        /// <param name="currentTime">Current value of Environment.TickCount</param>
        /// <remarks>This does not immediately acknowledge the packet, it only
        /// queues the ack so it can be handled in a thread-safe way later</remarks>
        public void Acknowledge(uint sequenceNumber, int currentTime, bool fromResend)
        {
            m_pendingAcknowledgements.Enqueue(new PendingAck(sequenceNumber, currentTime, fromResend));
        }

        /// <summary>
        /// Marks a packet as no longer needing acknowledgement without a received acknowledgement.
        /// This method is called when a packet expires and we no longer need an acknowledgement.
        /// When some reliable packet types expire, they are handled in a way other than simply
        /// resending them. The only effect of removal this way is to update unacked byte count.
        /// </summary>
        /// <param name="sequenceNumber">Sequence number of the packet to
        /// acknowledge</param>
        /// <remarks>The does not immediately remove the packet, it only queues the removal
        /// so it can be handled in a thread safe way later</remarks>
        public void Remove(uint sequenceNumber)
        {
            m_pendingRemoves.Enqueue(sequenceNumber);
        }

        /// <summary>
        /// Returns a list of all of the packets with a TickCount older than
        /// the specified timeout
        /// </summary>
        /// <remarks>
        /// This function is not thread safe, and cannot be called
        /// multiple times concurrently
        /// </remarks>
        /// <param name="timeoutMS">Number of ticks (milliseconds) before a
        /// packet is considered expired
        /// </param>
        /// <returns>
        /// A list of all expired packets according to the given
        /// expiration timeout
        /// </returns>
        public List<OutgoingPacket> GetExpiredPackets(int timeoutMS)
        {
            ProcessQueues();

            List<OutgoingPacket> expiredPackets = null;
            bool doolder = true;
            if (m_packets.Count > 0)
            {
                int now = Environment.TickCount & Int32.MaxValue;

                foreach (OutgoingPacket packet in m_packets.Values)
                {
                    if(packet.Buffer == null)
                    {
                        Remove(packet.SequenceNumber);
                        continue;
                    }

                    if(doolder)
                    {
                        m_older = packet.SequenceNumber;
                        doolder = false;
                    }

                    // TickCount of zero means a packet is in the resend queue
                    // but hasn't actually been sent over the wire yet
                    int ptime = Interlocked.Exchange(ref packet.TickCount, 0);
                    if (ptime == 0)
                        continue;

                    if(now - ptime < timeoutMS)
                    {
                        int t = Interlocked.Exchange(ref packet.TickCount, ptime);
                        if (t > ptime)
                            Interlocked.Exchange(ref packet.TickCount, t);
                        continue;
                    }

                    if (expiredPackets == null)
                        expiredPackets = new List<OutgoingPacket>();

                    // As with other network applications, assume that an expired packet is
                    // an indication of some network problem, slow transmission
                    packet.Client.FlowThrottle.ExpirePackets(1);

                    expiredPackets.Add(packet);
                }
            }

            // if (expiredPackets != null)
            //     m_log.DebugFormat("[UNACKED PACKET COLLECTION]: Found {0} expired packets on timeout of {1}", expiredPackets.Count, timeoutMS);

            return expiredPackets;
        }

        private void ProcessQueues()
        {
            while (m_pendingRemoves.TryDequeue(out uint pendingRemove))
            {
                if (m_packets.TryGetValue(pendingRemove, out OutgoingPacket removedPacket))
                {
                    m_packets.Remove(pendingRemove);
                    if (removedPacket != null)
                    {
                        // Update stats
                        Interlocked.Add(ref removedPacket.Client.UnackedBytes, -removedPacket.Buffer.DataLength);

                        removedPacket.Client.FreeUDPBuffer(removedPacket.Buffer);
                        removedPacket.Buffer = null;
                    }
                }
            }

            // Process all the pending adds
            while (m_pendingAdds.TryDequeue(out OutgoingPacket pendingAdd))
            {
                if (pendingAdd != null)
                    m_packets[pendingAdd.SequenceNumber] = pendingAdd;
            }

            // Process all the pending removes, including updating statistics and round-trip times
            while (m_pendingAcknowledgements.TryDequeue(out PendingAck pendingAcknowledgement))
            {
                //m_log.DebugFormat("[UNACKED PACKET COLLECTION]: Processing ack {0}", pendingAcknowledgement.SequenceNumber);
                if (m_packets.TryGetValue(pendingAcknowledgement.SequenceNumber, out OutgoingPacket ackedPacket))
                {
                    m_packets.Remove(pendingAcknowledgement.SequenceNumber);
                    if (ackedPacket != null)
                    {
                        // Update stats
                        if(ackedPacket.Buffer != null)
                        {
                            Interlocked.Add(ref ackedPacket.Client.UnackedBytes, -ackedPacket.Buffer.DataLength);

                            ackedPacket.Client.FreeUDPBuffer(ackedPacket.Buffer);
                            ackedPacket.Buffer = null;
                        }
                        // As with other network applications, assume that an acknowledged packet is an
                        // indication that the network can handle a little more load, speed up the transmission
                        ackedPacket.Client.FlowThrottle.AcknowledgePackets(1);
                    }
                }
            }
        }
    }
}