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

using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.Framework.Scenes.Hypergrid
{
    public partial class HGScene : Scene
    {
        #region Fields
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private HGAssetMapper m_assMapper;
        public HGAssetMapper AssetMapper
        {
            get { return m_assMapper; }
        }

        private IHyperAssetService m_hyper;
        private IHyperAssetService HyperAssets
        {
            get
            {
                if (m_hyper == null)
                    m_hyper = RequestModuleInterface<IHyperAssetService>();
                return m_hyper;
            }
        }

        #endregion

        #region Constructors

        public HGScene(RegionInfo regInfo, AgentCircuitManager authen,
                       CommunicationsManager commsMan, SceneCommunicationService sceneGridService,
                       StorageManager storeManager,
                       ModuleLoader moduleLoader, bool dumpAssetsToFile, bool physicalPrim,
                       bool SeeIntoRegionFromNeighbor, IConfigSource config, string simulatorVersion)
            : base(regInfo, authen, commsMan, sceneGridService, storeManager, moduleLoader,
                   dumpAssetsToFile, physicalPrim, SeeIntoRegionFromNeighbor, config, simulatorVersion)
        {
            m_log.Info("[HGScene]: Starting HGScene.");
            m_assMapper = new HGAssetMapper(this);

            EventManager.OnNewInventoryItemUploadComplete += UploadInventoryItem;
        }

        #endregion

        #region Event handlers

        public void UploadInventoryItem(UUID avatarID, UUID assetID, string name, int userlevel)
        {
            CachedUserInfo userInfo = CommsManager.UserProfileCacheService.GetUserDetails(avatarID);
            if (userInfo != null)
            {
                m_assMapper.Post(assetID, avatarID);
            }
        }

        #endregion

        #region Overrides of Scene.Inventory methods

        /// 
        /// CapsUpdateInventoryItemAsset
        ///
        public override UUID CapsUpdateInventoryItemAsset(IClientAPI remoteClient, UUID itemID, byte[] data)
        {
            UUID newAssetID = base.CapsUpdateInventoryItemAsset(remoteClient, itemID, data);

            UploadInventoryItem(remoteClient.AgentId, newAssetID, "", 0);

            return newAssetID;
        }

        ///
        /// DeleteToInventory
        ///
        public override UUID DeleteToInventory(DeRezAction action, UUID folderID, SceneObjectGroup objectGroup, IClientAPI remoteClient)
        {
            UUID assetID = base.DeleteToInventory(action, folderID, objectGroup, remoteClient);

            if (!assetID.Equals(UUID.Zero))
            {
                UploadInventoryItem(remoteClient.AgentId, assetID, "", 0);
            }
            else
                m_log.Debug("[HGScene]: Scene.Inventory did not create asset");

            return assetID;
        }

        ///
        /// RezObject
        ///
        public override SceneObjectGroup RezObject(IClientAPI remoteClient, UUID itemID, Vector3 RayEnd, Vector3 RayStart,
                                                   UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
                                                   bool RezSelected, bool RemoveItem, UUID fromTaskID, bool attachment)
        {
            m_log.DebugFormat("[HGScene] RezObject itemID={0} fromTaskID={1}", itemID, fromTaskID);

            //if (fromTaskID.Equals(UUID.Zero))
            //{
            InventoryItemBase item = new InventoryItemBase(itemID);
            item.Owner = remoteClient.AgentId;
            item = InventoryService.GetItem(item);
            //if (item == null)
            //{ // Fetch the item
            //    item = new InventoryItemBase();
            //    item.Owner = remoteClient.AgentId;
            //    item.ID = itemID;
            //    item = m_assMapper.Get(item, userInfo.RootFolder.ID, userInfo);
            //}
            if (item != null)
            {
                m_assMapper.Get(item.AssetID, remoteClient.AgentId);

            }
            //}

            // OK, we're done fetching. Pass it up to the default RezObject
            return base.RezObject(remoteClient, itemID, RayEnd, RayStart, RayTargetID, BypassRayCast, RayEndIsIntersection, 
                                  RezSelected, RemoveItem, fromTaskID, attachment);

        }

        protected override void TransferInventoryAssets(InventoryItemBase item, UUID sender, UUID receiver)
        {
            string userAssetServer = HyperAssets.GetUserAssetServer(sender);
            if ((userAssetServer != string.Empty) && (userAssetServer != HyperAssets.GetSimAssetServer()))
                m_assMapper.Get(item.AssetID, sender);

            userAssetServer = HyperAssets.GetUserAssetServer(receiver);
            if ((userAssetServer != string.Empty) && (userAssetServer != HyperAssets.GetSimAssetServer()))
                m_assMapper.Post(item.AssetID, receiver);
        }

        #endregion

    }

}
