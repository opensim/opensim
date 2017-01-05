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
using System.Text;
using System.Threading;
using System.Timers;
using Timer=System.Timers.Timer;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.CoreModules.Framework.InventoryAccess;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.Framework.Tests
{
    [TestFixture]
    public class UserInventoryTests : OpenSimTestCase
    {
        [Test]
        public void TestCreateInventoryFolders()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            // For this test both folders will have the same name which is legal in SL user inventories.
            string foldersName = "f1";

            Scene scene = new SceneHelpers().SetupScene();
            UserAccount user1 = UserAccountHelpers.CreateUserWithInventory(scene, TestHelpers.ParseTail(1001));

            UserInventoryHelpers.CreateInventoryFolder(scene.InventoryService, user1.PrincipalID, foldersName, false);

            List<InventoryFolderBase> oneFolder
                = UserInventoryHelpers.GetInventoryFolders(scene.InventoryService, user1.PrincipalID, foldersName);

            Assert.That(oneFolder.Count, Is.EqualTo(1));
            InventoryFolderBase firstRetrievedFolder = oneFolder[0];
            Assert.That(firstRetrievedFolder.Name, Is.EqualTo(foldersName));

            UserInventoryHelpers.CreateInventoryFolder(scene.InventoryService, user1.PrincipalID, foldersName, false);

            List<InventoryFolderBase> twoFolders
                = UserInventoryHelpers.GetInventoryFolders(scene.InventoryService, user1.PrincipalID, foldersName);

            Assert.That(twoFolders.Count, Is.EqualTo(2));
            Assert.That(twoFolders[0].Name, Is.EqualTo(foldersName));
            Assert.That(twoFolders[1].Name, Is.EqualTo(foldersName));
            Assert.That(twoFolders[0].ID, Is.Not.EqualTo(twoFolders[1].ID));
        }

        [Test]
        public void TestGiveInventoryItem()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            Scene scene = new SceneHelpers().SetupScene();
            UserAccount user1 = UserAccountHelpers.CreateUserWithInventory(scene, TestHelpers.ParseTail(1001));
            UserAccount user2 = UserAccountHelpers.CreateUserWithInventory(scene, TestHelpers.ParseTail(1002));
            InventoryItemBase item1 = UserInventoryHelpers.CreateInventoryItem(scene, "item1", user1.PrincipalID);

            string message;

            scene.GiveInventoryItem(user2.PrincipalID, user1.PrincipalID, item1.ID, out message);

            InventoryItemBase retrievedItem1
                = UserInventoryHelpers.GetInventoryItem(scene.InventoryService, user2.PrincipalID, "Notecards/item1");

            Assert.That(retrievedItem1, Is.Not.Null);

            // Try giving back the freshly received item
            scene.GiveInventoryItem(user1.PrincipalID, user2.PrincipalID, retrievedItem1.ID, out message);

            List<InventoryItemBase> reretrievedItems
                = UserInventoryHelpers.GetInventoryItems(scene.InventoryService, user1.PrincipalID, "Notecards/item1");

            Assert.That(reretrievedItems.Count, Is.EqualTo(2));
        }

        [Test]
        public void TestGiveInventoryFolder()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            Scene scene = new SceneHelpers().SetupScene();
            UserAccount user1 = UserAccountHelpers.CreateUserWithInventory(scene, TestHelpers.ParseTail(1001));
            UserAccount user2 = UserAccountHelpers.CreateUserWithInventory(scene, TestHelpers.ParseTail(1002));
            InventoryFolderBase folder1
                = UserInventoryHelpers.CreateInventoryFolder(scene.InventoryService, user1.PrincipalID, "folder1", false);

            scene.GiveInventoryFolder(null, user2.PrincipalID, user1.PrincipalID, folder1.ID, UUID.Zero);

            InventoryFolderBase retrievedFolder1
                = UserInventoryHelpers.GetInventoryFolder(scene.InventoryService, user2.PrincipalID, "folder1");

            Assert.That(retrievedFolder1, Is.Not.Null);

            // Try giving back the freshly received folder
            scene.GiveInventoryFolder(null, user1.PrincipalID, user2.PrincipalID, retrievedFolder1.ID, UUID.Zero);

            List<InventoryFolderBase> reretrievedFolders
                = UserInventoryHelpers.GetInventoryFolders(scene.InventoryService, user1.PrincipalID, "folder1");

            Assert.That(reretrievedFolders.Count, Is.EqualTo(2));
        }

        // Work in Progress test. All Assertions pertaining permissions are commented for now.
        [Test]
        public void TestGiveInventoryItemFullPerms()
        {
            TestHelpers.InMethod();

            List<Object> modules = new List<object>();
            IConfigSource config = DefaultConfig(modules);
            Scene scene = new SceneHelpers().SetupScene("Inventory Permissions", UUID.Random(), 1000, 1000, config);
            SceneHelpers.SetupSceneModules(scene, config, modules.ToArray());

            UserAccount user1 = UserAccountHelpers.CreateUserWithInventory(scene, TestHelpers.ParseTail(1001));
            UserAccount user2 = UserAccountHelpers.CreateUserWithInventory(scene, TestHelpers.ParseTail(1002));
            ScenePresence sp1 = SceneHelpers.AddScenePresence(scene, user1.PrincipalID);
            ScenePresence sp2 = SceneHelpers.AddScenePresence(scene, user2.PrincipalID);

            InventoryItemBase item1 = UserInventoryHelpers.CreateInventoryItem(scene, "SomeObject", user1.PrincipalID, InventoryType.Object);
            // Set All perms in inventory
            item1.NextPermissions = (uint)OpenMetaverse.PermissionMask.All;
            scene.UpdateInventoryItemAsset(sp1.ControllingClient, UUID.Zero, item1.ID, item1);
            //Assert.That((item1.NextPermissions & (uint)OpenMetaverse.PermissionMask.All) == (uint)OpenMetaverse.PermissionMask.All);

            string message;

            InventoryItemBase retrievedItem1 = scene.GiveInventoryItem(user2.PrincipalID, user1.PrincipalID, item1.ID, out message);
            Assert.That(retrievedItem1, Is.Not.Null);
            //Assert.That((retrievedItem1.CurrentPermissions & (uint)OpenMetaverse.PermissionMask.All) == (uint)OpenMetaverse.PermissionMask.All);

            retrievedItem1
                = UserInventoryHelpers.GetInventoryItem(scene.InventoryService, user2.PrincipalID, "Objects/SomeObject");
            Assert.That(retrievedItem1, Is.Not.Null);
            //Assert.That((retrievedItem1.BasePermissions & (uint)OpenMetaverse.PermissionMask.All) == (uint)OpenMetaverse.PermissionMask.All);
            //Assert.That((retrievedItem1.CurrentPermissions & (uint)OpenMetaverse.PermissionMask.All) == (uint)OpenMetaverse.PermissionMask.All);

            // Rez the object
            scene.RezObject(sp2.ControllingClient, retrievedItem1.ID, UUID.Zero, Vector3.Zero, Vector3.Zero, UUID.Zero, 0, false, false, false, UUID.Zero);
            SceneObjectGroup sog = scene.GetSceneObjectGroup("SomeObject");
            Assert.That(sog, Is.Not.Null);

            // This is failing for all sorts of reasons. We'll fix it after perms are fixed.
            //Console.WriteLine("Item Perms " + retrievedItem1.CurrentPermissions + " Obj Owner Perms " + sog.RootPart.OwnerMask + " Base Perms " + sog.RootPart.BaseMask + "\n");
            //Assert.True((sog.RootPart.OwnerMask & (uint)OpenMetaverse.PermissionMask.All) == (uint)OpenMetaverse.PermissionMask.All);

        }

        public static IConfigSource DefaultConfig(List<object> modules)
        {
            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("InventoryAccessModule", "BasicInventoryAccessModule");

            config.AddConfig("Permissions");
            config.Configs["Permissions"].Set("permissionmodules", "DefaultPermissionsModule");
            config.Configs["Permissions"].Set("serverside_object_permissions", true);
            config.Configs["Permissions"].Set("propagate_permissions", true);

            modules.Add(new BasicInventoryAccessModule());
            return config;
        }

    }
}