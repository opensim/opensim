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

using System.Collections.Generic;
using OpenSim.Data;
using OpenMetaverse;

namespace OpenSim.Framework.Communications.Osp
{
    /// <summary>
    /// Wrap other inventory data plugins so that we can perform OSP related post processing for items
    /// </summary>
    public class OspInventoryWrapperPlugin : IInventoryDataPlugin
    {
        protected IInventoryDataPlugin m_wrappedPlugin;
        protected CommunicationsManager m_commsManager;

        public OspInventoryWrapperPlugin(IInventoryDataPlugin wrappedPlugin, CommunicationsManager commsManager)
        {
            m_wrappedPlugin = wrappedPlugin;
            m_commsManager = commsManager;
        }
            
        public string Name { get { return "OspInventoryWrapperPlugin"; } }
        public string Version { get { return "0.1"; } }
        public void Initialise() {}
        public void Initialise(string connect) {}
        public void Dispose() {}

        public InventoryItemBase getInventoryItem(UUID item)
        {
            return PostProcessItem(m_wrappedPlugin.getInventoryItem(item));
        }

        // XXX: Why on earth does this exist as it appears to duplicate getInventoryItem?
        public InventoryItemBase queryInventoryItem(UUID item)
        {
            return PostProcessItem(m_wrappedPlugin.queryInventoryItem(item));
        }
        
        public List<InventoryItemBase> getInventoryInFolder(UUID folderID)
        {
            List<InventoryItemBase> items = m_wrappedPlugin.getInventoryInFolder(folderID);
            
            foreach (InventoryItemBase item in items)
                PostProcessItem(item);

            return items;
        }

        public List<InventoryItemBase> fetchActiveGestures(UUID avatarID)
        {
            return m_wrappedPlugin.fetchActiveGestures(avatarID);

            // Presuming that no post processing is needed here as gestures don't refer to creator information (?)
        }

        protected InventoryItemBase PostProcessItem(InventoryItemBase item)
        {
            item.CreatorIdAsUuid = OspResolver.ResolveOspa(item.CreatorId, m_commsManager);
            return item;
        }
        
        public List<InventoryFolderBase> getFolderHierarchy(UUID parentID) { return m_wrappedPlugin.getFolderHierarchy(parentID); }
        public List<InventoryFolderBase> getUserRootFolders(UUID user) { return m_wrappedPlugin.getUserRootFolders(user); }
        public InventoryFolderBase getUserRootFolder(UUID user) { return m_wrappedPlugin.getUserRootFolder(user); }
        public List<InventoryFolderBase> getInventoryFolders(UUID parentID) { return m_wrappedPlugin.getInventoryFolders(parentID); }
        public InventoryFolderBase getInventoryFolder(UUID folder) { return m_wrappedPlugin.getInventoryFolder(folder); }
        public void addInventoryItem(InventoryItemBase item) { m_wrappedPlugin.addInventoryItem(item); }
        public void updateInventoryItem(InventoryItemBase item) { m_wrappedPlugin.updateInventoryItem(item); }
        public void deleteInventoryItem(UUID item) { m_wrappedPlugin.deleteInventoryItem(item); }
        public InventoryFolderBase queryInventoryFolder(UUID folder) { return m_wrappedPlugin.queryInventoryFolder(folder); }
        public void addInventoryFolder(InventoryFolderBase folder) { m_wrappedPlugin.addInventoryFolder(folder); }
        public void updateInventoryFolder(InventoryFolderBase folder) { m_wrappedPlugin.updateInventoryFolder(folder); }
        public void moveInventoryFolder(InventoryFolderBase folder) { m_wrappedPlugin.moveInventoryFolder(folder); }
        public void deleteInventoryFolder(UUID folder) { m_wrappedPlugin.deleteInventoryFolder(folder); }
    }
}
