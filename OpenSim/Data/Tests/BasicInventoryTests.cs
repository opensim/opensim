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
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenSim.Framework;
using OpenSim.Data.Tests;
using OpenSim.Region.Environment.Scenes;
using OpenMetaverse;

namespace OpenSim.Data.Tests
{
    public class BasicInventoryTest
    {
        public IInventoryDataPlugin db;
        public UUID zero = UUID.Zero;
        public UUID uuid1;
        public UUID uuid2;
        public UUID uuid3;
        public UUID owner1;
        public UUID owner2;
        public UUID owner3;
        public string name1;
        public string name2;
        public string name3;

        public void SuperInit()
        {
            uuid1 = UUID.Random();
            uuid2 = UUID.Random();
            uuid3 = UUID.Random();
            owner1 = UUID.Random();
            owner2 = UUID.Random();
            owner3 = UUID.Random();
            name1 = "Root Folder for " + owner1.ToString();
            name2 = "First Level folder";
            name3 = "First Level folder 2";
        }
        
        [Test]
        public void T001_LoadEmpty()
        {
            Assert.That(db.getInventoryItem(uuid1), Is.Null);
            Assert.That(db.getUserRootFolder(owner1), Is.Null);
        }

        // 01x - folder tests
        [Test]
        public void T010_FolderNonParent()
        {
            InventoryFolderBase f1 = NewFolder(uuid2, uuid1, owner1, name2);
            // the folder will go in
            db.addInventoryFolder(f1);
            InventoryFolderBase f1a = db.getUserRootFolder(owner1);
            Assert.That(f1a, Is.Null);
        }

        [Test]
        public void T011_FolderCreate()
        {
            InventoryFolderBase f1 = NewFolder(uuid1, zero, owner1, name1);
            // TODO: this is probably wrong behavior, but is what we have
            // db.updateInventoryFolder(f1);
            // InventoryFolderBase f1a = db.getUserRootFolder(owner1);
            // Assert.That(uuid1, Is.EqualTo(f1a.ID))
            // Assert.That(name1, Text.Matches(f1a.Name));
            // Assert.That(db.getUserRootFolder(owner1), Is.Null);

            // succeed with true
            db.addInventoryFolder(f1);
            InventoryFolderBase f1a = db.getUserRootFolder(owner1);
            Assert.That(uuid1, Is.EqualTo(f1a.ID));
            Assert.That(name1, Text.Matches(f1a.Name));
        }

        // we now have the following tree
        // uuid1
        //   +--- uuid2
        //   +--- uuid3

        [Test]
        public void T012_FolderList()
        {
            InventoryFolderBase f2 = NewFolder(uuid3, uuid1, owner1, name3);
            db.addInventoryFolder(f2);
           
            Assert.That(db.getInventoryFolders(zero).Count, Is.EqualTo(1));

            Assert.That(db.getInventoryFolders(uuid1).Count, Is.EqualTo(2));

            Assert.That(db.getInventoryFolders(uuid2).Count, Is.EqualTo(0));

            Assert.That(db.getInventoryFolders(uuid3).Count, Is.EqualTo(0));

            Assert.That(db.getInventoryFolders(UUID.Random()).Count, Is.EqualTo(0));

        }
        
        [Test]
        public void T013_FolderHierarchy()
        {
            Assert.That(db.getFolderHierarchy(zero).Count, Is.EqualTo(0));

            Assert.That(db.getFolderHierarchy(uuid1).Count, Is.EqualTo(2));

            Assert.That(db.getFolderHierarchy(uuid2).Count, Is.EqualTo(0));

            Assert.That(db.getFolderHierarchy(uuid3).Count, Is.EqualTo(0));
            
            Assert.That(db.getFolderHierarchy(UUID.Random()).Count, Is.EqualTo(0));
        }

        
        [Test]
        public void T014_MoveFolder()
        {
            InventoryFolderBase f2 = db.getInventoryFolder(uuid2);
            f2.ParentID = uuid3;
            db.moveInventoryFolder(f2);
           
            Assert.That(db.getInventoryFolders(zero).Count, Is.EqualTo(1));

            Assert.That(db.getInventoryFolders(uuid1).Count, Is.EqualTo(1));

            Assert.That(db.getInventoryFolders(uuid2).Count, Is.EqualTo(0));

            Assert.That(db.getInventoryFolders(uuid3).Count, Is.EqualTo(1));

            Assert.That(db.getInventoryFolders(UUID.Random()).Count, Is.EqualTo(0));
        }

        [Test]
        public void T015_FolderHierarchy()
        {
            Assert.That(db.getFolderHierarchy(zero).Count, Is.EqualTo(0));

            Assert.That(db.getFolderHierarchy(uuid1).Count, Is.EqualTo(2));

            Assert.That(db.getFolderHierarchy(uuid2).Count, Is.EqualTo(0));

            Assert.That(db.getFolderHierarchy(uuid3).Count, Is.EqualTo(1));
            
            Assert.That(db.getFolderHierarchy(UUID.Random()).Count, Is.EqualTo(0));
        }



        private InventoryFolderBase NewFolder(UUID id, UUID parent, UUID owner, string name)
        {
            InventoryFolderBase f = new InventoryFolderBase();
            f.ID = id;
            f.ParentID = parent;
            f.Owner = owner;
            f.Name = name;
            return f;
        }
    }
}