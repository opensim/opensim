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
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Scripting.ScriptModuleComms;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.OptionalModules.Scripting.JsonStore.Tests
{
    /// <summary>
    /// Tests for inventory functions in LSL
    /// </summary>
    [TestFixture]
    public class JsonStoreScriptModuleTests : OpenSimTestCase
    {
        private Scene m_scene;
        private MockScriptEngine m_engine;
        private ScriptModuleCommsModule m_smcm;
        private JsonStoreScriptModule m_jssm;

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

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            IConfigSource configSource = new IniConfigSource();
            IConfig jsonStoreConfig = configSource.AddConfig("JsonStore");
            jsonStoreConfig.Set("Enabled", "true");

            m_engine = new MockScriptEngine();
            m_smcm = new ScriptModuleCommsModule();
            JsonStoreModule jsm = new JsonStoreModule();
            m_jssm = new JsonStoreScriptModule();

            m_scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(m_scene, configSource, m_engine, m_smcm, jsm, m_jssm);

            try
            {
                m_smcm.RegisterScriptInvocation(this, "DummyTestMethod");
            }
            catch (ArgumentException)
            {
                Assert.Ignore("Ignoring test since running on .NET 3.5 or earlier.");
            }

            // XXX: Unfortunately, ICommsModule currently has no way of deregistering methods.
        }

        private object InvokeOp(string name, params object[] args)
        {
            return InvokeOpOnHost(name, UUID.Zero, args);
        }

        private object InvokeOpOnHost(string name, UUID hostId, params object[] args)
        {
            return m_smcm.InvokeOperation(hostId, UUID.Zero, name, args);
        }

        [Test]
        public void TestJsonCreateStore()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            // Test blank store
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{}");
                Assert.That(storeId, Is.Not.EqualTo(UUID.Zero));
            }

            // Test single element store
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ 'Hello' : 'World' }");
                Assert.That(storeId, Is.Not.EqualTo(UUID.Zero));
            }

            // Test with an integer value
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ 'Hello' : 42.15 }");
                Assert.That(storeId, Is.Not.EqualTo(UUID.Zero));

                string value = (string)InvokeOp("JsonGetValue", storeId, "Hello");
                Assert.That(value, Is.EqualTo("42.15"));
            }

            // Test with an array as the root node
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "[ 'one', 'two', 'three' ]");
                Assert.That(storeId, Is.Not.EqualTo(UUID.Zero));

                string value = (string)InvokeOp("JsonGetValue", storeId, "[1]");
                Assert.That(value, Is.EqualTo("two"));
            }
        }

        [Test]
        public void TestJsonDestroyStore()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ 'Hello' : 'World' }");
            int dsrv = (int)InvokeOp("JsonDestroyStore", storeId);

            Assert.That(dsrv, Is.EqualTo(1));

            int tprv = (int)InvokeOp("JsonGetNodeType", storeId, "Hello");
            Assert.That(tprv, Is.EqualTo(JsonStoreScriptModule.JSON_NODETYPE_UNDEF));
        }

        [Test]
        public void TestJsonDestroyStoreNotExists()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID fakeStoreId = TestHelpers.ParseTail(0x500);

            int dsrv = (int)InvokeOp("JsonDestroyStore", fakeStoreId);

            Assert.That(dsrv, Is.EqualTo(0));
        }

        [Test]
        public void TestJsonGetValue()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ 'Hello' : { 'World' : 'Two' } }");

            {
                string value = (string)InvokeOp("JsonGetValue", storeId, "Hello.World");
                Assert.That(value, Is.EqualTo("Two"));
            }

            // Test get of path section instead of leaf
            {
                string value = (string)InvokeOp("JsonGetValue", storeId, "Hello");
                Assert.That(value, Is.EqualTo(""));
            }

            // Test get of non-existing value
            {
                string fakeValueGet = (string)InvokeOp("JsonGetValue", storeId, "foo");
                Assert.That(fakeValueGet, Is.EqualTo(""));
            }

            // Test get from non-existing store
            {
                UUID fakeStoreId = TestHelpers.ParseTail(0x500);
                string fakeStoreValueGet = (string)InvokeOp("JsonGetValue", fakeStoreId, "Hello");
                Assert.That(fakeStoreValueGet, Is.EqualTo(""));
            }
        }

        [Test]
        public void TestJsonGetJson()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ 'Hello' : { 'World' : 'Two' } }");

            {
                string value = (string)InvokeOp("JsonGetJson", storeId, "Hello.World");
                Assert.That(value, Is.EqualTo("'Two'"));
            }

            // Test get of path section instead of leaf
            {
                string value = (string)InvokeOp("JsonGetJson", storeId, "Hello");
                Assert.That(value, Is.EqualTo("{\"World\":\"Two\"}"));
            }

            // Test get of non-existing value
            {
                string fakeValueGet = (string)InvokeOp("JsonGetJson", storeId, "foo");
                Assert.That(fakeValueGet, Is.EqualTo(""));
            }

            // Test get from non-existing store
            {
                UUID fakeStoreId = TestHelpers.ParseTail(0x500);
                string fakeStoreValueGet = (string)InvokeOp("JsonGetJson", fakeStoreId, "Hello");
                Assert.That(fakeStoreValueGet, Is.EqualTo(""));
            }
        }

//        [Test]
//        public void TestJsonTakeValue()
//        {
//            TestHelpers.InMethod();
////            TestHelpers.EnableLogging();
//
//            UUID storeId
//                = (UUID)m_smcm.InvokeOperation(
//                    UUID.Zero, UUID.Zero, "JsonCreateStore", new object[] { "{ 'Hello' : 'World' }" });
//
//            string value
//                = (string)m_smcm.InvokeOperation(
//                    UUID.Zero, UUID.Zero, "JsonTakeValue", new object[] { storeId, "Hello" });
//
//            Assert.That(value, Is.EqualTo("World"));
//
//            string value2
//                = (string)m_smcm.InvokeOperation(
//                    UUID.Zero, UUID.Zero, "JsonGetValue", new object[] { storeId, "Hello" });
//
//            Assert.That(value, Is.Null);
//        }

        [Test]
        public void TestJsonRemoveValue()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            // Test remove of node in object pointing to a string
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ 'Hello' : 'World' }");

                int returnValue = (int)InvokeOp( "JsonRemoveValue", storeId, "Hello");
                Assert.That(returnValue, Is.EqualTo(1));

                int result = (int)InvokeOp("JsonGetNodeType", storeId, "Hello");
                Assert.That(result, Is.EqualTo(JsonStoreScriptModule.JSON_NODETYPE_UNDEF));

                string returnValue2 = (string)InvokeOp("JsonGetValue", storeId, "Hello");
                Assert.That(returnValue2, Is.EqualTo(""));
            }

            // Test remove of node in object pointing to another object
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ 'Hello' : { 'World' : 'Wally' } }");

                int returnValue = (int)InvokeOp( "JsonRemoveValue", storeId, "Hello");
                Assert.That(returnValue, Is.EqualTo(1));

                int result = (int)InvokeOp("JsonGetNodeType", storeId, "Hello");
                Assert.That(result, Is.EqualTo(JsonStoreScriptModule.JSON_NODETYPE_UNDEF));

                string returnValue2 = (string)InvokeOp("JsonGetJson", storeId, "Hello");
                Assert.That(returnValue2, Is.EqualTo(""));
            }

            // Test remove of node in an array
            {
                UUID storeId
                    = (UUID)InvokeOp("JsonCreateStore", "{ 'Hello' : [ 'value1', 'value2' ] }");

                int returnValue = (int)InvokeOp( "JsonRemoveValue", storeId, "Hello[0]");
                Assert.That(returnValue, Is.EqualTo(1));

                int result = (int)InvokeOp("JsonGetNodeType", storeId, "Hello[0]");
                Assert.That(result, Is.EqualTo(JsonStoreScriptModule.JSON_NODETYPE_VALUE));

                result = (int)InvokeOp("JsonGetNodeType", storeId, "Hello[1]");
                Assert.That(result, Is.EqualTo(JsonStoreScriptModule.JSON_NODETYPE_UNDEF));

                string stringReturnValue = (string)InvokeOp("JsonGetValue", storeId, "Hello[0]");
                Assert.That(stringReturnValue, Is.EqualTo("value2"));

                stringReturnValue = (string)InvokeOp("JsonGetJson", storeId, "Hello[1]");
                Assert.That(stringReturnValue, Is.EqualTo(""));
            }

            // Test remove of non-existing value
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ 'Hello' : 'World' }");

                int fakeValueRemove = (int)InvokeOp("JsonRemoveValue", storeId, "Cheese");
                Assert.That(fakeValueRemove, Is.EqualTo(0));
            }

            {
                // Test get from non-existing store
                UUID fakeStoreId = TestHelpers.ParseTail(0x500);
                int fakeStoreValueRemove = (int)InvokeOp("JsonRemoveValue", fakeStoreId, "Hello");
                Assert.That(fakeStoreValueRemove, Is.EqualTo(0));
            }
        }

//        [Test]
//        public void TestJsonTestPath()
//        {
//            TestHelpers.InMethod();
////            TestHelpers.EnableLogging();
//
//            UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ 'Hello' : { 'World' : 'One' } }");
//
//            {
//                int result = (int)InvokeOp("JsonTestPath", storeId, "Hello.World");
//                Assert.That(result, Is.EqualTo(1));
//            }
//
//            // Test for path which does not resolve to a value.
//            {
//                int result = (int)InvokeOp("JsonTestPath", storeId, "Hello");
//                Assert.That(result, Is.EqualTo(0));
//            }
//
//            {
//                int result2 = (int)InvokeOp("JsonTestPath", storeId, "foo");
//                Assert.That(result2, Is.EqualTo(0));
//            }
//
//            // Test with fake store
//            {
//                UUID fakeStoreId = TestHelpers.ParseTail(0x500);
//                int fakeStoreValueRemove = (int)InvokeOp("JsonTestPath", fakeStoreId, "Hello");
//                Assert.That(fakeStoreValueRemove, Is.EqualTo(0));
//            }
//        }

//        [Test]
//        public void TestJsonTestPathJson()
//        {
//            TestHelpers.InMethod();
////            TestHelpers.EnableLogging();
//
//            UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ 'Hello' : { 'World' : 'One' } }");
//
//            {
//                int result = (int)InvokeOp("JsonTestPathJson", storeId, "Hello.World");
//                Assert.That(result, Is.EqualTo(1));
//            }
//
//            // Test for path which does not resolve to a value.
//            {
//                int result = (int)InvokeOp("JsonTestPathJson", storeId, "Hello");
//                Assert.That(result, Is.EqualTo(1));
//            }
//
//            {
//                int result2 = (int)InvokeOp("JsonTestPathJson", storeId, "foo");
//                Assert.That(result2, Is.EqualTo(0));
//            }
//
//            // Test with fake store
//            {
//                UUID fakeStoreId = TestHelpers.ParseTail(0x500);
//                int fakeStoreValueRemove = (int)InvokeOp("JsonTestPathJson", fakeStoreId, "Hello");
//                Assert.That(fakeStoreValueRemove, Is.EqualTo(0));
//            }
//        }

        [Test]
        public void TestJsonGetArrayLength()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ 'Hello' : { 'World' : [ 'one', 2 ] } }");

            {
                int result = (int)InvokeOp("JsonGetArrayLength", storeId, "Hello.World");
                Assert.That(result, Is.EqualTo(2));
            }

            // Test path which is not an array
            {
                int result = (int)InvokeOp("JsonGetArrayLength", storeId, "Hello");
                Assert.That(result, Is.EqualTo(-1));
            }

            // Test fake path
            {
                int result = (int)InvokeOp("JsonGetArrayLength", storeId, "foo");
                Assert.That(result, Is.EqualTo(-1));
            }

            // Test fake store
            {
                UUID fakeStoreId = TestHelpers.ParseTail(0x500);
                int result = (int)InvokeOp("JsonGetArrayLength", fakeStoreId, "Hello.World");
                Assert.That(result, Is.EqualTo(-1));
            }
        }

        [Test]
        public void TestJsonGetNodeType()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ 'Hello' : { 'World' : [ 'one', 2 ] } }");

            {
                int result = (int)InvokeOp("JsonGetNodeType", storeId, ".");
                Assert.That(result, Is.EqualTo(JsonStoreScriptModule.JSON_NODETYPE_OBJECT));
            }

            {
                int result = (int)InvokeOp("JsonGetNodeType", storeId, "Hello");
                Assert.That(result, Is.EqualTo(JsonStoreScriptModule.JSON_NODETYPE_OBJECT));
            }

            {
                int result = (int)InvokeOp("JsonGetNodeType", storeId, "Hello.World");
                Assert.That(result, Is.EqualTo(JsonStoreScriptModule.JSON_NODETYPE_ARRAY));
            }

            {
                int result = (int)InvokeOp("JsonGetNodeType", storeId, "Hello.World[0]");
                Assert.That(result, Is.EqualTo(JsonStoreScriptModule.JSON_NODETYPE_VALUE));
            }

            {
                int result = (int)InvokeOp("JsonGetNodeType", storeId, "Hello.World[1]");
                Assert.That(result, Is.EqualTo(JsonStoreScriptModule.JSON_NODETYPE_VALUE));
            }

            // Test for non-existent path
            {
                int result = (int)InvokeOp("JsonGetNodeType", storeId, "foo");
                Assert.That(result, Is.EqualTo(JsonStoreScriptModule.JSON_NODETYPE_UNDEF));
            }

            // Test for non-existent store
            {
                UUID fakeStoreId = TestHelpers.ParseTail(0x500);
                int result = (int)InvokeOp("JsonGetNodeType", fakeStoreId, ".");
                Assert.That(result, Is.EqualTo(JsonStoreScriptModule.JSON_NODETYPE_UNDEF));
            }
        }

        [Test]
        public void TestJsonList2Path()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            // Invoking these methods directly since I just couldn't get comms module invocation to work for some reason
            // - some confusion with the methods that take a params object[] invocation.
            {
                string result = m_jssm.JsonList2Path(UUID.Zero, UUID.Zero, new object[] { "foo" });
                Assert.That(result, Is.EqualTo("{foo}"));
            }

            {
                string result = m_jssm.JsonList2Path(UUID.Zero, UUID.Zero, new object[] { "foo", "bar" });
                Assert.That(result, Is.EqualTo("{foo}.{bar}"));
            }

            {
                string result = m_jssm.JsonList2Path(UUID.Zero, UUID.Zero, new object[] { "foo", 1, "bar" });
                Assert.That(result, Is.EqualTo("{foo}.[1].{bar}"));
            }
        }

        [Test]
        public void TestJsonSetValue()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{}");

                int result = (int)InvokeOp("JsonSetValue", storeId, "Fun", "Times");
                Assert.That(result, Is.EqualTo(1));

                string value = (string)InvokeOp("JsonGetValue", storeId, "Fun");
                Assert.That(value, Is.EqualTo("Times"));
            }

            // Test setting a key containing periods with delineation
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{}");

                int result = (int)InvokeOp("JsonSetValue", storeId, "{Fun.Circus}", "Times");
                Assert.That(result, Is.EqualTo(1));

                string value = (string)InvokeOp("JsonGetValue", storeId, "{Fun.Circus}");
                Assert.That(value, Is.EqualTo("Times"));
            }

            // *** Test [] ***

            // Test setting a key containing unbalanced ] without delineation.  Expecting failure
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{}");

                int result = (int)InvokeOp("JsonSetValue", storeId, "Fun]Circus", "Times");
                Assert.That(result, Is.EqualTo(0));

                string value = (string)InvokeOp("JsonGetValue", storeId, "Fun]Circus");
                Assert.That(value, Is.EqualTo(""));
            }

            // Test setting a key containing unbalanced [ without delineation.  Expecting failure
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{}");

                int result = (int)InvokeOp("JsonSetValue", storeId, "Fun[Circus", "Times");
                Assert.That(result, Is.EqualTo(0));

                string value = (string)InvokeOp("JsonGetValue", storeId, "Fun[Circus");
                Assert.That(value, Is.EqualTo(""));
            }

            // Test setting a key containing unbalanced [] without delineation.  Expecting failure
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{}");

                int result = (int)InvokeOp("JsonSetValue", storeId, "Fun[]Circus", "Times");
                Assert.That(result, Is.EqualTo(0));

                string value = (string)InvokeOp("JsonGetValue", storeId, "Fun[]Circus");
                Assert.That(value, Is.EqualTo(""));
            }

            // Test setting a key containing unbalanced ] with delineation
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{}");

                int result = (int)InvokeOp("JsonSetValue", storeId, "{Fun]Circus}", "Times");
                Assert.That(result, Is.EqualTo(1));

                string value = (string)InvokeOp("JsonGetValue", storeId, "{Fun]Circus}");
                Assert.That(value, Is.EqualTo("Times"));
            }

            // Test setting a key containing unbalanced [ with delineation
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{}");

                int result = (int)InvokeOp("JsonSetValue", storeId, "{Fun[Circus}", "Times");
                Assert.That(result, Is.EqualTo(1));

                string value = (string)InvokeOp("JsonGetValue", storeId, "{Fun[Circus}");
                Assert.That(value, Is.EqualTo("Times"));
            }

            // Test setting a key containing empty balanced [] with delineation
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{}");

                int result = (int)InvokeOp("JsonSetValue", storeId, "{Fun[]Circus}", "Times");
                Assert.That(result, Is.EqualTo(1));

                string value = (string)InvokeOp("JsonGetValue", storeId, "{Fun[]Circus}");
                Assert.That(value, Is.EqualTo("Times"));
            }

//            // Commented out as this currently unexpectedly fails.
//            // Test setting a key containing brackets around an integer with delineation
//            {
//                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{}");
//
//                int result = (int)InvokeOp("JsonSetValue", storeId, "{Fun[0]Circus}", "Times");
//                Assert.That(result, Is.EqualTo(1));
//
//                string value = (string)InvokeOp("JsonGetValue", storeId, "{Fun[0]Circus}");
//                Assert.That(value, Is.EqualTo("Times"));
//            }

            // *** Test {} ***

            // Test setting a key containing unbalanced } without delineation.  Expecting failure (?)
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{}");

                int result = (int)InvokeOp("JsonSetValue", storeId, "Fun}Circus", "Times");
                Assert.That(result, Is.EqualTo(0));

                string value = (string)InvokeOp("JsonGetValue", storeId, "Fun}Circus");
                Assert.That(value, Is.EqualTo(""));
            }

            // Test setting a key containing unbalanced { without delineation.  Expecting failure (?)
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{}");

                int result = (int)InvokeOp("JsonSetValue", storeId, "Fun{Circus", "Times");
                Assert.That(result, Is.EqualTo(0));

                string value = (string)InvokeOp("JsonGetValue", storeId, "Fun}Circus");
                Assert.That(value, Is.EqualTo(""));
            }

//            // Commented out as this currently unexpectedly fails.
//            // Test setting a key containing unbalanced }
//            {
//                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{}");
//
//                int result = (int)InvokeOp("JsonSetValue", storeId, "{Fun}Circus}", "Times");
//                Assert.That(result, Is.EqualTo(0));
//            }

            // Test setting a key containing unbalanced { with delineation
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{}");

                int result = (int)InvokeOp("JsonSetValue", storeId, "{Fun{Circus}", "Times");
                Assert.That(result, Is.EqualTo(1));

                string value = (string)InvokeOp("JsonGetValue", storeId, "{Fun{Circus}");
                Assert.That(value, Is.EqualTo("Times"));
            }

            // Test setting a key containing balanced {} with delineation.  This should fail.
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{}");

                int result = (int)InvokeOp("JsonSetValue", storeId, "{Fun{Filled}Circus}", "Times");
                Assert.That(result, Is.EqualTo(0));

                string value = (string)InvokeOp("JsonGetValue", storeId, "{Fun{Filled}Circus}");
                Assert.That(value, Is.EqualTo(""));
            }

            // Test setting to location that does not exist.  This should fail.
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{}");

                int result = (int)InvokeOp("JsonSetValue", storeId, "Fun.Circus", "Times");
                Assert.That(result, Is.EqualTo(0));

                string value = (string)InvokeOp("JsonGetValue", storeId, "Fun.Circus");
                Assert.That(value, Is.EqualTo(""));
            }

            // Test with fake store
            {
                UUID fakeStoreId = TestHelpers.ParseTail(0x500);
                int fakeStoreValueSet = (int)InvokeOp("JsonSetValue", fakeStoreId, "Hello", "World");
                Assert.That(fakeStoreValueSet, Is.EqualTo(0));
            }
        }

        [Test]
        public void TestJsonSetJson()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            // Single quoted token case
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ }");

                int result = (int)InvokeOp("JsonSetJson", storeId, "Fun", "'Times'");
                Assert.That(result, Is.EqualTo(1));

                string value = (string)InvokeOp("JsonGetValue", storeId, "Fun");
                Assert.That(value, Is.EqualTo("Times"));
            }

            // Sub-tree case
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ }");

                int result = (int)InvokeOp("JsonSetJson", storeId, "Fun", "{ 'Filled' : 'Times' }");
                Assert.That(result, Is.EqualTo(1));

                string value = (string)InvokeOp("JsonGetValue", storeId, "Fun.Filled");
                Assert.That(value, Is.EqualTo("Times"));
            }

            // If setting single strings in JsonSetValueJson, these must be single quoted tokens, not bare strings.
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ }");

                int result = (int)InvokeOp("JsonSetJson", storeId, "Fun", "Times");
                Assert.That(result, Is.EqualTo(0));

                string value = (string)InvokeOp("JsonGetValue", storeId, "Fun");
                Assert.That(value, Is.EqualTo(""));
            }

            // Test setting to location that does not exist.  This should fail.
            {
                UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ }");

                int result = (int)InvokeOp("JsonSetJson", storeId, "Fun.Circus", "'Times'");
                Assert.That(result, Is.EqualTo(0));

                string value = (string)InvokeOp("JsonGetValue", storeId, "Fun.Circus");
                Assert.That(value, Is.EqualTo(""));
            }

            // Test with fake store
            {
                UUID fakeStoreId = TestHelpers.ParseTail(0x500);
                int fakeStoreValueSet = (int)InvokeOp("JsonSetJson", fakeStoreId, "Hello", "'World'");
                Assert.That(fakeStoreValueSet, Is.EqualTo(0));
            }
        }

        /// <summary>
        /// Test for writing json to a notecard
        /// </summary>
        /// <remarks>
        /// TODO: Really needs to test correct receipt of the link_message event.  Could do this by directly fetching
        /// it via the MockScriptEngine or perhaps by a dummy script instance.
        /// </remarks>
        [Test]
        public void TestJsonWriteNotecard()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            SceneObjectGroup so = SceneHelpers.CreateSceneObject(1, TestHelpers.ParseTail(0x1));
            m_scene.AddSceneObject(so);

            UUID storeId = (UUID)InvokeOp("JsonCreateStore", "{ 'Hello':'World' }");

            {
                string notecardName = "nc1";

                // Write notecard
                UUID writeNotecardRequestId = (UUID)InvokeOpOnHost("JsonWriteNotecard", so.UUID, storeId, "", notecardName);
                Assert.That(writeNotecardRequestId, Is.Not.EqualTo(UUID.Zero));

                TaskInventoryItem nc1Item = so.RootPart.Inventory.GetInventoryItem(notecardName);
                Assert.That(nc1Item, Is.Not.Null);

                // TODO: Should independently check the contents.
            }

            // TODO: Write partial test

            {
                // Try to write notecard for a bad path
                // In this case we do get a request id but no notecard is written.
                string badPathNotecardName = "badPathNotecardName";

                UUID writeNotecardBadPathRequestId
                    = (UUID)InvokeOpOnHost("JsonWriteNotecard", so.UUID, storeId, "flibble", badPathNotecardName);
                Assert.That(writeNotecardBadPathRequestId, Is.Not.EqualTo(UUID.Zero));

                TaskInventoryItem badPathItem = so.RootPart.Inventory.GetInventoryItem(badPathNotecardName);
                Assert.That(badPathItem, Is.Null);
            }

            {
                // Test with fake store
                // In this case we do get a request id but no notecard is written.
                string fakeStoreNotecardName = "fakeStoreNotecardName";

                UUID fakeStoreId = TestHelpers.ParseTail(0x500);
                UUID fakeStoreWriteNotecardValue
                    = (UUID)InvokeOpOnHost("JsonWriteNotecard", so.UUID, fakeStoreId, "", fakeStoreNotecardName);
                Assert.That(fakeStoreWriteNotecardValue, Is.Not.EqualTo(UUID.Zero));

                TaskInventoryItem fakeStoreItem = so.RootPart.Inventory.GetInventoryItem(fakeStoreNotecardName);
                Assert.That(fakeStoreItem, Is.Null);
            }
        }

        /// <summary>
        /// Test for reading json from a notecard
        /// </summary>
        /// <remarks>
        /// TODO: Really needs to test correct receipt of the link_message event.  Could do this by directly fetching
        /// it via the MockScriptEngine or perhaps by a dummy script instance.
        /// </remarks>
        [Test]
        public void TestJsonReadNotecard()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string notecardName = "nc1";

            SceneObjectGroup so = SceneHelpers.CreateSceneObject(1, TestHelpers.ParseTail(0x1));
            m_scene.AddSceneObject(so);

            UUID creatingStoreId = (UUID)InvokeOp("JsonCreateStore", "{ 'Hello':'World' }");

            // Write notecard
            InvokeOpOnHost("JsonWriteNotecard", so.UUID, creatingStoreId, "", notecardName);

            {
                // Read notecard
                UUID receivingStoreId = (UUID)InvokeOp("JsonCreateStore", "{}");
                UUID readNotecardRequestId = (UUID)InvokeOpOnHost("JsonReadNotecard", so.UUID, receivingStoreId, "", notecardName);
                Assert.That(readNotecardRequestId, Is.Not.EqualTo(UUID.Zero));

                string value = (string)InvokeOp("JsonGetValue", receivingStoreId, "Hello");
                Assert.That(value, Is.EqualTo("World"));
            }

            {
                // Read notecard to new single component path
                UUID receivingStoreId = (UUID)InvokeOp("JsonCreateStore", "{}");
                UUID readNotecardRequestId = (UUID)InvokeOpOnHost("JsonReadNotecard", so.UUID, receivingStoreId, "make", notecardName);
                Assert.That(readNotecardRequestId, Is.Not.EqualTo(UUID.Zero));

                string value = (string)InvokeOp("JsonGetValue", receivingStoreId, "Hello");
                Assert.That(value, Is.EqualTo(""));

                value = (string)InvokeOp("JsonGetValue", receivingStoreId, "make.Hello");
                Assert.That(value, Is.EqualTo("World"));
            }

            {
                // Read notecard to new multi-component path.  This should not work.
                UUID receivingStoreId = (UUID)InvokeOp("JsonCreateStore", "{}");
                UUID readNotecardRequestId = (UUID)InvokeOpOnHost("JsonReadNotecard", so.UUID, receivingStoreId, "make.it", notecardName);
                Assert.That(readNotecardRequestId, Is.Not.EqualTo(UUID.Zero));

                string value = (string)InvokeOp("JsonGetValue", receivingStoreId, "Hello");
                Assert.That(value, Is.EqualTo(""));

                value = (string)InvokeOp("JsonGetValue", receivingStoreId, "make.it.Hello");
                Assert.That(value, Is.EqualTo(""));
            }

            {
                // Read notecard to existing multi-component path.  This should work
                UUID receivingStoreId = (UUID)InvokeOp("JsonCreateStore", "{ 'make' : { 'it' : 'so' } }");
                UUID readNotecardRequestId = (UUID)InvokeOpOnHost("JsonReadNotecard", so.UUID, receivingStoreId, "make.it", notecardName);
                Assert.That(readNotecardRequestId, Is.Not.EqualTo(UUID.Zero));

                string value = (string)InvokeOp("JsonGetValue", receivingStoreId, "Hello");
                Assert.That(value, Is.EqualTo(""));

                value = (string)InvokeOp("JsonGetValue", receivingStoreId, "make.it.Hello");
                Assert.That(value, Is.EqualTo("World"));
            }

            {
                // Read notecard to invalid path.  This should not work.
                UUID receivingStoreId = (UUID)InvokeOp("JsonCreateStore", "{ 'make' : { 'it' : 'so' } }");
                UUID readNotecardRequestId = (UUID)InvokeOpOnHost("JsonReadNotecard", so.UUID, receivingStoreId, "/", notecardName);
                Assert.That(readNotecardRequestId, Is.Not.EqualTo(UUID.Zero));

                string value = (string)InvokeOp("JsonGetValue", receivingStoreId, "Hello");
                Assert.That(value, Is.EqualTo(""));
            }

            {
                // Try read notecard to fake store.
                UUID fakeStoreId = TestHelpers.ParseTail(0x500);
                UUID readNotecardRequestId = (UUID)InvokeOpOnHost("JsonReadNotecard", so.UUID, fakeStoreId, "", notecardName);
                Assert.That(readNotecardRequestId, Is.Not.EqualTo(UUID.Zero));

                string value = (string)InvokeOp("JsonGetValue", fakeStoreId, "Hello");
                Assert.That(value, Is.EqualTo(""));
            }
        }

        public object DummyTestMethod(object o1, object o2, object o3, object o4, object o5) { return null; }
    }
}
