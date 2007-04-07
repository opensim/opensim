using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Terrain.BasicTerrain;

namespace OpenSim.Terrain
{
    public class TerrainEngine
    {
        /// <summary>
        /// A [normally] 256x256 heightmap
        /// </summary>
        public float[,] map;
        /// <summary>
        /// A 256x256 heightmap storing water height values
        /// </summary>
        public float[,] water;
        int w, h;

        /// <summary>
        /// Generate a new TerrainEngine instance and creates a new heightmap
        /// </summary>
        public TerrainEngine()
        {
            w = 256;
            h = 256;
            map = new float[w, h];
            water = new float[w, h];

        }

        /// <summary>
        /// Converts the heightmap to a 65536 value 1D floating point array
        /// </summary>
        /// <returns>A float[65536] array containing the heightmap</returns>
        public float[] getHeights1D()
        {
            float[] heights = new float[w*h];
            int i;
            for(i=0;i<w*h;i++) {
                heights[i] = map[i / w, i % w];
            }
            return heights;
        }

        /// <summary>
        /// Imports a 1D floating point array into the 2D heightmap array
        /// </summary>
        /// <param name="heights">The array to import (must have 65536 members)</param>
        public void setHeights1D(float[] heights)
        {
            int i;
            for (i = 0; i < w * h; i++)
            {
                map[i / w, i % w] = heights[i];
            }
        }

        /// <summary>
        /// Loads a file consisting of 256x256 doubles and imports it as an array into the map.
        /// </summary>
        /// <param name="filename">The filename of the double array to import</param>
        public void loadFromFileF64(string filename)
        {
            System.IO.FileInfo file = new System.IO.FileInfo(filename);
            System.IO.FileStream s = file.Open(System.IO.FileMode.Open, System.IO.FileAccess.Read);
            System.IO.BinaryReader bs = new System.IO.BinaryReader(s);
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    map[x, y] = (float)bs.ReadDouble();
                }
            }
        }

        /// <summary>
        /// Swaps the references between the height and water buffers to allow you to edit the water heightmap. Remember to swap back when you are done.
        /// </summary>
        public void swapWaterBuffer()
        {
            float[,] temp = map;
            map = water;
            water = temp;
        }

        /// <summary>
        /// Raises land in a sphere around the specified coordinates
        /// </summary>
        /// <param name="rx">Center of the sphere on the X axis</param>
        /// <param name="ry">Center of the sphere on the Y axis</param>
        /// <param name="size">The radius of the sphere</param>
        /// <param name="amount">Scale the height of the sphere by this amount (recommended 0..2)</param>
        public void raise(double rx, double ry, double size, double amount)
        {
            lock (map)
            {
                RaiseLower.raiseSphere(this.map, rx, ry, size, amount);
            }
        }

        /// <summary>
        /// Lowers the land in a sphere around the specified coordinates
        /// </summary>
        /// <param name="rx">The center of the sphere at the X axis</param>
        /// <param name="ry">The center of the sphere at the Y axis</param>
        /// <param name="size">The radius of the sphere in meters</param>
        /// <param name="amount">Scale the height of the sphere by this amount (recommended 0..2)</param>
        public void lower(double rx, double ry, double size, double amount)
        {
            lock (map)
            {
                RaiseLower.lowerSphere(this.map, rx, ry, size, amount);
            }
        }

        /// <summary>
        /// Generates a simple set of hills in the shape of an island
        /// </summary>
        public void hills()
        {
            lock (map)
            {
                Hills.hillsSpheres(this.map, 1337, 200, 20, 40, true, true, false);
                Normalise.normalise(this.map,60);
            }
        }

    }
}
