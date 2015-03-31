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
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;

namespace OpenSim.Tests.Common
{
    /// <summary>
    /// In memory inventory data plugin for test purposes.  Could be another dll when properly filled out and when the
    /// mono addin plugin system starts co-operating with the unit test system.  Currently no locking since unit
    /// tests are single threaded.
    /// </summary>
    public class TestInventoryDataPlugin : IInventoryDataPlugin
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        /// <value>
        /// Inventory folders
        /// </value>
        private Dictionary<UUID, InventoryFolderBase> m_folders = new Dictionary<UUID, InventoryFolderBase>();

        //// <value>
        /// Inventory items
        /// </value>
        private Dictionary<UUID, InventoryItemBase> m_items = new Dictionary<UUID, InventoryItemBase>();

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
//            InventoryFolderBase folder = m_folders[folderID];
            
//            m_log.DebugFormat("[MOCK INV DB]: Getting items in folder {0} {1}", folder.Name, folder.ID);
            
            List<InventoryItemBase> items = new List<InventoryItemBase>();

            foreach (InventoryItemBase item in m_items.Values)
            {
                if (item.Folder == folderID)
                {
//                    m_log.DebugFormat("[MOCK INV DB]: getInventoryInFolder() adding item {0}", item.Name);
                    items.Add(item);
                }
            }
            
            return items;
        }

        public List<InventoryFolderBase> getUserRootFolders(UUID user) { return null; }

        public InventoryFolderBase getUserRootFolder(UUID user)
        {
//            m_log.DebugFormat("[MOCK INV DB]: Looking for root folder for {0}", user);
            
            InventoryFolderBase folder = null;
            m_rootFolders.TryGetValue(user, out folder);

            return folder;
        }

        public List<InventoryFolderBase> getInventoryFolders(UUID parentID)
        {
//            InventoryFolderBase parentFolder = m_folders[parentID];
            
//            m_log.DebugFormat("[MOCK INV DB]: Getting folders in folder {0} {1}", parentFolder.Name, parentFolder.ID);
            
            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();

            foreach (InventoryFolderBase folder in m_folders.Values)
            {
                if (folder.ParentID == parentID)
                {
//                    m_log.DebugFormat(
//                        "[MOCK INV DB]: Found folder {0} {1} in {2} {3}", 
//                        folder.Name, folder.ID, parentFolder.Name, parentFolder.ID);
                    
                    folders.Add(folder);
                }
            }

            return folders;
        }

        public InventoryFolderBase getInventoryFolder(UUID folderId)
        {
            InventoryFolderBase folder = null;
            m_folders.TryGetValue(folderId, out folder);

            return folder;
        }

        public InventoryFolderBase queryInventoryFolder(UUID folderID)
        {
            return getInventoryFolder(folderID);
        }

        public void addInventoryFolder(InventoryFolderBase folder)
        {
//            m_log.DebugFormat(
//                "[MOCK INV DB]: Adding inventory folder {0} {1} type {2}", 
//                folder.Name, folder.ID, (AssetType)folder.Type);
            
            m_folders[folder.ID] = folder;

            if (folder.ParentID == UUID.Zero)
            {
//                m_log.DebugFormat(
//                    "[MOCK INV DB]: Adding root folder {0} {1} for {2}", folder.Name, folder.ID, folder.Owner);
                m_rootFolders[folder.Owner] = folder;
            }
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

        public void addInventoryItem(InventoryItemBase item) 
        {
            InventoryFolderBase folder = m_folders[item.Folder];
            
//            m_log.DebugFormat(
//                "[MOCK INV DB]: Adding inventory item {0} {1} in {2} {3}", item.Name, item.ID, folder.Name, folder.ID);
            
            m_items[item.ID] = item;
        }
        
        public void updateInventoryItem(InventoryItemBase item) { addInventoryItem(item); }
        
        public void deleteInventoryItem(UUID itemId) 
        {
            if (m_items.ContainsKey(itemId))
                m_items.Remove(itemId);
        }
        
        public InventoryItemBase getInventoryItem(UUID itemId) 
        {
            if (m_items.ContainsKey(itemId))
                return m_items[itemId];
            else
                return null; 
        }

        public InventoryItemBase queryInventoryItem(UUID item)
        {
            return null;
        }

        public List<InventoryItemBase> fetchActiveGestures(UUID avatarID) { return null; }
    }
}
