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
using OpenMetaverse;
using NUnit.Framework;
using System;

namespace OpenSim.Framework.Tests
{
    [TestFixture]
    public class AgentCircuitManagerTests
    {
        private AgentCircuitData m_agentCircuitData1;
        private AgentCircuitData m_agentCircuitData2;
        private UUID AgentId1;
        private UUID AgentId2;
        private uint circuitcode1;
        private uint circuitcode2;

        private UUID SessionId1;
        private UUID SessionId2;
        private Random rnd = new Random(Environment.TickCount);

        [SetUp]
        public void setup()
        {

            AgentId1 = UUID.Random();
            AgentId2 = UUID.Random();
            circuitcode1 = (uint) rnd.Next((int)uint.MinValue, int.MaxValue);
            circuitcode2 = (uint) rnd.Next((int)uint.MinValue, int.MaxValue);
            SessionId1 = UUID.Random();
            SessionId2 = UUID.Random();
            UUID BaseFolder = UUID.Random();
            string CapsPath = "http://www.opensimulator.org/Caps/Foo";
            Dictionary<ulong,string> ChildrenCapsPaths = new Dictionary<ulong, string>();
            ChildrenCapsPaths.Add(ulong.MaxValue, "http://www.opensimulator.org/Caps/Foo2");
            string firstname = "CoolAvatarTest";
            string lastname = "test";
            Vector3 StartPos = new Vector3(5, 23, 125);

            UUID SecureSessionId = UUID.Random();
            // TODO: unused: UUID SessionId = UUID.Random();

            m_agentCircuitData1 = new AgentCircuitData();
            m_agentCircuitData1.AgentID = AgentId1;
            m_agentCircuitData1.Appearance = new AvatarAppearance();
            m_agentCircuitData1.BaseFolder = BaseFolder;
            m_agentCircuitData1.CapsPath = CapsPath;
            m_agentCircuitData1.child = false;
            m_agentCircuitData1.ChildrenCapSeeds = ChildrenCapsPaths;
            m_agentCircuitData1.circuitcode = circuitcode1;
            m_agentCircuitData1.firstname = firstname;
            m_agentCircuitData1.InventoryFolder = BaseFolder;
            m_agentCircuitData1.lastname = lastname;
            m_agentCircuitData1.SecureSessionID = SecureSessionId;
            m_agentCircuitData1.SessionID = SessionId1;
            m_agentCircuitData1.startpos = StartPos;

            m_agentCircuitData2 = new AgentCircuitData();
            m_agentCircuitData2.AgentID = AgentId2;
            m_agentCircuitData2.Appearance = new AvatarAppearance();
            m_agentCircuitData2.BaseFolder = BaseFolder;
            m_agentCircuitData2.CapsPath = CapsPath;
            m_agentCircuitData2.child = false;
            m_agentCircuitData2.ChildrenCapSeeds = ChildrenCapsPaths;
            m_agentCircuitData2.circuitcode = circuitcode2;
            m_agentCircuitData2.firstname = firstname;
            m_agentCircuitData2.InventoryFolder = BaseFolder;
            m_agentCircuitData2.lastname = lastname;
            m_agentCircuitData2.SecureSessionID = SecureSessionId;
            m_agentCircuitData2.SessionID = SessionId2;
            m_agentCircuitData2.startpos = StartPos;
        }

        /// <summary>
        /// Validate that adding the circuit works appropriately
        /// </summary>
        [Test]
        public void AddAgentCircuitTest()
        {
            AgentCircuitManager agentCircuitManager = new AgentCircuitManager();
            agentCircuitManager.AddNewCircuit(circuitcode1,m_agentCircuitData1);
            agentCircuitManager.AddNewCircuit(circuitcode2, m_agentCircuitData2);
            AgentCircuitData agent = agentCircuitManager.GetAgentCircuitData(circuitcode1);

            Assert.That((m_agentCircuitData1.AgentID == agent.AgentID));
            Assert.That((m_agentCircuitData1.BaseFolder == agent.BaseFolder));

            Assert.That((m_agentCircuitData1.CapsPath == agent.CapsPath));
            Assert.That((m_agentCircuitData1.child == agent.child));
            Assert.That((m_agentCircuitData1.ChildrenCapSeeds.Count == agent.ChildrenCapSeeds.Count));
            Assert.That((m_agentCircuitData1.circuitcode == agent.circuitcode));
            Assert.That((m_agentCircuitData1.firstname == agent.firstname));
            Assert.That((m_agentCircuitData1.InventoryFolder == agent.InventoryFolder));
            Assert.That((m_agentCircuitData1.lastname == agent.lastname));
            Assert.That((m_agentCircuitData1.SecureSessionID == agent.SecureSessionID));
            Assert.That((m_agentCircuitData1.SessionID == agent.SessionID));
            Assert.That((m_agentCircuitData1.startpos == agent.startpos));
        }

        /// <summary>
        /// Validate that removing the circuit code removes it appropriately
        /// </summary>
        [Test]
        public void RemoveAgentCircuitTest()
        {
            AgentCircuitManager agentCircuitManager = new AgentCircuitManager();
            agentCircuitManager.AddNewCircuit(circuitcode1, m_agentCircuitData1);
            agentCircuitManager.AddNewCircuit(circuitcode2, m_agentCircuitData2);
            agentCircuitManager.RemoveCircuit(circuitcode2);

            AgentCircuitData agent = agentCircuitManager.GetAgentCircuitData(circuitcode2);
            Assert.That(agent == null);

        }

        /// <summary>
        /// Validate that changing the circuit code works
        /// </summary>
        [Test]
        public void ChangeAgentCircuitCodeTest()
        {
            AgentCircuitManager agentCircuitManager = new AgentCircuitManager();
            agentCircuitManager.AddNewCircuit(circuitcode1, m_agentCircuitData1);
            agentCircuitManager.AddNewCircuit(circuitcode2, m_agentCircuitData2);
            bool result = false;

            result = agentCircuitManager.TryChangeCiruitCode(circuitcode1, 393930);

            AgentCircuitData agent = agentCircuitManager.GetAgentCircuitData(393930);
            AgentCircuitData agent2 = agentCircuitManager.GetAgentCircuitData(circuitcode1);
            Assert.That(agent != null);
            Assert.That(agent2 == null);
            Assert.That(result);

        }

        /// <summary>
        /// Validates that the login authentication scheme is working
        /// First one should be authorized
        /// Rest should not be authorized
        /// </summary>
        [Test]
        public void ValidateLoginTest()
        {
            AgentCircuitManager agentCircuitManager = new AgentCircuitManager();
            agentCircuitManager.AddNewCircuit(circuitcode1, m_agentCircuitData1);
            agentCircuitManager.AddNewCircuit(circuitcode2, m_agentCircuitData2);

            // should be authorized
            AuthenticateResponse resp = agentCircuitManager.AuthenticateSession(SessionId1, AgentId1, circuitcode1);
            Assert.That(resp.Authorised);


            //should not be authorized
            resp = agentCircuitManager.AuthenticateSession(SessionId1, UUID.Random(), circuitcode1);
            Assert.That(!resp.Authorised);

            resp = agentCircuitManager.AuthenticateSession(UUID.Random(), AgentId1, circuitcode1);
            Assert.That(!resp.Authorised);

            resp = agentCircuitManager.AuthenticateSession(SessionId1, AgentId1, circuitcode2);
            Assert.That(!resp.Authorised);

            resp = agentCircuitManager.AuthenticateSession(SessionId2, AgentId1, circuitcode2);
            Assert.That(!resp.Authorised);

            agentCircuitManager.RemoveCircuit(circuitcode2);

            resp = agentCircuitManager.AuthenticateSession(SessionId2, AgentId2, circuitcode2);
            Assert.That(!resp.Authorised);
        }
    }
}
