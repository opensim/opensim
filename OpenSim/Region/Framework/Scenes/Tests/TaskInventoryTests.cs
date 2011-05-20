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
using OpenSim.Framework.Communications;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.CoreModules.Avatar.Inventory.Archiver;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;
using OpenSim.Tests.Common.Setup;

namespace OpenSim.Region.Framework.Tests
{
    [TestFixture]
    public class TaskInventoryTests
    {
        protected UserAccount CreateUser(Scene scene)
        {
            string userFirstName = "Jock";
            string userLastName = "Stirrup";
            string userPassword = "troll";
            UUID userId = UUID.Parse("00000000-0000-0000-0000-000000000020");
            return UserProfileTestUtils.CreateUserWithInventory(scene, userFirstName, userLastName, userId, userPassword);
        }
        
        protected SceneObjectGroup CreateSO1(Scene scene, UUID ownerId)
        {
            string part1Name = "part1";
            UUID part1Id = UUID.Parse("10000000-0000-0000-0000-000000000000");
            SceneObjectPart part1
                = new SceneObjectPart(ownerId, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero) 
                    { Name = part1Name, UUID = part1Id };
            return new SceneObjectGroup(part1);
        }
        
        protected TaskInventoryItem CreateSOItem1(Scene scene, SceneObjectPart part)
        {
            AssetNotecard nc = new AssetNotecard();
            nc.BodyText = "Hello World!";
            nc.Encode();
            UUID ncAssetUuid = new UUID("00000000-0000-0000-1000-000000000000");
            UUID ncItemUuid = new UUID("00000000-0000-0000-1100-000000000000");
            AssetBase ncAsset 
                = AssetHelpers.CreateAsset(ncAssetUuid, AssetType.Notecard, nc.AssetData, UUID.Zero);
            scene.AssetService.Store(ncAsset);
            TaskInventoryItem ncItem 
                = new TaskInventoryItem 
                    { Name = "ncItem", AssetID = ncAssetUuid, ItemID = ncItemUuid, 
                      Type = (int)AssetType.Notecard, InvType = (int)InventoryType.Notecard };
            part.Inventory.AddInventoryItem(ncItem, true); 
            
            return ncItem;
        }

        [Test]
        public void TestRezObjectFromInventoryItem()
        {
            TestHelper.InMethod();
//            log4net.Config.XmlConfigurator.Configure();
            
            Scene scene = SceneSetupHelpers.SetupScene();
            UserAccount user1 = CreateUser(scene);
            SceneObjectGroup sog1 = CreateSO1(scene, user1.PrincipalID);
            SceneObjectPart sop1 = sog1.RootPart;

            // Create an object embedded inside the first
            UUID taskSceneObjectItemId = UUID.Parse("00000000-0000-0000-0000-100000000000");

            SceneObjectGroup taskSceneObject = SceneSetupHelpers.CreateSceneObject(1, UUID.Zero);
            AssetBase taskSceneObjectAsset = AssetHelpers.CreateAsset(0x10, taskSceneObject);
            scene.AssetService.Store(taskSceneObjectAsset);
            TaskInventoryItem taskSceneObjectItem
                = new TaskInventoryItem
                    { Name = "tso", AssetID = taskSceneObjectAsset.FullID, ItemID = taskSceneObjectItemId,
                      Type = (int)AssetType.Object, InvType = (int)InventoryType.Object };
            sop1.Inventory.AddInventoryItem(taskSceneObjectItem, true);

            scene.AddSceneObject(sog1);

            Vector3 rezPos = new Vector3(10, 10, 10);
            Quaternion rezRot = new Quaternion(0.5f, 0.5f, 0.5f, 0.5f);
            Vector3 rezVel = new Vector3(2, 2, 2);

            scene.RezObject(sop1, taskSceneObjectItem, rezPos, rezRot, rezVel, 0);

            SceneObjectPart rezzedObjectPart = scene.GetSceneObjectPart("tso");

            Assert.That(rezzedObjectPart, Is.Not.Null);
            Assert.That(rezzedObjectPart.AbsolutePosition, Is.EqualTo(rezPos));
            Assert.That(rezzedObjectPart.RotationOffset, Is.EqualTo(rezRot));

            // Velocity isn't being set, possibly because we have no physics
            //Assert.That(rezzedObjectPart.Velocity, Is.EqualTo(rezVel));
        }

        /// <summary>
        /// Test MoveTaskInventoryItem where the item has no parent folder assigned.
        /// </summary>
        /// <remarks>
        /// This should place it in the most suitable user folder.
        /// </remarks>
        [Test]
        public void TestMoveTaskInventoryItem()
        {
            TestHelper.InMethod();
//            log4net.Config.XmlConfigurator.Configure();
            
            Scene scene = SceneSetupHelpers.SetupScene();
            UserAccount user1 = CreateUser(scene);
            SceneObjectGroup sog1 = CreateSO1(scene, user1.PrincipalID);
            SceneObjectPart sop1 = sog1.RootPart;
            TaskInventoryItem sopItem1 = CreateSOItem1(scene, sop1);
            InventoryFolderBase folder 
                = InventoryArchiveUtils.FindFolderByPath(scene.InventoryService, user1.PrincipalID, "Objects")[0];
            
            // Perform test
            scene.MoveTaskInventoryItem(user1.PrincipalID, folder.ID, sop1, sopItem1.ItemID);
                
            InventoryItemBase ncUserItem
                = InventoryArchiveUtils.FindItemByPath(scene.InventoryService, user1.PrincipalID, "Objects/ncItem");
            Assert.That(ncUserItem, Is.Not.Null, "Objects/ncItem was not found");
        }
        
        /// <summary>
        /// Test MoveTaskInventoryItem where the item has no parent folder assigned.
        /// </summary>
        /// This should place it in the most suitable user folder.
        [Test]
        public void TestMoveTaskInventoryItemNoParent()
        {
            TestHelper.InMethod();
//            log4net.Config.XmlConfigurator.Configure();
            
            Scene scene = SceneSetupHelpers.SetupScene();
            UserAccount user1 = CreateUser(scene);
            SceneObjectGroup sog1 = CreateSO1(scene, user1.PrincipalID);
            SceneObjectPart sop1 = sog1.RootPart;
            TaskInventoryItem sopItem1 = CreateSOItem1(scene, sop1);
            
            // Perform test
            scene.MoveTaskInventoryItem(user1.PrincipalID, UUID.Zero, sop1, sopItem1.ItemID);
                
            InventoryItemBase ncUserItem
                = InventoryArchiveUtils.FindItemByPath(scene.InventoryService, user1.PrincipalID, "Notecards/ncItem");
            Assert.That(ncUserItem, Is.Not.Null, "Notecards/ncItem was not found");
        }
    }
}