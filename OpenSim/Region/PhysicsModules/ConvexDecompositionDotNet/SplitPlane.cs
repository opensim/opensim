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

namespace OpenSim.Region.PhysicsModules.ConvexDecompositionDotNet
{
    public class Rect3d
    {
        public float[] mMin = new float[3];
        public float[] mMax = new float[3];

        public Rect3d()
        {
        }

        public Rect3d(float[] bmin, float[] bmax)
        {
            mMin[0] = bmin[0];
            mMin[1] = bmin[1];
            mMin[2] = bmin[2];

            mMax[0] = bmax[0];
            mMax[1] = bmax[1];
            mMax[2] = bmax[2];
        }

        public void SetMin(float[] bmin)
        {
            mMin[0] = bmin[0];
            mMin[1] = bmin[1];
            mMin[2] = bmin[2];
        }

        public void SetMax(float[] bmax)
        {
            mMax[0] = bmax[0];
            mMax[1] = bmax[1];
            mMax[2] = bmax[2];
        }

        public void SetMin(float x, float y, float z)
        {
            mMin[0] = x;
            mMin[1] = y;
            mMin[2] = z;
        }

        public void SetMax(float x, float y, float z)
        {
            mMax[0] = x;
            mMax[1] = y;
            mMax[2] = z;
        }
    }

    public static class SplitPlane
    {
        public static bool computeSplitPlane(List<float3> vertices, List<int> indices, ref float4 plane)
        {
            float[] bmin = { Single.MaxValue, Single.MaxValue, Single.MaxValue };
            float[] bmax = { Single.MinValue, Single.MinValue, Single.MinValue };

            for (int i = 0; i < vertices.Count; i++)
            {
                float3 p = vertices[i];

                if (p[0] < bmin[0])
                    bmin[0] = p[0];
                if (p[1] < bmin[1])
                    bmin[1] = p[1];
                if (p[2] < bmin[2])
                    bmin[2] = p[2];

                if (p[0] > bmax[0])
                    bmax[0] = p[0];
                if (p[1] > bmax[1])
                    bmax[1] = p[1];
                if (p[2] > bmax[2])
                    bmax[2] = p[2];
            }

            float dx = bmax[0] - bmin[0];
            float dy = bmax[1] - bmin[1];
            float dz = bmax[2] - bmin[2];

            float laxis = dx;

            int axis = 0;

            if (dy > dx)
            {
                axis = 1;
                laxis = dy;
            }

            if (dz > dx && dz > dy)
            {
                axis = 2;
                laxis = dz;
            }

            float[] p1 = new float[3];
            float[] p2 = new float[3];
            float[] p3 = new float[3];

            p3[0] = p2[0] = p1[0] = bmin[0] + dx * 0.5f;
            p3[1] = p2[1] = p1[1] = bmin[1] + dy * 0.5f;
            p3[2] = p2[2] = p1[2] = bmin[2] + dz * 0.5f;

            Rect3d b = new Rect3d(bmin, bmax);

            Rect3d b1 = new Rect3d();
            Rect3d b2 = new Rect3d();

            splitRect(axis, b, b1, b2, p1);

            switch (axis)
            {
                case 0:
                    p2[1] = bmin[1];
                    p2[2] = bmin[2];

                    if (dz > dy)
                    {
                        p3[1] = bmax[1];
                        p3[2] = bmin[2];
                    }
                    else
                    {
                        p3[1] = bmin[1];
                        p3[2] = bmax[2];
                    }

                    break;
                case 1:
                    p2[0] = bmin[0];
                    p2[2] = bmin[2];

                    if (dx > dz)
                    {
                        p3[0] = bmax[0];
                        p3[2] = bmin[2];
                    }
                    else
                    {
                        p3[0] = bmin[0];
                        p3[2] = bmax[2];
                    }

                    break;
                case 2:
                    p2[0] = bmin[0];
                    p2[1] = bmin[1];

                    if (dx > dy)
                    {
                        p3[0] = bmax[0];
                        p3[1] = bmin[1];
                    }
                    else
                    {
                        p3[0] = bmin[0];
                        p3[1] = bmax[1];
                    }

                    break;
            }

            computePlane(p1, p2, p3, plane);

            return true;
        }

        internal static void computePlane(float[] A, float[] B, float[] C, float4 plane)
        {
            float vx = (B[0] - C[0]);
            float vy = (B[1] - C[1]);
            float vz = (B[2] - C[2]);

            float wx = (A[0] - B[0]);
            float wy = (A[1] - B[1]);
            float wz = (A[2] - B[2]);

            float vw_x = vy * wz - vz * wy;
            float vw_y = vz * wx - vx * wz;
            float vw_z = vx * wy - vy * wx;

            float mag = (float)Math.Sqrt((vw_x * vw_x) + (vw_y * vw_y) + (vw_z * vw_z));

            if (mag < 0.000001f)
            {
                mag = 0;
            }
            else
            {
                mag = 1.0f / mag;
            }

            float x = vw_x * mag;
            float y = vw_y * mag;
            float z = vw_z * mag;

            float D = 0.0f - ((x * A[0]) + (y * A[1]) + (z * A[2]));

            plane.x = x;
            plane.y = y;
            plane.z = z;
            plane.w = D;
        }

        public static void splitRect(int axis, Rect3d source, Rect3d b1, Rect3d b2, float[] midpoint)
        {
            switch (axis)
            {
                case 0:
                    b1.SetMin(source.mMin);
                    b1.SetMax(midpoint[0], source.mMax[1], source.mMax[2]);

                    b2.SetMin(midpoint[0], source.mMin[1], source.mMin[2]);
                    b2.SetMax(source.mMax);
                    break;
                case 1:
                    b1.SetMin(source.mMin);
                    b1.SetMax(source.mMax[0], midpoint[1], source.mMax[2]);

                    b2.SetMin(source.mMin[0], midpoint[1], source.mMin[2]);
                    b2.SetMax(source.mMax);
                    break;
                case 2:
                    b1.SetMin(source.mMin);
                    b1.SetMax(source.mMax[0], source.mMax[1], midpoint[2]);

                    b2.SetMin(source.mMin[0], source.mMin[1], midpoint[2]);
                    b2.SetMax(source.mMax);
                    break;
            }
        }
    }
}
