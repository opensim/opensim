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
using System.Net;
using System.Reflection;
using libsecondlife;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGS1InventoryService : IInventoryServices
    {
        private static readonly ILog m_log 
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string _inventoryServerUrl;
        private Dictionary<LLUUID, InventoryRequest> m_RequestingInventory = new Dictionary<LLUUID, InventoryRequest>();

        public OGS1InventoryService(string inventoryServerUrl)
        {
            _inventoryServerUrl = inventoryServerUrl;
        }

        #region IInventoryServices Members

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInventoryServices"></see>
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="callback"></param>
        public void RequestInventoryForUser(LLUUID userID, InventoryReceiptCallback callback)
        {
            if (!m_RequestingInventory.ContainsKey(userID))
            {
                InventoryRequest request = new InventoryRequest(userID, callback);
                m_RequestingInventory.Add(userID, request);
                
                try
                {
                    m_log.InfoFormat(
                        "[OGS1 INVENTORY SERVICE]: Requesting inventory from {0}/GetInventory/ for user {1}",
                        _inventoryServerUrl, userID);
    
                    RestObjectPosterResponse<InventoryCollection> requester
                        = new RestObjectPosterResponse<InventoryCollection>();
                    requester.ResponseCallback = InventoryResponse;
    
                    requester.BeginPostObject<Guid>(_inventoryServerUrl + "/GetInventory/", userID.UUID);
                }
                catch (WebException e)
                {
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
            LLUUID userID = response.UserID;
            if (m_RequestingInventory.ContainsKey(userID))
            {
                m_log.InfoFormat("[OGS1 INVENTORY SERVICE]: " +
                                 "Received inventory response for user {0} containing {1} folders and {2} items",
                                 userID, response.Folders.Count, response.Items.Count);

                InventoryFolderImpl rootFolder = null;
                InventoryRequest request = m_RequestingInventory[userID];
                
                ICollection<InventoryFolderImpl> folders = new List<InventoryFolderImpl>();
                ICollection<InventoryItemBase> items = new List<InventoryItemBase>();
                
                foreach (InventoryFolderBase folder in response.Folders)
                {
                    if (folder.ParentID == LLUUID.Zero)
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
                
                request.Callback(userID, folders, items);
                
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
        /// <param name="userID"></param>
        /// <param name="folder"></param>        
        public void AddNewInventoryFolder(LLUUID userID, InventoryFolderBase folder)
        {
            try
            {
                SynchronousRestObjectPoster.BeginPostObject<InventoryFolderBase, bool>(
                    "POST", _inventoryServerUrl + "/NewFolder/", folder);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Add new inventory folder operation failed, {0} {1}", 
                     e.Source, e.Message);
            }
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInventoryServices"></see>
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="folder"></param>        
        public void MoveInventoryFolder(LLUUID userID, InventoryFolderBase folder)
        {
            try
            {            
                SynchronousRestObjectPoster.BeginPostObject<InventoryFolderBase, bool>(
                    "POST", _inventoryServerUrl + "/MoveFolder/", folder);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Move inventory folder operation failed, {0} {1}", 
                     e.Source, e.Message);
            }                
        }
        
        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInventoryServices"></see>
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="folder"></param>
        /// <returns></returns>
        public void PurgeInventoryFolder(LLUUID userID, InventoryFolderBase folder)
        {
            try
            {            
                SynchronousRestObjectPoster.BeginPostObject<InventoryFolderBase, bool>(
                    "POST", _inventoryServerUrl + "/PurgeFolder/", folder);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Move inventory folder operation failed, {0} {1}", 
                     e.Source, e.Message);
            }    
        }        

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInventoryServices"></see>
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="folder"></param>        
        public void AddNewInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            try
            {                
                SynchronousRestObjectPoster.BeginPostObject<InventoryItemBase, bool>(
                    "POST", _inventoryServerUrl + "/NewItem/", item);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Add new inventory item operation failed, {0} {1}", 
                     e.Source, e.Message);
            }                
        }

        // TODO: this is a temporary workaround, the UpdateInventoryItem method need to be implemented
        public void UpdateInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            try
            {
                SynchronousRestObjectPoster.BeginPostObject<InventoryItemBase, bool>(
                    "POST", _inventoryServerUrl + "/NewItem/", item);
            }
            catch (System.Net.WebException e)
            {
                m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Update new inventory item operation failed, {0} {1}",
                     e.Source, e.Message);
            }
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInventoryServices"></see>
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="folder"></param>        
        public void DeleteInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            try
            {                    
                SynchronousRestObjectPoster.BeginPostObject<InventoryItemBase, bool>(
                    "POST", _inventoryServerUrl + "/DeleteItem/", item);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: Delete inventory item operation failed, {0} {1}", 
                     e.Source, e.Message);
            }                
        }

        public bool HasInventoryForUser(LLUUID userID)
        {
            return false;
        }

        public InventoryFolderBase RequestRootFolder(LLUUID userID)
        {
            return null;
        }

        public bool CreateNewUserInventory(LLUUID user)
        {
            return false;
        }
        
        // See IInventoryServices
        public List<InventoryFolderBase> GetInventorySkeleton(LLUUID userId)
        {
            m_log.ErrorFormat("[OGS1 INVENTORY SERVICE]: The GetInventorySkeleton() method here should never be called!");
            
            return new List<InventoryFolderBase>();
        }

        #endregion

        /// <summary>
        /// Caches a pending inventory request that has yet to be satisfied by the inventory service
        /// </summary>
        public class InventoryRequest
        {
            public LLUUID UserID;
            public InventoryReceiptCallback Callback;

            public InventoryRequest(LLUUID userId, InventoryReceiptCallback callback)
            {
                UserID = userId;
                Callback = callback;
            }
        }
    }
}
