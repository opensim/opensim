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
    public class SmoothSphere : ITerrainPaintableEffect
    {
        #region ITerrainPaintableEffect Members

        public void PaintEffect(ITerrainChannel map, bool[,] mask, double rx, double ry, double rz,
            double strength, double duration, int startX, int endX, int startY, int endY)
        {
            strength = TerrainUtil.MetersToSphericalStrength(strength);

            int x, y;
            double[,] tweak = new double[map.Width,map.Height];

            double area = strength;
            double step = strength / 4.0;
            duration = 0.03; //MCP Should be read from ini file


            // compute delta map
            for (x = startX; x <= endX; x++)
            {
                for (y = startY; y <= endY; y++)
                {
                    if (!mask[x, y])
                        continue;

                    double z = TerrainUtil.SphericalFactor(x, y, rx, ry, strength);

                    if (z > 0) // add in non-zero amount
                    {
                        double average = 0.0;
                        int avgsteps = 0;

                        double n;
                        for (n = 0.0 - area; n < area; n += step)
                        {
                            double l;
                            for (l = 0.0 - area; l < area; l += step)
                            {
                                avgsteps++;
                                average += TerrainUtil.GetBilinearInterpolate(x + n, y + l, map);
                            }
                        }
                        tweak[x, y] = average / avgsteps;
                    }
                }
            }
            // blend in map
            for (x = startX; x <= endX; x++)
            {
                for (y = startY; y <= endY; y++)
                {
                    if (!mask[x, y])
                        continue;

                    double z = TerrainUtil.SphericalFactor(x, y, rx, ry, strength);

                    if (z > 0) // add in non-zero amount
                    {
                        double da = z;
                        double a = (map[x, y] - tweak[x, y]) * da;
                        double newz = map[x, y] - (a * duration);

                        if (newz > 0.0)
                            map[x, y] = newz;
                    }
                }
            }
        }

        #endregion
    }
}
