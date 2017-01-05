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
using System.Text;
using log4net;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.CoreModules.Scripting.LSLHttp;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.ScriptEngine.Shared.Tests
{
    /// <summary>
    /// Tests for HTTP related functions in LSL
    /// </summary>
    [TestFixture]
    public class LSL_ApiHttpTests : OpenSimTestCase
    {
        private Scene m_scene;
        private MockScriptEngine m_engine;
        private UrlModule m_urlModule;

        private TaskInventoryItem m_scriptItem;
        private LSL_Api m_lslApi;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
            Util.FireAndForgetMethod = FireAndForgetMethod.RegressionTest;
        }

        [TestFixtureTearDown]
        public void TestFixureTearDown()
        {
            // We must set this back afterwards, otherwise later tests will fail since they're expecting multiple
            // threads.  Possibly, later tests should be rewritten so none of them require async stuff (which regression
            // tests really shouldn't).
            Util.FireAndForgetMethod = Util.DefaultFireAndForgetMethod;
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // This is an unfortunate bit of clean up we have to do because MainServer manages things through static
            // variables and the VM is not restarted between tests.
            uint port = 9999;
            MainServer.RemoveHttpServer(port);

            BaseHttpServer server = new BaseHttpServer(port, false, 0, "");
            MainServer.AddHttpServer(server);
            MainServer.Instance = server;

            server.Start();

            m_engine = new MockScriptEngine();
            m_urlModule = new UrlModule();

            m_scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(m_scene, new IniConfigSource(), m_engine, m_urlModule);

            SceneObjectGroup so = SceneHelpers.AddSceneObject(m_scene);
            m_scriptItem = TaskInventoryHelpers.AddScript(m_scene.AssetService, so.RootPart);

            // This is disconnected from the actual script - the mock engine does not set up any LSL_Api atm.
            // Possibly this could be done and we could obtain it directly from the MockScriptEngine.
            m_lslApi = new LSL_Api();
            m_lslApi.Initialize(m_engine, so.RootPart, m_scriptItem);
        }

        [TearDown]
        public void TearDown()
        {
            MainServer.Instance.Stop();
        }

        [Test]
        public void TestLlReleaseUrl()
        {
            TestHelpers.InMethod();

            m_lslApi.llRequestURL();
            string returnedUri = m_engine.PostedEvents[m_scriptItem.ItemID][0].Params[2].ToString();

            {
                // Check that the initial number of URLs is correct
                Assert.That(m_lslApi.llGetFreeURLs().value, Is.EqualTo(m_urlModule.TotalUrls - 1));
            }

            {
                // Check releasing a non-url
                m_lslApi.llReleaseURL("GARBAGE");
                Assert.That(m_lslApi.llGetFreeURLs().value, Is.EqualTo(m_urlModule.TotalUrls - 1));
            }

            {
                // Check releasing a non-existing url
                m_lslApi.llReleaseURL("http://example.com");
                Assert.That(m_lslApi.llGetFreeURLs().value, Is.EqualTo(m_urlModule.TotalUrls - 1));
            }

            {
                // Check URL release
                m_lslApi.llReleaseURL(returnedUri);
                Assert.That(m_lslApi.llGetFreeURLs().value, Is.EqualTo(m_urlModule.TotalUrls));

                HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(returnedUri);

                bool gotExpectedException = false;

                try
                {
                    using (HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse())
                    {}
                }
                catch (WebException e)
                {
//                    using (HttpWebResponse response = (HttpWebResponse)e.Response)
//                        gotExpectedException = response.StatusCode == HttpStatusCode.NotFound;
                    gotExpectedException = true;
                }

                Assert.That(gotExpectedException, Is.True);
            }

            {
                // Check releasing the same URL again
                m_lslApi.llReleaseURL(returnedUri);
                Assert.That(m_lslApi.llGetFreeURLs().value, Is.EqualTo(m_urlModule.TotalUrls));
            }
        }

        [Test]
        public void TestLlRequestUrl()
        {
            TestHelpers.InMethod();

            string requestId = m_lslApi.llRequestURL();
            Assert.That(requestId, Is.Not.EqualTo(UUID.Zero.ToString()));
            string returnedUri;

            {
                // Check that URL is correctly set up
                Assert.That(m_lslApi.llGetFreeURLs().value, Is.EqualTo(m_urlModule.TotalUrls - 1));

                Assert.That(m_engine.PostedEvents.ContainsKey(m_scriptItem.ItemID));

                List<EventParams> events = m_engine.PostedEvents[m_scriptItem.ItemID];
                Assert.That(events.Count, Is.EqualTo(1));
                EventParams eventParams = events[0];
                Assert.That(eventParams.EventName, Is.EqualTo("http_request"));

                UUID returnKey;
                string rawReturnKey = eventParams.Params[0].ToString();
                string method = eventParams.Params[1].ToString();
                returnedUri = eventParams.Params[2].ToString();

                Assert.That(UUID.TryParse(rawReturnKey, out returnKey), Is.True);
                Assert.That(method, Is.EqualTo(ScriptBaseClass.URL_REQUEST_GRANTED));
                Assert.That(Uri.IsWellFormedUriString(returnedUri, UriKind.Absolute), Is.True);
            }

            {
                // Check that request to URL works.
                string testResponse = "Hello World";

                m_engine.ClearPostedEvents();
                m_engine.PostEventHook
                    += (itemId, evp) => m_lslApi.llHTTPResponse(evp.Params[0].ToString(), 200, testResponse);

//                Console.WriteLine("Trying {0}", returnedUri);

                AssertHttpResponse(returnedUri, testResponse);

                Assert.That(m_engine.PostedEvents.ContainsKey(m_scriptItem.ItemID));

                List<EventParams> events = m_engine.PostedEvents[m_scriptItem.ItemID];
                Assert.That(events.Count, Is.EqualTo(1));
                EventParams eventParams = events[0];
                Assert.That(eventParams.EventName, Is.EqualTo("http_request"));

                UUID returnKey;
                string rawReturnKey = eventParams.Params[0].ToString();
                string method = eventParams.Params[1].ToString();
                string body = eventParams.Params[2].ToString();

                Assert.That(UUID.TryParse(rawReturnKey, out returnKey), Is.True);
                Assert.That(method, Is.EqualTo("GET"));
                Assert.That(body, Is.EqualTo(""));
            }
        }

        private void AssertHttpResponse(string uri, string expectedResponse)
        {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(uri);

            using (HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                using (Stream stream = webResponse.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        Assert.That(reader.ReadToEnd(), Is.EqualTo(expectedResponse));
                    }
                }
            }
        }
    }
}
