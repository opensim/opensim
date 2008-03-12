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

namespace OpenSim.Region.Environment.Modules.Terrain.FloodBrushes
{
    public class NoiseArea : ITerrainFloodEffect
    {
        private double Noise(int x, int y)
        {
            // TODO: Seed
            int n = x + y * 57;
            n = (n<<13) ^ n;
            return (1.0 - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824.0);
        }

        private double SmoothedNoise1(int x, int y)
        {
            double corners = (Noise(x - 1, y - 1) + Noise(x + 1, y - 1) + Noise(x - 1, y + 1) + Noise(x + 1, y + 1)) / 16;
            double sides = (Noise(x - 1, y) + Noise(x + 1, y) + Noise(x, y - 1) + Noise(x, y + 1)) / 8;
            double center = Noise(x, y) / 4;
            return corners + sides + center;
        }

        private double Interpolate(double x, double y, double z)
        {
            return (x * (1.0 - z)) + (y * z);
        }

        private double InterpolatedNoise(double x, double y)
        {
            int integer_X = (int)(x);
            double fractional_X = x - integer_X;

            int integer_Y = (int)y;
            double fractional_Y = y - integer_Y;

            double v1 = SmoothedNoise1(integer_X, integer_Y);
            double v2 = SmoothedNoise1(integer_X + 1, integer_Y);
            double v3 = SmoothedNoise1(integer_X, integer_Y + 1);
            double v4 = SmoothedNoise1(integer_X + 1, integer_Y + 1);

            double i1 = Interpolate(v1, v2, fractional_X);
            double i2 = Interpolate(v3, v4, fractional_X);

            return Interpolate(i1, i2, fractional_Y);
        }

        private double PerlinNoise2D(double x, double y)
        {
            int octaves = 1;
            double persistence = 0.0005;

            double frequency = 0.0;
            double amplitude = 0.0;
            double total = 0.0;

            for (int i = 0; i < octaves; i++)
            {
                frequency = System.Math.Pow(2, i);
                amplitude = System.Math.Pow(persistence, i);

                total += InterpolatedNoise(x * frequency, y * frequency) * amplitude;
            }
            return total;
        }

        #region ITerrainFloodEffect Members

        public void FloodEffect(ITerrainChannel map, bool[,] fillArea, double strength)
        {
            int x, y;
            for (x = 0; x < map.Width; x++)
            {
                for (y = 0; y < map.Height; y++)
                {
                    if (fillArea[x, y] == true)
                    {
                        lock (OpenSim.Framework.Util.RandomClass)
                        {
                            double noise = PerlinNoise2D(x, y);//OpenSim.Framework.Util.RandomClass.NextDouble();
                            map[x, y] += (noise /*- 0.5*/) * strength;
                        }
                    }
                }
            }
        }

        #endregion
    }
}
