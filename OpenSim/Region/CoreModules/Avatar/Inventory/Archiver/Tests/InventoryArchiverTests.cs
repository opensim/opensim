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

            m_scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(m_scene, serialiserModule, m_archiverModule);            
        }
                
        [Test]
        public void TestLoadCoalesecedItem()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();
            
            UserAccountHelpers.CreateUserWithInventory(m_scene, m_uaLL1, "password");
            m_archiverModule.DearchiveInventory(m_uaLL1.FirstName, m_uaLL1.LastName, "/", "password", m_iarStream);            
            
            InventoryItemBase coaItem
                = InventoryArchiveUtils.FindItemByPath(m_scene.InventoryService, m_uaLL1.PrincipalID, m_coaItemName);
            
            Assert.That(coaItem, Is.Not.Null, "Didn't find loaded item 1");            
            
            string assetXml = AssetHelpers.ReadAssetAsString(m_scene.AssetService, coaItem.AssetID);
            
            CoalescedSceneObjects coa;            
            bool readResult = CoalescedSceneObjectsSerializer.TryFromXml(assetXml, out coa);
            
            Assert.That(readResult, Is.True);
            Assert.That(coa.Count, Is.EqualTo(2));
            
            List<SceneObjectGroup> coaObjects = coa.Objects;
            Assert.That(coaObjects[0].UUID, Is.EqualTo(UUID.Parse("00000000-0000-0000-0000-000000000120")));
            Assert.That(coaObjects[0].AbsolutePosition, Is.EqualTo(new Vector3(15, 30, 45)));
            
            Assert.That(coaObjects[1].UUID, Is.EqualTo(UUID.Parse("00000000-0000-0000-0000-000000000140")));
            Assert.That(coaObjects[1].AbsolutePosition, Is.EqualTo(new Vector3(25, 50, 75)));            
        }   
        
        /// <summary>
        /// Test that the IAR has the required files in the right order.
        /// </summary>
        /// <remarks>
        /// At the moment, the only thing that matters is that the control file is the very first one.
        /// </remarks>
        [Test]
        public void TestOrder()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();            
            
            MemoryStream archiveReadStream = new MemoryStream(m_iarStreamBytes);
            TarArchiveReader tar = new TarArchiveReader(archiveReadStream);            
            string filePath;
            TarArchiveReader.TarEntryType tarEntryType;
            
            byte[] data = tar.ReadEntry(out filePath, out tarEntryType);
            Assert.That(filePath, Is.EqualTo(ArchiveConstants.CONTROL_FILE_PATH));
            
            InventoryArchiveReadRequest iarr 
                = new InventoryArchiveReadRequest(null, null, null, (Stream)null, false);
            iarr.LoadControlFile(filePath, data);
            
            Assert.That(iarr.ControlFileLoaded, Is.True);
        }
        
        /// <summary>
        /// Test saving a single inventory item to an IAR
        /// (subject to change since there is no fixed format yet).
        /// </summary>
        [Test]
        public void TestSaveItemToIar()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            // Create user
            string userFirstName = "Jock";
            string userLastName = "Stirrup";
            string userPassword = "troll";
            UUID userId = UUID.Parse("00000000-0000-0000-0000-000000000020");
            UserAccountHelpers.CreateUserWithInventory(m_scene, userFirstName, userLastName, userId, userPassword);
            
            // Create asset
            UUID ownerId = UUID.Parse("00000000-0000-0000-0000-000000000040");
            SceneObjectGroup object1 = SceneHelpers.CreateSceneObject(1, ownerId, "My Little Dog Object", 0x50);         

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
        /// Test saving a single inventory item to an IAR without its asset
        /// </summary>
        [Test]
        public void TestSaveItemToIarNoAssets()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            // Create user
            string userFirstName = "Jock";
            string userLastName = "Stirrup";
            string userPassword = "troll";
            UUID userId = UUID.Parse("00000000-0000-0000-0000-000000000020");
            UserAccountHelpers.CreateUserWithInventory(m_scene, userFirstName, userLastName, userId, userPassword);

            // Create asset
            UUID ownerId = UUID.Parse("00000000-0000-0000-0000-000000000040");
            SceneObjectGroup object1 = SceneHelpers.CreateSceneObject(1, ownerId, "My Little Dog Object", 0x50);

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

            Dictionary<string, Object> options = new Dictionary<string, Object>();
            options.Add("noassets", true);

            // When we're not saving assets, archiving is being done synchronously.
            m_archiverModule.ArchiveInventory(
                Guid.NewGuid(), userFirstName, userLastName, "Objects/" + item1Name, userPassword, archiveWriteStream, options);

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
                else if (filePath.StartsWith(ArchiveConstants.ASSETS_PATH))
                {
                    Assert.Fail("Found asset path in TestSaveItemToIarNoAssets()");
                }
            }

//            Assert.That(gotControlFile, Is.True, "No control file in archive");
            Assert.That(gotObject1File, Is.True, "No item1 file in archive");
//            Assert.That(gotObject2File, Is.True, "No object2 file in archive");

            // TODO: Test presence of more files and contents of files.
        }
        
        /// <summary>
        /// Test case where a creator account exists for the creator UUID embedded in item metadata and serialized
        /// objects.
        /// </summary>        
        [Test]
        public void TestLoadIarCreatorAccountPresent()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UserAccountHelpers.CreateUserWithInventory(m_scene, m_uaLL1, "meowfood");
            
            m_archiverModule.DearchiveInventory(m_uaLL1.FirstName, m_uaLL1.LastName, "/", "meowfood", m_iarStream);            
            InventoryItemBase foundItem1
                = InventoryArchiveUtils.FindItemByPath(m_scene.InventoryService, m_uaLL1.PrincipalID, m_item1Name);

            Assert.That(
                foundItem1.CreatorId, Is.EqualTo(m_uaLL1.PrincipalID.ToString()), 
                "Loaded item non-uuid creator doesn't match original");            
            Assert.That(
                foundItem1.CreatorIdAsUuid, Is.EqualTo(m_uaLL1.PrincipalID), 
                "Loaded item uuid creator doesn't match original");
            Assert.That(foundItem1.Owner, Is.EqualTo(m_uaLL1.PrincipalID),
                "Loaded item owner doesn't match inventory reciever");
            
            AssetBase asset1 = m_scene.AssetService.Get(foundItem1.AssetID.ToString());            
            string xmlData = Utils.BytesToString(asset1.Data);
            SceneObjectGroup sog1 = SceneObjectSerializer.FromOriginalXmlFormat(xmlData);
            
            Assert.That(sog1.RootPart.CreatorID, Is.EqualTo(m_uaLL1.PrincipalID));            
        }
        
//        /// <summary>
//        /// Test loading a V0.1 OpenSim Inventory Archive (subject to change since there is no fixed format yet) where
//        /// an account exists with the same name as the creator, though not the same id.
//        /// </summary>
//        [Test]
//        public void TestLoadIarV0_1SameNameCreator()
//        {
//            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();
//
//            UserAccountHelpers.CreateUserWithInventory(m_scene, m_uaMT, "meowfood");
//            UserAccountHelpers.CreateUserWithInventory(m_scene, m_uaLL2, "hampshire");
//            
//            m_archiverModule.DearchiveInventory(m_uaMT.FirstName, m_uaMT.LastName, "/", "meowfood", m_iarStream);            
//            InventoryItemBase foundItem1
//                = InventoryArchiveUtils.FindItemByPath(m_scene.InventoryService, m_uaMT.PrincipalID, m_item1Name);
//
//            Assert.That(
//                foundItem1.CreatorId, Is.EqualTo(m_uaLL2.PrincipalID.ToString()), 
//                "Loaded item non-uuid creator doesn't match original");            
//            Assert.That(
//                foundItem1.CreatorIdAsUuid, Is.EqualTo(m_uaLL2.PrincipalID), 
//                "Loaded item uuid creator doesn't match original");
//            Assert.That(foundItem1.Owner, Is.EqualTo(m_uaMT.PrincipalID),
//                "Loaded item owner doesn't match inventory reciever");
//            
//            AssetBase asset1 = m_scene.AssetService.Get(foundItem1.AssetID.ToString());            
//            string xmlData = Utils.BytesToString(asset1.Data);
//            SceneObjectGroup sog1 = SceneObjectSerializer.FromOriginalXmlFormat(xmlData);
//            
//            Assert.That(sog1.RootPart.CreatorID, Is.EqualTo(m_uaLL2.PrincipalID));
//        }

        /// <summary>
        /// Test loading a V0.1 OpenSim Inventory Archive (subject to change since there is no fixed format yet) where
        /// the creator or an account with the creator's name does not exist within the system.
        /// </summary>
        [Test]
        public void TestLoadIarV0_1AbsentCreator()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();
            
            UserAccountHelpers.CreateUserWithInventory(m_scene, m_uaMT, "password");
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