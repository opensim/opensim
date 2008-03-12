using System;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Modules.Terrain
{
    public static class TerrainUtil
    {
        public static double MetersToSphericalStrength(double size)
        {
            return Math.Pow(2, size);
        }

        public static double SphericalFactor(double x, double y, double rx, double ry, double size)
        {
            return size * size - ((x - rx) * (x - rx) + (y - ry) * (y - ry));
        }

        public static double GetBilinearInterpolate(double x, double y, ITerrainChannel map)
        {
            int w = map.Width;
            int h = map.Height;

            if (x > w - 2.0)
                x = w - 2.0;
            if (y > h - 2.0)
                y = h - 2.0;
            if (x < 0.0)
                x = 0.0;
            if (y < 0.0)
                y = 0.0;

            int stepSize = 1;
            double h00 = map[(int)x, (int)y];
            double h10 = map[(int)x + stepSize, (int)y];
            double h01 = map[(int)x, (int)y + stepSize];
            double h11 = map[(int)x + stepSize, (int)y + stepSize];
            double h1 = h00;
            double h2 = h10;
            double h3 = h01;
            double h4 = h11;
            double a00 = h1;
            double a10 = h2 - h1;
            double a01 = h3 - h1;
            double a11 = h1 - h2 - h3 + h4;
            double partialx = x - (int)x;
            double partialz = y - (int)y;
            double hi = a00 + (a10 * partialx) + (a01 * partialz) + (a11 * partialx * partialz);
            return hi;
        }
    }
}
