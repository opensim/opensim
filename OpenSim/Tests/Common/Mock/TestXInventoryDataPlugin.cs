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
using System.Linq;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;
using OpenSim.Data.Null;

namespace OpenSim.Tests.Common
{
    public class TestXInventoryDataPlugin : NullGenericDataHandler, IXInventoryData
    {
        private Dictionary<UUID, XInventoryFolder> m_allFolders = new Dictionary<UUID, XInventoryFolder>();
        private Dictionary<UUID, XInventoryItem> m_allItems = new Dictionary<UUID, XInventoryItem>();

        public TestXInventoryDataPlugin(string conn, string realm) {}

        public XInventoryItem[] GetItems(string[] fields, string[] vals)
        {
//            Console.WriteLine(
//                "Requesting items, fields {0}, vals {1}", string.Join(", ", fields), string.Join(", ", vals));

            List<XInventoryItem> origItems = Get<XInventoryItem>(fields, vals, m_allItems.Values.ToList());

            XInventoryItem[] items = origItems.Select(i => i.Clone()).ToArray();

//            Console.WriteLine("Found {0} items", items.Length);
//            Array.ForEach(items, i => Console.WriteLine("Found item {0} {1}", i.inventoryName, i.inventoryID));

            return items;
        }

        public XInventoryFolder[] GetFolders(string[] fields, string[] vals)
        {
//            Console.WriteLine(
//                "Requesting folders, fields {0}, vals {1}", string.Join(", ", fields), string.Join(", ", vals));

            List<XInventoryFolder> origFolders
                = Get<XInventoryFolder>(fields, vals, m_allFolders.Values.ToList());

            XInventoryFolder[] folders = origFolders.Select(f => f.Clone()).ToArray();

//            Console.WriteLine("Found {0} folders", folders.Length);
//            Array.ForEach(folders, f => Console.WriteLine("Found folder {0} {1}", f.folderName, f.folderID));

            return folders;
        }

        public bool StoreFolder(XInventoryFolder folder)
        {
            m_allFolders[folder.folderID] = folder.Clone();

//            Console.WriteLine("Added folder {0} {1}", folder.folderName, folder.folderID);

            return true;
        }

        public bool StoreItem(XInventoryItem item)
        {
            m_allItems[item.inventoryID] = item.Clone();

//            Console.WriteLine(
//                "Added item {0} {1}, folder {2}, creator {3}, owner {4}", 
//                item.inventoryName, item.inventoryID, item.parentFolderID, item.creatorID, item.avatarID);

            return true;
        }

        public bool DeleteFolders(string field, string val)
        {
            return DeleteFolders(new string[] { field }, new string[] { val });
        }

        public bool DeleteFolders(string[] fields, string[] vals)
        {
            XInventoryFolder[] foldersToDelete = GetFolders(fields, vals);
            Array.ForEach(foldersToDelete, f => m_allFolders.Remove(f.folderID));

            return true;
        }

        public bool DeleteItems(string field, string val)
        {
            return DeleteItems(new string[] { field }, new string[] { val });
        }

        public bool DeleteItems(string[] fields, string[] vals)
        {
            XInventoryItem[] itemsToDelete = GetItems(fields, vals);
            Array.ForEach(itemsToDelete, i => m_allItems.Remove(i.inventoryID));

            return true;
        }

        public bool MoveItem(string id, string newParent) { throw new NotImplementedException(); }

        public bool MoveFolder(string id, string newParent) 
        { 
            // Don't use GetFolders() here - it takes a clone!
            XInventoryFolder folder = m_allFolders[new UUID(id)];

            if (folder == null)
                return false;

            folder.parentFolderID = new UUID(newParent);

//            XInventoryFolder[] newParentFolders 
//                = GetFolders(new string[] { "folderID" }, new string[] { folder.parentFolderID.ToString() });

//            Console.WriteLine(
//                "Moved folder {0} {1}, to {2} {3}", 
//                folder.folderName, folder.folderID, newParentFolders[0].folderName, folder.parentFolderID);

            // TODO: Really need to implement folder version incrementing, though this should be common code anyway,
            // not reimplemented in each db plugin.

            return true;
        }

        public XInventoryItem[] GetActiveGestures(UUID principalID) { throw new NotImplementedException(); }
        public int GetAssetPermissions(UUID principalID, UUID assetID) { throw new NotImplementedException(); }
    }
}