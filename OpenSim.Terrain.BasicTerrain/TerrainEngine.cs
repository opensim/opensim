using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Terrain.BasicTerrain;

namespace OpenSim.Terrain
{
    public class TerrainEngine
    {
        public float[,] map;
        public float[,] water;
        int w, h;

        public TerrainEngine()
        {
            w = 256;
            h = 256;
            map = new float[w, h];
            water = new float[w, h];

        }

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
        public void lower(double rx, double ry, double size, double amount)
        {
            lock (map)
            {
                RaiseLower.lowerSphere(this.map, rx, ry, size, amount);
            }
        }

        public void hills()
        {
            lock (map)
            {
                Hills.hillsSpheres(this.map, 1337, 200, 20, 40, true, true, false);
            }
        }

    }
}
