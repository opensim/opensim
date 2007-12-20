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
* 
*/

using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Console;
using libsecondlife;

namespace OpenSim.Grid.InventoryServer
{
    public class GridInventoryService : InventoryServiceBase
    {
        public override void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack,
                                                    InventoryItemInfo itemCallBack)
        {

        }

        private bool TryGetUsersInventory(LLUUID userID, out List<InventoryFolderBase> folderList, out List<InventoryItemBase> itemsList)
        {
            List<InventoryFolderBase> rootFolders = RequestFirstLevelFolders(userID);
            List<InventoryItemBase> allItems = new List<InventoryItemBase>();
            List<InventoryFolderBase> allFolders = new List<InventoryFolderBase>();

            if (rootFolders != null)
            {
                allFolders.InsertRange(0, rootFolders);
                foreach (InventoryFolderBase subfolder in rootFolders)
                {
                    List<InventoryFolderBase> subFolders = GetAllFolders(subfolder.folderID);
                    if (subFolders != null)
                    {
                        allFolders.InsertRange(0, subFolders);
                    }
                }
            }

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
            
            MainLog.Instance.Verbose("INVENTORY", "Request for inventory for " + userID.ToString());            
            
            InventoryCollection invCollection = new InventoryCollection();
            List<InventoryFolderBase> folders;
            List<InventoryItemBase> allItems;
            if (TryGetUsersInventory(userID, out folders, out allItems))
            {
                invCollection.AllItems = allItems;
                invCollection.Folders = folders;
                invCollection.UserID = userID;
            }
            return invCollection;
        }

        public bool CreateUsersInventory(Guid rawUserID)
        {
            LLUUID userID = new LLUUID(rawUserID);
            
            MainLog.Instance.Verbose(
                "INVENTORY", "Creating new set of inventory folders for " + userID.ToString());
            
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
            MainLog.Instance.Verbose(
                "INVENTORY",
                "Updating in   " + folder.parentID.ToString()
                    + ", folder " + folder.name);
            
            AddNewInventoryFolder(folder.agentID, folder);
            return true;
        }

        public bool MoveInventoryFolder(InventoryFolderBase folder)
        {            
            MainLog.Instance.Verbose(
                "INVENTORY",
                "Moving folder " + folder.folderID
                    + " to " + folder.parentID.ToString());
            
            MoveExistingInventoryFolder(folder);
            return true;
        }

        public bool AddInventoryItem( InventoryItemBase item)
        {
            // Right now, this actions act more like an update/insert combination than a simple create.
            MainLog.Instance.Verbose(
                "INVENTORY", 
                "Updating in   " + item.parentFolderID.ToString()
                    + ", item " + item.inventoryName);

            AddNewInventoryItem(item.avatarID, item);
            return true;
        }

        public override void DeleteInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            // extra spaces to align with other inventory messages
            MainLog.Instance.Verbose(
                "INVENTORY",
                "Deleting in   " + item.parentFolderID.ToString()
                    + ", item " + item.inventoryName);
            
            DeleteItem(item);
        }

        public bool DeleteInvItem( InventoryItemBase item)
        {
            DeleteInventoryItem(item.avatarID, item);
            return true;
        }
    }
}
