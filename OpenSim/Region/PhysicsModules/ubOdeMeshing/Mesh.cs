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
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace OpenSim.Region.PhysicsModule.ubODEMeshing
{
    public class MeshBuildingData
    {
        public Dictionary<Vertex, int> m_vertices;
        public List<Triangle> m_triangles;
        public float m_obbXmin;
        public float m_obbXmax;
        public float m_obbYmin;
        public float m_obbYmax;
        public float m_obbZmin;
        public float m_obbZmax;
        public Vector3 m_centroid;
        public int m_centroidDiv;
    }

    [Serializable()]
    public class Mesh : IMesh
    {
        float[] vertices;
        int[] indexes;
        Vector3 m_obb;
        Vector3 m_obboffset;
        [NonSerialized()]
        MeshBuildingData m_bdata;
        [NonSerialized()]
        GCHandle vhandler;
        [NonSerialized()]
        GCHandle ihandler;
        [NonSerialized()]
        IntPtr m_verticesPtr = IntPtr.Zero;
        [NonSerialized()]
        IntPtr m_indicesPtr = IntPtr.Zero;
        [NonSerialized()]
        int m_vertexCount = 0;
        [NonSerialized()]
        int m_indexCount = 0;

        public int RefCount { get; set; }
        public AMeshKey Key { get; set; }

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

            m_bdata = new MeshBuildingData();
            m_bdata.m_vertices = new Dictionary<Vertex, int>(vcomp);
            m_bdata.m_triangles = new List<Triangle>();
            m_bdata.m_centroid = Vector3.Zero;
            m_bdata.m_centroidDiv = 0;
            m_bdata.m_obbXmin = float.MaxValue;
            m_bdata.m_obbXmax = float.MinValue;
            m_bdata.m_obbYmin = float.MaxValue;
            m_bdata.m_obbYmax = float.MinValue;
            m_bdata.m_obbZmin = float.MaxValue;
            m_bdata.m_obbZmax = float.MinValue;
            m_obb = new Vector3(0.5f, 0.5f, 0.5f);
            m_obboffset = Vector3.Zero;
        }


        public Mesh Scale(Vector3 scale)
        {
            if (m_verticesPtr == null || m_indicesPtr == null)
                return null;

            Mesh result = new Mesh();

            float x = scale.X;
            float y = scale.Y;
            float z = scale.Z;

            float tmp;
            tmp = m_obb.X * x;
            if(tmp < 0.0005f)
                tmp = 0.0005f;
            result.m_obb.X = tmp;

            tmp =  m_obb.Y * y;
            if(tmp < 0.0005f)
                tmp = 0.0005f;
            result.m_obb.Y = tmp;

            tmp =  m_obb.Z * z;
            if(tmp < 0.0005f)
                tmp = 0.0005f;
            result.m_obb.Z = tmp;

            result.m_obboffset.X = m_obboffset.X * x;
            result.m_obboffset.Y = m_obboffset.Y * y;
            result.m_obboffset.Z = m_obboffset.Z * z;

            result.vertices = new float[vertices.Length];
            int j = 0;
            for (int i = 0; i < m_vertexCount; i++)
            {
                result.vertices[j] = vertices[j] * x;
                j++;
                result.vertices[j] = vertices[j] * y;
                j++;
                result.vertices[j] = vertices[j] * z;
                j++;
            }

            result.indexes = new int[indexes.Length];
            indexes.CopyTo(result.indexes,0);

            result.pinMemory();

            return result;
        }

        public Mesh Clone()
        {
            Mesh result = new Mesh();

            if (m_bdata != null)
            {
                result.m_bdata = new MeshBuildingData();
                foreach (Triangle t in m_bdata.m_triangles)
                {
                    result.Add(new Triangle(t.v1.Clone(), t.v2.Clone(), t.v3.Clone()));
                }
                result.m_bdata.m_centroid = m_bdata.m_centroid;
                result.m_bdata.m_centroidDiv = m_bdata.m_centroidDiv;
                result.m_bdata.m_obbXmin = m_bdata.m_obbXmin;
                result.m_bdata.m_obbXmax = m_bdata.m_obbXmax;
                result.m_bdata.m_obbYmin = m_bdata.m_obbYmin;
                result.m_bdata.m_obbYmax = m_bdata.m_obbYmax;
                result.m_bdata.m_obbZmin = m_bdata.m_obbZmin;
                result.m_bdata.m_obbZmax = m_bdata.m_obbZmax;
            }
            result.m_obb = m_obb;
            result.m_obboffset = m_obboffset;
            return result;
        }

        public void addVertexLStats(Vertex v)
        {
            float x = v.X;
            float y = v.Y;
            float z = v.Z;

            m_bdata.m_centroid.X += x;
            m_bdata.m_centroid.Y += y;
            m_bdata.m_centroid.Z += z;
            m_bdata.m_centroidDiv++;

            if (x > m_bdata.m_obbXmax)
                m_bdata.m_obbXmax = x;
            if (x < m_bdata.m_obbXmin)
                m_bdata.m_obbXmin = x;

            if (y > m_bdata.m_obbYmax)
                m_bdata.m_obbYmax = y;
            if (y < m_bdata.m_obbYmin)
                m_bdata.m_obbYmin = y;

            if (z > m_bdata.m_obbZmax)
                m_bdata.m_obbZmax = z;
            if (z < m_bdata.m_obbZmin)
                m_bdata.m_obbZmin = z;

        }

        public void Add(Triangle triangle)
        {
            if (m_indicesPtr != IntPtr.Zero || m_verticesPtr != IntPtr.Zero)
                throw new NotSupportedException("Attempt to Add to a pinned Mesh");


            triangle.v1.X = (float)Math.Round(triangle.v1.X, 6);
            triangle.v1.Y = (float)Math.Round(triangle.v1.Y, 6);
            triangle.v1.Z = (float)Math.Round(triangle.v1.Z, 6);
            triangle.v2.X = (float)Math.Round(triangle.v2.X, 6);
            triangle.v2.Y = (float)Math.Round(triangle.v2.Y, 6);
            triangle.v2.Z = (float)Math.Round(triangle.v2.Z, 6);
            triangle.v3.X = (float)Math.Round(triangle.v3.X, 6);
            triangle.v3.Y = (float)Math.Round(triangle.v3.Y, 6);
            triangle.v3.Z = (float)Math.Round(triangle.v3.Z, 6);

            if ((triangle.v1.X == triangle.v2.X && triangle.v1.Y == triangle.v2.Y && triangle.v1.Z == triangle.v2.Z)
                || (triangle.v1.X == triangle.v3.X && triangle.v1.Y == triangle.v3.Y && triangle.v1.Z == triangle.v3.Z)
                || (triangle.v2.X == triangle.v3.X && triangle.v2.Y == triangle.v3.Y && triangle.v2.Z == triangle.v3.Z)
                )               
            {               
                return;
            }

            if (m_bdata.m_vertices.Count == 0)
            {
                m_bdata.m_centroidDiv = 0;
                m_bdata.m_centroid = Vector3.Zero;
            }

            if (!m_bdata.m_vertices.ContainsKey(triangle.v1))
            {
                m_bdata.m_vertices[triangle.v1] = m_bdata.m_vertices.Count;
                addVertexLStats(triangle.v1);
            }
            if (!m_bdata.m_vertices.ContainsKey(triangle.v2))
            {
                m_bdata.m_vertices[triangle.v2] = m_bdata.m_vertices.Count;
                addVertexLStats(triangle.v2);
            }
            if (!m_bdata.m_vertices.ContainsKey(triangle.v3))
            {
                m_bdata.m_vertices[triangle.v3] = m_bdata.m_vertices.Count;
                addVertexLStats(triangle.v3);
            }
            m_bdata.m_triangles.Add(triangle);
        }

        public Vector3 GetCentroid()
        {
            return m_obboffset;

        }

        public Vector3 GetOBB()
        {
            return m_obb;
/*
            float x, y, z;
            if (m_bdata.m_centroidDiv > 0)
            {
                x = (m_bdata.m_obbXmax - m_bdata.m_obbXmin) * 0.5f;
                y = (m_bdata.m_obbYmax - m_bdata.m_obbYmin) * 0.5f;
                z = (m_bdata.m_obbZmax - m_bdata.m_obbZmin) * 0.5f;
            }
            else // ??
            {
                x = 0.5f;
                y = 0.5f;
                z = 0.5f;
            }
            return new Vector3(x, y, z);
*/
        }

        public int numberVertices()
        {
            return m_bdata.m_vertices.Count;
        }

        public int numberTriangles()
        {
            return m_bdata.m_triangles.Count;
        }

        public List<Vector3> getVertexList()
        {
            List<Vector3> result = new List<Vector3>();
            foreach (Vertex v in m_bdata.m_vertices.Keys)
            {
                result.Add(new Vector3(v.X, v.Y, v.Z));
            }
            return result;
        }

        public float[] getVertexListAsFloat()
        {
            if (m_bdata.m_vertices == null)
                throw new NotSupportedException();
            float[] result = new float[m_bdata.m_vertices.Count * 3];
            foreach (KeyValuePair<Vertex, int> kvp in m_bdata.m_vertices)
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
            return null;
        }

        public void getVertexListAsPtrToFloatArray(out IntPtr _vertices, out int vertexStride, out int vertexCount)
        {
            // A vertex is 3 floats
            vertexStride = 3 * sizeof(float);

            // If there isn't an unmanaged array allocated yet, do it now
            if (m_verticesPtr == IntPtr.Zero && m_bdata != null)
            {
                vertices = getVertexListAsFloat();
                // Each vertex is 3 elements (floats)
                m_vertexCount = vertices.Length / 3;
                vhandler = GCHandle.Alloc(vertices, GCHandleType.Pinned);
                m_verticesPtr = vhandler.AddrOfPinnedObject();
                GC.AddMemoryPressure(Buffer.ByteLength(vertices));
            }
            _vertices = m_verticesPtr;
            vertexCount = m_vertexCount;
        }

        public int[] getIndexListAsInt()
        {
            if (m_bdata.m_triangles == null)
                throw new NotSupportedException();
            int[] result = new int[m_bdata.m_triangles.Count * 3];
            for (int i = 0; i < m_bdata.m_triangles.Count; i++)
            {
                Triangle t = m_bdata.m_triangles[i];
                result[3 * i + 0] = m_bdata.m_vertices[t.v1];
                result[3 * i + 1] = m_bdata.m_vertices[t.v2];
                result[3 * i + 2] = m_bdata.m_vertices[t.v3];
            }
            return result;
        }

        /// <summary>
        /// creates a list of index values that defines triangle faces. THIS METHOD FREES ALL NON-PINNED MESH DATA
        /// </summary>
        /// <returns></returns>
        public int[] getIndexListAsIntLocked()
        {
            return null;
        }

        public void getIndexListAsPtrToIntArray(out IntPtr indices, out int triStride, out int indexCount)
        {
            // If there isn't an unmanaged array allocated yet, do it now
            if (m_indicesPtr == IntPtr.Zero && m_bdata != null)
            {
                indexes = getIndexListAsInt();
                m_indexCount = indexes.Length;
                ihandler = GCHandle.Alloc(indexes, GCHandleType.Pinned);
                m_indicesPtr = ihandler.AddrOfPinnedObject();
                GC.AddMemoryPressure(Buffer.ByteLength(indexes));
            }
            // A triangle is 3 ints (indices)
            triStride = 3 * sizeof(int);
            indices = m_indicesPtr;
            indexCount = m_indexCount;
        }

        public void releasePinned()
        {
            if (m_verticesPtr != IntPtr.Zero)
            {
                vhandler.Free();
                vertices = null;
                m_verticesPtr = IntPtr.Zero;
            }
            if (m_indicesPtr != IntPtr.Zero)
            {
                ihandler.Free();
                indexes = null;
                m_indicesPtr = IntPtr.Zero;
            }
        }

        /// <summary>
        /// frees up the source mesh data to minimize memory - call this method after calling get*Locked() functions
        /// </summary>
        public void releaseSourceMeshData()
        {
            if (m_bdata != null)
            {
                m_bdata.m_triangles = null;
                m_bdata.m_vertices = null;
            }
        }

        public void releaseBuildingMeshData()
        {
            if (m_bdata != null)
            {
                m_bdata.m_triangles = null;
                m_bdata.m_vertices = null;
                m_bdata = null;
            }
        }

        public void Append(IMesh newMesh)
        {
            if (m_indicesPtr != IntPtr.Zero || m_verticesPtr != IntPtr.Zero)
                throw new NotSupportedException("Attempt to Append to a pinned Mesh");
        
            if (!(newMesh is Mesh))
                return;

            foreach (Triangle t in ((Mesh)newMesh).m_bdata.m_triangles)
                Add(t);
        }

        // Do a linear transformation of  mesh.
        public void TransformLinear(float[,] matrix, float[] offset)
        {
            if (m_indicesPtr != IntPtr.Zero || m_verticesPtr != IntPtr.Zero)
                throw new NotSupportedException("Attempt to TransformLinear a pinned Mesh");

            foreach (Vertex v in m_bdata.m_vertices.Keys)
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
            if (m_bdata == null)
                return;
            String fileName = name + "_" + title + ".raw";
            String completePath = System.IO.Path.Combine(path, fileName);
            StreamWriter sw = new StreamWriter(completePath);
            foreach (Triangle t in m_bdata.m_triangles)
            {
                String s = t.ToStringRaw();
                sw.WriteLine(s);
            }
            sw.Close();
        }

        public void TrimExcess()
        {
            m_bdata.m_triangles.TrimExcess();
        }

        public void pinMemory()
        {
            m_vertexCount = vertices.Length / 3;
            vhandler = GCHandle.Alloc(vertices, GCHandleType.Pinned);
            m_verticesPtr = vhandler.AddrOfPinnedObject();
            GC.AddMemoryPressure(Buffer.ByteLength(vertices));

            m_indexCount = indexes.Length;
            ihandler = GCHandle.Alloc(indexes, GCHandleType.Pinned);
            m_indicesPtr = ihandler.AddrOfPinnedObject();
            GC.AddMemoryPressure(Buffer.ByteLength(indexes));
        }

        public void PrepForOde()
        {
            // If there isn't an unmanaged array allocated yet, do it now
            if (m_verticesPtr == IntPtr.Zero)
                vertices = getVertexListAsFloat();

            // If there isn't an unmanaged array allocated yet, do it now
            if (m_indicesPtr == IntPtr.Zero)
                indexes = getIndexListAsInt();

            pinMemory();

            float x, y, z;

            if (m_bdata.m_centroidDiv > 0)
            {
                m_obboffset = new Vector3(m_bdata.m_centroid.X / m_bdata.m_centroidDiv, m_bdata.m_centroid.Y / m_bdata.m_centroidDiv, m_bdata.m_centroid.Z / m_bdata.m_centroidDiv);
                x = (m_bdata.m_obbXmax - m_bdata.m_obbXmin) * 0.5f;
                if(x < 0.0005f)
                    x = 0.0005f;
                y = (m_bdata.m_obbYmax - m_bdata.m_obbYmin) * 0.5f;
                if(y < 0.0005f)
                    y = 0.0005f;
                z = (m_bdata.m_obbZmax - m_bdata.m_obbZmin) * 0.5f;
                if(z < 0.0005f)
                    z = 0.0005f;
            }

            else
            {
                m_obboffset = Vector3.Zero;
                x = 0.5f;
                y = 0.5f;
                z = 0.5f;
            }

            m_obb = new Vector3(x, y, z);

            releaseBuildingMeshData();
        }
        public bool ToStream(Stream st)
        {
            if (m_indicesPtr == IntPtr.Zero || m_verticesPtr == IntPtr.Zero)
                return false;

            BinaryWriter bw = new BinaryWriter(st);
            bool ok = true;

            try
            {

                bw.Write(m_vertexCount);
                bw.Write(m_indexCount);

                for (int i = 0; i < 3 * m_vertexCount; i++)
                    bw.Write(vertices[i]);
                for (int i = 0; i < m_indexCount; i++)
                    bw.Write(indexes[i]);
                bw.Write(m_obb.X);
                bw.Write(m_obb.Y);
                bw.Write(m_obb.Z);
                bw.Write(m_obboffset.X);
                bw.Write(m_obboffset.Y);
                bw.Write(m_obboffset.Z);
            }
            catch
            {
                ok = false;
            }

            if (bw != null)
            {
                bw.Flush();
                bw.Close();
            }

            return ok;
        }

        public static Mesh FromStream(Stream st, AMeshKey key)
        {
            Mesh mesh = new Mesh();
            mesh.releaseBuildingMeshData();

            BinaryReader br = new BinaryReader(st);

            bool ok = true;
            try
            {
                mesh.m_vertexCount = br.ReadInt32();
                mesh.m_indexCount = br.ReadInt32();

                int n = 3 * mesh.m_vertexCount;
                mesh.vertices = new float[n];
                for (int i = 0; i < n; i++)
                    mesh.vertices[i] = br.ReadSingle();

                mesh.indexes = new int[mesh.m_indexCount];
                for (int i = 0; i < mesh.m_indexCount; i++)
                    mesh.indexes[i] = br.ReadInt32();

                mesh.m_obb.X = br.ReadSingle();
                mesh.m_obb.Y = br.ReadSingle();
                mesh.m_obb.Z = br.ReadSingle();

                mesh.m_obboffset.X = br.ReadSingle();
                mesh.m_obboffset.Y = br.ReadSingle();
                mesh.m_obboffset.Z = br.ReadSingle();
            }
            catch
            {
                ok = false;
            }

            br.Close();

            if (ok)
            {
                mesh.pinMemory();

                mesh.Key = key;
                mesh.RefCount = 1;

                return mesh;
            }

            mesh.vertices = null;
            mesh.indexes = null;
            return null;
        }
    }
}
