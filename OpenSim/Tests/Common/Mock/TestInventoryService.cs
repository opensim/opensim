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
using OpenSim.Framework;
using OpenMetaverse;
using OpenSim.Services.Interfaces;
using Nini.Config;

namespace OpenSim.Tests.Common.Mock
{
    public class TestInventoryService : IInventoryService
    {
        public TestInventoryService()
        {
        }
        
        public TestInventoryService(IConfigSource config)
        {
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInterServiceInventoryServices"/>
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public bool CreateUserInventory(UUID userId)
        {
            return false;
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInterServiceInventoryServices"/>
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> GetInventorySkeleton(UUID userId)
        {
            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            InventoryFolderBase folder = new InventoryFolderBase();
            folder.ID = UUID.Random();
            folder.Owner = userId;
            folders.Add(folder);
            return folders;
        }

        public InventoryFolderBase GetRootFolder(UUID userID)
        {
            return new InventoryFolderBase();
        }

        public InventoryCollection GetFolderContent(UUID userID, UUID folderID)
        {
            return null;
        }

        public InventoryFolderBase GetFolderForType(UUID userID, AssetType type)
        {
            return null;
        }

        /// <summary>
        /// Returns a list of all the active gestures in a user's inventory.
        /// </summary>
        /// <param name="userId">
        /// The <see cref="UUID"/> of the user
        /// </param>
        /// <returns>
        /// A flat list of the gesture items.
        /// </returns>
        public List<InventoryItemBase> GetActiveGestures(UUID userId)
        {
            return null;
        }

        public InventoryCollection GetUserInventory(UUID userID)
        {
            return null;
        }

        public void GetUserInventory(UUID userID, OpenSim.Services.Interfaces.InventoryReceiptCallback callback)
        {
        }

        public List<InventoryItemBase> GetFolderItems(UUID userID, UUID folderID)
        {
            return null;
        }

        public bool AddFolder(InventoryFolderBase folder)
        {
            return false;
        }

        public bool UpdateFolder(InventoryFolderBase folder)
        {
            return false;
        }

        public bool MoveFolder(InventoryFolderBase folder)
        {
            return false;
        }

        public bool PurgeFolder(InventoryFolderBase folder)
        {
            return false;
        }

        public bool AddItem(InventoryItemBase item)
        {
            return false;
        }

        public bool UpdateItem(InventoryItemBase item)
        {
            return false;
        }

        public bool MoveItems(UUID ownerID, List<InventoryItemBase> items)
        {
            return false;
        }

        public bool DeleteItems(UUID ownerID, List<UUID> itemIDs)
        {
            return false;
        }

        public InventoryItemBase GetItem(InventoryItemBase item)
        {
            return null;
        }

        public InventoryFolderBase GetFolder(InventoryFolderBase folder)
        {
            return null;
        }

        public bool HasInventoryForUser(UUID userID)
        {
            return false;
        }

        public InventoryFolderBase RequestRootFolder(UUID userID)
        {
            InventoryFolderBase root = new InventoryFolderBase();
            root.ID = UUID.Random();
            root.Owner = userID;
            root.ParentID = UUID.Zero;
            return root;
        }

        public int GetAssetPermissions(UUID userID, UUID assetID)
        {
            return 1;
        }
    }
}
