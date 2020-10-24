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
    public class RegionBaseAssetServicesConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        const int MAXSENDRETRIESLEN = 30;
        protected IServiceAuth m_Auth;
        protected IAssetCache m_Cache = null;
        private List<AssetBase>[] m_sendRetries;
        private List<string>[] m_sendCachedRetries;
        private System.Timers.Timer m_retryTimer;

        public readonly object ConnectorLock = new object();
        private string m_ServerURI = string.Empty;
        private int m_retryCounter;
        private bool m_inRetries;

        private int m_maxAssetRequestConcurrency = 8;

        private delegate void AssetRetrievedEx(AssetBase asset);

        // Keeps track of concurrent requests for the same asset, so that it's only loaded once.
        // Maps: Asset ID -> Handlers which will be called when the asset has been loaded

        private Dictionary<string, List<AssetRetrievedEx>> m_AssetHandlers = new Dictionary<string, List<AssetRetrievedEx>>();

        private Dictionary<string, string> m_UriMap;

        private Thread[] m_fetchThreads;

        public int MaxAssetRequestConcurrency
        {
            get { return m_maxAssetRequestConcurrency; }
            set { m_maxAssetRequestConcurrency = value; }
        }

        public void baseInitialise(IConfigSource source)
        {
            IConfig assetConfig = source.Configs["AssetService"];
            if (assetConfig == null)
            {
                m_log.Error("[ASSET CONNECTOR]: AssetService missing from OpenSim.ini");
                throw new Exception("Asset connector init error");
            }

            IConfig netConfig = source.Configs["Network"];
            m_ServerURI = assetConfig.GetString("AssetServerURI", string.Empty);
            if (string.IsNullOrEmpty(m_ServerURI))
            {
                if(netConfig != null)
                    m_ServerURI = netConfig.GetString("asset_server_url", string.Empty);
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

            string authType = assetConfig.GetString("AuthType", string.Empty);
            if (string.IsNullOrEmpty(authType))
            {
                if (netConfig != null)
                    authType = netConfig.GetString("AuthType", string.Empty);
            }
            switch (authType)
            {
                case "BasicHttpAuthentication":
                    m_Auth = new BasicHttpAuthentication(source, "AssetService");
                    break;
            }

            bool usemaps = assetConfig.GetBoolean("AssetServerIsMultiple", false);

            if(usemaps)
            {
                m_UriMap = new Dictionary<string, string>();
                for (int i = 0; i < 256; i++)
                {
                    string prefix = i.ToString("x2");
                    string groupHost = assetConfig.GetString("AssetServerHost_" + prefix, string.Empty);
                    if(string.IsNullOrEmpty(groupHost))
                        m_UriMap[prefix] = m_ServerURI;
                    else
                    {
                         OSHHTPHost other = new OSHHTPHost(groupHost, true);
                         if(!other.IsResolvedHost)
                         {
                             m_log.Error("[ASSET CONNECTOR]: Could not parse or resolve AssetServerHost_" + prefix);
                            throw new Exception("Asset connector init error");
                         }
                         m_UriMap[prefix] = other.URI;
                    }
                }
            }
            else
                m_UriMap = null;

            m_sendRetries = new List<AssetBase>[MAXSENDRETRIESLEN];
            m_sendCachedRetries = new List<string>[MAXSENDRETRIESLEN];

            m_retryTimer = new System.Timers.Timer();
            m_retryTimer.Elapsed += new ElapsedEventHandler(retryCheck);
            m_retryTimer.AutoReset = true;
            m_retryTimer.Interval = 60000;

            m_fetchThreads = new Thread[3];

            for (int i = 0 ; i < m_fetchThreads.Length; i++)
            {
                m_fetchThreads[i] = WorkManager.StartThread(AssetRequestProcessor, string.Format("GetAssetsWorker{0}", i));
            }
        }

        private string MapServer(string id)
        {
            if (m_UriMap == null)
                return m_ServerURI;

            string prefix = id.Substring(0, 2).ToLower();

            if (m_UriMap.TryGetValue(prefix, out string host))
                return host;

            return m_UriMap["00"];
        }

        protected void retryCheck(object source, ElapsedEventArgs e)
        {
            lock(m_sendRetries)
            {
                if(m_inRetries)
                    return;
                m_inRetries = true;
            }

            m_retryCounter++;
            if(m_retryCounter >= 61 ) // avoid overflow 60 is max in use below
                m_retryCounter = 1;

            int inUse = 0;
            int nextlevel;
            int timefactor;
            if(m_Cache == null)
            {
                List<AssetBase> retrylist;
                // we need to go down
                for(int i = MAXSENDRETRIESLEN - 1; i >= 0; i--)
                {
                    lock(m_sendRetries)
                        retrylist = m_sendRetries[i];

                    if(retrylist == null)
                        continue;

                    inUse++;
                    nextlevel = i + 1;

                    //We exponentially fall back on frequency until we reach one attempt per hour
                    //The net result is that we end up in the queue for roughly 24 hours..
                    //24 hours worth of assets could be a lot, so the hope is that the region admin
                    //will have gotten the asset connector back online quickly!
                    if(i == 0)
                        timefactor = 1;
                    else
                    {
                        timefactor = 1 << nextlevel;
                        if (timefactor > 60)
                            timefactor = 60;
                    }

                    if(m_retryCounter < timefactor)
                        continue; // to update inUse;

                    if (m_retryCounter % timefactor != 0)
                        continue;

                    // a list to retry
                    lock(m_sendRetries)
                        m_sendRetries[i] = null;

                    // we are the only ones with a copy of this retrylist now
                    foreach(AssetBase ass in retrylist)
                       retryStore(ass, nextlevel);
                }
            }
            else
            {
                List<string> retrylist;

                for (int i = MAXSENDRETRIESLEN - 1; i >= 0; i--)
                {
                    lock (m_sendRetries)
                        retrylist = m_sendCachedRetries[i];

                    if (retrylist == null)
                        continue;

                    inUse++;
                    nextlevel = i + 1;

                    //We exponentially fall back on frequency until we reach one attempt per hour
                    //The net result is that we end up in the queue for roughly 24 hours..
                    //24 hours worth of assets could be a lot, so the hope is that the region admin
                    //will have gotten the asset connector back online quickly!
                    if (i == 0)
                        timefactor = 1;
                    else
                    {
                        timefactor = 1 << nextlevel;
                        if (timefactor > 60)
                            timefactor = 60;
                    }

                    if (m_retryCounter < timefactor)
                        continue; // to update inUse;

                    if (m_retryCounter % timefactor != 0)
                        continue;

                    // a list to retry
                    lock (m_sendRetries)
                        m_sendCachedRetries[i] = null;

                    // we are the only ones with a copy of this retrylist now
                    foreach (string id in retrylist)
                        retryCachedStore(id, nextlevel);
                }
            }

            lock (m_sendRetries)
            {
                if(inUse == 0 )
                    m_retryTimer.Stop();

                m_inRetries = false;
            }
        }

        public void SetCache(IAssetCache cache)
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

            if (asset == null)
            {
                asset = GetFromLocal(id);
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

        public AssetBase GetFromLocal(string id)
        {
            string local = MapServer(id) + "/assets/" + id;
            return SynchronousRestObjectRequester.MakeRequest<int, AssetBase>("GET", local, 0, m_Auth);
        }

        public AssetBase GetFromForeign(string id, string ForeignAssetService)
        {
            if(string.IsNullOrEmpty(ForeignAssetService) || ForeignAssetService.Equals(m_ServerURI))
                return null;
            if(ForeignAssetService.EndsWith("/"))
                ForeignAssetService = ForeignAssetService + "assets/" + id;
            else
                ForeignAssetService = ForeignAssetService + "/assets/" + id;
            return SynchronousRestObjectRequester.MakeRequest<int, AssetBase>("GET", ForeignAssetService, 0, null);
        }

        public AssetBase GetForeign(string id)
        {
            int type = Util.ParseForeignAssetID(id, out string uri, out string uuidstr);
            if (type < 0)
                return null;

            AssetBase asset = null;
            if (m_Cache != null)
            {
                //if (!m_Cache.Get(uuidstr, out asset))
                //    return null;
                m_Cache.Get(uuidstr, out asset); // negative cache is a fail on HG
            }

            if (asset == null)
            {
                IServiceAuth auth = null;
                if (type == 0)
                {
                    uri = MapServer(uuidstr) + "/assets/" + uuidstr;
                    auth = m_Auth;
                }
                else
                    uri = uri + "/assets/" + uuidstr;

                asset = SynchronousRestObjectRequester.MakeRequest<int, AssetBase>("GET", uri, 0, auth);
            }
            return asset;
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

            string uri = MapServer(id) + "/assets/" + id + "/metadata";

            AssetMetadata asset = SynchronousRestObjectRequester.MakeRequest<int, AssetMetadata>("GET", uri, 0, m_Auth);
            return asset;
        }

        public AssetMetadata GetForeignMetadata(string id)
        {
            int type = Util.ParseForeignAssetID(id, out string uri, out string uuidstr);
            if (type < 0)
                return null;

            if (m_Cache != null)
            {
                AssetBase fullAsset;
                if (!m_Cache.Get(uuidstr, out fullAsset))
                    return null;

                if (fullAsset != null)
                    return fullAsset.Metadata;
            }

            IServiceAuth auth = null;
            if (type == 0)
            {
                auth = m_Auth;
                uri = MapServer(uuidstr) + "/assets/" + uuidstr + "/metadata";
            }
            else
                uri = uri + "/assets/" + uuidstr + "/metadata";

            AssetMetadata asset = SynchronousRestObjectRequester.MakeRequest<int, AssetMetadata>("GET", uri, 0, auth);
            return asset;
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

            string uri = MapServer(id);

            using (RestClient rc = new RestClient(uri))
            {
                rc.AddResourcePath("assets/" + id + "/Data");
                rc.RequestMethod = "GET";

                using (MemoryStream s = rc.Request(m_Auth))
                {
                    if (s == null || s.Length == 0)
                        return null;
                    return s.ToArray();
                }
            }
        }

        public byte[] GetForeignData(string id)
        {
            int type = Util.ParseForeignAssetID(id, out string uri, out string uuidstr);
            if (type < 0)
                return null;

            if (m_Cache != null)
            {
                if (!m_Cache.Get(uuidstr, out AssetBase fullAsset))
                    return null;

                if (fullAsset != null)
                    return fullAsset.Data;
            }

            IServiceAuth auth = null;
            if (type == 0)
            {
                uri = MapServer(uuidstr);
                auth = m_Auth;
            }

            using (RestClient rc = new RestClient(uri))
            {
                rc.AddResourcePath("assets/" + id + "/Data");
                rc.RequestMethod = "GET";

                using (MemoryStream s = rc.Request(auth))
                {
                    if (s == null || s.Length == 0)
                        return null;
                    return s.ToArray();
                }
            }
        }

        private class QueuedAssetRequest
        {
            public string uri;
            public string id;
            public IServiceAuth auth;
        }

        public virtual bool Get(string id, object sender, AssetRetrieved handler)
        {
            AssetBase asset = null;
            if (m_Cache != null)
            {
                if (!m_Cache.Get(id, out asset))
                    return false;
            }

            if (asset == null)
            {
                string uri = MapServer(id) + "/assets/" + id;

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
                    request.auth = m_Auth;
                    m_requestQueue.Add(request);
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

        public bool GetForeign(string id, object sender, AssetRetrieved handler)
        {
            int type = Util.ParseForeignAssetID(id, out string uri, out string uuidstr);
            if (type < 0)
                return false;

            AssetBase asset = null;
            if (m_Cache != null)
            {
                m_Cache.Get(uuidstr, out asset);
            }

            if (asset == null)
            {
                IServiceAuth auth = null;
                if (type == 0)
                {
                    uri = MapServer(uuidstr) + "/assets/" + uuidstr;
                    auth = m_Auth;
                }
                else
                    uri = uri + "/assets/" + uuidstr;

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
                    request.auth = m_Auth;
                    m_requestQueue.Add(request);
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

        private BlockingCollection<QueuedAssetRequest> m_requestQueue = new BlockingCollection<QueuedAssetRequest>();
        private void AssetRequestProcessor()
        {
            QueuedAssetRequest r;

            while (true)
            {
                if(!m_requestQueue.TryTake(out r, 4500) || r == null)
                {
                    Watchdog.UpdateThread();
                    continue;
                }

                Watchdog.UpdateThread();
                string id = r.id;

                try
                {
                    AssetBase a = SynchronousRestObjectRequester.MakeRequest<int, AssetBase>("GET", r.uri, 0, 30000, r.auth);

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
                        Util.FireAndForget(x =>
                        {
                            foreach (AssetRetrievedEx h in handlers)
                            {
                                try { h.Invoke(a); }
                                catch { }
                            }
                            handlers.Clear();
                        });
                    }
                }
                catch { }
            }
        }

        public virtual bool[] AssetsExist(string[] ids)
        {
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

        private struct AssetAndIndex
        {
            public string assetID;
            public int index;
            public IServiceAuth auth;

            public AssetAndIndex(string assetID, int index, IServiceAuth auth)
            {
                this.assetID = assetID;
                this.index = index;
                this.auth = auth;
            }
        }

        public bool[] ForeignAssetsExist(string[] ids)
        {
            bool[] exist = new bool[ids.Length];

            var url2assets = new Dictionary<string, List<AssetAndIndex>>();

            for (int i = 0; i < ids.Length; i++)
            {
                int ltype = Util.ParseForeignAssetID(ids[i], out string lurl, out string luuidstr);
                if (ltype > 0)
                {
                    IServiceAuth auth = null;

                    if (ltype == 0)
                    {
                        lurl = m_ServerURI;
                        auth = m_Auth;
                    }

                    List < AssetAndIndex > lst;
                    if (!url2assets.TryGetValue(lurl, out lst))
                    {
                        lst = new List<AssetAndIndex>();
                        url2assets.Add(lurl, lst);
                    }
                    lst.Add(new AssetAndIndex(luuidstr, i, auth));
                }
            }

            // Query each of the servers in turn
            foreach (KeyValuePair<string, List<AssetAndIndex>> kvp in url2assets)
            {
                List<AssetAndIndex> curAssets = kvp.Value;
                string[] assetIDs = new string[curAssets.Count];
                IServiceAuth auth = curAssets[0].auth;
                for (int i = 0; i < assetIDs.Length;++i)
                    assetIDs[i] = curAssets[i].assetID;

                string uri = kvp.Key + "/get_assets_exist";

                bool[] curExist = null;
                try
                {
                    curExist = SynchronousRestObjectRequester.MakeRequest<string[], bool[]>("POST", uri, assetIDs, auth);
                }
                catch (Exception)
                {
                    // This is most likely to happen because the server doesn't support this function,
                    // so just silently return "doesn't exist" for all the assets.
                }

                if(curExist != null)
                {
                    int i = 0;
                    foreach (AssetAndIndex ai in curAssets)
                    {
                        exist[ai.index] = curExist[i];
                        ++i;
                    }
                }
            }

            return exist;
        }

        string stringUUIDZero = UUID.Zero.ToString();

        public virtual string Store(AssetBase asset)
        {
            // Have to assign the asset ID here. This isn't likely to
            // trigger since current callers don't pass emtpy IDs
            // We need the asset ID to route the request to the proper
            // cluster member, so we can't have the server assign one.
            if (asset.ID == string.Empty || asset.ID == stringUUIDZero)
            {
                if (asset.FullID == UUID.Zero)
                {
                    asset.FullID = UUID.Random();
                }
                m_log.WarnFormat("[Assets] Zero ID: {0}",asset.Name);
                asset.ID = asset.FullID.ToString();
            }

            if (asset.FullID == UUID.Zero)
            {
                UUID uuid = UUID.Zero;
                if (UUID.TryParse(asset.ID, out uuid))
                {
                    asset.FullID = uuid;
                }
                if(asset.FullID == UUID.Zero)
                {
                    m_log.WarnFormat("[Assets] Zero IDs: {0}",asset.Name);
                    asset.FullID = UUID.Random();
                    asset.ID = asset.FullID.ToString();
                }
            }

            if (m_Cache != null)
                m_Cache.Cache(asset);

            if (asset.Temporary || asset.Local)
            {
                return asset.ID;
            }

            string uri = MapServer(asset.FullID.ToString()) + "/assets/";

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
                //The asset upload failed, try later
                if(m_sendRetries != null)
                {
                    lock(m_sendRetries)
                    {
                        if(m_Cache == null)
                        {
                            if (m_sendRetries[0] == null)
                                m_sendRetries[0] = new List<AssetBase>();
                            m_sendRetries[0].Add(asset);
                        }
                        else
                        {
                            if (m_sendCachedRetries[0] == null)
                                m_sendCachedRetries[0] = new List<string>();
                            m_sendCachedRetries[0].Add(asset.ID);
                        }

                        m_log.WarnFormat("[Assets] Upload failed: {0} type {1} will retry later",
                                asset.ID.ToString(), asset.Type.ToString());
                        m_retryTimer.Start();
                    }
                }
            }
            else
            {
                if (newID != asset.ID)
                {
                    // Placing this here, so that this work with old asset servers that don't send any reply back
                    // SynchronousRestObjectRequester returns somethins that is not an empty string
                    asset.ID = newID;
                    if (m_Cache != null)
                        m_Cache.Cache(asset);
                }
            }

            return asset.ID;
        }

        public virtual string StoreForeign(AssetBase asset)
        {
            int type = Util.ParseForeignAssetID(asset.ID, out string uri, out string uuidstr);
            if(type < 0)
                return string.Empty;

            if(type != 0)
                asset.ID = uuidstr;

            if (m_Cache != null)
                m_Cache.Cache(asset);

            if (asset.Temporary || asset.Local)
            {
                return asset.ID;
            }

            IServiceAuth auth = null;
            if(type == 0)
            {
                uri = MapServer(uuidstr) + "/assets/";
                auth = m_Auth;
            }
            else
                uri += "/assets/";

            string newID = null;
            try
            {
                newID = SynchronousRestObjectRequester.MakeRequest<AssetBase, string>("POST", uri, asset, 30000, auth);
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
                    asset.ID = newID;
                    if (m_Cache != null)
                        m_Cache.Cache(asset);
                }
            }

            return asset.ID;
        }

        public void retryStore(AssetBase asset, int nextRetryLevel)
        {
            string uri = MapServer(asset.FullID.ToString()) + "/assets/";

            string newID = null;
            try
            {
                newID = SynchronousRestObjectRequester.MakeRequest<AssetBase, string>("POST", uri, asset, 100000, m_Auth);
            }
            catch
            {
                newID = null;
            }

            if (string.IsNullOrEmpty(newID) || newID == stringUUIDZero)
            {
                if (nextRetryLevel >= MAXSENDRETRIESLEN)
                    m_log.WarnFormat("[Assets] Giving up on uploading after {2} retries id: {0} type {1}",
                            asset.ID.ToString(), asset.Type.ToString(), MAXSENDRETRIESLEN);
                else
                {
                    lock (m_sendRetries)
                    {
                        if (m_sendRetries[nextRetryLevel] == null)
                            m_sendRetries[nextRetryLevel] = new List<AssetBase>();

                        List<AssetBase> m_queue = m_sendRetries[nextRetryLevel];
                        m_queue.Add(asset);
                        m_log.WarnFormat("[Assets] Upload failed: {0} type {1} will retry later",
                                asset.ID.ToString(), asset.Type.ToString());
                    }
                }
            }
            else
            {
                m_log.InfoFormat("[Assets] Upload of {0} succeeded after {1} failed attempts", asset.ID.ToString(), nextRetryLevel.ToString());
                if (newID != asset.ID)
                {
                    asset.ID = newID;
                    m_Cache?.Cache(asset);
                }
            }
        }

        public void retryCachedStore(string assetID, int nextRetryLevel)
        {
            m_Cache.Get(assetID,out AssetBase asset);
            if(asset == null)
            {
                m_log.WarnFormat("[Assets] asset not in cache on uploading after {2} retries id: {0}",
                        assetID, MAXSENDRETRIESLEN);
            }

            string uri = MapServer(asset.FullID.ToString()) + "/assets/";

            string newID = null;
            try
            {
                newID = SynchronousRestObjectRequester.
                        MakeRequest<AssetBase, string>("POST", uri, asset, 100000, m_Auth);
            }
            catch
            {
                newID = null;
            }

            if (string.IsNullOrEmpty(newID) || newID == stringUUIDZero)
            {
                if (nextRetryLevel >= MAXSENDRETRIESLEN)
                    m_log.WarnFormat("[Assets] Giving up on uploading after {2} retries id: {0} type {1}",
                            asset.ID.ToString(), asset.Type.ToString(), MAXSENDRETRIESLEN);
                else
                {
                    lock (m_sendRetries)
                    {
                        if (m_sendCachedRetries[nextRetryLevel] == null)
                            m_sendCachedRetries[nextRetryLevel] = new List<string>();

                        m_sendCachedRetries[nextRetryLevel].Add(assetID);
                        m_log.WarnFormat("[Assets] Upload failed: {0} type {1} will retry later",
                                asset.ID.ToString(), asset.Type.ToString());
                    }
                }
            }
            else
            {
                m_log.InfoFormat("[Assets] Upload of {0} succeeded after {1} failed attempts", asset.ID.ToString(), nextRetryLevel.ToString());
                if (newID != asset.ID)
                {
                    asset.ID = newID;
                    m_Cache?.Cache(asset);
                }
            }
        }

        public virtual bool UpdateContent(string id, byte[] data)
        {
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

            string uri = MapServer(id) + "/assets/" + id;

            if (SynchronousRestObjectRequester.MakeRequest<AssetBase, bool>("POST", uri, asset, m_Auth))
            {
                m_Cache?.Cache(asset, true);
                return true;
            }
            return false;
        }

        public virtual bool Delete(string id)
        {
            string uri = MapServer(id) + "/assets/" + id;

            if (SynchronousRestObjectRequester.MakeRequest<int, bool>("DELETE", uri, 0, m_Auth))
            {
                if (m_Cache != null)
                    m_Cache.Expire(id);

                return true;
            }
            return false;
        }
    }
}
