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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using log4net;
using HttpServer;

using HttpListener = HttpServer.HttpListener;

namespace OpenSim.Framework.Servers
{
    /// <summary>
    /// OSHttpServer provides an HTTP server bound to a specific
    /// port. When instantiated with just address and port it uses
    /// normal HTTP, when instantiated with address, port, and X509
    /// certificate, it uses HTTPS.
    /// </summary>
    public class OSHttpServer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // underlying HttpServer.HttpListener
        protected HttpListener _listener;
        protected Thread _engine;

        // OSHttpRequestPumps "pumping" incoming OSHttpRequests
        // upwards
        protected OSHttpRequestPump[] _pumps;

        // thread identifier
        protected string _engineId;
        public string EngineID
        {
            get { return _engineId; }
        }

        /// <summary>
        /// True if this is an HTTPS connection; false otherwise.
        /// </summary>
        protected bool _isSecure;
        public bool IsSecure
        {
            get { return _isSecure; }
        }

        /// <summary>
        /// Instantiate an HTTP server.
        /// </summary>
        public OSHttpServer(IPAddress address, int port, int poolSize)
        {
            _engineId = String.Format("OSHttpServer [HTTP:{0}/ps:{1}]", port, poolSize);
            _isSecure = false;

            _pumps = OSHttpRequestPump.Pumps(this, poolSize);
        }

        /// <summary>
        /// Instantiate an HTTPS server.
        /// </summary>
        public OSHttpServer(IPAddress address, int port, X509Certificate certificate, int poolSize) :
        this(address, port, poolSize)
        {
            _engineId = String.Format("OSHttpServer [HTTPS:{0}/ps:{1}]", port, poolSize);
            _isSecure = true;
        }

        /// <summary>
        /// Start the HTTP server engine.
        /// </summary>
        public void Start()
        {
            _engine = new Thread(new ThreadStart(Engine));
            _engine.Name = _engineId;
            _engine.IsBackground = true;
            _engine.Start();
            ThreadTracker.Add(_engine);
        }

        /// <summary>
        /// </summary>
        private void Engine()
        {
            while (true)
            {
                // do stuff
            }
        }

    }
}
