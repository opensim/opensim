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
using System.Threading;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.ScriptEngine.Shared.Tests
{
    [TestFixture]
    public class LSL_ApiUserTests : OpenSimTestCase
    {
        private Scene m_scene;
        private MockScriptEngine m_engine;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            m_engine = new MockScriptEngine();

            m_scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(m_scene, m_engine);
        }

        [Test]
        public void TestLlRequestAgentDataOnline()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);

            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(m_scene, userId);

            SceneObjectPart part = SceneHelpers.AddSceneObject(m_scene).RootPart;
            TaskInventoryItem scriptItem = TaskInventoryHelpers.AddScript(m_scene.AssetService, part);

            LSL_Api apiGrp1 = new LSL_Api();
            apiGrp1.Initialize(m_engine, part, scriptItem);

            // Initially long timeout to test cache
            apiGrp1.LlRequestAgentDataCacheTimeoutMs = 20000;

            // Offline test
            {
                apiGrp1.llRequestAgentData(userId.ToString(), ScriptBaseClass.DATA_ONLINE);

                Assert.That(m_engine.PostedEvents.ContainsKey(scriptItem.ItemID));

                List<EventParams> events = m_engine.PostedEvents[scriptItem.ItemID];
                Assert.That(events.Count, Is.EqualTo(1));
                EventParams eventParams = events[0];
                Assert.That(eventParams.EventName, Is.EqualTo("dataserver"));

                string data = eventParams.Params[1].ToString();
                Assert.AreEqual(0, int.Parse(data));

                m_engine.PostedEvents.Clear();
            }

            // Online test.  Should get the 'wrong' result because of caching.
            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, ua1);

            {
                apiGrp1.llRequestAgentData(userId.ToString(), ScriptBaseClass.DATA_ONLINE);

                Assert.That(m_engine.PostedEvents.ContainsKey(scriptItem.ItemID));

                List<EventParams> events = m_engine.PostedEvents[scriptItem.ItemID];
                Assert.That(events.Count, Is.EqualTo(1));
                EventParams eventParams = events[0];
                Assert.That(eventParams.EventName, Is.EqualTo("dataserver"));

                string data = eventParams.Params[1].ToString();
                Assert.AreEqual(0, int.Parse(data));

                m_engine.PostedEvents.Clear();
            }

            apiGrp1.LlRequestAgentDataCacheTimeoutMs = 20;

            // Make absolutely sure that we should trigger cache timeout.
            Thread.Sleep(apiGrp1.LlRequestAgentDataCacheTimeoutMs + 50);

            {
                apiGrp1.llRequestAgentData(userId.ToString(), ScriptBaseClass.DATA_ONLINE);

                Assert.That(m_engine.PostedEvents.ContainsKey(scriptItem.ItemID));

                List<EventParams> events = m_engine.PostedEvents[scriptItem.ItemID];
                Assert.That(events.Count, Is.EqualTo(1));
                EventParams eventParams = events[0];
                Assert.That(eventParams.EventName, Is.EqualTo("dataserver"));

                string data = eventParams.Params[1].ToString();
                Assert.AreEqual(1, int.Parse(data));

                m_engine.PostedEvents.Clear();
            }

            m_scene.CloseAgent(userId, false);

            Thread.Sleep(apiGrp1.LlRequestAgentDataCacheTimeoutMs + 50);

            {
                apiGrp1.llRequestAgentData(userId.ToString(), ScriptBaseClass.DATA_ONLINE);

                Assert.That(m_engine.PostedEvents.ContainsKey(scriptItem.ItemID));

                List<EventParams> events = m_engine.PostedEvents[scriptItem.ItemID];
                Assert.That(events.Count, Is.EqualTo(1));
                EventParams eventParams = events[0];
                Assert.That(eventParams.EventName, Is.EqualTo("dataserver"));

                string data = eventParams.Params[1].ToString();
                Assert.AreEqual(0, int.Parse(data));

                m_engine.PostedEvents.Clear();
            }
        }
    }
}
