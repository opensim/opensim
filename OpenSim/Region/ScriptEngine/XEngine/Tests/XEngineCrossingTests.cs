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
using System.Threading;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Framework.EntityTransfer;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Tests.Common;

namespace OpenSim.Region.ScriptEngine.XEngine.Tests
{
    /// <summary>
    /// XEngine tests connected with crossing scripts between regions.
    /// </summary>
    [TestFixture]
    public class XEngineCrossingTests : OpenSimTestCase
    {
        [TestFixtureSetUp]
        public void FixtureInit()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
            Util.FireAndForgetMethod = FireAndForgetMethod.RegressionTest;
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            // We must set this back afterwards, otherwise later tests will fail since they're expecting multiple
            // threads.  Possibly, later tests should be rewritten so none of them require async stuff (which regression
            // tests really shouldn't).
            Util.FireAndForgetMethod = Util.DefaultFireAndForgetMethod;
        }

        /// <summary>
        /// Test script state preservation when a script crosses between regions on the same simulator.
        /// </summary>
        [Test]
        public void TestScriptCrossOnSameSimulator()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);
            int sceneObjectIdTail = 0x2;

            EntityTransferModule etmA = new EntityTransferModule();
            EntityTransferModule etmB = new EntityTransferModule();
            LocalSimulationConnectorModule lscm = new LocalSimulationConnectorModule();
            XEngine xEngineA = new XEngine();
            XEngine xEngineB = new XEngine();
            xEngineA.DebugLevel = 1;
            xEngineB.DebugLevel = 1;

            IConfigSource configSource = new IniConfigSource();

            IConfig startupConfig = configSource.AddConfig("Startup");
            startupConfig.Set("DefaultScriptEngine", "XEngine");
            startupConfig.Set("TrustBinaries", "true");

            IConfig xEngineConfig = configSource.AddConfig("XEngine");
            xEngineConfig.Set("Enabled", "true");
            xEngineConfig.Set("StartDelay", "0");

            // These tests will not run with AppDomainLoading = true, at least on mono.  For unknown reasons, the call
            // to AssemblyResolver.OnAssemblyResolve fails.
            xEngineConfig.Set("AppDomainLoading", "false");

            IConfig modulesConfig = configSource.AddConfig("Modules");
            modulesConfig.Set("EntityTransferModule", etmA.Name);
            modulesConfig.Set("SimulationServices", lscm.Name);

            SceneHelpers sh = new SceneHelpers();
            TestScene sceneA = sh.SetupScene("sceneA", TestHelpers.ParseTail(0x100), 1000, 1000, configSource);
            TestScene sceneB = sh.SetupScene("sceneB", TestHelpers.ParseTail(0x200), 1000, 999, configSource);

            SceneHelpers.SetupSceneModules(new Scene[] { sceneA, sceneB }, configSource, lscm);
            SceneHelpers.SetupSceneModules(sceneA, configSource, etmA, xEngineA);
            SceneHelpers.SetupSceneModules(sceneB, configSource, etmB, xEngineB);
            sceneA.StartScripts();
            sceneB.StartScripts();

            SceneObjectGroup soSceneA = SceneHelpers.AddSceneObject(sceneA, 1, userId, "so1-", sceneObjectIdTail);
            soSceneA.AbsolutePosition = new Vector3(128, 10, 20);

            string soSceneAName = soSceneA.Name;
            string scriptItemSceneAName = "script1";

            // CREATE SCRIPT TODO
            InventoryItemBase scriptItemSceneA = new InventoryItemBase();
            //            itemTemplate.ID = itemId;
            scriptItemSceneA.Name = scriptItemSceneAName;
            scriptItemSceneA.Folder = soSceneA.UUID;
            scriptItemSceneA.InvType = (int)InventoryType.LSL;

            AutoResetEvent chatEvent = new AutoResetEvent(false);
            OSChatMessage messageReceived = null;
            sceneA.EventManager.OnChatFromWorld += (s, m) => { messageReceived = m; chatEvent.Set(); };

            sceneA.RezNewScript(userId, scriptItemSceneA, 
@"integer c = 0;

default
{    
    state_entry()
    {
        llSay(0, ""Script running"");
    }

    changed(integer change)
    {
        llSay(0, ""Changed"");
    }

    touch_start(integer n)
    {
        c = c + 1; 
        llSay(0, (string)c);
    }
}");

            chatEvent.WaitOne(60000);

            Assert.That(messageReceived, Is.Not.Null, "No chat message received.");
            Assert.That(messageReceived.Message, Is.EqualTo("Script running"));           

            {
                // XXX: Should not be doing this so directly.  Should call some variant of EventManager.touch() instead.
                DetectParams[] det = new DetectParams[1];
                det[0] = new DetectParams();
                det[0].Key = userId;
                det[0].Populate(sceneA);

                EventParams ep = new EventParams("touch_start", new Object[] { new LSL_Types.LSLInteger(1) }, det);

                messageReceived = null;
                chatEvent.Reset();
                xEngineA.PostObjectEvent(soSceneA.LocalId, ep);
                chatEvent.WaitOne(60000);

                Assert.That(messageReceived.Message, Is.EqualTo("1")); 
            }

            AutoResetEvent chatEventB = new AutoResetEvent(false);
            sceneB.EventManager.OnChatFromWorld += (s, m) => { messageReceived = m; chatEventB.Set(); };

            messageReceived = null;
            chatEventB.Reset();
            // Cross with a negative value
            soSceneA.AbsolutePosition = new Vector3(128, -10, 20);

            chatEventB.WaitOne(60000);
            Assert.That(messageReceived, Is.Not.Null, "No Changed message received.");
            Assert.That(messageReceived.Message, Is.Not.Null, "Changed message without content");
            Assert.That(messageReceived.Message, Is.EqualTo("Changed")); 

            // TEST sending event to moved prim and output
            {
                SceneObjectGroup soSceneB = sceneB.GetSceneObjectGroup(soSceneAName);
                TaskInventoryItem scriptItemSceneB = soSceneB.RootPart.Inventory.GetInventoryItem(scriptItemSceneAName);

                // XXX: Should not be doing this so directly.  Should call some variant of EventManager.touch() instead.
                DetectParams[] det = new DetectParams[1];
                det[0] = new DetectParams();
                det[0].Key = userId;
                det[0].Populate(sceneB);

                EventParams ep = new EventParams("touch_start", new Object[] { new LSL_Types.LSLInteger(1) }, det);

                messageReceived = null;
                chatEventB.Reset();
                xEngineB.PostObjectEvent(soSceneB.LocalId, ep);
                chatEventB.WaitOne(60000);

                Assert.That(messageReceived.Message, Is.EqualTo("2")); 
            }
        }
    }
}