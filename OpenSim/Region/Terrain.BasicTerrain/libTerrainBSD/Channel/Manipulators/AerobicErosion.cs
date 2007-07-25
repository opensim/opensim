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
        // Ideas for Aerobic erosion
        //
        // Unlike thermal (gravity) and hydraulic (water suspension)
        // aerobic erosion should displace mass by moving sediment
        // in "hops". The length of the hop being dictated by the
        // presence of sharp cliffs and wind speed.

        // The ability to pickup sediment is defined by the total
        // surface area, such that:
        //                      0 0 0
        //                      0 1 0
        //                      0 0 0
        // Would be the best possible value for sediment to be
        // picked up (total difference = 8) and flatter land
        // will erode less quickly.

        // Suspended particles assist the erosion process by hitting
        // the surface and chiselling additional particles off faster
        // than alone.

        // Particles are deposited when one of two conditions is met
        // First:
        //          When particles hit a wall - such that the
        //          wind direction points at a difference >= the
        //          deposition mininum talus.
        // Second:
        //          When wind speed is lowered to below the minimum
        //          required for transit. An idea for this is to
        //          use the navier-stokes algorithms for simulating
        //          pressure across the terrain.

        /// <summary>
        /// An experimental erosion algorithm developed by Adam. Moves sediment by factoring the surface area of each height point.
        /// </summary>
        /// <param name="windspeed">0..1 The speed of the wind</param>
        /// <param name="pickup_talus_minimum">The minimum angle at which rock is eroded 0..1 (recommended: <= 0.30)</param>
        /// <param name="drop_talus_minimum">The minimum angle at which rock is dropped 0..1 (recommended: >= 0.00)</param>
        /// <param name="carry">The percentage of rock which can be picked up to pickup 0..1</param>
        /// <param name="rounds">The number of erosion rounds (recommended: 25+)</param>
        /// <param name="lowest">Drop sediment at the lowest point?</param>
        public void AerobicErosion(double windspeed, double pickupTalusMinimum, double dropTalusMinimum, double carry, int rounds, bool lowest, bool usingFluidDynamics)
        {
            bool debugImages = false;

            Channel wind = new Channel(w, h) ;
            Channel sediment = new Channel(w, h);
            int x, y, i, j;

            this.Normalise();

            wind = this.Copy();
            wind.Noise();

            if (debugImages)
                wind.SaveImage("testimg/wind_start.png");

            if (usingFluidDynamics)
            {
                wind.navierStokes(20, 0.1, 0.0, 0.0);
            }
            else
            {
                wind.Pertubation(30);
            }

            if (debugImages)
                wind.SaveImage("testimg/wind_begin.png");

            for (i = 0; i < rounds; i++)
            {
                // Convert some rocks to sand
                for (x = 1; x < w - 1; x++)
                {
                    for (y = 1; y < h - 1; y++)
                    {
                        double me = Get(x, y);
                        double surfacearea = 0.3; // Everything will erode even if it's flat. Just slower.

                        for (j = 0; j < 9; j++)
                        {
                            int[] coords = Neighbours(NeighbourSystem.Moore, j);
                            double target = Get(x + coords[0], y + coords[1]);

                            surfacearea += Math.Abs(target - me);
                        }

                        double amount = surfacearea * wind.map[x, y] * carry;

                        if (amount < 0)
                            amount = 0;

                        if (surfacearea > pickupTalusMinimum)
                        {
                            Set(x, y, map[x, y] - amount);
                            sediment.map[x, y] += amount;
                        }
                    }
                }

                if (usingFluidDynamics)
                {
                    sediment.navierStokes(7, 0.1, 0.0, 0.1);

                    Channel noiseChan = new Channel(w, h);
                    noiseChan.Noise();
                    wind.Blend(noiseChan, 0.01);

                    wind.navierStokes(10, 0.1, 0.01, 0.01);

                    sediment.Distort(wind, windspeed);
                }
                else
                {
                    wind.Pertubation(15);   // Can do better later
                    wind.seed++;
                    sediment.Pertubation(10); // Sediment is blown around a bit
                    sediment.seed++;
                }

                if (debugImages)
                    wind.SaveImage("testimg/wind_" + i.ToString() + ".png");

                // Convert some sand to rock
                for (x = 1; x < w - 1; x++)
                {
                    for (y = 1; y < h - 1; y++)
                    {
                        double me = Get(x, y);
                        double surfacearea = 0.01; // Flat land does not get deposition
                        double min = double.MaxValue;
                        int[] minside = new int[2];

                        for (j = 0; j < 9; j++)
                        {
                            int[] coords = Neighbours(NeighbourSystem.Moore, j);
                            double target = Get(x + coords[0], y + coords[1]);

                            surfacearea += Math.Abs(target - me);

                            if (target < min && lowest)
                            {
                                minside = (int[])coords.Clone();
                                min = target;
                            }
                        }

                        double amount = surfacearea * (1.0 - wind.map[x, y]) * carry;

                        if (amount < 0)
                            amount = 0;

                        if (surfacearea > dropTalusMinimum)
                        {
                            Set(x + minside[0], y + minside[1], map[x + minside[0], y + minside[1]] + amount);
                            sediment.map[x, y] -= amount;
                        }
                    }
                }

                if (debugImages)
                    sediment.SaveImage("testimg/sediment_" + i.ToString() + ".png");

                wind.Normalise();
                wind *= windspeed;

                this.Normalise();
            }

            Channel myself = this;
            myself += sediment;
            myself.Normalise();

            if (debugImages)
                this.SaveImage("testimg/output.png");
        }
    }
}