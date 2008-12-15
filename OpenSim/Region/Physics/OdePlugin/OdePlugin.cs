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
 *     * Neither the name of the OpenSim Project nor the
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
//#define USE_DRAWSTUFF

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using log4net;
using Nini.Config;
using Ode.NET;
#if USE_DRAWSTUFF
using Drawstuff.NET;
#endif 
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using OpenMetaverse;

//using OpenSim.Region.Physics.OdePlugin.Meshing;

namespace OpenSim.Region.Physics.OdePlugin
{
    /// <summary>
    /// ODE plugin
    /// </summary>
    public class OdePlugin : IPhysicsPlugin
    {
        //private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private CollisionLocker ode;
        private OdeScene _mScene;

        public OdePlugin()
        {
            ode = new CollisionLocker();
        }

        public bool Init()
        {
            return true;
        }

        public PhysicsScene GetScene(String sceneIdentifier)
        {
            if (_mScene == null)
            {
                // Initializing ODE only when a scene is created allows alternative ODE plugins to co-habit (according to
                // http://opensimulator.org/mantis/view.php?id=2750).
                d.InitODE();
                
                _mScene = new OdeScene(ode, sceneIdentifier);
            }
            return (_mScene);
        }

        public string GetName()
        {
            return ("OpenDynamicsEngine");
        }

        public void Dispose()
        {
        }
    }

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

    [Flags]
    public enum CollisionCategories : int
    {
        Disabled = 0,
        Geom = 0x00000001,
        Body = 0x00000002,
        Space = 0x00000004,
        Character = 0x00000008,
        Land = 0x00000010,
        Water = 0x00000020,
        Wind = 0x00000040,
        Sensor = 0x00000080,
        Selected = 0x00000100
    }

    public class OdeScene : PhysicsScene
    {
        private ILog m_log;
        // private Dictionary<string, sCollisionData> m_storedCollisions = new Dictionary<string, sCollisionData>();

        CollisionLocker ode;

        protected Random fluidRandomizer = new Random(Environment.TickCount);

        private const uint m_regionWidth = Constants.RegionSize;
        private const uint m_regionHeight = Constants.RegionSize;

        private float ODE_STEPSIZE = 0.020f;
        private float metersInSpace = 29.9f;

        public float gravityx = 0f;
        public float gravityy = 0f;
        public float gravityz = -9.8f;

        private float contactsurfacelayer = 0.001f;

        private int worldHashspaceLow = -4;
        private int worldHashspaceHigh = 128;

        private int smallHashspaceLow = -4;
        private int smallHashspaceHigh = 66;

        private float waterlevel = 0f;
        private int framecount = 0;
        //private int m_returncollisions = 10;

        private IntPtr contactgroup;
        private IntPtr LandGeom;

        private IntPtr WaterGeom;

        private float nmTerrainContactFriction = 255.0f;
        private float nmTerrainContactBounce = 0.1f;
        private float nmTerrainContactERP = 0.1025f;

        private float mTerrainContactFriction = 75f;
        private float mTerrainContactBounce = 0.1f;
        private float mTerrainContactERP = 0.05025f;

        private float nmAvatarObjectContactFriction = 250f;
        private float nmAvatarObjectContactBounce = 0.1f;

        private float mAvatarObjectContactFriction = 75f;
        private float mAvatarObjectContactBounce = 0.1f;

        private float avPIDD = 3200f;
        private float avPIDP = 1400f;
        private float avCapRadius = 0.37f;
        private float avStandupTensor = 2000000f;
        private float avDensity = 80f;
        private float avHeightFudgeFactor = 0.52f;
        private float avMovementDivisorWalk = 1.3f;
        private float avMovementDivisorRun = 0.8f;

        public bool meshSculptedPrim = true;

        public float meshSculptLOD = 32;
        public float MeshSculptphysicalLOD = 16;

        public float geomDefaultDensity = 10.000006836f;

        public int geomContactPointsStartthrottle = 3;
        public int geomUpdatesPerThrottledUpdate = 15;

        public float bodyPIDD = 35f;
        public float bodyPIDG = 25;

        public int geomCrossingFailuresBeforeOutofbounds = 5;

        public float bodyMotorJointMaxforceTensor = 2;

        public int bodyFramesAutoDisable = 20;

        private float[] _heightmap;

        private float[] _watermap;
        private bool m_filterCollisions = true;

        // private float[] _origheightmap;

        private d.NearCallback nearCallback;
        public d.TriCallback triCallback;
        public d.TriArrayCallback triArrayCallback;
        private List<OdeCharacter> _characters = new List<OdeCharacter>();
        private List<OdePrim> _prims = new List<OdePrim>();
        private List<OdePrim> _activeprims = new List<OdePrim>();
        private List<OdePrim> _taintedPrim = new List<OdePrim>();
        private List<OdeCharacter> _taintedActors = new List<OdeCharacter>();
        private List<d.ContactGeom> _perloopContact = new List<d.ContactGeom>();
        private List<PhysicsActor> _collisionEventPrim = new List<PhysicsActor>();
        public Dictionary<IntPtr, String> geom_name_map = new Dictionary<IntPtr, String>();
        public Dictionary<IntPtr, PhysicsActor> actor_name_map = new Dictionary<IntPtr, PhysicsActor>();
        private d.ContactGeom[] contacts = new d.ContactGeom[80];

        private d.Contact contact;
        private d.Contact TerrainContact;
        private d.Contact AvatarMovementprimContact;
        private d.Contact AvatarMovementTerrainContact;
        private d.Contact WaterContact;

//Ckrinke: Comment out until used. We declare it, initialize it, but do not use it
//Ckrinke        private int m_randomizeWater = 200;
        private int m_physicsiterations = 10;
        private float m_SkipFramesAtms = 0.40f; // Drop frames gracefully at a 400 ms lag
        private PhysicsActor PANull = new NullPhysicsActor();
        private float step_time = 0.0f;
//Ckrinke: Comment out until used. We declare it, initialize it, but do not use it
//Ckrinke        private int ms = 0;
        public IntPtr world;
        //private bool returncollisions = false;
        // private uint obj1LocalID = 0;
        private uint obj2LocalID = 0;
        //private int ctype = 0;
        private OdeCharacter cc1;
        private OdePrim cp1;
        private OdeCharacter cc2;
        private OdePrim cp2;
        //private int cStartStop = 0;
        //private string cDictKey = "";

        public IntPtr space;

        //private IntPtr tmpSpace;
        // split static geometry collision handling into spaces of 30 meters
        public IntPtr[,] staticPrimspace;

        public Object OdeLock;

        public IMesher mesher;

        private IConfigSource m_config;

        public bool physics_logging = false;
        public int physics_logging_interval = 0;
        public bool physics_logging_append_existing_logfile = false;

        public d.Vector3 xyz = new d.Vector3(128.1640f, 128.3079f, 25.7600f);
        public d.Vector3 hpr = new d.Vector3(125.5000f, -17.0000f, 0.0000f);

        /// <summary>
        /// Initiailizes the scene
        /// Sets many properties that ODE requires to be stable
        /// These settings need to be tweaked 'exactly' right or weird stuff happens.
        /// </summary>
        public OdeScene(CollisionLocker dode, string sceneIdentifier)
        {
            m_log 
                = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.ToString() + "." + sceneIdentifier);

            OdeLock = new Object();
            ode = dode;
            nearCallback = near;
            triCallback = TriCallback;
            triArrayCallback = TriArrayCallback;

            lock (OdeLock)
            {
                // Create the world and the first space
                world = d.WorldCreate();
                space = d.HashSpaceCreate(IntPtr.Zero);

                contactgroup = d.JointGroupCreate(0);
                //contactgroup

                d.WorldSetAutoDisableFlag(world, false);
                #if USE_DRAWSTUFF
                
                Thread viewthread = new Thread(new ParameterizedThreadStart(startvisualization));
                viewthread.Start();
                #endif
            }

            // zero out a heightmap array float array (single dimention [flattened]))
            _heightmap = new float[514*514];
            _watermap = new float[258 * 258];

            // Zero out the prim spaces array (we split our space into smaller spaces so
            // we can hit test less.
        }

#if USE_DRAWSTUFF
        public void startvisualization(object o)
        {
            ds.Functions fn;
            fn.version = ds.VERSION;
            fn.start = new ds.CallbackFunction(start);
            fn.step = new ds.CallbackFunction(step);
            fn.command = new ds.CallbackFunction(command);
            fn.stop = null;
            fn.path_to_textures = "./textures";
            string[] args = new string[0];
            ds.SimulationLoop(args.Length, args, 352, 288, ref fn);
        }
#endif

        // Initialize the mesh plugin
        public override void Initialise(IMesher meshmerizer, IConfigSource config)
        {
            mesher = meshmerizer;
            m_config = config;
            // Defaults

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                avPIDD = 3200.0f;
                avPIDP = 1400.0f;
                avStandupTensor = 2000000f;
            }
            else
            {
                avPIDD = 2200.0f;
                avPIDP = 900.0f;
                avStandupTensor = 550000f;
            }

            if (m_config != null)
            {
                IConfig physicsconfig = m_config.Configs["ODEPhysicsSettings"];
                if (physicsconfig != null)
                {
                    gravityx = physicsconfig.GetFloat("world_gravityx", 0f);
                    gravityy = physicsconfig.GetFloat("world_gravityy", 0f);
                    gravityz = physicsconfig.GetFloat("world_gravityz", -9.8f);

                    worldHashspaceLow = physicsconfig.GetInt("world_hashspace_low", -4);
                    worldHashspaceHigh = physicsconfig.GetInt("world_hashspace_high", 128);

                    metersInSpace = physicsconfig.GetFloat("meters_in_small_space", 29.9f);
                    smallHashspaceLow = physicsconfig.GetInt("small_hashspace_size_low", -4);
                    smallHashspaceHigh = physicsconfig.GetInt("small_hashspace_size_high", 66);

                    contactsurfacelayer = physicsconfig.GetFloat("world_contact_surface_layer", 0.001f);

                    nmTerrainContactFriction = physicsconfig.GetFloat("nm_terraincontact_friction", 255.0f);
                    nmTerrainContactBounce = physicsconfig.GetFloat("nm_terraincontact_bounce", 0.1f);
                    nmTerrainContactERP = physicsconfig.GetFloat("nm_terraincontact_erp", 0.1025f);

                    mTerrainContactFriction = physicsconfig.GetFloat("m_terraincontact_friction", 75f);
                    mTerrainContactBounce = physicsconfig.GetFloat("m_terraincontact_bounce", 0.1f);
                    mTerrainContactERP = physicsconfig.GetFloat("m_terraincontact_erp", 0.05025f);

                    nmAvatarObjectContactFriction = physicsconfig.GetFloat("objectcontact_friction", 250f);
                    nmAvatarObjectContactBounce = physicsconfig.GetFloat("objectcontact_bounce", 0.2f);

                    mAvatarObjectContactFriction = physicsconfig.GetFloat("m_avatarobjectcontact_friction", 75f);
                    mAvatarObjectContactBounce = physicsconfig.GetFloat("m_avatarobjectcontact_bounce", 0.1f);

                    ODE_STEPSIZE = physicsconfig.GetFloat("world_stepsize", 0.020f);
                    m_physicsiterations = physicsconfig.GetInt("world_internal_steps_without_collisions", 10);

                    avDensity = physicsconfig.GetFloat("av_density", 80f);
                    avHeightFudgeFactor = physicsconfig.GetFloat("av_height_fudge_factor", 0.52f);
                    avMovementDivisorWalk = physicsconfig.GetFloat("av_movement_divisor_walk", 1.3f);
                    avMovementDivisorRun = physicsconfig.GetFloat("av_movement_divisor_run", 0.8f);
                    avCapRadius = physicsconfig.GetFloat("av_capsule_radius", 0.37f);

                    geomContactPointsStartthrottle = physicsconfig.GetInt("geom_contactpoints_start_throttling", 3);
                    geomUpdatesPerThrottledUpdate = physicsconfig.GetInt("geom_updates_before_throttled_update", 15);
                    geomCrossingFailuresBeforeOutofbounds = physicsconfig.GetInt("geom_crossing_faiures_before_outofbounds", 5);

                    geomDefaultDensity = physicsconfig.GetFloat("geometry_default_density", 10.000006836f);
                    bodyFramesAutoDisable = physicsconfig.GetInt("body_frames_auto_disable", 20);

                    bodyPIDD = physicsconfig.GetFloat("body_pid_derivative", 35f);
                    bodyPIDG = physicsconfig.GetFloat("body_pid_gain", 25f);

                    meshSculptedPrim = physicsconfig.GetBoolean("mesh_sculpted_prim", true);
                    meshSculptLOD = physicsconfig.GetFloat("mesh_lod", 32f);
                    MeshSculptphysicalLOD = physicsconfig.GetFloat("mesh_physical_lod", 16f);
                    m_filterCollisions = physicsconfig.GetBoolean("filter_collisions", true);

                    if (Environment.OSVersion.Platform == PlatformID.Unix)
                    {
                        avPIDD = physicsconfig.GetFloat("av_pid_derivative_linux", 2200.0f);
                        avPIDP = physicsconfig.GetFloat("av_pid_proportional_linux", 900.0f);
                        avStandupTensor = physicsconfig.GetFloat("av_capsule_standup_tensor_linux", 550000f);
                        bodyMotorJointMaxforceTensor = physicsconfig.GetFloat("body_motor_joint_maxforce_tensor_linux", 5f);
                    }
                    else
                    {
                        avPIDD = physicsconfig.GetFloat("av_pid_derivative_win", 2200.0f);
                        avPIDP = physicsconfig.GetFloat("av_pid_proportional_win", 900.0f);
                        avStandupTensor = physicsconfig.GetFloat("av_capsule_standup_tensor_win", 550000f);
                        bodyMotorJointMaxforceTensor = physicsconfig.GetFloat("body_motor_joint_maxforce_tensor_win", 5f);
                    }

                    physics_logging = physicsconfig.GetBoolean("physics_logging", false);
                    physics_logging_interval = physicsconfig.GetInt("physics_logging_interval", 0);
                    physics_logging_append_existing_logfile = physicsconfig.GetBoolean("physics_logging_append_existing_logfile", false);
                }
            }

            staticPrimspace = new IntPtr[(int)(300 / metersInSpace), (int)(300 / metersInSpace)];

            // Centeral contact friction and bounce
            // ckrinke 11/10/08 Enabling soft_erp but not soft_cfm until I figure out why
            // an avatar falls through in Z but not in X or Y when walking on a prim.
            contact.surface.mode |= d.ContactFlags.SoftERP;
            contact.surface.mu = nmAvatarObjectContactFriction;
            contact.surface.bounce = nmAvatarObjectContactBounce;
            contact.surface.soft_cfm = 0.010f;
            contact.surface.soft_erp = 0.010f;

            // Terrain contact friction and Bounce
            // This is the *non* moving version.   Use this when an avatar
            // isn't moving to keep it in place better
            TerrainContact.surface.mode |= d.ContactFlags.SoftERP;
            TerrainContact.surface.mu = nmTerrainContactFriction;
            TerrainContact.surface.bounce = nmTerrainContactBounce;
            TerrainContact.surface.soft_erp = nmTerrainContactERP;

            WaterContact.surface.mode |= (d.ContactFlags.SoftERP | d.ContactFlags.SoftCFM);
            WaterContact.surface.mu = 0f; // No friction
            WaterContact.surface.bounce = 0.0f; // No bounce
            WaterContact.surface.soft_cfm = 0.010f;
            WaterContact.surface.soft_erp = 0.010f;

            // Prim contact friction and bounce
            // THis is the *non* moving version of friction and bounce
            // Use this when an avatar comes in contact with a prim
            // and is moving
            AvatarMovementprimContact.surface.mu = mAvatarObjectContactFriction;
            AvatarMovementprimContact.surface.bounce = mAvatarObjectContactBounce;

            // Terrain contact friction bounce and various error correcting calculations
            // Use this when an avatar is in contact with the terrain and moving.
            AvatarMovementTerrainContact.surface.mode |= d.ContactFlags.SoftERP;
            AvatarMovementTerrainContact.surface.mu = mTerrainContactFriction;
            AvatarMovementTerrainContact.surface.bounce = mTerrainContactBounce;
            AvatarMovementTerrainContact.surface.soft_erp = mTerrainContactERP;

            d.HashSpaceSetLevels(space, worldHashspaceLow, worldHashspaceHigh);

            // Set the gravity,, don't disable things automatically (we set it explicitly on some things)

            d.WorldSetGravity(world, gravityx, gravityy, gravityz);
            d.WorldSetContactSurfaceLayer(world, contactsurfacelayer);

            // Set how many steps we go without running collision testing
            // This is in addition to the step size.
            // Essentially Steps * m_physicsiterations
            d.WorldSetQuickStepNumIterations(world, m_physicsiterations);
            //d.WorldSetContactMaxCorrectingVel(world, 1000.0f);

            for (int i = 0; i < staticPrimspace.GetLength(0); i++)
            {
                for (int j = 0; j < staticPrimspace.GetLength(1); j++)
                {
                    staticPrimspace[i, j] = IntPtr.Zero;
                }
            }
        }

        internal void waitForSpaceUnlock(IntPtr space)
        {
            //if (space != IntPtr.Zero)
                //while (d.SpaceLockQuery(space)) { } // Wait and do nothing
        }

        /// <summary>
        /// Debug space message for printing the space that a prim/avatar is in.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns>Returns which split up space the given position is in.</returns>
        public string whichspaceamIin(PhysicsVector pos)
        {
            return calculateSpaceForGeom(pos).ToString();
        }

        #region Collision Detection

        /// <summary>
        /// This is our near callback.  A geometry is near a body
        /// </summary>
        /// <param name="space">The space that contains the geoms.  Remember, spaces are also geoms</param>
        /// <param name="g1">a geometry or space</param>
        /// <param name="g2">another geometry or space</param>
        private void near(IntPtr space, IntPtr g1, IntPtr g2)
        {
            //  no lock here!  It's invoked from within Simulate(), which is thread-locked

            // Test if we're colliding a geom with a space.
            // If so we have to drill down into the space recursively

            if (d.GeomIsSpace(g1) || d.GeomIsSpace(g2))
            {
                if (g1 == IntPtr.Zero || g2 == IntPtr.Zero)
                    return;
                
                // Separating static prim geometry spaces.
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
                //Colliding a space or a geom with a space or a geom. so drill down

                //Collide all geoms in each space..
                //if (d.GeomIsSpace(g1)) d.SpaceCollide(g1, IntPtr.Zero, nearCallback);
                //if (d.GeomIsSpace(g2)) d.SpaceCollide(g2, IntPtr.Zero, nearCallback);
                return;
            }

            if (g1 == IntPtr.Zero || g2 == IntPtr.Zero)
                return;

            IntPtr b1 = d.GeomGetBody(g1);
            IntPtr b2 = d.GeomGetBody(g2);

            // d.GeomClassID id = d.GeomGetClass(g1);

            String name1 = null;
            String name2 = null;

            if (!geom_name_map.TryGetValue(g1, out name1))
            {
                name1 = "null";
            }
            if (!geom_name_map.TryGetValue(g2, out name2))
            {
                name2 = "null";
            }

            //if (id == d.GeomClassId.TriMeshClass)
            //{
                //               m_log.InfoFormat("near: A collision was detected between {1} and {2}", 0, name1, name2);
                //System.Console.WriteLine("near: A collision was detected between {1} and {2}", 0, name1, name2);
            //}

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

                lock (contacts)
                {
                    count = d.Collide(g1, g2, contacts.GetLength(0), contacts, d.ContactGeom.SizeOf);
                }
            }
            catch (SEHException)
            {
                m_log.Error("[PHYSICS]: The Operating system shut down ODE because of corrupt memory.  This could be a result of really irregular terrain.  If this repeats continuously, restart using Basic Physics and terrain fill your terrain.  Restarting the sim.");
                ode.drelease(world);
                base.TriggerPhysicsBasedRestart();
            }
            catch (AccessViolationException)
            {
                m_log.Warn("[PHYSICS]: Unable to collide test an object");
                return;
            }

            PhysicsActor p1;
            PhysicsActor p2;

            if (!actor_name_map.TryGetValue(g1, out p1))
            {
                p1 = PANull;
            }

            if (!actor_name_map.TryGetValue(g2, out p2))
            {
                p2 = PANull;
            }

            float max_collision_depth = 0f;
            if (p1.CollisionScore + count >= float.MaxValue)
                p1.CollisionScore = 0;
            p1.CollisionScore += count;

            if (p2.CollisionScore + count >= float.MaxValue)
                p2.CollisionScore = 0;
            p2.CollisionScore += count;

            for (int i = 0; i < count; i++)
            {


                max_collision_depth = (contacts[i].depth > max_collision_depth) ? contacts[i].depth : max_collision_depth;
                //m_log.Warn("[CCOUNT]: " + count);
                IntPtr joint;
                // If we're colliding with terrain, use 'TerrainContact' instead of contact.
                // allows us to have different settings
                
                // We only need to test p2 for 'jump crouch purposes'
                p2.IsColliding = true;
                
                //if ((framecount % m_returncollisions) == 0)

                switch (p1.PhysicsActorType)
                {
                    case (int)ActorTypes.Agent:
                        p2.CollidingObj = true;
                        break;
                    case (int)ActorTypes.Prim:
                        if (p2.Velocity.X > 0 || p2.Velocity.Y > 0 || p2.Velocity.Z > 0)
                            p2.CollidingObj = true;
                        break;
                    case (int)ActorTypes.Unknown:
                        p2.CollidingGround = true;
                        break;
                    default:
                        p2.CollidingGround = true;
                        break;
                }

                // we don't want prim or avatar to explode

                #region InterPenetration Handling - Unintended physics explosions

                if (contacts[i].depth >= 0.08f)
                {
                    //This is disabled at the moment only because it needs more tweaking
                    //It will eventually be uncommented

                    if (contacts[i].depth >= 1.00f)
                    {
                        //m_log.Debug("[PHYSICS]: " + contacts[i].depth.ToString());
                    }

                    //If you interpenetrate a prim with an agent
                    if ((p2.PhysicsActorType == (int) ActorTypes.Agent &&
                         p1.PhysicsActorType == (int) ActorTypes.Prim) ||
                        (p1.PhysicsActorType == (int) ActorTypes.Agent &&
                         p2.PhysicsActorType == (int) ActorTypes.Prim))
                    {
                        # region disabled code1
                        //contacts[i].depth = contacts[i].depth * 4.15f;
                        /*
                        if (p2.PhysicsActorType == (int) ActorTypes.Agent)
                        {
                            p2.CollidingObj = true;
                            contacts[i].depth = 0.003f;
                            p2.Velocity = p2.Velocity + new PhysicsVector(0, 0, 2.5f);
                            OdeCharacter character = (OdeCharacter) p2;
                            character.SetPidStatus(true);
                            contacts[i].pos = new d.Vector3(contacts[i].pos.X + (p1.Size.X / 2), contacts[i].pos.Y + (p1.Size.Y / 2), contacts[i].pos.Z + (p1.Size.Z / 2));

                        }
                        else
                        {

                            //contacts[i].depth = 0.0000000f;
                        }
                        if (p1.PhysicsActorType == (int) ActorTypes.Agent)
                        {

                            p1.CollidingObj = true;
                            contacts[i].depth = 0.003f;
                            p1.Velocity = p1.Velocity + new PhysicsVector(0, 0, 2.5f);
                            contacts[i].pos = new d.Vector3(contacts[i].pos.X + (p2.Size.X / 2), contacts[i].pos.Y + (p2.Size.Y / 2), contacts[i].pos.Z + (p2.Size.Z / 2));
                            OdeCharacter character = (OdeCharacter)p1;
                            character.SetPidStatus(true);
                        }
                        else
                        {

                            //contacts[i].depth = 0.0000000f;
                        }
                          */
                        #endregion
                    }

                    // If you interpenetrate a prim with another prim
                    if (p1.PhysicsActorType == (int) ActorTypes.Prim && p2.PhysicsActorType == (int) ActorTypes.Prim)
                    {
                        #region disabledcode2
                        //OdePrim op1 = (OdePrim)p1;
                        //OdePrim op2 = (OdePrim)p2;
                        //op1.m_collisionscore++;
                        //op2.m_collisionscore++;

                        //if (op1.m_collisionscore > 8000 || op2.m_collisionscore > 8000)
                        //{
                            //op1.m_taintdisable = true;
                            //AddPhysicsActorTaint(p1);
                            //op2.m_taintdisable = true;
                            //AddPhysicsActorTaint(p2);
                        //}

                        //if (contacts[i].depth >= 0.25f)
                        //{
                            // Don't collide, one or both prim will expld.

                            //op1.m_interpenetrationcount++;
                            //op2.m_interpenetrationcount++;
                            //interpenetrations_before_disable = 200;
                            //if (op1.m_interpenetrationcount >= interpenetrations_before_disable)
                            //{
                                //op1.m_taintdisable = true;
                                //AddPhysicsActorTaint(p1);
                            //}
                            //if (op2.m_interpenetrationcount >= interpenetrations_before_disable)
                            //{
                               // op2.m_taintdisable = true;
                                //AddPhysicsActorTaint(p2);
                            //}

                            //contacts[i].depth = contacts[i].depth / 8f;
                            //contacts[i].normal = new d.Vector3(0, 0, 1);
                        //}
                        //if (op1.m_disabled || op2.m_disabled)
                        //{
                            //Manually disabled objects stay disabled
                            //contacts[i].depth = 0f;
                        //}
                        #endregion
                    }

                    if (contacts[i].depth >= 1.00f)
                    {
                        //m_log.Info("[P]: " + contacts[i].depth.ToString());
                        if ((p2.PhysicsActorType == (int) ActorTypes.Agent &&
                             p1.PhysicsActorType == (int) ActorTypes.Unknown) ||
                            (p1.PhysicsActorType == (int) ActorTypes.Agent &&
                             p2.PhysicsActorType == (int) ActorTypes.Unknown))
                        {
                            if (p2.PhysicsActorType == (int) ActorTypes.Agent)
                            {
                                OdeCharacter character = (OdeCharacter) p2;

                                //p2.CollidingObj = true;
                                contacts[i].depth = 0.00000003f;
                                p2.Velocity = p2.Velocity + new PhysicsVector(0, 0, 0.5f);
                                contacts[i].pos =
                                    new d.Vector3(contacts[i].pos.X + (p1.Size.X/2),
                                                  contacts[i].pos.Y + (p1.Size.Y/2),
                                                  contacts[i].pos.Z + (p1.Size.Z/2));
                                character.SetPidStatus(true);
                            }
                            else
                            {
                            }

                            if (p1.PhysicsActorType == (int) ActorTypes.Agent)
                            {
                                OdeCharacter character = (OdeCharacter)p1;

                                //p2.CollidingObj = true;
                                contacts[i].depth = 0.00000003f;
                                p1.Velocity = p1.Velocity + new PhysicsVector(0, 0, 0.5f);
                                contacts[i].pos =
                                    new d.Vector3(contacts[i].pos.X + (p1.Size.X/2),
                                                  contacts[i].pos.Y + (p1.Size.Y/2),
                                                  contacts[i].pos.Z + (p1.Size.Z/2));
                                character.SetPidStatus(true);
                            }
                            else
                            {
                                //contacts[i].depth = 0.0000000f;
                            }
                        }
                    }
                }

                #endregion

                if (contacts[i].depth >= 0f && !checkDupe(contacts[i], p2.PhysicsActorType))
                {
                    // If we're colliding against terrain
                    if (name1 == "Terrain" || name2 == "Terrain")
                    {
                        // If we're moving
                        if ((p2.PhysicsActorType == (int) ActorTypes.Agent) &&
                            (Math.Abs(p2.Velocity.X) > 0.01f || Math.Abs(p2.Velocity.Y) > 0.01f))
                        {
                            // Use the movement terrain contact
                            AvatarMovementTerrainContact.geom = contacts[i];
                            _perloopContact.Add(contacts[i]);
                            joint = d.JointCreateContact(world, contactgroup, ref AvatarMovementTerrainContact);
                        }
                        else
                        {
                            // Use the non moving terrain contact
                            TerrainContact.geom = contacts[i];
                            _perloopContact.Add(contacts[i]);
                            joint = d.JointCreateContact(world, contactgroup, ref TerrainContact);
                        }
                        //if (p2.PhysicsActorType == (int)ActorTypes.Prim)
                        //{
                            //m_log.Debug("[PHYSICS]: prim contacting with ground");
                        //}
                    }
                    else if (name1 == "Water" || name2 == "Water")
                    {
                        if ((p2.PhysicsActorType == (int)ActorTypes.Prim))
                        {
                        }
                        else
                        {
                        }

                        //WaterContact.surface.soft_cfm = 0.0000f;
                        //WaterContact.surface.soft_erp = 0.00000f;
                        if (contacts[i].depth > 0.1f)
                        {
                            contacts[i].depth *= 52;
                            //contacts[i].normal = new d.Vector3(0, 0, 1);
                            //contacts[i].pos = new d.Vector3(0, 0, contacts[i].pos.Z - 5f);
                        }
                        WaterContact.geom = contacts[i];
                        _perloopContact.Add(contacts[i]);
                        joint = d.JointCreateContact(world, contactgroup, ref WaterContact);

                        //m_log.Info("[PHYSICS]: Prim Water Contact" + contacts[i].depth);
                    }
                    else
                    {   // we're colliding with prim or avatar
                        // check if we're moving
                        if ((p2.PhysicsActorType == (int)ActorTypes.Agent) &&
                            (Math.Abs(p2.Velocity.X) > 0.01f || Math.Abs(p2.Velocity.Y) > 0.01f))
                        {
                            // Use the Movement prim contact
                            AvatarMovementprimContact.geom = contacts[i];
                            _perloopContact.Add(contacts[i]);
                            joint = d.JointCreateContact(world, contactgroup, ref AvatarMovementprimContact);
                        }
                        else
                        {   // Use the non movement contact
                            contact.geom = contacts[i];
                            _perloopContact.Add(contacts[i]);
                            joint = d.JointCreateContact(world, contactgroup, ref contact);
                        }
                    }
                    d.JointAttach(joint, b1, b2);
                }
                collision_accounting_events(p1, p2, max_collision_depth);
                if (count > geomContactPointsStartthrottle)
                {
                    // If there are more then 3 contact points, it's likely
                    // that we've got a pile of objects, so ...
                    // We don't want to send out hundreds of terse updates over and over again
                    // so lets throttle them and send them again after it's somewhat sorted out.
                    p2.ThrottleUpdates = true;
                }
                //System.Console.WriteLine(count.ToString());
                //System.Console.WriteLine("near: A collision was detected between {1} and {2}", 0, name1, name2);
            }
        }

        private bool checkDupe(d.ContactGeom contactGeom, int atype)
        {
            bool result = false;
            //return result;
            if (!m_filterCollisions)
                return false;

            ActorTypes at = (ActorTypes)atype;
            lock (_perloopContact)
            {
                foreach (d.ContactGeom contact in _perloopContact)
                {
                    //if ((contact.g1 == contactGeom.g1 && contact.g2 == contactGeom.g2))
                    //{
                        // || (contact.g2 == contactGeom.g1 && contact.g1 == contactGeom.g2)
                    if (at == ActorTypes.Agent)
                    {
                            if (((Math.Abs(contactGeom.normal.X - contact.normal.X) < 1.026f) && (Math.Abs(contactGeom.normal.Y - contact.normal.Y) < 0.303f) && (Math.Abs(contactGeom.normal.Z - contact.normal.Z) < 0.065f)) && contactGeom.g1 != LandGeom && contactGeom.g2 != LandGeom)
                            {
                                
                                if (Math.Abs(contact.depth - contactGeom.depth) < 0.052f)
                                {
                                    //contactGeom.depth *= .00005f;
                                   //m_log.DebugFormat("[Collsion]: Depth {0}", Math.Abs(contact.depth - contactGeom.depth));
                                    // m_log.DebugFormat("[Collision]: <{0},{1},{2}>", Math.Abs(contactGeom.normal.X - contact.normal.X), Math.Abs(contactGeom.normal.Y - contact.normal.Y), Math.Abs(contactGeom.normal.Z - contact.normal.Z));
                                    result = true;
                                    break;
                                }
                                else
                                {
                                    //m_log.DebugFormat("[Collsion]: Depth {0}", Math.Abs(contact.depth - contactGeom.depth));
                                }
                            }
                            else
                            {
                                //m_log.DebugFormat("[Collision]: <{0},{1},{2}>", Math.Abs(contactGeom.normal.X - contact.normal.X), Math.Abs(contactGeom.normal.Y - contact.normal.Y), Math.Abs(contactGeom.normal.Z - contact.normal.Z));
                                //int i = 0;
                            }
                    } 
                    else if (at == ActorTypes.Prim)
                    {
                            //d.AABB aabb1 = new d.AABB();
                            //d.AABB aabb2 = new d.AABB();

                            //d.GeomGetAABB(contactGeom.g2, out aabb2);
                            //d.GeomGetAABB(contactGeom.g1, out aabb1);
                            //aabb1.
                            if (((Math.Abs(contactGeom.normal.X - contact.normal.X) < 1.026f) && (Math.Abs(contactGeom.normal.Y - contact.normal.Y) < 0.303f) && (Math.Abs(contactGeom.normal.Z - contact.normal.Z) < 0.065f)) && contactGeom.g1 != LandGeom && contactGeom.g2 != LandGeom)
                            {
                                if (contactGeom.normal.X == contact.normal.X && contactGeom.normal.Y == contact.normal.Y && contactGeom.normal.Z == contact.normal.Z)
                                {
                                    if (Math.Abs(contact.depth - contactGeom.depth) < 0.272f)
                                    {
                                        result = true;
                                        break;
                                    }
                                }
                                //m_log.DebugFormat("[Collsion]: Depth {0}", Math.Abs(contact.depth - contactGeom.depth));
                                //m_log.DebugFormat("[Collision]: <{0},{1},{2}>", Math.Abs(contactGeom.normal.X - contact.normal.X), Math.Abs(contactGeom.normal.Y - contact.normal.Y), Math.Abs(contactGeom.normal.Z - contact.normal.Z));
                            }
                            
                    }
                    
                    //}

                }
            }
            return result;
        }

        private void collision_accounting_events(PhysicsActor p1, PhysicsActor p2, float collisiondepth)
        {
            // obj1LocalID = 0;
            //returncollisions = false;
            obj2LocalID = 0;
            //ctype = 0;
            //cStartStop = 0;
            if (!p2.SubscribedEvents() && !p1.SubscribedEvents())
                return;

            switch ((ActorTypes)p2.PhysicsActorType)
            {
                case ActorTypes.Agent:
                    cc2 = (OdeCharacter)p2;

                    // obj1LocalID = cc2.m_localID;
                    switch ((ActorTypes)p1.PhysicsActorType)
                    {
                        case ActorTypes.Agent:
                            cc1 = (OdeCharacter)p1;
                            obj2LocalID = cc1.m_localID;
                            cc1.AddCollisionEvent(cc2.m_localID, collisiondepth);
                            //ctype = (int)CollisionCategories.Character;

                            //if (cc1.CollidingObj)
                            //cStartStop = (int)StatusIndicators.Generic;
                            //else
                            //cStartStop = (int)StatusIndicators.Start;

                            //returncollisions = true;
                            break;
                        case ActorTypes.Prim:
                            cp1 = (OdePrim)p1;
                            obj2LocalID = cp1.m_localID;
                            cp1.AddCollisionEvent(cc2.m_localID, collisiondepth);
                            //ctype = (int)CollisionCategories.Geom;

                            //if (cp1.CollidingObj)
                            //cStartStop = (int)StatusIndicators.Generic;
                            //else
                            //cStartStop = (int)StatusIndicators.Start;

                            //returncollisions = true;
                            break;

                        case ActorTypes.Ground:
                        case ActorTypes.Unknown:
                            obj2LocalID = 0;
                            //ctype = (int)CollisionCategories.Land;
                            //returncollisions = true;
                            break;
                    }

                    cc2.AddCollisionEvent(obj2LocalID, collisiondepth);
                    break;
                case ActorTypes.Prim:
                    cp2 = (OdePrim)p2;

                    // obj1LocalID = cp2.m_localID;
                    switch ((ActorTypes)p1.PhysicsActorType)
                    {
                        case ActorTypes.Agent:
                            cc1 = (OdeCharacter)p1;
                            obj2LocalID = cc1.m_localID;
                            cc1.AddCollisionEvent(cp2.m_localID, collisiondepth);
                            //ctype = (int)CollisionCategories.Character;

                            //if (cc1.CollidingObj)
                                //cStartStop = (int)StatusIndicators.Generic;
                            //else
                                //cStartStop = (int)StatusIndicators.Start;
                            //returncollisions = true;

                            break;
                        case ActorTypes.Prim:
                            cp1 = (OdePrim)p1;
                            obj2LocalID = cp1.m_localID;
                            cp1.AddCollisionEvent(cp2.m_localID, collisiondepth);
                            //ctype = (int)CollisionCategories.Geom;

                            //if (cp1.CollidingObj)
                                //cStartStop = (int)StatusIndicators.Generic;
                            //else
                                //cStartStop = (int)StatusIndicators.Start;

                            //returncollisions = true;
                            break;

                        case ActorTypes.Ground:
                        case ActorTypes.Unknown:
                            obj2LocalID = 0;
                            //ctype = (int)CollisionCategories.Land;

                            //returncollisions = true;
                            break;
                    }

                    cp2.AddCollisionEvent(obj2LocalID, collisiondepth);
                    break;
            }
            //if (returncollisions)
            //{

                //lock (m_storedCollisions)
                //{
                    //cDictKey = obj1LocalID.ToString() + obj2LocalID.ToString() + cStartStop.ToString() + ctype.ToString();
                    //if (m_storedCollisions.ContainsKey(cDictKey))
                    //{
                        //sCollisionData objd = m_storedCollisions[cDictKey];
                        //objd.NumberOfCollisions += 1;
                        //objd.lastframe = framecount;
                        //m_storedCollisions[cDictKey] = objd;
                    //}
                    //else
                    //{
                        //sCollisionData objd = new sCollisionData();
                        //objd.ColliderLocalId = obj1LocalID;
                        //objd.CollidedWithLocalId = obj2LocalID;
                        //objd.CollisionType = ctype;
                        //objd.NumberOfCollisions = 1;
                        //objd.lastframe = framecount;
                        //objd.StatusIndicator = cStartStop;
                        //m_storedCollisions.Add(cDictKey, objd);
                    //}
                //}
           // }
        }

        public int TriArrayCallback(IntPtr trimesh, IntPtr refObject, int[] triangleIndex, int triCount)
        {
            /*            String name1 = null;
                        String name2 = null;

                        if (!geom_name_map.TryGetValue(trimesh, out name1))
                        {
                            name1 = "null";
                        }
                        if (!geom_name_map.TryGetValue(refObject, out name2))
                        {
                            name2 = "null";
                        }

                        m_log.InfoFormat("TriArrayCallback: A collision was detected between {1} and {2}", 0, name1, name2);
            */
            return 1;
        }

        public int TriCallback(IntPtr trimesh, IntPtr refObject, int triangleIndex)
        {
            String name1 = null;
            String name2 = null;

            if (!geom_name_map.TryGetValue(trimesh, out name1))
            {
                name1 = "null";
            }

            if (!geom_name_map.TryGetValue(refObject, out name2))
            {
                name2 = "null";
            }

            //            m_log.InfoFormat("TriCallback: A collision was detected between {1} and {2}. Index was {3}", 0, name1, name2, triangleIndex);

            d.Vector3 v0 = new d.Vector3();
            d.Vector3 v1 = new d.Vector3();
            d.Vector3 v2 = new d.Vector3();

            d.GeomTriMeshGetTriangle(trimesh, 0, ref v0, ref v1, ref v2);
            //            m_log.DebugFormat("Triangle {0} is <{1},{2},{3}>, <{4},{5},{6}>, <{7},{8},{9}>", triangleIndex, v0.X, v0.Y, v0.Z, v1.X, v1.Y, v1.Z, v2.X, v2.Y, v2.Z);

            return 1;
        }

        /// <summary>
        /// This is our collision testing routine in ODE
        /// </summary>
        /// <param name="timeStep"></param>
        private void collision_optimized(float timeStep)
        {
            _perloopContact.Clear();

            foreach (OdeCharacter chr in _characters)
            {
                // Reset the collision values to false
                // since we don't know if we're colliding yet

                // For some reason this can happen. Don't ask...
                //
                if (chr == null)
                    continue;

                chr.IsColliding = false;
                chr.CollidingGround = false;
                chr.CollidingObj = false;

                // test the avatar's geometry for collision with the space
                // This will return near and the space that they are the closest to
                // And we'll run this again against the avatar and the space segment
                // This will return with a bunch of possible objects in the space segment
                // and we'll run it again on all of them.
                try
                {
                    d.SpaceCollide2(space, chr.Shell, IntPtr.Zero, nearCallback);
                }
                catch (AccessViolationException)
                {
                    m_log.Warn("[PHYSICS]: Unable to space collide");
                }
                //float terrainheight = GetTerrainHeightAtXY(chr.Position.X, chr.Position.Y);
                //if (chr.Position.Z + (chr.Velocity.Z * timeStep) < terrainheight + 10)
                //{
                    //chr.Position.Z = terrainheight + 10.0f;
                    //forcedZ = true;
                //}
            }

            lock (_activeprims)
            {
                List<OdePrim> removeprims = null;
                foreach (OdePrim chr in _activeprims)
                {
                    if (chr.Body != IntPtr.Zero && d.BodyIsEnabled(chr.Body) && (!chr.m_disabled))
                    {
                        try
                        {
                            lock (chr)
                            {
                                if (space != IntPtr.Zero && chr.prim_geom != IntPtr.Zero && chr.m_taintremove == false)
                                {
                                    d.SpaceCollide2(space, chr.prim_geom, IntPtr.Zero, nearCallback);
                                }
                                else
                                {
                                    if (removeprims == null)
                                    {
                                        removeprims = new List<OdePrim>();
                                    }
                                    removeprims.Add(chr);
                                    m_log.Debug("[PHYSICS]: unable to collide test active prim against space.  The space was zero, the geom was zero or it was in the process of being removed.  Removed it from the active prim list.  This needs to be fixed!");
                                }
                            }
                        }
                        catch (AccessViolationException)
                        {
                            m_log.Warn("[PHYSICS]: Unable to space collide");
                        }
                    }
                }
                if (removeprims != null)
                {
                    foreach (OdePrim chr in removeprims)
                    {
                        _activeprims.Remove(chr);
                    }
                }
            }

            _perloopContact.Clear();
        }

        #endregion

// TODO: unused
//         private float GetTerrainHeightAtXY(float x, float y)
//         {
//             return (float)_origheightmap[(int)y * Constants.RegionSize + (int)x];
//         }

        public void addCollisionEventReporting(PhysicsActor obj)
        {
            lock (_collisionEventPrim)
            {
                if (!_collisionEventPrim.Contains(obj))
                    _collisionEventPrim.Add(obj);
            }
        }

        public void remCollisionEventReporting(PhysicsActor obj)
        {
            lock (_collisionEventPrim)
            {
                if (!_collisionEventPrim.Contains(obj))
                    _collisionEventPrim.Remove(obj);
            }
        }

        #region Add/Remove Entities

        public override PhysicsActor AddAvatar(string avName, PhysicsVector position, PhysicsVector size)
        {
            PhysicsVector pos = new PhysicsVector();
            pos.X = position.X;
            pos.Y = position.Y;
            pos.Z = position.Z;
            OdeCharacter newAv = new OdeCharacter(avName, this, pos, ode, size, avPIDD, avPIDP, avCapRadius, avStandupTensor, avDensity, avHeightFudgeFactor, avMovementDivisorWalk, avMovementDivisorRun);
            _characters.Add(newAv);
            return newAv;
        }

        public override void RemoveAvatar(PhysicsActor actor)
        {
            lock (OdeLock)
            {
                //m_log.Debug("[PHYSICS]:ODELOCK");
                ((OdeCharacter) actor).Destroy();
                _characters.Remove((OdeCharacter) actor);
            }
        }

        private PhysicsActor AddPrim(String name, PhysicsVector position, PhysicsVector size, Quaternion rotation,
                                     IMesh mesh, PrimitiveBaseShape pbs, bool isphysical)
        {
            PhysicsVector pos = new PhysicsVector(position.X, position.Y, position.Z);
            //pos.X = position.X;
            //pos.Y = position.Y;
            //pos.Z = position.Z;
            PhysicsVector siz = new PhysicsVector();
            siz.X = size.X;
            siz.Y = size.Y;
            siz.Z = size.Z;
            Quaternion rot = rotation;

            OdePrim newPrim;
            lock (OdeLock)
            {
                newPrim = new OdePrim(name, this, pos, siz, rot, mesh, pbs, isphysical, ode);

                lock (_prims)
                    _prims.Add(newPrim);
            }

            return newPrim;
        }

        public void addActivePrim(OdePrim activatePrim)
        {
            // adds active prim..   (ones that should be iterated over in collisions_optimized
            lock (_activeprims)
            {
                if (!_activeprims.Contains(activatePrim))
                    _activeprims.Add(activatePrim);
                //else
                  //  m_log.Warn("[PHYSICS]: Double Entry in _activeprims detected, potential crash immenent");
            }
        }

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, PhysicsVector position,
                                                  PhysicsVector size, Quaternion rotation) //To be removed
        {
            return AddPrimShape(primName, pbs, position, size, rotation, false);
        }

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, PhysicsVector position,
                                                  PhysicsVector size, Quaternion rotation, bool isPhysical)
        {
            PhysicsActor result;
            IMesh mesh = null;

            //switch (pbs.ProfileShape)
            //{
            //    case ProfileShape.Square:
            //         //support simple box & hollow box now; later, more shapes
            //        if (needsMeshing(pbs))
            //        {
            //            mesh = mesher.CreateMesh(primName, pbs, size, 32f, isPhysical);
            //        }

            //        break;
            //}

            if (needsMeshing(pbs))
                mesh = mesher.CreateMesh(primName, pbs, size, 32f, isPhysical);

            result = AddPrim(primName, position, size, rotation, mesh, pbs, isPhysical);

            return result;
        }

        public void remActivePrim(OdePrim deactivatePrim)
        {
            lock (_activeprims)
            {
                _activeprims.Remove(deactivatePrim);
            }
        }

        public override void RemovePrim(PhysicsActor prim)
        {
            if (prim is OdePrim)
            {
                lock (OdeLock)
                {
                    OdePrim p = (OdePrim) prim;

                    p.setPrimForRemoval();
                    AddPhysicsActorTaint(prim);
                    //RemovePrimThreadLocked(p);
                }
            }
        }

        /// <summary>
        /// This is called from within simulate but outside the locked portion
        /// We need to do our own locking here
        /// Essentially, we need to remove the prim from our space segment, whatever segment it's in.
        ///
        /// If there are no more prim in the segment, we need to empty (spacedestroy)the segment and reclaim memory
        /// that the space was using.
        /// </summary>
        /// <param name="prim"></param>
        public void RemovePrimThreadLocked(OdePrim prim)
        {
            lock (prim)
            {
                remCollisionEventReporting(prim);
                lock (ode)
                {
                    if (prim.prim_geom != IntPtr.Zero)
                    {
                        prim.ResetTaints();

                        if (prim.IsPhysical)
                        {
                            prim.disableBody();
                            if (prim.childPrim)
                            {
                                prim.childPrim = false;
                                prim.Body = IntPtr.Zero;
                                prim.m_disabled = true;
                                prim.IsPhysical = false;
                            }


                        }
                        // we don't want to remove the main space

                        // If the geometry is in the targetspace, remove it from the target space
                        //m_log.Warn(prim.m_targetSpace);

                        //if (prim.m_targetSpace != IntPtr.Zero)
                        //{
                        //if (d.SpaceQuery(prim.m_targetSpace, prim.prim_geom))
                        //{

                        //if (d.GeomIsSpace(prim.m_targetSpace))
                        //{
                        //waitForSpaceUnlock(prim.m_targetSpace);
                        //d.SpaceRemove(prim.m_targetSpace, prim.prim_geom);
                        prim.m_targetSpace = IntPtr.Zero;
                        //}
                        //else
                        //{
                        // m_log.Info("[Physics]: Invalid Scene passed to 'removeprim from scene':" +
                        //((OdePrim)prim).m_targetSpace.ToString());
                        //}

                        //}
                        //}
                        //m_log.Warn(prim.prim_geom);
                        try
                        {
                            if (prim.prim_geom != IntPtr.Zero)
                            {
                                d.GeomDestroy(prim.prim_geom);
                                prim.prim_geom = IntPtr.Zero;
                            }
                            else
                            {
                                m_log.Warn("[PHYSICS]: Unable to remove prim from physics scene");
                            }
                        }
                        catch (AccessViolationException)
                        {
                            m_log.Info("[PHYSICS]: Couldn't remove prim from physics scene, it was already be removed.");
                        }
                        lock (_prims)
                        _prims.Remove(prim);

                        //If there are no more geometries in the sub-space, we don't need it in the main space anymore
                        //if (d.SpaceGetNumGeoms(prim.m_targetSpace) == 0)
                        //{
                        //if (prim.m_targetSpace != null)
                        //{
                        //if (d.GeomIsSpace(prim.m_targetSpace))
                        //{
                        //waitForSpaceUnlock(prim.m_targetSpace);
                        //d.SpaceRemove(space, prim.m_targetSpace);
                        // free up memory used by the space.
                        //d.SpaceDestroy(prim.m_targetSpace);
                        //int[] xyspace = calculateSpaceArrayItemFromPos(prim.Position);
                        //resetSpaceArrayItemToZero(xyspace[0], xyspace[1]);
                        //}
                        //else
                        //{
                        //m_log.Info("[Physics]: Invalid Scene passed to 'removeprim from scene':" +
                        //((OdePrim) prim).m_targetSpace.ToString());
                        //}
                        //}
                        //}
                    }
                }
            }
        }

        #endregion

        #region Space Separation Calculation

        /// <summary>
        /// Takes a space pointer and zeros out the array we're using to hold the spaces
        /// </summary>
        /// <param name="space"></param>
        public void resetSpaceArrayItemToZero(IntPtr space)
        {
            for (int x = 0; x < staticPrimspace.GetLength(0); x++)
            {
                for (int y = 0; y < staticPrimspace.GetLength(1); y++)
                {
                    if (staticPrimspace[x, y] == space)
                        staticPrimspace[x, y] = IntPtr.Zero;
                }
            }
        }

        public void resetSpaceArrayItemToZero(int arrayitemX, int arrayitemY)
        {
            staticPrimspace[arrayitemX, arrayitemY] = IntPtr.Zero;
        }

        /// <summary>
        /// Called when a static prim moves.  Allocates a space for the prim based on it's position
        /// </summary>
        /// <param name="geom">the pointer to the geom that moved</param>
        /// <param name="pos">the position that the geom moved to</param>
        /// <param name="currentspace">a pointer to the space it was in before it was moved.</param>
        /// <returns>a pointer to the new space it's in</returns>
        public IntPtr recalculateSpaceForGeom(IntPtr geom, PhysicsVector pos, IntPtr currentspace)
        {
            // Called from setting the Position and Size of an ODEPrim so
            // it's already in locked space.

            // we don't want to remove the main space
            // we don't need to test physical here because this function should
            // never be called if the prim is physical(active)

            // All physical prim end up in the root space
            //Thread.Sleep(20);
            if (currentspace != space)
            {
                //m_log.Info("[SPACE]: C:" + currentspace.ToString() + " g:" + geom.ToString());
                //if (currentspace == IntPtr.Zero)
               //{
                    //int adfadf = 0;
                //}
                if (d.SpaceQuery(currentspace, geom) && currentspace != IntPtr.Zero)
                {
                    if (d.GeomIsSpace(currentspace))
                    {
                        waitForSpaceUnlock(currentspace);
                        d.SpaceRemove(currentspace, geom);
                    }
                    else
                    {
                        m_log.Info("[Physics]: Invalid Scene passed to 'recalculatespace':" + currentspace.ToString() +
                                   " Geom:" + geom.ToString());
                    }
                }
                else
                {
                    IntPtr sGeomIsIn = d.GeomGetSpace(geom);
                    if (sGeomIsIn != IntPtr.Zero)
                    {
                        if (d.GeomIsSpace(currentspace))
                        {
                            waitForSpaceUnlock(sGeomIsIn);
                            d.SpaceRemove(sGeomIsIn, geom);
                        }
                        else
                        {
                            m_log.Info("[Physics]: Invalid Scene passed to 'recalculatespace':" +
                                       sGeomIsIn.ToString() + " Geom:" + geom.ToString());
                        }
                    }
                }

                //If there are no more geometries in the sub-space, we don't need it in the main space anymore
                if (d.SpaceGetNumGeoms(currentspace) == 0)
                {
                    if (currentspace != IntPtr.Zero)
                    {
                        if (d.GeomIsSpace(currentspace))
                        {
                            waitForSpaceUnlock(currentspace);
                            waitForSpaceUnlock(space);
                            d.SpaceRemove(space, currentspace);
                            // free up memory used by the space.

                            //d.SpaceDestroy(currentspace);
                            resetSpaceArrayItemToZero(currentspace);
                        }
                        else
                        {
                            m_log.Info("[Physics]: Invalid Scene passed to 'recalculatespace':" +
                                       currentspace.ToString() + " Geom:" + geom.ToString());
                        }
                    }
                }
            }
            else
            {
                // this is a physical object that got disabled. ;.;
                if (currentspace != IntPtr.Zero && geom != IntPtr.Zero)
                {
                    if (d.SpaceQuery(currentspace, geom))
                    {
                        if (d.GeomIsSpace(currentspace))
                        {
                            waitForSpaceUnlock(currentspace);
                            d.SpaceRemove(currentspace, geom);
                        }
                        else
                        {
                            m_log.Info("[Physics]: Invalid Scene passed to 'recalculatespace':" +
                                       currentspace.ToString() + " Geom:" + geom.ToString());
                        }
                    }
                    else
                    {
                        IntPtr sGeomIsIn = d.GeomGetSpace(geom);
                        if (sGeomIsIn != IntPtr.Zero)
                        {
                            if (d.GeomIsSpace(sGeomIsIn))
                            {
                                waitForSpaceUnlock(sGeomIsIn);
                                d.SpaceRemove(sGeomIsIn, geom);
                            }
                            else
                            {
                                m_log.Info("[Physics]: Invalid Scene passed to 'recalculatespace':" +
                                           sGeomIsIn.ToString() + " Geom:" + geom.ToString());
                            }
                        }
                    }
                }
            }

            // The routines in the Position and Size sections do the 'inserting' into the space,
            // so all we have to do is make sure that the space that we're putting the prim into
            // is in the 'main' space.
            int[] iprimspaceArrItem = calculateSpaceArrayItemFromPos(pos);
            IntPtr newspace = calculateSpaceForGeom(pos);

            if (newspace == IntPtr.Zero)
            {
                newspace = createprimspace(iprimspaceArrItem[0], iprimspaceArrItem[1]);
                d.HashSpaceSetLevels(newspace, smallHashspaceLow, smallHashspaceHigh);
            }

            return newspace;
        }

        /// <summary>
        /// Creates a new space at X Y
        /// </summary>
        /// <param name="iprimspaceArrItemX"></param>
        /// <param name="iprimspaceArrItemY"></param>
        /// <returns>A pointer to the created space</returns>
        public IntPtr createprimspace(int iprimspaceArrItemX, int iprimspaceArrItemY)
        {
            // creating a new space for prim and inserting it into main space.
            staticPrimspace[iprimspaceArrItemX, iprimspaceArrItemY] = d.HashSpaceCreate(IntPtr.Zero);
            d.GeomSetCategoryBits(staticPrimspace[iprimspaceArrItemX, iprimspaceArrItemY], (int)CollisionCategories.Space);
            waitForSpaceUnlock(space);
            d.SpaceSetSublevel(space, 1);
            d.SpaceAdd(space, staticPrimspace[iprimspaceArrItemX, iprimspaceArrItemY]);
            return staticPrimspace[iprimspaceArrItemX, iprimspaceArrItemY];
        }

        /// <summary>
        /// Calculates the space the prim should be in by its position
        /// </summary>
        /// <param name="pos"></param>
        /// <returns>a pointer to the space. This could be a new space or reused space.</returns>
        public IntPtr calculateSpaceForGeom(PhysicsVector pos)
        {
            int[] xyspace = calculateSpaceArrayItemFromPos(pos);
            //m_log.Info("[Physics]: Attempting to use arrayItem: " + xyspace[0].ToString() + "," + xyspace[1].ToString());
            return staticPrimspace[xyspace[0], xyspace[1]];
        }

        /// <summary>
        /// Holds the space allocation logic
        /// </summary>
        /// <param name="pos"></param>
        /// <returns>an array item based on the position</returns>
        public int[] calculateSpaceArrayItemFromPos(PhysicsVector pos)
        {
            int[] returnint = new int[2];

            returnint[0] = (int) (pos.X/metersInSpace);

            if (returnint[0] > ((int) (259f/metersInSpace)))
                returnint[0] = ((int) (259f/metersInSpace));
            if (returnint[0] < 0)
                returnint[0] = 0;

            returnint[1] = (int) (pos.Y/metersInSpace);
            if (returnint[1] > ((int) (259f/metersInSpace)))
                returnint[1] = ((int) (259f/metersInSpace));
            if (returnint[1] < 0)
                returnint[1] = 0;

            return returnint;
        }

        #endregion

        /// <summary>
        /// Routine to figure out if we need to mesh this prim with our mesher
        /// </summary>
        /// <param name="pbs"></param>
        /// <returns></returns>
        public bool needsMeshing(PrimitiveBaseShape pbs)
        {
            // most of this is redundant now as the mesher will return null if it cant mesh a prim
            // but we still need to check for sculptie meshing being enabled so this is the most
            // convenient place to do it for now...

        //    //if (pbs.PathCurve == (byte)Primitive.PathCurve.Circle && pbs.ProfileCurve == (byte)Primitive.ProfileCurve.Circle && pbs.PathScaleY <= 0.75f)
        //    //Console.WriteLine("needsMeshing: " + " pathCurve: " + pbs.PathCurve.ToString() + " profileCurve: " + pbs.ProfileCurve.ToString() + " pathScaleY: " + Primitive.UnpackPathScale(pbs.PathScaleY).ToString());
            int iPropertiesNotSupportedDefault = 0;

            if (pbs.SculptEntry && !meshSculptedPrim)
            {
#if SPAM
                m_log.Warn("NonMesh");
#endif
                return false;
            }

            // if it's a standard box or sphere with no cuts or hollows or twist, return false since ODE can use an internal representation for the prim
            if ((pbs.ProfileShape == ProfileShape.Square && pbs.PathCurve == (byte)Extrusion.Straight)
                || (pbs.ProfileShape == ProfileShape.HalfCircle && pbs.PathCurve == (byte)Extrusion.Curve1
                && pbs.Scale.X == pbs.Scale.Y && pbs.Scale.Y == pbs.Scale.Z))
            {

                if (pbs.ProfileBegin == 0 && pbs.ProfileEnd == 0
                    && pbs.ProfileHollow == 0
                    && pbs.PathTwist == 0 && pbs.PathTwistBegin == 0
                    && pbs.PathBegin == 0 && pbs.PathEnd == 0
                    && pbs.PathTaperX == 0 && pbs.PathTaperY == 0
                    && pbs.PathScaleX == 100 && pbs.PathScaleY == 100)
                {
#if SPAM
                    m_log.Warn("NonMesh");
#endif
                    return false;
                }
            }

            if (pbs.ProfileHollow != 0)
                iPropertiesNotSupportedDefault++;

            if (((Int16)pbs.PathTwistBegin != 0) || ((Int16)pbs.PathTwist != 0))
                iPropertiesNotSupportedDefault++; 

            if ((pbs.ProfileBegin != 0) || pbs.ProfileEnd != 0)
                iPropertiesNotSupportedDefault++;

            if ((pbs.PathScaleX != 100) || (pbs.PathScaleY != 100))
                iPropertiesNotSupportedDefault++;

            if ((pbs.PathShearX != 0) || (pbs.PathShearY != 0))
                iPropertiesNotSupportedDefault++;

            if (pbs.ProfileShape == ProfileShape.Circle && pbs.PathCurve == (byte)Extrusion.Straight)
                iPropertiesNotSupportedDefault++;

            if (pbs.ProfileShape == ProfileShape.HalfCircle && pbs.PathCurve == (byte)Extrusion.Curve1 && (pbs.Scale.X != pbs.Scale.Y || pbs.Scale.Y != pbs.Scale.Z || pbs.Scale.Z != pbs.Scale.X))
                iPropertiesNotSupportedDefault++;

            if (pbs.ProfileShape == ProfileShape.HalfCircle && pbs.PathCurve == (byte) Extrusion.Curve1)
                iPropertiesNotSupportedDefault++;

            // test for torus
            if ((pbs.ProfileCurve & 0x07) == (byte)ProfileShape.Square)
            {
                if (pbs.PathCurve == (byte)Extrusion.Curve1)
                {
                    iPropertiesNotSupportedDefault++;
                }
            }
            else if ((pbs.ProfileCurve & 0x07) == (byte)ProfileShape.Circle)
            {
                if (pbs.PathCurve == (byte)Extrusion.Straight)
                {
                    iPropertiesNotSupportedDefault++;
                }

                // ProfileCurve seems to combine hole shape and profile curve so we need to only compare against the lower 3 bits
                else if (pbs.PathCurve == (byte)Extrusion.Curve1)
                {
                    iPropertiesNotSupportedDefault++;
                }
            }
            else if ((pbs.ProfileCurve & 0x07) == (byte)ProfileShape.HalfCircle)
            {
                if (pbs.PathCurve == (byte)Extrusion.Curve1 || pbs.PathCurve == (byte)Extrusion.Curve2)
                {
                    iPropertiesNotSupportedDefault++;
                }
            }
            else if ((pbs.ProfileCurve & 0x07) == (byte)ProfileShape.EquilateralTriangle)
            {
                if (pbs.PathCurve == (byte)Extrusion.Straight)
                {
                    iPropertiesNotSupportedDefault++;
                }
                else if (pbs.PathCurve == (byte)Extrusion.Curve1)
                {
                    iPropertiesNotSupportedDefault++;
                }
            }


            if (iPropertiesNotSupportedDefault == 0)
            {
#if SPAM              
                m_log.Warn("NonMesh");
#endif
                return false;
            }
#if SPAM
            m_log.Debug("Mesh");
#endif
            return true; 
        }

        /// <summary>
        /// Called after our prim properties are set Scale, position etc.
        /// We use this event queue like method to keep changes to the physical scene occuring in the threadlocked mutex
        /// This assures us that we have no race conditions
        /// </summary>
        /// <param name="prim"></param>
        public override void AddPhysicsActorTaint(PhysicsActor prim)
        {
            
            if (prim is OdePrim)
            {
                OdePrim taintedprim = ((OdePrim) prim);
                lock (_taintedPrim)
                {
                    if (!(_taintedPrim.Contains(taintedprim)))
                        _taintedPrim.Add(taintedprim);
                }
                return;
            }
            else if (prim is OdeCharacter)
            {
                OdeCharacter taintedchar = ((OdeCharacter)prim);
                lock (_taintedActors)
                {
                    if (!(_taintedActors.Contains(taintedchar)))
                        _taintedActors.Add(taintedchar);
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
            if (framecount >= int.MaxValue)
                framecount = 0;

            framecount++;

            float fps = 0;
            //m_log.Info(timeStep.ToString());
            step_time += timeStep;

            // If We're loaded down by something else,
            // or debugging with the Visual Studio project on pause
            // skip a few frames to catch up gracefully.
            // without shooting the physicsactors all over the place

            if (step_time >= m_SkipFramesAtms)
            {
                // Instead of trying to catch up, it'll do 5 physics frames only
                step_time = ODE_STEPSIZE;
                m_physicsiterations = 5;
            }
            else
            {
                m_physicsiterations = 10;
            }
            lock (OdeLock)
            {
                // Process 10 frames if the sim is running normal..
                // process 5 frames if the sim is running slow
                //try
                //{
                    //d.WorldSetQuickStepNumIterations(world, m_physicsiterations);
                //}
                //catch (StackOverflowException)
                //{
                   // m_log.Error("[PHYSICS]: The operating system wasn't able to allocate enough memory for the simulation.  Restarting the sim.");
                   // ode.drelease(world);
                    //base.TriggerPhysicsBasedRestart();
                //}

                int i = 0;

                // Figure out the Frames Per Second we're going at.
                //(step_time == 0.004f, there's 250 of those per second.   Times the step time/step size
                step_time = 0.09375f;
                fps = (step_time/ODE_STEPSIZE) * 1000;

                while (step_time > 0.0f)
                {
                    //lock (ode)
                    //{
                        //if (!ode.lockquery())
                        //{
                           // ode.dlock(world);
                            try
                            { 
                                // Insert, remove Characters
                                bool processedtaints = false;

                                lock (_taintedActors)
                                {
                                    if (_taintedActors.Count > 0)
                                    {
                                        foreach (OdeCharacter character in _taintedActors)
                                        {

                                            character.ProcessTaints(timeStep);

                                            processedtaints = true;
                                            //character.m_collisionscore = 0;
                                        }

                                        if (processedtaints)
                                            _taintedActors.Clear();
                                    }
                                }

                                // Modify other objects in the scene.
                                processedtaints = false;

                                lock (_taintedPrim)
                                {
                                    foreach (OdePrim prim in _taintedPrim)
                                    {
                                        if (prim.m_taintremove)
                                        {
                                            RemovePrimThreadLocked(prim);
                                        }
                                        else
                                        {
                                            prim.ProcessTaints(timeStep);
                                        }
                                        processedtaints = true;
                                        prim.m_collisionscore = 0;
                                    }

                                    if (processedtaints)
                                        _taintedPrim.Clear();
                                }

                                // Move characters
                                lock (_characters)
                                {
                                    foreach (OdeCharacter actor in _characters)
                                    {
                                        if (actor != null)
                                            actor.Move(timeStep);
                                    }
                                }

                                // Move other active objects
                                lock (_activeprims)
                                {
                                    foreach (OdePrim prim in _activeprims)
                                    {
                                        prim.m_collisionscore = 0;
                                        prim.Move(timeStep);
                                    }
                                }

                                //if ((framecount % m_randomizeWater) == 0)
                                   // randomizeWater(waterlevel);

                                collision_optimized(timeStep);

                                lock (_collisionEventPrim)
                                {
                                    foreach (PhysicsActor obj in _collisionEventPrim)
                                    {
                                        if (obj == null)
                                            continue;

                                        switch ((ActorTypes)obj.PhysicsActorType)
                                        {
                                            case ActorTypes.Agent:
                                                OdeCharacter cobj = (OdeCharacter)obj;
                                                cobj.SendCollisions();
                                                break;
                                            case ActorTypes.Prim:
                                                OdePrim pobj = (OdePrim)obj;
                                                pobj.SendCollisions();
                                                break;
                                        }
                                    }
                                }

                                d.WorldQuickStep(world, ODE_STEPSIZE);
                                d.JointGroupEmpty(contactgroup);
                                //ode.dunlock(world);
                            }
                            catch (Exception e)
                            {
                                m_log.ErrorFormat("[PHYSICS]: {0}, {1}, {2}", e.Message, e.TargetSite, e);
                                ode.dunlock(world);
                            }

                            step_time -= ODE_STEPSIZE;
                            i++;
                        //}
                        //else
                        //{
                            //fps = 0;
                        //}
                    //}
                }

                lock (_characters)
                {
                    foreach (OdeCharacter actor in _characters)
                    {
                        if (actor != null)
                            actor.UpdatePositionAndVelocity();
                    }
                }

                lock (_activeprims)
                {
                    //if (timeStep < 0.2f)
                    {
                        foreach (OdePrim actor in _activeprims)
                        {
                            if (actor.IsPhysical && (d.BodyIsEnabled(actor.Body) || !actor._zeroFlag))
                            {
                                actor.UpdatePositionAndVelocity();
                            }
                        }
                    }
                }

                // Finished with all sim stepping. If requested, dump world state to file for debugging.
                // TODO: This call to the export function is already inside lock (OdeLock) - but is an extra lock needed?
                // TODO: This overwrites all dump files in-place. Should this be a growing logfile, or separate snapshots?
                if (physics_logging && (physics_logging_interval>0) && (framecount % physics_logging_interval == 0))
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
            }

            return fps;
        }

        public override void GetResults()
        {
        }

        public override bool IsThreaded
        {
            // for now we won't be multithreaded
            get { return (false); }
        }

        #region ODE Specific Terrain Fixes
        public float[] ResizeTerrain512NearestNeighbour(float[] heightMap)
        {
            float[] returnarr = new float[262144];
            float[,] resultarr = new float[m_regionWidth, m_regionHeight];

            // Filling out the array into it's multi-dimentional components
            for (int y = 0; y < m_regionHeight; y++)
            {
                for (int x = 0; x < m_regionWidth; x++)
                {
                    resultarr[y, x] = heightMap[y * m_regionWidth + x];
                }
            }

            // Resize using Nearest Neighbour

            // This particular way is quick but it only works on a multiple of the original

            // The idea behind this method can be described with the following diagrams
            // second pass and third pass happen in the same loop really..  just separated
            // them to show what this does.

            // First Pass
            // ResultArr:
            // 1,1,1,1,1,1
            // 1,1,1,1,1,1
            // 1,1,1,1,1,1
            // 1,1,1,1,1,1
            // 1,1,1,1,1,1
            // 1,1,1,1,1,1

            // Second Pass
            // ResultArr2:
            // 1,,1,,1,,1,,1,,1,
            // ,,,,,,,,,,
            // 1,,1,,1,,1,,1,,1,
            // ,,,,,,,,,,
            // 1,,1,,1,,1,,1,,1,
            // ,,,,,,,,,,
            // 1,,1,,1,,1,,1,,1,
            // ,,,,,,,,,,
            // 1,,1,,1,,1,,1,,1,
            // ,,,,,,,,,,
            // 1,,1,,1,,1,,1,,1,

            // Third pass fills in the blanks
            // ResultArr2:
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1

            // X,Y = .
            // X+1,y = ^
            // X,Y+1 = *
            // X+1,Y+1 = #

            // Filling in like this;
            // .*
            // ^#
            // 1st .
            // 2nd *
            // 3rd ^
            // 4th #
            // on single loop.

            float[,] resultarr2 = new float[512, 512];
            for (int y = 0; y < m_regionHeight; y++)
            {
                for (int x = 0; x < m_regionWidth; x++)
                {
                    resultarr2[y * 2, x * 2] = resultarr[y, x];

                    if (y < m_regionHeight)
                    {
                        resultarr2[(y * 2) + 1, x * 2] = resultarr[y, x];
                    }
                    if (x < m_regionWidth)
                    {
                        resultarr2[y * 2, (x * 2) + 1] = resultarr[y, x];
                    }
                    if (x < m_regionWidth && y < m_regionHeight)
                    {
                        resultarr2[(y * 2) + 1, (x * 2) + 1] = resultarr[y, x];
                    }
                }
            }

            //Flatten out the array
            int i = 0;
            for (int y = 0; y < 512; y++)
            {
                for (int x = 0; x < 512; x++)
                {
                    if (resultarr2[y, x] <= 0)
                        returnarr[i] = 0.0000001f;
                    else
                        returnarr[i] = resultarr2[y, x];

                    i++;
                }
            }

            return returnarr;
        }

        public float[] ResizeTerrain512Interpolation(float[] heightMap)
        {
            float[] returnarr = new float[262144];
            float[,] resultarr = new float[m_regionWidth,m_regionHeight];

            // Filling out the array into it's multi-dimentional components
            for (int y = 0; y < m_regionHeight; y++)
            {
                for (int x = 0; x < m_regionWidth; x++)
                {
                    resultarr[y, x] = heightMap[y*m_regionWidth + x];
                }
            }

            // Resize using interpolation

            // This particular way is quick but it only works on a multiple of the original

            // The idea behind this method can be described with the following diagrams
            // second pass and third pass happen in the same loop really..  just separated
            // them to show what this does.

            // First Pass
            // ResultArr:
            // 1,1,1,1,1,1
            // 1,1,1,1,1,1
            // 1,1,1,1,1,1
            // 1,1,1,1,1,1
            // 1,1,1,1,1,1
            // 1,1,1,1,1,1

            // Second Pass
            // ResultArr2:
            // 1,,1,,1,,1,,1,,1,
            // ,,,,,,,,,,
            // 1,,1,,1,,1,,1,,1,
            // ,,,,,,,,,,
            // 1,,1,,1,,1,,1,,1,
            // ,,,,,,,,,,
            // 1,,1,,1,,1,,1,,1,
            // ,,,,,,,,,,
            // 1,,1,,1,,1,,1,,1,
            // ,,,,,,,,,,
            // 1,,1,,1,,1,,1,,1,

            // Third pass fills in the blanks
            // ResultArr2:
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1

            // X,Y = .
            // X+1,y = ^
            // X,Y+1 = *
            // X+1,Y+1 = #

            // Filling in like this;
            // .*
            // ^#
            // 1st .
            // 2nd *
            // 3rd ^
            // 4th #
            // on single loop.

            float[,] resultarr2 = new float[512,512];
            for (int y = 0; y < m_regionHeight; y++)
            {
                for (int x = 0; x < m_regionWidth; x++)
                {
                    resultarr2[y*2, x*2] = resultarr[y, x];

                    if (y < m_regionHeight)
                    {
                        if (y + 1 < m_regionHeight)
                        {
                            if (x + 1 < m_regionWidth)
                            {
                                resultarr2[(y*2) + 1, x*2] = ((resultarr[y, x] + resultarr[y + 1, x] +
                                                               resultarr[y, x + 1] + resultarr[y + 1, x + 1])/4);
                            }
                            else
                            {
                                resultarr2[(y*2) + 1, x*2] = ((resultarr[y, x] + resultarr[y + 1, x])/2);
                            }
                        }
                        else
                        {
                            resultarr2[(y*2) + 1, x*2] = resultarr[y, x];
                        }
                    }
                    if (x < m_regionWidth)
                    {
                        if (x + 1 < m_regionWidth)
                        {
                            if (y + 1 < m_regionHeight)
                            {
                                resultarr2[y*2, (x*2) + 1] = ((resultarr[y, x] + resultarr[y + 1, x] +
                                                               resultarr[y, x + 1] + resultarr[y + 1, x + 1])/4);
                            }
                            else
                            {
                                resultarr2[y*2, (x*2) + 1] = ((resultarr[y, x] + resultarr[y, x + 1])/2);
                            }
                        }
                        else
                        {
                            resultarr2[y*2, (x*2) + 1] = resultarr[y, x];
                        }
                    }
                    if (x < m_regionWidth && y < m_regionHeight)
                    {
                        if ((x + 1 < m_regionWidth) && (y + 1 < m_regionHeight))
                        {
                            resultarr2[(y*2) + 1, (x*2) + 1] = ((resultarr[y, x] + resultarr[y + 1, x] +
                                                                 resultarr[y, x + 1] + resultarr[y + 1, x + 1])/4);
                        }
                        else
                        {
                            resultarr2[(y*2) + 1, (x*2) + 1] = resultarr[y, x];
                        }
                    }
                }
            }
            //Flatten out the array
            int i = 0;
            for (int y = 0; y < 512; y++)
            {
                for (int x = 0; x < 512; x++)
                {
                    if (Single.IsNaN(resultarr2[y, x]) || Single.IsInfinity(resultarr2[y, x]))
                    {
                        m_log.Warn("[PHYSICS]: Non finite heightfield element detected.  Setting it to 0");
                        resultarr2[y, x] = 0;
                    }

                    if (resultarr2[y, x] <= 0)
                    {
                        returnarr[i] = 0.0000001f;

                    }
                    else
                        returnarr[i] = resultarr2[y, x];

                    i++;
                }
            }

            return returnarr;
        }

        #endregion

        public override void SetTerrain(float[] heightMap)
        {
            // this._heightmap[i] = (double)heightMap[i];
            // dbm (danx0r) -- creating a buffer zone of one extra sample all around
            // _origheightmap = heightMap;
            const uint heightmapWidth = m_regionWidth + 2;
            const uint heightmapHeight = m_regionHeight + 2;
            const uint heightmapWidthSamples = 2*m_regionWidth + 2;
            const uint heightmapHeightSamples = 2*m_regionHeight + 2;
            const float scale = 1.0f;
            const float offset = 0.0f;
            const float thickness = 0.2f;
            const int wrap = 0;

            //Double resolution
            heightMap = ResizeTerrain512Interpolation(heightMap);

            for (int x = 0; x < heightmapWidthSamples; x++)
            {
                for (int y = 0; y < heightmapHeightSamples; y++)
                {
                    int xx = Util.Clip(x - 1, 0, 511);
                    int yy = Util.Clip(y - 1, 0, 511);

                    float val = heightMap[yy*512 + xx];
                    _heightmap[x*heightmapHeightSamples + y] = val;
                }
            }

            lock (OdeLock)
            {
                if (LandGeom != IntPtr.Zero)
                {
                    d.SpaceRemove(space, LandGeom);
                }
                IntPtr HeightmapData = d.GeomHeightfieldDataCreate();
                d.GeomHeightfieldDataBuildSingle(HeightmapData, _heightmap, 0, heightmapWidth, heightmapHeight,
                                                 (int) heightmapWidthSamples, (int) heightmapHeightSamples, scale,
                                                 offset, thickness, wrap);
                d.GeomHeightfieldDataSetBounds(HeightmapData, m_regionWidth, m_regionHeight);
                LandGeom = d.CreateHeightfield(space, HeightmapData, 1);
                if (LandGeom != IntPtr.Zero)
                {
                    d.GeomSetCategoryBits(LandGeom, (int)(CollisionCategories.Land));
                    d.GeomSetCollideBits(LandGeom, (int)(CollisionCategories.Space));

                }
                geom_name_map[LandGeom] = "Terrain";

                d.Matrix3 R = new d.Matrix3();

                Quaternion q1 = Quaternion.CreateFromAxisAngle(new Vector3(1, 0, 0), 1.5707f);
                Quaternion q2 = Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), 1.5707f);
                //Axiom.Math.Quaternion q3 = Axiom.Math.Quaternion.FromAngleAxis(3.14f, new Axiom.Math.Vector3(0, 0, 1));

                q1 = q1*q2;
                //q1 = q1 * q3;
                Vector3 v3;
                float angle;
                q1.GetAxisAngle(out v3, out angle);

                d.RFromAxisAndAngle(out R, v3.X, v3.Y, v3.Z, angle);
                d.GeomSetRotation(LandGeom, ref R);
                d.GeomSetPosition(LandGeom, 128, 128, 0);
            }
        }

        public override void DeleteTerrain()
        {
        }

        public override void SetWaterLevel(float baseheight)
        {
            waterlevel = baseheight;
            randomizeWater(waterlevel);
        }

        public void randomizeWater(float baseheight)
        {
            const uint heightmapWidth = m_regionWidth + 2;
            const uint heightmapHeight = m_regionHeight + 2;
            const uint heightmapWidthSamples = m_regionWidth + 2;
            const uint heightmapHeightSamples = m_regionHeight + 2;
            const float scale = 1.0f;
            const float offset = 0.0f;
            const float thickness = 2.9f;
            const int wrap = 0;

            for (int i = 0; i < (258 * 258); i++)
            {
                _watermap[i] = (baseheight-0.1f) + ((float)fluidRandomizer.Next(1,9) / 10f);
               // m_log.Info((baseheight - 0.1f) + ((float)fluidRandomizer.Next(1, 9) / 10f));
            }

            lock (OdeLock)
            {
                if (WaterGeom != IntPtr.Zero)
                {
                    d.SpaceRemove(space, WaterGeom);
                }
                IntPtr HeightmapData = d.GeomHeightfieldDataCreate();
                d.GeomHeightfieldDataBuildSingle(HeightmapData, _watermap, 0, heightmapWidth, heightmapHeight,
                                                 (int)heightmapWidthSamples, (int)heightmapHeightSamples, scale,
                                                 offset, thickness, wrap);
                d.GeomHeightfieldDataSetBounds(HeightmapData, m_regionWidth, m_regionHeight);
                WaterGeom = d.CreateHeightfield(space, HeightmapData, 1);
                if (WaterGeom != IntPtr.Zero)
                {
                    d.GeomSetCategoryBits(WaterGeom, (int)(CollisionCategories.Water));
                    d.GeomSetCollideBits(WaterGeom, (int)(CollisionCategories.Space));

                }
                geom_name_map[WaterGeom] = "Water";

                d.Matrix3 R = new d.Matrix3();

                Quaternion q1 = Quaternion.CreateFromAxisAngle(new Vector3(1, 0, 0), 1.5707f);
                Quaternion q2 = Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), 1.5707f);
                //Axiom.Math.Quaternion q3 = Axiom.Math.Quaternion.FromAngleAxis(3.14f, new Axiom.Math.Vector3(0, 0, 1));

                q1 = q1 * q2;
                //q1 = q1 * q3;
                Vector3 v3;
                float angle;
                q1.GetAxisAngle(out v3, out angle);

                d.RFromAxisAndAngle(out R, v3.X, v3.Y, v3.Z, angle);
                d.GeomSetRotation(WaterGeom, ref R);
                d.GeomSetPosition(WaterGeom, 128, 128, 0);
            }

        }

        public override void Dispose()
        {
            lock (OdeLock)
            {
                lock (_prims)
                {
                    foreach (OdePrim prm in _prims)
                    {
                        RemovePrim(prm);
                    }
                }

                //foreach (OdeCharacter act in _characters)
                //{
                    //RemoveAvatar(act);
                //}
                d.WorldDestroy(world);
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
                        returncolliders.Add(prm.m_localID, prm.CollisionScore);
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
#if USE_DRAWSTUFF
        // Keyboard callback
        public void command(int cmd)
        {
            IntPtr geom;
            d.Mass mass;
            d.Vector3 sides = new d.Vector3(d.RandReal() * 0.5f + 0.1f, d.RandReal() * 0.5f + 0.1f, d.RandReal() * 0.5f + 0.1f);

            

            Char ch = Char.ToLower((Char)cmd);
            switch ((Char)ch)
            {
                case 'w':
                    try
                    {
                        Vector3 rotate = (new Vector3(1, 0, 0) * Quaternion.CreateFromEulers(hpr.Z * Utils.DEG_TO_RAD, hpr.Y * Utils.DEG_TO_RAD, hpr.X * Utils.DEG_TO_RAD));

                        xyz.X += rotate.X; xyz.Y += rotate.Y; xyz.Z += rotate.Z;
                        ds.SetViewpoint(ref xyz, ref hpr);
                    }
                    catch (ArgumentException)
                    { hpr.X = 0; }
                    break;

                case 'a':
                    hpr.X++;
                    ds.SetViewpoint(ref xyz, ref hpr);
                    break;

                case 's':
                    try
                    {
                        Vector3 rotate2 = (new Vector3(-1, 0, 0) * Quaternion.CreateFromEulers(hpr.Z * Utils.DEG_TO_RAD, hpr.Y * Utils.DEG_TO_RAD, hpr.X * Utils.DEG_TO_RAD));

                        xyz.X += rotate2.X; xyz.Y += rotate2.Y; xyz.Z += rotate2.Z;
                        ds.SetViewpoint(ref xyz, ref hpr);
                    }
                    catch (ArgumentException)
                    { hpr.X = 0; }
                    break;
                case 'd':
                    hpr.X--;
                    ds.SetViewpoint(ref xyz, ref hpr);
                    break;
                case 'r':
                    xyz.Z++;
                    ds.SetViewpoint(ref xyz, ref hpr);
                    break;
                case 'f':
                    xyz.Z--;
                    ds.SetViewpoint(ref xyz, ref hpr);
                    break;
                case 'e':
                    xyz.Y++;
                    ds.SetViewpoint(ref xyz, ref hpr);
                    break;
                case 'q':
                    xyz.Y--;
                    ds.SetViewpoint(ref xyz, ref hpr);
                    break;
            }
        }

        public void step(int pause)
        {
            
            ds.SetColor(1.0f, 1.0f, 0.0f);
            ds.SetTexture(ds.Texture.Wood);
            lock (_prims)
            {
                foreach (OdePrim prm in _prims)
                {
                    //IntPtr body = d.GeomGetBody(prm.prim_geom);
                    if (prm.prim_geom != IntPtr.Zero)
                    {
                        d.Vector3 pos;
                        d.GeomCopyPosition(prm.prim_geom, out pos);
                        //d.BodyCopyPosition(body, out pos);

                        d.Matrix3 R;
                        d.GeomCopyRotation(prm.prim_geom, out R);
                        //d.BodyCopyRotation(body, out R);


                        d.Vector3 sides = new d.Vector3();
                        sides.X = prm.Size.X;
                        sides.Y = prm.Size.Y;
                        sides.Z = prm.Size.Z;

                        ds.DrawBox(ref pos, ref R, ref sides);
                    }
                }
            }
            ds.SetColor(1.0f, 0.0f, 0.0f);
            lock (_characters)
            {
                foreach (OdeCharacter chr in _characters)
                {
                    if (chr.Shell != IntPtr.Zero)
                    {
                        IntPtr body = d.GeomGetBody(chr.Shell);

                        d.Vector3 pos;
                        d.GeomCopyPosition(chr.Shell, out pos);
                        //d.BodyCopyPosition(body, out pos);

                        d.Matrix3 R;
                        d.GeomCopyRotation(chr.Shell, out R);
                        //d.BodyCopyRotation(body, out R);

                        ds.DrawCapsule(ref pos, ref R, chr.Size.Z, 0.35f);
                        d.Vector3 sides = new d.Vector3();
                        sides.X = 0.5f;
                        sides.Y = 0.5f;
                        sides.Z = 0.5f;

                        ds.DrawBox(ref pos, ref R, ref sides);


                    }
                }
            }
        }

        public void start(int unused)
        {
            
            ds.SetViewpoint(ref xyz, ref hpr);
        }
#endif
    }
}
