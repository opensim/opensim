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
using System.Net;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.Framework
{
    public sealed class PacketPool
    {
        private static readonly PacketPool instance = new PacketPool();

        private Hashtable pool = new Hashtable();

        static PacketPool()
        {
        }

        public static PacketPool Instance
        {
            get { return instance; }
        }

        public Packet GetPacket(PacketType type)
        {
            Packet packet;

            lock (pool)
            {
                if (pool[type] == null || ((Stack) pool[type]).Count == 0)
                {
                    // Creating a new packet if we cannot reuse an old package
                    packet = Packet.BuildPacket(type);
                }
                else
                {
                    // Recycle old packages
                    packet = (Packet) ((Stack) pool[type]).Pop();
                }
            }

            return packet;
        }

        // private byte[] decoded_header = new byte[10];
        private static PacketType GetType(byte[] bytes)
        {
            byte[] decoded_header = new byte[10 + 8];
            ushort id;
            PacketFrequency freq;

            Buffer.BlockCopy(bytes, 0, decoded_header, 0, 10);

            if ((bytes[0] & Helpers.MSG_ZEROCODED) != 0)
            {
                Helpers.ZeroDecodeCommand(bytes, decoded_header);
            }

            if (decoded_header[6] == 0xFF)
            {
                if (decoded_header[7] == 0xFF)
                {
                    id = (ushort) ((decoded_header[8] << 8) + decoded_header[9]);
                    freq = PacketFrequency.Low;
                }
                else
                {
                    id = (ushort) decoded_header[7];
                    freq = PacketFrequency.Medium;
                }
            }
            else
            {
                id = (ushort) decoded_header[6];
                freq = PacketFrequency.High;
            }

            return Packet.GetType(id, freq);
        }

        public Packet GetPacket(byte[] bytes, ref int packetEnd, byte[] zeroBuffer)
        {
            PacketType type = GetType(bytes);

            int i = 0;
            Packet packet = GetPacket(type);
            packet.FromBytes(bytes, ref i, ref packetEnd, zeroBuffer);
            return packet;
        }

        public void ReturnPacket(Packet packet)
        {
            return; // packet pool disabled

            /* // Commented out to remove a compiler warning. :)
            lock (pool)
            {
                PacketType type=packet.Type;

                if (pool[type] == null)
                {
                    pool[type] = new Stack();
                }
                if (((Stack)pool[type]).Count < 50)
                {
                    ((Stack)pool[type]).Push(packet);
                }
            }
            */
        }
    }
}
