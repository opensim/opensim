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
        public Channel normalise()
        {
            double max = findMax();
            double min = findMin();

            int x, y;

            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    map[x, y] = (map[x, y] - min) * (1.0 / (max - min));
                }
            }

            return this;
        }

        public Channel normalise(double minv, double maxv)
        {
            double max = findMax();
            double min = findMin();

            int x, y;

            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    double val = (map[x, y] - min) * (1.0 / max - min);
                    val *= maxv - minv;
                    val += minv;

                    map[x, y] = val;
                }
            }

            return this;
        }

        public Channel clip()
        {
            int x, y;

            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    setClip(x, y, map[x, y]);
                }
            }

            return this;
        }

        public Channel clip(double min, double max)
        {
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    double val = map[x, y];
                    if (val > max) val = max;
                    if (val < min) val = min;
                    map[x, y] = val;
                }
            }
            return this;
        }

        public Channel crop(int x1, int y1, int x2, int y2)
        {
            int width = x1 - x2 + 1;
            int height = y1 - y2 + 1;
            Channel chan = new Channel(width, height);

            int x, y;
            int nx, ny;

            nx = 0;
            for (x = x1; x < x2; x++)
            {
                ny = 0;
                for (y = y1; y < y2; y++)
                {
                    chan.map[nx, ny] = map[x, y];

                    ny++;
                }
                nx++;
            }

            return this;
        }

        public Channel addClip(Channel other)
        {
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    map[x, y] = other.map[x, y];
                    if (map[x, y] > 1)
                        map[x, y] = 1;
                    if (map[x, y] < 0)
                        map[x, y] = 0;
                }
            }
            return this;
        }

        public void smooth(double amount)
        {
            double area = amount;
            double step = amount / 4.0;

            double[,] manipulate = new double[w, h];
            int x, y;
            double n, l;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    double average = 0.0;
                    int avgsteps = 0;

                    for (n = 0.0 - area; n < area; n += step)
                    {
                        for (l = 0.0 - area; l < area; l += step)
                        {
                            avgsteps++;
                            average += getBilinearInterpolate(x + n, y + l);
                        }
                    }

                    manipulate[x, y] = average / avgsteps;
                }
            }
            map = manipulate;
        }

        public void pertubation(double amount)
        {
            // Simple pertubation filter
            double[,] manipulated = new double[w, h];
            Random generator = new Random(seed); // Seeds FTW!
            //double amount = 8.0;

            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    double offset_x = (double)x + (generator.NextDouble() * amount) - (amount / 2.0);
                    double offset_y = (double)y + (generator.NextDouble() * amount) - (amount / 2.0);
                    double p = getBilinearInterpolate(offset_x, offset_y);
                    manipulated[x, y] = p;
                }
            }
            map = manipulated;
        }

        public void pertubationMask(Channel mask)
        {
            // Simple pertubation filter
            double[,] manipulated = new double[w, h];
            Random generator = new Random(seed); // Seeds FTW!
            //double amount = 8.0;

            double amount;

            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    amount = mask.map[x, y];
                    double offset_x = (double)x + (generator.NextDouble() * amount) - (amount / 2.0);
                    double offset_y = (double)y + (generator.NextDouble() * amount) - (amount / 2.0);

                    if (offset_x > w)
                        offset_x = w - 1;
                    if (offset_y > h)
                        offset_y = h - 1;
                    if (offset_y < 0)
                        offset_y = 0;
                    if (offset_x < 0)
                        offset_x = 0;

                    double p = getBilinearInterpolate(offset_x, offset_y);
                    manipulated[x, y] = p;
                }
            }
            map = manipulated;
        }

        public Channel blend(Channel other, double amount)
        {
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    map[x, y] = Tools.linearInterpolate(map[x,y],other.map[x,y],amount);
                }
            }
            return this;
        }

        public Channel blend(Channel other, Channel amount)
        {
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    map[x, y] = Tools.linearInterpolate(map[x, y], other.map[x, y], amount.map[x,y]);
                }
            }
            return this;
        }
    }
}
