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
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.Physics.Meshing
{
    class Extruder
    {
        public float startParameter;
        public float stopParameter;
        public Manager.PhysicsVector size;

        public Mesh Extrude(Mesh m)
        {
            // Currently only works for iSteps=1;
            Mesh result = new Mesh();

            Mesh workingPlus  = m.Clone();
            Mesh workingMinus = m.Clone();

            foreach (Vertex v in workingPlus.vertices)
            {
                if (v == null)
                    continue;

                v.Z = +.5f;
                v.X *= size.X;
                v.Y *= size.Y;
                v.Z *= size.Z;
            }

            foreach (Vertex v in workingMinus.vertices)
            {
                if (v == null)
                    continue;

                v.Z = -.5f;
                v.X *= size.X;
                v.Y *= size.Y;
                v.Z *= size.Z;
            }

            foreach (Triangle t in workingMinus.triangles)
            {
                t.invertNormal();
            }

            result.Append(workingMinus);
            result.Append(workingPlus);

            int iLastNull = 0;
            for (int i = 0; i < workingPlus.vertices.Count; i++)
            {
                int iNext = (i + 1);
                
                if (workingPlus.vertices[i] == null) // Can't make a simplex here
                {
                    iLastNull = i+1;
                    continue;
                }

                if (i == workingPlus.vertices.Count-1) // End of list
                {
                    iNext = iLastNull;
                }

                if (workingPlus.vertices[iNext] == null) // Null means wrap to begin of last segment
                {
                    iNext = iLastNull;
                }

                Triangle tSide;
                tSide = new Triangle(workingPlus.vertices[i], workingMinus.vertices[i], workingPlus.vertices[iNext]);
                result.Add(tSide);

                tSide = new Triangle(workingPlus.vertices[iNext], workingMinus.vertices[i], workingMinus.vertices[iNext]);
                result.Add(tSide);
            }

            return result;
        }
    }
}
