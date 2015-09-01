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
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.Framework.Scenes
{
    public class Border
    {       
        /// <summary>
        /// Line perpendicular to the Direction Cardinal.  Z value is the 
        /// </summary>
        public Vector3 BorderLine = Vector3.Zero;
        
        /// <summary>
        /// Direction cardinal of the border, think, 'which side of the region this is'.  EX South border: Cardinal.S
        /// </summary>
        public Cardinals CrossDirection = Cardinals.N;
        public uint TriggerRegionX = 0;
        public uint TriggerRegionY = 0;

        public Border()
        {
        }

        /// <summary>
        /// Creates a Border.  The line is perpendicular to the direction cardinal. 
        /// IE: if the direction cardinal is South, the line is West->East
        /// </summary>
        /// <param name="lineStart">The starting point for the line of the border.
        /// The position of an object must be greater then this for this border to trigger.
        /// Perpendicular to the direction cardinal</param>
        /// <param name="lineEnd">The ending point for the line of the border.
        /// The position of an object must be less then this for this border to trigger.
        /// Perpendicular to the direction cardinal</param>
        /// <param name="triggerCoordinate">The position that triggers border the border 
        /// cross parallel to the direction cardinal.  On the North cardinal, this 
        /// normally 256.  On the South cardinal, it's normally 0.  Any position past this 
        /// point on the cartesian coordinate will trigger the border cross as long as it 
        /// falls within the line start and the line end.</param>
        /// <param name="triggerRegionX">When this border triggers, teleport to this regionX 
        /// in the grid</param>
        /// <param name="triggerRegionY">When this border triggers, teleport to this regionY 
        /// in the grid</param>
        /// <param name="direction">Cardinal for border direction.  Think, 'which side of the 
        /// region is this'</param>
        public Border(float lineStart, float lineEnd, float triggerCoordinate, uint triggerRegionX, 
            uint triggerRegionY, Cardinals direction)
        {
            BorderLine = new Vector3(lineStart,lineEnd,triggerCoordinate);
            CrossDirection = direction;
            TriggerRegionX = triggerRegionX;
            TriggerRegionY = triggerRegionY;
        }

        /// <summary>
        /// Tests to see if the given position would cross this border.
        /// </summary>
        /// <returns></returns>
        public bool TestCross(Vector3 position)
        {
            bool result = false;
            switch (CrossDirection)
            {
                case Cardinals.N:  // x+0, y+1
                    if (position.X >= BorderLine.X && position.X <= BorderLine.Y && position.Y > BorderLine.Z)
                    {
                        return true;
                    }
                    break;
                case Cardinals.NE: // x+1, y+1
                    break;
                case Cardinals.E:  // x+1, y+0
                    if (position.Y >= BorderLine.X && position.Y <= BorderLine.Y && position.X > BorderLine.Z)
                    {
                        return true;
                    }
                    break;
                case Cardinals.SE: // x+1, y-1
                    break;
                case Cardinals.S:  // x+0, y-1
                    if (position.X >= BorderLine.X && position.X <= BorderLine.Y && position.Y < BorderLine.Z)
                    {
                        return true;
                    }
                    break;
                case Cardinals.SW: // x-1, y-1
                    break;
                case Cardinals.W:  // x-1, y+0
                    if (position.Y >= BorderLine.X && position.Y <= BorderLine.Y && position.X < BorderLine.Z)
                    {
                        return true;
                    }
                    break; 
                case Cardinals.NW: // x-1, y+1
                    break;
            }

            return result;
        }

        public float Extent
        {
            get
            {
                switch (CrossDirection)
                {
                    case Cardinals.N:
                        break;
                    case Cardinals.S:
                        break;
                    case Cardinals.W:
                        break;
                    case Cardinals.E:
                        break;
                }
                return 0;
            }
        }
    }
}
