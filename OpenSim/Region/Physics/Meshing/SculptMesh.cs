using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using OpenJPEGNet;
using Image = System.Drawing.Image;

namespace OpenSim.Region.Physics.Meshing
{
    public class SculptMesh : Mesh
    {
        Image idata = null;
        Bitmap bLOD = null;
        Bitmap bBitmap = null;

        Vertex northpole = (Vertex)Vertex.Zero;
        Vertex southpole = (Vertex)Vertex.Zero;

        private int lod = 64;
        private const float RANGE = 128.0f;

        public SculptMesh(byte[] jpegData)
        {
            idata = OpenJPEG.DecodeToImage(jpegData);
            if (idata != null)
                bBitmap = new Bitmap(idata);

            
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
            northpole = (Vertex)Vertex.Zero;
            for (int x = 0; x < bBitmap.Width; x++)
            {
                northpole += ColorToVertex(GetPixel(0, 0));
            }
            northpole /= bBitmap.Width;

            southpole = (Vertex)Vertex.Zero;
            for (int x = 0; x < bBitmap.Width; x++)
            {
                southpole += ColorToVertex(GetPixel(bBitmap.Height - 1, (bBitmap.Height - 1)));
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
            normals = new float[0];

        }
        public void processSculptTexture()
        {
            int x_max = Math.Min(Scale, bBitmap.Width);
            int y_max = Math.Min(Scale, bBitmap.Height);

            int COLUMNS = x_max + 1;

            Vertex[] sVertices = new Vertex[COLUMNS * y_max];
            float[] indices = new float[COLUMNS * (y_max - 1) * 6];

            for (int y = 0; y < y_max; y++)
            {
                for (int x = 0; x < x_max; x++)
                {
                    // Create the vertex
                    Vertex v1 = new Vertex(0,0,0);

                    // Create a vertex position from the RGB channels in the current pixel
                    int ypos = y * bLOD.Width;

                    
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
                    Add(v1);
                    sVertices[y * COLUMNS + x] = v1;
                }
                Vertex tempVertex = vertices[y * COLUMNS];
                sVertices[y * COLUMNS + x_max] = tempVertex;
            }

            // Create the Triangles
            int i = 0;

            for (int y = 0; y < y_max - 1; y++)
            {
                int x;

                for (x = 0; x < x_max; x++)
                {
                    Triangle tri1 = new Triangle(sVertices[(y * COLUMNS + x)], sVertices[(y * COLUMNS + (x + 1))],
                                                 sVertices[((y + 1) * COLUMNS + (x + 1))]);
                    //indices[i++] = (ushort)(y * COLUMNS + x);
                    //indices[i++] = (ushort)(y * COLUMNS + (x + 1));
                    //indices[i++] = (ushort)((y + 1) * COLUMNS + (x + 1));
                    Add(tri1);
                    Triangle tri2 = new Triangle(sVertices[(y * COLUMNS + x)],sVertices[((y + 1) * COLUMNS + (x + 1))],
                                                 sVertices[((y + 1) * COLUMNS + x)]);

                    Add(tri2);
                    //indices[i++] = (ushort)(y * COLUMNS + x);
                    //indices[i++] = (ushort)((y + 1) * COLUMNS + (x + 1));
                    //indices[i++] = (ushort)((y + 1) * COLUMNS + x);
                }
                Triangle tri3 = new Triangle(sVertices[(y * x_max + x)], sVertices[(y * x_max + 0)], sVertices[((y + 1) * x_max + 0)]);
                Add(tri3);
                // Wrap the last cell in the row around
                //indices[i++] = (ushort)(y * x_max + x); //a
                //indices[i++] = (ushort)(y * x_max + 0); //b
                //indices[i++] = (ushort)((y + 1) * x_max + 0); //c

                Triangle tri4 = new Triangle(sVertices[(y * x_max + x)], sVertices[((y + 1) * x_max + 0)], sVertices[((y + 1) * x_max + x)]);
                Add(tri4);
                //indices[i++] = (ushort)(y * x_max + x); //a
                //indices[i++] = (ushort)((y + 1) * x_max + 0); //b
                //indices[i++] = (ushort)((y + 1) * x_max + x); //c
            }
        }
    }
}
