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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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
using OpenSim.Framework.Communications.Cache;

namespace OpenSim.Framework.Communications
{
    public delegate void InventoryFolderInfo(LLUUID userID, InventoryFolderImpl folderInfo);

    public delegate void InventoryItemInfo(LLUUID userID, InventoryItemBase itemInfo);

    /// <summary>
    /// Defines all the operations one can perform on a user's inventory.
    /// </summary>
    public interface IInventoryServices
    {
        void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack, InventoryItemInfo itemCallBack);
        
        /// <summary>
        /// Add a new folder to the given user's inventory
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="folder"></param>
        void AddNewInventoryFolder(LLUUID userID, InventoryFolderBase folder);
        
        void MoveInventoryFolder(LLUUID userID, InventoryFolderBase folder);
        
        /// <summary>
        /// Add a new item to the given user's inventory
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="item"></param>
        void AddNewInventoryItem(LLUUID userID, InventoryItemBase item);
        
        /// <summary>
        /// Delete an item from the given user's inventory
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="item"></param>
        void DeleteInventoryItem(LLUUID userID, InventoryItemBase item);
        
        /// <summary>
        /// Create a new inventory for the given user
        /// </summary>
        /// <param name="user"></param>
        void CreateNewUserInventory(LLUUID user);
        
        bool HasInventoryForUser(LLUUID userID);

        /// <summary>
        /// Retrieve the root inventory folder for the given user.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>null if no root folder was found</returns>
        InventoryFolderBase RequestRootFolder(LLUUID userID);

        /// <summary>
        /// Returns the root folder plus any folders in root (so down one level in the Inventory folders tree)
        /// for the given user.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        List<InventoryFolderBase> RequestFirstLevelFolders(LLUUID userID);
    }
}