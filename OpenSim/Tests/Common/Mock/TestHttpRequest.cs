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
using System.Collections.Specialized;
using System.IO;
using HttpServer;
using HttpServer.FormDecoders;

namespace OpenSim.Tests.Common
{
/*
    public class TestHttpRequest: IHttpRequest
    {
        private string _uriPath;
        public bool BodyIsComplete
        {
            get { return true; }
        }
        public string[] AcceptTypes
        {
            get {return _acceptTypes; }
        }
        private string[] _acceptTypes;
        public Stream Body
        {
            get { return _body; }
            set { _body = value;}
        }
        private Stream _body;
        public ConnectionType Connection
        {
            get { return _connection; }
            set { _connection = value; }
        }
        private ConnectionType _connection;
        public int ContentLength
        {
            get { return _contentLength; }
            set { _contentLength = value; }
        }
        private int _contentLength;
        public NameValueCollection Headers
        {
            get { return _headers; }
        }
        private NameValueCollection _headers = new NameValueCollection();

        public string HttpVersion { get; set; }

        public string Method
        {
            get { return _method; }
            set { _method = value; }
        }
        private string _method = null;
        public HttpInput QueryString
        {
            get { return _queryString;  }
        }
        private HttpInput _queryString = null;
        public Uri Uri
        {
            get { return _uri; }
            set { _uri = value; }
        }
        private Uri _uri = null;
        public string[] UriParts
        {
            get { return _uri.Segments; }
        }
        public HttpParam Param
        {
            get { return null; }
        }
        public HttpForm Form
        {
            get { return null; }
        }
        public bool IsAjax
        {
            get { return false; }
        }
        public RequestCookies Cookies
        {
            get { return null; }
        }

        public TestHttpRequest()
        {
            HttpVersion = "HTTP/1.1";
        }

        public TestHttpRequest(string contentEncoding, string contentType, string userAgent,
                               string remoteAddr, string remotePort, string[] acceptTypes,
                               ConnectionType connectionType, int contentLength, Uri uri) : base()
        {
            _headers["content-encoding"] = contentEncoding;
            _headers["content-type"] = contentType;
            _headers["user-agent"] = userAgent;
            _headers["remote_addr"] = remoteAddr;
            _headers["remote_port"] = remotePort;

            _acceptTypes = acceptTypes;
            _connection = connectionType;
            _contentLength = contentLength;
            _uri = uri;
        }

        public void DecodeBody(FormDecoderProvider providers) {}
        public void SetCookies(RequestCookies cookies) {}
        public void AddHeader(string name, string value)
        {
            _headers.Add(name, value);
        }
        public int AddToBody(byte[] bytes, int offset, int length)
        {
            return 0;
        }
        public void Clear() {}

        public object Clone()
        {
            TestHttpRequest clone = new TestHttpRequest();
            clone._acceptTypes = _acceptTypes;
            clone._connection = _connection;
            clone._contentLength = _contentLength;
            clone._uri = _uri;
            clone._headers = new NameValueCollection(_headers);

            return clone;
        }
        public IHttpResponse CreateResponse(IHttpClientContext context)
        {
            return new HttpResponse(context, this);
        }
        /// <summary>
        /// Path and query (will be merged with the host header) and put in Uri
        /// </summary>
        /// <see cref="Uri"/>
        public string UriPath
        {
            get { return _uriPath; }
            set
            {
                _uriPath = value;

            }
        }
    }
*/
}