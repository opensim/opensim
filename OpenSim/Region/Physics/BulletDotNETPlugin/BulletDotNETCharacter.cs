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
using System.Reflection;
using BulletDotNET;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using log4net;

namespace OpenSim.Region.Physics.BulletDotNETPlugin
{
    public class BulletDotNETCharacter : PhysicsActor
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public btRigidBody Body;
        public btCollisionShape Shell;
        public btVector3 tempVector1;
        public btVector3 tempVector2;
        public btVector3 tempVector3;
        public btVector3 tempVector4;

        public btVector3 tempVector5RayCast;
        public btVector3 tempVector6RayCast;
        public btVector3 tempVector7RayCast;

        public btQuaternion tempQuat1;
        public btTransform tempTrans1;

        public ClosestNotMeRayResultCallback ClosestCastResult;
        private btTransform m_bodyTransform;
        private btVector3 m_bodyPosition;
        private btVector3 m_CapsuleOrientationAxis;
        private btQuaternion m_bodyOrientation;
        private btDefaultMotionState m_bodyMotionState;
        private btGeneric6DofConstraint m_aMotor;
        // private Vector3 m_movementComparision;
        private Vector3 m_position;
        private Vector3 m_zeroPosition;
        private bool m_zeroFlag = false;
        private bool m_lastUpdateSent = false;
        private Vector3 m_velocity;
        private Vector3 m_target_velocity;
        private Vector3 m_acceleration;
        private Vector3 m_rotationalVelocity;
        private bool m_pidControllerActive = true;
        public float PID_D = 80.0f;
        public float PID_P = 90.0f;
        public float CAPSULE_RADIUS = 0.37f;
        public float CAPSULE_LENGTH = 2.140599f;
        public float heightFudgeFactor = 0.52f;
        public float walkDivisor = 1.3f;
        public float runDivisor = 0.8f;
        private float m_mass = 80f;
        public float m_density = 60f;
        private bool m_flying = false;
        private bool m_iscolliding = false;
        private bool m_iscollidingGround = false;
        private bool m_wascolliding = false;
        private bool m_wascollidingGround = false;
        private bool m_iscollidingObj = false;
        private bool m_alwaysRun = false;
        private bool m_hackSentFall = false;
        private bool m_hackSentFly = false;
        public uint m_localID = 0;
        public bool m_returnCollisions = false;
        // taints and their non-tainted counterparts
        public bool m_isPhysical = false; // the current physical status
        public bool m_tainted_isPhysical = false; // set when the physical status is tainted (false=not existing in physics engine, true=existing)
        private float m_tainted_CAPSULE_LENGTH; // set when the capsule length changes. 
        private bool m_taintRemove = false;
        // private bool m_taintedPosition = false;
        // private Vector3 m_taintedPosition_value;
        private Vector3 m_taintedForce;

        private float m_buoyancy = 0f;

        // private CollisionLocker ode;

        // private string m_name = String.Empty;

        private bool[] m_colliderarr = new bool[11];
        private bool[] m_colliderGroundarr = new bool[11];



        private BulletDotNETScene m_parent_scene;

        public int m_eventsubscription = 0;
        // private CollisionEventUpdate CollisionEventsThisFrame = new CollisionEventUpdate();

        public BulletDotNETCharacter(string avName, BulletDotNETScene parent_scene, Vector3 pos, Vector3 size, float pid_d, float pid_p, float capsule_radius, float tensor, float density, float height_fudge_factor, float walk_divisor, float rundivisor)
        {
            m_position = pos;
            m_zeroPosition = pos;
            m_parent_scene = parent_scene;
            PID_D = pid_d;
            PID_P = pid_p;
            CAPSULE_RADIUS = capsule_radius;
            m_density = density;
            heightFudgeFactor = height_fudge_factor;
            walkDivisor = walk_divisor;
            runDivisor = rundivisor;
            
            for (int i = 0; i < 11; i++)
            {
                m_colliderarr[i] = false;
            }
            for (int i = 0; i < 11; i++)
            {
                m_colliderGroundarr[i] = false;
            }
            CAPSULE_LENGTH = (size.Z * 1.15f) - CAPSULE_RADIUS * 2.0f;
            m_tainted_CAPSULE_LENGTH = CAPSULE_LENGTH;
            m_isPhysical = false; // current status: no ODE information exists
            m_tainted_isPhysical = true; // new tainted status: need to create ODE information

            m_parent_scene.AddPhysicsActorTaint(this);
            
            // m_name = avName;
            tempVector1 = new btVector3(0, 0, 0);
            tempVector2 = new btVector3(0, 0, 0);
            tempVector3 = new btVector3(0, 0, 0);
            tempVector4 = new btVector3(0, 0, 0);

            tempVector5RayCast = new btVector3(0, 0, 0);
            tempVector6RayCast = new btVector3(0, 0, 0);
            tempVector7RayCast = new btVector3(0, 0, 0);

            tempQuat1 = new btQuaternion(0, 0, 0, 1);
            tempTrans1 = new btTransform(tempQuat1, tempVector1);
            // m_movementComparision = new PhysicsVector(0, 0, 0);
            m_CapsuleOrientationAxis = new btVector3(1, 0, 1);
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

            Shell = new btCapsuleShape(CAPSULE_RADIUS, CAPSULE_LENGTH);

            if (m_bodyPosition == null)
                m_bodyPosition = new btVector3(npositionX, npositionY, npositionZ);

            m_bodyPosition.setValue(npositionX, npositionY, npositionZ);

            if (m_bodyOrientation == null)
                m_bodyOrientation = new btQuaternion(m_CapsuleOrientationAxis, (Utils.DEG_TO_RAD * 90));

            if (m_bodyTransform == null)
                m_bodyTransform = new btTransform(m_bodyOrientation, m_bodyPosition);
            else
            {
                m_bodyTransform.Dispose();
                m_bodyTransform = new btTransform(m_bodyOrientation, m_bodyPosition);
            }

            if (m_bodyMotionState == null)
                m_bodyMotionState = new btDefaultMotionState(m_bodyTransform);
            else
                m_bodyMotionState.setWorldTransform(m_bodyTransform);

            m_mass = Mass;

            Body = new btRigidBody(m_mass, m_bodyMotionState, Shell);
            Body.setUserPointer(new IntPtr((int)Body.Handle));
            
            if (ClosestCastResult != null)
                ClosestCastResult.Dispose();
            ClosestCastResult = new ClosestNotMeRayResultCallback(Body);

            m_parent_scene.AddRigidBody(Body);
            Body.setActivationState(4);
            if (m_aMotor != null)
            {
                if (m_aMotor.Handle != IntPtr.Zero)
                {
                    m_parent_scene.getBulletWorld().removeConstraint(m_aMotor);
                    m_aMotor.Dispose();
                }
                m_aMotor = null;
            }

            m_aMotor = new btGeneric6DofConstraint(Body, m_parent_scene.TerrainBody,
                                                                         m_parent_scene.TransZero,
                                                                         m_parent_scene.TransZero, false);
            m_aMotor.setAngularLowerLimit(m_parent_scene.VectorZero);
            m_aMotor.setAngularUpperLimit(m_parent_scene.VectorZero);
            
           
        }
        public void Remove()
        {
            m_taintRemove = true;
        }
        public override bool Stopped
        {
            get { return m_zeroFlag; }
        }

        public override Vector3 Size
        {
            get { return new Vector3(CAPSULE_RADIUS * 2, CAPSULE_RADIUS * 2, CAPSULE_LENGTH); }
            set
            {
                m_pidControllerActive = true;

                Vector3 SetSize = value;
                    m_tainted_CAPSULE_LENGTH = (SetSize.Z * 1.15f) - CAPSULE_RADIUS * 2.0f;
                    //m_log.Info("[SIZE]: " + CAPSULE_LENGTH.ToString());

                    Velocity = Vector3.Zero;
               
                m_parent_scene.AddPhysicsActorTaint(this);
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

        public override PrimitiveBaseShape Shape
        {
            set { return; }
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


        public override void CrossingFailure()
        {
            
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

        public override Vector3 Position
        {
            get { return m_position; }
            set
            {
                // m_taintedPosition_value = value;
                m_position = value;
                // m_taintedPosition = true;
            }
        }

        public override float Mass
        {
            get
            {
                float AVvolume = (float)(Math.PI * Math.Pow(CAPSULE_RADIUS, 2) * CAPSULE_LENGTH);
                return m_density * AVvolume;
            }
        }

        public override Vector3 Force
        {
            get { return m_target_velocity; }
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

        public override Vector3 GeometricCenter
        {
            get { return Vector3.Zero; }
        }

        public override Vector3 CenterOfMass
        {
            get { return Vector3.Zero; }
        }

        public override Vector3 Velocity
        {
            get
            {
                if (m_zeroFlag)
                    return Vector3.Zero;
                m_lastUpdateSent = false;
                return m_velocity;
            }
            set
            {
                m_pidControllerActive = true;
                m_target_velocity = value;
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

        public override Vector3 Acceleration
        {
            get { return m_acceleration; }
        }

        public override Quaternion Orientation
        {
            get { return Quaternion.Identity; }
            set
            {

            }
        }

        public override int PhysicsActorType
        {
            get { return (int)ActorTypes.Agent; }
            set { return; }
        }

        public override bool IsPhysical
        {
            get { return false; }
            set { return; }
        }

        public override bool Flying
        {
            get { return m_flying; }
            set { m_flying = value; }
        }

        public override bool SetAlwaysRun
        {
            get { return m_alwaysRun; }
            set { m_alwaysRun = value; }
        }


        public override bool ThrottleUpdates
        {
            get { return false; }
            set { return; }
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
                m_log.DebugFormat("[PHYSICS]: TrueCount:{0}, FalseCount:{1}",truecount,falsecount);
                if (falsecount > 1.2 * truecount)
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

                if (falsecount > 1.2 * truecount)
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
                if (value)
                    m_pidControllerActive = false;
                else
                    m_pidControllerActive = true;
            }
        }


        public override bool FloatOnWater
        {
            set { return; }
        }

        public override Vector3 RotationalVelocity
        {
            get { return m_rotationalVelocity; }
            set { m_rotationalVelocity = value; }
        }

        public override bool Kinematic
        {
            get { return false; }
            set { }
        }

        public override float Buoyancy
        {
            get { return m_buoyancy; }
            set { m_buoyancy = value; }
        }

        public override Vector3 PIDTarget { set { return; } }
        public override bool PIDActive { set { return; } }
        public override float PIDTau { set { return; } }

        public override bool PIDHoverActive
        {
            set { return; }
        }

        public override float PIDHoverHeight
        {
            set { return; }
        }

        public override PIDHoverType PIDHoverType
        {
            set { return; }
        }

        public override float PIDHoverTau
        {
            set { return; }
        }

        /// <summary>
        /// Adds the force supplied to the Target Velocity
        /// The PID controller takes this target velocity and tries to make it a reality
        /// </summary>
        /// <param name="force"></param>
        /// <param name="pushforce">Is this a push by a script?</param>
        public override void AddForce(Vector3 force, bool pushforce)
        {
            if (pushforce)
            {
                m_pidControllerActive = false;
                force *= 100f;
                doForce(force, false);
                //System.Console.WriteLine("Push!");
                //_target_velocity.X += force.X;
                // _target_velocity.Y += force.Y;
                //_target_velocity.Z += force.Z;
            }
            else
            {
                m_pidControllerActive = true;
                m_target_velocity.X += force.X;
                m_target_velocity.Y += force.Y;
                m_target_velocity.Z += force.Z;
            }
            //m_lastUpdateSent = false;
        }

        public void doForce(Vector3 force, bool now)
        {

            tempVector3.setValue(force.X, force.Y, force.Z);
            if (now)
            {
                Body.applyCentralForce(tempVector3);
            }
            else
            {
                m_taintedForce += force;
                m_parent_scene.AddPhysicsActorTaint(this);
            }
        }

        public void doImpulse(Vector3 force, bool now)
        {

            tempVector3.setValue(force.X, force.Y, force.Z);
            if (now)
            {
                Body.applyCentralImpulse(tempVector3);
            }
            else
            {
                m_taintedForce += force;
                m_parent_scene.AddPhysicsActorTaint(this);
            }
        }

        public override void AddAngularForce(Vector3 force, bool pushforce)
        {

        }

        public override void SetMomentum(Vector3 momentum)
        {
            
        }

        public override void SubscribeEvents(int ms)
        {
            m_eventsubscription = ms;
            m_parent_scene.addCollisionEventReporting(this);
        }

        public override void UnSubscribeEvents()
        {
             m_parent_scene.remCollisionEventReporting(this);
            m_eventsubscription = 0;
        }

        public override bool SubscribedEvents()
        {
            if (m_eventsubscription > 0)
                return true;
            return false;
        }

        internal void Dispose()
        {
            if (Body.isInWorld())
                m_parent_scene.removeFromWorld(Body);

            if (m_aMotor.Handle != IntPtr.Zero)
                m_parent_scene.getBulletWorld().removeConstraint(m_aMotor);

            m_aMotor.Dispose(); m_aMotor = null;
            ClosestCastResult.Dispose(); ClosestCastResult = null;
            Body.Dispose(); Body = null;
            Shell.Dispose(); Shell = null;
            tempQuat1.Dispose();
            tempTrans1.Dispose();
            tempVector1.Dispose();
            tempVector2.Dispose();
            tempVector3.Dispose();
            tempVector4.Dispose();
            tempVector5RayCast.Dispose();
            tempVector6RayCast.Dispose();

        }

        public void ProcessTaints(float timestep)
        {

            if (m_tainted_isPhysical != m_isPhysical)
            {
                if (m_tainted_isPhysical)
                {
                    // Create avatar capsule and related ODE data
                    if (!(Shell == null && Body == null))
                    {
                        m_log.Warn("[PHYSICS]: re-creating the following avatar ODE data, even though it already exists - "
                            + (Shell != null ? "Shell " : "")
                            + (Body != null ? "Body " : ""));
                    }
                    AvatarGeomAndBodyCreation(m_position.X, m_position.Y, m_position.Z);

                   
                }
                else
                {
                    // destroy avatar capsule and related ODE data

                    Dispose();
                    tempVector1 = new btVector3(0, 0, 0);
                    tempVector2 = new btVector3(0, 0, 0);
                    tempVector3 = new btVector3(0, 0, 0);
                    tempVector4 = new btVector3(0, 0, 0);

                    tempVector5RayCast = new btVector3(0, 0, 0);
                    tempVector6RayCast = new btVector3(0, 0, 0);
                    tempVector7RayCast = new btVector3(0, 0, 0);

                    tempQuat1 = new btQuaternion(0, 0, 0, 1);
                    tempTrans1 = new btTransform(tempQuat1, tempVector1);
                    // m_movementComparision = new PhysicsVector(0, 0, 0);
                    m_CapsuleOrientationAxis = new btVector3(1, 0, 1);
                }

                m_isPhysical = m_tainted_isPhysical;
            }

            if (m_tainted_CAPSULE_LENGTH != CAPSULE_LENGTH)
            {
                if (Body != null)
                {

                    m_pidControllerActive = true;
                    // no lock needed on _parent_scene.OdeLock because we are called from within the thread lock in OdePlugin's simulate()
                    //d.JointDestroy(Amotor);
                    float prevCapsule = CAPSULE_LENGTH;
                    CAPSULE_LENGTH = m_tainted_CAPSULE_LENGTH;
                    //m_log.Info("[SIZE]: " + CAPSULE_LENGTH.ToString());
                    Dispose();

                    tempVector1 = new btVector3(0, 0, 0);
                    tempVector2 = new btVector3(0, 0, 0);
                    tempVector3 = new btVector3(0, 0, 0);
                    tempVector4 = new btVector3(0, 0, 0);

                    tempVector5RayCast = new btVector3(0, 0, 0);
                    tempVector6RayCast = new btVector3(0, 0, 0);
                    tempVector7RayCast = new btVector3(0, 0, 0);

                    tempQuat1 = new btQuaternion(0, 0, 0, 1);
                    tempTrans1 = new btTransform(tempQuat1, tempVector1);
                    // m_movementComparision = new PhysicsVector(0, 0, 0);
                    m_CapsuleOrientationAxis = new btVector3(1, 0, 1);

                    AvatarGeomAndBodyCreation(m_position.X, m_position.Y,
                                      m_position.Z + (Math.Abs(CAPSULE_LENGTH - prevCapsule) * 2));
                    Velocity = Vector3.Zero;

                }
                else
                {
                    m_log.Warn("[PHYSICS]: trying to change capsule size, but the following ODE data is missing - "
                        + (Shell == null ? "Shell " : "")
                        + (Body == null ? "Body " : ""));
                }
            }
            if (m_taintRemove)
            {
                Dispose();
            }
        }

        /// <summary>
        /// Called from Simulate
        /// This is the avatar's movement control + PID Controller
        /// </summary>
        /// <param name="timeStep"></param>
        public void Move(float timeStep)
        {
            //  no lock; for now it's only called from within Simulate()

            // If the PID Controller isn't active then we set our force
            // calculating base velocity to the current position
            if (Body == null)
                return;
            tempTrans1.Dispose();
            tempTrans1 = Body.getInterpolationWorldTransform();
            tempVector1.Dispose();
            tempVector1 = tempTrans1.getOrigin();
            tempVector2.Dispose();
            tempVector2 = Body.getInterpolationLinearVelocity();

            if (m_pidControllerActive == false)
            {
                m_zeroPosition.X = tempVector1.getX();
                m_zeroPosition.Y = tempVector1.getY();
                m_zeroPosition.Z = tempVector1.getZ();
            }
            //PidStatus = true;

            Vector3 vec = Vector3.Zero;

            Vector3 vel = new Vector3(tempVector2.getX(), tempVector2.getY(), tempVector2.getZ());

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
            if (m_target_velocity.X == 0.0f && m_target_velocity.Y == 0.0f && m_target_velocity.Z == 0.0f && m_iscolliding)
            {
                //  keep track of where we stopped.  No more slippin' & slidin'
                if (!m_zeroFlag)
                {
                    m_zeroFlag = true;
                    m_zeroPosition.X = tempVector1.getX();
                    m_zeroPosition.Y = tempVector1.getY();
                    m_zeroPosition.Z = tempVector1.getZ();
                }
                if (m_pidControllerActive)
                {
                    // We only want to deactivate the PID Controller if we think we want to have our surrogate
                    // react to the physics scene by moving it's position.
                    // Avatar to Avatar collisions
                    // Prim to avatar collisions

                    Vector3 pos = new Vector3(tempVector1.getX(), tempVector1.getY(), tempVector1.getZ());
                    vec.X = (m_target_velocity.X - vel.X) * (PID_D) + (m_zeroPosition.X - pos.X) * (PID_P * 2);
                    vec.Y = (m_target_velocity.Y - vel.Y) * (PID_D) + (m_zeroPosition.Y - pos.Y) * (PID_P * 2);
                    if (m_flying)
                    {
                        vec.Z = (m_target_velocity.Z - vel.Z) * (PID_D) + (m_zeroPosition.Z - pos.Z) * PID_P;
                    }
                }
                //PidStatus = true;
            }
            else
            {
                m_pidControllerActive = true;
                m_zeroFlag = false;
                if (m_iscolliding && !m_flying)
                {
                    // We're standing on something
                    vec.X = ((m_target_velocity.X / movementdivisor) - vel.X) * (PID_D);
                    vec.Y = ((m_target_velocity.Y / movementdivisor) - vel.Y) * (PID_D);
                }
                else if (m_iscolliding && m_flying)
                {
                    // We're flying and colliding with something
                    vec.X = ((m_target_velocity.X / movementdivisor) - vel.X) * (PID_D / 16);
                    vec.Y = ((m_target_velocity.Y / movementdivisor) - vel.Y) * (PID_D / 16);
                }
                else if (!m_iscolliding && m_flying)
                {
                    // we're in mid air suspended
                    vec.X = ((m_target_velocity.X / movementdivisor) - vel.X) * (PID_D / 6);
                    vec.Y = ((m_target_velocity.Y / movementdivisor) - vel.Y) * (PID_D / 6);

                    // We don't want linear velocity to cause our avatar to bounce, so we check target Z and actual velocity X, Y
                    // rebound preventing
                    if (m_target_velocity.Z < 0.025f && m_velocity.X < 0.25f && m_velocity.Y < 0.25f)
                        m_zeroFlag = true;
                }

                if (m_iscolliding && !m_flying && m_target_velocity.Z > 0.0f)
                {
                    // We're colliding with something and we're not flying but we're moving
                    // This means we're walking or running.
                    Vector3 pos = new Vector3(tempVector1.getX(), tempVector1.getY(), tempVector1.getZ());
                    vec.Z = (m_target_velocity.Z - vel.Z) * PID_D + (m_zeroPosition.Z - pos.Z) * PID_P;
                    if (m_target_velocity.X > 0)
                    {
                        vec.X = ((m_target_velocity.X - vel.X) / 1.2f) * PID_D;
                    }
                    if (m_target_velocity.Y > 0)
                    {
                        vec.Y = ((m_target_velocity.Y - vel.Y) / 1.2f) * PID_D;
                    }
                }
                else if (!m_iscolliding && !m_flying)
                {
                    // we're not colliding and we're not flying so that means we're falling!
                    // m_iscolliding includes collisions with the ground.

                    // d.Vector3 pos = d.BodyGetPosition(Body);
                    if (m_target_velocity.X > 0)
                    {
                        vec.X = ((m_target_velocity.X - vel.X) / 1.2f) * PID_D;
                    }
                    if (m_target_velocity.Y > 0)
                    {
                        vec.Y = ((m_target_velocity.Y - vel.Y) / 1.2f) * PID_D;
                    }
                }


                if (m_flying)
                {
                    vec.Z = (m_target_velocity.Z - vel.Z) * (PID_D);
                }
            }
            if (m_flying)
            {
                // Slight PID correction
                vec.Z += (((-1 * m_parent_scene.gravityz) * m_mass) * 0.06f);


                //auto fly height. Kitto Flora
                //d.Vector3 pos = d.BodyGetPosition(Body);
                float target_altitude = m_parent_scene.GetTerrainHeightAtXY(m_position.X, m_position.Y) + 5.0f;

                if (m_position.Z < target_altitude)
                {
                    vec.Z += (target_altitude - m_position.Z) * PID_P * 5.0f;
                }

            }
            if (Body != null && (((m_target_velocity.X > 0.2f || m_target_velocity.X < -0.2f) || (m_target_velocity.Y > 0.2f || m_target_velocity.Y < -0.2f))))
            {
                Body.setFriction(0.001f);
                //m_log.DebugFormat("[PHYSICS]: Avatar force applied: {0}, Target:{1}", vec.ToString(), m_target_velocity.ToString());
            }

            if (Body != null)
            {
                int activationstate = Body.getActivationState();
                if (activationstate == 0)
                {
                    Body.forceActivationState(1);
                }
               

            }
            doImpulse(vec, true);
        }

        /// <summary>
        /// Updates the reported position and velocity.  This essentially sends the data up to ScenePresence.
        /// </summary>
        public void UpdatePositionAndVelocity()
        {
            if (Body == null)
                return;
            //int val = Environment.TickCount;
            CheckIfStandingOnObject();
            //m_log.DebugFormat("time:{0}", Environment.TickCount - val);

            //IsColliding = Body.checkCollideWith(m_parent_scene.TerrainBody);
            
            tempTrans1.Dispose();
            tempTrans1 = Body.getInterpolationWorldTransform();
            tempVector1.Dispose();
            tempVector1 = tempTrans1.getOrigin();
            tempVector2.Dispose();
            tempVector2 = Body.getInterpolationLinearVelocity();

            //  no lock; called from Simulate() -- if you call this from elsewhere, gotta lock or do Monitor.Enter/Exit!
            Vector3 vec = new Vector3(tempVector1.getX(), tempVector1.getY(), tempVector1.getZ());

            //  kluge to keep things in bounds.  ODE lets dead avatars drift away (they should be removed!)
            if (vec.X < -10.0f) vec.X = 0.0f;
            if (vec.Y < -10.0f) vec.Y = 0.0f;
            if (vec.X > (int)Constants.RegionSize + 10.2f) vec.X = (int)Constants.RegionSize + 10.2f;
            if (vec.Y > (int)Constants.RegionSize + 10.2f) vec.Y = (int)Constants.RegionSize + 10.2f;

            m_position.X = vec.X;
            m_position.Y = vec.Y;
            m_position.Z = vec.Z;

            // Did we move last? = zeroflag
            // This helps keep us from sliding all over

            if (m_zeroFlag)
            {
                m_velocity.X = 0.0f;
                m_velocity.Y = 0.0f;
                m_velocity.Z = 0.0f;

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
                vec = new Vector3(tempVector2.getX(), tempVector2.getY(), tempVector2.getZ());
                m_velocity.X = (vec.X);
                m_velocity.Y = (vec.Y);

                m_velocity.Z = (vec.Z);
                //m_log.Debug(m_target_velocity);
                if (m_velocity.Z < -6 && !m_hackSentFall)
                {
                    m_hackSentFall = true;
                    m_pidControllerActive = false;
                }
                else if (m_flying && !m_hackSentFly)
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
            if (Body != null)
            {
                if (Body.getFriction() < 0.9f)
                    Body.setFriction(0.9f);
            }
            //if (Body != null)
            //    Body.clearForces();
        }

        public void CheckIfStandingOnObject()
        {
           
            float capsuleHalfHeight = ((CAPSULE_LENGTH + 2*CAPSULE_RADIUS)*0.5f);

            tempVector5RayCast.setValue(m_position.X, m_position.Y, m_position.Z);
            tempVector6RayCast.setValue(m_position.X, m_position.Y, m_position.Z - 1 * capsuleHalfHeight * 1.1f);


            ClosestCastResult.Dispose();
            ClosestCastResult = new ClosestNotMeRayResultCallback(Body);

            try
            {
                m_parent_scene.getBulletWorld().rayTest(tempVector5RayCast, tempVector6RayCast, ClosestCastResult);
            }
            catch (AccessViolationException)
            {
                m_log.Debug("BAD!");
            }
            if (ClosestCastResult.hasHit())
            {
                
                if (tempVector7RayCast != null)
                    tempVector7RayCast.Dispose();

                //tempVector7RayCast = ClosestCastResult.getHitPointWorld();

                /*if (tempVector7RayCast == null) // null == no result also
                {
                    CollidingObj = false;
                    IsColliding = false;
                    CollidingGround = false;
                   
                    return;
                }
                float zVal = tempVector7RayCast.getZ();
                if (zVal != 0)
                    m_log.Debug("[PHYSICS]: HAAAA");
                if (zVal < m_position.Z && zVal > ((CAPSULE_LENGTH + 2 * CAPSULE_RADIUS) *0.5f))
                {
                    CollidingObj = true;
                    IsColliding = true;
                }
                else
                {
                    CollidingObj = false;
                    IsColliding = false;
                    CollidingGround = false;
                }*/

                //height+2*radius = capsule full length
                //CollidingObj = true;
                //IsColliding = true;
                m_iscolliding = true;
            }
            else
            {
                //CollidingObj = false;
                //IsColliding = false;
                //CollidingGround = false;
                m_iscolliding = false;
            }
        }
    }

}
