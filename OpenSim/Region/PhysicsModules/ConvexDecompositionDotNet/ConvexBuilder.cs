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
    public class DecompDesc
    {
        public List<float3> mVertices;
        public List<int> mIndices;

        // options
        public uint mDepth; // depth to split, a maximum of 10, generally not over 7.
        public float mCpercent; // the concavity threshold percentage. 0=20 is reasonable.
        public float mPpercent; // the percentage volume conservation threshold to collapse hulls. 0-30 is reasonable.

        // hull output limits.
        public uint mMaxVertices; // maximum number of vertices in the output hull. Recommended 32 or less.
        public float mSkinWidth; // a skin width to apply to the output hulls.

        public ConvexDecompositionCallback mCallback; // the interface to receive back the results.

        public DecompDesc()
        {
            mDepth = 5;
            mCpercent = 5;
            mPpercent = 5;
            mMaxVertices = 32;
        }
    }

    public class CHull
    {
        public float[] mMin = new float[3];
        public float[] mMax = new float[3];
        public float mVolume;
        public float mDiagonal;
        public ConvexResult mResult;

        public CHull(ConvexResult result)
        {
            mResult = new ConvexResult(result);
            mVolume = Concavity.computeMeshVolume(result.HullVertices, result.HullIndices);

            mDiagonal = getBoundingRegion(result.HullVertices, mMin, mMax);

            float dx = mMax[0] - mMin[0];
            float dy = mMax[1] - mMin[1];
            float dz = mMax[2] - mMin[2];

            dx *= 0.1f; // inflate 1/10th on each edge
            dy *= 0.1f; // inflate 1/10th on each edge
            dz *= 0.1f; // inflate 1/10th on each edge

            mMin[0] -= dx;
            mMin[1] -= dy;
            mMin[2] -= dz;

            mMax[0] += dx;
            mMax[1] += dy;
            mMax[2] += dz;
        }

        public void Dispose()
        {
            mResult = null;
        }

        public bool overlap(CHull h)
        {
            return overlapAABB(mMin, mMax, h.mMin, h.mMax);
        }

        // returns the d1Giagonal distance
        private static float getBoundingRegion(List<float3> points, float[] bmin, float[] bmax)
        {
            float3 first = points[0];

            bmin[0] = first.x;
            bmin[1] = first.y;
            bmin[2] = first.z;

            bmax[0] = first.x;
            bmax[1] = first.y;
            bmax[2] = first.z;

            for (int i = 1; i < points.Count; i++)
            {
                float3 p = points[i];

                if (p[0] < bmin[0]) bmin[0] = p[0];
                if (p[1] < bmin[1]) bmin[1] = p[1];
                if (p[2] < bmin[2]) bmin[2] = p[2];

                if (p[0] > bmax[0]) bmax[0] = p[0];
                if (p[1] > bmax[1]) bmax[1] = p[1];
                if (p[2] > bmax[2]) bmax[2] = p[2];
            }

            float dx = bmax[0] - bmin[0];
            float dy = bmax[1] - bmin[1];
            float dz = bmax[2] - bmin[2];

            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // return true if the two AABB's overlap.
        private static bool overlapAABB(float[] bmin1, float[] bmax1, float[] bmin2, float[] bmax2)
        {
            if (bmax2[0] < bmin1[0]) return false; // if the maximum is less than our minimum on any axis
            if (bmax2[1] < bmin1[1]) return false;
            if (bmax2[2] < bmin1[2]) return false;

            if (bmin2[0] > bmax1[0]) return false; // if the minimum is greater than our maximum on any axis
            if (bmin2[1] > bmax1[1]) return false; // if the minimum is greater than our maximum on any axis
            if (bmin2[2] > bmax1[2]) return false; // if the minimum is greater than our maximum on any axis

            return true; // the extents overlap
        }
    }

    public class ConvexBuilder
    {
        public List<CHull> mChulls = new List<CHull>();
        private ConvexDecompositionCallback mCallback;

        private int MAXDEPTH = 8;
        private float CONCAVE_PERCENT = 1f;
        private float MERGE_PERCENT = 2f;

        public ConvexBuilder(ConvexDecompositionCallback callback)
        {
            mCallback = callback;
        }

        public void Dispose()
        {
            int i;
            for (i = 0; i < mChulls.Count; i++)
            {
                CHull cr = mChulls[i];
                cr.Dispose();
            }
        }

        public bool isDuplicate(uint i1, uint i2, uint i3, uint ci1, uint ci2, uint ci3)
        {
            uint dcount = 0;

            Debug.Assert(i1 != i2 && i1 != i3 && i2 != i3);
            Debug.Assert(ci1 != ci2 && ci1 != ci3 && ci2 != ci3);

            if (i1 == ci1 || i1 == ci2 || i1 == ci3)
                dcount++;
            if (i2 == ci1 || i2 == ci2 || i2 == ci3)
                dcount++;
            if (i3 == ci1 || i3 == ci2 || i3 == ci3)
                dcount++;

            return dcount == 3;
        }

        public void getMesh(ConvexResult cr, VertexPool vc, List<int> indices)
        {
            List<int> src = cr.HullIndices;

            for (int i = 0; i < src.Count / 3; i++)
            {
                int i1 = src[i * 3 + 0];
                int i2 = src[i * 3 + 1];
                int i3 = src[i * 3 + 2];

                float3 p1 = cr.HullVertices[i1];
                float3 p2 = cr.HullVertices[i2];
                float3 p3 = cr.HullVertices[i3];

                i1 = vc.getIndex(p1);
                i2 = vc.getIndex(p2);
                i3 = vc.getIndex(p3);
            }
        }

        public CHull canMerge(CHull a, CHull b)
        {
            if (!a.overlap(b)) // if their AABB's (with a little slop) don't overlap, then return.
                return null;

            CHull ret = null;

            // ok..we are going to combine both meshes into a single mesh
            // and then we are going to compute the concavity...

            VertexPool vc = new VertexPool();

            List<int> indices = new List<int>();

            getMesh(a.mResult, vc, indices);
            getMesh(b.mResult, vc, indices);

            int vcount = vc.GetSize();
            List<float3> vertices = vc.GetVertices();
            int tcount = indices.Count / 3;

            //don't do anything if hull is empty
            if (tcount == 0)
            {
                vc.Clear();
                return null;
            }

            HullResult hresult = new HullResult();
            HullDesc desc = new HullDesc();

            desc.SetHullFlag(HullFlag.QF_TRIANGLES);
            desc.Vertices = vertices;

            HullError hret = HullUtils.CreateConvexHull(desc, ref hresult);

            if (hret == HullError.QE_OK)
            {
                float combineVolume = Concavity.computeMeshVolume(hresult.OutputVertices, hresult.Indices);
                float sumVolume = a.mVolume + b.mVolume;

                float percent = (sumVolume * 100) / combineVolume;
                if (percent >= (100.0f - MERGE_PERCENT))
                {
                    ConvexResult cr = new ConvexResult(hresult.OutputVertices, hresult.Indices);
                    ret = new CHull(cr);
                }
            }

            vc.Clear();
            return ret;
        }

        public bool combineHulls()
        {
            bool combine = false;

            sortChulls(mChulls); // sort the convex hulls, largest volume to least...

            List<CHull> output = new List<CHull>(); // the output hulls...

            int i;
            for (i = 0; i < mChulls.Count && !combine; ++i)
            {
                CHull cr = mChulls[i];

                int j;
                for (j = 0; j < mChulls.Count; j++)
                {
                    CHull match = mChulls[j];

                    if (cr != match) // don't try to merge a hull with itself, that be stoopid
                    {

                        CHull merge = canMerge(cr, match); // if we can merge these two....

                        if (merge != null)
                        {
                            output.Add(merge);

                            ++i;
                            while (i != mChulls.Count)
                            {
                                CHull cr2 = mChulls[i];
                                if (cr2 != match)
                                {
                                    output.Add(cr2);
                                }
                                i++;
                            }

                            cr.Dispose();
                            match.Dispose();
                            combine = true;
                            break;
                        }
                    }
                }

                if (combine)
                {
                    break;
                }
                else
                {
                    output.Add(cr);
                }
            }

            if (combine)
            {
                mChulls.Clear();
                mChulls = output;
                output.Clear();
            }

            return combine;
        }

        public int process(DecompDesc desc)
        {
            int ret = 0;

            MAXDEPTH = (int)desc.mDepth;
            CONCAVE_PERCENT = desc.mCpercent;
            MERGE_PERCENT = desc.mPpercent;

            ConvexDecomposition.calcConvexDecomposition(desc.mVertices, desc.mIndices, ConvexDecompResult, 0f, 0, MAXDEPTH, CONCAVE_PERCENT, MERGE_PERCENT);

            while (combineHulls()) // keep combinging hulls until I can't combine any more...
                ;

            int i;
            for (i = 0; i < mChulls.Count; i++)
            {
                CHull cr = mChulls[i];

                // before we hand it back to the application, we need to regenerate the hull based on the
                // limits given by the user.

                ConvexResult c = cr.mResult; // the high resolution hull...

                HullResult result = new HullResult();
                HullDesc hdesc = new HullDesc();

                hdesc.SetHullFlag(HullFlag.QF_TRIANGLES);

                hdesc.Vertices = c.HullVertices;
                hdesc.MaxVertices = desc.mMaxVertices; // maximum number of vertices allowed in the output

                if (desc.mSkinWidth != 0f)
                {
                    hdesc.SkinWidth = desc.mSkinWidth;
                    hdesc.SetHullFlag(HullFlag.QF_SKIN_WIDTH); // do skin width computation.
                }

                HullError ret2 = HullUtils.CreateConvexHull(hdesc, ref result);

                if (ret2 == HullError.QE_OK)
                {
                    ConvexResult r = new ConvexResult(result.OutputVertices, result.Indices);

                    r.mHullVolume = Concavity.computeMeshVolume(result.OutputVertices, result.Indices); // the volume of the hull.

                    // compute the best fit OBB
                    //computeBestFitOBB(result.mNumOutputVertices, result.mOutputVertices, sizeof(float) * 3, r.mOBBSides, r.mOBBTransform);

                    //r.mOBBVolume = r.mOBBSides[0] * r.mOBBSides[1] * r.mOBBSides[2]; // compute the OBB volume.

                    //fm_getTranslation(r.mOBBTransform, r.mOBBCenter); // get the translation component of the 4x4 matrix.

                    //fm_matrixToQuat(r.mOBBTransform, r.mOBBOrientation); // extract the orientation as a quaternion.

                    //r.mSphereRadius = computeBoundingSphere(result.mNumOutputVertices, result.mOutputVertices, r.mSphereCenter);
                    //r.mSphereVolume = fm_sphereVolume(r.mSphereRadius);

                    mCallback(r);
                }

                result = null;
                cr.Dispose();
            }

            ret = mChulls.Count;

            mChulls.Clear();

            return ret;
        }

        public void ConvexDecompResult(ConvexResult result)
        {
            CHull ch = new CHull(result);
            mChulls.Add(ch);
        }

        public void sortChulls(List<CHull> hulls)
        {
            hulls.Sort(delegate(CHull a, CHull b) { return a.mVolume.CompareTo(b.mVolume); });
        }
    }
}
