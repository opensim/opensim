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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

using log4net;
using Nini.Config;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Scenes.Hypergrid
{
    public partial class HGScene : Scene
    {
        #region Fields
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private HGAssetMapper m_assMapper;

        #endregion

        #region Constructors

        public HGScene(RegionInfo regInfo, AgentCircuitManager authen,
                       CommunicationsManager commsMan, SceneCommunicationService sceneGridService,
                       AssetCache assetCach, StorageManager storeManager, BaseHttpServer httpServer,
                       ModuleLoader moduleLoader, bool dumpAssetsToFile, bool physicalPrim,
                       bool SeeIntoRegionFromNeighbor, IConfigSource config, string simulatorVersion)
            : base(regInfo, authen, commsMan, sceneGridService, assetCach, storeManager, httpServer, moduleLoader,
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
        public override UUID DeleteToInventory(int destination, UUID folderID, SceneObjectGroup objectGroup, IClientAPI remoteClient)
        {
            UUID assetID = base.DeleteToInventory(destination, folderID, objectGroup, remoteClient);

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
            CachedUserInfo userInfo = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);
            if (userInfo != null)
            {
                if (userInfo.RootFolder != null)
                {
                    InventoryItemBase item = userInfo.RootFolder.FindItem(itemID);

                    if (item != null)
                    {
                        m_assMapper.Get(item.AssetID, remoteClient.AgentId);
                        
                    }
                }
            }

            // OK, we're done fetching. Pass it up to the default RezObject
            return base.RezObject(remoteClient, itemID, RayEnd, RayStart, RayTargetID, BypassRayCast, RayEndIsIntersection, 
                                  RezSelected, RemoveItem, fromTaskID, attachment);

        }


        #endregion

    }

}
