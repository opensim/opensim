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
    public class int3
    {
        public int x;
        public int y;
        public int z;

        public int3()
        {
        }

        public int3(int _x, int _y, int _z)
        {
            x = _x;
            y = _y;
            z = _z;
        }

        public int this[int i]
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
            set
            {
                switch (i)
                {
                    case 0: x = value; return;
                    case 1: y = value; return;
                    case 2: z = value; return;
                }
                throw new ArgumentOutOfRangeException();
            }
        }

        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            int3 i = obj as int3;
            if (i == null)
                return false;

            return this == i;
        }

        public static bool operator ==(int3 a, int3 b)
        {
            // If both are null, or both are same instance, return true.
            if (System.Object.ReferenceEquals(a, b))
                return true;
            // If one is null, but not both, return false.
            if (((object)a == null) || ((object)b == null))
                return false;

            for (int i = 0; i < 3; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }

        public static bool operator !=(int3 a, int3 b)
        {
            return !(a == b);
        }

        public static int3 roll3(int3 a)
        {
            int tmp = a[0];
            a[0] = a[1];
            a[1] = a[2];
            a[2] = tmp;
            return a;
        }

        public static bool isa(int3 a, int3 b)
        {
            return (a == b || roll3(a) == b || a == roll3(b));
        }

        public static bool b2b(int3 a, int3 b)
        {
            return isa(a, new int3(b[2], b[1], b[0]));
        }
    }
}
