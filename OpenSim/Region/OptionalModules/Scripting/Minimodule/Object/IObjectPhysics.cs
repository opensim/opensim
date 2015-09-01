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
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule.Object
{
    /// <summary>
    /// This implements an interface similar to that provided by physics engines to OpenSim internally.
    /// Eg, PhysicsActor. It is capable of setting and getting properties related to the current
    /// physics scene representation of this object.
    /// </summary>
    public interface IObjectPhysics
    {
        bool Enabled { get; set; }

        bool Phantom { get; set; }
        bool PhantomCollisions { get; set; }

        double Density { get; set; }
        double Mass { get; set; }
        double Buoyancy { get; set; }

        Vector3 GeometricCenter { get; }
        Vector3 CenterOfMass { get; }

        Vector3 RotationalVelocity { get; set; }
        Vector3 Velocity { get; set; }
        Vector3 Torque { get; set; }
        Vector3 Acceleration { get; }
        Vector3 Force { get; set; }

        bool FloatOnWater { set; }

        void AddForce(Vector3 force, bool pushforce);
        void AddAngularForce(Vector3 force, bool pushforce);
        void SetMomentum(Vector3 momentum);
    }
}
