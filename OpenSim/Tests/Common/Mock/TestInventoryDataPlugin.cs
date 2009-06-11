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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;

namespace OpenSim.Tests.Common.Mock
{
    /// <summary>
    /// In memory inventory data plugin for test purposes.  Could be another dll when properly filled out and when the
    /// mono addin plugin system starts co-operating with the unit test system.  Currently no locking since unit
    /// tests are single threaded.
    /// </summary>
    public class TestInventoryDataPlugin : IInventoryDataPlugin
    {
        /// <value>
        /// Known inventory folders
        /// </value>
        private Dictionary<UUID, InventoryFolderBase> m_folders = new Dictionary<UUID, InventoryFolderBase>();

        /// <value>
        /// User root folders
        /// </value>
        private Dictionary<UUID, InventoryFolderBase> m_rootFolders = new Dictionary<UUID, InventoryFolderBase>();

        public string Version { get { return "0"; } }
        public string Name { get { return "TestInventoryDataPlugin"; } }

        public void Initialise() {}
        public void Initialise(string connect) {}
        public void Dispose() {}

        public List<InventoryFolderBase> getFolderHierarchy(UUID parentID)
        {
            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();

            foreach (InventoryFolderBase folder in m_folders.Values)
            {
                if (folder.ParentID == parentID)
                {
                    folders.AddRange(getFolderHierarchy(folder.ID));
                    folders.Add(folder);
                }
            }

            return folders;
        }

        public List<InventoryItemBase> getInventoryInFolder(UUID folderID)
        {
            return new List<InventoryItemBase>();
        }

        public List<InventoryFolderBase> getUserRootFolders(UUID user) { return null; }

        public InventoryFolderBase getUserRootFolder(UUID user)
        {
            InventoryFolderBase folder = null;
            m_rootFolders.TryGetValue(user, out folder);

            return folder;
        }

        public List<InventoryFolderBase> getInventoryFolders(UUID parentID)
        {
            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();

            foreach (InventoryFolderBase folder in m_folders.Values)
            {
                if (folder.ParentID == parentID)
                    folders.Add(folder);
            }

            return folders;
        }

        public InventoryItemBase getInventoryItem(UUID item) { return null; }

        public InventoryFolderBase getInventoryFolder(UUID folderId)
        {
            InventoryFolderBase folder = null;
            m_folders.TryGetValue(folderId, out folder);

            return folder;
        }

        public void addInventoryItem(InventoryItemBase item) {}
        public void updateInventoryItem(InventoryItemBase item) {}
        public void deleteInventoryItem(UUID item) {}

        public InventoryItemBase queryInventoryItem(UUID item)
        {
            return null;
        }

        public InventoryFolderBase queryInventoryFolder(UUID folderID)
        {
            return getInventoryFolder(folderID);
        }

        public void addInventoryFolder(InventoryFolderBase folder)
        {
            m_folders[folder.ID] = folder;

            if (folder.ParentID == UUID.Zero)
                m_rootFolders[folder.Owner] = folder;
        }

        public void updateInventoryFolder(InventoryFolderBase folder)
        {
            m_folders[folder.ID] = folder;
        }

        public void moveInventoryFolder(InventoryFolderBase folder)
        {
            // Simple replace
            updateInventoryFolder(folder);
        }

        public void deleteInventoryFolder(UUID folderId)
        {
            if (m_folders.ContainsKey(folderId))
                m_folders.Remove(folderId);
        }

        public List<InventoryItemBase> fetchActiveGestures(UUID avatarID) { return null; }
    }
}
