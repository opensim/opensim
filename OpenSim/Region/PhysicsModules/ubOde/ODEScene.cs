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
        SetInertia,

        Null             //keep this last used do dim the methods array. does nothing but pulsing the prim
    }

    public struct ODEchangeitem
    {
        public PhysicsActor actor;
        public changes what;
        public Object arg;
    }

    public class ODEScene : PhysicsScene
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public bool m_OSOdeLib = false;
        public Scene m_frameWorkScene = null;

//        private int threadid = 0;

//        const d.ContactFlags comumContactFlags = d.ContactFlags.SoftERP | d.ContactFlags.SoftCFM |d.ContactFlags.Approx1 | d.ContactFlags.Bounce;

//        const d.ContactFlags comumContactFlags = d.ContactFlags.Bounce | d.ContactFlags.Approx1 | d.ContactFlags.Slip1 | d.ContactFlags.Slip2;
        const SafeNativeMethods.ContactFlags comumContactFlags = SafeNativeMethods.ContactFlags.Bounce | SafeNativeMethods.ContactFlags.Approx1;
        const float comumContactERP = 0.75f;
        const float comumContactCFM = 0.0001f;
        const float comumContactSLIP = 0f;

//        float frictionMovementMult = 0.2f;

        float TerrainBounce = 0.001f;
        float TerrainFriction = 0.3f;

        public float AvatarFriction = 0;// 0.9f * 0.5f;

        // this netx dimensions are only relevant for terrain partition (mega regions)
        // WorldExtents below has the simulation dimensions
        // they should be identical except on mega regions
        private int m_regionWidth = (int)Constants.RegionSize;
        private int m_regionHeight = (int)Constants.RegionSize;

        public float ODE_STEPSIZE = 0.020f;
        public float HalfOdeStep = 0.01f;
        public int odetimestepMS = 20; // rounded
        private float m_timeDilation = 1.0f;

        private double m_lastframe;
        private double m_lastMeshExpire;

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

        public int bodyFramesAutoDisable = 10;

        private SafeNativeMethods.NearCallback nearCallback;

        private Dictionary<uint,OdePrim> _prims = new Dictionary<uint,OdePrim>();
        private HashSet<OdeCharacter> _characters = new HashSet<OdeCharacter>();
        private HashSet<OdePrim> _activeprims = new HashSet<OdePrim>();
        private HashSet<OdePrim> _activegroups = new HashSet<OdePrim>();

        public ConcurrentQueue<ODEchangeitem> ChangesQueue = new ConcurrentQueue<ODEchangeitem>();

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
        private SafeNativeMethods.Contact SharedTmpcontact = new SafeNativeMethods.Contact();

        const int maxContactsbeforedeath = 6000;
        private volatile int m_global_contactcount = 0;

        private IntPtr contactgroup;

        public ContactData[] m_materialContactsData = new ContactData[8];

        private IntPtr m_terrainGeom;
        private float[] m_terrainHeights;
        private GCHandle m_terrainHeightsHandler = new GCHandle();
        private IntPtr HeightmapData;
        private int m_lastRegionWidth;
        private int m_lastRegionHeight;

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

        public object OdeLock = new object();
        public static object SimulationLock = new object();

        public IMesher mesher;

        public IConfigSource m_config;

        public Vector2 WorldExtents = new Vector2((int)Constants.RegionSize, (int)Constants.RegionSize);

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

        public ODEScene(Scene pscene, IConfigSource psourceconfig, string pname, string pversion, bool pOSOdeLib)
        {
            EngineType = pname;
            PhysicsSceneName = EngineType + "/" + pscene.RegionInfo.RegionName;
            EngineName = pname + " " + pversion;
            m_config = psourceconfig;
            m_OSOdeLib = pOSOdeLib;

//            m_OSOdeLib = false; //debug

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
            SafeNativeMethods.AllocateODEDataForThread(~0U);

            nearCallback = near;

            m_rayCastManager = new ODERayCastRequestManager(this);

            WorldExtents.X = m_frameWorkScene.RegionInfo.RegionSizeX;
            m_regionWidth = (int)WorldExtents.X;
            WorldExtents.Y = m_frameWorkScene.RegionInfo.RegionSizeY;
            m_regionHeight = (int)WorldExtents.Y;

            lock (OdeLock)
            {
                // Create the world and the first space
                try
                {
                    world = SafeNativeMethods.WorldCreate();
                    TopSpace = SafeNativeMethods.SimpleSpaceCreate(IntPtr.Zero);
                    ActiveSpace = SafeNativeMethods.SimpleSpaceCreate(TopSpace);
                    CharsSpace = SafeNativeMethods.SimpleSpaceCreate(TopSpace);
                    GroundSpace = SafeNativeMethods.SimpleSpaceCreate(TopSpace);
                    float sx = WorldExtents.X + 16;
                    float sy = WorldExtents.Y + 16;
                    SafeNativeMethods.Vector3 ex =new SafeNativeMethods.Vector3(sx, sy, 0);
                    SafeNativeMethods.Vector3 px =new SafeNativeMethods.Vector3(sx * 0.5f, sx  * 0.5f, 0);
                    if(sx < sy)
                        sx = sy;
                    sx = (float)Math.Log(sx) * 1.442695f + 0.5f;
                    int dp = (int)sx - 2;
                    if(dp > 8)
                        dp = 8;
                    else if(dp < 4)
                        dp = 4;
                    StaticSpace = SafeNativeMethods.QuadTreeSpaceCreate(TopSpace, ref px, ref ex, dp);
                }
                catch
                {
                    // i must RtC#FM
                    // i did!
                }

                // demote to second level
                SafeNativeMethods.SpaceSetSublevel(ActiveSpace, 1);
                SafeNativeMethods.SpaceSetSublevel(CharsSpace, 1);
                SafeNativeMethods.SpaceSetSublevel(StaticSpace, 1);
                SafeNativeMethods.SpaceSetSublevel(GroundSpace, 1);

                SafeNativeMethods.GeomSetCategoryBits(ActiveSpace, (uint)(CollisionCategories.Space |
                                                        CollisionCategories.Geom |
                                                        CollisionCategories.Character |
                                                        CollisionCategories.Phantom |
                                                        CollisionCategories.VolumeDtc
                                                        ));
                SafeNativeMethods.GeomSetCollideBits(ActiveSpace, (uint)(CollisionCategories.Space |
                                                        CollisionCategories.Geom |
                                                        CollisionCategories.Character |
                                                        CollisionCategories.Phantom |
                                                        CollisionCategories.VolumeDtc
                                                        ));
                SafeNativeMethods.GeomSetCategoryBits(CharsSpace, (uint)(CollisionCategories.Space |
                                        CollisionCategories.Geom |
                                        CollisionCategories.Character |
                                        CollisionCategories.Phantom |
                                        CollisionCategories.VolumeDtc
                                        ));
                SafeNativeMethods.GeomSetCollideBits(CharsSpace, 0);

                SafeNativeMethods.GeomSetCategoryBits(StaticSpace, (uint)(CollisionCategories.Space |
                                                        CollisionCategories.Geom |
                                                        //                                                        CollisionCategories.Land |
                                                        //                                                        CollisionCategories.Water |
                                                        CollisionCategories.Phantom |
                                                        CollisionCategories.VolumeDtc
                                                        ));
                SafeNativeMethods.GeomSetCollideBits(StaticSpace, 0);

                SafeNativeMethods.GeomSetCategoryBits(GroundSpace, (uint)(CollisionCategories.Land));
                SafeNativeMethods.GeomSetCollideBits(GroundSpace, 0);

                contactgroup = SafeNativeMethods.JointGroupCreate(maxContactsbeforedeath + 1);
                //contactgroup

                SafeNativeMethods.WorldSetAutoDisableFlag(world, false);
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

                    //                    contactsurfacelayer = physicsconfig.GetFloat("world_contact_surface_layer", contactsurfacelayer);

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

            SafeNativeMethods.WorldSetCFM(world, comumContactCFM);
            SafeNativeMethods.WorldSetERP(world, comumContactERP);

            SafeNativeMethods.WorldSetGravity(world, gravityx, gravityy, gravityz);

            SafeNativeMethods.WorldSetLinearDamping(world, 0.001f);
            SafeNativeMethods.WorldSetAngularDamping(world, 0.002f);
            SafeNativeMethods.WorldSetAngularDampingThreshold(world, 0f);
            SafeNativeMethods.WorldSetLinearDampingThreshold(world, 0f);
            SafeNativeMethods.WorldSetMaxAngularSpeed(world, maximumAngularVelocity);

            SafeNativeMethods.WorldSetQuickStepNumIterations(world, m_physicsiterations);

            SafeNativeMethods.WorldSetContactSurfaceLayer(world, contactsurfacelayer);
            SafeNativeMethods.WorldSetContactMaxCorrectingVel(world, 60.0f);

            HalfOdeStep = ODE_STEPSIZE * 0.5f;
            odetimestepMS = (int)(1000.0f * ODE_STEPSIZE + 0.5f);

            ContactgeomsArray = Marshal.AllocHGlobal(contactsPerCollision * SafeNativeMethods.ContactGeom.unmanagedSizeOf);
            GlobalContactsArray = Marshal.AllocHGlobal((maxContactsbeforedeath + 100) * SafeNativeMethods.Contact.unmanagedSizeOf);

            SharedTmpcontact.geom.g1 = IntPtr.Zero;
            SharedTmpcontact.geom.g2 = IntPtr.Zero;

            SharedTmpcontact.geom.side1 = -1;
            SharedTmpcontact.geom.side2 = -1;

            SharedTmpcontact.surface.mode = comumContactFlags;
            SharedTmpcontact.surface.mu = 0;
            SharedTmpcontact.surface.bounce = 0;
            SharedTmpcontact.surface.bounce_vel = 1.5f;
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

            m_lastframe = Util.GetTimeStamp();
            m_lastMeshExpire = m_lastframe;
            step_time = -1;


            base.Initialise(m_frameWorkScene.PhysicsRequestAsset,
                (m_frameWorkScene.Heightmap != null ? m_frameWorkScene.Heightmap.GetFloatsSerialised() : new float[m_frameWorkScene.RegionInfo.RegionSizeX * m_frameWorkScene.RegionInfo.RegionSizeY]),
                (float)m_frameWorkScene.RegionInfo.RegionSettings.WaterHeight);
        }

        internal void waitForSpaceUnlock(IntPtr space)
        {
            //if (space != IntPtr.Zero)
                //while (d.SpaceLockQuery(space)) { } // Wait and do nothing
        }

        #region Collision Detection

        // sets a global contact for a joint for contactgeom , and base contact description)
        private IntPtr CreateContacJoint(ref SafeNativeMethods.ContactGeom contactGeom,bool smooth)
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

            IntPtr contact = new IntPtr(GlobalContactsArray.ToInt64() + (Int64)(m_global_contactcount * SafeNativeMethods.Contact.unmanagedSizeOf));
            Marshal.StructureToPtr(SharedTmpcontact, contact, true);
            return SafeNativeMethods.JointCreateContactPtr(world, contactgroup, contact);
        }

        private bool GetCurContactGeom(int index, ref SafeNativeMethods.ContactGeom newcontactgeom)
        {
            if (ContactgeomsArray == IntPtr.Zero || index >= contactsPerCollision)
                return false;

            IntPtr contactptr = new IntPtr(ContactgeomsArray.ToInt64() + (Int64)(index * SafeNativeMethods.ContactGeom.unmanagedSizeOf));
            newcontactgeom = (SafeNativeMethods.ContactGeom)Marshal.PtrToStructure(contactptr, typeof(SafeNativeMethods.ContactGeom));
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

            if (SafeNativeMethods.GeomIsSpace(g1) || SafeNativeMethods.GeomIsSpace(g2))
            {
                // We'll be calling near recursivly if one
                // of them is a space to find all of the
                // contact points in the space
                try
                {
                    SafeNativeMethods.SpaceCollide2(g1, g2, IntPtr.Zero, nearCallback);
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

            // Figure out how many contact points we have
            int count = 0;
            try
            {
                if (g1 == g2)
                    return; // Can't collide with yourself

                if (SafeNativeMethods.GeomGetCategoryBits(g1) == (uint)CollisionCategories.VolumeDtc ||
                    SafeNativeMethods.GeomGetCategoryBits(g2) == (uint)CollisionCategories.VolumeDtc)
                {
                    int cflags;
                    unchecked
                    {
                        cflags = (int)(1 | SafeNativeMethods.CONTACTS_UNIMPORTANT);
                    }
                    count = SafeNativeMethods.CollidePtr(g1, g2, cflags, ContactgeomsArray, SafeNativeMethods.ContactGeom.unmanagedSizeOf);
                }
                else
                    count = SafeNativeMethods.CollidePtr(g1, g2, (contactsPerCollision & 0xffff), ContactgeomsArray, SafeNativeMethods.ContactGeom.unmanagedSizeOf);
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

            // get first contact
            SafeNativeMethods.ContactGeom curContact = new SafeNativeMethods.ContactGeom();
            if (!GetCurContactGeom(0, ref curContact))
                return;

            // try get physical actors
            PhysicsActor p1;
            if (!actor_name_map.TryGetValue(g1, out p1))
            {
                m_log.WarnFormat("[PHYSICS]: failed actor mapping for geom 1");
                return;
            }

            PhysicsActor p2;
            if (!actor_name_map.TryGetValue(g2, out p2))
            {
                m_log.WarnFormat("[PHYSICS]: failed actor mapping for geom 2");
                return;
            }

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
//                                Vector3 relV = p1.rootVelocity - p2.rootVelocity;
//                                float relVlenSQ = relV.LengthSquared();
//                                if (relVlenSQ > 0.0001f)
                                {
                                    p1.CollidingObj = true;
                                    p2.CollidingObj = true;
                                }
                                p1.getContactData(ref contactdata1);
                                p2.getContactData(ref contactdata2);
                                bounce = contactdata1.bounce * contactdata2.bounce;
                                mu = (float)Math.Sqrt(contactdata1.mu * contactdata2.mu);

//                                if (relVlenSQ > 0.01f)
//                                    mu *= frictionMovementMult;

                                if(SafeNativeMethods.GeomGetClass(g2) == SafeNativeMethods.GeomClassID.TriMeshClass &&
                                    SafeNativeMethods.GeomGetClass(g1) == SafeNativeMethods.GeomClassID.TriMeshClass)
                                    smoothMesh = true;
                                break;

                            case (int)ActorTypes.Ground:
                                p1.getContactData(ref contactdata1);
                                bounce = contactdata1.bounce * TerrainBounce;
                                mu = (float)Math.Sqrt(contactdata1.mu * TerrainFriction);

//                                Vector3 v1 = p1.rootVelocity;
//                                if (Math.Abs(v1.X) > 0.1f || Math.Abs(v1.Y) > 0.1f)
//                                    mu *= frictionMovementMult;
                                p1.CollidingGround = true;

                                if(SafeNativeMethods.GeomGetClass(g1) == SafeNativeMethods.GeomClassID.TriMeshClass)
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
//                        Vector3 v2 = p2.rootVelocity;
//                        if (Math.Abs(v2.X) > 0.1f || Math.Abs(v2.Y) > 0.1f)
//                            mu *= frictionMovementMult;

                        if(SafeNativeMethods.GeomGetClass(g2) == SafeNativeMethods.GeomClassID.TriMeshClass)
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

            SafeNativeMethods.ContactGeom altContact = new SafeNativeMethods.ContactGeom();
            bool useAltcontact;
            bool noskip;

            if(dop1ava || dop2ava)
                smoothMesh = false;

            IntPtr b1 = SafeNativeMethods.GeomGetBody(g1);
            IntPtr b2 = SafeNativeMethods.GeomGetBody(g2);

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
                        else if (p2.rootVelocity.LengthSquared() > 0.0f)
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
                    else if (p1.rootVelocity.LengthSquared() > 0.0f)
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

                    SafeNativeMethods.JointAttach(Joint, b1, b2);

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
            if (p2 != null && p2.IsPhysical)
                vel = p2.rootVelocity;

            if (p1 != null && p1.IsPhysical)
                vel -= p1.rootVelocity;

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
                                //AddCollisionEventReporting(p2);
                                p2.AddCollisionEvent(p1.ParentActor.LocalID, contact);
                            }
                            else if(p1.IsVolumeDtc)
                                p2.AddVDTCCollisionEvent(p1.ParentActor.LocalID, contact);

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
                        SafeNativeMethods.SpaceCollide2(chr.collider, StaticSpace, IntPtr.Zero, nearCallback);

                        // no coll with gnd
                    }
                    // chars with chars
                    SafeNativeMethods.SpaceCollide(CharsSpace, IntPtr.Zero, nearCallback);

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
                    if(!aprim.m_outbounds && SafeNativeMethods.BodyIsEnabled(aprim.Body))
                        aprim.clearSleeperCollisions();
                }
            }

            lock (_activegroups)
            {
                try
                {
                    foreach (OdePrim aprim in _activegroups)
                    {
                        if(!aprim.m_outbounds && SafeNativeMethods.BodyIsEnabled(aprim.Body) &&
                                aprim.collide_geom != IntPtr.Zero)
                        {
                            SafeNativeMethods.SpaceCollide2(StaticSpace, aprim.collide_geom, IntPtr.Zero, nearCallback);
                            SafeNativeMethods.SpaceCollide2(GroundSpace, aprim.collide_geom, IntPtr.Zero, nearCallback);
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
                SafeNativeMethods.SpaceCollide(ActiveSpace, IntPtr.Zero, nearCallback);
            }
            catch (Exception e)
            {
                    m_log.Warn("[PHYSICS]: Unable to collide in Active: " + e.Message);
            }

            // and with chars
            try
            {
                SafeNativeMethods.SpaceCollide2(CharsSpace,ActiveSpace, IntPtr.Zero, nearCallback);
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
            lock(_collisionEventPrimRemove)
            {
               if (_collisionEventPrim.Contains(obj) && !_collisionEventPrimRemove.Contains(obj))
                    _collisionEventPrimRemove.Add(obj);
            }
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
            if (world == IntPtr.Zero)
                return;
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
                OdePrim p = (OdePrim)prim;
                p.setPrimForRemoval();
            }
        }

        public void RemovePrimThreadLocked(OdePrim prim)
        {
            //Console.WriteLine("RemovePrimThreadLocked " +  prim.m_primName);
            lock (_prims)
                _prims.Remove(prim.LocalID);
        }

        public void addToPrims(OdePrim prim)
        {
            lock (_prims)
                _prims[prim.LocalID] = prim;
        }

        public OdePrim getPrim(uint id)
        {
            lock (_prims)
            {
                if(_prims.ContainsKey(id))
                    return _prims[id];
                else
                    return null;
            }
        }

        public bool havePrim(OdePrim prm)
        {
            lock (_prims)
                return _prims.ContainsKey(prm.LocalID);
        }

        public void changePrimID(OdePrim prim,uint oldID)
        {
            lock (_prims)
            {
                if(_prims.ContainsKey(oldID))
                    _prims.Remove(oldID);
                _prims[prim.LocalID] = prim;
            }
        }

        public bool haveActor(PhysicsActor actor)
        {
            if (actor is OdePrim)
            {
                lock (_prims)
                    return _prims.ContainsKey(((OdePrim)actor).LocalID);
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
            if (currentspace != IntPtr.Zero && SafeNativeMethods.SpaceQuery(currentspace, geom))
            {
                if (SafeNativeMethods.GeomIsSpace(currentspace))
                {
                    waitForSpaceUnlock(currentspace);
                    SafeNativeMethods.SpaceRemove(currentspace, geom);

                    if (SafeNativeMethods.SpaceGetSublevel(currentspace) > 2 && SafeNativeMethods.SpaceGetNumGeoms(currentspace) == 0)
                    {
                        SafeNativeMethods.SpaceDestroy(currentspace);
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
                currentspace = SafeNativeMethods.GeomGetSpace(geom);
                if (currentspace != IntPtr.Zero)
                {
                    if (SafeNativeMethods.GeomIsSpace(currentspace))
                    {
                        waitForSpaceUnlock(currentspace);
                        SafeNativeMethods.SpaceRemove(currentspace, geom);

                        if (SafeNativeMethods.SpaceGetSublevel(currentspace) > 2 && SafeNativeMethods.SpaceGetNumGeoms(currentspace) == 0)
                        {
                            SafeNativeMethods.SpaceDestroy(currentspace);
                        }
                    }
                }
            }

            // put the geom in the newspace
            waitForSpaceUnlock(StaticSpace);
            if(SafeNativeMethods.SpaceQuery(StaticSpace, geom))
                m_log.Info("[Physics]: 'MoveGeomToStaticSpace' geom already in static space:" + geom);
            else
                SafeNativeMethods.SpaceAdd(StaticSpace, geom);

            return StaticSpace;
        }
        #endregion


        /// <summary>
        /// Called to queue a change to a actor
        /// to use in place of old taint mechanism so changes do have a time sequence
        /// </summary>

        public void AddChange(PhysicsActor _actor, changes _what, Object _arg)
        {
            if (world == IntPtr.Zero)
                return;

            ODEchangeitem item = new ODEchangeitem
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

                ODEchangeitem item;

                int donechanges = 0;
                if (!ChangesQueue.IsEmpty)
                {
                    m_log.InfoFormat("[ubOde] start processing pending actor operations");
                    int tstart = Util.EnvironmentTickCount();

                    SafeNativeMethods.AllocateODEDataForThread(~0U);

                    while (ChangesQueue.TryDequeue(out item))
                    {
                        if (item.actor != null)
                        {
                            try
                            {
                                lock (SimulationLock)
                                {
                                    if (item.actor is OdeCharacter)
                                        ((OdeCharacter)item.actor).DoAChange(item.what, item.arg);
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

            if (framecount < 0)
                framecount = 0;

            framecount++;

//            checkThread();
            int nodeframes = 0;
            float fps = 0;

            lock (OdeLock)
            {

//                d.WorldSetQuickStepNumIterations(world, curphysiteractions);

                double loopstartMS = Util.GetTimeStampMS();
                double looptimeMS = 0;
                double changestimeMS = 0;
                double maxChangestime = (int)(reqTimeStep * 500f); // half the time
                double maxLoopTime = (int)(reqTimeStep * 1200f); // 1.2 the time

/*
                double collisionTime = 0;
                double qstepTIme = 0;
                double tmpTime = 0;
                double changestot = 0;
                double collisonRepo = 0;
                double updatesTime = 0;
                double moveTime = 0;
                double rayTime = 0;
*/
                SafeNativeMethods.AllocateODEDataForThread(~0U);

                if (!ChangesQueue.IsEmpty)
                {
                    ODEchangeitem item;

                    while (ChangesQueue.TryDequeue(out item))
                    {
                        if (item.actor != null)
                        {
                            try
                            {
                                lock (SimulationLock)
                                {
                                    if (item.actor is OdeCharacter)
                                        ((OdeCharacter)item.actor).DoAChange(item.what, item.arg);
                                    else if (((OdePrim)item.actor).DoAChange(item.what, item.arg))
                                        RemovePrimThreadLocked((OdePrim)item.actor);
                                }
                            }
                            catch
                            {
                                m_log.WarnFormat("[PHYSICS]: doChange failed for a actor {0} {1}",
                                    item.actor.Name, item.what.ToString());
                            }
                        }
                        changestimeMS = Util.GetTimeStampMS() - loopstartMS;
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

                        //                        tmpTime =  Util.GetTimeStampMS();

                        // Move characters
                        lock (_characters)
                        {
                            foreach (OdeCharacter actor in _characters)
                            {
                                lock (SimulationLock)
                                    actor.Move();
                            }
                        }

                        // Move other active objects
                        lock (_activegroups)
                        {
                            foreach (OdePrim aprim in _activegroups)
                            {
                                lock (SimulationLock)
                                    aprim.Move();
                            }
                        }
                        // moveTime += Util.GetTimeStampMS() - tmpTime;
                        // tmpTime =  Util.GetTimeStampMS();
                        lock (SimulationLock)
                        {
                            m_rayCastManager.ProcessQueuedRequests();
                        // rayTime += Util.GetTimeStampMS() - tmpTime;

                        // tmpTime =  Util.GetTimeStampMS();
                            collision_optimized();
                        }

                        // collisionTime += Util.GetTimeStampMS() - tmpTime;

                        // tmpTime =  Util.GetTimeStampMS();
                        lock(_collisionEventPrimRemove)
                        {
                            foreach (PhysicsActor obj in _collisionEventPrimRemove)
                                _collisionEventPrim.Remove(obj);

                            _collisionEventPrimRemove.Clear();
                        }

                        List<OdePrim> sleepers = new List<OdePrim>();
                        foreach (PhysicsActor obj in _collisionEventPrim)
                        {
                            switch ((ActorTypes)obj.PhysicsActorType)
                            {
                                case ActorTypes.Agent:
                                    OdeCharacter cobj = (OdeCharacter)obj;
                                    cobj.SendCollisions((int)(odetimestepMS));
                                    break;

                                case ActorTypes.Prim:
                                    OdePrim pobj = (OdePrim)obj;
                                    if (!pobj.m_outbounds)
                                    {
                                        pobj.SendCollisions((int)(odetimestepMS));
                                        lock(SimulationLock)
                                        {
                                            if(pobj.Body != IntPtr.Zero && !pobj.m_isSelected &&
                                                !pobj.m_disabled && !pobj.m_building &&
                                                !SafeNativeMethods.BodyIsEnabled(pobj.Body))
                                            sleepers.Add(pobj);
                                        }
                                    }
                                    break;
                            }
                        }

                        foreach(OdePrim prm in sleepers)
                            prm.SleeperAddCollisionEvents();
                        sleepers.Clear();
                        // collisonRepo += Util.GetTimeStampMS() - tmpTime;


                        // do a ode simulation step
                        // tmpTime =  Util.GetTimeStampMS();
                        lock (SimulationLock)
                        {
                            SafeNativeMethods.WorldQuickStep(world, ODE_STEPSIZE);
                            SafeNativeMethods.JointGroupEmpty(contactgroup);
                        }
                        // qstepTIme += Util.GetTimeStampMS() - tmpTime;

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
                        //                        tmpTime =  Util.GetTimeStampMS();
                        //lock (SimulationLock)
                        {
                            lock (_activegroups)
                            {
                                {
                                    foreach (OdePrim actor in _activegroups)
                                    {
                                        if (actor.IsPhysical)
                                        {
                                            lock (SimulationLock)
                                                actor.UpdatePositionAndVelocity(framecount);
                                        }
                                    }
                                }
                            }
                        }

//                        updatesTime += Util.GetTimeStampMS() - tmpTime;
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[PHYSICS]: {0}, {1}, {2}", e.Message, e.TargetSite, e);
//                        ode.dunlock(world);
                    }

                    step_time -= ODE_STEPSIZE;
                    nodeframes++;

                    looptimeMS = Util.GetTimeStampMS() - loopstartMS;

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
/*
// information block for in debug breakpoint only

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

/*
                looptimeMS /= nodeframes;
                collisionTime /= nodeframes;
                qstepTIme /= nodeframes;
                changestot /= nodeframes; 
                collisonRepo /= nodeframes;
                updatesTime /= nodeframes;
                moveTime /= nodeframes;
                rayTime /= nodeframes;

                if(looptimeMS > .05)
                {


                }
*/
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

        public float GetTerrainHeightAtXY(float x, float y)
        {
            if (m_terrainGeom == IntPtr.Zero)
                return 0f;

            if (m_terrainHeights == null || m_terrainHeights.Length == 0)
                return 0f;

            // TerrainHeightField for ODE as offset 1m
            x += 1f;
            y += 1f;

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

            float h0;
            float h1;
            float h2;

            iy *= regsize;
            iy += ix; // all indexes have iy + ix

            float[] heights = m_terrainHeights;
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
            Vector3 norm = new Vector3(0, 0, 1);

            if (m_terrainGeom == IntPtr.Zero)
                return norm;

            if (m_terrainHeights == null || m_terrainHeights.Length == 0)
                return norm;

            // TerrainHeightField for ODE as offset 1m
            x += 1f;
            y += 1f;

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

            float h0;
            float h1;
            float h2;

            iy *= regsize;
            iy += ix; // all indexes have iy + ix

            float[] heights = m_terrainHeights;

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

        private void InitTerrain()
        {
            lock(SimulationLock)
            lock (OdeLock)
            {
                SafeNativeMethods.AllocateODEDataForThread(~0U);

                if (m_terrainGeom != IntPtr.Zero)
                {
                    actor_name_map.Remove(m_terrainGeom);
                    SafeNativeMethods.GeomDestroy(m_terrainGeom);
                }

                if (m_terrainHeightsHandler.IsAllocated)
                    m_terrainHeightsHandler.Free();
                m_terrainHeights = null;

                int heightmapWidthSamples = m_regionWidth + 3;
                int heightmapHeightSamples = m_regionHeight + 3;

                m_terrainHeights = new float[heightmapWidthSamples * heightmapHeightSamples];
                m_terrainHeightsHandler = GCHandle.Alloc(m_terrainHeights, GCHandleType.Pinned);

                m_lastRegionWidth = m_regionWidth;

                HeightmapData = SafeNativeMethods.GeomOSTerrainDataCreate();
                SafeNativeMethods.GeomOSTerrainDataBuild(HeightmapData, m_terrainHeightsHandler.AddrOfPinnedObject(), 0, 1.0f,
                                                 heightmapWidthSamples, heightmapHeightSamples,
                                                 1, 0);

                m_terrainGeom = SafeNativeMethods.CreateOSTerrain(GroundSpace, HeightmapData, 1);
                if (m_terrainGeom != IntPtr.Zero)
                {
                    SafeNativeMethods.GeomSetCategoryBits(m_terrainGeom, (uint)(CollisionCategories.Land));
                    SafeNativeMethods.GeomSetCollideBits(m_terrainGeom, 0);

                    PhysicsActor pa = new NullPhysicsActor();
                    pa.Name = "Terrain";
                    pa.PhysicsActorType = (int)ActorTypes.Ground;
                    actor_name_map[m_terrainGeom] = pa;

                    //geom_name_map[GroundGeom] = "Terrain";

                    SafeNativeMethods.GeomSetPosition(m_terrainGeom, m_regionWidth * 0.5f, m_regionHeight * 0.5f, 0.0f);
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
                    m_terrainGeom == IntPtr.Zero)
                InitTerrain();

            int regionsizeX = m_regionWidth;
            int regionsizeY = m_regionHeight;

            int heightmapWidth = regionsizeX + 2;
            int heightmapHeight = regionsizeY + 2;

            int heightmapWidthSamples = heightmapWidth + 1;
            int heightmapHeightSamples = heightmapHeight + 1;

            float val;

            int maxXX = regionsizeX + 1;
            int maxYY = regionsizeY + 1;
            // adding one margin all around so things don't fall in edges

            int xx;
            int yy = 0;
            int yt = 0;
            float minH = float.MaxValue;
            float maxH = float.MinValue;

            for (int y = 0; y < heightmapHeightSamples; y++)
            {
                if (y > 1 && y < maxYY)
                    yy += regionsizeX;
                xx = 0;

                lock(OdeLock)
                {
                    for (int x = 0; x < heightmapWidthSamples; x++)
                    {
                        if (x > 1 && x < maxXX)
                            xx++;

                        val = heightMap[yy + xx];
                        if (val < -100.0f)
                            val = -100.0f;
                        if(val > maxH)
                            maxH = val;
                        if(val < minH)
                            minH = val;
                        m_terrainHeights[yt + x] = val;
                    }
                }
                yt += heightmapWidthSamples;
            }

            lock(SimulationLock)
            lock (OdeLock)
            {
                SafeNativeMethods.GeomOSTerrainDataSetBounds(HeightmapData, minH, maxH);
                SafeNativeMethods.GeomSetPosition(m_terrainGeom, m_regionWidth * 0.5f, m_regionHeight * 0.5f, 0.0f);
            }
        }

        public override void DeleteTerrain()
        {
        }

        public float GetWaterLevel()
        {
            return waterlevel;
        }

        public override void SetWaterLevel(float baseheight)
        {
            waterlevel = baseheight;
        }

        public override void Dispose()
        {
            lock(SimulationLock)
            lock (OdeLock)
            {
                if (world == IntPtr.Zero)
                    return;

                SafeNativeMethods.AllocateODEDataForThread(~0U);

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

                foreach (OdeCharacter ch in chtorem)
                    ch.DoAChange(changes.Remove, null);

                if (m_terrainGeom != IntPtr.Zero)
                        SafeNativeMethods.GeomDestroy(m_terrainGeom);
                m_terrainGeom = IntPtr.Zero;

                if (m_terrainHeightsHandler.IsAllocated)
                    m_terrainHeightsHandler.Free();

                m_terrainHeights = null;
                m_lastRegionWidth = 0;
                m_lastRegionHeight = 0;

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

                SafeNativeMethods.WorldDestroy(world);
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
            topColliders = orderedPrims.Take(25).ToDictionary(p => p.LocalID, p => p.CollisionScore);

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
