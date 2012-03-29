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
using System.Linq;
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

        private ExpiringCache<UUID, List<XInventoryFolder>> m_SuitcaseTrees = new ExpiringCache<UUID,List<XInventoryFolder>>();

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

        public override InventoryCollection GetFolderContent(UUID principalID, UUID folderID)
        {
            InventoryCollection coll = null;
            XInventoryFolder suitcase = GetSuitcaseXFolder(principalID);
            XInventoryFolder root = GetRootXFolder(principalID);

            if (!IsWithinSuitcaseTree(folderID, root, suitcase))
                return new InventoryCollection();

            if (folderID == root.folderID) // someone's asking for the root folder, we'll give them the suitcase
            {
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

        public override List<InventoryItemBase> GetFolderItems(UUID principalID, UUID folderID)
        {
            // Let's do a bit of sanity checking, more than the base service does
            // make sure the given folder exists under the suitcase tree of this user
            XInventoryFolder root = GetRootXFolder(principalID);
            XInventoryFolder suitcase = GetSuitcaseXFolder(principalID);

            if (!IsWithinSuitcaseTree(folderID, root, suitcase))
                return new List<InventoryItemBase>();

            return base.GetFolderItems(principalID, folderID);
        }

        public override bool AddFolder(InventoryFolderBase folder)
        {
            // Let's do a bit of sanity checking, more than the base service does
            // make sure the given folder's parent folder exists under the suitcase tree of this user
            XInventoryFolder root = GetRootXFolder(folder.Owner);
            XInventoryFolder suitcase = GetSuitcaseXFolder(folder.Owner);

            if (!IsWithinSuitcaseTree(folder.ParentID, root, suitcase))
                return false;

            // OK, it's legit
            // Check if it's under the Root folder directly
            if (folder.ParentID == root.folderID) 
            {
                // someone's trying to add a subfolder of the root folder, we'll add it to the suitcase instead
                m_log.DebugFormat("[HG SUITCASE INVENTORY SERVICE]: AddFolder for root folder for user {0}. Adding in suitcase instead", folder.Owner);
                folder.ParentID = suitcase.folderID;
            }

            return base.AddFolder(folder);
       }

        public bool UpdateFolder(InventoryFolderBase folder)
        {
            XInventoryFolder root = GetRootXFolder(folder.Owner);
            XInventoryFolder suitcase = GetSuitcaseXFolder(folder.Owner);

            if (!IsWithinSuitcaseTree(folder.ID, root, suitcase))
                return false;

            return base.UpdateFolder(folder);
        }

        public override bool MoveFolder(InventoryFolderBase folder)
        {
            XInventoryFolder root = GetRootXFolder(folder.Owner);
            XInventoryFolder suitcase = GetSuitcaseXFolder(folder.Owner);

            if (!IsWithinSuitcaseTree(folder.ID, root, suitcase) || !IsWithinSuitcaseTree(folder.ParentID, root, suitcase))
                return false;

            if (folder.ParentID == root.folderID)
            {
                // someone's trying to add a subfolder of the root folder, we'll add it to the suitcase instead
                m_log.DebugFormat("[HG SUITCASE INVENTORY SERVICE]: MoveFolder to root folder for user {0}. Moving it to suitcase instead", folder.Owner);
                folder.ParentID = suitcase.folderID;
            }

            return base.MoveFolder(folder);
        }

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

        public override bool AddItem(InventoryItemBase item)
        {
            // Let's do a bit of sanity checking, more than the base service does
            // make sure the given folder's parent folder exists under the suitcase tree of this user
            XInventoryFolder root = GetRootXFolder(item.Owner);
            XInventoryFolder suitcase = GetSuitcaseXFolder(item.Owner);

            if (!IsWithinSuitcaseTree(item.Folder, root, suitcase))
                return false;

            // OK, it's legit
            // Check if it's under the Root folder directly
            if (item.Folder == root.folderID)
            {
                // someone's trying to add a subfolder of the root folder, we'll add it to the suitcase instead
                m_log.DebugFormat("[HG SUITCASE INVENTORY SERVICE]: AddItem for root folder for user {0}. Adding in suitcase instead", item.Owner);
                item.Folder = suitcase.folderID;
            }

            return base.AddItem(item);

        }

        public override bool UpdateItem(InventoryItemBase item)
        {
            XInventoryFolder root = GetRootXFolder(item.Owner);
            XInventoryFolder suitcase = GetSuitcaseXFolder(item.Owner);

            if (!IsWithinSuitcaseTree(item.Folder, root, suitcase))
                return false;

            return base.UpdateItem(item);
        }

        public override bool MoveItems(UUID principalID, List<InventoryItemBase> items)
        {
            // Principal is b0rked. *sigh*

            XInventoryFolder root = GetRootXFolder(items[0].Owner);
            XInventoryFolder suitcase = GetSuitcaseXFolder(items[0].Owner);

            if (!IsWithinSuitcaseTree(items[0].Folder, root, suitcase))
                return false;

            foreach (InventoryItemBase it in items)
                if (it.Folder == root.folderID)
                {
                    // someone's trying to add a subfolder of the root folder, we'll add it to the suitcase instead
                    m_log.DebugFormat("[HG SUITCASE INVENTORY SERVICE]: MoveItem to root folder for user {0}. Moving it to suitcase instead", it.Owner);
                    it.Folder = suitcase.folderID;
                }

            return base.MoveItems(principalID, items);

        }

        // Let these pass. Use inherited methods.
        public override bool DeleteItems(UUID principalID, List<UUID> itemIDs)
        {
            return false;
        }

        public override InventoryItemBase GetItem(InventoryItemBase item)
        {
            InventoryItemBase it = base.GetItem(item);
            XInventoryFolder root = GetRootXFolder(it.Owner);
            XInventoryFolder suitcase = GetSuitcaseXFolder(it.Owner);

            if (it != null)
            {
                if (!IsWithinSuitcaseTree(it.Folder, root, suitcase))
                    return null;

                if (it.Folder == suitcase.folderID)
                    it.Folder = root.folderID;

                //    UserAccount user = m_Cache.GetUser(it.CreatorId);

                //    // Adjust the creator data
                //    if (user != null && it != null && (it.CreatorData == null || it.CreatorData == string.Empty))
                //        it.CreatorData = m_HomeURL + ";" + user.FirstName + " " + user.LastName;
                //}
            }

            return it;
        }

        public override InventoryFolderBase GetFolder(InventoryFolderBase folder)
        {
            InventoryFolderBase f = base.GetFolder(folder);
            XInventoryFolder root = GetRootXFolder(f.Owner);
            XInventoryFolder suitcase = GetSuitcaseXFolder(f.Owner);

            if (f != null)
            {
                if (!IsWithinSuitcaseTree(f.ID, root, suitcase))
                    return null;

                if (f.ParentID == suitcase.folderID)
                    f.ParentID = root.folderID;
            }

            return f;
        }

        //public List<InventoryItemBase> GetActiveGestures(UUID principalID)
        //{
        //}

        //public int GetAssetPermissions(UUID principalID, UUID assetID)
        //{
        //}

        #region Auxiliary functions
        private XInventoryFolder GetXFolder(UUID userID, UUID folderID)
        {
            XInventoryFolder[] folders = m_Database.GetFolders(
                    new string[] { "agentID", "folderID" },
                    new string[] { userID.ToString(), folderID.ToString() });

            if (folders.Length == 0)
                return null;

            return folders[0];
        }

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

        private List<XInventoryFolder> GetFolderTree(UUID root)
        {
            List<XInventoryFolder> t = null;
            if (m_SuitcaseTrees.TryGetValue(root, out t))
                return t;

            t = GetFolderTreeRecursive(root);
            m_SuitcaseTrees.AddOrUpdate(root, t, 120);
            return t;
        }

        private List<XInventoryFolder> GetFolderTreeRecursive(UUID root)
        {
            List<XInventoryFolder> tree = new List<XInventoryFolder>();
            XInventoryFolder[] folders = m_Database.GetFolders(
                    new string[] { "parentFolderID" },
                    new string[] { root.ToString() });

            if (folders == null || (folders != null && folders.Length == 0))
                return tree; // empty tree
            else
            {
                foreach (XInventoryFolder f in folders)
                {
                    tree.Add(f);
                    tree.AddRange(GetFolderTreeRecursive(f.folderID));
                }
                return tree;
            }

        }

        private bool IsWithinSuitcaseTree(UUID folderID, XInventoryFolder root, XInventoryFolder suitcase)
        {
            List<XInventoryFolder> tree = new List<XInventoryFolder>();
            tree.Add(root); // Warp! the tree is the real root folder plus the children of the suitcase folder
            tree.AddRange(GetFolderTree(suitcase.folderID));
            XInventoryFolder f = tree.Find(delegate(XInventoryFolder fl)
            {
                if (fl.folderID == folderID) return true;
                else return false;
            });

            if (f == null) return false;
            else return true;
        }
        #endregion
    }
}
