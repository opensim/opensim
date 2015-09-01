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
using log4net;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    [TestFixture]
    public class ScenePresenceAutopilotTests : OpenSimTestCase
    {
        private TestScene m_scene;

        [TestFixtureSetUp]
        public void FixtureInit()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
            Util.FireAndForgetMethod = FireAndForgetMethod.None;
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            // We must set this back afterwards, otherwise later tests will fail since they're expecting multiple
            // threads.  Possibly, later tests should be rewritten not to worry about such things.
            Util.FireAndForgetMethod = Util.DefaultFireAndForgetMethod;
        }

        [SetUp]
        public void Init()
        {
            m_scene = new SceneHelpers().SetupScene();
        }

        [Test]
        public void TestMove()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, TestHelpers.ParseTail(0x1));

            Vector3 startPos = sp.AbsolutePosition;
//            Vector3 startPos = new Vector3(128, 128, 30);

            // For now, we'll make the scene presence fly to simplify this test, but this needs to change.
            sp.Flying = true;

            m_scene.Update(1);
            Assert.That(sp.AbsolutePosition, Is.EqualTo(startPos));

            Vector3 targetPos = startPos + new Vector3(0, 10, 0);
            sp.MoveToTarget(targetPos, false, false);

            Assert.That(sp.AbsolutePosition, Is.EqualTo(startPos));
            Assert.That(
                sp.Rotation, new QuaternionToleranceConstraint(new Quaternion(0, 0, 0.7071068f, 0.7071068f), 0.000001));

            m_scene.Update(1);

            // We should really check the exact figure.
            Assert.That(sp.AbsolutePosition.X, Is.EqualTo(startPos.X));
            Assert.That(sp.AbsolutePosition.Y, Is.GreaterThan(startPos.Y));
            Assert.That(sp.AbsolutePosition.Z, Is.EqualTo(startPos.Z));
            Assert.That(sp.AbsolutePosition.Z, Is.LessThan(targetPos.X));

            m_scene.Update(10);

            double distanceToTarget = Util.GetDistanceTo(sp.AbsolutePosition, targetPos);
            Assert.That(distanceToTarget, Is.LessThan(1), "Avatar not within 1 unit of target position on first move");
            Assert.That(sp.AbsolutePosition, Is.EqualTo(targetPos));
            Assert.That(sp.AgentControlFlags, Is.EqualTo((uint)AgentManager.ControlFlags.NONE));

            // Try a second movement
            startPos = sp.AbsolutePosition;
            targetPos = startPos + new Vector3(10, 0, 0);
            sp.MoveToTarget(targetPos, false, false);

            Assert.That(sp.AbsolutePosition, Is.EqualTo(startPos));
            Assert.That(
                sp.Rotation, new QuaternionToleranceConstraint(new Quaternion(0, 0, 0, 1), 0.000001));

            m_scene.Update(1);

            // We should really check the exact figure.
            Assert.That(sp.AbsolutePosition.X, Is.GreaterThan(startPos.X));
            Assert.That(sp.AbsolutePosition.X, Is.LessThan(targetPos.X));
            Assert.That(sp.AbsolutePosition.Y, Is.EqualTo(startPos.Y));
            Assert.That(sp.AbsolutePosition.Z, Is.EqualTo(startPos.Z));

            m_scene.Update(10);

            distanceToTarget = Util.GetDistanceTo(sp.AbsolutePosition, targetPos);
            Assert.That(distanceToTarget, Is.LessThan(1), "Avatar not within 1 unit of target position on second move");
            Assert.That(sp.AbsolutePosition, Is.EqualTo(targetPos));
        }
    }
}