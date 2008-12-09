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
using OpenMetaverse;
//using System.Reflection;

//using log4net;

namespace OpenSim.Framework.Communications.Cache
{
    public class InventoryFolderImpl : InventoryFolderBase
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static readonly string PATH_DELIMITER = "/";

        /// <summary>
        /// Items that are contained in this folder
        /// </summary>
        public Dictionary<UUID, InventoryItemBase> Items = new Dictionary<UUID, InventoryItemBase>();

        /// <summary>
        /// Child folders that are contained in this folder
        /// </summary>
        public Dictionary<UUID, InventoryFolderImpl> SubFolders = new Dictionary<UUID, InventoryFolderImpl>();

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
        public InventoryFolderImpl CreateChildFolder(UUID folderID, string folderName, ushort type)
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
        /// Add a folder that already exists.
        /// </summary>
        /// <param name="folder"></param>
        public void AddChildFolder(InventoryFolderImpl folder)
        {
            lock (SubFolders)
            {
                folder.ParentID = ID;
                SubFolders[folder.ID] = folder;
            }                        
        }
        
        /// <summary>
        /// Removes the given child subfolder.
        /// </summary>
        /// <param name="folderID"></param>
        /// <returns>
        /// The folder removed, or null if the folder was not present.
        /// </returns>
        public InventoryFolderImpl RemoveChildFolder(UUID folderID)
        {
            InventoryFolderImpl removedFolder = null;
            
            lock (SubFolders)
            {
                if (SubFolders.ContainsKey(folderID))
                {
                    removedFolder = SubFolders[folderID];
                    SubFolders.Remove(folderID);
                }                    
            }
            
            return removedFolder;
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
        /// Returns the item if it exists in this folder or in any of this folder's descendant folders
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>null if the item is not found</returns>
        public InventoryItemBase FindItem(UUID itemID)
        {
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
                    InventoryItemBase item = folder.FindItem(itemID);

                    if (item != null)
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        public InventoryItemBase FindAsset(UUID assetID)
        {
            lock (Items)
            {
                foreach (InventoryItemBase item in Items.Values)
                {
                    if (item.AssetID == assetID)
                        return item;
                }
            }

            lock (SubFolders)
            {
                foreach (InventoryFolderImpl folder in SubFolders.Values)
                {
                    InventoryItemBase item = folder.FindAsset(assetID);

                    if (item != null)
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Deletes an item if it exists in this folder or any children
        /// </summary>
        /// <param name="folderID"></param>
        /// <returns></returns>
        public bool DeleteItem(UUID itemID)
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
        /// Returns the folder requested if it is this folder or is a descendent of this folder.  The search is depth
        /// first.
        /// </summary>
        /// <returns>The requested folder if it exists, null if it does not.</returns>
        public InventoryFolderImpl FindFolder(UUID folderID)
        {
            if (folderID == ID)
                return this;

            lock (SubFolders)
            {
                foreach (InventoryFolderImpl folder in SubFolders.Values)
                {
                    InventoryFolderImpl returnFolder = folder.FindFolder(folderID);

                    if (returnFolder != null)
                        return returnFolder;
                }
            }

            return null;
        }

        /// <summary>
        /// Find a folder given a PATH_DELIMITOR delimited path starting from this folder
        ///
        /// This method does not handle paths that contain multiple delimitors
        ///
        /// FIXME: We do not yet handle situations where folders have the same name.  We could handle this by some
        /// XPath like expression
        ///
        /// FIXME: Delimitors which occur in names themselves are not currently escapable.
        /// </summary>
        /// <param name="path">
        /// The path to the required folder.  It this is empty then this folder itself is returned.
        /// </param>
        /// <returns>null if the folder is not found</returns>
        public InventoryFolderImpl FindFolderByPath(string path)
        {
            if (path == string.Empty)
                return this;

            string[] components = path.Split(new string[] { PATH_DELIMITER }, 2, StringSplitOptions.None);

            lock (SubFolders)
            {
                foreach (InventoryFolderImpl folder in SubFolders.Values)
                {
                    if (folder.Name == components[0])
                        if (components.Length > 1)
                            return folder.FindFolderByPath(components[1]);
                        else
                            return folder;
                }
            }

            // We didn't find a folder with the given name
            return null;
        }

        /// <summary>
        /// Find an item given a PATH_DELIMITOR delimited path starting from this folder.
        ///
        /// This method does not handle paths that contain multiple delimitors
        ///
        /// FIXME: We do not yet handle situations where folders or items have the same name.  We could handle this by some
        /// XPath like expression
        ///
        /// FIXME: Delimitors which occur in names themselves are not currently escapable.
        /// </summary>
        /// <param name="path">
        /// The path to the required item.
        /// </param>
        /// <returns>null if the item is not found</returns>
        public InventoryItemBase FindItemByPath(string path)
        {
            string[] components = path.Split(new string[] { PATH_DELIMITER }, 2, StringSplitOptions.None);

            if (components.Length == 1)
            {
                lock (Items)
                {
                    foreach (InventoryItemBase item in Items.Values)
                    {
                        if (item.Name == components[0])
                            return item;
                    }
                }
            }
            else
            {
                lock (SubFolders)
                {
                    foreach (InventoryFolderImpl folder in SubFolders.Values)
                    {
                        if (folder.Name == components[0])
                            return folder.FindItemByPath(components[1]);
                    }
                }
            }

            // We didn't find an item or intermediate folder with the given name
            return null;
        }

        /// <summary>
        /// Return a copy of the list of child items in this folder
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
        /// Return a copy of the list of child folders in this folder.
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

        public List<InventoryFolderImpl> RequestListOfFolderImpls()
        {
            List<InventoryFolderImpl> folderList = new List<InventoryFolderImpl>();

            lock (SubFolders)
            {
                foreach (InventoryFolderImpl folder in SubFolders.Values)
                {
                    folderList.Add(folder);
                }
            }

            return folderList;
        }

        /// <value>
        /// The total number of items in this folder and in the immediate child folders (though not from other
        /// descendants).
        /// </value>
        public int TotalCount
        {
            get
            {
                int total = Items.Count;

                foreach (InventoryFolderImpl folder in SubFolders.Values)
                {
                    total = total + folder.TotalCount;
                }

                return total;
            }
        }
    }
}
