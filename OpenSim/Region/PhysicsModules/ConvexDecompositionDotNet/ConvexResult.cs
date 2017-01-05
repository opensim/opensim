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
    public class ConvexResult
    {
        public List<float3> HullVertices;
        public List<int> HullIndices;

        public float mHullVolume; // the volume of the convex hull.

        //public float[] OBBSides = new float[3]; // the width, height and breadth of the best fit OBB
        //public float[] OBBCenter = new float[3]; // the center of the OBB
        //public float[] OBBOrientation = new float[4]; // the quaternion rotation of the OBB.
        //public float[] OBBTransform = new float[16]; // the 4x4 transform of the OBB.
        //public float OBBVolume; // the volume of the OBB

        //public float SphereRadius; // radius and center of best fit sphere
        //public float[] SphereCenter = new float[3];
        //public float SphereVolume; // volume of the best fit sphere

        public ConvexResult()
        {
            HullVertices = new List<float3>();
            HullIndices = new List<int>();
        }

        public ConvexResult(List<float3> hvertices, List<int> hindices)
        {
            HullVertices = hvertices;
            HullIndices = hindices;
        }

        public ConvexResult(ConvexResult r)
        {
            HullVertices = new List<float3>(r.HullVertices);
            HullIndices = new List<int>(r.HullIndices);
        }

        public void Dispose()
        {
            HullVertices = null;
            HullIndices = null;
        }
    }
}
