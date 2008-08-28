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
using System.IO;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.Meshing
{
    public struct Coord
    {
        public float X;
        public float Y;
        public float Z;

        public Coord(float x, float y, float z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        public override string ToString()
        {
            return this.X.ToString() + " " + this.Y.ToString() + " " + this.Z.ToString();
        }
    }

    public struct Face
    {
        public int v1;
        public int v2;
        public int v3;

        public Face(int v1, int v2, int v3)
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

        private void intersection(double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
        { // ref: http://local.wasp.uwa.edu.au/~pbourke/geometry/lineline2d/
            double denom = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);
            double uaNumerator = (x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3);

            if (denom != 0.0)
            {
                double ua = uaNumerator / denom;
                iX = (float)(x1 + ua * (x2 - x1));
                iY = (float)(y1 + ua * (y2 - y1));
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

            if (sides < 1)
                throw new Exception("number of sides not greater than zero");
            if (stopAngle <= startAngle)
                throw new Exception("stopAngle not greater than startAngle");

            double stepSize = twoPi / sides;

            int startStep = (int) (startAngle / stepSize);
            double angle = stepSize * startStep;
            int step = startStep;
            double stopAngleTest = stopAngle;
            if (stopAngle < twoPi)
            {
                stopAngleTest = stepSize * ((int)(stopAngle / stepSize) + 1);
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

    /// <summary>
    /// generates a profile for extrusion
    /// </summary>
    public class Profile
    {
        private const float twoPi = 2.0f * (float)Math.PI;

        internal List<Coord> coords;
        internal List<Face> faces;

        internal Profile()
        {
            this.coords = new List<Coord>();
            this.faces = new List<Face>();
        }

        public Profile(int sides, float profileStart, float profileEnd, float hollow, int hollowSides)
        {
            this.coords = new List<Coord>();
            this.faces = new List<Face>();
            Coord center = new Coord(0.0f, 0.0f, 0.0f);
            List<Coord> hollowCoords = new List<Coord>();

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

            try { angles.makeAngles(sides, startAngle, stopAngle); }
            catch (Exception ex)
            {
                Console.WriteLine("makeAngles failed: Exception: " + ex.ToString());
                Console.WriteLine("sides: " + sides.ToString() + " startAngle: " + startAngle.ToString() + " stopAngle: " + stopAngle.ToString());
                return;
            }

            if (hollow > 0.001f)
            {
                if (sides == hollowSides)
                    hollowAngles = angles;
                else
                {
                    try { hollowAngles.makeAngles(hollowSides, startAngle, stopAngle); }
                    catch (Exception ex)
                    {
                        Console.WriteLine("makeAngles failed: Exception: " + ex.ToString());
                        Console.WriteLine("sides: " + sides.ToString() + " startAngle: " + startAngle.ToString() + " stopAngle: " + stopAngle.ToString());
                        return;
                    }
                }
            }
            else
                this.coords.Add(center);

            float z = 0.0f;

            Angle angle;
            Coord newVert = new Coord();
            if (hollow > 0.001f && hollowSides != sides)
            {
                int numHollowAngles = hollowAngles.angles.Count;
                for (int i = 0; i < numHollowAngles; i++)
                {
                    angle = hollowAngles.angles[i];
                    newVert.X = hollow * xScale * angle.X;
                    newVert.Y = hollow * yScale * angle.Y;
                    newVert.Z = z;

                    hollowCoords.Add(newVert);
                }
            }

            int index = 0;
            int numAngles = angles.angles.Count;
            for (int i = 0; i < numAngles; i++)
            {
                angle = angles.angles[i];
                newVert.X = angle.X * xScale;
                newVert.Y = angle.Y * yScale;
                newVert.Z = z;
                this.coords.Add(newVert);

                if (hollow > 0.0f)
                {
                    if (hollowSides == sides)
                    {
                        newVert.X *= hollow;
                        newVert.Y *= hollow;
                        newVert.Z = z;
                        hollowCoords.Add(newVert);
                    }
                }
                else if (angle.angle > 0.0001f)
                {
                    Face newFace = new Face();
                    newFace.v1 = 0;
                    newFace.v2 = index;
                    newFace.v3 = index + 1;
                    this.faces.Add(newFace);
                }
                index += 1;
            }

            if (hollow > 0.0f)
            {
                hollowCoords.Reverse();

                int numOuterVerts = this.coords.Count;
                int numHollowVerts = hollowCoords.Count;
                int numTotalVerts = numOuterVerts + numHollowVerts;

                if (numOuterVerts == numHollowVerts)
                {
                    Face newFace = new Face();

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
                        Face newFace = new Face();
                        int j = 0; // j is the index for outer vertices
                        int maxJ = numOuterVerts - 1;
                        for (int i = 0; i < numHollowVerts; i++) // i is the index for inner vertices
                        {
                            if (j < maxJ)
                                if (angles.angles[j + 1].angle - hollowAngles.angles[i].angle <= hollowAngles.angles[i].angle - angles.angles[j].angle)
                                {
                                    newFace.v1 = numTotalVerts - i - 1;
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
                        Face newFace = new Face();
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

        public Profile Clone()
        {
            Profile clone = new Profile();

            clone.coords.AddRange(this.coords);
            clone.faces.AddRange(this.faces);

            return clone;
        }

        public void AddPos(Coord v)
        {
            this.AddPos(v.X, v.Y, v.Z);
        }

        public void AddPos(float x, float y, float z)
        {
            int i;
            int numVerts = this.coords.Count;
            Coord vert;

            for (i = 0; i < numVerts; i++)
            {
                vert = this.coords[i];
                vert.X += x;
                vert.Y += y;
                vert.Z += z;
                this.coords[i] = vert;
            }
        }

        public void AddRot(Quaternion q)
        {
            int i;
            int numVerts = this.coords.Count;
            Coord vert;

            for (i = 0; i < numVerts; i++)
            {
                vert = this.coords[i];
                Vertex v = new Vertex(vert.X, vert.Y, vert.Z) * q;
                
                vert.X = v.X;
                vert.Y = v.Y;
                vert.Z = v.Z;
                this.coords[i] = vert;
            }
        }

        public void Scale(float x, float y)
        {
            int i;
            int numVerts = this.coords.Count;
            Coord vert;

            for (i = 0; i < numVerts; i++)
            {
                vert = this.coords[i];
                vert.X *= x;
                vert.Y *= y;
                this.coords[i] = vert;
            }
        }

        public void FlipNormals()
        {
            int i;
            int numFaces = this.faces.Count;
            Face tmpFace;
            int tmp;

            for (i = 0; i < numFaces; i++)
            {
                tmpFace = this.faces[i];
                tmp = tmpFace.v3;
                tmpFace.v3 = tmpFace.v1;
                tmpFace.v1 = tmp;
                this.faces[i] = tmpFace;
            }
        }

        public void AddValue2Faces(int num)
        {
            int numFaces = this.faces.Count;
            Face tmpFace;
            for (int i = 0; i < numFaces; i++)
            {
                tmpFace = this.faces[i];
                tmpFace.v1 += num;
                tmpFace.v2 += num;
                tmpFace.v3 += num;
                this.faces[i] = tmpFace;
            }
        }

        public void DumpRaw(String path, String name, String title)
        {
            if (path == null)
                return;
            String fileName = name + "_" + title + ".raw";
            String completePath = Path.Combine(path, fileName);
            StreamWriter sw = new StreamWriter(completePath);

            for (int i = 0; i < this.faces.Count; i++)
            {
                string s = this.coords[this.faces[i].v1].ToString();
                s += " " + this.coords[this.faces[i].v2].ToString();
                s += " " + this.coords[this.faces[i].v3].ToString();

                sw.WriteLine(s);
            }

            sw.Close();
        }
    }

    public class PrimMesh
    {
        private const float twoPi = 2.0f * (float)Math.PI;

        public List<Coord> coords;
        public List<Face> faces;

        public int sides = 4;
        public int hollowSides = 4;
        public float profileStart = 0.0f;
        public float profileEnd = 1.0f;
        public float hollow = 0.0f;
        public int twistBegin = 0;
        public int twistEnd = 0;
        public float topShearX = 0.0f;
        public float topShearY = 0.0f;
        public float pathCutBegin = 0.0f;
        public float pathCutEnd = 1.0f;
        public float dimpleBegin = 0.0f;
        public float dimpleEnd = 1.0f;
        public float skew = 0.0f;
        public float holeSizeX = 1.0f; // called pathScaleX in pbs
        public float holeSizeY = 0.25f;
        public float taperX = 0.0f;
        public float taperY = 0.0f;
        public float radius = 0.0f;
        public float revolutions = 1.0f;
        public int stepsPerRevolution = 24;

        public string ParamsToDisplayString()
        {
            string s = "";
            s += "sides..................: " + this.sides.ToString();
            s += "\nhollowSides..........: " + this.hollowSides.ToString();
            s += "\nprofileStart.........: " + this.profileStart.ToString();
	        s += "\nprofileEnd...........: " + this.profileEnd.ToString();
	        s += "\nhollow...............: " + this.hollow.ToString();
	        s += "\ntwistBegin...........: " + this.twistBegin.ToString();
	        s += "\ntwistEnd.............: " + this.twistEnd.ToString();
	        s += "\ntopShearX............: " + this.topShearX.ToString();
	        s += "\ntopShearY............: " + this.topShearY.ToString();
	        s += "\npathCutBegin.........: " + this.pathCutBegin.ToString();
	        s += "\npathCutEnd...........: " + this.pathCutEnd.ToString();
	        s += "\ndimpleBegin..........: " + this.dimpleBegin.ToString();
	        s += "\ndimpleEnd............: " + this.dimpleEnd.ToString();
	        s += "\nskew.................: " + this.skew.ToString();
	        s += "\nholeSizeX............: " + this.holeSizeX.ToString();
	        s += "\nholeSizeY............: " + this.holeSizeY.ToString();
	        s += "\ntaperX...............: " + this.taperX.ToString();
	        s += "\ntaperY...............: " + this.taperY.ToString();
	        s += "\nradius...............: " + this.radius.ToString();
	        s += "\nrevolutions..........: " + this.revolutions.ToString();
            s += "\nstepsPerRevolution...: " + this.stepsPerRevolution.ToString();

            return s;
        }


        public PrimMesh(int sides, float profileStart, float profileEnd, float hollow, int hollowSides)
        {
            this.coords = new List<Coord>();
            this.faces = new List<Face>();

            this.sides = sides;
            this.profileStart = profileStart;
            this.profileEnd = profileEnd;
            this.hollow = hollow;
            this.hollowSides = hollowSides;

            if (sides < 3)
			    this.sides = 3;
            if ( hollowSides < 3)
			    this.hollowSides = 3;
		    if (profileStart < 0.0f)
                this.profileStart = 0.0f;
		    if (profileEnd > 1.0f)
                this.profileEnd = 1.0f;
		    if (profileEnd < 0.02f)
                this.profileEnd = 0.02f;
		    if (profileStart >= profileEnd)
                this.profileStart = profileEnd - 0.02f;
		    if (hollow > 1.0f)
                this.hollow = 1.0f;
		    if (hollow < 0.0f)
                this.hollow = 0.0f;
        }

        public void ExtrudeLinear()
        {
            this.coords = new List<Coord>();
            this.faces = new List<Face>();

            int step = 0;
            int steps = 1;

            float length = this.pathCutEnd - this.pathCutBegin;

#if VIEWER
            if (this.sides == 3)
            {
                // prisms don't taper well so add some vertical resolution
                // other prims may benefit from this but just do prisms for now
                if (Math.Abs(this.taperX) > 0.01 || Math.Abs(this.taperY) > 0.01)
                    steps = (int)(steps * 4.5 * length);
            }
#endif
            
            float twistBegin = this.twistBegin / 360.0f * twoPi;
            float twistEnd = this.twistEnd / 360.0f * twoPi;
            float twistTotal = twistEnd - twistBegin;
            float twistTotalAbs = Math.Abs(twistTotal);
            if (twistTotalAbs > 0.01f)
                steps += (int)(twistTotalAbs * 3.66); //  dahlia's magic number

            float start = -0.5f;
            float stepSize = length / (float)steps;
            float percentOfPathMultiplier = stepSize;
            float xProfileScale = 1.0f;
            float yProfileScale = 1.0f;
            float xOffset = 0.0f;
            float yOffset = 0.0f;
            float zOffset = start;
            float xOffsetStepIncrement = this.topShearX / steps;
            float yOffsetStepIncrement = this.topShearY / steps;

            float percentOfPath = this.pathCutBegin;
            zOffset += percentOfPath;

            float hollow = this.hollow;

            // sanity checks
            float initialProfileRot = 0.0f;
            if (this.sides == 3)
            {
                if (this.hollowSides == 4)
                {
                    if (hollow > 0.7f)
                        hollow = 0.7f;
                    hollow *= 0.707f;
                }
                else hollow *= 0.5f;
            }
            else if (this.sides == 4)
            {
                initialProfileRot = 1.25f * (float)Math.PI;
                if (this.hollowSides != 4)
                    hollow *= 0.707f;
            }

            Profile profile = new Profile(this.sides, this.profileStart, this.profileEnd, hollow, this.hollowSides);

            if (initialProfileRot != 0.0f)
                profile.AddRot(new Quaternion(new Vertex(0.0f, 0.0f, 1.0f), initialProfileRot));

            bool done = false;
            while (!done)
            {
                Profile newLayer = profile.Clone();

                if (this.taperX == 0.0f)
                    xProfileScale = 1.0f;
                else if (this.taperX > 0.0f)
                    xProfileScale = 1.0f - percentOfPath * this.taperX;
                else xProfileScale = 1.0f + (1.0f - percentOfPath) * this.taperX;

                if (this.taperY == 0.0f)
                    yProfileScale = 1.0f;
                else if (this.taperY > 0.0f)
                    yProfileScale = 1.0f - percentOfPath * this.taperY;
                else yProfileScale = 1.0f + (1.0f - percentOfPath) * this.taperY;

                if (xProfileScale != 1.0f || yProfileScale != 1.0f)
                    newLayer.Scale(xProfileScale, yProfileScale);

                float twist = twistBegin + twistTotal * percentOfPath;
                if (twist != 0.0f)
                    newLayer.AddRot(new Quaternion(new Vertex(0.0f, 0.0f, 1.0f), twist));

                newLayer.AddPos(xOffset, yOffset, zOffset);

                if (step == 0)
                    newLayer.FlipNormals();

                // append this layer

                int coordsLen = this.coords.Count;
                newLayer.AddValue2Faces(coordsLen);

                this.coords.AddRange(newLayer.coords);
                this.faces.AddRange(newLayer.faces); 

                // fill faces between layers

                int numVerts = newLayer.coords.Count;
                Face newFace = new Face();
                if (step > 0)
                {
                    for (int i = coordsLen; i < this.coords.Count - 1; i++)
                    {
                        newFace.v1 = i;
                        newFace.v2 = i - numVerts;
                        newFace.v3 = i - numVerts + 1;
                        this.faces.Add(newFace);

                        newFace.v2 = i - numVerts + 1;
                        newFace.v3 = i + 1;
                        this.faces.Add(newFace);
                    }

                    newFace.v1 = coordsLen - 1;
                    newFace.v2 = coordsLen - numVerts;
                    newFace.v3 = coordsLen;
                    this.faces.Add(newFace);

                    newFace.v1 = coordsLen + numVerts - 1;
                    newFace.v2 = coordsLen - 1;
                    newFace.v3 = coordsLen;
                    this.faces.Add(newFace);
                }

                // calc the step for the next iteration of the loop

                if (step < steps)
                {
                    step += 1;
                    percentOfPath += percentOfPathMultiplier;
                    xOffset += xOffsetStepIncrement;
                    yOffset += yOffsetStepIncrement;
                    zOffset += stepSize;
                    if (percentOfPath > this.pathCutEnd)
                        done = true;
                }
                else done = true;
            }
        }
        

        public void ExtrudeCircular()
        {
            this.coords = new List<Coord>();
            this.faces = new List<Face>();

            int step = 0;
            int steps = 24;

            float twistBegin = this.twistBegin / 360.0f * twoPi;
            float twistEnd = this.twistEnd / 360.0f * twoPi;
            float twistTotal = twistEnd - twistBegin;

		    // if the profile has a lot of twist, add more layers otherwise the layers may overlap
		    // and the resulting mesh may be quite inaccurate. This method is arbitrary and doesn't
		    // accurately match the viewer
            float twistTotalAbs = Math.Abs(twistTotal);
            if (twistTotalAbs > 0.01f)
            {
                if (twistTotalAbs > Math.PI * 1.5f)
                    steps *= 2;
                if (twistTotalAbs > Math.PI * 3.0f)
                    steps *= 2;
            }
            
            float yPathScale = this.holeSizeY * 0.5f;
            float pathLength = this.pathCutEnd - this.pathCutBegin;
            float totalSkew = this.skew * 2.0f * pathLength;
            float skewStart = this.pathCutBegin * 2.0f * this.skew - this.skew;

            // It's not quite clear what pushY (Y top shear) does, but subtracting it from the start and end
	        // angles appears to approximate it's effects on path cut. Likewise, adding it to the angle used
	        // to calculate the sine for generating the path radius appears to approximate it's effects there
	        // too, but there are some subtle differences in the radius which are noticeable as the prim size 
	        // increases and it may affect megaprims quite a bit. The effect of the Y top shear parameter on
	        // the meshes generated with this technique appear nearly identical in shape to the same prims when
	        // displayed by the viewer.

            float startAngle = (twoPi * this.pathCutBegin * this.revolutions) - this.topShearY * 0.9f;
            float endAngle = (twoPi * this.pathCutEnd * this.revolutions) - this.topShearY * 0.9f;
            float stepSize = twoPi / this.stepsPerRevolution;

            step = (int)(startAngle / stepSize);
            int firstStep = step;
            float angle = startAngle;
            float hollow = this.hollow;

            // sanity checks
            float initialProfileRot = 0.0f;
            if (this.sides == 3)
            {
                initialProfileRot = (float)Math.PI;
                if (this.hollowSides == 4)
                {
                    if (hollow > 0.7f)
                        hollow = 0.7f;
                    hollow *= 0.707f;
                }
                else hollow *= 0.5f;
            }
            else if (this.sides == 4)
            {
                initialProfileRot = 0.25f * (float)Math.PI;
                if (this.hollowSides != 4)
                    hollow *= 0.707f;
            }
            else if (this.sides > 4)
            {
                initialProfileRot = (float)Math.PI;
                if (this.hollowSides == 4)
                {
                    if (hollow > 0.7f)
                        hollow = 0.7f;
                    hollow /= 0.7f;
                }
            }
            
            Profile profile = new Profile(this.sides, this.profileStart, this.profileEnd, hollow, this.hollowSides);

            if (initialProfileRot != 0.0f)
                profile.AddRot(new Quaternion(new Vertex(0.0f, 0.0f, 1.0f), initialProfileRot));

            bool done = false;
            while (!done) // loop through the length of the path and add the layers
            {
                Profile newLayer = profile.Clone();

                float xProfileScale = (1.0f - Math.Abs(this.skew)) * this.holeSizeX;
                float yProfileScale = this.holeSizeY;

                float percentOfPath = angle / (twoPi * this.revolutions);
                float percentOfAngles = (angle - startAngle) / (endAngle - startAngle);

                if (this.taperX > 0.01f)
                    xProfileScale *= 1.0f - percentOfPath * this.taperX;
                else if (this.taperX < -0.01f)
                    xProfileScale *= 1.0f + (1.0f - percentOfPath) * this.taperX;

                if (this.taperY > 0.01f)
                    yProfileScale *= 1.0f - percentOfPath * this.taperY;
                else if (this.taperY < -0.01f)
                    yProfileScale *= 1.0f + (1.0f - percentOfPath) * this.taperY;

                if (xProfileScale != 1.0f || yProfileScale != 1.0f)
                    newLayer.Scale(xProfileScale, yProfileScale);

                float radiusScale = 1.0f;
                if (this.radius > 0.001f)
                    radiusScale = 1.0f - this.radius * percentOfPath;
                else if (this.radius < 0.001f)
                    radiusScale = 1.0f + this.radius * (1.0f - percentOfPath);

                float twist = twistBegin + twistTotal * percentOfPath;

                float xOffset = 0.5f * (skewStart + totalSkew * percentOfAngles);

                xOffset += (float)Math.Sin(angle) * this.topShearX * 0.45f;
                float yOffset = (float)Math.Cos(angle) * (0.5f - yPathScale) * radiusScale;

                float zOffset = (float)Math.Sin(angle + this.topShearY * 0.9f) * (0.5f - yPathScale) * radiusScale;

                // next apply twist rotation to the profile layer
                if (twistTotal != 0.0f || twistBegin != 0.0f)
                    newLayer.AddRot(new Quaternion(new Vertex(0.0f, 0.0f, 1.0f), twist));

                // now orient the rotation of the profile layer relative to it's position on the path
		        // adding taperY to the angle used to generate the quat appears to approximate the viewer
                newLayer.AddRot(new Quaternion(new Vertex(1.0f, 0.0f, 0.0f), angle + this.topShearY * 0.9f));
                newLayer.AddPos(xOffset, yOffset, zOffset);

                if (angle == startAngle)
                    newLayer.FlipNormals();

                // append the layer and fill in the sides

                int coordsLen = this.coords.Count;
                newLayer.AddValue2Faces(coordsLen);

                this.coords.AddRange(newLayer.coords);
                this.faces.AddRange(newLayer.faces);

                // fill faces between layers

                int numVerts = newLayer.coords.Count;
                Face newFace = new Face();
                if (step > firstStep)
                {
                    for (int i = coordsLen; i < this.coords.Count - 1; i++)
                    {
                        newFace.v1 = i;
                        newFace.v2 = i - numVerts;
                        newFace.v3 = i - numVerts + 1;
                        this.faces.Add(newFace);

                        newFace.v2 = i - numVerts + 1;
                        newFace.v3 = i + 1;
                        this.faces.Add(newFace);
                    }

                    newFace.v1 = coordsLen - 1;
                    newFace.v2 = coordsLen - numVerts;
                    newFace.v3 = coordsLen;
                    this.faces.Add(newFace);

                    newFace.v1 = coordsLen + numVerts - 1;
                    newFace.v2 = coordsLen - 1;
                    newFace.v3 = coordsLen;
                    this.faces.Add(newFace);
                }

                // calculate terms for next iteration
                // calculate the angle for the next iteration of the loop

                if (angle >= endAngle)
                    done = true;
                else
                {
                    step += 1;
                    angle = stepSize * step;
                    if (angle > endAngle)
                        angle = endAngle;
                }
            }
        }

        public void AddPos(float x, float y, float z)
        {
            int i;
            int numVerts = this.coords.Count;
            Coord vert;

            for (i = 0; i < numVerts; i++)
            {
                vert = this.coords[i];
                vert.X += x;
                vert.Y += y;
                vert.Z += z;
                this.coords[i] = vert;
            }
        }

        public void AddRot(Quaternion q)
        {
            int i;
            int numVerts = this.coords.Count;
            Coord vert;

            for (i = 0; i < numVerts; i++)
            {
                vert = this.coords[i];
                Vertex v = new Vertex(vert.X, vert.Y, vert.Z) * q;

                vert.X = v.X;
                vert.Y = v.Y;
                vert.Z = v.Z;
                this.coords[i] = vert;
            }
        }

        public void Scale(float x, float y, float z)
        {
            int i;
            int numVerts = this.coords.Count;
            Coord vert;

            for (i = 0; i < numVerts; i++)
            {
                vert = this.coords[i];
                vert.X *= x;
                vert.Y *= y;
                vert.Z *= z;
                this.coords[i] = vert;
            }
        }

        public void DumpRaw(String path, String name, String title)
        {
            if (path == null)
                return;
            String fileName = name + "_" + title + ".raw";
            String completePath = Path.Combine(path, fileName);
            StreamWriter sw = new StreamWriter(completePath);

            for (int i = 0; i < this.faces.Count; i++)
            {
                string s = this.coords[this.faces[i].v1].ToString();
                s += " " + this.coords[this.faces[i].v2].ToString();
                s += " " + this.coords[this.faces[i].v3].ToString();

                sw.WriteLine(s);
            }
            
            sw.Close();
        }
    }
}
