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
using Nini.Config;
using System;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Framework;

using OpenSim.Server.Base;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Asset
{
    public class HGAssetBroker :
            ISharedRegionModule, IAssetService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private IImprovedAssetCache m_Cache = null;
        private IAssetService m_GridService;
        private IAssetService m_HGService;

        private Scene m_aScene;
        private string m_LocalAssetServiceURI;

        private bool m_Enabled = false;

        private AssetPermissions m_AssetPerms;

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "HGAssetBroker"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("AssetServices", "");
                if (name == Name)
                {
                    IConfig assetConfig = source.Configs["AssetService"];
                    if (assetConfig == null)
                    {
                        m_log.Error("[HG ASSET CONNECTOR]: AssetService missing from OpenSim.ini");
                        return;
                    }

                    string localDll = assetConfig.GetString("LocalGridAssetService",
                            String.Empty);
                    string HGDll = assetConfig.GetString("HypergridAssetService",
                            String.Empty);

                    if (localDll == String.Empty)
                    {
                        m_log.Error("[HG ASSET CONNECTOR]: No LocalGridAssetService named in section AssetService");
                        return;
                    }

                    if (HGDll == String.Empty)
                    {
                        m_log.Error("[HG ASSET CONNECTOR]: No HypergridAssetService named in section AssetService");
                        return;
                    }

                    Object[] args = new Object[] { source };
                    m_GridService =
                            ServerUtils.LoadPlugin<IAssetService>(localDll,
                            args);

                    m_HGService =
                            ServerUtils.LoadPlugin<IAssetService>(HGDll,
                            args);

                    if (m_GridService == null)
                    {
                        m_log.Error("[HG ASSET CONNECTOR]: Can't load local asset service");
                        return;
                    }
                    if (m_HGService == null)
                    {
                        m_log.Error("[HG ASSET CONNECTOR]: Can't load hypergrid asset service");
                        return;
                    }

                    m_LocalAssetServiceURI = assetConfig.GetString("AssetServerURI", string.Empty);
                    if (m_LocalAssetServiceURI == string.Empty)
                    {
                        IConfig netConfig = source.Configs["Network"];
                        m_LocalAssetServiceURI = netConfig.GetString("asset_server_url", string.Empty);
                    }

                    if (m_LocalAssetServiceURI != string.Empty)
                        m_LocalAssetServiceURI = m_LocalAssetServiceURI.Trim('/');

                    IConfig hgConfig = source.Configs["HGAssetService"];
                    m_AssetPerms = new AssetPermissions(hgConfig); // it's ok if arg is null

                    m_Enabled = true;
                    m_log.Info("[HG ASSET CONNECTOR]: HG asset broker enabled");
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;
            
            m_aScene = scene;

            m_aScene.RegisterModuleInterface<IAssetService>(this);
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            if (m_Cache == null)
            {
                m_Cache = scene.RequestModuleInterface<IImprovedAssetCache>();

                if (!(m_Cache is ISharedRegionModule))
                    m_Cache = null;
            }

            m_log.InfoFormat("[HG ASSET CONNECTOR]: Enabled hypergrid asset broker for region {0}", scene.RegionInfo.RegionName);

            if (m_Cache != null)
            {
                m_log.InfoFormat("[HG ASSET CONNECTOR]: Enabled asset caching for region {0}", scene.RegionInfo.RegionName);
            }
        }

        private bool IsHG(string id)
        {
            Uri assetUri;

            if (Uri.TryCreate(id, UriKind.Absolute, out assetUri) &&
                    assetUri.Scheme == Uri.UriSchemeHttp)
                return true;

            return false;
        }

        public AssetBase Get(string id)
        {
            //m_log.DebugFormat("[HG ASSET CONNECTOR]: Get {0}", id);
            AssetBase asset = null;
            
            if (m_Cache != null)
            {
                asset = m_Cache.Get(id);

                if (asset != null)
                    return asset;
            }

            if (IsHG(id))
            {
                asset = m_HGService.Get(id);
                if (asset != null)
                {
                    // Now store it locally, if allowed
                    if (m_AssetPerms.AllowedImport(asset.Type))
                        m_GridService.Store(asset);
                    else
                        return null;
                }
            }
            else
                asset = m_GridService.Get(id);

            if (m_Cache != null)
                m_Cache.Cache(asset);

            return asset;
        }

        public AssetBase GetCached(string id)
        {
            if (m_Cache != null)
                return m_Cache.Get(id);

            return null;
        }

        public AssetMetadata GetMetadata(string id)
        {
            AssetBase asset = null;
            
            if (m_Cache != null)
            {
                if (m_Cache != null)
                    m_Cache.Get(id);

                if (asset != null)
                    return asset.Metadata;
            }

            AssetMetadata metadata;

            if (IsHG(id))
                metadata = m_HGService.GetMetadata(id);
            else
                metadata = m_GridService.GetMetadata(id);

            return metadata;
        }

        public byte[] GetData(string id)
        {
            AssetBase asset = null;
            
            if (m_Cache != null)
            {
                if (m_Cache != null)
                    m_Cache.Get(id);

                if (asset != null)
                    return asset.Data;
            }

            if (IsHG(id))
                return m_HGService.GetData(id);
            else
                return m_GridService.GetData(id);

        }

        public bool Get(string id, Object sender, AssetRetrieved handler)
        {
            AssetBase asset = null;
            
            if (m_Cache != null)
                asset = m_Cache.Get(id);

            if (asset != null)
            {
                Util.FireAndForget(delegate { handler(id, sender, asset); });
                return true;
            }

            if (IsHG(id))
            {
                return m_HGService.Get(id, sender, delegate (string assetID, Object s, AssetBase a)
                {
                    if (m_Cache != null)
                        m_Cache.Cache(a);
                    handler(assetID, s, a);
                });
            }
            else
            {
                return m_GridService.Get(id, sender, delegate (string assetID, Object s, AssetBase a)
                {
                    if (m_Cache != null)
                        m_Cache.Cache(a);
                    handler(assetID, s, a);
                });
            }
        }

        public string Store(AssetBase asset)
        {
            bool isHG = IsHG(asset.ID);

            if ((m_Cache != null) && !isHG)
                // Don't store it in the cache if the asset is to 
                // be sent to the other grid, because this is already
                // a copy of the local asset.
                m_Cache.Cache(asset);

            if (asset.Temporary || asset.Local)
            {
                if (m_Cache != null)
                    m_Cache.Cache(asset);
                return asset.ID;
            }

            string id = string.Empty;
            if (IsHG(asset.ID))
            {
                if (m_AssetPerms.AllowedExport(asset.Type))
                    id = m_HGService.Store(asset);
                else
                    return String.Empty;
            }
            else
                id = m_GridService.Store(asset);

            if (id != String.Empty)
            {
                // Placing this here, so that this work with old asset servers that don't send any reply back
                // SynchronousRestObjectRequester returns somethins that is not an empty string
                if (id != null)
                    asset.ID = id;

                if (m_Cache != null)
                    m_Cache.Cache(asset);
            }
            return id;

        }

        public bool UpdateContent(string id, byte[] data)
        {
            AssetBase asset = null;
            
            if (m_Cache != null)
                asset = m_Cache.Get(id);

            if (asset != null)
            {
                asset.Data = data;
                m_Cache.Cache(asset);
            }

            if (IsHG(id))
                return m_HGService.UpdateContent(id, data);
            else
                return m_GridService.UpdateContent(id, data);
        }

        public bool Delete(string id)
        {
            if (m_Cache != null)
                m_Cache.Expire(id);

            bool result = false;
            if (IsHG(id))
                result = m_HGService.Delete(id);
            else
                result = m_GridService.Delete(id);

            if (result && m_Cache != null)
                m_Cache.Expire(id);

            return result;
        }

    }
}
