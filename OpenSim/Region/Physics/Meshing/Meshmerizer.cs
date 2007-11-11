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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System;
using System.IO;
using System.Globalization;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.Meshing
{

    public class MeshmerizerPlugin : IMeshingPlugin
    {
        public MeshmerizerPlugin()
        {
        }

        public string GetName()
        {
            return "Meshmerizer";
        }

        public IMesher GetMesher()
        {
            return new Meshmerizer();
        }
    }

    public class Meshmerizer : IMesher
    {
        // Setting baseDir to a path will enable the dumping of raw files
        // raw files can be imported by blender so a visual inspection of the results can be done
        //        const string baseDir = "rawFiles";
                const string baseDir = null;

        static void IntersectionParameterPD(PhysicsVector p1, PhysicsVector r1, PhysicsVector p2, PhysicsVector r2, ref float lambda, ref float mu)
        { 
            // p1, p2, points on the straight
            // r1, r2, directional vectors of the straight. Not necessarily of length 1!
            // note, that l, m can be scaled such, that the range 0..1 is mapped to the area between two points,
            // thus allowing to decide whether an intersection is between two points

            float r1x = r1.X;
            float r1y = r1.Y;
            float r2x = r2.X;
            float r2y = r2.Y;

            float denom = r1y*r2x - r1x*r2y;

            if (denom == 0.0)
            {
                lambda = Single.NaN;
                mu = Single.NaN;
                return;
            }

            float p1x = p1.X;
            float p1y = p1.Y;
            float p2x = p2.X;
            float p2y = p2.Y;
            lambda = (-p2x * r2y + p1x * r2y + (p2y - p1y) * r2x) / denom;
            mu     = (-p2x * r1y + p1x * r1y + (p2y - p1y) * r1x) / denom;
        
        }

        private static List<Triangle> FindInfluencedTriangles(List<Triangle> triangles, Vertex v)
        {
            List<Triangle> influenced = new List<Triangle>();
            foreach (Triangle t in triangles)
            {
                if (t.isInCircle(v.X, v.Y))
                {
                    influenced.Add(t);
                }
            }
            return influenced;
        }
        
        
        private static void InsertVertices(List<Vertex> vertices, int usedForSeed, List<Triangle> triangles)
        {
            // This is a variant of the delaunay algorithm
            // each time a new vertex is inserted, all triangles that are influenced by it are deleted
            // and replaced by new ones including the new vertex
            // It is not very time efficient but easy to implement.

            int iCurrentVertex;
            int iMaxVertex = vertices.Count;
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
                if (v == null)
                    continue;   // Null is polygon stop marker. Ignore it
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
                // Now sort the simplices. That will make identical ones reside side by side in the list
                simplices.Sort();

                // Look for duplicate simplices here. 
                // Remember, they are directly side by side in the list right now, 
                // So we only check directly neighbours
                int iSimplex;
                List<Simplex> innerSimplices = new List<Simplex>();
                for (iSimplex = 1; iSimplex < simplices.Count; iSimplex++) // Startindex=1, so we can refer backwards
                {
                    if (simplices[iSimplex - 1].CompareTo(simplices[iSimplex]) == 0)
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
                // with each of these simplices. Build the new triangles and add them to the list
                foreach (Simplex s in simplices)
                {
                    Triangle t = new Triangle(s.v1, s.v2, vertices[iCurrentVertex]);
                    if (!t.isDegraded())
                    {
                        triangles.Add(t);
                    }
                }
            }

        }

            
        static Mesh CreateBoxMesh(String primName, PrimitiveBaseShape primShape, PhysicsVector size)
        // Builds the z (+ and -) surfaces of a box shaped prim
        {
            UInt16 hollowFactor = primShape.ProfileHollow;
            UInt16 profileBegin = primShape.ProfileBegin;
            UInt16 profileEnd = primShape.ProfileEnd;

            // Procedure: This is based on the fact that the upper (plus) and lower (minus) Z-surface
            // of a block are basically the same
            // They may be warped differently but the shape is identical
            // So we only create one surface as a model and derive both plus and minus surface of the block from it
            // This is done in a model space where the block spans from -.5 to +.5 in X and Y
            // The mapping to Scene space is done later during the "extrusion" phase

            // Base
            Vertex MM = new Vertex(-0.5f, -0.5f, 0.0f);
            Vertex PM = new Vertex(+0.5f, -0.5f, 0.0f);
            Vertex MP = new Vertex(-0.5f, +0.5f, 0.0f);
            Vertex PP = new Vertex(+0.5f, +0.5f, 0.0f);

            Meshing.SimpleHull outerHull = new SimpleHull();
            outerHull.AddVertex(MM);
            outerHull.AddVertex(PM);
            outerHull.AddVertex(PP);
            outerHull.AddVertex(MP);

            // Deal with cuts now
            if ((profileBegin != 0) || (profileEnd != 0))
            {
                double fProfileBeginAngle = profileBegin / 50000.0 * 360.0; // In degree, for easier debugging and understanding
                fProfileBeginAngle -= (90.0 + 45.0);   // for some reasons, the SL client counts from the corner -X/-Y
                double fProfileEndAngle = 360.0 - profileEnd / 50000.0 * 360.0; // Pathend comes as complement to 1.0
                fProfileEndAngle -= (90.0 + 45.0);
                if (fProfileBeginAngle < fProfileEndAngle)
                    fProfileEndAngle -= 360.0;

                // Note, that we don't want to cut out a triangle, even if this is a 
                // good approximation for small cuts. Indeed we want to cut out an arc
                // and we approximate this arc by a polygon chain
                // Also note, that these vectors are of length 1.0 and thus their endpoints lay outside the model space
                // So it can easily be subtracted from the outer hull
                int iSteps = (int)(((fProfileBeginAngle - fProfileEndAngle) / 45.0) + .5); // how many steps do we need with approximately 45 degree
                double dStepWidth=(fProfileBeginAngle-fProfileEndAngle)/iSteps;

                Vertex origin = new Vertex(0.0f, 0.0f, 0.0f);

                // Note the sequence of vertices here. It's important to have the other rotational sense than in outerHull
                SimpleHull cutHull = new SimpleHull();
                cutHull.AddVertex(origin);
                for (int i=0; i<iSteps; i++) {
                    double angle=fProfileBeginAngle-i*dStepWidth; // we count against the angle orientation!!!!
                    Vertex v = Vertex.FromAngle(angle * Math.PI / 180.0);
                    cutHull.AddVertex(v);
                }
                Vertex legEnd = Vertex.FromAngle(fProfileEndAngle * Math.PI / 180.0); // Calculated separately to avoid errors
                cutHull.AddVertex(legEnd);

                MainLog.Instance.Debug("Starting cutting of the hollow shape from the prim {1}", 0, primName);
                SimpleHull cuttedHull = SimpleHull.SubtractHull(outerHull, cutHull);

                outerHull = cuttedHull;
            }

            // Deal with the hole here
            if (hollowFactor > 0)
            {
                float hollowFactorF = (float) hollowFactor/(float) 50000;
                Vertex IMM = new Vertex(-0.5f * hollowFactorF, -0.5f * hollowFactorF, 0.0f);
                Vertex IPM = new Vertex(+0.5f * hollowFactorF, -0.5f * hollowFactorF, 0.0f);
                Vertex IMP = new Vertex(-0.5f * hollowFactorF, +0.5f * hollowFactorF, 0.0f);
                Vertex IPP = new Vertex(+0.5f * hollowFactorF, +0.5f * hollowFactorF, 0.0f);

                SimpleHull holeHull = new SimpleHull();

                holeHull.AddVertex(IMM);
                holeHull.AddVertex(IMP);
                holeHull.AddVertex(IPP);
                holeHull.AddVertex(IPM);

                SimpleHull hollowedHull = SimpleHull.SubtractHull(outerHull, holeHull);

                outerHull = hollowedHull;

            }

            Mesh m = new Mesh();

            Vertex Seed1 = new Vertex(0.0f, -10.0f, 0.0f);
            Vertex Seed2 = new Vertex(-10.0f, 10.0f, 0.0f);
            Vertex Seed3 = new Vertex(10.0f, 10.0f, 0.0f);

            m.Add(Seed1);
            m.Add(Seed2);
            m.Add(Seed3);

            m.Add(new Triangle(Seed1, Seed2, Seed3));
            m.Add(outerHull.getVertices());

            InsertVertices(m.vertices, 3, m.triangles);
            m.DumpRaw(baseDir, primName, "Proto first Mesh");

            m.Remove(Seed1);
            m.Remove(Seed2);
            m.Remove(Seed3);
            m.DumpRaw(baseDir, primName, "Proto seeds removed");
            
            m.RemoveTrianglesOutside(outerHull);
            m.DumpRaw(baseDir, primName, "Proto outsides removed");

            foreach (Triangle t in m.triangles)
            {
                PhysicsVector n = t.getNormal();
                if (n.Z < 0.0)
                    t.invertNormal();
            }

            Extruder extr = new Extruder();

            extr.size = size;

            Mesh result = extr.Extrude(m);
            result.DumpRaw(baseDir, primName, "Z extruded");
            return result;
        }

        public static void CalcNormals(Mesh mesh)
        {
            int iTriangles = mesh.triangles.Count;

            mesh.normals = new float[iTriangles*3];

            int i = 0;
            foreach (Triangle t in mesh.triangles)
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
                nx = e1y*e2z - e1z*e2y;
                ny = e1z*e2x - e1x*e2z;
                nz = e1x*e2y - e1y*e2x;

                // Length
                float l = (float) Math.Sqrt(nx*nx + ny*ny + nz*nz);

                // Normalized "normal"
                nx /= l;
                ny /= l;
                nz /= l;

                mesh.normals[i] = nx;
                mesh.normals[i + 1] = ny;
                mesh.normals[i + 2] = nz;

                i += 3;
            }
        }

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, PhysicsVector size)
        {
            Mesh mesh = null;

            switch (primShape.ProfileShape)
            {
                case ProfileShape.Square:
                    mesh=CreateBoxMesh(primName, primShape, size);
                    CalcNormals(mesh);
                    break;
                default:
                    mesh = CreateBoxMesh(primName, primShape, size);
                    CalcNormals(mesh);
                    //Set default mesh to cube otherwise it'll return 
                    // null and crash on the 'setMesh' method in the physics plugins.
                    //mesh = null;
                    break;
            }

            return mesh;
        }
    }

}
