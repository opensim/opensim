using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Terrain.BasicTerrain
{
    static class Normalise
    {
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
