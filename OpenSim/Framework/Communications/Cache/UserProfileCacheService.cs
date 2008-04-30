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
using System.Threading;

using libsecondlife;
using log4net;

namespace OpenSim.Framework.Communications.Cache
{
    internal delegate void CreateInventoryFolderDelegate(
        IClientAPI remoteClient, LLUUID folderID, ushort folderType, string folderName, LLUUID parentID); 
    internal delegate void MoveInventoryFolderDelegate(IClientAPI remoteClient, LLUUID folderID, LLUUID parentID);         
    internal delegate void PurgeInventoryDescendentsDelegate(IClientAPI remoteClient, LLUUID folderID);     
    internal delegate void UpdateInventoryFolderDelegate(
        IClientAPI remoteClient, LLUUID folderID, ushort type, string name, LLUUID parentID);
        
    /// <summary>
    /// Holds user profile information and retrieves it from backend services.
    /// </summary>
    public class UserProfileCacheService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The comms manager holds references to services (user, grid, inventory, etc.)
        /// </summary>
        private readonly CommunicationsManager m_commsManager;
        
        /// <summary>
        /// Each user has a cached profile.
        /// </summary>
        private readonly Dictionary<LLUUID, CachedUserInfo> m_userProfiles = new Dictionary<LLUUID, CachedUserInfo>();

        public readonly LibraryRootFolder libraryRoot = new LibraryRootFolder();

        // Methods
        public UserProfileCacheService(CommunicationsManager commsManager)
        {
            m_commsManager = commsManager;
        }

        /// <summary>
        /// A new user has moved into a region in this instance so retrieve their profile from the user service.
        /// </summary>
        /// <param name="userID"></param>
        public void AddNewUser(LLUUID userID)
        {
            // Potential fix - Multithreading issue.
            lock (m_userProfiles)
            {
                if (!m_userProfiles.ContainsKey(userID))
                {
                    UserProfileData userProfile = m_commsManager.UserService.GetUserProfile(userID);
                    CachedUserInfo userInfo = new CachedUserInfo(m_commsManager, userProfile);

                    if (userInfo.UserProfile != null)
                    {
                        // The inventory for the user will be populated when they actually enter the scene
                        m_userProfiles.Add(userID, userInfo);
                    }
                    else
                    {
                        m_log.ErrorFormat("[USER CACHE]: User profile for user {0} not found.", userID);
                    }
                }
            }
        }        
        
        /// <summary>
        /// Remove this user's profile cache.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>true if the user was successfully removed, false otherwise</returns>
        public bool RemoveUser(LLUUID userID)
        {
            lock (m_userProfiles)
            {
                if (m_userProfiles.ContainsKey(userID))
                {
                    m_userProfiles.Remove(userID);
                    return true;
                }
                else
                {
                    m_log.WarnFormat("[USER CACHE]: Tried to remove the profile of user {0}, but this was not in the scene", userID);
                }               
            }
            
            return false;
        }

        /// <summary>
        /// Request the inventory data for the given user.  This will occur asynchronously if running on a grid
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="userInfo"></param>
        public void RequestInventoryForUser(LLUUID userID)
        {
            CachedUserInfo userInfo = GetUserDetails(userID);
            if (userInfo != null)
            {            
                m_commsManager.InventoryService.RequestInventoryForUser(userID, userInfo.InventoryReceive);
            }
            else
            {
                m_log.ErrorFormat("[USER CACHE]: RequestInventoryForUser() - user profile for user {0} not found", userID);
            }
        }            

        /// <summary>
        /// Get the details of the given user.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>null if no user details are found</returns>
        public CachedUserInfo GetUserDetails(LLUUID userID)
        {
            if (m_userProfiles.ContainsKey(userID))
                return m_userProfiles[userID];
            else
                return null;
        }

        /// <summary>
        /// Handle an inventory folder creation request from the client.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="folderType"></param>
        /// <param name="folderName"></param>
        /// <param name="parentID"></param>
        public void HandleCreateInventoryFolder(IClientAPI remoteClient, LLUUID folderID, ushort folderType,
                                                string folderName, LLUUID parentID)
        {
//            m_log.DebugFormat(
//                "[AGENT INVENTORY]: Creating inventory folder {0} {1} for {2} {3}", folderID, folderName, remoteClient.Name, remoteClient.AgentId);
            
            CachedUserInfo userProfile;

            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                if (userProfile.HasInventory)
                {
                    if (userProfile.RootFolder.ID == parentID)
                    {
                        InventoryFolderImpl createdFolder =
                            userProfile.RootFolder.CreateNewSubFolder(folderID, folderName, folderType);

                        if (createdFolder != null)
                        {
                            InventoryFolderBase createdBaseFolder = new InventoryFolderBase();
                            createdBaseFolder.Owner = createdFolder.Owner;
                            createdBaseFolder.ID = createdFolder.ID;
                            createdBaseFolder.Name = createdFolder.Name;
                            createdBaseFolder.ParentID = createdFolder.ParentID;
                            createdBaseFolder.Type = createdFolder.Type;
                            createdBaseFolder.Version = createdFolder.Version;
                            m_commsManager.InventoryService.AddNewInventoryFolder(remoteClient.AgentId, createdBaseFolder);
                        }
                        else
                        {
                            m_log.WarnFormat(
                                 "[INVENTORY CACHE]: Tried to create folder {0} {1} for user {2} {3} but the folder already exists", 
                                 folderName, folderID, remoteClient.Name, remoteClient.AgentId);
                        }
                    }
                    else
                    {
                        InventoryFolderImpl folder = userProfile.RootFolder.HasSubFolder(parentID);
                        if (folder != null)
                        {
                            InventoryFolderImpl createdFolder = folder.CreateNewSubFolder(folderID, folderName, folderType);
                         
                            if (createdFolder != null)
                            {
                                InventoryFolderBase createdBaseFolder = new InventoryFolderBase();
                                createdBaseFolder.Owner = createdFolder.Owner;
                                createdBaseFolder.ID = createdFolder.ID;
                                createdBaseFolder.Name = createdFolder.Name;
                                createdBaseFolder.ParentID = createdFolder.ParentID;
                                createdBaseFolder.Type = createdFolder.Type;
                                createdBaseFolder.Version = createdFolder.Version;                            
                                
                                m_commsManager.InventoryService.AddNewInventoryFolder(remoteClient.AgentId, createdBaseFolder);
                            }
                            else
                            {
                                m_log.WarnFormat(
                                     "[INVENTORY CACHE]: Tried to create folder {0} {1} for user {2} {3} but the folder already exists", 
                                     folderName, folderID, remoteClient.Name, remoteClient.AgentId);
                            }    
                        }  
                        else
                        {
                            m_log.WarnFormat(
                                 "[INVENTORY CACHE]: Could not find parent folder with id {0} in order to create folder {1} {2} for user {3} {4}",
                                 parentID, folderName, folderID, remoteClient.Name, remoteClient.AgentId);
                        }
                    }
                }
                else
                {
                    userProfile.AddRequest(
                        new InventoryRequest(
                            Delegate.CreateDelegate(typeof(CreateInventoryFolderDelegate), this, "HandleCreateInventoryFolder"),
                            new object[] { remoteClient, folderID, folderType, folderName, parentID }));                                       
                }
            }
        }

        /// <summary>
        /// Handle a client request to update the inventory folder
        /// 
        /// FIXME: We call add new inventory folder because in the data layer, we happen to use an SQL REPLACE
        /// so this will work to rename an existing folder.  Needless to say, to rely on this is very confusing,
        /// and needs to be changed.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <param name="parentID"></param>
        public void HandleUpdateInventoryFolder(IClientAPI remoteClient, LLUUID folderID, ushort type, string name,
                                                LLUUID parentID)
        {
//            m_log.DebugFormat(
//                "[AGENT INVENTORY]: Updating inventory folder {0} {1} for {2} {3}", folderID, name, remoteClient.Name, remoteClient.AgentId);
            
            CachedUserInfo userProfile;

            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                if (userProfile.HasInventory)
                {
                    InventoryFolderBase baseFolder = new InventoryFolderBase();
                    baseFolder.Owner = remoteClient.AgentId;
                    baseFolder.ID = folderID;
                    baseFolder.Name = name;
                    baseFolder.ParentID = parentID;
                    baseFolder.Type = (short) type;
                    baseFolder.Version = userProfile.RootFolder.Version;
                    m_commsManager.InventoryService.AddNewInventoryFolder(remoteClient.AgentId, baseFolder);
                }
                else
                {
                    userProfile.AddRequest(
                        new InventoryRequest(
                            Delegate.CreateDelegate(typeof(UpdateInventoryFolderDelegate), this, "HandleUpdateInventoryFolder"),
                            new object[] { remoteClient, folderID, type, name, parentID }));
                }
            }
        }

        /// <summary>
        /// Handle an inventory folder move request from the client.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="parentID"></param>
        public void HandleMoveInventoryFolder(IClientAPI remoteClient, LLUUID folderID, LLUUID parentID)
        {
//            m_log.DebugFormat(
//                "[AGENT INVENTORY]: Moving inventory folder {0} into folder {1} for {2} {3}",
//                parentID, remoteClient.Name, remoteClient.Name, remoteClient.AgentId);

            CachedUserInfo userProfile;

            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                if (userProfile.HasInventory)
                {
                    InventoryFolderBase baseFolder = new InventoryFolderBase();
                    baseFolder.Owner = remoteClient.AgentId;
                    baseFolder.ID = folderID;
                    baseFolder.ParentID = parentID;
                    m_commsManager.InventoryService.MoveInventoryFolder(remoteClient.AgentId, baseFolder);
                }
                else
                {
                    userProfile.AddRequest(
                        new InventoryRequest(
                            Delegate.CreateDelegate(typeof(MoveInventoryFolderDelegate), this, "HandleMoveInventoryFolder"),
                            new object[] { remoteClient, folderID, parentID }));   
                }
            }
        }

        /// <summary>
        /// Tell the client about the various child items and folders contained in the requested folder.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="ownerID"></param>
        /// <param name="fetchFolders"></param>
        /// <param name="fetchItems"></param>
        /// <param name="sortOrder"></param>
        public void HandleFetchInventoryDescendents(IClientAPI remoteClient, LLUUID folderID, LLUUID ownerID,
                                                    bool fetchFolders, bool fetchItems, int sortOrder)
        {
            // XXX We're not handling sortOrder yet!

            InventoryFolderImpl fold = null;
            if (folderID == libraryRoot.ID)
            {
                remoteClient.SendInventoryFolderDetails(
                    libraryRoot.Owner, libraryRoot.ID, libraryRoot.RequestListOfItems(),
                    libraryRoot.RequestListOfFolders(), fetchFolders, fetchItems);

                return;
            }

            if ((fold = libraryRoot.HasSubFolder(folderID)) != null)
            {
                remoteClient.SendInventoryFolderDetails(
                    libraryRoot.Owner, folderID, fold.RequestListOfItems(),
                    fold.RequestListOfFolders(), fetchFolders, fetchItems);

                return;
            }

            CachedUserInfo userProfile;
            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                // XXX: When a client crosses into a scene, their entire inventory is fetched
                // asynchronously.  However, if the client is logging on and does not have a cached root 
                // folder, then the root folder request usually comes in *before* the async completes, leading to 
                // inventory failure.
                //
                // This is a crude way of dealing with that by retrying the lookup.
                if (!userProfile.HasInventory)
                {
                    int attempts = 5;
                    while (attempts-- > 0)
                    {
                        Thread.Sleep(3000);
                        
                        if (userProfile.HasInventory)
                        {
                            break;
                        }
                    }
                }
                
                if (userProfile.HasInventory)
                {
                    if (userProfile.RootFolder.ID == folderID)
                    {
//                        m_log.DebugFormat(
//                            "[AGENT INVENTORY]: Found root folder {0} for client {1}", 
//                            folderID, remoteClient.AgentId);
                        
                        remoteClient.SendInventoryFolderDetails(
                            remoteClient.AgentId, folderID, userProfile.RootFolder.RequestListOfItems(),
                            userProfile.RootFolder.RequestListOfFolders(),
                            fetchFolders, fetchItems);

                        return;
                    }
                    else
                    {
                        if ((fold = userProfile.RootFolder.HasSubFolder(folderID)) != null)
                        {
//                            m_log.DebugFormat(
//                                "[AGENT INVENTORY]: Found folder {0} for client {1}", 
//                                folderID, remoteClient.AgentId);
                            
                            remoteClient.SendInventoryFolderDetails(
                                remoteClient.AgentId, folderID, fold.RequestListOfItems(),
                                fold.RequestListOfFolders(), fetchFolders, fetchItems);

                            return;
                        }
                    }
                }
                else
                {
                    m_log.ErrorFormat("[INVENTORY CACHE]: Could not find root folder for user {0}", remoteClient.Name);

                    return;
                }
            }
            else
            {
                m_log.ErrorFormat(
                     "[USER CACHE]: HandleFetchInventoryDescendents() could not find user profile {0}, {1}",
                     remoteClient.Name, remoteClient.AgentId);

                return;
            }

            // If we've reached this point then we couldn't find the folder, even though the client thinks
            // it exists
            m_log.ErrorFormat("[INVENTORY CACHE]: Could not find folder {0} for user {1}",
                              folderID, remoteClient.Name);
        }

        /// <summary>
        /// Handle the caps inventory descendents fetch.
        /// 
        /// Since the folder structure is sent to the client on login, I believe we only need to handle items.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="folderID"></param>
        /// <param name="ownerID"></param>
        /// <param name="fetchFolders"></param>
        /// <param name="fetchItems"></param>
        /// <param name="sortOrder"></param>
        /// <returns>null if the inventory look up failed</returns>
        public List<InventoryItemBase> HandleFetchInventoryDescendentsCAPS(LLUUID agentID, LLUUID folderID, LLUUID ownerID,
                                                   bool fetchFolders, bool fetchItems, int sortOrder)
        {
            //m_log.DebugFormat("[INVENTORY CACHE]: Fetching folders/items from {0} for agent {1}", folderID, agentID);
            
            // XXX We're not handling sortOrder yet!

            InventoryFolderImpl fold = null;
            if (folderID == libraryRoot.ID)
            {
                return libraryRoot.RequestListOfItems();
            }

            if ((fold = libraryRoot.HasSubFolder(folderID)) != null)
            {
                return fold.RequestListOfItems();
            }         

            CachedUserInfo userProfile;
            if (m_userProfiles.TryGetValue(agentID, out userProfile))
            {            
                // XXX: When a client crosses into a scene, their entire inventory is fetched
                // asynchronously.  If the client makes a request before the inventory is received, we need
                // to give the inventory a chance to come in.
                //
                // This is a crude way of dealing with that by retrying the lookup.  It's not quite as bad
                // in CAPS as doing this with the udp request, since here it won't hold up other packets.
                // In fact, here we'll be generous and try for longer.
                if (!userProfile.HasInventory)
                {
                    int attempts = 0;
                    while (attempts++ < 20)
                    {
                        m_log.DebugFormat(
                             "[INVENTORY CACHE]: Poll number {0} for inventory items in folder {1} for user {2}", 
                             attempts, folderID, agentID);
                        
                        Thread.Sleep(3000);
                        
                        if (userProfile.HasInventory)
                        {
                            break;
                        }
                    }
                }   
                
                if (userProfile.HasInventory)
                {
                    if (userProfile.RootFolder.ID == folderID)
                    {
                        return userProfile.RootFolder.RequestListOfItems();
                    }
                    else
                    {
                        if ((fold = userProfile.RootFolder.HasSubFolder(folderID)) != null)
                        {
                            return fold.RequestListOfItems();
                        }
                    }
                }
                else
                {
                    m_log.ErrorFormat("[INVENTORY CACHE]: Could not find root folder for user {0}", agentID.ToString());

                    return null;
                }
            }
            else
            {
                m_log.ErrorFormat(
                     "[USER CACHE]: HandleFetchInventoryDescendentsCAPS() Could not find user profile for {0}",
                     agentID);
            
                return null;
            }

            // If we've reached this point then we couldn't find the folder, even though the client thinks
            // it exists
            m_log.ErrorFormat("[INVENTORY CACHE]: " +
                              "Could not find folder {0} for user {1}",
                              folderID, agentID.ToString());

            return new List<InventoryItemBase>();
        }

        /// <summary>
        /// This should delete all the items and folders in the given directory.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        public void HandlePurgeInventoryDescendents(IClientAPI remoteClient, LLUUID folderID)
        {
//            m_log.InfoFormat("[AGENT INVENTORY]: Purging folder {0} for {1} uuid {2}", 
//                folderID, remoteClient.Name, remoteClient.AgentId);
            
            CachedUserInfo userProfile;
            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                if (userProfile.HasInventory)
                {
                    InventoryFolderImpl purgedFolder = userProfile.RootFolder.HasSubFolder(folderID);
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
                        
                        m_commsManager.InventoryService.PurgeInventoryFolder(remoteClient.AgentId, purgedBaseFolder);                                              
                        
                        purgedFolder.Purge();                        
                    }
                }
                else
                {
                    userProfile.AddRequest(
                        new InventoryRequest(
                            Delegate.CreateDelegate(typeof(PurgeInventoryDescendentsDelegate), this, "HandlePurgeInventoryDescendents"),
                            new object[] { remoteClient, folderID }));                 
                }
            }
        }

        public void HandleFetchInventory(IClientAPI remoteClient, LLUUID itemID, LLUUID ownerID)
        {
            if (ownerID == libraryRoot.Owner)
            {
                //Console.WriteLine("request info for library item");

                return;
            }

            CachedUserInfo userProfile;
            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                if (userProfile.HasInventory)
                {
                    InventoryItemBase item = userProfile.RootFolder.HasItem(itemID);
                    if (item != null)
                    {
                        remoteClient.SendInventoryItemDetails(ownerID, item);
                    }
                }
            }
        }
    }
}
