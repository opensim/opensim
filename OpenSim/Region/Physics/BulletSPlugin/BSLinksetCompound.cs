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

namespace OpenSim.Region.Physics.BulletSPlugin
{
public sealed class BSLinksetCompound : BSLinkset
{
    private static string LogHeader = "[BULLETSIM LINKSET COMPOUND]";

    public BSLinksetCompound(BSScene scene, BSPhysObject parent)
    {
        base.Initialize(scene, parent);
    }

    // For compound implimented linksets, if there are children, use compound shape for the root.
    public override ShapeData.PhysicsShapeType PreferredPhysicalShape(BSPhysObject requestor)
    { 
        ShapeData.PhysicsShapeType ret = ShapeData.PhysicsShapeType.SHAPE_UNKNOWN;
        if (IsRoot(requestor) && HasAnyChildren)
        {
            ret = ShapeData.PhysicsShapeType.SHAPE_COMPOUND;
        }
        // DetailLog("{0},BSLinksetCompound.PreferredPhysicalShape,call,shape={1}", LinksetRoot.LocalID, ret);
        return ret;
    }

    // When physical properties are changed the linkset needs to recalculate
    //   its internal properties.
    // This is queued in the 'post taint' queue so the
    //   refresh will happen once after all the other taints are applied.
    public override void Refresh(BSPhysObject requestor)
    {
        // External request for Refresh (from BSPrim) is not necessary
        // InternalRefresh(requestor);
    }

    private void InternalRefresh(BSPhysObject requestor)
    {
        DetailLog("{0},BSLinksetCompound.Refresh,schedulingRefresh,requestor={1}", LinksetRoot.LocalID, requestor.LocalID);
        // Queue to happen after all the other taint processing
        PhysicsScene.PostTaintObject("BSLinksetCompound.Refresh", requestor.LocalID, delegate()
        {
            if (IsRoot(requestor) && HasAnyChildren)
                RecomputeLinksetCompound();
        });
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
        if (!IsRoot(child))
        {
            // Physical children are removed from the world as the shape ofthe root compound
            //     shape takes over.
            BulletSimAPI.AddToCollisionFlags2(child.PhysBody.ptr, CollisionFlags.CF_NO_CONTACT_RESPONSE);
            BulletSimAPI.ForceActivationState2(child.PhysBody.ptr, ActivationState.DISABLE_SIMULATION);
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
        if (!IsRoot(child))
        {
            // The non-physical children can come back to life.
            BulletSimAPI.RemoveFromCollisionFlags2(child.PhysBody.ptr, CollisionFlags.CF_NO_CONTACT_RESPONSE);
            // Don't force activation so setting of DISABLE_SIMULATION can stay.
            BulletSimAPI.Activate2(child.PhysBody.ptr, false);
            ret = true;
        }
        return ret;
    }

    // Called at taint-time!!
    public override void UpdateProperties(BSPhysObject updated)
    {
        // Nothing to do for constraints on property updates
    }

    // The children move around in relationship to the root.
    // Just grab the current values of wherever it is right now.
    public override OMV.Vector3 Position(BSPhysObject member)
    {
        return BulletSimAPI.GetPosition2(member.PhysBody.ptr);
    }

    public override OMV.Quaternion Orientation(BSPhysObject member)
    {
        return BulletSimAPI.GetOrientation2(member.PhysBody.ptr);
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
            // Cause the current shape to be freed and the new one to be built.
            InternalRefresh(LinksetRoot);
            ret = true;
        }

        return ret;
    }

    // Companion to RemoveBodyDependencies(). If RemoveBodyDependencies() returns 'true',
    // this routine will restore the removed constraints.
    // Called at taint-time!!
    public override void RestoreBodyDependencies(BSPrim child)
    {
        // The Refresh operation queued by RemoveBodyDependencies() will build any missing constraints.
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

            // Cause constraints and assorted properties to be recomputed before the next simulation step.
            InternalRefresh(LinksetRoot);
        }
        return;
    }

    // Remove the specified child from the linkset.
    // Safe to call even if the child is not really in my linkset.
    protected override void RemoveChildFromLinkset(BSPhysObject child)
    {
        if (m_children.Remove(child))
        {
            DetailLog("{0},BSLinksetCompound.RemoveChildFromLinkset,call,rID={1},rBody={2},cID={3},cBody={4}",
                            child.LocalID,
                            LinksetRoot.LocalID, LinksetRoot.PhysBody.ptr.ToString("X"),
                            child.LocalID, child.PhysBody.ptr.ToString("X"));

            // Cause the child's body to be rebuilt and thus restored to normal operation
            child.ForceBodyShapeRebuild(false);

            if (!HasAnyChildren)
            {
                // The linkset is now empty. The root needs rebuilding.
                LinksetRoot.ForceBodyShapeRebuild(false);
            }
            else
            {
                // Schedule a rebuild of the linkset  before the next simulation tick.
                InternalRefresh(LinksetRoot);
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
        // Cause the root shape to be rebuilt as a compound object with just the root in it
        LinksetRoot.ForceBodyShapeRebuild(true);

        DetailLog("{0},BSLinksetCompound.RecomputeLinksetCompound,start,rBody={1},rShape={2},numChildren={3}",
                        LinksetRoot.LocalID, LinksetRoot.PhysBody, LinksetRoot.PhysShape, NumberOfChildren);

        // Add a shape for each of the other children in the linkset
        ForEachMember(delegate(BSPhysObject cPrim)
        {
            if (!IsRoot(cPrim))
            {
                // Each child position and rotation is given relative to the root.
                OMV.Quaternion invRootOrientation = OMV.Quaternion.Inverse(LinksetRoot.RawOrientation);
                OMV.Vector3 displacementPos = (cPrim.RawPosition - LinksetRoot.RawPosition) * invRootOrientation;
                OMV.Quaternion displacementRot = cPrim.RawOrientation * invRootOrientation;

                DetailLog("{0},BSLinksetCompound.RecomputeLinksetCompound,addMemberToShape,mID={1},mShape={2},dispPos={3},dispRot={4}",
                    LinksetRoot.LocalID, cPrim.LocalID, cPrim.PhysShape, displacementPos, displacementRot);

                if (cPrim.PhysShape.isNativeShape)
                {
                    // Native shapes are not shared so we need to create a new one.
                    // A mesh or hull is created because scale is not available on a native shape.
                    //     (TODO: Bullet does have a btScaledCollisionShape. Can that be used?)
                    BulletShape saveShape = cPrim.PhysShape;
                    cPrim.PhysShape.ptr = IntPtr.Zero;  // Don't let the create free the child's shape
                    PhysicsScene.Shapes.CreateGeomMeshOrHull(cPrim, null);
                    BulletShape newShape = cPrim.PhysShape;
                    cPrim.PhysShape = saveShape;
                    BulletSimAPI.AddChildShapeToCompoundShape2(LinksetRoot.PhysShape.ptr, newShape.ptr, displacementPos, displacementRot);
                }
                else
                {
                    // For the shared shapes (meshes and hulls), just use the shape in the child.
                    if (PhysicsScene.Shapes.ReferenceShape(cPrim.PhysShape))
                    {
                        PhysicsScene.Logger.ErrorFormat("{0} Rebuilt sharable shape when building linkset! Region={1}, primID={2}, shape={3}",
                                            LogHeader, PhysicsScene.RegionName, cPrim.LocalID, cPrim.PhysShape);
                    }
                    BulletSimAPI.AddChildShapeToCompoundShape2(LinksetRoot.PhysShape.ptr, cPrim.PhysShape.ptr, displacementPos, displacementRot);
                }
            }
            return false;   // 'false' says to move onto the next child in the list
        });

        // With all of the linkset packed into the root prim, it has the mass of everyone.
        float linksetMass = LinksetMass;
        LinksetRoot.UpdatePhysicalMassProperties(linksetMass);

        // DEBUG: see of inter-linkset collisions are causing problems for constraint linksets.
        // BulletSimAPI.SetCollisionFilterMask2(LinksetRoot.BSBody.ptr, 
        //                     (uint)CollisionFilterGroups.LinksetFilter, (uint)CollisionFilterGroups.LinksetMask);

    }
}
}