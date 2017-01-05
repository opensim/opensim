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
using System.Linq;
using System.Reflection;
using System.Threading;
using log4net.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Scripting.DynamicTexture;
using OpenSim.Region.CoreModules.Scripting.VectorRender;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Tests.Common;

namespace OpenSim.Tests.Stress
{
    [TestFixture]
    public class VectorRenderModuleStressTests : OpenSimTestCase
    {
        public Scene Scene { get; private set; }
        public DynamicTextureModule Dtm { get; private set; }
        public VectorRenderModule Vrm { get; private set; }

        private void SetupScene(bool reuseTextures)
        {
            Scene = new SceneHelpers().SetupScene();

            Dtm = new DynamicTextureModule();
            Dtm.ReuseTextures = reuseTextures;

            Vrm = new VectorRenderModule();

            SceneHelpers.SetupSceneModules(Scene, Dtm, Vrm);
        }

        [Test]
        public void TestConcurrentRepeatedDraw()
        {
            int threads = 4;
            TestHelpers.InMethod();

            SetupScene(false);

            List<Drawer> drawers = new List<Drawer>();

            for (int i = 0; i < threads; i++)
            {
                Drawer d = new Drawer(this, i);
                drawers.Add(d);
                Console.WriteLine("Starting drawer {0}", i);
                Util.FireAndForget(o => d.Draw(), null, "VectorRenderModuleStressTests.TestConcurrentRepeatedDraw");
            }

            Thread.Sleep(10 * 60 * 1000);

            drawers.ForEach(d => d.Ready = false);
            drawers.ForEach(d => Console.WriteLine("Drawer {0} drew {1} textures", d.Number, d.Pass + 1));
        }

        class Drawer
        {
            public int Number { get; private set; }
            public int Pass { get; private set; }
            public bool Ready { get; set; }

            private VectorRenderModuleStressTests m_tests;

            public Drawer(VectorRenderModuleStressTests tests, int number)
            {
                m_tests = tests;
                Number = number;
                Ready = true;
            }

            public void Draw()
            {
                SceneObjectGroup so = SceneHelpers.AddSceneObject(m_tests.Scene);

                while (Ready)
                {
                    UUID originalTextureID = so.RootPart.Shape.Textures.GetFace(0).TextureID;

                    // Ensure unique text
                    string text = string.Format("{0:D2}{1}", Number, Pass);

                    m_tests.Dtm.AddDynamicTextureData(
                        m_tests.Scene.RegionInfo.RegionID,
                        so.UUID,
                        m_tests.Vrm.GetContentType(),
                        string.Format("PenColour BLACK; MoveTo 40,220; FontSize 32; Text {0};", text),
                        "",
                        0);

                    Assert.That(originalTextureID, Is.Not.EqualTo(so.RootPart.Shape.Textures.GetFace(0).TextureID));

                    Pass++;
                }
            }
        }
    }
}