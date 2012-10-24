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
public abstract class BSLinkset
{
    // private static string LogHeader = "[BULLETSIM LINKSET]";

    // Create the correct type of linkset for this child
    public static BSLinkset Factory(BSScene physScene, BSPhysObject parent)
    {
        BSLinkset ret = null;
        /*
        if (parent.IsPhysical)
            ret = new BSLinksetConstraints(physScene, parent);
        else
            ret = new BSLinksetManual(physScene, parent);
         */

        // at the moment, there is only one
        ret = new BSLinksetConstraints(physScene, parent);

        return ret;
    }

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
    protected HashSet<BSPhysObject> m_children;
    protected HashSet<BSPhysObject> m_taintChildren;

    // We lock the diddling of linkset classes to prevent any badness.
    // This locks the modification of the instances of this class. Changes
    //    to the physical representation is done via the tainting mechenism.
    protected object m_linksetActivityLock = new Object();

    // We keep the prim's mass in the linkset structure since it could be dependent on other prims
    protected float m_mass;
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

    protected void Initialize(BSScene scene, BSPhysObject parent)
    {
        // A simple linkset of one (no children)
        LinksetID = m_nextLinksetID++;
        // We create LOTS of linksets.
        if (m_nextLinksetID <= 0)
            m_nextLinksetID = 1;
        PhysicsScene = scene;
        LinksetRoot = parent;
        m_children = new HashSet<BSPhysObject>();
        m_taintChildren = new HashSet<BSPhysObject>();
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
        return BSLinkset.Factory(PhysicsScene, child);
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
            if (m_children.Contains(child))
                    ret = true;
            /*
            foreach (BSPhysObject bp in m_children)
            {
                if (child.LocalID == bp.LocalID)
                {
                    ret = true;
                    break;
                }
            }
             */
        }
        return ret;
    }

    // When physical properties are changed the linkset needs to recalculate
    //   its internal properties.
    // May be called at runtime or taint-time (just pass the appropriate flag).
    public abstract void Refresh(BSPhysObject requestor, bool inTaintTime);

    // The object is going dynamic (physical). Do any setup necessary
    //     for a dynamic linkset.
    // Only the state of the passed object can be modified. The rest of the linkset
    //     has not yet been fully constructed.
    // Return 'true' if any properties updated on the passed object.
    // Called at taint-time!
    public abstract bool MakeDynamic(BSPhysObject child);

    // The object is going static (non-physical). Do any setup necessary
    //     for a static linkset.
    // Return 'true' if any properties updated on the passed object.
    // Called at taint-time!
    public abstract bool MakeStatic(BSPhysObject child);

    // Called when a parameter update comes from the physics engine for any object
    //      of the linkset is received.
    // Called at taint-time!!
    public abstract void UpdateProperties(BSPhysObject physObject);

    // Routine used when rebuilding the body of the root of the linkset
    // Destroy all the constraints have have been made to root.
    // This is called when the root body is changing.
    // Returns 'true' of something was actually removed and would need restoring
    // Called at taint-time!!
    public abstract bool RemoveBodyDependencies(BSPrim child);

    // Companion to RemoveBodyDependencies(). If RemoveBodyDependencies() returns 'true',
    //     this routine will restore the removed constraints.
    // Called at taint-time!!
    public abstract void RestoreBodyDependencies(BSPrim child);

    // ================================================================
    // Below this point is internal magic

    protected virtual float ComputeLinksetMass()
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

    protected virtual OMV.Vector3 ComputeLinksetCenterOfMass()
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

    protected virtual OMV.Vector3 ComputeLinksetGeometricCenter()
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
    protected abstract void AddChildToLinkset(BSPhysObject child);

    // Forcefully removing a child from a linkset.
    // This is not being called by the child so we have to make sure the child doesn't think
    //    it's still connected to the linkset.
    // Normal OpenSimulator operation will never do this because other SceneObjectPart information
    //    also has to be updated (like pointer to prim's parent).
    protected abstract void RemoveChildFromOtherLinkset(BSPhysObject pchild);

    // I am the root of a linkset and one of my children is being removed.
    // Safe to call even if the child is not really in my linkset.
    protected abstract void RemoveChildFromLinkset(BSPhysObject child);

    // Invoke the detailed logger and output something if it's enabled.
    protected void DetailLog(string msg, params Object[] args)
    {
        if (PhysicsScene.PhysicsLogging.Enabled)
            PhysicsScene.DetailLog(msg, args);
    }

}
}
