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

// to build without references to System.Drawing, comment this out
#define SYSTEM_DRAWING

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

#if SYSTEM_DRAWING
using System.Drawing;
using System.Drawing.Imaging;
#endif

namespace PrimMesher
{

    public class SculptMesh
    {
        public List<Coord> coords;
        public List<Face> faces;

        public List<ViewerFace> viewerFaces;
        public List<Coord> normals;
        public List<UVCoord> uvs;

        public enum SculptType { sphere = 1, torus = 2, plane = 3, cylinder = 4 };

#if SYSTEM_DRAWING

        public SculptMesh SculptMeshFromFile(string fileName, SculptType sculptType, int lod, bool viewerMode)
        {
            Bitmap bitmap = (Bitmap)Bitmap.FromFile(fileName);
            SculptMesh sculptMesh = new SculptMesh(bitmap, sculptType, lod, viewerMode);
            bitmap.Dispose();
            return sculptMesh;
        }


        public SculptMesh(string fileName, int sculptType, int lod, int viewerMode, int mirror, int invert)
        {
            Bitmap bitmap = (Bitmap)Bitmap.FromFile(fileName);
            _SculptMesh(bitmap, (SculptType)sculptType, lod, viewerMode != 0, mirror != 0, invert != 0);
            bitmap.Dispose();
        }
#endif

        /// <summary>
        /// ** Experimental ** May disappear from future versions ** not recommeneded for use in applications
        /// Construct a sculpt mesh from a 2D array of floats
        /// </summary>
        /// <param name="zMap"></param>
        /// <param name="xBegin"></param>
        /// <param name="xEnd"></param>
        /// <param name="yBegin"></param>
        /// <param name="yEnd"></param>
        /// <param name="viewerMode"></param>
        public SculptMesh(float[,] zMap, float xBegin, float xEnd, float yBegin, float yEnd, bool viewerMode)
        {
            float xStep, yStep;
            float uStep, vStep;

            int numYElements = zMap.GetLength(0);
            int numXElements = zMap.GetLength(1);

            try
            {
                xStep = (xEnd - xBegin) / (float)(numXElements - 1);
                yStep = (yEnd - yBegin) / (float)(numYElements - 1);

                uStep = 1.0f / (numXElements - 1);
                vStep = 1.0f / (numYElements - 1);
            }
            catch (DivideByZeroException)
            {
                return;
            }

            coords = new List<Coord>();
            faces = new List<Face>();
            normals = new List<Coord>();
            uvs = new List<UVCoord>();

            viewerFaces = new List<ViewerFace>();

            int p1, p2, p3, p4;

            int x, y;
            int xStart = 0, yStart = 0;

            for (y = yStart; y < numYElements; y++)
            {
                int rowOffset = y * numXElements;

                for (x = xStart; x < numXElements; x++)
                {
                    /*
                    *   p1-----p2
                    *   | \ f2 |
                    *   |   \  |
                    *   | f1  \|
                    *   p3-----p4
                    */

                    p4 = rowOffset + x;
                    p3 = p4 - 1;

                    p2 = p4 - numXElements;
                    p1 = p3 - numXElements;

                    Coord c = new Coord(xBegin + x * xStep, yBegin + y * yStep, zMap[y, x]);
                    this.coords.Add(c);
                    if (viewerMode)
                    {
                        this.normals.Add(new Coord());
                        this.uvs.Add(new UVCoord(uStep * x, 1.0f - vStep * y));
                    }

                    if (y > 0 && x > 0)
                    {
                        Face f1, f2;

                        if (viewerMode)
                        {
                            f1 = new Face(p1, p4, p3, p1, p4, p3);
                            f1.uv1 = p1;
                            f1.uv2 = p4;
                            f1.uv3 = p3;

                            f2 = new Face(p1, p2, p4, p1, p2, p4);
                            f2.uv1 = p1;
                            f2.uv2 = p2;
                            f2.uv3 = p4;
                        }
                        else
                        {
                            f1 = new Face(p1, p4, p3);
                            f2 = new Face(p1, p2, p4);
                        }

                        this.faces.Add(f1);
                        this.faces.Add(f2);
                    }
                }
            }

            if (viewerMode)
                calcVertexNormals(SculptType.plane, numXElements, numYElements);
        }

#if SYSTEM_DRAWING
        public SculptMesh(Bitmap sculptBitmap, SculptType sculptType, int lod, bool viewerMode)
        {
            _SculptMesh(sculptBitmap, sculptType, lod, viewerMode, false, false);
        }

        public SculptMesh(Bitmap sculptBitmap, SculptType sculptType, int lod, bool viewerMode, bool mirror, bool invert)
        {
            _SculptMesh(sculptBitmap, sculptType, lod, viewerMode, mirror, invert);
        }
#endif

        public SculptMesh(List<List<Coord>> rows, SculptType sculptType, bool viewerMode, bool mirror, bool invert)
        {
            _SculptMesh(rows, sculptType, viewerMode, mirror, invert);
        }

#if SYSTEM_DRAWING
        /// <summary>
        /// converts a bitmap to a list of lists of coords, while scaling the image.
        /// the scaling is done in floating point so as to allow for reduced vertex position
        /// quantization as the position will be averaged between pixel values. this routine will
        /// likely fail if the bitmap width and height are not powers of 2.
        /// </summary>
        /// <param name="bitmap"></param>
        /// <param name="scale"></param>
        /// <param name="mirror"></param>
        /// <returns></returns>
        private List<List<Coord>> bitmap2Coords(Bitmap bitmap, int scale, bool mirror)
        {
            int numRows = bitmap.Height / scale;
            int numCols = bitmap.Width / scale;
            List<List<Coord>> rows = new List<List<Coord>>(numRows);

            float pixScale = 1.0f / (scale * scale);
            pixScale /= 255;

            int imageX, imageY = 0;

            int rowNdx, colNdx;

            for (rowNdx = 0; rowNdx < numRows; rowNdx++)
            {
                List<Coord> row = new List<Coord>(numCols);
                for (colNdx = 0; colNdx < numCols; colNdx++)
                {
                    imageX = colNdx * scale;
                    int imageYStart = rowNdx * scale;
                    int imageYEnd = imageYStart + scale;
                    int imageXEnd = imageX + scale;
                    float rSum = 0.0f;
                    float gSum = 0.0f;
                    float bSum = 0.0f;
                    for (; imageX < imageXEnd; imageX++)
                    {
                        for (imageY = imageYStart; imageY < imageYEnd; imageY++)
                        {
                            Color c = bitmap.GetPixel(imageX, imageY);
                            if (c.A != 255)
                            {
                                bitmap.SetPixel(imageX, imageY, Color.FromArgb(255, c.R, c.G, c.B));
                                c = bitmap.GetPixel(imageX, imageY);
                            }
                            rSum += c.R;
                            gSum += c.G;
                            bSum += c.B;
                        }
                    }
                    if (mirror)
                        row.Add(new Coord(-(rSum * pixScale - 0.5f), gSum * pixScale - 0.5f, bSum * pixScale - 0.5f));
                    else
                        row.Add(new Coord(rSum * pixScale - 0.5f, gSum * pixScale - 0.5f, bSum * pixScale - 0.5f));

                }
                rows.Add(row);
            }
            return rows;
        }

        private List<List<Coord>> bitmap2CoordsSampled(Bitmap bitmap, int scale, bool mirror)
        {
            int numRows = bitmap.Height / scale;
            int numCols = bitmap.Width / scale;
            List<List<Coord>> rows = new List<List<Coord>>(numRows);

            float pixScale = 1.0f / 256.0f;

            int imageX, imageY = 0;

            int rowNdx, colNdx;

            for (rowNdx = 0; rowNdx <= numRows; rowNdx++)
            {
                List<Coord> row = new List<Coord>(numCols);
                imageY = rowNdx * scale;
                if (rowNdx == numRows) imageY--;
                for (colNdx = 0; colNdx <= numCols; colNdx++)
                {
                    imageX = colNdx * scale;
                    if (colNdx == numCols) imageX--;

                    Color c = bitmap.GetPixel(imageX, imageY);
                    if (c.A != 255)
                    {
                        bitmap.SetPixel(imageX, imageY, Color.FromArgb(255, c.R, c.G, c.B));
                        c = bitmap.GetPixel(imageX, imageY);
                    }

                    if (mirror)
                        row.Add(new Coord(-(c.R * pixScale - 0.5f), c.G * pixScale - 0.5f, c.B * pixScale - 0.5f));
                    else
                        row.Add(new Coord(c.R * pixScale - 0.5f, c.G * pixScale - 0.5f, c.B * pixScale - 0.5f));

                }
                rows.Add(row);
            }
            return rows;
        }


        void _SculptMesh(Bitmap sculptBitmap, SculptType sculptType, int lod, bool viewerMode, bool mirror, bool invert)
        {
            _SculptMesh(new SculptMap(sculptBitmap, lod).ToRows(mirror), sculptType, viewerMode, mirror, invert);
        }
#endif

        void _SculptMesh(List<List<Coord>> rows, SculptType sculptType, bool viewerMode, bool mirror, bool invert)
        {
            coords = new List<Coord>();
            faces = new List<Face>();
            normals = new List<Coord>();
            uvs = new List<UVCoord>();

            sculptType = (SculptType)(((int)sculptType) & 0x07);

            if (mirror)
                invert = !invert;

            viewerFaces = new List<ViewerFace>();

            int width = rows[0].Count;

            int p1, p2, p3, p4;

            int imageX, imageY;

            if (sculptType != SculptType.plane)
            {
                if (rows.Count % 2 == 0)
                {
                    for (int rowNdx = 0; rowNdx < rows.Count; rowNdx++)
                        rows[rowNdx].Add(rows[rowNdx][0]);
                }
                else
                {
                    int lastIndex = rows[0].Count - 1;

                    for (int i = 0; i < rows.Count; i++)
                        rows[i][0] = rows[i][lastIndex];
                }
            }

            Coord topPole = rows[0][width / 2];
            Coord bottomPole = rows[rows.Count - 1][width / 2];

            if (sculptType == SculptType.sphere)
            {
                if (rows.Count % 2 == 0)
                {
                    int count = rows[0].Count;
                    List<Coord> topPoleRow = new List<Coord>(count);
                    List<Coord> bottomPoleRow = new List<Coord>(count);

                    for (int i = 0; i < count; i++)
                    {
                        topPoleRow.Add(topPole);
                        bottomPoleRow.Add(bottomPole);
                    }
                    rows.Insert(0, topPoleRow);
                    rows.Add(bottomPoleRow);
                }
                else
                {
                    int count = rows[0].Count;

                    List<Coord> topPoleRow = rows[0];
                    List<Coord> bottomPoleRow = rows[rows.Count - 1];

                    for (int i = 0; i < count; i++)
                    {
                        topPoleRow[i] = topPole;
                        bottomPoleRow[i] = bottomPole;
                    }
                }
            }

            if (sculptType == SculptType.torus)
                rows.Add(rows[0]);

            int coordsDown = rows.Count;
            int coordsAcross = rows[0].Count;
//            int lastColumn = coordsAcross - 1;

            float widthUnit = 1.0f / (coordsAcross - 1);
            float heightUnit = 1.0f / (coordsDown - 1);

            for (imageY = 0; imageY < coordsDown; imageY++)
            {
                int rowOffset = imageY * coordsAcross;

                for (imageX = 0; imageX < coordsAcross; imageX++)
                {
                    /*
                    *   p1-----p2
                    *   | \ f2 |
                    *   |   \  |
                    *   | f1  \|
                    *   p3-----p4
                    */

                    p4 = rowOffset + imageX;
                    p3 = p4 - 1;

                    p2 = p4 - coordsAcross;
                    p1 = p3 - coordsAcross;

                    this.coords.Add(rows[imageY][imageX]);
                    if (viewerMode)
                    {
                        this.normals.Add(new Coord());
                        this.uvs.Add(new UVCoord(widthUnit * imageX, heightUnit * imageY));
                    }

                    if (imageY > 0 && imageX > 0)
                    {
                        Face f1, f2;

                        if (viewerMode)
                        {
                            if (invert)
                            {
                                f1 = new Face(p1, p4, p3, p1, p4, p3);
                                f1.uv1 = p1;
                                f1.uv2 = p4;
                                f1.uv3 = p3;

                                f2 = new Face(p1, p2, p4, p1, p2, p4);
                                f2.uv1 = p1;
                                f2.uv2 = p2;
                                f2.uv3 = p4;
                            }
                            else
                            {
                                f1 = new Face(p1, p3, p4, p1, p3, p4);
                                f1.uv1 = p1;
                                f1.uv2 = p3;
                                f1.uv3 = p4;

                                f2 = new Face(p1, p4, p2, p1, p4, p2);
                                f2.uv1 = p1;
                                f2.uv2 = p4;
                                f2.uv3 = p2;
                            }
                        }
                        else
                        {
                            if (invert)
                            {
                                f1 = new Face(p1, p4, p3);
                                f2 = new Face(p1, p2, p4);
                            }
                            else
                            {
                                f1 = new Face(p1, p3, p4);
                                f2 = new Face(p1, p4, p2);
                            }
                        }

                        this.faces.Add(f1);
                        this.faces.Add(f2);
                    }
                }
            }

            if (viewerMode)
                calcVertexNormals(sculptType, coordsAcross, coordsDown);
        }

        /// <summary>
        /// Duplicates a SculptMesh object. All object properties are copied by value, including lists.
        /// </summary>
        /// <returns></returns>
        public SculptMesh Copy()
        {
            return new SculptMesh(this);
        }

        public SculptMesh(SculptMesh sm)
        {
            coords = new List<Coord>(sm.coords);
            faces = new List<Face>(sm.faces);
            viewerFaces = new List<ViewerFace>(sm.viewerFaces);
            normals = new List<Coord>(sm.normals);
            uvs = new List<UVCoord>(sm.uvs);
        }

        private void calcVertexNormals(SculptType sculptType, int xSize, int ySize)
        {  // compute vertex normals by summing all the surface normals of all the triangles sharing
            // each vertex and then normalizing
            int numFaces = this.faces.Count;
            for (int i = 0; i < numFaces; i++)
            {
                Face face = this.faces[i];
                Coord surfaceNormal = face.SurfaceNormal(this.coords);
                this.normals[face.n1] += surfaceNormal;
                this.normals[face.n2] += surfaceNormal;
                this.normals[face.n3] += surfaceNormal;
            }

            int numNormals = this.normals.Count;
            for (int i = 0; i < numNormals; i++)
                this.normals[i] = this.normals[i].Normalize();

            if (sculptType != SculptType.plane)
            { // blend the vertex normals at the cylinder seam
                for (int y = 0; y < ySize; y++)
                {
                    int rowOffset = y * xSize;

                    this.normals[rowOffset] = this.normals[rowOffset + xSize - 1] = (this.normals[rowOffset] + this.normals[rowOffset + xSize - 1]).Normalize();
                }
            }

            foreach (Face face in this.faces)
            {
                ViewerFace vf = new ViewerFace(0);
                vf.v1 = this.coords[face.v1];
                vf.v2 = this.coords[face.v2];
                vf.v3 = this.coords[face.v3];

                vf.coordIndex1 = face.v1;
                vf.coordIndex2 = face.v2;
                vf.coordIndex3 = face.v3;

                vf.n1 = this.normals[face.n1];
                vf.n2 = this.normals[face.n2];
                vf.n3 = this.normals[face.n3];

                vf.uv1 = this.uvs[face.uv1];
                vf.uv2 = this.uvs[face.uv2];
                vf.uv3 = this.uvs[face.uv3];

                this.viewerFaces.Add(vf);
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

            int numNormals = this.normals.Count;
            for (i = 0; i < numNormals; i++)
                this.normals[i] *= q;

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

        public void Scale(float x, float y, float z)
        {
            int i;
            int numVerts = this.coords.Count;

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
