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
using System.IO;
using System.Reflection;
using System.Threading;
using log4net.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.World.Media.Moap;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.World.Media.Moap.Tests
{
    [TestFixture]
    public class MoapTests : OpenSimTestCase
    {
        protected TestScene m_scene;
        protected MoapModule m_module;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            m_module = new MoapModule();
            m_scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(m_scene, m_module);
        }

        [Test]
        public void TestClearMediaUrl()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            SceneObjectPart part = SceneHelpers.AddSceneObject(m_scene).RootPart;
            MediaEntry me = new MediaEntry();

            m_module.SetMediaEntry(part, 1, me);
            m_module.ClearMediaEntry(part, 1);

            Assert.That(part.Shape.Media[1], Is.EqualTo(null));

            // Although we've cleared one face, other faces may still be present.  So we need to check for an
            // update media url version
            Assert.That(part.MediaUrl, Is.EqualTo("x-mv:0000000001/" + UUID.Zero));

            // By changing media flag to false, the face texture once again becomes identical to the DefaultTexture.
            // Therefore, when libOMV reserializes it, it disappears and we are left with no face texture in this slot.
            // Not at all confusing, eh?
            Assert.That(part.Shape.Textures.FaceTextures[1], Is.Null);
        }

        [Test]
        public void TestSetMediaUrl()
        {
            TestHelpers.InMethod();

            string homeUrl = "opensimulator.org";

            SceneObjectPart part = SceneHelpers.AddSceneObject(m_scene).RootPart;
            MediaEntry me = new MediaEntry() { HomeURL = homeUrl };

            m_module.SetMediaEntry(part, 1, me);

            Assert.That(part.Shape.Media[1].HomeURL, Is.EqualTo(homeUrl));
            Assert.That(part.MediaUrl, Is.EqualTo("x-mv:0000000000/" + UUID.Zero));
            Assert.That(part.Shape.Textures.FaceTextures[1].MediaFlags, Is.True);
        }
    }
}