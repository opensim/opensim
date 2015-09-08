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
    public class float3 : IEquatable<float3>
    {
        public float x;
        public float y;
        public float z;

        public float3()
        {
            x = 0;
            y = 0;
            z = 0;
        }

        public float3(float _x, float _y, float _z)
        {
            x = _x;
            y = _y;
            z = _z;
        }

        public float3(float3 f)
        {
            x = f.x;
            y = f.y;
            z = f.z;
        }

        public float this[int i]
        {
            get
            {
                switch (i)
                {
                    case 0: return x;
                    case 1: return y;
                    case 2: return z;
                }
                throw new ArgumentOutOfRangeException();
            }
        }

        public float Distance(float3 a)
        {
            float3 d = new float3(a.x - x, a.y - y, a.z - z);
            return d.Length();
        }

        public float Distance2(float3 a)
        {
            float dx = a.x - x;
            float dy = a.y - y;
            float dz = a.z - z;
            return dx * dx + dy * dy + dz * dz;
        }

        public float Length()
        {
            return (float)Math.Sqrt(x * x + y * y + z * z);
        }

        public float Area(float3 p1, float3 p2)
        {
            float A = Partial(p1);
            A += p1.Partial(p2);
            A += p2.Partial(this);
            return A * 0.5f;
        }

        public float Partial(float3 p)
        {
            return (x * p.y) - (p.x * y);
        }

        // Given a point and a line (defined by two points), compute the closest point
        // in the line.  (The line is treated as infinitely long.)
        public void NearestPointInLine(float3 point, float3 line0, float3 line1)
        {
            float3 nearestPoint = new float3();
            float3 lineDelta = line1 - line0;

            // Handle degenerate lines
            if (lineDelta == float3.Zero)
            {
                nearestPoint = line0;
            }
            else
            {
                float delta = float3.dot(point - line0, lineDelta) / float3.dot(lineDelta, lineDelta);
                nearestPoint = line0 + lineDelta * delta;
            }

            this.x = nearestPoint.x;
            this.y = nearestPoint.y;
            this.z = nearestPoint.z;
        }

        // Given a point and a line segment (defined by two points), compute the closest point
        // in the line.  Cap the point at the endpoints of the line segment.
        public void NearestPointInLineSegment(float3 point, float3 line0, float3 line1)
        {
            float3 nearestPoint = new float3();
            float3 lineDelta = line1 - line0;

            // Handle degenerate lines
            if (lineDelta == Zero)
            {
                nearestPoint = line0;
            }
            else
            {
                float delta = float3.dot(point - line0, lineDelta) / float3.dot(lineDelta, lineDelta);

                // Clamp the point to conform to the segment's endpoints
                if (delta < 0)
                    delta = 0;
                else if (delta > 1)
                    delta = 1;

                nearestPoint = line0 + lineDelta * delta;
            }

            this.x = nearestPoint.x;
            this.y = nearestPoint.y;
            this.z = nearestPoint.z;
        }

        // Given a point and a triangle (defined by three points), compute the closest point
        // in the triangle.  Clamp the point so it's confined to the area of the triangle.
        public void NearestPointInTriangle(float3 point, float3 triangle0, float3 triangle1, float3 triangle2)
        {
            float3 nearestPoint = new float3();

            float3 lineDelta0 = triangle1 - triangle0;
            float3 lineDelta1 = triangle2 - triangle0;

            // Handle degenerate triangles
            if ((lineDelta0 == Zero) || (lineDelta1 == Zero))
            {
                nearestPoint.NearestPointInLineSegment(point, triangle1, triangle2);
            }
            else if (lineDelta0 == lineDelta1)
            {
                nearestPoint.NearestPointInLineSegment(point, triangle0, triangle1);
            }
            else
            {
                float3[] axis = new float3[3] { new float3(), new float3(), new float3() };
                axis[0].NearestPointInLine(triangle0, triangle1, triangle2);
                axis[1].NearestPointInLine(triangle1, triangle0, triangle2);
                axis[2].NearestPointInLine(triangle2, triangle0, triangle1);

                float3 axisDot = new float3();
                axisDot.x = dot(triangle0 - axis[0], point - axis[0]);
                axisDot.y = dot(triangle1 - axis[1], point - axis[1]);
                axisDot.z = dot(triangle2 - axis[2], point - axis[2]);

                bool bForce = true;
                float bestMagnitude2 = 0;
                float closeMagnitude2;
                float3 closePoint = new float3();

                if (axisDot.x < 0f)
                {
                    closePoint.NearestPointInLineSegment(point, triangle1, triangle2);
                    closeMagnitude2 = point.Distance2(closePoint);
                    if (bForce || (bestMagnitude2 > closeMagnitude2))
                    {
                        bForce = false;
                        bestMagnitude2 = closeMagnitude2;
                        nearestPoint = closePoint;
                    }
                }
                if (axisDot.y < 0f)
                {
                    closePoint.NearestPointInLineSegment(point, triangle0, triangle2);
                    closeMagnitude2 = point.Distance2(closePoint);
                    if (bForce || (bestMagnitude2 > closeMagnitude2))
                    {
                        bForce = false;
                        bestMagnitude2 = closeMagnitude2;
                        nearestPoint = closePoint;
                    }
                }
                if (axisDot.z < 0f)
                {
                    closePoint.NearestPointInLineSegment(point, triangle0, triangle1);
                    closeMagnitude2 = point.Distance2(closePoint);
                    if (bForce || (bestMagnitude2 > closeMagnitude2))
                    {
                        bForce = false;
                        bestMagnitude2 = closeMagnitude2;
                        nearestPoint = closePoint;
                    }
                }

                // If bForce is true at this point, it means the nearest point lies
                // inside the triangle; use the nearest-point-on-a-plane equation
                if (bForce)
                {
                    float3 normal;

                    // Get the normal of the polygon (doesn't have to be a unit vector)
                    normal = float3.cross(lineDelta0, lineDelta1);

                    float3 pointDelta = point - triangle0;
                    float delta = float3.dot(normal, pointDelta) / float3.dot(normal, normal);

                    nearestPoint = point - normal * delta;
                }
            }

            this.x = nearestPoint.x;
            this.y = nearestPoint.y;
            this.z = nearestPoint.z;
        }

        public static float3 operator +(float3 a, float3 b)
        {
            return new float3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static float3 operator -(float3 a, float3 b)
        {
            return new float3(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        public static float3 operator -(float3 a, float s)
        {
            return new float3(a.x - s, a.y - s, a.z - s);
        }

        public static float3 operator -(float3 v)
        {
            return new float3(-v.x, -v.y, -v.z);
        }

        public static float3 operator *(float3 v, float s)
        {
            return new float3(v.x * s, v.y * s, v.z * s);
        }

        public static float3 operator *(float s, float3 v)
        {
            return new float3(v.x * s, v.y * s, v.z * s);
        }

        public static float3 operator *(float3 v, float3x3 m)
        {
            return new float3((m.x.x * v.x + m.y.x * v.y + m.z.x * v.z), (m.x.y * v.x + m.y.y * v.y + m.z.y * v.z), (m.x.z * v.x + m.y.z * v.y + m.z.z * v.z));
        }

        public static float3 operator *(float3x3 m, float3 v)
        {
            return new float3(dot(m.x, v), dot(m.y, v), dot(m.z, v));
        }

        public static float3 operator /(float3 v, float s)
        {
            float sinv = 1.0f / s;
            return new float3(v.x * sinv, v.y * sinv, v.z * sinv);
        }

        public bool Equals(float3 other)
        {
            return this == other;
        }

        public override bool Equals(object obj)
        {
            float3 f = obj as float3;
            if (f == null)
                return false;

            return this == f;
        }

        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode();
        }

        public static bool operator ==(float3 a, float3 b)
        {
            // If both are null, or both are same instance, return true.
            if (System.Object.ReferenceEquals(a, b))
                return true;
            // If one is null, but not both, return false.
            if (((object)a == null) || ((object)b == null))
                return false;

            return (a.x == b.x && a.y == b.y && a.z == b.z);
        }

        public static bool operator !=(float3 a, float3 b)
        {
            return (a.x != b.x || a.y != b.y || a.z != b.z);
        }

        public static float dot(float3 a, float3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        public static float3 cmul(float3 v1, float3 v2)
        {
            return new float3(v1.x * v2.x, v1.y * v2.y, v1.z * v2.z);
        }

        public static float3 cross(float3 a, float3 b)
        {
            return new float3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        }

        public static float3 Interpolate(float3 v0, float3 v1, float alpha)
        {
            return v0 * (1 - alpha) + v1 * alpha;
        }

        public static float3 Round(float3 a, int digits)
        {
            return new float3((float)Math.Round(a.x, digits), (float)Math.Round(a.y, digits), (float)Math.Round(a.z, digits));
        }

        public static float3 VectorMax(float3 a, float3 b)
        {
            return new float3(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z));
        }

        public static float3 VectorMin(float3 a, float3 b)
        {
            return new float3(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
        }

        public static float3 vabs(float3 v)
        {
            return new float3(Math.Abs(v.x), Math.Abs(v.y), Math.Abs(v.z));
        }

        public static float magnitude(float3 v)
        {
            return (float)Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        }

        public static float3 normalize(float3 v)
        {
            float d = magnitude(v);
            if (d == 0)
                d = 0.1f;
            d = 1 / d;
            return new float3(v.x * d, v.y * d, v.z * d);
        }

        public static float3 safenormalize(float3 v)
        {
            if (magnitude(v) <= 0.0f)
                return new float3(1, 0, 0);
            else
                return normalize(v);
        }

        public static float Yaw(float3 v)
        {
            return (v.y == 0.0 && v.x == 0.0) ? 0.0f : (float)Math.Atan2(-v.x, v.y) * (180.0f / 3.14159264f);
        }

        public static float Pitch(float3 v)
        {
            return (float)Math.Atan2(v.z, Math.Sqrt(v.x * v.x + v.y * v.y)) * (180.0f / 3.14159264f);
        }

        public float ComputePlane(float3 A, float3 B, float3 C)
        {
            float vx, vy, vz, wx, wy, wz, vw_x, vw_y, vw_z, mag;

            vx = (B.x - C.x);
            vy = (B.y - C.y);
            vz = (B.z - C.z);

            wx = (A.x - B.x);
            wy = (A.y - B.y);
            wz = (A.z - B.z);

            vw_x = vy * wz - vz * wy;
            vw_y = vz * wx - vx * wz;
            vw_z = vx * wy - vy * wx;

            mag = (float)Math.Sqrt((vw_x * vw_x) + (vw_y * vw_y) + (vw_z * vw_z));

            if (mag < 0.000001f)
            {
                mag = 0;
            }
            else
            {
                mag = 1.0f / mag;
            }

            x = vw_x * mag;
            y = vw_y * mag;
            z = vw_z * mag;

            float D = 0.0f - ((x * A.x) + (y * A.y) + (z * A.z));
            return D;
        }

        public override string ToString()
        {
            return String.Format("<{0}, {1}, {2}>", x, y, z);
        }

        public static readonly float3 Zero = new float3();
    }
}
