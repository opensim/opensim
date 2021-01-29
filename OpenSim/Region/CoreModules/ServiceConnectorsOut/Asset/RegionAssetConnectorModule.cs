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
using System.Collections.Concurrent;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Timers;

using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;


namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Asset
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RegionAssetConnector")]
    public class RegionAssetConnector : ISharedRegionModule, IAssetService
    {
        private static readonly ILog m_log = LogManager.GetLogger( MethodBase.GetCurrentMethod().DeclaringType);

        private delegate void AssetRetrievedEx(AssetBase asset);
        private bool m_Enabled = false;

        private Scene m_aScene;

        private IAssetCache m_Cache;
        private IAssetService m_localConnector;
        private IAssetService m_HGConnector;
        private AssetPermissions m_AssetPerms;

        //const int MAXSENDRETRIESLEN = 30;
        //private List<AssetBase>[] m_sendRetries;
        //private List<string>[] m_sendCachedRetries;
        //private System.Timers.Timer m_retryTimer;

        //private int m_retryCounter;
        //private bool m_inRetries;

        private Dictionary<string, List<AssetRetrievedEx>> m_AssetHandlers = new Dictionary<string, List<AssetRetrievedEx>>();

        private ObjectJobEngine m_requestQueue;

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "RegionAssetConnector"; }
        }

        public RegionAssetConnector() {}

        public RegionAssetConnector(IConfigSource config)
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
                    IConfig assetConfig = source.Configs["AssetService"];
                    if (assetConfig == null)
                    {
                        m_log.Error("[REGIONASSETCONNECTOR]: AssetService missing from configuration files");
                        throw new Exception("Region asset connector init error");
                    }

                    string localGridConnector = assetConfig.GetString("LocalGridAssetService", string.Empty);
                    if(string.IsNullOrEmpty(localGridConnector))
                    {
                        m_log.Error("[REGIONASSETCONNECTOR]: LocalGridAssetService missing from configuration files");
                        throw new Exception("Region asset connector init error");
                    }

                    object[] args = new object[] { source };

                    m_localConnector = ServerUtils.LoadPlugin<IAssetService>(localGridConnector, args);
                    if (m_localConnector == null)
                    {
                        m_log.Error("[REGIONASSETCONNECTOR]: Fail to load local asset service " + localGridConnector);
                        throw new Exception("Region asset connector init error");
                    }

                    string HGConnector = assetConfig.GetString("HypergridAssetService", string.Empty);
                    if(!string.IsNullOrEmpty(HGConnector))
                    {
                        m_HGConnector = ServerUtils.LoadPlugin<IAssetService>(HGConnector, args);
                        if (m_HGConnector == null)
                        {
                            m_log.Error("[REGIONASSETCONNECTOR]: Fail to load HG asset service " + HGConnector);
                            throw new Exception("Region asset connector init error");
                        }
                        IConfig hgConfig = source.Configs["HGAssetService"];
                        if (hgConfig != null)
                            m_AssetPerms = new AssetPermissions(hgConfig);
                    }

                    m_requestQueue = new ObjectJobEngine(AssetRequestProcessor, "GetAssetsWorkers", 2000, 2);
                    m_Enabled = true;
                    m_log.Info("[REGIONASSETCONNECTOR]: enabled");
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
            if (!m_Enabled)
                return;

            m_requestQueue.Dispose();
            m_requestQueue = null;
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

            if(m_HGConnector == null)
            {
                if (m_Cache != null)
                    m_log.InfoFormat("[REGIONASSETCONNECTOR]: active with cache for region {0}", scene.RegionInfo.RegionName);
                else
                    m_log.InfoFormat("[REGIONASSETCONNECTOR]: active  without cache for region {0}", scene.RegionInfo.RegionName);
            }
            else
            {
                if (m_Cache != null)
                    m_log.InfoFormat("[REGIONASSETCONNECTOR]: active with HG and cache for region {0}", scene.RegionInfo.RegionName);
                else
                    m_log.InfoFormat("[REGIONASSETCONNECTOR]: active with HG and without cache for region {0}", scene.RegionInfo.RegionName);
            }
        }


        private bool IsHG(string id)
        {
            return id.Length > 0 && (id[0] == 'h' || id[0] == 'H');
        }

        public AssetBase GetCached(string id)
        {
            AssetBase asset = null;
            if (m_Cache != null)
                m_Cache.Get(id, out asset);
            return asset;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private AssetBase GetFromLocal(string id)
        {
            return m_localConnector.Get(id);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private AssetBase GetFromForeign(string id, string ForeignAssetService)
        {
            if (m_HGConnector == null || string.IsNullOrEmpty(ForeignAssetService))
                return null;
            return m_HGConnector.Get(id , ForeignAssetService, true);
        }

        public AssetBase GetForeign(string id)
        {
            int type = Util.ParseForeignAssetID(id, out string uri, out string uuidstr);
            if (type < 0)
                return null;

            AssetBase asset = null;
            if (m_Cache != null)
            {
                 asset = m_Cache.GetCached(uuidstr);
                if(asset != null)
                    return asset;
            }

            asset = GetFromLocal(uuidstr);
            if (asset != null || type == 0)
                return asset;
            return GetFromForeign(uuidstr, uri);
        }

        public AssetBase Get(string id)
        {
            //m_log.DebugFormat("[HG ASSET CONNECTOR]: Get {0}", id);
            AssetBase asset = null;
            if (IsHG(id))
            {
                asset = GetForeign(id);
                if (asset != null)
                {
                    // Now store it locally, if allowed
                    if (m_AssetPerms != null && !m_AssetPerms.AllowedImport(asset.Type))
                        return null;
                    Store(asset);
                }
            }
            else
            {
                if (m_Cache != null)
                {
                    if(!m_Cache.Get(id, out asset))
                        return null;
                    if (asset != null)
                        return asset;
                }
                asset = GetFromLocal(id);
                if(m_Cache != null)
                {
                    if(asset == null)
                        m_Cache.CacheNegative(id);
                    else
                        m_Cache.Cache(asset);
                }
            }
            return asset;
        }

        public AssetBase Get(string id, string ForeignAssetService, bool StoreOnLocalGrid)
        {
            // assumes id and ForeignAssetService are valid and resolved
            AssetBase asset = null;
            if (m_Cache != null)
            {
                asset = m_Cache.GetCached(id);
                if (asset != null)
                    return asset;
            }

            asset = GetFromLocal(id);
            if (asset == null)
            {
                asset = GetFromForeign(id, ForeignAssetService);
                if (asset != null)
                {
                    if (m_AssetPerms != null && !m_AssetPerms.AllowedImport(asset.Type))
                    {
                        if (m_Cache != null)
                            m_Cache.CacheNegative(id);
                        return null;
                    }
                    if(StoreOnLocalGrid)
                        Store(asset);
                    else if (m_Cache != null)
                        m_Cache.Cache(asset);
                }
                else if (m_Cache != null)
                    m_Cache.CacheNegative(id);
            }
            else if (m_Cache != null)
                m_Cache.Cache(asset);

            return asset;
        }

        public AssetMetadata GetMetadata(string id)
        {
            AssetBase asset = Get(id);
            if (asset != null)
                return asset.Metadata;
            return null;
        }

        public byte[] GetData(string id)
        {
            AssetBase asset = Get(id);
            if (asset != null)
                return asset.Data;
            return null;
        }

        public virtual bool Get(string id, object sender, AssetRetrieved callBack)
        {
            AssetBase asset = null;
            if (m_Cache != null)
            {
                if (!m_Cache.Get(id, out asset))
                    return false;
            }

            if (asset == null)
            {
                lock (m_AssetHandlers)
                {
                    AssetRetrievedEx handlerEx = new AssetRetrievedEx(delegate (AssetBase _asset) { callBack(id, sender, _asset); });

                    List<AssetRetrievedEx> handlers;
                    if (m_AssetHandlers.TryGetValue(id, out handlers))
                    {
                        // Someone else is already loading this asset. It will notify our handler when done.
                        handlers.Add(handlerEx);
                        return true;
                    }

                    handlers = new List<AssetRetrievedEx>();
                    handlers.Add(handlerEx);

                    m_AssetHandlers.Add(id, handlers);
                    m_requestQueue.Enqueue(id);
                }
            }
            else
            {
                if (asset != null && (asset.Data == null || asset.Data.Length == 0))
                    asset = null;
                callBack(id, sender, asset);
            }
            return true;
        }

        private void AssetRequestProcessor(object o)
        {
            string id = o as string;
            if(id == null)
                return;

            try
            {
                AssetBase a = Get(id);
                List<AssetRetrievedEx> handlers;
                lock (m_AssetHandlers)
                {
                    handlers = m_AssetHandlers[id];
                    m_AssetHandlers.Remove(id);
                }

                if (handlers != null)
                {
                    Util.FireAndForget(x =>
                    {
                        foreach (AssetRetrievedEx h in handlers)
                        {
                            try
                            {
                                h.Invoke(a);
                            }
                            catch { }
                        }
                        handlers.Clear();
                    });
                }
            }
            catch { }
        }

        public bool[] AssetsExist(string[] ids)
        {
            int numHG = 0;
            foreach (string id in ids)
            {
                if (IsHG(id))
                    ++numHG;
            }
            if(numHG == 0)
                return m_localConnector.AssetsExist(ids);
            else if (m_HGConnector != null)
                return m_HGConnector.AssetsExist(ids);
            return null;
        }

        private readonly string stringUUIDZero = UUID.Zero.ToString();
        public string Store(AssetBase asset)
        {
            string id;
            if (IsHG(asset.ID))
            {
                if (asset.Local || asset.Temporary)
                    return null;

                id = StoreForeign(asset);
                if (m_Cache != null)
                {
                    if (!string.IsNullOrEmpty(id) && !id.Equals(stringUUIDZero))
                        m_Cache.Cache(asset);
                }
                return id;
            }

            if (m_Cache != null)
            {
                 m_Cache.Cache(asset);
                if (asset.Local || asset.Temporary)
                    return asset.ID;
            }

            id = StoreLocal(asset);

            if (string.IsNullOrEmpty(id))
                return string.Empty;

            return id;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private string StoreForeign(AssetBase asset)
        {
            if (m_HGConnector == null)
                return string.Empty;
            if (m_AssetPerms != null && !m_AssetPerms.AllowedExport(asset.Type))
                return string.Empty;
            return m_HGConnector.Store(asset);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private string StoreLocal(AssetBase asset)
        {
            return m_localConnector.Store(asset);
        }

        public bool UpdateContent(string id, byte[] data)
        {
            if (IsHG(id))
                return false;
            return m_localConnector.UpdateContent(id, data);
        }

        public bool Delete(string id)
        {
            if (IsHG(id))
                return false;

            return m_localConnector.Delete(id);
        }
    }
}
