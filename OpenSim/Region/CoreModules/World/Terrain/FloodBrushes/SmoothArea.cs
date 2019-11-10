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

namespace OpenSim.Region.CoreModules.World.Terrain.FloodBrushes
{
    public class SmoothArea : ITerrainFloodEffect
    {
        #region ITerrainFloodEffect Members

        public void FloodEffect(ITerrainChannel map, bool[,] fillArea, float height, float strength,
            int startX, int endX, int startY, int endY)
        {
            double area = 4;
            double step = 1;

            strength *= 0.002f;
            if(strength > 1.0f)
                strength = 1.0f;

            double[,] manipulate = new double[map.Width,map.Height];
            int x, y;
            for (x = startX; x <= endX; x++)
            {
                for (y = startY; y <= endY; y++)
                {
                    if (!fillArea[x, y])
                        continue;

                    double average = 0.0;
                    int avgsteps = 0;

                    double n;
                    for (n = -area; n < area; n += step)
                    {
                        double l;
                        for (l = -area; l < area; l += step)
                        {
                            avgsteps++;
                            average += GetBilinearInterpolate(x + n, y + l, map);
                        }
                    }

                    manipulate[x, y] = average / avgsteps;
                }
            }
            for (x = startX; x <= endX; x++)
            {
                for (y = startY; y <= endY; y++)
                {
                    if (!fillArea[x, y])
                        continue;

                    map[x, y] = (1.0 - strength) * map[x, y] + strength * manipulate[x, y];
                }
            }
        }

        #endregion

        private static double GetBilinearInterpolate(double x, double y, ITerrainChannel map)
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

            const int stepSize = 1;
            double h00 = map[(int) x, (int) y];
            double h10 = map[(int) x + stepSize, (int) y];
            double h01 = map[(int) x, (int) y + stepSize];
            double h11 = map[(int) x + stepSize, (int) y + stepSize];
            double h1 = h00;
            double h2 = h10;
            double h3 = h01;
            double h4 = h11;
            double a00 = h1;
            double a10 = h2 - h1;
            double a01 = h3 - h1;
            double a11 = h1 - h2 - h3 + h4;
            double partialx = x - (int) x;
            double partialz = y - (int) y;
            double hi = a00 + (a10 * partialx) + (a01 * partialz) + (a11 * partialx * partialz);
            return hi;
        }
    }
}