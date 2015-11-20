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

// Revision 2011/12/13 by Ubit Umarov
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
using Mono.Addins;
using OdeAPI;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.ubOde
{
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

    public class ODEScene : PhysicsScene
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public bool m_OSOdeLib = false;
        public bool m_suportCombine = false; // mega suport not tested
        public Scene m_frameWorkScene = null;

//        private int threadid = 0;

//        const d.ContactFlags comumContactFlags = d.ContactFlags.SoftERP | d.ContactFlags.SoftCFM |d.ContactFlags.Approx1 | d.ContactFlags.Bounce;

        const d.ContactFlags comumContactFlags = d.ContactFlags.Bounce | d.ContactFlags.Approx1 | d.ContactFlags.Slip1 | d.ContactFlags.Slip2;
        const float comumContactERP = 0.75f;
        const float comumContactCFM = 0.0001f;
        const float comumContactSLIP = 0f;
        
        float frictionMovementMult = 0.8f;

        float TerrainBounce = 0.1f;
        float TerrainFriction = 0.3f;

        public float AvatarFriction = 0;// 0.9f * 0.5f;

        // this netx dimensions are only relevant for terrain partition (mega regions)
        // WorldExtents below has the simulation dimensions
        // they should be identical except on mega regions
        private uint m_regionWidth = Constants.RegionSize;
        private uint m_regionHeight = Constants.RegionSize;

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

        private float avDensity = 80f;
        private float avMovementDivisorWalk = 1.3f;
        private float avMovementDivisorRun = 0.8f;
        private float minimumGroundFlightOffset = 3f;
        public float maximumMassObject = 10000.01f;
        public float geomDefaultDensity = 10.0f;

        public float maximumAngularVelocity = 12.0f; // default 12rad/s
        public float maxAngVelocitySQ = 144f;   // squared value

        public float bodyPIDD = 35f;
        public float bodyPIDG = 25;

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
        public Dictionary<IntPtr, PhysicsActor> actor_name_map = new Dictionary<IntPtr, PhysicsActor>();

        private float contactsurfacelayer = 0.002f;

        private int contactsPerCollision = 80;
        internal IntPtr ContactgeomsArray = IntPtr.Zero;
        private IntPtr GlobalContactsArray = IntPtr.Zero;
        private d.Contact SharedTmpcontact = new d.Contact();

        const int maxContactsbeforedeath = 6000;
        private volatile int m_global_contactcount = 0;

        private IntPtr contactgroup;

        public ContactData[] m_materialContactsData = new ContactData[8];

        private Dictionary<Vector3, IntPtr> RegionTerrain = new Dictionary<Vector3, IntPtr>();
        private Dictionary<IntPtr, float[]> TerrainHeightFieldHeights = new Dictionary<IntPtr, float[]>();
        private Dictionary<IntPtr, GCHandle> TerrainHeightFieldHeightsHandlers = new Dictionary<IntPtr, GCHandle>();
       
        private int m_physicsiterations = 15;
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

        // some speedup variables
        private int spaceGridMaxX;
        private int spaceGridMaxY;
        private float spacesPerMeterX;
        private float spacesPerMeterY;

        // split static geometry collision into a grid as before
        private IntPtr[,] staticPrimspace;
        private IntPtr[] staticPrimspaceOffRegion;

        public Object OdeLock;
        public static Object SimulationLock;

        public IMesher mesher;

        public IConfigSource m_config;

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

        IConfig physicsconfig = null;

        public ODEScene(Scene pscene, IConfigSource psourceconfig, string pname, bool pOSOdeLib)
        {
            OdeLock = new Object();

            EngineType = pname;
            PhysicsSceneName = EngineType + "/" + pscene.RegionInfo.RegionName;

			m_config = psourceconfig;
            m_OSOdeLib = pOSOdeLib;

//            m_OSOdeLib = false; //debug

            m_frameWorkScene = pscene;

            m_frameWorkScene.RegisterModuleInterface<PhysicsScene>(this);

            Initialization();

            base.Initialise(m_frameWorkScene.PhysicsRequestAsset,
                (m_frameWorkScene.Heightmap != null ? m_frameWorkScene.Heightmap.GetFloatsSerialised() : new float[m_frameWorkScene.RegionInfo.RegionSizeX * m_frameWorkScene.RegionInfo.RegionSizeY]),
                (float)m_frameWorkScene.RegionInfo.RegionSettings.WaterHeight);           
        }

        public void RegionLoaded()
        {
            mesher = m_frameWorkScene.RequestModuleInterface<IMesher>();
            if (mesher == null)
            {
                m_log.ErrorFormat("[ubOde] No mesher. module disabled");
                return;
            }

            m_meshWorker = new ODEMeshWorker(this, m_log, mesher, physicsconfig);
            m_frameWorkScene.PhysicsEnabled = true;
        }
        /// <summary>
        /// Initiailizes the scene
        /// Sets many properties that ODE requires to be stable
        /// These settings need to be tweaked 'exactly' right or weird stuff happens.
        /// </summary>
        private void Initialization()
        {
            d.AllocateODEDataForThread(~0U);

            SimulationLock = new Object();

            nearCallback = near;

            m_rayCastManager = new ODERayCastRequestManager(this);

            WorldExtents.X = m_frameWorkScene.RegionInfo.RegionSizeX;
            m_regionWidth = (uint)WorldExtents.X;
            WorldExtents.Y = m_frameWorkScene.RegionInfo.RegionSizeY;
            m_regionHeight = (uint)WorldExtents.Y;

            m_suportCombine = false;

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

                d.HashSpaceSetLevels(TopSpace, -5, 12);
                d.HashSpaceSetLevels(ActiveSpace, -5, 10);
                d.HashSpaceSetLevels(CharsSpace, -4, 3);
                d.HashSpaceSetLevels(StaticSpace, -5, 12);
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

                contactgroup = d.JointGroupCreate(maxContactsbeforedeath + 1);
                //contactgroup

                d.WorldSetAutoDisableFlag(world, false);
            }


            //  checkThread();


            // Defaults

            int contactsPerCollision = 80;

            physicsconfig = null;

            if (m_config != null)
            {
                physicsconfig = m_config.Configs["ODEPhysicsSettings"];
                if (physicsconfig != null)
                {
                    gravityx = physicsconfig.GetFloat("world_gravityx", gravityx);
                    gravityy = physicsconfig.GetFloat("world_gravityy", gravityy);
                    gravityz = physicsconfig.GetFloat("world_gravityz", gravityz);

                    metersInSpace = physicsconfig.GetFloat("meters_in_small_space", metersInSpace);

                    //                    contactsurfacelayer = physicsconfig.GetFloat("world_contact_surface_layer", contactsurfacelayer);

                    ODE_STEPSIZE = physicsconfig.GetFloat("world_stepsize", ODE_STEPSIZE);

                    avDensity = physicsconfig.GetFloat("av_density", avDensity);
                    avMovementDivisorWalk = physicsconfig.GetFloat("av_movement_divisor_walk", avMovementDivisorWalk);
                    avMovementDivisorRun = physicsconfig.GetFloat("av_movement_divisor_run", avMovementDivisorRun);

                    contactsPerCollision = physicsconfig.GetInt("contacts_per_collision", contactsPerCollision);

                    geomDefaultDensity = physicsconfig.GetFloat("geometry_default_density", geomDefaultDensity);
                    bodyFramesAutoDisable = physicsconfig.GetInt("body_frames_auto_disable", bodyFramesAutoDisable);

                    physics_logging = physicsconfig.GetBoolean("physics_logging", false);
                    physics_logging_interval = physicsconfig.GetInt("physics_logging_interval", 0);
                    physics_logging_append_existing_logfile = physicsconfig.GetBoolean("physics_logging_append_existing_logfile", false);

                    minimumGroundFlightOffset = physicsconfig.GetFloat("minimum_ground_flight_offset", minimumGroundFlightOffset);
                    maximumMassObject = physicsconfig.GetFloat("maximum_mass_object", maximumMassObject);

                    avDensity *= 3f / 80f;  // scale other engines density option to this
                }
            }

            float heartbeat = 1/m_frameWorkScene.FrameTime;
            maximumAngularVelocity = 0.49f * heartbeat *(float)Math.PI;
            maxAngVelocitySQ = maximumAngularVelocity * maximumAngularVelocity;

            d.WorldSetCFM(world, comumContactCFM);
            d.WorldSetERP(world, comumContactERP);

            d.WorldSetGravity(world, gravityx, gravityy, gravityz);

            d.WorldSetLinearDamping(world, 0.002f);
            d.WorldSetAngularDamping(world, 0.002f);
            d.WorldSetAngularDampingThreshold(world, 0f);
            d.WorldSetLinearDampingThreshold(world, 0f);
            d.WorldSetMaxAngularSpeed(world, maximumAngularVelocity);

            d.WorldSetQuickStepNumIterations(world, m_physicsiterations);

            d.WorldSetContactSurfaceLayer(world, contactsurfacelayer);
            d.WorldSetContactMaxCorrectingVel(world, 60.0f);

            HalfOdeStep = ODE_STEPSIZE * 0.5f;
            odetimestepMS = (int)(1000.0f * ODE_STEPSIZE + 0.5f);

            ContactgeomsArray = Marshal.AllocHGlobal(contactsPerCollision * d.ContactGeom.unmanagedSizeOf);
            GlobalContactsArray = Marshal.AllocHGlobal((maxContactsbeforedeath + 100) * d.Contact.unmanagedSizeOf);

            SharedTmpcontact.geom.g1 = IntPtr.Zero;
            SharedTmpcontact.geom.g2 = IntPtr.Zero;

            SharedTmpcontact.geom.side1 = -1;
            SharedTmpcontact.geom.side2 = -1;

            SharedTmpcontact.surface.mode = comumContactFlags;
            SharedTmpcontact.surface.mu = 0;
            SharedTmpcontact.surface.bounce = 0;
            SharedTmpcontact.surface.soft_cfm = comumContactCFM;
            SharedTmpcontact.surface.soft_erp = comumContactERP;
            SharedTmpcontact.surface.slip1 = comumContactSLIP;
            SharedTmpcontact.surface.slip2 = comumContactSLIP;

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


            spacesPerMeterX = 1.0f / metersInSpace;
            spacesPerMeterY = spacesPerMeterX;
            spaceGridMaxX = (int)(WorldExtents.X * spacesPerMeterX);
            spaceGridMaxY = (int)(WorldExtents.Y * spacesPerMeterY);

            if (spaceGridMaxX > 24)
            {
                spaceGridMaxX = 24;
                spacesPerMeterX = spaceGridMaxX / WorldExtents.X;
            }

            if (spaceGridMaxY > 24)
            {
                spaceGridMaxY = 24;
                spacesPerMeterY = spaceGridMaxY / WorldExtents.Y;
            }

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

            // let this now be index limit
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
        private IntPtr CreateContacJoint(ref d.ContactGeom contactGeom,bool smooth)
        {
            if (m_global_contactcount >= maxContactsbeforedeath)
                return IntPtr.Zero;

            m_global_contactcount++;
            if(smooth)
                SharedTmpcontact.geom.depth = contactGeom.depth * 0.05f;
            else
                SharedTmpcontact.geom.depth = contactGeom.depth;
            SharedTmpcontact.geom.pos = contactGeom.pos;
            SharedTmpcontact.geom.normal = contactGeom.normal;

            IntPtr contact = new IntPtr(GlobalContactsArray.ToInt64() + (Int64)(m_global_contactcount * d.Contact.unmanagedSizeOf));
            Marshal.StructureToPtr(SharedTmpcontact, contact, true);
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

            ContactPoint maxDepthContact = new ContactPoint();

            // do volume detection case
            if ((p1.IsVolumeDtc || p2.IsVolumeDtc))
            {
                maxDepthContact = new ContactPoint(
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
//            bool IgnoreNegSides = false;

            ContactData contactdata1 = new ContactData(0, 0, false);
            ContactData contactdata2 = new ContactData(0, 0, false);

            bool dop1ava = false;
            bool dop2ava = false;
            bool ignore = false;
            bool smoothMesh = false;

            switch (p1.PhysicsActorType)
            {
                case (int)ActorTypes.Agent:
                    {
                        dop1ava = true;
                        switch (p2.PhysicsActorType)
                        {
                            case (int)ActorTypes.Agent:
                            case (int)ActorTypes.Prim:
                                break;

                            default:
                                ignore = true; // avatar to terrain and water ignored
                                break;
                        }
                        break;
                    }

                case (int)ActorTypes.Prim:
                    {
                        switch (p2.PhysicsActorType)
                        {
                            case (int)ActorTypes.Agent:
                                dop2ava = true;
                                break;

                            case (int)ActorTypes.Prim:
                                Vector3 relV = p1.Velocity - p2.Velocity;
                                float relVlenSQ = relV.LengthSquared();
                                if (relVlenSQ > 0.0001f)
                                {
                                    p1.CollidingObj = true;
                                    p2.CollidingObj = true;
                                }
                                p1.getContactData(ref contactdata1);
                                p2.getContactData(ref contactdata2);
                                bounce = contactdata1.bounce * contactdata2.bounce;
                                mu = (float)Math.Sqrt(contactdata1.mu * contactdata2.mu);

                                if (relVlenSQ > 0.01f)
                                    mu *= frictionMovementMult;

                                if(d.GeomGetClass(g2) == d.GeomClassID.TriMeshClass &&
                                    d.GeomGetClass(g1) == d.GeomClassID.TriMeshClass)
                                    smoothMesh = true;
                                break;

                            case (int)ActorTypes.Ground:
                                p1.getContactData(ref contactdata1);
                                bounce = contactdata1.bounce * TerrainBounce;
                                mu = (float)Math.Sqrt(contactdata1.mu * TerrainFriction);

                                if (Math.Abs(p1.Velocity.X) > 0.1f || Math.Abs(p1.Velocity.Y) > 0.1f)
                                    mu *= frictionMovementMult;
                                p1.CollidingGround = true;

                                if(d.GeomGetClass(g1) == d.GeomClassID.TriMeshClass)
                                    smoothMesh = true;
                                break;

                            case (int)ActorTypes.Water:
                            default:
                                ignore = true;
                                break;
                        }
                    }
                    break;

                case (int)ActorTypes.Ground:
                    if (p2.PhysicsActorType == (int)ActorTypes.Prim)
                    {
                        p2.CollidingGround = true;
                        p2.getContactData(ref contactdata2);
                        bounce = contactdata2.bounce * TerrainBounce;
                        mu = (float)Math.Sqrt(contactdata2.mu * TerrainFriction);

//                        if (curContact.side1 > 0) // should be 2 ?
//                            IgnoreNegSides = true;

                        if (Math.Abs(p2.Velocity.X) > 0.1f || Math.Abs(p2.Velocity.Y) > 0.1f)
                            mu *= frictionMovementMult;

                        if(d.GeomGetClass(g2) == d.GeomClassID.TriMeshClass)
                            smoothMesh = true;
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

            IntPtr Joint;
            bool FeetCollision = false;
            int ncontacts = 0;

            int i = 0;

            maxDepthContact = new ContactPoint();
            maxDepthContact.PenetrationDepth = float.MinValue;
            ContactPoint minDepthContact = new ContactPoint();
            minDepthContact.PenetrationDepth = float.MaxValue;

            SharedTmpcontact.geom.depth = 0;
            SharedTmpcontact.surface.mu = mu;
            SharedTmpcontact.surface.bounce = bounce;

            d.ContactGeom altContact = new d.ContactGeom();
            bool useAltcontact = false;
            bool noskip = true;

            if(dop1ava || dop2ava)
                smoothMesh = false;

            while (true)
            {
                noskip = true;
                useAltcontact = false;

                if (dop1ava)
                {
                    if ((((OdeCharacter)p1).Collide(g1, g2, false, ref curContact, ref altContact , ref useAltcontact, ref FeetCollision)))
                    {
                        if (p2.PhysicsActorType == (int)ActorTypes.Agent)
                        {
                            p1.CollidingObj = true;
                            p2.CollidingObj = true;
                        }
                        else if (p2.Velocity.LengthSquared() > 0.0f)
                            p2.CollidingObj = true;
                    }
                    else
                        noskip = false;
                }
                else if (dop2ava)
                {
                if ((((OdeCharacter)p2).Collide(g2, g1, true, ref curContact, ref altContact , ref useAltcontact, ref FeetCollision)))
                    {
                    if (p1.PhysicsActorType == (int)ActorTypes.Agent)
                        {
                            p1.CollidingObj = true;
                            p2.CollidingObj = true;
                        }
                    else if (p2.Velocity.LengthSquared() > 0.0f)
                        p1.CollidingObj = true;
                    }
                else
                    noskip = false;
                }

                if (noskip)
                {
                    if(useAltcontact)
                        Joint = CreateContacJoint(ref altContact,smoothMesh);
                    else
                        Joint = CreateContacJoint(ref curContact,smoothMesh);

                    if (Joint == IntPtr.Zero)
                        break;

                    d.JointAttach(Joint, b1, b2);

                    ncontacts++;

                    if (curContact.depth > maxDepthContact.PenetrationDepth)
                    {
                        maxDepthContact.Position.X = curContact.pos.X;
                        maxDepthContact.Position.Y = curContact.pos.Y;
                        maxDepthContact.Position.Z = curContact.pos.Z;
                        maxDepthContact.PenetrationDepth = curContact.depth;
                        maxDepthContact.CharacterFeet = FeetCollision;
                    }

                    if (curContact.depth < minDepthContact.PenetrationDepth)
                    {
                        minDepthContact.PenetrationDepth = curContact.depth;
                        minDepthContact.SurfaceNormal.X = curContact.normal.X;
                        minDepthContact.SurfaceNormal.Y = curContact.normal.Y;
                        minDepthContact.SurfaceNormal.Z = curContact.normal.Z;
                    }
                }

                if (++i >= count)
                    break;

                if (!GetCurContactGeom(i, ref curContact))
                    break;
            }

            if (ncontacts > 0)
            {
                maxDepthContact.SurfaceNormal.X = minDepthContact.SurfaceNormal.X;
                maxDepthContact.SurfaceNormal.Y = minDepthContact.SurfaceNormal.Y;
                maxDepthContact.SurfaceNormal.Z = minDepthContact.SurfaceNormal.Z;

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
                        if (chr == null)
                            continue;

                        chr.IsColliding = false;
                        // chr.CollidingGround = false; not done here
                        chr.CollidingObj = false;
                        
                        if(chr.Body == IntPtr.Zero || chr.collider == IntPtr.Zero )
                            continue;
                    
                        // do colisions with static space
                        d.SpaceCollide2(chr.collider, StaticSpace, IntPtr.Zero, nearCallback);

                        // no coll with gnd
                    }
                    // chars with chars
                    d.SpaceCollide(CharsSpace, IntPtr.Zero, nearCallback);

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
            lock (_activegroups)
            {
                try
                {
                    foreach (OdePrim aprim in _activegroups)
                    {
                        if(!aprim.m_outbounds && d.BodyIsEnabled(aprim.Body) &&
                                aprim.collide_geom != IntPtr.Zero)    
                        {
                            d.SpaceCollide2(StaticSpace, aprim.collide_geom, IntPtr.Zero, nearCallback);
                            d.SpaceCollide2(GroundSpace, aprim.collide_geom, IntPtr.Zero, nearCallback);
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Warn("[PHYSICS]: Unable to collide Active to Static: " + e.Message);
                }
            }

            // colide active amoung them
            try
            {
                d.SpaceCollide(ActiveSpace, IntPtr.Zero, nearCallback);
            }
            catch (Exception e)
            {
                    m_log.Warn("[PHYSICS]: Unable to collide in Active: " + e.Message);
            }

            // and with chars
            try
            {
                d.SpaceCollide2(CharsSpace,ActiveSpace, IntPtr.Zero, nearCallback);
            }
            catch (Exception e)
            {
                    m_log.Warn("[PHYSICS]: Unable to collide Active to Character: " + e.Message);
            }
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

        public override PhysicsActor AddAvatar(string avName, Vector3 position, Vector3 velocity, Vector3 size, bool isFlying)
        {
            return null;
        }

        public override PhysicsActor AddAvatar(uint localID, string avName, Vector3 position, Vector3 size, float feetOffset, bool isFlying)
        {
             OdeCharacter newAv = new OdeCharacter(localID, avName, this, position,
                size, feetOffset, avDensity, avMovementDivisorWalk, avMovementDivisorRun);
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
            lock (OdeLock)
            {
                d.AllocateODEDataForThread(0);
                ((OdeCharacter) actor).Destroy();
            }
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

            x = (int)(pos.X * spacesPerMeterX);
            if (x > spaceGridMaxX)
                return staticPrimspaceOffRegion[1];
            
            y = (int)(pos.Y * spacesPerMeterY);
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
        public override void ProcessPreSimulation()
        {
            lock (OdeLock)
            {
                if (world == IntPtr.Zero)
                {
                    ChangesQueue.Clear();
                    return;
                }

                d.AllocateODEDataForThread(~0U);

                ODEchangeitem item;

                int donechanges = 0;
                if (ChangesQueue.Count > 0)
                {
                    m_log.InfoFormat("[ubOde] start processing pending actor operations");
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
                    m_log.InfoFormat("[ubOde] finished {0} operations in {1}ms", donechanges, time);
                }
                m_log.InfoFormat("[ubOde] {0} prim actors loaded",_prims.Count);
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
        public override float Simulate(float reqTimeStep)
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan timedif = now - m_lastframe;
            float timeStep = (float)timedif.TotalSeconds;
            m_lastframe = now;
            
            // acumulate time so we can reduce error
            step_time += timeStep;

            if (step_time < HalfOdeStep)
                return 0;

            if (framecount < 0)
                framecount = 0;

            framecount++;

//            checkThread();
            int nodeframes = 0;
            float fps = 0;

            lock (SimulationLock)
                lock(OdeLock)
            {
                if (world == IntPtr.Zero)
                {
                    ChangesQueue.Clear();
                    return 0;
                }

                ODEchangeitem item;
                
//                d.WorldSetQuickStepNumIterations(world, curphysiteractions);

                int loopstartMS = Util.EnvironmentTickCount();
                int looptimeMS = 0;
                int changestimeMS = 0;
                int maxChangestime = (int)(reqTimeStep * 500f); // half the time
                int maxLoopTime = (int)(reqTimeStep * 1200f); // 1.2 the time
 
                d.AllocateODEDataForThread(~0U);
               
                if (ChangesQueue.Count > 0)
                {
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
                        changestimeMS = Util.EnvironmentTickCountSubtract(loopstartMS);
                        if (changestimeMS > maxChangestime)
                            break;
                    }
                }

                // do simulation taking at most 150ms total time including changes
                while (step_time > HalfOdeStep)
                {
                    try
                    {
                        // clear pointer/counter to contacts to pass into joints
                        m_global_contactcount = 0;


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
                                        actor.UpdatePositionAndVelocity(framecount);
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
                    if (looptimeMS > maxLoopTime)
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

// information block for in debug breakpoint only
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
                
                fps = (float)nodeframes * ODE_STEPSIZE / reqTimeStep;

                if(step_time < HalfOdeStep)
                    m_timeDilation = 1.0f;
                else if (step_time > m_SkipFramesAtms)
                {
                    // if we lag too much skip frames
                    m_timeDilation = 0.0f;
                    step_time = 0;
                    m_lastframe = DateTime.UtcNow; // skip also the time lost
                }
                else
                {
                    m_timeDilation = ODE_STEPSIZE / step_time;
                    if (m_timeDilation > 1)
                        m_timeDilation = 1;
                }
            }

            return fps;
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

            int offsetX = 0;
            int offsetY = 0;

            if (m_suportCombine)
            {
                offsetX = ((int)(x / (int)Constants.RegionSize)) * (int)Constants.RegionSize;
                offsetY = ((int)(y / (int)Constants.RegionSize)) * (int)Constants.RegionSize;
            }

            // get region map
            IntPtr heightFieldGeom = IntPtr.Zero;
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

            int regsizeX = (int)m_regionWidth + 3; // map size see setterrain number of samples
            int regsizeY = (int)m_regionHeight + 3; // map size see setterrain number of samples
            int regsize = regsizeX;

            if (m_OSOdeLib)
            {
                if (x < regsizeX - 1)
                {
                    ix = (int)x;
                    dx = x - (float)ix;
                }
                else // out world use external height
                {
                    ix = regsizeX - 2;
                    dx = 0;
                }
                if (y < regsizeY - 1)
                {
                    iy = (int)y;
                    dy = y - (float)iy;
                }
                else
                {
                    iy = regsizeY - 2;
                    dy = 0;
                }
            }
            else
            {
                // we  still have square fixed size regions
                // also flip x and y because of how map is done for ODE fliped axis
                // so ix,iy,dx and dy are inter exchanged

                regsize = regsizeY;

                if (x < regsizeX - 1)
                {
                    iy = (int)x;
                    dy = x - (float)iy;
                }
                else // out world use external height
                {
                    iy = regsizeX - 2;
                    dy = 0;
                }
                if (y < regsizeY - 1)
                {
                    ix = (int)y;
                    dx = y - (float)ix;
                }
                else
                {
                    ix = regsizeY - 2;
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

            if (dy>dx)
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

        public Vector3 GetTerrainNormalAtXY(float x, float y)
        {
            int offsetX = 0;
            int offsetY = 0;

            if (m_suportCombine)
            {
                offsetX = ((int)(x / (int)Constants.RegionSize)) * (int)Constants.RegionSize;
                offsetY = ((int)(y / (int)Constants.RegionSize)) * (int)Constants.RegionSize;
            }

            // get region map
            IntPtr heightFieldGeom = IntPtr.Zero;
            Vector3 norm = new Vector3(0, 0, 1);

            if (!RegionTerrain.TryGetValue(new Vector3(offsetX, offsetY, 0), out heightFieldGeom))
                return norm; ;

            if (heightFieldGeom == IntPtr.Zero)
                return norm;

            if (!TerrainHeightFieldHeights.ContainsKey(heightFieldGeom))
                return norm;

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

            int regsizeX = (int)m_regionWidth + 3; // map size see setterrain number of samples
            int regsizeY = (int)m_regionHeight + 3; // map size see setterrain number of samples
            int regsize = regsizeX;

            int xstep = 1;
            int ystep = regsizeX;
            bool firstTri = false;

            if (m_OSOdeLib)
            {
                if (x < regsizeX - 1)
                {
                    ix = (int)x;
                    dx = x - (float)ix;
                }
                else // out world use external height
                {
                    ix = regsizeX - 2;
                    dx = 0;
                }
                if (y < regsizeY - 1)
                {
                    iy = (int)y;
                    dy = y - (float)iy;
                }
                else
                {
                    iy = regsizeY - 2;
                    dy = 0;
                }
                firstTri = dy > dx;
            }

            else
            {
                xstep = regsizeY;
                ystep = 1;
                regsize = regsizeY;

                // we  still have square fixed size regions
                // also flip x and y because of how map is done for ODE fliped axis
                // so ix,iy,dx and dy are inter exchanged
                if (x < regsizeX - 1)
                {
                    iy = (int)x;
                    dy = x - (float)iy;
                }
                else // out world use external height
                {
                    iy = regsizeX - 2;
                    dy = 0;
                }
                if (y < regsizeY - 1)
                {
                    ix = (int)y;
                    dx = y - (float)ix;
                }
                else
                {
                    ix = regsizeY - 2;
                    dx = 0;
                }
                firstTri = dx > dy;
            }

            float h0;
            float h1;
            float h2;

            iy *= regsize;
            iy += ix; // all indexes have iy + ix

            float[] heights = TerrainHeightFieldHeights[heightFieldGeom];

            if (firstTri)
            {
                h1 = ((float)heights[iy]); // 0,0 vertice
                iy += ystep;
                h0 = (float)heights[iy]; // 0,1
                h2 = (float)heights[iy+xstep]; // 1,1 vertice
                norm.X = h0 - h2;
                norm.Y = h1 - h0;              
            }
            else
            {
                h2 = ((float)heights[iy]); // 0,0 vertice
                iy += xstep;
                h0 = ((float)heights[iy]); // 1,0 vertice
                h1 = (float)heights[iy+ystep]; // vertice 1,1
                norm.X = h2 - h0;
                norm.Y = h0 - h1;
            }
            norm.Z = 1;
            norm.Normalize();
            return norm;
        }

        public override void SetTerrain(float[] heightMap)
        {
            if (m_worldOffset != Vector3.Zero && m_parentScene != null)
            {
                if (m_parentScene is ODEScene)
                {
                    ((ODEScene)m_parentScene).SetTerrain(heightMap, m_worldOffset);
                }
            }
            else
            {
                SetTerrain(heightMap, m_worldOffset);
            }
        }

        public override void CombineTerrain(float[] heightMap, Vector3 pOffset)
        {
            if(m_suportCombine)
                SetTerrain(heightMap, pOffset);
        }

        public void SetTerrain(float[] heightMap, Vector3 pOffset)
        {
            if (m_OSOdeLib)
                OSSetTerrain(heightMap, pOffset);
            else
                OriSetTerrain(heightMap, pOffset);
        }

        public void OriSetTerrain(float[] heightMap, Vector3 pOffset)
        {
            // assumes 1m size grid and constante size square regions
            // needs to know about sims around in future

            float[] _heightmap;

            uint regionsizeX = m_regionWidth;
            uint regionsizeY = m_regionHeight;

            // map is rotated
            uint heightmapWidth = regionsizeY + 2;
            uint heightmapHeight = regionsizeX + 2;

            uint heightmapWidthSamples = heightmapWidth + 1;
            uint heightmapHeightSamples = heightmapHeight + 1;

            _heightmap = new float[heightmapWidthSamples * heightmapHeightSamples];

            const float scale = 1.0f;
            const float offset = 0.0f;
            const float thickness = 10f;
            const int wrap = 0;

 
            float hfmin = float.MaxValue;
            float hfmax = float.MinValue;
            float val;
            uint xx;
            uint yy;

            uint maxXX = regionsizeX - 1;
            uint maxYY = regionsizeY - 1;
            // flipping map adding one margin all around so things don't fall in edges

            uint xt = 0;
            xx = 0;

            for (uint x = 0; x < heightmapWidthSamples; x++)
            {
                if (x > 1 && xx < maxXX)
                    xx++;
                yy = 0;
                for (uint y = 0; y < heightmapHeightSamples; y++)
                {
                    if (y > 1 && y < maxYY)
                        yy += regionsizeX;

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
                d.AllocateODEDataForThread(~0U);

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

                d.GeomHeightfieldDataBuildSingle(HeightmapData, _heightmaphandler.AddrOfPinnedObject(), 0,
                                                heightmapHeight, heightmapWidth ,
                                                 (int)heightmapHeightSamples, (int)heightmapWidthSamples, scale,
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

                    d.Quaternion q = new d.Quaternion();
                    q.X = 0.5f;
                    q.Y = 0.5f;
                    q.Z = 0.5f;
                    q.W = 0.5f;

                    d.GeomSetQuaternion(GroundGeom, ref q);
                    d.GeomSetPosition(GroundGeom, pOffset.X + m_regionWidth * 0.5f, pOffset.Y + m_regionHeight * 0.5f, 0.0f);
                    RegionTerrain.Add(pOffset, GroundGeom);
                    TerrainHeightFieldHeights.Add(GroundGeom, _heightmap);
                    TerrainHeightFieldHeightsHandlers.Add(GroundGeom, _heightmaphandler);
                }
            }
        }

        public void OSSetTerrain(float[] heightMap, Vector3 pOffset)
        {
            // assumes 1m size grid and constante size square regions
            // needs to know about sims around in future

            float[] _heightmap;

            uint regionsizeX = m_regionWidth;
            uint regionsizeY = m_regionHeight;

            uint heightmapWidth = regionsizeX + 2;
            uint heightmapHeight = regionsizeY + 2;

            uint heightmapWidthSamples = heightmapWidth + 1;
            uint heightmapHeightSamples = heightmapHeight + 1;

            _heightmap = new float[heightmapWidthSamples * heightmapHeightSamples];


            float hfmin = float.MaxValue;
//            float hfmax = float.MinValue;
            float val;


            uint maxXX = regionsizeX - 1;
            uint maxYY = regionsizeY - 1;
            // adding one margin all around so things don't fall in edges

            uint xx;
            uint yy = 0;
            uint yt = 0;

            for (uint y = 0; y < heightmapHeightSamples; y++)
            {
                if (y > 1 && y < maxYY)
                    yy += regionsizeX;
                xx = 0;
                for (uint x = 0; x < heightmapWidthSamples; x++)
                {
                    if (x > 1 && x < maxXX)
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
                IntPtr HeightmapData = d.GeomOSTerrainDataCreate();

                const int wrap = 0;
                float thickness = hfmin;
                if (thickness < 0)
                    thickness = 1;

                GCHandle _heightmaphandler = GCHandle.Alloc(_heightmap, GCHandleType.Pinned);

                d.GeomOSTerrainDataBuild(HeightmapData, _heightmaphandler.AddrOfPinnedObject(), 0, 1.0f,
                                                 (int)heightmapWidthSamples, (int)heightmapHeightSamples,
                                                 thickness, wrap);

//                d.GeomOSTerrainDataSetBounds(HeightmapData, hfmin - 1, hfmax + 1);
                GroundGeom = d.CreateOSTerrain(GroundSpace, HeightmapData, 1);
                if (GroundGeom != IntPtr.Zero)
                {
                    d.GeomSetCategoryBits(GroundGeom, (uint)(CollisionCategories.Land));
                    d.GeomSetCollideBits(GroundGeom, 0);


                    PhysicsActor pa = new NullPhysicsActor();
                    pa.Name = "Terrain";
                    pa.PhysicsActorType = (int)ActorTypes.Ground;
                    actor_name_map[GroundGeom] = pa;

//                    geom_name_map[GroundGeom] = "Terrain";

                    d.GeomSetPosition(GroundGeom, pOffset.X + m_regionWidth * 0.5f, pOffset.Y + m_regionHeight * 0.5f, 0.0f);
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
            return m_suportCombine;
        }

        public override void SetWaterLevel(float baseheight)
        {
            waterlevel = baseheight;
        }

        public override void Dispose()
        {
            lock (OdeLock)
            {

                if (world == IntPtr.Zero)
                    return;

                d.AllocateODEDataForThread(~0U);

                if (m_meshWorker != null)
                    m_meshWorker.Stop();

                if (m_rayCastManager != null)
                {
                    m_rayCastManager.Dispose();
                    m_rayCastManager = null;
                }

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

                if (ContactgeomsArray != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(ContactgeomsArray);
                    ContactgeomsArray = IntPtr.Zero;
                }
                if (GlobalContactsArray != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(GlobalContactsArray);
                    GlobalContactsArray = IntPtr.Zero;
                }

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
                req.actor = null;
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
                req.actor = null;
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
            req.actor = null;
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

        public override bool SupportsRaycastWorldFiltered()
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
            req.actor = null;
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
            req.actor = actor;
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
            req.actor = null;
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
            req.actor = null;
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
            req.actor = null;
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
