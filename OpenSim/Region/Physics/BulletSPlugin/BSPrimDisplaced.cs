/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
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
 *
 * The quotations from http://wiki.secondlife.com/wiki/Linden_Vehicle_Tutorial
 * are Copyright (c) 2009 Linden Research, Inc and are used under their license
 * of Creative Commons Attribution-Share Alike 3.0
 * (http://creativecommons.org/licenses/by-sa/3.0/).
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

using OMV = OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin
{
public class BSPrimDisplaced : BSPrim
{
    // The purpose of this module is to do any mapping between what the simulator thinks
    //    the prim position and orientation is and what the physical position/orientation.
    //    This difference happens because Bullet assumes the center-of-mass is the <0,0,0>
    //    of the prim/linkset. The simulator tracks the location of the prim/linkset by
    //    the location of the root prim. So, if center-of-mass is anywhere but the origin
    //    of the root prim, the physical origin is displaced from the simulator origin.
    //
    // This routine works by capturing the Force* setting of position/orientation/... and
    //    adjusting the simulator values (being set) into the physical values.
    //    The conversion is also done in the opposite direction (physical origin -> simulator origin).
    //
    // The updateParameter call is also captured and the values from the physics engine
    //    are converted into simulator origin values before being passed to the base
    //    class.

    public virtual OMV.Vector3 PositionDisplacement { get; set; }
    public virtual OMV.Quaternion OrientationDisplacement { get; set; }

    public BSPrimDisplaced(uint localID, String primName, BSScene parent_scene, OMV.Vector3 pos, OMV.Vector3 size,
                       OMV.Quaternion rotation, PrimitiveBaseShape pbs, bool pisPhysical)
        : base(localID, primName, parent_scene, pos, size, rotation, pbs, pisPhysical)
    {
        ClearDisplacement();
    }

    public void ClearDisplacement()
    {
        PositionDisplacement = OMV.Vector3.Zero;
        OrientationDisplacement = OMV.Quaternion.Identity;
    }

    // Set this sets and computes the displacement from the passed prim to the center-of-mass.
    // A user set value for center-of-mass overrides whatever might be passed in here.
    // The displacement is in local coordinates (relative to root prim in linkset oriented coordinates).
    public virtual void SetEffectiveCenterOfMassW(Vector3 centerOfMassDisplacement)
    {
        Vector3 comDisp;
        if (UserSetCenterOfMass.HasValue)
            comDisp = (OMV.Vector3)UserSetCenterOfMass;
        else
            comDisp = centerOfMassDisplacement;

        if (comDisp == Vector3.Zero)
        {
            // If there is no diplacement. Things get reset.
            PositionDisplacement = OMV.Vector3.Zero;
            OrientationDisplacement = OMV.Quaternion.Identity;
        }
        else
        {
            // Remember the displacement from root as well as the origional rotation of the
            //    new center-of-mass.
            PositionDisplacement = comDisp;
            OrientationDisplacement = OMV.Quaternion.Identity;
        }
    }

    public override Vector3 ForcePosition
    {
        get { return base.ForcePosition; }
        set
        {
            if (PositionDisplacement != OMV.Vector3.Zero)
                base.ForcePosition = value - (PositionDisplacement * RawOrientation);
            else
                base.ForcePosition = value;
        }
    }

    public override Quaternion ForceOrientation
    {
        get { return base.ForceOrientation; }
        set
        {
            base.ForceOrientation = value;
        }
    }

    // TODO: decide if this is the right place for these variables.
    //     Somehow incorporate the optional settability by the user.
    // Is this used?
    public override OMV.Vector3 CenterOfMass
    {
        get { return RawPosition; }
    }

    // Is this used?
    public override OMV.Vector3 GeometricCenter
    {
        get { return RawPosition; }
    }

    public override void UpdateProperties(EntityProperties entprop)
    {
        // Undo any center-of-mass displacement that might have been done.
        if (PositionDisplacement != OMV.Vector3.Zero || OrientationDisplacement != OMV.Quaternion.Identity)
        {
            // Correct for any rotation around the center-of-mass
            // TODO!!!
            entprop.Position = entprop.Position + (PositionDisplacement * entprop.Rotation);
            // entprop.Rotation = something;
        }

        base.UpdateProperties(entprop);
    }
}
}
