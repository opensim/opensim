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
using OpenMetaverse;
using OpenSim.Services.Interfaces;

namespace OpenSim.Framework.Communications.Cache
{
    internal delegate void AddItemDelegate(InventoryItemBase itemInfo);
    internal delegate void UpdateItemDelegate(InventoryItemBase itemInfo);
    internal delegate void DeleteItemDelegate(UUID itemID);
    internal delegate void QueryItemDelegate(UUID itemID);
    internal delegate void QueryFolderDelegate(UUID folderID);

    internal delegate void CreateFolderDelegate(string folderName, UUID folderID, ushort folderType, UUID parentID);
    internal delegate void MoveFolderDelegate(UUID folderID, UUID parentID);
    internal delegate void PurgeFolderDelegate(UUID folderID);
    internal delegate void UpdateFolderDelegate(string name, UUID folderID, ushort type, UUID parentID);

    internal delegate void SendInventoryDescendentsDelegate(
        IClientAPI client, UUID folderID, bool fetchFolders, bool fetchItems);

    public delegate void OnItemReceivedDelegate(UUID itemID);
    public delegate void OnInventoryReceivedDelegate(UUID userID);

    /// <summary>
    /// Stores user profile and inventory data received from backend services for a particular user.
    /// </summary>
    public class CachedUserInfo
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        //// <value>
        /// Fired when a particular item has been received from the inventory service
        /// </value>
        public event OnItemReceivedDelegate OnItemReceived;

        /// <value>
        /// Fired once the entire inventory has been received for the user
        /// </value>
        public event OnInventoryReceivedDelegate OnInventoryReceived;

        /// <summary>
        /// The comms manager holds references to services (user, grid, inventory, etc.)
        /// </summary>
        private readonly IInventoryService m_InventoryService;

        public UserProfileData UserProfile { get { return m_userProfile; } }
        private UserProfileData m_userProfile;

        /// <summary>
        /// Have we received the user's inventory from the inventory service?
        /// </summary>
        public bool HasReceivedInventory { get { return m_hasReceivedInventory; } }
        private bool m_hasReceivedInventory;

        /// <summary>
        /// Inventory requests waiting for receipt of this user's inventory from the inventory service.
        /// </summary>
        private readonly IList<IInventoryRequest> m_pendingRequests = new List<IInventoryRequest>();

        /// <summary>
        /// The root folder of this user's inventory.  Returns null if the root folder has not yet been received.
        /// </summary>
        public InventoryFolderImpl RootFolder { get { return m_rootFolder; } }
        private InventoryFolderImpl m_rootFolder;

        public UUID SessionID
        {
            get { return m_session_id; }
            set { m_session_id = value; }
        }
        private UUID m_session_id = UUID.Zero;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="commsManager"></param>
        /// <param name="userProfile"></param>
        public CachedUserInfo(IInventoryService invService, UserProfileData userProfile)
        {
            m_userProfile = userProfile;
            m_InventoryService = invService;
        }

        /// <summary>
        /// This allows a request to be added to be processed once we receive a user's inventory
        /// from the inventory service.  If we already have the inventory, the request
        /// is executed immediately instead.
        /// </summary>
        /// <param name="parent"></param>
        protected void AddRequest(IInventoryRequest request)
        {
            lock (m_pendingRequests)
            {
                if (HasReceivedInventory)
                {
                    request.Execute();
                }
                else
                {
                    m_pendingRequests.Add(request);
                }
            }
        }

        /// <summary>
        /// Helper function for InventoryReceive() - Store a folder temporarily until we've received entire folder list
        /// </summary>
        /// <param name="folder"></param>
        private void AddFolderToDictionary(InventoryFolderImpl folder, IDictionary<UUID, IList<InventoryFolderImpl>> dictionary)
        {
            UUID parentFolderId = folder.ParentID;

            if (dictionary.ContainsKey(parentFolderId))
            {
                dictionary[parentFolderId].Add(folder);
            }
            else
            {
                IList<InventoryFolderImpl> folders = new List<InventoryFolderImpl>();
                folders.Add(folder);
                dictionary[parentFolderId] = folders;
            }
        }

        /// <summary>
        /// Recursively, in depth-first order, add all the folders we've received (stored 
        /// in a dictionary indexed by parent ID) into the tree that describes user folder
        /// heirarchy
        /// Any folder that is resolved into the tree is also added to resolvedFolderDictionary,
        /// indexed by folder ID.
        /// </summary>
        /// <param name="parentId">
        /// A <see cref="UUID"/>
        /// </param>
        private void ResolveReceivedFolders(InventoryFolderImpl parentFolder, 
                                            IDictionary<UUID, IList<InventoryFolderImpl>> receivedFolderDictionary, 
                                            IDictionary<UUID, InventoryFolderImpl> resolvedFolderDictionary)
        {
            if (receivedFolderDictionary.ContainsKey(parentFolder.ID))
            {
                List<InventoryFolderImpl> resolvedFolders = new List<InventoryFolderImpl>(); // Folders we've resolved with this invocation
                foreach (InventoryFolderImpl folder in receivedFolderDictionary[parentFolder.ID])
                {
                    if (parentFolder.ContainsChildFolder(folder.ID))
                    {
                        m_log.WarnFormat(
                            "[INVENTORY CACHE]: Received folder {0} {1} from inventory service which has already been received",
                            folder.Name, folder.ID);
                    }
                    else
                    {
                        if (resolvedFolderDictionary.ContainsKey(folder.ID)) 
                        {
                            m_log.WarnFormat(
                                "[INVENTORY CACHE]: Received folder {0} {1} from inventory service has already been received but with different parent",
                                folder.Name, folder.ID);
                        }
                        else
                        {
                            resolvedFolders.Add(folder);
                            resolvedFolderDictionary[folder.ID] = folder;
                            parentFolder.AddChildFolder(folder);
                        }
                    }
                } // foreach (folder in pendingCategorizationFolders[parentFolder.ID])

                receivedFolderDictionary.Remove(parentFolder.ID);
                foreach (InventoryFolderImpl folder in resolvedFolders)
                    ResolveReceivedFolders(folder, receivedFolderDictionary, resolvedFolderDictionary);
            } // if (receivedFolderDictionary.ContainsKey(parentFolder.ID))
        }

        /// <summary>
        /// Drop all cached inventory.
        /// </summary>
        public void DropInventory()
        {
            m_log.Debug("[INVENTORY CACHE]: DropInventory called");
            // Make sure there aren't pending requests around when we do this
            // FIXME: There is still a race condition where an inventory operation can be requested (since these aren't being locked).
            // Will have to extend locking to exclude this very soon.
            lock (m_pendingRequests)
            {
                m_hasReceivedInventory = false;
                m_rootFolder = null;
            }
        }
        
        /// <summary>
        /// Fetch inventory for this user.
        /// </summary>
        /// This has to be executed as a separate step once user information is retreived.
        /// This will occur synchronously if the inventory service is in the same process as this class, and
        /// asynchronously otherwise.
        public void FetchInventory()
        {
            m_InventoryService.GetUserInventory(UserProfile.ID, InventoryReceive);
        }

        /// <summary>
        /// Callback invoked when the inventory is received from an async request to the inventory service
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="inventoryCollection"></param>
        public void InventoryReceive(ICollection<InventoryFolderImpl> folders, ICollection<InventoryItemBase> items)
        {
            // FIXME: Exceptions thrown upwards never appear on the console.  Could fix further up if these
            // are simply being swallowed

            try
            {
                // collection of all received folders, indexed by their parent ID
                IDictionary<UUID, IList<InventoryFolderImpl>> receivedFolders =
                    new Dictionary<UUID, IList<InventoryFolderImpl>>();

                // collection of all folders that have been placed into the folder heirarchy starting at m_rootFolder
                // This dictonary exists so we don't have to do an InventoryFolderImpl.FindFolder(), which is O(n) on the
                // number of folders in our inventory. 
                // Maybe we should make this structure a member so we can skip InventoryFolderImpl.FindFolder() calls later too?
                IDictionary<UUID, InventoryFolderImpl> resolvedFolders =
                    new Dictionary<UUID, InventoryFolderImpl>();

                // Take all received folders, find the root folder, and put ther rest into
                // the pendingCategorizationFolders collection
                foreach (InventoryFolderImpl folder in folders)
                    AddFolderToDictionary(folder, receivedFolders);

                if (!receivedFolders.ContainsKey(UUID.Zero))
                    throw new Exception("Database did not return a root inventory folder");
                else
                {
                    IList<InventoryFolderImpl> rootFolderList = receivedFolders[UUID.Zero];
                    m_rootFolder = rootFolderList[0];
                    resolvedFolders[m_rootFolder.ID] = m_rootFolder;
                    if (rootFolderList.Count > 1)
                    {
                        for (int i = 1; i < rootFolderList.Count; i++)
                        {
                            m_log.WarnFormat(
                                "[INVENTORY CACHE]: Discarding extra root folder {0}. Using previously received root folder {1}",
                                rootFolderList[i].ID, RootFolder.ID);
                        }
                    }
                    receivedFolders.Remove(UUID.Zero);
                }

                // Now take the pendingCategorizationFolders collection, and turn that into a tree,
                // with the root being RootFolder
                if (RootFolder != null)
                    ResolveReceivedFolders(RootFolder, receivedFolders, resolvedFolders);

                // Generate a warning for folders that are not part of the heirarchy
                foreach (KeyValuePair<UUID, IList<InventoryFolderImpl>> folderList in receivedFolders)
                {
                    foreach (InventoryFolderImpl folder in folderList.Value)
                        m_log.WarnFormat("[INVENTORY CACHE]: Malformed Database: Unresolved Pending Folder {0}", folder.Name);
                }

                // Take all ther received items and put them into the folder tree heirarchy
                foreach (InventoryItemBase item in items) {
                    InventoryFolderImpl folder = resolvedFolders.ContainsKey(item.Folder) ? resolvedFolders[item.Folder] : null;
                    ItemReceive(item, folder);
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CACHE]: Error processing inventory received from inventory service, {0}", e);
            }

            // Deal with pending requests
            lock (m_pendingRequests)
            {
                // We're going to change inventory status within the lock to avoid a race condition
                // where requests are processed after the AddRequest() method has been called.
                m_hasReceivedInventory = true;

                foreach (IInventoryRequest request in m_pendingRequests)
                {
                    request.Execute();
                }
            }

            if (OnInventoryReceived != null)
                OnInventoryReceived(UserProfile.ID);
        }

        /// <summary>
        /// Callback invoked when an item is received from an async request to the inventory service.
        ///
        /// We're assuming here that items are always received after all the folders
        /// received.
        /// If folder is null, we will search for it starting from RootFolder (an O(n) operation),
        /// otherwise we'll just put it into folder
        /// </summary>
        /// <param name="folderInfo"></param>
        private void ItemReceive(InventoryItemBase itemInfo, InventoryFolderImpl folder)
        {
            //            m_log.DebugFormat(
            //                "[INVENTORY CACHE]: Received item {0} {1} for user {2}",
            //                itemInfo.Name, itemInfo.ID, userID);

            if (folder == null && RootFolder != null)
                folder = RootFolder.FindFolder(itemInfo.Folder);

            if (null == folder)
            {
                m_log.WarnFormat(
                    "Received item {0} {1} but its folder {2} does not exist",
                    itemInfo.Name, itemInfo.ID, itemInfo.Folder);

                return;
            }

            lock (folder.Items)
            {
                folder.Items[itemInfo.ID] = itemInfo;
            }

            if (OnItemReceived != null)
                OnItemReceived(itemInfo.ID);
        }

        /// <summary>
        /// Create a folder in this agent's inventory.
        /// </summary>
        ///
        /// If the inventory service has not yet delievered the inventory
        /// for this user then the request will be queued.
        /// 
        /// <param name="parentID"></param>
        /// <returns></returns>
        public bool CreateFolder(string folderName, UUID folderID, ushort folderType, UUID parentID)
        {
            //            m_log.DebugFormat(
            //                "[AGENT INVENTORY]: Creating inventory folder {0} {1} for {2} {3}", folderID, folderName, remoteClient.Name, remoteClient.AgentId);

            if (m_hasReceivedInventory)
            {
                InventoryFolderImpl parentFolder = RootFolder.FindFolder(parentID);

                if (null == parentFolder)
                {
                    m_log.WarnFormat(
                        "[AGENT INVENTORY]: Tried to create folder {0} {1} but the parent {2} does not exist",
                        folderName, folderID, parentID);

                    return false;
                }

                InventoryFolderImpl createdFolder = parentFolder.CreateChildFolder(folderID, folderName, folderType);

                if (createdFolder != null)
                {
                    InventoryFolderBase createdBaseFolder = new InventoryFolderBase();
                    createdBaseFolder.Owner = createdFolder.Owner;
                    createdBaseFolder.ID = createdFolder.ID;
                    createdBaseFolder.Name = createdFolder.Name;
                    createdBaseFolder.ParentID = createdFolder.ParentID;
                    createdBaseFolder.Type = createdFolder.Type;
                    createdBaseFolder.Version = createdFolder.Version;

                    m_InventoryService.AddFolder(createdBaseFolder);

                    return true;
                }
                else
                {
                    m_log.WarnFormat(
                         "[AGENT INVENTORY]: Tried to create folder {0} {1} but the folder already exists",
                         folderName, folderID);

                    return false;
                }
            }
            else
            {
                AddRequest(
                    new InventoryRequest(
                        Delegate.CreateDelegate(typeof(CreateFolderDelegate), this, "CreateFolder"),
                        new object[] { folderName, folderID, folderType, parentID }));

                return true;
            }
        }

        /// <summary>
        /// Handle a client request to update the inventory folder
        /// </summary>
        ///
        /// If the inventory service has not yet delievered the inventory
        /// for this user then the request will be queued.
        ///
        /// FIXME: We call add new inventory folder because in the data layer, we happen to use an SQL REPLACE
        /// so this will work to rename an existing folder.  Needless to say, to rely on this is very confusing,
        /// and needs to be changed.
        ///
        /// <param name="folderID"></param>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <param name="parentID"></param>
        public bool UpdateFolder(string name, UUID folderID, ushort type, UUID parentID)
        {
            //            m_log.DebugFormat(
            //                "[AGENT INVENTORY]: Updating inventory folder {0} {1} for {2} {3}", folderID, name, remoteClient.Name, remoteClient.AgentId);

            if (m_hasReceivedInventory)
            {
                InventoryFolderImpl folder = RootFolder.FindFolder(folderID);
                
                // Delegate movement if updated parent id isn't the same as the existing parentId
                if (folder.ParentID != parentID)
                    MoveFolder(folderID, parentID);
                
                InventoryFolderBase baseFolder = new InventoryFolderBase();
                baseFolder.Owner = m_userProfile.ID;
                baseFolder.ID = folderID;
                baseFolder.Name = name;
                baseFolder.ParentID = parentID;
                baseFolder.Type = (short)type;
                baseFolder.Version = RootFolder.Version;

                m_InventoryService.UpdateFolder(baseFolder);

                folder.Name = name;
                folder.Type = (short)type;
            }
            else
            {
                AddRequest(
                    new InventoryRequest(
                        Delegate.CreateDelegate(typeof(UpdateFolderDelegate), this, "UpdateFolder"),
                        new object[] { name, folderID, type, parentID }));
            }

            return true;
        }

        /// <summary>
        /// Handle an inventory folder move request from the client.
        ///
        /// If the inventory service has not yet delievered the inventory
        /// for this user then the request will be queued.
        /// </summary>
        ///
        /// <param name="folderID"></param>
        /// <param name="parentID"></param>
        /// <returns>
        /// true if the delete was successful, or if it was queued pending folder receipt
        /// false if the folder to be deleted did not exist.
        /// </returns>
        public bool MoveFolder(UUID folderID, UUID parentID)
        {
            //            m_log.DebugFormat(
            //                "[AGENT INVENTORY]: Moving inventory folder {0} into folder {1} for {2} {3}",
            //                parentID, remoteClient.Name, remoteClient.Name, remoteClient.AgentId);

            if (m_hasReceivedInventory)
            {
                InventoryFolderBase baseFolder = new InventoryFolderBase();
                baseFolder.Owner = m_userProfile.ID;
                baseFolder.ID = folderID;
                baseFolder.ParentID = parentID;

                m_InventoryService.MoveFolder(baseFolder);
                
                InventoryFolderImpl folder = RootFolder.FindFolder(folderID);
                InventoryFolderImpl parentFolder = RootFolder.FindFolder(parentID);
                if (parentFolder != null && folder != null)
                {
                    InventoryFolderImpl oldParentFolder = RootFolder.FindFolder(folder.ParentID);

                    if (oldParentFolder != null)
                    {
                        oldParentFolder.RemoveChildFolder(folderID);
                        parentFolder.AddChildFolder(folder);
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                { 
                    return false;
                }

                return true;
            }
            else
            {
                AddRequest(
                    new InventoryRequest(
                        Delegate.CreateDelegate(typeof(MoveFolderDelegate), this, "MoveFolder"),
                        new object[] { folderID, parentID }));

                return true;
            }
        }

        /// <summary>
        /// This method will delete all the items and folders in the given folder.
        /// </summary>
        /// If the inventory service has not yet delievered the inventory
        /// for this user then the request will be queued.
        ///
        /// <param name="folderID"></param>
        public bool PurgeFolder(UUID folderID)
        {
            //            m_log.InfoFormat("[AGENT INVENTORY]: Purging folder {0} for {1} uuid {2}",
            //                folderID, remoteClient.Name, remoteClient.AgentId);

            if (m_hasReceivedInventory)
            {
                InventoryFolderImpl purgedFolder = RootFolder.FindFolder(folderID);

                if (purgedFolder != null)
                {
                    // XXX Nasty - have to create a new object to hold details we already have
                    InventoryFolderBase purgedBaseFolder = new InventoryFolderBase();
                    purgedBaseFolder.Owner = purgedFolder.Owner;
                    purgedBaseFolder.ID = purgedFolder.ID;
                    purgedBaseFolder.Name = purgedFolder.Name;
                    purgedBaseFolder.ParentID = purgedFolder.ParentID;
                    purgedBaseFolder.Type = purgedFolder.Type;
                    purgedBaseFolder.Version = purgedFolder.Version;

                    m_InventoryService.PurgeFolder(purgedBaseFolder);

                    purgedFolder.Purge();

                    return true;
                }
            }
            else
            {
                AddRequest(
                    new InventoryRequest(
                        Delegate.CreateDelegate(typeof(PurgeFolderDelegate), this, "PurgeFolder"),
                        new object[] { folderID }));

                return true;
            }

            return false;
        }

        /// <summary>
        /// Add an item to the user's inventory.
        /// </summary>
        /// If the item has no folder set (i.e. it is UUID.Zero), then it is placed in the most appropriate folder
        /// for that type.
        /// <param name="itemInfo"></param>
        public void AddItem(InventoryItemBase item)
        {
            if (m_hasReceivedInventory)
            {
                if (item.Folder == UUID.Zero)
                {
                    InventoryFolderImpl f = FindFolderForType(item.AssetType);
                    if (f != null)
                        item.Folder = f.ID;
                    else
                        item.Folder = RootFolder.ID;
                }
                ItemReceive(item, null);
                
                m_InventoryService.AddItem(item);
            }
            else
            {
                AddRequest(
                    new InventoryRequest(
                        Delegate.CreateDelegate(typeof(AddItemDelegate), this, "AddItem"),
                        new object[] { item }));
            }
        }

        /// <summary>
        /// Update an item in the user's inventory
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="itemInfo"></param>
        public void UpdateItem(InventoryItemBase item)
        {
            if (m_hasReceivedInventory)
            {
                m_InventoryService.UpdateItem(item);
            }
            else
            {
                AddRequest(
                    new InventoryRequest(
                        Delegate.CreateDelegate(typeof(UpdateItemDelegate), this, "UpdateItem"),
                        new object[] { item }));
            }
        }

        /// <summary>
        /// Delete an item from the user's inventory
        ///
        /// If the inventory service has not yet delievered the inventory
        /// for this user then the request will be queued.
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>
        /// true on a successful delete or a if the request is queued.
        /// Returns false on an immediate failure
        /// </returns>
        public bool DeleteItem(UUID itemID)
        {
            if (m_hasReceivedInventory)
            {
                // XXX For historical reasons (grid comms), we need to retrieve the whole item in order to delete, even though
                // really only the item id is required.
                InventoryItemBase item = RootFolder.FindItem(itemID);

                if (null == item)
                {
                    m_log.WarnFormat("[AGENT INVENTORY]: Tried to delete item {0} which does not exist", itemID);

                    return false;
                }

                if (RootFolder.DeleteItem(item.ID))
                {
                    List<UUID> uuids = new List<UUID>();
                    uuids.Add(itemID);
                    return m_InventoryService.DeleteItems(this.UserProfile.ID, uuids);
                }
            }
            else
            {
                AddRequest(
                    new InventoryRequest(
                        Delegate.CreateDelegate(typeof(DeleteItemDelegate), this, "DeleteItem"),
                        new object[] { itemID }));

                return true;
            }

            return false;
        }

        /// <summary>
        /// Send details of the inventory items and/or folders in a given folder to the client.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="folderID"></param>
        /// <param name="fetchFolders"></param>
        /// <param name="fetchItems"></param>
        /// <returns>true if the request was queued or successfully processed, false otherwise</returns>
        public bool SendInventoryDecendents(IClientAPI client, UUID folderID, bool fetchFolders, bool fetchItems)
        {
            if (m_hasReceivedInventory)
            {
                InventoryFolderImpl folder;

                if ((folder = RootFolder.FindFolder(folderID)) != null)
                {
                    //                            m_log.DebugFormat(
                    //                                "[AGENT INVENTORY]: Found folder {0} for client {1}",
                    //                                folderID, remoteClient.AgentId);

                    client.SendInventoryFolderDetails(
                        client.AgentId, folderID, folder.RequestListOfItems(),
                        folder.RequestListOfFolders(), fetchFolders, fetchItems);

                    return true;
                }
                else
                {
                    m_log.WarnFormat(
                        "[AGENT INVENTORY]: Could not find folder {0} requested by user {1} {2}",
                        folderID, client.Name, client.AgentId);

                    return false;
                }
            }
            else
            {
                AddRequest(
                    new InventoryRequest(
                        Delegate.CreateDelegate(typeof(SendInventoryDescendentsDelegate), this, "SendInventoryDecendents", false, false),
                        new object[] { client, folderID, fetchFolders, fetchItems }));

                return true;
            }
        }

        /// <summary>
        /// Find an appropriate folder for the given asset type
        /// </summary>
        /// <param name="type"></param>
        /// <returns>null if no appropriate folder exists</returns>
        public InventoryFolderImpl FindFolderForType(int type)
        {
            if (RootFolder == null)
                return null;

            return RootFolder.FindFolderForType(type);
        }

        // Load additional items that other regions have put into the database
        // The item will be added tot he local cache. Returns true if the item
        // was found and can be sent to the client
        //
        public bool QueryItem(InventoryItemBase item)
        {
            if (m_hasReceivedInventory)
            {
                InventoryItemBase invItem = RootFolder.FindItem(item.ID);

                if (invItem != null)
                {
                    // Item is in local cache, just update client
                    //
                    return true;
                }

                InventoryItemBase itemInfo = null;

                itemInfo = m_InventoryService.GetItem(item);

                if (itemInfo != null)
                {
                    InventoryFolderImpl folder = RootFolder.FindFolder(itemInfo.Folder);
                    ItemReceive(itemInfo, folder);
                    return true;
                }

                return false;
            }
            else
            {
                AddRequest(
                    new InventoryRequest(
                        Delegate.CreateDelegate(typeof(QueryItemDelegate), this, "QueryItem"),
                        new object[] { item.ID }));

                return true;
            }
        }

        public bool QueryFolder(InventoryFolderBase folder)
        {
            if (m_hasReceivedInventory)
            {
                InventoryFolderBase invFolder = RootFolder.FindFolder(folder.ID);

                if (invFolder != null)
                {
                    // Folder is in local cache, just update client
                    //
                    return true;
                }

                InventoryFolderBase folderInfo = null;

                folderInfo = m_InventoryService.GetFolder(folder);

                if (folderInfo != null)
                {
                    InventoryFolderImpl createdFolder = RootFolder.CreateChildFolder(folderInfo.ID, folderInfo.Name, (ushort)folderInfo.Type);

                    createdFolder.Version = folderInfo.Version;
                    createdFolder.Owner = folderInfo.Owner;
                    createdFolder.ParentID = folderInfo.ParentID;

                    return true;
                }

                return false;
            }
            else
            {
                AddRequest(
                    new InventoryRequest(
                        Delegate.CreateDelegate(typeof(QueryFolderDelegate), this, "QueryFolder"),
                        new object[] { folder.ID }));

                return true;
            }
        }
    }

    /// <summary>
    /// Should be implemented by callers which require a callback when the user's inventory is received
    /// </summary>
    public interface IInventoryRequest
    {
        /// <summary>
        /// This is the method executed once we have received the user's inventory by which the request can be fulfilled.
        /// </summary>
        void Execute();
    }

    /// <summary>
    /// Generic inventory request
    /// </summary>
    class InventoryRequest : IInventoryRequest
    {
        private Delegate m_delegate;
        private Object[] m_args;

        internal InventoryRequest(Delegate delegat, Object[] args)
        {
            m_delegate = delegat;
            m_args = args;
        }

        public void Execute()
        {
            if (m_delegate != null)
                m_delegate.DynamicInvoke(m_args);
        }
    }
}
