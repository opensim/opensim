/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyrightD
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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.CoreModules;
using Logging = OpenSim.Region.CoreModules.Framework.Statistics.Logging;
using OpenSim.Region.Physics.Manager;
using Nini.Config;
using log4net;
using OpenMetaverse;

// TODOs for BulletSim (for BSScene, BSPrim, BSCharacter and BulletSim)
// Based on material, set density and friction
// More efficient memory usage when passing hull information from BSPrim to BulletSim
// Do attachments need to be handled separately? Need collision events. Do not collide with VolumeDetect
// Implement LockAngularMotion
// Add PID movement operations. What does ScenePresence.MoveToTarget do?
// Check terrain size. 128 or 127?
// Raycast
//
namespace OpenSim.Region.Physics.BulletSNPlugin
{
public sealed class BSScene : PhysicsScene, IPhysicsParameters
{
    private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly string LogHeader = "[BULLETS SCENE]";

    // The name of the region we're working for.
    public string RegionName { get; private set; }

    public string BulletSimVersion = "?";

    public Dictionary<uint, BSPhysObject> PhysObjects;
    public BSShapeCollection Shapes;

    // Keeping track of the objects with collisions so we can report begin and end of a collision
    public HashSet<BSPhysObject> ObjectsWithCollisions = new HashSet<BSPhysObject>();
    public HashSet<BSPhysObject> ObjectsWithNoMoreCollisions = new HashSet<BSPhysObject>();
    // Keep track of all the avatars so we can send them a collision event
    //    every tick so OpenSim will update its animation.
    private HashSet<BSPhysObject> m_avatars = new HashSet<BSPhysObject>();

    // let my minuions use my logger
    public ILog Logger { get { return m_log; } }

    public IMesher mesher;
    public uint WorldID { get; private set; }
    public BulletSim World { get; private set; }

    // All the constraints that have been allocated in this instance.
    public BSConstraintCollection Constraints { get; private set; }

    // Simulation parameters
    internal int m_maxSubSteps;
    internal float m_fixedTimeStep;
    internal long m_simulationStep = 0;
    public long SimulationStep { get { return m_simulationStep; } }
    internal int m_taintsToProcessPerStep;
    internal float LastTimeStep { get; private set; }

    // Physical objects can register for prestep or poststep events
    public delegate void PreStepAction(float timeStep);
    public delegate void PostStepAction(float timeStep);
    public event PreStepAction BeforeStep;
    public event PreStepAction AfterStep;

    // A value of the time now so all the collision and update routines do not have to get their own
    // Set to 'now' just before all the prims and actors are called for collisions and updates
    public int SimulationNowTime { get; private set; }

    // True if initialized and ready to do simulation steps
    private bool m_initialized = false;

    // Flag which is true when processing taints.
    // Not guaranteed to be correct all the time (don't depend on this) but good for debugging.
    public bool InTaintTime { get; private set; }

    // Pinned memory used to pass step information between managed and unmanaged
    internal int m_maxCollisionsPerFrame;
    private List<BulletXNA.CollisionDesc> m_collisionArray;
    //private GCHandle m_collisionArrayPinnedHandle;

    internal int m_maxUpdatesPerFrame;
    private List<BulletXNA.EntityProperties> m_updateArray;
    //private GCHandle m_updateArrayPinnedHandle;


    public const uint TERRAIN_ID = 0;       // OpenSim senses terrain with a localID of zero
    public const uint GROUNDPLANE_ID = 1;
    public const uint CHILDTERRAIN_ID = 2;  // Terrain allocated based on our mega-prim childre start here

    public float SimpleWaterLevel { get; set; }
    public BSTerrainManager TerrainManager { get; private set; }

    public ConfigurationParameters Params
    {
        get { return UnmanagedParams[0]; }
    }
    public Vector3 DefaultGravity
    {
        get { return new Vector3(0f, 0f, Params.gravity); }
    }
    // Just the Z value of the gravity
    public float DefaultGravityZ
    {
        get { return Params.gravity; }
    }

    // When functions in the unmanaged code must be called, it is only
    //   done at a known time just before the simulation step. The taint
    //   system saves all these function calls and executes them in
    //   order before the simulation.
    public delegate void TaintCallback();
    private struct TaintCallbackEntry
    {
        public String ident;
        public TaintCallback callback;
        public TaintCallbackEntry(string i, TaintCallback c)
        {
            ident = i;
            callback = c;
        }
    }
    private Object _taintLock = new Object();   // lock for using the next object
    private List<TaintCallbackEntry> _taintOperations;
    private Dictionary<string, TaintCallbackEntry> _postTaintOperations;
    private List<TaintCallbackEntry> _postStepOperations;

    // A pointer to an instance if this structure is passed to the C++ code
    // Used to pass basic configuration values to the unmanaged code.
    internal ConfigurationParameters[] UnmanagedParams;
    //GCHandle m_paramsHandle;

    // Handle to the callback used by the unmanaged code to call into the managed code.
    // Used for debug logging.
    // Need to store the handle in a persistant variable so it won't be freed.
    private BulletSimAPI.DebugLogCallback m_DebugLogCallbackHandle;

    // Sometimes you just have to log everything.
    public Logging.LogWriter PhysicsLogging;
    private bool m_physicsLoggingEnabled;
    private string m_physicsLoggingDir;
    private string m_physicsLoggingPrefix;
    private int m_physicsLoggingFileMinutes;
    private bool m_physicsLoggingDoFlush;
    // 'true' of the vehicle code is to log lots of details
    public bool VehicleLoggingEnabled { get; private set; }
    public bool VehiclePhysicalLoggingEnabled { get; private set; }

    #region Construction and Initialization
    public BSScene(string identifier)
    {
        m_initialized = false;
        // we are passed the name of the region we're working for.
        RegionName = identifier;
    }

    public override void Initialise(IMesher meshmerizer, IConfigSource config)
    {
        mesher = meshmerizer;
        _taintOperations = new List<TaintCallbackEntry>();
        _postTaintOperations = new Dictionary<string, TaintCallbackEntry>();
        _postStepOperations = new List<TaintCallbackEntry>();
        PhysObjects = new Dictionary<uint, BSPhysObject>();
        Shapes = new BSShapeCollection(this);

        // Allocate pinned memory to pass parameters.
        UnmanagedParams = new ConfigurationParameters[1];
        //m_paramsHandle = GCHandle.Alloc(UnmanagedParams, GCHandleType.Pinned);

        // Set default values for physics parameters plus any overrides from the ini file
        GetInitialParameterValues(config);

        // allocate more pinned memory close to the above in an attempt to get the memory all together
        m_collisionArray = new List<BulletXNA.CollisionDesc>();
        //m_collisionArrayPinnedHandle = GCHandle.Alloc(m_collisionArray, GCHandleType.Pinned);
        m_updateArray = new List<BulletXNA.EntityProperties>();
        //m_updateArrayPinnedHandle = GCHandle.Alloc(m_updateArray, GCHandleType.Pinned);

        // Enable very detailed logging.
        // By creating an empty logger when not logging, the log message invocation code
        //     can be left in and every call doesn't have to check for null.
        if (m_physicsLoggingEnabled)
        {
            PhysicsLogging = new Logging.LogWriter(m_physicsLoggingDir, m_physicsLoggingPrefix, m_physicsLoggingFileMinutes);
            PhysicsLogging.ErrorLogger = m_log; // for DEBUG. Let's the logger output error messages.
        }
        else
        {
            PhysicsLogging = new Logging.LogWriter();
        }

        // If Debug logging level, enable logging from the unmanaged code
        m_DebugLogCallbackHandle = null;
        if (m_log.IsDebugEnabled || PhysicsLogging.Enabled)
        {
            m_log.DebugFormat("{0}: Initialize: Setting debug callback for unmanaged code", LogHeader);
            if (PhysicsLogging.Enabled)
                // The handle is saved in a variable to make sure it doesn't get freed after this call
                m_DebugLogCallbackHandle = new BulletSimAPI.DebugLogCallback(BulletLoggerPhysLog);
            else
                m_DebugLogCallbackHandle = new BulletSimAPI.DebugLogCallback(BulletLogger);
        }

        // Get the version of the DLL
        // TODO: this doesn't work yet. Something wrong with marshaling the returned string.
        // BulletSimVersion = BulletSimAPI.GetVersion();
        // m_log.WarnFormat("{0}: BulletSim.dll version='{1}'", LogHeader, BulletSimVersion);

        // The bounding box for the simulated world. The origin is 0,0,0 unless we're
        //    a child in a mega-region.
        // Bullet actually doesn't care about the extents of the simulated
        //    area. It tracks active objects no matter where they are.
        Vector3 worldExtent = new Vector3(Constants.RegionSize, Constants.RegionSize, Constants.RegionHeight);

        // m_log.DebugFormat("{0}: Initialize: Calling BulletSimAPI.Initialize.", LogHeader);

        World = new BulletSim(0, this, BulletSimAPI.Initialize2(worldExtent, UnmanagedParams,
                                        m_maxCollisionsPerFrame, ref m_collisionArray,
                                        m_maxUpdatesPerFrame,ref m_updateArray,
                                        m_DebugLogCallbackHandle));

        Constraints = new BSConstraintCollection(World);

        TerrainManager = new BSTerrainManager(this);
        TerrainManager.CreateInitialGroundPlaneAndTerrain();

        m_log.WarnFormat("{0} Linksets implemented with {1}", LogHeader, (BSLinkset.LinksetImplementation)BSParam.LinksetImplementation);

        InTaintTime = false;
        m_initialized = true;
    }

    // All default parameter values are set here. There should be no values set in the
    // variable definitions.
    private void GetInitialParameterValues(IConfigSource config)
    {
        ConfigurationParameters parms = new ConfigurationParameters();
        UnmanagedParams[0] = parms;

        BSParam.SetParameterDefaultValues(this);

        if (config != null)
        {
            // If there are specifications in the ini file, use those values
            IConfig pConfig = config.Configs["BulletSim"];
            if (pConfig != null)
            {
                BSParam.SetParameterConfigurationValues(this, pConfig);

                // Very detailed logging for physics debugging
                m_physicsLoggingEnabled = pConfig.GetBoolean("PhysicsLoggingEnabled", false);
                m_physicsLoggingDir = pConfig.GetString("PhysicsLoggingDir", ".");
                m_physicsLoggingPrefix = pConfig.GetString("PhysicsLoggingPrefix", "physics-%REGIONNAME%-");
                m_physicsLoggingFileMinutes = pConfig.GetInt("PhysicsLoggingFileMinutes", 5);
                m_physicsLoggingDoFlush = pConfig.GetBoolean("PhysicsLoggingDoFlush", false);
                // Very detailed logging for vehicle debugging
                VehicleLoggingEnabled = pConfig.GetBoolean("VehicleLoggingEnabled", false);
                VehiclePhysicalLoggingEnabled = pConfig.GetBoolean("VehiclePhysicalLoggingEnabled", false);

                // Do any replacements in the parameters
                m_physicsLoggingPrefix = m_physicsLoggingPrefix.Replace("%REGIONNAME%", RegionName);
            }

            // The material characteristics.
            BSMaterials.InitializeFromDefaults(Params);
            if (pConfig != null)
            {
                // Let the user add new and interesting material property values.
                BSMaterials.InitializefromParameters(pConfig);
            }
        }
    }

    // A helper function that handles a true/false parameter and returns the proper float number encoding
    float ParamBoolean(IConfig config, string parmName, float deflt)
    {
        float ret = deflt;
        if (config.Contains(parmName))
        {
            ret = ConfigurationParameters.numericFalse;
            if (config.GetBoolean(parmName, false))
            {
                ret = ConfigurationParameters.numericTrue;
            }
        }
        return ret;
    }

    // Called directly from unmanaged code so don't do much
    private void BulletLogger(string msg)
    {
        m_log.Debug("[BULLETS UNMANAGED]:" + msg);
    }

    // Called directly from unmanaged code so don't do much
    private void BulletLoggerPhysLog(string msg)
    {
        DetailLog("[BULLETS UNMANAGED]:" + msg);
    }

    public override void Dispose()
    {
        // m_log.DebugFormat("{0}: Dispose()", LogHeader);

        // make sure no stepping happens while we're deleting stuff
        m_initialized = false;

        foreach (KeyValuePair<uint, BSPhysObject> kvp in PhysObjects)
        {
            kvp.Value.Destroy();
        }
        PhysObjects.Clear();

        // Now that the prims are all cleaned up, there should be no constraints left
        if (Constraints != null)
        {
            Constraints.Dispose();
            Constraints = null;
        }

        if (Shapes != null)
        {
            Shapes.Dispose();
            Shapes = null;
        }

        if (TerrainManager != null)
        {
            TerrainManager.ReleaseGroundPlaneAndTerrain();
            TerrainManager.Dispose();
            TerrainManager = null;
        }

        // Anything left in the unmanaged code should be cleaned out
        BulletSimAPI.Shutdown2(World.ptr);

        // Not logging any more
        PhysicsLogging.Close();
    }
    #endregion // Construction and Initialization

    #region Prim and Avatar addition and removal

    public override PhysicsActor AddAvatar(string avName, Vector3 position, Vector3 size, bool isFlying)
    {
        m_log.ErrorFormat("{0}: CALL TO AddAvatar in BSScene. NOT IMPLEMENTED", LogHeader);
        return null;
    }

    public override PhysicsActor AddAvatar(uint localID, string avName, Vector3 position, Vector3 size, bool isFlying)
    {
        // m_log.DebugFormat("{0}: AddAvatar: {1}", LogHeader, avName);

        if (!m_initialized) return null;

        BSCharacter actor = new BSCharacter(localID, avName, this, position, size, isFlying);
        lock (PhysObjects) PhysObjects.Add(localID, actor);

        // TODO: Remove kludge someday.
        // We must generate a collision for avatars whether they collide or not.
        // This is required by OpenSim to update avatar animations, etc.
        lock (m_avatars) m_avatars.Add(actor);

        return actor;
    }

    public override void RemoveAvatar(PhysicsActor actor)
    {
        // m_log.DebugFormat("{0}: RemoveAvatar", LogHeader);

        if (!m_initialized) return;

        BSCharacter bsactor = actor as BSCharacter;
        if (bsactor != null)
        {
            try
            {
                lock (PhysObjects) PhysObjects.Remove(actor.LocalID);
                // Remove kludge someday
                lock (m_avatars) m_avatars.Remove(bsactor);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("{0}: Attempt to remove avatar that is not in physics scene: {1}", LogHeader, e);
            }
            bsactor.Destroy();
            // bsactor.dispose();
        }
    }

    public override void RemovePrim(PhysicsActor prim)
    {
        if (!m_initialized) return;

        BSPrim bsprim = prim as BSPrim;
        if (bsprim != null)
        {
            DetailLog("{0},RemovePrim,call", bsprim.LocalID);
            // m_log.DebugFormat("{0}: RemovePrim. id={1}/{2}", LogHeader, bsprim.Name, bsprim.LocalID);
            try
            {
                lock (PhysObjects) PhysObjects.Remove(bsprim.LocalID);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0}: Attempt to remove prim that is not in physics scene: {1}", LogHeader, e);
            }
            bsprim.Destroy();
            // bsprim.dispose();
        }
        else
        {
            m_log.ErrorFormat("{0}: Attempt to remove prim that is not a BSPrim type.", LogHeader);
        }
    }

    public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, Vector3 position,
                                              Vector3 size, Quaternion rotation, bool isPhysical, uint localID)
    {
        // m_log.DebugFormat("{0}: AddPrimShape2: {1}", LogHeader, primName);

        if (!m_initialized) return null;

        DetailLog("{0},AddPrimShape,call", localID);

        BSPrim prim = new BSPrim(localID, primName, this, position, size, rotation, pbs, isPhysical);
        lock (PhysObjects) PhysObjects.Add(localID, prim);
        return prim;
    }

    // This is a call from the simulator saying that some physical property has been updated.
    // The BulletSim driver senses the changing of relevant properties so this taint
    // information call is not needed.
    public override void AddPhysicsActorTaint(PhysicsActor prim) { }

    #endregion // Prim and Avatar addition and removal

    #region Simulation
    // Simulate one timestep
    public override float Simulate(float timeStep)
    {
        // prevent simulation until we've been initialized
        if (!m_initialized) return 5.0f;

        LastTimeStep = timeStep;

        int updatedEntityCount = 0;
        //Object updatedEntitiesPtr;
        int collidersCount = 0;
        //Object collidersPtr;

        int beforeTime = 0;
        int simTime = 0;

        // update the prim states while we know the physics engine is not busy
        int numTaints = _taintOperations.Count;

        InTaintTime = true; // Only used for debugging so locking is not necessary.

        ProcessTaints();

        // Some of the physical objects requre individual, pre-step calls
        TriggerPreStepEvent(timeStep);

        // the prestep actions might have added taints
        ProcessTaints();

        InTaintTime = false; // Only used for debugging so locking is not necessary.

        // step the physical world one interval
        m_simulationStep++;
        int numSubSteps = 0;

        try
        {
            //if (VehicleLoggingEnabled) DumpVehicles();  // DEBUG
            if (PhysicsLogging.Enabled) beforeTime = Util.EnvironmentTickCount();

            numSubSteps = BulletSimAPI.PhysicsStep2(World.ptr, timeStep, m_maxSubSteps, m_fixedTimeStep,
                        out updatedEntityCount, out m_updateArray, out collidersCount, out m_collisionArray);

            if (PhysicsLogging.Enabled) simTime = Util.EnvironmentTickCountSubtract(beforeTime);
            DetailLog("{0},Simulate,call, frame={1}, nTaints={2}, simTime={3}, substeps={4}, updates={5}, colliders={6}, objWColl={7}",
                                    DetailLogZero, m_simulationStep, numTaints, simTime, numSubSteps, 
                                    updatedEntityCount, collidersCount, ObjectsWithCollisions.Count);
        }
        catch (Exception e)
        {
            m_log.WarnFormat("{0},PhysicsStep Exception: nTaints={1}, substeps={2}, updates={3}, colliders={4}, e={5}",
                        LogHeader, numTaints, numSubSteps, updatedEntityCount, collidersCount, e);
            DetailLog("{0},PhysicsStepException,call, nTaints={1}, substeps={2}, updates={3}, colliders={4}",
                        DetailLogZero, numTaints, numSubSteps, updatedEntityCount, collidersCount);
            updatedEntityCount = 0;
            collidersCount = 0;
        }

        // Don't have to use the pointers passed back since we know it is the same pinned memory we passed in.

        // Get a value for 'now' so all the collision and update routines don't have to get their own.
        SimulationNowTime = Util.EnvironmentTickCount();

        // If there were collisions, process them by sending the event to the prim.
        // Collisions must be processed before updates.
        if (collidersCount > 0)
        {
            for (int ii = 0; ii < collidersCount; ii++)
            {
                uint cA = m_collisionArray[ii].aID;
                uint cB = m_collisionArray[ii].bID;
                Vector3 point = new Vector3(m_collisionArray[ii].point.X, m_collisionArray[ii].point.Y,
                                            m_collisionArray[ii].point.Z);
                Vector3 normal = new Vector3(m_collisionArray[ii].normal.X, m_collisionArray[ii].normal.Y,
                                            m_collisionArray[ii].normal.Z); 
                SendCollision(cA, cB, point, normal, 0.01f);
                SendCollision(cB, cA, point, -normal, 0.01f);
            }
        }

        // The above SendCollision's batch up the collisions on the objects.
        //      Now push the collisions into the simulator.
        if (ObjectsWithCollisions.Count > 0)
        {
            foreach (BSPhysObject bsp in ObjectsWithCollisions)
                if (!bsp.SendCollisions())
                {
                    // If the object is done colliding, see that it's removed from the colliding list
                    ObjectsWithNoMoreCollisions.Add(bsp);
                }
        }

        // This is a kludge to get avatar movement updates.
        // The simulator expects collisions for avatars even if there are have been no collisions.
        //    The event updates avatar animations and stuff.
        // If you fix avatar animation updates, remove this overhead and let normal collision processing happen.
        foreach (BSPhysObject bsp in m_avatars)
            if (!ObjectsWithCollisions.Contains(bsp))   // don't call avatars twice
                bsp.SendCollisions();

        // Objects that are done colliding are removed from the ObjectsWithCollisions list.
        // Not done above because it is inside an iteration of ObjectWithCollisions.
        // This complex collision processing is required to create an empty collision
        //     event call after all collisions have happened on an object. This enables
        //     the simulator to generate the 'collision end' event.
        if (ObjectsWithNoMoreCollisions.Count > 0)
        {
            foreach (BSPhysObject po in ObjectsWithNoMoreCollisions)
                ObjectsWithCollisions.Remove(po);
            ObjectsWithNoMoreCollisions.Clear();
        }
        // Done with collisions.

        // If any of the objects had updated properties, tell the object it has been changed by the physics engine
        if (updatedEntityCount > 0)
        {
            for (int ii = 0; ii < updatedEntityCount; ii++)
            {

                BulletXNA.EntityProperties entprop = m_updateArray[ii];
                BSPhysObject pobj;
                if (PhysObjects.TryGetValue(entprop.ID, out pobj))
                {
                    EntityProperties prop = new EntityProperties()
                                                {
                                                    Acceleration = new Vector3(entprop.Acceleration.X, entprop.Acceleration.Y, entprop.Acceleration.Z),
                                                    ID = entprop.ID,
                                                    Position = new Vector3(entprop.Position.X,entprop.Position.Y,entprop.Position.Z),
                                                    Rotation = new Quaternion(entprop.Rotation.X,entprop.Rotation.Y,entprop.Rotation.Z,entprop.Rotation.W),
                                                    RotationalVelocity = new Vector3(entprop.AngularVelocity.X,entprop.AngularVelocity.Y,entprop.AngularVelocity.Z),
                                                    Velocity = new Vector3(entprop.Velocity.X,entprop.Velocity.Y,entprop.Velocity.Z)
                                                };
                    //m_log.Debug(pobj.Name + ":" + prop.ToString() + "\n");
                    pobj.UpdateProperties(prop);
                }
            }
        }

        TriggerPostStepEvent(timeStep);

        // The following causes the unmanaged code to output ALL the values found in ALL the objects in the world.
        // Only enable this in a limited test world with few objects.
        // BulletSimAPI.DumpAllInfo2(World.ptr);    // DEBUG DEBUG DEBUG

        // The physics engine returns the number of milliseconds it simulated this call.
        // These are summed and normalized to one second and divided by 1000 to give the reported physics FPS.
        // Multiply by 55 to give a nominal frame rate of 55.
        return (float)numSubSteps * m_fixedTimeStep * 1000f * 55f;
    }

    // Something has collided
    private void SendCollision(uint localID, uint collidingWith, Vector3 collidePoint, Vector3 collideNormal, float penetration)
    {
        if (localID <= TerrainManager.HighestTerrainID)
        {
            return;         // don't send collisions to the terrain
        }

        BSPhysObject collider;
        if (!PhysObjects.TryGetValue(localID, out collider))
        {
            // If the object that is colliding cannot be found, just ignore the collision.
            DetailLog("{0},BSScene.SendCollision,colliderNotInObjectList,id={1},with={2}", DetailLogZero, localID, collidingWith);
            return;
        }

        // The terrain is not in the physical object list so 'collidee' can be null when Collide() is called.
        BSPhysObject collidee = null;
        PhysObjects.TryGetValue(collidingWith, out collidee);

        // DetailLog("{0},BSScene.SendCollision,collide,id={1},with={2}", DetailLogZero, localID, collidingWith);

        if (collider.Collide(collidingWith, collidee, collidePoint, collideNormal, penetration))
        {
            // If a collision was posted, remember to send it to the simulator
            ObjectsWithCollisions.Add(collider);
        }

        return;
    }

    #endregion // Simulation

    public override void GetResults() { }

    #region Terrain

    public override void SetTerrain(float[] heightMap) {
        TerrainManager.SetTerrain(heightMap);
    }

    public override void SetWaterLevel(float baseheight)
    {
        SimpleWaterLevel = baseheight;
    }

    public override void DeleteTerrain()
    {
        // m_log.DebugFormat("{0}: DeleteTerrain()", LogHeader);
    }

    // Although no one seems to check this, I do support combining.
    public override bool SupportsCombining()
    {
        return TerrainManager.SupportsCombining();
    }
    // This call says I am a child to region zero in a mega-region. 'pScene' is that
    //    of region zero, 'offset' is my offset from regions zero's origin, and
    //    'extents' is the largest XY that is handled in my region.
    public override void Combine(PhysicsScene pScene, Vector3 offset, Vector3 extents)
    {
        TerrainManager.Combine(pScene, offset, extents);
    }

    // Unhook all the combining that I know about.
    public override void UnCombine(PhysicsScene pScene)
    {
        TerrainManager.UnCombine(pScene);
    }

    #endregion // Terrain

    public override Dictionary<uint, float> GetTopColliders()
    {
        return new Dictionary<uint, float>();
    }

    public override bool IsThreaded { get { return false;  } }

    #region Taints
    // The simulation execution order is:
    // Simulate()
    //    DoOneTimeTaints
    //    TriggerPreStepEvent
    //    DoOneTimeTaints
    //    Step()
    //       ProcessAndForwardCollisions
    //       ProcessAndForwardPropertyUpdates
    //    TriggerPostStepEvent

    // Calls to the PhysicsActors can't directly call into the physics engine
    //       because it might be busy. We delay changes to a known time.
    // We rely on C#'s closure to save and restore the context for the delegate.
    public void TaintedObject(String ident, TaintCallback callback)
    {
        if (!m_initialized) return;

        lock (_taintLock)
        {
            _taintOperations.Add(new TaintCallbackEntry(ident, callback));
        }

        return;
    }

    // Sometimes a potentially tainted operation can be used in and out of taint time.
    // This routine executes the command immediately if in taint-time otherwise it is queued.
    public void TaintedObject(bool inTaintTime, string ident, TaintCallback callback)
    {
        if (inTaintTime)
            callback();
        else
            TaintedObject(ident, callback);
    }

    private void TriggerPreStepEvent(float timeStep)
    {
        PreStepAction actions = BeforeStep;
        if (actions != null)
            actions(timeStep);

    }

    private void TriggerPostStepEvent(float timeStep)
    {
        PreStepAction actions = AfterStep;
        if (actions != null)
            actions(timeStep);

    }

    // When someone tries to change a property on a BSPrim or BSCharacter, the object queues
    // a callback into itself to do the actual property change. That callback is called
    // here just before the physics engine is called to step the simulation.
    public void ProcessTaints()
    {
        ProcessRegularTaints();
        ProcessPostTaintTaints();
    }

    private void ProcessRegularTaints()
    {
        if (_taintOperations.Count > 0)  // save allocating new list if there is nothing to process
        {
            // swizzle a new list into the list location so we can process what's there
            List<TaintCallbackEntry> oldList;
            lock (_taintLock)
            {
                oldList = _taintOperations;
                _taintOperations = new List<TaintCallbackEntry>();
            }

            foreach (TaintCallbackEntry tcbe in oldList)
            {
                try
                {
                    DetailLog("{0},BSScene.ProcessTaints,doTaint,id={1}", DetailLogZero, tcbe.ident); // DEBUG DEBUG DEBUG
                    tcbe.callback();
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("{0}: ProcessTaints: {1}: Exception: {2}", LogHeader, tcbe.ident, e);
                }
            }
            oldList.Clear();
        }
    }

    // Schedule an update to happen after all the regular taints are processed.
    // Note that new requests for the same operation ("ident") for the same object ("ID")
    //     will replace any previous operation by the same object.
    public void PostTaintObject(String ident, uint ID, TaintCallback callback)
    {
        string uniqueIdent = ident + "-" + ID.ToString();
        lock (_taintLock)
        {
            _postTaintOperations[uniqueIdent] = new TaintCallbackEntry(uniqueIdent, callback);
        }

        return;
    }

    // Taints that happen after the normal taint processing but before the simulation step.
    private void ProcessPostTaintTaints()
    {
        if (_postTaintOperations.Count > 0)
        {
            Dictionary<string, TaintCallbackEntry> oldList;
            lock (_taintLock)
            {
                oldList = _postTaintOperations;
                _postTaintOperations = new Dictionary<string, TaintCallbackEntry>();
            }

            foreach (KeyValuePair<string,TaintCallbackEntry> kvp in oldList)
            {
                try
                {
                    DetailLog("{0},BSScene.ProcessPostTaintTaints,doTaint,id={1}", DetailLogZero, kvp.Key); // DEBUG DEBUG DEBUG
                    kvp.Value.callback();
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("{0}: ProcessPostTaintTaints: {1}: Exception: {2}", LogHeader, kvp.Key, e);
                }
            }
            oldList.Clear();
        }
    }

    // Only used for debugging. Does not change state of anything so locking is not necessary.
    public bool AssertInTaintTime(string whereFrom)
    {
        if (!InTaintTime)
        {
            DetailLog("{0},BSScene.AssertInTaintTime,NOT IN TAINT TIME,Region={1},Where={2}", DetailLogZero, RegionName, whereFrom);
            m_log.ErrorFormat("{0} NOT IN TAINT TIME!! Region={1}, Where={2}", LogHeader, RegionName, whereFrom);
            Util.PrintCallStack();  // Prints the stack into the DEBUG log file.
        }
        return InTaintTime;
    }

    #endregion // Taints

    #region INI and command line parameter processing

    #region IPhysicsParameters
    // Get the list of parameters this physics engine supports
    public PhysParameterEntry[] GetParameterList()
    {
        BSParam.BuildParameterTable();
        return BSParam.SettableParameters;
    }

    // Set parameter on a specific or all instances.
    // Return 'false' if not able to set the parameter.
    // Setting the value in the m_params block will change the value the physics engine
    //   will use the next time since it's pinned and shared memory.
    // Some of the values require calling into the physics engine to get the new
    //   value activated ('terrainFriction' for instance).
    public bool SetPhysicsParameter(string parm, float val, uint localID)
    {
        bool ret = false;
        BSParam.ParameterDefn theParam;
        if (BSParam.TryGetParameter(parm, out theParam))
        {
            theParam.setter(this, parm, localID, val);
            ret = true;
        }
        return ret;
    }

    // update all the localIDs specified
    // If the local ID is APPLY_TO_NONE, just change the default value
    // If the localID is APPLY_TO_ALL change the default value and apply the new value to all the lIDs
    // If the localID is a specific object, apply the parameter change to only that object
    internal delegate void AssignVal(float x);
    internal void UpdateParameterObject(AssignVal setDefault, string parm, uint localID, float val)
    {
        List<uint> objectIDs = new List<uint>();
        switch (localID)
        {
            case PhysParameterEntry.APPLY_TO_NONE:
                setDefault(val);            // setting only the default value
                // This will cause a call into the physical world if some operation is specified (SetOnObject).
                objectIDs.Add(TERRAIN_ID);
                TaintedUpdateParameter(parm, objectIDs, val);
                break;
            case PhysParameterEntry.APPLY_TO_ALL:
                setDefault(val);  // setting ALL also sets the default value
                lock (PhysObjects) objectIDs = new List<uint>(PhysObjects.Keys);
                TaintedUpdateParameter(parm, objectIDs, val);
                break;
            default:
                // setting only one localID
                objectIDs.Add(localID);
                TaintedUpdateParameter(parm, objectIDs, val);
                break;
        }
    }

    // schedule the actual updating of the paramter to when the phys engine is not busy
    private void TaintedUpdateParameter(string parm, List<uint> lIDs, float val)
    {
        float xval = val;
        List<uint> xlIDs = lIDs;
        string xparm = parm;
        TaintedObject("BSScene.UpdateParameterSet", delegate() {
            BSParam.ParameterDefn thisParam;
            if (BSParam.TryGetParameter(xparm, out thisParam))
            {
                if (thisParam.onObject != null)
                {
                    foreach (uint lID in xlIDs)
                    {
                        BSPhysObject theObject = null;
                        PhysObjects.TryGetValue(lID, out theObject);
                        thisParam.onObject(this, theObject, xval);
                    }
                }
            }
        });
    }

    // Get parameter.
    // Return 'false' if not able to get the parameter.
    public bool GetPhysicsParameter(string parm, out float value)
    {
        float val = 0f;
        bool ret = false;
        BSParam.ParameterDefn theParam;
        if (BSParam.TryGetParameter(parm, out theParam))
        {
            val = theParam.getter(this);
            ret = true;
        }
        value = val;
        return ret;
    }

    #endregion IPhysicsParameters

    #endregion Runtime settable parameters

    // Invoke the detailed logger and output something if it's enabled.
    public void DetailLog(string msg, params Object[] args)
    {
        PhysicsLogging.Write(msg, args);
        // Add the Flush() if debugging crashes. Gets all the messages written out.
        if (m_physicsLoggingDoFlush) PhysicsLogging.Flush();
    }
    // Used to fill in the LocalID when there isn't one. It's the correct number of characters.
    public const string DetailLogZero = "0000000000";

}
}
