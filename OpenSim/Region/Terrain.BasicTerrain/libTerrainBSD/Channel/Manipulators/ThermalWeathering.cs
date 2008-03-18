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

namespace libTerrain
{
    partial class Channel
    {
        /// <summary>
        /// A thermal weathering implementation based on Musgrave's original 1989 algorithm. This is Adam's custom implementation which may differ slightly from the original.
        /// </summary>
        /// <param name="talus">The rock angle (represented as a dy/dx ratio) at which point it will be succeptible to breakage</param>
        /// <param name="rounds">The number of erosion rounds</param>
        /// <param name="c">The amount of rock to carry each round</param>
        public Channel ThermalWeathering(double talus, int rounds, double c)
        {
            SetDiff();

            double[,] lastFrame;
            double[,] thisFrame;

            lastFrame = (double[,]) map.Clone();
            thisFrame = (double[,]) map.Clone();

            NeighbourSystem type = NeighbourSystem.Moore;
            // Using moore neighbourhood (twice as computationally expensive)
            int NEIGHBOUR_ME = 4; // I am always 4 in both systems.

            int NEIGHBOUR_MAX = type == NeighbourSystem.Moore ? 9 : 5;

            int frames = rounds; // Number of thermal erosion iterations to run
            int i, j;
            int x, y;

            for (i = 0; i < frames; i++)
            {
                for (x = 0; x < w; x++)
                {
                    for (y = 0; y < h; y++)
                    {
                        for (j = 0; j < NEIGHBOUR_MAX; j++)
                        {
                            if (j != NEIGHBOUR_ME)
                            {
                                int[] coords = Neighbours(type, j);

                                coords[0] += x;
                                coords[1] += y;

                                if (coords[0] > w - 1)
                                    coords[0] = w - 1;
                                if (coords[1] > h - 1)
                                    coords[1] = h - 1;
                                if (coords[0] < 0)
                                    coords[0] = 0;
                                if (coords[1] < 0)
                                    coords[1] = 0;

                                double heightF = thisFrame[x, y];
                                double target = thisFrame[coords[0], coords[1]];

                                if (target > heightF + talus)
                                {
                                    double calc = c*((target - heightF) - talus);
                                    heightF += calc;
                                    target -= calc;
                                }

                                thisFrame[x, y] = heightF;
                                thisFrame[coords[0], coords[1]] = target;
                            }
                        }
                    }
                }
                lastFrame = (double[,]) thisFrame.Clone();
            }

            map = thisFrame;

            Normalise(); // Just to guaruntee a smooth 0..1 value
            return this;
        }
    }
}
