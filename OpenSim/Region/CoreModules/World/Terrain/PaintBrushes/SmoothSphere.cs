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

        public void PaintEffect(ITerrainChannel map, bool[,] mask, float rx, float ry, float rz,
            float size, float strength, int startX, int endX, int startY, int endY)
        {
            float distancefactor;
            float dx2;

            double[,] tweak = new double[endX - startX + 1, endX - startX + 1];
            int ssize = (int)(size + 0.5);
            if(ssize > 4)
                ssize = 4;

            size *= size;

            if (strength > 1.0f)
                strength = 1.0f;

            // compute delta map
            for (int x = startX,  i = 0; x <= endX; x++, i++)
            {
                dx2 = (x - rx) * (x - rx);
                for (int y = startY, j = 0; y <= endY; y++, j++)
                {
                    if (!mask[x, y])
                        continue;

                    distancefactor = (dx2 + (y - ry) * (y - ry)) / size;
                    if (distancefactor <= 1.0f)
                    {
                        distancefactor = strength * (1.0f - distancefactor);

                        double average = 0.0;
                        int avgsteps = 0;

                        for (int n = x - ssize; n <=  x + ssize; ++n)
                        {
                            if(n > 0 && n < map.Width)
                            {
                                for (int l = y - ssize; l <= y + ssize; ++l)
                                {
                                    if (l > 0 && l < map.Height)
                                    {
                                        avgsteps++;
                                        average += map[n, l];
                                    }
                                }
                            }
                        }
                        average /= avgsteps;
                        tweak[i, j] = distancefactor * (map[x, y] - average);
                    }
                }
            }
            // blend in map
            for (int x = startX, i = 0; x <= endX; x++, i++)
            {
                for (int y = startY, j = 0; y <= endY; y++, j++)
                {
                    double tz = tweak[i, j];
                    if(tz != 0.0)
                    {
                        double newz = map[x, y] - tz;
                        if (newz > 0.0)
                            map[x, y] = newz;
                    }
                }
            }
        }

        #endregion
    }
}
