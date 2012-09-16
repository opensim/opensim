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
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.BulletSPlugin
{
// Class to wrap all objects.
// The rest of BulletSim doesn't need to keep checking for avatars or prims
// unless the difference is significant.
public abstract class BSPhysObject : PhysicsActor
{
    public abstract BSLinkset Linkset { get; set; }

    public abstract bool Collide(uint collidingWith, BSPhysObject collidee,
            OMV.Vector3 contactPoint, OMV.Vector3 contactNormal, float pentrationDepth);
    public abstract void SendCollisions();

    // Return the object mass without calculating it or side effects
    public abstract float MassRaw { get; }

    // Reference to the physical body (btCollisionObject) of this object
    public abstract BulletBody BSBody { get; set; }
    // Reference to the physical shape (btCollisionShape) of this object
    public abstract BulletShape BSShape { get; set; }

    public abstract void ZeroMotion();

    public virtual void StepVehicle(float timeStep) { }

    public abstract void UpdateProperties(EntityProperties entprop);

    public abstract void Destroy();
}
}
