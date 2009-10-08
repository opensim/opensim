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

using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Services.Connectors
{
    public class InventoryServicesConnector : ISessionAuthInventoryService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;

        private Dictionary<UUID, InventoryReceiptCallback> m_RequestingInventory = new Dictionary<UUID, InventoryReceiptCallback>();
        private Dictionary<UUID, DateTime> m_RequestTime = new Dictionary<UUID, DateTime>();

        public InventoryServicesConnector()
        {
        }

        public InventoryServicesConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/');
        }

        public InventoryServicesConnector(IConfigSource source)
        {
            Initialise(source);
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig inventoryConfig = source.Configs["InventoryService"];
            if (inventoryConfig == null)
            {
                m_log.Error("[INVENTORY CONNECTOR]: InventoryService missing from OpenSim.ini");
                throw new Exception("InventoryService missing from OpenSim.ini");
            }

            string serviceURI = inventoryConfig.GetString("InventoryServerURI",
                    String.Empty);

            if (serviceURI == String.Empty)
            {
                m_log.Error("[INVENTORY CONNECTOR]: No Server URI named in section InventoryService");
                throw new Exception("Unable to proceed. Please make sure your ini files in config-include are updated according to .example's");
            }
            m_ServerURI = serviceURI.TrimEnd('/');
        }

        #region ISessionAuthInventoryService

        public string Host
        {
            get { return m_ServerURI; }
        }

        /// <summary>
        /// Caller must catch eventual Exceptions.
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="sessionID"></param>
        /// <param name="callback"></param>
        public void GetUserInventory(string userIDStr, UUID sessionID, InventoryReceiptCallback callback)
        {
            UUID userID = UUID.Zero;
            if (UUID.TryParse(userIDStr, out userID))
            {
                lock (m_RequestingInventory)
                {
                    // *HACK ALERT*

                    // If an inventory request times out, it blocks any further requests from the 
                    // same user, even after a relog. This is bad, and makes me sad.

                    // Really, we should detect a timeout and report a failure to the callback,
                    // BUT in my testing i found that it's hard to detect a timeout.. sometimes,
                    // a partial response is recieved, and sometimes a null response.

                    // So, for now, add a timer of ten seconds (which is the request timeout).

                    // This should basically have the same effect.

                    lock (m_RequestTime)
                    {
                        if (m_RequestTime.ContainsKey(userID))
                        {
                            TimeSpan interval = DateTime.Now - m_RequestTime[userID];
                            if (interval.TotalSeconds > 10)
                            {
                                m_RequestTime.Remove(userID);
                                if (m_RequestingInventory.ContainsKey(userID))
                                {
                                    m_RequestingInventory.Remove(userID);
                                }
                            }
                        }
                        if (!m_RequestingInventory.ContainsKey(userID))
                        {
                            m_RequestTime.Add(userID, DateTime.Now);
                            m_RequestingInventory.Add(userID, callback);
                        }
                        else
                        {
                            m_log.ErrorFormat("[INVENTORY CONNECTOR]: GetUserInventory - ignoring repeated request for user {0}", userID);
                            return;
                        }
                    }
                }

                m_log.InfoFormat(
                    "[INVENTORY CONNECTOR]: Requesting inventory from {0}/GetInventory/ for user {1}",
                    m_ServerURI, userID);

                RestSessionObjectPosterResponse<Guid, InventoryCollection> requester
                    = new RestSessionObjectPosterResponse<Guid, InventoryCollection>();
                requester.ResponseCallback = InventoryResponse;

                requester.BeginPostObject(m_ServerURI + "/GetInventory/", userID.Guid, sessionID.ToString(), userID.ToString());
            }
        }

        /// <summary>
        /// Gets the user folder for the given folder-type
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public Dictionary<AssetType, InventoryFolderBase> GetSystemFolders(string userID, UUID sessionID)
        {
            List<InventoryFolderBase> folders = null;
            Dictionary<AssetType, InventoryFolderBase> dFolders = new Dictionary<AssetType, InventoryFolderBase>();
            try
            {
                folders = SynchronousRestSessionObjectPoster<Guid, List<InventoryFolderBase>>.BeginPostObject(
                    "POST", m_ServerURI + "/SystemFolders/", new Guid(userID), sessionID.ToString(), userID.ToString());

                foreach (InventoryFolderBase f in folders)
                    dFolders[(AssetType)f.Type] = f;

                return dFolders;
            }
            catch (Exception e)
            {
                // Maybe we're talking to an old inventory server. Try this other thing.
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: GetSystemFolders operation failed, {0} {1} (old sever?). Trying GetInventory.",
                     e.Source, e.Message);

                try
                {
                    InventoryCollection inventory = SynchronousRestSessionObjectPoster<Guid, InventoryCollection>.BeginPostObject(
                        "POST", m_ServerURI + "/GetInventory/", new Guid(userID), sessionID.ToString(), userID.ToString());
                    folders = inventory.Folders;
                }
                catch (Exception ex)
                {
                    m_log.ErrorFormat("[INVENTORY CONNECTOR]: GetInventory operation also failed, {0} {1}. Giving up.",
                         e.Source, ex.Message);
                }

                if ((folders != null) && (folders.Count > 0))
                {
                    m_log.DebugFormat("[INVENTORY CONNECTOR]: Received entire inventory ({0} folders) for user {1}",
                        folders.Count, userID);
                    foreach (InventoryFolderBase f in folders)
                    {
                        if ((f.Type != (short)AssetType.Folder) && (f.Type != (short)AssetType.Unknown))
                        dFolders[(AssetType)f.Type] = f;
                    }

                    UUID rootFolderID = dFolders[AssetType.Animation].ParentID;
                    InventoryFolderBase rootFolder = new InventoryFolderBase(rootFolderID, new UUID(userID));
                    rootFolder = QueryFolder(userID, rootFolder, sessionID);
                    dFolders[AssetType.Folder] = rootFolder;
                    m_log.DebugFormat("[INVENTORY CONNECTOR]: {0} system folders for user {1}", dFolders.Count, userID);
                    return dFolders;
                }
            }

            return new Dictionary<AssetType, InventoryFolderBase>();
        }

        /// <summary>
        /// Gets everything (folders and items) inside a folder
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="folderID"></param>
        /// <returns></returns>
        public InventoryCollection GetFolderContent(string userID, UUID folderID, UUID sessionID)
        {
            try
            {
                // normal case
                return SynchronousRestSessionObjectPoster<Guid, InventoryCollection>.BeginPostObject(
                    "POST", m_ServerURI + "/GetFolderContent/", folderID.Guid, sessionID.ToString(), userID.ToString());
            }
            catch (TimeoutException e)
            {
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: GetFolderContent operation to {0} timed out {0} {1}.", m_ServerURI,
                     e.Source, e.Message);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: GetFolderContent operation failed, {0} {1} (old server?).",
                     e.Source, e.Message);
            }

            InventoryCollection nullCollection = new InventoryCollection();
            nullCollection.Folders = new List<InventoryFolderBase>();
            nullCollection.Items = new List<InventoryItemBase>();
            nullCollection.UserID = new UUID(userID);
            return nullCollection;
        }

        public bool AddFolder(string userID, InventoryFolderBase folder, UUID sessionID)
        {
            try
            {
                return SynchronousRestSessionObjectPoster<InventoryFolderBase, bool>.BeginPostObject(
                    "POST", m_ServerURI + "/NewFolder/", folder, sessionID.ToString(), folder.Owner.ToString());
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: Add new inventory folder operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        public bool UpdateFolder(string userID, InventoryFolderBase folder, UUID sessionID)
        {
            try
            {
                return SynchronousRestSessionObjectPoster<InventoryFolderBase, bool>.BeginPostObject(
                    "POST", m_ServerURI + "/UpdateFolder/", folder, sessionID.ToString(), folder.Owner.ToString());
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: Update inventory folder operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        public bool DeleteFolders(string userID, List<UUID> folderIDs, UUID sessionID)
        {
            try
            {
                List<Guid> guids = new List<Guid>();
                foreach (UUID u in folderIDs)
                    guids.Add(u.Guid);
                return SynchronousRestSessionObjectPoster<List<Guid>, bool>.BeginPostObject(
                    "POST", m_ServerURI + "/DeleteFolders/", guids, sessionID.ToString(), userID);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: Delete inventory folders operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        public bool MoveFolder(string userID, InventoryFolderBase folder, UUID sessionID)
        {
            try
            {
                return SynchronousRestSessionObjectPoster<InventoryFolderBase, bool>.BeginPostObject(
                    "POST", m_ServerURI + "/MoveFolder/", folder, sessionID.ToString(), folder.Owner.ToString());
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: Move inventory folder operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        public bool PurgeFolder(string userID, InventoryFolderBase folder, UUID sessionID)
        {
            try
            {
                return SynchronousRestSessionObjectPoster<InventoryFolderBase, bool>.BeginPostObject(
                    "POST", m_ServerURI + "/PurgeFolder/", folder, sessionID.ToString(), folder.Owner.ToString());
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: Move inventory folder operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        public List<InventoryItemBase> GetFolderItems(string userID, UUID folderID, UUID sessionID)
        {
            try
            {
                InventoryFolderBase folder = new InventoryFolderBase(folderID, new UUID(userID));
                return SynchronousRestSessionObjectPoster<InventoryFolderBase, List<InventoryItemBase>>.BeginPostObject(
                    "POST", m_ServerURI + "/GetItems/", folder, sessionID.ToString(), userID);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: Get folder items operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return null;
        }

        public bool AddItem(string userID, InventoryItemBase item, UUID sessionID)
        {
            try
            {
                return SynchronousRestSessionObjectPoster<InventoryItemBase, bool>.BeginPostObject(
                    "POST", m_ServerURI + "/NewItem/", item, sessionID.ToString(), item.Owner.ToString());
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: Add new inventory item operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        public bool UpdateItem(string userID, InventoryItemBase item, UUID sessionID)
        {
            try
            {
                return SynchronousRestSessionObjectPoster<InventoryItemBase, bool>.BeginPostObject(
                    "POST", m_ServerURI + "/NewItem/", item, sessionID.ToString(), item.Owner.ToString());
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: Update new inventory item operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        /**
         * MoveItems Async group
         */

        delegate void MoveItemsDelegate(string userID, List<InventoryItemBase> items, UUID sessionID);

        private void MoveItemsAsync(string userID, List<InventoryItemBase> items, UUID sessionID)
        {
            if (items == null)
            {
                m_log.WarnFormat("[INVENTORY CONNECTOR]: request to move items got a null list.");
                return;
            }

            try
            {
                //SynchronousRestSessionObjectPoster<List<InventoryItemBase>, bool>.BeginPostObject(
                //    "POST", m_ServerURI + "/MoveItems/", items, sessionID.ToString(), userID.ToString());

                //// Success
                //return;
                string uri = m_ServerURI + "/inventory/" + userID;
                if (SynchronousRestObjectRequester.
                        MakeRequest<List<InventoryItemBase>, bool>("PUT", uri, items))
                    m_log.DebugFormat("[INVENTORY CONNECTOR]: move {0} items poster succeeded {1}", items.Count, uri);
                else
                    m_log.DebugFormat("[INVENTORY CONNECTOR]: move {0} items poster failed {1}", items.Count, uri); ;

                return;

            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: Move inventory items operation failed, {0} {1} (old server?). Trying slow way.",
                     e.Source, e.Message);
            }

        }

        private void MoveItemsCompleted(IAsyncResult iar)
        {
            MoveItemsDelegate d = (MoveItemsDelegate)iar.AsyncState;
            d.EndInvoke(iar);
        }

        public bool MoveItems(string userID, List<InventoryItemBase> items, UUID sessionID)
        {
            MoveItemsDelegate d = MoveItemsAsync;
            d.BeginInvoke(userID, items, sessionID, MoveItemsCompleted, d);
            return true;
        }

        public bool DeleteItems(string userID, List<UUID> items, UUID sessionID)
        {
            try
            {
                List<Guid> guids = new List<Guid>();
                foreach (UUID u in items)
                    guids.Add(u.Guid);
                return SynchronousRestSessionObjectPoster<List<Guid>, bool>.BeginPostObject(
                    "POST", m_ServerURI + "/DeleteItem/", guids, sessionID.ToString(), userID);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: Delete inventory item operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return false;
        }

        public InventoryItemBase QueryItem(string userID, InventoryItemBase item, UUID sessionID)
        {
            try
            {
                return SynchronousRestSessionObjectPoster<InventoryItemBase, InventoryItemBase>.BeginPostObject(
                    "POST", m_ServerURI + "/QueryItem/", item, sessionID.ToString(), item.Owner.ToString());
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: Query inventory item operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return null;
        }

        public InventoryFolderBase QueryFolder(string userID, InventoryFolderBase folder, UUID sessionID)
        {
            try
            {
                return SynchronousRestSessionObjectPoster<InventoryFolderBase, InventoryFolderBase>.BeginPostObject(
                    "POST", m_ServerURI + "/QueryFolder/", folder, sessionID.ToString(), userID);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: Query inventory item operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return null;
        }

        public int GetAssetPermissions(string userID, UUID assetID, UUID sessionID)
        {
            try
            {
                InventoryItemBase item = new InventoryItemBase();
                item.Owner = new UUID(userID);
                item.AssetID = assetID;
                return SynchronousRestSessionObjectPoster<InventoryItemBase, int>.BeginPostObject(
                    "POST", m_ServerURI + "/AssetPermissions/", item, sessionID.ToString(), userID);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: AssetPermissions operation failed, {0} {1}",
                     e.Source, e.Message);
            }

            return 0;
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
                    callback = m_RequestingInventory[userID];
                    m_RequestingInventory.Remove(userID);
                    lock (m_RequestTime)
                    {
                        if (m_RequestTime.ContainsKey(userID))
                        {
                            m_RequestTime.Remove(userID);
                        }
                    }
                }
                else
                {
                    m_log.WarnFormat(
                        "[INVENTORY CONNECTOR]: " +
                        "Received inventory response for {0} for which we do not have a record of requesting!",
                        userID);
                    return;
                }
            }

            m_log.InfoFormat("[INVENTORY CONNECTOR]: " +
                             "Received inventory response for user {0} containing {1} folders and {2} items",
                             userID, response.Folders.Count, response.Items.Count);

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
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: Did not get back an inventory containing a root folder for user {0}", userID);
            }

            callback(folders, items);

        }


    }
}
