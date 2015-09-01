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
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation;
using OpenSim.Tests.Common;
using System.Threading;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    [TestFixture]
    public class ScenePresenceSitTests : OpenSimTestCase
    {
        private TestScene m_scene;
        private ScenePresence m_sp;

        [SetUp]
        public void Init()
        {
            m_scene = new SceneHelpers().SetupScene();
            m_sp = SceneHelpers.AddScenePresence(m_scene, TestHelpers.ParseTail(0x1));
        }

        [Test]
        public void TestSitOutsideRangeNoTarget()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            // More than 10 meters away from 0, 0, 0 (default part position)
            Vector3 startPos = new Vector3(10.1f, 0, 0);
            m_sp.AbsolutePosition = startPos;

            SceneObjectPart part = SceneHelpers.AddSceneObject(m_scene).RootPart;

            m_sp.HandleAgentRequestSit(m_sp.ControllingClient, m_sp.UUID, part.UUID, Vector3.Zero);

            Assert.That(part.SitTargetAvatar, Is.EqualTo(UUID.Zero));
            Assert.That(part.GetSittingAvatarsCount(), Is.EqualTo(0));
            Assert.That(part.GetSittingAvatars(), Is.Null);
            Assert.That(m_sp.ParentID, Is.EqualTo(0));
            Assert.AreEqual(startPos, m_sp.AbsolutePosition);
        }

        [Test]
        public void TestSitWithinRangeNoTarget()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            // Less than 10 meters away from 0, 0, 0 (default part position)
            Vector3 startPos = new Vector3(9.9f, 0, 0);
            m_sp.AbsolutePosition = startPos;

            SceneObjectPart part = SceneHelpers.AddSceneObject(m_scene).RootPart;

            // We need to preserve this here because phys actor is removed by the sit.
            Vector3 spPhysActorSize = m_sp.PhysicsActor.Size;
            m_sp.HandleAgentRequestSit(m_sp.ControllingClient, m_sp.UUID, part.UUID, Vector3.Zero);

            Assert.That(m_sp.PhysicsActor, Is.Null);

            Assert.That(
                m_sp.AbsolutePosition,
                Is.EqualTo(part.AbsolutePosition + new Vector3(0, 0, spPhysActorSize.Z / 2)));

            Assert.That(part.SitTargetAvatar, Is.EqualTo(UUID.Zero));
            Assert.That(part.GetSittingAvatarsCount(), Is.EqualTo(1));
            HashSet<ScenePresence> sittingAvatars = part.GetSittingAvatars();
            Assert.That(sittingAvatars.Count, Is.EqualTo(1));
            Assert.That(sittingAvatars.Contains(m_sp));
            Assert.That(m_sp.ParentID, Is.EqualTo(part.LocalId));
        }

        [Test]
        public void TestSitAndStandWithNoSitTarget()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            // Make sure we're within range to sit
            Vector3 startPos = new Vector3(1, 1, 1);
            m_sp.AbsolutePosition = startPos;

            SceneObjectPart part = SceneHelpers.AddSceneObject(m_scene).RootPart;

            // We need to preserve this here because phys actor is removed by the sit.
            Vector3 spPhysActorSize = m_sp.PhysicsActor.Size;
            m_sp.HandleAgentRequestSit(m_sp.ControllingClient, m_sp.UUID, part.UUID, Vector3.Zero);

            Assert.That(
                m_sp.AbsolutePosition,
                Is.EqualTo(part.AbsolutePosition + new Vector3(0, 0, spPhysActorSize.Z / 2)));

            m_sp.StandUp();

            Assert.That(part.SitTargetAvatar, Is.EqualTo(UUID.Zero));
            Assert.That(part.GetSittingAvatarsCount(), Is.EqualTo(0));
            Assert.That(part.GetSittingAvatars(), Is.Null);
            Assert.That(m_sp.ParentID, Is.EqualTo(0));
            Assert.That(m_sp.PhysicsActor, Is.Not.Null);
        }

        [Test]
        public void TestSitAndStandWithNoSitTargetChildPrim()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            // Make sure we're within range to sit
            Vector3 startPos = new Vector3(1, 1, 1);
            m_sp.AbsolutePosition = startPos;

            SceneObjectPart part = SceneHelpers.AddSceneObject(m_scene, 2, m_sp.UUID, "part", 0x10).Parts[1];
            part.OffsetPosition = new Vector3(2, 3, 4);

            // We need to preserve this here because phys actor is removed by the sit.
            Vector3 spPhysActorSize = m_sp.PhysicsActor.Size;
            m_sp.HandleAgentRequestSit(m_sp.ControllingClient, m_sp.UUID, part.UUID, Vector3.Zero);

            Assert.That(
                m_sp.AbsolutePosition,
                Is.EqualTo(part.AbsolutePosition + new Vector3(0, 0, spPhysActorSize.Z / 2)));

            m_sp.StandUp();

            Assert.That(part.SitTargetAvatar, Is.EqualTo(UUID.Zero));
            Assert.That(part.GetSittingAvatarsCount(), Is.EqualTo(0));
            Assert.That(part.GetSittingAvatars(), Is.Null);
            Assert.That(m_sp.ParentID, Is.EqualTo(0));
            Assert.That(m_sp.PhysicsActor, Is.Not.Null);
        }

        [Test]
        public void TestSitAndStandWithSitTarget()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            // If a prim has a sit target then we can sit from any distance away
            Vector3 startPos = new Vector3(128, 128, 30);
            m_sp.AbsolutePosition = startPos;

            SceneObjectPart part = SceneHelpers.AddSceneObject(m_scene).RootPart;
            part.SitTargetPosition = new Vector3(0, 0, 1);

            m_sp.HandleAgentRequestSit(m_sp.ControllingClient, m_sp.UUID, part.UUID, Vector3.Zero);

            Assert.That(part.SitTargetAvatar, Is.EqualTo(m_sp.UUID));
            Assert.That(m_sp.ParentID, Is.EqualTo(part.LocalId));

            // This section is copied from ScenePresence.HandleAgentSit().  Correctness is not guaranteed.
            double x, y, z, m1, m2;

            Quaternion r = part.SitTargetOrientation;;
            m1 = r.X * r.X + r.Y * r.Y;
            m2 = r.Z * r.Z + r.W * r.W;

            // Rotate the vector <0, 0, 1>
            x = 2 * (r.X * r.Z + r.Y * r.W);
            y = 2 * (-r.X * r.W + r.Y * r.Z);
            z = m2 - m1;

            // Set m to be the square of the norm of r.
            double m = m1 + m2;

            // This constant is emperically determined to be what is used in SL.
            // See also http://opensimulator.org/mantis/view.php?id=7096
            double offset = 0.05;

            Vector3 up = new Vector3((float)x, (float)y, (float)z);
            Vector3 sitOffset = up * (float)offset;
            // End of copied section.

            Assert.That(
                m_sp.AbsolutePosition,
                Is.EqualTo(part.AbsolutePosition + part.SitTargetPosition - sitOffset + ScenePresence.SIT_TARGET_ADJUSTMENT));
            Assert.That(m_sp.PhysicsActor, Is.Null);

            Assert.That(part.GetSittingAvatarsCount(), Is.EqualTo(1));
            HashSet<ScenePresence> sittingAvatars = part.GetSittingAvatars();
            Assert.That(sittingAvatars.Count, Is.EqualTo(1));
            Assert.That(sittingAvatars.Contains(m_sp));

            m_sp.StandUp();

            Assert.That(part.SitTargetAvatar, Is.EqualTo(UUID.Zero));
            Assert.That(m_sp.ParentID, Is.EqualTo(0));
            Assert.That(m_sp.PhysicsActor, Is.Not.Null);

            Assert.That(part.SitTargetAvatar, Is.EqualTo(UUID.Zero));
            Assert.That(part.GetSittingAvatarsCount(), Is.EqualTo(0));
            Assert.That(part.GetSittingAvatars(), Is.Null);
        }

        [Test]
        public void TestSitAndStandOnGround()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            // If a prim has a sit target then we can sit from any distance away
//            Vector3 startPos = new Vector3(128, 128, 30);
//            sp.AbsolutePosition = startPos;

            m_sp.HandleAgentSitOnGround();

            Assert.That(m_sp.SitGround, Is.True);
            Assert.That(m_sp.PhysicsActor, Is.Null);

            m_sp.StandUp();

            Assert.That(m_sp.SitGround, Is.False);
            Assert.That(m_sp.PhysicsActor, Is.Not.Null);
        }
    }
}