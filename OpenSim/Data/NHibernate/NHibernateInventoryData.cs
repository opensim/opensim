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
 *     * Neither the name of the OpenSimulator Project nor the
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
using log4net;
using NHibernate;
using NHibernate.Criterion;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.NHibernate
{
    public class NHibernateInventoryData: IInventoryDataPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private NHibernateManager manager;
        public NHibernateManager Manager
        {
            get
            {
                return manager;
            }
        }

        /// <summary>
        /// The plugin being loaded
        /// </summary>
        /// <returns>A string containing the plugin name</returns>
        public string Name
        {
            get { return "NHibernate Inventory Data Interface"; }
        }

        /// <summary>
        /// The plugins version
        /// </summary>
        /// <returns>A string containing the plugin version</returns>
        public string Version
        {
            get
            {
                Module module = GetType().Module;
                // string dllName = module.Assembly.ManifestModule.Name;
                Version dllVersion = module.Assembly.GetName().Version;


                return
                    string.Format("{0}.{1}.{2}.{3}", dllVersion.Major, dllVersion.Minor, dllVersion.Build,
                            dllVersion.Revision);
            }
        }

        public void Initialise() 
        { 
            m_log.Info("[NHibernateInventoryData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException (Name);
        }

        /// <summary>
        /// Initialises the interface
        /// </summary>
        public void Initialise(string connect)
        {
            m_log.InfoFormat("[NHIBERNATE] Initializing NHibernateInventoryData");
            manager = new NHibernateManager(connect, "InventoryStore");
        }

        /// <summary>
        /// Closes the interface
        /// </summary>
        public void Dispose()
        {
        }

        /*****************************************************************
         *
         *   Basic CRUD operations on Data
         *
         ****************************************************************/

        // READ

        /// <summary>
        /// Returns an inventory item by its UUID
        /// </summary>
        /// <param name="item">The UUID of the item to be returned</param>
        /// <returns>A class containing item information</returns>
        public InventoryItemBase getInventoryItem(UUID item)
        {
            try
            {
                m_log.InfoFormat("[NHIBERNATE] getInventoryItem {0}", item);
                return (InventoryItemBase)manager.Get(typeof(InventoryItemBase), item);
            }
            catch
            {
                m_log.ErrorFormat("Couldn't find inventory item: {0}", item);
                return null;
            }
        }

        /// <summary>
        /// Creates a new inventory item based on item
        /// </summary>
        /// <param name="item">The item to be created</param>
        public void addInventoryItem(InventoryItemBase item)
        {
            if (!ExistsItem(item.ID))
            {
                manager.Insert(item);
            }
            else
            {
                m_log.ErrorFormat("[NHIBERNATE] Attempted to add Inventory Item {0} that already exists, updating instead", item.ID);
                updateInventoryItem(item);
            }
        }

        /// <summary>
        /// Updates an inventory item with item (updates based on ID)
        /// </summary>
        /// <param name="item">The updated item</param>
        public void updateInventoryItem(InventoryItemBase item)
        {
            if (ExistsItem(item.ID))
            {
                manager.Update(item);
            }
            else
            {
                m_log.ErrorFormat("[NHIBERNATE] Attempted to add Inventory Item {0} that already exists", item.ID);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="item"></param>
        public void deleteInventoryItem(UUID itemID)
        {
            InventoryItemBase item = (InventoryItemBase)manager.Get(typeof(InventoryItemBase), itemID);
            if (item != null)
            {
                manager.Delete(item);
            }
            else
            {
                m_log.ErrorFormat("[NHIBERNATE] Error deleting InventoryItemBase {0}", itemID);
            }
            
        }

        public InventoryItemBase queryInventoryItem(UUID itemID)
        {
            return null;
        }

        public InventoryFolderBase queryInventoryFolder(UUID folderID)
        {
            return null;
        }

        /// <summary>
        /// Returns an inventory folder by its UUID
        /// </summary>
        /// <param name="folder">The UUID of the folder to be returned</param>
        /// <returns>A class containing folder information</returns>
        public InventoryFolderBase getInventoryFolder(UUID folder)
        {
            try
            {
                return (InventoryFolderBase)manager.Get(typeof(InventoryFolderBase), folder);
            }
            catch
            {
                m_log.ErrorFormat("[NHIBERNATE] Couldn't find inventory item: {0}", folder);
                return null;
            }
        }

        /// <summary>
        /// Creates a new inventory folder based on folder
        /// </summary>
        /// <param name="folder">The folder to be created</param>
        public void addInventoryFolder(InventoryFolderBase folder)
        {
            if (!ExistsFolder(folder.ID))
            {
                manager.Insert(folder);
            }
            else
            {
                m_log.ErrorFormat("[NHIBERNATE] Attempted to add Inventory Folder {0} that already exists, updating instead", folder.ID);
                updateInventoryFolder(folder);
            }
        }

        /// <summary>
        /// Updates an inventory folder with folder (updates based on ID)
        /// </summary>
        /// <param name="folder">The updated folder</param>
        public void updateInventoryFolder(InventoryFolderBase folder)
        {
            if (ExistsFolder(folder.ID))
            {
                manager.Update(folder);
            }
            else
            {
                m_log.ErrorFormat("[NHIBERNATE] Attempted to add Inventory Folder {0} that already exists", folder.ID);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="folder"></param>
        public void deleteInventoryFolder(UUID folderID)
        {
            InventoryFolderBase item = (InventoryFolderBase)manager.Get(typeof(InventoryFolderBase), folderID);
            if (item != null)
            {
                manager.Delete(item);
            }
            else
            {
                m_log.ErrorFormat("[NHIBERNATE] Error deleting InventoryFolderBase {0}", folderID);
            }
            manager.Delete(folderID);
        }

        // useful private methods
        private bool ExistsItem(UUID uuid)
        {
            return (getInventoryItem(uuid) != null) ? true : false;
        }

        private bool ExistsFolder(UUID uuid)
        {
            return (getInventoryFolder(uuid) != null) ? true : false;
        }

        public void Shutdown()
        {
            // TODO: DataSet commit
        }

        // Move seems to be just update

        public void moveInventoryFolder(InventoryFolderBase folder)
        {
            updateInventoryFolder(folder);
        }

        public void moveInventoryItem(InventoryItemBase item)
        {
            updateInventoryItem(item);
        }



        /// <summary>
        /// Returns a list of inventory items contained within the specified folder
        /// </summary>
        /// <param name="folderID">The UUID of the target folder</param>
        /// <returns>A List of InventoryItemBase items</returns>
        public List<InventoryItemBase> getInventoryInFolder(UUID folderID)
        {
            // try {
            ICriteria criteria = manager.GetSession().CreateCriteria(typeof(InventoryItemBase));
            criteria.Add(Expression.Eq("Folder", folderID));
            List<InventoryItemBase> list = new List<InventoryItemBase>();
            foreach (InventoryItemBase item in criteria.List())
            {
                list.Add(item);
            }
            return list;
            //                 }
            //                 catch
            //                 {
            //                     return new List<InventoryItemBase>();
            //                 }
        }

        public List<InventoryFolderBase> getUserRootFolders(UUID user)
        {
            return new List<InventoryFolderBase>();
        }

        // see InventoryItemBase.getUserRootFolder
        public InventoryFolderBase getUserRootFolder(UUID user)
        {
            ICriteria criteria = manager.GetSession().CreateCriteria(typeof(InventoryFolderBase));
            criteria.Add(Expression.Eq("ParentID", UUID.Zero));
            criteria.Add(Expression.Eq("Owner", user));
            foreach (InventoryFolderBase folder in criteria.List())
            {
                return folder;
            }
            m_log.ErrorFormat("No Inventory Root Folder Found for: {0}", user);
            return null;
        }

        /// <summary>
        /// Append a list of all the child folders of a parent folder
        /// </summary>
        /// <param name="folders">list where folders will be appended</param>
        /// <param name="parentID">ID of parent</param>
        private void getInventoryFolders(ref List<InventoryFolderBase> folders, UUID parentID)
        {
            ICriteria criteria = manager.GetSession().CreateCriteria(typeof(InventoryFolderBase));
            criteria.Add(Expression.Eq("ParentID", parentID));
            foreach (InventoryFolderBase item in criteria.List())
            {
                folders.Add(item);
            }
        }

        /// <summary>
        /// Returns a list of inventory folders contained in the folder 'parentID'
        /// </summary>
        /// <param name="parentID">The folder to get subfolders for</param>
        /// <returns>A list of inventory folders</returns>
        public List<InventoryFolderBase> getInventoryFolders(UUID parentID)
        {
            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            getInventoryFolders(ref folders, parentID);
            return folders;
        }

        // See IInventoryDataPlugin
        public List<InventoryFolderBase> getFolderHierarchy(UUID parentID)
        {
            if (parentID == UUID.Zero)
            {
                // Zero UUID is not a real parent folder.
                return new List<InventoryFolderBase>();
            }

            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            
            getInventoryFolders(ref folders, parentID);

            for (int i = 0; i < folders.Count; i++)
                getInventoryFolders(ref folders, folders[i].ID);

            return folders;
        }

        public List<InventoryItemBase> fetchActiveGestures (UUID avatarID)
        {
            return null;
        }
    }
}
