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
using OpenSim.Framework;
using OpenSim.Framework.Communications;

namespace OpenSim.Grid.InventoryServer
{
    /// <summary>
    /// Used on a grid server to satisfy external inventory requests
    /// </summary>
    public class GridInventoryService : InventoryServiceBase
    {
        private static readonly ILog m_log 
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public override void RequestInventoryForUser(LLUUID userID, InventoryReceiptCallback callback)
        {
        }

        /// <summary>
        /// Get a user's inventory.
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="folderList"></param>
        /// <param name="itemsList"></param>
        /// <returns>true if the inventory was retrieved, false otherwise</returns>
        private bool GetUsersInventory(LLUUID userID, out List<InventoryFolderBase> folderList,
                                          out List<InventoryItemBase> itemsList)
        {
            List<InventoryFolderBase> allFolders = GetInventorySkeleton(userID);
            List<InventoryItemBase> allItems = new List<InventoryItemBase>();

            foreach (InventoryFolderBase folder in allFolders)
            {
                List<InventoryItemBase> items = RequestFolderItems(folder.ID);
                if (items != null)
                {
                    allItems.InsertRange(0, items);
                }
            }

            folderList = allFolders;
            itemsList = allItems;
            if (folderList != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private List<InventoryFolderBase> GetAllFolders(LLUUID folder)
        {
            List<InventoryFolderBase> allFolders = new List<InventoryFolderBase>();
            List<InventoryFolderBase> folders = RequestSubFolders(folder);
            if (folders != null)
            {
                allFolders.InsertRange(0, folders);
                foreach (InventoryFolderBase subfolder in folders)
                {
                    List<InventoryFolderBase> subFolders = GetAllFolders(subfolder.ID);
                    if (subFolders != null)
                    {
                        allFolders.InsertRange(0, subFolders);
                    }
                }
            }
            return allFolders;
        }

        /// <summary>
        /// Return a user's entire inventory
        /// </summary>
        /// <param name="rawUserID"></param>
        /// <returns>The user's inventory.  If an inventory cannot be found then an empty collection is returned.</returns>
        public InventoryCollection GetUserInventory(Guid rawUserID)
        {
            // uncomment me to simulate an overloaded inventory server
            //Thread.Sleep(25000);
            
            LLUUID userID = new LLUUID(rawUserID);

            m_log.InfoFormat("[GRID AGENT INVENTORY]: Processing request for inventory of {0}", userID);            

            InventoryCollection invCollection = new InventoryCollection();
            
            List<InventoryFolderBase> allFolders = GetInventorySkeleton(userID);
            
            if (null == allFolders)
            {
                m_log.WarnFormat("[GRID AGENT INVENTORY]: No inventory found for user {0}", rawUserID);
                
                return invCollection;
            }
            
            List<InventoryItemBase> allItems = new List<InventoryItemBase>();

            foreach (InventoryFolderBase folder in allFolders)
            {
                List<InventoryItemBase> items = RequestFolderItems(folder.ID);
                
                if (items != null)
                {
                    allItems.InsertRange(0, items);
                }
            }

            invCollection.UserID = userID;
            invCollection.Folders = allFolders;            
            invCollection.Items = allItems;            
            
//            foreach (InventoryFolderBase folder in invCollection.Folders)
//            {
//                m_log.DebugFormat("[GRID AGENT INVENTORY]: Sending back folder {0} {1}", folder.Name, folder.ID);
//            }
//            
//            foreach (InventoryItemBase item in invCollection.Items)
//            {
//                m_log.DebugFormat("[GRID AGENT INVENTORY]: Sending back item {0} {1}, folder {2}", item.Name, item.ID, item.Folder);
//            }
            
            m_log.InfoFormat(
                "[GRID AGENT INVENTORY]: Sending back inventory response to user {0} containing {1} folders and {2} items",
                invCollection.UserID, invCollection.Folders.Count, invCollection.Items.Count);            
                        
            return invCollection;
        }
                
        /// <summary>
        /// Guid to UUID wrapper for same name IInventoryServices method
        /// </summary>
        /// <param name="rawUserID"></param>
        /// <returns></returns>        
        public List<InventoryFolderBase> GetInventorySkeleton(Guid rawUserID)
        {
            LLUUID userID = new LLUUID(rawUserID);
            return GetInventorySkeleton(userID);
        }        

        /// <summary>
        /// Create an inventory for the given user.
        /// </summary>
        /// <param name="rawUserID"></param>
        /// <returns></returns>
        public bool CreateUsersInventory(Guid rawUserID)
        {
            LLUUID userID = new LLUUID(rawUserID);

            m_log.InfoFormat("[GRID AGENT INVENTORY]: Creating new set of inventory folders for user {0}", userID);

            CreateNewUserInventory(userID);
            return true;
        }


        public override void AddNewInventoryFolder(LLUUID userID, InventoryFolderBase folder)
        {
            AddFolder(folder);
        }

        public override void MoveExistingInventoryFolder(InventoryFolderBase folder)
        {
            MoveFolder(folder);
        }

        public override void AddNewInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            AddItem(item);
        }

        public bool AddInventoryFolder(InventoryFolderBase folder)
        {
            // Right now, this actions act more like an update/insert combination than a simple create.
            m_log.InfoFormat("[GRID AGENT INVENTORY]: Creating folder {0} {1} in folder {2}", folder.Name, folder.ID, folder.ParentID);

            AddNewInventoryFolder(folder.Owner, folder);
            return true;
        }

        public bool MoveInventoryFolder(InventoryFolderBase folder)
        {
            m_log.InfoFormat("[GRID AGENT INVENTORY]: Moving folder {0} {1} to folder {2}", folder.Name, folder.ID, folder.ParentID);

            MoveExistingInventoryFolder(folder);
            return true;
        }

        public bool AddInventoryItem(InventoryItemBase item)
        {
            // Right now, this actions act more like an update/insert combination than a simple create.
            m_log.InfoFormat("[GRID AGENT INVENTORY]: Adding item {0} {1} to folder {2}", item.Name, item.ID, item.Folder);

            AddNewInventoryItem(item.Owner, item);
            return true;
        }

        public override void DeleteInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            m_log.InfoFormat("[GRID AGENT INVENTORY]: Deleting item {0} {1} from folder {2}", item.Name, item.ID, item.Folder);

            DeleteItem(item);
        }

        /// <summary>
        /// FIXME: Get DeleteInventoryItem to return a bool
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool DeleteInvItem(InventoryItemBase item)
        {
            DeleteInventoryItem(item.Owner, item);
            return true;
        }
    }
}
