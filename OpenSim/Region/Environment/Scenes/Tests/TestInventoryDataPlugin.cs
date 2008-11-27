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
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Region.Environment.Scenes.Tests
{
    public class TestInventoryDataPlugin : IInventoryDataPlugin
    {
        public string Version { get { return "0"; } }
        public string Name { get { return "TestInventoryDataPlugin"; } }

        public void Initialise() {}
        public void Initialise(string connect) {}
        public void Dispose() {}

        public List<InventoryFolderBase> getFolderHierarchy(UUID parentID) { return null; } 
        public List<InventoryItemBase> getInventoryInFolder(UUID folderID) { return null; }
        public List<InventoryFolderBase> getUserRootFolders(UUID user) { return null; }
        public InventoryFolderBase getUserRootFolder(UUID user) { return null; }
        public List<InventoryFolderBase> getInventoryFolders(UUID parentID) { return null; }
        public InventoryItemBase getInventoryItem(UUID item) { return null; }
        public InventoryFolderBase getInventoryFolder(UUID folder) { return null; }
        public void addInventoryItem(InventoryItemBase item) {}
        public void updateInventoryItem(InventoryItemBase item) {}
        public void deleteInventoryItem(UUID item) {}
        public void addInventoryFolder(InventoryFolderBase folder) {}
        public void updateInventoryFolder(InventoryFolderBase folder) {}
        public void moveInventoryFolder(InventoryFolderBase folder) {}
        public void deleteInventoryFolder(UUID folder) {}
        public List<InventoryItemBase> fetchActiveGestures(UUID avatarID) { return null; }          
    }
}
