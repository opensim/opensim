using System;
using System.Collections.Generic;
using System.Text;
using libTerrain;

namespace OpenSim.Terrain
{
    public class TerrainEngine
    {
        /// <summary>
        /// A [normally] 256x256 heightmap
        /// </summary>
        public Channel heightmap;

        int w, h;

        /// <summary>
        /// Generate a new TerrainEngine instance and creates a new heightmap
        /// </summary>
        public TerrainEngine()
        {
            w = 256;
            h = 256;
            heightmap = new Channel(w, h);

        }

        /// <summary>
        /// Converts the heightmap to a 65536 value 1D floating point array
        /// </summary>
        /// <returns>A float[65536] array containing the heightmap</returns>
        public float[] getHeights1D()
        {
            float[] heights = new float[w * h];
            int i;

            for (i = 0; i < w * h; i++)
            {
                heights[i] = (float)heightmap.map[i / w, i % w];
            }

            return heights;
        }

        public float[,] getHeights2D()
        {
            float[,] heights = new float[w, h];
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    heights[x, y] = (float)heightmap.map[x, y];
                }
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
                heightmap.map[i / w, i % w] = heights[i];
            }
        }

        public float[,] setHeights2D(float[,] heights)
        {
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    heightmap.set(x,y,(double)heights[x,y]);
                }
            }
            return heights;
        }

        public void setRange(float min, float max)
        {
            heightmap.normalise((double)min, (double)max);
        }

        /// <summary>
        /// Loads a file consisting of 256x256 doubles and imports it as an array into the map.
        /// </summary>
        /// <remarks>TODO: Move this to libTerrain itself</remarks>
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
                    heightmap.map[x, y] = bs.ReadDouble();
                }
            }

            bs.Close();
            s.Close();
        }

        /// <summary>
        /// Loads a file consisting of 256x256 floats and imports it as an array into the map.
        /// </summary>
        /// <remarks>TODO: Move this to libTerrain itself</remarks>
        /// <param name="filename">The filename of the float array to import</param>
        public void loadFromFileF32(string filename)
        {
            System.IO.FileInfo file = new System.IO.FileInfo(filename);
            System.IO.FileStream s = file.Open(System.IO.FileMode.Open, System.IO.FileAccess.Read);
            System.IO.BinaryReader bs = new System.IO.BinaryReader(s);
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    heightmap.map[x, y] = (double)bs.ReadSingle();
                }
            }

            bs.Close();
            s.Close();
        }

        public void writeToFileF64(string filename)
        {
            System.IO.FileInfo file = new System.IO.FileInfo(filename);
            System.IO.FileStream s = file.Open(System.IO.FileMode.CreateNew, System.IO.FileAccess.Write);
            System.IO.BinaryWriter bs = new System.IO.BinaryWriter(s);

            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    bs.Write(heightmap.get(x,y));
                }
            }

            bs.Close();
            s.Close();
        }

        public void writeToFileF32(string filename)
        {
            System.IO.FileInfo file = new System.IO.FileInfo(filename);
            System.IO.FileStream s = file.Open(System.IO.FileMode.CreateNew, System.IO.FileAccess.Write);
            System.IO.BinaryWriter bs = new System.IO.BinaryWriter(s);

            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    bs.Write((float)heightmap.get(x, y));
                }
            }

            bs.Close();
            s.Close();
        }

        public void setSeed(int val)
        {
            heightmap.seed = val;
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
            lock (heightmap)
            {
                heightmap.raise(rx, ry, size, amount);
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
            lock (heightmap)
            {
                heightmap.lower(rx, ry, size, amount);
            }
        }

        /// <summary>
        /// Generates a simple set of hills in the shape of an island
        /// </summary>
        public void hills()
        {
            lock (heightmap)
            {
                heightmap.hillsSpheres(200, 20, 40, true, true, false);
                heightmap.normalise();
                heightmap *= 60.0; // Raise to 60m
            }
        }

        public float this[int x, int y]
        {
            get
            {
                return (float)heightmap.get(x,y);
            }
            set
            {
                heightmap.set(x,y,(double)value);
            }
        }
    }
}
