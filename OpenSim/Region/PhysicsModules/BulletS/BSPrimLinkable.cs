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
using System.Linq;
using System.Text;

using OpenSim.Framework;

using OMV = OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.BulletS
{
public class BSPrimLinkable : BSPrimDisplaced
{
    // The purpose of this subclass is to add linkset functionality to the prim. This overrides
    //    operations necessary for keeping the linkset created and, additionally, this
    //    calls the linkset implementation for its creation and management.

#pragma warning disable 414
    private static readonly string LogHeader = "[BULLETS PRIMLINKABLE]";
#pragma warning restore 414

    // This adds the overrides for link() and delink() so the prim is linkable.

    public BSLinkset Linkset { get; set; }
    // The index of this child prim.
    public int LinksetChildIndex { get; set; }

    public BSLinkset.LinksetImplementation LinksetType { get; set; }

    public BSPrimLinkable(uint localID, String primName, BSScene parent_scene, OMV.Vector3 pos, OMV.Vector3 size,
                       OMV.Quaternion rotation, PrimitiveBaseShape pbs, bool pisPhysical)
        : base(localID, primName, parent_scene, pos, size, rotation, pbs, pisPhysical)
    {
        // Default linkset implementation for this prim
        LinksetType = (BSLinkset.LinksetImplementation)BSParam.LinksetImplementation;

        Linkset = BSLinkset.Factory(PhysScene, this);

        Linkset.Refresh(this);
    }

    public override void Destroy()
    {
        Linkset = Linkset.RemoveMeFromLinkset(this, false /* inTaintTime */);
        base.Destroy();
    }

    public override void link(OpenSim.Region.PhysicsModules.SharedBase.PhysicsActor obj)
    {
        BSPrimLinkable parent = obj as BSPrimLinkable;
        if (parent != null)
        {
            BSPhysObject parentBefore = Linkset.LinksetRoot;    // DEBUG
            int childrenBefore = Linkset.NumberOfChildren;      // DEBUG

            Linkset = parent.Linkset.AddMeToLinkset(this);

            DetailLog("{0},BSPrimLinkable.link,call,parentBefore={1}, childrenBefore=={2}, parentAfter={3}, childrenAfter={4}",
                LocalID, parentBefore.LocalID, childrenBefore, Linkset.LinksetRoot.LocalID, Linkset.NumberOfChildren);
        }
        return;
    }

    public override void delink()
    {
        // TODO: decide if this parent checking needs to happen at taint time
        // Race condition here: if link() and delink() in same simulation tick, the delink will not happen

        BSPhysObject parentBefore = Linkset.LinksetRoot;        // DEBUG
        int childrenBefore = Linkset.NumberOfChildren;          // DEBUG

        Linkset = Linkset.RemoveMeFromLinkset(this, false /* inTaintTime*/);

        DetailLog("{0},BSPrimLinkable.delink,parentBefore={1},childrenBefore={2},parentAfter={3},childrenAfter={4}, ",
            LocalID, parentBefore.LocalID, childrenBefore, Linkset.LinksetRoot.LocalID, Linkset.NumberOfChildren);
        return;
    }

    // When simulator changes position, this might be moving a child of the linkset.
    public override OMV.Vector3 Position
    {
        get { return base.Position; }
        set
        {
            base.Position = value;
            PhysScene.TaintedObject(LocalID, "BSPrimLinkable.setPosition", delegate()
            {
                Linkset.UpdateProperties(UpdatedProperties.Position, this);
            });
        }
    }

    // When simulator changes orientation, this might be moving a child of the linkset.
    public override OMV.Quaternion Orientation
    {
        get { return base.Orientation; }
        set
        {
            base.Orientation = value;
            PhysScene.TaintedObject(LocalID, "BSPrimLinkable.setOrientation", delegate()
            {
                Linkset.UpdateProperties(UpdatedProperties.Orientation, this);
            });
        }
    }

    public override float TotalMass
    {
        get { return Linkset.LinksetMass; }
    }

    public override OMV.Vector3 CenterOfMass
    {
        get { return Linkset.CenterOfMass; }
    }

    public override OMV.Vector3 GeometricCenter
    {
        get { return Linkset.GeometricCenter; }
    }

    // Refresh the linkset structure and parameters when the prim's physical parameters are changed.
    public override void UpdatePhysicalParameters()
    {
        base.UpdatePhysicalParameters();
        // Recompute any linkset parameters.
        // When going from non-physical to physical, this re-enables the constraints that
        //     had been automatically disabled when the mass was set to zero.
        // For compound based linksets, this enables and disables interactions of the children.
        if (Linkset != null)    // null can happen during initialization
            Linkset.Refresh(this);
    }

    // When the prim is made dynamic or static, the linkset needs to change.
    protected override void MakeDynamic(bool makeStatic)
    {
        base.MakeDynamic(makeStatic);
        if (Linkset != null)    // null can happen during initialization
        {
            if (makeStatic)
                Linkset.MakeStatic(this);
            else
                Linkset.MakeDynamic(this);
        }
    }

    // Body is being taken apart. Remove physical dependencies and schedule a rebuild.
    protected override void RemoveDependencies()
    {
        Linkset.RemoveDependencies(this);
        base.RemoveDependencies();
    }

    // Called after a simulation step for the changes in physical object properties.
    // Do any filtering/modification needed for linksets.
    public override void UpdateProperties(EntityProperties entprop)
    {
        if (Linkset.IsRoot(this) || Linkset.ShouldReportPropertyUpdates(this))
        {
            // Properties are only updated for the roots of a linkset.
            // TODO: this will have to change when linksets are articulated.
            base.UpdateProperties(entprop);
        }
            /*
        else
        {
            // For debugging, report the movement of children
            DetailLog("{0},BSPrim.UpdateProperties,child,pos={1},orient={2},vel={3},accel={4},rotVel={5}",
                    LocalID, entprop.Position, entprop.Rotation, entprop.Velocity,
                    entprop.Acceleration, entprop.RotationalVelocity);
        }
             */
        // The linkset might like to know about changing locations
        Linkset.UpdateProperties(UpdatedProperties.EntPropUpdates, this);
    }

    // Called after a simulation step to post a collision with this object.
    // This returns 'true' if the collision has been queued and the SendCollisions call must
    //     be made at the end of the simulation step.
    public override bool Collide(BSPhysObject collidee, OMV.Vector3 contactPoint, OMV.Vector3 contactNormal, float pentrationDepth)
    {
        bool ret = false;
        // Ask the linkset if it wants to handle the collision
        if (!Linkset.HandleCollide(this, collidee, contactPoint, contactNormal, pentrationDepth))
        {
            // The linkset didn't handle it so pass the collision through normal processing
            ret = base.Collide(collidee, contactPoint, contactNormal, pentrationDepth);
        }
        return ret;
    }

    // A linkset reports any collision on any part of the linkset.
    public long SomeCollisionSimulationStep = 0;
    public override bool HasSomeCollision
    {
        get
        {
            return (SomeCollisionSimulationStep == PhysScene.SimulationStep) || base.IsColliding;
        }
        set
        {
            if (value)
                SomeCollisionSimulationStep = PhysScene.SimulationStep;
            else
                SomeCollisionSimulationStep = 0;

            base.HasSomeCollision = value;
        }
    }

    // Convert the existing linkset of this prim into a new type.
    public bool ConvertLinkset(BSLinkset.LinksetImplementation newType)
    {
        bool ret = false;
        if (LinksetType != newType)
        {
            DetailLog("{0},BSPrimLinkable.ConvertLinkset,oldT={1},newT={2}", LocalID, LinksetType, newType);

            // Set the implementation type first so the call to BSLinkset.Factory gets the new type.
            this.LinksetType = newType;

            BSLinkset oldLinkset = this.Linkset;
            BSLinkset newLinkset = BSLinkset.Factory(PhysScene, this);

            this.Linkset = newLinkset;

            // Pick up any physical dependencies this linkset might have in the physics engine.
            oldLinkset.RemoveDependencies(this);

            // Create a list of the children (mainly because can't interate through a list that's changing)
            List<BSPrimLinkable> children = new List<BSPrimLinkable>();
            oldLinkset.ForEachMember((child) =>
            {
                if (!oldLinkset.IsRoot(child))
                    children.Add(child);
                return false;   // 'false' says to continue to next member
            });

            // Remove the children from the old linkset and add to the new (will be a new instance from the factory)
            foreach (BSPrimLinkable child in children)
            {
                oldLinkset.RemoveMeFromLinkset(child, true /*inTaintTime*/);
            }
            foreach (BSPrimLinkable child in children)
            {
                newLinkset.AddMeToLinkset(child);
                child.Linkset = newLinkset;
            }

            // Force the shape and linkset to get reconstructed
            newLinkset.Refresh(this);
            this.ForceBodyShapeRebuild(true /* inTaintTime */);
        }
        return ret;
    }

    #region Extension
    public override object Extension(string pFunct, params object[] pParams)
    {
        DetailLog("{0} BSPrimLinkable.Extension,op={1},nParam={2}", LocalID, pFunct, pParams.Length);
        object ret = null;
        switch (pFunct)
        {
            // physGetLinksetType();
            // pParams = [ BSPhysObject root, null ]
            case ExtendedPhysics.PhysFunctGetLinksetType:
            {
                ret = (object)LinksetType;
                DetailLog("{0},BSPrimLinkable.Extension.physGetLinksetType,type={1}", LocalID, ret);
                break;
            }
            // physSetLinksetType(type);
            // pParams = [ BSPhysObject root, null, integer type ]
            case ExtendedPhysics.PhysFunctSetLinksetType:
            {
                if (pParams.Length > 2)
                {
                    BSLinkset.LinksetImplementation linksetType = (BSLinkset.LinksetImplementation)pParams[2];
                    if (Linkset.IsRoot(this))
                    {
                        PhysScene.TaintedObject(LocalID, "BSPrim.PhysFunctSetLinksetType", delegate()
                        {
                            // Cause the linkset type to change
                            DetailLog("{0},BSPrimLinkable.Extension.physSetLinksetType, oldType={1},newType={2}",
                                                LocalID, Linkset.LinksetImpl, linksetType);
                            ConvertLinkset(linksetType);
                        });
                    }
                    ret = (object)(int)linksetType;
                }
                break;
            }
            // physChangeLinkType(linknum, typeCode);
            // pParams = [ BSPhysObject root, BSPhysObject child, integer linkType ]
            case ExtendedPhysics.PhysFunctChangeLinkType:
            {
                ret = Linkset.Extension(pFunct, pParams);
                break;
            }
            // physGetLinkType(linknum);
            // pParams = [ BSPhysObject root, BSPhysObject child ]
            case ExtendedPhysics.PhysFunctGetLinkType:
            {
                ret = Linkset.Extension(pFunct, pParams);
                break;
            }
            // physChangeLinkParams(linknum, [code, value, code, value, ...]);
            // pParams = [ BSPhysObject root, BSPhysObject child, object[] [ string op, object opParam, string op, object opParam, ... ] ]
            case ExtendedPhysics.PhysFunctChangeLinkParams:
            {
                ret = Linkset.Extension(pFunct, pParams);
                break;
            }
            default:
                ret = base.Extension(pFunct, pParams);
                break;
        }
        return ret;
    }
    #endregion  // Extension
}
}
