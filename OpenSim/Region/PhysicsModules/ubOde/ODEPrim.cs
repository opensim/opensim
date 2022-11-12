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
using System.Runtime.CompilerServices;
using log4net;
using OpenMetaverse;
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

        internal bool m_building;
        protected bool m_forcePosOrRotation;
        private bool m_iscolliding;

        internal bool m_isSelected;
        private bool m_delaySelect;
        private bool m_lastdoneSelected;
        internal bool m_outbounds;

        private byte m_angularlocks = 0;

        private Quaternion m_lastorientation;
        private Quaternion m_orientation;

        private Vector3 m_position;
        private Vector3 _velocity;
        private Vector3 m_lastVelocity;
        private Vector3 m_lastposition;
        private Vector3 m_rotationalVelocity;
        private Vector3 m_size;
        private Vector3 m_acceleration;
        private IntPtr Amotor;

        internal Vector3 m_force;
        internal Vector3 m_forceacc;
        internal Vector3 m_torque;
        internal Vector3 m_angularForceacc;

        public readonly ODEScene m_parentScene;
        private readonly float m_sceneInverseTimeStep;
        private readonly float m_sceneTimeStep;

        private Vector3 m_PIDTarget;
        private float m_PIDTau;
        private bool m_usePID;

        private float m_PIDHoverHeight;
        private float m_PIDHoverTau;
        private bool m_useHoverPID;
        private PIDHoverType m_PIDHoverType;
        private float m_targetHoverHeight;
        private float m_buoyancy;                //KF: m_buoyancy should be set by llSetBuoyancy() for non-vehicle.

        private readonly int m_body_autodisable_frames;
        public int m_bodydisablecontrol = 0;
        private float m_gravmod = 1.0f;

        // Default we're a Geometry
        private CollisionCategories m_collisionCategories = (CollisionCategories.Geom);
        // Default colide nonphysical don't try to colide with anything
        private const CollisionCategories m_default_collisionFlagsNotPhysical = 0;

        private const CollisionCategories m_default_collisionFlagsPhysical = (CollisionCategories.Geom |
                                        CollisionCategories.Character |
                                        CollisionCategories.Land |
                                        CollisionCategories.VolumeDtc);

        //private bool m_collidesLand = true;
        //private bool m_collidesWater;
        //public bool m_returnCollisions;

        private bool m_NoColide;  // for now only for internal use for bad meshs


        // Default, Collide with Other Geometries, spaces and Bodies
        private CollisionCategories m_collisionFlags = m_default_collisionFlagsNotPhysical;

        public bool m_disabled;

        private IMesh m_mesh;
        private readonly object m_meshlock = new();
        private PrimitiveBaseShape m_pbs;

        private UUID? m_assetID;
        private MeshState m_meshState;


        /// <summary>
        /// The physics space which contains prim geometry
        /// </summary>
        public IntPtr m_targetSpace;

        public IntPtr m_prim_geom;
        public IntPtr _triMeshData;

        private PhysicsActor _parent;

        private readonly List<OdePrim> childrenPrim = new();

        public float m_collisionscore;
        private int m_colliderfilter = 0;

        public IntPtr m_collide_geom; // for objects: geom if single prim space it linkset

        private float m_density;
        private byte m_shapetype;
        private byte m_fakeShapetype;
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

        internal UBOdeNative.Mass primdMass; // prim inertia information on it's own referencial
        private PhysicsInertiaData m_InertiaOverride;
        float primMass; // prim own mass
        float primVolume; // prim own volume;
        float m_mass; // object mass acording to case

        public int m_givefakepos;
        private Vector3 fakepos;
        public int m_givefakeori;
        private Quaternion fakeori;
        private PhysicsInertiaData m_fakeInertiaOverride;

        private int m_eventsubscription;
        private int m_cureventsubscription;
        private CollisionEventUpdate CollisionEvents = null;
        private CollisionEventUpdate CollisionVDTCEvents = null;
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_fakeisphysical; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_fakeisVolumeDetect; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                m_fakeisVolumeDetect = value;
                AddChange(changes.VolumeDtc, value);
            }
        }

        public override bool Phantom  // this is not reliable for internal use
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_fakeisphantom; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                m_fakeisphantom = value;
                AddChange(changes.Phantom, value);
            }
        }

        public override bool Building  // this is not reliable for internal use
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_building; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                //if (value)
                //    m_building = true;
                AddChange(changes.building, value);
            }
        }

        public override void getContactData(ref ContactData cdata)
        {
            cdata.mu = mu;
            cdata.bounce = bounce;

            //cdata.softcolide = m_softcolide;
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
                //    cdata.mu *= 0;
            }
        }

        public override float PhysicsCost
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_physCost;
            }
        }

        public override float StreamCost
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_streamCost;
            }
        }

        public override int PhysicsActorType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (int)ActorTypes.Prim; }
            set {}
        }

        public override bool SetAlwaysRun
        {
            get { return false; }
            set {}
        }

        public override uint LocalID
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_baseLocalID; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                uint oldid = m_baseLocalID;
                m_baseLocalID = value;
                m_parentScene.changePrimID(this, oldid);
            }
        }

        public override PhysicsActor ParentActor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (childPrim) ?  _parent : (PhysicsActor)this;
            }
        }

        public override bool Grabbed
        {
            set {}
        }

        public override bool Selected
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value)
                    m_isSelected = value; // if true set imediatly to stop moves etc
                AddChange(changes.Selected, value);
            }
        }

        public override bool Flying
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            // no flying prims for you
            get { return false; }
            set { }
        }

        public override bool IsColliding
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_iscolliding; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return false; }
            set {}
        }

        public override bool CollidingObj
        {
            get { return false; }
            set {}
        }


        public override bool ThrottleUpdates {get;set;}

        public override bool Stopped
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _zeroFlag; }
        }

        public override Vector3 Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (m_givefakepos > 0) ? fakepos : m_position;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                fakepos = value;
                m_givefakepos++;
                AddChange(changes.Position, value);
            }
        }

        public override Vector3 Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_size; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value.IsFinite())
                {
                     m_parentScene.m_meshWorker.ChangeActorPhysRep(this, m_pbs, value, m_fakeShapetype);
                }
                else
                {
                    m_log.WarnFormat("[PHYSICS]: Got NaN Size on object {0}", Name);
                }
            }
        }

        public override float Mass
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return primMass; }
        }

        public override Vector3 Force
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_force; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Vector3.Zero;
            }
        }

        public override PhysicsInertiaData GetInertiaData()
        {
            PhysicsInertiaData inertia;
            if(childPrim)
            {
                if(_parent != null)
                    return _parent.GetInertiaData();
                else
                {
                    inertia = new PhysicsInertiaData
                    {
                        TotalMass = -1
                    };
                    return inertia;
                }
            }

            inertia = new PhysicsInertiaData();

            // double buffering
            if(m_fakeInertiaOverride != null)
            { 
                UBOdeNative.Mass objdmass = new();
                objdmass.I.M00 = m_fakeInertiaOverride.Inertia.X;
                objdmass.I.M11 = m_fakeInertiaOverride.Inertia.Y;
                objdmass.I.M22 = m_fakeInertiaOverride.Inertia.Z;

                objdmass.mass = m_fakeInertiaOverride.TotalMass;
                    
                if(MathF.Abs(m_fakeInertiaOverride.InertiaRotation.W) < 0.999)
                {
                    UBOdeNative.Matrix3 inertiarotmat = new();
                    UBOdeNative.RfromQ(ref inertiarotmat, ref m_fakeInertiaOverride.InertiaRotation);
                    UBOdeNative.MassRotate(ref objdmass, ref inertiarotmat);
                }

                inertia.TotalMass = m_fakeInertiaOverride.TotalMass;
                inertia.CenterOfMass = m_fakeInertiaOverride.CenterOfMass;
                inertia.Inertia.X = objdmass.I.M00;
                inertia.Inertia.Y = objdmass.I.M11;
                inertia.Inertia.Z = objdmass.I.M22;
                inertia.InertiaRotation.X =  objdmass.I.M01;
                inertia.InertiaRotation.Y =  objdmass.I.M02;
                inertia.InertiaRotation.Z =  objdmass.I.M12;
                return inertia;
            }

            inertia.TotalMass = m_mass;

            if(Body == IntPtr.Zero || m_prim_geom == IntPtr.Zero)
            {
                inertia.CenterOfMass = Vector3.Zero;
                inertia.Inertia = Vector3.Zero;
                inertia.InertiaRotation =  Vector4.Zero;
                return inertia;
            }

            UBOdeNative.Vector3 dtmp;
            UBOdeNative.Mass m = new();
            lock(m_parentScene.OdeLock)
            {
                UBOdeNative.AllocateODEDataForThread(0);
                dtmp = UBOdeNative.GeomGetOffsetPosition(m_prim_geom);
                UBOdeNative.BodyGetMass(Body, out m);
            }

            Vector3 cm = new(-dtmp.X, -dtmp.Y, -dtmp.Z);
            inertia.CenterOfMass = cm;
            inertia.Inertia = new(m.I.M00, m.I.M11, m.I.M22);
            inertia.InertiaRotation = new(m.I.M01, m.I.M02 , m.I.M12, 0);

            return inertia;
        }

        public override void SetInertiaData(PhysicsInertiaData inertia)
        {
            if(childPrim)
            {
                if(_parent != null)
                    _parent.SetInertiaData(inertia);
                return;
            }

            if(inertia.TotalMass > 0)
                m_fakeInertiaOverride = new PhysicsInertiaData(inertia);
            else
                m_fakeInertiaOverride = null;

            if (inertia.TotalMass > m_parentScene.maximumMassObject)
                inertia.TotalMass = m_parentScene.maximumMassObject;
            AddChange(changes.SetInertia,(object)m_fakeInertiaOverride);
        }

        public override Vector3 CenterOfMass
        {
            get
            {
                lock (m_parentScene.OdeLock)
                {
                    UBOdeNative.AllocateODEDataForThread(0);

                    if (!childPrim && Body != IntPtr.Zero)
                    {
                        return UBOdeNative.BodyGetPositionOMV(Body);
                    }
                    else if (m_prim_geom != IntPtr.Zero)
                    {
                        Vector3 Ptot = UBOdeNative.GeomGetPositionOMV(m_prim_geom);
                        Quaternion q = UBOdeNative.GeomGetQuaternionOMV(m_prim_geom);
                        Ptot += m_OBBOffset * q;
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
                        return m_position;
                }
            }
        }

        public override PrimitiveBaseShape Shape
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                //AddChange(changes.Shape, value);
                m_parentScene.m_meshWorker.ChangeActorPhysRep(this, value, m_size, m_fakeShapetype);
            }
        }

        public override byte PhysicsShapeType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_fakeShapetype;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                m_fakeShapetype = value;
               m_parentScene.m_meshWorker.ChangeActorPhysRep(this, m_pbs, m_size, value);
            }
        }

        public override Vector3 rootVelocity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (_parent == null) ? Velocity : ((OdePrim)_parent).Velocity;
            }
        }

        public override Vector3 Velocity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (_zeroFlag) ? Vector3.Zero : _velocity;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value.IsFinite())
                {
                    if(m_outbounds)
                        _velocity = value;
                    else
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (!IsPhysical || Body == IntPtr.Zero) ? Vector3.Zero : m_torque;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_collisionscore; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { m_collisionscore = value; }
        }

        public override bool Kinematic
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return false; }
            set { }
        }

        public override Quaternion Orientation
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_givefakeori > 0 ? fakeori : m_orientation;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (QuaternionIsFinite(value))
                {
                    fakeori = value;
                    m_givefakeori++;

                    value.Normalize();
                    AddChange(changes.Orientation, value);
                }
                else
                    m_log.WarnFormat("[PHYSICS]: Got NaN quaternion Orientation from Scene in Object {0}", Name);

            }
        }

        public override Vector3 Acceleration
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_acceleration; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if(m_outbounds)
                    m_acceleration = value;
            }
        }

        public override Vector3 RotationalVelocity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _zeroFlag || m_rotationalVelocity.ApproxZero(0.001f) ? Vector3.Zero : m_rotationalVelocity;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value.IsFinite())
                {
                    if(m_outbounds)
                        m_rotationalVelocity = value;
                    else
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_buoyancy; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                 AddChange(changes.Buoyancy,value);
            }
        }

        public override Vector3 PIDTarget
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_usePID;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                AddChange(changes.PIDActive,value);
            }
        }

        public override float PIDTau
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value > 0)
                {
                    float mint = (0.05f > m_sceneTimeStep ? 0.05f : m_sceneTimeStep);
                    if (value < mint)
                        AddChange(changes.PIDTau, mint);
                    else
                        AddChange(changes.PIDTau, value);
                }
                else
                    AddChange(changes.PIDTau, 0);
            }
        }

        public override float PIDHoverHeight
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                AddChange(changes.PIDHoverHeight,value);
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
                AddChange(changes.PIDHoverType,value);
            }
        }

        public override float PIDHoverTau
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value > 0)
                {
                    float mint = (0.05f > m_sceneTimeStep ? 0.05f : m_sceneTimeStep);
                    if (value < mint)
                        AddChange(changes.PIDHoverTau, mint);
                    else
                        AddChange(changes.PIDHoverTau, value);
                }
                else
                    AddChange(changes.PIDHoverTau, 0);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void VehicleFloatParam(int param, float value)
        {
            strVehicleFloatParam fp = new()
            {
                param = param,
                value = value
            };
            AddChange(changes.VehicleFloatParam, fp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void VehicleVectorParam(int param, Vector3 value)
        {
            strVehicleVectorParam fp = new()
            {
                param = param,
                value = value
            };
            AddChange(changes.VehicleVectorParam, fp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void VehicleRotationParam(int param, Quaternion value)
        {
            strVehicleQuatParam fp = new()
            {
                param = param,
                value = value
            };
            AddChange(changes.VehicleRotationParam, fp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void VehicleFlags(int param, bool value)
        {
            strVehicleBoolParam bp = new()
            {
                param = param,
                value = value
            };
            AddChange(changes.VehicleFlags, bp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SetVehicle(object vdata)
        {
            AddChange(changes.SetVehicle, vdata);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAcceleration(Vector3 accel)
        {
            m_acceleration = accel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AvatarJump(float forceZ) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AddForce(Vector3 force, bool pushforce)
        {
            if (force.IsFinite())
            {
                if(pushforce)
                    AddChange(changes.AddForce, force);
                else // a impulse
                    AddChange(changes.AddForce, force * m_sceneInverseTimeStep);
            }
            else
            {
                m_log.WarnFormat("[PHYSICS]: Got Invalid linear force vector from Scene in Object {0}", Name);
            }
            //m_log.Info("[PHYSICS]: Added Force:" + force.ToString() +  " to prim at " + Position.ToString());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AddAngularForce(Vector3 force, bool pushforce)
        {
            if (force.IsFinite())
            {
                //if(pushforce)  for now applyrotationimpulse seems more happy applied as a force
                    AddChange(changes.AddAngForce, force);
                //else // a impulse
                    //AddChange(changes.AddAngForce, force * m_invTimeStep);
            }
            else
            {
                m_log.WarnFormat("[PHYSICS]: Got Invalid Angular force vector from Scene in Object {0}", Name);
            }
        }

        public override void CrossingFailure()
        {
            lock(m_parentScene.OdeLock)
            {
                if (m_outbounds)
                {
                    m_position.X = Utils.Clamp(m_position.X, 0.5f, m_parentScene.WorldExtents.X - 0.5f);
                    m_position.Y = Utils.Clamp(m_position.Y, 0.5f, m_parentScene.WorldExtents.Y - 0.5f);
                    m_position.Z = Utils.Clamp(m_position.Z + 0.2f, Constants.MinSimulationHeight, Constants.MaxSimulationHeight);

                    m_lastposition = m_position;
                    _velocity = Vector3.Zero;

                    UBOdeNative.AllocateODEDataForThread(0);

                    m_lastVelocity = _velocity;
                    if (m_vehicle != null && m_vehicle.Type != Vehicle.TYPE_NONE)
                        m_vehicle.Stop();

                    if(Body != IntPtr.Zero)
                        UBOdeNative.BodySetLinearVel(Body, 0, 0, 0); // stop it
                    if (m_prim_geom != IntPtr.Zero)
                        UBOdeNative.GeomSetPosition(m_prim_geom, m_position.X, m_position.Y, m_position.Z);

                    m_outbounds = false;
                    changeDisable(false);
                    base.RequestPhysicsterseUpdate();
                }
            }
        }

        public override void CrossingStart()
        {
            lock(m_parentScene.OdeLock)
            {
                if (m_outbounds || childPrim)
                    return;

                m_outbounds = true;

                m_lastposition = m_position;
                m_lastorientation = m_orientation;

                UBOdeNative.AllocateODEDataForThread(0);
                if(Body != IntPtr.Zero)
                {
                    m_rotationalVelocity = UBOdeNative.BodyGetAngularVelOMV(Body);
                    _velocity = UBOdeNative.BodyGetLinearVelOMV(Body);

                    UBOdeNative.BodySetLinearVel(Body, 0, 0, 0); // stop it
                    UBOdeNative.BodySetAngularVel(Body, 0, 0, 0);
                }
                if(m_prim_geom != IntPtr.Zero)
                    UBOdeNative.GeomSetPosition(m_prim_geom, m_position.X, m_position.Y, m_position.Z);
                disableBodySoft(); // stop collisions
                UnSubscribeEvents();
            }
        }

        public override void SetMomentum(Vector3 momentum)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SetMaterial(int pMaterial)
        {
            m_material = pMaterial;
            mu = m_parentScene.m_materialContactsData[pMaterial].mu;
            bounce = m_parentScene.m_materialContactsData[pMaterial].bounce;
        }

        public override float Density
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_density * 100f;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                //float old = m_density;
                m_density = value / 100f;
                //if(m_density != old)
                //    UpdatePrimBodyData();
            }
        }
        public override float GravModifier
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_gravmod;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                m_gravmod = value;
                if (m_vehicle != null)
                    m_vehicle.GravMod = m_gravmod;
            }
        }
        public override float Friction
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return mu;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                mu = value;
            }
        }

        public override float Restitution
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return bounce;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                bounce = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void setPrimForRemoval()
        {
            AddChange(changes.Remove, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void link(PhysicsActor obj)
        {
            AddChange(changes.Link, obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void delink()
        {
            AddChange(changes.DeLink, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void LockAngularMotion(byte axislock)
        {
            //m_log.DebugFormat("[axislock]: <{0},{1},{2}>", axis.X, axis.Y, axis.Z);
            AddChange(changes.AngLock, axislock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SubscribeEvents(int ms)
        {
            m_eventsubscription = ms;
            m_cureventsubscription = 0;
            CollisionEvents ??= new CollisionEventUpdate();
            CollisionVDTCEvents ??= new CollisionEventUpdate();
            SentEmptyCollisionsEvent = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void UnSubscribeEvents()
        {
            if (CollisionVDTCEvents != null)
            {
                CollisionVDTCEvents.Clear();
                CollisionVDTCEvents = null;
            }
            if (CollisionEvents != null)
            {
                CollisionEvents.Clear();
                CollisionEvents = null;
            }
            m_eventsubscription = 0;
           m_parentScene.RemoveCollisionEventReporting(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AddCollisionEvent(uint CollidedWith, ContactPoint contact)
        {
            CollisionEvents ??= new CollisionEventUpdate();

            CollisionEvents.AddCollider(CollidedWith, contact);
            m_parentScene.AddCollisionEventReporting(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AddVDTCCollisionEvent(uint CollidedWith, ContactPoint contact)
        {
            CollisionVDTCEvents ??= new CollisionEventUpdate();

            CollisionVDTCEvents.AddCollider(CollidedWith, contact);
            m_parentScene.AddCollisionEventReporting(this);
        }

        internal void SleeperAddCollisionEvents()
        {
            if(CollisionEvents != null && CollisionEvents.m_objCollisionList.Count != 0)
            {
                foreach(KeyValuePair<uint,ContactPoint> kvp in CollisionEvents.m_objCollisionList)
                {
                    if(kvp.Key == 0)
                        continue;
                    OdePrim other = m_parentScene.getPrim(kvp.Key);
                    if(other == null)
                        continue;
                    ContactPoint cp = kvp.Value;
                    cp.SurfaceNormal = - cp.SurfaceNormal;
                    cp.RelativeSpeed = -cp.RelativeSpeed;
                    other.AddCollisionEvent(ParentActor.m_baseLocalID, cp);
                }
            }
            if(CollisionVDTCEvents != null && CollisionVDTCEvents.m_objCollisionList.Count != 0)
            {
                foreach(KeyValuePair<uint,ContactPoint> kvp in CollisionVDTCEvents.m_objCollisionList)
                {
                    OdePrim other = m_parentScene.getPrim(kvp.Key);
                    if(other == null)
                        continue;
                    ContactPoint cp = kvp.Value;
                    cp.SurfaceNormal = - cp.SurfaceNormal;
                    cp.RelativeSpeed = -cp.RelativeSpeed;
                    other.AddCollisionEvent(ParentActor.m_baseLocalID, cp);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void clearSleeperCollisions()
        {
            if(CollisionVDTCEvents != null && CollisionVDTCEvents.Count >0 )
                CollisionVDTCEvents.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendCollisions(int timestep)
        {
            if (m_cureventsubscription < 50000)
                m_cureventsubscription += timestep;


            if (m_cureventsubscription < m_eventsubscription)
                return;

            if (CollisionEvents == null)
                return;

            int ncolisions = CollisionEvents.m_objCollisionList.Count;

            if (!SentEmptyCollisionsEvent || ncolisions > 0)
            {
                base.SendCollisionUpdate(CollisionEvents);
                m_cureventsubscription = 0;

                if (ncolisions == 0)
                {
                    SentEmptyCollisionsEvent = true;
                    //_parent_scene.RemoveCollisionEventReporting(this);
                }
                else if(Body == IntPtr.Zero || (UBOdeNative.BodyIsEnabled(Body) && m_bodydisablecontrol >= 0 ))
                {
                    SentEmptyCollisionsEvent = false;
                    CollisionEvents.Clear();
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

        public OdePrim(String primName, ODEScene parent_scene, Vector3 pos, Vector3 size,
                       Quaternion rotation, PrimitiveBaseShape pbs, bool pisPhysical,bool pisPhantom,byte _shapeType,uint plocalID)
        {
            m_parentScene = parent_scene;

            Name = primName;
            m_baseLocalID = plocalID;

            m_vehicle = null;

            if (!pos.IsFinite())
            {
                pos = new Vector3(((float)Constants.RegionSize * 0.5f), ((float)Constants.RegionSize * 0.5f),
                    parent_scene.GetTerrainHeightAtXY(((float)Constants.RegionSize * 0.5f), ((float)Constants.RegionSize * 0.5f)) + 0.5f);
                m_log.WarnFormat("[PHYSICS]: Got nonFinite Object create Position for {0}", Name);
            }
            m_position = pos;
            m_givefakepos = 0;

            m_sceneTimeStep = parent_scene.ODE_STEPSIZE;
            m_sceneInverseTimeStep = 1f / m_sceneTimeStep;

            m_density = parent_scene.geomDefaultDensity;
            m_body_autodisable_frames = parent_scene.bodyFramesAutoDisable;

            m_prim_geom = IntPtr.Zero;
            m_collide_geom = IntPtr.Zero;
            Body = IntPtr.Zero;

            if (!size.IsFinite())
            {
                size = new Vector3(0.5f, 0.5f, 0.5f);
                m_log.WarnFormat("[PHYSICS]: Got nonFinite Object create Size for {0}", Name);
            }

            m_size.X = (size.X <= 0) ? 0.01f : size.X;
            m_size.Y = (size.Y <= 0) ? 0.01f : size.Y;
            m_size.Z = (size.Z <= 0) ? 0.01f : size.Z;

            if (!QuaternionIsFinite(rotation))
            {
                rotation = Quaternion.Identity;
                m_log.WarnFormat("[PHYSICS]: Got nonFinite Object create Rotation for {0}", Name);
            }

            m_orientation = rotation;
            m_givefakeori = 0;

            m_pbs = pbs;

            m_targetSpace = IntPtr.Zero;

            m_isphysical = pos.Z >= Constants.MinSimulationHeight && pos.Z <= Constants.MaxSimulationHeight && pisPhysical;
            m_fakeisphysical = m_isphysical;

            m_isVolumeDetect = false;
            m_fakeisVolumeDetect = false;

            m_force = Vector3.Zero;

            m_iscolliding = false;
            m_colliderfilter = 0;
            m_NoColide = false;

            _triMeshData = IntPtr.Zero;

            m_fakeShapetype = _shapeType;

            m_lastdoneSelected = false;
            m_isSelected = false;
            m_delaySelect = false;

            m_isphantom = pisPhantom;
            m_fakeisphantom = pisPhantom;

            mu = parent_scene.m_materialContactsData[(int)Material.Wood].mu;
            bounce = parent_scene.m_materialContactsData[(int)Material.Wood].bounce;

            m_building = true; // control must set this to false when done

            AddChange(changes.Add, null);

            // get basic mass parameters
            ODEPhysRepData repData = m_parentScene.m_meshWorker.NewActorPhysRep(this, m_pbs, m_size, _shapeType);

            primVolume = repData.volume;
            m_OBB = repData.OBB;
            m_OBBOffset = repData.OBBOffset;

            UpdatePrimBodyData();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            if (m_prim_geom != IntPtr.Zero)
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

                        if (prm.m_prim_geom != IntPtr.Zero)
                        {
                            if (prm.m_NoColide)
                            {
                                UBOdeNative.GeomSetCategoryBits(prm.m_prim_geom, 0);
                                if (m_isphysical)
                                    UBOdeNative.GeomSetCollideBits(prm.m_prim_geom, (int)CollisionCategories.Land);
                                else
                                    UBOdeNative.GeomSetCollideBits(prm.m_prim_geom, 0);
                            }
                            else
                            {
                                UBOdeNative.GeomSetCategoryBits(prm.m_prim_geom, (uint)prm.m_collisionCategories);
                                UBOdeNative.GeomSetCollideBits(prm.m_prim_geom, (uint)prm.m_collisionFlags);
                            }
                        }
                    }
                }

                if (m_NoColide)
                {
                    UBOdeNative.GeomSetCategoryBits(m_prim_geom, 0);
                    UBOdeNative.GeomSetCollideBits(m_prim_geom, (uint)CollisionCategories.Land);
                    if (m_collide_geom != m_prim_geom && m_collide_geom != IntPtr.Zero)
                    {
                        UBOdeNative.GeomSetCategoryBits(m_collide_geom, 0);
                        UBOdeNative.GeomSetCollideBits(m_collide_geom, (uint)CollisionCategories.Land);
                    }
                }
                else
                {
                    UBOdeNative.GeomSetCategoryBits(m_prim_geom, (uint)m_collisionCategories);
                    UBOdeNative.GeomSetCollideBits(m_prim_geom, (uint)m_collisionFlags);
                    if (m_collide_geom != m_prim_geom && m_collide_geom != IntPtr.Zero)
                    {
                        UBOdeNative.GeomSetCategoryBits(m_collide_geom, (uint)m_collisionCategories);
                        UBOdeNative.GeomSetCollideBits(m_collide_geom, (uint)m_collisionFlags);
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
                UBOdeNative.JointDestroy(Amotor);
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
            UBOdeNative.BodySetTorque(Body, 0, 0, 0);
            UBOdeNative.BodySetAngularVel(Body, 0, 0, 0);

            Amotor = UBOdeNative.JointCreateAMotor(m_parentScene.world, IntPtr.Zero);
            UBOdeNative.JointAttach(Amotor, Body, IntPtr.Zero);

            UBOdeNative.JointSetAMotorMode(Amotor, 0);

            UBOdeNative.JointSetAMotorNumAxes(Amotor, axisnum);

            // get current orientation to lock

            Quaternion curr= UBOdeNative.BodyGetQuaternionOMV(Body);
            Vector3 ax;

            int i = 0;
            int j = 0;
            if (axisX)
            {
                ax = (new Vector3(1, 0, 0)) * curr; // rotate world X to current local X
                UBOdeNative.JointSetAMotorAxis(Amotor, 0, 0, ax.X, ax.Y, ax.Z);
                UBOdeNative.JointSetAMotorAngle(Amotor, 0, 0);
                UBOdeNative.JointSetAMotorParam(Amotor, (int)UBOdeNative.JointParam.LoStop, 0f);
                UBOdeNative.JointSetAMotorParam(Amotor, (int)UBOdeNative.JointParam.HiStop, 0f);
                UBOdeNative.JointSetAMotorParam(Amotor, (int)UBOdeNative.JointParam.Vel, 0);
                UBOdeNative.JointSetAMotorParam(Amotor, (int)UBOdeNative.JointParam.FudgeFactor, 0.0001f);
                UBOdeNative.JointSetAMotorParam(Amotor, (int)UBOdeNative.JointParam.Bounce, 0f);
                UBOdeNative.JointSetAMotorParam(Amotor, (int)UBOdeNative.JointParam.CFM, 0f);
                UBOdeNative.JointSetAMotorParam(Amotor, (int)UBOdeNative.JointParam.FMax, 5e8f);
                UBOdeNative.JointSetAMotorParam(Amotor, (int)UBOdeNative.JointParam.StopCFM, 0f);
                UBOdeNative.JointSetAMotorParam(Amotor, (int)UBOdeNative.JointParam.StopERP, 0.8f);
                i++;
                j = 256; // move to next axis set
            }

            if (axisY)
            {
                ax = (new Vector3(0, 1, 0)) * curr;
                UBOdeNative.JointSetAMotorAxis(Amotor, i, 0, ax.X, ax.Y, ax.Z);
                UBOdeNative.JointSetAMotorAngle(Amotor, i, 0);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.LoStop, 0f);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.HiStop, 0f);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.Vel, 0);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.FudgeFactor, 0.0001f);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.Bounce, 0f);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.CFM, 0f);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.FMax, 5e8f);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.StopCFM, 0f);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.StopERP, 0.8f);
                i++;
                j += 256;
            }

            if (axisZ)
            {
                ax = (new Vector3(0, 0, 1)) * curr;
                UBOdeNative.JointSetAMotorAxis(Amotor, i, 0, ax.X, ax.Y, ax.Z);
                UBOdeNative.JointSetAMotorAngle(Amotor, i, 0);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.LoStop, 0f);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.HiStop, 0f);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.Vel, 0);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.FudgeFactor, 0.0001f);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.Bounce, 0f);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.CFM, 0f);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.FMax, 5e8f);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.StopCFM, 0f);
                UBOdeNative.JointSetAMotorParam(Amotor, j + (int)UBOdeNative.JointParam.StopERP, 0.8f);
            }
        }


        private void SetGeom(IntPtr geom)
        {
            m_prim_geom = geom;
            //Console.WriteLine("SetGeom to " + prim_geom + " for " + Name);
            if (m_prim_geom != IntPtr.Zero)
            {

                if (m_NoColide)
                {
                    UBOdeNative.GeomSetCategoryBits(m_prim_geom, 0);
                    if (m_isphysical)
                    {
                        UBOdeNative.GeomSetCollideBits(m_prim_geom, (uint)CollisionCategories.Land);
                    }
                    else
                    {
                        UBOdeNative.GeomSetCollideBits(m_prim_geom, 0);
                        UBOdeNative.GeomDisable(m_prim_geom);
                    }
                }
                else
                {
                    UBOdeNative.GeomSetCategoryBits(m_prim_geom, (uint)m_collisionCategories);
                    UBOdeNative.GeomSetCollideBits(m_prim_geom, (uint)m_collisionFlags);
                }

                UpdatePrimBodyData();
                m_parentScene.actor_name_map[m_prim_geom] = this;

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
            IMesh mesh = m_mesh;
            if (mesh is null)
                return false;

            mesh.getVertexListAsPtrToFloatArray(out IntPtr vertices, out int vertexStride, out int vertexCount);
            mesh.getIndexListAsPtrToIntArray(out IntPtr indices, out int triStride, out int indexCount);

            if (vertexCount == 0 || indexCount == 0)
            {
                m_log.WarnFormat("[PHYSICS]: Invalid mesh data on OdePrim {0}, mesh {1} at {2}",
                    Name, m_pbs.SculptEntry ? m_pbs.SculptTexture.ToString() : "primMesh", m_position.ToString());

                m_hasOBB = false;
                m_OBBOffset = Vector3.Zero;
                m_OBB = m_size * 0.5f;

                m_physCost = 0.1f;
                m_streamCost = 1.0f;

                m_parentScene.mesher.ReleaseMesh(mesh);
                m_meshState = MeshState.MeshFailed;
                m_mesh = null;
                return false;
            }

            if (vertexCount > 64000 || indexCount > 64000)
            {
                m_log.WarnFormat("[PHYSICS]: large mesh data on OdePrim {0}, mesh {1} at {2}, {3} vertices, {4} indexes",
                    Name, m_pbs.SculptEntry ? m_pbs.SculptTexture.ToString() : "primMesh",
                    m_position.ToString() ,vertexCount , indexCount );
            }
            IntPtr geo = IntPtr.Zero;

            try
            {
                _triMeshData = UBOdeNative.GeomTriMeshDataCreate();

                UBOdeNative.GeomTriMeshDataBuildSimple(_triMeshData, vertices, vertexStride, vertexCount, indices, indexCount, triStride);
                UBOdeNative.GeomTriMeshDataPreprocess(_triMeshData);

                geo = UBOdeNative.CreateTriMesh(m_targetSpace, _triMeshData, null, null, null);
            }

            catch (Exception e)
            {
                m_log.ErrorFormat("[PHYSICS]: SetGeom Mesh failed for {0} exception: {1}", Name, e);
                if (_triMeshData != IntPtr.Zero)
                {
                    try
                    {
                        UBOdeNative.GeomTriMeshDataDestroy(_triMeshData);
                    }
                    catch
                    {
                    }
                }
                _triMeshData = IntPtr.Zero;

                m_hasOBB = false;
                m_OBBOffset = Vector3.Zero;
                m_OBB = m_size * 0.5f;
                m_physCost = 0.1f;
                m_streamCost = 1.0f;

                m_parentScene.mesher.ReleaseMesh(mesh);
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

        private void CreateGeom(bool OverrideToBox)
        {
            bool hasMesh = false;

            m_NoColide = false;

            if ((m_meshState & MeshState.MeshNoColide) != 0)
                m_NoColide = true;

            else if(!OverrideToBox && m_mesh != null)
            {
                if (GetMeshGeom())
                    hasMesh = true;
                else
                    m_NoColide = true;
            }


            if (!hasMesh)
            {
                IntPtr geo;

                if (m_pbs.ProfileShape == ProfileShape.HalfCircle && m_pbs.PathCurve == (byte)Extrusion.Curve1
                    && m_size.X == m_size.Y && m_size.Y == m_size.Z)
                { // it's a sphere
                    try
                    {
                        geo = UBOdeNative.CreateSphere(m_targetSpace, m_size.X * 0.5f);
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
                        geo = UBOdeNative.CreateBox(m_targetSpace, m_size.X, m_size.Y, m_size.Z);
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
            if (m_prim_geom != IntPtr.Zero)
            {
                m_parentScene.actor_name_map.Remove(m_prim_geom);

                try
                {
                    UBOdeNative.GeomDestroy(m_prim_geom);
                    if (_triMeshData != IntPtr.Zero)
                    {
                        UBOdeNative.GeomTriMeshDataDestroy(_triMeshData);
                        _triMeshData = IntPtr.Zero;
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[PHYSICS]: PrimGeom destruction failed for {0} exception {1}", Name, e);
                }

                m_prim_geom = IntPtr.Zero;
                m_collide_geom = IntPtr.Zero;
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
                    m_parentScene.mesher.ReleaseMesh(m_mesh);
                    m_mesh = null;
                }
            }

            Body = IntPtr.Zero;
            m_hasOBB = false;
        }

        //sets non physical prim m_targetSpace to right space in spaces grid for static prims
        // should only be called for non physical prims unless they are becoming non physical
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetInStaticSpace(OdePrim prim)
        {
            IntPtr targetSpace = m_parentScene.MoveGeomToStaticSpace(prim.m_prim_geom, prim.m_targetSpace);
            prim.m_targetSpace = targetSpace;
            m_collide_geom = IntPtr.Zero;
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
                    UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                    UBOdeNative.BodyEnable(Body);
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
                    UBOdeNative.BodyDisable(Body);
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

            if (m_prim_geom == IntPtr.Zero)
            {
                m_log.Warn("[PHYSICS]: Unable to link the linkset.  Root has no geom yet");
                return;
            }

            if (Body != IntPtr.Zero)
            {
                DestroyBody();
                m_log.Warn("[PHYSICS]: MakeBody called having a body");
            }

            if (UBOdeNative.GeomGetBody(m_prim_geom) != IntPtr.Zero)
            {
                UBOdeNative.GeomSetBody(m_prim_geom, IntPtr.Zero);
                m_log.Warn("[PHYSICS]: MakeBody root geom already had a body");
            }

            bool noInertiaOverride = (m_InertiaOverride == null);

            Body = UBOdeNative.BodyCreate(m_parentScene.world);

            // set the body rotation
            UBOdeNative.Matrix3 mymat = new();
            UBOdeNative.RfromQ(ref mymat, ref m_orientation);
            UBOdeNative.BodySetRotation(Body, ref mymat);

            UBOdeNative.Mass objdmass = new();


            if (noInertiaOverride)
            {
                objdmass = primdMass;
                UBOdeNative.MassRotate(ref objdmass, ref mymat);
            }
    
            // recompute full object inertia if needed
            if (childrenPrim.Count > 0)
            {
                UBOdeNative.Matrix3 mat = new();
                UBOdeNative.Mass tmpdmass;
                Vector3 rcm;

                rcm = m_position;

                lock (childrenPrim)
                {
                    foreach (OdePrim prm in childrenPrim)
                    {
                        if (prm.m_prim_geom == IntPtr.Zero)
                        {
                            m_log.Warn("[PHYSICS]: Unable to link one of the linkset elements, skipping it.  No geom yet");
                            continue;
                        }

                        UBOdeNative.RfromQ(ref mat, ref prm.m_orientation);

                        // fix prim colision cats

                        if (UBOdeNative.GeomGetBody(prm.m_prim_geom) != IntPtr.Zero)
                        {
                            UBOdeNative.GeomSetBody(prm.m_prim_geom, IntPtr.Zero);
                            m_log.Warn("[PHYSICS]: MakeBody child geom already had a body");
                        }

                        UBOdeNative.GeomClearOffset(prm.m_prim_geom);
                        UBOdeNative.GeomSetBody(prm.m_prim_geom, Body);
                        prm.Body = Body;
                        UBOdeNative.GeomSetOffsetWorldRotation(prm.m_prim_geom, ref mat); // set relative rotation

                        if(noInertiaOverride)
                        {
                            tmpdmass = prm.primdMass;

                            UBOdeNative.MassRotate(ref tmpdmass, ref mat);
                            Vector3 ppos = prm.m_position;
                            ppos -= rcm;
                            // refer inertia to root prim center of mass position
                            UBOdeNative.MassTranslate(ref tmpdmass,
                                ppos.X,
                                ppos.Y,
                                ppos.Z);

                            UBOdeNative.MassAdd(ref objdmass, ref tmpdmass); // add to total object inertia
                        }
                    }
                }
            }

            UBOdeNative.GeomClearOffset(m_prim_geom); // make sure we don't have a hidden offset
            // associate root geom with body
            UBOdeNative.GeomSetBody(m_prim_geom, Body);

            if(noInertiaOverride)
                UBOdeNative.BodySetPosition(Body, m_position.X + objdmass.c.X, m_position.Y + objdmass.c.Y, m_position.Z + objdmass.c.Z);
            else
            {
                Vector3 ncm =  m_InertiaOverride.CenterOfMass * m_orientation;
                UBOdeNative.BodySetPosition(Body,
                    m_position.X + ncm.X,
                    m_position.Y + ncm.Y,
                    m_position.Z + ncm.Z);
            }

            UBOdeNative.GeomSetOffsetWorldPosition(m_prim_geom, m_position.X, m_position.Y, m_position.Z);

            if(noInertiaOverride)
            {
                UBOdeNative.MassTranslate(ref objdmass, -objdmass.c.X, -objdmass.c.Y, -objdmass.c.Z); // ode wants inertia at center of body
                Quaternion mr = Quaternion.Conjugate(m_orientation);

                UBOdeNative.RfromQ(ref mymat, ref mr);
                UBOdeNative.MassRotate(ref objdmass, ref mymat);

                UBOdeNative.BodySetMass(Body, ref objdmass);
                m_mass = objdmass.mass;
            }
            else
            {
                objdmass.c.X = 0;
                objdmass.c.Y = 0;
                objdmass.c.Z = 0;

                objdmass.I.M00 = m_InertiaOverride.Inertia.X;
                objdmass.I.M11 = m_InertiaOverride.Inertia.Y;
                objdmass.I.M22 = m_InertiaOverride.Inertia.Z;

                objdmass.mass = m_InertiaOverride.TotalMass;

                if(MathF.Abs(m_InertiaOverride.InertiaRotation.W) < 0.999f)
                {
                    UBOdeNative.Matrix3 inertiarotmat = new();
                    UBOdeNative.RfromQ(ref inertiarotmat, ref m_InertiaOverride.InertiaRotation);
                    UBOdeNative.MassRotate(ref objdmass, ref inertiarotmat);
                }
                UBOdeNative.BodySetMass(Body, ref objdmass);

                m_mass = objdmass.mass;
            }

            // disconnect from world gravity so we can apply buoyancy
            UBOdeNative.BodySetGravityMode(Body, false);

            UBOdeNative.BodySetAutoDisableFlag(Body, true);
            UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
            UBOdeNative.BodySetAutoDisableAngularThreshold(Body, 0.001f);
            UBOdeNative.BodySetAutoDisableLinearThreshold(Body, 0.01f);
            UBOdeNative.BodySetDamping(Body, .002f, .0005f);

            if (m_targetSpace != IntPtr.Zero)
            {
                //m_parentScene.waitForSpaceUnlock(m_targetSpace);
                if (UBOdeNative.SpaceQuery(m_targetSpace, m_prim_geom))
                    UBOdeNative.SpaceRemove(m_targetSpace, m_prim_geom);
            }

            if (childrenPrim.Count == 0)
            {
                m_collide_geom = m_prim_geom;
                m_targetSpace = m_parentScene.ActiveSpace;
            }
            else
            {
                m_targetSpace = UBOdeNative.SimpleSpaceCreate(m_parentScene.ActiveSpace);
                UBOdeNative.SpaceSetSublevel(m_targetSpace, 0);
                UBOdeNative.SpaceSetCleanup(m_targetSpace, false);

                UBOdeNative.GeomSetCategoryBits(m_targetSpace, (uint)(CollisionCategories.Space |
                                                            CollisionCategories.Geom |
                                                            CollisionCategories.Phantom |
                                                            CollisionCategories.VolumeDtc
                                                            ));
                UBOdeNative.GeomSetCollideBits(m_targetSpace, 0);
                m_collide_geom = m_targetSpace;
            }

            if (UBOdeNative.SpaceQuery(m_targetSpace, m_prim_geom))
                m_log.Debug("[PRIM]: parent already in target space");
            else
                UBOdeNative.SpaceAdd(m_targetSpace, m_prim_geom);

            if (m_delaySelect)
            {
                m_isSelected = true;
                m_delaySelect = false;
            }

            m_collisionscore = 0;

            UpdateCollisionCatFlags();
            ApplyCollisionCatFlags();

            m_parentScene.addActivePrim(this);

            lock (childrenPrim)
            {
                foreach (OdePrim prm in childrenPrim)
                {
                    IntPtr prmgeom = prm.m_prim_geom;
                    if (prmgeom == IntPtr.Zero)
                        continue;

                    Vector3 ppos = prm.m_position;
                    UBOdeNative.GeomSetOffsetWorldPosition(prm.m_prim_geom, ppos.X, ppos.Y, ppos.Z); // set relative position

                    IntPtr prmspace = prm.m_targetSpace;
                    if (prmspace != m_targetSpace)
                    {
                        if (prmspace != IntPtr.Zero)
                        {
                            //m_parentScene.waitForSpaceUnlock(prmspace);
                            if (UBOdeNative.SpaceQuery(prmspace, prmgeom))
                                UBOdeNative.SpaceRemove(prmspace, prmgeom);
                        }
                        prm.m_targetSpace = m_targetSpace;
                        if (UBOdeNative.SpaceQuery(m_targetSpace, prmgeom))
                            m_log.Debug("[PRIM]: child already in target space");
                        else
                            UBOdeNative.SpaceAdd(m_targetSpace, prmgeom);
                    }

                    prm.m_collisionscore = 0;

                    if(!m_disabled)
                        prm.m_disabled = false;

                    m_parentScene.addActivePrim(prm);
                }
            }

            // The body doesn't already have a finite rotation mode set here
            if (m_angularlocks != 0 && _parent == null)
            {
                createAMotor(m_angularlocks);
            }

            if (m_isSelected || m_disabled)
            {
                UBOdeNative.BodyDisable(Body);
                _zeroFlag = true;
            }
            else
            {
                UBOdeNative.BodySetAngularVel(Body, m_rotationalVelocity.X, m_rotationalVelocity.Y, m_rotationalVelocity.Z);
                UBOdeNative.BodySetLinearVel(Body, _velocity.X, _velocity.Y, _velocity.Z);

                _zeroFlag = false;
                m_bodydisablecontrol = 0;
            }
            m_parentScene.addActiveGroups(this);
        }

        private void DestroyBody()
        {
            if (Body != IntPtr.Zero)
            {
                m_parentScene.remActivePrim(this);

                m_collide_geom = IntPtr.Zero;

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

                if (m_prim_geom != IntPtr.Zero)
                {
                    if (m_NoColide)
                    {
                        UBOdeNative.GeomSetCategoryBits(m_prim_geom, 0);
                        UBOdeNative.GeomSetCollideBits(m_prim_geom, 0);
                    }
                    else
                    {
                        UBOdeNative.GeomSetCategoryBits(m_prim_geom, (uint)m_collisionCategories);
                        UBOdeNative.GeomSetCollideBits(m_prim_geom, (uint)m_collisionFlags);
                    }
                    UpdateDataFromGeom();
                    UBOdeNative.GeomSetBody(m_prim_geom, IntPtr.Zero);
                    SetInStaticSpace(this);
                }

                if (!childPrim)
                {
                    lock (childrenPrim)
                    {
                        foreach (OdePrim prm in childrenPrim)
                        {
                            m_parentScene.remActivePrim(prm);

                            if (prm.m_isSelected)
                                prm.m_collisionCategories = CollisionCategories.Selected;
                            else if (prm.m_isVolumeDetect)
                                prm.m_collisionCategories = CollisionCategories.VolumeDtc;
                            else if (prm.m_isphantom)
                                prm.m_collisionCategories = CollisionCategories.Phantom;
                            else
                                prm.m_collisionCategories = CollisionCategories.Geom;

                            prm.m_collisionFlags = 0;

                            if (prm.m_prim_geom != IntPtr.Zero)
                            {
                                if (prm.m_NoColide)
                                {
                                    UBOdeNative.GeomSetCategoryBits(prm.m_prim_geom, 0);
                                    UBOdeNative.GeomSetCollideBits(prm.m_prim_geom, 0);
                                }
                                else
                                {
                                    UBOdeNative.GeomSetCategoryBits(prm.m_prim_geom, (uint)prm.m_collisionCategories);
                                    UBOdeNative.GeomSetCollideBits(prm.m_prim_geom, (uint)prm.m_collisionFlags);
                                }
                                prm.UpdateDataFromGeom();
                                SetInStaticSpace(prm);
                            }
                            prm.Body = IntPtr.Zero;
                            prm.m_mass = prm.primMass;
                            prm.m_collisionscore = 0;
                        }
                    }
                    if (Amotor != IntPtr.Zero)
                    {
                        UBOdeNative.JointDestroy(Amotor);
                        Amotor = IntPtr.Zero;
                    }
                    m_parentScene.remActiveGroup(this);
                    UBOdeNative.BodyDestroy(Body);
                }
                Body = IntPtr.Zero;
            }
            m_mass = primMass;
            m_collisionscore = 0;
        }

        private void FixInertia(Vector3 NewPos,Quaternion newrot)
        {
            UBOdeNative.BodyGetMass(Body, out UBOdeNative.Mass tmpdmass);
            UBOdeNative.Mass objdmass = tmpdmass;

            UBOdeNative.Vector3 dobjpos;
            UBOdeNative.Vector3 thispos;

            // get current object position and rotation
            dobjpos = UBOdeNative.BodyGetPosition(Body);

            // get prim own inertia in its local frame
            tmpdmass = primdMass;

            // transform to object frame
            UBOdeNative.Matrix3 mat = UBOdeNative.GeomGetOffsetRotation(m_prim_geom);
            UBOdeNative.MassRotate(ref tmpdmass, ref mat);

            thispos = UBOdeNative.GeomGetOffsetPosition(m_prim_geom);
            UBOdeNative.MassTranslate(ref tmpdmass,
                            thispos.X,
                            thispos.Y,
                            thispos.Z);

            // subtract current prim inertia from object
            DMassSubPartFromObj(ref tmpdmass, ref objdmass);

            // back prim own inertia
            tmpdmass = primdMass;

            // update to new position and orientation
            m_position = NewPos;
            UBOdeNative.GeomSetOffsetWorldPosition(m_prim_geom, NewPos.X, NewPos.Y, NewPos.Z);
            m_orientation = newrot;
            UBOdeNative.Quaternion quat = new()
            {
                X = newrot.X,
                Y = newrot.Y,
                Z = newrot.Z,
                W = newrot.W
            };
            UBOdeNative.GeomSetOffsetWorldQuaternion(m_prim_geom, ref quat);

            mat = UBOdeNative.GeomGetOffsetRotation(m_prim_geom);
            UBOdeNative.MassRotate(ref tmpdmass, ref mat);

            thispos = UBOdeNative.GeomGetOffsetPosition(m_prim_geom);
            UBOdeNative.MassTranslate(ref tmpdmass,
                            thispos.X,
                            thispos.Y,
                            thispos.Z);

            UBOdeNative.MassAdd(ref objdmass, ref tmpdmass);

            // fix all positions
            IntPtr g = UBOdeNative.BodyGetFirstGeom(Body);
            while (g != IntPtr.Zero)
            {
                thispos = UBOdeNative.GeomGetOffsetPosition(g);
                thispos.X -= objdmass.c.X;
                thispos.Y -= objdmass.c.Y;
                thispos.Z -= objdmass.c.Z;
                UBOdeNative.GeomSetOffsetPosition(g, thispos.X, thispos.Y, thispos.Z);
                g = UBOdeNative.dBodyGetNextGeom(g);
            }
            UBOdeNative.BodyVectorToWorld(Body,objdmass.c.X, objdmass.c.Y, objdmass.c.Z,out thispos);

            UBOdeNative.BodySetPosition(Body, dobjpos.X + thispos.X, dobjpos.Y + thispos.Y, dobjpos.Z + thispos.Z);
            UBOdeNative.MassTranslate(ref objdmass, -objdmass.c.X, -objdmass.c.Y, -objdmass.c.Z); // ode wants inertia at center of body
            UBOdeNative.BodySetMass(Body, ref objdmass);
            m_mass = objdmass.mass;
        }

        private void FixInertia(Vector3 NewPos)
        {
            UBOdeNative.Matrix3 primmat;
            UBOdeNative.Mass tmpdmass;
            UBOdeNative.Mass primmass;

            UBOdeNative.Vector3 dobjpos;
            UBOdeNative.Vector3 thispos;

            UBOdeNative.BodyGetMass(Body, out UBOdeNative.Mass objdmass);

            // get prim own inertia in its local frame
            primmass = primdMass;
            // transform to object frame
            primmat = UBOdeNative.GeomGetOffsetRotation(m_prim_geom);
            UBOdeNative.MassRotate(ref primmass, ref primmat);

            tmpdmass = primmass;

            thispos = UBOdeNative.GeomGetOffsetPosition(m_prim_geom);
            UBOdeNative.MassTranslate(ref tmpdmass,
                            thispos.X,
                            thispos.Y,
                            thispos.Z);

            // subtract current prim inertia from object
            DMassSubPartFromObj(ref tmpdmass, ref objdmass);

            // update to new position
            m_position = NewPos;
            UBOdeNative.GeomSetOffsetWorldPosition(m_prim_geom, NewPos.X, NewPos.Y, NewPos.Z);

            thispos = UBOdeNative.GeomGetOffsetPosition(m_prim_geom);
            UBOdeNative.MassTranslate(ref primmass,
                            thispos.X,
                            thispos.Y,
                            thispos.Z);

            UBOdeNative.MassAdd(ref objdmass, ref primmass);

            // fix all positions
            IntPtr g = UBOdeNative.BodyGetFirstGeom(Body);
            while (g != IntPtr.Zero)
            {
                thispos = UBOdeNative.GeomGetOffsetPosition(g);
                thispos.X -= objdmass.c.X;
                thispos.Y -= objdmass.c.Y;
                thispos.Z -= objdmass.c.Z;
                UBOdeNative.GeomSetOffsetPosition(g, thispos.X, thispos.Y, thispos.Z);
                g = UBOdeNative.dBodyGetNextGeom(g);
            }

            UBOdeNative.BodyVectorToWorld(Body, objdmass.c.X, objdmass.c.Y, objdmass.c.Z, out thispos);

            // get current object position and rotation
            dobjpos = UBOdeNative.BodyGetPosition(Body);

            UBOdeNative.BodySetPosition(Body, dobjpos.X + thispos.X, dobjpos.Y + thispos.Y, dobjpos.Z + thispos.Z);
            UBOdeNative.MassTranslate(ref objdmass, -objdmass.c.X, -objdmass.c.Y, -objdmass.c.Z); // ode wants inertia at center of body
            UBOdeNative.BodySetMass(Body, ref objdmass);
            m_mass = objdmass.mass;
        }

        private void FixInertia(Quaternion newrot)
        {
            UBOdeNative.Matrix3 mat;
            UBOdeNative.Quaternion quat = new();

            UBOdeNative.Vector3 dobjpos;
            UBOdeNative.Vector3 thispos;

            UBOdeNative.BodyGetMass(Body, out UBOdeNative.Mass objdmass);

            // get prim own inertia in its local frame
            UBOdeNative.Mass tmpdmass = primdMass;
            mat = UBOdeNative.GeomGetOffsetRotation(m_prim_geom);
            UBOdeNative.MassRotate(ref tmpdmass, ref mat);
            // transform to object frame
            thispos = UBOdeNative.GeomGetOffsetPosition(m_prim_geom);
            UBOdeNative.MassTranslate(ref tmpdmass,
                            thispos.X,
                            thispos.Y,
                            thispos.Z);

            // subtract current prim inertia from object
            DMassSubPartFromObj(ref tmpdmass, ref objdmass);

            // update to new orientation
            m_orientation = newrot;
            quat.X = newrot.X;
            quat.Y = newrot.Y;
            quat.Z = newrot.Z;
            quat.W = newrot.W;
            UBOdeNative.GeomSetOffsetWorldQuaternion(m_prim_geom, ref quat);

            tmpdmass = primdMass;
            mat = UBOdeNative.GeomGetOffsetRotation(m_prim_geom);
            UBOdeNative.MassRotate(ref tmpdmass, ref mat);
            UBOdeNative.MassTranslate(ref tmpdmass,
                            thispos.X,
                            thispos.Y,
                            thispos.Z);

            UBOdeNative.MassAdd(ref objdmass, ref tmpdmass);

            // fix all positions
            IntPtr g = UBOdeNative.BodyGetFirstGeom(Body);
            while (g != IntPtr.Zero)
            {
                thispos = UBOdeNative.GeomGetOffsetPosition(g);
                thispos.X -= objdmass.c.X;
                thispos.Y -= objdmass.c.Y;
                thispos.Z -= objdmass.c.Z;
                UBOdeNative.GeomSetOffsetPosition(g, thispos.X, thispos.Y, thispos.Z);
                g = UBOdeNative.dBodyGetNextGeom(g);
            }

            UBOdeNative.BodyVectorToWorld(Body, objdmass.c.X, objdmass.c.Y, objdmass.c.Z, out thispos);
            // get current object position and rotation
            dobjpos = UBOdeNative.BodyGetPosition(Body);

            UBOdeNative.BodySetPosition(Body, dobjpos.X + thispos.X, dobjpos.Y + thispos.Y, dobjpos.Z + thispos.Z);
            UBOdeNative.MassTranslate(ref objdmass, -objdmass.c.X, -objdmass.c.Y, -objdmass.c.Z); // ode wants inertia at center of body
            UBOdeNative.BodySetMass(Body, ref objdmass);
            m_mass = objdmass.mass;
        }


        #region Mass Calculation

        private void UpdatePrimBodyData()
        {
            primMass = m_density * primVolume;

            if (primMass <= 0)
                primMass = 0.0001f;//ckrinke: Mass must be greater then zero.
            if (primMass > m_parentScene.maximumMassObject)
                primMass = m_parentScene.maximumMassObject;

            m_mass = primMass; // just in case

            UBOdeNative.MassSetBoxTotal(out primdMass, primMass, 2.0f * m_OBB.X, 2.0f * m_OBB.Y, 2.0f * m_OBB.Z);

            UBOdeNative.MassTranslate(ref primdMass,
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
            if (m_baseLocalID != prim.m_baseLocalID)
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
                                if (prm.m_prim_geom != IntPtr.Zero)
                                    UBOdeNative.GeomSetBody(prm.m_prim_geom, IntPtr.Zero);
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
                    if (prim.m_prim_geom != IntPtr.Zero)
                        UBOdeNative.GeomSetBody(prim.m_prim_geom, IntPtr.Zero);
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
            if (m_prim_geom != IntPtr.Zero)
            {
                m_orientation = UBOdeNative.GeomGetQuaternionOMV(m_prim_geom);
                /*
                // Debug
                float qlen = _orientation.Length();
                if (qlen > 1.01f || qlen < 0.99)
                    m_log.WarnFormat("[PHYSICS]: Got nonnorm quaternion from geom in Object {0} norm {1}", Name, qlen);
                */
                m_orientation.Normalize();
                m_position = UBOdeNative.GeomGetPositionOMV(m_prim_geom);
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
                    //odePrim.UpdateDataFromGeom();
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeadd()
        {
            m_parentScene.addToPrims(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                            UBOdeNative.JointDestroy(Amotor);
                            Amotor = IntPtr.Zero;
                        }
                    }
                }
            }
            // Store this for later in case we get turned into a separate body
            m_angularlocks = newLocks;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Stop()
        {
            if (!childPrim)
            {
                //m_force = Vector3.Zero;
                m_forceacc = Vector3.Zero;
                m_angularForceacc = Vector3.Zero;
                //m_torque = Vector3.Zero;
                _velocity = Vector3.Zero;
                m_acceleration = Vector3.Zero;
                m_rotationalVelocity = Vector3.Zero;
                _target_velocity = Vector3.Zero;
                if (m_vehicle != null && m_vehicle.Type != Vehicle.TYPE_NONE)
                    m_vehicle.Stop();

                _zeroFlag = false;
                base.RequestPhysicsterseUpdate();
            }

            if (Body != IntPtr.Zero)
            {
                UBOdeNative.BodySetForce(Body, 0f, 0f, 0f);
                UBOdeNative.BodySetTorque(Body, 0f, 0f, 0f);
                UBOdeNative.BodySetLinearVel(Body, 0f, 0f, 0f);
                UBOdeNative.BodySetAngularVel(Body, 0f, 0f, 0f);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePhantomStatus(bool newval)
        {
            m_isphantom = newval;

            UpdateCollisionCatFlags();
            ApplyCollisionCatFlags();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeSelectedStatus(bool newval)
        {
            if (m_lastdoneSelected == newval)
                return;

            m_lastdoneSelected = newval;
            DoSelectedStatus(newval);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckDelaySelect()
        {
            if (m_delaySelect)
            {
                DoSelectedStatus(m_isSelected);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoSelectedStatus(bool newval)
        {
            m_isSelected = newval;
            Stop();

            if (newval)
            {
                if (!childPrim && Body != IntPtr.Zero)
                    UBOdeNative.BodyDisable(Body);

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

                            if (prm.m_prim_geom != IntPtr.Zero)
                            {

                                if (prm.m_NoColide)
                                {
                                    UBOdeNative.GeomSetCategoryBits(prm.m_prim_geom, 0);
                                    UBOdeNative.GeomSetCollideBits(prm.m_prim_geom, 0);
                                }
                                else
                                {
                                    UBOdeNative.GeomSetCategoryBits(prm.m_prim_geom, (uint)m_collisionCategories);
                                    UBOdeNative.GeomSetCollideBits(prm.m_prim_geom, (uint)m_collisionFlags);
                                }
                            }
                            prm.m_delaySelect = false;
                        }
                    }
 
                    if (m_prim_geom != IntPtr.Zero)
                    {
                        if (m_NoColide)
                        {
                            UBOdeNative.GeomSetCategoryBits(m_prim_geom, 0);
                            UBOdeNative.GeomSetCollideBits(m_prim_geom, 0);
                            if (m_collide_geom != m_prim_geom && m_collide_geom != IntPtr.Zero)
                            {
                                UBOdeNative.GeomSetCategoryBits(m_collide_geom, 0);
                                UBOdeNative.GeomSetCollideBits(m_collide_geom, 0);
                            }
                        }
                        else
                        {
                            UBOdeNative.GeomSetCategoryBits(m_prim_geom, (uint)m_collisionCategories);
                            UBOdeNative.GeomSetCollideBits(m_prim_geom, (uint)m_collisionFlags);
                            if (m_collide_geom != m_prim_geom && m_collide_geom != IntPtr.Zero)
                            {
                                UBOdeNative.GeomSetCategoryBits(m_collide_geom, (uint)m_collisionCategories);
                                UBOdeNative.GeomSetCollideBits(m_collide_geom, (uint)m_collisionFlags);
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
                        UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                        UBOdeNative.BodyEnable(Body);
                    }
                }

                UpdateCollisionCatFlags();
                ApplyCollisionCatFlags();

                m_delaySelect = false;
            }

            resetCollisionAccounting();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePosition(Vector3 newPos)
        {
            CheckDelaySelect();
            if (m_isphysical)
            {
                if (childPrim)  // inertia is messed, must rebuild
                {
                    if (m_building)
                    {
                        m_position = newPos;
                    }

                    else if (m_forcePosOrRotation && Body != IntPtr.Zero && m_position.NotEqual(newPos))
                    {
                        FixInertia(newPos);
                        if (!UBOdeNative.BodyIsEnabled(Body))
                        {
                            _zeroFlag = true;
                            UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                            UBOdeNative.BodyEnable(Body);
                        }
                    }
                }
                else
                {
                    if (m_position.NotEqual(newPos))
                    {
                        UBOdeNative.GeomSetPosition(m_prim_geom, newPos.X, newPos.Y, newPos.Z);
                        m_position = newPos;
                    }
                    if (Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
                    {
                        _zeroFlag = true;
                        UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                        UBOdeNative.BodyEnable(Body);
                    }
                }
            }
            else
            {
                if (m_prim_geom != IntPtr.Zero)
                {
                    if (m_position.NotEqual(newPos))
                    {
                        UBOdeNative.GeomSetPosition(m_prim_geom, newPos.X, newPos.Y, newPos.Z);
                        m_position = newPos;

                        m_targetSpace = m_parentScene.MoveGeomToStaticSpace(m_prim_geom, m_targetSpace);
                    }
                }
            }
            m_givefakepos--;
            if (m_givefakepos < 0)
                m_givefakepos = 0;
            //changeSelectedStatus();
            resetCollisionAccounting();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeOrientation(Quaternion newOri)
        {
            CheckDelaySelect();
            if (m_isphysical)
            {
                if (childPrim)  // inertia is messed, must rebuild
                {
                    if (m_building)
                    {
                        m_orientation = newOri;
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
                    if (newOri.NotEqual(m_orientation))
                    {
                        UBOdeNative.Quaternion myrot = new()
                            {
                                X = newOri.X,
                                Y = newOri.Y,
                                Z = newOri.Z,
                                W = newOri.W
                            };
                        UBOdeNative.GeomSetQuaternion(m_prim_geom, ref myrot);
                        m_orientation = newOri;
                        
                        if (Body != IntPtr.Zero)
                        {
                            if(m_angularlocks != 0)
                                createAMotor(m_angularlocks);
                        }
                    }
                    if (Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
                    {
                        _zeroFlag = true;
                        UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                        UBOdeNative.BodyEnable(Body);
                    }
                }
            }
            else
            {
                if (m_prim_geom != IntPtr.Zero)
                {
                    if (newOri.NotEqual(m_orientation))
                    {
                        UBOdeNative.Quaternion myrot = new()
                            {
                                X = newOri.X,
                                Y = newOri.Y,
                                Z = newOri.Z,
                                W = newOri.W
                             };
                        UBOdeNative.GeomSetQuaternion(m_prim_geom, ref myrot);
                        m_orientation = newOri;
                    }
                }
            }
            m_givefakeori--;
            if (m_givefakeori < 0)
                m_givefakeori = 0;
            resetCollisionAccounting();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePositionAndOrientation(Vector3 newPos, Quaternion newOri)
        {
            CheckDelaySelect();
            if (m_isphysical)
            {
                if (childPrim && m_building)  // inertia is messed, must rebuild
                {
                    m_position = newPos;
                    m_orientation = newOri;
                }
                else
                {
                    if (newOri.NotEqual(m_orientation))
                    {
                        UBOdeNative.Quaternion myrot = new()
                            {
                                X = newOri.X,
                                Y = newOri.Y,
                                Z = newOri.Z,
                                W = newOri.W
                            };
                        UBOdeNative.GeomSetQuaternion(m_prim_geom, ref myrot);
                        m_orientation = newOri;
                        if (Body != IntPtr.Zero && m_angularlocks != 0)
                            createAMotor(m_angularlocks);
                    }
                    if (m_position.NotEqual(newPos))
                    {
                        UBOdeNative.GeomSetPosition(m_prim_geom, newPos.X, newPos.Y, newPos.Z);
                        m_position = newPos;
                    }
                    if (Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
                    {
                        _zeroFlag = true;
                        UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                        UBOdeNative.BodyEnable(Body);
                    }
                }
            }
            else
            {
                // string primScenAvatarIn = _parent_scene.whichspaceamIin(_position);
                // int[] arrayitem = _parent_scene.calculateSpaceArrayItemFromPos(_position);

                if (m_prim_geom != IntPtr.Zero)
                {
                    if (newOri.NotEqual(m_orientation))
                    {
                        UBOdeNative.Quaternion myrot = new()
                        {
                            X = newOri.X,
                            Y = newOri.Y,
                            Z = newOri.Z,
                            W = newOri.W
                        };
                        UBOdeNative.GeomSetQuaternion(m_prim_geom, ref myrot);
                        m_orientation = newOri;
                    }

                    if (newPos.NotEqual(m_position))
                    {
                        UBOdeNative.GeomSetPosition(m_prim_geom, newPos.X, newPos.Y, newPos.Z);
                        m_position = newPos;

                        m_targetSpace = m_parentScene.MoveGeomToStaticSpace(m_prim_geom, m_targetSpace);
                    }
                }
            }
            m_givefakepos--;
            if (m_givefakepos < 0)
                m_givefakepos = 0;
            m_givefakeori--;
            if (m_givefakeori < 0)
                m_givefakeori = 0;
            resetCollisionAccounting();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeSize(Vector3 newSize)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeShape(PrimitiveBaseShape newShape)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeAddPhysRep(ODEPhysRepData repData)
        {
            m_size = repData.size; //??
            m_pbs = repData.pbs;

            m_mesh = repData.mesh;

            m_assetID = repData.assetID;
            m_meshState = repData.meshState;

            m_hasOBB = repData.hasOBB;
            m_OBBOffset = repData.OBBOffset;
            m_OBB = repData.OBB;

            primVolume = repData.volume;

            CreateGeom(repData.isTooSmall);

            if (m_prim_geom != IntPtr.Zero)
            {
                UBOdeNative.GeomSetPosition(m_prim_geom, m_position.X, m_position.Y, m_position.Z);
                UBOdeNative.Quaternion myrot = new()
                {
                    X = m_orientation.X,
                    Y = m_orientation.Y,
                    Z = m_orientation.Z,
                    W = m_orientation.W
                };
                UBOdeNative.GeomSetQuaternion(m_prim_geom, ref myrot);
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
                repData.size = m_size;
                repData.pbs = m_pbs;
                repData.shapetype = m_fakeShapetype;
                m_parentScene.m_meshWorker.RequestMesh(repData);
            }
            else
                m_shapetype = repData.shapetype;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePhysRepData(ODEPhysRepData repData)
        {
            if(m_size == repData.size &&
                    m_pbs == repData.pbs &&
                    m_shapetype == repData.shapetype &&
                    m_mesh == repData.mesh &&
                    primVolume == repData.volume)
                return;

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

            m_size = repData.size;
            m_pbs = repData.pbs;

            m_mesh = repData.mesh;

            m_assetID = repData.assetID;
            m_meshState = repData.meshState;

            m_hasOBB = repData.hasOBB;
            m_OBBOffset = repData.OBBOffset;
            m_OBB = repData.OBB;

            primVolume = repData.volume;

            CreateGeom(repData.isTooSmall);

            if (m_prim_geom != IntPtr.Zero)
            {
                UBOdeNative.GeomSetPosition(m_prim_geom, m_position.X, m_position.Y, m_position.Z);
                UBOdeNative.Quaternion myrot = new()
                {
                    X = m_orientation.X,
                    Y = m_orientation.Y,
                    Z = m_orientation.Z,
                    W = m_orientation.W
                };
                UBOdeNative.GeomSetQuaternion(m_prim_geom, ref myrot);
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
                repData.size = m_size;
                repData.pbs = m_pbs;
                repData.shapetype = m_fakeShapetype;
                m_parentScene.m_meshWorker.RequestMesh(repData);
            }
            else
                m_shapetype = repData.shapetype;
        }

        /*
        private void changeFloatOnWater(bool newval)
        {
            m_collidesWater = newval;

            UpdateCollisionCatFlags();
            ApplyCollisionCatFlags();
        }
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeSetTorque(Vector3 newtorque)
        {
            if (!m_isSelected && !m_outbounds)
            {
                if (m_isphysical && Body != IntPtr.Zero)
                {
                    if (m_disabled)
                        enableBodySoft();
                    else if (!UBOdeNative.BodyIsEnabled(Body))
                    {
                        UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                        UBOdeNative.BodyEnable(Body);
                    }
                }
                m_torque = newtorque;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeForce(Vector3 force)
        {
            m_force = force;
            if (!m_isSelected && !m_outbounds && Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
            {
                UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                UBOdeNative.BodyEnable(Body);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeAddForce(Vector3 theforce)
        {
            m_forceacc += theforce;
            if (!m_isSelected && !m_outbounds)
            {
                lock (this)
                {
                    //m_log.Info("[PHYSICS]: dequeing forcelist");
                    if (m_isphysical && Body != IntPtr.Zero)
                    {
                        if (m_disabled)
                            enableBodySoft();
                        else if (!UBOdeNative.BodyIsEnabled(Body))
                        {
                            UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                            UBOdeNative.BodyEnable(Body);
                        }
                    }
                }
                m_collisionscore = 0;
            }
        }

        // actually angular impulse
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeAddAngularImpulse(Vector3 aimpulse)
        {
            m_angularForceacc += aimpulse * m_sceneInverseTimeStep;
            if (!m_isSelected && !m_outbounds)
            {
                lock (this)
                {
                    if (m_isphysical && Body != IntPtr.Zero)
                    {
                        if (m_disabled)
                            enableBodySoft();
                        else if (!UBOdeNative.BodyIsEnabled(Body))
                        {
                            UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                            UBOdeNative.BodyEnable(Body);
                        }
                    }
                }
                m_collisionscore = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changevelocity(Vector3 newVel)
        {
            float len = newVel.LengthSquared();
            if (len > 100000.0f) // limit to 100m/s
            {
                len = 100.0f / MathF.Sqrt(len);
                newVel *= len;
            }

            if (!m_isSelected && !m_outbounds)
            {
                if (Body != IntPtr.Zero)
                {
                    if (m_disabled)
                        enableBodySoft();
                    else if (!UBOdeNative.BodyIsEnabled(Body))
                    {
                        UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                        UBOdeNative.BodyEnable(Body);
                    }
                    UBOdeNative.BodySetLinearVel(Body, newVel.X, newVel.Y, newVel.Z);
                }
                //resetCollisionAccounting();
            }
            _velocity = newVel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeangvelocity(Vector3 newAngVel)
        {
            float len = newAngVel.LengthSquared();
            if (len > m_parentScene.maxAngVelocitySQ)
            {
                len = m_parentScene.maximumAngularVelocity / MathF.Sqrt(len);
                newAngVel *= len;
            }

            if (!m_isSelected && !m_outbounds)
            {
                if (Body != IntPtr.Zero)
                {
                    if (m_disabled)
                        enableBodySoft();
                    else if (!UBOdeNative.BodyIsEnabled(Body))
                    {
                        UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                        UBOdeNative.BodyEnable(Body);
                    }
                    UBOdeNative.BodySetAngularVel(Body, newAngVel.X, newAngVel.Y, newAngVel.Z);
                }
                //resetCollisionAccounting();
            }
            m_rotationalVelocity = newAngVel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeVolumedetetion(bool newVolDtc)
        {
            m_isVolumeDetect = newVolDtc;
            m_fakeisVolumeDetect = newVolDtc;
            UpdateCollisionCatFlags();
            ApplyCollisionCatFlags();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void changeSetVehicle(VehicleData vdata)
        {
            m_vehicle ??= new ODEDynamics(this);
            m_vehicle.DoSetVehicle(vdata);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeVehicleType(int value)
        {
            if (value == (int)Vehicle.TYPE_NONE)
            {
                if (m_vehicle != null)
                    m_vehicle = null;
            }
            else
            {
                m_vehicle ??= new ODEDynamics(this);

                m_vehicle.ProcessTypeChange((Vehicle)value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeVehicleFloatParam(strVehicleFloatParam fp)
        {
            if (m_vehicle == null)
                return;

            m_vehicle.ProcessFloatVehicleParam((Vehicle)fp.param, fp.value);
            if (Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
            {
                UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                UBOdeNative.BodyEnable(Body);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeVehicleVectorParam(strVehicleVectorParam vp)
        {
            if (m_vehicle == null)
                return;
            m_vehicle.ProcessVectorVehicleParam((Vehicle)vp.param, vp.value);
            if (Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
            {
                UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                UBOdeNative.BodyEnable(Body);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeVehicleRotationParam(strVehicleQuatParam qp)
        {
            if (m_vehicle == null)
                return;
            m_vehicle.ProcessRotationVehicleParam((Vehicle)qp.param, qp.value);
            if (Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
            {
                UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                UBOdeNative.BodyEnable(Body);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeVehicleFlags(strVehicleBoolParam bp)
        {
            if (m_vehicle == null)
                return;
            m_vehicle.ProcessVehicleFlags(bp.param, bp.value);
            if (Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
            {
                UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                UBOdeNative.BodyEnable(Body);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeBuoyancy(float b)
        {
            m_buoyancy = b;
            if (Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
            {
                UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                UBOdeNative.BodyEnable(Body);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePIDTarget(Vector3 trg)
        {
            m_PIDTarget = trg;
            if (Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
            {
                UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                UBOdeNative.BodyEnable(Body);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePIDTau(float tau)
        {
            m_PIDTau = tau;
            if (Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
            {
                UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                UBOdeNative.BodyEnable(Body);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePIDActive(bool val)
        {
            m_usePID = val;
            if (Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
            {
                UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                UBOdeNative.BodyEnable(Body);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePIDHoverHeight(float val)
        {
            m_PIDHoverHeight = val;
            if (val == 0)
                m_useHoverPID = false;
            if (Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
            {
                UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                UBOdeNative.BodyEnable(Body);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePIDHoverType(PIDHoverType type)
        {
            m_PIDHoverType = type;
            if (Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
            {
                UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                UBOdeNative.BodyEnable(Body);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePIDHoverTau(float tau)
        {
            m_PIDHoverTau = tau;
            if (Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
            {
                UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                UBOdeNative.BodyEnable(Body);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changePIDHoverActive(bool active)
        {
            m_useHoverPID = active;
            if (Body != IntPtr.Zero && !UBOdeNative.BodyIsEnabled(Body))
            {
                UBOdeNative.BodySetAutoDisableSteps(Body, m_body_autodisable_frames);
                UBOdeNative.BodyEnable(Body);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeInertia(PhysicsInertiaData inertia)
        {
            m_InertiaOverride = inertia;

            if (Body != IntPtr.Zero)
                DestroyBody();
            MakeBody();
        }

        #endregion

        public void Move()
        {
            if (!childPrim && m_isphysical && Body != IntPtr.Zero &&
                !m_disabled && !m_isSelected && !m_building && !m_outbounds)
            {
                if (!UBOdeNative.BodyIsEnabled(Body))
                {
                    // let vehicles sleep
                    if (m_vehicle != null && m_vehicle.Type != Vehicle.TYPE_NONE)
                        return;

                    if (++m_bodydisablecontrol < 50)
                        return;

                    // clear residuals
                    UBOdeNative.BodySetAngularVel(Body,0f,0f,0f);
                    UBOdeNative.BodySetLinearVel(Body,0f,0f,0f);
                    _zeroFlag = true;
                    UBOdeNative.BodySetAutoDisableSteps(Body, 1);
                    UBOdeNative.BodyEnable(Body);
                    m_bodydisablecontrol = -3;
                }

                if(m_bodydisablecontrol < 0)
                    m_bodydisablecontrol++;

                if (m_vehicle != null && m_vehicle.Type != Vehicle.TYPE_NONE)
                {
                    // 'VEHICLES' are dealt with in ODEDynamics.cs
                    m_vehicle.Step();
                    return;
                }

                float fx = 0;
                float fy = 0;
                float fz = 0;

                Vector3 lpos = UBOdeNative.GeomGetPositionOMV(m_prim_geom); // root position that is seem by rest of simulator

                if (m_usePID && m_PIDTau > 0)
                {
                    // for now position error
                    _target_velocity = m_PIDTarget - lpos;
                    if (_target_velocity.ApproxZero(0.02f))
                    {
                        UBOdeNative.BodySetPosition(Body, m_PIDTarget.X, m_PIDTarget.Y, m_PIDTarget.Z);
                        UBOdeNative.BodySetLinearVel(Body, 0, 0, 0);
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

                        UBOdeNative.Vector3 vel = UBOdeNative.BodyGetLinearVel(Body);
                        fx = (_target_velocity.X - vel.X) * m_sceneInverseTimeStep;
                        fy = (_target_velocity.Y - vel.Y) * m_sceneInverseTimeStep;
                        fz = (_target_velocity.Z - vel.Z) * m_sceneInverseTimeStep;
//                        d.BodySetLinearVel(Body, _target_velocity.X, _target_velocity.Y, _target_velocity.Z);
                    }
                }        // end if (m_usePID)

                // Hover PID Controller needs to be mutually exlusive to MoveTo PID controller
                else if (m_useHoverPID && m_PIDHoverTau != 0 && m_PIDHoverHeight != 0)
                {

                    //    Non-Vehicles have a limited set of Hover options.
                    // determine what our target height really is based on HoverType

                    float groundHeight = m_parentScene.GetTerrainHeightAtXY(lpos.X, lpos.Y);

                    switch (m_PIDHoverType)
                    {
                        case PIDHoverType.Ground:
                            m_targetHoverHeight = groundHeight + m_PIDHoverHeight;
                            break;

                        case PIDHoverType.GroundAndWater:
                            if (groundHeight > m_parentScene.WaterLevel)
                                m_targetHoverHeight = groundHeight + m_PIDHoverHeight;
                            else
                                m_targetHoverHeight = m_parentScene.WaterLevel + m_PIDHoverHeight;
                            break;
                    }     // end switch (m_PIDHoverType)

                    // don't go underground unless volumedetector

                    if (m_targetHoverHeight > groundHeight || m_isVolumeDetect)
                    {
                        UBOdeNative.Vector3 vel = UBOdeNative.BodyGetLinearVel(Body);

                        fz = (m_targetHoverHeight - lpos.Z);

                        //  if error is zero, use position control; otherwise, velocity control
                        if (MathF.Abs(fz) < 0.01f)
                        {
                            UBOdeNative.BodySetPosition(Body, lpos.X, lpos.Y, m_targetHoverHeight);
                            UBOdeNative.BodySetLinearVel(Body, vel.X, vel.Y, 0);
                        }
                        else
                        {
                            _zeroFlag = false;
                            fz /= m_PIDHoverTau;

                            if(fz < 0)
                            {
                                if (fz < -50f)
                                    fz = -50f;
                                else if (fz > -0.1f)
                                    fz = -0.1f;
                            }
                            else
                            {
                                if (fx > 50f)
                                    fz = 50f;
                                else if (fz < 0.1f)
                                    fz = 0.1f;
                            }

                            fz = ((fz - vel.Z) * m_sceneInverseTimeStep);
                        }
                    }
                }
                else
                {
                    float b = (1.0f - m_buoyancy) * m_gravmod;
                    fx = m_parentScene.gravityx * b;
                    fy = m_parentScene.gravityy * b;
                    fz = m_parentScene.gravityz * b;
                }

                //aceleration to force +  constant force + acc
                fx = m_mass * fx + m_force.X + m_forceacc.X;
                fy = m_mass * fy + m_force.Y + m_forceacc.Y;
                fz = m_mass * fz + m_force.Z + m_forceacc.Z;

                m_forceacc = Vector3.Zero;

                //m_log.Info("[OBJPID]: X:" + fx.ToString() + " Y:" + fy.ToString() + " Z:" + fz.ToString());
                if (fz != 0 || fx != 0 || fy != 0)
                {
                    UBOdeNative.BodyAddForce(Body, fx, fy, fz);
                    //Console.WriteLine("AddForce " + fx + "," + fy + "," + fz);
                }

                Vector3 trq = m_torque + m_angularForceacc;
                m_angularForceacc = Vector3.Zero;
                if (trq.X != 0 || trq.Y != 0 || trq.Z != 0)
                {
                    UBOdeNative.BodyAddTorque(Body, trq.X, trq.Y, trq.Z);
                }
            }
            else
            {   // is not physical, or is not a body or is selected
                //  _zeroPosition = d.BodyGetPosition(Body);
                //Console.WriteLine("Nothing " +  Name);
            }
        }

        public void UpdatePositionAndVelocity(int frame)
        {
            if (_parent == null && !m_isSelected && !m_disabled && !m_building && !m_outbounds && Body != IntPtr.Zero)
            {
                if(m_bodydisablecontrol < 0)
                    return;

                bool bodyenabled = UBOdeNative.BodyIsEnabled(Body);
                if (bodyenabled || !_zeroFlag)
                {
                    bool lastZeroFlag = _zeroFlag;

                    Vector3 lpos = UBOdeNative.GeomGetPositionOMV(m_prim_geom);

                    // check outside region
                    if (lpos.Z < -100 || lpos.Z > 100000f)
                    {
                        m_outbounds = true;

                        lpos.Z = Utils.Clamp(lpos.Z, -100f, 100000f);
                        m_acceleration = Vector3.Zero;
                        _velocity = Vector3.Zero;
                        m_rotationalVelocity = Vector3.Zero;

                        UBOdeNative.BodySetLinearVel(Body, 0, 0, 0); // stop it
                        UBOdeNative.BodySetAngularVel(Body, 0, 0, 0); // stop it
                        UBOdeNative.BodySetPosition(Body, lpos.X, lpos.Y, lpos.Z); // put it somewhere
                        m_lastposition = m_position;
                        m_lastorientation = m_orientation;

                        base.RequestPhysicsterseUpdate();

//                        throttleCounter = 0;
                        _zeroFlag = true;

                        disableBodySoft(); // disable it and colisions
                        base.RaiseOutOfBounds(m_position);
                        return;
                    }

                    if (lpos.X < 0f)
                    {
                        m_position.X = Utils.Clamp(lpos.X, -2f, -0.1f);
                        m_outbounds = true;
                    }
                    else if (lpos.X > m_parentScene.WorldExtents.X)
                    {
                        m_position.X = Utils.Clamp(lpos.X, m_parentScene.WorldExtents.X + 0.1f, m_parentScene.WorldExtents.X + 2f);
                        m_outbounds = true;
                    }
                    if (lpos.Y < 0f)
                    {
                        m_position.Y = Utils.Clamp(lpos.Y, -2f, -0.1f);
                        m_outbounds = true;
                    }
                    else if (lpos.Y > m_parentScene.WorldExtents.Y)
                    {
                        m_position.Y = Utils.Clamp(lpos.Y, m_parentScene.WorldExtents.Y + 0.1f, m_parentScene.WorldExtents.Y + 2f);
                        m_outbounds = true;
                    }

                    if (m_outbounds)
                    {
                        m_lastposition = m_position;
                        m_lastorientation = m_orientation;

                        _velocity = UBOdeNative.BodyGetLinearVelOMV(Body);
                        m_rotationalVelocity = UBOdeNative.BodyGetAngularVelOMV(Body);

                        UBOdeNative.BodySetLinearVel(Body, 0, 0, 0); // stop it
                        UBOdeNative.BodySetAngularVel(Body, 0, 0, 0);
                        UBOdeNative.GeomSetPosition(m_prim_geom, m_position.X, m_position.Y, m_position.Z);
                        disableBodySoft(); // stop collisions
                        UnSubscribeEvents();

                        base.RequestPhysicsterseUpdate();
                        return;
                    }

                    Quaternion ori = UBOdeNative.GeomGetQuaternionOMV(m_prim_geom);

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

                        if  (
                            (MathF.Abs(m_position.X - lpos.X) < poserror)
                            && (MathF.Abs(m_position.Y - lpos.Y) < poserror)
                            && (MathF.Abs(m_position.Z - lpos.Z) < poserror)
                            && (MathF.Abs(m_orientation.X - ori.X) < angerror)
                            && (MathF.Abs(m_orientation.Y - ori.Y) < angerror)
                            && (MathF.Abs(m_orientation.Z - ori.Z) < angerror)  // ignore W
                            )
                            _zeroFlag = true;
                        else
                            _zeroFlag = false;
                    }

                    // update position
                    if (!(_zeroFlag && lastZeroFlag))
                    {
                        m_position = lpos;
                        m_orientation = ori;
                    }

                    // update velocities and acceleration
                    if (_zeroFlag || lastZeroFlag)
                    {
                         // disable interpolators
                        _velocity = Vector3.Zero;
                        m_acceleration = Vector3.Zero;
                        m_rotationalVelocity = Vector3.Zero;
                    }
                    else
                    {
                        Vector3 vel = UBOdeNative.BodyGetLinearVelOMV(Body);
                        m_acceleration = _velocity;
                        if (vel.ApproxZero(0.005f))
                        {
                            _velocity = Vector3.Zero;
                            float t = -m_sceneInverseTimeStep;
                            m_acceleration *= t;
                        }
                        else
                        {
                            _velocity = vel;
                            m_acceleration = (_velocity - m_acceleration) * m_sceneInverseTimeStep;
                        }

                        if (m_acceleration.ApproxZero(0.01f))
                        {
                            m_acceleration = Vector3.Zero;
                        }

                        vel = UBOdeNative.BodyGetAngularVelOMV(Body);
                        if (vel.ApproxZero(0.0001f))
                        {
                            m_rotationalVelocity = Vector3.Zero;
                        }
                        else
                        {
                            m_rotationalVelocity = vel;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        internal static void DMassSubPartFromObj(ref UBOdeNative.Mass part, ref UBOdeNative.Mass theobj)
        {
            // assumes object center of mass is zero
            float smass = part.mass;
            theobj.mass -= smass;

            smass *= 1.0f / (theobj.mass);

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
            if (m_prim_geom == IntPtr.Zero && what != changes.Add && what != changes.AddPhysRep && what != changes.Remove)
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

                //case changes.TargetVelocity:
                //    break;

                //case changes.Acceleration:
                //    changeacceleration((Vector3)arg);
                //    break;

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

                //case changes.CollidesWater:
                //    changeFloatOnWater((bool)arg);
                //    break;

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

                case changes.SetInertia:
                    changeInertia((PhysicsInertiaData) arg);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddChange(changes what, object arg)
        {
            m_parentScene.AddChange(this, what, arg);
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
