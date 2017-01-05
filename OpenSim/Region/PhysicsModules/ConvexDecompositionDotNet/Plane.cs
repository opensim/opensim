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

namespace OpenSim.Region.PhysicsModules.ConvexDecompositionDotNet
{
    public class Plane
    {
        public float3 normal = new float3();
        public float dist; // distance below origin - the D from plane equasion Ax+By+Cz+D=0

        public Plane(float3 n, float d)
        {
            normal = new float3(n);
            dist = d;
        }

        public Plane(Plane p)
        {
            normal = new float3(p.normal);
            dist = p.dist;
        }

        public Plane()
        {
            dist = 0;
        }

        public void Transform(float3 position, Quaternion orientation)
        {
            //   Transforms the plane to the space defined by the
            //   given position/orientation
            float3 newNormal = Quaternion.Inverse(orientation) * normal;
            float3 origin = Quaternion.Inverse(orientation) * (-normal * dist - position);

            normal = newNormal;
            dist = -float3.dot(newNormal, origin);
        }

        public override int GetHashCode()
        {
            return normal.GetHashCode() ^ dist.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            Plane p = obj as Plane;
            if (p == null)
                return false;

            return this == p;
        }

        public static bool operator ==(Plane a, Plane b)
        {
            return (a.normal == b.normal && a.dist == b.dist);
        }

        public static bool operator !=(Plane a, Plane b)
        {
            return !(a == b);
        }

        public static Plane PlaneFlip(Plane plane)
        {
            return new Plane(-plane.normal, -plane.dist);
        }

        public static bool coplanar(Plane a, Plane b)
        {
            return (a == b || a == PlaneFlip(b));
        }
    }
}
