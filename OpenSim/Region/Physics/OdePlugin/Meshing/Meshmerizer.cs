using System;
using System.Globalization;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

using OpenSim.Framework.Types;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.OdePlugin
{
    public class Mesh
    {
        public List<Vertex> vertices;
        public List<Triangle> triangles;

        public float[] normals;

        public Mesh()
        {
            vertices = new List<Vertex>();
            triangles = new List<Triangle>();
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


        public float[] getVertexListAsFloat()
        {
            float[] result = new float[vertices.Count * 3];
            for (int i = 0; i < vertices.Count; i++)
            {
                Vertex v = vertices[i];
                PhysicsVector point = v.point;
                result[3 * i + 0] = point.X;
                result[3 * i + 1] = point.Y;
                result[3 * i + 2] = point.Z;
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
    }



    public class Meshmerizer
    {

        static List<Triangle> FindInfluencedTriangles(List<Triangle> triangles, Vertex v) 
        {
            List<Triangle> influenced = new List<Triangle>();
            foreach (Triangle t in triangles)
            {
                float dx, dy;

                if (t.isInCircle(v.point.X, v.point.Y))
                {
                    influenced.Add(t);
                }
            }
            return influenced;
        }
        
        
        static void InsertVertices(List<Vertex> vertices, int usedForSeed, List<Triangle> triangles, List<int> innerBorders) 
        {
            // This is a variant of the delaunay algorithm
            // each time a new vertex is inserted, all triangles that are influenced by it are deleted
            // and replaced by new ones including the new vertex
            // It is not very time efficient but easy to implement.

            int iCurrentVertex;
            int iMaxVertex=vertices.Count;
            for (iCurrentVertex = usedForSeed; iCurrentVertex < iMaxVertex; iCurrentVertex++)
            {
                // Background: A triangle mesh fulfills the delaunay condition if (iff!)
                // each circumlocutory circle (i.e. the circle that touches all three corners) 
                // of each triangle is empty of other vertices.
                // Obviously a single (seeding) triangle fulfills this condition.
                // If we now add one vertex, we need to reconstruct all triangles, that
                // do not fulfill this condition with respect to the new triangle

                // Find the triangles that are influenced by the new vertex
                Vertex v=vertices[iCurrentVertex];
                List<Triangle> influencedTriangles=FindInfluencedTriangles(triangles, v);

                List<Simplex> simplices = new List<Simplex>();

                // Reconstruction phase. First step, dissolve each triangle into it's simplices,
                // i.e. it's "border lines"
                // Goal is to find "inner" borders and delete them, while the hull gets conserved.
                // Inner borders are special in the way that they always come twice, which is how we detect them 
                foreach (Triangle t in influencedTriangles)
                {
                    List<Simplex> newSimplices = t.GetSimplices();
                    simplices.AddRange(newSimplices);
                    triangles.Remove(t);
                }
                // Now sort the simplices. That will make identical ones side by side in the list
                simplices.Sort();

                // Look for duplicate simplices here. 
                // Remember, they are directly side by side in the list right now
                int iSimplex;
                List<Simplex> innerSimplices=new List<Simplex>();
                for (iSimplex = 1; iSimplex < simplices.Count; iSimplex++) // Startindex=1, so we can refer backwards
                {
                    if (simplices[iSimplex - 1].CompareTo(simplices[iSimplex])==0)
                    {
                        innerSimplices.Add(simplices[iSimplex - 1]);
                        innerSimplices.Add(simplices[iSimplex]);
                    }
                }

                foreach (Simplex s in innerSimplices)
                {
                        simplices.Remove(s);
                }

                // each simplex still in the list belongs to the hull of the region in question
                // The new vertex (yes, we still deal with verices here :-) ) forms a triangle 
                // With each of these simplices. Build the new triangles and add them to the list
                foreach (Simplex s in simplices)
                {
                    Triangle t = new Triangle(s.v1, s.v2, vertices[iCurrentVertex]);
                    triangles.Add(t);
                }
            }

            // At this point all vertices should be inserted into the mesh
            // But the areas, that should be kept free still are filled with triangles
            // We have to remove them. For this we have a list of indices to vertices.
            // Each triangle that solemnly constists of vertices from the inner border
            // are deleted

            List<Triangle> innerTriangles = new List<Triangle>();
            foreach (Triangle t in triangles)
            {
                if (
                       innerBorders.Contains(vertices.IndexOf(t.v1))
                    && innerBorders.Contains(vertices.IndexOf(t.v2))
                    && innerBorders.Contains(vertices.IndexOf(t.v3))
                    )
                    innerTriangles.Add(t);
            }
            foreach (Triangle t in innerTriangles)
            {
                triangles.Remove(t);
            }
        }


        static Mesh CreateBoxMeshX(PrimitiveBaseShape primShape, PhysicsVector size)
        // Builds the x (+ and -) surfaces of a box shaped prim
        {
            UInt16 hollowFactor = primShape.ProfileHollow;
            Mesh meshMX = new Mesh();


            // Surface 0, -X
            meshMX.Add(new Vertex("-X-Y-Z", -size.X / 2.0f, -size.Y / 2.0f, -size.Z / 2.0f));
            meshMX.Add(new Vertex("-X+Y-Z", -size.X / 2.0f, +size.Y / 2.0f, -size.Z / 2.0f));
            meshMX.Add(new Vertex("-X-Y+Z", -size.X / 2.0f, -size.Y / 2.0f, +size.Z / 2.0f));
            meshMX.Add(new Vertex("-X+Y+Z", -size.X / 2.0f, +size.Y / 2.0f, +size.Z / 2.0f));

            meshMX.Add(new Triangle(meshMX.vertices[0], meshMX.vertices[2], meshMX.vertices[1]));
            meshMX.Add(new Triangle(meshMX.vertices[1], meshMX.vertices[2], meshMX.vertices[3]));


            Mesh meshPX = new Mesh();
            // Surface 1, +X
            meshPX.Add(new Vertex("+X-Y-Z", +size.X / 2.0f, -size.Y / 2.0f, -size.Z / 2.0f));
            meshPX.Add(new Vertex("+X+Y-Z", +size.X / 2.0f, +size.Y / 2.0f, -size.Z / 2.0f));
            meshPX.Add(new Vertex("+X-Y+Z", +size.X / 2.0f, -size.Y / 2.0f, +size.Z / 2.0f));
            meshPX.Add(new Vertex("+X+Y+Z", +size.X / 2.0f, +size.Y / 2.0f, +size.Z / 2.0f));


            meshPX.Add(new Triangle(meshPX.vertices[0], meshPX.vertices[1], meshPX.vertices[2]));
            meshPX.Add(new Triangle(meshPX.vertices[2], meshPX.vertices[1], meshPX.vertices[3]));


            if (hollowFactor > 0)
            {
                float hollowFactorF = (float)hollowFactor / (float)50000;

                Vertex IPP;
                Vertex IPM;
                Vertex IMP;
                Vertex IMM;

                IPP = new Vertex("Inner-X+Y+Z", -size.X * hollowFactorF / 2.0f, +size.Y * hollowFactorF / 2.0f, +size.Z / 2.0f);
                IPM = new Vertex("Inner-X+Y-Z", -size.X * hollowFactorF / 2.0f, +size.Y * hollowFactorF / 2.0f, -size.Z / 2.0f);
                IMP = new Vertex("Inner-X-Y+Z", -size.X * hollowFactorF / 2.0f, -size.Y * hollowFactorF / 2.0f, +size.Z / 2.0f);
                IMM = new Vertex("Inner-X-Y-Z", -size.X * hollowFactorF / 2.0f, -size.Y * hollowFactorF / 2.0f, -size.Z / 2.0f);

                meshMX.Add(IPP);
                meshMX.Add(IPM);
                meshMX.Add(IMP);
                meshMX.Add(IMM);

                meshMX.Add(new Triangle(IPP, IMP, IPM));
                meshMX.Add(new Triangle(IPM, IMP, IMM));

                foreach (Triangle t in meshMX.triangles)
                {
                    PhysicsVector n = t.getNormal();
                }



                IPP = new Vertex("Inner+X+Y+Z", +size.X * hollowFactorF / 2.0f, +size.Y * hollowFactorF / 2.0f, +size.Z / 2.0f);
                IPM = new Vertex("Inner+X+Y-Z", +size.X * hollowFactorF / 2.0f, +size.Y * hollowFactorF / 2.0f, -size.Z / 2.0f);
                IMP = new Vertex("Inner+X-Y+Z", +size.X * hollowFactorF / 2.0f, -size.Y * hollowFactorF / 2.0f, +size.Z / 2.0f);
                IMM = new Vertex("Inner+X-Y-Z", +size.X * hollowFactorF / 2.0f, -size.Y * hollowFactorF / 2.0f, -size.Z / 2.0f);

                meshPX.Add(IPP);
                meshPX.Add(IPM);
                meshPX.Add(IMP);
                meshPX.Add(IMM);

                meshPX.Add(new Triangle(IPP, IPM, IMP));
                meshPX.Add(new Triangle(IMP, IPM, IMM));

                foreach (Triangle t in meshPX.triangles)
                {
                    PhysicsVector n = t.getNormal();
                }
            }

            Mesh result = new Mesh();
            result.Append(meshMX);
            result.Append(meshPX);

            return result;
        }



        static Mesh CreateBoxMeshY(PrimitiveBaseShape primShape, PhysicsVector size)
        // Builds the y (+ and -) surfaces of a box shaped prim
        {
            UInt16 hollowFactor = primShape.ProfileHollow;

            // (M)inus Y
            Mesh MeshMY = new Mesh();
            MeshMY.Add(new Vertex("-X-Y-Z", -size.X / 2.0f, -size.Y / 2.0f, -size.Z / 2.0f));
            MeshMY.Add(new Vertex("+X-Y-Z", +size.X / 2.0f, -size.Y / 2.0f, -size.Z / 2.0f));
            MeshMY.Add(new Vertex("-X-Y+Z", -size.X / 2.0f, -size.Y / 2.0f, +size.Z / 2.0f));
            MeshMY.Add(new Vertex("+X-Y+Z", +size.X / 2.0f, -size.Y / 2.0f, +size.Z / 2.0f));

            MeshMY.Add(new Triangle(MeshMY.vertices[0], MeshMY.vertices[1], MeshMY.vertices[2]));
            MeshMY.Add(new Triangle(MeshMY.vertices[2], MeshMY.vertices[1], MeshMY.vertices[3]));

            // (P)lus Y
            Mesh MeshPY = new Mesh();

            MeshPY.Add(new Vertex("-X+Y-Z", -size.X / 2.0f, +size.Y / 2.0f, -size.Z / 2.0f));
            MeshPY.Add(new Vertex("+X+Y-Z", +size.X / 2.0f, +size.Y / 2.0f, -size.Z / 2.0f));
            MeshPY.Add(new Vertex("-X+Y+Z", -size.X / 2.0f, +size.Y / 2.0f, +size.Z / 2.0f));
            MeshPY.Add(new Vertex("+X+Y+Z", +size.X / 2.0f, +size.Y / 2.0f, +size.Z / 2.0f));

            MeshPY.Add(new Triangle(MeshPY.vertices[1], MeshPY.vertices[0], MeshPY.vertices[2]));
            MeshPY.Add(new Triangle(MeshPY.vertices[1], MeshPY.vertices[2], MeshPY.vertices[3]));

            if (hollowFactor > 0)
            {
                float hollowFactorF = (float)hollowFactor / (float)50000;

                Vertex IPP;
                Vertex IPM;
                Vertex IMP;
                Vertex IMM;

                IPP = new Vertex("Inner+X-Y+Z", +size.X * hollowFactorF / 2.0f, -size.Y * hollowFactorF / 2.0f, +size.Z / 2.0f);
                IPM = new Vertex("Inner+X-Y-Z", +size.X * hollowFactorF / 2.0f, -size.Y * hollowFactorF / 2.0f, -size.Z / 2.0f);
                IMP = new Vertex("Inner-X-Y+Z", -size.X * hollowFactorF / 2.0f, -size.Y * hollowFactorF / 2.0f, +size.Z / 2.0f);
                IMM = new Vertex("Inner-X-Y-Z", -size.X * hollowFactorF / 2.0f, -size.Y * hollowFactorF / 2.0f, -size.Z / 2.0f);

                MeshMY.Add(IPP);
                MeshMY.Add(IPM);
                MeshMY.Add(IMP);
                MeshMY.Add(IMM);

                MeshMY.Add(new Triangle(IPP, IPM, IMP));
                MeshMY.Add(new Triangle(IMP, IPM, IMM));

                foreach (Triangle t in MeshMY.triangles)
                {
                    PhysicsVector n = t.getNormal();
                }



                IPP = new Vertex("Inner+X+Y+Z", +size.X * hollowFactorF / 2.0f, +size.Y * hollowFactorF / 2.0f, +size.Z / 2.0f);
                IPM=new Vertex("Inner+X+Y-Z", +size.X * hollowFactorF / 2.0f, +size.Y  * hollowFactorF / 2.0f, -size.Z / 2.0f);
                IMP=new Vertex("Inner-X+Y+Z", -size.X * hollowFactorF / 2.0f, +size.Y  * hollowFactorF / 2.0f, +size.Z / 2.0f);
                IMM=new Vertex("Inner-X+Y-Z", -size.X * hollowFactorF / 2.0f, +size.Y  * hollowFactorF / 2.0f, -size.Z / 2.0f);

                MeshPY.Add(IPP);
                MeshPY.Add(IPM);
                MeshPY.Add(IMP);
                MeshPY.Add(IMM);

                MeshPY.Add(new Triangle(IPM, IPP, IMP));
                MeshPY.Add(new Triangle(IMP, IMM, IPM));

                foreach (Triangle t in MeshPY.triangles)
                {
                    PhysicsVector n = t.getNormal();
                }



            }


            Mesh result = new Mesh();
            result.Append(MeshMY);
            result.Append(MeshPY);

            return result;
        }
            
        static Mesh CreateBoxMeshZ(PrimitiveBaseShape primShape, PhysicsVector size)
        // Builds the z (+ and -) surfaces of a box shaped prim
        {
            UInt16 hollowFactor = primShape.ProfileHollow;

            // Base, i.e. outer shape
            // (M)inus Z
            Mesh MZ = new Mesh();

            MZ.Add(new Vertex("-X-Y-Z", -size.X / 2.0f, -size.Y / 2.0f, -size.Z / 2.0f));
            MZ.Add(new Vertex("+X-Y-Z", +size.X / 2.0f, -size.Y / 2.0f, -size.Z / 2.0f));
            MZ.Add(new Vertex("-X+Y-Z", -size.X / 2.0f, +size.Y / 2.0f, -size.Z / 2.0f));
            MZ.Add(new Vertex("+X+Y-Z", +size.X / 2.0f, +size.Y / 2.0f, -size.Z / 2.0f));


            MZ.Add(new Triangle(MZ.vertices[1], MZ.vertices[0], MZ.vertices[2]));
            MZ.Add(new Triangle(MZ.vertices[1], MZ.vertices[2], MZ.vertices[3]));

            // (P)lus Z
            Mesh PZ = new Mesh();

            PZ.Add(new Vertex("-X-Y+Z", -size.X / 2.0f, -size.Y / 2.0f, 0.0f));
            PZ.Add(new Vertex("+X-Y+Z", +size.X / 2.0f, -size.Y / 2.0f, 0.0f));
            PZ.Add(new Vertex("-X+Y+Z", -size.X / 2.0f, +size.Y / 2.0f, 0.0f));
            PZ.Add(new Vertex("+X+Y+Z", +size.X / 2.0f, +size.Y / 2.0f, 0.0f));

            // Surface 5, +Z
            PZ.Add(new Triangle(PZ.vertices[0], PZ.vertices[1], PZ.vertices[2]));
            PZ.Add(new Triangle(PZ.vertices[2], PZ.vertices[1], PZ.vertices[3]));

            if (hollowFactor > 0)
            {
                float hollowFactorF = (float)hollowFactor / (float)50000;

                MZ.Add(new Vertex("-X-Y-Z", -size.X * hollowFactorF / 2.0f, -size.Y * hollowFactorF / 2.0f, 0.0f));
                MZ.Add(new Vertex("-X+Y-Z", +size.X * hollowFactorF / 2.0f, -size.Y * hollowFactorF / 2.0f, 0.0f));
                MZ.Add(new Vertex("-X-Y+Z", -size.X * hollowFactorF / 2.0f, +size.Y * hollowFactorF / 2.0f, 0.0f));
                MZ.Add(new Vertex("-X+Y+Z", +size.X * hollowFactorF / 2.0f, +size.Y * hollowFactorF / 2.0f, 0.0f));

                List<int> innerBorders = new List<int>();
                innerBorders.Add(4);
                innerBorders.Add(5);
                innerBorders.Add(6);
                innerBorders.Add(7);

                InsertVertices(MZ.vertices, 4, MZ.triangles, innerBorders);

                PZ.Add(new Vertex("-X-Y-Z", -size.X * hollowFactorF / 2.0f, -size.Y * hollowFactorF / 2.0f, 0.0f));
                PZ.Add(new Vertex("-X+Y-Z", +size.X * hollowFactorF / 2.0f, -size.Y * hollowFactorF / 2.0f, 0.0f));
                PZ.Add(new Vertex("-X-Y+Z", -size.X * hollowFactorF / 2.0f, +size.Y * hollowFactorF / 2.0f, 0.0f));
                PZ.Add(new Vertex("-X+Y+Z", +size.X * hollowFactorF / 2.0f, +size.Y * hollowFactorF / 2.0f, 0.0f));

                innerBorders = new List<int>();
                innerBorders.Add(4);
                innerBorders.Add(5);
                innerBorders.Add(6);
                innerBorders.Add(7);

                InsertVertices(PZ.vertices, 4, PZ.triangles, innerBorders);

            }

            foreach (Vertex v in PZ.vertices)
            {
                v.point.Z = size.Z / 2.0f;
            }
            foreach (Vertex v in MZ.vertices)
            {
                v.point.Z = -size.Z / 2.0f;
            }

            foreach (Triangle t in MZ.triangles)
            {
                PhysicsVector n = t.getNormal();
                if (n.Z > 0.0)
                    t.invertNormal();
            }

            foreach (Triangle t in PZ.triangles)
            {
                PhysicsVector n = t.getNormal();
                if (n.Z < 0.0)
                    t.invertNormal();
            }

            Mesh result = new Mesh();
            result.Append(MZ);
            result.Append(PZ);

            return result;
        }

        static Mesh CreateBoxMesh(PrimitiveBaseShape primShape, PhysicsVector size)
        {
            Mesh result = new Mesh();



            Mesh MeshX = Meshmerizer.CreateBoxMeshX(primShape, size);
            Mesh MeshY = Meshmerizer.CreateBoxMeshY(primShape, size);
            Mesh MeshZ = Meshmerizer.CreateBoxMeshZ(primShape, size);

            result.Append(MeshX);
            result.Append(MeshY);
            result.Append(MeshZ);

            return result;
        }


        public static void CalcNormals(Mesh mesh) 
        {
            int iTriangles = mesh.triangles.Count;

            mesh.normals = new float[iTriangles*3];

            int i=0;
            foreach (Triangle t in mesh.triangles)
            {

                float ux, uy, uz;
                float vx, vy, vz;
                float wx, wy, wz;

                    ux = t.v1.point.X;
                    uy = t.v1.point.Y;
                    uz = t.v1.point.Z;

                    vx = t.v2.point.X;
                    vy = t.v2.point.Y;
                    vz = t.v2.point.Z;

                    wx = t.v3.point.X;
                    wy = t.v3.point.Y;
                    wz = t.v3.point.Z;

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

                    // Normalized "normal"
                    nx /= l;
                    ny /= l;
                    nz /= l;

                mesh.normals[i] = nx;
                mesh.normals[i + 1] = ny;
                mesh.normals[i + 2] = nz;

                i+=3;
            }
        }

        public static Mesh CreateMesh(PrimitiveBaseShape primShape, PhysicsVector size)
        {
            Mesh mesh = null;

            switch (primShape.ProfileShape)
            {
                case ProfileShape.Square:
                    mesh=CreateBoxMesh(primShape, size);
                    CalcNormals(mesh);
                    break;
                default:
                    mesh=null;
                    break;
            }

            return mesh;

        }
    }
}

