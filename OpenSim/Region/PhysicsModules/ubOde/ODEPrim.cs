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
using OpenSim.Region.PhysicsModules.SharedBase;

namespace OpenSim.Region.PhysicsModule.ubOde
{
    public class OdePrim : PhysicsActor
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_isphysical;
        private bool m_fakeisphysical;
        private bool m_isphantom;
        private bool m_fakeisphantom;
        internal bool m_isVolumeDetect; // If true, this prim only detects collisions but doesn't collide actively
        private bool m_fakeisVolumeDetect; // If true, this prim only detects collisions but doesn't collide actively

        protected bool m_building;
        protected bool m_forcePosOrRotation;
        private bool m_iscolliding;

        internal bool m_isSelected;
        private bool m_delaySelect;
        private bool m_lastdoneSelected;
        internal bool m_outbounds;

        private byte m_angularlocks = 0;

        private Quaternion m_lastorientation;
        private Quaternion _orientation;

        private Vector3 _position;
        private Vector3 _velocity;
        private Vector3 m_torque;
        private Vector3 m_lastVelocity;
        private Vector3 m_lastposition;
        private Vector3 m_rotationalVelocity;
        private Vector3 _size;
        private Vector3 _acceleration;
        private IntPtr Amotor;

        private Vector3 m_force;
        private Vector3 m_forceacc;
        private Vector3 m_angularForceacc;

        private float m_invTimeStep;
        private float m_timeStep;

        private Vector3 m_PIDTarget;
        private float m_PIDTau;
        private bool m_usePID;

        private float m_PIDHoverHeight;
        private float m_PIDHoverTau;
        private bool m_useHoverPID;
        private PIDHoverType m_PIDHoverType;
        private float m_targetHoverHeight;
        private float m_groundHeight;
        private float m_waterHeight;
        private float m_buoyancy;                //KF: m_buoyancy should be set by llSetBuoyancy() for non-vehicle. 

        private int body_autodisable_frames;
        public int bodydisablecontrol = 0;
        private float m_gravmod = 1.0f;

        // Default we're a Geometry
        private CollisionCategories m_collisionCategories = (CollisionCategories.Geom);
        // Default colide nonphysical don't try to colide with anything
        private const CollisionCategories m_default_collisionFlagsNotPhysical = 0;

        private const CollisionCategories m_default_collisionFlagsPhysical = (CollisionCategories.Geom |
                                        CollisionCategories.Character |
                                        CollisionCategories.Land |
                                        CollisionCategories.VolumeDtc);

//        private bool m_collidesLand = true;
        private bool m_collidesWater;
//        public bool m_returnCollisions;

        private bool m_NoColide;  // for now only for internal use for bad meshs


        // Default, Collide with Other Geometries, spaces and Bodies
        private CollisionCategories m_collisionFlags = m_default_collisionFlagsNotPhysical;

        public bool m_disabled;

        private uint m_localID;

        private IMesh m_mesh;
        private object m_meshlock = new object();
        private PrimitiveBaseShape _pbs;

        private UUID? m_assetID;
        private MeshState m_meshState;
        
        public ODEScene _parent_scene;

        /// <summary>
        /// The physics space which contains prim geometry
        /// </summary>
        public IntPtr m_targetSpace;

        public IntPtr prim_geom;
        public IntPtr _triMeshData;

        private PhysicsActor _parent;

        private List<OdePrim> childrenPrim = new List<OdePrim>();

        public float m_collisionscore;
        private int m_colliderfilter = 0;

        public IntPtr collide_geom; // for objects: geom if single prim space it linkset

        private float m_density;
        private byte m_shapetype;
        public bool _zeroFlag;
        private bool m_lastUpdateSent;

        public IntPtr Body;

        private Vector3 _target_velocity;

        public Vector3 m_OBBOffset;
        public Vector3 m_OBB;
        public float primOOBradiusSQ;

        private bool m_hasOBB = true;

        private float m_physCost;
        private float m_streamCost;

        public d.Mass primdMass; // prim inertia information on it's own referencial
        float primMass; // prim own mass
        float primVolume; // prim own volume;
        float _mass; // object mass acording to case

        public int givefakepos;
        private Vector3 fakepos;
        public int givefakeori;
        private Quaternion fakeori;

        private int m_eventsubscription;
        private int m_cureventsubscription;
        private CollisionEventUpdate CollisionEventsThisFrame = null;
        private bool SentEmptyCollisionsEvent;

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

        public override bool IsVolumeDtc
        {
            get { return m_fakeisVolumeDetect; }
            set
            {
                m_fakeisVolumeDetect = value;
                AddChange(changes.VolumeDtc, value);
            }
        }

        public override bool Phantom  // this is not reliable for internal use
        {
            get { return m_fakeisphantom; }
            set
            {
                m_fakeisphantom = value;
                AddChange(changes.Phantom, value);
            }
        }

        public override bool Building  // this is not reliable for internal use
        {
            get { return m_building; }
            set
            {
//                if (value)
//                    m_building = true;
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
//                    cdata.mu *= 0;
            }
        }

        public override float PhysicsCost
        {
            get
            {
                return m_physCost;
            }
        }

        public override float StreamCost
        {
            get
            {
                return m_streamCost;
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
            get { return m_localID; }
            set { m_localID = value; }
        }

        public override PhysicsActor ParentActor
        {
            get
            {
                if (childPrim)
                    return _parent;
                else
                    return (PhysicsActor)this;
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
                    m_iscolliding = false;
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


        public override bool ThrottleUpdates {get;set;}

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
                     _parent_scene.m_meshWorker.ChangeActorPhysRep(this, _pbs, value, m_shapetype);
                }
                else
                {
                    m_log.WarnFormat("[PHYSICS]: Got NaN Size on object {0}", Name);
                }
            }
        }

        public override float Mass
        {
            get { return primMass; }
        }

        public override Vector3 Force
        {
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
            m_fakeisVolumeDetect = (param != 0);
            AddChange(changes.VolumeDtc, m_fakeisVolumeDetect);
        }

        public override Vector3 GeometricCenter
        {
            // this is not real geometric center but a average of positions relative to root prim acording to
            // http://wiki.secondlife.com/wiki/llGetGeometricCenter
            // ignoring tortured prims details since sl also seems to ignore
            // so no real use in doing it on physics
            get
            {
                return Vector3.Zero;
            }
        }

        public override Vector3 CenterOfMass
        {
            get
            {
                lock (_parent_scene.OdeLock)
                {
                    d.AllocateODEDataForThread(0);

                    d.Vector3 dtmp;
                    if (!childPrim && Body != IntPtr.Zero)
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

                        Vector3 Ptot = m_OBBOffset * q;
                        dtmp = d.GeomGetPosition(prim_geom);
                        Ptot.X += dtmp.X;
                        Ptot.Y += dtmp.Y;
                        Ptot.Z += dtmp.Z;

                        //                    if(childPrim)  we only know about physical linksets
                        return Ptot;
/*
                        float tmass = _mass;
                        Ptot *= tmass;

                        float m;

                        foreach (OdePrim prm in childrenPrim)
                        {
                            m = prm._mass;
                            Ptot += prm.CenterOfMass * m;
                            tmass += m;
                        }

                        if (tmass == 0)
                            tmass = 0;
                        else
                            tmass = 1.0f / tmass;

                        Ptot *= tmass;
                        return Ptot;
*/
                    }
                    else
                        return _position;
                }
            }
        }

        public override Vector3 OOBsize
            {
            get
                {
                return m_OBB;
                }
            }

        public override Vector3 OOBoffset
            {
            get
                {
                return m_OBBOffset;
                }
            }

        public override float OOBRadiusSQ
            {
            get
                {
                return primOOBradiusSQ;
                }
            }

        public override PrimitiveBaseShape Shape
        {
            set
            {
//                AddChange(changes.Shape, value);
                _parent_scene.m_meshWorker.ChangeActorPhysRep(this, value, _size, m_shapetype);
            }
        }

        public override byte PhysicsShapeType
        {
            get
            {
                return m_shapetype;
            }
            set
            {
                m_shapetype = value;
               _parent_scene.m_meshWorker.ChangeActorPhysRep(this, _pbs, _size, value);
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

                return m_torque;
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

                    value.Normalize();

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

                if (m_rotationalVelocity.ApproxEquals(pv, 0.0001f))
                    return pv;

                return m_rotationalVelocity;
            }
            set
            {
                if (value.IsFinite())
                {
                    AddChange(changes.AngVelocity, value);
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
                 AddChange(changes.Buoyancy,value);
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
                    AddChange(changes.PIDTarget,value);
                }
                else
                    m_log.WarnFormat("[PHYSICS]: Got NaN PIDTarget from Scene on Object {0}", Name);
            }
        }

        public override bool PIDActive
        {
            get
            {
                return m_usePID;
            }
            set
            {
                AddChange(changes.PIDActive,value);
            }
        }

        public override float PIDTau
        {
            set
            {
                float tmp = 0;
                if (value > 0)
                {
                    float mint = (0.05f > m_timeStep ? 0.05f : m_timeStep);
                    if (value < mint)
                        tmp = mint;
                    else
                        tmp = value;
                }
                AddChange(changes.PIDTau,tmp);
            }
        }

        public override float PIDHoverHeight
        {
            set
            {
                AddChange(changes.PIDHoverHeight,value);
            }
        }
        public override bool PIDHoverActive
        {
            get
            {
                return m_useHoverPID;
            }
            set
            {
                AddChange(changes.PIDHoverActive, value);
            }
        }

        public override PIDHoverType PIDHoverType
        {
            set
            {
                AddChange(changes.PIDHoverType,value);
            }
        }

        public override float PIDHoverTau
        {
            set
            {
                float tmp =0;
                if (value > 0)
                {
                    float mint = (0.05f > m_timeStep ? 0.05f : m_timeStep);
                    if (value < mint)
                        tmp = mint;
                    else
                        tmp = value;
                }
                AddChange(changes.PIDHoverTau, tmp);
            }
        }

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
                if(pushforce)
                    AddChange(changes.AddForce, force);
                else // a impulse
                    AddChange(changes.AddForce, force * m_invTimeStep);
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
//                if(pushforce)  for now applyrotationimpulse seems more happy applied as a force
                    AddChange(changes.AddAngForce, force);
//                else // a impulse
//                    AddChange(changes.AddAngForce, force * m_invTimeStep);
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

                d.AllocateODEDataForThread(0);

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

        public override float Density
        {
            get
            {
                return m_density * 100f;
            }
            set
            {
                m_density = value / 100f;
                // for not prim mass is not updated since this implies full rebuild of body inertia TODO
            }
        }
        public override float GravModifier
        {
            get
            {
                return m_gravmod;
            }
            set
            {
                m_gravmod = value;
                if (m_vehicle != null)
                    m_vehicle.GravMod = m_gravmod;
            }
        }
        public override float Friction
        {
            get
            {
                return mu;
            }
            set
            {
                mu = value;
            }
        }

        public override float Restitution
        {
            get
            {
                return bounce;
            }
            set
            {
                bounce = value;
            }
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

        public override void LockAngularMotion(byte axislock)
        {
//                m_log.DebugFormat("[axislock]: <{0},{1},{2}>", axis.X, axis.Y, axis.Z);
            AddChange(changes.AngLock, axislock);
         
        }

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
           _parent_scene.RemoveCollisionEventReporting(this);
        }

        public override void AddCollisionEvent(uint CollidedWith, ContactPoint contact)
        {
            if (CollisionEventsThisFrame == null)
                CollisionEventsThisFrame = new CollisionEventUpdate();
//            if(CollisionEventsThisFrame.Count < 32)
                CollisionEventsThisFrame.AddCollider(CollidedWith, contact);
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
            if (m_cureventsubscription < 50000)
                m_cureventsubscription += t;
        }

        public override bool SubscribedEvents()
        {
            if (m_eventsubscription > 0)
                return true;
            return false;
        }

        public OdePrim(String primName, ODEScene parent_scene, Vector3 pos, Vector3 size,
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

            m_timeStep = parent_scene.ODE_STEPSIZE;
            m_invTimeStep = 1f / m_timeStep;

            m_density = parent_scene.geomDefaultDensity;
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
            m_fakeisVolumeDetect = false;

            m_force = Vector3.Zero;

            m_iscolliding = false;
            m_colliderfilter = 0;
            m_NoColide = false;

            _triMeshData = IntPtr.Zero;

            m_shapetype = _shapeType;

            m_lastdoneSelected = false;
            m_isSelected = false;
            m_delaySelect = false;

            m_isphantom = pisPhantom;
            m_fakeisphantom = pisPhantom;

            mu = parent_scene.m_materialContactsData[(int)Material.Wood].mu;
            bounce = parent_scene.m_materialContactsData[(int)Material.Wood].bounce;

            m_building = true; // control must set this to false when done

            // get basic mass parameters
            ODEPhysRepData repData = _parent_scene.m_meshWorker.NewActorPhysRep(this, _pbs, _size, m_shapetype);

            primVolume = repData.volume;
            m_OBB = repData.OBB;
            m_OBBOffset = repData.OBBOffset;

            UpdatePrimBodyData();
        }

        private void resetCollisionAccounting()
        {
            m_collisionscore = 0;
        }

        private void UpdateCollisionCatFlags()
        {
            if(m_isphysical && m_disabled)
            {
                m_collisionCategories = 0;
                m_collisionFlags = 0;
            }

            else if (m_isSelected)
            {
                m_collisionCategories = CollisionCategories.Selected;
                m_collisionFlags = 0;
            }

            else if (m_isVolumeDetect)
            {
                m_collisionCategories = CollisionCategories.VolumeDtc;
                if (m_isphysical)
                    m_collisionFlags = CollisionCategories.Geom | CollisionCategories.Character;
                else
                    m_collisionFlags = 0;
            }
            else if (m_isphantom)
            {
                m_collisionCategories = CollisionCategories.Phantom;
                if (m_isphysical)
                    m_collisionFlags = CollisionCategories.Land;
                else
                    m_collisionFlags = 0;
            }
            else
            {
                m_collisionCategories = CollisionCategories.Geom;
                if (m_isphysical)
                    m_collisionFlags = m_default_collisionFlagsPhysical;
                else
                    m_collisionFlags = m_default_collisionFlagsNotPhysical;
            }
        }

        private void ApplyCollisionCatFlags()
        {
            if (prim_geom != IntPtr.Zero)
            {
                if (!childPrim && childrenPrim.Count > 0)
                {
                    foreach (OdePrim prm in childrenPrim)
                    {
                        if (m_isphysical && m_disabled)
                        {
                            prm.m_collisionCategories = 0;
                            prm.m_collisionFlags = 0;
                        }
                        else
                        {
                            // preserve some
                            if (prm.m_isSelected)
                            {
                                prm.m_collisionCategories = CollisionCategories.Selected;
                                prm.m_collisionFlags = 0;
                            }
                            else if (prm.m_isVolumeDetect)
                            {
                                prm.m_collisionCategories = CollisionCategories.VolumeDtc;
                                if (m_isphysical)
                                    prm.m_collisionFlags = CollisionCategories.Geom | CollisionCategories.Character;
                                else
                                    prm.m_collisionFlags = 0;
                            }
                            else if (prm.m_isphantom)
                            {
                                prm.m_collisionCategories = CollisionCategories.Phantom;
                                if (m_isphysical)
                                    prm.m_collisionFlags = CollisionCategories.Land;
                                else
                                    prm.m_collisionFlags = 0;
                            }
                            else
                            {
                                prm.m_collisionCategories = m_collisionCategories;
                                prm.m_collisionFlags = m_collisionFlags;
                            }
                        }

                        if (prm.prim_geom != IntPtr.Zero)
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
                                d.GeomSetCategoryBits(prm.prim_geom, (uint)prm.m_collisionCategories);
                                d.GeomSetCollideBits(prm.prim_geom, (uint)prm.m_collisionFlags);
                            }
                        }
                    }
                }

                if (m_NoColide)
                {
                    d.GeomSetCategoryBits(prim_geom, 0);
                    d.GeomSetCollideBits(prim_geom, (uint)CollisionCategories.Land);
                    if (collide_geom != prim_geom && collide_geom != IntPtr.Zero)
                    {
                        d.GeomSetCategoryBits(collide_geom, 0);
                        d.GeomSetCollideBits(collide_geom, (uint)CollisionCategories.Land);
                    }
                }
                else
                {
                    d.GeomSetCategoryBits(prim_geom, (uint)m_collisionCategories);
                    d.GeomSetCollideBits(prim_geom, (uint)m_collisionFlags);
                    if (collide_geom != prim_geom && collide_geom != IntPtr.Zero)
                    {
                        d.GeomSetCategoryBits(collide_geom, (uint)m_collisionCategories);
                        d.GeomSetCollideBits(collide_geom, (uint)m_collisionFlags);
                    }
                }
            }
        }

        private void createAMotor(byte axislock)
        {
            if (Body == IntPtr.Zero)
                return;

            if (Amotor != IntPtr.Zero)
            {
                d.JointDestroy(Amotor);
                Amotor = IntPtr.Zero;
            }

            int axisnum = 0;
            bool axisX = false;
            bool axisY = false;
            bool axisZ = false;
            if((axislock & 0x02) != 0)
                {
                axisnum++;
                axisX = true;
                }
            if((axislock & 0x04) != 0)
                {
                axisnum++;
                axisY = true;
                }
            if((axislock & 0x08) != 0)
                {
                axisnum++;
                axisZ = true;
                }

            if(axisnum == 0)
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
            if (axisX)
            {
                ax = (new Vector3(1, 0, 0)) * curr; // rotate world X to current local X
                d.JointSetAMotorAxis(Amotor, 0, 0, ax.X, ax.Y, ax.Z);
                d.JointSetAMotorAngle(Amotor, 0, 0);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.LoStop, 0f);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.HiStop, 0f);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.Vel, 0);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.FudgeFactor, 0.0001f);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.Bounce, 0f);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.CFM, 0f);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.FMax, 5e8f);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.StopCFM, 0f);
                d.JointSetAMotorParam(Amotor, (int)d.JointParam.StopERP, 0.8f);
                i++;
                j = 256; // move to next axis set
            }

            if (axisY)
            {
                ax = (new Vector3(0, 1, 0)) * curr;
                d.JointSetAMotorAxis(Amotor, i, 0, ax.X, ax.Y, ax.Z);
                d.JointSetAMotorAngle(Amotor, i, 0);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.LoStop, 0f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.HiStop, 0f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.Vel, 0);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.FudgeFactor, 0.0001f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.Bounce, 0f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.CFM, 0f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.FMax, 5e8f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.StopCFM, 0f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.StopERP, 0.8f);
                i++;
                j += 256;
            }

            if (axisZ)
            {
                ax = (new Vector3(0, 0, 1)) * curr;
                d.JointSetAMotorAxis(Amotor, i, 0, ax.X, ax.Y, ax.Z);
                d.JointSetAMotorAngle(Amotor, i, 0);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.LoStop, 0f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.HiStop, 0f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.Vel, 0);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.FudgeFactor, 0.0001f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.Bounce, 0f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.CFM, 0f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.FMax, 5e8f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.StopCFM, 0f);
                d.JointSetAMotorParam(Amotor, j + (int)d.JointParam.StopERP, 0.8f);
            }
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
                        d.GeomSetCollideBits(prim_geom, (uint)CollisionCategories.Land);
                    }
                    else
                    {
                        d.GeomSetCollideBits(prim_geom, 0);
                        d.GeomDisable(prim_geom);
                    }
                }
                else
                {
                    d.GeomSetCategoryBits(prim_geom, (uint)m_collisionCategories);
                    d.GeomSetCollideBits(prim_geom, (uint)m_collisionFlags);
                }

                UpdatePrimBodyData();
                _parent_scene.actor_name_map[prim_geom] = this;

/*
// debug
                d.AABB aabb;
                d.GeomGetAABB(prim_geom, out aabb);
                float x = aabb.MaxX - aabb.MinX;
                float y = aabb.MaxY - aabb.MinY;
                float z = aabb.MaxZ - aabb.MinZ;
                if( x > 60.0f || y > 60.0f || z > 60.0f)
                    m_log.WarnFormat("[PHYSICS]: large prim geo {0},size {1}, AABBsize <{2},{3},{4}, mesh {5} at {6}",
                        Name, _size.ToString(), x, y, z, _pbs.SculptEntry ? _pbs.SculptTexture.ToString() : "primMesh", _position.ToString());
                else if (x < 0.001f || y < 0.001f || z < 0.001f)
                    m_log.WarnFormat("[PHYSICS]: small prim geo {0},size {1}, AABBsize <{2},{3},{4}, mesh {5} at {6}",
                        Name, _size.ToString(), x, y, z, _pbs.SculptEntry ? _pbs.SculptTexture.ToString() : "primMesh", _position.ToString());
*/

            }
            else
                m_log.Warn("Setting bad Geom");
        }

        private bool GetMeshGeom()
        {
            IntPtr vertices, indices;
            int vertexCount, indexCount;
            int vertexStride, triStride;
            
            IMesh mesh = m_mesh;

            if (mesh == null)
                return false;

            mesh.getVertexListAsPtrToFloatArray(out vertices, out vertexStride, out vertexCount);
            mesh.getIndexListAsPtrToIntArray(out indices, out triStride, out indexCount);

            if (vertexCount == 0 || indexCount == 0)
            {
                m_log.WarnFormat("[PHYSICS]: Invalid mesh data on OdePrim {0}, mesh {1} at {2}",
                    Name, _pbs.SculptEntry ? _pbs.SculptTexture.ToString() : "primMesh",_position.ToString());

                m_hasOBB = false;
                m_OBBOffset = Vector3.Zero;
                m_OBB = _size * 0.5f;

                m_physCost = 0.1f;
                m_streamCost = 1.0f;

                _parent_scene.mesher.ReleaseMesh(mesh);
                m_meshState = MeshState.MeshFailed;
                m_mesh = null;
                return false;
            }

            if (vertexCount > 64000 || indexCount > 64000)
            {
                m_log.WarnFormat("[PHYSICS]: large mesh data on OdePrim {0}, mesh {1} at {2}, {3} vertices, {4} indexes",
                    Name, _pbs.SculptEntry ? _pbs.SculptTexture.ToString() : "primMesh",
                    _position.ToString() ,vertexCount , indexCount );
            }
            IntPtr geo = IntPtr.Zero;

            try
            {
                _triMeshData = d.GeomTriMeshDataCreate();

                d.GeomTriMeshDataBuildSimple(_triMeshData, vertices, vertexStride, vertexCount, indices, indexCount, triStride);
                d.GeomTriMeshDataPreprocess(_triMeshData);

                geo = d.CreateTriMesh(m_targetSpace, _triMeshData, null, null, null);
            }

            catch (Exception e)
            {
                m_log.ErrorFormat("[PHYSICS]: SetGeom Mesh failed for {0} exception: {1}", Name, e);
                if (_triMeshData != IntPtr.Zero)
                {
                    try
                    {
                        d.GeomTriMeshDataDestroy(_triMeshData);
                    }
                    catch
                    {
                    }
                }
                _triMeshData = IntPtr.Zero;

                m_hasOBB = false;
                m_OBBOffset = Vector3.Zero;
                m_OBB = _size * 0.5f;
                m_physCost = 0.1f;
                m_streamCost = 1.0f;
              
                _parent_scene.mesher.ReleaseMesh(mesh);
                m_meshState = MeshState.MeshFailed;
                m_mesh = null;
                return false;
            }

            m_physCost = 0.0013f * (float)indexCount;
            // todo
            m_streamCost = 1.0f;

            SetGeom(geo);

            return true;
        }

        private void CreateGeom()
        {
            bool hasMesh = false;

            m_NoColide = false;

            if ((m_meshState & MeshState.MeshNoColide) != 0)
                m_NoColide = true;

            else if(m_mesh != null)
            {
                if (GetMeshGeom())
                    hasMesh = true;
                else
                    m_NoColide = true;
            }


            if (!hasMesh)
            {
                IntPtr geo = IntPtr.Zero;

                if (_pbs.ProfileShape == ProfileShape.HalfCircle && _pbs.PathCurve == (byte)Extrusion.Curve1
                    && _size.X == _size.Y && _size.Y == _size.Z)
                { // it's a sphere
                    try
                    {
                        geo = d.CreateSphere(m_targetSpace, _size.X * 0.5f);
                    }
                    catch (Exception e)
                    {
                        m_log.WarnFormat("[PHYSICS]: Create sphere failed: {0}", e);
                        return;
                    }
                }
                else
                {// do it as a box
                    try
                    {
                        geo = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                    }
                    catch (Exception e)
                    {
                        m_log.Warn("[PHYSICS]: Create box failed: {0}", e);
                        return;
                    }
                }
                m_physCost = 0.1f;
                m_streamCost = 1.0f;
                SetGeom(geo);
            }
        }

        private void RemoveGeom()
        {
            if (prim_geom != IntPtr.Zero)
            {
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
                catch (Exception e)
                {
                    m_log.ErrorFormat("[PHYSICS]: PrimGeom destruction failed for {0} exception {1}", Name, e);
                }

                prim_geom = IntPtr.Zero;
                collide_geom = IntPtr.Zero;
                m_targetSpace = IntPtr.Zero;
            }
            else
            {
                m_log.ErrorFormat("[PHYSICS]: PrimGeom destruction BAD {0}", Name);
            }

            lock (m_meshlock)
            {
                if (m_mesh != null)
                {
                    _parent_scene.mesher.ReleaseMesh(m_mesh);
                    m_mesh = null;
                }
            }

            Body = IntPtr.Zero;
            m_hasOBB = false;
        }

        //sets non physical prim m_targetSpace to right space in spaces grid for static prims
        // should only be called for non physical prims unless they are becoming non physical
        private void SetInStaticSpace(OdePrim prim)
        {
            IntPtr targetSpace = _parent_scene.MoveGeomToStaticSpace(prim.prim_geom, prim._position, prim.m_targetSpace);
            prim.m_targetSpace = targetSpace;
            collide_geom = IntPtr.Zero;
        }

        public void enableBodySoft()
        {
            m_disabled = false;
            if (!childPrim && !m_isSelected)
            {
                if (m_isphysical && Body != IntPtr.Zero)
                {
                    UpdateCollisionCatFlags();
                    ApplyCollisionCatFlags();

                    _zeroFlag = true;
                    d.BodyEnable(Body);

                }
            }
            resetCollisionAccounting();
        }

        private void disableBodySoft()
        {
            m_disabled = true;
            if (!childPrim)
            {
                if (m_isphysical && Body != IntPtr.Zero)
                {
                    if (m_isSelected)
                        m_collisionFlags = CollisionCategories.Selected;
                    else
                        m_collisionCategories = 0;
                    m_collisionFlags = 0;
                    ApplyCollisionCatFlags();
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
                DestroyBody();
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

            objdmass = primdMass;

            // rotate inertia
            myrot.X = _orientation.X;
            myrot.Y = _orientation.Y;
            myrot.Z = _orientation.Z;
            myrot.W = _orientation.W;

            d.RfromQ(out mymat, ref myrot);
            d.MassRotate(ref objdmass, ref mymat);

            // set the body rotation
            d.BodySetRotation(Body, ref mymat);

            // recompute full object inertia if needed
            if (childrenPrim.Count > 0)
            {
                d.Matrix3 mat = new d.Matrix3();
                d.Quaternion quat = new d.Quaternion();
                d.Mass tmpdmass = new d.Mass { };
                Vector3 rcm;

                rcm.X = _position.X;
                rcm.Y = _position.Y;
                rcm.Z = _position.Z;

                lock (childrenPrim)
                {
                    foreach (OdePrim prm in childrenPrim)
                    {
                        if (prm.prim_geom == IntPtr.Zero)
                        {
                            m_log.Warn("[PHYSICS]: Unable to link one of the linkset elements, skipping it.  No geom yet");
                            continue;
                        }

                        tmpdmass = prm.primdMass;

                        // apply prim current rotation to inertia
                        quat.X = prm._orientation.X;
                        quat.Y = prm._orientation.Y;
                        quat.Z = prm._orientation.Z;
                        quat.W = prm._orientation.W;
                        d.RfromQ(out mat, ref quat);
                        d.MassRotate(ref tmpdmass, ref mat);

                        Vector3 ppos = prm._position;
                        ppos.X -= rcm.X;
                        ppos.Y -= rcm.Y;
                        ppos.Z -= rcm.Z;
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
            myrot.X = -myrot.X;
            myrot.Y = -myrot.Y;
            myrot.Z = -myrot.Z;

            d.RfromQ(out mymat, ref myrot);
            d.MassRotate(ref objdmass, ref mymat);

            d.BodySetMass(Body, ref objdmass);
            _mass = objdmass.mass;

            // disconnect from world gravity so we can apply buoyancy
            d.BodySetGravityMode(Body, false);

            d.BodySetAutoDisableFlag(Body, true);
            d.BodySetAutoDisableSteps(Body, body_autodisable_frames);
            d.BodySetAutoDisableAngularThreshold(Body, 0.05f);
            d.BodySetAutoDisableLinearThreshold(Body, 0.05f);
            d.BodySetDamping(Body, .004f, .001f);

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
            }
            else
            {
                m_targetSpace = d.HashSpaceCreate(_parent_scene.ActiveSpace);
                d.HashSpaceSetLevels(m_targetSpace, -2, 8);
                d.SpaceSetSublevel(m_targetSpace, 3);
                d.SpaceSetCleanup(m_targetSpace, false);

                d.GeomSetCategoryBits(m_targetSpace, (uint)(CollisionCategories.Space |
                                                            CollisionCategories.Geom |
                                                            CollisionCategories.Phantom |
                                                            CollisionCategories.VolumeDtc
                                                            ));
                d.GeomSetCollideBits(m_targetSpace, 0);
                collide_geom = m_targetSpace;
            }

            d.SpaceAdd(m_targetSpace, prim_geom);

            if (m_delaySelect)
            {
                m_isSelected = true;
                m_delaySelect = false;
            }

            m_collisionscore = 0;

            UpdateCollisionCatFlags();
            ApplyCollisionCatFlags();

            _parent_scene.addActivePrim(this);

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

                    prm.m_collisionscore = 0;

                    if(!m_disabled)
                        prm.m_disabled = false;

                    _parent_scene.addActivePrim(prm);
                }
            }

            // The body doesn't already have a finite rotation mode set here
            if (m_angularlocks != 0 && _parent == null)
            {
                createAMotor(m_angularlocks);
            }

            if (m_isSelected || m_disabled)
            {
                d.BodyDisable(Body);
                _zeroFlag = true;
            }
            else
            {
                d.BodySetAngularVel(Body, m_rotationalVelocity.X, m_rotationalVelocity.Y, m_rotationalVelocity.Z);
                d.BodySetLinearVel(Body, _velocity.X, _velocity.Y, _velocity.Z);
                _zeroFlag = false;
                bodydisablecontrol = 0;
            }               
            _parent_scene.addActiveGroups(this);
        }

        private void DestroyBody()
        {
            if (Body != IntPtr.Zero)
            {
                _parent_scene.remActivePrim(this);

                collide_geom = IntPtr.Zero;

                if (m_disabled)
                    m_collisionCategories = 0;
                else if (m_isSelected)
                    m_collisionCategories = CollisionCategories.Selected;
                else if (m_isVolumeDetect)
                    m_collisionCategories = CollisionCategories.VolumeDtc;
                else if (m_isphantom)
                    m_collisionCategories = CollisionCategories.Phantom;
                else
                    m_collisionCategories = CollisionCategories.Geom;

                m_collisionFlags = 0;

                if (prim_geom != IntPtr.Zero)
                {
                    if (m_NoColide)
                    {
                        d.GeomSetCategoryBits(prim_geom, 0);
                        d.GeomSetCollideBits(prim_geom, 0);
                    }
                    else
                    {
                        d.GeomSetCategoryBits(prim_geom, (uint)m_collisionCategories);
                        d.GeomSetCollideBits(prim_geom, (uint)m_collisionFlags);
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

                            if (prm.m_isSelected)
                                prm.m_collisionCategories = CollisionCategories.Selected;
                            else if (prm.m_isVolumeDetect)
                                prm.m_collisionCategories = CollisionCategories.VolumeDtc;
                            else if (prm.m_isphantom)
                                prm.m_collisionCategories = CollisionCategories.Phantom;
                            else
                                prm.m_collisionCategories = CollisionCategories.Geom;

                            prm.m_collisionFlags = 0;

                            if (prm.prim_geom != IntPtr.Zero)
                            {
                                if (prm.m_NoColide)
                                {
                                    d.GeomSetCategoryBits(prm.prim_geom, 0);
                                    d.GeomSetCollideBits(prm.prim_geom, 0);
                                }
                                else
                                {
                                    d.GeomSetCategoryBits(prm.prim_geom, (uint)prm.m_collisionCategories);
                                    d.GeomSetCollideBits(prm.prim_geom, (uint)prm.m_collisionFlags);
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

        private void FixInertia(Vector3 NewPos,Quaternion newrot)
        {
            d.Matrix3 mat = new d.Matrix3();
            d.Quaternion quat = new d.Quaternion();

            d.Mass tmpdmass = new d.Mass { };         
            d.Mass objdmass = new d.Mass { };

            d.BodyGetMass(Body, out tmpdmass);
            objdmass = tmpdmass;

            d.Vector3 dobjpos;
            d.Vector3 thispos;

            // get current object position and rotation
            dobjpos = d.BodyGetPosition(Body);

            // get prim own inertia in its local frame
            tmpdmass = primdMass;

            // transform to object frame
            mat = d.GeomGetOffsetRotation(prim_geom);
            d.MassRotate(ref tmpdmass, ref mat);

            thispos = d.GeomGetOffsetPosition(prim_geom);
            d.MassTranslate(ref tmpdmass,
                            thispos.X,
                            thispos.Y,
                            thispos.Z);

            // subtract current prim inertia from object
            DMassSubPartFromObj(ref tmpdmass, ref objdmass);

            // back prim own inertia 
            tmpdmass = primdMass;

            // update to new position and orientation
            _position = NewPos;
            d.GeomSetOffsetWorldPosition(prim_geom, NewPos.X, NewPos.Y, NewPos.Z);
            _orientation = newrot;
            quat.X = newrot.X;
            quat.Y = newrot.Y;
            quat.Z = newrot.Z;
            quat.W = newrot.W;
            d.GeomSetOffsetWorldQuaternion(prim_geom, ref quat);

            mat = d.GeomGetOffsetRotation(prim_geom);
            d.MassRotate(ref tmpdmass, ref mat);

            thispos = d.GeomGetOffsetPosition(prim_geom);
            d.MassTranslate(ref tmpdmass,
                            thispos.X,
                            thispos.Y,
                            thispos.Z);

            d.MassAdd(ref objdmass, ref tmpdmass);

            // fix all positions
            IntPtr g = d.BodyGetFirstGeom(Body);
            while (g != IntPtr.Zero)
            {
                thispos = d.GeomGetOffsetPosition(g);
                thispos.X -= objdmass.c.X;
                thispos.Y -= objdmass.c.Y;
                thispos.Z -= objdmass.c.Z;
                d.GeomSetOffsetPosition(g, thispos.X, thispos.Y, thispos.Z);
                g = d.dBodyGetNextGeom(g);
            }
            d.BodyVectorToWorld(Body,objdmass.c.X, objdmass.c.Y, objdmass.c.Z,out thispos);

            d.BodySetPosition(Body, dobjpos.X + thispos.X, dobjpos.Y + thispos.Y, dobjpos.Z + thispos.Z);
            d.MassTranslate(ref objdmass, -objdmass.c.X, -objdmass.c.Y, -objdmass.c.Z); // ode wants inertia at center of body
            d.BodySetMass(Body, ref objdmass);
            _mass = objdmass.mass;
        }

        private void FixInertia(Vector3 NewPos)
        {
            d.Matrix3 primmat = new d.Matrix3();
            d.Mass tmpdmass = new d.Mass { };
            d.Mass objdmass = new d.Mass { };
            d.Mass primmass = new d.Mass { };

            d.Vector3 dobjpos;
            d.Vector3 thispos;

            d.BodyGetMass(Body, out objdmass);

            // get prim own inertia in its local frame
            primmass = primdMass;
            // transform to object frame
            primmat = d.GeomGetOffsetRotation(prim_geom);
            d.MassRotate(ref primmass, ref primmat);

            tmpdmass = primmass;

            thispos = d.GeomGetOffsetPosition(prim_geom);
            d.MassTranslate(ref tmpdmass,
                            thispos.X,
                            thispos.Y,
                            thispos.Z);

            // subtract current prim inertia from object
            DMassSubPartFromObj(ref tmpdmass, ref objdmass);

            // update to new position
            _position = NewPos;
            d.GeomSetOffsetWorldPosition(prim_geom, NewPos.X, NewPos.Y, NewPos.Z);

            thispos = d.GeomGetOffsetPosition(prim_geom);
            d.MassTranslate(ref primmass,
                            thispos.X,
                            thispos.Y,
                            thispos.Z);

            d.MassAdd(ref objdmass, ref primmass);

            // fix all positions
            IntPtr g = d.BodyGetFirstGeom(Body);
            while (g != IntPtr.Zero)
            {
                thispos = d.GeomGetOffsetPosition(g);
                thispos.X -= objdmass.c.X;
                thispos.Y -= objdmass.c.Y;
                thispos.Z -= objdmass.c.Z;
                d.GeomSetOffsetPosition(g, thispos.X, thispos.Y, thispos.Z);
                g = d.dBodyGetNextGeom(g);
            }

            d.BodyVectorToWorld(Body, objdmass.c.X, objdmass.c.Y, objdmass.c.Z, out thispos);

            // get current object position and rotation
            dobjpos = d.BodyGetPosition(Body);

            d.BodySetPosition(Body, dobjpos.X + thispos.X, dobjpos.Y + thispos.Y, dobjpos.Z + thispos.Z);
            d.MassTranslate(ref objdmass, -objdmass.c.X, -objdmass.c.Y, -objdmass.c.Z); // ode wants inertia at center of body
            d.BodySetMass(Body, ref objdmass);
            _mass = objdmass.mass;
        }

        private void FixInertia(Quaternion newrot)
        {
            d.Matrix3 mat = new d.Matrix3();
            d.Quaternion quat = new d.Quaternion();

            d.Mass tmpdmass = new d.Mass { };
            d.Mass objdmass = new d.Mass { };
            d.Vector3 dobjpos;
            d.Vector3 thispos;

            d.BodyGetMass(Body, out objdmass);

            // get prim own inertia in its local frame
            tmpdmass = primdMass;
            mat = d.GeomGetOffsetRotation(prim_geom);
            d.MassRotate(ref tmpdmass, ref mat);
            // transform to object frame
            thispos = d.GeomGetOffsetPosition(prim_geom);
            d.MassTranslate(ref tmpdmass,
                            thispos.X,
                            thispos.Y,
                            thispos.Z);

            // subtract current prim inertia from object
            DMassSubPartFromObj(ref tmpdmass, ref objdmass);

            // update to new orientation
            _orientation = newrot;
            quat.X = newrot.X;
            quat.Y = newrot.Y;
            quat.Z = newrot.Z;
            quat.W = newrot.W;
            d.GeomSetOffsetWorldQuaternion(prim_geom, ref quat);

            tmpdmass = primdMass;
            mat = d.GeomGetOffsetRotation(prim_geom);
            d.MassRotate(ref tmpdmass, ref mat);
            d.MassTranslate(ref tmpdmass,
                            thispos.X,
                            thispos.Y,
                            thispos.Z);

            d.MassAdd(ref objdmass, ref tmpdmass);

            // fix all positions
            IntPtr g = d.BodyGetFirstGeom(Body);
            while (g != IntPtr.Zero)
            {
                thispos = d.GeomGetOffsetPosition(g);
                thispos.X -= objdmass.c.X;
                thispos.Y -= objdmass.c.Y;
                thispos.Z -= objdmass.c.Z;
                d.GeomSetOffsetPosition(g, thispos.X, thispos.Y, thispos.Z);
                g = d.dBodyGetNextGeom(g);
            }

            d.BodyVectorToWorld(Body, objdmass.c.X, objdmass.c.Y, objdmass.c.Z, out thispos);
            // get current object position and rotation
            dobjpos = d.BodyGetPosition(Body);

            d.BodySetPosition(Body, dobjpos.X + thispos.X, dobjpos.Y + thispos.Y, dobjpos.Z + thispos.Z);
            d.MassTranslate(ref objdmass, -objdmass.c.X, -objdmass.c.Y, -objdmass.c.Z); // ode wants inertia at center of body
            d.BodySetMass(Body, ref objdmass);
            _mass = objdmass.mass;
        }


        #region Mass Calculation

        private void UpdatePrimBodyData()
        {
            primMass = m_density * primVolume;

            if (primMass <= 0)
                primMass = 0.0001f;//ckrinke: Mass must be greater then zero.
            if (primMass > _parent_scene.maximumMassObject)
                primMass = _parent_scene.maximumMassObject;

            _mass = primMass; // just in case

            d.MassSetBoxTotal(out primdMass, primMass, 2.0f * m_OBB.X, 2.0f * m_OBB.Y, 2.0f * m_OBB.Z);

            d.MassTranslate(ref primdMass,
                                m_OBBOffset.X,
                                m_OBBOffset.Y,
                                m_OBBOffset.Z);

            primOOBradiusSQ = m_OBB.LengthSquared();

            if (_triMeshData != IntPtr.Zero)
            {
                float pc = m_physCost;
                float psf = primOOBradiusSQ;
                psf *= 1.33f * .2f;
                pc *= psf;
                if (pc < 0.1f)
                    pc = 0.1f;

                m_physCost = pc;
            }
            else
                m_physCost = 0.1f;

            m_streamCost = 1.0f;
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
                d.Quaternion qtmp;
                d.GeomCopyQuaternion(prim_geom, out qtmp);
                _orientation.X = qtmp.X;
                _orientation.Y = qtmp.Y;
                _orientation.Z = qtmp.Z;
                _orientation.W = qtmp.W;
/*
// Debug
                float qlen = _orientation.Length();
                if (qlen > 1.01f || qlen < 0.99)
                    m_log.WarnFormat("[PHYSICS]: Got nonnorm quaternion from geom in Object {0} norm {1}", Name, qlen);
//
*/
                _orientation.Normalize();

                d.Vector3 lpos = d.GeomGetPosition(prim_geom);
                _position.X = lpos.X;
                _position.Y = lpos.Y;
                _position.Z = lpos.Z;
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
        }

        private void changeAngularLock(byte newLocks)
        {
            // do we have a Physical object?
            if (Body != IntPtr.Zero)
            {
                //Check that we have a Parent
                //If we have a parent then we're not authorative here
                if (_parent == null)
                {
                    if (newLocks != 0)
                    {
                        createAMotor(newLocks);
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
            m_angularlocks = newLocks;
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
//                m_force = Vector3.Zero;
                m_forceacc = Vector3.Zero;
                m_angularForceacc = Vector3.Zero;
//                m_torque = Vector3.Zero;
                _velocity = Vector3.Zero;
                _acceleration = Vector3.Zero;
                m_rotationalVelocity = Vector3.Zero;
                _target_velocity = Vector3.Zero;
                if (m_vehicle != null && m_vehicle.Type != Vehicle.TYPE_NONE)
                    m_vehicle.Stop();

                _zeroFlag = false;
                base.RequestPhysicsterseUpdate();
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

            UpdateCollisionCatFlags();
            ApplyCollisionCatFlags();
        }

/* not in use
        internal void ChildSelectedChange(bool childSelect)
        {
            if(childPrim)
                return;

            if (childSelect == m_isSelected)
                return;

            if (childSelect)
            {
                DoSelectedStatus(true);
            }

            else
            {
                foreach (OdePrim prm in childrenPrim)
                {
                    if (prm.m_isSelected)
                        return;
                }
                DoSelectedStatus(false);
            }
        }
*/
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
                    m_collisionFlags = 0;

                    if (!childPrim)
                    {
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
                                    d.GeomSetCategoryBits(prm.prim_geom, (uint)m_collisionCategories);
                                    d.GeomSetCollideBits(prm.prim_geom, (uint)m_collisionFlags);
                                }
                            }
                            prm.m_delaySelect = false;
                        }
                    }
//                    else if (_parent != null)
//                        ((OdePrim)_parent).ChildSelectedChange(true);


                    if (prim_geom != IntPtr.Zero)
                    {
                        if (m_NoColide)
                        {
                            d.GeomSetCategoryBits(prim_geom, 0);
                            d.GeomSetCollideBits(prim_geom, 0);
                            if (collide_geom != prim_geom && collide_geom != IntPtr.Zero)
                            {
                                d.GeomSetCategoryBits(collide_geom, 0);
                                d.GeomSetCollideBits(collide_geom, 0);
                            }

                        }
                        else
                        {
                            d.GeomSetCategoryBits(prim_geom, (uint)m_collisionCategories);
                            d.GeomSetCollideBits(prim_geom, (uint)m_collisionFlags);
                            if (collide_geom != prim_geom && collide_geom != IntPtr.Zero)
                            {
                                d.GeomSetCategoryBits(collide_geom, (uint)m_collisionCategories);
                                d.GeomSetCollideBits(collide_geom, (uint)m_collisionFlags);
                            }
                        }
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
                if (!childPrim)
                {
                    if (Body != IntPtr.Zero && !m_disabled)
                    {
                        _zeroFlag = true;
                        d.BodyEnable(Body);
                    }
                }
//                else if (_parent != null)
//                    ((OdePrim)_parent).ChildSelectedChange(false);

                UpdateCollisionCatFlags();
                ApplyCollisionCatFlags();

                m_delaySelect = false;
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

                    else if (m_forcePosOrRotation && _position != newPos && Body != IntPtr.Zero)
                    {
                        FixInertia(newPos);
                        if (!d.BodyIsEnabled(Body))
                        {
                            _zeroFlag = true;
                            d.BodyEnable(Body);
                        }
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
                    {
                        _zeroFlag = true;
                        d.BodyEnable(Body);
                    }
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
/*
                    else if (m_forcePosOrRotation && _orientation != newOri && Body != IntPtr.Zero)
                    {
                        FixInertia(_position, newOri);
                        if (!d.BodyIsEnabled(Body))
                            d.BodyEnable(Body);
                    }
*/
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
                        if (Body != IntPtr.Zero && m_angularlocks != 0)
                            createAMotor(m_angularlocks);
                    }
                    if (Body != IntPtr.Zero && !d.BodyIsEnabled(Body))
                    {
                        _zeroFlag = true;
                        d.BodyEnable(Body);
                    }
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
                        if (Body != IntPtr.Zero && m_angularlocks != 0)
                            createAMotor(m_angularlocks);
                    }
                    if (_position != newPos)
                    {
                        d.GeomSetPosition(prim_geom, newPos.X, newPos.Y, newPos.Z);
                        _position = newPos;
                    }
                    if (Body != IntPtr.Zero && !d.BodyIsEnabled(Body))
                    {
                        _zeroFlag = true;
                        d.BodyEnable(Body);
                    }
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

        private void changeSize(Vector3 newSize)
        {
        }

        private void changeShape(PrimitiveBaseShape newShape)
        {
        }

        private void changeAddPhysRep(ODEPhysRepData repData)
        {
            _size = repData.size; //??
            _pbs = repData.pbs;
            m_shapetype = repData.shapetype;

            m_mesh = repData.mesh;

            m_assetID = repData.assetID;
            m_meshState = repData.meshState;

            m_hasOBB = repData.hasOBB;
            m_OBBOffset = repData.OBBOffset;
            m_OBB = repData.OBB;

            primVolume = repData.volume;

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

            if (!m_isphysical)
            {
                SetInStaticSpace(this);
                UpdateCollisionCatFlags();
                ApplyCollisionCatFlags();
            }
            else
                MakeBody();

            if ((m_meshState & MeshState.NeedMask) != 0)
            {
                repData.size = _size;
                repData.pbs = _pbs;
                repData.shapetype = m_shapetype;
                _parent_scene.m_meshWorker.RequestMesh(repData);
            }
        }

        private void changePhysRepData(ODEPhysRepData repData)
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

            _size = repData.size;
            _pbs = repData.pbs;
            m_shapetype = repData.shapetype;

            m_mesh = repData.mesh;

            m_assetID = repData.assetID;
            m_meshState = repData.meshState;

            m_hasOBB = repData.hasOBB;
            m_OBBOffset = repData.OBBOffset;
            m_OBB = repData.OBB;

            primVolume = repData.volume;

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

            if (m_isphysical)
            {
                if (chp)
                {
                    if (parent != null)
                    {
                        parent.MakeBody();
                    }
                }
                else
                    MakeBody();
            }
            else
            {
                SetInStaticSpace(this);
                UpdateCollisionCatFlags();
                ApplyCollisionCatFlags();
            }

            resetCollisionAccounting();

            if ((m_meshState & MeshState.NeedMask) != 0)
            {
                repData.size = _size;
                repData.pbs = _pbs;
                repData.shapetype = m_shapetype;
                _parent_scene.m_meshWorker.RequestMesh(repData);
            }
        }

        private void changeFloatOnWater(bool newval)
        {
            m_collidesWater = newval;

            UpdateCollisionCatFlags();
            ApplyCollisionCatFlags();
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
                m_torque = newtorque;
            }
        }

        private void changeForce(Vector3 force)
        {
            m_force = force;
            if (Body != IntPtr.Zero && !d.BodyIsEnabled(Body))
                d.BodyEnable(Body);
        }

        private void changeAddForce(Vector3 theforce)
        {
            m_forceacc += theforce;
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

        // actually angular impulse
        private void changeAddAngularImpulse(Vector3 aimpulse)
        {
            m_angularForceacc += aimpulse * m_invTimeStep;
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
            float len = newVel.LengthSquared();
            if (len > 100000.0f) // limit to 100m/s
            {
                len = 100.0f / (float)Math.Sqrt(len);
                newVel *= len;
            }

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

        private void changeangvelocity(Vector3 newAngVel)
        {
            float len = newAngVel.LengthSquared();
            if (len > _parent_scene.maxAngVelocitySQ)
            {
                len = _parent_scene.maximumAngularVelocity / (float)Math.Sqrt(len);
                newAngVel *= len;
            }

            if (!m_isSelected)
            {
                if (Body != IntPtr.Zero)
                {
                    if (m_disabled)
                        enableBodySoft();
                    else if (!d.BodyIsEnabled(Body))
                        d.BodyEnable(Body);


                    d.BodySetAngularVel(Body, newAngVel.X, newAngVel.Y, newAngVel.Z);
                }
                //resetCollisionAccounting();           
            }
            m_rotationalVelocity = newAngVel;
        }

        private void changeVolumedetetion(bool newVolDtc)
        {
            m_isVolumeDetect = newVolDtc;
            m_fakeisVolumeDetect = newVolDtc;
            UpdateCollisionCatFlags();
            ApplyCollisionCatFlags();
        }

        protected void changeBuilding(bool newbuilding)
        {
            // Check if we need to do anything
            if (newbuilding == m_building)
                return;

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

        private void changeBuoyancy(float b)
        {
            m_buoyancy = b;
        }

        private void changePIDTarget(Vector3 trg)
        {
            m_PIDTarget = trg;
        }

        private void changePIDTau(float tau)
        {
            m_PIDTau = tau;
        }

        private void changePIDActive(bool val)
        {
            m_usePID = val;
        }

        private void changePIDHoverHeight(float val)
        {
          m_PIDHoverHeight = val;
          if (val == 0)
            m_useHoverPID = false;
        }

        private void changePIDHoverType(PIDHoverType type)
        {
            m_PIDHoverType = type;
        }

        private void changePIDHoverTau(float tau)
        {
            m_PIDHoverTau = tau;
        }

        private void changePIDHoverActive(bool active)
        {
            m_useHoverPID = active;
        }

        #endregion

        public void Move()
        {
            if (!childPrim && m_isphysical && Body != IntPtr.Zero &&
                !m_disabled && !m_isSelected && !m_building && !m_outbounds)
            {
                if (!d.BodyIsEnabled(Body))
                {
                    // let vehicles sleep
                    if (m_vehicle != null && m_vehicle.Type != Vehicle.TYPE_NONE)
                        return;
                    
                    if (++bodydisablecontrol < 50)
                        return;

                    // clear residuals
                    d.BodySetAngularVel(Body,0f,0f,0f);
                    d.BodySetLinearVel(Body,0f,0f,0f);
                    _zeroFlag = true;
                    d.BodyEnable(Body);
                    bodydisablecontrol = -4;
                }

                if(bodydisablecontrol < 0)
                    bodydisablecontrol ++;

                d.Vector3 lpos = d.GeomGetPosition(prim_geom); // root position that is seem by rest of simulator

                if (m_vehicle != null && m_vehicle.Type != Vehicle.TYPE_NONE)
                {
                    // 'VEHICLES' are dealt with in ODEDynamics.cs
                    m_vehicle.Step();
                    return;
                }

                float fx = 0;
                float fy = 0;
                float fz = 0;

                float m_mass = _mass;

                if (m_usePID && m_PIDTau > 0)
                {
                    // for now position error
                    _target_velocity =
                        new Vector3(
                            (m_PIDTarget.X - lpos.X),
                            (m_PIDTarget.Y - lpos.Y),
                            (m_PIDTarget.Z - lpos.Z)
                            );

                    if (_target_velocity.ApproxEquals(Vector3.Zero, 0.02f))
                    {
                        d.BodySetPosition(Body, m_PIDTarget.X, m_PIDTarget.Y, m_PIDTarget.Z);
                        d.BodySetLinearVel(Body, 0, 0, 0);
                        return;
                    }
                    else
                    {
                        _zeroFlag = false;

                        float tmp = 1 / m_PIDTau;
                        _target_velocity *= tmp;

                        // apply limits
                        tmp = _target_velocity.Length();
                        if (tmp > 50.0f)
                        {
                            tmp = 50 / tmp;
                            _target_velocity *= tmp;
                        }
                        else if (tmp < 0.05f)
                        {
                            tmp = 0.05f / tmp;
                            _target_velocity *= tmp;
                        }

                        d.Vector3 vel = d.BodyGetLinearVel(Body);
                        fx = (_target_velocity.X - vel.X) * m_invTimeStep;
                        fy = (_target_velocity.Y - vel.Y) * m_invTimeStep;
                        fz = (_target_velocity.Z - vel.Z) * m_invTimeStep;
//                        d.BodySetLinearVel(Body, _target_velocity.X, _target_velocity.Y, _target_velocity.Z);
                    }
                }        // end if (m_usePID)

                // Hover PID Controller needs to be mutually exlusive to MoveTo PID controller
                else if (m_useHoverPID && m_PIDHoverTau != 0 && m_PIDHoverHeight != 0)
                {

                    //    Non-Vehicles have a limited set of Hover options.
                    // determine what our target height really is based on HoverType

                    m_groundHeight = _parent_scene.GetTerrainHeightAtXY(lpos.X, lpos.Y);

                    switch (m_PIDHoverType)
                    {
                        case PIDHoverType.Ground:
                            m_targetHoverHeight = m_groundHeight + m_PIDHoverHeight;
                            break;

                        case PIDHoverType.GroundAndWater:
                            m_waterHeight = _parent_scene.GetWaterLevel();
                            if (m_groundHeight > m_waterHeight)
                                m_targetHoverHeight = m_groundHeight + m_PIDHoverHeight;
                            else
                                m_targetHoverHeight = m_waterHeight + m_PIDHoverHeight;
                            break;
                    }     // end switch (m_PIDHoverType)

                    // don't go underground unless volumedetector 

                    if (m_targetHoverHeight > m_groundHeight || m_isVolumeDetect)
                    {
                        d.Vector3 vel = d.BodyGetLinearVel(Body);

                        fz = (m_targetHoverHeight - lpos.Z);

                        //  if error is zero, use position control; otherwise, velocity control
                        if (Math.Abs(fz) < 0.01f)
                        {
                            d.BodySetPosition(Body, lpos.X, lpos.Y, m_targetHoverHeight);
                            d.BodySetLinearVel(Body, vel.X, vel.Y, 0);
                        }
                        else
                        {
                            _zeroFlag = false;
                            fz /= m_PIDHoverTau;

                            float tmp = Math.Abs(fz);
                            if (tmp > 50)
                                fz = 50 * Math.Sign(fz);
                            else if (tmp < 0.1)
                                fz = 0.1f * Math.Sign(fz);

                            fz = ((fz - vel.Z) * m_invTimeStep);
                        }
                    }
                }
                else
                {
                    float b = (1.0f - m_buoyancy) * m_gravmod;
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

                trq = m_torque;
                trq += m_angularForceacc;
                m_angularForceacc = Vector3.Zero;
                if (trq.X != 0 || trq.Y != 0 || trq.Z != 0)
                {
                    d.BodyAddTorque(Body, trq.X, trq.Y, trq.Z);
                }
            }
            else
            {    // is not physical, or is not a body or is selected
                //  _zeroPosition = d.BodyGetPosition(Body);
                return;
                //Console.WriteLine("Nothing " +  Name);

            }
        }

        public void UpdatePositionAndVelocity(int frame)
        {
            if (_parent == null && !m_disabled && !m_building && !m_outbounds && Body != IntPtr.Zero)
            {
                bool bodyenabled = d.BodyIsEnabled(Body);

                if(bodydisablecontrol < 0)
                    return;

                if (bodyenabled || !_zeroFlag)
                {
                    bool lastZeroFlag = _zeroFlag;

                    d.Vector3 lpos = d.GeomGetPosition(prim_geom);

                    // check outside region
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

//                        throttleCounter = 0;
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
                    else if (lpos.X > _parent_scene.WorldExtents.X)
                    {
                        _position.X = Util.Clip(lpos.X, _parent_scene.WorldExtents.X + 0.1f, _parent_scene.WorldExtents.X + 2f);
                        m_outbounds = true;
                    }
                    if (lpos.Y < 0f)
                    {
                        _position.Y = Util.Clip(lpos.Y, -2f, -0.1f);
                        m_outbounds = true;
                    }
                    else if (lpos.Y > _parent_scene.WorldExtents.Y)
                    {
                        _position.Y = Util.Clip(lpos.Y, _parent_scene.WorldExtents.Y + 0.1f, _parent_scene.WorldExtents.Y + 2f);
                        m_outbounds = true;
                    }

                    if (m_outbounds)
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
                        UnSubscribeEvents();

                        base.RequestPhysicsterseUpdate();
                        return;
                    }

                    d.Quaternion ori;
                    d.GeomCopyQuaternion(prim_geom, out ori);

                    // decide if moving
                    // use positions since this are integrated quantities
                    // tolerance values depende a lot on simulation noise...
                    // use simple math.abs since we dont need to be exact
                    if(!bodyenabled)
                    {
                        _zeroFlag = true;
                    }
                    else
                    {
                        float poserror;
                        float angerror;
                        if(_zeroFlag)
                        {
                            poserror = 0.01f;
                            angerror = 0.001f;
                        }
                        else
                        {
                            poserror = 0.005f;
                            angerror = 0.0005f;
                        }

                        if (
                            (Math.Abs(_position.X - lpos.X) < poserror)
                            && (Math.Abs(_position.Y - lpos.Y) < poserror)
                            && (Math.Abs(_position.Z - lpos.Z) < poserror)
                            && (Math.Abs(_orientation.X - ori.X) < angerror)
                            && (Math.Abs(_orientation.Y - ori.Y) < angerror)
                            && (Math.Abs(_orientation.Z - ori.Z) < angerror)  // ignore W
                            )
                            _zeroFlag = true;
                        else
                            _zeroFlag = false;
                    }

                    // update position
                    if (!(_zeroFlag && lastZeroFlag))
                    {
                        _position.X = lpos.X;
                        _position.Y = lpos.Y;
                        _position.Z = lpos.Z;

                        _orientation.X = ori.X;
                        _orientation.Y = ori.Y;
                        _orientation.Z = ori.Z;
                        _orientation.W = ori.W;                   
                    }

                    // update velocities and aceleration
                    if (_zeroFlag || lastZeroFlag)
                    {
                         // disable interpolators
                        _velocity = Vector3.Zero;
                        _acceleration = Vector3.Zero;
                         m_rotationalVelocity = Vector3.Zero;
                    }
                    else
                    {
                        d.Vector3 vel = d.BodyGetLinearVel(Body);

                        _acceleration = _velocity;

                        if ((Math.Abs(vel.X) < 0.005f) &&
                            (Math.Abs(vel.Y) < 0.005f) &&
                            (Math.Abs(vel.Z) < 0.005f))
                        {
                            _velocity = Vector3.Zero;
                            float t = -m_invTimeStep;
                            _acceleration = _acceleration * t;
                        }
                        else
                        {
                            _velocity.X = vel.X;
                            _velocity.Y = vel.Y;
                            _velocity.Z = vel.Z;
                            _acceleration = (_velocity - _acceleration) * m_invTimeStep;
                        }

                        if ((Math.Abs(_acceleration.X) < 0.01f) &&
                            (Math.Abs(_acceleration.Y) < 0.01f) &&
                            (Math.Abs(_acceleration.Z) < 0.01f))
                        {
                            _acceleration = Vector3.Zero;
                        }

                        vel = d.BodyGetAngularVel(Body);
                        if ((Math.Abs(vel.X) < 0.0001) &&
                           (Math.Abs(vel.Y) < 0.0001) &&
                           (Math.Abs(vel.Z) < 0.0001)
                           )
                        {
                            m_rotationalVelocity = Vector3.Zero;
                        }
                        else
                        {
                            m_rotationalVelocity.X = vel.X;
                            m_rotationalVelocity.Y = vel.Y;
                            m_rotationalVelocity.Z = vel.Z;
                        }
                    }

                    if (_zeroFlag)
                    {
                        if (!m_lastUpdateSent)
                        {
                            base.RequestPhysicsterseUpdate();
                            if (lastZeroFlag)
                                m_lastUpdateSent = true;
                        }
                        return;
                    }

                    base.RequestPhysicsterseUpdate();
                    m_lastUpdateSent = false;
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

        internal static void DMassSubPartFromObj(ref d.Mass part, ref d.Mass theobj)
        {
            // assumes object center of mass is zero
            float smass = part.mass;
            theobj.mass -= smass;

            smass *= 1.0f / (theobj.mass); ;

            theobj.c.X -= part.c.X * smass;
            theobj.c.Y -= part.c.Y * smass;
            theobj.c.Z -= part.c.Z * smass;

            theobj.I.M00 -= part.I.M00;
            theobj.I.M01 -= part.I.M01;
            theobj.I.M02 -= part.I.M02;
            theobj.I.M10 -= part.I.M10;
            theobj.I.M11 -= part.I.M11;
            theobj.I.M12 -= part.I.M12;
            theobj.I.M20 -= part.I.M20;
            theobj.I.M21 -= part.I.M21;
            theobj.I.M22 -= part.I.M22;
        }

        private void donullchange()
        {
        }

        public bool DoAChange(changes what, object arg)
        {
            if (prim_geom == IntPtr.Zero && what != changes.Add && what != changes.AddPhysRep && what != changes.Remove)
            {
                return false;
            }

            // nasty switch
            switch (what)
            {
                case changes.Add:
                    changeadd();
                    break;

                case changes.AddPhysRep:
                    changeAddPhysRep((ODEPhysRepData)arg);
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

                case changes.AngVelocity:
                    changeangvelocity((Vector3)arg);
                    break;

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
                    changeAddAngularImpulse((Vector3)arg);
                    break;

                case changes.AngLock:
                    changeAngularLock((byte)arg);
                    break;

                case changes.Size:
                    changeSize((Vector3)arg);
                    break;

                case changes.Shape:
                    changeShape((PrimitiveBaseShape)arg);
                    break;

                case changes.PhysRepData:
                    changePhysRepData((ODEPhysRepData) arg);
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

                case changes.Buoyancy:
                    changeBuoyancy((float)arg);
                    break;

                case changes.PIDTarget:
                    changePIDTarget((Vector3)arg);
                    break;

                case changes.PIDTau:
                    changePIDTau((float)arg);
                    break;

                case changes.PIDActive:
                    changePIDActive((bool)arg);
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
