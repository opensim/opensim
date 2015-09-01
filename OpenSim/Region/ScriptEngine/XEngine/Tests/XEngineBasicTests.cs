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
using OpenSim.Region.CoreModules.Scripting.WorldComm;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.ScriptEngine.XEngine.Tests
{
    /// <summary>
    /// Basic XEngine tests.
    /// </summary>
    [TestFixture]
    public class XEngineBasicTests : OpenSimTestCase
    {
        private TestScene m_scene;
        private XEngine m_xEngine;
        private AutoResetEvent m_chatEvent = new AutoResetEvent(false);
        private OSChatMessage m_osChatMessageReceived;

        [TestFixtureSetUp]
        public void Init()
        {
            //AppDomain.CurrentDomain.SetData("APPBASE", Environment.CurrentDirectory + "/bin");
//            Console.WriteLine(AppDomain.CurrentDomain.BaseDirectory);
            m_xEngine = new XEngine();

            IniConfigSource configSource = new IniConfigSource();
            
            IConfig startupConfig = configSource.AddConfig("Startup");
            startupConfig.Set("DefaultScriptEngine", "XEngine");

            IConfig xEngineConfig = configSource.AddConfig("XEngine");
            xEngineConfig.Set("Enabled", "true");
            xEngineConfig.Set("StartDelay", "0");

            // These tests will not run with AppDomainLoading = true, at least on mono.  For unknown reasons, the call
            // to AssemblyResolver.OnAssemblyResolve fails.
            xEngineConfig.Set("AppDomainLoading", "false");

            m_scene = new SceneHelpers().SetupScene("My Test", UUID.Random(), 1000, 1000, configSource);
            SceneHelpers.SetupSceneModules(m_scene, configSource, m_xEngine);
            m_scene.StartScripts();
        }

        /// <summary>
        /// Test compilation and starting of a script.
        /// </summary>
        /// <remarks>
        /// This is a less than ideal regression test since it involves an asynchronous operation (in this case,
        /// compilation of the script).
        /// </remarks>
        [Test]
        public void TestCompileAndStartScript()
        {
            TestHelpers.InMethod();
            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);
//            UUID objectId = TestHelpers.ParseTail(0x100);
//            UUID itemId = TestHelpers.ParseTail(0x3);
            string itemName = "TestStartScript() Item";

            SceneObjectGroup so = SceneHelpers.CreateSceneObject(1, userId, "TestStartScriptPart_", 0x100);
            m_scene.AddNewSceneObject(so, true);

            InventoryItemBase itemTemplate = new InventoryItemBase();
//            itemTemplate.ID = itemId;
            itemTemplate.Name = itemName;
            itemTemplate.Folder = so.UUID;
            itemTemplate.InvType = (int)InventoryType.LSL;

            m_scene.EventManager.OnChatFromWorld += OnChatFromWorld;

            SceneObjectPart partWhereRezzed = m_scene.RezNewScript(userId, itemTemplate);

            m_chatEvent.WaitOne(60000);

            Assert.That(m_osChatMessageReceived, Is.Not.Null, "No chat message received in TestStartScript()");
            Assert.That(m_osChatMessageReceived.Message, Is.EqualTo("Script running"));

            bool running;
            TaskInventoryItem scriptItem = partWhereRezzed.Inventory.GetInventoryItem(itemName);
            Assert.That(
                SceneObjectPartInventory.TryGetScriptInstanceRunning(m_scene, scriptItem, out running), Is.True);
            Assert.That(running, Is.True);
        }

        private void OnChatFromWorld(object sender, OSChatMessage oscm)
        {
//            Console.WriteLine("Got chat [{0}]", oscm.Message);

            m_osChatMessageReceived = oscm;
            m_chatEvent.Set();
        }
    }
}