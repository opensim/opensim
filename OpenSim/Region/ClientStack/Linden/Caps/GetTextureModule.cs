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
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;
using OpenSim.Capabilities.Handlers;
using OpenSim.Framework.Monitoring;

namespace OpenSim.Region.ClientStack.Linden
{

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GetTextureModule")]
    public class GetTextureModule : INonSharedRegionModule
    {

        class APollRequest
        {
            public PollServiceTextureEventArgs thepoll;
            public UUID reqID;
            public Hashtable request;
            public bool send503;
        }

        public class APollResponse
        {
            public Hashtable response;
            public int bytes;
        }


        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;

        private static GetTextureHandler m_getTextureHandler;

        private IAssetService m_assetService = null;

        private Dictionary<UUID, string> m_capsDict = new Dictionary<UUID, string>();
        private static Thread[] m_workerThreads = null;
        private static int m_NumberScenes = 0;
        private static BlockingCollection<APollRequest> m_queue = new BlockingCollection<APollRequest>();

        private Dictionary<UUID,PollServiceTextureEventArgs> m_pollservices = new Dictionary<UUID,PollServiceTextureEventArgs>();

        private string m_Url = "localhost";

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["ClientStack.LindenCaps"];

            if (config == null)
                return;
/*
            m_URL = config.GetString("Cap_GetTexture", string.Empty);
            // Cap doesn't exist
            if (m_URL != string.Empty)
            {
                m_Enabled = true;
                m_RedirectURL = config.GetString("GetTextureRedirectURL");
            }
*/
            m_Url = config.GetString("Cap_GetTexture", "localhost");
        }

        public void AddRegion(Scene s)
        {
            m_scene = s;
        }

        public void RemoveRegion(Scene s)
        {
            s.EventManager.OnRegisterCaps -= RegisterCaps;
            s.EventManager.OnDeregisterCaps -= DeregisterCaps;
            m_NumberScenes--;
            m_scene = null;
        }

        public void RegionLoaded(Scene s)
        {
            if(m_assetService == null)
            {
                m_assetService = s.RequestModuleInterface<IAssetService>();
                // We'll reuse the same handler for all requests.
                m_getTextureHandler = new GetTextureHandler(m_assetService);
            }

            s.EventManager.OnRegisterCaps += RegisterCaps;
            s.EventManager.OnDeregisterCaps += DeregisterCaps;

            m_NumberScenes++;

            if (m_workerThreads == null)
            {
                m_workerThreads = new Thread[2];

                for (uint i = 0; i < 2; i++)
                {
                    m_workerThreads[i] = WorkManager.StartThread(DoTextureRequests,
                            String.Format("GetTextureWorker{0}", i),
                            ThreadPriority.Normal,
                            true,
                            false,
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
            if(m_NumberScenes <= 0 && m_workerThreads != null)
            {
                m_log.DebugFormat("[GetTextureModule] Closing");

                foreach (Thread t in m_workerThreads)
                    Watchdog.AbortThread(t.ManagedThreadId);

                m_queue.Dispose();
            }
        }

        public string Name { get { return "GetTextureModule"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        private class PollServiceTextureEventArgs : PollServiceEventArgs
        {
            private List<Hashtable> requests =
                    new List<Hashtable>();
            private Dictionary<UUID, APollResponse> responses =
                    new Dictionary<UUID, APollResponse>();
            private HashSet<UUID> dropedResponses = new HashSet<UUID>();

            private Scene m_scene;
            private ScenePresence m_presence;
            public PollServiceTextureEventArgs(UUID pId, Scene scene) :
                    base(null, "", null, null, null, null, pId, int.MaxValue)              
            {
                m_scene = scene;
                // x is request id, y is userid
                HasEvents = (x, y) =>
                {
                    lock (responses)
                    {
                        APollResponse response;
                        if (responses.TryGetValue(x, out response))
                        {
                            if (m_presence == null)
                                m_presence = m_scene.GetScenePresence(pId);

                            if (m_presence == null || m_presence.IsDeleted)
                                return true;
                            return m_presence.CapCanSendAsset(0, response.bytes);
                        }
                        return false;
                    }
                };

                Drop = (x, y) =>
                {
                    lock (responses)
                    {
                        responses.Remove(x);
                        dropedResponses.Add(x);
                    }
               };

                GetEvents = (x, y) =>
                {
                    lock (responses)
                    {
                        try
                        {
                            return responses[x].response;
                        }
                        finally
                        {
                            responses.Remove(x);
                        }
                    }
                };
                // x is request id, y is request data hashtable
                Request = (x, y) =>
                {
                    APollRequest reqinfo = new APollRequest();
                    reqinfo.thepoll = this;
                    reqinfo.reqID = x;
                    reqinfo.request = y;
                    reqinfo.send503 = false;

                    lock (responses)
                    {
                        if (responses.Count > 0)
                        {
                            if (m_queue.Count >= 4)
                            {
                                // Never allow more than 4 fetches to wait
                                reqinfo.send503 = true;
                            }
                        }
                    }
                    m_queue.Add(reqinfo);
                };

                // this should never happen except possible on shutdown
                NoEvents = (x, y) =>
                {
/*
                    lock (requests)
                    {
                        Hashtable request = requests.Find(id => id["RequestID"].ToString() == x.ToString());
                        requests.Remove(request);
                    }
*/
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
                Hashtable response;

                UUID requestID = requestinfo.reqID;

                if(m_scene.ShuttingDown)
                    return;

                lock (responses)
                {
                    lock(dropedResponses)
                    {
                        if(dropedResponses.Contains(requestID))
                        {
                            dropedResponses.Remove(requestID);
                            return;
                        }
                    }

                    if (requestinfo.send503)
                    {
                        response = new Hashtable();

                        response["int_response_code"] = 503;
                        response["str_response_string"] = "Throttled";
                        response["content_type"] = "text/plain";
                        response["keepalive"] = false;

                        Hashtable headers = new Hashtable();
                        headers["Retry-After"] = 30;
                        response["headers"] = headers;

                        responses[requestID] = new APollResponse() {bytes = 0, response = response};

                        return;
                    }

                // If the avatar is gone, don't bother to get the texture
                    if (m_scene.GetScenePresence(Id) == null)
                    {
                        response = new Hashtable();

                        response["int_response_code"] = 500;
                        response["str_response_string"] = "Script timeout";
                        response["content_type"] = "text/plain";
                        response["keepalive"] = false;

                        responses[requestID] = new APollResponse() {bytes = 0, response = response};

                        return;
                    }
                }

                response = m_getTextureHandler.Handle(requestinfo.request);

                lock (responses)
                {
                    lock(dropedResponses)
                    {
                        if(dropedResponses.Contains(requestID))
                        {
                            dropedResponses.Remove(requestID);
                            return;
                        }
                    }
                    responses[requestID] = new APollResponse()
                        {
                            bytes = (int) response["int_bytes"],
                            response = response
                        };
                } 
            }
        }

        private void RegisterCaps(UUID agentID, Caps caps)
        {
            if (m_Url == "localhost")
            {
                string capUrl = "/CAPS/" + UUID.Random() + "/";

                // Register this as a poll service
                PollServiceTextureEventArgs args = new PollServiceTextureEventArgs(agentID, m_scene);

                args.Type = PollServiceEventArgs.EventType.Texture;
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
                IExternalCapsModule handler = m_scene.RequestModuleInterface<IExternalCapsModule>();
                if (handler != null)
                    handler.RegisterExternalUserCapsHandler(agentID, caps, "GetTexture", capUrl);
                else
                    caps.RegisterHandler("GetTexture", String.Format("{0}://{1}:{2}{3}", protocol, hostName, port, capUrl));
                m_pollservices[agentID] = args;
                m_capsDict[agentID] = capUrl;
            }
            else
            {
                caps.RegisterHandler("GetTexture", m_Url);
            }
        }

        private void DeregisterCaps(UUID agentID, Caps caps)
        {
            PollServiceTextureEventArgs args;

            MainServer.Instance.RemoveHTTPHandler("", m_Url);
            m_capsDict.Remove(agentID);

            if (m_pollservices.TryGetValue(agentID, out args))
            {
                m_pollservices.Remove(agentID);
            }
        }

        private static void DoTextureRequests()
        {
            APollRequest poolreq;
            while (m_NumberScenes > 0)
            {
                poolreq = null;
                if(!m_queue.TryTake(out poolreq, 4500) || poolreq == null)
                {
                    Watchdog.UpdateThread();
                    continue;
                }

                if(m_NumberScenes <= 0)
                   break;
          
                Watchdog.UpdateThread();
                if(poolreq.reqID != UUID.Zero)
                    poolreq.thepoll.Process(poolreq);
            }
        }
    }
}
