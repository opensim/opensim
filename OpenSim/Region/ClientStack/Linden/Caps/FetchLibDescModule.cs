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
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;
using OpenSim.Capabilities.Handlers;
using OpenSim.Framework.Monitoring;

using OpenMetaverse.StructuredData;

namespace OpenSim.Region.ClientStack.Linden
{
    /// <summary>
    /// This module implements both WebFetchInventoryDescendents and FetchInventoryDescendents2 capabilities.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "FetchLibDescModule")]
    public class FetchLibDescModule : INonSharedRegionModule
    {
        class APollRequest
        {
            public PollServiceInventoryEventArgs thepoll;
            public UUID reqID;
            public OSHttpRequest request;
        }

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Control whether requests will be processed asynchronously.
        /// </summary>
        /// <remarks>
        /// Defaults to true.  Can currently not be changed once a region has been added to the module.
        /// </remarks>
        public bool ProcessQueuedRequestsAsync { get; private set; }

        /// <summary>
        /// Number of inventory requests processed by this module.
        /// </summary>
        /// <remarks>
        /// It's the PollServiceRequestManager that actually sends completed requests back to the requester.
        /// </remarks>
        public static int ProcessedRequestsCount { get; set; }

        public Scene Scene { get; private set; }

        private ILibraryService m_LibraryService;

        private bool m_Enabled;
        private ExpiringKey<UUID> m_badRequests;

        private string m_fetchLibDescendents2Url;

        private static FetchLibDescHandler m_FetchHandler;

        private static ObjectJobEngine m_workerpool = null;

        #region ISharedRegionModule Members

        public FetchLibDescModule() : this(true) {}

        public FetchLibDescModule(bool processQueuedResultsAsync)
        {
            ProcessQueuedRequestsAsync = processQueuedResultsAsync;
        }

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["ClientStack.LindenCaps"];
            if (config == null)
                return;

            m_fetchLibDescendents2Url = config.GetString("Cap_FetchLibDescendents2", string.Empty);
            m_Enabled = m_fetchLibDescendents2Url.Length > 0;
        }

        public void AddRegion(Scene s)
        {
            if (!m_Enabled)
                return;

            Scene = s;
        }

        public void RemoveRegion(Scene s)
        {
            if (!m_Enabled)
                return;

            Scene.EventManager.OnRegisterCaps -= RegisterCaps;
            Scene = null;
        }

        public void RegionLoaded(Scene s)
        {
            if (!m_Enabled)
                return;

            m_LibraryService = Scene.LibraryService;

            // We'll reuse the same handler for all requests.
            m_FetchHandler = new FetchLibDescHandler(m_LibraryService, Scene);

            Scene.EventManager.OnRegisterCaps += RegisterCaps;

            if(m_badRequests == null)
                m_badRequests = new ExpiringKey<UUID>(30000);

            if (ProcessQueuedRequestsAsync && m_workerpool == null)
                m_workerpool = new ObjectJobEngine(DoInventoryRequests, "LibInventoryWorker", 2000, 2);
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
            if (!m_Enabled)
                return;

            if (ProcessQueuedRequestsAsync)
            {
                if (m_workerpool != null)
                {
                    m_workerpool.Dispose();
                    m_workerpool = null;
                    m_badRequests.Dispose();
                    m_badRequests = null;
                }
            }
            //m_queue.Dispose();
        }

        public string Name { get { return "FetchLibDescModule"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        private class PollServiceInventoryEventArgs : PollServiceEventArgs
        {
            private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            private Dictionary<UUID, Hashtable> responses = new Dictionary<UUID, Hashtable>();
            private HashSet<UUID> dropedResponses = new HashSet<UUID>();

            private FetchLibDescModule m_module;

            public PollServiceInventoryEventArgs(FetchLibDescModule module, string url, UUID pId) :
                base(null, url, null, null, null, null, pId, int.MaxValue)
            {
                m_module = module;

                HasEvents = (requestID, y) =>
                {
                    lock (responses)
                        return responses.ContainsKey(requestID);
                };

                Drop = (requestID, y) =>
                {
                    lock (responses)
                    {
                        responses.Remove(requestID);
                        lock(dropedResponses)
                            dropedResponses.Add(requestID);
                    }
                };

                GetEvents = (requestID, y) =>
                {
                    lock (responses)
                    {
                        try
                        {
                            return responses[requestID];
                        }
                        finally
                        {
                            responses.Remove(requestID);
                        }
                    }
                };

                Request = (requestID, request) =>
                {
                    APollRequest reqinfo = new APollRequest();
                    reqinfo.thepoll = this;
                    reqinfo.reqID = requestID;
                    reqinfo.request = request;
                    m_workerpool.Enqueue(reqinfo);
                    return null;
                };

                NoEvents = (x, y) =>
                {
                    Hashtable response = new Hashtable();
                    response["int_response_code"] = 500;
                    response["str_response_string"] = "Script timeout";
                    response["content_type"] = "text/plain";
                    response["keepalive"] = false;

                    return response;
                };
            }

            public void Process(APollRequest requestinfo)
            {
                if(m_module == null || m_module.Scene == null || m_module.Scene.ShuttingDown)
                    return;

                UUID requestID = requestinfo.reqID;

                lock(responses)
                {
                    lock(dropedResponses)
                    {
                        if(dropedResponses.Contains(requestID))
                        {
                            dropedResponses.Remove(requestID);
                            return;
                        }
                    }
                }

                OSHttpResponse osresponse = new OSHttpResponse(requestinfo.request);
                m_FetchHandler.FetchRequest(requestinfo.request, osresponse, m_module.m_badRequests, requestinfo.thepoll.Id);
                requestinfo.request.InputStream.Dispose();

                lock (responses)
                {
                    lock(dropedResponses)
                    {
                        if(dropedResponses.Contains(requestID))
                        {
                            dropedResponses.Remove(requestID);
                            ProcessedRequestsCount++;
                            return;
                        }
                    }

                    Hashtable response = new Hashtable();
                    response["h"] = osresponse;
                    responses[requestID] = response;
                }
                ProcessedRequestsCount++;
            }
        }

        private void RegisterCaps(UUID agentID, Caps caps)
        {
            RegisterFetchLibDescendentsCap(agentID, caps, "FetchLibDescendents2", m_fetchLibDescendents2Url);
        }

        private void RegisterFetchLibDescendentsCap(UUID agentID, Caps caps, string capName, string url)
        {
            string capUrl;

            // handled by the simulator
            if (url == "localhost")
            {
                capUrl = "/" + UUID.Random();

                // Register this as a poll service
                PollServiceInventoryEventArgs args = new PollServiceInventoryEventArgs(this, capUrl, agentID);
                //args.Type = PollServiceEventArgs.EventType.Inventory;

                caps.RegisterPollHandler(capName, args);
            }
            // external handler
            else
            {
                capUrl = url;
                IExternalCapsModule handler = Scene.RequestModuleInterface<IExternalCapsModule>();
                if (handler != null)
                    handler.RegisterExternalUserCapsHandler(agentID, caps, capName, capUrl);
                else
                    caps.RegisterHandler(capName, capUrl);
            }
        }

        private static void DoInventoryRequests(object o)
        {
            APollRequest poolreq = o as APollRequest;
            if (poolreq != null && poolreq.thepoll != null)
                poolreq.thepoll.Process(poolreq);
        }
    }
}
