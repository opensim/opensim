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
    public class RevertArea : ITerrainFloodEffect
    {
        private readonly ITerrainChannel m_revertmap;

        public RevertArea(ITerrainChannel revertmap)
        {
            m_revertmap = revertmap;
        }

        #region ITerrainFloodEffect Members

        /// <summary>
        /// reverts an area of the map to the heightfield stored in the revertmap
        /// </summary>
        /// <param name="map">the current heightmap</param>
        /// <param name="fillArea">array indicating which sections of the map are to be reverted</param>
        /// <param name="strength"></param>
        public void FloodEffect(ITerrainChannel map, bool[,] fillArea, float height, float strength,
            int startX, int endX, int startY, int endY)
        {
            strength *= 2f;
            if (strength >= .999f)
            {
                for (int x = startX; x <= endX; x++)
                {
                    for (int y = startY; y <= endY; y++)
                    {
                        if (fillArea[x, y])
                        {
                            map[x, y] = m_revertmap[x, y];
                        }
                    }
                }
                return;
            }

            if(strength < 1e-4f)
                return;

            float OneMinusstrength = 1.0f - strength;
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    if (fillArea[x, y])
                    {
                        map[x, y] = map[x, y] * OneMinusstrength + m_revertmap[x, y] * strength;
                    }
                }
            }
        }

        #endregion
    }
}
