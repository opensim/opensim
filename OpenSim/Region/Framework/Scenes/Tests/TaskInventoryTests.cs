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
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.CoreModules.Avatar.Inventory.Archiver;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.Framework.Tests
{
    [TestFixture]
    public class TaskInventoryTests : OpenSimTestCase
    {
        [Test]
        public void TestAddTaskInventoryItem()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            Scene scene = new SceneHelpers().SetupScene();
            UserAccount user1 = UserAccountHelpers.CreateUserWithInventory(scene);
            SceneObjectGroup sog1 = SceneHelpers.CreateSceneObject(1, user1.PrincipalID);
            SceneObjectPart sop1 = sog1.RootPart;

            // Create an object embedded inside the first
            UUID taskSceneObjectItemId = UUID.Parse("00000000-0000-0000-0000-100000000000");
            TaskInventoryHelpers.AddSceneObject(scene.AssetService, sop1, "tso", taskSceneObjectItemId, user1.PrincipalID);

            TaskInventoryItem addedItem = sop1.Inventory.GetInventoryItem(taskSceneObjectItemId);
            Assert.That(addedItem.ItemID, Is.EqualTo(taskSceneObjectItemId));
            Assert.That(addedItem.OwnerID, Is.EqualTo(user1.PrincipalID));
            Assert.That(addedItem.ParentID, Is.EqualTo(sop1.UUID));
            Assert.That(addedItem.InvType, Is.EqualTo((int)InventoryType.Object));
            Assert.That(addedItem.Type, Is.EqualTo((int)AssetType.Object));
        }

        [Test]
        public void TestRezObjectFromInventoryItem()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            Scene scene = new SceneHelpers().SetupScene();
            UserAccount user1 = UserAccountHelpers.CreateUserWithInventory(scene);
            SceneObjectGroup sog1 = SceneHelpers.CreateSceneObject(1, user1.PrincipalID);
            SceneObjectPart sop1 = sog1.RootPart;

            // Create an object embedded inside the first
            UUID taskSceneObjectItemId = UUID.Parse("00000000-0000-0000-0000-100000000000");
            TaskInventoryItem taskSceneObjectItem
                = TaskInventoryHelpers.AddSceneObject(scene.AssetService, sop1, "tso", taskSceneObjectItemId, user1.PrincipalID);

            scene.AddSceneObject(sog1);

            Vector3 rezPos = new Vector3(10, 10, 10);
            Quaternion rezRot = new Quaternion(0.5f, 0.5f, 0.5f, 0.5f);
            Vector3 rezVel = new Vector3(2, 2, 2);

            scene.RezObject(sop1, taskSceneObjectItem, rezPos, rezRot, rezVel, 0,false);

            SceneObjectGroup rezzedObject = scene.GetSceneObjectGroup("tso");

            Assert.That(rezzedObject, Is.Not.Null);
            Assert.That(rezzedObject.AbsolutePosition, Is.EqualTo(rezPos));

            // Velocity doesn't get applied, probably because there is no physics in tests (yet)
//            Assert.That(rezzedObject.Velocity, Is.EqualTo(rezVel));
            Assert.That(rezzedObject.Velocity, Is.EqualTo(Vector3.Zero));

            // Confusingly, this isn't the rezzedObject.Rotation
            Assert.That(rezzedObject.RootPart.RotationOffset, Is.EqualTo(rezRot));
        }

        /// <summary>
        /// Test MoveTaskInventoryItem from a part inventory to a user inventory where the item has no parent folder assigned.
        /// </summary>
        /// <remarks>
        /// This should place it in the most suitable user folder.
        /// </remarks>
        [Test]
        public void TestMoveTaskInventoryItem()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            Scene scene = new SceneHelpers().SetupScene();
            UserAccount user1 = UserAccountHelpers.CreateUserWithInventory(scene);
            SceneObjectGroup sog1 = SceneHelpers.CreateSceneObject(1, user1.PrincipalID);
            SceneObjectPart sop1 = sog1.RootPart;
            TaskInventoryItem sopItem1
                = TaskInventoryHelpers.AddNotecard(
                    scene.AssetService, sop1, "ncItem", TestHelpers.ParseTail(0x800), TestHelpers.ParseTail(0x900), "Hello World!");

            InventoryFolderBase folder
                = InventoryArchiveUtils.FindFoldersByPath(scene.InventoryService, user1.PrincipalID, "Objects")[0];

            // Perform test
            string message;
            scene.MoveTaskInventoryItem(user1.PrincipalID, folder.ID, sop1, sopItem1.ItemID, out message);

            InventoryItemBase ncUserItem
                = InventoryArchiveUtils.FindItemByPath(scene.InventoryService, user1.PrincipalID, "Objects/ncItem");
            Assert.That(ncUserItem, Is.Not.Null, "Objects/ncItem was not found");
        }

        /// <summary>
        /// Test MoveTaskInventoryItem from a part inventory to a user inventory where the item has no parent folder assigned.
        /// </summary>
        /// <remarks>
        /// This should place it in the most suitable user folder.
        /// </remarks>
        [Test]
        public void TestMoveTaskInventoryItemNoParent()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            Scene scene = new SceneHelpers().SetupScene();
            UserAccount user1 = UserAccountHelpers.CreateUserWithInventory(scene);
            SceneObjectGroup sog1 = SceneHelpers.CreateSceneObject(1, user1.PrincipalID);

            SceneObjectPart sop1 = sog1.RootPart;
            TaskInventoryItem sopItem1
                = TaskInventoryHelpers.AddNotecard(
                    scene.AssetService, sop1, "ncItem", TestHelpers.ParseTail(0x800), TestHelpers.ParseTail(0x900), "Hello World!");

            // Perform test
            string message;
            scene.MoveTaskInventoryItem(user1.PrincipalID, UUID.Zero, sop1, sopItem1.ItemID, out message);

            InventoryItemBase ncUserItem
                = InventoryArchiveUtils.FindItemByPath(scene.InventoryService, user1.PrincipalID, "Notecards/ncItem");
            Assert.That(ncUserItem, Is.Not.Null, "Notecards/ncItem was not found");
        }
    }
}