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
using System.Runtime.CompilerServices;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.PhysicsModules.SharedBase;
using log4net;

namespace OpenSim.Region.PhysicsModule.ubOde
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

        internal AABB2D _AABB2D;
        internal Vector3 _position;
        public int Colliderfilter = 0;

        public float CapsuleSizeZ;
        public float CapsuleRadius;
        internal Vector2 Orientation2D;
        internal float AvaSizeXsq = 0.3f;
        internal float AvaSizeYsq = 0.2f;
        internal float feetOff = 0;
        internal float boneOff = 0;

        public IntPtr Body = IntPtr.Zero;

        public int m_bodydisablecontrol = 0;
        private float m_scenegravityForceZ;
        int m_colliderObjectfilter = 0;
        int m_colliderGroundfilter = 0;

        private UBOdeNative.Quaternion m_NativeOrientation2D;
        private Vector3 _zeroPosition;
        internal Quaternion m_orientation;
        private Vector3 _velocity;
        private Vector3 m_rotationalVelocity;
        private Vector3 _acceleration;
        private Vector3 m_lastFallVel;

        private Vector3 m_size;

        private float m_mass = 80f;
        private float m_massInvTimeScaled = 1600f;
        public readonly float m_density = 60f;
        private bool m_pidControllerActive = true;

        internal Vector3 CollideNormal;

        public int  _charsListIndex;


        const float basePID_D = 0.55f; // scaled for unit mass unit time (2200 /(50*80))
        const float basePID_P = 0.225f; // scaled for unit mass unit time (900 /(50*80))

        public float PID_D;
        public float PID_P;

        private readonly ODEScene m_parent_scene;

        private readonly float m_sceneTimeStep;
        private readonly float m_sceneInverseTimeStep;

        public readonly float m_walkMultiplier = 1.0f / 1.3f;
        public readonly float m_runMultiplier = 1.0f / 0.8f;
        private bool m_flying = false;
        private bool m_iscolliding = false;
        private bool m_iscollidingGround = false;
        private bool m_iscollidingObj = false;

        private bool _zeroFlag = false;
        private bool m_haveLastFallVel = false;

        public bool m_returnCollisions = false;
        // taints and their non-tainted counterparts
        public bool m_isPhysical = false; // the current physical status
        public float MinimumGroundFlightOffset = 3f;

        private bool m_freemove = false;

        // private string m_name = String.Empty;
        // other filter control

        // Default we're a Character
        private const CollisionCategories m_collisionCategories = (CollisionCategories.Character);

        // Default, Collide with Other Geometries, spaces, bodies and characters.
        private const CollisionCategories m_collisionFlags = (CollisionCategories.Character
                                                        | CollisionCategories.Geom
                                                        | CollisionCategories.VolumeDtc
                                                        );
        // we do land collisions not ode                | CollisionCategories.Land);
        //private IntPtr capsule = IntPtr.Zero;
        public IntPtr collider = IntPtr.Zero;

        public IntPtr Amotor = IntPtr.Zero;

        internal UBOdeNative.Mass ShellMass;

        public int m_eventsubscription = 0;
        private int m_cureventsubscription = 0;
        private readonly CollisionEventUpdate CollisionEventsThisFrame = new();
        private bool SentEmptyCollisionsEvent;

        public bool bad = false;

        private readonly float m_frictionmu;

        // HoverHeight control
        private float m_PIDHoverHeight;
        private float m_PIDHoverTau;
        private bool m_useHoverPID;
        private PIDHoverType m_PIDHoverType;
        private float m_targetHoverHeight;

        public OdeCharacter(uint localID, String avName, ODEScene parent_scene, Vector3 pos, Vector3 pSize, float pfeetOffset, float density, float walk_divisor, float rundivisor)
        {
            m_baseLocalID = localID;
            m_parent_scene = parent_scene;

            m_sceneTimeStep = parent_scene.ODE_STEPSIZE;
            m_sceneInverseTimeStep = 1.0f / m_sceneTimeStep;

            if (pos.IsFinite())
            {
                if (pos.Z > Constants.MaxSimulationHeight)
                {
                    pos.Z = parent_scene.GetTerrainHeightAtXY(127, 127) + 5;
                }
                if (pos.Z < Constants.MinSimulationHeight) // shouldn't this be 0 ?
                {
                    pos.Z = parent_scene.GetTerrainHeightAtXY(127, 127) + 5;
                }
                _position = pos;
            }
            else
            {
                _position = new Vector3(((float)m_parent_scene.WorldExtents.X * 0.5f), ((float)m_parent_scene.WorldExtents.Y * 0.5f), parent_scene.GetTerrainHeightAtXY(128f, 128f) + 10f);
                m_log.Warn("[PHYSICS]: Got NaN Position on Character Create");
            }

            m_size.X = pSize.X > 0.01f ? 0.5f * pSize.X : 0.01f;
            m_size.Y = pSize.Y > 0.01f ? 0.5f * pSize.Y : 0.01f;
            m_size.Z = pSize.Z > 0.01f ? 0.5f * pSize.Z : 0.01f;

            CapsuleRadius = MathF.Max(m_size.X, m_size.Y);
            CapsuleSizeZ = m_size.Z;

            AvaSizeXsq = m_size.X;
            AvaSizeXsq *= AvaSizeXsq;
            AvaSizeYsq = m_size.Y;
            AvaSizeYsq *= AvaSizeYsq;

            m_orientation = Quaternion.Identity;
            Orientation2D = new(0f, 1f);
            m_NativeOrientation2D.X = 0f;
            m_NativeOrientation2D.Y = 0f;
            m_NativeOrientation2D.Z = 0f;
            m_NativeOrientation2D.W = 1f;

            m_density = density;

            // force lower density for testing
            m_density = 3.0f;

            m_frictionmu = m_parent_scene.AvatarFriction;

            m_walkMultiplier = 1.0f / walk_divisor;
            m_runMultiplier = 1.0f / rundivisor;

            m_mass = 8f * m_density * m_size.X * m_size.Y * m_size.Z;
            m_massInvTimeScaled = m_mass * m_sceneInverseTimeStep;
            // sure we have a default

            PID_D = basePID_D * m_massInvTimeScaled;
            PID_P = basePID_P * m_massInvTimeScaled;

            m_scenegravityForceZ = parent_scene.gravityz * m_mass;

            m_isPhysical = false; // current status: no ODE information exists

            Name = avName;

            UpdateAABB2D();

            AddChange(changes.Add, null);
        }

        public override int PhysicsActorType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (int)ActorTypes.Agent;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void getContactData(ref ContactData cdata)
        {
            cdata.mu = m_frictionmu;
            cdata.bounce = 0;
            cdata.softcolide = false;
        }

        public override bool Building
        {
            get; set;
        }

        /// <summary>
        /// If this is set, the avatar will move faster
        /// </summary>
        private bool m_alwaysRun = false;
        public override bool SetAlwaysRun
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_alwaysRun;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                m_alwaysRun = value;
            }
        }

        public override PhysicsActor ParentActor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (PhysicsActor)this;
            }
        }

        public override bool Grabbed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                return;
            }
        }

        public override bool Selected
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                return;
            }
        }

        private float m_buoyancy = 0f;
        public override float Buoyancy
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_buoyancy;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                m_buoyancy = value;
            }
        }

        public override bool IsPhysical
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_isPhysical;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                return;
            }
        }

        public override bool ThrottleUpdates
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return false;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                return;
            }
        }

        public override bool Flying
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_flying;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                m_flying = value;
                //m_log.DebugFormat("[PHYSICS]: Set OdeCharacter Flying to {0}", flying);
            }
        }

        /// <summary>
        /// Returns if the avatar is colliding in general.
        /// This includes the ground and objects and avatar.
        /// </summary>
        public override bool IsColliding
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (m_iscolliding || m_iscollidingGround);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value)
                {
                    Colliderfilter += 3;
                    if (Colliderfilter > 9)
                        Colliderfilter = 9;
                }
                else
                {
                    Colliderfilter--;
                    if (Colliderfilter < 0)
                        Colliderfilter = 0;
                }

                if (Colliderfilter < 6)
                    m_iscolliding = false;
                else
                {
                    m_pidControllerActive = true;
                    m_iscolliding = true;
                    m_freemove = false;
                }
            }
        }

        /// <summary>
        /// Returns if an avatar is colliding with the ground
        /// </summary>
        public override bool CollidingGround
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_iscollidingGround;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_iscollidingObj;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
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

                // m_iscollidingObj = value;

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPidStatus(bool status)
        {
            m_pidControllerActive = status;
        }

        public override bool Stopped
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _zeroFlag;
            }
        }

        /// <summary>
        /// This 'puts' an avatar somewhere in the physics space.
        /// Not really a good choice unless you 'know' it's a good
        /// spot otherwise you're likely to orbit the avatar.
        /// </summary>
        public override Vector3 Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _position;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value.IsFinite())
                {
                    if (value.Z < -100f || value.Z > 9999999f)
                        value.Z = m_parent_scene.GetTerrainHeightAtXY(127, 127) + 5;

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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_rotationalVelocity;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                m_rotationalVelocity = value;
            }
        }

        /// <summary>
        /// This property sets the height of the avatar only.  We use the height to make sure the avatar stands up straight
        /// and use it to offset landings properly
        /// </summary>
        public override Vector3 Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_size * 2f;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value.IsFinite())
                {
                    if (value.X < 0.01f)
                        value.X = 0.01f;
                    if (value.Y < 0.01f)
                        value.Y = 0.01f;
                    if (value.Z < 0.01f)
                        value.Z = 0.01f;

                    AddChange(changes.Size, value);
                }
                else
                {
                    m_log.Warn("[PHYSICS]: Got a NaN Size from Scene on a Character");
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

                strAvatarSize st = new()
                {
                    size = size,
                };
                AddChange(changes.AvatarSize, st);
            }
            else
            {
                m_log.Warn("[PHYSICS]: Got a NaN AvatarSize from Scene on a Character");
            }

        }

        /// <summary>
        /// Uses the capped cyllinder volume formula to calculate the avatar's mass.
        /// This may be used in calculations in the scene/scenepresence
        /// </summary>
        public override float Mass
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        public override void LockAngularMotion(byte axislocks)
        {

        }

        public override Vector3 Force
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_targetVelocity;
            }
            set
            {
            }
        }

        public override int VehicleType
        {
            get
            {
                return 0;
            }
            set
            {
                return;
            }
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
                return _position;
            }
        }

        public override Vector3 GeometricCenter
        {
            get
            {
                return _position;
            }
        }

        public override PrimitiveBaseShape Shape
        {
            set
            {
                return;
            }
        }

        public override Vector3 rootVelocity
        {
            get
            {
                return _velocity;
            }
        }

        public override Vector3 Velocity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _velocity;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        public override Vector3 TargetVelocity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_targetVelocity;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value.IsFinite())
                {
                    AddChange(changes.TargetVelocity, value);
                }
                else
                {
                    m_log.Warn("[PHYSICS]: Got a NaN velocity from Scene in a Character");
                }
            }
        }

        public override Vector3 Torque
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Vector3.Zero;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                return;
            }
        }

        public override float CollisionScore
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return 0f;
            }
            set
            {
            }
        }

        public override bool Kinematic
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return false;
            }
            set
            {
            }
        }

        public override Quaternion Orientation
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_orientation;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                //fakeori = value;
                //givefakeori++;
                value.Normalize();
                AddChange(changes.Orientation, value);
            }
        }

        public override Vector3 Acceleration
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _acceleration;
            }
            set
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAcceleration(Vector3 accel)
        {
            m_pidControllerActive = true;
            _acceleration = accel;
            if (Body != IntPtr.Zero)
                UBOdeNative.BodyEnable(Body);
        }

        /// <summary>
        /// Adds the force supplied to the Target Velocity
        /// The PID controller takes this target velocity and tries to make it a reality
        /// </summary>
        /// <param name="force"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AddForce(Vector3 force, bool pushforce)
        {
            if (force.IsFinite())
            {
                if (pushforce)
                {
                    AddChange(changes.Force, force * m_density * m_sceneInverseTimeStep / 28f);
                }
                else
                {
                    AddChange(changes.TargetVelocity, force);
                }
            }
            else
            {
                m_log.Warn("[PHYSICS]: Got a NaN force applied to a Character");
            }
            //m_lastUpdateSent = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AvatarJump(float impulseZ)
        {
            // convert back to force and remove mass effect
            AddChange(changes.Force, new Vector3(0, 0, impulseZ * m_massInvTimeScaled));
        }

        public override void AddAngularForce(Vector3 force, bool pushforce)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SetMomentum(Vector3 momentum)
        {
            if (momentum.IsFinite())
                AddChange(changes.Momentum, momentum);
        }


        private void AvatarGeomAndBodyCreate()
        {
            float sx = m_size.X;
            float sy = m_size.Y;
            float sz = m_size.Z;

            float bot = -sz;
            boneOff = bot + 0.3f;

            float feetsz = sz * 0.45f;
            if (feetsz > 0.6f)
                feetsz = 0.6f;
            feetOff = bot + feetsz;

            AvaSizeXsq = sx;
            AvaSizeXsq *= AvaSizeXsq;
            AvaSizeYsq = sy;
            AvaSizeYsq *= AvaSizeYsq;

            CapsuleRadius = MathF.Max(sx, sy);
            float l = sz - CapsuleRadius;
            CapsuleSizeZ = sz;

            collider = UBOdeNative.CreateCapsule(m_parent_scene.TopSpace, CapsuleRadius, 2.0f * l);
            UBOdeNative.GeomSetCategoryBits(collider, (uint)m_collisionCategories);
            UBOdeNative.GeomSetCollideBits(collider, (uint)m_collisionFlags);

            // update mass
            m_mass = 8f * m_density * sx * sy * sz;
            UBOdeNative.MassSetBoxTotal(out ShellMass, m_mass, 2f * sx, 2f * sy, 2f * sz);
            m_massInvTimeScaled = m_mass * m_sceneInverseTimeStep;
            PID_D = basePID_D * m_massInvTimeScaled;
            PID_P = basePID_P * m_massInvTimeScaled;
            m_scenegravityForceZ = m_parent_scene.gravityz * m_mass;

            Body = UBOdeNative.BodyCreate(m_parent_scene.world);

            _zeroFlag = false;
            m_pidControllerActive = true;
            m_freemove = false;

            _velocity = Vector3.Zero;

            // SafeNativeMethods.BodySetAutoDisableFlag(Body,false);
            UBOdeNative.BodySetAutoDisableFlag(Body, true);
            m_bodydisablecontrol = 0;

            UBOdeNative.BodySetPosition(Body, _position.X, _position.Y, _position.Z);

            UBOdeNative.BodySetMass(Body, ref ShellMass);
            //SafeNativeMethods.GeomSetBody(capsule, Body);
            UBOdeNative.GeomSetBody(collider, Body);

            // The purpose of the AMotor here is to keep the avatar's physical
            // surrogate from rotating while moving
            Amotor = UBOdeNative.JointCreateAMotor(m_parent_scene.world, IntPtr.Zero);
            UBOdeNative.JointAttach(Amotor, Body, IntPtr.Zero);

            UBOdeNative.JointSetAMotorMode(Amotor, 0);
            UBOdeNative.JointSetAMotorNumAxes(Amotor, 3);
            UBOdeNative.JointSetAMotorAxis(Amotor, 0, 0, 1, 0, 0);
            UBOdeNative.JointSetAMotorAxis(Amotor, 1, 0, 0, 1, 0);
            UBOdeNative.JointSetAMotorAxis(Amotor, 2, 0, 0, 0, 1);

            UBOdeNative.JointSetAMotorAngle(Amotor, 0, 0);
            UBOdeNative.JointSetAMotorAngle(Amotor, 1, 0);
            UBOdeNative.JointSetAMotorAngle(Amotor, 2, 0);

            UBOdeNative.JointSetAMotorParam(Amotor, (int)dParam.StopCFM, 0f); // make it HARD
            UBOdeNative.JointSetAMotorParam(Amotor, (int)dParam.StopCFM2, 0f);
            UBOdeNative.JointSetAMotorParam(Amotor, (int)dParam.StopCFM3, 0f);
            UBOdeNative.JointSetAMotorParam(Amotor, (int)dParam.StopERP, 0.8f);
            UBOdeNative.JointSetAMotorParam(Amotor, (int)dParam.StopERP2, 0.8f);
            UBOdeNative.JointSetAMotorParam(Amotor, (int)dParam.StopERP3, 0.8f);

            // These lowstops and high stops are effectively (no wiggle room)
            UBOdeNative.JointSetAMotorParam(Amotor, (int)dParam.LowStop, -1e-5f);
            UBOdeNative.JointSetAMotorParam(Amotor, (int)dParam.HiStop, 1e-5f);
            UBOdeNative.JointSetAMotorParam(Amotor, (int)dParam.LoStop2, -1e-5f);
            UBOdeNative.JointSetAMotorParam(Amotor, (int)dParam.HiStop2, 1e-5f);
            UBOdeNative.JointSetAMotorParam(Amotor, (int)dParam.LoStop3, -1e-5f);
            UBOdeNative.JointSetAMotorParam(Amotor, (int)dParam.HiStop3, 1e-5f);

            UBOdeNative.JointSetAMotorParam(Amotor, (int)UBOdeNative.JointParam.Vel, 0);
            UBOdeNative.JointSetAMotorParam(Amotor, (int)UBOdeNative.JointParam.Vel2, 0);
            UBOdeNative.JointSetAMotorParam(Amotor, (int)UBOdeNative.JointParam.Vel3, 0);

            UBOdeNative.JointSetAMotorParam(Amotor, (int)dParam.FMax, 5e8f);
            UBOdeNative.JointSetAMotorParam(Amotor, (int)dParam.FMax2, 5e8f);
            UBOdeNative.JointSetAMotorParam(Amotor, (int)dParam.FMax3, 5e8f);
        }

        /// <summary>
        /// Destroys the avatar body and geom

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AvatarGeomAndBodyDestroy()
        {
            // Kill the Amotor
            if (Amotor != IntPtr.Zero)
            {
                UBOdeNative.JointDestroy(Amotor);
                Amotor = IntPtr.Zero;
            }

            if (Body != IntPtr.Zero)
            {
                //kill the body
                UBOdeNative.BodyDestroy(Body);
                Body = IntPtr.Zero;
            }

            //kill the Geoms
            /*
            if (capsule != IntPtr.Zero)
            {
                m_parent_scene.actor_name_map.Remove(capsule);
                //m_parent_scene.waitForSpaceUnlock(collider);
                SafeNativeMethods.GeomDestroy(capsule);
                capsule = IntPtr.Zero;
            }

            if (collider != IntPtr.Zero)
            {
                SafeNativeMethods.SpaceDestroy(collider);
                collider = IntPtr.Zero;
            }
            */
            if (collider != IntPtr.Zero)
            {
                m_parent_scene.actor_name_map.Remove(collider);
                //m_parent_scene.waitForSpaceUnlock(m_parent_scene.CharsSpace);
                UBOdeNative.GeomDestroy(collider);
                collider = IntPtr.Zero;
            }
        }

        //in place 2D rotation around Z assuming rot is normalised and is a rotation around Z
        public static void RotateXYonZ(ref float x, ref float y, ref Quaternion rot)
        {
            float sin = 2.0f * rot.Z * rot.W;
            float cos = rot.W * rot.W - rot.Z * rot.Z;
            float tx = x;

            x = tx * cos - y * sin;
            y = tx * sin + y * cos;
        }
        public static void RotateXYonZ(ref float x, ref float y, float sin, float cos)
        {
            float tx = x;
            x = tx * cos - y * sin;
            y = tx * sin + y * cos;
        }
        public static void invRotateXYonZ(ref float x, ref float y, float sin, float cos)
        {
            float tx = x;
            x = tx * cos + y * sin;
            y = -tx * sin + y * cos;
        }

        public static void invRotateXYonZ(ref float x, ref float y, in Quaternion rot)
        {
            float sin = -2.0f * rot.Z * rot.W;
            float cos = rot.W * rot.W - rot.Z * rot.Z;
            float tx = x;

            x = tx * cos - y * sin;
            y = tx * sin + y * cos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Collide(IntPtr other, bool reverse, ref UBOdeNative.ContactGeom contact,
            ref UBOdeNative.ContactGeom altContact, ref bool useAltcontact, ref bool feetcollision)
        {
            feetcollision = false;
            useAltcontact = false;

            Vector3 offset;

            float h = contact.pos.Z - _position.Z;
            offset.Z = h - feetOff;

            offset.X = contact.pos.X - _position.X;
            offset.Y = contact.pos.Y - _position.Y;

            UBOdeNative.GeomClassID gtype = UBOdeNative.GeomGetClass(other);

            if (gtype == UBOdeNative.GeomClassID.SphereClass && UBOdeNative.GeomGetBody(other) != IntPtr.Zero)
            {
                if (UBOdeNative.GeomSphereGetRadius(other) < 0.5)
                    return true;
            }

            if (offset.Z > 0 || contact.normal.Z > 0.35f)
            {
                if (offset.Z <= 0)
                {
                    feetcollision = true;
                    if (h < boneOff)
                    {
                        CollideNormal = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref contact.normal);
                        IsColliding = true;
                    }
                }
                return true;
            }

            if (m_flying)
                return true;

            feetcollision = true;
            if (h < boneOff)
            {
                CollideNormal = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref contact.normal);
                IsColliding = true;
            }

            useAltcontact = true;

            offset.Z -= 0.2f;

            offset.Normalize();

            float tdp = contact.depth;
            float t = offset.X;
            t = MathF.Abs(t);
            if (t > 1e-6)
            {
                tdp /= t;
                tdp *= contact.normal.X;
            }
            else
                tdp *= 10;

            if (tdp > 0.25f)
                tdp = 0.25f;

            altContact.pos = contact.pos;
            altContact.depth = tdp;

            if (reverse)
            {
                altContact.normal = Unsafe.As<Vector3, UBOdeNative.Vector3>(ref offset);
            }
            else
            {
                altContact.normal.X = -offset.X;
                altContact.normal.Y = -offset.Y;
                altContact.normal.Z = -offset.Z;
            }
            return true;
        }

        /// <summary>
        /// Called from Simulate
        /// This is the avatar's movement control + PID Controller
        /// </summary>
        /// <param name="timeStep"></param>
        public void Move()
        {
            if (Body == IntPtr.Zero)
                return;

            if (!UBOdeNative.BodyIsEnabled(Body))
            {
                if (++m_bodydisablecontrol < 50)
                    return;

                // clear residuals
                UBOdeNative.BodySetAngularVel(Body, 0f, 0f, 0f);
                UBOdeNative.BodySetLinearVel(Body, 0f, 0f, 0f);
                _zeroFlag = true;
                UBOdeNative.BodyEnable(Body);
            }

            m_bodydisablecontrol = 0;

            // the Amotor still lets avatar rotation to drift during colisions
            // so force it back to identity
            UBOdeNative.BodySetQuaternion(Body, ref m_NativeOrientation2D);

            _position = UBOdeNative.BodyGetPositionOMV(Body);
            // check outbounds forcing to be in world
            bool fixbody = false;
            if ((Single.IsNaN(_position.X) || Single.IsInfinity(_position.X)))
            {
                fixbody = true;
                _position.X = 128f;
            }
            else if (_position.X < 0.0f)
            {
                fixbody = true;
                _position.X = 0.1f;
            }
            else if (_position.X > m_parent_scene.WorldExtents.X - 0.1f)
            {
                fixbody = true;
                _position.X = m_parent_scene.WorldExtents.X - 0.1f;
            }

            if ((Single.IsNaN(_position.Y) || Single.IsInfinity(_position.Y)))
            {
                fixbody = true;
                _position.Y = 128f;
            }
            else if (_position.Y < 0.0f)
            {
                fixbody = true;
                _position.Y = 0.1f;
            }
            else if (_position.Y > m_parent_scene.WorldExtents.Y - 0.1f)
            {
                fixbody = true;
                _position.Y = m_parent_scene.WorldExtents.Y - 0.1f;
            }

            if ((Single.IsNaN(_position.Z) || Single.IsInfinity(_position.Z)))
            {
                fixbody = true;
                _position.Z = 128f;
            }

            if (fixbody)
            {
                m_freemove = false;
                UBOdeNative.BodySetPosition(Body, _position.X, _position.Y, _position.Z);
            }

            if (!m_pidControllerActive)
                _zeroPosition = _position;

            //Update AABB
            UpdateAABB2D();

            float aabbminz = _position.Z - CapsuleSizeZ;
            //float aabbmaxz = _position.Z + CapsuleSizeZ;

            bool tviszero = m_targetVelocity.IsZero();

            Vector3 ctv;
            if (tviszero)
                ctv = Vector3.Zero;
            else
            {
                if (m_alwaysRun)
                {
                    ctv = new( m_targetVelocity.X * m_runMultiplier,
                               m_targetVelocity.Y * m_runMultiplier,
                               m_targetVelocity.Z);
                }
                else
                {
                    ctv = new ( m_targetVelocity.X * m_walkMultiplier,
                                m_targetVelocity.Y * m_walkMultiplier,
                               m_targetVelocity.Z);
                }
            }

            Vector3 vel = UBOdeNative.BodyGetLinearVelOMV(Body);

            //******************************************
            // colide with land
            float tmpX = _position.X;
            float tmpY = _position.Y;
            if (m_flying)
            {
                tmpX += vel.X * m_sceneTimeStep;
                tmpY += vel.Y * m_sceneTimeStep;
            }

            Vector3 vec = Vector3.Zero;
            float terrainheight = m_parent_scene.GetTerrainHeightAtXY(tmpX, tmpY);
            if (aabbminz < terrainheight)
            {
                if (ctv.Z < 0f)
                    ctv.Z = 0f;

                if (!m_haveLastFallVel)
                {
                    m_lastFallVel = vel;
                    m_haveLastFallVel = true;
                }

                float pidp50 = PID_P * 50;
                float depth = terrainheight - aabbminz;
                vec.Z = depth * pidp50;

                Vector3 n = m_parent_scene.GetTerrainNormalAtXY(tmpX, tmpY);
                if (!m_flying)
                {
                    vec.Z -= vel.Z * PID_D;

                    if (n.Z < 0.4f)
                    {
                        vec.X = depth * pidp50 - vel.X * PID_D;
                        vec.X *= n.X;
                        vec.Y = depth * pidp50 - vel.Y * PID_D;
                        vec.Y *= n.Y;
                        vec.Z *= n.Z;
                        if (n.Z < 0.1f)
                        {
                            // cancel the slope pose
                            n.X = 0f;
                            n.Y = 0f;
                            n.Z = 1.0f;
                        }
                    }
                }

                if (depth < 0.2f)
                {
                    m_colliderGroundfilter++;
                    if (m_colliderGroundfilter > 2)
                    {
                        m_iscolliding = true;
                        Colliderfilter = 2;

                        if (m_colliderGroundfilter > 10)
                        {
                            m_colliderGroundfilter = 10;
                            m_freemove = false;
                        }

                        CollideNormal = n;

                        m_iscollidingGround = true;

                        ContactPoint contact = new()
                        {
                            PenetrationDepth = depth,
                            Position = new( _position.X, _position.Y, terrainheight),
                            SurfaceNormal = -n,
                            RelativeSpeed = Vector3.Dot(m_lastFallVel, n),
                            CharacterFeet = true
                        };
                        AddCollisionEvent(0,contact);
                        m_lastFallVel = Vector3.Zero;
                    }
                }

                else
                {
                    m_colliderGroundfilter -= 5;
                    if (m_colliderGroundfilter <= 0)
                    {
                        m_colliderGroundfilter = 0;
                        m_iscollidingGround = false;
                    }
                }
            }
            else
            {
                m_haveLastFallVel = false;
                m_colliderGroundfilter -= 5;
                if (m_colliderGroundfilter <= 0)
                {
                    m_colliderGroundfilter = 0;
                    m_iscollidingGround = false;
                }
            }

            bool hoverPIDActive = false;

            if (m_useHoverPID && m_PIDHoverTau != 0 && m_PIDHoverHeight != 0)
            {
                hoverPIDActive = true;

                switch (m_PIDHoverType)
                {
                    case PIDHoverType.Ground:
                        m_targetHoverHeight = terrainheight + m_PIDHoverHeight;
                        break;

                    case PIDHoverType.GroundAndWater:
                        if (terrainheight > m_parent_scene.WaterLevel)
                            m_targetHoverHeight = terrainheight + m_PIDHoverHeight;
                        else
                            m_targetHoverHeight = m_parent_scene.WaterLevel + m_PIDHoverHeight;
                        break;
                }     // end switch (m_PIDHoverType)

                // don't go underground
                if (m_targetHoverHeight > terrainheight + _position.Z)
                {
                    float fz = (m_targetHoverHeight - _position.Z);

                    //  if error is zero, use position control; otherwise, velocity control
                    if (MathF.Abs(fz) < 0.01f)
                    {
                        ctv.Z = 0;
                    }
                    else
                    {
                        _zeroFlag = false;
                        fz /= m_PIDHoverTau;

                        float tmp = MathF.Abs(fz);
                        if (tmp > 50f)
                            fz = 50f * MathF.Sign(fz);
                        else if (tmp < 0.1f)
                            fz = 0.1f * MathF.Sign(fz);

                        ctv.Z = fz;
                    }
                }
            }

            //******************************************
            if (!m_iscolliding)
                CollideNormal.Z = 0;

            if (!tviszero)
            {
                m_freemove = false;

                // movement relative to surface if moving on it
                // dont disturbe vertical movement, ie jumps
                if (m_iscolliding && !m_flying && ctv.Z == 0f && CollideNormal.Z > 0.2f && CollideNormal.Z < 0.94f)
                {
                    float p = ctv.X * CollideNormal.X + ctv.Y * CollideNormal.Y;
                    ctv.X *= MathF.Sqrt(1 - CollideNormal.X * CollideNormal.X);
                    ctv.Y *= MathF.Sqrt(1 - CollideNormal.Y * CollideNormal.Y);
                    ctv.Z -= p;
                    if (ctv.Z < 0f)
                        ctv.Z *= 2f;
                }
            }

            float breakfactor;
            if (!m_freemove)
            {
                //  if velocity is zero, use position control; otherwise, velocity control
                if (tviszero)
                {
                    if (m_iscolliding || m_flying)
                    {
                        //  keep track of where we stopped.  No more slippin' & slidin'
                        if (!_zeroFlag)
                        {
                            _zeroFlag = true;
                            _zeroPosition = _position;
                            if(!m_pidControllerActive)
                            {
                                float pidd0833 = PID_D * 0.833f;
                                vec.X -= vel.X * pidd0833;
                                vec.Y -= vel.Y * pidd0833;
                            }
                        }
                        if (m_pidControllerActive)
                        {
                            // We only want to deactivate the PID Controller if we think we want to have our surrogate
                            // react to the physics scene by moving it's position.
                            // Avatar to Avatar collisions
                            // Prim to avatar collisions
                            float pidd2 = PID_D * 2f;
                            float pidp5 = PID_P * 5;

                            vec.X = -vel.X * pidd2 + (_zeroPosition.X - _position.X) * pidp5;
                            vec.Y = -vel.Y * pidd2 + (_zeroPosition.Y - _position.Y) * pidp5;
                            if (vel.Z > 0)
                                vec.Z += -vel.Z * PID_D + (_zeroPosition.Z - _position.Z) * PID_P;
                            else
                                vec.Z += (-vel.Z * PID_D + (_zeroPosition.Z - _position.Z) * PID_P) * 0.2f;
                        }
                    }
                    else
                    {
                        _zeroFlag = false;
                        float pidd0833 = PID_D * 0.833f;
                        vec.X += (ctv.X - vel.X) * pidd0833;
                        vec.Y += (ctv.Y - vel.Y) * pidd0833;
                        // hack for  breaking on fall
                        if (ctv.Z == -9999f)
                            vec.Z += -vel.Z * PID_D - m_scenegravityForceZ;
                    }
                }
                else
                {
                    m_pidControllerActive = true;
                    _zeroFlag = false;

                    if (m_iscolliding)
                    {
                        if (!m_flying)
                        {
                            // we are on a surface
                            if (ctv.Z > 0f)
                            {
                                // moving up or JUMPING
                                vec.Z += (ctv.Z - vel.Z) * PID_D * 2f;
                                vec.X += (ctv.X - vel.X) * PID_D;
                                vec.Y += (ctv.Y - vel.Y) * PID_D;
                            }
                            else
                            {
                                // we are moving down on a surface
                                if (ctv.Z == 0)
                                {
                                    if (vel.Z > 0)
                                        vec.Z -= vel.Z * PID_D * 2f;
                                    vec.X += (ctv.X - vel.X) * PID_D;
                                    vec.Y += (ctv.Y - vel.Y) * PID_D;
                                }
                                // intencionally going down
                                else
                                {
                                    if (ctv.Z < vel.Z)
                                        vec.Z += (ctv.Z - vel.Z) * PID_D;
                                    else
                                    {
                                    }

                                    if (MathF.Abs(ctv.X) > MathF.Abs(vel.X))
                                        vec.X += (ctv.X - vel.X) * PID_D;
                                    if (MathF.Abs(ctv.Y) > MathF.Abs(vel.Y))
                                        vec.Y += (ctv.Y - vel.Y) * PID_D;
                                }
                            }

                            // We're standing on something
                        }
                        else
                        {
                            // We're flying and colliding with something
                            float pidd00625 = PID_D * 0.0625f;
                            vec.X += (ctv.X - vel.X) * pidd00625;
                            vec.Y += (ctv.Y - vel.Y) * pidd00625;
                            vec.Z += (ctv.Z - vel.Z) * pidd00625;
                        }
                    }
                    else // ie not colliding
                    {
                        if (m_flying || hoverPIDActive) //(!m_iscolliding && flying)
                        {
                            // we're in mid air suspended
                            vec.X += (ctv.X - vel.X) * PID_D;
                            vec.Y += (ctv.Y - vel.Y) * PID_D;
                            vec.Z += (ctv.Z - vel.Z) * PID_D;
                        }

                        else
                        {
                            // we're not colliding and we're not flying so that means we're falling!
                            // m_iscolliding includes collisions with the ground.

                            // d.Vector3 pos = d.BodyGetPosition(Body);
                            float pidd0833 = PID_D * 0.833f;
                            vec.X += (ctv.X - vel.X) * pidd0833;
                            vec.Y += (ctv.Y - vel.Y) * pidd0833;
                            // hack for  breaking on fall
                            if (ctv.Z == -9999f)
                                vec.Z += -vel.Z * PID_D - m_scenegravityForceZ;
                        }
                    }
                }
                float velLengthSquared = vel.LengthSquared();
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
                if (m_flying)
                    vec.Z -= 0.5f * breakfactor * vel.Z;
                else
                    vec.Z -= .16f * m_mass * vel.Z;
            }

            if (m_flying || hoverPIDActive)
            {
                vec.Z -= m_scenegravityForceZ;

                if (!hoverPIDActive)
                {
                    //Added for auto fly height. Kitto Flora
                    float target_altitude = terrainheight + MinimumGroundFlightOffset;

                    if (_position.Z < target_altitude)
                    {
                        vec.Z += (target_altitude - _position.Z) * PID_P * 5.0f;
                    }
                    // end add Kitto Flora
                }
            }
            else if (m_buoyancy != 0.0)
            {
                vec.Z -= m_scenegravityForceZ * m_buoyancy;
            }

            if ((vec.Z != 0 || vec.X != 0 || vec.Y != 0))
                UBOdeNative.BodyAddForce(Body, vec.X, vec.Y, vec.Z);

            if (_zeroFlag)
            {
                _velocity = Vector3.Zero;
                _acceleration = Vector3.Zero;
                m_rotationalVelocity = Vector3.Zero;
            }
            else
            {
                Vector3 a = _velocity; // previous velocity
                SetSmooth(ref _velocity, ref vel, 2);
                a = (_velocity - a) * m_sceneInverseTimeStep;
                SetSmooth(ref _acceleration, ref a, 2);

                m_rotationalVelocity = UBOdeNative.BodyGetAngularVelOMVforAvatar(Body);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void round(ref Vector3 v, int digits)
        {
            v.X = MathF.Round(v.X, digits);
            v.Y = MathF.Round(v.Y, digits);
            v.Z = MathF.Round(v.Z, digits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSmooth(ref Vector3 dst, ref Vector3 value, int rounddigits)
        {
            dst.X = 0.4f * dst.X + 0.6f * value.X;
            dst.X = MathF.Round(dst.X, rounddigits);

            dst.Y = 0.4f * dst.Y + 0.6f * value.Y;
            dst.Y = MathF.Round(dst.Y, rounddigits);

            dst.Z = 0.4f * dst.Z + 0.6f * value.Z;
            dst.Z = MathF.Round(dst.Z, rounddigits);
        }

        /// <summary>
        /// Updates the reported position and velocity.
        /// Used to copy variables from unmanaged space at heartbeat rate and also trigger scene updates acording
        /// also outbounds checking
        /// copy and outbounds now done in move(..) at ode rate
        ///
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdatePositionAndVelocity()
        {
        }

        /// <summary>
        /// Cleanup the things we use in the scene.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Destroy()
        {
            AddChange(changes.Remove, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void CrossingFailure()
        {
        }

        public override Vector3 PIDTarget
        {
            set
            {
                return;
            }
        }
        public override bool PIDActive
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_pidControllerActive;
            }
            set
            {
            }
        }
        public override float PIDTau
        {
            set
            {
            }
        }

        public override float PIDHoverHeight
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                AddChange(changes.PIDHoverHeight, value);
            }
        }
        public override bool PIDHoverActive
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_useHoverPID;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                AddChange(changes.PIDHoverActive, value);
            }
        }

        public override PIDHoverType PIDHoverType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                AddChange(changes.PIDHoverType, value);
            }
        }

        public override float PIDHoverTau
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                float tmp = 0;
                if (value > 0)
                {
                    float mint = m_sceneTimeStep < 0.05f ? 0.05f : m_sceneTimeStep;
                    tmp = value < mint ? mint : value;
                }
                AddChange(changes.PIDHoverTau, tmp);
            }
        }

        public override Quaternion APIDTarget
        {
            set
            {
            }
        }

        public override bool APIDActive
        {
            set
            {
            }
        }

        public override float APIDStrength
        {
            set
            {
            }
        }

        public override float APIDDamping
        {
            set
            {
                return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SubscribeEvents(int ms)
        {
            m_eventsubscription = ms;
            m_cureventsubscription = 0;
            CollisionEventsThisFrame.Clear();
            SentEmptyCollisionsEvent = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void UnSubscribeEvents()
        {
            m_eventsubscription = 0;
            m_parent_scene.RemoveCollisionEventReporting(this);
            lock (CollisionEventsThisFrame)
                CollisionEventsThisFrame.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AddCollisionEvent(uint CollidedWith, ContactPoint contact)
        {
            lock (CollisionEventsThisFrame)
                CollisionEventsThisFrame.AddCollider(CollidedWith, contact);
            m_parent_scene.AddCollisionEventReporting(this);
        }

        public void SendCollisions(int timestep)
        {
            if (m_cureventsubscription < 50000)
                m_cureventsubscription += timestep;

            if (m_cureventsubscription < m_eventsubscription)
                return;

            if (Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
                return;

            lock (CollisionEventsThisFrame)
            {
                int ncolisions = CollisionEventsThisFrame.m_objCollisionList.Count;

                if (!SentEmptyCollisionsEvent || ncolisions > 0)
                {
                    base.SendCollisionUpdate(CollisionEventsThisFrame);
                    m_cureventsubscription = 0;

                    if (ncolisions == 0)
                    {
                        SentEmptyCollisionsEvent = true;
                        //_parent_scene.RemoveCollisionEventReporting(this);
                    }
                    else
                    {
                        SentEmptyCollisionsEvent = false;
                        CollisionEventsThisFrame.Clear();
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool SubscribedEvents()
        {
            if (m_eventsubscription > 0)
                return true;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePhysicsStatus(bool NewStatus)
        {
            if (NewStatus != m_isPhysical)
            {
                if (NewStatus)
                {
                    AvatarGeomAndBodyDestroy();
                    AvatarGeomAndBodyCreate();

                    m_parent_scene.actor_name_map[collider] = this;
                    m_parent_scene.AddCharacter(this);
                }
                else
                {
                    m_parent_scene.RemoveCollisionEventReporting(this);
                    m_parent_scene.RemoveCharacter(this);
                    // destroy avatar capsule and related ODE data
                    AvatarGeomAndBodyDestroy();
                }
                m_freemove = false;
                m_isPhysical = NewStatus;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeAdd()
        {
            changePhysicsStatus(true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeRemove()
        {
            changePhysicsStatus(false);
        }

        /*
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeShape(PrimitiveBaseShape arg)
        {
        }
        */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeAvatarSize(strAvatarSize st)
        {
            changeSize(st.size);
        }

        private void changeSize(Vector3 pSize)
        {
            if (pSize.IsFinite())
            {
                // for now only look to Z changes since viewers also don't change X and Y
                if (pSize.Z != m_size.Z)
                {
                    float oldsz = m_size.Z;
                    m_size = pSize * 0.5f;

                    float sz = m_size.Z;

                    float bot = -sz;
                    boneOff = bot + 0.3f;

                    float feetsz = sz * 0.9f;
                    if (feetsz > 0.6f)
                        feetsz = 0.6f;
                    feetOff = bot + feetsz;

                    float sx = m_size.X;
                    AvaSizeXsq = sx;
                    AvaSizeXsq *= AvaSizeXsq;

                    float sy = m_size.Y;
                    AvaSizeYsq = sy;
                    AvaSizeYsq *= AvaSizeYsq;

                    CapsuleRadius = MathF.Max(sx, sy);
                    float l = sz - CapsuleRadius;
                    CapsuleSizeZ= sz;

                    UBOdeNative.GeomCapsuleSetParams(collider, CapsuleRadius, 2f * l);

                    m_mass = 8f * m_density * sx * sy * sz;  // update mass
                    m_massInvTimeScaled = m_mass * m_sceneInverseTimeStep;
                    PID_D = basePID_D * m_massInvTimeScaled;
                    PID_P = basePID_P * m_massInvTimeScaled;
                    UBOdeNative.MassSetBoxTotal(out ShellMass, m_mass, 2f * sx, 2f * sy, 2f * sz);
                    UBOdeNative.BodySetMass(Body, ref ShellMass);

                    m_scenegravityForceZ = m_parent_scene.gravityz * m_mass;

                    _position.Z += sz - oldsz;
                    UBOdeNative.BodySetPosition(Body, _position.X, _position.Y, _position.Z);

                    UpdateAABB2D();

                    m_bodydisablecontrol = 0;
                    _zeroFlag = false;
                    _velocity = Vector3.Zero;
                    m_targetVelocity = Vector3.Zero;
                }
                m_freemove = false;
                m_pidControllerActive = true;
            }
            else
            {
                m_log.Warn("[PHYSICS]: Got a NaN Size from Scene on a Character");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePosition(Vector3 newPos)
        {
            if (Body != IntPtr.Zero)
            {
                UBOdeNative.BodySetPosition(Body, newPos.X, newPos.Y, newPos.Z);
                UBOdeNative.BodyEnable(Body);
            }
            _position = newPos;
            UpdateAABB2D();

            m_freemove = false;
            _zeroFlag = false;
            m_pidControllerActive = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeOrientation(Quaternion newOri)
        {
            if (m_orientation.NotEqual(newOri))
            {
      
                m_orientation = newOri;
                Orientation2D.X = newOri.Z;
                Orientation2D.Y = newOri.W;
                Orientation2D.Normalize();

                m_NativeOrientation2D.X = 0;
                m_NativeOrientation2D.Y = 0;
                m_NativeOrientation2D.Z = Orientation2D.X;
                m_NativeOrientation2D.W = Orientation2D.Y;

                if (Body != IntPtr.Zero)
                {
                    UBOdeNative.BodySetQuaternion(Body, ref m_NativeOrientation2D);
                    UBOdeNative.BodyEnable(Body);
                }
            }
            else if (Body != IntPtr.Zero)
                UBOdeNative.BodyEnable(Body);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeVelocity(Vector3 newVel)
        {
            _velocity = newVel;
            setFreeMove();

            if (Body != IntPtr.Zero)
            {
                UBOdeNative.BodySetLinearVel(Body, newVel.X, newVel.Y, newVel.Z);
                UBOdeNative.BodyEnable(Body);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeTargetVelocity(Vector3 newVel)
        {
            //m_pidControllerActive = true;
            //m_freemove = false;
            m_targetVelocity = newVel;
            if (Body != IntPtr.Zero)
                UBOdeNative.BodyEnable(Body);
        }
        /*
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeSetTorque(Vector3 newTorque)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeAddForce(Vector3 newForce)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeAddAngularForce(Vector3 arg)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeAngularLock(byte arg)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeFloatOnWater(bool arg)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeVolumedetetion(bool arg)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeSelectedStatus(bool arg)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeDisable(bool arg)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeBuilding(bool arg)
        {
        }
        */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void setFreeMove()
        {
            m_pidControllerActive = true;
            _zeroFlag = false;
            m_targetVelocity = Vector3.Zero;
            m_freemove = true;
            Colliderfilter = int.MinValue;
            m_colliderObjectfilter = -1;
            m_colliderGroundfilter = -1;

            m_iscolliding = false;
            m_iscollidingGround = false;
            m_iscollidingObj = false;

            CollisionEventsThisFrame.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeForce(Vector3 newForce)
        {
            setFreeMove();

            if (Body != IntPtr.Zero)
            {
                if (newForce.X != 0f || newForce.Y != 0f || newForce.Z != 0)
                    UBOdeNative.BodyAddForce(Body, newForce.X, newForce.Y, newForce.Z);
                UBOdeNative.BodyEnable(Body);
            }
        }

        // for now momentum is actually velocity
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeMomentum(Vector3 newmomentum)
        {
            _velocity = newmomentum;
            setFreeMove();

            if (Body != IntPtr.Zero)
            {
                UBOdeNative.BodySetLinearVel(Body, newmomentum.X, newmomentum.Y, newmomentum.Z);
                UBOdeNative.BodyEnable(Body);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePIDHoverHeight(float val)
        {
            m_PIDHoverHeight = val;
            if (val == 0)
                m_useHoverPID = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePIDHoverType(PIDHoverType type)
        {
            m_PIDHoverType = type;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePIDHoverTau(float tau)
        {
            m_PIDHoverTau = tau;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePIDHoverActive(bool active)
        {
            m_useHoverPID = active;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void donullchange()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateAABB2D()
        {
            _AABB2D.minx = _position.X - CapsuleRadius;
            _AABB2D.maxx = _position.X + CapsuleRadius;
            _AABB2D.miny = _position.Y - CapsuleRadius;
            _AABB2D.maxy = _position.Y + CapsuleRadius;
        }

        public bool DoAChange(changes what, object arg)
        {
            if (collider == IntPtr.Zero && what != changes.Add && what != changes.Remove)
            {
                return false;
            }

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

                /*
                case changes.PosOffset:
                    donullchange();
                    break;

                case changes.OriOffset:
                    donullchange();
                    break;
                */
                case changes.Velocity:
                    changeVelocity((Vector3)arg);
                    break;

                case changes.TargetVelocity:
                    changeTargetVelocity((Vector3)arg);
                    break;

                /*
                case changes.Acceleration:
                    changeacceleration((Vector3)arg);
                    break;

                case changes.AngVelocity:
                    changeangvelocity((Vector3)arg);
                    break;
                */
                case changes.Force:
                    changeForce((Vector3)arg);
                    break;
                /*
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
                    changeAngularLock((byte)arg);
                    break;
                */
                case changes.Size:
                    changeSize((Vector3)arg);
                    break;

                case changes.AvatarSize:
                    changeAvatarSize((strAvatarSize)arg);
                    break;

                case changes.Momentum:
                    changeMomentum((Vector3)arg);
                    break;

                case changes.PIDHoverHeight:
                    changePIDHoverHeight((float)arg);
                    break;

                case changes.PIDHoverType:
                    changePIDHoverType((PIDHoverType)arg);
                    break;

                case changes.PIDHoverTau:
                    changePIDHoverTau((float)arg);
                    break;

                case changes.PIDHoverActive:
                    changePIDHoverActive((bool)arg);
                    break;

                /*
                case changes.Shape:
                    changeShape((PrimitiveBaseShape)arg);
                    break;

                case changes.CollidesWater:
                    changeFloatOnWater((bool)arg);
                    break;

                case changes.VolumeDtc:
                    changeVolumedetetion((bool)arg);
                    break;
                */
                case changes.Physical:
                    changePhysicsStatus((bool)arg);
                    break;
                /*
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
                //case changes.Null:
                default:
                    break;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddChange(changes what, object arg)
        {
            m_parent_scene.AddChange(this, what, arg);
        }

        private struct strAvatarSize
        {
            public Vector3 size;
        }
    }
}
