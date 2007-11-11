using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using System.Runtime.InteropServices;


using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.Meshing
{
    public class Mesh : IMesh
    {
        public List<Vertex> vertices;
        public List<Triangle> triangles;

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
                iV1 = this.vertices.IndexOf(t.v1);
                iV2 = this.vertices.IndexOf(t.v2);
                iV3 = this.vertices.IndexOf(t.v3);

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

        public void RemoveTrianglesOutside(SimpleHull hull)
        {
            int i;

            for (i = 0; i < triangles.Count; i++)
            {
                Triangle t = triangles[i];
                Vertex v1 = t.v1;
                Vertex v2 = t.v2;
                Vertex v3 = t.v3;
                PhysicsVector m = v1 + v2 + v3;
                m /= 3.0f;
                if (!hull.IsPointIn(new Vertex(m)))
                {
                    triangles.RemoveAt(i);
                    i--;
                }
            }
        }


        public void Add(List<Vertex> lv)
        {
            foreach (Vertex v in lv)
            {
                vertices.Add(v);
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
            float[] result = new float[vertices.Count * 3];
            for (int i = 0; i < vertices.Count; i++)
            {
                Vertex v = vertices[i];
                if (v == null)
                    continue;
                result[3 * i + 0] = v.X;
                result[3 * i + 1] = v.Y;
                result[3 * i + 2] = v.Z;
            }
            GCHandle.Alloc(result, GCHandleType.Pinned);
            return result;
        }

        public int[] getIndexListAsInt()
        {
            int[] result = new int[triangles.Count * 3];
            for (int i = 0; i < triangles.Count; i++)
            {
                Triangle t = triangles[i];
                result[3 * i + 0] = vertices.IndexOf(t.v1);
                result[3 * i + 1] = vertices.IndexOf(t.v2);
                result[3 * i + 2] = vertices.IndexOf(t.v3);
            }
            return result;
        }

        public int[] getIndexListAsIntLocked()
        {
            int[] result = getIndexListAsInt();
            GCHandle.Alloc(result, GCHandleType.Pinned);
            return result;
        }


        public void Append(Mesh newMesh)
        {
            foreach (Vertex v in newMesh.vertices)
                vertices.Add(v);

            foreach (Triangle t in newMesh.triangles)
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
                x = v.X * matrix[0, 0] + v.Y * matrix[1, 0] + v.Z * matrix[2, 0];
                y = v.X * matrix[0, 1] + v.Y * matrix[1, 1] + v.Z * matrix[2, 1];
                z = v.X * matrix[0, 2] + v.Y * matrix[1, 2] + v.Z * matrix[2, 2];
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
