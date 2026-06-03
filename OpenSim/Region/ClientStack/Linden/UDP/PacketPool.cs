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
using System.Reflection;
using OpenMetaverse;
using OpenMetaverse.Packets;
using log4net;
using OpenSim.Framework.Monitoring;
using System.Runtime.InteropServices;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public sealed class PacketPool
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly PacketPool instance = new PacketPool();

        /// <summary>
        /// Pool of packets available for reuse.
        /// </summary>
        private readonly Dictionary<PacketType, Stack<Packet>> pool = new Dictionary<PacketType, Stack<Packet>>();

        private static Dictionary<Type, Stack<Object>> DataBlocks = new Dictionary<Type, Stack<Object>>();

        public static PacketPool Instance
        {
            get { return instance; }
        }

        public bool RecyclePackets { get; set; }

        /// <summary>
        /// The number of packets pooled
        /// </summary>
        public int PacketsPooled
        {
            get
            {
                lock (pool)
                    return pool.Count;
            }
        }

        /// <summary>
        /// The number of blocks pooled.
        /// </summary>
        public int BlocksPooled
        {
            get
            {
                lock (DataBlocks)
                    return DataBlocks.Count;
            }
        }

        /// <summary>
        /// Number of packets requested.
        /// </summary>
        public long PacketsRequested { get; private set; }

        /// <summary>
        /// Number of packets reused.
        /// </summary>
        public long PacketsReused { get; private set; }

        /// <summary>
        /// Number of packet blocks requested.
        /// </summary>
        public long BlocksRequested { get; private set; }

        /// <summary>
        /// Number of packet blocks reused.
        /// </summary>
        public long BlocksReused { get; private set; }

        private PacketPool()
        {
            // defaults
            RecyclePackets = true;
            //RecyclePackets = false;
        }

        /// <summary>
        /// Gets a packet of the given type.
        /// </summary>
        /// <param name='type'></param>
        /// <returns>Guaranteed to always return a packet, whether from the pool or newly constructed.</returns>
        public Packet GetPacket(PacketType type)
        {
            PacketsRequested++;
            if (!RecyclePackets)
                return Packet.BuildPacket(type);

            Packet packet;
            lock (pool)
            {
                if (!pool.TryGetValue(type, out Stack<Packet> typePacketsStack) || typePacketsStack == null || typePacketsStack.Count == 0)
                {
                    //m_log.DebugFormat("[PACKETPOOL]: Building {0} packet", type);
                    packet = Packet.BuildPacket(type);
                }
                else
                {
                    //m_log.DebugFormat("[PACKETPOOL]: Pulling {0} packet", type);

                    // Recycle old packages
                    PacketsReused++;
                    packet = typePacketsStack.Pop();
                }
            }

            return packet;
        }

        private static PacketType GetType(byte[] bytes)
        {
            ushort id;
            PacketFrequency freq;
            bool isZeroCoded = (bytes[0] & Helpers.MSG_ZEROCODED) != 0;

            if (bytes[6] == 0xFF)
            {
                if (bytes[7] == 0xFF)
                {
                    freq = PacketFrequency.Low;
                    if (isZeroCoded && bytes[8] == 0)
                        id = bytes[10];
                    else
                        id = (ushort)((bytes[8] << 8) + bytes[9]);
                }
                else
                {
                    freq = PacketFrequency.Medium;
                    id = bytes[7];
                }
            }
            else
            {
                freq = PacketFrequency.High;
                id = bytes[6];
            }

            return Packet.GetType(id, freq);
        }

        public Packet GetPacket(byte[] bytes, ref int packetEnd, byte[] zeroBuffer)
        {
            PacketType type = GetType(bytes);

//            Array.Clear(zeroBuffer, 0, zeroBuffer.Length);

            int i = 0;
            Packet packet = GetPacket(type);
            if (packet == null)
                m_log.WarnFormat("[PACKETPOOL]: Failed to get packet of type {0}", type);
            else
                packet.FromBytes(bytes, ref i, ref packetEnd, zeroBuffer);

            return packet;
        }

        /// <summary>
        /// Return a packet to the packet pool
        /// </summary>
        /// <param name="packet"></param>
        public void ReturnPacket(Packet packet)
        {
            if (!RecyclePackets)
                return;

            PacketType type = packet.Type;

            switch (type)
            {
                case PacketType.ObjectUpdate:
                    ObjectUpdatePacket oup = (ObjectUpdatePacket)packet;
                    oup.ObjectData = null;
                    break;

                case PacketType.ImprovedTerseObjectUpdate:
                    ImprovedTerseObjectUpdatePacket itoup = (ImprovedTerseObjectUpdatePacket)packet;
                    itoup.ObjectData = null;
                    break;

                case PacketType.PacketAck:
                    PacketAckPacket ackup = (PacketAckPacket)packet;
                    ackup.Packets = null;
                    break;

                default:
                    return;
            }

            lock (pool)
            {
                ref Stack<Packet> spkt = ref CollectionsMarshal.GetValueRefOrAddDefault(pool, type, out bool exists);
                if (exists && spkt.Count < 50)
                {
                    spkt.Push(packet);
                    return;
                }

                spkt = new Stack<Packet>();
                spkt.Push(packet);
            }
        }
    }
}