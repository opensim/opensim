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
    partial class Channel
    {
        public void HydraulicErosion(Channel rain, double evaporation, double solubility, int frequency, int rounds)
        {
            SetDiff();

            Channel water = new Channel(w, h);
            Channel sediment = new Channel(w, h);
            Channel terrain = this;
            Channel waterFlow = new Channel(w, h);

            NeighbourSystem type = NeighbourSystem.Moore;
            int NEIGHBOUR_ME = 4;

            int NEIGHBOUR_MAX = type == NeighbourSystem.Moore ? 9 : 5;

            for (int i = 0; i < rounds; i++)
            {
                water += rain;

                sediment = terrain*water;
                terrain -= sediment;

                for (int x = 1; x < w - 1; x++)
                {
                    for (int y = 1; y < h - 1; y++)
                    {
                        double[] heights = new double[NEIGHBOUR_MAX];
                        double[] diffs = new double[NEIGHBOUR_MAX];

                        double heightCenter = map[x, y];

                        for (int j = 0; j < NEIGHBOUR_MAX; j++)
                        {
                            if (j != NEIGHBOUR_ME)
                            {
                                int[] coords = Neighbours(type, j);
                                coords[0] += x;
                                coords[1] += y;

                                heights[j] = map[coords[0], coords[1]] + water.map[coords[0], coords[1]] +
                                             sediment.map[coords[0], coords[1]];
                                diffs[j] = heightCenter - heights[j];
                            }
                        }

                        double totalHeight = 0;
                        double totalHeightDiff = 0;
                        int totalCellsCounted = 1;

                        for (int j = 0; j < NEIGHBOUR_MAX; j++)
                        {
                            if (j != NEIGHBOUR_ME)
                            {
                                if (diffs[j] > 0)
                                {
                                    totalHeight += heights[j];
                                    totalHeightDiff += diffs[j];
                                    totalCellsCounted++;
                                }
                            }
                        }

                        if (totalCellsCounted == 1)
                            continue;

                        double averageHeight = totalHeight/totalCellsCounted;
                        double waterAmount = Math.Min(water.map[x, y], heightCenter - averageHeight);

                        // TODO: Check this.
                        waterFlow.map[x, y] += waterFlow.map[x, y] - waterAmount;

                        double totalInverseDiff = waterAmount/totalHeightDiff;

                        for (int j = 0; j < NEIGHBOUR_MAX; j++)
                        {
                            if (j != NEIGHBOUR_ME)
                            {
                                int[] coords = Neighbours(type, j);
                                coords[0] += x;
                                coords[1] += y;

                                if (diffs[j] > 0)
                                {
                                    waterFlow.SetWrap(coords[0], coords[1],
                                                      waterFlow.map[coords[0], coords[1]] + diffs[j]*totalInverseDiff);
                                }
                            }
                        }
                    }
                }

                water += waterFlow;
                waterFlow.Fill(0);

                water *= evaporation;

                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        double deposition = sediment.map[x, y] - water.map[x, y]*solubility;
                        if (deposition > 0)
                        {
                            sediment.map[x, y] -= deposition;
                            terrain.map[x, y] += deposition;
                        }
                    }
                }
            }
        }
    }
}