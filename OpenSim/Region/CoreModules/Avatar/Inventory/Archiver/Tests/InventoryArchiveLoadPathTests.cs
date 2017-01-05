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
using OpenSim.Region.CoreModules.Avatar.Inventory.Archiver;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Archiver.Tests
{
    [TestFixture]
    public class InventoryArchiveLoadPathTests : InventoryArchiveTestCase
    {
        /// <summary>
        /// Test loading an IAR to various different inventory paths.
        /// </summary>
        [Test]
        public void TestLoadIarToInventoryPaths()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            SerialiserModule serialiserModule = new SerialiserModule();
            InventoryArchiverModule archiverModule = new InventoryArchiverModule();

            // Annoyingly, we have to set up a scene even though inventory loading has nothing to do with a scene
            Scene scene = new SceneHelpers().SetupScene();

            SceneHelpers.SetupSceneModules(scene, serialiserModule, archiverModule);

            UserAccountHelpers.CreateUserWithInventory(scene, m_uaMT, "meowfood");
            UserAccountHelpers.CreateUserWithInventory(scene, m_uaLL1, "hampshire");

            archiverModule.DearchiveInventory(UUID.Random(), m_uaMT.FirstName, m_uaMT.LastName, "/", "meowfood", m_iarStream);
            InventoryItemBase foundItem1
                = InventoryArchiveUtils.FindItemByPath(scene.InventoryService, m_uaMT.PrincipalID, m_item1Name);

            Assert.That(foundItem1, Is.Not.Null, "Didn't find loaded item 1");

            // Now try loading to a root child folder
            UserInventoryHelpers.CreateInventoryFolder(scene.InventoryService, m_uaMT.PrincipalID, "xA", false);
            MemoryStream archiveReadStream = new MemoryStream(m_iarStream.ToArray());
            archiverModule.DearchiveInventory(UUID.Random(), m_uaMT.FirstName, m_uaMT.LastName, "xA", "meowfood", archiveReadStream);

            InventoryItemBase foundItem2
                = InventoryArchiveUtils.FindItemByPath(scene.InventoryService, m_uaMT.PrincipalID, "xA/" + m_item1Name);
            Assert.That(foundItem2, Is.Not.Null, "Didn't find loaded item 2");

            // Now try loading to a more deeply nested folder
            UserInventoryHelpers.CreateInventoryFolder(scene.InventoryService, m_uaMT.PrincipalID, "xB/xC", false);
            archiveReadStream = new MemoryStream(archiveReadStream.ToArray());
            archiverModule.DearchiveInventory(UUID.Random(), m_uaMT.FirstName, m_uaMT.LastName, "xB/xC", "meowfood", archiveReadStream);

            InventoryItemBase foundItem3
                = InventoryArchiveUtils.FindItemByPath(scene.InventoryService, m_uaMT.PrincipalID, "xB/xC/" + m_item1Name);
            Assert.That(foundItem3, Is.Not.Null, "Didn't find loaded item 3");
        }

        /// <summary>
        /// Test that things work when the load path specified starts with a slash
        /// </summary>
        [Test]
        public void TestLoadIarPathStartsWithSlash()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            SerialiserModule serialiserModule = new SerialiserModule();
            InventoryArchiverModule archiverModule = new InventoryArchiverModule();
            Scene scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(scene, serialiserModule, archiverModule);

            UserAccountHelpers.CreateUserWithInventory(scene, m_uaMT, "password");
            archiverModule.DearchiveInventory(UUID.Random(), m_uaMT.FirstName, m_uaMT.LastName, "/Objects", "password", m_iarStream);

            InventoryItemBase foundItem1
                = InventoryArchiveUtils.FindItemByPath(
                    scene.InventoryService, m_uaMT.PrincipalID, "/Objects/" + m_item1Name);

            Assert.That(foundItem1, Is.Not.Null, "Didn't find loaded item 1 in TestLoadIarFolderStartsWithSlash()");
        }

        [Test]
        public void TestLoadIarPathWithEscapedChars()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            string itemName = "You & you are a mean/man/";
            string humanEscapedItemName = @"You & you are a mean\/man\/";
            string userPassword = "meowfood";

            InventoryArchiverModule archiverModule = new InventoryArchiverModule();

            Scene scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(scene, archiverModule);

            // Create user
            string userFirstName = "Jock";
            string userLastName = "Stirrup";
            UUID userId = UUID.Parse("00000000-0000-0000-0000-000000000020");
            UserAccountHelpers.CreateUserWithInventory(scene, userFirstName, userLastName, userId, "meowfood");

            // Create asset
            SceneObjectGroup object1;
            SceneObjectPart part1;
            {
                string partName = "part name";
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
            AssetBase asset1 = AssetHelpers.CreateAsset(asset1Id, object1);
            scene.AssetService.Store(asset1);

            // Create item
            UUID item1Id = UUID.Parse("00000000-0000-0000-0000-000000000080");
            InventoryItemBase item1 = new InventoryItemBase();
            item1.Name = itemName;
            item1.AssetID = asset1.FullID;
            item1.ID = item1Id;
            InventoryFolderBase objsFolder
                = InventoryArchiveUtils.FindFoldersByPath(scene.InventoryService, userId, "Objects")[0];
            item1.Folder = objsFolder.ID;
            scene.AddInventoryItem(item1);

            MemoryStream archiveWriteStream = new MemoryStream();
            archiverModule.OnInventoryArchiveSaved += SaveCompleted;

            mre.Reset();
            archiverModule.ArchiveInventory(
                UUID.Random(), userFirstName, userLastName, "Objects", userPassword, archiveWriteStream);
            mre.WaitOne(60000, false);

            // LOAD ITEM
            MemoryStream archiveReadStream = new MemoryStream(archiveWriteStream.ToArray());

            archiverModule.DearchiveInventory(UUID.Random(), userFirstName, userLastName, "Scripts", userPassword, archiveReadStream);

            InventoryItemBase foundItem1
                = InventoryArchiveUtils.FindItemByPath(
                    scene.InventoryService, userId, "Scripts/Objects/" + humanEscapedItemName);

            Assert.That(foundItem1, Is.Not.Null, "Didn't find loaded item 1");
//            Assert.That(
//                foundItem1.CreatorId, Is.EqualTo(userUuid),
//                "Loaded item non-uuid creator doesn't match that of the loading user");
            Assert.That(
                foundItem1.Name, Is.EqualTo(itemName),
                "Loaded item name doesn't match saved name");
        }

        /// <summary>
        /// Test replication of an archive path to the user's inventory.
        /// </summary>
        [Test]
        public void TestNewIarPath()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            Scene scene = new SceneHelpers().SetupScene();
            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(scene);

            Dictionary <string, InventoryFolderBase> foldersCreated = new Dictionary<string, InventoryFolderBase>();
            HashSet<InventoryNodeBase> nodesLoaded = new HashSet<InventoryNodeBase>();

            string folder1Name = "1";
            string folder2aName = "2a";
            string folder2bName = "2b";

            string folder1ArchiveName = InventoryArchiveWriteRequest.CreateArchiveFolderName(folder1Name, UUID.Random());
            string folder2aArchiveName = InventoryArchiveWriteRequest.CreateArchiveFolderName(folder2aName, UUID.Random());
            string folder2bArchiveName = InventoryArchiveWriteRequest.CreateArchiveFolderName(folder2bName, UUID.Random());

            string iarPath1 = string.Join("", new string[] { folder1ArchiveName, folder2aArchiveName });
            string iarPath2 = string.Join("", new string[] { folder1ArchiveName, folder2bArchiveName });

            {
                // Test replication of path1
                new InventoryArchiveReadRequest(UUID.Random(), null, scene.InventoryService, scene.AssetService, scene.UserAccountService, ua1, null, (Stream)null, false)
                    .ReplicateArchivePathToUserInventory(
                        iarPath1, scene.InventoryService.GetRootFolder(ua1.PrincipalID),
                        foldersCreated, nodesLoaded);

                List<InventoryFolderBase> folder1Candidates
                    = InventoryArchiveUtils.FindFoldersByPath(scene.InventoryService, ua1.PrincipalID, folder1Name);
                Assert.That(folder1Candidates.Count, Is.EqualTo(1));

                InventoryFolderBase folder1 = folder1Candidates[0];
                List<InventoryFolderBase> folder2aCandidates
                    = InventoryArchiveUtils.FindFoldersByPath(scene.InventoryService, folder1, folder2aName);
                Assert.That(folder2aCandidates.Count, Is.EqualTo(1));
            }

            {
                // Test replication of path2
                new InventoryArchiveReadRequest(UUID.Random(), null, scene.InventoryService, scene.AssetService, scene.UserAccountService, ua1, null, (Stream)null, false)
                    .ReplicateArchivePathToUserInventory(
                        iarPath2, scene.InventoryService.GetRootFolder(ua1.PrincipalID),
                        foldersCreated, nodesLoaded);

                List<InventoryFolderBase> folder1Candidates
                    = InventoryArchiveUtils.FindFoldersByPath(scene.InventoryService, ua1.PrincipalID, folder1Name);
                Assert.That(folder1Candidates.Count, Is.EqualTo(1));

                InventoryFolderBase folder1 = folder1Candidates[0];

                List<InventoryFolderBase> folder2aCandidates
                    = InventoryArchiveUtils.FindFoldersByPath(scene.InventoryService, folder1, folder2aName);
                Assert.That(folder2aCandidates.Count, Is.EqualTo(1));

                List<InventoryFolderBase> folder2bCandidates
                    = InventoryArchiveUtils.FindFoldersByPath(scene.InventoryService, folder1, folder2bName);
                Assert.That(folder2bCandidates.Count, Is.EqualTo(1));
            }
        }

        /// <summary>
        /// Test replication of a partly existing archive path to the user's inventory.  This should create
        /// a duplicate path without the merge option.
        /// </summary>
        [Test]
        public void TestPartExistingIarPath()
        {
            TestHelpers.InMethod();
            //log4net.Config.XmlConfigurator.Configure();

            Scene scene = new SceneHelpers().SetupScene();
            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(scene);

            string folder1ExistingName = "a";
            string folder2Name = "b";

            InventoryFolderBase folder1
                = UserInventoryHelpers.CreateInventoryFolder(
                    scene.InventoryService, ua1.PrincipalID, folder1ExistingName, false);

            string folder1ArchiveName = InventoryArchiveWriteRequest.CreateArchiveFolderName(folder1ExistingName, UUID.Random());
            string folder2ArchiveName = InventoryArchiveWriteRequest.CreateArchiveFolderName(folder2Name, UUID.Random());

            string itemArchivePath = string.Join("", new string[] { folder1ArchiveName, folder2ArchiveName });

            new InventoryArchiveReadRequest(UUID.Random(), null, scene.InventoryService, scene.AssetService, scene.UserAccountService, ua1, null, (Stream)null, false)
                .ReplicateArchivePathToUserInventory(
                    itemArchivePath, scene.InventoryService.GetRootFolder(ua1.PrincipalID),
                    new Dictionary<string, InventoryFolderBase>(), new HashSet<InventoryNodeBase>());

            List<InventoryFolderBase> folder1PostCandidates
                = InventoryArchiveUtils.FindFoldersByPath(scene.InventoryService, ua1.PrincipalID, folder1ExistingName);
            Assert.That(folder1PostCandidates.Count, Is.EqualTo(2));

            // FIXME: Temporarily, we're going to do something messy to make sure we pick up the created folder.
            InventoryFolderBase folder1Post = null;
            foreach (InventoryFolderBase folder in folder1PostCandidates)
            {
                if (folder.ID != folder1.ID)
                {
                    folder1Post = folder;
                    break;
                }
            }
//            Assert.That(folder1Post.ID, Is.EqualTo(folder1.ID));

            List<InventoryFolderBase> folder2PostCandidates
                = InventoryArchiveUtils.FindFoldersByPath(scene.InventoryService, folder1Post, "b");
            Assert.That(folder2PostCandidates.Count, Is.EqualTo(1));
        }

        /// <summary>
        /// Test replication of a partly existing archive path to the user's inventory.  This should create
        /// a merged path.
        /// </summary>
        [Test]
        public void TestMergeIarPath()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            Scene scene = new SceneHelpers().SetupScene();
            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(scene);

            string folder1ExistingName = "a";
            string folder2Name = "b";

            InventoryFolderBase folder1
                = UserInventoryHelpers.CreateInventoryFolder(
                    scene.InventoryService, ua1.PrincipalID, folder1ExistingName, false);

            string folder1ArchiveName = InventoryArchiveWriteRequest.CreateArchiveFolderName(folder1ExistingName, UUID.Random());
            string folder2ArchiveName = InventoryArchiveWriteRequest.CreateArchiveFolderName(folder2Name, UUID.Random());

            string itemArchivePath = string.Join("", new string[] { folder1ArchiveName, folder2ArchiveName });

            new InventoryArchiveReadRequest(UUID.Random(), null, scene.InventoryService, scene.AssetService, scene.UserAccountService, ua1, folder1ExistingName, (Stream)null, true)
                .ReplicateArchivePathToUserInventory(
                    itemArchivePath, scene.InventoryService.GetRootFolder(ua1.PrincipalID),
                    new Dictionary<string, InventoryFolderBase>(), new HashSet<InventoryNodeBase>());

            List<InventoryFolderBase> folder1PostCandidates
                = InventoryArchiveUtils.FindFoldersByPath(scene.InventoryService, ua1.PrincipalID, folder1ExistingName);
            Assert.That(folder1PostCandidates.Count, Is.EqualTo(1));
            Assert.That(folder1PostCandidates[0].ID, Is.EqualTo(folder1.ID));

            List<InventoryFolderBase> folder2PostCandidates
                = InventoryArchiveUtils.FindFoldersByPath(scene.InventoryService, folder1PostCandidates[0], "b");
            Assert.That(folder2PostCandidates.Count, Is.EqualTo(1));
        }
    }
}

