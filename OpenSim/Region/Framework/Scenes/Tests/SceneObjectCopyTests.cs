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
using OpenSim.Region.CoreModules.Framework.EntityTransfer;
using OpenSim.Region.CoreModules.Framework.InventoryAccess;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation;
using OpenSim.Region.CoreModules.World.Permissions;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    /// <summary>
    /// Test copying of scene objects.
    /// </summary>
    /// <remarks>
    /// This is at a level above the SceneObjectBasicTests, which act on the scene directly.
    /// </remarks>
    [TestFixture]
    public class SceneObjectCopyTests : OpenSimTestCase
    {
        [TestFixtureSetUp]
        public void FixtureInit()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
            // This facility was added after the original async delete tests were written, so it may be possible now
            // to not bother explicitly disabling their async (since everything will be running sync).
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
        public void TestTakeCopyWhenCopierIsOwnerWithPerms()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("InventoryAccessModule", "BasicInventoryAccessModule");

            TestScene scene = new SceneHelpers().SetupScene("s1", TestHelpers.ParseTail(0x99), 1000, 1000, config);
            SceneHelpers.SetupSceneModules(scene, config, new PermissionsModule(), new BasicInventoryAccessModule());
            UserAccount ua = UserAccountHelpers.CreateUserWithInventory(scene, TestHelpers.ParseTail(0x1));
            TestClient client = (TestClient)SceneHelpers.AddScenePresence(scene, ua.PrincipalID).ControllingClient;

            // Turn off the timer on the async sog deleter - we'll crank it by hand for this test.
            AsyncSceneObjectGroupDeleter sogd = scene.SceneObjectGroupDeleter;
            sogd.Enabled = false;            

            SceneObjectGroup so = SceneHelpers.AddSceneObject(scene, "so1", ua.PrincipalID);
            uint soLocalId = so.LocalId;
//            so.UpdatePermissions(
//                ua.PrincipalID, (byte)PermissionWho.Owner, so.LocalId, (uint)OpenMetaverse.PermissionMask.Copy, 1);
//            so.UpdatePermissions(
//                ua.PrincipalID, (byte)PermissionWho.Owner, so.LocalId, (uint)OpenMetaverse.PermissionMask.Transfer, 0);
//            so.UpdatePermissions(
//                ua.PrincipalID, (byte)PermissionWho.Base, so.LocalId, (uint)OpenMetaverse.PermissionMask.Transfer, 0);
//            scene.HandleObjectPermissionsUpdate(client, client.AgentId, client.SessionId, (byte)PermissionWho.Owner, so.LocalId, (uint)OpenMetaverse.PermissionMask.Transfer, 0);

            // Ideally we might change these via client-focussed method calls as commented out above.  However, this
            // becomes very convoluted so we will set only the copy perm directly.
            so.RootPart.BaseMask = (uint)OpenMetaverse.PermissionMask.Copy;
//            so.RootPart.OwnerMask = (uint)OpenMetaverse.PermissionMask.Copy;

            List<uint> localIds = new List<uint>();
            localIds.Add(so.LocalId);

            // Specifying a UUID.Zero in this case will currently plop it in Lost and Found
            scene.DeRezObjects(client, localIds, UUID.Zero, DeRezAction.TakeCopy, UUID.Zero);

            // Check that object isn't copied until we crank the sogd handle.
            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(so.LocalId);
            Assert.That(retrievedPart, Is.Not.Null);
            Assert.That(retrievedPart.ParentGroup.IsDeleted, Is.False);

            sogd.InventoryDeQueueAndDelete();

            // Check that object is still there.
            SceneObjectPart retrievedPart2 = scene.GetSceneObjectPart(so.LocalId);
            Assert.That(retrievedPart2, Is.Not.Null);              
            Assert.That(client.ReceivedKills.Count, Is.EqualTo(0));

            // Check that we have a copy in inventory
            InventoryItemBase item 
                = UserInventoryHelpers.GetInventoryItem(scene.InventoryService, ua.PrincipalID, "Lost And Found/so1");
            Assert.That(item, Is.Not.Null);
        }

        [Test]
        public void TestTakeCopyWhenCopierIsOwnerWithoutPerms()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("InventoryAccessModule", "BasicInventoryAccessModule");

            TestScene scene = new SceneHelpers().SetupScene("s1", TestHelpers.ParseTail(0x99), 1000, 1000, config);
            SceneHelpers.SetupSceneModules(scene, config, new PermissionsModule(), new BasicInventoryAccessModule());
            UserAccount ua = UserAccountHelpers.CreateUserWithInventory(scene, TestHelpers.ParseTail(0x1));
            TestClient client = (TestClient)SceneHelpers.AddScenePresence(scene, ua.PrincipalID).ControllingClient;

            // Turn off the timer on the async sog deleter - we'll crank it by hand for this test.
            AsyncSceneObjectGroupDeleter sogd = scene.SceneObjectGroupDeleter;
            sogd.Enabled = false;            

            SceneObjectGroup so = SceneHelpers.AddSceneObject(scene, "so1", ua.PrincipalID);
            uint soLocalId = so.LocalId;

            so.RootPart.BaseMask = (uint)(OpenMetaverse.PermissionMask.All & ~OpenMetaverse.PermissionMask.Copy);
            //so.RootPart.OwnerMask = (uint)(OpenMetaverse.PermissionMask.Copy & ~OpenMetaverse.PermissionMask.Copy);

            List<uint> localIds = new List<uint>();
            localIds.Add(so.LocalId);

            // Specifying a UUID.Zero in this case will currently plop it in Lost and Found
            scene.DeRezObjects(client, localIds, UUID.Zero, DeRezAction.TakeCopy, UUID.Zero);

            // Check that object isn't copied until we crank the sogd handle.
            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(so.LocalId);
            Assert.That(retrievedPart, Is.Not.Null);
            Assert.That(retrievedPart.ParentGroup.IsDeleted, Is.False);

            sogd.InventoryDeQueueAndDelete();

            // Check that object is still there.
            SceneObjectPart retrievedPart2 = scene.GetSceneObjectPart(so.LocalId);
            Assert.That(retrievedPart2, Is.Not.Null);              
            Assert.That(client.ReceivedKills.Count, Is.EqualTo(0));

            // Check that we do not have a copy in inventory
            InventoryItemBase item 
                = UserInventoryHelpers.GetInventoryItem(scene.InventoryService, ua.PrincipalID, "Lost And Found/so1");
            Assert.That(item, Is.Null);
        }

        [Test]
        public void TestTakeCopyWhenCopierIsNotOwnerWithPerms()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("InventoryAccessModule", "BasicInventoryAccessModule");

            TestScene scene = new SceneHelpers().SetupScene("s1", TestHelpers.ParseTail(0x99), 1000, 1000, config);
            SceneHelpers.SetupSceneModules(scene, config, new PermissionsModule(), new BasicInventoryAccessModule());
            UserAccount ua = UserAccountHelpers.CreateUserWithInventory(scene, TestHelpers.ParseTail(0x1));
            TestClient client = (TestClient)SceneHelpers.AddScenePresence(scene, ua.PrincipalID).ControllingClient;

            // Turn off the timer on the async sog deleter - we'll crank it by hand for this test.
            AsyncSceneObjectGroupDeleter sogd = scene.SceneObjectGroupDeleter;
            sogd.Enabled = false;            

            SceneObjectGroup so = SceneHelpers.AddSceneObject(scene, "so1", TestHelpers.ParseTail(0x2));
            uint soLocalId = so.LocalId;

            // Base must allow transfer and copy
            so.RootPart.BaseMask = (uint)(OpenMetaverse.PermissionMask.Copy | OpenMetaverse.PermissionMask.Transfer);
            // Must be set so anyone can copy
            so.RootPart.EveryoneMask = (uint)OpenMetaverse.PermissionMask.Copy;

            List<uint> localIds = new List<uint>();
            localIds.Add(so.LocalId);

            // Specifying a UUID.Zero in this case will plop it in the Objects folder
            scene.DeRezObjects(client, localIds, UUID.Zero, DeRezAction.TakeCopy, UUID.Zero);

            // Check that object isn't copied until we crank the sogd handle.
            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(so.LocalId);
            Assert.That(retrievedPart, Is.Not.Null);
            Assert.That(retrievedPart.ParentGroup.IsDeleted, Is.False);

            sogd.InventoryDeQueueAndDelete();

            // Check that object is still there.
            SceneObjectPart retrievedPart2 = scene.GetSceneObjectPart(so.LocalId);
            Assert.That(retrievedPart2, Is.Not.Null);              
            Assert.That(client.ReceivedKills.Count, Is.EqualTo(0));

            // Check that we have a copy in inventory
            InventoryItemBase item 
                = UserInventoryHelpers.GetInventoryItem(scene.InventoryService, ua.PrincipalID, "Objects/so1");
            Assert.That(item, Is.Not.Null);
        }

        [Test]
        public void TestTakeCopyWhenCopierIsNotOwnerWithoutPerms()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("InventoryAccessModule", "BasicInventoryAccessModule");

            TestScene scene = new SceneHelpers().SetupScene("s1", TestHelpers.ParseTail(0x99), 1000, 1000, config);
            SceneHelpers.SetupSceneModules(scene, config, new PermissionsModule(), new BasicInventoryAccessModule());
            UserAccount ua = UserAccountHelpers.CreateUserWithInventory(scene, TestHelpers.ParseTail(0x1));
            TestClient client = (TestClient)SceneHelpers.AddScenePresence(scene, ua.PrincipalID).ControllingClient;

            // Turn off the timer on the async sog deleter - we'll crank it by hand for this test.
            AsyncSceneObjectGroupDeleter sogd = scene.SceneObjectGroupDeleter;
            sogd.Enabled = false;            

            SceneObjectGroup so = SceneHelpers.AddSceneObject(scene, "so1", TestHelpers.ParseTail(0x2));
            uint soLocalId = so.LocalId;

            {
                // Check that object is not copied if copy base perms is missing.
                // Should not allow copy if base does not have this.
                so.RootPart.BaseMask = (uint)OpenMetaverse.PermissionMask.Transfer;
                // Must be set so anyone can copy
                so.RootPart.EveryoneMask = (uint)OpenMetaverse.PermissionMask.Copy;

                // Check that object is not copied
                List<uint> localIds = new List<uint>();
                localIds.Add(so.LocalId);

                // Specifying a UUID.Zero in this case will plop it in the Objects folder if we have perms
                scene.DeRezObjects(client, localIds, UUID.Zero, DeRezAction.TakeCopy, UUID.Zero);

                // Check that object isn't copied until we crank the sogd handle.
                SceneObjectPart retrievedPart = scene.GetSceneObjectPart(so.LocalId);
                Assert.That(retrievedPart, Is.Not.Null);
                Assert.That(retrievedPart.ParentGroup.IsDeleted, Is.False);

                sogd.InventoryDeQueueAndDelete();
                // Check that object is still there.
                SceneObjectPart retrievedPart2 = scene.GetSceneObjectPart(so.LocalId);
                Assert.That(retrievedPart2, Is.Not.Null);              
                Assert.That(client.ReceivedKills.Count, Is.EqualTo(0));

                // Check that we have a copy in inventory
                InventoryItemBase item 
                    = UserInventoryHelpers.GetInventoryItem(scene.InventoryService, ua.PrincipalID, "Objects/so1");
                Assert.That(item, Is.Null);
            }

            {
                // Check that object is not copied if copy trans perms is missing.
                // Should not allow copy if base does not have this.
                so.RootPart.BaseMask = (uint)OpenMetaverse.PermissionMask.Copy;
                // Must be set so anyone can copy
                so.RootPart.EveryoneMask = (uint)OpenMetaverse.PermissionMask.Copy;

                // Check that object is not copied
                List<uint> localIds = new List<uint>();
                localIds.Add(so.LocalId);

                // Specifying a UUID.Zero in this case will plop it in the Objects folder if we have perms
                scene.DeRezObjects(client, localIds, UUID.Zero, DeRezAction.TakeCopy, UUID.Zero);

                // Check that object isn't copied until we crank the sogd handle.
                SceneObjectPart retrievedPart = scene.GetSceneObjectPart(so.LocalId);
                Assert.That(retrievedPart, Is.Not.Null);
                Assert.That(retrievedPart.ParentGroup.IsDeleted, Is.False);

                sogd.InventoryDeQueueAndDelete();
                // Check that object is still there.
                SceneObjectPart retrievedPart2 = scene.GetSceneObjectPart(so.LocalId);
                Assert.That(retrievedPart2, Is.Not.Null);              
                Assert.That(client.ReceivedKills.Count, Is.EqualTo(0));

                // Check that we have a copy in inventory
                InventoryItemBase item 
                    = UserInventoryHelpers.GetInventoryItem(scene.InventoryService, ua.PrincipalID, "Objects/so1");
                Assert.That(item, Is.Null);
            }

            {
                // Check that object is not copied if everyone copy perms is missing.
                // Should not allow copy if base does not have this.
                so.RootPart.BaseMask = (uint)(OpenMetaverse.PermissionMask.Copy | OpenMetaverse.PermissionMask.Transfer);
                // Make sure everyone perm does not allow copy
                so.RootPart.EveryoneMask = (uint)(OpenMetaverse.PermissionMask.All & ~OpenMetaverse.PermissionMask.Copy);

                // Check that object is not copied
                List<uint> localIds = new List<uint>();
                localIds.Add(so.LocalId);

                // Specifying a UUID.Zero in this case will plop it in the Objects folder if we have perms
                scene.DeRezObjects(client, localIds, UUID.Zero, DeRezAction.TakeCopy, UUID.Zero);

                // Check that object isn't copied until we crank the sogd handle.
                SceneObjectPart retrievedPart = scene.GetSceneObjectPart(so.LocalId);
                Assert.That(retrievedPart, Is.Not.Null);
                Assert.That(retrievedPart.ParentGroup.IsDeleted, Is.False);

                sogd.InventoryDeQueueAndDelete();
                // Check that object is still there.
                SceneObjectPart retrievedPart2 = scene.GetSceneObjectPart(so.LocalId);
                Assert.That(retrievedPart2, Is.Not.Null);              
                Assert.That(client.ReceivedKills.Count, Is.EqualTo(0));

                // Check that we have a copy in inventory
                InventoryItemBase item 
                    = UserInventoryHelpers.GetInventoryItem(scene.InventoryService, ua.PrincipalID, "Objects/so1");
                Assert.That(item, Is.Null);
            }
        }
    }
}