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


// Revision by Ubit 2011

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
        private float m_mass = 80f;
        public float m_density = 60f;
        private bool m_pidControllerActive = true;
        public float PID_D = 800.0f;
        public float PID_P = 900.0f;
        //private static float POSTURE_SERVO = 10000.0f;
        public float CAPSULE_RADIUS = 0.37f;
        public float CAPSULE_LENGTH = 2.140599f;
        public float walkDivisor = 1.3f;
        public float runDivisor = 0.8f;
        private bool flying = false;
        private bool m_iscolliding = false;
        private bool m_iscollidingGround = false;
        private bool m_iscollidingObj = false;
        private bool m_alwaysRun = false;
        private int m_requestedUpdateFrequency = 0;
        private Vector3 m_taintPosition = Vector3.Zero;
        private bool m_hasTaintPosition = false;
        public uint m_localID = 0;
        public bool m_returnCollisions = false;
        // taints and their non-tainted counterparts
        public bool m_isPhysical = false; // the current physical status
        public bool m_tainted_isPhysical = false; // set when the physical status is tainted (false=not existing in physics engine, true=existing)
        public float MinimumGroundFlightOffset = 3f;


        private Vector3 m_taintForce;
        private bool m_hasTaintForce;
        private float m_tainted_CAPSULE_LENGTH; // set when the capsule length changes. 


        private float m_buoyancy = 0f;

        // private CollisionLocker ode;

        private string m_name = String.Empty;
        // other filter control
        int m_colliderfilter = 0;
        //        int m_colliderGroundfilter = 0;
        int m_colliderObjectfilter = 0;

        // Default we're a Character
        private CollisionCategories m_collisionCategories = (CollisionCategories.Character);

        // Default, Collide with Other Geometries, spaces, bodies and characters.
        private CollisionCategories m_collisionFlags = (CollisionCategories.Geom
                                                        | CollisionCategories.Space
                                                        | CollisionCategories.Body
                                                        | CollisionCategories.Character
                                                        );
        // we do land collisions not ode                                                       | CollisionCategories.Land);
        public IntPtr Body = IntPtr.Zero;
        private OdeScene _parent_scene;
        public IntPtr Shell = IntPtr.Zero;
        public IntPtr Amotor = IntPtr.Zero;
        public d.Mass ShellMass;
        public bool collidelock = false;

        private bool m_haseventsubscription = false;
        public int m_eventsubscription = 0;
        private CollisionEventUpdate CollisionEventsThisFrame = new CollisionEventUpdate();

        // unique UUID of this character object
        public UUID m_uuid;
        public bool bad = false;

        public ContactData AvatarContactData = new ContactData(10f, 0.3f);

        public OdeCharacter(String avName, OdeScene parent_scene, Vector3 pos, Vector3 size, float pid_d, float pid_p, float capsule_radius, float density, float walk_divisor, float rundivisor)
        {
            m_uuid = UUID.Random();

            m_hasTaintPosition = false;

            if (pos.IsFinite())
            {
                if (pos.Z > 9999999f)
                {
                    pos.Z = parent_scene.GetTerrainHeightAtXY(127, 127) + 5;
                }
                if (pos.Z < -90000f)
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

            PID_D = pid_d;
            PID_P = pid_p;
            CAPSULE_RADIUS = capsule_radius;
            m_density = density;
            m_mass = 80f; // sure we have a default

            AvatarContactData.mu = parent_scene.AvatarFriction;
            AvatarContactData.bounce = parent_scene.AvatarBounce;

            walkDivisor = walk_divisor;
            runDivisor = rundivisor;

            CAPSULE_LENGTH = size.Z * 1.15f - CAPSULE_RADIUS * 2.0f;
            //m_log.Info("[SIZE]: " + CAPSULE_LENGTH.ToString());
            m_tainted_CAPSULE_LENGTH = CAPSULE_LENGTH;

            m_isPhysical = false; // current status: no ODE information exists
            m_tainted_isPhysical = true; // new tainted status: need to create ODE information

            m_hasTaintForce = false;
            _parent_scene.AddPhysicsActorTaint(this);

            m_name = avName;
        }

        public override int PhysicsActorType
        {
            get { return (int)ActorTypes.Agent; }
            set { return; }
        }

        public override ContactData ContactData
        {
            get { return AvatarContactData; }
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
            set { m_localID = value; }
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
            get { return false; }
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
//                    SetPidStatus(false);
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
/*
                if (m_iscollidingObj)
                    m_pidControllerActive = false;
                else
                    m_pidControllerActive = true;
 */
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
                if (Body == IntPtr.Zero || Shell == IntPtr.Zero)
                {
                    if (value.IsFinite())
                    {
                        if (value.Z > 9999999f)
                        {
                            value.Z = _parent_scene.GetTerrainHeightAtXY(127, 127) + 5;
                        }
                        if (value.Z < -90000f)
                        {
                            value.Z = _parent_scene.GetTerrainHeightAtXY(127, 127) + 5;
                        }

                        m_taintPosition.X = value.X;
                        m_taintPosition.Y = value.Y;
                        m_taintPosition.Z = value.Z;
                        m_hasTaintPosition = true;
                        _parent_scene.AddPhysicsActorTaint(this);
                    }
                    else
                    {
                        m_log.Warn("[PHYSICS]: Got a NaN Position from Scene on a Character");
                    }
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
            get {
                float d = CAPSULE_RADIUS * 2;
                return new Vector3(d, d, (CAPSULE_LENGTH +d)/1.15f); }
            set
            {
                if (value.IsFinite())
                {
                    m_pidControllerActive = true;

                    Vector3 SetSize = value;
                    m_tainted_CAPSULE_LENGTH = SetSize.Z *1.15f - CAPSULE_RADIUS * 2.0f;
                    _parent_scene.AddPhysicsActorTaint(this);
                }
                else
                {
                    m_log.Warn("[PHYSICS]: Got a NaN Size from Scene on a Character");
                }
            }
        }

        /// <summary>
        /// This creates the Avatar's physical Surrogate at the position supplied
        /// </summary>
        /// <param name="npositionX"></param>
        /// <param name="npositionY"></param>
        /// <param name="npositionZ"></param>

        // WARNING: This MUST NOT be called outside of ProcessTaints, else we can have unsynchronized access
        // to ODE internals. ProcessTaints is called from within thread-locked Simulate(), so it is the only 
        // place that is safe to call this routine AvatarGeomAndBodyCreation.
        private void AvatarGeomAndBodyCreation(float npositionX, float npositionY, float npositionZ)
        {
            _parent_scene.waitForSpaceUnlock(_parent_scene.ActiveSpace);
            if (CAPSULE_LENGTH <= 0)
            {
                m_log.Warn("[PHYSICS]: The capsule size you specified in opensim.ini is invalid!  Setting it to the smallest possible size!");
                CAPSULE_LENGTH = 0.01f;

            }

            if (CAPSULE_RADIUS <= 0)
            {
                m_log.Warn("[PHYSICS]: The capsule size you specified in opensim.ini is invalid!  Setting it to the smallest possible size!");
                CAPSULE_RADIUS = 0.01f;

            }
            Shell = d.CreateCapsule(_parent_scene.ActiveSpace, CAPSULE_RADIUS, CAPSULE_LENGTH);

            d.GeomSetCategoryBits(Shell, (int)m_collisionCategories);
            d.GeomSetCollideBits(Shell, (int)m_collisionFlags);

            d.MassSetCapsule(out ShellMass, m_density, 3, CAPSULE_RADIUS, CAPSULE_LENGTH);

            m_mass = ShellMass.mass;  // update mass

            // rescale PID parameters 
            PID_D = _parent_scene.avPIDD;
            PID_P = _parent_scene.avPIDP;

            // rescale PID parameters so that this aren't affected by mass
            // and so don't get unstable for some masses
            // also scale by ode time step so you don't need to refix them

            PID_D /= 50 * 80; //scale to original mass of around 80 and 50 ODE fps
            PID_D *= m_mass / _parent_scene.ODE_STEPSIZE;
            PID_P /= 50 * 80;
            PID_P *= m_mass / _parent_scene.ODE_STEPSIZE;

            Body = d.BodyCreate(_parent_scene.world);

            d.BodySetAutoDisableFlag(Body, false);
            d.BodySetPosition(Body, npositionX, npositionY, npositionZ);

            _position.X = npositionX;
            _position.Y = npositionY;
            _position.Z = npositionZ;

            m_hasTaintPosition = false;

            d.BodySetMass(Body, ref ShellMass);
            d.GeomSetBody(Shell, Body);

            // The purpose of the AMotor here is to keep the avatar's physical
            // surrogate from rotating while moving
            Amotor = d.JointCreateAMotor(_parent_scene.world, IntPtr.Zero);
            d.JointAttach(Amotor, Body, IntPtr.Zero);

            d.JointSetAMotorMode(Amotor, 0);
            d.JointSetAMotorNumAxes(Amotor, 3);
            d.JointSetAMotorAxis(Amotor, 0, 0 , 1, 0, 0);
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

            d.JointSetAMotorParam(Amotor, (int)dParam.FMax, 5e6f);
            d.JointSetAMotorParam(Amotor, (int)dParam.FMax2, 5e6f);
            d.JointSetAMotorParam(Amotor, (int)dParam.FMax3, 5e6f);
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

            //kill the Geometry
            if (Shell != IntPtr.Zero)
            {
                _parent_scene.geom_name_map.Remove(Shell);
                _parent_scene.waitForSpaceUnlock(_parent_scene.ActiveSpace);
                d.GeomDestroy(Shell);
                _parent_scene.geom_name_map.Remove(Shell);
                Shell = IntPtr.Zero;
            }
        }
        //
        /// <summary>
        /// Uses the capped cyllinder volume formula to calculate the avatar's mass.
        /// This may be used in calculations in the scene/scenepresence
        /// </summary>
        public override float Mass
        {
            get
            {
                float AVvolume = (float)(Math.PI * CAPSULE_RADIUS * CAPSULE_RADIUS * (1.3333333333f * CAPSULE_RADIUS + CAPSULE_LENGTH));
                return m_density * AVvolume;
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

        //UBit mess
        /* for later use
                public override Vector3 PrimOOBsize
                    {
                    get
                        {
                        Vector3 s=Size;
                        s.X *=0.5f;
                        s.Y *=0.5f;
                        s.Z *=0.5f;
                        return s;
                        }
                    }
 
                public override Vector3 PrimOOBoffset
                    {
                    get
                        {
                        return Vector3.Zero;
                        }
                    }
        */

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
                    m_pidControllerActive = true;
                    _target_velocity = value;
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
            get { return Quaternion.Identity; }
            set
            {
                //Matrix3 or = Orientation.ToRotationMatrix();
                //d.Matrix3 ord = new d.Matrix3(or.m00, or.m10, or.m20, or.m01, or.m11, or.m21, or.m02, or.m12, or.m22);
                //d.BodySetRotation(Body, ref ord);
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
                    m_pidControllerActive = false;
                    m_taintForce = force / _parent_scene.ODE_STEPSIZE;
                    m_hasTaintForce = true;
                    _parent_scene.AddPhysicsActorTaint(this);
                }
                else
                {
                    m_pidControllerActive = true;
                    _target_velocity.X += force.X;
                    _target_velocity.Y += force.Y;
                    _target_velocity.Z += force.Z;
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
        }


        /// <summary>
        /// Called from Simulate
        /// This is the avatar's movement control + PID Controller
        /// </summary>
        /// <param name="timeStep"></param>
        public void Move(float timeStep, List<OdeCharacter> defects)
        {
            //  no lock; for now it's only called from within Simulate()

            // If the PID Controller isn't active then we set our force
            // calculating base velocity to the current position

            if (Body == IntPtr.Zero)
                return;

            d.Vector3 dtmp;
            d.BodyCopyPosition(Body, out dtmp);
            Vector3 localpos = new Vector3(dtmp.X, dtmp.Y, dtmp.Z);

            // the Amotor still lets avatar rotation to drift during colisions
            // so force it back to identity
            
            d.Quaternion qtmp;
            qtmp.W = 1;
            qtmp.X = 0;
            qtmp.Y = 0;
            qtmp.Z = 0;
            d.BodySetQuaternion(Body, ref qtmp);

            if (m_pidControllerActive == false)
            {
                _zeroPosition = localpos;
            }
            //PidStatus = true;


            if (!localpos.IsFinite())
            {

                m_log.Warn("[PHYSICS]: Avatar Position is non-finite!");
                defects.Add(this);
                // _parent_scene.RemoveCharacter(this);

                // destroy avatar capsule and related ODE data
                AvatarGeomAndBodyDestroy();

                return;
            }

            Vector3 vec = Vector3.Zero;
            dtmp = d.BodyGetLinearVel(Body);
            Vector3 vel = new Vector3(dtmp.X, dtmp.Y, dtmp.Z);

            float movementdivisor = 1f;
            //Ubit change divisions into multiplications below
            if (!m_alwaysRun)
            {
                movementdivisor = 1 / walkDivisor;
            }
            else
            {
                movementdivisor = 1 / runDivisor;
            }

            // colide with land

            d.AABB aabb;
            d.GeomGetAABB(Shell, out aabb);
            float chrminZ = aabb.MinZ;

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

                /*
                                Vector3 vtmp;
                                vtmp.X = _target_velocity.X * timeStep;
                                vtmp.Y = _target_velocity.Y * timeStep;
                                // fake and avoid squares
                                float k = (Math.Abs(vtmp.X) + Math.Abs(vtmp.Y));
                                if (k > 0)
                                    {
                                    posch.X += vtmp.X;
                                    posch.Y += vtmp.Y;
                                    terrainheight -= _parent_scene.GetTerrainHeightAtXY(posch.X, posch.Y);
                                    k = 1 + Math.Abs(terrainheight) / k;
                                    movementdivisor /= k;

                                    if (k < 1)
                                        k = 1;
                                    }
                */


                if (depth < 0.1f)
                {
                    m_iscolliding = true;
                    m_colliderfilter = 2;
                    m_iscollidingGround = true;

                    ContactPoint contact = new ContactPoint();
                    contact.PenetrationDepth = depth;
                    contact.Position.X = localpos.X;
                    contact.Position.Y = localpos.Y;
                    contact.Position.Z = chrminZ;
                    contact.SurfaceNormal.X = 0f;
                    contact.SurfaceNormal.Y = 0f;
                    contact.SurfaceNormal.Z = -1f;
                    AddCollisionEvent(0, contact);

                    vec.Z *= 0.5f;
                }

                else
                    m_iscollidingGround = false;
            }
            else
                m_iscollidingGround = false;


            //  if velocity is zero, use position control; otherwise, velocity control
            if (_target_velocity.X == 0.0f && _target_velocity.Y == 0.0f && _target_velocity.Z == 0.0f
                && m_iscolliding)
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
            }
        }

        /// <summary>
        /// Updates the reported position and velocity.  This essentially sends the data up to ScenePresence.
        /// </summary>
        public void UpdatePositionAndVelocity()
        {
            //  no lock; called from Simulate() -- if you call this from elsewhere, gotta lock or do Monitor.Enter/Exit!
            if (Body == IntPtr.Zero)
                return;

            d.Vector3 vec;
            try
            {
                d.BodyCopyPosition(Body, out vec);
            }
            catch (NullReferenceException)
            {
                bad = true;
                _parent_scene.BadCharacter(this);
                vec = new d.Vector3(_position.X, _position.Y, _position.Z);
                base.RaiseOutOfBounds(_position); // Tells ScenePresence that there's a problem!
                m_log.WarnFormat("[ODEPLUGIN]: Avatar Null reference for Avatar {0}, physical actor {1}", m_name, m_uuid);
            }

            _position.X = vec.X;
            _position.Y = vec.Y;
            _position.Z = vec.Z;

            bool fixbody = false;

            if (_position.X < 0.0f)
            {
                fixbody = true;
                _position.X = 0.1f;
            }
            else if (_position.X > (int)_parent_scene.WorldExtents.X - 0.1f)
            {
                fixbody = true;
                _position.X = (int)_parent_scene.WorldExtents.X - 0.1f;
            }

            if (_position.Y < 0.0f)
            {
                fixbody = true;
                _position.Y = 0.1f;
            }
            else if (_position.Y > (int)_parent_scene.WorldExtents.Y - 0.1)
            {
                fixbody = true;
                _position.Y = (int)_parent_scene.WorldExtents.Y - 0.1f;
            }

            if (fixbody)
                d.BodySetPosition(Body, _position.X, _position.Y, _position.Z);

            // Did we move last? = zeroflag
            // This helps keep us from sliding all over
/*
            if (_zeroFlag)
            {
                _velocity.X = 0.0f;
                _velocity.Y = 0.0f;
                _velocity.Z = 0.0f;

                // Did we send out the 'stopped' message?
                if (!m_lastUpdateSent)
                {
                    m_lastUpdateSent = true;
                    base.RequestPhysicsterseUpdate();
                }
            }
            else
            {
                m_lastUpdateSent = false;
 */
                try
                {
                    vec = d.BodyGetLinearVel(Body);
                }
                catch (NullReferenceException)
                {
                    vec.X = _velocity.X;
                    vec.Y = _velocity.Y;
                    vec.Z = _velocity.Z;
                }
                _velocity.X = (vec.X);
                _velocity.Y = (vec.Y);
                _velocity.Z = (vec.Z);
 //           }
        }

        /// <summary>
        /// Cleanup the things we use in the scene.
        /// </summary>
        public void Destroy()
        {
            m_tainted_isPhysical = false;
            _parent_scene.AddPhysicsActorTaint(this);
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
            m_requestedUpdateFrequency = ms;
            m_eventsubscription = ms;
            _parent_scene.AddCollisionEventReporting(this);
            m_haseventsubscription = true;
        }

        public override void UnSubscribeEvents()
        {
            m_haseventsubscription = false;
            _parent_scene.RemoveCollisionEventReporting(this);
            m_requestedUpdateFrequency = 0;
            m_eventsubscription = 0;
        }

        public void AddCollisionEvent(uint CollidedWith, ContactPoint contact)
        {
            if (m_haseventsubscription)
            {
                //                m_log.DebugFormat(
                //                    "[PHYSICS]: Adding collision event for {0}, collidedWith {1}, contact {2}", "", CollidedWith, contact);

                CollisionEventsThisFrame.AddCollider(CollidedWith, contact);
            }
        }

        public void SendCollisions()
        {
            if (m_haseventsubscription && m_eventsubscription > m_requestedUpdateFrequency)
            {
                if (CollisionEventsThisFrame != null)
                {
                    base.SendCollisionUpdate(CollisionEventsThisFrame);
                }
                CollisionEventsThisFrame = new CollisionEventUpdate();
                m_eventsubscription = 0;
            }
        }

        public override bool SubscribedEvents()
        {
            return m_haseventsubscription;
        }

        public void ProcessTaints(float timestep)
        {

            if (m_tainted_isPhysical != m_isPhysical)
            {
                if (m_tainted_isPhysical)
                {
                    // Create avatar capsule and related ODE data
                    if ((Shell != IntPtr.Zero))
                    {
                        // a lost shell ?
                        m_log.Warn("[PHYSICS]: re-creating the following avatar ODE data, even though it already exists - "
                            + (Shell != IntPtr.Zero ? "Shell " : "")
                            + (Body != IntPtr.Zero ? "Body " : "")
                            + (Amotor != IntPtr.Zero ? "Amotor " : ""));
                        AvatarGeomAndBodyDestroy();
                    }

                    AvatarGeomAndBodyCreation(_position.X, _position.Y, _position.Z);
                    _parent_scene.geom_name_map[Shell] = m_name;
                    _parent_scene.actor_name_map[Shell] = (PhysicsActor)this;
                    _parent_scene.AddCharacter(this);
                }
                else
                {
                    _parent_scene.RemoveCharacter(this);
                    // destroy avatar capsule and related ODE data
                    AvatarGeomAndBodyDestroy();
                }

                m_isPhysical = m_tainted_isPhysical;
            }

            if (m_tainted_CAPSULE_LENGTH != CAPSULE_LENGTH)
            {
                if (Shell != IntPtr.Zero && Body != IntPtr.Zero && Amotor != IntPtr.Zero)
                {
                    AvatarGeomAndBodyDestroy();

                    m_pidControllerActive = true;

                    float prevCapsule = CAPSULE_LENGTH;
                    CAPSULE_LENGTH = m_tainted_CAPSULE_LENGTH;
                    
                    AvatarGeomAndBodyCreation(_position.X, _position.Y,
                                      _position.Z + (Math.Abs(CAPSULE_LENGTH - prevCapsule) * 2));

                    Velocity = Vector3.Zero;

                    _parent_scene.geom_name_map[Shell] = m_name;
                    _parent_scene.actor_name_map[Shell] = (PhysicsActor)this;
                }
                else
                {
                    m_log.Warn("[PHYSICS]: trying to change capsule size, but the following ODE data is missing - "
                        + (Shell == IntPtr.Zero ? "Shell " : "")
                        + (Body == IntPtr.Zero ? "Body " : "")
                        + (Amotor == IntPtr.Zero ? "Amotor " : ""));
                }
            }

            if (m_hasTaintPosition)
            {
                if (Body != IntPtr.Zero)
                    d.BodySetPosition(Body, m_taintPosition.X, m_taintPosition.Y, m_taintPosition.Z);

                _position.X = m_taintPosition.X;
                _position.Y = m_taintPosition.Y;
                _position.Z = m_taintPosition.Z;
                m_hasTaintPosition = false;
            }

            if (m_hasTaintForce)
            {
                if (Body != IntPtr.Zero)
                {
                    if(m_taintForce.X !=0f || m_taintForce.Y !=0f || m_taintForce.Z !=0)
                        d.BodyAddForce(Body, m_taintForce.X, m_taintForce.Y, m_taintForce.Z);
                    m_hasTaintForce = false;
                }
            }

        }

        internal void AddCollisionFrameTime(int p)
        {
            // protect it from overflow crashing
            if (m_eventsubscription + p >= int.MaxValue)
                m_eventsubscription = 0;
            m_eventsubscription += p;
        }
    }
}
