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
using System.Net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Region.ClientStack.LindenUDP;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Tests.Common
{
    /// <summary>
    /// This class adds full UDP client classes and associated scene presence to scene.
    /// </summary>
    /// <remarks>
    /// This is used for testing client stack code.  For testing other code, use SceneHelper methods instead since
    /// they operate without the burden of setting up UDP structures which should be unnecessary for testing scene
    /// code.
    /// </remarks>
    public static class ClientStackHelpers
    {
        public static ScenePresence AddChildClient(
            Scene scene, LLUDPServer udpServer, UUID agentId, UUID sessionId, uint circuitCode)
        {
            IPEndPoint testEp = new IPEndPoint(IPAddress.Loopback, 999);

            UseCircuitCodePacket uccp = new UseCircuitCodePacket();

            UseCircuitCodePacket.CircuitCodeBlock uccpCcBlock
                = new UseCircuitCodePacket.CircuitCodeBlock();
            uccpCcBlock.Code = circuitCode;
            uccpCcBlock.ID = agentId;
            uccpCcBlock.SessionID = sessionId;
            uccp.CircuitCode = uccpCcBlock;

            byte[] uccpBytes = uccp.ToBytes();
            UDPPacketBuffer upb = new UDPPacketBuffer(testEp, uccpBytes.Length);
            upb.DataLength = uccpBytes.Length;  // God knows why this isn't set by the constructor.
            Buffer.BlockCopy(uccpBytes, 0, upb.Data, 0, uccpBytes.Length);

            AgentCircuitData acd = new AgentCircuitData();
            acd.AgentID = agentId;
            acd.SessionID = sessionId;

            scene.AuthenticateHandler.AddNewCircuit(circuitCode, acd);

            udpServer.PacketReceived(upb);

            return scene.GetScenePresence(agentId);
        }

        public static TestLLUDPServer AddUdpServer(Scene scene)
        {
            return AddUdpServer(scene, new IniConfigSource());
        }

        public static TestLLUDPServer AddUdpServer(Scene scene, IniConfigSource configSource)
        {
            uint port = 0;
            AgentCircuitManager acm = scene.AuthenticateHandler;

            TestLLUDPServer udpServer = new TestLLUDPServer(IPAddress.Any, ref port, 0, configSource, acm);
            udpServer.AddScene(scene);

            return udpServer;
        }
    }
}
