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
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using HttpServer;
using HttpServer.Exceptions;
using HttpServer.FormDecoders;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;

using OpenSim.Framework.Servers;

namespace OpenSim.Framework.Servers.Tests
{
    [TestFixture]
    public class OSHttpTests
    {
        // we need an IHttpClientContext for our tests
        public class TestHttpClientContext: HttpServer.IHttpClientContext
        {
            private bool _secured;
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
        }

        public class TestHttpRequest: HttpServer.IHttpRequest
        {
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
        }

        
        public OSHttpRequest r0;
        public OSHttpRequest r1;

        public IPEndPoint ipEP0;

        [TestFixtureSetUp]
        public void Init()
        {
            TestHttpRequest thr0 = new TestHttpRequest("utf-8", "text/xml", "OpenSim Test Agent", "192.168.0.1", "4711", 
                                                       new string[] {"text/xml"}, 
                                                       ConnectionType.KeepAlive, 4711, 
                                                       new Uri("http://127.0.0.1/admin/inventory/Dr+Who/Tardis"));
            thr0.Method = "GET";
            thr0.HttpVersion = HttpHelper.HTTP10;

            TestHttpRequest thr1 = new TestHttpRequest("utf-8", "text/xml", "OpenSim Test Agent", "192.168.0.1", "4711", 
                                                       new string[] {"text/xml"}, 
                                                       ConnectionType.KeepAlive, 4711, 
                                                       new Uri("http://127.0.0.1/admin/inventory/Dr+Who/Tardis?a=0&b=1&c=2"));
            thr1.Method = "POST";
            thr1.HttpVersion = HttpHelper.HTTP11;
            thr1.Headers["x-wuff"] = "wuffwuff";
            thr1.Headers["www-authenticate"] = "go away";
            
            r0 = new OSHttpRequest(new TestHttpClientContext(false), thr0);
            r1 = new OSHttpRequest(new TestHttpClientContext(false), thr1);

            ipEP0 = new IPEndPoint(IPAddress.Parse("192.168.0.1"), 4711);
        }

        [Test]
        public void T001_SimpleOSHttpRequest()
        {
            Assert.That(r0.HttpMethod, Is.EqualTo("GET"));
            Assert.That(r0.ContentType, Is.EqualTo("text/xml"));
            Assert.That(r0.ContentLength, Is.EqualTo(4711));

            Assert.That(r1.HttpMethod, Is.EqualTo("POST"));
        }

        [Test]
        public void T002_HeaderAccess()
        {
            Assert.That(r1.Headers["x-wuff"], Is.EqualTo("wuffwuff"));
            Assert.That(r1.Headers.Get("x-wuff"), Is.EqualTo("wuffwuff"));

            Assert.That(r1.Headers["www-authenticate"], Is.EqualTo("go away"));
            Assert.That(r1.Headers.Get("www-authenticate"), Is.EqualTo("go away"));

            Assert.That(r0.RemoteIPEndPoint, Is.EqualTo(ipEP0));
        }

        [Test]
        public void T003_UriParsing()
        {
            Assert.That(r0.RawUrl, Is.EqualTo("/admin/inventory/Dr+Who/Tardis"));
            Assert.That(r1.Url.ToString(), Is.EqualTo("http://127.0.0.1/admin/inventory/Dr+Who/Tardis?a=0&b=1&c=2"));
        }
    }
}
