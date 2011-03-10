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
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Serialization;
using OpenSim.Framework.Serialization.External;
using OpenSim.Framework.Communications;
using OpenSim.Region.CoreModules.Avatar.Inventory.Archiver;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;
using OpenSim.Tests.Common.Setup;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Archiver.Tests
{
    [TestFixture]
    public class InventoryArchiverTests : InventoryArchiveTestCase
    {   
        protected TestScene m_scene;
        protected InventoryArchiverModule m_archiverModule;
            
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            
            SerialiserModule serialiserModule = new SerialiserModule();
            m_archiverModule = new InventoryArchiverModule();

            m_scene = SceneSetupHelpers.SetupScene("Inventory");
            SceneSetupHelpers.SetupSceneModules(m_scene, serialiserModule, m_archiverModule);            
        }
        
        /// <summary>
        /// Test saving a single inventory item to a V0.1 OpenSim Inventory Archive 
        /// (subject to change since there is no fixed format yet).
        /// </summary>
        [Test]
        public void TestSaveItemToIarV0_1()
        {
            TestHelper.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            // Create user
            string userFirstName = "Jock";
            string userLastName = "Stirrup";
            string userPassword = "troll";
            UUID userId = UUID.Parse("00000000-0000-0000-0000-000000000020");
            UserProfileTestUtils.CreateUserWithInventory(m_scene, userFirstName, userLastName, userId, userPassword);
            
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
                m_scene.AddNewSceneObject(object1, false);
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
                = InventoryArchiveUtils.FindFolderByPath(m_scene.InventoryService, userId, "Objects")[0];
            item1.Folder = objsFolder.ID;
            m_scene.AddInventoryItem(item1);

            MemoryStream archiveWriteStream = new MemoryStream();
            m_archiverModule.OnInventoryArchiveSaved += SaveCompleted;

            mre.Reset();
            m_archiverModule.ArchiveInventory(
                Guid.NewGuid(), userFirstName, userLastName, "Objects/" + item1Name, userPassword, archiveWriteStream);
            mre.WaitOne(60000, false);

            byte[] archive = archiveWriteStream.ToArray();
            MemoryStream archiveReadStream = new MemoryStream(archive);
            TarArchiveReader tar = new TarArchiveReader(archiveReadStream);

            //bool gotControlFile = false;
            bool gotObject1File = false;
            //bool gotObject2File = false;
            string expectedObject1FileName = InventoryArchiveWriteRequest.CreateArchiveItemName(item1);
            string expectedObject1FilePath = string.Format(
                "{0}{1}",
                ArchiveConstants.INVENTORY_PATH,
                expectedObject1FileName);

            string filePath;
            TarArchiveReader.TarEntryType tarEntryType;

//            Console.WriteLine("Reading archive");
            
            while (tar.ReadEntry(out filePath, out tarEntryType) != null)
            {
                Console.WriteLine("Got {0}", filePath);

//                if (ArchiveConstants.CONTROL_FILE_PATH == filePath)
//                {
//                    gotControlFile = true;
//                }
                
                if (filePath.StartsWith(ArchiveConstants.INVENTORY_PATH) && filePath.EndsWith(".xml"))
                {
//                    string fileName = filePath.Remove(0, "Objects/".Length);
//
//                    if (fileName.StartsWith(part1.Name))
//                    {
                        Assert.That(expectedObject1FilePath, Is.EqualTo(filePath));
                        gotObject1File = true;
//                    }
//                    else if (fileName.StartsWith(part2.Name))
//                    {
//                        Assert.That(fileName, Is.EqualTo(expectedObject2FileName));
//                        gotObject2File = true;
//                    }
                }
            }

//            Assert.That(gotControlFile, Is.True, "No control file in archive");
            Assert.That(gotObject1File, Is.True, "No item1 file in archive");
//            Assert.That(gotObject2File, Is.True, "No object2 file in archive");

            // TODO: Test presence of more files and contents of files.
        }
        
        /// <summary>
        /// Test loading a V0.1 OpenSim Inventory Archive (subject to change since there is no fixed format yet) where
        /// an account exists with the same name as the creator, though not the same id.
        /// </summary>
        [Test]
        public void TestLoadIarV0_1SameNameCreator()
        {
            TestHelper.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UserProfileTestUtils.CreateUserWithInventory(m_scene, m_uaMT, "meowfood");
            UserProfileTestUtils.CreateUserWithInventory(m_scene, m_uaLL2, "hampshire");
            
            m_archiverModule.DearchiveInventory(m_uaMT.FirstName, m_uaMT.LastName, "/", "meowfood", m_iarStream);            
            InventoryItemBase foundItem1
                = InventoryArchiveUtils.FindItemByPath(m_scene.InventoryService, m_uaMT.PrincipalID, m_item1Name);

            Assert.That(
                foundItem1.CreatorId, Is.EqualTo(m_uaLL2.PrincipalID.ToString()), 
                "Loaded item non-uuid creator doesn't match original");            
            Assert.That(
                foundItem1.CreatorIdAsUuid, Is.EqualTo(m_uaLL2.PrincipalID), 
                "Loaded item uuid creator doesn't match original");
            Assert.That(foundItem1.Owner, Is.EqualTo(m_uaMT.PrincipalID),
                "Loaded item owner doesn't match inventory reciever");
            
            AssetBase asset1 = m_scene.AssetService.Get(foundItem1.AssetID.ToString());            
            string xmlData = Utils.BytesToString(asset1.Data);
            SceneObjectGroup sog1 = SceneObjectSerializer.FromOriginalXmlFormat(xmlData);
            
            Assert.That(sog1.RootPart.CreatorID, Is.EqualTo(m_uaLL2.PrincipalID));
        }

        /// <summary>
        /// Test loading a V0.1 OpenSim Inventory Archive (subject to change since there is no fixed format yet) where
        /// the creator or an account with the creator's name does not exist within the system.
        /// </summary>
        [Test]
        public void TestLoadIarV0_1AbsentCreator()
        {
            TestHelper.InMethod();
//            log4net.Config.XmlConfigurator.Configure();
            
            UserProfileTestUtils.CreateUserWithInventory(m_scene, m_uaMT, "password");
            m_archiverModule.DearchiveInventory(m_uaMT.FirstName, m_uaMT.LastName, "/", "password", m_iarStream);

            InventoryItemBase foundItem1
                = InventoryArchiveUtils.FindItemByPath(m_scene.InventoryService, m_uaMT.PrincipalID, m_item1Name);
            
            Assert.That(foundItem1, Is.Not.Null, "Didn't find loaded item 1");
            Assert.That(
                foundItem1.CreatorId, Is.EqualTo(m_uaMT.PrincipalID.ToString()), 
                "Loaded item non-uuid creator doesn't match that of the loading user");
            Assert.That(
                foundItem1.CreatorIdAsUuid, Is.EqualTo(m_uaMT.PrincipalID), 
                "Loaded item uuid creator doesn't match that of the loading user");
            
            AssetBase asset1 = m_scene.AssetService.Get(foundItem1.AssetID.ToString());            
            string xmlData = Utils.BytesToString(asset1.Data);
            SceneObjectGroup sog1 = SceneObjectSerializer.FromOriginalXmlFormat(xmlData);
            
            Assert.That(sog1.RootPart.CreatorID, Is.EqualTo(m_uaMT.PrincipalID));            
        }
    }
}