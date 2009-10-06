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
using OpenMetaverse;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    /// <summary>
    /// Special collection that is optimized for tracking unacknowledged packets
    /// </summary>
    public sealed class UnackedPacketCollection
    {
        /// <summary>Synchronization primitive. A lock must be acquired on this
        /// object before calling any of the unsafe methods</summary>
        public object SyncRoot = new object();

        /// <summary>Holds the actual unacked packet data, sorted by sequence number</summary>
        private SortedDictionary<uint, OutgoingPacket> packets = new SortedDictionary<uint, OutgoingPacket>();

        /// <summary>Gets the total number of unacked packets</summary>
        public int Count { get { return packets.Count; } }

        /// <summary>
        /// Default constructor
        /// </summary>
        public UnackedPacketCollection()
        {
        }

        /// <summary>
        /// Add an unacked packet to the collection
        /// </summary>
        /// <param name="packet">Packet that is awaiting acknowledgement</param>
        /// <returns>True if the packet was successfully added, false if the
        /// packet already existed in the collection</returns>
        public bool Add(OutgoingPacket packet)
        {
            lock (SyncRoot)
            {
                if (!packets.ContainsKey(packet.SequenceNumber))
                {
                    packets.Add(packet.SequenceNumber, packet);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Removes a packet from the collection without attempting to obtain a
        /// lock first
        /// </summary>
        /// <param name="sequenceNumber">Sequence number of the packet to remove</param>
        /// <returns>True if the packet was found and removed, otherwise false</returns>
        public bool RemoveUnsafe(uint sequenceNumber)
        {
            return packets.Remove(sequenceNumber);
        }

        /// <summary>
        /// Removes a packet from the collection without attempting to obtain a
        /// lock first
        /// </summary>
        /// <param name="sequenceNumber">Sequence number of the packet to remove</param>
        /// <param name="packet">Returns the removed packet</param>
        /// <returns>True if the packet was found and removed, otherwise false</returns>
        public bool RemoveUnsafe(uint sequenceNumber, out OutgoingPacket packet)
        {
            if (packets.TryGetValue(sequenceNumber, out packet))
            {
                packets.Remove(sequenceNumber);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the packet with the lowest sequence number
        /// </summary>
        /// <returns>The packet with the lowest sequence number, or null if the
        /// collection is empty</returns>
        public OutgoingPacket GetOldest()
        {
            lock (SyncRoot)
            {
                using (SortedDictionary<uint, OutgoingPacket>.ValueCollection.Enumerator e = packets.Values.GetEnumerator())
                {
                    if (e.MoveNext())
                        return e.Current;
                    else
                        return null;
                }
            }
        }

        /// <summary>
        /// Returns a list of all of the packets with a TickCount older than
        /// the specified timeout
        /// </summary>
        /// <param name="timeoutMS">Number of ticks (milliseconds) before a
        /// packet is considered expired</param>
        /// <returns>A list of all expired packets according to the given
        /// expiration timeout</returns>
        public List<OutgoingPacket> GetExpiredPackets(int timeoutMS)
        {
            List<OutgoingPacket> expiredPackets = null;

            lock (SyncRoot)
            {
                int now = Environment.TickCount;
                foreach (OutgoingPacket packet in packets.Values)
                {
                    if (packet.TickCount == 0)
                        continue;

                    if (now - packet.TickCount >= timeoutMS)
                    {
                        if (expiredPackets == null)
                            expiredPackets = new List<OutgoingPacket>();
                        expiredPackets.Add(packet);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return expiredPackets;
        }
    }
}
