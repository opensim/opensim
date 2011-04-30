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

using System.Collections.Generic;
using OpenMetaverse.Packets;

namespace OpenSim.Region.ClientStack.LindenUDP.Tests
{ 
    public class TestLLPacketServer : LLPacketServer
    {
        /// <summary>
        /// Record counts of packets received
        /// </summary>
        protected Dictionary<PacketType, int> m_packetsReceived = new Dictionary<PacketType, int>();
        
        public TestLLPacketServer(LLUDPServer networkHandler, ClientStackUserSettings userSettings)
            : base(networkHandler, userSettings)
        {}
        
        public override void InPacket(uint circuitCode, Packet packet)
        {
            base.InPacket(circuitCode, packet);
            
            if (m_packetsReceived.ContainsKey(packet.Type))
                m_packetsReceived[packet.Type]++;
            else
                m_packetsReceived[packet.Type] = 1;
        }
        
        public int GetTotalPacketsReceived()
        {
            int totalCount = 0;
            
            foreach (int count in m_packetsReceived.Values)
                totalCount += count;
            
            return totalCount;
        }
        
        public int GetPacketsReceivedFor(PacketType packetType)
        {
            if (m_packetsReceived.ContainsKey(packetType))
                return m_packetsReceived[packetType];
            else
                return 0;
        }
    }
}
