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

using System.Collections.Generic;
using System.Reflection;
using System.Net;
using log4net;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Framework.Servers
{
    public class MainServer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static BaseHttpServer instance = null;
        private static Dictionary<uint, BaseHttpServer> m_Servers =
                new Dictionary<uint, BaseHttpServer>();

        public static BaseHttpServer Instance
        {
            get { return instance; }
            set { instance = value; }
        }

        /// <summary>
        /// Add an already started HTTP server to the collection of known servers.
        /// </summary>
        /// <param name='server'></param>
        public static void AddHttpServer(BaseHttpServer server)
        {
            m_Servers.Add(server.Port, server);
        }

        /// <summary>
        /// Get the default http server or an http server for a specific port.
        /// </summary>
        /// <remarks>
        /// If the requested HTTP server doesn't already exist then a new one is instantiated and started.
        /// </remarks>
        /// <returns></returns>
        /// <param name='port'>If 0 then the default HTTP server is returned.</param>
        public static IHttpServer GetHttpServer(uint port)
        {
            return GetHttpServer(port, null);
        }

        /// <summary>
        /// Get the default http server, an http server for a specific port
        /// and/or an http server bound to a specific address
        /// </summary>
        /// <remarks>
        /// If the requested HTTP server doesn't already exist then a new one is instantiated and started.
        /// </remarks>
        /// <returns></returns>
        /// <param name='port'>If 0 then the default HTTP server is returned.</param>
        /// <param name='ipaddr'>A specific IP address to bind to.  If null then the default IP address is used.</param>
        public static IHttpServer GetHttpServer(uint port, IPAddress ipaddr)
        {
            if (port == 0)
                return Instance;
            
            if (instance != null && port == Instance.Port)
                return Instance;

            if (m_Servers.ContainsKey(port))
                return m_Servers[port];

            m_Servers[port] = new BaseHttpServer(port);

            if (ipaddr != null)
                m_Servers[port].ListenIPAddress = ipaddr;

            m_log.InfoFormat("[MAIN HTTP SERVER]: Starting main http server on port {0}", port);
            m_Servers[port].Start();

            return m_Servers[port];
        }
    }
}
