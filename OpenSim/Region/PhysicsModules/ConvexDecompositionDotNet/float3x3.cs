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
    public class float3x3
    {
        public float3 x = new float3();
        public float3 y = new float3();
        public float3 z = new float3();

        public float3x3()
        {
        }

        public float3x3(float xx, float xy, float xz, float yx, float yy, float yz, float zx, float zy, float zz)
        {
            x = new float3(xx, xy, xz);
            y = new float3(yx, yy, yz);
            z = new float3(zx, zy, zz);
        }

        public float3x3(float3 _x, float3 _y, float3 _z)
        {
            x = new float3(_x);
            y = new float3(_y);
            z = new float3(_z);
        }

        public float3 this[int i]
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

        public float this[int i, int j]
        {
            get
            {
                switch (i)
                {
                    case 0:
                        switch (j)
                        {
                            case 0: return x.x;
                            case 1: return x.y;
                            case 2: return x.z;
                        }
                        break;
                    case 1:
                        switch (j)
                        {
                            case 0: return y.x;
                            case 1: return y.y;
                            case 2: return y.z;
                        }
                        break;
                    case 2:
                        switch (j)
                        {
                            case 0: return z.x;
                            case 1: return z.y;
                            case 2: return z.z;
                        }
                        break;
                }
                throw new ArgumentOutOfRangeException();
            }
            set
            {
                switch (i)
                {
                    case 0:
                        switch (j)
                        {
                            case 0: x.x = value; return;
                            case 1: x.y = value; return;
                            case 2: x.z = value; return;
                        }
                        break;
                    case 1:
                        switch (j)
                        {
                            case 0: y.x = value; return;
                            case 1: y.y = value; return;
                            case 2: y.z = value; return;
                        }
                        break;
                    case 2:
                        switch (j)
                        {
                            case 0: z.x = value; return;
                            case 1: z.y = value; return;
                            case 2: z.z = value; return;
                        }
                        break;
                }
                throw new ArgumentOutOfRangeException();
            }
        }

        public static float3x3 Transpose(float3x3 m)
        {
            return new float3x3(new float3(m.x.x, m.y.x, m.z.x), new float3(m.x.y, m.y.y, m.z.y), new float3(m.x.z, m.y.z, m.z.z));
        }

        public static float3x3 operator *(float3x3 a, float3x3 b)
        {
            return new float3x3(a.x * b, a.y * b, a.z * b);
        }

        public static float3x3 operator *(float3x3 a, float s)
        {
            return new float3x3(a.x * s, a.y * s, a.z * s);
        }

        public static float3x3 operator /(float3x3 a, float s)
        {
            float t = 1f / s;
            return new float3x3(a.x * t, a.y * t, a.z * t);
        }

        public static float3x3 operator +(float3x3 a, float3x3 b)
        {
            return new float3x3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static float3x3 operator -(float3x3 a, float3x3 b)
        {
            return new float3x3(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        public static float Determinant(float3x3 m)
        {
            return m.x.x * m.y.y * m.z.z + m.y.x * m.z.y * m.x.z + m.z.x * m.x.y * m.y.z - m.x.x * m.z.y * m.y.z - m.y.x * m.x.y * m.z.z - m.z.x * m.y.y * m.x.z;
        }

        public static float3x3 Inverse(float3x3 a)
        {
            float3x3 b = new float3x3();
            float d = Determinant(a);
            Debug.Assert(d != 0);
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    int i1 = (i + 1) % 3;
                    int i2 = (i + 2) % 3;
                    int j1 = (j + 1) % 3;
                    int j2 = (j + 2) % 3;

                    // reverse indexs i&j to take transpose
                    b[i, j] = (a[i1][j1] * a[i2][j2] - a[i1][j2] * a[i2][j1]) / d;
                }
            }
            return b;
        }
    }
}
