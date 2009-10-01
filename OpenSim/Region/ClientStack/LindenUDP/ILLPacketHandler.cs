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
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public delegate void PacketStats(int inPackets, int outPackets, int unAckedBytes);
    public delegate void PacketDrop(Packet pack, Object id);
    public delegate bool SynchronizeClientHandler(IScene scene, Packet packet, UUID agentID, ThrottleOutPacketType throttlePacketType);

    /// <summary>
    /// Interface to a class that handles all the activity involved with maintaining the client circuit (handling acks,
    /// resends, pings, etc.)
    /// </summary>
    public interface ILLPacketHandler
    {
        event PacketStats OnPacketStats;
        event PacketDrop OnPacketDrop;
        SynchronizeClientHandler SynchronizeClient { set; }

        int PacketsReceived { get; }
        int PacketsReceivedReported { get; }
        uint ResendTimeout { get; set; }
        bool ReliableIsImportant { get; set; }
        int MaxReliableResends { get; set; }

        /// <summary>
        /// Initial handling of a received packet.  It will be processed later in ProcessInPacket()
        /// </summary>
        /// <param name="packet"></param>
        void InPacket(Packet packet);
        
        /// <summary>
        /// Take action depending on the type and contents of an received packet.
        /// </summary>
        /// <param name="item"></param>
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
}
