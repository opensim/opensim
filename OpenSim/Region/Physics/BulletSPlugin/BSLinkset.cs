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
    public BSPrim Root { get { return m_linksetRoot; } }

    private BSScene m_scene;
    public BSScene Scene { get { return m_scene; } }

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
        m_scene = scene;
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
        return new BSLinkset(Scene, child);
    }

    /* DEPRECATED: this is really bad in that it trys to unlink other prims.
    // An existing linkset had one of its members rebuilt or something.
    // Go through the linkset and rebuild the pointers to the bodies of the linkset members.
    public BSLinkset RefreshLinkset(BSPrim requestor)
    {
        BSLinkset ret = requestor.Linkset;

        lock (m_linksetActivityLock)
        {
            // The body pointer is refetched in case anything has moved.
            System.IntPtr aPtr = BulletSimAPI.GetBodyHandle2(m_scene.World.Ptr, m_linksetRoot.LocalID);
            if (aPtr == System.IntPtr.Zero)
            {
                // That's odd. We can't find the root of the linkset.
                // The linkset is somehow dead.  The requestor is now a member of a linkset of one.
                DetailLog("{0},RefreshLinkset.RemoveRoot,child={1}", m_linksetRoot.LocalID, m_linksetRoot.LocalID);
                ret = RemoveMeFromLinkset(m_linksetRoot);
            }
            else
            {
                // Reconstruct the pointer to the body of the linkset root.
                DetailLog("{0},RefreshLinkset.RebuildRoot,rootID={1},ptr={2}", m_linksetRoot.LocalID, m_linksetRoot.LocalID, aPtr);
                m_linksetRoot.Body = new BulletBody(m_linksetRoot.LocalID, aPtr);

                List<BSPrim> toRemove = new List<BSPrim>();
                foreach (BSPrim bsp in m_children)
                {
                    aPtr = BulletSimAPI.GetBodyHandle2(m_scene.World.Ptr, bsp.LocalID);
                    if (aPtr == System.IntPtr.Zero)
                    {
                        toRemove.Add(bsp);
                    }
                    else
                    {
                        // Reconstruct the pointer to the body of the linkset root.
                        DetailLog("{0},RefreshLinkset.RebuildChild,rootID={1},ptr={2}", bsp.LocalID, m_linksetRoot.LocalID, aPtr);
                        bsp.Body = new BulletBody(bsp.LocalID, aPtr);
                    }
                }
                foreach (BSPrim bsp in toRemove)
                {
                    RemoveChildFromOtherLinkset(bsp);
                }
            }
        }

        return ret;
    }
     */


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
        foreach (BSPrim bp in m_children)
        {
            if (child.LocalID == bp.LocalID)
            {
                ret = true;
                break;
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

        foreach (BSPrim bp in m_children)
        {
            com += bp.Position * bp.MassRaw;
            totalMass += bp.MassRaw;
        }
        if (totalMass != 0f)
            com /= totalMass;

        return com;
    }

    private OMV.Vector3 ComputeLinksetGeometricCenter()
    {
        OMV.Vector3 com = m_linksetRoot.Position;

        foreach (BSPrim bp in m_children)
        {
            com += bp.Position * bp.MassRaw;
        }
        com /= (m_children.Count + 1);

        return com;
    }

    // I am the root of a linkset and a new child is being added
    // Called while LinkActivity is locked.
    public void AddChildToLinkset(BSPrim child)
    {
        if (!HasChild(child))
        {
            m_children.Add(child);

            BSPrim root = Root; // capture the root as of now
            m_scene.TaintedObject("AddChildToLinkset", delegate()
            {
                DebugLog("{0}: AddChildToLinkset: adding child {1} to {2}", LogHeader, child.LocalID, m_linksetRoot.LocalID);
                DetailLog("{0},AddChildToLinkset,taint,child={1}", m_linksetRoot.LocalID, child.LocalID);
                PhysicallyLinkAChildToRoot(root, child);     // build the physical binding between me and the child
            });
        }
        return;
    }

    // Forcefully removing a child from a linkset.
    // This is not being called by the child so we have to make sure the child doesn't think
    //    it's still connected to the linkset.
    // Normal OpenSimulator operation will never do this because other SceneObjectPart information
    //    has to be updated also (like pointer to prim's parent).
    public void RemoveChildFromOtherLinkset(BSPrim pchild)
    {
        pchild.Linkset = new BSLinkset(m_scene, pchild);
        RemoveChildFromLinkset(pchild);
    }

    // I am the root of a linkset and one of my children is being removed.
    // Safe to call even if the child is not really in my linkset.
    public void RemoveChildFromLinkset(BSPrim child)
    {
        if (m_children.Remove(child))
        {
            BSPrim root = Root; // capture the root as of now
            m_scene.TaintedObject("RemoveChildFromLinkset", delegate()
            {
                DebugLog("{0}: RemoveChildFromLinkset: Removing constraint to {1}", LogHeader, child.LocalID);
                DetailLog("{0},RemoveChildFromLinkset,taint,child={1}", m_linksetRoot.LocalID, child.LocalID);

                PhysicallyUnlinkAChildFromRoot(root, child);
            });
        }
        else
        {
            // This will happen if we remove the root of the linkset first. Non-fatal occurance.
            // m_scene.Logger.ErrorFormat("{0}: Asked to remove child from linkset that was not in linkset", LogHeader);
        }
        return;
    }

    // Create a constraint between me (root of linkset) and the passed prim (the child).
    // Called at taint time!
    private void PhysicallyLinkAChildToRoot(BSPrim rootPrim, BSPrim childPrim)
    {
        // Zero motion for children so they don't interpolate
        childPrim.ZeroMotion();

        // relative position normalized to the root prim
        OMV.Quaternion invThisOrientation = OMV.Quaternion.Inverse(rootPrim.Orientation);
        OMV.Vector3 childRelativePosition = (childPrim.Position - rootPrim.Position) * invThisOrientation;

        // relative rotation of the child to the parent
        OMV.Quaternion childRelativeRotation = invThisOrientation * childPrim.Orientation;

        // create a constraint that allows no freedom of movement between the two objects
        // http://bulletphysics.org/Bullet/phpBB3/viewtopic.php?t=4818
        // DebugLog("{0}: CreateLinkset: Adding a constraint between root prim {1} and child prim {2}", LogHeader, LocalID, childPrim.LocalID);
        DetailLog("{0},PhysicallyLinkAChildToRoot,taint,root={1},child={2}", rootPrim.LocalID, rootPrim.LocalID, childPrim.LocalID);
        BS6DofConstraint constrain = new BS6DofConstraint(
                        m_scene.World, rootPrim.Body, childPrim.Body,
                        childRelativePosition,
                        childRelativeRotation,
                        OMV.Vector3.Zero,
                        -childRelativeRotation
                        );
        m_scene.Constraints.AddConstraint(constrain);

        // zero linear and angular limits makes the objects unable to move in relation to each other
        constrain.SetLinearLimits(OMV.Vector3.Zero, OMV.Vector3.Zero);
        constrain.SetAngularLimits(OMV.Vector3.Zero, OMV.Vector3.Zero);

        // tweek the constraint to increase stability
        constrain.UseFrameOffset(m_scene.BoolNumeric(m_scene.Params.linkConstraintUseFrameOffset));
        constrain.TranslationalLimitMotor(m_scene.BoolNumeric(m_scene.Params.linkConstraintEnableTransMotor),
                        m_scene.Params.linkConstraintTransMotorMaxVel,
                        m_scene.Params.linkConstraintTransMotorMaxForce);
        constrain.SetCFMAndERP(m_scene.Params.linkConstraintCFM, m_scene.Params.linkConstraintERP);

    }

    // Remove linkage between myself and a particular child
    // Called at taint time!
    private void PhysicallyUnlinkAChildFromRoot(BSPrim rootPrim, BSPrim childPrim)
    {
        // DebugLog("{0}: PhysicallyUnlinkAChildFromRoot: RemoveConstraint between root prim {1} and child prim {2}", 
        //             LogHeader, rootPrim.LocalID, childPrim.LocalID);
        DetailLog("{0},PhysicallyUnlinkAChildFromRoot,taint,root={1},child={2}", rootPrim.LocalID, rootPrim.LocalID, childPrim.LocalID);

        m_scene.Constraints.RemoveAndDestroyConstraint(rootPrim.Body, childPrim.Body);
        // Make the child refresh its location
        BulletSimAPI.PushUpdate2(childPrim.Body.Ptr);
    }

    // Remove linkage between myself and any possible children I might have
    // Called at taint time!
    private void PhysicallyUnlinkAllChildrenFromRoot(BSPrim rootPrim)
    {
        // DebugLog("{0}: PhysicallyUnlinkAllChildren:", LogHeader);
        DetailLog("{0},PhysicallyUnlinkAllChildren,taint", rootPrim.LocalID);

        m_scene.Constraints.RemoveAndDestroyConstraint(rootPrim.Body);
    }

    // Invoke the detailed logger and output something if it's enabled.
    private void DebugLog(string msg, params Object[] args)
    {
        if (m_scene.ShouldDebugLog)
            m_scene.Logger.DebugFormat(msg, args);
    }

    // Invoke the detailed logger and output something if it's enabled.
    private void DetailLog(string msg, params Object[] args)
    {
        m_scene.PhysicsLogging.Write(msg, args);
    }

}
}
