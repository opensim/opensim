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