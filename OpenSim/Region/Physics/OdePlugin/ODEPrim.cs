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

        private bool m_isphysical;

        public int ExpectedCollisionContacts { get { return m_expectedCollisionContacts; } }
        private int m_expectedCollisionContacts = 0;

        /// <summary>
        /// Gets collide bits so that we can still perform land collisions if a mesh fails to load.
        /// </summary>
        private int BadMeshAssetCollideBits
        {
            get { return m_isphysical ? (int)CollisionCategories.Land : 0; }
        }

        /// <summary>
        /// Is this prim subject to physics?  Even if not, it's still solid for collision purposes.
        /// </summary>
        public override bool IsPhysical
        {
            get { return m_isphysical; }
            set
            {
                m_isphysical = value;
                if (!m_isphysical) // Zero the remembered last velocity
                    m_lastVelocity = Vector3.Zero;
            }
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
        private Vector3 m_angularlock = Vector3.One;
        private Vector3 m_taintAngularLock = Vector3.One;
        private IntPtr Amotor = IntPtr.Zero;

        private object m_assetsLock = new object();
        private bool m_assetFailed = false;

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

        // Default we're a Geometry
        private CollisionCategories m_collisionCategories = (CollisionCategories.Geom);

        // Default, Collide with Other Geometries, spaces and Bodies
        private CollisionCategories m_collisionFlags = m_default_collisionFlags;

        public bool m_taintremove { get; private set; }
        public bool m_taintdisable { get; private set; }
        internal bool m_disabled;
        public bool m_taintadd { get; private set; }
        public bool m_taintselected { get; private set; }
        public bool m_taintCollidesWater { get; private set; }

        private bool m_taintforce = false;
        private bool m_taintaddangularforce = false;
        private Vector3 m_force;
        private List<Vector3> m_forcelist = new List<Vector3>();
        private List<Vector3> m_angularforcelist = new List<Vector3>();

        private PrimitiveBaseShape _pbs;
        private OdeScene _parent_scene;
        
        /// <summary>
        /// The physics space which contains prim geometries
        /// </summary>
        public IntPtr m_targetSpace = IntPtr.Zero;

        /// <summary>
        /// The prim geometry, used for collision detection.
        /// </summary>
        /// <remarks>
        /// This is never null except for a brief period when the geometry needs to be replaced (due to resizing or
        /// mesh change) or when the physical prim is being removed from the scene.
        /// </remarks>
        public IntPtr prim_geom { get; private set; }

        public IntPtr _triMeshData { get; private set; }

        private IntPtr _linkJointGroup = IntPtr.Zero;
        private PhysicsActor _parent;
        private PhysicsActor m_taintparent;

        private List<OdePrim> childrenPrim = new List<OdePrim>();

        private bool iscolliding;
        private bool m_isSelected;

        internal bool m_isVolumeDetect; // If true, this prim only detects collisions but doesn't collide actively

        private bool m_throttleUpdates;
        private int throttleCounter;
        public int m_interpenetrationcount { get; private set; }
        internal float m_collisionscore;
        public int m_roundsUnderMotionThreshold { get; private set; }
        private int m_crossingfailures;

        public bool outofBounds { get; private set; }
        private float m_density = 10.000006836f; // Aluminum g/cm3;

        public bool _zeroFlag { get; private set; }
        private bool m_lastUpdateSent;

        public IntPtr Body = IntPtr.Zero;
        private Vector3 _target_velocity;
        private d.Mass pMass;

        private int m_eventsubscription;
        private CollisionEventUpdate CollisionEventsThisFrame = new CollisionEventUpdate();

        /// <summary>
        /// Signal whether there were collisions on the previous frame, so we know if we need to send the
        /// empty CollisionEventsThisFrame to the prim so that it can detect the end of a collision.
        /// </summary>
        /// <remarks>
        /// This is probably a temporary measure, pending storing this information consistently in CollisionEventUpdate itself.
        /// </remarks>
        private bool m_collisionsOnPreviousFrame;

        private IntPtr m_linkJoint = IntPtr.Zero;

        internal volatile bool childPrim;

        private ODEDynamics m_vehicle;

        internal int m_material = (int)Material.Wood;

        public OdePrim(
            String primName, OdeScene parent_scene, Vector3 pos, Vector3 size,
            Quaternion rotation, PrimitiveBaseShape pbs, bool pisPhysical)
        {
            Name = primName;
            m_vehicle = new ODEDynamics();
            //gc = GCHandle.Alloc(prim_geom, GCHandleType.Pinned);

            if (!pos.IsFinite())
            {
                pos = new Vector3(((float)Constants.RegionSize * 0.5f), ((float)Constants.RegionSize * 0.5f),
                    parent_scene.GetTerrainHeightAtXY(((float)Constants.RegionSize * 0.5f), ((float)Constants.RegionSize * 0.5f)) + 0.5f);
                m_log.WarnFormat("[PHYSICS]: Got nonFinite Object create Position for {0}", Name);
            }
            _position = pos;
            m_taintposition = pos;
            PID_D = parent_scene.bodyPIDD;
            PID_G = parent_scene.bodyPIDG;
            m_density = parent_scene.geomDefaultDensity;
            // m_tensor = parent_scene.bodyMotorJointMaxforceTensor;
            body_autodisable_frames = parent_scene.bodyFramesAutoDisable;

            prim_geom = IntPtr.Zero;

            if (!pos.IsFinite())
            {
                size = new Vector3(0.5f, 0.5f, 0.5f);
                m_log.WarnFormat("[PHYSICS]: Got nonFinite Object create Size for {0}", Name);
            }

            if (size.X <= 0) size.X = 0.01f;
            if (size.Y <= 0) size.Y = 0.01f;
            if (size.Z <= 0) size.Z = 0.01f;

            _size = size;
            m_taintsize = _size;

            if (!QuaternionIsFinite(rotation))
            {
                rotation = Quaternion.Identity;
                m_log.WarnFormat("[PHYSICS]: Got nonFinite Object create Rotation for {0}", Name);
            }

            _orientation = rotation;
            m_taintrot = _orientation;
            _pbs = pbs;

            _parent_scene = parent_scene;
            m_targetSpace = (IntPtr)0;

            if (pos.Z < 0)
            {
                IsPhysical = false;
            }
            else
            {
                IsPhysical = pisPhysical;
                // If we're physical, we need to be in the master space for now.
                // linksets *should* be in a space together..  but are not currently
                if (IsPhysical)
                    m_targetSpace = _parent_scene.space;
            }

            m_taintadd = true;
            m_assetFailed = false;
            _parent_scene.AddPhysicsActorTaint(this);
        }

        public override int PhysicsActorType
        {
            get { return (int) ActorTypes.Prim; }
            set { return; }
        }

        public override bool SetAlwaysRun
        {
            get { return false; }
            set { return; }
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

                if ((IsPhysical && !_zeroFlag) || !value)
                {
                    m_taintselected = value;
                    _parent_scene.AddPhysicsActorTaint(this);
                }
                else
                {
                    m_taintselected = value;
                    m_isSelected = value;
                }

                if (m_isSelected)
                    disableBodySoft();
            }
        }

        /// <summary>
        /// Set a new geometry for this prim.
        /// </summary>
        /// <param name="geom"></param>
        private void SetGeom(IntPtr geom)
        {
            prim_geom = geom;
//Console.WriteLine("SetGeom to " + prim_geom + " for " + Name);

            if (m_assetFailed)
            {
                d.GeomSetCategoryBits(prim_geom, 0);
                d.GeomSetCollideBits(prim_geom, BadMeshAssetCollideBits);
            }
            else
            {
                d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
            }

            _parent_scene.geom_name_map[prim_geom] = Name;
            _parent_scene.actor_name_map[prim_geom] = this;

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

        private void enableBodySoft()
        {
            if (!childPrim)
            {
                if (IsPhysical && Body != IntPtr.Zero)
                {
                    d.BodyEnable(Body);
                    if (m_vehicle.Type != Vehicle.TYPE_NONE)
                        m_vehicle.Enable(Body, _parent_scene);
                }

                m_disabled = false;
            }
        }

        private void disableBodySoft()
        {
            m_disabled = true;

            if (IsPhysical && Body != IntPtr.Zero)
            {
                d.BodyDisable(Body);
            }
        }

        /// <summary>
        /// Make a prim subject to physics.
        /// </summary>
        private void enableBody()
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

                if (m_assetFailed)
                {
                    d.GeomSetCategoryBits(prim_geom, 0);
                    d.GeomSetCollideBits(prim_geom, BadMeshAssetCollideBits);
                }
                else
                {
                    m_collisionCategories |= CollisionCategories.Body;
                    m_collisionFlags |= (CollisionCategories.Land | CollisionCategories.Wind);
                }

                d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);

                d.BodySetAutoDisableFlag(Body, true);
                d.BodySetAutoDisableSteps(Body, body_autodisable_frames);
                
                // disconnect from world gravity so we can apply buoyancy
                d.BodySetGravityMode (Body, false);

                m_interpenetrationcount = 0;
                m_collisionscore = 0;
                m_disabled = false;

                // The body doesn't already have a finite rotation mode set here
                if ((!m_angularlock.ApproxEquals(Vector3.Zero, 0.0f)) && _parent == null)
                {
                    createAMotor(m_angularlock);
                }
                if (m_vehicle.Type != Vehicle.TYPE_NONE)
                {
                    m_vehicle.Enable(Body, _parent_scene);
                }

                _parent_scene.ActivatePrim(this);
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
                        tmp= 1.0f -2.0e-2f * (float)(200 - _pbs.PathScaleY);
                        volume -= volume*tmp*tmp;
                        
                        if (hollowAmount > 0.0)
                            {
                            hollowVolume *= hollowAmount;
                            
                            switch (_pbs.HollowShape)
                                {
                                case HollowShape.Square:
                                case HollowShape.Same:
                                    break;

                                case HollowShape.Circle:
                                    hollowVolume *= 0.78539816339f;;
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
        }

        #endregion

        private void setMass()
        {
            if (Body != (IntPtr) 0)
            {
                float newmass = CalculateMass();

                //m_log.Info("[PHYSICS]: New Mass: " + newmass.ToString());

                d.MassSetBoxTotal(out pMass, newmass, _size.X, _size.Y, _size.Z);
                d.BodySetMass(Body, ref pMass);
            }
        }

        /// <summary>
        /// Stop a prim from being subject to physics.
        /// </summary>
        internal void disableBody()
        {
            //this kills the body so things like 'mesh' can re-create it.
            lock (this)
            {
                if (!childPrim)
                {
                    if (Body != IntPtr.Zero)
                    {
                        _parent_scene.DeactivatePrim(this);
                        m_collisionCategories &= ~CollisionCategories.Body;
                        m_collisionFlags &= ~(CollisionCategories.Wind | CollisionCategories.Land);

                        if (m_assetFailed)
                        {
                            d.GeomSetCategoryBits(prim_geom, 0);
                            d.GeomSetCollideBits(prim_geom, 0);
                        }
                        else
                        {
                            d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                            d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
                        }

                        d.BodyDestroy(Body);
                        lock (childrenPrim)
                        {
                            if (childrenPrim.Count > 0)
                            {
                                foreach (OdePrim prm in childrenPrim)
                                {
                                    _parent_scene.DeactivatePrim(prm);
                                    prm.Body = IntPtr.Zero;
                                }
                            }
                        }
                        Body = IntPtr.Zero;
                    }
                }
                else
                {
                    _parent_scene.DeactivatePrim(this);
                    
                    m_collisionCategories &= ~CollisionCategories.Body;
                    m_collisionFlags &= ~(CollisionCategories.Wind | CollisionCategories.Land);

                    if (m_assetFailed)
                    {
                        d.GeomSetCategoryBits(prim_geom, 0);
                        d.GeomSetCollideBits(prim_geom, 0);
                    }
                    else
                    {

                        d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                        d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
                    }

                    Body = IntPtr.Zero;
                }
            }

            m_disabled = true;
            m_collisionscore = 0;
        }

        private static Dictionary<IMesh, IntPtr> m_MeshToTriMeshMap = new Dictionary<IMesh, IntPtr>();

        private void setMesh(OdeScene parent_scene, IMesh mesh)
        {
//            m_log.DebugFormat("[ODE PRIM]: Setting mesh on {0} to {1}", Name, mesh);

            // This sleeper is there to moderate how long it takes between
            // setting up the mesh and pre-processing it when we get rapid fire mesh requests on a single object

            //Thread.Sleep(10);

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
            m_expectedCollisionContacts = indexCount;
            mesh.releaseSourceMeshData(); // free up the original mesh data to save memory

            // We must lock here since m_MeshToTriMeshMap is static and multiple scene threads may call this method at
            // the same time.
            lock (m_MeshToTriMeshMap)
            {
                if (m_MeshToTriMeshMap.ContainsKey(mesh))
                {
                    _triMeshData = m_MeshToTriMeshMap[mesh];
                }
                else
                {
                    _triMeshData = d.GeomTriMeshDataCreate();
    
                    d.GeomTriMeshDataBuildSimple(_triMeshData, vertices, vertexStride, vertexCount, indices, indexCount, triStride);
                    d.GeomTriMeshDataPreprocess(_triMeshData);
                    m_MeshToTriMeshMap[mesh] = _triMeshData;
                }
            }

//            _parent_scene.waitForSpaceUnlock(m_targetSpace);
            try
            {
                SetGeom(d.CreateTriMesh(m_targetSpace, _triMeshData, parent_scene.triCallback, null, null));
            }
            catch (AccessViolationException)
            {
                m_log.ErrorFormat("[PHYSICS]: MESH LOCKED FOR {0}", Name);
                return;
            }

           // if (IsPhysical && Body == (IntPtr) 0)
           // {
                // Recreate the body
          //     m_interpenetrationcount = 0;
           //     m_collisionscore = 0;

           //     enableBody();
           // }
        }

        internal void ProcessTaints()
        {
#if SPAM
Console.WriteLine("ZProcessTaints for " + Name);
#endif

            // This must be processed as the very first taint so that later operations have a prim_geom to work with
            // if this is a new prim.
            if (m_taintadd)
                changeadd();

            if (!_position.ApproxEquals(m_taintposition, 0f))
                 changemove();

            if (m_taintrot != _orientation)
            {
                if (childPrim && IsPhysical)    // For physical child prim...
                {
                    rotate();
                    // KF: ODE will also rotate the parent prim!
                    // so rotate the root back to where it was
                    OdePrim parent = (OdePrim)_parent;
                    parent.rotate();
                }
                else
                {
                    //Just rotate the prim
                    rotate();
                }
            }
        
            if (m_taintPhysics != IsPhysical && !(m_taintparent != _parent))
                changePhysicsStatus();

            if (!_size.ApproxEquals(m_taintsize, 0f))
                changesize();

            if (m_taintshape)
                changeshape();

            if (m_taintforce)
                changeAddForce();

            if (m_taintaddangularforce)
                changeAddAngularForce();

            if (!m_taintTorque.ApproxEquals(Vector3.Zero, 0.001f))
                changeSetTorque();

            if (m_taintdisable)
                changedisable();

            if (m_taintselected != m_isSelected)
                changeSelectedStatus();

            if (!m_taintVelocity.ApproxEquals(Vector3.Zero, 0.001f))
                changevelocity();

            if (m_taintparent != _parent)
                changelink();

            if (m_taintCollidesWater != m_collidesWater)
                changefloatonwater();

            if (!m_angularlock.ApproxEquals(m_taintAngularLock,0f))
                changeAngularLock();
        }

        /// <summary>
        /// Change prim in response to an angular lock taint.
        /// </summary>
        private void changeAngularLock()
        {
            // do we have a Physical object?
            if (Body != IntPtr.Zero)
            {
                //Check that we have a Parent
                //If we have a parent then we're not authorative here
                if (_parent == null)
                {
                    if (!m_taintAngularLock.ApproxEquals(Vector3.One, 0f))
                    {
                        //d.BodySetFiniteRotationMode(Body, 0);
                        //d.BodySetFiniteRotationAxis(Body,m_taintAngularLock.X,m_taintAngularLock.Y,m_taintAngularLock.Z);
                        createAMotor(m_taintAngularLock);
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
            m_angularlock = m_taintAngularLock;
        }

        /// <summary>
        /// Change prim in response to a link taint.
        /// </summary>
        private void changelink()
        {
            // If the newly set parent is not null
            // create link
            if (_parent == null && m_taintparent != null)
            {
                if (m_taintparent.PhysicsActorType == (int)ActorTypes.Prim)
                {
                    OdePrim obj = (OdePrim)m_taintparent;
                    //obj.disableBody();
//Console.WriteLine("changelink calls ParentPrim");
                    obj.AddChildPrim(this);

                    /*
                    if (obj.Body != (IntPtr)0 && Body != (IntPtr)0 && obj.Body != Body)
                    {
                        _linkJointGroup = d.JointGroupCreate(0);
                        m_linkJoint = d.JointCreateFixed(_parent_scene.world, _linkJointGroup);
                        d.JointAttach(m_linkJoint, obj.Body, Body);
                        d.JointSetFixed(m_linkJoint);
                    }
                     */
                }
            }
            // If the newly set parent is null
            // destroy link
            else if (_parent != null && m_taintparent == null)
            {
//Console.WriteLine("  changelink B");
            
                if (_parent is OdePrim)
                {
                    OdePrim obj = (OdePrim)_parent;
                    obj.ChildDelink(this);
                    childPrim = false;
                    //_parent = null;
                }
                
                /*
                    if (Body != (IntPtr)0 && _linkJointGroup != (IntPtr)0)
                    d.JointGroupDestroy(_linkJointGroup);
                        
                    _linkJointGroup = (IntPtr)0;
                    m_linkJoint = (IntPtr)0;
                */
            }
 
            _parent = m_taintparent;
            m_taintPhysics = IsPhysical;
        }

        /// <summary>
        /// Add a child prim to this parent prim.
        /// </summary>
        /// <param name="prim">Child prim</param>
        private void AddChildPrim(OdePrim prim)
        {
            if (LocalID == prim.LocalID)
                return;

            if (Body == IntPtr.Zero)
            {
                Body = d.BodyCreate(_parent_scene.world);
                setMass();
            }

            lock (childrenPrim)
            {
                if (childrenPrim.Contains(prim))
                    return;

//                m_log.DebugFormat(
//                    "[ODE PRIM]: Linking prim {0} {1} to {2} {3}", prim.Name, prim.LocalID, Name, LocalID);

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
                    prm.m_collisionCategories |= CollisionCategories.Body;
                    prm.m_collisionFlags |= (CollisionCategories.Land | CollisionCategories.Wind);

//Console.WriteLine(" GeomSetCategoryBits 1: " + prm.prim_geom + " - " + (int)prm.m_collisionCategories + " for " + Name);
                    if (prm.m_assetFailed)
                    {
                        d.GeomSetCategoryBits(prm.prim_geom, 0);
                        d.GeomSetCollideBits(prm.prim_geom, prm.BadMeshAssetCollideBits);
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
                        d.GeomSetOffsetWorldPosition(prm.prim_geom, prm.Position.X , prm.Position.Y, prm.Position.Z);
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
                        m_log.DebugFormat("[PHYSICS]: {0} ain't got no boooooooooddy, no body", Name);
                    }

                    prm.m_interpenetrationcount = 0;
                    prm.m_collisionscore = 0;
                    prm.m_disabled = false;

                    // The body doesn't already have a finite rotation mode set here
                    if ((!m_angularlock.ApproxEquals(Vector3.Zero, 0f)) && _parent == null)
                    {
                        prm.createAMotor(m_angularlock);
                    }
                    prm.Body = Body;
                    _parent_scene.ActivatePrim(prm);
                }

                m_collisionCategories |= CollisionCategories.Body;
                m_collisionFlags |= (CollisionCategories.Land | CollisionCategories.Wind);

                if (m_assetFailed)
                {
                    d.GeomSetCategoryBits(prim_geom, 0);
                    d.GeomSetCollideBits(prim_geom, BadMeshAssetCollideBits);
                }
                else
                {
                    //Console.WriteLine("GeomSetCategoryBits 2: " + prim_geom + " - " + (int)m_collisionCategories + " for " + Name);
                    d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                    //Console.WriteLine(" Post GeomSetCategoryBits 2");
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

                // The body doesn't already have a finite rotation mode set here
                if ((!m_angularlock.ApproxEquals(Vector3.Zero, 0f)) && _parent == null)
                {
                    createAMotor(m_angularlock);
                }

                d.BodySetPosition(Body, Position.X, Position.Y, Position.Z);

                if (m_vehicle.Type != Vehicle.TYPE_NONE)
                    m_vehicle.Enable(Body, _parent_scene);

                _parent_scene.ActivatePrim(this);
            }
        }

        private void ChildSetGeom(OdePrim odePrim)
        {
//            m_log.DebugFormat(
//                "[ODE PRIM]: ChildSetGeom {0} {1} for {2} {3}", odePrim.Name, odePrim.LocalID, Name, LocalID);

            //if (IsPhysical && Body != IntPtr.Zero)
            lock (childrenPrim)
            {
                foreach (OdePrim prm in childrenPrim)
                {
                    //prm.childPrim = true;
                    prm.disableBody();
                    //prm.m_taintparent = null;
                    //prm._parent = null;
                    //prm.m_taintPhysics = false;
                    //prm.m_disabled = true;
                    //prm.childPrim = false;
                }
            }

            disableBody();

            // Spurious - Body == IntPtr.Zero after disableBody()
//            if (Body != IntPtr.Zero)
//            {
//                _parent_scene.DeactivatePrim(this);
//            }

            lock (childrenPrim)
            {
                foreach (OdePrim prm in childrenPrim)
                {
//Console.WriteLine("ChildSetGeom calls ParentPrim");
                    AddChildPrim(prm);
                }
            }
        }

        private void ChildDelink(OdePrim odePrim)
        {
//            m_log.DebugFormat(
//                "[ODE PRIM]: Delinking prim {0} {1} from {2} {3}", odePrim.Name, odePrim.LocalID, Name, LocalID);

            // Okay, we have a delinked child..   need to rebuild the body.
            lock (childrenPrim)
            {
                foreach (OdePrim prm in childrenPrim)
                {
                    prm.childPrim = true;
                    prm.disableBody();
                    //prm.m_taintparent = null;
                    //prm._parent = null;
                    //prm.m_taintPhysics = false;
                    //prm.m_disabled = true;
                    //prm.childPrim = false;
                }
            }

            disableBody();

            lock (childrenPrim)
            {
 //Console.WriteLine("childrenPrim.Remove " + odePrim);
                childrenPrim.Remove(odePrim);
            }

            // Spurious - Body == IntPtr.Zero after disableBody()
//            if (Body != IntPtr.Zero)
//            {
//                _parent_scene.DeactivatePrim(this);
//            }

            lock (childrenPrim)
            {
                foreach (OdePrim prm in childrenPrim)
                {
//Console.WriteLine("ChildDelink calls ParentPrim");
                    AddChildPrim(prm);
                }
            }
        }

        /// <summary>
        /// Change prim in response to a selection taint.
        /// </summary>
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

                if (IsPhysical)
                {
                    disableBodySoft();
                }

                if (m_assetFailed)
                {
                    d.GeomSetCategoryBits(prim_geom, 0);
                    d.GeomSetCollideBits(prim_geom, 0);
                }
                else
                {
                    d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                    d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
                }

                if (IsPhysical)
                {
                    disableBodySoft();
                }
            }
            else
            {
                m_collisionCategories = CollisionCategories.Geom;

                if (IsPhysical)
                    m_collisionCategories |= CollisionCategories.Body;

                m_collisionFlags = m_default_collisionFlags;

                if (m_collidesLand)
                    m_collisionFlags |= CollisionCategories.Land;
                if (m_collidesWater)
                    m_collisionFlags |= CollisionCategories.Water;

                if (m_assetFailed)
                {
                    d.GeomSetCategoryBits(prim_geom, 0);
                    d.GeomSetCollideBits(prim_geom, BadMeshAssetCollideBits);
                }
                else
                {
                    d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                    d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
                }

                if (IsPhysical)
                {
                    if (Body != IntPtr.Zero)
                    {
                        d.BodySetLinearVel(Body, 0f, 0f, 0f);
                        d.BodySetForce(Body, 0, 0, 0);
                        enableBodySoft();
                    }
                }
            }

            resetCollisionAccounting();
            m_isSelected = m_taintselected;
        }//end changeSelectedStatus

        internal void ResetTaints()
        {
            m_taintposition = _position;
            m_taintrot = _orientation;
            m_taintPhysics = IsPhysical;
            m_taintselected = m_isSelected;
            m_taintsize = _size;
            m_taintshape = false;
            m_taintforce = false;
            m_taintdisable = false;
            m_taintVelocity = Vector3.Zero;
        }

        /// <summary>
        /// Create a geometry for the given mesh in the given target space.
        /// </summary>
        /// <param name="m_targetSpace"></param>
        /// <param name="mesh">If null, then a mesh is used that is based on the profile shape data.</param>
        private void CreateGeom(IntPtr m_targetSpace, IMesh mesh)
        {
#if SPAM
Console.WriteLine("CreateGeom:");
#endif
            if (mesh != null)
            {
                setMesh(_parent_scene, mesh);
            }
            else
            {
                if (_pbs.ProfileShape == ProfileShape.HalfCircle && _pbs.PathCurve == (byte)Extrusion.Curve1)
                {
                    if (_size.X == _size.Y && _size.Y == _size.Z && _size.X == _size.Z)
                    {
                        if (((_size.X / 2f) > 0f))
                        {
//                            _parent_scene.waitForSpaceUnlock(m_targetSpace);
                            try
                            {
//Console.WriteLine(" CreateGeom 1");
                                SetGeom(d.CreateSphere(m_targetSpace, _size.X / 2));
                                m_expectedCollisionContacts = 3;
                            }
                            catch (AccessViolationException)
                            {
                                m_log.WarnFormat("[PHYSICS]: Unable to create physics proxy for object {0}", Name);
                                return;
                            }
                        }
                        else
                        {
//                            _parent_scene.waitForSpaceUnlock(m_targetSpace);
                            try
                            {
//Console.WriteLine(" CreateGeom 2");
                                SetGeom(d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z));
                                m_expectedCollisionContacts = 4;
                            }
                            catch (AccessViolationException)
                            {
                                m_log.WarnFormat("[PHYSICS]: Unable to create physics proxy for object {0}", Name);
                                return;
                            }
                        }
                    }
                    else
                    {
//                        _parent_scene.waitForSpaceUnlock(m_targetSpace);
                        try
                        {
//Console.WriteLine("  CreateGeom 3");
                            SetGeom(d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z));
                            m_expectedCollisionContacts = 4;
                        }
                        catch (AccessViolationException)
                        {
                            m_log.WarnFormat("[PHYSICS]: Unable to create physics proxy for object {0}", Name);
                            return;
                        }
                    }
                }
                else
                {
//                    _parent_scene.waitForSpaceUnlock(m_targetSpace);
                    try
                    {
//Console.WriteLine("  CreateGeom 4");
                        SetGeom(d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z));
                        m_expectedCollisionContacts = 4;
                    }
                    catch (AccessViolationException)
                    {
                        m_log.WarnFormat("[PHYSICS]: Unable to create physics proxy for object {0}", Name);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Remove the existing geom from this prim.
        /// </summary>
        /// <param name="m_targetSpace"></param>
        /// <param name="mesh">If null, then a mesh is used that is based on the profile shape data.</param>
        /// <returns>true if the geom was successfully removed, false if it was already gone or the remove failed.</returns>
        internal bool RemoveGeom()
        {
            if (prim_geom != IntPtr.Zero)
            {
                try
                {
                    _parent_scene.geom_name_map.Remove(prim_geom);
                    _parent_scene.actor_name_map.Remove(prim_geom);
                    d.GeomDestroy(prim_geom);
                    m_expectedCollisionContacts = 0;
                    prim_geom = IntPtr.Zero;
                }
                catch (System.AccessViolationException)
                {
                    prim_geom = IntPtr.Zero;
                    m_expectedCollisionContacts = 0;
                    m_log.ErrorFormat("[PHYSICS]: PrimGeom dead for {0}", Name);

                    return false;
                }

                return true;
            }
            else
            {
                m_log.WarnFormat(
                    "[ODE PRIM]: Called RemoveGeom() on {0} {1} where geometry was already null.", Name, LocalID);

                return false;
            }
        }
        /// <summary>
        /// Add prim in response to an add taint.
        /// </summary>
        private void changeadd()
        {
//            m_log.DebugFormat("[ODE PRIM]: Adding prim {0}", Name);
            
            int[] iprimspaceArrItem = _parent_scene.calculateSpaceArrayItemFromPos(_position);
            IntPtr targetspace = _parent_scene.calculateSpaceForGeom(_position);

            if (targetspace == IntPtr.Zero)
                targetspace = _parent_scene.createprimspace(iprimspaceArrItem[0], iprimspaceArrItem[1]);

            m_targetSpace = targetspace;

            IMesh mesh = null;

            if (_parent_scene.needsMeshing(_pbs))
            {
                // Don't need to re-enable body..   it's done in SetMesh
                mesh = _parent_scene.mesher.CreateMesh(Name, _pbs, _size, _parent_scene.meshSculptLOD, IsPhysical);
                // createmesh returns null when it's a shape that isn't a cube.
               // m_log.Debug(m_localID);
                if (mesh == null)
                    CheckMeshAsset();
                else
                    m_assetFailed = false;
            }

#if SPAM
Console.WriteLine("changeadd 1");
#endif
            CreateGeom(m_targetSpace, mesh);

            d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
            d.Quaternion myrot = new d.Quaternion();
            myrot.X = _orientation.X;
            myrot.Y = _orientation.Y;
            myrot.Z = _orientation.Z;
            myrot.W = _orientation.W;
            d.GeomSetQuaternion(prim_geom, ref myrot);

            if (IsPhysical && Body == IntPtr.Zero)
                enableBody();

            changeSelectedStatus();

            m_taintadd = false;
        }

        /// <summary>
        /// Move prim in response to a move taint.
        /// </summary>
        private void changemove()
        {
            if (IsPhysical)
            {
                if (!m_disabled && !m_taintremove && !childPrim)
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
Console.WriteLine(" JointCreateFixed");
                                m_linkJoint = d.JointCreateFixed(_parent_scene.world, _linkJointGroup);
                                d.JointAttach(m_linkJoint, Body, odParent.Body);
                                d.JointSetFixed(m_linkJoint);
                            }
                        }
                        d.BodyEnable(Body);
                        if (m_vehicle.Type != Vehicle.TYPE_NONE)
                        {
                            m_vehicle.Enable(Body, _parent_scene);
                        }
                    }
                    else
                    {
                        m_log.WarnFormat("[PHYSICS]: Body for {0} still null after enableBody().  This is a crash scenario.", Name);
                    }
                }
                //else
               // {
                    //m_log.Debug("[BUG]: race!");
                //}
            }

            // string primScenAvatarIn = _parent_scene.whichspaceamIin(_position);
            // int[] arrayitem = _parent_scene.calculateSpaceArrayItemFromPos(_position);
//          _parent_scene.waitForSpaceUnlock(m_targetSpace);

            IntPtr tempspace = _parent_scene.recalculateSpaceForGeom(prim_geom, _position, m_targetSpace);
            m_targetSpace = tempspace;

//                _parent_scene.waitForSpaceUnlock(m_targetSpace);

            d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);

//                    _parent_scene.waitForSpaceUnlock(m_targetSpace);
            d.SpaceAdd(m_targetSpace, prim_geom);

            changeSelectedStatus();

            resetCollisionAccounting();
            m_taintposition = _position;
        }

        internal void Move(float timestep)
        {
            float fx = 0;
            float fy = 0;
            float fz = 0;

            if (IsPhysical && (Body != IntPtr.Zero) && !m_isSelected && !childPrim)        // KF: Only move root prims.
            {
                if (m_vehicle.Type != Vehicle.TYPE_NONE)
                {
                    // 'VEHICLES' are dealt with in ODEDynamics.cs
                    m_vehicle.Step(timestep, _parent_scene);
                }
                else
                {
//Console.WriteLine("Move " +  Name);
                    if (!d.BodyIsEnabled (Body))  d.BodyEnable (Body); // KF add 161009
                    // NON-'VEHICLES' are dealt with here
//                    if (d.BodyIsEnabled(Body) && !m_angularlock.ApproxEquals(Vector3.Zero, 0.003f))
//                    {
//                        d.Vector3 avel2 = d.BodyGetAngularVel(Body);
//                        /*
//                        if (m_angularlock.X == 1)
//                            avel2.X = 0;
//                        if (m_angularlock.Y == 1)
//                            avel2.Y = 0;
//                        if (m_angularlock.Z == 1)
//                            avel2.Z = 0;
//                        d.BodySetAngularVel(Body, avel2.X, avel2.Y, avel2.Z);
//                         */
//                    }
                    //float PID_P = 900.0f;

                    float m_mass = CalculateMass();

//                    fz = 0f;
                    //m_log.Info(m_collisionFlags.ToString());

                    
                    //KF: m_buoyancy should be set by llSetBuoyancy() for non-vehicle.
                    // would come from SceneObjectPart.cs, public void SetBuoyancy(float fvalue) , PhysActor.Buoyancy = fvalue; ??
                    // m_buoyancy: (unlimited value) <0=Falls fast; 0=1g; 1=0g; >1 = floats up 
                    // gravityz multiplier = 1 - m_buoyancy
                    fz = _parent_scene.gravityz * (1.0f - m_buoyancy) * m_mass;

                    if (m_usePID)
                    {
//Console.WriteLine("PID " +  Name);
                        // KF - this is for object move? eg. llSetPos() ?
                        //if (!d.BodyIsEnabled(Body))
                        //d.BodySetForce(Body, 0f, 0f, 0f);
                        // If we're using the PID controller, then we have no gravity
                        //fz = (-1 * _parent_scene.gravityz) * m_mass;     //KF: ?? Prims have no global gravity,so simply...
                        fz = 0f;

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
                        //PidStatus = true;

                        // PhysicsVector vec = new PhysicsVector();
                        d.Vector3 vel = d.BodyGetLinearVel(Body);

                        d.Vector3 pos = d.BodyGetPosition(Body);
                        _target_velocity =
                            new Vector3(
                                (m_PIDTarget.X - pos.X) * ((PID_G - m_PIDTau) * timestep),
                                (m_PIDTarget.Y - pos.Y) * ((PID_G - m_PIDTau) * timestep),
                                (m_PIDTarget.Z - pos.Z) * ((PID_G - m_PIDTau) * timestep)
                                );

                        //  if velocity is zero, use position control; otherwise, velocity control

                        if (_target_velocity.ApproxEquals(Vector3.Zero,0.1f))
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

                            fz = fz + ((_target_velocity.Z - vel.Z) * (PID_D) * m_mass);
                        }
                    }        // end if (m_usePID)

                    // Hover PID Controller needs to be mutually exlusive to MoveTo PID controller
                    if (m_useHoverPID && !m_usePID)
                    {
//Console.WriteLine("Hover " +  Name);
                    
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
                                m_waterHeight  = _parent_scene.GetWaterLevel();
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
                            d.BodyAddForce(Body, 0, 0, fz);
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
                        //m_taintdisable = true;
                        //base.RaiseOutOfBounds(Position);
                        //d.BodySetLinearVel(Body, fx, fy, 0f);
                        if (!d.BodyIsEnabled(Body))
                        {
                            // A physical body at rest on a surface will auto-disable after a while,
                            // this appears to re-enable it incase the surface it is upon vanishes,
                            // and the body should fall again. 
                            d.BodySetLinearVel(Body, 0f, 0f, 0f);
                            d.BodySetForce(Body, 0, 0, 0);
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
//Console.WriteLine("AddForce " + fx + "," + fy + "," + fz);
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

        private void rotate()
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
                if (IsPhysical)
                {
                    if (!m_angularlock.ApproxEquals(Vector3.One, 0f))
                        createAMotor(m_angularlock);
                }
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

        /// <summary>
        /// Change prim in response to a disable taint.
        /// </summary>
        private void changedisable()
        {
            m_disabled = true;
            if (Body != IntPtr.Zero)
            {
                d.BodyDisable(Body);
                Body = IntPtr.Zero;
            }

            m_taintdisable = false;
        }

        /// <summary>
        /// Change prim in response to a physics status taint
        /// </summary>
        private void changePhysicsStatus()
        {
            if (IsPhysical)
            {
                if (Body == IntPtr.Zero)
                {
                    if (_pbs.SculptEntry && _parent_scene.meshSculptedPrim)
                    {
                        changeshape();
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
                        RemoveGeom();

//Console.WriteLine("changePhysicsStatus for " + Name);
                        changeadd();
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
            m_taintPhysics = IsPhysical;
        }

        /// <summary>
        /// Change prim in response to a size taint.
        /// </summary>
        private void changesize()
        {
#if SPAM
            m_log.DebugFormat("[ODE PRIM]: Called changesize");
#endif

            if (_size.X <= 0) _size.X = 0.01f;
            if (_size.Y <= 0) _size.Y = 0.01f;
            if (_size.Z <= 0) _size.Z = 0.01f;

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
//                _parent_scene.waitForSpaceUnlock(m_targetSpace);
                d.SpaceRemove(m_targetSpace, prim_geom);
            }

            RemoveGeom();

            // we don't need to do space calculation because the client sends a position update also.

            IMesh mesh = null;

            // Construction of new prim
            if (_parent_scene.needsMeshing(_pbs))
            {
                float meshlod = _parent_scene.meshSculptLOD;

                if (IsPhysical)
                    meshlod = _parent_scene.MeshSculptphysicalLOD;
                // Don't need to re-enable body..   it's done in SetMesh

                if (_parent_scene.needsMeshing(_pbs))
                {
                    mesh = _parent_scene.mesher.CreateMesh(Name, _pbs, _size, meshlod, IsPhysical);
                    if (mesh == null)
                        CheckMeshAsset();
                    else
                        m_assetFailed = false;
                }
                    
            }

            CreateGeom(m_targetSpace, mesh);
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

        /// <summary>
        /// Change prim in response to a float on water taint.
        /// </summary>
        /// <param name="timestep"></param>
        private void changefloatonwater()
        {
            m_collidesWater = m_taintCollidesWater;

            if (m_collidesWater)
            {
                m_collisionFlags |= CollisionCategories.Water;
            }
            else
            {
                m_collisionFlags &= ~CollisionCategories.Water;
            }

            if (m_assetFailed)
                d.GeomSetCollideBits(prim_geom, BadMeshAssetCollideBits);
            else

                d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
        }
        /// <summary>
        /// Change prim in response to a shape taint.
        /// </summary>
        private void changeshape()
        {
            m_taintshape = false;

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

            RemoveGeom();

            // we don't need to do space calculation because the client sends a position update also.
            if (_size.X <= 0) _size.X = 0.01f;
            if (_size.Y <= 0) _size.Y = 0.01f;
            if (_size.Z <= 0) _size.Z = 0.01f;
            // Construction of new prim

            IMesh mesh = null;


            if (_parent_scene.needsMeshing(_pbs))
            {
                // Don't need to re-enable body..   it's done in CreateMesh
                float meshlod = _parent_scene.meshSculptLOD;

                if (IsPhysical)
                    meshlod = _parent_scene.MeshSculptphysicalLOD;

                // createmesh returns null when it doesn't mesh.
                mesh = _parent_scene.mesher.CreateMesh(Name, _pbs, _size, meshlod, IsPhysical);
                if (mesh == null)
                    CheckMeshAsset();
                else
                    m_assetFailed = false;
            }

            CreateGeom(m_targetSpace, mesh);
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
//            m_taintshape = false;
        }

        /// <summary>
        /// Change prim in response to an add force taint.
        /// </summary>
        private void changeAddForce()
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

        /// <summary>
        /// Change prim in response to a torque taint.
        /// </summary>
        private void changeSetTorque()
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

        /// <summary>
        /// Change prim in response to an angular force taint.
        /// </summary>
        private void changeAddAngularForce()
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

        /// <summary>
        /// Change prim in response to a velocity taint.
        /// </summary>
        private void changevelocity()
        {
            if (!m_isSelected)
            {
                // Not sure exactly why this sleep is here, but from experimentation it appears to stop an avatar
                // walking through a default rez size prim if it keeps kicking it around - justincc.
                Thread.Sleep(20);

                if (IsPhysical)
                {
                    if (Body != IntPtr.Zero)
                    {
                        d.BodySetLinearVel(Body, m_taintVelocity.X, m_taintVelocity.Y, m_taintVelocity.Z);
                    }
                }
                
                //resetCollisionAccounting();
            }

            m_taintVelocity = Vector3.Zero;
        }

        internal void setPrimForRemoval()
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

            set { _position = value;
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
//                    m_log.DebugFormat("[PHYSICS]: Set size on {0} to {1}", Name, value);
                }
                else
                {
                    m_log.WarnFormat("[PHYSICS]: Got NaN Size on object {0}", Name);
                }
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
            set
            {
                if (value.IsFinite())
                {
                    m_force = value;
                }
                else
                {
                    m_log.WarnFormat("[PHYSICS]: NaN in Force Applied to an Object {0}", Name);
                }
            }
        }

        public override int VehicleType
        {
            get { return (int)m_vehicle.Type; }
            set { m_vehicle.ProcessTypeChange((Vehicle)value); }
        }

        public override void VehicleFloatParam(int param, float value)
        {
            m_vehicle.ProcessFloatVehicleParam((Vehicle) param, value);
        }

        public override void VehicleVectorParam(int param, Vector3 value)
        {
            m_vehicle.ProcessVectorVehicleParam((Vehicle) param, value);
        }

        public override void VehicleRotationParam(int param, Quaternion rotation)
        {
            m_vehicle.ProcessRotationVehicleParam((Vehicle) param, rotation);
        }

        public override void VehicleFlags(int param, bool remove)
        {
            m_vehicle.ProcessVehicleFlags(param, remove);
        }

        public override void SetVolumeDetect(int param)
        {
            // We have to lock the scene here so that an entire simulate loop either uses volume detect for all
            // possible collisions with this prim or for none of them.
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
                m_assetFailed = false;
                m_taintshape = true;
            }
        }

        public override Vector3 Velocity
        {
            get
            {
                // Average previous velocity with the new one so
                // client object interpolation works a 'little' better
                if (_zeroFlag)
                    return Vector3.Zero;

                Vector3 returnVelocity = Vector3.Zero;
                returnVelocity.X = (m_lastVelocity.X + _velocity.X) * 0.5f; // 0.5f is mathematically equiv to '/ 2'
                returnVelocity.Y = (m_lastVelocity.Y + _velocity.Y) * 0.5f;
                returnVelocity.Z = (m_lastVelocity.Z + _velocity.Z) * 0.5f;
                return returnVelocity;
            }
            set
            {
                if (value.IsFinite())
                {
                    _velocity = value;

                    m_taintVelocity = value;
                    _parent_scene.AddPhysicsActorTaint(this);
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
                    m_taintTorque = value;
                    _parent_scene.AddPhysicsActorTaint(this);
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
            get { return _orientation; }
            set
            {
                if (QuaternionIsFinite(value))
                    _orientation = value;
                else
                    m_log.WarnFormat("[PHYSICS]: Got NaN quaternion Orientation from Scene in Object {0}", Name);
            }
        }

        private static bool QuaternionIsFinite(Quaternion q)
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

        public override Vector3 Acceleration
        {
            get { return _acceleration; }
            set { _acceleration = value; }
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
                m_log.WarnFormat("[PHYSICS]: Got Invalid linear force vector from Scene in Object {0}", Name);
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
                m_log.WarnFormat("[PHYSICS]: Got Invalid Angular force vector from Scene in Object {0}", Name);
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
            set
            {
                if (value.IsFinite())
                {
                    m_rotationalVelocity = value;
                }
                else
                {
                    m_log.WarnFormat("[PHYSICS]: Got NaN RotationalVelocity in Object {0}", Name);
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
                m_log.Warn("[PHYSICS]: Too many crossing failures for: " + Name);
            }
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
            // reverse the zero/non zero values for ODE.
            if (axis.IsFinite())
            {
                axis.X = (axis.X > 0) ? 1f : 0f;
                axis.Y = (axis.Y > 0) ? 1f : 0f;
                axis.Z = (axis.Z > 0) ? 1f : 0f;
                m_log.DebugFormat("[axislock]: <{0},{1},{2}>", axis.X, axis.Y, axis.Z);
                m_taintAngularLock = axis;
            }
            else
            {
                m_log.WarnFormat("[PHYSICS]: Got NaN locking axis from Scene on Object {0}", Name);
            }
        }

        internal void UpdatePositionAndVelocity()
        {
            //  no lock; called from Simulate() -- if you call this from elsewhere, gotta lock or do Monitor.Enter/Exit!
            if (_parent == null)
            {
                Vector3 pv = Vector3.Zero;
                bool lastZeroFlag = _zeroFlag;
                float m_minvelocity = 0;
                if (Body != (IntPtr)0) // FIXME -> or if it is a joint
                {
                    d.Vector3 vec = d.BodyGetPosition(Body);
                    d.Quaternion ori = d.BodyGetQuaternion(Body);
                    d.Vector3 vel = d.BodyGetLinearVel(Body);
                    d.Vector3 rotvel = d.BodyGetAngularVel(Body);
                    d.Vector3 torque = d.BodyGetTorque(Body);
                    _torque = new Vector3(torque.X, torque.Y, torque.Z);
                    Vector3 l_position = Vector3.Zero;
                    Quaternion l_orientation = Quaternion.Identity;

                    //  kluge to keep things in bounds.  ODE lets dead avatars drift away (they should be removed!)
                    //if (vec.X < 0.0f) { vec.X = 0.0f; if (Body != (IntPtr)0) d.BodySetAngularVel(Body, 0, 0, 0); }
                    //if (vec.Y < 0.0f) { vec.Y = 0.0f; if (Body != (IntPtr)0) d.BodySetAngularVel(Body, 0, 0, 0); }
                    //if (vec.X > 255.95f) { vec.X = 255.95f; if (Body != (IntPtr)0) d.BodySetAngularVel(Body, 0, 0, 0); }
                    //if (vec.Y > 255.95f) { vec.Y = 255.95f; if (Body != (IntPtr)0) d.BodySetAngularVel(Body, 0, 0, 0); }

                    m_lastposition = _position;
                    m_lastorientation = _orientation;

                    l_position.X = vec.X;
                    l_position.Y = vec.Y;
                    l_position.Z = vec.Z;
                    l_orientation.X = ori.X;
                    l_orientation.Y = ori.Y;
                    l_orientation.Z = ori.Z;
                    l_orientation.W = ori.W;

                    if (l_position.X > ((int)_parent_scene.WorldExtents.X - 0.05f) || l_position.X < 0f || l_position.Y > ((int)_parent_scene.WorldExtents.Y - 0.05f) || l_position.Y < 0f)
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

                    if (l_position.Z < 0)
                    {
                        // This is so prim that get lost underground don't fall forever and suck up
                        //
                        // Sim resources and memory.
                        // Disables the prim's movement physics....
                        // It's a hack and will generate a console message if it fails.

                        //IsPhysical = false;
                        if (_parent == null)
                            base.RaiseOutOfBounds(_position);

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
                        throttleCounter = 0;
                        _zeroFlag = true;
                        //outofBounds = true;
                    }

                    //float Adiff = 1.0f - Math.Abs(Quaternion.Dot(m_lastorientation, l_orientation));
//Console.WriteLine("Adiff " + Name + " = " + Adiff);
                    if ((Math.Abs(m_lastposition.X - l_position.X) < 0.02)
                        && (Math.Abs(m_lastposition.Y - l_position.Y) < 0.02)
                        && (Math.Abs(m_lastposition.Z - l_position.Z) < 0.02)
//                        && (1.0 - Math.Abs(Quaternion.Dot(m_lastorientation, l_orientation)) < 0.01))
                        && (1.0 - Math.Abs(Quaternion.Dot(m_lastorientation, l_orientation)) < 0.0001))  // KF 0.01 is far to large
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
                            throttleCounter = 0;
                            m_rotationalVelocity = pv;

                            if (_parent == null)
                            {
                                base.RequestPhysicsterseUpdate();
                            }

                            m_lastUpdateSent = true;
                        }
                    }
                    else
                    {
                        if (lastZeroFlag != _zeroFlag)
                        {
                            if (_parent == null)
                            {
                                base.RequestPhysicsterseUpdate();
                            }
                        }

                        m_lastVelocity = _velocity;

                        _position = l_position;

                        _velocity.X = vel.X;
                        _velocity.Y = vel.Y;
                        _velocity.Z = vel.Z;

                        _acceleration = ((_velocity - m_lastVelocity) / 0.1f);
                        _acceleration = new Vector3(_velocity.X - m_lastVelocity.X / 0.1f, _velocity.Y - m_lastVelocity.Y / 0.1f, _velocity.Z - m_lastVelocity.Z / 0.1f);
                        //m_log.Info("[PHYSICS]: V1: " + _velocity + " V2: " + m_lastVelocity + " Acceleration: " + _acceleration.ToString());
                       
                        // Note here that linearvelocity is affecting angular velocity...  so I'm guessing this is a vehicle specific thing... 
                        // it does make sense to do this for tiny little instabilities with physical prim, however 0.5m/frame is fairly large. 
                        // reducing this to 0.02m/frame seems to help the angular rubberbanding quite a bit, however, to make sure it doesn't affect elevators and vehicles
                        // adding these logical exclusion situations to maintain this where I think it was intended to be.
                        if (m_throttleUpdates || m_usePID || (m_vehicle != null && m_vehicle.Type != Vehicle.TYPE_NONE) || (Amotor != IntPtr.Zero)) 
                        {
                            m_minvelocity = 0.5f;
                        }
                        else
                        {
                            m_minvelocity = 0.02f;
                        }

                        if (_velocity.ApproxEquals(pv, m_minvelocity))
                        {
                            m_rotationalVelocity = pv;
                        }
                        else
                        {
                            m_rotationalVelocity = new Vector3(rotvel.X, rotvel.Y, rotvel.Z);
                        }

                        //m_log.Debug("ODE: " + m_rotationalVelocity.ToString());
                        _orientation.X = ori.X;
                        _orientation.Y = ori.Y;
                        _orientation.Z = ori.Z;
                        _orientation.W = ori.W;
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

        public override bool FloatOnWater
        {
            set {
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
                    m_log.WarnFormat("[PHYSICS]: Got NaN PIDTarget from Scene on Object {0}", Name);
            } 
        }
        public override bool PIDActive { set { m_usePID = value; } }
        public override float PIDTau { set { m_PIDTau = value; } }

        public override float PIDHoverHeight { set { m_PIDHoverHeight = value; ; } }
        public override bool PIDHoverActive { set { m_useHoverPID = value; } }
        public override PIDHoverType PIDHoverType { set { m_PIDHoverType = value; } }
        public override float PIDHoverTau { set { m_PIDHoverTau = value; } }
        
        public override Quaternion APIDTarget{ set { return; } }

        public override bool APIDActive{ set { return; } }

        public override float APIDStrength{ set { return; } }

        public override float APIDDamping{ set { return; } }

        private void createAMotor(Vector3 axis)
        {
            if (Body == IntPtr.Zero)
                return;

            if (Amotor != IntPtr.Zero)
            {
                d.JointDestroy(Amotor);
                Amotor = IntPtr.Zero;
            }

            float axisnum = 3;

            axisnum = (axisnum - (axis.X + axis.Y + axis.Z));

            // PhysicsVector totalSize = new PhysicsVector(_size.X, _size.Y, _size.Z);

            
            // Inverse Inertia Matrix, set the X, Y, and/r Z inertia to 0 then invert it again.
            d.Mass objMass;
            d.MassSetZero(out objMass);
            DMassCopy(ref pMass, ref objMass);

            //m_log.DebugFormat("1-{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, ", objMass.I.M00, objMass.I.M01, objMass.I.M02, objMass.I.M10, objMass.I.M11, objMass.I.M12, objMass.I.M20, objMass.I.M21, objMass.I.M22);

            Matrix4 dMassMat = FromDMass(objMass);

            Matrix4 mathmat = Inverse(dMassMat);

            /*
            //m_log.DebugFormat("2-{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, ", mathmat[0, 0], mathmat[0, 1], mathmat[0, 2], mathmat[1, 0], mathmat[1, 1], mathmat[1, 2], mathmat[2, 0], mathmat[2, 1], mathmat[2, 2]);

            mathmat = Inverse(mathmat);


            objMass = FromMatrix4(mathmat, ref objMass);
            //m_log.DebugFormat("3-{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, ", objMass.I.M00, objMass.I.M01, objMass.I.M02, objMass.I.M10, objMass.I.M11, objMass.I.M12, objMass.I.M20, objMass.I.M21, objMass.I.M22);

            mathmat = Inverse(mathmat);
            */
            if (axis.X == 0)
            {
                mathmat.M33 = 50.0000001f;
                //objMass.I.M22 = 0;
            }
            if (axis.Y == 0)
            {
                mathmat.M22 = 50.0000001f;
                //objMass.I.M11 = 0;
            }
            if (axis.Z == 0)
            {
                mathmat.M11 = 50.0000001f;
                //objMass.I.M00 = 0;
            }
            
            

            mathmat = Inverse(mathmat);
            objMass = FromMatrix4(mathmat, ref objMass);
            //m_log.DebugFormat("4-{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, ", objMass.I.M00, objMass.I.M01, objMass.I.M02, objMass.I.M10, objMass.I.M11, objMass.I.M12, objMass.I.M20, objMass.I.M21, objMass.I.M22);
           
            //return;
            if (d.MassCheck(ref objMass))
            {
                d.BodySetMass(Body, ref objMass);
            }
            else
            {
                //m_log.Debug("[PHYSICS]: Mass invalid, ignoring");
            }

            if (axisnum <= 0)
                return;
            // int dAMotorEuler = 1;

            Amotor = d.JointCreateAMotor(_parent_scene.world, IntPtr.Zero);
            d.JointAttach(Amotor, Body, IntPtr.Zero);
            d.JointSetAMotorMode(Amotor, 0);

            d.JointSetAMotorNumAxes(Amotor,(int)axisnum);
            int i = 0;

            if (axis.X == 0)
            {
                d.JointSetAMotorAxis(Amotor, i, 0, 1, 0, 0);
                i++;
            }

            if (axis.Y == 0)
            {
                d.JointSetAMotorAxis(Amotor, i, 0, 0, 1, 0);
                i++;
            }

            if (axis.Z == 0)
            {
                d.JointSetAMotorAxis(Amotor, i, 0, 0, 0, 1);
                i++;
            }

            for (int j = 0; j < (int)axisnum; j++)
            {
                //d.JointSetAMotorAngle(Amotor, j, 0);
            }

            //d.JointSetAMotorAngle(Amotor, 1, 0);
            //d.JointSetAMotorAngle(Amotor, 2, 0);

            // These lowstops and high stops are effectively (no wiggle room)
            d.JointSetAMotorParam(Amotor, (int)dParam.LowStop, -0f);
            d.JointSetAMotorParam(Amotor, (int)dParam.LoStop3, -0f);
            d.JointSetAMotorParam(Amotor, (int)dParam.LoStop2, -0f);
            d.JointSetAMotorParam(Amotor, (int)dParam.HiStop, 0f);
            d.JointSetAMotorParam(Amotor, (int)dParam.HiStop3, 0f);
            d.JointSetAMotorParam(Amotor, (int)dParam.HiStop2, 0f);
            //d.JointSetAMotorParam(Amotor, (int) dParam.Vel, 9000f);
            d.JointSetAMotorParam(Amotor, (int)dParam.FudgeFactor, 0f);
            d.JointSetAMotorParam(Amotor, (int)dParam.FMax, Mass * 50f);//
        }

        private Matrix4 FromDMass(d.Mass pMass)
        {
            Matrix4 obj;
            obj.M11 = pMass.I.M00;
            obj.M12 = pMass.I.M01;
            obj.M13 = pMass.I.M02;
            obj.M14 = 0;
            obj.M21 = pMass.I.M10;
            obj.M22 = pMass.I.M11;
            obj.M23 = pMass.I.M12;
            obj.M24 = 0;
            obj.M31 = pMass.I.M20;
            obj.M32 = pMass.I.M21;
            obj.M33 = pMass.I.M22;
            obj.M34 = 0;
            obj.M41 = 0;
            obj.M42 = 0;
            obj.M43 = 0;
            obj.M44 = 1;
            return obj;
        }

        private d.Mass FromMatrix4(Matrix4 pMat, ref d.Mass obj)
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
            _parent_scene.AddCollisionEventReporting(this);
        }

        public override void UnSubscribeEvents()
        {
            _parent_scene.RemoveCollisionEventReporting(this);
            m_eventsubscription = 0;
        }

        public void AddCollisionEvent(uint CollidedWith, ContactPoint contact)
        {
            CollisionEventsThisFrame.AddCollider(CollidedWith, contact);
        }

        public void SendCollisions()
        {
            if (m_collisionsOnPreviousFrame || CollisionEventsThisFrame.Count > 0)
            {
                base.SendCollisionUpdate(CollisionEventsThisFrame);

                if (CollisionEventsThisFrame.Count > 0)
                {
                    m_collisionsOnPreviousFrame = true;
                    CollisionEventsThisFrame.Clear();
                }
                else
                {
                    m_collisionsOnPreviousFrame = false;
                }
            }
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
            for (int i=0; i<4; i++)
            {
                for (int j=0; j<4; j++)
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
                    Matrix4SetValue(ref minor, m,n, matrix[i, j]);
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
            float diag1 = pMat[0, 0]*pMat[1, 1]*pMat[2, 2];
            float diag2 = pMat[0, 1]*pMat[2, 1]*pMat[2, 0];
            float diag3 = pMat[0, 2]*pMat[1, 0]*pMat[2, 1];
            float diag4 = pMat[2, 0]*pMat[1, 1]*pMat[0, 2];
            float diag5 = pMat[2, 1]*pMat[1, 2]*pMat[0, 0];
            float diag6 = pMat[2, 2]*pMat[1, 0]*pMat[0, 1];

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

        private void CheckMeshAsset()
        {
            if (_pbs.SculptEntry && !m_assetFailed && _pbs.SculptTexture != UUID.Zero)
            {
                m_assetFailed = true;
                Util.FireAndForget(delegate
                    {
                        RequestAssetDelegate assetProvider = _parent_scene.RequestAssetMethod;
                        if (assetProvider != null)
                            assetProvider(_pbs.SculptTexture, MeshAssetReceived);
                    });
            }
        }

        private void MeshAssetReceived(AssetBase asset)
        {
            if (asset != null && asset.Data != null && asset.Data.Length > 0)
            {
                if (!_pbs.SculptEntry)
                    return;
                if (_pbs.SculptTexture.ToString() != asset.ID)
                    return;

                _pbs.SculptData = new byte[asset.Data.Length];
                asset.Data.CopyTo(_pbs.SculptData, 0);
//                m_assetFailed = false;
                m_taintshape = true;
               _parent_scene.AddPhysicsActorTaint(this);
            }
            else
            {
                m_log.WarnFormat(
                    "[ODE PRIM]: Could not get mesh/sculpt asset {0} for {1} at {2} in {3}",
                    _pbs.SculptTexture, Name, _position, _parent_scene.Name);
            }
        }          
    }
}