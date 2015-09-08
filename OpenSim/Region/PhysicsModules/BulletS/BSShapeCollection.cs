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
using OpenSim.Region.PhysicsModules.SharedBase;

namespace OpenSim.Region.PhysicsModule.BulletS
{
public sealed class BSShapeCollection : IDisposable
{
#pragma warning disable 414
    private static string LogHeader = "[BULLETSIM SHAPE COLLECTION]";
#pragma warning restore 414

    private BSScene m_physicsScene { get; set; }

    private Object m_collectionActivityLock = new Object();

    private bool DDetail = false;

    public BSShapeCollection(BSScene physScene)
    {
        m_physicsScene = physScene;
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
    public delegate void PhysicalDestructionCallback(BulletBody pBody, BulletShape pShape);

    // Called to update/change the body and shape for an object.
    // The object has some shape and body on it. Here we decide if that is the correct shape
    //    for the current state of the object (static/dynamic/...).
    // If bodyCallback is not null, it is called if either the body or the shape are changed
    //    so dependencies (like constraints) can be removed before the physical object is dereferenced.
    // Return 'true' if either the body or the shape changed.
    // Called at taint-time.
    public bool GetBodyAndShape(bool forceRebuild, BulletWorld sim, BSPhysObject prim, PhysicalDestructionCallback bodyCallback)
    {
        m_physicsScene.AssertInTaintTime("BSShapeCollection.GetBodyAndShape");

        bool ret = false;

        // This lock could probably be pushed down lower but building shouldn't take long
        lock (m_collectionActivityLock)
        {
            // Do we have the correct geometry for this type of object?
            // Updates prim.BSShape with information/pointers to shape.
            // Returns 'true' of BSShape is changed to a new shape.
            bool newGeom = CreateGeom(forceRebuild, prim, bodyCallback);
            // If we had to select a new shape geometry for the object,
            //    rebuild the body around it.
            // Updates prim.BSBody with information/pointers to requested body
            // Returns 'true' if BSBody was changed.
            bool newBody = CreateBody((newGeom || forceRebuild), prim, m_physicsScene.World, bodyCallback);
            ret = newGeom || newBody;
        }
        DetailLog("{0},BSShapeCollection.GetBodyAndShape,taintExit,force={1},ret={2},body={3},shape={4}",
                                prim.LocalID, forceRebuild, ret, prim.PhysBody, prim.PhysShape);

        return ret;
    }

    public bool GetBodyAndShape(bool forceRebuild, BulletWorld sim, BSPhysObject prim)
    {
        return GetBodyAndShape(forceRebuild, sim, prim, null);
    }

    // If the existing prim's shape is to be replaced, remove the tie to the existing shape
    //     before replacing it.
    private void DereferenceExistingShape(BSPhysObject prim, PhysicalDestructionCallback shapeCallback)
    {
        if (prim.PhysShape.HasPhysicalShape)
        {
            if (shapeCallback != null)
                shapeCallback(prim.PhysBody, prim.PhysShape.physShapeInfo);
            prim.PhysShape.Dereference(m_physicsScene);
        }
        prim.PhysShape = new BSShapeNull();
    }

    // Create the geometry information in Bullet for later use.
    // The objects needs a hull if it's physical otherwise a mesh is enough.
    // if 'forceRebuild' is true, the geometry is unconditionally rebuilt. For meshes and hulls,
    //     shared geometries will be used. If the parameters of the existing shape are the same
    //     as this request, the shape is not rebuilt.
    // Info in prim.BSShape is updated to the new shape.
    // Returns 'true' if the geometry was rebuilt.
    // Called at taint-time!
    public const int AvatarShapeCapsule = 0;
    public const int AvatarShapeCube = 1;
    public const int AvatarShapeOvoid = 2;
    public const int AvatarShapeMesh = 3;
    private bool CreateGeom(bool forceRebuild, BSPhysObject prim, PhysicalDestructionCallback shapeCallback)
    {
        bool ret = false;
        bool haveShape = false;
        bool nativeShapePossible = true;
        PrimitiveBaseShape pbs = prim.BaseShape;

        // Kludge to create the capsule for the avatar.
        // TDOD: Remove/redo this when BSShapeAvatar is working!!
        BSCharacter theChar = prim as BSCharacter;
        if (theChar != null)
        {
            DereferenceExistingShape(prim, shapeCallback);
            switch (BSParam.AvatarShape)
            {
                case AvatarShapeCapsule:
                    prim.PhysShape = BSShapeNative.GetReference(m_physicsScene, prim,
                                            BSPhysicsShapeType.SHAPE_CAPSULE, FixedShapeKey.KEY_CAPSULE);
                    ret = true;
                    haveShape = true;
                    break;
                case AvatarShapeCube:
                    prim.PhysShape = BSShapeNative.GetReference(m_physicsScene, prim,
                                            BSPhysicsShapeType.SHAPE_BOX, FixedShapeKey.KEY_CAPSULE);
                    ret = true;
                    haveShape = true;
                    break;
                case AvatarShapeOvoid:
                    // Saddly, Bullet doesn't scale spheres so this doesn't work as an avatar shape
                    prim.PhysShape = BSShapeNative.GetReference(m_physicsScene, prim,
                                            BSPhysicsShapeType.SHAPE_SPHERE, FixedShapeKey.KEY_CAPSULE);
                    ret = true;
                    haveShape = true;
                    break;
                case AvatarShapeMesh:
                    break;
                default:
                    break;
            }
        }

        // If the prim attributes are simple, this could be a simple Bullet native shape
        // Native shapes work whether to object is static or physical.
        if (!haveShape
                && nativeShapePossible
                && pbs != null
                && PrimHasNoCuts(pbs)
                && ( !pbs.SculptEntry || (pbs.SculptEntry && !BSParam.ShouldMeshSculptedPrim) )
            )
        {
            // Get the scale of any existing shape so we can see if the new shape is same native type and same size.
            OMV.Vector3 scaleOfExistingShape = OMV.Vector3.Zero;
            if (prim.PhysShape.HasPhysicalShape)
                scaleOfExistingShape = m_physicsScene.PE.GetLocalScaling(prim.PhysShape.physShapeInfo);

            if (DDetail) DetailLog("{0},BSShapeCollection.CreateGeom,maybeNative,force={1},primScale={2},primSize={3},primShape={4}",
                        prim.LocalID, forceRebuild, prim.Scale, prim.Size, prim.PhysShape.physShapeInfo.shapeType);

            // It doesn't look like Bullet scales native spheres so make sure the scales are all equal
            if ((pbs.ProfileShape == ProfileShape.HalfCircle && pbs.PathCurve == (byte)Extrusion.Curve1)
                                && pbs.Scale.X == pbs.Scale.Y && pbs.Scale.Y == pbs.Scale.Z)
            {
                haveShape = true;
                if (forceRebuild
                        || prim.PhysShape.ShapeType != BSPhysicsShapeType.SHAPE_SPHERE
                    )
                {
                    DereferenceExistingShape(prim, shapeCallback);
                    prim.PhysShape = BSShapeNative.GetReference(m_physicsScene, prim,
                                            BSPhysicsShapeType.SHAPE_SPHERE, FixedShapeKey.KEY_SPHERE);
                    ret = true;
                }
                if (DDetail) DetailLog("{0},BSShapeCollection.CreateGeom,sphere,force={1},rebuilt={2},shape={3}",
                                        prim.LocalID, forceRebuild, ret, prim.PhysShape);
            }
            // If we didn't make a sphere, maybe a box will work.
            if (!haveShape && pbs.ProfileShape == ProfileShape.Square && pbs.PathCurve == (byte)Extrusion.Straight)
            {
                haveShape = true;
                if (forceRebuild
                        || prim.Scale != scaleOfExistingShape
                        || prim.PhysShape.ShapeType != BSPhysicsShapeType.SHAPE_BOX
                        )
                {
                    DereferenceExistingShape(prim, shapeCallback);
                    prim.PhysShape = BSShapeNative.GetReference(m_physicsScene, prim,
                                            BSPhysicsShapeType.SHAPE_BOX, FixedShapeKey.KEY_BOX);
                    ret = true;
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

    // return 'true' if this shape description does not include any cutting or twisting.
    public static bool PrimHasNoCuts(PrimitiveBaseShape pbs)
    {
        return pbs.ProfileBegin == 0 && pbs.ProfileEnd == 0
            && pbs.ProfileHollow == 0
            && pbs.PathTwist == 0 && pbs.PathTwistBegin == 0
            && pbs.PathBegin == 0 && pbs.PathEnd == 0
            && pbs.PathTaperX == 0 && pbs.PathTaperY == 0
            && pbs.PathScaleX == 100 && pbs.PathScaleY == 100
            && pbs.PathShearX == 0 && pbs.PathShearY == 0;
    }

    // return 'true' if the prim's shape was changed.
    private bool CreateGeomMeshOrHull(BSPhysObject prim, PhysicalDestructionCallback shapeCallback)
    {

        bool ret = false;
        // Note that if it's a native shape, the check for physical/non-physical is not
        //     made. Native shapes work in either case.
        if (prim.IsPhysical && BSParam.ShouldUseHullsForPhysicalObjects)
        {
            // Use a simple, single mesh convex hull shape if the object is simple enough
            BSShape potentialHull = null;

            PrimitiveBaseShape pbs = prim.BaseShape;
            // Use a simple, one section convex shape for prims that are probably convex (no cuts or twists)
            if (BSParam.ShouldUseSingleConvexHullForPrims
                && pbs != null
                && !pbs.SculptEntry
                && PrimHasNoCuts(pbs)
                )
            {
                potentialHull = BSShapeConvexHull.GetReference(m_physicsScene, false /* forceRebuild */, prim);
            }
            // Use the GImpact shape if it is a prim that has some concaveness
            if (potentialHull == null
                && BSParam.ShouldUseGImpactShapeForPrims
                && pbs != null
                && !pbs.SculptEntry
                )
            {
                    potentialHull = BSShapeGImpact.GetReference(m_physicsScene, false /* forceRebuild */, prim);
            }
            // If not any of the simple cases, just make a hull
            if (potentialHull == null)
            {
                potentialHull = BSShapeHull.GetReference(m_physicsScene, false /*forceRebuild*/, prim);
            }

            // If the current shape is not what is on the prim at the moment, time to change.
            if (!prim.PhysShape.HasPhysicalShape
                        || potentialHull.ShapeType != prim.PhysShape.ShapeType
                        || potentialHull.physShapeInfo.shapeKey != prim.PhysShape.physShapeInfo.shapeKey)
            {
                DereferenceExistingShape(prim, shapeCallback);
                prim.PhysShape = potentialHull;
                ret = true;
            }
            else
            {
                // The current shape on the prim is the correct one. We don't need the potential reference.
                potentialHull.Dereference(m_physicsScene);
            }
            if (DDetail) DetailLog("{0},BSShapeCollection.CreateGeom,hull,shape={1}", prim.LocalID, prim.PhysShape);
        }
        else
        {
            // Non-physical objects should be just meshes.
            BSShape potentialMesh = BSShapeMesh.GetReference(m_physicsScene, false /*forceRebuild*/, prim);
            // If the current shape is not what is on the prim at the moment, time to change.
            if (!prim.PhysShape.HasPhysicalShape
                        || potentialMesh.ShapeType != prim.PhysShape.ShapeType
                        || potentialMesh.physShapeInfo.shapeKey != prim.PhysShape.physShapeInfo.shapeKey)
            {
                DereferenceExistingShape(prim, shapeCallback);
                prim.PhysShape = potentialMesh;
                ret = true;
            }
            else
            {
                // We don't need this reference to the mesh that is already being using.
                potentialMesh.Dereference(m_physicsScene);
            }
            if (DDetail) DetailLog("{0},BSShapeCollection.CreateGeom,mesh,shape={1}", prim.LocalID, prim.PhysShape);
        }
        return ret;
    }

    // Track another user of a body.
    // We presume the caller has allocated the body.
    // Bodies only have one user so the body is just put into the world if not already there.
    private void ReferenceBody(BulletBody body)
    {
        lock (m_collectionActivityLock)
        {
            if (DDetail) DetailLog("{0},BSShapeCollection.ReferenceBody,newBody,body={1}", body.ID, body);
            if (!m_physicsScene.PE.IsInWorld(m_physicsScene.World, body))
            {
                m_physicsScene.PE.AddObjectToWorld(m_physicsScene.World, body);
                if (DDetail) DetailLog("{0},BSShapeCollection.ReferenceBody,addedToWorld,ref={1}", body.ID, body);
            }
        }
    }

    // Release the usage of a body.
    // Called when releasing use of a BSBody. BSShape is handled separately.
    // Called in taint time.
    public void DereferenceBody(BulletBody body, PhysicalDestructionCallback bodyCallback )
    {
        if (!body.HasPhysicalBody)
            return;

        m_physicsScene.AssertInTaintTime("BSShapeCollection.DereferenceBody");

        lock (m_collectionActivityLock)
        {
            if (DDetail) DetailLog("{0},BSShapeCollection.DereferenceBody,DestroyingBody,body={1}", body.ID, body);
            // If the caller needs to know the old body is going away, pass the event up.
            if (bodyCallback != null)
                bodyCallback(body, null);

            // Removing an object not in the world is a NOOP
            m_physicsScene.PE.RemoveObjectFromWorld(m_physicsScene.World, body);

            // Zero any reference to the shape so it is not freed when the body is deleted.
            m_physicsScene.PE.SetCollisionShape(m_physicsScene.World, body, null);

            m_physicsScene.PE.DestroyObject(m_physicsScene.World, body);
        }
    }

    // Create a body object in Bullet.
    // Updates prim.BSBody with the information about the new body if one is created.
    // Returns 'true' if an object was actually created.
    // Called at taint-time.
    private bool CreateBody(bool forceRebuild, BSPhysObject prim, BulletWorld sim, PhysicalDestructionCallback bodyCallback)
    {
        bool ret = false;

        // the mesh, hull or native shape must have already been created in Bullet
        bool mustRebuild = !prim.PhysBody.HasPhysicalBody;

        // If there is an existing body, verify it's of an acceptable type.
        // If not a solid object, body is a GhostObject. Otherwise a RigidBody.
        if (!mustRebuild)
        {
            CollisionObjectTypes bodyType = (CollisionObjectTypes)m_physicsScene.PE.GetBodyType(prim.PhysBody);
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
                aBody = m_physicsScene.PE.CreateBodyFromShape(sim, prim.PhysShape.physShapeInfo, prim.LocalID, prim.RawPosition, prim.RawOrientation);
                if (DDetail) DetailLog("{0},BSShapeCollection.CreateBody,rigid,body={1}", prim.LocalID, aBody);
            }
            else
            {
                aBody = m_physicsScene.PE.CreateGhostFromShape(sim, prim.PhysShape.physShapeInfo, prim.LocalID, prim.RawPosition, prim.RawOrientation);
                if (DDetail) DetailLog("{0},BSShapeCollection.CreateBody,ghost,body={1}", prim.LocalID, aBody);
            }

            ReferenceBody(aBody);

            prim.PhysBody = aBody;

            ret = true;
        }

        return ret;
    }

    private void DetailLog(string msg, params Object[] args)
    {
        if (m_physicsScene.PhysicsLogging.Enabled)
            m_physicsScene.DetailLog(msg, args);
    }
}
}
