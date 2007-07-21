/*
Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.

    * Redistributions in binary form must reproduce the above
      copyright notice, this list of conditions and the following
      disclaimer in the documentation and/or other materials provided
      with the distribution.

    * Neither the name of libTerrain nor the names of
      its contributors may be used to endorse or promote products
      derived from this software without specific prior written
      permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace libTerrain
{
    partial class Channel
    {
        /// <summary>
        /// Generates a Voronoi diagram (sort of a stained glass effect) which will fill the entire channel
        /// </summary>
        /// <remarks>3-Clause BSD Licensed</remarks>
        /// <param name="pointsPerBlock">The number of generator points in each block</param>
        /// <param name="blockSize">A multiple of the channel width and height which will have voronoi points generated in it.
        /// <para>This is to ensure a more even distribution of the points than pure random allocation.</para></param>
        /// <param name="c">The Voronoi diagram type. Usually an array with values consisting of [-1,1]. Experiment with the chain, you can have as many values as you like.</param>
        public void voronoiDiagram(int pointsPerBlock, int blockSize, double[] c)
        {
            List<Point2D> points = new List<Point2D>();
            Random generator = new Random(seed);

            // Generate the emitter points
            int x, y, i;
            for (x = -blockSize; x < w + blockSize; x += blockSize)
            {
                for (y = -blockSize; y < h + blockSize; y += blockSize)
                {
                    for (i = 0; i < pointsPerBlock; i++)
                    {
                        double pX = x + (generator.NextDouble() * (double)blockSize);
                        double pY = y + (generator.NextDouble() * (double)blockSize);

                        points.Add(new Point2D(pX, pY));
                    }
                }
            }

            double[] distances = new double[points.Count];

            // Calculate the distance each pixel is from an emitter
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    for (i = 0; i < points.Count; i++)
                    {
                        double dx, dy;
                        dx = Math.Abs((double)x - points[i].x);
                        dy = Math.Abs((double)y - points[i].y);

                        distances[i] = (dx * dx + dy * dy);
                    }

                    Array.Sort(distances);

                    double f = 0.0;

                    // Multiply the distances with their 'c' counterpart
                    // ordering the distances descending
                    for (i = 0; i < c.Length; i++)
                    {
                        if (i >= points.Count)
                            break;

                        f += c[i] * distances[i];
                    }

                    map[x, y] = f;
                }
            }

            // Normalise the result
            normalise();
        }

        public void voronoiDiagram(List<Point2D> points, double[] c)
        {
            
            Random generator = new Random(seed);
            int x, y, i;
            double[] distances = new double[points.Count];

            // Calculate the distance each pixel is from an emitter
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    for (i = 0; i < points.Count; i++)
                    {
                        double dx, dy;
                        dx = Math.Abs((double)x - points[i].x);
                        dy = Math.Abs((double)y - points[i].y);

                        distances[i] = (dx * dx + dy * dy);
                    }

                    Array.Sort(distances);

                    double f = 0.0;

                    // Multiply the distances with their 'c' counterpart
                    // ordering the distances descending
                    for (i = 0; i < c.Length; i++)
                    {
                        if (i >= points.Count)
                            break;

                        f += c[i] * distances[i];
                    }

                    map[x, y] = f;
                }
            }

            // Normalise the result
            normalise();
        }

        public void voroflatDiagram(int pointsPerBlock, int blockSize)
        {
            List<Point2D> points = new List<Point2D>();
            Random generator = new Random(seed);

            // Generate the emitter points
            int x, y, i;
            for (x = -blockSize; x < w + blockSize; x += blockSize)
            {
                for (y = -blockSize; y < h + blockSize; y += blockSize)
                {
                    for (i = 0; i < pointsPerBlock; i++)
                    {
                        double pX = x + (generator.NextDouble() * (double)blockSize);
                        double pY = y + (generator.NextDouble() * (double)blockSize);

                        points.Add(new Point2D(pX, pY));
                    }
                }
            }

            double[] distances = new double[points.Count];

            // Calculate the distance each pixel is from an emitter
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    for (i = 0; i < points.Count; i++)
                    {
                        double dx, dy;
                        dx = Math.Abs((double)x - points[i].x);
                        dy = Math.Abs((double)y - points[i].y);

                        distances[i] = (dx * dx + dy * dy);
                    }

                    //Array.Sort(distances);

                    double f = 0.0;

                    double min = double.MaxValue;
                    for (int j = 0; j < distances.Length;j++ )
                    {
                        if (distances[j] < min)
                        {
                            min = distances[j];
                            f = j;
                        }
                    }

                    // Multiply the distances with their 'c' counterpart
                    // ordering the distances descending

                    map[x, y] = f;
                }
            }

            // Normalise the result
            normalise();
        }
    }
}
