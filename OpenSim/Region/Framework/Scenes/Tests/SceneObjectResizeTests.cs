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
using System.Reflection;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    /// <summary>
    /// Basic scene object resize tests
    /// </summary>
    [TestFixture]
    public class SceneObjectResizeTests : OpenSimTestCase
    {
        /// <summary>
        /// Test resizing an object
        /// </summary>
        [Test]
        public void TestResizeSceneObject()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            Scene scene = new SceneHelpers().SetupScene();
            SceneObjectGroup g1 = SceneHelpers.AddSceneObject(scene);

            g1.GroupResize(new Vector3(2, 3, 4));

            SceneObjectGroup g1Post = scene.GetSceneObjectGroup(g1.UUID);

            Assert.That(g1Post.RootPart.Scale.X, Is.EqualTo(2));
            Assert.That(g1Post.RootPart.Scale.Y, Is.EqualTo(3));
            Assert.That(g1Post.RootPart.Scale.Z, Is.EqualTo(4));

//            Assert.That(g1Post.RootPart.UndoCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Test resizing an individual part in a scene object.
        /// </summary>
        [Test]
        public void TestResizeSceneObjectPart()
        {
            TestHelpers.InMethod();
            //log4net.Config.XmlConfigurator.Configure();

            Scene scene = new SceneHelpers().SetupScene();
            UUID owner = UUID.Random();
            SceneObjectGroup g1 = SceneHelpers.CreateSceneObject(2, owner);
            g1.RootPart.Scale = new Vector3(2, 3, 4);
            g1.Parts[1].Scale = new Vector3(5, 6, 7);

            scene.AddSceneObject(g1);

            SceneObjectGroup g1Post = scene.GetSceneObjectGroup(g1.UUID);

            g1Post.Parts[1].Resize(new Vector3(8, 9, 10));

            SceneObjectGroup g1PostPost = scene.GetSceneObjectGroup(g1.UUID);

            SceneObjectPart g1RootPart = g1PostPost.RootPart;
            SceneObjectPart g1ChildPart = g1PostPost.Parts[1];

            Assert.That(g1RootPart.Scale.X, Is.EqualTo(2));
            Assert.That(g1RootPart.Scale.Y, Is.EqualTo(3));
            Assert.That(g1RootPart.Scale.Z, Is.EqualTo(4));

            Assert.That(g1ChildPart.Scale.X, Is.EqualTo(8));
            Assert.That(g1ChildPart.Scale.Y, Is.EqualTo(9));
            Assert.That(g1ChildPart.Scale.Z, Is.EqualTo(10));
        }
    }
}