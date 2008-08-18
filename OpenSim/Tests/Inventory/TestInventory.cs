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
using System.Text;
using NUnit.Framework;

using libsecondlife;
using OpenSim.Framework.Types;
using OpenSim.Data;
using OpenSim.Data.SQLite;
using OpenSim.Data.MySQL;
using OpenSim.Framework.Console;

namespace OpenSim.Test.Inventory
{
    [TestFixture]
    public class TestInventory
    {
        IInventoryDataPlugin _dbPlugin;
        LLUUID _agent_1_id;
        public static LLUUID LibraryFolderRootUuid = new LLUUID("5926de2a-c2d7-4c11-ac4e-74512ffeb6d1");

        Random _rnd = new Random((int)DateTime.Now.Ticks);

        [TestFixtureSetUp]
        public void SetupInventoryTest()
        {
            _agent_1_id = LLUUID.Random();

            MainConsole.Instance = new ConsoleBase("TEST", null);

//            _dbPlugin = new SQLiteInventoryStore();
            _dbPlugin = new MySQLInventoryData();
            _dbPlugin.Initialise();
        }

        [TestFixtureTearDown]
        public void TeardownInventoryTest()
        {
            _dbPlugin.Close();
        }

        private bool AreFoldersIdentical(InventoryFolderBase a, InventoryFolderBase b)
        {
            if (a.agentID != b.agentID) return false;
            if (a.folderID != b.folderID) return false;
            if (a.name != b.name) return false;
            if (a.parentID != b.parentID) return false;
            if (a.type != b.type) return false;
            if (a.version != b.version) return false;
            return true;
        }

        private bool AreItemsIdentical(InventoryItemBase a, InventoryItemBase b)
        {
            if (a.assetID != b.assetID) return false;
            if (a.assetType != b.assetType) return false;
            if (a.avatarID != b.avatarID) return false;
            if (a.creatorsID != b.creatorsID) return false;
            if (a.inventoryBasePermissions != b.inventoryBasePermissions) return false;
            if (a.inventoryCurrentPermissions != b.inventoryCurrentPermissions) return false;
            if (a.inventoryEveryOnePermissions != b.inventoryEveryOnePermissions) return false;
            if (a.inventoryNextPermissions != b.inventoryNextPermissions) return false;
            if (a.inventoryID != b.inventoryID) return false;
            if (a.inventoryDescription != b.inventoryDescription) return false;
            if (a.inventoryName != b.inventoryName) return false;
            if (a.invType != b.invType) return false;
            if (a.parentFolderID != b.parentFolderID) return false;
            return true;
        }

        /// <summary>
        /// Test that we can insert a root folder
        /// </summary>
        [Test]
        public void T01_SetupRootFolder()
        {
            InventoryFolderBase root = new InventoryFolderBase();
            root.agentID = _agent_1_id;
            root.folderID = _agent_1_id;
            root.name = "Root folder";
            root.parentID = LLUUID.Zero;
            root.type = 2;
            root.version = 2;
            _dbPlugin.addInventoryFolder(root);

            InventoryFolderBase f = _dbPlugin.getInventoryFolder(root.folderID);
            Assert.IsNotNull(f, "Failed to get existing folder");
            Assert.IsTrue(AreFoldersIdentical(root, f), "Difference between stored and retrieved folder data");

            // Test that we only get the root folder, based on it's id, i.e. any other gui will not find the folder
            f = _dbPlugin.getInventoryFolder(LLUUID.Zero);
            Assert.IsNull(f, "get folder returned a folder, but shouldn't find one");

            f = _dbPlugin.getInventoryFolder(LLUUID.Random());
            Assert.IsNull(f, "get folder returned a folder, but shouldn't find one");

            // test that we can delete the folder

            _dbPlugin.deleteInventoryFolder(_agent_1_id);
            f = _dbPlugin.getInventoryFolder(root.folderID);
            Assert.IsNull(f, "get folder returned a folder, but it should have been deleted");
        }

        /// <summary>
        /// Make sure that all folders reported as root folders are root folders
        /// </summary>
        [Test]
        public void T02_TestRootFolder()
        {
            InventoryFolderBase root = new InventoryFolderBase();
            root.agentID = _agent_1_id;
            root.folderID = _agent_1_id;
            root.name = "Root folder";
            root.parentID = LLUUID.Zero;
            root.type = 2;
            root.version = 2;
            _dbPlugin.addInventoryFolder(root);

            List<InventoryFolderBase> folders = _dbPlugin.getUserRootFolders(_agent_1_id);
            Assert.IsNotNull(folders, "Failed to get rootfolders for user");

            bool foundRoot = false;
            foreach (InventoryFolderBase f in folders)
            {
                // a root folder has a zero valued LLUUID
                Assert.AreEqual(f.parentID, LLUUID.Zero, "non root folder returned");

                if (f.agentID == root.agentID)
                {
                    // we cannot have two different user specific root folders
                    Assert.IsFalse(foundRoot, "Two different user specific root folders returned");

                    Assert.IsTrue(AreFoldersIdentical(root, f), "Difference between stored and retrieved folder data");
                    foundRoot = false;
                }
            }
            _dbPlugin.deleteInventoryFolder(_agent_1_id);
        }

        /// <summary>
        /// Test of folder hierarchy
        /// </summary>
        [Test]
        public void T03_TestRootFolder()
        {
            InventoryFolderBase root = new InventoryFolderBase();
            root.agentID = _agent_1_id;
            root.folderID = _agent_1_id;
            root.name = "Root folder";
            root.parentID = LLUUID.Zero;
            root.type = 2;
            root.version = 2;
            _dbPlugin.addInventoryFolder(root);

            List<InventoryFolderBase> folders = _dbPlugin.getInventoryFolders(_agent_1_id);
            Assert.IsNotNull(folders, "null was returned, but an empty list of subfolders were expected");
            Assert.IsEmpty(folders, "non empty collection was returned, even though the list of sub-folders should be empty");

            InventoryFolderBase child1 = new InventoryFolderBase();
            child1.agentID = _agent_1_id;
            child1.folderID = LLUUID.Random();
            child1.name = "Child 1";
            child1.parentID = root.folderID;
            child1.type = 3;
            child1.version = 3;
            _dbPlugin.addInventoryFolder(child1);

            InventoryFolderBase child2 = new InventoryFolderBase();
            child2.agentID = _agent_1_id;
            child2.folderID = LLUUID.Random();
            child2.name = "Child 2";
            child2.parentID = root.folderID;
            child2.type = 4;
            child2.version = 4;
            _dbPlugin.addInventoryFolder(child2);

            folders = _dbPlugin.getInventoryFolders(_agent_1_id);
            Assert.IsNotNull(folders, "null was returned, but an empty list of subfolders were expected");
            Assert.AreEqual(folders.Count, 2, "two children were reported as inserted into the root folder");

            bool foundChild1 = false;
            bool foundChild2 = false;

            foreach (InventoryFolderBase f in folders)
            {
                if (f.folderID == child1.folderID)
                {
                    Assert.IsTrue(AreFoldersIdentical(child1, f), "Difference between stored and retrieved folder data");
                    foundChild1 = true;
                }
                else if (f.folderID == child2.folderID)
                {
                    Assert.IsTrue(AreFoldersIdentical(child2, f), "Difference between stored and retrieved folder data");
                    foundChild2 = true;
                }
                else
                {
                    Assert.Fail("found unknown child folder");
                }
            }

            if (foundChild1 == false || foundChild2 == false)
            {
                Assert.Fail("one of the two child folders was not returned");
            }

            _dbPlugin.deleteInventoryFolder(child2.folderID);
            _dbPlugin.deleteInventoryFolder(child1.folderID);
            _dbPlugin.deleteInventoryFolder(_agent_1_id);
        }

        /// <summary>
        /// Test of folder hierarchy, and deletion
        /// </summary>
        [Test]
        public void T04_TestRootFolder()
        {
            InventoryFolderBase root = new InventoryFolderBase();
            root.agentID = _agent_1_id;
            root.folderID = _agent_1_id;
            root.name = "Root folder";
            root.parentID = LLUUID.Zero;
            root.type = 2;
            root.version = 2;
            _dbPlugin.addInventoryFolder(root);

            InventoryFolderBase child1 = new InventoryFolderBase();
            child1.agentID = _agent_1_id;
            child1.folderID = LLUUID.Random();
            child1.name = "Child 1";
            child1.parentID = root.folderID;
            child1.type = 3;
            child1.version = 3;
            _dbPlugin.addInventoryFolder(child1);

            InventoryFolderBase child2 = new InventoryFolderBase();
            child2.agentID = _agent_1_id;
            child2.folderID = LLUUID.Random();
            child2.name = "Child 2";
            child2.parentID = root.folderID;
            child2.type = 4;
            child2.version = 4;
            _dbPlugin.addInventoryFolder(child2);

            _dbPlugin.deleteInventoryFolder(_agent_1_id);

            List<InventoryFolderBase> folders = _dbPlugin.getInventoryFolders(_agent_1_id);
            Assert.IsNotNull(folders, "null was returned, but an empty list of subfolders were expected");
            Assert.IsEmpty(folders, "non empty collection was returned, even though the list of sub-folders should be empty");

            InventoryFolderBase f = _dbPlugin.getInventoryFolder(_agent_1_id);
            Assert.IsNull(f, "Folder was returned, even though is has been deleted");

            f = _dbPlugin.getInventoryFolder(child1.folderID);
            Assert.IsNull(f, "Folder was returned, even though is has been deleted");

            f = _dbPlugin.getInventoryFolder(child2.folderID);
            Assert.IsNull(f, "Folder was returned, even though is has been deleted");
        }

        /// <summary>
        /// Folder update
        /// </summary>
        [Test]
        public void T05_UpdateFolder()
        {
            InventoryFolderBase root = new InventoryFolderBase();
            root.agentID = _agent_1_id;
            root.folderID = _agent_1_id;
            root.name = "Root folder";
            root.parentID = LLUUID.Zero;
            root.type = 2;
            root.version = 2;
            _dbPlugin.addInventoryFolder(root);

            InventoryFolderBase f = _dbPlugin.getInventoryFolder(_agent_1_id);
            Assert.IsTrue(AreFoldersIdentical(root, f), "Difference between stored and retrieved folder data");

            root.agentID = LLUUID.Random();
            _dbPlugin.updateInventoryFolder(root);
            f = _dbPlugin.getInventoryFolder(root.folderID);
            Assert.IsTrue(AreFoldersIdentical(root, f), "Difference between stored and retrieved folder data");

            root.folderID = LLUUID.Random();
            _dbPlugin.updateInventoryFolder(root);
            f = _dbPlugin.getInventoryFolder(root.folderID);
            Assert.IsTrue(AreFoldersIdentical(root, f), "Difference between stored and retrieved folder data");

            root.name = "Root folder 2";
            _dbPlugin.updateInventoryFolder(root);
            f = _dbPlugin.getInventoryFolder(root.folderID);
            Assert.IsTrue(AreFoldersIdentical(root, f), "Difference between stored and retrieved folder data");

            root.parentID = LLUUID.Random();
            _dbPlugin.updateInventoryFolder(root);
            f = _dbPlugin.getInventoryFolder(root.folderID);
            Assert.IsTrue(AreFoldersIdentical(root, f), "Difference between stored and retrieved folder data");

            root.type = (short)(root.type + 1);
            _dbPlugin.updateInventoryFolder(root);
            f = _dbPlugin.getInventoryFolder(root.folderID);
            Assert.IsTrue(AreFoldersIdentical(root, f), "Difference between stored and retrieved folder data");

            root.version = (ushort)(root.version + 1);
            _dbPlugin.updateInventoryFolder(root);
            f = _dbPlugin.getInventoryFolder(root.folderID);
            Assert.IsTrue(AreFoldersIdentical(root, f), "Difference between stored and retrieved folder data");

            _dbPlugin.deleteInventoryFolder(_agent_1_id);
            _dbPlugin.deleteInventoryFolder(root.folderID);
        }

        /// <summary>
        /// Test that we can insert a root folder
        /// </summary>
        [Test]
        public void T06_SetupInventoryWithItems()
        {

            // Setup inventory
            InventoryFolderBase root = new InventoryFolderBase();
            root.agentID = _agent_1_id;
            root.folderID = _agent_1_id;
            root.name = "Root folder";
            root.parentID = LLUUID.Zero;
            root.type = 2;
            root.version = 2;
            _dbPlugin.addInventoryFolder(root);

            InventoryFolderBase child1 = new InventoryFolderBase();
            child1.agentID = _agent_1_id;
            child1.folderID = LLUUID.Random();
            child1.name = "Child 1";
            child1.parentID = root.folderID;
            child1.type = 3;
            child1.version = 3;
            _dbPlugin.addInventoryFolder(child1);

            InventoryFolderBase child1Child = new InventoryFolderBase();
            child1Child.agentID = _agent_1_id;
            child1Child.folderID = LLUUID.Random();
            child1Child.name = "Child 1 child";
            child1Child.parentID = child1.folderID;
            child1Child.type = 4;
            child1Child.version = 4;
            _dbPlugin.addInventoryFolder(child1Child);

            InventoryFolderBase child2 = new InventoryFolderBase();
            child2.agentID = _agent_1_id;
            child2.folderID = LLUUID.Random();
            child2.name = "Child 2";
            child2.parentID = root.folderID;
            child2.type = 5;
            child2.version = 5;
            _dbPlugin.addInventoryFolder(child2);

            InventoryFolderBase child2Child = new InventoryFolderBase();
            child2Child.agentID = _agent_1_id;
            child2Child.folderID = LLUUID.Random();
            child2Child.name = "Child 2 child";
            child2Child.parentID = child2.folderID;
            child2Child.type = 6;
            child2Child.version = 6;
            _dbPlugin.addInventoryFolder(child2Child);

            InventoryItemBase rootItem = new InventoryItemBase();
            rootItem.assetID = LLUUID.Random();
            rootItem.assetType = _rnd.Next(1, 1000);
            rootItem.avatarID = _agent_1_id;
            rootItem.creatorsID = LLUUID.Random();
            rootItem.inventoryBasePermissions = (uint)_rnd.Next(1,1000000);
            rootItem.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            rootItem.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            rootItem.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            rootItem.inventoryID = LLUUID.Random();
            rootItem.inventoryDescription = "Root item, Description";
            rootItem.inventoryName = "Root item";
            rootItem.invType = _rnd.Next(1, 1000);
            rootItem.parentFolderID = root.folderID;
            _dbPlugin.addInventoryItem(rootItem);

            InventoryItemBase child1Item = new InventoryItemBase();
            child1Item.assetID = LLUUID.Random();
            child1Item.assetType = _rnd.Next(1, 1000);
            child1Item.avatarID = _agent_1_id;
            child1Item.creatorsID = LLUUID.Random();
            child1Item.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            child1Item.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            child1Item.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            child1Item.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            child1Item.inventoryID = LLUUID.Random();
            child1Item.inventoryDescription = "child1 item, Description";
            child1Item.inventoryName = "child1 item";
            child1Item.invType = _rnd.Next(1, 1000);
            child1Item.parentFolderID = child1.folderID;
            _dbPlugin.addInventoryItem(child1Item);

            InventoryItemBase child1ChildItem = new InventoryItemBase();
            child1ChildItem.assetID = LLUUID.Random();
            child1ChildItem.assetType = _rnd.Next(1, 1000);
            child1ChildItem.avatarID = _agent_1_id;
            child1ChildItem.creatorsID = LLUUID.Random();
            child1ChildItem.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            child1ChildItem.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            child1ChildItem.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            child1ChildItem.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            child1ChildItem.inventoryID = LLUUID.Random();
            child1ChildItem.inventoryDescription = "child1Child item, Description";
            child1ChildItem.inventoryName = "child1Child item";
            child1ChildItem.invType = _rnd.Next(1, 1000);
            child1ChildItem.parentFolderID = child1Child.folderID;
            _dbPlugin.addInventoryItem(child1ChildItem);

            InventoryItemBase child2Item = new InventoryItemBase();
            child2Item.assetID = LLUUID.Random();
            child2Item.assetType = _rnd.Next(1, 1000);
            child2Item.avatarID = _agent_1_id;
            child2Item.creatorsID = LLUUID.Random();
            child2Item.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            child2Item.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            child2Item.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            child2Item.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            child2Item.inventoryID = LLUUID.Random();
            child2Item.inventoryDescription = "child2 item, Description";
            child2Item.inventoryName = "child2 item";
            child2Item.invType = _rnd.Next(1, 1000);
            child2Item.parentFolderID = child2.folderID;
            _dbPlugin.addInventoryItem(child2Item);

            InventoryItemBase child2ChildItem = new InventoryItemBase();
            child2ChildItem.assetID = LLUUID.Random();
            child2ChildItem.assetType = _rnd.Next(1, 1000);
            child2ChildItem.avatarID = _agent_1_id;
            child2ChildItem.creatorsID = LLUUID.Random();
            child2ChildItem.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            child2ChildItem.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            child2ChildItem.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            child2ChildItem.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            child2ChildItem.inventoryID = LLUUID.Random();
            child2ChildItem.inventoryDescription = "child2Child item, Description";
            child2ChildItem.inventoryName = "child2Child item";
            child2ChildItem.invType = _rnd.Next(1, 1000);
            child2ChildItem.parentFolderID = child2Child.folderID;
            _dbPlugin.addInventoryItem(child2ChildItem);

            // test read of items

            InventoryItemBase item = _dbPlugin.getInventoryItem(rootItem.inventoryID);
            Assert.IsTrue(AreItemsIdentical(rootItem, item));

            item = _dbPlugin.getInventoryItem(child1Item.inventoryID);
            Assert.IsTrue(AreItemsIdentical(child1Item, item));

            item = _dbPlugin.getInventoryItem(child1ChildItem.inventoryID);
            Assert.IsTrue(AreItemsIdentical(child1ChildItem, item));

            item = _dbPlugin.getInventoryItem(child2Item.inventoryID);
            Assert.IsTrue(AreItemsIdentical(child2Item, item));

            item = _dbPlugin.getInventoryItem(child2ChildItem.inventoryID);
            Assert.IsTrue(AreItemsIdentical(child2ChildItem, item));

            _dbPlugin.deleteInventoryItem(rootItem.inventoryID);
            _dbPlugin.deleteInventoryItem(child1Item.inventoryID);
            _dbPlugin.deleteInventoryItem(child1ChildItem.inventoryID);
            _dbPlugin.deleteInventoryItem(child2Item.inventoryID);
            _dbPlugin.deleteInventoryItem(child2ChildItem.inventoryID);
        }

        /// <summary>
        /// Test that we can insert a root folder
        /// </summary>
        [Test]
        public void T07_DeleteInventoryWithItems()
        {

            // Setup inventory
            InventoryFolderBase root = new InventoryFolderBase();
            root.agentID = _agent_1_id;
            root.folderID = _agent_1_id;
            root.name = "Root folder";
            root.parentID = LLUUID.Zero;
            root.type = 2;
            root.version = 2;
            _dbPlugin.addInventoryFolder(root);

            InventoryFolderBase child1 = new InventoryFolderBase();
            child1.agentID = _agent_1_id;
            child1.folderID = LLUUID.Random();
            child1.name = "Child 1";
            child1.parentID = root.folderID;
            child1.type = 3;
            child1.version = 3;
            _dbPlugin.addInventoryFolder(child1);

            InventoryFolderBase child1Child = new InventoryFolderBase();
            child1Child.agentID = _agent_1_id;
            child1Child.folderID = LLUUID.Random();
            child1Child.name = "Child 1 child";
            child1Child.parentID = child1.folderID;
            child1Child.type = 4;
            child1Child.version = 4;
            _dbPlugin.addInventoryFolder(child1Child);

            InventoryFolderBase child2 = new InventoryFolderBase();
            child2.agentID = _agent_1_id;
            child2.folderID = LLUUID.Random();
            child2.name = "Child 2";
            child2.parentID = root.folderID;
            child2.type = 5;
            child2.version = 5;
            _dbPlugin.addInventoryFolder(child2);

            InventoryFolderBase child2Child = new InventoryFolderBase();
            child2Child.agentID = _agent_1_id;
            child2Child.folderID = LLUUID.Random();
            child2Child.name = "Child 2 child";
            child2Child.parentID = child2.folderID;
            child2Child.type = 6;
            child2Child.version = 6;
            _dbPlugin.addInventoryFolder(child2Child);

            InventoryItemBase rootItem = new InventoryItemBase();
            rootItem.assetID = LLUUID.Random();
            rootItem.assetType = _rnd.Next(1, 1000);
            rootItem.avatarID = _agent_1_id;
            rootItem.creatorsID = LLUUID.Random();
            rootItem.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            rootItem.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            rootItem.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            rootItem.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            rootItem.inventoryID = LLUUID.Random();
            rootItem.inventoryDescription = "Root item, Description";
            rootItem.inventoryName = "Root item";
            rootItem.invType = _rnd.Next(1, 1000);
            rootItem.parentFolderID = root.folderID;
            _dbPlugin.addInventoryItem(rootItem);

            InventoryItemBase child1Item = new InventoryItemBase();
            child1Item.assetID = LLUUID.Random();
            child1Item.assetType = _rnd.Next(1, 1000);
            child1Item.avatarID = _agent_1_id;
            child1Item.creatorsID = LLUUID.Random();
            child1Item.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            child1Item.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            child1Item.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            child1Item.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            child1Item.inventoryID = LLUUID.Random();
            child1Item.inventoryDescription = "child1 item, Description";
            child1Item.inventoryName = "child1 item";
            child1Item.invType = _rnd.Next(1, 1000);
            child1Item.parentFolderID = child1.folderID;
            _dbPlugin.addInventoryItem(child1Item);

            InventoryItemBase child1ChildItem = new InventoryItemBase();
            child1ChildItem.assetID = LLUUID.Random();
            child1ChildItem.assetType = _rnd.Next(1, 1000);
            child1ChildItem.avatarID = _agent_1_id;
            child1ChildItem.creatorsID = LLUUID.Random();
            child1ChildItem.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            child1ChildItem.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            child1ChildItem.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            child1ChildItem.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            child1ChildItem.inventoryID = LLUUID.Random();
            child1ChildItem.inventoryDescription = "child1Child item, Description";
            child1ChildItem.inventoryName = "child1Child item";
            child1ChildItem.invType = _rnd.Next(1, 1000);
            child1ChildItem.parentFolderID = child1Child.folderID;
            _dbPlugin.addInventoryItem(child1ChildItem);

            InventoryItemBase child2Item = new InventoryItemBase();
            child2Item.assetID = LLUUID.Random();
            child2Item.assetType = _rnd.Next(1, 1000);
            child2Item.avatarID = _agent_1_id;
            child2Item.creatorsID = LLUUID.Random();
            child2Item.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            child2Item.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            child2Item.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            child2Item.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            child2Item.inventoryID = LLUUID.Random();
            child2Item.inventoryDescription = "child2 item, Description";
            child2Item.inventoryName = "child2 item";
            child2Item.invType = _rnd.Next(1, 1000);
            child2Item.parentFolderID = child2.folderID;
            _dbPlugin.addInventoryItem(child2Item);

            InventoryItemBase child2ChildItem = new InventoryItemBase();
            child2ChildItem.assetID = LLUUID.Random();
            child2ChildItem.assetType = _rnd.Next(1, 1000);
            child2ChildItem.avatarID = _agent_1_id;
            child2ChildItem.creatorsID = LLUUID.Random();
            child2ChildItem.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            child2ChildItem.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            child2ChildItem.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            child2ChildItem.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            child2ChildItem.inventoryID = LLUUID.Random();
            child2ChildItem.inventoryDescription = "child2Child item, Description";
            child2ChildItem.inventoryName = "child2Child item";
            child2ChildItem.invType = _rnd.Next(1, 1000);
            child2ChildItem.parentFolderID = child2Child.folderID;
            _dbPlugin.addInventoryItem(child2ChildItem);

            // test deletetion of items

            _dbPlugin.deleteInventoryItem(rootItem.inventoryID);
            _dbPlugin.deleteInventoryItem(child1Item.inventoryID);
            _dbPlugin.deleteInventoryItem(child1ChildItem.inventoryID);
            _dbPlugin.deleteInventoryItem(child2Item.inventoryID);
            _dbPlugin.deleteInventoryItem(child2ChildItem.inventoryID);

            InventoryItemBase item = _dbPlugin.getInventoryItem(rootItem.inventoryID);
            Assert.IsNull(item);

            item = _dbPlugin.getInventoryItem(child1Item.inventoryID);
            Assert.IsNull(item);

            item = _dbPlugin.getInventoryItem(child1ChildItem.inventoryID);
            Assert.IsNull(item);

            item = _dbPlugin.getInventoryItem(child2Item.inventoryID);
            Assert.IsNull(item);

            item = _dbPlugin.getInventoryItem(child2ChildItem.inventoryID);
            Assert.IsNull(item);

            _dbPlugin.deleteInventoryFolder(_agent_1_id);
        }


        /// <summary>
        /// Test that we can insert a root folder
        /// </summary>
        [Test]
        public void T08_DeleteInventoryWithItems()
        {

            // Setup inventory
            InventoryFolderBase root = new InventoryFolderBase();
            root.agentID = _agent_1_id;
            root.folderID = _agent_1_id;
            root.name = "Root folder";
            root.parentID = LLUUID.Zero;
            root.type = 2;
            root.version = 2;
            _dbPlugin.addInventoryFolder(root);

            InventoryFolderBase child1 = new InventoryFolderBase();
            child1.agentID = _agent_1_id;
            child1.folderID = LLUUID.Random();
            child1.name = "Child 1";
            child1.parentID = root.folderID;
            child1.type = 3;
            child1.version = 3;
            _dbPlugin.addInventoryFolder(child1);

            InventoryFolderBase child1Child = new InventoryFolderBase();
            child1Child.agentID = _agent_1_id;
            child1Child.folderID = LLUUID.Random();
            child1Child.name = "Child 1 child";
            child1Child.parentID = child1.folderID;
            child1Child.type = 4;
            child1Child.version = 4;
            _dbPlugin.addInventoryFolder(child1Child);

            InventoryFolderBase child2 = new InventoryFolderBase();
            child2.agentID = _agent_1_id;
            child2.folderID = LLUUID.Random();
            child2.name = "Child 2";
            child2.parentID = root.folderID;
            child2.type = 5;
            child2.version = 5;
            _dbPlugin.addInventoryFolder(child2);

            InventoryFolderBase child2Child = new InventoryFolderBase();
            child2Child.agentID = _agent_1_id;
            child2Child.folderID = LLUUID.Random();
            child2Child.name = "Child 2 child";
            child2Child.parentID = child2.folderID;
            child2Child.type = 6;
            child2Child.version = 6;
            _dbPlugin.addInventoryFolder(child2Child);

            InventoryItemBase rootItem = new InventoryItemBase();
            rootItem.assetID = LLUUID.Random();
            rootItem.assetType = _rnd.Next(1, 1000);
            rootItem.avatarID = _agent_1_id;
            rootItem.creatorsID = LLUUID.Random();
            rootItem.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            rootItem.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            rootItem.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            rootItem.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            rootItem.inventoryID = LLUUID.Random();
            rootItem.inventoryDescription = "Root item, Description";
            rootItem.inventoryName = "Root item";
            rootItem.invType = _rnd.Next(1, 1000);
            rootItem.parentFolderID = root.folderID;
            _dbPlugin.addInventoryItem(rootItem);

            InventoryItemBase child1Item = new InventoryItemBase();
            child1Item.assetID = LLUUID.Random();
            child1Item.assetType = _rnd.Next(1, 1000);
            child1Item.avatarID = _agent_1_id;
            child1Item.creatorsID = LLUUID.Random();
            child1Item.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            child1Item.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            child1Item.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            child1Item.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            child1Item.inventoryID = LLUUID.Random();
            child1Item.inventoryDescription = "child1 item, Description";
            child1Item.inventoryName = "child1 item";
            child1Item.invType = _rnd.Next(1, 1000);
            child1Item.parentFolderID = child1.folderID;
            _dbPlugin.addInventoryItem(child1Item);

            InventoryItemBase child1ChildItem = new InventoryItemBase();
            child1ChildItem.assetID = LLUUID.Random();
            child1ChildItem.assetType = _rnd.Next(1, 1000);
            child1ChildItem.avatarID = _agent_1_id;
            child1ChildItem.creatorsID = LLUUID.Random();
            child1ChildItem.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            child1ChildItem.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            child1ChildItem.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            child1ChildItem.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            child1ChildItem.inventoryID = LLUUID.Random();
            child1ChildItem.inventoryDescription = "child1Child item, Description";
            child1ChildItem.inventoryName = "child1Child item";
            child1ChildItem.invType = _rnd.Next(1, 1000);
            child1ChildItem.parentFolderID = child1Child.folderID;
            _dbPlugin.addInventoryItem(child1ChildItem);

            InventoryItemBase child2Item = new InventoryItemBase();
            child2Item.assetID = LLUUID.Random();
            child2Item.assetType = _rnd.Next(1, 1000);
            child2Item.avatarID = _agent_1_id;
            child2Item.creatorsID = LLUUID.Random();
            child2Item.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            child2Item.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            child2Item.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            child2Item.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            child2Item.inventoryID = LLUUID.Random();
            child2Item.inventoryDescription = "child2 item, Description";
            child2Item.inventoryName = "child2 item";
            child2Item.invType = _rnd.Next(1, 1000);
            child2Item.parentFolderID = child2.folderID;
            _dbPlugin.addInventoryItem(child2Item);

            InventoryItemBase child2ChildItem = new InventoryItemBase();
            child2ChildItem.assetID = LLUUID.Random();
            child2ChildItem.assetType = _rnd.Next(1, 1000);
            child2ChildItem.avatarID = _agent_1_id;
            child2ChildItem.creatorsID = LLUUID.Random();
            child2ChildItem.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            child2ChildItem.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            child2ChildItem.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            child2ChildItem.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            child2ChildItem.inventoryID = LLUUID.Random();
            child2ChildItem.inventoryDescription = "child2Child item, Description";
            child2ChildItem.inventoryName = "child2Child item";
            child2ChildItem.invType = _rnd.Next(1, 1000);
            child2ChildItem.parentFolderID = child2Child.folderID;
            _dbPlugin.addInventoryItem(child2ChildItem);

            // test deletetion of items

            _dbPlugin.deleteInventoryFolder(_agent_1_id);

            InventoryItemBase item = _dbPlugin.getInventoryItem(rootItem.inventoryID);
            Assert.IsNull(item);

            item = _dbPlugin.getInventoryItem(child1Item.inventoryID);
            Assert.IsNull(item);

            item = _dbPlugin.getInventoryItem(child1ChildItem.inventoryID);
            Assert.IsNull(item);

            item = _dbPlugin.getInventoryItem(child2Item.inventoryID);
            Assert.IsNull(item);

            item = _dbPlugin.getInventoryItem(child2ChildItem.inventoryID);
            Assert.IsNull(item);

        }

        /// <summary>
        /// Test that we can update items
        /// </summary>
        [Test]
        public void T09_UpdateItem()
        {

            // Setup inventory
            InventoryFolderBase root = new InventoryFolderBase();
            root.agentID = _agent_1_id;
            root.folderID = _agent_1_id;
            root.name = "Root folder";
            root.parentID = LLUUID.Zero;
            root.type = 2;
            root.version = 2;
            _dbPlugin.addInventoryFolder(root);

            InventoryItemBase rootItem = new InventoryItemBase();
            rootItem.assetID = LLUUID.Random();
            rootItem.assetType = _rnd.Next(1, 1000);
            rootItem.avatarID = _agent_1_id;
            rootItem.creatorsID = LLUUID.Random();
            rootItem.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            rootItem.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            rootItem.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            rootItem.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            rootItem.inventoryID = LLUUID.Random();
            rootItem.inventoryDescription = "Root item, Description";
            rootItem.inventoryName = "Root item";
            rootItem.invType = _rnd.Next(1, 1000);
            rootItem.parentFolderID = root.folderID;
            _dbPlugin.addInventoryItem(rootItem);

            rootItem.assetID = LLUUID.Random();
            _dbPlugin.updateInventoryItem(rootItem);
            InventoryItemBase item = _dbPlugin.getInventoryItem(rootItem.inventoryID);
            Assert.IsTrue(AreItemsIdentical(rootItem, item));

            rootItem.assetType = rootItem.assetType+1;
            _dbPlugin.updateInventoryItem(rootItem);
            item = _dbPlugin.getInventoryItem(rootItem.inventoryID);
            Assert.IsTrue(AreItemsIdentical(rootItem, item));

            rootItem.avatarID = LLUUID.Random();
            _dbPlugin.updateInventoryItem(rootItem);
            item = _dbPlugin.getInventoryItem(rootItem.inventoryID);
            Assert.IsTrue(AreItemsIdentical(rootItem, item));

            rootItem.creatorsID = LLUUID.Random();
            _dbPlugin.updateInventoryItem(rootItem);
            item = _dbPlugin.getInventoryItem(rootItem.inventoryID);
            Assert.IsTrue(AreItemsIdentical(rootItem, item));

            rootItem.inventoryBasePermissions = rootItem.inventoryBasePermissions+1;
            _dbPlugin.updateInventoryItem(rootItem);
            item = _dbPlugin.getInventoryItem(rootItem.inventoryID);
            Assert.IsTrue(AreItemsIdentical(rootItem, item));

            rootItem.inventoryCurrentPermissions = rootItem.inventoryCurrentPermissions+1;
            _dbPlugin.updateInventoryItem(rootItem);
            item = _dbPlugin.getInventoryItem(rootItem.inventoryID);
            Assert.IsTrue(AreItemsIdentical(rootItem, item));

            rootItem.inventoryDescription = "New description";
            _dbPlugin.updateInventoryItem(rootItem);
            item = _dbPlugin.getInventoryItem(rootItem.inventoryID);
            Assert.IsTrue(AreItemsIdentical(rootItem, item));

            rootItem.inventoryEveryOnePermissions = rootItem.inventoryEveryOnePermissions+1;
            _dbPlugin.updateInventoryItem(rootItem);
            item = _dbPlugin.getInventoryItem(rootItem.inventoryID);
            Assert.IsTrue(AreItemsIdentical(rootItem, item));

            rootItem.inventoryName = "New name";
            _dbPlugin.updateInventoryItem(rootItem);
            item = _dbPlugin.getInventoryItem(rootItem.inventoryID);
            Assert.IsTrue(AreItemsIdentical(rootItem, item));

            rootItem.inventoryNextPermissions = rootItem.inventoryNextPermissions+1;
            _dbPlugin.updateInventoryItem(rootItem);
            item = _dbPlugin.getInventoryItem(rootItem.inventoryID);
            Assert.IsTrue(AreItemsIdentical(rootItem, item));

            rootItem.invType = rootItem.invType+1;
            _dbPlugin.updateInventoryItem(rootItem);
            item = _dbPlugin.getInventoryItem(rootItem.inventoryID);
            Assert.IsTrue(AreItemsIdentical(rootItem, item));

            rootItem.parentFolderID = LLUUID.Zero;
            _dbPlugin.updateInventoryItem(rootItem);
            item = _dbPlugin.getInventoryItem(rootItem.inventoryID);
            Assert.IsTrue(AreItemsIdentical(rootItem, item));

            _dbPlugin.deleteInventoryFolder(_agent_1_id);
            _dbPlugin.deleteInventoryItem(rootItem.inventoryID);
        }


        /// <summary>
        /// Test that we can insert a root folder
        /// </summary>
        [Test]
        public void T10_GetListOfItemsInFolder()
        {

            // Setup inventory
            InventoryFolderBase root = new InventoryFolderBase();
            root.agentID = _agent_1_id;
            root.folderID = _agent_1_id;
            root.name = "Root folder";
            root.parentID = LLUUID.Zero;
            root.type = 2;
            root.version = 2;
            _dbPlugin.addInventoryFolder(root);

            InventoryFolderBase child1 = new InventoryFolderBase();
            child1.agentID = _agent_1_id;
            child1.folderID = LLUUID.Random();
            child1.name = "Child 1";
            child1.parentID = root.folderID;
            child1.type = 3;
            child1.version = 3;
            _dbPlugin.addInventoryFolder(child1);

            InventoryFolderBase child1Child = new InventoryFolderBase();
            child1Child.agentID = _agent_1_id;
            child1Child.folderID = LLUUID.Random();
            child1Child.name = "Child 1 child";
            child1Child.parentID = child1.folderID;
            child1Child.type = 4;
            child1Child.version = 4;
            _dbPlugin.addInventoryFolder(child1Child);


            InventoryItemBase item1 = new InventoryItemBase();
            item1.assetID = LLUUID.Random();
            item1.assetType = _rnd.Next(1, 1000);
            item1.avatarID = _agent_1_id;
            item1.creatorsID = LLUUID.Random();
            item1.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            item1.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            item1.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            item1.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            item1.inventoryID = LLUUID.Random();
            item1.inventoryDescription = "Item 1, description";
            item1.inventoryName = "Item 1";
            item1.invType = _rnd.Next(1, 1000);
            item1.parentFolderID = child1Child.folderID;
            _dbPlugin.addInventoryItem(item1);

            InventoryItemBase item2 = new InventoryItemBase();
            item2.assetID = LLUUID.Random();
            item2.assetType = _rnd.Next(1, 1000);
            item2.avatarID = _agent_1_id;
            item2.creatorsID = LLUUID.Random();
            item2.inventoryBasePermissions = (uint)_rnd.Next(1, 1000000);
            item2.inventoryCurrentPermissions = (uint)_rnd.Next(1, 1000000);
            item2.inventoryEveryOnePermissions = (uint)_rnd.Next(1, 1000000);
            item2.inventoryNextPermissions = (uint)_rnd.Next(1, 1000000);
            item2.inventoryID = LLUUID.Random();
            item2.inventoryDescription = "Item 1, description";
            item2.inventoryName = "Item 1";
            item2.invType = _rnd.Next(1, 1000);
            item2.parentFolderID = child1Child.folderID;
            _dbPlugin.addInventoryItem(item2);

            List<InventoryItemBase> items = _dbPlugin.getInventoryInFolder(child1Child.folderID);
            Assert.IsNotNull(items);
            Assert.IsNotEmpty(items);

            bool foundItem1 = false;
            bool foundItem2 = false;

            foreach (InventoryItemBase i in items)
            {
                if (i.inventoryID == item1.inventoryID)
                {
                    foundItem1 = true;
                    Assert.IsTrue(AreItemsIdentical(item1, i));
                }
                else if (i.inventoryID == item2.inventoryID)
                {
                    foundItem2 = true;
                    Assert.IsTrue(AreItemsIdentical(item2, i));
                }
                else
                {
                    Assert.Fail("Unknown inventory item found");
                }
            }

            Assert.IsTrue(foundItem1 && foundItem2, "not all items were returned");
            _dbPlugin.deleteInventoryFolder(_agent_1_id);
        }
    }
}
