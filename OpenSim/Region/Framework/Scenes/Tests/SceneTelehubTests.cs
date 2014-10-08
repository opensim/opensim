/*
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
using OpenSim.Region.CoreModules.World.Estate;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    /// <summary>
    /// Scene telehub tests
    /// </summary>
    /// <remarks>
    /// TODO: Tests which run through normal functionality.  Currently, the only test is one that checks behaviour
    /// in the case of an error condition
    /// </remarks>
    [TestFixture]
    public class SceneTelehubTests : OpenSimTestCase
    {
        /// <summary>
        /// Test for desired behaviour when a telehub has no spawn points
        /// </summary>
        [Test]
        public void TestNoTelehubSpawnPoints()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            EstateManagementModule emm = new EstateManagementModule();

            SceneHelpers sh = new SceneHelpers();
            Scene scene = sh.SetupScene();
            SceneHelpers.SetupSceneModules(scene, emm);

            UUID telehubSceneObjectOwner = TestHelpers.ParseTail(0x1);

            SceneObjectGroup telehubSo = SceneHelpers.AddSceneObject(scene, "telehubObject", telehubSceneObjectOwner);

            emm.HandleOnEstateManageTelehub(null, UUID.Zero, UUID.Zero, "connect", telehubSo.LocalId);
            scene.RegionInfo.EstateSettings.AllowDirectTeleport = false;

            // Must still be possible to successfully log in
            UUID loggingInUserId = TestHelpers.ParseTail(0x2);

            UserAccount ua 
                = UserAccountHelpers.CreateUserWithInventory(scene, "Test", "User", loggingInUserId, "password");

            SceneHelpers.AddScenePresence(scene, ua);

            Assert.That(scene.GetScenePresence(loggingInUserId), Is.Not.Null);
        }

        /// <summary>
        /// Test for desired behaviour when the scene object nominated as a telehub object does not exist.
        /// </summary>
        [Test]
        public void TestNoTelehubSceneObject()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            EstateManagementModule emm = new EstateManagementModule();

            SceneHelpers sh = new SceneHelpers();
            Scene scene = sh.SetupScene();
            SceneHelpers.SetupSceneModules(scene, emm);

            UUID telehubSceneObjectOwner = TestHelpers.ParseTail(0x1);

            SceneObjectGroup telehubSo = SceneHelpers.AddSceneObject(scene, "telehubObject", telehubSceneObjectOwner);
            SceneObjectGroup spawnPointSo = SceneHelpers.AddSceneObject(scene, "spawnpointObject", telehubSceneObjectOwner);

            emm.HandleOnEstateManageTelehub(null, UUID.Zero, UUID.Zero, "connect", telehubSo.LocalId);
            emm.HandleOnEstateManageTelehub(null, UUID.Zero, UUID.Zero, "spawnpoint add", spawnPointSo.LocalId);
            scene.RegionInfo.EstateSettings.AllowDirectTeleport = false;

            scene.DeleteSceneObject(telehubSo, false);

            // Must still be possible to successfully log in
            UUID loggingInUserId = TestHelpers.ParseTail(0x2);

            UserAccount ua 
                = UserAccountHelpers.CreateUserWithInventory(scene, "Test", "User", loggingInUserId, "password");

            SceneHelpers.AddScenePresence(scene, ua);

            Assert.That(scene.GetScenePresence(loggingInUserId), Is.Not.Null);
        }
    }
}