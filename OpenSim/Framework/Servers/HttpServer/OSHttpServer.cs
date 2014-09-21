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
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using log4net;
using HttpServer;

using HttpListener = HttpServer.HttpListener;

namespace OpenSim.Framework.Servers.HttpServer
{
    /// <summary>
    /// OSHttpServer provides an HTTP server bound to a specific
    /// port. When instantiated with just address and port it uses
    /// normal HTTP, when instantiated with address, port, and X509
    /// certificate, it uses HTTPS.
    /// </summary>
    public class OSHttpServer
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private object _syncObject = new object();

        // underlying HttpServer.HttpListener
        protected HttpListener _listener;
        // underlying core/engine thread
        protected Thread _engine;

        // Queue containing (OS)HttpRequests
        protected OSHttpRequestQueue _queue;

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

        public int QueueSize
        {
            get { return _pumps.Length; }
        }

        /// <summary>
        /// List of registered OSHttpHandlers for this OSHttpServer instance.
        /// </summary>
        protected List<OSHttpHandler> _httpHandlers = new List<OSHttpHandler>();
        public List<OSHttpHandler> OSHttpHandlers
        {
            get
            {
                lock (_httpHandlers)
                {
                    return new List<OSHttpHandler>(_httpHandlers);
                }
            }
        }


        /// <summary>
        /// Instantiate an HTTP server.
        /// </summary>
        public OSHttpServer(IPAddress address, int port, int poolSize)
        {
            _engineId = String.Format("OSHttpServer (HTTP:{0})", port);
            _isSecure = false;
            _log.DebugFormat("[{0}] HTTP server instantiated", EngineID);

            _listener = new HttpListener(address, port);
            _queue = new OSHttpRequestQueue();
            _pumps = OSHttpRequestPump.Pumps(this, _queue, poolSize);
        }

        /// <summary>
        /// Instantiate an HTTPS server.
        /// </summary>
        public OSHttpServer(IPAddress address, int port, X509Certificate certificate, int poolSize)
        {
            _engineId = String.Format("OSHttpServer [HTTPS:{0}/ps:{1}]", port, poolSize);
            _isSecure = true;
            _log.DebugFormat("[{0}] HTTPS server instantiated", EngineID);

            _listener = new HttpListener(address, port, certificate);
            _queue = new OSHttpRequestQueue();
            _pumps = OSHttpRequestPump.Pumps(this, _queue, poolSize);
        }

        /// <summary>
        /// Turn an HttpRequest into an OSHttpRequestItem and place it
        /// in the queue. The OSHttpRequestQueue object will pulse the
        /// next available idle pump.
        /// </summary>
        protected void OnHttpRequest(HttpClientContext client, HttpRequest request)
        {
            // turn request into OSHttpRequest
            OSHttpRequest req = new OSHttpRequest(client, request);

            // place OSHttpRequest into _httpRequestQueue, will
            // trigger Pulse to idle waiting pumps
            _queue.Enqueue(req);
        }

        /// <summary>
        /// Start the HTTP server engine.
        /// </summary>
        public void Start()
        {
            _engine = new Thread(new ThreadStart(Engine));
            _engine.IsBackground = true;
            _engine.Start();
            _engine.Name = string.Format ("Engine:{0}",_engineId);

            ThreadTracker.Add(_engine);

            // start the pumps...
            for (int i = 0; i < _pumps.Length; i++)
                _pumps[i].Start();
        }

        public void Stop()
        {
            lock (_syncObject) Monitor.Pulse(_syncObject);
        }

        /// <summary>
        /// Engine keeps the HTTP server running.
        /// </summary>
        private void Engine()
        {
            try {
                _listener.RequestHandler += OnHttpRequest;
                _listener.Start(QueueSize);
                _log.InfoFormat("[{0}] HTTP server started", EngineID);

                lock (_syncObject) Monitor.Wait(_syncObject);
            }
            catch (Exception ex)
            {
                _log.DebugFormat("[{0}] HTTP server startup failed: {1}", EngineID, ex.ToString());
            }

            _log.InfoFormat("[{0}] HTTP server terminated", EngineID);
        }


        /// <summary>
        /// Add an HTTP request handler.
        /// </summary>
        /// <param name="handler">OSHttpHandler delegate</param>
        /// <param name="path">regex object for path matching</parm>
        /// <param name="headers">dictionary containing header names
        /// and regular expressions to match against header values</param>
        public void AddHandler(OSHttpHandler handler)
        {
            lock (_httpHandlers)
            {
                if (_httpHandlers.Contains(handler))
                {
                    _log.DebugFormat("[OSHttpServer] attempt to add already existing handler ignored");
                    return;
                }
                _httpHandlers.Add(handler);
            }
        }
    }
}
