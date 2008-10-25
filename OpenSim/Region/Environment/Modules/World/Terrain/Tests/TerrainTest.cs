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
 *     * Neither the name of the OpenSim Project nor the
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
using OpenSim.Region.Environment.Modules.World.Terrain.PaintBrushes;

namespace OpenSim.Region.Environment.Modules.World.Terrain.Tests
{
    [TestFixture]
    public class TerrainTest
    {
//        [Test]
//        public void BrushTest()
//        {
//            TerrainChannel map = new TerrainChannel(256, 256);
//            bool[,] allowMask = new bool[map.Width,map.Height];
//            int x;
//            int y;
//            for (x=0; x<map.Width; x++)
//            {
//                for (y=0; y<map.Height; y++)
//                {
//                    allowMask[x,y] = true;
//                }
//            }
//
//            ITerrainPaintableEffect effect = new RaiseSphere();
//
//            effect.PaintEffect(map, allowMask, 128.0, 128.0, 23.0, 100, 0.1);
//            Assert.That(map[128, 128] > 0.0, "Raise brush not raising values.");
//            Assert.That(map[0, 128] > 0.0, "Raise brush lowering edge values.");
//
//            map = new TerrainChannel(256, 256);
//            effect = new LowerSphere();
//
//            effect.PaintEffect(map, allowMask, 128.0, 128.0, -1, 100, 0.1);
//            Assert.That(map[128, 128] < 0.0, "Lower not lowering values.");
//            Assert.That(map[0, 128] < 0.0, "Lower brush affecting edge values.");
//        }

        [Test]
        public void TerrainChannelTest()
        {
            TerrainChannel x = new TerrainChannel(256, 256);
            Assert.That(x[0, 0] == 0.0, "Terrain not initialising correctly.");

            x[0, 0] = 1.0;
            Assert.That(x[0, 0] == 1.0, "Terrain not setting values correctly.");

            x[0, 0] = 0;
            x[0, 0] += 5.0;
            x[0, 0] -= 1.0;
            Assert.That(x[0, 0] == 4.0, "Terrain addition/subtraction error.");

            x[0, 0] = Math.PI;
            double[,] doublesExport = x.GetDoubles();
            Assert.That(doublesExport[0, 0] == Math.PI, "Export to double[,] array not working correctly.");

            x[0, 0] = 1.0;
            float[] floatsExport = x.GetFloatsSerialised();
            Assert.That(floatsExport[0] == 1.0f, "Export to float[] not working correctly.");

            x[0, 0] = 1.0;
            Assert.That(x.Tainted(0, 0), "Terrain channel tainting not working correctly.");
            Assert.That(!x.Tainted(0, 0), "Terrain channel tainting not working correctly.");

            TerrainChannel y = x.Copy();
            Assert.That(!ReferenceEquals(x, y), "Terrain copy not duplicating correctly.");
            Assert.That(!ReferenceEquals(x.GetDoubles(), y.GetDoubles()), "Terrain array not duplicating correctly.");
        }
    }
}
