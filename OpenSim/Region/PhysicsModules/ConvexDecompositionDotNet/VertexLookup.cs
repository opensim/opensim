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
    public class VertexPool
    {
        private List<float3> mVertices = new List<float3>();
        private Dictionary<float3, int> mIndices = new Dictionary<float3, int>();

        public int getIndex(float3 vtx)
        {
            int idx;
            if (mIndices.TryGetValue(vtx, out idx))
                return idx;

            idx = mVertices.Count;
            mVertices.Add(vtx);
            mIndices.Add(vtx, idx);
            return idx;
        }

        public float3 Get(int idx)
        {
            return mVertices[idx];
        }

        public int GetSize()
        {
            return mVertices.Count;
        }

        public List<float3> GetVertices()
        {
            return mVertices;
        }

        public void Clear()
        {
            mVertices.Clear();
        }
    }
}
