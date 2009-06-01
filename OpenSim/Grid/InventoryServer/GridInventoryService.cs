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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;

namespace OpenSim.Grid.InventoryServer
{
    /// <summary>
    /// Used on a grid server to satisfy external inventory requests
    /// </summary>
    public class GridInventoryService : InventoryServiceBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        private bool m_doLookup = false;

        public bool DoLookup
        {
            get { return m_doLookup; }
            set { m_doLookup = value; }
        }
        
        private static readonly int INVENTORY_DEFAULT_SESSION_TIME = 30; // secs

        private string m_userserver_url;
        private AuthedSessionCache m_session_cache = new AuthedSessionCache(INVENTORY_DEFAULT_SESSION_TIME);

        public GridInventoryService(string userserver_url)
        {
            m_userserver_url = userserver_url;
        }

        /// <summary>
        /// Check that the source of an inventory request is one that we trust.
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        public bool CheckTrustSource(IPEndPoint peer)
        {
            if (m_doLookup)
            {
                m_log.InfoFormat("[GRID AGENT INVENTORY]: Checking trusted source {0}", peer);
                UriBuilder ub = new UriBuilder(m_userserver_url);
                IPAddress[] uaddrs = Dns.GetHostAddresses(ub.Host);
                foreach (IPAddress uaddr in uaddrs)
                {
                    if (uaddr.Equals(peer.Address))
                    {
                        return true;
                    }
                }

                m_log.WarnFormat(
                    "[GRID AGENT INVENTORY]: Rejecting request since source {0} was not in the list of trusted sources",
                    peer);

                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Check that the source of an inventory request for a particular agent is a current session belonging to
        /// that agent.
        /// </summary>
        /// <param name="session_id"></param>
        /// <param name="avatar_id"></param>
        /// <returns></returns>
        public bool CheckAuthSession(string session_id, string avatar_id)
        {
            if (m_doLookup)
            {
                m_log.InfoFormat("[GRID AGENT INVENTORY]: checking authed session {0} {1}", session_id, avatar_id);

                if (m_session_cache.getCachedSession(session_id, avatar_id) == null)
                {
                    // cache miss, ask userserver
                    Hashtable requestData = new Hashtable();
                    requestData["avatar_uuid"] = avatar_id;
                    requestData["session_id"] = session_id;
                    ArrayList SendParams = new ArrayList();
                    SendParams.Add(requestData);
                    XmlRpcRequest UserReq = new XmlRpcRequest("check_auth_session", SendParams);
                    XmlRpcResponse UserResp = UserReq.Send(m_userserver_url, 3000);

                    Hashtable responseData = (Hashtable)UserResp.Value;
                    if (responseData.ContainsKey("auth_session") && responseData["auth_session"].ToString() == "TRUE")
                    {
                        m_log.Info("[GRID AGENT INVENTORY]: got authed session from userserver");
                        // add to cache; the session time will be automatically renewed
                        m_session_cache.Add(session_id, avatar_id);
                        return true;
                    }
                }
                else
                {
                    // cache hits
                    m_log.Info("[GRID AGENT INVENTORY]: got authed session from cache");
                    return true;
                }

                m_log.Warn("[GRID AGENT INVENTORY]: unknown session_id, request rejected");
                return false;
            }
            else
            {
                return true;
            }
        }

        public override void RequestInventoryForUser(UUID userID, InventoryReceiptCallback callback)
        {
        }

        /// <summary>
        /// Return a user's entire inventory
        /// </summary>
        /// <param name="rawUserID"></param>
        /// <returns>The user's inventory.  If an inventory cannot be found then an empty collection is returned.</returns>
        public InventoryCollection GetUserInventory(Guid rawUserID)
        {
            UUID userID = new UUID(rawUserID);

            m_log.InfoFormat("[GRID AGENT INVENTORY]: Processing request for inventory of {0}", userID);

            // Uncomment me to simulate a slow responding inventory server
            //Thread.Sleep(16000);

            InventoryCollection invCollection = new InventoryCollection();

            List<InventoryFolderBase> allFolders = GetInventorySkeleton(userID);

            if (null == allFolders)
            {
                m_log.WarnFormat("[GRID AGENT INVENTORY]: No inventory found for user {0}", rawUserID);

                return invCollection;
            }

            List<InventoryItemBase> allItems = new List<InventoryItemBase>();

            foreach (InventoryFolderBase folder in allFolders)
            {
                List<InventoryItemBase> items = RequestFolderItems(folder.ID);

                if (items != null)
                {
                    allItems.InsertRange(0, items);
                }
            }

            invCollection.UserID = userID;
            invCollection.Folders = allFolders;
            invCollection.Items = allItems;

            //            foreach (InventoryFolderBase folder in invCollection.Folders)
            //            {
            //                m_log.DebugFormat("[GRID AGENT INVENTORY]: Sending back folder {0} {1}", folder.Name, folder.ID);
            //            }
            //
            //            foreach (InventoryItemBase item in invCollection.Items)
            //            {
            //                m_log.DebugFormat("[GRID AGENT INVENTORY]: Sending back item {0} {1}, folder {2}", item.Name, item.ID, item.Folder);
            //            }

            m_log.InfoFormat(
                "[GRID AGENT INVENTORY]: Sending back inventory response to user {0} containing {1} folders and {2} items",
                invCollection.UserID, invCollection.Folders.Count, invCollection.Items.Count);

            return invCollection;
        }

        public List<InventoryItemBase> GetFolderItems(Guid folderID)
        {
            List<InventoryItemBase> allItems = new List<InventoryItemBase>();


            List<InventoryItemBase> items = RequestFolderItems(new UUID(folderID));

            if (items != null)
            {
                allItems.InsertRange(0, items);
            }
            m_log.InfoFormat(
              "[GRID AGENT INVENTORY]: Sending back inventory response  containing {0} items", allItems.Count.ToString());
            return allItems;
        }

        /// <summary>
        /// Guid to UUID wrapper for same name IInventoryServices method
        /// </summary>
        /// <param name="rawUserID"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> GetInventorySkeleton(Guid rawUserID)
        {
            UUID userID = new UUID(rawUserID);
            return GetInventorySkeleton(userID);
        }

        /// <summary>
        /// Create an inventory for the given user.
        /// </summary>
        /// <param name="rawUserID"></param>
        /// <returns></returns>
        public bool CreateUsersInventory(Guid rawUserID)
        {
            UUID userID = new UUID(rawUserID);

            m_log.InfoFormat("[GRID AGENT INVENTORY]: Creating new set of inventory folders for user {0}", userID);

            return CreateNewUserInventory(userID);
        }

        public List<InventoryItemBase> GetActiveGestures(Guid rawUserID)
        {
            UUID userID = new UUID(rawUserID);

            m_log.InfoFormat("[GRID AGENT INVENTORY]: fetching active gestures for user {0}", userID);

            return GetActiveGestures(userID);
        }
    }
}
