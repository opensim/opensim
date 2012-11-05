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

        struct aPollRequest
        {
            public PollServiceTextureEventArgs thepoll;
            public UUID reqID;
            public Hashtable request;
        }

        public struct aPollResponse
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

        private static OpenMetaverse.BlockingQueue<aPollRequest> m_queue =
                new OpenMetaverse.BlockingQueue<aPollRequest>();

        private Dictionary<UUID,PollServiceTextureEventArgs> m_pollservices = new Dictionary<UUID,PollServiceTextureEventArgs>();

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
            m_scene.EventManager.OnThrottleUpdate -= ThrottleUpdate;
            m_scene = null;
        }

        public void RegionLoaded(Scene s)
        {
            // We'll reuse the same handler for all requests.
            m_getTextureHandler = new GetTextureHandler(m_assetService);

            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
            m_scene.EventManager.OnDeregisterCaps += DeregisterCaps;
            m_scene.EventManager.OnThrottleUpdate += ThrottleUpdate;

            if (m_workerThreads == null)
            {
                m_workerThreads = new Thread[2];

                for (uint i = 0; i < 2; i++)
                {
                    m_workerThreads[i] = Watchdog.StartThread(DoTextureRequests,
                            String.Format("TextureWorkerThread{0}", i),
                            ThreadPriority.Normal,
                            false,
                            false,
                            null,
                            int.MaxValue);
                }
            }
        }
        private int ExtractImageThrottle(byte[] pthrottles)
        {
       
            byte[] adjData;
            int pos = 0;

            if (!BitConverter.IsLittleEndian)
            {
                byte[] newData = new byte[7 * 4];
                Buffer.BlockCopy(pthrottles, 0, newData, 0, 7 * 4);

                for (int i = 0; i < 7; i++)
                    Array.Reverse(newData, i * 4, 4);

                adjData = newData;
            }
            else
            {
                adjData = pthrottles;
            }

            // 0.125f converts from bits to bytes
            //int resend = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
           // int land = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
           // int wind = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
           // int cloud = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
           // int task = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            pos = pos + 20;
            int texture = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            //int asset = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f);
            return texture;
        }

        // Now we know when the throttle is changed by the client in the case of a root agent or by a neighbor region in the case of a child agent.
        public void ThrottleUpdate(ScenePresence p)
        {
            byte[] throttles = p.ControllingClient.GetThrottlesPacked(1);
            UUID user = p.UUID;
            int imagethrottle = ExtractImageThrottle(throttles);
            PollServiceTextureEventArgs args;
            if (m_pollservices.TryGetValue(user,out args))
            {
                args.UpdateThrottle(imagethrottle);
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
                Watchdog.AbortThread(t.ManagedThreadId);

        }

        private class PollServiceTextureEventArgs : PollServiceEventArgs
        {
            private List<Hashtable> requests =
                    new List<Hashtable>();
            private Dictionary<UUID, aPollResponse> responses =
                    new Dictionary<UUID, aPollResponse>();

            private Scene m_scene;

            public PollServiceTextureEventArgs(UUID pId, Scene scene) :
                    base(null, null, null, null, pId, int.MaxValue)              
            {
                m_scene = scene;
                // x is request id, y is userid
                HasEvents = (x, y) =>
                {
                    lock (responses)
                        return responses.ContainsKey(x);
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
                        responses[requestID] = new aPollResponse() {bytes = 0,response = response};

                    return;
                }

                response = m_getTextureHandler.Handle(requestinfo.request);
                lock (responses)
                    responses[requestID] = new aPollResponse() { bytes = (int)response["int_bytes"], response = response};
            }

            internal void UpdateThrottle(int pimagethrottle)
            {
               
            }
        }

        private void RegisterCaps(UUID agentID, Caps caps)
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
            caps.RegisterHandler("GetTexture", String.Format("{0}://{1}:{2}{3}", protocol, hostName, port, capUrl));
            m_pollservices.Add(agentID, args);
            m_capsDict[agentID] = capUrl;
        }

        private void DeregisterCaps(UUID agentID, Caps caps)
        {
            string capUrl;
            PollServiceTextureEventArgs args;
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

        private void DoTextureRequests()
        {
            while (true)
            {
                aPollRequest poolreq = m_queue.Dequeue();

                poolreq.thepoll.Process(poolreq);
            }
        }
    }
}
