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
using OpenSim.Region.CoreModules.World.Terrain.PaintBrushes;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.World.Terrain.Tests
{
    [TestFixture]
    public class TerrainTest : OpenSimTestCase
    {
        [Test]
        public void BrushTest()
        {
            int midRegion = (int)Constants.RegionSize / 2;

            // Create a mask that covers only the left half of the region
            bool[,] allowMask = new bool[(int)Constants.RegionSize, 256];
            int x;
            int y;
            for (x = 0; x < midRegion; x++)
            {
                for (y = 0; y < (int)Constants.RegionSize; y++)
                {
                    allowMask[x,y] = true;
                }
            }

            //
            // Test RaiseSphere
            //
            TerrainChannel map = new TerrainChannel((int)Constants.RegionSize, (int)Constants.RegionSize);
            ITerrainPaintableEffect effect = new RaiseSphere();

            effect.PaintEffect(map, allowMask, midRegion, midRegion, -1.0f, 2, 6.0f,
                0, midRegion - 1,0, (int)Constants.RegionSize -1);
            Assert.That(map[127, midRegion] > 0.0, "Raise brush should raising value at this point (127,128).");
            Assert.That(map[125, midRegion] > 0.0, "Raise brush should raising value at this point (124,128).");
            Assert.That(map[120, midRegion] == 0.0, "Raise brush should not change value at this point (120,128).");
            Assert.That(map[128, midRegion] == 0.0, "Raise brush should not change value at this point (128,128).");
//            Assert.That(map[0, midRegion] == 0.0, "Raise brush should not change value at this point (0,128).");
            //
            // Test LowerSphere
            //
            map = new TerrainChannel((int)Constants.RegionSize, (int)Constants.RegionSize);
            for (x=0; x<map.Width; x++)
            {
                for (y=0; y<map.Height; y++)
                {
                    map[x,y] = 1.0;
                }
            }
            effect = new LowerSphere();

            effect.PaintEffect(map, allowMask, midRegion, midRegion, -1.0f, 2, 6.0f,
                0, (int)Constants.RegionSize -1,0, (int)Constants.RegionSize -1);
            Assert.That(map[127, midRegion] >= 0.0, "Lower should not lowering value below 0.0 at this point (127,128).");
            Assert.That(map[127, midRegion] == 0.0, "Lower brush should lowering value to 0.0 at this point (127,128).");
            Assert.That(map[125, midRegion] < 1.0, "Lower brush should lowering value at this point (124,128).");
            Assert.That(map[120, midRegion] == 1.0, "Lower brush should not change value at this point (120,128).");
            Assert.That(map[128, midRegion] == 1.0, "Lower brush should not change value at this point (128,128).");
//            Assert.That(map[0, midRegion] == 1.0, "Lower brush should not change value at this point (0,128).");
        }

        [Test]
        public void TerrainChannelTest()
        {
            TerrainChannel x = new TerrainChannel((int)Constants.RegionSize, (int)Constants.RegionSize);
            Assert.That(x[0, 0] == 0.0, "Terrain not initialising correctly.");

            x[0, 0] = 1.0;
            Assert.That(x[0, 0] == 1.0, "Terrain not setting values correctly.");

            x[0, 0] = 0;
            x[0, 0] += 5.0;
            x[0, 0] -= 1.0;
            Assert.That(x[0, 0] == 4.0, "Terrain addition/subtraction error.");

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
