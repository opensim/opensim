using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Terrain.BasicTerrain
{
    static class Hills
    {
        /// <summary>
        /// Generates a series of spheres which are then either max()'d or added together. Inspired by suggestion from jh.
        /// </summary>
        /// <remarks>3-Clause BSD Licensed</remarks>
        /// <param name="number">The number of hills to generate</param>
        /// <param name="scale_min">The minimum size of each hill</param>
        /// <param name="scale_range">The maximum size of each hill</param>
        /// <param name="island">Whether to bias hills towards the center of the map</param>
        /// <param name="additive">Whether to add hills together or to pick the largest value</param>
        /// <param name="noisy">Generates hill-shaped noise instead of consistent hills</param>
        public static void hillsSpheres(float[,] map,int seed, int number, double scale_min, double scale_range, bool island, bool additive, bool noisy)
        {
            Random random = new Random(seed);
            int w = map.GetLength(0);
            int h = map.GetLength(1);
            int x, y;
            int i;

            for (i = 0; i < number; i++)
            {
                double rx = Math.Min(255.0, random.NextDouble() * w);
                double ry = Math.Min(255.0, random.NextDouble() * h);
                double rand = random.NextDouble();

                if (island)
                {
                    // Move everything towards the center
                    rx -= w / 2;
                    rx /= 2;
                    rx += w / 2;

                    ry -= h / 2;
                    ry /= 2;
                    ry += h / 2;
                }

                for (x = 0; x < w; x++)
                {
                    for (y = 0; y < h; y++)
                    {
                        if (noisy)
                            rand = random.NextDouble();

                        double z = (scale_min + (scale_range * rand));
                        z *= z;
                        z -= ((x - rx) * (x - rx)) + ((y - ry) * (y - ry));

                        if (z < 0)
                            z = 0;

                        if (additive)
                        {
                            map[x, y] += (float)z;
                        }
                        else
                        {
                            map[x, y] = (float)Math.Max(map[x, y], z);
                        }
                    }
                }
            }
        }
    }
}
