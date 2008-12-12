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
using log4net;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Communications.Local;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Framework.Communications.Tests
{       
    /// <summary>
    /// User profile cache service tests
    /// </summary>
    [TestFixture]    
    public class UserProfileCacheServiceTests
    {        
        /// <summary>
        /// Test user details get.
        /// </summary>
        [Test]
        public void TestGetUserDetails()
        {
            UUID nonExistingUserId = UUID.Parse("00000000-0000-0000-0000-000000000001");
            UUID existingUserId = UUID.Parse("00000000-0000-0000-0000-000000000002");
            
            CommunicationsManager commsManager = UserProfileTestUtils.SetupServices();
            CachedUserInfo existingUserInfo = UserProfileTestUtils.CreateUserWithInventory(commsManager, existingUserId);
            
            Assert.That(existingUserInfo, Is.Not.Null, "Existing user info unexpectedly not found");
            
            CachedUserInfo nonExistingUserInfo = commsManager.UserProfileCacheService.GetUserDetails(nonExistingUserId);
            
            Assert.That(nonExistingUserInfo, Is.Null, "Non existing user info unexpectedly found");                        
        }
        
        /// <summary>
        /// Test requesting inventory for a user
        /// </summary>
        [Test]
        public void TestRequestInventoryForUser()
        {
            CommunicationsManager commsManager = UserProfileTestUtils.SetupServices();
            CachedUserInfo userInfo = UserProfileTestUtils.CreateUserWithInventory(commsManager);          
            
            Assert.That(userInfo.HasReceivedInventory, Is.True);
        }
                
        /// <summary>
        /// Test retrieving a child folder
        /// </summary>
        [Test]
        public void TestGetChildFolder()
        {
            CommunicationsManager commsManager = UserProfileTestUtils.SetupServices();
            CachedUserInfo userInfo = UserProfileTestUtils.CreateUserWithInventory(commsManager);
            
            UUID folderId = UUID.Parse("00000000-0000-0000-0000-000000000011");
            
            Assert.That(userInfo.RootFolder.GetChildFolder(folderId), Is.Null);                                   
            userInfo.CreateFolder("testFolder", folderId, (ushort)AssetType.Animation, userInfo.RootFolder.ID);
            
            Assert.That(userInfo.RootFolder.GetChildFolder(folderId), Is.Not.Null);
        }           
        
        /// <summary>
        /// Test creating an inventory folder
        /// </summary>
        [Test]
        public void TestCreateFolder()
        {
            IUserDataPlugin userDataPlugin = new TestUserDataPlugin();
            IInventoryDataPlugin inventoryDataPlugin = new TestInventoryDataPlugin();
            
            CommunicationsManager commsManager 
                = UserProfileTestUtils.SetupServices(userDataPlugin, inventoryDataPlugin);
            CachedUserInfo userInfo = UserProfileTestUtils.CreateUserWithInventory(commsManager);
            
            UUID folderId = UUID.Parse("00000000-0000-0000-0000-000000000010");            
            Assert.That(userInfo.RootFolder.ContainsChildFolder(folderId), Is.False);
            
            // 1: Try a folder create that should fail because the parent id given does not exist
            UUID missingFolderId = UUID.Random();
             
            Assert.That(
                userInfo.CreateFolder("testFolder1", folderId, (ushort)AssetType.Animation, missingFolderId), Is.False);
            Assert.That(inventoryDataPlugin.getInventoryFolder(folderId), Is.Null);
            Assert.That(userInfo.RootFolder.ContainsChildFolder(missingFolderId), Is.False);
            Assert.That(userInfo.RootFolder.FindFolder(folderId), Is.Null);
            
            // 2: Try a folder create that should work
            Assert.That(
                userInfo.CreateFolder("testFolder2", folderId, (ushort)AssetType.Animation, userInfo.RootFolder.ID), Is.True);
            Assert.That(inventoryDataPlugin.getInventoryFolder(folderId), Is.Not.Null);
            Assert.That(userInfo.RootFolder.ContainsChildFolder(folderId), Is.True);
        }
        
        /// <summary>
        /// Test updating a folder
        /// </summary>
        [Test]
        public void TestUpdateFolder()
        {
            IUserDataPlugin userDataPlugin = new TestUserDataPlugin();
            IInventoryDataPlugin inventoryDataPlugin = new TestInventoryDataPlugin();
            
            CommunicationsManager commsManager 
                = UserProfileTestUtils.SetupServices(userDataPlugin, inventoryDataPlugin);
            CachedUserInfo userInfo = UserProfileTestUtils.CreateUserWithInventory(commsManager);
            
            UUID folder1Id = UUID.Parse("00000000-0000-0000-0000-000000000060");
            InventoryFolderImpl rootFolder = userInfo.RootFolder;
            
            userInfo.CreateFolder("folder1", folder1Id, (ushort)AssetType.Animation, rootFolder.ID);
            
            // 1: Test updates that don't involve moving the folder
            {
                string newFolderName1 = "newFolderName1";
                ushort folderType1 = (ushort)AssetType.Texture;
                userInfo.UpdateFolder(newFolderName1, folder1Id, folderType1, rootFolder.ID);
                
                InventoryFolderImpl folder1 = rootFolder.GetChildFolder(folder1Id);
                Assert.That(newFolderName1, Is.EqualTo(folder1.Name));
                Assert.That(folderType1, Is.EqualTo((ushort)folder1.Type));
                
                InventoryFolderBase dataFolder1 = inventoryDataPlugin.getInventoryFolder(folder1Id);
                Assert.That(newFolderName1, Is.EqualTo(dataFolder1.Name));
                Assert.That(folderType1, Is.EqualTo((ushort)dataFolder1.Type));
            }
            
            // 2: Test an update that also involves moving the folder
            {
                UUID folder2Id = UUID.Parse("00000000-0000-0000-0000-000000000061");
                userInfo.CreateFolder("folder2", folder2Id, (ushort)AssetType.Animation, rootFolder.ID);
                InventoryFolderImpl folder2 = rootFolder.GetChildFolder(folder2Id);
                
                string newFolderName2 = "newFolderName2";
                ushort folderType2 = (ushort)AssetType.Bodypart;
                userInfo.UpdateFolder(newFolderName2, folder1Id, folderType2, folder2Id);
                
                InventoryFolderImpl folder1 = folder2.GetChildFolder(folder1Id);
                Assert.That(newFolderName2, Is.EqualTo(folder1.Name));
                Assert.That(folderType2, Is.EqualTo((ushort)folder1.Type));
                Assert.That(folder2Id, Is.EqualTo(folder1.ParentID));
                
                Assert.That(folder2.ContainsChildFolder(folder1Id), Is.True);
                Assert.That(rootFolder.ContainsChildFolder(folder1Id), Is.False);
                
                InventoryFolderBase dataFolder1 = inventoryDataPlugin.getInventoryFolder(folder1Id);
                Assert.That(newFolderName2, Is.EqualTo(dataFolder1.Name));
                Assert.That(folderType2, Is.EqualTo((ushort)dataFolder1.Type));  
                Assert.That(folder2Id, Is.EqualTo(dataFolder1.ParentID));
            }
            
        }            

        /// <summary>
        /// Test moving an inventory folder
        /// </summary>
        [Test]
        public void TestMoveFolder()
        {
            IUserDataPlugin userDataPlugin = new TestUserDataPlugin();
            IInventoryDataPlugin inventoryDataPlugin = new TestInventoryDataPlugin();
            
            CommunicationsManager commsManager 
                = UserProfileTestUtils.SetupServices(userDataPlugin, inventoryDataPlugin);
            CachedUserInfo userInfo = UserProfileTestUtils.CreateUserWithInventory(commsManager);

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
            Assert.That(folder2.ContainsChildFolder(folderToMoveId), Is.True);
            Assert.That(inventoryDataPlugin.getInventoryFolder(folderToMoveId).ParentID, Is.EqualTo(folder2Id));
            
            Assert.That(folder1.ContainsChildFolder(folderToMoveId), Is.False);
        }
        
        /// <summary>
        /// Test purging an inventory folder
        /// </summary>
        [Test]
        public void TestPurgeFolder()
        {
            //log4net.Config.XmlConfigurator.Configure();
            
            IUserDataPlugin userDataPlugin = new TestUserDataPlugin();
            IInventoryDataPlugin inventoryDataPlugin = new TestInventoryDataPlugin();
            
            CommunicationsManager commsManager 
                = UserProfileTestUtils.SetupServices(userDataPlugin, inventoryDataPlugin);
            CachedUserInfo userInfo = UserProfileTestUtils.CreateUserWithInventory(commsManager);
            
            UUID folder1Id = UUID.Parse("00000000-0000-0000-0000-000000000070");
            InventoryFolderImpl rootFolder = userInfo.RootFolder;
            
            userInfo.CreateFolder("folder1", folder1Id, (ushort)AssetType.Animation, rootFolder.ID);
            Assert.That(inventoryDataPlugin.getInventoryFolder(folder1Id), Is.Not.Null);
            
            // Test purge
            userInfo.PurgeFolder(rootFolder.ID);
            
            Assert.That(rootFolder.RequestListOfFolders(), Is.Empty);
            Assert.That(inventoryDataPlugin.getInventoryFolder(folder1Id), Is.Null);
        }
    }
}
