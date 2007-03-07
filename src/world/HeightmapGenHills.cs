using System;

namespace libsecondlife
{
    public class HeightmapGenHills
    {
        private Random Rand = new Random();
        private int NumHills;
        private float HillMin;
        private float HillMax;
        private bool Island;
        private float[] heightmap;

        public float[] GenerateHeightmap(int numHills, float hillMin, float hillMax, bool island)
        {
            NumHills = numHills;
            HillMin = hillMin;
            HillMax = hillMax;
            Island = island;

            heightmap = new float[256 * 256];

            for (int i = 0; i < numHills; i++)
            {
                AddHill();
            }

            Normalize();

            return heightmap;
        }

        private void AddHill()
        {
            float x, y;
            float radius = RandomRange(HillMin, HillMax);

            if (Island)
            {
                // Which direction from the center of the map the hill is placed
                float theta = RandomRange(0, 6.28f);

                // How far from the center of the map to place the hill. The radius
                // is subtracted from the range to prevent any part of the hill from
                // reaching the edge of the map
                float distance = RandomRange(radius / 2.0f, 128.0f - radius);

                x = 128.0f + (float)Math.Cos(theta) * distance;
                y = 128.0f + (float)Math.Sin(theta) * distance;
            }
            else
            {
                x = RandomRange(-radius, 256.0f + radius);
                y = RandomRange(-radius, 256.0f + radius);
            }

            float radiusSq = radius * radius;
            float distSq;
            float height;

            int xMin = (int)(x - radius) - 1;
            int xMax = (int)(x + radius) + 1;
            if (xMin < 0) xMin = 0;
            if (xMax > 255) xMax = 255;

            int yMin = (int)(y - radius) - 1;
            int yMax = (int)(y + radius) + 1;
            if (yMin < 0) yMin = 0;
            if (yMax > 255) yMax = 255;

            // Loop through each affected cell and determine the height at that point
            for (int v = yMin; v <= yMax; ++v)
            {
                float fv = (float)v;

                for (int h = xMin; h <= xMax; ++h)
                {
                    float fh = (float)h;

                    // Determine how far from the center of this hill this point is
                    distSq = (x - fh) * (x - fh) + (y - fv) * (y - fv);
                    height = radiusSq - distSq;

                    // Don't add negative hill values
                    if (height > 0.0f) heightmap[h + v * 256] += height;
                }
            }
        }

        private void Normalize()
        {
            float min = heightmap[0];
            float max = heightmap[0];

            for (int x = 0; x < 256; x++)
            {
                for (int y = 0; y < 256; y++)
                {
                    if (heightmap[x + y * 256] < min) min = heightmap[x + y * 256];
                    if (heightmap[x + y * 256] > max) max = heightmap[x + y * 256];
                }
            }

            // Avoid a rare divide by zero
            if (min != max)
            {
                for (int x = 0; x < 256; x++)
                {
                    for (int y = 0; y < 256; y++)
                    {
                        heightmap[x + y * 256] = ((heightmap[x + y * 256] - min) / (max - min)) * (HillMax - HillMin);
                    }
                }
            }
        }

        private float RandomRange(float min, float max)
        {
            return (float)Rand.NextDouble() * (max - min) + min;
        }
    }
}
