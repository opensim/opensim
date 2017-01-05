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
    public static class HullUtils
    {
        public static int argmin(float[] a, int n)
        {
            int r = 0;
            for (int i = 1; i < n; i++)
            {
                if (a[i] < a[r])
                {
                    r = i;
                }
            }
            return r;
        }

        public static float clampf(float a)
        {
            return Math.Min(1.0f, Math.Max(0.0f, a));
        }

        public static float Round(float a, float precision)
        {
            return (float)Math.Floor(0.5f + a / precision) * precision;
        }

        public static float Interpolate(float f0, float f1, float alpha)
        {
            return f0 * (1 - alpha) + f1 * alpha;
        }

        public static void Swap<T>(ref T a, ref T b)
        {
            T tmp = a;
            a = b;
            b = tmp;
        }

        public static bool above(List<float3> vertices, int3 t, float3 p, float epsilon)
        {
            float3 vtx = vertices[t.x];
            float3 n = TriNormal(vtx, vertices[t.y], vertices[t.z]);
            return (float3.dot(n, p - vtx) > epsilon); // EPSILON???
        }

        public static int hasedge(int3 t, int a, int b)
        {
            for (int i = 0; i < 3; i++)
            {
                int i1 = (i + 1) % 3;
                if (t[i] == a && t[i1] == b)
                    return 1;
            }
            return 0;
        }

        public static bool hasvert(int3 t, int v)
        {
            return (t[0] == v || t[1] == v || t[2] == v);
        }

        public static int shareedge(int3 a, int3 b)
        {
            int i;
            for (i = 0; i < 3; i++)
            {
                int i1 = (i + 1) % 3;
                if (hasedge(a, b[i1], b[i]) != 0)
                    return 1;
            }
            return 0;
        }

        public static void b2bfix(HullTriangle s, HullTriangle t, List<HullTriangle> tris)
        {
            int i;
            for (i = 0; i < 3; i++)
            {
                int i1 = (i + 1) % 3;
                int i2 = (i + 2) % 3;
                int a = (s)[i1];
                int b = (s)[i2];
                Debug.Assert(tris[s.neib(a, b)].neib(b, a) == s.id);
                Debug.Assert(tris[t.neib(a, b)].neib(b, a) == t.id);
                tris[s.neib(a, b)].setneib(b, a, t.neib(b, a));
                tris[t.neib(b, a)].setneib(a, b, s.neib(a, b));
            }
        }

        public static void removeb2b(HullTriangle s, HullTriangle t, List<HullTriangle> tris)
        {
            b2bfix(s, t, tris);
            s.Dispose();
            t.Dispose();
        }

        public static void checkit(HullTriangle t, List<HullTriangle> tris)
        {
            int i;
            Debug.Assert(tris[t.id] == t);
            for (i = 0; i < 3; i++)
            {
                int i1 = (i + 1) % 3;
                int i2 = (i + 2) % 3;
                int a = (t)[i1];
                int b = (t)[i2];
                Debug.Assert(a != b);
                Debug.Assert(tris[t.n[i]].neib(b, a) == t.id);
            }
        }

        public static void extrude(HullTriangle t0, int v, List<HullTriangle> tris)
        {
            int3 t = t0;
            int n = tris.Count;
            HullTriangle ta = new HullTriangle(v, t[1], t[2], tris);
            ta.n = new int3(t0.n[0], n + 1, n + 2);
            tris[t0.n[0]].setneib(t[1], t[2], n + 0);
            HullTriangle tb = new HullTriangle(v, t[2], t[0], tris);
            tb.n = new int3(t0.n[1], n + 2, n + 0);
            tris[t0.n[1]].setneib(t[2], t[0], n + 1);
            HullTriangle tc = new HullTriangle(v, t[0], t[1], tris);
            tc.n = new int3(t0.n[2], n + 0, n + 1);
            tris[t0.n[2]].setneib(t[0], t[1], n + 2);
            checkit(ta, tris);
            checkit(tb, tris);
            checkit(tc, tris);
            if (hasvert(tris[ta.n[0]], v))
                removeb2b(ta, tris[ta.n[0]], tris);
            if (hasvert(tris[tb.n[0]], v))
                removeb2b(tb, tris[tb.n[0]], tris);
            if (hasvert(tris[tc.n[0]], v))
                removeb2b(tc, tris[tc.n[0]], tris);
            t0.Dispose();
        }

        public static HullTriangle extrudable(float epsilon, List<HullTriangle> tris)
        {
            int i;
            HullTriangle t = null;
            for (i = 0; i < tris.Count; i++)
            {
                if (t == null || (tris.Count > i && (object)tris[i] != null && t.rise < tris[i].rise))
                {
                    t = tris[i];
                }
            }
            return (t.rise > epsilon) ? t : null;
        }

        public static Quaternion RotationArc(float3 v0, float3 v1)
        {
            Quaternion q = new Quaternion();
            v0 = float3.normalize(v0); // Comment these two lines out if you know its not needed.
            v1 = float3.normalize(v1); // If vector is already unit length then why do it again?
            float3 c = float3.cross(v0, v1);
            float d = float3.dot(v0, v1);
            if (d <= -1.0f) // 180 about x axis
            {
                return new Quaternion(1f, 0f, 0f, 0f);
            }
            float s = (float)Math.Sqrt((1 + d) * 2f);
            q.x = c.x / s;
            q.y = c.y / s;
            q.z = c.z / s;
            q.w = s / 2.0f;
            return q;
        }

        public static float3 PlaneLineIntersection(Plane plane, float3 p0, float3 p1)
        {
            // returns the point where the line p0-p1 intersects the plane n&d
            float3 dif = p1 - p0;
            float dn = float3.dot(plane.normal, dif);
            float t = -(plane.dist + float3.dot(plane.normal, p0)) / dn;
            return p0 + (dif * t);
        }

        public static float3 LineProject(float3 p0, float3 p1, float3 a)
        {
            float3 w = new float3();
            w = p1 - p0;
            float t = float3.dot(w, (a - p0)) / (w.x * w.x + w.y * w.y + w.z * w.z);
            return p0 + w * t;
        }

        public static float3 PlaneProject(Plane plane, float3 point)
        {
            return point - plane.normal * (float3.dot(point, plane.normal) + plane.dist);
        }

        public static float LineProjectTime(float3 p0, float3 p1, float3 a)
        {
            float3 w = new float3();
            w = p1 - p0;
            float t = float3.dot(w, (a - p0)) / (w.x * w.x + w.y * w.y + w.z * w.z);
            return t;
        }

        public static float3 ThreePlaneIntersection(Plane p0, Plane p1, Plane p2)
        {
            float3x3 mp = float3x3.Transpose(new float3x3(p0.normal, p1.normal, p2.normal));
            float3x3 mi = float3x3.Inverse(mp);
            float3 b = new float3(p0.dist, p1.dist, p2.dist);
            return -b * mi;
        }

        public static bool PolyHit(List<float3> vert, float3 v0, float3 v1)
        {
            float3 impact = new float3();
            float3 normal = new float3();
            return PolyHit(vert, v0, v1, out impact, out normal);
        }

        public static bool PolyHit(List<float3> vert, float3 v0, float3 v1, out float3 impact)
        {
            float3 normal = new float3();
            return PolyHit(vert, v0, v1, out impact, out normal);
        }

        public static bool PolyHit(List<float3> vert, float3 v0, float3 v1, out float3 impact, out float3 normal)
        {
            float3 the_point = new float3();

            impact = null;
            normal = null;

            int i;
            float3 nrml = new float3(0, 0, 0);
            for (i = 0; i < vert.Count; i++)
            {
                int i1 = (i + 1) % vert.Count;
                int i2 = (i + 2) % vert.Count;
                nrml = nrml + float3.cross(vert[i1] - vert[i], vert[i2] - vert[i1]);
            }

            float m = float3.magnitude(nrml);
            if (m == 0.0)
            {
                return false;
            }
            nrml = nrml * (1.0f / m);
            float dist = -float3.dot(nrml, vert[0]);
            float d0;
            float d1;
            if ((d0 = float3.dot(v0, nrml) + dist) < 0 || (d1 = float3.dot(v1, nrml) + dist) > 0)
            {
                return false;
            }

            // By using the cached plane distances d0 and d1
            // we can optimize the following:
            //     the_point = planelineintersection(nrml,dist,v0,v1);
            float a = d0 / (d0 - d1);
            the_point = v0 * (1 - a) + v1 * a;


            bool inside = true;
            for (int j = 0; inside && j < vert.Count; j++)
            {
                // let inside = 0 if outside
                float3 pp1 = new float3();
                float3 pp2 = new float3();
                float3 side = new float3();
                pp1 = vert[j];
                pp2 = vert[(j + 1) % vert.Count];
                side = float3.cross((pp2 - pp1), (the_point - pp1));
                inside = (float3.dot(nrml, side) >= 0.0);
            }
            if (inside)
            {
                if (normal != null)
                {
                    normal = nrml;
                }
                if (impact != null)
                {
                    impact = the_point;
                }
            }
            return inside;
        }

        public static bool BoxInside(float3 p, float3 bmin, float3 bmax)
        {
            return (p.x >= bmin.x && p.x <= bmax.x && p.y >= bmin.y && p.y <= bmax.y && p.z >= bmin.z && p.z <= bmax.z);
        }

        public static bool BoxIntersect(float3 v0, float3 v1, float3 bmin, float3 bmax, float3 impact)
        {
            if (BoxInside(v0, bmin, bmax))
            {
                impact = v0;
                return true;
            }
            if (v0.x <= bmin.x && v1.x >= bmin.x)
            {
                float a = (bmin.x - v0.x) / (v1.x - v0.x);
                //v.x = bmin.x;
                float vy = (1 - a) * v0.y + a * v1.y;
                float vz = (1 - a) * v0.z + a * v1.z;
                if (vy >= bmin.y && vy <= bmax.y && vz >= bmin.z && vz <= bmax.z)
                {
                    impact.x = bmin.x;
                    impact.y = vy;
                    impact.z = vz;
                    return true;
                }
            }
            else if (v0.x >= bmax.x && v1.x <= bmax.x)
            {
                float a = (bmax.x - v0.x) / (v1.x - v0.x);
                //v.x = bmax.x;
                float vy = (1 - a) * v0.y + a * v1.y;
                float vz = (1 - a) * v0.z + a * v1.z;
                if (vy >= bmin.y && vy <= bmax.y && vz >= bmin.z && vz <= bmax.z)
                {
                    impact.x = bmax.x;
                    impact.y = vy;
                    impact.z = vz;
                    return true;
                }
            }
            if (v0.y <= bmin.y && v1.y >= bmin.y)
            {
                float a = (bmin.y - v0.y) / (v1.y - v0.y);
                float vx = (1 - a) * v0.x + a * v1.x;
                //v.y = bmin.y;
                float vz = (1 - a) * v0.z + a * v1.z;
                if (vx >= bmin.x && vx <= bmax.x && vz >= bmin.z && vz <= bmax.z)
                {
                    impact.x = vx;
                    impact.y = bmin.y;
                    impact.z = vz;
                    return true;
                }
            }
            else if (v0.y >= bmax.y && v1.y <= bmax.y)
            {
                float a = (bmax.y - v0.y) / (v1.y - v0.y);
                float vx = (1 - a) * v0.x + a * v1.x;
                // vy = bmax.y;
                float vz = (1 - a) * v0.z + a * v1.z;
                if (vx >= bmin.x && vx <= bmax.x && vz >= bmin.z && vz <= bmax.z)
                {
                    impact.x = vx;
                    impact.y = bmax.y;
                    impact.z = vz;
                    return true;
                }
            }
            if (v0.z <= bmin.z && v1.z >= bmin.z)
            {
                float a = (bmin.z - v0.z) / (v1.z - v0.z);
                float vx = (1 - a) * v0.x + a * v1.x;
                float vy = (1 - a) * v0.y + a * v1.y;
                // v.z = bmin.z;
                if (vy >= bmin.y && vy <= bmax.y && vx >= bmin.x && vx <= bmax.x)
                {
                    impact.x = vx;
                    impact.y = vy;
                    impact.z = bmin.z;
                    return true;
                }
            }
            else if (v0.z >= bmax.z && v1.z <= bmax.z)
            {
                float a = (bmax.z - v0.z) / (v1.z - v0.z);
                float vx = (1 - a) * v0.x + a * v1.x;
                float vy = (1 - a) * v0.y + a * v1.y;
                // v.z = bmax.z;
                if (vy >= bmin.y && vy <= bmax.y && vx >= bmin.x && vx <= bmax.x)
                {
                    impact.x = vx;
                    impact.y = vy;
                    impact.z = bmax.z;
                    return true;
                }
            }
            return false;
        }

        public static float DistanceBetweenLines(float3 ustart, float3 udir, float3 vstart, float3 vdir, float3 upoint)
        {
            return DistanceBetweenLines(ustart, udir, vstart, vdir, upoint, null);
        }

        public static float DistanceBetweenLines(float3 ustart, float3 udir, float3 vstart, float3 vdir)
        {
            return DistanceBetweenLines(ustart, udir, vstart, vdir, null, null);
        }

        public static float DistanceBetweenLines(float3 ustart, float3 udir, float3 vstart, float3 vdir, float3 upoint, float3 vpoint)
        {
            float3 cp = float3.normalize(float3.cross(udir, vdir));

            float distu = -float3.dot(cp, ustart);
            float distv = -float3.dot(cp, vstart);
            float dist = (float)Math.Abs(distu - distv);
            if (upoint != null)
            {
                Plane plane = new Plane();
                plane.normal = float3.normalize(float3.cross(vdir, cp));
                plane.dist = -float3.dot(plane.normal, vstart);
                upoint = PlaneLineIntersection(plane, ustart, ustart + udir);
            }
            if (vpoint != null)
            {
                Plane plane = new Plane();
                plane.normal = float3.normalize(float3.cross(udir, cp));
                plane.dist = -float3.dot(plane.normal, ustart);
                vpoint = PlaneLineIntersection(plane, vstart, vstart + vdir);
            }
            return dist;
        }

        public static float3 TriNormal(float3 v0, float3 v1, float3 v2)
        {
            // return the normal of the triangle
            // inscribed by v0, v1, and v2
            float3 cp = float3.cross(v1 - v0, v2 - v1);
            float m = float3.magnitude(cp);
            if (m == 0)
                return new float3(1, 0, 0);
            return cp * (1.0f / m);
        }

        public static int PlaneTest(Plane p, float3 v, float planetestepsilon)
        {
            float a = float3.dot(v, p.normal) + p.dist;
            int flag = (a > planetestepsilon) ? (2) : ((a < -planetestepsilon) ? (1) : (0));
            return flag;
        }

        public static int SplitTest(ref ConvexH convex, Plane plane, float planetestepsilon)
        {
            int flag = 0;
            for (int i = 0; i < convex.vertices.Count; i++)
            {
                flag |= PlaneTest(plane, convex.vertices[i], planetestepsilon);
            }
            return flag;
        }

        public static Quaternion VirtualTrackBall(float3 cop, float3 cor, float3 dir1, float3 dir2)
        {
            // routine taken from game programming gems.
            // Implement track ball functionality to spin stuf on the screen
            //  cop   center of projection
            //  cor   center of rotation
            //  dir1  old mouse direction
            //  dir2  new mouse direction
            // pretend there is a sphere around cor.  Then find the points
            // where dir1 and dir2 intersect that sphere.  Find the
            // rotation that takes the first point to the second.
            float m;
            // compute plane
            float3 nrml = cor - cop;
            float fudgefactor = 1.0f / (float3.magnitude(nrml) * 0.25f); // since trackball proportional to distance from cop
            nrml = float3.normalize(nrml);
            float dist = -float3.dot(nrml, cor);
            float3 u = PlaneLineIntersection(new Plane(nrml, dist), cop, cop + dir1);
            u = u - cor;
            u = u * fudgefactor;
            m = float3.magnitude(u);
            if (m > 1)
            {
                u /= m;
            }
            else
            {
                u = u - (nrml * (float)Math.Sqrt(1 - m * m));
            }
            float3 v = PlaneLineIntersection(new Plane(nrml, dist), cop, cop + dir2);
            v = v - cor;
            v = v * fudgefactor;
            m = float3.magnitude(v);
            if (m > 1)
            {
                v /= m;
            }
            else
            {
                v = v - (nrml * (float)Math.Sqrt(1 - m * m));
            }
            return RotationArc(u, v);
        }

        public static bool AssertIntact(ConvexH convex, float planetestepsilon)
        {
            int i;
            int estart = 0;
            for (i = 0; i < convex.edges.Count; i++)
            {
                if (convex.edges[estart].p != convex.edges[i].p)
                {
                    estart = i;
                }
                int inext = i + 1;
                if (inext >= convex.edges.Count || convex.edges[inext].p != convex.edges[i].p)
                {
                    inext = estart;
                }
                Debug.Assert(convex.edges[inext].p == convex.edges[i].p);
                int nb = convex.edges[i].ea;
                Debug.Assert(nb != 255);
                if (nb == 255 || nb == -1)
                    return false;
                Debug.Assert(nb != -1);
                Debug.Assert(i == convex.edges[nb].ea);
            }
            for (i = 0; i < convex.edges.Count; i++)
            {
                Debug.Assert((0) == PlaneTest(convex.facets[convex.edges[i].p], convex.vertices[convex.edges[i].v], planetestepsilon));
                if ((0) != PlaneTest(convex.facets[convex.edges[i].p], convex.vertices[convex.edges[i].v], planetestepsilon))
                    return false;
                if (convex.edges[estart].p != convex.edges[i].p)
                {
                    estart = i;
                }
                int i1 = i + 1;
                if (i1 >= convex.edges.Count || convex.edges[i1].p != convex.edges[i].p)
                {
                    i1 = estart;
                }
                int i2 = i1 + 1;
                if (i2 >= convex.edges.Count || convex.edges[i2].p != convex.edges[i].p)
                {
                    i2 = estart;
                }
                if (i == i2) // i sliced tangent to an edge and created 2 meaningless edges
                    continue;
                float3 localnormal = TriNormal(convex.vertices[convex.edges[i].v], convex.vertices[convex.edges[i1].v], convex.vertices[convex.edges[i2].v]);
                Debug.Assert(float3.dot(localnormal, convex.facets[convex.edges[i].p].normal) > 0);
                if (float3.dot(localnormal, convex.facets[convex.edges[i].p].normal) <= 0)
                    return false;
            }
            return true;
        }

        public static ConvexH test_btbq(float planetestepsilon)
        {
            // back to back quads
            ConvexH convex = new ConvexH(4, 8, 2);
            convex.vertices[0] = new float3(0, 0, 0);
            convex.vertices[1] = new float3(1, 0, 0);
            convex.vertices[2] = new float3(1, 1, 0);
            convex.vertices[3] = new float3(0, 1, 0);
            convex.facets[0] = new Plane(new float3(0, 0, 1), 0);
            convex.facets[1] = new Plane(new float3(0, 0, -1), 0);
            convex.edges[0] = new ConvexH.HalfEdge(7, 0, 0);
            convex.edges[1] = new ConvexH.HalfEdge(6, 1, 0);
            convex.edges[2] = new ConvexH.HalfEdge(5, 2, 0);
            convex.edges[3] = new ConvexH.HalfEdge(4, 3, 0);

            convex.edges[4] = new ConvexH.HalfEdge(3, 0, 1);
            convex.edges[5] = new ConvexH.HalfEdge(2, 3, 1);
            convex.edges[6] = new ConvexH.HalfEdge(1, 2, 1);
            convex.edges[7] = new ConvexH.HalfEdge(0, 1, 1);
            AssertIntact(convex, planetestepsilon);
            return convex;
        }

        public static ConvexH test_cube()
        {
            ConvexH convex = new ConvexH(8, 24, 6);
            convex.vertices[0] = new float3(0, 0, 0);
            convex.vertices[1] = new float3(0, 0, 1);
            convex.vertices[2] = new float3(0, 1, 0);
            convex.vertices[3] = new float3(0, 1, 1);
            convex.vertices[4] = new float3(1, 0, 0);
            convex.vertices[5] = new float3(1, 0, 1);
            convex.vertices[6] = new float3(1, 1, 0);
            convex.vertices[7] = new float3(1, 1, 1);

            convex.facets[0] = new Plane(new float3(-1, 0, 0), 0);
            convex.facets[1] = new Plane(new float3(1, 0, 0), -1);
            convex.facets[2] = new Plane(new float3(0, -1, 0), 0);
            convex.facets[3] = new Plane(new float3(0, 1, 0), -1);
            convex.facets[4] = new Plane(new float3(0, 0, -1), 0);
            convex.facets[5] = new Plane(new float3(0, 0, 1), -1);

            convex.edges[0] = new ConvexH.HalfEdge(11, 0, 0);
            convex.edges[1] = new ConvexH.HalfEdge(23, 1, 0);
            convex.edges[2] = new ConvexH.HalfEdge(15, 3, 0);
            convex.edges[3] = new ConvexH.HalfEdge(16, 2, 0);

            convex.edges[4] = new ConvexH.HalfEdge(13, 6, 1);
            convex.edges[5] = new ConvexH.HalfEdge(21, 7, 1);
            convex.edges[6] = new ConvexH.HalfEdge(9, 5, 1);
            convex.edges[7] = new ConvexH.HalfEdge(18, 4, 1);

            convex.edges[8] = new ConvexH.HalfEdge(19, 0, 2);
            convex.edges[9] = new ConvexH.HalfEdge(6, 4, 2);
            convex.edges[10] = new ConvexH.HalfEdge(20, 5, 2);
            convex.edges[11] = new ConvexH.HalfEdge(0, 1, 2);

            convex.edges[12] = new ConvexH.HalfEdge(22, 3, 3);
            convex.edges[13] = new ConvexH.HalfEdge(4, 7, 3);
            convex.edges[14] = new ConvexH.HalfEdge(17, 6, 3);
            convex.edges[15] = new ConvexH.HalfEdge(2, 2, 3);

            convex.edges[16] = new ConvexH.HalfEdge(3, 0, 4);
            convex.edges[17] = new ConvexH.HalfEdge(14, 2, 4);
            convex.edges[18] = new ConvexH.HalfEdge(7, 6, 4);
            convex.edges[19] = new ConvexH.HalfEdge(8, 4, 4);

            convex.edges[20] = new ConvexH.HalfEdge(10, 1, 5);
            convex.edges[21] = new ConvexH.HalfEdge(5, 5, 5);
            convex.edges[22] = new ConvexH.HalfEdge(12, 7, 5);
            convex.edges[23] = new ConvexH.HalfEdge(1, 3, 5);

            return convex;
        }

        public static ConvexH ConvexHMakeCube(float3 bmin, float3 bmax)
        {
            ConvexH convex = test_cube();
            convex.vertices[0] = new float3(bmin.x, bmin.y, bmin.z);
            convex.vertices[1] = new float3(bmin.x, bmin.y, bmax.z);
            convex.vertices[2] = new float3(bmin.x, bmax.y, bmin.z);
            convex.vertices[3] = new float3(bmin.x, bmax.y, bmax.z);
            convex.vertices[4] = new float3(bmax.x, bmin.y, bmin.z);
            convex.vertices[5] = new float3(bmax.x, bmin.y, bmax.z);
            convex.vertices[6] = new float3(bmax.x, bmax.y, bmin.z);
            convex.vertices[7] = new float3(bmax.x, bmax.y, bmax.z);

            convex.facets[0] = new Plane(new float3(-1, 0, 0), bmin.x);
            convex.facets[1] = new Plane(new float3(1, 0, 0), -bmax.x);
            convex.facets[2] = new Plane(new float3(0, -1, 0), bmin.y);
            convex.facets[3] = new Plane(new float3(0, 1, 0), -bmax.y);
            convex.facets[4] = new Plane(new float3(0, 0, -1), bmin.z);
            convex.facets[5] = new Plane(new float3(0, 0, 1), -bmax.z);
            return convex;
        }

        public static ConvexH ConvexHCrop(ref ConvexH convex, Plane slice, float planetestepsilon)
        {
            int i;
            int vertcountunder = 0;
            int vertcountover = 0;
            List<int> vertscoplanar = new List<int>(); // existing vertex members of convex that are coplanar
            List<int> edgesplit = new List<int>(); // existing edges that members of convex that cross the splitplane

            Debug.Assert(convex.edges.Count < 480);

            EdgeFlag[] edgeflag = new EdgeFlag[512];
            VertFlag[] vertflag = new VertFlag[256];
            PlaneFlag[] planeflag = new PlaneFlag[128];
            ConvexH.HalfEdge[] tmpunderedges = new ConvexH.HalfEdge[512];
            Plane[] tmpunderplanes = new Plane[128];
            Coplanar[] coplanaredges = new Coplanar[512];
            int coplanaredges_num = 0;

            List<float3> createdverts = new List<float3>();

            // do the side-of-plane tests
            for (i = 0; i < convex.vertices.Count; i++)
            {
                vertflag[i].planetest = (byte)PlaneTest(slice, convex.vertices[i], planetestepsilon);
                if (vertflag[i].planetest == (0))
                {
                    // ? vertscoplanar.Add(i);
                    vertflag[i].undermap = (byte)vertcountunder++;
                    vertflag[i].overmap = (byte)vertcountover++;
                }
                else if (vertflag[i].planetest == (1))
                {
                    vertflag[i].undermap = (byte)vertcountunder++;
                }
                else
                {
                    Debug.Assert(vertflag[i].planetest == (2));
                    vertflag[i].overmap = (byte)vertcountover++;
                    vertflag[i].undermap = 255; // for debugging purposes
                }
            }
            int vertcountunderold = vertcountunder; // for debugging only

            int under_edge_count = 0;
            int underplanescount = 0;
            int e0 = 0;

            for (int currentplane = 0; currentplane < convex.facets.Count; currentplane++)
            {
                int estart = e0;
                int enextface = 0;
                int planeside = 0;
                int e1 = e0 + 1;
                int vout = -1;
                int vin = -1;
                int coplanaredge = -1;
                do
                {

                    if (e1 >= convex.edges.Count || convex.edges[e1].p != currentplane)
                    {
                        enextface = e1;
                        e1 = estart;
                    }
                    ConvexH.HalfEdge edge0 = convex.edges[e0];
                    ConvexH.HalfEdge edge1 = convex.edges[e1];
                    ConvexH.HalfEdge edgea = convex.edges[edge0.ea];

                    planeside |= vertflag[edge0.v].planetest;
                    //if((vertflag[edge0.v].planetest & vertflag[edge1.v].planetest)  == COPLANAR) {
                    //	assert(ecop==-1);
                    //	ecop=e;
                    //}

                    if (vertflag[edge0.v].planetest == (2) && vertflag[edge1.v].planetest == (2))
                    {
                        // both endpoints over plane
                        edgeflag[e0].undermap = -1;
                    }
                    else if ((vertflag[edge0.v].planetest | vertflag[edge1.v].planetest) == (1))
                    {
                        // at least one endpoint under, the other coplanar or under

                        edgeflag[e0].undermap = (short)under_edge_count;
                        tmpunderedges[under_edge_count].v = vertflag[edge0.v].undermap;
                        tmpunderedges[under_edge_count].p = (byte)underplanescount;
                        if (edge0.ea < e0)
                        {
                            // connect the neighbors
                            Debug.Assert(edgeflag[edge0.ea].undermap != -1);
                            tmpunderedges[under_edge_count].ea = edgeflag[edge0.ea].undermap;
                            tmpunderedges[edgeflag[edge0.ea].undermap].ea = (short)under_edge_count;
                        }
                        under_edge_count++;
                    }
                    else if ((vertflag[edge0.v].planetest | vertflag[edge1.v].planetest) == (0))
                    {
                        // both endpoints coplanar
                        // must check a 3rd point to see if UNDER
                        int e2 = e1 + 1;
                        if (e2 >= convex.edges.Count || convex.edges[e2].p != currentplane)
                        {
                            e2 = estart;
                        }
                        Debug.Assert(convex.edges[e2].p == currentplane);
                        ConvexH.HalfEdge edge2 = convex.edges[e2];
                        if (vertflag[edge2.v].planetest == (1))
                        {

                            edgeflag[e0].undermap = (short)under_edge_count;
                            tmpunderedges[under_edge_count].v = vertflag[edge0.v].undermap;
                            tmpunderedges[under_edge_count].p = (byte)underplanescount;
                            tmpunderedges[under_edge_count].ea = -1;
                            // make sure this edge is added to the "coplanar" list
                            coplanaredge = under_edge_count;
                            vout = vertflag[edge0.v].undermap;
                            vin = vertflag[edge1.v].undermap;
                            under_edge_count++;
                        }
                        else
                        {
                            edgeflag[e0].undermap = -1;
                        }
                    }
                    else if (vertflag[edge0.v].planetest == (1) && vertflag[edge1.v].planetest == (2))
                    {
                        // first is under 2nd is over

                        edgeflag[e0].undermap = (short)under_edge_count;
                        tmpunderedges[under_edge_count].v = vertflag[edge0.v].undermap;
                        tmpunderedges[under_edge_count].p = (byte)underplanescount;
                        if (edge0.ea < e0)
                        {
                            Debug.Assert(edgeflag[edge0.ea].undermap != -1);
                            // connect the neighbors
                            tmpunderedges[under_edge_count].ea = edgeflag[edge0.ea].undermap;
                            tmpunderedges[edgeflag[edge0.ea].undermap].ea = (short)under_edge_count;
                            vout = tmpunderedges[edgeflag[edge0.ea].undermap].v;
                        }
                        else
                        {
                            Plane p0 = convex.facets[edge0.p];
                            Plane pa = convex.facets[edgea.p];
                            createdverts.Add(ThreePlaneIntersection(p0, pa, slice));
                            //createdverts.Add(PlaneProject(slice,PlaneLineIntersection(slice,convex.vertices[edge0.v],convex.vertices[edgea.v])));
                            //createdverts.Add(PlaneLineIntersection(slice,convex.vertices[edge0.v],convex.vertices[edgea.v]));
                            vout = vertcountunder++;
                        }
                        under_edge_count++;
                        /// hmmm something to think about: i might be able to output this edge regarless of
                        // wheter or not we know v-in yet.  ok i;ll try this now:
                        tmpunderedges[under_edge_count].v = (byte)vout;
                        tmpunderedges[under_edge_count].p = (byte)underplanescount;
                        tmpunderedges[under_edge_count].ea = -1;
                        coplanaredge = under_edge_count;
                        under_edge_count++;

                        if (vin != -1)
                        {
                            // we previously processed an edge  where we came under
                            // now we know about vout as well

                            // ADD THIS EDGE TO THE LIST OF EDGES THAT NEED NEIGHBOR ON PARTITION PLANE!!
                        }

                    }
                    else if (vertflag[edge0.v].planetest == (0) && vertflag[edge1.v].planetest == (2))
                    {
                        // first is coplanar 2nd is over

                        edgeflag[e0].undermap = -1;
                        vout = vertflag[edge0.v].undermap;
                        // I hate this but i have to make sure part of this face is UNDER before ouputting this vert
                        int k = estart;
                        Debug.Assert(edge0.p == currentplane);
                        while (!((planeside & 1) != 0) && k < convex.edges.Count && convex.edges[k].p == edge0.p)
                        {
                            planeside |= vertflag[convex.edges[k].v].planetest;
                            k++;
                        }
                        if ((planeside & 1) != 0)
                        {
                            tmpunderedges[under_edge_count].v = (byte)vout;
                            tmpunderedges[under_edge_count].p = (byte)underplanescount;
                            tmpunderedges[under_edge_count].ea = -1;
                            coplanaredge = under_edge_count; // hmmm should make a note of the edge # for later on
                            under_edge_count++;

                        }
                    }
                    else if (vertflag[edge0.v].planetest == (2) && vertflag[edge1.v].planetest == (1))
                    {
                        // first is over next is under
                        // new vertex!!!
                        Debug.Assert(vin == -1);
                        if (e0 < edge0.ea)
                        {
                            Plane p0 = convex.facets[edge0.p];
                            Plane pa = convex.facets[edgea.p];
                            createdverts.Add(ThreePlaneIntersection(p0, pa, slice));
                            //createdverts.Add(PlaneLineIntersection(slice,convex.vertices[edge0.v],convex.vertices[edgea.v]));
                            //createdverts.Add(PlaneProject(slice,PlaneLineIntersection(slice,convex.vertices[edge0.v],convex.vertices[edgea.v])));
                            vin = vertcountunder++;
                        }
                        else
                        {
                            // find the new vertex that was created by edge[edge0.ea]
                            int nea = edgeflag[edge0.ea].undermap;
                            Debug.Assert(tmpunderedges[nea].p == tmpunderedges[nea + 1].p);
                            vin = tmpunderedges[nea + 1].v;
                            Debug.Assert(vin < vertcountunder);
                            Debug.Assert(vin >= vertcountunderold); // for debugging only
                        }
                        if (vout != -1)
                        {
                            // we previously processed an edge  where we went over
                            // now we know vin too
                            // ADD THIS EDGE TO THE LIST OF EDGES THAT NEED NEIGHBOR ON PARTITION PLANE!!
                        }
                        // output edge
                        tmpunderedges[under_edge_count].v = (byte)vin;
                        tmpunderedges[under_edge_count].p = (byte)underplanescount;
                        edgeflag[e0].undermap = (short)under_edge_count;
                        if (e0 > edge0.ea)
                        {
                            Debug.Assert(edgeflag[edge0.ea].undermap != -1);
                            // connect the neighbors
                            tmpunderedges[under_edge_count].ea = edgeflag[edge0.ea].undermap;
                            tmpunderedges[edgeflag[edge0.ea].undermap].ea = (short)under_edge_count;
                        }
                        Debug.Assert(edgeflag[e0].undermap == under_edge_count);
                        under_edge_count++;
                    }
                    else if (vertflag[edge0.v].planetest == (2) && vertflag[edge1.v].planetest == (0))
                    {
                        // first is over next is coplanar

                        edgeflag[e0].undermap = -1;
                        vin = vertflag[edge1.v].undermap;
                        Debug.Assert(vin != -1);
                        if (vout != -1)
                        {
                            // we previously processed an edge  where we came under
                            // now we know both endpoints
                            // ADD THIS EDGE TO THE LIST OF EDGES THAT NEED NEIGHBOR ON PARTITION PLANE!!
                        }

                    }
                    else
                    {
                        Debug.Assert(false);
                    }


                    e0 = e1;
                    e1++; // do the modulo at the beginning of the loop

                } while (e0 != estart);
                e0 = enextface;
                if ((planeside & 1) != 0)
                {
                    planeflag[currentplane].undermap = (byte)underplanescount;
                    tmpunderplanes[underplanescount] = convex.facets[currentplane];
                    underplanescount++;
                }
                else
                {
                    planeflag[currentplane].undermap = 0;
                }
                if (vout >= 0 && (planeside & 1) != 0)
                {
                    Debug.Assert(vin >= 0);
                    Debug.Assert(coplanaredge >= 0);
                    Debug.Assert(coplanaredge != 511);
                    coplanaredges[coplanaredges_num].ea = (ushort)coplanaredge;
                    coplanaredges[coplanaredges_num].v0 = (byte)vin;
                    coplanaredges[coplanaredges_num].v1 = (byte)vout;
                    coplanaredges_num++;
                }
            }

            // add the new plane to the mix:
            if (coplanaredges_num > 0)
            {
                tmpunderplanes[underplanescount++] = slice;
            }
            for (i = 0; i < coplanaredges_num - 1; i++)
            {
                if (coplanaredges[i].v1 != coplanaredges[i + 1].v0)
                {
                    int j = 0;
                    for (j = i + 2; j < coplanaredges_num; j++)
                    {
                        if (coplanaredges[i].v1 == coplanaredges[j].v0)
                        {
                            Coplanar tmp = coplanaredges[i + 1];
                            coplanaredges[i + 1] = coplanaredges[j];
                            coplanaredges[j] = tmp;
                            break;
                        }
                    }
                    if (j >= coplanaredges_num)
                    {
                        Debug.Assert(j < coplanaredges_num);
                        return null;
                    }
                }
            }

            ConvexH punder = new ConvexH(vertcountunder, under_edge_count + coplanaredges_num, underplanescount);
            ConvexH under = punder;

            {
                int k = 0;
                for (i = 0; i < convex.vertices.Count; i++)
                {
                    if (vertflag[i].planetest != (2))
                    {
                        under.vertices[k++] = convex.vertices[i];
                    }
                }
                i = 0;
                while (k < vertcountunder)
                {
                    under.vertices[k++] = createdverts[i++];
                }
                Debug.Assert(i == createdverts.Count);
            }

            for (i = 0; i < coplanaredges_num; i++)
            {
                ConvexH.HalfEdge edge = under.edges[under_edge_count + i];
                edge.p = (byte)(underplanescount - 1);
                edge.ea = (short)coplanaredges[i].ea;
                edge.v = (byte)coplanaredges[i].v0;
                under.edges[under_edge_count + i] = edge;

                tmpunderedges[coplanaredges[i].ea].ea = (short)(under_edge_count + i);
            }

            under.edges = new List<ConvexH.HalfEdge>(tmpunderedges);
            under.facets = new List<Plane>(tmpunderplanes);
            return punder;
        }

        public static ConvexH ConvexHDup(ConvexH src)
        {
            ConvexH dst = new ConvexH(src.vertices.Count, src.edges.Count, src.facets.Count);
            dst.vertices = new List<float3>(src.vertices.Count);
            foreach (float3 f in src.vertices)
                dst.vertices.Add(new float3(f));
            dst.edges = new List<ConvexH.HalfEdge>(src.edges.Count);
            foreach (ConvexH.HalfEdge e in src.edges)
                dst.edges.Add(new ConvexH.HalfEdge(e));
            dst.facets = new List<Plane>(src.facets.Count);
            foreach (Plane p in src.facets)
                dst.facets.Add(new Plane(p));
            return dst;
        }

        public static int candidateplane(List<Plane> planes, int planes_count, ConvexH convex, float epsilon)
        {
            int p = 0;
            float md = 0;
            int i;
            for (i = 0; i < planes_count; i++)
            {
                float d = 0;
                for (int j = 0; j < convex.vertices.Count; j++)
                {
                    d = Math.Max(d, float3.dot(convex.vertices[j], planes[i].normal) + planes[i].dist);
                }
                if (i == 0 || d > md)
                {
                    p = i;
                    md = d;
                }
            }
            return (md > epsilon) ? p : -1;
        }

        public static float3 orth(float3 v)
        {
            float3 a = float3.cross(v, new float3(0f, 0f, 1f));
            float3 b = float3.cross(v, new float3(0f, 1f, 0f));
            return float3.normalize((float3.magnitude(a) > float3.magnitude(b)) ? a : b);
        }

        public static int maxdir(List<float3> p, int count, float3 dir)
        {
            Debug.Assert(count != 0);
            int m = 0;
            float currDotm = float3.dot(p[0], dir);
            for (int i = 1; i < count; i++)
            {
                float currDoti = float3.dot(p[i], dir);
                if (currDoti > currDotm)
                {
                    currDotm = currDoti;
                    m = i;
                }
            }
            return m;
        }

        public static int maxdirfiltered(List<float3> p, int count, float3 dir, byte[] allow)
        {
            //Debug.Assert(count != 0);
            int m = -1;
            float currDotm = 0;
            float currDoti;

            for (int i = 0; i < count; i++)
            {
                if (allow[i] != 0)
                {
                    currDotm = float3.dot(p[i], dir);
                    m = i;
                    break;
               }
            }

            if(m == -1)
            {
                Debug.Assert(false);
                return m;
            }

            for (int i = m + 1; i < count; i++)
            {
                if (allow[i] != 0)
                {
                    currDoti = float3.dot(p[i], dir);
                    if (currDoti > currDotm)
                    {
                        currDotm = currDoti;
                        m = i;
                    }
                }
            }

//            Debug.Assert(m != -1);
            return m;
        }

        public static int maxdirsterid(List<float3> p, int count, float3 dir, byte[] allow)
        {
            int m = -1;
            while (m == -1)
            {
                m = maxdirfiltered(p, count, dir, allow);
                if (allow[m] == 3)
                    return m;
                float3 u = orth(dir);
                float3 v = float3.cross(u, dir);
                int ma = -1;
                for (float x = 0.0f; x <= 360.0f; x += 45.0f)
                {
                    int mb;
                    {
                        float s = (float)Math.Sin(0.01745329f * x);
                        float c = (float)Math.Cos(0.01745329f * x);
                        mb = maxdirfiltered(p, count, dir + (u * s + v * c) * 0.025f, allow);
                    }
                    if (ma == m && mb == m)
                    {
                        allow[m] = 3;
                        return m;
                    }
                    if (ma != -1 && ma != mb) // Yuck - this is really ugly
                    {
                        int mc = ma;
                        for (float xx = x - 40.0f; xx <= x; xx += 5.0f)
                        {
                            float s = (float)Math.Sin(0.01745329f * xx);
                            float c = (float)Math.Cos(0.01745329f * xx);
                            int md = maxdirfiltered(p, count, dir + (u * s + v * c) * 0.025f, allow);
                            if (mc == m && md == m)
                            {
                                allow[m] = 3;
                                return m;
                            }
                            mc = md;
                        }
                    }
                    ma = mb;
                }
                allow[m] = 0;
                m = -1;
            }

            Debug.Assert(false);
            return m;
        }

        public static int4 FindSimplex(List<float3> verts, byte[] allow)
        {
            float3[] basis = new float3[3];
            basis[0] = new float3(0.01f, 0.02f, 1.0f);
            int p0 = maxdirsterid(verts, verts.Count, basis[0], allow);
            int p1 = maxdirsterid(verts, verts.Count, -basis[0], allow);
            basis[0] = verts[p0] - verts[p1];
            if (p0 == p1 || basis[0] == new float3(0, 0, 0))
                return new int4(-1, -1, -1, -1);
            basis[1] = float3.cross(new float3(1, 0.02f, 0), basis[0]);
            basis[2] = float3.cross(new float3(-0.02f, 1, 0), basis[0]);
            basis[1] = float3.normalize((float3.magnitude(basis[1]) > float3.magnitude(basis[2])) ? basis[1] : basis[2]);
            int p2 = maxdirsterid(verts, verts.Count, basis[1], allow);
            if (p2 == p0 || p2 == p1)
            {
                p2 = maxdirsterid(verts, verts.Count, -basis[1], allow);
            }
            if (p2 == p0 || p2 == p1)
                return new int4(-1, -1, -1, -1);
            basis[1] = verts[p2] - verts[p0];
            basis[2] = float3.normalize(float3.cross(basis[1], basis[0]));
            int p3 = maxdirsterid(verts, verts.Count, basis[2], allow);
            if (p3 == p0 || p3 == p1 || p3 == p2)
                p3 = maxdirsterid(verts, verts.Count, -basis[2], allow);
            if (p3 == p0 || p3 == p1 || p3 == p2)
                return new int4(-1, -1, -1, -1);
            Debug.Assert(!(p0 == p1 || p0 == p2 || p0 == p3 || p1 == p2 || p1 == p3 || p2 == p3));
            if (float3.dot(verts[p3] - verts[p0], float3.cross(verts[p1] - verts[p0], verts[p2] - verts[p0])) < 0)
            {
                return new int4(p0, p1, p3, p2);
            }
            return new int4(p0, p1, p2, p3);
        }

        public static float GetDist(float px, float py, float pz, float3 p2)
        {
            float dx = px - p2.x;
            float dy = py - p2.y;
            float dz = pz - p2.z;

            return dx * dx + dy * dy + dz * dz;
        }

        public static void ReleaseHull(PHullResult result)
        {
            if (result.Indices != null)
                result.Indices = null;
            if (result.Vertices != null)
                result.Vertices = null;
        }

        public static int calchullgen(List<float3> verts, int vlimit, List<HullTriangle> tris)
        {
            if (verts.Count < 4)
                return 0;
            if (vlimit == 0)
                vlimit = 1000000000;
            int j;
            float3 bmin = new float3(verts[0]);
            float3 bmax = new float3(verts[0]);
            byte[] isextreme = new byte[verts.Count];
            byte[] allow = new byte[verts.Count];
            for (j = 0; j < verts.Count; j++)
            {
                allow[j] = 1;
                isextreme[j] = 0;
                bmin = float3.VectorMin(bmin, verts[j]);
                bmax = float3.VectorMax(bmax, verts[j]);
            }
            float epsilon = float3.magnitude(bmax - bmin) * 0.001f;

            int4 p = FindSimplex(verts, allow);
            if (p.x == -1) // simplex failed
                return 0;

            float3 center = (verts[p[0]] + verts[p[1]] + verts[p[2]] + verts[p[3]]) / 4.0f; // a valid interior point
            HullTriangle t0 = new HullTriangle(p[2], p[3], p[1], tris);
            t0.n = new int3(2, 3, 1);
            HullTriangle t1 = new HullTriangle(p[3], p[2], p[0], tris);
            t1.n = new int3(3, 2, 0);
            HullTriangle t2 = new HullTriangle(p[0], p[1], p[3], tris);
            t2.n = new int3(0, 1, 3);
            HullTriangle t3 = new HullTriangle(p[1], p[0], p[2], tris);
            t3.n = new int3(1, 0, 2);
            isextreme[p[0]] = isextreme[p[1]] = isextreme[p[2]] = isextreme[p[3]] = 1;
            checkit(t0, tris);
            checkit(t1, tris);
            checkit(t2, tris);
            checkit(t3, tris);

            for (j = 0; j < tris.Count; j++)
            {
                HullTriangle t = tris[j];
                Debug.Assert((object)t != null);
                Debug.Assert(t.vmax < 0);
                float3 n = TriNormal(verts[(t)[0]], verts[(t)[1]], verts[(t)[2]]);
                t.vmax = maxdirsterid(verts, verts.Count, n, allow);
                t.rise = float3.dot(n, verts[t.vmax] - verts[(t)[0]]);
            }
            HullTriangle te;
            vlimit -= 4;
            while (vlimit > 0 && (te = extrudable(epsilon, tris)) != null)
            {
                int3 ti = te;
                int v = te.vmax;
                Debug.Assert(isextreme[v] == 0); // wtf we've already done this vertex
                isextreme[v] = 1;
                //if(v==p0 || v==p1 || v==p2 || v==p3) continue; // done these already
                j = tris.Count;
                while (j-- != 0)
                {
                    if (tris.Count <= j || (object)tris[j] == null)
                        continue;
                    int3 t = tris[j];
                    if (above(verts, t, verts[v], 0.01f * epsilon))
                    {
                        extrude(tris[j], v, tris);
                    }
                }
                // now check for those degenerate cases where we have a flipped triangle or a really skinny triangle
                j = tris.Count;
                while (j-- != 0)
                {
                    if (tris.Count <= j || (object)tris[j] == null)
                        continue;
                    if (!hasvert(tris[j], v))
                        break;
                    int3 nt = tris[j];
                    if (above(verts, nt, center, 0.01f * epsilon) || float3.magnitude(float3.cross(verts[nt[1]] - verts[nt[0]], verts[nt[2]] - verts[nt[1]])) < epsilon * epsilon * 0.1f)
                    {
                        HullTriangle nb = tris[tris[j].n[0]];
                        Debug.Assert(nb != null);
                        Debug.Assert(!hasvert(nb, v));
                        Debug.Assert(nb.id < j);
                        extrude(nb, v, tris);
                        j = tris.Count;
                    }
                }
                j = tris.Count;
                while (j-- != 0)
                {
                    HullTriangle t = tris[j];
                    if (t == null)
                        continue;
                    if (t.vmax >= 0)
                        break;
                    float3 n = TriNormal(verts[(t)[0]], verts[(t)[1]], verts[(t)[2]]);
                    t.vmax = maxdirsterid(verts, verts.Count, n, allow);
                    if (isextreme[t.vmax] != 0)
                    {
                        t.vmax = -1; // already done that vertex - algorithm needs to be able to terminate.
                    }
                    else
                    {
                        t.rise = float3.dot(n, verts[t.vmax] - verts[(t)[0]]);
                    }
                }
                vlimit--;
            }
            return 1;
        }

        public static bool calchull(List<float3> verts, out List<int> tris_out, int vlimit, List<HullTriangle> tris)
        {
            tris_out = null;

            int rc = calchullgen(verts, vlimit, tris);
            if (rc == 0)
                return false;
            List<int> ts = new List<int>();
            for (int i = 0; i < tris.Count; i++)
            {
                if ((object)tris[i] != null)
                {
                    for (int j = 0; j < 3; j++)
                        ts.Add((tris[i])[j]);
                    tris[i] = null;
                }
            }

            tris_out = ts;
            tris.Clear();
            return true;
        }

        public static int calchullpbev(List<float3> verts, int vlimit, out List<Plane> planes, float bevangle, List<HullTriangle> tris)
        {
            int i;
            int j;
            planes = new List<Plane>();
            int rc = calchullgen(verts, vlimit, tris);
            if (rc == 0)
                return 0;
            for (i = 0; i < tris.Count; i++)
            {
                if (tris[i] != null)
                {
                    Plane p = new Plane();
                    HullTriangle t = tris[i];
                    p.normal = TriNormal(verts[(t)[0]], verts[(t)[1]], verts[(t)[2]]);
                    p.dist = -float3.dot(p.normal, verts[(t)[0]]);
                    planes.Add(p);
                    for (j = 0; j < 3; j++)
                    {
                        if (t.n[j] < t.id)
                            continue;
                        HullTriangle s = tris[t.n[j]];
                        float3 snormal = TriNormal(verts[(s)[0]], verts[(s)[1]], verts[(s)[2]]);
                        if (float3.dot(snormal, p.normal) >= Math.Cos(bevangle * (3.14159264f / 180.0f)))
                            continue;
                        float3 n = float3.normalize(snormal + p.normal);
                        planes.Add(new Plane(n, -float3.dot(n, verts[maxdir(verts, verts.Count, n)])));
                    }
                }
            }

            tris.Clear();
            return 1;
        }

        public static int overhull(List<Plane> planes, List<float3> verts, int maxplanes, out List<float3> verts_out, out List<int> faces_out, float inflate)
        {
            verts_out = null;
            faces_out = null;

            int i;
            int j;
            if (verts.Count < 4)
                return 0;
            maxplanes = Math.Min(maxplanes, planes.Count);
            float3 bmin = new float3(verts[0]);
            float3 bmax = new float3(verts[0]);
            for (i = 0; i < verts.Count; i++)
            {
                bmin = float3.VectorMin(bmin, verts[i]);
                bmax = float3.VectorMax(bmax, verts[i]);
            }
            //	float diameter = magnitude(bmax-bmin);
            //	inflate *=diameter;   // RELATIVE INFLATION
            bmin -= new float3(inflate, inflate, inflate);
            bmax += new float3(inflate, inflate, inflate);
            for (i = 0; i < planes.Count; i++)
            {
                planes[i].dist -= inflate;
            }
            float3 emin = new float3(bmin);
            float3 emax = new float3(bmax);
            float epsilon = float3.magnitude(emax - emin) * 0.025f;
            float planetestepsilon = float3.magnitude(emax - emin) * (0.001f);
            // todo: add bounding cube planes to force bevel. or try instead not adding the diameter expansion ??? must think.
            // ConvexH *convex = ConvexHMakeCube(bmin - float3(diameter,diameter,diameter),bmax+float3(diameter,diameter,diameter));
            ConvexH c = ConvexHMakeCube(new float3(bmin), new float3(bmax));
            int k;
            while (maxplanes-- != 0 && (k = candidateplane(planes, planes.Count, c, epsilon)) >= 0)
            {
                ConvexH tmp = c;
                c = ConvexHCrop(ref tmp, planes[k], planetestepsilon);
                if (c == null) // might want to debug this case better!!!
                {
                    c = tmp;
                    break;
                }
                if (AssertIntact(c, planetestepsilon) == false) // might want to debug this case better too!!!
                {
                    c = tmp;
                    break;
                }
                tmp.edges = null;
                tmp.facets = null;
                tmp.vertices = null;
            }

            Debug.Assert(AssertIntact(c, planetestepsilon));
            //return c;
            //C++ TO C# CONVERTER TODO TASK: The memory management function 'malloc' has no equivalent in C#:
            faces_out = new List<int>(); //(int)malloc(sizeof(int) * (1 + c.facets.Count + c.edges.Count)); // new int[1+c->facets.count+c->edges.count];
            int faces_count_out = 0;
            i = 0;
            faces_out[faces_count_out++] = -1;
            k = 0;
            while (i < c.edges.Count)
            {
                j = 1;
                while (j + i < c.edges.Count && c.edges[i].p == c.edges[i + j].p)
                {
                    j++;
                }
                faces_out[faces_count_out++] = j;
                while (j-- != 0)
                {
                    faces_out[faces_count_out++] = c.edges[i].v;
                    i++;
                }
                k++;
            }
            faces_out[0] = k; // number of faces.
            Debug.Assert(k == c.facets.Count);
            Debug.Assert(faces_count_out == 1 + c.facets.Count + c.edges.Count);
            verts_out = c.vertices; // new float3[c->vertices.count];
            int verts_count_out = c.vertices.Count;
            for (i = 0; i < c.vertices.Count; i++)
            {
                verts_out[i] = new float3(c.vertices[i]);
            }

            c.edges = null;
            c.facets = null;
            c.vertices = null;
            return 1;
        }

        public static int overhullv(List<float3> verts, int maxplanes, out List<float3> verts_out, out List<int> faces_out, float inflate, float bevangle, int vlimit, List<HullTriangle> tris)
        {
            verts_out = null;
            faces_out = null;

            if (verts.Count == 0)
                return 0;
            List<Plane> planes = new List<Plane>();
            int rc = calchullpbev(verts, vlimit, out planes, bevangle, tris);
            if (rc == 0)
                return 0;
            return overhull(planes, verts, maxplanes, out verts_out, out faces_out, inflate);
        }

        public static void addPoint(ref uint vcount, List<float3> p, float x, float y, float z)
        {
            p.Add(new float3(x, y, z));
            vcount++;
        }

        public static bool ComputeHull(List<float3> vertices, ref PHullResult result, int vlimit, float inflate)
        {
            List<HullTriangle> tris = new List<HullTriangle>();
            List<int> faces;
            List<float3> verts_out;

            if (inflate == 0.0f)
            {
                List<int> tris_out;
                bool ret = calchull(vertices, out tris_out, vlimit, tris);
                if (ret == false)
                    return false;

                result.Indices = tris_out;
                result.Vertices = vertices;
                return true;
            }
            else
            {
                int ret = overhullv(vertices, 35, out verts_out, out faces, inflate, 120.0f, vlimit, tris);
                if (ret == 0)
                    return false;

                List<int3> tris2 = new List<int3>();
                int n = faces[0];
                int k = 1;
                for (int i = 0; i < n; i++)
                {
                    int pn = faces[k++];
                    for (int j = 2; j < pn; j++)
                        tris2.Add(new int3(faces[k], faces[k + j - 1], faces[k + j]));
                    k += pn;
                }
                Debug.Assert(tris2.Count == faces.Count - 1 - (n * 3));

                result.Indices = new List<int>(tris2.Count * 3);
                for (int i = 0; i < tris2.Count; i++)
                {
                    result.Indices.Add(tris2[i].x);
                    result.Indices.Add(tris2[i].y);
                    result.Indices.Add(tris2[i].z);
                }
                result.Vertices = verts_out;

                return true;
            }
        }

        public static bool ComputeHull(List<float3> vertices, out List<int> indices)
        {
            List<HullTriangle> tris = new List<HullTriangle>();

            bool ret = calchull(vertices, out indices, 0, tris);
            if (ret == false)
            {
                indices = new List<int>();
                return false;
            }
            return true;
        }

        private static bool CleanupVertices(List<float3> svertices, out List<float3> vertices, float normalepsilon, out float3 scale)
        {
            const float EPSILON = 0.000001f;

            vertices = new List<float3>();
            scale = new float3(1f, 1f, 1f);

            if (svertices.Count == 0)
                return false;

            uint vcount = 0;

            float[] recip = new float[3];

            float[] bmin = { Single.MaxValue, Single.MaxValue, Single.MaxValue };
            float[] bmax = { Single.MinValue, Single.MinValue, Single.MinValue };

            for (int i = 0; i < svertices.Count; i++)
            {
                float3 p = svertices[i];

                for (int j = 0; j < 3; j++)
                {
                    if (p[j] < bmin[j])
                        bmin[j] = p[j];
                    if (p[j] > bmax[j])
                        bmax[j] = p[j];
                }
            }

            float dx = bmax[0] - bmin[0];
            float dy = bmax[1] - bmin[1];
            float dz = bmax[2] - bmin[2];

            float3 center = new float3();

            center.x = dx * 0.5f + bmin[0];
            center.y = dy * 0.5f + bmin[1];
            center.z = dz * 0.5f + bmin[2];

            if (dx < EPSILON || dy < EPSILON || dz < EPSILON || svertices.Count < 3)
            {
                float len = Single.MaxValue;

                if (dx > EPSILON && dx < len)
                    len = dx;
                if (dy > EPSILON && dy < len)
                    len = dy;
                if (dz > EPSILON && dz < len)
                    len = dz;

                if (len == Single.MaxValue)
                {
                    dx = dy = dz = 0.01f; // one centimeter
                }
                else
                {
                    if (dx < EPSILON) // 1/5th the shortest non-zero edge.
                        dx = len * 0.05f;
                    if (dy < EPSILON)
                        dy = len * 0.05f;
                    if (dz < EPSILON)
                        dz = len * 0.05f;
                }

                float x1 = center[0] - dx;
                float x2 = center[0] + dx;

                float y1 = center[1] - dy;
                float y2 = center[1] + dy;

                float z1 = center[2] - dz;
                float z2 = center[2] + dz;

                addPoint(ref vcount, vertices, x1, y1, z1);
                addPoint(ref vcount, vertices, x2, y1, z1);
                addPoint(ref vcount, vertices, x2, y2, z1);
                addPoint(ref vcount, vertices, x1, y2, z1);
                addPoint(ref vcount, vertices, x1, y1, z2);
                addPoint(ref vcount, vertices, x2, y1, z2);
                addPoint(ref vcount, vertices, x2, y2, z2);
                addPoint(ref vcount, vertices, x1, y2, z2);

                return true; // return cube
            }
            else
            {
                scale.x = dx;
                scale.y = dy;
                scale.z = dz;

                recip[0] = 1f / dx;
                recip[1] = 1f / dy;
                recip[2] = 1f / dz;

                center.x *= recip[0];
                center.y *= recip[1];
                center.z *= recip[2];
            }

            for (int i = 0; i < svertices.Count; i++)
            {
                float3 p = svertices[i];

                float px = p[0];
                float py = p[1];
                float pz = p[2];

                px = px * recip[0]; // normalize
                py = py * recip[1]; // normalize
                pz = pz * recip[2]; // normalize

                if (true)
                {
                    int j;

                    for (j = 0; j < vcount; j++)
                    {
                        float3 v = vertices[j];

                        float x = v[0];
                        float y = v[1];
                        float z = v[2];

                        float dx1 = Math.Abs(x - px);
                        float dy1 = Math.Abs(y - py);
                        float dz1 = Math.Abs(z - pz);

                        if (dx1 < normalepsilon && dy1 < normalepsilon && dz1 < normalepsilon)
                        {
                            // ok, it is close enough to the old one
                            // now let us see if it is further from the center of the point cloud than the one we already recorded.
                            // in which case we keep this one instead.
                            float dist1 = GetDist(px, py, pz, center);
                            float dist2 = GetDist(v[0], v[1], v[2], center);

                            if (dist1 > dist2)
                            {
                                v.x = px;
                                v.y = py;
                                v.z = pz;
                            }

                            break;
                        }
                    }

                    if (j == vcount)
                    {
                        float3 dest = new float3(px, py, pz);
                        vertices.Add(dest);
                        vcount++;
                    }
                }
            }

            // ok..now make sure we didn't prune so many vertices it is now invalid.
            if (true)
            {
                float[] bmin2 = { Single.MaxValue, Single.MaxValue, Single.MaxValue };
                float[] bmax2 = { Single.MinValue, Single.MinValue, Single.MinValue };

                for (int i = 0; i < vcount; i++)
                {
                    float3 p = vertices[i];
                    for (int j = 0; j < 3; j++)
                    {
                        if (p[j] < bmin2[j])
                            bmin2[j] = p[j];
                        if (p[j] > bmax2[j])
                            bmax2[j] = p[j];
                    }
                }

                float dx2 = bmax2[0] - bmin2[0];
                float dy2 = bmax2[1] - bmin2[1];
                float dz2 = bmax2[2] - bmin2[2];

                if (dx2 < EPSILON || dy2 < EPSILON || dz2 < EPSILON || vcount < 3)
                {
                    float cx = dx2 * 0.5f + bmin2[0];
                    float cy = dy2 * 0.5f + bmin2[1];
                    float cz = dz2 * 0.5f + bmin2[2];

                    float len = Single.MaxValue;

                    if (dx2 >= EPSILON && dx2 < len)
                        len = dx2;
                    if (dy2 >= EPSILON && dy2 < len)
                        len = dy2;
                    if (dz2 >= EPSILON && dz2 < len)
                        len = dz2;

                    if (len == Single.MaxValue)
                    {
                        dx2 = dy2 = dz2 = 0.01f; // one centimeter
                    }
                    else
                    {
                        if (dx2 < EPSILON) // 1/5th the shortest non-zero edge.
                            dx2 = len * 0.05f;
                        if (dy2 < EPSILON)
                            dy2 = len * 0.05f;
                        if (dz2 < EPSILON)
                            dz2 = len * 0.05f;
                    }

                    float x1 = cx - dx2;
                    float x2 = cx + dx2;

                    float y1 = cy - dy2;
                    float y2 = cy + dy2;

                    float z1 = cz - dz2;
                    float z2 = cz + dz2;

                    vcount = 0; // add box

                    addPoint(ref vcount, vertices, x1, y1, z1);
                    addPoint(ref vcount, vertices, x2, y1, z1);
                    addPoint(ref vcount, vertices, x2, y2, z1);
                    addPoint(ref vcount, vertices, x1, y2, z1);
                    addPoint(ref vcount, vertices, x1, y1, z2);
                    addPoint(ref vcount, vertices, x2, y1, z2);
                    addPoint(ref vcount, vertices, x2, y2, z2);
                    addPoint(ref vcount, vertices, x1, y2, z2);

                    return true;
                }
            }

            return true;
        }

        private static void BringOutYourDead(List<float3> verts, out List<float3> overts, List<int> indices)
        {
            int[] used = new int[verts.Count];
            int ocount = 0;

            overts = new List<float3>();

            for (int i = 0; i < indices.Count; i++)
            {
                int v = indices[i]; // original array index

                Debug.Assert(v >= 0 && v < verts.Count);

                if (used[v] != 0) // if already remapped
                {
                    indices[i] = used[v] - 1; // index to new array
                }
                else
                {
                    indices[i] = ocount; // new index mapping

                    overts.Add(verts[v]); // copy old vert to new vert array

                    ocount++; // increment output vert count

                    Debug.Assert(ocount >= 0 && ocount <= verts.Count);

                    used[v] = ocount; // assign new index remapping
                }
            }
        }

        public static HullError CreateConvexHull(HullDesc desc, ref HullResult result)
        {
            HullError ret = HullError.QE_FAIL;

            PHullResult hr = new PHullResult();

            uint vcount = (uint)desc.Vertices.Count;
            if (vcount < 8)
                vcount = 8;

            List<float3> vsource;
            float3 scale = new float3();

            bool ok = CleanupVertices(desc.Vertices, out vsource, desc.NormalEpsilon, out scale); // normalize point cloud, remove duplicates!

            if (ok)
            {
                if (true) // scale vertices back to their original size.
                {
                    for (int i = 0; i < vsource.Count; i++)
                    {
                        float3 v = vsource[i];
                        v.x *= scale[0];
                        v.y *= scale[1];
                        v.z *= scale[2];
                    }
                }

                float skinwidth = 0;
                if (desc.HasHullFlag(HullFlag.QF_SKIN_WIDTH))
                    skinwidth = desc.SkinWidth;

                ok = ComputeHull(vsource, ref hr, (int)desc.MaxVertices, skinwidth);

                if (ok)
                {
                    List<float3> vscratch;
                    BringOutYourDead(hr.Vertices, out vscratch, hr.Indices);

                    ret = HullError.QE_OK;

                    if (desc.HasHullFlag(HullFlag.QF_TRIANGLES)) // if he wants the results as triangle!
                    {
                        result.Polygons = false;
                        result.Indices = hr.Indices;
                        result.OutputVertices = vscratch;
                    }
                    else
                    {
                        result.Polygons = true;
                        result.OutputVertices = vscratch;

                        if (true)
                        {
                            List<int> source = hr.Indices;
                            List<int> dest = new List<int>();
                            for (int i = 0; i < hr.Indices.Count / 3; i++)
                            {
                                dest.Add(3);
                                dest.Add(source[i * 3 + 0]);
                                dest.Add(source[i * 3 + 1]);
                                dest.Add(source[i * 3 + 2]);
                            }

                            result.Indices = dest;
                        }
                    }
                }
            }

            return ret;
        }
    }
}
