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
 */

using System;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.Meshing
{
    public struct vertex
    {
        public float X;
        public float Y;
        public float Z;

        public vertex(float x, float y, float z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }
    }

    public struct face
    {
        public int v1;
        public int v2;
        public int v3;

        public face(int v1, int v2, int v3)
        {
            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;
        }
    }

    internal struct Angle
    {
        internal float angle;
        internal float X;
        internal float Y;

        internal Angle(float angle, float x, float y)
        {
            this.angle = angle;
            this.X = x;
            this.Y = y;
        }
    }

    internal class AngleList
    {
        private float iX, iY; // intersection point
        private void intersection( float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
        { // ref: http://local.wasp.uwa.edu.au/~pbourke/geometry/lineline2d/
            float denom = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);
            float uaNumerator = (x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3);

            if (denom != 0.0)
            {
                float ua = uaNumerator / denom;
                iX = x1 + ua * (x2 - x1);
                iY = y1 + ua * (y2 - y1);
            }
        }

        internal List<Angle> angles;

        // this class should have a table of most commonly computed values
	    // instead of all the trig function calls
	    // most common would be for sides = 3, 4, or 24
        internal void makeAngles( int sides, float startAngle, float stopAngle )
        {
            angles = new List<Angle>();
            double twoPi = System.Math.PI * 2.0;
            double stepSize = twoPi / sides;

            int startStep = (int) (startAngle / stepSize);
            double angle = stepSize * startStep;
            int step = startStep;
            double stopAngleTest = stopAngle;
            if (stopAngle < twoPi)
            {
                stopAngleTest = stepSize * (int)(stopAngle / stepSize) + 1;
                if (stopAngleTest < stopAngle)
                    stopAngleTest += stepSize;
                if (stopAngleTest > twoPi)
                    stopAngleTest = twoPi;
            }
	
	        while (angle <= stopAngleTest)
            {
                Angle newAngle;
                newAngle.angle = (float) angle;
                newAngle.X = (float) System.Math.Cos(angle);
                newAngle.Y = (float) System.Math.Sin(angle);
                angles.Add(newAngle);
                step += 1;
		        angle = stepSize * step;
            }

            if (startAngle > angles[0].angle)
            {
                Angle newAngle;
                intersection(angles[0].X, angles[0].Y, angles[1].X, angles[1].Y, 0.0f, 0.0f, (float)Math.Cos(startAngle), (float)Math.Sin(startAngle));
                newAngle.angle = startAngle;
                newAngle.X = iX;
                newAngle.Y = iY;
                angles[0] = newAngle;
            }

            int index = angles.Count - 1;
            if (stopAngle < angles[index].angle)
            {
                Angle newAngle;
                intersection(angles[index - 1].X, angles[index - 1].Y, angles[index].X, angles[index].Y, 0.0f, 0.0f, (float)Math.Cos(stopAngle), (float)Math.Sin(stopAngle));
                newAngle.angle = stopAngle;
                newAngle.X = iX;
                newAngle.Y = iY;
                angles[index] = newAngle;
            }
        }
    }

    internal class makeProfile
    {
        private float twoPi = 2.0f * (float)Math.PI;

        internal List<vertex> coords;
        internal List<face> faces;

        internal makeProfile(int sides, float profileStart, float profileEnd, float hollow, int hollowSides)
        {
            this.coords = new List<vertex>();
            List<vertex> hollowCoords = new List<vertex>();
            this.faces = new List<face>();
            vertex center = new vertex(0.0f, 0.0f, 0.0f);

            AngleList angles = new AngleList();
            AngleList hollowAngles = new AngleList();

            float xScale = 0.5f;
            float yScale = 0.5f; 
            if (sides == 4)  // corners of a square are sqrt(2) from center
			{
                xScale = 0.707f;
			    yScale = 0.707f;
            }

            float startAngle = profileStart * twoPi;
		    float stopAngle = profileEnd * twoPi;
            float stepSize = twoPi / sides;
            
            angles.makeAngles(sides, startAngle, stopAngle);

            if (hollow > 0.001f)
            {
                if (sides == hollowSides)
                    hollowAngles = angles;
                else
                {
                    hollowAngles = new AngleList();
                    hollowAngles.makeAngles(hollowSides, startAngle, stopAngle);
                }
            }
            else
                this.coords.Add(center);

            Angle angle;
            vertex newVert = new vertex();

            if (hollow > 0.001f && hollowSides != sides)
            {
                int numHollowAngles = hollowAngles.angles.Count;
                for (int i = 0; i < numHollowAngles; i++)
                {
                    angle = hollowAngles.angles[i];
                    newVert.X = hollow * xScale * angle.X;
                    newVert.Y = hollow * yScale * angle.Y;
                    newVert.Z = 0.0f;

                    hollowCoords.Add(newVert);
                }
            }

            int numAngles = angles.angles.Count;
            int index;
            for (index = 0; index < numAngles; index++)
            {
                angle = angles.angles[index];
                newVert.X = angle.X * xScale;
                newVert.Y = angle.Y * yScale;
                newVert.Z = 0.0f;
                this.coords.Add(newVert);

                if (hollow > 0.0f)
                {
                    newVert.X = angle.X *= hollow;
                    newVert.Y = angle.Y *= hollow;
                    newVert.Z = 0.0f;
                    hollowCoords.Add(newVert);
                }
                else if (angle.angle > 0.0001f)
                {
                    face newFace = new face();
                    newFace.v1 = 0;
                    newFace.v2 = index;
                    newFace.v3 = index;
                    this.faces.Add(newFace);
                }
            }

            if (hollow > 0.0)
            {
                hollowCoords.Reverse();

                int numOuterVerts = this.coords.Count;
                int numHollowVerts = hollowCoords.Count;
                int numTotalVerts = numOuterVerts + numHollowVerts;

                if (numOuterVerts == numHollowVerts)
                {
                    face newFace = new face();

                    for (int coordIndex = 0; coordIndex < numOuterVerts - 1; coordIndex++)
                    {
                        newFace.v1 = coordIndex;
                        newFace.v2 = coordIndex + 1;
                        newFace.v3 = numTotalVerts - coordIndex - 1;

                        this.faces.Add(newFace);

                        newFace.v1 = coordIndex + 1;
                        newFace.v2 = numTotalVerts - coordIndex - 2;
                        newFace.v3 = numTotalVerts - coordIndex - 1;

                        this.faces.Add(newFace);
                    }
                }

                else
                {
                    if (numOuterVerts < numHollowVerts)
                    {
                        face newFace = new face();
                        int j = 0; // j is the index for outer vertices
                        int maxJ = numOuterVerts - 1;
                        for (int i = 0; i < numHollowVerts; i++) // i is the index for inner vertices
                        {
                            if (j < maxJ)
                                if (angles.angles[j + 1].angle - hollowAngles.angles[i].angle <= hollowAngles.angles[i].angle - angles.angles[j].angle)
                                {
                                    newFace.v1 = numTotalVerts - i - 2;
                                    newFace.v2 = j;
                                    newFace.v3 = j + 1;

                                    this.faces.Add(newFace);
                                    j += 1;
                                }

                            newFace.v1 = j;
                            newFace.v2 = numTotalVerts - i - 2;
                            newFace.v3 = numTotalVerts - i - 1;

                            this.faces.Add(newFace);
                        }
                    }
                    else // numHollowVerts < numOuterVerts
                    {
                        face newFace = new face();
                        int j = 0; // j is the index for inner vertices
                        int maxJ = numHollowVerts - 1;
                        for (int i = 0; i < numOuterVerts; i++)
                        {
                            if (j < maxJ)
                                if (hollowAngles.angles[j + 1].angle - angles.angles[i].angle <= angles.angles[i].angle - hollowAngles.angles[j].angle)
                                {
                                    newFace.v1 = i;
                                    newFace.v2 = numTotalVerts - j - 2;
                                    newFace.v3 = numTotalVerts - j - 1;

                                    this.faces.Add(newFace);
                                    j += 1;
                                }

                            newFace.v1 = numTotalVerts - j - 1;
                            newFace.v2 = i;
                            newFace.v3 = i + 1;

                            this.faces.Add(newFace);
                        }
                    }
                    
                }

                this.coords.AddRange(hollowCoords);
            }
        }

        internal void addPos(vertex v)
        {
            this.addPos(v.X, v.Y, v.Z);
        }

        internal void addPos(float x, float y, float z)
        {
            int i;
            int numVerts = this.coords.Count;
            vertex vert;

            for (i = 0; i < numVerts; i++)
            {
                vert = this.coords[i];
                vert.X += x;
                vert.Y += y;
                vert.Z += z;
            }
        }

        internal void addRot(Quaternion q)
        {
            int i;
            int numVerts = this.coords.Count;
            vertex vert;

            for (i = 0; i < numVerts; i++)
            {
                vert = this.coords[i];
                Vertex v = new Vertex(vert.X, vert.Y, vert.Z);
                
                v = v * q;
                
                vert.X = v.X;
                vert.Y = v.Y;
                vert.Z = v.Z;
            }
        }

        internal void scale(float x, float y, float z)
        {
            int i;
            int numVerts = this.coords.Count;
            vertex vert;

            for (i = 0; i < numVerts; i++)
            {
                vert = this.coords[i];

                vert.X *= x;
                vert.Y *= y;
                vert.Z *= z;
            }
        }

        internal void flipNormals()
        {
            int i;
            int numFaces = this.faces.Count;
            face tmpFace;
            int tmp;

            for (i = 0; i < numFaces; i++)
            {
                tmpFace = this.faces[i];
                tmp = tmpFace.v3;
                tmpFace.v3 = tmpFace.v1;
                tmpFace.v1 = tmp;
            }
        }
    }

    public class PrimMesher
    {
        public List<vertex> vertices;
        public List<face> faces;

        PrimMesher()
        {
            vertices = new List<vertex>();
            faces = new List<face>();


        }
    }
}
