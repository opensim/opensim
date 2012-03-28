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
using OpenMetaverse;
using log4net;
using Nini.Config;
using System.Reflection;
using OpenSim.Services.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Services.InventoryService;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Server.Base;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// Hypergrid inventory service. It serves the IInventoryService interface,
    /// but implements it in ways that are appropriate for inter-grid
    /// inventory exchanges. Specifically, it does not performs deletions
    /// and it responds to GetRootFolder requests with the ID of the
    /// Suitcase folder, not the actual "My Inventory" folder.
    /// </summary>
    public class HGSuitcaseInventoryService : XInventoryService, IInventoryService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_HomeURL;
        private IUserAccountService m_UserAccountService;

        private UserAccountCache m_Cache;

        public HGSuitcaseInventoryService(IConfigSource config, string configName)
            : base(config, configName)
        {
            m_log.DebugFormat("[HG SUITCASE INVENTORY SERVICE]: Starting with config name {0}", configName);
            if (configName != string.Empty)
                m_ConfigName = configName;

            if (m_Database == null)
                m_log.WarnFormat("[XXX]: m_Database is null!");

            //
            // Try reading the [InventoryService] section, if it exists
            //
            IConfig invConfig = config.Configs[m_ConfigName];
            if (invConfig != null)
            {
                // realm = authConfig.GetString("Realm", realm);
                string userAccountsDll = invConfig.GetString("UserAccountsService", string.Empty);
                if (userAccountsDll == string.Empty)
                    throw new Exception("Please specify UserAccountsService in HGInventoryService configuration");

                Object[] args = new Object[] { config };
                m_UserAccountService = ServerUtils.LoadPlugin<IUserAccountService>(userAccountsDll, args);
                if (m_UserAccountService == null)
                    throw new Exception(String.Format("Unable to create UserAccountService from {0}", userAccountsDll));

                // legacy configuration [obsolete]
                m_HomeURL = invConfig.GetString("ProfileServerURI", string.Empty);
                // Preferred
                m_HomeURL = invConfig.GetString("HomeURI", m_HomeURL);

                m_Cache = UserAccountCache.CreateUserAccountCache(m_UserAccountService);
            }

            m_log.Debug("[HG SUITCASE INVENTORY SERVICE]: Starting...");
        }

        public override bool CreateUserInventory(UUID principalID)
        {
            // NOGO
            return false;
        }


        public override List<InventoryFolderBase> GetInventorySkeleton(UUID principalID)
        {
            // NOGO for this inventory service
            return new List<InventoryFolderBase>();
        }

        public override InventoryFolderBase GetRootFolder(UUID principalID)
        {
            m_log.DebugFormat("[HG SUITCASE INVENTORY SERVICE]: GetRootFolder for {0}", principalID);
            if (m_Database == null)
                m_log.ErrorFormat("[XXX]: m_Database is NULL!");

            // Let's find out the local root folder
            XInventoryFolder root = GetRootXFolder(principalID); ;
            if (root == null)
            {
                m_log.WarnFormat("[HG SUITCASE INVENTORY SERVICE]: Unable to retrieve local root folder for user {0}", principalID);
            }

            // Warp! Root folder for travelers is the suitcase folder
            XInventoryFolder suitcase = GetSuitcaseXFolder(principalID);

            if (suitcase == null)
            {
                m_log.DebugFormat("[HG SUITCASE INVENTORY SERVICE]: User {0} does not have a Suitcase folder. Creating it...", principalID);
                // make one, and let's add it to the user's inventory as a direct child of the root folder
                suitcase = CreateFolder(principalID, root.folderID, 100, "My Suitcase");
                if (suitcase == null)
                    m_log.ErrorFormat("[HG SUITCASE INVENTORY SERVICE]: Unable to create suitcase folder");

                m_Database.StoreFolder(suitcase);
            }

            // Now let's change the folder ID to match that of the real root folder
            SetAsRootFolder(suitcase, root.folderID);

            return ConvertToOpenSim(suitcase);
        }

        public override InventoryFolderBase GetFolderForType(UUID principalID, AssetType type)
        {
            //m_log.DebugFormat("[HG INVENTORY SERVICE]: GetFolderForType for {0} {0}", principalID, type);
            return GetRootFolder(principalID);
        }

        //
        // Use the inherited methods
        //
        public override InventoryCollection GetFolderContent(UUID principalID, UUID folderID)
        {
            InventoryCollection coll = null;
            XInventoryFolder root = GetRootXFolder(principalID);
            if (folderID == root.folderID) // someone's asking for the root folder, we'll give them the suitcase
            {
                XInventoryFolder suitcase = GetSuitcaseXFolder(principalID);
                if (suitcase != null)
                {
                    coll = base.GetFolderContent(principalID, suitcase.folderID);
                    foreach (InventoryFolderBase f in coll.Folders)
                        f.ParentID = root.folderID;
                    foreach (InventoryItemBase i in coll.Items)
                        i.Folder = root.folderID;
                    m_log.DebugFormat("[HG SUITCASE INVENTORY SERVICE]: GetFolderContent for root folder returned content for suitcase folder");
                }
            }
            else
            {
                coll = base.GetFolderContent(principalID, folderID);
                m_log.DebugFormat("[HG SUITCASE INVENTORY SERVICE]: GetFolderContent for non-root folder {0}", folderID);
            }
            if (coll == null)
            {
                m_log.WarnFormat("[HG SUITCASE INVENTORY SERVICE]: Something wrong with user {0}'s suitcase folder", principalID);
                coll = new InventoryCollection();
            }
            return coll;
        }

        //public List<InventoryItemBase> GetFolderItems(UUID principalID, UUID folderID)
        //{
        //}

        //public override bool AddFolder(InventoryFolderBase folder)
        //{
        //    // Check if it's under the Suitcase folder
        //    List<InventoryFolderBase> skel = base.GetInventorySkeleton(folder.Owner);
        //    InventoryFolderBase suitcase = GetRootFolder(folder.Owner);
        //    List<InventoryFolderBase> suitDescendents = GetDescendents(skel, suitcase.ID);

        //    foreach (InventoryFolderBase f in suitDescendents)
        //        if (folder.ParentID == f.ID)
        //        {
        //            XInventoryFolder xFolder = ConvertFromOpenSim(folder);
        //            return m_Database.StoreFolder(xFolder);
        //        }
        //    return false;
        //}

        private List<InventoryFolderBase> GetDescendents(List<InventoryFolderBase> lst, UUID root)
        {
            List<InventoryFolderBase> direct = lst.FindAll(delegate(InventoryFolderBase f) { return f.ParentID == root; });
            if (direct == null)
                return new List<InventoryFolderBase>();

            List<InventoryFolderBase> indirect = new List<InventoryFolderBase>();
            foreach (InventoryFolderBase f in direct)
                indirect.AddRange(GetDescendents(lst, f.ID));

            direct.AddRange(indirect);
            return direct;
        }

        // Use inherited method
        //public bool UpdateFolder(InventoryFolderBase folder)
        //{
        //}

        //public override bool MoveFolder(InventoryFolderBase folder)
        //{
        //    XInventoryFolder[] x = m_Database.GetFolders(
        //            new string[] { "folderID" },
        //            new string[] { folder.ID.ToString() });

        //    if (x.Length == 0)
        //        return false;

        //    // Check if it's under the Suitcase folder
        //    List<InventoryFolderBase> skel = base.GetInventorySkeleton(folder.Owner);
        //    InventoryFolderBase suitcase = GetRootFolder(folder.Owner);
        //    List<InventoryFolderBase> suitDescendents = GetDescendents(skel, suitcase.ID);

        //    foreach (InventoryFolderBase f in suitDescendents)
        //        if (folder.ParentID == f.ID)
        //        {
        //            x[0].parentFolderID = folder.ParentID;
        //            return m_Database.StoreFolder(x[0]);
        //        }

        //    return false;
        //}

        public override bool DeleteFolders(UUID principalID, List<UUID> folderIDs)
        {
            // NOGO
            return false;
        }

        public override bool PurgeFolder(InventoryFolderBase folder)
        {
            // NOGO
            return false;
        }

        // Unfortunately we need to use the inherited method because of how DeRez works.
        // The viewer sends the folderID hard-wired in the derez message
        //public override bool AddItem(InventoryItemBase item)
        //{
        //    // Check if it's under the Suitcase folder
        //    List<InventoryFolderBase> skel = base.GetInventorySkeleton(item.Owner);
        //    InventoryFolderBase suitcase = GetRootFolder(item.Owner);
        //    List<InventoryFolderBase> suitDescendents = GetDescendents(skel, suitcase.ID);

        //    foreach (InventoryFolderBase f in suitDescendents)
        //        if (item.Folder == f.ID)
        //            return m_Database.StoreItem(ConvertFromOpenSim(item));

        //    return false;
        //}

        //public override bool UpdateItem(InventoryItemBase item)
        //{
        //    // Check if it's under the Suitcase folder
        //    List<InventoryFolderBase> skel = base.GetInventorySkeleton(item.Owner);
        //    InventoryFolderBase suitcase = GetRootFolder(item.Owner);
        //    List<InventoryFolderBase> suitDescendents = GetDescendents(skel, suitcase.ID);

        //    foreach (InventoryFolderBase f in suitDescendents)
        //        if (item.Folder == f.ID)
        //            return m_Database.StoreItem(ConvertFromOpenSim(item));

        //    return false;
        //}

        //public override bool MoveItems(UUID principalID, List<InventoryItemBase> items)
        //{
        //    // Principal is b0rked. *sigh*
        //    //
        //    // Let's assume they all have the same principal
        //    // Check if it's under the Suitcase folder
        //    List<InventoryFolderBase> skel = base.GetInventorySkeleton(items[0].Owner);
        //    InventoryFolderBase suitcase = GetRootFolder(items[0].Owner);
        //    List<InventoryFolderBase> suitDescendents = GetDescendents(skel, suitcase.ID);

        //    foreach (InventoryItemBase i in items)
        //    {
        //        foreach (InventoryFolderBase f in suitDescendents)
        //            if (i.Folder == f.ID)
        //                m_Database.MoveItem(i.ID.ToString(), i.Folder.ToString());
        //    }

        //    return true;
        //}

        // Let these pass. Use inherited methods.
        //public bool DeleteItems(UUID principalID, List<UUID> itemIDs)
        //{
        //}

        //public override InventoryItemBase GetItem(InventoryItemBase item)
        //{
        //    InventoryItemBase it = base.GetItem(item);
        //    if (it != null)
        //    {
        //        UserAccount user = m_Cache.GetUser(it.CreatorId);

        //        // Adjust the creator data
        //        if (user != null && it != null && (it.CreatorData == null || it.CreatorData == string.Empty))
        //            it.CreatorData = m_HomeURL + ";" + user.FirstName + " " + user.LastName;
        //    }
        //    return it;
        //}

        //public InventoryFolderBase GetFolder(InventoryFolderBase folder)
        //{
        //}

        //public List<InventoryItemBase> GetActiveGestures(UUID principalID)
        //{
        //}

        //public int GetAssetPermissions(UUID principalID, UUID assetID)
        //{
        //}

        private XInventoryFolder GetRootXFolder(UUID principalID)
        {
            XInventoryFolder[] folders = m_Database.GetFolders(
                new string[] { "agentID", "folderName", "type" },
                new string[] { principalID.ToString(), "My Inventory", ((int)AssetType.RootFolder).ToString() });

            if (folders != null && folders.Length > 0)
                return folders[0];
            return null;
        }

        private XInventoryFolder GetSuitcaseXFolder(UUID principalID)
        {
            // Warp! Root folder for travelers
            XInventoryFolder[] folders = m_Database.GetFolders(
                    new string[] { "agentID", "type" },
                    new string[] { principalID.ToString(), "100" }); // This is a special folder type...

            if (folders != null && folders.Length > 0)
                return folders[0];
            return null;
        }

        private void SetAsRootFolder(XInventoryFolder suitcase, UUID rootID)
        {
            suitcase.folderID = rootID;
            suitcase.parentFolderID = UUID.Zero;
        }
    }
}
