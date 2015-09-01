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

using System.Drawing;
using System.Drawing.Imaging;

namespace PrimMesher
{

    public class SculptMesh
    {
        public List<Coord> coords;
        public List<Face> faces;

        public enum SculptType { sphere = 1, torus = 2, plane = 3, cylinder = 4 };


        public SculptMesh(Bitmap sculptBitmap, SculptType sculptType, int lod, bool mirror, bool invert)
        {
            if (mirror)
                invert = !invert;

            SculptMap smap = new SculptMap(sculptBitmap, lod);

            List<List<Coord>> rows = smap.ToRows(mirror);

            _SculptMesh(rows, sculptType, invert);
        }

        private void _SculptMesh(List<List<Coord>> rows, SculptType sculptType, bool invert)
        {
            coords = new List<Coord>();
            faces = new List<Face>();

            sculptType = (SculptType)(((int)sculptType) & 0x07);

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

                    if (imageY > 0 && imageX > 0)
                    {
                        Face f1, f2;

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

                        this.faces.Add(f1);
                        this.faces.Add(f2);
                    }
                }
            }
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
        }

        public void Scale(float x, float y, float z)
        {
            int i;
            int numVerts = this.coords.Count;

            Coord m = new Coord(x, y, z);
            for (i = 0; i < numVerts; i++)
                this.coords[i] *= m;
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
