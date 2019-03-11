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
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework.Capabilities;
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
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "WebFetchInvDescModule")]
    public class WebFetchInvDescModule : INonSharedRegionModule
    {
        class APollRequest
        {
            public PollServiceInventoryEventArgs thepoll;
            public UUID reqID;
            public Hashtable request;
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

        private static Stat s_queuedRequestsStat;
        private static Stat s_processedRequestsStat;

        public Scene Scene { get; private set; }

        private IInventoryService m_InventoryService;
        private ILibraryService m_LibraryService;

        private bool m_Enabled;

        private string m_fetchInventoryDescendents2Url;
//        private string m_webFetchInventoryDescendentsUrl;

        private static FetchInvDescHandler m_webFetchHandler;

        private static Thread[] m_workerThreads = null;

        private static BlockingCollection<APollRequest> m_queue = new BlockingCollection<APollRequest>();

        private static int m_NumberScenes = 0;

        #region ISharedRegionModule Members

        public WebFetchInvDescModule() : this(true) {}

        public WebFetchInvDescModule(bool processQueuedResultsAsync)
        {
            ProcessQueuedRequestsAsync = processQueuedResultsAsync;
        }

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["ClientStack.LindenCaps"];
            if (config == null)
                return;

            m_fetchInventoryDescendents2Url = config.GetString("Cap_FetchInventoryDescendents2", string.Empty);
//            m_webFetchInventoryDescendentsUrl = config.GetString("Cap_WebFetchInventoryDescendents", string.Empty);

//            if (m_fetchInventoryDescendents2Url != string.Empty || m_webFetchInventoryDescendentsUrl != string.Empty)
            if (m_fetchInventoryDescendents2Url != string.Empty)
            {
                m_Enabled = true;
            }
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

            StatsManager.DeregisterStat(s_processedRequestsStat);
            StatsManager.DeregisterStat(s_queuedRequestsStat);

            m_NumberScenes--;
            Scene = null;
        }

        public void RegionLoaded(Scene s)
        {
            if (!m_Enabled)
                return;

            if (s_processedRequestsStat == null)
                s_processedRequestsStat =
                    new Stat(
                        "ProcessedFetchInventoryRequests",
                        "Number of processed fetch inventory requests",
                        "These have not necessarily yet been dispatched back to the requester.",
                        "",
                        "inventory",
                        "httpfetch",
                        StatType.Pull,
                        MeasuresOfInterest.AverageChangeOverTime,
                        stat => { stat.Value = ProcessedRequestsCount; },
                        StatVerbosity.Debug);

            if (s_queuedRequestsStat == null)
                s_queuedRequestsStat =
                    new Stat(
                        "QueuedFetchInventoryRequests",
                        "Number of fetch inventory requests queued for processing",
                        "",
                        "",
                        "inventory",
                        "httpfetch",
                        StatType.Pull,
                        MeasuresOfInterest.AverageChangeOverTime,
                        stat => { stat.Value = m_queue.Count; },
                        StatVerbosity.Debug);

            StatsManager.RegisterStat(s_processedRequestsStat);
            StatsManager.RegisterStat(s_queuedRequestsStat);

            m_InventoryService = Scene.InventoryService;
            m_LibraryService = Scene.LibraryService;

            // We'll reuse the same handler for all requests.
            m_webFetchHandler = new FetchInvDescHandler(m_InventoryService, m_LibraryService, Scene);

            Scene.EventManager.OnRegisterCaps += RegisterCaps;

            m_NumberScenes++;

            int nworkers = 2; // was 2
            if (ProcessQueuedRequestsAsync && m_workerThreads == null)
            {
                m_workerThreads = new Thread[nworkers];

                for (uint i = 0; i < nworkers; i++)
                {
                    m_workerThreads[i] = WorkManager.StartThread(DoInventoryRequests,
                            String.Format("InventoryWorkerThread{0}", i),
                            ThreadPriority.Normal,
                            true,
                            true,
                            null,
                            int.MaxValue);
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

            if (ProcessQueuedRequestsAsync)
            {
                if (m_NumberScenes <= 0 && m_workerThreads != null)
                {
                    m_log.DebugFormat("[WebFetchInvDescModule] Closing");
                    foreach (Thread t in m_workerThreads)
                        Watchdog.AbortThread(t.ManagedThreadId);

                    m_workerThreads = null;
                }
            }
//            m_queue.Dispose();
        }

        public string Name { get { return "WebFetchInvDescModule"; } }

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

            private WebFetchInvDescModule m_module;

            public PollServiceInventoryEventArgs(WebFetchInvDescModule module, string url, UUID pId) :
                base(null, url, null, null, null, null, pId, int.MaxValue)
            {
                m_module = module;

                HasEvents = (x, y) => { lock (responses) return responses.ContainsKey(x); };

                Drop = (x, y) =>
                {
                    lock (responses)
                    {
                        responses.Remove(x);
                        lock(dropedResponses)
                            dropedResponses.Add(x);
                    }
                };

                GetEvents = (x, y) =>
                {
                    lock (responses)
                    {
                        try
                        {
                            return responses[x];
                        }
                        finally
                        {
                            responses.Remove(x);
                        }
                    }
                };

                Request = (x, y) =>
                {
                    APollRequest reqinfo = new APollRequest();
                    reqinfo.thepoll = this;
                    reqinfo.reqID = x;
                    reqinfo.request = y;
                    m_queue.Add(reqinfo);
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

                Hashtable response = new Hashtable();

                response["int_response_code"] = 200;
                response["content_type"] = "text/plain";

                response["bin_response_data"] = System.Text.Encoding.UTF8.GetBytes(
                        m_webFetchHandler.FetchInventoryDescendentsRequest(
                                    requestinfo.request["body"].ToString(),
                                    String.Empty, String.Empty, null, null)
                        );
                lock (responses)
                {
                    lock(dropedResponses)
                    {
                        if(dropedResponses.Contains(requestID))
                        {
                            dropedResponses.Remove(requestID);
                            requestinfo.request.Clear();
                            WebFetchInvDescModule.ProcessedRequestsCount++;
                            return;
                        }
                    }

                    if (responses.ContainsKey(requestID))
                        m_log.WarnFormat("[FETCH INVENTORY DESCENDENTS2 MODULE]: Caught in the act of loosing responses! Please report this on mantis #7054");
                    responses[requestID] = response;
                }
                requestinfo.request.Clear();
                WebFetchInvDescModule.ProcessedRequestsCount++;
            }
        }

        private void RegisterCaps(UUID agentID, Caps caps)
        {
            RegisterFetchDescendentsCap(agentID, caps, "FetchInventoryDescendents2", m_fetchInventoryDescendents2Url);
        }

        private void RegisterFetchDescendentsCap(UUID agentID, Caps caps, string capName, string url)
        {
            string capUrl;

            // disable the cap clause
            if (url == "")
            {
                return;
            }
            // handled by the simulator
            else if (url == "localhost")
            {
                capUrl = "/CAPS/" + UUID.Random() + "/";

                // Register this as a poll service
                PollServiceInventoryEventArgs args = new PollServiceInventoryEventArgs(this, capUrl, agentID);
                args.Type = PollServiceEventArgs.EventType.Inventory;

                caps.RegisterPollHandler(capName, args);
            }
            // external handler
            else
            {
                capUrl = url;
                IExternalCapsModule handler = Scene.RequestModuleInterface<IExternalCapsModule>();
                if (handler != null)
                    handler.RegisterExternalUserCapsHandler(agentID,caps,capName,capUrl);
                else
                    caps.RegisterHandler(capName, capUrl);
            }
        }

        private static void DoInventoryRequests()
        {
            bool running = true;
            while (running)
            {
                try
                {
                    APollRequest poolreq;
                    if (m_queue.TryTake(out poolreq, 4500))
                    {
                        Watchdog.UpdateThread();
                        if (poolreq.thepoll != null)
                            poolreq.thepoll.Process(poolreq);
                        poolreq = null;
                    }
                    Watchdog.UpdateThread();
                }
                catch (ThreadAbortException)
                {
                    Thread.ResetAbort();
                    running = false;
                }
            }
        }
    }
}
