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
 *     * Neither the name of the OpenSimulator Project nor the
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
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.CoreModules.World.Terrain.Features
{
    public class RectangleFeature : TerrainFeature
    {
        public RectangleFeature(ITerrainModule module) : base(module)
        {
        }

        public override string CreateFeature(ITerrainChannel map, string[] args)
        {
            string val;
            string result;
            if (args.Length < 7)
            {
                result = "Usage: " + GetUsage();
            }
            else
            {
                result = String.Empty;

                float targetElevation;
                val = base.parseFloat(args[3], out targetElevation);
                if (val != String.Empty)
                {
                    result = val;
                }

                int xOrigin;
                val = base.parseInt(args[4], out xOrigin);
                if (val != String.Empty)
                {
                    result = val;
                }
                else if (xOrigin < 0 || xOrigin >= map.Width)
                {
                    result = "x-origin must be within the region";
                }

                int yOrigin;
                val = base.parseInt(args[5], out yOrigin);
                if (val != String.Empty)
                {
                    result = val;
                }
                else if (yOrigin < 0 || yOrigin >= map.Height)
                {
                    result = "y-origin must be within the region";
                }

                int xDelta;
                val = base.parseInt(args[6], out xDelta);
                if (val != String.Empty)
                {
                    result = val;
                }
                else if (xDelta <= 0)
                {
                    result = "x-size must be greater than zero";
                }

                int yDelta;
                if (args.Length > 7)
                {
                    val = base.parseInt(args[7], out yDelta);
                    if (val != String.Empty)
                    {
                        result = val;
                    }
                    else if (yDelta <= 0)
                    {
                        result = "y-size must be greater than zero";
                    }
                }
                else
                {
                    // no y-size.. make it square
                    yDelta = xDelta;
                }

                // slightly more complex validation, if required.
                if (result == String.Empty)
                {
                    if (xOrigin + xDelta > map.Width)
                    {
                        result = "(x-origin + x-size) must be within the region size";
                    }
                    else if (yOrigin + yDelta > map.Height)
                    {
                        result = "(y-origin + y-size) must be within the region size";
                    }
                }

                // if it's all good, then do the work
                if (result == String.Empty)
                {
                    int yPos = yOrigin + yDelta;
                    while(--yPos >= yOrigin)
                    {
                        int xPos = xOrigin + xDelta;
                        while(--xPos >= xOrigin)
                        {
                            map[xPos, yPos] = (double)targetElevation;
                        }
                    }
                }
            }

            return result;
        }

        public override string GetUsage()
        {
            return "rectangle <height> <x-origin> <y-origin> <x-size> [<y-size>]";
        }
    }

}

