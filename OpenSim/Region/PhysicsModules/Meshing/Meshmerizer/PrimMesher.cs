/*
 * Copyright (c) Contributors
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
using System.IO;

namespace PrimMesher
{
    public struct Quat
    {
        /// <summary>X value</summary>
        public float X;
        /// <summary>Y value</summary>
        public float Y;
        /// <summary>Z value</summary>
        public float Z;
        /// <summary>W value</summary>
        public float W;

        public Quat(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public Quat(Coord axis, float angle)
        {
            axis = axis.Normalize();

            angle *= 0.5f;
            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);

            X = axis.X * s;
            Y = axis.Y * s;
            Z = axis.Z * s;
            W = c;

            Normalize();
        }

        public float Length()
        {
            return (float)Math.Sqrt(X * X + Y * Y + Z * Z + W * W);
        }

        public Quat Normalize()
        {
            const float MAG_THRESHOLD = 0.0000001f;
            float mag = Length();

            // Catch very small rounding errors when normalizing
            if (mag > MAG_THRESHOLD)
            {
                float oomag = 1f / mag;
                X *= oomag;
                Y *= oomag;
                Z *= oomag;
                W *= oomag;
            }
            else
            {
                X = 0f;
                Y = 0f;
                Z = 0f;
                W = 1f;
            }

            return this;
        }

        public static Quat operator *(Quat q1, Quat q2)
        {
            float x = q1.W * q2.X + q1.X * q2.W + q1.Y * q2.Z - q1.Z * q2.Y;
            float y = q1.W * q2.Y - q1.X * q2.Z + q1.Y * q2.W + q1.Z * q2.X;
            float z = q1.W * q2.Z + q1.X * q2.Y - q1.Y * q2.X + q1.Z * q2.W;
            float w = q1.W * q2.W - q1.X * q2.X - q1.Y * q2.Y - q1.Z * q2.Z;
            return new Quat(x, y, z, w);
        }

        public override string ToString()
        {
            return "< X: " + this.X.ToString() + ", Y: " + this.Y.ToString() + ", Z: " + this.Z.ToString() + ", W: " + this.W.ToString() + ">";
        }
    }

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

        public float Length()
        {
            return (float)Math.Sqrt(this.X * this.X + this.Y * this.Y + this.Z * this.Z);
        }

        public Coord Invert()
        {
            this.X = -this.X;
            this.Y = -this.Y;
            this.Z = -this.Z;

            return this;
        }

        public Coord Normalize()
        {
            const float MAG_THRESHOLD = 0.0000001f;
            float mag = Length();

            // Catch very small rounding errors when normalizing
            if (mag > MAG_THRESHOLD)
            {
                float oomag = 1.0f / mag;
                this.X *= oomag;
                this.Y *= oomag;
                this.Z *= oomag;
            }
            else
            {
                this.X = 0.0f;
                this.Y = 0.0f;
                this.Z = 0.0f;
            }

            return this;
        }

        public override string ToString()
        {
            return this.X.ToString() + " " + this.Y.ToString() + " " + this.Z.ToString();
        }

        public static Coord Cross(Coord c1, Coord c2)
        {
            return new Coord(
                c1.Y * c2.Z - c2.Y * c1.Z,
                c1.Z * c2.X - c2.Z * c1.X,
                c1.X * c2.Y - c2.X * c1.Y
                );
        }

        public static Coord operator +(Coord v, Coord a)
        {
            return new Coord(v.X + a.X, v.Y + a.Y, v.Z + a.Z);
        }

        public static Coord operator *(Coord v, Coord m)
        {
            return new Coord(v.X * m.X, v.Y * m.Y, v.Z * m.Z);
        }

        public static Coord operator *(Coord v, Quat q)
        {
            // From http://www.euclideanspace.com/maths/algebra/realNormedAlgebra/quaternions/transforms/

            Coord c2 = new Coord(0.0f, 0.0f, 0.0f);

            c2.X = q.W * q.W * v.X +
                2f * q.Y * q.W * v.Z -
                2f * q.Z * q.W * v.Y +
                     q.X * q.X * v.X +
                2f * q.Y * q.X * v.Y +
                2f * q.Z * q.X * v.Z -
                     q.Z * q.Z * v.X -
                     q.Y * q.Y * v.X;

            c2.Y =
                2f * q.X * q.Y * v.X +
                     q.Y * q.Y * v.Y +
                2f * q.Z * q.Y * v.Z +
                2f * q.W * q.Z * v.X -
                     q.Z * q.Z * v.Y +
                     q.W * q.W * v.Y -
                2f * q.X * q.W * v.Z -
                     q.X * q.X * v.Y;

            c2.Z =
                2f * q.X * q.Z * v.X +
                2f * q.Y * q.Z * v.Y +
                     q.Z * q.Z * v.Z -
                2f * q.W * q.Y * v.X -
                     q.Y * q.Y * v.Z +
                2f * q.W * q.X * v.Y -
                     q.X * q.X * v.Z +
                     q.W * q.W * v.Z;

            return c2;
        }
    }

    public struct UVCoord
    {
        public float U;
        public float V;


        public UVCoord(float u, float v)
        {
            this.U = u;
            this.V = v;
        }

        public UVCoord Flip()
        {
            this.U = 1.0f - this.U;
            this.V = 1.0f - this.V;
            return this;
        }
    }

    public struct Face
    {
        public int primFace;

        // vertices
        public int v1;
        public int v2;
        public int v3;

        //normals
        public int n1;
        public int n2;
        public int n3;

        // uvs
        public int uv1;
        public int uv2;
        public int uv3;

        public Face(int v1, int v2, int v3)
        {
            primFace = 0;

            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;

            this.n1 = 0;
            this.n2 = 0;
            this.n3 = 0;

            this.uv1 = 0;
            this.uv2 = 0;
            this.uv3 = 0;

        }

        public Face(int v1, int v2, int v3, int n1, int n2, int n3)
        {
            primFace = 0;

            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;

            this.n1 = n1;
            this.n2 = n2;
            this.n3 = n3;

            this.uv1 = 0;
            this.uv2 = 0;
            this.uv3 = 0;
        }

        public Coord SurfaceNormal(List<Coord> coordList)
        {
            Coord c1 = coordList[this.v1];
            Coord c2 = coordList[this.v2];
            Coord c3 = coordList[this.v3];

            Coord edge1 = new Coord(c2.X - c1.X, c2.Y - c1.Y, c2.Z - c1.Z);
            Coord edge2 = new Coord(c3.X - c1.X, c3.Y - c1.Y, c3.Z - c1.Z);

            return Coord.Cross(edge1, edge2).Normalize();
        }
    }

    public struct ViewerFace
    {
        public int primFaceNumber;

        public Coord v1;
        public Coord v2;
        public Coord v3;

        public int coordIndex1;
        public int coordIndex2;
        public int coordIndex3;

        public Coord n1;
        public Coord n2;
        public Coord n3;

        public UVCoord uv1;
        public UVCoord uv2;
        public UVCoord uv3;

        public ViewerFace(int primFaceNumber)
        {
            this.primFaceNumber = primFaceNumber;

            this.v1 = new Coord();
            this.v2 = new Coord();
            this.v3 = new Coord();

            this.coordIndex1 = this.coordIndex2 = this.coordIndex3 = -1; // -1 means not assigned yet

            this.n1 = new Coord();
            this.n2 = new Coord();
            this.n3 = new Coord();

            this.uv1 = new UVCoord();
            this.uv2 = new UVCoord();
            this.uv3 = new UVCoord();
        }

        public void Scale(float x, float y, float z)
        {
            this.v1.X *= x;
            this.v1.Y *= y;
            this.v1.Z *= z;

            this.v2.X *= x;
            this.v2.Y *= y;
            this.v2.Z *= z;

            this.v3.X *= x;
            this.v3.Y *= y;
            this.v3.Z *= z;
        }

        public void AddPos(float x, float y, float z)
        {
            this.v1.X += x;
            this.v2.X += x;
            this.v3.X += x;

            this.v1.Y += y;
            this.v2.Y += y;
            this.v3.Y += y;

            this.v1.Z += z;
            this.v2.Z += z;
            this.v3.Z += z;
        }

        public void AddRot(Quat q)
        {
            this.v1 *= q;
            this.v2 *= q;
            this.v3 *= q;

            this.n1 *= q;
            this.n2 *= q;
            this.n3 *= q;
        }

        public void CalcSurfaceNormal()
        {

            Coord edge1 = new Coord(this.v2.X - this.v1.X, this.v2.Y - this.v1.Y, this.v2.Z - this.v1.Z);
            Coord edge2 = new Coord(this.v3.X - this.v1.X, this.v3.Y - this.v1.Y, this.v3.Z - this.v1.Z);

            this.n1 = this.n2 = this.n3 = Coord.Cross(edge1, edge2).Normalize();
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

        private static Angle[] angles3 =
        {
            new Angle(0.0f, 1.0f, 0.0f),
            new Angle(0.33333333333333333f, -0.5f, 0.86602540378443871f),
            new Angle(0.66666666666666667f, -0.5f, -0.86602540378443837f),
            new Angle(1.0f, 1.0f, 0.0f)
        };

        private static Coord[] normals3 =
        {
            new Coord(0.25f, 0.4330127019f, 0.0f).Normalize(),
            new Coord(-0.5f, 0.0f, 0.0f).Normalize(),
            new Coord(0.25f, -0.4330127019f, 0.0f).Normalize(),
            new Coord(0.25f, 0.4330127019f, 0.0f).Normalize()
        };

        private static Angle[] angles4 =
        {
            new Angle(0.0f, 1.0f, 0.0f),
            new Angle(0.25f, 0.0f, 1.0f),
            new Angle(0.5f, -1.0f, 0.0f),
            new Angle(0.75f, 0.0f, -1.0f),
            new Angle(1.0f, 1.0f, 0.0f)
        };

        private static Coord[] normals4 = 
        {
            new Coord(0.5f, 0.5f, 0.0f).Normalize(),
            new Coord(-0.5f, 0.5f, 0.0f).Normalize(),
            new Coord(-0.5f, -0.5f, 0.0f).Normalize(),
            new Coord(0.5f, -0.5f, 0.0f).Normalize(),
            new Coord(0.5f, 0.5f, 0.0f).Normalize()
        };

        private static Angle[] angles24 =
        {
            new Angle(0.0f, 1.0f, 0.0f),
            new Angle(0.041666666666666664f, 0.96592582628906831f, 0.25881904510252074f),
            new Angle(0.083333333333333329f, 0.86602540378443871f, 0.5f),
            new Angle(0.125f, 0.70710678118654757f, 0.70710678118654746f),
            new Angle(0.16666666666666667f, 0.5f, 0.8660254037844386f),
            new Angle(0.20833333333333331f, 0.25881904510252096f, 0.9659258262890682f),
            new Angle(0.25f, 0.0f, 1.0f),
            new Angle(0.29166666666666663f, -0.25881904510252063f, 0.96592582628906831f),
            new Angle(0.33333333333333333f, -0.5f, 0.86602540378443871f),
            new Angle(0.375f, -0.70710678118654746f, 0.70710678118654757f),
            new Angle(0.41666666666666663f, -0.86602540378443849f, 0.5f),
            new Angle(0.45833333333333331f, -0.9659258262890682f, 0.25881904510252102f),
            new Angle(0.5f, -1.0f, 0.0f),
            new Angle(0.54166666666666663f, -0.96592582628906842f, -0.25881904510252035f),
            new Angle(0.58333333333333326f, -0.86602540378443882f, -0.5f),
            new Angle(0.62499999999999989f, -0.70710678118654791f, -0.70710678118654713f),
            new Angle(0.66666666666666667f, -0.5f, -0.86602540378443837f),
            new Angle(0.70833333333333326f, -0.25881904510252152f, -0.96592582628906809f),
            new Angle(0.75f, 0.0f, -1.0f),
            new Angle(0.79166666666666663f, 0.2588190451025203f, -0.96592582628906842f),
            new Angle(0.83333333333333326f, 0.5f, -0.86602540378443904f),
            new Angle(0.875f, 0.70710678118654735f, -0.70710678118654768f),
            new Angle(0.91666666666666663f, 0.86602540378443837f, -0.5f),
            new Angle(0.95833333333333326f, 0.96592582628906809f, -0.25881904510252157f),
            new Angle(1.0f, 1.0f, 0.0f)
        };

        private Angle interpolatePoints(float newPoint, Angle p1, Angle p2)
        {
            float m = (newPoint - p1.angle) / (p2.angle - p1.angle);
            return new Angle(newPoint, p1.X + m * (p2.X - p1.X), p1.Y + m * (p2.Y - p1.Y));
        }

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
        internal List<Coord> normals;

        internal void makeAngles(int sides, float startAngle, float stopAngle)
        {
            angles = new List<Angle>();
            normals = new List<Coord>();

            double twoPi = System.Math.PI * 2.0;
            float twoPiInv = 1.0f / (float)twoPi;

            if (sides < 1)
                throw new Exception("number of sides not greater than zero");
            if (stopAngle <= startAngle)
                throw new Exception("stopAngle not greater than startAngle");

            if ((sides == 3 || sides == 4 || sides == 24))
            {
                startAngle *= twoPiInv;
                stopAngle *= twoPiInv;

                Angle[] sourceAngles;
                if (sides == 3)
                    sourceAngles = angles3;
                else if (sides == 4)
                    sourceAngles = angles4;
                else sourceAngles = angles24;

                int startAngleIndex = (int)(startAngle * sides);
                int endAngleIndex = sourceAngles.Length - 1;
                if (stopAngle < 1.0f)
                    endAngleIndex = (int)(stopAngle * sides) + 1;
                if (endAngleIndex == startAngleIndex)
                    endAngleIndex++;

                for (int angleIndex = startAngleIndex; angleIndex < endAngleIndex + 1; angleIndex++)
                {
                    angles.Add(sourceAngles[angleIndex]);
                    if (sides == 3)
                        normals.Add(normals3[angleIndex]);
                    else if (sides == 4)
                        normals.Add(normals4[angleIndex]);
                }

                if (startAngle > 0.0f)
                    angles[0] = interpolatePoints(startAngle, angles[0], angles[1]);

                if (stopAngle < 1.0f)
                {
                    int lastAngleIndex = angles.Count - 1;
                    angles[lastAngleIndex] = interpolatePoints(stopAngle, angles[lastAngleIndex - 1], angles[lastAngleIndex]);
                }
            }
            else
            {
                double stepSize = twoPi / sides;

                int startStep = (int)(startAngle / stepSize);
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
                    newAngle.angle = (float)angle;
                    newAngle.X = (float)System.Math.Cos(angle);
                    newAngle.Y = (float)System.Math.Sin(angle);
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
    }

    /// <summary>
    /// generates a profile for extrusion
    /// </summary>
    public class Profile
    {
        private const float twoPi = 2.0f * (float)Math.PI;

        public string errorMessage = null;

        public List<Coord> coords;
        public List<Face> faces;
        public List<Coord> vertexNormals;
        public List<float> us;
        public List<UVCoord> faceUVs;
        public List<int> faceNumbers;

        // use these for making individual meshes for each prim face
        public List<int> outerCoordIndices = null;
        public List<int> hollowCoordIndices = null;
        public List<int> cut1CoordIndices = null;
        public List<int> cut2CoordIndices = null;

        public Coord faceNormal = new Coord(0.0f, 0.0f, 1.0f);
        public Coord cutNormal1 = new Coord();
        public Coord cutNormal2 = new Coord();

        public int numOuterVerts = 0;
        public int numHollowVerts = 0;

        public int outerFaceNumber = -1;
        public int hollowFaceNumber = -1;

        public bool calcVertexNormals = false;
        public int bottomFaceNumber = 0;
        public int numPrimFaces = 0;

        public Profile()
        {
            this.coords = new List<Coord>();
            this.faces = new List<Face>();
            this.vertexNormals = new List<Coord>();
            this.us = new List<float>();
            this.faceUVs = new List<UVCoord>();
            this.faceNumbers = new List<int>();
        }

        public Profile(int sides, float profileStart, float profileEnd, float hollow, int hollowSides, bool createFaces, bool calcVertexNormals)
        {
            this.calcVertexNormals = calcVertexNormals;
            this.coords = new List<Coord>();
            this.faces = new List<Face>();
            this.vertexNormals = new List<Coord>();
            this.us = new List<float>();
            this.faceUVs = new List<UVCoord>();
            this.faceNumbers = new List<int>();

            Coord center = new Coord(0.0f, 0.0f, 0.0f);

            List<Coord> hollowCoords = new List<Coord>();
            List<Coord> hollowNormals = new List<Coord>();
            List<float> hollowUs = new List<float>();

            if (calcVertexNormals)
            {
                this.outerCoordIndices = new List<int>();
                this.hollowCoordIndices = new List<int>();
                this.cut1CoordIndices = new List<int>();
                this.cut2CoordIndices = new List<int>();
            }

            bool hasHollow = (hollow > 0.0f);

            bool hasProfileCut = (profileStart > 0.0f || profileEnd < 1.0f);

            AngleList angles = new AngleList();
            AngleList hollowAngles = new AngleList();

            float xScale = 0.5f;
            float yScale = 0.5f;
            if (sides == 4)  // corners of a square are sqrt(2) from center
            {
                xScale = 0.707107f;
                yScale = 0.707107f;
            }

            float startAngle = profileStart * twoPi;
            float stopAngle = profileEnd * twoPi;

            try { angles.makeAngles(sides, startAngle, stopAngle); }
            catch (Exception ex)
            {

                errorMessage = "makeAngles failed: Exception: " + ex.ToString()
                + "\nsides: " + sides.ToString() + " startAngle: " + startAngle.ToString() + " stopAngle: " + stopAngle.ToString();

                return;
            }

            this.numOuterVerts = angles.angles.Count;

            // flag to create as few triangles as possible for 3 or 4 side profile
            bool simpleFace = (sides < 5 && !hasHollow && !hasProfileCut);

            if (hasHollow)
            {
                if (sides == hollowSides)
                    hollowAngles = angles;
                else
                {
                    try { hollowAngles.makeAngles(hollowSides, startAngle, stopAngle); }
                    catch (Exception ex)
                    {
                        errorMessage = "makeAngles failed: Exception: " + ex.ToString()
                        + "\nsides: " + sides.ToString() + " startAngle: " + startAngle.ToString() + " stopAngle: " + stopAngle.ToString();

                        return;
                    }
                }
                this.numHollowVerts = hollowAngles.angles.Count;
            }
            else if (!simpleFace)
            {
                this.coords.Add(center);
                if (this.calcVertexNormals)
                    this.vertexNormals.Add(new Coord(0.0f, 0.0f, 1.0f));
                this.us.Add(0.0f);
            }

            float z = 0.0f;

            Angle angle;
            Coord newVert = new Coord();
            if (hasHollow && hollowSides != sides)
            {
                int numHollowAngles = hollowAngles.angles.Count;
                for (int i = 0; i < numHollowAngles; i++)
                {
                    angle = hollowAngles.angles[i];
                    newVert.X = hollow * xScale * angle.X;
                    newVert.Y = hollow * yScale * angle.Y;
                    newVert.Z = z;

                    hollowCoords.Add(newVert);
                    if (this.calcVertexNormals)
                    {
                        if (hollowSides < 5)
                            hollowNormals.Add(hollowAngles.normals[i].Invert());
                        else
                            hollowNormals.Add(new Coord(-angle.X, -angle.Y, 0.0f));

                        if (hollowSides == 4)
                            hollowUs.Add(angle.angle * hollow * 0.707107f);
                        else
                            hollowUs.Add(angle.angle * hollow);
                    }
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
                if (this.calcVertexNormals)
                {
                    this.outerCoordIndices.Add(this.coords.Count - 1);

                    if (sides < 5)
                    {
                        this.vertexNormals.Add(angles.normals[i]);
                        float u = angle.angle;
                        this.us.Add(u);
                    }
                    else
                    {
                        this.vertexNormals.Add(new Coord(angle.X, angle.Y, 0.0f));
                        this.us.Add(angle.angle);
                    }
                }

                if (hasHollow)
                {
                    if (hollowSides == sides)
                    {
                        newVert.X *= hollow;
                        newVert.Y *= hollow;
                        newVert.Z = z;
                        hollowCoords.Add(newVert);
                        if (this.calcVertexNormals)
                        {
                            if (sides < 5)
                            {
                                hollowNormals.Add(angles.normals[i].Invert());
                            }

                            else
                                hollowNormals.Add(new Coord(-angle.X, -angle.Y, 0.0f));

                            hollowUs.Add(angle.angle * hollow);
                        }
                    }
                }
                else if (!simpleFace && createFaces && angle.angle > 0.0001f)
                {
                    Face newFace = new Face();
                    newFace.v1 = 0;
                    newFace.v2 = index;
                    newFace.v3 = index + 1;

                    this.faces.Add(newFace);
                }
                index += 1;
            }

            if (hasHollow)
            {
                hollowCoords.Reverse();
                if (this.calcVertexNormals)
                {
                    hollowNormals.Reverse();
                    hollowUs.Reverse();
                }

                if (createFaces)
                {
                    int numTotalVerts = this.numOuterVerts + this.numHollowVerts;

                    if (this.numOuterVerts == this.numHollowVerts)
                    {
                        Face newFace = new Face();

                        for (int coordIndex = 0; coordIndex < this.numOuterVerts - 1; coordIndex++)
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
                        if (this.numOuterVerts < this.numHollowVerts)
                        {
                            Face newFace = new Face();
                            int j = 0; // j is the index for outer vertices
                            int maxJ = this.numOuterVerts - 1;
                            for (int i = 0; i < this.numHollowVerts; i++) // i is the index for inner vertices
                            {
                                if (j < maxJ)
                                    if (angles.angles[j + 1].angle - hollowAngles.angles[i].angle < hollowAngles.angles[i].angle - angles.angles[j].angle + 0.000001f)
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
                            int maxJ = this.numHollowVerts - 1;
                            for (int i = 0; i < this.numOuterVerts; i++)
                            {
                                if (j < maxJ)
                                    if (hollowAngles.angles[j + 1].angle - angles.angles[i].angle < angles.angles[i].angle - hollowAngles.angles[j].angle + 0.000001f)
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
                }

                if (calcVertexNormals)
                {
                    foreach (Coord hc in hollowCoords)
                    {
                        this.coords.Add(hc);
                        hollowCoordIndices.Add(this.coords.Count - 1);
                    }
                }
                else
                    this.coords.AddRange(hollowCoords);

                if (this.calcVertexNormals)
                {
                    this.vertexNormals.AddRange(hollowNormals);
                    this.us.AddRange(hollowUs);

                }
            }

            if (simpleFace && createFaces)
            {
                if (sides == 3)
                    this.faces.Add(new Face(0, 1, 2));
                else if (sides == 4)
                {
                    this.faces.Add(new Face(0, 1, 2));
                    this.faces.Add(new Face(0, 2, 3));
                }
            }

            if (calcVertexNormals && hasProfileCut)
            {
                int lastOuterVertIndex = this.numOuterVerts - 1;

                if (hasHollow)
                {
                    this.cut1CoordIndices.Add(0);
                    this.cut1CoordIndices.Add(this.coords.Count - 1);

                    this.cut2CoordIndices.Add(lastOuterVertIndex + 1);
                    this.cut2CoordIndices.Add(lastOuterVertIndex);

                    this.cutNormal1.X = this.coords[0].Y - this.coords[this.coords.Count - 1].Y;
                    this.cutNormal1.Y = -(this.coords[0].X - this.coords[this.coords.Count - 1].X);

                    this.cutNormal2.X = this.coords[lastOuterVertIndex + 1].Y - this.coords[lastOuterVertIndex].Y;
                    this.cutNormal2.Y = -(this.coords[lastOuterVertIndex + 1].X - this.coords[lastOuterVertIndex].X);
                }

                else
                {
                    this.cut1CoordIndices.Add(0);
                    this.cut1CoordIndices.Add(1);

                    this.cut2CoordIndices.Add(lastOuterVertIndex);
                    this.cut2CoordIndices.Add(0);

                    this.cutNormal1.X = this.vertexNormals[1].Y;
                    this.cutNormal1.Y = -this.vertexNormals[1].X;

                    this.cutNormal2.X = -this.vertexNormals[this.vertexNormals.Count - 2].Y;
                    this.cutNormal2.Y = this.vertexNormals[this.vertexNormals.Count - 2].X;

                }
                this.cutNormal1.Normalize();
                this.cutNormal2.Normalize();
            }

            this.MakeFaceUVs();

            hollowCoords = null;
            hollowNormals = null;
            hollowUs = null;

            if (calcVertexNormals)
            { // calculate prim face numbers

                // face number order is top, outer, hollow, bottom, start cut, end cut
                // I know it's ugly but so is the whole concept of prim face numbers

                int faceNum = 1; // start with outer faces
                this.outerFaceNumber = faceNum;

                int startVert = hasProfileCut && !hasHollow ? 1 : 0;
                if (startVert > 0)
                    this.faceNumbers.Add(-1);
                for (int i = 0; i < this.numOuterVerts - 1; i++)
                    this.faceNumbers.Add(sides < 5 && i <= sides ? faceNum++ : faceNum);

                this.faceNumbers.Add(hasProfileCut ? -1 : faceNum++);

                if (sides > 4 && (hasHollow || hasProfileCut))
                    faceNum++;

                if (sides < 5 && (hasHollow || hasProfileCut) && this.numOuterVerts < sides)
                    faceNum++;

                if (hasHollow)
                {
                    for (int i = 0; i < this.numHollowVerts; i++)
                        this.faceNumbers.Add(faceNum);

                    this.hollowFaceNumber = faceNum++;
                }

                this.bottomFaceNumber = faceNum++;

                if (hasHollow && hasProfileCut)
                    this.faceNumbers.Add(faceNum++);

                for (int i = 0; i < this.faceNumbers.Count; i++)
                    if (this.faceNumbers[i] == -1)
                        this.faceNumbers[i] = faceNum++;

                this.numPrimFaces = faceNum;
            }

        }

        public void MakeFaceUVs()
        {
            this.faceUVs = new List<UVCoord>();
            foreach (Coord c in this.coords)
                this.faceUVs.Add(new UVCoord(1.0f - (0.5f + c.X), 1.0f - (0.5f - c.Y)));
        }

        public Profile Copy()
        {
            return this.Copy(true);
        }

        public Profile Copy(bool needFaces)
        {
            Profile copy = new Profile();

            copy.coords.AddRange(this.coords);
            copy.faceUVs.AddRange(this.faceUVs);

            if (needFaces)
                copy.faces.AddRange(this.faces);
            if ((copy.calcVertexNormals = this.calcVertexNormals) == true)
            {
                copy.vertexNormals.AddRange(this.vertexNormals);
                copy.faceNormal = this.faceNormal;
                copy.cutNormal1 = this.cutNormal1;
                copy.cutNormal2 = this.cutNormal2;
                copy.us.AddRange(this.us);
                copy.faceNumbers.AddRange(this.faceNumbers);

                copy.cut1CoordIndices = new List<int>(this.cut1CoordIndices);
                copy.cut2CoordIndices = new List<int>(this.cut2CoordIndices);
                copy.hollowCoordIndices = new List<int>(this.hollowCoordIndices);
                copy.outerCoordIndices = new List<int>(this.outerCoordIndices);
            }
            copy.numOuterVerts = this.numOuterVerts;
            copy.numHollowVerts = this.numHollowVerts;

            return copy;
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

        public void AddRot(Quat q)
        {
            int i;
            int numVerts = this.coords.Count;

            for (i = 0; i < numVerts; i++)
                this.coords[i] *= q;

            if (this.calcVertexNormals)
            {
                int numNormals = this.vertexNormals.Count;
                for (i = 0; i < numNormals; i++)
                    this.vertexNormals[i] *= q;

                this.faceNormal *= q;
                this.cutNormal1 *= q;
                this.cutNormal2 *= q;

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

        /// <summary>
        /// Changes order of the vertex indices and negates the center vertex normal. Does not alter vertex normals of radial vertices
        /// </summary>
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

            if (this.calcVertexNormals)
            {
                int normalCount = this.vertexNormals.Count;
                if (normalCount > 0)
                {
                    Coord n = this.vertexNormals[normalCount - 1];
                    n.Z = -n.Z;
                    this.vertexNormals[normalCount - 1] = n;
                }
            }

            this.faceNormal.X = -this.faceNormal.X;
            this.faceNormal.Y = -this.faceNormal.Y;
            this.faceNormal.Z = -this.faceNormal.Z;

            int numfaceUVs = this.faceUVs.Count;
            for (i = 0; i < numfaceUVs; i++)
            {
                UVCoord uv = this.faceUVs[i];
                uv.V = 1.0f - uv.V;
                this.faceUVs[i] = uv;
            }
        }

        public void AddValue2FaceVertexIndices(int num)
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

        public void AddValue2FaceNormalIndices(int num)
        {
            if (this.calcVertexNormals)
            {
                int numFaces = this.faces.Count;
                Face tmpFace;
                for (int i = 0; i < numFaces; i++)
                {
                    tmpFace = this.faces[i];
                    tmpFace.n1 += num;
                    tmpFace.n2 += num;
                    tmpFace.n3 += num;

                    this.faces[i] = tmpFace;
                }
            }
        }

        public void DumpRaw(String path, String name, String title)
        {
            if (path == null)
                return;
            String fileName = name + "_" + title + ".raw";
            String completePath = System.IO.Path.Combine(path, fileName);
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

    public struct PathNode
    {
        public Coord position;
        public Quat rotation;
        public float xScale;
        public float yScale;
        public float percentOfPath;
    }

    public enum PathType { Linear = 0, Circular = 1, Flexible = 2 }

    public class Path
    {
        public List<PathNode> pathNodes = new List<PathNode>();

        public float twistBegin = 0.0f;
        public float twistEnd = 0.0f;
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

        private const float twoPi = 2.0f * (float)Math.PI;

        public void Create(PathType pathType, int steps)
        {
            if (this.taperX > 0.999f)
                this.taperX = 0.999f;
            if (this.taperX < -0.999f)
                this.taperX = -0.999f;
            if (this.taperY > 0.999f)
                this.taperY = 0.999f;
            if (this.taperY < -0.999f)
                this.taperY = -0.999f;

            if (pathType == PathType.Linear || pathType == PathType.Flexible)
            {
                int step = 0;

                float length = this.pathCutEnd - this.pathCutBegin;
                float twistTotal = twistEnd - twistBegin;
                float twistTotalAbs = Math.Abs(twistTotal);
                if (twistTotalAbs > 0.01f)
                    steps += (int)(twistTotalAbs * 3.66); //  dahlia's magic number

                float start = -0.5f;
                float stepSize = length / (float)steps;
                float percentOfPathMultiplier = stepSize * 0.999999f;
                float xOffset = this.topShearX * this.pathCutBegin;
                float yOffset = this.topShearY * this.pathCutBegin;
                float zOffset = start;
                float xOffsetStepIncrement = this.topShearX * length / steps;
                float yOffsetStepIncrement = this.topShearY * length / steps;

                float percentOfPath = this.pathCutBegin;
                zOffset += percentOfPath;

                // sanity checks

                bool done = false;

                while (!done)
                {
                    PathNode newNode = new PathNode();

                    newNode.xScale = 1.0f;
                    if (this.taperX == 0.0f)
                        newNode.xScale = 1.0f;
                    else if (this.taperX > 0.0f)
                        newNode.xScale = 1.0f - percentOfPath * this.taperX;
                    else newNode.xScale = 1.0f + (1.0f - percentOfPath) * this.taperX;

                    newNode.yScale = 1.0f;
                    if (this.taperY == 0.0f)
                        newNode.yScale = 1.0f;
                    else if (this.taperY > 0.0f)
                        newNode.yScale = 1.0f - percentOfPath * this.taperY;
                    else newNode.yScale = 1.0f + (1.0f - percentOfPath) * this.taperY;

                    float twist = twistBegin + twistTotal * percentOfPath;

                    newNode.rotation = new Quat(new Coord(0.0f, 0.0f, 1.0f), twist);
                    newNode.position = new Coord(xOffset, yOffset, zOffset);
                    newNode.percentOfPath = percentOfPath;

                    pathNodes.Add(newNode);

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
            } // end of linear path code

            else // pathType == Circular
            {
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
                float xOffsetTopShearXFactor = this.topShearX * (0.25f + 0.5f * (0.5f - this.holeSizeY));
                float yShearCompensation = 1.0f + Math.Abs(this.topShearY) * 0.25f;

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

                int step = (int)(startAngle / stepSize);
                float angle = startAngle;

                bool done = false;
                while (!done) // loop through the length of the path and add the layers
                {
                    PathNode newNode = new PathNode();

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

                    newNode.xScale = xProfileScale;
                    newNode.yScale = yProfileScale;

                    float radiusScale = 1.0f;
                    if (this.radius > 0.001f)
                        radiusScale = 1.0f - this.radius * percentOfPath;
                    else if (this.radius < 0.001f)
                        radiusScale = 1.0f + this.radius * (1.0f - percentOfPath);

                    float twist = twistBegin + twistTotal * percentOfPath;

                    float xOffset = 0.5f * (skewStart + totalSkew * percentOfAngles);
                    xOffset += (float)Math.Sin(angle) * xOffsetTopShearXFactor;

                    float yOffset = yShearCompensation * (float)Math.Cos(angle) * (0.5f - yPathScale) * radiusScale;

                    float zOffset = (float)Math.Sin(angle + this.topShearY) * (0.5f - yPathScale) * radiusScale;

                    newNode.position = new Coord(xOffset, yOffset, zOffset);

                    // now orient the rotation of the profile layer relative to it's position on the path
                    // adding taperY to the angle used to generate the quat appears to approximate the viewer

                    newNode.rotation = new Quat(new Coord(1.0f, 0.0f, 0.0f), angle + this.topShearY);

                    // next apply twist rotation to the profile layer
                    if (twistTotal != 0.0f || twistBegin != 0.0f)
                        newNode.rotation *= new Quat(new Coord(0.0f, 0.0f, 1.0f), twist);

                    newNode.percentOfPath = percentOfPath;

                    pathNodes.Add(newNode);

                    // calculate terms for next iteration
                    // calculate the angle for the next iteration of the loop

                    if (angle >= endAngle - 0.01)
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
        }
    }

    public class PrimMesh
    {
        public string errorMessage = "";
        private const float twoPi = 2.0f * (float)Math.PI;

        public List<Coord> coords;
        public List<Coord> normals;
        public List<Face> faces;

        public List<ViewerFace> viewerFaces;

        private int sides = 4;
        private int hollowSides = 4;
        private float profileStart = 0.0f;
        private float profileEnd = 1.0f;
        private float hollow = 0.0f;
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

        private int profileOuterFaceNumber = -1;
        private int profileHollowFaceNumber = -1;

        private bool hasProfileCut = false;
        private bool hasHollow = false;
        public bool calcVertexNormals = false;
        private bool normalsProcessed = false;
        public bool viewerMode = false;
        public bool sphereMode = false;

        public int numPrimFaces = 0;

        /// <summary>
        /// Human readable string representation of the parameters used to create a mesh.
        /// </summary>
        /// <returns></returns>
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
            s += "\nsphereMode...........: " + this.sphereMode.ToString();
            s += "\nhasProfileCut........: " + this.hasProfileCut.ToString();
            s += "\nhasHollow............: " + this.hasHollow.ToString();
            s += "\nviewerMode...........: " + this.viewerMode.ToString();

            return s;
        }

        public int ProfileOuterFaceNumber
        {
            get { return profileOuterFaceNumber; }
        }

        public int ProfileHollowFaceNumber
        {
            get { return profileHollowFaceNumber; }
        }

        public bool HasProfileCut
        {
            get { return hasProfileCut; }
        }

        public bool HasHollow
        {
            get { return hasHollow; }
        }


        /// <summary>
        /// Constructs a PrimMesh object and creates the profile for extrusion.
        /// </summary>
        /// <param name="sides"></param>
        /// <param name="profileStart"></param>
        /// <param name="profileEnd"></param>
        /// <param name="hollow"></param>
        /// <param name="hollowSides"></param>
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
            if (hollowSides < 3)
                this.hollowSides = 3;
            if (profileStart < 0.0f)
                this.profileStart = 0.0f;
            if (profileEnd > 1.0f)
                this.profileEnd = 1.0f;
            if (profileEnd < 0.02f)
                this.profileEnd = 0.02f;
            if (profileStart >= profileEnd)
                this.profileStart = profileEnd - 0.02f;
            if (hollow > 0.99f)
                this.hollow = 0.99f;
            if (hollow < 0.0f)
                this.hollow = 0.0f;
        }

        /// <summary>
        /// Extrudes a profile along a path.
        /// </summary>
        public void Extrude(PathType pathType)
        {
            bool needEndFaces = false;

            this.coords = new List<Coord>();
            this.faces = new List<Face>();

            if (this.viewerMode)
            {
                this.viewerFaces = new List<ViewerFace>();
                this.calcVertexNormals = true;
            }

            if (this.calcVertexNormals)
                this.normals = new List<Coord>();

            int steps = 1;

            float length = this.pathCutEnd - this.pathCutBegin;
            normalsProcessed = false;

            if (this.viewerMode && this.sides == 3)
            {
                // prisms don't taper well so add some vertical resolution
                // other prims may benefit from this but just do prisms for now
                if (Math.Abs(this.taperX) > 0.01 || Math.Abs(this.taperY) > 0.01)
                    steps = (int)(steps * 4.5 * length);
            }

            if (this.sphereMode)
                this.hasProfileCut = this.profileEnd - this.profileStart < 0.4999f;
            else
                this.hasProfileCut = this.profileEnd - this.profileStart < 0.9999f;
            this.hasHollow = (this.hollow > 0.001f);

            float twistBegin = this.twistBegin / 360.0f * twoPi;
            float twistEnd = this.twistEnd / 360.0f * twoPi;
            float twistTotal = twistEnd - twistBegin;
            float twistTotalAbs = Math.Abs(twistTotal);
            if (twistTotalAbs > 0.01f)
                steps += (int)(twistTotalAbs * 3.66); //  dahlia's magic number

            float hollow = this.hollow;

            if (pathType == PathType.Circular)
            {
                needEndFaces = false;
                if (this.pathCutBegin != 0.0f || this.pathCutEnd != 1.0f)
                    needEndFaces = true;
                else if (this.taperX != 0.0f || this.taperY != 0.0f)
                    needEndFaces = true;
                else if (this.skew != 0.0f)
                    needEndFaces = true;
                else if (twistTotal != 0.0f)
                    needEndFaces = true;
                else if (this.radius != 0.0f)
                    needEndFaces = true;
            }
            else needEndFaces = true;

            // sanity checks
            float initialProfileRot = 0.0f;
            if (pathType == PathType.Circular)
            {
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
            }
            else
            {
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
                else if (this.sides == 24 && this.hollowSides == 4)
                    hollow *= 1.414f;
            }

            Profile profile = new Profile(this.sides, this.profileStart, this.profileEnd, hollow, this.hollowSides, true, calcVertexNormals);
            this.errorMessage = profile.errorMessage;

            this.numPrimFaces = profile.numPrimFaces;

            int cut1FaceNumber = profile.bottomFaceNumber + 1;
            int cut2FaceNumber = cut1FaceNumber + 1;
            if (!needEndFaces)
            {
                cut1FaceNumber -= 2;
                cut2FaceNumber -= 2;
            }

            profileOuterFaceNumber = profile.outerFaceNumber;
            if (!needEndFaces)
                profileOuterFaceNumber--;

            if (hasHollow)
            {
                profileHollowFaceNumber = profile.hollowFaceNumber;
                if (!needEndFaces)
                    profileHollowFaceNumber--;
            }

            int cut1Vert = -1;
            int cut2Vert = -1;
            if (hasProfileCut)
            {
                cut1Vert = hasHollow ? profile.coords.Count - 1 : 0;
                cut2Vert = hasHollow ? profile.numOuterVerts - 1 : profile.numOuterVerts;
            }

            if (initialProfileRot != 0.0f)
            {
                profile.AddRot(new Quat(new Coord(0.0f, 0.0f, 1.0f), initialProfileRot));
                if (viewerMode)
                    profile.MakeFaceUVs();
            }

            Coord lastCutNormal1 = new Coord();
            Coord lastCutNormal2 = new Coord();
            float thisV = 0.0f;
            float lastV = 0.0f;

            Path path = new Path();
            path.twistBegin = twistBegin;
            path.twistEnd = twistEnd;
            path.topShearX = topShearX;
            path.topShearY = topShearY;
            path.pathCutBegin = pathCutBegin;
            path.pathCutEnd = pathCutEnd;
            path.dimpleBegin = dimpleBegin;
            path.dimpleEnd = dimpleEnd;
            path.skew = skew;
            path.holeSizeX = holeSizeX;
            path.holeSizeY = holeSizeY;
            path.taperX = taperX;
            path.taperY = taperY;
            path.radius = radius;
            path.revolutions = revolutions;
            path.stepsPerRevolution = stepsPerRevolution;

            path.Create(pathType, steps);

            for (int nodeIndex = 0; nodeIndex < path.pathNodes.Count; nodeIndex++)
            {
                PathNode node = path.pathNodes[nodeIndex];
                Profile newLayer = profile.Copy();
                newLayer.Scale(node.xScale, node.yScale);

                newLayer.AddRot(node.rotation);
                newLayer.AddPos(node.position);

                if (needEndFaces && nodeIndex == 0)
                {
                    newLayer.FlipNormals();

                    // add the bottom faces to the viewerFaces list
                    if (this.viewerMode)
                    {
                        Coord faceNormal = newLayer.faceNormal;
                        ViewerFace newViewerFace = new ViewerFace(profile.bottomFaceNumber);
                        int numFaces = newLayer.faces.Count;
                        List<Face> faces = newLayer.faces;

                        for (int i = 0; i < numFaces; i++)
                        {
                            Face face = faces[i];
                            newViewerFace.v1 = newLayer.coords[face.v1];
                            newViewerFace.v2 = newLayer.coords[face.v2];
                            newViewerFace.v3 = newLayer.coords[face.v3];

                            newViewerFace.coordIndex1 = face.v1;
                            newViewerFace.coordIndex2 = face.v2;
                            newViewerFace.coordIndex3 = face.v3;

                            newViewerFace.n1 = faceNormal;
                            newViewerFace.n2 = faceNormal;
                            newViewerFace.n3 = faceNormal;

                            newViewerFace.uv1 = newLayer.faceUVs[face.v1];
                            newViewerFace.uv2 = newLayer.faceUVs[face.v2];
                            newViewerFace.uv3 = newLayer.faceUVs[face.v3];

                            if (pathType == PathType.Linear)
                            {
                                newViewerFace.uv1.Flip();
                                newViewerFace.uv2.Flip();
                                newViewerFace.uv3.Flip();
                            }

                            this.viewerFaces.Add(newViewerFace);
                        }
                    }
                } // if (nodeIndex == 0)

                // append this layer

                int coordsLen = this.coords.Count;
                newLayer.AddValue2FaceVertexIndices(coordsLen);

                this.coords.AddRange(newLayer.coords);

                if (this.calcVertexNormals)
                {
                    newLayer.AddValue2FaceNormalIndices(this.normals.Count);
                    this.normals.AddRange(newLayer.vertexNormals);
                }

                if (node.percentOfPath < this.pathCutBegin + 0.01f || node.percentOfPath > this.pathCutEnd - 0.01f)
                    this.faces.AddRange(newLayer.faces);

                // fill faces between layers

                int numVerts = newLayer.coords.Count;
                Face newFace1 = new Face();
                Face newFace2 = new Face();

                thisV = 1.0f - node.percentOfPath;

                if (nodeIndex > 0)
                {
                    int startVert = coordsLen + 1;
                    int endVert = this.coords.Count;

                    if (sides < 5 || this.hasProfileCut || this.hasHollow)
                        startVert--;

                    for (int i = startVert; i < endVert; i++)
                    {
                        int iNext = i + 1;
                        if (i == endVert - 1)
                            iNext = startVert;

                        int whichVert = i - startVert;

                        newFace1.v1 = i;
                        newFace1.v2 = i - numVerts;
                        newFace1.v3 = iNext;

                        newFace1.n1 = newFace1.v1;
                        newFace1.n2 = newFace1.v2;
                        newFace1.n3 = newFace1.v3;
                        this.faces.Add(newFace1);

                        newFace2.v1 = iNext;
                        newFace2.v2 = i - numVerts;
                        newFace2.v3 = iNext - numVerts;

                        newFace2.n1 = newFace2.v1;
                        newFace2.n2 = newFace2.v2;
                        newFace2.n3 = newFace2.v3;
                        this.faces.Add(newFace2);

                        if (this.viewerMode)
                        {
                            // add the side faces to the list of viewerFaces here

                            int primFaceNum = profile.faceNumbers[whichVert];
                            if (!needEndFaces)
                                primFaceNum -= 1;

                            ViewerFace newViewerFace1 = new ViewerFace(primFaceNum);
                            ViewerFace newViewerFace2 = new ViewerFace(primFaceNum);

                            int uIndex = whichVert;
                            if (!hasHollow && sides > 4 && uIndex < newLayer.us.Count - 1)
                            {
                                uIndex++;
                            }

                            float u1 = newLayer.us[uIndex];
                            float u2 = 1.0f;
                            if (uIndex < (int)newLayer.us.Count - 1)
                                u2 = newLayer.us[uIndex + 1];

                            if (whichVert == cut1Vert || whichVert == cut2Vert)
                            {
                                u1 = 0.0f;
                                u2 = 1.0f;
                            }
                            else if (sides < 5)
                            {
                                if (whichVert < profile.numOuterVerts)
                                { // boxes and prisms have one texture face per side of the prim, so the U values have to be scaled
                                    // to reflect the entire texture width
                                    u1 *= sides;
                                    u2 *= sides;
                                    u2 -= (int)u1;
                                    u1 -= (int)u1;
                                    if (u2 < 0.1f)
                                        u2 = 1.0f;
                                }
                            }

                            if (this.sphereMode)
                            {
                                if (whichVert != cut1Vert && whichVert != cut2Vert)
                                {
                                    u1 = u1 * 2.0f - 1.0f;
                                    u2 = u2 * 2.0f - 1.0f;

                                    if (whichVert >= newLayer.numOuterVerts)
                                    {
                                        u1 -= hollow;
                                        u2 -= hollow;
                                    }

                                }
                            }

                            newViewerFace1.uv1.U = u1;
                            newViewerFace1.uv2.U = u1;
                            newViewerFace1.uv3.U = u2;

                            newViewerFace1.uv1.V = thisV;
                            newViewerFace1.uv2.V = lastV;
                            newViewerFace1.uv3.V = thisV;

                            newViewerFace2.uv1.U = u2;
                            newViewerFace2.uv2.U = u1;
                            newViewerFace2.uv3.U = u2;

                            newViewerFace2.uv1.V = thisV;
                            newViewerFace2.uv2.V = lastV;
                            newViewerFace2.uv3.V = lastV;

                            newViewerFace1.v1 = this.coords[newFace1.v1];
                            newViewerFace1.v2 = this.coords[newFace1.v2];
                            newViewerFace1.v3 = this.coords[newFace1.v3];

                            newViewerFace2.v1 = this.coords[newFace2.v1];
                            newViewerFace2.v2 = this.coords[newFace2.v2];
                            newViewerFace2.v3 = this.coords[newFace2.v3];

                            newViewerFace1.coordIndex1 = newFace1.v1;
                            newViewerFace1.coordIndex2 = newFace1.v2;
                            newViewerFace1.coordIndex3 = newFace1.v3;

                            newViewerFace2.coordIndex1 = newFace2.v1;
                            newViewerFace2.coordIndex2 = newFace2.v2;
                            newViewerFace2.coordIndex3 = newFace2.v3;

                            // profile cut faces
                            if (whichVert == cut1Vert)
                            {
                                newViewerFace1.primFaceNumber = cut1FaceNumber;
                                newViewerFace2.primFaceNumber = cut1FaceNumber;
                                newViewerFace1.n1 = newLayer.cutNormal1;
                                newViewerFace1.n2 = newViewerFace1.n3 = lastCutNormal1;

                                newViewerFace2.n1 = newViewerFace2.n3 = newLayer.cutNormal1;
                                newViewerFace2.n2 = lastCutNormal1;
                            }
                            else if (whichVert == cut2Vert)
                            {
                                newViewerFace1.primFaceNumber = cut2FaceNumber;
                                newViewerFace2.primFaceNumber = cut2FaceNumber;
                                newViewerFace1.n1 = newLayer.cutNormal2;
                                newViewerFace1.n2 = lastCutNormal2;
                                newViewerFace1.n3 = lastCutNormal2;

                                newViewerFace2.n1 = newLayer.cutNormal2;
                                newViewerFace2.n3 = newLayer.cutNormal2;
                                newViewerFace2.n2 = lastCutNormal2;
                            }

                            else // outer and hollow faces
                            {
                                if ((sides < 5 && whichVert < newLayer.numOuterVerts) || (hollowSides < 5 && whichVert >= newLayer.numOuterVerts))
                                { // looks terrible when path is twisted... need vertex normals here
                                    newViewerFace1.CalcSurfaceNormal();
                                    newViewerFace2.CalcSurfaceNormal();
                                }
                                else
                                {
                                    newViewerFace1.n1 = this.normals[newFace1.n1];
                                    newViewerFace1.n2 = this.normals[newFace1.n2];
                                    newViewerFace1.n3 = this.normals[newFace1.n3];

                                    newViewerFace2.n1 = this.normals[newFace2.n1];
                                    newViewerFace2.n2 = this.normals[newFace2.n2];
                                    newViewerFace2.n3 = this.normals[newFace2.n3];
                                }
                            }

                            this.viewerFaces.Add(newViewerFace1);
                            this.viewerFaces.Add(newViewerFace2);

                        }
                    }
                }

                lastCutNormal1 = newLayer.cutNormal1;
                lastCutNormal2 = newLayer.cutNormal2;
                lastV = thisV;

                if (needEndFaces && nodeIndex == path.pathNodes.Count - 1 && viewerMode)
                {
                    // add the top faces to the viewerFaces list here
                    Coord faceNormal = newLayer.faceNormal;
                    ViewerFace newViewerFace = new ViewerFace(0);
                    int numFaces = newLayer.faces.Count;
                    List<Face> faces = newLayer.faces;

                    for (int i = 0; i < numFaces; i++)
                    {
                        Face face = faces[i];
                        newViewerFace.v1 = newLayer.coords[face.v1 - coordsLen];
                        newViewerFace.v2 = newLayer.coords[face.v2 - coordsLen];
                        newViewerFace.v3 = newLayer.coords[face.v3 - coordsLen];

                        newViewerFace.coordIndex1 = face.v1 - coordsLen;
                        newViewerFace.coordIndex2 = face.v2 - coordsLen;
                        newViewerFace.coordIndex3 = face.v3 - coordsLen;

                        newViewerFace.n1 = faceNormal;
                        newViewerFace.n2 = faceNormal;
                        newViewerFace.n3 = faceNormal;

                        newViewerFace.uv1 = newLayer.faceUVs[face.v1 - coordsLen];
                        newViewerFace.uv2 = newLayer.faceUVs[face.v2 - coordsLen];
                        newViewerFace.uv3 = newLayer.faceUVs[face.v3 - coordsLen];

                        if (pathType == PathType.Linear)
                        {
                            newViewerFace.uv1.Flip();
                            newViewerFace.uv2.Flip();
                            newViewerFace.uv3.Flip();
                        }

                        this.viewerFaces.Add(newViewerFace);
                    }
                }


            } // for (int nodeIndex = 0; nodeIndex < path.pathNodes.Count; nodeIndex++)

        }


        /// <summary>
        /// DEPRICATED - use Extrude(PathType.Linear) instead
        /// Extrudes a profile along a straight line path. Used for prim types box, cylinder, and prism.
        /// </summary>
        /// 
        public void ExtrudeLinear()
        {
            this.Extrude(PathType.Linear);
        }


        /// <summary>
        /// DEPRICATED - use Extrude(PathType.Circular) instead
        /// Extrude a profile into a circular path prim mesh. Used for prim types torus, tube, and ring.
        /// </summary>
        /// 
        public void ExtrudeCircular()
        {
            this.Extrude(PathType.Circular);
        }


        private Coord SurfaceNormal(Coord c1, Coord c2, Coord c3)
        {
            Coord edge1 = new Coord(c2.X - c1.X, c2.Y - c1.Y, c2.Z - c1.Z);
            Coord edge2 = new Coord(c3.X - c1.X, c3.Y - c1.Y, c3.Z - c1.Z);

            Coord normal = Coord.Cross(edge1, edge2);

            normal.Normalize();

            return normal;
        }

        private Coord SurfaceNormal(Face face)
        {
            return SurfaceNormal(this.coords[face.v1], this.coords[face.v2], this.coords[face.v3]);
        }

        /// <summary>
        /// Calculate the surface normal for a face in the list of faces
        /// </summary>
        /// <param name="faceIndex"></param>
        /// <returns></returns>
        public Coord SurfaceNormal(int faceIndex)
        {
            int numFaces = this.faces.Count;
            if (faceIndex < 0 || faceIndex >= numFaces)
                throw new Exception("faceIndex out of range");

            return SurfaceNormal(this.faces[faceIndex]);
        }

        /// <summary>
        /// Duplicates a PrimMesh object. All object properties are copied by value, including lists.
        /// </summary>
        /// <returns></returns>
        public PrimMesh Copy()
        {
            PrimMesh copy = new PrimMesh(this.sides, this.profileStart, this.profileEnd, this.hollow, this.hollowSides);
            copy.twistBegin = this.twistBegin;
            copy.twistEnd = this.twistEnd;
            copy.topShearX = this.topShearX;
            copy.topShearY = this.topShearY;
            copy.pathCutBegin = this.pathCutBegin;
            copy.pathCutEnd = this.pathCutEnd;
            copy.dimpleBegin = this.dimpleBegin;
            copy.dimpleEnd = this.dimpleEnd;
            copy.skew = this.skew;
            copy.holeSizeX = this.holeSizeX;
            copy.holeSizeY = this.holeSizeY;
            copy.taperX = this.taperX;
            copy.taperY = this.taperY;
            copy.radius = this.radius;
            copy.revolutions = this.revolutions;
            copy.stepsPerRevolution = this.stepsPerRevolution;
            copy.calcVertexNormals = this.calcVertexNormals;
            copy.normalsProcessed = this.normalsProcessed;
            copy.viewerMode = this.viewerMode;
            copy.numPrimFaces = this.numPrimFaces;
            copy.errorMessage = this.errorMessage;

            copy.coords = new List<Coord>(this.coords);
            copy.faces = new List<Face>(this.faces);
            copy.viewerFaces = new List<ViewerFace>(this.viewerFaces);
            copy.normals = new List<Coord>(this.normals);

            return copy;
        }

        /// <summary>
        /// Calculate surface normals for all of the faces in the list of faces in this mesh
        /// </summary>
        public void CalcNormals()
        {
            if (normalsProcessed)
                return;

            normalsProcessed = true;

            int numFaces = faces.Count;

            if (!this.calcVertexNormals)
                this.normals = new List<Coord>();

            for (int i = 0; i < numFaces; i++)
            {
                Face face = faces[i];

                this.normals.Add(SurfaceNormal(i).Normalize());

                int normIndex = normals.Count - 1;
                face.n1 = normIndex;
                face.n2 = normIndex;
                face.n3 = normIndex;

                this.faces[i] = face;
            }
        }

        /// <summary>
        /// Adds a value to each XYZ vertex coordinate in the mesh
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
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

            if (this.viewerFaces != null)
            {
                int numViewerFaces = this.viewerFaces.Count;

                for (i = 0; i < numViewerFaces; i++)
                {
                    ViewerFace v = this.viewerFaces[i];
                    v.AddPos(x, y, z);
                    this.viewerFaces[i] = v;
                }
            }
        }

        /// <summary>
        /// Rotates the mesh
        /// </summary>
        /// <param name="q"></param>
        public void AddRot(Quat q)
        {
            int i;
            int numVerts = this.coords.Count;

            for (i = 0; i < numVerts; i++)
                this.coords[i] *= q;

            if (this.normals != null)
            {
                int numNormals = this.normals.Count;
                for (i = 0; i < numNormals; i++)
                    this.normals[i] *= q;
            }

            if (this.viewerFaces != null)
            {
                int numViewerFaces = this.viewerFaces.Count;

                for (i = 0; i < numViewerFaces; i++)
                {
                    ViewerFace v = this.viewerFaces[i];
                    v.v1 *= q;
                    v.v2 *= q;
                    v.v3 *= q;

                    v.n1 *= q;
                    v.n2 *= q;
                    v.n3 *= q;
                    this.viewerFaces[i] = v;
                }
            }
        }

#if VERTEX_INDEXER
        public VertexIndexer GetVertexIndexer()
        {
            if (this.viewerMode && this.viewerFaces.Count > 0)
                return new VertexIndexer(this);
            return null;
        }
#endif

        /// <summary>
        /// Scales the mesh
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public void Scale(float x, float y, float z)
        {
            int i;
            int numVerts = this.coords.Count;
            //Coord vert;

            Coord m = new Coord(x, y, z);
            for (i = 0; i < numVerts; i++)
                this.coords[i] *= m;

            if (this.viewerFaces != null)
            {
                int numViewerFaces = this.viewerFaces.Count;
                for (i = 0; i < numViewerFaces; i++)
                {
                    ViewerFace v = this.viewerFaces[i];
                    v.v1 *= m;
                    v.v2 *= m;
                    v.v3 *= m;
                    this.viewerFaces[i] = v;
                }

            }

        }

        /// <summary>
        /// Dumps the mesh to a Blender compatible "Raw" format file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="name"></param>
        /// <param name="title"></param>
        public void DumpRaw(String path, String name, String title)
        {
            if (path == null)
                return;
            String fileName = name + "_" + title + ".raw";
            String completePath = System.IO.Path.Combine(path, fileName);
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
