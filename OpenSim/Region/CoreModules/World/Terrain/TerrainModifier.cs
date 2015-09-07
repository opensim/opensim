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
using System.Reflection;
using log4net;

using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.CoreModules.World.Terrain
{
    public abstract class TerrainModifier : ITerrainModifier
    {
        protected ITerrainModule m_module;
        protected static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected TerrainModifier(ITerrainModule module)
        {
            m_module = module;
        }

        public abstract string ModifyTerrain(ITerrainChannel map, string[] args);

        public abstract string GetUsage();

        public abstract double operate(double[,] map, TerrainModifierData data, int x, int y);

        protected String parseParameters(string[] args, out TerrainModifierData data)
        {
            string val;
            string arg;
            string result;
            data = new TerrainModifierData();
            data.shape = String.Empty;
            data.bevel = String.Empty;
            data.dx = 0;
            data.dy = 0;
            if (args.Length < 4)
            {
                result = "Usage: " + GetUsage();
            }
            else
            {
                result = this.parseFloat(args[3], out data.elevation);
            }
            if (result == String.Empty)
            {
                int index = 3;
                while(++index < args.Length && result == String.Empty)
                {
                    arg = args[index];
                    // check for shape
                    if (arg.StartsWith("-rec=") || arg.StartsWith("-ell="))
                    {
                        if (data.shape != String.Empty)
                        {
                            result = "Only 1 '-rec' or '-ell' parameter is permitted.";
                        }
                        else
                        {
                            data.shape = arg.StartsWith("-ell=") ? "ellipse" : "rectangle";
                            val = arg.Substring(arg.IndexOf("=") + 1);
                            string[] coords = val.Split(new char[] {','});
                            if ((coords.Length < 3) || (coords.Length > 4))
                            {
                                result = String.Format("Bad format for shape parameter {0}", arg);
                            }
                            else
                            {
                                result = this.parseInt(coords[0], out data.x0);
                                if (result == String.Empty)
                                {
                                    result = this.parseInt(coords[1], out data.y0);
                                }
                                if (result == String.Empty)
                                {
                                    result = this.parseInt(coords[2], out data.dx);
                                }
                                if (result == String.Empty)
                                {
                                    if (coords.Length == 4)
                                    {
                                        result = this.parseInt(coords[3], out data.dy);
                                    }
                                    else
                                    {
                                        data.dy = data.dx;
                                    }
                                }
                                if (result == String.Empty)
                                {
                                    if ((data.dx <= 0) || (data.dy <= 0))
                                    {
                                        result = "Shape sizes must be positive integers";
                                    }
                                }
                                else
                                {
                                    result = String.Format("Bad value in shape parameters {0}", arg);
                                }
                            }
                        }
                    }
                    else if (arg.StartsWith("-taper="))
                    {
                        if (data.bevel != String.Empty)
                        {
                            result = "Only 1 '-taper' parameter is permitted.";
                        }
                        else
                        {
                            data.bevel = "taper";
                            val = arg.Substring(arg.IndexOf("=") + 1);
                            result = this.parseFloat(val, out data.bevelevation);
                            if (result != String.Empty)
                            {
                                result = String.Format("Bad format for taper parameter {0}", arg);
                            }
                        }
                    }
                    else
                    {
                        result = String.Format("Unrecognized parameter {0}", arg);
                    }
                }
            }
            return result;
        }

        protected string parseFloat(String s, out float f)
        {
            string result;
            double d;
            if (Double.TryParse(s, out d))
            {
                try
                {
                    f = (float)d;
                    result = String.Empty;
                }
                catch(InvalidCastException)
                {
                    result = String.Format("{0} is invalid", s);
                    f = -1.0f;
                }
            }
            else
            {
                f = -1.0f;
                result = String.Format("{0} is invalid", s);
            }
            return result;
        }

        protected string parseInt(String s, out int i)
        {
            string result;
            if (Int32.TryParse(s, out i))
            {
                result = String.Empty;
            }
            else
            {
                result = String.Format("{0} is invalid", s);
            }
            return result;
        }

        protected void applyModification(ITerrainChannel map, TerrainModifierData data)
        {
            bool[,] mask;
            int xMax;
            int yMax;
            int xMid;
            int yMid;
            if (data.shape == "ellipse")
            {
                mask = this.ellipticalMask(data.dx, data.dy);
                xMax = mask.GetLength(0);
                yMax = mask.GetLength(1);
                xMid = xMax / 2 + xMax % 2;
                yMid = yMax / 2 + yMax % 2;
            }
            else
            {
                mask = this.rectangularMask(data.dx, data.dy);
                xMax = mask.GetLength(0);
                yMax = mask.GetLength(1);
                xMid = 0;
                yMid = 0;
            }
//            m_log.DebugFormat("Apply {0} mask {1}x{2} @ {3},{4}", data.shape, xMax, yMax, xMid, yMid);
            double[,] buffer = map.GetDoubles();
            int yDim = yMax;
            while(--yDim >= 0)
            {
                int yPos = data.y0 + yDim - yMid;
                if ((yPos >= 0) && (yPos < map.Height))
                {
                    int xDim = xMax;
                    while(--xDim >= 0)
                    {
                        int xPos = data.x0 + xDim - xMid;
                        if ((xPos >= 0) && (xPos < map.Width) && (mask[xDim, yDim]))
                        {
                            double endElevation = this.operate(buffer, data, xPos, yPos);
                            map[xPos, yPos] = endElevation;
                        }
                    }
                }
            }
        }

        protected double computeBevel(TerrainModifierData data, int x, int y)
        {
            int deltaX;
            int deltaY;
            int xMax;
            int yMax;
            double factor;
            if (data.bevel == "taper")
            {
                if (data.shape == "ellipse")
                {
                    deltaX = x - data.x0;
                    deltaY = y - data.y0;
                    xMax = data.dx;
                    yMax = data.dy;
                    factor = (double)((deltaX * deltaX) + (deltaY * deltaY));
                    factor /= ((xMax * xMax) + (yMax * yMax));
                }
                else
                {
                    // pyramid
                    xMax = data.dx / 2 + data.dx % 2;
                    yMax = data.dy / 2 + data.dy % 2;
                    deltaX = Math.Abs(data.x0 + xMax - x);
                    deltaY = Math.Abs(data.y0 + yMax - y);
                    factor = Math.Max(((double)(deltaY) / yMax), ((double)(deltaX) / xMax));
                }
            }
            else
            {
                factor = 0.0;
            }
            return factor;
        }

        private bool[,] rectangularMask(int xSize, int ySize)
        {
            bool[,] mask = new bool[xSize, ySize];
            int yPos = ySize;
            while(--yPos >= 0)
            {
                int xPos = xSize;
                while(--xPos >= 0)
                {
                    mask[xPos, yPos] = true;
                }
            }
            return mask;
        }

        /*
         * Fast ellipse-based derivative of Bresenham algorithm.
         *   https://web.archive.org/web/20120225095359/http://homepage.smc.edu/kennedy_john/belipse.pdf
         */
        private bool[,] ellipticalMask(int xRadius, int yRadius)
        {
            long twoASquared = 2L * xRadius * xRadius;
            long twoBSquared = 2L * yRadius * yRadius;

            bool[,] mask = new bool[2 * xRadius + 1, 2 * yRadius + 1];

            long ellipseError = 0L;
            long stoppingX = twoBSquared * xRadius;
            long stoppingY = 0L;
            long xChange = yRadius * yRadius * (1L - 2L * xRadius);
            long yChange = xRadius * xRadius;

            int xPos = xRadius;
            int yPos = 0;

            // first set of points
            while(stoppingX >= stoppingY)
            {
                int yUpper = yRadius + yPos;
                int yLower = yRadius - yPos;
                // fill in the mask
                int xNow = xPos;
                while(xNow >= 0)
                {
                    mask[xRadius + xNow, yUpper] = true;
                    mask[xRadius - xNow, yUpper] = true;
                    mask[xRadius + xNow, yLower] = true;
                    mask[xRadius - xNow, yLower] = true;
                    --xNow;
                }
                yPos++;
                stoppingY += twoASquared;
                ellipseError += yChange;
                yChange += twoASquared;
                if ((2L * ellipseError + xChange) > 0L)
                {
                    xPos--;
                    stoppingX -= twoBSquared;
                    ellipseError += xChange;
                    xChange += twoBSquared;
                }
            }

            // second set of points
            xPos = 0;
            yPos = yRadius;
            xChange = yRadius * yRadius;
            yChange = xRadius * xRadius * (1L - 2L * yRadius);

            ellipseError = 0L;
            stoppingX = 0L;
            stoppingY = twoASquared * yRadius;

            while(stoppingX <= stoppingY)
            {
                int xUpper = xRadius + xPos;
                int xLower = xRadius - xPos;
                // fill in the mask
                int yNow = yPos;
                while(yNow >= 0)
                {
                    mask[xUpper, yRadius + yNow] = true;
                    mask[xUpper, yRadius - yNow] = true;
                    mask[xLower, yRadius + yNow] = true;
                    mask[xLower, yRadius - yNow] = true;
                    --yNow;
                }
                xPos++;
                stoppingX += twoBSquared;
                ellipseError += xChange;
                xChange += twoBSquared;
                if ((2L * ellipseError + yChange) > 0L)
                {
                    yPos--;
                    stoppingY -= twoASquared;
                    ellipseError += yChange;
                    yChange += twoASquared;
                }
            }
            return mask;
        }
    }
}

