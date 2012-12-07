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


// Revision by Ubit 2011/12

using System;
using System.Collections.Generic;
using System.Reflection;
using OpenMetaverse;
using OdeAPI;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using log4net;

namespace OpenSim.Region.Physics.OdePlugin
{
    /// <summary>
    /// Various properties that ODE uses for AMotors but isn't exposed in ODE.NET so we must define them ourselves.
    /// </summary>

    public enum dParam : int
    {
        LowStop = 0,
        HiStop = 1,
        Vel = 2,
        FMax = 3,
        FudgeFactor = 4,
        Bounce = 5,
        CFM = 6,
        StopERP = 7,
        StopCFM = 8,
        LoStop2 = 256,
        HiStop2 = 257,
        Vel2 = 258,
        FMax2 = 259,
        StopERP2 = 7 + 256,
        StopCFM2 = 8 + 256,
        LoStop3 = 512,
        HiStop3 = 513,
        Vel3 = 514,
        FMax3 = 515,
        StopERP3 = 7 + 512,
        StopCFM3 = 8 + 512
    }
 
    public class OdeCharacter : PhysicsActor
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Vector3 _position;
        private Vector3 _zeroPosition;
        private bool _zeroFlag = false;
        private Vector3 _velocity;
        private Vector3 _target_velocity;
        private Vector3 _acceleration;
        private Vector3 m_rotationalVelocity;
        private Vector3 m_size;
        private Quaternion m_orientation;
        private float m_mass = 80f;
        public float m_density = 60f;
        private bool m_pidControllerActive = true;

        const float basePID_D = 0.55f; // scaled for unit mass unit time (2200 /(50*80))
        const float basePID_P = 0.225f; // scaled for unit mass unit time (900 /(50*80))
        public float PID_D;
        public float PID_P;

        private float m_feetOffset = 0;
        private float feetOff = 0;
        private float feetSZ = 0.5f;
        const float feetScale = 0.8f;
        private float boneOff = 0;

        public float walkDivisor = 1.3f;
        public float runDivisor = 0.8f;
        private bool flying = false;
        private bool m_iscolliding = false;
        private bool m_iscollidingGround = false;
        private bool m_iscollidingObj = false;
        private bool m_alwaysRun = false;
        private int m_requestedUpdateFrequency = 0;
        private uint m_localID = 0;
        public bool m_returnCollisions = false;
        // taints and their non-tainted counterparts
        public bool m_isPhysical = false; // the current physical status
        public float MinimumGroundFlightOffset = 3f;

        private float m_buoyancy = 0f;

        private bool m_freemove = false;
        // private CollisionLocker ode;

//        private string m_name = String.Empty;
        // other filter control
        int m_colliderfilter = 0;
        int m_colliderGroundfilter = 0;
        int m_colliderObjectfilter = 0;

        // Default we're a Character
        private CollisionCategories m_collisionCategories = (CollisionCategories.Character);

        // Default, Collide with Other Geometries, spaces, bodies and characters.
        private CollisionCategories m_collisionFlags = (CollisionCategories.Character
                                                        | CollisionCategories.Geom
                                                        | CollisionCategories.VolumeDtc
                                                        );
        // we do land collisions not ode                | CollisionCategories.Land);
        public IntPtr Body = IntPtr.Zero;
        private OdeScene _parent_scene;
        public IntPtr topbox = IntPtr.Zero;
        public IntPtr midbox = IntPtr.Zero;
        public IntPtr feetbox = IntPtr.Zero;
        public IntPtr bonebox = IntPtr.Zero;

        public IntPtr Amotor = IntPtr.Zero;

        public d.Mass ShellMass;



        public int m_eventsubscription = 0;
        private int m_cureventsubscription = 0;
        private CollisionEventUpdate CollisionEventsThisFrame = null;
        private bool SentEmptyCollisionsEvent;

        // unique UUID of this character object
        public UUID m_uuid;
        public bool bad = false;

        float mu;

        

        public OdeCharacter(String avName, OdeScene parent_scene, Vector3 pos, Vector3 pSize, float pfeetOffset, float density, float walk_divisor, float rundivisor)
        {
            m_uuid = UUID.Random();

            if (pos.IsFinite())
            {
                if (pos.Z > 99999f)
                {
                    pos.Z = parent_scene.GetTerrainHeightAtXY(127, 127) + 5;
                }
                if (pos.Z < -100f) // shouldn't this be 0 ?
                {
                    pos.Z = parent_scene.GetTerrainHeightAtXY(127, 127) + 5;
                }
                _position = pos;
            }
            else
            {
                _position = new Vector3(((float)_parent_scene.WorldExtents.X * 0.5f), ((float)_parent_scene.WorldExtents.Y * 0.5f), parent_scene.GetTerrainHeightAtXY(128f, 128f) + 10f);
                m_log.Warn("[PHYSICS]: Got NaN Position on Character Create");
            }

            _parent_scene = parent_scene;


            m_size.X = pSize.X;
            m_size.Y = pSize.Y;
            m_size.Z = pSize.Z;

            if(m_size.X <0.01f)
                m_size.X = 0.01f;
            if(m_size.Y <0.01f)
                m_size.Y = 0.01f;
            if(m_size.Z <0.01f)
                m_size.Z = 0.01f;

            m_feetOffset = pfeetOffset;
            m_orientation = Quaternion.Identity;
            m_density = density;

            // force lower density for testing
            m_density = 3.0f;

            m_density *= 1.4f; // scale to have mass similar to capsule

            mu = parent_scene.AvatarFriction;

            walkDivisor = walk_divisor;
            runDivisor = rundivisor;

            m_mass = m_density * m_size.X * m_size.Y * m_size.Z; ; // sure we have a default           

            PID_D = basePID_D * m_mass / parent_scene.ODE_STEPSIZE;
            PID_P = basePID_P * m_mass / parent_scene.ODE_STEPSIZE;

            m_isPhysical = false; // current status: no ODE information exists

            Name = avName;

            AddChange(changes.Add, null);
        }

        public override int PhysicsActorType
        {
            get { return (int)ActorTypes.Agent; }
            set { return; }
        }

        public override void getContactData(ref ContactData cdata)
        {
            cdata.mu = mu;
            cdata.bounce = 0;
            cdata.softcolide = false;
        }

        public override bool Building { get; set; }

        /// <summary>
        /// If this is set, the avatar will move faster
        /// </summary>
        public override bool SetAlwaysRun
        {
            get { return m_alwaysRun; }
            set { m_alwaysRun = value; }
        }

        public override uint LocalID
        {
            get { return m_localID; }     
            set { m_localID = value; }
        }

        public override PhysicsActor ParentActor
        {
            get { return (PhysicsActor)this; }
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
            get { return m_buoyancy; }
            set { m_buoyancy = value; }
        }

        public override bool FloatOnWater
        {
            set { return; }
        }

        public override bool IsPhysical
        {
            get { return m_isPhysical; }
            set { return; }
        }

        public override bool ThrottleUpdates
        {
            get { return false; }
            set { return; }
        }

        public override bool Flying
        {
            get { return flying; }
            set
            {
                flying = value;
                //                m_log.DebugFormat("[PHYSICS]: Set OdeCharacter Flying to {0}", flying);
            }
        }

        /// <summary>
        /// Returns if the avatar is colliding in general.
        /// This includes the ground and objects and avatar.
        /// </summary>
        public override bool IsColliding
        {
            get { return (m_iscolliding || m_iscollidingGround); }
            set
            {
                if (value)
                {
                    m_colliderfilter += 2;
                    if (m_colliderfilter > 2)
                        m_colliderfilter = 2;
                }
                else
                {
                    m_colliderfilter--;
                    if (m_colliderfilter < 0)
                        m_colliderfilter = 0;
                }

                if (m_colliderfilter == 0)
                    m_iscolliding = false;
                else
                {
                    m_pidControllerActive = true;
                    m_iscolliding = true;
                }
            }
        }

        /// <summary>
        /// Returns if an avatar is colliding with the ground
        /// </summary>
        public override bool CollidingGround
        {
            get { return m_iscollidingGround; }
            set
            {
                /*  we now control this
                                if (value)
                                    {
                                    m_colliderGroundfilter += 2;
                                    if (m_colliderGroundfilter > 2)
                                        m_colliderGroundfilter = 2;
                                    }
                                else
                                    {
                                    m_colliderGroundfilter--;
                                    if (m_colliderGroundfilter < 0)
                                        m_colliderGroundfilter = 0;
                                    }

                                if (m_colliderGroundfilter == 0)
                                    m_iscollidingGround = false;
                                else
                                    m_iscollidingGround = true;
                 */
            }

        }

        /// <summary>
        /// Returns if the avatar is colliding with an object
        /// </summary>
        public override bool CollidingObj
        {
            get { return m_iscollidingObj; }
            set
            {
                // Ubit filter this also
                if (value)
                {
                    m_colliderObjectfilter += 2;
                    if (m_colliderObjectfilter > 2)
                        m_colliderObjectfilter = 2;
                }
                else
                {
                    m_colliderObjectfilter--;
                    if (m_colliderObjectfilter < 0)
                        m_colliderObjectfilter = 0;
                }

                if (m_colliderObjectfilter == 0)
                    m_iscollidingObj = false;
                else
                    m_iscollidingObj = true;

                //            m_iscollidingObj = value;

                if (m_iscollidingObj)
                    m_pidControllerActive = false;
                else
                    m_pidControllerActive = true;
            }
        }

        /// <summary>
        /// turn the PID controller on or off.
        /// The PID Controller will turn on all by itself in many situations
        /// </summary>
        /// <param name="status"></param>
        public void SetPidStatus(bool status)
        {
            m_pidControllerActive = status;
        }

        public override bool Stopped
        {
            get { return _zeroFlag; }
        }

        /// <summary>
        /// This 'puts' an avatar somewhere in the physics space.
        /// Not really a good choice unless you 'know' it's a good
        /// spot otherwise you're likely to orbit the avatar.
        /// </summary>
        public override Vector3 Position
        {
            get { return _position; }
            set
            {
                if (value.IsFinite())
                {
                    if (value.Z > 9999999f)
                    {
                        value.Z = _parent_scene.GetTerrainHeightAtXY(127, 127) + 5;
                    }
                    if (value.Z < -100f)
                    {
                        value.Z = _parent_scene.GetTerrainHeightAtXY(127, 127) + 5;
                    }
                    AddChange(changes.Position, value);
                }
                else
                {
                    m_log.Warn("[PHYSICS]: Got a NaN Position from Scene on a Character");
                }
            }
        }

        public override Vector3 RotationalVelocity
        {
            get { return m_rotationalVelocity; }
            set { m_rotationalVelocity = value; }
        }

        /// <summary>
        /// This property sets the height of the avatar only.  We use the height to make sure the avatar stands up straight
        /// and use it to offset landings properly
        /// </summary>
        public override Vector3 Size
        {
            get
            {
                return m_size;
            }
            set
            {
                if (value.IsFinite())
                {
                    if(value.X <0.01f)
                        value.X = 0.01f;
                    if(value.Y <0.01f)
                        value.Y = 0.01f;
                    if(value.Z <0.01f)
                        value.Z = 0.01f;

                    AddChange(changes.Size, value);
                }
                else
                {
                    m_log.Warn("[PHYSICS]: Got a NaN Size from Scene on a Character");
                }
            }
        }

        public override void setAvatarSize(Vector3 size, float feetOffset)
        {
            if (size.IsFinite())
            {
                if (size.X < 0.01f)
                    size.X = 0.01f;
                if (size.Y < 0.01f)
                    size.Y = 0.01f;
                if (size.Z < 0.01f)
                    size.Z = 0.01f;

                strAvatarSize st = new strAvatarSize();
                st.size = size;
                st.offset = feetOffset;
                AddChange(changes.AvatarSize, st);
            }
            else
            {
                m_log.Warn("[PHYSICS]: Got a NaN AvatarSize from Scene on a Character");
            }
            
        }
        /// <summary>
        /// This creates the Avatar's physical Surrogate at the position supplied
        /// </summary>
        /// <param name="npositionX"></param>
        /// <param name="npositionY"></param>
        /// <param name="npositionZ"></param>

        //
        /// <summary>
        /// Uses the capped cyllinder volume formula to calculate the avatar's mass.
        /// This may be used in calculations in the scene/scenepresence
        /// </summary>
        public override float Mass
        {
            get
            {
                return m_mass;
            }
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


        public override Vector3 Force
        {
            get { return _target_velocity; }
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

        public override void VehicleFlags(int param, bool remove)
        {

        }

        public override void SetVolumeDetect(int param)
        {

        }

        public override Vector3 CenterOfMass
        {
            get
            {
                Vector3 pos = _position;
                return pos;
            }
        }

        public override Vector3 GeometricCenter
        {
            get
            {
                Vector3 pos = _position;
                return pos;
            }
        }

        public override PrimitiveBaseShape Shape
        {
            set { return; }
        }

        public override Vector3 Velocity
        {
            get
            {
                return _velocity;
            }
            set
            {
                if (value.IsFinite())
                {
                    AddChange(changes.Velocity, value);
                }
                else
                {
                    m_log.Warn("[PHYSICS]: Got a NaN velocity from Scene in a Character");
                }
            }
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

        public override bool Kinematic
        {
            get { return false; }
            set { }
        }

        public override Quaternion Orientation
        {
            get { return m_orientation; }
            set
            {
                //                fakeori = value;
                //                givefakeori++;

                value.Normalize();
                AddChange(changes.Orientation, value);
            }
        }

        public override Vector3 Acceleration
        {
            get { return _acceleration; }
            set { }
        }

        public void SetAcceleration(Vector3 accel)
        {
            m_pidControllerActive = true;
            _acceleration = accel;
        }

        /// <summary>
        /// Adds the force supplied to the Target Velocity
        /// The PID controller takes this target velocity and tries to make it a reality
        /// </summary>
        /// <param name="force"></param>
        public override void AddForce(Vector3 force, bool pushforce)
        {
            if (force.IsFinite())
            {
                if (pushforce)
                {
                    AddChange(changes.Force, force * m_density / (_parent_scene.ODE_STEPSIZE * 28f));
                }
                else
                {
                    AddChange(changes.Velocity, force);
                }
            }
            else
            {
                m_log.Warn("[PHYSICS]: Got a NaN force applied to a Character");
            }
            //m_lastUpdateSent = false;
        }

        public override void AddAngularForce(Vector3 force, bool pushforce)
        {

        }

        public override void SetMomentum(Vector3 momentum)
        {
            if (momentum.IsFinite())
                AddChange(changes.Momentum, momentum);
        }

        private void AvatarGeomAndBodyCreation(float npositionX, float npositionY, float npositionZ)
        {
            // sizes  one day should came from visual parameters
            float sx = m_size.X;
            float sy = m_size.Y;
            float sz = m_size.Z;


            float topsx = sx * 0.9f;
            float midsx = sx;
            float feetsx = sx * feetScale;
            float bonesx = sx * 0.2f;

            float topsy = sy * 0.4f;
            float midsy = sy;
            float feetsy = sy * feetScale * 0.8f;
            float bonesy = feetsy * 0.2f;

            float topsz = sz * 0.15f;
            float feetsz = sz * 0.45f;
            if (feetsz > 0.6f)
                feetsz = 0.6f;

            float midsz = sz - topsz - feetsz;
            float bonesz = sz;

            float bot = -sz * 0.5f + m_feetOffset;

            boneOff = bot + 0.3f;

            float feetz = bot + feetsz * 0.5f;
            bot += feetsz;

            feetOff = bot;
            feetSZ = feetsz;

            float midz = bot + midsz * 0.5f;
            bot += midsz;
            float topz = bot + topsz * 0.5f;

            _parent_scene.waitForSpaceUnlock(_parent_scene.CharsSpace);

            feetbox = d.CreateBox(_parent_scene.CharsSpace, feetsx, feetsy, feetsz);
            d.GeomSetCategoryBits(feetbox, (uint)m_collisionCategories);
            d.GeomSetCollideBits(feetbox, (uint)m_collisionFlags);

            midbox = d.CreateBox(_parent_scene.CharsSpace, midsx, midsy, midsz);
            d.GeomSetCategoryBits(midbox, (uint)m_collisionCategories);
            d.GeomSetCollideBits(midbox, (uint)m_collisionFlags);

            topbox = d.CreateBox(_parent_scene.CharsSpace, topsx, topsy, topsz);
            d.GeomSetCategoryBits(topbox, (uint)m_collisionCategories);
            d.GeomSetCollideBits(topbox, (uint)m_collisionFlags);

            bonebox = d.CreateBox(_parent_scene.CharsSpace, bonesx, bonesy, bonesz);
            d.GeomSetCategoryBits(bonebox, (uint)m_collisionCategories);
            d.GeomSetCollideBits(bonebox, (uint)m_collisionFlags);

            m_mass = m_density * m_size.X * m_size.Y * m_size.Z;  // update mass

            d.MassSetBoxTotal(out ShellMass, m_mass, m_size.X, m_size.Y, m_size.Z);

            PID_D = basePID_D * m_mass / _parent_scene.ODE_STEPSIZE;
            PID_P = basePID_P * m_mass / _parent_scene.ODE_STEPSIZE;
            
            Body = d.BodyCreate(_parent_scene.world);

            _zeroFlag = false;
            m_pidControllerActive = true;
            m_freemove = false;

            d.BodySetAutoDisableFlag(Body, false);
            d.BodySetPosition(Body, npositionX, npositionY, npositionZ);

            _position.X = npositionX;
            _position.Y = npositionY;
            _position.Z = npositionZ;

            d.BodySetMass(Body, ref ShellMass);
            d.GeomSetBody(feetbox, Body);
            d.GeomSetBody(midbox, Body);
            d.GeomSetBody(topbox, Body);
            d.GeomSetBody(bonebox, Body);

            d.GeomSetOffsetPosition(feetbox, 0, 0, feetz);
            d.GeomSetOffsetPosition(midbox, 0, 0, midz);
            d.GeomSetOffsetPosition(topbox, 0, 0, topz);
            d.GeomSetOffsetPosition(bonebox, 0, 0, m_feetOffset);

            // The purpose of the AMotor here is to keep the avatar's physical
            // surrogate from rotating while moving
            Amotor = d.JointCreateAMotor(_parent_scene.world, IntPtr.Zero);
            d.JointAttach(Amotor, Body, IntPtr.Zero);

            d.JointSetAMotorMode(Amotor, 0);
            d.JointSetAMotorNumAxes(Amotor, 3);
            d.JointSetAMotorAxis(Amotor, 0, 0, 1, 0, 0);
            d.JointSetAMotorAxis(Amotor, 1, 0, 0, 1, 0);
            d.JointSetAMotorAxis(Amotor, 2, 0, 0, 0, 1);

            d.JointSetAMotorAngle(Amotor, 0, 0);
            d.JointSetAMotorAngle(Amotor, 1, 0);
            d.JointSetAMotorAngle(Amotor, 2, 0);

            d.JointSetAMotorParam(Amotor, (int)dParam.StopCFM, 0f); // make it HARD
            d.JointSetAMotorParam(Amotor, (int)dParam.StopCFM2, 0f);
            d.JointSetAMotorParam(Amotor, (int)dParam.StopCFM3, 0f);
            d.JointSetAMotorParam(Amotor, (int)dParam.StopERP, 0.8f);
            d.JointSetAMotorParam(Amotor, (int)dParam.StopERP2, 0.8f);
            d.JointSetAMotorParam(Amotor, (int)dParam.StopERP3, 0.8f);

            // These lowstops and high stops are effectively (no wiggle room)
            d.JointSetAMotorParam(Amotor, (int)dParam.LowStop, -1e-5f);
            d.JointSetAMotorParam(Amotor, (int)dParam.HiStop, 1e-5f);
            d.JointSetAMotorParam(Amotor, (int)dParam.LoStop2, -1e-5f);
            d.JointSetAMotorParam(Amotor, (int)dParam.HiStop2, 1e-5f);
            d.JointSetAMotorParam(Amotor, (int)dParam.LoStop3, -1e-5f);
            d.JointSetAMotorParam(Amotor, (int)dParam.HiStop3, 1e-5f);

            d.JointSetAMotorParam(Amotor, (int)d.JointParam.Vel, 0);
            d.JointSetAMotorParam(Amotor, (int)d.JointParam.Vel2, 0);
            d.JointSetAMotorParam(Amotor, (int)d.JointParam.Vel3, 0);

            d.JointSetAMotorParam(Amotor, (int)dParam.FMax, 5e8f);
            d.JointSetAMotorParam(Amotor, (int)dParam.FMax2, 5e8f);
            d.JointSetAMotorParam(Amotor, (int)dParam.FMax3, 5e8f);
        }

        /// <summary>
        /// Destroys the avatar body and geom

        private void AvatarGeomAndBodyDestroy()
        {
            // Kill the Amotor
            if (Amotor != IntPtr.Zero)
            {
                d.JointDestroy(Amotor);
                Amotor = IntPtr.Zero;
            }

            if (Body != IntPtr.Zero)
            {
                //kill the body
                d.BodyDestroy(Body);
                Body = IntPtr.Zero;
            }

            //kill the Geoms
            if (topbox != IntPtr.Zero)
            {
                _parent_scene.actor_name_map.Remove(topbox);
                _parent_scene.waitForSpaceUnlock(_parent_scene.CharsSpace);
                d.GeomDestroy(topbox);
                topbox = IntPtr.Zero;
            }
            if (midbox != IntPtr.Zero)
            {
                _parent_scene.actor_name_map.Remove(midbox);
                _parent_scene.waitForSpaceUnlock(_parent_scene.CharsSpace);
                d.GeomDestroy(midbox);
                midbox = IntPtr.Zero;
            }
            if (feetbox != IntPtr.Zero)
            {
                _parent_scene.actor_name_map.Remove(feetbox);
                _parent_scene.waitForSpaceUnlock(_parent_scene.CharsSpace);
                d.GeomDestroy(feetbox);
                feetbox = IntPtr.Zero;
            }

            if (bonebox != IntPtr.Zero)
            {
                _parent_scene.actor_name_map.Remove(bonebox);
                _parent_scene.waitForSpaceUnlock(_parent_scene.CharsSpace);
                d.GeomDestroy(bonebox);
                bonebox = IntPtr.Zero;
            }

        }

        public bool Collide(IntPtr me, bool reverse, ref d.ContactGeom contact, ref bool feetcollision)
        {
            feetcollision = false;

            if (me == bonebox) // inner bone
            {
                if (contact.pos.Z - _position.Z < boneOff)
                    IsColliding = true;
                return true;
            }

            if (me == topbox) // keep a box head
                return true;

            float t;
            float offx = contact.pos.X - _position.X;
            float offy = contact.pos.Y - _position.Y;

            if (me == midbox)
            {
                t = offx * offx + offy * offy;
                t = (float)Math.Sqrt(t);
                t = 1 / t;
                offx *= t;
                offy *= t;

                if (reverse)
                {
                    contact.normal.X = offx;
                    contact.normal.Y = offy;
                }
                else
                {
                    contact.normal.X = -offx;
                    contact.normal.Y = -offy;
                }

                contact.normal.Z = 0;
                return true;
            }

            else if (me == feetbox)
            {
                float h = contact.pos.Z - _position.Z;

                if (Math.Abs(contact.normal.Z) > 0.95f)
                {
                    feetcollision = true;
                    if (h < boneOff)
                        IsColliding = true;
                    return true;
                }

                float offz = h - feetOff; // distance from top of feetbox

                if (offz > 0)
                    return false;

                if (offz > -0.01)
                {
                    offx = 0;
                    offy = 0;
                    offz = -1.0f;
                }
                else
                {
                    t = offx * offx + offy * offy + offz * offz;
                    t = (float)Math.Sqrt(t);
                    t = 1 / t;
                    offx *= t;
                    offy *= t;
                    offz *= t;
                }

                if (reverse)
                {
                    contact.normal.X = offx;
                    contact.normal.Y = offy;
                    contact.normal.Z = offz;
                }
                else
                {
                    contact.normal.X = -offx;
                    contact.normal.Y = -offy;
                    contact.normal.Z = -offz;
                }
                feetcollision = true;
                if (h < boneOff)
                    IsColliding = true;
            }
            else
                return false;

            return true;
        }

        /// <summary>
        /// Called from Simulate
        /// This is the avatar's movement control + PID Controller
        /// </summary>
        /// <param name="timeStep"></param>
        public void Move(float timeStep, List<OdeCharacter> defects)
        {
            if (Body == IntPtr.Zero)
                return;

            d.Vector3 dtmp = d.BodyGetPosition(Body);
            Vector3 localpos = new Vector3(dtmp.X, dtmp.Y, dtmp.Z);

            // the Amotor still lets avatar rotation to drift during colisions
            // so force it back to identity

            d.Quaternion qtmp;
            qtmp.W = m_orientation.W;
            qtmp.X = m_orientation.X;
            qtmp.Y = m_orientation.Y;
            qtmp.Z = m_orientation.Z;
            d.BodySetQuaternion(Body, ref qtmp);

            if (m_pidControllerActive == false)
            {
                _zeroPosition = localpos;
            }

            if (!localpos.IsFinite())
            {
                m_log.Warn("[PHYSICS]: Avatar Position is non-finite!");
                defects.Add(this);
                // _parent_scene.RemoveCharacter(this);

                // destroy avatar capsule and related ODE data
                AvatarGeomAndBodyDestroy();
                return;
            }

            // check outbounds forcing to be in world
            bool fixbody = false;
            if (localpos.X < 0.0f)
            {
                fixbody = true;
                localpos.X = 0.1f;
            }
            else if (localpos.X > _parent_scene.WorldExtents.X - 0.1f)
            {
                fixbody = true;
                localpos.X = _parent_scene.WorldExtents.X - 0.1f;
            }
            if (localpos.Y < 0.0f)
            {
                fixbody = true;
                localpos.Y = 0.1f;
            }
            else if (localpos.Y > _parent_scene.WorldExtents.Y - 0.1)
            {
                fixbody = true;
                localpos.Y = _parent_scene.WorldExtents.Y - 0.1f;
            }
            if (fixbody)
            {
                m_freemove = false;
                d.BodySetPosition(Body, localpos.X, localpos.Y, localpos.Z);
            }

            float breakfactor;

            Vector3 vec = Vector3.Zero;
            dtmp = d.BodyGetLinearVel(Body);
            Vector3 vel = new Vector3(dtmp.X, dtmp.Y, dtmp.Z);
            float velLengthSquared = vel.LengthSquared();

            float movementdivisor = 1f;
            //Ubit change divisions into multiplications below
            if (!m_alwaysRun)
                movementdivisor = 1 / walkDivisor;
            else
                movementdivisor = 1 / runDivisor;

            //******************************************
            // colide with land
            d.AABB aabb;
            d.GeomGetAABB(feetbox, out aabb);
            float chrminZ = aabb.MinZ - 0.02f; // move up a bit
            Vector3 posch = localpos;

            float ftmp;

            if (flying)
            {
                ftmp = timeStep;
                posch.X += vel.X * ftmp;
                posch.Y += vel.Y * ftmp;
            }

            float terrainheight = _parent_scene.GetTerrainHeightAtXY(posch.X, posch.Y);
            if (chrminZ < terrainheight)
            {
                float depth = terrainheight - chrminZ;
                if (!flying)
                {
                    vec.Z = -vel.Z * PID_D * 1.5f + depth * PID_P * 50;
                }
                else
                    vec.Z = depth * PID_P * 50;

                if (depth < 0.1f)
                {
                    m_colliderGroundfilter++;
                    if (m_colliderGroundfilter > 2)
                    {
                        m_iscolliding = true;
                        m_colliderfilter = 2;

                        if (m_colliderGroundfilter > 10)
                        {
                            m_colliderGroundfilter = 10;
                            m_freemove = false;
                        }

                        m_iscollidingGround = true;

                        ContactPoint contact = new ContactPoint();
                        contact.PenetrationDepth = depth;
                        contact.Position.X = localpos.X;
                        contact.Position.Y = localpos.Y;
                        contact.Position.Z = chrminZ;
                        contact.SurfaceNormal.X = 0f;
                        contact.SurfaceNormal.Y = 0f;
                        contact.SurfaceNormal.Z = -1f;
                        contact.RelativeSpeed = -vel.Z;
                        contact.CharacterFeet = true;
                        AddCollisionEvent(0, contact);

                        vec.Z *= 0.5f;
                    }
                }

                else
                {
                    m_colliderGroundfilter = 0;
                    m_iscollidingGround = false;
                }
            }
            else
            {
                m_colliderGroundfilter = 0;
                m_iscollidingGround = false;
            }

            //******************************************

            bool tviszero = (_target_velocity.X == 0.0f && _target_velocity.Y == 0.0f && _target_velocity.Z == 0.0f);

            //            if (!tviszero || m_iscolliding || velLengthSquared <0.01)
            if (!tviszero)
                m_freemove = false;

            if (!m_freemove)
            {

                //  if velocity is zero, use position control; otherwise, velocity control
                if (tviszero && m_iscolliding)
                {
                    //  keep track of where we stopped.  No more slippin' & slidin'
                    if (!_zeroFlag)
                    {
                        _zeroFlag = true;
                        _zeroPosition = localpos;
                    }
                    if (m_pidControllerActive)
                    {
                        // We only want to deactivate the PID Controller if we think we want to have our surrogate
                        // react to the physics scene by moving it's position.
                        // Avatar to Avatar collisions
                        // Prim to avatar collisions

                        vec.X = -vel.X * PID_D + (_zeroPosition.X - localpos.X) * (PID_P * 2);
                        vec.Y = -vel.Y * PID_D + (_zeroPosition.Y - localpos.Y) * (PID_P * 2);
                        if (flying)
                        {
                            vec.Z += -vel.Z * PID_D + (_zeroPosition.Z - localpos.Z) * PID_P;
                        }
                    }
                    //PidStatus = true;
                }
                else
                {
                    m_pidControllerActive = true;
                    _zeroFlag = false;

                    if (m_iscolliding)
                    {
                        if (!flying)
                        {
                            if (_target_velocity.Z > 0.0f)
                            {
                                // We're colliding with something and we're not flying but we're moving
                                // This means we're walking or running. JUMPING
                                vec.Z += (_target_velocity.Z - vel.Z) * PID_D * 1.2f;// +(_zeroPosition.Z - localpos.Z) * PID_P;
                            }
                            // We're standing on something
                            vec.X = ((_target_velocity.X * movementdivisor) - vel.X) * (PID_D);
                            vec.Y = ((_target_velocity.Y * movementdivisor) - vel.Y) * (PID_D);
                        }
                        else
                        {
                            // We're flying and colliding with something
                            vec.X = ((_target_velocity.X * movementdivisor) - vel.X) * (PID_D * 0.0625f);
                            vec.Y = ((_target_velocity.Y * movementdivisor) - vel.Y) * (PID_D * 0.0625f);
                            vec.Z += (_target_velocity.Z - vel.Z) * (PID_D);
                        }
                    }
                    else // ie not colliding
                    {
                        if (flying) //(!m_iscolliding && flying)
                        {
                            // we're in mid air suspended
                            vec.X = ((_target_velocity.X * movementdivisor) - vel.X) * (PID_D * 1.667f);
                            vec.Y = ((_target_velocity.Y * movementdivisor) - vel.Y) * (PID_D * 1.667f);
                            vec.Z += (_target_velocity.Z - vel.Z) * (PID_D);
                        }

                        else
                        {
                            // we're not colliding and we're not flying so that means we're falling!
                            // m_iscolliding includes collisions with the ground.

                            // d.Vector3 pos = d.BodyGetPosition(Body);
                            vec.X = (_target_velocity.X - vel.X) * PID_D * 0.833f;
                            vec.Y = (_target_velocity.Y - vel.Y) * PID_D * 0.833f;
                        }
                    }
                }

                if (velLengthSquared > 2500.0f) // 50m/s apply breaks
                {
                    breakfactor = 0.16f * m_mass;
                    vec.X -= breakfactor * vel.X;
                    vec.Y -= breakfactor * vel.Y;
                    vec.Z -= breakfactor * vel.Z;
                }
            }
            else
            {
                breakfactor = m_mass;
                vec.X -= breakfactor * vel.X;
                vec.Y -= breakfactor * vel.Y;
                if (flying)
                    vec.Z -= breakfactor * vel.Z;
                else
                    vec.Z -= .5f* m_mass * vel.Z;
            }

            if (flying)
            {
                vec.Z -= _parent_scene.gravityz * m_mass;

                //Added for auto fly height. Kitto Flora
                float target_altitude = _parent_scene.GetTerrainHeightAtXY(localpos.X, localpos.Y) + MinimumGroundFlightOffset;

                if (localpos.Z < target_altitude)
                {
                    vec.Z += (target_altitude - localpos.Z) * PID_P * 5.0f;
                }
                // end add Kitto Flora
            }

            if (vec.IsFinite())
            {
                if (vec.X != 0 || vec.Y !=0 || vec.Z !=0)
                    d.BodyAddForce(Body, vec.X, vec.Y, vec.Z);
            }
            else
            {
                m_log.Warn("[PHYSICS]: Got a NaN force vector in Move()");
                m_log.Warn("[PHYSICS]: Avatar Position is non-finite!");
                defects.Add(this);
                // _parent_scene.RemoveCharacter(this);
                // destroy avatar capsule and related ODE data
                AvatarGeomAndBodyDestroy();
                return;
            }

            // update our local ideia of position velocity and aceleration
            _position = localpos;
            if (_zeroFlag)
            {
                _velocity = Vector3.Zero;
                _acceleration = Vector3.Zero;
            }
            else
            {
                _acceleration = _velocity; // previus velocity
                _velocity = vel;
                _acceleration = (vel - _acceleration) / timeStep;
            }
          
        }

        /// <summary>
        /// Updates the reported position and velocity.
        /// Used to copy variables from unmanaged space at heartbeat rate and also trigger scene updates acording
        /// also outbounds checking
        /// copy and outbounds now done in move(..) at ode rate
        /// 
        /// </summary>
        public void UpdatePositionAndVelocity()
        {
            return;

//            if (Body == IntPtr.Zero)
//                return;

        }

        /// <summary>
        /// Cleanup the things we use in the scene.
        /// </summary>
        public void Destroy()
        {
            AddChange(changes.Remove, null);
        }

        public override void CrossingFailure()
        {
        }

        public override Vector3 PIDTarget { set { return; } }
        public override bool PIDActive { set { return; } }
        public override float PIDTau { set { return; } }

        public override float PIDHoverHeight { set { return; } }
        public override bool PIDHoverActive { set { return; } }
        public override PIDHoverType PIDHoverType { set { return; } }
        public override float PIDHoverTau { set { return; } }

        public override Quaternion APIDTarget { set { return; } }

        public override bool APIDActive { set { return; } }

        public override float APIDStrength { set { return; } }

        public override float APIDDamping { set { return; } }


        public override void SubscribeEvents(int ms)
        {
            m_eventsubscription = ms;
            m_cureventsubscription = 0;
            if (CollisionEventsThisFrame == null)
                CollisionEventsThisFrame = new CollisionEventUpdate();
            SentEmptyCollisionsEvent = false;
        }

        public override void UnSubscribeEvents()
        {
            if (CollisionEventsThisFrame != null)
            {
                CollisionEventsThisFrame.Clear();
                CollisionEventsThisFrame = null;
            }
            m_eventsubscription = 0;
        }

        public override void AddCollisionEvent(uint CollidedWith, ContactPoint contact)
        {
            if (CollisionEventsThisFrame == null)
                CollisionEventsThisFrame = new CollisionEventUpdate();
            CollisionEventsThisFrame.AddCollider(CollidedWith, contact);
            _parent_scene.AddCollisionEventReporting(this);
        }

        public void SendCollisions()
        {
            if (CollisionEventsThisFrame == null)
                return;

            if (m_cureventsubscription < m_eventsubscription)
                return;

            m_cureventsubscription = 0;

            int ncolisions = CollisionEventsThisFrame.m_objCollisionList.Count;

            if (!SentEmptyCollisionsEvent || ncolisions > 0)
            {
                base.SendCollisionUpdate(CollisionEventsThisFrame);

                if (ncolisions == 0)
                {
                    SentEmptyCollisionsEvent = true;
                    _parent_scene.RemoveCollisionEventReporting(this);
                }
                else
                {
                    SentEmptyCollisionsEvent = false;
                    CollisionEventsThisFrame.Clear();
                }
            }           
        }

        internal void AddCollisionFrameTime(int t)
        {
            // protect it from overflow crashing
            if (m_cureventsubscription < 50000)
                m_cureventsubscription += t;
        }

        public override bool SubscribedEvents()
        {
            if (m_eventsubscription > 0)
                return true;
            return false;
        }

        private void changePhysicsStatus(bool NewStatus)
        {
            if (NewStatus != m_isPhysical)
            {
                if (NewStatus)
                {
                    AvatarGeomAndBodyDestroy();

                    AvatarGeomAndBodyCreation(_position.X, _position.Y, _position.Z);

                    _parent_scene.actor_name_map[topbox] = (PhysicsActor)this;
                    _parent_scene.actor_name_map[midbox] = (PhysicsActor)this;
                    _parent_scene.actor_name_map[feetbox] = (PhysicsActor)this;
                    _parent_scene.actor_name_map[bonebox] = (PhysicsActor)this;
                    _parent_scene.AddCharacter(this);
                }
                else
                {
                    _parent_scene.RemoveCollisionEventReporting(this);
                    _parent_scene.RemoveCharacter(this);
                    // destroy avatar capsule and related ODE data
                    AvatarGeomAndBodyDestroy();
                }
                m_freemove = false;
                m_isPhysical = NewStatus;
            }
        }

        private void changeAdd()
        {
            changePhysicsStatus(true);
        }

        private void changeRemove()
        {
            changePhysicsStatus(false);
        }

        private void changeShape(PrimitiveBaseShape arg)
        {
        }

        private void changeAvatarSize(strAvatarSize st)
        {
            m_feetOffset = st.offset;
            changeSize(st.size);
        }

        private void changeSize(Vector3 pSize)
        {
            if (pSize.IsFinite())
            {
                // for now only look to Z changes since viewers also don't change X and Y
                if (pSize.Z != m_size.Z)
                {
                    AvatarGeomAndBodyDestroy();


                    float oldsz = m_size.Z;
                    m_size = pSize;


                    AvatarGeomAndBodyCreation(_position.X, _position.Y,
                                      _position.Z + (m_size.Z - oldsz) * 0.5f);

                    Velocity = Vector3.Zero;

                    _parent_scene.actor_name_map[topbox] = (PhysicsActor)this;
                    _parent_scene.actor_name_map[midbox] = (PhysicsActor)this;
                    _parent_scene.actor_name_map[feetbox] = (PhysicsActor)this;
                    _parent_scene.actor_name_map[bonebox] = (PhysicsActor)this;
                }
                m_freemove = false;
                m_pidControllerActive = true;
            }
            else
            {
                m_log.Warn("[PHYSICS]: Got a NaN Size from Scene on a Character");
            }
        }

        private void changePosition( Vector3 newPos)
            {
                if (Body != IntPtr.Zero)
                    d.BodySetPosition(Body, newPos.X, newPos.Y, newPos.Z);
                _position = newPos;
                m_freemove = false;
                m_pidControllerActive = true;               
            }

        private void changeOrientation(Quaternion newOri)
        {
            d.Quaternion myrot = new d.Quaternion();
            myrot.X = newOri.X;
            myrot.Y = newOri.Y;
            myrot.Z = newOri.Z;
            myrot.W = newOri.W;
            float t = d.JointGetAMotorAngle(Amotor, 2);
            d.BodySetQuaternion(Body,ref myrot);
            m_orientation = newOri;
        }

        private void changeVelocity(Vector3 newVel)
        {
            m_pidControllerActive = true;
            m_freemove = false;
            _target_velocity = newVel;
        }

        private void changeSetTorque(Vector3 newTorque)
        {
        }                                 

        private void changeAddForce(Vector3 newForce)
        {
        }                                 

        private void changeAddAngularForce(Vector3 arg)
        {
        }                                 

        private void changeAngularLock(Vector3 arg)
        {
        }                                 

        private void changeFloatOnWater(bool arg)
        {
        }                                 

        private void changeVolumedetetion(bool arg)
        {
        }                                 

        private void changeSelectedStatus(bool arg)
        {
        }                                 

        private void changeDisable(bool arg)
        {
        }                                 

        private void changeBuilding(bool arg)
        {
        }

        private void setFreeMove()
        {
            m_pidControllerActive = true;
            _zeroFlag = false;
            _target_velocity = Vector3.Zero;
            m_freemove = true;
            m_colliderfilter = -2;
            m_colliderObjectfilter = -2;
            m_colliderGroundfilter = -2;

            m_iscolliding = false;
            m_iscollidingGround = false;
            m_iscollidingObj = false;

            CollisionEventsThisFrame.Clear();
        }

        private void changeForce(Vector3 newForce)
        {
            setFreeMove();

            if (Body != IntPtr.Zero)
            {
                if (newForce.X != 0f || newForce.Y != 0f || newForce.Z != 0)
                    d.BodyAddForce(Body, newForce.X, newForce.Y, newForce.Z);
            }
        }

        // for now momentum is actually velocity
        private void changeMomentum(Vector3 newmomentum)
        {
            _velocity = newmomentum;
            setFreeMove();

            if (Body != IntPtr.Zero)
                d.BodySetLinearVel(Body, newmomentum.X, newmomentum.Y, newmomentum.Z);
        }

        private void donullchange()
        {
        }

        public bool DoAChange(changes what, object arg)
        {
            if (topbox == IntPtr.Zero && what != changes.Add && what != changes.Remove)
            {
                return false;
            }

            // nasty switch
            switch (what)
            {
                case changes.Add:
                    changeAdd();
                    break;
                case changes.Remove:
                    changeRemove();
                    break;

                case changes.Position:
                    changePosition((Vector3)arg);
                    break;

                case changes.Orientation:
                    changeOrientation((Quaternion)arg);
                    break;

                case changes.PosOffset:
                    donullchange();
                    break;

                case changes.OriOffset:
                    donullchange();
                    break;

                case changes.Velocity:
                    changeVelocity((Vector3)arg);
                    break;

                //                case changes.Acceleration:
                //                    changeacceleration((Vector3)arg);
                //                    break;
                //                case changes.AngVelocity:
                //                    changeangvelocity((Vector3)arg);
                //                    break;

                case changes.Force:
                    changeForce((Vector3)arg);
                    break;

                case changes.Torque:
                    changeSetTorque((Vector3)arg);
                    break;

                case changes.AddForce:
                    changeAddForce((Vector3)arg);
                    break;

                case changes.AddAngForce:
                    changeAddAngularForce((Vector3)arg);
                    break;

                case changes.AngLock:
                    changeAngularLock((Vector3)arg);
                    break;

                case changes.Size:
                    changeSize((Vector3)arg);
                    break;

                case changes.AvatarSize:
                    changeAvatarSize((strAvatarSize)arg);
                    break;

                case changes.Momentum:
                    changeMomentum((Vector3)arg);
                    break;
/* not in use for now
                case changes.Shape:
                    changeShape((PrimitiveBaseShape)arg);
                    break;

                case changes.CollidesWater:
                    changeFloatOnWater((bool)arg);
                    break;

                case changes.VolumeDtc:
                    changeVolumedetetion((bool)arg);
                    break;

                case changes.Physical:
                    changePhysicsStatus((bool)arg);
                    break;

                case changes.Selected:
                    changeSelectedStatus((bool)arg);
                    break;

                case changes.disabled:
                    changeDisable((bool)arg);
                    break;

                case changes.building:
                    changeBuilding((bool)arg);
                    break;
*/
                case changes.Null:
                    donullchange();
                    break;

                default:
                    donullchange();
                    break;
            }
            return false;
        }

        public void AddChange(changes what, object arg)
        {
            _parent_scene.AddChange((PhysicsActor)this, what, arg);
        }

        private struct strAvatarSize
        {
            public Vector3 size;
            public float offset;
        }

    }
}
