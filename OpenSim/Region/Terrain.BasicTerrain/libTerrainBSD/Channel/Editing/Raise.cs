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
        /// Raises land around the selection
        /// </summary>
        /// <param name="rx">The center the X coordinate of where you wish to raise the land</param>
        /// <param name="ry">The center the Y coordinate of where you wish to raise the land</param>
        /// <param name="size">The radius of the dimple</param>
        /// <param name="amount">How much impact to add to the terrain (0..2 usually)</param>
        public void Raise(double rx, double ry, double size, double amount)
        {
            RaiseSphere(rx, ry, size, amount);
        }

        /// <summary>
        /// Raises land in a sphere around the selection
        /// </summary>
        /// <param name="rx">The center the X coordinate of where you wish to raise the land</param>
        /// <param name="ry">The center the Y coordinate of where you wish to raise the land</param>
        /// <param name="size">The radius of the sphere dimple</param>
        /// <param name="amount">How much impact to add to the terrain (0..2 usually)</param>
        public void RaiseSphere(double rx, double ry, double size, double amount)
        {
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    double z = size;
                    z *= z;
                    z -= ((x - rx) * (x - rx)) + ((y - ry) * (y - ry));

                    if (z < 0)
                        z = 0;

                    Set(x, y, map[x, y] + (z * amount));
                }
            }
        }

        /// <summary>
        /// Raises land in a cone around the selection
        /// </summary>
        /// <param name="rx">The center the X coordinate of where you wish to raise the land</param>
        /// <param name="ry">The center the Y coordinate of where you wish to raise the land</param>
        /// <param name="size">The radius of the cone</param>
        /// <param name="amount">How much impact to add to the terrain (0..2 usually)</param>
        public void RaiseCone(double rx, double ry, double size, double amount)
        {
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    double z = size;
                    z -= Math.Sqrt(((x - rx) * (x - rx)) + ((y - ry) * (y - ry)));

                    if (z < 0)
                        z = 0;

                    Set(x, y, map[x, y] + (z * amount));
                }
            }
        }

        /// <summary>
        /// Lowers land in a sphere around the selection
        /// </summary>
        /// <param name="rx">The center the X coordinate of where you wish to lower the land</param>
        /// <param name="ry">The center the Y coordinate of where you wish to lower the land</param>
        /// <param name="size">The radius of the sphere dimple</param>
        /// <param name="amount">How much impact to remove from the terrain (0..2 usually)</param>
        public void Lower(double rx, double ry, double size, double amount)
        {
            LowerSphere(rx, ry, size, amount);
        }

        /// <summary>
        /// Lowers land in a sphere around the selection
        /// </summary>
        /// <param name="rx">The center the X coordinate of where you wish to lower the land</param>
        /// <param name="ry">The center the Y coordinate of where you wish to lower the land</param>
        /// <param name="size">The radius of the sphere dimple</param>
        /// <param name="amount">How much impact to remove from the terrain (0..2 usually)</param>
        public void LowerSphere(double rx, double ry, double size, double amount)
        {
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    double z = size;
                    z *= z;
                    z -= ((x - rx) * (x - rx)) + ((y - ry) * (y - ry));

                    if (z < 0)
                        z = 0;

                    Set(x, y, map[x, y] - (z * amount));
                }
            }
        }
    }
}