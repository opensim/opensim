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
using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    /// <summary>
    /// Holds a reference to the <seealso cref="LLUDPClient"/> this packet is
    /// destined for, along with the serialized packet data, sequence number
    /// (if this is a resend), number of times this packet has been resent,
    /// the time of the last resend, and the throttling category for this
    /// packet
    /// </summary>
    public sealed class OutgoingPacket
    {
        /// <summary>Client this packet is destined for</summary>
        public LLUDPClient Client;
        /// <summary>Packet data to send</summary>
        public UDPPacketBuffer Buffer;
        /// <summary>Sequence number of the wrapped packet</summary>
        public uint SequenceNumber;
        /// <summary>Number of times this packet has been resent</summary>
        public int ResendCount;
        /// <summary>Environment.TickCount when this packet was last sent over the wire</summary>
        public int TickCount;
        /// <summary>Category this packet belongs to</summary>
        public ThrottleOutPacketType Category;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="client">Reference to the client this packet is destined for</param>
        /// <param name="buffer">Serialized packet data. If the flags or sequence number
        /// need to be updated, they will be injected directly into this binary buffer</param>
        /// <param name="category">Throttling category for this packet</param>
        public OutgoingPacket(LLUDPClient client, UDPPacketBuffer buffer, ThrottleOutPacketType category)
        {
            Client = client;
            Buffer = buffer;
            Category = category;
        }
    }
}
