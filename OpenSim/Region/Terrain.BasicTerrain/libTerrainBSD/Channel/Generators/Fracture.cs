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

using System;

namespace libTerrain
{
    partial class Channel
    {
        /// <summary>
        /// Produces a set of coordinates defined by an edge point. Eg - 0 = 0,0. 256 = 0,256. 512 = 256,256
        /// Assumes a 256^2 heightmap. This needs fixing for input values of w,h
        /// </summary>
        /// <param name="val"></param>
        /// <param name="w"></param>
        /// <param name="h"></param>
        /// <returns></returns>
        private int[] RadialEdge256(int val)
        {
            // Four cases:
            // 1.   000..255      return 0,val
            // 2.   256..511      return val - 256,255
            // 3.   512..767      return 255, val - 511
            // 4.   768..1023     return val - 768,0

            int[] ret = new int[2];

            if (val < 256)
            {
                ret[0] = 0;
                ret[1] = val;
                return ret;
            }
            if (val < 512)
            {
                ret[0] = (val%256);
                ret[1] = 255;
                return ret;
            }
            if (val < 768)
            {
                ret[0] = 255;
                ret[1] = 255 - (val%256);
                return ret;
            }
            if (val < 1024)
            {
                ret[0] = 255 - (val%256);
                ret[1] = 255;
                return ret;
            }

            throw new Exception("Out of bounds parameter (val)");
        }

        public void Fracture(int number, double scalemin, double scalemax)
        {
            SetDiff();

            Random rand = new Random(seed);

            for (int i = 0; i < number; i++)
            {
                int[] a, b;

                a = RadialEdge256(rand.Next(1023)); // TODO: Broken
                b = RadialEdge256(rand.Next(1023)); // TODO: Broken
                double z = rand.NextDouble();
                double u = rand.NextDouble();
                double v = rand.NextDouble();

                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        double miny = Tools.LinearInterpolate(a[1], b[1], (double) x/(double) w);

                        if (v >= 0.5)
                        {
                            if (u >= 0.5)
                            {
                                if (y > miny)
                                {
                                    map[x, y] += Tools.LinearInterpolate(scalemin, scalemax, z);
                                }
                            }
                            else
                            {
                                if (y < miny)
                                {
                                    map[x, y] += Tools.LinearInterpolate(scalemin, scalemax, z);
                                }
                            }
                        }
                        else
                        {
                            if (u >= 0.5)
                            {
                                if (x > miny)
                                {
                                    map[x, y] += Tools.LinearInterpolate(scalemin, scalemax, z);
                                }
                            }
                            else
                            {
                                if (x < miny)
                                {
                                    map[x, y] += Tools.LinearInterpolate(scalemin, scalemax, z);
                                }
                            }
                        }
                    }
                }
            }
            Normalise();
        }
    }
}
