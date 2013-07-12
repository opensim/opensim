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
using System.IO;
using System.Reflection;
using System.Threading;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

// using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Framework.Capabilities
{
    /// <summary>
    /// XXX Probably not a particularly nice way of allow us to get the scene presence from the scene (chiefly so that
    /// we can popup a message on the user's client if the inventory service has permanently failed).  But I didn't want
    /// to just pass the whole Scene into CAPS.
    /// </summary>
    public delegate IClientAPI GetClientDelegate(UUID agentID);

    public class Caps
    {
//        private static readonly ILog m_log =
//            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_httpListenerHostName;
        private uint m_httpListenPort;

        /// <summary>
        /// This is the uuid portion of every CAPS path.  It is used to make capability urls private to the requester.
        /// </summary>
        private string m_capsObjectPath;
        public string CapsObjectPath { get { return m_capsObjectPath; } }

        private CapsHandlers m_capsHandlers;
        private Dictionary<string, string> m_externalCapsHandlers;

        private IHttpServer m_httpListener;
        private UUID m_agentID;
        private string m_regionName;
        private ManualResetEvent m_capsActive = new ManualResetEvent(false);

        public UUID AgentID
        {
            get { return m_agentID; }
        }

        public string RegionName
        {
            get { return m_regionName; }
        }

        public string HostName
        {
            get { return m_httpListenerHostName; }
        }

        public uint Port
        {
            get { return m_httpListenPort; }
        }

        public IHttpServer HttpListener
        {
            get { return m_httpListener; }
        }

        public bool SSLCaps
        {
            get { return m_httpListener.UseSSL; }
        }

        public string SSLCommonName
        {
            get { return m_httpListener.SSLCommonName; }
        }

        public CapsHandlers CapsHandlers
        {
            get { return m_capsHandlers; }
        }

        public Dictionary<string, string> ExternalCapsHandlers
        {
            get { return m_externalCapsHandlers; }
        }

        public Caps(IHttpServer httpServer, string httpListen, uint httpPort, string capsPath,
                    UUID agent, string regionName)
        {
            m_capsObjectPath = capsPath;
            m_httpListener = httpServer;
            m_httpListenerHostName = httpListen;

            m_httpListenPort = httpPort;

            if (httpServer != null && httpServer.UseSSL)
            {
                m_httpListenPort = httpServer.SSLPort;
                httpListen = httpServer.SSLCommonName;
                httpPort = httpServer.SSLPort;
            }

            m_agentID = agent;
            m_capsHandlers = new CapsHandlers(httpServer, httpListen, httpPort, (httpServer == null) ? false : httpServer.UseSSL);
            m_externalCapsHandlers = new Dictionary<string, string>();
            m_regionName = regionName;
        }

        /// <summary>
        /// Register a handler.  This allows modules to register handlers.
        /// </summary>
        /// <param name="capName"></param>
        /// <param name="handler"></param>
        public void RegisterHandler(string capName, IRequestHandler handler)
        {
            //m_log.DebugFormat("[CAPS]: Registering handler for \"{0}\": path {1}", capName, handler.Path);
            m_capsHandlers[capName] = handler;
        }

        /// <summary>
        /// Register an external handler. The service for this capability is somewhere else
        /// given by the URL.
        /// </summary>
        /// <param name="capsName"></param>
        /// <param name="url"></param>
        public void RegisterHandler(string capsName, string url)
        {
            m_externalCapsHandlers.Add(capsName, url);
        }

        /// <summary>
        /// Remove all CAPS service handlers.
        /// </summary>
        public void DeregisterHandlers()
        {
            if (m_capsHandlers != null)
            {
                foreach (string capsName in m_capsHandlers.Caps)
                {
                    m_capsHandlers.Remove(capsName);
                }
            }
        }

        public void Activate()
        {
            m_capsActive.Set();
        }

        public bool WaitForActivation()
        {
            // Wait for 30s. If that elapses, return false and run without caps
            return m_capsActive.WaitOne(30000);
        }
    }
}
