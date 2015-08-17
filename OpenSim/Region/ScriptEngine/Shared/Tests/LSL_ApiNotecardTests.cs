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
    /// Tests for notecard related functions in LSL
    /// </summary>
    [TestFixture]
    public class LSL_ApiNotecardTests : OpenSimTestCase
    {
        private Scene m_scene;
        private MockScriptEngine m_engine;

        private SceneObjectGroup m_so;
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

            m_engine = new MockScriptEngine();

            m_scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(m_scene, new IniConfigSource(), m_engine);

            m_so = SceneHelpers.AddSceneObject(m_scene);
            m_scriptItem = TaskInventoryHelpers.AddScript(m_scene.AssetService, m_so.RootPart);

            // This is disconnected from the actual script - the mock engine does not set up any LSL_Api atm.
            // Possibly this could be done and we could obtain it directly from the MockScriptEngine.
            m_lslApi = new LSL_Api();
            m_lslApi.Initialize(m_engine, m_so.RootPart, m_scriptItem, null);
        }

        [Test]
        public void TestLlGetNotecardLine()
        {
            TestHelpers.InMethod();

            string[] ncLines = { "One", "Twoè", "Three" };

            TaskInventoryItem ncItem 
                = TaskInventoryHelpers.AddNotecard(m_scene.AssetService, m_so.RootPart, "nc", "1", "10", string.Join("\n", ncLines));

            AssertValidNotecardLine(ncItem.Name, 0, ncLines[0]);
            AssertValidNotecardLine(ncItem.Name, 2, ncLines[2]);
            AssertValidNotecardLine(ncItem.Name, 3, ScriptBaseClass.EOF);
            AssertValidNotecardLine(ncItem.Name, 4, ScriptBaseClass.EOF);

            // XXX: Is this correct or do we really expect no dataserver event to fire at all?
            AssertValidNotecardLine(ncItem.Name, -1, "");
            AssertValidNotecardLine(ncItem.Name, -2, "");
        }

        [Test]
        public void TestLlGetNotecardLine_NoNotecard()
        {
            TestHelpers.InMethod();

            AssertInValidNotecardLine("nc", 0);
        }

        [Test]
        public void TestLlGetNotecardLine_NotANotecard()
        {
            TestHelpers.InMethod();

            TaskInventoryItem ncItem = TaskInventoryHelpers.AddScript(m_scene.AssetService, m_so.RootPart, "nc1", "Not important");

            AssertInValidNotecardLine(ncItem.Name, 0);
        }

        private void AssertValidNotecardLine(string ncName, int lineNumber, string assertLine)
        {
            string key = m_lslApi.llGetNotecardLine(ncName, lineNumber);
            Assert.That(key, Is.Not.EqualTo(UUID.Zero.ToString()));
        
            Assert.That(m_engine.PostedEvents.Count, Is.EqualTo(1));
            Assert.That(m_engine.PostedEvents.ContainsKey(m_scriptItem.ItemID));

            List<EventParams> events = m_engine.PostedEvents[m_scriptItem.ItemID];
            Assert.That(events.Count, Is.EqualTo(1));
            EventParams eventParams = events[0];

            Assert.That(eventParams.EventName, Is.EqualTo("dataserver"));
            Assert.That(eventParams.Params[0].ToString(), Is.EqualTo(key));
            Assert.That(eventParams.Params[1].ToString(), Is.EqualTo(assertLine));

            m_engine.ClearPostedEvents();
        }

        private void AssertInValidNotecardLine(string ncName, int lineNumber)
        {
            string key = m_lslApi.llGetNotecardLine(ncName, lineNumber);
            Assert.That(key, Is.EqualTo(UUID.Zero.ToString()));

            Assert.That(m_engine.PostedEvents.Count, Is.EqualTo(0));
        }

//        [Test]
//        public void TestLlReleaseUrl()
//        {
//            TestHelpers.InMethod();
//
//            m_lslApi.llRequestURL();
//            string returnedUri = m_engine.PostedEvents[m_scriptItem.ItemID][0].Params[2].ToString();
//
//            {
//                // Check that the initial number of URLs is correct
//                Assert.That(m_lslApi.llGetFreeURLs().value, Is.EqualTo(m_urlModule.TotalUrls - 1));
//            }
//
//            {
//                // Check releasing a non-url
//                m_lslApi.llReleaseURL("GARBAGE");
//                Assert.That(m_lslApi.llGetFreeURLs().value, Is.EqualTo(m_urlModule.TotalUrls - 1));
//            }
//
//            {
//                // Check releasing a non-existing url
//                m_lslApi.llReleaseURL("http://example.com");
//                Assert.That(m_lslApi.llGetFreeURLs().value, Is.EqualTo(m_urlModule.TotalUrls - 1));
//            }
//
//            {
//                // Check URL release
//                m_lslApi.llReleaseURL(returnedUri);
//                Assert.That(m_lslApi.llGetFreeURLs().value, Is.EqualTo(m_urlModule.TotalUrls));
//
//                HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(returnedUri);
//
//                bool gotExpectedException = false;
//
//                try
//                {
//                    using (HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse())
//                    {}
//                }
//                catch (WebException e)
//                {
//                    using (HttpWebResponse response = (HttpWebResponse)e.Response)
//                        gotExpectedException = response.StatusCode == HttpStatusCode.NotFound;
//                }
//
//                Assert.That(gotExpectedException, Is.True);
//            }
//
//            {
//                // Check releasing the same URL again
//                m_lslApi.llReleaseURL(returnedUri);
//                Assert.That(m_lslApi.llGetFreeURLs().value, Is.EqualTo(m_urlModule.TotalUrls));
//            }
//        }
//
//        [Test]
//        public void TestLlRequestUrl()
//        {
//            TestHelpers.InMethod();
//
//            string requestId = m_lslApi.llRequestURL();
//            Assert.That(requestId, Is.Not.EqualTo(UUID.Zero.ToString()));
//            string returnedUri;
//
//            {
//                // Check that URL is correctly set up
//                Assert.That(m_lslApi.llGetFreeURLs().value, Is.EqualTo(m_urlModule.TotalUrls - 1));
//
//                Assert.That(m_engine.PostedEvents.ContainsKey(m_scriptItem.ItemID));
//
//                List<EventParams> events = m_engine.PostedEvents[m_scriptItem.ItemID];
//                Assert.That(events.Count, Is.EqualTo(1));
//                EventParams eventParams = events[0];
//                Assert.That(eventParams.EventName, Is.EqualTo("http_request"));
//
//                UUID returnKey;
//                string rawReturnKey = eventParams.Params[0].ToString();
//                string method = eventParams.Params[1].ToString();
//                returnedUri = eventParams.Params[2].ToString();
//
//                Assert.That(UUID.TryParse(rawReturnKey, out returnKey), Is.True);
//                Assert.That(method, Is.EqualTo(ScriptBaseClass.URL_REQUEST_GRANTED));
//                Assert.That(Uri.IsWellFormedUriString(returnedUri, UriKind.Absolute), Is.True);
//            }
//
//            {
//                // Check that request to URL works.
//                string testResponse = "Hello World";
//
//                m_engine.ClearPostedEvents();                
//                m_engine.PostEventHook 
//                    += (itemId, evp) => m_lslApi.llHTTPResponse(evp.Params[0].ToString(), 200, testResponse);
//
////                Console.WriteLine("Trying {0}", returnedUri);
//                HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(returnedUri);
//
//                AssertHttpResponse(returnedUri, testResponse);
//
//                Assert.That(m_engine.PostedEvents.ContainsKey(m_scriptItem.ItemID));
//
//                List<EventParams> events = m_engine.PostedEvents[m_scriptItem.ItemID];
//                Assert.That(events.Count, Is.EqualTo(1));
//                EventParams eventParams = events[0];
//                Assert.That(eventParams.EventName, Is.EqualTo("http_request"));
//
//                UUID returnKey;
//                string rawReturnKey = eventParams.Params[0].ToString();
//                string method = eventParams.Params[1].ToString();
//                string body = eventParams.Params[2].ToString();
//
//                Assert.That(UUID.TryParse(rawReturnKey, out returnKey), Is.True);
//                Assert.That(method, Is.EqualTo("GET"));
//                Assert.That(body, Is.EqualTo(""));
//            }
//        }
//
//        private void AssertHttpResponse(string uri, string expectedResponse)
//        {
//            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(uri);
//
//            using (HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse())
//            {
//                using (Stream stream = webResponse.GetResponseStream())
//                {
//                    using (StreamReader reader = new StreamReader(stream))
//                    {
//                        Assert.That(reader.ReadToEnd(), Is.EqualTo(expectedResponse));
//                    }
//                }
//            }
//        }
    }
}