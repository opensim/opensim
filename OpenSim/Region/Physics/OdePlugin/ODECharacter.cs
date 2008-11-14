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
using OpenMetaverse;
using Ode.NET;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

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
        ERP = 7,
        StopCFM = 8,
        LoStop2 = 256,
        HiStop2 = 257,
        LoStop3 = 512,
        HiStop3 = 513
    }
    public class OdeCharacter : PhysicsActor
    {
        //private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private PhysicsVector _position;
        private d.Vector3 _zeroPosition;
        // private d.Matrix3 m_StandUpRotation;
        private bool _zeroFlag = false;
        private bool m_lastUpdateSent = false;
        private PhysicsVector _velocity;
        private PhysicsVector _target_velocity;
        private PhysicsVector _acceleration;
        private PhysicsVector m_rotationalVelocity;
        private float m_mass = 80f;
        public float m_density = 60f;
        private bool m_pidControllerActive = true;
        public float PID_D = 800.0f;
        public float PID_P = 900.0f;
        //private static float POSTURE_SERVO = 10000.0f;
        public float CAPSULE_RADIUS = 0.37f;
        public float CAPSULE_LENGTH = 2.140599f;
        public float m_tensor = 3800000f;
        public float heightFudgeFactor = 0.52f;
        public float walkDivisor = 1.3f;
        public float runDivisor = 0.8f;
        private bool flying = false;
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

        private float m_buoyancy = 0f;

        // private CollisionLocker ode;

        private string m_name = String.Empty;

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
        public IntPtr Body;
        private OdeScene _parent_scene;
        public IntPtr Shell;
        public IntPtr Amotor;
        public d.Mass ShellMass;
        public bool collidelock = false;

        public int m_eventsubscription = 0;
        private CollisionEventUpdate CollisionEventsThisFrame = new CollisionEventUpdate();

        public OdeCharacter(String avName, OdeScene parent_scene, PhysicsVector pos, CollisionLocker dode, PhysicsVector size, float pid_d, float pid_p, float capsule_radius, float tensor, float density, float height_fudge_factor, float walk_divisor, float rundivisor)
        {
            // ode = dode;
            _velocity = new PhysicsVector();
            _target_velocity = new PhysicsVector();
            _position = pos;
            _acceleration = new PhysicsVector();
            _parent_scene = parent_scene;

            PID_D = pid_d;
            PID_P = pid_p;
            CAPSULE_RADIUS = capsule_radius;
            m_tensor = tensor;
            m_density = density;
            heightFudgeFactor = height_fudge_factor;
            walkDivisor = walk_divisor;
            runDivisor = rundivisor;


            // m_StandUpRotation =
            //     new d.Matrix3(0.5f, 0.7071068f, 0.5f, -0.7071068f, 0f, 0.7071068f, 0.5f, -0.7071068f,
            //                   0.5f);

            for (int i = 0; i < 11; i++)
            {
                m_colliderarr[i] = false;
            }
            CAPSULE_LENGTH = (size.Z * 1.15f) - CAPSULE_RADIUS * 2.0f;
            //m_log.Info("[SIZE]: " + CAPSULE_LENGTH.ToString());

            lock (_parent_scene.OdeLock)
            {
                AvatarGeomAndBodyCreation(pos.X, pos.Y, pos.Z, m_tensor);
            }
            m_name = avName;
            parent_scene.geom_name_map[Shell] = avName;
            parent_scene.actor_name_map[Shell] = (PhysicsActor) this;
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
            set { flying = value; }
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
                if (value)
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
        public override PhysicsVector Position
        {
            get { return _position; }
            set
            {
                lock (_parent_scene.OdeLock)
                {
                    d.BodySetPosition(Body, value.X, value.Y, value.Z);
                    _position = value;
                }
            }
        }

        public override PhysicsVector RotationalVelocity
        {
            get { return m_rotationalVelocity; }
            set { m_rotationalVelocity = value; }
        }

        /// <summary>
        /// This property sets the height of the avatar only.  We use the height to make sure the avatar stands up straight
        /// and use it to offset landings properly
        /// </summary>
        public override PhysicsVector Size
        {
            get { return new PhysicsVector(CAPSULE_RADIUS*2, CAPSULE_RADIUS*2, CAPSULE_LENGTH); }
            set
            {
                m_pidControllerActive = true;
                lock (_parent_scene.OdeLock)
                {
                    d.JointDestroy(Amotor);

                    PhysicsVector SetSize = value;
                    float prevCapsule = CAPSULE_LENGTH;
                    // float capsuleradius = CAPSULE_RADIUS;
                    //capsuleradius = 0.2f;

                    CAPSULE_LENGTH = (SetSize.Z * 1.15f) - CAPSULE_RADIUS * 2.0f;
                    //m_log.Info("[SIZE]: " + CAPSULE_LENGTH.ToString());
                    d.BodyDestroy(Body);

                    _parent_scene.waitForSpaceUnlock(_parent_scene.space);

                    d.GeomDestroy(Shell);
                    AvatarGeomAndBodyCreation(_position.X, _position.Y,
                                      _position.Z + (Math.Abs(CAPSULE_LENGTH - prevCapsule) * 2), m_tensor);
                    Velocity = new PhysicsVector(0f, 0f, 0f);

                }
                _parent_scene.geom_name_map[Shell] = m_name;
                _parent_scene.actor_name_map[Shell] = (PhysicsActor) this;
            }
        }
        
        /// <summary>
        /// This creates the Avatar's physical Surrogate at the position supplied
        /// </summary>
        /// <param name="npositionX"></param>
        /// <param name="npositionY"></param>
        /// <param name="npositionZ"></param>
        private void AvatarGeomAndBodyCreation(float npositionX, float npositionY, float npositionZ, float tensor)
        {

            int dAMotorEuler = 1;
            _parent_scene.waitForSpaceUnlock(_parent_scene.space);
            Shell = d.CreateCapsule(_parent_scene.space, CAPSULE_RADIUS, CAPSULE_LENGTH);

            d.GeomSetCategoryBits(Shell, (int)m_collisionCategories);
            d.GeomSetCollideBits(Shell, (int)m_collisionFlags);

            d.MassSetCapsuleTotal(out ShellMass, m_mass, 2, CAPSULE_RADIUS, CAPSULE_LENGTH);
            Body = d.BodyCreate(_parent_scene.world);
            d.BodySetPosition(Body, npositionX, npositionY, npositionZ);

            d.BodySetMass(Body, ref ShellMass);
            d.Matrix3 m_caprot;
            // 90 Stand up on the cap of the capped cyllinder
            d.RFromAxisAndAngle(out m_caprot, 1, 0, 1, (float)(Math.PI / 2));


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
            d.JointSetAMotorParam(Amotor, (int)dParam.LowStop, -0.000000000001f);
            d.JointSetAMotorParam(Amotor, (int)dParam.LoStop3, -0.000000000001f);
            d.JointSetAMotorParam(Amotor, (int)dParam.LoStop2, -0.000000000001f);
            d.JointSetAMotorParam(Amotor, (int)dParam.HiStop, 0.000000000001f);
            d.JointSetAMotorParam(Amotor, (int)dParam.HiStop3, 0.000000000001f);
            d.JointSetAMotorParam(Amotor, (int)dParam.HiStop2, 0.000000000001f);

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
                float AVvolume = (float) (Math.PI*Math.Pow(CAPSULE_RADIUS, 2)*CAPSULE_LENGTH);
                return m_density*AVvolume;
            }
        }
        public override void link(PhysicsActor obj)
        {

        }

        public override void delink()
        {

        }

        public override void LockAngularMotion(PhysicsVector axis)
        {

        }

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
//             //m_log.Info("[PHYSICSAV]: Rotation: " + bodyrotation.M00 + " : " + bodyrotation.M01 + " : " + bodyrotation.M02 + " : " + bodyrotation.M10 + " : " + bodyrotation.M11 + " : " + bodyrotation.M12 + " : " + bodyrotation.M20 + " : " + bodyrotation.M21 + " : " + bodyrotation.M22);
//         }

        public override PhysicsVector Force
        {
            get { return new PhysicsVector(_target_velocity.X, _target_velocity.Y, _target_velocity.Z); }
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

        public override void VehicleVectorParam(int param, PhysicsVector value)
        {

        }

        public override void VehicleRotationParam(int param, Quaternion rotation)
        {

        }


        public override PhysicsVector CenterOfMass
        {
            get { return PhysicsVector.Zero; }
        }

        public override PhysicsVector GeometricCenter
        {
            get { return PhysicsVector.Zero; }
        }

        public override PrimitiveBaseShape Shape
        {
            set { return; }
        }

        public override PhysicsVector Velocity
        {
            get {
                // There's a problem with PhysicsVector.Zero! Don't Use it Here!
                if (_zeroFlag)
                    return new PhysicsVector(0f, 0f, 0f);
                m_lastUpdateSent = false;
                return _velocity;
            }
            set
            {
                m_pidControllerActive = true;
                _target_velocity = value;
            }
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

        public override PhysicsVector Acceleration
        {
            get { return _acceleration; }
        }

        public void SetAcceleration(PhysicsVector accel)
        {
            m_pidControllerActive = true;
            _acceleration = accel;
        }

        /// <summary>
        /// Adds the force supplied to the Target Velocity
        /// The PID controller takes this target velocity and tries to make it a reality
        /// </summary>
        /// <param name="force"></param>
        public override void AddForce(PhysicsVector force, bool pushforce)
        {
            if (pushforce)
            {
                m_pidControllerActive = false;
                force *= 100f;
                doForce(force);
                //System.Console.WriteLine("Push!");
                //_target_velocity.X += force.X;
               // _target_velocity.Y += force.Y;
                //_target_velocity.Z += force.Z;
            }
            else
            {
                m_pidControllerActive = true;
                _target_velocity.X += force.X;
                _target_velocity.Y += force.Y;
                _target_velocity.Z += force.Z;
            }
            //m_lastUpdateSent = false;
        }

        /// <summary>
        /// After all of the forces add up with 'add force' we apply them with doForce
        /// </summary>
        /// <param name="force"></param>
        public void doForce(PhysicsVector force)
        {
            if (!collidelock)
            {
                d.BodyAddForce(Body, force.X, force.Y, force.Z);
                //d.BodySetRotation(Body, ref m_StandUpRotation);
                //standupStraight();

            }
        }

        public override void SetMomentum(PhysicsVector momentum)
        {
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


            if (m_pidControllerActive == false)
            {
                _zeroPosition = d.BodyGetPosition(Body);
            }
            //PidStatus = true;

            PhysicsVector vec = new PhysicsVector();
            d.Vector3 vel = d.BodyGetLinearVel(Body);
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
                    vec.Y = (_target_velocity.Y - vel.Y)*(PID_D) + (_zeroPosition.Y - pos.Y)* (PID_P * 2);
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
                    vec.X = ((_target_velocity.X/movementdivisor) - vel.X)*(PID_D / 16);
                    vec.Y = ((_target_velocity.Y/movementdivisor) - vel.Y)*(PID_D / 16);
                }
                else if (!m_iscolliding && flying)
                {
                    // we're in mid air suspended
                    vec.X = ((_target_velocity.X / movementdivisor) - vel.X) * (PID_D/6);
                    vec.Y = ((_target_velocity.Y / movementdivisor) - vel.Y) * (PID_D/6);
                }

                if (m_iscolliding && !flying && _target_velocity.Z > 0.0f)
                {
                    // We're colliding with something and we're not flying but we're moving
                    // This means we're walking or running.
                    d.Vector3 pos = d.BodyGetPosition(Body);
                    vec.Z = (_target_velocity.Z - vel.Z)*PID_D + (_zeroPosition.Z - pos.Z)*PID_P;
                    if (_target_velocity.X > 0)
                    {
                        vec.X = ((_target_velocity.X - vel.X)/1.2f)*PID_D;
                    }
                    if (_target_velocity.Y > 0)
                    {
                        vec.Y = ((_target_velocity.Y - vel.Y)/1.2f)*PID_D;
                    }
                }
                else if (!m_iscolliding && !flying)
                {
                    // we're not colliding and we're not flying so that means we're falling!
                    // m_iscolliding includes collisions with the ground.

                    // d.Vector3 pos = d.BodyGetPosition(Body);
                    if (_target_velocity.X > 0)
                    {
                        vec.X = ((_target_velocity.X - vel.X)/1.2f)*PID_D;
                    }
                    if (_target_velocity.Y > 0)
                    {
                        vec.Y = ((_target_velocity.Y - vel.Y)/1.2f)*PID_D;
                    }
                }


                if (flying)
                {
                    vec.Z = (_target_velocity.Z - vel.Z) * (PID_D);
                }
            }
            if (flying)
            {
                vec.Z += ((-1 * _parent_scene.gravityz)*m_mass);
            }


            doForce(vec);
        }

        /// <summary>
        /// Updates the reported position and velocity.  This essentially sends the data up to ScenePresence.
        /// </summary>
        public void UpdatePositionAndVelocity()
        {
            //  no lock; called from Simulate() -- if you call this from elsewhere, gotta lock or do Monitor.Enter/Exit!
            d.Vector3 vec = d.BodyGetPosition(Body);

            //  kluge to keep things in bounds.  ODE lets dead avatars drift away (they should be removed!)
            if (vec.X < 0.0f) vec.X = 0.0f;
            if (vec.Y < 0.0f) vec.Y = 0.0f;
            if (vec.X > 255.95f) vec.X = 255.95f;
            if (vec.Y > 255.95f) vec.Y = 255.95f;

            _position.X = vec.X;
            _position.Y = vec.Y;
            _position.Z = vec.Z;

            // Did we move last? = zeroflag
            // This helps keep us from sliding all over

            if (_zeroFlag)
            {
                _velocity.X = 0.0f;
                _velocity.Y = 0.0f;
                _velocity.Z = 0.0f;

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
                vec = d.BodyGetLinearVel(Body);
                _velocity.X = (vec.X);
                _velocity.Y = (vec.Y);

                _velocity.Z = (vec.Z);

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
        /// Cleanup the things we use in the scene.
        /// </summary>
        public void Destroy()
        {
            lock (_parent_scene.OdeLock)
            {
                // Kill the Amotor
                d.JointDestroy(Amotor);

                //kill the Geometry
                _parent_scene.waitForSpaceUnlock(_parent_scene.space);

                d.GeomDestroy(Shell);
                _parent_scene.geom_name_map.Remove(Shell);

                //kill the body
                d.BodyDestroy(Body);
            }
        }

        public override void CrossingFailure()
        {
        }
        public override PhysicsVector PIDTarget { set { return; } }
        public override bool PIDActive { set { return; } }
        public override float PIDTau { set { return; } }
        public override void SubscribeEvents(int ms)
        {
            m_eventsubscription = ms;
            _parent_scene.addCollisionEventReporting(this);
        }
        public override void UnSubscribeEvents()
        {
            _parent_scene.remCollisionEventReporting(this);
            m_eventsubscription = 0;
        }
        public void AddCollisionEvent(uint CollidedWith, float depth)
        {
            if (m_eventsubscription > 0)
                CollisionEventsThisFrame.addCollider(CollidedWith,depth);
        }

        public void SendCollisions()
        {
            if (m_eventsubscription > 0)
            {
                base.SendCollisionUpdate(CollisionEventsThisFrame);
                CollisionEventsThisFrame = new CollisionEventUpdate();
            }
        }
        public override bool SubscribedEvents()
        {
            if (m_eventsubscription > 0)
                return true;
            return false;
        }
    }
}
