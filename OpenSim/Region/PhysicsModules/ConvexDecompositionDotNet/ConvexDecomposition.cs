/* The MIT License
 * 
 * Copyright (c) 2010 Intel Corporation.
 * All rights reserved.
 *
 * Based on the convexdecomposition library from 
 * <http://codesuppository.googlecode.com> by John W. Ratcliff and Stan Melax.
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenSim.Region.PhysicsModules.ConvexDecompositionDotNet
{
    public delegate void ConvexDecompositionCallback(ConvexResult result);

    public class FaceTri
    {
        public float3 P1;
        public float3 P2;
        public float3 P3;

        public FaceTri() { }

        public FaceTri(List<float3> vertices, int i1, int i2, int i3)
        {
            P1 = new float3(vertices[i1]);
            P2 = new float3(vertices[i2]);
            P3 = new float3(vertices[i3]);
        }
    }

    public static class ConvexDecomposition
    {
        private static void addTri(VertexPool vl, List<int> list, float3 p1, float3 p2, float3 p3)
        {
            int i1 = vl.getIndex(p1);
            int i2 = vl.getIndex(p2);
            int i3 = vl.getIndex(p3);

            // do *not* process degenerate triangles!
            if ( i1 != i2 && i1 != i3 && i2 != i3 )
            {
                list.Add(i1);
                list.Add(i2);
                list.Add(i3);
            }
        }

        public static void calcConvexDecomposition(List<float3> vertices, List<int> indices, ConvexDecompositionCallback callback, float masterVolume, int depth,
            int maxDepth, float concavePercent, float mergePercent)
        {
            float4 plane = new float4();
            bool split = false;

            if (depth < maxDepth)
            {
                float volume = 0f;
                float c = Concavity.computeConcavity(vertices, indices, ref plane, ref volume);

                if (depth == 0)
                {
                    masterVolume = volume;
                }

                float percent = (c * 100.0f) / masterVolume;

                if (percent > concavePercent) // if great than 5% of the total volume is concave, go ahead and keep splitting.
                {
                    split = true;
                }
            }

            if (depth >= maxDepth || !split)
            {
                HullResult result = new HullResult();
                HullDesc desc = new HullDesc();

                desc.SetHullFlag(HullFlag.QF_TRIANGLES);

                desc.Vertices = vertices;

                HullError ret = HullUtils.CreateConvexHull(desc, ref result);

                if (ret == HullError.QE_OK)
                {
                    ConvexResult r = new ConvexResult(result.OutputVertices, result.Indices);
                    callback(r);
                }

                return;
            }

            List<int> ifront = new List<int>();
            List<int> iback = new List<int>();

            VertexPool vfront = new VertexPool();
            VertexPool vback = new VertexPool();

            // ok..now we are going to 'split' all of the input triangles against this plane!
            for (int i = 0; i < indices.Count / 3; i++)
            {
                int i1 = indices[i * 3 + 0];
                int i2 = indices[i * 3 + 1];
                int i3 = indices[i * 3 + 2];

                FaceTri t = new FaceTri(vertices, i1, i2, i3);

                float3[] front = new float3[4];
                float3[] back = new float3[4];

                int fcount = 0;
                int bcount = 0;

                PlaneTriResult result = PlaneTri.planeTriIntersection(plane, t, 0.00001f, ref front, out fcount, ref back, out bcount);

                if (fcount > 4 || bcount > 4)
                {
                    result = PlaneTri.planeTriIntersection(plane, t, 0.00001f, ref front, out fcount, ref back, out bcount);
                }

                switch (result)
                {
                    case PlaneTriResult.PTR_FRONT:
                        Debug.Assert(fcount == 3);
                        addTri(vfront, ifront, front[0], front[1], front[2]);
                        break;
                    case PlaneTriResult.PTR_BACK:
                        Debug.Assert(bcount == 3);
                        addTri(vback, iback, back[0], back[1], back[2]);
                        break;
                    case PlaneTriResult.PTR_SPLIT:
                        Debug.Assert(fcount >= 3 && fcount <= 4);
                        Debug.Assert(bcount >= 3 && bcount <= 4);

                        addTri(vfront, ifront, front[0], front[1], front[2]);
                        addTri(vback, iback, back[0], back[1], back[2]);

                        if (fcount == 4)
                        {
                            addTri(vfront, ifront, front[0], front[2], front[3]);
                        }

                        if (bcount == 4)
                        {
                            addTri(vback, iback, back[0], back[2], back[3]);
                        }

                        break;
                }
            }

            // ok... here we recursively call
            if (ifront.Count > 0)
            {
                int vcount = vfront.GetSize();
                List<float3> vertices2 = vfront.GetVertices();
                for (int i = 0; i < vertices2.Count; i++)
                    vertices2[i] = new float3(vertices2[i]);
                int tcount = ifront.Count / 3;

                calcConvexDecomposition(vertices2, ifront, callback, masterVolume, depth + 1, maxDepth, concavePercent, mergePercent);
            }

            ifront.Clear();
            vfront.Clear();

            if (iback.Count > 0)
            {
                int vcount = vback.GetSize();
                List<float3> vertices2 = vback.GetVertices();
                int tcount = iback.Count / 3;

                calcConvexDecomposition(vertices2, iback, callback, masterVolume, depth + 1, maxDepth, concavePercent, mergePercent);
            }

            iback.Clear();
            vback.Clear();
        }
    }
}
