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


using System;

namespace libTerrain
{
    public partial class Channel
    {
        public int GetWidth()
        {
            return w;
        }

        public int GetHeight()
        {
            return h;
        }

        public Channel Copy()
        {
            Channel x = new Channel(w, h);
            x.map = (double[,]) map.Clone();
            return x;
        }

        public void SetDiff()
        {
            SetDiff(1);
        }

        public void SetDiff(int val)
        {
            for (int x = 0; x < w/16; x++)
            {
                for (int y = 0; y < h/16; y++)
                {
                    diff[x, y] = val;
                }
            }
        }

        public void SetDiff(int x, int y)
        {
            diff[x/16, y/16]++;
        }

        public void Set(int x, int y, double val)
        {
            if (x >= w)
                throw new Exception("Bounds error while setting pixel (width)");
            if (y >= h)
                throw new Exception("Bounds error while setting pixel (height)");
            if (x < 0)
                throw new Exception("Bounds error while setting pixel (width)");
            if (y < 0)
                throw new Exception("Bounds error while setting pixel (height)");

            if (map[x, y] != val)
            {
                SetDiff(x, y);

                map[x, y] = val;
            }
        }

        public void SetClip(int x, int y, double val)
        {
            SetDiff(x, y);

            if (x >= w)
                throw new Exception("Bounds error while setting pixel (width)");
            if (y >= h)
                throw new Exception("Bounds error while setting pixel (height)");
            if (x < 0)
                throw new Exception("Bounds error while setting pixel (width)");
            if (y < 0)
                throw new Exception("Bounds error while setting pixel (height)");

            if (val > 1.0)
                val = 1.0;
            if (val < 0.0)
                val = 0.0;

            map[x, y] = val;
        }

        private double GetBilinearInterpolate(double x, double y)
        {
            if (x > w - 2.0)
                x = w - 2.0;
            if (y > h - 2.0)
                y = h - 2.0;
            if (x < 0.0)
                x = 0.0;
            if (y < 0.0)
                y = 0.0;

            int stepSize = 1;
            double h00 = Get((int) x, (int) y);
            double h10 = Get((int) x + stepSize, (int) y);
            double h01 = Get((int) x, (int) y + stepSize);
            double h11 = Get((int) x + stepSize, (int) y + stepSize);
            double h1 = h00;
            double h2 = h10;
            double h3 = h01;
            double h4 = h11;
            double a00 = h1;
            double a10 = h2 - h1;
            double a01 = h3 - h1;
            double a11 = h1 - h2 - h3 + h4;
            double partialx = x - (int) x;
            double partialz = y - (int) y;
            double hi = a00 + (a10*partialx) + (a01*partialz) + (a11*partialx*partialz);
            return hi;
        }

        public double Get(int x, int y)
        {
            try
            {
                return map[x, y];
            }
            catch (IndexOutOfRangeException)
            {
                if (x >= w)
                    x = w - 1;
                if (y >= h)
                    y = h - 1;
                if (x < 0)
                    x = 0;
                if (y < 0)
                    y = 0;
                return map[x, y];
            }
        }

        public void SetWrap(int x, int y, double val)
        {
            SetDiff(x, y);

            map[x%w, y%h] = val;
        }

        public void SetWrapClip(int x, int y, double val)
        {
            SetDiff(x, y);

            if (val > 1.0)
                val = 1.0;
            if (val < 0.0)
                val = 0.0;

            map[x%w, y%h] = val;
        }

        public void Fill(double val)
        {
            SetDiff();

            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    map[x, y] = val;
                }
            }
        }

        public void Fill(double min, double max, double val)
        {
            SetDiff();

            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    if (map[x, y] >= min && map[x, y] <= max)
                        map[x, y] = val;
                }
            }
        }

        public double FindMax()
        {
            int x, y;
            double max = double.MinValue;

            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    if (map[x, y] > max)
                        max = map[x, y];
                }
            }

            return max;
        }

        public double FindMin()
        {
            int x, y;
            double min = double.MaxValue;

            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    if (map[x, y] < min)
                        min = map[x, y];
                }
            }

            return min;
        }

        public double Sum()
        {
            int x, y;
            double sum = 0.0;

            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    sum += map[x, y];
                }
            }

            return sum;
        }

        public double Avg()
        {
            return Sum()/(w*h);
        }

        public bool ContainsNaN()
        {
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    double elm = map[x, y];

                    if (Double.IsNaN(elm))
                        return true;
                }
            }
            return false;
        }
    }
}
