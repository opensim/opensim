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
    public class HullTriangle : int3
    {
        public int3 n = new int3();
        public int id;
        public int vmax;
        public float rise;
        private List<HullTriangle> tris;

        public HullTriangle(int a, int b, int c, List<HullTriangle> tris)
            : base(a, b, c)
        {
            this.tris = tris;

            n = new int3(-1, -1, -1);
            id = tris.Count;
            tris.Add(this);
            vmax = -1;
            rise = 0.0f;
        }

        public void Dispose()
        {
            Debug.Assert(tris[id] == this);
            tris[id] = null;
        }

        public int neib(int a, int b)
        {
            int i;

            for (i = 0; i < 3; i++)
            {
                int i1 = (i + 1) % 3;
                int i2 = (i + 2) % 3;
                if ((this)[i] == a && (this)[i1] == b)
                    return n[i2];
                if ((this)[i] == b && (this)[i1] == a)
                    return n[i2];
            }

            Debug.Assert(false);
            return -1;
        }

        public void setneib(int a, int b, int value)
        {
            int i;

            for (i = 0; i < 3; i++)
            {
                int i1 = (i + 1) % 3;
                int i2 = (i + 2) % 3;
                if ((this)[i] == a && (this)[i1] == b)
                {
                    n[i2] = value;
                    return;
                }
                if ((this)[i] == b && (this)[i1] == a)
                {
                    n[i2] = value;
                    return;
                }
            }
        }
    }
}
