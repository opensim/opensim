/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Region.PhysicsModules.SharedBase
{
    public interface IMesher
    {
        IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod);
        IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical);
        IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical, bool convex, bool forOde);
        IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical, bool shouldCache, bool convex, bool forOde);
        IMesh GetMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical, bool convex);
        void ReleaseMesh(IMesh mesh);
        void ExpireReleaseMeshs();
        void ExpireFileCache();
    }

    // Values for level of detail to be passed to the mesher.
    // Values origionally chosen for the LOD of sculpties (the sqrt(width*heigth) of sculpt texture)
    // Lower level of detail reduces the number of vertices used to represent the meshed shape.
    public enum LevelOfDetail
    {
        High = 32,
        Medium = 16,
        Low = 8,
        VeryLow = 4
    }

    public interface IVertex
    {
    }

    [Serializable()]
    [StructLayout(LayoutKind.Explicit)]
    public struct AMeshKey
    {
        [FieldOffset(0)]
        public UUID uuid;
        [FieldOffset(0)]
        public ulong hashA;
        [FieldOffset(8)]
        public ulong hashB;
        [FieldOffset(16)]
        public ulong hashC;

        public override string ToString()
        {
            return uuid.ToString() + "-" + hashC.ToString("x") ;
        }
    }

    public interface IMesh
    {
        List<Vector3> getVertexList();
        int[] getIndexListAsInt();
        int[] getIndexListAsIntLocked();
        float[] getVertexListAsFloat();
        float[] getVertexListAsFloatLocked();
        void getIndexListAsPtrToIntArray(out IntPtr indices, out int triStride, out int indexCount);
        void getVertexListAsPtrToFloatArray(out IntPtr vertexList, out int vertexStride, out int vertexCount);
        void releaseSourceMeshData();
        void releasePinned();
        void Append(IMesh newMesh);
        void TransformLinear(float[,] matrix, float[] offset);
        Vector3 GetCentroid();
        Vector3 GetOBB();
    }
}
