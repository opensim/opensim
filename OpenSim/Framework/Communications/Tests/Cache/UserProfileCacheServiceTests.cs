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
        /// Test moving an inventory folder
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
            
            userInfo.CreateFolder("testFolder", folderId, (ushort)AssetType.Animation, userInfo.RootFolder.ID);
            Assert.That(inventoryDataPlugin.getInventoryFolder(folderId), Is.Not.Null);
            Assert.That(userInfo.RootFolder.ContainsChildFolder(folderId), Is.True);
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
    }
}
