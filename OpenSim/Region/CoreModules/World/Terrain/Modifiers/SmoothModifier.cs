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

namespace OpenSim.Region.CoreModules.World.Terrain.Modifiers
{
    public class SmoothModifier : TerrainModifier
    {
        public SmoothModifier(ITerrainModule module) : base(module)
        {
        }

        public override string ModifyTerrain(ITerrainChannel map, string[] args)
        {
            string result;
            if (args.Length < 3)
            {
                result = "Usage: " + GetUsage();
            }
            else
            {
                TerrainModifierData data;
                result = this.parseParameters(args, out data);

                // Context-specific validation
                if (result == String.Empty)
                {
                    if (data.bevel == "taper")
                    {
                        if (data.bevelevation < 0.01 || data.bevelevation > 0.99)
                        {
                            result = String.Format("Taper must be 0.01 to 0.99: {0}", data.bevelevation);
                        }
                    }
                    else
                    {
                        data.bevelevation = 2.0f / 3.0f;
                    }

                    if (data.elevation < 0.0 || data.elevation > 1.0)
                    {
                        result = String.Format("Smoothing strength must be 0.0 to 1.0: {0}", data.elevation);
                    }

                    if (data.shape == String.Empty)
                    {
                        data.shape = "rectangle";
                        data.x0 = 0;
                        data.y0 = 0;
                        data.dx = map.Width;
                        data.dy = map.Height;
                    }
                }

                // if it's all good, then do the work
                if (result == String.Empty)
                {
                    this.applyModification(map, data);
                }
            }

            return result;
        }

        public override string GetUsage()
        {
            string val = "smooth <strength> [ -rec=x1,y1,dx[,dy] | -ell=x0,y0,rx[,ry] ] [-taper=<fraction>]"
                                + "\nSmooths all points within the specified range using a simple averaging algorithm.";
            return val;
        }

        public override float operate(float[,] map, TerrainModifierData data, int x, int y)
        {
            float[] scale = new float[3];
            scale[0] = data.elevation;
            scale[1] = ((1.0f - scale[0]) * data.bevelevation) / 8.0f;
            scale[2] = ((1.0f - scale[0]) * (1.0f - data.bevelevation)) / 16.0f;
            int xMax = map.GetLength(0);
            int yMax = map.GetLength(1);
            float result;
            if ((x == 0) || (y == 0) || (x == (xMax - 1)) || (y == (yMax - 1)))
            {
                result = map[x, y];
            }
            else
            {
                result = 0.0f;
                for(int yPos = (y - 2); yPos < (y + 3); yPos++)
                {
                    int yVal = (yPos <= 0) ? 0 : ((yPos < yMax) ? yPos : yMax - 1);
                    for(int xPos = (x - 2); xPos < (x + 3); xPos++)
                    {
                        int xVal = (xPos <= 0) ? 0 : ((xPos < xMax) ? xPos : xMax - 1);
                        int dist = Math.Max(Math.Abs(x - xVal), Math.Abs(y - yVal));
                        result += map[xVal, yVal] * scale[dist];
                    }
                }
            }
            return result;
        }

    }

}

