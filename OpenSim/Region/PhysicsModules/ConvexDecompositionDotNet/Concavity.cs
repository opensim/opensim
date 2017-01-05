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
using System.Text;

namespace OpenSim.Region.PhysicsModules.ConvexDecompositionDotNet
{
    public static class Concavity
    {
        // compute's how 'concave' this object is and returns the total volume of the
        // convex hull as well as the volume of the 'concavity' which was found.
        public static float computeConcavity(List<float3> vertices, List<int> indices, ref float4 plane, ref float volume)
        {
            float cret = 0f;
            volume = 1f;

            HullResult result = new HullResult();
            HullDesc desc = new HullDesc();

            desc.MaxFaces = 256;
            desc.MaxVertices = 256;
            desc.SetHullFlag(HullFlag.QF_TRIANGLES);
            desc.Vertices = vertices;

            HullError ret = HullUtils.CreateConvexHull(desc, ref result);

            if (ret == HullError.QE_OK)
            {
                volume = computeMeshVolume2(result.OutputVertices, result.Indices);

                // ok..now..for each triangle on the original mesh..
                // we extrude the points to the nearest point on the hull.
                List<CTri> tris = new List<CTri>();

                for (int i = 0; i < result.Indices.Count / 3; i++)
                {
                    int i1 = result.Indices[i * 3 + 0];
                    int i2 = result.Indices[i * 3 + 1];
                    int i3 = result.Indices[i * 3 + 2];

                    float3 p1 = result.OutputVertices[i1];
                    float3 p2 = result.OutputVertices[i2];
                    float3 p3 = result.OutputVertices[i3];

                    CTri t = new CTri(p1, p2, p3, i1, i2, i3);
                    tris.Add(t);
                }

                // we have not pre-computed the plane equation for each triangle in the convex hull..
                float totalVolume = 0;

                List<CTri> ftris = new List<CTri>(); // 'feature' triangles.
                List<CTri> input_mesh = new List<CTri>();

                for (int i = 0; i < indices.Count / 3; i++)
                {
                    int i1 = indices[i * 3 + 0];
                    int i2 = indices[i * 3 + 1];
                    int i3 = indices[i * 3 + 2];

                    float3 p1 = vertices[i1];
                    float3 p2 = vertices[i2];
                    float3 p3 = vertices[i3];

                    CTri t = new CTri(p1, p2, p3, i1, i2, i3);
                    input_mesh.Add(t);
                }

                for (int i = 0; i < indices.Count / 3; i++)
                {
                    int i1 = indices[i * 3 + 0];
                    int i2 = indices[i * 3 + 1];
                    int i3 = indices[i * 3 + 2];

                    float3 p1 = vertices[i1];
                    float3 p2 = vertices[i2];
                    float3 p3 = vertices[i3];

                    CTri t = new CTri(p1, p2, p3, i1, i2, i3);

                    featureMatch(t, tris, input_mesh);

                    if (t.mConcavity > 0.05f)
                    {
                        float v = t.getVolume();
                        totalVolume += v;
                        ftris.Add(t);
                    }
                }

                SplitPlane.computeSplitPlane(vertices, indices, ref plane);
                cret = totalVolume;
            }

            return cret;
        }

        public static bool featureMatch(CTri m, List<CTri> tris, List<CTri> input_mesh)
        {
            bool ret = false;
            float neardot = 0.707f;
            m.mConcavity = 0;

            for (int i = 0; i < tris.Count; i++)
            {
                CTri t = tris[i];

                if (t.samePlane(m))
                {
                    ret = false;
                    break;
                }

                float dot = float3.dot(t.mNormal, m.mNormal);

                if (dot > neardot)
                {
                    float d1 = t.planeDistance(m.mP1);
                    float d2 = t.planeDistance(m.mP2);
                    float d3 = t.planeDistance(m.mP3);

                    if (d1 > 0.001f || d2 > 0.001f || d3 > 0.001f) // can't be near coplaner!
                    {
                        neardot = dot;

                        t.raySect(m.mP1, m.mNormal, ref m.mNear1);
                        t.raySect(m.mP2, m.mNormal, ref m.mNear2);
                        t.raySect(m.mP3, m.mNormal, ref m.mNear3);

                        ret = true;
                    }
                }
            }

            if (ret)
            {
                m.mC1 = m.mP1.Distance(m.mNear1);
                m.mC2 = m.mP2.Distance(m.mNear2);
                m.mC3 = m.mP3.Distance(m.mNear3);

                m.mConcavity = m.mC1;

                if (m.mC2 > m.mConcavity)
                    m.mConcavity = m.mC2;
                if (m.mC3 > m.mConcavity)
                    m.mConcavity = m.mC3;
            }

            return ret;
        }

        private static float det(float3 p1, float3 p2, float3 p3)
        {
            return p1.x * p2.y * p3.z + p2.x * p3.y * p1.z + p3.x * p1.y * p2.z - p1.x * p3.y * p2.z - p2.x * p1.y * p3.z - p3.x * p2.y * p1.z;
        }

        public static float computeMeshVolume(List<float3> vertices, List<int> indices)
        {
            float volume = 0f;

            for (int i = 0; i < indices.Count / 3; i++)
            {
                float3 p1 = vertices[indices[i * 3 + 0]];
                float3 p2 = vertices[indices[i * 3 + 1]];
                float3 p3 = vertices[indices[i * 3 + 2]];

                volume += det(p1, p2, p3); // compute the volume of the tetrahedran relative to the origin.
            }

            volume *= (1.0f / 6.0f);
            if (volume < 0f)
                return -volume;
            return volume;
        }

        public static float computeMeshVolume2(List<float3> vertices, List<int> indices)
        {
            float volume = 0f;

            float3 p0 = vertices[0];
            for (int i = 0; i < indices.Count / 3; i++)
            {
                float3 p1 = vertices[indices[i * 3 + 0]];
                float3 p2 = vertices[indices[i * 3 + 1]];
                float3 p3 = vertices[indices[i * 3 + 2]];

                volume += tetVolume(p0, p1, p2, p3); // compute the volume of the tetrahedron relative to the root vertice
            }

            return volume * (1.0f / 6.0f);
        }

        private static float tetVolume(float3 p0, float3 p1, float3 p2, float3 p3)
        {
            float3 a = p1 - p0;
            float3 b = p2 - p0;
            float3 c = p3 - p0;

            float3 cross = float3.cross(b, c);
            float volume = float3.dot(a, cross);

            if (volume < 0f)
                return -volume;
            return volume;
        }
    }
}
