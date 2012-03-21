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

/* Revision 2011/12 by Ubit Umarov
 *
 * 
 */

/*
 * Revised August 26 2009 by Kitto Flora. ODEDynamics.cs replaces
 * ODEVehicleSettings.cs. It and ODEPrim.cs are re-organised:
 * ODEPrim.cs contains methods dealing with Prim editing, Prim
 * characteristics and Kinetic motion.
 * ODEDynamics.cs contains methods dealing with Prim Physical motion
 * (dynamics) and the associated settings. Old Linear and angular
 * motors for dynamic motion have been replace with  MoveLinear()
 * and MoveAngular(); 'Physical' is used only to switch ODE dynamic 
 * simualtion on/off; VEHICAL_TYPE_NONE/VEHICAL_TYPE_<other> is to
 * switch between 'VEHICLE' parameter use and general dynamics
 * settings use.
 */

//#define SPAM

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using log4net;
using OpenMetaverse;
using OdeAPI;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;


namespace OpenSim.Region.Physics.OdePlugin
{
    public class OdePrim : PhysicsActor
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_isphysical;
        private bool m_fakeisphysical;
        private bool m_isphantom;
        private bool m_fakeisphantom;

        protected bool m_building;
        private Quaternion m_lastorientation = new Quaternion();
        private Quaternion _orientation;

        private Vector3 _position;
        private Vector3 _velocity;
        private Vector3 _torque;
        private Vector3 m_lastVelocity;
        private Vector3 m_lastposition;
        private Vector3 m_rotationalVelocity;
        private Vector3 _size;
        private Vector3 _acceleration;
        private Vector3 m_angularlock = Vector3.One;
        private IntPtr Amotor = IntPtr.Zero;

        private Vector3 m_force;
        private Vector3 m_forceacc;
        private Vector3 m_angularForceacc;

        private Vector3 m_PIDTarget;
        private float m_PIDTau;
        private float PID_D = 35f;
        private float PID_G = 25f;
        private bool m_usePID;

        // KF: These next 7 params apply to llSetHoverHeight(float height, integer water, float tau),
        // and are for non-VEHICLES only.

        private float m_PIDHoverHeight;
        private float m_PIDHoverTau;
        private bool m_useHoverPID;
        private PIDHoverType m_PIDHoverType = PIDHoverType.Ground;
        private float m_targetHoverHeight;
        private float m_groundHeight;
        private float m_waterHeight;
        private float m_buoyancy;                //KF: m_buoyancy should be set by llSetBuoyancy() for non-vehicle. 

        private int body_autodisable_frames = 20;

        private const CollisionCategories m_default_collisionFlags = (CollisionCategories.Geom
                                                        | CollisionCategories.Space
                                                        | CollisionCategories.Body
                                                        | CollisionCategories.Character
                                                        );
//        private bool m_collidesLand = true;
        private bool m_collidesWater;
        public bool m_returnCollisions;
        private bool m_softcolide;

        private bool m_NoColide;  // for now only for internal use for bad meshs

        // Default we're a Geometry
        private CollisionCategories m_collisionCategories = (CollisionCategories.Geom);

        // Default, Collide with Other Geometries, spaces and Bodies
        private CollisionCategories m_collisionFlags = m_default_collisionFlags;

        public bool m_disabled;


        public uint m_localID;

        private PrimitiveBaseShape _pbs;
        public OdeScene _parent_scene;

        /// <summary>
        /// The physics space which contains prim geometry
        /// </summary>
        public IntPtr m_targetSpace = IntPtr.Zero;

        public IntPtr prim_geom;
        public IntPtr _triMeshData;

        private PhysicsActor _parent;

        private List<OdePrim> childrenPrim = new List<OdePrim>();

        private bool m_iscolliding;

        public bool m_isSelected;
        private bool m_delaySelect;
        private bool m_lastdoneSelected;
        public bool m_outbounds;

        internal bool m_isVolumeDetect; // If true, this prim only detects collisions but doesn't collide actively

        private bool m_throttleUpdates;
        private int throttleCounter;
        public float m_collisionscore;
        int m_colliderfilter = 0;

        public IntPtr collide_geom; // for objects: geom if single prim space it linkset

        private float m_density = 10.000006836f; // Aluminum g/cm3;
        private byte m_shapetype;
        public bool _zeroFlag;
        private bool m_lastUpdateSent;

        public IntPtr Body = IntPtr.Zero;
        public String Name { get; private set; }
        private Vector3 _target_velocity;

        public Vector3 primOOBsize; // prim real dimensions from mesh 
        public Vector3 primOOBoffset; // its centroid out of mesh or rest aabb
        public float primOOBradiusSQ;
        public d.Mass primdMass; // prim inertia information on it's own referencial
        float primMass; // prim own mass
        float _mass; // object mass acording to case
        private bool hasOOBoffsetFromMesh = false; // if true we did compute it form mesh centroid, else from aabb

        public int givefakepos = 0;
        private Vector3 fakepos;
        public int givefakeori = 0;
        private Quaternion fakeori;

        public int m_eventsubscription;
        private CollisionEventUpdate CollisionEventsThisFrame = new CollisionEventUpdate();

        public volatile bool childPrim;

        public ODEDynamics m_vehicle;

        internal int m_material = (int)Material.Wood;
        private float mu;
        private float bounce;

        /// <summary>
        /// Is this prim subject to physics?  Even if not, it's still solid for collision purposes.
        /// </summary>
        public override bool IsPhysical  // this is not reliable for internal use
        {
            get { return m_fakeisphysical; }
            set
            {
                m_fakeisphysical = value; // we show imediatly to outside that we changed physical
                // and also to stop imediatly some updates
                // but real change will only happen in taintprocessing

                if (!value) // Zero the remembered last velocity
                    m_lastVelocity = Vector3.Zero;
                AddChange(changes.Physical, value);
            }
        }

        public override bool Phantom  // this is not reliable for internal use
        {
            get { return m_fakeisphantom; }
            set
            {
                m_fakeisphantom = value; // we show imediatly to outside that we changed physical
                // and also to stop imediatly some updates
                // but real change will only happen in taintprocessing

                AddChange(changes.Phantom, value);
            }
        }

        public override bool Building  // this is not reliable for internal use
        {
            get { return m_building; }
            set
            {
                if (value)
                    m_building = true;
                AddChange(changes.building, value);
            }
        }

        public override void getContactData(ref ContactData cdata)
        {
            cdata.mu = mu;
            cdata.bounce = bounce;

            //            cdata.softcolide = m_softcolide;
            cdata.softcolide = false;

            if (m_isphysical)
            {
                ODEDynamics veh;
                if (_parent != null)
                    veh = ((OdePrim)_parent).m_vehicle;
                else
                    veh = m_vehicle;

                if (veh != null && veh.Type != Vehicle.TYPE_NONE)
                    cdata.mu *= veh.FrictionFactor;
            }
        }    

        public override int PhysicsActorType
        {
            get { return (int)ActorTypes.Prim; }
            set { return; }
        }

        public override bool SetAlwaysRun
        {
            get { return false; }
            set { return; }
        }

        public override uint LocalID
        {
            get
            {
                return m_localID;
            }
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
                if (value)
                    m_isSelected = value; // if true set imediatly to stop moves etc
                AddChange(changes.Selected, value);
            }
        }

        public override bool Flying
        {
            // no flying prims for you
            get { return false; }
            set { }
        }

        public override bool IsColliding
        {
            get { return m_iscolliding; }
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
                {
                    m_softcolide = false;
                    m_iscolliding = false;
                }
                else
                    m_iscolliding = true;
            }
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

        public override bool ThrottleUpdates
        {
            get { return m_throttleUpdates; }
            set { m_throttleUpdates = value; }
        }

        public override bool Stopped
        {
            get { return _zeroFlag; }
        }

        public override Vector3 Position
        {
            get
            {
                if (givefakepos > 0)
                    return fakepos;
                else
                    return _position;
            }

            set
            {
                fakepos = value;
                givefakepos++;
                AddChange(changes.Position, value);
            }
        }

        public override Vector3 Size
        {
            get { return _size; }
            set
            {
                if (value.IsFinite())
                {
                    AddChange(changes.Size, value);
                }
                else
                {
                    m_log.WarnFormat("[PHYSICS]: Got NaN Size on object {0}", Name);
                }
            }
        }

        public override float Mass
        {
            get { return _mass; }
        }

        public override Vector3 Force
        {
            //get { return Vector3.Zero; }
            get { return m_force; }
            set
            {
                if (value.IsFinite())
                {
                    AddChange(changes.Force, value);
                }
                else
                {
                    m_log.WarnFormat("[PHYSICS]: NaN in Force Applied to an Object {0}", Name);
                }
            }
        }

        public override void SetVolumeDetect(int param)
        {
            AddChange(changes.VolumeDtc, (param != 0));
        }

        public override Vector3 GeometricCenter
        {
            get
            {
                return Vector3.Zero;
            }
        }

        public override Vector3 CenterOfMass
        {
            get
            {
                d.Vector3 dtmp;
                if (IsPhysical && !childPrim && Body != IntPtr.Zero)
                {
                    dtmp = d.BodyGetPosition(Body);
                    return new Vector3(dtmp.X, dtmp.Y, dtmp.Z);
                }
                else if (prim_geom != IntPtr.Zero)
                {
                    d.Quaternion dq;
                    d.GeomCopyQuaternion(prim_geom, out dq);
                    Quaternion q;
                    q.X = dq.X;
                    q.Y = dq.Y;
                    q.Z = dq.Z;
                    q.W = dq.W;

                    Vector3 vtmp = primOOBoffset * q;
                    dtmp = d.GeomGetPosition(prim_geom);
                    return new Vector3(dtmp.X + vtmp.X, dtmp.Y + vtmp.Y, dtmp.Z + vtmp.Z);
                }
                else
                    return Vector3.Zero;
            }
        }
        /*
                public override Vector3 PrimOOBsize
                    {
                    get
                        {
                        return primOOBsize;
                        }
                    }

                public override Vector3 PrimOOBoffset
                    {
                    get
                        {
                        return primOOBoffset;
                        }
                    }

                public override float PrimOOBRadiusSQ
                    {
                    get
                        {
                        return primOOBradiusSQ;
                        }
                    }
        */
        public override PrimitiveBaseShape Shape
        {
            set
            {
                AddChange(changes.Shape, value);
            }
        }

        public override Vector3 Velocity
        {
            get
            {
                if (_zeroFlag)
                    return Vector3.Zero;
                return _velocity;
            }
            set
            {
                if (value.IsFinite())
                {
                    AddChange(changes.Velocity, value);
                    //                    _velocity = value;

                }
                else
                {
                    m_log.WarnFormat("[PHYSICS]: Got NaN Velocity in Object {0}", Name);
                }

            }
        }

        public override Vector3 Torque
        {
            get
            {
                if (!IsPhysical || Body == IntPtr.Zero)
                    return Vector3.Zero;

                return _torque;
            }

            set
            {
                if (value.IsFinite())
                {
                    AddChange(changes.Torque, value);
                }
                else
                {
                    m_log.WarnFormat("[PHYSICS]: Got NaN Torque in Object {0}", Name);
                }
            }
        }

        public override float CollisionScore
        {
            get { return m_collisionscore; }
            set { m_collisionscore = value; }
        }

        public override bool Kinematic
        {
            get { return false; }
            set { }
        }

        public override Quaternion Orientation
        {
            get
            {
                if (givefakeori > 0)
                    return fakeori;
                else

                    return _orientation;
            }
            set
            {
                if (QuaternionIsFinite(value))
                {
                    fakeori = value;
                    givefakeori++;
                    AddChange(changes.Orientation, value);
                }
                else
                    m_log.WarnFormat("[PHYSICS]: Got NaN quaternion Orientation from Scene in Object {0}", Name);

            }
        }

        public override Vector3 Acceleration
        {
            get { return _acceleration; }
            set { }
        }

        public override Vector3 RotationalVelocity
        {
            get
            {
                Vector3 pv = Vector3.Zero;
                if (_zeroFlag)
                    return pv;
                m_lastUpdateSent = false;

                if (m_rotationalVelocity.ApproxEquals(pv, 0.0001f))
                    return pv;

                return m_rotationalVelocity;
            }
            set
            {
                if (value.IsFinite())
                {
                    m_rotationalVelocity = value;
                    if (Body != IntPtr.Zero && !d.BodyIsEnabled(Body))
                        d.BodyEnable(Body);
                }
                else
                {
                    m_log.WarnFormat("[PHYSICS]: Got NaN RotationalVelocity in Object {0}", Name);
                }
            }
        }


        public override float Buoyancy
        {
            get { return m_buoyancy; }
            set
            {
                m_buoyancy = value;
            }
        }

        public override bool FloatOnWater
        {
            set
            {
                AddChange(changes.CollidesWater, value);
            }
        }

        public override Vector3 PIDTarget
        {
            set
            {
                if (value.IsFinite())
                {
                    m_PIDTarget = value;
                }
                else
                    m_log.WarnFormat("[PHYSICS]: Got NaN PIDTarget from Scene on Object {0}", Name);
            }
        }

        public override bool PIDActive { set { m_usePID = value; } }
        public override float PIDTau { set { m_PIDTau = value; } }

        public override float PIDHoverHeight { set { m_PIDHoverHeight = value; ; } }
        public override bool PIDHoverActive { set { m_useHoverPID = value; } }
        public override PIDHoverType PIDHoverType { set { m_PIDHoverType = value; } }
        public override float PIDHoverTau { set { m_PIDHoverTau = value; } }

        public override Quaternion APIDTarget { set { return; } }

        public override bool APIDActive { set { return; } }

        public override float APIDStrength { set { return; } }

        public override float APIDDamping { set { return; } }

        public override int VehicleType
        {
            // we may need to put a fake on this
            get
            {
                if (m_vehicle == null)
                    return (int)Vehicle.TYPE_NONE;
                else
                    return (int)m_vehicle.Type;
            }
            set
            {
                AddChange(changes.VehicleType, value);
            }
        }

        public override void VehicleFloatParam(int param, float value)
        {
            strVehicleFloatParam fp = new strVehicleFloatParam();
            fp.param = param;
            fp.value = value;
            AddChange(changes.VehicleFloatParam, fp);
        }

        public override void VehicleVectorParam(int param, Vector3 value)
        {
            strVehicleVectorParam fp = new strVehicleVectorParam();
            fp.param = param;
            fp.value = value;
            AddChange(changes.VehicleVectorParam, fp);
        }

        public override void VehicleRotationParam(int param, Quaternion value)
        {
            strVehicleQuatParam fp = new strVehicleQuatParam();
            fp.param = param;
            fp.value = value;
            AddChange(changes.VehicleRotationParam, fp);
        }

        public override void VehicleFlags(int param, bool value)
        {
            strVehicleBoolParam bp = new strVehicleBoolParam();
            bp.param = param;
            bp.value = value;
            AddChange(changes.VehicleFlags, bp);
        }

        public override void SetVehicle(object vdata)
        {
            AddChange(changes.SetVehicle, vdata);
        }
        public void SetAcceleration(Vector3 accel)
        {
            _acceleration = accel;
        }

        public override void AddForce(Vector3 force, bool pushforce)
        {
            if (force.IsFinite())
            {
                AddChange(changes.AddForce, force / _parent_scene.ODE_STEPSIZE);
            }
            else
            {
                m_log.WarnFormat("[PHYSICS]: Got Invalid linear force vector from Scene in Object {0}", Name);
            }
            //m_log.Info("[PHYSICS]: Added Force:" + force.ToString() +  " to prim at " + Position.ToString());
        }

        public override void AddAngularForce(Vector3 force, bool pushforce)
        {
            if (force.IsFinite())
            {
                AddChange(changes.AddAngForce, force / _parent_scene.ODE_STEPSIZE);
            }
            else
            {
                m_log.WarnFormat("[PHYSICS]: Got Invalid Angular force vector from Scene in Object {0}", Name);
            }
        }

        public override void CrossingFailure()
        {
            if (m_outbounds)
            {
                _position.X = Util.Clip(_position.X, 0.5f, _parent_scene.WorldExtents.X - 0.5f);
                _position.Y = Util.Clip(_position.Y, 0.5f, _parent_scene.WorldExtents.Y - 0.5f);
                _position.Z = Util.Clip(_position.Z + 0.2f, -100f, 50000f);

                m_lastposition = _position;
                _velocity.X = 0;
                _velocity.Y = 0;
                _velocity.Z = 0;

                m_lastVelocity = _velocity;
                if (m_vehicle != null && m_vehicle.Type != Vehicle.TYPE_NONE)
                    m_vehicle.Stop();

                if(Body != IntPtr.Zero)
                    d.BodySetLinearVel(Body, 0, 0, 0); // stop it
                if (prim_geom != IntPtr.Zero)
                    d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);

                m_outbounds = false;
                changeDisable(false);
                base.RequestPhysicsterseUpdate();
            }
        }

        public override void SetMomentum(Vector3 momentum)
        {
        }

        public override void SetMaterial(int pMaterial)
        {
            m_material = pMaterial;
            mu = _parent_scene.m_materialContactsData[pMaterial].mu;
            bounce = _parent_scene.m_materialContactsData[pMaterial].bounce;
        }

        public void setPrimForRemoval()
        {
            AddChange(changes.Remove, null);
        }

        public override void link(PhysicsActor obj)
        {
            AddChange(changes.Link, obj);
        }

        public override void delink()
        {
            AddChange(changes.DeLink, null);
        }

        public override void LockAngularMotion(Vector3 axis)
        {
            // reverse the zero/non zero values for ODE.
            if (axis.IsFinite())
            {
                axis.X = (axis.X > 0) ? 1f : 0f;
                axis.Y = (axis.Y > 0) ? 1f : 0f;
                axis.Z = (axis.Z > 0) ? 1f : 0f;
                m_log.DebugFormat("[axislock]: <{0},{1},{2}>", axis.X, axis.Y, axis.Z);
                AddChange(changes.AngLock, axis);
            }
            else
            {
                m_log.WarnFormat("[PHYSICS]: Got NaN locking axis from Scene on Object {0}", Name);
            }
        }

        public override void SubscribeEvents(int ms)
        {
            m_eventsubscription = ms;
            _parent_scene.AddCollisionEventReporting(this);
        }

        public override void UnSubscribeEvents()
        {
            _parent_scene.RemoveCollisionEventReporting(this);
            m_eventsubscription = 0;
        }

        public void AddCollisionEvent(uint CollidedWith, ContactPoint contact)
        {
            if (CollisionEventsThisFrame == null)
                CollisionEventsThisFrame = new CollisionEventUpdate();

            CollisionEventsThisFrame.AddCollider(CollidedWith, contact);
        }

        public void SendCollisions()
        {
            if (CollisionEventsThisFrame == null)
                return;

            base.SendCollisionUpdate(CollisionEventsThisFrame);

            if (CollisionEventsThisFrame.m_objCollisionList.Count == 0)
                CollisionEventsThisFrame = null;
            else
                CollisionEventsThisFrame = new CollisionEventUpdate();
        }

        public override bool SubscribedEvents()
        {
            if (m_eventsubscription > 0)
                return true;
            return false;
        }


        public OdePrim(String primName, OdeScene parent_scene, Vector3 pos, Vector3 size,
                       Quaternion rotation, PrimitiveBaseShape pbs, bool pisPhysical,bool pisPhantom,byte _shapeType,uint plocalID)
        {
            Name = primName;
            LocalID = plocalID;

            m_vehicle = null;

            if (!pos.IsFinite())
            {
                pos = new Vector3(((float)Constants.RegionSize * 0.5f), ((float)Constants.RegionSize * 0.5f),
                    parent_scene.GetTerrainHeightAtXY(((float)Constants.RegionSize * 0.5f), ((float)Constants.RegionSize * 0.5f)) + 0.5f);
                m_log.WarnFormat("[PHYSICS]: Got nonFinite Object create Position for {0}", Name);
            }
            _position = pos;
            givefakepos = 0;

            PID_D = parent_scene.bodyPIDD;
            PID_G = parent_scene.bodyPIDG;
            m_density = parent_scene.geomDefaultDensity;
            // m_tensor = parent_scene.bodyMotorJointMaxforceTensor;
            body_autodisable_frames = parent_scene.bodyFramesAutoDisable;

            prim_geom = IntPtr.Zero;
            collide_geom = IntPtr.Zero;
            Body = IntPtr.Zero;

            if (!size.IsFinite())
            {
                size = new Vector3(0.5f, 0.5f, 0.5f);
                m_log.WarnFormat("[PHYSICS]: Got nonFinite Object create Size for {0}", Name);
            }

            if (size.X <= 0) size.X = 0.01f;
            if (size.Y <= 0) size.Y = 0.01f;
            if (size.Z <= 0) size.Z = 0.01f;

            _size = size;

            if (!QuaternionIsFinite(rotation))
            {
                rotation = Quaternion.Identity;
                m_log.WarnFormat("[PHYSICS]: Got nonFinite Object create Rotation for {0}", Name);
            }

            _orientation = rotation;
            givefakeori = 0;

            _pbs = pbs;

            _parent_scene = parent_scene;
            m_targetSpace = IntPtr.Zero;

            if (pos.Z < 0)
            {
                m_isphysical = false;
            }
            else
            {
                m_isphysical = pisPhysical;
            }
            m_fakeisphysical = m_isphysical;

            m_isVolumeDetect = false;

            m_force = Vector3.Zero;

            m_iscolliding = false;
            m_colliderfilter = 0;
            m_softcolide = true;
            m_NoColide = false;

            hasOOBoffsetFromMesh = false;
            _triMeshData = IntPtr.Zero;

            m_shapetype = _shapeType;

            m_lastdoneSelected = false;
            m_isSelected = false;
            m_delaySelect = false;

            m_isphantom = pisPhantom;
            m_fakeisphantom = pisPhantom;

            mu = parent_scene.m_materialContactsData[(int)Material.Wood].mu;
            bounce = parent_scene.m_materialContactsData[(int)Material.Wood].bounce;

            CalcPrimBodyData();

            m_building = true; // control must set this to false when done

            AddChange(changes.Add, null);
        }

        private void resetCollisionAccounting()
        {
            m_collisionscore = 0;
        }

        private void createAMotor(Vector3 axis)
        {
            if (Body == IntPtr.Zero)
                return;

            if (Amotor != IntPtr.Zero)
            {
                d.JointDestroy(Amotor);
                Amotor = IntPtr.Zero;
            }

            int axisnum = 3 - (int)(axis.X + axis.Y + axis.Z);

            if (axisnum <= 0)
                return;

            // stop it
            d.BodySetTorque(Body, 0, 0, 0);
            d.BodySetAngularVel(Body, 0, 0, 0);

            Amotor = d.JointCreateAMotor(_parent_scene.world, IntPtr.Zero);
            d.JointAttach(Amotor, Body, IntPtr.Zero);

            d.JointSetAMotorMode(Amotor, 0);

            d.JointSetAMotorNumAxes(Amotor, axisnum);

            // get current orientation to lock

            d.Quaternion dcur = d.BodyGetQuaternion(Body);
            Quaternion curr; // crap convertion between identical things
            curr.X = dcur.X;
            curr.Y = dcur.Y;
            curr.Z = dcur.Z;
            curr.W = dcur.W;
            Vector3 ax;

            int i = 0;
            int j = 0;
            if (axis.X == 0)
            {
                ax = (new Vector3(1, 0, 0)) * curr; // rotate world X to current local X
                // ODE should do this  with axis relative to body 1 but seems to fail
                d.JointSetAMotorAxis(Amotor, 0, 0, ax.X, ax.Y, ax.Z);
                d.JointSetAMotorAngle(Amotor, 0, 0);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.LoStop, -0.000001f);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.HiStop, 0.000001f);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.Vel, 0);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.FudgeFactor, 0.0001f);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.Bounce, 0f);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.FMax, 5e8f);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.StopCFM, 0f);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.StopERP, 0.8f);
                i++;
                j = 256; // move to next axis set
            }

            if (axis.Y == 0)
            {
                ax = (new Vector3(0, 1, 0)) * curr;
                d.JointSetAMotorAxis(Amotor, i, 0, ax.X, ax.Y, ax.Z);
                d.JointSetAMotorAngle(Amotor, i, 0);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.LoStop, -0.000001f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.HiStop, 0.000001f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.Vel, 0);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.FudgeFactor, 0.0001f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.Bounce, 0f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.FMax, 5e8f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.StopCFM, 0f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.StopERP, 0.8f);
                i++;
                j += 256;
            }

            if (axis.Z == 0)
            {
                ax = (new Vector3(0, 0, 1)) * curr;
                d.JointSetAMotorAxis(Amotor, i, 0, ax.X, ax.Y, ax.Z);
                d.JointSetAMotorAngle(Amotor, i, 0);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.LoStop, -0.000001f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.HiStop, 0.000001f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.Vel, 0);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.FudgeFactor, 0.0001f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.Bounce, 0f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.FMax, 5e8f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.StopCFM, 0f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.StopERP, 0.8f);
            }
        }

        private bool setMesh(OdeScene parent_scene)
        {
            if (Body != IntPtr.Zero)
            {
                if (childPrim)
                {
                    if (_parent != null)
                    {
                        OdePrim parent = (OdePrim)_parent;
                        parent.ChildDelink(this, false);
                    }
                }
                else
                {
                    DestroyBody();
                }
            }

            bool convex;
            if (m_shapetype == 0)
                convex = false;
            else
                convex = true;

            IMesh mesh = _parent_scene.mesher.CreateMesh(Name, _pbs, _size, (int)LevelOfDetail.High, true,convex);
            if (mesh == null)
            {
                m_log.WarnFormat("[PHYSICS]: CreateMesh Failed on prim {0} at <{1},{2},{3}>.", Name, _position.X, _position.Y, _position.Z);
                return false;
            }

            IntPtr vertices, indices;
            int vertexCount, indexCount;
            int vertexStride, triStride;

            mesh.getVertexListAsPtrToFloatArray(out vertices, out vertexStride, out vertexCount); // Note, that vertices are fixed in unmanaged heap
            mesh.getIndexListAsPtrToIntArray(out indices, out triStride, out indexCount); // Also fixed, needs release after usage

            if (vertexCount == 0 || indexCount == 0)
            {
                m_log.WarnFormat("[PHYSICS]: Got invalid mesh on prim {0} at <{1},{2},{3}>. mesh UUID {4}",
                    Name, _position.X, _position.Y, _position.Z, _pbs.SculptTexture.ToString());
                mesh.releaseSourceMeshData();
                return false;
            }

            primOOBoffset = mesh.GetCentroid();
            hasOOBoffsetFromMesh = true;

            mesh.releaseSourceMeshData();

            IntPtr geo = IntPtr.Zero;

            try
            {
                _triMeshData = d.GeomTriMeshDataCreate();

                d.GeomTriMeshDataBuildSimple(_triMeshData, vertices, vertexStride, vertexCount, indices, indexCount, triStride);
                d.GeomTriMeshDataPreprocess(_triMeshData);

                _parent_scene.waitForSpaceUnlock(m_targetSpace);
                geo = d.CreateTriMesh(m_targetSpace, _triMeshData, null, null, null);
            }

            catch (Exception e)
            {
                m_log.ErrorFormat("[PHYSICS]: SetGeom Mesh failed for {0} exception: {1}", Name, e);
                if (_triMeshData != IntPtr.Zero)
                {
                    d.GeomTriMeshDataDestroy(_triMeshData);
                    _triMeshData = IntPtr.Zero;
                }
                return false;
            }

            SetGeom(geo);
            return true;
        }

        private void SetGeom(IntPtr geom)
        {
            prim_geom = geom;
            //Console.WriteLine("SetGeom to " + prim_geom + " for " + Name);
            if (prim_geom != IntPtr.Zero)
            {
                if (m_NoColide)
                {
                    d.GeomSetCategoryBits(prim_geom, 0);
                    if (m_isphysical)
                    {
                        d.GeomSetCollideBits(prim_geom, (int)CollisionCategories.Land);
                    }
                    else
                    {
                        d.GeomSetCollideBits(prim_geom, 0);
                        d.GeomDisable(prim_geom);
                    }
                }
                else
                {
                    d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                    d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
                }

                CalcPrimBodyData();

                _parent_scene.geom_name_map[prim_geom] = Name;
                _parent_scene.actor_name_map[prim_geom] = this;

            }
            else
                m_log.Warn("Setting bad Geom");
        }


        /// <summary>
        /// Create a geometry for the given mesh in the given target space.
        /// </summary>
        /// <param name="m_targetSpace"></param>
        /// <param name="mesh">If null, then a mesh is used that is based on the profile shape data.</param>
        private void CreateGeom()
        {
            if (_triMeshData != IntPtr.Zero)
            {
                d.GeomTriMeshDataDestroy(_triMeshData);
                _triMeshData = IntPtr.Zero;
            }

            bool haveMesh = false;
            hasOOBoffsetFromMesh = false;
            m_NoColide = false;

            if (_parent_scene.needsMeshing(_pbs))
            {
                haveMesh = setMesh(_parent_scene); // this will give a mesh to non trivial known prims
                if (!haveMesh)
                    m_NoColide = true;
            }

            if (!haveMesh)
            {
                if (_pbs.ProfileShape == ProfileShape.HalfCircle && _pbs.PathCurve == (byte)Extrusion.Curve1
                    && _size.X == _size.Y && _size.Y == _size.Z)
                { // it's a sphere
                    _parent_scene.waitForSpaceUnlock(m_targetSpace);
                    try
                    {
                        SetGeom(d.CreateSphere(m_targetSpace, _size.X * 0.5f));
                    }
                    catch (Exception e)
                    {
                        m_log.WarnFormat("[PHYSICS]: Create sphere failed: {0}", e);
                        return;
                    }
                }
                else
                {// do it as a box
                    _parent_scene.waitForSpaceUnlock(m_targetSpace);
                    try
                    {
                        //Console.WriteLine("  CreateGeom 4");
                        SetGeom(d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z));
                    }
                    catch (Exception e)
                    {
                        m_log.Warn("[PHYSICS]: Create box failed: {0}", e);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Set a new geometry for this prim.
        /// </summary>
        /// <param name="geom"></param>
        private void RemoveGeom()
        {
            if (prim_geom != IntPtr.Zero)
            {
                _parent_scene.geom_name_map.Remove(prim_geom);
                _parent_scene.actor_name_map.Remove(prim_geom);
                try
                {
                    d.GeomDestroy(prim_geom);
                    if (_triMeshData != IntPtr.Zero)
                    {
                        d.GeomTriMeshDataDestroy(_triMeshData);
                        _triMeshData = IntPtr.Zero;
                    }
                }
                //                catch (System.AccessViolationException)
                catch (Exception e)
                {
                    m_log.ErrorFormat("[PHYSICS]: PrimGeom destruction failed for {0} exception {1}", Name, e);
                }

                prim_geom = IntPtr.Zero;
            }
            else
            {
                m_log.ErrorFormat("[PHYSICS]: PrimGeom destruction BAD {0}", Name);
            }
            Body = IntPtr.Zero;
            hasOOBoffsetFromMesh = false;
            CalcPrimBodyData();
        }

        private void ChildSetGeom(OdePrim odePrim)
        {
            // well.. 
            DestroyBody();
            MakeBody();
        }

        //sets non physical prim m_targetSpace to right space in spaces grid for static prims
        // should only be called for non physical prims unless they are becoming non physical
        private void SetInStaticSpace(OdePrim prim)
        {
            IntPtr targetSpace = _parent_scene.MoveGeomToStaticSpace(prim.prim_geom, prim._position, prim.m_targetSpace);
            prim.m_targetSpace = targetSpace;
            d.GeomEnable(prim_geom);
        }

        public void enableBodySoft()
        {
            if (!childPrim && !m_isSelected)
            {
                if (m_isphysical && Body != IntPtr.Zero)
                {
                    if (m_isphantom && !m_isVolumeDetect)
                    {
                        m_collisionCategories = 0;
                        m_collisionFlags = CollisionCategories.Land;
                    }
                    else
                    {
                        m_collisionCategories |= CollisionCategories.Body;
                        m_collisionFlags |= (CollisionCategories.Land | CollisionCategories.Wind);
                    }

                    foreach (OdePrim prm in childrenPrim)
                    {
                        prm.m_collisionCategories = m_collisionCategories;
                        prm.m_collisionFlags = m_collisionFlags;

                        if (prm.prim_geom != IntPtr.Zero)
                        {
                            if (prm.m_NoColide)
                            {
                                d.GeomSetCategoryBits(prm.prim_geom, 0);
                                d.GeomSetCollideBits(prm.prim_geom, (int)CollisionCategories.Land);
                            }
                            else
                            {
                                d.GeomSetCategoryBits(prm.prim_geom, (int)m_collisionCategories);
                                d.GeomSetCollideBits(prm.prim_geom, (int)m_collisionFlags);
                            }
                            d.GeomEnable(prm.prim_geom);
                        }
                    }

                    if (prim_geom != IntPtr.Zero)
                    {
                        if (m_NoColide)
                        {
                            d.GeomSetCategoryBits(prim_geom, 0);
                            d.GeomSetCollideBits(prim_geom, (int)CollisionCategories.Land);
                        }
                        else
                        {
                            d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                            d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
                        }
                        d.GeomEnable(prim_geom);
                    }
                    d.BodyEnable(Body);
                }
            }
            m_disabled = false;
            resetCollisionAccounting(); // this sets m_disable to false
        }

        private void disableBodySoft()
        {
            m_disabled = true;
            if (!childPrim)
            {
                if (m_isphysical && Body != IntPtr.Zero)
                {
                    m_collisionCategories &= ~CollisionCategories.Body;
                    m_collisionFlags &= ~(CollisionCategories.Wind | CollisionCategories.Land);

                    foreach (OdePrim prm in childrenPrim)
                    {
                        prm.m_collisionCategories = m_collisionCategories;
                        prm.m_collisionFlags = m_collisionFlags;

                        if (prm.prim_geom != IntPtr.Zero)
                        {
                            if (prm.m_NoColide)
                            {
                                d.GeomSetCategoryBits(prm.prim_geom, 0);
                                d.GeomSetCollideBits(prm.prim_geom, 0);
                            }
                            else
                            {
                                d.GeomSetCategoryBits(prm.prim_geom, (int)m_collisionCategories);
                                d.GeomSetCollideBits(prm.prim_geom, (int)m_collisionFlags);
                            }
                            d.GeomDisable(prm.prim_geom);
                        }
                    }

                    if (prim_geom != IntPtr.Zero)
                    {
                        if (m_NoColide)
                        {
                            d.GeomSetCategoryBits(prim_geom, 0);
                            d.GeomSetCollideBits(prim_geom, 0);
                        }
                        else
                        {
                            d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                            d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
                        }
                        d.GeomDisable(prim_geom);
                    }

                    d.BodyDisable(Body);
                }
            }
        }

        private void MakeBody()
        {
            if (!m_isphysical) // only physical get bodies
                return;

            if (childPrim)  // child prims don't get bodies;
                return;

            if (m_building)
                return;

            if (prim_geom == IntPtr.Zero)
            {
                m_log.Warn("[PHYSICS]: Unable to link the linkset.  Root has no geom yet");
                return;
            }

            if (Body != IntPtr.Zero)
            {
                d.BodyDestroy(Body);
                Body = IntPtr.Zero;
                m_log.Warn("[PHYSICS]: MakeBody called having a body");
            }


            if (d.GeomGetBody(prim_geom) != IntPtr.Zero)
            {
                d.GeomSetBody(prim_geom, IntPtr.Zero);
                m_log.Warn("[PHYSICS]: MakeBody root geom already had a body");
            }

            d.Matrix3 mymat = new d.Matrix3();
            d.Quaternion myrot = new d.Quaternion();
            d.Mass objdmass = new d.Mass { };

            Body = d.BodyCreate(_parent_scene.world);

            DMassDup(ref primdMass, out objdmass);

            // rotate inertia
            myrot.X = _orientation.X;
            myrot.Y = _orientation.Y;
            myrot.Z = _orientation.Z;
            myrot.W = _orientation.W;

            d.RfromQ(out mymat, ref myrot);
            d.MassRotate(ref objdmass, ref mymat);

            // set the body rotation and position
            d.BodySetRotation(Body, ref mymat);

            // recompute full object inertia if needed
            if (childrenPrim.Count > 0)
            {
                d.Matrix3 mat = new d.Matrix3();
                d.Quaternion quat = new d.Quaternion();
                d.Mass tmpdmass = new d.Mass { };
                Vector3 rcm;

                rcm.X = _position.X + objdmass.c.X;
                rcm.Y = _position.Y + objdmass.c.Y;
                rcm.Z = _position.Z + objdmass.c.Z;

                lock (childrenPrim)
                {
                    foreach (OdePrim prm in childrenPrim)
                    {
                        if (prm.prim_geom == IntPtr.Zero)
                        {
                            m_log.Warn("[PHYSICS]: Unable to link one of the linkset elements, skipping it.  No geom yet");
                            continue;
                        }

                        DMassCopy(ref prm.primdMass, ref tmpdmass);

                        // apply prim current rotation to inertia
                        quat.X = prm._orientation.X;
                        quat.Y = prm._orientation.Y;
                        quat.Z = prm._orientation.Z;
                        quat.W = prm._orientation.W;
                        d.RfromQ(out mat, ref quat);
                        d.MassRotate(ref tmpdmass, ref mat);

                        Vector3 ppos = prm._position;
                        ppos.X += tmpdmass.c.X - rcm.X;
                        ppos.Y += tmpdmass.c.Y - rcm.Y;
                        ppos.Z += tmpdmass.c.Z - rcm.Z;

                        // refer inertia to root prim center of mass position
                        d.MassTranslate(ref tmpdmass,
                            ppos.X,
                            ppos.Y,
                            ppos.Z);

                        d.MassAdd(ref objdmass, ref tmpdmass); // add to total object inertia
                        // fix prim colision cats

                        if (d.GeomGetBody(prm.prim_geom) != IntPtr.Zero)
                        {
                            d.GeomSetBody(prm.prim_geom, IntPtr.Zero);
                            m_log.Warn("[PHYSICS]: MakeBody child geom already had a body");
                        }

                        d.GeomClearOffset(prm.prim_geom);
                        d.GeomSetBody(prm.prim_geom, Body);
                        prm.Body = Body;
                        d.GeomSetOffsetWorldRotation(prm.prim_geom, ref mat); // set relative rotation
                    }
                }
            }

            d.GeomClearOffset(prim_geom); // make sure we don't have a hidden offset
            // associate root geom with body
            d.GeomSetBody(prim_geom, Body);

            d.BodySetPosition(Body, _position.X + objdmass.c.X, _position.Y + objdmass.c.Y, _position.Z + objdmass.c.Z);
            d.GeomSetOffsetWorldPosition(prim_geom, _position.X, _position.Y, _position.Z);

            d.MassTranslate(ref objdmass, -objdmass.c.X, -objdmass.c.Y, -objdmass.c.Z); // ode wants inertia at center of body
            myrot.W = -myrot.W;
            d.RfromQ(out mymat, ref myrot);
            d.MassRotate(ref objdmass, ref mymat);
            d.BodySetMass(Body, ref objdmass);
            _mass = objdmass.mass;

            // disconnect from world gravity so we can apply buoyancy
            d.BodySetGravityMode(Body, false);

            d.BodySetAutoDisableFlag(Body, true);
            d.BodySetAutoDisableSteps(Body, body_autodisable_frames);
            //            d.BodySetLinearDampingThreshold(Body, 0.01f);
            //            d.BodySetAngularDampingThreshold(Body, 0.001f);
            d.BodySetDamping(Body, .002f, .002f);


                if (m_targetSpace != IntPtr.Zero)
                {
                    _parent_scene.waitForSpaceUnlock(m_targetSpace);
                    if (d.SpaceQuery(m_targetSpace, prim_geom))
                        d.SpaceRemove(m_targetSpace, prim_geom);
                }


            if (childrenPrim.Count == 0)
            {
                collide_geom = prim_geom;
                m_targetSpace = _parent_scene.ActiveSpace;
                d.SpaceAdd(m_targetSpace, prim_geom);
            }
            else
            {
                m_targetSpace = d.HashSpaceCreate(_parent_scene.ActiveSpace);
                d.HashSpaceSetLevels(m_targetSpace, -2, 8);
                d.SpaceSetSublevel(m_targetSpace, 3);
                d.SpaceSetCleanup(m_targetSpace, false);
                d.SpaceAdd(m_targetSpace, prim_geom);
                collide_geom = m_targetSpace;
            }

            if (m_delaySelect)
            {
                m_isSelected = true;
                m_delaySelect = false;
            }

            lock (childrenPrim)
            {
                foreach (OdePrim prm in childrenPrim)
                {
                    if (prm.prim_geom == IntPtr.Zero)
                        continue;

                    Vector3 ppos = prm._position;
                    d.GeomSetOffsetWorldPosition(prm.prim_geom, ppos.X, ppos.Y, ppos.Z); // set relative position

                    if (prm.m_targetSpace != m_targetSpace)
                    {
                        if (prm.m_targetSpace != IntPtr.Zero)
                        {
                            _parent_scene.waitForSpaceUnlock(prm.m_targetSpace);
                            if (d.SpaceQuery(prm.m_targetSpace, prm.prim_geom))
                                d.SpaceRemove(prm.m_targetSpace, prm.prim_geom);
                        }
                        prm.m_targetSpace = m_targetSpace;
                        d.SpaceAdd(m_targetSpace, prm.prim_geom);
                    }

                    if (m_isSelected || m_disabled)
                    {
                        prm.m_collisionCategories &= ~CollisionCategories.Body;
                        prm.m_collisionFlags &= ~(CollisionCategories.Land | CollisionCategories.Wind);
                        d.GeomDisable(prm.prim_geom);
                    }
                    else
                    {
                        if (m_isphantom && !m_isVolumeDetect)
                        {
                            prm.m_collisionCategories = 0;
                            prm.m_collisionFlags = CollisionCategories.Land;
                        }
                        else
                        {
                            prm.m_collisionCategories |= CollisionCategories.Body;
                            prm.m_collisionFlags |= (CollisionCategories.Land | CollisionCategories.Wind);
                        }
                        d.GeomEnable(prm.prim_geom);
                    }

                    if (prm.m_NoColide)
                    {
                        d.GeomSetCategoryBits(prm.prim_geom, 0);
                        d.GeomSetCollideBits(prm.prim_geom, (int)CollisionCategories.Land);
                        d.GeomEnable(prm.prim_geom);
                    }
                    else
                    {
                        d.GeomSetCategoryBits(prm.prim_geom, (int)prm.m_collisionCategories);
                        d.GeomSetCollideBits(prm.prim_geom, (int)prm.m_collisionFlags);
                    }
                    prm.m_collisionscore = 0;

                    if(!m_disabled)
                        prm.m_disabled = false;

                    _parent_scene.addActivePrim(prm);
                }
            }

            // The body doesn't already have a finite rotation mode set here
            if ((!m_angularlock.ApproxEquals(Vector3.One, 0.0f)) && _parent == null)
            {
                createAMotor(m_angularlock);
            }

            if (m_isSelected || m_disabled)
            {
                m_collisionCategories &= ~CollisionCategories.Body;
                m_collisionFlags &= ~(CollisionCategories.Land | CollisionCategories.Wind);

                d.GeomDisable(prim_geom);
                d.BodyDisable(Body);
            }
            else
            {
                if (m_isphantom && !m_isVolumeDetect)
                {
                    m_collisionCategories = 0;
                    m_collisionFlags = CollisionCategories.Land;
                }
                else
                {
                    m_collisionCategories |= CollisionCategories.Body;
                    m_collisionFlags |= (CollisionCategories.Land | CollisionCategories.Wind);
                }

                d.BodySetAngularVel(Body, m_rotationalVelocity.X, m_rotationalVelocity.Y, m_rotationalVelocity.Z);
                d.BodySetLinearVel(Body, _velocity.X, _velocity.Y, _velocity.Z);
            }

            if (m_NoColide)
            {
                d.GeomSetCategoryBits(prim_geom, 0);
                d.GeomSetCollideBits(prim_geom, (int)CollisionCategories.Land);
            }
            else
            {
                d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
            }

            m_collisionscore = 0;

            m_softcolide = true;
            _parent_scene.addActivePrim(this);
            _parent_scene.addActiveGroups(this);
        }

        private void DestroyBody()
        {
            if (Body != IntPtr.Zero)
            {
                _parent_scene.remActivePrim(this);
                m_collisionCategories &= ~CollisionCategories.Body;
                m_collisionFlags &= ~(CollisionCategories.Wind | CollisionCategories.Land);
                if (prim_geom != IntPtr.Zero)
                {
                    if (m_NoColide)
                    {
                        d.GeomSetCategoryBits(prim_geom, 0);
                        d.GeomSetCollideBits(prim_geom, 0);
                    }
                    else
                    {
                        d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                        d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
                    }
                    UpdateDataFromGeom();
                    d.GeomSetBody(prim_geom, IntPtr.Zero);
                    SetInStaticSpace(this);
                }

                if (!childPrim)
                {
                    lock (childrenPrim)
                    {
                        foreach (OdePrim prm in childrenPrim)
                        {
                            _parent_scene.remActivePrim(prm);
                            prm.m_collisionCategories = m_collisionCategories;
                            prm.m_collisionFlags = m_collisionFlags;
                            if (prm.prim_geom != IntPtr.Zero)
                            {
                                if (prm.m_NoColide)
                                {
                                    d.GeomSetCategoryBits(prm.prim_geom, 0);
                                    d.GeomSetCollideBits(prm.prim_geom, 0);
                                }
                                else
                                {
                                    d.GeomSetCategoryBits(prm.prim_geom, (int)m_collisionCategories);
                                    d.GeomSetCollideBits(prm.prim_geom, (int)m_collisionFlags);
                                }
                                prm.UpdateDataFromGeom();
                                SetInStaticSpace(prm);
                            }
                            prm.Body = IntPtr.Zero;
                            prm._mass = prm.primMass;
                            prm.m_collisionscore = 0;
                        }
                    }
                    if (Amotor != IntPtr.Zero)
                    {
                        d.JointDestroy(Amotor);
                        Amotor = IntPtr.Zero;
                    }
                    _parent_scene.remActiveGroup(this);
                    d.BodyDestroy(Body);
                }
                Body = IntPtr.Zero;
            }
            _mass = primMass;
            m_collisionscore = 0;
        }

        #region Mass Calculation

        private float CalculatePrimVolume()
        {
            float volume = _size.X * _size.Y * _size.Z; // default
            float tmp;

            float hollowAmount = (float)_pbs.ProfileHollow * 2.0e-5f;
            float hollowVolume = hollowAmount * hollowAmount;

            switch (_pbs.ProfileShape)
            {
                case ProfileShape.Square:
                    // default box

                    if (_pbs.PathCurve == (byte)Extrusion.Straight)
                    {
                        if (hollowAmount > 0.0)
                        {
                            switch (_pbs.HollowShape)
                            {
                                case HollowShape.Square:
                                case HollowShape.Same:
                                    break;

                                case HollowShape.Circle:

                                    hollowVolume *= 0.78539816339f;
                                    break;

                                case HollowShape.Triangle:

                                    hollowVolume *= (0.5f * .5f);
                                    break;

                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }

                    else if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                        //a tube 

                        volume *= 0.78539816339e-2f * (float)(200 - _pbs.PathScaleX);
                        tmp = 1.0f - 2.0e-2f * (float)(200 - _pbs.PathScaleY);
                        volume -= volume * tmp * tmp;

                        if (hollowAmount > 0.0)
                        {
                            hollowVolume *= hollowAmount;

                            switch (_pbs.HollowShape)
                            {
                                case HollowShape.Square:
                                case HollowShape.Same:
                                    break;

                                case HollowShape.Circle:
                                    hollowVolume *= 0.78539816339f;
                                    break;

                                case HollowShape.Triangle:
                                    hollowVolume *= 0.5f * 0.5f;
                                    break;
                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }

                    break;

                case ProfileShape.Circle:

                    if (_pbs.PathCurve == (byte)Extrusion.Straight)
                    {
                        volume *= 0.78539816339f; // elipse base

                        if (hollowAmount > 0.0)
                        {
                            switch (_pbs.HollowShape)
                            {
                                case HollowShape.Same:
                                case HollowShape.Circle:
                                    break;

                                case HollowShape.Square:
                                    hollowVolume *= 0.5f * 2.5984480504799f;
                                    break;

                                case HollowShape.Triangle:
                                    hollowVolume *= .5f * 1.27323954473516f;
                                    break;

                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }

                    else if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                        volume *= 0.61685027506808491367715568749226e-2f * (float)(200 - _pbs.PathScaleX);
                        tmp = 1.0f - .02f * (float)(200 - _pbs.PathScaleY);
                        volume *= (1.0f - tmp * tmp);

                        if (hollowAmount > 0.0)
                        {

                            // calculate the hollow volume by it's shape compared to the prim shape
                            hollowVolume *= hollowAmount;

                            switch (_pbs.HollowShape)
                            {
                                case HollowShape.Same:
                                case HollowShape.Circle:
                                    break;

                                case HollowShape.Square:
                                    hollowVolume *= 0.5f * 2.5984480504799f;
                                    break;

                                case HollowShape.Triangle:
                                    hollowVolume *= .5f * 1.27323954473516f;
                                    break;

                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }
                    break;

                case ProfileShape.HalfCircle:
                    if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                        volume *= 0.52359877559829887307710723054658f;
                    }
                    break;

                case ProfileShape.EquilateralTriangle:

                    if (_pbs.PathCurve == (byte)Extrusion.Straight)
                    {
                        volume *= 0.32475953f;

                        if (hollowAmount > 0.0)
                        {

                            // calculate the hollow volume by it's shape compared to the prim shape
                            switch (_pbs.HollowShape)
                            {
                                case HollowShape.Same:
                                case HollowShape.Triangle:
                                    hollowVolume *= .25f;
                                    break;

                                case HollowShape.Square:
                                    hollowVolume *= 0.499849f * 3.07920140172638f;
                                    break;

                                case HollowShape.Circle:
                                    // Hollow shape is a perfect cyllinder in respect to the cube's scale
                                    // Cyllinder hollow volume calculation

                                    hollowVolume *= 0.1963495f * 3.07920140172638f;
                                    break;

                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }
                    else if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                        volume *= 0.32475953f;
                        volume *= 0.01f * (float)(200 - _pbs.PathScaleX);
                        tmp = 1.0f - .02f * (float)(200 - _pbs.PathScaleY);
                        volume *= (1.0f - tmp * tmp);

                        if (hollowAmount > 0.0)
                        {

                            hollowVolume *= hollowAmount;

                            switch (_pbs.HollowShape)
                            {
                                case HollowShape.Same:
                                case HollowShape.Triangle:
                                    hollowVolume *= .25f;
                                    break;

                                case HollowShape.Square:
                                    hollowVolume *= 0.499849f * 3.07920140172638f;
                                    break;

                                case HollowShape.Circle:

                                    hollowVolume *= 0.1963495f * 3.07920140172638f;
                                    break;

                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }
                    break;

                default:
                    break;
            }

            float taperX1;
            float taperY1;
            float taperX;
            float taperY;
            float pathBegin;
            float pathEnd;
            float profileBegin;
            float profileEnd;

            if (_pbs.PathCurve == (byte)Extrusion.Straight || _pbs.PathCurve == (byte)Extrusion.Flexible)
            {
                taperX1 = _pbs.PathScaleX * 0.01f;
                if (taperX1 > 1.0f)
                    taperX1 = 2.0f - taperX1;
                taperX = 1.0f - taperX1;

                taperY1 = _pbs.PathScaleY * 0.01f;
                if (taperY1 > 1.0f)
                    taperY1 = 2.0f - taperY1;
                taperY = 1.0f - taperY1;
            }
            else
            {
                taperX = _pbs.PathTaperX * 0.01f;
                if (taperX < 0.0f)
                    taperX = -taperX;
                taperX1 = 1.0f - taperX;

                taperY = _pbs.PathTaperY * 0.01f;
                if (taperY < 0.0f)
                    taperY = -taperY;
                taperY1 = 1.0f - taperY;
            }

            volume *= (taperX1 * taperY1 + 0.5f * (taperX1 * taperY + taperX * taperY1) + 0.3333333333f * taperX * taperY);

            pathBegin = (float)_pbs.PathBegin * 2.0e-5f;
            pathEnd = 1.0f - (float)_pbs.PathEnd * 2.0e-5f;
            volume *= (pathEnd - pathBegin);

            // this is crude aproximation
            profileBegin = (float)_pbs.ProfileBegin * 2.0e-5f;
            profileEnd = 1.0f - (float)_pbs.ProfileEnd * 2.0e-5f;
            volume *= (profileEnd - profileBegin);

            return volume;
        }


        private void CalcPrimBodyData()
        {
            float volume;

            if (prim_geom == IntPtr.Zero)
            {
                // Ubit let's have a initial basic OOB
                primOOBsize.X = _size.X;
                primOOBsize.Y = _size.Y;
                primOOBsize.Z = _size.Z;
                primOOBoffset = Vector3.Zero;
            }
            else
            {
                d.AABB AABB;
                d.GeomGetAABB(prim_geom, out AABB); // get the AABB from engine geom

                primOOBsize.X = (AABB.MaxX - AABB.MinX);
                primOOBsize.Y = (AABB.MaxY - AABB.MinY);
                primOOBsize.Z = (AABB.MaxZ - AABB.MinZ);
                if (!hasOOBoffsetFromMesh)
                {
                    primOOBoffset.X = (AABB.MaxX + AABB.MinX) * 0.5f;
                    primOOBoffset.Y = (AABB.MaxY + AABB.MinY) * 0.5f;
                    primOOBoffset.Z = (AABB.MaxZ + AABB.MinZ) * 0.5f;
                }
            }

            // also its own inertia and mass
            // keep using basic shape mass for now
            volume = CalculatePrimVolume();

            primMass = m_density * volume;

            if (primMass <= 0)
                primMass = 0.0001f;//ckrinke: Mass must be greater then zero.
            if (primMass > _parent_scene.maximumMassObject)
                primMass = _parent_scene.maximumMassObject;

            _mass = primMass; // just in case

            d.MassSetBoxTotal(out primdMass, primMass, primOOBsize.X, primOOBsize.Y, primOOBsize.Z);

            d.MassTranslate(ref primdMass,
                                primOOBoffset.X,
                                primOOBoffset.Y,
                                primOOBoffset.Z);

            primOOBsize *= 0.5f; // let obb size be a corner coords
            primOOBradiusSQ = primOOBsize.LengthSquared();
        }


        #endregion


        /// <summary>
        /// Add a child prim to this parent prim.
        /// </summary>
        /// <param name="prim">Child prim</param>
        // I'm the parent
        // prim is the child
        public void ParentPrim(OdePrim prim)
        {
            //Console.WriteLine("ParentPrim  " + m_primName);
            if (this.m_localID != prim.m_localID)
            {
                DestroyBody();  // for now we need to rebuil entire object on link change

                lock (childrenPrim)
                {
                    // adopt the prim
                    if (!childrenPrim.Contains(prim))
                        childrenPrim.Add(prim);

                    // see if this prim has kids and adopt them also
                    // should not happen for now
                    foreach (OdePrim prm in prim.childrenPrim)
                    {
                        if (!childrenPrim.Contains(prm))
                        {
                            if (prm.Body != IntPtr.Zero)
                            {
                                if (prm.prim_geom != IntPtr.Zero)
                                    d.GeomSetBody(prm.prim_geom, IntPtr.Zero);
                                if (prm.Body != prim.Body)
                                    prm.DestroyBody(); // don't loose bodies around
                                prm.Body = IntPtr.Zero;
                            }

                            childrenPrim.Add(prm);
                            prm._parent = this;
                        }
                    }
                }
                //Remove old children from the prim
                prim.childrenPrim.Clear();

                if (prim.Body != IntPtr.Zero)
                {
                    if (prim.prim_geom != IntPtr.Zero)
                        d.GeomSetBody(prim.prim_geom, IntPtr.Zero);
                    prim.DestroyBody(); // don't loose bodies around
                    prim.Body = IntPtr.Zero;
                }

                prim.childPrim = true;
                prim._parent = this;

                MakeBody(); // full nasty reconstruction
            }
        }

        private void UpdateChildsfromgeom()
        {
            if (childrenPrim.Count > 0)
            {
                foreach (OdePrim prm in childrenPrim)
                    prm.UpdateDataFromGeom();
            }
        }

        private void UpdateDataFromGeom()
        {
            if (prim_geom != IntPtr.Zero)
            {
                d.Vector3 lpos;
                d.GeomCopyPosition(prim_geom, out lpos);
                _position.X = lpos.X;
                _position.Y = lpos.Y;
                _position.Z = lpos.Z;
                d.Quaternion qtmp = new d.Quaternion { };
                d.GeomCopyQuaternion(prim_geom, out qtmp);
                _orientation.W = qtmp.W;
                _orientation.X = qtmp.X;
                _orientation.Y = qtmp.Y;
                _orientation.Z = qtmp.Z;
            }
        }

        private void ChildDelink(OdePrim odePrim, bool remakebodies)
        {
            // Okay, we have a delinked child.. destroy all body and remake
            if (odePrim != this && !childrenPrim.Contains(odePrim))
                return;

            DestroyBody();

            if (odePrim == this) // delinking the root prim
            {
                OdePrim newroot = null;
                lock (childrenPrim)
                {
                    if (childrenPrim.Count > 0)
                    {
                        newroot = childrenPrim[0];
                        childrenPrim.RemoveAt(0);
                        foreach (OdePrim prm in childrenPrim)
                        {
                            newroot.childrenPrim.Add(prm);
                        }
                        childrenPrim.Clear();
                    }
                    if (newroot != null)
                    {
                        newroot.childPrim = false;
                        newroot._parent = null;
                        if (remakebodies)
                            newroot.MakeBody();
                    }
                }
            }

            else
            {
                lock (childrenPrim)
                {
                    childrenPrim.Remove(odePrim);
                    odePrim.childPrim = false;
                    odePrim._parent = null;
                    //                    odePrim.UpdateDataFromGeom();
                    if (remakebodies)
                        odePrim.MakeBody();
                }
            }
            if (remakebodies)
                MakeBody();
        }

        protected void ChildRemove(OdePrim odePrim, bool reMakeBody)
        {
            // Okay, we have a delinked child.. destroy all body and remake
            if (odePrim != this && !childrenPrim.Contains(odePrim))
                return;

            DestroyBody();

            if (odePrim == this)
            {
                OdePrim newroot = null;
                lock (childrenPrim)
                {
                    if (childrenPrim.Count > 0)
                    {
                        newroot = childrenPrim[0];
                        childrenPrim.RemoveAt(0);
                        foreach (OdePrim prm in childrenPrim)
                        {
                            newroot.childrenPrim.Add(prm);
                        }
                        childrenPrim.Clear();
                    }
                    if (newroot != null)
                    {
                        newroot.childPrim = false;
                        newroot._parent = null;
                        newroot.MakeBody();
                    }
                }
                if (reMakeBody)
                    MakeBody();
                return;
            }
            else
            {
                lock (childrenPrim)
                {
                    childrenPrim.Remove(odePrim);
                    odePrim.childPrim = false;
                    odePrim._parent = null;
                    if (reMakeBody)
                        odePrim.MakeBody();
                }
            }
            MakeBody();
        }

        #region changes

        private void changeadd()
        {
            CreateGeom();

            if (prim_geom != IntPtr.Zero)
            {
                d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
                d.Quaternion myrot = new d.Quaternion();
                myrot.X = _orientation.X;
                myrot.Y = _orientation.Y;
                myrot.Z = _orientation.Z;
                myrot.W = _orientation.W;
                d.GeomSetQuaternion(prim_geom, ref myrot);

                if (!m_isphysical)
                    SetInStaticSpace(this);
            }

            if (m_isphysical && Body == IntPtr.Zero)
            {
                MakeBody();
            }
        }

        private void changeAngularLock(Vector3 newLock)
        {
            // do we have a Physical object?
            if (Body != IntPtr.Zero)
            {
                //Check that we have a Parent
                //If we have a parent then we're not authorative here
                if (_parent == null)
                {
                    if (!newLock.ApproxEquals(Vector3.One, 0f))
                    {
                        createAMotor(newLock);
                    }
                    else
                    {
                        if (Amotor != IntPtr.Zero)
                        {
                            d.JointDestroy(Amotor);
                            Amotor = IntPtr.Zero;
                        }
                    }
                }
            }
            // Store this for later in case we get turned into a separate body
            m_angularlock = newLock;
        }

        private void changeLink(OdePrim NewParent)
        {
            if (_parent == null && NewParent != null)
            {
                NewParent.ParentPrim(this);
            }
            else if (_parent != null)
            {
                if (_parent is OdePrim)
                {
                    if (NewParent != _parent)
                    {
                        (_parent as OdePrim).ChildDelink(this, false); // for now...
                        childPrim = false;

                        if (NewParent != null)
                        {
                            NewParent.ParentPrim(this);
                        }
                    }
                }
            }
            _parent = NewParent;
        }


        private void Stop()
        {
            if (!childPrim)
            {
                m_force = Vector3.Zero;
                m_forceacc = Vector3.Zero;
                m_angularForceacc = Vector3.Zero;
                _torque = Vector3.Zero;
                _velocity = Vector3.Zero;
                _acceleration = Vector3.Zero;
                m_rotationalVelocity = Vector3.Zero;
                _target_velocity = Vector3.Zero;
                if (m_vehicle != null && m_vehicle.Type != Vehicle.TYPE_NONE)
                    m_vehicle.Stop();
            }

            if (Body != IntPtr.Zero)
            {
                d.BodySetForce(Body, 0f, 0f, 0f);
                d.BodySetTorque(Body, 0f, 0f, 0f);
                d.BodySetLinearVel(Body, 0f, 0f, 0f);
                d.BodySetAngularVel(Body, 0f, 0f, 0f);
            }
        }


        private void changePhantomStatus(bool newval)
        {
            m_isphantom = newval;

            if (m_isSelected)
            {
                m_collisionCategories = CollisionCategories.Selected;
                m_collisionFlags = (CollisionCategories.Sensor | CollisionCategories.Space);
            }
            else
            {
                if (m_isphantom && !m_isVolumeDetect)
                {
                    m_collisionCategories = 0;
                    if (m_isphysical)
                        m_collisionFlags = CollisionCategories.Land;
                    else
                        m_collisionFlags = 0; // should never happen
                }

                else
                {
                    m_collisionCategories = CollisionCategories.Geom;
                    if (m_isphysical)
                        m_collisionCategories |= CollisionCategories.Body;

                    m_collisionFlags = m_default_collisionFlags | CollisionCategories.Land;

                    if (m_collidesWater)
                        m_collisionFlags |= CollisionCategories.Water;
                }
            }

            if (!childPrim)
            {
                foreach (OdePrim prm in childrenPrim)
                {
                    prm.m_collisionCategories = m_collisionCategories;
                    prm.m_collisionFlags = m_collisionFlags;

                    if (!prm.m_disabled && prm.prim_geom != IntPtr.Zero)
                    {
                        if (prm.m_NoColide)
                        {
                            d.GeomSetCategoryBits(prm.prim_geom, 0);
                            if (m_isphysical)
                                d.GeomSetCollideBits(prm.prim_geom, (int)CollisionCategories.Land);
                            else
                                d.GeomSetCollideBits(prm.prim_geom, 0);
                        }
                        else
                        {
                            d.GeomSetCategoryBits(prm.prim_geom, (int)m_collisionCategories);
                            d.GeomSetCollideBits(prm.prim_geom, (int)m_collisionFlags);
                        }
                        if(!m_isSelected)
                            d.GeomEnable(prm.prim_geom);
                    }
                }
            }

            if (!m_disabled && prim_geom != IntPtr.Zero)
            {
                if (m_NoColide)
                {
                    d.GeomSetCategoryBits(prim_geom, 0);
                    if (m_isphysical)
                        d.GeomSetCollideBits(prim_geom, (int)CollisionCategories.Land);
                    else
                        d.GeomSetCollideBits(prim_geom, 0);
                }
                else
                {
                    d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                    d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
                }
                if(!m_isSelected)
                    d.GeomEnable(prim_geom);
            }
        }

        private void changeSelectedStatus(bool newval)
        {
            if (m_lastdoneSelected == newval)
                return;

            m_lastdoneSelected = newval;
            DoSelectedStatus(newval);
        }

        private void CheckDelaySelect()
        {
            if (m_delaySelect)
            {
                DoSelectedStatus(m_isSelected);
            }
        }

        private void DoSelectedStatus(bool newval)
        {
            m_isSelected = newval;
            Stop();

            if (newval)
            {
                if (!childPrim && Body != IntPtr.Zero)
                    d.BodyDisable(Body);

                if (m_delaySelect || m_isphysical)
                {
                    m_collisionCategories = CollisionCategories.Selected;
                    m_collisionFlags = (CollisionCategories.Sensor | CollisionCategories.Space);

                    if (!childPrim)
                    {
                        foreach (OdePrim prm in childrenPrim)
                        {
                            prm.m_collisionCategories = m_collisionCategories;
                            prm.m_collisionFlags = m_collisionFlags;

                            if (prm.prim_geom != null)
                            {

                                if (prm.m_NoColide)
                                {
                                    d.GeomSetCategoryBits(prm.prim_geom, 0);
                                    d.GeomSetCollideBits(prm.prim_geom, 0);
                                }
                                else
                                {
                                    d.GeomSetCategoryBits(prm.prim_geom, (int)m_collisionCategories);
                                    d.GeomSetCollideBits(prm.prim_geom, (int)m_collisionFlags);
                                }
                                d.GeomDisable(prm.prim_geom);
                            }
                            prm.m_delaySelect = false;
                        }
                    }

                    if (prim_geom != null)
                    {
                        if (m_NoColide)
                        {
                            d.GeomSetCategoryBits(prim_geom, 0);
                            d.GeomSetCollideBits(prim_geom, 0);
                        }
                        else
                        {
                            d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                            d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
                        }
                        d.GeomDisable(prim_geom);
                    }

                    m_delaySelect = false;
                }
                else if(!m_isphysical)
                {
                    m_delaySelect = true;
                }
            }
            else
            {
                if (!childPrim && Body != IntPtr.Zero && !m_disabled)
                    d.BodyEnable(Body);

                if (m_isphantom && !m_isVolumeDetect)
                {
                    m_collisionCategories = 0;
                    if(m_isphysical)
                        m_collisionFlags = CollisionCategories.Land;
                    else
                        m_collisionFlags = 0;
                }
                else
                {
                    m_collisionCategories = CollisionCategories.Geom;
                    if (m_isphysical)
                        m_collisionCategories |= CollisionCategories.Body;

                    m_collisionFlags = m_default_collisionFlags | CollisionCategories.Land;

                    if (m_collidesWater)
                        m_collisionFlags |= CollisionCategories.Water;
                }

                if (!childPrim)
                {
                    foreach (OdePrim prm in childrenPrim)
                    {
                        prm.m_collisionCategories = m_collisionCategories;
                        prm.m_collisionFlags = m_collisionFlags;

                        if (!prm.m_disabled && prm.prim_geom != IntPtr.Zero)
                        {
                            if (prm.m_NoColide)
                            {
                                d.GeomSetCategoryBits(prm.prim_geom, 0);
                                if (m_isphysical)
                                    d.GeomSetCollideBits(prm.prim_geom, (int)CollisionCategories.Land);
                                else
                                    d.GeomSetCollideBits(prm.prim_geom, 0);
                            }
                            else
                            {
                                d.GeomSetCategoryBits(prm.prim_geom, (int)m_collisionCategories);
                                d.GeomSetCollideBits(prm.prim_geom, (int)m_collisionFlags);
                            }
                            d.GeomEnable(prm.prim_geom);
                        }
                        prm.m_delaySelect = false;
                        prm.m_softcolide = true;
                    }
                }

                if (!m_disabled && prim_geom != IntPtr.Zero)
                {
                    if (m_NoColide)
                    {
                        d.GeomSetCategoryBits(prim_geom, 0);
                        if (m_isphysical)
                            d.GeomSetCollideBits(prim_geom, (int)CollisionCategories.Land);
                        else
                            d.GeomSetCollideBits(prim_geom, 0);
                    }
                    else
                    {
                        d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                        d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
                    }
                    d.GeomEnable(prim_geom);
                }

                m_delaySelect = false;
                m_softcolide = true;
            }

            resetCollisionAccounting();
        }

        private void changePosition(Vector3 newPos)
        {
            CheckDelaySelect();
            if (m_isphysical)
            {
                if (childPrim)  // inertia is messed, must rebuild
                {
                    if (m_building)
                    {
                        _position = newPos;
                    }
                }
                else
                {
                    if (_position != newPos)
                    {
                        d.GeomSetPosition(prim_geom, newPos.X, newPos.Y, newPos.Z);
                        _position = newPos;
                    }
                    if (Body != IntPtr.Zero && !d.BodyIsEnabled(Body))
                        d.BodyEnable(Body);
                }
            }
            else
            {
                if (prim_geom != IntPtr.Zero)
                {
                    if (newPos != _position)
                    {
                        d.GeomSetPosition(prim_geom, newPos.X, newPos.Y, newPos.Z);
                        _position = newPos;

                        m_targetSpace = _parent_scene.MoveGeomToStaticSpace(prim_geom, _position, m_targetSpace);
                    }
                }
            }
            givefakepos--;
            if (givefakepos < 0)
                givefakepos = 0;
            //            changeSelectedStatus();
            m_softcolide = true;
            resetCollisionAccounting();
        }

        private void changeOrientation(Quaternion newOri)
        {
            CheckDelaySelect();
            if (m_isphysical)
            {
                if (childPrim)  // inertia is messed, must rebuild
                {
                    if (m_building)
                    {
                        _orientation = newOri;
                    }
                }
                else
                {
                    if (newOri != _orientation)
                    {
                        d.Quaternion myrot = new d.Quaternion();
                        myrot.X = newOri.X;
                        myrot.Y = newOri.Y;
                        myrot.Z = newOri.Z;
                        myrot.W = newOri.W;
                        d.GeomSetQuaternion(prim_geom, ref myrot);
                        _orientation = newOri;
                        if (Body != IntPtr.Zero && !m_angularlock.ApproxEquals(Vector3.One, 0f))
                            createAMotor(m_angularlock);
                    }
                    if (Body != IntPtr.Zero && !d.BodyIsEnabled(Body))
                        d.BodyEnable(Body);
                }
            }
            else
            {
                if (prim_geom != IntPtr.Zero)
                {
                    if (newOri != _orientation)
                    {
                        d.Quaternion myrot = new d.Quaternion();
                        myrot.X = newOri.X;
                        myrot.Y = newOri.Y;
                        myrot.Z = newOri.Z;
                        myrot.W = newOri.W;
                        d.GeomSetQuaternion(prim_geom, ref myrot);
                        _orientation = newOri;
                    }
                }
            }
            givefakeori--;
            if (givefakeori < 0)
                givefakeori = 0;
            m_softcolide = true;
            resetCollisionAccounting();
        }

        private void changePositionAndOrientation(Vector3 newPos, Quaternion newOri)
        {
            CheckDelaySelect();
            if (m_isphysical)
            {
                if (childPrim && m_building)  // inertia is messed, must rebuild
                {
                    _position = newPos;
                    _orientation = newOri;
                }
                else
                {
                    if (newOri != _orientation)
                    {
                        d.Quaternion myrot = new d.Quaternion();
                        myrot.X = newOri.X;
                        myrot.Y = newOri.Y;
                        myrot.Z = newOri.Z;
                        myrot.W = newOri.W;
                        d.GeomSetQuaternion(prim_geom, ref myrot);
                        _orientation = newOri;
                        if (Body != IntPtr.Zero && !m_angularlock.ApproxEquals(Vector3.One, 0f))
                            createAMotor(m_angularlock);
                    }
                    if (_position != newPos)
                    {
                        d.GeomSetPosition(prim_geom, newPos.X, newPos.Y, newPos.Z);
                        _position = newPos;
                    }
                    if (Body != IntPtr.Zero && !d.BodyIsEnabled(Body))
                        d.BodyEnable(Body);
                }
            }
            else
            {
                // string primScenAvatarIn = _parent_scene.whichspaceamIin(_position);
                // int[] arrayitem = _parent_scene.calculateSpaceArrayItemFromPos(_position);

                if (prim_geom != IntPtr.Zero)
                {
                    if (newOri != _orientation)
                    {
                        d.Quaternion myrot = new d.Quaternion();
                        myrot.X = newOri.X;
                        myrot.Y = newOri.Y;
                        myrot.Z = newOri.Z;
                        myrot.W = newOri.W;
                        d.GeomSetQuaternion(prim_geom, ref myrot);
                        _orientation = newOri;
                    }

                    if (newPos != _position)
                    {
                        d.GeomSetPosition(prim_geom, newPos.X, newPos.Y, newPos.Z);
                        _position = newPos;

                        m_targetSpace = _parent_scene.MoveGeomToStaticSpace(prim_geom, _position, m_targetSpace);
                    }
                }
            }
            givefakepos--;
            if (givefakepos < 0)
                givefakepos = 0;
            givefakeori--;
            if (givefakeori < 0)
                givefakeori = 0;

            m_softcolide = true;
            resetCollisionAccounting();
        }


        private void changeDisable(bool disable)
        {
            if (disable)
            {
                if (!m_disabled)
                    disableBodySoft();
            }
            else
            {
                if (m_disabled)
                    enableBodySoft();
            }
        }

        private void changePhysicsStatus(bool NewStatus)
        {
            CheckDelaySelect();

            m_isphysical = NewStatus;

            if (!childPrim)
            {
                if (NewStatus)
                {
                    if (Body == IntPtr.Zero)
                        MakeBody();
                }
                else
                {
                    if (Body != IntPtr.Zero)
                    {
                        DestroyBody();
                    }
                    Stop();
                }
            }

            resetCollisionAccounting();
        }

        private void changeprimsizeshape()
        {
            CheckDelaySelect();

            OdePrim parent = (OdePrim)_parent;

            bool chp = childPrim;

            if (chp)
            {
                if (parent != null)
                {
                    parent.DestroyBody();
                }
            }
            else
            {
                DestroyBody();
            }

            RemoveGeom();

            // we don't need to do space calculation because the client sends a position update also.
            if (_size.X <= 0)
                _size.X = 0.01f;
            if (_size.Y <= 0)
                _size.Y = 0.01f;
            if (_size.Z <= 0)
                _size.Z = 0.01f;
            // Construction of new prim

            CreateGeom();

            if (prim_geom != IntPtr.Zero)
            {
                d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
                d.Quaternion myrot = new d.Quaternion();
                myrot.X = _orientation.X;
                myrot.Y = _orientation.Y;
                myrot.Z = _orientation.Z;
                myrot.W = _orientation.W;
                d.GeomSetQuaternion(prim_geom, ref myrot);
            }

            if (chp)
            {
                if (parent != null)
                {
                    parent.MakeBody();
                }
            }
            else
                MakeBody();

            m_softcolide = true;
            resetCollisionAccounting();
        }

        private void changeSize(Vector3 newSize)
        {
            _size = newSize;
            changeprimsizeshape();
        }

        private void changeShape(PrimitiveBaseShape newShape)
        {
            _pbs = newShape;
            changeprimsizeshape();
        }

        private void changeFloatOnWater(bool newval)
        {
            m_collidesWater = newval;

            if (prim_geom != IntPtr.Zero && !m_isphantom)
            {
                if (m_collidesWater)
                {
                    m_collisionFlags |= CollisionCategories.Water;
                }
                else
                {
                    m_collisionFlags &= ~CollisionCategories.Water;
                }
                d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
            }
        }

        private void changeSetTorque(Vector3 newtorque)
        {
            if (!m_isSelected)
            {
                if (m_isphysical && Body != IntPtr.Zero)
                {
                    if (m_disabled)
                        enableBodySoft();
                    else if (!d.BodyIsEnabled(Body))
                        d.BodyEnable(Body);

                }
                _torque = newtorque;
            }
        }

        private void changeForce(Vector3 force)
        {
            m_force = force;
            if (Body != IntPtr.Zero && !d.BodyIsEnabled(Body))
                d.BodyEnable(Body);
        }

        private void changeAddForce(Vector3 force)
        {
            m_forceacc += force;
            if (!m_isSelected)
            {
                lock (this)
                {
                    //m_log.Info("[PHYSICS]: dequeing forcelist");
                    if (m_isphysical && Body != IntPtr.Zero)
                    {
                        if (m_disabled)
                            enableBodySoft();
                        else if (!d.BodyIsEnabled(Body))
                            d.BodyEnable(Body);
                    }
                }

                m_collisionscore = 0;
            }
        }

        private void changeAddAngularForce(Vector3 aforce)
        {
            m_angularForceacc += aforce;
            if (!m_isSelected)
            {
                lock (this)
                {
                    if (m_isphysical && Body != IntPtr.Zero)
                    {
                        if (m_disabled)
                            enableBodySoft();
                        else if (!d.BodyIsEnabled(Body))
                            d.BodyEnable(Body);
                    }
                }
                m_collisionscore = 0;
            }
        }

        private void changevelocity(Vector3 newVel)
        {
            if (!m_isSelected)
            {
                if (Body != IntPtr.Zero)
                {
                    if (m_disabled)
                        enableBodySoft();
                    else if (!d.BodyIsEnabled(Body))
                        d.BodyEnable(Body);

                    d.BodySetLinearVel(Body, newVel.X, newVel.Y, newVel.Z);
                }
                //resetCollisionAccounting();           
            }
            _velocity = newVel;
        }

        private void changeVolumedetetion(bool newVolDtc)
        {
            m_isVolumeDetect = newVolDtc;
        }

        protected void changeBuilding(bool newbuilding)
        {
            if ((bool)newbuilding)
            {
                m_building = true;
                if (!childPrim)
                    DestroyBody();
            }
            else
            {
                m_building = false;
                CheckDelaySelect();
                if (!childPrim)
                    MakeBody();
            }
            if (!childPrim && childrenPrim.Count > 0)
            {
                foreach (OdePrim prm in childrenPrim)
                    prm.changeBuilding(m_building); // call directly
            }
        }

       public void changeSetVehicle(VehicleData vdata)
        {
            if (m_vehicle == null)
                m_vehicle = new ODEDynamics(this);
            m_vehicle.DoSetVehicle(vdata);
        }
        private void changeVehicleType(int value)
        {
            if (value == (int)Vehicle.TYPE_NONE)
            {
                if (m_vehicle != null)
                    m_vehicle = null;
            }
            else
            {
                if (m_vehicle == null)
                    m_vehicle = new ODEDynamics(this);

                m_vehicle.ProcessTypeChange((Vehicle)value);
            }
        }

        private void changeVehicleFloatParam(strVehicleFloatParam fp)
        {
            if (m_vehicle == null)
                return;

            m_vehicle.ProcessFloatVehicleParam((Vehicle)fp.param, fp.value);
        }

        private void changeVehicleVectorParam(strVehicleVectorParam vp)
        {
            if (m_vehicle == null)
                return;
            m_vehicle.ProcessVectorVehicleParam((Vehicle)vp.param, vp.value);
        }

        private void changeVehicleRotationParam(strVehicleQuatParam qp)
        {
            if (m_vehicle == null)
                return;
            m_vehicle.ProcessRotationVehicleParam((Vehicle)qp.param, qp.value);
        }

        private void changeVehicleFlags(strVehicleBoolParam bp)
        {
            if (m_vehicle == null)
                return;
            m_vehicle.ProcessVehicleFlags(bp.param, bp.value);
        }

        #endregion

        public void Move()
        {
            if (!childPrim && m_isphysical && Body != IntPtr.Zero &&
                !m_disabled && !m_isSelected && d.BodyIsEnabled(Body) && !m_building && !m_outbounds)
            //                  !m_disabled && !m_isSelected && !m_building && !m_outbounds)
            {
//                if (!d.BodyIsEnabled(Body)) d.BodyEnable(Body); // KF add 161009

                float timestep = _parent_scene.ODE_STEPSIZE;

                // check outside region
                d.Vector3 lpos;
                d.GeomCopyPosition(prim_geom, out lpos); // root position that is seem by rest of simulator

                if (lpos.Z < -100 || lpos.Z > 100000f)
                {
                    m_outbounds = true;

                    lpos.Z = Util.Clip(lpos.Z, -100f, 100000f);
                    _acceleration.X = 0;
                    _acceleration.Y = 0;
                    _acceleration.Z = 0;

                    _velocity.X = 0;
                    _velocity.Y = 0;
                    _velocity.Z = 0;
                    m_rotationalVelocity.X = 0;
                    m_rotationalVelocity.Y = 0;
                    m_rotationalVelocity.Z = 0;

                    d.BodySetLinearVel(Body, 0, 0, 0); // stop it
                    d.BodySetAngularVel(Body, 0, 0, 0); // stop it
                    d.BodySetPosition(Body, lpos.X, lpos.Y, lpos.Z); // put it somewhere 
                    m_lastposition = _position;
                    m_lastorientation = _orientation;

                    base.RequestPhysicsterseUpdate();

                    m_throttleUpdates = false;
                    throttleCounter = 0;
                    _zeroFlag = true;

                    disableBodySoft(); // disable it and colisions
                    base.RaiseOutOfBounds(_position);
                    return;
                }

                if (lpos.X < 0f)
                {
                    _position.X = Util.Clip(lpos.X, -2f, -0.1f);
                    m_outbounds = true;
                }
                else if(lpos.X > _parent_scene.WorldExtents.X)
                {
                    _position.X = Util.Clip(lpos.X, _parent_scene.WorldExtents.X + 0.1f, _parent_scene.WorldExtents.X + 2f);
                    m_outbounds = true;
                }
                if (lpos.Y < 0f)
                {
                    _position.Y = Util.Clip(lpos.Y, -2f, -0.1f);
                    m_outbounds = true;
                }
                else if(lpos.Y > _parent_scene.WorldExtents.Y)
                {
                    _position.Y = Util.Clip(lpos.Y, _parent_scene.WorldExtents.Y + 0.1f, _parent_scene.WorldExtents.Y + 2f);
                    m_outbounds = true;
                }

                if(m_outbounds)
                {
                    m_lastposition = _position;
                    m_lastorientation = _orientation;

                    d.Vector3 dtmp = d.BodyGetAngularVel(Body);
                    m_rotationalVelocity.X = dtmp.X;
                    m_rotationalVelocity.Y = dtmp.Y;
                    m_rotationalVelocity.Z = dtmp.Z;

                    dtmp = d.BodyGetLinearVel(Body);
                    _velocity.X = dtmp.X;
                    _velocity.Y = dtmp.Y;
                    _velocity.Z = dtmp.Z;

                    d.BodySetLinearVel(Body, 0, 0, 0); // stop it
                    d.BodySetAngularVel(Body, 0, 0, 0);
                    d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
                    disableBodySoft(); // stop collisions
                    base.RequestPhysicsterseUpdate();
                    return;
                }


                float fx = 0;
                float fy = 0;
                float fz = 0;

                if (m_vehicle != null && m_vehicle.Type != Vehicle.TYPE_NONE)
                {
                    // 'VEHICLES' are dealt with in ODEDynamics.cs
                    m_vehicle.Step();
                }
                else
                {
                    float m_mass = _mass;

                    //                    fz = 0f;
                    //m_log.Info(m_collisionFlags.ToString());
                    if (m_usePID)
                    {

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

                        d.Vector3 vel = d.BodyGetLinearVel(Body);
                        d.Vector3 pos = d.BodyGetPosition(Body);
                        _target_velocity =
                            new Vector3(
                                (m_PIDTarget.X - pos.X) * ((PID_G - m_PIDTau) * timestep),
                                (m_PIDTarget.Y - pos.Y) * ((PID_G - m_PIDTau) * timestep),
                                (m_PIDTarget.Z - pos.Z) * ((PID_G - m_PIDTau) * timestep)
                                );

                        //  if velocity is zero, use position control; otherwise, velocity control

                        if (_target_velocity.ApproxEquals(Vector3.Zero, 0.1f))
                        {
                            //  keep track of where we stopped.  No more slippin' & slidin'

                            // We only want to deactivate the PID Controller if we think we want to have our surrogate
                            // react to the physics scene by moving it's position.
                            // Avatar to Avatar collisions
                            // Prim to avatar collisions

                            //fx = (_target_velocity.X - vel.X) * (PID_D) + (_zeroPosition.X - pos.X) * (PID_P * 2);
                            //fy = (_target_velocity.Y - vel.Y) * (PID_D) + (_zeroPosition.Y - pos.Y) * (PID_P * 2);
                            //fz = fz + (_target_velocity.Z - vel.Z) * (PID_D) + (_zeroPosition.Z - pos.Z) * PID_P;
                            d.BodySetPosition(Body, m_PIDTarget.X, m_PIDTarget.Y, m_PIDTarget.Z);
                            d.BodySetLinearVel(Body, 0, 0, 0);
                            d.BodyAddForce(Body, 0, 0, fz);
                            return;
                        }
                        else
                        {
                            _zeroFlag = false;

                            // We're flying and colliding with something
                            fx = ((_target_velocity.X) - vel.X) * (PID_D);
                            fy = ((_target_velocity.Y) - vel.Y) * (PID_D);

                            // vec.Z = (_target_velocity.Z - vel.Z) * PID_D + (_zeroPosition.Z - pos.Z) * PID_P;

                            fz = ((_target_velocity.Z - vel.Z) * (PID_D));
                        }
                    }        // end if (m_usePID)

                    // Hover PID Controller needs to be mutually exlusive to MoveTo PID controller
                    else if (m_useHoverPID)
                    {
                        //Console.WriteLine("Hover " +  Name);

                        // If we're using the PID controller, then we have no gravity

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

                        // Where are we, and where are we headed?
                        d.Vector3 pos = d.BodyGetPosition(Body);
                        d.Vector3 vel = d.BodyGetLinearVel(Body);

                        //    Non-Vehicles have a limited set of Hover options.
                        // determine what our target height really is based on HoverType
                        switch (m_PIDHoverType)
                        {
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

                        }     // end switch (m_PIDHoverType)


                        _target_velocity =
                            new Vector3(0.0f, 0.0f,
                                (m_targetHoverHeight - pos.Z) * ((PID_G - m_PIDHoverTau) * timestep)
                                );

                        //  if velocity is zero, use position control; otherwise, velocity control

                        if (_target_velocity.ApproxEquals(Vector3.Zero, 0.1f))
                        {
                            //  keep track of where we stopped.  No more slippin' & slidin'

                            // We only want to deactivate the PID Controller if we think we want to have our surrogate
                            // react to the physics scene by moving it's position.
                            // Avatar to Avatar collisions
                            // Prim to avatar collisions

                            d.BodySetPosition(Body, pos.X, pos.Y, m_targetHoverHeight);
                            d.BodySetLinearVel(Body, vel.X, vel.Y, 0);
                            // ?                        d.BodyAddForce(Body, 0, 0, fz);
                            return;
                        }
                        else
                        {
                            _zeroFlag = false;

                            // We're flying and colliding with something
                            fz = ((_target_velocity.Z - vel.Z) * (PID_D));
                        }
                    }
                    else
                    {
                        float b = (1.0f - m_buoyancy);
                        fx = _parent_scene.gravityx * b;
                        fy = _parent_scene.gravityy * b;
                        fz = _parent_scene.gravityz * b;
                    }

                    fx *= m_mass;
                    fy *= m_mass;
                    fz *= m_mass;

                    // constant force
                    fx += m_force.X;
                    fy += m_force.Y;
                    fz += m_force.Z;

                    fx += m_forceacc.X;
                    fy += m_forceacc.Y;
                    fz += m_forceacc.Z;

                    m_forceacc = Vector3.Zero;

                    //m_log.Info("[OBJPID]: X:" + fx.ToString() + " Y:" + fy.ToString() + " Z:" + fz.ToString());
                    if (fx != 0 || fy != 0 || fz != 0)
                    {
                        d.BodyAddForce(Body, fx, fy, fz);
                        //Console.WriteLine("AddForce " + fx + "," + fy + "," + fz);
                    }

                    Vector3 trq;

                    trq = _torque;
                    trq += m_angularForceacc;
                    m_angularForceacc = Vector3.Zero;
                    if (trq.X != 0 || trq.Y != 0 || trq.Z != 0)
                    {
                        d.BodyAddTorque(Body, trq.X, trq.Y, trq.Z);
                    }

                }
            }
            else
            {    // is not physical, or is not a body or is selected
                //  _zeroPosition = d.BodyGetPosition(Body);
                return;
                //Console.WriteLine("Nothing " +  Name);

            }
        }


        public void UpdatePositionAndVelocity(float simulatedtime)
        {
            //  no lock; called from Simulate() -- if you call this from elsewhere, gotta lock or do Monitor.Enter/Exit!
            if (_parent == null && !m_disabled && !m_building && !m_outbounds)
            {
                if (Body != IntPtr.Zero)
                {
                    Vector3 pv = Vector3.Zero;
                    bool lastZeroFlag = _zeroFlag;

                    d.Vector3 lpos;
                    d.GeomCopyPosition(prim_geom, out lpos); // root position that is seem by rest of simulator


                    d.Quaternion ori;
                    d.GeomCopyQuaternion(prim_geom, out ori);
                    d.Vector3 vel = d.BodyGetLinearVel(Body);
                    d.Vector3 rotvel = d.BodyGetAngularVel(Body);

                    if ((Math.Abs(m_lastposition.X - lpos.X) < 0.01)
                        && (Math.Abs(m_lastposition.Y - lpos.Y) < 0.01)
                        && (Math.Abs(m_lastposition.Z - lpos.Z) < 0.01)
                        && (Math.Abs(m_lastorientation.X - ori.X) < 0.0001)
                        && (Math.Abs(m_lastorientation.Y - ori.Y) < 0.0001)
                        && (Math.Abs(m_lastorientation.Z - ori.Z) < 0.0001)
                        )
                    {
                        _zeroFlag = true;
                        //Console.WriteLine("ZFT 2");
                        m_throttleUpdates = false;
                    }
                    else
                    {
                        //m_log.Debug(Math.Abs(m_lastposition.X - l_position.X).ToString());
                        _zeroFlag = false;
                        m_lastUpdateSent = false;
                        //m_throttleUpdates = false;
                    }

                    if (_zeroFlag)
                    {
                        m_lastposition = _position;
                        m_lastorientation = _orientation;

                        _velocity.X = 0.0f;
                        _velocity.Y = 0.0f;
                        _velocity.Z = 0.0f;

                        _acceleration.X = 0;
                        _acceleration.Y = 0;
                        _acceleration.Z = 0;

                        m_rotationalVelocity.X = 0;
                        m_rotationalVelocity.Y = 0;
                        m_rotationalVelocity.Z = 0;
                        if (!m_lastUpdateSent)
                        {
                            m_throttleUpdates = false;
                            throttleCounter = 0;
                            m_rotationalVelocity = pv;

                            base.RequestPhysicsterseUpdate();

                            m_lastUpdateSent = true;
                        }
                    }
                    else
                    {
                        if (lastZeroFlag != _zeroFlag)
                        {
                            base.RequestPhysicsterseUpdate();
                        }

                        m_lastVelocity = _velocity;

                        _position.X = lpos.X;
                        _position.Y = lpos.Y;
                        _position.Z = lpos.Z;

                        _velocity.X = vel.X;
                        _velocity.Y = vel.Y;
                        _velocity.Z = vel.Z;

                        _orientation.X = ori.X;
                        _orientation.Y = ori.Y;
                        _orientation.Z = ori.Z;
                        _orientation.W = ori.W;

                        _acceleration = ((_velocity - m_lastVelocity) / simulatedtime);

                        if (m_rotationalVelocity.ApproxEquals(pv, 0.0001f))
                        {
                            m_rotationalVelocity = pv;
                        }
                        else
                        {
                            m_rotationalVelocity.X = rotvel.X;
                            m_rotationalVelocity.Y = rotvel.Y;
                            m_rotationalVelocity.Z = rotvel.Z;
                        }

                        m_lastUpdateSent = false;
                        if (!m_throttleUpdates || throttleCounter > _parent_scene.geomUpdatesPerThrottledUpdate)
                        {
                            m_lastposition = _position;
                            m_lastorientation = _orientation;
                            base.RequestPhysicsterseUpdate();
                        }
                        else
                        {
                            throttleCounter++;
                        }
                    }
                }
                else if (!m_lastUpdateSent || !_zeroFlag)
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

                    if (!m_lastUpdateSent)
                    {
                        m_throttleUpdates = false;
                        throttleCounter = 0;

                        base.RequestPhysicsterseUpdate();

                        m_lastUpdateSent = true;
                    }
                }
            }
        }

        internal static bool QuaternionIsFinite(Quaternion q)
        {
            if (Single.IsNaN(q.X) || Single.IsInfinity(q.X))
                return false;
            if (Single.IsNaN(q.Y) || Single.IsInfinity(q.Y))
                return false;
            if (Single.IsNaN(q.Z) || Single.IsInfinity(q.Z))
                return false;
            if (Single.IsNaN(q.W) || Single.IsInfinity(q.W))
                return false;
            return true;
        }

        internal static void DMassCopy(ref d.Mass src, ref d.Mass dst)
        {
            dst.c.W = src.c.W;
            dst.c.X = src.c.X;
            dst.c.Y = src.c.Y;
            dst.c.Z = src.c.Z;
            dst.mass = src.mass;
            dst.I.M00 = src.I.M00;
            dst.I.M01 = src.I.M01;
            dst.I.M02 = src.I.M02;
            dst.I.M10 = src.I.M10;
            dst.I.M11 = src.I.M11;
            dst.I.M12 = src.I.M12;
            dst.I.M20 = src.I.M20;
            dst.I.M21 = src.I.M21;
            dst.I.M22 = src.I.M22;
        }

        private static void DMassDup(ref d.Mass src, out d.Mass dst)
        {
            dst = new d.Mass { };

            dst.c.W = src.c.W;
            dst.c.X = src.c.X;
            dst.c.Y = src.c.Y;
            dst.c.Z = src.c.Z;
            dst.mass = src.mass;
            dst.I.M00 = src.I.M00;
            dst.I.M01 = src.I.M01;
            dst.I.M02 = src.I.M02;
            dst.I.M10 = src.I.M10;
            dst.I.M11 = src.I.M11;
            dst.I.M12 = src.I.M12;
            dst.I.M20 = src.I.M20;
            dst.I.M21 = src.I.M21;
            dst.I.M22 = src.I.M22;
        }
        private void donullchange()
        {
        }

        public bool DoAChange(changes what, object arg)
        {
            if (prim_geom == IntPtr.Zero && what != changes.Add && what != changes.Remove)
            {
                return false;
            }

            // nasty switch
            switch (what)
            {
                case changes.Add:
                    changeadd();
                    break;
                case changes.Remove:
                    //If its being removed, we don't want to rebuild the physical rep at all, so ignore this stuff...
                    //When we return true, it destroys all of the prims in the linkset anyway
                    if (_parent != null)
                    {
                        OdePrim parent = (OdePrim)_parent;
                        parent.ChildRemove(this, false);
                    }
                    else
                        ChildRemove(this, false);

                    m_vehicle = null;
                    RemoveGeom();
                    m_targetSpace = IntPtr.Zero;
                    if (m_eventsubscription > 0)
                        UnSubscribeEvents();
                    return true;

                case changes.Link:
                    OdePrim tmp = (OdePrim)arg;
                    changeLink(tmp);
                    break;

                case changes.DeLink:
                    changeLink(null);
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
                    changevelocity((Vector3)arg);
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

                case changes.Shape:
                    changeShape((PrimitiveBaseShape)arg);
                    break;

                case changes.CollidesWater:
                    changeFloatOnWater((bool)arg);
                    break;

                case changes.VolumeDtc:
                    changeVolumedetetion((bool)arg);
                    break;

                case changes.Phantom:
                    changePhantomStatus((bool)arg);
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

                case changes.VehicleType:
                    changeVehicleType((int)arg);
                    break;

                case changes.VehicleFlags:
                    changeVehicleFlags((strVehicleBoolParam) arg);
                    break;

                case changes.VehicleFloatParam:
                    changeVehicleFloatParam((strVehicleFloatParam) arg);
                    break;

                case changes.VehicleVectorParam:
                    changeVehicleVectorParam((strVehicleVectorParam) arg);
                    break;

                case changes.VehicleRotationParam:
                    changeVehicleRotationParam((strVehicleQuatParam) arg);
                    break;

                case changes.SetVehicle:
                    changeSetVehicle((VehicleData) arg);
                    break;
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
            _parent_scene.AddChange((PhysicsActor) this, what, arg);
        }


        private struct strVehicleBoolParam
        {
            public int param;
            public bool value;
        }

        private struct strVehicleFloatParam
        {
            public int param;
            public float value;
        }

        private struct strVehicleQuatParam
        {
            public int param;
            public Quaternion value;
        }

        private struct strVehicleVectorParam
        {
            public int param;
            public Vector3 value;
        }
    }
}