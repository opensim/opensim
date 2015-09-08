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
    public class Wpoint
    {
        public float3 mPoint;
        public float mWeight;

        public Wpoint(float3 p, float w)
        {
            mPoint = p;
            mWeight = w;
        }
    }

    public class CTri
    {
        private const int WSCALE = 4;

        public float3 mP1;
        public float3 mP2;
        public float3 mP3;
        public float3 mNear1;
        public float3 mNear2;
        public float3 mNear3;
        public float3 mNormal;
        public float mPlaneD;
        public float mConcavity;
        public float mC1;
        public float mC2;
        public float mC3;
        public int mI1;
        public int mI2;
        public int mI3;
        public int mProcessed; // already been added...

        public CTri(float3 p1, float3 p2, float3 p3, int i1, int i2, int i3)
        {
            mProcessed = 0;
            mI1 = i1;
            mI2 = i2;
            mI3 = i3;

            mP1 = new float3(p1);
            mP2 = new float3(p2);
            mP3 = new float3(p3);

            mNear1 = new float3();
            mNear2 = new float3();
            mNear3 = new float3();

            mNormal = new float3();
            mPlaneD = mNormal.ComputePlane(mP1, mP2, mP3);
        }

        public float Facing(CTri t)
        {
            return float3.dot(mNormal, t.mNormal);
        }

        public bool clip(float3 start, ref float3 end)
        {
            float3 sect = new float3();
            bool hit = lineIntersectsTriangle(start, end, mP1, mP2, mP3, ref sect);

            if (hit)
                end = sect;
            return hit;
        }

        public bool Concave(float3 p, ref float distance, ref float3 n)
        {
            n.NearestPointInTriangle(p, mP1, mP2, mP3);
            distance = p.Distance(n);
            return true;
        }

        public void addTri(int[] indices, int i1, int i2, int i3, ref int tcount)
        {
            indices[tcount * 3 + 0] = i1;
            indices[tcount * 3 + 1] = i2;
            indices[tcount * 3 + 2] = i3;
            tcount++;
        }

        public float getVolume()
        {
            int[] indices = new int[8 * 3];

            int tcount = 0;

            addTri(indices, 0, 1, 2, ref tcount);
            addTri(indices, 3, 4, 5, ref tcount);

            addTri(indices, 0, 3, 4, ref tcount);
            addTri(indices, 0, 4, 1, ref tcount);

            addTri(indices, 1, 4, 5, ref tcount);
            addTri(indices, 1, 5, 2, ref tcount);

            addTri(indices, 0, 3, 5, ref tcount);
            addTri(indices, 0, 5, 2, ref tcount);

            List<float3> vertices = new List<float3> { mP1, mP2, mP3, mNear1, mNear2, mNear3 };
            List<int> indexList = new List<int>(indices);

            float v = Concavity.computeMeshVolume(vertices, indexList);
            return v;
        }

        public float raySect(float3 p, float3 dir, ref float3 sect)
        {
            float4 plane = new float4();

            plane.x = mNormal.x;
            plane.y = mNormal.y;
            plane.z = mNormal.z;
            plane.w = mPlaneD;

            float3 dest = p + dir * 100000f;

            intersect(p, dest, ref sect, plane);

            return sect.Distance(p); // return the intersection distance
        }

        public float planeDistance(float3 p)
        {
            float4 plane = new float4();

            plane.x = mNormal.x;
            plane.y = mNormal.y;
            plane.z = mNormal.z;
            plane.w = mPlaneD;

            return DistToPt(p, plane);
        }

        public bool samePlane(CTri t)
        {
            const float THRESH = 0.001f;
            float dd = Math.Abs(t.mPlaneD - mPlaneD);
            if (dd > THRESH)
                return false;
            dd = Math.Abs(t.mNormal.x - mNormal.x);
            if (dd > THRESH)
                return false;
            dd = Math.Abs(t.mNormal.y - mNormal.y);
            if (dd > THRESH)
                return false;
            dd = Math.Abs(t.mNormal.z - mNormal.z);
            if (dd > THRESH)
                return false;
            return true;
        }

        public bool hasIndex(int i)
        {
            if (i == mI1 || i == mI2 || i == mI3)
                return true;
            return false;
        }

        public bool sharesEdge(CTri t)
        {
            bool ret = false;
            uint count = 0;

            if (t.hasIndex(mI1))
                count++;
            if (t.hasIndex(mI2))
                count++;
            if (t.hasIndex(mI3))
                count++;

            if (count >= 2)
                ret = true;

            return ret;
        }

        public float area()
        {
            float a = mConcavity * mP1.Area(mP2, mP3);
            return a;
        }

        public void addWeighted(List<Wpoint> list)
        {
            Wpoint p1 = new Wpoint(mP1, mC1);
            Wpoint p2 = new Wpoint(mP2, mC2);
            Wpoint p3 = new Wpoint(mP3, mC3);

            float3 d1 = mNear1 - mP1;
            float3 d2 = mNear2 - mP2;
            float3 d3 = mNear3 - mP3;

            d1 *= WSCALE;
            d2 *= WSCALE;
            d3 *= WSCALE;

            d1 = d1 + mP1;
            d2 = d2 + mP2;
            d3 = d3 + mP3;

            Wpoint p4 = new Wpoint(d1, mC1);
            Wpoint p5 = new Wpoint(d2, mC2);
            Wpoint p6 = new Wpoint(d3, mC3);

            list.Add(p1);
            list.Add(p2);
            list.Add(p3);

            list.Add(p4);
            list.Add(p5);
            list.Add(p6);
        }

        private static float DistToPt(float3 p, float4 plane)
	    {
		    float x = p.x;
		    float y = p.y;
		    float z = p.z;
		    float d = x*plane.x + y*plane.y + z*plane.z + plane.w;
		    return d;
	    }

        private static void intersect(float3 p1, float3 p2, ref float3 split, float4 plane)
        {
            float dp1 = DistToPt(p1, plane);

            float3 dir = new float3();
            dir.x = p2[0] - p1[0];
            dir.y = p2[1] - p1[1];
            dir.z = p2[2] - p1[2];

            float dot1 = dir[0] * plane[0] + dir[1] * plane[1] + dir[2] * plane[2];
            float dot2 = dp1 - plane[3];

            float t = -(plane[3] + dot2) / dot1;

            split.x = (dir[0] * t) + p1[0];
            split.y = (dir[1] * t) + p1[1];
            split.z = (dir[2] * t) + p1[2];
        }

        private static bool rayIntersectsTriangle(float3 p, float3 d, float3 v0, float3 v1, float3 v2, out float t)
        {
            t = 0f;

            float3 e1, e2, h, s, q;
            float a, f, u, v;

            e1 = v1 - v0;
            e2 = v2 - v0;
            h = float3.cross(d, e2);
            a = float3.dot(e1, h);

            if (a > -0.00001f && a < 0.00001f)
                return false;

            f = 1f / a;
            s = p - v0;
            u = f * float3.dot(s, h);

            if (u < 0.0f || u > 1.0f)
                return false;

            q = float3.cross(s, e1);
            v = f * float3.dot(d, q);
            if (v < 0.0f || u + v > 1.0f)
                return false;

            // at this stage we can compute t to find out where
            // the intersection point is on the line
            t = f * float3.dot(e2, q);
            if (t > 0f) // ray intersection
                return true;
            else // this means that there is a line intersection but not a ray intersection
                return false;
        }

        private static bool lineIntersectsTriangle(float3 rayStart, float3 rayEnd, float3 p1, float3 p2, float3 p3, ref float3 sect)
        {
            float3 dir = rayEnd - rayStart;

            float d = (float)Math.Sqrt(dir[0] * dir[0] + dir[1] * dir[1] + dir[2] * dir[2]);
            float r = 1.0f / d;

            dir *= r;

            float t;
            bool ret = rayIntersectsTriangle(rayStart, dir, p1, p2, p3, out t);

            if (ret)
            {
                if (t > d)
                {
                    sect.x = rayStart.x + dir.x * t;
                    sect.y = rayStart.y + dir.y * t;
                    sect.z = rayStart.z + dir.z * t;
                }
                else
                {
                    ret = false;
                }
            }

            return ret;
        }
    }
}
