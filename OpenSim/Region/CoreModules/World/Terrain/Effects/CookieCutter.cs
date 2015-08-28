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
using OpenSim.Region.CoreModules.World.Terrain.PaintBrushes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using log4net;
using System.Reflection;

namespace OpenSim.Region.CoreModules.World.Terrain.Effects
{
    internal class CookieCutter : ITerrainEffect
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region ITerrainEffect Members

        public void RunEffect(ITerrainChannel map)
        {
            ITerrainPaintableEffect eroder = new WeatherSphere();

            bool[,] cliffMask = new bool[map.Width,map.Height];
            bool[,] channelMask = new bool[map.Width,map.Height];
            bool[,] smoothMask = new bool[map.Width,map.Height];
            bool[,] allowMask = new bool[map.Width,map.Height];

            m_log.Info("S1");

            // Step one, generate rough mask
            int x, y;
            for (x = 0; x < map.Width; x++)
            {
                for (y = 0; y < map.Height; y++)
                {
                    m_log.Info(".");
                    smoothMask[x, y] = true;
                    allowMask[x,y] = true;

                    // Start underwater
                    map[x, y] = TerrainUtil.PerlinNoise2D(x, y, 3, 0.25) * 5;
                    // Add a little height. (terrain should now be above water, mostly.)
                    map[x, y] += 20;

                    const int channelsX = 4;
                    int channelWidth = (map.Width / channelsX / 4);
                    const int channelsY = 4;
                    int channelHeight = (map.Height / channelsY / 4);

                    SetLowerChannel(map, cliffMask, channelMask, x, y, channelsX, channelWidth, map.Width, x);
                    SetLowerChannel(map, cliffMask, channelMask, x, y, channelsY, channelHeight, map.Height, y);
                }
            }

            m_log.Info("S2");
            //smooth.FloodEffect(map, smoothMask, 4.0);

            m_log.Info("S3");
            for (x = 0; x < map.Width; x++)
            {
                for (y = 0; y < map.Height; y++)
                {
                    if (cliffMask[x, y])
                        eroder.PaintEffect(map, allowMask, x, y, -1, 4, 0.1,0,map.Width - 1,0,map.Height - 1);
                }
            }

            for (x = 0; x < map.Width; x += 2)
            {
                for (y = 0; y < map.Height; y += 2)
                {
                    if (map[x, y] < 0.1)
                        map[x, y] = 0.1;
                    if (map[x, y] > 256)
                        map[x, y] = 256;
                }
            }
            //smooth.FloodEffect(map, smoothMask, 4.0);
        }

        #endregion

        private static void SetLowerChannel(ITerrainChannel map, bool[,] cliffMask, bool[,] channelMask, int x, int y, int numChannels, int channelWidth,
                                            int mapSize, int rp)
        {
            for (int i = 0; i < numChannels; i++)
            {
                double distanceToLine = Math.Abs(rp - ((mapSize / numChannels) * i));

                if (distanceToLine < channelWidth)
                {
                    if (channelMask[x, y])
                        return;

                    // Remove channels
                    map[x, y] -= 10;
                    channelMask[x, y] = true;
                }
                if (distanceToLine < 1)
                {
                    cliffMask[x, y] = true;
                }
            }
        }
    }
}
