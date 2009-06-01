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
using System.Runtime.InteropServices;
using OpenSim.Region.Physics.Manager;
using PrimMesher;

namespace OpenSim.Region.Physics.Meshing
{
    public class Mesh : IMesh
    {
        public List<Vertex> vertices;
        public List<Triangle> triangles;
        GCHandle pinnedVirtexes;
        GCHandle pinnedIndex;
        public PrimMesh primMesh = null;
        public float[] normals;

        public Mesh()
        {
            vertices = new List<Vertex>();
            triangles = new List<Triangle>();
        }

        public Mesh Clone()
        {
            Mesh result = new Mesh();

            foreach (Vertex v in vertices)
            {
                if (v == null)
                    result.vertices.Add(null);
                else
                    result.vertices.Add(v.Clone());
            }

            foreach (Triangle t in triangles)
            {
                int iV1, iV2, iV3;
                iV1 = vertices.IndexOf(t.v1);
                iV2 = vertices.IndexOf(t.v2);
                iV3 = vertices.IndexOf(t.v3);

                Triangle newT = new Triangle(result.vertices[iV1], result.vertices[iV2], result.vertices[iV3]);
                result.Add(newT);
            }

            return result;
        }

        public void Add(Triangle triangle)
        {
            int i;
            i = vertices.IndexOf(triangle.v1);
            if (i < 0)
                throw new ArgumentException("Vertex v1 not known to mesh");
            i = vertices.IndexOf(triangle.v2);
            if (i < 0)
                throw new ArgumentException("Vertex v2 not known to mesh");
            i = vertices.IndexOf(triangle.v3);
            if (i < 0)
                throw new ArgumentException("Vertex v3 not known to mesh");

            triangles.Add(triangle);
        }

        public void Add(Vertex v)
        {
            vertices.Add(v);
        }

        public void Remove(Vertex v)
        {
            int i;

            // First, remove all triangles that are build on v
            for (i = 0; i < triangles.Count; i++)
            {
                Triangle t = triangles[i];
                if (t.v1 == v || t.v2 == v || t.v3 == v)
                {
                    triangles.RemoveAt(i);
                    i--;
                }
            }

            // Second remove v itself
            vertices.Remove(v);
        }

        public void Add(List<Vertex> lv)
        {
            foreach (Vertex v in lv)
            {
                vertices.Add(v);
            }
        }

        public void CalcNormals()
        {
            int iTriangles = triangles.Count;

            this.normals = new float[iTriangles * 3];

            int i = 0;
            foreach (Triangle t in triangles)
            {
                float ux, uy, uz;
                float vx, vy, vz;
                float wx, wy, wz;

                ux = t.v1.X;
                uy = t.v1.Y;
                uz = t.v1.Z;

                vx = t.v2.X;
                vy = t.v2.Y;
                vz = t.v2.Z;

                wx = t.v3.X;
                wy = t.v3.Y;
                wz = t.v3.Z;


                // Vectors for edges
                float e1x, e1y, e1z;
                float e2x, e2y, e2z;

                e1x = ux - vx;
                e1y = uy - vy;
                e1z = uz - vz;

                e2x = ux - wx;
                e2y = uy - wy;
                e2z = uz - wz;


                // Cross product for normal
                float nx, ny, nz;
                nx = e1y * e2z - e1z * e2y;
                ny = e1z * e2x - e1x * e2z;
                nz = e1x * e2y - e1y * e2x;

                // Length
                float l = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                float lReciprocal = 1.0f / l;

                // Normalized "normal"
                //nx /= l;
                //ny /= l;
                //nz /= l;

                normals[i] = nx * lReciprocal;
                normals[i + 1] = ny * lReciprocal;
                normals[i + 2] = nz * lReciprocal;

                i += 3;
            }
        }

        public List<PhysicsVector> getVertexList()
        {
            List<PhysicsVector> result = new List<PhysicsVector>();
            foreach (Vertex v in vertices)
            {
                result.Add(v);
            }
            return result;
        }

        public float[] getVertexListAsFloatLocked()
        {
            float[] result;

            if (primMesh == null)
            {
                result = new float[vertices.Count * 3];
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vertex v = vertices[i];
                    if (v == null)
                        continue;
                    result[3 * i + 0] = v.X;
                    result[3 * i + 1] = v.Y;
                    result[3 * i + 2] = v.Z;
                }
                pinnedVirtexes = GCHandle.Alloc(result, GCHandleType.Pinned);
            }
            else
            {
                int count = primMesh.coords.Count;
                result = new float[count * 3];
                for (int i = 0; i < count; i++)
                {
                    Coord c = primMesh.coords[i];
                    {
                        int resultIndex = 3 * i;
                        result[resultIndex] = c.X;
                        result[resultIndex + 1] = c.Y;
                        result[resultIndex + 2] = c.Z;
                    }

                }
                pinnedVirtexes = GCHandle.Alloc(result, GCHandleType.Pinned);
            }
            return result;
        }

        public int[] getIndexListAsInt()
        {
            int[] result;

            if (primMesh == null)
            {
                result = new int[triangles.Count * 3];
                for (int i = 0; i < triangles.Count; i++)
                {
                    Triangle t = triangles[i];
                    result[3 * i + 0] = vertices.IndexOf(t.v1);
                    result[3 * i + 1] = vertices.IndexOf(t.v2);
                    result[3 * i + 2] = vertices.IndexOf(t.v3);
                }
            }
            else
            {
                int numFaces = primMesh.faces.Count;
                result = new int[numFaces * 3];
                for (int i = 0; i < numFaces; i++)
                {
                    Face f = primMesh.faces[i];
//                    Coord c1 = primMesh.coords[f.v1];
//                    Coord c2 = primMesh.coords[f.v2];
//                    Coord c3 = primMesh.coords[f.v3];

                    int resultIndex = i * 3;
                    result[resultIndex] = f.v1;
                    result[resultIndex + 1] = f.v2;
                    result[resultIndex + 2] = f.v3;
                }
            }
            return result;
        }

        /// <summary>
        /// creates a list of index values that defines triangle faces. THIS METHOD FREES ALL NON-PINNED MESH DATA
        /// </summary>
        /// <returns></returns>
        public int[] getIndexListAsIntLocked()
        {
            int[] result = getIndexListAsInt();
            pinnedIndex = GCHandle.Alloc(result, GCHandleType.Pinned);

            return result;
        }

        public void releasePinned()
        {
            pinnedVirtexes.Free();
            pinnedIndex.Free();
        }

        /// <summary>
        /// frees up the source mesh data to minimize memory - call this method after calling get*Locked() functions
        /// </summary>
        public void releaseSourceMeshData()
        {
            triangles = null;
            vertices = null;
            primMesh = null;
        }

        public void Append(IMesh newMesh)
        {
            Mesh newMesh2;
            if (newMesh is Mesh)
            {
                newMesh2 = (Mesh)newMesh;
            }
            else
            {
                return;
            }

            foreach (Vertex v in newMesh2.vertices)
                vertices.Add(v);

            foreach (Triangle t in newMesh2.triangles)
                Add(t);
        }

        // Do a linear transformation of  mesh.
        public void TransformLinear(float[,] matrix, float[] offset)
        {
            foreach (Vertex v in vertices)
            {
                if (v == null)
                    continue;
                float x, y, z;
                x = v.X*matrix[0, 0] + v.Y*matrix[1, 0] + v.Z*matrix[2, 0];
                y = v.X*matrix[0, 1] + v.Y*matrix[1, 1] + v.Z*matrix[2, 1];
                z = v.X*matrix[0, 2] + v.Y*matrix[1, 2] + v.Z*matrix[2, 2];
                v.X = x + offset[0];
                v.Y = y + offset[1];
                v.Z = z + offset[2];
            }
        }

        public void DumpRaw(String path, String name, String title)
        {
            if (path == null)
                return;
            String fileName = name + "_" + title + ".raw";
            String completePath = Path.Combine(path, fileName);
            StreamWriter sw = new StreamWriter(completePath);
            foreach (Triangle t in triangles)
            {
                String s = t.ToStringRaw();
                sw.WriteLine(s);
            }
            sw.Close();
        }
    }
}
