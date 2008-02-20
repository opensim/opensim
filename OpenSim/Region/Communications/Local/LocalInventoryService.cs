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

using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;

namespace OpenSim.Region.Communications.Local
{
    /// <summary>
    /// An implementation of user inventory where the inventory is held locally (e.g. when OpenSim is
    /// operating in standalone mode. 
    /// </summary>
    public class LocalInventoryService : InventoryServiceBase
    {
        public override void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack,
                                                     InventoryItemInfo itemCallBack)
        {
            List<InventoryFolderBase> folders = RequestFirstLevelFolders(userID);
            InventoryFolderImpl rootFolder = null;

            //need to make sure we send root folder first
            foreach (InventoryFolderBase folder in folders)
            {
                if (folder.parentID == LLUUID.Zero)
                {
                    rootFolder = RequestInventoryFolder(userID, folder, folderCallBack, itemCallBack);
                }
            }

            if (rootFolder != null)
            {
                foreach (InventoryFolderBase folder in folders)
                {
                    if (folder.folderID != rootFolder.folderID)
                    {
                        RequestInventoryFolder(userID, folder, folderCallBack, itemCallBack);
                    }
                }
            }
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

        public override void DeleteInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            DeleteItem(item);
        }

        public override bool HasInventoryForUser(LLUUID userID)
        {
            InventoryFolderBase root = RequestUsersRoot(userID);
            if (root == null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public override InventoryFolderBase RequestNamedFolder(LLUUID userID, string folderName)
        {
             List<InventoryFolderBase> folders = RequestFirstLevelFolders(userID);
             InventoryFolderBase requestedFolder = null;

            //need to make sure we send root folder first
            foreach (InventoryFolderBase folder in folders)
            {
                if (folder.name == folderName)
                {
                    requestedFolder = folder;
                    break;
                }
            }

            return requestedFolder;
        }

        /// <summary>
        /// Send the given inventory folder and its item contents back to the requester.
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="folder"></param>
        private InventoryFolderImpl RequestInventoryFolder(LLUUID userID, InventoryFolderBase folder,
                                                           InventoryFolderInfo folderCallBack,
                                                           InventoryItemInfo itemCallBack)
        {
            InventoryFolderImpl newFolder = new InventoryFolderImpl(folder);
            folderCallBack(userID, newFolder);

            List<InventoryItemBase> items = RequestFolderItems(newFolder.folderID);
            foreach (InventoryItemBase item in items)
            {
                itemCallBack(userID, item);
            }

            return newFolder;
        }
    }
}