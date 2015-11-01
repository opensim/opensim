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
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
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

namespace OpenSim.Region.Framework.Scenes.Tests
{
    /// <summary>
    /// Teleport tests in a standalone OpenSim
    /// </summary>
    [TestFixture]
    public class ScenePresenceTeleportTests : OpenSimTestCase
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
        public void TestSameRegion()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            EntityTransferModule etm = new EntityTransferModule();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            // Not strictly necessary since FriendsModule assumes it is the default (!)
            config.Configs["Modules"].Set("EntityTransferModule", etm.Name);

            TestScene scene = new SceneHelpers().SetupScene("sceneA", TestHelpers.ParseTail(0x100), 1000, 1000);
            SceneHelpers.SetupSceneModules(scene, config, etm);

            Vector3 teleportPosition = new Vector3(10, 11, 12);
            Vector3 teleportLookAt = new Vector3(20, 21, 22);

            ScenePresence sp = SceneHelpers.AddScenePresence(scene, TestHelpers.ParseTail(0x1));
            sp.AbsolutePosition = new Vector3(30, 31, 32);
            scene.RequestTeleportLocation(
                sp.ControllingClient,
                scene.RegionInfo.RegionHandle,
                teleportPosition,
                teleportLookAt,
                (uint)TeleportFlags.ViaLocation);

            Assert.That(sp.AbsolutePosition, Is.EqualTo(teleportPosition));

            Assert.That(scene.GetRootAgentCount(), Is.EqualTo(1));
            Assert.That(scene.GetChildAgentCount(), Is.EqualTo(0));

            // Lookat is sent to the client only - sp.Lookat does not yield the same thing (calculation from camera
            // position instead).
//            Assert.That(sp.Lookat, Is.EqualTo(teleportLookAt));
        }

/*
        [Test]
        public void TestSameSimulatorIsolatedRegionsV1()
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
            IConfig entityTransferConfig = config.AddConfig("EntityTransfer");

            // In order to run a single threaded regression test we do not want the entity transfer module waiting
            // for a callback from the destination scene before removing its avatar data.
            entityTransferConfig.Set("wait_for_callback", false);

            SceneHelpers sh = new SceneHelpers();
            TestScene sceneA = sh.SetupScene("sceneA", TestHelpers.ParseTail(0x100), 1000, 1000);
            TestScene sceneB = sh.SetupScene("sceneB", TestHelpers.ParseTail(0x200), 1002, 1000);

            SceneHelpers.SetupSceneModules(sceneA, config, etmA);
            SceneHelpers.SetupSceneModules(sceneB, config, etmB);
            SceneHelpers.SetupSceneModules(new Scene[] { sceneA, sceneB }, config, lscm);

            // FIXME: Hack - this is here temporarily to revert back to older entity transfer behaviour
            lscm.ServiceVersion = 0.1f;

            Vector3 teleportPosition = new Vector3(10, 11, 12);
            Vector3 teleportLookAt = new Vector3(20, 21, 22);

            ScenePresence sp = SceneHelpers.AddScenePresence(sceneA, userId);
            sp.AbsolutePosition = new Vector3(30, 31, 32);

            List<TestClient> destinationTestClients = new List<TestClient>();
            EntityTransferHelpers.SetupInformClientOfNeighbourTriggersNeighbourClientCreate(
                (TestClient)sp.ControllingClient, destinationTestClients);

            sceneA.RequestTeleportLocation(
                sp.ControllingClient,
                sceneB.RegionInfo.RegionHandle,
                teleportPosition,
                teleportLookAt,
                (uint)TeleportFlags.ViaLocation);

            // SetupInformClientOfNeighbour() will have handled the callback into the target scene to setup the child
            // agent.  This call will now complete the movement of the user into the destination and upgrade the agent
            // from child to root.
            destinationTestClients[0].CompleteMovement();

            Assert.That(sceneA.GetScenePresence(userId), Is.Null);

            ScenePresence sceneBSp = sceneB.GetScenePresence(userId);
            Assert.That(sceneBSp, Is.Not.Null);
            Assert.That(sceneBSp.Scene.RegionInfo.RegionName, Is.EqualTo(sceneB.RegionInfo.RegionName));
            Assert.That(sceneBSp.AbsolutePosition, Is.EqualTo(teleportPosition));

            Assert.That(sceneA.GetRootAgentCount(), Is.EqualTo(0));
            Assert.That(sceneA.GetChildAgentCount(), Is.EqualTo(0));
            Assert.That(sceneB.GetRootAgentCount(), Is.EqualTo(1));
            Assert.That(sceneB.GetChildAgentCount(), Is.EqualTo(0));

            // TODO: Add assertions to check correct circuit details in both scenes.

            // Lookat is sent to the client only - sp.Lookat does not yield the same thing (calculation from camera
            // position instead).
//            Assert.That(sp.Lookat, Is.EqualTo(teleportLookAt));
        }
*/

        [Test]
        public void TestSameSimulatorIsolatedRegionsV2()
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
            TestScene sceneB = sh.SetupScene("sceneB", TestHelpers.ParseTail(0x200), 1002, 1000);

            SceneHelpers.SetupSceneModules(sceneA, config, etmA);
            SceneHelpers.SetupSceneModules(sceneB, config, etmB);
            SceneHelpers.SetupSceneModules(new Scene[] { sceneA, sceneB }, config, lscm);

            Vector3 teleportPosition = new Vector3(10, 11, 12);
            Vector3 teleportLookAt = new Vector3(20, 21, 22);

            ScenePresence sp = SceneHelpers.AddScenePresence(sceneA, userId);
            sp.AbsolutePosition = new Vector3(30, 31, 32);

            List<TestClient> destinationTestClients = new List<TestClient>();
            EntityTransferHelpers.SetupSendRegionTeleportTriggersDestinationClientCreateAndCompleteMovement(
                (TestClient)sp.ControllingClient, destinationTestClients);

            sceneA.RequestTeleportLocation(
                sp.ControllingClient,
                sceneB.RegionInfo.RegionHandle,
                teleportPosition,
                teleportLookAt,
                (uint)TeleportFlags.ViaLocation);

            Assert.That(sceneA.GetScenePresence(userId), Is.Null);

            ScenePresence sceneBSp = sceneB.GetScenePresence(userId);
            Assert.That(sceneBSp, Is.Not.Null);
            Assert.That(sceneBSp.Scene.RegionInfo.RegionName, Is.EqualTo(sceneB.RegionInfo.RegionName));
            Assert.That(sceneBSp.AbsolutePosition, Is.EqualTo(teleportPosition));

            Assert.That(sceneA.GetRootAgentCount(), Is.EqualTo(0));
            Assert.That(sceneA.GetChildAgentCount(), Is.EqualTo(0));
            Assert.That(sceneB.GetRootAgentCount(), Is.EqualTo(1));
            Assert.That(sceneB.GetChildAgentCount(), Is.EqualTo(0));

            // TODO: Add assertions to check correct circuit details in both scenes.

            // Lookat is sent to the client only - sp.Lookat does not yield the same thing (calculation from camera
            // position instead).
//            Assert.That(sp.Lookat, Is.EqualTo(teleportLookAt));
        }

        /// <summary>
        /// Test teleport procedures when the target simulator returns false when queried about access.
        /// </summary>
        [Test]
        public void TestSameSimulatorIsolatedRegions_DeniedOnQueryAccess()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);
            Vector3 preTeleportPosition = new Vector3(30, 31, 32);

            EntityTransferModule etmA = new EntityTransferModule();
            EntityTransferModule etmB = new EntityTransferModule();

            LocalSimulationConnectorModule lscm = new LocalSimulationConnectorModule();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("EntityTransferModule", etmA.Name);
            config.Configs["Modules"].Set("SimulationServices", lscm.Name);

            config.AddConfig("EntityTransfer");

            // In order to run a single threaded regression test we do not want the entity transfer module waiting
            // for a callback from the destination scene before removing its avatar data.
            config.Configs["EntityTransfer"].Set("wait_for_callback", false);

            config.AddConfig("Startup");
            config.Configs["Startup"].Set("serverside_object_permissions", true);

            SceneHelpers sh = new SceneHelpers();
            TestScene sceneA = sh.SetupScene("sceneA", TestHelpers.ParseTail(0x100), 1000, 1000);
            TestScene sceneB = sh.SetupScene("sceneB", TestHelpers.ParseTail(0x200), 1002, 1000);

            SceneHelpers.SetupSceneModules(sceneA, config, etmA );

            // We need to set up the permisions module on scene B so that our later use of agent limit to deny
            // QueryAccess won't succeed anyway because administrators are always allowed in and the default
            // IsAdministrator if no permissions module is present is true.
            SceneHelpers.SetupSceneModules(sceneB, config, new object[] { new DefaultPermissionsModule(), etmB });

            // Shared scene modules
            SceneHelpers.SetupSceneModules(new Scene[] { sceneA, sceneB }, config, lscm);

            Vector3 teleportPosition = new Vector3(10, 11, 12);
            Vector3 teleportLookAt = new Vector3(20, 21, 22);

            ScenePresence sp = SceneHelpers.AddScenePresence(sceneA, userId);
            sp.AbsolutePosition = preTeleportPosition;

            // Make sceneB return false on query access
            sceneB.RegionInfo.RegionSettings.AgentLimit = 0;

            sceneA.RequestTeleportLocation(
                sp.ControllingClient,
                sceneB.RegionInfo.RegionHandle,
                teleportPosition,
                teleportLookAt,
                (uint)TeleportFlags.ViaLocation);

//            ((TestClient)sp.ControllingClient).CompleteTeleportClientSide();

            Assert.That(sceneB.GetScenePresence(userId), Is.Null);

            ScenePresence sceneASp = sceneA.GetScenePresence(userId);
            Assert.That(sceneASp, Is.Not.Null);
            Assert.That(sceneASp.Scene.RegionInfo.RegionName, Is.EqualTo(sceneA.RegionInfo.RegionName));
            Assert.That(sceneASp.AbsolutePosition, Is.EqualTo(preTeleportPosition));

            Assert.That(sceneA.GetRootAgentCount(), Is.EqualTo(1));
            Assert.That(sceneA.GetChildAgentCount(), Is.EqualTo(0));
            Assert.That(sceneB.GetRootAgentCount(), Is.EqualTo(0));
            Assert.That(sceneB.GetChildAgentCount(), Is.EqualTo(0));

            // TODO: Add assertions to check correct circuit details in both scenes.

            // Lookat is sent to the client only - sp.Lookat does not yield the same thing (calculation from camera
            // position instead).
//            Assert.That(sp.Lookat, Is.EqualTo(teleportLookAt));

//            TestHelpers.DisableLogging();
        }

        /// <summary>
        /// Test teleport procedures when the target simulator create agent step is refused.
        /// </summary>
        [Test]
        public void TestSameSimulatorIsolatedRegions_DeniedOnCreateAgent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);
            Vector3 preTeleportPosition = new Vector3(30, 31, 32);

            EntityTransferModule etmA = new EntityTransferModule();
            EntityTransferModule etmB = new EntityTransferModule();
            LocalSimulationConnectorModule lscm = new LocalSimulationConnectorModule();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("EntityTransferModule", etmA.Name);
            config.Configs["Modules"].Set("SimulationServices", lscm.Name);

            config.AddConfig("EntityTransfer");

            // In order to run a single threaded regression test we do not want the entity transfer module waiting
            // for a callback from the destination scene before removing its avatar data.
            config.Configs["EntityTransfer"].Set("wait_for_callback", false);

            SceneHelpers sh = new SceneHelpers();
            TestScene sceneA = sh.SetupScene("sceneA", TestHelpers.ParseTail(0x100), 1000, 1000);
            TestScene sceneB = sh.SetupScene("sceneB", TestHelpers.ParseTail(0x200), 1002, 1000);

            SceneHelpers.SetupSceneModules(sceneA, config, etmA);
            SceneHelpers.SetupSceneModules(sceneB, config, etmB);

            // Shared scene modules
            SceneHelpers.SetupSceneModules(new Scene[] { sceneA, sceneB }, config, lscm);

            Vector3 teleportPosition = new Vector3(10, 11, 12);
            Vector3 teleportLookAt = new Vector3(20, 21, 22);

            ScenePresence sp = SceneHelpers.AddScenePresence(sceneA, userId);
            sp.AbsolutePosition = preTeleportPosition;

            // Make sceneB refuse CreateAgent
            sceneB.LoginsEnabled = false;

            sceneA.RequestTeleportLocation(
                sp.ControllingClient,
                sceneB.RegionInfo.RegionHandle,
                teleportPosition,
                teleportLookAt,
                (uint)TeleportFlags.ViaLocation);

//            ((TestClient)sp.ControllingClient).CompleteTeleportClientSide();

            Assert.That(sceneB.GetScenePresence(userId), Is.Null);

            ScenePresence sceneASp = sceneA.GetScenePresence(userId);
            Assert.That(sceneASp, Is.Not.Null);
            Assert.That(sceneASp.Scene.RegionInfo.RegionName, Is.EqualTo(sceneA.RegionInfo.RegionName));
            Assert.That(sceneASp.AbsolutePosition, Is.EqualTo(preTeleportPosition));

            Assert.That(sceneA.GetRootAgentCount(), Is.EqualTo(1));
            Assert.That(sceneA.GetChildAgentCount(), Is.EqualTo(0));
            Assert.That(sceneB.GetRootAgentCount(), Is.EqualTo(0));
            Assert.That(sceneB.GetChildAgentCount(), Is.EqualTo(0));

            // TODO: Add assertions to check correct circuit details in both scenes.

            // Lookat is sent to the client only - sp.Lookat does not yield the same thing (calculation from camera
            // position instead).
//            Assert.That(sp.Lookat, Is.EqualTo(teleportLookAt));

//            TestHelpers.DisableLogging();
        }

        /// <summary>
        /// Test teleport when the destination region does not process (or does not receive) the connection attempt
        /// from the viewer.
        /// </summary>
        /// <remarks>
        /// This could be quite a common case where the source region can connect to a remove destination region
        /// (for CreateAgent) but the viewer cannot reach the destination region due to network issues.
        /// </remarks>
        [Test]
        public void TestSameSimulatorIsolatedRegions_DestinationDidNotProcessViewerConnection()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);
            Vector3 preTeleportPosition = new Vector3(30, 31, 32);

            EntityTransferModule etmA = new EntityTransferModule();
            EntityTransferModule etmB = new EntityTransferModule();

            LocalSimulationConnectorModule lscm = new LocalSimulationConnectorModule();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("EntityTransferModule", etmA.Name);
            config.Configs["Modules"].Set("SimulationServices", lscm.Name);

            config.AddConfig("EntityTransfer");

            // In order to run a single threaded regression test we do not want the entity transfer module waiting
            // for a callback from the destination scene before removing its avatar data.
            config.Configs["EntityTransfer"].Set("wait_for_callback", false);

//            config.AddConfig("Startup");
//            config.Configs["Startup"].Set("serverside_object_permissions", true);

            SceneHelpers sh = new SceneHelpers();
            TestScene sceneA = sh.SetupScene("sceneA", TestHelpers.ParseTail(0x100), 1000, 1000);
            TestScene sceneB = sh.SetupScene("sceneB", TestHelpers.ParseTail(0x200), 1002, 1000);

            SceneHelpers.SetupSceneModules(sceneA, config, etmA );

            // We need to set up the permisions module on scene B so that our later use of agent limit to deny
            // QueryAccess won't succeed anyway because administrators are always allowed in and the default
            // IsAdministrator if no permissions module is present is true.
            SceneHelpers.SetupSceneModules(sceneB, config, new object[] { new DefaultPermissionsModule(), etmB });

            // Shared scene modules
            SceneHelpers.SetupSceneModules(new Scene[] { sceneA, sceneB }, config, lscm);

            Vector3 teleportPosition = new Vector3(10, 11, 12);
            Vector3 teleportLookAt = new Vector3(20, 21, 22);

            ScenePresence sp = SceneHelpers.AddScenePresence(sceneA, userId);
            sp.AbsolutePosition = preTeleportPosition;

            sceneA.RequestTeleportLocation(
                sp.ControllingClient,
                sceneB.RegionInfo.RegionHandle,
                teleportPosition,
                teleportLookAt,
                (uint)TeleportFlags.ViaLocation);

            // FIXME: Not setting up InformClientOfNeighbour on the TestClient means that it does not initiate 
            // communication with the destination region.  But this is a very non-obvious way of doing it - really we
            // should be forced to expicitly set this up.

            Assert.That(sceneB.GetScenePresence(userId), Is.Null);

            ScenePresence sceneASp = sceneA.GetScenePresence(userId);
            Assert.That(sceneASp, Is.Not.Null);
            Assert.That(sceneASp.Scene.RegionInfo.RegionName, Is.EqualTo(sceneA.RegionInfo.RegionName));
            Assert.That(sceneASp.AbsolutePosition, Is.EqualTo(preTeleportPosition));

            Assert.That(sceneA.GetRootAgentCount(), Is.EqualTo(1));
            Assert.That(sceneA.GetChildAgentCount(), Is.EqualTo(0));
            Assert.That(sceneB.GetRootAgentCount(), Is.EqualTo(0));
            Assert.That(sceneB.GetChildAgentCount(), Is.EqualTo(0));

            // TODO: Add assertions to check correct circuit details in both scenes.

            // Lookat is sent to the client only - sp.Lookat does not yield the same thing (calculation from camera
            // position instead).
//            Assert.That(sp.Lookat, Is.EqualTo(teleportLookAt));

//            TestHelpers.DisableLogging();
        }

/*
        [Test]
        public void TestSameSimulatorNeighbouringRegionsV1()
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
            IConfig entityTransferConfig = config.AddConfig("EntityTransfer");

            // In order to run a single threaded regression test we do not want the entity transfer module waiting
            // for a callback from the destination scene before removing its avatar data.
            entityTransferConfig.Set("wait_for_callback", false);

            SceneHelpers sh = new SceneHelpers();
            TestScene sceneA = sh.SetupScene("sceneA", TestHelpers.ParseTail(0x100), 1000, 1000);
            TestScene sceneB = sh.SetupScene("sceneB", TestHelpers.ParseTail(0x200), 1001, 1000);

            SceneHelpers.SetupSceneModules(new Scene[] { sceneA, sceneB }, config, lscm);
            SceneHelpers.SetupSceneModules(sceneA, config, new CapabilitiesModule(), etmA);
            SceneHelpers.SetupSceneModules(sceneB, config, new CapabilitiesModule(), etmB);

            // FIXME: Hack - this is here temporarily to revert back to older entity transfer behaviour
            lscm.ServiceVersion = 0.1f;

            Vector3 teleportPosition = new Vector3(10, 11, 12);
            Vector3 teleportLookAt = new Vector3(20, 21, 22);

            AgentCircuitData acd = SceneHelpers.GenerateAgentData(userId);
            TestClient tc = new TestClient(acd, sceneA);
            List<TestClient> destinationTestClients = new List<TestClient>();
            EntityTransferHelpers.SetupInformClientOfNeighbourTriggersNeighbourClientCreate(tc, destinationTestClients);

            ScenePresence beforeSceneASp = SceneHelpers.AddScenePresence(sceneA, tc, acd);
            beforeSceneASp.AbsolutePosition = new Vector3(30, 31, 32);

            Assert.That(beforeSceneASp, Is.Not.Null);
            Assert.That(beforeSceneASp.IsChildAgent, Is.False);

            ScenePresence beforeSceneBSp = sceneB.GetScenePresence(userId);
            Assert.That(beforeSceneBSp, Is.Not.Null);
            Assert.That(beforeSceneBSp.IsChildAgent, Is.True);

            // In this case, we will not receieve a second InformClientOfNeighbour since the viewer already knows
            // about the neighbour region it is teleporting to.
            sceneA.RequestTeleportLocation(
                beforeSceneASp.ControllingClient,
                sceneB.RegionInfo.RegionHandle,
                teleportPosition,
                teleportLookAt,
                (uint)TeleportFlags.ViaLocation);

            destinationTestClients[0].CompleteMovement();

            ScenePresence afterSceneASp = sceneA.GetScenePresence(userId);
            Assert.That(afterSceneASp, Is.Not.Null);
            Assert.That(afterSceneASp.IsChildAgent, Is.True);

            ScenePresence afterSceneBSp = sceneB.GetScenePresence(userId);
            Assert.That(afterSceneBSp, Is.Not.Null);
            Assert.That(afterSceneBSp.IsChildAgent, Is.False);
            Assert.That(afterSceneBSp.Scene.RegionInfo.RegionName, Is.EqualTo(sceneB.RegionInfo.RegionName));
            Assert.That(afterSceneBSp.AbsolutePosition, Is.EqualTo(teleportPosition));

            Assert.That(sceneA.GetRootAgentCount(), Is.EqualTo(0));
            Assert.That(sceneA.GetChildAgentCount(), Is.EqualTo(1));
            Assert.That(sceneB.GetRootAgentCount(), Is.EqualTo(1));
            Assert.That(sceneB.GetChildAgentCount(), Is.EqualTo(0));

            // TODO: Add assertions to check correct circuit details in both scenes.

            // Lookat is sent to the client only - sp.Lookat does not yield the same thing (calculation from camera
            // position instead).
//            Assert.That(sp.Lookat, Is.EqualTo(teleportLookAt));

//            TestHelpers.DisableLogging();
        }
*/

        [Test]
        public void TestSameSimulatorNeighbouringRegionsV2()
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
            TestScene sceneB = sh.SetupScene("sceneB", TestHelpers.ParseTail(0x200), 1001, 1000);

            SceneHelpers.SetupSceneModules(new Scene[] { sceneA, sceneB }, config, lscm);
            SceneHelpers.SetupSceneModules(sceneA, config, new CapabilitiesModule(), etmA);
            SceneHelpers.SetupSceneModules(sceneB, config, new CapabilitiesModule(), etmB);

            Vector3 teleportPosition = new Vector3(10, 11, 12);
            Vector3 teleportLookAt = new Vector3(20, 21, 22);

            AgentCircuitData acd = SceneHelpers.GenerateAgentData(userId);
            TestClient tc = new TestClient(acd, sceneA);
            List<TestClient> destinationTestClients = new List<TestClient>();
            EntityTransferHelpers.SetupInformClientOfNeighbourTriggersNeighbourClientCreate(tc, destinationTestClients);

            ScenePresence beforeSceneASp = SceneHelpers.AddScenePresence(sceneA, tc, acd);
            beforeSceneASp.AbsolutePosition = new Vector3(30, 31, 32);

            Assert.That(beforeSceneASp, Is.Not.Null);
            Assert.That(beforeSceneASp.IsChildAgent, Is.False);

            ScenePresence beforeSceneBSp = sceneB.GetScenePresence(userId);
            Assert.That(beforeSceneBSp, Is.Not.Null);
            Assert.That(beforeSceneBSp.IsChildAgent, Is.True);

            // Here, we need to make clientA's receipt of SendRegionTeleport trigger clientB's CompleteMovement().  This
            // is to operate the teleport V2 mechanism where the EntityTransferModule will first request the client to
            // CompleteMovement to the region and then call UpdateAgent to the destination region to confirm the receipt
            // Both these operations will occur on different threads and will wait for each other.
            // We have to do this via ThreadPool directly since FireAndForget has been switched to sync for the V1
            // test protocol, where we are trying to avoid unpredictable async operations in regression tests.
            tc.OnTestClientSendRegionTeleport 
                += (regionHandle, simAccess, regionExternalEndPoint, locationID, flags, capsURL) 
                    => ThreadPool.UnsafeQueueUserWorkItem(o => destinationTestClients[0].CompleteMovement(), null);

            sceneA.RequestTeleportLocation(
                beforeSceneASp.ControllingClient,
                sceneB.RegionInfo.RegionHandle,
                teleportPosition,
                teleportLookAt,
                (uint)TeleportFlags.ViaLocation);

            ScenePresence afterSceneASp = sceneA.GetScenePresence(userId);
            Assert.That(afterSceneASp, Is.Not.Null);
            Assert.That(afterSceneASp.IsChildAgent, Is.True);

            ScenePresence afterSceneBSp = sceneB.GetScenePresence(userId);
            Assert.That(afterSceneBSp, Is.Not.Null);
            Assert.That(afterSceneBSp.IsChildAgent, Is.False);
            Assert.That(afterSceneBSp.Scene.RegionInfo.RegionName, Is.EqualTo(sceneB.RegionInfo.RegionName));
            Assert.That(afterSceneBSp.AbsolutePosition, Is.EqualTo(teleportPosition));

            Assert.That(sceneA.GetRootAgentCount(), Is.EqualTo(0));
            Assert.That(sceneA.GetChildAgentCount(), Is.EqualTo(1));
            Assert.That(sceneB.GetRootAgentCount(), Is.EqualTo(1));
            Assert.That(sceneB.GetChildAgentCount(), Is.EqualTo(0));

            // TODO: Add assertions to check correct circuit details in both scenes.

            // Lookat is sent to the client only - sp.Lookat does not yield the same thing (calculation from camera
            // position instead).
//            Assert.That(sp.Lookat, Is.EqualTo(teleportLookAt));

//            TestHelpers.DisableLogging();
        }
    }
}
