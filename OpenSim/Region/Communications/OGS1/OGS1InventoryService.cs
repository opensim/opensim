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
using System.Net;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Statistics;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGS1InventoryService : IInventoryServices
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string _inventoryServerUrl;
        private Uri m_Uri;
        private Dictionary<UUID, InventoryReceiptCallback> m_RequestingInventory
            = new Dictionary<UUID, InventoryReceiptCallback>();

        public OGS1InventoryService(string inventoryServerUrl)
        {
            _inventoryServerUrl = inventoryServerUrl;
            m_Uri = new Uri(_inventoryServerUrl);
        }

        #region IInventoryServices Members

        public string Host
        {
            get { return m_Uri.Host; }
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInventoryServices"></see>
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="callback"></param>
        public void RequestInventoryForUser(UUID userID, InventoryReceiptCallback callback)
        {
            if (!m_RequestingInventory.ContainsKey(userID))
            {
                m_RequestingInventory.Add(userID, callback);

                try
                {
                    m_log.InfoFormat(
                        "[OGS1 INVENTORY SERVICE]: Requesting inventory from {0}/GetInventory/ for user {1}",
                        _inventoryServerUrl, userID);

                    RestObjectPosterResponse<InventoryCollection> requester
                        = new RestObjectPosterResponse<InventoryCollection>();
                    requester.ResponseCallback = InventoryResponse;

                    requester.BeginPostObject<Guid>(_inventoryServerUrl + "/GetInventory/", userID.Guid);
                }
                catch (WebException e)
                {
                    if (StatsManager.SimExtraStats != null)
                        StatsManager.SimExtraStats.AddInventoryServiceRetrievalFailure();

                    m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Request inventory operation failed, {0} {1}",
                        e.Source, e.Message);
                }
            }
            else
            {
                m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: RequestInventoryForUser() - could you not find user profile for {0}", userID);
            }
        }

        /// <summary>
        /// Callback used by the inventory server GetInventory request
        /// </summary>
        /// <param name="userID"></param>
        private void InventoryResponse(InventoryCollection response)
        {
            UUID userID = response.UserID;
            if (m_RequestingInventory.ContainsKey(userID))
            {
                m_log.InfoFormat("[OGS1 INVENTORY SERVICE]: " +
                                 "Received inventory response for user {0} containing {1} folders and {2} items",
                                 userID, response.Folders.Count, response.Items.Count);

                InventoryFolderImpl rootFolder = null;
                InventoryReceiptCallback callback = m_RequestingInventory[userID];

                ICollection<InventoryFolderImpl> folders = new List<InventoryFolderImpl>();
                ICollection<InventoryItemBase> items = new List<InventoryItemBase>();

                foreach (InventoryFolderBase folder in response.Folders)
                {
                    if (folder.ParentID == UUID.Zero)
                    {
                        rootFolder = new InventoryFolderImpl(folder);
                        folders.Add(rootFolder);

                        break;
                    }
                }

                if (rootFolder != null)
                {
                    foreach (InventoryFolderBase folder in response.Folders)
                    {
                        if (folder.ID != rootFolder.ID)
                        {
                            folders.Add(new InventoryFolderImpl(folder));
                        }
                    }

                    foreach (InventoryItemBase item in response.Items)
                    {
                        items.Add(item);
                    }
                }
                else
                {
                    m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Did not get back an inventory containing a root folder for user {0}", userID);
                }

                callback(folders, items);

                m_RequestingInventory.Remove(userID);
            }
            else
            {
                m_log.WarnFormat(
                    "[OGS1 INVENTORY SERVICE]: " +
                    "Received inventory response for {0} for which we do not have a record of requesting!",
                    userID);
            }
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInventoryServices"></see>
        /// </summary>
        public bool AddFolder(InventoryFolderBase folder)
        {
            try
            {
                return SynchronousRestObjectPoster.BeginPostObject<InventoryFolderBase, bool>(
                    "POST", _inventoryServerUrl + "/NewFolder/", folder);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Add new inventory folder operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInventoryServices"></see>
        /// </summary>
        /// <param name="folder"></param>
        public bool UpdateFolder(InventoryFolderBase folder)
        {
            try
            {
                return SynchronousRestObjectPoster.BeginPostObject<InventoryFolderBase, bool>(
                    "POST", _inventoryServerUrl + "/UpdateFolder/", folder);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Update inventory folder operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInventoryServices"></see>
        /// </summary>
        /// <param name="folder"></param>
        public bool MoveFolder(InventoryFolderBase folder)
        {
            try
            {
                return SynchronousRestObjectPoster.BeginPostObject<InventoryFolderBase, bool>(
                    "POST", _inventoryServerUrl + "/MoveFolder/", folder);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Move inventory folder operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInventoryServices"></see>
        /// </summary>
        public bool PurgeFolder(InventoryFolderBase folder)
        {
            try
            {
                return SynchronousRestObjectPoster.BeginPostObject<InventoryFolderBase, bool>(
                    "POST", _inventoryServerUrl + "/PurgeFolder/", folder);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Move inventory folder operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInventoryServices"></see>
        /// </summary>
        public bool AddItem(InventoryItemBase item)
        {
            try
            {
                return SynchronousRestObjectPoster.BeginPostObject<InventoryItemBase, bool>(
                    "POST", _inventoryServerUrl + "/NewItem/", item);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Add new inventory item operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        // TODO: this is a temporary workaround, the UpdateInventoryItem method need to be implemented
        public bool UpdateItem(InventoryItemBase item)
        {
            try
            {
                return SynchronousRestObjectPoster.BeginPostObject<InventoryItemBase, bool>(
                    "POST", _inventoryServerUrl + "/NewItem/", item);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Update new inventory item operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInventoryServices"></see>
        /// </summary>
        public bool DeleteItem(InventoryItemBase item)
        {
            try
            {
                return SynchronousRestObjectPoster.BeginPostObject<InventoryItemBase, bool>(
                    "POST", _inventoryServerUrl + "/DeleteItem/", item);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Delete inventory item operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        public InventoryItemBase QueryItem(InventoryItemBase item)
        {
            try
            {
                return SynchronousRestObjectPoster.BeginPostObject<InventoryItemBase, InventoryItemBase>(
                    "POST", _inventoryServerUrl + "/QueryItem/", item);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Query inventory item operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return null;
        }

        public InventoryFolderBase QueryFolder(InventoryFolderBase item)
        {
            try
            {
                return SynchronousRestObjectPoster.BeginPostObject<InventoryFolderBase, InventoryFolderBase>(
                    "POST", _inventoryServerUrl + "/QueryFolder/", item);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Query inventory item operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return null;
        }

        public bool HasInventoryForUser(UUID userID)
        {
            return false;
        }

        public InventoryFolderBase RequestRootFolder(UUID userID)
        {
            return null;
        }

        #endregion
    }
}
