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

        private PhysicsVector _position;
        private PhysicsVector _velocity;
        private PhysicsVector _torque = new PhysicsVector(0,0,0);
        private PhysicsVector m_lastVelocity = new PhysicsVector(0.0f, 0.0f, 0.0f);
        private PhysicsVector m_lastposition = new PhysicsVector(0.0f, 0.0f, 0.0f);
        private Quaternion m_lastorientation = new Quaternion();
        private PhysicsVector m_rotationalVelocity;
        private PhysicsVector _size;
        private PhysicsVector _acceleration;
        // private d.Vector3 _zeroPosition = new d.Vector3(0.0f, 0.0f, 0.0f);
        private Quaternion _orientation;
        private PhysicsVector m_taintposition;
        private PhysicsVector m_taintsize;
        private PhysicsVector m_taintVelocity = new PhysicsVector(0, 0, 0);
        private PhysicsVector m_taintTorque = new PhysicsVector(0, 0, 0);
        private Quaternion m_taintrot;
        private PhysicsVector m_angularlock = new PhysicsVector(1f, 1f, 1f);
        private PhysicsVector m_taintAngularLock = new PhysicsVector(1f, 1f, 1f);
        private IntPtr Amotor = IntPtr.Zero;

        private PhysicsVector m_PIDTarget = new PhysicsVector(0, 0, 0);
        // private PhysicsVector m_taintPIDTarget = new PhysicsVector(0, 0, 0);
        private float m_PIDTau = 0f;
        private float PID_D = 35f;
        private float PID_G = 25f;
        private bool m_usePID = false;

        // KF: These next 7 params apply to llSetHoverHeight(float height, integer water, float tau),
        // and are for non-VEHICLES only.
         
        private float m_PIDHoverHeight = 0f;
        private float m_PIDHoverTau = 0f;
        private bool m_useHoverPID = false;
        private PIDHoverType m_PIDHoverType = PIDHoverType.Ground;
        private float m_targetHoverHeight = 0f;
        private float m_groundHeight = 0f;
        private float m_waterHeight = 0f;
        private float m_buoyancy = 0f;				//KF: m_buoyancy should be set by llSetBuoyancy() for non-vehicle. 

        // private float m_tensor = 5f;
        private int body_autodisable_frames = 20;


        private const CollisionCategories m_default_collisionFlags = (CollisionCategories.Geom
                                                        | CollisionCategories.Space
                                                        | CollisionCategories.Body
                                                        | CollisionCategories.Character
                                                        );
        private bool m_taintshape = false;
        private bool m_taintPhysics = false;
        private bool m_collidesLand = true;
        private bool m_collidesWater = false;
        public bool m_returnCollisions = false;

        // Default we're a Geometry
        private CollisionCategories m_collisionCategories = (CollisionCategories.Geom);

        // Default, Collide with Other Geometries, spaces and Bodies
        private CollisionCategories m_collisionFlags = m_default_collisionFlags;

        public bool m_taintremove = false;
        public bool m_taintdisable = false;
        public bool m_disabled = false;
        public bool m_taintadd = false;
        public bool m_taintselected = false;
        public bool m_taintCollidesWater = false;

        public uint m_localID = 0;

        //public GCHandle gc;
        private CollisionLocker ode;

        private bool m_taintforce = false;
        private bool m_taintaddangularforce = false;
        private PhysicsVector m_force = new PhysicsVector(0.0f, 0.0f, 0.0f);
        private List<PhysicsVector> m_forcelist = new List<PhysicsVector>();
        private List<PhysicsVector> m_angularforcelist = new List<PhysicsVector>();

        private IMesh _mesh;
        private PrimitiveBaseShape _pbs;
        private OdeScene _parent_scene;
        public IntPtr m_targetSpace = (IntPtr) 0;
        public IntPtr prim_geom;
        public IntPtr prev_geom;
        public IntPtr _triMeshData;

        private IntPtr _linkJointGroup = (IntPtr)0;
        private PhysicsActor _parent = null;
        private PhysicsActor m_taintparent = null;

        private List<OdePrim> childrenPrim = new List<OdePrim>();

        private bool iscolliding = false;
        private bool m_isphysical = false;
        private bool m_isSelected = false;

        internal bool m_isVolumeDetect = false; // If true, this prim only detects collisions but doesn't collide actively

        private bool m_throttleUpdates = false;
        private int throttleCounter = 0;
        public int m_interpenetrationcount = 0;
        public float m_collisionscore = 0;
        public int m_roundsUnderMotionThreshold = 0;
        private int m_crossingfailures = 0;

        public bool outofBounds = false;
        private float m_density = 10.000006836f; // Aluminum g/cm3;

        public bool _zeroFlag = false;
        private bool m_lastUpdateSent = false;

        public IntPtr Body = (IntPtr) 0;
        public String m_primName;
//        private String m_primName;
        private PhysicsVector _target_velocity;
        public d.Mass pMass;

        public int m_eventsubscription = 0;
        private CollisionEventUpdate CollisionEventsThisFrame = null;

        private IntPtr m_linkJoint = (IntPtr)0;

        public volatile bool childPrim = false;

        private ODEDynamics m_vehicle;

        internal int m_material = (int)Material.Wood;

        public OdePrim(String primName, OdeScene parent_scene, PhysicsVector pos, PhysicsVector size,
                       Quaternion rotation, IMesh mesh, PrimitiveBaseShape pbs, bool pisPhysical, CollisionLocker dode)
        {
            _target_velocity = new PhysicsVector(0, 0, 0);
            m_vehicle = new ODEDynamics();
            //gc = GCHandle.Alloc(prim_geom, GCHandleType.Pinned);
            ode = dode;
            _velocity = new PhysicsVector();
            if (!PhysicsVector.isFinite(pos))
            {
                pos = new PhysicsVector(((int)Constants.RegionSize * 0.5f), ((int)Constants.RegionSize * 0.5f), parent_scene.GetTerrainHeightAtXY(((int)Constants.RegionSize * 0.5f), ((int)Constants.RegionSize * 0.5f)) + 0.5f);
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
            prev_geom = IntPtr.Zero;

            if (!PhysicsVector.isFinite(pos))
            {
                size = new PhysicsVector(0.5f, 0.5f, 0.5f);
                m_log.Warn("[PHYSICS]: Got nonFinite Object create Size");
            }

            if (size.X <= 0) size.X = 0.01f;
            if (size.Y <= 0) size.Y = 0.01f;
            if (size.Z <= 0) size.Z = 0.01f;

            _size = size;
            m_taintsize = _size;
            _acceleration = new PhysicsVector();
            m_rotationalVelocity = PhysicsVector.Zero;

            if (!QuaternionIsFinite(rotation))
            {
                rotation = Quaternion.Identity;
                m_log.Warn("[PHYSICS]: Got nonFinite Object create Rotation");
            }

            _orientation = rotation;
            m_taintrot = _orientation;
            _mesh = mesh;
            _pbs = pbs;

            _parent_scene = parent_scene;
            m_targetSpace = (IntPtr)0;

            if (pos.Z < 0)
                m_isphysical = false;
            else
            {
                m_isphysical = pisPhysical;
                // If we're physical, we need to be in the master space for now.
                // linksets *should* be in a space together..  but are not currently
                if (m_isphysical)
                    m_targetSpace = _parent_scene.space;
            }
            m_primName = primName;
            m_taintadd = true;
            _parent_scene.AddPhysicsActorTaint(this);
            //  don't do .add() here; old geoms get recycled with the same hash
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

        public override uint LocalID
        {
            set {
                //m_log.Info("[PHYSICS]: Setting TrackerID: " + value);
                m_localID = value; }
        }

        public override bool Grabbed
        {
            set { return; }
        }

        public override bool Selected
        {
            set {
        
            
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
                if(m_isSelected) disableBodySoft();         
            }
        }

        public void SetGeom(IntPtr geom)
        {
            prev_geom = prim_geom;
            prim_geom = geom;
//Console.WriteLine("SetGeom to " + prim_geom + " for " + m_primName);     
            if (prim_geom != IntPtr.Zero)
            {
                d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
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
                    if (m_vehicle.Type != Vehicle.TYPE_NONE)
	                    m_vehicle.Enable(Body, _parent_scene);
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
                if ((!m_angularlock.IsIdentical(PhysicsVector.Zero, 0)) && _parent == null)
                {
                    createAMotor(m_angularlock);
                }
                if (m_vehicle.Type != Vehicle.TYPE_NONE)
                {
                    m_vehicle.Enable(Body, _parent_scene);
                }

                _parent_scene.addActivePrim(this);
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

                    volume = _size.X*_size.Y*_size.Z;

                    // If the user has 'hollowed out'
                    // ProfileHollow is one of those 0 to 50000 values :P
                    // we like percentages better..   so turning into a percentage

                    if (((float) _pbs.ProfileHollow/50000f) > 0.0)
                    {
                        float hollowAmount = (float) _pbs.ProfileHollow/50000f;

                        // calculate the hollow volume by it's shape compared to the prim shape
                        float hollowVolume = 0;
                        switch (_pbs.HollowShape)
                        {
                            case HollowShape.Square:
                            case HollowShape.Same:
                                // Cube Hollow volume calculation
                                float hollowsizex = _size.X*hollowAmount;
                                float hollowsizey = _size.Y*hollowAmount;
                                float hollowsizez = _size.Z*hollowAmount;
                                hollowVolume = hollowsizex*hollowsizey*hollowsizez;
                                break;

                            case HollowShape.Circle:
                                // Hollow shape is a perfect cyllinder in respect to the cube's scale
                                // Cyllinder hollow volume calculation
                                float hRadius = _size.X/2;
                                float hLength = _size.Z;

                                // pi * r2 * h
                                hollowVolume = ((float) (Math.PI*Math.Pow(hRadius, 2)*hLength)*hollowAmount);
                                break;

                            case HollowShape.Triangle:
                                // Equilateral Triangular Prism volume hollow calculation
                                // Triangle is an Equilateral Triangular Prism with aLength = to _size.Y

                                float aLength = _size.Y;
                                // 1/2 abh
                                hollowVolume = (float) ((0.5*aLength*_size.X*_size.Z)*hollowAmount);
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
                        float volume1 = (float)(Math.PI * Math.Pow(_size.X/2, 2) * _size.Z);
                        float volume2 = (float)(Math.PI * Math.Pow(_size.Y/2, 2) * _size.Z);

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
                        if (_size.X == _size.Y && _size.Y == _size.Z)
                        {
                            // regular sphere
                            // v = 4/3 * pi * r^3
                            float sradius3 = (float)Math.Pow((_size.X / 2), 3);
                            volume = (float)((4f / 3f) * Math.PI * sradius3);
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
                                hollowVolume = ((float)((Math.PI * Math.Pow(hRadius, 2) * hLength)/2) * hollowAmount);
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
                    volume = _size.X*_size.Y*_size.Z;
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
            if (((PathCutStartAmount + PathCutEndAmount)/50000f) > 0.0f)
            {
                float pathCutAmount = ((PathCutStartAmount + PathCutEndAmount)/50000f);

                // Check the return amount for sanity
                if (pathCutAmount >= 0.99f)
                    pathCutAmount = 0.99f;

                volume = volume - (volume*pathCutAmount);
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
            returnMass = m_density*volume;
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
            return returnMass;
        }// end CalculateMass

        #endregion

        public void setMass()
        {
            if (Body != (IntPtr) 0)
            {
                float newmass = CalculateMass();
                //m_log.Info("[PHYSICS]: New Mass: " + newmass.ToString());

                d.MassSetBoxTotal(out pMass, newmass, _size.X, _size.Y, _size.Z);
                d.BodySetMass(Body, ref pMass);
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
                                    _parent_scene.remActivePrim(prm);
                                    prm.Body = IntPtr.Zero;
                                }
                            }
                        }
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
                        d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                        d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
                    }

                    
                    Body = IntPtr.Zero;
                }
            }
            m_disabled = true;
            m_collisionscore = 0;
        }

        public void setMesh(OdeScene parent_scene, IMesh mesh)
        {
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

            mesh.releaseSourceMeshData(); // free up the original mesh data to save memory

            _triMeshData = d.GeomTriMeshDataCreate();

            d.GeomTriMeshDataBuildSimple(_triMeshData, vertices, vertexStride, vertexCount, indices, indexCount, triStride);
            d.GeomTriMeshDataPreprocess(_triMeshData);

            _parent_scene.waitForSpaceUnlock(m_targetSpace);

            try
            {
                if (prim_geom == IntPtr.Zero)
                {
//Console.WriteLine(" setMesh 1");               
                    SetGeom(d.CreateTriMesh(m_targetSpace, _triMeshData, parent_scene.triCallback, null, null));
                }
            }
            catch (AccessViolationException)
            {
                m_log.Error("[PHYSICS]: MESH LOCKED");
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

        public void ProcessTaints(float timestep)
        {
//Console.WriteLine("ProcessTaints for " + m_primName );
            if (m_taintadd)
            {
                changeadd(timestep);
            }
            
            if (prim_geom != IntPtr.Zero)
            {
	        	 if (!_position.IsIdentical(m_taintposition,0f))
	                    changemove(timestep);

	             if (m_taintrot != _orientation)
	             {
	                if(childPrim && IsPhysical)	// For physical child prim...
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
            
                if (m_taintPhysics != m_isphysical && !(m_taintparent != _parent))
                    changePhysicsStatus(timestep);
                //

                if (!_size.IsIdentical(m_taintsize,0))
                    changesize(timestep);
                //

                if (m_taintshape)
                    changeshape(timestep);
                //

                if (m_taintforce)
                    changeAddForce(timestep);

                if (m_taintaddangularforce)
                    changeAddAngularForce(timestep);

                if (!m_taintTorque.IsIdentical(PhysicsVector.Zero, 0.001f))
                    changeSetTorque(timestep);

                if (m_taintdisable)
                    changedisable(timestep);

                if (m_taintselected != m_isSelected)
                    changeSelectedStatus(timestep);

                if (!m_taintVelocity.IsIdentical(PhysicsVector.Zero, 0.001f))
                    changevelocity(timestep);

                if (m_taintparent != _parent)
                    changelink(timestep);

                if (m_taintCollidesWater != m_collidesWater)
                    changefloatonwater(timestep);

                if (!m_angularlock.IsIdentical(m_taintAngularLock,0))
                    changeAngularLock(timestep);
 
            }
            else
            {
                m_log.Error("[PHYSICS]: The scene reused a disposed PhysActor! *waves finger*, Don't be evil.  A couple of things can cause this.   An improper prim breakdown(be sure to set prim_geom to zero after d.GeomDestroy!   An improper buildup (creating the geom failed).   Or, the Scene Reused a physics actor after disposing it.)");
            }
        }


        private void changeAngularLock(float timestep)
        {
            // do we have a Physical object?
            if (Body != IntPtr.Zero)
            {
                //Check that we have a Parent
                //If we have a parent then we're not authorative here
                if (_parent == null)
                {
                    if (!m_taintAngularLock.IsIdentical(new PhysicsVector(1f,1f,1f), 0))
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
            m_angularlock = new PhysicsVector(m_taintAngularLock.X, m_taintAngularLock.Y, m_taintAngularLock.Z);
            
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
                    //obj.disableBody();
//Console.WriteLine("changelink calls ParentPrim");
                    obj.ParentPrim(this);

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
            m_taintPhysics = m_isphysical;
        }

        // I'm the parent
        // prim is the child
        public void ParentPrim(OdePrim prim)
        {
//Console.WriteLine("ParentPrim  " + m_primName);        
            if (this.m_localID != prim.m_localID)
            {
                if (Body == IntPtr.Zero)
                {
                    Body = d.BodyCreate(_parent_scene.world);
                    setMass();
                }
                if (Body != IntPtr.Zero)
                {
                    lock (childrenPrim)
                    {
                        if (!childrenPrim.Contains(prim))
                        {
//Console.WriteLine("childrenPrim.Add " + prim);                          
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

                                if (prm.prim_geom == IntPtr.Zero)
                                {
                                    m_log.Warn("[PHYSICS]: Unable to link one of the linkset elements.  No geom yet");
                                    continue;
                                }
//Console.WriteLine(" GeomSetCategoryBits 1: " + prm.prim_geom + " - " + (int)prm.m_collisionCategories + " for " + m_primName);    
                                d.GeomSetCategoryBits(prm.prim_geom, (int)prm.m_collisionCategories);
                                d.GeomSetCollideBits(prm.prim_geom, (int)prm.m_collisionFlags);


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
                                    m_log.Debug("[PHYSICS]:I ain't got no boooooooooddy, no body");
                                }


                                prm.m_interpenetrationcount = 0;
                                prm.m_collisionscore = 0;
                                prm.m_disabled = false;

                                // The body doesn't already have a finite rotation mode set here
                                if ((!m_angularlock.IsIdentical(PhysicsVector.Zero, 0)) && _parent == null)
                                {
                                    prm.createAMotor(m_angularlock);
                                }
                                prm.Body = Body;
                                _parent_scene.addActivePrim(prm);
                            }
                            m_collisionCategories |= CollisionCategories.Body;
                            m_collisionFlags |= (CollisionCategories.Land | CollisionCategories.Wind);

//Console.WriteLine("GeomSetCategoryBits 2: " + prim_geom + " - " + (int)m_collisionCategories + " for " + m_primName);  
                            d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
//Console.WriteLine(" Post GeomSetCategoryBits 2");
                            d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);


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
                            if ((!m_angularlock.IsIdentical(PhysicsVector.Zero, 0)) && _parent == null)
                            {
                                createAMotor(m_angularlock);
                            }
                            d.BodySetPosition(Body, Position.X, Position.Y, Position.Z);
                            if (m_vehicle.Type != Vehicle.TYPE_NONE) m_vehicle.Enable(Body, _parent_scene);
                            _parent_scene.addActivePrim(this);
                        }
                    }
                }
            }

        }

        private void ChildSetGeom(OdePrim odePrim)
        {
            //if (m_isphysical && Body != IntPtr.Zero)
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


            if (Body != IntPtr.Zero)
            {
                _parent_scene.remActivePrim(this);
            }

            lock (childrenPrim)
            {
                foreach (OdePrim prm in childrenPrim)
                {
//Console.WriteLine("ChildSetGeom calls ParentPrim");               
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
            
            
            

            if (Body != IntPtr.Zero)
            {
                _parent_scene.remActivePrim(this);
            }

            

            lock (childrenPrim)
            {
                foreach (OdePrim prm in childrenPrim)
                {
//Console.WriteLine("ChildDelink calls ParentPrim");                
                    ParentPrim(prm);
                }
            }

           
        }

        private void changeSelectedStatus(float timestep)
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
                }

                if (m_isphysical)
                {
                    disableBodySoft();
                }
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

                if (prim_geom != IntPtr.Zero)
                {
                    d.GeomSetCategoryBits(prim_geom, (int)m_collisionCategories);
                    d.GeomSetCollideBits(prim_geom, (int)m_collisionFlags);
                }
                if (m_isphysical)
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
            m_taintVelocity = PhysicsVector.Zero;
        }

        public void CreateGeom(IntPtr m_targetSpace, IMesh _mesh)
        {
//Console.WriteLine("CreateGeom:");         
            if (_mesh != null)
            {
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
                            _parent_scene.waitForSpaceUnlock(m_targetSpace);
                            try
                            {
//Console.WriteLine(" CreateGeom 1");
                                SetGeom(d.CreateSphere(m_targetSpace, _size.X / 2));
                            }
                            catch (AccessViolationException)
                            {
                                m_log.Warn("[PHYSICS]: Unable to create physics proxy for object");
                                ode.dunlock(_parent_scene.world);
                                return;
                            }
                        }
                        else
                        {
                            _parent_scene.waitForSpaceUnlock(m_targetSpace);
                            try
                            {
//Console.WriteLine(" CreateGeom 2");                           
                                SetGeom(d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z));
                            }
                            catch (AccessViolationException)
                            {
                                m_log.Warn("[PHYSICS]: Unable to create physics proxy for object");
                                ode.dunlock(_parent_scene.world);
                                return;
                            }
                        }
                    }
                    else
                    {
                        _parent_scene.waitForSpaceUnlock(m_targetSpace);
                        try
                        {
//Console.WriteLine("  CreateGeom 3");                       
                            SetGeom(d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z));
                        }
                        catch (AccessViolationException)
                        {
                            m_log.Warn("[PHYSICS]: Unable to create physics proxy for object");
                            ode.dunlock(_parent_scene.world);
                            return;
                        }
                    }
                }

                else
                {
                    _parent_scene.waitForSpaceUnlock(m_targetSpace);
                    try
                    {
//Console.WriteLine("  CreateGeom 4");                  
                        SetGeom(d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z));
                    }
                    catch (AccessViolationException)
                    {
                        m_log.Warn("[PHYSICS]: Unable to create physics proxy for object");
                        ode.dunlock(_parent_scene.world);
                        return;
                    }
                }
            }
        }

        public void changeadd(float timestep)
        {
            int[] iprimspaceArrItem = _parent_scene.calculateSpaceArrayItemFromPos(_position);
            IntPtr targetspace = _parent_scene.calculateSpaceForGeom(_position);

            if (targetspace == IntPtr.Zero)
                targetspace = _parent_scene.createprimspace(iprimspaceArrItem[0], iprimspaceArrItem[1]);

            m_targetSpace = targetspace;

            if (_mesh == null)
            {
                if (_parent_scene.needsMeshing(_pbs))
                {
                    // Don't need to re-enable body..   it's done in SetMesh
                    _mesh = _parent_scene.mesher.CreateMesh(m_primName, _pbs, _size, _parent_scene.meshSculptLOD, IsPhysical);
                    // createmesh returns null when it's a shape that isn't a cube.
                   // m_log.Debug(m_localID);
                }
            }


            lock (_parent_scene.OdeLock)
            {
//Console.WriteLine("changeadd 1");           
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

            _parent_scene.geom_name_map[prim_geom] = this.m_primName;
            _parent_scene.actor_name_map[prim_geom] = (PhysicsActor)this;

            changeSelectedStatus(timestep);

            m_taintadd = false;
        }

        public void changemove(float timestep)
        {
            if (m_isphysical)
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

            changeSelectedStatus(timestep);

            resetCollisionAccounting();
            m_taintposition = _position;
        }

        public void Move(float timestep)
        {
            float fx = 0;
            float fy = 0;
            float fz = 0;

                
            if (IsPhysical && (Body != IntPtr.Zero) && !m_isSelected && !childPrim)		// KF: Only move root prims.
            {
            	if (m_vehicle.Type != Vehicle.TYPE_NONE)
            	{
            		// 'VEHICLES' are dealt with in ODEDynamics.cs
            		m_vehicle.Step(timestep, _parent_scene);
            	}
            	else
            	{
//Console.WriteLine("Move " +  m_primName);           	
            		if(!d.BodyIsEnabled (Body))  d.BodyEnable (Body); // KF add 161009
            		// NON-'VEHICLES' are dealt with here
	                if (d.BodyIsEnabled(Body) && !m_angularlock.IsIdentical(PhysicsVector.Zero, 0.003f))
	                {
	                    d.Vector3 avel2 = d.BodyGetAngularVel(Body);
	                    if (m_angularlock.X == 1)
	                        avel2.X = 0;
	                    if (m_angularlock.Y == 1)
	                        avel2.Y = 0;
	                    if (m_angularlock.Z == 1)
	                        avel2.Z = 0;
	                    d.BodySetAngularVel(Body, avel2.X, avel2.Y, avel2.Z);
	                }
	                //float PID_P = 900.0f;

	                float m_mass = CalculateMass();

//	                fz = 0f;
                    //m_log.Info(m_collisionFlags.ToString());

	                
	                //KF: m_buoyancy should be set by llSetBuoyancy() for non-vehicle.
	                // would come from SceneObjectPart.cs, public void SetBuoyancy(float fvalue) , PhysActor.Buoyancy = fvalue; ??
	                // m_buoyancy: (unlimited value) <0=Falls fast; 0=1g; 1=0g; >1 = floats up 
	                // gravityz multiplier = 1 - m_buoyancy
	                fz = _parent_scene.gravityz * (1.0f - m_buoyancy) * m_mass;

	                if (m_usePID)
	                {
//Console.WriteLine("PID " +  m_primName);           	
                    	// KF - this is for object move? eg. llSetPos() ?
	                    //if (!d.BodyIsEnabled(Body))
	                    //d.BodySetForce(Body, 0f, 0f, 0f);
	                    // If we're using the PID controller, then we have no gravity
	                    //fz = (-1 * _parent_scene.gravityz) * m_mass; 	//KF: ?? Prims have no global gravity,so simply...
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
	                        new PhysicsVector(
	                            (m_PIDTarget.X - pos.X) * ((PID_G - m_PIDTau) * timestep),
	                            (m_PIDTarget.Y - pos.Y) * ((PID_G - m_PIDTau) * timestep),
	                            (m_PIDTarget.Z - pos.Z) * ((PID_G - m_PIDTau) * timestep)
	                            );

	                    //  if velocity is zero, use position control; otherwise, velocity control

	                    if (_target_velocity.IsIdentical(PhysicsVector.Zero,0.1f))
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
	                }		// end if (m_usePID)

	                // Hover PID Controller needs to be mutually exlusive to MoveTo PID controller
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

	                    } 	// end switch (m_PIDHoverType)


	                    _target_velocity =
    	                    new PhysicsVector(0.0f, 0.0f,
    	                        (m_targetHoverHeight - pos.Z) * ((PID_G - m_PIDHoverTau) * timestep)
    	                        );

    	                //  if velocity is zero, use position control; otherwise, velocity control

    	                if (_target_velocity.IsIdentical(PhysicsVector.Zero, 0.1f))
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
            {	// is not physical, or is not a body or is selected
              //  _zeroPosition = d.BodyGetPosition(Body);
                return;
//Console.WriteLine("Nothing " +  m_primName);           	
               
            }
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
	            if (m_isphysical)
	            {
	                if (!m_angularlock.IsIdentical(new PhysicsVector(1, 1, 1), 0))
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
                        

                        if (prim_geom != IntPtr.Zero)
                        {
                            try
                            {
                                d.GeomDestroy(prim_geom);
                                prim_geom = IntPtr.Zero;
                                _mesh = null;
                            }
                            catch (System.AccessViolationException)
                            {
                                prim_geom = IntPtr.Zero;
                                m_log.Error("[PHYSICS]: PrimGeom dead");
                            }
                        }
//Console.WriteLine("changePhysicsStatus for " + m_primName );
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

            changeSelectedStatus(timestep);

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
            d.GeomDestroy(prim_geom);
            prim_geom = IntPtr.Zero;
            // we don't need to do space calculation because the client sends a position update also.

            // Construction of new prim
            if (_parent_scene.needsMeshing(_pbs))
            {
                float meshlod = _parent_scene.meshSculptLOD;

                if (IsPhysical)
                    meshlod = _parent_scene.MeshSculptphysicalLOD;
                // Don't need to re-enable body..   it's done in SetMesh

                IMesh mesh = null;

                if (_parent_scene.needsMeshing(_pbs))
                    mesh = _parent_scene.mesher.CreateMesh(oldname, _pbs, _size, meshlod, IsPhysical);

                //IMesh mesh = _parent_scene.mesher.CreateMesh(oldname, _pbs, _size, meshlod, IsPhysical);
//Console.WriteLine("changesize 1");
                CreateGeom(m_targetSpace, mesh);

               
            }
            else
            {
                _mesh = null;
//Console.WriteLine("changesize 2");    
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

            changeSelectedStatus(timestamp);
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
            try
            {
                d.GeomDestroy(prim_geom);
            }
            catch (System.AccessViolationException)
            {
                prim_geom = IntPtr.Zero;
                m_log.Error("[PHYSICS]: PrimGeom dead");
            }
            prim_geom = IntPtr.Zero;
            // we don't need to do space calculation because the client sends a position update also.
            if (_size.X <= 0) _size.X = 0.01f;
            if (_size.Y <= 0) _size.Y = 0.01f;
            if (_size.Z <= 0) _size.Z = 0.01f;
            // Construction of new prim

            if (_parent_scene.needsMeshing(_pbs))
            {
                // Don't need to re-enable body..   it's done in SetMesh
                float meshlod = _parent_scene.meshSculptLOD;

                if (IsPhysical)
                    meshlod = _parent_scene.MeshSculptphysicalLOD;

                IMesh mesh = _parent_scene.mesher.CreateMesh(oldname, _pbs, _size, meshlod, IsPhysical);
                // createmesh returns null when it doesn't mesh.
                CreateGeom(m_targetSpace, mesh);
            }
            else
            {
                _mesh = null;
//Console.WriteLine("changeshape");              
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
                d.BodyEnable(Body);
            }
            _parent_scene.geom_name_map[prim_geom] = oldname;

            changeSelectedStatus(timestamp);
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
                        PhysicsVector iforce = new PhysicsVector();
                        for (int i = 0; i < m_forcelist.Count; i++)
                        {
                            iforce = iforce + (m_forcelist[i] * 100);
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
            
            m_taintTorque = new PhysicsVector(0, 0, 0);
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
                        PhysicsVector iforce = new PhysicsVector();
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
                    {
                        d.BodySetLinearVel(Body, m_taintVelocity.X, m_taintVelocity.Y, m_taintVelocity.Z);
                    }
                }
                
                //resetCollisionAccounting();
            }
            m_taintVelocity = PhysicsVector.Zero;
        }

        public override bool IsPhysical
        {
            get { return m_isphysical; }
            set { 
                  m_isphysical = value;
                  if (!m_isphysical) // Zero the remembered last velocity
                      m_lastVelocity = new PhysicsVector(0.0f, 0.0f, 0.0f);
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

        public override PhysicsVector Position
        {
            get { return _position; }

            set { _position = value;
                //m_log.Info("[PHYSICS]: " + _position.ToString());
            }
        }

        public override PhysicsVector Size
        {
            get { return _size; }
            set
            {
                if (PhysicsVector.isFinite(value))
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
            get { return CalculateMass(); }
        }

        public override PhysicsVector Force
        {
            //get { return PhysicsVector.Zero; }
            get { return m_force; }
            set
            {
                if (PhysicsVector.isFinite(value))
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
            get { return (int)m_vehicle.Type; }
            set { m_vehicle.ProcessTypeChange((Vehicle)value); }
        }

        public override void VehicleFloatParam(int param, float value)
        {
            m_vehicle.ProcessFloatVehicleParam((Vehicle) param, value);
        }

        public override void VehicleVectorParam(int param, PhysicsVector value)
        {
            m_vehicle.ProcessVectorVehicleParam((Vehicle) param, value);
        }

        public override void VehicleRotationParam(int param, Quaternion rotation)
        {
            m_vehicle.ProcessRotationVehicleParam((Vehicle) param, rotation);
        }

        public override void SetVolumeDetect(int param)
        {
            lock (_parent_scene.OdeLock)
            {
                m_isVolumeDetect = (param!=0);
            }
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
            set
            {
                _pbs = value;
                m_taintshape = true;
            }
        }

        public override PhysicsVector Velocity
        {
            get
            {
                // Averate previous velocity with the new one so
                // client object interpolation works a 'little' better
                PhysicsVector returnVelocity = new PhysicsVector();
                returnVelocity.X = (m_lastVelocity.X + _velocity.X)/2;
                returnVelocity.Y = (m_lastVelocity.Y + _velocity.Y)/2;
                returnVelocity.Z = (m_lastVelocity.Z + _velocity.Z)/2;
                return returnVelocity;
            }
            set
            {
                if (PhysicsVector.isFinite(value))
                {
                    _velocity = value;

                    m_taintVelocity = value;
                    _parent_scene.AddPhysicsActorTaint(this);
                }
                else
                {
                    m_log.Warn("[PHYSICS]: Got NaN Velocity in Object");
                }

            }
        }

        public override PhysicsVector Torque
        {
            get
            {
                if (!m_isphysical || Body == IntPtr.Zero)
                    return new PhysicsVector(0,0,0);

                return _torque;
            }

            set
            {
                if (PhysicsVector.isFinite(value))
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

        public override PhysicsVector Acceleration
        {
            get { return _acceleration; }
        }


        public void SetAcceleration(PhysicsVector accel)
        {
            _acceleration = accel;
        }

        public override void AddForce(PhysicsVector force, bool pushforce)
        {
            if (PhysicsVector.isFinite(force))
            {
                m_forcelist.Add(force);
                m_taintforce = true;
            }
            else
            {
                m_log.Warn("[PHYSICS]: Got Invalid linear force vector from Scene in Object");
            }
            //m_log.Info("[PHYSICS]: Added Force:" + force.ToString() +  " to prim at " + Position.ToString());
        }

        public override void AddAngularForce(PhysicsVector force, bool pushforce)
        {
            if (PhysicsVector.isFinite(force))
            {
                m_angularforcelist.Add(force);
                m_taintaddangularforce = true;
            }
            else
            {
                m_log.Warn("[PHYSICS]: Got Invalid Angular force vector from Scene in Object");
            }
        }

        public override PhysicsVector RotationalVelocity
        {
            get
            {
                PhysicsVector pv = new PhysicsVector(0, 0, 0);
                if (_zeroFlag)
                    return pv;
                m_lastUpdateSent = false;

                if (m_rotationalVelocity.IsIdentical(pv, 0.2f))
                    return pv;

                return m_rotationalVelocity;
            }
            set
            {
                if (PhysicsVector.isFinite(value))
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

        public override void LockAngularMotion(PhysicsVector axis)
        {
            // reverse the zero/non zero values for ODE.
            if (PhysicsVector.isFinite(axis))
            {
                axis.X = (axis.X > 0) ? 1f : 0f;
                axis.Y = (axis.Y > 0) ? 1f : 0f;
                axis.Z = (axis.Z > 0) ? 1f : 0f;
                m_log.DebugFormat("[axislock]: <{0},{1},{2}>", axis.X, axis.Y, axis.Z);
                m_taintAngularLock = new PhysicsVector(axis.X, axis.Y, axis.Z);
            }
            else
            {
                m_log.Warn("[PHYSICS]: Got NaN locking axis from Scene on Object");
            }
        }

        public void UpdatePositionAndVelocity()
        {
            //  no lock; called from Simulate() -- if you call this from elsewhere, gotta lock or do Monitor.Enter/Exit!
            if (_parent == null)
            {
                PhysicsVector pv = new PhysicsVector(0, 0, 0);
                bool lastZeroFlag = _zeroFlag;
                if (Body != (IntPtr)0) // FIXME -> or if it is a joint
                {
                    d.Vector3 vec = d.BodyGetPosition(Body);
                    d.Quaternion ori = d.BodyGetQuaternion(Body);
                    d.Vector3 vel = d.BodyGetLinearVel(Body);
                    d.Vector3 rotvel = d.BodyGetAngularVel(Body);
                    d.Vector3 torque = d.BodyGetTorque(Body);
                    _torque.setValues(torque.X, torque.Y, torque.Z);
                    PhysicsVector l_position = new PhysicsVector();
                    Quaternion l_orientation = new Quaternion();

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

					float Adiff = 1.0f - Math.Abs(Quaternion.Dot(m_lastorientation, l_orientation));
//Console.WriteLine("Adiff " + m_primName + " = " + Adiff);					
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

                        _velocity.X = vel.X;
                        _velocity.Y = vel.Y;
                        _velocity.Z = vel.Z;

                        _acceleration = ((_velocity - m_lastVelocity) / 0.1f);
                        _acceleration = new PhysicsVector(_velocity.X - m_lastVelocity.X / 0.1f, _velocity.Y - m_lastVelocity.Y / 0.1f, _velocity.Z - m_lastVelocity.Z / 0.1f);
                        //m_log.Info("[PHYSICS]: V1: " + _velocity + " V2: " + m_lastVelocity + " Acceleration: " + _acceleration.ToString());

                        if (_velocity.IsIdentical(pv, 0.5f))
                        {
                            m_rotationalVelocity = pv;
                        }
                        else
                        {
                            m_rotationalVelocity.setValues(rotvel.X, rotvel.Y, rotvel.Z);
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
                                base.RequestPhysicsterseUpdate();
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

        public override void SetMomentum(PhysicsVector momentum)
        {
        }

        public override PhysicsVector PIDTarget 
        { 
            set
            {
                if (PhysicsVector.isFinite(value))
                {
                    m_PIDTarget = value;
                }
                else
                    m_log.Warn("[PHYSICS]: Got NaN PIDTarget from Scene on Object");
            } 
        }
        public override bool PIDActive { set { m_usePID = value; } }
        public override float PIDTau { set { m_PIDTau = value; } }

        public override float PIDHoverHeight { set { m_PIDHoverHeight = value; ; } }
        public override bool PIDHoverActive { set { m_useHoverPID = value; } }
        public override PIDHoverType PIDHoverType { set { m_PIDHoverType = value; } }
        public override float PIDHoverTau { set { m_PIDHoverTau = value; } }

        private void createAMotor(PhysicsVector axis)
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

        public Matrix4 FromDMass(d.Mass pMass)
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

        public void AddCollisionEvent(uint CollidedWith, float depth)
        {
            if (CollisionEventsThisFrame == null)
                CollisionEventsThisFrame = new CollisionEventUpdate();
            CollisionEventsThisFrame.addCollider(CollidedWith,depth);
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

    }
}
