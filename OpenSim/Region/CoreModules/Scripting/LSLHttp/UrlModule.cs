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
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Scripting.LSLHttp
{
    public class UrlData
    {
        public UUID hostID;
        public UUID itemID;
        public IScriptModule engine;
        public string url;
        public UUID urlcode;
        public Dictionary<UUID, RequestData> requests;
    }

    public class RequestData
    {
        public UUID requestID;
        public Dictionary<string, string> headers;
        public string body;
        public ManualResetEvent ev;
    }

    public class UrlModule : ISharedRegionModule, IUrlModule
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<UUID, UrlData> m_RequestMap =
                new Dictionary<UUID, UrlData>();

        private Dictionary<string, UrlData> m_UrlMap =
                new Dictionary<string, UrlData>();

        private int m_TotalUrls = 100;

        private IHttpServer m_HttpServer = null;

        public string Name
        {
            get { return "UrlModule"; }
        }

        public void Initialise(IConfigSource config)
        {
        }

        public void PostInitialise()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (m_HttpServer == null)
            {
                // There can only be one
                //
                m_HttpServer = scene.CommsManager.HttpServer;
            }

            scene.RegisterModuleInterface<IUrlModule>(this);

            scene.EventManager.OnScriptReset += OnScriptReset;
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void Close()
        {
        }

        public UUID RequestURL(IScriptModule engine, SceneObjectPart host, UUID itemID)
        {
            UUID urlcode = UUID.Random();

            lock (m_UrlMap)
            {
                if (m_UrlMap.Count >= m_TotalUrls)
                {
                    engine.PostScriptEvent(itemID, "http_request", new Object[] { urlcode.ToString(), "URL_REQUEST_DENIED", "" });
                    return urlcode;
                }
                string url = "http://"+System.Environment.MachineName+":"+m_HttpServer.Port.ToString()+"/lslhttp/"+urlcode.ToString()+"/";

                UrlData urlData = new UrlData();
                urlData.hostID = host.UUID;
                urlData.itemID = itemID;
                urlData.engine = engine;
                urlData.url = url;
                urlData.urlcode = urlcode;
                urlData.requests = new Dictionary<UUID, RequestData>();

                m_UrlMap[url] = urlData;

                m_HttpServer.AddHTTPHandler("/lslhttp/"+urlcode.ToString()+"/", HttpRequestHandler);

                engine.PostScriptEvent(itemID, "http_request", new Object[] { urlcode.ToString(), "URL_REQUEST_GRANTED", url });
            }

            return urlcode;
        }

        public UUID RequestSecureURL(IScriptModule engine, SceneObjectPart host, UUID itemID)
        {
            UUID urlcode = UUID.Random();

            engine.PostScriptEvent(itemID, "http_request", new Object[] { urlcode.ToString(), "URL_REQUEST_DENIED", "" });

            return urlcode;
        }

        public void ReleaseURL(string url)
        {
            lock (m_UrlMap)
            {
                UrlData data;

                if (!m_UrlMap.TryGetValue(url, out data))
                    return;

                foreach (UUID req in data.requests.Keys)
                    m_RequestMap.Remove(req);

                RemoveUrl(data);
                m_UrlMap.Remove(url);
            }
        }

        public void HttpResponse(UUID request, int status, string body)
        {
        }

        public string GetHttpHeader(UUID request, string header)
        {
            return String.Empty;
        }

        public int GetFreeUrls()
        {
            return m_TotalUrls - m_UrlMap.Count;
        }

        public void ScriptRemoved(UUID itemID)
        {
            lock (m_UrlMap)
            {
                List<string> removeURLs = new List<string>();

                foreach (KeyValuePair<string, UrlData> url in m_UrlMap)
                {
                    if (url.Value.itemID == itemID)
                    {
                        RemoveUrl(url.Value);
                        removeURLs.Add(url.Key);
                        foreach (UUID req in url.Value.requests.Keys)
                            m_RequestMap.Remove(req);
                    }
                }

                foreach (string urlname in removeURLs)
                    m_UrlMap.Remove(urlname);
            }
        }

        public void ObjectRemoved(UUID objectID)
        {
            lock (m_UrlMap)
            {
                List<string> removeURLs = new List<string>();

                foreach (KeyValuePair<string, UrlData> url in m_UrlMap)
                {
                    if (url.Value.hostID == objectID)
                    {
                        RemoveUrl(url.Value);
                        removeURLs.Add(url.Key);
                        foreach (UUID req in url.Value.requests.Keys)
                            m_RequestMap.Remove(req);
                    }
                }

                foreach (string urlname in removeURLs)
                    m_UrlMap.Remove(urlname);
            }
        }

        private void RemoveUrl(UrlData data)
        {
            m_HttpServer.RemoveHTTPHandler("", "/lslhttp/"+data.urlcode.ToString()+"/");
        }

        private Hashtable HttpRequestHandler(Hashtable request)
        {
            foreach (KeyValuePair<string, Object> kvp in request)
            {
                m_log.DebugFormat("{0} = {1}", kvp.Key, kvp.Value.ToString());
            }
            Hashtable response = new Hashtable();
            response["int_response_code"] = 404;
            response["str_response_string"] = "Test";

            return response;
        }

        private void OnScriptReset(uint localID, UUID itemID)
        {
            ScriptRemoved(itemID);
        }
    }
}
