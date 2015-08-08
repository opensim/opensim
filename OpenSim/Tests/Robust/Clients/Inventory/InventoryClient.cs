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
using System.Text;
using System.Reflection;

using OpenMetaverse;
using NUnit.Framework;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors;

using OpenSim.Tests.Common;

namespace Robust.Tests
{
    [TestFixture]
    public class InventoryClient
    {
//        private static readonly ILog m_log =
//                LogManager.GetLogger(
//                MethodBase.GetCurrentMethod().DeclaringType);

        private UUID m_userID = new UUID("00000000-0000-0000-0000-333333333333");
        private UUID m_rootFolderID;
        private UUID m_notecardsFolder;
        private UUID m_objectsFolder;

        [Test]
        public void Inventory_001_CreateInventory()
        {
            TestHelpers.InMethod();
            XInventoryServicesConnector m_Connector = new XInventoryServicesConnector(DemonServer.Address);

            // Create an inventory that looks like this:
            //
            // /My Inventory
            //   <other system folders>
            //   /Objects
            //      Some Object
            //   /Notecards
            //      Notecard 1
            //      Notecard 2
            //   /Test Folder
            //      Link to notecard  -> /Notecards/Notecard 2
            //      Link to Objects folder -> /Objects

            bool success = m_Connector.CreateUserInventory(m_userID);
            Assert.IsTrue(success, "Failed to create user inventory");

            m_rootFolderID = m_Connector.GetRootFolder(m_userID).ID;
            Assert.AreNotEqual(m_rootFolderID, UUID.Zero, "Root folder ID must not be UUID.Zero");

            InventoryFolderBase of = m_Connector.GetFolderForType(m_userID, FolderType.Object);
            Assert.IsNotNull(of, "Failed to retrieve Objects folder");
            m_objectsFolder = of.ID;
            Assert.AreNotEqual(m_objectsFolder, UUID.Zero, "Objects folder ID must not be UUID.Zero");

            // Add an object
            InventoryItemBase item = new InventoryItemBase(new UUID("b0000000-0000-0000-0000-00000000000b"), m_userID);
            item.AssetID = UUID.Random();
            item.AssetType = (int)AssetType.Object;
            item.Folder = m_objectsFolder;
            item.Name = "Some Object";
            item.Description = string.Empty;
            success = m_Connector.AddItem(item);
            Assert.IsTrue(success, "Failed to add object to inventory");

            InventoryFolderBase ncf = m_Connector.GetFolderForType(m_userID, FolderType.Notecard);
            Assert.IsNotNull(of, "Failed to retrieve Notecards folder");
            m_notecardsFolder = ncf.ID;
            Assert.AreNotEqual(m_notecardsFolder, UUID.Zero, "Notecards folder ID must not be UUID.Zero");
            m_notecardsFolder = ncf.ID;

            // Add a notecard
            item = new InventoryItemBase(new UUID("10000000-0000-0000-0000-000000000001"), m_userID);
            item.AssetID = UUID.Random();
            item.AssetType = (int)AssetType.Notecard;
            item.Folder = m_notecardsFolder;
            item.Name = "Test Notecard 1";
            item.Description = string.Empty;
            success = m_Connector.AddItem(item);
            Assert.IsTrue(success, "Failed to add Notecard 1 to inventory");
            // Add another notecard
            item.ID = new UUID("20000000-0000-0000-0000-000000000002");
            item.AssetID = new UUID("a0000000-0000-0000-0000-00000000000a");
            item.Name = "Test Notecard 2";
            item.Description = string.Empty;
            success = m_Connector.AddItem(item);
            Assert.IsTrue(success, "Failed to add Notecard 2 to inventory");

            // Add a folder
            InventoryFolderBase folder = new InventoryFolderBase(new UUID("f0000000-0000-0000-0000-00000000000f"), "Test Folder", m_userID, m_rootFolderID);
            folder.Type = (int)FolderType.None;
            success = m_Connector.AddFolder(folder);
            Assert.IsTrue(success, "Failed to add Test Folder to inventory");

            // Add a link to notecard 2 in Test Folder
            item.AssetID = item.ID; // use item ID of notecard 2
            item.ID = new UUID("40000000-0000-0000-0000-000000000004");
            item.AssetType = (int)AssetType.Link;
            item.Folder = folder.ID;
            item.Name = "Link to notecard";
            item.Description = string.Empty;
            success = m_Connector.AddItem(item);
            Assert.IsTrue(success, "Failed to add link to notecard to inventory");

            // Add a link to the Objects folder in Test Folder
            item.AssetID = m_Connector.GetFolderForType(m_userID, FolderType.Object).ID; // use item ID of Objects folder
            item.ID = new UUID("50000000-0000-0000-0000-000000000005");
            item.AssetType = (int)AssetType.LinkFolder;
            item.Folder = folder.ID;
            item.Name = "Link to Objects folder";
            item.Description = string.Empty;
            success = m_Connector.AddItem(item);
            Assert.IsTrue(success, "Failed to add link to objects folder to inventory");

            InventoryCollection coll = m_Connector.GetFolderContent(m_userID, m_rootFolderID);
            Assert.IsNotNull(coll, "Failed to retrieve contents of root folder");
            Assert.Greater(coll.Folders.Count, 0, "Root folder does not have any subfolders");

            coll = m_Connector.GetFolderContent(m_userID, folder.ID);
            Assert.IsNotNull(coll, "Failed to retrieve contents of Test Folder");
            Assert.AreEqual(coll.Items.Count + coll.Folders.Count, 2, "Test Folder is expected to have exactly 2 things inside");

        }

        [Test]
        public void Inventory_002_MultipleItemsRequest()
        {
            TestHelpers.InMethod();
            XInventoryServicesConnector m_Connector = new XInventoryServicesConnector(DemonServer.Address);

            // Prefetch Notecard 1, will be cached from here on
            InventoryItemBase item = new InventoryItemBase(new UUID("10000000-0000-0000-0000-000000000001"), m_userID);
            item = m_Connector.GetItem(item);
            Assert.NotNull(item, "Failed to get Notecard 1");
            Assert.AreEqual("Test Notecard 1", item.Name, "Wrong name for Notecard 1");

            UUID[] uuids = new UUID[2];
            uuids[0] = item.ID;
            uuids[1] = new UUID("20000000-0000-0000-0000-000000000002");

            InventoryItemBase[] items = m_Connector.GetMultipleItems(m_userID, uuids);
            Assert.NotNull(items, "Failed to get multiple items");
            Assert.IsTrue(items.Length == 2, "Requested 2 items, but didn't receive 2 items");

            // Now they should both be cached
            items = m_Connector.GetMultipleItems(m_userID, uuids);
            Assert.NotNull(items, "(Repeat) Failed to get multiple items");
            Assert.IsTrue(items.Length == 2, "(Repeat) Requested 2 items, but didn't receive 2 items");

            // This item doesn't exist, but [0] does, and it's cached. 
            uuids[1] = new UUID("bb000000-0000-0000-0000-0000000000bb");
            // Fetching should return 2 items, but [1] should be null
            items = m_Connector.GetMultipleItems(m_userID, uuids);
            Assert.NotNull(items, "(Three times) Failed to get multiple items");
            Assert.IsTrue(items.Length == 2, "(Three times) Requested 2 items, but didn't receive 2 items");
            Assert.AreEqual("Test Notecard 1", items[0].Name, "(Three times) Wrong name for Notecard 1");
            Assert.IsNull(items[1], "(Three times) Expecting 2nd item to be null");

            // Now both don't exist 
            uuids[0] = new UUID("aa000000-0000-0000-0000-0000000000aa");
            items = m_Connector.GetMultipleItems(m_userID, uuids);
            Assert.Null(items[0], "Request to multiple non-existent items is supposed to return null [0]");
            Assert.Null(items[1], "Request to multiple non-existent items is supposed to return null [1]");

            // This item exists, and it's not cached
            uuids[1] = new UUID("b0000000-0000-0000-0000-00000000000b");
            // Fetching should return 2 items, but [0] should be null
            items = m_Connector.GetMultipleItems(m_userID, uuids);
            Assert.NotNull(items, "(Four times) Failed to get multiple items");
            Assert.IsTrue(items.Length == 2, "(Four times) Requested 2 items, but didn't receive 2 items");
            Assert.AreEqual("Some Object", items[1].Name, "(Four times) Wrong name for Some Object");
            Assert.IsNull(items[0], "(Four times) Expecting 1st item to be null");

        }
    }
}
