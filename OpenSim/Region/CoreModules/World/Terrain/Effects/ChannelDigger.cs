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

using System;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.CoreModules.World.Terrain.FloodBrushes;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.CoreModules.World.Terrain.Effects
{
    public class ChannelDigger : ITerrainEffect
    {
        private readonly int num_h = 4;
        private readonly int num_w = 4;

        private readonly ITerrainFloodEffect raiseFunction = new RaiseArea();
        private readonly ITerrainFloodEffect smoothFunction = new SmoothArea();

        #region ITerrainEffect Members

        public void RunEffect(ITerrainChannel map)
        {
            FillMap(map, 15);
            BuildTiles(map, 7);
            SmoothMap(map, 3);
        }

        #endregion

        private void SmoothMap(ITerrainChannel map, int rounds)
        {
            Boolean[,] bitmap = new bool[map.Width,map.Height];
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    bitmap[x, y] = true;
                }
            }

            for (int i = 0; i < rounds; i++)
            {
                smoothFunction.FloodEffect(map, bitmap, -1f, 1.0f, 0, map.Width - 1, 0, map.Height - 1);
            }
        }

        private void FillMap(ITerrainChannel map, float val)
        {
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    map[x, y] = val;
        }

        private void BuildTiles(ITerrainChannel map, float height)
        {
            int channelWidth = (int) Math.Floor((map.Width / num_w) * 0.8);
            int channelHeight = (int) Math.Floor((map.Height / num_h) * 0.8);
            int channelXOffset = (map.Width / num_w) - channelWidth;
            int channelYOffset = (map.Height / num_h) - channelHeight;

            for (int x = 0; x < num_w; x++)
            {
                for (int y = 0; y < num_h; y++)
                {
                    int xoff = ((channelXOffset + channelWidth) * x) + (channelXOffset / 2);
                    int yoff = ((channelYOffset + channelHeight) * y) + (channelYOffset / 2);

                    Boolean[,] bitmap = new bool[map.Width,map.Height];

                    for (int dx = 0; dx < channelWidth; dx++)
                    {
                        for (int dy = 0; dy < channelHeight; dy++)
                        {
                            bitmap[dx + xoff, dy + yoff] = true;
                        }
                    }

                    raiseFunction.FloodEffect(map, bitmap, -1f,(float)height, 0, map.Width - 1, 0, map.Height - 1);
                }
            }
        }
    }
}
