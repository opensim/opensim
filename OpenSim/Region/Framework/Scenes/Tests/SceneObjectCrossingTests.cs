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
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Framework.EntityTransfer;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation;
using OpenSim.Region.CoreModules.World.Land;
using OpenSim.Region.OptionalModules;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

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
            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);
            int sceneObjectIdTail = 0x2;

            EntityTransferModule etmA = new EntityTransferModule();
            EntityTransferModule etmB = new EntityTransferModule();
            LocalSimulationConnectorModule lscm = new LocalSimulationConnectorModule();

            IConfigSource config = new IniConfigSource();
            IConfig modulesConfig = config.AddConfig("Modules");
            modulesConfig.Set("EntityTransferModule", etmA.Name);
            modulesConfig.Set("SimulationServices", lscm.Name);
//            IConfig entityTransferConfig = config.AddConfig("EntityTransfer");

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

            Assert.IsNull(sceneA.GetSceneObjectGroup(so1Id));
            Assert.NotNull(sceneB.GetSceneObjectGroup(so1Id));
        }

        /// <summary>
        /// Test cross with no prim limit module.
        /// </summary>
        /// <remarks>
        /// XXX: This test may be better off in a specific PrimLimitsModuleTest class in optional module tests in the
        /// future (though it is configured as active by default, so not really optional).
        /// </remarks>
        [Test]
        public void TestCrossOnSameSimulatorPrimLimitsOkay()
        {
            TestHelpers.InMethod();
            TestHelpers.EnableLogging();

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

            // Remove?
//            IConfig entityTransferConfig = config.AddConfig("EntityTransfer");

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

            SceneObjectGroup so1 = SceneHelpers.AddSceneObject(sceneA, 1, userId, "", sceneObjectIdTail);
            UUID so1Id = so1.UUID;
            so1.AbsolutePosition = new Vector3(128, 10, 20);

            // Cross with a negative value.  We must make this call rather than setting AbsolutePosition directly
            // because only this will execute permission checks in the source region.
            sceneA.SceneGraph.UpdatePrimGroupPosition(so1.LocalId, new Vector3(128, -10, 20), userId);

            Assert.IsNull(sceneA.GetSceneObjectGroup(so1Id));
            Assert.NotNull(sceneB.GetSceneObjectGroup(so1Id));
        }
    }
}