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
using System.Net;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Statistics;
using OpenSim.Region.Communications.Local;

namespace OpenSim.Region.Communications.Hypergrid
{
    public class HGInventoryServiceClient : LocalInventoryService, ISecureInventoryService
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string _inventoryServerUrl;
        //private Uri m_Uri;
        private UserProfileCacheService m_userProfileCache;
        private bool m_gridmode = false;

        private Dictionary<UUID, InventoryReceiptCallback> m_RequestingInventory
            = new Dictionary<UUID, InventoryReceiptCallback>();

        public UserProfileCacheService UserProfileCache
        {
            set { m_userProfileCache = value; }
        }

        public HGInventoryServiceClient(string inventoryServerUrl, UserProfileCacheService userProfileCacheService, bool gridmode)
        {
            _inventoryServerUrl = HGNetworkServersInfo.ServerURI(inventoryServerUrl);
            //m_Uri = new Uri(_inventoryServerUrl);
            //m_userProfileCache = userProfileCacheService;
            m_gridmode = gridmode;
        }

        #region ISecureInventoryService Members

        public void RequestInventoryForUser(UUID userID, UUID session_id, InventoryReceiptCallback callback)
        {
            if (IsLocalStandaloneUser(userID))
            {
                base.RequestInventoryForUser(userID, callback);
                return;
            }

            // grid/hypergrid mode
            lock (m_RequestingInventory)
            {
                if (!m_RequestingInventory.ContainsKey(userID))
                {
                    m_RequestingInventory.Add(userID, callback);
                }
                else
                {
                    m_log.ErrorFormat("[HGrid INVENTORY SERVICE]: RequestInventoryForUser() - could  not find user profile for {0}", userID);
                    return;
                }
            }
            string invServer = GetUserInventoryURI(userID);
            m_log.InfoFormat(
                "[HGrid INVENTORY SERVICE]: Requesting inventory from {0}/GetInventory/ for user {1} ({2})",
                /*_inventoryServerUrl*/ invServer, userID, userID.Guid);

            try
            {

                //RestSessionObjectPosterResponse<Guid, InventoryCollection> requester
                //    = new RestSessionObjectPosterResponse<Guid, InventoryCollection>();
                //requester.ResponseCallback = InventoryResponse;

                //requester.BeginPostObject(invServer + "/GetInventory/", userID.Guid, session_id.ToString(), userID.ToString());
                GetInventoryDelegate d = GetInventoryAsync;
                d.BeginInvoke(invServer, userID, session_id, GetInventoryCompleted, d);

            }
            catch (WebException e)
            {
                if (StatsManager.SimExtraStats != null)
                    StatsManager.SimExtraStats.AddInventoryServiceRetrievalFailure();

                m_log.ErrorFormat("[HGrid INVENTORY SERVICE]: Request inventory operation failed, {0} {1}",
                    e.Source, e.Message);

                // Well, let's synthesize one
                InventoryCollection icol = new InventoryCollection();
                icol.UserID = userID;
                icol.Items = new List<InventoryItemBase>();
                icol.Folders = new List<InventoryFolderBase>();
                InventoryFolderBase rootFolder = new InventoryFolderBase();
                rootFolder.ID = UUID.Random();
                rootFolder.Owner = userID;
                icol.Folders.Add(rootFolder);
                InventoryResponse(icol);
            }            
        }

        private delegate InventoryCollection GetInventoryDelegate(string url, UUID userID, UUID sessionID);

        protected InventoryCollection GetInventoryAsync(string url, UUID userID, UUID sessionID)
        {
            InventoryCollection icol = null;
            try
            {
                icol = SynchronousRestSessionObjectPoster<Guid, InventoryCollection>.BeginPostObject("POST", url + "/GetInventory/", 
                    userID.Guid, sessionID.ToString(), userID.ToString());

            }
            catch (Exception e)
            {
                m_log.Debug("[HGrid]: Exception getting users inventory: " + e.Message);
            }
            if (icol == null)
            {
                // Well, let's synthesize one
                icol = new InventoryCollection();
                icol.UserID = userID;
                icol.Items = new List<InventoryItemBase>();
                icol.Folders = new List<InventoryFolderBase>();
                InventoryFolderBase rootFolder = new InventoryFolderBase();
                rootFolder.ID = UUID.Random();
                rootFolder.Owner = userID;
                icol.Folders.Add(rootFolder);
            }

            return icol;
        }

        private void GetInventoryCompleted(IAsyncResult iar)
        {
            GetInventoryDelegate icon = (GetInventoryDelegate)iar.AsyncState;
            InventoryCollection icol = icon.EndInvoke(iar);
            InventoryResponse(icol);
        }

        /// <summary>
        /// Add a new folder to the user's inventory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully added</returns>
        public bool AddFolder(InventoryFolderBase folder, UUID session_id)
        {
            if (IsLocalStandaloneUser(folder.Owner))
            {
                return base.AddFolder(folder);
            }

            try
            {
                string invServ = GetUserInventoryURI(folder.Owner);

                return SynchronousRestSessionObjectPoster<InventoryFolderBase, bool>.BeginPostObject(
                    "POST", invServ + "/NewFolder/", folder, session_id.ToString(), folder.Owner.ToString());
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[HGrid INVENTORY SERVICE]: Add new inventory folder operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;

        }

        /// <summary>
        /// Update a folder in the user's inventory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully updated</returns>
        public bool UpdateFolder(InventoryFolderBase folder, UUID session_id)
        {
            if (IsLocalStandaloneUser(folder.Owner))
            {
                return base.UpdateFolder(folder);
            }
            try
            {
                string invServ = GetUserInventoryURI(folder.Owner);

                return SynchronousRestSessionObjectPoster<InventoryFolderBase, bool>.BeginPostObject(
                    "POST", invServ + "/UpdateFolder/", folder, session_id.ToString(), folder.Owner.ToString());
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[HGrid INVENTORY SERVICE]: Update inventory folder operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;

        }

        /// <summary>
        /// Move an inventory folder to a new location
        /// </summary>
        /// <param name="folder">A folder containing the details of the new location</param>
        /// <returns>true if the folder was successfully moved</returns>
        public bool MoveFolder(InventoryFolderBase folder, UUID session_id)
        {
            if (IsLocalStandaloneUser(folder.Owner))
            {
                return base.MoveFolder(folder);
            }

            try
            {
                string invServ = GetUserInventoryURI(folder.Owner);

                return SynchronousRestSessionObjectPoster<InventoryFolderBase, bool>.BeginPostObject(
                    "POST", invServ + "/MoveFolder/", folder, session_id.ToString(), folder.Owner.ToString());
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[HGrid INVENTORY SERVICE]: Move inventory folder operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        /// <summary>
        /// Purge an inventory folder of all its items and subfolders.
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>true if the folder was successfully purged</returns>
        public bool PurgeFolder(InventoryFolderBase folder, UUID session_id)
        {
            if (IsLocalStandaloneUser(folder.Owner))
            {
                return base.PurgeFolder(folder);
            }

            try
            {
                string invServ = GetUserInventoryURI(folder.Owner);

                return SynchronousRestSessionObjectPoster<InventoryFolderBase, bool>.BeginPostObject(
                    "POST", invServ + "/PurgeFolder/", folder, session_id.ToString(), folder.Owner.ToString());
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[HGrid INVENTORY SERVICE]: Move inventory folder operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        /// <summary>
        /// Add a new item to the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully added</returns>
        public bool AddItem(InventoryItemBase item, UUID session_id)
        {
            if (IsLocalStandaloneUser(item.Owner))
            {
                return base.AddItem(item);
            }

            try
            {
                string invServ = GetUserInventoryURI(item.Owner);

                return SynchronousRestSessionObjectPoster<InventoryItemBase, bool>.BeginPostObject(
                    "POST", invServ + "/NewItem/", item, session_id.ToString(), item.Owner.ToString());
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[HGrid INVENTORY SERVICE]: Add new inventory item operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        /// <summary>
        /// Update an item in the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully updated</returns>
        public bool UpdateItem(InventoryItemBase item, UUID session_id)
        {
            if (IsLocalStandaloneUser(item.Owner))
            {
                return base.UpdateItem(item);
            }

            try
            {
                string invServ = GetUserInventoryURI(item.Owner);
                return SynchronousRestSessionObjectPoster<InventoryItemBase, bool>.BeginPostObject(
                    "POST", invServ + "/NewItem/", item, session_id.ToString(), item.Owner.ToString());
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[HGrid INVENTORY SERVICE]: Update new inventory item operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        /// <summary>
        /// Delete an item from the user's inventory
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if the item was successfully deleted</returns>
        public bool DeleteItem(InventoryItemBase item, UUID session_id)
        {
            if (IsLocalStandaloneUser(item.Owner))
            {
                return base.DeleteItem(item);
            }

            try
            {
                string invServ = GetUserInventoryURI(item.Owner);

                return SynchronousRestSessionObjectPoster<InventoryItemBase, bool>.BeginPostObject(
                    "POST", invServ + "/DeleteItem/", item, session_id.ToString(), item.Owner.ToString());
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[HGrid INVENTORY SERVICE]: Delete inventory item operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        public InventoryItemBase QueryItem(InventoryItemBase item, UUID session_id)
        {
            if (IsLocalStandaloneUser(item.Owner))
            {
                return base.QueryItem(item);
            }

            try
            {
                string invServ = GetUserInventoryURI(item.Owner);

                return SynchronousRestSessionObjectPoster<InventoryItemBase, InventoryItemBase>.BeginPostObject(
                    "POST", invServ + "/QueryItem/", item, session_id.ToString(), item.Owner.ToString());
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[HGrid INVENTORY SERVICE]: Query inventory item operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return null;
        }

        public InventoryFolderBase QueryFolder(InventoryFolderBase item, UUID session_id)
        {
            if (IsLocalStandaloneUser(item.Owner))
            {
                return base.QueryFolder(item);
            }

            try
            {
                string invServ = GetUserInventoryURI(item.Owner);

                return SynchronousRestSessionObjectPoster<InventoryFolderBase, InventoryFolderBase>.BeginPostObject(
                    "POST", invServ + "/QueryFolder/", item, session_id.ToString(), item.Owner.ToString());
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[HGrid INVENTORY SERVICE]: Query inventory item operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return null;
        }
        #endregion

        #region Methods common to ISecureInventoryService and IInventoryService

        /// <summary>
        /// Does the given user have an inventory structure?
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public override bool HasInventoryForUser(UUID userID)
        {
            if (IsLocalStandaloneUser(userID))
            {
                return base.HasInventoryForUser(userID);
            }
            return false;
        }

        /// <summary>
        /// Retrieve the root inventory folder for the given user.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>null if no root folder was found</returns>
        public override InventoryFolderBase RequestRootFolder(UUID userID)
        {
            if (IsLocalStandaloneUser(userID))
            {
                return base.RequestRootFolder(userID);
            }

            return null;
        }

        #endregion


        /// <summary>
        /// Callback used by the inventory server GetInventory request
        /// </summary>
        /// <param name="userID"></param>
        private void InventoryResponse(InventoryCollection response)
        {
            UUID userID = response.UserID;
            InventoryReceiptCallback callback = null;
            lock (m_RequestingInventory)
            {
                if (m_RequestingInventory.ContainsKey(userID))
                {
                    m_log.InfoFormat("[HGrid INVENTORY SERVICE]: " +
                                     "Received inventory response for user {0} containing {1} folders and {2} items",
                                     userID, response.Folders.Count, response.Items.Count);
                    callback = m_RequestingInventory[userID];
                    m_RequestingInventory.Remove(userID);
                }
                else
                {
                    m_log.WarnFormat(
                        "[HGrid INVENTORY SERVICE]: " +
                        "Received inventory response for {0} for which we do not have a record of requesting!",
                        userID);
                    return;
                }
            }

            InventoryFolderImpl rootFolder = null;

            ICollection<InventoryFolderImpl> folders = new List<InventoryFolderImpl>();
            ICollection<InventoryItemBase> items = new List<InventoryItemBase>();

            foreach (InventoryFolderBase folder in response.Folders)
            {
                if (folder.ParentID == UUID.Zero)
                {
                    rootFolder = new InventoryFolderImpl(folder);
                    folders.Add(rootFolder);

                    break;
                }
            }

            if (rootFolder != null)
            {
                foreach (InventoryFolderBase folder in response.Folders)
                {
                    if (folder.ID != rootFolder.ID)
                    {
                        folders.Add(new InventoryFolderImpl(folder));
                    }
                }

                foreach (InventoryItemBase item in response.Items)
                {
                    items.Add(item);
                }
            }
            else
            {
                m_log.ErrorFormat("[HGrid INVENTORY SERVICE]: Did not get back an inventory containing a root folder for user {0}", userID);
            }

            callback(folders, items);

        }

        private bool IsLocalStandaloneUser(UUID userID)
        {
            if (m_userProfileCache == null)
                return false;

            CachedUserInfo uinfo = m_userProfileCache.GetUserDetails(userID);
            if (uinfo == null)
                return true;

            string userInventoryServerURI = HGNetworkServersInfo.ServerURI(uinfo.UserProfile.UserInventoryURI);

            if ((!m_gridmode) && ((userInventoryServerURI == _inventoryServerUrl)) || (userInventoryServerURI == ""))
            {
                return true;
            }
            return false;
        }

        private string GetUserInventoryURI(UUID userID)
        {
            string invURI = _inventoryServerUrl;

            CachedUserInfo uinfo = m_userProfileCache.GetUserDetails(userID);
            if ((uinfo == null) || (uinfo.UserProfile == null))
                return invURI;

            string userInventoryServerURI = HGNetworkServersInfo.ServerURI(uinfo.UserProfile.UserInventoryURI);

            if ((userInventoryServerURI != null) &&
                (userInventoryServerURI != ""))
                invURI = userInventoryServerURI;
            return invURI;
        }

    }
}
