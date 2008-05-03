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
 */

using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Modules.World.Terrain
{
    /// <summary>
    /// A new version of the old Channel class, simplified
    /// </summary>
    public class TerrainChannel : ITerrainChannel
    {
        private readonly bool[,] taint;
        private double[,] map;

        public TerrainChannel()
        {
            map = new double[Constants.RegionSize,Constants.RegionSize];
            taint = new bool[Constants.RegionSize / 16,Constants.RegionSize / 16];

            int x;
            for (x = 0; x < Constants.RegionSize; x++)
            {
                int y;
                for (y = 0; y < Constants.RegionSize; y++)
                {
                    map[x, y] = TerrainUtil.PerlinNoise2D(x, y, 3, 0.25) * 10;
                    double spherFac = TerrainUtil.SphericalFactor(x, y, Constants.RegionSize / 2.0, Constants.RegionSize / 2.0, 50) * 0.01;
                    if (map[x, y] < spherFac)
                    {
                        map[x, y] = spherFac;
                    }
                }
            }
        }

        public TerrainChannel(double[,] import)
        {
            map = import;
            taint = new bool[import.GetLength(0),import.GetLength(1)];
        }

        public TerrainChannel(bool createMap)
        {
            if (createMap)
            {
                map = new double[Constants.RegionSize,Constants.RegionSize];
                taint = new bool[Constants.RegionSize / 16,Constants.RegionSize / 16];
            }
        }

        public TerrainChannel(int w, int h)
        {
            map = new double[w,h];
            taint = new bool[w / 16,h / 16];
        }

        #region ITerrainChannel Members

        public int Width
        {
            get { return map.GetLength(0); }
        }

        public int Height
        {
            get { return map.GetLength(1); }
        }

        public ITerrainChannel MakeCopy()
        {
            TerrainChannel copy = new TerrainChannel(false);
            copy.map = (double[,]) map.Clone();

            return copy;
        }

        public float[] GetFloatsSerialised()
        {
            float[] heights = new float[Width * Height];
            int i;

            for (i = 0; i < Width * Height; i++)
            {
                heights[i] = (float) map[i % Width, i / Width];
            }

            return heights;
        }

        public double[,] GetDoubles()
        {
            return map;
        }

        public double this[int x, int y]
        {
            get { return map[x, y]; }
            set
            {
                if (map[x, y] != value)
                {
                    taint[x / 16, y / 16] = true;
                    map[x, y] = value;
                }
            }
        }

        public bool Tainted(int x, int y)
        {
            if (taint[x / 16, y / 16])
            {
                taint[x / 16, y / 16] = false;
                return true;
            }
            return false;
        }

        #endregion

        public TerrainChannel Copy()
        {
            TerrainChannel copy = new TerrainChannel(false);
            copy.map = (double[,]) map.Clone();

            return copy;
        }
    }
}