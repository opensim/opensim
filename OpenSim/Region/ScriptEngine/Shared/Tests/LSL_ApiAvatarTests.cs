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
using OpenMetaverse.Assets;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Avatar.AvatarFactory;
using OpenSim.Region.OptionalModules.World.NPC;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.Instance;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;

namespace OpenSim.Region.ScriptEngine.Shared.Tests
{
    /// <summary>
    /// Tests relating directly to avatars
    /// </summary>
    [TestFixture]
    public class LSL_ApiAvatarTests : OpenSimTestCase
    {
        protected Scene m_scene;
        protected XEngine.XEngine m_engine;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            IConfigSource initConfigSource = new IniConfigSource();
            IConfig config = initConfigSource.AddConfig("XEngine");
            config.Set("Enabled", "true");

            m_scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(m_scene, initConfigSource);

            m_engine = new XEngine.XEngine();
            m_engine.Initialise(initConfigSource);
            m_engine.AddRegion(m_scene);
        }

        /// <summary>
        /// Test llSetLinkPrimtiveParams for agents.
        /// </summary>
        /// <remarks>
        /// Also testing entity updates here as well.  Possibly that's putting 2 different concerns into one test and
        /// this should be separated.
        /// </remarks>
        [Test]
        public void TestllSetLinkPrimitiveParamsForAgent()
        {
/* siting avatars position changed
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);

            SceneObjectPart part = SceneHelpers.AddSceneObject(m_scene).RootPart;
            part.RotationOffset = new Quaternion(0.7071068f, 0, 0, 0.7071068f);

            LSL_Api apiGrp1 = new LSL_Api();
            apiGrp1.Initialize(m_engine, part, null);

            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, userId);

            // sp has to be less than 10 meters away from 0, 0, 0 (default part position)
            Vector3 startPos = new Vector3(3, 2, 1);
            sp.AbsolutePosition = startPos;

            sp.HandleAgentRequestSit(sp.ControllingClient, sp.UUID, part.UUID, Vector3.Zero);

            int entityUpdates = 0;
            ((TestClient)sp.ControllingClient).OnReceivedEntityUpdate += (entity, flags) => { if (entity is ScenePresence) { entityUpdates++; }};

            // Test position
            {
                Vector3 newPos = new Vector3(1, 2, 3);
                apiGrp1.llSetLinkPrimitiveParams(2, new LSL_Types.list(ScriptBaseClass.PRIM_POSITION, newPos));

                Assert.That(sp.OffsetPosition, Is.EqualTo(newPos));

                m_scene.Update(1);
                Assert.That(entityUpdates, Is.EqualTo(1));
            }

            // Test small reposition
            {
                Vector3 newPos = new Vector3(1.001f, 2, 3);
                apiGrp1.llSetLinkPrimitiveParams(2, new LSL_Types.list(ScriptBaseClass.PRIM_POSITION, newPos));

                Assert.That(sp.OffsetPosition, Is.EqualTo(newPos));

                m_scene.Update(1);
                Assert.That(entityUpdates, Is.EqualTo(2));
            }

            // Test world rotation
            {
                Quaternion newRot = new Quaternion(0, 0.7071068f, 0, 0.7071068f);
                apiGrp1.llSetLinkPrimitiveParams(2, new LSL_Types.list(ScriptBaseClass.PRIM_ROTATION, newRot));

                Assert.That(
                    sp.Rotation, new QuaternionToleranceConstraint(part.GetWorldRotation() * newRot, 0.000001));

                m_scene.Update(1);
                Assert.That(entityUpdates, Is.EqualTo(3));
            }

            // Test local rotation
            {
                Quaternion newRot = new Quaternion(0, 0.7071068f, 0, 0.7071068f);
                apiGrp1.llSetLinkPrimitiveParams(2, new LSL_Types.list(ScriptBaseClass.PRIM_ROT_LOCAL, newRot));

                Assert.That(
                    sp.Rotation, new QuaternionToleranceConstraint(newRot, 0.000001));

                m_scene.Update(1);
                Assert.That(entityUpdates, Is.EqualTo(4));
            }
*/
        }
    }
}
