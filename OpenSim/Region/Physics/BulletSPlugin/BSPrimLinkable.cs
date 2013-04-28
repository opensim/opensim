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

namespace OpenSim.Region.Physics.BulletSPlugin
{
public class BSPrimLinkable : BSPrimDisplaced
{
    public BSLinkset Linkset { get; set; }
    // The index of this child prim.
    public int LinksetChildIndex { get; set; }

    public BSLinksetInfo LinksetInfo { get; set; }

    public BSPrimLinkable(uint localID, String primName, BSScene parent_scene, OMV.Vector3 pos, OMV.Vector3 size,
                       OMV.Quaternion rotation, PrimitiveBaseShape pbs, bool pisPhysical)
        : base(localID, primName, parent_scene, pos, size, rotation, pbs, pisPhysical)
    {
        Linkset = BSLinkset.Factory(PhysicsScene, this);

        PhysicsScene.TaintedObject("BSPrimLinksetCompound.Refresh", delegate()
        {
            Linkset.Refresh(this);
        });
    }

    public override void Destroy()
    {
        Linkset = Linkset.RemoveMeFromLinkset(this);
        base.Destroy();
    }

    public override BSPhysicsShapeType PreferredPhysicalShape
        { get { return Linkset.PreferredPhysicalShape(this); } }

    public override void link(Manager.PhysicsActor obj)
    {
        BSPrimLinkable parent = obj as BSPrimLinkable;
        if (parent != null)
        {
            BSPhysObject parentBefore = Linkset.LinksetRoot;
            int childrenBefore = Linkset.NumberOfChildren;

            Linkset = parent.Linkset.AddMeToLinkset(this);

            DetailLog("{0},BSPrimLinkset.link,call,parentBefore={1}, childrenBefore=={2}, parentAfter={3}, childrenAfter={4}",
                LocalID, parentBefore.LocalID, childrenBefore, Linkset.LinksetRoot.LocalID, Linkset.NumberOfChildren);
        }
        return;
    }

    public override void delink()
    {
        // TODO: decide if this parent checking needs to happen at taint time
        // Race condition here: if link() and delink() in same simulation tick, the delink will not happen

        BSPhysObject parentBefore = Linkset.LinksetRoot;
        int childrenBefore = Linkset.NumberOfChildren;

        Linkset = Linkset.RemoveMeFromLinkset(this);

        DetailLog("{0},BSPrimLinkset.delink,parentBefore={1},childrenBefore={2},parentAfter={3},childrenAfter={4}, ",
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
            PhysicsScene.TaintedObject("BSPrimLinkset.setPosition", delegate()
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
            PhysicsScene.TaintedObject("BSPrimLinkset.setOrientation", delegate()
            {
                Linkset.UpdateProperties(UpdatedProperties.Orientation, this);
            });
        }
    }

    public override float TotalMass
    {
        get { return Linkset.LinksetMass; }
    }

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

    protected override void MakeDynamic(bool makeStatic)
    {
        base.MakeDynamic(makeStatic);
        if (makeStatic)
            Linkset.MakeStatic(this);
        else
            Linkset.MakeDynamic(this);
    }

    // Body is being taken apart. Remove physical dependencies and schedule a rebuild.
    protected override void RemoveBodyDependencies()
    {
        Linkset.RemoveBodyDependencies(this);
        base.RemoveBodyDependencies();
    }

    public override void UpdateProperties(EntityProperties entprop)
    {
        if (Linkset.IsRoot(this))
        {
            // Properties are only updated for the roots of a linkset.
            // TODO: this will have to change when linksets are articulated.
            base.UpdateProperties(entprop);
        }
        // The linkset might like to know about changing locations
        Linkset.UpdateProperties(UpdatedProperties.EntPropUpdates, this);
    }

    public override bool Collide(uint collidingWith, BSPhysObject collidee,
                    OMV.Vector3 contactPoint, OMV.Vector3 contactNormal, float pentrationDepth)
    {
        // prims in the same linkset cannot collide with each other
        BSPrimLinkable convCollidee = collidee as BSPrimLinkable;
        if (convCollidee != null && (this.Linkset.LinksetID == convCollidee.Linkset.LinksetID))
        {
            return false;
        }
        return base.Collide(collidingWith, collidee, contactPoint, contactNormal, pentrationDepth);
    }
}
}
