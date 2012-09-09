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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;
using OpenSim.Capabilities.Handlers;

namespace OpenSim.Region.ClientStack.Linden
{
    /// <summary>
    /// This module implements both WebFetchInventoryDescendents and FetchInventoryDescendents2 capabilities.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class WebFetchInvDescModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;

        private IInventoryService m_InventoryService;
        private ILibraryService m_LibraryService;

        private WebFetchInvDescHandler m_webFetchHandler;

        private object m_lock = new object();

        private Dictionary<UUID, string> m_capsDict = new Dictionary<UUID, string>();
        private Dictionary<UUID, Hashtable> m_requests = new Dictionary<UUID, Hashtable>();
        bool m_busy = false;

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(Scene s)
        {
            m_scene = s;
        }

        public void RemoveRegion(Scene s)
        {
            m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
            m_scene.EventManager.OnDeregisterCaps -= DeregisterCaps;
            m_scene = null;
        }

        public void RegionLoaded(Scene s)
        {
            m_InventoryService = m_scene.InventoryService;
            m_LibraryService = m_scene.LibraryService;

            // We'll reuse the same handler for all requests.
            m_webFetchHandler = new WebFetchInvDescHandler(m_InventoryService, m_LibraryService);

            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
            m_scene.EventManager.OnDeregisterCaps += DeregisterCaps;
        }

        public void PostInitialise()
        {
        }

        public void Close() { }

        public string Name { get { return "WebFetchInvDescModule"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        private void RegisterCaps(UUID agentID, Caps caps)
        {
            string capUrl = "/CAPS/" + UUID.Random() + "/";

            // Register this as a poll service
            // absurd large timeout to tune later to make a bit less than viewer
            PollServiceEventArgs args = new PollServiceEventArgs(HttpRequestHandler, HasEvents, GetEvents, NoEvents, agentID, 300000);
            
            args.Type = PollServiceEventArgs.EventType.Inventory;
            MainServer.Instance.AddPollServiceHTTPHandler(capUrl, args);

            string hostName = m_scene.RegionInfo.ExternalHostName;
            uint port = (MainServer.Instance == null) ? 0 : MainServer.Instance.Port;
            string protocol = "http";
            
            if (MainServer.Instance.UseSSL)
            {
                hostName = MainServer.Instance.SSLCommonName;
                port = MainServer.Instance.SSLPort;
                protocol = "https";
            }
            caps.RegisterHandler("FetchInventoryDescendents2", String.Format("{0}://{1}:{2}{3}", protocol, hostName, port, capUrl));

            m_capsDict[agentID] = capUrl;

            m_busy = false;
        }

        private void DeregisterCaps(UUID agentID, Caps caps)
        {
            string capUrl;

            if (m_capsDict.TryGetValue(agentID, out capUrl))
            {
                MainServer.Instance.RemoveHTTPHandler("", capUrl);
                m_capsDict.Remove(agentID);
            }
        }

        public void HttpRequestHandler(UUID requestID, Hashtable request)
        {
//            m_log.DebugFormat("[FETCH2]: Received request {0}", requestID);
            lock(m_lock)
                m_requests[requestID] = request;
        }

        private bool HasEvents(UUID requestID, UUID sessionID)
        {
            lock (m_lock)
            {
                return !m_busy;
            }
        }

        private Hashtable NoEvents(UUID requestID, UUID sessionID)
        {
            lock(m_lock)
                m_requests.Remove(requestID);

            Hashtable response = new Hashtable();

            response["int_response_code"] = 500;
            response["str_response_string"] = "Script timeout";
            response["content_type"] = "text/plain";
            response["keepalive"] = false;
            response["reusecontext"] = false;

            lock (m_lock)
                m_busy = false;

            return response;
        }

        private Hashtable GetEvents(UUID requestID, UUID sessionID, string request)
        {
            lock (m_lock)
                m_busy = true;

            Hashtable response = new Hashtable();

            response["int_response_code"] = 500;
            response["str_response_string"] = "Internal error";
            response["content_type"] = "text/plain";
            response["keepalive"] = false;
            response["reusecontext"] = false;

            try
            {

                Hashtable requestHash;
                lock (m_lock)
                {
                    if (!m_requests.TryGetValue(requestID, out requestHash))
                    {
                        m_busy = false;
                        response["str_response_string"] = "Invalid request";
                        return response;
                    }
                    m_requests.Remove(requestID);
                }

//                m_log.DebugFormat("[FETCH2]: Processed request {0}", requestID);

                string reply = m_webFetchHandler.FetchInventoryDescendentsRequest(requestHash["body"].ToString(), String.Empty, String.Empty, null, null);
               

                response["int_response_code"] = 200;
                response["str_response_string"] = reply;
            }
            finally
            {
                lock (m_lock)
                    m_busy = false;
            }

            return response;
        }
    }
}
