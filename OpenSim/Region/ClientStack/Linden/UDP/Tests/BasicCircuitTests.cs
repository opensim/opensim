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
using log4net.Config;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.ClientStack.LindenUDP.Tests
{
    /// <summary>
    /// This will contain basic tests for the LindenUDP client stack
    /// </summary>
    [TestFixture]
    public class BasicCircuitTests
    {
        [TestFixtureSetUp]
        public void FixtureInit()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
            Util.FireAndForgetMethod = FireAndForgetMethod.RegressionTest;
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            // We must set this back afterwards, otherwise later tests will fail since they're expecting multiple
            // threads.  Possibly, later tests should be rewritten so none of them require async stuff (which regression
            // tests really shouldn't).
            Util.FireAndForgetMethod = Util.DefaultFireAndForgetMethod;
        }

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
        
//        /// <summary>
//        /// Set up a client for tests which aren't concerned with this process itself and where only one client is being
//        /// tested
//        /// </summary>
//        /// <param name="circuitCode"></param>
//        /// <param name="epSender"></param>
//        /// <param name="testLLUDPServer"></param>
//        /// <param name="acm"></param>
//        protected void AddClient(
//            uint circuitCode, EndPoint epSender, TestLLUDPServer testLLUDPServer, AgentCircuitManager acm)
//        {
//            UUID myAgentUuid   = UUID.Parse("00000000-0000-0000-0000-000000000001");
//            UUID mySessionUuid = UUID.Parse("00000000-0000-0000-0000-000000000002");
//            
//            AddClient(circuitCode, epSender, myAgentUuid, mySessionUuid, testLLUDPServer, acm);
//        }
        
//        /// <summary>
//        /// Set up a client for tests which aren't concerned with this process itself
//        /// </summary>
//        /// <param name="circuitCode"></param>
//        /// <param name="epSender"></param>
//        /// <param name="agentId"></param>
//        /// <param name="sessionId"></param>
//        /// <param name="testLLUDPServer"></param>
//        /// <param name="acm"></param>
//        protected void AddClient(
//            uint circuitCode, EndPoint epSender, UUID agentId, UUID sessionId, 
//            TestLLUDPServer testLLUDPServer, AgentCircuitManager acm)
//        {
//            AgentCircuitData acd = new AgentCircuitData();
//            acd.AgentID = agentId;
//            acd.SessionID = sessionId; 
//            
//            UseCircuitCodePacket uccp = new UseCircuitCodePacket();
//            
//            UseCircuitCodePacket.CircuitCodeBlock uccpCcBlock 
//                = new UseCircuitCodePacket.CircuitCodeBlock();
//            uccpCcBlock.Code = circuitCode;
//            uccpCcBlock.ID = agentId;
//            uccpCcBlock.SessionID = sessionId;
//            uccp.CircuitCode = uccpCcBlock;
//
//            acm.AddNewCircuit(circuitCode, acd);
//            
//            testLLUDPServer.LoadReceive(uccp, epSender);
//            testLLUDPServer.ReceiveData(null);
//        }
        
        /// <summary>
        /// Build an object name packet for test purposes
        /// </summary>
        /// <param name="objectLocalId"></param>
        /// <param name="objectName"></param>
        protected ObjectNamePacket BuildTestObjectNamePacket(uint objectLocalId, string objectName)
        {
            ObjectNamePacket onp = new ObjectNamePacket();
            ObjectNamePacket.ObjectDataBlock odb = new ObjectNamePacket.ObjectDataBlock();
            odb.LocalID = objectLocalId;
            odb.Name = Utils.StringToBytes(objectName);
            onp.ObjectData = new ObjectNamePacket.ObjectDataBlock[] { odb };
            onp.Header.Zerocoded = false;
            
            return onp;
        }
        
        /// <summary>
        /// Test adding a client to the stack
        /// </summary>
        [Test]
        public void TestAddClient()
        {
            TestHelpers.InMethod();
//            XmlConfigurator.Configure();

            TestScene scene = SceneHelpers.SetupScene();
            uint myCircuitCode = 123456;
            UUID myAgentUuid   = TestHelpers.ParseTail(0x1);
            UUID mySessionUuid = TestHelpers.ParseTail(0x2);
            IPEndPoint testEp = new IPEndPoint(IPAddress.Loopback, 999);

            uint port = 0;
            AgentCircuitManager acm = scene.AuthenticateHandler;

            TestLLUDPServer llUdpServer
                = new TestLLUDPServer(IPAddress.Any, ref port, 0, false, new IniConfigSource(), acm);
            llUdpServer.AddScene(scene);

            UseCircuitCodePacket uccp = new UseCircuitCodePacket();

            UseCircuitCodePacket.CircuitCodeBlock uccpCcBlock
                = new UseCircuitCodePacket.CircuitCodeBlock();
            uccpCcBlock.Code = myCircuitCode;
            uccpCcBlock.ID = myAgentUuid;
            uccpCcBlock.SessionID = mySessionUuid;
            uccp.CircuitCode = uccpCcBlock;

            byte[] uccpBytes = uccp.ToBytes();
            UDPPacketBuffer upb = new UDPPacketBuffer(testEp, uccpBytes.Length);
            upb.DataLength = uccpBytes.Length;  // God knows why this isn't set by the constructor.
            Buffer.BlockCopy(uccpBytes, 0, upb.Data, 0, uccpBytes.Length);

            llUdpServer.PacketReceived(upb);

            // Presence shouldn't exist since the circuit manager doesn't know about this circuit for authentication yet
            Assert.That(scene.GetScenePresence(myAgentUuid), Is.Null);

            AgentCircuitData acd = new AgentCircuitData();
            acd.AgentID = myAgentUuid;
            acd.SessionID = mySessionUuid;

            acm.AddNewCircuit(myCircuitCode, acd);

            llUdpServer.PacketReceived(upb);

            // Should succeed now
            ScenePresence sp = scene.GetScenePresence(myAgentUuid);
            Assert.That(sp.UUID, Is.EqualTo(myAgentUuid));

            // FIXME: We're still replying to an ack when the client is not authorized, which is not correct behaviour.
            Assert.That(llUdpServer.PacketsSent.Count, Is.EqualTo(2));

            Packet packet = llUdpServer.PacketsSent[1];
            Assert.That(packet, Is.InstanceOf(typeof(PacketAckPacket)));

            PacketAckPacket ackPacket = packet as PacketAckPacket;
            Assert.That(ackPacket.Packets.Length, Is.EqualTo(1));
            Assert.That(ackPacket.Packets[0].ID, Is.EqualTo(0));
        }

//        /// <summary>
//        /// Test removing a client from the stack
//        /// </summary>
//        [Test]
//        public void TestRemoveClient()
//        {
//            TestHelper.InMethod();
//
//            uint myCircuitCode = 123457;
//            
//            TestLLUDPServer testLLUDPServer;
//            TestLLPacketServer testLLPacketServer;
//            AgentCircuitManager acm;
//            SetupStack(new MockScene(), out testLLUDPServer, out testLLPacketServer, out acm);
//            AddClient(myCircuitCode, new IPEndPoint(IPAddress.Loopback, 1000), testLLUDPServer, acm);
//            
//            testLLUDPServer.RemoveClientCircuit(myCircuitCode);
//            Assert.IsFalse(testLLUDPServer.HasCircuit(myCircuitCode));
//            
//            // Check that removing a non-existant circuit doesn't have any bad effects
//            testLLUDPServer.RemoveClientCircuit(101);
//            Assert.IsFalse(testLLUDPServer.HasCircuit(101));
//        }
//        
//        /// <summary>
//        /// Make sure that the client stack reacts okay to malformed packets
//        /// </summary>
//        [Test]
//        public void TestMalformedPacketSend()
//        {
//            TestHelper.InMethod();
//
//            uint myCircuitCode = 123458;
//            EndPoint testEp = new IPEndPoint(IPAddress.Loopback, 1001);
//            MockScene scene = new MockScene(); 
//            
//            TestLLUDPServer testLLUDPServer;
//            TestLLPacketServer testLLPacketServer;
//            AgentCircuitManager acm;
//            SetupStack(scene, out testLLUDPServer, out testLLPacketServer, out acm);
//            AddClient(myCircuitCode, testEp, testLLUDPServer, acm);
//
//            byte[] data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
//
//            // Send two garbled 'packets' in succession
//            testLLUDPServer.LoadReceive(data, testEp);
//            testLLUDPServer.LoadReceive(data, testEp);
//            testLLUDPServer.ReceiveData(null); 
//            
//            // Check that we are still here
//            Assert.IsTrue(testLLUDPServer.HasCircuit(myCircuitCode));
//            Assert.That(testLLPacketServer.GetTotalPacketsReceived(), Is.EqualTo(0));
//            
//            // Check that sending a valid packet to same circuit still succeeds
//            Assert.That(scene.ObjectNameCallsReceived, Is.EqualTo(0));
//            
//            testLLUDPServer.LoadReceive(BuildTestObjectNamePacket(1, "helloooo"), testEp);
//            testLLUDPServer.ReceiveData(null);
//            
//            Assert.That(testLLPacketServer.GetTotalPacketsReceived(), Is.EqualTo(1));
//            Assert.That(testLLPacketServer.GetPacketsReceivedFor(PacketType.ObjectName), Is.EqualTo(1));
//        }
//        
//        /// <summary>
//        /// Test that the stack continues to work even if some client has caused a 
//        /// SocketException on Socket.BeginReceive()
//        /// </summary>
//        [Test]
//        public void TestExceptionOnBeginReceive()
//        {
//            TestHelper.InMethod();
//
//            MockScene scene = new MockScene();
//            
//            uint circuitCodeA = 130000;
//            EndPoint epA = new IPEndPoint(IPAddress.Loopback, 1300);
//            UUID agentIdA   = UUID.Parse("00000000-0000-0000-0000-000000001300");
//            UUID sessionIdA = UUID.Parse("00000000-0000-0000-0000-000000002300");
//            
//            uint circuitCodeB = 130001;
//            EndPoint epB = new IPEndPoint(IPAddress.Loopback, 1301);
//            UUID agentIdB   = UUID.Parse("00000000-0000-0000-0000-000000001301");
//            UUID sessionIdB = UUID.Parse("00000000-0000-0000-0000-000000002301");
//            
//            TestLLUDPServer testLLUDPServer;
//            TestLLPacketServer testLLPacketServer;
//            AgentCircuitManager acm;
//            SetupStack(scene, out testLLUDPServer, out testLLPacketServer, out acm);
//            AddClient(circuitCodeA, epA, agentIdA, sessionIdA, testLLUDPServer, acm);
//            AddClient(circuitCodeB, epB, agentIdB, sessionIdB, testLLUDPServer, acm);
//            
//            testLLUDPServer.LoadReceive(BuildTestObjectNamePacket(1, "packet1"), epA);
//            testLLUDPServer.LoadReceive(BuildTestObjectNamePacket(1, "packet2"), epB);
//            testLLUDPServer.LoadReceiveWithBeginException(epA);
//            testLLUDPServer.LoadReceive(BuildTestObjectNamePacket(2, "packet3"), epB);
//            testLLUDPServer.ReceiveData(null);
//            
//            Assert.IsFalse(testLLUDPServer.HasCircuit(circuitCodeA));
//         
//            Assert.That(testLLPacketServer.GetTotalPacketsReceived(), Is.EqualTo(3));
//            Assert.That(testLLPacketServer.GetPacketsReceivedFor(PacketType.ObjectName), Is.EqualTo(3));
//        }
    }
}
