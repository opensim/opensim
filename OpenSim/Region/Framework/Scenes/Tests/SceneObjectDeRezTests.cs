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
using OpenSim.Framework.Communications;
using OpenSim.Region.CoreModules.Framework.InventoryAccess;
using OpenSim.Region.CoreModules.World.Permissions;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    /// <summary>
    /// Tests derez of scene objects.
    /// </summary>
    /// <remarks>
    /// This is at a level above the SceneObjectBasicTests, which act on the scene directly.
    /// TODO: These tests are incomplete - need to test more kinds of derez (e.g. return object).
    /// </remarks>
    [TestFixture]
    public class SceneObjectDeRezTests : OpenSimTestCase
    {
        /// <summary>
        /// Test deleting an object from a scene.
        /// </summary>
        [Test]
        public void TestDeRezSceneObject()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();
                        
            UUID userId = UUID.Parse("10000000-0000-0000-0000-000000000001");
            
            TestScene scene = new SceneHelpers().SetupScene();
            IConfigSource configSource = new IniConfigSource();
            IConfig config = configSource.AddConfig("Startup");
            config.Set("serverside_object_permissions", true);
            SceneHelpers.SetupSceneModules(scene, configSource, new object[] { new PermissionsModule() });
            IClientAPI client = SceneHelpers.AddScenePresence(scene, userId).ControllingClient;
            
            // Turn off the timer on the async sog deleter - we'll crank it by hand for this test.
            AsyncSceneObjectGroupDeleter sogd = scene.SceneObjectGroupDeleter;
            sogd.Enabled = false;            
            
            SceneObjectPart part
                = new SceneObjectPart(userId, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero);
            part.Name = "obj1";
            scene.AddNewSceneObject(new SceneObjectGroup(part), false);

            List<uint> localIds = new List<uint>();
            localIds.Add(part.LocalId);
            scene.DeRezObjects(client, localIds, UUID.Zero, DeRezAction.Delete, UUID.Zero);

            // Check that object isn't deleted until we crank the sogd handle.
            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(part.LocalId);
            Assert.That(retrievedPart, Is.Not.Null);
            Assert.That(retrievedPart.ParentGroup.IsDeleted, Is.False);

            sogd.InventoryDeQueueAndDelete();
            
            SceneObjectPart retrievedPart2 = scene.GetSceneObjectPart(part.LocalId);
            Assert.That(retrievedPart2, Is.Null);
        }
        
        /// <summary>
        /// Test deleting an object from a scene where the deleter is not the owner
        /// </summary>
        /// 
        /// This test assumes that the deleter is not a god.       
        [Test]
        public void TestDeRezSceneObjectNotOwner()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();
                        
            UUID userId = UUID.Parse("10000000-0000-0000-0000-000000000001");
            UUID objectOwnerId = UUID.Parse("20000000-0000-0000-0000-000000000001");
            
            TestScene scene = new SceneHelpers().SetupScene();
            IConfigSource configSource = new IniConfigSource();
            IConfig config = configSource.AddConfig("Startup");
            config.Set("serverside_object_permissions", true);
            SceneHelpers.SetupSceneModules(scene, configSource, new object[] { new PermissionsModule() });            
            IClientAPI client = SceneHelpers.AddScenePresence(scene, userId).ControllingClient;
            
            // Turn off the timer on the async sog deleter - we'll crank it by hand for this test.
            AsyncSceneObjectGroupDeleter sogd = scene.SceneObjectGroupDeleter;
            sogd.Enabled = false;            
            
            SceneObjectPart part
                = new SceneObjectPart(objectOwnerId, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero);
            part.Name = "obj1";
            scene.AddNewSceneObject(new SceneObjectGroup(part), false);
            List<uint> localIds = new List<uint>();
            localIds.Add(part.LocalId);            

            scene.DeRezObjects(client, localIds, UUID.Zero, DeRezAction.Delete, UUID.Zero);
            sogd.InventoryDeQueueAndDelete();
            
            // Object should still be in the scene.
            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(part.LocalId);
            Assert.That(retrievedPart.UUID, Is.EqualTo(part.UUID));
        }   
 
        /// <summary>
        /// Test deleting an object asynchronously to user inventory.
        /// </summary>
        [Test]
        public void TestDeleteSceneObjectAsyncToUserInventory()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID agentId = UUID.Parse("00000000-0000-0000-0000-000000000001");
            string myObjectName = "Fred";

            TestScene scene = new SceneHelpers().SetupScene();

            IConfigSource configSource = new IniConfigSource();
            IConfig config = configSource.AddConfig("Modules");            
            config.Set("InventoryAccessModule", "BasicInventoryAccessModule");
            SceneHelpers.SetupSceneModules(
                scene, configSource, new object[] { new BasicInventoryAccessModule() });

            SceneHelpers.SetupSceneModules(scene, new object[] { });

            // Turn off the timer on the async sog deleter - we'll crank it by hand for this test.
            AsyncSceneObjectGroupDeleter sogd = scene.SceneObjectGroupDeleter;
            sogd.Enabled = false;

            SceneObjectGroup so = SceneHelpers.AddSceneObject(scene, myObjectName, agentId);

            UserAccount ua = UserAccountHelpers.CreateUserWithInventory(scene, agentId);
            InventoryFolderBase folder1
                = UserInventoryHelpers.CreateInventoryFolder(scene.InventoryService, ua.PrincipalID, "folder1");

            IClientAPI client = SceneHelpers.AddScenePresence(scene, agentId).ControllingClient;
            scene.DeRezObjects(client, new List<uint>() { so.LocalId }, UUID.Zero, DeRezAction.Take, folder1.ID);

            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(so.LocalId);

            Assert.That(retrievedPart, Is.Not.Null);
            Assert.That(so.IsDeleted, Is.False);

            sogd.InventoryDeQueueAndDelete();

            Assert.That(so.IsDeleted, Is.True);

            SceneObjectPart retrievedPart2 = scene.GetSceneObjectPart(so.LocalId);
            Assert.That(retrievedPart2, Is.Null);

//            SceneSetupHelpers.DeleteSceneObjectAsync(scene, part, DeRezAction.Take, userInfo.RootFolder.ID, client);

            InventoryItemBase retrievedItem
                = UserInventoryHelpers.GetInventoryItem(
                    scene.InventoryService, ua.PrincipalID, "folder1/" + myObjectName);

            // Check that we now have the taken part in our inventory
            Assert.That(retrievedItem, Is.Not.Null);

            // Check that the taken part has actually disappeared
//            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(part.LocalId);
//            Assert.That(retrievedPart, Is.Null);
        }
    }
}