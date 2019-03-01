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
using Mono.Addins;
using OpenSim.Framework.Monitoring;
using log4net;
using Nini.Config;
using OpenMetaverse;
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
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GetAssetsModule")]
    public class GetAssetsModule : INonSharedRegionModule
    {
//        private static readonly ILog m_log =
//            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private bool m_Enabled;

        private string m_GetTextureURL;
        private string m_GetMeshURL;
        private string m_GetMesh2URL;
        private string m_GetAssetURL;

        class APollRequest
        {
            public PollServiceAssetEventArgs thepoll;
            public UUID reqID;
            public Hashtable request;
        }

        public class APollResponse
        {
            public Hashtable response;
            public int bytes;
            public bool throttle;
        }

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static IAssetService m_assetService = null;
        private static GetAssetsHandler m_getAssetHandler;
        private static Thread[] m_workerThreads = null;
        private static int m_NumberScenes = 0;
        private static BlockingCollection<APollRequest> m_queue = new BlockingCollection<APollRequest>();
        private static object m_loadLock = new object();

        private Dictionary<UUID, string> m_capsDictTexture = new Dictionary<UUID, string>();
        private Dictionary<UUID, string> m_capsDictGetMesh = new Dictionary<UUID, string>();
        private Dictionary<UUID, string> m_capsDictGetMesh2 = new Dictionary<UUID, string>();
        private Dictionary<UUID, string> m_capsDictGetAsset = new Dictionary<UUID, string>();

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

            m_GetTextureURL = config.GetString("Cap_GetTexture", string.Empty);
            if (m_GetTextureURL != string.Empty)
                m_Enabled = true;

            m_GetMeshURL = config.GetString("Cap_GetMesh", string.Empty);
            if (m_GetMeshURL != string.Empty)
                m_Enabled = true;

            m_GetMesh2URL = config.GetString("Cap_GetMesh2", string.Empty);
            if (m_GetMesh2URL != string.Empty)
                m_Enabled = true;

            m_GetAssetURL = config.GetString("Cap_GetAsset", string.Empty);
            if (m_GetAssetURL != string.Empty)
                m_Enabled = true;

        }

        public void AddRegion(Scene pScene)
        {
            if (!m_Enabled)
                return;

            m_scene = pScene;
        }

        public void RemoveRegion(Scene s)
        {
            if (!m_Enabled)
                return;

            s.EventManager.OnRegisterCaps -= RegisterCaps;
            s.EventManager.OnDeregisterCaps -= DeregisterCaps;
            m_NumberScenes--;
            m_scene = null;
        }

        public void RegionLoaded(Scene s)
        {
            if (!m_Enabled)
                return;

            lock(m_loadLock)
            {
                if (m_assetService == null && m_NumberScenes == 0)
                {
                    m_assetService = s.RequestModuleInterface<IAssetService>();
                    // We'll reuse the same handler for all requests.
                    m_getAssetHandler = new GetAssetsHandler(m_assetService);
                }

                if (m_assetService == null)
                {
                    m_Enabled = false;
                    return;
                }

                s.EventManager.OnRegisterCaps += RegisterCaps;
                s.EventManager.OnDeregisterCaps += DeregisterCaps;

                m_NumberScenes++;

                if (m_workerThreads == null)
                {
                    m_workerThreads = new Thread[3];
                    for (uint i = 0; i < 3; i++)
                    {
                        m_workerThreads[i] = WorkManager.StartThread(DoAssetRequests,
                                String.Format("GetCapsAssetWorker{0}", i),
                                ThreadPriority.Normal,
                                true,
                                false,
                                null,
                                int.MaxValue);
                    }
                }
            }
        }

        public void Close()
        {
            if(m_NumberScenes <= 0 && m_workerThreads != null)
            {
                m_log.DebugFormat("[GetAssetsModule] Closing");
                foreach (Thread t in m_workerThreads)
                    Watchdog.AbortThread(t.ManagedThreadId);
                // This will fail on region shutdown. Its harmless.
                // Prevent red ink.
                try
                {
                    m_queue.Dispose();
                }
                catch {}
            }
        }

        public string Name { get { return "GetAssetsModule"; } }

        #endregion

        private static void DoAssetRequests()
        {
            try
            {
                while (m_NumberScenes > 0)
                {
                    APollRequest poolreq;
                    if (m_queue.TryTake(out poolreq, 4500))
                    {
                        if (m_NumberScenes <= 0)
                            break;
                        Watchdog.UpdateThread();
                        if (poolreq.reqID != UUID.Zero)
                            poolreq.thepoll.Process(poolreq);
                        poolreq = null;
                    }
                    Watchdog.UpdateThread();
                }
            }
            catch (ThreadAbortException)
            {
                Thread.ResetAbort();
            }
        }

        private class PollServiceAssetEventArgs : PollServiceEventArgs
        {
            private List<Hashtable> requests = new List<Hashtable>();
            private Dictionary<UUID, APollResponse> responses =new Dictionary<UUID, APollResponse>();
            private HashSet<UUID> dropedResponses = new HashSet<UUID>();

            private Scene m_scene;
            private ScenePresence m_presence;
            public PollServiceAssetEventArgs(string uri, UUID pId, Scene scene) :
                base(null, uri, null, null, null, null, pId, int.MaxValue)
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
                            if(response.throttle)
                                return m_presence.CapCanSendAsset(1, response.bytes);
                            return m_presence.CapCanSendAsset(2, response.bytes);
                        }
                        return false;
                    }
                };

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
                    response["str_response_string"] = "timeout";
                    response["content_type"] = "text/plain";
                    response["keepalive"] = false;
                    return response;
                };
            }

            public void Process(APollRequest requestinfo)
            {
                Hashtable curresponse;

                UUID requestID = requestinfo.reqID;

                if(m_scene.ShuttingDown)
                    return;

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
/* can't do this with current viewers; HG problem
                    // If the avatar is gone, don't bother to get the texture
                    if(m_scene.GetScenePresence(Id) == null)
                    {
                        curresponse = new Hashtable();
                        curresponse["int_response_code"] = 500;
                        curresponse["str_response_string"] = "timeout";
                        curresponse["content_type"] = "text/plain";
                        curresponse["keepalive"] = false;
                        responses[requestID] = new APollResponse() { bytes = 0, response = curresponse };
                        return;
                    }
*/
                }

                curresponse = m_getAssetHandler.Handle(requestinfo.request);

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

                    APollResponse preq= new APollResponse()
                    {
                        bytes = (int)curresponse["int_bytes"],
                        response = curresponse
                    };
                    if(curresponse.Contains("throttle"))
                        preq.throttle = true;
                    responses[requestID] = preq;
                }
            }
        }

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            string hostName = m_scene.RegionInfo.ExternalHostName;
            uint port = (MainServer.Instance == null) ? 0 : MainServer.Instance.Port;
            string protocol = "http";
            if (MainServer.Instance.UseSSL)
            {
                hostName = MainServer.Instance.SSLCommonName;
                port = MainServer.Instance.SSLPort;
                protocol = "https";
            }

            string baseURL = String.Format("{0}://{1}:{2}", protocol, hostName, port);

            if (m_GetTextureURL == "localhost")
            {
                string capUrl = "/CAPS/" + UUID.Random() + "/";

                // Register this as a poll service
                PollServiceAssetEventArgs args = new PollServiceAssetEventArgs(capUrl, agentID, m_scene);

                args.Type = PollServiceEventArgs.EventType.Texture;
                MainServer.Instance.AddPollServiceHTTPHandler(capUrl, args);

                IExternalCapsModule handler = m_scene.RequestModuleInterface<IExternalCapsModule>();
                if (handler != null)
                    handler.RegisterExternalUserCapsHandler(agentID, caps, "GetTexture", capUrl);
                else
                    caps.RegisterHandler("GetTexture", baseURL + capUrl);
                m_capsDictTexture[agentID] = capUrl;
            }
            else
            {
                caps.RegisterHandler("GetTexture", m_GetTextureURL);
            }

            //GetMesh
            if (m_GetMeshURL == "localhost")
            {
                string capUrl = "/CAPS/" + UUID.Random() + "/";

                PollServiceAssetEventArgs args = new PollServiceAssetEventArgs(capUrl, agentID, m_scene);
                args.Type = PollServiceEventArgs.EventType.Mesh;
                MainServer.Instance.AddPollServiceHTTPHandler(capUrl, args);

                IExternalCapsModule handler = m_scene.RequestModuleInterface<IExternalCapsModule>();
                if (handler != null)
                    handler.RegisterExternalUserCapsHandler(agentID, caps, "GetMesh", capUrl);
                else
                    caps.RegisterHandler("GetMesh", baseURL + capUrl);
                m_capsDictGetMesh[agentID] = capUrl;
            }
            else if (m_GetMeshURL != string.Empty)
                caps.RegisterHandler("GetMesh", m_GetMeshURL);

            //GetMesh2
            if (m_GetMesh2URL == "localhost")
            {
                string capUrl = "/CAPS/" + UUID.Random() + "/";

                PollServiceAssetEventArgs args = new PollServiceAssetEventArgs(capUrl, agentID, m_scene);
                args.Type = PollServiceEventArgs.EventType.Mesh2;
                MainServer.Instance.AddPollServiceHTTPHandler(capUrl, args);
                IExternalCapsModule handler = m_scene.RequestModuleInterface<IExternalCapsModule>();
                if (handler != null)
                    handler.RegisterExternalUserCapsHandler(agentID, caps, "GetMesh2", capUrl);
                else
                    caps.RegisterHandler("GetMesh2", baseURL + capUrl);
                m_capsDictGetMesh2[agentID] = capUrl;
            }
            else if (m_GetMesh2URL != string.Empty)
                caps.RegisterHandler("GetMesh2", m_GetMesh2URL);

            //ViewerAsset
            if (m_GetAssetURL == "localhost")
            {
                string capUrl = "/CAPS/" + UUID.Random() + "/";

                PollServiceAssetEventArgs args = new PollServiceAssetEventArgs(capUrl, agentID, m_scene);
                args.Type = PollServiceEventArgs.EventType.Asset;
                MainServer.Instance.AddPollServiceHTTPHandler(capUrl, args);
                IExternalCapsModule handler = m_scene.RequestModuleInterface<IExternalCapsModule>();
                if (handler != null)
                    handler.RegisterExternalUserCapsHandler(agentID, caps, "ViewerAsset", capUrl);
                else
                    caps.RegisterHandler("ViewerAsset", baseURL + capUrl);
                m_capsDictGetAsset[agentID] = capUrl;
            }
            else if (m_GetAssetURL != string.Empty)
                caps.RegisterHandler("ViewerAsset", m_GetMesh2URL);

        }

        private void DeregisterCaps(UUID agentID, Caps caps)
        {
            string capUrl;
            if (m_capsDictTexture.TryGetValue(agentID, out capUrl))
            {
                MainServer.Instance.RemovePollServiceHTTPHandler("", capUrl);
                m_capsDictTexture.Remove(agentID);
            }
            if (m_capsDictGetMesh.TryGetValue(agentID, out capUrl))
            {
                MainServer.Instance.RemovePollServiceHTTPHandler("", capUrl);
                m_capsDictGetMesh.Remove(agentID);
            }
            if (m_capsDictGetMesh2.TryGetValue(agentID, out capUrl))
            {
                MainServer.Instance.RemovePollServiceHTTPHandler("", capUrl);
                m_capsDictGetMesh2.Remove(agentID);
            }

            if (m_capsDictGetAsset.TryGetValue(agentID, out capUrl))
            {
                MainServer.Instance.RemovePollServiceHTTPHandler("", capUrl);
                m_capsDictGetAsset.Remove(agentID);
            }
        }
    }
}
