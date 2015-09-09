/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyrightD
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
using System.Text;

using OpenSim.Framework;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenSim.Region.PhysicsModule.Meshing;
using OpenSim.Region.PhysicsModules.ConvexDecompositionDotNet;

using OMV = OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.BulletS
{
// Information class that holds stats for the shape. Which values mean
//     something depends on the type of shape.
// This information is used for debugging and stats and is not used
//     for operational things.
public class ShapeInfoInfo
{
    public int Vertices { get; set; }
    private int m_hullCount;
    private int[] m_verticesPerHull;
    public ShapeInfoInfo()
    {
        Vertices = 0;
        m_hullCount = 0;
        m_verticesPerHull = null;
    }
    public int HullCount
    {
        set
        {
            m_hullCount = value;
            m_verticesPerHull = new int[m_hullCount];
            Array.Clear(m_verticesPerHull, 0, m_hullCount);
        }
        get { return m_hullCount; }
    }
    public void SetVerticesPerHull(int hullNum, int vertices)
    {
        if (m_verticesPerHull != null && hullNum < m_verticesPerHull.Length)
        {
            m_verticesPerHull[hullNum] = vertices;
        }
    }
    public int GetVerticesPerHull(int hullNum)
    {
        if (m_verticesPerHull != null && hullNum < m_verticesPerHull.Length)
        {
            return m_verticesPerHull[hullNum];
        }
        return 0;
    }
    public override string ToString()
    {
        StringBuilder buff = new StringBuilder();
        // buff.Append("ShapeInfo=<");
        buff.Append("<");
        if (Vertices > 0)
        {
            buff.Append("verts=");
            buff.Append(Vertices.ToString());
        }

        if (Vertices > 0 && HullCount > 0) buff.Append(",");

        if (HullCount > 0)
        {
            buff.Append("nHulls=");
            buff.Append(HullCount.ToString());
            buff.Append(",");
            buff.Append("hullVerts=");
            for (int ii = 0; ii < HullCount; ii++)
            {
                if (ii != 0) buff.Append(",");
                buff.Append(GetVerticesPerHull(ii).ToString());
            }
        }
        buff.Append(">");
        return buff.ToString();
    }
}

public abstract class BSShape
{
    private static string LogHeader = "[BULLETSIM SHAPE]";

    public int referenceCount { get; set; }
    public DateTime lastReferenced { get; set; }
    public BulletShape physShapeInfo { get; set; }
    public ShapeInfoInfo shapeInfo { get; private set; }

    public BSShape()
    {
        referenceCount = 1;
        lastReferenced = DateTime.Now;
        physShapeInfo = new BulletShape();
        shapeInfo = new ShapeInfoInfo();
    }
    public BSShape(BulletShape pShape)
    {
        referenceCount = 1;
        lastReferenced = DateTime.Now;
        physShapeInfo = pShape;
        shapeInfo = new ShapeInfoInfo();
    }

    // Get another reference to this shape.
    public abstract BSShape GetReference(BSScene pPhysicsScene, BSPhysObject pPrim);

    // Called when this shape is being used again.
    // Used internally. External callers should call instance.GetReference() to properly copy/reference
    //       the shape.
    protected virtual void IncrementReference()
    {
        referenceCount++;
        lastReferenced = DateTime.Now;
    }

    // Called when this shape is done being used.
    protected virtual void DecrementReference()
    {
        referenceCount--;
        lastReferenced = DateTime.Now;
    }

    // Release the use of a physical shape.
    public abstract void Dereference(BSScene physicsScene);

    // Return 'true' if there is an allocated physics physical shape under this class instance.
    public virtual bool HasPhysicalShape
    {
        get
        {
            if (physShapeInfo != null)
                return physShapeInfo.HasPhysicalShape;
            return false;
        }
    }
    public virtual BSPhysicsShapeType ShapeType
    {
        get
        {
            BSPhysicsShapeType ret = BSPhysicsShapeType.SHAPE_UNKNOWN;
            if (physShapeInfo != null && physShapeInfo.HasPhysicalShape)
                ret = physShapeInfo.shapeType;
            return ret;
        }
    }

    // Returns a string for debugging that uniquily identifies the memory used by this instance
    public virtual string AddrString
    {
        get
        {
            if (physShapeInfo != null)
                return physShapeInfo.AddrString;
            return "unknown";
        }
    }

    public override string ToString()
    {
        StringBuilder buff = new StringBuilder();
        if (physShapeInfo == null)
        {
            buff.Append("<noPhys");
        }
        else
        {
            buff.Append("<phy=");
            buff.Append(physShapeInfo.ToString());
        }
        buff.Append(",c=");
        buff.Append(referenceCount.ToString());
        buff.Append(">");
        return buff.ToString();
    }

    #region Common shape routines
    // Create a hash of all the shape parameters to be used as a key for this particular shape.
    public static System.UInt64 ComputeShapeKey(OMV.Vector3 size, PrimitiveBaseShape pbs, out float retLod)
    {
        // level of detail based on size and type of the object
        float lod = BSParam.MeshLOD;
        if (pbs.SculptEntry)
            lod = BSParam.SculptLOD;

        // Mega prims usually get more detail because one can interact with shape approximations at this size.
        float maxAxis = Math.Max(size.X, Math.Max(size.Y, size.Z));
        if (maxAxis > BSParam.MeshMegaPrimThreshold)
            lod = BSParam.MeshMegaPrimLOD;

        retLod = lod;
        return pbs.GetMeshKey(size, lod);
    }

    // The creation of a mesh or hull can fail if an underlying asset is not available.
    // There are two cases: 1) the asset is not in the cache and it needs to be fetched;
    //     and 2) the asset cannot be converted (like failed decompression of JPEG2000s).
    //     The first case causes the asset to be fetched. The second case requires
    //     us to not loop forever.
    // Called after creating a physical mesh or hull. If the physical shape was created,
    //     just return.
    public static BulletShape VerifyMeshCreated(BSScene physicsScene, BulletShape newShape, BSPhysObject prim)
    {
        // If the shape was successfully created, nothing more to do
        if (newShape.HasPhysicalShape)
            return newShape;

        // VerifyMeshCreated is called after trying to create the mesh. If we think the asset had been
        //    fetched but we end up here again, the meshing of the asset must have failed.
        // Prevent trying to keep fetching the mesh by declaring failure.
        if (prim.PrimAssetState == BSPhysObject.PrimAssetCondition.Fetched)
        {
            prim.PrimAssetState = BSPhysObject.PrimAssetCondition.FailedMeshing;
            physicsScene.Logger.WarnFormat("{0} Fetched asset would not mesh. prim={1}, texture={2}",
                                            LogHeader, UsefulPrimInfo(physicsScene, prim), prim.BaseShape.SculptTexture);
            physicsScene.DetailLog("{0},BSShape.VerifyMeshCreated,setFailed,prim={1},tex={2}",
                                            prim.LocalID, UsefulPrimInfo(physicsScene, prim), prim.BaseShape.SculptTexture);
        }
        else
        {
            // If this mesh has an underlying asset and we have not failed getting it before, fetch the asset
            if (prim.BaseShape.SculptEntry
                && !prim.AssetFailed()
                && prim.PrimAssetState != BSPhysObject.PrimAssetCondition.Waiting
                && prim.BaseShape.SculptTexture != OMV.UUID.Zero
                )
            {
                physicsScene.DetailLog("{0},BSShape.VerifyMeshCreated,fetchAsset,objNam={1},tex={2}",
                                            prim.LocalID, prim.PhysObjectName, prim.BaseShape.SculptTexture);
                // Multiple requestors will know we're waiting for this asset
                prim.PrimAssetState = BSPhysObject.PrimAssetCondition.Waiting;

                BSPhysObject xprim = prim;
                RequestAssetDelegate assetProvider = physicsScene.RequestAssetMethod;
                if (assetProvider != null)
                {
                    BSPhysObject yprim = xprim; // probably not necessary, but, just in case.
                    assetProvider(yprim.BaseShape.SculptTexture, delegate(AssetBase asset)
                    {
                        // physicsScene.DetailLog("{0},BSShape.VerifyMeshCreated,assetProviderCallback", xprim.LocalID);
                        bool assetFound = false;
                        string mismatchIDs = String.Empty;  // DEBUG DEBUG
                        if (asset != null && yprim.BaseShape.SculptEntry)
                        {
                            if (yprim.BaseShape.SculptTexture.ToString() == asset.ID)
                            {
                                yprim.BaseShape.SculptData = asset.Data;
                                // This will cause the prim to see that the filler shape is not the right
                                //    one and try again to build the object.
                                // No race condition with the normal shape setting since the rebuild is at taint time.
                                yprim.PrimAssetState = BSPhysObject.PrimAssetCondition.Fetched;
                                yprim.ForceBodyShapeRebuild(false /* inTaintTime */);
                                assetFound = true;
                            }
                            else
                            {
                                mismatchIDs = yprim.BaseShape.SculptTexture.ToString() + "/" + asset.ID;
                            }
                        }
                        if (!assetFound)
                        {
                            yprim.PrimAssetState = BSPhysObject.PrimAssetCondition.FailedAssetFetch;
                        }
                        physicsScene.DetailLog("{0},BSShape.VerifyMeshCreated,fetchAssetCallback,found={1},isSculpt={2},ids={3}",
                                    yprim.LocalID, assetFound, yprim.BaseShape.SculptEntry, mismatchIDs );
                    });
                }
                else
                {
                    xprim.PrimAssetState = BSPhysObject.PrimAssetCondition.FailedAssetFetch;
                    physicsScene.Logger.ErrorFormat("{0} Physical object requires asset but no asset provider. Name={1}",
                                                LogHeader, physicsScene.PhysicsSceneName);
                }
            }
            else
            {
                if (prim.PrimAssetState == BSPhysObject.PrimAssetCondition.FailedAssetFetch)
                {
                    physicsScene.Logger.WarnFormat("{0} Mesh failed to fetch asset. prim={1}, texture={2}",
                                                LogHeader, UsefulPrimInfo(physicsScene, prim), prim.BaseShape.SculptTexture);
                    physicsScene.DetailLog("{0},BSShape.VerifyMeshCreated,wasFailed,prim={1},tex={2}",
                                                prim.LocalID, UsefulPrimInfo(physicsScene, prim), prim.BaseShape.SculptTexture);
                }
                if (prim.PrimAssetState == BSPhysObject.PrimAssetCondition.FailedMeshing)
                {
                    physicsScene.Logger.WarnFormat("{0} Mesh asset would not mesh. prim={1}, texture={2}",
                                                LogHeader, UsefulPrimInfo(physicsScene, prim), prim.BaseShape.SculptTexture);
                    physicsScene.DetailLog("{0},BSShape.VerifyMeshCreated,wasFailedMeshing,prim={1},tex={2}",
                                                prim.LocalID, UsefulPrimInfo(physicsScene, prim), prim.BaseShape.SculptTexture);
                }
            }
         }

        // While we wait for the mesh defining asset to be loaded, stick in a simple box for the object.
        BSShape fillShape = BSShapeNative.GetReference(physicsScene, prim, BSPhysicsShapeType.SHAPE_BOX, FixedShapeKey.KEY_BOX);
        physicsScene.DetailLog("{0},BSShape.VerifyMeshCreated,boxTempShape", prim.LocalID);

        return fillShape.physShapeInfo;
     }

    public static String UsefulPrimInfo(BSScene pScene, BSPhysObject prim)
    {
        StringBuilder buff = new StringBuilder(prim.PhysObjectName);
        buff.Append("/pos=");
        buff.Append(prim.RawPosition.ToString());
        if (pScene != null)
        {
            buff.Append("/rgn=");
            buff.Append(pScene.PhysicsSceneName);
        }
        return buff.ToString();
    }

    #endregion // Common shape routines
}

// ============================================================================================================
public class BSShapeNull : BSShape
{
    public BSShapeNull() : base()
    {
    }
    public static BSShape GetReference() { return new BSShapeNull();  }
    public override BSShape GetReference(BSScene pPhysicsScene, BSPhysObject pPrim) { return new BSShapeNull();  }
    public override void Dereference(BSScene physicsScene) { /* The magic of garbage collection will make this go away */ }
}

// ============================================================================================================
// BSShapeNative is a wrapper for a Bullet 'native' shape -- cube and sphere.
// They are odd in that they don't allocate meshes but are computated/procedural.
// This means allocation and freeing is different than meshes.
public class BSShapeNative : BSShape
{
    private static string LogHeader = "[BULLETSIM SHAPE NATIVE]";
    public BSShapeNative(BulletShape pShape) : base(pShape)
    {
    }

    public static BSShape GetReference(BSScene physicsScene, BSPhysObject prim,
                                            BSPhysicsShapeType shapeType, FixedShapeKey shapeKey)
    {
        // Native shapes are not shared and are always built anew.
        return new BSShapeNative(CreatePhysicalNativeShape(physicsScene, prim, shapeType, shapeKey));
    }

    public override BSShape GetReference(BSScene pPhysicsScene, BSPhysObject pPrim)
    {
        // Native shapes are not shared so we return a new shape.
        BSShape ret = null;
        lock (physShapeInfo)
        {
            ret = new BSShapeNative(CreatePhysicalNativeShape(pPhysicsScene, pPrim,
                                        physShapeInfo.shapeType, (FixedShapeKey)physShapeInfo.shapeKey));
        }
        return ret;
    }

    // Make this reference to the physical shape go away since native shapes are not shared.
    public override void Dereference(BSScene physicsScene)
    {
        // Native shapes are not tracked and are released immediately
        lock (physShapeInfo)
        {
            if (physShapeInfo.HasPhysicalShape)
            {
                physicsScene.DetailLog("{0},BSShapeNative.Dereference,deleteNativeShape,shape={1}", BSScene.DetailLogZero, this);
                physicsScene.PE.DeleteCollisionShape(physicsScene.World, physShapeInfo);
            }
            physShapeInfo.Clear();
            // Garbage collection will free up this instance.
        }
    }

    private static BulletShape CreatePhysicalNativeShape(BSScene physicsScene, BSPhysObject prim,
                                            BSPhysicsShapeType shapeType, FixedShapeKey shapeKey)
    {
        BulletShape newShape;

        ShapeData nativeShapeData = new ShapeData();
        nativeShapeData.Type = shapeType;
        nativeShapeData.ID = prim.LocalID;
        nativeShapeData.Scale = prim.Scale;
        nativeShapeData.Size = prim.Scale;
        nativeShapeData.MeshKey = (ulong)shapeKey;
        nativeShapeData.HullKey = (ulong)shapeKey;

        if (shapeType == BSPhysicsShapeType.SHAPE_CAPSULE)
        {
            newShape = physicsScene.PE.BuildCapsuleShape(physicsScene.World, 1f, 1f, prim.Scale);
            physicsScene.DetailLog("{0},BSShapeNative,capsule,scale={1}", prim.LocalID, prim.Scale);
        }
        else
        {
            newShape = physicsScene.PE.BuildNativeShape(physicsScene.World, nativeShapeData);
        }
        if (!newShape.HasPhysicalShape)
        {
            physicsScene.Logger.ErrorFormat("{0} BuildPhysicalNativeShape failed. ID={1}, shape={2}",
                                    LogHeader, prim.LocalID, shapeType);
        }
        newShape.shapeType = shapeType;
        newShape.isNativeShape = true;
        newShape.shapeKey = (UInt64)shapeKey;
        return newShape;
    }

}

// ============================================================================================================
// BSShapeMesh is a simple mesh.
public class BSShapeMesh : BSShape
{
    private static string LogHeader = "[BULLETSIM SHAPE MESH]";
    public static Dictionary<System.UInt64, BSShapeMesh> Meshes = new Dictionary<System.UInt64, BSShapeMesh>();

    public BSShapeMesh(BulletShape pShape) : base(pShape)
    {
    }
    public static BSShape GetReference(BSScene physicsScene, bool forceRebuild, BSPhysObject prim)
    {
        float lod;
        System.UInt64 newMeshKey = BSShape.ComputeShapeKey(prim.Size, prim.BaseShape, out lod);

        BSShapeMesh retMesh = null;
        lock (Meshes)
        {
            if (Meshes.TryGetValue(newMeshKey, out retMesh))
            {
                // The mesh has already been created. Return a new reference to same.
                retMesh.IncrementReference();
            }
            else
            {
                retMesh = new BSShapeMesh(new BulletShape());
                // An instance of this mesh has not been created. Build and remember same.
                BulletShape newShape = retMesh.CreatePhysicalMesh(physicsScene, prim, newMeshKey, prim.BaseShape, prim.Size, lod);

                // Check to see if mesh was created (might require an asset).
                newShape = VerifyMeshCreated(physicsScene, newShape, prim);
                if (!newShape.isNativeShape || prim.AssetFailed() )
                {
                    // If a mesh was what was created, remember the built shape for later sharing.
                    // Also note that if meshing failed we put it in the mesh list as there is nothing else to do about the mesh.
                    Meshes.Add(newMeshKey, retMesh);
                }

                retMesh.physShapeInfo = newShape;
            }
        }
        physicsScene.DetailLog("{0},BSShapeMesh,getReference,mesh={1},size={2},lod={3}", prim.LocalID, retMesh, prim.Size, lod);
        return retMesh;
    }
    public override BSShape GetReference(BSScene pPhysicsScene, BSPhysObject pPrim)
    {
        BSShape ret = null;
        // If the underlying shape is native, the actual shape has not been build (waiting for asset)
        //     and we must create a copy of the native shape since they are never shared.
        if (physShapeInfo.HasPhysicalShape && physShapeInfo.isNativeShape)
        {
            // TODO: decide when the native shapes should be freed. Check in Dereference?
            ret = BSShapeNative.GetReference(pPhysicsScene, pPrim, BSPhysicsShapeType.SHAPE_BOX, FixedShapeKey.KEY_BOX);
        }
        else
        {
            // Another reference to this shape is just counted.
            IncrementReference();
            ret = this;
        }
        return ret;
    }
    public override void Dereference(BSScene physicsScene)
    {
        lock (Meshes)
        {
            this.DecrementReference();
            physicsScene.DetailLog("{0},BSShapeMesh.Dereference,shape={1}", BSScene.DetailLogZero, this);
            // TODO: schedule aging and destruction of unused meshes.
        }
    }
    // Loop through all the known meshes and return the description based on the physical address.
    public static bool TryGetMeshByPtr(BulletShape pShape, out BSShapeMesh outMesh)
    {
        bool ret = false;
        BSShapeMesh foundDesc = null;
        lock (Meshes)
        {
            foreach (BSShapeMesh sm in Meshes.Values)
            {
                if (sm.physShapeInfo.ReferenceSame(pShape))
                {
                    foundDesc = sm;
                    ret = true;
                    break;
                }

            }
        }
        outMesh = foundDesc;
        return ret;
    }

    public delegate BulletShape CreateShapeCall(BulletWorld world, int indicesCount, int[] indices, int verticesCount, float[] vertices );
    private BulletShape CreatePhysicalMesh(BSScene physicsScene, BSPhysObject prim, System.UInt64 newMeshKey,
                                            PrimitiveBaseShape pbs, OMV.Vector3 size, float lod)
    {
        return BSShapeMesh.CreatePhysicalMeshShape(physicsScene, prim, newMeshKey, pbs, size, lod,
                            (w, iC, i, vC, v) =>
                            {
                                shapeInfo.Vertices = vC;
                                return physicsScene.PE.CreateMeshShape(w, iC, i, vC, v);
                            });
    }

    // Code that uses the mesher to create the index/vertices info for a trimesh shape.
    // This is used by the passed 'makeShape' call to create the Bullet mesh shape.
    // The actual build call is passed so this logic can be used by several of the shapes that use a
    //     simple mesh as their base shape.
    public static BulletShape CreatePhysicalMeshShape(BSScene physicsScene, BSPhysObject prim, System.UInt64 newMeshKey,
                                            PrimitiveBaseShape pbs, OMV.Vector3 size, float lod, CreateShapeCall makeShape)
    {
        BulletShape newShape = new BulletShape();

        IMesh meshData = null;
        lock (physicsScene.mesher)
        {
            meshData = physicsScene.mesher.CreateMesh(prim.PhysObjectName, pbs, size, lod,
                                            false,  // say it is not physical so a bounding box is not built
                                            false,   // do not cache the mesh and do not use previously built versions
                                            false,
                                            false
                                            );
        }

        if (meshData != null)
        {
            if (prim.PrimAssetState == BSPhysObject.PrimAssetCondition.Fetched)
            {
                // Release the fetched asset data once it has been used.
                pbs.SculptData = new byte[0];
                prim.PrimAssetState = BSPhysObject.PrimAssetCondition.Unknown;
            }

            int[] indices = meshData.getIndexListAsInt();
            int realIndicesIndex = indices.Length;
            float[] verticesAsFloats = meshData.getVertexListAsFloat();

            if (BSParam.ShouldRemoveZeroWidthTriangles)
            {
                // Remove degenerate triangles. These are triangles with two of the vertices
                //    are the same. This is complicated by the problem that vertices are not
                //    made unique in sculpties so we have to compare the values in the vertex.
                realIndicesIndex = 0;
                for (int tri = 0; tri < indices.Length; tri += 3)
                {
                    // Compute displacements into vertex array for each vertex of the triangle
                    int v1 = indices[tri + 0] * 3;
                    int v2 = indices[tri + 1] * 3;
                    int v3 = indices[tri + 2] * 3;
                // Check to see if any two of the vertices are the same
                    if (!( (  verticesAsFloats[v1 + 0] == verticesAsFloats[v2 + 0]
                           && verticesAsFloats[v1 + 1] == verticesAsFloats[v2 + 1]
                           && verticesAsFloats[v1 + 2] == verticesAsFloats[v2 + 2])
                        || (  verticesAsFloats[v2 + 0] == verticesAsFloats[v3 + 0]
                           && verticesAsFloats[v2 + 1] == verticesAsFloats[v3 + 1]
                           && verticesAsFloats[v2 + 2] == verticesAsFloats[v3 + 2])
                        || (  verticesAsFloats[v1 + 0] == verticesAsFloats[v3 + 0]
                           && verticesAsFloats[v1 + 1] == verticesAsFloats[v3 + 1]
                           && verticesAsFloats[v1 + 2] == verticesAsFloats[v3 + 2]) )
                    )
                    {
                        // None of the vertices of the triangles are the same. This is a good triangle;
                        indices[realIndicesIndex + 0] = indices[tri + 0];
                        indices[realIndicesIndex + 1] = indices[tri + 1];
                        indices[realIndicesIndex + 2] = indices[tri + 2];
                        realIndicesIndex += 3;
                    }
                }
            }
            physicsScene.DetailLog("{0},BSShapeMesh.CreatePhysicalMesh,key={1},origTri={2},realTri={3},numVerts={4}",
                        BSScene.DetailLogZero, newMeshKey.ToString("X"), indices.Length / 3, realIndicesIndex / 3, verticesAsFloats.Length / 3);

            if (realIndicesIndex != 0)
            {
                newShape = makeShape(physicsScene.World, realIndicesIndex, indices, verticesAsFloats.Length / 3, verticesAsFloats);
            }
            else
            {
                // Force the asset condition to 'failed' so we won't try to keep fetching and processing this mesh.
                prim.PrimAssetState = BSPhysObject.PrimAssetCondition.FailedMeshing;
                physicsScene.Logger.DebugFormat("{0} All mesh triangles degenerate. Prim={1}", LogHeader, UsefulPrimInfo(physicsScene, prim) );
                physicsScene.DetailLog("{0},BSShapeMesh.CreatePhysicalMesh,allDegenerate,key={1}", prim.LocalID, newMeshKey);
            }
        }
        newShape.shapeKey = newMeshKey;

        return newShape;
    }
}

// ============================================================================================================
// BSShapeHull is a physical shape representation htat is made up of many convex hulls.
// The convex hulls are either supplied with the asset or are approximated by one of the
//     convex hull creation routines (in OpenSim or in Bullet).
public class BSShapeHull : BSShape
{
#pragma warning disable 414
    private static string LogHeader = "[BULLETSIM SHAPE HULL]";
#pragma warning restore 414

    public static Dictionary<System.UInt64, BSShapeHull> Hulls = new Dictionary<System.UInt64, BSShapeHull>();


    public BSShapeHull(BulletShape pShape) : base(pShape)
    {
    }
    public static BSShape GetReference(BSScene physicsScene, bool forceRebuild, BSPhysObject prim)
    {
        float lod;
        System.UInt64 newHullKey = BSShape.ComputeShapeKey(prim.Size, prim.BaseShape, out lod);

        BSShapeHull retHull = null;
        lock (Hulls)
        {
            if (Hulls.TryGetValue(newHullKey, out retHull))
            {
                // The mesh has already been created. Return a new reference to same.
                retHull.IncrementReference();
            }
            else
            {
                retHull = new BSShapeHull(new BulletShape());
                // An instance of this mesh has not been created. Build and remember same.
                BulletShape newShape = retHull.CreatePhysicalHull(physicsScene, prim, newHullKey, prim.BaseShape, prim.Size, lod);

                // Check to see if hull was created (might require an asset).
                newShape = VerifyMeshCreated(physicsScene, newShape, prim);
                if (!newShape.isNativeShape || prim.AssetFailed())
                {
                    // If a mesh was what was created, remember the built shape for later sharing.
                    Hulls.Add(newHullKey, retHull);
                }
                retHull.physShapeInfo = newShape;
            }
        }
        physicsScene.DetailLog("{0},BSShapeHull,getReference,hull={1},size={2},lod={3}", prim.LocalID, retHull, prim.Size, lod);
        return retHull;
    }
    public override BSShape GetReference(BSScene pPhysicsScene, BSPhysObject pPrim)
    {
        BSShape ret = null;
        // If the underlying shape is native, the actual shape has not been build (waiting for asset)
        //     and we must create a copy of the native shape since they are never shared.
        if (physShapeInfo.HasPhysicalShape && physShapeInfo.isNativeShape)
        {
            // TODO: decide when the native shapes should be freed. Check in Dereference?
            ret = BSShapeNative.GetReference(pPhysicsScene, pPrim, BSPhysicsShapeType.SHAPE_BOX, FixedShapeKey.KEY_BOX);
        }
        else
        {
            // Another reference to this shape is just counted.
            IncrementReference();
            ret = this;
        }
        return ret;
    }
    public override void Dereference(BSScene physicsScene)
    {
        lock (Hulls)
        {
            this.DecrementReference();
            physicsScene.DetailLog("{0},BSShapeHull.Dereference,shape={1}", BSScene.DetailLogZero, this);
            // TODO: schedule aging and destruction of unused meshes.
        }
    }

    List<ConvexResult> m_hulls;
    private BulletShape CreatePhysicalHull(BSScene physicsScene, BSPhysObject prim, System.UInt64 newHullKey,
                                            PrimitiveBaseShape pbs, OMV.Vector3 size, float lod)
    {
        BulletShape newShape = new BulletShape();

        IMesh meshData = null;
        List<List<OMV.Vector3>> allHulls = null;
        lock (physicsScene.mesher)
        {
            // Pass true for physicalness as this prevents the creation of bounding box which is not needed
            meshData = physicsScene.mesher.CreateMesh(prim.PhysObjectName, pbs, size, lod, true /* isPhysical */, false /* shouldCache */, false, false);

            // If we should use the asset's hull info, fetch it out of the locked mesher
            if (meshData != null && BSParam.ShouldUseAssetHulls)
            {
                Meshmerizer realMesher = physicsScene.mesher as Meshmerizer;
                if (realMesher != null)
                {
                    allHulls = realMesher.GetConvexHulls(size);
                }
                if (allHulls == null)
                {
                    physicsScene.DetailLog("{0},BSShapeHull.CreatePhysicalHull,assetHulls,noAssetHull", prim.LocalID);
                }
            }
        }

        // If there is hull data in the mesh asset, build the hull from that
        if (allHulls != null && BSParam.ShouldUseAssetHulls)
        {
            int hullCount = allHulls.Count;
            shapeInfo.HullCount = hullCount;
            int totalVertices = 1;      // include one for the count of the hulls
            // Using the structure described for HACD hulls, create the memory sturcture
            //     to pass the hull data to the creater.
            foreach (List<OMV.Vector3> hullVerts in allHulls)
            {
                totalVertices += 4;                     // add four for the vertex count and centroid
                totalVertices += hullVerts.Count * 3;   // one vertex is three dimensions
            }
            float[] convHulls = new float[totalVertices];

            convHulls[0] = (float)hullCount;
            int jj = 1;
            int hullIndex = 0;
            foreach (List<OMV.Vector3> hullVerts in allHulls)
            {
                convHulls[jj + 0] = hullVerts.Count;
                convHulls[jj + 1] = 0f;   // centroid x,y,z
                convHulls[jj + 2] = 0f;
                convHulls[jj + 3] = 0f;
                jj += 4;
                foreach (OMV.Vector3 oneVert in hullVerts)
                {
                    convHulls[jj + 0] = oneVert.X;
                    convHulls[jj + 1] = oneVert.Y;
                    convHulls[jj + 2] = oneVert.Z;
                    jj += 3;
                }
                shapeInfo.SetVerticesPerHull(hullIndex, hullVerts.Count);
                hullIndex++;
            }

            // create the hull data structure in Bullet
            newShape = physicsScene.PE.CreateHullShape(physicsScene.World, hullCount, convHulls);

            physicsScene.DetailLog("{0},BSShapeHull.CreatePhysicalHull,assetHulls,hulls={1},totVert={2},shape={3}",
                                        prim.LocalID, hullCount, totalVertices, newShape);
        }

        // If no hull specified in the asset and we should use Bullet's HACD approximation...
        if (!newShape.HasPhysicalShape && BSParam.ShouldUseBulletHACD)
        {
            // Build the hull shape from an existing mesh shape.
            // The mesh should have already been created in Bullet.
            physicsScene.DetailLog("{0},BSShapeHull.CreatePhysicalHull,bulletHACD,entry", prim.LocalID);
            BSShape meshShape = BSShapeMesh.GetReference(physicsScene, true, prim);

            if (meshShape.physShapeInfo.HasPhysicalShape)
            {
                HACDParams parms = new HACDParams();
                parms.maxVerticesPerHull = BSParam.BHullMaxVerticesPerHull;
                parms.minClusters = BSParam.BHullMinClusters;
                parms.compacityWeight = BSParam.BHullCompacityWeight;
                parms.volumeWeight = BSParam.BHullVolumeWeight;
                parms.concavity = BSParam.BHullConcavity;
                parms.addExtraDistPoints = BSParam.NumericBool(BSParam.BHullAddExtraDistPoints);
                parms.addNeighboursDistPoints = BSParam.NumericBool(BSParam.BHullAddNeighboursDistPoints);
                parms.addFacesPoints = BSParam.NumericBool(BSParam.BHullAddFacesPoints);
                parms.shouldAdjustCollisionMargin = BSParam.NumericBool(BSParam.BHullShouldAdjustCollisionMargin);
                parms.whichHACD = 0;    // Use the HACD routine that comes with Bullet

                physicsScene.DetailLog("{0},BSShapeHull.CreatePhysicalHull,hullFromMesh,beforeCall", prim.LocalID, newShape.HasPhysicalShape);
                newShape = physicsScene.PE.BuildHullShapeFromMesh(physicsScene.World, meshShape.physShapeInfo, parms);
                physicsScene.DetailLog("{0},BSShapeHull.CreatePhysicalHull,hullFromMesh,shape={1}", prim.LocalID, newShape);

                // Now done with the mesh shape.
                shapeInfo.HullCount = 1;
                BSShapeMesh maybeMesh = meshShape as BSShapeMesh;
                if (maybeMesh != null)
                    shapeInfo.SetVerticesPerHull(0, maybeMesh.shapeInfo.Vertices);
                meshShape.Dereference(physicsScene);
            }
            physicsScene.DetailLog("{0},BSShapeHull.CreatePhysicalHull,bulletHACD,exit,hasBody={1}", prim.LocalID, newShape.HasPhysicalShape);
        }

        // If no other hull specifications, use our HACD hull approximation.
        if (!newShape.HasPhysicalShape && meshData != null)
        {
            if (prim.PrimAssetState == BSPhysObject.PrimAssetCondition.Fetched)
            {
                // Release the fetched asset data once it has been used.
                pbs.SculptData = new byte[0];
                prim.PrimAssetState = BSPhysObject.PrimAssetCondition.Unknown;
            }

            int[] indices = meshData.getIndexListAsInt();
            List<OMV.Vector3> vertices = meshData.getVertexList();

            //format conversion from IMesh format to DecompDesc format
            List<int> convIndices = new List<int>();
            List<float3> convVertices = new List<float3>();
            for (int ii = 0; ii < indices.GetLength(0); ii++)
            {
                convIndices.Add(indices[ii]);
            }
            foreach (OMV.Vector3 vv in vertices)
            {
                convVertices.Add(new float3(vv.X, vv.Y, vv.Z));
            }

            uint maxDepthSplit = (uint)BSParam.CSHullMaxDepthSplit;
            if (BSParam.CSHullMaxDepthSplit != BSParam.CSHullMaxDepthSplitForSimpleShapes)
            {
                // Simple primitive shapes we know are convex so they are better implemented with
                //    fewer hulls.
                // Check for simple shape (prim without cuts) and reduce split parameter if so.
                if (BSShapeCollection.PrimHasNoCuts(pbs))
                {
                    maxDepthSplit = (uint)BSParam.CSHullMaxDepthSplitForSimpleShapes;
                }
            }

            // setup and do convex hull conversion
            m_hulls = new List<ConvexResult>();
            DecompDesc dcomp = new DecompDesc();
            dcomp.mIndices = convIndices;
            dcomp.mVertices = convVertices;
            dcomp.mDepth = maxDepthSplit;
            dcomp.mCpercent = BSParam.CSHullConcavityThresholdPercent;
            dcomp.mPpercent = BSParam.CSHullVolumeConservationThresholdPercent;
            dcomp.mMaxVertices = (uint)BSParam.CSHullMaxVertices;
            dcomp.mSkinWidth = BSParam.CSHullMaxSkinWidth;
            ConvexBuilder convexBuilder = new ConvexBuilder(HullReturn);
            // create the hull into the _hulls variable
            convexBuilder.process(dcomp);

            physicsScene.DetailLog("{0},BSShapeCollection.CreatePhysicalHull,key={1},inVert={2},inInd={3},split={4},hulls={5}",
                                BSScene.DetailLogZero, newHullKey, indices.GetLength(0), vertices.Count, maxDepthSplit, m_hulls.Count);

            // Convert the vertices and indices for passing to unmanaged.
            // The hull information is passed as a large floating point array.
            // The format is:
            //  convHulls[0] = number of hulls
            //  convHulls[1] = number of vertices in first hull
            //  convHulls[2] = hull centroid X coordinate
            //  convHulls[3] = hull centroid Y coordinate
            //  convHulls[4] = hull centroid Z coordinate
            //  convHulls[5] = first hull vertex X
            //  convHulls[6] = first hull vertex Y
            //  convHulls[7] = first hull vertex Z
            //  convHulls[8] = second hull vertex X
            //  ...
            //  convHulls[n] = number of vertices in second hull
            //  convHulls[n+1] = second hull centroid X coordinate
            //  ...
            //
            // TODO: is is very inefficient. Someday change the convex hull generator to return
            //   data structures that do not need to be converted in order to pass to Bullet.
            //   And maybe put the values directly into pinned memory rather than marshaling.
            int hullCount = m_hulls.Count;
            int totalVertices = 1;          // include one for the count of the hulls
            foreach (ConvexResult cr in m_hulls)
            {
                totalVertices += 4;                         // add four for the vertex count and centroid
                totalVertices += cr.HullIndices.Count * 3;  // we pass just triangles
            }
            float[] convHulls = new float[totalVertices];

            convHulls[0] = (float)hullCount;
            int jj = 1;
            foreach (ConvexResult cr in m_hulls)
            {
                // copy vertices for index access
                float3[] verts = new float3[cr.HullVertices.Count];
                int kk = 0;
                foreach (float3 ff in cr.HullVertices)
                {
                    verts[kk++] = ff;
                }

                // add to the array one hull's worth of data
                convHulls[jj++] = cr.HullIndices.Count;
                convHulls[jj++] = 0f;   // centroid x,y,z
                convHulls[jj++] = 0f;
                convHulls[jj++] = 0f;
                foreach (int ind in cr.HullIndices)
                {
                    convHulls[jj++] = verts[ind].x;
                    convHulls[jj++] = verts[ind].y;
                    convHulls[jj++] = verts[ind].z;
                }
            }
            // create the hull data structure in Bullet
            newShape = physicsScene.PE.CreateHullShape(physicsScene.World, hullCount, convHulls);
        }
        newShape.shapeKey = newHullKey;
        return newShape;
    }
    // Callback from convex hull creater with a newly created hull.
    // Just add it to our collection of hulls for this shape.
    private void HullReturn(ConvexResult result)
    {
        m_hulls.Add(result);
        return;
    }
    // Loop through all the known hulls and return the description based on the physical address.
    public static bool TryGetHullByPtr(BulletShape pShape, out BSShapeHull outHull)
    {
        bool ret = false;
        BSShapeHull foundDesc = null;
        lock (Hulls)
        {
            foreach (BSShapeHull sh in Hulls.Values)
            {
                if (sh.physShapeInfo.ReferenceSame(pShape))
                {
                    foundDesc = sh;
                    ret = true;
                    break;
                }

            }
        }
        outHull = foundDesc;
        return ret;
    }
}

// ============================================================================================================
// BSShapeCompound is a wrapper for the Bullet compound shape which is built from multiple, separate
//    meshes. Used by BulletSim for complex shapes like linksets.
public class BSShapeCompound : BSShape
{
    private static string LogHeader = "[BULLETSIM SHAPE COMPOUND]";
    public static Dictionary<string, BSShapeCompound> CompoundShapes = new Dictionary<string, BSShapeCompound>();

    public BSShapeCompound(BulletShape pShape) : base(pShape)
    {
    }
    public static BSShape GetReference(BSScene physicsScene)
    {
        // Base compound shapes are not shared so this returns a raw shape.
        // A built compound shape can be reused in linksets.
        BSShapeCompound ret = new BSShapeCompound(CreatePhysicalCompoundShape(physicsScene));
        CompoundShapes.Add(ret.AddrString, ret);
        return ret;
    }
    public override BSShape GetReference(BSScene physicsScene, BSPhysObject prim)
    {
        // Calling this reference means we want another handle to an existing compound shape
        //     (usually linksets) so return this copy.
        IncrementReference();
        return this;
    }
    // Dereferencing a compound shape releases the hold on all the child shapes.
    public override void Dereference(BSScene physicsScene)
    {
        lock (physShapeInfo)
        {
            this.DecrementReference();
            physicsScene.DetailLog("{0},BSShapeCompound.Dereference,shape={1}", BSScene.DetailLogZero, this);
            if (referenceCount <= 0)
            {
                if (!physicsScene.PE.IsCompound(physShapeInfo))
                {
                    // Failed the sanity check!!
                    physicsScene.Logger.ErrorFormat("{0} Attempt to free a compound shape that is not compound!! type={1}, ptr={2}",
                                                LogHeader, physShapeInfo.shapeType, physShapeInfo.AddrString);
                    physicsScene.DetailLog("{0},BSShapeCollection.DereferenceCompound,notACompoundShape,type={1},ptr={2}",
                                                BSScene.DetailLogZero, physShapeInfo.shapeType, physShapeInfo.AddrString);
                    return;
                }

                int numChildren = physicsScene.PE.GetNumberOfCompoundChildren(physShapeInfo);
                physicsScene.DetailLog("{0},BSShapeCollection.DereferenceCompound,shape={1},children={2}",
                                        BSScene.DetailLogZero, physShapeInfo, numChildren);

                // Loop through all the children dereferencing each.
                for (int ii = numChildren - 1; ii >= 0; ii--)
                {
                    BulletShape childShape = physicsScene.PE.RemoveChildShapeFromCompoundShapeIndex(physShapeInfo, ii);
                    DereferenceAnonCollisionShape(physicsScene, childShape);
                }

                lock (CompoundShapes)
                    CompoundShapes.Remove(physShapeInfo.AddrString);
                physicsScene.PE.DeleteCollisionShape(physicsScene.World, physShapeInfo);
            }
        }
    }
    public static bool TryGetCompoundByPtr(BulletShape pShape, out BSShapeCompound outCompound)
    {
        lock (CompoundShapes)
        {
            string addr = pShape.AddrString;
            return CompoundShapes.TryGetValue(addr, out outCompound);
        }
    }
    private static BulletShape CreatePhysicalCompoundShape(BSScene physicsScene)
    {
        BulletShape cShape = physicsScene.PE.CreateCompoundShape(physicsScene.World, false);
        return cShape;
    }
    // Sometimes we have a pointer to a collision shape but don't know what type it is.
    // Figure out type and call the correct dereference routine.
    // Called at taint-time.
    private void DereferenceAnonCollisionShape(BSScene physicsScene, BulletShape pShape)
    {
        // TODO: figure a better way to go through all the shape types and find a possible instance.
        physicsScene.DetailLog("{0},BSShapeCompound.DereferenceAnonCollisionShape,shape={1}",
                                            BSScene.DetailLogZero, pShape);
        BSShapeMesh meshDesc;
        if (BSShapeMesh.TryGetMeshByPtr(pShape, out meshDesc))
        {
            meshDesc.Dereference(physicsScene);
            // physicsScene.DetailLog("{0},BSShapeCompound.DereferenceAnonCollisionShape,foundMesh,shape={1}", BSScene.DetailLogZero, pShape);
        }
        else
        {
            BSShapeHull hullDesc;
            if (BSShapeHull.TryGetHullByPtr(pShape, out hullDesc))
            {
                hullDesc.Dereference(physicsScene);
                // physicsScene.DetailLog("{0},BSShapeCompound.DereferenceAnonCollisionShape,foundHull,shape={1}", BSScene.DetailLogZero, pShape);
            }
            else
            {
                BSShapeConvexHull chullDesc;
                if (BSShapeConvexHull.TryGetConvexHullByPtr(pShape, out chullDesc))
                {
                    chullDesc.Dereference(physicsScene);
                    // physicsScene.DetailLog("{0},BSShapeCompound.DereferenceAnonCollisionShape,foundConvexHull,shape={1}", BSScene.DetailLogZero, pShape);
                }
                else
                {
                    BSShapeGImpact gImpactDesc;
                    if (BSShapeGImpact.TryGetGImpactByPtr(pShape, out gImpactDesc))
                    {
                        gImpactDesc.Dereference(physicsScene);
                        // physicsScene.DetailLog("{0},BSShapeCompound.DereferenceAnonCollisionShape,foundgImpact,shape={1}", BSScene.DetailLogZero, pShape);
                    }
                    else
                    {
                        // Didn't find it in the lists of specific types. It could be compound.
                        BSShapeCompound compoundDesc;
                        if (BSShapeCompound.TryGetCompoundByPtr(pShape, out compoundDesc))
                        {
                            compoundDesc.Dereference(physicsScene);
                            // physicsScene.DetailLog("{0},BSShapeCompound.DereferenceAnonCollisionShape,recursiveCompoundShape,shape={1}", BSScene.DetailLogZero, pShape);
                        }
                        else
                        {
                            // If none of the above, maybe it is a simple native shape.
                            if (physicsScene.PE.IsNativeShape(pShape))
                            {
                                // physicsScene.DetailLog("{0},BSShapeCompound.DereferenceAnonCollisionShape,assumingNative,shape={1}", BSScene.DetailLogZero, pShape);
                                BSShapeNative nativeShape = new BSShapeNative(pShape);
                                nativeShape.Dereference(physicsScene);
                            }
                            else
                            {
                                physicsScene.Logger.WarnFormat("{0} DereferenceAnonCollisionShape. Did not find shape. {1}",
                                    LogHeader, pShape);
                            }
                        }
                    }
                }
            }
        }
    }
}

// ============================================================================================================
// BSShapeConvexHull is a wrapper for a Bullet single convex hull. A BSShapeHull contains multiple convex
//     hull shapes. This is used for simple prims that are convex and thus can be made into a simple
//     collision shape (a single hull). More complex physical shapes will be BSShapeHull's.
public class BSShapeConvexHull : BSShape
{
#pragma warning disable 414
    private static string LogHeader = "[BULLETSIM SHAPE CONVEX HULL]";
#pragma warning restore 414

    public static Dictionary<System.UInt64, BSShapeConvexHull> ConvexHulls = new Dictionary<System.UInt64, BSShapeConvexHull>();

    public BSShapeConvexHull(BulletShape pShape) : base(pShape)
    {
    }
    public static BSShape GetReference(BSScene physicsScene, bool forceRebuild, BSPhysObject prim)
    {
        float lod;
        System.UInt64 newMeshKey = BSShape.ComputeShapeKey(prim.Size, prim.BaseShape, out lod);

        physicsScene.DetailLog("{0},BSShapeConvexHull,getReference,newKey={1},size={2},lod={3}",
                                prim.LocalID, newMeshKey.ToString("X"), prim.Size, lod);

        BSShapeConvexHull retConvexHull = null;
        lock (ConvexHulls)
        {
            if (ConvexHulls.TryGetValue(newMeshKey, out retConvexHull))
            {
                // The mesh has already been created. Return a new reference to same.
                retConvexHull.IncrementReference();
            }
            else
            {
                retConvexHull = new BSShapeConvexHull(new BulletShape());
                BulletShape convexShape = null;

                // Get a handle to a mesh to build the hull from
                BSShape baseMesh = BSShapeMesh.GetReference(physicsScene, false /* forceRebuild */, prim);
                if (baseMesh.physShapeInfo.isNativeShape)
                {
                    // We get here if the mesh was not creatable. Could be waiting for an asset from the disk.
                    // In the short term, we return the native shape and a later ForceBodyShapeRebuild should
                    //     get back to this code with a buildable mesh.
                    // TODO: not sure the temp native shape is freed when the mesh is rebuilt. When does this get freed?
                    convexShape = baseMesh.physShapeInfo;
                }
                else
                {
                    convexShape = physicsScene.PE.BuildConvexHullShapeFromMesh(physicsScene.World, baseMesh.physShapeInfo);
                    convexShape.shapeKey = newMeshKey;
                    ConvexHulls.Add(convexShape.shapeKey, retConvexHull);
                    physicsScene.DetailLog("{0},BSShapeConvexHull.GetReference,addingNewlyCreatedShape,shape={1}",
                                        BSScene.DetailLogZero, convexShape);
                }

                // Done with the base mesh
                baseMesh.Dereference(physicsScene);

                retConvexHull.physShapeInfo = convexShape;
            }
        }
        return retConvexHull;
    }
    public override BSShape GetReference(BSScene physicsScene, BSPhysObject prim)
    {
        // Calling this reference means we want another handle to an existing shape
        //     (usually linksets) so return this copy.
        IncrementReference();
        return this;
    }
    // Dereferencing a compound shape releases the hold on all the child shapes.
    public override void Dereference(BSScene physicsScene)
    {
        lock (ConvexHulls)
        {
            this.DecrementReference();
            physicsScene.DetailLog("{0},BSShapeConvexHull.Dereference,shape={1}", BSScene.DetailLogZero, this);
            // TODO: schedule aging and destruction of unused meshes.
        }
    }
    // Loop through all the known hulls and return the description based on the physical address.
    public static bool TryGetConvexHullByPtr(BulletShape pShape, out BSShapeConvexHull outHull)
    {
        bool ret = false;
        BSShapeConvexHull foundDesc = null;
        lock (ConvexHulls)
        {
            foreach (BSShapeConvexHull sh in ConvexHulls.Values)
            {
                if (sh.physShapeInfo.ReferenceSame(pShape))
                {
                    foundDesc = sh;
                    ret = true;
                    break;
                }

            }
        }
        outHull = foundDesc;
        return ret;
    }
}
// ============================================================================================================
// BSShapeGImpact is a wrapper for the Bullet GImpact shape which is a collision mesh shape that
//     can handle concave as well as convex shapes. Much slower computationally but creates smoother
//     shapes than multiple convex hull approximations.
public class BSShapeGImpact : BSShape
{
#pragma warning disable 414
    private static string LogHeader = "[BULLETSIM SHAPE GIMPACT]";
#pragma warning restore 414

    public static Dictionary<System.UInt64, BSShapeGImpact> GImpacts = new Dictionary<System.UInt64, BSShapeGImpact>();

    public BSShapeGImpact(BulletShape pShape) : base(pShape)
    {
    }
    public static BSShape GetReference(BSScene physicsScene, bool forceRebuild, BSPhysObject prim)
    {
        float lod;
        System.UInt64 newMeshKey = BSShape.ComputeShapeKey(prim.Size, prim.BaseShape, out lod);

        physicsScene.DetailLog("{0},BSShapeGImpact,getReference,newKey={1},size={2},lod={3}",
                                prim.LocalID, newMeshKey.ToString("X"), prim.Size, lod);

        BSShapeGImpact retGImpact = null;
        lock (GImpacts)
        {
            if (GImpacts.TryGetValue(newMeshKey, out retGImpact))
            {
                // The mesh has already been created. Return a new reference to same.
                retGImpact.IncrementReference();
            }
            else
            {
                retGImpact = new BSShapeGImpact(new BulletShape());
                BulletShape newShape = retGImpact.CreatePhysicalGImpact(physicsScene, prim, newMeshKey, prim.BaseShape, prim.Size, lod);

                // Check to see if mesh was created (might require an asset).
                newShape = VerifyMeshCreated(physicsScene, newShape, prim);
                newShape.shapeKey = newMeshKey;
                if (!newShape.isNativeShape || prim.AssetFailed())
                {
                    // If a mesh was what was created, remember the built shape for later sharing.
                    // Also note that if meshing failed we put it in the mesh list as there is nothing
                    //         else to do about the mesh.
                    GImpacts.Add(newMeshKey, retGImpact);
                }

                retGImpact.physShapeInfo = newShape;
            }
        }
        return retGImpact;
    }

    private BulletShape CreatePhysicalGImpact(BSScene physicsScene, BSPhysObject prim, System.UInt64 newMeshKey,
                                            PrimitiveBaseShape pbs, OMV.Vector3 size, float lod)
    {
        return BSShapeMesh.CreatePhysicalMeshShape(physicsScene, prim, newMeshKey, pbs, size, lod,
                            (w, iC, i, vC, v) =>
                            {
                                shapeInfo.Vertices = vC;
                                return physicsScene.PE.CreateGImpactShape(w, iC, i, vC, v);
                            });
    }

    public override BSShape GetReference(BSScene pPhysicsScene, BSPhysObject pPrim)
    {
        BSShape ret = null;
        // If the underlying shape is native, the actual shape has not been build (waiting for asset)
        //     and we must create a copy of the native shape since they are never shared.
        if (physShapeInfo.HasPhysicalShape && physShapeInfo.isNativeShape)
        {
            // TODO: decide when the native shapes should be freed. Check in Dereference?
            ret = BSShapeNative.GetReference(pPhysicsScene, pPrim, BSPhysicsShapeType.SHAPE_BOX, FixedShapeKey.KEY_BOX);
        }
        else
        {
            // Another reference to this shape is just counted.
            IncrementReference();
            ret = this;
        }
        return ret;
    }
    // Dereferencing a compound shape releases the hold on all the child shapes.
    public override void Dereference(BSScene physicsScene)
    {
        lock (GImpacts)
        {
            this.DecrementReference();
            physicsScene.DetailLog("{0},BSShapeGImpact.Dereference,shape={1}", BSScene.DetailLogZero, this);
            // TODO: schedule aging and destruction of unused meshes.
        }
    }
    // Loop through all the known hulls and return the description based on the physical address.
    public static bool TryGetGImpactByPtr(BulletShape pShape, out BSShapeGImpact outHull)
    {
        bool ret = false;
        BSShapeGImpact foundDesc = null;
        lock (GImpacts)
        {
            foreach (BSShapeGImpact sh in GImpacts.Values)
            {
                if (sh.physShapeInfo.ReferenceSame(pShape))
                {
                    foundDesc = sh;
                    ret = true;
                    break;
                }

            }
        }
        outHull = foundDesc;
        return ret;
    }
}

// ============================================================================================================
// BSShapeAvatar is a specialized mesh shape for avatars.
public class BSShapeAvatar : BSShape
{
#pragma warning disable 414
    private static string LogHeader = "[BULLETSIM SHAPE AVATAR]";
#pragma warning restore 414

    public BSShapeAvatar()
        : base()
    {
    }
    public static BSShape GetReference(BSPhysObject prim)
    {
        return new BSShapeNull();
    }
    public override BSShape GetReference(BSScene pPhysicsScene, BSPhysObject pPrim)
    {
        return new BSShapeNull();
    }
    public override void Dereference(BSScene physicsScene) { }

    // From the front:
    //     A---A
    //    /     \
    //   B-------B
    //  /         \        +Z
    // C-----------C        |
    // \           /   -Y --+-- +Y
    //  \         /         |
    //   \       /         -Z
    //    D-----D
    //     \   /
    //      E-E

    // From the top A and E are just lines.
    //              B, C and D are hexagons:
    //
    //     C1--C2            +X
    //    /      \            |
    //  C0        C3     -Y --+-- +Y
    //    \      /            |
    //     C5--C4            -X

    // Zero goes directly through the middle so the offsets are from that middle axis
    //     and up and down from a middle horizon (A and E are the same distance from the zero).
    // The height, width and depth is one. All scaling is done by the simulator.

    // Z component -- how far the level is from the middle zero
    private const float Aup = 0.5f;
    private const float Bup = 0.4f;
    private const float Cup = 0.3f;
    private const float Dup = -0.4f;
    private const float Eup = -0.5f;

    // Y component -- distance from center to x0 and x3
    private const float Awid = 0.25f;
    private const float Bwid = 0.3f;
    private const float Cwid = 0.5f;
    private const float Dwid = 0.3f;
    private const float Ewid = 0.2f;

    // Y component -- distance from center to x1, x2, x4 and x5
    private const float Afwid = 0.0f;
    private const float Bfwid = 0.2f;
    private const float Cfwid = 0.4f;
    private const float Dfwid = 0.2f;
    private const float Efwid = 0.0f;

    // X component -- distance from zero to the front or back of a level
    private const float Adep = 0f;
    private const float Bdep = 0.3f;
    private const float Cdep = 0.5f;
    private const float Ddep = 0.2f;
    private const float Edep = 0f;

    private OMV.Vector3[] avatarVertices = {
           new OMV.Vector3( 0.0f, -Awid,  Aup),   // A0
           new OMV.Vector3( 0.0f, +Awid,  Aup),   // A3

           new OMV.Vector3( 0.0f, -Bwid,  Bup),   // B0
           new OMV.Vector3(+Bdep, -Bfwid, Bup),   // B1
           new OMV.Vector3(+Bdep, +Bfwid, Bup),   // B2
           new OMV.Vector3( 0.0f, +Bwid,  Bup),   // B3
           new OMV.Vector3(-Bdep, +Bfwid, Bup),   // B4
           new OMV.Vector3(-Bdep, -Bfwid, Bup),   // B5

           new OMV.Vector3( 0.0f, -Cwid,  Cup),   // C0
           new OMV.Vector3(+Cdep, -Cfwid, Cup),   // C1
           new OMV.Vector3(+Cdep, +Cfwid, Cup),   // C2
           new OMV.Vector3( 0.0f, +Cwid,  Cup),   // C3
           new OMV.Vector3(-Cdep, +Cfwid, Cup),   // C4
           new OMV.Vector3(-Cdep, -Cfwid, Cup),   // C5

           new OMV.Vector3( 0.0f, -Dwid,  Dup),   // D0
           new OMV.Vector3(+Ddep, -Dfwid, Dup),   // D1
           new OMV.Vector3(+Ddep, +Dfwid, Dup),   // D2
           new OMV.Vector3( 0.0f, +Dwid,  Dup),   // D3
           new OMV.Vector3(-Ddep, +Dfwid, Dup),   // D4
           new OMV.Vector3(-Ddep, -Dfwid, Dup),   // D5

           new OMV.Vector3( 0.0f, -Ewid,  Eup),   // E0
           new OMV.Vector3( 0.0f, +Ewid,  Eup),   // E3
    };

    // Offsets of the vertices in the vertices array
    private enum Ind : int
    {
        A0, A3,
        B0, B1, B2, B3, B4, B5,
        C0, C1, C2, C3, C4, C5,
        D0, D1, D2, D3, D4, D5,
        E0, E3
    }

    // Comments specify trianges and quads in clockwise direction
    private Ind[] avatarIndices = {
        Ind.A0, Ind.B0, Ind.B1,                         // A0,B0,B1
        Ind.A0, Ind.B1, Ind.B2, Ind.B2, Ind.A3, Ind.A0, // A0,B1,B2,A3
        Ind.A3, Ind.B2, Ind.B3,                         // A3,B2,B3
        Ind.A3, Ind.B3, Ind.B4,                         // A3,B3,B4
        Ind.A3, Ind.B4, Ind.B5, Ind.B5, Ind.A0, Ind.A3, // A3,B4,B5,A0
        Ind.A0, Ind.B5, Ind.B0,                         // A0,B5,B0

        Ind.B0, Ind.C0, Ind.C1, Ind.C1, Ind.B1, Ind.B0, // B0,C0,C1,B1
        Ind.B1, Ind.C1, Ind.C2, Ind.C2, Ind.B2, Ind.B1, // B1,C1,C2,B2
        Ind.B2, Ind.C2, Ind.C3, Ind.C3, Ind.B3, Ind.B2, // B2,C2,C3,B3
        Ind.B3, Ind.C3, Ind.C4, Ind.C4, Ind.B4, Ind.B3, // B3,C3,C4,B4
        Ind.B4, Ind.C4, Ind.C5, Ind.C5, Ind.B5, Ind.B4, // B4,C4,C5,B5
        Ind.B5, Ind.C5, Ind.C0, Ind.C0, Ind.B0, Ind.B5, // B5,C5,C0,B0

        Ind.C0, Ind.D0, Ind.D1, Ind.D1, Ind.C1, Ind.C0, // C0,D0,D1,C1
        Ind.C1, Ind.D1, Ind.D2, Ind.D2, Ind.C2, Ind.C1, // C1,D1,D2,C2
        Ind.C2, Ind.D2, Ind.D3, Ind.D3, Ind.C3, Ind.C2, // C2,D2,D3,C3
        Ind.C3, Ind.D3, Ind.D4, Ind.D4, Ind.C4, Ind.C3, // C3,D3,D4,C4
        Ind.C4, Ind.D4, Ind.D5, Ind.D5, Ind.C5, Ind.C4, // C4,D4,D5,C5
        Ind.C5, Ind.D5, Ind.D0, Ind.D0, Ind.C0, Ind.C5, // C5,D5,D0,C0

        Ind.E0, Ind.D0, Ind.D1,                         // E0,D0,D1
        Ind.E0, Ind.D1, Ind.D2, Ind.D2, Ind.E3, Ind.E0, // E0,D1,D2,E3
        Ind.E3, Ind.D2, Ind.D3,                         // E3,D2,D3
        Ind.E3, Ind.D3, Ind.D4,                         // E3,D3,D4
        Ind.E3, Ind.D4, Ind.D5, Ind.D5, Ind.E0, Ind.E3, // E3,D4,D5,E0
        Ind.E0, Ind.D5, Ind.D0,                         // E0,D5,D0

    };

}
}
