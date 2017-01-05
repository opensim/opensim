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

using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Tests.Common;

namespace OpenSim.Region.ClientStack.LindenUDP.Tests
{
    /// <summary>
    /// Tests for the LL packet handler
    /// </summary>
    [TestFixture]
    public class PacketHandlerTests : OpenSimTestCase
    {
//        [Test]
//        /// <summary>
//        /// More a placeholder, really
//        /// </summary>
//        public void InPacketTest()
//        {
//            TestHelper.InMethod();
//
//            AgentCircuitData agent = new AgentCircuitData();
//            agent.AgentID = UUID.Random();
//            agent.firstname = "testfirstname";
//            agent.lastname = "testlastname";
//            agent.SessionID = UUID.Zero;
//            agent.SecureSessionID = UUID.Zero;
//            agent.circuitcode = 123;
//            agent.BaseFolder = UUID.Zero;
//            agent.InventoryFolder = UUID.Zero;
//            agent.startpos = Vector3.Zero;
//            agent.CapsPath = "http://wibble.com";
//
//            TestLLUDPServer testLLUDPServer;
//            TestLLPacketServer testLLPacketServer;
//            AgentCircuitManager acm;
//            IScene scene = new MockScene();
//            SetupStack(scene, out testLLUDPServer, out testLLPacketServer, out acm);
//
//            TestClient testClient = new TestClient(agent, scene);
//
//            LLPacketHandler packetHandler
//                = new LLPacketHandler(testClient, testLLPacketServer, new ClientStackUserSettings());
//
//            packetHandler.InPacket(new AgentAnimationPacket());
//            LLQueItem receivedPacket = packetHandler.PacketQueue.Dequeue();
//
//            Assert.That(receivedPacket, Is.Not.Null);
//            Assert.That(receivedPacket.Incoming, Is.True);
//            Assert.That(receivedPacket.Packet, Is.TypeOf(typeof(AgentAnimationPacket)));
//        }
//
//        /// <summary>
//        /// Add a client for testing
//        /// </summary>
//        /// <param name="scene"></param>
//        /// <param name="testLLUDPServer"></param>
//        /// <param name="testPacketServer"></param>
//        /// <param name="acm">Agent circuit manager used in setting up the stack</param>
//        protected void SetupStack(
//            IScene scene, out TestLLUDPServer testLLUDPServer, out TestLLPacketServer testPacketServer,
//            out AgentCircuitManager acm)
//        {
//            IConfigSource configSource = new IniConfigSource();
//            ClientStackUserSettings userSettings = new ClientStackUserSettings();
//            testLLUDPServer = new TestLLUDPServer();
//            acm = new AgentCircuitManager();
//
//            uint port = 666;
//            testLLUDPServer.Initialise(null, ref port, 0, false, configSource, acm);
//            testPacketServer = new TestLLPacketServer(testLLUDPServer, userSettings);
//            testLLUDPServer.LocalScene = scene;
//        }
    }
}
