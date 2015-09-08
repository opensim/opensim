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
    [TestFixture]
    public class SceneStatisticsTests : OpenSimTestCase
    {
        private TestScene m_scene;

        [SetUp]
        public void Init()
        {
            m_scene = new SceneHelpers().SetupScene();
        }

        [Test]
        public void TestAddRemovePhysicalLinkset()
        {
            Assert.That(m_scene.SceneGraph.GetActiveObjectsCount(), Is.EqualTo(0));

            UUID ownerId = TestHelpers.ParseTail(0x1);
            SceneObjectGroup so1 = SceneHelpers.CreateSceneObject(3, ownerId, "so1", 0x10);
            so1.ScriptSetPhysicsStatus(true);
            m_scene.AddSceneObject(so1);

            Assert.That(m_scene.SceneGraph.GetTotalObjectsCount(), Is.EqualTo(3));
            Assert.That(m_scene.SceneGraph.GetActiveObjectsCount(), Is.EqualTo(3));

            m_scene.DeleteSceneObject(so1, false);

            Assert.That(m_scene.SceneGraph.GetTotalObjectsCount(), Is.EqualTo(0));
            Assert.That(m_scene.SceneGraph.GetActiveObjectsCount(), Is.EqualTo(0));
        }
    }
}