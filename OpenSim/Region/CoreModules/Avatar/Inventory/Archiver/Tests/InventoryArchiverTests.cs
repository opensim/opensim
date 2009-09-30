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
using System.Text;
using System.Threading;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Serialization;
using OpenSim.Framework.Serialization.External;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Osp;
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
    public class InventoryArchiverTests
    {
        protected ManualResetEvent mre = new ManualResetEvent(false);
        
        private void InventoryReceived(UUID userId)
        {
            lock (this)
            {
                Monitor.PulseAll(this);
            }
        }
        
        private void SaveCompleted(
            Guid id, bool succeeded, CachedUserInfo userInfo, string invPath, Stream saveStream, 
            Exception reportedException)
        {
            mre.Set();
        }

        /// <summary>
        /// Test saving a V0.1 OpenSim Inventory Archive (subject to change since there is no fixed format yet).
        /// </summary>
        // Commenting for now! The mock inventory service needs more beef, at least for
        // GetFolderForType
        [Test]
        public void TestSaveIarV0_1()
        {
            TestHelper.InMethod();
            //log4net.Config.XmlConfigurator.Configure();

            InventoryArchiverModule archiverModule = new InventoryArchiverModule(true);

            Scene scene = SceneSetupHelpers.SetupScene("Inventory");
            SceneSetupHelpers.SetupSceneModules(scene, archiverModule);
            CommunicationsManager cm = scene.CommsManager;

            // Create user
            string userFirstName = "Jock";
            string userLastName = "Stirrup";
            UUID userId = UUID.Parse("00000000-0000-0000-0000-000000000020");

            lock (this)
            {
                UserProfileTestUtils.CreateUserWithInventory(
                    cm, userFirstName, userLastName, userId, InventoryReceived);
                Monitor.Wait(this, 60000);
            }
            
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
                scene.AddNewSceneObject(object1, false);
            }

            UUID asset1Id = UUID.Parse("00000000-0000-0000-0000-000000000060");
            AssetBase asset1 = new AssetBase();
            asset1.FullID = asset1Id;
            asset1.Data = Encoding.ASCII.GetBytes(SceneObjectSerializer.ToXml2Format(object1));
            scene.AssetService.Store(asset1);

            // Create item
            UUID item1Id = UUID.Parse("00000000-0000-0000-0000-000000000080");
            InventoryItemBase item1 = new InventoryItemBase();
            item1.Name = "My Little Dog";
            item1.AssetID = asset1.FullID;
            item1.ID = item1Id;
            InventoryFolderBase objsFolder 
                = InventoryArchiveUtils.FindFolderByPath(scene.InventoryService, userId, "Objects");
            item1.Folder = objsFolder.ID;
            scene.AddInventoryItem(userId, item1);

            MemoryStream archiveWriteStream = new MemoryStream();
            archiverModule.OnInventoryArchiveSaved += SaveCompleted;

            mre.Reset();
            archiverModule.ArchiveInventory(
                Guid.NewGuid(), userFirstName, userLastName, "Objects", "troll", archiveWriteStream);
            mre.WaitOne(60000, false);

            byte[] archive = archiveWriteStream.ToArray();
            MemoryStream archiveReadStream = new MemoryStream(archive);
            TarArchiveReader tar = new TarArchiveReader(archiveReadStream);

            //bool gotControlFile = false;
            bool gotObject1File = false;
            //bool gotObject2File = false;
            string expectedObject1FileName = InventoryArchiveWriteRequest.CreateArchiveItemName(item1);
            string expectedObject1FilePath = string.Format(
                "{0}{1}{2}",
                ArchiveConstants.INVENTORY_PATH,
                InventoryArchiveWriteRequest.CreateArchiveFolderName(objsFolder),
                expectedObject1FileName);

            string filePath;
            TarArchiveReader.TarEntryType tarEntryType;

            Console.WriteLine("Reading archive");
            
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
        /// an account exists with the creator name.
        /// </summary>
        ///
        /// This test also does some deeper probing of loading into nested inventory structures
        [Test]
        public void TestLoadIarV0_1ExistingUsers()
        {
            TestHelper.InMethod();
            
            //log4net.Config.XmlConfigurator.Configure();
            
            string userFirstName = "Mr";
            string userLastName = "Tiddles";
            UUID userUuid = UUID.Parse("00000000-0000-0000-0000-000000000555");
            string userItemCreatorFirstName = "Lord";
            string userItemCreatorLastName = "Lucan";
            UUID userItemCreatorUuid = UUID.Parse("00000000-0000-0000-0000-000000000666");
            
            string itemName = "b.lsl";
            string archiveItemName = InventoryArchiveWriteRequest.CreateArchiveItemName(itemName, UUID.Random());

            MemoryStream archiveWriteStream = new MemoryStream();
            TarArchiveWriter tar = new TarArchiveWriter(archiveWriteStream);

            InventoryItemBase item1 = new InventoryItemBase();
            item1.Name = itemName;
            item1.AssetID = UUID.Random();
            item1.GroupID = UUID.Random();
            item1.CreatorId = OspResolver.MakeOspa(userItemCreatorFirstName, userItemCreatorLastName);
            //item1.CreatorId = userUuid.ToString();
            //item1.CreatorId = "00000000-0000-0000-0000-000000000444";
            item1.Owner = UUID.Zero;
            
            string item1FileName 
                = string.Format("{0}{1}", ArchiveConstants.INVENTORY_PATH, archiveItemName);
            tar.WriteFile(item1FileName, UserInventoryItemSerializer.Serialize(item1));
            tar.Close();

            MemoryStream archiveReadStream = new MemoryStream(archiveWriteStream.ToArray());
            SerialiserModule serialiserModule = new SerialiserModule();
            InventoryArchiverModule archiverModule = new InventoryArchiverModule(true);
            
            // Annoyingly, we have to set up a scene even though inventory loading has nothing to do with a scene
            Scene scene = SceneSetupHelpers.SetupScene("inventory");
            IUserAdminService userAdminService = scene.CommsManager.UserAdminService;
            
            SceneSetupHelpers.SetupSceneModules(scene, serialiserModule, archiverModule);
            userAdminService.AddUser(
                userFirstName, userLastName, "meowfood", String.Empty, 1000, 1000, userUuid);
            userAdminService.AddUser(
                userItemCreatorFirstName, userItemCreatorLastName, "hampshire", 
                String.Empty, 1000, 1000, userItemCreatorUuid);
            
            archiverModule.DearchiveInventory(userFirstName, userLastName, "/", "meowfood", archiveReadStream);

            CachedUserInfo userInfo 
                = scene.CommsManager.UserProfileCacheService.GetUserDetails(userFirstName, userLastName);

            InventoryItemBase foundItem1
                = InventoryArchiveUtils.FindItemByPath(scene.InventoryService, userInfo.UserProfile.ID, itemName);
            
            Assert.That(foundItem1, Is.Not.Null, "Didn't find loaded item 1");
            Assert.That(
                foundItem1.CreatorId, Is.EqualTo(item1.CreatorId), 
                "Loaded item non-uuid creator doesn't match original");
            Assert.That(
                foundItem1.CreatorIdAsUuid, Is.EqualTo(userItemCreatorUuid), 
                "Loaded item uuid creator doesn't match original");
            Assert.That(foundItem1.Owner, Is.EqualTo(userUuid),
                "Loaded item owner doesn't match inventory reciever");

            // Now try loading to a root child folder
            UserInventoryTestUtils.CreateInventoryFolder(scene.InventoryService, userInfo.UserProfile.ID, "xA");
            archiveReadStream = new MemoryStream(archiveReadStream.ToArray());
            archiverModule.DearchiveInventory(userFirstName, userLastName, "xA", "meowfood", archiveReadStream);

            InventoryItemBase foundItem2
                = InventoryArchiveUtils.FindItemByPath(scene.InventoryService, userInfo.UserProfile.ID, "xA/" + itemName);
            Assert.That(foundItem2, Is.Not.Null, "Didn't find loaded item 2");

            // Now try loading to a more deeply nested folder
            UserInventoryTestUtils.CreateInventoryFolder(scene.InventoryService, userInfo.UserProfile.ID, "xB/xC");
            archiveReadStream = new MemoryStream(archiveReadStream.ToArray());
            archiverModule.DearchiveInventory(userFirstName, userLastName, "xB/xC", "meowfood", archiveReadStream);

            InventoryItemBase foundItem3
                = InventoryArchiveUtils.FindItemByPath(scene.InventoryService, userInfo.UserProfile.ID, "xB/xC/" + itemName);
            Assert.That(foundItem3, Is.Not.Null, "Didn't find loaded item 3");
        }

        /// <summary>
        /// Test loading a V0.1 OpenSim Inventory Archive (subject to change since there is no fixed format yet) where
        /// embedded creators do not exist in the system
        /// </summary>
        ///
        /// This may possibly one day get overtaken by the as yet incomplete temporary profiles feature 
        /// (as tested in the a later commented out test)
        [Test]
        public void TestLoadIarV0_1AbsentUsers()
        {
            TestHelper.InMethod();
            
            log4net.Config.XmlConfigurator.Configure();
            
            string userFirstName = "Charlie";
            string userLastName = "Chan";
            UUID userUuid = UUID.Parse("00000000-0000-0000-0000-000000000999");
            string userItemCreatorFirstName = "Bat";
            string userItemCreatorLastName = "Man";
            //UUID userItemCreatorUuid = UUID.Parse("00000000-0000-0000-0000-000000008888");
            
            string itemName = "b.lsl";
            string archiveItemName = InventoryArchiveWriteRequest.CreateArchiveItemName(itemName, UUID.Random());

            MemoryStream archiveWriteStream = new MemoryStream();
            TarArchiveWriter tar = new TarArchiveWriter(archiveWriteStream);

            InventoryItemBase item1 = new InventoryItemBase();
            item1.Name = itemName;
            item1.AssetID = UUID.Random();
            item1.GroupID = UUID.Random();
            item1.CreatorId = OspResolver.MakeOspa(userItemCreatorFirstName, userItemCreatorLastName);
            //item1.CreatorId = userUuid.ToString();
            //item1.CreatorId = "00000000-0000-0000-0000-000000000444";
            item1.Owner = UUID.Zero;
            
            string item1FileName 
                = string.Format("{0}{1}", ArchiveConstants.INVENTORY_PATH, archiveItemName);
            tar.WriteFile(item1FileName, UserInventoryItemSerializer.Serialize(item1));
            tar.Close();

            MemoryStream archiveReadStream = new MemoryStream(archiveWriteStream.ToArray());
            SerialiserModule serialiserModule = new SerialiserModule();
            InventoryArchiverModule archiverModule = new InventoryArchiverModule(true);
            
            // Annoyingly, we have to set up a scene even though inventory loading has nothing to do with a scene
            Scene scene = SceneSetupHelpers.SetupScene("inventory");
            IUserAdminService userAdminService = scene.CommsManager.UserAdminService;
            
            SceneSetupHelpers.SetupSceneModules(scene, serialiserModule, archiverModule);
            userAdminService.AddUser(
                userFirstName, userLastName, "meowfood", String.Empty, 1000, 1000, userUuid);
            
            archiverModule.DearchiveInventory(userFirstName, userLastName, "/", "meowfood", archiveReadStream);

            CachedUserInfo userInfo 
                = scene.CommsManager.UserProfileCacheService.GetUserDetails(userFirstName, userLastName);

            InventoryItemBase foundItem1
                = InventoryArchiveUtils.FindItemByPath(scene.InventoryService, userInfo.UserProfile.ID, itemName);
            
            Assert.That(foundItem1, Is.Not.Null, "Didn't find loaded item 1");
//            Assert.That(
//                foundItem1.CreatorId, Is.EqualTo(userUuid), 
//                "Loaded item non-uuid creator doesn't match that of the loading user");
            Assert.That(
                foundItem1.CreatorIdAsUuid, Is.EqualTo(userUuid), 
                "Loaded item uuid creator doesn't match that of the loading user");
        }

        /// <summary>
        /// Test loading a V0.1 OpenSim Inventory Archive (subject to change since there is no fixed format yet) where
        /// no account exists with the creator name
        /// </summary>
        /// Disabled since temporary profiles have not yet been implemented.
        //[Test]
        public void TestLoadIarV0_1TempProfiles()
        {
            TestHelper.InMethod();
            
            log4net.Config.XmlConfigurator.Configure();
            
            string userFirstName = "Dennis";
            string userLastName = "Menace";
            UUID userUuid = UUID.Parse("00000000-0000-0000-0000-000000000aaa");
            string user2FirstName = "Walter";
            string user2LastName = "Mitty";
            
            string itemName = "b.lsl";
            string archiveItemName = InventoryArchiveWriteRequest.CreateArchiveItemName(itemName, UUID.Random());

            MemoryStream archiveWriteStream = new MemoryStream();
            TarArchiveWriter tar = new TarArchiveWriter(archiveWriteStream);

            InventoryItemBase item1 = new InventoryItemBase();
            item1.Name = itemName;
            item1.AssetID = UUID.Random();
            item1.GroupID = UUID.Random();
            item1.CreatorId = OspResolver.MakeOspa(user2FirstName, user2LastName);
            item1.Owner = UUID.Zero;
            
            string item1FileName 
                = string.Format("{0}{1}", ArchiveConstants.INVENTORY_PATH, archiveItemName);
            tar.WriteFile(item1FileName, UserInventoryItemSerializer.Serialize(item1));
            tar.Close();

            MemoryStream archiveReadStream = new MemoryStream(archiveWriteStream.ToArray());
            SerialiserModule serialiserModule = new SerialiserModule();
            InventoryArchiverModule archiverModule = new InventoryArchiverModule(true);
            
            // Annoyingly, we have to set up a scene even though inventory loading has nothing to do with a scene
            Scene scene = SceneSetupHelpers.SetupScene();
            IUserAdminService userAdminService = scene.CommsManager.UserAdminService;
            
            SceneSetupHelpers.SetupSceneModules(scene, serialiserModule, archiverModule);
            userAdminService.AddUser(
                userFirstName, userLastName, "meowfood", String.Empty, 1000, 1000, userUuid);
            
            archiverModule.DearchiveInventory(userFirstName, userLastName, "/", "troll", archiveReadStream);
            
            // Check that a suitable temporary user profile has been created.
            UserProfileData user2Profile 
                = scene.CommsManager.UserService.GetUserProfile(
                    OspResolver.HashName(user2FirstName + " " + user2LastName));
            Assert.That(user2Profile, Is.Not.Null);
            Assert.That(user2Profile.FirstName == user2FirstName);
            Assert.That(user2Profile.SurName == user2LastName);
            
            CachedUserInfo userInfo 
                = scene.CommsManager.UserProfileCacheService.GetUserDetails(userFirstName, userLastName);
            userInfo.OnInventoryReceived += InventoryReceived;

            lock (this)
            {
                userInfo.FetchInventory();
                Monitor.Wait(this, 60000);
            }
            
            InventoryItemBase foundItem = userInfo.RootFolder.FindItemByPath(itemName);
            
            Assert.That(foundItem.CreatorId, Is.EqualTo(item1.CreatorId));
            Assert.That(
                foundItem.CreatorIdAsUuid, Is.EqualTo(OspResolver.HashName(user2FirstName + " " + user2LastName)));
            Assert.That(foundItem.Owner, Is.EqualTo(userUuid));
            
            Console.WriteLine("### Successfully completed {0} ###", MethodBase.GetCurrentMethod());
        }
        
        /// <summary>
        /// Test replication of an archive path to the user's inventory.
        /// </summary>
        [Test]
        public void TestReplicateArchivePathToUserInventory()
        {
            TestHelper.InMethod();

            //log4net.Config.XmlConfigurator.Configure();
            
            Scene scene = SceneSetupHelpers.SetupScene("inventory");
            CommunicationsManager commsManager = scene.CommsManager;
            CachedUserInfo userInfo;

            lock (this)
            {
                userInfo = UserProfileTestUtils.CreateUserWithInventory(commsManager, InventoryReceived);
                Monitor.Wait(this, 60000);
            }
            
            //Console.WriteLine("userInfo.RootFolder 1: {0}", userInfo.RootFolder);
            
            Dictionary <string, InventoryFolderBase> foldersCreated = new Dictionary<string, InventoryFolderBase>();
            List<InventoryNodeBase> nodesLoaded = new List<InventoryNodeBase>();
            
            string folder1Name = "a";
            string folder2Name = "b";
            string itemName = "c.lsl";
            
            string folder1ArchiveName = InventoryArchiveWriteRequest.CreateArchiveFolderName(folder1Name, UUID.Random());
            string folder2ArchiveName = InventoryArchiveWriteRequest.CreateArchiveFolderName(folder2Name, UUID.Random());
            string itemArchiveName = InventoryArchiveWriteRequest.CreateArchiveItemName(itemName, UUID.Random());
            
            string itemArchivePath
                = string.Format(
                    "{0}{1}{2}{3}", 
                    ArchiveConstants.INVENTORY_PATH, folder1ArchiveName, folder2ArchiveName, itemArchiveName);

            //Console.WriteLine("userInfo.RootFolder 2: {0}", userInfo.RootFolder);

            new InventoryArchiveReadRequest(scene, userInfo, null, (Stream)null)
                .ReplicateArchivePathToUserInventory(
                    itemArchivePath, false, scene.InventoryService.GetRootFolder(userInfo.UserProfile.ID), 
                    foldersCreated, nodesLoaded);

            //Console.WriteLine("userInfo.RootFolder 3: {0}", userInfo.RootFolder);
            //InventoryFolderImpl folder1 = userInfo.RootFolder.FindFolderByPath("a");
            InventoryFolderBase folder1 
                = InventoryArchiveUtils.FindFolderByPath(scene.InventoryService, userInfo.UserProfile.ID, "a");
            Assert.That(folder1, Is.Not.Null, "Could not find folder a");
            InventoryFolderBase folder2 = InventoryArchiveUtils.FindFolderByPath(scene.InventoryService, folder1, "b");
            Assert.That(folder2, Is.Not.Null, "Could not find folder b");
        }
    }
}
