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
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.OptionalModules.Scripting.JsonStore.Tests
{
    /// <summary>
    /// Tests for inventory functions in LSL
    /// </summary>
    [TestFixture]
    public class LSL_ApiInventoryTests : OpenSimTestCase
    {
        private Scene m_scene;
        private MockScriptEngine m_engine;
        private ScriptModuleCommsModule m_smcm;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            TestHelpers.EnableLogging();

            IConfigSource configSource = new IniConfigSource();
            IConfig jsonStoreConfig = configSource.AddConfig("JsonStore");
            jsonStoreConfig.Set("Enabled", "true");

            m_engine = new MockScriptEngine();
            m_smcm = new ScriptModuleCommsModule();
            JsonStoreModule jsm = new JsonStoreModule();
            JsonStoreScriptModule jssm = new JsonStoreScriptModule();

            m_scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(m_scene, configSource, m_engine, m_smcm, jsm, jssm);
        }

//        [Test]
        public void TestJsonCreateStore()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID storeId = (UUID)m_smcm.InvokeOperation(UUID.Zero, UUID.Zero, "JsonCreateStore", new object[] { "{}" }); 

            Assert.That(storeId, Is.Not.EqualTo(UUID.Zero));
        }

//        [Test]
        public void TestJsonGetValue()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID storeId 
                = (UUID)m_smcm.InvokeOperation(
                    UUID.Zero, UUID.Zero, "JsonCreateStore", new object[] { "{ 'Hello' : 'World' }" }); 

            string value 
                = (string)m_smcm.InvokeOperation(
                    UUID.Zero, UUID.Zero, "JsonGetValue", new object[] { storeId, "Hello" });

            Assert.That(value, Is.EqualTo("World"));
        }

//        [Test]
        public void TestJsonTestPath()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID storeId 
                = (UUID)m_smcm.InvokeOperation(
                    UUID.Zero, UUID.Zero, "JsonCreateStore", new object[] { "{ 'Hello' : 'World' }" }); 

            int result 
                = (int)m_smcm.InvokeOperation(
                    UUID.Zero, UUID.Zero, "JsonTestPath", new object[] { storeId, "Hello" });

            Assert.That(result, Is.EqualTo(1));
        }

//        [Test]
        public void TestJsonSetValue()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID storeId 
                = (UUID)m_smcm.InvokeOperation(
                    UUID.Zero, UUID.Zero, "JsonCreateStore", new object[] { "{ }" }); 

            int result 
                = (int)m_smcm.InvokeOperation(
                    UUID.Zero, UUID.Zero, "JsonSetValue", new object[] { storeId, "Hello", "World" });

            Assert.That(result, Is.EqualTo(1));

            string value 
                = (string)m_smcm.InvokeOperation(
                    UUID.Zero, UUID.Zero, "JsonGetValue", new object[] { storeId, "Hello" });

            Assert.That(value, Is.EqualTo("World"));
        }
    }
}