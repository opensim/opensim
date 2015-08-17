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

namespace OpenSim.Region.ScriptEngine.Shared.Tests
{
    /// <summary>
    /// Tests for linking functions in LSL
    /// </summary>
    /// <remarks>
    /// This relates to LSL.  Actual linking functionality should be tested in the main
    /// OpenSim.Region.Framework.Scenes.Tests.SceneObjectLinkingTests.
    /// </remarks>
    [TestFixture]
    public class LSL_ApiLinkingTests : OpenSimTestCase
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

        [Test]
        public void TestllCreateLink()
        {
            TestHelpers.InMethod();

            UUID ownerId = TestHelpers.ParseTail(0x1);

            SceneObjectGroup grp1 = SceneHelpers.CreateSceneObject(2, ownerId, "grp1-", 0x10);
            grp1.AbsolutePosition = new Vector3(10, 10, 10);
            m_scene.AddSceneObject(grp1);

            // FIXME: This should really be a script item (with accompanying script)
            TaskInventoryItem grp1Item
                = TaskInventoryHelpers.AddNotecard(
                    m_scene.AssetService, grp1.RootPart, "ncItem", TestHelpers.ParseTail(0x800), TestHelpers.ParseTail(0x900), "Hello World!");
            grp1Item.PermsMask |= ScriptBaseClass.PERMISSION_CHANGE_LINKS;

            SceneObjectGroup grp2 = SceneHelpers.CreateSceneObject(2, ownerId, "grp2-", 0x20);
            grp2.AbsolutePosition = new Vector3(20, 20, 20);

            // <180,0,0>
            grp2.UpdateGroupRotationR(Quaternion.CreateFromEulers(180 * Utils.DEG_TO_RAD, 0, 0));

            m_scene.AddSceneObject(grp2);

            LSL_Api apiGrp1 = new LSL_Api();
            apiGrp1.Initialize(m_engine, grp1.RootPart, grp1Item, null);

            apiGrp1.llCreateLink(grp2.UUID.ToString(), ScriptBaseClass.TRUE);

            Assert.That(grp1.Parts.Length, Is.EqualTo(4));
            Assert.That(grp2.IsDeleted, Is.True);
        }

        [Test]
        public void TestllBreakLink()
        {
            TestHelpers.InMethod();

            UUID ownerId = TestHelpers.ParseTail(0x1);

            SceneObjectGroup grp1 = SceneHelpers.CreateSceneObject(2, ownerId, "grp1-", 0x10);
            grp1.AbsolutePosition = new Vector3(10, 10, 10);
            m_scene.AddSceneObject(grp1);

            // FIXME: This should really be a script item (with accompanying script)
            TaskInventoryItem grp1Item
                = TaskInventoryHelpers.AddNotecard(
                    m_scene.AssetService, grp1.RootPart, "ncItem", TestHelpers.ParseTail(0x800), TestHelpers.ParseTail(0x900), "Hello World!");
            
            grp1Item.PermsMask |= ScriptBaseClass.PERMISSION_CHANGE_LINKS;

            LSL_Api apiGrp1 = new LSL_Api();
            apiGrp1.Initialize(m_engine, grp1.RootPart, grp1Item, null);

            apiGrp1.llBreakLink(2);

            Assert.That(grp1.Parts.Length, Is.EqualTo(1));

            SceneObjectGroup grp2 = m_scene.GetSceneObjectGroup("grp1-Part1");
            Assert.That(grp2, Is.Not.Null);
        }

        [Test]
        public void TestllBreakAllLinks()
        {
            TestHelpers.InMethod();

            UUID ownerId = TestHelpers.ParseTail(0x1);

            SceneObjectGroup grp1 = SceneHelpers.CreateSceneObject(3, ownerId, "grp1-", 0x10);
            grp1.AbsolutePosition = new Vector3(10, 10, 10);
            m_scene.AddSceneObject(grp1);

            // FIXME: This should really be a script item (with accompanying script)
            TaskInventoryItem grp1Item
                = TaskInventoryHelpers.AddNotecard(
                    m_scene.AssetService, grp1.RootPart, "ncItem", TestHelpers.ParseTail(0x800), TestHelpers.ParseTail(0x900), "Hello World!");
            
            grp1Item.PermsMask |= ScriptBaseClass.PERMISSION_CHANGE_LINKS;

            LSL_Api apiGrp1 = new LSL_Api();
            apiGrp1.Initialize(m_engine, grp1.RootPart, grp1Item, null);

            apiGrp1.llBreakAllLinks();

            {
                SceneObjectGroup nowGrp = m_scene.GetSceneObjectGroup("grp1-Part1");
                Assert.That(nowGrp, Is.Not.Null);
                Assert.That(nowGrp.Parts.Length, Is.EqualTo(1));
            }

            {
                SceneObjectGroup nowGrp = m_scene.GetSceneObjectGroup("grp1-Part2");
                Assert.That(nowGrp, Is.Not.Null);
                Assert.That(nowGrp.Parts.Length, Is.EqualTo(1));
            }

            {
                SceneObjectGroup nowGrp = m_scene.GetSceneObjectGroup("grp1-Part3");
                Assert.That(nowGrp, Is.Not.Null);
                Assert.That(nowGrp.Parts.Length, Is.EqualTo(1));
            }
        }
    }
}