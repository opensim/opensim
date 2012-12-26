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

namespace OpenSim.Region.Physics.BulletSNPlugin
{
public sealed class BSLinksetConstraints : BSLinkset
{
    // private static string LogHeader = "[BULLETSIM LINKSET CONSTRAINTS]";

    public BSLinksetConstraints(BSScene scene, BSPhysObject parent) : base(scene, parent)
    {
    }

    // When physical properties are changed the linkset needs to recalculate
    //   its internal properties.
    // This is queued in the 'post taint' queue so the
    //   refresh will happen once after all the other taints are applied.
    public override void Refresh(BSPhysObject requestor)
    {
        // Queue to happen after all the other taint processing
        PhysicsScene.PostTaintObject("BSLinksetContraints.Refresh", requestor.LocalID, delegate()
            {
                if (HasAnyChildren && IsRoot(requestor))
                    RecomputeLinksetConstraints();
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
    public override void UpdateProperties(BSPhysObject updated, bool inTaintTime)
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

        DetailLog("{0},BSLinksetConstraint.RemoveBodyDependencies,removeChildrenForRoot,rID={1},rBody={2}",
                                    child.LocalID, LinksetRoot.LocalID, LinksetRoot.PhysBody.ptr.ToString());

        lock (m_linksetActivityLock)
        {
            // Just undo all the constraints for this linkset. Rebuild at the end of the step.
            ret = PhysicallyUnlinkAllChildrenFromRoot(LinksetRoot);
            // Cause the constraints, et al to be rebuilt before the next simulation step.
            Refresh(LinksetRoot);
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

            DetailLog("{0},BSLinksetConstraints.AddChildToLinkset,call,child={1}", LinksetRoot.LocalID, child.LocalID);

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
            BSPhysObject rootx = LinksetRoot; // capture the root and body as of now
            BSPhysObject childx = child;

            DetailLog("{0},BSLinksetConstraints.RemoveChildFromLinkset,call,rID={1},rBody={2},cID={3},cBody={4}",
                            childx.LocalID,
                            rootx.LocalID, rootx.PhysBody.ptr.ToString(),
                            childx.LocalID, childx.PhysBody.ptr.ToString());

            PhysicsScene.TaintedObject("BSLinksetConstraints.RemoveChildFromLinkset", delegate()
            {
                PhysicallyUnlinkAChildFromRoot(rootx, childx);
            });
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

    // Create a constraint between me (root of linkset) and the passed prim (the child).
    // Called at taint time!
    private void PhysicallyLinkAChildToRoot(BSPhysObject rootPrim, BSPhysObject childPrim)
    {
        // Don't build the constraint when asked. Put it off until just before the simulation step.
        Refresh(rootPrim);
    }

    private BSConstraint BuildConstraint(BSPhysObject rootPrim, BSPhysObject childPrim)
    {
        // Zero motion for children so they don't interpolate
        childPrim.ZeroMotion(true);

        // Relative position normalized to the root prim
        // Essentually a vector pointing from center of rootPrim to center of childPrim
        OMV.Vector3 childRelativePosition = childPrim.Position - rootPrim.Position;

        // real world coordinate of midpoint between the two objects
        OMV.Vector3 midPoint = rootPrim.Position + (childRelativePosition / 2);

        DetailLog("{0},BSLinksetConstraint.BuildConstraint,taint,root={1},rBody={2},child={3},cBody={4},rLoc={5},cLoc={6},midLoc={7}",
                                        rootPrim.LocalID,
                                        rootPrim.LocalID, rootPrim.PhysBody.ptr.ToString(),
                                        childPrim.LocalID, childPrim.PhysBody.ptr.ToString(),
                                        rootPrim.Position, childPrim.Position, midPoint);

        // create a constraint that allows no freedom of movement between the two objects
        // http://bulletphysics.org/Bullet/phpBB3/viewtopic.php?t=4818

        BSConstraint6Dof constrain = new BSConstraint6Dof(
                            PhysicsScene.World, rootPrim.PhysBody, childPrim.PhysBody, midPoint, true, true );
                            // PhysicsScene.World, childPrim.BSBody, rootPrim.BSBody, midPoint, true, true );

        /* NOTE: below is an attempt to build constraint with full frame computation, etc.
         *     Using the midpoint is easier since it lets the Bullet code manipulate the transforms
         *     of the objects.
         * Code left for future programmers.
        // ==================================================================================
        // relative position normalized to the root prim
        OMV.Quaternion invThisOrientation = OMV.Quaternion.Inverse(rootPrim.Orientation);
        OMV.Vector3 childRelativePosition = (childPrim.Position - rootPrim.Position) * invThisOrientation;

        // relative rotation of the child to the parent
        OMV.Quaternion childRelativeRotation = invThisOrientation * childPrim.Orientation;
        OMV.Quaternion inverseChildRelativeRotation = OMV.Quaternion.Inverse(childRelativeRotation);

        DetailLog("{0},BSLinksetConstraint.PhysicallyLinkAChildToRoot,taint,root={1},child={2}", rootPrim.LocalID, rootPrim.LocalID, childPrim.LocalID);
        BS6DofConstraint constrain = new BS6DofConstraint(
                        PhysicsScene.World, rootPrim.Body, childPrim.Body,
                        OMV.Vector3.Zero,
                        OMV.Quaternion.Inverse(rootPrim.Orientation),
                        OMV.Vector3.Zero,
                        OMV.Quaternion.Inverse(childPrim.Orientation),
                        true,
                        true
                        );
        // ==================================================================================
        */

        PhysicsScene.Constraints.AddConstraint(constrain);

        // zero linear and angular limits makes the objects unable to move in relation to each other
        constrain.SetLinearLimits(OMV.Vector3.Zero, OMV.Vector3.Zero);
        constrain.SetAngularLimits(OMV.Vector3.Zero, OMV.Vector3.Zero);

        // tweek the constraint to increase stability
        constrain.UseFrameOffset(BSParam.BoolNumeric(BSParam.LinkConstraintUseFrameOffset));
        constrain.TranslationalLimitMotor(BSParam.BoolNumeric(BSParam.LinkConstraintEnableTransMotor),
                        BSParam.LinkConstraintTransMotorMaxVel,
                        BSParam.LinkConstraintTransMotorMaxForce);
        constrain.SetCFMAndERP(BSParam.LinkConstraintCFM, BSParam.LinkConstraintERP);
        if (BSParam.LinkConstraintSolverIterations != 0f)
        {
            constrain.SetSolverIterations(BSParam.LinkConstraintSolverIterations);
        }
        return constrain;
    }

    // Remove linkage between the linkset root and a particular child
    // The root and child bodies are passed in because we need to remove the constraint between
    //      the bodies that were present at unlink time.
    // Called at taint time!
    private bool PhysicallyUnlinkAChildFromRoot(BSPhysObject rootPrim, BSPhysObject childPrim)
    {
        bool ret = false;
        DetailLog("{0},BSLinksetConstraint.PhysicallyUnlinkAChildFromRoot,taint,root={1},rBody={2},child={3},cBody={4}",
                            rootPrim.LocalID,
                            rootPrim.LocalID, rootPrim.PhysBody.ptr.ToString(),
                            childPrim.LocalID, childPrim.PhysBody.ptr.ToString());

        // Find the constraint for this link and get rid of it from the overall collection and from my list
        if (PhysicsScene.Constraints.RemoveAndDestroyConstraint(rootPrim.PhysBody, childPrim.PhysBody))
        {
            // Make the child refresh its location
            BulletSimAPI.PushUpdate2(childPrim.PhysBody.ptr);
            ret = true;
        }

        return ret;
    }

    // Remove linkage between myself and any possible children I might have.
    // Returns 'true' of any constraints were destroyed.
    // Called at taint time!
    private bool PhysicallyUnlinkAllChildrenFromRoot(BSPhysObject rootPrim)
    {
        DetailLog("{0},BSLinksetConstraint.PhysicallyUnlinkAllChildren,taint", rootPrim.LocalID);

        return PhysicsScene.Constraints.RemoveAndDestroyConstraint(rootPrim.PhysBody);
    }

    // Call each of the constraints that make up this linkset and recompute the
    //    various transforms and variables. Create constraints of not created yet.
    // Called before the simulation step to make sure the constraint based linkset
    //    is all initialized.
    // Called at taint time!!
    private void RecomputeLinksetConstraints()
    {
        float linksetMass = LinksetMass;
        LinksetRoot.UpdatePhysicalMassProperties(linksetMass);

        // DEBUG: see of inter-linkset collisions are causing problems
        // BulletSimAPI.SetCollisionFilterMask2(LinksetRoot.BSBody.ptr, 
        //                     (uint)CollisionFilterGroups.LinksetFilter, (uint)CollisionFilterGroups.LinksetMask);
        DetailLog("{0},BSLinksetConstraint.RecomputeLinksetConstraints,set,rBody={1},linksetMass={2}",
                            LinksetRoot.LocalID, LinksetRoot.PhysBody.ptr.ToString(), linksetMass);

        foreach (BSPhysObject child in m_children)
        {
            // A child in the linkset physically shows the mass of the whole linkset.
            // This allows Bullet to apply enough force on the child to move the whole linkset.
            // (Also do the mass stuff before recomputing the constraint so mass is not zero.)
            child.UpdatePhysicalMassProperties(linksetMass);

            BSConstraint constrain;
            if (!PhysicsScene.Constraints.TryGetConstraint(LinksetRoot.PhysBody, child.PhysBody, out constrain))
            {
                // If constraint doesn't exist yet, create it.
                constrain = BuildConstraint(LinksetRoot, child);
            }
            constrain.RecomputeConstraintVariables(linksetMass);

            // DEBUG: see of inter-linkset collisions are causing problems
            // BulletSimAPI.SetCollisionFilterMask2(child.BSBody.ptr, 
            //                 (uint)CollisionFilterGroups.LinksetFilter, (uint)CollisionFilterGroups.LinksetMask);

            // BulletSimAPI.DumpConstraint2(PhysicsScene.World.ptr, constrain.Constraint.ptr);    // DEBUG DEBUG
        }

    }
}
}
