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
using OpenSim.Region.PhysicsModules.SharedBase;
using PrimMesher;
using OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.Meshing
{
    public class Mesh : IMesh
    {
        private Dictionary<Vertex, int> m_vertices;
        private List<Triangle> m_triangles;
        GCHandle m_pinnedVertexes;
        GCHandle m_pinnedIndex;
        IntPtr m_verticesPtr = IntPtr.Zero;
        int m_vertexCount = 0;
        IntPtr m_indicesPtr = IntPtr.Zero;
        int m_indexCount = 0;
        public float[] m_normals;
        Vector3 _centroid;
        int _centroidDiv;

        private class vertexcomp : IEqualityComparer<Vertex>
        {
            public bool Equals(Vertex v1, Vertex v2)
            {
                if (v1.X == v2.X && v1.Y == v2.Y && v1.Z == v2.Z)
                    return true;
                else
                    return false;
            }
            public int GetHashCode(Vertex v)
            {
                int a = v.X.GetHashCode();
                int b = v.Y.GetHashCode();
                int c = v.Z.GetHashCode();
                return (a << 16) ^ (b << 8) ^ c;
            }

        }

        public Mesh()
        {
            vertexcomp vcomp = new vertexcomp();

            m_vertices = new Dictionary<Vertex, int>(vcomp);
            m_triangles = new List<Triangle>();
            _centroid = Vector3.Zero;
            _centroidDiv = 0;
        }

        public Mesh Clone()
        {
            Mesh result = new Mesh();

            foreach (Triangle t in m_triangles)
            {
                result.Add(new Triangle(t.v1.Clone(), t.v2.Clone(), t.v3.Clone()));
            }
            result._centroid = _centroid;
            result._centroidDiv = _centroidDiv;
            return result;
        }

        public void Add(Triangle triangle)
        {
            if (m_pinnedIndex.IsAllocated || m_pinnedVertexes.IsAllocated || m_indicesPtr != IntPtr.Zero || m_verticesPtr != IntPtr.Zero)
                throw new NotSupportedException("Attempt to Add to a pinned Mesh");
            // If a vertex of the triangle is not yet in the vertices list,
            // add it and set its index to the current index count
            // vertex == seems broken
            // skip colapsed triangles
            if ((triangle.v1.X == triangle.v2.X && triangle.v1.Y == triangle.v2.Y && triangle.v1.Z == triangle.v2.Z)
                || (triangle.v1.X == triangle.v3.X && triangle.v1.Y == triangle.v3.Y && triangle.v1.Z == triangle.v3.Z)
                || (triangle.v2.X == triangle.v3.X && triangle.v2.Y == triangle.v3.Y && triangle.v2.Z == triangle.v3.Z)
                )               
            {               
                return;
            }

            if (m_vertices.Count == 0)
            {
                _centroidDiv = 0;
                _centroid = Vector3.Zero;
            }

            if (!m_vertices.ContainsKey(triangle.v1))
            {
                m_vertices[triangle.v1] = m_vertices.Count;
                _centroid.X += triangle.v1.X;
                _centroid.Y += triangle.v1.Y;
                _centroid.Z += triangle.v1.Z;
                _centroidDiv++;
            }
            if (!m_vertices.ContainsKey(triangle.v2))
            {
                m_vertices[triangle.v2] = m_vertices.Count;
                _centroid.X += triangle.v2.X;
                _centroid.Y += triangle.v2.Y;
                _centroid.Z += triangle.v2.Z;
                _centroidDiv++;
            }
            if (!m_vertices.ContainsKey(triangle.v3))
            {
                m_vertices[triangle.v3] = m_vertices.Count;
                _centroid.X += triangle.v3.X;
                _centroid.Y += triangle.v3.Y;
                _centroid.Z += triangle.v3.Z;
                _centroidDiv++;
            }
            m_triangles.Add(triangle);
        }

        public Vector3 GetCentroid()
        {
            if (_centroidDiv > 0)
                return new Vector3(_centroid.X / _centroidDiv, _centroid.Y / _centroidDiv, _centroid.Z / _centroidDiv);
            else
                return Vector3.Zero;
        }

        // not functional
        public Vector3 GetOBB()
        {
            return new Vector3(0.5f, 0.5f, 0.5f);
        }

        public void CalcNormals()
        {
            int iTriangles = m_triangles.Count;

            this.m_normals = new float[iTriangles * 3];

            int i = 0;
            foreach (Triangle t in m_triangles)
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

                m_normals[i] = nx * lReciprocal;
                m_normals[i + 1] = ny * lReciprocal;
                m_normals[i + 2] = nz * lReciprocal;

                i += 3;
            }
        }

        public List<Vector3> getVertexList()
        {
            List<Vector3> result = new List<Vector3>();
            foreach (Vertex v in m_vertices.Keys)
            {
                result.Add(new Vector3(v.X, v.Y, v.Z));
            }
            return result;
        }

        public float[] getVertexListAsFloat()
        {
            if (m_vertices == null)
                throw new NotSupportedException();
            float[] result = new float[m_vertices.Count * 3];
            foreach (KeyValuePair<Vertex, int> kvp in m_vertices)
            {
                Vertex v = kvp.Key;
                int i = kvp.Value;
                result[3 * i + 0] = v.X;
                result[3 * i + 1] = v.Y;
                result[3 * i + 2] = v.Z;
            }
            return result;
        }

        public float[] getVertexListAsFloatLocked()
        {
            if (m_pinnedVertexes.IsAllocated)
                return (float[])(m_pinnedVertexes.Target);

            float[] result = getVertexListAsFloat();
            m_pinnedVertexes = GCHandle.Alloc(result, GCHandleType.Pinned);
            // Inform the garbage collector of this unmanaged allocation so it can schedule
            // the next GC round more intelligently
            GC.AddMemoryPressure(Buffer.ByteLength(result));

            return result;
        }

        public void getVertexListAsPtrToFloatArray(out IntPtr vertices, out int vertexStride, out int vertexCount)
        {
            // A vertex is 3 floats
            
            vertexStride = 3 * sizeof(float);

            // If there isn't an unmanaged array allocated yet, do it now
            if (m_verticesPtr == IntPtr.Zero)
            {
                float[] vertexList = getVertexListAsFloat();
                // Each vertex is 3 elements (floats)
                m_vertexCount = vertexList.Length / 3;
                int byteCount = m_vertexCount * vertexStride;
                m_verticesPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(byteCount);
                System.Runtime.InteropServices.Marshal.Copy(vertexList, 0, m_verticesPtr, m_vertexCount * 3);
            }
            vertices = m_verticesPtr;
            vertexCount = m_vertexCount;
        }

        public int[] getIndexListAsInt()
        {
            if (m_triangles == null)
                throw new NotSupportedException();
            int[] result = new int[m_triangles.Count * 3];
            for (int i = 0; i < m_triangles.Count; i++)
            {
                Triangle t = m_triangles[i];
                result[3 * i + 0] = m_vertices[t.v1];
                result[3 * i + 1] = m_vertices[t.v2];
                result[3 * i + 2] = m_vertices[t.v3];
            }
            return result;
        }

        /// <summary>
        /// creates a list of index values that defines triangle faces. THIS METHOD FREES ALL NON-PINNED MESH DATA
        /// </summary>
        /// <returns></returns>
        public int[] getIndexListAsIntLocked()
        {
            if (m_pinnedIndex.IsAllocated)
                return (int[])(m_pinnedIndex.Target);
        
            int[] result = getIndexListAsInt();
            m_pinnedIndex = GCHandle.Alloc(result, GCHandleType.Pinned);
            // Inform the garbage collector of this unmanaged allocation so it can schedule
            // the next GC round more intelligently
            GC.AddMemoryPressure(Buffer.ByteLength(result));

            return result;
        }

        public void getIndexListAsPtrToIntArray(out IntPtr indices, out int triStride, out int indexCount)
        {
            // If there isn't an unmanaged array allocated yet, do it now
            if (m_indicesPtr == IntPtr.Zero)
            {
                int[] indexList = getIndexListAsInt();
                m_indexCount = indexList.Length;
                int byteCount = m_indexCount * sizeof(int);
                m_indicesPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(byteCount);
                System.Runtime.InteropServices.Marshal.Copy(indexList, 0, m_indicesPtr, m_indexCount);
            }
            // A triangle is 3 ints (indices)
            triStride = 3 * sizeof(int);
            indices = m_indicesPtr;
            indexCount = m_indexCount;
        }

        public void releasePinned()
        {
            if (m_pinnedVertexes.IsAllocated)
                m_pinnedVertexes.Free();
            if (m_pinnedIndex.IsAllocated)
                m_pinnedIndex.Free();
            if (m_verticesPtr != IntPtr.Zero)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(m_verticesPtr);
                m_verticesPtr = IntPtr.Zero;
            }
            if (m_indicesPtr != IntPtr.Zero)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(m_indicesPtr);
                m_indicesPtr = IntPtr.Zero;
            }
        }

        /// <summary>
        /// frees up the source mesh data to minimize memory - call this method after calling get*Locked() functions
        /// </summary>
        public void releaseSourceMeshData()
        {
            m_triangles = null;
            m_vertices = null;
        }

        public void Append(IMesh newMesh)
        {
            if (m_pinnedIndex.IsAllocated || m_pinnedVertexes.IsAllocated || m_indicesPtr != IntPtr.Zero || m_verticesPtr != IntPtr.Zero)
                throw new NotSupportedException("Attempt to Append to a pinned Mesh");
        
            if (!(newMesh is Mesh))
                return;

            foreach (Triangle t in ((Mesh)newMesh).m_triangles)
                Add(t);
        }

        // Do a linear transformation of  mesh.
        public void TransformLinear(float[,] matrix, float[] offset)
        {
            if (m_pinnedIndex.IsAllocated || m_pinnedVertexes.IsAllocated || m_indicesPtr != IntPtr.Zero || m_verticesPtr != IntPtr.Zero)
                throw new NotSupportedException("Attempt to TransformLinear a pinned Mesh");
        
            foreach (Vertex v in m_vertices.Keys)
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
            String completePath = System.IO.Path.Combine(path, fileName);
            StreamWriter sw = new StreamWriter(completePath);
            foreach (Triangle t in m_triangles)
            {
                String s = t.ToStringRaw();
                sw.WriteLine(s);
            }
            sw.Close();
        }

        public void TrimExcess()
        {
            m_triangles.TrimExcess();
        }
    }
}
