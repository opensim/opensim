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
    // 'Position' and 'Orientation' is what the simulator thinks the positions of the prim is.
    // Because Bullet needs the zero coordinate to be the center of mass of the linkset,
    //     sometimes it is necessary to displace the position the physics engine thinks
    //     the position is. PositionDisplacement must be added and removed from the
    //     position as the simulator position is stored and fetched from the physics
    //     engine. Similar to OrientationDisplacement.
    public virtual OMV.Vector3 PositionDisplacement { get; set; }
    public virtual OMV.Quaternion OrientationDisplacement { get; set; }
    public virtual OMV.Vector3 CenterOfMassLocation { get; set; }
    public virtual OMV.Vector3 GeometricCenterLocation { get; set; }

    public BSPrimDisplaced(uint localID, String primName, BSScene parent_scene, OMV.Vector3 pos, OMV.Vector3 size,
                       OMV.Quaternion rotation, PrimitiveBaseShape pbs, bool pisPhysical)
        : base(localID, primName, parent_scene, pos, size, rotation, pbs, pisPhysical)
    {
        CenterOfMassLocation = RawPosition;
        GeometricCenterLocation = RawPosition;
    }

    public override Vector3 ForcePosition
    {
        get
        {
            return base.ForcePosition;
        }
        set
        {
            base.ForcePosition = value;
            CenterOfMassLocation = RawPosition;
            GeometricCenterLocation = RawPosition;
        }
    }

    public override Quaternion ForceOrientation
    {
        get
        {
            return base.ForceOrientation;
        }
        set
        {
            base.ForceOrientation = value;
        }
    }

    // Is this used?
    public override OMV.Vector3 CenterOfMass
    {
        get { return CenterOfMassLocation; }
    }

    // Is this used?
    public override OMV.Vector3 GeometricCenter
    {
        get { return GeometricCenterLocation; }
    }


    public override void UpdateProperties(EntityProperties entprop)
    {
        // Undo any center-of-mass displacement that might have been done.
        if (PositionDisplacement != OMV.Vector3.Zero)
        {
            // Correct for any rotation around the center-of-mass
            // TODO!!!
            entprop.Position -= PositionDisplacement;
        }

        base.UpdateProperties(entprop);
    }
}
}
