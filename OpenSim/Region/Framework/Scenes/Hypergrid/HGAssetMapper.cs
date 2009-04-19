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
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Clients;

//using HyperGrid.Framework;
//using OpenSim.Region.Communications.Hypergrid;

namespace OpenSim.Region.Framework.Scenes.Hypergrid
{
    public class HGAssetMapper
    {
        #region Fields
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // This maps between asset server URLs and asset server clients
        private Dictionary<string, GridAssetClient> m_assetServers = new Dictionary<string, GridAssetClient>();

        // This maps between asset UUIDs and asset servers
        private Dictionary<UUID, GridAssetClient> m_assetMap = new Dictionary<UUID, GridAssetClient>();

        // This maps between inventory server urls and inventory server clients
        private Dictionary<string, InventoryClient> m_inventoryServers = new Dictionary<string, InventoryClient>();


        private Scene m_scene;
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

        private string UserInventoryURL(UUID userID)
        {
            CachedUserInfo uinfo = m_scene.CommsManager.UserProfileCacheService.GetUserDetails(userID);
            if (uinfo != null)
                return (uinfo.UserProfile.UserInventoryURI == "") ? null : uinfo.UserProfile.UserInventoryURI;
            return null;
        }

        private bool IsLocalUser(UUID userID)
        {
            CachedUserInfo uinfo = m_scene.CommsManager.UserProfileCacheService.GetUserDetails(userID);

            if (uinfo != null)
            {
                if (HGNetworkServersInfo.Singleton.IsLocalUser(uinfo.UserProfile))
                {
                    m_log.Debug("[HGScene]: Home user " + uinfo.UserProfile.FirstName + " " + uinfo.UserProfile.SurName);
                    return true;
                }
            }

            m_log.Debug("[HGScene]: Foreign user " + uinfo.UserProfile.FirstName + " " + uinfo.UserProfile.SurName);
            return false;
        }

        private bool IsInAssetMap(UUID uuid)
        {
            return m_assetMap.ContainsKey(uuid);
        }

        private bool FetchAsset(GridAssetClient asscli, UUID assetID, bool isTexture)
        {
            // I'm not going over 3 seconds since this will be blocking processing of all the other inbound
            // packets from the client.
            int pollPeriod = 200;
            int maxPolls = 15;

            AssetBase asset;

            // Maybe it came late, and it's already here. Check first.
            if (m_scene.CommsManager.AssetCache.TryGetCachedAsset(assetID, out asset))
            {
                m_log.Debug("[HGScene]: Asset already in asset cache. " + assetID);
                return true;
            }


            asscli.RequestAsset(assetID, isTexture);

            do
            {
                Thread.Sleep(pollPeriod);

                if (m_scene.CommsManager.AssetCache.TryGetCachedAsset(assetID, out asset) && (asset != null))
                {
                    m_log.Debug("[HGScene]: Asset made it to asset cache. " + asset.Name + " " + assetID);
                    // I think I need to store it in the asset DB too.
                    // For now, let me just do it for textures and scripts
                    if (((AssetType)asset.Type == AssetType.Texture) ||
                        ((AssetType)asset.Type == AssetType.LSLBytecode) ||
                        ((AssetType)asset.Type == AssetType.LSLText))
                    {
                        AssetBase asset1 = new AssetBase();
                        Copy(asset, asset1);
                        m_scene.CommsManager.AssetCache.AssetServer.StoreAsset(asset1);
                    }
                    return true;
                }
            } while (--maxPolls > 0);

            m_log.WarnFormat("[HGScene]: {0} {1} was not received before the retrieval timeout was reached",
                             isTexture ? "texture" : "asset", assetID.ToString());

            return false;
        }

        private bool PostAsset(GridAssetClient asscli, UUID assetID, bool isTexture)
        {
            AssetBase asset1;
            //m_scene.CommsManager.AssetCache.TryGetCachedAsset(assetID, out asset1);
            asset1 = m_scene.CommsManager.AssetCache.GetAsset(assetID, isTexture);

            if (asset1 != null)
            {
                // See long comment in AssetCache.AddAsset
                if (!asset1.Temporary || asset1.Local)
                {
                    // The asset cache returns instances of subclasses of AssetBase:
                    // TextureImage or AssetInfo. So in passing them to the remote
                    // server we first need to convert this to instances of AssetBase,
                    // which is the serializable class for assets.
                    AssetBase asset = new AssetBase();
                    Copy(asset1, asset);

                    asscli.StoreAsset(asset);
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

        private void _guardedAdd(Dictionary<UUID, bool> lst, UUID obj, bool val)
        {
            if (!lst.ContainsKey(obj))
                lst.Add(obj, val);
        }

        private void SniffTextureUUIDs(Dictionary<UUID, bool> uuids, SceneObjectGroup sog)
        {
            try
            {
                _guardedAdd(uuids, sog.RootPart.Shape.Textures.DefaultTexture.TextureID, true);
            }
            catch (Exception) { }

            foreach (Primitive.TextureEntryFace tface in sog.RootPart.Shape.Textures.FaceTextures)
            {
                try
                {
                    _guardedAdd(uuids, tface.TextureID, true);
                }
                catch (Exception) { }
            }

            foreach (SceneObjectPart sop in sog.Children.Values)
            {
                try
                {
                    _guardedAdd(uuids, sop.Shape.Textures.DefaultTexture.TextureID, true);
                }
                catch (Exception) { }
                foreach (Primitive.TextureEntryFace tface in sop.Shape.Textures.FaceTextures)
                {
                    try
                    {
                        _guardedAdd(uuids, tface.TextureID, true);
                    }
                    catch (Exception) { }
                }
            }
        }

        private void SniffTaskInventoryUUIDs(Dictionary<UUID, bool> uuids, SceneObjectGroup sog)
        {
            TaskInventoryDictionary tinv = sog.RootPart.TaskInventory;

            lock (tinv)
            {
                foreach (TaskInventoryItem titem in tinv.Values)
                {
                    uuids.Add(titem.AssetID, (InventoryType)titem.Type == InventoryType.Texture);
                }
            }
        }

        private Dictionary<UUID, bool> SniffUUIDs(AssetBase asset)
        {
            Dictionary<UUID, bool> uuids = new Dictionary<UUID, bool>();
            if ((asset != null) && ((AssetType)asset.Type == AssetType.Object))
            {
                string ass_str = Utils.BytesToString(asset.Data);
                SceneObjectGroup sog = new SceneObjectGroup(ass_str, true);

                SniffTextureUUIDs(uuids, sog);

                // We need to sniff further...
                SniffTaskInventoryUUIDs(uuids, sog);

            }

            return uuids;
        }

        private Dictionary<UUID, bool> SniffUUIDs(UUID assetID)
        {
            //Dictionary<UUID, bool> uuids = new Dictionary<UUID, bool>();

            AssetBase asset;
            m_scene.CommsManager.AssetCache.TryGetCachedAsset(assetID, out asset);

            return SniffUUIDs(asset);
        }

        private void Dump(Dictionary<UUID, bool> lst)
        {
            m_log.Debug("XXX -------- UUID DUMP ------- XXX");
            foreach (KeyValuePair<UUID, bool> kvp in lst)
                m_log.Debug(" >> " + kvp.Key + " (texture? " + kvp.Value + ")");
            m_log.Debug("XXX -------- UUID DUMP ------- XXX");
        }

        #endregion


        #region Public interface

        public void Get(UUID assetID, UUID ownerID)
        {
            if (!IsInAssetMap(assetID) && !IsLocalUser(ownerID))
            {
                // Get the item from the remote asset server onto the local AssetCache
                // and place an entry in m_assetMap

                GridAssetClient asscli = null;
                string userAssetURL = UserAssetURL(ownerID);
                if (userAssetURL != null)
                {
                    m_assetServers.TryGetValue(userAssetURL, out asscli);
                    if (asscli == null)
                    {
                        m_log.Debug("[HGScene]: Starting new GridAssetClient for " + userAssetURL);
                        asscli = new GridAssetClient(userAssetURL);
                        asscli.SetReceiver(m_scene.CommsManager.AssetCache); // Straight to the asset cache!
                        m_assetServers.Add(userAssetURL, asscli);
                        asscli.Start();
                    }

                    m_log.Debug("[HGScene]: Fetching object " + assetID + " to asset server " + userAssetURL);
                    bool success = FetchAsset(asscli, assetID, false); // asscli.RequestAsset(item.ItemID, false);

                    // OK, now fetch the inside.
                    Dictionary<UUID, bool> ids = SniffUUIDs(assetID);
                    Dump(ids);
                    foreach (KeyValuePair<UUID, bool> kvp in ids)
                        FetchAsset(asscli, kvp.Key, kvp.Value);


                    if (success)
                    {
                        m_log.Debug("[HGScene]: Successfully fetched item from remote asset server " + userAssetURL);
                        m_assetMap.Add(assetID, asscli);
                    }
                    else
                        m_log.Warn("[HGScene]: Could not fetch asset from remote asset server " + userAssetURL);
                }
                else
                    m_log.Warn("[HGScene]: Unable to locate foreign user's asset server");
            }
        }

        public InventoryItemBase Get(InventoryItemBase item, UUID rootFolder, CachedUserInfo userInfo)
        {
            InventoryClient invCli = null;
            string inventoryURL = UserInventoryURL(item.Owner);
            if (!m_inventoryServers.TryGetValue(inventoryURL, out invCli))
            {
                m_log.Debug("[HGScene]: Starting new InventorytClient for " + inventoryURL);
                invCli = new InventoryClient(inventoryURL);
                m_inventoryServers.Add(inventoryURL, invCli);
            }

            item = invCli.GetInventoryItem(item);
            if (item != null)
            {
                // Change the folder, stick it in root folder, all items flattened out here in this region cache
                item.Folder = rootFolder;
                //userInfo.AddItem(item); don't use this, it calls back to the inventory server
                lock (userInfo.RootFolder.Items)
                {
                    userInfo.RootFolder.Items[item.ID] = item;
                }

            }
            return item;
        }

        public void Post(UUID assetID, UUID ownerID)
        {
            if (!IsLocalUser(ownerID))
            {
                // Post the item from the local AssetCache ontp the remote asset server
                // and place an entry in m_assetMap

                GridAssetClient asscli = null;
                string userAssetURL = UserAssetURL(ownerID);
                if (userAssetURL != null)
                {
                    m_assetServers.TryGetValue(userAssetURL, out asscli);
                    if (asscli == null)
                    {
                        m_log.Debug("[HGScene]: Starting new GridAssetClient for " + userAssetURL);
                        asscli = new GridAssetClient(userAssetURL);
                        asscli.SetReceiver(m_scene.CommsManager.AssetCache); // Straight to the asset cache!
                        m_assetServers.Add(userAssetURL, asscli);
                    }
                    m_log.Debug("[HGScene]: Posting object " + assetID + " to asset server " + userAssetURL);
                    AssetBase ass1 = null;
                    m_scene.CommsManager.AssetCache.TryGetCachedAsset(assetID, out ass1);
                    if (ass1 != null)
                    {
                        bool success = PostAsset(asscli, assetID, (ass1.Type == (sbyte)AssetType.Texture));

                        // Now the inside
                        Dictionary<UUID, bool> ids = SniffUUIDs(assetID);
                        Dump(ids);
                        foreach (KeyValuePair<UUID, bool> kvp in ids)
                            PostAsset(asscli, kvp.Key, kvp.Value);

                        if (success)
                        {
                            m_log.Debug("[HGScene]: Successfully posted item to remote asset server " + userAssetURL);
                            if (!m_assetMap.ContainsKey(assetID))
                                m_assetMap.Add(assetID, asscli);
                        }
                        else
                            m_log.Warn("[HGScene]: Could not post asset to remote asset server " + userAssetURL);

                    }
                    else
                        m_log.Debug("[HGScene]: Something wrong with asset");
                }
                else
                    m_log.Warn("[HGScene]: Unable to locate foreign user's asset server");

            }
        }

        #endregion

    }
}
