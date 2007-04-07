using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Terrain.BasicTerrain
{
    static class Normalise
    {
        /// <summary>
        /// Converts the heightmap to values ranging from 0..1
        /// </summary>
        /// <param name="map">The heightmap to be normalised</param>
        public static void normalise(float[,] map)
        {
            double max = findMax(map);
            double min = findMin(map);
            int w = map.GetLength(0);
            int h = map.GetLength(1);

            int x, y;

            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    map[x, y] = (float)((map[x, y] - min) * (1.0 / (max - min)));
                }
            }
        }

        /// <summary>
        /// Converts the heightmap to values ranging from 0..<newmax>
        /// </summary>
        /// <param name="map">The heightmap to be normalised</param>
        /// <param name="newmax">The new maximum height value of the map</param>
        public static void normalise(float[,] map, double newmax)
        {
            double max = findMax(map);
            double min = findMin(map);
            int w = map.GetLength(0);
            int h = map.GetLength(1);

            int x, y;

            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    map[x, y] = (float)((map[x, y] - min) * (1.0 / (max - min)) * newmax);
                }
            }
        }

        /// <summary>
        /// Finds the largest value in the heightmap
        /// </summary>
        /// <param name="map">The heightmap</param>
        /// <returns>The highest value</returns>
        public static double findMax(float[,] map)
        {
            int x, y;
            int w = map.GetLength(0);
            int h = map.GetLength(1);
            double max = double.MinValue;

            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    if (map[x, y] > max)
                        max = map[x, y];
                }
            }

            return max;
        }

        /// <summary>
        /// Finds the lowest value in a heightmap
        /// </summary>
        /// <param name="map">The heightmap</param>
        /// <returns>The minimum value</returns>
        public static double findMin(float[,] map)
        {
            int x, y;
            int w = map.GetLength(0);
            int h = map.GetLength(1);
            double min = double.MaxValue;

            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    if (map[x, y] < min)
                        min = map[x, y];
                }
            }

            return min;
        }
    }
}
