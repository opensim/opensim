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
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
*/
using Axiom.Math;
using OpenSim.Framework;

namespace OpenSim.Region.Physics.Manager
{
    public delegate void PositionUpdate(PhysicsVector position);

    public delegate void VelocityUpdate(PhysicsVector velocity);

    public delegate void OrientationUpdate(Quaternion orientation);

    public abstract class PhysicsActor
    {
#pragma warning disable 67
        public event PositionUpdate OnPositionUpdate;
        public event VelocityUpdate OnVelocityUpdate;
        public event OrientationUpdate OnOrientationUpdate;
#pragma warning restore 67

        public static PhysicsActor Null
        {
            get { return new NullPhysicsActor(); }
        }

        public abstract PhysicsVector Size { get; set; }

        public abstract PrimitiveBaseShape Shape
        {
            set;
        }

        public abstract PhysicsVector Position { get; set; }

        public abstract PhysicsVector Velocity { get; set; }

        public abstract PhysicsVector Acceleration { get; }

        public abstract Quaternion Orientation { get; set; }

        public abstract bool IsPhysical {get; set;}

        public abstract bool Flying { get; set; }

        public abstract bool IsColliding { get; set; }

        public abstract bool Kinematic { get; set; }

        public abstract void AddForce(PhysicsVector force);

        public abstract void SetMomentum(PhysicsVector momentum);
    }

    public class NullPhysicsActor : PhysicsActor
    {
        public override PhysicsVector Position
        {
            get { return PhysicsVector.Zero; }
            set { return; }
        }

        public override PhysicsVector Size
        {
            get { return PhysicsVector.Zero; }
            set { return; }
        }

        public override PrimitiveBaseShape Shape
        {
            set
            {
                return;
            }
        }

        public override PhysicsVector Velocity
        {
            get { return PhysicsVector.Zero; }
            set { return; }
        }

        public override Quaternion Orientation
        {
            get { return Quaternion.Identity; }
            set { }
        }

        public override PhysicsVector Acceleration
        {
            get { return PhysicsVector.Zero; }
        }

        public override bool IsPhysical
        {
            get { return false; }
            set { return; }
        }

        public override bool Flying
        {
            get { return false; }
            set { return; }
        }

        public override bool IsColliding
        {
            get { return false; }
            set { return; }
        }

        public override bool Kinematic
        {
            get { return true; }
            set { return; }
        }

        public override void AddForce(PhysicsVector force)
        {
            return;
        }

        public override void SetMomentum(PhysicsVector momentum)
        {
            return;
        }
    }
}