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
 *     * Neither the name of the OpenSim Project nor the
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
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.Environment.Scenes.Tests
{
    /// <summary>
    /// Scene object tests
    /// </summary>
    [TestFixture]
    public class SceneObjectTests
    {         
        /// <summary>
        /// Test adding an object to a scene.
        /// </summary>
        [Test]
        public void TestAddSceneObject()
        {              
            Scene scene = SceneTestUtils.SetupScene();
            SceneObjectPart part = SceneTestUtils.AddSceneObject(scene);
            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(part.LocalId);
            
            //System.Console.WriteLine("retrievedPart : {0}", retrievedPart);
            // If the parts have the same UUID then we will consider them as one and the same
            Assert.That(retrievedPart.UUID, Is.EqualTo(part.UUID));         
        }
        
        /// <summary>
        /// Test deleting an object from a scene.
        /// </summary>
        [Test]
        public void TestDeleteSceneObject()
        {
            TestScene scene = SceneTestUtils.SetupScene();         
            SceneObjectPart part = SceneTestUtils.AddSceneObject(scene);
            scene.DeleteSceneObject(part.ParentGroup, false);
            
            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(part.LocalId);            
            Assert.That(retrievedPart, Is.Null);
        }
        
        /// <summary>
        /// Test deleting an object asynchronously
        /// </summary>
        [Test]
        public void TestDeleteSceneObjectAsync()
        {
            UUID agentId = UUID.Parse("00000000-0000-0000-0000-000000000001");
            
            TestScene scene = SceneTestUtils.SetupScene();
            
            // Turn off the timer on the async sog deleter - we'll crank it by hand for this test.
            AsyncSceneObjectGroupDeleter sogd = scene.SceneObjectGroupDeleter;
            sogd.Enabled = false;
                
            SceneObjectPart part = SceneTestUtils.AddSceneObject(scene);
            
            IClientAPI client = SceneTestUtils.AddRootAgent(scene, agentId);
            scene.DeRezObject(client, part.LocalId, UUID.Zero, DeRezAction.Delete, UUID.Zero);
            
            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(part.LocalId);
            Assert.That(retrievedPart, Is.Not.Null);
            
            sogd.InventoryDeQueueAndDelete();
            SceneObjectPart retrievedPart2 = scene.GetSceneObjectPart(part.LocalId);
            Assert.That(retrievedPart2, Is.Null);            
        }

        [Test]
        public void TestLinkDelink2SceneObjects()
        {
            bool debugtest = false; 

            Scene scene = SceneTestUtils.SetupScene();
            SceneObjectPart part1 = SceneTestUtils.AddSceneObject(scene);
            SceneObjectGroup grp1 = part1.ParentGroup;
            SceneObjectPart part2 = SceneTestUtils.AddSceneObject(scene);
            SceneObjectGroup grp2 = part2.ParentGroup;


            grp1.AbsolutePosition = new Vector3(10, 10, 10);
            grp2.AbsolutePosition = Vector3.Zero;
            
            // <90,0,0>
            grp1.Rotation = (Quaternion.CreateFromEulers(90 * Utils.DEG_TO_RAD, 0, 0));

            // <180,0,0>
            grp2.UpdateGroupRotation(Quaternion.CreateFromEulers(180 * Utils.DEG_TO_RAD, 0, 0));
            
            // Required for linking
            grp1.RootPart.UpdateFlag = 0;
            grp2.RootPart.UpdateFlag = 0;

            // Link grp2 to grp1.   part2 becomes child prim to grp1. grp2 is eliminated.
            grp1.LinkToGroup(grp2);

            Assert.That(grp1.Children.Count == 2);

            if (debugtest)
            {
                System.Console.WriteLine("parts: {0}", grp1.Children.Count);
                System.Console.WriteLine("Group1: Pos:{0}, Rot:{1}", grp1.AbsolutePosition, grp1.Rotation);
                System.Console.WriteLine("Group1: Prim1: OffsetPosition:{0}, OffsetRotation:{1}", part1.OffsetPosition, part1.RotationOffset);
                System.Console.WriteLine("Group1: Prim2: OffsetPosition:{0}, OffsetRotation:{1}", part2.OffsetPosition, part2.RotationOffset);
            }

            // root part should have no offset position or rotation
            Assert.That(part1.OffsetPosition == Vector3.Zero && part1.RotationOffset == Quaternion.Identity);

            // offset position should be root part position - part2.absolute position.
            Assert.That(part2.OffsetPosition == new Vector3(-10, -10, -10));

            float roll = 0;
            float pitch = 0;
            float yaw = 0;

            // There's a euler anomoly at 180, 0, 0 so expect 180 to turn into -180.
            part1.RotationOffset.GetEulerAngles(out roll, out pitch, out yaw);
            Vector3 rotEuler1 = new Vector3(roll * Utils.RAD_TO_DEG, pitch * Utils.RAD_TO_DEG, yaw * Utils.RAD_TO_DEG);
            
            if (debugtest)
                System.Console.WriteLine(rotEuler1);

            part2.RotationOffset.GetEulerAngles(out roll, out pitch, out yaw);
            Vector3 rotEuler2 = new Vector3(roll * Utils.RAD_TO_DEG, pitch * Utils.RAD_TO_DEG, yaw * Utils.RAD_TO_DEG);
             
            if (debugtest)
                System.Console.WriteLine(rotEuler2);

            Assert.That(rotEuler2.ApproxEquals(new Vector3(-180, 0, 0), 0.001f) || rotEuler2.ApproxEquals(new Vector3(180, 0, 0), 0.001f));

            // Delink part 2
            grp1.DelinkFromGroup(part2.LocalId);

            if (debugtest)
                System.Console.WriteLine("Group2: Prim2: OffsetPosition:{0}, OffsetRotation:{1}", part2.AbsolutePosition, part2.RotationOffset);

            Assert.That(part2.AbsolutePosition == Vector3.Zero);
            
        }
 
        /// <summary>
        /// Test deleting an object asynchronously to user inventory.
        /// </summary>
        [Test]
        public void TestDeleteSceneObjectAsyncToUserInventory()
        {
            //log4net.Config.XmlConfigurator.Configure();                  
            
            UUID agentId = UUID.Parse("00000000-0000-0000-0000-000000000001");
            string myObjectName = "Fred";
            
            TestScene scene = SceneTestUtils.SetupScene();                
            SceneObjectPart part = SceneTestUtils.AddSceneObject(scene, myObjectName);
            
            ((LocalUserServices)scene.CommsManager.UserService).AddPlugin(new TestUserDataPlugin());
            ((LocalInventoryService)scene.CommsManager.InventoryService).AddPlugin(new TestInventoryDataPlugin());
            
            Assert.That(
                scene.CommsManager.UserAdminService.AddUser(
                    "Bob", "Hoskins", "test", "test@test.com", 1000, 1000, agentId),
                Is.EqualTo(agentId));  
            
            IClientAPI client = SceneTestUtils.AddRootAgent(scene, agentId);
                                                
            CachedUserInfo userInfo = scene.CommsManager.UserProfileCacheService.GetUserDetails(agentId);
            Assert.That(userInfo, Is.Not.Null);
            Assert.That(userInfo.RootFolder, Is.Not.Null);
            
            SceneTestUtils.DeleteSceneObjectAsync(scene, part, DeRezAction.Take, userInfo.RootFolder.ID, client);
            
            // Check that we now have the taken part in our inventory
            Assert.That(myObjectName, Is.EqualTo(userInfo.RootFolder.FindItemByPath(myObjectName).Name));
            
            // Check that the taken part has actually disappeared
            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(part.LocalId);
            Assert.That(retrievedPart, Is.Null);                             
        }
    }
}