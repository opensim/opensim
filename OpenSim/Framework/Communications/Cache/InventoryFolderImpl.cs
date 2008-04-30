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

using System.Collections.Generic;
//using System.Reflection;

using libsecondlife;
//using log4net;

namespace OpenSim.Framework.Communications.Cache
{
    public class InventoryFolderImpl : InventoryFolderBase
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        // Fields
        public Dictionary<LLUUID, InventoryItemBase> Items = new Dictionary<LLUUID, InventoryItemBase>();
        public Dictionary<LLUUID, InventoryFolderImpl> SubFolders = new Dictionary<LLUUID, InventoryFolderImpl>();

        // Accessors
        public int SubFoldersCount
        {
            get { return SubFolders.Count; }
        }

        // Constructors
        public InventoryFolderImpl(InventoryFolderBase folderbase)
        {
            Owner = folderbase.Owner;
            ID = folderbase.ID;
            Name = folderbase.Name;
            ParentID = folderbase.ParentID;
            Type = folderbase.Type;
            Version = folderbase.Version;
        }

        public InventoryFolderImpl()
        {
        }

        /// <summary>
        /// Create a new subfolder.  This exists only in the cache.
        /// </summary>
        /// <param name="folderID"></param>
        /// <param name="folderName"></param>
        /// <param name="type"></param>
        /// <returns>The newly created subfolder.  Returns null if the folder already exists</returns>
        public InventoryFolderImpl CreateNewSubFolder(LLUUID folderID, string folderName, ushort type)
        {
            lock (SubFolders)
            {
                if (!SubFolders.ContainsKey(folderID))
                {
                    InventoryFolderImpl subFold = new InventoryFolderImpl();
                    subFold.Name = folderName;
                    subFold.ID = folderID;
                    subFold.Type = (short) type;
                    subFold.ParentID = this.ID;
                    subFold.Owner = Owner;
                    SubFolders.Add(subFold.ID, subFold);
                    return subFold;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Delete all the folders and items in this folder.
        /// </summary>
        public void Purge()
        {
            foreach (InventoryFolderImpl folder in SubFolders.Values)
            {
                folder.Purge();                
            }
            
            SubFolders.Clear();
            Items.Clear();
        }

        /// <summary>
        /// Does this folder or any of its subfolders contain the given item?
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns></returns>
        public InventoryItemBase HasItem(LLUUID itemID)
        {
            InventoryItemBase base2 = null;
            
            lock (Items)
            {
                if (Items.ContainsKey(itemID))
                {
                    return Items[itemID];
                }
            }
            
            lock (SubFolders)
            {
                foreach (InventoryFolderImpl folder in SubFolders.Values)
                {
                    base2 = folder.HasItem(itemID);
                    if (base2 != null)
                    {
                        break;
                    }
                }
            }
            
            return base2;
        }

        /// <summary>
        /// Delete an item from the folder.
        /// </summary>
        /// <param name="folderID"></param>
        /// <returns></returns>
        public bool DeleteItem(LLUUID itemID)
        {
            bool found = false;
            
            lock (Items)
            {
                if (Items.ContainsKey(itemID))
                {
                    Items.Remove(itemID);
                    return true;
                }
            }
            
            lock (SubFolders)
            {
                foreach (InventoryFolderImpl folder in SubFolders.Values)
                {
                    found = folder.DeleteItem(itemID);
                    if (found == true)
                    {
                        break;
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// Does this folder contain the given subfolder?
        /// </summary>
        /// <returns></returns>
        public InventoryFolderImpl HasSubFolder(LLUUID folderID)
        {            
            InventoryFolderImpl returnFolder = null;
            
            lock (SubFolders)
            {
                if (SubFolders.ContainsKey(folderID))
                {
                    returnFolder = SubFolders[folderID];
                }
                else
                {
                    foreach (InventoryFolderImpl folder in SubFolders.Values)
                    {
                        returnFolder = folder.HasSubFolder(folderID);
                        if (returnFolder != null)
                        {
                            break;
                        }
                    }
                }
            }
            
            return returnFolder;
        }

        /// <summary>
        /// Return the list of items in this folder
        /// </summary>
        public List<InventoryItemBase> RequestListOfItems()
        {
            List<InventoryItemBase> itemList = new List<InventoryItemBase>();
                
            lock (Items)
            {
                foreach (InventoryItemBase item in Items.Values)
                {
                    itemList.Add(item);
                }
            }
            
            //m_log.DebugFormat("[INVENTORY FOLDER IMPL]: Found {0} items", itemList.Count);
            
            return itemList;
        }

        /// <summary>
        /// Return the list of immediate child folders in this folder.
        /// </summary>
        public List<InventoryFolderBase> RequestListOfFolders()
        {            
            List<InventoryFolderBase> folderList = new List<InventoryFolderBase>();
            
            lock (SubFolders)
            {
                foreach (InventoryFolderBase folder in SubFolders.Values)
                {
                    folderList.Add(folder);
                }
            }
            
            return folderList;
        }
    }
}
