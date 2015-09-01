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
 *     * Neither the name of the OpenSimulator Project nor the
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
 */

using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Terrain.PaintBrushes
{
    /// <summary>
    /// Thermal Weathering Paint Brush
    /// </summary>
    public class WeatherSphere : ITerrainPaintableEffect
    {
        private const double talus = 0.2;
        private const NeighbourSystem type = NeighbourSystem.Moore;

        #region Supporting Functions

        private static int[] Neighbours(NeighbourSystem neighbourType, int index)
        {
            int[] coord = new int[2];

            index++;

            switch (neighbourType)
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

        private enum NeighbourSystem
        {
            Moore,
            VonNeumann
        } ;

        #endregion

        #region ITerrainPaintableEffect Members

        public void PaintEffect(ITerrainChannel map, bool[,] mask, double rx, double ry, double rz,
            double strength, double duration, int startX, int endX, int startY, int endY)
        {
            strength = TerrainUtil.MetersToSphericalStrength(strength);

            int x,y;

            for (x = startX; x <= endX; x++)
            {
                for (y = startY; y <= endY; y++)
                {
                    if (!mask[x,y])
                        continue;

                    double z = TerrainUtil.SphericalFactor(x, y, rx, ry, strength);

                    if (z > 0) // add in non-zero amount
                    {
                        const int NEIGHBOUR_ME = 4;
                        const int NEIGHBOUR_MAX = 9;

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
