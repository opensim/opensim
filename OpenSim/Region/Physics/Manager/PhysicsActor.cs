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
using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Region.Physics.Manager
{
    public delegate void PositionUpdate(Vector3 position);
    public delegate void VelocityUpdate(Vector3 velocity);
    public delegate void OrientationUpdate(Quaternion orientation);

    public enum ActorTypes : int
    {
        Unknown = 0,
        Agent = 1,
        Prim = 2,
        Ground = 3
    }

    public enum PIDHoverType
    {
        Ground
        , GroundAndWater
        , Water
        , Absolute
    }

    public struct ContactPoint
    {
        public Vector3 Position;
        public Vector3 SurfaceNormal;
        public float PenetrationDepth;

        public ContactPoint(Vector3 position, Vector3 surfaceNormal, float penetrationDepth)
        {
            Position = position;
            SurfaceNormal = surfaceNormal;
            PenetrationDepth = penetrationDepth;
        }
    }

    public class CollisionEventUpdate : EventArgs
    {
        // Raising the event on the object, so don't need to provide location..  further up the tree knows that info.

        public int m_colliderType;
        public int m_GenericStartEnd;
        //public uint m_LocalID;
        public Dictionary<uint, ContactPoint> m_objCollisionList = new Dictionary<uint, ContactPoint>();

        public CollisionEventUpdate(uint localID, int colliderType, int GenericStartEnd, Dictionary<uint, ContactPoint> objCollisionList)
        {
            m_colliderType = colliderType;
            m_GenericStartEnd = GenericStartEnd;
            m_objCollisionList = objCollisionList;
        }

        public CollisionEventUpdate()
        {
            m_colliderType = (int) ActorTypes.Unknown;
            m_GenericStartEnd = 1;
            m_objCollisionList = new Dictionary<uint, ContactPoint>();
        }

        public int collidertype
        {
            get { return m_colliderType; }
            set { m_colliderType = value; }
        }

        public int GenericStartEnd
        {
            get { return m_GenericStartEnd; }
            set { m_GenericStartEnd = value; }
        }

        public void addCollider(uint localID, ContactPoint contact)
        {
            if (!m_objCollisionList.ContainsKey(localID))
            {
                m_objCollisionList.Add(localID, contact);
            }
            else
            {
                if (m_objCollisionList[localID].PenetrationDepth < contact.PenetrationDepth)
                    m_objCollisionList[localID] = contact;
            }
        }
    }

    public abstract class PhysicsActor
    {
        public delegate void RequestTerseUpdate();
        public delegate void CollisionUpdate(EventArgs e);
        public delegate void OutOfBounds(Vector3 pos);

// disable warning: public events
#pragma warning disable 67
        public event PositionUpdate OnPositionUpdate;
        public event VelocityUpdate OnVelocityUpdate;
        public event OrientationUpdate OnOrientationUpdate;
        public event RequestTerseUpdate OnRequestTerseUpdate;
        public event CollisionUpdate OnCollisionUpdate;
        public event OutOfBounds OnOutOfBounds;
#pragma warning restore 67

        public static PhysicsActor Null
        {
            get { return new NullPhysicsActor(); }
        }

        public abstract bool Stopped { get; }

        public abstract Vector3 Size { get; set; }

        public abstract PrimitiveBaseShape Shape { set; }

        public abstract uint LocalID { set; }

        public abstract bool Grabbed { set; }

        public abstract bool Selected { set; }

        public string SOPName;
        public string SOPDescription;

        public abstract void CrossingFailure();

        public abstract void link(PhysicsActor obj);

        public abstract void delink();

        public abstract void LockAngularMotion(Vector3 axis);

        public virtual void RequestPhysicsterseUpdate()
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            RequestTerseUpdate handler = OnRequestTerseUpdate;

            if (handler != null)
            {
                handler();
            }
        }

        public virtual void RaiseOutOfBounds(Vector3 pos)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            OutOfBounds handler = OnOutOfBounds;

            if (handler != null)
            {
                handler(pos);
            }
        }

        public virtual void SendCollisionUpdate(EventArgs e)
        {
            CollisionUpdate handler = OnCollisionUpdate;

            if (handler != null)
            {
                handler(e);
            }
        }

        public virtual void SetMaterial (int material)
        {
            
        }

        public abstract Vector3 Position { get; set; }
        public abstract float Mass { get; }
        public abstract Vector3 Force { get; set; }

        public abstract int VehicleType { get; set; }
        public abstract void VehicleFloatParam(int param, float value);
        public abstract void VehicleVectorParam(int param, Vector3 value);
        public abstract void VehicleRotationParam(int param, Quaternion rotation);

        public abstract void SetVolumeDetect(int param);    // Allows the detection of collisions with inherently non-physical prims. see llVolumeDetect for more

        public abstract Vector3 GeometricCenter { get; }
        public abstract Vector3 CenterOfMass { get; }
        public abstract Vector3 Velocity { get; set; }
        public abstract Vector3 Torque { get; set; }
        public abstract float CollisionScore { get; set;}
        public abstract Vector3 Acceleration { get; }
        public abstract Quaternion Orientation { get; set; }
        public abstract int PhysicsActorType { get; set; }
        public abstract bool IsPhysical { get; set; }
        public abstract bool Flying { get; set; }
        public abstract bool SetAlwaysRun { get; set; }
        public abstract bool ThrottleUpdates { get; set; }
        public abstract bool IsColliding { get; set; }
        public abstract bool CollidingGround { get; set; }
        public abstract bool CollidingObj { get; set; }
        public abstract bool FloatOnWater { set; }
        public abstract Vector3 RotationalVelocity { get; set; }
        public abstract bool Kinematic { get; set; }
        public abstract float Buoyancy { get; set; }

        // Used for MoveTo
        public abstract Vector3 PIDTarget { set; }
        public abstract bool  PIDActive { set;}
        public abstract float PIDTau { set; }

        // Used for llSetHoverHeight and maybe vehicle height
        // Hover Height will override MoveTo target's Z
        public abstract bool PIDHoverActive { set;}
        public abstract float PIDHoverHeight { set;}
        public abstract PIDHoverType PIDHoverType { set;}
        public abstract float PIDHoverTau { set;}


        public abstract void AddForce(Vector3 force, bool pushforce);
        public abstract void AddAngularForce(Vector3 force, bool pushforce);
        public abstract void SetMomentum(Vector3 momentum);
        public abstract void SubscribeEvents(int ms);
        public abstract void UnSubscribeEvents();
        public abstract bool SubscribedEvents();
    }

    public class NullPhysicsActor : PhysicsActor
    {
        public override bool Stopped
        {
            get{ return false; }
        }

        public override Vector3 Position
        {
            get { return Vector3.Zero; }
            set { return; }
        }

        public override bool SetAlwaysRun
        {
            get { return false; }
            set { return; }
        }

        public override uint LocalID
        {
            set { return; }
        }

        public override bool Grabbed
        {
            set { return; }
        }

        public override bool Selected
        {
            set { return; }
        }

        public override float Buoyancy
        {
            get { return 0f; }
            set { return; }
        }

        public override bool  FloatOnWater
        {
            set { return; }
        }

        public override bool CollidingGround
        {
            get { return false; }
            set { return; }
        }

        public override bool CollidingObj
        {
            get { return false; }
            set { return; }
        }

        public override Vector3 Size
        {
            get { return Vector3.Zero; }
            set { return; }
        }

        public override float Mass
        {
            get { return 0f; }
        }

        public override Vector3 Force
        {
            get { return Vector3.Zero; }
            set { return; }
        }

        public override int VehicleType
        {
            get { return 0; }
            set { return; }
        }

        public override void VehicleFloatParam(int param, float value)
        {

        }

        public override void VehicleVectorParam(int param, Vector3 value)
        {

        }

        public override void VehicleRotationParam(int param, Quaternion rotation)
        {

        }

        public override void SetVolumeDetect(int param)
        {

        }

        public override void SetMaterial(int material)
        {
            
        }

        public override Vector3 CenterOfMass
        {
            get { return Vector3.Zero; }
        }

        public override Vector3 GeometricCenter
        {
            get { return Vector3.Zero; }
        }

        public override PrimitiveBaseShape Shape
        {
            set { return; }
        }

        public override Vector3 Velocity
        {
            get { return Vector3.Zero; }
            set { return; }
        }

        public override Vector3 Torque
        {
            get { return Vector3.Zero; }
            set { return; }
        }

        public override float CollisionScore
        {
            get { return 0f; }
            set { }
        }

        public override void CrossingFailure()
        {
        }

        public override Quaternion Orientation
        {
            get { return Quaternion.Identity; }
            set { }
        }

        public override Vector3 Acceleration
        {
            get { return Vector3.Zero; }
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

        public override bool ThrottleUpdates
        {
            get { return false; }
            set { return; }
        }

        public override bool IsColliding
        {
            get { return false; }
            set { return; }
        }

        public override int PhysicsActorType
        {
            get { return (int) ActorTypes.Unknown; }
            set { return; }
        }

        public override bool Kinematic
        {
            get { return true; }
            set { return; }
        }

        public override void link(PhysicsActor obj)
        {
        }

        public override void delink()
        {
        }

        public override void LockAngularMotion(Vector3 axis)
        {
        }

        public override void AddForce(Vector3 force, bool pushforce)
        {
        }

        public override void AddAngularForce(Vector3 force, bool pushforce)
        {
            
        }

        public override Vector3 RotationalVelocity
        {
            get { return Vector3.Zero; }
            set { return; }
        }

        public override Vector3 PIDTarget { set { return; } }
        public override bool PIDActive { set { return; } }
        public override float PIDTau { set { return; } }

        public override float PIDHoverHeight { set { return; } }
        public override bool PIDHoverActive { set { return; } }
        public override PIDHoverType PIDHoverType { set { return; } }
        public override float PIDHoverTau { set { return; } }

        public override void SetMomentum(Vector3 momentum)
        {
        }

        public override void SubscribeEvents(int ms)
        {

        }
        public override void UnSubscribeEvents()
        {

        }
        public override bool SubscribedEvents()
        {
            return false;
        }
    }
}
