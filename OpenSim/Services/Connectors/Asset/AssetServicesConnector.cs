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
using System.IO;
using System.Reflection;
using System.Timers;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Communications;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Services.Connectors
{
    public class AssetServicesConnector : IAssetService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;
        private IImprovedAssetCache m_Cache = null;
        private int m_retryCounter;
        private Dictionary<int, List<AssetBase>> m_retryQueue = new Dictionary<int, List<AssetBase>>();
        private System.Timers.Timer m_retryTimer;
        private delegate void AssetRetrievedEx(AssetBase asset);

        // Keeps track of concurrent requests for the same asset, so that it's only loaded once.
        // Maps: Asset ID -> Handlers which will be called when the asset has been loaded
//        private Dictionary<string, AssetRetrievedEx> m_AssetHandlers = new Dictionary<string, AssetRetrievedEx>();

        private Dictionary<string, List<AssetRetrievedEx>> m_AssetHandlers = new Dictionary<string, List<AssetRetrievedEx>>();

        private Dictionary<string, string> m_UriMap = new Dictionary<string, string>();

        private Thread[] m_fetchThreads;

        public AssetServicesConnector()
        {
        }

        public AssetServicesConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/');
        }

        public AssetServicesConnector(IConfigSource source)
        {
            Initialise(source);
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig assetConfig = source.Configs["AssetService"];
            if (assetConfig == null)
            {
                m_log.Error("[ASSET CONNECTOR]: AssetService missing from OpenSim.ini");
                throw new Exception("Asset connector init error");
            }

            string serviceURI = assetConfig.GetString("AssetServerURI",
                    String.Empty);

            m_ServerURI = serviceURI;

            if (serviceURI == String.Empty)
            {
                m_log.Error("[ASSET CONNECTOR]: No Server URI named in section AssetService");
                throw new Exception("Asset connector init error");
            }


            m_retryTimer = new System.Timers.Timer();
            m_retryTimer.Elapsed += new ElapsedEventHandler(retryCheck);
            m_retryTimer.Interval = 60000;

            Uri serverUri = new Uri(m_ServerURI);

            string groupHost = serverUri.Host;

            for (int i = 0 ; i < 256 ; i++)
            {
                string prefix = i.ToString("x2");
                groupHost = assetConfig.GetString("AssetServerHost_"+prefix, groupHost);

                m_UriMap[prefix] = groupHost;
                //m_log.DebugFormat("[ASSET]: Using {0} for prefix {1}", groupHost, prefix);
            }

            m_fetchThreads = new Thread[2];

            for (int i = 0 ; i < 2 ; i++)
            {
                m_fetchThreads[i] = new Thread(AssetRequestProcessor);
                m_fetchThreads[i].Start();
            }
        }

        private string MapServer(string id)
        {
            UriBuilder serverUri = new UriBuilder(m_ServerURI);

            string prefix = id.Substring(0, 2).ToLower();

            string host = m_UriMap[prefix];

            serverUri.Host = host;

            // m_log.DebugFormat("[ASSET]: Using {0} for host name for prefix {1}", host, prefix);

            string ret = serverUri.Uri.AbsoluteUri;
            if (ret.EndsWith("/"))
                ret = ret.Substring(0, ret.Length - 1);
            return ret;
        }

        protected void retryCheck(object source, ElapsedEventArgs e)
        {
            m_retryCounter++;
            if (m_retryCounter > 60) m_retryCounter -= 60;
            List<int> keys = new List<int>();
            foreach (int a in m_retryQueue.Keys)
            {
                keys.Add(a);
            }
            foreach (int a in keys)
            {
                //We exponentially fall back on frequency until we reach one attempt per hour
                //The net result is that we end up in the queue for roughly 24 hours..
                //24 hours worth of assets could be a lot, so the hope is that the region admin
                //will have gotten the asset connector back online quickly!

                int timefactor = a ^ 2; 
                if (timefactor > 60)
                {
                    timefactor = 60;
                }

                //First, find out if we care about this timefactor
                if (timefactor % a == 0)
                {
                    //Yes, we do!   
                    List<AssetBase> retrylist = m_retryQueue[a];
                    m_retryQueue.Remove(a);

                    foreach(AssetBase ass in retrylist)
                    {
                        Store(ass); //Store my ass. This function will put it back in the dictionary if it fails
                    }
                }
            }

            if (m_retryQueue.Count == 0)
            {
                //It might only be one tick per minute, but I have
                //repented and abandoned my wasteful ways
                m_retryCounter = 0;
                m_retryTimer.Stop();
            }
        }

        protected void SetCache(IImprovedAssetCache cache)
        {
            m_Cache = cache;
        }

        public AssetBase Get(string id)
        {
            string uri = MapServer(id) + "/assets/" + id;

            AssetBase asset = null;
            if (m_Cache != null)
                asset = m_Cache.Get(id);
            
            if (asset == null || asset.Data == null || asset.Data.Length == 0)
            {
                asset = SynchronousRestObjectRequester.
                        MakeRequest<int, AssetBase>("GET", uri, 0, 30);

                if (m_Cache != null)
                    m_Cache.Cache(asset);
            }
            return asset;
        }

        public AssetBase GetCached(string id)
        {
//            m_log.DebugFormat("[ASSET SERVICE CONNECTOR]: Cache request for {0}", id);

            if (m_Cache != null)
                return m_Cache.Get(id);

            return null;
        }

        public AssetMetadata GetMetadata(string id)
        {
            if (m_Cache != null)
            {
                AssetBase fullAsset = m_Cache.Get(id);

                if (fullAsset != null)
                    return fullAsset.Metadata;
            }

            string uri = MapServer(id) + "/assets/" + id + "/metadata";

            AssetMetadata asset = SynchronousRestObjectRequester.
                    MakeRequest<int, AssetMetadata>("GET", uri, 0);
            return asset;
        }

        public byte[] GetData(string id)
        {
            if (m_Cache != null)
            {
                AssetBase fullAsset = m_Cache.Get(id);

                if (fullAsset != null)
                    return fullAsset.Data;
            }

            RestClient rc = new RestClient(MapServer(id));
            rc.AddResourcePath("assets");
            rc.AddResourcePath(id);
            rc.AddResourcePath("data");

            rc.RequestMethod = "GET";

            Stream s = rc.Request();

            if (s == null)
                return null;

            if (s.Length > 0)
            {
                byte[] ret = new byte[s.Length];
                s.Read(ret, 0, (int)s.Length);

                return ret;
            }

            return null;
        }

        private class QueuedAssetRequest
        {
            public string uri;
            public string id;
        }

        private OpenMetaverse.BlockingQueue<QueuedAssetRequest> m_requestQueue =
                new OpenMetaverse.BlockingQueue<QueuedAssetRequest>();

        private void AssetRequestProcessor()
        {
            QueuedAssetRequest r;

            while (true)
            {
                r = m_requestQueue.Dequeue();

                string uri = r.uri;
                string id = r.id;

                bool success = false;
                try
                {
                    AssetBase a = SynchronousRestObjectRequester.MakeRequest<int, AssetBase>("GET", uri, 0, 30);
                    if (a != null)
                    {
                        if (m_Cache != null)
                            m_Cache.Cache(a);

                        List<AssetRetrievedEx> handlers;
                        lock (m_AssetHandlers)
                        {
                            handlers = m_AssetHandlers[id];
                            m_AssetHandlers.Remove(id);
                        }
                        foreach (AssetRetrievedEx h in handlers)
                        {
                            Util.FireAndForget(x =>
                            {
                                h.Invoke(a);
                            });
                        }
                        if (handlers != null)
                            handlers.Clear();
                    
                        success = true;
                    }
                }
                finally
                {
                    if (!success)
                    {
                        List<AssetRetrievedEx> handlers;
                        lock (m_AssetHandlers)
                        {
                            handlers = m_AssetHandlers[id];
                            m_AssetHandlers.Remove(id);
                        }
                        if (handlers != null)
                            handlers.Clear();
                    }
                }
            }
        }

        public bool Get(string id, Object sender, AssetRetrieved handler)
        {
            string uri = MapServer(id) + "/assets/" + id;

            AssetBase asset = null;
            if (m_Cache != null)
                asset = m_Cache.Get(id);

            if (asset == null || asset.Data == null || asset.Data.Length == 0)
            {
                lock (m_AssetHandlers)
                {
                    AssetRetrievedEx handlerEx = new AssetRetrievedEx(delegate(AssetBase _asset) { handler(id, sender, _asset); });

//                    AssetRetrievedEx handlers;
                    List<AssetRetrievedEx> handlers;
                    if (m_AssetHandlers.TryGetValue(id, out handlers))
                    {
                        // Someone else is already loading this asset. It will notify our handler when done.
//                        handlers += handlerEx;
                        handlers.Add(handlerEx);
                        return true;
                    }

                    // Load the asset ourselves
//                    handlers += handlerEx;
                    handlers = new List<AssetRetrievedEx>();
                    handlers.Add(handlerEx);

                    m_AssetHandlers.Add(id, handlers);
                }

                QueuedAssetRequest request = new QueuedAssetRequest();
                request.id = id;
                request.uri = uri;

                m_requestQueue.Enqueue(request);
            }
            else
            {
                handler(id, sender, asset);
            }

            return true;
        }

        public string Store(AssetBase asset)
        {
            // Have to assign the asset ID here. This isn't likely to
            // trigger since current callers don't pass emtpy IDs
            // We need the asset ID to route the request to the proper
            // cluster member, so we can't have the server assign one.
            if (asset.ID == string.Empty)
            {
                if (asset.FullID == UUID.Zero)
                {
                    asset.FullID = UUID.Random();
                }
                asset.ID = asset.FullID.ToString();
            }
            else if (asset.FullID == UUID.Zero)
            {
                UUID uuid = UUID.Zero;
                if (UUID.TryParse(asset.ID, out uuid))
                {
                    asset.FullID = uuid;
                }
                else
                {
                    asset.FullID = UUID.Random();
                }
            }

            if (m_Cache != null)
                m_Cache.Cache(asset);
            if (asset.Temporary || asset.Local)
            {
                return asset.ID;
            }

            string uri = MapServer(asset.FullID.ToString()) + "/assets/";

            string newID = string.Empty;
            try
            {
                newID = SynchronousRestObjectRequester.
                        MakeRequest<AssetBase, string>("POST", uri, asset, 25);
                if (newID == null || newID == "")
                {
                    newID = UUID.Zero.ToString();
                }
            }
            catch (Exception e)
            {
                newID = UUID.Zero.ToString();
            }

            if (newID == UUID.Zero.ToString())
            {
                //The asset upload failed, put it in a queue for later
                asset.UploadAttempts++;
                if (asset.UploadAttempts > 30)
                {
                    //By this stage we've been in the queue for a good few hours;
                    //We're going to drop the asset.
                    m_log.ErrorFormat("[Assets] Dropping asset {0} - Upload has been in the queue for too long.", asset.ID.ToString());
                }
                else
                {
                    if (!m_retryQueue.ContainsKey(asset.UploadAttempts))
                    {
                        m_retryQueue.Add(asset.UploadAttempts, new List<AssetBase>());
                    }
                    List<AssetBase> m_queue = m_retryQueue[asset.UploadAttempts];
                    m_queue.Add(asset);
                    m_log.WarnFormat("[Assets] Upload failed: {0} - Requeuing asset for another run.", asset.ID.ToString());
                    m_retryTimer.Start();
                }
            }
            else
            {
                if (asset.UploadAttempts > 0)
                {
                    m_log.InfoFormat("[Assets] Upload of {0} succeeded after {1} failed attempts", asset.ID.ToString(), asset.UploadAttempts.ToString());
                }
                if (newID != String.Empty)
                {
                    // Placing this here, so that this work with old asset servers that don't send any reply back
                    // SynchronousRestObjectRequester returns somethins that is not an empty string
                    if (newID != null)
                        asset.ID = newID;

                    if (m_Cache != null)
                        m_Cache.Cache(asset);
                }
            }
            return asset.ID;
        }

        public bool UpdateContent(string id, byte[] data)
        {
            AssetBase asset = null;

            if (m_Cache != null)
                asset = m_Cache.Get(id);

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

            if (SynchronousRestObjectRequester.
                    MakeRequest<AssetBase, bool>("POST", uri, asset))
            {
                if (m_Cache != null)
                    m_Cache.Cache(asset);

                return true;
            }
            return false;
        }

        public bool Delete(string id)
        {
            string uri = MapServer(id) + "/assets/" + id;

            if (SynchronousRestObjectRequester.
                    MakeRequest<int, bool>("DELETE", uri, 0))
            {
                if (m_Cache != null)
                    m_Cache.Expire(id);

                return true;
            }
            return false;
        }
    }
}
