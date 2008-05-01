using System;
using NUnit.Framework;
using OpenSim.Region.Environment.Modules.World.Terrain.PaintBrushes;

namespace OpenSim.Region.Environment.Modules.World.Terrain.Tests
{
    [TestFixture]
    public class TerrainTest
    {
        [Test]
        public void BrushTest()
        {
            TerrainChannel x = new TerrainChannel(256, 256);
            ITerrainPaintableEffect effect = new RaiseSphere();

            effect.PaintEffect(x, 128.0, 128.0, 50, 0.1);
            Assert.That(x[128, 128] > 0.0, "Raise brush not raising values.");
            Assert.That(x[0, 128] > 0.0, "Raise brush lowering edge values.");

            x = new TerrainChannel(256, 256);
            effect = new LowerSphere();

            effect.PaintEffect(x, 128.0, 128.0, 50, 0.1);
            Assert.That(x[128, 128] < 0.0, "Lower not lowering values.");
            Assert.That(x[0, 128] < 0.0, "Lower brush affecting edge values.");
        }

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