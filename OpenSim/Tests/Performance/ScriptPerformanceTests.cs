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
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using log4net;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Scripting.WorldComm;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.XEngine;
using OpenSim.Tests.Common;

namespace OpenSim.Tests.Performance
{
    /// <summary>
    /// Script performance tests
    /// </summary>
    /// <remarks>
    /// Don't rely on the numbers given by these tests - they will vary a lot depending on what is already cached,
    /// how much memory is free, etc.  In some cases, later larger tests will apparently take less time than smaller
    /// earlier tests.
    /// </remarks>
    [TestFixture]
    public class ScriptPerformanceTests : OpenSimTestCase
    {
        private TestScene m_scene;
        private XEngine m_xEngine;
        private AutoResetEvent m_chatEvent = new AutoResetEvent(false);

        private int m_expectedChatMessages;
        private List<OSChatMessage> m_osChatMessagesReceived = new List<OSChatMessage>();

        [SetUp]
        public void Init()
        {
            //AppDomain.CurrentDomain.SetData("APPBASE", Environment.CurrentDirectory + "/bin");
//            Console.WriteLine(AppDomain.CurrentDomain.BaseDirectory);
            m_xEngine = new XEngine();

            // Necessary to stop serialization complaining
            WorldCommModule wcModule = new WorldCommModule();

            IniConfigSource configSource = new IniConfigSource();

            IConfig startupConfig = configSource.AddConfig("Startup");
            startupConfig.Set("DefaultScriptEngine", "XEngine");

            IConfig xEngineConfig = configSource.AddConfig("XEngine");
            xEngineConfig.Set("Enabled", "true");

            // These tests will not run with AppDomainLoading = true, at least on mono.  For unknown reasons, the call
            // to AssemblyResolver.OnAssemblyResolve fails.
            xEngineConfig.Set("AppDomainLoading", "false");

            m_scene = new SceneHelpers().SetupScene("My Test", UUID.Random(), 1000, 1000, configSource);
            SceneHelpers.SetupSceneModules(m_scene, configSource, m_xEngine, wcModule);

            m_scene.EventManager.OnChatFromWorld += OnChatFromWorld;
            m_scene.StartScripts();
        }

        [TearDown]
        public void TearDown()
        {
            m_scene.Close();
            m_scene = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        [Test]
        public void TestCompileAndStart100Scripts()
        {
            TestHelpers.InMethod();
            log4net.Config.XmlConfigurator.Configure();

            TestCompileAndStartScripts(100);
        }

        private void TestCompileAndStartScripts(int scriptsToCreate)
        {
            UUID userId = TestHelpers.ParseTail(0x1);

            m_expectedChatMessages = scriptsToCreate;
            int startingObjectIdTail = 0x100;

            GC.Collect();

            for (int idTail = startingObjectIdTail;idTail < startingObjectIdTail + scriptsToCreate; idTail++)
            {
                AddObjectAndScript(idTail, userId);
            }

            m_chatEvent.WaitOne(40000 + scriptsToCreate * 1000);

            Assert.That(m_osChatMessagesReceived.Count, Is.EqualTo(m_expectedChatMessages));

            foreach (OSChatMessage msg in m_osChatMessagesReceived)
                Assert.That(
                    msg.Message,
                    Is.EqualTo("Script running"),
                    string.Format(
                        "Message from {0} was {1} rather than {2}", msg.SenderUUID, msg.Message, "Script running"));
        }

        private void AddObjectAndScript(int objectIdTail, UUID userId)
        {
//            UUID itemId = TestHelpers.ParseTail(0x3);
            string itemName = string.Format("AddObjectAndScript() Item for object {0}", objectIdTail);

            SceneObjectGroup so = SceneHelpers.CreateSceneObject(1, userId, "AddObjectAndScriptPart_", objectIdTail);
            m_scene.AddNewSceneObject(so, true);

            InventoryItemBase itemTemplate = new InventoryItemBase();
//            itemTemplate.ID = itemId;
            itemTemplate.Name = itemName;
            itemTemplate.Folder = so.UUID;
            itemTemplate.InvType = (int)InventoryType.LSL;

            m_scene.RezNewScript(userId, itemTemplate);
        }

        private void OnChatFromWorld(object sender, OSChatMessage oscm)
        {
//            Console.WriteLine("Got chat [{0}]", oscm.Message);

            lock (m_osChatMessagesReceived)
            {
                m_osChatMessagesReceived.Add(oscm);

                if (m_osChatMessagesReceived.Count == m_expectedChatMessages)
                    m_chatEvent.Set();
            }
        }
    }
}