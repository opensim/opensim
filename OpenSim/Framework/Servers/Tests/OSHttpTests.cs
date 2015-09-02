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
using System.Net;
using System.Net.Sockets;
using System.Text;
using HttpServer;
using HttpServer.FormDecoders;
using NUnit.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Tests.Common;

namespace OpenSim.Framework.Servers.Tests
{
    [TestFixture]
    public class OSHttpTests : OpenSimTestCase
    {
        // we need an IHttpClientContext for our tests
        public class TestHttpClientContext: IHttpClientContext
        {
            private bool _secured;
            public bool IsSecured
            {
                get { return _secured; }
            }
            public bool Secured
            {
                get { return _secured; }
            }

            public TestHttpClientContext(bool secured)
            {
                _secured = secured;
            }

            public void Disconnect(SocketError error) {}
            public void Respond(string httpVersion, HttpStatusCode statusCode, string reason, string body) {}
            public void Respond(string httpVersion, HttpStatusCode statusCode, string reason) {}
            public void Respond(string body) {}
            public void Send(byte[] buffer) {}
            public void Send(byte[] buffer, int offset, int size) {}
            public void Respond(string httpVersion, HttpStatusCode statusCode, string reason, string body, string contentType) {}
            public void Close() { }
            public bool EndWhenDone { get { return false;} set { return;}}

            public HTTPNetworkContext GiveMeTheNetworkStreamIKnowWhatImDoing()
            {
                return new HTTPNetworkContext();
            }

            public event EventHandler<DisconnectedEventArgs> Disconnected = delegate { };
            /// <summary>
            /// A request have been received in the context.
            /// </summary>
            public event EventHandler<RequestEventArgs> RequestReceived = delegate { };

            public bool CanSend { get { return true; } }
            public string RemoteEndPoint { get { return ""; } }
            public string RemoteEndPointAddress { get { return ""; } }
            public string RemoteEndPointPort { get { return ""; } }
        }

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
            public string HttpVersion 
            { 
                get { return _httpVersion; }
                set { _httpVersion = value; }
            }
            private string _httpVersion = null;
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

            public TestHttpRequest() {}

            public TestHttpRequest(string contentEncoding, string contentType, string userAgent, 
                                   string remoteAddr, string remotePort, string[] acceptTypes,
                                   ConnectionType connectionType, int contentLength, Uri uri) 
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

        public class TestHttpResponse: IHttpResponse
        {
            public Stream Body 
            {
                get { return _body; }

                set { _body = value; }
            }
            private Stream _body;

            public string ProtocolVersion 
            { 
                get { return _protocolVersion; }
                set { _protocolVersion = value; }
            }
            private string _protocolVersion;

            public bool Chunked 
            {
                get { return _chunked; }

                set { _chunked = value; }
            }
            private bool _chunked;

            public ConnectionType Connection 
            {
                get { return _connection; }

                set { _connection = value; }
            }
            private ConnectionType _connection;

            public Encoding Encoding 
            {
                get { return _encoding; }

                set { _encoding = value; }
            }
            private Encoding _encoding;

            public int KeepAlive 
            {
                get { return _keepAlive; }

                set { _keepAlive = value; }
            }
            private int _keepAlive;

            public HttpStatusCode Status 
            {
                get { return _status; }

                set { _status = value; }
            }
            private HttpStatusCode _status;

            public string Reason 
            {
                get { return _reason; }

                set { _reason = value; }
            }
            private string _reason;

            public long ContentLength 
            {
                get { return _contentLength; }

                set { _contentLength = value; }
            }
            private long _contentLength;

            public string ContentType 
            {
                get { return _contentType; }

                set { _contentType = value; }
            }
            private string _contentType;

            public bool HeadersSent 
            {
                get { return _headersSent; }
            }
            private bool _headersSent;

            public bool Sent 
            {
                get { return _sent; }
            }
            private bool _sent;

            public ResponseCookies Cookies 
            {
                get { return _cookies; }
            }
            private ResponseCookies _cookies = null;

            public TestHttpResponse()
            {
                _headersSent = false;
                _sent = false;
            }

            public void AddHeader(string name, string value) {}
            public void Send() 
            {
                if (!_headersSent) SendHeaders();
                if (_sent) throw new InvalidOperationException("stuff already sent");
                _sent = true;
            }

            public void SendBody(byte[] buffer, int offset, int count) 
            {
                if (!_headersSent) SendHeaders();
                _sent = true;
            }
            public void SendBody(byte[] buffer) 
            {
                if (!_headersSent) SendHeaders();
                _sent = true;
            }

            public void SendHeaders() 
            {
                if (_headersSent) throw new InvalidOperationException("headers already sent");
                _headersSent = true;
            }

            public void Redirect(Uri uri) {}
            public void Redirect(string url) {}
        }
       

        public OSHttpRequest req0;
        public OSHttpRequest req1;

        public OSHttpResponse rsp0;

        public IPEndPoint ipEP0;

        [TestFixtureSetUp]
        public void Init()
        {
            TestHttpRequest threq0 = new TestHttpRequest("utf-8", "text/xml", "OpenSim Test Agent", "192.168.0.1", "4711", 
                                                       new string[] {"text/xml"}, 
                                                       ConnectionType.KeepAlive, 4711, 
                                                       new Uri("http://127.0.0.1/admin/inventory/Dr+Who/Tardis"));
            threq0.Method = "GET";
            threq0.HttpVersion = HttpHelper.HTTP10;

            TestHttpRequest threq1 = new TestHttpRequest("utf-8", "text/xml", "OpenSim Test Agent", "192.168.0.1", "4711", 
                                                       new string[] {"text/xml"}, 
                                                       ConnectionType.KeepAlive, 4711, 
                                                       new Uri("http://127.0.0.1/admin/inventory/Dr+Who/Tardis?a=0&b=1&c=2"));
            threq1.Method = "POST";
            threq1.HttpVersion = HttpHelper.HTTP11;
            threq1.Headers["x-wuff"] = "wuffwuff";
            threq1.Headers["www-authenticate"] = "go away";
            
            req0 = new OSHttpRequest(new TestHttpClientContext(false), threq0);
            req1 = new OSHttpRequest(new TestHttpClientContext(false), threq1);

            rsp0 = new OSHttpResponse(new TestHttpResponse());

            ipEP0 = new IPEndPoint(IPAddress.Parse("192.168.0.1"), 4711);

        }

        [Test]
        public void T000_OSHttpRequest()
        {
            Assert.That(req0.HttpMethod, Is.EqualTo("GET"));
            Assert.That(req0.ContentType, Is.EqualTo("text/xml"));
            Assert.That(req0.ContentLength, Is.EqualTo(4711));

            Assert.That(req1.HttpMethod, Is.EqualTo("POST"));
        }

        [Test]
        public void T001_OSHttpRequestHeaderAccess()
        {
            Assert.That(req1.Headers["x-wuff"], Is.EqualTo("wuffwuff"));
            Assert.That(req1.Headers.Get("x-wuff"), Is.EqualTo("wuffwuff"));

            Assert.That(req1.Headers["www-authenticate"], Is.EqualTo("go away"));
            Assert.That(req1.Headers.Get("www-authenticate"), Is.EqualTo("go away"));

            Assert.That(req0.RemoteIPEndPoint, Is.EqualTo(ipEP0));
        }

        [Test]
        public void T002_OSHttpRequestUriParsing()
        {
            Assert.That(req0.RawUrl, Is.EqualTo("/admin/inventory/Dr+Who/Tardis"));
            Assert.That(req1.Url.ToString(), Is.EqualTo("http://127.0.0.1/admin/inventory/Dr+Who/Tardis?a=0&b=1&c=2"));
        }

        [Test]
        public void T100_OSHttpResponse()
        {
            rsp0.ContentType = "text/xml";
            Assert.That(rsp0.ContentType, Is.EqualTo("text/xml"));
        }
    }
}
