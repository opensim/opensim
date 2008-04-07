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
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Console;

namespace OpenSim.Grid.InventoryServer
{
    public class GridInventoryService : InventoryServiceBase
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public override void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack,
                                                     InventoryItemInfo itemCallBack)
        {
        }

        private bool TryGetUsersInventory(LLUUID userID, out List<InventoryFolderBase> folderList,
                                          out List<InventoryItemBase> itemsList)
        {
            List<InventoryFolderBase> allFolders = GetInventorySkeleton(userID);
            List<InventoryItemBase> allItems = new List<InventoryItemBase>();

            foreach (InventoryFolderBase folder in allFolders)
            {
                List<InventoryItemBase> items = RequestFolderItems(folder.folderID);
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
                    List<InventoryFolderBase> subFolders = GetAllFolders(subfolder.folderID);
                    if (subFolders != null)
                    {
                        allFolders.InsertRange(0, subFolders);
                    }
                }
            }
            return allFolders;
        }


        public InventoryCollection GetUserInventory(Guid rawUserID)
        {
            LLUUID userID = new LLUUID(rawUserID);

            m_log.Info("[AGENT INVENTORY]: Processing request for inventory of " + userID.ToString());            

            InventoryCollection invCollection = new InventoryCollection();
            List<InventoryFolderBase> folders;
            List<InventoryItemBase> allItems;
            if (TryGetUsersInventory(userID, out folders, out allItems))
            {
                invCollection.AllItems = allItems;
                invCollection.Folders = folders;
                invCollection.UserID = userID;
            }
            
//            foreach (InventoryFolderBase folder in folders)
//            {
//                m_log.DebugFormat(
//                    "[AGENT INVENTORY]: Sending back folder {0}, {1}", 
//                    folder.name, folder.folderID);
//            }
//            
//            foreach (InventoryItemBase item in allItems)
//            {
//                m_log.DebugFormat(
//                    "[AGENT INVENTORY]: Sending back item {0}, {1}, folder {2}", 
//                    item.inventoryName, item.inventoryID, item.parentFolderID);
//            }
                        
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

        public bool CreateUsersInventory(Guid rawUserID)
        {
            LLUUID userID = new LLUUID(rawUserID);

            m_log.Info(
                "[AGENT INVENTORY]: Creating new set of inventory folders for " + userID.ToString());

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
            m_log.Info(
                "[AGENT INVENTORY]: " +
                "Updating in   " + folder.parentID.ToString()
                + ", folder " + folder.name);

            AddNewInventoryFolder(folder.agentID, folder);
            return true;
        }

        public bool MoveInventoryFolder(InventoryFolderBase folder)
        {
            m_log.Info(
                "[AGENT INVENTORY]: " +
                "Moving folder " + folder.folderID
                + " to " + folder.parentID.ToString());

            MoveExistingInventoryFolder(folder);
            return true;
        }

        public bool AddInventoryItem(InventoryItemBase item)
        {
            // Right now, this actions act more like an update/insert combination than a simple create.
            m_log.Info(
                "[AGENT INVENTORY]: " +
                "Updating in   " + item.Folder.ToString()
                + ", item " + item.Name);

            AddNewInventoryItem(item.Owner, item);
            return true;
        }

        public override void DeleteInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            // extra spaces to align with other inventory messages
            m_log.Info(
                "[AGENT INVENTORY]: " +
                "Deleting in   " + item.Folder.ToString()
                + ", item " + item.Name);

            DeleteItem(item);
        }

        public bool DeleteInvItem(InventoryItemBase item)
        {
            DeleteInventoryItem(item.Owner, item);
            return true;
        }
    }
}
