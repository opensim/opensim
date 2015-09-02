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
using OpenMetaverse;
using Ode.NET;
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
        private d.Vector3 _zeroPosition;
        private bool _zeroFlag = false;
        private bool m_lastUpdateSent = false;
        private Vector3 _velocity;
        private Vector3 m_taintTargetVelocity;
        private Vector3 _target_velocity;
        private Vector3 _acceleration;
        private Vector3 m_rotationalVelocity;
        private float m_mass = 80f;
        private float m_density = 60f;
        private bool m_pidControllerActive = true;
        private float PID_D = 800.0f;
        private float PID_P = 900.0f;
        //private static float POSTURE_SERVO = 10000.0f;
        private float CAPSULE_RADIUS = 0.37f;
        private float CAPSULE_LENGTH = 2.140599f;
        private float m_tensor = 3800000f;
//        private float heightFudgeFactor = 0.52f;
        private float walkDivisor = 1.3f;
        private float runDivisor = 0.8f;
        private bool flying = false;
        private bool m_iscolliding = false;
        private bool m_iscollidingGround = false;
        private bool m_wascolliding = false;
        private bool m_wascollidingGround = false;
        private bool m_iscollidingObj = false;
        private bool m_alwaysRun = false;
        private bool m_hackSentFall = false;
        private bool m_hackSentFly = false;
        private int m_requestedUpdateFrequency = 0;
        private Vector3 m_taintPosition;
        internal bool m_avatarplanted = false;
        /// <summary>
        /// Hold set forces so we can process them outside physics calculations.  This prevents race conditions if we set force
        /// while calculatios are going on
        /// </summary>
        private Vector3 m_taintForce;

        // taints and their non-tainted counterparts
        private bool m_isPhysical = false; // the current physical status
        private bool m_tainted_isPhysical = false; // set when the physical status is tainted (false=not existing in physics engine, true=existing)
        internal float MinimumGroundFlightOffset = 3f;

        private float m_tainted_CAPSULE_LENGTH; // set when the capsule length changes.

        /// <summary>
        /// Base movement for calculating tilt.
        /// </summary>
        private float m_tiltBaseMovement = (float)Math.Sqrt(2);

        /// <summary>
        /// Used to introduce a fixed tilt because a straight-up capsule falls through terrain, probably a bug in terrain collider
        /// </summary>
        private float m_tiltMagnitudeWhenProjectedOnXYPlane = 0.1131371f;

        private float m_buoyancy = 0f;

        // private CollisionLocker ode;
        private bool[] m_colliderarr = new bool[11];
        private bool[] m_colliderGroundarr = new bool[11];

        // Default we're a Character
        private CollisionCategories m_collisionCategories = (CollisionCategories.Character);

        // Default, Collide with Other Geometries, spaces, bodies and characters.
        private CollisionCategories m_collisionFlags = (CollisionCategories.Geom
                                                        | CollisionCategories.Space
                                                        | CollisionCategories.Body
                                                        | CollisionCategories.Character
                                                        | CollisionCategories.Land);
        /// <summary>
        /// Body for dynamics simulation
        /// </summary>
        internal IntPtr Body { get; private set; }

        private OdeScene _parent_scene;

        /// <summary>
        /// Collision geometry
        /// </summary>
        internal IntPtr Shell { get; private set; }
        
        private IntPtr Amotor = IntPtr.Zero;
        private d.Mass ShellMass;

        private int m_eventsubscription = 0;
        private CollisionEventUpdate CollisionEventsThisFrame = new CollisionEventUpdate();

        // unique UUID of this character object
        internal UUID m_uuid { get; private set; }
        internal bool bad = false;

        /// <summary>
        /// ODE Avatar.
        /// </summary>
        /// <param name="avName"></param>
        /// <param name="parent_scene"></param>
        /// <param name="pos"></param>
        /// <param name="vel"></param>
        /// <param name="size"></param>
        /// <param name="pid_d"></param>
        /// <param name="pid_p"></param>
        /// <param name="capsule_radius"></param>
        /// <param name="tensor"></param>
        /// <param name="density">
        /// Only used right now to return information to LSL.  Not actually used to set mass in ODE!
        /// </param>
        /// <param name="walk_divisor"></param>
        /// <param name="rundivisor"></param>
        public OdeCharacter(
            String avName, OdeScene parent_scene, Vector3 pos, Vector3 vel, Vector3 size, float pid_d, float pid_p,
            float capsule_radius, float tensor, float density,
            float walk_divisor, float rundivisor)
        {
            m_uuid = UUID.Random();

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
                m_taintPosition = pos;
            }
            else
            {
                _position
                    = new Vector3(
                        (float)_parent_scene.WorldExtents.X * 0.5f,
                        (float)_parent_scene.WorldExtents.Y * 0.5f,
                        parent_scene.GetTerrainHeightAtXY(128f, 128f) + 10f);
                m_taintPosition = _position;

                m_log.WarnFormat("[ODE CHARACTER]: Got NaN Position on Character Create for {0}", avName);
            }

            _velocity = vel;
            m_taintTargetVelocity = vel;

            _parent_scene = parent_scene;

            PID_D = pid_d;
            PID_P = pid_p;
            CAPSULE_RADIUS = capsule_radius;
            m_tensor = tensor;
            m_density = density;
//            heightFudgeFactor = height_fudge_factor;
            walkDivisor = walk_divisor;
            runDivisor = rundivisor;

            // m_StandUpRotation =
            //     new d.Matrix3(0.5f, 0.7071068f, 0.5f, -0.7071068f, 0f, 0.7071068f, 0.5f, -0.7071068f,
            //                   0.5f);

            // We can set taint and actual to be the same here, since the entire character will be set up when the
            // m_tainted_isPhysical is processed.
            SetTaintedCapsuleLength(size);
            CAPSULE_LENGTH = m_tainted_CAPSULE_LENGTH;

            m_isPhysical = false; // current status: no ODE information exists
            m_tainted_isPhysical = true; // new tainted status: need to create ODE information

            _parent_scene.AddPhysicsActorTaint(this);
            
            Name = avName;
        }

        public override int PhysicsActorType
        {
            get { return (int) ActorTypes.Agent; }
            set { return; }
        }

        /// <summary>
        /// If this is set, the avatar will move faster
        /// </summary>
        public override bool SetAlwaysRun
        {
            get { return m_alwaysRun; }
            set { m_alwaysRun = value; }
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
//                m_log.DebugFormat("[ODE CHARACTER]: Set OdeCharacter Flying to {0}", flying);
            }
        }

        /// <summary>
        /// Returns if the avatar is colliding in general.
        /// This includes the ground and objects and avatar.
        /// </summary>
        public override bool IsColliding
        {
            get { return m_iscolliding; }
            set
            {
                int i;
                int truecount = 0;
                int falsecount = 0;

                if (m_colliderarr.Length >= 10)
                {
                    for (i = 0; i < 10; i++)
                    {
                        m_colliderarr[i] = m_colliderarr[i + 1];
                    }
                }
                m_colliderarr[10] = value;

                for (i = 0; i < 11; i++)
                {
                    if (m_colliderarr[i])
                    {
                        truecount++;
                    }
                    else
                    {
                        falsecount++;
                    }
                }

                // Equal truecounts and false counts means we're colliding with something.

                if (falsecount > 1.2*truecount)
                {
                    m_iscolliding = false;
                }
                else
                {
                    m_iscolliding = true;
                }

                if (m_wascolliding != m_iscolliding)
                {
                    //base.SendCollisionUpdate(new CollisionEventUpdate());
                }

                m_wascolliding = m_iscolliding;
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
                // Collisions against the ground are not really reliable
                // So, to get a consistant value we have to average the current result over time
                // Currently we use 1 second = 10 calls to this.
                int i;
                int truecount = 0;
                int falsecount = 0;

                if (m_colliderGroundarr.Length >= 10)
                {
                    for (i = 0; i < 10; i++)
                    {
                        m_colliderGroundarr[i] = m_colliderGroundarr[i + 1];
                    }
                }
                m_colliderGroundarr[10] = value;

                for (i = 0; i < 11; i++)
                {
                    if (m_colliderGroundarr[i])
                    {
                        truecount++;
                    }
                    else
                    {
                        falsecount++;
                    }
                }

                // Equal truecounts and false counts means we're colliding with something.

                if (falsecount > 1.2*truecount)
                {
                    m_iscollidingGround = false;
                }
                else
                {
                    m_iscollidingGround = true;
                }
                if (m_wascollidingGround != m_iscollidingGround)
                {
                    //base.SendCollisionUpdate(new CollisionEventUpdate());
                }
                m_wascollidingGround = m_iscollidingGround;
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
                m_iscollidingObj = value;
                if (value && !m_avatarplanted)
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

                        m_taintPosition = value;                        
                        _parent_scene.AddPhysicsActorTaint(this);
                    }
                    else
                    {
                        m_log.WarnFormat("[ODE CHARACTER]: Got a NaN Position from Scene on character {0}", Name);
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
            get { return new Vector3(CAPSULE_RADIUS * 2, CAPSULE_RADIUS * 2, CAPSULE_LENGTH); }
            set
            {
                SetTaintedCapsuleLength(value);

                    // If we reset velocity here, then an avatar stalls when it crosses a border for the first time
                    // (as the height of the new root agent is set).
//                    Velocity = Vector3.Zero;

                _parent_scene.AddPhysicsActorTaint(this);
            }
        }

        private void SetTaintedCapsuleLength(Vector3 size)
        {
            if (size.IsFinite())
            {
                m_pidControllerActive = true;

                m_tainted_CAPSULE_LENGTH = size.Z - CAPSULE_RADIUS * 2.0f;

                // m_log.InfoFormat("[ODE CHARACTER]: Size = {0}, Capsule Length = {1} (Capsule Radius = {2})",
                //     size, m_tainted_CAPSULE_LENGTH, CAPSULE_RADIUS);
            }
            else
            {
                m_log.WarnFormat("[ODE CHARACTER]: Got a NaN Size for {0} in {1}", Name, _parent_scene.Name);
            }
        }

        private void AlignAvatarTiltWithCurrentDirectionOfMovement(Vector3 movementVector)
        {
            movementVector.Z = 0f;
            float magnitude = (float)Math.Sqrt((double)(movementVector.X * movementVector.X + movementVector.Y * movementVector.Y));
            if (magnitude < 0.1f) return;

            // normalize the velocity vector
            float invMagnitude = 1.0f / magnitude;
            movementVector.X *= invMagnitude;
            movementVector.Y *= invMagnitude;

            // if we change the capsule heading too often, the capsule can fall down
            // therefore we snap movement vector to just 1 of 4 predefined directions (ne, nw, se, sw),
            // meaning only 4 possible capsule tilt orientations
            if (movementVector.X > 0)
            {
                // east
                if (movementVector.Y > 0)
                {
                    // northeast
                    movementVector.X = m_tiltBaseMovement;
                    movementVector.Y = m_tiltBaseMovement;
                }
                else
                {
                    // southeast
                    movementVector.X = m_tiltBaseMovement;
                    movementVector.Y = -m_tiltBaseMovement;
                }
            }
            else
            {
                // west
                if (movementVector.Y > 0)
                {
                    // northwest
                    movementVector.X = -m_tiltBaseMovement;
                    movementVector.Y = m_tiltBaseMovement;
                }
                else
                {
                    // southwest
                    movementVector.X = -m_tiltBaseMovement;
                    movementVector.Y = -m_tiltBaseMovement;
                }
            }

            // movementVector.Z is zero

            // calculate tilt components based on desired amount of tilt and current (snapped) heading.
            // the "-" sign is to force the tilt to be OPPOSITE the direction of movement.
            float xTiltComponent = -movementVector.X * m_tiltMagnitudeWhenProjectedOnXYPlane;
            float yTiltComponent = -movementVector.Y * m_tiltMagnitudeWhenProjectedOnXYPlane;

            //m_log.Debug("[ODE CHARACTER]: changing avatar tilt");
            d.JointSetAMotorParam(Amotor, (int)dParam.LowStop, xTiltComponent);
            d.JointSetAMotorParam(Amotor, (int)dParam.HiStop, xTiltComponent); // must be same as lowstop, else a different, spurious tilt is introduced
            d.JointSetAMotorParam(Amotor, (int)dParam.LoStop2, yTiltComponent);
            d.JointSetAMotorParam(Amotor, (int)dParam.HiStop2, yTiltComponent); // same as lowstop
            d.JointSetAMotorParam(Amotor, (int)dParam.LoStop3, 0f);
            d.JointSetAMotorParam(Amotor, (int)dParam.HiStop3, 0f); // same as lowstop
        }

        /// <summary>
        /// Uses the capped cyllinder volume formula to calculate the avatar's mass.
        /// This may be used in calculations in the scene/scenepresence
        /// </summary>
        public override float Mass
        {
            get
            {
                float AVvolume = (float)(Math.PI * Math.Pow(CAPSULE_RADIUS, 2) * CAPSULE_LENGTH);
                return m_density * AVvolume;
            }
        }

        public override void link(PhysicsActor obj) {}

        public override void delink() {}

        public override void LockAngularMotion(Vector3 axis) {}

//      This code is very useful. Written by DanX0r. We're just not using it right now.
//      Commented out to prevent a warning.
//
//         private void standupStraight()
//         {
//             // The purpose of this routine here is to quickly stabilize the Body while it's popped up in the air.
//             // The amotor needs a few seconds to stabilize so without it, the avatar shoots up sky high when you
//             // change appearance and when you enter the simulator
//             // After this routine is done, the amotor stabilizes much quicker
//             d.Vector3 feet;
//             d.Vector3 head;
//             d.BodyGetRelPointPos(Body, 0.0f, 0.0f, -1.0f, out feet);
//             d.BodyGetRelPointPos(Body, 0.0f, 0.0f, 1.0f, out head);
//             float posture = head.Z - feet.Z;

//             // restoring force proportional to lack of posture:
//             float servo = (2.5f - posture) * POSTURE_SERVO;
//             d.BodyAddForceAtRelPos(Body, 0.0f, 0.0f, servo, 0.0f, 0.0f, 1.0f);
//             d.BodyAddForceAtRelPos(Body, 0.0f, 0.0f, -servo, 0.0f, 0.0f, -1.0f);
//             //d.Matrix3 bodyrotation = d.BodyGetRotation(Body);
//             //m_log.Info("[PHYSICSAV]: Rotation: " + bodyrotation.M00 + " : " + bodyFArotation.M01 + " : " + bodyrotation.M02 + " : " + bodyrotation.M10 + " : " + bodyrotation.M11 + " : " + bodyrotation.M12 + " : " + bodyrotation.M20 + " : " + bodyrotation.M21 + " : " + bodyrotation.M22);
//         }

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

        public override Vector3 TargetVelocity
        {
            get
            {
                return m_taintTargetVelocity;
            }

            set
            {
                Velocity = value;
            }
        }


        public override Vector3 Velocity
        {
            get
            {
                // There's a problem with Vector3.Zero! Don't Use it Here!
                if (_zeroFlag)
                    return Vector3.Zero;
                m_lastUpdateSent = false;
                return _velocity;
            }

            set
            {
                if (value.IsFinite())
                {
                    m_pidControllerActive = true;
                    m_taintTargetVelocity = value;
                    _parent_scene.AddPhysicsActorTaint(this);
                }
                else
                {
                    m_log.WarnFormat("[ODE CHARACTER]: Got a NaN velocity from Scene for {0}", Name);
                }

//                m_log.DebugFormat("[PHYSICS]: Set target velocity of {0}", m_taintTargetVelocity);
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
            set {
                //Matrix3 or = Orientation.ToRotationMatrix();
                //d.Matrix3 ord = new d.Matrix3(or.m00, or.m10, or.m20, or.m01, or.m11, or.m21, or.m02, or.m12, or.m22);
                //d.BodySetRotation(Body, ref ord);
            }
        }

        public override Vector3 Acceleration
        {
            get { return _acceleration; }
            set { _acceleration = value; }
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
                    force *= 100f;
                    m_taintForce += force;
                    _parent_scene.AddPhysicsActorTaint(this);

                    // If uncommented, things get pushed off world
                    //
                    // m_log.Debug("Push!");
                    // m_taintTargetVelocity.X += force.X;
                    // m_taintTargetVelocity.Y += force.Y;
                    // m_taintTargetVelocity.Z += force.Z;
                }
                else
                {
                    m_pidControllerActive = true;
                    m_taintTargetVelocity += force;
                }
            }
            else
            {
                m_log.WarnFormat("[ODE CHARACTER]: Got a NaN force applied to {0}", Name);
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
        /// <param name="defects">The character will be added to this list if there is something wrong (non-finite
        /// position or velocity).
        /// </param>
        internal void Move(List<OdeCharacter> defects)
        {
            //  no lock; for now it's only called from within Simulate()
            
            // If the PID Controller isn't active then we set our force
            // calculating base velocity to the current position

            if (Body == IntPtr.Zero)
                return;

            if (m_pidControllerActive == false)
            {
                _zeroPosition = d.BodyGetPosition(Body);
            }
            //PidStatus = true;

            d.Vector3 localpos = d.BodyGetPosition(Body);
            Vector3 localPos = new Vector3(localpos.X, localpos.Y, localpos.Z);
            
            if (!localPos.IsFinite())
            {
                m_log.WarnFormat(
                    "[ODE CHARACTER]: Avatar position of {0} for {1} is non-finite!  Removing from physics scene.",
                    localPos, Name);

                defects.Add(this);

                return;
            }

            Vector3 vec = Vector3.Zero;
            d.Vector3 vel = d.BodyGetLinearVel(Body);

//            m_log.DebugFormat(
//                "[ODE CHARACTER]: Current velocity in Move() is <{0},{1},{2}>, target {3} for {4}",
//                vel.X, vel.Y, vel.Z, _target_velocity, Name);

            float movementdivisor = 1f;

            if (!m_alwaysRun)
            {
                movementdivisor = walkDivisor;
            }
            else
            {
                movementdivisor = runDivisor;
            }

            //  if velocity is zero, use position control; otherwise, velocity control
            if (_target_velocity.X == 0.0f && _target_velocity.Y == 0.0f && _target_velocity.Z == 0.0f && m_iscolliding)
            {
                //  keep track of where we stopped.  No more slippin' & slidin'
                if (!_zeroFlag)
                {
                    _zeroFlag = true;
                    _zeroPosition = d.BodyGetPosition(Body);
                }

                if (m_pidControllerActive)
                {
                    // We only want to deactivate the PID Controller if we think we want to have our surrogate
                    // react to the physics scene by moving it's position.
                    // Avatar to Avatar collisions
                    // Prim to avatar collisions

                    d.Vector3 pos = d.BodyGetPosition(Body);
                    vec.X = (_target_velocity.X - vel.X) * (PID_D) + (_zeroPosition.X - pos.X) * (PID_P * 2);
                    vec.Y = (_target_velocity.Y - vel.Y) * (PID_D) + (_zeroPosition.Y - pos.Y)* (PID_P * 2);
                    if (flying)
                    {
                        vec.Z = (_target_velocity.Z - vel.Z) * (PID_D) + (_zeroPosition.Z - pos.Z) * PID_P;
                    }
                }
                //PidStatus = true;
            }
            else
            {
                m_pidControllerActive = true;
                _zeroFlag = false;
                if (m_iscolliding && !flying)
                {
                    // We're standing on something
                    vec.X = ((_target_velocity.X / movementdivisor) - vel.X) * (PID_D);
                    vec.Y = ((_target_velocity.Y / movementdivisor) - vel.Y) * (PID_D);
                }
                else if (m_iscolliding && flying)
                {
                    // We're flying and colliding with something
                    vec.X = ((_target_velocity.X / movementdivisor) - vel.X) * (PID_D / 16);
                    vec.Y = ((_target_velocity.Y / movementdivisor) - vel.Y) * (PID_D / 16);
                }
                else if (!m_iscolliding && flying)
                {
                    // we're in mid air suspended
                    vec.X = ((_target_velocity.X / movementdivisor) - vel.X) * (PID_D / 6);
                    vec.Y = ((_target_velocity.Y / movementdivisor) - vel.Y) * (PID_D / 6);

//                    m_log.DebugFormat(
//                        "[ODE CHARACTER]: !m_iscolliding && flying, vec {0}, _target_velocity {1}, movementdivisor {2}, vel {3}",
//                        vec, _target_velocity, movementdivisor, vel);
                }

                if (flying)
                {
                    // This also acts as anti-gravity so that we hover when flying rather than fall.
                    vec.Z = (_target_velocity.Z - vel.Z) * (PID_D);
                }
                else
                {
                    if (m_iscolliding && _target_velocity.Z > 0.0f)
                    {
                        // We're colliding with something and we're not flying but we're moving
                        // This means we're walking or running.
                        d.Vector3 pos = d.BodyGetPosition(Body);
                        vec.Z = (_target_velocity.Z - vel.Z) * PID_D + (_zeroPosition.Z - pos.Z) * PID_P;
                        vec.X = ((_target_velocity.X - vel.X) / 1.2f) * PID_D;
                        vec.Y = ((_target_velocity.Y - vel.Y) / 1.2f) * PID_D;
                    }
                    else if (!m_iscolliding)
                    {
                        // we're not colliding and we're not flying so that means we're falling!
                        // m_iscolliding includes collisions with the ground.
                        vec.X = ((_target_velocity.X - vel.X) / 1.2f) * PID_D;
                        vec.Y = ((_target_velocity.Y - vel.Y) / 1.2f) * PID_D;
                    }
                }
            }

            if (flying)
            {
                // Anti-gravity so that we hover when flying rather than fall.
                vec.Z += ((-1 * _parent_scene.gravityz) * m_mass);

                //Added for auto fly height. Kitto Flora
                //d.Vector3 pos = d.BodyGetPosition(Body);
                float target_altitude = _parent_scene.GetTerrainHeightAtXY(_position.X, _position.Y) + MinimumGroundFlightOffset;

                if (_position.Z < target_altitude)
                {
                    vec.Z += (target_altitude - _position.Z) * PID_P * 5.0f;
                }
                // end add Kitto Flora
            }

            if (vec.IsFinite())
            {
                // Apply the total force acting on this avatar
                d.BodyAddForce(Body, vec.X, vec.Y, vec.Z);

                if (!_zeroFlag)
                    AlignAvatarTiltWithCurrentDirectionOfMovement(vec);
            }
            else
            {
                m_log.WarnFormat(
                    "[ODE CHARACTER]: Got a NaN force vector {0} in Move() for {1}.  Removing character from physics scene.",
                    vec, Name);

                defects.Add(this);

                return;
            }

            d.Vector3 newVel = d.BodyGetLinearVel(Body);
            if (newVel.X >= 256 || newVel.X <= 256 || newVel.Y >= 256 || newVel.Y <= 256 || newVel.Z >= 256 || newVel.Z <= 256)
            {
//                m_log.DebugFormat(
//                    "[ODE CHARACTER]: Limiting falling velocity from {0} to {1} for {2}", newVel.Z, -9.8, Name);

                newVel.X = Util.Clamp<float>(newVel.X, -255f, 255f);
                newVel.Y = Util.Clamp<float>(newVel.Y, -255f, 255f);

                if (!flying)
                    newVel.Z
                        = Util.Clamp<float>(
                            newVel.Z, -_parent_scene.AvatarTerminalVelocity, _parent_scene.AvatarTerminalVelocity);
                else
                    newVel.Z = Util.Clamp<float>(newVel.Z, -255f, 255f);

                d.BodySetLinearVel(Body, newVel.X, newVel.Y, newVel.Z);
            }
        }

        /// <summary>
        /// Updates the reported position and velocity.  This essentially sends the data up to ScenePresence.
        /// </summary>
        /// <param name="defects">The character will be added to this list if there is something wrong (non-finite
        /// position or velocity).
        /// </param>
        internal void UpdatePositionAndVelocity(List<OdeCharacter> defects)
        {
            //  no lock; called from Simulate() -- if you call this from elsewhere, gotta lock or do Monitor.Enter/Exit!
            d.Vector3 newPos;
            try
            {
                newPos = d.BodyGetPosition(Body);
            }
            catch (NullReferenceException)
            {
                bad = true;
                defects.Add(this);
                newPos = new d.Vector3(_position.X, _position.Y, _position.Z);
                base.RaiseOutOfBounds(_position); // Tells ScenePresence that there's a problem!
                m_log.WarnFormat("[ODE CHARACTER]: Avatar Null reference for Avatar {0}, physical actor {1}", Name, m_uuid);

                return;
            }

            //  kluge to keep things in bounds.  ODE lets dead avatars drift away (they should be removed!)
            if (newPos.X < 0.0f) newPos.X = 0.0f;
            if (newPos.Y < 0.0f) newPos.Y = 0.0f;
            if (newPos.X > (int)_parent_scene.WorldExtents.X - 0.05f) newPos.X = (int)_parent_scene.WorldExtents.X - 0.05f;
            if (newPos.Y > (int)_parent_scene.WorldExtents.Y - 0.05f) newPos.Y = (int)_parent_scene.WorldExtents.Y - 0.05f;

            _position.X = newPos.X;
            _position.Y = newPos.Y;
            _position.Z = newPos.Z;

            // I think we need to update the taintPosition too -- Diva 12/24/10
            m_taintPosition = _position;

            // Did we move last? = zeroflag
            // This helps keep us from sliding all over

            if (_zeroFlag)
            {
                _velocity = Vector3.Zero;

                // Did we send out the 'stopped' message?
                if (!m_lastUpdateSent)
                {
                    m_lastUpdateSent = true;
                    //base.RequestPhysicsterseUpdate();
                }
            }
            else
            {
                m_lastUpdateSent = false;
                d.Vector3 newVelocity;

                try
                {
                    newVelocity = d.BodyGetLinearVel(Body);
                }
                catch (NullReferenceException)
                {
                    newVelocity.X = _velocity.X;
                    newVelocity.Y = _velocity.Y;
                    newVelocity.Z = _velocity.Z;
                }

                _velocity.X = newVelocity.X;
                _velocity.Y = newVelocity.Y;
                _velocity.Z = newVelocity.Z;

                if (_velocity.Z < -6 && !m_hackSentFall)
                {
                    m_hackSentFall = true;
                    m_pidControllerActive = false;
                }
                else if (flying && !m_hackSentFly)
                {
                    //m_hackSentFly = true;
                    //base.SendCollisionUpdate(new CollisionEventUpdate());
                }
                else
                {
                    m_hackSentFly = false;
                    m_hackSentFall = false;
                }
            }
        }

        /// <summary>
        /// This creates the Avatar's physical Surrogate in ODE at the position supplied
        /// </summary>
        /// <remarks>
        /// WARNING: This MUST NOT be called outside of ProcessTaints, else we can have unsynchronized access
        /// to ODE internals. ProcessTaints is called from within thread-locked Simulate(), so it is the only
        /// place that is safe to call this routine AvatarGeomAndBodyCreation.
        /// </remarks>
        /// <param name="npositionX"></param>
        /// <param name="npositionY"></param>
        /// <param name="npositionZ"></param>
        /// <param name="tensor"></param>
        private void CreateOdeStructures(float npositionX, float npositionY, float npositionZ, float tensor)
        {
            if (!(Shell == IntPtr.Zero && Body == IntPtr.Zero && Amotor == IntPtr.Zero))
            {
                m_log.ErrorFormat(
                    "[ODE CHARACTER]: Creating ODE structures for {0} even though some already exist.  Shell = {1}, Body = {2}, Amotor = {3}",
                    Name, Shell, Body, Amotor);
            }

            int dAMotorEuler = 1;
//            _parent_scene.waitForSpaceUnlock(_parent_scene.space);
            if (CAPSULE_LENGTH <= 0)
            {
                m_log.Warn("[ODE CHARACTER]: The capsule size you specified in opensim.ini is invalid!  Setting it to the smallest possible size!");
                CAPSULE_LENGTH = 0.01f;
            }

            if (CAPSULE_RADIUS <= 0)
            {
                m_log.Warn("[ODE CHARACTER]: The capsule size you specified in opensim.ini is invalid!  Setting it to the smallest possible size!");
                CAPSULE_RADIUS = 0.01f;
            }

//          lock (OdeScene.UniversalColliderSyncObject)
            Shell = d.CreateCapsule(_parent_scene.space, CAPSULE_RADIUS, CAPSULE_LENGTH);

            d.GeomSetCategoryBits(Shell, (int)m_collisionCategories);
            d.GeomSetCollideBits(Shell, (int)m_collisionFlags);

            d.MassSetCapsuleTotal(out ShellMass, m_mass, 2, CAPSULE_RADIUS, CAPSULE_LENGTH);
            Body = d.BodyCreate(_parent_scene.world);
            d.BodySetPosition(Body, npositionX, npositionY, npositionZ);

            _position.X = npositionX;
            _position.Y = npositionY;
            _position.Z = npositionZ;

            m_taintPosition = _position;

            d.BodySetMass(Body, ref ShellMass);
            d.Matrix3 m_caprot;
            // 90 Stand up on the cap of the capped cyllinder
            if (_parent_scene.IsAvCapsuleTilted)
            {
                d.RFromAxisAndAngle(out m_caprot, 1, 0, 1, (float)(Math.PI / 2));
            }
            else
            {
                d.RFromAxisAndAngle(out m_caprot, 0, 0, 1, (float)(Math.PI / 2));
            }

            d.GeomSetRotation(Shell, ref m_caprot);
            d.BodySetRotation(Body, ref m_caprot);

            d.GeomSetBody(Shell, Body);

            // The purpose of the AMotor here is to keep the avatar's physical
            // surrogate from rotating while moving
            Amotor = d.JointCreateAMotor(_parent_scene.world, IntPtr.Zero);
            d.JointAttach(Amotor, Body, IntPtr.Zero);
            d.JointSetAMotorMode(Amotor, dAMotorEuler);
            d.JointSetAMotorNumAxes(Amotor, 3);
            d.JointSetAMotorAxis(Amotor, 0, 0, 1, 0, 0);
            d.JointSetAMotorAxis(Amotor, 1, 0, 0, 1, 0);
            d.JointSetAMotorAxis(Amotor, 2, 0, 0, 0, 1);
            d.JointSetAMotorAngle(Amotor, 0, 0);
            d.JointSetAMotorAngle(Amotor, 1, 0);
            d.JointSetAMotorAngle(Amotor, 2, 0);

            // These lowstops and high stops are effectively (no wiggle room)
            if (_parent_scene.IsAvCapsuleTilted)
            {
                d.JointSetAMotorParam(Amotor, (int)dParam.LowStop, -0.000000000001f);
                d.JointSetAMotorParam(Amotor, (int)dParam.LoStop3, -0.000000000001f);
                d.JointSetAMotorParam(Amotor, (int)dParam.LoStop2, -0.000000000001f);
                d.JointSetAMotorParam(Amotor, (int)dParam.HiStop, 0.000000000001f);
                d.JointSetAMotorParam(Amotor, (int)dParam.HiStop3, 0.000000000001f);
                d.JointSetAMotorParam(Amotor, (int)dParam.HiStop2, 0.000000000001f);
            }
            else
            {
                #region Documentation of capsule motor LowStop and HighStop parameters
                // Intentionally introduce some tilt into the capsule by setting
                // the motor stops to small epsilon values. This small tilt prevents
                // the capsule from falling into the terrain; a straight-up capsule
                // (with -0..0 motor stops) falls into the terrain for reasons yet
                // to be comprehended in their entirety.
                #endregion
                AlignAvatarTiltWithCurrentDirectionOfMovement(Vector3.Zero);
                d.JointSetAMotorParam(Amotor, (int)dParam.LowStop, 0.08f);
                d.JointSetAMotorParam(Amotor, (int)dParam.LoStop3, -0f);
                d.JointSetAMotorParam(Amotor, (int)dParam.LoStop2, 0.08f);
                d.JointSetAMotorParam(Amotor, (int)dParam.HiStop,  0.08f); // must be same as lowstop, else a different, spurious tilt is introduced
                d.JointSetAMotorParam(Amotor, (int)dParam.HiStop3, 0f); // same as lowstop
                d.JointSetAMotorParam(Amotor, (int)dParam.HiStop2, 0.08f); // same as lowstop
            }

            // Fudge factor is 1f by default, we're setting it to 0.  We don't want it to Fudge or the
            // capped cyllinder will fall over
            d.JointSetAMotorParam(Amotor, (int)dParam.FudgeFactor, 0f);
            d.JointSetAMotorParam(Amotor, (int)dParam.FMax, tensor);

            //d.Matrix3 bodyrotation = d.BodyGetRotation(Body);
            //d.QfromR(
            //d.Matrix3 checkrotation = new d.Matrix3(0.7071068,0.5, -0.7071068,
            //
            //m_log.Info("[PHYSICSAV]: Rotation: " + bodyrotation.M00 + " : " + bodyrotation.M01 + " : " + bodyrotation.M02 + " : " + bodyrotation.M10 + " : " + bodyrotation.M11 + " : " + bodyrotation.M12 + " : " + bodyrotation.M20 + " : " + bodyrotation.M21 + " : " + bodyrotation.M22);
            //standupStraight();

            _parent_scene.geom_name_map[Shell] = Name;
            _parent_scene.actor_name_map[Shell] = this;
        }

        /// <summary>
        /// Cleanup the things we use in the scene.
        /// </summary>
        internal void Destroy()
        {
            m_tainted_isPhysical = false;
            _parent_scene.AddPhysicsActorTaint(this);
        }

        /// <summary>
        /// Used internally to destroy the ODE structures associated with this character.
        /// </summary>
        internal void DestroyOdeStructures()
        {
            // Create avatar capsule and related ODE data
            if (Shell == IntPtr.Zero || Body == IntPtr.Zero || Amotor == IntPtr.Zero)
            {
                m_log.ErrorFormat(
                    "[ODE CHARACTER]: Destroying ODE structures for {0} even though some are already null.  Shell = {1}, Body = {2}, Amotor = {3}",
                    Name, Shell, Body, Amotor);
            }

            // destroy avatar capsule and related ODE data
            if (Amotor != IntPtr.Zero)
            {
                // Kill the Amotor
                d.JointDestroy(Amotor);
                Amotor = IntPtr.Zero;
            }

            //kill the Geometry
//            _parent_scene.waitForSpaceUnlock(_parent_scene.space);

            if (Body != IntPtr.Zero)
            {
                //kill the body
                d.BodyDestroy(Body);
                Body = IntPtr.Zero;
            }

            if (Shell != IntPtr.Zero)
            {
//              lock (OdeScene.UniversalColliderSyncObject)
                d.GeomDestroy(Shell);

                _parent_scene.geom_name_map.Remove(Shell);
                _parent_scene.actor_name_map.Remove(Shell);

                Shell = IntPtr.Zero;
            }
        }

        public override void CrossingFailure()
        {
        }

        public override Vector3 PIDTarget { set { return; } }
        public override bool PIDActive 
        { 
            get { return false; }
            set { return; } 
        }
        public override float PIDTau { set { return; } }

        public override float PIDHoverHeight { set { return; } }
        public override bool PIDHoverActive { set { return; } }
        public override PIDHoverType PIDHoverType { set { return; } }
        public override float PIDHoverTau { set { return; } }
        
        public override Quaternion APIDTarget{ set { return; } }

        public override bool APIDActive{ set { return; } }

        public override float APIDStrength{ set { return; } }

        public override float APIDDamping{ set { return; } }

        public override void SubscribeEvents(int ms)
        {
            m_requestedUpdateFrequency = ms;
            m_eventsubscription = ms;

            // Don't clear collision event reporting here.  This is called directly from scene code and so can lead
            // to a race condition with the simulate loop

            _parent_scene.AddCollisionEventReporting(this);
        }

        public override void UnSubscribeEvents()
        {
            _parent_scene.RemoveCollisionEventReporting(this);

            // Don't clear collision event reporting here.  This is called directly from scene code and so can lead
            // to a race condition with the simulate loop

            m_requestedUpdateFrequency = 0;
            m_eventsubscription = 0;
        }

        internal void AddCollisionEvent(uint CollidedWith, ContactPoint contact)
        {
            if (m_eventsubscription > 0)
            {
//                m_log.DebugFormat(
//                    "[PHYSICS]: Adding collision event for {0}, collidedWith {1}, contact {2}", "", CollidedWith, contact);

                CollisionEventsThisFrame.AddCollider(CollidedWith, contact);
            }
        }

        internal void SendCollisions()
        {
            if (m_eventsubscription > m_requestedUpdateFrequency)
            {
                base.SendCollisionUpdate(CollisionEventsThisFrame);

                CollisionEventsThisFrame.Clear();
                m_eventsubscription = 0;
            }
        }

        public override bool SubscribedEvents()
        {
            if (m_eventsubscription > 0)
                return true;
            return false;
        }

        internal void ProcessTaints()
        {
            if (m_taintPosition != _position)
            {
                if (Body != IntPtr.Zero)
                {
                    d.BodySetPosition(Body, m_taintPosition.X, m_taintPosition.Y, m_taintPosition.Z);
                    _position = m_taintPosition;
                }
            }

            if (m_taintForce != Vector3.Zero)
            {
                if (Body != IntPtr.Zero)
                {
                    // FIXME: This is not a good solution since it's subject to a race condition if a force is another
                    // thread sets a new force while we're in this loop (since it could be obliterated by
                    // m_taintForce = Vector3.Zero.  Need to lock ProcessTaints() when we set a new tainted force.
                    d.BodyAddForce(Body, m_taintForce.X, m_taintForce.Y, m_taintForce.Z);
                }

                m_taintForce = Vector3.Zero;
            }

            if (m_taintTargetVelocity != _target_velocity)
                _target_velocity = m_taintTargetVelocity;

            if (m_tainted_isPhysical != m_isPhysical)
            {
                if (m_tainted_isPhysical)
                {
                    CreateOdeStructures(_position.X, _position.Y, _position.Z, m_tensor);
                    _parent_scene.AddCharacter(this);
                }
                else
                {
                    _parent_scene.RemoveCharacter(this);
                    DestroyOdeStructures();
                }

                m_isPhysical = m_tainted_isPhysical;
            }

            if (m_tainted_CAPSULE_LENGTH != CAPSULE_LENGTH)
            {
                if (Shell != IntPtr.Zero && Body != IntPtr.Zero && Amotor != IntPtr.Zero)
                {
//                    m_log.DebugFormat(
//                        "[ODE CHARACTER]: Changing capsule size from {0} to {1} for {2}",
//                        CAPSULE_LENGTH, m_tainted_CAPSULE_LENGTH, Name);
                    
                    m_pidControllerActive = true;

                    // no lock needed on _parent_scene.OdeLock because we are called from within the thread lock in OdePlugin's simulate()
                    DestroyOdeStructures();

                    float prevCapsule = CAPSULE_LENGTH;
                    CAPSULE_LENGTH = m_tainted_CAPSULE_LENGTH;

                    CreateOdeStructures(
                        _position.X,
                        _position.Y,
                        _position.Z + (Math.Abs(CAPSULE_LENGTH - prevCapsule) * 2), m_tensor);

                    // As with Size, we reset velocity.  However, this isn't strictly necessary since it doesn't
                    // appear to stall initial region crossings when done here.  Being done for consistency.
//                    Velocity = Vector3.Zero;
                }
                else
                {
                    m_log.Warn("[ODE CHARACTER]: trying to change capsule size for " + Name + ", but the following ODE data is missing - "
                        + (Shell==IntPtr.Zero ? "Shell ":"")
                        + (Body==IntPtr.Zero ? "Body ":"")
                        + (Amotor==IntPtr.Zero ? "Amotor ":""));
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
