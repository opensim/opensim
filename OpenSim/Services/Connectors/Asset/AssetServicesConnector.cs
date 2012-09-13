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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

        private delegate void AssetRetrievedEx(AssetBase asset);

        // Keeps track of concurrent requests for the same asset, so that it's only loaded once.
        // Maps: Asset ID -> Handlers which will be called when the asset has been loaded
        private Dictionary<string, AssetRetrievedEx> m_AssetHandlers = new Dictionary<string, AssetRetrievedEx>();


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

            if (serviceURI == String.Empty)
            {
                m_log.Error("[ASSET CONNECTOR]: No Server URI named in section AssetService");
                throw new Exception("Asset connector init error");
            }

            m_ServerURI = serviceURI;
        }

        protected void SetCache(IImprovedAssetCache cache)
        {
            m_Cache = cache;
        }

        public AssetBase Get(string id)
        {
//            m_log.DebugFormat("[ASSET SERVICE CONNECTOR]: Synchronous get request for {0}", id);

            string uri = m_ServerURI + "/assets/" + id;

            AssetBase asset = null;
            if (m_Cache != null)
                asset = m_Cache.Get(id);

            if (asset == null)
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

            string uri = m_ServerURI + "/assets/" + id + "/metadata";

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

            RestClient rc = new RestClient(m_ServerURI);
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

        public bool Get(string id, Object sender, AssetRetrieved handler)
        {
//            m_log.DebugFormat("[ASSET SERVICE CONNECTOR]: Potentially asynchronous get request for {0}", id);

            string uri = m_ServerURI + "/assets/" + id;

            AssetBase asset = null;
            if (m_Cache != null)
                asset = m_Cache.Get(id);

            if (asset == null)
            {
                lock (m_AssetHandlers)
                {
                    AssetRetrievedEx handlerEx = new AssetRetrievedEx(delegate(AssetBase _asset) { handler(id, sender, _asset); });

                    AssetRetrievedEx handlers;
                    if (m_AssetHandlers.TryGetValue(id, out handlers))
                    {
                        // Someone else is already loading this asset. It will notify our handler when done.
                        handlers += handlerEx;
                        return true;
                    }

                    // Load the asset ourselves
                    handlers += handlerEx;
                    m_AssetHandlers.Add(id, handlers);
                }

                bool success = false;
                try
                {
                    AsynchronousRestObjectRequester.MakeRequest<int, AssetBase>("GET", uri, 0,
                        delegate(AssetBase a)
                        {
                            if (m_Cache != null)
                                m_Cache.Cache(a);

                            AssetRetrievedEx handlers;
                            lock (m_AssetHandlers)
                            {
                                handlers = m_AssetHandlers[id];
                                m_AssetHandlers.Remove(id);
                            }
                            handlers.Invoke(a);
                        }, 30);
                    
                    success = true;
                }
                finally
                {
                    if (!success)
                    {
                        lock (m_AssetHandlers)
                        {
                            m_AssetHandlers.Remove(id);
                        }
                    }
                }
            }
            else
            {
                handler(id, sender, asset);
            }

            return true;
        }

        public string Store(AssetBase asset)
        {
            if (asset.Temporary || asset.Local)
            {
                if (m_Cache != null)
                    m_Cache.Cache(asset);

                return asset.ID;
            }

            string uri = m_ServerURI + "/assets/";

            string newID = string.Empty;
            try
            {
                newID = SynchronousRestObjectRequester.
                        MakeRequest<AssetBase, string>("POST", uri, asset);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[ASSET CONNECTOR]: Unable to send asset {0} to asset server. Reason: {1}", asset.ID, e.Message);
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
            return newID;
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

            string uri = m_ServerURI + "/assets/" + id;

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
            string uri = m_ServerURI + "/assets/" + id;

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
