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
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    /// <summary>
    /// Basic scene object status tests
    /// </summary>
    [TestFixture]
    public class SceneObjectStatusTests : OpenSimTestCase
    {
        private TestScene m_scene;
        private UUID m_ownerId = TestHelpers.ParseTail(0x1);
        private SceneObjectGroup m_so1;
        private SceneObjectGroup m_so2;

        [SetUp]
        public void Init()
        {
            m_scene = new SceneHelpers().SetupScene();
            m_so1 = SceneHelpers.CreateSceneObject(1, m_ownerId, "so1", 0x10);
            m_so2 = SceneHelpers.CreateSceneObject(1, m_ownerId, "so2", 0x20);
        }

        [Test]
        public void TestSetTemporary()
        {
            TestHelpers.InMethod();

            m_scene.AddSceneObject(m_so1);
            m_so1.ScriptSetTemporaryStatus(true);

            // Is this really the correct flag?
            Assert.That(m_so1.RootPart.Flags, Is.EqualTo(PrimFlags.TemporaryOnRez));
            Assert.That(m_so1.Backup, Is.False);

            // Test setting back to non-temporary
            m_so1.ScriptSetTemporaryStatus(false);

            Assert.That(m_so1.RootPart.Flags, Is.EqualTo(PrimFlags.None));
            Assert.That(m_so1.Backup, Is.True);
        }

        [Test]
        public void TestSetPhantomSinglePrim()
        {
            TestHelpers.InMethod();

            m_scene.AddSceneObject(m_so1);

            SceneObjectPart rootPart = m_so1.RootPart;
            Assert.That(rootPart.Flags, Is.EqualTo(PrimFlags.None));

            m_so1.ScriptSetPhantomStatus(true);

//            Console.WriteLine("so.RootPart.Flags [{0}]", so.RootPart.Flags);
            Assert.That(rootPart.Flags, Is.EqualTo(PrimFlags.Phantom));

            m_so1.ScriptSetPhantomStatus(false);

            Assert.That(rootPart.Flags, Is.EqualTo(PrimFlags.None));
        }

        [Test]
        public void TestSetNonPhysicsVolumeDetectSinglePrim()
        {
            TestHelpers.InMethod();

            m_scene.AddSceneObject(m_so1);

            SceneObjectPart rootPart = m_so1.RootPart;
            Assert.That(rootPart.Flags, Is.EqualTo(PrimFlags.None));

            m_so1.ScriptSetVolumeDetect(true);

//            Console.WriteLine("so.RootPart.Flags [{0}]", so.RootPart.Flags);
            // PrimFlags.JointLP2P is incorrect it now means VolumeDetect (as defined by viewers)
            Assert.That(rootPart.Flags, Is.EqualTo(PrimFlags.Phantom | PrimFlags.JointLP2P));

            m_so1.ScriptSetVolumeDetect(false);

            Assert.That(rootPart.Flags, Is.EqualTo(PrimFlags.None));
        }

        [Test]
        public void TestSetPhysicsSinglePrim()
        {
            TestHelpers.InMethod();

            m_scene.AddSceneObject(m_so1);

            SceneObjectPart rootPart = m_so1.RootPart;
            Assert.That(rootPart.Flags, Is.EqualTo(PrimFlags.None));

            m_so1.ScriptSetPhysicsStatus(true);

            Assert.That(rootPart.Flags, Is.EqualTo(PrimFlags.Physics));

            m_so1.ScriptSetPhysicsStatus(false);

            Assert.That(rootPart.Flags, Is.EqualTo(PrimFlags.None));
        }

        [Test]
        public void TestSetPhysicsVolumeDetectSinglePrim()
        {
            TestHelpers.InMethod();

            m_scene.AddSceneObject(m_so1);

            SceneObjectPart rootPart = m_so1.RootPart;
            Assert.That(rootPart.Flags, Is.EqualTo(PrimFlags.None));

            m_so1.ScriptSetPhysicsStatus(true);
            m_so1.ScriptSetVolumeDetect(true);

            // PrimFlags.JointLP2P is incorrect it now means VolumeDetect (as defined by viewers)
            Assert.That(rootPart.Flags, Is.EqualTo(PrimFlags.Phantom | PrimFlags.Physics | PrimFlags.JointLP2P));

            m_so1.ScriptSetVolumeDetect(false);

            Assert.That(rootPart.Flags, Is.EqualTo(PrimFlags.Physics));
        }

        [Test]
        public void TestSetPhysicsLinkset()
        {
            TestHelpers.InMethod();

            m_scene.AddSceneObject(m_so1);
            m_scene.AddSceneObject(m_so2);

            m_scene.LinkObjects(m_ownerId, m_so1.LocalId, new List<uint>() { m_so2.LocalId });

            m_so1.ScriptSetPhysicsStatus(true);

            Assert.That(m_so1.RootPart.Flags, Is.EqualTo(PrimFlags.Physics));
            Assert.That(m_so1.Parts[1].Flags, Is.EqualTo(PrimFlags.Physics));

            m_so1.ScriptSetPhysicsStatus(false);

            Assert.That(m_so1.RootPart.Flags, Is.EqualTo(PrimFlags.None));
            Assert.That(m_so1.Parts[1].Flags, Is.EqualTo(PrimFlags.None));

            m_so1.ScriptSetPhysicsStatus(true);

            Assert.That(m_so1.RootPart.Flags, Is.EqualTo(PrimFlags.Physics));
            Assert.That(m_so1.Parts[1].Flags, Is.EqualTo(PrimFlags.Physics));
        }

        /// <summary>
        /// Test that linking results in the correct physical status for all linkees.
        /// </summary>
        [Test]
        public void TestLinkPhysicsBothPhysical()
        {
            TestHelpers.InMethod();

            m_scene.AddSceneObject(m_so1);
            m_scene.AddSceneObject(m_so2);

            m_so1.ScriptSetPhysicsStatus(true);
            m_so2.ScriptSetPhysicsStatus(true);

            m_scene.LinkObjects(m_ownerId, m_so1.LocalId, new List<uint>() { m_so2.LocalId });

            Assert.That(m_so1.RootPart.Flags, Is.EqualTo(PrimFlags.Physics));
            Assert.That(m_so1.Parts[1].Flags, Is.EqualTo(PrimFlags.Physics));
        }

        /// <summary>
        /// Test that linking results in the correct physical status for all linkees.
        /// </summary>
        [Test]
        public void TestLinkPhysicsRootPhysicalOnly()
        {
            TestHelpers.InMethod();

            m_scene.AddSceneObject(m_so1);
            m_scene.AddSceneObject(m_so2);

            m_so1.ScriptSetPhysicsStatus(true);

            m_scene.LinkObjects(m_ownerId, m_so1.LocalId, new List<uint>() { m_so2.LocalId });

            Assert.That(m_so1.RootPart.Flags, Is.EqualTo(PrimFlags.Physics));
            Assert.That(m_so1.Parts[1].Flags, Is.EqualTo(PrimFlags.Physics));
        }

        /// <summary>
        /// Test that linking results in the correct physical status for all linkees.
        /// </summary>
        [Test]
        public void TestLinkPhysicsChildPhysicalOnly()
        {
            TestHelpers.InMethod();

            m_scene.AddSceneObject(m_so1);
            m_scene.AddSceneObject(m_so2);

            m_so2.ScriptSetPhysicsStatus(true);

            m_scene.LinkObjects(m_ownerId, m_so1.LocalId, new List<uint>() { m_so2.LocalId });

            Assert.That(m_so1.RootPart.Flags, Is.EqualTo(PrimFlags.None));
            Assert.That(m_so1.Parts[1].Flags, Is.EqualTo(PrimFlags.None));
        }
    }
}