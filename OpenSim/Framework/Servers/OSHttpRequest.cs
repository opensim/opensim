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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.IO;
using System.Text;
using HttpServer;

namespace OpenSim.Framework.Servers
{
    public class OSHttpRequest
    {
        public string[] AcceptTypes
        {
            get { return _acceptTypes; }
        }
        private string[] _acceptTypes;

        public Encoding ContentEncoding
        {
            get { return _contentEncoding; }
        }
        private Encoding _contentEncoding;

        public long ContentLength
        {
            get { return _contentLength64; }
        }
        private long _contentLength64;

        public long ContentLength64
        {
            get { return ContentLength; }
        }

        public string ContentType
        {
            get { return _contentType; }
        }
        private string _contentType;

        // public CookieCollection Cookies
        // {
        //     get { return _cookies; }
        // }
        // private CookieCollection _cookies;

        public NameValueCollection Headers
        {
            get { return _headers; }
        }
        private NameValueCollection _headers;

        public string HttpMethod
        {
            get { return _httpMethod; }
        }
        private string _httpMethod;

        public Stream InputStream
        {
            get { return _inputStream; }
        }
        private Stream _inputStream;

        // public bool IsSecureConnection
        // {
        //     get { return _isSecureConnection; }
        // }
        // private bool _isSecureConnection;

        // public bool IsAuthenticated
        // {
        //     get { return _isAuthenticated; }
        // }
        // private bool _isAuthenticated;

        public bool HasEntityBody
        {
            get { return _hasbody; }
        }
        private bool _hasbody;

        public bool KeepAlive
        {
            get { return _keepAlive; }
        }
        private bool _keepAlive;

        public string RawUrl
        {
            get { return _rawUrl; }
        }
        private string _rawUrl;

        public Uri Url
        {
            get { return _url; }
        }
        private Uri _url;

        public string UserAgent
        {
            get { return _userAgent; }
        }
        private string _userAgent;

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

        public IPEndPoint RemoteIPEndPoint
        {
            get { return _ipEndPoint; }
        }
        private IPEndPoint _ipEndPoint;

        // internal HttpRequest HttpRequest
        // {
        //     get { return _request; }
        // }
        // private HttpRequest _request;

        // internal HttpClientContext HttpClientContext
        // {
        //     get { return _context; }
        // }
        // private HttpClientContext _context;

        /// <summary>
        /// Internal whiteboard for handlers to store temporary stuff
        /// into.
        /// </summary>
        internal Dictionary<string, object> Whiteboard
        {
            get { return _whiteboard; }
        }
        private Dictionary<string, object> _whiteboard = new Dictionary<string, object>();

        public OSHttpRequest()
        {
        }

        public OSHttpRequest(HttpListenerRequest req)
        {
            _acceptTypes = req.AcceptTypes;
            _contentEncoding = req.ContentEncoding;
            _contentLength64 = req.ContentLength64;
            _contentType = req.ContentType;
            _headers = req.Headers;
            _httpMethod = req.HttpMethod;
            _hasbody = req.HasEntityBody;
            _inputStream = req.InputStream;
            _keepAlive = req.KeepAlive;
            _rawUrl = req.RawUrl;
            _url = req.Url;
            _queryString = req.QueryString;
            _userAgent = req.UserAgent;
            _ipEndPoint = req.RemoteEndPoint;

            // _cookies = req.Cookies;
            // _isSecureConnection = req.IsSecureConnection;
            // _isAuthenticated = req.IsAuthenticated;
        }

         public OSHttpRequest(HttpServer.IHttpClientContext context, HttpServer.IHttpRequest req)
         {
             //_context = context;
             HttpServer.IHttpRequest _request = req;

             _acceptTypes = req.AcceptTypes;
             if (null != req.Headers["content-encoding"])
                 _contentEncoding = Encoding.GetEncoding(_request.Headers["content-encoding"]);
             _contentLength64 = req.ContentLength;
             if (null != req.Headers["content-type"])
                 _contentType = _request.Headers["content-type"];
             _headers = req.Headers;
             _httpMethod = req.Method;
             _hasbody = req.ContentLength != 0;
             _inputStream = req.Body;
             _keepAlive = ConnectionType.KeepAlive == req.Connection;
             _rawUrl = req.Uri.AbsolutePath;
             _url = req.Uri;
             if (null != req.Headers["user-agent"])
                 _userAgent = req.Headers["user-agent"];
             _queryString = new NameValueCollection();
             _query = new Hashtable();
             try
             {
                 foreach (KeyValuePair<string, HttpInputItem> q in req.QueryString)
                 {
                     try
                     {
                         _queryString.Add(q.Key, q.Value.Value);
                         _query[q.Key] = q.Value.Value;
                     }
                     catch (InvalidCastException)
                     {
                         System.Console.WriteLine("[OSHttpRequest]: Errror parsing querystring..  but it was recoverable..  skipping on to the next one");
                         continue;
                     }
                 }
             }
             catch (Exception)
             {
                 System.Console.WriteLine("[OSHttpRequest]: Errror parsing querystring");
             }
             // TODO: requires change to HttpServer.HttpRequest
             _ipEndPoint = null;

             // _cookies = req.Cookies;
             // _isSecureConnection = req.IsSecureConnection;
             // _isAuthenticated = req.IsAuthenticated;
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
                me.Append(String.Format("    IP: {0}\n", RemoteIPEndPoint.ToString()));
            }

            return me.ToString();
        }
    }
}
