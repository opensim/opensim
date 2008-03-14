
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Modules.Terrain
{

    /// <summary>
    /// A new version of the old Channel class, simplified
    /// </summary>
    public class TerrainChannel : ITerrainChannel
    {
        private double[,] map;
        private bool[,] taint;

        public int Width
        {
            get { return map.GetLength(0); }
        }

        public int Height
        {
            get { return map.GetLength(1); }
        }

        public TerrainChannel Copy()
        {
            TerrainChannel copy = new TerrainChannel(false);
            copy.map = (double[,])this.map.Clone();

            return copy;
        }

        public float[] GetFloatsSerialised()
        {
            float[] heights = new float[Width * Height];
            int i;

            for (i = 0; i < Width * Height; i++)
            {
                heights[i] = (float)map[i % Width, i / Width];
            }

            return heights;
        }

        public double[,] GetDoubles()
        {
            return map;
        }

        public double this[int x, int y]
        {
            get
            {
                return map[x, y];
            }
            set
            {
                if (map[x, y] != value)
                {
                    taint[x / 16, y / 16] = true;
                    map[x, y] = value;
                }
            }
        }

        public bool Tainted(int x, int y)
        {
            if (taint[x / 16, y / 16] != false)
            {
                taint[x / 16, y / 16] = false;
                return true;
            }
            else
            {
                return false;
            }
        }

        public TerrainChannel()
        {
            map = new double[Constants.RegionSize, Constants.RegionSize];
            taint = new bool[Constants.RegionSize / 16, Constants.RegionSize / 16];

            int x, y;
            for (x = 0; x < Constants.RegionSize; x++)
            {
                for (y = 0; y < Constants.RegionSize; y++)
                {
                    map[x, y] = 60.0 - // 60 = Sphere Radius
                        ((x - (Constants.RegionSize / 2)) * (x - (Constants.RegionSize / 2)) +
                        (y - (Constants.RegionSize / 2)) * (y - (Constants.RegionSize / 2)));
                }
            }
        }

        public TerrainChannel(double[,] import)
        {
            map = import;
            taint = new bool[import.GetLength(0), import.GetLength(1)];
        }

        public TerrainChannel(bool createMap)
        {
            if (createMap)
            {
                map = new double[Constants.RegionSize, Constants.RegionSize];
                taint = new bool[Constants.RegionSize / 16, Constants.RegionSize / 16];
            }
        }

        public TerrainChannel(int w, int h)
        {
            map = new double[w, h];
            taint = new bool[w / 16, h / 16];
        }
    }
}
