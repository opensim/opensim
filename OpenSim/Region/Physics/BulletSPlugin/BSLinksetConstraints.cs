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
public class BSLinksetConstraints : BSLinkset
{
    // private static string LogHeader = "[BULLETSIM LINKSET CONSTRAINTS]";

    public BSLinksetConstraints(BSScene scene, BSPhysObject parent)
    {
        base.Initialize(scene, parent);
    }

    // When physical properties are changed the linkset needs to recalculate
    //   its internal properties.
    // May be called at runtime or taint-time (just pass the appropriate flag).
    public override void Refresh(BSPhysObject requestor, bool inTaintTime)
    {
        // If there are no children or not root, I am not the one that recomputes the constraints
        if (!HasAnyChildren || !IsRoot(requestor))
            return;

        BSScene.TaintCallback refreshOperation = delegate()
            {
                RecomputeLinksetConstraintVariables();
                DetailLog("{0},BSLinkset.Refresh,complete,rBody={1}",
                                LinksetRoot.LocalID, LinksetRoot.BSBody.ptr.ToString("X"));
            };
        if (inTaintTime)
            refreshOperation();
        else
            PhysicsScene.TaintedObject("BSLinkSet.Refresh", refreshOperation);
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

    // The object is going static (non-physical). Do any setup necessary
    //     for a static linkset.
    // Return 'true' if any properties updated on the passed object.
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

    // Routine used when rebuilding the body of the root of the linkset
    // Destroy all the constraints have have been made to root.
    // This is called when the root body is changing.
    // Returns 'true' of something eas actually removed and would need restoring
    // Called at taint-time!!
    public override bool RemoveBodyDependencies(BSPrim child)
    {
        bool ret = false;

        lock (m_linksetActivityLock)
        {
            if (IsRoot(child))
            {
                // If the one with the dependency is root, must undo all children
                DetailLog("{0},BSLinkset.RemoveBodyDependencies,removeChildrenForRoot,rID={1},rBody={2}",
                                                child.LocalID, LinksetRoot.LocalID, LinksetRoot.BSBody.ptr.ToString("X"));

                ret = PhysicallyUnlinkAllChildrenFromRoot(LinksetRoot);
            }
            else
            {
                DetailLog("{0},BSLinkset.RemoveBodyDependencies,removeSingleChild,rID={1},rBody={2},cID={3},cBody={4}",
                                                child.LocalID,
                                                LinksetRoot.LocalID, LinksetRoot.BSBody.ptr.ToString("X"),
                                                child.LocalID, child.BSBody.ptr.ToString("X"));
                // ret = PhysicallyUnlinkAChildFromRoot(LinksetRoot, child);
                // Despite the function name, this removes any link to the specified object.
                ret = PhysicallyUnlinkAllChildrenFromRoot(child);
            }
        }
        return ret;
    }

    // Companion to RemoveBodyDependencies(). If RemoveBodyDependencies() returns 'true',
    // this routine will restore the removed constraints.
    // Called at taint-time!!
    public override void RestoreBodyDependencies(BSPrim child)
    {
        lock (m_linksetActivityLock)
        {
            if (IsRoot(child))
            {
                DetailLog("{0},BSLinkset.RestoreBodyDependencies,restoreChildrenForRoot,rID={1},numChild={2}",
                                                child.LocalID, LinksetRoot.LocalID, m_taintChildren.Count);
                foreach (BSPhysObject bpo in m_taintChildren)
                {
                    PhysicallyLinkAChildToRoot(LinksetRoot, bpo);
                }
            }
            else
            {
                DetailLog("{0},BSLinkset.RestoreBodyDependencies,restoreSingleChild,rID={1},rBody={2},cID={3},cBody={4}",
                                                LinksetRoot.LocalID,
                                                LinksetRoot.LocalID, LinksetRoot.BSBody.ptr.ToString("X"),
                                                child.LocalID, child.BSBody.ptr.ToString("X"));
                PhysicallyLinkAChildToRoot(LinksetRoot, child);
            }
        }
    }

    // ================================================================
    // Below this point is internal magic

    // I am the root of a linkset and a new child is being added
    // Called while LinkActivity is locked.
    protected override void AddChildToLinkset(BSPhysObject child)
    {
        if (!HasChild(child))
        {
            m_children.Add(child);

            BSPhysObject rootx = LinksetRoot; // capture the root as of now
            BSPhysObject childx = child;

            DetailLog("{0},AddChildToLinkset,call,child={1}", LinksetRoot.LocalID, child.LocalID);

            PhysicsScene.TaintedObject("AddChildToLinkset", delegate()
            {
                DetailLog("{0},AddChildToLinkset,taint,rID={1},rBody={2},cID={3},cBody={4}",
                                rootx.LocalID,
                                rootx.LocalID, rootx.BSBody.ptr.ToString("X"),
                                childx.LocalID, childx.BSBody.ptr.ToString("X"));
                // Since this is taint-time, the body and shape could have changed for the child
                rootx.ForcePosition = rootx.Position;   // DEBUG
                childx.ForcePosition = childx.Position;   // DEBUG
                PhysicallyLinkAChildToRoot(rootx, childx);
                m_taintChildren.Add(child);
            });
        }
        return;
    }

    // Forcefully removing a child from a linkset.
    // This is not being called by the child so we have to make sure the child doesn't think
    //    it's still connected to the linkset.
    // Normal OpenSimulator operation will never do this because other SceneObjectPart information
    //    also has to be updated (like pointer to prim's parent).
    protected override void RemoveChildFromOtherLinkset(BSPhysObject pchild)
    {
        pchild.Linkset = BSLinkset.Factory(PhysicsScene, pchild);
        RemoveChildFromLinkset(pchild);
    }

    // I am the root of a linkset and one of my children is being removed.
    // Safe to call even if the child is not really in my linkset.
    protected override void RemoveChildFromLinkset(BSPhysObject child)
    {
        if (m_children.Remove(child))
        {
            BSPhysObject rootx = LinksetRoot; // capture the root and body as of now
            BSPhysObject childx = child;

            DetailLog("{0},RemoveChildFromLinkset,call,rID={1},rBody={2},cID={3},cBody={4}",
                            childx.LocalID,
                            rootx.LocalID, rootx.BSBody.ptr.ToString("X"),
                            childx.LocalID, childx.BSBody.ptr.ToString("X"));

            PhysicsScene.TaintedObject("RemoveChildFromLinkset", delegate()
            {
                m_taintChildren.Remove(child);
                PhysicallyUnlinkAChildFromRoot(rootx, childx);
                RecomputeLinksetConstraintVariables();
            });

        }
        else
        {
            // This will happen if we remove the root of the linkset first. Non-fatal occurance.
            // PhysicsScene.Logger.ErrorFormat("{0}: Asked to remove child from linkset that was not in linkset", LogHeader);
        }
        return;
    }

    // Create a constraint between me (root of linkset) and the passed prim (the child).
    // Called at taint time!
    private void PhysicallyLinkAChildToRoot(BSPhysObject rootPrim, BSPhysObject childPrim)
    {
        // Zero motion for children so they don't interpolate
        childPrim.ZeroMotion();

        // Relative position normalized to the root prim
        // Essentually a vector pointing from center of rootPrim to center of childPrim
        OMV.Vector3 childRelativePosition = childPrim.Position - rootPrim.Position;

        // real world coordinate of midpoint between the two objects
        OMV.Vector3 midPoint = rootPrim.Position + (childRelativePosition / 2);

        DetailLog("{0},BSLinkset.PhysicallyLinkAChildToRoot,taint,root={1},rBody={2},child={3},cBody={4},rLoc={5},cLoc={6},midLoc={7}",
                                        rootPrim.LocalID,
                                        rootPrim.LocalID, rootPrim.BSBody.ptr.ToString("X"),
                                        childPrim.LocalID, childPrim.BSBody.ptr.ToString("X"),
                                        rootPrim.Position, childPrim.Position, midPoint);

        // create a constraint that allows no freedom of movement between the two objects
        // http://bulletphysics.org/Bullet/phpBB3/viewtopic.php?t=4818

        BS6DofConstraint constrain = new BS6DofConstraint(
                            PhysicsScene.World, rootPrim.BSBody, childPrim.BSBody, midPoint, true, true );

        /* NOTE: below is an attempt to build constraint with full frame computation, etc.
         *     Using the midpoint is easier since it lets the Bullet code manipulate the transforms
         *     of the objects.
         * Code left as a warning to future programmers.
        // ==================================================================================
        // relative position normalized to the root prim
        OMV.Quaternion invThisOrientation = OMV.Quaternion.Inverse(rootPrim.Orientation);
        OMV.Vector3 childRelativePosition = (childPrim.Position - rootPrim.Position) * invThisOrientation;

        // relative rotation of the child to the parent
        OMV.Quaternion childRelativeRotation = invThisOrientation * childPrim.Orientation;
        OMV.Quaternion inverseChildRelativeRotation = OMV.Quaternion.Inverse(childRelativeRotation);

        // create a constraint that allows no freedom of movement between the two objects
        // http://bulletphysics.org/Bullet/phpBB3/viewtopic.php?t=4818
        DetailLog("{0},BSLinkset.PhysicallyLinkAChildToRoot,taint,root={1},child={2}", rootPrim.LocalID, rootPrim.LocalID, childPrim.LocalID);
        BS6DofConstraint constrain = new BS6DofConstraint(
                        PhysicsScene.World, rootPrim.Body, childPrim.Body,
                        OMV.Vector3.Zero,
                        OMV.Quaternion.Inverse(rootPrim.Orientation),
                        OMV.Vector3.Zero,
                        OMV.Quaternion.Inverse(childPrim.Orientation),
                        // A point half way between the parent and child
                        // childRelativePosition/2,
                        // childRelativeRotation,
                        // childRelativePosition/2,
                        // inverseChildRelativeRotation,
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
        constrain.UseFrameOffset(PhysicsScene.BoolNumeric(PhysicsScene.Params.linkConstraintUseFrameOffset));
        constrain.TranslationalLimitMotor(PhysicsScene.BoolNumeric(PhysicsScene.Params.linkConstraintEnableTransMotor),
                        PhysicsScene.Params.linkConstraintTransMotorMaxVel,
                        PhysicsScene.Params.linkConstraintTransMotorMaxForce);
        constrain.SetCFMAndERP(PhysicsScene.Params.linkConstraintCFM, PhysicsScene.Params.linkConstraintERP);
        if (PhysicsScene.Params.linkConstraintSolverIterations != 0f)
        {
            constrain.SetSolverIterations(PhysicsScene.Params.linkConstraintSolverIterations);
        }
    }

    // Remove linkage between myself and a particular child
    // The root and child bodies are passed in because we need to remove the constraint between
    //      the bodies that were at unlink time.
    // Called at taint time!
    private bool PhysicallyUnlinkAChildFromRoot(BSPhysObject rootPrim, BSPhysObject childPrim)
    {
        bool ret = false;
        DetailLog("{0},BSLinkset.PhysicallyUnlinkAChildFromRoot,taint,root={1},rBody={2},child={3},cBody={4}",
                            rootPrim.LocalID,
                            rootPrim.LocalID, rootPrim.BSBody.ptr.ToString("X"),
                            childPrim.LocalID, childPrim.BSBody.ptr.ToString("X"));

        // Find the constraint for this link and get rid of it from the overall collection and from my list
        if (PhysicsScene.Constraints.RemoveAndDestroyConstraint(rootPrim.BSBody, childPrim.BSBody))
        {
            // Make the child refresh its location
            BulletSimAPI.PushUpdate2(childPrim.BSBody.ptr);
            ret = true;
        }

        return ret;
    }

    // Remove linkage between myself and any possible children I might have.
    // Called at taint time!
    private bool PhysicallyUnlinkAllChildrenFromRoot(BSPhysObject rootPrim)
    {
        DetailLog("{0},BSLinkset.PhysicallyUnlinkAllChildren,taint", rootPrim.LocalID);
        bool ret = false;

        if (PhysicsScene.Constraints.RemoveAndDestroyConstraint(rootPrim.BSBody))
        {
            ret = true;
        }
        return ret;
    }

    // Call each of the constraints that make up this linkset and recompute the
    //    various transforms and variables. Used when objects are added or removed
    //    from a linkset to make sure the constraints know about the new mass and
    //    geometry.
    // Must only be called at taint time!!
    private void RecomputeLinksetConstraintVariables()
    {
        float linksetMass = LinksetMass;
        foreach (BSPhysObject child in m_taintChildren)
        {
            BSConstraint constrain;
            if (PhysicsScene.Constraints.TryGetConstraint(LinksetRoot.BSBody, child.BSBody, out constrain))
            {
                // DetailLog("{0},BSLinkset.RecomputeLinksetConstraintVariables,taint,child={1},mass={2},A={3},B={4}",
                //         LinksetRoot.LocalID, child.LocalID, linksetMass, constrain.Body1.ID, constrain.Body2.ID);
                constrain.RecomputeConstraintVariables(linksetMass);
            }
            else
            {
                // Non-fatal error that happens when children are being added to the linkset but
                //    their constraints have not been created yet.
                break;
            }
        }

        // If the whole linkset is not here, doesn't make sense to recompute linkset wide values
        if (m_children.Count == m_taintChildren.Count)
        {
            // If this is a multiple object linkset, set everybody's center of mass to the set's center of mass
            OMV.Vector3 centerOfMass = ComputeLinksetCenterOfMass();
            BulletSimAPI.SetCenterOfMassByPosRot2(LinksetRoot.BSBody.ptr,
                                centerOfMass, OMV.Quaternion.Identity);
            DetailLog("{0},BSLinkset.RecomputeLinksetConstraintVariables,setCenterOfMass,COM={1},rBody={2}",
                                LinksetRoot.LocalID, centerOfMass, LinksetRoot.BSBody.ptr.ToString("X"));
            foreach (BSPhysObject child in m_taintChildren)
            {
                BulletSimAPI.SetCenterOfMassByPosRot2(child.BSBody.ptr,
                                centerOfMass, OMV.Quaternion.Identity);
            }

            // BulletSimAPI.DumpAllInfo2(PhysicsScene.World.ptr);  // DEBUG DEBUG DEBUG
        }
        return;
    }
}
}
