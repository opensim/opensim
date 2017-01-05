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
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using HttpServer;
using log4net;

namespace OpenSim.Framework.Servers.HttpServer
{
    public class OSHttpRequest : IOSHttpRequest
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected IHttpRequest _request = null;
        protected IHttpClientContext _context = null;

        public string[] AcceptTypes
        {
            get { return _request.AcceptTypes; }
        }

        public Encoding ContentEncoding
        {
            get { return _contentEncoding; }
        }
        private Encoding _contentEncoding;

        public long ContentLength
        {
            get { return _request.ContentLength; }
        }

        public long ContentLength64
        {
            get { return ContentLength; }
        }

        public string ContentType
        {
            get { return _contentType; }
        }
        private string _contentType;

        public HttpCookieCollection Cookies
        {
            get
            {
                RequestCookies cookies = _request.Cookies;
                HttpCookieCollection httpCookies = new HttpCookieCollection();
                foreach (RequestCookie cookie in cookies)
                    httpCookies.Add(new HttpCookie(cookie.Name, cookie.Value));
                return httpCookies;
            }
        }

        public bool HasEntityBody
        {
            get { return _request.ContentLength != 0; }
        }

        public NameValueCollection Headers
        {
            get { return _request.Headers; }
        }

        public string HttpMethod
        {
            get { return _request.Method; }
        }

        public Stream InputStream
        {
            get { return _request.Body; }
        }

        public bool IsSecured
        {
            get { return _context.IsSecured; }
        }

        public bool KeepAlive
        {
            get { return ConnectionType.KeepAlive == _request.Connection; }
        }

        public NameValueCollection QueryString
        {
            get { return _queryString; }
        }
        private NameValueCollection _queryString;

        public Hashtable Query
        {
            get { return _query; }
        }
        private Hashtable _query;

        /// <value>
        /// POST request values, if applicable
        /// </value>
//        public Hashtable Form { get; private set; }

        public string RawUrl
        {
            get { return _request.Uri.AbsolutePath; }
        }

        public IPEndPoint RemoteIPEndPoint
        {
            get { return _remoteIPEndPoint; }
        }
        private IPEndPoint _remoteIPEndPoint;

        public Uri Url
        {
            get { return _request.Uri; }
        }

        public string UserAgent
        {
            get { return _userAgent; }
        }
        private string _userAgent;

        internal IHttpRequest IHttpRequest
        {
            get { return _request; }
        }

        internal IHttpClientContext IHttpClientContext
        {
            get { return _context; }
        }

        /// <summary>
        /// Internal whiteboard for handlers to store temporary stuff
        /// into.
        /// </summary>
        internal Dictionary<string, object> Whiteboard
        {
            get { return _whiteboard; }
        }
        private Dictionary<string, object> _whiteboard = new Dictionary<string, object>();

        public OSHttpRequest() {}

        public OSHttpRequest(IHttpClientContext context, IHttpRequest req)
        {
            _request = req;
            _context = context;

            if (null != req.Headers["content-encoding"])
            {
                try
                {
                    _contentEncoding = Encoding.GetEncoding(_request.Headers["content-encoding"]);
                }
                catch (Exception)
                {
                    // ignore
                }
            }

            if (null != req.Headers["content-type"])
                _contentType = _request.Headers["content-type"];
            if (null != req.Headers["user-agent"])
                _userAgent = req.Headers["user-agent"];

            if (null != req.Headers["remote_addr"])
            {
                try
                {
                    IPAddress addr = IPAddress.Parse(req.Headers["remote_addr"]);
                    // sometimes req.Headers["remote_port"] returns a comma separated list, so use
                    // the first one in the list and log it
                    string[] strPorts = req.Headers["remote_port"].Split(new char[] { ',' });
                    if (strPorts.Length > 1)
                    {
                        _log.ErrorFormat("[OSHttpRequest]: format exception on addr/port {0}:{1}, ignoring",
                                     req.Headers["remote_addr"], req.Headers["remote_port"]);
                    }
                    int port = Int32.Parse(strPorts[0]);
                    _remoteIPEndPoint = new IPEndPoint(addr, port);
                }
                catch (FormatException)
                {
                    _log.ErrorFormat("[OSHttpRequest]: format exception on addr/port {0}:{1}, ignoring",
                                     req.Headers["remote_addr"], req.Headers["remote_port"]);
                }
            }

            _queryString = new NameValueCollection();
            _query = new Hashtable();
            try
            {
                foreach (HttpInputItem item in req.QueryString)
                {
                    try
                    {
                        _queryString.Add(item.Name, item.Value);
                        _query[item.Name] = item.Value;
                    }
                    catch (InvalidCastException)
                    {
                        _log.DebugFormat("[OSHttpRequest]: error parsing {0} query item, skipping it", item.Name);
                        continue;
                    }
                }
            }
            catch (Exception)
            {
                _log.ErrorFormat("[OSHttpRequest]: Error parsing querystring");
            }

//            Form = new Hashtable();
//            foreach (HttpInputItem item in req.Form)
//            {
//                _log.DebugFormat("[OSHttpRequest]: Got form item {0}={1}", item.Name, item.Value);
//                Form.Add(item.Name, item.Value);
//            }
        }

        public override string ToString()
        {
            StringBuilder me = new StringBuilder();
            me.Append(String.Format("OSHttpRequest: {0} {1}\n", HttpMethod, RawUrl));
            foreach (string k in Headers.AllKeys)
            {
                me.Append(String.Format("    {0}: {1}\n", k, Headers[k]));
            }
            if (null != RemoteIPEndPoint)
            {
                me.Append(String.Format("    IP: {0}\n", RemoteIPEndPoint));
            }

            return me.ToString();
        }
    }
}
