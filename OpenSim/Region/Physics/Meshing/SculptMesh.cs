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
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using OpenJPEGNet;
using Image = System.Drawing.Image;

namespace OpenSim.Region.Physics.Meshing
{
    // This functionality based on the XNA SculptPreview by John Hurliman.
    public class SculptMesh : Mesh
    {
        Image idata = null;
        Bitmap bLOD = null;
        Bitmap bBitmap = null;

        Vertex northpole = new Vertex(0, 0, 0);
        Vertex southpole = new Vertex(0, 0, 0);

        private int lod = 32;
        private const float RANGE = 128.0f;

        public SculptMesh(byte[] jpegData, float _lod)
        {
            if (_lod == 2f || _lod == 4f || _lod == 8f || _lod == 16f || _lod == 32f || _lod == 64f)
                lod = (int)_lod;

            try
            {
                idata = OpenJPEG.DecodeToImage(jpegData);
                //int i = 0;
                //i = i / i;
            }
            catch (Exception)
            {
                System.Console.WriteLine("[PHYSICS]: Unable to generate a Sculpty physics proxy.  Sculpty texture decode failed!");
                return;
            }
            if (idata != null)
            {
                bBitmap = new Bitmap(idata);
                if (bBitmap.Width == bBitmap.Height)
                {
                    DoLOD();

                    LoadPoles();

                    processSculptTexture();

                    bLOD.Dispose();
                    bBitmap.Dispose();
                    idata.Dispose();
                }
            }


        }
        private Vertex ColorToVertex(Color input)
        {
            return new Vertex(
                ((float)input.R - 128) / RANGE,
                ((float)input.G - 128) / RANGE,
                ((float)input.B - 128) / RANGE);
        }
        private void LoadPoles()
        {
            northpole = new Vertex(0, 0, 0);
            for (int x = 0; x < bLOD.Width; x++)
            {
                northpole += ColorToVertex(GetPixel(0, 0));
            }
            northpole /= bLOD.Width;

            southpole = new Vertex(0, 0, 0);
            for (int x = 0; x < bLOD.Width; x++)
            {
                //System.Console.WriteLine("Height: " + bLOD.Height.ToString());
                southpole += ColorToVertex(GetPixel(bLOD.Height - 1, (bLOD.Height - 1)));
            }
            southpole /= bBitmap.Width;
        }

        private Color GetPixel(int x, int y)
        {
            return bLOD.GetPixel(x, y);
        }

        public int LOD
        {
            get
            {
                return (int)Math.Log(Scale, 2);
            }
            set
            {
                int power = value;
                if (power == 0)
                    power = 6;
                if (power < 2)
                    power = 2;
                if (power > 9)
                    power = 9;
                int t = (int)Math.Pow(2, power);
                if (t != Scale)
                {
                    lod = t;
                }
            }
        }

        public int Scale
        {
            get
            {
                return lod;
            }
        }
        private void DoLOD()
        {
            int x_max = Math.Min(Scale, bBitmap.Width);
            int y_max = Math.Min(Scale, bBitmap.Height);
            if (bBitmap.Width == x_max && bBitmap.Height == y_max)
                bLOD = bBitmap;

            else if (bLOD == null || x_max != bLOD.Width || y_max != bLOD.Height)//don't resize if you don't need to.
            {
                System.Drawing.Bitmap tile = new System.Drawing.Bitmap(bBitmap.Width * 2, bBitmap.Height, PixelFormat.Format24bppRgb);
                System.Drawing.Bitmap tile_LOD = new System.Drawing.Bitmap(x_max * 2, y_max, PixelFormat.Format24bppRgb);

                bLOD = new System.Drawing.Bitmap(x_max, y_max, PixelFormat.Format24bppRgb);
                bLOD.SetResolution(bBitmap.HorizontalResolution, bBitmap.VerticalResolution);

                System.Drawing.Graphics grPhoto = System.Drawing.Graphics.FromImage(tile);
                grPhoto.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                grPhoto.DrawImage(bBitmap,
                    new System.Drawing.Rectangle(0, 0, bBitmap.Width / 2, bBitmap.Height),
                    new System.Drawing.Rectangle(bBitmap.Width / 2, 0, bBitmap.Width / 2, bBitmap.Height),
                    System.Drawing.GraphicsUnit.Pixel);

                grPhoto.DrawImage(bBitmap,
                    new System.Drawing.Rectangle((3 * bBitmap.Width) / 2, 0, bBitmap.Width / 2, bBitmap.Height),
                    new System.Drawing.Rectangle(0, 0, bBitmap.Width / 2, bBitmap.Height),
                    System.Drawing.GraphicsUnit.Pixel);

                grPhoto.DrawImage(bBitmap,
                    new System.Drawing.Rectangle(bBitmap.Width / 2, 0, bBitmap.Width, bBitmap.Height),
                    new System.Drawing.Rectangle(0, 0, bBitmap.Width, bBitmap.Height),
                    System.Drawing.GraphicsUnit.Pixel);

                grPhoto = System.Drawing.Graphics.FromImage(tile_LOD);
                grPhoto.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;

                grPhoto.DrawImage(tile,
                    new System.Drawing.Rectangle(0, 0, tile_LOD.Width, tile_LOD.Height),
                    new System.Drawing.Rectangle(0, 0, tile.Width, tile.Height),
                    System.Drawing.GraphicsUnit.Pixel);

                grPhoto = System.Drawing.Graphics.FromImage(bLOD);
                grPhoto.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                grPhoto.DrawImage(tile_LOD,
                    new System.Drawing.Rectangle(0, 0, bLOD.Width, bLOD.Height),
                    new System.Drawing.Rectangle(tile_LOD.Width / 4, 0, tile_LOD.Width / 2, tile_LOD.Height),
                    System.Drawing.GraphicsUnit.Pixel);

                grPhoto.Dispose();
                tile_LOD.Dispose();
                tile.Dispose();
            }

        }
        public void clearStuff()
        {
            this.triangles.Clear();
            this.vertices.Clear();
            //normals = new float[0];

        }
        public void processSculptTexture()
        {
            int x_max = Math.Min(Scale, bBitmap.Width);
            int y_max = Math.Min(Scale, bBitmap.Height);

            int COLUMNS = x_max + 1;

            Vertex[] sVertices = new Vertex[COLUMNS * y_max];
            //float[] indices = new float[COLUMNS * (y_max - 1) * 6];

            for (int y = 0; y < y_max; y++)
            {
                for (int x = 0; x < x_max; x++)
                {
                    // Create the vertex
                    Vertex v1 = new Vertex(0,0,0);

                    // Create a vertex position from the RGB channels in the current pixel
                    // int ypos = y * bLOD.Width;


                    if (y == 0)
                    {
                        v1 = northpole;
                    }
                    else if (y == y_max - 1)
                    {
                        v1 = southpole;
                    }
                    else
                    {
                        v1 = ColorToVertex(GetPixel(x, y));
                    }

                    // Add the vertex for use later
                    if (!vertices.Contains(v1))
                        Add(v1);

                    sVertices[y * COLUMNS + x] = v1;
                    //System.Console.WriteLine("adding: " + v1.ToString());
                }
                //Vertex tempVertex = vertices[y * COLUMNS];
               // sVertices[y * COLUMNS + x_max] = tempVertex;
            }

            // Create the Triangles
            //int i = 0;

            for (int y = 0; y < y_max - 1; y++)
            {
                int x;

                for (x = 0; x < x_max; x++)
                {
                    Vertex vt11 = sVertices[(y * COLUMNS + x)];
                    Vertex vt12 = sVertices[(y * COLUMNS + (x + 1))];
                    Vertex vt13 = sVertices[((y + 1) * COLUMNS + (x + 1))];
                    if (vt11 != null && vt12 != null && vt13 != null)
                    {
                        if (vt11 != vt12 && vt11 != vt13 && vt12 != vt13)
                        {
                            Triangle tri1 = new Triangle(vt11, vt12, vt13);
                            //indices[i++] = (ushort)(y * COLUMNS + x);
                            //indices[i++] = (ushort)(y * COLUMNS + (x + 1));
                            //indices[i++] = (ushort)((y + 1) * COLUMNS + (x + 1));
                            Add(tri1);
                        }
                    }

                    Vertex vt21 = sVertices[(y * COLUMNS + x)];
                    Vertex vt22 = sVertices[((y + 1) * COLUMNS + (x + 1))];
                    Vertex vt23 = sVertices[((y + 1) * COLUMNS + x)];
                    if (vt21 != null && vt22 != null && vt23 != null)
                    {
                        if (vt21.Equals(vt22, 0.022f) || vt21.Equals(vt23, 0.022f) || vt22.Equals(vt23, 0.022f))
                        {
                        }
                        else
                        {
                            Triangle tri2 = new Triangle(vt21, vt22, vt23);
                            //indices[i++] = (ushort)(y * COLUMNS + x);
                            //indices[i++] = (ushort)((y + 1) * COLUMNS + (x + 1));
                            //indices[i++] = (ushort)((y + 1) * COLUMNS + x);
                            Add(tri2);
                        }
                    }

                }
                //Vertex vt31 = sVertices[(y * x_max + x)];
                //Vertex vt32 = sVertices[(y * x_max + 0)];
                //Vertex vt33 = sVertices[((y + 1) * x_max + 0)];
                //if (vt31 != null && vt32 != null && vt33 != null)
                //{
                    //if (vt31.Equals(vt32, 0.022f) || vt31.Equals(vt33, 0.022f) || vt32.Equals(vt33, 0.022f))
                    //{
                    //}
                    //else
                    //{
                        //Triangle tri3 = new Triangle(vt31, vt32, vt33);
                        // Wrap the last cell in the row around
                        //indices[i++] = (ushort)(y * x_max + x); //a
                        //indices[i++] = (ushort)(y * x_max + 0); //b
                        //indices[i++] = (ushort)((y + 1) * x_max + 0); //c
                        //Add(tri3);
                   // }
                //}

                //Vertex vt41 = sVertices[(y * x_max + x)];
                //Vertex vt42 = sVertices[((y + 1) * x_max + 0)];
                //Vertex vt43 = sVertices[((y + 1) * x_max + x)];
                //if (vt41 != null && vt42 != null && vt43 != null)
                //{
                    //if (vt41.Equals(vt42, 0.022f) || vt31.Equals(vt43, 0.022f) || vt32.Equals(vt43, 0.022f))
                    //{
                    //}
                   // else
                   // {
                        //Triangle tri4 = new Triangle(vt41, vt42, vt43);
                        //indices[i++] = (ushort)(y * x_max + x); //a
                        //indices[i++] = (ushort)((y + 1) * x_max + 0); //b
                        //indices[i++] = (ushort)((y + 1) * x_max + x); //c
                        //Add(tri4);
                    //}
                //}

            }
        }
    }
}
