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

using libsecondlife;

namespace OpenSim.Framework.Communications.Cache
{
    public class CachedUserInfo
    {
        private static readonly log4net.ILog m_log 
            = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        private readonly CommunicationsManager m_parentCommsManager;

        // FIXME: These need to be hidden behind accessors
        public InventoryFolderImpl RootFolder = null;
        public UserProfileData UserProfile = null;
        
        /// <summary>
        /// Stores received folders for which we have not yet received the parents.
        /// </summary></param>
        private IDictionary<LLUUID, IList<InventoryFolderImpl>> pendingCategorizationFolders 
            = new Dictionary<LLUUID, IList<InventoryFolderImpl>>();

        public CachedUserInfo(CommunicationsManager commsManager)
        {
            m_parentCommsManager = commsManager;
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
                    
                    if (!parent.SubFolders.ContainsKey(folder.ID))
                    {
                        parent.SubFolders.Add(folder.ID, folder);
                    }                    
                }
            }
        }

        /// <summary>
        /// Callback invoked when a folder is received from an async request to the inventory service.
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="folderInfo"></param>
        public void FolderReceive(LLUUID userID, InventoryFolderImpl folderInfo)
        {
            // FIXME: Exceptions thrown upwards never appear on the console.  Could fix further up if these
            // are simply being swallowed
            try
            {
//                m_log.DebugFormat(
//                    "[INVENTORY CACHE]: Received folder {0} {1} for user {2}", 
//                    folderInfo.name, folderInfo.folderID, userID);
                
                if (userID == UserProfile.UUID)
                {
                    if (RootFolder == null)
                    {
                        if (folderInfo.ParentID == LLUUID.Zero)
                        {
                            RootFolder = folderInfo;
                        }
                    }
                    else if (RootFolder.ID == folderInfo.ParentID)
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
                    else
                    {
                        InventoryFolderImpl folder = RootFolder.HasSubFolder(folderInfo.ParentID);
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
                    
                    ResolvePendingFolders(folderInfo);
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CACHE] {0}", e);
            }
        }

        /// <summary>
        /// Callback invoked when an item is received from an async request to the inventory service.
        /// 
        /// FIXME: We're assuming here that items are always received after all the folders have been
        /// received.
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="folderInfo"></param>        
        public void ItemReceive(LLUUID userID, InventoryItemBase itemInfo)
        {
            if ((userID == UserProfile.UUID) && (RootFolder != null))
            {
                if (itemInfo.Folder == RootFolder.ID)
                {
                    if (!RootFolder.Items.ContainsKey(itemInfo.ID))
                    {
                        RootFolder.Items.Add(itemInfo.ID, itemInfo);
                    }
                }
                else
                {
                    InventoryFolderImpl folder = RootFolder.HasSubFolder(itemInfo.Folder);
                    if (folder != null)
                    {
                        if (!folder.Items.ContainsKey(itemInfo.ID))
                        {
                            folder.Items.Add(itemInfo.ID, itemInfo);
                        }
                    }
                }
            }
        }

        public void AddItem(LLUUID userID, InventoryItemBase itemInfo)
        {
            if ((userID == UserProfile.UUID) && (RootFolder != null))
            {
                ItemReceive(userID, itemInfo);
                m_parentCommsManager.InventoryService.AddNewInventoryItem(userID, itemInfo);
            }
        }

        public void UpdateItem(LLUUID userID, InventoryItemBase itemInfo)
        {
            if ((userID == UserProfile.UUID) && (RootFolder != null))
            {
                m_parentCommsManager.InventoryService.AddNewInventoryItem(userID, itemInfo);
            }
        }

        public bool DeleteItem(LLUUID userID, InventoryItemBase item)
        {
            bool result = false;
            if ((userID == UserProfile.UUID) && (RootFolder != null))
            {
                result = RootFolder.DeleteItem(item.ID);
                if (result)
                {
                    m_parentCommsManager.InventoryService.DeleteInventoryItem(userID, item);
                }
            }
            return result;
        }
    }
}
