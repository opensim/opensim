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
using System.IO;
using System.Reflection;
using System.Threading;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Serialization;
using OpenSim.Framework.Serialization.External;
using OpenSim.Framework.Communications;
using OpenSim.Region.CoreModules.Avatar.Inventory.Archiver;
using OpenSim.Region.CoreModules.Framework.InventoryAccess;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;
using OpenSim.Tests.Common.Setup;

namespace OpenSim.Region.CoreModules.Framework.InventoryAccess.Tests
{
    [TestFixture]
    public class InventoryAccessModuleTests
    {      
        protected TestScene m_scene;
        protected BasicInventoryAccessModule m_iam;
        protected UUID m_userId = UUID.Parse("00000000-0000-0000-0000-000000000020");
        protected TestClient m_tc;
            
        [SetUp]
        public void SetUp()
        {
            m_iam = new BasicInventoryAccessModule();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("InventoryAccessModule", "BasicInventoryAccessModule");
            
            m_scene = SceneSetupHelpers.SetupScene("Inventory");
            SceneSetupHelpers.SetupSceneModules(m_scene, config, m_iam);
            
            // Create user
            string userFirstName = "Jock";
            string userLastName = "Stirrup";
            string userPassword = "troll";
            UserProfileTestUtils.CreateUserWithInventory(m_scene, userFirstName, userLastName, m_userId, userPassword);                        
            
            AgentCircuitData acd = new AgentCircuitData();
            acd.AgentID = m_userId;
            m_tc = new TestClient(acd, m_scene);            
        }
        
        [Test]
        public void TestRezCoalescedObject()
        {
            TestHelper.InMethod();
//            log4net.Config.XmlConfigurator.Configure();
            
            // Create asset
            SceneObjectGroup object1;
            {
                string partName = "Object1";
                UUID ownerId = UUID.Parse("00000000-0000-0000-0000-000000000040");
                PrimitiveBaseShape shape = PrimitiveBaseShape.CreateSphere();
                Vector3 groupPosition = new Vector3(10, 20, 30);
                Quaternion rotationOffset = new Quaternion(20, 30, 40, 50);
                Vector3 offsetPosition = new Vector3(5, 10, 15);

                SceneObjectPart part1
                    = new SceneObjectPart(
                        ownerId, shape, groupPosition, rotationOffset, offsetPosition);
                part1.Name = partName;

                object1 = new SceneObjectGroup(part1);
            }
            
            SceneObjectGroup object2;
            {
                string partName = "Object2";
                UUID ownerId = UUID.Parse("00000000-0000-0000-0000-000000000040");
                PrimitiveBaseShape shape = PrimitiveBaseShape.CreateSphere();
                Vector3 groupPosition = new Vector3(10, 20, 30);
                Quaternion rotationOffset = new Quaternion(20, 30, 40, 50);
                Vector3 offsetPosition = new Vector3(5, 10, 15);

                SceneObjectPart part1
                    = new SceneObjectPart(
                        ownerId, shape, groupPosition, rotationOffset, offsetPosition);
                part1.Name = partName;

                object2 = new SceneObjectGroup(part1);
            }            
            
            CoalescedSceneObjects coa = new CoalescedSceneObjects(m_userId, object1, object2);

            UUID asset1Id = UUID.Parse("00000000-0000-0000-0000-000000000060");
            AssetBase asset1 = AssetHelpers.CreateAsset(asset1Id, coa);
            m_scene.AssetService.Store(asset1);

            // Create item
            UUID item1Id = UUID.Parse("00000000-0000-0000-0000-000000000080");
            string item1Name = "My Little Dog";
            InventoryItemBase item1 = new InventoryItemBase();
            item1.Name = item1Name;
            item1.AssetID = asset1.FullID;
            item1.ID = item1Id;
            InventoryFolderBase objsFolder 
                = InventoryArchiveUtils.FindFolderByPath(m_scene.InventoryService, m_userId, "Objects")[0];
            item1.Folder = objsFolder.ID;
            m_scene.AddInventoryItem(item1);
            
            SceneObjectGroup so 
                = m_iam.RezObject(
                    m_tc, item1Id, Vector3.Zero, Vector3.Zero, UUID.Zero, 1, false, false, false, UUID.Zero, false);            
            
            Assert.That(so, Is.Not.Null);
            
            Assert.That(m_scene.SceneGraph.GetTotalObjectsCount(), Is.EqualTo(2));
          
            SceneObjectPart retrievedObj1Part = m_scene.GetSceneObjectPart(object1.Name);
            Assert.That(retrievedObj1Part, Is.Null);
            
            retrievedObj1Part = m_scene.GetSceneObjectPart(item1.Name);
            Assert.That(retrievedObj1Part, Is.Not.Null);
            Assert.That(retrievedObj1Part.Name, Is.EqualTo(item1.Name));
            
            SceneObjectPart retrievedObj2Part = m_scene.GetSceneObjectPart(object2.Name);
            Assert.That(retrievedObj2Part, Is.Not.Null);            
        }        
        
        [Test]
        public void TestRezObject()
        {
            TestHelper.InMethod();
//            log4net.Config.XmlConfigurator.Configure();
            
            // Create asset
            SceneObjectGroup object1;
            SceneObjectPart part1;
            {
                string partName = "My Little Dog Object";
                UUID ownerId = UUID.Parse("00000000-0000-0000-0000-000000000040");
                PrimitiveBaseShape shape = PrimitiveBaseShape.CreateSphere();
                Vector3 groupPosition = new Vector3(10, 20, 30);
                Quaternion rotationOffset = new Quaternion(20, 30, 40, 50);
                Vector3 offsetPosition = new Vector3(5, 10, 15);

                part1
                    = new SceneObjectPart(
                        ownerId, shape, groupPosition, rotationOffset, offsetPosition);
                part1.Name = partName;

                object1 = new SceneObjectGroup(part1);
            }

            UUID asset1Id = UUID.Parse("00000000-0000-0000-0000-000000000060");
            AssetBase asset1 = AssetHelpers.CreateAsset(asset1Id, object1);
            m_scene.AssetService.Store(asset1);

            // Create item
            UUID item1Id = UUID.Parse("00000000-0000-0000-0000-000000000080");
            string item1Name = "My Little Dog";
            InventoryItemBase item1 = new InventoryItemBase();
            item1.Name = item1Name;
            item1.AssetID = asset1.FullID;
            item1.ID = item1Id;
            InventoryFolderBase objsFolder 
                = InventoryArchiveUtils.FindFolderByPath(m_scene.InventoryService, m_userId, "Objects")[0];
            item1.Folder = objsFolder.ID;
            m_scene.AddInventoryItem(item1);
            
            SceneObjectGroup so 
                = m_iam.RezObject(
                    m_tc, item1Id, Vector3.Zero, Vector3.Zero, UUID.Zero, 1, false, false, false, UUID.Zero, false);            
            
            Assert.That(so, Is.Not.Null);
            
            SceneObjectPart retrievedPart = m_scene.GetSceneObjectPart(so.UUID);
            Assert.That(retrievedPart, Is.Not.Null);
        }
    }
}