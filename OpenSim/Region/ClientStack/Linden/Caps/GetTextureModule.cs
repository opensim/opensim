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
using OpenSim.Framework.Monitoring;

namespace OpenSim.Region.ClientStack.Linden
{

    /// <summary>
    /// This module implements both WebFetchTextureDescendents and FetchTextureDescendents2 capabilities.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GetTextureModule")]
    public class GetTextureModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;

        private static GetTextureHandler m_getTextureHandler;

        private IAssetService m_assetService = null;

        private Dictionary<UUID, string> m_capsDict = new Dictionary<UUID, string>();
        private static Thread[] m_workerThreads = null;

        private static OpenMetaverse.BlockingQueue<PollServiceTextureEventArgs> m_queue =
                new OpenMetaverse.BlockingQueue<PollServiceTextureEventArgs>();

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(Scene s)
        {
            m_scene = s;
            m_assetService = s.AssetService;
        }

        public void RemoveRegion(Scene s)
        {
            m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
            m_scene.EventManager.OnDeregisterCaps -= DeregisterCaps;
            m_scene = null;
        }

        public void RegionLoaded(Scene s)
        {
            // We'll reuse the same handler for all requests.
            m_getTextureHandler = new GetTextureHandler(m_assetService);

            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
            m_scene.EventManager.OnDeregisterCaps += DeregisterCaps;

            if (m_workerThreads == null)
            {
                m_workerThreads = new Thread[4];

                for (uint i = 0; i < 4; i++)
                {
                    m_workerThreads[i] = Watchdog.StartThread(DoTextureRequests,
                            String.Format("TextureWorkerThread{0}", i),
                            ThreadPriority.Normal,
                            false,
                            true,
                            null,
                            int.MaxValue);
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void Close() { }

        public string Name { get { return "GetTextureModule"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        ~GetTextureModule()
        {
            foreach (Thread t in m_workerThreads)
                t.Abort();
        }

        private class PollServiceTextureEventArgs : PollServiceEventArgs
        {
            private List<Hashtable> requests =
                    new List<Hashtable>();
            private Dictionary<UUID, Hashtable> responses =
                    new Dictionary<UUID, Hashtable>();

            private Scene m_scene;

            public PollServiceTextureEventArgs(UUID pId, Scene scene) :
                    base(null, null, null, null, pId, 30000)
            {
                m_scene = scene;

                HasEvents = (x, y) => { return this.responses.ContainsKey(x); };
                GetEvents = (x, y, s) =>
                {
                    try
                    {
                        return this.responses[x];
                    }
                    finally
                    {
                        responses.Remove(x);
                    }
                };

                Request = (x, y) =>
                {
                    y["RequestID"] = x.ToString();
                    lock (this.requests)
                        this.requests.Add(y);

                    m_queue.Enqueue(this);
                };

                NoEvents = (x, y) =>
                {
                    lock (this.requests)
                    {
                        Hashtable request = requests.Find(id => id["RequestID"].ToString() == x.ToString());
                        requests.Remove(request);
                    }

                    Hashtable response = new Hashtable();

                    response["int_response_code"] = 500;
                    response["str_response_string"] = "Script timeout";
                    response["content_type"] = "text/plain";
                    response["keepalive"] = false;
                    response["reusecontext"] = false;

                    return response;
                };
            }

            public void Process()
            {
                Hashtable response;
                Hashtable request = null;

                try
                {
                    lock (this.requests)
                    {
                        request = requests[0];
                        requests.RemoveAt(0);
                    }
                }
                catch
                {
                    return;
                }

                UUID requestID = new UUID(request["RequestID"].ToString());

                // If the avatar is gone, don't bother to get the texture
                if (m_scene.GetScenePresence(Id) == null)
                {
                    response = new Hashtable();

                    response["int_response_code"] = 500;
                    response["str_response_string"] = "Script timeout";
                    response["content_type"] = "text/plain";
                    response["keepalive"] = false;
                    response["reusecontext"] = false;

                    responses[requestID] = response;
                    return;
                }

                response = m_getTextureHandler.Handle(request);

                responses[requestID] = response; 
            }
        }

        private void RegisterCaps(UUID agentID, Caps caps)
        {
            string capUrl = "/CAPS/" + UUID.Random() + "/";

            // Register this as a poll service
            // absurd large timeout to tune later to make a bit less than viewer
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
            caps.RegisterHandler("GetTexture", String.Format("{0}://{1}:{2}{3}", protocol, hostName, port, capUrl));

            m_capsDict[agentID] = capUrl;
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

        private void DoTextureRequests()
        {
            while (true)
            {
                PollServiceTextureEventArgs args = m_queue.Dequeue();

                args.Process();
            }
        }
    }

}
