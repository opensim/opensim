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
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using log4net.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Scripting.HttpRequest;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.Scripting.HttpRequest.Tests
{
    class TestWebRequestCreate : IWebRequestCreate
    {       
        public TestWebRequest NextRequest { get; set; }

        public WebRequest Create(Uri uri)
        {
//            NextRequest.RequestUri = uri;

            return NextRequest;

//            return new TestWebRequest(new SerializationInfo(typeof(TestWebRequest), new FormatterConverter()), new StreamingContext());
        }
    }

    class TestWebRequest : WebRequest
    {
        public override string ContentType { get; set; }
        public override string Method { get; set; }

        public Func<IAsyncResult, WebResponse> OnEndGetResponse { get; set; }

        public TestWebRequest() : base() 
        {
//            Console.WriteLine("created");
        }

//        public TestWebRequest(SerializationInfo serializationInfo, StreamingContext streamingContext) 
//            : base(serializationInfo, streamingContext) 
//        {
//            Console.WriteLine("created");
//        }

        public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
        {
//            Console.WriteLine("bish");
            TestAsyncResult tasr = new TestAsyncResult();
            callback(tasr);

            return tasr;
        }

        public override WebResponse EndGetResponse(IAsyncResult asyncResult)
        {
//            Console.WriteLine("bosh");
            return OnEndGetResponse(asyncResult);
        }
    }

    class TestHttpWebResponse : HttpWebResponse
    {
        public string Response { get; set; }

#pragma warning disable 0618
        public TestHttpWebResponse(SerializationInfo serializationInfo, StreamingContext streamingContext) 
            : base(serializationInfo, streamingContext) {}
#pragma warning restore 0618

        public override Stream GetResponseStream()
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(Response));
        }
    }

    class TestAsyncResult : IAsyncResult
    {
        WaitHandle m_wh = new ManualResetEvent(true);

        object IAsyncResult.AsyncState 
        {
            get {
                throw new System.NotImplementedException ();
            }
        }

        WaitHandle IAsyncResult.AsyncWaitHandle 
        {
            get { return m_wh; }
        }

        bool IAsyncResult.CompletedSynchronously 
        {
            get { return false; }
        }

        bool IAsyncResult.IsCompleted 
        {
            get { return true; }
        }
    }

    /// <summary>
    /// Test script http request code.
    /// </summary>
    /// <remarks>
    /// This class uses some very hacky workarounds in order to mock HttpWebResponse which are Mono dependent (though
    /// alternative code can be written to make this work for Windows).  However, the value of being able to
    /// regression test this kind of code is very high.
    /// </remarks>
    [TestFixture]
    public class ScriptsHttpRequestsTests : OpenSimTestCase
    {
        /// <summary>
        /// Test what happens when we get a 404 response from a call.
        /// </summary>
//        [Test]
        public void Test404Response()
        {
            TestHelpers.InMethod();
            TestHelpers.EnableLogging();

            if (!Util.IsPlatformMono)
                Assert.Ignore("Ignoring test since can only currently run on Mono");           

            string rawResponse = "boom";

            TestWebRequestCreate twrc = new TestWebRequestCreate();

            TestWebRequest twr = new TestWebRequest();
            //twr.OnEndGetResponse += ar => new TestHttpWebResponse(null, new StreamingContext());
            twr.OnEndGetResponse += ar => 
            {
                SerializationInfo si = new SerializationInfo(typeof(HttpWebResponse), new FormatterConverter());
                StreamingContext sc = new StreamingContext();
//                WebHeaderCollection headers = new WebHeaderCollection();
//                si.AddValue("m_HttpResponseHeaders", headers);
                si.AddValue("uri", new Uri("test://arrg"));
//                si.AddValue("m_Certificate", null);
                si.AddValue("version", HttpVersion.Version11);
                si.AddValue("statusCode", HttpStatusCode.NotFound);
                si.AddValue("contentLength", 0);
                si.AddValue("method", "GET");
                si.AddValue("statusDescription", "Not Found");
                si.AddValue("contentType", null);
                si.AddValue("cookieCollection", new CookieCollection());

                TestHttpWebResponse thwr = new TestHttpWebResponse(si, sc);
                thwr.Response = rawResponse;

                throw new WebException("no message", null, WebExceptionStatus.ProtocolError, thwr);
            };

            twrc.NextRequest = twr;

            WebRequest.RegisterPrefix("test", twrc);
            HttpRequestClass hr = new HttpRequestClass();
            hr.Url = "test://something";
            hr.SendRequest();

            Assert.That(hr.Status, Is.EqualTo((int)HttpStatusCode.NotFound));
            Assert.That(hr.ResponseBody, Is.EqualTo(rawResponse));
        }
    }
}