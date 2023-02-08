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


using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
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
        TargetVelocity,
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
        SetInertia,

        Null             //keep this last used do dim the methods array. does nothing but pulsing the prim
    }

    public struct ODEchangeitem
    {
        public PhysicsActor actor;
        public changes what;
        public Object arg;
    }

    public partial class ODEScene : PhysicsScene
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Scene m_frameWorkScene = null;

        //private int threadid = 0;

        const UBOdeNative.ContactFlags commomContactFlags = UBOdeNative.ContactFlags.Bounce | UBOdeNative.ContactFlags.Approx1;
        const float commomContactERP = 0.75f;
        const float commonContactCFM = 0.0001f;
        const float commomContactSLIP = 0f;

        readonly float TerrainBounce = 0.001f;
        readonly float TerrainFriction = 0.3f;

        public float AvatarFriction = 0;// 0.9f * 0.5f;

        // this netx dimensions are only relevant for terrain partition (mega regions)
        // WorldExtents below has the simulation dimensions
        // they should be identical except on mega regions
        private int m_regionWidth = (int)Constants.RegionSize;
        private int m_regionHeight = (int)Constants.RegionSize;

        private int m_heightmapWidthSamples = (int)Constants.RegionSize + 3;
        private int m_heightmapHeightSamples = (int)Constants.RegionSize + 3;

        public float ODE_STEPSIZE = 0.020f;
        public float HalfOdeStep = 0.01f;
        public int odetimestepMS = 20; // rounded
        private float m_timeDilation = 1.0f;

        private double m_lastframe;
        private double m_lastMeshExpire;

        public float gravityx = 0f;
        public float gravityy = 0f;
        public float gravityz = -9.8f;

        public float WaterLevel = 0f;
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

        public int bodyFramesAutoDisable = 10;

        private UBOdeNative.NearCallback NearCallback;

        private readonly Dictionary<uint, OdePrim> _prims = new();
        private readonly HashSet<OdeCharacter> _characters = new();
        private readonly HashSet<OdePrim> _activeprims = new();
        private readonly HashSet<OdePrim> _activegroups = new();
        public List<OdeCharacter> _charactersList;

        public readonly ConcurrentQueue<ODEchangeitem> ChangesQueue = new();

        /// <summary>
        /// A list of actors that should receive collision events.
        /// </summary>
        private readonly Dictionary<uint, PhysicsActor> _collisionEventPrim = new();
        private readonly List<PhysicsActor> _collisionEventPrimRemove = new();

        private readonly List<OdeCharacter> _badCharacter = new();
        public readonly Dictionary<IntPtr, PhysicsActor> actor_name_map = new();

        private readonly float contactsurfacelayer = 0.002f;

        private readonly int contactsPerCollision = 80;
        internal IntPtr ContactgeomsArray = IntPtr.Zero;
        internal UBOdeNative.ContactGeom[] m_contacts;
        internal GCHandle m_contactsHandler;

        private IntPtr GlobalContactsArray;

        private UBOdeNative.Contact contactSharedForJoints = new();

        const int maxContactJoints = 6000;
        private volatile int ContactJointCount = 0;

        private IntPtr JointContactGroup;

        public readonly ContactData[] m_materialContactsData = new ContactData[8];

        public IntPtr TerrainGeom;
        private float[] m_terrainHeights;
        private GCHandle m_terrainHeightsHandler = new();
        private IntPtr HeightmapData;
        private int m_lastRegionWidth;
        private int m_lastRegionHeight;

        private readonly int m_physicsiterations = 15;
        private const float m_SkipFramesAtms = 0.40f; // Drop frames gracefully at a 400 ms lag
        //private PhysicsActor PANull = new NullPhysicsActor();
        private float step_time = 0.0f;

        public IntPtr world;

        // split the spaces acording to contents type
        // ActiveSpace contains characters and active prims
        // StaticSpace contains land and other that is mostly static in enviroment
        // this can contain subspaces, like the grid in staticspace
        // as now space only contains this 2 top spaces

        public IntPtr TopSpace; // the global space
        public IntPtr ActiveSpace; // space for active prims
        public IntPtr StaticSpace; // space for the static things around

        public readonly object OdeLock = new();
        public static readonly object SimulationLock = new();
        public IMesher mesher;

        public IConfigSource m_config;

        public Vector2 WorldExtents = new((int)Constants.RegionSize, (int)Constants.RegionSize);

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

        public ODEScene(Scene pscene, IConfigSource psourceconfig, string pname, string pversion)
        {
            EngineType = pname;
            PhysicsSceneName = EngineType + "/" + pscene.RegionInfo.RegionName;
            EngineName = pname + " " + pversion;
            m_config = psourceconfig;

            m_frameWorkScene = pscene;

            m_frameWorkScene.RegisterModuleInterface<PhysicsScene>(this);

            Initialization();
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
            UBOdeNative.AllocateODEDataForThread(~0U);

            NearCallback = DefaultNearCallback;

            _charactersList = new List<OdeCharacter>(m_frameWorkScene.RegionInfo.AgentCapacity);

            WorldExtents.X = m_frameWorkScene.RegionInfo.RegionSizeX;
            m_regionWidth = (int)WorldExtents.X;
            WorldExtents.Y = m_frameWorkScene.RegionInfo.RegionSizeY;
            m_regionHeight = (int)WorldExtents.Y;

            lock (OdeLock)
            {
                // Create the world and the first space
                try
                {
                    world = UBOdeNative.WorldCreate();
                    TopSpace = UBOdeNative.SimpleSpaceCreate(IntPtr.Zero);
                    ActiveSpace = UBOdeNative.SimpleSpaceCreate(TopSpace);
                    float sx = m_regionWidth + 16;
                    float sy = m_regionHeight + 16;
                    UBOdeNative.Vector3 px = new(sx * 0.5f, sy  * 0.5f, 0);
                    if (sx < sy)
                        sx = sy;
                    int dp = Util.intLog2((uint)sx);
                    if(dp > 8)
                        dp = 8;
                    else if(dp < 4)
                        dp = 4;                  
                    StaticSpace = UBOdeNative.QuadTreeSpaceCreate(TopSpace, ref px, ref px, dp);
                }
                catch
                {
                }

                // move to high level
                UBOdeNative.SpaceSetSublevel(ActiveSpace, 1);
                UBOdeNative.SpaceSetSublevel(StaticSpace, 1);

                UBOdeNative.GeomSetCategoryBits(ActiveSpace, (uint)(CollisionCategories.Space |
                                                        CollisionCategories.Geom |
                                                        CollisionCategories.Character |
                                                        CollisionCategories.Phantom |
                                                        CollisionCategories.VolumeDtc
                                                        ));
                UBOdeNative.GeomSetCollideBits(ActiveSpace, (uint)(CollisionCategories.Space |
                                                        CollisionCategories.Geom |
                                                        CollisionCategories.Character |
                                                        CollisionCategories.Phantom |
                                                        CollisionCategories.VolumeDtc
                                                        ));

                UBOdeNative.GeomSetCategoryBits(StaticSpace, (uint)(CollisionCategories.Space |
                                                        CollisionCategories.Geom |
                                                        //CollisionCategories.Land |
                                                        //CollisionCategories.Water |
                                                        CollisionCategories.Phantom |
                                                        CollisionCategories.VolumeDtc
                                                        ));
                UBOdeNative.GeomSetCollideBits(StaticSpace, 0);

                JointContactGroup = UBOdeNative.JointGroupCreate(maxContactJoints + 1);
                //contactgroup

                UBOdeNative.WorldSetAutoDisableFlag(world, false);
            }


            //checkThread();


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

                    //contactsurfacelayer = physicsconfig.GetFloat("world_contact_surface_layer", contactsurfacelayer);

                    ODE_STEPSIZE = physicsconfig.GetFloat("world_stepsize", ODE_STEPSIZE);

                    avDensity = physicsconfig.GetFloat("av_density", avDensity);
                    avMovementDivisorWalk = physicsconfig.GetFloat("av_movement_divisor_walk", avMovementDivisorWalk);
                    avMovementDivisorRun = physicsconfig.GetFloat("av_movement_divisor_run", avMovementDivisorRun);

                    contactsPerCollision = physicsconfig.GetInt("contacts_per_collision", contactsPerCollision);

                    geomDefaultDensity = physicsconfig.GetFloat("geometry_default_density", geomDefaultDensity);
//                    bodyFramesAutoDisable = physicsconfig.GetInt("body_frames_auto_disable", bodyFramesAutoDisable);

                    minimumGroundFlightOffset = physicsconfig.GetFloat("minimum_ground_flight_offset", minimumGroundFlightOffset);
                    maximumMassObject = physicsconfig.GetFloat("maximum_mass_object", maximumMassObject);

                    avDensity *= 3f / 80f;  // scale other engines density option to this
                }
            }

            float heartbeat = 1/m_frameWorkScene.FrameTime;
            maximumAngularVelocity = 0.49f * heartbeat *(float)Math.PI;
            maxAngVelocitySQ = maximumAngularVelocity * maximumAngularVelocity;

            UBOdeNative.WorldSetCFM(world, commonContactCFM);
            UBOdeNative.WorldSetERP(world, commomContactERP);

            UBOdeNative.WorldSetGravity(world, gravityx, gravityy, gravityz);

            UBOdeNative.WorldSetLinearDamping(world, 0.001f);
            UBOdeNative.WorldSetAngularDamping(world, 0.002f);
            UBOdeNative.WorldSetAngularDampingThreshold(world, 0f);
            UBOdeNative.WorldSetLinearDampingThreshold(world, 0f);
            UBOdeNative.WorldSetMaxAngularSpeed(world, maximumAngularVelocity);

            UBOdeNative.WorldSetQuickStepNumIterations(world, m_physicsiterations);

            UBOdeNative.WorldSetContactSurfaceLayer(world, contactsurfacelayer);
            UBOdeNative.WorldSetContactMaxCorrectingVel(world, 60.0f);

            HalfOdeStep = ODE_STEPSIZE * 0.5f;
            odetimestepMS = (int)(1000.0f * ODE_STEPSIZE + 0.5f);

            m_contacts = new UBOdeNative.ContactGeom[contactsPerCollision];
            m_contactsHandler = GCHandle.Alloc(m_contacts, GCHandleType.Pinned);
            ContactgeomsArray = m_contactsHandler.AddrOfPinnedObject();

            GlobalContactsArray = Marshal.AllocHGlobal((maxContactJoints + 100) * UBOdeNative.SizeOfContact);

            contactSharedForJoints.geom.g1 = IntPtr.Zero;
            contactSharedForJoints.geom.g2 = IntPtr.Zero;

            contactSharedForJoints.geom.side1 = -1;
            contactSharedForJoints.geom.side2 = -1;

            contactSharedForJoints.surface.mode = commomContactFlags;
            contactSharedForJoints.surface.mu = 0;
            contactSharedForJoints.surface.bounce = 0;
            contactSharedForJoints.surface.bounce_vel = 1.5f;
            contactSharedForJoints.surface.soft_cfm = commonContactCFM;
            contactSharedForJoints.surface.soft_erp = commomContactERP;
            contactSharedForJoints.surface.slip1 = commomContactSLIP;
            contactSharedForJoints.surface.slip2 = commomContactSLIP;

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

            m_lastframe = Util.GetTimeStamp();
            m_lastMeshExpire = m_lastframe;
            step_time = -1;

            m_rayCastManager = new ODERayCastRequestManager(this);

            base.Initialise(m_frameWorkScene.PhysicsRequestAsset,
                (m_frameWorkScene.Heightmap != null ? m_frameWorkScene.Heightmap.GetFloatsSerialised() : new float[m_frameWorkScene.RegionInfo.RegionSizeX * m_frameWorkScene.RegionInfo.RegionSizeY]),
                (float)m_frameWorkScene.RegionInfo.RegionSettings.WaterHeight);
        }

        /*
        internal void waitForSpaceUnlock(IntPtr space)
        {
            //if (space != IntPtr.Zero)
                //while (d.SpaceLockQuery(space)) { } // Wait and do nothing
        }
        */

        #region Collision Detection
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IntPtr CreateContacJoint(ref UBOdeNative.ContactGeom contactGeom, bool smooth)
        {
            if (ContactJointCount >= maxContactJoints)
                return IntPtr.Zero;

            ContactJointCount++;
            contactSharedForJoints.geom.depth = smooth ? contactGeom.depth * 0.05f : contactGeom.depth;
            contactSharedForJoints.geom.pos = contactGeom.pos;
            contactSharedForJoints.geom.normal = contactGeom.normal;

            IntPtr contact = new(GlobalContactsArray.ToInt64() + (Int64)(ContactJointCount * UBOdeNative.SizeOfContact));
            Marshal.StructureToPtr(contactSharedForJoints, contact, false);
            return UBOdeNative.JointCreateContactPtr(world, JointContactGroup, contact);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IntPtr CreateCharContacJoint()
        {
            ContactJointCount++;

            IntPtr contact = new(GlobalContactsArray.ToInt64() + (Int64)(ContactJointCount * UBOdeNative.SizeOfContact));
            Marshal.StructureToPtr(contactSharedForJoints, contact, false);
            return UBOdeNative.JointCreateContactPtr(world, JointContactGroup, contact);
        }

        UBOdeNative.ContactGeom altWorkContact = new();

        /// <summary>
        /// This is our near callback.  A geometry is near a body
        /// </summary>
        /// <param name="space">The space that contains the geoms.  Remember, spaces are also geoms</param>
        /// <param name="g1">a geometry or space</param>
        /// <param name="g2">another geometry or space</param>
        ///
        private void DefaultNearCallback(IntPtr space, IntPtr g1, IntPtr g2)
        {
            //  no lock here!  It's invoked from within Simulate(), which is thread-locked
            if (ContactJointCount >= maxContactJoints)
                return;

            // Test if we're colliding a geom with a space.
            // If so we have to drill down into the space recursively
            if (g1 == IntPtr.Zero || g2 == IntPtr.Zero)
                return;

            if (UBOdeNative.GeomIsSpace(g1) || UBOdeNative.GeomIsSpace(g2))
            {
                // We'll be calling near recursivly if one
                // of them is a space to find all of the
                // contact points in the space
                try
                {
                    UBOdeNative.SpaceCollide2(g1, g2, IntPtr.Zero, DefaultNearCallback);
                }
                catch (AccessViolationException)
                {
                    m_log.Warn("[PHYSICS]: Unable to collide test a space");
                }
                return;
            }

            if (g1 == g2)
                return; // Can't collide with yourself

            // Figure out how many contact points we have
            int count = 0;
            try
            {
                if (UBOdeNative.GeomGetCategoryBits(g1) == (uint)CollisionCategories.VolumeDtc ||
                    UBOdeNative.GeomGetCategoryBits(g2) == (uint)CollisionCategories.VolumeDtc)
                {
                    int cflags = unchecked((int)(1 | UBOdeNative.CONTACTS_UNIMPORTANT));
                    count = UBOdeNative.CollidePtr(g1, g2, cflags, ContactgeomsArray, UBOdeNative.SizeOfContactGeom);
                }
                else
                    count = UBOdeNative.CollidePtr(g1, g2, contactsPerCollision, ContactgeomsArray, UBOdeNative.SizeOfContactGeom);
            }
            catch (SEHException)
            {
                m_log.Error("[PHYSICS]: The Operating system shut down ODE because of corrupt memory.  This could be a result of really irregular terrain.  If this repeats continuously, restart using Basic Physics and terrain fill your terrain.  Restarting the sim.");
                //ode.drelease(world);
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
            if (!actor_name_map.TryGetValue(g1, out PhysicsActor p1))
            {
                m_log.WarnFormat("[PHYSICS]: failed actor mapping for geom 1");
                return;
            }

            if (!actor_name_map.TryGetValue(g2, out PhysicsActor p2))
            {
                m_log.WarnFormat("[PHYSICS]: failed actor mapping for geom 2");
                return;
            }

            // do volume detection case
            if ((p1.IsVolumeDtc || p2.IsVolumeDtc))
            {
                ref UBOdeNative.ContactGeom curctc0 = ref m_contacts[0];
                ContactPoint volDepthContact = new(
                    new Vector3(curctc0.pos.X, curctc0.pos.Y, curctc0.pos.Z),
                    new Vector3(curctc0.normal.X, curctc0.normal.Y, curctc0.normal.Z),
                    curctc0.depth, false
                    );

                Collision_accounting_events(p1, p2, ref volDepthContact);
                return;
            }

            // big messy collision analises
            float mu = 0;
            float bounce = 0;
            //bool IgnoreNegSides = false;

            ContactData contactdata1 = new(0, 0, false);
            ContactData contactdata2 = new(0, 0, false);

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
                                //Vector3 relV = p1.rootVelocity - p2.rootVelocity;
                                //float relVlenSQ = relV.LengthSquared();
                                //if (relVlenSQ > 0.0001f)
                                {
                                    p1.CollidingObj = true;
                                    p2.CollidingObj = true;
                                }
                                p1.getContactData(ref contactdata1);
                                p2.getContactData(ref contactdata2);
                                bounce = contactdata1.bounce * contactdata2.bounce;
                                mu = (float)Math.Sqrt(contactdata1.mu * contactdata2.mu);

                                //if (relVlenSQ > 0.01f)
                                //    mu *= frictionMovementMult;

                                if (UBOdeNative.GeomGetClass(g2) == UBOdeNative.GeomClassID.TriMeshClass &&
                                    UBOdeNative.GeomGetClass(g1) == UBOdeNative.GeomClassID.TriMeshClass)
                                    smoothMesh = true;
                                break;

                            case (int)ActorTypes.Ground:
                                p1.getContactData(ref contactdata1);
                                bounce = contactdata1.bounce * TerrainBounce;
                                mu = (float)Math.Sqrt(contactdata1.mu * TerrainFriction);

                                //Vector3 v1 = p1.rootVelocity;
                                //if (Math.Abs(v1.X) > 0.1f || Math.Abs(v1.Y) > 0.1f)
                                //    mu *= frictionMovementMult;
                                p1.CollidingGround = true;

                                if (UBOdeNative.GeomGetClass(g1) == UBOdeNative.GeomClassID.TriMeshClass)
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

                        //if (curContact.side1 > 0) // should be 2 ?
                        //    IgnoreNegSides = true;
                        //Vector3 v2 = p2.rootVelocity;
                        //if (Math.Abs(v2.X) > 0.1f || Math.Abs(v2.Y) > 0.1f)
                        //    mu *= frictionMovementMult;

                        if (UBOdeNative.GeomGetClass(g2) == UBOdeNative.GeomClassID.TriMeshClass)
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

            ContactPoint maxDepthContact = new();

            float minDepth = float.MaxValue;
            float maxDepth = float.MinValue;

            contactSharedForJoints.surface.mu = mu;
            contactSharedForJoints.surface.bounce = bounce;

            bool useAltcontact;
            bool noskip;

            if (dop1ava || dop2ava)
                smoothMesh = false;

            IntPtr b1 = UBOdeNative.GeomGetBody(g1);
            IntPtr b2 = UBOdeNative.GeomGetBody(g2);

            for (int i = 0; i < count; ++i)
            {
                ref UBOdeNative.ContactGeom curctc = ref m_contacts[i];
                noskip = true;
                useAltcontact = false;

                if (dop1ava)
                {
                    if ((((OdeCharacter)p1).Collide(g2, false, ref curctc, ref altWorkContact, ref useAltcontact, ref FeetCollision)))
                    {
                        if (p2.PhysicsActorType == (int)ActorTypes.Agent)
                        {
                            p1.CollidingObj = true;
                            p2.CollidingObj = true;
                        }
                        else if (p2.rootVelocity.LengthSquared() > 0.0f)
                            p2.CollidingObj = true;
                    }
                    else
                        noskip = false;
                }
                else if (dop2ava)
                {
                    if ((((OdeCharacter)p2).Collide(g1, true, ref curctc, ref altWorkContact, ref useAltcontact, ref FeetCollision)))
                    {
                        if (p1.PhysicsActorType == (int)ActorTypes.Agent)
                        {
                            p1.CollidingObj = true;
                            p2.CollidingObj = true;
                        }
                        else if (p1.rootVelocity.LengthSquared() > 0.0f)
                            p1.CollidingObj = true;
                    }
                    else
                        noskip = false;
                }

                if (noskip)
                {
                    Joint = useAltcontact ? 
                                CreateContacJoint(ref altWorkContact, smoothMesh) : 
                                CreateContacJoint(ref curctc, smoothMesh);
                    if (Joint == IntPtr.Zero)
                        break;

                    UBOdeNative.JointAttach(Joint, b1, b2);

                    ncontacts++;

                    if (curctc.depth > maxDepth)
                    {
                        maxDepth = curctc.depth;
                        maxDepthContact.PenetrationDepth = maxDepth;
                        maxDepthContact.Position = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curctc.pos);
                        maxDepthContact.CharacterFeet = FeetCollision;
                    }

                    if (curctc.depth < minDepth)
                    {
                        minDepth = curctc.depth;
                        maxDepthContact.SurfaceNormal = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curctc.normal);
                    }
                }
            }

            if (ncontacts > 0)
            {
                Collision_accounting_events(p1, p2, ref maxDepthContact);
            }
        }

        private void CharPrimNearCallback(IntPtr space, IntPtr g1, IntPtr g2)
        {
            //  no lock here!  It's invoked from within Simulate(), which is thread-locked
            if (ContactJointCount >= maxContactJoints)
                return;

            // Test if we're colliding a geom with a space.
            // If so we have to drill down into the space recursively
            if (g1 == IntPtr.Zero || g2 == IntPtr.Zero)
                return;

            //if (SafeNativeMethods.GeomIsSpace(g1) || SafeNativeMethods.GeomIsSpace(g2))
            if (UBOdeNative.GeomIsSpace(g2))
            {
                // We'll be calling near recursivly if one
                // of them is a space to find all of the
                // contact points in the space
                try
                {
                    UBOdeNative.SpaceCollide2(g1, g2, IntPtr.Zero, CharPrimNearCallback);
                }
                catch (AccessViolationException)
                {
                    m_log.Warn("[PHYSICS]: Unable to collide test a space");
                }
                return;
            }

            // Figure out how many contact points we have
            int count = 0;
            try
            {
                if (UBOdeNative.GeomGetCategoryBits(g2) == (uint)CollisionCategories.VolumeDtc)
                {
                    int cflags = unchecked((int)(1 | UBOdeNative.CONTACTS_UNIMPORTANT));
                    count = UBOdeNative.CollidePtr(g1, g2, cflags, ContactgeomsArray, UBOdeNative.SizeOfContactGeom);
                }
                else
                    count = UBOdeNative.CollidePtr(g1, g2, contactsPerCollision, ContactgeomsArray, UBOdeNative.SizeOfContactGeom);
            }
            catch (SEHException)
            {
                m_log.Error("[PHYSICS]: The Operating system shut down ODE because of corrupt memory.  This could be a result of really irregular terrain.  If this repeats continuously, restart using Basic Physics and terrain fill your terrain.  Restarting the sim.");
                //ode.drelease(world);
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
            if (!actor_name_map.TryGetValue(g1, out PhysicsActor p1))
            {
                m_log.WarnFormat("[PHYSICS]: failed actor mapping for geom 1");
                return;
            }

            if (!actor_name_map.TryGetValue(g2, out PhysicsActor p2))
            {
                m_log.WarnFormat("[PHYSICS]: failed actor mapping for geom 2");
                return;
            }

            // do volume detection case
            if (p2.IsVolumeDtc)
            {
                ref UBOdeNative.ContactGeom curctc0 = ref m_contacts[0];
                ContactPoint volDepthContact = new(
                    new Vector3(curctc0.pos.X, curctc0.pos.Y, curctc0.pos.Z),
                    new Vector3(curctc0.normal.X, curctc0.normal.Y, curctc0.normal.Z),
                    curctc0.depth, false
                    );

                Collision_accounting_events(p1, p2, ref volDepthContact);
                return;
            }

            if(p2.PhysicsActorType != (int)ActorTypes.Prim)
                    return;

            IntPtr Joint;
            bool FeetCollision = false;
            int ncontacts = 0;

            ContactPoint accountContact = new();

            float minDepth = float.MaxValue;
            float maxDepth = float.MinValue;

            contactSharedForJoints.surface.mu = 0;
            contactSharedForJoints.surface.bounce = 0;

            bool useAltcontact;

            IntPtr b1 = UBOdeNative.GeomGetBody(g1);
            IntPtr b2 = UBOdeNative.GeomGetBody(g2);

            for (int i = 0; i < count; ++i)
            {
                ref UBOdeNative.ContactGeom curctc = ref m_contacts[i];
                useAltcontact = false;
                 
                if ((((OdeCharacter)p1).Collide(g2, false, ref curctc, ref altWorkContact, ref useAltcontact, ref FeetCollision)))
                {
                    if(p2.rootVelocity.LengthSquared() > 0.0f)
                        p2.CollidingObj = true;
 
                    Joint = useAltcontact ?
                                CreateContacJoint(ref altWorkContact, false) :
                                CreateContacJoint(ref curctc, false);
                    if (Joint == IntPtr.Zero)
                        break;

                    UBOdeNative.JointAttach(Joint, b1, b2);

                    ncontacts++;

                    if (curctc.depth > maxDepth)
                    {
                        maxDepth = curctc.depth;
                        accountContact.PenetrationDepth = maxDepth;
                        accountContact.Position = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curctc.pos);
                        accountContact.CharacterFeet = FeetCollision;
                    }

                    if (curctc.depth < minDepth)
                    {
                        minDepth = curctc.depth;
                        accountContact.SurfaceNormal = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curctc.normal);
                    }
                }
            }

            if (ncontacts > 0)
            {
                Collision_accounting_events(p1, p2, ref accountContact);
            }
        }

        private static void Collision_accounting_events(PhysicsActor p1, PhysicsActor p2, ref ContactPoint contact)
        {

            // update actors collision score
            if (p1.CollisionScore < float.MaxValue)
                p1.CollisionScore += 1.0f;
            if (p2.CollisionScore < float.MaxValue)
                p2.CollisionScore += 1.0f;

            bool p1events = p1.SubscribedEvents();
            bool p2events = p2.SubscribedEvents();

            if (p1.IsVolumeDtc)
                p2events = false;
            if (p2.IsVolumeDtc)
                p1events = false;

            if (!p2events && !p1events)
                return;

            Vector3 vel = Vector3.Zero;
            if (p2.IsPhysical)
                vel = p2.rootVelocity;

            if (p1.IsPhysical)
                vel -= p1.rootVelocity;

            contact.RelativeSpeed = Vector3.Dot(vel, contact.SurfaceNormal);

            uint obj2LocalID;

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
                                //AddCollisionEventReporting(p2);
                                p2.AddCollisionEvent(p1.ParentActor.m_baseLocalID, contact);
                            }
                            else if(p1.IsVolumeDtc)
                                p2.AddVDTCCollisionEvent(p1.ParentActor.m_baseLocalID, contact);

                            obj2LocalID = p2.ParentActor.m_baseLocalID;
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
                        contact.RelativeSpeed = -contact.RelativeSpeed;
                        //AddCollisionEventReporting(p1);
                        p1.AddCollisionEvent(obj2LocalID, contact);
                    }
                    else if(p2.IsVolumeDtc)
                    {
                        contact.SurfaceNormal = -contact.SurfaceNormal;
                        contact.RelativeSpeed = -contact.RelativeSpeed;
                        //AddCollisionEventReporting(p1);
                        p1.AddVDTCCollisionEvent(obj2LocalID, contact);
                    }
                    break;
                }
                case ActorTypes.Ground:
                case ActorTypes.Unknown:
                default:
                {
                    if (p2events && !p2.IsVolumeDtc)
                    {
                        //AddCollisionEventReporting(p2);
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
                if (_charactersList.Count > 0)
                {
                    try
                    {
                        Span<OdeCharacter> charsSpan = CollectionsMarshal.AsSpan(_charactersList);
                        if (charsSpan.Length == 1)
                        {
                            OdeCharacter chr = charsSpan[0];
                            if (chr.Colliderfilter < -1)
                                chr.Colliderfilter = -1;
                            else
                            {
                                chr.IsColliding = false;
                                chr.CollidingObj = false;

                                UBOdeNative.SpaceCollide2(chr.collider, StaticSpace, IntPtr.Zero, CharPrimNearCallback);
                                UBOdeNative.SpaceCollide2(chr.collider, ActiveSpace, IntPtr.Zero, CharPrimNearCallback);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < charsSpan.Length; ++i)
                            {
                                OdeCharacter chr = charsSpan[i];

                                if (chr.Colliderfilter < -1)
                                    chr.Colliderfilter = -1;
                                else
                                {
                                    chr.IsColliding = false;
                                    chr.CollidingObj = false;

                                    // do colisions with static space
                                    UBOdeNative.SpaceCollide2(chr.collider, StaticSpace, IntPtr.Zero, CharPrimNearCallback);
                                    UBOdeNative.SpaceCollide2(chr.collider, ActiveSpace, IntPtr.Zero, CharPrimNearCallback);

                                    float mx = chr._AABB2D.minx;
                                    float Mx = chr._AABB2D.maxx;
                                    float my = chr._AABB2D.miny;
                                    float My = chr._AABB2D.maxy;

                                    for (int j = i + 1 ; j < charsSpan.Length; ++j)
                                    {
                                        OdeCharacter chr2 = charsSpan[j];
                                        if (chr2.Colliderfilter < -1)
                                            continue;

                                        if(Mx < chr2._AABB2D.minx ||
                                           mx > chr2._AABB2D.maxx ||
                                           My < chr2._AABB2D.miny ||
                                           my > chr2._AABB2D.maxy)
                                            continue;
 
                                        CollideCharChar(chr, chr2);
                                    }
                                }
                            }
                        }

                        // chars with chars
                        //SafeNativeMethods.SpaceCollide(CharsSpace, IntPtr.Zero, nearCallback);
                    }
                    catch (AccessViolationException)
                    {
                        m_log.Warn("[PHYSICS]: Unable to collide Character to static space");
                    }
                }
            }

            if(_activeprims.Count > 0)
            {
                lock (_activeprims)
                {
                    foreach (OdePrim aprim in _activeprims)
                    {
                        aprim.CollisionScore = 0;
                        aprim.IsColliding = false;
                        if(!aprim.m_outbounds && UBOdeNative.BodyIsEnabled(aprim.Body))
                            aprim.clearSleeperCollisions();
                    }
                }

                lock (_activegroups)
                {
                    try
                    {
                        foreach (OdePrim aprim in _activegroups)
                        {
                            if(!aprim.m_outbounds && UBOdeNative.BodyIsEnabled(aprim.Body) &&
                                    aprim.m_collide_geom != IntPtr.Zero)
                            {
                                UBOdeNative.SpaceCollide2(StaticSpace, aprim.m_collide_geom, IntPtr.Zero, NearCallback);
                                UBOdeNative.SpaceCollide2(TerrainGeom, aprim.m_collide_geom, IntPtr.Zero, NearCallback);
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
                    UBOdeNative.SpaceCollide(ActiveSpace, IntPtr.Zero, NearCallback);
                }
                catch (Exception e)
                {
                        m_log.Warn("[PHYSICS]: Unable to collide in Active: " + e.Message);
                }
            }
            /*
            // and with chars
            try
            {
                SafeNativeMethods.SpaceCollide2(CharsSpace,ActiveSpace, IntPtr.Zero, nearCallback);
            }
            catch (Exception e)
            {
                    m_log.Warn("[PHYSICS]: Unable to collide Active to Character: " + e.Message);
            }
            */
        }

        #endregion
        /// <summary>
        /// Add actor to the list that should receive collision events in the simulate loop.
        /// </summary>
        /// <param name="obj"></param>
        public void AddCollisionEventReporting(PhysicsActor obj)
        {
            _collisionEventPrim[obj.LocalID] = obj;
        }

        /// <summary>
        /// Remove actor from the list that should receive collision events in the simulate loop.
        /// </summary>
        /// <param name="obj"></param>
        public void RemoveCollisionEventReporting(PhysicsActor obj)
        {
            lock(_collisionEventPrimRemove)
            {
                _collisionEventPrimRemove.Add(obj);
            }
        }

        public override float TimeDilation
        {
            get { return m_timeDilation; }
        }

        #region Add/Remove Entities

        public override PhysicsActor AddAvatar(string avName, Vector3 position, Vector3 velocity, Vector3 size, bool isFlying)
        {
            return null;
        }

        public override PhysicsActor AddAvatar(uint localID, string avName, Vector3 position, Vector3 size, float feetOffset, bool isFlying)
        {
            OdeCharacter newAv = new(localID, avName, this, position,
                size, feetOffset, avDensity, avMovementDivisorWalk, avMovementDivisorRun)
            {
                Flying = isFlying,
                MinimumGroundFlightOffset = minimumGroundFlightOffset
            };

            return newAv;
        }

        public void AddCharacter(OdeCharacter chr)
        {
            lock (_characters)
            {
                if (_characters.Add(chr))
                {
                    chr._charsListIndex = _charactersList.Count;
                    _charactersList.Add(chr);
                }
                else
                    chr._charsListIndex = -1;
                if (chr.bad)
                    m_log.DebugFormat("[PHYSICS] Added BAD actor {0} to characters list", chr.m_baseLocalID);
            }
        }

        public void RemoveCharacter(OdeCharacter chr)
        {
            lock (_characters)
            {
                if (_characters.Remove(chr))
                {
                    if (chr._charsListIndex >= 0)
                    {
                        int last = _charactersList.Count - 1;
                        if(chr._charsListIndex != last)
                        {
                            _charactersList[chr._charsListIndex] = _charactersList[last];
                            _charactersList[chr._charsListIndex]._charsListIndex = chr._charsListIndex;
                        }
                        _charactersList.RemoveAt(last);
                        chr._charsListIndex = -1;
                    }
                }
            }
        }

        public void BadCharacter(OdeCharacter chr)
        {
            lock (_badCharacter)
            {
                _badCharacter.Add(chr);
            }
        }

        public override void RemoveAvatar(PhysicsActor actor)
        {
            //m_log.Debug("[PHYSICS]:ODELOCK");
            if (world == IntPtr.Zero)
                return;
            ((OdeCharacter) actor).Destroy();
        }

        public void addActivePrim(OdePrim activatePrim)
        {
            // adds active prim..
            lock (_activeprims)
            {
                _activeprims.Add(activatePrim);
            }
        }

        public void addActiveGroups(OdePrim activatePrim)
        {
            lock (_activegroups)
            {
                _activegroups.Add(activatePrim);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PhysicsActor AddPrim(String name, Vector3 position, Vector3 size, Quaternion rotation,
                                     PrimitiveBaseShape pbs, bool isphysical, bool isPhantom, byte shapeType, uint localID)
        {
            OdePrim newPrim;
            lock (OdeLock)
            {
                newPrim = new OdePrim(name, this, position, size, rotation, pbs, isphysical, isPhantom, shapeType, localID);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void remActivePrim(OdePrim deactivatePrim)
        {
            lock (_activeprims)
            {
                _activeprims.Remove(deactivatePrim);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void remActiveGroup(OdePrim deactivatePrim)
        {
            lock (_activegroups)
            {
                _activegroups.Remove(deactivatePrim);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void RemovePrim(PhysicsActor prim)
        {
            // As with all ODE physics operations, we don't remove the prim immediately but signal that it should be
            // removed in the next physics simulate pass.
            if (prim is OdePrim p)
            {
                p.setPrimForRemoval();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemovePrimThreadLocked(OdePrim prim)
        {
            //Console.WriteLine("RemovePrimThreadLocked " +  prim.m_primName);
            lock (_prims)
                _prims.Remove(prim.m_baseLocalID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void addToPrims(OdePrim prim)
        {
            lock (_prims)
            {
                _prims[prim.m_baseLocalID] = prim;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OdePrim getPrim(uint id)
        {
            lock (_prims)
            {
                if(_prims.TryGetValue(id, out OdePrim p))
                    return p;
                else
                    return null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool havePrim(OdePrim prim)
        {
            lock (_prims)
                return _prims.ContainsKey(prim.m_baseLocalID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void changePrimID(OdePrim prim,uint oldID)
        {
            lock (_prims)
            {
                _prims.Remove(oldID);
                _prims[prim.m_baseLocalID] = prim;
            }
        }

        public bool haveActor(PhysicsActor actor)
        {
            if (actor is OdePrim prim)
            {
                lock (_prims)
                    return _prims.ContainsKey(prim.m_baseLocalID);
            }
            else if (actor is OdeCharacter ch)
            {
                lock (_characters)
                    return _characters.Contains(ch);
            }
            return false;
        }

        #endregion

        #region Space Separation Calculation

        /// <summary>
        /// Called when a static prim moves or becomes static
        /// Places the prim in a space one the static space
        /// </summary>
        /// <param name="geom">the pointer to the geom that moved</param>
        /// <param name="currentspace">a pointer to the space it was in before it was moved.</param>
        /// <returns>a pointer to the new space it's in</returns>
        public IntPtr MoveGeomToStaticSpace(IntPtr geom, IntPtr currentspace)
        {
            // moves a prim into static sub-space

            // Called ODEPrim so
            // it's already in locked space.

            if (geom == IntPtr.Zero) // shouldn't happen
                return IntPtr.Zero;
            
            if (StaticSpace == currentspace) // if we are there all done
                return StaticSpace;

            // else remove it from its current space
            if (currentspace != IntPtr.Zero && UBOdeNative.SpaceQuery(currentspace, geom))
            {
                if (UBOdeNative.GeomIsSpace(currentspace))
                {
                    //waitForSpaceUnlock(currentspace);
                    UBOdeNative.SpaceRemove(currentspace, geom);

                    if (UBOdeNative.SpaceGetSublevel(currentspace) == 0 && UBOdeNative.SpaceGetNumGeoms(currentspace) == 0)
                    {
                        UBOdeNative.SpaceDestroy(currentspace);
                    }
                }
                else
                {
                    m_log.Info("[Physics]: Invalid or empty Space passed to 'MoveGeomToStaticSpace':" + currentspace +
                                   " Geom:" + geom);
                }
            }
            else
            {
                currentspace = UBOdeNative.GeomGetSpace(geom);
                if (currentspace != IntPtr.Zero)
                {
                    if (UBOdeNative.GeomIsSpace(currentspace))
                    {
                        //waitForSpaceUnlock(currentspace);
                        UBOdeNative.SpaceRemove(currentspace, geom);

                        if (UBOdeNative.SpaceGetSublevel(currentspace) == 0 && UBOdeNative.SpaceGetNumGeoms(currentspace) == 0)
                        {
                            UBOdeNative.SpaceDestroy(currentspace);
                        }
                    }
                }
            }

            // put the geom in the newspace
            //waitForSpaceUnlock(StaticSpace);
            if(UBOdeNative.SpaceQuery(StaticSpace, geom))
                m_log.Info("[Physics]: 'MoveGeomToStaticSpace' geom already in static space:" + geom);
            else
                UBOdeNative.SpaceAdd(StaticSpace, geom);

            return StaticSpace;
        }
        #endregion


        /// <summary>
        /// Called to queue a change to a actor
        /// to use in place of old taint mechanism so changes do have a time sequence
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddChange(PhysicsActor _actor, changes _what, Object _arg)
        {
            if (world == IntPtr.Zero)
                return;
            ODEchangeitem item = new()
            { 
                actor = _actor,
                what = _what,
                arg = _arg
            };
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
                    return;

                int donechanges = 0;
                if (!ChangesQueue.IsEmpty)
                {
                    m_log.InfoFormat("[ubOde] start processing pending actor operations");
                    int tstart = Util.EnvironmentTickCount();

                    UBOdeNative.AllocateODEDataForThread(~0U);

                    while (ChangesQueue.TryDequeue(out ODEchangeitem item))
                    {
                        if (item.actor != null)
                        {
                            try
                            {
                                lock (SimulationLock)
                                {
                                    if (item.actor is OdeCharacter character)
                                        character.DoAChange(item.what, item.arg);
                                    else if (((OdePrim)item.actor).DoAChange(item.what, item.arg))
                                        RemovePrimThreadLocked((OdePrim)item.actor);
                                }
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
            m_lastframe = Util.GetTimeStamp() + 0.5;
            step_time = -0.5f;
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
            if (world == IntPtr.Zero)
                return 0;

            double now = Util.GetTimeStamp();
            double timeStep = now - m_lastframe;
            m_lastframe = now;

            // acumulate time so we can reduce error
            step_time += (float)timeStep;

            if (step_time < HalfOdeStep)
                return 0;

            if (framecount <= 0)
                framecount = 1;
            else
                framecount++;

            //checkThread();
            int nodeframes = 0;
            float fps = 0;

            lock (OdeLock)
            {

                //d.WorldSetQuickStepNumIterations(world, curphysiteractions);

                double loopstartMS = Util.GetTimeStampMS();
                double maxChangestime = (int)(reqTimeStep * 500f) + loopstartMS; // half the time
                double maxLoopTime = (int)(reqTimeStep * 1200f) + loopstartMS; // 1.2 the time


                //double collisionTime = 0;
                //double qstepTIme = 0;
                //double tmpTime = 0;
                //double changestot = 0;
                //double collisonRepo = 0;
                //double updatesTime = 0;
                //double moveTime = 0;
                //double rayTime = 0;


                UBOdeNative.AllocateODEDataForThread(~0U);

                while (ChangesQueue.TryDequeue(out ODEchangeitem item))
                {
                    try
                    {
                        lock (SimulationLock)
                        {
                            if (item.actor is OdeCharacter ch)
                                ch.DoAChange(item.what, item.arg);
                            else if (((OdePrim)item.actor).DoAChange(item.what, item.arg))
                                RemovePrimThreadLocked((OdePrim)item.actor);
                        }
                    }
                    catch
                    {
                        m_log.WarnFormat("[PHYSICS]: doChange failed for a actor {0} {1}",
                            item.actor.Name, item.what.ToString());
                    }
                    if (maxChangestime < Util.GetTimeStampMS())
                        break;
                }

                // do simulation taking at most 150ms total time including changes
                while (step_time > HalfOdeStep)
                {
                    try
                    {
                        // clear pointer/counter to contacts to pass into joints
                        ContactJointCount = 0;

                        //tmpTime =  Util.GetTimeStampMS();

                        // Move characters
                        lock (_characters)
                        {
                            foreach (OdeCharacter actor in _characters)
                            {
                                    actor.Move();
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
                        //moveTime += Util.GetTimeStampMS() - tmpTime;
                        //tmpTime =  Util.GetTimeStampMS();
                        lock (SimulationLock)
                        {
                            m_rayCastManager.ProcessQueuedRequests();

                            //rayTime += Util.GetTimeStampMS() - tmpTime;
                            //tmpTime =  Util.GetTimeStampMS();

                            collision_optimized();
                        }

                        //collisionTime += Util.GetTimeStampMS() - tmpTime;
                        //tmpTime =  Util.GetTimeStampMS();

                        lock (_collisionEventPrimRemove)
                        {
                            foreach (PhysicsActor obj in CollectionsMarshal.AsSpan(_collisionEventPrimRemove))
                                _collisionEventPrim.Remove(obj.LocalID);

                            _collisionEventPrimRemove.Clear();
                        }

                        List<OdePrim> sleepers = new();
                        foreach (PhysicsActor obj in _collisionEventPrim.Values)
                        {
                            switch ((ActorTypes)obj.PhysicsActorType)
                            {
                                case ActorTypes.Agent:
                                    OdeCharacter cobj = (OdeCharacter)obj;
                                    cobj.SendCollisions(odetimestepMS);
                                    break;

                                case ActorTypes.Prim:
                                    OdePrim pobj = (OdePrim)obj;
                                    if (!pobj.m_outbounds)
                                    {
                                        pobj.SendCollisions(odetimestepMS);
                                        lock(SimulationLock)
                                        {
                                            if(pobj.Body != IntPtr.Zero && !pobj.m_isSelected &&
                                                !pobj.m_disabled && !pobj.m_building &&
                                                !UBOdeNative.BodyIsEnabled(pobj.Body))
                                            sleepers.Add(pobj);
                                        }
                                    }
                                    break;
                            }
                        }

                        foreach(OdePrim prm in sleepers)
                            prm.SleeperAddCollisionEvents();
                        sleepers.Clear();

                        //collisonRepo += Util.GetTimeStampMS() - tmpTime;
                        //tmpTime = Util.GetTimeStampMS();

                        // do a ode simulation step
                        lock (SimulationLock)
                        {
                            UBOdeNative.WorldQuickStep(world, ODE_STEPSIZE);
                            UBOdeNative.JointGroupEmpty(JointContactGroup);
                        }
                        //qstepTIme += Util.GetTimeStampMS() - tmpTime;

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
                        //tmpTime =  Util.GetTimeStampMS();
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

                        //updatesTime += Util.GetTimeStampMS() - tmpTime;
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[PHYSICS]: {0}, {1}, {2}", e.Message, e.TargetSite, e);
                        //ode.dunlock(world);
                    }

                    step_time -= ODE_STEPSIZE;
                    nodeframes++;

                     if (Util.GetTimeStampMS() > maxLoopTime)
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

                // information block for in debug breakpoint only
                /*
                int ntopactivegeoms = SafeNativeMethods.SpaceGetNumGeoms(ActiveSpace);
                int ntopstaticgeoms = SafeNativeMethods.SpaceGetNumGeoms(StaticSpace);
                int ngroundgeoms = SafeNativeMethods.SpaceGetNumGeoms(GroundSpace);

                int nactivegeoms = 0;
                int nactivespaces = 0;

                int nstaticgeoms = 0;
                int nstaticspaces = 0;
                IntPtr sp;

                for (int i = 0; i < ntopactivegeoms; i++)
                {
                    sp = SafeNativeMethods.SpaceGetGeom(ActiveSpace, i);
                    if (SafeNativeMethods.GeomIsSpace(sp))
                    {
                        nactivespaces++;
                        nactivegeoms += SafeNativeMethods.SpaceGetNumGeoms(sp);
                    }
                    else
                        nactivegeoms++;
                }

                for (int i = 0; i < ntopstaticgeoms; i++)
                {
                    sp = SafeNativeMethods.SpaceGetGeom(StaticSpace, i);
                    if (SafeNativeMethods.GeomIsSpace(sp))
                    {
                        nstaticspaces++;
                        nstaticgeoms += SafeNativeMethods.SpaceGetNumGeoms(sp);
                    }
                    else
                        nstaticgeoms++;
                }
                
                int ntopgeoms = SafeNativeMethods.SpaceGetNumGeoms(TopSpace);

                int totgeoms = nstaticgeoms + nactivegeoms + ngroundgeoms + 1; // one ray
                int nbodies = SafeNativeMethods.NTotalBodies;
                int ngeoms = SafeNativeMethods.NTotalGeoms;
                */

                //looptimeMS /= nodeframes;
                //collisionTime /= nodeframes;
                //qstepTIme /= nodeframes;
                //changestot /= nodeframes; 
                //collisonRepo /= nodeframes;
                //updatesTime /= nodeframes;
                //moveTime /= nodeframes;
                //rayTime /= nodeframes;

                //if(looptimeMS > .05)
                {
                }
                
                fps = (float)nodeframes * ODE_STEPSIZE / reqTimeStep;

                if(step_time < HalfOdeStep)
                    m_timeDilation = 1.0f;
                else if (step_time > m_SkipFramesAtms)
                {
                    // if we lag too much skip frames
                    m_timeDilation = 0.0f;
                    step_time = 0;
                    m_lastframe = Util.GetTimeStamp(); // skip also the time lost
                }
                else
                {
                    m_timeDilation = ODE_STEPSIZE / step_time;
                    if (m_timeDilation > 1)
                        m_timeDilation = 1;
                }

                if (m_timeDilation == 1 && now - m_lastMeshExpire > 30)
                {
                    mesher.ExpireReleaseMeshs();
                    m_lastMeshExpire = now;
                }
            }

            return fps;
        }

        public unsafe float GetTerrainHeightAtXY(float x, float y)
        {
            // TerrainHeightField for ODE as offset 1m
            x += 1f;
            y += 1f;

            // integer indexs
            int ix;
            int iy;
            //  interpolators offset
            float dx;
            float dy;

            // make position fit into array
            if (x < 0)
            {
                ix = 0;
                dx = 0;
            }
            else if (x < m_heightmapWidthSamples - 1)
            {
                ix = (int)x;
                dx = x - ix;
            }
            else // out world use external height
            {
                ix = m_heightmapWidthSamples - 2;
                dx = 0;
            }
            if (y < 0)
            {
                iy = 0;
                dy = 0;
            }
            else if (y < m_heightmapHeightSamples - 1)
            {
                iy = (int)y;
                dy = y - iy;
            }
            else
            {
                iy = m_heightmapHeightSamples - 2;
                dy = 0;
            }

            float h0;
            float h1;
            float h2;

            iy *= m_heightmapWidthSamples;
            iy += ix; // all indexes have iy + ix

            fixed(float* heightsb = &m_terrainHeights[iy])
            {
                float* heights = heightsb;
                h0 = *heights; // 0,0 vertice

                if (dy>dx)
                {
                    heights += m_heightmapWidthSamples;
                    h2 = *heights; // 0,1 vertice
                    h1 = (h2 - h0) * dy; // 0,1 vertice minus 0,0
                    ++heights;
                    h2 = (*heights - h2) * dx; // 1,1 vertice minus 0,1
                }
                else
                {
                    ++heights;
                    h2 = *heights; // vertice 1,0
                    h1 = (h2 - h0) * dx; // 1,0 vertice minus 0,0
                    heights += m_heightmapWidthSamples;
                    h2 = (*heights - h2) * dy; // 1,1 vertice minus 1,0
                }
            }
            return h0 + h1 + h2;
        }

        public unsafe Vector3 GetTerrainNormalAtXY(float x, float y)
        {
            // TerrainHeightField for ODE as offset 1m
            x += 1f;
            y += 1f;

            int ix;
            int iy;
            float dx;
            float dy;

            // make position fit into array
            if (x < 0)
            {
                ix = 0;
                dx = 0;
            }
            else if (x < m_heightmapWidthSamples - 1)
            {
                ix = (int)x;
                dx = x - ix;
            }
            else // out world use external height
            {
                ix = m_heightmapWidthSamples - 2;
                dx = 0;
            }
            if (y < 0)
            {
                iy = 0;
                dy = 0;
            }
            else if (y < m_heightmapHeightSamples - 1)
            {
                iy = (int)y;
                dy = y - iy;
            }
            else
            {
                iy = m_heightmapHeightSamples - 2;
                dy = 0;
            }

            float h0;
            float h1;
            float h2;

            iy *= m_heightmapWidthSamples;
            iy += ix; // all indexes have iy + ix

            float rx;
            float ry;
            fixed (float* heightsB = &m_terrainHeights[iy])
            {
                float* heights = heightsB;
                if (dy > dx)
                {
                    h1 = *heights; // 0,0 vertice
                    heights += m_heightmapWidthSamples;
                    h0 = *heights; // 0,1
                    h2 = *(heights + 1); // 1,1 vertice
                    rx = h0 - h2;
                    ry = h1 - h0;
                }
                else
                {
                    h2 = *heights; // 0,0 vertice
                    heights++;
                    h0 = *heights; // 1,0 vertice
                    h1 = *(heights + m_heightmapWidthSamples); // vertice 1,1
                    rx = h2 - h0;
                    ry = h0 - h1;
                }
            }
            h0 = rx * rx + ry * ry + 1.0f;
            h0 = 1.0f / MathF.Sqrt(h0);
            return new Vector3(rx * h0, ry * h0, h0);
        }

        private void InitTerrain()
        {
            lock(SimulationLock)
            lock (OdeLock)
            {
                UBOdeNative.AllocateODEDataForThread(~0U);

                if (TerrainGeom != IntPtr.Zero)
                {
                    actor_name_map.Remove(TerrainGeom);
                    UBOdeNative.GeomDestroy(TerrainGeom);
                }

                if (m_terrainHeightsHandler.IsAllocated)
                    m_terrainHeightsHandler.Free();
                m_terrainHeights = null;

                m_heightmapWidthSamples = m_regionWidth + 3;
                m_heightmapHeightSamples = m_regionHeight + 3;

                m_terrainHeights = new float[m_heightmapWidthSamples * m_heightmapHeightSamples];
                m_terrainHeightsHandler = GCHandle.Alloc(m_terrainHeights, GCHandleType.Pinned);

                m_lastRegionWidth = m_regionWidth;
                m_lastRegionHeight = m_regionHeight;

                HeightmapData = UBOdeNative.GeomOSTerrainDataCreate();
                UBOdeNative.GeomOSTerrainDataBuild(HeightmapData, m_terrainHeightsHandler.AddrOfPinnedObject(), 0, 1.0f,
                                                 m_heightmapWidthSamples, m_heightmapHeightSamples,
                                                 1, 0);

                TerrainGeom = UBOdeNative.CreateOSTerrain(TopSpace, HeightmapData, 1);
                if (TerrainGeom != IntPtr.Zero)
                {
                    UBOdeNative.GeomSetCategoryBits(TerrainGeom, (uint)(CollisionCategories.Land));
                    UBOdeNative.GeomSetCollideBits(TerrainGeom, 0);

                    PhysicsActor pa = new NullPhysicsActor();
                    pa.Name = "Terrain";
                    pa.PhysicsActorType = (int)ActorTypes.Ground;
                    actor_name_map[TerrainGeom] = pa;

                    //geom_name_map[GroundGeom] = "Terrain";

                    UBOdeNative.GeomSetPosition(TerrainGeom, m_regionWidth * 0.5f, m_regionHeight * 0.5f, 0.0f);
                }
                else
                    m_terrainHeightsHandler.Free();
            }
        }

        public override void SetTerrain(float[] heightMap)
        {
            // assumes 1m size grid and constante size square regions
            // needs to know about sims around in future

            if(m_regionWidth != m_lastRegionWidth ||
                    m_regionHeight != m_lastRegionHeight ||
                    !m_terrainHeightsHandler.IsAllocated ||
                    TerrainGeom == IntPtr.Zero)
                InitTerrain();

            int regionsizeX = m_regionWidth;
            int regionsizeY = m_regionHeight;

            int heightmapWidth = regionsizeX + 2;
            int heightmapHeight = regionsizeY + 2;

            m_heightmapWidthSamples = heightmapWidth + 1;
            m_heightmapHeightSamples = heightmapHeight + 1;

            float val;

            int maxXX = regionsizeX + 1;
            int maxYY = regionsizeY + 1;
            // adding one margin all around so things don't fall in edges

            int xx;
            int yy = 0;
            int yt = 0;
            float minH = float.MaxValue;
            float maxH = float.MinValue;

            for (int y = 0; y < m_heightmapHeightSamples; y++)
            {
                if (y > 1 && y < maxYY)
                    yy += regionsizeX;
                xx = 0;

                lock(OdeLock)
                {
                    for (int x = 0; x < m_heightmapWidthSamples; x++)
                    {
                        if (x > 1 && x < maxXX)
                            xx++;

                        val = Utils.Clamp(heightMap[yy + xx], Constants.MinTerrainHeightmap, Constants.MaxTerrainHeightmap);
                        if(val > maxH)
                            maxH = val;
                        if(val < minH)
                            minH = val;
                        m_terrainHeights[yt + x] = val;
                    }
                }
                yt += m_heightmapWidthSamples;
            }

            lock(SimulationLock)
            lock (OdeLock)
            {
                UBOdeNative.GeomOSTerrainDataSetBounds(HeightmapData, minH, maxH);
                UBOdeNative.GeomSetPosition(TerrainGeom, m_regionWidth * 0.5f, m_regionHeight * 0.5f, 0.0f);
            }
        }

        public override void DeleteTerrain()
        {
        }

        public override void SetWaterLevel(float baseheight)
        {
            WaterLevel = baseheight;
        }

        public override void Dispose()
        {
            lock(SimulationLock)
            lock (OdeLock)
            {
                if (world == IntPtr.Zero)
                    return;

                UBOdeNative.AllocateODEDataForThread(~0U);

                if (m_meshWorker != null)
                    m_meshWorker.Stop();

                if (m_rayCastManager != null)
                {
                    m_rayCastManager.Dispose();
                    m_rayCastManager = null;
                }

                lock (_prims)
                {
                    foreach (OdePrim prm in _prims.Values)
                    {
                        prm.DoAChange(changes.Remove, null);
                        _collisionEventPrim.Remove(prm.LocalID);
                    }
                    _prims.Clear();
                }

                OdeCharacter[] chtorem;
                lock (_characters)
                {
                    chtorem = new OdeCharacter[_characters.Count];
                    _characters.CopyTo(chtorem);
                }

                foreach (OdeCharacter ch in chtorem)
                    ch.DoAChange(changes.Remove, null);

                if (TerrainGeom != IntPtr.Zero)
                {
                    UBOdeNative.GeomDestroy(TerrainGeom);
                    TerrainGeom = IntPtr.Zero;
                }

                if (m_terrainHeightsHandler.IsAllocated)
                    m_terrainHeightsHandler.Free();
                m_terrainHeights = null;

                if (m_contactsHandler.IsAllocated)
                {
                    m_contactsHandler.Free();
                    ContactgeomsArray = IntPtr.Zero;
                }

                if (GlobalContactsArray != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(GlobalContactsArray);
                    GlobalContactsArray = IntPtr.Zero;
                }

                UBOdeNative.WorldDestroy(world);
                world = IntPtr.Zero;
                //d.CloseODE();
            }
        }

        private int compareByCollisionsDesc(OdePrim A, OdePrim B)
        {
            return -A.CollisionScore.CompareTo(B.CollisionScore);
        }

        public override Dictionary<uint, float> GetTopColliders()
        {
            Dictionary<uint, float> topColliders;
            List<OdePrim> orderedPrims;
            lock (_activeprims)
                orderedPrims = new List<OdePrim>(_activeprims);

            orderedPrims.Sort(compareByCollisionsDesc);
            topColliders = orderedPrims.Take(25).ToDictionary(p => p.m_baseLocalID, p => p.CollisionScore);

            return topColliders;
        }

        public override bool SupportsRayCast()
        {
            return true;
        }

        public override void RaycastWorld(Vector3 position, Vector3 direction, float length, RaycastCallback retMethod)
        {
            if (retMethod != null)
            {
                ODERayRequest req = new()
                {
                    actor = null,
                    callbackMethod = retMethod,
                    length = length,
                    Normal = direction,
                    Origin = position,
                    Count = 0,
                    filter = RayFilterFlags.AllPrims
                };
                m_rayCastManager.QueueRequest(req);
            }
        }

        public override void RaycastWorld(Vector3 position, Vector3 direction, float length, int Count, RayCallback retMethod)
        {
            if (retMethod != null)
            {
                ODERayRequest req = new()
                {
                    actor = null,
                    callbackMethod = retMethod,
                    length = length,
                    Normal = direction,
                    Origin = position,
                    Count = Count,
                    filter = RayFilterFlags.AllPrims
                };

                m_rayCastManager.QueueRequest(req);
            }
        }

        public override List<ContactResult> RaycastWorld(Vector3 position, Vector3 direction, float length, int Count)
        {
            List<ContactResult> ourresults = new();
            object SyncObject = new();

            RayCallback retMethod = delegate(List<ContactResult> results)
            {
                lock (SyncObject)
                {
                    ourresults = results;
                    Monitor.PulseAll(SyncObject);
                }
            };

            ODERayRequest req = new()
            {
                actor = null,
                callbackMethod = retMethod,
                length = length,
                Normal = direction,
                Origin = position,
                Count = Count,
                filter = RayFilterFlags.AllPrims
            };

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
            object SyncObject = new();
            List<ContactResult> ourresults = new();

            RayCallback retMethod = delegate(List<ContactResult> results)
            {
                lock (SyncObject)
                {
                    ourresults = results;
                    Monitor.PulseAll(SyncObject);
                }
            };

            ODERayRequest req = new()
            {
                actor = null,
                callbackMethod = retMethod,
                length = length,
                Normal = direction,
                Origin = position,
                Count = Count,
                filter = filter
            };

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
            if (actor is OdePrim prim)
                geom = prim.m_prim_geom;
            else if (actor is OdeCharacter ch)
                geom = ch.collider;
            else
                return new List<ContactResult>();

            if (geom == IntPtr.Zero)
                return new List<ContactResult>();

            List<ContactResult> ourResults = null;
            object SyncObject = new();

            RayCallback retMethod = delegate(List<ContactResult> results)
            {
                lock (SyncObject)
                {
                    ourResults = results;
                    Monitor.PulseAll(SyncObject);
                }
            };

            ODERayRequest req = new()
            {
                actor = actor,
                callbackMethod = retMethod,
                length = length,
                Normal = direction,
                Origin = position,
                Count = Count,
                filter = flags
            };

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
                ODESitAvatar sitAvatar = new(this, m_rayCastManager);
                if(sitAvatar is not null)
                    sitAvatar.Sit(actor, AbsolutePosition, CameraPosition, offset, AvatarSize, PhysicsSitResponse);
            });
            return 1;
        }
    }
}
