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
// Move all logic out of the C++ code and into the C# code for easier future modifications.
// Test sculpties (verified that they don't work)
// Compute physics FPS reasonably
// Based on material, set density and friction
// Don't use constraints in linksets of non-physical objects. Means having to move children manually.
// Four states of prim: Physical, regular, phantom and selected. Are we modeling these correctly?
//     In SL one can set both physical and phantom (gravity, does not effect others, makes collisions with ground)
//     At the moment, physical and phantom causes object to drop through the terrain
// Physical phantom objects and related typing (collision options )
// Check out llVolumeDetect. Must do something for that.
// Use collision masks for collision with terrain and phantom objects
// More efficient memory usage when passing hull information from BSPrim to BulletSim
// Should prim.link() and prim.delink() membership checking happen at taint time?
// Mesh sharing. Use meshHash to tell if we already have a hull of that shape and only create once.
// Do attachments need to be handled separately? Need collision events. Do not collide with VolumeDetect
// Implement LockAngularMotion
// Decide if clearing forces is the right thing to do when setting position (BulletSim::SetObjectTranslation)
// Remove mesh and Hull stuff. Use mesh passed to bullet and use convexdecom from bullet.
// Add PID movement operations. What does ScenePresence.MoveToTarget do?
// Check terrain size. 128 or 127?
// Raycast
//
namespace OpenSim.Region.Physics.BulletSPlugin
{
public class BSScene : PhysicsScene, IPhysicsParameters
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

    // List of all the objects that have vehicle properties and should be called
    //    to update each physics step.
    private List<BSPhysObject> m_vehicles = new List<BSPhysObject>();

    // let my minuions use my logger
    public ILog Logger { get { return m_log; } }

    // If non-zero, the number of simulation steps between calls to the physics
    //    engine to output detailed physics stats. Debug logging level must be on also.
    private int m_detailedStatsStep = 0;

    public IMesher mesher;
    // Level of Detail values kept as float because that's what the Meshmerizer wants
    public float MeshLOD { get; private set; }
    public float MeshMegaPrimLOD { get; private set; }
    public float MeshMegaPrimThreshold { get; private set; }
    public float SculptLOD { get; private set; }

    public uint WorldID { get; private set; }
    public BulletSim World { get; private set; }

    // All the constraints that have been allocated in this instance.
    public BSConstraintCollection Constraints { get; private set; }

    // Simulation parameters
    private int m_maxSubSteps;
    private float m_fixedTimeStep;
    private long m_simulationStep = 0;
    public long SimulationStep { get { return m_simulationStep; } }

    // A value of the time now so all the collision and update routines do not have to get their own
    // Set to 'now' just before all the prims and actors are called for collisions and updates
    public int SimulationNowTime { get; private set; }

    // True if initialized and ready to do simulation steps
    private bool m_initialized = false;

    // Pinned memory used to pass step information between managed and unmanaged
    private int m_maxCollisionsPerFrame;
    private CollisionDesc[] m_collisionArray;
    private GCHandle m_collisionArrayPinnedHandle;

    private int m_maxUpdatesPerFrame;
    private EntityProperties[] m_updateArray;
    private GCHandle m_updateArrayPinnedHandle;

    public bool ShouldMeshSculptedPrim { get; private set; }   // cause scuplted prims to get meshed
    public bool ShouldForceSimplePrimMeshing { get; private set; }   // if a cube or sphere, let Bullet do internal shapes

    public float PID_D { get; private set; }    // derivative
    public float PID_P { get; private set; }    // proportional

    public const uint TERRAIN_ID = 0;       // OpenSim senses terrain with a localID of zero
    public const uint GROUNDPLANE_ID = 1;
    public const uint CHILDTERRAIN_ID = 2;  // Terrain allocated based on our mega-prim childre start here

    private float m_waterLevel;
    public BSTerrainManager TerrainManager { get; private set; }

    public ConfigurationParameters Params
    {
        get { return m_params[0]; }
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

    public float MaximumObjectMass { get; private set; }

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
    private List<TaintCallbackEntry> _taintedObjects;

    // A pointer to an instance if this structure is passed to the C++ code
    // Used to pass basic configuration values to the unmanaged code.
    ConfigurationParameters[] m_params;
    GCHandle m_paramsHandle;

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
    // 'true' of the vehicle code is to log lots of details
    public bool VehicleLoggingEnabled { get; private set; }

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
        _taintedObjects = new List<TaintCallbackEntry>();
        PhysObjects = new Dictionary<uint, BSPhysObject>();
        Shapes = new BSShapeCollection(this);

        // Allocate pinned memory to pass parameters.
        m_params = new ConfigurationParameters[1];
        m_paramsHandle = GCHandle.Alloc(m_params, GCHandleType.Pinned);

        // Set default values for physics parameters plus any overrides from the ini file
        GetInitialParameterValues(config);

        // allocate more pinned memory close to the above in an attempt to get the memory all together
        m_collisionArray = new CollisionDesc[m_maxCollisionsPerFrame];
        m_collisionArrayPinnedHandle = GCHandle.Alloc(m_collisionArray, GCHandleType.Pinned);
        m_updateArray = new EntityProperties[m_maxUpdatesPerFrame];
        m_updateArrayPinnedHandle = GCHandle.Alloc(m_updateArray, GCHandleType.Pinned);

        // Enable very detailed logging.
        // By creating an empty logger when not logging, the log message invocation code
        //     can be left in and every call doesn't have to check for null.
        if (m_physicsLoggingEnabled)
        {
            PhysicsLogging = new Logging.LogWriter(m_physicsLoggingDir, m_physicsLoggingPrefix, m_physicsLoggingFileMinutes);
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
        // Turns out that Bullet really doesn't care about the extents of the simulated
        //    area. It tracks active objects no matter where they are.
        Vector3 worldExtent = new Vector3(Constants.RegionSize, Constants.RegionSize, Constants.RegionHeight);

        // m_log.DebugFormat("{0}: Initialize: Calling BulletSimAPI.Initialize.", LogHeader);
        WorldID = BulletSimAPI.Initialize(worldExtent, m_paramsHandle.AddrOfPinnedObject(),
                                        m_maxCollisionsPerFrame, m_collisionArrayPinnedHandle.AddrOfPinnedObject(),
                                        m_maxUpdatesPerFrame, m_updateArrayPinnedHandle.AddrOfPinnedObject(),
                                        m_DebugLogCallbackHandle);

        // Initialization to support the transition to a new API which puts most of the logic
        //   into the C# code so it is easier to modify and add to.
        World = new BulletSim(WorldID, this, BulletSimAPI.GetSimHandle2(WorldID));

        Constraints = new BSConstraintCollection(World);

        TerrainManager = new BSTerrainManager(this);
        TerrainManager.CreateInitialGroundPlaneAndTerrain();

        m_initialized = true;
    }

    // All default parameter values are set here. There should be no values set in the
    // variable definitions.
    private void GetInitialParameterValues(IConfigSource config)
    {
        ConfigurationParameters parms = new ConfigurationParameters();
        m_params[0] = parms;

        SetParameterDefaultValues();

        if (config != null)
        {
            // If there are specifications in the ini file, use those values
            IConfig pConfig = config.Configs["BulletSim"];
            if (pConfig != null)
            {
                SetParameterConfigurationValues(pConfig);

                // Very detailed logging for physics debugging
                m_physicsLoggingEnabled = pConfig.GetBoolean("PhysicsLoggingEnabled", false);
                m_physicsLoggingDir = pConfig.GetString("PhysicsLoggingDir", ".");
                m_physicsLoggingPrefix = pConfig.GetString("PhysicsLoggingPrefix", "physics-%REGIONNAME%-");
                m_physicsLoggingFileMinutes = pConfig.GetInt("PhysicsLoggingFileMinutes", 5);
                // Very detailed logging for vehicle debugging
                VehicleLoggingEnabled = pConfig.GetBoolean("VehicleLoggingEnabled", false);

                // Do any replacements in the parameters
                m_physicsLoggingPrefix = m_physicsLoggingPrefix.Replace("%REGIONNAME%", RegionName);
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
        PhysicsLogging.Write("[BULLETS UNMANAGED]:" + msg);
    }

    public override void Dispose()
    {
        // m_log.DebugFormat("{0}: Dispose()", LogHeader);

        // make sure no stepping happens while we're deleting stuff
        m_initialized = false;

        TerrainManager.ReleaseGroundPlaneAndTerrain();

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

        // Anything left in the unmanaged code should be cleaned out
        BulletSimAPI.Shutdown(WorldID);

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
        int updatedEntityCount = 0;
        IntPtr updatedEntitiesPtr;
        int collidersCount = 0;
        IntPtr collidersPtr;

        int beforeTime = 0;
        int simTime = 0;

        // prevent simulation until we've been initialized
        if (!m_initialized) return 5.0f;

        // update the prim states while we know the physics engine is not busy
        int numTaints = _taintedObjects.Count;
        ProcessTaints();

        // Some of the prims operate with special vehicle properties
        ProcessVehicles(timeStep);
        numTaints += _taintedObjects.Count;
        ProcessTaints();    // the vehicles might have added taints

        // step the physical world one interval
        m_simulationStep++;
        int numSubSteps = 0;
        try
        {
            if (PhysicsLogging.Enabled) beforeTime = Util.EnvironmentTickCount();

            numSubSteps = BulletSimAPI.PhysicsStep(WorldID, timeStep, m_maxSubSteps, m_fixedTimeStep,
                        out updatedEntityCount, out updatedEntitiesPtr, out collidersCount, out collidersPtr);

            if (PhysicsLogging.Enabled) simTime = Util.EnvironmentTickCountSubtract(beforeTime);
            DetailLog("{0},Simulate,call, nTaints={1}, simTime={2}, substeps={3}, updates={4}, colliders={5}",
                        DetailLogZero, numTaints, simTime, numSubSteps, updatedEntityCount, collidersCount);
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


        // Don't have to use the pointers passed back since we know it is the same pinned memory we passed in

        // Get a value for 'now' so all the collision and update routines don't have to get their own
        SimulationNowTime = Util.EnvironmentTickCount();

        // If there were collisions, process them by sending the event to the prim.
        // Collisions must be processed before updates.
        if (collidersCount > 0)
        {
            for (int ii = 0; ii < collidersCount; ii++)
            {
                uint cA = m_collisionArray[ii].aID;
                uint cB = m_collisionArray[ii].bID;
                Vector3 point = m_collisionArray[ii].point;
                Vector3 normal = m_collisionArray[ii].normal;
                SendCollision(cA, cB, point, normal, 0.01f);
                SendCollision(cB, cA, point, -normal, 0.01f);
            }
        }

        // This is a kludge to get avatar movement updates.
        //   ODE sends collisions for avatars even if there are have been no collisions. This updates
        //   avatar animations and stuff.
        // If you fix avatar animation updates, remove this overhead and let normal collision processing happen.
        foreach (BSPhysObject bsp in m_avatars)
            bsp.SendCollisions();

        // The above SendCollision's batch up the collisions on the objects.
        //      Now push the collisions into the simulator.
        if (ObjectsWithCollisions.Count > 0)
        {
            foreach (BSPhysObject bsp in ObjectsWithCollisions)
                if (!m_avatars.Contains(bsp))   // don't call avatars twice
                    if (!bsp.SendCollisions())
                    {
                        // If the object is done colliding, see that it's removed from the colliding list
                        ObjectsWithNoMoreCollisions.Add(bsp);
                    }
        }

        // Objects that are done colliding are removed from the ObjectsWithCollisions list.
        // This can't be done by SendCollisions because it is inside an iteration of ObjectWithCollisions.
        if (ObjectsWithNoMoreCollisions.Count > 0)
        {
            foreach (BSPhysObject po in ObjectsWithNoMoreCollisions)
                ObjectsWithCollisions.Remove(po);
            ObjectsWithNoMoreCollisions.Clear();
        }

        // If any of the objects had updated properties, tell the object it has been changed by the physics engine
        if (updatedEntityCount > 0)
        {
            for (int ii = 0; ii < updatedEntityCount; ii++)
            {
                EntityProperties entprop = m_updateArray[ii];
                BSPhysObject pobj;
                if (PhysObjects.TryGetValue(entprop.ID, out pobj))
                {
                    pobj.UpdateProperties(entprop);
                }
            }
        }

        // If enabled, call into the physics engine to dump statistics
        if (m_detailedStatsStep > 0)
        {
            if ((m_simulationStep % m_detailedStatsStep) == 0)
            {
                BulletSimAPI.DumpBulletStatistics();
            }
        }

        // The physics engine returns the number of milliseconds it simulated this call.
        // These are summed and normalized to one second and divided by 1000 to give the reported physics FPS.
        // Since Bullet normally does 5 or 6 substeps, this will normally sum to about 60 FPS.
        return numSubSteps * m_fixedTimeStep * 1000;
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

        DetailLog("{0},BSScene.SendCollision,collide,id={1},with={2}", DetailLogZero, localID, collidingWith);

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
        m_waterLevel = baseheight;
    }
    // Someday....
    public float GetWaterLevelAtXYZ(Vector3 loc)
    {
        return m_waterLevel;
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

    // Calls to the PhysicsActors can't directly call into the physics engine
    // because it might be busy. We delay changes to a known time.
    // We rely on C#'s closure to save and restore the context for the delegate.
    public void TaintedObject(String ident, TaintCallback callback)
    {
        if (!m_initialized) return;

        lock (_taintLock)
        {
            _taintedObjects.Add(new TaintCallbackEntry(ident, callback));
        }

        return;
    }

    // When someone tries to change a property on a BSPrim or BSCharacter, the object queues
    // a callback into itself to do the actual property change. That callback is called
    // here just before the physics engine is called to step the simulation.
    public void ProcessTaints()
    {
        if (_taintedObjects.Count > 0)  // save allocating new list if there is nothing to process
        {
            // swizzle a new list into the list location so we can process what's there
            List<TaintCallbackEntry> oldList;
            lock (_taintLock)
            {
                oldList = _taintedObjects;
                _taintedObjects = new List<TaintCallbackEntry>();
            }

            foreach (TaintCallbackEntry tcbe in oldList)
            {
                try
                {
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

    #region Vehicles

    public void VehicleInSceneTypeChanged(BSPrim vehic, Vehicle newType)
    {
        if (newType == Vehicle.TYPE_NONE)
        {
            RemoveVehiclePrim(vehic);
        }
        else
        {
            // make it so the scene will call us each tick to do vehicle things
           AddVehiclePrim(vehic);
        }
    }

    // Make so the scene will call this prim for vehicle actions each tick.
    // Safe to call if prim is already in the vehicle list.
    public void AddVehiclePrim(BSPrim vehicle)
    {
        lock (m_vehicles)
        {
            if (!m_vehicles.Contains(vehicle))
            {
                m_vehicles.Add(vehicle);
            }
        }
    }

    // Remove a prim from our list of vehicles.
    // Safe to call if the prim is not in the vehicle list.
    public void RemoveVehiclePrim(BSPrim vehicle)
    {
        lock (m_vehicles)
        {
            if (m_vehicles.Contains(vehicle))
            {
                m_vehicles.Remove(vehicle);
            }
        }
    }

    // Some prims have extra vehicle actions
    // no locking because only called when physics engine is not busy
    private void ProcessVehicles(float timeStep)
    {
        foreach (BSPhysObject pobj in m_vehicles)
        {
            pobj.StepVehicle(timeStep);
        }
    }
    #endregion Vehicles

    #region INI and command line parameter processing

    delegate void ParamUser(BSScene scene, IConfig conf, string paramName, float val);
    delegate float ParamGet(BSScene scene);
    delegate void ParamSet(BSScene scene, string paramName, uint localID, float val);

    private struct ParameterDefn
    {
        public string name;         // string name of the parameter
        public string desc;         // a short description of what the parameter means
        public float defaultValue;  // default value if not specified anywhere else
        public ParamUser userParam; // get the value from the configuration file
        public ParamGet getter;     // return the current value stored for this parameter
        public ParamSet setter;     // set the current value for this parameter
        public ParameterDefn(string n, string d, float v, ParamUser u, ParamGet g, ParamSet s)
        {
            name = n;
            desc = d;
            defaultValue = v;
            userParam = u;
            getter = g;
            setter = s;
        }
    }

    // List of all of the externally visible parameters.
    // For each parameter, this table maps a text name to getter and setters.
    // To add a new externally referencable/settable parameter, add the paramter storage
    //    location somewhere in the program and make an entry in this table with the
    //    getters and setters.
    // It is easiest to find an existing definition and copy it.
    // Parameter values are floats. Booleans are converted to a floating value.
    //
    // A ParameterDefn() takes the following parameters:
    //    -- the text name of the parameter. This is used for console input and ini file.
    //    -- a short text description of the parameter. This shows up in the console listing.
    //    -- a delegate for fetching the parameter from the ini file.
    //          Should handle fetching the right type from the ini file and converting it.
    //    -- a delegate for getting the value as a float
    //    -- a delegate for setting the value from a float
    //
    // The single letter parameters for the delegates are:
    //    s = BSScene
    //    p = string parameter name
    //    l = localID of referenced object
    //    v = float value
    //    cf = parameter configuration class (for fetching values from ini file)
    private ParameterDefn[] ParameterDefinitions =
    {
        new ParameterDefn("MeshSculptedPrim", "Whether to create meshes for sculpties",
            ConfigurationParameters.numericTrue,
            (s,cf,p,v) => { s.ShouldMeshSculptedPrim = cf.GetBoolean(p, s.BoolNumeric(v)); },
            (s) => { return s.NumericBool(s.ShouldMeshSculptedPrim); },
            (s,p,l,v) => { s.ShouldMeshSculptedPrim = s.BoolNumeric(v); } ),
        new ParameterDefn("ForceSimplePrimMeshing", "If true, only use primitive meshes for objects",
            ConfigurationParameters.numericFalse,
            (s,cf,p,v) => { s.ShouldForceSimplePrimMeshing = cf.GetBoolean(p, s.BoolNumeric(v)); },
            (s) => { return s.NumericBool(s.ShouldForceSimplePrimMeshing); },
            (s,p,l,v) => { s.ShouldForceSimplePrimMeshing = s.BoolNumeric(v); } ),

        new ParameterDefn("MeshLevelOfDetail", "Level of detail to render meshes (32, 16, 8 or 4. 32=most detailed)",
            8f,
            (s,cf,p,v) => { s.MeshLOD = (float)cf.GetInt(p, (int)v); },
            (s) => { return s.MeshLOD; },
            (s,p,l,v) => { s.MeshLOD = v; } ),
        new ParameterDefn("MeshLevelOfDetailMegaPrim", "Level of detail to render meshes larger than threshold meters",
            16f,
            (s,cf,p,v) => { s.MeshMegaPrimLOD = (float)cf.GetInt(p, (int)v); },
            (s) => { return s.MeshMegaPrimLOD; },
            (s,p,l,v) => { s.MeshMegaPrimLOD = v; } ),
        new ParameterDefn("MeshLevelOfDetailMegaPrimThreshold", "Size (in meters) of a mesh before using MeshMegaPrimLOD",
            10f,
            (s,cf,p,v) => { s.MeshMegaPrimThreshold = (float)cf.GetInt(p, (int)v); },
            (s) => { return s.MeshMegaPrimThreshold; },
            (s,p,l,v) => { s.MeshMegaPrimThreshold = v; } ),
        new ParameterDefn("SculptLevelOfDetail", "Level of detail to render sculpties (32, 16, 8 or 4. 32=most detailed)",
            32f,
            (s,cf,p,v) => { s.SculptLOD = (float)cf.GetInt(p, (int)v); },
            (s) => { return s.SculptLOD; },
            (s,p,l,v) => { s.SculptLOD = v; } ),

        new ParameterDefn("MaxSubStep", "In simulation step, maximum number of substeps",
            10f,
            (s,cf,p,v) => { s.m_maxSubSteps = cf.GetInt(p, (int)v); },
            (s) => { return (float)s.m_maxSubSteps; },
            (s,p,l,v) => { s.m_maxSubSteps = (int)v; } ),
        new ParameterDefn("FixedTimeStep", "In simulation step, seconds of one substep (1/60)",
            1f / 60f,
            (s,cf,p,v) => { s.m_fixedTimeStep = cf.GetFloat(p, v); },
            (s) => { return (float)s.m_fixedTimeStep; },
            (s,p,l,v) => { s.m_fixedTimeStep = v; } ),
        new ParameterDefn("MaxCollisionsPerFrame", "Max collisions returned at end of each frame",
            2048f,
            (s,cf,p,v) => { s.m_maxCollisionsPerFrame = cf.GetInt(p, (int)v); },
            (s) => { return (float)s.m_maxCollisionsPerFrame; },
            (s,p,l,v) => { s.m_maxCollisionsPerFrame = (int)v; } ),
        new ParameterDefn("MaxUpdatesPerFrame", "Max updates returned at end of each frame",
            8000f,
            (s,cf,p,v) => { s.m_maxUpdatesPerFrame = cf.GetInt(p, (int)v); },
            (s) => { return (float)s.m_maxUpdatesPerFrame; },
            (s,p,l,v) => { s.m_maxUpdatesPerFrame = (int)v; } ),
        new ParameterDefn("MaxObjectMass", "Maximum object mass (10000.01)",
            10000.01f,
            (s,cf,p,v) => { s.MaximumObjectMass = cf.GetFloat(p, v); },
            (s) => { return (float)s.MaximumObjectMass; },
            (s,p,l,v) => { s.MaximumObjectMass = v; } ),

        new ParameterDefn("PID_D", "Derivitive factor for motion smoothing",
            2200f,
            (s,cf,p,v) => { s.PID_D = cf.GetFloat(p, v); },
            (s) => { return (float)s.PID_D; },
            (s,p,l,v) => { s.PID_D = v; } ),
        new ParameterDefn("PID_P", "Parameteric factor for motion smoothing",
            900f,
            (s,cf,p,v) => { s.PID_P = cf.GetFloat(p, v); },
            (s) => { return (float)s.PID_P; },
            (s,p,l,v) => { s.PID_P = v; } ),

        new ParameterDefn("DefaultFriction", "Friction factor used on new objects",
            0.5f,
            (s,cf,p,v) => { s.m_params[0].defaultFriction = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].defaultFriction; },
            (s,p,l,v) => { s.m_params[0].defaultFriction = v; } ),
        new ParameterDefn("DefaultDensity", "Density for new objects" ,
            10.000006836f,  // Aluminum g/cm3
            (s,cf,p,v) => { s.m_params[0].defaultDensity = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].defaultDensity; },
            (s,p,l,v) => { s.m_params[0].defaultDensity = v; } ),
        new ParameterDefn("DefaultRestitution", "Bouncyness of an object" ,
            0f,
            (s,cf,p,v) => { s.m_params[0].defaultRestitution = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].defaultRestitution; },
            (s,p,l,v) => { s.m_params[0].defaultRestitution = v; } ),
        new ParameterDefn("CollisionMargin", "Margin around objects before collisions are calculated (must be zero!)",
            0f,
            (s,cf,p,v) => { s.m_params[0].collisionMargin = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].collisionMargin; },
            (s,p,l,v) => { s.m_params[0].collisionMargin = v; } ),
        new ParameterDefn("Gravity", "Vertical force of gravity (negative means down)",
            -9.80665f,
            (s,cf,p,v) => { s.m_params[0].gravity = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].gravity; },
            (s,p,l,v) => { s.m_params[0].gravity = v; s.TaintedUpdateParameter(p,l,v); } ),


        new ParameterDefn("LinearDamping", "Factor to damp linear movement per second (0.0 - 1.0)",
            0f,
            (s,cf,p,v) => { s.m_params[0].linearDamping = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].linearDamping; },
            (s,p,l,v) => { s.UpdateParameterObject(ref s.m_params[0].linearDamping, p, l, v); } ),
        new ParameterDefn("AngularDamping", "Factor to damp angular movement per second (0.0 - 1.0)",
            0f,
            (s,cf,p,v) => { s.m_params[0].angularDamping = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].angularDamping; },
            (s,p,l,v) => { s.UpdateParameterObject(ref s.m_params[0].angularDamping, p, l, v); } ),
        new ParameterDefn("DeactivationTime", "Seconds before considering an object potentially static",
            0.2f,
            (s,cf,p,v) => { s.m_params[0].deactivationTime = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].deactivationTime; },
            (s,p,l,v) => { s.UpdateParameterObject(ref s.m_params[0].deactivationTime, p, l, v); } ),
        new ParameterDefn("LinearSleepingThreshold", "Seconds to measure linear movement before considering static",
            0.8f,
            (s,cf,p,v) => { s.m_params[0].linearSleepingThreshold = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].linearSleepingThreshold; },
            (s,p,l,v) => { s.UpdateParameterObject(ref s.m_params[0].linearSleepingThreshold, p, l, v); } ),
        new ParameterDefn("AngularSleepingThreshold", "Seconds to measure angular movement before considering static",
            1.0f,
            (s,cf,p,v) => { s.m_params[0].angularSleepingThreshold = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].angularSleepingThreshold; },
            (s,p,l,v) => { s.UpdateParameterObject(ref s.m_params[0].angularSleepingThreshold, p, l, v); } ),
        new ParameterDefn("CcdMotionThreshold", "Continuious collision detection threshold (0 means no CCD)" ,
            0f,     // set to zero to disable
            (s,cf,p,v) => { s.m_params[0].ccdMotionThreshold = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].ccdMotionThreshold; },
            (s,p,l,v) => { s.UpdateParameterObject(ref s.m_params[0].ccdMotionThreshold, p, l, v); } ),
        new ParameterDefn("CcdSweptSphereRadius", "Continuious collision detection test radius" ,
            0f,
            (s,cf,p,v) => { s.m_params[0].ccdSweptSphereRadius = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].ccdSweptSphereRadius; },
            (s,p,l,v) => { s.UpdateParameterObject(ref s.m_params[0].ccdSweptSphereRadius, p, l, v); } ),
        new ParameterDefn("ContactProcessingThreshold", "Distance between contacts before doing collision check" ,
            0.1f,
            (s,cf,p,v) => { s.m_params[0].contactProcessingThreshold = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].contactProcessingThreshold; },
            (s,p,l,v) => { s.UpdateParameterObject(ref s.m_params[0].contactProcessingThreshold, p, l, v); } ),

        new ParameterDefn("TerrainFriction", "Factor to reduce movement against terrain surface" ,
            0.5f,
            (s,cf,p,v) => { s.m_params[0].terrainFriction = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].terrainFriction; },
            (s,p,l,v) => { s.m_params[0].terrainFriction = v; s.TaintedUpdateParameter(p,l,v); } ),
        new ParameterDefn("TerrainHitFraction", "Distance to measure hit collisions" ,
            0.8f,
            (s,cf,p,v) => { s.m_params[0].terrainHitFraction = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].terrainHitFraction; },
            (s,p,l,v) => { s.m_params[0].terrainHitFraction = v; s.TaintedUpdateParameter(p,l,v); } ),
        new ParameterDefn("TerrainRestitution", "Bouncyness" ,
            0f,
            (s,cf,p,v) => { s.m_params[0].terrainRestitution = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].terrainRestitution; },
            (s,p,l,v) => { s.m_params[0].terrainRestitution = v; s.TaintedUpdateParameter(p,l,v); } ),
        new ParameterDefn("AvatarFriction", "Factor to reduce movement against an avatar. Changed on avatar recreation.",
            0.2f,
            (s,cf,p,v) => { s.m_params[0].avatarFriction = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].avatarFriction; },
            (s,p,l,v) => { s.UpdateParameterObject(ref s.m_params[0].avatarFriction, p, l, v); } ),
        new ParameterDefn("AvatarDensity", "Density of an avatar. Changed on avatar recreation.",
            60f,
            (s,cf,p,v) => { s.m_params[0].avatarDensity = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].avatarDensity; },
            (s,p,l,v) => { s.UpdateParameterObject(ref s.m_params[0].avatarDensity, p, l, v); } ),
        new ParameterDefn("AvatarRestitution", "Bouncyness. Changed on avatar recreation.",
            0f,
            (s,cf,p,v) => { s.m_params[0].avatarRestitution = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].avatarRestitution; },
            (s,p,l,v) => { s.UpdateParameterObject(ref s.m_params[0].avatarRestitution, p, l, v); } ),
        new ParameterDefn("AvatarCapsuleRadius", "Radius of space around an avatar",
            0.37f,
            (s,cf,p,v) => { s.m_params[0].avatarCapsuleRadius = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].avatarCapsuleRadius; },
            (s,p,l,v) => { s.UpdateParameterObject(ref s.m_params[0].avatarCapsuleRadius, p, l, v); } ),
        new ParameterDefn("AvatarCapsuleHeight", "Default height of space around avatar",
            1.5f,
            (s,cf,p,v) => { s.m_params[0].avatarCapsuleHeight = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].avatarCapsuleHeight; },
            (s,p,l,v) => { s.UpdateParameterObject(ref s.m_params[0].avatarCapsuleHeight, p, l, v); } ),
	    new ParameterDefn("AvatarContactProcessingThreshold", "Distance from capsule to check for collisions",
            0.1f,
            (s,cf,p,v) => { s.m_params[0].avatarContactProcessingThreshold = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].avatarContactProcessingThreshold; },
            (s,p,l,v) => { s.UpdateParameterObject(ref s.m_params[0].avatarContactProcessingThreshold, p, l, v); } ),


	    new ParameterDefn("MaxPersistantManifoldPoolSize", "Number of manifolds pooled (0 means default of 4096)",
            0f,     // zero to disable
            (s,cf,p,v) => { s.m_params[0].maxPersistantManifoldPoolSize = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].maxPersistantManifoldPoolSize; },
            (s,p,l,v) => { s.m_params[0].maxPersistantManifoldPoolSize = v; } ),
	    new ParameterDefn("MaxCollisionAlgorithmPoolSize", "Number of collisions pooled (0 means default of 4096)",
            0f,     // zero to disable
            (s,cf,p,v) => { s.m_params[0].maxCollisionAlgorithmPoolSize = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].maxCollisionAlgorithmPoolSize; },
            (s,p,l,v) => { s.m_params[0].maxCollisionAlgorithmPoolSize = v; } ),
	    new ParameterDefn("ShouldDisableContactPoolDynamicAllocation", "Enable to allow large changes in object count",
            ConfigurationParameters.numericFalse,
            (s,cf,p,v) => { s.m_params[0].shouldDisableContactPoolDynamicAllocation = s.NumericBool(cf.GetBoolean(p, s.BoolNumeric(v))); },
            (s) => { return s.m_params[0].shouldDisableContactPoolDynamicAllocation; },
            (s,p,l,v) => { s.m_params[0].shouldDisableContactPoolDynamicAllocation = v; } ),
	    new ParameterDefn("ShouldForceUpdateAllAabbs", "Enable to recomputer AABBs every simulator step",
            ConfigurationParameters.numericFalse,
            (s,cf,p,v) => { s.m_params[0].shouldForceUpdateAllAabbs = s.NumericBool(cf.GetBoolean(p, s.BoolNumeric(v))); },
            (s) => { return s.m_params[0].shouldForceUpdateAllAabbs; },
            (s,p,l,v) => { s.m_params[0].shouldForceUpdateAllAabbs = v; } ),
	    new ParameterDefn("ShouldRandomizeSolverOrder", "Enable for slightly better stacking interaction",
            ConfigurationParameters.numericFalse,
            (s,cf,p,v) => { s.m_params[0].shouldRandomizeSolverOrder = s.NumericBool(cf.GetBoolean(p, s.BoolNumeric(v))); },
            (s) => { return s.m_params[0].shouldRandomizeSolverOrder; },
            (s,p,l,v) => { s.m_params[0].shouldRandomizeSolverOrder = v; } ),
	    new ParameterDefn("ShouldSplitSimulationIslands", "Enable splitting active object scanning islands",
            ConfigurationParameters.numericTrue,
            (s,cf,p,v) => { s.m_params[0].shouldSplitSimulationIslands = s.NumericBool(cf.GetBoolean(p, s.BoolNumeric(v))); },
            (s) => { return s.m_params[0].shouldSplitSimulationIslands; },
            (s,p,l,v) => { s.m_params[0].shouldSplitSimulationIslands = v; } ),
	    new ParameterDefn("ShouldEnableFrictionCaching", "Enable friction computation caching",
            ConfigurationParameters.numericFalse,
            (s,cf,p,v) => { s.m_params[0].shouldEnableFrictionCaching = s.NumericBool(cf.GetBoolean(p, s.BoolNumeric(v))); },
            (s) => { return s.m_params[0].shouldEnableFrictionCaching; },
            (s,p,l,v) => { s.m_params[0].shouldEnableFrictionCaching = v; } ),
	    new ParameterDefn("NumberOfSolverIterations", "Number of internal iterations (0 means default)",
            0f,     // zero says use Bullet default
            (s,cf,p,v) => { s.m_params[0].numberOfSolverIterations = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].numberOfSolverIterations; },
            (s,p,l,v) => { s.m_params[0].numberOfSolverIterations = v; } ),

	    new ParameterDefn("LinkConstraintUseFrameOffset", "For linksets built with constraints, enable frame offsetFor linksets built with constraints, enable frame offset.",
            ConfigurationParameters.numericFalse,
            (s,cf,p,v) => { s.m_params[0].linkConstraintUseFrameOffset = s.NumericBool(cf.GetBoolean(p, s.BoolNumeric(v))); },
            (s) => { return s.m_params[0].linkConstraintUseFrameOffset; },
            (s,p,l,v) => { s.m_params[0].linkConstraintUseFrameOffset = v; } ),
	    new ParameterDefn("LinkConstraintEnableTransMotor", "Whether to enable translational motor on linkset constraints",
            ConfigurationParameters.numericTrue,
            (s,cf,p,v) => { s.m_params[0].linkConstraintEnableTransMotor = s.NumericBool(cf.GetBoolean(p, s.BoolNumeric(v))); },
            (s) => { return s.m_params[0].linkConstraintEnableTransMotor; },
            (s,p,l,v) => { s.m_params[0].linkConstraintEnableTransMotor = v; } ),
	    new ParameterDefn("LinkConstraintTransMotorMaxVel", "Maximum velocity to be applied by translational motor in linkset constraints",
            5.0f,
            (s,cf,p,v) => { s.m_params[0].linkConstraintTransMotorMaxVel = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].linkConstraintTransMotorMaxVel; },
            (s,p,l,v) => { s.m_params[0].linkConstraintTransMotorMaxVel = v; } ),
	    new ParameterDefn("LinkConstraintTransMotorMaxForce", "Maximum force to be applied by translational motor in linkset constraints",
            0.1f,
            (s,cf,p,v) => { s.m_params[0].linkConstraintTransMotorMaxForce = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].linkConstraintTransMotorMaxForce; },
            (s,p,l,v) => { s.m_params[0].linkConstraintTransMotorMaxForce = v; } ),
	    new ParameterDefn("LinkConstraintCFM", "Amount constraint can be violated. 0=no violation, 1=infinite. Default=0.1",
            0.1f,
            (s,cf,p,v) => { s.m_params[0].linkConstraintCFM = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].linkConstraintCFM; },
            (s,p,l,v) => { s.m_params[0].linkConstraintCFM = v; } ),
	    new ParameterDefn("LinkConstraintERP", "Amount constraint is corrected each tick. 0=none, 1=all. Default = 0.2",
            0.2f,
            (s,cf,p,v) => { s.m_params[0].linkConstraintERP = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].linkConstraintERP; },
            (s,p,l,v) => { s.m_params[0].linkConstraintERP = v; } ),
	    new ParameterDefn("LinkConstraintSolverIterations", "Number of solver iterations when computing constraint. (0 = Bullet default)",
            40,
            (s,cf,p,v) => { s.m_params[0].linkConstraintSolverIterations = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].linkConstraintSolverIterations; },
            (s,p,l,v) => { s.m_params[0].linkConstraintSolverIterations = v; } ),

        new ParameterDefn("DetailedStats", "Frames between outputting detailed phys stats. (0 is off)",
            0f,
            (s,cf,p,v) => { s.m_detailedStatsStep = cf.GetInt(p, (int)v); },
            (s) => { return (float)s.m_detailedStatsStep; },
            (s,p,l,v) => { s.m_detailedStatsStep = (int)v; } ),
    };

    // Convert a boolean to our numeric true and false values
    public float NumericBool(bool b)
    {
        return (b ? ConfigurationParameters.numericTrue : ConfigurationParameters.numericFalse);
    }

    // Convert numeric true and false values to a boolean
    public bool BoolNumeric(float b)
    {
        return (b == ConfigurationParameters.numericTrue ? true : false);
    }

    // Search through the parameter definitions and return the matching
    //    ParameterDefn structure.
    // Case does not matter as names are compared after converting to lower case.
    // Returns 'false' if the parameter is not found.
    private bool TryGetParameter(string paramName, out ParameterDefn defn)
    {
        bool ret = false;
        ParameterDefn foundDefn = new ParameterDefn();
        string pName = paramName.ToLower();

        foreach (ParameterDefn parm in ParameterDefinitions)
        {
            if (pName == parm.name.ToLower())
            {
                foundDefn = parm;
                ret = true;
                break;
            }
        }
        defn = foundDefn;
        return ret;
    }

    // Pass through the settable parameters and set the default values
    private void SetParameterDefaultValues()
    {
        foreach (ParameterDefn parm in ParameterDefinitions)
        {
            parm.setter(this, parm.name, PhysParameterEntry.APPLY_TO_NONE, parm.defaultValue);
        }
    }

    // Get user set values out of the ini file.
    private void SetParameterConfigurationValues(IConfig cfg)
    {
        foreach (ParameterDefn parm in ParameterDefinitions)
        {
            parm.userParam(this, cfg, parm.name, parm.defaultValue);
        }
    }

    private PhysParameterEntry[] SettableParameters = new PhysParameterEntry[1];

    // This creates an array in the correct format for returning the list of
    //    parameters. This is used by the 'list' option of the 'physics' command.
    private void BuildParameterTable()
    {
        if (SettableParameters.Length < ParameterDefinitions.Length)
        {

            List<PhysParameterEntry> entries = new List<PhysParameterEntry>();
            for (int ii = 0; ii < ParameterDefinitions.Length; ii++)
            {
                ParameterDefn pd = ParameterDefinitions[ii];
                entries.Add(new PhysParameterEntry(pd.name, pd.desc));
            }

            // make the list in alphabetical order for estetic reasons
            entries.Sort(delegate(PhysParameterEntry ppe1, PhysParameterEntry ppe2)
            {
                return ppe1.name.CompareTo(ppe2.name);
            });

            SettableParameters = entries.ToArray();
        }
    }


    #region IPhysicsParameters
    // Get the list of parameters this physics engine supports
    public PhysParameterEntry[] GetParameterList()
    {
        BuildParameterTable();
        return SettableParameters;
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
        ParameterDefn theParam;
        if (TryGetParameter(parm, out theParam))
        {
            theParam.setter(this, parm, localID, val);
            ret = true;
        }
        return ret;
    }

    // check to see if we are updating a parameter for a particular or all of the prims
    protected void UpdateParameterObject(ref float loc, string parm, uint localID, float val)
    {
        List<uint> operateOn;
        lock (PhysObjects) operateOn = new List<uint>(PhysObjects.Keys);
        UpdateParameterSet(operateOn, ref loc, parm, localID, val);
    }

    // update all the localIDs specified
    // If the local ID is APPLY_TO_NONE, just change the default value
    // If the localID is APPLY_TO_ALL change the default value and apply the new value to all the lIDs
    // If the localID is a specific object, apply the parameter change to only that object
    protected void UpdateParameterSet(List<uint> lIDs, ref float defaultLoc, string parm, uint localID, float val)
    {
        switch (localID)
        {
            case PhysParameterEntry.APPLY_TO_NONE:
                defaultLoc = val;   // setting only the default value
                break;
            case PhysParameterEntry.APPLY_TO_ALL:
                defaultLoc = val;  // setting ALL also sets the default value
                List<uint> objectIDs = lIDs;
                string xparm = parm.ToLower();
                float xval = val;
                TaintedObject("BSScene.UpdateParameterSet", delegate() {
                    foreach (uint lID in objectIDs)
                    {
                        BulletSimAPI.UpdateParameter(WorldID, lID, xparm, xval);
                    }
                });
                break;
            default:
                // setting only one localID
                TaintedUpdateParameter(parm, localID, val);
                break;
        }
    }

    // schedule the actual updating of the paramter to when the phys engine is not busy
    protected void TaintedUpdateParameter(string parm, uint localID, float val)
    {
        uint xlocalID = localID;
        string xparm = parm.ToLower();
        float xval = val;
        TaintedObject("BSScene.TaintedUpdateParameter", delegate() {
            BulletSimAPI.UpdateParameter(WorldID, xlocalID, xparm, xval);
        });
    }

    // Get parameter.
    // Return 'false' if not able to get the parameter.
    public bool GetPhysicsParameter(string parm, out float value)
    {
        float val = 0f;
        bool ret = false;
        ParameterDefn theParam;
        if (TryGetParameter(parm, out theParam))
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
    }
    // used to fill in the LocalID when there isn't one
    public const string DetailLogZero = "0000000000";

}
}
