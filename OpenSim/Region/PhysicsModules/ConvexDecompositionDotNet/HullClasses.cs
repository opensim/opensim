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
    public class HullResult
    {
        public bool Polygons = true; // true if indices represents polygons, false indices are triangles
        public List<float3> OutputVertices = new List<float3>();
        public List<int> Indices;

        // If triangles, then indices are array indexes into the vertex list.
        // If polygons, indices are in the form (number of points in face) (p1, p2, p3, ..) etc..
    }

    public class PHullResult
    {
        public List<float3> Vertices = new List<float3>();
        public List<int> Indices = new List<int>();
    }

    [Flags]
    public enum HullFlag : int
    {
        QF_DEFAULT = 0,
        QF_TRIANGLES = (1 << 0), // report results as triangles, not polygons.
        QF_SKIN_WIDTH = (1 << 2) // extrude hull based on this skin width
    }

    public enum HullError : int
    {
        QE_OK, // success!
        QE_FAIL // failed.
    }

    public class HullDesc
    {
        public HullFlag Flags; // flags to use when generating the convex hull.
        public List<float3> Vertices;
        public float NormalEpsilon; // the epsilon for removing duplicates. This is a normalized value, if normalized bit is on.
        public float SkinWidth;
        public uint MaxVertices; // maximum number of vertices to be considered for the hull!
        public uint MaxFaces;

        public HullDesc()
        {
            Flags = HullFlag.QF_DEFAULT;
            Vertices = new List<float3>();
            NormalEpsilon = 0.001f;
            MaxVertices = 4096;
            MaxFaces = 4096;
            SkinWidth = 0.01f;
        }

        public HullDesc(HullFlag flags, List<float3> vertices)
        {
            Flags = flags;
            Vertices = new List<float3>(vertices);
            NormalEpsilon = 0.001f;
            MaxVertices = 4096;
            MaxFaces = 4096;
            SkinWidth = 0.01f;
        }

        public bool HasHullFlag(HullFlag flag)
        {
            return (Flags & flag) != 0;
        }

        public void SetHullFlag(HullFlag flag)
        {
            Flags |= flag;
        }

        public void ClearHullFlag(HullFlag flag)
        {
            Flags &= ~flag;
        }
    }

    public class ConvexH
    {
        public struct HalfEdge
        {
            public short ea; // the other half of the edge (index into edges list)
            public byte v; // the vertex at the start of this edge (index into vertices list)
            public byte p; // the facet on which this edge lies (index into facets list)

            public HalfEdge(short _ea, byte _v, byte _p)
            {
                ea = _ea;
                v = _v;
                p = _p;
            }

            public HalfEdge(HalfEdge e)
            {
                ea = e.ea;
                v = e.v;
                p = e.p;
            }
        }

        public List<float3> vertices = new List<float3>();
        public List<HalfEdge> edges = new List<HalfEdge>();
        public List<Plane> facets = new List<Plane>();

        public ConvexH(int vertices_size, int edges_size, int facets_size)
        {
            vertices = new List<float3>(vertices_size);
            edges = new List<HalfEdge>(edges_size);
            facets = new List<Plane>(facets_size);
        }
    }

    public class VertFlag
    {
        public byte planetest;
        public byte junk;
        public byte undermap;
        public byte overmap;
    }

    public class EdgeFlag
    {
        public byte planetest;
        public byte fixes;
        public short undermap;
        public short overmap;
    }

    public class PlaneFlag
    {
        public byte undermap;
        public byte overmap;
    }

    public class Coplanar
    {
        public ushort ea;
        public byte v0;
        public byte v1;
    }
}
