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
using System.Linq;
using System.Text;

namespace OpenSim.Region.PhysicsModules.ConvexDecompositionDotNet
{
    public class float4x4
    {
        public float4 x = new float4();
        public float4 y = new float4();
        public float4 z = new float4();
        public float4 w = new float4();

        public float4x4()
        {
        }

        public float4x4(float4 _x, float4 _y, float4 _z, float4 _w)
        {
            x = new float4(_x);
            y = new float4(_y);
            z = new float4(_z);
            w = new float4(_w);
        }

        public float4x4(
            float m00, float m01, float m02, float m03,
            float m10, float m11, float m12, float m13,
            float m20, float m21, float m22, float m23,
            float m30, float m31, float m32, float m33)
        {
            x = new float4(m00, m01, m02, m03);
            y = new float4(m10, m11, m12, m13);
            z = new float4(m20, m21, m22, m23);
            w = new float4(m30, m31, m32, m33);
        }

        public float4x4(float4x4 m)
        {
            x = new float4(m.x);
            y = new float4(m.y);
            z = new float4(m.z);
            w = new float4(m.w);
        }

        public float4 this[int i]
        {
            get
            {
                switch (i)
                {
                    case 0: return x;
                    case 1: return y;
                    case 2: return z;
                    case 3: return w;
                }
                throw new ArgumentOutOfRangeException();
            }
            set
            {
                switch (i)
                {
                    case 0: x = value; return;
                    case 1: y = value; return;
                    case 2: z = value; return;
                    case 3: w = value; return;
                }
                throw new ArgumentOutOfRangeException();
            }
        }

        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode() ^ w.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            float4x4 m = obj as float4x4;
            if (m == null)
                return false;

            return this == m;
        }

        public static float4x4 operator *(float4x4 a, float4x4 b)
        {
            return new float4x4(a.x * b, a.y * b, a.z * b, a.w * b);
        }

        public static bool operator ==(float4x4 a, float4x4 b)
        {
            return (a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w);
        }

        public static bool operator !=(float4x4 a, float4x4 b)
        {
            return !(a == b);
        }

        public static float4x4 Inverse(float4x4 m)
        {
            float4x4 d = new float4x4();
            //float dst = d.x.x;
            float[] tmp = new float[12]; // temp array for pairs
            float[] src = new float[16]; // array of transpose source matrix
            float det; // determinant
            // transpose matrix
            for (int i = 0; i < 4; i++)
            {
                src[i] = m[i].x;
                src[i + 4] = m[i].y;
                src[i + 8] = m[i].z;
                src[i + 12] = m[i].w;
            }
            // calculate pairs for first 8 elements (cofactors)
            tmp[0] = src[10] * src[15];
            tmp[1] = src[11] * src[14];
            tmp[2] = src[9] * src[15];
            tmp[3] = src[11] * src[13];
            tmp[4] = src[9] * src[14];
            tmp[5] = src[10] * src[13];
            tmp[6] = src[8] * src[15];
            tmp[7] = src[11] * src[12];
            tmp[8] = src[8] * src[14];
            tmp[9] = src[10] * src[12];
            tmp[10] = src[8] * src[13];
            tmp[11] = src[9] * src[12];
            // calculate first 8 elements (cofactors)
            d.x.x = tmp[0]*src[5] + tmp[3]*src[6] + tmp[4]*src[7];
            d.x.x -= tmp[1]*src[5] + tmp[2]*src[6] + tmp[5]*src[7];
            d.x.y = tmp[1]*src[4] + tmp[6]*src[6] + tmp[9]*src[7];
            d.x.y -= tmp[0]*src[4] + tmp[7]*src[6] + tmp[8]*src[7];
            d.x.z = tmp[2]*src[4] + tmp[7]*src[5] + tmp[10]*src[7];
            d.x.z -= tmp[3]*src[4] + tmp[6]*src[5] + tmp[11]*src[7];
            d.x.w = tmp[5]*src[4] + tmp[8]*src[5] + tmp[11]*src[6];
            d.x.w -= tmp[4]*src[4] + tmp[9]*src[5] + tmp[10]*src[6];
            d.y.x = tmp[1]*src[1] + tmp[2]*src[2] + tmp[5]*src[3];
            d.y.x -= tmp[0]*src[1] + tmp[3]*src[2] + tmp[4]*src[3];
            d.y.y = tmp[0]*src[0] + tmp[7]*src[2] + tmp[8]*src[3];
            d.y.y -= tmp[1]*src[0] + tmp[6]*src[2] + tmp[9]*src[3];
            d.y.z = tmp[3]*src[0] + tmp[6]*src[1] + tmp[11]*src[3];
            d.y.z -= tmp[2]*src[0] + tmp[7]*src[1] + tmp[10]*src[3];
            d.y.w = tmp[4]*src[0] + tmp[9]*src[1] + tmp[10]*src[2];
            d.y.w -= tmp[5]*src[0] + tmp[8]*src[1] + tmp[11]*src[2];
            // calculate pairs for second 8 elements (cofactors)
            tmp[0] = src[2]*src[7];
            tmp[1] = src[3]*src[6];
            tmp[2] = src[1]*src[7];
            tmp[3] = src[3]*src[5];
            tmp[4] = src[1]*src[6];
            tmp[5] = src[2]*src[5];
            tmp[6] = src[0]*src[7];
            tmp[7] = src[3]*src[4];
            tmp[8] = src[0]*src[6];
            tmp[9] = src[2]*src[4];
            tmp[10] = src[0]*src[5];
            tmp[11] = src[1]*src[4];
            // calculate second 8 elements (cofactors)
            d.z.x = tmp[0]*src[13] + tmp[3]*src[14] + tmp[4]*src[15];
            d.z.x -= tmp[1]*src[13] + tmp[2]*src[14] + tmp[5]*src[15];
            d.z.y = tmp[1]*src[12] + tmp[6]*src[14] + tmp[9]*src[15];
            d.z.y -= tmp[0]*src[12] + tmp[7]*src[14] + tmp[8]*src[15];
            d.z.z = tmp[2]*src[12] + tmp[7]*src[13] + tmp[10]*src[15];
            d.z.z -= tmp[3]*src[12] + tmp[6]*src[13] + tmp[11]*src[15];
            d.z.w = tmp[5]*src[12] + tmp[8]*src[13] + tmp[11]*src[14];
            d.z.w-= tmp[4]*src[12] + tmp[9]*src[13] + tmp[10]*src[14];
            d.w.x = tmp[2]*src[10] + tmp[5]*src[11] + tmp[1]*src[9];
            d.w.x-= tmp[4]*src[11] + tmp[0]*src[9] + tmp[3]*src[10];
            d.w.y = tmp[8]*src[11] + tmp[0]*src[8] + tmp[7]*src[10];
            d.w.y-= tmp[6]*src[10] + tmp[9]*src[11] + tmp[1]*src[8];
            d.w.z = tmp[6]*src[9] + tmp[11]*src[11] + tmp[3]*src[8];
            d.w.z-= tmp[10]*src[11] + tmp[2]*src[8] + tmp[7]*src[9];
            d.w.w = tmp[10]*src[10] + tmp[4]*src[8] + tmp[9]*src[9];
            d.w.w-= tmp[8]*src[9] + tmp[11]*src[10] + tmp[5]*src[8];
            // calculate determinant
            det = src[0] * d.x.x + src[1] * d.x.y + src[2] * d.x.z + src[3] * d.x.w;
            // calculate matrix inverse
            det = 1/det;
            for (int j = 0; j < 4; j++)
                d[j] *= det;
            return d;
        }

        public static float4x4 MatrixRigidInverse(float4x4 m)
        {
            float4x4 trans_inverse = MatrixTranslation(-m.w.xyz());
            float4x4 rot = new float4x4(m);
            rot.w = new float4(0f, 0f, 0f, 1f);
            return trans_inverse * MatrixTranspose(rot);
        }
        public static float4x4 MatrixTranspose(float4x4 m)
        {
            return new float4x4(m.x.x, m.y.x, m.z.x, m.w.x, m.x.y, m.y.y, m.z.y, m.w.y, m.x.z, m.y.z, m.z.z, m.w.z, m.x.w, m.y.w, m.z.w, m.w.w);
        }
        public static float4x4 MatrixPerspectiveFov(float fovy, float aspect, float zn, float zf)
        {
            float h = 1.0f / (float)Math.Tan(fovy / 2.0f); // view space height
            float w = h / aspect; // view space width
            return new float4x4(w, 0, 0, 0, 0, h, 0, 0, 0, 0, zf / (zn - zf), -1, 0, 0, zn * zf / (zn - zf), 0);
        }
        public static float4x4 MatrixTranslation(float3 t)
        {
            return new float4x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, t.x, t.y, t.z, 1);
        }
        public static float4x4 MatrixRotationZ(float angle_radians)
        {
            float s = (float)Math.Sin(angle_radians);
            float c = (float)Math.Cos(angle_radians);
            return new float4x4(c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
        }
        public static float4x4 MatrixLookAt(float3 eye, float3 at, float3 up)
        {
            float4x4 m = new float4x4();
            m.w.w = 1.0f;
            m.w.setxyz(eye);
            m.z.setxyz(float3.normalize(eye - at));
            m.x.setxyz(float3.normalize(float3.cross(up, m.z.xyz())));
            m.y.setxyz(float3.cross(m.z.xyz(), m.x.xyz()));
            return MatrixRigidInverse(m);
        }

        public static float4x4 MatrixFromQuatVec(Quaternion q, float3 v)
        {
            // builds a 4x4 transformation matrix based on orientation q and translation v
            float qx2 = q.x * q.x;
            float qy2 = q.y * q.y;
            float qz2 = q.z * q.z;

            float qxqy = q.x * q.y;
            float qxqz = q.x * q.z;
            float qxqw = q.x * q.w;
            float qyqz = q.y * q.z;
            float qyqw = q.y * q.w;
            float qzqw = q.z * q.w;

            return new float4x4(
                1 - 2 * (qy2 + qz2),
                2 * (qxqy + qzqw),
                2 * (qxqz - qyqw),
                0,
                2 * (qxqy - qzqw),
                1 - 2 * (qx2 + qz2),
                2 * (qyqz + qxqw),
                0,
                2 * (qxqz + qyqw),
                2 * (qyqz - qxqw),
                1 - 2 * (qx2 + qy2),
                0,
                v.x,
                v.y,
                v.z,
                1.0f);
        }
    }
}
