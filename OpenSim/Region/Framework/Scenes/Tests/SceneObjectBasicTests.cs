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
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;
using OpenSim.Tests.Common.Setup;

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
        [Test, LongRunning]
        public void TestAddSceneObject()
        {
            TestHelper.InMethod();

            Scene scene = SceneSetupHelpers.SetupScene();
            SceneObjectPart part = SceneSetupHelpers.AddSceneObject(scene);
            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(part.LocalId);
            
            //m_log.Debug("retrievedPart : {0}", retrievedPart);
            // If the parts have the same UUID then we will consider them as one and the same
            Assert.That(retrievedPart.UUID, Is.EqualTo(part.UUID));
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
            
            UUID agentId = UUID.Parse("00000000-0000-0000-0000-000000000001");
            
            TestScene scene = SceneSetupHelpers.SetupScene();
            
            // Turn off the timer on the async sog deleter - we'll crank it by hand for this test.
            AsyncSceneObjectGroupDeleter sogd = scene.SceneObjectGroupDeleter;
            sogd.Enabled = false;
                
            SceneObjectPart part = SceneSetupHelpers.AddSceneObject(scene);
            
            IClientAPI client = SceneSetupHelpers.AddRootAgent(scene, agentId);
            scene.DeRezObject(client, part.LocalId, UUID.Zero, DeRezAction.Delete, UUID.Zero);
            
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
    }
}
