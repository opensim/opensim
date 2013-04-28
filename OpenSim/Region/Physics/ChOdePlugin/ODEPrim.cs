/* Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
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
 *
 * Revised March 5th 2010 by Kitto Flora. ODEDynamics.cs
 * Ubit 2012
 * rolled into ODEPrim.cs
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using log4net;
using OpenMetaverse;
using Ode.NET;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.OdePlugin
{
    /// <summary>
    /// Various properties that ODE uses for AMotors but isn't exposed in ODE.NET so we must define them ourselves.
    /// </summary>

    public class OdePrim : PhysicsActor
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public class SerialControl
        {
            public object alock = new object();
            public byte[] data = new byte[0];
        }
        private Vector3 _position;
        private Vector3 _velocity;
        private Vector3 _torque;
        private Vector3 m_lastVelocity;
        private Vector3 m_lastposition;
        private Quaternion m_lastorientation = new Quaternion();
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
        private Vector3 m_rotateEnable = Vector3.One;				// Current setting
        private Vector3 m_rotateEnableRequest = Vector3.One;			// Request from LSL
        private bool m_rotateEnableUpdate = false;
        private Vector3 m_lockX;
        private Vector3 m_lockY;
        private Vector3 m_lockZ;
        private IntPtr Amotor = IntPtr.Zero;
        private IntPtr AmotorX = IntPtr.Zero;
        private IntPtr AmotorY = IntPtr.Zero;
        private IntPtr AmotorZ = IntPtr.Zero;

        private Vector3 m_PIDTarget;
        private float m_PIDTau;
        private float PID_D = 35f;
        private float PID_G = 25f;
        private bool m_usePID = false;

        private Quaternion m_APIDTarget = new Quaternion();
        private float m_APIDStrength = 0.5f;
        private float m_APIDDamping = 0.5f;
        private bool m_useAPID = false;
        private float m_APIDdamper = 1.0f;

        // These next 7 params apply to llSetHoverHeight(float height, integer water, float tau),
        // do not confuse with VEHICLE HOVER

        private float m_PIDHoverHeight;
        private float m_PIDHoverTau;
        private bool m_useHoverPID;
        private PIDHoverType m_PIDHoverType = PIDHoverType.Ground;
        private float m_targetHoverHeight;
        private float m_groundHeight;
        private float m_waterHeight;
        private float m_buoyancy;				//m_buoyancy set by llSetBuoyancy() 

        // private float m_tensor = 5f;
        private int body_autodisable_frames = 20;


        private const CollisionCategories m_default_collisionFlags = (CollisionCategories.Geom
                                                        | CollisionCategories.Space
                                                        | CollisionCategories.Body
                                                        | CollisionCategories.Character
                                                        );
        private bool m_taintshape;
        private bool m_taintPhysics;
        private bool m_collidesLand = true;
        private bool m_collidesWater;
        //        public bool m_returnCollisions;

        // Default we're a Geometry
        private CollisionCategories m_collisionCategories = (CollisionCategories.Geom);

        // Default, Collide with Other Geometries, spaces and Bodies
        private CollisionCategories m_collisionFlags = m_default_collisionFlags;

        public bool m_taintremove;
        public bool m_taintdisable;
        public bool m_disabled;
        public bool m_taintadd;
        public bool m_taintselected;
        public bool m_taintphantom;
        public bool m_taintCollidesWater;

        public uint m_localID;

        //public GCHandle gc;
        private CollisionLocker ode;

        private bool m_meshfailed = false;
        private bool m_taintforce = false;
        private bool m_taintaddangularforce = false;
        private Vector3 m_force;
        private List<Vector3> m_forcelist = new List<Vector3>();
        private List<Vector3> m_angularforcelist = new List<Vector3>();

        private IMesh _mesh;
        private PrimitiveBaseShape _pbs;
        private OdeScene _parent_scene;
        public IntPtr m_targetSpace = IntPtr.Zero;
        public IntPtr prim_geom;
        //        public IntPtr prev_geom;
        public IntPtr _triMeshData;

        private IntPtr _linkJointGroup = IntPtr.Zero;
        private PhysicsActor _parent;
        private PhysicsActor m_taintparent;

        private List<OdePrim> childrenPrim = new List<OdePrim>();

        private bool iscolliding;
        private bool m_isphysical;
        private bool m_isphantom;
        private bool m_isSelected;

        private bool m_NoColide;  // for now only for internal use for bad meshs

        internal bool m_isVolumeDetect; // If true, this prim only detects collisions but doesn't collide actively

        private bool m_throttleUpdates;
        private int throttleCounter;
        public int m_interpenetrationcount;
        public float m_collisionscore;
        //        public int m_roundsUnderMotionThreshold;
        //        private int m_crossingfailures;

        public bool m_outofBounds;
        private float m_density = 10.000006836f; // Aluminum g/cm3;

        private float m_primMass = 10.000006836f; // Aluminum g/cm3;

        private byte m_shapetype;
        private byte m_taintshapetype;

        public bool _zeroFlag;					 // if body has been stopped
        private bool m_lastUpdateSent;

        public IntPtr Body = IntPtr.Zero;
        public String m_primName;
        private Vector3 _target_velocity;
        public d.Mass pMass;

        public int m_eventsubscription;
        private CollisionEventUpdate CollisionEventsThisFrame;

        private IntPtr m_linkJoint = IntPtr.Zero;

        public volatile bool childPrim;

        internal int m_material = (int)Material.Wood;

        private IntPtr m_body = IntPtr.Zero;

        // Vehicle properties ============================================================================================
        private Vehicle m_type = Vehicle.TYPE_NONE;						// If a 'VEHICLE', and what kind
        // private Quaternion m_referenceFrame = Quaternion.Identity;	// Axis modifier
        private VehicleFlag m_flags = (VehicleFlag)0;					// Bit settings:
        // HOVER_TERRAIN_ONLY
        // HOVER_GLOBAL_HEIGHT
        // NO_DEFLECTION_UP
        // HOVER_WATER_ONLY
        // HOVER_UP_ONLY
        // LIMIT_MOTOR_UP
        // LIMIT_ROLL_ONLY

        // Linear properties
        private Vector3 m_linearMotorDirection = Vector3.Zero;			// (was m_linearMotorDirectionLASTSET) the (local) Velocity 
        //requested by LSL
        private float m_linearMotorTimescale = 0;						// Motor Attack rate set by LSL
        private float m_linearMotorDecayTimescale = 0;				// Motor Decay rate set by LSL
        private Vector3 m_linearFrictionTimescale = Vector3.Zero;		// General Friction set by LSL

        private Vector3 m_lLinMotorDVel = Vector3.Zero;					// decayed motor
        private Vector3 m_lLinObjectVel = Vector3.Zero;					// local frame object velocity
        private Vector3 m_wLinObjectVel = Vector3.Zero;					// world frame object velocity

        //Angular properties
        private Vector3 m_angularMotorDirection = Vector3.Zero;			// angular velocity requested by LSL motor 

        private float m_angularMotorTimescale = 0;						// motor angular Attack rate set by LSL
        private float m_angularMotorDecayTimescale = 0;					// motor angular Decay rate set by LSL
        private Vector3 m_angularFrictionTimescale = Vector3.Zero;		// body angular Friction set by LSL

        private Vector3 m_angularMotorDVel = Vector3.Zero;				// decayed angular motor
        //        private Vector3 m_angObjectVel = Vector3.Zero;					// current body angular velocity
        private Vector3 m_lastAngularVelocity = Vector3.Zero;			// what was last applied to body

        //Deflection properties        
        // private float m_angularDeflectionEfficiency = 0;
        // private float m_angularDeflectionTimescale = 0;
        // private float m_linearDeflectionEfficiency = 0;
        // private float m_linearDeflectionTimescale = 0;

        //Banking properties
        // private float m_bankingEfficiency = 0;
        // private float m_bankingMix = 0;
        // private float m_bankingTimescale = 0;

        //Hover and Buoyancy properties
        private float m_VhoverHeight = 0f;
        //        private float m_VhoverEfficiency = 0f;
        private float m_VhoverTimescale = 0f;
        private float m_VhoverTargetHeight = -1.0f;     // if <0 then no hover, else its the current target height 
        private float m_VehicleBuoyancy = 0f;			// Set by VEHICLE_BUOYANCY, for a vehicle.
        // Modifies gravity. Slider between -1 (double-gravity) and 1 (full anti-gravity) 
        // KF: So far I have found no good method to combine a script-requested .Z velocity and gravity.
        // Therefore only m_VehicleBuoyancy=1 (0g) will use the script-requested .Z velocity. 

        //Attractor properties        												
        private float m_verticalAttractionEfficiency = 1.0f;		// damped     
        private float m_verticalAttractionTimescale = 500f;			// Timescale > 300  means no vert attractor.

//        SerialControl m_taintserial = null;
        object m_taintvehicledata = null;

        public void DoSetVehicle()
        {
            VehicleData vd = (VehicleData)m_taintvehicledata;

        m_type = vd.m_type;
        m_flags = vd.m_flags;

        // Linear properties
        m_linearMotorDirection = vd.m_linearMotorDirection;
        m_linearFrictionTimescale = vd.m_linearFrictionTimescale;
        m_linearMotorDecayTimescale = vd.m_linearMotorDecayTimescale;
        m_linearMotorTimescale = vd.m_linearMotorTimescale;
//        m_linearMotorOffset = vd.m_linearMotorOffset;

        //Angular properties
        m_angularMotorDirection = vd.m_angularMotorDirection;
        m_angularMotorTimescale = vd.m_angularMotorTimescale;
        m_angularMotorDecayTimescale = vd.m_angularMotorDecayTimescale;
        m_angularFrictionTimescale = vd.m_angularFrictionTimescale;

        //Deflection properties
//        m_angularDeflectionEfficiency = vd.m_angularDeflectionEfficiency;
//        m_angularDeflectionTimescale = vd.m_angularDeflectionTimescale;
//        m_linearDeflectionEfficiency = vd.m_linearDeflectionEfficiency;
//        m_linearDeflectionTimescale = vd.m_linearDeflectionTimescale;

        //Banking properties
//        m_bankingEfficiency = vd.m_bankingEfficiency;
//        m_bankingMix = vd.m_bankingMix;
//        m_bankingTimescale = vd.m_bankingTimescale;

        //Hover and Buoyancy properties
        m_VhoverHeight = vd.m_VhoverHeight;
//        m_VhoverEfficiency = vd.m_VhoverEfficiency;
        m_VhoverTimescale = vd.m_VhoverTimescale;
        m_VehicleBuoyancy = vd.m_VehicleBuoyancy;

        //Attractor properties
        m_verticalAttractionEfficiency = vd.m_verticalAttractionEfficiency;
        m_verticalAttractionTimescale = vd.m_verticalAttractionTimescale;

        // Axis
//        m_referenceFrame = vd.m_referenceFrame;


            m_taintvehicledata = null;
        }

        public override void SetVehicle(object vdata)
        {
            m_taintvehicledata = vdata;
            _parent_scene.AddPhysicsActorTaint(this);
        }

        public OdePrim(String primName, OdeScene parent_scene, Vector3 pos, Vector3 size,
                       Quaternion rotation, IMesh mesh, PrimitiveBaseShape pbs, bool pisPhysical,
                       bool pisPhantom,byte shapetype, CollisionLocker dode, uint localid)
        {
            m_localID = localid;
            ode = dode;
            if (!pos.IsFinite())
            {
                pos = new Vector3(((float)Constants.RegionSize * 0.5f), ((float)Constants.RegionSize * 0.5f),
                    parent_scene.GetTerrainHeightAtXY(((float)Constants.RegionSize * 0.5f), ((float)Constants.RegionSize * 0.5f)) + 0.5f);
                m_log.Warn("[PHYSICS]: Got nonFinite Object create Position");
            }

            _position = pos;
            m_taintposition = pos;
            PID_D = parent_scene.bodyPIDD;
            PID_G = parent_scene.bodyPIDG;
            m_density = parent_scene.geomDefaultDensity;
            // m_tensor = parent_scene.bodyMotorJointMaxforceTensor;
            body_autodisable_frames = parent_scene.bodyFramesAutoDisable;

            prim_geom = IntPtr.Zero;
            //            prev_geom = IntPtr.Zero;

            if (!pos.IsFinite())
            {
                size = new Vector3(0.5f, 0.5f, 0.5f);
                m_log.Warn("[PHYSICS]: Got nonFinite Object create Size");
            }

            if (size.X <= 0) size.X = 0.01f;
            if (size.Y <= 0) size.Y = 0.01f;
            if (size.Z <= 0) size.Z = 0.01f;

            _size = size;
            m_taintsize = _size;

            if (!QuaternionIsFinite(rotation))
            {
                rotation = Quaternion.Identity;
                m_log.Warn("[PHYSICS]: Got nonFinite Object create Rotation");
            }

            _orientation = rotation;
            m_taintrot = _orientation;
            _mesh = mesh;
            _pbs = pbs;
            m_shapetype = shapetype;
            m_taintshapetype = shapetype;

            _parent_scene = parent_scene;
            m_targetSpace = (IntPtr)0;

            //            if (pos.Z < 0)
            if (pos.Z < parent_scene.GetTerrainHeightAtXY(pos.X, pos.Y))
                m_isphysical = false;
            else
            {
                m_isphysical = pisPhysical;
                // If we're physical, we need to be in the master space for now.
                // linksets *should* be in a space together..  but are not currently
                if (m_isphysical)
                    m_targetSpace = _parent_scene.space;
            }

            m_isphantom = pisPhantom;
            m_taintphantom = pisPhantom;

            _triMeshData = IntPtr.Zero;
            m_NoColide = false;

//            m_taintserial = null;
            m_primName = primName;
            m_taintadd = true;
            _parent_scene.AddPhysicsActorTaint(this);
            //  don't do .add() here; old geoms get recycled with the same hash
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
                //Console.WriteLine("Sel {0}  {1}  {2}", m_primName, value,   m_isphysical);        
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
                if (m_isSelected) disableBodySoft();
            }
        }

        public override bool IsPhysical
        {
            get { return m_isphysical; }
            set
            {
                m_isphysical = value;
                if (!m_isphysical)
                {		// Zero the remembered last velocity
                    m_lastVelocity = Vector3.Zero;
                    if (m_type != Vehicle.TYPE_NONE) Halt();
                }
            }
        }

        public override bool IsVolumeDtc
        {
            set { return; }
            get { return m_isVolumeDetect; }

        }

        public override bool Phantom
        {
            get { return m_isphantom; }
            set
            {
                m_isphantom = value;
            }
        }

        public void setPrimForRemoval()
        {
            m_taintremove = true;
        }

        public override bool Flying
        {
            // no flying prims for you
            get { return false; }
            set { }
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
            get { return _position; }

            set
            {
                _position = value;
                //m_log.Info("[PHYSICS]: " + _position.ToString());
            }
        }

        public override Vector3 Size
        {
            get { return _size; }
            set
            {
                if (value.IsFinite())
                {
                    _size = value;
                }
                else
                {
                    m_log.Warn("[PHYSICS]: Got NaN Size on object");
                }
            }
        }

        public override float Mass
        {
            get
            {
                CalculateMass();
                return m_primMass;
            }
        }

        public override Vector3 Force
        {
            //get { return Vector3.Zero; }
            get { return m_force; }
            set
            {
                if (value.IsFinite())
                {
                    m_force = value;
                }
                else
                {
                    m_log.Warn("[PHYSICS]: NaN in Force Applied to an Object");
                }
            }
        }

        public override int VehicleType
        {
            get { return (int)m_type; }
            set { ProcessTypeChange((Vehicle)value); }
        }

        public override void VehicleFloatParam(int param, float value)
        {
            ProcessFloatVehicleParam((Vehicle)param, value);
        }

        public override void VehicleVectorParam(int param, Vector3 value)
        {
            ProcessVectorVehicleParam((Vehicle)param, value);
        }

        public override void VehicleRotationParam(int param, Quaternion rotation)
        {
            ProcessRotationVehicleParam((Vehicle)param, rotation);
        }

        public override void VehicleFlags(int param, bool remove)
        {
            ProcessVehicleFlags(param, remove);
        }

        public override void SetVolumeDetect(int param)
        {
            lock (_parent_scene.OdeLock)
            {
                m_isVolumeDetect = (param != 0);
            }
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
            set
            {
                _pbs = value;
                m_taintshape = true;
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
                m_taintshapetype = value;
                _parent_scene.AddPhysicsActorTaint(this);
            }
        }

        public override Vector3 Velocity
        {
            get
            {
                // Averate previous velocity with the new one so
                // client object interpolation works a 'little' better
                if (_zeroFlag)
                    return Vector3.Zero;

                Vector3 returnVelocity = Vector3.Zero;
                returnVelocity.X = (m_lastVelocity.X + _velocity.X) / 2;
                returnVelocity.Y = (m_lastVelocity.Y + _velocity.Y) / 2;
                returnVelocity.Z = (m_lastVelocity.Z + _velocity.Z) / 2;
                return returnVelocity;
            }
            set
            {
                if (value.IsFinite())
                {
                    _velocity = value;
                    if (_velocity.ApproxEquals(Vector3.Zero, 0.001f))
                        _acceleration = Vector3.Zero;

                    m_taintVelocity = value;
                    _parent_scene.AddPhysicsActorTaint(this);
                }
                else
                {
                    m_log.Warn("[PHYSICS]: Got NaN Velocity in Object");
                }

            }
        }

        public override Vector3 Torque
        {
            get
            {
                if (!m_isphysical || Body == IntPtr.Zero)
                    return Vector3.Zero;

                return _torque;
            }

            set
            {
                if (value.IsFinite())
                {
                    m_taintTorque = value;
                    _parent_scene.AddPhysicsActorTaint(this);
                }
                else
                {
                    m_log.Warn("[PHYSICS]: Got NaN Torque in Object");
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
            get { return _orientation; }
            set
            {
                if (QuaternionIsFinite(value))
                {
                    _orientation = value;
                }
                else
                    m_log.Warn("[PHYSICS]: Got NaN quaternion Orientation from Scene in Object");

            }
        }

        public override bool FloatOnWater
        {
            set
            {
                m_taintCollidesWater = value;
                _parent_scene.AddPhysicsActorTaint(this);
            }
        }

        public override void SetMomentum(Vector3 momentum)
        {
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
                    m_log.Warn("[PHYSICS]: Got NaN PIDTarget from Scene on Object");
            }
        }
        public override bool PIDActive { set { m_usePID = value; } }
        public override float PIDTau { set { m_PIDTau = value; } }

        // For RotLookAt        
        public override Quaternion APIDTarget { set { m_APIDTarget = value; } }
        public override bool APIDActive { set { m_useAPID = value; } }
        public override float APIDStrength { set { m_APIDStrength = value; } }
        public override float APIDDamping { set { m_APIDDamping = value; } }

        public override float PIDHoverHeight { set { m_PIDHoverHeight = value; ; } }
        public override bool PIDHoverActive { set { m_useHoverPID = value; } }
        public override PIDHoverType PIDHoverType { set { m_PIDHoverType = value; } }
        public override float PIDHoverTau { set { m_PIDHoverTau = value; } }

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

        public override Vector3 Acceleration		// client updates read data via here
        {
            get
            {
                if (_zeroFlag)
                {
                    return Vector3.Zero;
                }
                return _acceleration;
            }
            set { _acceleration = value; }
        }


        public void SetAcceleration(Vector3 accel) // No one calls this, and it would not do anything.
        {
            _acceleration = accel;
        }

        public override void AddForce(Vector3 force, bool pushforce)
        {
            if (force.IsFinite())
            {
                lock (m_forcelist)
                    m_forcelist.Add(force);

                m_taintforce = true;
            }
            else
            {
                m_log.Warn("[PHYSICS]: Got Invalid linear force vector from Scene in Object");
            }
            //m_log.Info("[PHYSICS]: Added Force:" + force.ToString() +  " to prim at " + Position.ToString());
        }

        public override void AddAngularForce(Vector3 force, bool pushforce)
        {
            if (force.IsFinite())
            {
                m_angularforcelist.Add(force);
                m_taintaddangularforce = true;
            }
            else
            {
                m_log.Warn("[PHYSICS]: Got Invalid Angular force vector from Scene in Object");
            }
        }

        public override Vector3 RotationalVelocity
        {
            get
            {
                return m_rotationalVelocity;
            }
            set
            {
                if (value.IsFinite())
                {
                    m_rotationalVelocity = value;
                }
                else
                {
                    m_log.Warn("[PHYSICS]: Got NaN RotationalVelocity in Object");
                }
            }
        }

        public override void CrossingFailure()
        {
            if (m_outofBounds)
            {
                _position.X = Util.Clip(_position.X, 0.5f, _parent_scene.WorldExtents.X - 0.5f);
                _position.Y = Util.Clip(_position.Y, 0.5f, _parent_scene.WorldExtents.Y - 0.5f);
                _position.Z = Util.Clip(_position.Z, -100f, 50000f);
                d.BodySetPosition(Body, _position.X, _position.Y, _position.Z);

                m_lastposition = _position;

                _velocity = Vector3.Zero;
                m_lastVelocity = _velocity;


                if (m_type != Vehicle.TYPE_NONE)
                    Halt();

                d.BodySetLinearVel(Body, 0, 0, 0);
                base.RequestPhysicsterseUpdate();
                m_outofBounds = false;
            }
            /*
                        int tmp = Interlocked.Increment(ref m_crossingfailures);
                        if (tmp > _parent_scene.geomCrossingFailuresBeforeOutofbounds)
                        {
                            base.RaiseOutOfBounds(_position);
                            return;
                        }
                        else if (tmp == _parent_scene.geomCrossingFailuresBeforeOutofbounds)
                        {
                            m_log.Warn("[PHYSICS]: Too many crossing failures for: " + m_primName);
                        }
             */
        }

        public override float Buoyancy
        {
            get { return m_buoyancy; }
            set { m_buoyancy = value; }
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
            // This is actually ROTATION ENABLE, not a lock.
            // default is <1,1,1> which is all enabled. 
            // The lock value is updated inside Move(), no point in using the taint system.
            // OS 'm_taintAngularLock' etc change to m_rotateEnable.
            if (axis.IsFinite())
            {
                axis.X = (axis.X > 0) ? 1f : 0f;
                axis.Y = (axis.Y > 0) ? 1f : 0f;
                axis.Z = (axis.Z > 0) ? 1f : 0f;
                m_log.DebugFormat("[axislock]: <{0},{1},{2}>", axis.X, axis.Y, axis.Z);
                m_rotateEnableRequest = axis;
                m_rotateEnableUpdate = true;
            }
            else
            {
                m_log.Warn("[PHYSICS]: Got NaN locking axis from Scene on Object");
            }
        }

        public void SetGeom(IntPtr geom)
        {
            if (prim_geom != IntPtr.Zero)
            {
                // Remove any old entries
                //string tPA;
                //_parent_scene.geom_name_map.TryGetValue(prim_geom, out tPA);
                //Console.WriteLine("**** Remove {0}", tPA);
                if (_parent_scene.geom_name_map.ContainsKey(prim_geom)) _parent_scene.geom_name_map.Remove(prim_geom);
                if (_parent_scene.actor_name_map.ContainsKey(prim_geom)) _parent_scene.actor_name_map.Remove(prim_geom);
                d.GeomDestroy(prim_geom);
            }

            prim_geom = geom;
            //Console.WriteLine("SetGeom to " + prim_geom + " for " + m_primName);     
            if (prim_geom != IntPtr.Zero)
            {
                _parent_scene.geom_name_map[prim_geom] = this.m_primName;
                _parent_scene.actor_name_map[prim_geom] = (PhysicsActor)this;
                //Console.WriteLine("**** Create {2}    Dicts: actor={0}   name={1}", _parent_scene.actor_name_map.Count, _parent_scene.geom_name_map.Count, this.m_primName);
                if (m_NoColide)
                {
                    d.GeomSetCategoryBits(prim_geom, 0);
                    if (m_isphysical && !m_isVolumeDetect)
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
            }

            if (childPrim)
            {
                if (_parent != null && _parent is OdePrim)
                {
                    OdePrim parent = (OdePrim)_parent;
                    //Console.WriteLine("SetGeom calls ChildSetGeom");                    
                    parent.ChildSetGeom(this);
                }
            }
            //m_log.Warn("Setting Geom to: " + prim_geom);
        }

        public void enableBodySoft()
        {
            if (!childPrim)
            {
                if (m_isphysical && Body != IntPtr.Zero)
                {
                    d.BodyEnable(Body);
                    if (m_type != Vehicle.TYPE_NONE)
                        Enable(Body, _parent_scene);
                }

                m_disabled = false;
            }
        }

        public void disableBodySoft()
        {
            m_disabled = true;

            if (m_isphysical && Body != IntPtr.Zero)
            {
                d.BodyDisable(Body);
                Halt();
            }
        }

        public void enableBody()
        {
            // Don't enable this body if we're a child prim
            // this should be taken care of in the parent function not here
            if (!childPrim)
            {
                // Sets the geom to a body
                Body = d.BodyCreate(_parent_scene.world);

                setMass();
                d.BodySetPosition(Body, _position.X, _position.Y, _position.Z);
                d.Quaternion myrot = new d.Quaternion();
                myrot.X = _orientation.X;
                myrot.Y = _orientation.Y;
                myrot.Z = _orientation.Z;
                myrot.W = _orientation.W;
                d.BodySetQuaternion(Body, ref myrot);
                d.GeomSetBody(prim_geom, Body);

                m_collisionCategories |= CollisionCategories.Body;
                m_collisionFlags |= (CollisionCategories.Land | CollisionCategories.Wind);

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

                d.BodySetAutoDisableFlag(Body, true);
                d.BodySetAutoDisableSteps(Body, body_autodisable_frames);

                // disconnect from world gravity so we can apply buoyancy
                d.BodySetGravityMode(Body, false);

                m_interpenetrationcount = 0;
                m_collisionscore = 0;
                m_disabled = false;

                if (m_type != Vehicle.TYPE_NONE)
                {
                    Enable(Body, _parent_scene);
                }

                _parent_scene.addActivePrim(this);
            }
        }

        #region Mass Calculation

        private float CalculateMass()
        {
            float volume = _size.X * _size.Y * _size.Z; // default
            float tmp;

            float returnMass = 0;
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
                                    hollowVolume *= 0.78539816339f; ;
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

            returnMass = m_density * volume;

            if (returnMass <= 0)
                returnMass = 0.0001f;//ckrinke: Mass must be greater then zero.
            //            else if (returnMass > _parent_scene.maximumMassObject)
            //                returnMass = _parent_scene.maximumMassObject;



            m_primMass = returnMass;
            if (m_primMass > _parent_scene.maximumMassObject)
                m_primMass = _parent_scene.maximumMassObject;

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
                OdePrim[] childPrimArr = new OdePrim[0];

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
            if (returnMass > _parent_scene.maximumMassObject)
                returnMass = _parent_scene.maximumMassObject;
            return returnMass;
        }// end CalculateMass

        #endregion

        public void setMass()
        {
            if (Body != (IntPtr)0)
            {
                float newmass = CalculateMass();

                //m_log.Info("[PHYSICS]: New Mass: " + newmass.ToString());

                d.MassSetBoxTotal(out pMass, newmass, _size.X, _size.Y, _size.Z);
                d.BodySetMass(Body, ref pMass);
            }
        }


        private void UpdateDataFromGeom()
        {
            if (prim_geom != IntPtr.Zero)
            {
                d.Quaternion qtmp;
                d.GeomCopyQuaternion(prim_geom, out qtmp);
                _orientation.W = qtmp.W;
                _orientation.X = qtmp.X;
                _orientation.Y = qtmp.Y;
                _orientation.Z = qtmp.Z;

                d.Vector3 lpos = d.GeomGetPosition(prim_geom);
                _position.X = lpos.X;
                _position.Y = lpos.Y;
                _position.Z = lpos.Z;
            }
        }

        public void disableBody()
        {
            //this kills the body so things like 'mesh' can re-create it.
            lock (this)
            {
                if (!childPrim)
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
                                d.GeomDisable(prim_geom);
                            }
                            else
                            {
                                d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                                d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
                            }
                        }

                        UpdateDataFromGeom();

                        lock (childrenPrim)
                        {
                            if (childrenPrim.Count > 0)
                            {
                                foreach (OdePrim prm in childrenPrim)
                                {
                                    if (prm.prim_geom != IntPtr.Zero)
                                    {
                                        if (prm.m_NoColide)
                                        {
                                            d.GeomSetCategoryBits(prm.prim_geom, 0);
                                            d.GeomSetCollideBits(prm.prim_geom, 0);
                                            d.GeomDisable(prm.prim_geom);

                                        }
                                        prm.UpdateDataFromGeom();
                                    }
                                    _parent_scene.remActivePrim(prm);
                                    prm.Body = IntPtr.Zero;
                                }
                            }
                        }
                        d.BodyDestroy(Body);
                        Body = IntPtr.Zero;
                    }
                }
                else
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
                            d.GeomDisable(prim_geom);
                        }
                        else
                        {
                            d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                            d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
                        }
                    }

                    Body = IntPtr.Zero;
                }
            }
            m_disabled = true;
            m_collisionscore = 0;
        }

//        private static Dictionary<IMesh, IntPtr> m_MeshToTriMeshMap = new Dictionary<IMesh, IntPtr>();

        public bool setMesh(OdeScene parent_scene, IMesh mesh)
        {
            //Kill Body so that mesh can re-make the geom
            if (IsPhysical && Body != IntPtr.Zero)
            {
                if (childPrim)
                {
                    if (_parent != null)
                    {
                        OdePrim parent = (OdePrim)_parent;
                        parent.ChildDelink(this);
                    }
                }
                else
                {
                    disableBody();
                }
            }

            IntPtr vertices, indices;
            int vertexCount, indexCount;
            int vertexStride, triStride;
            mesh.getVertexListAsPtrToFloatArray(out vertices, out vertexStride, out vertexCount); // Note, that vertices are fixed in unmanaged heap
            mesh.getIndexListAsPtrToIntArray(out indices, out triStride, out indexCount); // Also fixed, needs release after usage

            // warning this destroys the mesh for eventual future use. Only pinned float arrays stay valid
            mesh.releaseSourceMeshData(); // free up the original mesh data to save memory

            if (vertexCount == 0 || indexCount == 0)
            {
                m_log.WarnFormat("[PHYSICS]: Got invalid mesh on prim {0} at <{1},{2},{3}>. mesh UUID {4}", Name, _position.X, _position.Y, _position.Z, _pbs.SculptTexture.ToString());
                return false;
            }

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
                m_log.ErrorFormat("[PHYSICS]: Create trimesh failed on prim {0} : {1}",Name,e.Message);

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

        public void ProcessTaints(float timestep) //=============================================================================
        {
            if (m_taintadd)
            {
                changeadd(timestep);
            }

            if (m_taintremove)
                return;

            if (prim_geom != IntPtr.Zero)
            {
                if (!_position.ApproxEquals(m_taintposition, 0f))
                {
                    changemove(timestep);
                }
                if (m_taintrot != _orientation)
                {
                    if (childPrim && IsPhysical)	// For physical child prim...
                    {
                        rotate(timestep);
                        // KF: ODE will also rotate the parent prim!
                        // so rotate the root back to where it was
                        OdePrim parent = (OdePrim)_parent;
                        parent.rotate(timestep);
                    }
                    else
                    {
                        //Just rotate the prim
                        rotate(timestep);
                    }
                }
                //
                if (m_taintphantom != m_isphantom )
                {
                    changePhantomStatus();
                }//

                if (m_taintPhysics != m_isphysical && !(m_taintparent != _parent))
                {
                    changePhysicsStatus(timestep);
                }//


                if (!_size.ApproxEquals(m_taintsize, 0f))
                    changesize(timestep);
                //

                if(m_taintshapetype != m_shapetype)
                {
                    m_shapetype = m_taintshapetype;
                    changeshape(timestep);
                }

                if (m_taintshape)
                    changeshape(timestep);
                //

                if (m_taintforce)
                    changeAddForce(timestep);

                if (m_taintaddangularforce)
                    changeAddAngularForce(timestep);

                if (!m_taintTorque.ApproxEquals(Vector3.Zero, 0.001f))
                    changeSetTorque(timestep);

                if (m_taintdisable)
                    changedisable(timestep);

                if (m_taintselected != m_isSelected)
                    changeSelectedStatus();

                if (!m_taintVelocity.ApproxEquals(Vector3.Zero, 0.001f))
                    changevelocity(timestep);

                if (m_taintparent != _parent)
                    changelink(timestep);

                if (m_taintCollidesWater != m_collidesWater)
                    changefloatonwater(timestep);

                if (m_taintvehicledata != null)
                    DoSetVehicle();

                /* obsolete                    
                                if (!m_angularLock.ApproxEquals(m_taintAngularLock,0f))
                                    changeAngularLock(timestep);
                 */
            }

            else
            {
                m_log.Error("[PHYSICS]: prim {0} at <{1},{2},{3}> as invalid geom");

                // not sure this will not flame...
                m_taintremove = true;
                _parent_scene.AddPhysicsActorTaint(this);
            }

        }

        private void changelink(float timestep)
        {
            // If the newly set parent is not null
            // create link
            if (_parent == null && m_taintparent != null)
            {
                if (m_taintparent.PhysicsActorType == (int)ActorTypes.Prim)
                {
                    OdePrim obj = (OdePrim)m_taintparent;
                    obj.ParentPrim(this);
                }
            }
            // If the newly set parent is null
            // destroy link
            else if (_parent != null && m_taintparent == null)
            {
                if (_parent is OdePrim)
                {
                    OdePrim obj = (OdePrim)_parent;
                    obj.ChildDelink(this);
                    childPrim = false;
                }
            }

            _parent = m_taintparent;
            m_taintPhysics = m_isphysical;
        }

        // I'm the parent
        // prim is the child
        public void ParentPrim(OdePrim prim)
        {
            if (this.m_localID != prim.m_localID)
            {
                if (Body == IntPtr.Zero)
                {
                    Body = d.BodyCreate(_parent_scene.world);
                    // disconnect from world gravity so we can apply buoyancy
                    d.BodySetGravityMode(Body, false);

                    setMass();
                }
                if (Body != IntPtr.Zero)
                {
                    lock (childrenPrim)
                    {
                        if (!childrenPrim.Contains(prim))
                        {
                            childrenPrim.Add(prim);

                            foreach (OdePrim prm in childrenPrim)
                            {
                                d.Mass m2;
                                d.MassSetZero(out m2);
                                d.MassSetBoxTotal(out m2, prim.CalculateMass(), prm._size.X, prm._size.Y, prm._size.Z);


                                d.Quaternion quat = new d.Quaternion();
                                quat.W = prm._orientation.W;
                                quat.X = prm._orientation.X;
                                quat.Y = prm._orientation.Y;
                                quat.Z = prm._orientation.Z;

                                d.Matrix3 mat = new d.Matrix3();
                                d.RfromQ(out mat, ref quat);
                                d.MassRotate(ref m2, ref mat);
                                d.MassTranslate(ref m2, Position.X - prm.Position.X, Position.Y - prm.Position.Y, Position.Z - prm.Position.Z);
                                d.MassAdd(ref pMass, ref m2);
                            }
                            foreach (OdePrim prm in childrenPrim)
                            {
                                if (m_isphantom && !prm.m_isVolumeDetect)
                                {
                                    prm.m_collisionCategories = 0;
                                    prm.m_collisionFlags = CollisionCategories.Land;
                                }
                                else
                                {
                                    prm.m_collisionCategories |= CollisionCategories.Body;
                                    prm.m_collisionFlags |= (CollisionCategories.Land | CollisionCategories.Wind);
                                }
                                if (prm.prim_geom == IntPtr.Zero)
                                {
                                    m_log.Warn("[PHYSICS]: Unable to link one of the linkset elements.  No geom yet");
                                    continue;
                                }

                                if (prm.m_NoColide)
                                {
                                    d.GeomSetCategoryBits(prm.prim_geom, 0);
                                    d.GeomSetCollideBits(prm.prim_geom, (int)CollisionCategories.Land);
                                }
                                else
                                {
                                    d.GeomSetCategoryBits(prm.prim_geom, (int)prm.m_collisionCategories);
                                    d.GeomSetCollideBits(prm.prim_geom, (int)prm.m_collisionFlags);
                                }

                                d.Quaternion quat = new d.Quaternion();
                                quat.W = prm._orientation.W;
                                quat.X = prm._orientation.X;
                                quat.Y = prm._orientation.Y;
                                quat.Z = prm._orientation.Z;

                                d.Matrix3 mat = new d.Matrix3();
                                d.RfromQ(out mat, ref quat);
                                if (Body != IntPtr.Zero)
                                {
                                    d.GeomSetBody(prm.prim_geom, Body);
                                    prm.childPrim = true;
                                    d.GeomSetOffsetWorldPosition(prm.prim_geom, prm.Position.X, prm.Position.Y, prm.Position.Z);
                                    //d.GeomSetOffsetPosition(prim.prim_geom,
                                    //    (Position.X - prm.Position.X) - pMass.c.X,
                                    //    (Position.Y - prm.Position.Y) - pMass.c.Y,
                                    //    (Position.Z - prm.Position.Z) - pMass.c.Z);
                                    d.GeomSetOffsetWorldRotation(prm.prim_geom, ref mat);
                                    //d.GeomSetOffsetRotation(prm.prim_geom, ref mat);
                                    d.MassTranslate(ref pMass, -pMass.c.X, -pMass.c.Y, -pMass.c.Z);
                                    d.BodySetMass(Body, ref pMass);
                                }
                                else
                                {
                                    m_log.Debug("[PHYSICS]:I ain't got no boooooooooddy, no body");
                                }

                                prm.m_interpenetrationcount = 0;
                                prm.m_collisionscore = 0;
                                prm.m_disabled = false;

                                prm.Body = Body;

                                    _parent_scene.addActivePrim(prm);
                            }

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

                            d.Quaternion quat2 = new d.Quaternion();
                            quat2.W = _orientation.W;
                            quat2.X = _orientation.X;
                            quat2.Y = _orientation.Y;
                            quat2.Z = _orientation.Z;

                            d.Matrix3 mat2 = new d.Matrix3();
                            d.RfromQ(out mat2, ref quat2);
                            d.GeomSetBody(prim_geom, Body);
                            d.GeomSetOffsetWorldPosition(prim_geom, Position.X - pMass.c.X, Position.Y - pMass.c.Y, Position.Z - pMass.c.Z);
                            //d.GeomSetOffsetPosition(prim.prim_geom,
                            //    (Position.X - prm.Position.X) - pMass.c.X,
                            //    (Position.Y - prm.Position.Y) - pMass.c.Y,
                            //    (Position.Z - prm.Position.Z) - pMass.c.Z);
                            //d.GeomSetOffsetRotation(prim_geom, ref mat2);
                            d.MassTranslate(ref pMass, -pMass.c.X, -pMass.c.Y, -pMass.c.Z);
                            d.BodySetMass(Body, ref pMass);

                            d.BodySetAutoDisableFlag(Body, true);
                            d.BodySetAutoDisableSteps(Body, body_autodisable_frames);

                            m_interpenetrationcount = 0;
                            m_collisionscore = 0;
                            m_disabled = false;

                            d.BodySetPosition(Body, Position.X, Position.Y, Position.Z);
                            if (m_type != Vehicle.TYPE_NONE) Enable(Body, _parent_scene);

                            _parent_scene.addActivePrim(this);
                        }
                    }
                }
            }
        }

        private void ChildSetGeom(OdePrim odePrim)
        {
            lock (childrenPrim)
            {
                foreach (OdePrim prm in childrenPrim)
                {
                    prm.disableBody();
                }
            }
            disableBody();

            if (Body != IntPtr.Zero)
            {
                _parent_scene.remActivePrim(this);
            }

            lock (childrenPrim)
            {
                foreach (OdePrim prm in childrenPrim)
                {
                    ParentPrim(prm);
                }
            }
        }

        private void ChildDelink(OdePrim odePrim)
        {
            // Okay, we have a delinked child..   need to rebuild the body.
            lock (childrenPrim)
            {
                foreach (OdePrim prm in childrenPrim)
                {
                    prm.childPrim = true;
                    prm.disableBody();
                }
            }
            disableBody();

            lock (childrenPrim)
            {
                childrenPrim.Remove(odePrim);
            }

            if (Body != IntPtr.Zero)
            {
                _parent_scene.remActivePrim(this);
            }

            lock (childrenPrim)
            {
                foreach (OdePrim prm in childrenPrim)
                {
                    ParentPrim(prm);
                }
            }
        }

        private void changePhantomStatus()
        {
            m_taintphantom = m_isphantom;
            changeSelectedStatus();
        }

/* not in use
        private void SetCollider()
        {
            SetCollider(m_isSelected, m_isphysical, m_isphantom, m_isSelected);
        }

        private void SetCollider(bool sel, bool phys, bool phan, bool vdtc)
        {
            if (sel)
            {
                m_collisionCategories = CollisionCategories.Selected;
                m_collisionFlags = (CollisionCategories.Sensor | CollisionCategories.Space);
            }
            else
            {
                if (phan && !vdtc)
                {
                    m_collisionCategories = 0;
                    if (phys)
                        m_collisionFlags = CollisionCategories.Land;
                    else
                        m_collisionFlags = 0; // this case should not happen non physical phantoms should not have physics
                }
                else
                {
                    m_collisionCategories = CollisionCategories.Geom;
                    if (phys)
                        m_collisionCategories |= CollisionCategories.Body;

                    m_collisionFlags = m_default_collisionFlags;

                    if (m_collidesLand)
                        m_collisionFlags |= CollisionCategories.Land;
                    if (m_collidesWater)
                        m_collisionFlags |= CollisionCategories.Water;
                }
            }

            if (prim_geom != IntPtr.Zero)
            {
                if (m_NoColide)
                {
                    d.GeomSetCategoryBits(prim_geom, 0);
                    if (phys)
                        d.GeomSetCollideBits(prim_geom, (int)CollisionCategories.Land);
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
            }
        }
*/

        private void changeSelectedStatus()
        {
            if (m_taintselected)
            {
                m_collisionCategories = CollisionCategories.Selected;
                m_collisionFlags = (CollisionCategories.Sensor | CollisionCategories.Space);

                // We do the body disable soft twice because 'in theory' a collision could have happened
                // in between the disabling and the collision properties setting
                // which would wake the physical body up from a soft disabling and potentially cause it to fall
                // through the ground.

                // NOTE FOR JOINTS: this doesn't always work for jointed assemblies because if you select
                // just one part of the assembly, the rest of the assembly is non-selected and still simulating,
                // so that causes the selected part to wake up and continue moving.

                // even if you select all parts of a jointed assembly, it is not guaranteed that the entire
                // assembly will stop simulating during the selection, because of the lack of atomicity
                // of select operations (their processing could be interrupted by a thread switch, causing
                // simulation to continue before all of the selected object notifications trickle down to
                // the physics engine).

                // e.g. we select 100 prims that are connected by joints. non-atomically, the first 50 are
                // selected and disabled. then, due to a thread switch, the selection processing is
                // interrupted and the physics engine continues to simulate, so the last 50 items, whose
                // selection was not yet processed, continues to simulate. this wakes up ALL of the 
                // first 50 again. then the last 50 are disabled. then the first 50, which were just woken
                // up, start simulating again, which in turn wakes up the last 50.

                if (m_isphysical)
                {
                    disableBodySoft();
                }

                if (prim_geom != IntPtr.Zero)
                {
                    d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                    d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
                    if (m_NoColide)
                        d.GeomDisable(prim_geom);
                }

                if (m_isphysical)
                {
                    disableBodySoft();
                }
                if (Body != IntPtr.Zero)
                {
                    d.BodySetLinearVel(Body, 0f, 0f, 0f);
                    d.BodySetForce(Body, 0f, 0f, 0f);
                    d.BodySetAngularVel(Body, 0.0f, 0.0f, 0.0f);
                    d.BodySetTorque(Body, 0.0f, 0.0f, 0.0f);
                }
            }
            else
            {
                if (m_isphantom && !m_isVolumeDetect)
                {
                    m_collisionCategories = 0;
                    if (m_isphysical)
                        m_collisionFlags = CollisionCategories.Land;
                    else
                        m_collisionFlags = 0; // this case should not happen non physical phantoms should not have physics
                }
                else
                {
                    m_collisionCategories = CollisionCategories.Geom;
                    if (m_isphysical)
                        m_collisionCategories |= CollisionCategories.Body;

                    m_collisionFlags = m_default_collisionFlags;

                    if (m_collidesLand)
                        m_collisionFlags |= CollisionCategories.Land;
                    if (m_collidesWater)
                        m_collisionFlags |= CollisionCategories.Water;
                }

                if (prim_geom != IntPtr.Zero)
                {
                    if (m_NoColide)
                    {
                        d.GeomSetCategoryBits(prim_geom, 0);
                        if (m_isphysical)
                            d.GeomSetCollideBits(prim_geom, (int)CollisionCategories.Land);
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
                }
                if (Body != IntPtr.Zero)
                {
                    d.BodySetLinearVel(Body, 0f, 0f, 0f);
                    d.BodySetForce(Body, 0f, 0f, 0f);
                    d.BodySetAngularVel(Body, 0.0f, 0.0f, 0.0f);
                    d.BodySetTorque(Body, 0.0f, 0.0f, 0.0f);
                }

                if (m_isphysical)
                {
                    if (Body != IntPtr.Zero)
                    {
                        enableBodySoft();
                    }
                }
            }

            resetCollisionAccounting();
            m_isSelected = m_taintselected;
        }//end changeSelectedStatus

        public void ResetTaints()
        {
            m_taintposition = _position;
            m_taintrot = _orientation;
            m_taintPhysics = m_isphysical;
            m_taintselected = m_isSelected;
            m_taintsize = _size;
            m_taintshape = false;
            m_taintforce = false;
            m_taintdisable = false;
            m_taintVelocity = Vector3.Zero;
        }

        public void CreateGeom(IntPtr m_targetSpace, IMesh _mesh)
        {
            bool gottrimesh = false;

            m_NoColide = false; // assume all will go well

            if (_triMeshData != IntPtr.Zero)
            {
                d.GeomTriMeshDataDestroy(_triMeshData);
                _triMeshData = IntPtr.Zero;
            }

            if (_mesh != null)
            {
                gottrimesh = setMesh(_parent_scene, _mesh);
                if (!gottrimesh)
                {
                    // getting a mesh failed,
                    // lets go on having a basic box or sphere, with prim size but not coliding
                    // physical colides with land, non with nothing

                    m_NoColide = true;
                }
            }

            if (!gottrimesh)
            { // we will have a basic box or sphere
                IntPtr geo = IntPtr.Zero;

                if (_pbs.ProfileShape == ProfileShape.HalfCircle && _pbs.PathCurve == (byte)Extrusion.Curve1
                    && _size.X == _size.Y && _size.X == _size.Z)
                {
                    // its a sphere
                    _parent_scene.waitForSpaceUnlock(m_targetSpace);
                    try
                    {
                        geo = d.CreateSphere(m_targetSpace, _size.X * 0.5f);
                    }
                    catch (Exception e)
                    {
                        m_log.WarnFormat("[PHYSICS]: Unable to create basic sphere for object {0}", e.Message);
                        geo = IntPtr.Zero;
                        ode.dunlock(_parent_scene.world);
                    }
                }
                else // make it a box
                {
                    _parent_scene.waitForSpaceUnlock(m_targetSpace);
                    try
                    {
                        geo = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                    }
                    catch (Exception e)
                    {
                        m_log.WarnFormat("[PHYSICS]: Unable to create basic sphere for object {0}", e.Message);
                        geo = IntPtr.Zero;
                        ode.dunlock(_parent_scene.world);
                    }
                }

                if (geo == IntPtr.Zero) // if this happens it must be fixed
                {
                    // if it does lets stop what we can
                    // not sure this will not flame...

                    m_taintremove = true;
                    _parent_scene.AddPhysicsActorTaint(this);
                    return;
                }

                SetGeom(geo); // this processes the m_NoColide
            }
        }

        public void changeadd(float timestep)
        {
            int[] iprimspaceArrItem = _parent_scene.calculateSpaceArrayItemFromPos(_position);
            IntPtr targetspace = _parent_scene.calculateSpaceForGeom(_position);

            if (targetspace == IntPtr.Zero)
                targetspace = _parent_scene.createprimspace(iprimspaceArrItem[0], iprimspaceArrItem[1]);

            m_targetSpace = targetspace;

            if (_mesh == null) // && m_meshfailed == false)
            {
                if (_parent_scene.needsMeshing(_pbs))
                {
                    bool convex;
                    if (m_shapetype == 2)
                        convex = true;
                    else
                        convex = false;
                    try
                    {
                        _mesh = _parent_scene.mesher.CreateMesh(m_primName, _pbs, _size, (int)LevelOfDetail.High, true,false,convex,false);
                    }
                    catch
                    {
                        //Don't continuously try to mesh prims when meshing has failed
                        m_meshfailed = true;
                        _mesh = null;
                        m_log.WarnFormat("[PHYSICS]: changeAdd CreateMesh fail on prim {0} at <{1},{2},{3}>", Name, _position.X, _position.Y, _position.Z);
                    }
                }
            }

            lock (_parent_scene.OdeLock)
            {
                CreateGeom(m_targetSpace, _mesh);

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

                if (m_isphysical && Body == IntPtr.Zero)
                {
                    enableBody();
                }
            }

            changeSelectedStatus();

            m_taintadd = false;
        }

        public void changemove(float timestep)
        {
            if (m_isphysical)
            {
                //                if (!m_disabled && !m_taintremove && !childPrim)  After one edit m_disabled is sometimes set, disabling further edits!
                if (!m_taintremove && !childPrim)
                {
                    if (Body == IntPtr.Zero)
                        enableBody();
                    //Prim auto disable after 20 frames,
                    //if you move it, re-enable the prim manually.
                    if (_parent != null)
                    {
                        if (m_linkJoint != IntPtr.Zero)
                        {
                            d.JointDestroy(m_linkJoint);
                            m_linkJoint = IntPtr.Zero;
                        }
                    }
                    if (Body != IntPtr.Zero)
                    {
                        d.BodySetPosition(Body, _position.X, _position.Y, _position.Z);

                        if (_parent != null)
                        {
                            OdePrim odParent = (OdePrim)_parent;
                            if (Body != (IntPtr)0 && odParent.Body != (IntPtr)0 && Body != odParent.Body)
                            {
                                // KF: Fixed Joints were removed? Anyway - this Console.WriteLine does not show up, so routine is not used??
                                Console.WriteLine("ODEPrim JointCreateFixed !!!");
                                m_linkJoint = d.JointCreateFixed(_parent_scene.world, _linkJointGroup);
                                d.JointAttach(m_linkJoint, Body, odParent.Body);
                                d.JointSetFixed(m_linkJoint);
                            }
                        }
                        d.BodyEnable(Body);
                        if (m_type != Vehicle.TYPE_NONE)
                        {
                            Enable(Body, _parent_scene);
                        }
                    }
                    else
                    {
                        m_log.Warn("[PHYSICS]: Body Still null after enableBody().  This is a crash scenario.");
                    }
                }
                //else
                // {
                //m_log.Debug("[BUG]: race!");
                //}
            }
            else
            {
                // string primScenAvatarIn = _parent_scene.whichspaceamIin(_position);
                // int[] arrayitem = _parent_scene.calculateSpaceArrayItemFromPos(_position);
                _parent_scene.waitForSpaceUnlock(m_targetSpace);

                IntPtr tempspace = _parent_scene.recalculateSpaceForGeom(prim_geom, _position, m_targetSpace);
                m_targetSpace = tempspace;

                _parent_scene.waitForSpaceUnlock(m_targetSpace);
                if (prim_geom != IntPtr.Zero)
                {
                    d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);

                    _parent_scene.waitForSpaceUnlock(m_targetSpace);
                    d.SpaceAdd(m_targetSpace, prim_geom);
                }
            }

            changeSelectedStatus();

            resetCollisionAccounting();
            m_taintposition = _position;
        }

        public void rotate(float timestep)
        {
            d.Quaternion myrot = new d.Quaternion();
            myrot.X = _orientation.X;
            myrot.Y = _orientation.Y;
            myrot.Z = _orientation.Z;
            myrot.W = _orientation.W;
            if (Body != IntPtr.Zero)
            {
                // KF: If this is a root prim do BodySet
                d.BodySetQuaternion(Body, ref myrot);
            }
            else
            {
                // daughter prim, do Geom set
                d.GeomSetQuaternion(prim_geom, ref myrot);
            }

            resetCollisionAccounting();
            m_taintrot = _orientation;
        }

        private void resetCollisionAccounting()
        {
            m_collisionscore = 0;
            m_interpenetrationcount = 0;
            m_disabled = false;
        }

        public void changedisable(float timestep)
        {
            m_disabled = true;
            if (Body != IntPtr.Zero)
            {
                d.BodyDisable(Body);
                Body = IntPtr.Zero;
            }

            m_taintdisable = false;
        }

        public void changePhysicsStatus(float timestep)
        {
            if (m_isphysical == true)
            {
                if (Body == IntPtr.Zero)
                {
                    if (_pbs.SculptEntry && _parent_scene.meshSculptedPrim)
                    {
                        changeshape(2f);
                    }
                    else
                    {
                        enableBody();
                    }
                }
            }
            else
            {
                if (Body != IntPtr.Zero)
                {
                    if (_pbs.SculptEntry && _parent_scene.meshSculptedPrim)
                    {
                        _mesh = null;
                        changeadd(2f);
                    }
                    if (childPrim)
                    {
                        if (_parent != null)
                        {
                            OdePrim parent = (OdePrim)_parent;
                            parent.ChildDelink(this);
                        }
                    }
                    else
                    {
                        disableBody();
                    }
                }
            }

            changeSelectedStatus();

            resetCollisionAccounting();
            m_taintPhysics = m_isphysical;
        }

        public void changesize(float timestamp)
        {

            string oldname = _parent_scene.geom_name_map[prim_geom];

            if (_size.X <= 0) _size.X = 0.01f;
            if (_size.Y <= 0) _size.Y = 0.01f;
            if (_size.Z <= 0) _size.Z = 0.01f;

            // Cleanup of old prim geometry
            if (_mesh != null)
            {
                // Cleanup meshing here
            }
            //kill body to rebuild
            if (IsPhysical && Body != IntPtr.Zero)
            {
                if (childPrim)
                {
                    if (_parent != null)
                    {
                        OdePrim parent = (OdePrim)_parent;
                        parent.ChildDelink(this);
                    }
                }
                else
                {
                    disableBody();
                }
            }
            if (d.SpaceQuery(m_targetSpace, prim_geom))
            {
                _parent_scene.waitForSpaceUnlock(m_targetSpace);
                d.SpaceRemove(m_targetSpace, prim_geom);
            }
            // we don't need to do space calculation because the client sends a position update also.

            // Construction of new prim
            if (_parent_scene.needsMeshing(_pbs))// && m_meshfailed == false)
            {
                float meshlod = _parent_scene.meshSculptLOD;

                if (IsPhysical)
                    meshlod = _parent_scene.MeshSculptphysicalLOD;
                // Don't need to re-enable body..   it's done in SetMesh

                IMesh mesh = null;

                try
                {
                    if (_parent_scene.needsMeshing(_pbs))
                        mesh = _parent_scene.mesher.CreateMesh(oldname, _pbs, _size, (int)LevelOfDetail.High, true);
                }
                catch
                {
                    m_meshfailed = true;
                    mesh = null;
                    m_log.WarnFormat("[PHYSICS]: changeSize CreateMesh fail on prim {0} at <{1},{2},{3}>", Name, _position.X, _position.Y, _position.Z);
                }

                //IMesh mesh = _parent_scene.mesher.CreateMesh(oldname, _pbs, _size, meshlod, IsPhysical);
                CreateGeom(m_targetSpace, mesh);
            }
            else
            {
                _mesh = null;
                CreateGeom(m_targetSpace, _mesh);
            }

            d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
            d.Quaternion myrot = new d.Quaternion();
            myrot.X = _orientation.X;
            myrot.Y = _orientation.Y;
            myrot.Z = _orientation.Z;
            myrot.W = _orientation.W;
            d.GeomSetQuaternion(prim_geom, ref myrot);

            //d.GeomBoxSetLengths(prim_geom, _size.X, _size.Y, _size.Z);
            if (IsPhysical && Body == IntPtr.Zero && !childPrim)
            {
                // Re creates body on size.
                // EnableBody also does setMass()
                enableBody();
                d.BodyEnable(Body);
            }

            _parent_scene.geom_name_map[prim_geom] = oldname;

            changeSelectedStatus();
            if (childPrim)
            {
                if (_parent is OdePrim)
                {
                    OdePrim parent = (OdePrim)_parent;
                    parent.ChildSetGeom(this);
                }
            }
            resetCollisionAccounting();
            m_taintsize = _size;
        }



        public void changefloatonwater(float timestep)
        {
            m_collidesWater = m_taintCollidesWater;

            if (prim_geom != IntPtr.Zero)
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

        public void changeshape(float timestamp)
        {
            string oldname = _parent_scene.geom_name_map[prim_geom];

            // Cleanup of old prim geometry and Bodies
            if (IsPhysical && Body != IntPtr.Zero)
            {
                if (childPrim)
                {
                    if (_parent != null)
                    {
                        OdePrim parent = (OdePrim)_parent;
                        parent.ChildDelink(this);
                    }
                }
                else
                {
                    disableBody();
                }
            }


            // we don't need to do space calculation because the client sends a position update also.
            if (_size.X <= 0) _size.X = 0.01f;
            if (_size.Y <= 0) _size.Y = 0.01f;
            if (_size.Z <= 0) _size.Z = 0.01f;
            // Construction of new prim

            if (_parent_scene.needsMeshing(_pbs))// && m_meshfailed == false)
            {
                // Don't need to re-enable body..   it's done in SetMesh
                float meshlod = _parent_scene.meshSculptLOD;
                IMesh mesh;

                if (IsPhysical)
                    meshlod = _parent_scene.MeshSculptphysicalLOD;

                bool convex;
                if (m_shapetype == 2)
                    convex = true;
                else
                    convex = false;

                try
                {
                    mesh = _parent_scene.mesher.CreateMesh(oldname, _pbs, _size, (int)LevelOfDetail.High, true, false,convex,false);
                }
                catch
                {
                    mesh = null;
                    m_meshfailed = true;
                    m_log.WarnFormat("[PHYSICS]: changeAdd CreateMesh fail on prim {0} at <{1},{2},{3}>", Name, _position.X, _position.Y, _position.Z);
                }

                CreateGeom(m_targetSpace, mesh);

                // createmesh returns null when it doesn't mesh.
            }
            else
            {
                _mesh = null;
                CreateGeom(m_targetSpace, null);
            }

            d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
            d.Quaternion myrot = new d.Quaternion();
            //myrot.W = _orientation.w;
            myrot.W = _orientation.W;
            myrot.X = _orientation.X;
            myrot.Y = _orientation.Y;
            myrot.Z = _orientation.Z;
            d.GeomSetQuaternion(prim_geom, ref myrot);

            //d.GeomBoxSetLengths(prim_geom, _size.X, _size.Y, _size.Z);
            if (IsPhysical && Body == IntPtr.Zero)
            {
                // Re creates body on size.
                // EnableBody also does setMass()
                enableBody();
                if (Body != IntPtr.Zero)
                {
                    d.BodyEnable(Body);
                }
            }
            _parent_scene.geom_name_map[prim_geom] = oldname;

            changeSelectedStatus();
            if (childPrim)
            {
                if (_parent is OdePrim)
                {
                    OdePrim parent = (OdePrim)_parent;
                    parent.ChildSetGeom(this);
                }
            }
            resetCollisionAccounting();
            m_taintshape = false;
        }

        public void changeAddForce(float timestamp)
        {
            if (!m_isSelected)
            {
                lock (m_forcelist)
                {
                    //m_log.Info("[PHYSICS]: dequeing forcelist");
                    if (IsPhysical)
                    {
                        Vector3 iforce = Vector3.Zero;
                        int i = 0;
                        try
                        {
                            for (i = 0; i < m_forcelist.Count; i++)
                            {

                                iforce = iforce + (m_forcelist[i] * 100);
                            }
                        }
                        catch (IndexOutOfRangeException)
                        {
                            m_forcelist = new List<Vector3>();
                            m_collisionscore = 0;
                            m_interpenetrationcount = 0;
                            m_taintforce = false;
                            return;
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            m_forcelist = new List<Vector3>();
                            m_collisionscore = 0;
                            m_interpenetrationcount = 0;
                            m_taintforce = false;
                            return;
                        }
                        d.BodyEnable(Body);

                        d.BodyAddForce(Body, iforce.X, iforce.Y, iforce.Z);
                    }
                    m_forcelist.Clear();
                }

                m_collisionscore = 0;
                m_interpenetrationcount = 0;
            }

            m_taintforce = false;

        }



        public void changeSetTorque(float timestamp)
        {
            if (!m_isSelected)
            {
                if (IsPhysical && Body != IntPtr.Zero)
                {
                    d.BodySetTorque(Body, m_taintTorque.X, m_taintTorque.Y, m_taintTorque.Z);
                }
            }

            m_taintTorque = Vector3.Zero;
        }

        public void changeAddAngularForce(float timestamp)
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
                            iforce = iforce + (m_angularforcelist[i] * 100);
                        }
                        d.BodyEnable(Body);
                        d.BodyAddTorque(Body, iforce.X, iforce.Y, iforce.Z);

                    }
                    m_angularforcelist.Clear();
                }

                m_collisionscore = 0;
                m_interpenetrationcount = 0;
            }

            m_taintaddangularforce = false;
        }

        private void changevelocity(float timestep)
        {
            if (!m_isSelected)
            {
                Thread.Sleep(20);
                if (IsPhysical)
                {
                    if (Body != IntPtr.Zero)
                        d.BodySetLinearVel(Body, m_taintVelocity.X, m_taintVelocity.Y, m_taintVelocity.Z);
                }

                //resetCollisionAccounting();
            }
            m_taintVelocity = Vector3.Zero;
        }

        public void UpdatePositionAndVelocity()
        {
            return;   // moved to the Move () method
        }

        public d.Mass FromMatrix4(Matrix4 pMat, ref d.Mass obj)
        {
            obj.I.M00 = pMat[0, 0];
            obj.I.M01 = pMat[0, 1];
            obj.I.M02 = pMat[0, 2];
            obj.I.M10 = pMat[1, 0];
            obj.I.M11 = pMat[1, 1];
            obj.I.M12 = pMat[1, 2];
            obj.I.M20 = pMat[2, 0];
            obj.I.M21 = pMat[2, 1];
            obj.I.M22 = pMat[2, 2];
            return obj;
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

        public static Matrix4 Inverse(Matrix4 pMat)
        {
            if (determinant3x3(pMat) == 0)
            {
                return Matrix4.Identity; // should probably throw an error.  singluar matrix inverse not possible
            }



            return (Adjoint(pMat) / determinant3x3(pMat));
        }

        public static Matrix4 Adjoint(Matrix4 pMat)
        {
            Matrix4 adjointMatrix = new Matrix4();
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    Matrix4SetValue(ref adjointMatrix, i, j, (float)(Math.Pow(-1, i + j) * (determinant3x3(Minor(pMat, i, j)))));
                }
            }

            adjointMatrix = Transpose(adjointMatrix);
            return adjointMatrix;
        }

        public static Matrix4 Minor(Matrix4 matrix, int iRow, int iCol)
        {
            Matrix4 minor = new Matrix4();
            int m = 0, n = 0;
            for (int i = 0; i < 4; i++)
            {
                if (i == iRow)
                    continue;
                n = 0;
                for (int j = 0; j < 4; j++)
                {
                    if (j == iCol)
                        continue;
                    Matrix4SetValue(ref minor, m, n, matrix[i, j]);
                    n++;
                }
                m++;
            }
            return minor;
        }

        public static Matrix4 Transpose(Matrix4 pMat)
        {
            Matrix4 transposeMatrix = new Matrix4();
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    Matrix4SetValue(ref transposeMatrix, i, j, pMat[j, i]);
            return transposeMatrix;
        }

        public static void Matrix4SetValue(ref Matrix4 pMat, int r, int c, float val)
        {
            switch (r)
            {
                case 0:
                    switch (c)
                    {
                        case 0:
                            pMat.M11 = val;
                            break;
                        case 1:
                            pMat.M12 = val;
                            break;
                        case 2:
                            pMat.M13 = val;
                            break;
                        case 3:
                            pMat.M14 = val;
                            break;
                    }

                    break;
                case 1:
                    switch (c)
                    {
                        case 0:
                            pMat.M21 = val;
                            break;
                        case 1:
                            pMat.M22 = val;
                            break;
                        case 2:
                            pMat.M23 = val;
                            break;
                        case 3:
                            pMat.M24 = val;
                            break;
                    }

                    break;
                case 2:
                    switch (c)
                    {
                        case 0:
                            pMat.M31 = val;
                            break;
                        case 1:
                            pMat.M32 = val;
                            break;
                        case 2:
                            pMat.M33 = val;
                            break;
                        case 3:
                            pMat.M34 = val;
                            break;
                    }

                    break;
                case 3:
                    switch (c)
                    {
                        case 0:
                            pMat.M41 = val;
                            break;
                        case 1:
                            pMat.M42 = val;
                            break;
                        case 2:
                            pMat.M43 = val;
                            break;
                        case 3:
                            pMat.M44 = val;
                            break;
                    }

                    break;
            }
        }
        private static float determinant3x3(Matrix4 pMat)
        {
            float det = 0;
            float diag1 = pMat[0, 0] * pMat[1, 1] * pMat[2, 2];
            float diag2 = pMat[0, 1] * pMat[2, 1] * pMat[2, 0];
            float diag3 = pMat[0, 2] * pMat[1, 0] * pMat[2, 1];
            float diag4 = pMat[2, 0] * pMat[1, 1] * pMat[0, 2];
            float diag5 = pMat[2, 1] * pMat[1, 2] * pMat[0, 0];
            float diag6 = pMat[2, 2] * pMat[1, 0] * pMat[0, 1];

            det = diag1 + diag2 + diag3 - (diag4 + diag5 + diag6);
            return det;

        }

        private static void DMassCopy(ref d.Mass src, ref d.Mass dst)
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

        public override void SetMaterial(int pMaterial)
        {
            m_material = pMaterial;
        }

        internal void ProcessFloatVehicleParam(Vehicle pParam, float pValue)
        {
            switch (pParam)
            {
                case Vehicle.ANGULAR_DEFLECTION_EFFICIENCY:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_angularDeflectionEfficiency = pValue;
                    break;
                case Vehicle.ANGULAR_DEFLECTION_TIMESCALE:
                    if (pValue < 0.1f) pValue = 0.1f;
                    // m_angularDeflectionTimescale = pValue;
                    break;
                case Vehicle.ANGULAR_MOTOR_DECAY_TIMESCALE:
                    if (pValue < 0.3f) pValue = 0.3f;
                    m_angularMotorDecayTimescale = pValue;
                    break;
                case Vehicle.ANGULAR_MOTOR_TIMESCALE:
                    if (pValue < 0.3f) pValue = 0.3f;
                    m_angularMotorTimescale = pValue;
                    break;
                case Vehicle.BANKING_EFFICIENCY:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_bankingEfficiency = pValue;
                    break;
                case Vehicle.BANKING_MIX:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_bankingMix = pValue;
                    break;
                case Vehicle.BANKING_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_bankingTimescale = pValue;
                    break;
                case Vehicle.BUOYANCY:
                    if (pValue < -1f) pValue = -1f;
                    if (pValue > 1f) pValue = 1f;
                    m_VehicleBuoyancy = pValue;
                    break;
                //                case Vehicle.HOVER_EFFICIENCY:
                //                	if (pValue < 0f) pValue = 0f;
                //                	if (pValue > 1f) pValue = 1f;
                //                    m_VhoverEfficiency = pValue;
                //                    break;
                case Vehicle.HOVER_HEIGHT:
                    m_VhoverHeight = pValue;
                    break;
                case Vehicle.HOVER_TIMESCALE:
                    if (pValue < 0.1f) pValue = 0.1f;
                    m_VhoverTimescale = pValue;
                    break;
                case Vehicle.LINEAR_DEFLECTION_EFFICIENCY:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_linearDeflectionEfficiency = pValue;
                    break;
                case Vehicle.LINEAR_DEFLECTION_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_linearDeflectionTimescale = pValue;
                    break;
                case Vehicle.LINEAR_MOTOR_DECAY_TIMESCALE:
                    if (pValue < 0.3f) pValue = 0.3f;
                    m_linearMotorDecayTimescale = pValue;
                    break;
                case Vehicle.LINEAR_MOTOR_TIMESCALE:
                    if (pValue < 0.1f) pValue = 0.1f;
                    m_linearMotorTimescale = pValue;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_EFFICIENCY:
                    if (pValue < 0.1f) pValue = 0.1f;	// Less goes unstable
                    if (pValue > 1.0f) pValue = 1.0f;
                    m_verticalAttractionEfficiency = pValue;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_TIMESCALE:
                    if (pValue < 0.1f) pValue = 0.1f;
                    m_verticalAttractionTimescale = pValue;
                    break;

                // These are vector properties but the engine lets you use a single float value to 
                // set all of the components to the same value
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    if (pValue > 30f) pValue = 30f;
                    if (pValue < 0.1f) pValue = 0.1f;
                    m_angularFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    m_angularMotorDirection = new Vector3(pValue, pValue, pValue);
                    UpdateAngDecay();
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    if (pValue < 0.1f) pValue = 0.1f;
                    m_linearFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue, pValue, pValue);
                    UpdateLinDecay();
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    // m_linearMotorOffset = new Vector3(pValue, pValue, pValue);
                    break;

            }

        }//end ProcessFloatVehicleParam

        internal void ProcessVectorVehicleParam(Vehicle pParam, Vector3 pValue)
        {
            switch (pParam)
            {
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    if (pValue.X > 30f) pValue.X = 30f;
                    if (pValue.X < 0.1f) pValue.X = 0.1f;
                    if (pValue.Y > 30f) pValue.Y = 30f;
                    if (pValue.Y < 0.1f) pValue.Y = 0.1f;
                    if (pValue.Z > 30f) pValue.Z = 30f;
                    if (pValue.Z < 0.1f) pValue.Z = 0.1f;
                    m_angularFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    m_angularMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    // Limit requested angular speed to 2 rps= 4 pi rads/sec
                    if (m_angularMotorDirection.X > 12.56f) m_angularMotorDirection.X = 12.56f;
                    if (m_angularMotorDirection.X < -12.56f) m_angularMotorDirection.X = -12.56f;
                    if (m_angularMotorDirection.Y > 12.56f) m_angularMotorDirection.Y = 12.56f;
                    if (m_angularMotorDirection.Y < -12.56f) m_angularMotorDirection.Y = -12.56f;
                    if (m_angularMotorDirection.Z > 12.56f) m_angularMotorDirection.Z = 12.56f;
                    if (m_angularMotorDirection.Z < -12.56f) m_angularMotorDirection.Z = -12.56f;
                    UpdateAngDecay();
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    if (pValue.X < 0.1f) pValue.X = 0.1f;
                    if (pValue.Y < 0.1f) pValue.Y = 0.1f;
                    if (pValue.Z < 0.1f) pValue.Z = 0.1f;
                    m_linearFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);	// velocity requested by LSL, for max limiting
                    UpdateLinDecay();
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    // m_linearMotorOffset = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
            }

        }//end ProcessVectorVehicleParam

        internal void ProcessRotationVehicleParam(Vehicle pParam, Quaternion pValue)
        {
            switch (pParam)
            {
                case Vehicle.REFERENCE_FRAME:
                    // m_referenceFrame = pValue;
                    break;
            }

        }//end ProcessRotationVehicleParam

        internal void ProcessVehicleFlags(int pParam, bool remove)
        {
            if (remove)
            {
                m_flags &= ~((VehicleFlag)pParam);
            }
            else
            {
                m_flags |= (VehicleFlag)pParam;
            }
        }

        internal void ProcessTypeChange(Vehicle pType)
        {
            // Set Defaults For Type
            m_type = pType;
            switch (pType)
            {
                case Vehicle.TYPE_SLED:
                    m_linearFrictionTimescale = new Vector3(30, 1, 1000);
                    m_angularFrictionTimescale = new Vector3(30, 30, 30);
                    //                     m_lLinMotorVel = Vector3.Zero;
                    m_linearMotorTimescale = 1000;
                    m_linearMotorDecayTimescale = 120;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorDVel = Vector3.Zero;
                    m_angularMotorTimescale = 1000;
                    m_angularMotorDecayTimescale = 120;
                    m_VhoverHeight = 0;
                    //                    m_VhoverEfficiency = 1;
                    m_VhoverTimescale = 10;
                    m_VehicleBuoyancy = 0;
                    // m_linearDeflectionEfficiency = 1;
                    // m_linearDeflectionTimescale = 1;
                    // m_angularDeflectionEfficiency = 1;
                    // m_angularDeflectionTimescale = 1000;
                    // m_bankingEfficiency = 0;
                    // m_bankingMix = 1;
                    // m_bankingTimescale = 10;
                    // m_referenceFrame = Quaternion.Identity;
                    m_flags &=
                        ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                          VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_ROLL_ONLY | VehicleFlag.LIMIT_MOTOR_UP);
                    break;
                case Vehicle.TYPE_CAR:
                    m_linearFrictionTimescale = new Vector3(100, 2, 1000);
                    m_angularFrictionTimescale = new Vector3(30, 30, 30);		// was 1000, but sl max frict time is 30.
                    //                     m_lLinMotorVel = Vector3.Zero;
                    m_linearMotorTimescale = 1;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorDVel = Vector3.Zero;
                    m_angularMotorTimescale = 1;
                    m_angularMotorDecayTimescale = 0.8f;
                    m_VhoverHeight = 0;
                    //                    m_VhoverEfficiency = 0;
                    m_VhoverTimescale = 1000;
                    m_VehicleBuoyancy = 0;
                    // // m_linearDeflectionEfficiency = 1;
                    // // m_linearDeflectionTimescale = 2;
                    // // m_angularDeflectionEfficiency = 0;
                    // m_angularDeflectionTimescale = 10;
                    m_verticalAttractionEfficiency = 1f;
                    m_verticalAttractionTimescale = 10f;
                    // m_bankingEfficiency = -0.2f;
                    // m_bankingMix = 1;
                    // m_bankingTimescale = 1;
                    // m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_ROLL_ONLY | VehicleFlag.HOVER_UP_ONLY |
                                VehicleFlag.LIMIT_MOTOR_UP);
                    break;
                case Vehicle.TYPE_BOAT:
                    m_linearFrictionTimescale = new Vector3(10, 3, 2);
                    m_angularFrictionTimescale = new Vector3(10, 10, 10);
                    //                     m_lLinMotorVel = Vector3.Zero;
                    m_linearMotorTimescale = 5;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorDVel = Vector3.Zero;
                    m_angularMotorTimescale = 4;
                    m_angularMotorDecayTimescale = 4;
                    m_VhoverHeight = 0;
                    //                    m_VhoverEfficiency = 0.5f;
                    m_VhoverTimescale = 2;
                    m_VehicleBuoyancy = 1;
                    // m_linearDeflectionEfficiency = 0.5f;
                    // m_linearDeflectionTimescale = 3;
                    // m_angularDeflectionEfficiency = 0.5f;
                    // m_angularDeflectionTimescale = 5;
                    m_verticalAttractionEfficiency = 0.5f;
                    m_verticalAttractionTimescale = 5f;
                    // m_bankingEfficiency = -0.3f;
                    // m_bankingMix = 0.8f;
                    // m_bankingTimescale = 1;
                    // m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.HOVER_TERRAIN_ONLY | VehicleFlag.LIMIT_ROLL_ONLY |
                            VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.HOVER_WATER_ONLY |
                                VehicleFlag.LIMIT_MOTOR_UP);
                    break;
                case Vehicle.TYPE_AIRPLANE:
                    m_linearFrictionTimescale = new Vector3(200, 10, 5);
                    m_angularFrictionTimescale = new Vector3(20, 20, 20);
                    //                     m_lLinMotorVel = Vector3.Zero;
                    m_linearMotorTimescale = 2;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorDVel = Vector3.Zero;
                    m_angularMotorTimescale = 4;
                    m_angularMotorDecayTimescale = 4;
                    m_VhoverHeight = 0;
                    //                    m_VhoverEfficiency = 0.5f;
                    m_VhoverTimescale = 1000;
                    m_VehicleBuoyancy = 0;
                    // m_linearDeflectionEfficiency = 0.5f;
                    // m_linearDeflectionTimescale = 3;
                    // m_angularDeflectionEfficiency = 1;
                    // m_angularDeflectionTimescale = 2;
                    m_verticalAttractionEfficiency = 0.9f;
                    m_verticalAttractionTimescale = 2f;
                    // m_bankingEfficiency = 1;
                    // m_bankingMix = 0.7f;
                    // m_bankingTimescale = 2;
                    // m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                        VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY | VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY);
                    break;
                case Vehicle.TYPE_BALLOON:
                    m_linearFrictionTimescale = new Vector3(5, 5, 5);
                    m_angularFrictionTimescale = new Vector3(10, 10, 10);
                    m_linearMotorTimescale = 5;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorDVel = Vector3.Zero;
                    m_angularMotorTimescale = 6;
                    m_angularMotorDecayTimescale = 10;
                    m_VhoverHeight = 5;
                    //                    m_VhoverEfficiency = 0.8f;
                    m_VhoverTimescale = 10;
                    m_VehicleBuoyancy = 1;
                    // m_linearDeflectionEfficiency = 0;
                    // m_linearDeflectionTimescale = 5;
                    // m_angularDeflectionEfficiency = 0;
                    // m_angularDeflectionTimescale = 5;
                    m_verticalAttractionEfficiency = 1f;
                    m_verticalAttractionTimescale = 100f;
                    // m_bankingEfficiency = 0;
                    // m_bankingMix = 0.7f;
                    // m_bankingTimescale = 5;
                    // m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                        VehicleFlag.HOVER_UP_ONLY | VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    break;

            }
        }//end SetDefaultsForType

        internal void Enable(IntPtr pBody, OdeScene pParentScene)
        {
            if (m_type == Vehicle.TYPE_NONE)
                return;

            m_body = pBody;
        }


        internal void Halt()
        {	// Kill all motions, when non-physical
            //	m_linearMotorDirection = Vector3.Zero;
            m_lLinMotorDVel = Vector3.Zero;
            m_lLinObjectVel = Vector3.Zero;
            m_wLinObjectVel = Vector3.Zero;
            m_angularMotorDirection = Vector3.Zero;
            m_lastAngularVelocity = Vector3.Zero;
            m_angularMotorDVel = Vector3.Zero;
            _acceleration = Vector3.Zero;
        }

        private void UpdateLinDecay()
        {
            m_lLinMotorDVel.X = m_linearMotorDirection.X;
            m_lLinMotorDVel.Y = m_linearMotorDirection.Y;
            m_lLinMotorDVel.Z = m_linearMotorDirection.Z;
        } // else let the motor decay on its own

        private void UpdateAngDecay()
        {
            m_angularMotorDVel.X = m_angularMotorDirection.X;
            m_angularMotorDVel.Y = m_angularMotorDirection.Y;
            m_angularMotorDVel.Z = m_angularMotorDirection.Z;
        } // else let the motor decay on its own

        public void Move(float timestep)
        {
            float fx = 0;
            float fy = 0;
            float fz = 0;
            Vector3 linvel;				// velocity applied, including any reversal

            // If geomCrossingFailuresBeforeOutofbounds is set to 0 in OpenSim.ini then phys objects bounce off region borders.
            // This is a temp patch until proper region crossing is developed. 


            if (IsPhysical && (Body != IntPtr.Zero) && !m_isSelected && !childPrim && !m_outofBounds)		// Only move root prims.
            {
                //  Old public void UpdatePositionAndVelocity(), more accuratley calculated here
                bool lastZeroFlag = _zeroFlag;  // was it stopped

                d.Vector3 vec = d.BodyGetPosition(Body);
                Vector3 l_position = Vector3.Zero;
                l_position.X = vec.X;
                l_position.Y = vec.Y;
                l_position.Z = vec.Z;
                m_lastposition = _position;
                _position = l_position;

                d.Quaternion ori = d.BodyGetQuaternion(Body);
                //       Quaternion l_orientation = Quaternion.Identity;
                _orientation.X = ori.X;
                _orientation.Y = ori.Y;
                _orientation.Z = ori.Z;
                _orientation.W = ori.W;
                m_lastorientation = _orientation;

                d.Vector3 vel = d.BodyGetLinearVel(Body);
                m_lastVelocity = _velocity;
                _velocity.X = vel.X;
                _velocity.Y = vel.Y;
                _velocity.Z = vel.Z;
                _acceleration = ((_velocity - m_lastVelocity) / timestep);

                d.Vector3 torque = d.BodyGetTorque(Body);
                _torque = new Vector3(torque.X, torque.Y, torque.Z);


                if (_position.X < 0f || _position.X > _parent_scene.WorldExtents.X
                    || _position.Y < 0f || _position.Y > _parent_scene.WorldExtents.Y
                    )
                {
                    // we are outside current region
                    // clip position to a stop just outside region and stop it only internally
                    // do it only once using m_crossingfailures as control
                    _position.X = Util.Clip(l_position.X, -0.2f, _parent_scene.WorldExtents.X + .2f);
                    _position.Y = Util.Clip(l_position.Y, -0.2f, _parent_scene.WorldExtents.Y + .2f);
                    _position.Z = Util.Clip(l_position.Z, -100f, 50000f);
                    d.BodySetPosition(Body, _position.X, _position.Y, _position.Z);
                    d.BodySetLinearVel(Body, 0, 0, 0);
                    m_outofBounds = true;
                    base.RequestPhysicsterseUpdate();
                    return;
                }

                base.RequestPhysicsterseUpdate();

                if (l_position.Z < 0)
                {
                    // This is so prim that get lost underground don't fall forever and suck up
                    //
                    // Sim resources and memory.
                    // Disables the prim's movement physics....
                    // It's a hack and will generate a console message if it fails.

                    //IsPhysical = false;
                    if (_parent == null) base.RaiseOutOfBounds(_position);


                    _acceleration.X = 0;			// This stuff may stop client display but it has no
                    _acceleration.Y = 0;			// effect on the object in phys engine!
                    _acceleration.Z = 0;

                    _velocity.X = 0;
                    _velocity.Y = 0;
                    _velocity.Z = 0;
                    m_lastVelocity = Vector3.Zero;
                    m_rotationalVelocity.X = 0;
                    m_rotationalVelocity.Y = 0;
                    m_rotationalVelocity.Z = 0;

                    if (_parent == null) base.RequestPhysicsterseUpdate();

                    m_throttleUpdates = false;
                    throttleCounter = 0;
                    _zeroFlag = true;
                    //outofBounds = true;
                }  // end neg Z check

                // Is it moving?
                /*   if ((Math.Abs(m_lastposition.X - l_position.X) < 0.02)
                       && (Math.Abs(m_lastposition.Y - l_position.Y) < 0.02)
                       && (Math.Abs(m_lastposition.Z - l_position.Z) < 0.02) */
                if ((Vector3.Mag(_velocity) < 0.01) &&                         // moving very slowly
                     (Vector3.Mag(_velocity) < Vector3.Mag(m_lastVelocity)) &&  // decelerating
                     (1.0 - Math.Abs(Quaternion.Dot(m_lastorientation, _orientation)) < 0.0001))  // spinning very slowly
                {
                    _zeroFlag = true;
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
                {		// Its stopped
                    _velocity.X = 0.0f;
                    _velocity.Y = 0.0f;
                    //        _velocity.Z = 0.0f;

                    _acceleration.X = 0;
                    _acceleration.Y = 0;
                    //        _acceleration.Z = 0;

                    m_rotationalVelocity.X = 0;
                    m_rotationalVelocity.Y = 0;
                    m_rotationalVelocity.Z = 0;
                    // Stop it in the phys engine
                    d.BodySetLinearVel(Body, 0.0f, 0.0f, _velocity.Z);
                    d.BodySetAngularVel(Body, 0.0f, 0.0f, 0.0f);
                    d.BodySetForce(Body, 0f, 0f, 0f);

                    if (!m_lastUpdateSent)
                    {
                        m_throttleUpdates = false;
                        throttleCounter = 0;
                        if (_parent == null)
                        {
                            base.RequestPhysicsterseUpdate();
                        }

                        m_lastUpdateSent = true;
                    }
                }
                else
                {			// Its moving
                    if (lastZeroFlag != _zeroFlag)
                    {
                        if (_parent == null)
                        {
                            base.RequestPhysicsterseUpdate();
                        }
                    }
                    m_lastUpdateSent = false;
                    if (!m_throttleUpdates || throttleCounter > _parent_scene.geomUpdatesPerThrottledUpdate)
                    {
                        if (_parent == null)
                        {
                            base.RequestPhysicsterseUpdate();
                        }
                    }
                    else
                    {
                        throttleCounter++;
                    }
                }
                m_lastposition = l_position;

                /// End UpdatePositionAndVelocity insert     


                // Rotation lock  =====================================
                if (m_rotateEnableUpdate)
                {
                    // Snapshot current angles, set up Amotor(s)
                    m_rotateEnableUpdate = false;
                    m_rotateEnable = m_rotateEnableRequest;
                    //Console.WriteLine("RotEnable {0} = {1}",m_primName,  m_rotateEnable);

                    if (Amotor != IntPtr.Zero)
                    {
                        d.JointDestroy(Amotor);
                        Amotor = IntPtr.Zero;
                        //Console.WriteLine("Old Amotor Destroyed");                        
                    }

                    if (!m_rotateEnable.ApproxEquals(Vector3.One, 0.003f))
                    {   // not all are enabled
                        d.Quaternion r = d.BodyGetQuaternion(Body);
                        Quaternion locrot = new Quaternion(r.X, r.Y, r.Z, r.W);
                        // extract the axes vectors
                        Vector3 vX = new Vector3(1f, 0f, 0f);
                        Vector3 vY = new Vector3(0f, 1f, 0f);
                        Vector3 vZ = new Vector3(0f, 0f, 1f);
                        vX = vX * locrot;
                        vY = vY * locrot;
                        vZ = vZ * locrot;
                        // snapshot the current angle vectors
                        m_lockX = vX;
                        m_lockY = vY;
                        m_lockZ = vZ;
                        //           m_lockRot = locrot;
                        Amotor = d.JointCreateAMotor(_parent_scene.world, IntPtr.Zero);
                        d.JointAttach(Amotor, Body, IntPtr.Zero);
                        d.JointSetAMotorMode(Amotor, 0);  // User mode??
                        //Console.WriteLine("New Amotor Created for {0}", m_primName);                   

                        float axisnum = 3;  // how many to lock
                        axisnum = (axisnum - (m_rotateEnable.X + m_rotateEnable.Y + m_rotateEnable.Z));
                        d.JointSetAMotorNumAxes(Amotor, (int)axisnum);
                        //Console.WriteLine("AxisNum={0}",(int)axisnum);                       

                        int i = 0;

                        if (m_rotateEnable.X == 0)
                        {
                            d.JointSetAMotorAxis(Amotor, i, 0, m_lockX.X, m_lockX.Y, m_lockX.Z);
                            //Console.WriteLine("AxisX {0} set to {1}", i,  m_lockX);                           
                            i++;
                        }

                        if (m_rotateEnable.Y == 0)
                        {
                            d.JointSetAMotorAxis(Amotor, i, 0, m_lockY.X, m_lockY.Y, m_lockY.Z);
                            //Console.WriteLine("AxisY {0} set to {1}", i,  m_lockY);                           
                            i++;
                        }

                        if (m_rotateEnable.Z == 0)
                        {
                            d.JointSetAMotorAxis(Amotor, i, 0, m_lockZ.X, m_lockZ.Y, m_lockZ.Z);
                            //Console.WriteLine("AxisZ {0} set to {1}", i,  m_lockZ);                           
                            i++;
                        }

                        // These lowstops and high stops are effectively (no wiggle room)
                        d.JointSetAMotorParam(Amotor, (int)dParam.LowStop, 0f);
                        d.JointSetAMotorParam(Amotor, (int)dParam.LoStop3, 0f);
                        d.JointSetAMotorParam(Amotor, (int)dParam.LoStop2, 0f);
                        d.JointSetAMotorParam(Amotor, (int)dParam.HiStop, 0f);
                        d.JointSetAMotorParam(Amotor, (int)dParam.HiStop3, 0f);
                        d.JointSetAMotorParam(Amotor, (int)dParam.HiStop2, 0f);
                        d.JointSetAMotorParam(Amotor, (int)dParam.Vel, 0f);
                        d.JointSetAMotorParam(Amotor, (int)dParam.Vel3, 0f);
                        d.JointSetAMotorParam(Amotor, (int)dParam.Vel2, 0f);
                        d.JointSetAMotorParam(Amotor, (int)dParam.StopCFM, 0f);
                        d.JointSetAMotorParam(Amotor, (int)dParam.StopCFM3, 0f);
                        d.JointSetAMotorParam(Amotor, (int)dParam.StopCFM2, 0f);
                    } // else none are locked
                } // end Rotation Update  


                // VEHICLE processing ==========================================            
                if (m_type != Vehicle.TYPE_NONE)
                {
                    // get body attitude
                    d.Quaternion rot = d.BodyGetQuaternion(Body);
                    Quaternion rotq = new Quaternion(rot.X, rot.Y, rot.Z, rot.W);	// rotq = rotation of object
                    Quaternion irotq = Quaternion.Inverse(rotq);

                    // VEHICLE Linear Motion
                    d.Vector3 velnow = d.BodyGetLinearVel(Body);					// this is in world frame
                    Vector3 vel_now = new Vector3(velnow.X, velnow.Y, velnow.Z);
                    m_lLinObjectVel = vel_now * irotq;
                    if (m_linearMotorDecayTimescale < 300.0f) //setting of 300 or more disables decay rate
                    {
                        if (Vector3.Mag(m_lLinMotorDVel) < 1.0f)
                        {
                            float decayfactor = m_linearMotorDecayTimescale / timestep;
                            Vector3 decayAmount = (m_lLinMotorDVel / decayfactor);
                            m_lLinMotorDVel -= decayAmount;
                        }
                        else
                        {
                            float decayfactor = 3.0f - (0.57f * (float)Math.Log((double)(m_linearMotorDecayTimescale)));
                            Vector3 decel = Vector3.Normalize(m_lLinMotorDVel) * decayfactor * timestep;
                            m_lLinMotorDVel -= decel;
                        }
                        if (m_lLinMotorDVel.ApproxEquals(Vector3.Zero, 0.01f))
                        {
                            m_lLinMotorDVel = Vector3.Zero;
                        }

                        /*	else
                            {
                                if (Math.Abs(m_lLinMotorDVel.X) <  Math.Abs(m_lLinObjectVel.X)) m_lLinObjectVel.X = m_lLinMotorDVel.X;
                                if (Math.Abs(m_lLinMotorDVel.Y) <  Math.Abs(m_lLinObjectVel.Y)) m_lLinObjectVel.Y = m_lLinMotorDVel.Y;
                                if (Math.Abs(m_lLinMotorDVel.Z) <  Math.Abs(m_lLinObjectVel.Z)) m_lLinObjectVel.Z = m_lLinMotorDVel.Z;	
                            } */
                    }  // end linear motor decay

                    if ((!m_lLinMotorDVel.ApproxEquals(Vector3.Zero, 0.01f)) || (!m_lLinObjectVel.ApproxEquals(Vector3.Zero, 0.01f)))
                    {
                        if (!d.BodyIsEnabled(Body)) d.BodyEnable(Body);
                        if (m_linearMotorTimescale < 300.0f)
                        {
                            Vector3 attack_error = m_lLinMotorDVel - m_lLinObjectVel;
                            float linfactor = m_linearMotorTimescale / timestep;
                            Vector3 attackAmount = (attack_error / linfactor) * 1.3f;
                            m_lLinObjectVel += attackAmount;
                        }
                        if (m_linearFrictionTimescale.X < 300.0f)
                        {
                            float fricfactor = m_linearFrictionTimescale.X / timestep;
                            float fricX = m_lLinObjectVel.X / fricfactor;
                            m_lLinObjectVel.X -= fricX;
                        }
                        if (m_linearFrictionTimescale.Y < 300.0f)
                        {
                            float fricfactor = m_linearFrictionTimescale.Y / timestep;
                            float fricY = m_lLinObjectVel.Y / fricfactor;
                            m_lLinObjectVel.Y -= fricY;
                        }
                        if (m_linearFrictionTimescale.Z < 300.0f)
                        {
                            float fricfactor = m_linearFrictionTimescale.Z / timestep;
                            float fricZ = m_lLinObjectVel.Z / fricfactor;
                            m_lLinObjectVel.Z -= fricZ;
                        }
                    }
                    m_wLinObjectVel = m_lLinObjectVel * rotq;

                    // Gravity and Buoyancy
                    Vector3 grav = Vector3.Zero;
                    if (m_VehicleBuoyancy < 1.0f)
                    {
                        // There is some gravity, make a gravity force vector
                        // that is applied after object velocity.     
                        d.Mass objMass;
                        d.BodyGetMass(Body, out objMass);
                        // m_VehicleBuoyancy: -1=2g; 0=1g; 1=0g; 
                        grav.Z = _parent_scene.gravityz * objMass.mass * (1f - m_VehicleBuoyancy); // Applied later as a force
                    } // else its 1.0, no gravity.

                    // Hovering
                    if ((m_flags & (VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT)) != 0)
                    {
                        // We should hover, get the target height
                        d.Vector3 pos = d.BodyGetPosition(Body);
                        if ((m_flags & VehicleFlag.HOVER_WATER_ONLY) == VehicleFlag.HOVER_WATER_ONLY)
                        {
                            m_VhoverTargetHeight = _parent_scene.GetWaterLevel() + m_VhoverHeight;
                        }
                        else if ((m_flags & VehicleFlag.HOVER_TERRAIN_ONLY) == VehicleFlag.HOVER_TERRAIN_ONLY)
                        {
                            m_VhoverTargetHeight = _parent_scene.GetTerrainHeightAtXY(pos.X, pos.Y) + m_VhoverHeight;
                        }
                        else if ((m_flags & VehicleFlag.HOVER_GLOBAL_HEIGHT) == VehicleFlag.HOVER_GLOBAL_HEIGHT)
                        {
                            m_VhoverTargetHeight = m_VhoverHeight;
                        }

                        if ((m_flags & VehicleFlag.HOVER_UP_ONLY) == VehicleFlag.HOVER_UP_ONLY)
                        {
                            // If body is aready heigher, use its height as target height
                            if (pos.Z > m_VhoverTargetHeight) m_VhoverTargetHeight = pos.Z;
                        }

                        //	            m_VhoverEfficiency = 0f;	// 0=boucy, 1=Crit.damped
                        //				m_VhoverTimescale = 0f;		// time to acheive height
                        //				timestep  is time since last frame,in secs 
                        float herr0 = pos.Z - m_VhoverTargetHeight;
                        // Replace Vertical speed with correction figure if significant
                        if (Math.Abs(herr0) > 0.01f)
                        {
                            //?          d.Mass objMass;
                            //?          d.BodyGetMass(Body, out objMass);
                            m_wLinObjectVel.Z = -((herr0 * timestep * 50.0f) / m_VhoverTimescale);
                            //KF: m_VhoverEfficiency is not yet implemented
                        }
                        else
                        {
                            m_wLinObjectVel.Z = 0f;
                        }
                    }
                    else
                    {	// not hovering
                        if (m_wLinObjectVel.Z == 0f)
                        {		// Gravity rules
                            m_wLinObjectVel.Z = vel_now.Z;
                        }  // else the motor has it
                    }
                    linvel = m_wLinObjectVel;

                    // Vehicle Linear  Motion done =======================================
                    // Apply velocity
                    d.BodySetLinearVel(Body, linvel.X, linvel.Y, linvel.Z);
                    // apply gravity force
                    d.BodyAddForce(Body, grav.X, grav.Y, grav.Z);
                    //if(frcount == 0) Console.WriteLine("Vel={0}    Force={1}",linvel ,  grav);
                    // end MoveLinear()


                    // MoveAngular
                    /*
                    private Vector3 m_angularMotorDirection = Vector3.Zero;			// angular velocity requested by LSL motor 
        
                    private float m_angularMotorTimescale = 0;						// motor angular Attack rate set by LSL
                    private float m_angularMotorDecayTimescale = 0;					// motor angular Decay rate set by LSL
                    private Vector3 m_angularFrictionTimescale = Vector3.Zero;		// body angular Friction set by LSL

                    private Vector3 m_angularMotorDVel = Vector3.Zero;				// decayed angular motor
                    private Vector3 m_angObjectVel = Vector3.Zero;					// what was last applied to body
                    */
                    //if(frcount == 0) Console.WriteLine("MoveAngular ");	

                    d.Vector3 angularObjectVel = d.BodyGetAngularVel(Body);
                    Vector3 angObjectVel = new Vector3(angularObjectVel.X, angularObjectVel.Y, angularObjectVel.Z);
                    angObjectVel = angObjectVel * irotq;	// ============ Converts to LOCAL rotation

                    //if(frcount == 0) Console.WriteLine("V0 = {0}", angObjectVel);        	

                    // Decay Angular Motor 1. In SL this also depends on attack rate! decay ~= 23/Attack.
                    float atk_decayfactor = 23.0f / (m_angularMotorTimescale * timestep);
                    m_angularMotorDVel -= m_angularMotorDVel / atk_decayfactor;
                    // Decay Angular Motor 2.
                    if (m_angularMotorDecayTimescale < 300.0f)
                    {
                        if (Vector3.Mag(m_angularMotorDVel) < 1.0f)
                        {
                            float decayfactor = (m_angularMotorDecayTimescale) / timestep;
                            Vector3 decayAmount = (m_angularMotorDVel / decayfactor);
                            m_angularMotorDVel -= decayAmount;
                        }
                        else
                        {
                            Vector3 decel = Vector3.Normalize(m_angularMotorDVel) * timestep / m_angularMotorDecayTimescale;
                            m_angularMotorDVel -= decel;
                        }

                        if (m_angularMotorDVel.ApproxEquals(Vector3.Zero, 0.01f))
                        {
                            m_angularMotorDVel = Vector3.Zero;
                        }
                        else
                        {
                            if (Math.Abs(m_angularMotorDVel.X) < Math.Abs(angObjectVel.X)) angObjectVel.X = m_angularMotorDVel.X;
                            if (Math.Abs(m_angularMotorDVel.Y) < Math.Abs(angObjectVel.Y)) angObjectVel.Y = m_angularMotorDVel.Y;
                            if (Math.Abs(m_angularMotorDVel.Z) < Math.Abs(angObjectVel.Z)) angObjectVel.Z = m_angularMotorDVel.Z;
                        }
                    } // end decay angular motor
                    //if(frcount == 0) Console.WriteLine("MotorDvel {0}    Obj {1}", m_angularMotorDVel, angObjectVel);

                    //if(frcount == 0) Console.WriteLine("VA = {0}", angObjectVel);   

                    if ((!m_angularMotorDVel.ApproxEquals(Vector3.Zero, 0.01f)) || (!angObjectVel.ApproxEquals(Vector3.Zero, 0.01f)))
                    {  // if motor or object have motion
                        if (!d.BodyIsEnabled(Body)) d.BodyEnable(Body);

                        if (m_angularMotorTimescale < 300.0f)
                        {
                            Vector3 attack_error = m_angularMotorDVel - angObjectVel;
                            float angfactor = m_angularMotorTimescale / timestep;
                            Vector3 attackAmount = (attack_error / angfactor);
                            angObjectVel += attackAmount;
                            //if(frcount == 0) Console.WriteLine("Accel {0}      Attk {1}",FrAaccel, attackAmount);                	
                            //if(frcount == 0) Console.WriteLine("V2+= {0}", angObjectVel);        	
                        }

                        angObjectVel.X -= angObjectVel.X / (m_angularFrictionTimescale.X * 0.7f / timestep);
                        angObjectVel.Y -= angObjectVel.Y / (m_angularFrictionTimescale.Y * 0.7f / timestep);
                        angObjectVel.Z -= angObjectVel.Z / (m_angularFrictionTimescale.Z * 0.7f / timestep);
                    } // else no signif. motion

                    //if(frcount == 0) Console.WriteLine("Dmotor {0}      Obj {1}", m_angularMotorDVel, angObjectVel);
                    // Bank section tba
                    // Deflection section tba
                    //if(frcount == 0) Console.WriteLine("V3 = {0}", angObjectVel);        	


                    /*			// Rotation Axis Disables:
                                if (!m_angularEnable.ApproxEquals(Vector3.One, 0.003f))
                                {
                                    if (m_angularEnable.X == 0)
                                        angObjectVel.X = 0f;
                                    if (m_angularEnable.Y == 0)
                                        angObjectVel.Y = 0f;
                                    if (m_angularEnable.Z == 0)
                                        angObjectVel.Z = 0f;
                                }        
                        */
                    angObjectVel = angObjectVel * rotq; // ================ Converts to WORLD rotation

                    // Vertical attractor section
                    Vector3 vertattr = Vector3.Zero;

                    if (m_verticalAttractionTimescale < 300)
                    {
                        float VAservo = 1.0f / (m_verticalAttractionTimescale * timestep);
                        // make a vector pointing up
                        Vector3 verterr = Vector3.Zero;
                        verterr.Z = 1.0f;
                        // rotate it to Body Angle
                        verterr = verterr * rotq;
                        // verterr.X and .Y are the World error ammounts. They are 0 when there is no error (Vehicle Body is 'vertical'), and .Z will be 1.
                        // As the body leans to its side |.X| will increase to 1 and .Z fall to 0. As body inverts |.X| will fall and .Z will go
                        // negative. Similar for tilt and |.Y|. .X and .Y must be modulated to prevent a stable inverted body.

                        if (verterr.Z < 0.0f)
                        {	// Deflection from vertical exceeds 90-degrees. This method will ensure stable return to
                            // vertical, BUT for some reason a z-rotation is imparted to the object. TBI.
                            //Console.WriteLine("InvertFlip");	
                            verterr.X = 2.0f - verterr.X;
                            verterr.Y = 2.0f - verterr.Y;
                        }
                        verterr *= 0.5f;
                        // verterror is 0 (no error) to +/- 1 (max error at 180-deg tilt)
                        Vector3 xyav = angObjectVel;
                        xyav.Z = 0.0f;
                        if ((!xyav.ApproxEquals(Vector3.Zero, 0.001f)) || (verterr.Z < 0.49f))
                        {
                            // As the body rotates around the X axis, then verterr.Y increases; Rotated around Y then .X increases, so 
                            // Change  Body angular velocity  X based on Y, and Y based on X. Z is not changed.
                            vertattr.X = verterr.Y;
                            vertattr.Y = -verterr.X;
                            vertattr.Z = 0f;
                            //if(frcount == 0) Console.WriteLine("VAerr=" + verterr);	

                            // scaling appears better usingsquare-law
                            float damped = m_verticalAttractionEfficiency * m_verticalAttractionEfficiency;
                            float bounce = 1.0f - damped;
                            // 0 = crit damp, 1 = bouncy
                            float oavz = angObjectVel.Z;   // retain z velocity
                            // time-scaled correction, which sums, therefore is bouncy:
                            angObjectVel = (angObjectVel + (vertattr * VAservo * 0.0333f)) * bounce;
                            // damped, good @ < 90:
                            angObjectVel = angObjectVel + (vertattr * VAservo * 0.0667f * damped);
                            angObjectVel.Z = oavz;
                            //if(frcount == 0) Console.WriteLine("VA+");					
                            //Console.WriteLine("VAttr {0}         OAvel {1}", vertattr, angObjectVel);
                        }
                        else
                        {
                            // else error is very small
                            angObjectVel.X = 0f;
                            angObjectVel.Y = 0f;
                            //if(frcount == 0) Console.WriteLine("VA0");					
                        }
                    } // else vertical attractor is off
                    //if(frcount == 0) Console.WriteLine("V1 = {0}", angObjectVel);        	


                    m_lastAngularVelocity = angObjectVel;
                    // apply Angular Velocity to body
                    d.BodySetAngularVel(Body, m_lastAngularVelocity.X, m_lastAngularVelocity.Y, m_lastAngularVelocity.Z);
                    //if(frcount == 0) Console.WriteLine("V4 = {0}", m_lastAngularVelocity);        	

                }  // end VEHICLES
                else
                {
                    // Dyamics (NON-'VEHICLES') are dealt with here ================================================================	                

                    if (!d.BodyIsEnabled(Body)) d.BodyEnable(Body); // KF add 161009

                    /// Dynamics Buoyancy
                    //KF: m_buoyancy is set by llSetBuoyancy() and is for non-vehicle.
                    // m_buoyancy: (unlimited value) <0=Falls fast; 0=1g; 1=0g; >1 = floats up 
                    // NB Prims in ODE are no subject to global gravity
                    // This should only affect gravity operations 

                    float m_mass = CalculateMass();
                    // calculate z-force due togravity on object.
                    fz = _parent_scene.gravityz * (1.0f - m_buoyancy) * m_mass; // force = acceleration * mass
                    if ((m_usePID) && (m_PIDTau > 0.0f))  // Dynamics  llMoveToTarget.
                    {
                        fz = 0;     // llMoveToTarget ignores gravity.
                        // it also ignores mass of object, and any physical resting on it.
                        // Vector3 m_PIDTarget is where we are going
                        // float m_PIDTau is time to get there
                        fx = 0;
                        fy = 0;
                        d.Vector3 pos = d.BodyGetPosition(Body);
                        Vector3 error = new Vector3(
                                (m_PIDTarget.X - pos.X),
                                (m_PIDTarget.Y - pos.Y),
                                (m_PIDTarget.Z - pos.Z));
                        if (error.ApproxEquals(Vector3.Zero, 0.01f))
                        {   // Very close, Jump there and quit move

                            d.BodySetPosition(Body, m_PIDTarget.X, m_PIDTarget.Y, m_PIDTarget.Z);
                            _target_velocity = Vector3.Zero;
                            d.BodySetLinearVel(Body, _target_velocity.X, _target_velocity.Y, _target_velocity.Z);
                            d.BodySetForce(Body, 0f, 0f, 0f);
                        }
                        else
                        {
                            float scale = 50.0f * timestep / m_PIDTau;
                            if ((error.ApproxEquals(Vector3.Zero, 0.5f)) && (_target_velocity != Vector3.Zero))
                            {
                                // Nearby, quit update of velocity
                            }
                            else
                            {  // Far, calc damped velocity
                                _target_velocity = error * scale;
                            }
                            d.BodySetLinearVel(Body, _target_velocity.X, _target_velocity.Y, _target_velocity.Z);
                        }
                    } // end PID MoveToTarget


                    /// Dynamics Hover ===================================================================================
                    // Hover PID Controller can only run if the PIDcontroller is not in use.
                    if (m_useHoverPID && !m_usePID)
                    {
                        //Console.WriteLine("Hover " +  m_primName);           	

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


                        // Where are we, and where are we headed?
                        d.Vector3 pos = d.BodyGetPosition(Body);
                        //	                    d.Vector3 vel = d.BodyGetLinearVel(Body);


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

                        } 	// end switch (m_PIDHoverType)


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
                            d.Vector3 dlinvel = vel;
                            d.BodySetPosition(Body, pos.X, pos.Y, m_targetHoverHeight);
                            d.BodySetLinearVel(Body, dlinvel.X, dlinvel.Y, dlinvel.Z);
                            d.BodyAddForce(Body, 0, 0, fz);
                            //KF this prevents furthur motions         return;
                        }
                        else
                        {
                            _zeroFlag = false;

                            // We're flying and colliding with something
                            fz = fz + ((_target_velocity.Z - vel.Z) * (PID_D) * m_mass);
                        }
                    } // end m_useHoverPID && !m_usePID


                    /// Dynamics Apply Forces ===================================================================================
                    fx *= m_mass;
                    fy *= m_mass;
                    //fz *= m_mass;
                    fx += m_force.X;
                    fy += m_force.Y;
                    fz += m_force.Z;

                    //m_log.Info("[OBJPID]: X:" + fx.ToString() + " Y:" + fy.ToString() + " Z:" + fz.ToString());
                    if (fx != 0 || fy != 0 || fz != 0)
                    {
                        //m_taintdisable = true;
                        //base.RaiseOutOfBounds(Position);
                        //d.BodySetLinearVel(Body, fx, fy, 0f);
                        if (!d.BodyIsEnabled(Body))
                        {
                            // A physical body at rest on a surface will auto-disable after a while,
                            // this appears to re-enable it incase the surface it is upon vanishes,
                            // and the body should fall again. 
                            d.BodySetLinearVel(Body, 0f, 0f, 0f);
                            d.BodySetForce(Body, 0f, 0f, 0f);
                            enableBodySoft();
                        }

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
                        d.BodyAddForce(Body, fx, fy, fz);
                    }  // end apply forces
                } // end Vehicle/Dynamics

                /// RotLookAt / LookAt =================================================================================
                if (m_useAPID)
                {
                    // RotLookAt, apparently overrides all other rotation sources. Inputs:
                    // Quaternion m_APIDTarget
                    // float m_APIDStrength		// From SL experiments, this is the time to get there
                    // float m_APIDDamping		// From SL experiments, this is damping, 1.0 = damped, 0.1 = wobbly
                    // Also in SL the mass of the object has no effect on time to get there.
                    // Factors:
                    // get present body rotation
                    float limit = 1.0f;
                    float rscaler = 50f;		// adjusts rotation damping time
                    float lscaler = 10f;		// adjusts linear damping time in llLookAt
                    float RLAservo = 0f;
                    Vector3 diff_axis;
                    float diff_angle;
                    d.Quaternion rot = d.BodyGetQuaternion(Body);						// prim present rotation
                    Quaternion rotq = new Quaternion(rot.X, rot.Y, rot.Z, rot.W);
                    Quaternion rtarget = new Quaternion();

                    if (m_APIDTarget.W == -99.9f)
                    {
                        // this is really a llLookAt(), x,y,z is the target vector
                        Vector3 target = new Vector3(m_APIDTarget.X, m_APIDTarget.Y, m_APIDTarget.Z);
                        Vector3 ospin = new Vector3(1.0f, 0.0f, 0.0f) * rotq;
                        Vector3 error = new Vector3(0.0f, 0.0f, 0.0f);
                        float twopi = 2.0f * (float)Math.PI;
                        Vector3 dir = target - _position;
                        dir.Normalize();
                        float tzrot = (float)Math.Atan2(dir.Y, dir.X);
                        float txy = (float)Math.Sqrt((dir.X * dir.X) + (dir.Y * dir.Y));
                        float terot = (float)Math.Atan2(dir.Z, txy);
                        float ozrot = (float)Math.Atan2(ospin.Y, ospin.X);
                        float oxy = (float)Math.Sqrt((ospin.X * ospin.X) + (ospin.Y * ospin.Y));
                        float oerot = (float)Math.Atan2(ospin.Z, oxy);
                        float ra = 2.0f * ((rotq.W * rotq.X) + (rotq.Y * rotq.Z));
                        float rb = 1.0f - 2.0f * ((rotq.Y * rotq.Y) + (rotq.X * rotq.X));
                        float roll = (float)Math.Atan2(ra, rb);
                        float errorz = tzrot - ozrot;
                        if (errorz > (float)Math.PI) errorz -= twopi;
                        else if (errorz < -(float)Math.PI) errorz += twopi;
                        float errory = oerot - terot;
                        if (errory > (float)Math.PI) errory -= twopi;
                        else if (errory < -(float)Math.PI) errory += twopi;
                        diff_angle = Math.Abs(errorz) + Math.Abs(errory) + Math.Abs(roll);
                        if (diff_angle > 0.01f * m_APIDdamper)
                        {
                            m_APIDdamper = 1.0f;
                            RLAservo = timestep / m_APIDStrength * rscaler;
                            errorz *= RLAservo;
                            errory *= RLAservo;
                            error.X = -roll * 8.0f;
                            error.Y = errory;
                            error.Z = errorz;
                            error *= rotq;
                            d.BodySetAngularVel(Body, error.X, error.Y, error.Z);
                        }
                        else
                        {
                            d.BodySetAngularVel(Body, 0.0f, 0.0f, 0.0f);
                            m_APIDdamper = 2.0f;
                        }
                    }
                    else
                    {
                        // this is a llRotLookAt()
                        rtarget = m_APIDTarget;

                        Quaternion rot_diff = Quaternion.Inverse(rotq) * rtarget;		// difference to desired rot
                        rot_diff.GetAxisAngle(out diff_axis, out diff_angle);				// convert to axis to point at & error angle
                        //if(frcount == 0) Console.WriteLine("axis {0}   angle {1}",diff_axis * 57.3f, diff_angle);

                        //   diff_axis.Normalize(); it already is!
                        if (diff_angle > 0.01f * m_APIDdamper)			// diff_angle is always +ve			// if there is enough error
                        {
                            m_APIDdamper = 1.0f;
                            Vector3 rotforce = new Vector3(diff_axis.X, diff_axis.Y, diff_axis.Z);
                            rotforce = rotforce * rotq;
                            if (diff_angle > limit) diff_angle = limit;		// cap the rotate rate
                            RLAservo = timestep / m_APIDStrength * lscaler;
                            rotforce = rotforce * RLAservo * diff_angle;
                            d.BodySetAngularVel(Body, rotforce.X, rotforce.Y, rotforce.Z);
                            //Console.WriteLine("axis= " + diff_axis + "    angle= " + diff_angle + "servo= " + RLAservo);							
                        }
                        else
                        {	// close enough
                            d.BodySetAngularVel(Body, 0.0f, 0.0f, 0.0f);
                            m_APIDdamper = 2.0f;
                        }
                    } // end llLookAt/llRotLookAt
                    //if(frcount == 0) Console.WriteLine("mass= " + m_mass + "  servo= " + RLAservo + "   angle= " + diff_angle);
                } // end m_useAPID
            } // end root prims
        }  // end Move()
    } // end class
}
