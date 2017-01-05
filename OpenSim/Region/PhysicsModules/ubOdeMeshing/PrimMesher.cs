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

    public struct Face
    {
        public int primFace;

        // vertices
        public int v1;
        public int v2;
        public int v3;

        public Face(int v1, int v2, int v3)
        {
            primFace = 0;

            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;
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

        private static Angle[] angles4 =
        {
            new Angle(0.0f, 1.0f, 0.0f),
            new Angle(0.25f, 0.0f, 1.0f),
            new Angle(0.5f, -1.0f, 0.0f),
            new Angle(0.75f, 0.0f, -1.0f),
            new Angle(1.0f, 1.0f, 0.0f)
        };

        private static Angle[] angles6 =
        {
            new Angle(0.0f, 1.0f, 0.0f),
            new Angle(0.16666666666666667f, 0.5f, 0.8660254037844386f),
            new Angle(0.33333333333333333f, -0.5f, 0.86602540378443871f),
            new Angle(0.5f, -1.0f, 0.0f),
            new Angle(0.66666666666666667f, -0.5f, -0.86602540378443837f),
            new Angle(0.83333333333333326f, 0.5f, -0.86602540378443904f),
            new Angle(1.0f, 1.0f, 0.0f)
        };

        private static Angle[] angles12 =
        {
            new Angle(0.0f, 1.0f, 0.0f),
            new Angle(0.083333333333333329f, 0.86602540378443871f, 0.5f),
            new Angle(0.16666666666666667f, 0.5f, 0.8660254037844386f),
            new Angle(0.25f, 0.0f, 1.0f),
            new Angle(0.33333333333333333f, -0.5f, 0.86602540378443871f),
            new Angle(0.41666666666666663f, -0.86602540378443849f, 0.5f),
            new Angle(0.5f, -1.0f, 0.0f),
            new Angle(0.58333333333333326f, -0.86602540378443882f, -0.5f),
            new Angle(0.66666666666666667f, -0.5f, -0.86602540378443837f),
            new Angle(0.75f, 0.0f, -1.0f),
            new Angle(0.83333333333333326f, 0.5f, -0.86602540378443904f),
            new Angle(0.91666666666666663f, 0.86602540378443837f, -0.5f),
            new Angle(1.0f, 1.0f, 0.0f)
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

        internal void makeAngles(int sides, float startAngle, float stopAngle, bool hasCut)
        {
            angles = new List<Angle>();

            const double twoPi = System.Math.PI * 2.0;
            const float twoPiInv = (float)(1.0d / twoPi);

            if (sides < 1)
                throw new Exception("number of sides not greater than zero");
            if (stopAngle <= startAngle)
                throw new Exception("stopAngle not greater than startAngle");

            if ((sides == 3 || sides == 4 || sides == 6 || sides == 12 || sides == 24))
            {
                startAngle *= twoPiInv;
                stopAngle *= twoPiInv;

                Angle[] sourceAngles;
                switch (sides)
                {
                    case 3:
                        sourceAngles = angles3;
                        break;
                    case 4:
                        sourceAngles = angles4;
                        break;
                    case 6:
                        sourceAngles = angles6;
                        break;
                    case 12:
                        sourceAngles = angles12;
                        break;
                    default:
                        sourceAngles = angles24;
                        break;
                }

                int startAngleIndex = (int)(startAngle * sides);
                int endAngleIndex = sourceAngles.Length - 1;

                if (hasCut)
                {
                    if (stopAngle < 1.0f)
                        endAngleIndex = (int)(stopAngle * sides) + 1;
                    if (endAngleIndex == startAngleIndex)
                        endAngleIndex++;

                    for (int angleIndex = startAngleIndex; angleIndex < endAngleIndex + 1; angleIndex++)
                    {
                        angles.Add(sourceAngles[angleIndex]);
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
                    for (int angleIndex = startAngleIndex; angleIndex < endAngleIndex; angleIndex++)
                        angles.Add(sourceAngles[angleIndex]);
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

        // use these for making individual meshes for each prim face
        public List<int> outerCoordIndices = null;
        public List<int> hollowCoordIndices = null;

        public int numOuterVerts = 0;
        public int numHollowVerts = 0;

        public int outerFaceNumber = -1;
        public int hollowFaceNumber = -1;

        public int bottomFaceNumber = 0;
        public int numPrimFaces = 0;

        public Profile()
        {
            coords = new List<Coord>();
            faces = new List<Face>();
        }

        public Profile(int sides, float profileStart, float profileEnd, float hollow, int hollowSides, bool hasProfileCut, bool createFaces)
        {
            const float halfSqr2 = 0.7071067811866f;

            coords = new List<Coord>();
            faces = new List<Face>();

            List<Coord> hollowCoords = new List<Coord>();

            bool hasHollow = (hollow > 0.0f);

            AngleList angles = new AngleList();
            AngleList hollowAngles = new AngleList();

            float xScale = 0.5f;
            float yScale = 0.5f;
            if (sides == 4)  // corners of a square are sqrt(2) from center
            {
                xScale = halfSqr2;
                yScale = halfSqr2;
            }

            float startAngle = profileStart * twoPi;
            float stopAngle = profileEnd * twoPi;

            try { angles.makeAngles(sides, startAngle, stopAngle,hasProfileCut); }
            catch (Exception ex)
            {

                errorMessage = "makeAngles failed: Exception: " + ex.ToString()
                + "\nsides: " + sides.ToString() + " startAngle: " + startAngle.ToString() + " stopAngle: " + stopAngle.ToString();

                return;
            }

            numOuterVerts = angles.angles.Count;

            Angle angle;
            Coord newVert = new Coord();

            // flag to create as few triangles as possible for 3 or 4 side profile
            bool simpleFace = (sides < 5 && !hasHollow && !hasProfileCut);

            if (hasHollow)
            {
                if (sides == hollowSides)
                    hollowAngles = angles;
                else
                {
                    try { hollowAngles.makeAngles(hollowSides, startAngle, stopAngle, hasProfileCut); }
                    catch (Exception ex)
                    {
                        errorMessage = "makeAngles failed: Exception: " + ex.ToString()
                        + "\nsides: " + sides.ToString() + " startAngle: " + startAngle.ToString() + " stopAngle: " + stopAngle.ToString();

                        return;
                    }

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
                numHollowVerts = hollowAngles.angles.Count;
            }
            else if (!simpleFace)
            {
                Coord center = new Coord(0.0f, 0.0f, 0.0f);
                this.coords.Add(center);
            }

            int numAngles = angles.angles.Count;
            bool hollowsame = (hasHollow && hollowSides == sides);

            for (int i = 0; i < numAngles; i++)
            {
                angle = angles.angles[i];
                newVert.X = angle.X * xScale;
                newVert.Y = angle.Y * yScale;
                newVert.Z = 0.0f;
                coords.Add(newVert);
                if (hollowsame)
                {
                    newVert.X *= hollow;
                    newVert.Y *= hollow;
                    hollowCoords.Add(newVert);
                }
            }

            if (hasHollow)
            {
                hollowCoords.Reverse();
                coords.AddRange(hollowCoords);

                if (createFaces)
                {
                    int numTotalVerts = numOuterVerts + numHollowVerts;

                    if (numOuterVerts == numHollowVerts)
                    {
                        Face newFace = new Face();

                        for (int coordIndex = 0; coordIndex < numOuterVerts - 1; coordIndex++)
                        {
                            newFace.v1 = coordIndex;
                            newFace.v2 = coordIndex + 1;
                            newFace.v3 = numTotalVerts - coordIndex - 1;
                            faces.Add(newFace);

                            newFace.v1 = coordIndex + 1;
                            newFace.v2 = numTotalVerts - coordIndex - 2;
                            newFace.v3 = numTotalVerts - coordIndex - 1;
                            faces.Add(newFace);
                        }
                        if (!hasProfileCut)
                        {
                            newFace.v1 = numOuterVerts - 1;
                            newFace.v2 = 0;
                            newFace.v3 = numOuterVerts;
                            faces.Add(newFace);

                            newFace.v1 = 0;
                            newFace.v2 = numTotalVerts - 1;
                            newFace.v3 = numOuterVerts;
                            faces.Add(newFace);
                        }
                    }
                    else if (numOuterVerts < numHollowVerts)
                    {
                        Face newFace = new Face();
                        int j = 0; // j is the index for outer vertices
                        int i;
                        int maxJ = numOuterVerts - 1;
                        float curHollowAngle = 0;
                        for (i = 0; i < numHollowVerts; i++) // i is the index for inner vertices
                        {
                            curHollowAngle = hollowAngles.angles[i].angle;
                            if (j < maxJ)
                            {
                                if (angles.angles[j + 1].angle - curHollowAngle < curHollowAngle - angles.angles[j].angle + 0.000001f)
                                {
                                    newFace.v1 = numTotalVerts - i - 1;
                                    newFace.v2 = j;
                                    newFace.v3 = j + 1;
                                    faces.Add(newFace);
                                    j++;
                                }
                            }
                            else
                            {
                                if (1.0f - curHollowAngle < curHollowAngle - angles.angles[j].angle + 0.000001f)
                                    break;
                            }

                            newFace.v1 = j;
                            newFace.v2 = numTotalVerts - i - 2;
                            newFace.v3 = numTotalVerts - i - 1;

                            faces.Add(newFace);
                        }

                        if (!hasProfileCut)
                        {
                            if (i == numHollowVerts)
                            {
                                newFace.v1 = numTotalVerts - numHollowVerts;
                                newFace.v2 = maxJ;
                                newFace.v3 = 0;

                                faces.Add(newFace);
                            }
                            else
                            {
                                if (1.0f - curHollowAngle < curHollowAngle - angles.angles[maxJ].angle + 0.000001f)
                                {
                                    newFace.v1 = numTotalVerts - i - 1;
                                    newFace.v2 = maxJ;
                                    newFace.v3 = 0;

                                    faces.Add(newFace);
                                }

                                for (; i < numHollowVerts - 1; i++)
                                {
                                    newFace.v1 = 0;
                                    newFace.v2 = numTotalVerts - i - 2;
                                    newFace.v3 = numTotalVerts - i - 1;

                                    faces.Add(newFace);
                                }
                            }

                            newFace.v1 = 0;
                            newFace.v2 = numTotalVerts - 1;
                            newFace.v3 = numTotalVerts - numHollowVerts;
                            faces.Add(newFace);
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
                                if (hollowAngles.angles[j + 1].angle - angles.angles[i].angle < angles.angles[i].angle - hollowAngles.angles[j].angle + 0.000001f)
                                {
                                    newFace.v1 = i;
                                    newFace.v2 = numTotalVerts - j - 2;
                                    newFace.v3 = numTotalVerts - j - 1;

                                    faces.Add(newFace);
                                    j += 1;
                                }

                            newFace.v1 = numTotalVerts - j - 1;
                            newFace.v2 = i;
                            newFace.v3 = i + 1;

                            faces.Add(newFace);
                        }

                        if (!hasProfileCut)
                        {
                            int i = numOuterVerts - 1;

                            if (hollowAngles.angles[0].angle - angles.angles[i].angle < angles.angles[i].angle - hollowAngles.angles[maxJ].angle + 0.000001f)
                            {
                                newFace.v1 = 0;
                                newFace.v2 = numTotalVerts - maxJ - 1;
                                newFace.v3 = numTotalVerts - 1;

                                faces.Add(newFace);
                            }

                            newFace.v1 = numTotalVerts - maxJ - 1;
                            newFace.v2 = i;
                            newFace.v3 = 0;

                            faces.Add(newFace);
                        }
                    }
                }

            }

            else if (createFaces)
            {
                if (simpleFace)
                {
                    if (sides == 3)
                        faces.Add(new Face(0, 1, 2));
                    else if (sides == 4)
                    {
                        faces.Add(new Face(0, 1, 2));
                        faces.Add(new Face(0, 2, 3));
                    }
                }
                else
                {
                    for (int i = 1; i < numAngles ; i++)
                    {
                        Face newFace = new Face();
                        newFace.v1 = 0;
                        newFace.v2 = i;
                        newFace.v3 = i + 1;
                        faces.Add(newFace);
                    }
                    if (!hasProfileCut)
                    {
                        Face newFace = new Face();
                        newFace.v1 = 0;
                        newFace.v2 = numAngles;
                        newFace.v3 = 1;
                        faces.Add(newFace);
                    }
                }
            }


            hollowCoords = null;
        }


        public Profile Copy()
        {
            return Copy(true);
        }

        public Profile Copy(bool needFaces)
        {
            Profile copy = new Profile();

            copy.coords.AddRange(coords);

            if (needFaces)
                copy.faces.AddRange(faces);

            copy.numOuterVerts = numOuterVerts;
            copy.numHollowVerts = numHollowVerts;

            return copy;
        }

        public void AddPos(Coord v)
        {
            this.AddPos(v.X, v.Y, v.Z);
        }

        public void AddPos(float x, float y, float z)
        {
            int i;
            int numVerts = coords.Count;
            Coord vert;

            for (i = 0; i < numVerts; i++)
            {
                vert = coords[i];
                vert.X += x;
                vert.Y += y;
                vert.Z += z;
                this.coords[i] = vert;
            }
        }

        public void AddRot(Quat q)
        {
            int i;
            int numVerts = coords.Count;

            for (i = 0; i < numVerts; i++)
                coords[i] *= q;
        }

        public void Scale(float x, float y)
        {
            int i;
            int numVerts = coords.Count;
            Coord vert;

            for (i = 0; i < numVerts; i++)
            {
                vert = coords[i];
                vert.X *= x;
                vert.X = (float)Math.Round(vert.X,5);
                vert.Y *= y;
                vert.Y = (float)Math.Round(vert.Y,5);
                coords[i] = vert;
            }

            if(x == 0f || y == 0f)
                faces = new List<Face>();
        }

        /// <summary>
        /// Changes order of the vertex indices and negates the center vertex normal. Does not alter vertex normals of radial vertices
        /// </summary>
        public void FlipNormals()
        {
            int numFaces = faces.Count;
            if(numFaces == 0)
                return;

            int i;
            Face tmpFace;
            int tmp;

            for (i = 0; i < numFaces; i++)
            {
                tmpFace = faces[i];
                tmp = tmpFace.v3;
                tmpFace.v3 = tmpFace.v1;
                tmpFace.v1 = tmp;
                faces[i] = tmpFace;
            }
        }

        public void AddValue2FaceVertexIndices(int num)
        {
            int numFaces = faces.Count;
            if(numFaces == 0)
                return;

            Face tmpFace;

            for (int i = 0; i < numFaces; i++)
            {
                tmpFace = faces[i];
                tmpFace.v1 += num;
                tmpFace.v2 += num;
                tmpFace.v3 += num;

                faces[i] = tmpFace;
            }
        }

         public void DumpRaw(String path, String name, String title)
        {
            if (path == null)
                return;
            String fileName = name + "_" + title + ".raw";
            String completePath = System.IO.Path.Combine(path, fileName);
            StreamWriter sw = new StreamWriter(completePath);

            for (int i = 0; i < faces.Count; i++)
            {
                string s = coords[faces[i].v1].ToString();
                s += " " + coords[faces[i].v2].ToString();
                s += " " + coords[faces[i].v3].ToString();

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
            if (taperX > .9999f)
                taperX = 1.0f;
            else if (taperX < -.9999f)
                taperX = -1.0f;
            if (taperY > .9999f)
                taperY = 1.0f;
            else if (taperY < -.9999f)
                taperY = -1.0f;

            if (pathType == PathType.Linear || pathType == PathType.Flexible)
            {
                int step = 0;

                float length = pathCutEnd - pathCutBegin;
                float twistTotal = twistEnd - twistBegin;
                float twistTotalAbs = Math.Abs(twistTotal);
                if (twistTotalAbs > 0.01f)
                    steps += (int)(twistTotalAbs * 3.66); //  dahlia's magic number

                float start = -0.5f;
                float stepSize = length / (float)steps;
                float percentOfPathMultiplier = stepSize * 0.999999f;
                float xOffset = topShearX * pathCutBegin;
                float yOffset = topShearY * pathCutBegin;
                float zOffset = start;
                float xOffsetStepIncrement = topShearX * length / steps;
                float yOffsetStepIncrement = topShearY * length / steps;

                float percentOfPath = pathCutBegin;
                zOffset += percentOfPath;

                // sanity checks

                bool done = false;

                while (!done)
                {
                    PathNode newNode = new PathNode();

                    newNode.xScale = 1.0f;
                    if (taperX > 0.0f)
                        newNode.xScale -= percentOfPath * taperX;
                    else if(taperX < 0.0f)
                        newNode.xScale += (1.0f - percentOfPath) * taperX;

                    newNode.yScale = 1.0f;
                    if (taperY > 0.0f)
                        newNode.yScale -= percentOfPath * taperY;
                    else if(taperY < 0.0f)
                        newNode.yScale += (1.0f - percentOfPath) * taperY;

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
                        if (percentOfPath > pathCutEnd)
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

                float yPathScale = holeSizeY * 0.5f;
                float pathLength = pathCutEnd - pathCutBegin;
                float totalSkew = skew * 2.0f * pathLength;
                float skewStart = pathCutBegin * 2.0f * skew - skew;
                float xOffsetTopShearXFactor = topShearX * (0.25f + 0.5f * (0.5f - holeSizeY));
                float yShearCompensation = 1.0f + Math.Abs(topShearY) * 0.25f;

                // It's not quite clear what pushY (Y top shear) does, but subtracting it from the start and end
                // angles appears to approximate it's effects on path cut. Likewise, adding it to the angle used
                // to calculate the sine for generating the path radius appears to approximate it's effects there
                // too, but there are some subtle differences in the radius which are noticeable as the prim size
                // increases and it may affect megaprims quite a bit. The effect of the Y top shear parameter on
                // the meshes generated with this technique appear nearly identical in shape to the same prims when
                // displayed by the viewer.

                float startAngle = (twoPi * pathCutBegin * revolutions) - topShearY * 0.9f;
                float endAngle = (twoPi * pathCutEnd * revolutions) - topShearY * 0.9f;
                float stepSize = twoPi / stepsPerRevolution;

                int step = (int)(startAngle / stepSize);
                float angle = startAngle;

                bool done = false;
                while (!done) // loop through the length of the path and add the layers
                {
                    PathNode newNode = new PathNode();

                    float xProfileScale = (1.0f - Math.Abs(skew)) * holeSizeX;
                    float yProfileScale = holeSizeY;

                    float percentOfPath = angle / (twoPi * revolutions);
                    float percentOfAngles = (angle - startAngle) / (endAngle - startAngle);

                    if (taperX > 0.01f)
                        xProfileScale *= 1.0f - percentOfPath * taperX;
                    else if (taperX < -0.01f)
                        xProfileScale *= 1.0f + (1.0f - percentOfPath) * taperX;

                    if (taperY > 0.01f)
                        yProfileScale *= 1.0f - percentOfPath * taperY;
                    else if (taperY < -0.01f)
                        yProfileScale *= 1.0f + (1.0f - percentOfPath) * taperY;

                    newNode.xScale = xProfileScale;
                    newNode.yScale = yProfileScale;

                    float radiusScale = 1.0f;
                    if (radius > 0.001f)
                        radiusScale = 1.0f - radius * percentOfPath;
                    else if (radius < 0.001f)
                        radiusScale = 1.0f + radius * (1.0f - percentOfPath);

                    float twist = twistBegin + twistTotal * percentOfPath;

                    float xOffset = 0.5f * (skewStart + totalSkew * percentOfAngles);
                    xOffset += (float)Math.Sin(angle) * xOffsetTopShearXFactor;

                    float yOffset = yShearCompensation * (float)Math.Cos(angle) * (0.5f - yPathScale) * radiusScale;

                    float zOffset = (float)Math.Sin(angle + topShearY) * (0.5f - yPathScale) * radiusScale;

                    newNode.position = new Coord(xOffset, yOffset, zOffset);

                    // now orient the rotation of the profile layer relative to it's position on the path
                    // adding taperY to the angle used to generate the quat appears to approximate the viewer

                    newNode.rotation = new Quat(new Coord(1.0f, 0.0f, 0.0f), angle + topShearY);

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
//        public List<Coord> normals;
        public List<Face> faces;

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

        private bool hasProfileCut = false;
        private bool hasHollow = false;

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
            s += "\nhasProfileCut........: " + this.hasProfileCut.ToString();
            s += "\nhasHollow............: " + this.hasHollow.ToString();

            return s;
        }

        public bool HasProfileCut
        {
            get { return hasProfileCut; }
            set { hasProfileCut = value; }
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
        /// <param name="sphereMode"></param>
        public PrimMesh(int _sides, float _profileStart, float _profileEnd, float _hollow, int _hollowSides)
        {
            coords = new List<Coord>();
            faces = new List<Face>();

            sides = _sides;
            profileStart = _profileStart;
            profileEnd = _profileEnd;
            hollow = _hollow;
            hollowSides = _hollowSides;

            if (sides < 3)
                sides = 3;
            if (hollowSides < 3)
                hollowSides = 3;
            if (profileStart < 0.0f)
                profileStart = 0.0f;
            if (profileEnd > 1.0f)
                profileEnd = 1.0f;
            if (profileEnd < 0.02f)
                profileEnd = 0.02f;
            if (profileStart >= profileEnd)
                profileStart = profileEnd - 0.02f;
            if (hollow > 0.99f)
                hollow = 0.99f;
            if (hollow < 0.0f)
                hollow = 0.0f;
        }

        /// <summary>
        /// Extrudes a profile along a path.
        /// </summary>
        public void Extrude(PathType pathType)
        {
            bool needEndFaces = false;

            coords = new List<Coord>();
            faces = new List<Face>();

            int steps = 1;

            float length = pathCutEnd - pathCutBegin;

            hasProfileCut = this.profileEnd - this.profileStart < 0.9999f;

            hasHollow = (this.hollow > 0.001f);

            float twistBegin = this.twistBegin / 360.0f * twoPi;
            float twistEnd = this.twistEnd / 360.0f * twoPi;
            float twistTotal = twistEnd - twistBegin;
            float twistTotalAbs = Math.Abs(twistTotal);
            if (twistTotalAbs > 0.01f)
                steps += (int)(twistTotalAbs * 3.66); //  dahlia's magic number

            float hollow = this.hollow;
            float initialProfileRot = 0.0f;

            if (pathType == PathType.Circular)
            {
                needEndFaces = false;
                if (pathCutBegin != 0.0f || pathCutEnd != 1.0f)
                    needEndFaces = true;
                else if (taperX != 0.0f || taperY != 0.0f)
                    needEndFaces = true;
                else if (skew != 0.0f)
                    needEndFaces = true;
                else if (twistTotal != 0.0f)
                    needEndFaces = true;
                else if (radius != 0.0f)
                    needEndFaces = true;
            }
            else needEndFaces = true;

            if (pathType == PathType.Circular)
            {
                if (sides == 3)
                {
                    initialProfileRot = (float)Math.PI;
                    if (hollowSides == 4)
                    {
                        if (hollow > 0.7f)
                            hollow = 0.7f;
                        hollow *= 0.707f;
                    }
                    else hollow *= 0.5f;
                }
                else if (sides == 4)
                {
                    initialProfileRot = 0.25f * (float)Math.PI;
                    if (hollowSides != 4)
                        hollow *= 0.707f;
                }
                else if (sides > 4)
                {
                    initialProfileRot = (float)Math.PI;
                    if (hollowSides == 4)
                    {
                        if (hollow > 0.7f)
                            hollow = 0.7f;
                        hollow /= 0.7f;
                    }
                }
            }
            else
            {
                if (sides == 3)
                {
                    if (hollowSides == 4)
                    {
                        if (hollow > 0.7f)
                            hollow = 0.7f;
                        hollow *= 0.707f;
                    }
                    else hollow *= 0.5f;
                }
                else if (sides == 4)
                {
                    initialProfileRot = 1.25f * (float)Math.PI;
                    if (hollowSides != 4)
                        hollow *= 0.707f;
                }
                else if (sides == 24 && hollowSides == 4)
                    hollow *= 1.414f;
            }

            Profile profile = new Profile(sides, profileStart, profileEnd, hollow, hollowSides,
                                HasProfileCut,true);
            errorMessage = profile.errorMessage;

            numPrimFaces = profile.numPrimFaces;

            if (initialProfileRot != 0.0f)
            {
                profile.AddRot(new Quat(new Coord(0.0f, 0.0f, 1.0f), initialProfileRot));
            }

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

            int lastNode = path.pathNodes.Count - 1;

            for (int nodeIndex = 0; nodeIndex < path.pathNodes.Count; nodeIndex++)
            {
                PathNode node = path.pathNodes[nodeIndex];
                Profile newLayer = profile.Copy();

                newLayer.Scale(node.xScale, node.yScale);
                newLayer.AddRot(node.rotation);
                newLayer.AddPos(node.position);

                // append this layer
                int coordsStart = coords.Count;
                coords.AddRange(newLayer.coords);

                if (needEndFaces && nodeIndex == 0 && newLayer.faces.Count > 0)
                {
                    newLayer.AddValue2FaceVertexIndices(coordsStart);
                    newLayer.FlipNormals();
                    faces.AddRange(newLayer.faces);
                }

                // fill faces between layers

                List<Face> linkfaces = new List<Face>();
                int numVerts = newLayer.coords.Count;
                Face newFace1 = new Face();
                Face newFace2 = new Face();

                if (nodeIndex > 0)
                {
                    int startVert = coordsStart;
                    int endVert = coords.Count;
                    if (!hasProfileCut)
                    {
                        if(numVerts > 5 && !hasHollow)
                            startVert++;
                        int i = startVert;
                        for (int l = 0; l < profile.numOuterVerts - 1; l++)
                        {
                            newFace1.v1 = i;
                            newFace1.v2 = i - numVerts;
                            newFace1.v3 = i + 1;
                            linkfaces.Add(newFace1);

                            newFace2.v1 = i + 1;
                            newFace2.v2 = i - numVerts;
                            newFace2.v3 = i + 1 - numVerts;
                            linkfaces.Add(newFace2);
                            i++;
                        }

                        newFace1.v1 = i;
                        newFace1.v2 = i - numVerts;
                        newFace1.v3 = startVert;
                        linkfaces.Add(newFace1);

                        newFace2.v1 = startVert;
                        newFace2.v2 = i - numVerts;
                        newFace2.v3 = startVert - numVerts;
                        linkfaces.Add(newFace2);

                        if (hasHollow)
                        {
                            startVert = ++i;
                            for (int l = 0; l < profile.numHollowVerts - 1; l++)
                            {
                                newFace1.v1 = i;
                                newFace1.v2 = i - numVerts;
                                newFace1.v3 = i + 1;
                                linkfaces.Add(newFace1);

                                newFace2.v1 = i + 1;
                                newFace2.v2 = i - numVerts;
                                newFace2.v3 = i + 1 - numVerts;
                                linkfaces.Add(newFace2);
                                i++;
                            }

                            newFace1.v1 = i;
                            newFace1.v2 = i - numVerts;
                            newFace1.v3 = startVert;
                            linkfaces.Add(newFace1);

                            newFace2.v1 = startVert;
                            newFace2.v2 = i - numVerts;
                            newFace2.v3 = startVert - numVerts;
                            linkfaces.Add(newFace2);
                        }
                    }
                    else
                    {
                        for (int i = startVert; i < endVert; i++)
                        {
                            int iNext = i + 1;
                            if (i == endVert - 1)
                                iNext = startVert;

                            newFace1.v1 = i;
                            newFace1.v2 = i - numVerts;
                            newFace1.v3 = iNext;
                            linkfaces.Add(newFace1);

                            newFace2.v1 = iNext;
                            newFace2.v2 = i - numVerts;
                            newFace2.v3 = iNext - numVerts;
                            linkfaces.Add(newFace2);
                        }
                    }
                }

                if(linkfaces.Count > 0)
                    faces.AddRange(linkfaces);

                if (needEndFaces && nodeIndex == lastNode && newLayer.faces.Count > 0)
                {
                    newLayer.AddValue2FaceVertexIndices(coordsStart);
                    faces.AddRange(newLayer.faces);
                }

            } // for (int nodeIndex = 0; nodeIndex < path.pathNodes.Count; nodeIndex++)
        // more cleanup will be done at Meshmerizer.cs
        }


        /// <summary>
        /// DEPRICATED - use Extrude(PathType.Linear) instead
        /// Extrudes a profile along a straight line path. Used for prim types box, cylinder, and prism.
        /// </summary>
        ///
        public void ExtrudeLinear()
        {
            Extrude(PathType.Linear);
        }


        /// <summary>
        /// DEPRICATED - use Extrude(PathType.Circular) instead
        /// Extrude a profile into a circular path prim mesh. Used for prim types torus, tube, and ring.
        /// </summary>
        ///
        public void ExtrudeCircular()
        {
            Extrude(PathType.Circular);
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

            copy.numPrimFaces = this.numPrimFaces;
            copy.errorMessage = this.errorMessage;

            copy.coords = new List<Coord>(this.coords);
            copy.faces = new List<Face>(this.faces);

            return copy;
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
        }

#if VERTEX_INDEXER
        public VertexIndexer GetVertexIndexer()
        {
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
