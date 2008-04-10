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
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Communications
{
    public abstract class InventoryServiceBase : IInventoryServices
    {
        private static readonly log4net.ILog m_log 
            = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected Dictionary<string, IInventoryData> m_plugins = new Dictionary<string, IInventoryData>();

        #region Plugin methods
        
        /// <summary>
        /// Adds a new user server plugin - plugins will be requested in the order they were loaded.
        /// </summary>
        /// <param name="FileName">The filename to the user server plugin DLL</param>
        public void AddPlugin(string FileName)
        {
            if (!String.IsNullOrEmpty(FileName))
            {
                m_log.Info("[AGENTINVENTORY]: Inventory storage: Attempting to load " + FileName);
                Assembly pluginAssembly = Assembly.LoadFrom(FileName);

                foreach (Type pluginType in pluginAssembly.GetTypes())
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("IInventoryData", true);

                        if (typeInterface != null)
                        {
                            IInventoryData plug =
                                (IInventoryData) Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            plug.Initialise();
                            m_plugins.Add(plug.getName(), plug);
                            m_log.Info("[AGENTINVENTORY]: Added IInventoryData Interface");
                        }
                    }
                }
            }
        }
        
        #endregion
        
        #region IInventoryServices methods        
        
        // See IInventoryServices
        public List<InventoryFolderBase> GetInventorySkeleton(LLUUID userId)
        {
//            m_log.DebugFormat("[AGENT INVENTORY]: Getting inventory skeleton for {0}", userId);
                                            
            InventoryFolderBase rootFolder = RequestRootFolder(userId);
            
            // Agent is completely new and has no inventory structure yet.
            if (null == rootFolder)
            {
                return null;                    
            }            
            
            List<InventoryFolderBase> userFolders = new List<InventoryFolderBase>();                        
            
            userFolders.Add(rootFolder);
                         
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                IList<InventoryFolderBase> folders = plugin.Value.getFolderHierarchy(rootFolder.ID);
                userFolders.AddRange(folders);
            }    
            
//            foreach (InventoryFolderBase folder in userFolders)
//            {
//                m_log.DebugFormat("[AGENT INVENTORY]: Got folder {0} {1}", folder.name, folder.folderID);
//            }
            
            return userFolders;
        }

        // See IInventoryServices
        public void MoveInventoryFolder(LLUUID userID, InventoryFolderBase folder)
        {
            // FIXME: Probably doesn't do what was originally intended - only ever queries the first plugin
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                plugin.Value.moveInventoryFolder(folder);
            }
        }
        
        // See IInventoryServices
        public virtual bool HasInventoryForUser(LLUUID userID)
        {
            return false;
        }

        // See IInventoryServices
        public InventoryFolderBase RequestRootFolder(LLUUID userID)
        {
            // FIXME: Probably doesn't do what was originally intended - only ever queries the first plugin
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                return plugin.Value.getUserRootFolder(userID);
            }
            return null;
        }

        // See IInventoryServices
        public void CreateNewUserInventory(LLUUID user)
        {
            InventoryFolderBase existingRootFolder = RequestRootFolder(user);
            
            if (null != existingRootFolder)
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Did not create a new inventory for user {0} since they already have "
                    + "a root inventory folder with id {1}", 
                    user, existingRootFolder.ID);
            }
            else
            {                
                UsersInventory inven = new UsersInventory();
                inven.CreateNewInventorySet(user);
                AddNewInventorySet(inven);
            }
        }      
        
        public abstract void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack,
                                                     InventoryItemInfo itemCallBack);
        public abstract void AddNewInventoryFolder(LLUUID userID, InventoryFolderBase folder);
        public abstract void MoveExistingInventoryFolder(InventoryFolderBase folder);
        public abstract void AddNewInventoryItem(LLUUID userID, InventoryItemBase item);
        public abstract void DeleteInventoryItem(LLUUID userID, InventoryItemBase item);        
        
        #endregion
        
        #region Methods used by GridInventoryService

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
        
        #endregion

        protected void AddFolder(InventoryFolderBase folder)
        {
            m_log.DebugFormat(
                "[INVENTORY SERVICE BASE]: Adding folder {0}, {1} to {2}", folder.Name, folder.ID, folder.ParentID);
            
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                plugin.Value.addInventoryFolder(folder);
            }
        }

        protected void MoveFolder(InventoryFolderBase folder)
        {
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                plugin.Value.moveInventoryFolder(folder);
            }
        }

        protected void AddItem(InventoryItemBase item)
        {
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                plugin.Value.addInventoryItem(item);
            }
        }

        protected void DeleteItem(InventoryItemBase item)
        {
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                plugin.Value.deleteInventoryItem(item.ID);
            }
        }
        
        private void AddNewInventorySet(UsersInventory inventory)
        {
            foreach (InventoryFolderBase folder in inventory.Folders.Values)
            {
                AddFolder(folder);
            }
        }

        private class UsersInventory
        {
            public Dictionary<LLUUID, InventoryFolderBase> Folders = new Dictionary<LLUUID, InventoryFolderBase>();
            public Dictionary<LLUUID, InventoryItemBase> Items = new Dictionary<LLUUID, InventoryItemBase>();

            public virtual void CreateNewInventorySet(LLUUID user)
            {
                InventoryFolderBase folder = new InventoryFolderBase();

                folder.ParentID = LLUUID.Zero;
                folder.Owner = user;
                folder.ID = LLUUID.Random();
                folder.Name = "My Inventory";
                folder.Type = (short)AssetType.Folder;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                LLUUID rootFolder = folder.ID;

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = LLUUID.Random();
                folder.Name = "Animations";
                folder.Type = (short)AssetType.Animation;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = LLUUID.Random();
                folder.Name = "Body Parts";
                folder.Type = (short)AssetType.Bodypart;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);
                
                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = LLUUID.Random();
                folder.Name = "Calling Cards";
                folder.Type = (short)AssetType.CallingCard;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);                

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = LLUUID.Random();
                folder.Name = "Clothing";
                folder.Type = (short)AssetType.Clothing;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = LLUUID.Random();
                folder.Name = "Gestures";
                folder.Type = (short)AssetType.Gesture;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = LLUUID.Random();
                folder.Name = "Landmarks";
                folder.Type = (short)AssetType.Landmark;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = LLUUID.Random();
                folder.Name = "Lost And Found";
                folder.Type = (short)AssetType.LostAndFoundFolder;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = LLUUID.Random();
                folder.Name = "Notecards";
                folder.Type = (short)AssetType.Notecard;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = LLUUID.Random();
                folder.Name = "Objects";
                folder.Type = (short)AssetType.Object;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = LLUUID.Random();
                folder.Name = "Photo Album";
                folder.Type = (short)AssetType.SnapshotFolder;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = LLUUID.Random();
                folder.Name = "Scripts";
                folder.Type = (short)AssetType.LSLText;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = LLUUID.Random();
                folder.Name = "Sounds";
                folder.Type = (short)AssetType.Sound;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = LLUUID.Random();
                folder.Name = "Textures";
                folder.Type = (short)AssetType.Texture;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);

                folder = new InventoryFolderBase();
                folder.ParentID = rootFolder;
                folder.Owner = user;
                folder.ID = LLUUID.Random();
                folder.Name = "Trash";
                folder.Type = (short)AssetType.TrashFolder;
                folder.Version = 1;
                Folders.Add(folder.ID, folder);
            }
        }
    }
}
