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
    protected BSScene PhysicsScene { get; set; }

    private Object m_collectionActivityLock = new Object();

    // Description of a Mesh
    private struct MeshDesc
    {
        public IntPtr Ptr;
        public int referenceCount;
        public DateTime lastReferenced;
        public IMesh meshData;
    }

    // Description of a hull.
    // Meshes and hulls have the same shape hash key but we only need hulls for efficient physical objects
    private struct HullDesc
    {
        public IntPtr Ptr;
        public int referenceCount;
        public DateTime lastReferenced;
    }

    private struct BodyDesc
    {
        public IntPtr Ptr;
        // Bodies are only used once so reference count is always either one or zero
        public int referenceCount;
        public DateTime lastReferenced;
    }

    private Dictionary<ulong, MeshDesc> Meshes = new Dictionary<ulong, MeshDesc>();
    private Dictionary<ulong, HullDesc> Hulls = new Dictionary<ulong, HullDesc>();
    private Dictionary<uint, BodyDesc> Bodies = new Dictionary<uint, BodyDesc>();

    public BSShapeCollection(BSScene physScene)
    {
        PhysicsScene = physScene;
    }

    public void Dispose()
    {
        // TODO!!!!!!!!!
    }

    // Called to update/change the body and shape for an object.
    // First checks the shape and updates that if necessary then makes
    //    sure the body is of the right type.
    // Return 'true' if either the body or the shape changed.
    // Called at taint-time!!
    public bool GetBodyAndShape(bool forceRebuild, BulletSim sim, BSPrim prim, ShapeData shapeData, PrimitiveBaseShape pbs)
    {
        bool ret = false;

        // This lock could probably be pushed down lower but building shouldn't take long
        lock (m_collectionActivityLock)
        {
            // Do we have the correct geometry for this type of object?
            bool newGeom = CreateGeom(forceRebuild, prim, shapeData, pbs);
            // If we had to select a new shape geometry for the object,
            //    rebuild the body around it.
            bool newBody = CreateBody((newGeom || forceRebuild), prim, PhysicsScene.World, prim.BSShape, shapeData);
            ret = newGeom || newBody;
        }
        DetailLog("{0},BSShapeCollection.GetBodyAndShape,force-{1},ret={2},body={3},shape={4}", 
            prim.LocalID, forceRebuild, ret, prim.BSBody, prim.BSShape);

        return ret;
    }

    // Track another user of a body
    // We presume the caller has allocated the body.
    // Bodies only have one user so the reference count is either 1 or 0.
    public void ReferenceBody(BulletBody shape, bool atTaintTime)
    {
        lock (m_collectionActivityLock)
        {
            BodyDesc bodyDesc;
            if (Bodies.TryGetValue(shape.ID, out bodyDesc))
            {
                bodyDesc.referenceCount++;
                DetailLog("{0},BSShapeCollection.ReferenceBody,existingBody,ref={1}", shape.ID, bodyDesc.referenceCount);
            }
            else
            {
                bodyDesc.Ptr = shape.ptr;
                bodyDesc.referenceCount = 1;
                DetailLog("{0},BSShapeCollection.ReferenceBody,newBody,ref={1}", shape.ID, bodyDesc.referenceCount);
            }
            bodyDesc.lastReferenced = System.DateTime.Now;
            Bodies[shape.ID] = bodyDesc;
        }
}

    // Release the usage of a body.
    // Not that this will also delete the body in BUllet if the body is now unused (reference count = 0).
    public void DereferenceBody(BulletBody shape, bool inTaintTime)
    {
        if (shape.ptr == IntPtr.Zero)
            return;

        lock (m_collectionActivityLock)
        {
            BodyDesc bodyDesc;
            if (Bodies.TryGetValue(shape.ID, out bodyDesc))
            {
                bodyDesc.referenceCount--;
                bodyDesc.lastReferenced = System.DateTime.Now;
                Bodies[shape.ID] = bodyDesc;
                DetailLog("{0},BSShapeCollection.DereferenceBody,ref={1}", shape.ID, bodyDesc.referenceCount);
                
                if (bodyDesc.referenceCount == 0)
                {
                    Bodies.Remove(shape.ID);
                    BSScene.TaintCallback removeOperation = delegate()
                    {
                        DetailLog("{0},BSShapeCollection.DereferenceBody,DestroyingBody. Ptr={1:X}", shape.ID, shape.ptr);
                        // zero any reference to the shape so it is not freed when the body is deleted
                        BulletSimAPI.SetCollisionShape2(PhysicsScene.World.ptr, shape.ptr, IntPtr.Zero);
                        // It may have already been removed from the world in which case the next is a NOOP
                        BulletSimAPI.RemoveObjectFromWorld2(PhysicsScene.World.ptr, shape.ptr);
                        BulletSimAPI.DestroyObject2(PhysicsScene.World.ptr, shape.ptr);
                    };
                    // If already in taint-time, do the operations now. Otherwise queue for later.
                    if (inTaintTime)
                        removeOperation();
                    else
                        PhysicsScene.TaintedObject("BSShapeCollection.DereferenceBody", removeOperation);
                }
            }
            else
            {
                DetailLog("{0},BSShapeCollection.DereferenceBody,DID NOT FIND BODY", shape.ID, bodyDesc.referenceCount);
            }
        }
    }

    // Track another user of the shape
    private bool ReferenceShape(BulletShape shape)
    {
        return ReferenceShape(shape, null);
    }

    // Track the datastructures and use count for a shape.
    // When creating a hull, this is called first to reference the mesh
    //     and then again to reference the hull.
    // Meshes and hulls for the same shape have the same hash key.
    // NOTE that native shapes are not added to the mesh list or removed.
    // Returns 'true' if this is the initial reference to the shape. Otherwise reused.
    private bool ReferenceShape(BulletShape shape, IMesh meshData)
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
                    DetailLog("{0},BSShapeColliction.ReferenceShape,existingMesh,key={1},cnt={2}",
                                BSScene.DetailLogZero, shape.shapeKey, meshDesc.referenceCount);
                }
                else
                {
                    // This is a new reference to a mesh
                    meshDesc.Ptr = shape.ptr;
                    meshDesc.meshData = meshData;
                    meshDesc.referenceCount = 1;
                    DetailLog("{0},BSShapeColliction.ReferenceShape,newMesh,key={1},cnt={2}",
                                BSScene.DetailLogZero, shape.shapeKey, meshDesc.referenceCount);
                    ret = true;
                }
                meshDesc.lastReferenced = System.DateTime.Now;
                Meshes[shape.shapeKey] = meshDesc;
                break;
            case ShapeData.PhysicsShapeType.SHAPE_HULL:
                HullDesc hullDesc;
                if (Hulls.TryGetValue(shape.shapeKey, out hullDesc))
                {
                    // There is an existing instance of this mesh.
                    hullDesc.referenceCount++;
                    DetailLog("{0},BSShapeColliction.ReferenceShape,existingHull,key={1},cnt={2}",
                                BSScene.DetailLogZero, shape.shapeKey, hullDesc.referenceCount);
                }
                else
                {
                    // This is a new reference to a hull
                    hullDesc.Ptr = shape.ptr;
                    hullDesc.referenceCount = 1;
                    DetailLog("{0},BSShapeColliction.ReferenceShape,newHull,key={1},cnt={2}",
                                BSScene.DetailLogZero, shape.shapeKey, hullDesc.referenceCount);
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

    // Release the usage of a shape
    private void DereferenceShape(BulletShape shape, bool atTaintTime)
    {
        if (shape.ptr == IntPtr.Zero)
            return;

        BSScene.TaintCallback dereferenceOperation = delegate()
        {
            switch (shape.type)
            {
                case ShapeData.PhysicsShapeType.SHAPE_HULL:
                    DereferenceHull(shape);
                    // Hulls also include a mesh
                    DereferenceMesh(shape);
                    break;
                case ShapeData.PhysicsShapeType.SHAPE_MESH:
                    DereferenceMesh(shape);
                    break;
                case ShapeData.PhysicsShapeType.SHAPE_UNKNOWN:
                    break;
                default:
                    // Native shapes are not tracked and are released immediately
                    if (shape.ptr != IntPtr.Zero & shape.isNativeShape)
                    {
                        BulletSimAPI.DeleteCollisionShape2(PhysicsScene.World.ptr, shape.ptr);
                    }
                    break;
            }
        };
        if (atTaintTime)
            dereferenceOperation();
        else
            PhysicsScene.TaintedObject("BSShapeCollection.DereferenceShape", dereferenceOperation);
    }

    // Count down the reference count for a mesh shape
    // Called at taint-time.
    private void DereferenceMesh(BulletShape shape)
    {
        MeshDesc meshDesc;
        if (Meshes.TryGetValue(shape.shapeKey, out meshDesc))
        {
            meshDesc.referenceCount--;
            // TODO: release the Bullet storage
            meshDesc.lastReferenced = System.DateTime.Now;
            Meshes[shape.shapeKey] = meshDesc;
            DetailLog("{0},BSShapeColliction.DereferenceMesh,key={1},cnt={2}",
                                    BSScene.DetailLogZero, shape.shapeKey, meshDesc.referenceCount);

        }
    }

    // Count down the reference count for a hull shape
    // Called at taint-time.
    private void DereferenceHull(BulletShape shape)
    {
        HullDesc hullDesc;
        if (Hulls.TryGetValue(shape.shapeKey, out hullDesc))
        {
            hullDesc.referenceCount--;
            // TODO: release the Bullet storage (aging old entries?)
            hullDesc.lastReferenced = System.DateTime.Now;
            Hulls[shape.shapeKey] = hullDesc;
            DetailLog("{0},BSShapeColliction.DereferenceHull,key={1},cnt={2}",
                                    BSScene.DetailLogZero, shape.shapeKey, hullDesc.referenceCount);
        }
    }

    // Create the geometry information in Bullet for later use.
    // The objects needs a hull if it's physical otherwise a mesh is enough.
    // No locking here because this is done when we know physics is not simulating.
    // if 'forceRebuild' is true, the geometry is rebuilt. Otherwise a previously built version is used.
    // Returns 'true' if the geometry was rebuilt.
    // Called at taint-time!
    private bool CreateGeom(bool forceRebuild, BSPrim prim, ShapeData shapeData, PrimitiveBaseShape pbs)
    {
        bool ret = false;
        bool haveShape = false;
        bool nativeShapePossible = true;

        BulletShape newShape = new BulletShape(IntPtr.Zero);

        // If the prim attributes are simple, this could be a simple Bullet native shape
        if (nativeShapePossible
                && ((pbs.SculptEntry && !PhysicsScene.ShouldMeshSculptedPrim)
                    || (pbs.ProfileBegin == 0 && pbs.ProfileEnd == 0
                        && pbs.ProfileHollow == 0
                        && pbs.PathTwist == 0 && pbs.PathTwistBegin == 0
                        && pbs.PathBegin == 0 && pbs.PathEnd == 0
                        && pbs.PathTaperX == 0 && pbs.PathTaperY == 0
                        && pbs.PathScaleX == 100 && pbs.PathScaleY == 100
                        && pbs.PathShearX == 0 && pbs.PathShearY == 0) ) )
        {
            if (pbs.ProfileShape == ProfileShape.HalfCircle && pbs.PathCurve == (byte)Extrusion.Curve1)
            {
                haveShape = true;
                if (forceRebuild || (prim.BSShape.type != ShapeData.PhysicsShapeType.SHAPE_SPHERE))
                {
                    newShape = AddNativeShapeToPrim(
                            prim, shapeData, ShapeData.PhysicsShapeType.SHAPE_SPHERE, ShapeData.FixedShapeKey.KEY_SPHERE);
                    DetailLog("{0},BSShapeCollection.CreateGeom,sphere,force={1},shape={2}", 
                                        prim.LocalID, forceRebuild,prim.BSShape);

                    ret = true;
                }
            }
            else
            {
                // m_log.DebugFormat("{0}: CreateGeom: Defaulting to box. lid={1}, type={2}, size={3}", LogHeader, LocalID, _shapeType, _size);
                haveShape = true;
                if (forceRebuild || (prim.BSShape.type != ShapeData.PhysicsShapeType.SHAPE_BOX))
                {
                    newShape = AddNativeShapeToPrim(
                        prim, shapeData, ShapeData.PhysicsShapeType.SHAPE_BOX, ShapeData.FixedShapeKey.KEY_BOX);
                    DetailLog("{0},BSShapeCollection.CreateGeom,box,force={1},shape={2}", 
                                        prim.LocalID, forceRebuild,prim.BSShape);

                    ret = true;
                }
            }
        }
        // If a simple shape is not happening, create a mesh and possibly a hull
        // Note that if it's a native shape, the check for physical/non-physical is not
        //     made. Native shapes are best used in either case.
        if (!haveShape)
        {
            if (prim.IsPhysical)
            {
                if (forceRebuild || !Hulls.ContainsKey(shapeData.HullKey))
                {
                    // physical objects require a hull for interaction.
                    // This also creates the mesh if it doesn't already exist.
                    ret = CreateGeomHull(prim, shapeData, pbs);
                }
                else
                {
                    prim.BSShape = new BulletShape(Hulls[shapeData.HullKey].Ptr,
                                                    ShapeData.PhysicsShapeType.SHAPE_HULL);
                    prim.BSShape.shapeKey = shapeData.HullKey;
                    // Another user of this shape.
                    ReferenceShape(prim.BSShape);
                    ret = true;
                }
            }
            else
            {
                if (forceRebuild || !Meshes.ContainsKey(prim.BSShape.shapeKey))
                {
                    // Static (non-physical) objects only need a mesh for bumping into
                    // Returning 'true' means prim.BShape was changed.
                    ret = CreateGeomMesh(prim, shapeData, pbs);
                }
                else
                {
                    prim.BSShape = new BulletShape(Hulls[shapeData.MeshKey].Ptr, 
                                                    ShapeData.PhysicsShapeType.SHAPE_MESH);
                    prim.BSShape.shapeKey = shapeData.MeshKey;
                    ReferenceShape(prim.BSShape);
                    ret = true;
                }
            }
        }
        return ret;
    }

    // Creates a native shape and assignes it to prim.BSShape
    private BulletShape AddNativeShapeToPrim(
                    BSPrim prim, ShapeData shapeData, ShapeData.PhysicsShapeType shapeType, 
                    ShapeData.FixedShapeKey shapeKey)
    {
        BulletShape newShape;

        // Bullet native objects are scaled by the Bullet engine so pass the size in
        prim.Scale = shapeData.Size;
        shapeData.Type = shapeType;
        shapeData.Scale = prim.Scale;

        // release any previous shape
        DereferenceShape(prim.BSShape, true);

        // Shape of this discriptioin is not allocated. Create new.
        newShape = new BulletShape(
                BulletSimAPI.BuildNativeShape2(PhysicsScene.World.ptr, shapeData), shapeType);
        newShape.shapeKey = (ulong)shapeKey;
        newShape.isNativeShape = true;

        // Don't to a 'ReferenceShape()' here because native shapes are not tracked.

        prim.BSShape = newShape;
        return newShape;
    }

    // Returns 'true' of a mesh was actually rebuild.
    // Called at taint-time!
    private bool CreateGeomMesh(BSPrim prim, ShapeData shapeData, PrimitiveBaseShape pbs)
    {
        BulletShape newShape = new BulletShape(IntPtr.Zero);

        // level of detail based on size and type of the object
        float lod = PhysicsScene.MeshLOD;
        if (pbs.SculptEntry) 
            lod = PhysicsScene.SculptLOD;

        float maxAxis = Math.Max(shapeData.Size.X, Math.Max(shapeData.Size.Y, shapeData.Size.Z));
        if (maxAxis > PhysicsScene.MeshMegaPrimThreshold) 
            lod = PhysicsScene.MeshMegaPrimLOD;

        ulong newMeshKey = (ulong)pbs.GetMeshKey(shapeData.Size, lod);

        // if this new shape is the same as last time, don't recreate the mesh
        if (prim.BSShape.shapeKey == newMeshKey) return false;

        DetailLog("{0},BSShapeCollection.CreateGeomMesh,create,key={1}", prim.LocalID, newMeshKey);

        // Since we're recreating new, get rid of the reference to the previous shape
        DereferenceShape(prim.BSShape, true);

        IMesh meshData = null;
        IntPtr meshPtr;
        MeshDesc meshDesc;
        if (Meshes.TryGetValue(newMeshKey, out meshDesc))
        {
            // If the mesh has already been built just use it.
            meshPtr = meshDesc.Ptr;
        }
        else
        {
            // always pass false for physicalness as this creates some sort of bounding box which we don't need
            meshData = PhysicsScene.mesher.CreateMesh(prim.PhysObjectName, pbs, shapeData.Size, lod, false);

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
        newShape = new BulletShape(meshPtr, ShapeData.PhysicsShapeType.SHAPE_MESH);
        newShape.shapeKey = newMeshKey;

        ReferenceShape(newShape, meshData);

        // meshes are already scaled by the meshmerizer
        prim.Scale = new OMV.Vector3(1f, 1f, 1f);
        prim.BSShape = newShape;
        return true;        // 'true' means a new shape has been added to this prim
    }

    // Returns 'true' of a mesh was actually rebuild (we could also have one of these specs).
    List<ConvexResult> m_hulls;
    private bool CreateGeomHull(BSPrim prim, ShapeData shapeData, PrimitiveBaseShape pbs)
    {
        BulletShape newShape;

        // Level of detail for the mesh can be different for sculpties and regular meshes.
        float lod = pbs.SculptEntry ? PhysicsScene.SculptLOD : PhysicsScene.MeshLOD;

        ulong newHullKey = (ulong)pbs.GetMeshKey(shapeData.Size, lod);

        // if the hull hasn't changed, don't rebuild it
        if (newHullKey == prim.BSShape.shapeKey) return false;

        DetailLog("{0},BSShapeCollection.CreateGeomHull,create,oldKey={1},newKey={2}", prim.LocalID, newHullKey, newHullKey);

        // Remove references to the previous shape. Also removes reference to underlying mesh.
        DereferenceShape(prim.BSShape, true);

        // Do not let the mesh dereference itself again. Was done in the above DerefereceShape().
        prim.BSShape.type = ShapeData.PhysicsShapeType.SHAPE_UNKNOWN;
        
        // Make sure the underlying mesh exists and is correct.
        // Since we're in the hull code, we know CreateGeomMesh() will not create a native shape.
        CreateGeomMesh(prim, shapeData, pbs);
        MeshDesc meshDesc = Meshes[newHullKey];

        IntPtr hullPtr;
        HullDesc hullDesc;
        if (Hulls.TryGetValue(newHullKey, out hullDesc))
        {
            // If the hull shape already is created, just use it.
            hullPtr = hullDesc.Ptr;
        }
        else
        {
            // Build a new hull in the physical world
            int[] indices = meshDesc.meshData.getIndexListAsInt();
            List<OMV.Vector3> vertices = meshDesc.meshData.getVertexList();

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
            // m_log.DebugFormat("{0}: CreateGeom: calling CreateHull. lid={1}, key={2}, hulls={3}", LogHeader, LocalID, _hullKey, hullCount);
            hullPtr = BulletSimAPI.CreateHullShape2(PhysicsScene.World.ptr, hullCount, convHulls);
        }

        newShape = new BulletShape(hullPtr, ShapeData.PhysicsShapeType.SHAPE_HULL);
        newShape.shapeKey = newHullKey;
        newShape.meshPtr = meshDesc.Ptr;

        ReferenceShape(newShape);

        // meshes and hulls are already scaled by the meshmerizer
        prim.Scale = new OMV.Vector3(1f, 1f, 1f);
        prim.BSShape = newShape;
        return true;        // 'true' means a new shape has been added to this prim
    }

    // Callback from convex hull creater with a newly created hull.
    // Just add it to our collection of hulls for this shape.
    private void HullReturn(ConvexResult result)
    {
        m_hulls.Add(result);
        return;
    }

    // Create an object in Bullet if it has not already been created.
    // Updates prim.BSBody with the information about the new body if one is created.
    // Returns 'true' if an object was actually created.
    // Called at taint-time.
    private bool CreateBody(bool forceRebuild, BSPrim prim, BulletSim sim, BulletShape shape, ShapeData shapeData)
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

        if (mustRebuild)
        {
            DereferenceBody(prim.BSBody, true);

            BulletBody aBody;
            IntPtr bodyPtr = IntPtr.Zero;
            if (prim.IsSolid)
            {
                bodyPtr = BulletSimAPI.CreateBodyFromShape2(sim.ptr, shape.ptr, shapeData.Position, shapeData.Rotation);
                DetailLog("{0},BSShapeCollection.CreateObject,mesh,ptr={1:X}", prim.LocalID, bodyPtr);
            }
            else
            {
                bodyPtr = BulletSimAPI.CreateGhostFromShape2(sim.ptr, shape.ptr, shapeData.Position, shapeData.Rotation);
                DetailLog("{0},BSShapeCollection.CreateObject,ghost,ptr={1:X}", prim.LocalID, bodyPtr);
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
        PhysicsScene.PhysicsLogging.Write(msg, args);
    }
}
}
