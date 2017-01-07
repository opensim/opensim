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
using System.Collections.Specialized;
using System.Reflection;
using System.IO;
using System.Threading;
using System.Web;
using Mono.Addins;
using OpenSim.Framework.Monitoring;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Capabilities.Handlers;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.ClientStack.Linden
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GetMeshModule")]
    public class GetMeshModule : INonSharedRegionModule
    {
//        private static readonly ILog m_log =
//            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private IAssetService m_AssetService;
        private bool m_Enabled = true;
        private string m_URL;

        private string m_URL2;
        private string m_RedirectURL = null;
        private string m_RedirectURL2 = null;

        struct aPollRequest
        {
            public PollServiceMeshEventArgs thepoll;
            public UUID reqID;
            public Hashtable request;
        }

        public class aPollResponse
        {
            public Hashtable response;
            public int bytes;
            public int lod;
        }


        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static GetMeshHandler m_getMeshHandler;

        private IAssetService m_assetService = null;

        private Dictionary<UUID, string> m_capsDict = new Dictionary<UUID, string>();
        private static Thread[] m_workerThreads = null;
        private static int m_NumberScenes = 0;
        private static OpenMetaverse.BlockingQueue<aPollRequest> m_queue =
                new OpenMetaverse.BlockingQueue<aPollRequest>();

        private Dictionary<UUID, PollServiceMeshEventArgs> m_pollservices = new Dictionary<UUID, PollServiceMeshEventArgs>();


        #region Region Module interfaceBase Members

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["ClientStack.LindenCaps"];
            if (config == null)
                return;

            m_URL = config.GetString("Cap_GetMesh", string.Empty);
            // Cap doesn't exist
            if (m_URL != string.Empty)
            {
                m_Enabled = true;
                m_RedirectURL = config.GetString("GetMeshRedirectURL");
            }

            m_URL2 = config.GetString("Cap_GetMesh2", string.Empty);
            // Cap doesn't exist
            if (m_URL2 != string.Empty)
            {
                m_Enabled = true;

                m_RedirectURL2 = config.GetString("GetMesh2RedirectURL");
            }
        }

        public void AddRegion(Scene pScene)
        {
            if (!m_Enabled)
                return;

            m_scene = pScene;

            m_assetService = pScene.AssetService;
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
            m_scene.EventManager.OnDeregisterCaps -= DeregisterCaps;
            m_scene.EventManager.OnThrottleUpdate -= ThrottleUpdate;
            m_NumberScenes--;
            m_scene = null;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_AssetService = m_scene.RequestModuleInterface<IAssetService>();
            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
            // We'll reuse the same handler for all requests.
            m_getMeshHandler = new GetMeshHandler(m_assetService);
            m_scene.EventManager.OnDeregisterCaps += DeregisterCaps;
            m_scene.EventManager.OnThrottleUpdate += ThrottleUpdate;

            m_NumberScenes++;

            if (m_workerThreads == null)
            {
                m_workerThreads = new Thread[2];

                for (uint i = 0; i < 2; i++)
                {
                    m_workerThreads[i] = WorkManager.StartThread(DoMeshRequests,
                            String.Format("GetMeshWorker{0}", i),
                            ThreadPriority.Normal,
                            false,
                            false,
                            null,
                            int.MaxValue);
                }
            }
        }

        public void Close()
        {
            if(m_NumberScenes <= 0 && m_workerThreads != null)
            {
                m_log.DebugFormat("[GetMeshModule] Closing");
                foreach (Thread t in m_workerThreads)
                    Watchdog.AbortThread(t.ManagedThreadId);
                // This will fail on region shutdown. Its harmless.
                // Prevent red ink.
                try
                {
                    m_queue.Clear();
                }
                catch {}
            }
        }

        public string Name { get { return "GetMeshModule"; } }

        #endregion

        private static void DoMeshRequests()
        {
            while(true)
            {
                aPollRequest poolreq = m_queue.Dequeue();
                Watchdog.UpdateThread();
                poolreq.thepoll.Process(poolreq);
            }
        }

        // Now we know when the throttle is changed by the client in the case of a root agent or by a neighbor region in the case of a child agent.
        public void ThrottleUpdate(ScenePresence p)
        {
            UUID user = p.UUID;
            int imagethrottle = p.ControllingClient.GetAgentThrottleSilent((int)ThrottleOutPacketType.Asset);
            PollServiceMeshEventArgs args;
            if (m_pollservices.TryGetValue(user, out args))
            {
                args.UpdateThrottle(imagethrottle, p);
            }
        }

        private class PollServiceMeshEventArgs : PollServiceEventArgs
        {
            private List<Hashtable> requests =
                    new List<Hashtable>();
            private Dictionary<UUID, aPollResponse> responses =
                    new Dictionary<UUID, aPollResponse>();

            private Scene m_scene;
            private MeshCapsDataThrottler m_throttler;
            public PollServiceMeshEventArgs(string uri, UUID pId, Scene scene) :
                base(null, uri, null, null, null, pId, int.MaxValue)
            {
                m_scene = scene;
                m_throttler = new MeshCapsDataThrottler(100000, 1400000, 10000, scene, pId);
                // x is request id, y is userid
                HasEvents = (x, y) =>
                {
                    lock (responses)
                    {
                        bool ret = m_throttler.hasEvents(x, responses);
                        m_throttler.ProcessTime();
                        return ret;

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
                            m_throttler.ProcessTime();
                            responses.Remove(x);
                        }
                    }
                };
                // x is request id, y is request data hashtable
                Request = (x, y) =>
                {
                    aPollRequest reqinfo = new aPollRequest();
                    reqinfo.thepoll = this;
                    reqinfo.reqID = x;
                    reqinfo.request = y;

                    m_queue.Enqueue(reqinfo);
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
                    response["reusecontext"] = false;

                    return response;
                };
            }

            public void Process(aPollRequest requestinfo)
            {
                Hashtable response;

                UUID requestID = requestinfo.reqID;

                if(m_scene.ShuttingDown)
                    return;

                // If the avatar is gone, don't bother to get the texture
                if (m_scene.GetScenePresence(Id) == null)
                {
                    response = new Hashtable();

                    response["int_response_code"] = 500;
                    response["str_response_string"] = "Script timeout";
                    response["content_type"] = "text/plain";
                    response["keepalive"] = false;
                    response["reusecontext"] = false;

                    lock (responses)
                        responses[requestID] = new aPollResponse() { bytes = 0, response = response, lod = 0 };

                    return;
                }

                response = m_getMeshHandler.Handle(requestinfo.request);
                lock (responses)
                {
                    responses[requestID] = new aPollResponse()
                    {
                        bytes = (int)response["int_bytes"],
                        lod = (int)response["int_lod"],
                        response = response
                    };

                }
                m_throttler.ProcessTime();
            }

            internal void UpdateThrottle(int pimagethrottle, ScenePresence p)
            {
                m_throttler.UpdateThrottle(pimagethrottle, p);
            }
        }

        public void RegisterCaps(UUID agentID, Caps caps)
        {
//            UUID capID = UUID.Random();
            if (m_URL == "localhost")
            {
                string capUrl = "/CAPS/" + UUID.Random() + "/";

                // Register this as a poll service
                PollServiceMeshEventArgs args = new PollServiceMeshEventArgs(capUrl, agentID, m_scene);

                args.Type = PollServiceEventArgs.EventType.Mesh;
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
                caps.RegisterHandler("GetMesh", String.Format("{0}://{1}:{2}{3}", protocol, hostName, port, capUrl));
                m_pollservices[agentID] = args;
                m_capsDict[agentID] = capUrl;
            }
            else
            {
                caps.RegisterHandler("GetMesh", m_URL);
            }
        }

        private void DeregisterCaps(UUID agentID, Caps caps)
        {
            string capUrl;
            PollServiceMeshEventArgs args;
            if (m_capsDict.TryGetValue(agentID, out capUrl))
            {
                MainServer.Instance.RemoveHTTPHandler("", capUrl);
                m_capsDict.Remove(agentID);
            }
            if (m_pollservices.TryGetValue(agentID, out args))
            {
                m_pollservices.Remove(agentID);
            }
        }

        internal sealed class MeshCapsDataThrottler
        {

            private volatile int currenttime = 0;
            private volatile int lastTimeElapsed = 0;
            private volatile int BytesSent = 0;
            private int CapSetThrottle = 0;
            private float CapThrottleDistributon = 0.30f;
            private readonly Scene m_scene;
            private ThrottleOutPacketType Throttle;
            private readonly UUID User;

            public MeshCapsDataThrottler(int pBytes, int max, int min, Scene pScene, UUID puser)
            {
                ThrottleBytes = pBytes;
                if(ThrottleBytes < 10000)
                    ThrottleBytes = 10000;
                lastTimeElapsed = Util.EnvironmentTickCount();
                Throttle = ThrottleOutPacketType.Asset;
                m_scene = pScene;
                User = puser;
            }

            public bool hasEvents(UUID key, Dictionary<UUID, aPollResponse> responses)
            {
                PassTime();
                // Note, this is called IN LOCK
                bool haskey = responses.ContainsKey(key);

                if (!haskey)
                {
                    return false;
                }
                aPollResponse response;
                if (responses.TryGetValue(key, out response))
                {
                    // Normal
                    if (BytesSent <= ThrottleBytes)
                    {
                        BytesSent += response.bytes;
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                return haskey;
            }

            public void ProcessTime()
            {
                PassTime();
            }

            private void PassTime()
            {
                currenttime = Util.EnvironmentTickCount();
                int timeElapsed = Util.EnvironmentTickCountSubtract(currenttime, lastTimeElapsed);
                if (timeElapsed >= 100)
                {
                    lastTimeElapsed = currenttime;
                    BytesSent -= (ThrottleBytes * timeElapsed / 1000);
                    if (BytesSent < 0) BytesSent = 0;
                }
            }

            private void AlterThrottle(int setting, ScenePresence p)
            {
                p.ControllingClient.SetAgentThrottleSilent((int)Throttle,setting);
            }

            public int ThrottleBytes
            {
                get { return CapSetThrottle; }
                set
                {
                    if (value > 10000)
                        CapSetThrottle = value;
                    else
                        CapSetThrottle = 10000;
                }
            }

            internal void UpdateThrottle(int pimagethrottle, ScenePresence p)
            {
                // Client set throttle !
                CapSetThrottle = 2 * pimagethrottle;
                ProcessTime();
            }
        }
    }
}
