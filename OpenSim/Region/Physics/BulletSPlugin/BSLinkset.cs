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

    private BSPrim m_linksetRoot;
    public BSPrim LinksetRoot { get { return m_linksetRoot; } }

    private BSScene m_physicsScene;
    public BSScene PhysicsScene { get { return m_physicsScene; } }

    // The children under the root in this linkset
    private List<BSPrim> m_children;

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

    public BSLinkset(BSScene scene, BSPrim parent)
    {
        // A simple linkset of one (no children)
        m_physicsScene = scene;
        m_linksetRoot = parent;
        m_children = new List<BSPrim>();
        m_mass = parent.MassRaw;
    }

    // Link to a linkset where the child knows the parent.
    // Parent changing should not happen so do some sanity checking.
    // We return the parent's linkset so the child can track its membership.
    public BSLinkset AddMeToLinkset(BSPrim child)
    {
        lock (m_linksetActivityLock)
        {
            AddChildToLinkset(child);
        }
        return this;
    }

    // Remove a child from a linkset.
    // Returns a new linkset for the child which is a linkset of one (just the
    //    orphened child).
    public BSLinkset RemoveMeFromLinkset(BSPrim child)
    {
        lock (m_linksetActivityLock)
        {
            if (IsRoot(child))
            {
                // if root of linkset, take the linkset apart
                while (m_children.Count > 0)
                {
                    // Note that we don't do a foreach because the remove routine
                    //    takes it out of the list.
                    RemoveChildFromOtherLinkset(m_children[0]);
                }
                m_children.Clear(); // just to make sure
            }
            else
            {
                // Just removing a child from an existing linkset
                RemoveChildFromLinkset(child);
            }
        }

        // The child is down to a linkset of just itself
        return new BSLinkset(PhysicsScene, child);
    }

    // Return 'true' if the passed object is the root object of this linkset
    public bool IsRoot(BSPrim requestor)
    {
        return (requestor.LocalID == m_linksetRoot.LocalID);
    }

    public int NumberOfChildren { get { return m_children.Count; } }

    // Return 'true' if this linkset has any children (more than the root member)
    public bool HasAnyChildren { get { return (m_children.Count > 0); } }

    // Return 'true' if this child is in this linkset
    public bool HasChild(BSPrim child)
    {
        bool ret = false;
        lock (m_linksetActivityLock)
        {
            foreach (BSPrim bp in m_children)
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

    private float ComputeLinksetMass()
    {
        float mass = m_linksetRoot.MassRaw;
        foreach (BSPrim bp in m_children)
        {
            mass += bp.MassRaw;
        }
        return mass;
    }

    private OMV.Vector3 ComputeLinksetCenterOfMass()
    {
        OMV.Vector3 com = m_linksetRoot.Position * m_linksetRoot.MassRaw;
        float totalMass = m_linksetRoot.MassRaw;

        lock (m_linksetActivityLock)
        {
            foreach (BSPrim bp in m_children)
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
        OMV.Vector3 com = m_linksetRoot.Position;

        lock (m_linksetActivityLock)
        {
            foreach (BSPrim bp in m_children)
            {
                com += bp.Position * bp.MassRaw;
            }
            com /= (m_children.Count + 1);
        }

        return com;
    }

    // When physical properties are changed the linkset needs to recalculate
    //   its internal properties.
    public void Refresh(BSPrim requestor)
    {
        // If there are no children, there aren't any constraints to recompute
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

    // Call each of the constraints that make up this linkset and recompute the
    //    various transforms and variables. Used when objects are added or removed
    //    from a linkset to make sure the constraints know about the new mass and
    //    geometry.
    // Must only be called at taint time!!
    private bool RecomputeLinksetConstraintVariables()
    {
        float linksetMass = LinksetMass;
        lock (m_linksetActivityLock)
        {
            foreach (BSPrim child in m_children)
            {
                BSConstraint constrain;
                if (m_physicsScene.Constraints.TryGetConstraint(LinksetRoot.Body, child.Body, out constrain))
                {
                    // DetailLog("{0},BSLinkset.RecomputeLinksetConstraintVariables,taint,child={1},mass={2},A={3},B={4}", 
                    //         LinksetRoot.LocalID, child.LocalID, linksetMass, constrain.Body1.ID, constrain.Body2.ID);
                    constrain.RecomputeConstraintVariables(linksetMass);
                }
                else
                {
                    // Non-fatal error that can happen when children are being added to the linkset but
                    //    their constraints have not been created yet.
                    // Caused by the fact that m_children is built at run time but building constraints
                    //    happens at taint time.
                    // m_physicsScene.Logger.ErrorFormat("[BULLETSIM LINKSET] RecomputeLinksetConstraintVariables: constraint not found for root={0}, child={1}",
                    //                                 m_linksetRoot.Body.ID, child.Body.ID);
                }
            }
        }
        return false;
    }

    // I am the root of a linkset and a new child is being added
    // Called while LinkActivity is locked.
    private void AddChildToLinkset(BSPrim child)
    {
        if (!HasChild(child))
        {
            m_children.Add(child);

            BSPrim rootx = LinksetRoot; // capture the root as of now
            BSPrim childx = child;
            m_physicsScene.TaintedObject("AddChildToLinkset", delegate()
            {
                // DebugLog("{0}: AddChildToLinkset: adding child {1} to {2}", LogHeader, child.LocalID, m_linksetRoot.LocalID);
                // DetailLog("{0},AddChildToLinkset,taint,child={1}", m_linksetRoot.LocalID, child.LocalID);
                PhysicallyLinkAChildToRoot(rootx, childx);     // build the physical binding between me and the child
            });
        }
        return;
    }

    // Forcefully removing a child from a linkset.
    // This is not being called by the child so we have to make sure the child doesn't think
    //    it's still connected to the linkset.
    // Normal OpenSimulator operation will never do this because other SceneObjectPart information
    //    has to be updated also (like pointer to prim's parent).
    private void RemoveChildFromOtherLinkset(BSPrim pchild)
    {
        pchild.Linkset = new BSLinkset(m_physicsScene, pchild);
        RemoveChildFromLinkset(pchild);
    }

    // I am the root of a linkset and one of my children is being removed.
    // Safe to call even if the child is not really in my linkset.
    private void RemoveChildFromLinkset(BSPrim child)
    {
        if (m_children.Remove(child))
        {
            BSPrim rootx = LinksetRoot; // capture the root as of now
            BSPrim childx = child;
            m_physicsScene.TaintedObject("RemoveChildFromLinkset", delegate()
            {
                // DebugLog("{0}: RemoveChildFromLinkset: Removing constraint to {1}", LogHeader, child.LocalID);
                // DetailLog("{0},RemoveChildFromLinkset,taint,child={1}", m_linksetRoot.LocalID, child.LocalID);

                PhysicallyUnlinkAChildFromRoot(rootx, childx);
            });

            RecomputeLinksetConstraintVariables();
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
    private void PhysicallyLinkAChildToRoot(BSPrim rootPrim, BSPrim childPrim)
    {
        // Zero motion for children so they don't interpolate
        childPrim.ZeroMotion();

        // Relative position normalized to the root prim
        // Essentually a vector pointing from center of rootPrim to center of childPrim
        OMV.Vector3 childRelativePosition = childPrim.Position - rootPrim.Position;

        // real world coordinate of midpoint between the two objects
        OMV.Vector3 midPoint = rootPrim.Position + (childRelativePosition / 2);

        // create a constraint that allows no freedom of movement between the two objects
        // http://bulletphysics.org/Bullet/phpBB3/viewtopic.php?t=4818
        // DebugLog("{0}: CreateLinkset: Adding a constraint between root prim {1} and child prim {2}", LogHeader, LocalID, childPrim.LocalID);
        DetailLog("{0},PhysicallyLinkAChildToRoot,taint,root={1},child={2},rLoc={3},cLoc={4},midLoc={5}", 
            rootPrim.LocalID, rootPrim.LocalID, childPrim.LocalID, rootPrim.Position, childPrim.Position, midPoint);
        BS6DofConstraint constrain = new BS6DofConstraint(
                        m_physicsScene.World, rootPrim.Body, childPrim.Body,
                        midPoint,
                        true,
                        true
                        );
        /* NOTE: attempt to build constraint with full frame computation, etc.
         *     Using the midpoint is easier since it lets the Bullet code use the transforms
         *     of the objects.
         * Code left here as an example.
        // ==================================================================================
        // relative position normalized to the root prim
        OMV.Quaternion invThisOrientation = OMV.Quaternion.Inverse(rootPrim.Orientation);
        OMV.Vector3 childRelativePosition = (childPrim.Position - rootPrim.Position) * invThisOrientation;

        // relative rotation of the child to the parent
        OMV.Quaternion childRelativeRotation = invThisOrientation * childPrim.Orientation;
        OMV.Quaternion inverseChildRelativeRotation = OMV.Quaternion.Inverse(childRelativeRotation);

        // create a constraint that allows no freedom of movement between the two objects
        // http://bulletphysics.org/Bullet/phpBB3/viewtopic.php?t=4818
        // DebugLog("{0}: CreateLinkset: Adding a constraint between root prim {1} and child prim {2}", LogHeader, LocalID, childPrim.LocalID);
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

        m_physicsScene.Constraints.AddConstraint(constrain);

        // zero linear and angular limits makes the objects unable to move in relation to each other
        constrain.SetLinearLimits(OMV.Vector3.Zero, OMV.Vector3.Zero);
        constrain.SetAngularLimits(OMV.Vector3.Zero, OMV.Vector3.Zero);

        // tweek the constraint to increase stability
        constrain.UseFrameOffset(PhysicsScene.BoolNumeric(PhysicsScene.Params.linkConstraintUseFrameOffset));
        constrain.TranslationalLimitMotor(PhysicsScene.BoolNumeric(PhysicsScene.Params.linkConstraintEnableTransMotor),
                        PhysicsScene.Params.linkConstraintTransMotorMaxVel,
                        PhysicsScene.Params.linkConstraintTransMotorMaxForce);
        constrain.SetCFMAndERP(PhysicsScene.Params.linkConstraintCFM, PhysicsScene.Params.linkConstraintERP);

        RecomputeLinksetConstraintVariables();
    }

    // Remove linkage between myself and a particular child
    // Called at taint time!
    private void PhysicallyUnlinkAChildFromRoot(BSPrim rootPrim, BSPrim childPrim)
    {
        // DebugLog("{0}: PhysicallyUnlinkAChildFromRoot: RemoveConstraint between root prim {1} and child prim {2}", 
        //             LogHeader, rootPrim.LocalID, childPrim.LocalID);
        DetailLog("{0},PhysicallyUnlinkAChildFromRoot,taint,root={1},child={2}", rootPrim.LocalID, rootPrim.LocalID, childPrim.LocalID);

        // Find the constraint for this link and get rid of it from the overall collection and from my list
        m_physicsScene.Constraints.RemoveAndDestroyConstraint(rootPrim.Body, childPrim.Body);

        // Make the child refresh its location
        BulletSimAPI.PushUpdate2(childPrim.Body.Ptr);
    }

    // Remove linkage between myself and any possible children I might have
    // Called at taint time!
    private void PhysicallyUnlinkAllChildrenFromRoot(BSPrim rootPrim)
    {
        // DebugLog("{0}: PhysicallyUnlinkAllChildren:", LogHeader);
        DetailLog("{0},PhysicallyUnlinkAllChildren,taint", rootPrim.LocalID);

        m_physicsScene.Constraints.RemoveAndDestroyConstraint(rootPrim.Body);
    }

    // Invoke the detailed logger and output something if it's enabled.
    private void DebugLog(string msg, params Object[] args)
    {
        if (m_physicsScene.ShouldDebugLog)
            m_physicsScene.Logger.DebugFormat(msg, args);
    }

    // Invoke the detailed logger and output something if it's enabled.
    private void DetailLog(string msg, params Object[] args)
    {
        m_physicsScene.PhysicsLogging.Write(msg, args);
    }

}
}
