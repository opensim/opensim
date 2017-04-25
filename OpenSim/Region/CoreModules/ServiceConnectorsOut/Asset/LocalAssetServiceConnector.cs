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
using Mono.Addins;
using Nini.Config;
using System;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Asset
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LocalAssetServicesConnector")]
    public class LocalAssetServicesConnector : ISharedRegionModule, IAssetService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IAssetCache m_Cache = null;

        private IAssetService m_AssetService;

        private bool m_Enabled = false;

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "LocalAssetServicesConnector"; }
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
                        m_log.Error("[LOCAL ASSET SERVICES CONNECTOR]: AssetService missing from OpenSim.ini");
                        return;
                    }

                    string serviceDll = assetConfig.GetString("LocalServiceModule", String.Empty);

                    if (serviceDll == String.Empty)
                    {
                        m_log.Error("[LOCAL ASSET SERVICES CONNECTOR]: No LocalServiceModule named in section AssetService");
                        return;
                    }
                    else
                    {
                        m_log.DebugFormat("[LOCAL ASSET SERVICES CONNECTOR]: Loading asset service at {0}", serviceDll);
                    }

                    Object[] args = new Object[] { source };
                    m_AssetService = ServerUtils.LoadPlugin<IAssetService>(serviceDll, args);

                    if (m_AssetService == null)
                    {
                        m_log.Error("[LOCAL ASSET SERVICES CONNECTOR]: Can't load asset service");
                        return;
                    }
                    m_Enabled = true;
                    m_log.Info("[LOCAL ASSET SERVICES CONNECTOR]: Local asset connector enabled");
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

            scene.RegisterModuleInterface<IAssetService>(this);
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
                m_Cache = scene.RequestModuleInterface<IAssetCache>();

                if (!(m_Cache is ISharedRegionModule))
                    m_Cache = null;
            }

            m_log.DebugFormat(
                "[LOCAL ASSET SERVICES CONNECTOR]: Enabled connector for region {0}", scene.RegionInfo.RegionName);

            if (m_Cache != null)
            {
                m_log.DebugFormat(
                    "[LOCAL ASSET SERVICES CONNECTOR]: Enabled asset caching for region {0}",
                    scene.RegionInfo.RegionName);
            }
            else
            {
                // Short-circuit directly to storage layer.  This ends up storing temporary and local assets.
                //
                scene.UnregisterModuleInterface<IAssetService>(this);
                scene.RegisterModuleInterface<IAssetService>(m_AssetService);
            }
        }

        public AssetBase Get(string id)
        {
//            m_log.DebugFormat("[LOCAL ASSET SERVICES CONNECTOR]: Synchronously requesting asset {0}", id);

            AssetBase asset = null;
            if (m_Cache != null)
            {
                if (!m_Cache.Get(id, out asset))
                    return null;
            }

            if (asset == null)
            {
                asset = m_AssetService.Get(id);
                if ((m_Cache != null) && (asset != null))
                    m_Cache.Cache(asset);

//                if (null == asset)
//                    m_log.WarnFormat("[LOCAL ASSET SERVICES CONNECTOR]: Could not synchronously find asset with id {0}", id);
            }

            return asset;
        }

        public AssetBase GetCached(string id)
        {
//            m_log.DebugFormat("[LOCAL ASSET SERVICES CONNECTOR]: Cache request for {0}", id);

            AssetBase asset = null;
            if (m_Cache != null)
                m_Cache.Get(id, out asset);

            return asset;
        }

        public AssetMetadata GetMetadata(string id)
        {
            AssetBase asset = null;
            if (m_Cache != null)
            {
                if (!m_Cache.Get(id, out asset))
                    return null;
            }

            if (asset != null)
                return asset.Metadata;

            asset = m_AssetService.Get(id);
            if (asset != null)
            {
                if (m_Cache != null)
                    m_Cache.Cache(asset);
                return asset.Metadata;
            }

            return null;
        }

        public byte[] GetData(string id)
        {
//            m_log.DebugFormat("[LOCAL ASSET SERVICES CONNECTOR]: Requesting data for asset {0}", id);

            AssetBase asset = null;

            if (m_Cache != null)
            {
                if (!m_Cache.Get(id, out asset))
                    return null;
            }

            if (asset != null)
                return asset.Data;

            asset = m_AssetService.Get(id);
            if (asset != null)
            {
                if (m_Cache != null)
                    m_Cache.Cache(asset);
                return asset.Data;
            }

            return null;
        }

        public bool Get(string id, Object sender, AssetRetrieved handler)
        {
//            m_log.DebugFormat("[LOCAL ASSET SERVICES CONNECTOR]: Asynchronously requesting asset {0}", id);

            if (m_Cache != null)
            {
                AssetBase asset;
                if (!m_Cache.Get(id, out asset))
                    return false;

                if (asset != null)
                {
                    Util.FireAndForget(
                        o => handler(id, sender, asset), null, "LocalAssetServiceConnector.GotFromCacheCallback");
                    return true;
                }
            }

            return m_AssetService.Get(id, sender, delegate (string assetID, Object s, AssetBase a)
            {
                if ((a != null) && (m_Cache != null))
                    m_Cache.Cache(a);

//                if (null == a)
//                    m_log.WarnFormat("[LOCAL ASSET SERVICES CONNECTOR]: Could not asynchronously find asset with id {0}", id);

                Util.FireAndForget(
                    o => handler(assetID, s, a), null, "LocalAssetServiceConnector.GotFromServiceCallback");
            });
        }

        public bool[] AssetsExist(string[] ids)
        {
            return m_AssetService.AssetsExist(ids);
        }

        public string Store(AssetBase asset)
        {
            if (m_Cache != null)
                m_Cache.Cache(asset);

            if (asset.Local)
            {
//                m_log.DebugFormat(
//                    "[LOCAL ASSET SERVICE CONNECTOR]: Returning asset {0} {1} without querying database since status Temporary = {2}, Local = {3}",
//                    asset.Name, asset.ID, asset.Temporary, asset.Local);

                return asset.ID;
            }
            else
            {
//                m_log.DebugFormat(
//                    "[LOCAL ASSET SERVICE CONNECTOR]: Passing {0} {1} on to asset service for storage, status Temporary = {2}, Local = {3}",
//                    asset.Name, asset.ID, asset.Temporary, asset.Local);

                return m_AssetService.Store(asset);
            }
        }

        public bool UpdateContent(string id, byte[] data)
        {
            AssetBase asset = null;
            if (m_Cache != null)
                m_Cache.Get(id, out asset);
            if (asset != null)
            {
                asset.Data = data;
                if (m_Cache != null)
                    m_Cache.Cache(asset);
            }

            return m_AssetService.UpdateContent(id, data);
        }

        public bool Delete(string id)
        {
            if (m_Cache != null)
                m_Cache.Expire(id);

            return m_AssetService.Delete(id);
        }
    }
}
