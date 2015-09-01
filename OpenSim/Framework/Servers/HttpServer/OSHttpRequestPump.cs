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

// #define DEBUGGING

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;
using HttpServer;

namespace OpenSim.Framework.Servers.HttpServer
{
    /// <summary>
    /// An OSHttpRequestPump fetches incoming OSHttpRequest objects
    /// from the OSHttpRequestQueue and feeds them to all subscribed
    /// parties. Each OSHttpRequestPump encapsulates one thread to do
    /// the work and there is a fixed number of pumps for each
    /// OSHttpServer object.
    /// </summary>
    public class OSHttpRequestPump
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected OSHttpServer _server;
        protected OSHttpRequestQueue _queue;
        protected Thread _engine;

        private int _id;

        public string EngineID
        {
            get { return String.Format("{0} pump {1}", _server.EngineID, _id); }
        }

        public OSHttpRequestPump(OSHttpServer server, OSHttpRequestQueue queue, int id)
        {
            _server = server;
            _queue = queue;
            _id = id;

            _engine = new Thread(new ThreadStart(Engine));
            _engine.IsBackground = true;
            _engine.Start();
            _engine.Name = string.Format ("Engine:{0}",EngineID);

            ThreadTracker.Add(_engine);
        }

        public static OSHttpRequestPump[] Pumps(OSHttpServer server, OSHttpRequestQueue queue, int poolSize)
        {
            OSHttpRequestPump[] pumps = new OSHttpRequestPump[poolSize];
            for (int i = 0; i < pumps.Length; i++)
            {
                pumps[i] = new OSHttpRequestPump(server, queue, i);
            }

            return pumps;
        }

        public void Start()
        {
            _engine = new Thread(new ThreadStart(Engine));
            _engine.IsBackground = true;
            _engine.Start();
            _engine.Name = string.Format ("Engine:{0}",EngineID);

            ThreadTracker.Add(_engine);
        }

        public void Engine()
        {
            OSHttpRequest req = null;

            while (true)
            {
                try
                {
                    // dequeue an OSHttpRequest from OSHttpServer's
                    // request queue
                    req = _queue.Dequeue();

                    // get a copy of the list of registered handlers
                    List<OSHttpHandler> handlers = _server.OSHttpHandlers;

                    // prune list and have it sorted from most
                    // specific to least specific
                    handlers = MatchHandlers(req, handlers);

                    // process req: we try each handler in turn until
                    // we are either out of handlers or get back a
                    // Pass or Done
                    OSHttpHandlerResult rc = OSHttpHandlerResult.Unprocessed;
                    foreach (OSHttpHandler h in handlers)
                    {
                        rc = h.Process(req);

                        // Pass: handler did not process the request,
                        // try next handler
                        if (OSHttpHandlerResult.Pass == rc) continue;

                        // Handled: handler has processed the request
                        if (OSHttpHandlerResult.Done == rc) break;

                        // hmm, something went wrong
                        throw new Exception(String.Format("[{0}] got unexpected OSHttpHandlerResult {1}", EngineID, rc));
                    }

                    if (OSHttpHandlerResult.Unprocessed == rc)
                    {
                        _log.InfoFormat("[{0}] OSHttpHandler: no handler registered for {1}", EngineID, req);

                        // set up response header
                        OSHttpResponse resp = new OSHttpResponse(req);
                        resp.StatusCode = (int)OSHttpStatusCode.ClientErrorNotFound;
                        resp.StatusDescription = String.Format("no handler on call for {0}", req);
                        resp.ContentType = "text/html";

                        // add explanatory message
                        StreamWriter body = new StreamWriter(resp.Body);
                        body.WriteLine("<html>");
                        body.WriteLine("<header><title>Ooops...</title><header>");
                        body.WriteLine(String.Format("<body><p>{0}</p></body>", resp.StatusDescription));
                        body.WriteLine("</html>");
                        body.Flush();

                        // and ship it back
                        resp.Send();
                    }
                }
                catch (Exception e)
                {
                    _log.DebugFormat("[{0}] OSHttpHandler problem: {1}", EngineID, e.ToString());
                    _log.ErrorFormat("[{0}] OSHttpHandler problem: {1}", EngineID, e.Message);
                }
            }
        }

        protected List<OSHttpHandler> MatchHandlers(OSHttpRequest req, List<OSHttpHandler> handlers)
        {
            Dictionary<OSHttpHandler, int> scoredHandlers = new Dictionary<OSHttpHandler, int>();

            _log.DebugFormat("[{0}] MatchHandlers for {1}", EngineID, req);
            foreach (OSHttpHandler h in handlers)
            {
                // initial anchor
                scoredHandlers[h] = 0;

                // first, check whether IPEndPointWhitelist applies
                // and, if it does, whether client is on that white
                // list.
                if (null != h.IPEndPointWhitelist)
                {
                    // TODO: following code requires code changes to
                    // HttpServer.HttpRequest to become functional

                    IPEndPoint remote = req.RemoteIPEndPoint;
                    if (null != remote)
                    {
                        Match epm = h.IPEndPointWhitelist.Match(remote.ToString());
                        if (!epm.Success)
                        {
                            scoredHandlers.Remove(h);
                            continue;
                        }
                    }
                }

                if (null != h.Method)
                {
                    Match m = h.Method.Match(req.HttpMethod);
                    if (!m.Success)
                    {
                        scoredHandlers.Remove(h);
                        continue;
                    }
                    scoredHandlers[h]++;
                }

                // whitelist ok, now check path
                if (null != h.Path)
                {
                    Match m = h.Path.Match(req.RawUrl);
                    if (!m.Success)
                    {
                        scoredHandlers.Remove(h);
                        continue;
                    }
                    scoredHandlers[h] += m.ToString().Length;
                }

                // whitelist & path ok, now check query string
                if (null != h.Query)
                {
                    int queriesMatch = MatchOnNameValueCollection(req.QueryString, h.Query);
                    if (0 == queriesMatch)
                    {
                        _log.DebugFormat("[{0}] request {1}", EngineID, req);
                        _log.DebugFormat("[{0}] dropping handler {1}", EngineID, h);

                        scoredHandlers.Remove(h);
                        continue;
                    }
                    scoredHandlers[h] +=  queriesMatch;
                }

                // whitelist, path, query string ok, now check headers
                if (null != h.Headers)
                {
                    int headersMatch = MatchOnNameValueCollection(req.Headers, h.Headers);
                    if (0 == headersMatch)
                    {
                        _log.DebugFormat("[{0}] request {1}", EngineID, req);
                        _log.DebugFormat("[{0}] dropping handler {1}", EngineID, h);

                        scoredHandlers.Remove(h);
                        continue;
                    }
                    scoredHandlers[h] +=  headersMatch;
                }
            }

            List<OSHttpHandler> matchingHandlers = new List<OSHttpHandler>(scoredHandlers.Keys);
            matchingHandlers.Sort(delegate(OSHttpHandler x, OSHttpHandler y)
                                  {
                                      return scoredHandlers[x] - scoredHandlers[y];
                                  });
            LogDumpHandlerList(matchingHandlers);
            return matchingHandlers;
        }

        protected int MatchOnNameValueCollection(NameValueCollection collection, Dictionary<string, Regex> regexs)
        {
            int matched = 0;

            foreach (string tag in regexs.Keys)
            {
                // do we have a header "tag"?
                if (null == collection[tag])
                {
                    return 0;
                }

                // does the content of collection[tag] match
                // the supplied regex?
                Match cm = regexs[tag].Match(collection[tag]);
                if (!cm.Success)
                {
                    return 0;
                }

                // ok: matches
                matched++;
                continue;
            }

            return matched;
        }

        [ConditionalAttribute("DEBUGGING")]
        private void LogDumpHandlerList(List<OSHttpHandler> l)
        {
            _log.DebugFormat("[{0}] OSHttpHandlerList dump:", EngineID);
            foreach (OSHttpHandler h in l)
                _log.DebugFormat("    ", h.ToString());
        }
    }
}
