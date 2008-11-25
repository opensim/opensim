/**
 * Copyright (c) 2008, Contributors. All rights reserved.
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 *     * Redistributions of source code must retain the above copyright notice, 
 *       this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright notice, 
 *       this list of conditions and the following disclaimer in the documentation 
 *       and/or other materials provided with the distribution.
 *     * Neither the name of the Organizations nor the names of Individual
 *       Contributors may be used to endorse or promote products derived from 
 *       this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES 
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL 
 * THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, 
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED 
 * AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED 
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 */

using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

using log4net;
using Nini.Config;

using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Hypergrid
{
    public class HGStandaloneInventoryService : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool initialized = false;
        private static bool enabled = false;
        
        Scene m_scene;
        InventoryService m_inventoryService;
        
        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (!initialized)
            {
                initialized = true;
                m_scene = scene;

                // This module is only on for standalones
                enabled = !config.Configs["Startup"].GetBoolean("gridmode", true) && config.Configs["Startup"].GetBoolean("hypergrid", false);
            }
        }

        public void PostInitialise()
        {
            if (enabled)
            {
                m_log.Info("[HGStandaloneInvService]: Starting...");
                m_inventoryService = new InventoryService(m_scene);
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "HGStandaloneInventoryService"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

    }

    public class InventoryService 
    {
        private InventoryServiceBase m_inventoryService;
        private IUserService m_userService;
        private bool m_doLookup = false;

        public bool DoLookup
        {
            get { return m_doLookup; }
            set { m_doLookup = value; }
        }
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public InventoryService(Scene m_scene)
        {
            m_inventoryService = (InventoryServiceBase)m_scene.CommsManager.SecureInventoryService;
            m_userService = m_scene.CommsManager.UserService;
            AddHttpHandlers(m_scene);

        }

        protected void AddHttpHandlers(Scene m_scene)
        {
            m_scene.AddStreamHandler(
                new RestDeserialiseSecureHandler<Guid, InventoryCollection>(
                    "POST", "/GetInventory/", GetUserInventory, CheckAuthSession));

            m_scene.AddStreamHandler(
                new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                    "POST", "/NewFolder/", m_inventoryService.AddFolder, CheckAuthSession));

            m_scene.AddStreamHandler(
                new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                    "POST", "/UpdateFolder/", m_inventoryService.UpdateFolder, CheckAuthSession));

            m_scene.AddStreamHandler(
                new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                    "POST", "/MoveFolder/", m_inventoryService.MoveFolder, CheckAuthSession));

            m_scene.AddStreamHandler(
                new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                    "POST", "/PurgeFolder/", m_inventoryService.PurgeFolder, CheckAuthSession));

            m_scene.AddStreamHandler(
                new RestDeserialiseSecureHandler<InventoryItemBase, bool>(
                    "POST", "/NewItem/", m_inventoryService.AddItem, CheckAuthSession));

            m_scene.AddStreamHandler(
                new RestDeserialiseSecureHandler<InventoryItemBase, bool>(
                    "POST", "/DeleteItem/", m_inventoryService.DeleteItem, CheckAuthSession));

            //// WARNING: Root folders no longer just delivers the root and immediate child folders (e.g
            //// system folders such as Objects, Textures), but it now returns the entire inventory skeleton.
            //// It would have been better to rename this request, but complexities in the BaseHttpServer
            //// (e.g. any http request not found is automatically treated as an xmlrpc request) make it easier
            //// to do this for now.
            //m_scene.AddStreamHandler(
            //    new RestDeserialiseTrustedHandler<Guid, List<InventoryFolderBase>>
            //        ("POST", "/RootFolders/", GetInventorySkeleton, CheckTrustSource));

            //// for persistent active gestures
            //m_scene.AddStreamHandler(
            //    new RestDeserialiseTrustedHandler<Guid, List<InventoryItemBase>>
            //        ("POST", "/ActiveGestures/", GetActiveGestures, CheckTrustSource));
        }


        ///// <summary>
        ///// Check that the source of an inventory request is one that we trust.
        ///// </summary>
        ///// <param name="peer"></param>
        ///// <returns></returns>
        //public bool CheckTrustSource(IPEndPoint peer)
        //{
        //    if (m_doLookup)
        //    {
        //        m_log.InfoFormat("[GRID AGENT INVENTORY]: Checking trusted source {0}", peer);
        //        UriBuilder ub = new UriBuilder(m_userserver_url);
        //        IPAddress[] uaddrs = Dns.GetHostAddresses(ub.Host);
        //        foreach (IPAddress uaddr in uaddrs)
        //        {
        //            if (uaddr.Equals(peer.Address))
        //            {
        //                return true;
        //            }
        //        }

        //        m_log.WarnFormat(
        //            "[GRID AGENT INVENTORY]: Rejecting request since source {0} was not in the list of trusted sources",
        //            peer);

        //        return false;
        //    }
        //    else
        //    {
        //        return true;
        //    }
        //}

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
                m_log.InfoFormat("[HGStandaloneInvService]: checking authed session {0} {1}", session_id, avatar_id);
                UUID userID = UUID.Zero;
                UUID sessionID = UUID.Zero;
                UUID.TryParse(avatar_id, out userID);
                UUID.TryParse(session_id, out sessionID);
                if (userID.Equals(UUID.Zero) || sessionID.Equals(UUID.Zero))
                {
                    m_log.Info("[HGStandaloneInvService]: Invalid user or session id " + avatar_id + "; " + session_id);
                    return false;
                }
                UserProfileData userProfile = m_userService.GetUserProfile(userID);
                if (userProfile != null && userProfile.CurrentAgent != null &&
                    userProfile.CurrentAgent.SessionID == sessionID)
                {
                    m_log.Info("[HGStandaloneInvService]: user is logged in and session is valid. Authorizing access.");
                    return true;
                }

                m_log.Warn("[HGStandaloneInvService]: unknown user or session_id, request rejected");
                return false;
            }
            else
            {
                return true;
            }
        }


        /// <summary>
        /// Return a user's entire inventory
        /// </summary>
        /// <param name="rawUserID"></param>
        /// <returns>The user's inventory.  If an inventory cannot be found then an empty collection is returned.</returns>
        public InventoryCollection GetUserInventory(Guid rawUserID)
        {
            UUID userID = new UUID(rawUserID);

            m_log.Info("[HGStandaloneInvService]: Processing request for inventory of " + userID);

            // Uncomment me to simulate a slow responding inventory server
            //Thread.Sleep(16000);

            InventoryCollection invCollection = new InventoryCollection();

            List<InventoryFolderBase> allFolders = ((InventoryServiceBase)m_inventoryService).GetInventorySkeleton(userID);

            if (null == allFolders)
            {
                m_log.WarnFormat("[HGStandaloneInvService]: No inventory found for user {0}", rawUserID);

                return invCollection;
            }

            List<InventoryItemBase> allItems = new List<InventoryItemBase>();

            foreach (InventoryFolderBase folder in allFolders)
            {
                List<InventoryItemBase> items = ((InventoryServiceBase)m_inventoryService).RequestFolderItems(folder.ID);

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
                "[HGStandaloneInvService]: Sending back inventory response to user {0} containing {1} folders and {2} items",
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
            UUID userID = new UUID(rawUserID);
            return ((InventoryServiceBase)m_inventoryService).GetInventorySkeleton(userID);
        }

        public List<InventoryItemBase> GetActiveGestures(Guid rawUserID)
        {
            UUID userID = new UUID(rawUserID);

            m_log.InfoFormat("[HGStandaloneInvService]: fetching active gestures for user {0}", userID);

            return ((InventoryServiceBase)m_inventoryService).GetActiveGestures(userID);
        }
    }
}
