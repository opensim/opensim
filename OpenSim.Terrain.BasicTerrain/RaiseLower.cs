using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Terrain.BasicTerrain
{
    static class RaiseLower
    {
        /// <summary>
        /// Raises land around the selection
        /// </summary>
        /// <param name="rx">The center the X coordinate of where you wish to raise the land</param>
        /// <param name="ry">The center the Y coordinate of where you wish to raise the land</param>
        /// <param name="size">The radius of the dimple</param>
        /// <param name="amount">How much impact to add to the terrain (0..2 usually)</param>
        public static void raise(float[,] map, double rx, double ry, double size, double amount)
        {
            raiseSphere(map, rx, ry, size, amount);
        }

        /// <summary>
        /// Raises land in a sphere around the selection
        /// </summary>
        /// <param name="rx">The center the X coordinate of where you wish to raise the land</param>
        /// <param name="ry">The center the Y coordinate of where you wish to raise the land</param>
        /// <param name="size">The radius of the sphere dimple</param>
        /// <param name="amount">How much impact to add to the terrain (0..2 usually)</param>
        public static void raiseSphere(float[,] map, double rx, double ry, double size, double amount)
        {
            int x, y;
            int w = map.GetLength(0);
            int h = map.GetLength(1);

            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    double z = size;
                    z *= z;
                    z -= ((x - rx) * (x - rx)) + ((y - ry) * (y - ry));

                    if (z < 0)
                        z = 0;

                    map[x, y] += (float)(z * amount);
                }
            }
        }

        /// <summary>
        /// Lowers land in a sphere around the selection
        /// </summary>
        /// <param name="rx">The center the X coordinate of where you wish to lower the land</param>
        /// <param name="ry">The center the Y coordinate of where you wish to lower the land</param>
        /// <param name="size">The radius of the sphere dimple</param>
        /// <param name="amount">How much impact to remove from the terrain (0..2 usually)</param>
        public static void lower(float[,] map, double rx, double ry, double size, double amount)
        {
            lowerSphere(map, rx, ry, size, amount);
        }

        /// <summary>
        /// Lowers land in a sphere around the selection
        /// </summary>
        /// <param name="rx">The center the X coordinate of where you wish to lower the land</param>
        /// <param name="ry">The center the Y coordinate of where you wish to lower the land</param>
        /// <param name="size">The radius of the sphere dimple</param>
        /// <param name="amount">How much impact to remove from the terrain (0..2 usually)</param>
        public static void lowerSphere(float[,] map, double rx, double ry, double size, double amount)
        {
            int x, y;
            int w = map.GetLength(0);
            int h = map.GetLength(1);

            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    double z = size;
                    z *= z;
                    z -= ((x - rx) * (x - rx)) + ((y - ry) * (y - ry));

                    if (z < 0)
                        z = 0;

                    map[x, y] -= (float)(z * amount);
                }
            }
        }
    }
}
