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
            int sx = (endX - startX + 1) / 2;
            if (sx > 4)
                sx = 4;

            int sy = (endY - startY + 1) / 2;
            if (sy > 4)
                sy = 4;

            strength *= 0.002f;
            if(strength > 1.0f)
                strength = 1.0f;

            float[,] tweak = new float[endX - startX + 1, endY - startY + 1];

            for (int x = startX, i = 0; x <= endX; x++, i++)
            {
                for (int y = startY, j = 0; y <= endY; y++, j++)
                {
                    if (!fillArea[x, y])
                        continue;

                    float average = 0f;
                    int avgsteps = 0;

                    for (int n = x - sx; n <= x + sx; ++n)
                    {
                        if (n >= 0 && n < map.Width)
                        {
                            for (int l = y - sy; l < y + sy; ++l)
                            {
                                if (l >= 0 && l < map.Height)
                                {
                                    avgsteps++;
                                    average += map[n, l];
                                }
                            }
                        }
                    }

                    tweak[i, j] = average / avgsteps;
                }
            }

            for (int x = startX, i = 0; x <= endX; x++, i++)
            {
                for (int y = startY, j = 0; y <= endY; y++, j++)
                {
                    float ty = tweak[i, j];
                    if (ty == 0.0)
                        continue;

                    map[x, y] = (1.0f - strength) * map[x, y] + strength * ty;
                }
            }
        }
    }
        #endregion
}