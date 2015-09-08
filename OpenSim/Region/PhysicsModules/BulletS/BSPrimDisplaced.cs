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
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.PhysicsModules.SharedBase;

using OMV = OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.BulletS
{
public class BSPrimDisplaced : BSPrim
{
    // The purpose of this subclass is to do any mapping between what the simulator thinks
    //    the prim position and orientation is and what the physical position/orientation.
    //    This difference happens because Bullet assumes the center-of-mass is the <0,0,0>
    //    of the prim/linkset. The simulator, on the other hand, tracks the location of
    //    the prim/linkset by the location of the root prim. So, if center-of-mass is anywhere
    //    but the origin of the root prim, the physical origin is displaced from the simulator origin.
    //
    // This routine works by capturing ForcePosition and
    //    adjusting the simulator values (being set) into the physical values.
    //    The conversion is also done in the opposite direction (physical origin -> simulator origin).
    //
    // The updateParameter call is also captured and the values from the physics engine
    //    are converted into simulator origin values before being passed to the base
    //    class.

    // PositionDisplacement is the vehicle relative distance from the root prim position to the center-of-mass.
    public virtual OMV.Vector3 PositionDisplacement { get; set; }

    public BSPrimDisplaced(uint localID, String primName, BSScene parent_scene, OMV.Vector3 pos, OMV.Vector3 size,
                       OMV.Quaternion rotation, PrimitiveBaseShape pbs, bool pisPhysical)
        : base(localID, primName, parent_scene, pos, size, rotation, pbs, pisPhysical)
    {
        ClearDisplacement();
    }

    // Clears any center-of-mass displacement introduced by linksets, etc.
    // Does not clear the displacement set by the user.
    public void ClearDisplacement()
    {
        if (UserSetCenterOfMassDisplacement.HasValue)
            PositionDisplacement = (OMV.Vector3)UserSetCenterOfMassDisplacement;
        else
            PositionDisplacement = OMV.Vector3.Zero;
    }

    // Set this sets and computes the displacement from the passed prim to the center-of-mass.
    // A user set value for center-of-mass overrides whatever might be passed in here.
    // The displacement is in local coordinates (relative to root prim in linkset oriented coordinates).
    // Returns the relative offset from the root position to the center-of-mass.
    // Called at taint time.
    public virtual Vector3 SetEffectiveCenterOfMassDisplacement(Vector3 centerOfMassDisplacement)
    {
        PhysScene.AssertInTaintTime("BSPrimDisplaced.SetEffectiveCenterOfMassDisplacement");
        Vector3 comDisp;
        if (UserSetCenterOfMassDisplacement.HasValue)
            comDisp = (OMV.Vector3)UserSetCenterOfMassDisplacement;
        else
            comDisp = centerOfMassDisplacement;

        // Eliminate any jitter caused be very slight differences in masses and positions
        if (comDisp.ApproxEquals(Vector3.Zero, 0.01f) )
            comDisp = Vector3.Zero;

        DetailLog("{0},BSPrimDisplaced.SetEffectiveCenterOfMassDisplacement,userSet={1},comDisp={2}",
                                    LocalID, UserSetCenterOfMassDisplacement.HasValue, comDisp);
        if ( !comDisp.ApproxEquals(PositionDisplacement, 0.01f) )
        {
            // Displacement setting is changing.
            // The relationship between the physical object and simulated object must be aligned.
            PositionDisplacement = comDisp;
            this.ForcePosition = RawPosition;
        }

        return PositionDisplacement;
    }

    // 'ForcePosition' is the one way to set the physical position of the body in the physics engine.
    // Displace the simulator idea of position (center of root prim) to the physical position.
    public override Vector3 ForcePosition
    {
        get {
            OMV.Vector3 physPosition = PhysScene.PE.GetPosition(PhysBody);
            if (PositionDisplacement != OMV.Vector3.Zero)
            {
                // If there is some displacement, return the physical position (center-of-mass)
                //     location minus the displacement to give the center of the root prim.
                OMV.Vector3 displacement = PositionDisplacement * ForceOrientation;
                DetailLog("{0},BSPrimDisplaced.ForcePosition,get,physPos={1},disp={2},simPos={3}",
                                LocalID, physPosition, displacement, physPosition - displacement);
                physPosition -= displacement;
            }
            RawPosition = physPosition;
            return physPosition;
        }
        set
        {
            if (PositionDisplacement != OMV.Vector3.Zero)
            {
                // This value is the simulator's idea of where the prim is: the center of the root prim
                RawPosition = value;

                // Move the passed root prim postion to the center-of-mass position and set in the physics engine.
                OMV.Vector3 displacement = PositionDisplacement * RawOrientation;
                OMV.Vector3 displacedPos = RawPosition + displacement;
                DetailLog("{0},BSPrimDisplaced.ForcePosition,set,simPos={1},disp={2},physPos={3}",
                                        LocalID, RawPosition, displacement, displacedPos);
                if (PhysBody.HasPhysicalBody)
                {
                    PhysScene.PE.SetTranslation(PhysBody, displacedPos, RawOrientation);
                    ActivateIfPhysical(false);
                }
            }
            else
            {
                base.ForcePosition = value;
            }
        }
    }

    // These are also overridden by BSPrimLinkable if the prim can be part of a linkset
    public override OMV.Vector3 CenterOfMass
    {
        get { return RawPosition; }
    }

    public override OMV.Vector3 GeometricCenter
    {
        get { return RawPosition; }
    }

    public override void UpdateProperties(EntityProperties entprop)
    {
        // Undo any center-of-mass displacement that might have been done.
        if (PositionDisplacement != OMV.Vector3.Zero)
        {
            // The origional shape was offset from 'zero' by PositionDisplacement.
            // These physical location must be back converted to be centered around the displaced
            //     root shape.

            // Move the returned center-of-mass location to the root prim location.
            OMV.Vector3 displacement = PositionDisplacement * entprop.Rotation;
            OMV.Vector3 displacedPos = entprop.Position - displacement;
            DetailLog("{0},BSPrimDisplaced.UpdateProperties,physPos={1},disp={2},simPos={3}",
                                    LocalID, entprop.Position, displacement, displacedPos);
            entprop.Position = displacedPos;
        }

        base.UpdateProperties(entprop);
    }
}
}
