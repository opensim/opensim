/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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

using System;
using System.Collections.Generic;
using System.Text;

namespace libTerrain
{
    partial class Channel
    {
        /// <summary>
        /// Generates a series of spheres which are then either max()'d or added together. Inspired by suggestion from jh.
        /// </summary>
        /// <remarks>3-Clause BSD Licensed</remarks>
        /// <param name="number">The number of hills to generate</param>
        /// <param name="scale_min">The minimum size of each hill</param>
        /// <param name="scale_range">The maximum size of each hill</param>
        /// <param name="island">Whether to bias hills towards the center of the map</param>
        /// <param name="additive">Whether to add hills together or to pick the largest value</param>
        /// <param name="noisy">Generates hill-shaped noise instead of consistent hills</param>
        public void HillsSpheres(int number, double scale_min, double scale_range, bool island, bool additive, bool noisy)
        {
            SetDiff();

            Random random = new Random(seed);

            int x, y;
            int i;

            for (i = 0; i < number; i++)
            {
                double rx = Math.Min(255.0, random.NextDouble() * w);
                double ry = Math.Min(255.0, random.NextDouble() * h);
                double rand = random.NextDouble();

                if (island)
                {
                    // Move everything towards the center
                    rx -= w / 2;
                    rx /= 2;
                    rx += w / 2;

                    ry -= h / 2;
                    ry /= 2;
                    ry += h / 2;
                }

                for (x = 0; x < w; x++)
                {
                    for (y = 0; y < h; y++)
                    {
                        if (noisy)
                            rand = random.NextDouble();

                        double z = (scale_min + (scale_range * rand));
                        z *= z;
                        z -= ((x - rx) * (x - rx)) + ((y - ry) * (y - ry));

                        if (z < 0)
                            z = 0;

                        if (additive)
                        {
                            map[x, y] += z;
                        }
                        else
                        {
                            map[x, y] = Math.Max(map[x, y], z);
                        }
                    }
                }
            }

            Normalise();
        }

        /// <summary>
        /// Generates a series of cones which are then either max()'d or added together. Inspired by suggestion from jh.
        /// </summary>
        /// <remarks>3-Clause BSD Licensed</remarks>
        /// <param name="number">The number of hills to generate</param>
        /// <param name="scale_min">The minimum size of each hill</param>
        /// <param name="scale_range">The maximum size of each hill</param>
        /// <param name="island">Whether to bias hills towards the center of the map</param>
        /// <param name="additive">Whether to add hills together or to pick the largest value</param>
        /// <param name="noisy">Generates hill-shaped noise instead of consistent hills</param>
        public void HillsCones(int number, double scale_min, double scale_range, bool island, bool additive, bool noisy)
        {
            SetDiff();

            Random random = new Random(seed);

            int x, y;
            int i;

            for (i = 0; i < number; i++)
            {
                double rx = Math.Min(255.0, random.NextDouble() * w);
                double ry = Math.Min(255.0, random.NextDouble() * h);
                double rand = random.NextDouble();

                if (island)
                {
                    // Move everything towards the center
                    rx -= w / 2;
                    rx /= 2;
                    rx += w / 2;

                    ry -= h / 2;
                    ry /= 2;
                    ry += h / 2;
                }

                for (x = 0; x < w; x++)
                {
                    for (y = 0; y < h; y++)
                    {
                        if (noisy)
                            rand = random.NextDouble();

                        double z = (scale_min + (scale_range * rand));
                        z -= Math.Sqrt(((x - rx) * (x - rx)) + ((y - ry) * (y - ry)));

                        if (z < 0)
                            z = 0;

                        if (additive)
                        {
                            map[x, y] += z;
                        }
                        else
                        {
                            map[x, y] = Math.Max(map[x, y], z);
                        }
                    }
                }
            }

            Normalise();
        }

        public void HillsBlocks(int number, double scale_min, double scale_range, bool island, bool additive, bool noisy)
        {
            SetDiff();

            Random random = new Random(seed);

            int x, y;
            int i;

            for (i = 0; i < number; i++)
            {
                double rx = Math.Min(255.0, random.NextDouble() * w);
                double ry = Math.Min(255.0, random.NextDouble() * h);
                double rand = random.NextDouble();

                if (island)
                {
                    // Move everything towards the center
                    rx -= w / 2;
                    rx /= 2;
                    rx += w / 2;

                    ry -= h / 2;
                    ry /= 2;
                    ry += h / 2;
                }

                for (x = 0; x < w; x++)
                {
                    for (y = 0; y < h; y++)
                    {
                        if (noisy)
                            rand = random.NextDouble();

                        double z = (scale_min + (scale_range * rand));
                        z -= Math.Abs(x-rx) + Math.Abs(y-ry);
                        //z -= Math.Sqrt(((x - rx) * (x - rx)) + ((y - ry) * (y - ry)));

                        if (z < 0)
                            z = 0;

                        if (additive)
                        {
                            map[x, y] += z;
                        }
                        else
                        {
                            map[x, y] = Math.Max(map[x, y], z);
                        }
                    }
                }
            }

            Normalise();
        }

        public void HillsSquared(int number, double scale_min, double scale_range, bool island, bool additive, bool noisy)
        {
            SetDiff();

            Random random = new Random(seed);

            int x, y;
            int i;

            for (i = 0; i < number; i++)
            {
                double rx = Math.Min(255.0, random.NextDouble() * w);
                double ry = Math.Min(255.0, random.NextDouble() * h);
                double rand = random.NextDouble();

                if (island)
                {
                    // Move everything towards the center
                    rx -= w / 2;
                    rx /= 2;
                    rx += w / 2;

                    ry -= h / 2;
                    ry /= 2;
                    ry += h / 2;
                }

                for (x = 0; x < w; x++)
                {
                    for (y = 0; y < h; y++)
                    {
                        if (noisy)
                            rand = random.NextDouble();

                        double z = (scale_min + (scale_range * rand));
                        z *= z * z * z;
                        double dx = Math.Abs(x - rx);
                        double dy = Math.Abs(y - ry);
                        z -= (dx * dx * dx * dx) + (dy * dy * dy * dy);

                        if (z < 0)
                            z = 0;

                        if (additive)
                        {
                            map[x, y] += z;
                        }
                        else
                        {
                            map[x, y] = Math.Max(map[x, y], z);
                        }
                    }
                }
            }

            Normalise();
        }

    }
}
