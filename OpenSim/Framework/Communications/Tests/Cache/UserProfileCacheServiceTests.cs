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

using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using System.Threading;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Communications.Local;
using OpenSim.Tests.Common.Mock;
using OpenSim.Tests.Common.Setup;
using OpenSim.Tests.Common;

namespace OpenSim.Framework.Communications.Tests
{
    [TestFixture]
    public class UserProfileCacheServiceTests
    {
        /// <value>Used by tests to indicate whether an async operation timed out</value>
        private bool timedOut;
        
        private void InventoryReceived(UUID userId)
        {
            lock (this)
            {
                timedOut = false;
                Monitor.PulseAll(this);
            }
        }
        
        [Test]
        public void TestGetUserDetails()
        {
            TestHelper.InMethod();

            UUID userId = UUID.Parse("00000000-0000-0000-0000-000000000002");
            string firstName = "Bill";
            string lastName = "Bailey";
            CachedUserInfo nonExistingUserInfo;

            TestCommunicationsManager commsManager = new TestCommunicationsManager();
            // Scene myScene = SceneSetupHelpers.SetupScene(commsManager, "");

            // Check we can't retrieve info before it exists by uuid
            nonExistingUserInfo = commsManager.UserProfileCacheService.GetUserDetails(userId);
            Assert.That(nonExistingUserInfo, Is.Null, "User info found by uuid before user creation");

            // Check we can't retrieve info before it exists by name
            nonExistingUserInfo = commsManager.UserProfileCacheService.GetUserDetails(firstName, lastName);
            Assert.That(nonExistingUserInfo, Is.Null, "User info found by name before user creation");

            LocalUserServices lus = (LocalUserServices)commsManager.UserService;
            lus.AddUser(firstName, lastName, "troll", "bill@bailey.com", 1000, 1000, userId);

            CachedUserInfo existingUserInfo;

            // Check we can retrieve info by uuid
            existingUserInfo = commsManager.UserProfileCacheService.GetUserDetails(userId);
            Assert.That(existingUserInfo, Is.Not.Null, "User info not found by uuid");

            // Check we can retrieve info by name
            existingUserInfo = commsManager.UserProfileCacheService.GetUserDetails(firstName, lastName);
            Assert.That(existingUserInfo, Is.Not.Null, "User info not found by name");
        }

        /**
         * Disabled as not fully implemented
        [Test]
        public void TestUpdateProfile()
        {
            UUID userId = UUID.Parse("00000000-0000-0000-0000-000000000292");
            string firstName = "Inspector";
            string originalLastName = "Morse";
            string newLastName = "Gadget";

            UserProfileData newProfile = new UserProfileData();
            newProfile.ID = userId;
            newProfile.FirstName = firstName;
            newProfile.SurName = newLastName;

            TestCommunicationsManager commsManager = new TestCommunicationsManager();
            UserProfileCacheService userCacheService = commsManager.UserProfileCacheService;
            IUserDataPlugin userDataPlugin = commsManager.UserDataPlugin;

            // Check that we can't update info before it exists
            Assert.That(userCacheService.StoreProfile(newProfile), Is.False);
            Assert.That(userDataPlugin.GetUserByUUID(userId), Is.Null);

            // Check that we can update a profile once it exists
            LocalUserServices lus = (LocalUserServices)commsManager.UserService;
            lus.AddUser(firstName, originalLastName, "pingu", "ted@excellentadventure.com", 1000, 1000, userId);

            Assert.That(userCacheService.StoreProfile(newProfile), Is.True);
            UserProfileData retrievedProfile = userCacheService.GetUserDetails(userId).UserProfile;
            Assert.That(retrievedProfile.SurName, Is.EqualTo(newLastName));
            Assert.That(userDataPlugin.GetUserByUUID(userId).SurName, Is.EqualTo(newLastName));
        }
        */

        [Test]
        public void TestFetchInventory()
        {
            TestHelper.InMethod();

            Scene myScene = SceneSetupHelpers.SetupScene("inventory");

            timedOut = true;
            lock (this)
            {
                UserProfileTestUtils.CreateUserWithInventory(myScene.CommsManager, InventoryReceived);
                Monitor.Wait(this, 60000);
             }

            Assert.That(timedOut, Is.False, "Timed out");
        }

        [Test]
        public void TestGetChildFolder()
        {
            TestHelper.InMethod();

            Scene myScene = SceneSetupHelpers.SetupScene("inventory");
            CachedUserInfo userInfo;
            
            lock (this)
            {
                userInfo = UserProfileTestUtils.CreateUserWithInventory(myScene.CommsManager, InventoryReceived);
                Monitor.Wait(this, 60000);
            }

            UUID folderId = UUID.Parse("00000000-0000-0000-0000-000000000011");
            Assert.That(userInfo.RootFolder.GetChildFolder(folderId), Is.Null);
            userInfo.CreateFolder("testFolder", folderId, (ushort)AssetType.Animation, userInfo.RootFolder.ID);

            Assert.That(userInfo.RootFolder.GetChildFolder(folderId), Is.Not.Null);
        }

        [Test]
        public void TestCreateFolder()
        {
            TestHelper.InMethod();

            Scene myScene = SceneSetupHelpers.SetupScene("inventory");
            CachedUserInfo userInfo;

            lock (this)
            {
                userInfo = UserProfileTestUtils.CreateUserWithInventory(myScene.CommsManager, InventoryReceived);
                Monitor.Wait(this, 60000);
            }

            UUID folderId = UUID.Parse("00000000-0000-0000-0000-000000000010");
            Assert.That(userInfo.RootFolder.ContainsChildFolder(folderId), Is.False);

            // 1: Try a folder create that should fail because the parent id given does not exist
            UUID missingFolderId = UUID.Random();
            InventoryFolderBase myFolder = new InventoryFolderBase();
            myFolder.ID = folderId;

            Assert.That(
                userInfo.CreateFolder("testFolder1", folderId, (ushort)AssetType.Animation, missingFolderId), Is.False);
            Assert.That(myScene.InventoryService.GetFolder(myFolder), Is.Null);
            Assert.That(userInfo.RootFolder.ContainsChildFolder(missingFolderId), Is.False);
            Assert.That(userInfo.RootFolder.FindFolder(folderId), Is.Null);

            // 2: Try a folder create that should work
            Assert.That(
                userInfo.CreateFolder("testFolder2", folderId, (ushort)AssetType.Animation, userInfo.RootFolder.ID), Is.True);
            Assert.That(myScene.InventoryService.GetFolder(myFolder), Is.Not.Null);
            Assert.That(userInfo.RootFolder.ContainsChildFolder(folderId), Is.True);
        }

        //[Test]
        public void TestUpdateFolder()
        {
            TestHelper.InMethod();

            Scene myScene = SceneSetupHelpers.SetupScene("inventory");
            CachedUserInfo userInfo;

            lock (this)
            {
                userInfo = UserProfileTestUtils.CreateUserWithInventory(myScene.CommsManager, InventoryReceived);
                Monitor.Wait(this, 60000);
            }

            UUID folder1Id = UUID.Parse("00000000-0000-0000-0000-000000000060");
            InventoryFolderImpl rootFolder = userInfo.RootFolder;
            InventoryFolderBase myFolder = new InventoryFolderBase();
            myFolder.ID = folder1Id;

            userInfo.CreateFolder("folder1", folder1Id, (ushort)AssetType.Animation, rootFolder.ID);

            // 1: Test updates that don't involve moving the folder
            {
                string newFolderName1 = "newFolderName1";
                ushort folderType1 = (ushort)AssetType.Texture;
                userInfo.UpdateFolder(newFolderName1, folder1Id, folderType1, rootFolder.ID);

                InventoryFolderImpl folder1 = rootFolder.GetChildFolder(folder1Id);
                Assert.That(newFolderName1, Is.EqualTo(folder1.Name));
                Assert.That(folderType1, Is.EqualTo((ushort)folder1.Type));

                InventoryFolderBase dataFolder1 = myScene.InventoryService.GetFolder(myFolder);
                Assert.That(newFolderName1, Is.EqualTo(dataFolder1.Name));
                Assert.That(folderType1, Is.EqualTo((ushort)dataFolder1.Type));
            }

            // 2: Test an update that also involves moving the folder
            {
                UUID folder2Id = UUID.Parse("00000000-0000-0000-0000-000000000061");
                userInfo.CreateFolder("folder2", folder2Id, (ushort)AssetType.Animation, rootFolder.ID);
                InventoryFolderImpl folder2 = rootFolder.GetChildFolder(folder2Id);

                InventoryFolderBase myFolder2 = new InventoryFolderBase();
                myFolder2.ID = folder2Id;

                string newFolderName2 = "newFolderName2";
                ushort folderType2 = (ushort)AssetType.Bodypart;
                userInfo.UpdateFolder(newFolderName2, folder1Id, folderType2, folder2Id);

                InventoryFolderImpl folder1 = folder2.GetChildFolder(folder1Id);
                Assert.That(newFolderName2, Is.EqualTo(folder1.Name));
                Assert.That(folderType2, Is.EqualTo((ushort)folder1.Type));
                Assert.That(folder2Id, Is.EqualTo(folder1.ParentID));

                Assert.That(folder2.ContainsChildFolder(folder1Id), Is.True);
                Assert.That(rootFolder.ContainsChildFolder(folder1Id), Is.False);

                InventoryFolderBase dataFolder1 = myScene.InventoryService.GetFolder(myFolder2);
                Assert.That(newFolderName2, Is.EqualTo(dataFolder1.Name));
                Assert.That(folderType2, Is.EqualTo((ushort)dataFolder1.Type));
                Assert.That(folder2Id, Is.EqualTo(dataFolder1.ParentID));
            }

        }

        [Test]
        public void TestMoveFolder()
        {
            TestHelper.InMethod();

            Scene myScene = SceneSetupHelpers.SetupScene("inventory");
            CachedUserInfo userInfo;

            lock (this)
            {
                userInfo = UserProfileTestUtils.CreateUserWithInventory(myScene.CommsManager, InventoryReceived);
                Monitor.Wait(this, 60000);
            }

            UUID folder1Id = UUID.Parse("00000000-0000-0000-0000-000000000020");
            UUID folder2Id = UUID.Parse("00000000-0000-0000-0000-000000000021");
            UUID folderToMoveId = UUID.Parse("00000000-0000-0000-0000-000000000030");
            InventoryFolderImpl rootFolder = userInfo.RootFolder;

            userInfo.CreateFolder("folder1", folder1Id, (ushort)AssetType.Animation, rootFolder.ID);
            InventoryFolderImpl folder1 = rootFolder.GetChildFolder(folder1Id);
            userInfo.CreateFolder("folder2", folder2Id, (ushort)AssetType.Animation, rootFolder.ID);
            InventoryFolderImpl folder2 = rootFolder.GetChildFolder(folder2Id);

            // Check folder is currently in folder1
            userInfo.CreateFolder("folderToMove", folderToMoveId, (ushort)AssetType.Animation, folder1Id);
            Assert.That(folder1.ContainsChildFolder(folderToMoveId), Is.True);

            userInfo.MoveFolder(folderToMoveId, folder2Id);

            // Check folder is now in folder2 and no trace remains in folder1
            InventoryFolderBase myFolder = new InventoryFolderBase();
            myFolder.ID = folderToMoveId;
            Assert.That(folder2.ContainsChildFolder(folderToMoveId), Is.True);
            Assert.That(myScene.InventoryService.GetFolder(myFolder).ParentID, Is.EqualTo(folder2Id));

            Assert.That(folder1.ContainsChildFolder(folderToMoveId), Is.False);
        }

        [Test]
        public void TestPurgeFolder()
        {
            TestHelper.InMethod();
            //log4net.Config.XmlConfigurator.Configure();

            Scene myScene = SceneSetupHelpers.SetupScene("inventory");
            CachedUserInfo userInfo;

            lock (this)
            {
                userInfo = UserProfileTestUtils.CreateUserWithInventory(myScene.CommsManager, InventoryReceived);
                Monitor.Wait(this, 60000);
            }

            UUID folder1Id = UUID.Parse("00000000-0000-0000-0000-000000000070");
            InventoryFolderImpl rootFolder = userInfo.RootFolder;
            InventoryFolderBase myFolder = new InventoryFolderBase();
            myFolder.ID = folder1Id;

            userInfo.CreateFolder("folder1", folder1Id, (ushort)AssetType.Animation, rootFolder.ID);
            Assert.That(myScene.InventoryService.GetFolder(myFolder), Is.Not.Null);

            // Test purge
            userInfo.PurgeFolder(rootFolder.ID);

            Assert.That(rootFolder.RequestListOfFolders(), Is.Empty);
            Assert.That(myScene.InventoryService.GetFolder(myFolder), Is.Null);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (MainServer.Instance != null) MainServer.Instance.Stop();
            }
            catch (System.NullReferenceException)
            { }
        }
    }
}