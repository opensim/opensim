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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Net;

using libsecondlife;
using log4net;
using Nwc.XmlRpc;

using OpenSim.Framework;
using OpenSim.Framework.Communications;

namespace OpenSim.Grid.InventoryServer
{
    /// <summary>
    /// Used on a grid server to satisfy external inventory requests
    /// </summary>
    public class GridInventoryService : InventoryServiceBase
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_userserver_url;

        public GridInventoryService(string userserver_url)
        {
            m_userserver_url = userserver_url;
        }

        public bool CheckTrustSource(IPEndPoint peer)
        {
            m_log.InfoFormat("[GRID AGENT INVENTORY]: checking trusted source {0}", peer.ToString());
            UriBuilder ub = new UriBuilder(m_userserver_url);
            if (ub.Host == peer.Address.ToString())
            {
                return true;
            }
            return false;
        }

        public bool CheckAuthSession(string session_id, string avatar_id)
        {
            m_log.InfoFormat("[GRID AGENT INVENTORY]: checking authed session {0} {1}", session_id, avatar_id);
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
                return true;
            }
            return false;
        }

        public override void RequestInventoryForUser(LLUUID userID, InventoryReceiptCallback callback)
        {
        }

        /// <summary>
        /// Return a user's entire inventory
        /// </summary>
        /// <param name="rawUserID"></param>
        /// <returns>The user's inventory.  If an inventory cannot be found then an empty collection is returned.</returns>
        public InventoryCollection GetUserInventory(Guid rawUserID)
        {
            LLUUID userID = new LLUUID(rawUserID);

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

        /// <summary>
        /// Guid to UUID wrapper for same name IInventoryServices method
        /// </summary>
        /// <param name="rawUserID"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> GetInventorySkeleton(Guid rawUserID)
        {
            LLUUID userID = new LLUUID(rawUserID);
            return GetInventorySkeleton(userID);
        }

        /// <summary>
        /// Create an inventory for the given user.
        /// </summary>
        /// <param name="rawUserID"></param>
        /// <returns></returns>
        public bool CreateUsersInventory(Guid rawUserID)
        {
            LLUUID userID = new LLUUID(rawUserID);

            m_log.InfoFormat("[GRID AGENT INVENTORY]: Creating new set of inventory folders for user {0}", userID);

            return CreateNewUserInventory(userID);
        }
    }
}
