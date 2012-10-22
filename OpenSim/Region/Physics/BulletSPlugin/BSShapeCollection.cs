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
using OMV = OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.Physics.ConvexDecompositionDotNet;

namespace OpenSim.Region.Physics.BulletSPlugin
{
public class BSShapeCollection : IDisposable
{
    private static string LogHeader = "[BULLETSIM SHAPE COLLECTION]";

    protected BSScene PhysicsScene { get; set; }

    private Object m_collectionActivityLock = new Object();

    // Description of a Mesh
    private struct MeshDesc
    {
        public IntPtr ptr;
        public int referenceCount;
        public DateTime lastReferenced;
    }

    // Description of a hull.
    // Meshes and hulls have the same shape hash key but we only need hulls for efficient collision calculations.
    private struct HullDesc
    {
        public IntPtr ptr;
        public int referenceCount;
        public DateTime lastReferenced;
    }

    // The sharable set of meshes and hulls. Indexed by their shape hash.
    private Dictionary<System.UInt64, MeshDesc> Meshes = new Dictionary<System.UInt64, MeshDesc>();
    private Dictionary<System.UInt64, HullDesc> Hulls = new Dictionary<System.UInt64, HullDesc>();

    public BSShapeCollection(BSScene physScene)
    {
        PhysicsScene = physScene;
    }

    public void Dispose()
    {
        // TODO!!!!!!!!!
    }

    // Callbacks called just before either the body or shape is destroyed.
    // Mostly used for changing bodies out from under Linksets.
    // Useful for other cases where parameters need saving.
    // Passing 'null' says no callback.
    public delegate void ShapeDestructionCallback(BulletShape shape);
    public delegate void BodyDestructionCallback(BulletBody body);

    // Called to update/change the body and shape for an object.
    // First checks the shape and updates that if necessary then makes
    //    sure the body is of the right type.
    // Return 'true' if either the body or the shape changed.
    // 'shapeCallback' and 'bodyCallback' are, if non-null, functions called just before
    //    the current shape or body is destroyed. This allows the caller to remove any
    //    higher level dependencies on the shape or body. Mostly used for LinkSets to
    //    remove the physical constraints before the body is destroyed.
    // Called at taint-time!!
    public bool GetBodyAndShape(bool forceRebuild, BulletSim sim, BSPhysObject prim,
                    ShapeData shapeData, PrimitiveBaseShape pbs,
                    ShapeDestructionCallback shapeCallback, BodyDestructionCallback bodyCallback)
    {
        bool ret = false;

        // This lock could probably be pushed down lower but building shouldn't take long
        lock (m_collectionActivityLock)
        {
            // Do we have the correct geometry for this type of object?
            // Updates prim.BSShape with information/pointers to shape.
            // CreateGeom returns 'true' of BSShape as changed to a new shape.
            bool newGeom = CreateGeom(forceRebuild, prim, shapeData, pbs, shapeCallback);
            // If we had to select a new shape geometry for the object,
            //    rebuild the body around it.
            // Updates prim.BSBody with information/pointers to requested body
            bool newBody = CreateBody((newGeom || forceRebuild), prim, PhysicsScene.World,
                                    prim.BSShape, shapeData, bodyCallback);
            ret = newGeom || newBody;
        }
        DetailLog("{0},BSShapeCollection.GetBodyAndShape,force={1},ret={2},body={3},shape={4}",
                                prim.LocalID, forceRebuild, ret, prim.BSBody, prim.BSShape);

        return ret;
    }

    // Track another user of a body
    // We presume the caller has allocated the body.
    // Bodies only have one user so the body is just put into the world if not already there.
    public void ReferenceBody(BulletBody body, bool inTaintTime)
    {
        lock (m_collectionActivityLock)
        {
            DetailLog("{0},BSShapeCollection.ReferenceBody,newBody", body.ID, body);
            BSScene.TaintCallback createOperation = delegate()
            {
                if (!BulletSimAPI.IsInWorld2(body.ptr))
                {
                    BulletSimAPI.AddObjectToWorld2(PhysicsScene.World.ptr, body.ptr);
                    DetailLog("{0},BSShapeCollection.ReferenceBody,addedToWorld,ref={1}", body.ID, body);
                }
            };
            if (inTaintTime)
                createOperation();
            else
                PhysicsScene.TaintedObject("BSShapeCollection.ReferenceBody", createOperation);
        }
    }

    // Release the usage of a body.
    // Called when releasing use of a BSBody. BSShape is handled separately.
    public void DereferenceBody(BulletBody body, bool inTaintTime, BodyDestructionCallback bodyCallback )
    {
        if (body.ptr == IntPtr.Zero)
            return;

        lock (m_collectionActivityLock)
        {
            BSScene.TaintCallback removeOperation = delegate()
            {
                DetailLog("{0},BSShapeCollection.DereferenceBody,DestroyingBody. ptr={1}, inTaintTime={2}",
                                            body.ID, body.ptr.ToString("X"), inTaintTime);
                // If the caller needs to know the old body is going away, pass the event up.
                if (bodyCallback != null) bodyCallback(body);

                // It may have already been removed from the world in which case the next is a NOOP.
                BulletSimAPI.RemoveObjectFromWorld2(PhysicsScene.World.ptr, body.ptr);

                // Zero any reference to the shape so it is not freed when the body is deleted.
                BulletSimAPI.SetCollisionShape2(PhysicsScene.World.ptr, body.ptr, IntPtr.Zero);
                BulletSimAPI.DestroyObject2(PhysicsScene.World.ptr, body.ptr);
            };
            // If already in taint-time, do the operations now. Otherwise queue for later.
            if (inTaintTime)
                removeOperation();
            else
                PhysicsScene.TaintedObject("BSShapeCollection.DereferenceBody", removeOperation);
        }
    }

    // Track the datastructures and use count for a shape.
    // When creating a hull, this is called first to reference the mesh
    //     and then again to reference the hull.
    // Meshes and hulls for the same shape have the same hash key.
    // NOTE that native shapes are not added to the mesh list or removed.
    // Returns 'true' if this is the initial reference to the shape. Otherwise reused.
    private bool ReferenceShape(BulletShape shape)
    {
        bool ret = false;
        switch (shape.type)
        {
            case ShapeData.PhysicsShapeType.SHAPE_MESH:
                MeshDesc meshDesc;
                if (Meshes.TryGetValue(shape.shapeKey, out meshDesc))
                {
                    // There is an existing instance of this mesh.
                    meshDesc.referenceCount++;
                    DetailLog("{0},BSShapeCollection.ReferenceShape,existingMesh,key={1},cnt={2}",
                                BSScene.DetailLogZero, shape.shapeKey.ToString("X"), meshDesc.referenceCount);
                }
                else
                {
                    // This is a new reference to a mesh
                    meshDesc.ptr = shape.ptr;
                    // We keep a reference to the underlying IMesh data so a hull can be built
                    meshDesc.referenceCount = 1;
                    DetailLog("{0},BSShapeCollection.ReferenceShape,newMesh,key={1},cnt={2}",
                                BSScene.DetailLogZero, shape.shapeKey.ToString("X"), meshDesc.referenceCount);
                    ret = true;
                }
                meshDesc.lastReferenced = System.DateTime.Now;
                Meshes[shape.shapeKey] = meshDesc;
                break;
            case ShapeData.PhysicsShapeType.SHAPE_HULL:
                HullDesc hullDesc;
                if (Hulls.TryGetValue(shape.shapeKey, out hullDesc))
                {
                    // There is an existing instance of this hull.
                    hullDesc.referenceCount++;
                    DetailLog("{0},BSShapeCollection.ReferenceShape,existingHull,key={1},cnt={2}",
                                BSScene.DetailLogZero, shape.shapeKey.ToString("X"), hullDesc.referenceCount);
                }
                else
                {
                    // This is a new reference to a hull
                    hullDesc.ptr = shape.ptr;
                    hullDesc.referenceCount = 1;
                    DetailLog("{0},BSShapeCollection.ReferenceShape,newHull,key={1},cnt={2}",
                                BSScene.DetailLogZero, shape.shapeKey.ToString("X"), hullDesc.referenceCount);
                    ret = true;

                }
                hullDesc.lastReferenced = System.DateTime.Now;
                Hulls[shape.shapeKey] = hullDesc;
                break;
            case ShapeData.PhysicsShapeType.SHAPE_UNKNOWN:
                break;
            default:
                // Native shapes are not tracked and they don't go into any list
                break;
        }
        return ret;
    }

    // Release the usage of a shape.
    public void DereferenceShape(BulletShape shape, bool inTaintTime, ShapeDestructionCallback shapeCallback)
    {
        if (shape.ptr == IntPtr.Zero)
            return;

        BSScene.TaintCallback dereferenceOperation = delegate()
        {
            if (shape.ptr != IntPtr.Zero)
            {
                if (shape.isNativeShape)
                {
                    // Native shapes are not tracked and are released immediately
                    DetailLog("{0},BSShapeCollection.DereferenceShape,deleteNativeShape,ptr={1},taintTime={2}",
                                    BSScene.DetailLogZero, shape.ptr.ToString("X"), inTaintTime);
                    if (shapeCallback != null) shapeCallback(shape);
                    BulletSimAPI.DeleteCollisionShape2(PhysicsScene.World.ptr, shape.ptr);
                }
                else
                {
                    switch (shape.type)
                    {
                        case ShapeData.PhysicsShapeType.SHAPE_HULL:
                            DereferenceHull(shape, shapeCallback);
                            break;
                        case ShapeData.PhysicsShapeType.SHAPE_MESH:
                            DereferenceMesh(shape, shapeCallback);
                            break;
                        case ShapeData.PhysicsShapeType.SHAPE_UNKNOWN:
                            break;
                        default:
                            break;
                    }
                }
            }
        };
        if (inTaintTime)
        {
            lock (m_collectionActivityLock)
            {
                dereferenceOperation();
            }
        }
        else
        {
            PhysicsScene.TaintedObject("BSShapeCollection.DereferenceShape", dereferenceOperation);
        }
    }

    // Count down the reference count for a mesh shape
    // Called at taint-time.
    private void DereferenceMesh(BulletShape shape, ShapeDestructionCallback shapeCallback)
    {
        MeshDesc meshDesc;
        if (Meshes.TryGetValue(shape.shapeKey, out meshDesc))
        {
            meshDesc.referenceCount--;
            // TODO: release the Bullet storage
            if (shapeCallback != null) shapeCallback(shape);
            meshDesc.lastReferenced = System.DateTime.Now;
            Meshes[shape.shapeKey] = meshDesc;
            DetailLog("{0},BSShapeCollection.DereferenceMesh,key={1},refCnt={2}",
                    BSScene.DetailLogZero, shape.shapeKey.ToString("X"), meshDesc.referenceCount);

        }
    }

    // Count down the reference count for a hull shape
    // Called at taint-time.
    private void DereferenceHull(BulletShape shape, ShapeDestructionCallback shapeCallback)
    {
        HullDesc hullDesc;
        if (Hulls.TryGetValue(shape.shapeKey, out hullDesc))
        {
            hullDesc.referenceCount--;
            // TODO: release the Bullet storage (aging old entries?)
            if (shapeCallback != null) shapeCallback(shape);
            hullDesc.lastReferenced = System.DateTime.Now;
            Hulls[shape.shapeKey] = hullDesc;
            DetailLog("{0},BSShapeCollection.DereferenceHull,key={1},refCnt={2}",
                    BSScene.DetailLogZero, shape.shapeKey.ToString("X"), hullDesc.referenceCount);
        }
    }

    // Create the geometry information in Bullet for later use.
    // The objects needs a hull if it's physical otherwise a mesh is enough.
    // if 'forceRebuild' is true, the geometry is unconditionally rebuilt. For meshes and hulls,
    //     shared geometries will be used. If the parameters of the existing shape are the same
    //     as this request, the shape is not rebuilt.
    // Info in prim.BSShape is updated to the new shape.
    // Returns 'true' if the geometry was rebuilt.
    // Called at taint-time!
    private bool CreateGeom(bool forceRebuild, BSPhysObject prim, ShapeData shapeData,
                            PrimitiveBaseShape pbs, ShapeDestructionCallback shapeCallback)
    {
        bool ret = false;
        bool haveShape = false;
        bool nativeShapePossible = true;

        if (shapeData.Type == ShapeData.PhysicsShapeType.SHAPE_AVATAR)
        {
            // an avatar capsule is close to a native shape (it is not shared)
            ret = GetReferenceToNativeShape(prim, shapeData, ShapeData.PhysicsShapeType.SHAPE_AVATAR,
                            ShapeData.FixedShapeKey.KEY_CAPSULE, shapeCallback);
            DetailLog("{0},BSShapeCollection.CreateGeom,avatarCapsule,shape={1}", prim.LocalID, prim.BSShape);
            haveShape = true;
        }
        // If the prim attributes are simple, this could be a simple Bullet native shape
        if (!haveShape
                && pbs != null
                && nativeShapePossible
                && ((pbs.SculptEntry && !PhysicsScene.ShouldMeshSculptedPrim)
                    || (pbs.ProfileBegin == 0 && pbs.ProfileEnd == 0
                        && pbs.ProfileHollow == 0
                        && pbs.PathTwist == 0 && pbs.PathTwistBegin == 0
                        && pbs.PathBegin == 0 && pbs.PathEnd == 0
                        && pbs.PathTaperX == 0 && pbs.PathTaperY == 0
                        && pbs.PathScaleX == 100 && pbs.PathScaleY == 100
                        && pbs.PathShearX == 0 && pbs.PathShearY == 0) ) )
        {
            if ((pbs.ProfileShape == ProfileShape.HalfCircle && pbs.PathCurve == (byte)Extrusion.Curve1)
                                && pbs.Scale.X == pbs.Scale.Y && pbs.Scale.Y == pbs.Scale.Z)
            {
                haveShape = true;
                if (forceRebuild
                        || prim.Scale != shapeData.Size
                        || prim.BSShape.type != ShapeData.PhysicsShapeType.SHAPE_SPHERE
                        )
                {
                    ret = GetReferenceToNativeShape(prim, shapeData, ShapeData.PhysicsShapeType.SHAPE_SPHERE,
                                            ShapeData.FixedShapeKey.KEY_SPHERE, shapeCallback);
                    DetailLog("{0},BSShapeCollection.CreateGeom,sphere,force={1},shape={2}",
                                        prim.LocalID, forceRebuild, prim.BSShape);
                }
            }
            if (pbs.ProfileShape == ProfileShape.Square && pbs.PathCurve == (byte)Extrusion.Straight)
            {
                haveShape = true;
                if (forceRebuild
                        || prim.Scale != shapeData.Size
                        || prim.BSShape.type != ShapeData.PhysicsShapeType.SHAPE_BOX
                        )
                {
                    ret = GetReferenceToNativeShape( prim, shapeData, ShapeData.PhysicsShapeType.SHAPE_BOX,
                                            ShapeData.FixedShapeKey.KEY_BOX, shapeCallback);
                    DetailLog("{0},BSShapeCollection.CreateGeom,box,force={1},shape={2}",
                                        prim.LocalID, forceRebuild, prim.BSShape);
                }
            }
        }
        // If a simple shape is not happening, create a mesh and possibly a hull.
        // Note that if it's a native shape, the check for physical/non-physical is not
        //     made. Native shapes are best used in either case.
        if (!haveShape && pbs != null)
        {
            if (prim.IsPhysical && PhysicsScene.ShouldUseHullsForPhysicalObjects)
            {
                // Update prim.BSShape to reference a hull of this shape.
                ret = GetReferenceToHull(prim, shapeData, pbs, shapeCallback);
                DetailLog("{0},BSShapeCollection.CreateGeom,hull,shape={1},key={2}",
                                        shapeData.ID, prim.BSShape, prim.BSShape.shapeKey.ToString("X"));
            }
            else
            {
                ret = GetReferenceToMesh(prim, shapeData, pbs, shapeCallback);
                DetailLog("{0},BSShapeCollection.CreateGeom,mesh,shape={1},key={2}",
                                        shapeData.ID, prim.BSShape, prim.BSShape.shapeKey.ToString("X"));
            }
        }
        return ret;
    }

    // Creates a native shape and assignes it to prim.BSShape.
    // "Native" shapes are never shared. they are created here and destroyed in DereferenceShape().
    private bool GetReferenceToNativeShape(BSPhysObject prim, ShapeData shapeData,
                            ShapeData.PhysicsShapeType shapeType, ShapeData.FixedShapeKey shapeKey,
                            ShapeDestructionCallback shapeCallback)
    {

        shapeData.Type = shapeType;
        // Bullet native objects are scaled by the Bullet engine so pass the size in
        prim.Scale = shapeData.Size;
        shapeData.Scale = shapeData.Size;

        // release any previous shape
        DereferenceShape(prim.BSShape, true, shapeCallback);

        BulletShape newShape = BuildPhysicalNativeShape(shapeType, shapeData, shapeKey);

        // Don't need to do a 'ReferenceShape()' here because native shapes are not shared.
        DetailLog("{0},BSShapeCollection.AddNativeShapeToPrim,create,newshape={1},scale={2}",
                                shapeData.ID, newShape, shapeData.Scale);

        prim.BSShape = newShape;
        return true;
    }

    private BulletShape BuildPhysicalNativeShape(ShapeData.PhysicsShapeType shapeType,
                        ShapeData shapeData, ShapeData.FixedShapeKey shapeKey)
    {
        BulletShape newShape;
        // Need to make sure the passed shape information is for the native type.
        ShapeData nativeShapeData = shapeData;
        nativeShapeData.Type = shapeType;
        nativeShapeData.MeshKey = (ulong)shapeKey;
        nativeShapeData.HullKey = (ulong)shapeKey;

        if (shapeType == ShapeData.PhysicsShapeType.SHAPE_AVATAR)
        {
            newShape = new BulletShape(
                        BulletSimAPI.BuildCapsuleShape2(PhysicsScene.World.ptr, 1.0f, 1.0f, nativeShapeData.Scale), shapeType);
            DetailLog("{0},BSShapeCollection.BuiletPhysicalNativeShape,capsule,scale={1}", nativeShapeData.ID, nativeShapeData.Scale);
        }
        else
        {
            newShape = new BulletShape(BulletSimAPI.BuildNativeShape2(PhysicsScene.World.ptr, nativeShapeData), shapeType);
        }
        if (newShape.ptr == IntPtr.Zero)
        {
            PhysicsScene.Logger.ErrorFormat("{0} BuildPhysicalNativeShape failed. ID={1}, shape={2}",
                                    LogHeader, nativeShapeData.ID, nativeShapeData.Type);
        }
        newShape.shapeKey = (System.UInt64)shapeKey;
        newShape.isNativeShape = true;

        return newShape;
    }

    // Builds a mesh shape in the physical world and updates prim.BSShape.
    // Dereferences previous shape in BSShape and adds a reference for this new shape.
    // Returns 'true' of a mesh was actually built. Otherwise .
    // Called at taint-time!
    private bool GetReferenceToMesh(BSPhysObject prim, ShapeData shapeData, PrimitiveBaseShape pbs,
                                ShapeDestructionCallback shapeCallback)
    {
        BulletShape newShape = new BulletShape(IntPtr.Zero);

        float lod;
        System.UInt64 newMeshKey = ComputeShapeKey(shapeData, pbs, out lod);

        // if this new shape is the same as last time, don't recreate the mesh
        if (newMeshKey == prim.BSShape.shapeKey && prim.BSShape.type == ShapeData.PhysicsShapeType.SHAPE_MESH)
            return false;

        DetailLog("{0},BSShapeCollection.CreateGeomMesh,create,oldKey={1},newKey={2}",
                                prim.LocalID, prim.BSShape.shapeKey.ToString("X"), newMeshKey.ToString("X"));

        // Since we're recreating new, get rid of the reference to the previous shape
        DereferenceShape(prim.BSShape, true, shapeCallback);

        newShape = CreatePhysicalMesh(prim.PhysObjectName, newMeshKey, pbs, shapeData.Size, lod);
        // Take evasive action if the mesh was not constructed.
        newShape = VerifyMeshCreated(newShape, prim, shapeData, pbs);

        ReferenceShape(newShape);

        // meshes are already scaled by the meshmerizer
        prim.Scale = new OMV.Vector3(1f, 1f, 1f);
        prim.BSShape = newShape;

        return true;        // 'true' means a new shape has been added to this prim
    }

    private BulletShape CreatePhysicalMesh(string objName, System.UInt64 newMeshKey, PrimitiveBaseShape pbs, OMV.Vector3 size, float lod)
    {
        IMesh meshData = null;
        IntPtr meshPtr = IntPtr.Zero;
        MeshDesc meshDesc;
        if (Meshes.TryGetValue(newMeshKey, out meshDesc))
        {
            // If the mesh has already been built just use it.
            meshPtr = meshDesc.ptr;
        }
        else
        {
            // Pass false for physicalness as this creates some sort of bounding box which we don't need
            meshData = PhysicsScene.mesher.CreateMesh(objName, pbs, size, lod, false);

            if (meshData != null)
            {
                int[] indices = meshData.getIndexListAsInt();
                List<OMV.Vector3> vertices = meshData.getVertexList();

                float[] verticesAsFloats = new float[vertices.Count * 3];
                int vi = 0;
                foreach (OMV.Vector3 vv in vertices)
                {
                    verticesAsFloats[vi++] = vv.X;
                    verticesAsFloats[vi++] = vv.Y;
                    verticesAsFloats[vi++] = vv.Z;
                }

                // m_log.DebugFormat("{0}: CreateGeomMesh: calling CreateMesh. lid={1}, key={2}, indices={3}, vertices={4}",
                //                  LogHeader, prim.LocalID, newMeshKey, indices.Length, vertices.Count);

                meshPtr = BulletSimAPI.CreateMeshShape2(PhysicsScene.World.ptr,
                                    indices.GetLength(0), indices, vertices.Count, verticesAsFloats);
            }
        }
        BulletShape newShape = new BulletShape(meshPtr, ShapeData.PhysicsShapeType.SHAPE_MESH);
        newShape.shapeKey = newMeshKey;

        return newShape;
    }

    // See that hull shape exists in the physical world and update prim.BSShape.
    // We could be creating the hull because scale changed or whatever.
    private bool GetReferenceToHull(BSPhysObject prim, ShapeData shapeData, PrimitiveBaseShape pbs,
                                ShapeDestructionCallback shapeCallback)
    {
        BulletShape newShape;

        float lod;
        System.UInt64 newHullKey = ComputeShapeKey(shapeData, pbs, out lod);

        // if the hull hasn't changed, don't rebuild it
        if (newHullKey == prim.BSShape.shapeKey && prim.BSShape.type == ShapeData.PhysicsShapeType.SHAPE_HULL)
            return false;

        DetailLog("{0},BSShapeCollection.CreateGeomHull,create,oldKey={1},newKey={2}",
                        prim.LocalID, prim.BSShape.shapeKey.ToString("X"), newHullKey.ToString("X"));

        // Remove usage of the previous shape.
        DereferenceShape(prim.BSShape, true, shapeCallback);

        newShape = CreatePhysicalHull(prim.PhysObjectName, newHullKey, pbs, shapeData.Size, lod);
        newShape = VerifyMeshCreated(newShape, prim, shapeData, pbs);

        ReferenceShape(newShape);

        // hulls are already scaled by the meshmerizer
        prim.Scale = new OMV.Vector3(1f, 1f, 1f);
        prim.BSShape = newShape;
        return true;        // 'true' means a new shape has been added to this prim
    }

    List<ConvexResult> m_hulls;
    private BulletShape CreatePhysicalHull(string objName, System.UInt64 newHullKey, PrimitiveBaseShape pbs, OMV.Vector3 size, float lod)
    {

        IntPtr hullPtr = IntPtr.Zero;
        HullDesc hullDesc;
        if (Hulls.TryGetValue(newHullKey, out hullDesc))
        {
            // If the hull shape already is created, just use it.
            hullPtr = hullDesc.ptr;
        }
        else
        {
            // Build a new hull in the physical world
            // Pass false for physicalness as this creates some sort of bounding box which we don't need
            IMesh meshData = PhysicsScene.mesher.CreateMesh(objName, pbs, size, lod, false);
            if (meshData != null)
            {

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

                // setup and do convex hull conversion
                m_hulls = new List<ConvexResult>();
                DecompDesc dcomp = new DecompDesc();
                dcomp.mIndices = convIndices;
                dcomp.mVertices = convVertices;
                ConvexBuilder convexBuilder = new ConvexBuilder(HullReturn);
                // create the hull into the _hulls variable
                convexBuilder.process(dcomp);

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
                hullPtr = BulletSimAPI.CreateHullShape2(PhysicsScene.World.ptr, hullCount, convHulls);
            }
        }

        BulletShape newShape = new BulletShape(hullPtr, ShapeData.PhysicsShapeType.SHAPE_HULL);
        newShape.shapeKey = newHullKey;

        return newShape;        // 'true' means a new shape has been added to this prim
    }

    // Callback from convex hull creater with a newly created hull.
    // Just add it to our collection of hulls for this shape.
    private void HullReturn(ConvexResult result)
    {
        m_hulls.Add(result);
        return;
    }

    // Create a hash of all the shape parameters to be used as a key
    //    for this particular shape.
    private System.UInt64 ComputeShapeKey(ShapeData shapeData, PrimitiveBaseShape pbs, out float retLod)
    {
        // level of detail based on size and type of the object
        float lod = PhysicsScene.MeshLOD;
        if (pbs.SculptEntry)
            lod = PhysicsScene.SculptLOD;

        // Mega prims usually get more detail because one can interact with shape approximations at this size.
        float maxAxis = Math.Max(shapeData.Size.X, Math.Max(shapeData.Size.Y, shapeData.Size.Z));
        if (maxAxis > PhysicsScene.MeshMegaPrimThreshold)
            lod = PhysicsScene.MeshMegaPrimLOD;

        retLod = lod;
        return pbs.GetMeshKey(shapeData.Size, lod);
    }
    // For those who don't want the LOD
    private System.UInt64 ComputeShapeKey(ShapeData shapeData, PrimitiveBaseShape pbs)
    {
        float lod;
        return ComputeShapeKey(shapeData, pbs, out lod);
    }

    // The creation of a mesh or hull can fail if an underlying asset is not available.
    // There are two cases: 1) the asset is not in the cache and it needs to be fetched;
    //     and 2) the asset cannot be converted (like decompressing JPEG2000s).
    //     The first case causes the asset to be fetched. The second case just requires
    //     us to not loop forever.
    // Called after creating a physical mesh or hull. If the physical shape was created,
    //     just return.
    private BulletShape VerifyMeshCreated(BulletShape newShape, BSPhysObject prim, ShapeData shapeData, PrimitiveBaseShape pbs)
    {
        // If the shape was successfully created, nothing more to do
        if (newShape.ptr != IntPtr.Zero)
            return newShape;

        // If this mesh has an underlying asset and we have not failed getting it before, fetch the asset
        if (pbs.SculptEntry && !prim.LastAssetBuildFailed && pbs.SculptTexture != OMV.UUID.Zero)
        {
            prim.LastAssetBuildFailed = true;
            BSPhysObject xprim = prim;
            DetailLog("{0},BSShapeCollection.VerifyMeshCreated,fetchAsset,lID={1},lastFailed={2}",
                            LogHeader, shapeData.ID.ToString("X"), prim.LastAssetBuildFailed);
            Util.FireAndForget(delegate
                {
                    RequestAssetDelegate assetProvider = PhysicsScene.RequestAssetMethod;
                    if (assetProvider != null)
                    {
                        BSPhysObject yprim = xprim; // probably not necessary, but, just in case.
                        assetProvider(yprim.BaseShape.SculptTexture, delegate(AssetBase asset)
                        {
                            if (!yprim.BaseShape.SculptEntry)
                                return;
                            if (yprim.BaseShape.SculptTexture.ToString() != asset.ID)
                                return;

                            yprim.BaseShape.SculptData = asset.Data;
                            // This will cause the prim to see that the filler shape is not the right
                            //    one and try again to build the object.
                            // No race condition with the native sphere setting since the rebuild is at taint time.
                            yprim.ForceBodyShapeRebuild(false);

                        });
                    }
                });
        }
        else
        {
            if (prim.LastAssetBuildFailed)
            {
                PhysicsScene.Logger.ErrorFormat("{0} Mesh failed to fetch asset. lID={1}, texture={2}",
                                            LogHeader, shapeData.ID, pbs.SculptTexture);
            }
        }

        // While we figure out the real problem, stick a simple native shape on the object.
        BulletShape fillinShape =
            BuildPhysicalNativeShape(ShapeData.PhysicsShapeType.SHAPE_BOX, shapeData, ShapeData.FixedShapeKey.KEY_BOX);

        return fillinShape;
    }

    // Create a body object in Bullet.
    // Updates prim.BSBody with the information about the new body if one is created.
    // Returns 'true' if an object was actually created.
    // Called at taint-time.
    private bool CreateBody(bool forceRebuild, BSPhysObject prim, BulletSim sim, BulletShape shape,
                            ShapeData shapeData, BodyDestructionCallback bodyCallback)
    {
        bool ret = false;

        // the mesh, hull or native shape must have already been created in Bullet
        bool mustRebuild = (prim.BSBody.ptr == IntPtr.Zero);

        // If there is an existing body, verify it's of an acceptable type.
        // If not a solid object, body is a GhostObject. Otherwise a RigidBody.
        if (!mustRebuild)
        {
            CollisionObjectTypes bodyType = (CollisionObjectTypes)BulletSimAPI.GetBodyType2(prim.BSBody.ptr);
            if (prim.IsSolid && bodyType != CollisionObjectTypes.CO_RIGID_BODY
                || !prim.IsSolid && bodyType != CollisionObjectTypes.CO_GHOST_OBJECT)
            {
                // If the collisionObject is not the correct type for solidness, rebuild what's there
                mustRebuild = true;
            }

        }

        if (mustRebuild || forceRebuild)
        {
            // Free any old body
            DereferenceBody(prim.BSBody, true, bodyCallback);

            BulletBody aBody;
            IntPtr bodyPtr = IntPtr.Zero;
            if (prim.IsSolid)
            {
                bodyPtr = BulletSimAPI.CreateBodyFromShape2(sim.ptr, shape.ptr,
                                        shapeData.ID, shapeData.Position, shapeData.Rotation);
                DetailLog("{0},BSShapeCollection.CreateBody,mesh,ptr={1}", prim.LocalID, bodyPtr.ToString("X"));
            }
            else
            {
                bodyPtr = BulletSimAPI.CreateGhostFromShape2(sim.ptr, shape.ptr,
                                        shapeData.ID, shapeData.Position, shapeData.Rotation);
                DetailLog("{0},BSShapeCollection.CreateBody,ghost,ptr={1}", prim.LocalID, bodyPtr.ToString("X"));
            }
            aBody = new BulletBody(shapeData.ID, bodyPtr);

            ReferenceBody(aBody, true);

            prim.BSBody = aBody;

            ret = true;
        }

        return ret;
    }

    private void DetailLog(string msg, params Object[] args)
    {
        if (PhysicsScene.PhysicsLogging.Enabled)
            PhysicsScene.DetailLog(msg, args);
    }
}
}
