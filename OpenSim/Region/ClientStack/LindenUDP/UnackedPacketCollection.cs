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
    public sealed class UnackedPacketCollection
    {
        public object SyncRoot = new object();

        SortedDictionary<uint, OutgoingPacket> packets;

        public int Count { get { return packets.Count; } }

        public UnackedPacketCollection()
        {
            packets = new SortedDictionary<uint, OutgoingPacket>();
        }

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

        public bool RemoveUnsafe(uint sequenceNumber)
        {
            return packets.Remove(sequenceNumber);
        }

        public bool RemoveUnsafe(uint sequenceNumber, out OutgoingPacket packet)
        {
            if (packets.TryGetValue(sequenceNumber, out packet))
            {
                packets.Remove(sequenceNumber);
                return true;
            }

            return false;
        }

        public OutgoingPacket GetOldest()
        {
            lock (SyncRoot)
            {
                using (SortedDictionary<uint, OutgoingPacket>.ValueCollection.Enumerator e = packets.Values.GetEnumerator())
                    return e.Current;
            }
        }

        public List<OutgoingPacket> GetExpiredPackets(int timeout)
        {
            List<OutgoingPacket> expiredPackets = null;

            lock (SyncRoot)
            {
                int now = Environment.TickCount;
                foreach (OutgoingPacket packet in packets.Values)
                {
                    if (packet.TickCount == 0)
                        continue;

                    if (now - packet.TickCount >= timeout)
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
