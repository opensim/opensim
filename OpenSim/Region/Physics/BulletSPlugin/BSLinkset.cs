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
public class BSLinkset
{
    private static string LogHeader = "[BULLETSIM LINKSET]";

    public BSPhysObject LinksetRoot { get; protected set; }

    public BSScene PhysicsScene { get; private set; }

    static int m_nextLinksetID = 1;
    public int LinksetID { get; private set; }

    // The children under the root in this linkset.
    // There are two lists of children: the current children at runtime
    //    and the children at taint-time. For instance, if you delink a
    //    child from the linkset, the child is removed from m_children
    //    but the constraint won't be removed until taint time.
    //    Two lists lets this track the 'current' children and
    //    the physical 'taint' children separately.
    // After taint processing and before the simulation step, these
    //    two lists must be the same.
    private List<BSPhysObject> m_children;
    private List<BSPhysObject> m_taintChildren;

    // We lock the diddling of linkset classes to prevent any badness.
    // This locks the modification of the instances of this class. Changes
    //    to the physical representation is done via the tainting mechenism.
    private object m_linksetActivityLock = new Object();

    // We keep the prim's mass in the linkset structure since it could be dependent on other prims
    private float m_mass;
    public float LinksetMass
    {
        get
        {
            m_mass = ComputeLinksetMass();
            return m_mass;
        }
    }

    public OMV.Vector3 CenterOfMass
    {
        get { return ComputeLinksetCenterOfMass(); }
    }

    public OMV.Vector3 GeometricCenter
    {
        get { return ComputeLinksetGeometricCenter(); }
    }

    public BSLinkset(BSScene scene, BSPhysObject parent)
    {
        // A simple linkset of one (no children)
        LinksetID = m_nextLinksetID++;
        // We create LOTS of linksets.
        if (m_nextLinksetID <= 0)
            m_nextLinksetID = 1;
        PhysicsScene = scene;
        LinksetRoot = parent;
        m_children = new List<BSPhysObject>();
        m_taintChildren = new List<BSPhysObject>();
        m_mass = parent.MassRaw;
    }

    // Link to a linkset where the child knows the parent.
    // Parent changing should not happen so do some sanity checking.
    // We return the parent's linkset so the child can track its membership.
    // Called at runtime.
    public BSLinkset AddMeToLinkset(BSPhysObject child)
    {
        lock (m_linksetActivityLock)
        {
            // Don't add the root to its own linkset
            if (!IsRoot(child))
                AddChildToLinkset(child);
        }
        return this;
    }

    // Remove a child from a linkset.
    // Returns a new linkset for the child which is a linkset of one (just the
    //    orphened child).
    // Called at runtime.
    public BSLinkset RemoveMeFromLinkset(BSPhysObject child)
    {
        lock (m_linksetActivityLock)
        {
            if (IsRoot(child))
            {
                // Cannot remove the root from a linkset.
                return this;
            }

            RemoveChildFromLinkset(child);
        }

        // The child is down to a linkset of just itself
        return new BSLinkset(PhysicsScene, child);
    }

    // Return 'true' if the passed object is the root object of this linkset
    public bool IsRoot(BSPhysObject requestor)
    {
        return (requestor.LocalID == LinksetRoot.LocalID);
    }

    public int NumberOfChildren { get { return m_children.Count; } }

    // Return 'true' if this linkset has any children (more than the root member)
    public bool HasAnyChildren { get { return (m_children.Count > 0); } }

    // Return 'true' if this child is in this linkset
    public bool HasChild(BSPhysObject child)
    {
        bool ret = false;
        lock (m_linksetActivityLock)
        {
            foreach (BSPhysObject bp in m_children)
            {
                if (child.LocalID == bp.LocalID)
                {
                    ret = true;
                    break;
                }
            }
        }
        return ret;
    }

    // The object is going dynamic (physical). Do any setup necessary
    //     for a dynamic linkset.
    // Only the state of the passed object can be modified. The rest of the linkset
    //     has not yet been fully constructed.
    // Return 'true' if any properties updated on the passed object.
    // Called at taint-time!
    public bool MakeDynamic(BSPhysObject child)
    {
        // What is done for each object in BSPrim is what we want.
        return false;
    }

    // The object is going static (non-physical). Do any setup necessary
    //     for a static linkset.
    // Return 'true' if any properties updated on the passed object.
    // Called at taint-time!
    public bool MakeStatic(BSPhysObject child)
    {
        // What is done for each object in BSPrim is what we want.
        return false;
    }

    // When physical properties are changed the linkset needs to recalculate
    //   its internal properties.
    // Called at runtime.
    public void Refresh(BSPhysObject requestor)
    {
        // If there are no children, there can't be any constraints to recompute
        if (!HasAnyChildren)
            return;

        // Only the root does the recomputation
        if (IsRoot(requestor))
        {
            PhysicsScene.TaintedObject("BSLinkSet.Refresh", delegate()
            {
                RecomputeLinksetConstraintVariables();
            });
        }
    }

    // Routine used when rebuilding the body of the root of the linkset
    // Destroy all the constraints have have been made to root.
    // This is called when the root body is changing.
    // Returns 'true' of something eas actually removed and would need restoring
    // Called at taint-time!!
    public bool RemoveBodyDependencies(BSPrim child)
    {
        bool ret = false;

        lock (m_linksetActivityLock)
        {
            if (IsRoot(child))
            {
                // If the one with the dependency is root, must undo all children
                DetailLog("{0},BSLinkset.RemoveBodyDependencies,removeChildrenForRoot,rID={1},numChild={2}",
                                                child.LocalID, LinksetRoot.LocalID, m_taintChildren.Count);
                foreach (BSPhysObject bpo in m_taintChildren)
                {
                    PhysicallyUnlinkAChildFromRoot(LinksetRoot, LinksetRoot.BSBody, bpo, bpo.BSBody);
                    ret = true;
                }
            }
            else
            {
                DetailLog("{0},BSLinkset.RemoveBodyDependencies,removeSingleChild,rID={1},rBody={2},cID={3},cBody={4}",
                                                child.LocalID,
                                                LinksetRoot.LocalID, LinksetRoot.BSBody.ptr.ToString("X"),
                                                child.LocalID, child.BSBody.ptr.ToString("X"));
                // Remove the dependency on the body of this one 
                if (m_taintChildren.Contains(child))
                {
                    PhysicallyUnlinkAChildFromRoot(LinksetRoot, LinksetRoot.BSBody, child, child.BSBody);
                    ret = true;
                }
            }
        }
        return ret;
    }

    // Routine used when rebuilding the body of the root of the linkset
    // This is called after RemoveAllLinksToRoot() to restore all the constraints.
    // This is called when the root body has been changed.
    // Called at taint-time!!
    public void RestoreBodyDependencies(BSPrim child)
    {
        lock (m_linksetActivityLock)
        {
            if (IsRoot(child))
            {
                DetailLog("{0},BSLinkset.RestoreBodyDependencies,restoreChildrenForRoot,rID={1},numChild={2}",
                                                child.LocalID, LinksetRoot.LocalID, m_taintChildren.Count);
                foreach (BSPhysObject bpo in m_taintChildren)
                {
                    PhysicallyLinkAChildToRoot(LinksetRoot, LinksetRoot.BSBody, bpo, bpo.BSBody);
                }
            }
            else
            {
                DetailLog("{0},BSLinkset.RestoreBodyDependencies,restoreSingleChild,rID={1},rBody={2},cID={3},cBody={4}",
                                                LinksetRoot.LocalID,
                                                LinksetRoot.LocalID, LinksetRoot.BSBody.ptr.ToString("X"),
                                                child.LocalID, child.BSBody.ptr.ToString("X"));
                PhysicallyLinkAChildToRoot(LinksetRoot, LinksetRoot.BSBody, child, child.BSBody);
            }
        }
    }

    // ================================================================
    // Below this point is internal magic

    private float ComputeLinksetMass()
    {
        float mass;
        lock (m_linksetActivityLock)
        {
            mass = LinksetRoot.MassRaw;
            foreach (BSPhysObject bp in m_taintChildren)
            {
                mass += bp.MassRaw;
            }
        }
        return mass;
    }

    private OMV.Vector3 ComputeLinksetCenterOfMass()
    {
        OMV.Vector3 com;
        lock (m_linksetActivityLock)
        {
            com = LinksetRoot.Position * LinksetRoot.MassRaw;
            float totalMass = LinksetRoot.MassRaw;

            foreach (BSPhysObject bp in m_taintChildren)
            {
                com += bp.Position * bp.MassRaw;
                totalMass += bp.MassRaw;
            }
            if (totalMass != 0f)
                com /= totalMass;
        }

        return com;
    }

    private OMV.Vector3 ComputeLinksetGeometricCenter()
    {
        OMV.Vector3 com;
        lock (m_linksetActivityLock)
        {
            com = LinksetRoot.Position;

            foreach (BSPhysObject bp in m_taintChildren)
            {
                com += bp.Position * bp.MassRaw;
            }
            com /= (m_taintChildren.Count + 1);
        }

        return com;
    }

    // I am the root of a linkset and a new child is being added
    // Called while LinkActivity is locked.
    private void AddChildToLinkset(BSPhysObject child)
    {
        if (!HasChild(child))
        {
            m_children.Add(child);

            BSPhysObject rootx = LinksetRoot; // capture the root and body as of now
            BulletBody rootBodyx = LinksetRoot.BSBody;
            BSPhysObject childx = child;
            BulletBody childBodyx = child.BSBody;

            DetailLog("{0},AddChildToLinkset,call,rID={1},rBody={2},cID={3},cBody={4}", 
                            rootx.LocalID,
                            rootx.LocalID, rootBodyx.ptr.ToString("X"),
                            childx.LocalID, childBodyx.ptr.ToString("X"));

            PhysicsScene.TaintedObject("AddChildToLinkset", delegate()
            {
                DetailLog("{0},AddChildToLinkset,taint,child={1}", LinksetRoot.LocalID, child.LocalID);
                // build the physical binding between me and the child
                m_taintChildren.Add(childx);
                PhysicallyLinkAChildToRoot(rootx, rootBodyx, childx, childBodyx);
            });
        }
        return;
    }

    // Forcefully removing a child from a linkset.
    // This is not being called by the child so we have to make sure the child doesn't think
    //    it's still connected to the linkset.
    // Normal OpenSimulator operation will never do this because other SceneObjectPart information
    //    also has to be updated (like pointer to prim's parent).
    private void RemoveChildFromOtherLinkset(BSPhysObject pchild)
    {
        pchild.Linkset = new BSLinkset(PhysicsScene, pchild);
        RemoveChildFromLinkset(pchild);
    }

    // I am the root of a linkset and one of my children is being removed.
    // Safe to call even if the child is not really in my linkset.
    private void RemoveChildFromLinkset(BSPhysObject child)
    {
        if (m_children.Remove(child))
        {
            BSPhysObject rootx = LinksetRoot; // capture the root and body as of now
            BulletBody rootBodyx = LinksetRoot.BSBody;
            BSPhysObject childx = child;
            BulletBody childBodyx = child.BSBody;

            DetailLog("{0},RemoveChildFromLinkset,call,rID={1},rBody={2},cID={3},cBody={4}", 
                            childx.LocalID,
                            rootx.LocalID, rootBodyx.ptr.ToString("X"),
                            childx.LocalID, childBodyx.ptr.ToString("X"));

            PhysicsScene.TaintedObject("RemoveChildFromLinkset", delegate()
            {
                if (m_taintChildren.Contains(childx))
                    m_taintChildren.Remove(childx);

                PhysicallyUnlinkAChildFromRoot(rootx, rootBodyx, childx, childBodyx);
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
    private void PhysicallyLinkAChildToRoot(BSPhysObject rootPrim, BulletBody rootBody,
                                    BSPhysObject childPrim, BulletBody childBody)
    {
        // Zero motion for children so they don't interpolate
        childPrim.ZeroMotion();

        // Relative position normalized to the root prim
        // Essentually a vector pointing from center of rootPrim to center of childPrim
        OMV.Vector3 childRelativePosition = childPrim.Position - rootPrim.Position;

        // real world coordinate of midpoint between the two objects
        OMV.Vector3 midPoint = rootPrim.Position + (childRelativePosition / 2);

        DetailLog("{0},PhysicallyLinkAChildToRoot,taint,root={1},rBody={2},child={3},cBody={4},rLoc={5},cLoc={6},midLoc={7}",
                                        rootPrim.LocalID,
                                        rootPrim.LocalID, rootBody.ptr.ToString("X"),
                                        childPrim.LocalID, childBody.ptr.ToString("X"),
                                        rootPrim.Position, childPrim.Position, midPoint);

        // create a constraint that allows no freedom of movement between the two objects
        // http://bulletphysics.org/Bullet/phpBB3/viewtopic.php?t=4818

        // There is great subtlty in these paramters. Notice the check for a ptr of zero.
        // We pass the BulletBody structure into the taint in order to capture the pointer
        //     of the body at the time of constraint creation. This doesn't work for the very first
        //     construction because there is no body yet. The body
        //     is constructed later at taint time. Thus we use the body address at time of the
        //     taint creation but, if it is zero, use what's in the prim at the moment.
        //     There is a possible race condition since shape can change without a taint call
        //     (like changing to a mesh that is already constructed). The fix for that would be
        //     to only change BSShape at taint time thus syncronizing these operations at
        //     the cost of efficiency and lag.
        BS6DofConstraint constrain = new BS6DofConstraint(
                        PhysicsScene.World,
                        rootBody.ptr == IntPtr.Zero ? rootPrim.BSBody : rootBody,
                        childBody.ptr == IntPtr.Zero ? childPrim.BSBody : childBody,
                        midPoint,
                        true,
                        true
                        );

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
        DetailLog("{0},PhysicallyLinkAChildToRoot,taint,root={1},child={2}", rootPrim.LocalID, rootPrim.LocalID, childPrim.LocalID);
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

        RecomputeLinksetConstraintVariables();
    }

    // Remove linkage between myself and a particular child
    // The root and child bodies are passed in because we need to remove the constraint between
    //      the bodies that were at unlink time.
    // Called at taint time!
    private void PhysicallyUnlinkAChildFromRoot(BSPhysObject rootPrim, BulletBody rootBody,
                                                    BSPhysObject childPrim, BulletBody childBody)
    {
        DetailLog("{0},PhysicallyUnlinkAChildFromRoot,taint,root={1},rBody={2},child={3},cBody={4}",
                            rootPrim.LocalID,
                            rootPrim.LocalID, rootBody.ptr.ToString("X"),
                            childPrim.LocalID, childBody.ptr.ToString("X"));

        // Find the constraint for this link and get rid of it from the overall collection and from my list
        PhysicsScene.Constraints.RemoveAndDestroyConstraint(rootBody, childBody);

        // Make the child refresh its location
        BulletSimAPI.PushUpdate2(childPrim.BSBody.ptr);
    }

    /*
    // Remove linkage between myself and any possible children I might have.
    // Called at taint time!
    private void PhysicallyUnlinkAllChildrenFromRoot(BSPhysObject rootPrim)
    {
        DetailLog("{0},PhysicallyUnlinkAllChildren,taint", rootPrim.LocalID);

        PhysicsScene.Constraints.RemoveAndDestroyConstraint(rootPrim.BSBody);
    }
     */

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
            BulletSimAPI.SetCenterOfMassByPosRot2(LinksetRoot.BSBody.ptr, centerOfMass, OMV.Quaternion.Identity);
            foreach (BSPhysObject child in m_taintChildren)
            {
                BulletSimAPI.SetCenterOfMassByPosRot2(child.BSBody.ptr, centerOfMass, OMV.Quaternion.Identity);
            }
        }
        return;
    }


    // Invoke the detailed logger and output something if it's enabled.
    private void DetailLog(string msg, params Object[] args)
    {
        PhysicsScene.PhysicsLogging.Write(msg, args);
    }

}
}
