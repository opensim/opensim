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
using System.Reflection;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Clients;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;

//using HyperGrid.Framework;
//using OpenSim.Region.Communications.Hypergrid;

namespace OpenSim.Region.Framework.Scenes.Hypergrid
{
    public class HGAssetMapper
    {
        #region Fields
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // This maps between inventory server urls and inventory server clients
//        private Dictionary<string, InventoryClient> m_inventoryServers = new Dictionary<string, InventoryClient>();

        private Scene m_scene;

        private IHyperAssetService m_hyper;
        IHyperAssetService HyperlinkAssets
        {
            get
            {
                if (m_hyper == null)
                    m_hyper = m_scene.RequestModuleInterface<IHyperAssetService>();
                return m_hyper;
            }
        }

        #endregion

        #region Constructor

        public HGAssetMapper(Scene scene)
        {
            m_scene = scene;
        }

        #endregion

        #region Internal functions

        private string UserAssetURL(UUID userID)
        {
            CachedUserInfo uinfo = m_scene.CommsManager.UserProfileCacheService.GetUserDetails(userID);
            if (uinfo != null)
                return (uinfo.UserProfile.UserAssetURI == "") ? null : uinfo.UserProfile.UserAssetURI;
            return null;
        }

//        private string UserInventoryURL(UUID userID)
//        {
//            CachedUserInfo uinfo = m_scene.CommsManager.UserProfileCacheService.GetUserDetails(userID);
//            if (uinfo != null)
//                return (uinfo.UserProfile.UserInventoryURI == "") ? null : uinfo.UserProfile.UserInventoryURI;
//            return null;
//        }


        public AssetBase FetchAsset(string url, UUID assetID)
        {
            AssetBase asset = m_scene.AssetService.Get(url + "/" + assetID.ToString());

            if (asset != null)
            {
                m_log.DebugFormat("[HGScene]: Copied asset {0} from {1} to local asset server. ", asset.ID, url);
                return asset;
            }
            return null;
        }

        public bool PostAsset(string url, AssetBase asset)
        {
            if (asset != null)
            {
                // See long comment in AssetCache.AddAsset
                if (!asset.Temporary || asset.Local)
                {
                    // We need to copy the asset into a new asset, because
                    // we need to set its ID to be URL+UUID, so that the
                    // HGAssetService dispatches it to the remote grid.
                    // It's not pretty, but the best that can be done while
                    // not having a global naming infrastructure
                    AssetBase asset1 = new AssetBase();
                    Copy(asset, asset1);
                    try
                    {
                        asset1.ID = url + "/" + asset.ID;
                    }
                    catch
                    {
                        m_log.Warn("[HGScene]: Oops.");
                    }

                    m_scene.AssetService.Store(asset1);
                    m_log.DebugFormat("[HGScene]: Posted copy of asset {0} from local asset server to {1}", asset1.ID, url);
                }
                return true;
           }
            else
                m_log.Warn("[HGScene]: Tried to post asset to remote server, but asset not in local cache.");

            return false;
        }

        private void Copy(AssetBase from, AssetBase to)
        {
            to.Data        = from.Data;
            to.Description = from.Description;
            to.FullID      = from.FullID;
            to.ID          = from.ID;
            to.Local       = from.Local;
            to.Name        = from.Name;
            to.Temporary   = from.Temporary;
            to.Type        = from.Type;

        }

        // TODO: unused
        // private void Dump(Dictionary<UUID, bool> lst)
        // {
        //     m_log.Debug("XXX -------- UUID DUMP ------- XXX");
        //     foreach (KeyValuePair<UUID, bool> kvp in lst)
        //         m_log.Debug(" >> " + kvp.Key + " (texture? " + kvp.Value + ")");
        //     m_log.Debug("XXX -------- UUID DUMP ------- XXX");
        // }

        #endregion


        #region Public interface

        public void Get(UUID assetID, UUID ownerID)
        {
            // Get the item from the remote asset server onto the local AssetCache
            // and place an entry in m_assetMap

            string userAssetURL = HyperlinkAssets.GetUserAssetServer(ownerID);
            if ((userAssetURL != string.Empty) && (userAssetURL != HyperlinkAssets.GetSimAssetServer()))
            {
                m_log.Debug("[HGScene]: Fetching object " + assetID + " from asset server " + userAssetURL);
                AssetBase asset = FetchAsset(userAssetURL, assetID); 

                if (asset != null)
                {
                    // OK, now fetch the inside.
                    Dictionary<UUID, int> ids = new Dictionary<UUID, int>();
                    HGUuidGatherer uuidGatherer = new HGUuidGatherer(this, m_scene.AssetService, userAssetURL);
                    uuidGatherer.GatherAssetUuids(asset.FullID, (AssetType)asset.Type, ids);
                    foreach (UUID uuid in ids.Keys)
                        FetchAsset(userAssetURL, uuid);

                    m_log.DebugFormat("[HGScene]: Successfully fetched asset {0} from asset server {1}", asset.ID, userAssetURL);

                }
                else
                    m_log.Warn("[HGScene]: Could not fetch asset from remote asset server " + userAssetURL);
            }
            else
                m_log.Debug("[HGScene]: user's asset server is the local region's asset server");
        }

        //public InventoryItemBase Get(InventoryItemBase item, UUID rootFolder, CachedUserInfo userInfo)
        //{
        //    InventoryClient invCli = null;
        //    string inventoryURL = UserInventoryURL(item.Owner);
        //    if (!m_inventoryServers.TryGetValue(inventoryURL, out invCli))
        //    {
        //        m_log.Debug("[HGScene]: Starting new InventorytClient for " + inventoryURL);
        //        invCli = new InventoryClient(inventoryURL);
        //        m_inventoryServers.Add(inventoryURL, invCli);
        //    }

        //    item = invCli.GetInventoryItem(item);
        //    if (item != null)
        //    {
        //        // Change the folder, stick it in root folder, all items flattened out here in this region cache
        //        item.Folder = rootFolder;
        //        //userInfo.AddItem(item); don't use this, it calls back to the inventory server
        //        lock (userInfo.RootFolder.Items)
        //        {
        //            userInfo.RootFolder.Items[item.ID] = item;
        //        }

        //    }
        //    return item;
        //}

        public void Post(UUID assetID, UUID ownerID)
        {
                // Post the item from the local AssetCache onto the remote asset server
                // and place an entry in m_assetMap

            string userAssetURL = HyperlinkAssets.GetUserAssetServer(ownerID);
            if ((userAssetURL != string.Empty) && (userAssetURL != HyperlinkAssets.GetSimAssetServer()))
            {
                m_log.Debug("[HGScene]: Posting object " + assetID + " to asset server " + userAssetURL);
                AssetBase asset = m_scene.AssetService.Get(assetID.ToString());
                if (asset != null)
                {
                    Dictionary<UUID, int> ids = new Dictionary<UUID, int>();
                    HGUuidGatherer uuidGatherer = new HGUuidGatherer(this, m_scene.AssetService, string.Empty);
                    uuidGatherer.GatherAssetUuids(asset.FullID, (AssetType)asset.Type, ids);
                    foreach (UUID uuid in ids.Keys)
                    {
                        asset = m_scene.AssetService.Get(uuid.ToString());
                        if (asset == null)
                            m_log.DebugFormat("[HGScene]: Could not find asset {0}", uuid);
                        else
                            PostAsset(userAssetURL, asset);
                    }

                     // maybe all pieces got there...
                    m_log.DebugFormat("[HGScene]: Successfully posted item {0} to asset server {1}", assetID, userAssetURL);

                }
                else
                    m_log.DebugFormat("[HGScene]: Something wrong with asset {0}, it could not be found", assetID);
            }
            else
                m_log.Debug("[HGScene]: user's asset server is local region's asset server");

        }

        #endregion

    }
}
