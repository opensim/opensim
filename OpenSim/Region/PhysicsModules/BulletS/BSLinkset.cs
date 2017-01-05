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

namespace OpenSim.Region.PhysicsModule.BulletS
{

public abstract class BSLinkset
{
    // private static string LogHeader = "[BULLETSIM LINKSET]";

    public enum LinksetImplementation
    {
        Constraint   = 0,   // linkset tied together with constraints
        Compound     = 1,   // linkset tied together as a compound object
        Manual       = 2    // linkset tied together manually (code moves all the pieces)
    }
    // Create the correct type of linkset for this child
    public static BSLinkset Factory(BSScene physScene, BSPrimLinkable parent)
    {
        BSLinkset ret = null;

        switch (parent.LinksetType)
        {
            case LinksetImplementation.Constraint:
                ret = new BSLinksetConstraints(physScene, parent);
                break;
            case LinksetImplementation.Compound:
                ret = new BSLinksetCompound(physScene, parent);
                break;
            case LinksetImplementation.Manual:
                // ret = new BSLinksetManual(physScene, parent);
                break;
            default:
                ret = new BSLinksetCompound(physScene, parent);
                break;
        }
        if (ret == null)
        {
            physScene.Logger.ErrorFormat("[BULLETSIM LINKSET] Factory could not create linkset. Parent name={1}, ID={2}", parent.Name, parent.LocalID);
        }
        return ret;
    }

    public class BSLinkInfo
    {
        public BSPrimLinkable member;
        public BSLinkInfo(BSPrimLinkable pMember)
        {
            member = pMember;
        }
        public virtual void ResetLink() { }
        public virtual void SetLinkParameters(BSConstraint constrain) { }
        // Returns 'true' if physical property updates from the child should be reported to the simulator
        public virtual bool ShouldUpdateChildProperties() { return false; }
    }

    public LinksetImplementation LinksetImpl { get; protected set; }

    public BSPrimLinkable LinksetRoot { get; protected set; }

    protected BSScene m_physicsScene { get; private set; }

    static int m_nextLinksetID = 1;
    public int LinksetID { get; private set; }

    // The children under the root in this linkset.
    // protected HashSet<BSPrimLinkable> m_children;
    protected Dictionary<BSPrimLinkable, BSLinkInfo> m_children;

    // We lock the diddling of linkset classes to prevent any badness.
    // This locks the modification of the instances of this class. Changes
    //    to the physical representation is done via the tainting mechenism.
    protected object m_linksetActivityLock = new Object();

    // We keep the prim's mass in the linkset structure since it could be dependent on other prims
    public float LinksetMass { get; protected set; }

    public virtual bool LinksetIsColliding { get { return false; } }

    public OMV.Vector3 CenterOfMass
    {
        get { return ComputeLinksetCenterOfMass(); }
    }

    public OMV.Vector3 GeometricCenter
    {
        get { return ComputeLinksetGeometricCenter(); }
    }

    protected BSLinkset(BSScene scene, BSPrimLinkable parent)
    {
        // A simple linkset of one (no children)
        LinksetID = m_nextLinksetID++;
        // We create LOTS of linksets.
        if (m_nextLinksetID <= 0)
            m_nextLinksetID = 1;
        m_physicsScene = scene;
        LinksetRoot = parent;
        m_children = new Dictionary<BSPrimLinkable, BSLinkInfo>();
        LinksetMass = parent.RawMass;
        Rebuilding = false;
        RebuildScheduled = false;

        parent.ClearDisplacement();
    }

    // Link to a linkset where the child knows the parent.
    // Parent changing should not happen so do some sanity checking.
    // We return the parent's linkset so the child can track its membership.
    // Called at runtime.
    public BSLinkset AddMeToLinkset(BSPrimLinkable child)
    {
        lock (m_linksetActivityLock)
        {
            // Don't add the root to its own linkset
            if (!IsRoot(child))
                AddChildToLinkset(child);
            LinksetMass = ComputeLinksetMass();
        }
        return this;
    }

    // Remove a child from a linkset.
    // Returns a new linkset for the child which is a linkset of one (just the
    //    orphened child).
    // Called at runtime.
    public BSLinkset RemoveMeFromLinkset(BSPrimLinkable child, bool inTaintTime)
    {
        lock (m_linksetActivityLock)
        {
            if (IsRoot(child))
            {
                // Cannot remove the root from a linkset.
                return this;
            }
            RemoveChildFromLinkset(child, inTaintTime);
            LinksetMass = ComputeLinksetMass();
        }

        // The child is down to a linkset of just itself
        return BSLinkset.Factory(m_physicsScene, child);
    }

    // Return 'true' if the passed object is the root object of this linkset
    public bool IsRoot(BSPrimLinkable requestor)
    {
        return (requestor.LocalID == LinksetRoot.LocalID);
    }

    public int NumberOfChildren { get { return m_children.Count; } }

    // Return 'true' if this linkset has any children (more than the root member)
    public bool HasAnyChildren { get { return (m_children.Count > 0); } }

    // Return 'true' if this child is in this linkset
    public bool HasChild(BSPrimLinkable child)
    {
        bool ret = false;
        lock (m_linksetActivityLock)
        {
            ret = m_children.ContainsKey(child);
        }
        return ret;
    }

    // Perform an action on each member of the linkset including root prim.
    // Depends on the action on whether this should be done at taint time.
    public delegate bool ForEachMemberAction(BSPrimLinkable obj);
    public virtual bool ForEachMember(ForEachMemberAction action)
    {
        bool ret = false;
        lock (m_linksetActivityLock)
        {
            action(LinksetRoot);
            foreach (BSPrimLinkable po in m_children.Keys)
            {
                if (action(po))
                    break;
            }
        }
        return ret;
    }

    public bool TryGetLinkInfo(BSPrimLinkable child, out BSLinkInfo foundInfo)
    {
        bool ret = false;
        BSLinkInfo found = null;
        lock (m_linksetActivityLock)
        {
            ret = m_children.TryGetValue(child, out found);
        }
        foundInfo = found;
        return ret;
    }
    // Perform an action on each member of the linkset including root prim.
    // Depends on the action on whether this should be done at taint time.
    public delegate bool ForEachLinkInfoAction(BSLinkInfo obj);
    public virtual bool ForEachLinkInfo(ForEachLinkInfoAction action)
    {
        bool ret = false;
        lock (m_linksetActivityLock)
        {
            foreach (BSLinkInfo po in m_children.Values)
            {
                if (action(po))
                    break;
            }
        }
        return ret;
    }

    // Check the type of the link and return 'true' if the link is flexible and the
    //    updates from the child should be sent to the simulator so things change.
    public virtual bool ShouldReportPropertyUpdates(BSPrimLinkable child)
    {
        bool ret = false;

        BSLinkInfo linkInfo;
        if (m_children.TryGetValue(child, out linkInfo))
        {
            ret = linkInfo.ShouldUpdateChildProperties();
        }

        return ret;
    }

    // Called after a simulation step to post a collision with this object.
    // Return 'true' if linkset processed the collision. 'false' says the linkset didn't have
    //     anything to add for the collision and it should be passed through normal processing.
    // Default processing for a linkset.
    public virtual bool HandleCollide(BSPhysObject collider, BSPhysObject collidee,
                                OMV.Vector3 contactPoint, OMV.Vector3 contactNormal, float pentrationDepth)
    {
        bool ret = false;

        // prims in the same linkset cannot collide with each other
        BSPrimLinkable convCollidee = collidee as BSPrimLinkable;
        if (convCollidee != null && (LinksetID == convCollidee.Linkset.LinksetID))
        {
            // By returning 'true', we tell the caller the collision has been 'handled' so it won't
            //     do anything about this collision and thus, effectivily, ignoring the collision.
            ret = true;
        }
        else
        {
            // Not a collision between members of the linkset. Must be a real collision.
            // So the linkset root can know if there is a collision anywhere in the linkset.
            LinksetRoot.SomeCollisionSimulationStep = m_physicsScene.SimulationStep;
        }

        return ret;
    }

    // I am the root of a linkset and a new child is being added
    // Called while LinkActivity is locked.
    protected abstract void AddChildToLinkset(BSPrimLinkable child);

    // I am the root of a linkset and one of my children is being removed.
    // Safe to call even if the child is not really in my linkset.
    protected abstract void RemoveChildFromLinkset(BSPrimLinkable child, bool inTaintTime);

    // When physical properties are changed the linkset needs to recalculate
    //   its internal properties.
    // May be called at runtime or taint-time.
    public virtual void Refresh(BSPrimLinkable requestor)
    {
        LinksetMass = ComputeLinksetMass();
    }

    // Flag denoting the linkset is in the process of being rebuilt.
    // Used to know not the schedule a rebuild in the middle of a rebuild.
    // Because of potential update calls that could want to schedule another rebuild.
    protected bool Rebuilding { get; set; }

    // Flag saying a linkset rebuild has been scheduled.
    // This is turned on when the rebuild is requested and turned off when
    //     the rebuild is complete. Used to limit modifications to the
    //     linkset parameters while the linkset is in an intermediate state.
    // Protected by a "lock(m_linsetActivityLock)" on the BSLinkset object
    public bool RebuildScheduled { get; protected set; }

    // The object is going dynamic (physical). Do any setup necessary
    //     for a dynamic linkset.
    // Only the state of the passed object can be modified. The rest of the linkset
    //     has not yet been fully constructed.
    // Return 'true' if any properties updated on the passed object.
    // Called at taint-time!
    public abstract bool MakeDynamic(BSPrimLinkable child);

    public virtual bool AllPartsComplete
    {
        get {
            bool ret = true;
            this.ForEachMember((member) =>
            {
                if ((!member.IsInitialized) || member.IsIncomplete || member.PrimAssetState == BSPhysObject.PrimAssetCondition.Waiting)
                {
                    ret = false;
                    return true;    // exit loop
                }
                return false;       // continue loop
            });
            return ret;
        }
    }

    // The object is going static (non-physical). Do any setup necessary
    //     for a static linkset.
    // Return 'true' if any properties updated on the passed object.
    // Called at taint-time!
    public abstract bool MakeStatic(BSPrimLinkable child);

    // Called when a parameter update comes from the physics engine for any object
    //      of the linkset is received.
    // Passed flag is update came from physics engine (true) or the user (false).
    // Called at taint-time!!
    public abstract void UpdateProperties(UpdatedProperties whichUpdated, BSPrimLinkable physObject);

    // Routine used when rebuilding the body of the root of the linkset
    // Destroy all the constraints have have been made to root.
    // This is called when the root body is changing.
    // Returns 'true' of something was actually removed and would need restoring
    // Called at taint-time!!
    public abstract bool RemoveDependencies(BSPrimLinkable child);

    // ================================================================
    // Some physical setting happen to all members of the linkset
    public virtual void SetPhysicalFriction(float friction)
    {
        ForEachMember((member) =>
            {
                if (member.PhysBody.HasPhysicalBody)
                    m_physicsScene.PE.SetFriction(member.PhysBody, friction);
                return false;   // 'false' says to continue looping
            }
        );
    }
    public virtual void SetPhysicalRestitution(float restitution)
    {
        ForEachMember((member) =>
            {
                if (member.PhysBody.HasPhysicalBody)
                    m_physicsScene.PE.SetRestitution(member.PhysBody, restitution);
                return false;   // 'false' says to continue looping
            }
        );
    }
    public virtual void SetPhysicalGravity(OMV.Vector3 gravity)
    {
        ForEachMember((member) =>
            {
                if (member.PhysBody.HasPhysicalBody)
                    m_physicsScene.PE.SetGravity(member.PhysBody, gravity);
                return false;   // 'false' says to continue looping
            }
        );
    }
    public virtual void ComputeAndSetLocalInertia(OMV.Vector3 inertiaFactor, float linksetMass)
    {
        ForEachMember((member) =>
            {
                if (member.PhysBody.HasPhysicalBody)
                {
                    OMV.Vector3 inertia = m_physicsScene.PE.CalculateLocalInertia(member.PhysShape.physShapeInfo, linksetMass);
                    member.Inertia = inertia * inertiaFactor;
                    m_physicsScene.PE.SetMassProps(member.PhysBody, linksetMass, member.Inertia);
                    m_physicsScene.PE.UpdateInertiaTensor(member.PhysBody);
                    DetailLog("{0},BSLinkset.ComputeAndSetLocalInertia,m.mass={1}, inertia={2}", member.LocalID, linksetMass, member.Inertia);

                }
                return false;   // 'false' says to continue looping
            }
        );
    }
    public virtual void SetPhysicalCollisionFlags(CollisionFlags collFlags)
    {
        ForEachMember((member) =>
            {
                if (member.PhysBody.HasPhysicalBody)
                    m_physicsScene.PE.SetCollisionFlags(member.PhysBody, collFlags);
                return false;   // 'false' says to continue looping
            }
        );
    }
    public virtual void AddToPhysicalCollisionFlags(CollisionFlags collFlags)
    {
        ForEachMember((member) =>
            {
                if (member.PhysBody.HasPhysicalBody)
                    m_physicsScene.PE.AddToCollisionFlags(member.PhysBody, collFlags);
                return false;   // 'false' says to continue looping
            }
        );
    }
    public virtual void RemoveFromPhysicalCollisionFlags(CollisionFlags collFlags)
    {
        ForEachMember((member) =>
            {
                if (member.PhysBody.HasPhysicalBody)
                    m_physicsScene.PE.RemoveFromCollisionFlags(member.PhysBody, collFlags);
                return false;   // 'false' says to continue looping
            }
        );
    }
    // ================================================================
    protected virtual float ComputeLinksetMass()
    {
        float mass = LinksetRoot.RawMass;
        if (HasAnyChildren)
        {
            lock (m_linksetActivityLock)
            {
                foreach (BSPrimLinkable bp in m_children.Keys)
                {
                    mass += bp.RawMass;
                }
            }
        }
        return mass;
    }

    // Computes linkset's center of mass in world coordinates.
    protected virtual OMV.Vector3 ComputeLinksetCenterOfMass()
    {
        OMV.Vector3 com;
        lock (m_linksetActivityLock)
        {
            com = LinksetRoot.Position * LinksetRoot.RawMass;
            float totalMass = LinksetRoot.RawMass;

            foreach (BSPrimLinkable bp in m_children.Keys)
            {
                com += bp.Position * bp.RawMass;
                totalMass += bp.RawMass;
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

            foreach (BSPrimLinkable bp in m_children.Keys)
            {
                com += bp.Position;
            }
            com /= (m_children.Count + 1);
        }

        return com;
    }

    #region Extension
    public virtual object Extension(string pFunct, params object[] pParams)
    {
        return null;
    }
    #endregion // Extension

    // Invoke the detailed logger and output something if it's enabled.
    protected void DetailLog(string msg, params Object[] args)
    {
        if (m_physicsScene.PhysicsLogging.Enabled)
            m_physicsScene.DetailLog(msg, args);
    }
}
}
