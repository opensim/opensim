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
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Framework;
using OpenSim.Region.CoreModules.Framework.EntityTransfer;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation;
using OpenSim.Region.CoreModules.World.Land;
using OpenSim.Region.OptionalModules;
using OpenSim.Tests.Common;
using System.Threading;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    public class SceneObjectCrossingTests : OpenSimTestCase
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

        /// <summary>
        /// Test cross with no prim limit module.
        /// </summary>
        [Test]
        public void TestCrossOnSameSimulator()
        {

            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);
            int sceneObjectIdTail = 0x2;

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
            SceneHelpers.SetupSceneModules(sceneA, config, etmA);
            SceneHelpers.SetupSceneModules(sceneB, config, etmB);

            SceneObjectGroup so1 = SceneHelpers.AddSceneObject(sceneA, 1, userId, "", sceneObjectIdTail);
            UUID so1Id = so1.UUID;
            so1.AbsolutePosition = new Vector3(128, 10, 20);

            // Cross with a negative value
            so1.AbsolutePosition = new Vector3(128, -10, 20);

            // crossing is async
            Thread.Sleep(500);

            Assert.IsNull(sceneA.GetSceneObjectGroup(so1Id));
            Assert.NotNull(sceneB.GetSceneObjectGroup(so1Id));
        }

        /// <summary>
        /// Test cross with no prim limit module.
        /// </summary>
        /// <remarks>
        /// Possibly this should belong in ScenePresenceCrossingTests, though here it is the object that is being moved
        /// where the avatar is just a passenger.
        /// </remarks>
        [Test]
        public void TestCrossOnSameSimulatorWithSittingAvatar()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);
            int sceneObjectIdTail = 0x2;
            Vector3 so1StartPos = new Vector3(128, 10, 20);

            EntityTransferModule etmA = new EntityTransferModule();
            EntityTransferModule etmB = new EntityTransferModule();
            LocalSimulationConnectorModule lscm = new LocalSimulationConnectorModule();

            IConfigSource config = new IniConfigSource();
            IConfig modulesConfig = config.AddConfig("Modules");
            modulesConfig.Set("EntityTransferModule", etmA.Name);
            modulesConfig.Set("SimulationServices", lscm.Name);
            IConfig entityTransferConfig = config.AddConfig("EntityTransfer");

            // In order to run a single threaded regression test we do not want the entity transfer module waiting
            // for a callback from the destination scene before removing its avatar data.
            entityTransferConfig.Set("wait_for_callback", false);

            SceneHelpers sh = new SceneHelpers();
            TestScene sceneA = sh.SetupScene("sceneA", TestHelpers.ParseTail(0x100), 1000, 1000);
            TestScene sceneB = sh.SetupScene("sceneB", TestHelpers.ParseTail(0x200), 1000, 999);

            SceneHelpers.SetupSceneModules(new Scene[] { sceneA, sceneB }, config, lscm);
            SceneHelpers.SetupSceneModules(sceneA, config, new CapabilitiesModule(), etmA);
            SceneHelpers.SetupSceneModules(sceneB, config, new CapabilitiesModule(), etmB);

            SceneObjectGroup so1 = SceneHelpers.AddSceneObject(sceneA, 1, userId, "", sceneObjectIdTail);
            UUID so1Id = so1.UUID;
            so1.AbsolutePosition = so1StartPos;

            AgentCircuitData acd = SceneHelpers.GenerateAgentData(userId);
            TestClient tc = new TestClient(acd, sceneA);
            List<TestClient> destinationTestClients = new List<TestClient>();
            EntityTransferHelpers.SetupInformClientOfNeighbourTriggersNeighbourClientCreate(tc, destinationTestClients);

            ScenePresence sp1SceneA = SceneHelpers.AddScenePresence(sceneA, tc, acd);
            sp1SceneA.AbsolutePosition = so1StartPos;
            sp1SceneA.HandleAgentRequestSit(sp1SceneA.ControllingClient, sp1SceneA.UUID, so1.UUID, Vector3.Zero);

            // Cross
            sceneA.SceneGraph.UpdatePrimGroupPosition(
                so1.LocalId, new Vector3(so1StartPos.X, so1StartPos.Y - 20, so1StartPos.Z), sp1SceneA.ControllingClient);

            // crossing is async
            Thread.Sleep(500);

            SceneObjectGroup so1PostCross;

            ScenePresence sp1SceneAPostCross = sceneA.GetScenePresence(userId);
            Assert.IsTrue(sp1SceneAPostCross.IsChildAgent, "sp1SceneAPostCross.IsChildAgent unexpectedly false");

            ScenePresence sp1SceneBPostCross = sceneB.GetScenePresence(userId);
            TestClient sceneBTc = ((TestClient)sp1SceneBPostCross.ControllingClient);
            sceneBTc.CompleteMovement();

            Assert.IsFalse(sp1SceneBPostCross.IsChildAgent, "sp1SceneAPostCross.IsChildAgent unexpectedly true");
            Assert.IsTrue(sp1SceneBPostCross.IsSatOnObject);

            Assert.IsNull(sceneA.GetSceneObjectGroup(so1Id), "uck");
            so1PostCross = sceneB.GetSceneObjectGroup(so1Id);
            Assert.NotNull(so1PostCross);
            Assert.AreEqual(1, so1PostCross.GetSittingAvatarsCount());


            Vector3 so1PostCrossPos = so1PostCross.AbsolutePosition;

//            Console.WriteLine("CRISSCROSS");

            // Recross
            sceneB.SceneGraph.UpdatePrimGroupPosition(
                so1PostCross.LocalId, new Vector3(so1PostCrossPos.X, so1PostCrossPos.Y + 20, so1PostCrossPos.Z), sp1SceneBPostCross.ControllingClient);

            // crossing is async
            Thread.Sleep(500);

            {
                ScenePresence sp1SceneBPostReCross = sceneB.GetScenePresence(userId);
                Assert.IsTrue(sp1SceneBPostReCross.IsChildAgent, "sp1SceneBPostReCross.IsChildAgent unexpectedly false");

                ScenePresence sp1SceneAPostReCross = sceneA.GetScenePresence(userId);
                TestClient sceneATc = ((TestClient)sp1SceneAPostReCross.ControllingClient);
                sceneATc.CompleteMovement();

                Assert.IsFalse(sp1SceneAPostReCross.IsChildAgent, "sp1SceneAPostCross.IsChildAgent unexpectedly true");
                Assert.IsTrue(sp1SceneAPostReCross.IsSatOnObject);

                Assert.IsNull(sceneB.GetSceneObjectGroup(so1Id), "uck2");
                SceneObjectGroup so1PostReCross = sceneA.GetSceneObjectGroup(so1Id);
                Assert.NotNull(so1PostReCross);
                Assert.AreEqual(1, so1PostReCross.GetSittingAvatarsCount());
            }
        }

        /// <summary>
        /// Test cross with no prim limit module.
        /// </summary>
        /// <remarks>
        /// XXX: This test may FCbe better off in a specific PrimLimitsModuleTest class in optional module tests in the
        /// future (though it is configured as active by default, so not really optional).
        /// </remarks>
        [Test]
        public void TestCrossOnSameSimulatorPrimLimitsOkay()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);
            int sceneObjectIdTail = 0x2;

            EntityTransferModule etmA = new EntityTransferModule();
            EntityTransferModule etmB = new EntityTransferModule();
            LocalSimulationConnectorModule lscm = new LocalSimulationConnectorModule();
            LandManagementModule lmmA = new LandManagementModule();
            LandManagementModule lmmB = new LandManagementModule();

            IConfigSource config = new IniConfigSource();
            IConfig modulesConfig = config.AddConfig("Modules");
            modulesConfig.Set("EntityTransferModule", etmA.Name);
            modulesConfig.Set("SimulationServices", lscm.Name);

            IConfig permissionsConfig = config.AddConfig("Permissions");
            permissionsConfig.Set("permissionmodules", "PrimLimitsModule");

            SceneHelpers sh = new SceneHelpers();
            TestScene sceneA = sh.SetupScene("sceneA", TestHelpers.ParseTail(0x100), 1000, 1000);
            TestScene sceneB = sh.SetupScene("sceneB", TestHelpers.ParseTail(0x200), 1000, 999);

            SceneHelpers.SetupSceneModules(new Scene[] { sceneA, sceneB }, config, lscm);
            SceneHelpers.SetupSceneModules(
                sceneA, config, etmA, lmmA, new PrimLimitsModule(), new PrimCountModule());
            SceneHelpers.SetupSceneModules(
                sceneB, config, etmB, lmmB, new PrimLimitsModule(), new PrimCountModule());

            // We must set up the parcel for this to work.  Normally this is taken care of by OpenSimulator startup
            // code which is not yet easily invoked by tests.
            lmmA.EventManagerOnNoLandDataFromStorage();
            lmmB.EventManagerOnNoLandDataFromStorage();

            AgentCircuitData acd = SceneHelpers.GenerateAgentData(userId);
            TestClient tc = new TestClient(acd, sceneA);
            List<TestClient> destinationTestClients = new List<TestClient>();
            EntityTransferHelpers.SetupInformClientOfNeighbourTriggersNeighbourClientCreate(tc, destinationTestClients);
            ScenePresence sp1SceneA = SceneHelpers.AddScenePresence(sceneA, tc, acd);

            SceneObjectGroup so1 = SceneHelpers.AddSceneObject(sceneA, 1, userId, "", sceneObjectIdTail);
            UUID so1Id = so1.UUID;
            so1.AbsolutePosition = new Vector3(128, 10, 20);

            // Cross with a negative value.  We must make this call rather than setting AbsolutePosition directly
            // because only this will execute permission checks in the source region.
            sceneA.SceneGraph.UpdatePrimGroupPosition(so1.LocalId, new Vector3(128, -10, 20), sp1SceneA.ControllingClient);

            // crossing is async
            Thread.Sleep(500);

            Assert.IsNull(sceneA.GetSceneObjectGroup(so1Id));
            Assert.NotNull(sceneB.GetSceneObjectGroup(so1Id));
        }
    }
}