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
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Connectors;
using OpenSim.Services.Interfaces;


namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Asset
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "HGAssetBroker")]
    public class HGAssetBroker : RegionBaseAssetServicesConnector, ISharedRegionModule, IAssetService
    {
        private static readonly ILog m_log = LogManager.GetLogger( MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_aScene;

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

        public HGAssetBroker() {}

        public HGAssetBroker(IConfigSource config)
        {
            Initialise(config);
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("AssetServices", "");
                if (name == Name)
                {
                    IConfig hgConfig = source.Configs["HGAssetService"];
                    if(hgConfig != null)
                        m_AssetPerms = new AssetPermissions(hgConfig); // it's ok if arg is null

                    baseInitialise(source);
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
                m_Cache = scene.RequestModuleInterface<IAssetCache>();

                if (!(m_Cache is ISharedRegionModule))
                    m_Cache = null;
            }

            if (m_Cache != null)
                m_log.InfoFormat("[HG ASSET CONNECTOR]: Enabled asset broker with cache for region {0}", scene.RegionInfo.RegionName);
            else
                m_log.InfoFormat("[HG ASSET CONNECTOR]: Enabled asset broker without cache for region {0}", scene.RegionInfo.RegionName);
        }

        private bool IsHG(string id)
        {
            return id.Length > 0 && (id[0] == 'h' || id[0] == 'H');
        }

        public override AssetBase Get(string id)
        {
            //m_log.DebugFormat("[HG ASSET CONNECTOR]: Get {0}", id);
            AssetBase asset = null;
            if (IsHG(id))
            {
                asset = GetForeign(id);
                if (asset != null)
                {
                    // Now store it locally, if allowed
                    if (m_AssetPerms.AllowedImport(asset.Type))
                        base.Store(asset);
                    else
                        return null;
                }
            }
            else
                asset = base.Get(id);
            return asset;
        }

        public AssetBase Get(string id, string ForeignAssetService)
        {
            // assumes id and ForeignAssetService are valid and resolved
            AssetBase asset = null;
            if (m_Cache != null)
            {
                m_Cache.Get(id, out asset); // negative cache is a fail on HG
            }

            if (asset == null)
            {
                asset = GetFromLocal(id);
                if (asset == null)
                {
                    asset = GetFromForeign(id, ForeignAssetService);
                    if (asset != null)
                    {
                        if (m_AssetPerms.AllowedImport(asset.Type))
                            base.Store(asset);
                        else
                        {
                            if (m_Cache != null)
                                m_Cache.CacheNegative(id);
                            return null;
                        }
                    }
                    else if (m_Cache != null)
                        m_Cache.CacheNegative(id);
                }
                else if (m_Cache != null)
                    m_Cache.Cache(asset);
            }
            return asset;
        }

        public override AssetMetadata GetMetadata(string id)
        {
            if (IsHG(id))
                return GetForeignMetadata(id);
            else
               return base.GetMetadata(id);
        }

        public override byte[] GetData(string id)
        {
            if (IsHG(id))
                return base.GetForeignData(id);
            else
                return base.GetData(id);
        }

        public override bool Get(string id, object sender, AssetRetrieved handler)
        {
            AssetBase asset = null;

            if (m_Cache != null)
            {
                if (!m_Cache.Get(id, out asset))
                    return false;
            }

            if (asset != null)
            {
                Util.FireAndForget(delegate { handler(id, sender, asset); asset = null; }, null, "HGAssetBroker.GotFromCache");
                return true;
            }

            if (IsHG(id))
            {
                return base.GetForeign(id, sender, delegate (string assetID, object s, AssetBase a)
                {
                    if (m_Cache != null)
                        m_Cache.Cache(a);
                    handler(assetID, s, a);
                    a = null;
                });
            }
            else
            {
                return base.Get(id, sender, delegate (string assetID, object s, AssetBase a)
                {
                    if (m_Cache != null)
                        m_Cache.Cache(a);
                    handler(assetID, s, a);
                    a = null;
                });
            }
        }

        public override bool[] AssetsExist(string[] ids)
        {
            int numHG = 0;
            foreach (string id in ids)
            {
                if (IsHG(id))
                    ++numHG;
            }

            return numHG == 0 ? base.AssetsExist(ids) : base.ForeignAssetsExist(ids);
        }

        public override string Store(AssetBase asset)
        {
            if (asset.Local || asset.Temporary)
            {
                if (m_Cache != null)
                    m_Cache.Cache(asset);
                return asset.ID;
            }

            string id;
            if (IsHG(asset.ID))
            {
                if (m_AssetPerms.AllowedExport(asset.Type))
                    id = base.StoreForeign(asset);
                else
                    return string.Empty;
                return id;
            }

            id = base.Store(asset);
            if (string.IsNullOrEmpty(id))
                return string.Empty;

            return id;
        }

        public override bool UpdateContent(string id, byte[] data)
        {
            if (IsHG(id))
                return false;
            return base.UpdateContent(id, data);
        }

        public override bool Delete(string id)
        {
            if (IsHG(id))
                return false;

            return base.Delete(id);
        }
    }
}
