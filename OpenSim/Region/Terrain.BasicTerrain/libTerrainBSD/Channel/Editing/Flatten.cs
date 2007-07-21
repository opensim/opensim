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
        /// Flattens the area underneath rx,ry by moving it to the average of the area. Uses a spherical mask provided by the raise() function.
        /// </summary>
        /// <param name="rx">The X coordinate of the terrain mask</param>
        /// <param name="ry">The Y coordinate of the terrain mask</param>
        /// <param name="size">The size of the terrain mask</param>
        /// <param name="amount">The scale of the terrain mask</param>
        public void flatten(double rx, double ry, double size, double amount)
        {
            // Generate the mask
            Channel temp = new Channel(w, h);
            temp.fill(0);
            temp.raise(rx, ry, size, amount);
            temp.normalise();
            double total_mod = temp.sum();

            // Establish the average height under the area
            Channel newmap = new Channel(w, h);
            newmap.map = (double[,])map.Clone();

            newmap *= temp;

            double total_terrain = newmap.sum();
            double avg_height = total_terrain / total_mod;

            // Create a flat terrain using the average height
            Channel flat = new Channel(w, h);
            flat.fill(avg_height);

            // Blend the current terrain with the average height terrain
            // using the "raised" empty terrain as a mask
            blend(flat, temp);

        }

        public void flatten(Channel mask, double amount)
        {
            // Generate the mask
            Channel temp = mask * amount;
            temp.clip(0, 1); // Cut off out-of-bounds values

            double total_mod = temp.sum();

            // Establish the average height under the area
            Channel map = new Channel(w, h);
            map.map = (double[,])this.map.Clone();

            map *= temp;

            double total_terrain = map.sum();
            double avg_height = total_terrain / total_mod;

            // Create a flat terrain using the average height
            Channel flat = new Channel(w, h);
            flat.fill(avg_height);

            // Blend the current terrain with the average height terrain
            // using the "raised" empty terrain as a mask
            blend(flat, temp);
        }
    }
}