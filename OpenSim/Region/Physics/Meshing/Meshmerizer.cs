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
//#define SPAM

using System;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using libsecondlife;

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
        //private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Setting baseDir to a path will enable the dumping of raw files
        // raw files can be imported by blender so a visual inspection of the results can be done
#if SPAM
        const string baseDir = "rawFiles";
#else
        private const string baseDir = null; //"rawFiles";
#endif
        private const float DEG_TO_RAD = 0.01745329238f;

// TODO: unused
//         private static void IntersectionParameterPD(PhysicsVector p1, PhysicsVector r1, PhysicsVector p2,
//                                                     PhysicsVector r2, ref float lambda, ref float mu)
//         {
//             // p1, p2, points on the straight
//             // r1, r2, directional vectors of the straight. Not necessarily of length 1!
//             // note, that l, m can be scaled such, that the range 0..1 is mapped to the area between two points,
//             // thus allowing to decide whether an intersection is between two points

//             float r1x = r1.X;
//             float r1y = r1.Y;
//             float r2x = r2.X;
//             float r2y = r2.Y;

//             float denom = r1y*r2x - r1x*r2y;

//             if (denom == 0.0)
//             {
//                 lambda = Single.NaN;
//                 mu = Single.NaN;
//                 return;
//             }

//             float p1x = p1.X;
//             float p1y = p1.Y;
//             float p2x = p2.X;
//             float p2y = p2.Y;
//             lambda = (-p2x*r2y + p1x*r2y + (p2y - p1y)*r2x)/denom;
//             mu = (-p2x*r1y + p1x*r1y + (p2y - p1y)*r1x)/denom;
//         }

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
                Vertex v = vertices[iCurrentVertex];
                if (v == null)
                    continue; // Null is polygon stop marker. Ignore it
                List<Triangle> influencedTriangles = FindInfluencedTriangles(triangles, v);

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
                // The new vertex (yes, we still deal with verices here :-)) forms a triangle
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

        private static SimpleHull BuildHoleHull(PrimitiveBaseShape pbs, ProfileShape pshape, HollowShape hshape, UInt16 hollowFactor)
        {
            // Tackle HollowShape.Same
            float fhollowFactor = (float)hollowFactor;

            switch (pshape)
            {
                case ProfileShape.Square:
                    if (hshape == HollowShape.Same)
                        hshape= HollowShape.Square;
                    break;
                case ProfileShape.EquilateralTriangle:
                    fhollowFactor = ((float)hollowFactor / 1.9f);
                    if (hshape == HollowShape.Same)
                    {
                        hshape = HollowShape.Triangle;
                    }

                    break;

                case ProfileShape.HalfCircle:
                case ProfileShape.Circle:
                    if (pbs.PathCurve == (byte)Extrusion.Straight)
                    {
                        if (hshape == HollowShape.Same)
                        {
                            hshape = HollowShape.Circle;
                        }
                    }
                    break;


                default:
                    if (hshape == HollowShape.Same)
                        hshape= HollowShape.Square;
                    break;
            }


            SimpleHull holeHull = null;

            if (hshape == HollowShape.Square)
            {
                float hollowFactorF = (float)fhollowFactor / (float)50000;
                Vertex IMM;
                Vertex IPM;
                Vertex IPP;
                Vertex IMP;

                if (pshape == ProfileShape.Circle)
                { // square cutout in cylinder is 45 degress rotated
                    IMM = new Vertex(0.0f, -0.707f * hollowFactorF, 0.0f);
                    IPM = new Vertex(0.707f * hollowFactorF, 0.0f, 0.0f);
                    IPP = new Vertex(0.0f, 0.707f * hollowFactorF, 0.0f);
                    IMP = new Vertex(-0.707f * hollowFactorF, 0.0f, 0.0f);
                }
                else if (pshape == ProfileShape.EquilateralTriangle)
                {
                    IMM = new Vertex(0.0f, -0.667f * hollowFactorF, 0.0f);
                    IPM = new Vertex(0.667f * hollowFactorF, 0.0f, 0.0f);
                    IPP = new Vertex(0.0f, 0.667f * hollowFactorF, 0.0f);
                    IMP = new Vertex(-0.667f * hollowFactorF, 0.0f, 0.0f);
                }
                else
                {
                    IMM = new Vertex(-0.5f * hollowFactorF, -0.5f * hollowFactorF, 0.0f);
                    IPM = new Vertex(+0.5f * hollowFactorF, -0.5f * hollowFactorF, 0.0f);
                    IPP = new Vertex(+0.5f * hollowFactorF, +0.5f * hollowFactorF, 0.0f);
                    IMP = new Vertex(-0.5f * hollowFactorF, +0.5f * hollowFactorF, 0.0f);
                }

                holeHull = new SimpleHull();

                holeHull.AddVertex(IMM);
                holeHull.AddVertex(IMP);
                holeHull.AddVertex(IPP);
                holeHull.AddVertex(IPM);
            }
            //if (hshape == HollowShape.Circle && pbs.PathCurve == (byte)Extrusion.Straight)
            if (hshape == HollowShape.Circle)
            {
                float hollowFactorF = (float)fhollowFactor / (float)50000;

                //Vertex IQ1Q15 = new Vertex(-0.35f * hollowFactorF, -0.35f * hollowFactorF, 0.0f);
                //Vertex IQ1Q16 = new Vertex(-0.30f * hollowFactorF, -0.40f * hollowFactorF, 0.0f);
                //Vertex IQ1Q17 = new Vertex(-0.24f * hollowFactorF, -0.43f * hollowFactorF, 0.0f);
                //Vertex IQ1Q18 = new Vertex(-0.18f * hollowFactorF, -0.46f * hollowFactorF, 0.0f);
                //Vertex IQ1Q19 = new Vertex(-0.11f * hollowFactorF, -0.48f * hollowFactorF, 0.0f);

                //Vertex IQ2Q10 = new Vertex(+0.0f * hollowFactorF, -0.50f * hollowFactorF, 0.0f);
                //Vertex IQ2Q11 = new Vertex(+0.11f * hollowFactorF, -0.48f * hollowFactorF, 0.0f);
                //Vertex IQ2Q12 = new Vertex(+0.18f * hollowFactorF, -0.46f * hollowFactorF, 0.0f);
                //Vertex IQ2Q13 = new Vertex(+0.24f * hollowFactorF, -0.43f * hollowFactorF, 0.0f);
                //Vertex IQ2Q14 = new Vertex(+0.30f * hollowFactorF, -0.40f * hollowFactorF, 0.0f);
                //Vertex IQ2Q15 = new Vertex(+0.35f * hollowFactorF, -0.35f * hollowFactorF, 0.0f);
                //Vertex IQ2Q16 = new Vertex(+0.40f * hollowFactorF, -0.30f * hollowFactorF, 0.0f);
                //Vertex IQ2Q17 = new Vertex(+0.43f * hollowFactorF, -0.24f * hollowFactorF, 0.0f);
                //Vertex IQ2Q18 = new Vertex(+0.46f * hollowFactorF, -0.18f * hollowFactorF, 0.0f);
                //Vertex IQ2Q19 = new Vertex(+0.48f * hollowFactorF, -0.11f * hollowFactorF, 0.0f);

                //Vertex IQ2Q20 = new Vertex(+0.50f * hollowFactorF, +0.0f * hollowFactorF, 0.0f);
                //Vertex IQ2Q21 = new Vertex(+0.48f * hollowFactorF, +0.11f * hollowFactorF, 0.0f);
                //Vertex IQ2Q22 = new Vertex(+0.46f * hollowFactorF, +0.18f * hollowFactorF, 0.0f);
                //Vertex IQ2Q23 = new Vertex(+0.43f * hollowFactorF, +0.24f * hollowFactorF, 0.0f);
                //Vertex IQ2Q24 = new Vertex(+0.40f * hollowFactorF, +0.30f * hollowFactorF, 0.0f);
                //Vertex IQ2Q25 = new Vertex(+0.35f * hollowFactorF, +0.35f * hollowFactorF, 0.0f);
                //Vertex IQ2Q26 = new Vertex(+0.30f * hollowFactorF, +0.40f * hollowFactorF, 0.0f);
                //Vertex IQ2Q27 = new Vertex(+0.24f * hollowFactorF, +0.43f * hollowFactorF, 0.0f);
                //Vertex IQ2Q28 = new Vertex(+0.18f * hollowFactorF, +0.46f * hollowFactorF, 0.0f);
                //Vertex IQ2Q29 = new Vertex(+0.11f * hollowFactorF, +0.48f * hollowFactorF, 0.0f);

                //Vertex IQ1Q20 = new Vertex(+0.0f * hollowFactorF, +0.50f * hollowFactorF, 0.0f);
                //Vertex IQ1Q21 = new Vertex(-0.11f * hollowFactorF, +0.48f * hollowFactorF, 0.0f);
                //Vertex IQ1Q22 = new Vertex(-0.18f * hollowFactorF, +0.46f * hollowFactorF, 0.0f);
                //Vertex IQ1Q23 = new Vertex(-0.24f * hollowFactorF, +0.43f * hollowFactorF, 0.0f);
                //Vertex IQ1Q24 = new Vertex(-0.30f * hollowFactorF, +0.40f * hollowFactorF, 0.0f);
                //Vertex IQ1Q25 = new Vertex(-0.35f * hollowFactorF, +0.35f * hollowFactorF, 0.0f);
                //Vertex IQ1Q26 = new Vertex(-0.40f * hollowFactorF, +0.30f * hollowFactorF, 0.0f);
                //Vertex IQ1Q27 = new Vertex(-0.43f * hollowFactorF, +0.24f * hollowFactorF, 0.0f);
                //Vertex IQ1Q28 = new Vertex(-0.46f * hollowFactorF, +0.18f * hollowFactorF, 0.0f);
                //Vertex IQ1Q29 = new Vertex(-0.48f * hollowFactorF, +0.11f * hollowFactorF, 0.0f);

                //Vertex IQ1Q10 = new Vertex(-0.50f * hollowFactorF, +0.0f * hollowFactorF, 0.0f);
                //Vertex IQ1Q11 = new Vertex(-0.48f * hollowFactorF, -0.11f * hollowFactorF, 0.0f);
                //Vertex IQ1Q12 = new Vertex(-0.46f * hollowFactorF, -0.18f * hollowFactorF, 0.0f);
                //Vertex IQ1Q13 = new Vertex(-0.43f * hollowFactorF, -0.24f * hollowFactorF, 0.0f);
                //Vertex IQ1Q14 = new Vertex(-0.40f * hollowFactorF, -0.30f * hollowFactorF, 0.0f);

                //Counter clockwise around the quadrants
                holeHull = new SimpleHull();
                //holeHull.AddVertex(IQ1Q15);
                //holeHull.AddVertex(IQ1Q14);
                //holeHull.AddVertex(IQ1Q13);
                //holeHull.AddVertex(IQ1Q12);
                //holeHull.AddVertex(IQ1Q11);
                //holeHull.AddVertex(IQ1Q10);

                //holeHull.AddVertex(IQ1Q29);
                //holeHull.AddVertex(IQ1Q28);
                //holeHull.AddVertex(IQ1Q27);
                //holeHull.AddVertex(IQ1Q26);
                //holeHull.AddVertex(IQ1Q25);
                //holeHull.AddVertex(IQ1Q24);
                //holeHull.AddVertex(IQ1Q23);
                //holeHull.AddVertex(IQ1Q22);
                //holeHull.AddVertex(IQ1Q21);
                //holeHull.AddVertex(IQ1Q20);

                //holeHull.AddVertex(IQ2Q29);
                //holeHull.AddVertex(IQ2Q28);
                //holeHull.AddVertex(IQ2Q27);
                //holeHull.AddVertex(IQ2Q26);
                //holeHull.AddVertex(IQ2Q25);
                //holeHull.AddVertex(IQ2Q24);
                //holeHull.AddVertex(IQ2Q23);
                //holeHull.AddVertex(IQ2Q22);
                //holeHull.AddVertex(IQ2Q21);
                //holeHull.AddVertex(IQ2Q20);

                //holeHull.AddVertex(IQ2Q19);
                //holeHull.AddVertex(IQ2Q18);
                //holeHull.AddVertex(IQ2Q17);
                //holeHull.AddVertex(IQ2Q16);
                //holeHull.AddVertex(IQ2Q15);
                //holeHull.AddVertex(IQ2Q14);
                //holeHull.AddVertex(IQ2Q13);
                //holeHull.AddVertex(IQ2Q12);
                //holeHull.AddVertex(IQ2Q11);
                //holeHull.AddVertex(IQ2Q10);

                //holeHull.AddVertex(IQ1Q19);
                //holeHull.AddVertex(IQ1Q18);
                //holeHull.AddVertex(IQ1Q17);
                //holeHull.AddVertex(IQ1Q16);

                holeHull.AddVertex(new Vertex(0.353553f * hollowFactorF, 0.353553f * hollowFactorF, 0.0f)); // 45 degrees
                holeHull.AddVertex(new Vertex(0.433013f * hollowFactorF, 0.250000f * hollowFactorF, 0.0f)); // 30 degrees
                holeHull.AddVertex(new Vertex(0.482963f * hollowFactorF, 0.129410f * hollowFactorF, 0.0f)); // 15 degrees
                holeHull.AddVertex(new Vertex(0.500000f * hollowFactorF, 0.000000f * hollowFactorF, 0.0f)); // 0 degrees
                holeHull.AddVertex(new Vertex(0.482963f * hollowFactorF, -0.129410f * hollowFactorF, 0.0f)); // 345 degrees
                holeHull.AddVertex(new Vertex(0.433013f * hollowFactorF, -0.250000f * hollowFactorF, 0.0f)); // 330 degrees
                holeHull.AddVertex(new Vertex(0.353553f * hollowFactorF, -0.353553f * hollowFactorF, 0.0f)); // 315 degrees
                holeHull.AddVertex(new Vertex(0.250000f * hollowFactorF, -0.433013f * hollowFactorF, 0.0f)); // 300 degrees
                holeHull.AddVertex(new Vertex(0.129410f * hollowFactorF, -0.482963f * hollowFactorF, 0.0f)); // 285 degrees
                holeHull.AddVertex(new Vertex(0.000000f * hollowFactorF, -0.500000f * hollowFactorF, 0.0f)); // 270 degrees
                holeHull.AddVertex(new Vertex(-0.129410f * hollowFactorF, -0.482963f * hollowFactorF, 0.0f)); // 255 degrees
                holeHull.AddVertex(new Vertex(-0.250000f * hollowFactorF, -0.433013f * hollowFactorF, 0.0f)); // 240 degrees
                holeHull.AddVertex(new Vertex(-0.353553f * hollowFactorF, -0.353553f * hollowFactorF, 0.0f)); // 225 degrees
                holeHull.AddVertex(new Vertex(-0.433013f * hollowFactorF, -0.250000f * hollowFactorF, 0.0f)); // 210 degrees
                holeHull.AddVertex(new Vertex(-0.482963f * hollowFactorF, -0.129410f * hollowFactorF, 0.0f)); // 195 degrees
                holeHull.AddVertex(new Vertex(-0.500000f * hollowFactorF, 0.000000f * hollowFactorF, 0.0f)); // 180 degrees
                holeHull.AddVertex(new Vertex(-0.482963f * hollowFactorF, 0.129410f * hollowFactorF, 0.0f)); // 165 degrees
                holeHull.AddVertex(new Vertex(-0.433013f * hollowFactorF, 0.250000f * hollowFactorF, 0.0f)); // 150 degrees
                holeHull.AddVertex(new Vertex(-0.353553f * hollowFactorF, 0.353553f * hollowFactorF, 0.0f)); // 135 degrees
                holeHull.AddVertex(new Vertex(-0.250000f * hollowFactorF, 0.433013f * hollowFactorF, 0.0f)); // 120 degrees
                holeHull.AddVertex(new Vertex(-0.129410f * hollowFactorF, 0.482963f * hollowFactorF, 0.0f)); // 105 degrees
                holeHull.AddVertex(new Vertex(0.000000f * hollowFactorF, 0.500000f * hollowFactorF, 0.0f)); // 90 degrees
                holeHull.AddVertex(new Vertex(0.129410f * hollowFactorF, 0.482963f * hollowFactorF, 0.0f)); // 75 degrees
                holeHull.AddVertex(new Vertex(0.250000f * hollowFactorF, 0.433013f * hollowFactorF, 0.0f)); // 60 degrees
                holeHull.AddVertex(new Vertex(0.353553f * hollowFactorF, 0.353553f * hollowFactorF, 0.0f)); // 45 degrees

            }
            if (hshape == HollowShape.Triangle)
            {
                float hollowFactorF = (float)fhollowFactor / (float)50000;
                Vertex IMM;
                Vertex IPM;
                Vertex IPP;

                if (pshape == ProfileShape.Square)
                {
                    // corner points are at 345, 105, and 225 degrees for the triangle within a box

                    //IMM = new Vertex(((float)Math.Cos(345.0 * DEG_TO_RAD) * 0.5f) * hollowFactorF, ((float)Math.Sin(345.0 * DEG_TO_RAD) * 0.5f) * hollowFactorF, 0.0f);
                    //IPM = new Vertex(((float)Math.Cos(105.0 * DEG_TO_RAD) * 0.5f) * hollowFactorF, ((float)Math.Sin(105.0 * DEG_TO_RAD) * 0.5f) * hollowFactorF, 0.0f);
                    //IPP = new Vertex(((float)Math.Cos(225.0 * DEG_TO_RAD) * 0.5f) * hollowFactorF, ((float)Math.Sin(225.0 * DEG_TO_RAD) * 0.5f) * hollowFactorF, 0.0f);

                    // hard coded here for speed, the equations are in the commented out lines above
                    IMM = new Vertex(0.48296f * hollowFactorF, -0.12941f * hollowFactorF, 0.0f);
                    IPM = new Vertex(-0.12941f * hollowFactorF, 0.48296f * hollowFactorF, 0.0f);
                    IPP = new Vertex(-0.35355f * hollowFactorF, -0.35355f * hollowFactorF, 0.0f);
                }
                else
                {
                    IMM = new Vertex(-0.25f * hollowFactorF, -0.45f * hollowFactorF, 0.0f);
                    IPM = new Vertex(+0.5f * hollowFactorF, +0f * hollowFactorF, 0.0f);
                    IPP = new Vertex(-0.25f * hollowFactorF, +0.45f * hollowFactorF, 0.0f);
                }

                holeHull = new SimpleHull();

                holeHull.AddVertex(IMM);
                holeHull.AddVertex(IPP);
                holeHull.AddVertex(IPM);

            }

            return holeHull;


        }

        private static Mesh CreateBoxMesh(String primName, PrimitiveBaseShape primShape, PhysicsVector size)
            // Builds the z (+ and -) surfaces of a box shaped prim
        {
            UInt16 hollowFactor = primShape.ProfileHollow;
            UInt16 profileBegin = primShape.ProfileBegin;
            UInt16 profileEnd = primShape.ProfileEnd;
            UInt16 taperX = primShape.PathScaleX;
            UInt16 taperY = primShape.PathScaleY;
            UInt16 pathShearX = primShape.PathShearX;
            UInt16 pathShearY = primShape.PathShearY;
            // Int16 twistTop = primShape.PathTwistBegin;
            // Int16 twistBot = primShape.PathTwist;

#if SPAM
            reportPrimParams("[BOX] " + primName, primShape);
#endif

            //m_log.Error("pathShear:" + primShape.PathShearX.ToString() + "," + primShape.PathShearY.ToString());
            //m_log.Error("pathTaper:" + primShape.PathTaperX.ToString() + "," + primShape.PathTaperY.ToString());
            //m_log.Error("ProfileBegin:" + primShape.ProfileBegin.ToString() + "," + primShape.ProfileBegin.ToString());
            //m_log.Error("PathScale:" + primShape.PathScaleX.ToString() + "," + primShape.PathScaleY.ToString());

            // Procedure: This is based on the fact that the upper (plus) and lower (minus) Z-surface
            // of a block are basically the same
            // They may be warped differently but the shape is identical
            // So we only create one surface as a model and derive both plus and minus surface of the block from it
            // This is done in a model space where the block spans from -.5 to +.5 in X and Y
            // The mapping to Scene space is done later during the "extrusion" phase

            // Base
            Vertex MM = new Vertex(-0.5f, -0.5f, 0.0f);
            Vertex PM = new Vertex(+0.5f, -0.5f, 0.0f);
            Vertex PP = new Vertex(+0.5f, +0.5f, 0.0f);
            Vertex MP = new Vertex(-0.5f, +0.5f, 0.0f);

            SimpleHull outerHull = new SimpleHull();
            //outerHull.AddVertex(MM);
            //outerHull.AddVertex(PM);
            //outerHull.AddVertex(PP);
            //outerHull.AddVertex(MP);
            outerHull.AddVertex(PP);
            outerHull.AddVertex(MP);
            outerHull.AddVertex(MM);
            outerHull.AddVertex(PM);

            // Deal with cuts now
            if ((profileBegin != 0) || (profileEnd != 0))
            {
                double fProfileBeginAngle = profileBegin/50000.0*360.0;
                    // In degree, for easier debugging and understanding
                fProfileBeginAngle -= (90.0 + 45.0); // for some reasons, the SL client counts from the corner -X/-Y
                double fProfileEndAngle = 360.0 - profileEnd/50000.0*360.0; // Pathend comes as complement to 1.0
                fProfileEndAngle -= (90.0 + 45.0);

                // avoid some problem angles until the hull subtraction routine is fixed
                if ((fProfileBeginAngle + 45.0f) % 90.0f == 0.0f)
                    fProfileBeginAngle += 5.0f;
                if ((fProfileEndAngle + 45.0f) % 90.0f == 0.0f)
                    fProfileEndAngle -= 5.0f;
                if (fProfileBeginAngle % 90.0f == 0.0f)
                    fProfileBeginAngle += 1.0f;
                if (fProfileEndAngle % 90.0f == 0.0f)
                    fProfileEndAngle -= 1.0f;

                if (fProfileBeginAngle < fProfileEndAngle)
                    fProfileEndAngle -= 360.0;

#if SPAM
                Console.WriteLine("Meshmerizer: fProfileBeginAngle: " + fProfileBeginAngle.ToString() + " fProfileEndAngle: " + fProfileEndAngle.ToString());
#endif

                // Note, that we don't want to cut out a triangle, even if this is a
                // good approximation for small cuts. Indeed we want to cut out an arc
                // and we approximate this arc by a polygon chain
                // Also note, that these vectors are of length 1.0 and thus their endpoints lay outside the model space
                // So it can easily be subtracted from the outer hull
                int iSteps = (int) (((fProfileBeginAngle - fProfileEndAngle)/45.0) + .5);
                    // how many steps do we need with approximately 45 degree
                double dStepWidth = (fProfileBeginAngle - fProfileEndAngle)/iSteps;

                Vertex origin = new Vertex(0.0f, 0.0f, 0.0f);

                // Note the sequence of vertices here. It's important to have the other rotational sense than in outerHull
                SimpleHull cutHull = new SimpleHull();
                cutHull.AddVertex(origin);
                for (int i = 0; i < iSteps; i++)
                {
                    double angle = fProfileBeginAngle - i*dStepWidth; // we count against the angle orientation!!!!
                    Vertex v = Vertex.FromAngle(angle*Math.PI/180.0);
                    cutHull.AddVertex(v);
                }
                Vertex legEnd = Vertex.FromAngle(fProfileEndAngle*Math.PI/180.0);
                    // Calculated separately to avoid errors
                cutHull.AddVertex(legEnd);

                //m_log.DebugFormat("Starting cutting of the hollow shape from the prim {1}", 0, primName);
                SimpleHull cuttedHull = SimpleHull.SubtractHull(outerHull, cutHull);

                outerHull = cuttedHull;
            }

            // Deal with the hole here
            if (hollowFactor > 0)
            {

                SimpleHull holeHull = BuildHoleHull(primShape, primShape.ProfileShape, primShape.HollowShape, hollowFactor);
                if (holeHull != null)
                {
                    SimpleHull hollowedHull = SimpleHull.SubtractHull(outerHull, holeHull);

                    outerHull = hollowedHull;
                }
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

            if (taperX != 100)
            {
                if (taperX > 100)
                {
                    extr.taperTopFactorX = 1.0f - ((float)(taperX - 100) / 100);
                    //System.Console.WriteLine("taperTopFactorX: " + extr.taperTopFactorX.ToString());
                }
                else
                {
                    extr.taperBotFactorX = 1.0f - ((100 - (float)taperX) / 100);
                    //System.Console.WriteLine("taperBotFactorX: " + extr.taperBotFactorX.ToString());
                }

            }

            if (taperY != 100)
            {
                if (taperY > 100)
                {
                    extr.taperTopFactorY = 1.0f - ((float)(taperY - 100) / 100);
                    //System.Console.WriteLine("taperTopFactorY: " + extr.taperTopFactorY.ToString());
                }
                else
                {
                    extr.taperBotFactorY = 1.0f - ((100 - (float)taperY) / 100);
                    //System.Console.WriteLine("taperBotFactorY: " + extr.taperBotFactorY.ToString());
                }
            }

            if (pathShearX != 0)
            {
                //System.Console.WriteLine("pushX: " + pathShearX.ToString());
                if (pathShearX > 50)
                {
                    // Complimentary byte.  Negative values wrap around the byte.  Positive values go up to 50
                    extr.pushX = (((float)(256 - pathShearX) / 100) * -1f);
                    //System.Console.WriteLine("pushX: " + extr.pushX);
                }
                else
                {
                    extr.pushX = (float)pathShearX / 100;
                    //System.Console.WriteLine("pushX: " + extr.pushX);
                }
            }

            if (pathShearY != 0)
            {
                if (pathShearY > 50)
                {
                    // Complimentary byte.  Negative values wrap around the byte.  Positive values go up to 50
                    extr.pushY = (((float)(256 - pathShearY) / 100) * -1f);
                    //System.Console.WriteLine("pushY: " + extr.pushY);
                }
                else
                {
                    extr.pushY = (float)pathShearY / 100;
                    //System.Console.WriteLine("pushY: " + extr.pushY);
                }
            }

            //if (twistTop != 0)
            //{
            //    extr.twistTop = 180 * ((float)twistTop / 100);
            //    if (extr.twistTop > 0)
            //    {
            //        extr.twistTop = 360 - (-1 * extr.twistTop);

            //    }


            //    extr.twistTop = (float)(extr.twistTop * DEG_TO_RAD);
            //}

            //float twistMid = ((twistTop + twistBot) * 0.5f);

            //if (twistMid != 0)
            //{
            //    extr.twistMid = 180 * ((float)twistMid / 100);
            //    if (extr.twistMid > 0)
            //    {
            //        extr.twistMid = 360 - (-1 * extr.twistMid);
            //    }
            //    extr.twistMid = (float)(extr.twistMid * DEG_TO_RAD);
            //}

            //if (twistBot != 0)
            //{
            //    extr.twistBot = 180 * ((float)twistBot / 100);
            //    if (extr.twistBot > 0)
            //    {
            //        extr.twistBot = 360 - (-1 * extr.twistBot);
            //    }
            //    extr.twistBot = (float)(extr.twistBot * DEG_TO_RAD);
            //}

            extr.twistTop = (float)primShape.PathTwist * (float)Math.PI * 0.01f;
            extr.twistBot = (float)primShape.PathTwistBegin * (float)Math.PI * 0.01f;
            extr.pathBegin = primShape.PathBegin;
            extr.pathEnd = primShape.PathEnd;

            //Mesh result = extr.Extrude(m);
            Mesh result = extr.ExtrudeLinearPath(m);
            result.DumpRaw(baseDir, primName, "Z extruded");
#if SPAM
            int vCount = 0;

            foreach (Vertex v in result.vertices)
                if (v != null)
                    vCount++;
            System.Console.WriteLine("Mesh vertex count: " + vCount.ToString());
#endif
            return result;
        }

        private static Mesh CreateCylinderMesh(String primName, PrimitiveBaseShape primShape, PhysicsVector size)
        // Builds the z (+ and -) surfaces of a box shaped prim
        {

            UInt16 hollowFactor = primShape.ProfileHollow;
            UInt16 profileBegin = primShape.ProfileBegin;
            UInt16 profileEnd = primShape.ProfileEnd;
            UInt16 taperX = primShape.PathScaleX;
            UInt16 taperY = primShape.PathScaleY;
            UInt16 pathShearX = primShape.PathShearX;
            UInt16 pathShearY = primShape.PathShearY;
            // Int16 twistBot = primShape.PathTwist;
            // Int16 twistTop = primShape.PathTwistBegin;

#if SPAM
            reportPrimParams("[CYLINDER] " + primName, primShape);
#endif


            // Procedure: This is based on the fact that the upper (plus) and lower (minus) Z-surface
            // of a block are basically the same
            // They may be warped differently but the shape is identical
            // So we only create one surface as a model and derive both plus and minus surface of the block from it
            // This is done in a model space where the block spans from -.5 to +.5 in X and Y
            // The mapping to Scene space is done later during the "extrusion" phase

            // Base
            // Q1Q15 = Quadrant 1, Quadrant1, Vertex 5
            //Vertex Q1Q15 = new Vertex(-0.35f, -0.35f, 0.0f);
            //Vertex Q1Q16 = new Vertex(-0.30f, -0.40f, 0.0f);
            //Vertex Q1Q17 = new Vertex(-0.24f, -0.43f, 0.0f);
            //Vertex Q1Q18 = new Vertex(-0.18f, -0.46f, 0.0f);
            //Vertex Q1Q19 = new Vertex(-0.11f, -0.48f, 0.0f);

            //Vertex Q2Q10 = new Vertex(+0.0f, -0.50f, 0.0f);
            //Vertex Q2Q11 = new Vertex(+0.11f, -0.48f, 0.0f);
            //Vertex Q2Q12 = new Vertex(+0.18f, -0.46f, 0.0f);
            //Vertex Q2Q13 = new Vertex(+0.24f, -0.43f, 0.0f);
            //Vertex Q2Q14 = new Vertex(+0.30f, -0.40f, 0.0f);
            //Vertex Q2Q15 = new Vertex(+0.35f, -0.35f, 0.0f);
            //Vertex Q2Q16 = new Vertex(+0.40f, -0.30f, 0.0f);
            //Vertex Q2Q17 = new Vertex(+0.43f, -0.24f, 0.0f);
            //Vertex Q2Q18 = new Vertex(+0.46f, -0.18f, 0.0f);
            //Vertex Q2Q19 = new Vertex(+0.48f, -0.11f, 0.0f);

            //Vertex Q2Q20 = new Vertex(+0.50f, +0.0f, 0.0f);
            //Vertex Q2Q21 = new Vertex(+0.48f, +0.11f, 0.0f);
            //Vertex Q2Q22 = new Vertex(+0.46f, +0.18f, 0.0f);
            //Vertex Q2Q23 = new Vertex(+0.43f, +0.24f, 0.0f);
            //Vertex Q2Q24 = new Vertex(+0.40f, +0.30f, 0.0f);
            //Vertex Q2Q25 = new Vertex(+0.35f, +0.35f, 0.0f);
            //Vertex Q2Q26 = new Vertex(+0.30f, +0.40f, 0.0f);
            //Vertex Q2Q27 = new Vertex(+0.24f, +0.43f, 0.0f);
            //Vertex Q2Q28 = new Vertex(+0.18f, +0.46f, 0.0f);
            //Vertex Q2Q29 = new Vertex(+0.11f, +0.48f, 0.0f);

            //Vertex Q1Q20 = new Vertex(+0.0f, +0.50f, 0.0f);
            //Vertex Q1Q21 = new Vertex(-0.11f, +0.48f, 0.0f);
            //Vertex Q1Q22 = new Vertex(-0.18f, +0.46f, 0.0f);
            //Vertex Q1Q23 = new Vertex(-0.24f, +0.43f, 0.0f);
            //Vertex Q1Q24 = new Vertex(-0.30f, +0.40f, 0.0f);
            //Vertex Q1Q25 = new Vertex(-0.35f, +0.35f, 0.0f);
            //Vertex Q1Q26 = new Vertex(-0.40f, +0.30f, 0.0f);
            //Vertex Q1Q27 = new Vertex(-0.43f, +0.24f, 0.0f);
            //Vertex Q1Q28 = new Vertex(-0.46f, +0.18f, 0.0f);
            //Vertex Q1Q29 = new Vertex(-0.48f, +0.11f, 0.0f);

            //Vertex Q1Q10 = new Vertex(-0.50f, +0.0f, 0.0f);
            //Vertex Q1Q11 = new Vertex(-0.48f, -0.11f, 0.0f);
            //Vertex Q1Q12 = new Vertex(-0.46f, -0.18f, 0.0f);
            //Vertex Q1Q13 = new Vertex(-0.43f, -0.24f, 0.0f);
            //Vertex Q1Q14 = new Vertex(-0.40f, -0.30f, 0.0f);

            SimpleHull outerHull = new SimpleHull();
            //Clockwise around the quadrants
            //outerHull.AddVertex(Q1Q15);
            //outerHull.AddVertex(Q1Q16);
            //outerHull.AddVertex(Q1Q17);
            //outerHull.AddVertex(Q1Q18);
            //outerHull.AddVertex(Q1Q19);

            //outerHull.AddVertex(Q2Q10);
            //outerHull.AddVertex(Q2Q11);
            //outerHull.AddVertex(Q2Q12);
            //outerHull.AddVertex(Q2Q13);
            //outerHull.AddVertex(Q2Q14);
            //outerHull.AddVertex(Q2Q15);
            //outerHull.AddVertex(Q2Q16);
            //outerHull.AddVertex(Q2Q17);
            //outerHull.AddVertex(Q2Q18);
            //outerHull.AddVertex(Q2Q19);

            //outerHull.AddVertex(Q2Q20);
            //outerHull.AddVertex(Q2Q21);
            //outerHull.AddVertex(Q2Q22);
            //outerHull.AddVertex(Q2Q23);
            //outerHull.AddVertex(Q2Q24);
            //outerHull.AddVertex(Q2Q25);
            //outerHull.AddVertex(Q2Q26);
            //outerHull.AddVertex(Q2Q27);
            //outerHull.AddVertex(Q2Q28);
            //outerHull.AddVertex(Q2Q29);

            //outerHull.AddVertex(Q1Q20);
            //outerHull.AddVertex(Q1Q21);
            //outerHull.AddVertex(Q1Q22);
            //outerHull.AddVertex(Q1Q23);
            //outerHull.AddVertex(Q1Q24);
            //outerHull.AddVertex(Q1Q25);
            //outerHull.AddVertex(Q1Q26);
            //outerHull.AddVertex(Q1Q27);
            //outerHull.AddVertex(Q1Q28);
            //outerHull.AddVertex(Q1Q29);

            //outerHull.AddVertex(Q1Q10);
            //outerHull.AddVertex(Q1Q11);
            //outerHull.AddVertex(Q1Q12);
            //outerHull.AddVertex(Q1Q13);
            //outerHull.AddVertex(Q1Q14);

            // counter-clockwise around the quadrants, start at 45 degrees

            outerHull.AddVertex(new Vertex(0.353553f, 0.353553f, 0.0f)); // 45 degrees
            outerHull.AddVertex(new Vertex(0.250000f, 0.433013f, 0.0f)); // 60 degrees
            outerHull.AddVertex(new Vertex(0.129410f, 0.482963f, 0.0f)); // 75 degrees
            outerHull.AddVertex(new Vertex(0.000000f, 0.500000f, 0.0f)); // 90 degrees
            outerHull.AddVertex(new Vertex(-0.129410f, 0.482963f, 0.0f)); // 105 degrees
            outerHull.AddVertex(new Vertex(-0.250000f, 0.433013f, 0.0f)); // 120 degrees
            outerHull.AddVertex(new Vertex(-0.353553f, 0.353553f, 0.0f)); // 135 degrees
            outerHull.AddVertex(new Vertex(-0.433013f, 0.250000f, 0.0f)); // 150 degrees
            outerHull.AddVertex(new Vertex(-0.482963f, 0.129410f, 0.0f)); // 165 degrees
            outerHull.AddVertex(new Vertex(-0.500000f, 0.000000f, 0.0f)); // 180 degrees
            outerHull.AddVertex(new Vertex(-0.482963f, -0.129410f, 0.0f)); // 195 degrees
            outerHull.AddVertex(new Vertex(-0.433013f, -0.250000f, 0.0f)); // 210 degrees
            outerHull.AddVertex(new Vertex(-0.353553f, -0.353553f, 0.0f)); // 225 degrees
            outerHull.AddVertex(new Vertex(-0.250000f, -0.433013f, 0.0f)); // 240 degrees
            outerHull.AddVertex(new Vertex(-0.129410f, -0.482963f, 0.0f)); // 255 degrees
            outerHull.AddVertex(new Vertex(0.000000f, -0.500000f, 0.0f)); // 270 degrees
            outerHull.AddVertex(new Vertex(0.129410f, -0.482963f, 0.0f)); // 285 degrees
            outerHull.AddVertex(new Vertex(0.250000f, -0.433013f, 0.0f)); // 300 degrees
            outerHull.AddVertex(new Vertex(0.353553f, -0.353553f, 0.0f)); // 315 degrees
            outerHull.AddVertex(new Vertex(0.433013f, -0.250000f, 0.0f)); // 330 degrees
            outerHull.AddVertex(new Vertex(0.482963f, -0.129410f, 0.0f)); // 345 degrees
            outerHull.AddVertex(new Vertex(0.500000f, 0.000000f, 0.0f)); // 0 degrees
            outerHull.AddVertex(new Vertex(0.482963f, 0.129410f, 0.0f)); // 15 degrees
            outerHull.AddVertex(new Vertex(0.433013f, 0.250000f, 0.0f)); // 30 degrees



            // Deal with cuts now
            if ((profileBegin != 0) || (profileEnd != 0))
            {
                double fProfileBeginAngle = profileBegin / 50000.0 * 360.0;
                // In degree, for easier debugging and understanding
                //fProfileBeginAngle -= (90.0 + 45.0); // for some reasons, the SL client counts from the corner -X/-Y
                double fProfileEndAngle = 360.0 - profileEnd / 50000.0 * 360.0; // Pathend comes as complement to 1.0
                //fProfileEndAngle -= (90.0 + 45.0);
#if SPAM
                Console.WriteLine("Extruder: Cylinder fProfileBeginAngle: " + fProfileBeginAngle.ToString() + " fProfileEndAngle: " + fProfileEndAngle.ToString());
#endif
                if (fProfileBeginAngle > 270.0f && fProfileBeginAngle < 271.8f) // a problem angle for the hull subtract routine :(
                    fProfileBeginAngle = 271.8f; // workaround - use the smaller slice

                if (fProfileBeginAngle < fProfileEndAngle)
                    fProfileEndAngle -= 360.0;
#if SPAM
                Console.WriteLine("Extruder: Cylinder fProfileBeginAngle: " + fProfileBeginAngle.ToString() + " fProfileEndAngle: " + fProfileEndAngle.ToString());
#endif

                // Note, that we don't want to cut out a triangle, even if this is a
                // good approximation for small cuts. Indeed we want to cut out an arc
                // and we approximate this arc by a polygon chain
                // Also note, that these vectors are of length 1.0 and thus their endpoints lay outside the model space
                // So it can easily be subtracted from the outer hull
                int iSteps = (int)(((fProfileBeginAngle - fProfileEndAngle) / 45.0) + .5);
                // how many steps do we need with approximately 45 degree
                double dStepWidth = (fProfileBeginAngle - fProfileEndAngle) / iSteps;

                Vertex origin = new Vertex(0.0f, 0.0f, 0.0f);

                // Note the sequence of vertices here. It's important to have the other rotational sense than in outerHull
                SimpleHull cutHull = new SimpleHull();
                cutHull.AddVertex(origin);
                for (int i = 0; i < iSteps; i++)
                {
                    double angle = fProfileBeginAngle - i * dStepWidth; // we count against the angle orientation!!!!
                    Vertex v = Vertex.FromAngle(angle * Math.PI / 180.0);
                    cutHull.AddVertex(v);
                }
                Vertex legEnd = Vertex.FromAngle(fProfileEndAngle * Math.PI / 180.0);
                // Calculated separately to avoid errors
                cutHull.AddVertex(legEnd);

                // m_log.DebugFormat("Starting cutting of the hollow shape from the prim {1}", 0, primName);
                SimpleHull cuttedHull = SimpleHull.SubtractHull(outerHull, cutHull);

                outerHull = cuttedHull;
            }

            // Deal with the hole here
            if (hollowFactor > 0)
            {
                SimpleHull holeHull = BuildHoleHull(primShape, primShape.ProfileShape, primShape.HollowShape, hollowFactor);
                if (holeHull != null)
                {
                    SimpleHull hollowedHull = SimpleHull.SubtractHull(outerHull, holeHull);

                    outerHull = hollowedHull;
                }
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

            //System.Console.WriteLine("taperFactorX: " + taperX.ToString());
            //System.Console.WriteLine("taperFactorY: " + taperY.ToString());

            if (taperX != 100)
            {
                if (taperX > 100)
                {
                    extr.taperTopFactorX = 1.0f - ((float)(taperX - 100) / 100);
                    //System.Console.WriteLine("taperTopFactorX: " + extr.taperTopFactorX.ToString());
                }
                else
                {
                    extr.taperBotFactorX = 1.0f - ((100 - (float)taperX) / 100);
                    //System.Console.WriteLine("taperBotFactorX: " + extr.taperBotFactorX.ToString());
                }

            }

            if (taperY != 100)
            {
                if (taperY > 100)
                {
                    extr.taperTopFactorY = 1.0f - ((float)(taperY - 100) / 100);
                   // System.Console.WriteLine("taperTopFactorY: " + extr.taperTopFactorY.ToString());
                }
                else
                {
                    extr.taperBotFactorY = 1.0f - ((100 - (float)taperY) / 100);
                    //System.Console.WriteLine("taperBotFactorY: " + extr.taperBotFactorY.ToString());
                }
            }

            if (pathShearX != 0)
            {
                if (pathShearX > 50)
                {
                    // Complimentary byte.  Negative values wrap around the byte.  Positive values go up to 50
                    extr.pushX = (((float)(256 - pathShearX) / 100) * -1f);
                    //m_log.Warn("pushX: " + extr.pushX);
                }
                else
                {
                    extr.pushX = (float)pathShearX / 100;
                    //m_log.Warn("pushX: " + extr.pushX);
                }
            }

            if (pathShearY != 0)
            {
                if (pathShearY > 50)
                {
                    // Complimentary byte.  Negative values wrap around the byte.  Positive values go up to 50
                    extr.pushY = (((float)(256 - pathShearY) / 100) * -1f);
                    //m_log.Warn("pushY: " + extr.pushY);
                }
                else
                {
                    extr.pushY = (float)pathShearY / 100;
                    //m_log.Warn("pushY: " + extr.pushY);
                }

            }

            //if (twistTop != 0)
            //{
            //    extr.twistTop = 180 * ((float)twistTop / 100);
            //    if (extr.twistTop > 0)
            //    {
            //        extr.twistTop = 360 - (-1 * extr.twistTop);

            //    }


            //    extr.twistTop = (float)(extr.twistTop * DEG_TO_RAD);
            //}

            //float twistMid = ((twistTop + twistBot) * 0.5f);

            //if (twistMid != 0)
            //{
            //    extr.twistMid = 180 * ((float)twistMid / 100);
            //    if (extr.twistMid > 0)
            //    {
            //        extr.twistMid = 360 - (-1 * extr.twistMid);
            //    }
            //    extr.twistMid = (float)(extr.twistMid * DEG_TO_RAD);
            //}

            //if (twistBot != 0)
            //{
            //    extr.twistBot = 180 * ((float)twistBot / 100);
            //    if (extr.twistBot > 0)
            //    {
            //        extr.twistBot = 360 - (-1 * extr.twistBot);
            //    }
            //    extr.twistBot = (float)(extr.twistBot * DEG_TO_RAD);
            //}

            extr.twistTop = (float)primShape.PathTwist * (float)Math.PI * 0.01f;
            extr.twistBot = (float)primShape.PathTwistBegin * (float)Math.PI * 0.01f;
            extr.pathBegin = primShape.PathBegin;
            extr.pathEnd = primShape.PathEnd;
            
            //System.Console.WriteLine("[MESH]: twistTop = " + twistTop.ToString() + "|" + extr.twistTop.ToString() + ", twistMid = " + twistMid.ToString() + "|" + extr.twistMid.ToString() + ", twistbot = " + twistBot.ToString() + "|" + extr.twistBot.ToString());
            //Mesh result = extr.Extrude(m);
            Mesh result = extr.ExtrudeLinearPath(m);
            result.DumpRaw(baseDir, primName, "Z extruded");
#if SPAM
            int vCount = 0;

            foreach (Vertex v in result.vertices)
                if (v != null)
                    vCount++;
            System.Console.WriteLine("Mesh vertex count: " + vCount.ToString());
#endif
            return result;
        }

        private static Mesh CreatePrismMesh(String primName, PrimitiveBaseShape primShape, PhysicsVector size)
        // Builds the z (+ and -) surfaces of a box shaped prim
        {
            UInt16 hollowFactor = primShape.ProfileHollow;
            UInt16 profileBegin = primShape.ProfileBegin;
            UInt16 profileEnd = primShape.ProfileEnd;
            UInt16 taperX = primShape.PathScaleX;
            UInt16 taperY = primShape.PathScaleY;
            UInt16 pathShearX = primShape.PathShearX;
            UInt16 pathShearY = primShape.PathShearY;

            // Int16 twistTop = primShape.PathTwistBegin;
            // Int16 twistBot = primShape.PathTwist;

#if SPAM
            reportPrimParams("[PRISM] " + primName, primShape);
#endif

            //m_log.Error("pathShear:" + primShape.PathShearX.ToString() + "," + primShape.PathShearY.ToString());
            //m_log.Error("pathTaper:" + primShape.PathTaperX.ToString() + "," + primShape.PathTaperY.ToString());
            //m_log.Error("ProfileBegin:" + primShape.ProfileBegin.ToString() + "," + primShape.ProfileBegin.ToString());
            //m_log.Error("PathScale:" + primShape.PathScaleX.ToString() + "," + primShape.PathScaleY.ToString());

            // Procedure: This is based on the fact that the upper (plus) and lower (minus) Z-surface
            // of a block are basically the same
            // They may be warped differently but the shape is identical
            // So we only create one surface as a model and derive both plus and minus surface of the block from it
            // This is done in a model space where the block spans from -.5 to +.5 in X and Y
            // The mapping to Scene space is done later during the "extrusion" phase

            // Base
            Vertex MM = new Vertex(-0.25f, -0.45f, 0.0f);
            Vertex PM = new Vertex(+0.5f, 0f, 0.0f);
            Vertex PP = new Vertex(-0.25f, +0.45f, 0.0f);


            SimpleHull outerHull = new SimpleHull();
            //outerHull.AddVertex(MM);
            //outerHull.AddVertex(PM);
            //outerHull.AddVertex(PP);
            outerHull.AddVertex(PP);
            outerHull.AddVertex(MM);
            outerHull.AddVertex(PM);

            // Deal with cuts now
            if ((profileBegin != 0) || (profileEnd != 0))
            {
                double fProfileBeginAngle = profileBegin / 50000.0 * 360.0;
                // In degree, for easier debugging and understanding
                //fProfileBeginAngle -= (90.0 + 45.0); // for some reasons, the SL client counts from the corner -X/-Y
                double fProfileEndAngle = 360.0 - profileEnd / 50000.0 * 360.0; // Pathend comes as complement to 1.0
                //fProfileEndAngle -= (90.0 + 45.0);
                if (fProfileBeginAngle < fProfileEndAngle)
                    fProfileEndAngle -= 360.0;

                // Note, that we don't want to cut out a triangle, even if this is a
                // good approximation for small cuts. Indeed we want to cut out an arc
                // and we approximate this arc by a polygon chain
                // Also note, that these vectors are of length 1.0 and thus their endpoints lay outside the model space
                // So it can easily be subtracted from the outer hull
                int iSteps = (int)(((fProfileBeginAngle - fProfileEndAngle) / 45.0) + .5);
                // how many steps do we need with approximately 45 degree
                double dStepWidth = (fProfileBeginAngle - fProfileEndAngle) / iSteps;

                Vertex origin = new Vertex(0.0f, 0.0f, 0.0f);

                // Note the sequence of vertices here. It's important to have the other rotational sense than in outerHull
                SimpleHull cutHull = new SimpleHull();
                cutHull.AddVertex(origin);
                for (int i = 0; i < iSteps; i++)
                {
                    double angle = fProfileBeginAngle - i * dStepWidth; // we count against the angle orientation!!!!
                    Vertex v = Vertex.FromAngle(angle * Math.PI / 180.0);
                    cutHull.AddVertex(v);
                }
                Vertex legEnd = Vertex.FromAngle(fProfileEndAngle * Math.PI / 180.0);
                // Calculated separately to avoid errors
                cutHull.AddVertex(legEnd);

                //m_log.DebugFormat("Starting cutting of the hollow shape from the prim {1}", 0, primName);
                SimpleHull cuttedHull = SimpleHull.SubtractHull(outerHull, cutHull);

                outerHull = cuttedHull;
            }

            // Deal with the hole here
            if (hollowFactor > 0)
            {
                SimpleHull holeHull = BuildHoleHull(primShape, primShape.ProfileShape, primShape.HollowShape, hollowFactor);
                if (holeHull != null)
                {
                    SimpleHull hollowedHull = SimpleHull.SubtractHull(outerHull, holeHull);

                    outerHull = hollowedHull;
                }
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

            if (taperX != 100)
            {
                if (taperX > 100)
                {
                    extr.taperTopFactorX = 1.0f - ((float)(taperX - 100) / 100);
                    //System.Console.WriteLine("taperTopFactorX: " + extr.taperTopFactorX.ToString());
                }
                else
                {
                    extr.taperBotFactorX = 1.0f - ((100 - (float)taperX) / 100);
                    //System.Console.WriteLine("taperBotFactorX: " + extr.taperBotFactorX.ToString());
                }

            }

            if (taperY != 100)
            {
                if (taperY > 100)
                {
                    extr.taperTopFactorY = 1.0f - ((float)(taperY - 100) / 100);
                    // System.Console.WriteLine("taperTopFactorY: " + extr.taperTopFactorY.ToString());
                }
                else
                {
                    extr.taperBotFactorY = 1.0f - ((100 - (float)taperY) / 100);
                    //System.Console.WriteLine("taperBotFactorY: " + extr.taperBotFactorY.ToString());
                }
            }

            if (pathShearX != 0)
            {
                if (pathShearX > 50)
                {
                    // Complimentary byte.  Negative values wrap around the byte.  Positive values go up to 50
                    extr.pushX = (((float)(256 - pathShearX) / 100) * -1f);
                    // m_log.Warn("pushX: " + extr.pushX);
                }
                else
                {
                    extr.pushX = (float)pathShearX / 100;
                    // m_log.Warn("pushX: " + extr.pushX);
                }
            }

            if (pathShearY != 0)
            {
                if (pathShearY > 50)
                {
                    // Complimentary byte.  Negative values wrap around the byte.  Positive values go up to 50
                    extr.pushY = (((float)(256 - pathShearY) / 100) * -1f);
                    //m_log.Warn("pushY: " + extr.pushY);
                }
                else
                {
                    extr.pushY = (float)pathShearY / 100;
                    //m_log.Warn("pushY: " + extr.pushY);
                }
            }

            //if (twistTop != 0)
            //{
            //    extr.twistTop = 180 * ((float)twistTop / 100);
            //    if (extr.twistTop > 0)
            //    {
            //        extr.twistTop = 360 - (-1 * extr.twistTop);

            //    }


            //    extr.twistTop = (float)(extr.twistTop * DEG_TO_RAD);
            //}

            //float twistMid = ((twistTop + twistBot) * 0.5f);

            //if (twistMid != 0)
            //{
            //    extr.twistMid = 180 * ((float)twistMid / 100);
            //    if (extr.twistMid > 0)
            //    {
            //        extr.twistMid = 360 - (-1 * extr.twistMid);
            //    }
            //    extr.twistMid = (float)(extr.twistMid * DEG_TO_RAD);
            //}

            //if (twistBot != 0)
            //{
            //    extr.twistBot = 180 * ((float)twistBot / 100);
            //    if (extr.twistBot > 0)
            //    {
            //        extr.twistBot = 360 - (-1 * extr.twistBot);
            //    }
            //    extr.twistBot = (float)(extr.twistBot * DEG_TO_RAD);
            //}

            extr.twistTop = (float)primShape.PathTwist * (float)Math.PI * 0.01f;
            extr.twistBot = (float)primShape.PathTwistBegin * (float)Math.PI * 0.01f;
            extr.pathBegin = primShape.PathBegin;
            extr.pathEnd = primShape.PathEnd;

            //System.Console.WriteLine("[MESH]: twistTop = " + twistTop.ToString() + "|" + extr.twistTop.ToString() + ", twistMid = " + twistMid.ToString() + "|" + extr.twistMid.ToString() + ", twistbot = " + twistBot.ToString() + "|" + extr.twistBot.ToString());
            //Mesh result = extr.Extrude(m);
            Mesh result = extr.ExtrudeLinearPath(m);
            result.DumpRaw(baseDir, primName, "Z extruded");
#if SPAM
            int vCount = 0;

            foreach (Vertex v in result.vertices)
                if (v != null)
                    vCount++;
            System.Console.WriteLine("Mesh vertex count: " + vCount.ToString());
#endif
            return result;
        }

        /// <summary>
        /// builds an icosahedral geodesic sphere - used as default in place of problem meshes
        /// </summary>
        /// <param name="primName"></param>
        /// <param name="primShape"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        private static Mesh CreateSphereMesh(String primName, PrimitiveBaseShape primShape, PhysicsVector size)
        {
            // Builds an icosahedral geodesic sphere
            // based on an article by Paul Bourke
            // http://local.wasp.uwa.edu.au/~pbourke/
            // articles:
            // http://local.wasp.uwa.edu.au/~pbourke/geometry/polygonmesh/
            // and
            // http://local.wasp.uwa.edu.au/~pbourke/geometry/polyhedra/index.html

            // Still have more to do here.

            // UInt16 hollowFactor = primShape.ProfileHollow;
            // UInt16 profileBegin = primShape.ProfileBegin;
            // UInt16 profileEnd = primShape.ProfileEnd;
            // UInt16 taperX = primShape.PathScaleX;
            // UInt16 taperY = primShape.PathScaleY;
            // UInt16 pathShearX = primShape.PathShearX;
            // UInt16 pathShearY = primShape.PathShearY;
            Mesh m = new Mesh();

#if SPAM
            reportPrimParams("[SPHERE] " + primName, primShape);
#endif

            float LOD = 0.2f;
            float diameter = 0.5f;// Our object will result in -0.5 to 0.5
            float sq5 = (float) Math.Sqrt(5.0);
            float phi = (1 + sq5) * 0.5f;
            float rat = (float) Math.Sqrt(10f + (2f * sq5)) / (4f * phi);
            float a = (diameter / rat) * 0.5f;
            float b = (diameter / rat) / (2.0f * phi);


            // 12 Icosahedron vertexes
            Vertex v1 = new Vertex(0f, b, -a);
            Vertex v2 = new Vertex(b, a, 0f);
            Vertex v3 = new Vertex(-b, a, 0f);
            Vertex v4 = new Vertex(0f, b, a);
            Vertex v5 = new Vertex(0f, -b, a);
            Vertex v6 = new Vertex(-a, 0f, b);
            Vertex v7 = new Vertex(0f, -b, -a);
            Vertex v8 = new Vertex(a, 0f, -b);
            Vertex v9 = new Vertex(a, 0f, b);
            Vertex v10 = new Vertex(-a, 0f, -b);
            Vertex v11 = new Vertex(b, -a, 0);
            Vertex v12 = new Vertex(-b, -a, 0);



            // Base Faces of the Icosahedron (20)
            SphereLODTriangle(v1, v2, v3, diameter, LOD, m);
            SphereLODTriangle(v4, v3, v2, diameter, LOD, m);
            SphereLODTriangle(v4, v5, v6, diameter, LOD, m);
            SphereLODTriangle(v4, v9, v5, diameter, LOD, m);
            SphereLODTriangle(v1, v7, v8, diameter, LOD, m);
            SphereLODTriangle(v1, v10, v7, diameter, LOD, m);
            SphereLODTriangle(v5, v11, v12, diameter, LOD, m);
            SphereLODTriangle(v7, v12, v11, diameter, LOD, m);
            SphereLODTriangle(v3, v6, v10, diameter, LOD, m);
            SphereLODTriangle(v12, v10, v6, diameter, LOD, m);
            SphereLODTriangle(v2, v8, v9, diameter, LOD, m);
            SphereLODTriangle(v11, v9, v8, diameter, LOD, m);
            SphereLODTriangle(v4, v6, v3, diameter, LOD, m);
            SphereLODTriangle(v4, v2, v9, diameter, LOD, m);
            SphereLODTriangle(v1, v3, v10, diameter, LOD, m);
            SphereLODTriangle(v1, v8, v2, diameter, LOD, m);
            SphereLODTriangle(v7, v10, v12, diameter, LOD, m);
            SphereLODTriangle(v7, v11, v8, diameter, LOD, m);
            SphereLODTriangle(v5, v12, v6, diameter, LOD, m);
            SphereLODTriangle(v5, v9, v11, diameter, LOD, m);

            // Scale the mesh based on our prim scale
            foreach (Vertex v in m.vertices)
            {
                v.X *= size.X;
                v.Y *= size.Y;
                v.Z *= size.Z;
            }

            // This was built with the normals pointing inside..
            // therefore we have to invert the normals
            foreach (Triangle t in m.triangles)
            {
                t.invertNormal();
            }
            // Dump the faces for visualization in blender.
            m.DumpRaw(baseDir, primName, "Icosahedron");
#if SPAM
            int vCount = 0;

            foreach (Vertex v in m.vertices)
                if (v != null)
                    vCount++;
            System.Console.WriteLine("Mesh vertex count: " + vCount.ToString());
#endif

            return m;
        }
        private SculptMesh CreateSculptMesh(string primName, PrimitiveBaseShape primShape, PhysicsVector size, float lod)
        {

#if SPAM
            reportPrimParams("[SCULPT] " + primName, primShape);
#endif

            SculptMesh sm = new SculptMesh(primShape.SculptData, lod);
            // Scale the mesh based on our prim scale
            foreach (Vertex v in sm.vertices)
            {
                v.X *= 0.5f;
                v.Y *= 0.5f;
                v.Z *= 0.5f;
                v.X *= size.X;
                v.Y *= size.Y;
                v.Z *= size.Z;
            }
            // This was built with the normals pointing inside..
            // therefore we have to invert the normals
            foreach (Triangle t in sm.triangles)
            {
                t.invertNormal();
            }
            sm.DumpRaw(baseDir, primName, "Sculpt");
            return sm;

        }

        /// <summary>
        /// Creates a mesh for prim types torus, ring, tube, and sphere 
        /// </summary>
        /// <param name="primName"></param>
        /// <param name="primShape"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        private static Mesh CreateCircularPathMesh(String primName, PrimitiveBaseShape primShape, PhysicsVector size)
        {

            UInt16 hollowFactor = primShape.ProfileHollow;
            UInt16 profileBegin = primShape.ProfileBegin;
            UInt16 profileEnd = primShape.ProfileEnd;
            // UInt16 taperX = primShape.PathScaleX;
            // UInt16 taperY = primShape.PathScaleY;
            UInt16 pathShearX = primShape.PathShearX;
            UInt16 pathShearY = primShape.PathShearY;
            // Int16 twistBot = primShape.PathTwist;
            // Int16 twistTop = primShape.PathTwistBegin;
            HollowShape hollowShape = primShape.HollowShape;

#if SPAM
            reportPrimParams("[CIRCULAR PATH PRIM] " + primName, primShape);
            Console.WriteLine("pathTwist: " + primShape.PathTwist.ToString() + " pathTwistBegin: " + primShape.PathTwistBegin.ToString());
            Console.WriteLine("primShape.ProfileCurve & 0x07: " + Convert.ToString(primShape.ProfileCurve & 0x07));

#endif

            SimpleHull outerHull = new SimpleHull();

            if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.Circle)
            {
#if SPAM
                Console.WriteLine("Meshmerizer thinks " + primName + " is a TORUS");
#endif
                if (hollowShape == HollowShape.Same)
                    hollowShape = HollowShape.Circle;

                // build the profile shape
                // counter-clockwise around the quadrants, start at 45 degrees

                outerHull.AddVertex(new Vertex(0.353553f, 0.353553f, 0.0f)); // 45 degrees
                outerHull.AddVertex(new Vertex(0.250000f, 0.433013f, 0.0f)); // 60 degrees
                outerHull.AddVertex(new Vertex(0.129410f, 0.482963f, 0.0f)); // 75 degrees
                outerHull.AddVertex(new Vertex(0.000000f, 0.500000f, 0.0f)); // 90 degrees
                outerHull.AddVertex(new Vertex(-0.129410f, 0.482963f, 0.0f)); // 105 degrees
                outerHull.AddVertex(new Vertex(-0.250000f, 0.433013f, 0.0f)); // 120 degrees
                outerHull.AddVertex(new Vertex(-0.353553f, 0.353553f, 0.0f)); // 135 degrees
                outerHull.AddVertex(new Vertex(-0.433013f, 0.250000f, 0.0f)); // 150 degrees
                outerHull.AddVertex(new Vertex(-0.482963f, 0.129410f, 0.0f)); // 165 degrees
                outerHull.AddVertex(new Vertex(-0.500000f, 0.000000f, 0.0f)); // 180 degrees
                outerHull.AddVertex(new Vertex(-0.482963f, -0.129410f, 0.0f)); // 195 degrees
                outerHull.AddVertex(new Vertex(-0.433013f, -0.250000f, 0.0f)); // 210 degrees
                outerHull.AddVertex(new Vertex(-0.353553f, -0.353553f, 0.0f)); // 225 degrees
                outerHull.AddVertex(new Vertex(-0.250000f, -0.433013f, 0.0f)); // 240 degrees
                outerHull.AddVertex(new Vertex(-0.129410f, -0.482963f, 0.0f)); // 255 degrees
                outerHull.AddVertex(new Vertex(0.000000f, -0.500000f, 0.0f)); // 270 degrees
                outerHull.AddVertex(new Vertex(0.129410f, -0.482963f, 0.0f)); // 285 degrees
                outerHull.AddVertex(new Vertex(0.250000f, -0.433013f, 0.0f)); // 300 degrees
                outerHull.AddVertex(new Vertex(0.353553f, -0.353553f, 0.0f)); // 315 degrees
                outerHull.AddVertex(new Vertex(0.433013f, -0.250000f, 0.0f)); // 330 degrees
                outerHull.AddVertex(new Vertex(0.482963f, -0.129410f, 0.0f)); // 345 degrees
                outerHull.AddVertex(new Vertex(0.500000f, 0.000000f, 0.0f)); // 0 degrees
                outerHull.AddVertex(new Vertex(0.482963f, 0.129410f, 0.0f)); // 15 degrees
                outerHull.AddVertex(new Vertex(0.433013f, 0.250000f, 0.0f)); // 30 degrees
            }

            else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.Square) // a ring
            {
#if SPAM
                Console.WriteLine("Meshmerizer thinks " + primName + " is a TUBE");
#endif
                if (hollowShape == HollowShape.Same)
                    hollowShape = HollowShape.Square;

                outerHull.AddVertex(new Vertex(+0.5f, +0.5f, 0.0f));
                outerHull.AddVertex(new Vertex(-0.5f, +0.5f, 0.0f));
                outerHull.AddVertex(new Vertex(-0.5f, -0.5f, 0.0f));
                outerHull.AddVertex(new Vertex(+0.5f, -0.5f, 0.0f));
            }

            else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.EquilateralTriangle)
            {
#if SPAM
                Console.WriteLine("Meshmerizer thinks " + primName + " is a RING");
#endif
                if (hollowShape == HollowShape.Same)
                    hollowShape = HollowShape.Triangle;

                outerHull.AddVertex(new Vertex(+0.255f, -0.375f, 0.0f));
                outerHull.AddVertex(new Vertex(+0.25f, +0.375f, 0.0f));
                outerHull.AddVertex(new Vertex(-0.5f, +0.0f, 0.0f));

            }

            else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.HalfCircle)
            {
#if SPAM
                Console.WriteLine("Meshmerizer thinks " + primName + " is a SPHERE");
#endif

                // sanity check here... some spheres have inverted normals which can trap avatars
                // so for now if the shape parameters are such that this may happen, revert to the
                // geodesic sphere mesh.. the threshold is arbitrary as it seems any twist on a sphere
                // will create some inverted normals
                if (
                    (System.Math.Abs(primShape.PathTwist - primShape.PathTwistBegin) > 65)
                    || (primShape.PathBegin == 0
                        && primShape.PathEnd == 0
                        && primShape.PathTwist == 0
                        && primShape.PathTwistBegin == 0
                        && primShape.ProfileBegin == 0
                        && primShape.ProfileEnd == 0
                        && hollowFactor == 0
                        ) // simple sphere, revert to geodesic shape

                )
                {
#if SPAM
                    System.Console.WriteLine("reverting to geodesic sphere for prim: " + primName);
#endif
                    return CreateSphereMesh(primName, primShape, size);
                }

                if (hollowFactor == 0)
                {
                    // the hull triangulator is happier with a minimal hollow
                    hollowFactor = 2000;
                }

                if (hollowShape == HollowShape.Same)
                    hollowShape = HollowShape.Circle;

                outerHull.AddVertex(new Vertex(0.250000f, 0.433013f, 0.0f)); // 60 degrees
                outerHull.AddVertex(new Vertex(0.129410f, 0.482963f, 0.0f)); // 75 degrees
                outerHull.AddVertex(new Vertex(0.000000f, 0.500000f, 0.0f)); // 90 degrees
                outerHull.AddVertex(new Vertex(-0.129410f, 0.482963f, 0.0f)); // 105 degrees
                outerHull.AddVertex(new Vertex(-0.250000f, 0.433013f, 0.0f)); // 120 degrees
                outerHull.AddVertex(new Vertex(-0.353553f, 0.353553f, 0.0f)); // 135 degrees
                outerHull.AddVertex(new Vertex(-0.433013f, 0.250000f, 0.0f)); // 150 degrees
                outerHull.AddVertex(new Vertex(-0.482963f, 0.129410f, 0.0f)); // 165 degrees
                outerHull.AddVertex(new Vertex(-0.500000f, 0.000000f, 0.0f)); // 180 degrees

                outerHull.AddVertex(new Vertex(0.500000f, 0.000000f, 0.0f)); // 0 degrees
                outerHull.AddVertex(new Vertex(0.482963f, 0.129410f, 0.0f)); // 15 degrees
                outerHull.AddVertex(new Vertex(0.433013f, 0.250000f, 0.0f)); // 30 degrees
                outerHull.AddVertex(new Vertex(0.353553f, 0.353553f, 0.0f)); // 45 degrees
            }

            // Deal with cuts now
            if ((profileBegin != 0) || (profileEnd != 0))
            {
                double fProfileBeginAngle = profileBegin / 50000.0 * 360.0;
                // In degree, for easier debugging and understanding
                //fProfileBeginAngle -= (90.0 + 45.0); // for some reasons, the SL client counts from the corner -X/-Y
                double fProfileEndAngle = 360.0 - profileEnd / 50000.0 * 360.0; // Pathend comes as complement to 1.0
                //fProfileEndAngle -= (90.0 + 45.0);
                if (fProfileBeginAngle < fProfileEndAngle)
                    fProfileEndAngle -= 360.0;

                if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.HalfCircle)
                { // dimpled sphere uses profile cut but since it's a half circle the angles are smaller
                    fProfileBeginAngle = 0.0036f * (float)primShape.ProfileBegin;
                    fProfileEndAngle = 180.0f - 0.0036f * (float)primShape.ProfileEnd;
                    if (fProfileBeginAngle < fProfileEndAngle)
                        fProfileEndAngle -= 360.0f;
                    // a cut starting at 0 degrees with a hollow causes an infinite loop so move the start angle
                    // past it into the empty part of the circle to avoid this condition
                    if (fProfileBeginAngle == 0.0f) fProfileBeginAngle = -10.0f;

#if SPAM
                    Console.WriteLine("Sphere dimple: fProfileBeginAngle: " + fProfileBeginAngle.ToString() + " fProfileEndAngle: " + fProfileEndAngle.ToString());
#endif
                }
                else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.Square)
                { // tube profile cut is offset 45 degrees from other prim types
                    fProfileBeginAngle += 45.0f;
                    fProfileEndAngle += 45.0f;
                    if (fProfileBeginAngle < fProfileEndAngle)
                        fProfileEndAngle -= 360.0;
                }
                else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.EquilateralTriangle)
                { // ring profile cut is offset 180 degrees from other prim types
                    fProfileBeginAngle += 180.0f;
                    fProfileEndAngle += 180.0f;
                    if (fProfileBeginAngle < fProfileEndAngle)
                        fProfileEndAngle -= 360.0;
                }

                // Note, that we don't want to cut out a triangle, even if this is a
                // good approximation for small cuts. Indeed we want to cut out an arc
                // and we approximate this arc by a polygon chain
                // Also note, that these vectors are of length 1.0 and thus their endpoints lay outside the model space
                // So it can easily be subtracted from the outer hull
                int iSteps = (int)(((fProfileBeginAngle - fProfileEndAngle) / 45.0) + .5);
                // how many steps do we need with approximately 45 degree
                double dStepWidth = (fProfileBeginAngle - fProfileEndAngle) / iSteps;

                Vertex origin = new Vertex(0.0f, 0.0f, 0.0f);

                // Note the sequence of vertices here. It's important to have the other rotational sense than in outerHull
                SimpleHull cutHull = new SimpleHull();
                cutHull.AddVertex(origin);
                for (int i = 0; i < iSteps; i++)
                {
                    double angle = fProfileBeginAngle - i * dStepWidth; // we count against the angle orientation!!!!
                    Vertex v = Vertex.FromAngle(angle * Math.PI / 180.0);
                    cutHull.AddVertex(v);
                }
                Vertex legEnd = Vertex.FromAngle(fProfileEndAngle * Math.PI / 180.0);
                // Calculated separately to avoid errors
                cutHull.AddVertex(legEnd);

                // m_log.DebugFormat("Starting cutting of the hollow shape from the prim {1}", 0, primName);
                SimpleHull cuttedHull = SimpleHull.SubtractHull(outerHull, cutHull);

                if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.Circle)
                {
                    Quaternion zFlip = new Quaternion(new Vertex(0.0f, 0.0f, 1.0f), (float)Math.PI);
                    Vertex vTmp = new Vertex(0.0f, 0.0f, 0.0f);
                    foreach (Vertex v in cuttedHull.getVertices())
                        if (v != null)
                        {
                            vTmp = v * zFlip;
                            v.X = vTmp.X;
                            v.Y = vTmp.Y;
                            v.Z = vTmp.Z;
                        }
                }

                outerHull = cuttedHull;
            }

            // Deal with the hole here
            if (hollowFactor > 0)
            {
                SimpleHull holeHull;

                if (hollowShape == HollowShape.Triangle)
                {
                    holeHull = new SimpleHull();

                    float hollowFactorF = (float)hollowFactor * 2.0e-5f;

                    if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.EquilateralTriangle)
                    {
                        holeHull.AddVertex(new Vertex(+0.125f * hollowFactorF, -0.1875f * hollowFactorF, 0.0f));
                        holeHull.AddVertex(new Vertex(-0.25f * hollowFactorF, -0f * hollowFactorF, 0.0f));
                        holeHull.AddVertex(new Vertex(+0.125f * hollowFactorF, +0.1875f * hollowFactorF, 0.0f));
                    }
                    else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.HalfCircle)
                    {
                        holeHull.AddVertex(new Vertex(-0.500000f * hollowFactorF, 0.000000f * hollowFactorF, 0.0f)); // 180 degrees
                        holeHull.AddVertex(new Vertex(-0.250000f * hollowFactorF, 0.433013f * hollowFactorF, 0.0f)); // 120 degrees
                        holeHull.AddVertex(new Vertex(0.250000f * hollowFactorF, 0.433013f * hollowFactorF, 0.0f)); // 60 degrees
                        holeHull.AddVertex(new Vertex(0.500000f * hollowFactorF, 0.000000f * hollowFactorF, 0.0f)); // 0 degrees
                    }
                    else
                    {
                        holeHull.AddVertex(new Vertex(+0.25f * hollowFactorF, -0.45f * hollowFactorF, 0.0f));
                        holeHull.AddVertex(new Vertex(-0.5f * hollowFactorF, -0f * hollowFactorF, 0.0f));
                        holeHull.AddVertex(new Vertex(+0.25f * hollowFactorF, +0.45f * hollowFactorF, 0.0f));
                    }
                }
                else if (hollowShape == HollowShape.Square && (primShape.ProfileCurve & 0x07) == (byte)ProfileShape.HalfCircle)
                {
                    holeHull = new SimpleHull();

                    float hollowFactorF = (float)hollowFactor * 2.0e-5f;

                    holeHull.AddVertex(new Vertex(-0.707f * hollowFactorF, 0.0f, 0.0f)); // 180 degrees
                    holeHull.AddVertex(new Vertex(0.0f, 0.707f * hollowFactorF, 0.0f)); // 120 degrees
                    holeHull.AddVertex(new Vertex(0.707f * hollowFactorF, 0.0f, 0.0f)); // 60 degrees
                }
                else
                {
                    holeHull = BuildHoleHull(primShape, primShape.ProfileShape, hollowShape, hollowFactor);
                }

                if (holeHull != null)
                {
                    SimpleHull hollowedHull = SimpleHull.SubtractHull(outerHull, holeHull);

                    outerHull = hollowedHull;
                }
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
                t.invertNormal();

            // Vertex vTemp = new Vertex(0.0f, 0.0f, 0.0f);

            
            float skew = primShape.PathSkew * 0.01f;
            float pathScaleX = (float)(200 - primShape.PathScaleX) * 0.01f;
            float pathScaleY = (float)(200 - primShape.PathScaleY) * 0.01f;
            float profileXComp = pathScaleX * (1.0f - Math.Abs(skew));

#if SPAM
            //Console.WriteLine("primShape.PathScaleX: " + primShape.PathScaleX.ToString() + " primShape.PathScaleY: " + primShape.PathScaleY.ToString());
            //Console.WriteLine("primShape.PathSkew: " + primShape.PathSkew.ToString() + " primShape.PathRadiusOffset: " + primShape.PathRadiusOffset.ToString() + " primShape.pathRevolutions: " + primShape.PathRevolutions.ToString());
            Console.WriteLine("PathScaleX: " + pathScaleX.ToString() + " pathScaleY: " + pathScaleY.ToString());
            Console.WriteLine("skew: " + skew.ToString() + " profileXComp: " + profileXComp.ToString());
#endif

            
            foreach (Vertex v in m.vertices)
                if (v != null)
                {
                    v.X *= profileXComp;
                    v.Y *= pathScaleY;
                    //v.Y *= 0.5f; // torus profile is scaled in y axis
                }

            Extruder extr = new Extruder();

            extr.size = size;
            extr.pathScaleX = pathScaleX;
            extr.pathScaleY = pathScaleY;
            extr.pathCutBegin = 0.00002f * primShape.PathBegin;
            extr.pathCutEnd = 0.00002f * (50000 - primShape.PathEnd);
            extr.pathBegin = primShape.PathBegin;
            extr.pathEnd = primShape.PathEnd;
            extr.skew = skew;
            extr.revolutions = 1.0f + (float)primShape.PathRevolutions * 3.0f / 200.0f;
            extr.pathTaperX = 0.01f * (float)primShape.PathTaperX;
            extr.pathTaperY = 0.01f * (float)primShape.PathTaperY;

            extr.radius = 0.01f * (float)primShape.PathRadiusOffset;

#if SPAM
            //System.Console.WriteLine("primShape.PathBegin: " + primShape.PathBegin.ToString() + " primShape.PathEnd: " + primShape.PathEnd.ToString());
            System.Console.WriteLine("extr.pathCutBegin: " + extr.pathCutBegin.ToString() + " extr.pathCutEnd: " + extr.pathCutEnd.ToString());
            System.Console.WriteLine("extr.revolutions: " + extr.revolutions.ToString());

            //System.Console.WriteLine("primShape.PathTaperX: " + primShape.PathTaperX.ToString());
            //System.Console.WriteLine("primShape.PathTaperY: " + primShape.PathTaperY.ToString());

            
            //System.Console.WriteLine("primShape.PathRadiusOffset: " + primShape.PathRadiusOffset.ToString());
#endif




            if (pathShearX != 0)
            {
                if (pathShearX > 50)
                {
                    // Complimentary byte.  Negative values wrap around the byte.  Positive values go up to 50
                    extr.pushX = (((float)(256 - pathShearX) / 100) * -1f);
                    //m_log.Warn("pushX: " + extr.pushX);
                }
                else
                {
                    extr.pushX = (float)pathShearX / 100;
                    //m_log.Warn("pushX: " + extr.pushX);
                }
            }

            if (pathShearY != 0)
            {
                if (pathShearY > 50)
                {
                    // Complimentary byte.  Negative values wrap around the byte.  Positive values go up to 50
                    extr.pushY = (((float)(256 - pathShearY) / 100) * -1f);
                    //m_log.Warn("pushY: " + extr.pushY);
                }
                else
                {
                    extr.pushY = (float)pathShearY / 100;
                    //m_log.Warn("pushY: " + extr.pushY);
                }

            }

            extr.twistTop = (float)primShape.PathTwist * (float)Math.PI * 0.02f;
            extr.twistBot = (float)primShape.PathTwistBegin * (float)Math.PI * 0.02f;

            //System.Console.WriteLine("[MESH]: twistTop = " + twistTop.ToString() + "|" + extr.twistTop.ToString() + ", twistMid = " + twistMid.ToString() + "|" + extr.twistMid.ToString() + ", twistbot = " + twistBot.ToString() + "|" + extr.twistBot.ToString());
            Mesh result = extr.ExtrudeCircularPath(m);
            result.DumpRaw(baseDir, primName, "Z extruded");

#if SPAM
            int vCount = 0;

            foreach (Vertex v in result.vertices)
            {
                if (v != null)
                    vCount++;
            }

            System.Console.WriteLine("Mesh vertex count: " + vCount.ToString());
#endif

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

        public static Vertex midUnitRadialPoint(Vertex a, Vertex b, float radius)
        {
            Vertex midpoint = new Vertex(a + b) * 0.5f;
            return  (midpoint.normalize() * radius);
        }

        public static void SphereLODTriangle(Vertex a, Vertex b, Vertex c, float diameter, float LOD, Mesh m)
        {
            Vertex aa = a - b;
            Vertex ba = b - c;
            Vertex da = c - a;

            if (((aa.length() < LOD) && (ba.length() < LOD) && (da.length() < LOD)))
            {
                // We don't want duplicate verticies.  Duplicates cause the scale algorithm to produce a spikeball
                // spikes are novel, but we want ellipsoids.

                if (!m.vertices.Contains(a))
                    m.Add(a);
                if (!m.vertices.Contains(b))
                    m.Add(b);
                if (!m.vertices.Contains(c))
                    m.Add(c);

                // Add the triangle to the mesh
                Triangle t = new Triangle(a, b, c);
                m.Add(t);
            }
            else
            {
                Vertex ab = midUnitRadialPoint(a, b, diameter);
                Vertex bc = midUnitRadialPoint(b, c, diameter);
                Vertex ca = midUnitRadialPoint(c, a, diameter);

                // Recursive!  Splits the triangle up into 4 smaller triangles
                SphereLODTriangle(a, ab, ca, diameter, LOD, m);
                SphereLODTriangle(ab, b, bc, diameter, LOD, m);
                SphereLODTriangle(ca, bc, c, diameter, LOD, m);
                SphereLODTriangle(ab, bc, ca, diameter, LOD, m);

            }
        }

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, PhysicsVector size, float lod)
        {
            Mesh mesh = null;
            if (primShape.SculptEntry && primShape.SculptType != (byte)0 && primShape.SculptData.Length > 0)
            {
                
                SculptMesh smesh = CreateSculptMesh(primName, primShape, size, lod);
                mesh = (Mesh)smesh;
                CalcNormals(mesh);
            }
            else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.Square)
            {
                if (primShape.PathCurve == (byte)LLObject.PathCurve.Line)
                { // its a box
                    mesh = CreateBoxMesh(primName, primShape, size);
                    CalcNormals(mesh);
                }
                else if (primShape.PathCurve == (byte)LLObject.PathCurve.Circle)
                { // tube
                    // do a cylinder for now
                    //mesh = CreateCylinderMesh(primName, primShape, size);
                    mesh = CreateCircularPathMesh(primName, primShape, size);
                    CalcNormals(mesh);
                }
            }
            else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.Circle)
            {
                if (primShape.PathCurve == (byte)Extrusion.Straight)
                {
                    mesh = CreateCylinderMesh(primName, primShape, size);
                    CalcNormals(mesh);
                }

                // look at LLObject.cs in libsecondlife for how to know the prim type
                // ProfileCurve seems to combine hole shape and profile curve so we need to only compare against the lower 3 bits
                else if (primShape.PathCurve == (byte) Extrusion.Curve1 && LLObject.UnpackPathScale(primShape.PathScaleY) <= 0.75f)
                {  // dahlia's favorite, a torus :)
                    mesh = CreateCircularPathMesh(primName, primShape, size);
                    CalcNormals(mesh);
                }
            }
            else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.HalfCircle)
            {
                if (primShape.PathCurve == (byte)Extrusion.Curve1 || primShape.PathCurve == (byte) Extrusion.Curve2)
                {
                    //mesh = CreateSphereMesh(primName, primShape, size);
                    mesh = CreateCircularPathMesh(primName, primShape, size);
                    CalcNormals(mesh);
                }
            }
            else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.EquilateralTriangle)
            {
                if (primShape.PathCurve == (byte)Extrusion.Straight)
                {
                    mesh = CreatePrismMesh(primName, primShape, size);
                    CalcNormals(mesh);
                }
                else if (primShape.PathCurve == (byte) Extrusion.Curve1)
                {  // a ring - do a cylinder for now
                    //mesh = CreateCylinderMesh(primName, primShape, size);
                    mesh = CreateCircularPathMesh(primName, primShape, size);
                    CalcNormals(mesh);
                }
            }
            else // just do a box
            {
                mesh = CreateBoxMesh(primName, primShape, size);
                CalcNormals(mesh);
            }

            //else
            //{
            //    switch (primShape.ProfileShape)
            //    {
            //        case ProfileShape.Square:
            //            mesh = CreateBoxMesh(primName, primShape, size);
            //            CalcNormals(mesh);
            //            break;
            //        case ProfileShape.Circle:
            //            if (primShape.PathCurve == (byte)Extrusion.Straight)
            //            {
            //                mesh = CreateCylinderMesh(primName, primShape, size);
            //                CalcNormals(mesh);
            //            }

            //            // look at LLObject.cs in libsecondlife for how to know the prim type
            //            // ProfileCurve seems to combine hole shape and profile curve so we need to only compare against the lower 3 bits
            //            else if ((primShape.ProfileCurve & 0x07) == (byte)LLObject.ProfileCurve.Circle && LLObject.UnpackPathScale(primShape.PathScaleY) <= 0.75f)
            //            {  // dahlia's favorite, a torus :)
            //                mesh = CreateCylinderMesh(primName, primShape, size);
            //                CalcNormals(mesh);
            //            }

            //            break;
            //        case ProfileShape.HalfCircle:
            //            if (primShape.PathCurve == (byte)Extrusion.Curve1)
            //            {
            //                mesh = CreateSphereMesh(primName, primShape, size);
            //                CalcNormals(mesh);
            //            }
            //            break;

            //        case ProfileShape.EquilateralTriangle:
            //            mesh = CreatePrismMesh(primName, primShape, size);
            //            CalcNormals(mesh);
            //            break;

            //        default:
            //            mesh = CreateBoxMesh(primName, primShape, size);
            //            CalcNormals(mesh);
            //            //Set default mesh to cube otherwise it'll return
            //            // null and crash on the 'setMesh' method in the physics plugins.
            //            //mesh = null;
            //            break;
            //    }
            //}

            return mesh;
        }

#if SPAM
        // please dont comment this out until I'm done with this module - dahlia
        private static void reportPrimParams(string name, PrimitiveBaseShape primShape)
        {

            float pathShearX = primShape.PathShearX < 128 ? (float)primShape.PathShearX * 0.01f : (float)(primShape.PathShearX - 256) * 0.01f;
            float pathShearY = primShape.PathShearY < 128 ? (float)primShape.PathShearY * 0.01f : (float)(primShape.PathShearY - 256) * 0.01f;
            float pathBegin = (float)primShape.PathBegin * 2.0e-5f;
            float pathEnd = 1.0f - (float)primShape.PathEnd * 2.0e-5f;
            
            float profileBegin = (float)primShape.ProfileBegin * 2.0e-5f;
            float profileEnd = 1.0f - (float)primShape.ProfileEnd * 2.0e-5f;

            Console.WriteLine("********************* PrimitiveBaseShape Parameters *******************\n"
                + "Name.............: " + name.ToString() + "\n"
                + "HollowShape......: " + primShape.HollowShape.ToString() + "\n"
                + "PathBegin........: " + primShape.PathBegin.ToString() + " " + pathBegin.ToString() + "\n"
                + "PathCurve........: " + primShape.PathCurve.ToString() + "\n"
                + "PathEnd..........: " + primShape.PathEnd.ToString() + " " + pathEnd.ToString() + "\n"
                + "PathRadiusOffset.: " + primShape.PathRadiusOffset.ToString() + "\n"
                + "PathRevolutions..: " + primShape.PathRevolutions.ToString() + "\n"
                + "PathScaleX.......: " + primShape.PathScaleX.ToString() + "\n"
                + "PathScaleY.......: " + primShape.PathScaleY.ToString() + "\n"
                + "PathShearX.......: " + primShape.PathShearX.ToString() + " (" + pathShearX.ToString() + ")\n"
                + "PathShearY.......: " + primShape.PathShearY.ToString() + " (" + pathShearY.ToString() + ")\n"
                + "PathSkew.........: " + primShape.PathSkew.ToString() + "\n"
                + "PathTaperX.......: " + primShape.PathTaperX.ToString() + "\n"
                + "PathTaperY.......: " + primShape.PathTaperY.ToString() + "\n"
                + "PathTwist........: " + primShape.PathTwist.ToString() + "\n"
                + "PathTwistBegin...: " + primShape.PathTwistBegin.ToString() + "\n"
                + "ProfileBegin.....: " + primShape.ProfileBegin.ToString() + " " + profileBegin.ToString() + "\n"
                + "ProfileCurve.....: " + primShape.ProfileCurve.ToString() + "\n"
                + "ProfileEnd.......: " + primShape.ProfileEnd.ToString() + " " + profileEnd.ToString() + "\n"
                + "ProfileHollow....: " + primShape.ProfileHollow.ToString() + "\n"
                + "ProfileShape.....: " + primShape.ProfileShape.ToString() + "\n"
                );
        }
#endif
    }
}
