/*
* Copyright (c) Contributors, http://opensimulator.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Modules.Terrain.PaintBrushes
{
    /// <summary>
    /// Thermal Weathering Paint Brush
    /// </summary>
    public class WeatherSphere : ITerrainPaintableEffect
    {
        NeighbourSystem type = NeighbourSystem.Moore; // Parameter
        double talus = 0.2; // Number of meters max difference before stop eroding. Tweakage required.

        #region Supporting Functions
        private enum NeighbourSystem
        {
            Moore,
            VonNeumann
        } ;

        private int[] Neighbours(NeighbourSystem type, int index)
        {
            int[] coord = new int[2];

            index++;

            switch (type)
            {
                case NeighbourSystem.Moore:
                    switch (index)
                    {
                        case 1:
                            coord[0] = -1;
                            coord[1] = -1;
                            break;

                        case 2:
                            coord[0] = -0;
                            coord[1] = -1;
                            break;

                        case 3:
                            coord[0] = +1;
                            coord[1] = -1;
                            break;

                        case 4:
                            coord[0] = -1;
                            coord[1] = -0;
                            break;

                        case 5:
                            coord[0] = -0;
                            coord[1] = -0;
                            break;

                        case 6:
                            coord[0] = +1;
                            coord[1] = -0;
                            break;

                        case 7:
                            coord[0] = -1;
                            coord[1] = +1;
                            break;

                        case 8:
                            coord[0] = -0;
                            coord[1] = +1;
                            break;

                        case 9:
                            coord[0] = +1;
                            coord[1] = +1;
                            break;

                        default:
                            break;
                    }
                    break;

                case NeighbourSystem.VonNeumann:
                    switch (index)
                    {
                        case 1:
                            coord[0] = 0;
                            coord[1] = -1;
                            break;

                        case 2:
                            coord[0] = -1;
                            coord[1] = 0;
                            break;

                        case 3:
                            coord[0] = +1;
                            coord[1] = 0;
                            break;

                        case 4:
                            coord[0] = 0;
                            coord[1] = +1;
                            break;

                        case 5:
                            coord[0] = -0;
                            coord[1] = -0;
                            break;

                        default:
                            break;
                    }
                    break;
            }

            return coord;
        }

        private double SphericalFactor(double x, double y, double rx, double ry, double size)
        {
            double z = size * size - ((x - rx) * (x - rx) + (y - ry) * (y - ry));
            return z;
        }

        private double GetBilinearInterpolate(double x, double y, ITerrainChannel map)
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

        #endregion

        #region ITerrainPaintableEffect Members

        public void PaintEffect(ITerrainChannel map, double rx, double ry, double strength, double duration)
        {
            int x, y;

            for (x = 0; x < map.Width; x++)
            {
                for (y = 0; y < map.Height; y++)
                {
                    double z = SphericalFactor(x, y, rx, ry, strength);

                    if (z > 0) // add in non-zero amount 
                    {
                        int NEIGHBOUR_ME = 4;

                        int NEIGHBOUR_MAX = type == NeighbourSystem.Moore ? 9 : 5;

                        for (int j = 0; j < NEIGHBOUR_MAX; j++)
                        {
                            if (j != NEIGHBOUR_ME)
                            {
                                int[] coords = Neighbours(type, j);

                                coords[0] += x;
                                coords[1] += y;

                                if (coords[0] > map.Width - 1)
                                    continue;
                                if (coords[1] > map.Height - 1)
                                    continue;
                                if (coords[0] < 0)
                                    continue;
                                if (coords[1] < 0)
                                    continue;

                                double heightF = map[x, y];
                                double target = map[coords[0], coords[1]];

                                if (target > heightF + talus)
                                {
                                    double calc = duration * ((target - heightF) - talus) * z;
                                    heightF += calc;
                                    target -= calc;
                                }

                                map[x, y] = heightF;
                                map[coords[0], coords[1]] = target;
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
}
