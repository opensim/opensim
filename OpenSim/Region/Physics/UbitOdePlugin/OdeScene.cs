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

//#define SPAM

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Diagnostics;
using log4net;
using Nini.Config;
using OdeAPI;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using OpenMetaverse;

namespace OpenSim.Region.Physics.OdePlugin
{
    public enum StatusIndicators : int
    {
        Generic = 0,
        Start = 1,
        End = 2
    }

    public struct sCollisionData
    {
        public uint ColliderLocalId;
        public uint CollidedWithLocalId;
        public int NumberOfCollisions;
        public int CollisionType;
        public int StatusIndicator;
        public int lastframe;
    }


    // colision flags of things others can colide with
    // rays, sensors, probes removed since can't  be colided with
    // The top space where things are placed provided further selection
    // ie physical are in active space nonphysical in static
    // this should be exclusive as possible

    [Flags]
    public enum CollisionCategories : uint
    {
        Disabled = 0,
        //by 'things' types
        Space =         0x01,
        Geom =          0x02, // aka prim/part
        Character =     0x04,
        Land =          0x08,
        Water =         0x010,

        // by state
        Phantom =       0x01000,
        VolumeDtc =     0x02000,
        Selected =      0x04000,
        NoShape =       0x08000,


        All =           0xffffffff
    }

    /// <summary>
    /// Material type for a primitive
    /// </summary>
    public enum Material : int
    {
        /// <summary></summary>
        Stone = 0,
        /// <summary></summary>
        Metal = 1,
        /// <summary></summary>
        Glass = 2,
        /// <summary></summary>
        Wood = 3,
        /// <summary></summary>
        Flesh = 4,
        /// <summary></summary>
        Plastic = 5,
        /// <summary></summary>
        Rubber = 6,

        light = 7 // compatibility with old viewers
    }
    
    public enum changes : int
    {
        Add = 0,                // arg null. finishs the prim creation. should be used internally only ( to remove later ?)
        Remove,
        Link,               // arg AuroraODEPrim new parent prim or null to delink. Makes the prim part of a object with prim parent as root
        //  or removes from a object if arg is null
        DeLink,
        Position,           // arg Vector3 new position in world coords. Changes prim position. Prim must know if it is root or child
        Orientation,        // arg Quaternion new orientation in world coords. Changes prim position. Prim must know it it is root or child
        PosOffset,          // not in use
        // arg Vector3 new position in local coords. Changes prim position in object
        OriOffset,          // not in use
        // arg Vector3 new position in local coords. Changes prim position in object
        Velocity,
        AngVelocity,
        Acceleration,
        Force,
        Torque,
        Momentum,

        AddForce,
        AddAngForce,
        AngLock,

        Buoyancy,

        PIDTarget,
        PIDTau,
        PIDActive,

        PIDHoverHeight,
        PIDHoverType,
        PIDHoverTau,
        PIDHoverActive,

        Size,
        AvatarSize,
        Shape,
        PhysRepData,
        AddPhysRep,

        CollidesWater,
        VolumeDtc,

        Physical,
        Phantom,
        Selected,
        disabled,
        building,

        VehicleType,
        VehicleFloatParam,
        VehicleVectorParam,
        VehicleRotationParam,
        VehicleFlags,
        SetVehicle,

        Null             //keep this last used do dim the methods array. does nothing but pulsing the prim
    }

    public struct ODEchangeitem
    {
        public PhysicsActor actor;
        public OdeCharacter character;
        public changes what;
        public Object arg;
    }

    

    public class OdeScene : PhysicsScene
    {
        private readonly ILog m_log;
        // private Dictionary<string, sCollisionData> m_storedCollisions = new Dictionary<string, sCollisionData>();

        public bool OdeUbitLib = false;
//        private int threadid = 0;
        private Random fluidRandomizer = new Random(Environment.TickCount);

        const d.ContactFlags comumContactFlags = d.ContactFlags.SoftERP | d.ContactFlags.SoftCFM |d.ContactFlags.Approx1 | d.ContactFlags.Bounce;
        const float MaxERP = 0.8f;
        const float minERP = 0.1f;
        const float comumContactCFM = 0.0001f;
        
        float frictionMovementMult = 0.8f;

        float TerrainBounce = 0.1f;
        float TerrainFriction = 0.3f;

        public float AvatarFriction = 0;// 0.9f * 0.5f;

        private const uint m_regionWidth = Constants.RegionSize;
        private const uint m_regionHeight = Constants.RegionSize;

        public float ODE_STEPSIZE = 0.020f;
        public float HalfOdeStep = 0.01f;
        public int odetimestepMS = 20; // rounded
        private float metersInSpace = 25.6f;
        private float m_timeDilation = 1.0f;

        private DateTime m_lastframe;
        private DateTime m_lastMeshExpire;

        public float gravityx = 0f;
        public float gravityy = 0f;
        public float gravityz = -9.8f;

        private float waterlevel = 0f;
        private int framecount = 0;

        private int m_meshExpireCntr;

//        private IntPtr WaterGeom = IntPtr.Zero;
//        private IntPtr WaterHeightmapData = IntPtr.Zero;
//        private GCHandle WaterMapHandler = new GCHandle();

        private float avDensity = 3f;
        private float avMovementDivisorWalk = 1.3f;
        private float avMovementDivisorRun = 0.8f;
        private float minimumGroundFlightOffset = 3f;
        public float maximumMassObject = 10000.01f;


        public float geomDefaultDensity = 10.000006836f;

        public int geomContactPointsStartthrottle = 3;
        public int geomUpdatesPerThrottledUpdate = 15;

        public float bodyPIDD = 35f;
        public float bodyPIDG = 25;

//        public int geomCrossingFailuresBeforeOutofbounds = 6;

        public int bodyFramesAutoDisable = 5;


        private d.NearCallback nearCallback;

        private HashSet<OdeCharacter> _characters = new HashSet<OdeCharacter>();
        private HashSet<OdePrim> _prims = new HashSet<OdePrim>();
        private HashSet<OdePrim> _activeprims = new HashSet<OdePrim>();
        private HashSet<OdePrim> _activegroups = new HashSet<OdePrim>();

        public OpenSim.Framework.LocklessQueue<ODEchangeitem> ChangesQueue = new OpenSim.Framework.LocklessQueue<ODEchangeitem>();

        /// <summary>
        /// A list of actors that should receive collision events.
        /// </summary>
        private List<PhysicsActor> _collisionEventPrim = new List<PhysicsActor>();
        private List<PhysicsActor> _collisionEventPrimRemove = new List<PhysicsActor>();
        
        private HashSet<OdeCharacter> _badCharacter = new HashSet<OdeCharacter>();
//        public Dictionary<IntPtr, String> geom_name_map = new Dictionary<IntPtr, String>();
        public Dictionary<IntPtr, PhysicsActor> actor_name_map = new Dictionary<IntPtr, PhysicsActor>();

        private float contactsurfacelayer = 0.002f;

        private int contactsPerCollision = 80;
        internal IntPtr ContactgeomsArray = IntPtr.Zero;
        private IntPtr GlobalContactsArray = IntPtr.Zero;

        const int maxContactsbeforedeath = 4000;
        private volatile int m_global_contactcount = 0;

        private IntPtr contactgroup;

        public ContactData[] m_materialContactsData = new ContactData[8];

        private Dictionary<Vector3, IntPtr> RegionTerrain = new Dictionary<Vector3, IntPtr>();
        private Dictionary<IntPtr, float[]> TerrainHeightFieldHeights = new Dictionary<IntPtr, float[]>();
        private Dictionary<IntPtr, GCHandle> TerrainHeightFieldHeightsHandlers = new Dictionary<IntPtr, GCHandle>();
       
        private int m_physicsiterations = 10;
        private const float m_SkipFramesAtms = 0.40f; // Drop frames gracefully at a 400 ms lag
//        private PhysicsActor PANull = new NullPhysicsActor();
        private float step_time = 0.0f;

        public IntPtr world;


        // split the spaces acording to contents type
        // ActiveSpace contains characters and active prims
        // StaticSpace contains land and other that is mostly static in enviroment
        // this can contain subspaces, like the grid in staticspace
        // as now space only contains this 2 top spaces

        public IntPtr TopSpace; // the global space
        public IntPtr ActiveSpace; // space for active prims
        public IntPtr CharsSpace; // space for active prims
        public IntPtr StaticSpace; // space for the static things around
        public IntPtr GroundSpace; // space for ground

        public IntPtr SharedRay;

        // some speedup variables
        private int spaceGridMaxX;
        private int spaceGridMaxY;
        private float spacesPerMeter;

        // split static geometry collision into a grid as before
        private IntPtr[,] staticPrimspace;
        private IntPtr[] staticPrimspaceOffRegion;

        public Object OdeLock;
        public static Object SimulationLock;

        public IMesher mesher;

        private IConfigSource m_config;

        public bool physics_logging = false;
        public int physics_logging_interval = 0;
        public bool physics_logging_append_existing_logfile = false;

        private Vector3 m_worldOffset = Vector3.Zero;
        public Vector2 WorldExtents = new Vector2((int)Constants.RegionSize, (int)Constants.RegionSize);
        private PhysicsScene m_parentScene = null;

        private ODERayCastRequestManager m_rayCastManager;
        public ODEMeshWorker m_meshWorker;

/* maybe needed if ode uses tls
        private void checkThread()
        {

            int th = Thread.CurrentThread.ManagedThreadId;
            if(th != threadid)
            {
                threadid = th;
                d.AllocateODEDataForThread(~0U);
            }
        }
 */
        /// <summary>
        /// Initiailizes the scene
        /// Sets many properties that ODE requires to be stable
        /// These settings need to be tweaked 'exactly' right or weird stuff happens.
        /// </summary>
        public OdeScene(string sceneIdentifier)
            {
            m_log 
                = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.ToString() + "." + sceneIdentifier);

//            checkThread();
            Name = sceneIdentifier;

            OdeLock = new Object();
            SimulationLock = new Object();

            nearCallback = near;

            m_rayCastManager = new ODERayCastRequestManager(this);         

            lock (OdeLock)
                {
                // Create the world and the first space
                try
                    {
                    world = d.WorldCreate();
                    TopSpace = d.HashSpaceCreate(IntPtr.Zero);

                    // now the major subspaces
                    ActiveSpace = d.HashSpaceCreate(TopSpace);
                    CharsSpace = d.HashSpaceCreate(TopSpace);
                    StaticSpace = d.HashSpaceCreate(TopSpace);
                    GroundSpace = d.HashSpaceCreate(TopSpace);
                    }
                catch
                    {
                    // i must RtC#FM 
                    // i did!
                    }

                d.HashSpaceSetLevels(TopSpace, -2, 8);
                d.HashSpaceSetLevels(ActiveSpace, -2, 8);
                d.HashSpaceSetLevels(CharsSpace, -4, 3);
                d.HashSpaceSetLevels(StaticSpace, -2, 8);
                d.HashSpaceSetLevels(GroundSpace, 0, 8);

                // demote to second level
                d.SpaceSetSublevel(ActiveSpace, 1);
                d.SpaceSetSublevel(CharsSpace, 1);
                d.SpaceSetSublevel(StaticSpace, 1);
                d.SpaceSetSublevel(GroundSpace, 1);

                d.GeomSetCategoryBits(ActiveSpace, (uint)(CollisionCategories.Space |
                                                        CollisionCategories.Geom |
                                                        CollisionCategories.Character |
                                                        CollisionCategories.Phantom |
                                                        CollisionCategories.VolumeDtc
                                                        ));
                d.GeomSetCollideBits(ActiveSpace, (uint)(CollisionCategories.Space |
                                                        CollisionCategories.Geom |
                                                        CollisionCategories.Character |
                                                        CollisionCategories.Phantom |
                                                        CollisionCategories.VolumeDtc
                                                        ));
                d.GeomSetCategoryBits(CharsSpace, (uint)(CollisionCategories.Space |
                                        CollisionCategories.Geom |
                                        CollisionCategories.Character |
                                        CollisionCategories.Phantom |
                                        CollisionCategories.VolumeDtc
                                        ));
                d.GeomSetCollideBits(CharsSpace, 0);

                d.GeomSetCategoryBits(StaticSpace, (uint)(CollisionCategories.Space |
                                                        CollisionCategories.Geom |
//                                                        CollisionCategories.Land |
//                                                        CollisionCategories.Water |
                                                        CollisionCategories.Phantom |
                                                        CollisionCategories.VolumeDtc
                                                        ));
                d.GeomSetCollideBits(StaticSpace, 0);

                d.GeomSetCategoryBits(GroundSpace, (uint)(CollisionCategories.Land));
                d.GeomSetCollideBits(GroundSpace, 0);

                contactgroup = d.JointGroupCreate(0);
                //contactgroup

                SharedRay = d.CreateRay(TopSpace, 1.0f);

                d.WorldSetAutoDisableFlag(world, false);
            }
        }

        // Initialize the mesh plugin
//        public override void Initialise(IMesher meshmerizer, IConfigSource config, RegionInfo region )
        public override void Initialise(IMesher meshmerizer, IConfigSource config)
        {
//            checkThread();
            mesher = meshmerizer;
            m_config = config;

            string ode_config = d.GetConfiguration();
            if (ode_config != null && ode_config != "")
            {
                m_log.WarnFormat("ODE configuration: {0}", ode_config);

                if (ode_config.Contains("ODE_Ubit"))
                {
                    OdeUbitLib = true;
                }
            }

            /*
                        if (region != null)
                        {
                            WorldExtents.X = region.RegionSizeX;
                            WorldExtents.Y = region.RegionSizeY;
                        }
             */

            // Defaults

            int contactsPerCollision = 80;

            IConfig physicsconfig = null;

            if (m_config != null)
            {
                physicsconfig = m_config.Configs["ODEPhysicsSettings"];
                if (physicsconfig != null)
                {
                    gravityx = physicsconfig.GetFloat("world_gravityx", gravityx);
                    gravityy = physicsconfig.GetFloat("world_gravityy", gravityy);
                    gravityz = physicsconfig.GetFloat("world_gravityz", gravityz);

                    metersInSpace = physicsconfig.GetFloat("meters_in_small_space", metersInSpace);

                    contactsurfacelayer = physicsconfig.GetFloat("world_contact_surface_layer", contactsurfacelayer);

                    ODE_STEPSIZE = physicsconfig.GetFloat("world_stepsize", ODE_STEPSIZE);
                    m_physicsiterations = physicsconfig.GetInt("world_internal_steps_without_collisions", m_physicsiterations);

                    avDensity = physicsconfig.GetFloat("av_density", avDensity);
                    avMovementDivisorWalk = physicsconfig.GetFloat("av_movement_divisor_walk", avMovementDivisorWalk);
                    avMovementDivisorRun = physicsconfig.GetFloat("av_movement_divisor_run", avMovementDivisorRun);

                    contactsPerCollision = physicsconfig.GetInt("contacts_per_collision", contactsPerCollision);

                    geomContactPointsStartthrottle = physicsconfig.GetInt("geom_contactpoints_start_throttling", 3);
                    geomUpdatesPerThrottledUpdate = physicsconfig.GetInt("geom_updates_before_throttled_update", 15);
//                    geomCrossingFailuresBeforeOutofbounds = physicsconfig.GetInt("geom_crossing_failures_before_outofbounds", 5);

                    geomDefaultDensity = physicsconfig.GetFloat("geometry_default_density", geomDefaultDensity);
                    bodyFramesAutoDisable = physicsconfig.GetInt("body_frames_auto_disable", bodyFramesAutoDisable);

                    physics_logging = physicsconfig.GetBoolean("physics_logging", false);
                    physics_logging_interval = physicsconfig.GetInt("physics_logging_interval", 0);
                    physics_logging_append_existing_logfile = physicsconfig.GetBoolean("physics_logging_append_existing_logfile", false);

                    minimumGroundFlightOffset = physicsconfig.GetFloat("minimum_ground_flight_offset", minimumGroundFlightOffset);
                    maximumMassObject = physicsconfig.GetFloat("maximum_mass_object", maximumMassObject);
                }
            }

            m_meshWorker = new ODEMeshWorker(this, m_log, meshmerizer, physicsconfig);

            HalfOdeStep = ODE_STEPSIZE * 0.5f;
            odetimestepMS = (int)(1000.0f * ODE_STEPSIZE +0.5f);

            ContactgeomsArray = Marshal.AllocHGlobal(contactsPerCollision * d.ContactGeom.unmanagedSizeOf);
            GlobalContactsArray = Marshal.AllocHGlobal(maxContactsbeforedeath * d.Contact.unmanagedSizeOf);

            m_materialContactsData[(int)Material.Stone].mu = 0.8f;
            m_materialContactsData[(int)Material.Stone].bounce = 0.4f;

            m_materialContactsData[(int)Material.Metal].mu = 0.3f;
            m_materialContactsData[(int)Material.Metal].bounce = 0.4f;

            m_materialContactsData[(int)Material.Glass].mu = 0.2f;
            m_materialContactsData[(int)Material.Glass].bounce = 0.7f;

            m_materialContactsData[(int)Material.Wood].mu = 0.6f;
            m_materialContactsData[(int)Material.Wood].bounce = 0.5f;

            m_materialContactsData[(int)Material.Flesh].mu = 0.9f;
            m_materialContactsData[(int)Material.Flesh].bounce = 0.3f;

            m_materialContactsData[(int)Material.Plastic].mu = 0.4f;
            m_materialContactsData[(int)Material.Plastic].bounce = 0.7f;

            m_materialContactsData[(int)Material.Rubber].mu = 0.9f;
            m_materialContactsData[(int)Material.Rubber].bounce = 0.95f;

            m_materialContactsData[(int)Material.light].mu = 0.0f;
            m_materialContactsData[(int)Material.light].bounce = 0.0f;

            // Set the gravity,, don't disable things automatically (we set it explicitly on some things)

            d.WorldSetGravity(world, gravityx, gravityy, gravityz);
            d.WorldSetContactSurfaceLayer(world, contactsurfacelayer);

            d.WorldSetLinearDamping(world, 0.002f);
            d.WorldSetAngularDamping(world, 0.002f);
            d.WorldSetAngularDampingThreshold(world, 0f);
            d.WorldSetLinearDampingThreshold(world, 0f);
            d.WorldSetMaxAngularSpeed(world, 100f);

            d.WorldSetCFM(world,1e-6f); // a bit harder than default
            //d.WorldSetCFM(world, 1e-4f); // a bit harder than default
            d.WorldSetERP(world, 0.6f); // higher than original

            // Set how many steps we go without running collision testing
            // This is in addition to the step size.
            // Essentially Steps * m_physicsiterations
            d.WorldSetQuickStepNumIterations(world, m_physicsiterations);

            d.WorldSetContactMaxCorrectingVel(world, 60.0f);

            spacesPerMeter = 1 / metersInSpace;
            spaceGridMaxX = (int)(WorldExtents.X * spacesPerMeter);
            spaceGridMaxY = (int)(WorldExtents.Y * spacesPerMeter);

            staticPrimspace = new IntPtr[spaceGridMaxX, spaceGridMaxY];

            // create all spaces now
            int i, j;
            IntPtr newspace;

            for (i = 0; i < spaceGridMaxX; i++)
                for (j = 0; j < spaceGridMaxY; j++)
                {
                    newspace = d.HashSpaceCreate(StaticSpace);
                    d.GeomSetCategoryBits(newspace, (int)CollisionCategories.Space);
                    waitForSpaceUnlock(newspace);
                    d.SpaceSetSublevel(newspace, 2);
                    d.HashSpaceSetLevels(newspace, -2, 8);
                    d.GeomSetCategoryBits(newspace, (uint)(CollisionCategories.Space |
                                        CollisionCategories.Geom |
                                        CollisionCategories.Land |
                                        CollisionCategories.Water |
                                        CollisionCategories.Phantom |
                                        CollisionCategories.VolumeDtc
                                        ));
                    d.GeomSetCollideBits(newspace, 0);

                    staticPrimspace[i, j] = newspace;
                }
            // let this now be real maximum values
            spaceGridMaxX--;
            spaceGridMaxY--;

            // create 4 off world spaces (x<0,x>max,y<0,y>max)
            staticPrimspaceOffRegion = new IntPtr[4];

            for (i = 0; i < 4; i++)
                {
                    newspace = d.HashSpaceCreate(StaticSpace);
                    d.GeomSetCategoryBits(newspace, (int)CollisionCategories.Space);
                    waitForSpaceUnlock(newspace);
                    d.SpaceSetSublevel(newspace, 2);
                    d.HashSpaceSetLevels(newspace, -2, 8);
                    d.GeomSetCategoryBits(newspace, (uint)(CollisionCategories.Space |
                                        CollisionCategories.Geom |
                                        CollisionCategories.Land |
                                        CollisionCategories.Water |
                                        CollisionCategories.Phantom |
                                        CollisionCategories.VolumeDtc
                                        ));
                    d.GeomSetCollideBits(newspace, 0);

                    staticPrimspaceOffRegion[i] = newspace;
                }

            m_lastframe = DateTime.UtcNow;
            m_lastMeshExpire = m_lastframe;
        }

        internal void waitForSpaceUnlock(IntPtr space)
        {
            //if (space != IntPtr.Zero)
                //while (d.SpaceLockQuery(space)) { } // Wait and do nothing
        }

        #region Collision Detection

        // sets a global contact for a joint for contactgeom , and base contact description)

        private IntPtr CreateContacJoint(ref d.ContactGeom contactGeom, float mu, float bounce, float cfm, float erpscale, float dscale)
        {
            if (GlobalContactsArray == IntPtr.Zero || m_global_contactcount >= maxContactsbeforedeath)
                return IntPtr.Zero;

            float erp = contactGeom.depth;
            erp *= erpscale;
            if (erp < minERP)
                erp = minERP;
            else if (erp > MaxERP)
                erp = MaxERP;

            float depth = contactGeom.depth * dscale;
            if (depth > 0.5f)
                depth = 0.5f;

            d.Contact newcontact = new d.Contact();
            newcontact.geom.depth = depth;
            newcontact.geom.g1 = contactGeom.g1;
            newcontact.geom.g2 = contactGeom.g2;
            newcontact.geom.pos = contactGeom.pos;
            newcontact.geom.normal = contactGeom.normal;
            newcontact.geom.side1 = contactGeom.side1;
            newcontact.geom.side2 = contactGeom.side2;

            // this needs bounce also
            newcontact.surface.mode = comumContactFlags;
            newcontact.surface.mu = mu;
            newcontact.surface.bounce = bounce;
            newcontact.surface.soft_cfm = cfm;
            newcontact.surface.soft_erp = erp;

            IntPtr contact = new IntPtr(GlobalContactsArray.ToInt64() + (Int64)(m_global_contactcount * d.Contact.unmanagedSizeOf));
            Marshal.StructureToPtr(newcontact, contact, true);
            return d.JointCreateContactPtr(world, contactgroup, contact);
        }

        private bool GetCurContactGeom(int index, ref d.ContactGeom newcontactgeom)
        {
            if (ContactgeomsArray == IntPtr.Zero || index >= contactsPerCollision)
                return false;

            IntPtr contactptr = new IntPtr(ContactgeomsArray.ToInt64() + (Int64)(index * d.ContactGeom.unmanagedSizeOf));
            newcontactgeom = (d.ContactGeom)Marshal.PtrToStructure(contactptr, typeof(d.ContactGeom));
            return true;
        }

        /// <summary>
        /// This is our near callback.  A geometry is near a body
        /// </summary>
        /// <param name="space">The space that contains the geoms.  Remember, spaces are also geoms</param>
        /// <param name="g1">a geometry or space</param>
        /// <param name="g2">another geometry or space</param>
        /// 

        private void near(IntPtr space, IntPtr g1, IntPtr g2)
        {
            //  no lock here!  It's invoked from within Simulate(), which is thread-locked

            if (m_global_contactcount >= maxContactsbeforedeath)
                return;

            // Test if we're colliding a geom with a space.
            // If so we have to drill down into the space recursively

            if (g1 == IntPtr.Zero || g2 == IntPtr.Zero)
                return;

            if (d.GeomIsSpace(g1) || d.GeomIsSpace(g2))
            {
                // We'll be calling near recursivly if one
                // of them is a space to find all of the
                // contact points in the space
                try
                {
                    d.SpaceCollide2(g1, g2, IntPtr.Zero, nearCallback);
                }
                catch (AccessViolationException)
                {
                    m_log.Warn("[PHYSICS]: Unable to collide test a space");
                    return;
                }
                //here one should check collisions of geoms inside a space
                // but on each space we only should have geoms that not colide amoung each other
                // so we don't dig inside spaces
                return;
            }

            // get geom bodies to check if we already a joint contact
            // guess this shouldn't happen now
            IntPtr b1 = d.GeomGetBody(g1);
            IntPtr b2 = d.GeomGetBody(g2);

            // d.GeomClassID id = d.GeomGetClass(g1);

            // Figure out how many contact points we have
            int count = 0;
            try
            {
                // Colliding Geom To Geom
                // This portion of the function 'was' blatantly ripped off from BoxStack.cs

                if (g1 == g2)
                    return; // Can't collide with yourself

                if (b1 != IntPtr.Zero && b2 != IntPtr.Zero && d.AreConnectedExcluding(b1, b2, d.JointType.Contact))
                    return;
                /*
                // debug
                                PhysicsActor dp2;
                                if (d.GeomGetClass(g1) == d.GeomClassID.HeightfieldClass)
                                {
                                    d.AABB aabb;
                                    d.GeomGetAABB(g2, out aabb);
                                    float x = aabb.MaxX - aabb.MinX;
                                    float y = aabb.MaxY - aabb.MinY;
                                    float z = aabb.MaxZ - aabb.MinZ;
                                    if (x > 60.0f || y > 60.0f || z > 60.0f)
                                    {
                                        if (!actor_name_map.TryGetValue(g2, out dp2))
                                            m_log.WarnFormat("[PHYSICS]: failed actor mapping for geom 2");
                                        else
                                            m_log.WarnFormat("[PHYSICS]: land versus large prim geo {0},size {1}, AABBsize <{2},{3},{4}>, at {5} ori {6},({7})",
                                                dp2.Name, dp2.Size, x, y, z,
                                                dp2.Position.ToString(),
                                                dp2.Orientation.ToString(),
                                                dp2.Orientation.Length());
                                        return;
                                    }
                                }
                //
                */


                if (d.GeomGetCategoryBits(g1) == (uint)CollisionCategories.VolumeDtc ||
                    d.GeomGetCategoryBits(g2) == (uint)CollisionCategories.VolumeDtc)
                {
                    int cflags;
                    unchecked
                    {
                        cflags = (int)(1 | d.CONTACTS_UNIMPORTANT);
                    }
                    count = d.CollidePtr(g1, g2, cflags, ContactgeomsArray, d.ContactGeom.unmanagedSizeOf);
                }
                else
                    count = d.CollidePtr(g1, g2, (contactsPerCollision & 0xffff), ContactgeomsArray, d.ContactGeom.unmanagedSizeOf);
            }
            catch (SEHException)
            {
                m_log.Error("[PHYSICS]: The Operating system shut down ODE because of corrupt memory.  This could be a result of really irregular terrain.  If this repeats continuously, restart using Basic Physics and terrain fill your terrain.  Restarting the sim.");
                //                ode.drelease(world);
                base.TriggerPhysicsBasedRestart();
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[PHYSICS]: Unable to collide test an object: {0}", e.Message);
                return;
            }

            // contacts done
            if (count == 0)
                return;

            // try get physical actors 
            PhysicsActor p1;
            PhysicsActor p2;

            if (!actor_name_map.TryGetValue(g1, out p1))
            {
                m_log.WarnFormat("[PHYSICS]: failed actor mapping for geom 1");
                return;
            }

            if (!actor_name_map.TryGetValue(g2, out p2))
            {
                m_log.WarnFormat("[PHYSICS]: failed actor mapping for geom 2");
                return;
            }

            // update actors collision score
            if (p1.CollisionScore >= float.MaxValue - count)
                p1.CollisionScore = 0;
            p1.CollisionScore += count;

            if (p2.CollisionScore >= float.MaxValue - count)
                p2.CollisionScore = 0;
            p2.CollisionScore += count;

            // get first contact
            d.ContactGeom curContact = new d.ContactGeom();

            if (!GetCurContactGeom(0, ref curContact))
                return;

            // do volume detection case
            if ((p1.IsVolumeDtc || p2.IsVolumeDtc))
            {
                ContactPoint maxDepthContact = new ContactPoint(
                    new Vector3(curContact.pos.X, curContact.pos.Y, curContact.pos.Z),
                    new Vector3(curContact.normal.X, curContact.normal.Y, curContact.normal.Z),
                    curContact.depth, false
                    );

                collision_accounting_events(p1, p2, maxDepthContact);
                return;
            }

            // big messy collision analises

            float mu = 0;
            float bounce = 0;
            float cfm = 0.0001f;
            float erpscale = 1.0f;
            float dscale = 1.0f;
            bool IgnoreNegSides = false;

            ContactData contactdata1 = new ContactData(0, 0, false);
            ContactData contactdata2 = new ContactData(0, 0, false);

            bool dop1ava = false;
            bool dop2ava = false;
            bool ignore = false;

            switch (p1.PhysicsActorType)
            {
                case (int)ActorTypes.Agent:
                    {
                        dop1ava = true;
                        switch (p2.PhysicsActorType)
                        {
                            case (int)ActorTypes.Agent:
                                p1.CollidingObj = true;
                                p2.CollidingObj = true;
                                break;

                            case (int)ActorTypes.Prim:
                                if (p2.Velocity.LengthSquared() > 0.0f)
                                    p2.CollidingObj = true;
                                break;

                            default:
                                ignore = true; // avatar to terrain and water ignored
                                break;
                        }
                        break;
                    }

                case (int)ActorTypes.Prim:
                    switch (p2.PhysicsActorType)
                    {
                        case (int)ActorTypes.Agent:

                            dop2ava = true;

                            if (p1.Velocity.LengthSquared() > 0.0f)
                                p1.CollidingObj = true;
                            break;

                        case (int)ActorTypes.Prim:
                            if ((p1.Velocity - p2.Velocity).LengthSquared() > 0.0f)
                            {
                                p1.CollidingObj = true;
                                p2.CollidingObj = true;
                            }
                            p1.getContactData(ref contactdata1);
                            p2.getContactData(ref contactdata2);
                            bounce = contactdata1.bounce * contactdata2.bounce;
                            mu = (float)Math.Sqrt(contactdata1.mu * contactdata2.mu);

                            cfm = p1.Mass;
                            if (cfm > p2.Mass)
                                cfm = p2.Mass;
                            dscale = 10 / cfm;
                            dscale = (float)Math.Sqrt(dscale);
                            if (dscale > 1.0f)
                                dscale = 1.0f;
                            erpscale = cfm * 0.01f;
                            cfm = 0.0001f / cfm;
                            if (cfm > 0.01f)
                                cfm = 0.01f;
                            else if (cfm < 0.00001f)
                                cfm = 0.00001f;

                            if ((Math.Abs(p2.Velocity.X - p1.Velocity.X) > 0.1f || Math.Abs(p2.Velocity.Y - p1.Velocity.Y) > 0.1f))
                                mu *= frictionMovementMult;

                            break;

                        case (int)ActorTypes.Ground:
                            p1.getContactData(ref contactdata1);
                            bounce = contactdata1.bounce * TerrainBounce;
                            mu = (float)Math.Sqrt(contactdata1.mu * TerrainFriction);
                            if (Math.Abs(p1.Velocity.X) > 0.1f || Math.Abs(p1.Velocity.Y) > 0.1f)
                                mu *= frictionMovementMult;
                            p1.CollidingGround = true;

                            cfm = p1.Mass;
                            dscale = 10 / cfm;
                            dscale = (float)Math.Sqrt(dscale);
                            if (dscale > 1.0f)
                                dscale = 1.0f;
                            erpscale = cfm * 0.01f;
                            cfm = 0.0001f / cfm;
                            if (cfm > 0.01f)
                                cfm = 0.01f;
                            else if (cfm < 0.00001f)
                                cfm = 0.00001f;

                            if (d.GeomGetClass(g1) == d.GeomClassID.TriMeshClass)
                            {
                                if (curContact.side1 > 0)
                                    IgnoreNegSides = true;
                            }
                            break;

                        case (int)ActorTypes.Water:
                        default:
                            ignore = true;
                            break;
                    }
                    break;

                case (int)ActorTypes.Ground:
                    if (p2.PhysicsActorType == (int)ActorTypes.Prim)
                    {
                        p2.CollidingGround = true;
                        p2.getContactData(ref contactdata2);
                        bounce = contactdata2.bounce * TerrainBounce;
                        mu = (float)Math.Sqrt(contactdata2.mu * TerrainFriction);

                        cfm = p2.Mass;
                        dscale = 10 / cfm;
                        dscale = (float)Math.Sqrt(dscale);

                        if (dscale > 1.0f)
                            dscale = 1.0f;

                        erpscale = cfm * 0.01f;
                        cfm = 0.0001f / cfm;
                        if (cfm > 0.01f)
                            cfm = 0.01f;
                        else if (cfm < 0.00001f)
                            cfm = 0.00001f;

                        if (curContact.side1 > 0) // should be 2 ?
                            IgnoreNegSides = true;

                        if (Math.Abs(p2.Velocity.X) > 0.1f || Math.Abs(p2.Velocity.Y) > 0.1f)
                            mu *= frictionMovementMult;
                    }
                    else
                        ignore = true;
                    break;

                case (int)ActorTypes.Water:
                default:
                    break;
            }

            if (ignore)
                return;


            d.ContactGeom maxContact = curContact;
//            if (IgnoreNegSides && curContact.side1 < 0)
//                maxContact.depth = float.MinValue;

            d.ContactGeom minContact = curContact;
//            if (IgnoreNegSides && curContact.side1 < 0)
//                minContact.depth = float.MaxValue;

            IntPtr Joint;
            bool FeetCollision = false;
            int ncontacts = 0;


                int i = 0;

                while (true)
                {
                    if (m_global_contactcount >= maxContactsbeforedeath)
                        break;

//                    if (!(IgnoreNegSides && curContact.side1 < 0))
                    {
                        bool noskip = true;
                        if (dop1ava)
                        {
                            if (!(((OdeCharacter)p1).Collide(g1,false, ref curContact, ref FeetCollision)))

                                noskip = false;
                        }
                        else if (dop2ava)
                        {
                            if (!(((OdeCharacter)p2).Collide(g2,true, ref curContact, ref FeetCollision)))
                                noskip = false;
                        }

                        if (noskip)
                        {
                            m_global_contactcount++;
                            ncontacts++;

                            Joint = CreateContacJoint(ref curContact, mu, bounce, cfm, erpscale, dscale);
                            d.JointAttach(Joint, b1, b2);

                            if (curContact.depth > maxContact.depth)
                                maxContact = curContact;

                            if (curContact.depth < minContact.depth)
                                minContact = curContact;
                        }
                    }

                    if (++i >= count)
                        break;

                    if (!GetCurContactGeom(i, ref curContact))
                        break;
                }

            if (ncontacts > 0)
            {
                ContactPoint maxDepthContact = new ContactPoint(
                            new Vector3(maxContact.pos.X, maxContact.pos.Y, maxContact.pos.Z),
                            new Vector3(minContact.normal.X, minContact.normal.Y, minContact.normal.Z),
                            maxContact.depth, FeetCollision
                            );
                collision_accounting_events(p1, p2, maxDepthContact);
            }
        }

        private void collision_accounting_events(PhysicsActor p1, PhysicsActor p2, ContactPoint contact)
        {
            uint obj2LocalID = 0;

            bool p1events = p1.SubscribedEvents();
            bool p2events = p2.SubscribedEvents();

            if (p1.IsVolumeDtc)
                p2events = false;
            if (p2.IsVolumeDtc)
                p1events = false;

            if (!p2events && !p1events)
                return;

            Vector3 vel = Vector3.Zero;
            if (p2 != null && p2.IsPhysical)
                vel = p2.Velocity;

            if (p1 != null && p1.IsPhysical)
                vel -= p1.Velocity;

            contact.RelativeSpeed = Vector3.Dot(vel, contact.SurfaceNormal);

            switch ((ActorTypes)p1.PhysicsActorType)
            {
                case ActorTypes.Agent:
                case ActorTypes.Prim:
                    {
                        switch ((ActorTypes)p2.PhysicsActorType)
                        {
                            case ActorTypes.Agent:
                            case ActorTypes.Prim:
                                if (p2events)
                                {
                                    AddCollisionEventReporting(p2);
                                    p2.AddCollisionEvent(p1.ParentActor.LocalID, contact);
                                }
                                obj2LocalID = p2.ParentActor.LocalID;
                                break;

                            case ActorTypes.Ground:
                            case ActorTypes.Unknown:
                            default:
                                obj2LocalID = 0;
                                break;
                        }
                        if (p1events)
                        {
                            contact.SurfaceNormal = -contact.SurfaceNormal;
                            AddCollisionEventReporting(p1);
                            p1.AddCollisionEvent(obj2LocalID, contact);
                        }
                        break;
                    }
                case ActorTypes.Ground:
                case ActorTypes.Unknown:
                default:
                    {
                        if (p2events && !p2.IsVolumeDtc)
                        {
                            AddCollisionEventReporting(p2);
                            p2.AddCollisionEvent(0, contact);
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// This is our collision testing routine in ODE
        /// </summary>
        /// <param name="timeStep"></param>
        private void collision_optimized()
        {
            lock (_characters)
                {
                try
                {
                    foreach (OdeCharacter chr in _characters)
                    {
                        if (chr == null || chr.Body == IntPtr.Zero)
                            continue;

                        chr.IsColliding = false;
                        //                    chr.CollidingGround = false; not done here
                        chr.CollidingObj = false;
                        // do colisions with static space
                        d.SpaceCollide2(chr.collider, StaticSpace, IntPtr.Zero, nearCallback);

                        // chars with chars
                        d.SpaceCollide(CharsSpace, IntPtr.Zero, nearCallback);
                        // no coll with gnd
                    }
                }
                catch (AccessViolationException)
                {
                    m_log.Warn("[PHYSICS]: Unable to collide Character to static space");
                }

            }

            lock (_activeprims)
            {
                foreach (OdePrim aprim in _activeprims)
                {
                    aprim.CollisionScore = 0;
                    aprim.IsColliding = false;
                }
            }

            // collide active prims with static enviroment
            lock (_activegroups)
            {
                try
                {
                    foreach (OdePrim prm in _activegroups)
                    {
                        if (!prm.m_outbounds)
                        {
                            if (d.BodyIsEnabled(prm.Body))
                            {
                                d.SpaceCollide2(StaticSpace, prm.collide_geom, IntPtr.Zero, nearCallback);
                                d.SpaceCollide2(GroundSpace, prm.collide_geom, IntPtr.Zero, nearCallback);
                            }
                        }
                    }
                }
                catch (AccessViolationException)
                {
                    m_log.Warn("[PHYSICS]: Unable to collide Active prim to static space");
                }
            }
            // colide active amoung them
            try
            {
                d.SpaceCollide(ActiveSpace, IntPtr.Zero, nearCallback);
            }
            catch (AccessViolationException)
            {
                m_log.Warn("[PHYSICS]: Unable to collide Active with Characters space");
            }
            // and with chars
            try
            {
                d.SpaceCollide2(CharsSpace,ActiveSpace, IntPtr.Zero, nearCallback);
            }
            catch (AccessViolationException)
            {
                m_log.Warn("[PHYSICS]: Unable to collide in Active space");
            }
            //            _perloopContact.Clear();
        }

        #endregion
        /// <summary>
        /// Add actor to the list that should receive collision events in the simulate loop.
        /// </summary>
        /// <param name="obj"></param>
        public void AddCollisionEventReporting(PhysicsActor obj)
        {
            if (!_collisionEventPrim.Contains(obj))
                _collisionEventPrim.Add(obj);
        }

        /// <summary>
        /// Remove actor from the list that should receive collision events in the simulate loop.
        /// </summary>
        /// <param name="obj"></param>
        public void RemoveCollisionEventReporting(PhysicsActor obj)
        {
            if (_collisionEventPrim.Contains(obj) && !_collisionEventPrimRemove.Contains(obj))
                _collisionEventPrimRemove.Add(obj);
        }

        public override float TimeDilation
        {
            get { return m_timeDilation; }
        }

        public override bool SupportsNINJAJoints
        {
            get { return false; }
        }

        #region Add/Remove Entities

        public override PhysicsActor AddAvatar(uint localID, string avName, Vector3 position, Vector3 size, float feetOffset, bool isFlying)
        {
            Vector3 pos;
            pos.X = position.X;
            pos.Y = position.Y;
            pos.Z = position.Z;
            OdeCharacter newAv = new OdeCharacter(localID,avName, this, pos, size, feetOffset, avDensity, avMovementDivisorWalk, avMovementDivisorRun);
            newAv.Flying = isFlying;
            newAv.MinimumGroundFlightOffset = minimumGroundFlightOffset;
            
            return newAv;
        }

        public void AddCharacter(OdeCharacter chr)
        {
            lock (_characters)
            {
                if (!_characters.Contains(chr))
                {
                    _characters.Add(chr);
                    if (chr.bad)
                        m_log.DebugFormat("[PHYSICS] Added BAD actor {0} to characters list", chr.m_uuid);
                }
            }
        }

        public void RemoveCharacter(OdeCharacter chr)
        {
            lock (_characters)
            {
                if (_characters.Contains(chr))
                {
                    _characters.Remove(chr);
                }
            }
        }

        public void BadCharacter(OdeCharacter chr)
        {
            lock (_badCharacter)
            {
                if (!_badCharacter.Contains(chr))
                    _badCharacter.Add(chr);
            }
        }

        public override void RemoveAvatar(PhysicsActor actor)
        {
            //m_log.Debug("[PHYSICS]:ODELOCK");
            ((OdeCharacter) actor).Destroy();
        }


        public void addActivePrim(OdePrim activatePrim)
        {
            // adds active prim..   
            lock (_activeprims)
            {
                if (!_activeprims.Contains(activatePrim))
                    _activeprims.Add(activatePrim);
            }
        }

        public void addActiveGroups(OdePrim activatePrim)
        {
            lock (_activegroups)
            {
                if (!_activegroups.Contains(activatePrim))
                    _activegroups.Add(activatePrim);
            }
        }

        private PhysicsActor AddPrim(String name, Vector3 position, Vector3 size, Quaternion rotation,
                                     PrimitiveBaseShape pbs, bool isphysical, bool isPhantom, byte shapeType, uint localID)
        {
            OdePrim newPrim;
            lock (OdeLock)
            {
                newPrim = new OdePrim(name, this, position, size, rotation, pbs, isphysical, isPhantom, shapeType, localID);
                lock (_prims)
                    _prims.Add(newPrim);
            }
            return newPrim;
        }

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, Vector3 position,
                                                  Vector3 size, Quaternion rotation, bool isPhysical, bool isPhantom, uint localid)
        {
            return AddPrim(primName, position, size, rotation, pbs, isPhysical, isPhantom, 0 , localid);
        }


        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, Vector3 position,
                                                  Vector3 size, Quaternion rotation, bool isPhysical, uint localid)
        {
            return AddPrim(primName, position, size, rotation, pbs, isPhysical,false, 0, localid);
        }

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, Vector3 position,
                                                  Vector3 size, Quaternion rotation, bool isPhysical, bool isPhantom, byte shapeType, uint localid)
        {

            return AddPrim(primName, position, size, rotation, pbs, isPhysical,isPhantom, shapeType, localid);
        }

        public void remActivePrim(OdePrim deactivatePrim)
        {
            lock (_activeprims)
            {
                _activeprims.Remove(deactivatePrim);
            }
        }
        public void remActiveGroup(OdePrim deactivatePrim)
        {
            lock (_activegroups)
            {
                _activegroups.Remove(deactivatePrim);
            }
        }

        public override void RemovePrim(PhysicsActor prim)
        {
            // As with all ODE physics operations, we don't remove the prim immediately but signal that it should be
            // removed in the next physics simulate pass.
            if (prim is OdePrim)
            {
//                lock (OdeLock)
                {
                    
                    OdePrim p = (OdePrim)prim;
                    p.setPrimForRemoval();
                }
            }
        }
        /// <summary>
        /// This is called from within simulate but outside the locked portion
        /// We need to do our own locking here
        /// (Note: As of 20110801 this no longer appears to be true - this is being called within lock (odeLock) in
        /// Simulate() -- justincc).
        ///
        /// Essentially, we need to remove the prim from our space segment, whatever segment it's in.
        ///
        /// If there are no more prim in the segment, we need to empty (spacedestroy)the segment and reclaim memory
        /// that the space was using.
        /// </summary>
        /// <param name="prim"></param>
        public void RemovePrimThreadLocked(OdePrim prim)
        {
            //Console.WriteLine("RemovePrimThreadLocked " +  prim.m_primName);
            lock (prim)
            {
//                RemoveCollisionEventReporting(prim);
                lock (_prims)
                    _prims.Remove(prim);
            }

        }

        public bool havePrim(OdePrim prm)
        {
            lock (_prims)
                return _prims.Contains(prm);
        }

        public bool haveActor(PhysicsActor actor)
        {
            if (actor is OdePrim)
            {
                lock (_prims)
                    return _prims.Contains((OdePrim)actor);
            }
            else if (actor is OdeCharacter)
            {
                lock (_characters)
                    return _characters.Contains((OdeCharacter)actor);
            }
            return false;
        }

        #endregion

        #region Space Separation Calculation

        /// <summary>
        /// Called when a static prim moves or becomes static
        /// Places the prim in a space one the static sub-spaces grid
        /// </summary>
        /// <param name="geom">the pointer to the geom that moved</param>
        /// <param name="pos">the position that the geom moved to</param>
        /// <param name="currentspace">a pointer to the space it was in before it was moved.</param>
        /// <returns>a pointer to the new space it's in</returns>
        public IntPtr MoveGeomToStaticSpace(IntPtr geom, Vector3 pos, IntPtr currentspace)
        {
            // moves a prim into another static sub-space or from another space into a static sub-space

            // Called ODEPrim so
            // it's already in locked space.

            if (geom == IntPtr.Zero) // shouldn't happen
                return IntPtr.Zero;

            // get the static sub-space for current position
            IntPtr newspace = calculateSpaceForGeom(pos);

            if (newspace == currentspace) // if we are there all done
                return newspace;

            // else remove it from its current space
            if (currentspace != IntPtr.Zero && d.SpaceQuery(currentspace, geom))
            {
                if (d.GeomIsSpace(currentspace))
                {
                    waitForSpaceUnlock(currentspace);
                    d.SpaceRemove(currentspace, geom);

                    if (d.SpaceGetSublevel(currentspace) > 2 && d.SpaceGetNumGeoms(currentspace) == 0)
                    {
                        d.SpaceDestroy(currentspace);
                    }
                }
                else
                {
                    m_log.Info("[Physics]: Invalid or empty Space passed to 'MoveGeomToStaticSpace':" + currentspace +
                                   " Geom:" + geom);
                }
            }
            else // odd currentspace is null or doesn't contain the geom? lets try the geom ideia of current space
            {
                currentspace = d.GeomGetSpace(geom);
                if (currentspace != IntPtr.Zero)
                {
                    if (d.GeomIsSpace(currentspace))
                    {
                        waitForSpaceUnlock(currentspace);
                        d.SpaceRemove(currentspace, geom);

                        if (d.SpaceGetSublevel(currentspace) > 2 && d.SpaceGetNumGeoms(currentspace) == 0)
                        {
                            d.SpaceDestroy(currentspace);
                        }

                    }
                }
            }

            // put the geom in the newspace
            waitForSpaceUnlock(newspace);
            d.SpaceAdd(newspace, geom);

            // let caller know this newspace
            return newspace;
        }

        /// <summary>
        /// Calculates the space the prim should be in by its position
        /// </summary>
        /// <param name="pos"></param>
        /// <returns>a pointer to the space. This could be a new space or reused space.</returns>
        public IntPtr calculateSpaceForGeom(Vector3 pos)
        {
            int x, y;

            if (pos.X < 0)
                return staticPrimspaceOffRegion[0];

            if (pos.Y < 0)
                return staticPrimspaceOffRegion[2];

            x = (int)(pos.X * spacesPerMeter);
            if (x > spaceGridMaxX)
                return staticPrimspaceOffRegion[1];
            
            y = (int)(pos.Y * spacesPerMeter);
            if (y > spaceGridMaxY)
                return staticPrimspaceOffRegion[3];

            return staticPrimspace[x, y];
        }
 
        #endregion


        /// <summary>
        /// Called to queue a change to a actor
        /// to use in place of old taint mechanism so changes do have a time sequence
        /// </summary>

        public void AddChange(PhysicsActor actor, changes what, Object arg)
        {
            ODEchangeitem item = new ODEchangeitem();
            item.actor = actor;
            item.what = what;
            item.arg = arg;
            ChangesQueue.Enqueue(item);
        }

        /// <summary>
        /// Called after our prim properties are set Scale, position etc.
        /// We use this event queue like method to keep changes to the physical scene occuring in the threadlocked mutex
        /// This assures us that we have no race conditions
        /// </summary>
        /// <param name="prim"></param>
        public override void AddPhysicsActorTaint(PhysicsActor prim)
        {
        }

        // does all pending changes generated during region load process
        public override void PrepareSimulation()
        {
            lock (OdeLock)
            {
                if (world == IntPtr.Zero)
                {
                    ChangesQueue.Clear();
                    return;
                }

                ODEchangeitem item;

                int donechanges = 0;
                if (ChangesQueue.Count > 0)
                {
                    m_log.InfoFormat("[ODE] start processing pending actor operations");
                    int tstart = Util.EnvironmentTickCount();

                    while (ChangesQueue.Dequeue(out item))
                    {
                        if (item.actor != null)
                        {
                            try
                            {
                                if (item.actor is OdeCharacter)
                                    ((OdeCharacter)item.actor).DoAChange(item.what, item.arg);
                                else if (((OdePrim)item.actor).DoAChange(item.what, item.arg))
                                    RemovePrimThreadLocked((OdePrim)item.actor);
                            }
                            catch
                            {
                                m_log.WarnFormat("[PHYSICS]: Operation failed for a actor {0} {1}",
                                    item.actor.Name, item.what.ToString());
                            }
                        }
                        donechanges++;
                    }
                    int time = Util.EnvironmentTickCountSubtract(tstart);
                    m_log.InfoFormat("[ODE] finished {0} operations in {1}ms", donechanges, time);
                }
            }
        }

        /// <summary>
        /// This is our main simulate loop
        /// It's thread locked by a Mutex in the scene.
        /// It holds Collisions, it instructs ODE to step through the physical reactions
        /// It moves the objects around in memory
        /// It calls the methods that report back to the object owners.. (scenepresence, SceneObjectGroup)
        /// </summary>
        /// <param name="timeStep"></param>
        /// <returns></returns>
        public override float Simulate(float timeStep)
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan timedif = now - m_lastframe;
            timeStep = (float)timedif.TotalSeconds;
            m_lastframe = now;
            
            // acumulate time so we can reduce error
            step_time += timeStep;

            if (step_time < HalfOdeStep)
                return 0;

            if (framecount < 0)
                framecount = 0;


            framecount++;

            int curphysiteractions;

            // if in trouble reduce step resolution
            if (step_time >= m_SkipFramesAtms)
                curphysiteractions = m_physicsiterations / 2;
            else
                curphysiteractions = m_physicsiterations;

//            checkThread();
            int nodeframes = 0;

            lock (SimulationLock)
                lock(OdeLock)
            {
                if (world == IntPtr.Zero)
                {
                    ChangesQueue.Clear();
                    return 0;
                }

                ODEchangeitem item;


                
                d.WorldSetQuickStepNumIterations(world, curphysiteractions);

                int loopstartMS = Util.EnvironmentTickCount();
                int looptimeMS = 0;
                

                while (step_time > HalfOdeStep)
                {
                    try
                    {
                        // clear pointer/counter to contacts to pass into joints
                        m_global_contactcount = 0;

                        if (ChangesQueue.Count > 0)
                        {
                            int changestartMS = Util.EnvironmentTickCount();
                            int ttmp;
                            while (ChangesQueue.Dequeue(out item))
                            {
                                if (item.actor != null)
                                {
                                    try
                                    {
                                        if (item.actor is OdeCharacter)
                                            ((OdeCharacter)item.actor).DoAChange(item.what, item.arg);
                                        else if (((OdePrim)item.actor).DoAChange(item.what, item.arg))
                                            RemovePrimThreadLocked((OdePrim)item.actor);
                                    }
                                    catch
                                    {
                                        m_log.WarnFormat("[PHYSICS]: doChange failed for a actor {0} {1}",
                                            item.actor.Name, item.what.ToString());
                                    }
                                }
                                ttmp = Util.EnvironmentTickCountSubtract(changestartMS);
                                if (ttmp > 20)
                                    break;
                            }
                        }

                        // Move characters
                        lock (_characters)
                        {
                            List<OdeCharacter> defects = new List<OdeCharacter>();
                            foreach (OdeCharacter actor in _characters)
                            {
                                if (actor != null)
                                    actor.Move(defects);
                            }
                            if (defects.Count != 0)
                            {
                                foreach (OdeCharacter defect in defects)
                                {
                                    RemoveCharacter(defect);
                                }
                                defects.Clear();
                            }
                        }

                        // Move other active objects
                        lock (_activegroups)
                        {
                            foreach (OdePrim aprim in _activegroups)
                            {
                                aprim.Move();
                            }
                        }

                        //if ((framecount % m_randomizeWater) == 0)
                        // randomizeWater(waterlevel);

                        m_rayCastManager.ProcessQueuedRequests();

                        collision_optimized();

                        foreach (PhysicsActor obj in _collisionEventPrim)
                        {
                            if (obj == null)
                                continue;

                            switch ((ActorTypes)obj.PhysicsActorType)
                            {
                                case ActorTypes.Agent:
                                    OdeCharacter cobj = (OdeCharacter)obj;
                                    cobj.AddCollisionFrameTime((int)(odetimestepMS));
                                    cobj.SendCollisions();
                                    break;

                                case ActorTypes.Prim:
                                    OdePrim pobj = (OdePrim)obj;
                                    if (pobj.Body == IntPtr.Zero || (d.BodyIsEnabled(pobj.Body) && !pobj.m_outbounds))
                                    if (!pobj.m_outbounds)
                                    {
                                        pobj.AddCollisionFrameTime((int)(odetimestepMS));
                                        pobj.SendCollisions();
                                    }
                                    break;
                            }
                        }

                        foreach (PhysicsActor obj in _collisionEventPrimRemove)
                            _collisionEventPrim.Remove(obj);

                        _collisionEventPrimRemove.Clear();

                        // do a ode simulation step
                        d.WorldQuickStep(world, ODE_STEPSIZE);
                        d.JointGroupEmpty(contactgroup);

                        // update managed ideia of physical data and do updates to core
        /*
                        lock (_characters)
                        {
                            foreach (OdeCharacter actor in _characters)
                            {
                                if (actor != null)
                                {
                                    if (actor.bad)
                                        m_log.WarnFormat("[PHYSICS]: BAD Actor {0} in _characters list was not removed?", actor.m_uuid);

                                    actor.UpdatePositionAndVelocity();
                                }
                            }
                        }
        */

                        lock (_activegroups)
                        {
                            {
                                foreach (OdePrim actor in _activegroups)
                                {
                                    if (actor.IsPhysical)
                                    {
                                        actor.UpdatePositionAndVelocity();
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[PHYSICS]: {0}, {1}, {2}", e.Message, e.TargetSite, e);
//                        ode.dunlock(world);
                    }

                    step_time -= ODE_STEPSIZE;
                    nodeframes++;

                    looptimeMS = Util.EnvironmentTickCountSubtract(loopstartMS);
                    if (looptimeMS > 100)
                        break;
                }

                lock (_badCharacter)
                {
                    if (_badCharacter.Count > 0)
                    {
                        foreach (OdeCharacter chr in _badCharacter)
                        {
                            RemoveCharacter(chr);
                        }

                        _badCharacter.Clear();
                    }
                }

                timedif = now - m_lastMeshExpire;

                if (timedif.Seconds > 10)
                {
                    mesher.ExpireReleaseMeshs();
                    m_lastMeshExpire = now;
                }

// information block running in debug only
/*
                int ntopactivegeoms = d.SpaceGetNumGeoms(ActiveSpace);
                int ntopstaticgeoms = d.SpaceGetNumGeoms(StaticSpace);
                int ngroundgeoms = d.SpaceGetNumGeoms(GroundSpace);

                int nactivegeoms = 0;
                int nactivespaces = 0;

                int nstaticgeoms = 0;
                int nstaticspaces = 0;
                IntPtr sp;

                for (int i = 0; i < ntopactivegeoms; i++)
                {
                    sp = d.SpaceGetGeom(ActiveSpace, i);
                    if (d.GeomIsSpace(sp))
                    {
                        nactivespaces++;
                        nactivegeoms += d.SpaceGetNumGeoms(sp);
                    }
                    else
                        nactivegeoms++;
                }

                for (int i = 0; i < ntopstaticgeoms; i++)
                {
                    sp = d.SpaceGetGeom(StaticSpace, i);
                    if (d.GeomIsSpace(sp))
                    {
                        nstaticspaces++;
                        nstaticgeoms += d.SpaceGetNumGeoms(sp);
                    }
                    else
                        nstaticgeoms++;
                }

                int ntopgeoms = d.SpaceGetNumGeoms(TopSpace);

                int totgeoms = nstaticgeoms + nactivegeoms + ngroundgeoms + 1; // one ray
                int nbodies = d.NTotalBodies;
                int ngeoms = d.NTotalGeoms;
*/
                // Finished with all sim stepping. If requested, dump world state to file for debugging.
                // TODO: This call to the export function is already inside lock (OdeLock) - but is an extra lock needed?
                // TODO: This overwrites all dump files in-place. Should this be a growing logfile, or separate snapshots?
                if (physics_logging && (physics_logging_interval > 0) && (framecount % physics_logging_interval == 0))
                {
                    string fname = "state-" + world.ToString() + ".DIF"; // give each physics world a separate filename
                    string prefix = "world" + world.ToString(); // prefix for variable names in exported .DIF file

                    if (physics_logging_append_existing_logfile)
                    {
                        string header = "-------------- START OF PHYSICS FRAME " + framecount.ToString() + " --------------";
                        TextWriter fwriter = File.AppendText(fname);
                        fwriter.WriteLine(header);
                        fwriter.Close();
                    }

                    d.WorldExportDIF(world, fname, physics_logging_append_existing_logfile, prefix);
                }
                
                // think time dilation as to do with dinamic step size that we dont' have
                // even so tell something to world
                if (looptimeMS < 100) // we did the requested loops
                    m_timeDilation = 1.0f;
                else if (step_time > 0)
                {
                    m_timeDilation = timeStep / step_time;
                    if (m_timeDilation > 1)
                        m_timeDilation = 1;
                    if (step_time > m_SkipFramesAtms)
                        step_time = 0;
                    m_lastframe = DateTime.UtcNow; // skip also the time lost
                }
            }

//            return nodeframes * ODE_STEPSIZE; // return real simulated time
            return 1000 * nodeframes; // return steps for now * 1000 to keep core happy
        }

        /// <summary>
        public override void GetResults()
        {
        }

        public override bool IsThreaded
        {
            // for now we won't be multithreaded
            get { return (false); }
        }

        public float GetTerrainHeightAtXY(float x, float y)
        {


            int offsetX = ((int)(x / (int)Constants.RegionSize)) * (int)Constants.RegionSize;
            int offsetY = ((int)(y / (int)Constants.RegionSize)) * (int)Constants.RegionSize;


            IntPtr heightFieldGeom = IntPtr.Zero;

            // get region map
            if (!RegionTerrain.TryGetValue(new Vector3(offsetX, offsetY, 0), out heightFieldGeom))
                return 0f;

            if (heightFieldGeom == IntPtr.Zero)
                return 0f;

            if (!TerrainHeightFieldHeights.ContainsKey(heightFieldGeom))
                return 0f;

            // TerrainHeightField for ODE as offset 1m
            x += 1f - offsetX;
            y += 1f - offsetY;

            // make position fit into array
            if (x < 0)
                x = 0;
            if (y < 0)
                y = 0;

            // integer indexs
            int ix;
            int iy;
            //  interpolators offset
            float dx;
            float dy;

            int regsize = (int)Constants.RegionSize + 3; // map size see setterrain number of samples

            if (OdeUbitLib)
            {
                if (x < regsize - 1)
                {
                    ix = (int)x;
                    dx = x - (float)ix;
                }
                else // out world use external height
                {
                    ix = regsize - 2;
                    dx = 0;
                }
                if (y < regsize - 1)
                {
                    iy = (int)y;
                    dy = y - (float)iy;
                }
                else
                {
                    iy = regsize - 2;
                    dy = 0;
                }
            }

            else
            {
                // we  still have square fixed size regions
                // also flip x and y because of how map is done for ODE fliped axis
                // so ix,iy,dx and dy are inter exchanged
                if (x < regsize - 1)
                {
                    iy = (int)x;
                    dy = x - (float)iy;
                }
                else // out world use external height
                {
                    iy = regsize - 2;
                    dy = 0;
                }
                if (y < regsize - 1)
                {
                    ix = (int)y;
                    dx = y - (float)ix;
                }
                else
                {
                    ix = regsize - 2;
                    dx = 0;
                }
            }

            float h0;
            float h1;
            float h2;

            iy *= regsize;
            iy += ix; // all indexes have iy + ix

            float[] heights = TerrainHeightFieldHeights[heightFieldGeom];
            /*
                        if ((dx + dy) <= 1.0f)
                        {
                            h0 = ((float)heights[iy]); // 0,0 vertice
                            h1 = (((float)heights[iy + 1]) - h0) * dx; // 1,0 vertice minus 0,0
                            h2 = (((float)heights[iy + regsize]) - h0) * dy; // 0,1 vertice minus 0,0
                        }
                        else
                        {
                            h0 = ((float)heights[iy + regsize + 1]); // 1,1 vertice
                            h1 = (((float)heights[iy + 1]) - h0) * (1 - dy); // 1,1 vertice minus 1,0
                            h2 = (((float)heights[iy + regsize]) - h0) * (1 - dx); // 1,1 vertice minus 0,1
                        }
            */
            h0 = ((float)heights[iy]); // 0,0 vertice

            if ((dy > dx))
            {
                iy += regsize;
                h2 = (float)heights[iy]; // 0,1 vertice
                h1 = (h2 - h0) * dy; // 0,1 vertice minus 0,0
                h2 = ((float)heights[iy + 1] - h2) * dx; // 1,1 vertice minus 0,1
            }
            else
            {
                iy++;
                h2 = (float)heights[iy]; // vertice 1,0
                h1 = (h2 - h0) * dx; // 1,0 vertice minus 0,0
                h2 = (((float)heights[iy + regsize]) - h2) * dy; // 1,1 vertice minus 1,0
            }

            return h0 + h1 + h2;
        }


        public override void SetTerrain(float[] heightMap)
        {
            if (m_worldOffset != Vector3.Zero && m_parentScene != null)
            {
                if (m_parentScene is OdeScene)
                {
                    ((OdeScene)m_parentScene).SetTerrain(heightMap, m_worldOffset);
                }
            }
            else
            {
                SetTerrain(heightMap, m_worldOffset);
            }
        }

        public override void CombineTerrain(float[] heightMap, Vector3 pOffset)
        {
            SetTerrain(heightMap, pOffset);
        }

        public void SetTerrain(float[] heightMap, Vector3 pOffset)
        {
            if (OdeUbitLib)
                UbitSetTerrain(heightMap, pOffset);
            else
                OriSetTerrain(heightMap, pOffset);
        }

        public void OriSetTerrain(float[] heightMap, Vector3 pOffset)
        {
            // assumes 1m size grid and constante size square regions
            // needs to know about sims around in future

            float[] _heightmap;

            uint heightmapWidth = Constants.RegionSize + 2;
            uint heightmapHeight = Constants.RegionSize + 2;

            uint heightmapWidthSamples = heightmapWidth + 1;
            uint heightmapHeightSamples = heightmapHeight + 1;

            _heightmap = new float[heightmapWidthSamples * heightmapHeightSamples];

            const float scale = 1.0f;
            const float offset = 0.0f;
            const float thickness = 10f;
            const int wrap = 0;

            uint regionsize = Constants.RegionSize;
 
            float hfmin = float.MaxValue;
            float hfmax = float.MinValue;
            float val;
            uint xx;
            uint yy;

            uint maxXXYY = regionsize - 1;
            // flipping map adding one margin all around so things don't fall in edges

            uint xt = 0;
            xx = 0;

            for (uint x = 0; x < heightmapWidthSamples; x++)
            {
                if (x > 1 && xx < maxXXYY)
                    xx++;
                yy = 0;
                for (uint y = 0; y < heightmapHeightSamples; y++)
                {
                    if (y > 1 && y < maxXXYY)
                        yy += regionsize;

                    val = heightMap[yy + xx];
                    if (val < 0.0f)
                        val = 0.0f; // no neg terrain as in chode
                    _heightmap[xt + y] = val;

                    if (hfmin > val)
                        hfmin = val;
                    if (hfmax < val)
                        hfmax = val;
                }
                xt += heightmapHeightSamples;
            }
            lock (OdeLock)
            {
                IntPtr GroundGeom = IntPtr.Zero;
                if (RegionTerrain.TryGetValue(pOffset, out GroundGeom))
                {
                    RegionTerrain.Remove(pOffset);
                    if (GroundGeom != IntPtr.Zero)
                    {
                        actor_name_map.Remove(GroundGeom);
                        d.GeomDestroy(GroundGeom);

                        if (TerrainHeightFieldHeights.ContainsKey(GroundGeom))
                            {
                            TerrainHeightFieldHeightsHandlers[GroundGeom].Free();
                            TerrainHeightFieldHeightsHandlers.Remove(GroundGeom);
                            TerrainHeightFieldHeights.Remove(GroundGeom);
                            }
                    }
                }
                IntPtr HeightmapData = d.GeomHeightfieldDataCreate();

                GCHandle _heightmaphandler = GCHandle.Alloc(_heightmap, GCHandleType.Pinned);

                d.GeomHeightfieldDataBuildSingle(HeightmapData, _heightmaphandler.AddrOfPinnedObject(), 0, heightmapWidth , heightmapHeight,
                                                 (int)heightmapWidthSamples, (int)heightmapHeightSamples, scale,
                                                 offset, thickness, wrap);

                d.GeomHeightfieldDataSetBounds(HeightmapData, hfmin - 1, hfmax + 1);

                GroundGeom = d.CreateHeightfield(GroundSpace, HeightmapData, 1);

                if (GroundGeom != IntPtr.Zero)
                {
                    d.GeomSetCategoryBits(GroundGeom, (uint)(CollisionCategories.Land));
                    d.GeomSetCollideBits(GroundGeom, 0);

                    PhysicsActor pa = new NullPhysicsActor();
                    pa.Name = "Terrain";
                    pa.PhysicsActorType = (int)ActorTypes.Ground;
                    actor_name_map[GroundGeom] = pa;

//                    geom_name_map[GroundGeom] = "Terrain";

                    d.Matrix3 R = new d.Matrix3();

                    Quaternion q1 = Quaternion.CreateFromAxisAngle(new Vector3(1, 0, 0), 1.5707f);
                    Quaternion q2 = Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), 1.5707f);


                    q1 = q1 * q2;

                    Vector3 v3;
                    float angle;
                    q1.GetAxisAngle(out v3, out angle);

                    d.RFromAxisAndAngle(out R, v3.X, v3.Y, v3.Z, angle);
                    d.GeomSetRotation(GroundGeom, ref R);
                    d.GeomSetPosition(GroundGeom, pOffset.X + (float)Constants.RegionSize * 0.5f, pOffset.Y + (float)Constants.RegionSize * 0.5f, 0);
                    RegionTerrain.Add(pOffset, GroundGeom);
                    TerrainHeightFieldHeights.Add(GroundGeom, _heightmap);
                    TerrainHeightFieldHeightsHandlers.Add(GroundGeom, _heightmaphandler);
                }
            }
        }

        public void UbitSetTerrain(float[] heightMap, Vector3 pOffset)
        {
            // assumes 1m size grid and constante size square regions
            // needs to know about sims around in future

            float[] _heightmap;

            uint heightmapWidth = Constants.RegionSize + 2;
            uint heightmapHeight = Constants.RegionSize + 2;

            uint heightmapWidthSamples = heightmapWidth + 1;
            uint heightmapHeightSamples = heightmapHeight + 1;

            _heightmap = new float[heightmapWidthSamples * heightmapHeightSamples];


            uint regionsize = Constants.RegionSize;

            float hfmin = float.MaxValue;
//            float hfmax = float.MinValue;
            float val;


            uint maxXXYY = regionsize - 1;
            // adding one margin all around so things don't fall in edges

            uint xx;
            uint yy = 0;
            uint yt = 0;

            for (uint y = 0; y < heightmapHeightSamples; y++)
            {
                if (y > 1 && y < maxXXYY)
                    yy += regionsize;
                xx = 0;
                for (uint x = 0; x < heightmapWidthSamples; x++)
                {
                    if (x > 1 && x < maxXXYY)
                        xx++;

                    val = heightMap[yy + xx];
                    if (val < 0.0f)
                        val = 0.0f; // no neg terrain as in chode
                    _heightmap[yt + x] = val;

                    if (hfmin > val)
                        hfmin = val;
//                    if (hfmax < val)
//                        hfmax = val;
                }
                yt += heightmapWidthSamples;
            }
            lock (OdeLock)
            {
                IntPtr GroundGeom = IntPtr.Zero;
                if (RegionTerrain.TryGetValue(pOffset, out GroundGeom))
                {
                    RegionTerrain.Remove(pOffset);
                    if (GroundGeom != IntPtr.Zero)
                    {
                        actor_name_map.Remove(GroundGeom);
                        d.GeomDestroy(GroundGeom);

                        if (TerrainHeightFieldHeights.ContainsKey(GroundGeom))
                        {
                            if (TerrainHeightFieldHeightsHandlers[GroundGeom].IsAllocated)
                                TerrainHeightFieldHeightsHandlers[GroundGeom].Free();
                            TerrainHeightFieldHeightsHandlers.Remove(GroundGeom);
                            TerrainHeightFieldHeights.Remove(GroundGeom);
                        }
                    }
                }
                IntPtr HeightmapData = d.GeomUbitTerrainDataCreate();

                const int wrap = 0;
                float thickness = hfmin;
                if (thickness < 0)
                    thickness = 1;

                GCHandle _heightmaphandler = GCHandle.Alloc(_heightmap, GCHandleType.Pinned);

                d.GeomUbitTerrainDataBuild(HeightmapData, _heightmaphandler.AddrOfPinnedObject(), 0, 1.0f,
                                                 (int)heightmapWidthSamples, (int)heightmapHeightSamples,
                                                 thickness, wrap);

//                d.GeomUbitTerrainDataSetBounds(HeightmapData, hfmin - 1, hfmax + 1);
                GroundGeom = d.CreateUbitTerrain(GroundSpace, HeightmapData, 1);
                if (GroundGeom != IntPtr.Zero)
                {
                    d.GeomSetCategoryBits(GroundGeom, (uint)(CollisionCategories.Land));
                    d.GeomSetCollideBits(GroundGeom, 0);


                    PhysicsActor pa = new NullPhysicsActor();
                    pa.Name = "Terrain";
                    pa.PhysicsActorType = (int)ActorTypes.Ground;
                    actor_name_map[GroundGeom] = pa;

//                    geom_name_map[GroundGeom] = "Terrain";

                    d.GeomSetPosition(GroundGeom, pOffset.X + (float)Constants.RegionSize * 0.5f, pOffset.Y + (float)Constants.RegionSize * 0.5f, 0);
                    RegionTerrain.Add(pOffset, GroundGeom);
                    TerrainHeightFieldHeights.Add(GroundGeom, _heightmap);
                    TerrainHeightFieldHeightsHandlers.Add(GroundGeom, _heightmaphandler);
                }
            }
        }


        public override void DeleteTerrain()
        {
        }

        public float GetWaterLevel()
        {
            return waterlevel;
        }

        public override bool SupportsCombining()
        {
            return true;
        }
/*
        public override void UnCombine(PhysicsScene pScene)
        {
            IntPtr localGround = IntPtr.Zero;
//            float[] localHeightfield;
            bool proceed = false;
            List<IntPtr> geomDestroyList = new List<IntPtr>();

            lock (OdeLock)
            {
                if (RegionTerrain.TryGetValue(Vector3.Zero, out localGround))
                {
                    foreach (IntPtr geom in TerrainHeightFieldHeights.Keys)
                    {
                        if (geom == localGround)
                        {
//                            localHeightfield = TerrainHeightFieldHeights[geom];
                            proceed = true;
                        }
                        else
                        {
                            geomDestroyList.Add(geom);
                        }
                    }

                    if (proceed)
                    {
                        m_worldOffset = Vector3.Zero;
                        WorldExtents = new Vector2((int)Constants.RegionSize, (int)Constants.RegionSize);
                        m_parentScene = null;

                        foreach (IntPtr g in geomDestroyList)
                        {
                            // removingHeightField needs to be done or the garbage collector will
                            // collect the terrain data before we tell ODE to destroy it causing 
                            // memory corruption
                            if (TerrainHeightFieldHeights.ContainsKey(g))
                            {
//                                float[] removingHeightField = TerrainHeightFieldHeights[g];
                                TerrainHeightFieldHeights.Remove(g);

                                if (RegionTerrain.ContainsKey(g))
                                {
                                    RegionTerrain.Remove(g);
                                }

                                d.GeomDestroy(g);
                                //removingHeightField = new float[0];
                            }
                        }

                    }
                    else
                    {
                        m_log.Warn("[PHYSICS]: Couldn't proceed with UnCombine.  Region has inconsistant data.");
                    }
                }
            }
        }
*/
        public override void SetWaterLevel(float baseheight)
        {
            waterlevel = baseheight;
//            randomizeWater(waterlevel);
        }
/*
        public void randomizeWater(float baseheight)
        {
            const uint heightmapWidth = Constants.RegionSize + 2;
            const uint heightmapHeight = Constants.RegionSize + 2;
            const uint heightmapWidthSamples = heightmapWidth + 1;
            const uint heightmapHeightSamples = heightmapHeight + 1;

            const float scale = 1.0f;
            const float offset = 0.0f;
            const int wrap = 0;

            float[] _watermap = new float[heightmapWidthSamples * heightmapWidthSamples];

            float maxheigh = float.MinValue;
            float minheigh = float.MaxValue;
            float val;
            for (int i = 0; i < (heightmapWidthSamples * heightmapHeightSamples); i++)
            {

                val = (baseheight - 0.1f) + ((float)fluidRandomizer.Next(1, 9) / 10f);
                _watermap[i] = val;
                if (maxheigh < val)
                    maxheigh = val;
                if (minheigh > val)
                    minheigh = val;
            }

            float thickness = minheigh;

            lock (OdeLock)
            {
                if (WaterGeom != IntPtr.Zero)
                {
                    actor_name_map.Remove(WaterGeom);
                    d.GeomDestroy(WaterGeom);
                    d.GeomHeightfieldDataDestroy(WaterHeightmapData);
                    WaterGeom = IntPtr.Zero;
                    WaterHeightmapData = IntPtr.Zero;
                    if(WaterMapHandler.IsAllocated)
                        WaterMapHandler.Free();
                }

                WaterHeightmapData = d.GeomHeightfieldDataCreate();

                WaterMapHandler = GCHandle.Alloc(_watermap, GCHandleType.Pinned);

                d.GeomHeightfieldDataBuildSingle(WaterHeightmapData, WaterMapHandler.AddrOfPinnedObject(), 0, heightmapWidth, heightmapHeight,
                                                 (int)heightmapWidthSamples, (int)heightmapHeightSamples, scale,
                                                 offset, thickness, wrap);
                d.GeomHeightfieldDataSetBounds(WaterHeightmapData, minheigh, maxheigh);
                WaterGeom = d.CreateHeightfield(StaticSpace, WaterHeightmapData, 1);
                if (WaterGeom != IntPtr.Zero)
                {
                    d.GeomSetCategoryBits(WaterGeom, (uint)(CollisionCategories.Water));
                    d.GeomSetCollideBits(WaterGeom, 0);


                    PhysicsActor pa = new NullPhysicsActor();
                    pa.Name = "Water";
                    pa.PhysicsActorType = (int)ActorTypes.Water;

                    actor_name_map[WaterGeom] = pa;
//                    geom_name_map[WaterGeom] = "Water";

                    d.Matrix3 R = new d.Matrix3();

                    Quaternion q1 = Quaternion.CreateFromAxisAngle(new Vector3(1, 0, 0), 1.5707f);
                    Quaternion q2 = Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), 1.5707f);

                    q1 = q1 * q2;
                    Vector3 v3;
                    float angle;
                    q1.GetAxisAngle(out v3, out angle);

                    d.RFromAxisAndAngle(out R, v3.X, v3.Y, v3.Z, angle);
                    d.GeomSetRotation(WaterGeom, ref R);
                    d.GeomSetPosition(WaterGeom, (float)Constants.RegionSize * 0.5f, (float)Constants.RegionSize * 0.5f, 0);
                }
            }
        }
*/
        public override void Dispose()
        {
            if (m_meshWorker != null)
                m_meshWorker.Stop();

            lock (OdeLock)
            {
                m_rayCastManager.Dispose();
                m_rayCastManager = null;

                lock (_prims)
                {
                    ChangesQueue.Clear();
                    foreach (OdePrim prm in _prims)
                    {
                        prm.DoAChange(changes.Remove, null);
                        _collisionEventPrim.Remove(prm);
                    }
                    _prims.Clear();
                }

                OdeCharacter[] chtorem;
                lock (_characters)
                {
                    chtorem = new OdeCharacter[_characters.Count];
                    _characters.CopyTo(chtorem);
                }

                ChangesQueue.Clear();
                foreach (OdeCharacter ch in chtorem)
                    ch.DoAChange(changes.Remove, null);

                               
                foreach (IntPtr GroundGeom in RegionTerrain.Values)
                {
                    if (GroundGeom != IntPtr.Zero)
                        d.GeomDestroy(GroundGeom);
                }


                RegionTerrain.Clear();

                if (TerrainHeightFieldHeightsHandlers.Count > 0)
                {
                    foreach (GCHandle gch in TerrainHeightFieldHeightsHandlers.Values)
                    {
                        if (gch.IsAllocated)
                            gch.Free();
                    }
                }

                TerrainHeightFieldHeightsHandlers.Clear();
                TerrainHeightFieldHeights.Clear();
/*
                if (WaterGeom != IntPtr.Zero)
                {
                    d.GeomDestroy(WaterGeom);
                        WaterGeom = IntPtr.Zero;
                    if (WaterHeightmapData != IntPtr.Zero)
                        d.GeomHeightfieldDataDestroy(WaterHeightmapData);
                    WaterHeightmapData = IntPtr.Zero;

                    if (WaterMapHandler.IsAllocated)
                        WaterMapHandler.Free();
                }
*/
                if (ContactgeomsArray != IntPtr.Zero)
                    Marshal.FreeHGlobal(ContactgeomsArray);
                if (GlobalContactsArray != IntPtr.Zero)
                    Marshal.FreeHGlobal(GlobalContactsArray);


                d.WorldDestroy(world);
                world = IntPtr.Zero;
                //d.CloseODE();
            }
        }

        public override Dictionary<uint, float> GetTopColliders()
        {
            Dictionary<uint, float> returncolliders = new Dictionary<uint, float>();
            int cnt = 0;
            lock (_prims)
            {
                foreach (OdePrim prm in _prims)
                {
                    if (prm.CollisionScore > 0)
                    {
                        returncolliders.Add(prm.LocalID, prm.CollisionScore);
                        cnt++;
                        prm.CollisionScore = 0f;
                        if (cnt > 25)
                        {
                            break;
                        }
                    }
                }
            }
            return returncolliders;
        }

        public override bool SupportsRayCast()
        {
            return true;
        }

        public override void RaycastWorld(Vector3 position, Vector3 direction, float length, RaycastCallback retMethod)
        {
            if (retMethod != null)
            {
                ODERayRequest req = new ODERayRequest();
                req.geom = IntPtr.Zero;
                req.callbackMethod = retMethod;
                req.length = length;
                req.Normal = direction;
                req.Origin = position;
                req.Count = 0;
                req.filter = RayFilterFlags.AllPrims;

                m_rayCastManager.QueueRequest(req);
            }
        }

        public override void RaycastWorld(Vector3 position, Vector3 direction, float length, int Count, RayCallback retMethod)
        {
            if (retMethod != null)
            {
                ODERayRequest req = new ODERayRequest();
                req.geom = IntPtr.Zero;
                req.callbackMethod = retMethod;
                req.length = length;
                req.Normal = direction;
                req.Origin = position;
                req.Count = Count;
                req.filter = RayFilterFlags.AllPrims;

                m_rayCastManager.QueueRequest(req);
            }
        }

       
        public override List<ContactResult> RaycastWorld(Vector3 position, Vector3 direction, float length, int Count)
        {
            List<ContactResult> ourresults = new List<ContactResult>();
            object SyncObject = new object();

            RayCallback retMethod = delegate(List<ContactResult> results)
            {
                lock (SyncObject)
                {
                    ourresults = results;
                    Monitor.PulseAll(SyncObject);
                }
            };

            ODERayRequest req = new ODERayRequest();
            req.geom = IntPtr.Zero;
            req.callbackMethod = retMethod;
            req.length = length;
            req.Normal = direction;
            req.Origin = position;
            req.Count = Count;
            req.filter = RayFilterFlags.AllPrims;

            lock (SyncObject)
            {
                m_rayCastManager.QueueRequest(req);
                if (!Monitor.Wait(SyncObject, 500))
                    return null;
                else
                    return ourresults;
            }
        }

        public override bool SuportsRaycastWorldFiltered()
        {
            return true;
        }

        public override object RaycastWorld(Vector3 position, Vector3 direction, float length, int Count, RayFilterFlags filter)
        {
            object SyncObject = new object();
            List<ContactResult> ourresults = new List<ContactResult>();

            RayCallback retMethod = delegate(List<ContactResult> results)
            {
                lock (SyncObject)
                {
                    ourresults = results;
                    Monitor.PulseAll(SyncObject);
                }
            };

            ODERayRequest req = new ODERayRequest();
            req.geom = IntPtr.Zero;
            req.callbackMethod = retMethod;
            req.length = length;
            req.Normal = direction;
            req.Origin = position;
            req.Count = Count;
            req.filter = filter;

            lock (SyncObject)
            {
                m_rayCastManager.QueueRequest(req);
                if (!Monitor.Wait(SyncObject, 500))
                    return null;
                else
                    return ourresults;
            }
        }

        public override List<ContactResult> RaycastActor(PhysicsActor actor, Vector3 position, Vector3 direction, float length, int Count, RayFilterFlags flags)
        {
            if (actor == null)
                return new List<ContactResult>();

            IntPtr geom;
            if (actor is OdePrim)
                geom = ((OdePrim)actor).prim_geom;
            else if (actor is OdeCharacter)
                geom = ((OdePrim)actor).prim_geom;
            else
                return new List<ContactResult>();

            if (geom == IntPtr.Zero)
                return new List<ContactResult>();

            List<ContactResult> ourResults = null;
            object SyncObject = new object();

            RayCallback retMethod = delegate(List<ContactResult> results)
            {
                lock (SyncObject)
                {
                    ourResults = results;
                    Monitor.PulseAll(SyncObject);
                }
            };

            ODERayRequest req = new ODERayRequest();
            req.geom = geom;
            req.callbackMethod = retMethod;
            req.length = length;
            req.Normal = direction;
            req.Origin = position;
            req.Count = Count;
            req.filter = flags;

            lock (SyncObject)
            {
                m_rayCastManager.QueueRequest(req);
                if (!Monitor.Wait(SyncObject, 500))
                    return new List<ContactResult>();
            }

            if (ourResults == null)
                return new List<ContactResult>();
            return ourResults;
        }

        public override List<ContactResult> BoxProbe(Vector3 position, Vector3 size, Quaternion orientation, int Count, RayFilterFlags flags)
        {
            List<ContactResult> ourResults = null;
            object SyncObject = new object();

            ProbeBoxCallback retMethod = delegate(List<ContactResult> results)
            {
                lock (SyncObject)
                {
                    ourResults = results;
                    Monitor.PulseAll(SyncObject);
                }
            };

            ODERayRequest req = new ODERayRequest();
            req.geom = IntPtr.Zero;
            req.callbackMethod = retMethod;
            req.Normal = size;
            req.Origin = position;
            req.orientation = orientation;
            req.Count = Count;
            req.filter = flags;

            lock (SyncObject)
            {
                m_rayCastManager.QueueRequest(req);
                if (!Monitor.Wait(SyncObject, 500))
                    return new List<ContactResult>();
            }

            if (ourResults == null)
                return new List<ContactResult>();
            return ourResults;
        }

        public override List<ContactResult> SphereProbe(Vector3 position, float radius, int Count, RayFilterFlags flags)
        {
            List<ContactResult> ourResults = null;
            object SyncObject = new object();

            ProbeSphereCallback retMethod = delegate(List<ContactResult> results)
            {
                ourResults = results;
                Monitor.PulseAll(SyncObject);
            };

            ODERayRequest req = new ODERayRequest();
            req.geom = IntPtr.Zero;
            req.callbackMethod = retMethod;
            req.length = radius;
            req.Origin = position;
            req.Count = Count;
            req.filter = flags;


            lock (SyncObject)
            {
                m_rayCastManager.QueueRequest(req);
                if (!Monitor.Wait(SyncObject, 500))
                    return new List<ContactResult>();
            }

            if (ourResults == null)
                return new List<ContactResult>();
            return ourResults;
        }

        public override List<ContactResult> PlaneProbe(PhysicsActor actor, Vector4 plane, int Count, RayFilterFlags flags)
        {
            IntPtr geom = IntPtr.Zero;;

            if (actor != null)
            {
                if (actor is OdePrim)
                    geom = ((OdePrim)actor).prim_geom;
                else if (actor is OdeCharacter)
                    geom = ((OdePrim)actor).prim_geom;
            }

            List<ContactResult> ourResults = null;
            object SyncObject = new object();

            ProbePlaneCallback retMethod = delegate(List<ContactResult> results)
            {
                ourResults = results;
                Monitor.PulseAll(SyncObject);
            };

            ODERayRequest req = new ODERayRequest();
            req.geom = geom;
            req.callbackMethod = retMethod;
            req.length = plane.W;
            req.Normal.X = plane.X;
            req.Normal.Y = plane.Y;
            req.Normal.Z = plane.Z;
            req.Count = Count;
            req.filter = flags;

            lock (SyncObject)
            {
                m_rayCastManager.QueueRequest(req);
                if (!Monitor.Wait(SyncObject, 500))
                    return new List<ContactResult>();
            }

            if (ourResults == null)
                return new List<ContactResult>();
            return ourResults;
        }

        public override int SitAvatar(PhysicsActor actor, Vector3 AbsolutePosition, Vector3 CameraPosition, Vector3 offset, Vector3 AvatarSize, SitAvatarCallback PhysicsSitResponse)
        {
            Util.FireAndForget( delegate
            {
                ODESitAvatar sitAvatar = new ODESitAvatar(this, m_rayCastManager);
                if(sitAvatar != null)
                    sitAvatar.Sit(actor, AbsolutePosition, CameraPosition, offset, AvatarSize, PhysicsSitResponse);
            });
            return 1;
        }

    }
}
