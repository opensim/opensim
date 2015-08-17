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
using NUnit.Framework;
using OpenSim.Framework;
using OpenSim.Tests.Common;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.Framework.Scenes;
using Nini.Config;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.Instance;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenMetaverse;
 
using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;

namespace OpenSim.Region.ScriptEngine.Shared.Tests
{
    [TestFixture]
    public class LSL_ApiListTests : OpenSimTestCase
    {
        private LSL_Api m_lslApi;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            IConfigSource initConfigSource = new IniConfigSource();
            IConfig config = initConfigSource.AddConfig("XEngine");
            config.Set("Enabled", "true");

            Scene scene = new SceneHelpers().SetupScene();
            SceneObjectPart part = SceneHelpers.AddSceneObject(scene).RootPart;

            XEngine.XEngine engine = new XEngine.XEngine();
            engine.Initialise(initConfigSource);
            engine.AddRegion(scene);

            m_lslApi = new LSL_Api();
            m_lslApi.Initialize(engine, part, null);
        }
 
        [Test]
        public void TestllListFindList()
        {
            TestHelpers.InMethod();

            LSL_List src = new LSL_List(new LSL_Integer(1), new LSL_Integer(2), new LSL_Integer(3));

            {
                // Test for a single item that should be found
                int result = m_lslApi.llListFindList(src, new LSL_List(new LSL_Integer(4)));
                Assert.That(result, Is.EqualTo(-1));
            }

            {
                // Test for a single item that should be found
                int result = m_lslApi.llListFindList(src, new LSL_List(new LSL_Integer(2)));
                Assert.That(result, Is.EqualTo(1));
            }

            {
                // Test for a constant that should be found
                int result = m_lslApi.llListFindList(src, new LSL_List(ScriptBaseClass.AGENT));
                Assert.That(result, Is.EqualTo(0));
            }

            {
                // Test for a list that should be found
                int result = m_lslApi.llListFindList(src, new LSL_List(new LSL_Integer(2), new LSL_Integer(3)));
                Assert.That(result, Is.EqualTo(1));
            }

            {
                // Test for a single item not in the list
                int result = m_lslApi.llListFindList(src, new LSL_List(new LSL_Integer(4)));
                Assert.That(result, Is.EqualTo(-1));
            }

            {
                // Test for something that should not be cast
                int result = m_lslApi.llListFindList(src, new LSL_List(new LSL_String("4")));
                Assert.That(result, Is.EqualTo(-1));
            }

            {
                // Test for a list not in the list
                int result
                    = m_lslApi.llListFindList(
                        src, new LSL_List(new LSL_Integer(2), new LSL_Integer(3), new LSL_Integer(4)));
                Assert.That(result, Is.EqualTo(-1));
            }

            {
                LSL_List srcWithConstants
                    = new LSL_List(new LSL_Integer(3), ScriptBaseClass.AGENT, ScriptBaseClass.OS_NPC_LAND_AT_TARGET);

                // Test for constants that appears in the source list that should be found
                int result
                    = m_lslApi.llListFindList(srcWithConstants, new LSL_List(new LSL_Integer(1), new LSL_Integer(2)));

                Assert.That(result, Is.EqualTo(1));
            }
         }
     }
 }
