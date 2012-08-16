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
// Debug linkset 
// Test with multiple regions in one simulator 
// Adjust character capsule size when height is adjusted (ScenePresence.SetHeight)
// Test sculpties
// Compute physics FPS reasonably
// Based on material, set density and friction
// More efficient memory usage when passing hull information from BSPrim to BulletSim
// Move all logic out of the C++ code and into the C# code for easier future modifications.
// Four states of prim: Physical, regular, phantom and selected. Are we modeling these correctly?
//     In SL one can set both physical and phantom (gravity, does not effect others, makes collisions with ground)
//     At the moment, physical and phantom causes object to drop through the terrain
// Physical phantom objects and related typing (collision options )
// Use collision masks for collision with terrain and phantom objects 
// Check out llVolumeDetect. Must do something for that.
// Should prim.link() and prim.delink() membership checking happen at taint time?
// changing the position and orientation of a linked prim must rebuild the constraint with the root.
// Mesh sharing. Use meshHash to tell if we already have a hull of that shape and only create once
// Do attachments need to be handled separately? Need collision events. Do not collide with VolumeDetect
// Implement the genCollisions feature in BulletSim::SetObjectProperties (don't pass up unneeded collisions)
// Implement LockAngularMotion
// Decide if clearing forces is the right thing to do when setting position (BulletSim::SetObjectTranslation)
// Does NeedsMeshing() really need to exclude all the different shapes?
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

    public void DebugLog(string mm, params Object[] xx) { if (ShouldDebugLog) m_log.DebugFormat(mm, xx); }

    public string BulletSimVersion = "?";

    private Dictionary<uint, BSCharacter> m_avatars = new Dictionary<uint, BSCharacter>();
    private Dictionary<uint, BSPrim> m_prims = new Dictionary<uint, BSPrim>();
    private HashSet<BSCharacter> m_avatarsWithCollisions = new HashSet<BSCharacter>();
    private HashSet<BSPrim> m_primsWithCollisions = new HashSet<BSPrim>();
    private List<BSPrim> m_vehicles = new List<BSPrim>();
    private float[] m_heightMap;
    private float m_waterLevel;
    private uint m_worldID;
    public uint WorldID { get { return m_worldID; } }

    // let my minuions use my logger
    public ILog Logger { get { return m_log; } }

    private bool m_initialized = false;

    private int m_detailedStatsStep = 0;

    public IMesher mesher;
    private float m_meshLOD;
    public float MeshLOD
    {
        get { return m_meshLOD; }
    }
    private float m_sculptLOD;
    public float SculptLOD
    {
        get { return m_sculptLOD; }
    }

    private BulletSim m_worldSim;
    public BulletSim World
    {
        get { return m_worldSim; }
    }
    private BSConstraintCollection m_constraintCollection;
    public BSConstraintCollection Constraints
    { 
        get { return m_constraintCollection; }
    }

    private int m_maxSubSteps;
    private float m_fixedTimeStep;
    private long m_simulationStep = 0;
    public long SimulationStep { get { return m_simulationStep; } }

    public float LastSimulatedTimestep { get; private set; }

    // A value of the time now so all the collision and update routines do not have to get their own
    // Set to 'now' just before all the prims and actors are called for collisions and updates
    private int m_simulationNowTime;
    public int SimulationNowTime { get { return m_simulationNowTime; } }

    private int m_maxCollisionsPerFrame;
    private CollisionDesc[] m_collisionArray;
    private GCHandle m_collisionArrayPinnedHandle;

    private int m_maxUpdatesPerFrame;
    private EntityProperties[] m_updateArray;
    private GCHandle m_updateArrayPinnedHandle;

    private bool _meshSculptedPrim = true;         // cause scuplted prims to get meshed
    private bool _forceSimplePrimMeshing = false;   // if a cube or sphere, let Bullet do internal shapes

    public float PID_D { get; private set; }    // derivative
    public float PID_P { get; private set; }    // proportional

    public const uint TERRAIN_ID = 0;       // OpenSim senses terrain with a localID of zero
    public const uint GROUNDPLANE_ID = 1;

    public ConfigurationParameters Params
    {
        get { return m_params[0]; }
    }
    public Vector3 DefaultGravity
    {
        get { return new Vector3(0f, 0f, Params.gravity); }
    }

    private float m_maximumObjectMass;
    public float MaximumObjectMass
    {
        get { return m_maximumObjectMass; }
    }

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
    private List<TaintCallbackEntry> _taintedObjects;
    private Object _taintLock = new Object();

    // A pointer to an instance if this structure is passed to the C++ code
    ConfigurationParameters[] m_params;
    GCHandle m_paramsHandle;

    public bool ShouldDebugLog { get; private set; }

    private BulletSimAPI.DebugLogCallback m_DebugLogCallbackHandle;

    // Sometimes you just have to log everything.
    public Logging.LogWriter PhysicsLogging;
    private bool m_physicsLoggingEnabled;
    private string m_physicsLoggingDir;
    private string m_physicsLoggingPrefix;
    private int m_physicsLoggingFileMinutes;

    private bool m_vehicleLoggingEnabled;
    public bool VehicleLoggingEnabled { get { return m_vehicleLoggingEnabled; } }

    public BSScene(string identifier)
    {
        m_initialized = false;
    }

    public override void Initialise(IMesher meshmerizer, IConfigSource config)
    {
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
        // can be left in and every call doesn't have to check for null.
        if (m_physicsLoggingEnabled)
        {
            PhysicsLogging = new Logging.LogWriter(m_physicsLoggingDir, m_physicsLoggingPrefix, m_physicsLoggingFileMinutes);
        }
        else
        {
            PhysicsLogging = new Logging.LogWriter();
        }

        // Get the version of the DLL
        // TODO: this doesn't work yet. Something wrong with marshaling the returned string.
        // BulletSimVersion = BulletSimAPI.GetVersion();
        // m_log.WarnFormat("{0}: BulletSim.dll version='{1}'", LogHeader, BulletSimVersion);

        // if Debug, enable logging from the unmanaged code
        if (m_log.IsDebugEnabled || PhysicsLogging.Enabled)
        {
            m_log.DebugFormat("{0}: Initialize: Setting debug callback for unmanaged code", LogHeader);
            if (PhysicsLogging.Enabled)
                m_DebugLogCallbackHandle = new BulletSimAPI.DebugLogCallback(BulletLoggerPhysLog);
            else
                m_DebugLogCallbackHandle = new BulletSimAPI.DebugLogCallback(BulletLogger);
            // the handle is saved in a variable to make sure it doesn't get freed after this call
            BulletSimAPI.SetDebugLogCallback(m_DebugLogCallbackHandle);
        }

        _taintedObjects = new List<TaintCallbackEntry>();

        mesher = meshmerizer;
        // The bounding box for the simulated world
        Vector3 worldExtent = new Vector3(Constants.RegionSize, Constants.RegionSize, 8192f);

        // m_log.DebugFormat("{0}: Initialize: Calling BulletSimAPI.Initialize.", LogHeader);
        m_worldID = BulletSimAPI.Initialize(worldExtent, m_paramsHandle.AddrOfPinnedObject(),
                                        m_maxCollisionsPerFrame, m_collisionArrayPinnedHandle.AddrOfPinnedObject(),
                                        m_maxUpdatesPerFrame, m_updateArrayPinnedHandle.AddrOfPinnedObject());

        // Initialization to support the transition to a new API which puts most of the logic
        //   into the C# code so it is easier to modify and add to.
        m_worldSim = new BulletSim(m_worldID, this, BulletSimAPI.GetSimHandle2(m_worldID));
        m_constraintCollection = new BSConstraintCollection(World);

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
                m_physicsLoggingPrefix = pConfig.GetString("PhysicsLoggingPrefix", "physics-");
                m_physicsLoggingFileMinutes = pConfig.GetInt("PhysicsLoggingFileMinutes", 5);
                // Very detailed logging for vehicle debugging
                m_vehicleLoggingEnabled = pConfig.GetBoolean("VehicleLoggingEnabled", false);
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
        lock (m_avatars) m_avatars.Add(localID, actor);
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
                lock (m_avatars) m_avatars.Remove(actor.LocalID);
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
            // DetailLog("{0},RemovePrim,call", bsprim.LocalID);
            // m_log.DebugFormat("{0}: RemovePrim. id={1}/{2}", LogHeader, bsprim.Name, bsprim.LocalID);
            try
            {
                lock (m_prims) m_prims.Remove(bsprim.LocalID);
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

        // DetailLog("{0},AddPrimShape,call", localID);

        BSPrim prim = new BSPrim(localID, primName, this, position, size, rotation, pbs, isPhysical);
        lock (m_prims) m_prims.Add(localID, prim);
        return prim;
    }

    // This is a call from the simulator saying that some physical property has been updated.
    // The BulletSim driver senses the changing of relevant properties so this taint 
    // information call is not needed.
    public override void AddPhysicsActorTaint(PhysicsActor prim) { }

    // Simulate one timestep
    public override float Simulate(float timeStep)
    {
        int updatedEntityCount = 0;
        IntPtr updatedEntitiesPtr;
        int collidersCount = 0;
        IntPtr collidersPtr;

        LastSimulatedTimestep = timeStep;

        // prevent simulation until we've been initialized
        if (!m_initialized) return 10.0f;

        int simulateStartTime = Util.EnvironmentTickCount();

        // update the prim states while we know the physics engine is not busy
        ProcessTaints();

        // Some of the prims operate with special vehicle properties
        ProcessVehicles(timeStep);
        ProcessTaints();    // the vehicles might have added taints

        // step the physical world one interval
        m_simulationStep++;
        int numSubSteps = 0;
        try
        {
            numSubSteps = BulletSimAPI.PhysicsStep(m_worldID, timeStep, m_maxSubSteps, m_fixedTimeStep,
                        out updatedEntityCount, out updatedEntitiesPtr, out collidersCount, out collidersPtr);
            // DetailLog("{0},Simulate,call, substeps={1}, updates={2}, colliders={3}", DetailLogZero, numSubSteps, updatedEntityCount, collidersCount); 
        }
        catch (Exception e)
        {
            m_log.WarnFormat("{0},PhysicsStep Exception: substeps={1}, updates={2}, colliders={3}, e={4}", LogHeader, numSubSteps, updatedEntityCount, collidersCount, e);
            // DetailLog("{0},PhysicsStepException,call, substeps={1}, updates={2}, colliders={3}", DetailLogZero, numSubSteps, updatedEntityCount, collidersCount);
            // updatedEntityCount = 0;
            collidersCount = 0;
        }


        // Don't have to use the pointers passed back since we know it is the same pinned memory we passed in

        // Get a value for 'now' so all the collision and update routines don't have to get their own
        m_simulationNowTime = Util.EnvironmentTickCount();

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

        // The above SendCollision's batch up the collisions on the objects.
        //      Now push the collisions into the simulator.
        foreach (BSPrim bsp in m_primsWithCollisions)
            bsp.SendCollisions();
        m_primsWithCollisions.Clear();

        // This is a kludge to get avatar movement updated. 
        //   Don't send collisions only if there were collisions -- send everytime.
        //   ODE sends collisions even if there are none and this is used to update
        //   avatar animations and stuff.
        // foreach (BSCharacter bsc in m_avatarsWithCollisions)
        //     bsc.SendCollisions();
        foreach (KeyValuePair<uint, BSCharacter> kvp in m_avatars)
            kvp.Value.SendCollisions();
        m_avatarsWithCollisions.Clear();

        // If any of the objects had updated properties, tell the object it has been changed by the physics engine
        if (updatedEntityCount > 0)
        {
            for (int ii = 0; ii < updatedEntityCount; ii++)
            {
                EntityProperties entprop = m_updateArray[ii];
                BSPrim prim;
                if (m_prims.TryGetValue(entprop.ID, out prim))
                {
                    prim.UpdateProperties(entprop);
                    continue;
                }
                BSCharacter actor;
                if (m_avatars.TryGetValue(entprop.ID, out actor))
                {
                    actor.UpdateProperties(entprop);
                    continue;
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

        // this is a waste since the outside routine also calcuates the physics simulation
        //   period. TODO: There should be a way of computing physics frames from simulator computation.
        // long simulateTotalTime = Util.EnvironmentTickCountSubtract(simulateStartTime);
        // return (timeStep * (float)simulateTotalTime);

        // TODO: FIX THIS: fps calculation possibly wrong.
        // This calculation says 1/timeStep is the ideal frame rate. Any time added to
        //    that by the physics simulation gives a slower frame rate.
        long totalSimulationTime = Util.EnvironmentTickCountSubtract(simulateStartTime);
        if (totalSimulationTime >= timeStep)
            return 0;
        return 1f / (timeStep + totalSimulationTime);
    }

    // Something has collided
    private void SendCollision(uint localID, uint collidingWith, Vector3 collidePoint, Vector3 collideNormal, float penitration)
    {
        if (localID == TERRAIN_ID || localID == GROUNDPLANE_ID)
        {
            return;         // don't send collisions to the terrain
        }

        ActorTypes type = ActorTypes.Prim;
        if (collidingWith == TERRAIN_ID || collidingWith == GROUNDPLANE_ID)
            type = ActorTypes.Ground;
        else if (m_avatars.ContainsKey(collidingWith))
            type = ActorTypes.Agent;

        BSPrim prim;
        if (m_prims.TryGetValue(localID, out prim)) {
            prim.Collide(collidingWith, type, collidePoint, collideNormal, penitration);
            m_primsWithCollisions.Add(prim);
            return;
        }
        BSCharacter actor;
        if (m_avatars.TryGetValue(localID, out actor)) {
            actor.Collide(collidingWith, type, collidePoint, collideNormal, penitration);
            m_avatarsWithCollisions.Add(actor);
            return;
        }
        return;
    }

    public override void GetResults() { }

    public override void SetTerrain(float[] heightMap) {
        m_heightMap = heightMap;
        this.TaintedObject("BSScene.SetTerrain", delegate()
        {
            BulletSimAPI.SetHeightmap(m_worldID, m_heightMap);
        });
    }

    // Someday we will have complex terrain with caves and tunnels
    // For the moment, it's flat and convex
    public float GetTerrainHeightAtXYZ(Vector3 loc)
    {
        return GetTerrainHeightAtXY(loc.X, loc.Y);
    }

    public float GetTerrainHeightAtXY(float tX, float tY)
    {
        if (tX < 0 || tX >= Constants.RegionSize || tY < 0 || tY >= Constants.RegionSize)
            return 30;
        return m_heightMap[((int)tX) * Constants.RegionSize + ((int)tY)];
    }

    public override void SetWaterLevel(float baseheight) 
    {
        m_waterLevel = baseheight;
        // TODO: pass to physics engine so things will float?
    }
    public float GetWaterLevel()
    {
        return m_waterLevel;
    }

    public override void DeleteTerrain() 
    {
        // m_log.DebugFormat("{0}: DeleteTerrain()", LogHeader);
    }

    public override void Dispose()
    {
        // m_log.DebugFormat("{0}: Dispose()", LogHeader);

        // make sure no stepping happens while we're deleting stuff
        m_initialized = false;

        foreach (KeyValuePair<uint, BSCharacter> kvp in m_avatars)
        {
            kvp.Value.Destroy();
        }
        m_avatars.Clear();

        foreach (KeyValuePair<uint, BSPrim> kvp in m_prims)
        {
            kvp.Value.Destroy();
        }
        m_prims.Clear();

        // Now that the prims are all cleaned up, there should be no constraints left
        if (m_constraintCollection != null)
        {
            m_constraintCollection.Dispose();
            m_constraintCollection = null;
        }

        // Anything left in the unmanaged code should be cleaned out
        BulletSimAPI.Shutdown(WorldID);

        // Not logging any more
        PhysicsLogging.Close();
    }

    public override Dictionary<uint, float> GetTopColliders()
    {
        return new Dictionary<uint, float>();
    }

    public override bool IsThreaded { get { return false;  } }

    /// <summary>
    /// Routine to figure out if we need to mesh this prim with our mesher
    /// </summary>
    /// <param name="pbs"></param>
    /// <returns>true if the prim needs meshing</returns>
    public bool NeedsMeshing(PrimitiveBaseShape pbs)
    {
        // most of this is redundant now as the mesher will return null if it cant mesh a prim
        // but we still need to check for sculptie meshing being enabled so this is the most
        // convenient place to do it for now...

        // int iPropertiesNotSupportedDefault = 0;

        if (pbs.SculptEntry && !_meshSculptedPrim)
        {
            // Render sculpties as boxes
            return false;
        }

        // if it's a standard box or sphere with no cuts, hollows, twist or top shear, return false since Bullet 
        // can use an internal representation for the prim
        if (!_forceSimplePrimMeshing)
        {
            if ((pbs.ProfileShape == ProfileShape.Square && pbs.PathCurve == (byte)Extrusion.Straight)
                || (pbs.ProfileShape == ProfileShape.HalfCircle && pbs.PathCurve == (byte)Extrusion.Curve1
                        && pbs.Scale.X == pbs.Scale.Y && pbs.Scale.Y == pbs.Scale.Z))
            {

                if (pbs.ProfileBegin == 0 && pbs.ProfileEnd == 0
                    && pbs.ProfileHollow == 0
                    && pbs.PathTwist == 0 && pbs.PathTwistBegin == 0
                    && pbs.PathBegin == 0 && pbs.PathEnd == 0
                    && pbs.PathTaperX == 0 && pbs.PathTaperY == 0
                    && pbs.PathScaleX == 100 && pbs.PathScaleY == 100
                    && pbs.PathShearX == 0 && pbs.PathShearY == 0)
                {
                    return false;
                }
            }
        }

        /*  TODO: verify that the mesher will now do all these shapes
        if (pbs.ProfileHollow != 0)
            iPropertiesNotSupportedDefault++;

        if ((pbs.PathBegin != 0) || pbs.PathEnd != 0)
            iPropertiesNotSupportedDefault++;

        if ((pbs.PathTwistBegin != 0) || (pbs.PathTwist != 0))
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
            return false;
        }
         */
        return true; 
    }

    // Calls to the PhysicsActors can't directly call into the physics engine
    // because it might be busy. We delay changes to a known time.
    // We rely on C#'s closure to save and restore the context for the delegate.
    public void TaintedObject(String ident, TaintCallback callback)
    {
        if (!m_initialized) return;

        lock (_taintLock)
            _taintedObjects.Add(new TaintCallbackEntry(ident, callback));
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
        foreach (BSPrim prim in m_vehicles)
        {
            prim.StepVehicle(timeStep);
        }
    }
    #endregion Vehicles

    #region Parameters

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
            (s,cf,p,v) => { s._meshSculptedPrim = cf.GetBoolean(p, s.BoolNumeric(v)); },
            (s) => { return s.NumericBool(s._meshSculptedPrim); },
            (s,p,l,v) => { s._meshSculptedPrim = s.BoolNumeric(v); } ),
        new ParameterDefn("ForceSimplePrimMeshing", "If true, only use primitive meshes for objects",
            ConfigurationParameters.numericFalse,
            (s,cf,p,v) => { s._forceSimplePrimMeshing = cf.GetBoolean(p, s.BoolNumeric(v)); },
            (s) => { return s.NumericBool(s._forceSimplePrimMeshing); },
            (s,p,l,v) => { s._forceSimplePrimMeshing = s.BoolNumeric(v); } ),

        new ParameterDefn("MeshLOD", "Level of detail to render meshes (32, 16, 8 or 4. 32=most detailed)",
            8f,
            (s,cf,p,v) => { s.m_meshLOD = cf.GetInt(p, (int)v); },
            (s) => { return (float)s.m_meshLOD; },
            (s,p,l,v) => { s.m_meshLOD = (int)v; } ),
        new ParameterDefn("SculptLOD", "Level of detail to render sculpties (32, 16, 8 or 4. 32=most detailed)",
            32f,
            (s,cf,p,v) => { s.m_sculptLOD = cf.GetInt(p, (int)v); },
            (s) => { return (float)s.m_sculptLOD; },
            (s,p,l,v) => { s.m_sculptLOD = (int)v; } ),

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
            (s,cf,p,v) => { s.m_maximumObjectMass = cf.GetFloat(p, v); },
            (s) => { return (float)s.m_maximumObjectMass; },
            (s,p,l,v) => { s.m_maximumObjectMass = v; } ),

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
            (s,p,l,v) => { s.UpdateParameterPrims(ref s.m_params[0].linearDamping, p, l, v); } ),
        new ParameterDefn("AngularDamping", "Factor to damp angular movement per second (0.0 - 1.0)",
            0f,
            (s,cf,p,v) => { s.m_params[0].angularDamping = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].angularDamping; },
            (s,p,l,v) => { s.UpdateParameterPrims(ref s.m_params[0].angularDamping, p, l, v); } ),
        new ParameterDefn("DeactivationTime", "Seconds before considering an object potentially static",
            0.2f,
            (s,cf,p,v) => { s.m_params[0].deactivationTime = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].deactivationTime; },
            (s,p,l,v) => { s.UpdateParameterPrims(ref s.m_params[0].deactivationTime, p, l, v); } ),
        new ParameterDefn("LinearSleepingThreshold", "Seconds to measure linear movement before considering static",
            0.8f,
            (s,cf,p,v) => { s.m_params[0].linearSleepingThreshold = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].linearSleepingThreshold; },
            (s,p,l,v) => { s.UpdateParameterPrims(ref s.m_params[0].linearSleepingThreshold, p, l, v); } ),
        new ParameterDefn("AngularSleepingThreshold", "Seconds to measure angular movement before considering static",
            1.0f,
            (s,cf,p,v) => { s.m_params[0].angularSleepingThreshold = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].angularSleepingThreshold; },
            (s,p,l,v) => { s.UpdateParameterPrims(ref s.m_params[0].angularSleepingThreshold, p, l, v); } ),
        new ParameterDefn("CcdMotionThreshold", "Continuious collision detection threshold (0 means no CCD)" ,
            0f,     // set to zero to disable
            (s,cf,p,v) => { s.m_params[0].ccdMotionThreshold = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].ccdMotionThreshold; },
            (s,p,l,v) => { s.UpdateParameterPrims(ref s.m_params[0].ccdMotionThreshold, p, l, v); } ),
        new ParameterDefn("CcdSweptSphereRadius", "Continuious collision detection test radius" ,
            0f,
            (s,cf,p,v) => { s.m_params[0].ccdSweptSphereRadius = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].ccdSweptSphereRadius; },
            (s,p,l,v) => { s.UpdateParameterPrims(ref s.m_params[0].ccdSweptSphereRadius, p, l, v); } ),
        new ParameterDefn("ContactProcessingThreshold", "Distance between contacts before doing collision check" ,
            0.1f,
            (s,cf,p,v) => { s.m_params[0].contactProcessingThreshold = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].contactProcessingThreshold; },
            (s,p,l,v) => { s.UpdateParameterPrims(ref s.m_params[0].contactProcessingThreshold, p, l, v); } ),

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
            0.5f,
            (s,cf,p,v) => { s.m_params[0].avatarFriction = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].avatarFriction; },
            (s,p,l,v) => { s.UpdateParameterAvatars(ref s.m_params[0].avatarFriction, p, l, v); } ),
        new ParameterDefn("AvatarDensity", "Density of an avatar. Changed on avatar recreation.",
            60f,
            (s,cf,p,v) => { s.m_params[0].avatarDensity = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].avatarDensity; },
            (s,p,l,v) => { s.UpdateParameterAvatars(ref s.m_params[0].avatarDensity, p, l, v); } ),
        new ParameterDefn("AvatarRestitution", "Bouncyness. Changed on avatar recreation.",
            0f,
            (s,cf,p,v) => { s.m_params[0].avatarRestitution = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].avatarRestitution; },
            (s,p,l,v) => { s.UpdateParameterAvatars(ref s.m_params[0].avatarRestitution, p, l, v); } ),
        new ParameterDefn("AvatarCapsuleRadius", "Radius of space around an avatar",
            0.37f,
            (s,cf,p,v) => { s.m_params[0].avatarCapsuleRadius = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].avatarCapsuleRadius; },
            (s,p,l,v) => { s.UpdateParameterAvatars(ref s.m_params[0].avatarCapsuleRadius, p, l, v); } ),
        new ParameterDefn("AvatarCapsuleHeight", "Default height of space around avatar",
            1.5f,
            (s,cf,p,v) => { s.m_params[0].avatarCapsuleHeight = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].avatarCapsuleHeight; },
            (s,p,l,v) => { s.UpdateParameterAvatars(ref s.m_params[0].avatarCapsuleHeight, p, l, v); } ),
	    new ParameterDefn("AvatarContactProcessingThreshold", "Distance from capsule to check for collisions",
            0.1f,
            (s,cf,p,v) => { s.m_params[0].avatarContactProcessingThreshold = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].avatarContactProcessingThreshold; },
            (s,p,l,v) => { s.UpdateParameterAvatars(ref s.m_params[0].avatarContactProcessingThreshold, p, l, v); } ),


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
            ConfigurationParameters.numericFalse,
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
	    new ParameterDefn("LinkConstraintCFM", "Amount constraint can be violated. 0=none, 1=all. Default=0",
            0.0f,
            (s,cf,p,v) => { s.m_params[0].linkConstraintCFM = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].linkConstraintCFM; },
            (s,p,l,v) => { s.m_params[0].linkConstraintCFM = v; } ),
	    new ParameterDefn("LinkConstraintERP", "Amount constraint is corrected each tick. 0=none, 1=all. Default = 0.2",
            0.2f,
            (s,cf,p,v) => { s.m_params[0].linkConstraintERP = cf.GetFloat(p, v); },
            (s) => { return s.m_params[0].linkConstraintERP; },
            (s,p,l,v) => { s.m_params[0].linkConstraintERP = v; } ),

        new ParameterDefn("DetailedStats", "Frames between outputting detailed phys stats. (0 is off)",
            0f,
            (s,cf,p,v) => { s.m_detailedStatsStep = cf.GetInt(p, (int)v); },
            (s) => { return (float)s.m_detailedStatsStep; },
            (s,p,l,v) => { s.m_detailedStatsStep = (int)v; } ),
        new ParameterDefn("ShouldDebugLog", "Enables detailed DEBUG log statements",
            ConfigurationParameters.numericFalse,
            (s,cf,p,v) => { s.ShouldDebugLog = cf.GetBoolean(p, s.BoolNumeric(v)); },
            (s) => { return s.NumericBool(s.ShouldDebugLog); },
            (s,p,l,v) => { s.ShouldDebugLog = s.BoolNumeric(v); } ),

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
    protected void UpdateParameterPrims(ref float loc, string parm, uint localID, float val)
    {
        List<uint> operateOn;
        lock (m_prims) operateOn = new List<uint>(m_prims.Keys);
        UpdateParameterSet(operateOn, ref loc, parm, localID, val);
    }

    // check to see if we are updating a parameter for a particular or all of the avatars
    protected void UpdateParameterAvatars(ref float loc, string parm, uint localID, float val)
    {
        List<uint> operateOn;
        lock (m_avatars) operateOn = new List<uint>(m_avatars.Keys);
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
                        BulletSimAPI.UpdateParameter(m_worldID, lID, xparm, xval);
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
            BulletSimAPI.UpdateParameter(m_worldID, xlocalID, xparm, xval);
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
