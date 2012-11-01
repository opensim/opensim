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
    // private static string LogHeader = "[BULLETSIM LINKSET CONSTRAINTS]";

    public BSLinksetCompound(BSScene scene, BSPhysObject parent)
    {
        base.Initialize(scene, parent);
    }

    // When physical properties are changed the linkset needs to recalculate
    //   its internal properties.
    // This is queued in the 'post taint' queue so the
    //   refresh will happen once after all the other taints are applied.
    public override void Refresh(BSPhysObject requestor)
    {
        // Queue to happen after all the other taint processing
        PhysicsScene.PostTaintObject("BSLinksetcompound.Refresh", requestor.LocalID, delegate()
            {
                if (HasAnyChildren && IsRoot(requestor))
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
        // What is done for each object in BSPrim is what we want.
        return false;
    }

    // The object is going static (non-physical). Do any setup necessary for a static linkset.
    // Return 'true' if any properties updated on the passed object.
    // This doesn't normally happen -- OpenSim removes the objects from the physical
    //     world if it is a static linkset.
    // Called at taint-time!
    public override bool MakeStatic(BSPhysObject child)
    {
        // What is done for each object in BSPrim is what we want.
        return false;
    }

    // Called at taint-time!!
    public override void UpdateProperties(BSPhysObject updated)
    {
        // Nothing to do for constraints on property updates
    }

    // Routine called when rebuilding the body of some member of the linkset.
    // Destroy all the constraints have have been made to root and set
    //     up to rebuild the constraints before the next simulation step.
    // Returns 'true' of something was actually removed and would need restoring
    // Called at taint-time!!
    public override bool RemoveBodyDependencies(BSPrim child)
    {
        bool ret = false;

        DetailLog("{0},BSLinksetcompound.RemoveBodyDependencies,removeChildrenForRoot,rID={1},rBody={2}",
                                    child.LocalID, LinksetRoot.LocalID, LinksetRoot.PhysBody.ptr.ToString("X"));

        // Cause the current shape to be freed and the new one to be built.
        Refresh(LinksetRoot);

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
            Refresh(LinksetRoot);
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

            // See that the linkset parameters are recomputed at the end of the taint time.
            Refresh(LinksetRoot);
        }
        else
        {
            // Non-fatal occurance.
            // PhysicsScene.Logger.ErrorFormat("{0}: Asked to remove child from linkset that was not in linkset", LogHeader);
        }
        return;
    }


    // Call each of the constraints that make up this linkset and recompute the
    //    various transforms and variables. Create constraints of not created yet.
    // Called before the simulation step to make sure the constraint based linkset
    //    is all initialized.
    // Called at taint time!!
    private void RecomputeLinksetCompound()
    {
        // Release the existing shape
        PhysicsScene.Shapes.DereferenceShape(LinksetRoot.PhysShape, true, null);
        
        float linksetMass = LinksetMass;
        LinksetRoot.UpdatePhysicalMassProperties(linksetMass);

            // DEBUG: see of inter-linkset collisions are causing problems
        // BulletSimAPI.SetCollisionFilterMask2(LinksetRoot.BSBody.ptr, 
        //                     (uint)CollisionFilterGroups.LinksetFilter, (uint)CollisionFilterGroups.LinksetMask);
        DetailLog("{0},BSLinksetCompound.RecomputeLinksetCompound,set,rBody={1},linksetMass={2}",
                            LinksetRoot.LocalID, LinksetRoot.PhysBody.ptr.ToString("X"), linksetMass);


    }
}
}