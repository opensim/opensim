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

using System;
using System.Collections.Generic;
using System.Reflection;
using libsecondlife;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Console;
using OpenSim.Framework.Types;
using InventoryFolder=OpenSim.Framework.Communications.Caches.InventoryFolder;

namespace OpenSim.Framework.Communications
{
    public abstract class InventoryServiceBase : IInventoryServices
    {
        protected Dictionary<string, IInventoryData> m_plugins = new Dictionary<string, IInventoryData>();
        //protected IAssetServer m_assetServer;

        public InventoryServiceBase()
        {
            //m_assetServer = assetServer;
        }

        /// <summary>
        /// Adds a new user server plugin - plugins will be requested in the order they were loaded.
        /// </summary>
        /// <param name="FileName">The filename to the user server plugin DLL</param>
        public void AddPlugin(string FileName)
        {
            if (!String.IsNullOrEmpty(FileName))
            {
                MainLog.Instance.Verbose("Inventory", "Inventorystorage: Attempting to load " + FileName);
                Assembly pluginAssembly = Assembly.LoadFrom(FileName);

                foreach (Type pluginType in pluginAssembly.GetTypes())
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("IInventoryData", true);

                        if (typeInterface != null)
                        {
                            IInventoryData plug =
                                (IInventoryData)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            plug.Initialise();
                            this.m_plugins.Add(plug.getName(), plug);
                            MainLog.Instance.Verbose("Inventorystorage: Added IInventoryData Interface");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the root folder plus any folders in root (so down one level in the Inventory folders tree)
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> RequestFirstLevelFolders(LLUUID userID)
        {
            List<InventoryFolderBase> inventoryList = new List<InventoryFolderBase>();
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                InventoryFolderBase rootFolder = plugin.Value.getUserRootFolder(userID);
                if (rootFolder != null)
                {
                    inventoryList = plugin.Value.getInventoryFolders(rootFolder.folderID);
                    inventoryList.Insert(0, rootFolder);
                    return inventoryList;
                }
            }
            return inventoryList;
        }

        /// <summary>
        /// 
        /// </summary>
        public InventoryFolderBase RequestUsersRoot(LLUUID userID)
        {
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                return plugin.Value.getUserRootFolder(userID);
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parentFolderID"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> RequestSubFolders(LLUUID parentFolderID)
        {
            List<InventoryFolderBase> inventoryList = new List<InventoryFolderBase>();
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                return plugin.Value.getInventoryFolders(parentFolderID);
            }
            return inventoryList;
        }

        public List<InventoryItemBase> RequestFolderItems(LLUUID folderID)
        {
            List<InventoryItemBase> itemsList = new List<InventoryItemBase>();
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                itemsList = plugin.Value.getInventoryInFolder(folderID);
                return itemsList;
            }
            return itemsList;
        }

        public void AddFolder(InventoryFolderBase folder)
        {
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                plugin.Value.addInventoryFolder(folder);
            }
        }

        public void AddItem(InventoryItemBase item)
        {
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                plugin.Value.addInventoryItem(item);
            }
        }

        public void deleteItem(InventoryItemBase item)
        {
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                plugin.Value.deleteInventoryItem(item.inventoryID);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inventory"></param>
        public void AddNewInventorySet(UsersInventory inventory)
        {
            foreach (InventoryFolderBase folder in inventory.Folders.Values)
            {
                this.AddFolder(folder);
            }
        }

        public void CreateNewUserInventory(LLUUID user)
        {
            UsersInventory inven = new UsersInventory();
            inven.CreateNewInventorySet(user);
            this.AddNewInventorySet(inven);
        }

        public class UsersInventory
        {
            public Dictionary<LLUUID, InventoryFolderBase> Folders = new Dictionary<LLUUID, InventoryFolderBase>();
            public Dictionary<LLUUID, InventoryItemBase> Items = new Dictionary<LLUUID, InventoryItemBase>();

            public UsersInventory()
            {

            }

            public virtual void CreateNewInventorySet(LLUUID user)
            {
                InventoryFolderBase folder = new InventoryFolderBase();
                folder.parentID = LLUUID.Zero;
                folder.agentID = user;
                folder.folderID = LLUUID.Random();
                folder.name = "My Inventory";
                folder.type = 8;
                folder.version = 1;
                Folders.Add(folder.folderID, folder);

                LLUUID rootFolder = folder.folderID;

                folder = new InventoryFolderBase();
                folder.parentID = rootFolder;
                folder.agentID = user;
                folder.folderID = LLUUID.Random();
                folder.name = "Textures";
                folder.type = 0;
                folder.version = 1;
                Folders.Add(folder.folderID, folder);

                folder = new InventoryFolderBase();
                folder.parentID = rootFolder;
                folder.agentID = user;
                folder.folderID = LLUUID.Random();
                folder.name = "Objects";
                folder.type = 6;
                folder.version = 1;
                Folders.Add(folder.folderID, folder);

                folder = new InventoryFolderBase();
                folder.parentID = rootFolder;
                folder.agentID = user;
                folder.folderID = LLUUID.Random();
                folder.name = "Clothes";
                folder.type = 5;
                folder.version = 1;
                Folders.Add(folder.folderID, folder);
            }
        }

        public abstract void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack, InventoryItemInfo itemCallBack);
        public abstract void AddNewInventoryFolder(LLUUID userID, InventoryFolder folder);
        public abstract void AddNewInventoryItem(LLUUID userID, InventoryItemBase item);
        public abstract void DeleteInventoryItem(LLUUID userID, InventoryItemBase item);
    }
}
