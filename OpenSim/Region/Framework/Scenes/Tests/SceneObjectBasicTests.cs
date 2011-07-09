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
using System.Reflection;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    /// <summary>
    /// Basic scene object tests (create, read and delete but not update).
    /// </summary>
    [TestFixture]
    public class SceneObjectBasicTests
    {
        /// <summary>
        /// Test adding an object to a scene.
        /// </summary>
        [Test]
        public void TestAddSceneObject()
        {
            TestHelper.InMethod();

            Scene scene = SceneSetupHelpers.SetupScene();

            string objName = "obj1";
            UUID objUuid = new UUID("00000000-0000-0000-0000-000000000001");

            SceneObjectPart part
                = new SceneObjectPart(UUID.Zero, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero) 
                    { Name = objName, UUID = objUuid };

            Assert.That(scene.AddNewSceneObject(new SceneObjectGroup(part), false), Is.True);
            
            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(objUuid);
            
            //m_log.Debug("retrievedPart : {0}", retrievedPart);
            // If the parts have the same UUID then we will consider them as one and the same
            Assert.That(retrievedPart.Name, Is.EqualTo(objName));
            Assert.That(retrievedPart.UUID, Is.EqualTo(objUuid));
        }

        [Test]
        /// <summary>
        /// It shouldn't be possible to add a scene object if one with that uuid already exists in the scene.
        /// </summary>
        public void TestAddExistingSceneObjectUuid()
        {
            TestHelper.InMethod();

            Scene scene = SceneSetupHelpers.SetupScene();

            string obj1Name = "Alfred";
            string obj2Name = "Betty";
            UUID objUuid = new UUID("00000000-0000-0000-0000-000000000001");

            SceneObjectPart part1
                = new SceneObjectPart(UUID.Zero, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero) 
                    { Name = obj1Name, UUID = objUuid };

            Assert.That(scene.AddNewSceneObject(new SceneObjectGroup(part1), false), Is.True);

            SceneObjectPart part2
                = new SceneObjectPart(UUID.Zero, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero) 
                    { Name = obj2Name, UUID = objUuid };

            Assert.That(scene.AddNewSceneObject(new SceneObjectGroup(part2), false), Is.False);
            
            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(objUuid);
            
            //m_log.Debug("retrievedPart : {0}", retrievedPart);
            // If the parts have the same UUID then we will consider them as one and the same
            Assert.That(retrievedPart.Name, Is.EqualTo(obj1Name));
            Assert.That(retrievedPart.UUID, Is.EqualTo(objUuid));
        }
        
        /// <summary>
        /// Test deleting an object from a scene.
        /// </summary>
        [Test]
        public void TestDeleteSceneObject()
        {
            TestHelper.InMethod();
            
            TestScene scene = SceneSetupHelpers.SetupScene();
            SceneObjectPart part = SceneSetupHelpers.AddSceneObject(scene);
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
            TestHelper.InMethod();
            //log4net.Config.XmlConfigurator.Configure();

            UUID agentId = UUID.Parse("00000000-0000-0000-0000-000000000001");

            TestScene scene = SceneSetupHelpers.SetupScene();

            // Turn off the timer on the async sog deleter - we'll crank it by hand for this test.
            AsyncSceneObjectGroupDeleter sogd = scene.SceneObjectGroupDeleter;
            sogd.Enabled = false;

            SceneObjectPart part = SceneSetupHelpers.AddSceneObject(scene);

            IClientAPI client = SceneSetupHelpers.AddClient(scene, agentId);
            scene.DeRezObjects(client, new System.Collections.Generic.List<uint>() { part.LocalId }, UUID.Zero, DeRezAction.Delete, UUID.Zero);

            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(part.LocalId);

            Assert.That(retrievedPart, Is.Not.Null);

            sogd.InventoryDeQueueAndDelete();

            SceneObjectPart retrievedPart2 = scene.GetSceneObjectPart(part.LocalId);
            Assert.That(retrievedPart2, Is.Null);
        }
 
        /// <summary>
        /// Test deleting an object asynchronously to user inventory.
        /// </summary>
        //[Test]
        //public void TestDeleteSceneObjectAsyncToUserInventory()
        //{
        //    TestHelper.InMethod();
        //    //log4net.Config.XmlConfigurator.Configure();
            
        //    UUID agentId = UUID.Parse("00000000-0000-0000-0000-000000000001");
        //    string myObjectName = "Fred";
            
        //    TestScene scene = SceneSetupHelpers.SetupScene();
        //    SceneObjectPart part = SceneSetupHelpers.AddSceneObject(scene, myObjectName);
            
        //    Assert.That(
        //        scene.CommsManager.UserAdminService.AddUser(
        //            "Bob", "Hoskins", "test", "test@test.com", 1000, 1000, agentId),
        //        Is.EqualTo(agentId));
            
        //    IClientAPI client = SceneSetupHelpers.AddRootAgent(scene, agentId);
                                                
        //    CachedUserInfo userInfo = scene.CommsManager.UserProfileCacheService.GetUserDetails(agentId);
        //    Assert.That(userInfo, Is.Not.Null);
        //    Assert.That(userInfo.RootFolder, Is.Not.Null);
            
        //    SceneSetupHelpers.DeleteSceneObjectAsync(scene, part, DeRezAction.Take, userInfo.RootFolder.ID, client);
            
        //    // Check that we now have the taken part in our inventory
        //    Assert.That(myObjectName, Is.EqualTo(userInfo.RootFolder.FindItemByPath(myObjectName).Name));
            
        //    // Check that the taken part has actually disappeared
        //    SceneObjectPart retrievedPart = scene.GetSceneObjectPart(part.LocalId);
        //    Assert.That(retrievedPart, Is.Null);
        //}
        
        /// <summary>
        /// Changing a scene object uuid changes the root part uuid.  This is a valid operation if the object is not
        /// in a scene and is useful if one wants to supply a UUID directly rather than use the one generated by 
        /// OpenSim.
        /// </summary>
        [Test]
        public void TestChangeSceneObjectUuid()
        {
            string rootPartName = "rootpart";
            UUID rootPartUuid = new UUID("00000000-0000-0000-0000-000000000001");
            string childPartName = "childPart";
            UUID childPartUuid = new UUID("00000000-0000-0000-0001-000000000000");
            
            SceneObjectPart rootPart
                = new SceneObjectPart(UUID.Zero, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero) 
                    { Name = rootPartName, UUID = rootPartUuid };
            SceneObjectPart linkPart
                = new SceneObjectPart(UUID.Zero, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero) 
                    { Name = childPartName, UUID = childPartUuid };

            SceneObjectGroup sog = new SceneObjectGroup(rootPart);
            sog.AddPart(linkPart);
            
            Assert.That(sog.UUID, Is.EqualTo(rootPartUuid));
            Assert.That(sog.RootPart.UUID, Is.EqualTo(rootPartUuid));
            Assert.That(sog.Parts.Length, Is.EqualTo(2));
            
            UUID newRootPartUuid = new UUID("00000000-0000-0000-0000-000000000002");
            sog.UUID = newRootPartUuid;
                        
            Assert.That(sog.UUID, Is.EqualTo(newRootPartUuid));
            Assert.That(sog.RootPart.UUID, Is.EqualTo(newRootPartUuid));
            Assert.That(sog.Parts.Length, Is.EqualTo(2));
        }
    }
}
