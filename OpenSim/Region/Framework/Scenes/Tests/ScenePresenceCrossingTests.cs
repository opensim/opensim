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
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.CoreModules.Framework;
using OpenSim.Region.CoreModules.Framework.EntityTransfer;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation;
using OpenSim.Region.CoreModules.World.Permissions;
using OpenSim.Tests.Common;
using OpenSim.Region.OptionalModules.Avatar.XmlRpcGroups;
using System.Threading;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    [TestFixture]
    public class ScenePresenceCrossingTests : OpenSimTestCase
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

        [Test]
        public void TestCrossOnSameSimulator()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);

//            TestEventQueueGetModule eqmA = new TestEventQueueGetModule();
            EntityTransferModule etmA = new EntityTransferModule();
            EntityTransferModule etmB = new EntityTransferModule();
            LocalSimulationConnectorModule lscm = new LocalSimulationConnectorModule();

            IConfigSource config = new IniConfigSource();
            IConfig modulesConfig = config.AddConfig("Modules");
            modulesConfig.Set("EntityTransferModule", etmA.Name);
            modulesConfig.Set("SimulationServices", lscm.Name);
//            IConfig entityTransferConfig = config.AddConfig("EntityTransfer");

            // In order to run a single threaded regression test we do not want the entity transfer module waiting
            // for a callback from the destination scene before removing its avatar data.
//            entityTransferConfig.Set("wait_for_callback", false);

            SceneHelpers sh = new SceneHelpers();
            TestScene sceneA = sh.SetupScene("sceneA", TestHelpers.ParseTail(0x100), 1000, 1000);
            TestScene sceneB = sh.SetupScene("sceneB", TestHelpers.ParseTail(0x200), 1000, 999);

            SceneHelpers.SetupSceneModules(new Scene[] { sceneA, sceneB }, config, lscm);
            SceneHelpers.SetupSceneModules(sceneA, config, new CapabilitiesModule(), etmA);
//            SceneHelpers.SetupSceneModules(sceneA, config, new CapabilitiesModule(), etmA, eqmA);
            SceneHelpers.SetupSceneModules(sceneB, config, new CapabilitiesModule(), etmB);

            AgentCircuitData acd = SceneHelpers.GenerateAgentData(userId);
            TestClient tc = new TestClient(acd, sceneA);
            List<TestClient> destinationTestClients = new List<TestClient>();
            EntityTransferHelpers.SetupInformClientOfNeighbourTriggersNeighbourClientCreate(tc, destinationTestClients);

            ScenePresence originalSp = SceneHelpers.AddScenePresence(sceneA, tc, acd);
            originalSp.AbsolutePosition = new Vector3(128, 32, 10);

//            originalSp.Flying = true;

//            Console.WriteLine("First pos {0}", originalSp.AbsolutePosition);

//            eqmA.ClearEvents();

            AgentUpdateArgs moveArgs = new AgentUpdateArgs();
            //moveArgs.BodyRotation = Quaternion.CreateFromEulers(Vector3.Zero);
            moveArgs.BodyRotation = Quaternion.CreateFromEulers(new Vector3(0, 0, (float)-(Math.PI / 2)));
            moveArgs.ControlFlags = (uint)AgentManager.ControlFlags.AGENT_CONTROL_AT_POS;
            moveArgs.SessionID = acd.SessionID;

            originalSp.HandleAgentUpdate(originalSp.ControllingClient, moveArgs);

            sceneA.Update(1);

//            Console.WriteLine("Second pos {0}", originalSp.AbsolutePosition);

            // FIXME: This is a sufficient number of updates to for the presence to reach the northern border.
            // But really we want to do this in a more robust way.
            for (int i = 0; i < 100; i++)
            {
                sceneA.Update(1);
//                Console.WriteLine("Pos {0}", originalSp.AbsolutePosition);
            }

            // Need to sort processing of EnableSimulator message on adding scene presences before we can test eqm
            // messages
//            Dictionary<UUID, List<TestEventQueueGetModule.Event>> eqmEvents = eqmA.Events;
//
//            Assert.That(eqmEvents.Count, Is.EqualTo(1));
//            Assert.That(eqmEvents.ContainsKey(originalSp.UUID), Is.True);
//
//            List<TestEventQueueGetModule.Event> spEqmEvents = eqmEvents[originalSp.UUID];
//
//            Assert.That(spEqmEvents.Count, Is.EqualTo(1));
//            Assert.That(spEqmEvents[0].Name, Is.EqualTo("CrossRegion"));

            // sceneA should now only have a child agent
            ScenePresence spAfterCrossSceneA = sceneA.GetScenePresence(originalSp.UUID);
            Assert.That(spAfterCrossSceneA.IsChildAgent, Is.True);

            ScenePresence spAfterCrossSceneB = sceneB.GetScenePresence(originalSp.UUID);

            // Agent remains a child until the client triggers complete movement
            Assert.That(spAfterCrossSceneB.IsChildAgent, Is.True);

            TestClient sceneBTc = ((TestClient)spAfterCrossSceneB.ControllingClient);

            int agentMovementCompleteReceived = 0;
            sceneBTc.OnReceivedMoveAgentIntoRegion += (ri, pos, look) => agentMovementCompleteReceived++;

            sceneBTc.CompleteMovement();

            Assert.That(agentMovementCompleteReceived, Is.EqualTo(1));
            Assert.That(spAfterCrossSceneB.IsChildAgent, Is.False);
        }

        /// <summary>
        /// Test a cross attempt where the user can see into the neighbour but does not have permission to become
        /// root there.
        /// </summary>
        [Test]
        public void TestCrossOnSameSimulatorNoRootDestPerm()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);

            EntityTransferModule etmA = new EntityTransferModule();
            EntityTransferModule etmB = new EntityTransferModule();
            LocalSimulationConnectorModule lscm = new LocalSimulationConnectorModule();

            IConfigSource config = new IniConfigSource();
            IConfig modulesConfig = config.AddConfig("Modules");
            modulesConfig.Set("EntityTransferModule", etmA.Name);
            modulesConfig.Set("SimulationServices", lscm.Name);

            SceneHelpers sh = new SceneHelpers();
            TestScene sceneA = sh.SetupScene("sceneA", TestHelpers.ParseTail(0x100), 1000, 1000);
            TestScene sceneB = sh.SetupScene("sceneB", TestHelpers.ParseTail(0x200), 1000, 999);

            SceneHelpers.SetupSceneModules(new Scene[] { sceneA, sceneB }, config, lscm);
            SceneHelpers.SetupSceneModules(sceneA, config, new CapabilitiesModule(), etmA);

            // We need to set up the permisions module on scene B so that our later use of agent limit to deny
            // QueryAccess won't succeed anyway because administrators are always allowed in and the default
            // IsAdministrator if no permissions module is present is true.
            SceneHelpers.SetupSceneModules(sceneB, config, new CapabilitiesModule(), new DefaultPermissionsModule(), etmB);

            AgentCircuitData acd = SceneHelpers.GenerateAgentData(userId);
            TestClient tc = new TestClient(acd, sceneA);
            List<TestClient> destinationTestClients = new List<TestClient>();
            EntityTransferHelpers.SetupInformClientOfNeighbourTriggersNeighbourClientCreate(tc, destinationTestClients);

            // Make sure sceneB will not accept this avatar.
            sceneB.RegionInfo.EstateSettings.PublicAccess = false;

            ScenePresence originalSp = SceneHelpers.AddScenePresence(sceneA, tc, acd);
            originalSp.AbsolutePosition = new Vector3(128, 32, 10);

            AgentUpdateArgs moveArgs = new AgentUpdateArgs();
            //moveArgs.BodyRotation = Quaternion.CreateFromEulers(Vector3.Zero);
            moveArgs.BodyRotation = Quaternion.CreateFromEulers(new Vector3(0, 0, (float)-(Math.PI / 2)));
            moveArgs.ControlFlags = (uint)AgentManager.ControlFlags.AGENT_CONTROL_AT_POS;
            moveArgs.SessionID = acd.SessionID;

            originalSp.HandleAgentUpdate(originalSp.ControllingClient, moveArgs);

            sceneA.Update(1);

//            Console.WriteLine("Second pos {0}", originalSp.AbsolutePosition);

            // FIXME: This is a sufficient number of updates to for the presence to reach the northern border.
            // But really we want to do this in a more robust way.
            for (int i = 0; i < 100; i++)
            {
                sceneA.Update(1);
//                Console.WriteLine("Pos {0}", originalSp.AbsolutePosition);
            }

            // sceneA agent should still be root
            ScenePresence spAfterCrossSceneA = sceneA.GetScenePresence(originalSp.UUID);
            Assert.That(spAfterCrossSceneA.IsChildAgent, Is.False);

            ScenePresence spAfterCrossSceneB = sceneB.GetScenePresence(originalSp.UUID);

            // sceneB agent should still be child
            Assert.That(spAfterCrossSceneB.IsChildAgent, Is.True);

            // sceneB should ignore unauthorized attempt to upgrade agent to root
            TestClient sceneBTc = ((TestClient)spAfterCrossSceneB.ControllingClient);

            int agentMovementCompleteReceived = 0;
            sceneBTc.OnReceivedMoveAgentIntoRegion += (ri, pos, look) => agentMovementCompleteReceived++;

            sceneBTc.CompleteMovement();

            Assert.That(agentMovementCompleteReceived, Is.EqualTo(0));
            Assert.That(spAfterCrossSceneB.IsChildAgent, Is.True);
        }
    }
}
