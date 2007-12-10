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
        /// <summary>
        /// Generates 'number' of worms which navigate randomly around the landscape creating terrain as they go.
        /// </summary>
        /// <param name="number">The number of worms which will traverse the map</param>
        /// <param name="rounds">The number of steps each worm will traverse</param>
        /// <param name="movement">The maximum distance each worm will move each step</param>
        /// <param name="size">The size of the area around the worm modified</param>
        /// <param name="centerspawn">Do worms start in the middle, or randomly?</param>
        public void Worms(int number, int rounds, double movement, double size, bool centerspawn)
        {
            SetDiff();

            Random random = new Random(seed);
            int i, j;

            for (i = 0; i < number; i++)
            {
                double rx, ry;
                if (centerspawn)
                {
                    rx = w/2.0;
                    ry = h/2.0;
                }
                else
                {
                    rx = random.NextDouble()*(w - 1);
                    ry = random.NextDouble()*(h - 1);
                }
                for (j = 0; j < rounds; j++)
                {
                    rx += (random.NextDouble()*movement) - (movement/2.0);
                    ry += (random.NextDouble()*movement) - (movement/2.0);
                    Raise(rx, ry, size, 1.0);
                }
            }
        }
    }
}
