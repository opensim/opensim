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

    private Object m_shapeActivityLock = new Object();

    private struct MeshDesc
    {
        public IntPtr Ptr;
        public int referenceCount;
        public DateTime lastReferenced;
        public IMesh meshData;
    }

    private struct HullDesc
    {
        public IntPtr Ptr;
        public int referenceCount;
        public DateTime lastReferenced;
    }

    private Dictionary<ulong, MeshDesc> Meshes = new Dictionary<ulong, MeshDesc>();
    private Dictionary<ulong, HullDesc> Hulls = new Dictionary<ulong, HullDesc>();

    public BSShapeCollection(BSScene physScene)
    {
        PhysicsScene = physScene;
    }

    public void Dispose()
    {
    }

    // Called to update/change the body and shape for an object.
    // First checks the shape and updates that if necessary then makes
    //    sure the body is of the right type.
    // Return 'true' if either the body or the shape changed.
    // Called at taint-time!!
    public bool GetBodyAndShape(bool forceRebuild, BulletSim sim, BSPrim prim, ShapeData shapeData, PrimitiveBaseShape pbs)
    {
        bool ret = false;

        // Do we have the correct geometry for this type of object?
        if (CreateGeom(forceRebuild, prim, shapeData, pbs))
        {
            // If we had to select a new shape geometry for the object,
            //    rebuild the body around it.
            CreateObject(true, prim, PhysicsScene.World, prim.BSShape, shapeData);
            ret = true;
        }

        return ret;
    }

    // Track another user of a body
    public void ReferenceBody(BulletBody shape)
    {
    }

    // Release the usage of a body
    public void DereferenceBody(BulletBody shape)
    {
    }

    // Track another user of the shape
    public void ReferenceShape(BulletShape shape)
    {
        ReferenceShape(shape, null);
    }

    // Track the datastructures and use count for a shape.
    // When creating a hull, this is called first to reference the mesh
    //     and then again to reference the hull.
    // Meshes and hulls for the same shape have the same hash key.
    private void ReferenceShape(BulletShape shape, IMesh meshData)
    {
        switch (shape.type)
        {
            case ShapeData.PhysicsShapeType.SHAPE_MESH:
                MeshDesc meshDesc;
                if (Meshes.TryGetValue(shape.shapeKey, out meshDesc))
                {
                    // There is an existing instance of this mesh.
                    meshDesc.referenceCount++;
                }
                else
                {
                    // This is a new reference to a mesh
                    meshDesc.Ptr = shape.Ptr;
                    meshDesc.meshData = meshData;
                    meshDesc.referenceCount = 1;

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
                }
                else
                {
                    // This is a new reference to a mesh
                    hullDesc.Ptr = shape.Ptr;
                    hullDesc.referenceCount = 1;

                }
                hullDesc.lastReferenced = System.DateTime.Now;
                Hulls[shape.shapeKey] = hullDesc;
                break;
            default:
                break;
        }
    }

    // Release the usage of a shape
    public void DereferenceShape(BulletShape shape)
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
            default:
                break;
        }
    }

    // Count down the reference count for a mesh shape
    private void DereferenceMesh(BulletShape shape)
    {
        MeshDesc meshDesc;
        if (Meshes.TryGetValue(shape.shapeKey, out meshDesc))
        {
            meshDesc.referenceCount--;
            // TODO: release the Bullet storage
            meshDesc.lastReferenced = System.DateTime.Now;
            Meshes[shape.shapeKey] = meshDesc;
        }
    }

    // Count down the reference count for a hull shape
    private void DereferenceHull(BulletShape shape)
    {
        HullDesc hullDesc;
        if (Hulls.TryGetValue(shape.shapeKey, out hullDesc))
        {
            hullDesc.referenceCount--;
            // TODO: release the Bullet storage (aging old entries?)
            hullDesc.lastReferenced = System.DateTime.Now;
            Hulls[shape.shapeKey] = hullDesc;
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

        // If the object is dynamic, it must have a hull shape
        if (prim.IsPhysical)
            nativeShapePossible = false;

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
                    DetailLog("{0},BSShapeCollection.CreateGeom,sphere (force={1}", prim.LocalID, forceRebuild);
                    newShape = AddNativeShapeToPrim(prim, shapeData, ShapeData.PhysicsShapeType.SHAPE_SPHERE);

                    ret = true;
                }
            }
            else
            {
                // m_log.DebugFormat("{0}: CreateGeom: Defaulting to box. lid={1}, type={2}, size={3}", LogHeader, LocalID, _shapeType, _size);
                haveShape = true;
                if (forceRebuild || (prim.BSShape.type != ShapeData.PhysicsShapeType.SHAPE_BOX))
                {
                    DetailLog("{0},BSShapeCollection.CreateGeom,box (force={1})", prim.LocalID, forceRebuild);
                    newShape = AddNativeShapeToPrim(prim, shapeData, ShapeData.PhysicsShapeType.SHAPE_BOX);

                    ret = true;
                }
            }
        }
        // If a simple shape isn't happening, create a mesh and possibly a hull
        if (!haveShape)
        {
            if (prim.IsPhysical)
            {
                if (forceRebuild || !Hulls.ContainsKey(prim.BSShape.shapeKey))
                {
                    // physical objects require a hull for interaction.
                    // This also creates the mesh if it doesn't already exist
                    ret = CreateGeomHull(prim, shapeData, pbs);
                }
            }
            else
            {
                if (forceRebuild || !Meshes.ContainsKey(prim.BSShape.shapeKey))
                {
                    // Static (non-physical) objects only need a mesh for bumping into
                    ret = CreateGeomMesh(prim, shapeData, pbs);
                }
            }
        }
        return ret;
    }

    private BulletShape AddNativeShapeToPrim(BSPrim prim, ShapeData shapeData, ShapeData.PhysicsShapeType shapeType)
    {
        BulletShape newShape;

        // Bullet native objects are scaled by the Bullet engine so pass the size in
        prim.Scale = shapeData.Size;

        // release any previous shape
        DereferenceShape(prim.BSShape);

        MeshDesc existingShapeDesc;
        if (Meshes.TryGetValue(shapeData.MeshKey, out existingShapeDesc))
        {
            // If there is an existing allocated shape, use it
            newShape = new BulletShape(existingShapeDesc.Ptr, shapeType);
        }
        else
        {
            // Shape of this discriptioin is not allocated. Create new.
            newShape = new BulletShape(
                        BulletSimAPI.BuildNativeShape2(PhysicsScene.World.Ptr,
                                    (float)shapeType,
                                    PhysicsScene.Params.collisionMargin,
                                    prim.Scale),
                        shapeType);
        }
        newShape.shapeKey = shapeData.MeshKey;
        ReferenceShape(newShape);
        prim.BSShape = newShape;
        return newShape;
    }

    // No locking here because this is done when we know physics is not simulating
    // Returns 'true' of a mesh was actually rebuild (we could also have one of these specs).
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
        // m_log.DebugFormat("{0}: CreateGeomMesh: lID={1}, oldKey={2}, newKey={3}", LogHeader, LocalID, _meshKey, newMeshKey);

        // if this new shape is the same as last time, don't recreate the mesh
        if (prim.BSShape.shapeKey == newMeshKey) return false;

        DetailLog("{0},BSShapeCollection.CreateGeomMesh,create,key={1}", prim.LocalID, newMeshKey);

        // Since we're recreating new, get rid of the reference to the previous shape
        DereferenceShape(prim.BSShape);

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

            meshPtr = BulletSimAPI.CreateMeshShape2(PhysicsScene.World.Ptr, 
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

    // No locking here because this is done when we know physics is not simulating
    // Returns 'true' of a mesh was actually rebuild (we could also have one of these specs).
    List<ConvexResult> m_hulls;
    private bool CreateGeomHull(BSPrim prim, ShapeData shapeData, PrimitiveBaseShape pbs)
    {
        BulletShape newShape;

        float lod = pbs.SculptEntry ? PhysicsScene.SculptLOD : PhysicsScene.MeshLOD;
        ulong newHullKey = (ulong)pbs.GetMeshKey(shapeData.Size, lod);
        // m_log.DebugFormat("{0}: CreateGeomHull: lID={1}, oldKey={2}, newKey={3}", LogHeader, LocalID, _hullKey, newHullKey);

        // if the hull hasn't changed, don't rebuild it
        if (newHullKey == prim.BSShape.shapeKey) return false;

        DetailLog("{0},BSShapeCollection.CreateGeomHull,create,oldKey={1},newKey={2}", prim.LocalID, newHullKey, newHullKey);

        // remove references to any previous shape
        DereferenceShape(prim.BSShape);

        // Make sure the underlying mesh exists and is correct
        // Since we're in the hull code, we know CreateGeomMesh() will not create a native shape.
        CreateGeomMesh(prim, shapeData, pbs);
        MeshDesc meshDesc = Meshes[newHullKey];

        IntPtr hullPtr;
        HullDesc hullDesc;
        if (Hulls.TryGetValue(newHullKey, out hullDesc))
        {
            hullPtr = hullDesc.Ptr;
        }
        else
        {
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
            hullPtr = BulletSimAPI.CreateHullShape2(PhysicsScene.World.Ptr, hullCount, convHulls);
        }
        newShape = new BulletShape(hullPtr, ShapeData.PhysicsShapeType.SHAPE_HULL);
        newShape.shapeKey = newHullKey;

        ReferenceShape(newShape);

        // meshes are already scaled by the meshmerizer
        prim.Scale = new OMV.Vector3(1f, 1f, 1f);
        prim.BSShape = newShape;
        return true;        // 'true' means a new shape has been added to this prim
    }

    // Callback from convex hull creater with a newly created hull.
    // Just add it to the collection of hulls for this shape.
    private void HullReturn(ConvexResult result)
    {
        m_hulls.Add(result);
        return;
    }

    // Create an object in Bullet if it has not already been created
    // No locking here because this is done when the physics engine is not simulating
    // Returns 'true' if an object was actually created.
    private bool CreateObject(bool forceRebuild, BSPrim prim, BulletSim sim, BulletShape shape, ShapeData shapeData)
    {
        // the mesh or hull must have already been created in Bullet
        // m_log.DebugFormat("{0}: CreateObject: lID={1}, shape={2}", LogHeader, LocalID, shape.Type);

        DereferenceBody(prim.BSBody);

        BulletBody aBody;
        IntPtr bodyPtr = IntPtr.Zero;
        if (prim.IsSolid)
        {
            bodyPtr = BulletSimAPI.CreateBodyFromShape2(sim.Ptr, shape.Ptr, shapeData.Position, shapeData.Rotation);
        }
        else
        {
            bodyPtr = BulletSimAPI.CreateGhostFromShape2(sim.Ptr, shape.Ptr, shapeData.Position, shapeData.Rotation);
        }
        aBody = new BulletBody(shapeData.ID, bodyPtr);

        ReferenceBody(aBody);

        prim.BSBody = aBody;
        return true;
    }

    private void DetailLog(string msg, params Object[] args)
    {
        PhysicsScene.PhysicsLogging.Write(msg, args);
    }





}
}
