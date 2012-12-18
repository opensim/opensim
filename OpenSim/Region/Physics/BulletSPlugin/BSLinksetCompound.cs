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

using OMV = OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin
{

// When a child is linked, the relationship position of the child to the parent
//    is remembered so the child's world position can be recomputed when it is
//    removed from the linkset.
sealed class BSLinksetCompoundInfo : BSLinksetInfo
{
    public OMV.Vector3 OffsetPos;
    public OMV.Quaternion OffsetRot;
    public BSLinksetCompoundInfo(OMV.Vector3 p, OMV.Quaternion r)
    {
        OffsetPos = p;
        OffsetRot = r;
    }
    public override void Clear()
    {
        OffsetPos = OMV.Vector3.Zero;
        OffsetRot = OMV.Quaternion.Identity;
    }
    public override string ToString()
    {
        StringBuilder buff = new StringBuilder();
        buff.Append("<p=");
        buff.Append(OffsetPos.ToString());
        buff.Append(",r=");
        buff.Append(OffsetRot.ToString());
        buff.Append(">");
        return buff.ToString();
    }
};

public sealed class BSLinksetCompound : BSLinkset
{
    private static string LogHeader = "[BULLETSIM LINKSET COMPOUND]";

    public BSLinksetCompound(BSScene scene, BSPhysObject parent) : base(scene, parent)
    {
    }

    // For compound implimented linksets, if there are children, use compound shape for the root.
    public override BSPhysicsShapeType PreferredPhysicalShape(BSPhysObject requestor)
    { 
        // Returning 'unknown' means we don't have a preference.
        BSPhysicsShapeType ret = BSPhysicsShapeType.SHAPE_UNKNOWN;
        if (IsRoot(requestor) && HasAnyChildren)
        {
            ret = BSPhysicsShapeType.SHAPE_COMPOUND;
        }
        // DetailLog("{0},BSLinksetCompound.PreferredPhysicalShape,call,shape={1}", LinksetRoot.LocalID, ret);
        return ret;
    }

    // When physical properties are changed the linkset needs to recalculate
    //   its internal properties.
    public override void Refresh(BSPhysObject requestor)
    {
        // Something changed so do the rebuilding thing
        // ScheduleRebuild();
    }

    // Schedule a refresh to happen after all the other taint processing.
    private void ScheduleRebuild()
    {
        DetailLog("{0},BSLinksetCompound.Refresh,schedulingRefresh,rebuilding={1}", 
                            LinksetRoot.LocalID, Rebuilding);
        // When rebuilding, it is possible to set properties that would normally require a rebuild.
        //    If already rebuilding, don't request another rebuild.
        if (!Rebuilding)
        {
            PhysicsScene.PostTaintObject("BSLinksetCompound.Refresh", LinksetRoot.LocalID, delegate()
            {
                if (HasAnyChildren)
                    RecomputeLinksetCompound();
            });
        }
    }

    // The object is going dynamic (physical). Do any setup necessary
    //     for a dynamic linkset.
    // Only the state of the passed object can be modified. The rest of the linkset
    //     has not yet been fully constructed.
    // Return 'true' if any properties updated on the passed object.
    // Called at taint-time!
    public override bool MakeDynamic(BSPhysObject child)
    {
        bool ret = false;
        DetailLog("{0},BSLinksetCompound.MakeDynamic,call,IsRoot={1}", child.LocalID, IsRoot(child));
        if (IsRoot(child))
        {
            // The root is going dynamic. Make sure mass is properly set.
            m_mass = ComputeLinksetMass();
            ScheduleRebuild();
        }
        else
        {
            // The origional prims are removed from the world as the shape of the root compound
            //     shape takes over.
            BulletSimAPI.AddToCollisionFlags2(child.PhysBody.ptr, CollisionFlags.CF_NO_CONTACT_RESPONSE);
            BulletSimAPI.ForceActivationState2(child.PhysBody.ptr, ActivationState.DISABLE_SIMULATION);
            // We don't want collisions from the old linkset children.
            BulletSimAPI.RemoveFromCollisionFlags2(child.PhysBody.ptr, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);

            child.PhysBody.collisionType = CollisionType.LinksetChild;

            ret = true;
        }
        return ret;
    }

    // The object is going static (non-physical). Do any setup necessary for a static linkset.
    // Return 'true' if any properties updated on the passed object.
    // This doesn't normally happen -- OpenSim removes the objects from the physical
    //     world if it is a static linkset.
    // Called at taint-time!
    public override bool MakeStatic(BSPhysObject child)
    {
        bool ret = false;
        DetailLog("{0},BSLinksetCompound.MakeStatic,call,IsRoot={1}", child.LocalID, IsRoot(child));
        if (IsRoot(child))
        {
            ScheduleRebuild();
        }
        else
        {
            // The non-physical children can come back to life.
            BulletSimAPI.RemoveFromCollisionFlags2(child.PhysBody.ptr, CollisionFlags.CF_NO_CONTACT_RESPONSE);

            child.PhysBody.collisionType = CollisionType.LinksetChild;

            // Don't force activation so setting of DISABLE_SIMULATION can stay if used.
            BulletSimAPI.Activate2(child.PhysBody.ptr, false);
            ret = true;
        }
        return ret;
    }

    public override void UpdateProperties(BSPhysObject updated, bool physicalUpdate)
    {
        // The user moving a child around requires the rebuilding of the linkset compound shape
        // One problem is this happens when a border is crossed -- the simulator implementation
        //    is to store the position into the group which causes the move of the object
        //    but it also means all the child positions get updated.
        //    What would cause an unnecessary rebuild so we make sure the linkset is in a
        //    region before bothering to do a rebuild.
        if (!IsRoot(updated) 
                && !physicalUpdate 
                && PhysicsScene.TerrainManager.IsWithinKnownTerrain(LinksetRoot.RawPosition))
        {
            updated.LinksetInfo = null;
            ScheduleRebuild();
        }
    }

    // Routine called when rebuilding the body of some member of the linkset.
    // Since we don't keep in world relationships, do nothing unless it's a child changing.
    // Returns 'true' of something was actually removed and would need restoring
    // Called at taint-time!!
    public override bool RemoveBodyDependencies(BSPrim child)
    {
        bool ret = false;

        DetailLog("{0},BSLinksetCompound.RemoveBodyDependencies,refreshIfChild,rID={1},rBody={2},isRoot={3}",
                        child.LocalID, LinksetRoot.LocalID, LinksetRoot.PhysBody.ptr.ToString("X"), IsRoot(child));

        if (!IsRoot(child))
        {
            // Because it is a convenient time, recompute child world position and rotation based on
            //    its position in the linkset.
            RecomputeChildWorldPosition(child, true);
        }

        // Cannot schedule a refresh/rebuild here because this routine is called when
        //     the linkset is being rebuilt.
        // InternalRefresh(LinksetRoot);

        return ret;
    }

    // Companion to RemoveBodyDependencies(). If RemoveBodyDependencies() returns 'true',
    //     this routine will restore the removed constraints.
    // Called at taint-time!!
    public override void RestoreBodyDependencies(BSPrim child)
    {
    }

    // When the linkset is built, the child shape is added to the compound shape relative to the
    //    root shape. The linkset then moves around but this does not move the actual child
    //    prim. The child prim's location must be recomputed based on the location of the root shape.
    private void RecomputeChildWorldPosition(BSPhysObject child, bool inTaintTime)
    {
        BSLinksetCompoundInfo lci = child.LinksetInfo as BSLinksetCompoundInfo;
        if (lci != null)
        {
            if (inTaintTime)
            {
                OMV.Vector3 oldPos = child.RawPosition;
                child.ForcePosition = LinksetRoot.RawPosition + lci.OffsetPos;
                child.ForceOrientation = LinksetRoot.RawOrientation * lci.OffsetRot;
                DetailLog("{0},BSLinksetCompound.RecomputeChildWorldPosition,oldPos={1},lci={2},newPos={3}",
                                            child.LocalID, oldPos, lci, child.RawPosition);
            }
            else
            {
                // TaintedObject is not used here so the raw position is set now and not at taint-time.
                child.Position = LinksetRoot.RawPosition + lci.OffsetPos;
                child.Orientation = LinksetRoot.RawOrientation * lci.OffsetRot;
            }
        }
        else
        {
            // This happens when children have been added to the linkset but the linkset
            //     has not been constructed yet. So like, at taint time, adding children to a linkset
            //     and then changing properties of the children (makePhysical, for instance)
            //     but the post-print action of actually rebuilding the linkset has not yet happened.
            // PhysicsScene.Logger.WarnFormat("{0} Restoring linkset child position failed because of no relative position computed. ID={1}",
            //                                 LogHeader, child.LocalID);
            DetailLog("{0},BSLinksetCompound.recomputeChildWorldPosition,noRelativePositonInfo", child.LocalID);
        }
    }

    // ================================================================

    // Add a new child to the linkset.
    // Called while LinkActivity is locked.
    protected override void AddChildToLinkset(BSPhysObject child)
    {
        if (!HasChild(child))
        {
            m_children.Add(child);

            DetailLog("{0},BSLinksetCompound.AddChildToLinkset,call,child={1}", LinksetRoot.LocalID, child.LocalID);

            // Rebuild the compound shape with the new child shape included
            ScheduleRebuild();
        }
        return;
    }

    // Remove the specified child from the linkset.
    // Safe to call even if the child is not really in the linkset.
    protected override void RemoveChildFromLinkset(BSPhysObject child)
    {
        if (m_children.Remove(child))
        {
            DetailLog("{0},BSLinksetCompound.RemoveChildFromLinkset,call,rID={1},rBody={2},cID={3},cBody={4}",
                            child.LocalID,
                            LinksetRoot.LocalID, LinksetRoot.PhysBody.ptr.ToString("X"),
                            child.LocalID, child.PhysBody.ptr.ToString("X"));

            // Cause the child's body to be rebuilt and thus restored to normal operation
            RecomputeChildWorldPosition(child, false);
            child.ForceBodyShapeRebuild(false);

            if (!HasAnyChildren)
            {
                // The linkset is now empty. The root needs rebuilding.
                LinksetRoot.ForceBodyShapeRebuild(false);
            }
            else
            {
                // Rebuild the compound shape with the child removed
                ScheduleRebuild();
            }
        }
        return;
    }

    // Called before the simulation step to make sure the compound based linkset
    //    is all initialized.
    // Constraint linksets are rebuilt every time.
    // Note that this works for rebuilding just the root after a linkset is taken apart.
    // Called at taint time!!
    private void RecomputeLinksetCompound()
    {
        try
        {
            // Suppress rebuilding while rebuilding
            Rebuilding = true;

            // Cause the root shape to be rebuilt as a compound object with just the root in it
            LinksetRoot.ForceBodyShapeRebuild(true);

            DetailLog("{0},BSLinksetCompound.RecomputeLinksetCompound,start,rBody={1},rShape={2},numChildren={3}",
                            LinksetRoot.LocalID, LinksetRoot.PhysBody, LinksetRoot.PhysShape, NumberOfChildren);

            // Add a shape for each of the other children in the linkset
            ForEachMember(delegate(BSPhysObject cPrim)
            {
                if (!IsRoot(cPrim))
                {
                    // Compute the displacement of the child from the root of the linkset.
                    // This info is saved in the child prim so the relationship does not
                    //    change over time and the new child position can be computed
                    //    when the linkset is being disassembled (the linkset may have moved).
                    BSLinksetCompoundInfo lci = cPrim.LinksetInfo as BSLinksetCompoundInfo;
                    if (lci == null)
                    {
                        // Each child position and rotation is given relative to the root.
                        OMV.Quaternion invRootOrientation = OMV.Quaternion.Inverse(LinksetRoot.RawOrientation);
                        OMV.Vector3 displacementPos = (cPrim.RawPosition - LinksetRoot.RawPosition) * invRootOrientation;
                        OMV.Quaternion displacementRot = cPrim.RawOrientation * invRootOrientation;

                        // Save relative position for recomputing child's world position after moving linkset.
                        lci = new BSLinksetCompoundInfo(displacementPos, displacementRot);
                        cPrim.LinksetInfo = lci;
                        DetailLog("{0},BSLinksetCompound.RecomputeLinksetCompound,creatingRelPos,lci={1}", cPrim.LocalID, lci);
                    }

                    DetailLog("{0},BSLinksetCompound.RecomputeLinksetCompound,addMemberToShape,mID={1},mShape={2},dispPos={3},dispRot={4}",
                        LinksetRoot.LocalID, cPrim.LocalID, cPrim.PhysShape, lci.OffsetPos, lci.OffsetRot);

                    if (cPrim.PhysShape.isNativeShape)
                    {
                        // A native shape is turning into a hull collision shape because native
                        //    shapes are not shared so we have to hullify it so it will be tracked
                        //    and freed at the correct time. This also solves the scaling problem
                        //    (native shapes scaled but hull/meshes are assumed to not be).
                        // TODO: decide of the native shape can just be used in the compound shape.
                        //    Use call to CreateGeomNonSpecial().
                        BulletShape saveShape = cPrim.PhysShape;
                        cPrim.PhysShape.Clear();        // Don't let the create free the child's shape
                        // PhysicsScene.Shapes.CreateGeomNonSpecial(true, cPrim, null);
                        PhysicsScene.Shapes.CreateGeomMeshOrHull(cPrim, null);
                        BulletShape newShape = cPrim.PhysShape;
                        cPrim.PhysShape = saveShape;
                        BulletSimAPI.AddChildShapeToCompoundShape2(LinksetRoot.PhysShape.ptr, newShape.ptr, lci.OffsetPos, lci.OffsetRot);
                    }
                    else
                    {
                        // For the shared shapes (meshes and hulls), just use the shape in the child.
                        // The reference count added here will be decremented when the compound shape
                        //     is destroyed in BSShapeCollection (the child shapes are looped over and dereferenced).
                        if (PhysicsScene.Shapes.ReferenceShape(cPrim.PhysShape))
                        {
                            PhysicsScene.Logger.ErrorFormat("{0} Rebuilt sharable shape when building linkset! Region={1}, primID={2}, shape={3}",
                                                LogHeader, PhysicsScene.RegionName, cPrim.LocalID, cPrim.PhysShape);
                        }
                        BulletSimAPI.AddChildShapeToCompoundShape2(LinksetRoot.PhysShape.ptr, cPrim.PhysShape.ptr, lci.OffsetPos, lci.OffsetRot);
                    }
                }
                return false;   // 'false' says to move onto the next child in the list
            });

            // With all of the linkset packed into the root prim, it has the mass of everyone.
            float linksetMass = LinksetMass;
            LinksetRoot.UpdatePhysicalMassProperties(linksetMass);
        }
        finally
        {
            Rebuilding = false;
        }

        BulletSimAPI.RecalculateCompoundShapeLocalAabb2(LinksetRoot.PhysShape.ptr);

        // DEBUG: see of inter-linkset collisions are causing problems for constraint linksets.
        // BulletSimAPI.SetCollisionFilterMask2(LinksetRoot.BSBody.ptr, 
        //                     (uint)CollisionFilterGroups.LinksetFilter, (uint)CollisionFilterGroups.LinksetMask);

    }
}
}