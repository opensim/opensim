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
using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Timers;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Services.Connectors
{
    public class AssetServicesConnector : BaseServiceConnector, IAssetService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public readonly object ConnectorLock = new object();

        protected IAssetCache m_Cache = null;

        private string m_ServerURI = string.Empty;

        private delegate void AssetRetrievedEx(AssetBase asset);

        // Keeps track of concurrent requests for the same asset, so that it's only loaded once.
        // Maps: Asset ID -> Handlers which will be called when the asset has been loaded

        private Dictionary<string, List<AssetRetrievedEx>> m_AssetHandlers = new Dictionary<string, List<AssetRetrievedEx>>();

        public AssetServicesConnector()
        {
        }

        public AssetServicesConnector(string serverURI)
        {
            OSHHTPHost tmp = new OSHHTPHost(serverURI, true);
            m_ServerURI = tmp.IsResolvedHost ? tmp.URI : null;
        }

        public AssetServicesConnector(IConfigSource source) 
        {
            Initialise(source);
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig netconfig = source.Configs["Network"];

            IConfig assetConfig = source.Configs["AssetService"];
            if (assetConfig == null)
            {
                m_log.Error("[ASSET CONNECTOR]: AssetService missing from OpenSim.ini");
                throw new Exception("Asset connector init error");
            }

            m_ServerURI = assetConfig.GetString("AssetServerURI", string.Empty);
            if (string.IsNullOrEmpty(m_ServerURI))
            {
                if(netconfig != null)
                    m_ServerURI = netconfig.GetString("asset_server_url", string.Empty);
            }
            if (string.IsNullOrEmpty(m_ServerURI))
            {
                m_log.Error("[ASSET CONNECTOR]: AssetServerURI not defined in section AssetService");
                throw new Exception("Asset connector init error");
            }

            OSHHTPHost m_GridAssetsURL = new OSHHTPHost(m_ServerURI, true);
            if(!m_GridAssetsURL.IsResolvedHost)
            {
                m_log.Error("[ASSET CONNECTOR]: Could not parse or resolve AssetServerURI");
                throw new Exception("Asset connector init error");
            }

            m_ServerURI = m_GridAssetsURL.URI;
            Initialise(source, "AssetService");
        }

        private int m_maxAssetRequestConcurrency = 8;
        public int MaxAssetRequestConcurrency
        {
            get { return m_maxAssetRequestConcurrency; }
            set { m_maxAssetRequestConcurrency = value; }
        }

        protected void SetCache(IAssetCache cache)
        {
            m_Cache = cache;
        }

        public AssetBase GetCached(string id)
        {
            AssetBase asset = null;
            if (m_Cache != null)
            {
                m_Cache.Get(id, out asset);
            }

            return asset;
        }

        public virtual AssetBase Get(string id)
        {
            AssetBase asset = null;
            if (m_Cache != null)
            {
                if (!m_Cache.Get(id, out asset))
                    return null;
            }

            if (asset == null && m_ServerURI != null)
            {
                string uri = m_ServerURI + "/assets/" + id;

                asset = SynchronousRestObjectRequester.MakeGetRequest<AssetBase>(uri, 0, m_Auth);
                if (m_Cache != null)
                {
                    if (asset != null)
                        m_Cache.Cache(asset);
                    else
                        m_Cache.CacheNegative(id);
                }
            }
            return asset;
        }

        public AssetBase Get(string id, string ForeignAssetService, bool dummy)
        {
            return null;
        }

        public virtual AssetMetadata GetMetadata(string id)
        {
            if (m_Cache != null)
            {
                AssetBase fullAsset;
                if (!m_Cache.Get(id, out fullAsset))
                    return null;

                if (fullAsset != null)
                    return fullAsset.Metadata;
            }

            if (m_ServerURI == null)
                return null;

            string uri = m_ServerURI + "/assets/" + id + "/metadata";
            return SynchronousRestObjectRequester.MakeGetRequest<AssetMetadata>(uri, 0, m_Auth);
        }


        public virtual byte[] GetData(string id)
        {
            if (m_Cache != null)
            {
                if (!m_Cache.Get(id, out AssetBase fullAsset))
                    return null;

                if (fullAsset != null)
                    return fullAsset.Data;
            }

            if (m_ServerURI == null)
                return null;

            using (RestClient rc = new RestClient(m_ServerURI))
            {
                rc.AddResourcePath("assets/" + id + "/data");
                rc.RequestMethod = "GET";

                using (MemoryStream s = rc.Request(m_Auth))
                {
                    if (s == null || s.Length == 0)
                        return null;
                    return s.ToArray();
                }
            }
        }

        public virtual bool Get(string id, object sender, AssetRetrieved handler)
        {
            AssetBase asset = null;
            if (m_Cache != null)
            {
                if (!m_Cache.Get(id, out asset))
                    return false;
            }

            if (asset == null && m_ServerURI != null)
            {
                string uri = m_ServerURI + "/assets/" + id;

                lock (m_AssetHandlers)
                {
                    AssetRetrievedEx handlerEx = new AssetRetrievedEx(delegate (AssetBase _asset) { handler(id, sender, _asset); });

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

                    QueuedAssetRequest request = new QueuedAssetRequest();
                    request.id = id;
                    request.uri = uri;
                    Util.FireAndForget(x =>
                    {
                        AssetRequestProcessor(request);
                    });
                }
            }
            else
            {
                if (asset != null && (asset.Data == null || asset.Data.Length == 0))
                    asset = null;
                handler(id, sender, asset);
            }

            return true;
        }

        private class QueuedAssetRequest
        {
            public string uri;
            public string id;
        }

        private void AssetRequestProcessor(QueuedAssetRequest r)
        {
            string id = r.id;
            try
            {
                AssetBase a = SynchronousRestObjectRequester.MakeGetRequest<AssetBase>(r.uri, 30000, m_Auth);

                if (a != null && m_Cache != null)
                    m_Cache.Cache(a);

                List<AssetRetrievedEx> handlers;
                lock (m_AssetHandlers)
                {
                    handlers = m_AssetHandlers[id];
                    m_AssetHandlers.Remove(id);
                }

                if(handlers != null)
                {
                    foreach (AssetRetrievedEx h in handlers)
                    {
                        try { h.Invoke(a); }
                        catch { }
                    }
                    handlers.Clear();
                }
            }
            catch { }
        }

        public virtual bool[] AssetsExist(string[] ids)
        {
            if (m_ServerURI == null)
                return null;

            string uri = m_ServerURI + "/get_assets_exist";

            bool[] exist = null;
            try
            {
                exist = SynchronousRestObjectRequester.MakeRequest<string[], bool[]>("POST", uri, ids, m_Auth);
            }
            catch (Exception)
            {
                // This is most likely to happen because the server doesn't support this function,
                // so just silently return "doesn't exist" for all the assets.
            }

            if (exist == null)
                exist = new bool[ids.Length];

            return exist;
        }

        string stringUUIDZero = UUID.Zero.ToString();

        public virtual string Store(AssetBase asset)
        {
            // Have to assign the asset ID here. This isn't likely to
            // trigger since current callers don't pass emtpy IDs
            // We need the asset ID to route the request to the proper
            // cluster member, so we can't have the server assign one.
            if (asset.ID.Length == 0 || asset.ID == stringUUIDZero)
            {
                if (asset.FullID.IsZero())
                {
                    asset.FullID = UUID.Random();
                    m_log.WarnFormat("[Assets] Zero ID: {0}", asset.Name);
                }
                asset.ID = asset.FullID.ToString();
            }
            else if (asset.FullID.IsZero())
            {
                if (UUID.TryParse(asset.ID, out UUID uuid))
                    asset.FullID = uuid;
                else
                {
                    m_log.WarnFormat("[Assets] Zero IDs: {0}",asset.Name);
                    asset.FullID = UUID.Random();
                    asset.ID = asset.FullID.ToString();
                }
            }

            if (asset.Temporary || asset.Local)
            {
                m_Cache?.Cache(asset);
                return asset.ID;
            }

            if (m_ServerURI == null)
                return null;

            string uri = m_ServerURI + "/assets/";

            string newID = null;
            try
            {
                newID = SynchronousRestObjectRequester.MakeRequest<AssetBase, string>("POST", uri, asset, 10000, m_Auth);
            }
            catch
            {
                newID = null;
            }

            if (string.IsNullOrEmpty(newID) || newID == stringUUIDZero)
            {
                return string.Empty;
            }
            else
            {
                if (newID != asset.ID)
                {
                    // Placing this here, so that this work with old asset servers that don't send any reply back
                    // SynchronousRestObjectRequester returns somethins that is not an empty string
                    asset.ID = newID;
                    m_Cache?.Cache(asset);
                }
            }

            return asset.ID;
        }

        public virtual bool UpdateContent(string id, byte[] data)
        {
            if (m_ServerURI == null)
                return false;

            AssetBase asset = null;

            m_Cache?.Get(id, out asset);

            if (asset == null)
            {
                AssetMetadata metadata = GetMetadata(id);
                if (metadata == null)
                    return false;

                asset = new AssetBase(metadata.FullID, metadata.Name, metadata.Type, UUID.Zero.ToString());
                asset.Metadata = metadata;
            }
            asset.Data = data;

            string uri = m_ServerURI + "/assets/" + id;

            if (SynchronousRestObjectRequester.MakeRequest<AssetBase, bool>("POST", uri, asset, m_Auth))
            {
                m_Cache?.Cache(asset, true);
                return true;
            }
            return false;
        }

        public virtual bool Delete(string id)
        {
            m_Cache?.Expire(id);

            if (m_ServerURI == null)
                return false;

            string uri = m_ServerURI + "/assets/" + id;
            return SynchronousRestObjectRequester.MakeRequest<int, bool>("DELETE", uri, 0, m_Auth);
        }
        public void Get(string id, string ForeignAssetService, bool StoreOnLocalGrid, SimpleAssetRetrieved callBack)
        {
            return;
        }

    }
}
