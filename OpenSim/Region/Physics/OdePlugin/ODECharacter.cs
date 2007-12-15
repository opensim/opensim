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
* 
*/

using System;
using System.Collections.Generic;
using Axiom.Math;
using Ode.NET;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.OdePlugin
{
    public class OdeCharacter : PhysicsActor
    {
        private PhysicsVector _position;
        private d.Vector3 _zeroPosition;
        private bool _zeroFlag = false;
        private bool m_lastUpdateSent = false;
        private PhysicsVector _velocity;
        private PhysicsVector _target_velocity;
        private PhysicsVector _acceleration;
        private PhysicsVector m_rotationalVelocity;
        private bool m_pidControllerActive = true;
        private static float PID_D = 3020.0f;
        private static float PID_P = 7000.0f;
        private static float POSTURE_SERVO = 10000.0f;
        public static float CAPSULE_RADIUS = 0.5f;
        public float CAPSULE_LENGTH = 0.79f;
        private bool flying = false;
        private bool m_iscolliding = false;
        private bool m_iscollidingGround = false;
        private bool m_wascolliding = false;
        private bool m_wascollidingGround = false;
        private bool m_iscollidingObj = false;
        private bool m_wascollidingObj = false;
        private bool m_alwaysRun = false;
        private bool m_hackSentFall = false;
        private bool m_hackSentFly = false;
        private string m_name = "";

        private bool[] m_colliderarr = new bool[11];
        private bool[] m_colliderGroundarr = new bool[11];


        private bool jumping = false;
        //private float gravityAccel;
        public IntPtr Body;
        private OdeScene _parent_scene;
        public IntPtr Shell;
        public d.Mass ShellMass;
        public bool collidelock = false;

        public OdeCharacter(String avName, OdeScene parent_scene, PhysicsVector pos)
        {
            _velocity = new PhysicsVector();
            _target_velocity = new PhysicsVector();
            _position = pos;
            _acceleration = new PhysicsVector();
            _parent_scene = parent_scene;

            for (int i = 0; i < 11; i++)
            {
                m_colliderarr[i] = false;
            }

            lock (OdeScene.OdeLock)
            {

                Shell = d.CreateCapsule(parent_scene.space, CAPSULE_RADIUS, CAPSULE_LENGTH);
                d.MassSetCapsule(out ShellMass, 50.0f, 3, 0.4f, 1.0f);
                Body = d.BodyCreate(parent_scene.world);
                d.BodySetMass(Body, ref ShellMass);
                d.BodySetPosition(Body, pos.X, pos.Y, pos.Z);
                d.GeomSetBody(Shell, Body);
            }
            m_name = avName;
            parent_scene.geom_name_map[Shell] = avName;
            parent_scene.actor_name_map[Shell] = (PhysicsActor)this;
        }
        public override int PhysicsActorType
        {
            get { return (int)ActorTypes.Agent; }
            set { return; }
        }
        public override bool SetAlwaysRun
        {
            get { return m_alwaysRun; }
            set { m_alwaysRun = value; }
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
                    base.SendCollisionUpdate(new CollisionEventUpdate());
                    
                }
                m_wascolliding = m_iscolliding;
            }
        }
        public override bool CollidingGround
        {
            get { return m_iscollidingGround; }
            set
            {
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
        public override bool CollidingObj
        {
            get { return m_iscollidingObj; }
            set { 
                m_iscollidingObj = value;
                if (value)
                    m_pidControllerActive = false;
                else
                    m_pidControllerActive = true;
            }
        }

        public override PhysicsVector Position
        {
            get { return _position; }
            set
            {
                lock (OdeScene.OdeLock)
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
        public override PhysicsVector Size
        {
            get { return new PhysicsVector(CAPSULE_RADIUS * 2, CAPSULE_RADIUS * 2, CAPSULE_LENGTH); }
            set
            {
                m_pidControllerActive = true;
                lock (OdeScene.OdeLock)
                {
                    PhysicsVector SetSize = value;
                    float prevCapsule = CAPSULE_LENGTH;
                    float capsuleradius = CAPSULE_RADIUS;
                    capsuleradius = 0.2f;

                    CAPSULE_LENGTH = (SetSize.Z - ((SetSize.Z * 0.43f))); // subtract 43% of the size
                    d.BodyDestroy(Body);
                    d.GeomDestroy(Shell);
                    //MainLog.Instance.Verbose("PHYSICS", "Set Avatar Height To: " + (CAPSULE_RADIUS + CAPSULE_LENGTH));
                    Shell = d.CreateCapsule(_parent_scene.space, capsuleradius, CAPSULE_LENGTH);
                    d.MassSetCapsule(out ShellMass, 50.0f, 3, CAPSULE_RADIUS, CAPSULE_LENGTH);
                    Body = d.BodyCreate(_parent_scene.world);
                    d.BodySetMass(Body, ref ShellMass);
                    d.BodySetPosition(Body, _position.X, _position.Y, _position.Z + Math.Abs(CAPSULE_LENGTH - prevCapsule));
                    d.GeomSetBody(Shell, Body);
                }
                _parent_scene.geom_name_map[Shell] = m_name;
                _parent_scene.actor_name_map[Shell] = (PhysicsActor)this;
            }
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
            get { return _velocity; }
            set {
                m_pidControllerActive = true;
                _target_velocity = value; }
        }

        public override bool Kinematic
        {
            get { return false; }
            set { }
        }

        public override Quaternion Orientation
        {
            get { return Quaternion.Identity; }
            set { }
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

        public override void AddForce(PhysicsVector force)
        {
            m_pidControllerActive = true;
            _target_velocity.X += force.X;
            _target_velocity.Y += force.Y;
            _target_velocity.Z += force.Z;

            //m_lastUpdateSent = false;
        }
        public void doForce(PhysicsVector force)
        {
            if (!collidelock)
            {
                d.BodyAddForce(Body, force.X, force.Y, force.Z);

                //  ok -- let's stand up straight!
                d.Vector3 feet;
                d.Vector3 head;
                d.BodyGetRelPointPos(Body, 0.0f, 0.0f, -1.0f, out feet);
                d.BodyGetRelPointPos(Body, 0.0f, 0.0f, 1.0f, out head);
                float posture = head.Z - feet.Z;

                // restoring force proportional to lack of posture:
                float servo = (2.5f - posture) * POSTURE_SERVO;
                d.BodyAddForceAtRelPos(Body, 0.0f, 0.0f, servo, 0.0f, 0.0f, 1.0f);
                d.BodyAddForceAtRelPos(Body, 0.0f, 0.0f, -servo, 0.0f, 0.0f, -1.0f);
                //m_lastUpdateSent = false;

            }

        }
        public override void SetMomentum(PhysicsVector momentum)
        {

        }

        public void Move(float timeStep)
        {
            //  no lock; for now it's only called from within Simulate()
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
                movementdivisor = 1.3f;
            }
            else
            {
                movementdivisor = 0.8f;

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
                    d.Vector3 pos = d.BodyGetPosition(Body);
                    vec.X = (_target_velocity.X - vel.X) * PID_D + (_zeroPosition.X - pos.X) * PID_P;
                    vec.Y = (_target_velocity.Y - vel.Y) * PID_D + (_zeroPosition.Y - pos.Y) * PID_P;
                    if (flying)
                    {
                        vec.Z = (_target_velocity.Z - vel.Z) * (PID_D + 5100) + (_zeroPosition.Z - pos.Z) * PID_P;
                    }
                }
                //PidStatus = true;
            }
            else
            {
                m_pidControllerActive = true;
                _zeroFlag = false;
                if (m_iscolliding || flying)
                {

                    vec.X = ((_target_velocity.X / movementdivisor) - vel.X) * PID_D;
                    vec.Y = ((_target_velocity.Y / movementdivisor) - vel.Y) * PID_D;
                }
                if (m_iscolliding && !flying && _target_velocity.Z > 0.0f)
                {
                    d.Vector3 pos = d.BodyGetPosition(Body);
                    vec.Z = (_target_velocity.Z - vel.Z) * PID_D + (_zeroPosition.Z - pos.Z) * PID_P;
                    if (_target_velocity.X > 0)
                    {
                        vec.X = ((_target_velocity.X - vel.X) / 1.2f) * PID_D;
                    }
                    if (_target_velocity.Y > 0)
                    {
                        vec.Y = ((_target_velocity.Y - vel.Y) / 1.2f) * PID_D;
                    }
                }
                else if (!m_iscolliding && !flying)
                {
                    d.Vector3 pos = d.BodyGetPosition(Body);
                    if (_target_velocity.X > 0)
                    {
                        vec.X = ((_target_velocity.X - vel.X) / 1.2f) * PID_D;
                    }
                    if (_target_velocity.Y > 0)
                    {
                        vec.Y = ((_target_velocity.Y - vel.Y) / 1.2f) * PID_D;
                    }

                }


                if (flying)
                {
                    vec.Z = (_target_velocity.Z - vel.Z) * (PID_D + 5100);
                }
            }
            if (flying)
            {
                vec.Z += 10.0f;
            }


            doForce(vec);
        }

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

            if (_zeroFlag)
            {
                _velocity.X = 0.0f;
                _velocity.Y = 0.0f;
                _velocity.Z = 0.0f;
                if (!m_lastUpdateSent)
                {
                    m_lastUpdateSent = true;
                    base.RequestPhysicsterseUpdate();
                    string primScenAvatarIn = _parent_scene.whichspaceamIin(_position);
                    int[] arrayitem = _parent_scene.calculateSpaceArrayItemFromPos(_position);
                    //if (primScenAvatarIn == "0")
                    //{
                        //MainLog.Instance.Verbose("Physics", "Avatar " + m_name + " in space with no prim. Arr:':" + arrayitem[0].ToString() + "," + arrayitem[1].ToString());
                    //}
                    //else
                    //{
                    //    MainLog.Instance.Verbose("Physics", "Avatar " + m_name + " in Prim space':" + primScenAvatarIn + ". Arr:" + arrayitem[0].ToString() + "," + arrayitem[1].ToString());
                    //}
                    
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
                    base.SendCollisionUpdate(new CollisionEventUpdate());
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

        public void Destroy()
        {
            lock (OdeScene.OdeLock)
            {
                d.GeomDestroy(Shell);
                _parent_scene.geom_name_map.Remove(Shell);
                d.BodyDestroy(Body);
            }
        }
    }

}
