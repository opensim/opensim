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
    public class Quaternion : float4
    {
        public Quaternion()
        {
            x = y = z = 0.0f;
            w = 1.0f;
        }

        public Quaternion(float3 v, float t)
        {
            v = float3.normalize(v);
            w = (float)Math.Cos(t / 2.0f);
            v = v * (float)Math.Sin(t / 2.0f);
            x = v.x;
            y = v.y;
            z = v.z;
        }

        public Quaternion(float _x, float _y, float _z, float _w)
        {
            x = _x;
            y = _y;
            z = _z;
            w = _w;
        }

        public float angle()
        {
            return (float)Math.Acos(w) * 2.0f;
        }

        public float3 axis()
        {
            float3 a = new float3(x, y, z);
            if (Math.Abs(angle()) < 0.0000001f)
                return new float3(1f, 0f, 0f);
            return a * (1 / (float)Math.Sin(angle() / 2.0f));
        }

        public float3 xdir()
        {
            return new float3(1 - 2 * (y * y + z * z), 2 * (x * y + w * z), 2 * (x * z - w * y));
        }

        public float3 ydir()
        {
            return new float3(2 * (x * y - w * z), 1 - 2 * (x * x + z * z), 2 * (y * z + w * x));
        }

        public float3 zdir()
        {
            return new float3(2 * (x * z + w * y), 2 * (y * z - w * x), 1 - 2 * (x * x + y * y));
        }

        public float3x3 getmatrix()
        {
            return new float3x3(xdir(), ydir(), zdir());
        }

        public static implicit operator float3x3(Quaternion q)
        {
            return q.getmatrix();
        }

        public static Quaternion operator *(Quaternion a, Quaternion b)
        {
            Quaternion c = new Quaternion();
            c.w = a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z;
            c.x = a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y;
            c.y = a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x;
            c.z = a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w;
            return c;
        }

        public static float3 operator *(Quaternion q, float3 v)
        {
            // The following is equivalent to:
            //return (q.getmatrix() * v);
            float qx2 = q.x * q.x;
            float qy2 = q.y * q.y;
            float qz2 = q.z * q.z;

            float qxqy = q.x * q.y;
            float qxqz = q.x * q.z;
            float qxqw = q.x * q.w;
            float qyqz = q.y * q.z;
            float qyqw = q.y * q.w;
            float qzqw = q.z * q.w;
            return new float3((1 - 2 * (qy2 + qz2)) * v.x + (2 * (qxqy - qzqw)) * v.y + (2 * (qxqz + qyqw)) * v.z, (2 * (qxqy + qzqw)) * v.x + (1 - 2 * (qx2 + qz2)) * v.y + (2 * (qyqz - qxqw)) * v.z, (2 * (qxqz - qyqw)) * v.x + (2 * (qyqz + qxqw)) * v.y + (1 - 2 * (qx2 + qy2)) * v.z);
        }

        public static Quaternion operator +(Quaternion a, Quaternion b)
        {
            return new Quaternion(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
        }

        public static Quaternion operator *(Quaternion a, float b)
        {
            return new Quaternion(a.x *b, a.y *b, a.z *b, a.w *b);
        }

        public static Quaternion normalize(Quaternion a)
        {
            float m = (float)Math.Sqrt(a.w * a.w + a.x * a.x + a.y * a.y + a.z * a.z);
            if (m < 0.000000001f)
            {
                a.w = 1;
                a.x = a.y = a.z = 0;
                return a;
            }
            return a * (1f / m);
        }

        public static float dot(Quaternion a, Quaternion b)
        {
            return (a.w * b.w + a.x * b.x + a.y * b.y + a.z * b.z);
        }

        public static Quaternion slerp(Quaternion a, Quaternion b, float interp)
        {
            if (dot(a, b) < 0.0)
            {
                a.w = -a.w;
                a.x = -a.x;
                a.y = -a.y;
                a.z = -a.z;
            }
            float d = dot(a, b);
            if (d >= 1.0)
            {
                return a;
            }
            float theta = (float)Math.Acos(d);
            if (theta == 0.0f)
            {
                return (a);
            }
            return a * ((float)Math.Sin(theta - interp * theta) / (float)Math.Sin(theta)) + b * ((float)Math.Sin(interp * theta) / (float)Math.Sin(theta));
        }

        public static Quaternion Interpolate(Quaternion q0, Quaternion q1, float alpha)
        {
            return slerp(q0, q1, alpha);
        }

        public static Quaternion Inverse(Quaternion q)
        {
            return new Quaternion(-q.x, -q.y, -q.z, q.w);
        }

        public static Quaternion YawPitchRoll(float yaw, float pitch, float roll)
        {
            roll *= (3.14159264f / 180.0f);
            yaw *= (3.14159264f / 180.0f);
            pitch *= (3.14159264f / 180.0f);
            return new Quaternion(new float3(0.0f, 0.0f, 1.0f), yaw) * new Quaternion(new float3(1.0f, 0.0f, 0.0f), pitch) * new Quaternion(new float3(0.0f, 1.0f, 0.0f), roll);
        }

        public static float Yaw(Quaternion q)
        {
            float3 v = q.ydir();
            return (v.y == 0.0 && v.x == 0.0) ? 0.0f : (float)Math.Atan2(-v.x, v.y) * (180.0f / 3.14159264f);
        }

        public static float Pitch(Quaternion q)
        {
            float3 v = q.ydir();
            return (float)Math.Atan2(v.z, Math.Sqrt(v.x * v.x + v.y * v.y)) * (180.0f / 3.14159264f);
        }

        public static float Roll(Quaternion q)
        {
            q = new Quaternion(new float3(0.0f, 0.0f, 1.0f), -Yaw(q) * (3.14159264f / 180.0f)) * q;
            q = new Quaternion(new float3(1.0f, 0.0f, 0.0f), -Pitch(q) * (3.14159264f / 180.0f)) * q;
            return (float)Math.Atan2(-q.xdir().z, q.xdir().x) * (180.0f / 3.14159264f);
        }
    }
}
