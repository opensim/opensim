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
            if (userID == LLUUID.Zero)
                return;
            m_log.DebugFormat("[USER CACHE]: Adding user profile for {0}", userID);
            GetUserDetails(userID);
        }

        /// <summary>
        /// Remove this user's profile cache.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>true if the user was successfully removed, false otherwise</returns>
        public bool RemoveUser(LLUUID userId)
        {
            lock (m_userProfiles)
            {
                if (m_userProfiles.ContainsKey(userId))
                {
                    m_log.DebugFormat("[USER CACHE]: Removing user {0}", userId);
                    m_userProfiles.Remove(userId);
                    return true;
                }
            }

            m_log.WarnFormat(
                "[USER CACHE]: Tried to remove the profile of user {0}, but this was not in the scene", userId);

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
                if (m_commsManager.SecureInventoryService != null)
                {
                    m_commsManager.SecureInventoryService.RequestInventoryForUser(userID, userInfo.SessionID, userInfo.InventoryReceive);
                }
                else
                {
                    m_commsManager.InventoryService.RequestInventoryForUser(userID, userInfo.InventoryReceive);
                }
                //IInventoryServices invService = userInfo.GetInventoryService();
                //if (invService != null)
                //{
                //    invService.RequestInventoryForUser(userID, userInfo.InventoryReceive);
                //}
            }
            else
            {
                m_log.ErrorFormat("[USER CACHE]: RequestInventoryForUser() - user profile for user {0} not found", userID);
            }
        }

        /// <summary>
        /// Get the details of the given user.  A caller should try this method first if it isn't sure that
        /// a user profile exists for the given user.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>null if no user details are found</returns>
        public CachedUserInfo GetUserDetails(LLUUID userID)
        {
            if (userID == LLUUID.Zero)
                return null;

            lock (m_userProfiles)
            {
                if (m_userProfiles.ContainsKey(userID))
                {
                    return m_userProfiles[userID];
                }
                else
                {
                    UserProfileData userprofile = m_commsManager.UserService.GetUserProfile(userID);
                    if (userprofile != null)
                    {
                        CachedUserInfo userinfo = new CachedUserInfo(m_commsManager, userprofile);
                        m_userProfiles.Add(userID, userinfo);
                        return userinfo;
                    }
                    else
                    {
                        m_log.ErrorFormat("[USER CACHE]: User profile for user {0} not found.", userID);
                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// Preloads User data into the region cache. Modules may use this service to add non-standard clients
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="userData"></param>
        public void PreloadUserCache(LLUUID userID, UserProfileData userData)
        {
            if (userID == LLUUID.Zero)
                return;
            
            lock (m_userProfiles)
            {
                if (m_userProfiles.ContainsKey(userID))
                {
                    return;
                }
                else
                {

                    CachedUserInfo userInfo = new CachedUserInfo(m_commsManager, userData);
                    m_userProfiles.Add(userID, userInfo);
                }
            }
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
            CachedUserInfo userProfile;

            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                if (!userProfile.CreateFolder(folderName, folderID, folderType, parentID))
                {
                    m_log.ErrorFormat(
                         "[AGENT INVENTORY]: Failed to create folder for user {0} {1}",
                         remoteClient.Name, remoteClient.AgentId);
                }
            }
            else
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Could not find user profile for {0} {1}",
                    remoteClient.Name, remoteClient.AgentId);
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
                if (!userProfile.UpdateFolder(name, folderID, type, parentID))
                {
                    m_log.ErrorFormat(
                         "[AGENT INVENTORY]: Failed to update folder for user {0} {1}",
                         remoteClient.Name, remoteClient.AgentId);
                }
            }
            else
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Could not find user profile for {0} {1}",
                    remoteClient.Name, remoteClient.AgentId);
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
            CachedUserInfo userProfile;

            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                if (!userProfile.MoveFolder(folderID, parentID))
                {
                    m_log.ErrorFormat(
                         "[AGENT INVENTORY]: Failed to move folder for user {0} {1}",
                         remoteClient.Name, remoteClient.AgentId);
                }
            }
            else
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Could not find user profile for {0} {1}",
                    remoteClient.Name, remoteClient.AgentId);
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
            // FIXME MAYBE: We're not handling sortOrder!

            InventoryFolderImpl fold = null;
            if ((fold = libraryRoot.FindFolder(folderID)) != null)
            {
                remoteClient.SendInventoryFolderDetails(
                    libraryRoot.Owner, folderID, fold.RequestListOfItems(),
                    fold.RequestListOfFolders(), fetchFolders, fetchItems);

                return;
            }

            CachedUserInfo userProfile;
            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                userProfile.SendInventoryDecendents(remoteClient, folderID, fetchFolders, fetchItems);
            }
            else
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Could not find user profile for {0} {1}",
                    remoteClient.Name, remoteClient.AgentId);
            }
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
//            m_log.DebugFormat(
//                "[INVENTORY CACHE]: Fetching folders ({0}), items ({1}) from {2} for agent {3}",
//                fetchFolders, fetchItems, folderID, agentID);

            // FIXME MAYBE: We're not handling sortOrder!

            InventoryFolderImpl fold;
            if ((fold = libraryRoot.FindFolder(folderID)) != null)
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
                if (!userProfile.HasReceivedInventory)
                {
                    int attempts = 0;
                    while (attempts++ < 30)
                    {
                        m_log.DebugFormat(
                             "[INVENTORY CACHE]: Poll number {0} for inventory items in folder {1} for user {2}",
                             attempts, folderID, agentID);

                        Thread.Sleep(2000);

                        if (userProfile.HasReceivedInventory)
                        {
                            break;
                        }
                    }
                }

                if (userProfile.HasReceivedInventory)
                {
                    if ((fold = userProfile.RootFolder.FindFolder(folderID)) != null)
                    {
                        return fold.RequestListOfItems();
                    }
                    else
                    {
                        m_log.WarnFormat(
                            "[AGENT INVENTORY]: Could not find folder {0} requested by user {1}",
                            folderID, agentID);

                        return null;
                    }
                }
                else
                {
                    m_log.ErrorFormat("[INVENTORY CACHE]: Could not find root folder for user {0}", agentID);

                    return null;
                }
            }
            else
            {
                m_log.ErrorFormat("[AGENT INVENTORY]: Could not find user profile for {0}", agentID);

                return null;
            }
        }

        /// <summary>
        /// This should delete all the items and folders in the given directory.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        public void HandlePurgeInventoryDescendents(IClientAPI remoteClient, LLUUID folderID)
        {
            CachedUserInfo userProfile;

            if (m_userProfiles.TryGetValue(remoteClient.AgentId, out userProfile))
            {
                if (!userProfile.PurgeFolder(folderID))
                {
                    m_log.ErrorFormat(
                         "[AGENT INVENTORY]: Failed to purge folder for user {0} {1}",
                         remoteClient.Name, remoteClient.AgentId);
                }
            }
            else
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Could not find user profile for {0} {1}",
                    remoteClient.Name, remoteClient.AgentId);
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
                if (userProfile.HasReceivedInventory)
                {
                    InventoryItemBase item = userProfile.RootFolder.FindItem(itemID);
                    if (item != null)
                    {
                        remoteClient.SendInventoryItemDetails(ownerID, item);
                    }
                }
            }
            else
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Could not find user profile for {0} {1}",
                    remoteClient.Name, remoteClient.AgentId);
            }
        }
    }
}
