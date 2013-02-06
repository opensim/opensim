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
public sealed class BSShapeCollection : IDisposable
{
    private static string LogHeader = "[BULLETSIM SHAPE COLLECTION]";

    private BSScene PhysicsScene { get; set; }

    private Object m_collectionActivityLock = new Object();

    // Description of a Mesh
    private struct MeshDesc
    {
        public BulletShape shape;
        public int referenceCount;
        public DateTime lastReferenced;
        public UInt64 shapeKey;
    }

    // Description of a hull.
    // Meshes and hulls have the same shape hash key but we only need hulls for efficient collision calculations.
    private struct HullDesc
    {
        public BulletShape shape;
        public int referenceCount;
        public DateTime lastReferenced;
        public UInt64 shapeKey;
    }

    // The sharable set of meshes and hulls. Indexed by their shape hash.
    private Dictionary<System.UInt64, MeshDesc> Meshes = new Dictionary<System.UInt64, MeshDesc>();
    private Dictionary<System.UInt64, HullDesc> Hulls = new Dictionary<System.UInt64, HullDesc>();

    private bool DDetail = false;

    public BSShapeCollection(BSScene physScene)
    {
        PhysicsScene = physScene;
        // Set the next to 'true' for very detailed shape update detailed logging (detailed details?)
        // While detailed debugging is still active, this is better than commenting out all the
        //     DetailLog statements. When debugging slows down, this and the protected logging
        //     statements can be commented/removed.
        DDetail = true;
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
    public bool GetBodyAndShape(bool forceRebuild, BulletWorld sim, BSPhysObject prim,
                    ShapeDestructionCallback shapeCallback, BodyDestructionCallback bodyCallback)
    {
        PhysicsScene.AssertInTaintTime("BSShapeCollection.GetBodyAndShape");

        bool ret = false;

        // This lock could probably be pushed down lower but building shouldn't take long
        lock (m_collectionActivityLock)
        {
            // Do we have the correct geometry for this type of object?
            // Updates prim.BSShape with information/pointers to shape.
            // Returns 'true' of BSShape is changed to a new shape.
            bool newGeom = CreateGeom(forceRebuild, prim, shapeCallback);
            // If we had to select a new shape geometry for the object,
            //    rebuild the body around it.
            // Updates prim.BSBody with information/pointers to requested body
            // Returns 'true' if BSBody was changed.
            bool newBody = CreateBody((newGeom || forceRebuild), prim, PhysicsScene.World, bodyCallback);
            ret = newGeom || newBody;
        }
        DetailLog("{0},BSShapeCollection.GetBodyAndShape,taintExit,force={1},ret={2},body={3},shape={4}",
                                prim.LocalID, forceRebuild, ret, prim.PhysBody, prim.PhysShape);

        return ret;
    }

    public bool GetBodyAndShape(bool forceRebuild, BulletWorld sim, BSPhysObject prim)
    {
        return GetBodyAndShape(forceRebuild, sim, prim, null, null);
    }

    // Track another user of a body.
    // We presume the caller has allocated the body.
    // Bodies only have one user so the body is just put into the world if not already there.
    private void ReferenceBody(BulletBody body)
    {
        lock (m_collectionActivityLock)
        {
            if (DDetail) DetailLog("{0},BSShapeCollection.ReferenceBody,newBody,body={1}", body.ID, body);
            if (!PhysicsScene.PE.IsInWorld(PhysicsScene.World, body))
            {
                PhysicsScene.PE.AddObjectToWorld(PhysicsScene.World, body);
                if (DDetail) DetailLog("{0},BSShapeCollection.ReferenceBody,addedToWorld,ref={1}", body.ID, body);
            }
        }
    }

    // Release the usage of a body.
    // Called when releasing use of a BSBody. BSShape is handled separately.
    // Called in taint time.
    public void DereferenceBody(BulletBody body, BodyDestructionCallback bodyCallback )
    {
        if (!body.HasPhysicalBody)
            return;

        PhysicsScene.AssertInTaintTime("BSShapeCollection.DereferenceBody");

        lock (m_collectionActivityLock)
        {
            if (DDetail) DetailLog("{0},BSShapeCollection.DereferenceBody,DestroyingBody,body={1}", body.ID, body);
            // If the caller needs to know the old body is going away, pass the event up.
            if (bodyCallback != null) bodyCallback(body);

            if (PhysicsScene.PE.IsInWorld(PhysicsScene.World, body))
            {
                PhysicsScene.PE.RemoveObjectFromWorld(PhysicsScene.World, body);
                if (DDetail) DetailLog("{0},BSShapeCollection.DereferenceBody,removingFromWorld. Body={1}", body.ID, body);
            }

            // Zero any reference to the shape so it is not freed when the body is deleted.
            PhysicsScene.PE.SetCollisionShape(PhysicsScene.World, body, null);
            PhysicsScene.PE.DestroyObject(PhysicsScene.World, body);
        }
    }

    // Track the datastructures and use count for a shape.
    // When creating a hull, this is called first to reference the mesh
    //     and then again to reference the hull.
    // Meshes and hulls for the same shape have the same hash key.
    // NOTE that native shapes are not added to the mesh list or removed.
    // Returns 'true' if this is the initial reference to the shape. Otherwise reused.
    public bool ReferenceShape(BulletShape shape)
    {
        bool ret = false;
        switch (shape.type)
        {
            case BSPhysicsShapeType.SHAPE_MESH:
                MeshDesc meshDesc;
                if (Meshes.TryGetValue(shape.shapeKey, out meshDesc))
                {
                    // There is an existing instance of this mesh.
                    meshDesc.referenceCount++;
                    if (DDetail) DetailLog("{0},BSShapeCollection.ReferenceShape,existingMesh,key={1},cnt={2}",
                                BSScene.DetailLogZero, shape.shapeKey.ToString("X"), meshDesc.referenceCount);
                }
                else
                {
                    // This is a new reference to a mesh
                    meshDesc.shape = shape.Clone();
                    meshDesc.shapeKey = shape.shapeKey;
                    // We keep a reference to the underlying IMesh data so a hull can be built
                    meshDesc.referenceCount = 1;
                    if (DDetail) DetailLog("{0},BSShapeCollection.ReferenceShape,newMesh,key={1},cnt={2}",
                                BSScene.DetailLogZero, shape.shapeKey.ToString("X"), meshDesc.referenceCount);
                    ret = true;
                }
                meshDesc.lastReferenced = System.DateTime.Now;
                Meshes[shape.shapeKey] = meshDesc;
                break;
            case BSPhysicsShapeType.SHAPE_HULL:
                HullDesc hullDesc;
                if (Hulls.TryGetValue(shape.shapeKey, out hullDesc))
                {
                    // There is an existing instance of this hull.
                    hullDesc.referenceCount++;
                    if (DDetail) DetailLog("{0},BSShapeCollection.ReferenceShape,existingHull,key={1},cnt={2}",
                                BSScene.DetailLogZero, shape.shapeKey.ToString("X"), hullDesc.referenceCount);
                }
                else
                {
                    // This is a new reference to a hull
                    hullDesc.shape = shape.Clone();
                    hullDesc.shapeKey = shape.shapeKey;
                    hullDesc.referenceCount = 1;
                    if (DDetail) DetailLog("{0},BSShapeCollection.ReferenceShape,newHull,key={1},cnt={2}",
                                BSScene.DetailLogZero, shape.shapeKey.ToString("X"), hullDesc.referenceCount);
                    ret = true;

                }
                hullDesc.lastReferenced = System.DateTime.Now;
                Hulls[shape.shapeKey] = hullDesc;
                break;
            case BSPhysicsShapeType.SHAPE_UNKNOWN:
                break;
            default:
                // Native shapes are not tracked and they don't go into any list
                break;
        }
        return ret;
    }

    // Release the usage of a shape.
    public void DereferenceShape(BulletShape shape, ShapeDestructionCallback shapeCallback)
    {
        if (!shape.HasPhysicalShape)
            return;

        PhysicsScene.AssertInTaintTime("BSShapeCollection.DereferenceShape");

        if (shape.HasPhysicalShape)
        {
            if (shape.isNativeShape)
            {
                // Native shapes are not tracked and are released immediately
                if (DDetail) DetailLog("{0},BSShapeCollection.DereferenceShape,deleteNativeShape,ptr={1}",
                                    BSScene.DetailLogZero, shape.AddrString);
                if (shapeCallback != null) shapeCallback(shape);
                PhysicsScene.PE.DeleteCollisionShape(PhysicsScene.World, shape);
            }
            else
            {
                switch (shape.type)
                {
                    case BSPhysicsShapeType.SHAPE_HULL:
                        DereferenceHull(shape, shapeCallback);
                        break;
                    case BSPhysicsShapeType.SHAPE_MESH:
                        DereferenceMesh(shape, shapeCallback);
                        break;
                    case BSPhysicsShapeType.SHAPE_COMPOUND:
                        DereferenceCompound(shape, shapeCallback);
                        break;
                    case BSPhysicsShapeType.SHAPE_UNKNOWN:
                        break;
                    default:
                        break;
                }
            }
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
            if (DDetail) DetailLog("{0},BSShapeCollection.DereferenceMesh,shape={1},refCnt={2}",
                                BSScene.DetailLogZero, shape, meshDesc.referenceCount);

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

            // Tell upper layers that, if they have dependencies on this shape, this link is going away
            if (shapeCallback != null) shapeCallback(shape);

            hullDesc.lastReferenced = System.DateTime.Now;
            Hulls[shape.shapeKey] = hullDesc;
            if (DDetail) DetailLog("{0},BSShapeCollection.DereferenceHull,shape={1},refCnt={2}",
                    BSScene.DetailLogZero, shape, hullDesc.referenceCount);
        }
    }

    // Remove a reference to a compound shape.
    // Taking a compound shape apart is a little tricky because if you just delete the
    //      physical shape, it will free all the underlying children. We can't do that because
    //      they could be shared. So, this removes each of the children from the compound and
    //      dereferences them separately before destroying the compound collision object itself.
    // Called at taint-time.
    private void DereferenceCompound(BulletShape shape, ShapeDestructionCallback shapeCallback)
    {
        if (!PhysicsScene.PE.IsCompound(shape))
        {
            // Failed the sanity check!!
            PhysicsScene.Logger.ErrorFormat("{0} Attempt to free a compound shape that is not compound!! type={1}, ptr={2}",
                                        LogHeader, shape.type, shape.AddrString);
            if (DDetail) DetailLog("{0},BSShapeCollection.DereferenceCompound,notACompoundShape,type={1},ptr={2}",
                                        BSScene.DetailLogZero, shape.type, shape.AddrString);
            return;
        }

        int numChildren = PhysicsScene.PE.GetNumberOfCompoundChildren(shape);
        if (DDetail) DetailLog("{0},BSShapeCollection.DereferenceCompound,shape={1},children={2}", BSScene.DetailLogZero, shape, numChildren);

        for (int ii = numChildren - 1; ii >= 0; ii--)
        {
            BulletShape childShape = PhysicsScene.PE.RemoveChildShapeFromCompoundShapeIndex(shape, ii);
            DereferenceAnonCollisionShape(childShape);
        }
        PhysicsScene.PE.DeleteCollisionShape(PhysicsScene.World, shape);
    }

    // Sometimes we have a pointer to a collision shape but don't know what type it is.
    // Figure out type and call the correct dereference routine.
    // Called at taint-time.
    private void DereferenceAnonCollisionShape(BulletShape shapeInfo)
    {
        MeshDesc meshDesc;
        HullDesc hullDesc;

        if (TryGetMeshByPtr(shapeInfo, out meshDesc))
        {
            shapeInfo.type = BSPhysicsShapeType.SHAPE_MESH;
            shapeInfo.shapeKey = meshDesc.shapeKey;
        }
        else
        {
            if (TryGetHullByPtr(shapeInfo, out hullDesc))
            {
                shapeInfo.type = BSPhysicsShapeType.SHAPE_HULL;
                shapeInfo.shapeKey = hullDesc.shapeKey;
            }
            else
            {
                if (PhysicsScene.PE.IsCompound(shapeInfo))
                {
                    shapeInfo.type = BSPhysicsShapeType.SHAPE_COMPOUND;
                }
                else
                {
                    if (PhysicsScene.PE.IsNativeShape(shapeInfo))
                    {
                        shapeInfo.isNativeShape = true;
                        shapeInfo.type = BSPhysicsShapeType.SHAPE_BOX; // (technically, type doesn't matter)
                    }
                }
            }
        }

        if (DDetail) DetailLog("{0},BSShapeCollection.DereferenceAnonCollisionShape,shape={1}", BSScene.DetailLogZero, shapeInfo);

        if (shapeInfo.type != BSPhysicsShapeType.SHAPE_UNKNOWN)
        {
            DereferenceShape(shapeInfo, null);
        }
        else
        {
            PhysicsScene.Logger.ErrorFormat("{0} Could not decypher shape type. Region={1}, addr={2}",
                                    LogHeader, PhysicsScene.RegionName, shapeInfo.AddrString);
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
    private bool CreateGeom(bool forceRebuild, BSPhysObject prim, ShapeDestructionCallback shapeCallback)
    {
        bool ret = false;
        bool haveShape = false;

        if (!haveShape && prim.PreferredPhysicalShape == BSPhysicsShapeType.SHAPE_CAPSULE)
        {
            // an avatar capsule is close to a native shape (it is not shared)
            GetReferenceToNativeShape(prim, BSPhysicsShapeType.SHAPE_CAPSULE, FixedShapeKey.KEY_CAPSULE, shapeCallback);
            if (DDetail) DetailLog("{0},BSShapeCollection.CreateGeom,avatarCapsule,shape={1}", prim.LocalID, prim.PhysShape);
            ret = true;
            haveShape = true;
        }

        // Compound shapes are handled special as they are rebuilt from scratch.
        // This isn't too great a hardship since most of the child shapes will have already been created.
        if (!haveShape && prim.PreferredPhysicalShape == BSPhysicsShapeType.SHAPE_COMPOUND)
        {
            ret = GetReferenceToCompoundShape(prim, shapeCallback);
            if (DDetail) DetailLog("{0},BSShapeCollection.CreateGeom,compoundShape,shape={1}", prim.LocalID, prim.PhysShape);
            haveShape = true;
        }

        if (!haveShape)
        {
            ret = CreateGeomNonSpecial(forceRebuild, prim, shapeCallback);
        }

        return ret;
    }

    // Create a mesh, hull or native shape.
    // Return 'true' if the prim's shape was changed.
    public bool CreateGeomNonSpecial(bool forceRebuild, BSPhysObject prim, ShapeDestructionCallback shapeCallback)
    {
        bool ret = false;
        bool haveShape = false;
        bool nativeShapePossible = true;
        PrimitiveBaseShape pbs = prim.BaseShape;

        // If the prim attributes are simple, this could be a simple Bullet native shape
        if (!haveShape
                && pbs != null
                && !pbs.SculptEntry
                && nativeShapePossible
                && ((pbs.SculptEntry && !BSParam.ShouldMeshSculptedPrim)
                    || (pbs.ProfileBegin == 0 && pbs.ProfileEnd == 0
                        && pbs.ProfileHollow == 0
                        && pbs.PathTwist == 0 && pbs.PathTwistBegin == 0
                        && pbs.PathBegin == 0 && pbs.PathEnd == 0
                        && pbs.PathTaperX == 0 && pbs.PathTaperY == 0
                        && pbs.PathScaleX == 100 && pbs.PathScaleY == 100
                        && pbs.PathShearX == 0 && pbs.PathShearY == 0) ) )
        {
            // Get the scale of any existing shape so we can see if the new shape is same native type and same size.
            OMV.Vector3 scaleOfExistingShape = OMV.Vector3.Zero;
            if (prim.PhysShape.HasPhysicalShape)
                scaleOfExistingShape = PhysicsScene.PE.GetLocalScaling(prim.PhysShape);

            if (DDetail) DetailLog("{0},BSShapeCollection.CreateGeom,maybeNative,force={1},primScale={2},primSize={3},primShape={4}",
                        prim.LocalID, forceRebuild, prim.Scale, prim.Size, prim.PhysShape.type);

            // It doesn't look like Bullet scales native spheres so make sure the scales are all equal
            if ((pbs.ProfileShape == ProfileShape.HalfCircle && pbs.PathCurve == (byte)Extrusion.Curve1)
                                && pbs.Scale.X == pbs.Scale.Y && pbs.Scale.Y == pbs.Scale.Z)
            {
                haveShape = true;
                if (forceRebuild
                        || prim.Scale != scaleOfExistingShape
                        || prim.PhysShape.type != BSPhysicsShapeType.SHAPE_SPHERE
                        )
                {
                    ret = GetReferenceToNativeShape(prim, BSPhysicsShapeType.SHAPE_SPHERE,
                                            FixedShapeKey.KEY_SPHERE, shapeCallback);
                }
                if (DDetail) DetailLog("{0},BSShapeCollection.CreateGeom,sphere,force={1},rebuilt={2},shape={3}",
                                        prim.LocalID, forceRebuild, ret, prim.PhysShape);
            }
            if (!haveShape && pbs.ProfileShape == ProfileShape.Square && pbs.PathCurve == (byte)Extrusion.Straight)
            {
                haveShape = true;
                if (forceRebuild
                        || prim.Scale != scaleOfExistingShape
                        || prim.PhysShape.type != BSPhysicsShapeType.SHAPE_BOX
                        )
                {
                    ret = GetReferenceToNativeShape( prim, BSPhysicsShapeType.SHAPE_BOX,
                                            FixedShapeKey.KEY_BOX, shapeCallback);
                }
                if (DDetail) DetailLog("{0},BSShapeCollection.CreateGeom,box,force={1},rebuilt={2},shape={3}",
                                        prim.LocalID, forceRebuild, ret, prim.PhysShape);
            }
        }

        // If a simple shape is not happening, create a mesh and possibly a hull.
        if (!haveShape && pbs != null)
        {
            ret = CreateGeomMeshOrHull(prim, shapeCallback);
        }

        return ret;
    }

    // return 'true' if the prim's shape was changed.
    public bool CreateGeomMeshOrHull(BSPhysObject prim, ShapeDestructionCallback shapeCallback)
    {

        bool ret = false;
        // Note that if it's a native shape, the check for physical/non-physical is not
        //     made. Native shapes work in either case.
        if (prim.IsPhysical && BSParam.ShouldUseHullsForPhysicalObjects)
        {
            // Update prim.BSShape to reference a hull of this shape.
            ret = GetReferenceToHull(prim,shapeCallback);
            if (DDetail) DetailLog("{0},BSShapeCollection.CreateGeom,hull,shape={1},key={2}",
                                    prim.LocalID, prim.PhysShape, prim.PhysShape.shapeKey.ToString("X"));
        }
        else
        {
            ret = GetReferenceToMesh(prim, shapeCallback);
            if (DDetail) DetailLog("{0},BSShapeCollection.CreateGeom,mesh,shape={1},key={2}",
                                    prim.LocalID, prim.PhysShape, prim.PhysShape.shapeKey.ToString("X"));
        }
        return ret;
    }

    // Creates a native shape and assignes it to prim.BSShape.
    // "Native" shapes are never shared. they are created here and destroyed in DereferenceShape().
    private bool GetReferenceToNativeShape(BSPhysObject prim,
                            BSPhysicsShapeType shapeType, FixedShapeKey shapeKey,
                            ShapeDestructionCallback shapeCallback)
    {
        // release any previous shape
        DereferenceShape(prim.PhysShape, shapeCallback);

        BulletShape newShape = BuildPhysicalNativeShape(prim, shapeType, shapeKey);

        // Don't need to do a 'ReferenceShape()' here because native shapes are not shared.
        if (DDetail) DetailLog("{0},BSShapeCollection.AddNativeShapeToPrim,create,newshape={1},scale={2}",
                                prim.LocalID, newShape, prim.Scale);

        // native shapes are scaled by Bullet
        prim.PhysShape = newShape;
        return true;
    }

    private BulletShape BuildPhysicalNativeShape(BSPhysObject prim, BSPhysicsShapeType shapeType,
                                    FixedShapeKey shapeKey)
    {
        BulletShape newShape;
        // Need to make sure the passed shape information is for the native type.
        ShapeData nativeShapeData = new ShapeData();
        nativeShapeData.Type = shapeType;
        nativeShapeData.ID = prim.LocalID;
        nativeShapeData.Scale = prim.Scale;
        nativeShapeData.Size = prim.Scale;  // unneeded, I think.
        nativeShapeData.MeshKey = (ulong)shapeKey;
        nativeShapeData.HullKey = (ulong)shapeKey;

        if (shapeType == BSPhysicsShapeType.SHAPE_CAPSULE)
        {

            newShape = PhysicsScene.PE.BuildCapsuleShape(PhysicsScene.World, 1f, 1f, prim.Scale);
            if (DDetail) DetailLog("{0},BSShapeCollection.BuiletPhysicalNativeShape,capsule,scale={1}", prim.LocalID, prim.Scale);
        }
        else
        {
            // Native shapes are scaled in Bullet so set the scaling to the size
            newShape = PhysicsScene.PE.BuildNativeShape(PhysicsScene.World, nativeShapeData);

        }
        if (!newShape.HasPhysicalShape)
        {
            PhysicsScene.Logger.ErrorFormat("{0} BuildPhysicalNativeShape failed. ID={1}, shape={2}",
                                    LogHeader, prim.LocalID, shapeType);
        }
        newShape.shapeKey = (System.UInt64)shapeKey;
        newShape.isNativeShape = true;

        return newShape;
    }

    // Builds a mesh shape in the physical world and updates prim.BSShape.
    // Dereferences previous shape in BSShape and adds a reference for this new shape.
    // Returns 'true' of a mesh was actually built. Otherwise .
    // Called at taint-time!
    private bool GetReferenceToMesh(BSPhysObject prim, ShapeDestructionCallback shapeCallback)
    {
        BulletShape newShape = new BulletShape();

        float lod;
        System.UInt64 newMeshKey = ComputeShapeKey(prim.Size, prim.BaseShape, out lod);

        // if this new shape is the same as last time, don't recreate the mesh
        if (newMeshKey == prim.PhysShape.shapeKey && prim.PhysShape.type == BSPhysicsShapeType.SHAPE_MESH)
            return false;

        if (DDetail) DetailLog("{0},BSShapeCollection.GetReferenceToMesh,create,oldKey={1},newKey={2},size={3},lod={4}",
                                prim.LocalID, prim.PhysShape.shapeKey.ToString("X"), newMeshKey.ToString("X"), prim.Size, lod);

        // Since we're recreating new, get rid of the reference to the previous shape
        DereferenceShape(prim.PhysShape, shapeCallback);

        newShape = CreatePhysicalMesh(prim.PhysObjectName, newMeshKey, prim.BaseShape, prim.Size, lod);
        // Take evasive action if the mesh was not constructed.
        newShape = VerifyMeshCreated(newShape, prim);

        ReferenceShape(newShape);

        prim.PhysShape = newShape;

        return true;        // 'true' means a new shape has been added to this prim
    }

    private BulletShape CreatePhysicalMesh(string objName, System.UInt64 newMeshKey, PrimitiveBaseShape pbs, OMV.Vector3 size, float lod)
    {
        BulletShape newShape = new BulletShape();

        MeshDesc meshDesc;
        if (Meshes.TryGetValue(newMeshKey, out meshDesc))
        {
            // If the mesh has already been built just use it.
            newShape = meshDesc.shape.Clone();
        }
        else
        {
            IMesh meshData = PhysicsScene.mesher.CreateMesh(objName, pbs, size, lod, 
                                        true,
                                        false,  // say it is not physical so a bounding box is not built
                                        false,  // do not cache the mesh and do not use previously built versions
                                        false   // It's NOT for ODE
                                        );

            if (meshData != null)
            {

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
                        int v1 = indices[tri + 0] * 3;
                        int v2 = indices[tri + 1] * 3;
                        int v3 = indices[tri + 2] * 3;
                        if (!((verticesAsFloats[v1 + 0] == verticesAsFloats[v2 + 0]
                               && verticesAsFloats[v1 + 1] == verticesAsFloats[v2 + 1]
                               && verticesAsFloats[v1 + 2] == verticesAsFloats[v2 + 2])
                            || (verticesAsFloats[v2 + 0] == verticesAsFloats[v3 + 0]
                               && verticesAsFloats[v2 + 1] == verticesAsFloats[v3 + 1]
                               && verticesAsFloats[v2 + 2] == verticesAsFloats[v3 + 2])
                            || (verticesAsFloats[v1 + 0] == verticesAsFloats[v3 + 0]
                               && verticesAsFloats[v1 + 1] == verticesAsFloats[v3 + 1]
                               && verticesAsFloats[v1 + 2] == verticesAsFloats[v3 + 2]))
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
                DetailLog("{0},BSShapeCollection.CreatePhysicalMesh,origTri={1},realTri={2},numVerts={3}",
                            BSScene.DetailLogZero, indices.Length / 3, realIndicesIndex / 3, verticesAsFloats.Length / 3);

                newShape = PhysicsScene.PE.CreateMeshShape(PhysicsScene.World,
                                    realIndicesIndex, indices, verticesAsFloats.Length/3, verticesAsFloats);
            }
        }
        newShape.shapeKey = newMeshKey;

        return newShape;
    }

    // See that hull shape exists in the physical world and update prim.BSShape.
    // We could be creating the hull because scale changed or whatever.
    private bool GetReferenceToHull(BSPhysObject prim, ShapeDestructionCallback shapeCallback)
    {
        BulletShape newShape;

        float lod;
        System.UInt64 newHullKey = ComputeShapeKey(prim.Size, prim.BaseShape, out lod);

        // if the hull hasn't changed, don't rebuild it
        if (newHullKey == prim.PhysShape.shapeKey && prim.PhysShape.type == BSPhysicsShapeType.SHAPE_HULL)
            return false;

        if (DDetail) DetailLog("{0},BSShapeCollection.GetReferenceToHull,create,oldKey={1},newKey={2}",
                        prim.LocalID, prim.PhysShape.shapeKey.ToString("X"), newHullKey.ToString("X"));

        // Remove usage of the previous shape.
        DereferenceShape(prim.PhysShape, shapeCallback);

        newShape = CreatePhysicalHull(prim.PhysObjectName, newHullKey, prim.BaseShape, prim.Size, lod);
        newShape = VerifyMeshCreated(newShape, prim);

        ReferenceShape(newShape);

        prim.PhysShape = newShape;
        return true;        // 'true' means a new shape has been added to this prim
    }

    List<ConvexResult> m_hulls;
    private BulletShape CreatePhysicalHull(string objName, System.UInt64 newHullKey, PrimitiveBaseShape pbs, OMV.Vector3 size, float lod)
    {

        BulletShape newShape = new BulletShape();
        IntPtr hullPtr = IntPtr.Zero;

        HullDesc hullDesc;
        if (Hulls.TryGetValue(newHullKey, out hullDesc))
        {
            // If the hull shape already is created, just use it.
            newShape = hullDesc.shape.Clone();
        }
        else
        {
            // Build a new hull in the physical world
            // Pass true for physicalness as this creates some sort of bounding box which we don't need
            IMesh meshData = PhysicsScene.mesher.CreateMesh(objName, pbs, size, lod, true, false, false, false);
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
                newShape = PhysicsScene.PE.CreateHullShape(PhysicsScene.World, hullCount, convHulls);
            }
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

    // Compound shapes are always built from scratch.
    // This shouldn't be to bad since most of the parts will be meshes that had been built previously.
    private bool GetReferenceToCompoundShape(BSPhysObject prim, ShapeDestructionCallback shapeCallback)
    {
        // Remove reference to the old shape
        // Don't need to do this as the shape is freed when the new root shape is created below.
        // DereferenceShape(prim.PhysShape, true, shapeCallback);

        BulletShape cShape = PhysicsScene.PE.CreateCompoundShape(PhysicsScene.World, false);

        // Create the shape for the root prim and add it to the compound shape. Cannot be a native shape.
        CreateGeomMeshOrHull(prim, shapeCallback);
        PhysicsScene.PE.AddChildShapeToCompoundShape(cShape, prim.PhysShape, OMV.Vector3.Zero, OMV.Quaternion.Identity);
        if (DDetail) DetailLog("{0},BSShapeCollection.GetReferenceToCompoundShape,addRootPrim,compShape={1},rootShape={2}",
                                    prim.LocalID, cShape, prim.PhysShape);

        prim.PhysShape = cShape;

        return true;
    }

    // Create a hash of all the shape parameters to be used as a key
    //    for this particular shape.
    private System.UInt64 ComputeShapeKey(OMV.Vector3 size, PrimitiveBaseShape pbs, out float retLod)
    {
        // level of detail based on size and type of the object
        float lod = BSParam.MeshLOD;

        // prims with curvy internal cuts need higher lod
        if (pbs.HollowShape == HollowShape.Circle)
            lod = BSParam.MeshCircularLOD;

        if (pbs.SculptEntry)
            lod = BSParam.SculptLOD;

        // Mega prims usually get more detail because one can interact with shape approximations at this size.
        float maxAxis = Math.Max(size.X, Math.Max(size.Y, size.Z));
        if (maxAxis > BSParam.MeshMegaPrimThreshold)
            lod = BSParam.MeshMegaPrimLOD;

        retLod = lod;
        return pbs.GetMeshKey(size, lod);
    }
    // For those who don't want the LOD
    private System.UInt64 ComputeShapeKey(OMV.Vector3 size, PrimitiveBaseShape pbs)
    {
        float lod;
        return ComputeShapeKey(size, pbs, out lod);
    }

    // The creation of a mesh or hull can fail if an underlying asset is not available.
    // There are two cases: 1) the asset is not in the cache and it needs to be fetched;
    //     and 2) the asset cannot be converted (like failed decompression of JPEG2000s).
    //     The first case causes the asset to be fetched. The second case requires
    //     us to not loop forever.
    // Called after creating a physical mesh or hull. If the physical shape was created,
    //     just return.
    private BulletShape VerifyMeshCreated(BulletShape newShape, BSPhysObject prim)
    {
        // If the shape was successfully created, nothing more to do
        if (newShape.HasPhysicalShape)
            return newShape;

        // If this mesh has an underlying asset and we have not failed getting it before, fetch the asset
        if (prim.BaseShape.SculptEntry && !prim.LastAssetBuildFailed && prim.BaseShape.SculptTexture != OMV.UUID.Zero)
        {
            prim.LastAssetBuildFailed = true;
            BSPhysObject xprim = prim;
            DetailLog("{0},BSShapeCollection.VerifyMeshCreated,fetchAsset,lastFailed={1}", prim.LocalID, prim.LastAssetBuildFailed);
            Util.FireAndForget(delegate
                {
                    RequestAssetDelegate assetProvider = PhysicsScene.RequestAssetMethod;
                    if (assetProvider != null)
                    {
                        BSPhysObject yprim = xprim; // probably not necessary, but, just in case.
                        assetProvider(yprim.BaseShape.SculptTexture, delegate(AssetBase asset)
                        {
                            bool assetFound = false;            // DEBUG DEBUG
                            string mismatchIDs = String.Empty;  // DEBUG DEBUG
                            if (yprim.BaseShape.SculptEntry)
                            {
                                if (yprim.BaseShape.SculptTexture.ToString() == asset.ID)
                                {
                                    yprim.BaseShape.SculptData = asset.Data;
                                    // This will cause the prim to see that the filler shape is not the right
                                    //    one and try again to build the object.
                                    // No race condition with the normal shape setting since the rebuild is at taint time.
                                    yprim.ForceBodyShapeRebuild(false /* inTaintTime */);
                                    assetFound = true;
                                }
                                else
                                {
                                    mismatchIDs = yprim.BaseShape.SculptTexture.ToString() + "/" + asset.ID;
                                }
                            }
                            DetailLog("{0},BSShapeCollection,fetchAssetCallback,found={1},isSculpt={2},ids={3}",
                                        yprim.LocalID, assetFound, yprim.BaseShape.SculptEntry, mismatchIDs );

                        });
                    }
                    else
                    {
                        PhysicsScene.Logger.ErrorFormat("{0} Physical object requires asset but no asset provider. Name={1}",
                                                    LogHeader, PhysicsScene.Name);
                    }
                });
        }
        else
        {
            if (prim.LastAssetBuildFailed)
            {
                PhysicsScene.Logger.ErrorFormat("{0} Mesh failed to fetch asset. lID={1}, texture={2}",
                                            LogHeader, prim.LocalID, prim.BaseShape.SculptTexture);
            }
        }

        // While we wait for the mesh defining asset to be loaded, stick in a simple box for the object.
        BulletShape fillinShape = BuildPhysicalNativeShape(prim, BSPhysicsShapeType.SHAPE_BOX, FixedShapeKey.KEY_BOX);
        DetailLog("{0},BSShapeCollection.VerifyMeshCreated,boxTempShape", prim.LocalID);

        return fillinShape;
    }

    // Create a body object in Bullet.
    // Updates prim.BSBody with the information about the new body if one is created.
    // Returns 'true' if an object was actually created.
    // Called at taint-time.
    private bool CreateBody(bool forceRebuild, BSPhysObject prim, BulletWorld sim, BodyDestructionCallback bodyCallback)
    {
        bool ret = false;

        // the mesh, hull or native shape must have already been created in Bullet
        bool mustRebuild = !prim.PhysBody.HasPhysicalBody;

        // If there is an existing body, verify it's of an acceptable type.
        // If not a solid object, body is a GhostObject. Otherwise a RigidBody.
        if (!mustRebuild)
        {
            CollisionObjectTypes bodyType = (CollisionObjectTypes)PhysicsScene.PE.GetBodyType(prim.PhysBody);
            if (prim.IsSolid && bodyType != CollisionObjectTypes.CO_RIGID_BODY
                || !prim.IsSolid && bodyType != CollisionObjectTypes.CO_GHOST_OBJECT)
            {
                // If the collisionObject is not the correct type for solidness, rebuild what's there
                mustRebuild = true;
                if (DDetail) DetailLog("{0},BSShapeCollection.CreateBody,forceRebuildBecauseChangingBodyType,bodyType={1}", prim.LocalID, bodyType);
            }
        }

        if (mustRebuild || forceRebuild)
        {
            // Free any old body
            DereferenceBody(prim.PhysBody, bodyCallback);

            BulletBody aBody;
            if (prim.IsSolid)
            {
                aBody = PhysicsScene.PE.CreateBodyFromShape(sim, prim.PhysShape, prim.LocalID, prim.RawPosition, prim.RawOrientation);
                if (DDetail) DetailLog("{0},BSShapeCollection.CreateBody,mesh,body={1}", prim.LocalID, aBody);
            }
            else
            {
                aBody = PhysicsScene.PE.CreateGhostFromShape(sim, prim.PhysShape, prim.LocalID, prim.RawPosition, prim.RawOrientation);
                if (DDetail) DetailLog("{0},BSShapeCollection.CreateBody,ghost,body={1}", prim.LocalID, aBody);
            }

            ReferenceBody(aBody);

            prim.PhysBody = aBody;

            ret = true;
        }

        return ret;
    }

    private bool TryGetMeshByPtr(BulletShape shape, out MeshDesc outDesc)
    {
        bool ret = false;
        MeshDesc foundDesc = new MeshDesc();
        foreach (MeshDesc md in Meshes.Values)
        {
            if (md.shape.ReferenceSame(shape))
            {
                foundDesc = md;
                ret = true;
                break;
            }

        }
        outDesc = foundDesc;
        return ret;
    }

    private bool TryGetHullByPtr(BulletShape shape, out HullDesc outDesc)
    {
        bool ret = false;
        HullDesc foundDesc = new HullDesc();
        foreach (HullDesc hd in Hulls.Values)
        {
            if (hd.shape.ReferenceSame(shape))
            {
                foundDesc = hd;
                ret = true;
                break;
            }

        }
        outDesc = foundDesc;
        return ret;
    }

    private void DetailLog(string msg, params Object[] args)
    {
        if (PhysicsScene.PhysicsLogging.Enabled)
            PhysicsScene.DetailLog(msg, args);
    }
}
}
