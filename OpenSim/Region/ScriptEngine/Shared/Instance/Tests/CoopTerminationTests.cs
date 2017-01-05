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
using OpenSim.Region.ScriptEngine.XEngine;
using OpenSim.Tests.Common;

namespace OpenSim.Region.ScriptEngine.Shared.Instance.Tests
{
    /// <summary>
    /// Test that co-operative script thread termination is working correctly.
    /// </summary>
    [TestFixture]
    public class CoopTerminationTests : OpenSimTestCase
    {
        private TestScene m_scene;
        private OpenSim.Region.ScriptEngine.XEngine.XEngine m_xEngine;

        private AutoResetEvent m_chatEvent;
        private AutoResetEvent m_stoppedEvent;

        private OSChatMessage m_osChatMessageReceived;

        /// <summary>
        /// Number of chat messages received so far.  Reset before each test.
        /// </summary>
        private int m_chatMessagesReceived;

        /// <summary>
        /// Number of chat messages expected.  m_chatEvent is not fired until this number is reached or exceeded.
        /// </summary>
        private int m_chatMessagesThreshold;

        [SetUp]
        public void Init()
        {
            m_osChatMessageReceived = null;
            m_chatMessagesReceived = 0;
            m_chatMessagesThreshold = 0;
            m_chatEvent = new AutoResetEvent(false);
            m_stoppedEvent = new AutoResetEvent(false);

            //AppDomain.CurrentDomain.SetData("APPBASE", Environment.CurrentDirectory + "/bin");
//            Console.WriteLine(AppDomain.CurrentDomain.BaseDirectory);
            m_xEngine = new OpenSim.Region.ScriptEngine.XEngine.XEngine();
            m_xEngine.DebugLevel = 1;

            IniConfigSource configSource = new IniConfigSource();

            IConfig startupConfig = configSource.AddConfig("Startup");
            startupConfig.Set("DefaultScriptEngine", "XEngine");

            IConfig xEngineConfig = configSource.AddConfig("XEngine");
            xEngineConfig.Set("Enabled", "true");
            xEngineConfig.Set("StartDelay", "0");

            // These tests will not run with AppDomainLoading = true, at least on mono.  For unknown reasons, the call
            // to AssemblyResolver.OnAssemblyResolve fails.
            xEngineConfig.Set("AppDomainLoading", "false");

            xEngineConfig.Set("ScriptStopStrategy", "co-op");

            // Make sure loops aren't actually being terminated by a script delay wait.
            xEngineConfig.Set("ScriptDelayFactor", 0);

            // This is really just set for debugging the test.
            xEngineConfig.Set("WriteScriptSourceToDebugFile", true);

            // Set to false if we need to debug test so the old scripts don't get wiped before each separate test
//            xEngineConfig.Set("DeleteScriptsOnStartup", false);

            // This is not currently used at all for co-op termination.  Bumping up to demonstrate that co-op termination
            // has an effect - without it tests will fail due to a 120 second wait for the event to finish.
            xEngineConfig.Set("WaitForEventCompletionOnScriptStop", 120000);

            m_scene = new SceneHelpers().SetupScene("My Test", TestHelpers.ParseTail(0x9999), 1000, 1000, configSource);
            SceneHelpers.SetupSceneModules(m_scene, configSource, m_xEngine);
            m_scene.StartScripts();
        }

        /// <summary>
        /// Test co-operative termination on derez of an object containing a script with a long-running event.
        /// </summary>
        /// <remarks>
        /// TODO: Actually compiling the script is incidental to this test.  Really want a way to compile test scripts
        /// within the build itself.
        /// </remarks>
        [Test]
        public void TestStopOnLongSleep()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script =
@"default
{
    state_entry()
    {
        llSay(0, ""Thin Lizzy"");
        llSleep(60);
    }
}";

            TestStop(script);
        }

        [Test]
        public void TestNoStopOnSingleStatementForLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script =
@"default
{
    state_entry()
    {
        integer i = 0;
        for (i = 0; i <= 1; i++) llSay(0, ""Iter "" + (string)i);
    }
}";

            TestSingleStatementNoStop(script);
        }

        [Test]
        public void TestStopOnLongSingleStatementForLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script =
@"default
{
    state_entry()
    {
        integer i = 0;
        llSay(0, ""Thin Lizzy"");

        for (i = 0; i < 2147483647; i++) llSay(0, ""Iter "" + (string)i);
    }
}";

            TestStop(script);
        }

        [Test]
        public void TestStopOnLongCompoundStatementForLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script =
@"default
{
    state_entry()
    {
        integer i = 0;
        llSay(0, ""Thin Lizzy"");

        for (i = 0; i < 2147483647; i++)
        {
            llSay(0, ""Iter "" + (string)i);
        }
    }
}";

            TestStop(script);
        }

        [Test]
        public void TestNoStopOnSingleStatementWhileLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script =
@"default
{
    state_entry()
    {
        integer i = 0;
        while (i < 2) llSay(0, ""Iter "" + (string)i++);
    }
}";

            TestSingleStatementNoStop(script);
        }

        [Test]
        public void TestStopOnLongSingleStatementWhileLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script =
@"default
{
    state_entry()
    {
        integer i = 0;
        llSay(0, ""Thin Lizzy"");

        while (1 == 1)
            llSay(0, ""Iter "" + (string)i++);
    }
}";

            TestStop(script);
        }

        [Test]
        public void TestStopOnLongCompoundStatementWhileLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script =
@"default
{
    state_entry()
    {
        integer i = 0;
        llSay(0, ""Thin Lizzy"");

        while (1 == 1)
        {
            llSay(0, ""Iter "" + (string)i++);
        }
    }
}";

            TestStop(script);
        }

        [Test]
        public void TestNoStopOnSingleStatementDoWhileLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script =
@"default
{
    state_entry()
    {
        integer i = 0;

        do llSay(0, ""Iter "" + (string)i++);
        while (i < 2);
    }
}";

            TestSingleStatementNoStop(script);
        }

        [Test]
        public void TestStopOnLongSingleStatementDoWhileLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script =
@"default
{
    state_entry()
    {
        integer i = 0;
        llSay(0, ""Thin Lizzy"");

        do llSay(0, ""Iter "" + (string)i++);
        while (1 == 1);
    }
}";

            TestStop(script);
        }

        [Test]
        public void TestStopOnLongCompoundStatementDoWhileLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script =
@"default
{
    state_entry()
    {
        integer i = 0;
        llSay(0, ""Thin Lizzy"");

        do
        {
            llSay(0, ""Iter "" + (string)i++);
        } while (1 == 1);
    }
}";

            TestStop(script);
        }

        [Test]
        public void TestStopOnInfiniteJumpLoop()
        {
            TestHelpers.InMethod();
            TestHelpers.EnableLogging();

            string script =
@"default
{
    state_entry()
    {
        integer i = 0;
        llSay(0, ""Thin Lizzy"");

        @p1;
        llSay(0, ""Iter "" + (string)i++);
        jump p1;
    }
}";

            TestStop(script);
        }

        // Disabling for now as these are not particularly useful tests (since they fail due to stack overflow before
        // termination can even be tried.
//        [Test]
        public void TestStopOnInfiniteUserFunctionCallLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script =
@"
integer i = 0;

ufn1()
{
  llSay(0, ""Iter ufn1() "" + (string)i++);
  ufn1();
}

default
{
    state_entry()
    {
        integer i = 0;
        llSay(0, ""Thin Lizzy"");

        ufn1();
    }
}";

            TestStop(script);
        }

        // Disabling for now as these are not particularly useful tests (since they fail due to stack overflow before
        // termination can even be tried.
//        [Test]
        public void TestStopOnInfiniteManualEventCallLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script =
@"default
{
    state_entry()
    {
        integer i = 0;
        llSay(0, ""Thin Lizzy"");

        llSay(0, ""Iter"" + (string)i++);
        default_event_state_entry();
    }
}";

            TestStop(script);
        }

        private SceneObjectPart CreateScript(string script, string itemName, UUID userId)
        {
//            UUID objectId = TestHelpers.ParseTail(0x100);
//            UUID itemId = TestHelpers.ParseTail(0x3);

            SceneObjectGroup so
                = SceneHelpers.CreateSceneObject(1, userId, string.Format("Object for {0}", itemName), 0x100);
            m_scene.AddNewSceneObject(so, true);

            InventoryItemBase itemTemplate = new InventoryItemBase();
//            itemTemplate.ID = itemId;
            itemTemplate.Name = itemName;
            itemTemplate.Folder = so.UUID;
            itemTemplate.InvType = (int)InventoryType.LSL;

            m_scene.EventManager.OnChatFromWorld += OnChatFromWorld;

            return m_scene.RezNewScript(userId, itemTemplate, script);
        }

        private void TestSingleStatementNoStop(string script)
        {
            // In these tests we expect to see at least 2 chat messages to confirm that the loop is working properly.
            m_chatMessagesThreshold = 2;

            UUID userId = TestHelpers.ParseTail(0x1);
//            UUID objectId = TestHelpers.ParseTail(0x100);
//            UUID itemId = TestHelpers.ParseTail(0x3);
            string itemName = "TestNoStop";

            SceneObjectPart partWhereRezzed = CreateScript(script, itemName, userId);

            // Wait for the script to start the event before we try stopping it.
            m_chatEvent.WaitOne(60000);

            if (m_osChatMessageReceived == null)
                Assert.Fail("Script did not start");
            else
                Assert.That(m_chatMessagesReceived, Is.EqualTo(2));

            bool running;
            TaskInventoryItem scriptItem = partWhereRezzed.Inventory.GetInventoryItem(itemName);
            Assert.That(
                SceneObjectPartInventory.TryGetScriptInstanceRunning(m_scene, scriptItem, out running), Is.True);
            Assert.That(running, Is.True);
        }

        private void TestStop(string script)
        {
            // In these tests we're only interested in the first message to confirm that the script has started.
            m_chatMessagesThreshold = 1;

            UUID userId = TestHelpers.ParseTail(0x1);
//            UUID objectId = TestHelpers.ParseTail(0x100);
//            UUID itemId = TestHelpers.ParseTail(0x3);
            string itemName = "TestStop";

            SceneObjectPart partWhereRezzed = CreateScript(script, itemName, userId);
            TaskInventoryItem rezzedItem = partWhereRezzed.Inventory.GetInventoryItem(itemName);

            // Wait for the script to start the event before we try stopping it.
            m_chatEvent.WaitOne(60000);

            if (m_osChatMessageReceived != null)
                Console.WriteLine("Script started with message [{0}]", m_osChatMessageReceived.Message);
            else
                Assert.Fail("Script did not start");

            // FIXME: This is a very poor way of trying to avoid a low-probability race condition where the script
            // executes llSay() but has not started the next statement before we try to stop it.
            Thread.Sleep(1000);

            // We need a way of carrying on if StopScript() fail, since it won't return if the script isn't actually
            // stopped.  This kind of multi-threading is far from ideal in a regression test.
            new Thread(() => { m_xEngine.StopScript(rezzedItem.ItemID); m_stoppedEvent.Set(); }).Start();

            if (!m_stoppedEvent.WaitOne(30000))
                Assert.Fail("Script did not co-operatively stop.");

            bool running;
            TaskInventoryItem scriptItem = partWhereRezzed.Inventory.GetInventoryItem(itemName);
            Assert.That(
                SceneObjectPartInventory.TryGetScriptInstanceRunning(m_scene, scriptItem, out running), Is.True);
            Assert.That(running, Is.False);
        }

        private void OnChatFromWorld(object sender, OSChatMessage oscm)
        {
            Console.WriteLine("Got chat [{0}]", oscm.Message);
            m_osChatMessageReceived = oscm;

            if (++m_chatMessagesReceived >= m_chatMessagesThreshold)
            {
                m_scene.EventManager.OnChatFromWorld -= OnChatFromWorld;
                m_chatEvent.Set();
            }
        }
    }
}