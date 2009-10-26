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
using System.Runtime.InteropServices;
using System.Threading;
using log4net;
using OpenMetaverse;
using BulletDotNET;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;


namespace OpenSim.Region.Physics.BulletDotNETPlugin
{
    public class BulletDotNETPrim : PhysicsActor
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Vector3 _position;
        private Vector3 m_zeroPosition;
        private Vector3 _velocity;
        private Vector3 _torque;
        private Vector3 m_lastVelocity;
        private Vector3 m_lastposition;
        private Quaternion m_lastorientation = Quaternion.Identity;
        private Vector3 m_rotationalVelocity;
        private Vector3 _size;
        private Vector3 _acceleration;
        // private d.Vector3 _zeroPosition = new d.Vector3(0.0f, 0.0f, 0.0f);
        private Quaternion _orientation;
        private Vector3 m_taintposition;
        private Vector3 m_taintsize;
        private Vector3 m_taintVelocity;
        private Vector3 m_taintTorque;
        private Quaternion m_taintrot;
        private Vector3 m_angularlock = Vector3.One;
        private Vector3 m_taintAngularLock = Vector3.One;
        // private btGeneric6DofConstraint Amotor;

        private Vector3 m_PIDTarget;
        private float m_PIDTau;
        private float m_PIDHoverHeight;
        private float m_PIDHoverTau;
        private bool m_useHoverPID;
        private PIDHoverType m_PIDHoverType = PIDHoverType.Ground;
        private float m_targetHoverHeight;
        private float m_groundHeight;
        private float m_waterHeight;
        private float PID_D = 35f;
        private float PID_G = 25f;
        // private float m_tensor = 5f;
        // private int body_autodisable_frames = 20;
        private IMesh primMesh;

        private bool m_usePID;

        private const CollisionCategories m_default_collisionFlags = (CollisionCategories.Geom
                                                        | CollisionCategories.Space
                                                        | CollisionCategories.Body
                                                        | CollisionCategories.Character
                                                     );

        private bool m_taintshape;
        private bool m_taintPhysics;
        // private bool m_collidesLand = true;
        private bool m_collidesWater;
        public bool m_returnCollisions;

        // Default we're a Geometry
        // private CollisionCategories m_collisionCategories = (CollisionCategories.Geom);

        // Default, Collide with Other Geometries, spaces and Bodies
        // private CollisionCategories m_collisionFlags = m_default_collisionFlags;

        public bool m_taintremove;
        public bool m_taintdisable;
        public bool m_disabled;
        public bool m_taintadd;
        public bool m_taintselected;
        public bool m_taintCollidesWater;

        public uint m_localID;

        //public GCHandle gc;
        // private CollisionLocker ode;

        private bool m_taintforce;
        private bool m_taintaddangularforce;
        private Vector3 m_force;
        private List<Vector3> m_forcelist = new List<Vector3>();
        private List<Vector3> m_angularforcelist = new List<Vector3>();

        private IMesh _mesh;
        private PrimitiveBaseShape _pbs;
        private BulletDotNETScene _parent_scene;
        public btCollisionShape prim_geom;
        public IntPtr _triMeshData;

        private PhysicsActor _parent;
        private PhysicsActor m_taintparent;

        private List<BulletDotNETPrim> childrenPrim = new List<BulletDotNETPrim>();

        private bool iscolliding;
        private bool m_isphysical;
        private bool m_isSelected;

        internal bool m_isVolumeDetect; // If true, this prim only detects collisions but doesn't collide actively

        private bool m_throttleUpdates;
        // private int throttleCounter;
        public int m_interpenetrationcount;
        public float m_collisionscore;
        public int m_roundsUnderMotionThreshold;
        private int m_crossingfailures;

        public float m_buoyancy;

        public bool outofBounds;
        private float m_density = 10.000006836f; // Aluminum g/cm3;

        public bool _zeroFlag;
        private bool m_lastUpdateSent;


        private String m_primName;
        private Vector3 _target_velocity;

        public int m_eventsubscription;
        // private CollisionEventUpdate CollisionEventsThisFrame = null;

        public volatile bool childPrim;

        private btVector3 tempPosition1;
        private btVector3 tempPosition2;
        private btVector3 tempPosition3;
        private btVector3 tempSize1;
        private btVector3 tempSize2;
        private btVector3 tempLinearVelocity1;
        private btVector3 tempLinearVelocity2;
        private btVector3 tempAngularVelocity1;
        private btVector3 tempAngularVelocity2;
        private btVector3 tempInertia1;
        private btVector3 tempInertia2;
        private btVector3 tempAddForce;
        private btQuaternion tempOrientation1;
        private btQuaternion tempOrientation2;
        private btMotionState tempMotionState1;
        private btMotionState tempMotionState2;
        private btMotionState tempMotionState3;
        private btTransform tempTransform1;
        private btTransform tempTransform2;
        private btTransform tempTransform3;
        private btTransform tempTransform4;
        private btTriangleIndexVertexArray btshapeArray;
        private btVector3 AxisLockAngleHigh;
        private btVector3 AxisLockLinearLow;
        private btVector3 AxisLockLinearHigh;
        private bool forceenable = false;

        private btGeneric6DofConstraint m_aMotor;

        public btRigidBody Body;

        public BulletDotNETPrim(String primName, BulletDotNETScene parent_scene, Vector3 pos, Vector3 size,
                       Quaternion rotation, IMesh mesh, PrimitiveBaseShape pbs, bool pisPhysical)
        {
            tempPosition1 = new btVector3(0, 0, 0);
            tempPosition2 = new btVector3(0, 0, 0);
            tempPosition3 = new btVector3(0, 0, 0);
            tempSize1 = new btVector3(0, 0, 0);
            tempSize2 = new btVector3(0, 0, 0);
            tempLinearVelocity1 = new btVector3(0, 0, 0);
            tempLinearVelocity2 = new btVector3(0, 0, 0);
            tempAngularVelocity1 = new btVector3(0, 0, 0);
            tempAngularVelocity2 = new btVector3(0, 0, 0);
            tempInertia1 = new btVector3(0, 0, 0);
            tempInertia2 = new btVector3(0, 0, 0);
            tempOrientation1 = new btQuaternion(0, 0, 0, 1);
            tempOrientation2 = new btQuaternion(0, 0, 0, 1);
            _parent_scene = parent_scene;
            tempTransform1 = new btTransform(_parent_scene.QuatIdentity, _parent_scene.VectorZero);
            tempTransform2 = new btTransform(_parent_scene.QuatIdentity, _parent_scene.VectorZero); ;
            tempTransform3 = new btTransform(_parent_scene.QuatIdentity, _parent_scene.VectorZero); ;
            tempTransform4 = new btTransform(_parent_scene.QuatIdentity, _parent_scene.VectorZero); ;

            tempMotionState1 = new btDefaultMotionState(_parent_scene.TransZero);
            tempMotionState2 = new btDefaultMotionState(_parent_scene.TransZero);
            tempMotionState3 = new btDefaultMotionState(_parent_scene.TransZero);


            AxisLockLinearLow = new btVector3(-1 * (int)Constants.RegionSize, -1 * (int)Constants.RegionSize, -1 * (int)Constants.RegionSize);
            int regionsize = (int)Constants.RegionSize;

            if (regionsize == 256)
                regionsize = 512;

            AxisLockLinearHigh = new btVector3((int)Constants.RegionSize, (int)Constants.RegionSize, (int)Constants.RegionSize);

            _target_velocity = Vector3.Zero;
            _velocity = Vector3.Zero;
            _position = pos;
            m_taintposition = pos;
            PID_D = parent_scene.bodyPIDD;
            PID_G = parent_scene.bodyPIDG;
            m_density = parent_scene.geomDefaultDensity;
            // m_tensor = parent_scene.bodyMotorJointMaxforceTensor;
            // body_autodisable_frames = parent_scene.bodyFramesAutoDisable;

            prim_geom = null;
            Body = null;

            if (size.X <= 0) size.X = 0.01f;
            if (size.Y <= 0) size.Y = 0.01f;
            if (size.Z <= 0) size.Z = 0.01f;

            _size = size;
            m_taintsize = _size;
            _acceleration = Vector3.Zero;
            m_rotationalVelocity = Vector3.Zero;
            _orientation = rotation;
            m_taintrot = _orientation;
            _mesh = mesh;
            _pbs = pbs;

            _parent_scene = parent_scene;

            if (pos.Z < 0)
                m_isphysical = false;
            else
            {
                m_isphysical = pisPhysical;
                // If we're physical, we need to be in the master space for now.
                // linksets *should* be in a space together..  but are not currently
            }
            m_primName = primName;
            m_taintadd = true;
            _parent_scene.AddPhysicsActorTaint(this);

        }

        #region PhysicsActor overrides

        public override bool Stopped
        {
            get { return _zeroFlag; }
        }

        public override Vector3 Size
        {
            get { return _size; }
            set { _size = value; }
        }

        public override PrimitiveBaseShape Shape
        {
            set
            {
                _pbs = value;
                m_taintshape = true;
            }
        }

        public override uint LocalID
        {
            set
            {
                //m_log.Info("[PHYSICS]: Setting TrackerID: " + value);
                m_localID = value;
            }
        }

        public override bool Grabbed
        {
            set { return; }
        }

        public override bool Selected
        {
            set
            {
                // This only makes the object not collidable if the object
                // is physical or the object is modified somehow *IN THE FUTURE*
                // without this, if an avatar selects prim, they can walk right
                // through it while it's selected
                m_collisionscore = 0;
                if ((m_isphysical && !_zeroFlag) || !value)
                {
                    m_taintselected = value;
                    _parent_scene.AddPhysicsActorTaint(this);
                }
                else
                {
                    m_taintselected = value;
                    m_isSelected = value;
                }
            }
        }

        public override void CrossingFailure()
        {
            m_crossingfailures++;
            if (m_crossingfailures > _parent_scene.geomCrossingFailuresBeforeOutofbounds)
            {
                base.RaiseOutOfBounds(_position);
                return;
            }
            else if (m_crossingfailures == _parent_scene.geomCrossingFailuresBeforeOutofbounds)
            {
                m_log.Warn("[PHYSICS]: Too many crossing failures for: " + m_primName);
            }
        }
        public override void link(PhysicsActor obj)
        {
            m_taintparent = obj;
        }

        public override void delink()
        {
            m_taintparent = null;
        }

        public override void LockAngularMotion(Vector3 axis)
        {
            m_log.DebugFormat("[axislock]: <{0},{1},{2}>", axis.X, axis.Y, axis.Z);
            m_taintAngularLock = axis;
        }

        public override Vector3 Position
        {
            get { return _position; }

            set
            {
                _position = value;
                //m_log.Info("[PHYSICS]: " + _position.ToString());
            }
        }

        public override float Mass
        {
            get { return CalculateMass(); }
        }

        public override Vector3 Force
        {
            //get { return Vector3.Zero; }
            get { return m_force; }
            set { m_force = value; }
        }

        public override int VehicleType
        {
            get { return 0; }
            set { return; }
        }

        public override void VehicleFloatParam(int param, float value)
        {
            //TODO:
        }

        public override void VehicleVectorParam(int param, Vector3 value)
        {
            //TODO:
        }

        public override void VehicleRotationParam(int param, Quaternion rotation)
        {
            //TODO:
        }

        public override void SetVolumeDetect(int param)
        {
            //TODO: GhostObject
            m_isVolumeDetect = (param != 0);

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
                // Averate previous velocity with the new one so
                // client object interpolation works a 'little' better
                Vector3 returnVelocity;
                returnVelocity.X = (m_lastVelocity.X + _velocity.X) / 2;
                returnVelocity.Y = (m_lastVelocity.Y + _velocity.Y) / 2;
                returnVelocity.Z = (m_lastVelocity.Z + _velocity.Z) / 2;
                return returnVelocity;
            }
            set
            {
                _velocity = value;

                m_taintVelocity = value;
                _parent_scene.AddPhysicsActorTaint(this);
            }
        }

        public override Vector3 Torque
        {
            get
            {
                if (!m_isphysical || Body.Handle == IntPtr.Zero)
                    return Vector3.Zero;

                return _torque;
            }

            set
            {
                m_taintTorque = value;
                _parent_scene.AddPhysicsActorTaint(this);
            }
        }

        public override float CollisionScore
        {
            get { return m_collisionscore; }
            set { m_collisionscore = value; }
        }

        public override Vector3 Acceleration
        {
            get { return _acceleration; }
        }

        public override Quaternion Orientation
        {
            get { return _orientation; }
            set { _orientation = value; }
        }

        public override int PhysicsActorType
        {
            get { return (int)ActorTypes.Prim; }
            set { return; }
        }

        public override bool IsPhysical
        {
            get { return m_isphysical; }
            set { m_isphysical = value; }
        }

        public override bool Flying
        {
            // no flying prims for you
            get { return false; }
            set { }
        }

        public override bool SetAlwaysRun
        {
            get { return false; }
            set { return; }
        }

        public override bool ThrottleUpdates
        {
            get { return m_throttleUpdates; }
            set { m_throttleUpdates = value; }
        }

        public override bool IsColliding
        {
            get { return iscolliding; }
            set { iscolliding = value; }
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

        public override bool FloatOnWater
        {
            set
            {
                m_taintCollidesWater = value;
                _parent_scene.AddPhysicsActorTaint(this);
            }
        }

        public override Vector3 RotationalVelocity
        {
            get
            {
                Vector3 pv = Vector3.Zero;
                if (_zeroFlag)
                    return pv;
                m_lastUpdateSent = false;

                if (m_rotationalVelocity.ApproxEquals(pv, 0.2f))
                    return pv;

                return m_rotationalVelocity;
            }
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

        public override Vector3 PIDTarget { set { m_PIDTarget = value; ; } }
        public override bool PIDActive { set { m_usePID = value; } }
        public override float PIDTau { set { m_PIDTau = value; } }

        public override float PIDHoverHeight { set { m_PIDHoverHeight = value; ; } }
        public override bool PIDHoverActive { set { m_useHoverPID = value; } }
        public override PIDHoverType PIDHoverType { set { m_PIDHoverType = value; } }
        public override float PIDHoverTau { set { m_PIDHoverTau = value; } }


        public override void AddForce(Vector3 force, bool pushforce)
        {
            m_forcelist.Add(force);
            m_taintforce = true;
            //m_log.Info("[PHYSICS]: Added Force:" + force.ToString() +  " to prim at " + Position.ToString());
        }

        public override void AddAngularForce(Vector3 force, bool pushforce)
        {
            m_angularforcelist.Add(force);
            m_taintaddangularforce = true;
        }

        public override void SetMomentum(Vector3 momentum)
        {
        }

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

        public override bool SubscribedEvents()
        {
            return (m_eventsubscription > 0);
        }

        #endregion



        internal void Dispose()
        {
            //TODO:
            DisableAxisMotor();
            DisposeOfBody();
            SetCollisionShape(null);

            if (tempMotionState3 != null && tempMotionState3.Handle != IntPtr.Zero)
            {
                tempMotionState3.Dispose();
                tempMotionState3 = null;
            }

            if (tempMotionState2 != null && tempMotionState2.Handle != IntPtr.Zero)
            {
                tempMotionState2.Dispose();
                tempMotionState2 = null;
            }

            if (tempMotionState1 != null && tempMotionState1.Handle != IntPtr.Zero)
            {
                tempMotionState1.Dispose();
                tempMotionState1 = null;
            }

            if (tempTransform4 != null && tempTransform4.Handle != IntPtr.Zero)
            {
                tempTransform4.Dispose();
                tempTransform4 = null;
            }

            if (tempTransform3 != null && tempTransform3.Handle != IntPtr.Zero)
            {
                tempTransform3.Dispose();
                tempTransform3 = null;
            }

            if (tempTransform2 != null && tempTransform2.Handle != IntPtr.Zero)
            {
                tempTransform2.Dispose();
                tempTransform2 = null;
            }

            if (tempTransform1 != null && tempTransform1.Handle != IntPtr.Zero)
            {
                tempTransform1.Dispose();
                tempTransform1 = null;
            }

            if (tempOrientation2 != null && tempOrientation2.Handle != IntPtr.Zero)
            {
                tempOrientation2.Dispose();
                tempOrientation2 = null;
            }

            if (tempOrientation1 != null && tempOrientation1.Handle != IntPtr.Zero)
            {
                tempOrientation1.Dispose();
                tempOrientation1 = null;
            }

            if (tempInertia1 != null && tempInertia1.Handle != IntPtr.Zero)
            {
                tempInertia1.Dispose();
                tempInertia1 = null;
            }

            if (tempInertia2 != null && tempInertia2.Handle != IntPtr.Zero)
            {
                tempInertia2.Dispose();
                tempInertia1 = null;
            }


            if (tempAngularVelocity2 != null && tempAngularVelocity2.Handle != IntPtr.Zero)
            {
                tempAngularVelocity2.Dispose();
                tempAngularVelocity2 = null;
            }

            if (tempAngularVelocity1 != null && tempAngularVelocity1.Handle != IntPtr.Zero)
            {
                tempAngularVelocity1.Dispose();
                tempAngularVelocity1 = null;
            }

            if (tempLinearVelocity2 != null && tempLinearVelocity2.Handle != IntPtr.Zero)
            {
                tempLinearVelocity2.Dispose();
                tempLinearVelocity2 = null;
            }

            if (tempLinearVelocity1 != null && tempLinearVelocity1.Handle != IntPtr.Zero)
            {
                tempLinearVelocity1.Dispose();
                tempLinearVelocity1 = null;
            }

            if (tempSize2 != null && tempSize2.Handle != IntPtr.Zero)
            {
                tempSize2.Dispose();
                tempSize2 = null;
            }

            if (tempSize1 != null && tempSize1.Handle != IntPtr.Zero)
            {
                tempSize1.Dispose();
                tempSize1 = null;
            }

            if (tempPosition3 != null && tempPosition3.Handle != IntPtr.Zero)
            {
                tempPosition3.Dispose();
                tempPosition3 = null;
            }

            if (tempPosition2 != null && tempPosition2.Handle != IntPtr.Zero)
            {
                tempPosition2.Dispose();
                tempPosition2 = null;
            }

            if (tempPosition1 != null && tempPosition1.Handle != IntPtr.Zero)
            {
                tempPosition1.Dispose();
                tempPosition1 = null;
            }
            if (AxisLockLinearLow != null && AxisLockLinearLow.Handle != IntPtr.Zero)
            {
                AxisLockLinearLow.Dispose();
                AxisLockLinearLow = null;
            }
            if (AxisLockLinearHigh != null && AxisLockLinearHigh.Handle != IntPtr.Zero)
            {
                AxisLockLinearHigh.Dispose();
                AxisLockLinearHigh = null;
            }

        }



        public void ProcessTaints(float timestep)
        {
            if (m_taintadd)
            {
                m_log.Debug("[PHYSICS]: TaintAdd");
                changeadd(timestep);
            }

            if (prim_geom == null)
            {
                CreateGeom(IntPtr.Zero, primMesh);

                if (IsPhysical)
                    SetBody(Mass);
                else
                    SetBody(0);
                m_log.Debug("[PHYSICS]: GEOM_DOESNT_EXSIT");
            }

            if (prim_geom.Handle == IntPtr.Zero)
            {
                CreateGeom(IntPtr.Zero, primMesh);

                if (IsPhysical)
                    SetBody(Mass);
                else
                    SetBody(0);
                m_log.Debug("[PHYSICS]: GEOM_DOESNT_EXSIT");

            }

            if (!_position.ApproxEquals(m_taintposition, 0f))
            {
                m_log.Debug("[PHYSICS]: TaintMove");
                changemove(timestep);
            }
            if (m_taintrot != _orientation)
            {
                m_log.Debug("[PHYSICS]: TaintRotate");
                rotate(timestep);
            } //

            if (m_taintPhysics != m_isphysical && !(m_taintparent != _parent))
            {
                m_log.Debug("[PHYSICS]: TaintPhysics");
                changePhysicsStatus(timestep);
            }
            //

            if (!_size.ApproxEquals(m_taintsize, 0f))
            {
                m_log.Debug("[PHYSICS]: TaintSize");
                changesize(timestep);
            }

            //

            if (m_taintshape)
            {
                m_log.Debug("[PHYSICS]: TaintShape");
                changeshape(timestep);
            } //

            if (m_taintforce)
            {
                m_log.Debug("[PHYSICS]: TaintForce");
                changeAddForce(timestep);
            }
            if (m_taintaddangularforce)
            {
                m_log.Debug("[PHYSICS]: TaintAngularForce");
                changeAddAngularForce(timestep);
            }
            if (!m_taintTorque.ApproxEquals(Vector3.Zero, 0.001f))
            {
                m_log.Debug("[PHYSICS]: TaintTorque");
                changeSetTorque(timestep);
            }
            if (m_taintdisable)
            {
                m_log.Debug("[PHYSICS]: TaintDisable");
                changedisable(timestep);
            }
            if (m_taintselected != m_isSelected)
            {
                m_log.Debug("[PHYSICS]: TaintSelected");
                changeSelectedStatus(timestep);
            }
            if (!m_taintVelocity.ApproxEquals(Vector3.Zero, 0.001f))
            {
                m_log.Debug("[PHYSICS]: TaintVelocity");
                changevelocity(timestep);
            }
            if (m_taintparent != _parent)
            {
                m_log.Debug("[PHYSICS]: TaintLink");
                changelink(timestep);
            }
            if (m_taintCollidesWater != m_collidesWater)
            {
                changefloatonwater(timestep);
            }
            if (!m_angularlock.ApproxEquals(m_taintAngularLock, 0))
            {
                m_log.Debug("[PHYSICS]: TaintAngularLock");
                changeAngularLock(timestep);
            }
            if (m_taintremove)
            {
                DisposeOfBody();
                Dispose();
            }

        }

        #region Physics Scene Change Action routines

        private void changeadd(float timestep)
        {
            //SetCollisionShape(null);
            // Construction of new prim
            if (Body != null)
            {
                if (Body.Handle != IntPtr.Zero)
                {
                    DisableAxisMotor();
                    _parent_scene.removeFromWorld(this, Body);
                    //Body.Dispose();
                }
                //Body = null;
                // TODO: dispose parts that make up body
            }
            if (_parent_scene.needsMeshing(_pbs))
            {
                // Don't need to re-enable body..   it's done in SetMesh
                float meshlod = _parent_scene.meshSculptLOD;

                if (IsPhysical)
                    meshlod = _parent_scene.MeshSculptphysicalLOD;

                IMesh mesh = _parent_scene.mesher.CreateMesh(SOPName, _pbs, _size, meshlod, IsPhysical);
                // createmesh returns null when it doesn't mesh.
                CreateGeom(IntPtr.Zero, mesh);
            }
            else
            {
                _mesh = null;
                CreateGeom(IntPtr.Zero, null);
            }

            if (IsPhysical)
                SetBody(Mass);
            else
                SetBody(0);
            //changeSelectedStatus(timestep);
            m_taintadd = false;

        }

        private void changemove(float timestep)
        {

            m_log.Debug("[PHYSICS]: _________ChangeMove");
            if (!m_isphysical)
            {
                tempTransform2 = Body.getWorldTransform();
                btQuaternion quat = tempTransform2.getRotation();
                tempPosition2.setValue(_position.X, _position.Y, _position.Z);
                tempTransform2.Dispose();
                tempTransform2 = new btTransform(quat, tempPosition2);
                Body.setWorldTransform(tempTransform2);

                changeSelectedStatus(timestep);

                resetCollisionAccounting();
            }
            else
            {
                if (Body != null)
                {
                    if (Body.Handle != IntPtr.Zero)
                    {
                        DisableAxisMotor();
                        _parent_scene.removeFromWorld(this, Body);
                        //Body.Dispose();
                    }
                    //Body = null;
                    // TODO: dispose parts that make up body
                }
                /*
                if (_parent_scene.needsMeshing(_pbs))
                {
                    // Don't need to re-enable body..   it's done in SetMesh
                    float meshlod = _parent_scene.meshSculptLOD;

                    if (IsPhysical)
                        meshlod = _parent_scene.MeshSculptphysicalLOD;

                    IMesh mesh = _parent_scene.mesher.CreateMesh(SOPName, _pbs, _size, meshlod, IsPhysical);
                    // createmesh returns null when it doesn't mesh.
                    CreateGeom(IntPtr.Zero, mesh);
                }
                else
                {
                    _mesh = null;
                    CreateGeom(IntPtr.Zero, null);
                }
                SetCollisionShape(prim_geom);
                */
                if (m_isphysical)
                    SetBody(Mass);
                else
                    SetBody(0);
                changeSelectedStatus(timestep);

                resetCollisionAccounting();
            }
            m_taintposition = _position;
        }

        private void rotate(float timestep)
        {
            m_log.Debug("[PHYSICS]: _________ChangeRotate");
            tempTransform2 = Body.getWorldTransform();
            tempOrientation2 = new btQuaternion(_orientation.X, _orientation.Y, _orientation.Z, _orientation.W);
            tempTransform2.setRotation(tempOrientation2);
            Body.setWorldTransform(tempTransform2);

            resetCollisionAccounting();
            m_taintrot = _orientation;
        }

        private void changePhysicsStatus(float timestep)
        {
            if (Body != null)
            {
                if (Body.Handle != IntPtr.Zero)
                {
                    DisableAxisMotor();
                    _parent_scene.removeFromWorld(this, Body);
                    //Body.Dispose();
                }
                //Body = null;
                // TODO: dispose parts that make up body
            }
            m_log.Debug("[PHYSICS]: _________ChangePhysics");

            ProcessGeomCreation();

            if (m_isphysical)
                SetBody(Mass);
            else
                SetBody(0);
            changeSelectedStatus(timestep);

            resetCollisionAccounting();
            m_taintPhysics = m_isphysical;
        }



        internal void ProcessGeomCreation()
        {
            if (_parent_scene.needsMeshing(_pbs))
            {
                ProcessGeomCreationAsTriMesh(Vector3.Zero, Quaternion.Identity);
                // createmesh returns null when it doesn't mesh.
                CreateGeom(IntPtr.Zero, _mesh);
            }
            else
            {
                _mesh = null;
                CreateGeom(IntPtr.Zero, null);
            }
            SetCollisionShape(prim_geom);
        }

        internal bool NeedsMeshing()
        {
            return _parent_scene.needsMeshing(_pbs);
        }

        internal void ProcessGeomCreationAsTriMesh(Vector3 positionOffset, Quaternion orientation)
        {
            // Don't need to re-enable body..   it's done in SetMesh
            float meshlod = _parent_scene.meshSculptLOD;

            if (IsPhysical)
                meshlod = _parent_scene.MeshSculptphysicalLOD;

            IMesh mesh = _parent_scene.mesher.CreateMesh(SOPName, _pbs, _size, meshlod, IsPhysical);
            if (!positionOffset.ApproxEquals(Vector3.Zero, 0.001f) || orientation != Quaternion.Identity)
            {

                float[] xyz = new float[3];
                xyz[0] = positionOffset.X;
                xyz[1] = positionOffset.Y;
                xyz[2] = positionOffset.Z;

                Matrix4 m4 = Matrix4.CreateFromQuaternion(orientation);

                float[,] matrix = new float[3, 3];

                matrix[0, 0] = m4.M11;
                matrix[0, 1] = m4.M12;
                matrix[0, 2] = m4.M13;
                matrix[1, 0] = m4.M21;
                matrix[1, 1] = m4.M22;
                matrix[1, 2] = m4.M23;
                matrix[2, 0] = m4.M31;
                matrix[2, 1] = m4.M32;
                matrix[2, 2] = m4.M33;


                mesh.TransformLinear(matrix, xyz);



            }

            _mesh = mesh;
        }

        private void changesize(float timestep)
        {
            if (Body != null)
            {
                if (Body.Handle != IntPtr.Zero)
                {
                    DisableAxisMotor();
                    _parent_scene.removeFromWorld(this, Body);
                    //Body.Dispose();
                }
                //Body = null;
                // TODO: dispose parts that make up body
            }

            m_log.Debug("[PHYSICS]: _________ChangeSize");
            SetCollisionShape(null);
            // Construction of new prim
            ProcessGeomCreation();

            if (IsPhysical)
                SetBody(Mass);
            else
                SetBody(0);

            m_taintsize = _size;

        }

        private void changeshape(float timestep)
        {
            if (Body != null)
            {
                if (Body.Handle != IntPtr.Zero)
                {
                    DisableAxisMotor();
                    _parent_scene.removeFromWorld(this, Body);
                    //Body.Dispose();
                }
                //Body = null;
                // TODO: dispose parts that make up body
            }
            // Cleanup of old prim geometry and Bodies
            if (IsPhysical && Body != null && Body.Handle != IntPtr.Zero)
            {
                if (childPrim)
                {
                    if (_parent != null)
                    {
                        BulletDotNETPrim parent = (BulletDotNETPrim)_parent;
                        parent.ChildDelink(this);
                    }
                }
                else
                {
                    //disableBody();
                }
            }
            try
            {
                //SetCollisionShape(null);
            }
            catch (System.AccessViolationException)
            {
                //prim_geom = IntPtr.Zero;
                m_log.Error("[PHYSICS]: PrimGeom dead");
            }

            // we don't need to do space calculation because the client sends a position update also.
            if (_size.X <= 0) _size.X = 0.01f;
            if (_size.Y <= 0) _size.Y = 0.01f;
            if (_size.Z <= 0) _size.Z = 0.01f;
            // Construction of new prim

            ProcessGeomCreation();

            tempPosition1.setValue(_position.X, _position.Y, _position.Z);
            if (tempOrientation1.Handle != IntPtr.Zero)
                tempOrientation1.Dispose();
            tempOrientation1 = new btQuaternion(_orientation.X, Orientation.Y, _orientation.Z, _orientation.W);
            if (tempTransform1 != null && tempTransform1.Handle != IntPtr.Zero)
                tempTransform1.Dispose();
            tempTransform1 = new btTransform(tempOrientation1, tempPosition1);




            //d.GeomBoxSetLengths(prim_geom, _size.X, _size.Y, _size.Z);
            if (IsPhysical)
            {
                SetBody(Mass);
                // Re creates body on size.
                // EnableBody also does setMass()

            }
            else
            {
                SetBody(0);
            }

            changeSelectedStatus(timestep);
            if (childPrim)
            {
                if (_parent is BulletDotNETPrim)
                {
                    BulletDotNETPrim parent = (BulletDotNETPrim)_parent;
                    parent.ChildSetGeom(this);
                }
            }
            resetCollisionAccounting();

            m_taintshape = false;
        }

        private void resetCollisionAccounting()
        {
            m_collisionscore = 0;
        }

        private void ChildSetGeom(BulletDotNETPrim bulletDotNETPrim)
        {
            // TODO: throw new NotImplementedException();
        }

        private void changeAddForce(float timestep)
        {
            if (!m_isSelected)
            {
                lock (m_forcelist)
                {
                    //m_log.Info("[PHYSICS]: dequeing forcelist");
                    if (IsPhysical)
                    {
                        Vector3 iforce = Vector3.Zero;
                        for (int i = 0; i < m_forcelist.Count; i++)
                        {
                            iforce = iforce + m_forcelist[i];
                        }

                        if (Body != null && Body.Handle != IntPtr.Zero)
                        {
                            if (tempAddForce != null && tempAddForce.Handle != IntPtr.Zero)
                                tempAddForce.Dispose();
                            enableBodySoft();
                            tempAddForce = new btVector3(iforce.X, iforce.Y, iforce.Z);
                            Body.applyCentralImpulse(tempAddForce);
                        }
                    }
                    m_forcelist.Clear();
                }

                m_collisionscore = 0;
                m_interpenetrationcount = 0;
            }

            m_taintforce = false;

        }

        private void changeAddAngularForce(float timestep)
        {
            if (!m_isSelected)
            {
                lock (m_angularforcelist)
                {
                    //m_log.Info("[PHYSICS]: dequeing forcelist");
                    if (IsPhysical)
                    {
                        Vector3 iforce = Vector3.Zero;
                        for (int i = 0; i < m_angularforcelist.Count; i++)
                        {
                            iforce = iforce + m_angularforcelist[i];
                        }

                        if (Body != null && Body.Handle != IntPtr.Zero)
                        {
                            if (tempAddForce != null && tempAddForce.Handle != IntPtr.Zero)
                                tempAddForce.Dispose();
                            enableBodySoft();
                            tempAddForce = new btVector3(iforce.X, iforce.Y, iforce.Z);
                            Body.applyTorqueImpulse(tempAddForce);
                        }

                    }
                    m_angularforcelist.Clear();
                }

                m_collisionscore = 0;
                m_interpenetrationcount = 0;
            }

            m_taintaddangularforce = false;
        }

        private void changeSetTorque(float timestep)
        {
            if (!m_isSelected)
            {
                if (IsPhysical)
                {
                    if (Body != null && Body.Handle != IntPtr.Zero)
                    {
                        tempAngularVelocity2.setValue(m_taintTorque.X, m_taintTorque.Y, m_taintTorque.Z);
                        Body.applyTorque(tempAngularVelocity2);
                    }
                }
            }
            m_taintTorque = Vector3.Zero;
        }

        private void changedisable(float timestep)
        {
            // TODO: throw new NotImplementedException();
        }

        private void changeSelectedStatus(float timestep)
        {
            // TODO: throw new NotImplementedException();
            if (m_taintselected)
            {
                Body.setCollisionFlags((int)ContactFlags.CF_NO_CONTACT_RESPONSE);
                disableBodySoft();

            }
            else
            {
                Body.setCollisionFlags(0 | (int)ContactFlags.CF_CUSTOM_MATERIAL_CALLBACK);
                enableBodySoft();
            }
            m_isSelected = m_taintselected;

        }

        private void changevelocity(float timestep)
        {
            if (!m_isSelected)
            {
                if (IsPhysical)
                {
                    if (Body != null && Body.Handle != IntPtr.Zero)
                    {
                        tempLinearVelocity2.setValue(m_taintVelocity.X, m_taintVelocity.Y, m_taintVelocity.Z);
                        Body.setLinearVelocity(tempLinearVelocity2);
                    }
                }

                //resetCollisionAccounting();
            }
            m_taintVelocity = Vector3.Zero;
        }

        private void changelink(float timestep)
        {
            if (IsPhysical)
            {
                // Construction of new prim
                if (Body != null)
                {
                    if (Body.Handle != IntPtr.Zero)
                    {
                        DisableAxisMotor();
                        _parent_scene.removeFromWorld(this, Body);
                        //Body.Dispose();
                    }
                    //Body = null;
                    // TODO: dispose parts that make up body
                }

                if (_parent == null && m_taintparent != null)
                {

                    if (m_taintparent is BulletDotNETPrim)
                    {
                        BulletDotNETPrim obj = (BulletDotNETPrim)m_taintparent;
                        obj.ParentPrim(this);
                        childPrim = true;

                    }
                }
                else if (_parent != null && m_taintparent == null)
                {
                    if (_parent is BulletDotNETPrim)
                    {
                        BulletDotNETPrim obj = (BulletDotNETPrim)_parent;
                        obj.ChildDelink(obj);

                        childPrim = false;
                    }
                }

                if (m_taintparent != null)
                {
                    Vector3 taintparentPosition = m_taintparent.Position;
                    taintparentPosition.Z = m_taintparent.Position.Z + 0.02f;
                    m_taintparent.Position = taintparentPosition;
                    _parent_scene.AddPhysicsActorTaint(m_taintparent);
                }
            }
            _parent = m_taintparent;

            m_taintPhysics = m_isphysical;

        }

        private void changefloatonwater(float timestep)
        {
            // TODO: throw new NotImplementedException();
        }

        private void changeAngularLock(float timestep)
        {
            if (IsPhysical && Body != null && Body.Handle != IntPtr.Zero)
            {
                if (_parent == null)
                {
                    if (!m_taintAngularLock.ApproxEquals(Vector3.One, 0f))
                    {
                        //d.BodySetFiniteRotationMode(Body, 0);
                        //d.BodySetFiniteRotationAxis(Body,m_taintAngularLock.X,m_taintAngularLock.Y,m_taintAngularLock.Z);
                        EnableAxisMotor(m_taintAngularLock);
                    }
                    else
                    {
                        DisableAxisMotor();
                    }
                }

            }
            m_angularlock = m_taintAngularLock;

        }
        #endregion




        internal void Move(float timestep)
        {
            //TODO:
            float fx = 0;
            float fy = 0;
            float fz = 0;

            if (IsPhysical && Body != null && Body.Handle != IntPtr.Zero && !m_isSelected)
            {
                float m_mass = CalculateMass();

                fz = 0f;
                //m_log.Info(m_collisionFlags.ToString());

                if (m_buoyancy != 0)
                {
                    if (m_buoyancy > 0)
                    {
                        fz = (((-1 * _parent_scene.gravityz) * m_buoyancy) * m_mass) * 0.035f;

                        //d.Vector3 l_velocity = d.BodyGetLinearVel(Body);
                        //m_log.Info("Using Buoyancy: " + buoyancy + " G: " + (_parent_scene.gravityz * m_buoyancy) + "mass:" + m_mass + "  Pos: " + Position.ToString());
                    }
                    else
                    {
                        fz = (-1 * (((-1 * _parent_scene.gravityz) * (-1 * m_buoyancy)) * m_mass) * 0.035f);
                    }
                }

                if (m_usePID)
                {
                    PID_D = 61f;
                    PID_G = 65f;
                    //if (!d.BodyIsEnabled(Body))
                    //d.BodySetForce(Body, 0f, 0f, 0f);
                    // If we're using the PID controller, then we have no gravity
                    fz = ((-1 * _parent_scene.gravityz) * m_mass) * 1.025f;

                    //  no lock; for now it's only called from within Simulate()

                    // If the PID Controller isn't active then we set our force
                    // calculating base velocity to the current position

                    if ((m_PIDTau < 1) && (m_PIDTau != 0))
                    {
                        //PID_G = PID_G / m_PIDTau;
                        m_PIDTau = 1;
                    }

                    if ((PID_G - m_PIDTau) <= 0)
                    {
                        PID_G = m_PIDTau + 1;
                    }

                    // TODO: NEED btVector3 for Linear Velocity
                    // NEED btVector3 for Position

                    Vector3 pos = _position; //TODO: Insert values gotten from bullet
                    Vector3 vel = _velocity;

                    _target_velocity =
                        new Vector3(
                            (m_PIDTarget.X - pos.X) * ((PID_G - m_PIDTau) * timestep),
                            (m_PIDTarget.Y - pos.Y) * ((PID_G - m_PIDTau) * timestep),
                            (m_PIDTarget.Z - pos.Z) * ((PID_G - m_PIDTau) * timestep)
                            );

                    if (_target_velocity.ApproxEquals(Vector3.Zero, 0.1f))
                    {

                        /* TODO: Do Bullet equiv
                         * 
                        d.BodySetPosition(Body, m_PIDTarget.X, m_PIDTarget.Y, m_PIDTarget.Z);
                        d.BodySetLinearVel(Body, 0, 0, 0);
                        d.BodyAddForce(Body, 0, 0, fz);
                        return;
                        */
                    }
                    else
                    {
                        _zeroFlag = false;

                        fx = ((_target_velocity.X) - vel.X) * (PID_D);
                        fy = ((_target_velocity.Y) - vel.Y) * (PID_D);
                        fz = fz + ((_target_velocity.Z - vel.Z) * (PID_D) * m_mass);

                    }

                }

                if (m_useHoverPID && !m_usePID)
                {
                    // If we're using the PID controller, then we have no gravity
                    fz = (-1 * _parent_scene.gravityz) * m_mass;

                    //  no lock; for now it's only called from within Simulate()

                    // If the PID Controller isn't active then we set our force
                    // calculating base velocity to the current position

                    if ((m_PIDTau < 1))
                    {
                        PID_G = PID_G / m_PIDTau;
                    }

                    if ((PID_G - m_PIDTau) <= 0)
                    {
                        PID_G = m_PIDTau + 1;
                    }
                    Vector3 pos = Vector3.Zero; //TODO: Insert values gotten from bullet
                    Vector3 vel = Vector3.Zero;

                    // determine what our target height really is based on HoverType
                    switch (m_PIDHoverType)
                    {
                        case PIDHoverType.Absolute:
                            m_targetHoverHeight = m_PIDHoverHeight;
                            break;
                        case PIDHoverType.Ground:
                            m_groundHeight = _parent_scene.GetTerrainHeightAtXY(pos.X, pos.Y);
                            m_targetHoverHeight = m_groundHeight + m_PIDHoverHeight;
                            break;
                        case PIDHoverType.GroundAndWater:
                            m_groundHeight = _parent_scene.GetTerrainHeightAtXY(pos.X, pos.Y);
                            m_waterHeight = _parent_scene.GetWaterLevel();
                            if (m_groundHeight > m_waterHeight)
                            {
                                m_targetHoverHeight = m_groundHeight + m_PIDHoverHeight;
                            }
                            else
                            {
                                m_targetHoverHeight = m_waterHeight + m_PIDHoverHeight;
                            }
                            break;
                        case PIDHoverType.Water:
                            m_waterHeight = _parent_scene.GetWaterLevel();
                            m_targetHoverHeight = m_waterHeight + m_PIDHoverHeight;
                            break;
                    }


                    _target_velocity =
                        new Vector3(0.0f, 0.0f,
                            (m_targetHoverHeight - pos.Z) * ((PID_G - m_PIDHoverTau) * timestep)
                            );

                    //  if velocity is zero, use position control; otherwise, velocity control

                    if (_target_velocity.ApproxEquals(Vector3.Zero, 0.1f))
                    {

                        /* TODO: Do Bullet Equiv
                        d.BodySetPosition(Body, pos.X, pos.Y, m_targetHoverHeight);
                        d.BodySetLinearVel(Body, vel.X, vel.Y, 0);
                        d.BodyAddForce(Body, 0, 0, fz);
                        */
                        if (Body != null && Body.Handle != IntPtr.Zero)
                        {
                            Body.setLinearVelocity(_parent_scene.VectorZero);
                            Body.clearForces();
                        }
                        return;
                    }
                    else
                    {
                        _zeroFlag = false;

                        // We're flying and colliding with something
                        fz = fz + ((_target_velocity.Z - vel.Z) * (PID_D) * m_mass);
                    }
                }

                fx *= m_mass;
                fy *= m_mass;
                //fz *= m_mass;

                fx += m_force.X;
                fy += m_force.Y;
                fz += m_force.Z;

                //m_log.Info("[OBJPID]: X:" + fx.ToString() + " Y:" + fy.ToString() + " Z:" + fz.ToString());
                if (fx != 0 || fy != 0 || fz != 0)
                {
                    /*
                     * TODO: Do Bullet Equiv
                    if (!d.BodyIsEnabled(Body))
                    {
                        d.BodySetLinearVel(Body, 0f, 0f, 0f);
                        d.BodySetForce(Body, 0, 0, 0);
                        enableBodySoft();
                    }
                    */
                    // 35x10 = 350n times the mass per second applied maximum.

                    float nmax = 35f * m_mass;
                    float nmin = -35f * m_mass;


                    if (fx > nmax)
                        fx = nmax;
                    if (fx < nmin)
                        fx = nmin;
                    if (fy > nmax)
                        fy = nmax;
                    if (fy < nmin)
                        fy = nmin;

                    // TODO: Do Bullet Equiv
                    // d.BodyAddForce(Body, fx, fy, fz);
                    if (Body != null && Body.Handle != IntPtr.Zero)
                    {
                        Body.activate(true);
                        if (tempAddForce != null && tempAddForce.Handle != IntPtr.Zero)
                            tempAddForce.Dispose();

                        tempAddForce = new btVector3(fx * 0.01f, fy * 0.01f, fz * 0.01f);
                        Body.applyCentralImpulse(tempAddForce);
                    }
                }
            }
            else
            {
                if (m_zeroPosition == null)
                    m_zeroPosition = Vector3.Zero;
                m_zeroPosition = _position;
                return;
            }
        }




        #region Mass Calculation

        private float CalculateMass()
        {
            float volume = 0;

            // No material is passed to the physics engines yet..  soo..
            // we're using the m_density constant in the class definition

            float returnMass = 0;

            switch (_pbs.ProfileShape)
            {
                case ProfileShape.Square:
                    // Profile Volume

                    volume = _size.X * _size.Y * _size.Z;

                    // If the user has 'hollowed out'
                    // ProfileHollow is one of those 0 to 50000 values :P
                    // we like percentages better..   so turning into a percentage

                    if (((float)_pbs.ProfileHollow / 50000f) > 0.0)
                    {
                        float hollowAmount = (float)_pbs.ProfileHollow / 50000f;

                        // calculate the hollow volume by it's shape compared to the prim shape
                        float hollowVolume = 0;
                        switch (_pbs.HollowShape)
                        {
                            case HollowShape.Square:
                            case HollowShape.Same:
                                // Cube Hollow volume calculation
                                float hollowsizex = _size.X * hollowAmount;
                                float hollowsizey = _size.Y * hollowAmount;
                                float hollowsizez = _size.Z * hollowAmount;
                                hollowVolume = hollowsizex * hollowsizey * hollowsizez;
                                break;

                            case HollowShape.Circle:
                                // Hollow shape is a perfect cyllinder in respect to the cube's scale
                                // Cyllinder hollow volume calculation
                                float hRadius = _size.X / 2;
                                float hLength = _size.Z;

                                // pi * r2 * h
                                hollowVolume = ((float)(Math.PI * Math.Pow(hRadius, 2) * hLength) * hollowAmount);
                                break;

                            case HollowShape.Triangle:
                                // Equilateral Triangular Prism volume hollow calculation
                                // Triangle is an Equilateral Triangular Prism with aLength = to _size.Y

                                float aLength = _size.Y;
                                // 1/2 abh
                                hollowVolume = (float)((0.5 * aLength * _size.X * _size.Z) * hollowAmount);
                                break;

                            default:
                                hollowVolume = 0;
                                break;
                        }
                        volume = volume - hollowVolume;
                    }

                    break;
                case ProfileShape.Circle:
                    if (_pbs.PathCurve == (byte)Extrusion.Straight)
                    {
                        // Cylinder
                        float volume1 = (float)(Math.PI * Math.Pow(_size.X / 2, 2) * _size.Z);
                        float volume2 = (float)(Math.PI * Math.Pow(_size.Y / 2, 2) * _size.Z);

                        // Approximating the cylinder's irregularity.
                        if (volume1 > volume2)
                        {
                            volume = (float)volume1 - (volume1 - volume2);
                        }
                        else if (volume2 > volume1)
                        {
                            volume = (float)volume2 - (volume2 - volume1);
                        }
                        else
                        {
                            // Regular cylinder
                            volume = volume1;
                        }
                    }
                    else
                    {
                        // We don't know what the shape is yet, so use default
                        volume = _size.X * _size.Y * _size.Z;
                    }
                    // If the user has 'hollowed out'
                    // ProfileHollow is one of those 0 to 50000 values :P
                    // we like percentages better..   so turning into a percentage

                    if (((float)_pbs.ProfileHollow / 50000f) > 0.0)
                    {
                        float hollowAmount = (float)_pbs.ProfileHollow / 50000f;

                        // calculate the hollow volume by it's shape compared to the prim shape
                        float hollowVolume = 0;
                        switch (_pbs.HollowShape)
                        {
                            case HollowShape.Same:
                            case HollowShape.Circle:
                                // Hollow shape is a perfect cyllinder in respect to the cube's scale
                                // Cyllinder hollow volume calculation
                                float hRadius = _size.X / 2;
                                float hLength = _size.Z;

                                // pi * r2 * h
                                hollowVolume = ((float)(Math.PI * Math.Pow(hRadius, 2) * hLength) * hollowAmount);
                                break;

                            case HollowShape.Square:
                                // Cube Hollow volume calculation
                                float hollowsizex = _size.X * hollowAmount;
                                float hollowsizey = _size.Y * hollowAmount;
                                float hollowsizez = _size.Z * hollowAmount;
                                hollowVolume = hollowsizex * hollowsizey * hollowsizez;
                                break;

                            case HollowShape.Triangle:
                                // Equilateral Triangular Prism volume hollow calculation
                                // Triangle is an Equilateral Triangular Prism with aLength = to _size.Y

                                float aLength = _size.Y;
                                // 1/2 abh
                                hollowVolume = (float)((0.5 * aLength * _size.X * _size.Z) * hollowAmount);
                                break;

                            default:
                                hollowVolume = 0;
                                break;
                        }
                        volume = volume - hollowVolume;
                    }
                    break;

                case ProfileShape.HalfCircle:
                    if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                        if (_size.X == _size.Y && _size.Z == _size.X)
                        {
                            // regular sphere
                            // v = 4/3 * pi * r^3
                            float sradius3 = (float)Math.Pow((_size.X / 2), 3);
                            volume = (float)((4 / 3f) * Math.PI * sradius3);
                        }
                        else
                        {
                            // we treat this as a box currently
                            volume = _size.X * _size.Y * _size.Z;
                        }
                    }
                    else
                    {
                        // We don't know what the shape is yet, so use default
                        volume = _size.X * _size.Y * _size.Z;
                    }
                    break;

                case ProfileShape.EquilateralTriangle:
                    /*
                        v = (abs((xB*yA-xA*yB)+(xC*yB-xB*yC)+(xA*yC-xC*yA))/2) * h

                        // seed mesh
                        Vertex MM = new Vertex(-0.25f, -0.45f, 0.0f);
                        Vertex PM = new Vertex(+0.5f, 0f, 0.0f);
                        Vertex PP = new Vertex(-0.25f, +0.45f, 0.0f);
                     */
                    float xA = -0.25f * _size.X;
                    float yA = -0.45f * _size.Y;

                    float xB = 0.5f * _size.X;
                    float yB = 0;

                    float xC = -0.25f * _size.X;
                    float yC = 0.45f * _size.Y;

                    volume = (float)((Math.Abs((xB * yA - xA * yB) + (xC * yB - xB * yC) + (xA * yC - xC * yA)) / 2) * _size.Z);

                    // If the user has 'hollowed out'
                    // ProfileHollow is one of those 0 to 50000 values :P
                    // we like percentages better..   so turning into a percentage
                    float fhollowFactor = ((float)_pbs.ProfileHollow / 1.9f);
                    if (((float)fhollowFactor / 50000f) > 0.0)
                    {
                        float hollowAmount = (float)fhollowFactor / 50000f;

                        // calculate the hollow volume by it's shape compared to the prim shape
                        float hollowVolume = 0;
                        switch (_pbs.HollowShape)
                        {
                            case HollowShape.Same:
                            case HollowShape.Triangle:
                                // Equilateral Triangular Prism volume hollow calculation
                                // Triangle is an Equilateral Triangular Prism with aLength = to _size.Y

                                float aLength = _size.Y;
                                // 1/2 abh
                                hollowVolume = (float)((0.5 * aLength * _size.X * _size.Z) * hollowAmount);
                                break;

                            case HollowShape.Square:
                                // Cube Hollow volume calculation
                                float hollowsizex = _size.X * hollowAmount;
                                float hollowsizey = _size.Y * hollowAmount;
                                float hollowsizez = _size.Z * hollowAmount;
                                hollowVolume = hollowsizex * hollowsizey * hollowsizez;
                                break;

                            case HollowShape.Circle:
                                // Hollow shape is a perfect cyllinder in respect to the cube's scale
                                // Cyllinder hollow volume calculation
                                float hRadius = _size.X / 2;
                                float hLength = _size.Z;

                                // pi * r2 * h
                                hollowVolume = ((float)((Math.PI * Math.Pow(hRadius, 2) * hLength) / 2) * hollowAmount);
                                break;

                            default:
                                hollowVolume = 0;
                                break;
                        }
                        volume = volume - hollowVolume;
                    }
                    break;

                default:
                    // we don't have all of the volume formulas yet so
                    // use the common volume formula for all
                    volume = _size.X * _size.Y * _size.Z;
                    break;
            }

            // Calculate Path cut effect on volume
            // Not exact, in the triangle hollow example
            // They should never be zero or less then zero..
            // we'll ignore it if it's less then zero

            // ProfileEnd and ProfileBegin are values
            // from 0 to 50000

            // Turning them back into percentages so that I can cut that percentage off the volume

            float PathCutEndAmount = _pbs.ProfileEnd;
            float PathCutStartAmount = _pbs.ProfileBegin;
            if (((PathCutStartAmount + PathCutEndAmount) / 50000f) > 0.0f)
            {
                float pathCutAmount = ((PathCutStartAmount + PathCutEndAmount) / 50000f);

                // Check the return amount for sanity
                if (pathCutAmount >= 0.99f)
                    pathCutAmount = 0.99f;

                volume = volume - (volume * pathCutAmount);
            }
            UInt16 taperX = _pbs.PathScaleX;
            UInt16 taperY = _pbs.PathScaleY;
            float taperFactorX = 0;
            float taperFactorY = 0;

            // Mass = density * volume
            if (taperX != 100)
            {
                if (taperX > 100)
                {
                    taperFactorX = 1.0f - ((float)taperX / 200);
                    //m_log.Warn("taperTopFactorX: " + extr.taperTopFactorX.ToString());
                }
                else
                {
                    taperFactorX = 1.0f - ((100 - (float)taperX) / 100);
                    //m_log.Warn("taperBotFactorX: " + extr.taperBotFactorX.ToString());
                }
                volume = (float)volume * ((taperFactorX / 3f) + 0.001f);
            }

            if (taperY != 100)
            {
                if (taperY > 100)
                {
                    taperFactorY = 1.0f - ((float)taperY / 200);
                    //m_log.Warn("taperTopFactorY: " + extr.taperTopFactorY.ToString());
                }
                else
                {
                    taperFactorY = 1.0f - ((100 - (float)taperY) / 100);
                    //m_log.Warn("taperBotFactorY: " + extr.taperBotFactorY.ToString());
                }
                volume = (float)volume * ((taperFactorY / 3f) + 0.001f);
            }
            returnMass = m_density * volume;
            if (returnMass <= 0) returnMass = 0.0001f;//ckrinke: Mass must be greater then zero.



            // Recursively calculate mass
            bool HasChildPrim = false;
            lock (childrenPrim)
            {
                if (childrenPrim.Count > 0)
                {
                    HasChildPrim = true;
                }

            }
            if (HasChildPrim)
            {
                BulletDotNETPrim[] childPrimArr = new BulletDotNETPrim[0];

                lock (childrenPrim)
                    childPrimArr = childrenPrim.ToArray();

                for (int i = 0; i < childPrimArr.Length; i++)
                {
                    if (childPrimArr[i] != null && !childPrimArr[i].m_taintremove)
                        returnMass += childPrimArr[i].CalculateMass();
                    // failsafe, this shouldn't happen but with OpenSim, you never know :)
                    if (i > 256)
                        break;
                }
            }





            return returnMass;
        }

        #endregion


        public void CreateGeom(IntPtr m_targetSpace, IMesh p_mesh)
        {
            m_log.Debug("[PHYSICS]: _________CreateGeom");
            if (p_mesh != null)
            {
                //_mesh = _parent_scene.mesher.CreateMesh(m_primName, _pbs, _size, _parent_scene.meshSculptLOD, IsPhysical);
                _mesh = p_mesh;
                setMesh(_parent_scene, _mesh);

            }
            else
            {
                if (_pbs.ProfileShape == ProfileShape.HalfCircle && _pbs.PathCurve == (byte)Extrusion.Curve1)
                {
                    if (_size.X == _size.Y && _size.Y == _size.Z && _size.X == _size.Z)
                    {
                        if (((_size.X / 2f) > 0f))
                        {
                            //SetGeom to a Regular Sphere
                            if (tempSize1 == null)
                                tempSize1 = new btVector3(0, 0, 0);
                            tempSize1.setValue(_size.X * 0.5f, _size.Y * 0.5f, _size.Z * 0.5f);
                            SetCollisionShape(new btSphereShape(_size.X * 0.5f));
                        }
                        else
                        {
                            // uses halfextents
                            if (tempSize1 == null)
                                tempSize1 = new btVector3(0, 0, 0);
                            tempSize1.setValue(_size.X * 0.5f, _size.Y * 0.5f, _size.Z * 0.5f);
                            SetCollisionShape(new btBoxShape(tempSize1));
                        }
                    }
                    else
                    {
                        // uses halfextents
                        if (tempSize1 == null)
                            tempSize1 = new btVector3(0, 0, 0);
                        tempSize1.setValue(_size.X * 0.5f, _size.Y * 0.5f, _size.Z * 0.5f);
                        SetCollisionShape(new btBoxShape(tempSize1));
                    }

                }
                else
                {
                    if (tempSize1 == null)
                        tempSize1 = new btVector3(0, 0, 0);
                    // uses halfextents
                    tempSize1.setValue(_size.X * 0.5f, _size.Y * 0.5f, _size.Z * 0.5f);
                    SetCollisionShape(new btBoxShape(tempSize1));
                }
            }
        }

        private void setMesh(BulletDotNETScene _parent_scene, IMesh mesh)
        {
            // TODO: Set Collision Body Mesh
            // This sleeper is there to moderate how long it takes between
            // setting up the mesh and pre-processing it when we get rapid fire mesh requests on a single object
            m_log.Debug("_________SetMesh");
            Thread.Sleep(10);

            //Kill Body so that mesh can re-make the geom
            if (IsPhysical && Body != null && Body.Handle != IntPtr.Zero)
            {
                if (childPrim)
                {
                    if (_parent != null)
                    {
                        BulletDotNETPrim parent = (BulletDotNETPrim)_parent;
                        parent.ChildDelink(this);
                    }
                }
                else
                {
                    //disableBody();
                }
            }

            //IMesh oldMesh = primMesh;

            //primMesh = mesh;

            //float[] vertexList = primMesh.getVertexListAsFloatLocked(); // Note, that vertextList is pinned in memory
            //int[] indexList = primMesh.getIndexListAsIntLocked(); // Also pinned, needs release after usage
            ////Array.Reverse(indexList);
            //primMesh.releaseSourceMeshData(); // free up the original mesh data to save memory

            IMesh oldMesh = primMesh;

            primMesh = mesh;

            float[] vertexList = mesh.getVertexListAsFloatLocked(); // Note, that vertextList is pinned in memory
            int[] indexList = mesh.getIndexListAsIntLocked(); // Also pinned, needs release after usage
            //Array.Reverse(indexList);
            mesh.releaseSourceMeshData(); // free up the original mesh data to save memory


            int VertexCount = vertexList.GetLength(0) / 3;
            int IndexCount = indexList.GetLength(0);

            if (btshapeArray != null && btshapeArray.Handle != IntPtr.Zero)
                btshapeArray.Dispose();
            //Array.Reverse(indexList);
            btshapeArray = new btTriangleIndexVertexArray(IndexCount / 3, indexList, (3 * sizeof(int)),
                                                                                     VertexCount, vertexList, 3 * sizeof(float));
            SetCollisionShape(new btGImpactMeshShape(btshapeArray));
            //((btGImpactMeshShape) prim_geom).updateBound();
            ((btGImpactMeshShape)prim_geom).setLocalScaling(new btVector3(1, 1, 1));
            ((btGImpactMeshShape)prim_geom).updateBound();
            _parent_scene.SetUsingGImpact();
            //if (oldMesh != null)
            //{
            //    oldMesh.releasePinned();
            //    oldMesh = null;
            //}

        }

        private void SetCollisionShape(btCollisionShape shape)
        {
            /*
            if (shape == null)
                m_log.Debug("[PHYSICS]:SetShape!Null");
            else
                m_log.Debug("[PHYSICS]:SetShape!");
            
            if (Body != null)
            {
                DisposeOfBody();
            }

            if (prim_geom != null)
            {
                prim_geom.Dispose();
                prim_geom = null;
            }
             */
            prim_geom = shape;

            //Body.set
        }

        public void SetBody(float mass)
        {

            if (!IsPhysical || childrenPrim.Count == 0)
            {
                if (tempMotionState1 != null && tempMotionState1.Handle != IntPtr.Zero)
                    tempMotionState1.Dispose();
                if (tempTransform2 != null && tempTransform2.Handle != IntPtr.Zero)
                    tempTransform2.Dispose();
                if (tempOrientation2 != null && tempOrientation2.Handle != IntPtr.Zero)
                    tempOrientation2.Dispose();

                if (tempPosition2 != null && tempPosition2.Handle != IntPtr.Zero)
                    tempPosition2.Dispose();

                tempOrientation2 = new btQuaternion(_orientation.X, _orientation.Y, _orientation.Z, _orientation.W);
                tempPosition2 = new btVector3(_position.X, _position.Y, _position.Z);
                tempTransform2 = new btTransform(tempOrientation2, tempPosition2);
                tempMotionState1 = new btDefaultMotionState(tempTransform2, _parent_scene.TransZero);
                if (tempInertia1 != null && tempInertia1.Handle != IntPtr.Zero)
                    tempInertia1.Dispose();
                tempInertia1 = new btVector3(0, 0, 0);


                prim_geom.calculateLocalInertia(mass, tempInertia1);

                if (mass != 0)
                    _parent_scene.addActivePrim(this);
                else
                    _parent_scene.remActivePrim(this);

                //     Body = new btRigidBody(mass, tempMotionState1, prim_geom);
                //else
                Body = new btRigidBody(mass, tempMotionState1, prim_geom, tempInertia1);

                if (prim_geom is btGImpactMeshShape)
                {
                    ((btGImpactMeshShape)prim_geom).setLocalScaling(new btVector3(1, 1, 1));
                    ((btGImpactMeshShape)prim_geom).updateBound();
                }
                //Body.setCollisionFlags(Body.getCollisionFlags() | (int)ContactFlags.CF_CUSTOM_MATERIAL_CALLBACK);
                //Body.setUserPointer((IntPtr) (int)m_localID);
                _parent_scene.AddPrimToScene(this);
            }
            else
            {
                // bool hasTrimesh = false;
                lock (childrenPrim)
                {
                    foreach (BulletDotNETPrim chld in childrenPrim)
                    {
                        if (chld == null)
                            continue;

                        // if (chld.NeedsMeshing())
                        //     hasTrimesh = true;
                    }
                }

                //if (hasTrimesh)
                //{
                ProcessGeomCreationAsTriMesh(Vector3.Zero, Quaternion.Identity);
                // createmesh returns null when it doesn't mesh.

                /*
                if (_mesh is Mesh)
                {
                }
                else
                {
                    m_log.Warn("[PHYSICS]: Can't link a OpenSim.Region.Physics.Meshing.Mesh object");
                    return;
                }
                */



                foreach (BulletDotNETPrim chld in childrenPrim)
                {
                    if (chld == null)
                        continue;
                    Vector3 offset = chld.Position - Position;
                    Vector3 pos = new Vector3(offset.X, offset.Y, offset.Z);
                    pos *= Quaternion.Inverse(Orientation);
                    //pos *= Orientation;
                    offset = pos;
                    chld.ProcessGeomCreationAsTriMesh(offset, chld.Orientation);

                    _mesh.Append(chld._mesh);


                }
                setMesh(_parent_scene, _mesh);

                //}

                if (tempMotionState1 != null && tempMotionState1.Handle != IntPtr.Zero)
                    tempMotionState1.Dispose();
                if (tempTransform2 != null && tempTransform2.Handle != IntPtr.Zero)
                    tempTransform2.Dispose();
                if (tempOrientation2 != null && tempOrientation2.Handle != IntPtr.Zero)
                    tempOrientation2.Dispose();

                if (tempPosition2 != null && tempPosition2.Handle != IntPtr.Zero)
                    tempPosition2.Dispose();

                tempOrientation2 = new btQuaternion(_orientation.X, _orientation.Y, _orientation.Z, _orientation.W);
                tempPosition2 = new btVector3(_position.X, _position.Y, _position.Z);
                tempTransform2 = new btTransform(tempOrientation2, tempPosition2);
                tempMotionState1 = new btDefaultMotionState(tempTransform2, _parent_scene.TransZero);
                if (tempInertia1 != null && tempInertia1.Handle != IntPtr.Zero)
                    tempInertia1.Dispose();
                tempInertia1 = new btVector3(0, 0, 0);


                prim_geom.calculateLocalInertia(mass, tempInertia1);

                if (mass != 0)
                    _parent_scene.addActivePrim(this);
                else
                    _parent_scene.remActivePrim(this);

                //     Body = new btRigidBody(mass, tempMotionState1, prim_geom);
                //else
                Body = new btRigidBody(mass, tempMotionState1, prim_geom, tempInertia1);

                if (prim_geom is btGImpactMeshShape)
                {
                    ((btGImpactMeshShape)prim_geom).setLocalScaling(new btVector3(1, 1, 1));
                    ((btGImpactMeshShape)prim_geom).updateBound();
                }
                _parent_scene.AddPrimToScene(this);

            }

            if (IsPhysical)
                changeAngularLock(0);
        }

        private void DisposeOfBody()
        {
            if (Body != null)
            {
                if (Body.Handle != IntPtr.Zero)
                {
                    DisableAxisMotor();
                    _parent_scene.removeFromWorld(this, Body);
                    Body.Dispose();
                }
                Body = null;
                // TODO: dispose parts that make up body
            }
        }

        private void ChildDelink(BulletDotNETPrim pPrim)
        {
            // Okay, we have a delinked child..   need to rebuild the body.
            lock (childrenPrim)
            {
                foreach (BulletDotNETPrim prm in childrenPrim)
                {
                    prm.childPrim = true;
                    prm.disableBody();

                }
            }
            disableBody();

            lock (childrenPrim)
            {
                childrenPrim.Remove(pPrim);
            }




            if (Body != null && Body.Handle != IntPtr.Zero)
            {
                _parent_scene.remActivePrim(this);
            }



            lock (childrenPrim)
            {
                foreach (BulletDotNETPrim prm in childrenPrim)
                {
                    ParentPrim(prm);
                }
            }

        }

        internal void ParentPrim(BulletDotNETPrim prm)
        {
            if (prm == null)
                return;



            lock (childrenPrim)
            {
                if (!childrenPrim.Contains(prm))
                {
                    childrenPrim.Add(prm);
                }
            }


        }

        public void disableBody()
        {
            //this kills the body so things like 'mesh' can re-create it.
            /*
            lock (this)
            {
                if (!childPrim)
                {
                    if (Body != null && Body.Handle != IntPtr.Zero)
                    {
                        _parent_scene.remActivePrim(this);

                        m_collisionCategories &= ~CollisionCategories.Body;
                        m_collisionFlags &= ~(CollisionCategories.Wind | CollisionCategories.Land);

                        if (prim_geom != null && prim_geom.Handle != IntPtr.Zero)
                        {
                            // TODO: Set Category bits and Flags
                        }

                        // TODO: destroy body
                        DisposeOfBody();

                        lock (childrenPrim)
                        {
                            if (childrenPrim.Count > 0)
                            {
                                foreach (BulletDotNETPrim prm in childrenPrim)
                                {
                                    _parent_scene.remActivePrim(prm);
                                    prm.DisposeOfBody();
                                    prm.SetCollisionShape(null);
                                }
                            }

                        }

                        DisposeOfBody();
                    }
                }
                else
                {
                    _parent_scene.remActivePrim(this);
                    m_collisionCategories &= ~CollisionCategories.Body;
                    m_collisionFlags &= ~(CollisionCategories.Wind | CollisionCategories.Land);

                    if (prim_geom != null && prim_geom.Handle != IntPtr.Zero)
                    {
                        // TODO: Set Category bits and Flags
                    }

                    DisposeOfBody();
                }
                
            }
            */
            DisableAxisMotor();
            m_disabled = true;
            m_collisionscore = 0;
        }

        public void disableBodySoft()
        {
            m_disabled = true;

            if (m_isphysical && Body.Handle != IntPtr.Zero)
            {
                Body.clearForces();
                Body.forceActivationState(0);

            }

        }

        public void enableBodySoft()
        {
            if (!childPrim)
            {
                if (m_isphysical && Body.Handle != IntPtr.Zero)
                {
                    Body.clearForces();
                    Body.forceActivationState(4);
                    forceenable = true;

                }
                m_disabled = false;
            }
        }

        public void enableBody()
        {
            if (!childPrim)
            {
                //SetCollisionShape(prim_geom);
                if (IsPhysical)
                    SetBody(Mass);
                else
                    SetBody(0);

                // TODO: Set Collision Category Bits and Flags
                // TODO: Set Auto Disable data

                m_interpenetrationcount = 0;
                m_collisionscore = 0;
                m_disabled = false;
                // The body doesn't already have a finite rotation mode set here
                if ((!m_angularlock.ApproxEquals(Vector3.Zero, 0f)) && _parent == null)
                {
                    // TODO: Create Angular Motor on Axis Lock!
                }
                _parent_scene.addActivePrim(this);
            }
        }

        public void UpdatePositionAndVelocity()
        {
            if (!m_isSelected)
            {
                if (_parent == null)
                {
                    Vector3 pv = Vector3.Zero;
                    bool lastZeroFlag = _zeroFlag;
                    if (tempPosition3 != null && tempPosition3.Handle != IntPtr.Zero)
                        tempPosition3.Dispose();
                    if (tempTransform3 != null && tempTransform3.Handle != IntPtr.Zero)
                        tempTransform3.Dispose();

                    if (tempOrientation2 != null && tempOrientation2.Handle != IntPtr.Zero)
                        tempOrientation2.Dispose();

                    if (tempAngularVelocity1 != null && tempAngularVelocity1.Handle != IntPtr.Zero)
                        tempAngularVelocity1.Dispose();

                    if (tempLinearVelocity1 != null && tempLinearVelocity1.Handle != IntPtr.Zero)
                        tempLinearVelocity1.Dispose();



                    tempTransform3 = Body.getInterpolationWorldTransform();
                    tempPosition3 = tempTransform3.getOrigin(); // vec
                    tempOrientation2 = tempTransform3.getRotation(); // ori
                    tempAngularVelocity1 = Body.getInterpolationAngularVelocity(); //rotvel
                    tempLinearVelocity1 = Body.getInterpolationLinearVelocity(); // vel

                    _torque = new Vector3(tempAngularVelocity1.getX(), tempAngularVelocity1.getX(),
                                      tempAngularVelocity1.getZ());
                    Vector3 l_position = Vector3.Zero;
                    Quaternion l_orientation = Quaternion.Identity;
                    m_lastposition = _position;
                    m_lastorientation = _orientation;

                    l_position.X = tempPosition3.getX();
                    l_position.Y = tempPosition3.getY();
                    l_position.Z = tempPosition3.getZ();
                    l_orientation.X = tempOrientation2.getX();
                    l_orientation.Y = tempOrientation2.getY();
                    l_orientation.Z = tempOrientation2.getZ();
                    l_orientation.W = tempOrientation2.getW();

                    if (l_position.X > ((int)Constants.RegionSize - 0.05f) || l_position.X < 0f || l_position.Y > ((int)Constants.RegionSize - 0.05f) || l_position.Y < 0f)
                    {
                        //base.RaiseOutOfBounds(l_position);

                        if (m_crossingfailures < _parent_scene.geomCrossingFailuresBeforeOutofbounds)
                        {
                            _position = l_position;
                            //_parent_scene.remActivePrim(this);
                            if (_parent == null)
                                base.RequestPhysicsterseUpdate();
                            return;
                        }
                        else
                        {
                            if (_parent == null)
                                base.RaiseOutOfBounds(l_position);
                            return;
                        }
                    }

                    if (l_position.Z < -200000f)
                    {
                        // This is so prim that get lost underground don't fall forever and suck up
                        //
                        // Sim resources and memory.
                        // Disables the prim's movement physics....
                        // It's a hack and will generate a console message if it fails.

                        //IsPhysical = false;
                        //if (_parent == null)
                        //base.RaiseOutOfBounds(_position);

                        _acceleration.X = 0;
                        _acceleration.Y = 0;
                        _acceleration.Z = 0;

                        _velocity.X = 0;
                        _velocity.Y = 0;
                        _velocity.Z = 0;
                        m_rotationalVelocity.X = 0;
                        m_rotationalVelocity.Y = 0;
                        m_rotationalVelocity.Z = 0;

                        if (_parent == null)
                            base.RequestPhysicsterseUpdate();

                        m_throttleUpdates = false;
                        // throttleCounter = 0;
                        _zeroFlag = true;
                        //outofBounds = true;
                    }

                    if ((Math.Abs(m_lastposition.X - l_position.X) < 0.02)
                        && (Math.Abs(m_lastposition.Y - l_position.Y) < 0.02)
                        && (Math.Abs(m_lastposition.Z - l_position.Z) < 0.02)
                        && (1.0 - Math.Abs(Quaternion.Dot(m_lastorientation, l_orientation)) < 0.01))
                    {
                        _zeroFlag = true;
                        m_throttleUpdates = false;
                    }
                    else
                    {
                        //m_log.Debug(Math.Abs(m_lastposition.X - l_position.X).ToString());
                        _zeroFlag = false;
                    }

                    if (_zeroFlag)
                    {
                        _velocity.X = 0.0f;
                        _velocity.Y = 0.0f;
                        _velocity.Z = 0.0f;

                        _acceleration.X = 0;
                        _acceleration.Y = 0;
                        _acceleration.Z = 0;

                        //_orientation.w = 0f;
                        //_orientation.X = 0f;
                        //_orientation.Y = 0f;
                        //_orientation.Z = 0f;
                        m_rotationalVelocity.X = 0;
                        m_rotationalVelocity.Y = 0;
                        m_rotationalVelocity.Z = 0;
                        if (!m_lastUpdateSent)
                        {
                            m_throttleUpdates = false;
                            // throttleCounter = 0;
                            m_rotationalVelocity = pv;

                            if (_parent == null)
                                base.RequestPhysicsterseUpdate();

                            m_lastUpdateSent = true;
                        }
                    }
                    else
                    {
                        if (lastZeroFlag != _zeroFlag)
                        {
                            if (_parent == null)
                                base.RequestPhysicsterseUpdate();
                        }

                        m_lastVelocity = _velocity;

                        _position = l_position;

                        _velocity.X = tempLinearVelocity1.getX();
                        _velocity.Y = tempLinearVelocity1.getY();
                        _velocity.Z = tempLinearVelocity1.getZ();

                        _acceleration = ((_velocity - m_lastVelocity) / 0.1f);
                        _acceleration = new Vector3(_velocity.X - m_lastVelocity.X / 0.1f,
                                                          _velocity.Y - m_lastVelocity.Y / 0.1f,
                                                          _velocity.Z - m_lastVelocity.Z / 0.1f);
                        //m_log.Info("[PHYSICS]: V1: " + _velocity + " V2: " + m_lastVelocity + " Acceleration: " + _acceleration.ToString());

                        if (_velocity.ApproxEquals(pv, 0.5f))
                        {
                            m_rotationalVelocity = pv;
                        }
                        else
                        {
                            m_rotationalVelocity = new Vector3(tempAngularVelocity1.getX(), tempAngularVelocity1.getY(), tempAngularVelocity1.getZ());
                        }

                        //m_log.Debug("ODE: " + m_rotationalVelocity.ToString());

                        _orientation.X = l_orientation.X;
                        _orientation.Y = l_orientation.Y;
                        _orientation.Z = l_orientation.Z;
                        _orientation.W = l_orientation.W;
                        m_lastUpdateSent = false;

                        //if (!m_throttleUpdates || throttleCounter > _parent_scene.geomUpdatesPerThrottledUpdate)
                        //{
                        if (_parent == null)
                            base.RequestPhysicsterseUpdate();
                        // }
                        // else
                        // {
                        //     throttleCounter++;
                        //}

                    }
                    m_lastposition = l_position;
                    if (forceenable)
                    {
                        Body.forceActivationState(1);
                        forceenable = false;
                    }
                }
                else
                {
                    // Not a body..   so Make sure the client isn't interpolating
                    _velocity.X = 0;
                    _velocity.Y = 0;
                    _velocity.Z = 0;

                    _acceleration.X = 0;
                    _acceleration.Y = 0;
                    _acceleration.Z = 0;

                    m_rotationalVelocity.X = 0;
                    m_rotationalVelocity.Y = 0;
                    m_rotationalVelocity.Z = 0;
                    _zeroFlag = true;
                }
            }
        }


        internal void setPrimForRemoval()
        {
            m_taintremove = true;
        }

        internal void EnableAxisMotor(Vector3 axislock)
        {
            if (m_aMotor != null)
                DisableAxisMotor();

            if (Body == null)
                return;

            if (Body.Handle == IntPtr.Zero)
                return;

            if (AxisLockAngleHigh != null && AxisLockAngleHigh.Handle != IntPtr.Zero)
                AxisLockAngleHigh.Dispose();



            m_aMotor = new btGeneric6DofConstraint(Body, _parent_scene.TerrainBody, _parent_scene.TransZero,
                                                   _parent_scene.TransZero, false);

            float endNoLock = (360 * Utils.DEG_TO_RAD);
            AxisLockAngleHigh = new btVector3((axislock.X == 0) ? 0 : endNoLock, (axislock.Y == 0) ? 0 : endNoLock, (axislock.Z == 0) ? 0 : endNoLock);

            m_aMotor.setAngularLowerLimit(_parent_scene.VectorZero);
            m_aMotor.setAngularUpperLimit(AxisLockAngleHigh);
            m_aMotor.setLinearLowerLimit(AxisLockLinearLow);
            m_aMotor.setLinearUpperLimit(AxisLockLinearHigh);
            _parent_scene.getBulletWorld().addConstraint((btTypedConstraint)m_aMotor);
            //m_aMotor.


        }
        internal void DisableAxisMotor()
        {
            if (m_aMotor != null && m_aMotor.Handle != IntPtr.Zero)
            {
                _parent_scene.getBulletWorld().removeConstraint(m_aMotor);
                m_aMotor.Dispose();
                m_aMotor = null;
            }
        }

    }
}

