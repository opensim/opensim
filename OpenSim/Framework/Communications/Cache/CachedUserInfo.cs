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
using System.Reflection;
using libsecondlife;
using log4net;

namespace OpenSim.Framework.Communications.Cache
{
    /// <summary>
    /// Stores user profile and inventory data received from backend services for a particular user.
    /// </summary>
    public class CachedUserInfo
    {
        private static readonly ILog m_log 
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        /// <summary>
        /// The comms manager holds references to services (user, grid, inventory, etc.)
        /// </summary>        
        private readonly CommunicationsManager m_commsManager;
        
        public UserProfileData UserProfile { get { return m_userProfile; } }
        private UserProfileData m_userProfile;                

        /// <summary>
        /// Has we received the user's inventory from the inventory service?
        /// </summary>
        private bool m_hasInventory;
        
        /// <summary>
        /// Inventory requests waiting for receipt of this user's inventory from the inventory service.
        /// </summary>
        private readonly IList<IInventoryRequest> m_pendingRequests = new List<IInventoryRequest>();         
        
        /// <summary>
        /// Has this user info object yet received its inventory information from the invetnroy service?
        /// </summary>
        public bool HasInventory { get { return m_hasInventory; } }
        
        private InventoryFolderImpl m_rootFolder;
        public InventoryFolderImpl RootFolder { get { return m_rootFolder; } }        
        
        /// <summary>
        /// FIXME: This could be contained within a local variable - it doesn't need to be a field
        /// </summary>
        private IDictionary<LLUUID, IList<InventoryFolderImpl>> pendingCategorizationFolders 
            = new Dictionary<LLUUID, IList<InventoryFolderImpl>>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="commsManager"></param>
        /// <param name="userProfile"></param>
        public CachedUserInfo(CommunicationsManager commsManager, UserProfileData userProfile)
        {
            m_commsManager = commsManager;
            m_userProfile = userProfile;
        }
        
        /// <summary>
        /// This allows a request to be added to be processed once we receive a user's inventory
        /// from the inventory service.  If we already have the inventory, the request
        /// is executed immediately instead.
        /// </summary>
        /// <param name="parent"></param>
        public void AddRequest(IInventoryRequest request)
        {
            lock (m_pendingRequests)
            {
                if (m_hasInventory)
                {
                    request.Execute();
                }
                else
                {
                    m_pendingRequests.Add(request);
                }
            }
        }
        
        /// <summary>
        /// Store a folder pending categorization when its parent is received.
        /// </summary>
        /// <param name="folder"></param>
        private void AddPendingFolder(InventoryFolderImpl folder)
        {
            LLUUID parentFolderId = folder.ParentID;
            
            if (pendingCategorizationFolders.ContainsKey(parentFolderId))
            {
                pendingCategorizationFolders[parentFolderId].Add(folder);
            }
            else
            {
                IList<InventoryFolderImpl> folders = new List<InventoryFolderImpl>();
                folders.Add(folder);
                
                pendingCategorizationFolders[parentFolderId] = folders;
            }
        }
        
        /// <summary>
        /// Add any pending folders which are children of parent
        /// </summary>
        /// <param name="parentId">
        /// A <see cref="LLUUID"/>
        /// </param>
        private void ResolvePendingFolders(InventoryFolderImpl parent)
        {
            if (pendingCategorizationFolders.ContainsKey(parent.ID))
            {
                foreach (InventoryFolderImpl folder in pendingCategorizationFolders[parent.ID])
                {
//                    m_log.DebugFormat(
//                        "[INVENTORY CACHE]: Resolving pending received folder {0} {1} into {2} {3}",
//                        folder.name, folder.folderID, parent.name, parent.folderID);
                    
                    lock (parent.SubFolders)
                    {
                        if (!parent.SubFolders.ContainsKey(folder.ID))
                        {
                            parent.SubFolders.Add(folder.ID, folder);
                        }                    
                    }
                }
            }
        }
        
        /// <summary>
        /// Callback invoked when the inventory is received from an async request to the inventory service
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="inventoryCollection"></param>
        public void InventoryReceive(LLUUID userID, ICollection<InventoryFolderImpl> folders, ICollection<InventoryItemBase> items)
        {
            // FIXME: Exceptions thrown upwards never appear on the console.  Could fix further up if these
            // are simply being swallowed
            try
            {            
                foreach (InventoryFolderImpl folder in folders)
                {
                    FolderReceive(userID, folder);
                }
                
                foreach (InventoryItemBase item in items)
                {
                    ItemReceive(userID, item);
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CACHE]: Error processing inventory received from inventory service, {0}", e);
            } 
                                    
            // Deal with pending requests
            lock (m_pendingRequests)
            {
                // We're going to change inventory status within the lock to avoid a race condition
                // where requests are processed after the AddRequest() method has been called.
                m_hasInventory = true;
                
                foreach (IInventoryRequest request in m_pendingRequests)
                {
                    request.Execute();
                }
            }
        }

        /// <summary>
        /// Callback invoked when a folder is received from an async request to the inventory service.
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="folderInfo"></param>
        private void FolderReceive(LLUUID userID, InventoryFolderImpl folderInfo)
        {
//            m_log.DebugFormat(
//                "[INVENTORY CACHE]: Received folder {0} {1} for user {2}", 
//                folderInfo.Name, folderInfo.ID, userID);
            
            if (userID == UserProfile.ID)
            {
                if (RootFolder == null)
                {
                    if (folderInfo.ParentID == LLUUID.Zero)
                    {
                        m_rootFolder = folderInfo;
                    }
                }
                else if (RootFolder.ID == folderInfo.ParentID)
                {
                    lock (RootFolder.SubFolders)
                    {
                        if (!RootFolder.SubFolders.ContainsKey(folderInfo.ID))
                        {
                            RootFolder.SubFolders.Add(folderInfo.ID, folderInfo);
                        }
                        else
                        {
                            AddPendingFolder(folderInfo);
                        }      
                    }
                }
                else
                {
                    InventoryFolderImpl folder = RootFolder.GetDescendentFolder(folderInfo.ParentID);
                    lock (folder.SubFolders)
                    {
                        if (folder != null)
                        {
                            if (!folder.SubFolders.ContainsKey(folderInfo.ID))
                            {
                                folder.SubFolders.Add(folderInfo.ID, folderInfo);
                            }
                        }
                        else
                        {
                            AddPendingFolder(folderInfo);
                        }
                    }
                }
                
                ResolvePendingFolders(folderInfo);
            }
        }

        /// <summary>
        /// Callback invoked when an item is received from an async request to the inventory service.
        /// 
        /// We're assuming here that items are always received after all the folders have been
        /// received.
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="folderInfo"></param>        
        private void ItemReceive(LLUUID userID, InventoryItemBase itemInfo)
        {
//            m_log.DebugFormat(
//                "[INVENTORY CACHE]: Received item {0} {1} for user {2}", 
//                itemInfo.Name, itemInfo.ID, userID);
            
            if ((userID == UserProfile.ID) && (RootFolder != null))
            {
                if (itemInfo.Folder == RootFolder.ID)
                {
                    lock (RootFolder.Items)
                    {
                        if (!RootFolder.Items.ContainsKey(itemInfo.ID))
                        {

                            RootFolder.Items.Add(itemInfo.ID, itemInfo);
                        }
                        else 
                        {
                            RootFolder.Items[itemInfo.ID] = itemInfo;
                        }
                    }
                }
                else
                {
                    InventoryFolderImpl folder = RootFolder.GetDescendentFolder(itemInfo.Folder);
                    if (folder != null)
                    {
                        lock (folder.Items)
                        {
                            if (!folder.Items.ContainsKey(itemInfo.ID))
                            {
                                folder.Items.Add(itemInfo.ID, itemInfo);
                            }
                            else
                            {
                                folder.Items[itemInfo.ID] = itemInfo;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Add an item to the user's inventory
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="itemInfo"></param>
        public void AddItem(LLUUID userID, InventoryItemBase itemInfo)
        {
            if ((userID == UserProfile.ID) && HasInventory)
            {
                ItemReceive(userID, itemInfo);
                m_commsManager.InventoryService.AddNewInventoryItem(userID, itemInfo);
            }
        }

        /// <summary>
        /// Update an item in the user's inventory
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="itemInfo"></param>
        public void UpdateItem(LLUUID userID, InventoryItemBase itemInfo)
        {
            if ((userID == UserProfile.ID) && HasInventory)
            {
                m_commsManager.InventoryService.UpdateInventoryItem(userID, itemInfo);
            }
        }

        /// <summary>
        /// Delete an item from the user's inventory
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool DeleteItem(LLUUID userID, InventoryItemBase item)
        {
            bool result = false;
            if ((userID == UserProfile.ID) && HasInventory)
            {
                result = RootFolder.DeleteItem(item.ID);
                if (result)
                {
                    m_commsManager.InventoryService.DeleteInventoryItem(userID, item);
                }
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// Should be implemented by callers which require a callback when the user's inventory is received
    /// </summary>    
    public interface IInventoryRequest
    {
        /// <summary>
        /// This is the method executed once we have received the user's inventory by which the request can be fulfilled.
        /// </summary>
        void Execute();
    }
        
    /// <summary>
    /// Generic inventory request
    /// </summary>
    public class InventoryRequest : IInventoryRequest
    {
        private Delegate m_delegat;
        private Object[] m_args;
        
        internal InventoryRequest(Delegate delegat, Object[] args)
        {
            m_delegat = delegat; 
            m_args = args;
        }
        
        public void Execute()
        {
            m_delegat.DynamicInvoke(m_args);
        }
    }      
}
