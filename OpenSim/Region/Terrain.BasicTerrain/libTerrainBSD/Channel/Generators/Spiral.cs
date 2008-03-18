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
using System.Collections.Generic;

namespace libTerrain
{
    partial class Channel
    {
        private double[] CoordinatesToPolar(int x, int y)
        {
            double theta = Math.Atan2(x - (w/2), y - (h/2));
            double rx = (double) x - ((double) w/2);
            double ry = (double) y - ((double) h/2);
            double r = Math.Sqrt((rx*rx) + (ry*ry));

            double[] coords = new double[2];
            coords[0] = r;
            coords[1] = theta;
            return coords;
        }

        public int[] PolarToCoordinates(double r, double theta)
        {
            double nx;
            double ny;

            nx = (double) r*Math.Cos(theta);
            ny = (double) r*Math.Sin(theta);

            nx += w/2;
            ny += h/2;

            if (nx >= w)
                nx = w - 1;

            if (ny >= h)
                ny = h - 1;

            if (nx < 0)
                nx = 0;

            if (ny < 0)
                ny = 0;

            int[] coords = new int[2];
            coords[0] = (int) nx;
            coords[1] = (int) ny;
            return coords;
        }

        public void Polar()
        {
            SetDiff();

            Channel n = Copy();

            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    double[] coords = CoordinatesToPolar(x, y);

                    coords[0] += w/2.0;
                    coords[1] += h/2.0;

                    map[x, y] = n.map[(int) coords[0]%n.w, (int) coords[1]%n.h];
                }
            }
        }

        public void SpiralPlanter(int steps, double incAngle, double incRadius, double offsetRadius, double offsetAngle)
        {
            SetDiff();

            int i;
            double r = offsetRadius;
            double theta = offsetAngle;
            for (i = 0; i < steps; i++)
            {
                r += incRadius;
                theta += incAngle;

                int[] coords = PolarToCoordinates(r, theta);
                Raise(coords[0], coords[1], 20, 1);
            }
        }

        public void SpiralCells(int steps, double incAngle, double incRadius, double offsetRadius, double offsetAngle,
                                double[] c)
        {
            SetDiff();

            List<Point2D> points = new List<Point2D>();

            int i;
            double r = offsetRadius;
            double theta = offsetAngle;
            for (i = 0; i < steps; i++)
            {
                r += incRadius;
                theta += incAngle;

                int[] coords = PolarToCoordinates(r, theta);
                points.Add(new Point2D(coords[0], coords[1]));
            }

            VoronoiDiagram(points, c);
        }

        public void Spiral(double wid, double hig, double offset)
        {
            SetDiff();

            int x, y, z;
            z = 0;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    z++;
                    double dx = Math.Abs((w/2) - x);
                    double dy = Math.Abs((h/2) - y);
                    map[x, y] += Math.Sin(dx/wid) + Math.Cos(dy/hig);
                }
            }
            Normalise();
        }
    }
}
