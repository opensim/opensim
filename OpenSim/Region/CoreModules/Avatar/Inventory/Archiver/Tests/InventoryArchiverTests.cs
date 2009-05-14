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
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;
using OpenSim.Tests.Common.Setup;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Archiver.Tests
{
    [TestFixture]
    public class InventoryArchiverTests
    {
        private void SaveCompleted(
            bool succeeded, CachedUserInfo userInfo, string invPath, Stream saveStream, Exception reportedException)
        {
            lock (this)
            {
                Monitor.PulseAll(this);
            }
        }

        /// <summary>
        /// Test saving a V0.1 OpenSim Inventory Archive (subject to change since there is no fixed format yet).
        /// </summary>
        [Test]
        public void TestSaveIarV0p1()
        {
            TestHelper.InMethod();
            //log4net.Config.XmlConfigurator.Configure();

            InventoryArchiverModule archiverModule = new InventoryArchiverModule();

            Scene scene = SceneSetupHelpers.SetupScene(false);
            SceneSetupHelpers.SetupSceneModules(scene, archiverModule);
            CommunicationsManager cm = scene.CommsManager;

            // Create user
            string userFirstName = "Jock";
            string userLastName = "Stirrup";
            UUID userId = UUID.Parse("00000000-0000-0000-0000-000000000020");
            cm.UserAdminService.AddUser(userFirstName, userLastName, string.Empty, string.Empty, 1000, 1000, userId);
            CachedUserInfo userInfo = cm.UserProfileCacheService.GetUserDetails(userId);
            userInfo.FetchInventory();

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
            cm.AssetCache.AddAsset(asset1);

            // Create item
            UUID item1Id = UUID.Parse("00000000-0000-0000-0000-000000000080");
            InventoryItemBase item1 = new InventoryItemBase();
            item1.Name = "My Little Dog";
            item1.AssetID = asset1.FullID;
            item1.ID = item1Id;
            item1.Folder = userInfo.RootFolder.FindFolderByPath("Objects").ID;
            scene.AddInventoryItem(userId, item1);

            MemoryStream archiveWriteStream = new MemoryStream();
            archiverModule.OnInventoryArchiveSaved += SaveCompleted;

            lock (this)
            {
                archiverModule.ArchiveInventory(userFirstName, userLastName, "Objects", archiveWriteStream);
                AssetServerBase assetServer = (AssetServerBase)scene.CommsManager.AssetCache.AssetServer;
                while (assetServer.HasWaitingRequests())
                    assetServer.ProcessNextRequest();
                
                Monitor.Wait(this, 60000);
            }

            byte[] archive = archiveWriteStream.ToArray();
            MemoryStream archiveReadStream = new MemoryStream(archive);
            TarArchiveReader tar = new TarArchiveReader(archiveReadStream);

            InventoryFolderImpl objectsFolder = userInfo.RootFolder.FindFolderByPath("Objects");

            //bool gotControlFile = false;
            bool gotObject1File = false;
            //bool gotObject2File = false;
            string expectedObject1FilePath = string.Format(
                "{0}{1}/{2}_{3}.xml",
                ArchiveConstants.INVENTORY_PATH,
                string.Format(
                    "Objects{0}{1}", ArchiveConstants.INVENTORY_NODE_NAME_COMPONENT_SEPARATOR, objectsFolder.ID),
                item1.Name,
                item1Id);

/*
            string expectedObject2FileName = string.Format(
                "{0}_{1:000}-{2:000}-{3:000}__{4}.xml",
                part2.Name,
                Math.Round(part2.GroupPosition.X), Math.Round(part2.GroupPosition.Y), Math.Round(part2.GroupPosition.Z),
                part2.UUID);
                */

            string filePath;
            TarArchiveReader.TarEntryType tarEntryType;

            while (tar.ReadEntry(out filePath, out tarEntryType) != null)
            {
                Console.WriteLine("Got {0}", filePath);

                /*
                if (ArchiveConstants.CONTROL_FILE_PATH == filePath)
                {
                    gotControlFile = true;
                }
                */
                if (filePath.StartsWith(ArchiveConstants.INVENTORY_PATH) && filePath.EndsWith(".xml"))
                {
                    //string fileName = filePath.Remove(0, "Objects/".Length);

                    //if (fileName.StartsWith(part1.Name))
                    //{
                        Assert.That(filePath, Is.EqualTo(expectedObject1FilePath));
                        gotObject1File = true;
                    //}
                    //else if (fileName.StartsWith(part2.Name))
                    //{
                    //    Assert.That(fileName, Is.EqualTo(expectedObject2FileName));
                    //    gotObject2File = true;
                    //}
                }
            }

            //Assert.That(gotControlFile, Is.True, "No control file in archive");
            Assert.That(gotObject1File, Is.True, "No item1 file in archive");
            //Assert.That(gotObject2File, Is.True, "No object2 file in archive");

            // TODO: Test presence of more files and contents of files.
        }
        
        /// <summary>
        /// Test loading a V0.1 OpenSim Inventory Archive (subject to change since there is no fixed format yet) where
        /// an account exists with the creator name.
        /// </summary>
        [Test]
        public void TestLoadIarV0p1ExistingUsers()
        {   
            Assert.Ignore();
            TestHelper.InMethod();
            Console.WriteLine("Started {0}", MethodBase.GetCurrentMethod());
            
            //log4net.Config.XmlConfigurator.Configure();
            
            string userFirstName = "Mr";
            string userLastName = "Tiddles";
            UUID userUuid = UUID.Parse("00000000-0000-0000-0000-000000000555");
            string user2FirstName = "Lord";
            string user2LastName = "Lucan";
            UUID user2Uuid = UUID.Parse("00000000-0000-0000-0000-000000000666");
            
            string itemName = "b.lsl";
            string archiveItemName
                = string.Format("{0}{1}{2}", itemName, "_", UUID.Random());            

            MemoryStream archiveWriteStream = new MemoryStream();
            TarArchiveWriter tar = new TarArchiveWriter(archiveWriteStream);

            InventoryItemBase item1 = new InventoryItemBase();
            item1.Name = itemName;
            item1.AssetID = UUID.Random();
            item1.GroupID = UUID.Random();
            item1.CreatorId = OspResolver.MakeOspa(user2FirstName, user2LastName);
            //item1.CreatorId = userUuid.ToString();
            //item1.CreatorId = "00000000-0000-0000-0000-000000000444";
            item1.Owner = UUID.Zero;
            
            string item1FileName 
                = string.Format("{0}{1}", ArchiveConstants.INVENTORY_PATH, archiveItemName);
            tar.WriteFile(item1FileName, UserInventoryItemSerializer.Serialize(item1));
            tar.Close();

            MemoryStream archiveReadStream = new MemoryStream(archiveWriteStream.ToArray());            
            SerialiserModule serialiserModule = new SerialiserModule();
            InventoryArchiverModule archiverModule = new InventoryArchiverModule();
            
            // Annoyingly, we have to set up a scene even though inventory loading has nothing to do with a scene
            Scene scene = SceneSetupHelpers.SetupScene();
            IUserAdminService userAdminService = scene.CommsManager.UserAdminService;
            
            SceneSetupHelpers.SetupSceneModules(scene, serialiserModule, archiverModule);
            userAdminService.AddUser(
                userFirstName, userLastName, "meowfood", String.Empty, 1000, 1000, userUuid);
            userAdminService.AddUser(
                user2FirstName, user2LastName, "hampshire", String.Empty, 1000, 1000, user2Uuid);
            
            archiverModule.DearchiveInventory(userFirstName, userLastName, "/", archiveReadStream);

            CachedUserInfo userInfo 
                = scene.CommsManager.UserProfileCacheService.GetUserDetails(userFirstName, userLastName);            
            InventoryItemBase foundItem = userInfo.RootFolder.FindItemByPath(itemName);
            
            Assert.That(foundItem.CreatorId, Is.EqualTo(item1.CreatorId));
            Assert.That(foundItem.CreatorIdAsUuid, Is.EqualTo(user2Uuid));
            Assert.That(foundItem.Owner, Is.EqualTo(userUuid));            
            
            Console.WriteLine("Successfully completed {0}", MethodBase.GetCurrentMethod());
        }

        /// <summary>
        /// Test loading a V0.1 OpenSim Inventory Archive (subject to change since there is no fixed format yet) where
        /// no account exists with the creator name 
        /// </summary>
        [Test]
        public void TestLoadIarV0p1TempProfiles()
        {   
            Assert.Ignore();
            TestHelper.InMethod();
            Console.WriteLine("### Started {0} ###", MethodBase.GetCurrentMethod());
            
            log4net.Config.XmlConfigurator.Configure();
            
            string userFirstName = "Dennis";
            string userLastName = "Menace";
            UUID userUuid = UUID.Parse("00000000-0000-0000-0000-000000000aaa");
            string user2FirstName = "Walter";
            string user2LastName = "Mitty";
            
            string itemName = "b.lsl";
            string archiveItemName
                = string.Format("{0}{1}{2}", itemName, "_", UUID.Random());            

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
            InventoryArchiverModule archiverModule = new InventoryArchiverModule();
            
            // Annoyingly, we have to set up a scene even though inventory loading has nothing to do with a scene
            Scene scene = SceneSetupHelpers.SetupScene();
            IUserAdminService userAdminService = scene.CommsManager.UserAdminService;
            
            SceneSetupHelpers.SetupSceneModules(scene, serialiserModule, archiverModule);
            userAdminService.AddUser(
                userFirstName, userLastName, "meowfood", String.Empty, 1000, 1000, userUuid);
            
            archiverModule.DearchiveInventory(userFirstName, userLastName, "/", archiveReadStream);
            
            // Check that a suitable temporary user profile has been created.
            UserProfileData user2Profile 
                = scene.CommsManager.UserService.GetUserProfile(
                    OspResolver.HashName(user2FirstName + " " + user2LastName));
            Assert.That(user2Profile, Is.Not.Null);
            Assert.That(user2Profile.FirstName == user2FirstName);
            Assert.That(user2Profile.SurName == user2LastName);
            
            CachedUserInfo userInfo 
                = scene.CommsManager.UserProfileCacheService.GetUserDetails(userFirstName, userLastName);            
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
            CommunicationsManager commsManager = new TestCommunicationsManager();            
            CachedUserInfo userInfo = UserProfileTestUtils.CreateUserWithInventory(commsManager);
            Dictionary <string, InventoryFolderImpl> foldersCreated = new Dictionary<string, InventoryFolderImpl>();
            List<InventoryNodeBase> nodesLoaded = new List<InventoryNodeBase>();
            
            string folder1Name = "a";
            string folder2Name = "b";
            string itemName = "c.lsl";
            
            string folder1ArchiveName 
                = string.Format(
                    "{0}{1}{2}", folder1Name, ArchiveConstants.INVENTORY_NODE_NAME_COMPONENT_SEPARATOR, UUID.Random());
            string folder2ArchiveName
                = string.Format(
                    "{0}{1}{2}", folder2Name, ArchiveConstants.INVENTORY_NODE_NAME_COMPONENT_SEPARATOR, UUID.Random());
            string itemArchivePath
                = string.Format(
                    "{0}{1}/{2}/{3}", 
                    ArchiveConstants.INVENTORY_PATH, folder1ArchiveName, folder2ArchiveName, itemName);            
            
            new InventoryArchiveReadRequest(userInfo, null, (Stream)null, null)
                .ReplicateArchivePathToUserInventory(itemArchivePath, false, userInfo.RootFolder, foldersCreated, nodesLoaded);
            
            InventoryFolderImpl folder1 = userInfo.RootFolder.FindFolderByPath("a");
            Assert.That(folder1, Is.Not.Null, "Could not find folder a");
            InventoryFolderImpl folder2 = folder1.FindFolderByPath("b");            
            Assert.That(folder2, Is.Not.Null, "Could not find folder b");
        }
    }
}
