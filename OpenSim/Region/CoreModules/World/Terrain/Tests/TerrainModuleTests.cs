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
using NUnit.Framework;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.Terrain.Tests
{
    public class TerrainModuleTests : OpenSimTestCase
    {
        [Test]
        public void TestTerrainFill()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            //UUID userId = TestHelpers.ParseTail(0x1);

            TerrainModule tm = new TerrainModule();
            Scene scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(scene, tm);

            // Fillheight of 30
            {
                double fillHeight = 30;

                tm.InterfaceFillTerrain(new object[] { fillHeight });

                double height = scene.Heightmap[128, 128];

                Assert.AreEqual(fillHeight, height);
            }

            // Max fillheight of 30
            // According to http://wiki.secondlife.com/wiki/Tips_for_Creating_Heightfields_and_Details_on_Terrain_RAW_Files#Notes_for_Creating_Height_Field_Maps_for_Second_Life
            {
                double fillHeight = 508;

                tm.InterfaceFillTerrain(new object[] { fillHeight });

                double height = scene.Heightmap[128, 128];

                Assert.AreEqual(fillHeight, height);
            }
        }
    }
}