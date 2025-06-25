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
using System.IO;
using OpenMetaverse;


namespace PrimMesher
{
    public struct Face
    {
        // vertices
        public int v1;
        public int v2;
        public int v3;

        public Face(int _v1, int _v2, int _v3)
        {
            v1 = _v1;
            v2 = _v2;
            v3 = _v3;
        }
    }

    internal struct Angle
    {
        internal float angle;
        internal float X;
        internal float Y;

        internal Angle(float _angle, float x, float y)
        {
            angle = _angle; // 1 is 2pi
            X = x; // cos
            Y = y; // sin
        }
        internal Angle(float _angle)
        {
            angle = _angle; // 1 is 2pi
            X = MathF.Cos(angle); // cos
            Y = MathF.Sin(angle); // sin
        }
    }

    internal class AngleList
    {
        private float iX, iY; // intersection point

        private static readonly Angle[] angles3 =
        {
            new(0.0f, 1.0f, 0.0f),
            new(0.33333333333333333f, -0.5f, 0.86602540378443871f),
            new(0.66666666666666667f, -0.5f, -0.86602540378443837f),
            new(1.0f, 1.0f, 0.0f)
        };

        private static readonly Angle[] angles4 =
        {
            new(0.0f, 1.0f, 0.0f),
            new(0.25f, 0.0f, 1.0f),
            new(0.5f, -1.0f, 0.0f),
            new(0.75f, 0.0f, -1.0f),
            new(1.0f, 1.0f, 0.0f)
        };

        private static readonly Angle[] angles6 =
        {
            new(0.0f, 1.0f, 0.0f),
            new(0.16666666666666667f, 0.5f, 0.8660254037844386f),
            new(0.33333333333333333f, -0.5f, 0.86602540378443871f),
            new(0.5f, -1.0f, 0.0f),
            new(0.66666666666666667f, -0.5f, -0.86602540378443837f),
            new(0.83333333333333326f, 0.5f, -0.86602540378443904f),
            new(1.0f, 1.0f, 0.0f)
        };

        private static readonly Angle[] angles12 =
        {
            new(0.0f, 1.0f, 0.0f),
            new(0.083333333333333329f, 0.86602540378443871f, 0.5f),
            new(0.16666666666666667f, 0.5f, 0.8660254037844386f),
            new(0.25f, 0.0f, 1.0f),
            new(0.33333333333333333f, -0.5f, 0.86602540378443871f),
            new(0.41666666666666663f, -0.86602540378443849f, 0.5f),
            new(0.5f, -1.0f, 0.0f),
            new(0.58333333333333326f, -0.86602540378443882f, -0.5f),
            new(0.66666666666666667f, -0.5f, -0.86602540378443837f),
            new(0.75f, 0.0f, -1.0f),
            new(0.83333333333333326f, 0.5f, -0.86602540378443904f),
            new(0.91666666666666663f, 0.86602540378443837f, -0.5f),
            new(1.0f, 1.0f, 0.0f)
        };

        private static readonly Angle[] angles24 =
        {
            new(0.0f, 1.0f, 0.0f),
            new(0.041666666666666664f, 0.96592582628906831f, 0.25881904510252074f),
            new(0.083333333333333329f, 0.86602540378443871f, 0.5f),
            new(0.125f, 0.70710678118654757f, 0.70710678118654746f),
            new(0.16666666666666667f, 0.5f, 0.8660254037844386f),
            new(0.20833333333333331f, 0.25881904510252096f, 0.9659258262890682f),
            new(0.25f, 0.0f, 1.0f),
            new(0.29166666666666663f, -0.25881904510252063f, 0.96592582628906831f),
            new(0.33333333333333333f, -0.5f, 0.86602540378443871f),
            new(0.375f, -0.70710678118654746f, 0.70710678118654757f),
            new(0.41666666666666663f, -0.86602540378443849f, 0.5f),
            new(0.45833333333333331f, -0.9659258262890682f, 0.25881904510252102f),
            new(0.5f, -1.0f, 0.0f),
            new(0.54166666666666663f, -0.96592582628906842f, -0.25881904510252035f),
            new(0.58333333333333326f, -0.86602540378443882f, -0.5f),
            new(0.62499999999999989f, -0.70710678118654791f, -0.70710678118654713f),
            new(0.66666666666666667f, -0.5f, -0.86602540378443837f),
            new(0.70833333333333326f, -0.25881904510252152f, -0.96592582628906809f),
            new(0.75f, 0.0f, -1.0f),
            new(0.79166666666666663f, 0.2588190451025203f, -0.96592582628906842f),
            new(0.83333333333333326f, 0.5f, -0.86602540378443904f),
            new(0.875f, 0.70710678118654735f, -0.70710678118654768f),
            new(0.91666666666666663f, 0.86602540378443837f, -0.5f),
            new(0.95833333333333326f, 0.96592582628906809f, -0.25881904510252157f),
            new(1.0f, 1.0f, 0.0f)
        };

        private static Angle interpolatePoints(float newPoint, Angle p1, Angle p2)
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

            const float twoPi = 2.0f * MathF.PI;
            const float twoPiInv = 0.5f / MathF.PI;

            if (sides < 1)
                throw new Exception("number of sides not greater than zero");
            if (stopAngle <= startAngle)
                throw new Exception("stopAngle not greater than startAngle");

            Angle[] sourceAngles = sides switch
            {
                3 => angles3,
                4 => angles4,
                6 => angles6,
                12 => angles12,
                24 => angles24,
                _ => null
            };

            if (sourceAngles != null)
            {
                startAngle *= twoPiInv;
                stopAngle *= twoPiInv;
                int startAngleIndex = (int)(startAngle * sides);
                int endAngleIndex = sourceAngles.Length - 1;

                if (hasCut)
                {
                    if (stopAngle < 1.0f)
                        endAngleIndex = (int)(stopAngle * sides) + 1;
                    if (endAngleIndex == startAngleIndex)
                        endAngleIndex++;

                    for (int angleIndex = startAngleIndex; angleIndex <= endAngleIndex; angleIndex++)
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
                float angle = (float)stepSize * startStep;
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
                    Angle newAngle = new(angle);
                    angles.Add(newAngle);
                    step += 1;
                    angle = (float)(stepSize * step);
                }

                if (startAngle > angles[0].angle)
                {
                    intersection(angles[0].X, angles[0].Y, angles[1].X, angles[1].Y, 0.0f, 0.0f, MathF.Cos(startAngle), MathF.Sin(startAngle));
                    Angle newAngle = new(startAngle, iX, iY);
                    angles[0] = newAngle;
                }

                int index = angles.Count - 1;
                if (stopAngle < angles[index].angle)
                {
                    intersection(angles[index - 1].X, angles[index - 1].Y, angles[index].X, angles[index].Y, 0.0f, 0.0f, MathF.Cos(stopAngle), MathF.Sin(stopAngle));
                    Angle newAngle = new(stopAngle, iX, iY);
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
        private const float twoPi = 2.0f * MathF.PI;

        public string errorMessage = null;

        public List<Vector3> coords;
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
            coords = [];
            faces = [];
        }

        public Profile(List<Vector3> _coords)
        {
            coords = _coords;
            faces = [];
        }

        public Profile(List<Vector3> _coords, List<Face> _faces)
        {
            coords = _coords;
            faces = _faces;
        }

        public Profile(int sides, float profileStart, float profileEnd, float hollow, int hollowSides, bool hasProfileCut, bool createFaces)
        {
            const float halfSqr2 = 0.7071067811866f;

            coords = new List<Vector3>();
            faces = new List<Face>();

            List<Vector3> hollowCoords = new();

            bool hasHollow = (hollow > 0.0f);

            AngleList angles = new();
            AngleList hollowAngles = new();

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
            Vector3 newVert = new();

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
                coords.Add(Vector3.Zero);
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
                        Face newFace = new();

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
                        Face newFace = new();
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
                        Face newFace = new();
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
                                newFace.v2 = numTotalVerts - 1;
                                newFace.v3 = numTotalVerts - maxJ - 1;

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
                        Face newFace = new()
                        {
                            v1 = 0,
                            v2 = i,
                            v3 = i + 1
                        };
                        faces.Add(newFace);
                    }
                    if (!hasProfileCut)
                    {
                        Face newFace = new()
                        {
                            v1 = 0,
                            v2 = numAngles,
                            v3 = 1
                        };
                        faces.Add(newFace);
                    }
                }
            }
        }


        public Profile Copy()
        {
            return Copy(true);
        }

        public Profile Copy(bool needFaces)
        {
            Profile copy = needFaces ?
                new(new List<Vector3>(coords), new List<Face>(faces)) :
                new(new List<Vector3>(coords));

            copy.numOuterVerts = numOuterVerts;
            copy.numHollowVerts = numHollowVerts;

            return copy;
        }

        public void AddPos(Vector3 v)
        {
            for (int i = 0; i < coords.Count; i++)
                coords[i] += v;
        }

        public void AddPos(float x, float y, float z)
        {
            Vector3 v = new(x,y,z);
            AddPos(v);
        }

        public void AddRot(Quaternion q)
        {
            int i;
            int numVerts = coords.Count;

            for (i = 0; i < numVerts; i++)
                coords[i] *= q;
        }

        public void Scale(float x, float y)
        {
            Vector3 vert;
            for (int i = 0; i < coords.Count; i++)
            {
                vert = coords[i];
                vert.X *= x;
                vert.X = MathF.Round(vert.X,5);
                vert.Y *= y;
                vert.Y = MathF.Round(vert.Y,5);
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
            if(coords.Count == 0)
                return;

            Face tmpFace;
            int tmp;

            for (int i = 0; i < faces.Count; i++)
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
            if(faces.Count == 0)
                return;

            Face tmpFace;
            for (int i = 0; i < faces.Count; i++)
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

            string completePath = System.IO.Path.Combine(path, $"{name}_{title}.raw");
            using StreamWriter sw = new(completePath);
            for (int i = 0; i < faces.Count; i++)
            {
                sw.WriteLine($"{coords[faces[i].v1]} {coords[faces[i].v3]} {coords[faces[i].v3]}");
            }

            sw.Close();
        }
    }

    public struct PathNode
    {
        public Vector3 position;
        public Quaternion rotation;
        public float xScale;
        public float yScale;
        public float percentOfPath;
    }

    public enum PathType { Linear = 0, Circular = 1, Flexible = 2 }

    public class Path
    {
        public List<PathNode> pathNodes = new();

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

        private const float twoPi = 2.0f * MathF.PI;

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
                float twistTotalAbs = MathF.Abs(twistTotal);
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
                    PathNode newNode = new();

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

                    newNode.rotation = new Quaternion(Quaternion.MainAxis.Z, twist);
                    newNode.position = new Vector3(xOffset, yOffset, zOffset);
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
                float twistTotalAbs = MathF.Abs(twistTotal);
                if (twistTotalAbs > 0.01f)
                {
                    if (twistTotalAbs > MathF.PI * 1.5f)
                        steps *= 2;
                    if (twistTotalAbs > MathF.PI * 3.0f)
                        steps *= 2;
                }

                float yPathScale = holeSizeY * 0.5f;
                float pathLength = pathCutEnd - pathCutBegin;
                float totalSkew = skew * 2.0f * pathLength;
                float skewStart = pathCutBegin * 2.0f * skew - skew;
                float xOffsetTopShearXFactor = topShearX * (0.25f + 0.5f * (0.5f - holeSizeY));
                float yShearCompensation = 1.0f + MathF.Abs(topShearY) * 0.25f;

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
                    PathNode newNode = new();

                    float xProfileScale = (1.0f - MathF.Abs(skew)) * holeSizeX;
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
                    xOffset += MathF.Sin(angle) * xOffsetTopShearXFactor;

                    float yOffset = yShearCompensation * MathF.Cos(angle) * (0.5f - yPathScale) * radiusScale;

                    float zOffset = MathF.Sin(angle + topShearY) * (0.5f - yPathScale) * radiusScale;

                    newNode.position = new Vector3(xOffset, yOffset, zOffset);

                    // now orient the rotation of the profile layer relative to it's position on the path
                    // adding taperY to the angle used to generate the quat appears to approximate the viewer

                    newNode.rotation = new Quaternion(Quaternion.MainAxis.X, angle + topShearY);

                    // next apply twist rotation to the profile layer
                    if (twistTotal != 0.0f || twistBegin != 0.0f)
                        newNode.rotation *= new Quaternion(Quaternion.MainAxis.Z, twist);

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
        public List<Vector3> coords;
        public List<Face> faces;

        private int sides = 4;
        private int hollowSides = 4;
        private float profileStart = 0.0f;
        private float profileEnd = 1.0f;
        private float hollow = 0.0f;
        public float twistBegin = 0;
        public float twistEnd = 0;
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
            coords = new List<Vector3>();
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
            else if (profileEnd < 0.02f)
                profileEnd = 0.02f;
            if (profileStart >= profileEnd)
                profileStart = profileEnd - 0.02f;
            if (hollow > 0.99f)
                hollow = 0.99f;
            else if (hollow < 0.0f)
                hollow = 0.0f;
        }

        /// <summary>
        /// Extrudes a profile along a path.
        /// </summary>
        public void Extrude(PathType pathType)
        {
            bool needEndFaces;

            coords = new List<Vector3>();
            faces = new List<Face>();

            int steps = 1;

            //float length = pathCutEnd - pathCutBegin;

            hasProfileCut = profileEnd - profileStart < 0.9999f;

            float twistTotal = twistEnd - twistBegin;
            float twistTotalAbs = MathF.Abs(twistTotal);
            if (twistTotalAbs > 0.01f)
                steps += (int)(twistTotalAbs * 3.66); //  dahlia's magic number

            float hollow = this.hollow;
            hasHollow = hollow > 0.001f;

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
                    initialProfileRot = MathF.PI;
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
                    initialProfileRot = 0.25f * MathF.PI;
                    if (hollowSides != 4)
                        hollow *= 0.707f;
                }
                else if (sides > 4)
                {
                    initialProfileRot = MathF.PI;
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
                    initialProfileRot = 1.25f * MathF.PI;
                    if (hollowSides != 4)
                        hollow *= 0.707f;
                }
                else if (sides == 24 && hollowSides == 4)
                    hollow *= 1.414f;
            }

            Profile profile = new(sides, profileStart, profileEnd, hollow, hollowSides, HasProfileCut, true);
            errorMessage = profile.errorMessage;

            numPrimFaces = profile.numPrimFaces;

            if (initialProfileRot != 0.0f)
            {
                profile.AddRot(new Quaternion(Quaternion.MainAxis.Z, initialProfileRot));
            }

            Path path = new()
            {
                twistBegin = twistBegin,
                twistEnd = twistEnd,
                topShearX = topShearX,
                topShearY = topShearY,
                pathCutBegin = pathCutBegin,
                pathCutEnd = pathCutEnd,
                dimpleBegin = dimpleBegin,
                dimpleEnd = dimpleEnd,
                skew = skew,
                holeSizeX = holeSizeX,
                holeSizeY = holeSizeY,
                taperX = taperX,
                taperY = taperY,
                radius = radius,
                revolutions = revolutions,
                stepsPerRevolution = stepsPerRevolution
            };

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

                List<Face> linkfaces = new();
                int numVerts = newLayer.coords.Count;
                Face newFace1 = new();
                Face newFace2 = new();

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

        private static Vector3 SurfaceNormal(Vector3 c1, Vector3 c2, Vector3 c3)
        {
            Vector3 edge1 = c2 - c1;
            Vector3 edge2 = c3 - c1;

            Vector3 normal = Vector3.Cross(edge1, edge2);
            normal.Normalize();

            return normal;
        }

        private Vector3 SurfaceNormal(Face face)
        {
            return SurfaceNormal(coords[face.v1], coords[face.v2], coords[face.v3]);
        }

        /// <summary>
        /// Calculate the surface normal for a face in the list of faces
        /// </summary>
        /// <param name="faceIndex"></param>
        /// <returns></returns>
        public Vector3 SurfaceNormal(int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= faces.Count)
                throw new Exception("faceIndex out of range");

            return SurfaceNormal(faces[faceIndex]);
        }

        /// <summary>
        /// Duplicates a PrimMesh object. All object properties are copied by value, including lists.
        /// </summary>
        /// <returns></returns>
        public PrimMesh Copy()
        {
            PrimMesh copy = new(sides, profileStart, profileEnd, hollow, hollowSides)
            {
                twistBegin = twistBegin,
                twistEnd = twistEnd,
                topShearX = topShearX,
                topShearY = topShearY,
                pathCutBegin = pathCutBegin,
                pathCutEnd = pathCutEnd,
                dimpleBegin = dimpleBegin,
                dimpleEnd = dimpleEnd,
                skew = skew,
                holeSizeX = holeSizeX,
                holeSizeY = holeSizeY,
                taperX = taperX,
                taperY = taperY,
                radius = radius,
                revolutions = revolutions,
                stepsPerRevolution = stepsPerRevolution,

                numPrimFaces = numPrimFaces,
                errorMessage = errorMessage,

                coords = new List<Vector3>(coords),
                faces = new List<Face>(faces)
            };

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
            Vector3 vert;
            for (int i = 0; i < coords.Count; i++)
            {
                vert = coords[i];
                vert.X += x;
                vert.Y += y;
                vert.Z += z;
                coords[i] = vert;
            }
        }

        /// <summary>
        /// Rotates the mesh
        /// </summary>
        /// <param name="q"></param>
        public void AddRot(Quaternion q)
        {
            for (int i = 0; i < coords.Count; i++)
                coords[i] *= q;
        }

        /// <summary>
        /// Scales the mesh
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public void Scale(float x, float y, float z)
        {
            Vector3 m = new(x, y, z);
            for (int i = 0; i < coords.Count; i++)
                coords[i] *= m;
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
            string completePath = System.IO.Path.Combine(path, $"{name}_{title}.raw");
            using StreamWriter sw = new(completePath);
            for (int i = 0; i < this.faces.Count; i++)
            {
                sw.Write(coords[faces[i].v1].ToString());
                sw.Write(coords[faces[i].v2].ToString());
                sw.WriteLine(coords[faces[i].v3].ToString());
            }
        }
    }
}
