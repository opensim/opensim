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
using OpenSim.Framework.Communications;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    /// <summary>
    /// Tests for undo/redo
    /// </summary>
    public class SceneObjectUndoRedoTests : OpenSimTestCase
    {
        [Test]
        public void TestUndoRedoResizeSceneObject()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            Vector3 firstSize = new Vector3(2, 3, 4);
            Vector3 secondSize = new Vector3(5, 6, 7);

            Scene scene = new SceneHelpers().SetupScene();
            scene.MaxUndoCount = 20;
            SceneObjectGroup g1 = SceneHelpers.AddSceneObject(scene);

            // TODO: It happens to be the case that we are not storing undo states for SOPs which are not yet in a SOG,
            // which is the way that AddSceneObject() sets up the object (i.e. it creates the SOP first).  However,
            // this is somewhat by chance.  Really, we shouldn't be storing undo states at all if the object is not
            // in a scene.
            Assert.That(g1.RootPart.UndoCount, Is.EqualTo(0));

            g1.GroupResize(firstSize);
            Assert.That(g1.RootPart.UndoCount, Is.EqualTo(1));

            g1.GroupResize(secondSize);
            Assert.That(g1.RootPart.UndoCount, Is.EqualTo(2));

            g1.RootPart.Undo();
            Assert.That(g1.RootPart.UndoCount, Is.EqualTo(1));
            Assert.That(g1.GroupScale, Is.EqualTo(firstSize));

            g1.RootPart.Redo();
            Assert.That(g1.RootPart.UndoCount, Is.EqualTo(2));
            Assert.That(g1.GroupScale, Is.EqualTo(secondSize));
        }

        [Test]
        public void TestUndoLimit()
        {
            TestHelpers.InMethod();

            Vector3 firstSize = new Vector3(2, 3, 4);
            Vector3 secondSize = new Vector3(5, 6, 7);
            Vector3 thirdSize = new Vector3(8, 9, 10);
            Vector3 fourthSize = new Vector3(11, 12, 13);

            Scene scene = new SceneHelpers().SetupScene();
            scene.MaxUndoCount = 2;
            SceneObjectGroup g1 = SceneHelpers.AddSceneObject(scene);

            g1.GroupResize(firstSize);
            g1.GroupResize(secondSize);
            g1.GroupResize(thirdSize);
            g1.GroupResize(fourthSize);

            g1.RootPart.Undo();
            g1.RootPart.Undo();
            g1.RootPart.Undo();

            Assert.That(g1.RootPart.UndoCount, Is.EqualTo(0));
            Assert.That(g1.GroupScale, Is.EqualTo(secondSize));
        }

        [Test]
        public void TestNoUndoOnObjectsNotInScene()
        {
            TestHelpers.InMethod();

            Vector3 firstSize = new Vector3(2, 3, 4);
            Vector3 secondSize = new Vector3(5, 6, 7);
//            Vector3 thirdSize = new Vector3(8, 9, 10);
//            Vector3 fourthSize = new Vector3(11, 12, 13);

            Scene scene = new SceneHelpers().SetupScene();
            scene.MaxUndoCount = 20;
            SceneObjectGroup g1 = SceneHelpers.CreateSceneObject(1, TestHelpers.ParseTail(0x1));

            g1.GroupResize(firstSize);
            g1.GroupResize(secondSize);

            Assert.That(g1.RootPart.UndoCount, Is.EqualTo(0));

            g1.RootPart.Undo();

            Assert.That(g1.GroupScale, Is.EqualTo(secondSize));
        }

        [Test]
        public void TestUndoBeyondAvailable()
        {
            TestHelpers.InMethod();

            Vector3 newSize = new Vector3(2, 3, 4);

            Scene scene = new SceneHelpers().SetupScene();
            scene.MaxUndoCount = 20;
            SceneObjectGroup g1 = SceneHelpers.AddSceneObject(scene);
            Vector3 originalSize = g1.GroupScale;

            g1.RootPart.Undo();

            Assert.That(g1.RootPart.UndoCount, Is.EqualTo(0));
            Assert.That(g1.GroupScale, Is.EqualTo(originalSize));

            g1.GroupResize(newSize);
            Assert.That(g1.RootPart.UndoCount, Is.EqualTo(1));
            Assert.That(g1.GroupScale, Is.EqualTo(newSize));

            g1.RootPart.Undo();
            g1.RootPart.Undo();

            Assert.That(g1.RootPart.UndoCount, Is.EqualTo(0));
            Assert.That(g1.GroupScale, Is.EqualTo(originalSize));
        }

        [Test]
        public void TestRedoBeyondAvailable()
        {
            TestHelpers.InMethod();

            Vector3 newSize = new Vector3(2, 3, 4);

            Scene scene = new SceneHelpers().SetupScene();
            scene.MaxUndoCount = 20;
            SceneObjectGroup g1 = SceneHelpers.AddSceneObject(scene);
            Vector3 originalSize = g1.GroupScale;

            g1.RootPart.Redo();

            Assert.That(g1.RootPart.UndoCount, Is.EqualTo(0));
            Assert.That(g1.GroupScale, Is.EqualTo(originalSize));

            g1.GroupResize(newSize);
            g1.RootPart.Undo();
            g1.RootPart.Redo();
            g1.RootPart.Redo();

            Assert.That(g1.RootPart.UndoCount, Is.EqualTo(1));
            Assert.That(g1.GroupScale, Is.EqualTo(newSize));
        }
    }
}