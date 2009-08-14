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

using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Services.Connectors
{
    /// <summary>
    /// This connector is temporary. It's used by the user server, before that server is refactored.
    /// </summary>
    public class QuickAndDirtyInventoryServiceConnector : IInventoryService
    {
//        private static readonly ILog m_log =
//                LogManager.GetLogger(
//                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;

        //private Dictionary<UUID, InventoryReceiptCallback> m_RequestingInventory = new Dictionary<UUID, InventoryReceiptCallback>();

        public QuickAndDirtyInventoryServiceConnector()
        {
        }

        public QuickAndDirtyInventoryServiceConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/');
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInterServiceInventoryServices"/>
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public bool CreateUserInventory(UUID userId)
        {
            return SynchronousRestObjectPoster.BeginPostObject<Guid, bool>(
                "POST", m_ServerURI + "CreateInventory/", userId.Guid);
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInterServiceInventoryServices"/>
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> GetInventorySkeleton(UUID userId)
        {
            return SynchronousRestObjectPoster.BeginPostObject<Guid, List<InventoryFolderBase>>(
                "POST", m_ServerURI + "RootFolders/", userId.Guid);
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
            return SynchronousRestObjectPoster.BeginPostObject<Guid, List<InventoryItemBase>>(
                "POST", m_ServerURI + "ActiveGestures/", userId.Guid);
        }

        public InventoryCollection GetUserInventory(UUID userID)
        {
            return null;
        }

        public void GetUserInventory(UUID userID, InventoryReceiptCallback callback)
        {
        }

        public InventoryFolderBase GetFolderForType(UUID userID, AssetType type)
        {
            return null;
        }

        public InventoryCollection GetFolderContent(UUID userID, UUID folderID)
        {
            return null;
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

        public bool DeleteItem(InventoryItemBase item)
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

        public InventoryFolderBase GetRootFolder(UUID userID)
        {
            return null;
        }

        public int GetAssetPermissions(UUID userID, UUID assetID)
        {
            return 0;
        }

    }
}
