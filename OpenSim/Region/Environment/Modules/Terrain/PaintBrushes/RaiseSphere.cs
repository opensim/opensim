using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Environment.Modules.Terrain;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Modules.Terrain.PaintBrushes
{
    class RaiseSphere : ITerrainPaintableEffect
    {
        #region ITerrainPaintableEffect Members

        public void PaintEffect(ITerrainChannel map, double rx, double ry, double strength)
        {
            int x, y;
            for (x = 0; x < map.Width; x++)
            {
                // Skip everything unlikely to be affected
                if (Math.Abs(x - rx) > strength * 1.1)
                    continue;

                for (y = 0; y < map.Height; y++)
                {
                    // Skip everything unlikely to be affected
                    if (Math.Abs(y - ry) > strength * 1.1)
                        continue;

                    // Calculate a sphere and add it to the heighmap
                    double z = strength;
                    z *= z;
                    z -= ((x - rx) * (x - rx)) + ((y - ry) * (y - ry));

                    if (z > 0.0)
                        map[x, y] += z;
                }
            }
        }

        #endregion
    }
}
