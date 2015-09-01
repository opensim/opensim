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
using System.Reflection;
using log4net.Config;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Avatar.Inventory.Transfer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Transfer.Tests
{
    [TestFixture]
    public class InventoryTransferModuleTests : OpenSimTestCase
    {    
        protected TestScene m_scene;       
            
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Messaging");
            config.Configs["Messaging"].Set("InventoryTransferModule", "InventoryTransferModule");

            m_scene = new SceneHelpers().SetupScene();            
            SceneHelpers.SetupSceneModules(m_scene, config, new InventoryTransferModule());
        } 

        [Test]
        public void TestAcceptGivenItem()
        {
//            TestHelpers.EnableLogging();

            UUID initialSessionId = TestHelpers.ParseTail(0x10);
            UUID itemId = TestHelpers.ParseTail(0x100);
            UUID assetId = TestHelpers.ParseTail(0x200);

            UserAccount ua1 
                = UserAccountHelpers.CreateUserWithInventory(m_scene, "User", "One", TestHelpers.ParseTail(0x1), "pw");
            UserAccount ua2 
                = UserAccountHelpers.CreateUserWithInventory(m_scene, "User", "Two", TestHelpers.ParseTail(0x2), "pw");

            ScenePresence giverSp = SceneHelpers.AddScenePresence(m_scene, ua1);
            TestClient giverClient = (TestClient)giverSp.ControllingClient;

            ScenePresence receiverSp = SceneHelpers.AddScenePresence(m_scene, ua2);
            TestClient receiverClient = (TestClient)receiverSp.ControllingClient;

            // Create the object to test give
            InventoryItemBase originalItem 
                = UserInventoryHelpers.CreateInventoryItem(
                    m_scene, "givenObj", itemId, assetId, giverSp.UUID, InventoryType.Object);

            byte[] giveImBinaryBucket = new byte[17];
            byte[] itemIdBytes = itemId.GetBytes();
            Array.Copy(itemIdBytes, 0, giveImBinaryBucket, 1, itemIdBytes.Length);

            GridInstantMessage giveIm 
                = new GridInstantMessage(
                    m_scene, 
                    giverSp.UUID, 
                    giverSp.Name, 
                    receiverSp.UUID, 
                    (byte)InstantMessageDialog.InventoryOffered,
                    false,
                    "inventory offered msg", 
                    initialSessionId,
                    false, 
                    Vector3.Zero,
                    giveImBinaryBucket,
                    true);

            giverClient.HandleImprovedInstantMessage(giveIm);

            // These details might not all be correct.  
            GridInstantMessage acceptIm 
                = new GridInstantMessage(
                    m_scene, 
                    receiverSp.UUID, 
                    receiverSp.Name, 
                    giverSp.UUID, 
                    (byte)InstantMessageDialog.InventoryAccepted, 
                    false,
                    "inventory accepted msg", 
                    initialSessionId,
                    false, 
                    Vector3.Zero,
                    null,
                    true);

            receiverClient.HandleImprovedInstantMessage(acceptIm);

            // Test for item remaining in the giver's inventory (here we assume a copy item)
            // TODO: Test no-copy items.
            InventoryItemBase originalItemAfterGive
                = UserInventoryHelpers.GetInventoryItem(m_scene.InventoryService, giverSp.UUID, "Objects/givenObj");

            Assert.That(originalItemAfterGive, Is.Not.Null);
            Assert.That(originalItemAfterGive.ID, Is.EqualTo(originalItem.ID));

            // Test for item successfully making it into the receiver's inventory
            InventoryItemBase receivedItem 
                = UserInventoryHelpers.GetInventoryItem(m_scene.InventoryService, receiverSp.UUID, "Objects/givenObj");

            Assert.That(receivedItem, Is.Not.Null);
            Assert.That(receivedItem.ID, Is.Not.EqualTo(originalItem.ID));

            // Test that on a delete, item still exists and is accessible for the giver.
            m_scene.InventoryService.DeleteItems(receiverSp.UUID, new List<UUID>() { receivedItem.ID });

            InventoryItemBase originalItemAfterDelete
                = UserInventoryHelpers.GetInventoryItem(m_scene.InventoryService, giverSp.UUID, "Objects/givenObj");

            Assert.That(originalItemAfterDelete, Is.Not.Null);

            // TODO: Test scenario where giver deletes their item first.
        }       

        /// <summary>
        /// Test user rejection of a given item.
        /// </summary>
        /// <remarks>
        /// A rejected item still ends up in the user's trash folder.
        /// </remarks>
        [Test]
        public void TestRejectGivenItem()
        {
//            TestHelpers.EnableLogging();

            UUID initialSessionId = TestHelpers.ParseTail(0x10);
            UUID itemId = TestHelpers.ParseTail(0x100);
            UUID assetId = TestHelpers.ParseTail(0x200);

            UserAccount ua1 
                = UserAccountHelpers.CreateUserWithInventory(m_scene, "User", "One", TestHelpers.ParseTail(0x1), "pw");
            UserAccount ua2 
                = UserAccountHelpers.CreateUserWithInventory(m_scene, "User", "Two", TestHelpers.ParseTail(0x2), "pw");

            ScenePresence giverSp = SceneHelpers.AddScenePresence(m_scene, ua1);
            TestClient giverClient = (TestClient)giverSp.ControllingClient;

            ScenePresence receiverSp = SceneHelpers.AddScenePresence(m_scene, ua2);
            TestClient receiverClient = (TestClient)receiverSp.ControllingClient;

            // Create the object to test give
            InventoryItemBase originalItem 
                = UserInventoryHelpers.CreateInventoryItem(
                    m_scene, "givenObj", itemId, assetId, giverSp.UUID, InventoryType.Object);

            GridInstantMessage receivedIm = null;
            receiverClient.OnReceivedInstantMessage += im => receivedIm = im;

            byte[] giveImBinaryBucket = new byte[17];
            byte[] itemIdBytes = itemId.GetBytes();
            Array.Copy(itemIdBytes, 0, giveImBinaryBucket, 1, itemIdBytes.Length);

            GridInstantMessage giveIm 
                = new GridInstantMessage(
                    m_scene, 
                    giverSp.UUID, 
                    giverSp.Name, 
                    receiverSp.UUID, 
                    (byte)InstantMessageDialog.InventoryOffered,
                    false,
                    "inventory offered msg", 
                    initialSessionId,
                    false, 
                    Vector3.Zero,
                    giveImBinaryBucket,
                    true);

            giverClient.HandleImprovedInstantMessage(giveIm);

            // These details might not all be correct.  
            // Session ID is now the created item ID (!)
            GridInstantMessage rejectIm 
                = new GridInstantMessage(
                    m_scene, 
                    receiverSp.UUID, 
                    receiverSp.Name, 
                    giverSp.UUID, 
                    (byte)InstantMessageDialog.InventoryDeclined, 
                    false,
                    "inventory declined msg", 
                    new UUID(receivedIm.imSessionID),
                    false, 
                    Vector3.Zero,
                    null,
                    true);

            receiverClient.HandleImprovedInstantMessage(rejectIm);

            // Test for item remaining in the giver's inventory (here we assume a copy item)
            // TODO: Test no-copy items.
            InventoryItemBase originalItemAfterGive
                = UserInventoryHelpers.GetInventoryItem(m_scene.InventoryService, giverSp.UUID, "Objects/givenObj");

            Assert.That(originalItemAfterGive, Is.Not.Null);
            Assert.That(originalItemAfterGive.ID, Is.EqualTo(originalItem.ID));

            // Test for item successfully making it into the receiver's inventory
            InventoryItemBase receivedItem 
                = UserInventoryHelpers.GetInventoryItem(m_scene.InventoryService, receiverSp.UUID, "Trash/givenObj");

            InventoryFolderBase trashFolder
                = m_scene.InventoryService.GetFolderForType(receiverSp.UUID, FolderType.Trash);

            Assert.That(receivedItem, Is.Not.Null);
            Assert.That(receivedItem.ID, Is.Not.EqualTo(originalItem.ID));
            Assert.That(receivedItem.Folder, Is.EqualTo(trashFolder.ID));

            // Test that on a delete, item still exists and is accessible for the giver.
            m_scene.InventoryService.PurgeFolder(trashFolder);

            InventoryItemBase originalItemAfterDelete
                = UserInventoryHelpers.GetInventoryItem(m_scene.InventoryService, giverSp.UUID, "Objects/givenObj");

            Assert.That(originalItemAfterDelete, Is.Not.Null);
        }  

        [Test]
        public void TestAcceptGivenFolder()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID initialSessionId = TestHelpers.ParseTail(0x10);
            UUID folderId = TestHelpers.ParseTail(0x100);

            UserAccount ua1 
                = UserAccountHelpers.CreateUserWithInventory(m_scene, "User", "One", TestHelpers.ParseTail(0x1), "pw");
            UserAccount ua2 
                = UserAccountHelpers.CreateUserWithInventory(m_scene, "User", "Two", TestHelpers.ParseTail(0x2), "pw");

            ScenePresence giverSp = SceneHelpers.AddScenePresence(m_scene, ua1);
            TestClient giverClient = (TestClient)giverSp.ControllingClient;

            ScenePresence receiverSp = SceneHelpers.AddScenePresence(m_scene, ua2);
            TestClient receiverClient = (TestClient)receiverSp.ControllingClient;

            InventoryFolderBase originalFolder 
                = UserInventoryHelpers.CreateInventoryFolder(
                    m_scene.InventoryService, giverSp.UUID, folderId, "f1", true);

            byte[] giveImBinaryBucket = new byte[17];
            giveImBinaryBucket[0] = (byte)AssetType.Folder;
            byte[] itemIdBytes = folderId.GetBytes();
            Array.Copy(itemIdBytes, 0, giveImBinaryBucket, 1, itemIdBytes.Length);

            GridInstantMessage giveIm 
                = new GridInstantMessage(
                    m_scene, 
                    giverSp.UUID, 
                    giverSp.Name, 
                    receiverSp.UUID, 
                    (byte)InstantMessageDialog.InventoryOffered,
                    false,
                    "inventory offered msg", 
                    initialSessionId,
                    false, 
                    Vector3.Zero,
                    giveImBinaryBucket,
                    true);

            giverClient.HandleImprovedInstantMessage(giveIm);

            // These details might not all be correct.  
            GridInstantMessage acceptIm 
                = new GridInstantMessage(
                    m_scene, 
                    receiverSp.UUID, 
                    receiverSp.Name, 
                    giverSp.UUID, 
                    (byte)InstantMessageDialog.InventoryAccepted, 
                    false,
                    "inventory accepted msg", 
                    initialSessionId,
                    false, 
                    Vector3.Zero,
                    null,
                    true);

            receiverClient.HandleImprovedInstantMessage(acceptIm);

            // Test for item remaining in the giver's inventory (here we assume a copy item)
            // TODO: Test no-copy items.
            InventoryFolderBase originalFolderAfterGive
                = UserInventoryHelpers.GetInventoryFolder(m_scene.InventoryService, giverSp.UUID, "f1");

            Assert.That(originalFolderAfterGive, Is.Not.Null);
            Assert.That(originalFolderAfterGive.ID, Is.EqualTo(originalFolder.ID));

            // Test for item successfully making it into the receiver's inventory
            InventoryFolderBase receivedFolder 
                = UserInventoryHelpers.GetInventoryFolder(m_scene.InventoryService, receiverSp.UUID, "f1");

            Assert.That(receivedFolder, Is.Not.Null);
            Assert.That(receivedFolder.ID, Is.Not.EqualTo(originalFolder.ID));

            // Test that on a delete, item still exists and is accessible for the giver.
            m_scene.InventoryService.DeleteFolders(receiverSp.UUID, new List<UUID>() { receivedFolder.ID });

            InventoryFolderBase originalFolderAfterDelete
                = UserInventoryHelpers.GetInventoryFolder(m_scene.InventoryService, giverSp.UUID, "f1");

            Assert.That(originalFolderAfterDelete, Is.Not.Null);

            // TODO: Test scenario where giver deletes their item first.
        }       

        /// <summary>
        /// Test user rejection of a given item.
        /// </summary>
        /// <remarks>
        /// A rejected item still ends up in the user's trash folder.
        /// </remarks>
        [Test]
        public void TestRejectGivenFolder()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID initialSessionId = TestHelpers.ParseTail(0x10);
            UUID folderId = TestHelpers.ParseTail(0x100);

            UserAccount ua1 
                = UserAccountHelpers.CreateUserWithInventory(m_scene, "User", "One", TestHelpers.ParseTail(0x1), "pw");
            UserAccount ua2 
                = UserAccountHelpers.CreateUserWithInventory(m_scene, "User", "Two", TestHelpers.ParseTail(0x2), "pw");

            ScenePresence giverSp = SceneHelpers.AddScenePresence(m_scene, ua1);
            TestClient giverClient = (TestClient)giverSp.ControllingClient;

            ScenePresence receiverSp = SceneHelpers.AddScenePresence(m_scene, ua2);
            TestClient receiverClient = (TestClient)receiverSp.ControllingClient;

            // Create the folder to test give
            InventoryFolderBase originalFolder 
                = UserInventoryHelpers.CreateInventoryFolder(
                    m_scene.InventoryService, giverSp.UUID, folderId, "f1", true);

            GridInstantMessage receivedIm = null;
            receiverClient.OnReceivedInstantMessage += im => receivedIm = im;

            byte[] giveImBinaryBucket = new byte[17];
            giveImBinaryBucket[0] = (byte)AssetType.Folder;
            byte[] itemIdBytes = folderId.GetBytes();
            Array.Copy(itemIdBytes, 0, giveImBinaryBucket, 1, itemIdBytes.Length);

            GridInstantMessage giveIm 
                = new GridInstantMessage(
                    m_scene, 
                    giverSp.UUID, 
                    giverSp.Name, 
                    receiverSp.UUID, 
                    (byte)InstantMessageDialog.InventoryOffered,
                    false,
                    "inventory offered msg", 
                    initialSessionId,
                    false, 
                    Vector3.Zero,
                    giveImBinaryBucket,
                    true);

            giverClient.HandleImprovedInstantMessage(giveIm);

            // These details might not all be correct.  
            // Session ID is now the created item ID (!)
            GridInstantMessage rejectIm 
                = new GridInstantMessage(
                    m_scene, 
                    receiverSp.UUID, 
                    receiverSp.Name, 
                    giverSp.UUID, 
                    (byte)InstantMessageDialog.InventoryDeclined, 
                    false,
                    "inventory declined msg", 
                    new UUID(receivedIm.imSessionID),
                    false, 
                    Vector3.Zero,
                    null,
                    true);

            receiverClient.HandleImprovedInstantMessage(rejectIm);

            // Test for item remaining in the giver's inventory (here we assume a copy item)
            // TODO: Test no-copy items.
            InventoryFolderBase originalFolderAfterGive
                = UserInventoryHelpers.GetInventoryFolder(m_scene.InventoryService, giverSp.UUID, "f1");

            Assert.That(originalFolderAfterGive, Is.Not.Null);
            Assert.That(originalFolderAfterGive.ID, Is.EqualTo(originalFolder.ID));

            // Test for folder successfully making it into the receiver's inventory
            InventoryFolderBase receivedFolder 
                = UserInventoryHelpers.GetInventoryFolder(m_scene.InventoryService, receiverSp.UUID, "Trash/f1");

            InventoryFolderBase trashFolder
                = m_scene.InventoryService.GetFolderForType(receiverSp.UUID, FolderType.Trash);

            Assert.That(receivedFolder, Is.Not.Null);
            Assert.That(receivedFolder.ID, Is.Not.EqualTo(originalFolder.ID));
            Assert.That(receivedFolder.ParentID, Is.EqualTo(trashFolder.ID));

            // Test that on a delete, item still exists and is accessible for the giver.
            m_scene.InventoryService.PurgeFolder(trashFolder);

            InventoryFolderBase originalFolderAfterDelete
                = UserInventoryHelpers.GetInventoryFolder(m_scene.InventoryService, giverSp.UUID, "f1");

            Assert.That(originalFolderAfterDelete, Is.Not.Null);
        }  
    }
}