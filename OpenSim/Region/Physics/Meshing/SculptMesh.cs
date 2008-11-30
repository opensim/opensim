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
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

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
        private const float pixScale = 0.00390625f; // 1.0 / 256

        private Bitmap ScaleImage(Bitmap srcImage, float scale)
        {
            int sourceWidth = srcImage.Width;
            int sourceHeight = srcImage.Height;
            int sourceX = 0;
            int sourceY = 0;

            int destX = 0;
            int destY = 0;
            int destWidth = (int)(sourceWidth * scale);
            int destHeight = (int)(sourceHeight * scale);

            Bitmap scaledImage = new Bitmap(destWidth, destHeight,
                                     PixelFormat.Format24bppRgb);
            scaledImage.SetResolution(srcImage.HorizontalResolution,
                                    srcImage.VerticalResolution);

            Graphics grPhoto = Graphics.FromImage(scaledImage);
            grPhoto.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;

            grPhoto.DrawImage(srcImage,
                new Rectangle(destX, destY, destWidth, destHeight),
                new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                GraphicsUnit.Pixel);

            grPhoto.Dispose();
            return scaledImage;
        }

        public SculptMesh SculptMeshFromFile(string fileName, SculptType sculptType, int lod, bool viewerMode)
        {
            Bitmap bitmap = (Bitmap)Bitmap.FromFile(fileName);
            SculptMesh sculptMesh = new SculptMesh(bitmap, sculptType, lod, viewerMode);
            bitmap.Dispose();
            return sculptMesh;
        }

        public SculptMesh(Bitmap sculptBitmap, SculptType sculptType, int lod, bool viewerMode)
        {
            coords = new List<Coord>();
            faces = new List<Face>();
            normals = new List<Coord>();
            uvs = new List<UVCoord>();

            float sourceScaleFactor = (float)lod / (float)Math.Max(sculptBitmap.Width, sculptBitmap.Height);
            bool scaleSourceImage = sourceScaleFactor < 1.0f ? true : false;

            Bitmap bitmap;
            if (scaleSourceImage)
                bitmap = ScaleImage(sculptBitmap, sourceScaleFactor);
            else
                bitmap = sculptBitmap;

            viewerFaces = new List<ViewerFace>();

            int width = bitmap.Width;
            int height = bitmap.Height;

            float widthUnit = 1.0f / width;
            float heightUnit = 1.0f / (height - 1);

            int p1, p2, p3, p4;
            Color color;
            float x, y, z;

            int imageX, imageY;

            if (sculptType == SculptType.sphere)
            { // average the top and bottom row pixel values so the resulting vertices appear to converge
                int lastRow = height - 1;
                int r1 = 0, g1 = 0, b1 = 0;
                int r2 = 0, g2 = 0, b2 = 0;
                for (imageX = 0; imageX < width; imageX++)
                {
                    Color c1 = bitmap.GetPixel(imageX, 0);
                    Color c2 = bitmap.GetPixel(imageX, lastRow);

                    r1 += c1.R;
                    g1 += c1.G;
                    b1 += c1.B;

                    r2 += c2.R;
                    g2 += c2.G;
                    b2 += c2.B;
                }

                Color newC1 = Color.FromArgb(r1 / width, g1 / width, b1 / width);
                Color newC2 = Color.FromArgb(r2 / width, g2 / width, b2 / width);

                for (imageX = 0; imageX < width; imageX++)
                {
                    bitmap.SetPixel(imageX, 0, newC1);
                    bitmap.SetPixel(imageX, lastRow, newC2);
                }
            }


            int pixelsAcross = sculptType == SculptType.plane ? width : width + 1;
            int pixelsDown = sculptType == SculptType.sphere || sculptType == SculptType.cylinder ? height + 1 : height;

            for (imageY = 0; imageY < pixelsDown; imageY++)
            {
                int rowOffset = imageY * width;

                for (imageX = 0; imageX < pixelsAcross; imageX++)
                {
                    /*
                    *   p1-----p2
                    *   | \ f2 |
                    *   |   \  |
                    *   | f1  \|
                    *   p3-----p4
                    */

                    if (imageX < width)
                    {
                        p4 = rowOffset + imageX;
                        p3 = p4 - 1;
                    }
                    else
                    {
                        p4 = rowOffset; // wrap around to beginning
                        p3 = rowOffset + imageX - 1;
                    }

                    p2 = p4 - width;
                    p1 = p3 - width;

                    color = bitmap.GetPixel(imageX == width ? 0 : imageX, imageY == height ? height - 1 : imageY);

                    x = (color.R - 128) * pixScale;
                    y = (color.G - 128) * pixScale;
                    z = (color.B - 128) * pixScale;

                    Coord c = new Coord(x, y, z);
                    this.coords.Add(c);
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
                            f1 = new Face(p1, p3, p4, p1, p3, p4);
                            f1.uv1 = p1;
                            f1.uv2 = p3;
                            f1.uv3 = p4;

                            f2 = new Face(p1, p4, p2, p1, p4, p2);
                            f2.uv1 = p1;
                            f2.uv2 = p4;
                            f2.uv3 = p2;
                        }
                        else
                        {
                            f1 = new Face(p1, p3, p4);
                            f2 = new Face(p1, p4, p2);
                        }

                        this.faces.Add(f1);
                        this.faces.Add(f2);
                    }
                }
            }

            if (scaleSourceImage)
                bitmap.Dispose();

            if (viewerMode)
            {  // compute vertex normals by summing all the surface normals of all the triangles sharing
                // each vertex and then normalizing
                int numFaces = this.faces.Count;
                for (int i = 0; i < numFaces; i++)
                {
                    Face face = this.faces[i];
                    Coord surfaceNormal = face.SurfaceNormal(this.coords);
                    this.normals[face.v1] += surfaceNormal;
                    this.normals[face.v2] += surfaceNormal;
                    this.normals[face.v3] += surfaceNormal;
                }

                int numCoords = this.coords.Count;
                for (int i = 0; i < numCoords; i++)
                    this.coords[i].Normalize();

                if (sculptType != SculptType.plane)
                { // blend the vertex normals at the cylinder seam
                    pixelsAcross = width + 1;
                    for (imageY = 0; imageY < height; imageY++)
                    {
                        int rowOffset = imageY * pixelsAcross;

                        this.normals[rowOffset] = this.normals[rowOffset + width - 1] = (this.normals[rowOffset] + this.normals[rowOffset + width - 1]).Normalize();
                    }
                }

                foreach (Face face in this.faces)
                {
                    ViewerFace vf = new ViewerFace(0);
                    vf.v1 = this.coords[face.v1];
                    vf.v2 = this.coords[face.v2];
                    vf.v3 = this.coords[face.v3];

                    vf.n1 = this.normals[face.n1];
                    vf.n2 = this.normals[face.n2];
                    vf.n3 = this.normals[face.n3];

                    vf.uv1 = this.uvs[face.uv1];
                    vf.uv2 = this.uvs[face.uv2];
                    vf.uv3 = this.uvs[face.uv3];

                    this.viewerFaces.Add(vf);
                }
            }
        }

        public void AddRot(Quat q)
        {
            int i;
            int numVerts = this.coords.Count;

            for (i = 0; i < numVerts; i++)
                this.coords[i] *= q;

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
