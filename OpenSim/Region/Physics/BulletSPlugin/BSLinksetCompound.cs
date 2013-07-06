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

public sealed class BSLinksetCompound : BSLinkset
{
    private static string LogHeader = "[BULLETSIM LINKSET COMPOUND]";

    public BSLinksetCompound(BSScene scene, BSPrimLinkable parent)
        : base(scene, parent)
    {
    }

    // When physical properties are changed the linkset needs to recalculate
    //   its internal properties.
    public override void Refresh(BSPrimLinkable requestor)
    {
        base.Refresh(requestor);

        // Something changed so do the rebuilding thing
        // ScheduleRebuild();
    }

    // Schedule a refresh to happen after all the other taint processing.
    private void ScheduleRebuild(BSPrimLinkable requestor)
    {
        DetailLog("{0},BSLinksetCompound.ScheduleRebuild,,rebuilding={1},hasChildren={2},actuallyScheduling={3}",
                            requestor.LocalID, Rebuilding, HasAnyChildren, (!Rebuilding && HasAnyChildren));
        // When rebuilding, it is possible to set properties that would normally require a rebuild.
        //    If already rebuilding, don't request another rebuild.
        //    If a linkset with just a root prim (simple non-linked prim) don't bother rebuilding.
        if (!Rebuilding && HasAnyChildren)
        {
            m_physicsScene.PostTaintObject("BSLinksetCompound.ScheduleRebuild", LinksetRoot.LocalID, delegate()
            {
                if (HasAnyChildren)
                    RecomputeLinksetCompound();
            });
        }
    }

    // The object is going dynamic (physical). Do any setup necessary for a dynamic linkset.
    // Only the state of the passed object can be modified. The rest of the linkset
    //     has not yet been fully constructed.
    // Return 'true' if any properties updated on the passed object.
    // Called at taint-time!
    public override bool MakeDynamic(BSPrimLinkable child)
    {
        bool ret = false;
        DetailLog("{0},BSLinksetCompound.MakeDynamic,call,IsRoot={1}", child.LocalID, IsRoot(child));
        if (IsRoot(child))
        {
            // The root is going dynamic. Rebuild the linkset so parts and mass get computed properly.
            ScheduleRebuild(LinksetRoot);
        }
        return ret;
    }

    // The object is going static (non-physical). We do not do anything for static linksets.
    // Return 'true' if any properties updated on the passed object.
    // Called at taint-time!
    public override bool MakeStatic(BSPrimLinkable child)
    {
        bool ret = false;

        DetailLog("{0},BSLinksetCompound.MakeStatic,call,IsRoot={1}", child.LocalID, IsRoot(child));
        child.ClearDisplacement();
        if (IsRoot(child))
        {
            // Schedule a rebuild to verify that the root shape is set to the real shape.
            ScheduleRebuild(LinksetRoot);
        }
        return ret;
    }

    // 'physicalUpdate' is true if these changes came directly from the physics engine. Don't need to rebuild then.
    // Called at taint-time.
    public override void UpdateProperties(UpdatedProperties whichUpdated, BSPrimLinkable updated)
    {
        if (!LinksetRoot.IsPhysicallyActive)
        {
            // No reason to do this physical stuff for static linksets.
            DetailLog("{0},BSLinksetCompound.UpdateProperties,notPhysical", LinksetRoot.LocalID);
            return;
        }

        // The user moving a child around requires the rebuilding of the linkset compound shape
        // One problem is this happens when a border is crossed -- the simulator implementation
        //    stores the position into the group which causes the move of the object
        //    but it also means all the child positions get updated.
        //    What would cause an unnecessary rebuild so we make sure the linkset is in a
        //    region before bothering to do a rebuild.
        if (!IsRoot(updated) && m_physicsScene.TerrainManager.IsWithinKnownTerrain(LinksetRoot.RawPosition))
        {
            // If a child of the linkset is updating only the position or rotation, that can be done
            //    without rebuilding the linkset.
            // If a handle for the child can be fetch, we update the child here. If a rebuild was
            //    scheduled by someone else, the rebuild will just replace this setting.

            bool updatedChild = false;
            // Anything other than updating position or orientation usually means a physical update
            //     and that is caused by us updating the object.
            if ((whichUpdated & ~(UpdatedProperties.Position | UpdatedProperties.Orientation)) == 0)
            {
                // Find the physical instance of the child
                if (LinksetRoot.PhysShape.HasPhysicalShape && m_physicsScene.PE.IsCompound(LinksetRoot.PhysShape.physShapeInfo))
                {
                    // It is possible that the linkset is still under construction and the child is not yet
                    //    inserted into the compound shape. A rebuild of the linkset in a pre-step action will
                    //    build the whole thing with the new position or rotation.
                    // The index must be checked because Bullet references the child array but does no validity
                    //    checking of the child index passed.
                    int numLinksetChildren = m_physicsScene.PE.GetNumberOfCompoundChildren(LinksetRoot.PhysShape.physShapeInfo);
                    if (updated.LinksetChildIndex < numLinksetChildren)
                    {
                        BulletShape linksetChildShape = m_physicsScene.PE.GetChildShapeFromCompoundShapeIndex(LinksetRoot.PhysShape.physShapeInfo, updated.LinksetChildIndex);
                        if (linksetChildShape.HasPhysicalShape)
                        {
                            // Found the child shape within the compound shape
                            m_physicsScene.PE.UpdateChildTransform(LinksetRoot.PhysShape.physShapeInfo, updated.LinksetChildIndex,
                                                                        updated.RawPosition - LinksetRoot.RawPosition,
                                                                        updated.RawOrientation * OMV.Quaternion.Inverse(LinksetRoot.RawOrientation),
                                                                        true /* shouldRecalculateLocalAabb */);
                            updatedChild = true;
                            DetailLog("{0},BSLinksetCompound.UpdateProperties,changeChildPosRot,whichUpdated={1},pos={2},rot={3}",
                                                        updated.LocalID, whichUpdated, updated.RawPosition, updated.RawOrientation);
                        }
                        else    // DEBUG DEBUG
                        {       // DEBUG DEBUG
                            DetailLog("{0},BSLinksetCompound.UpdateProperties,couldNotUpdateChild,noChildShape,shape={1}",
                                                                        updated.LocalID, linksetChildShape);
                        }       // DEBUG DEBUG
                    }
                    else    // DEBUG DEBUG
                    {       // DEBUG DEBUG
                        // the child is not yet in the compound shape. This is non-fatal.
                        DetailLog("{0},BSLinksetCompound.UpdateProperties,couldNotUpdateChild,childNotInCompoundShape,numChildren={1},index={2}",
                                                                    updated.LocalID, numLinksetChildren, updated.LinksetChildIndex);
                    }       // DEBUG DEBUG
                }
                else    // DEBUG DEBUG
                {       // DEBUG DEBUG
                    DetailLog("{0},BSLinksetCompound.UpdateProperties,couldNotUpdateChild,noBodyOrNotCompound", updated.LocalID);
                }       // DEBUG DEBUG

                if (!updatedChild)
                {
                    // If couldn't do the individual child, the linkset needs a rebuild to incorporate the new child info.
                    // Note: there are several ways through this code that will not update the child if
                    //    the linkset is being rebuilt. In this case, scheduling a rebuild is a NOOP since
                    //    there will already be a rebuild scheduled.
                    DetailLog("{0},BSLinksetCompound.UpdateProperties,couldNotUpdateChild.schedulingRebuild,whichUpdated={1}",
                                                                    updated.LocalID, whichUpdated);
                    ScheduleRebuild(updated);
                }
            }
        }
    }

    // Routine called when rebuilding the body of some member of the linkset.
    // If one of the bodies is being changed, the linkset needs rebuilding.
    // For instance, a linkset is built and then a mesh asset is read in and the mesh is recreated.
    // Returns 'true' of something was actually removed and would need restoring
    // Called at taint-time!!
    public override bool RemoveDependencies(BSPrimLinkable child)
    {
        bool ret = false;

        DetailLog("{0},BSLinksetCompound.RemoveBodyDependencies,refreshIfChild,rID={1},rBody={2},isRoot={3}",
                        child.LocalID, LinksetRoot.LocalID, LinksetRoot.PhysBody, IsRoot(child));

        ScheduleRebuild(child);

        return ret;
    }

    // ================================================================

    // Add a new child to the linkset.
    // Called while LinkActivity is locked.
    protected override void AddChildToLinkset(BSPrimLinkable child)
    {
        if (!HasChild(child))
        {
            m_children.Add(child);

            DetailLog("{0},BSLinksetCompound.AddChildToLinkset,call,child={1}", LinksetRoot.LocalID, child.LocalID);

            // Rebuild the compound shape with the new child shape included
            ScheduleRebuild(child);
        }
        return;
    }

    // Remove the specified child from the linkset.
    // Safe to call even if the child is not really in the linkset.
    protected override void RemoveChildFromLinkset(BSPrimLinkable child)
    {
        child.ClearDisplacement();

        if (m_children.Remove(child))
        {
            DetailLog("{0},BSLinksetCompound.RemoveChildFromLinkset,call,rID={1},rBody={2},cID={3},cBody={4}",
                            child.LocalID,
                            LinksetRoot.LocalID, LinksetRoot.PhysBody.AddrString,
                            child.LocalID, child.PhysBody.AddrString);

            // Cause the child's body to be rebuilt and thus restored to normal operation
            child.ForceBodyShapeRebuild(false);

            if (!HasAnyChildren)
            {
                // The linkset is now empty. The root needs rebuilding.
                LinksetRoot.ForceBodyShapeRebuild(false);
            }
            else
            {
                // Rebuild the compound shape with the child removed
                ScheduleRebuild(LinksetRoot);
            }
        }
        return;
    }

    // Called before the simulation step to make sure the compound based linkset
    //    is all initialized.
    // Constraint linksets are rebuilt every time.
    // Note that this works for rebuilding just the root after a linkset is taken apart.
    // Called at taint time!!
    private bool UseBulletSimRootOffsetHack = false;    // Attempt to have Bullet track the coords of root compound shape
    private void RecomputeLinksetCompound()
    {
        try
        {
            Rebuilding = true;

            // No matter what is being done, force the root prim's PhysBody and PhysShape to get set
            //     to what they should be as if the root was not in a linkset.
            // Not that bad since we only get into this routine if there are children in the linkset and
            //     something has been updated/changed.
            // Have to do the rebuild before checking for physical because this might be a linkset
            //     being destructed and going non-physical.
            LinksetRoot.ForceBodyShapeRebuild(true);

            // There is no reason to build all this physical stuff for a non-physical linkset.
            if (!LinksetRoot.IsPhysicallyActive)
            {
                DetailLog("{0},BSLinksetCompound.RecomputeLinksetCompound,notPhysical", LinksetRoot.LocalID);
                return; // Note the 'finally' clause at the botton which will get executed.
            }

            // Get a new compound shape to build the linkset shape in.
            BSShape linksetShape = BSShapeCompound.GetReference(m_physicsScene);

            // Compute a displacement for each component so it is relative to the center-of-mass.
            // Bullet presumes an object's origin (relative <0,0,0>) is its center-of-mass
            OMV.Vector3 centerOfMassW = ComputeLinksetCenterOfMass();

            OMV.Quaternion invRootOrientation = OMV.Quaternion.Normalize(OMV.Quaternion.Inverse(LinksetRoot.RawOrientation));
            OMV.Vector3 origRootPosition = LinksetRoot.RawPosition;

            // 'centerDisplacementV' is the vehicle relative distance from the simulator root position to the center-of-mass
            OMV.Vector3 centerDisplacementV = (centerOfMassW - LinksetRoot.RawPosition) * invRootOrientation;
            if (UseBulletSimRootOffsetHack || !BSParam.LinksetOffsetCenterOfMass)
            {
                // Zero everything if center-of-mass displacement is not being done.
                centerDisplacementV = OMV.Vector3.Zero;
                LinksetRoot.ClearDisplacement();
            }
            else
            {
                // The actual center-of-mass could have been set by the user.
                centerDisplacementV = LinksetRoot.SetEffectiveCenterOfMassDisplacement(centerDisplacementV);
            }

            DetailLog("{0},BSLinksetCompound.RecomputeLinksetCompound,COM,rootPos={1},com={2},comDisp={3}",
                                LinksetRoot.LocalID, origRootPosition, centerOfMassW, centerDisplacementV);

            // Add the shapes of all the components of the linkset
            int memberIndex = 1;
            ForEachMember(delegate(BSPrimLinkable cPrim)
            {
                if (IsRoot(cPrim))
                {
                    // Root shape is always index zero.
                    cPrim.LinksetChildIndex = 0;
                }
                else
                {
                    cPrim.LinksetChildIndex = memberIndex;
                    memberIndex++;
                }

                // Get a reference to the shape of the child for adding of that shape to the linkset compound shape
                BSShape childShape = cPrim.PhysShape.GetReference(m_physicsScene, cPrim);

                // Offset the child shape from the center-of-mass and rotate it to vehicle relative.
                OMV.Vector3 offsetPos = (cPrim.RawPosition - origRootPosition) * invRootOrientation - centerDisplacementV;
                OMV.Quaternion offsetRot = OMV.Quaternion.Normalize(cPrim.RawOrientation) * invRootOrientation;

                // Add the child shape to the compound shape being built
                m_physicsScene.PE.AddChildShapeToCompoundShape(linksetShape.physShapeInfo, childShape.physShapeInfo, offsetPos, offsetRot);
                DetailLog("{0},BSLinksetCompound.RecomputeLinksetCompound,addChild,indx={1},cShape={2},offPos={3},offRot={4}",
                                    LinksetRoot.LocalID, cPrim.LinksetChildIndex, childShape, offsetPos, offsetRot);

                // Since we are borrowing the shape of the child, disable the origional child body
                if (!IsRoot(cPrim))
                {
                    m_physicsScene.PE.AddToCollisionFlags(cPrim.PhysBody, CollisionFlags.CF_NO_CONTACT_RESPONSE);
                    m_physicsScene.PE.ForceActivationState(cPrim.PhysBody, ActivationState.DISABLE_SIMULATION);
                    // We don't want collisions from the old linkset children.
                    m_physicsScene.PE.RemoveFromCollisionFlags(cPrim.PhysBody, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
                    cPrim.PhysBody.collisionType = CollisionType.LinksetChild;
                }

                return false;   // 'false' says to move onto the next child in the list
            });

            // Replace the root shape with the built compound shape.
            // Object removed and added to world to get collision cache rebuilt for new shape.
            LinksetRoot.PhysShape.Dereference(m_physicsScene);
            LinksetRoot.PhysShape = linksetShape;
            m_physicsScene.PE.RemoveObjectFromWorld(m_physicsScene.World, LinksetRoot.PhysBody);
            m_physicsScene.PE.SetCollisionShape(m_physicsScene.World, LinksetRoot.PhysBody, linksetShape.physShapeInfo);
            m_physicsScene.PE.AddObjectToWorld(m_physicsScene.World, LinksetRoot.PhysBody);
            DetailLog("{0},BSLinksetCompound.RecomputeLinksetCompound,addBody,body={1},shape={2}",
                                        LinksetRoot.LocalID, LinksetRoot.PhysBody, linksetShape);

            // With all of the linkset packed into the root prim, it has the mass of everyone.
            LinksetMass = ComputeLinksetMass();
            LinksetRoot.UpdatePhysicalMassProperties(LinksetMass, true);

            if (UseBulletSimRootOffsetHack)
            {
                // Enable the physical position updator to return the position and rotation of the root shape.
                // This enables a feature in the C++ code to return the world coordinates of the first shape in the
                //     compound shape. This aleviates the need to offset the returned physical position by the
                //     center-of-mass offset.
                // TODO: either debug this feature or remove it.
                m_physicsScene.PE.AddToCollisionFlags(LinksetRoot.PhysBody, CollisionFlags.BS_RETURN_ROOT_COMPOUND_SHAPE);
            }
        }
        finally
        {
            Rebuilding = false;
        }

        // See that the Aabb surrounds the new shape
        m_physicsScene.PE.RecalculateCompoundShapeLocalAabb(LinksetRoot.PhysShape.physShapeInfo);
    }
}
}